using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Andastra.Parsing;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Resource;
using KotorCLI.Configuration;
using KotorCLI.Logging;
using Tomlyn.Model;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Pack command - Convert, compile, and pack all sources for target.
    /// </summary>
    public static class PackCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var packCommand = new Command("pack", "Convert, compile, and pack all sources for target");
            var targetsArgument = new Argument<string[]>("targets", () => Array.Empty<string>(), "Targets to pack (use 'all' for all targets)");
            packCommand.Add(targetsArgument);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before packing");
            packCommand.Options.Add(cleanOption);
            var noConvertOption = new Option<bool>("--noConvert", "Do not convert updated json files");
            packCommand.Options.Add(noConvertOption);
            var noCompileOption = new Option<bool>("--noCompile", "Do not recompile updated scripts");
            packCommand.Options.Add(noCompileOption);

            packCommand.SetAction(parseResult =>
            {
                var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                var clean = parseResult.GetValue(cleanOption);
                var noConvert = parseResult.GetValue(noConvertOption);
                var noCompile = parseResult.GetValue(noCompileOption);

                var logger = new StandardLogger();
                var exitCode = Execute(targets, clean, noConvert, noCompile, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(packCommand);
        }

        private static int Execute(string[] targetNames, bool clean, bool noConvert, bool noCompile, ILogger logger)
        {
            // Pack command orchestrates: convert -> compile -> pack
            // Matching PyKotor implementation at Tools/KotorCLI/src/kotorcli/commands/pack.py

            // Load configuration
            var configPath = ConfigFileFinder.FindConfigFile();
            if (configPath == null)
            {
                logger.Error("This is not a kotorcli repository. Please run 'kotorcli init'");
                return 1;
            }

            try
            {
                var config = new KotorCLIConfig(configPath);
                var rootDir = Path.GetDirectoryName(configPath);

                // Determine targets
                List<TomlTable> targets;
                if (targetNames.Length == 0 || targetNames.Contains("all"))
                {
                    targets = config.GetTargets();
                    if (targets.Count == 0)
                    {
                        logger.Error("No targets found in configuration");
                        return 1;
                    }
                }
                else
                {
                    targets = new List<TomlTable>();
                    foreach (var name in targetNames)
                    {
                        var target = config.GetTarget(name);
                        if (target == null)
                        {
                            logger.Error(name != null ? $"Target not found: {name}" : "No default target found");
                            return 1;
                        }
                        targets.Add(target);
                    }
                }

                // Process each target
                foreach (var target in targets)
                {
                    var targetName = GetTomlValue<string>(target, "name") ?? "unnamed";
                    logger.Info($"Packing target: {targetName}");

                    // Run convert unless --noConvert
                    if (!noConvert)
                    {
                        logger.Info("Converting JSON to GFF...");
                        int convertResult = ConvertCommand.Execute(targetNames, clean, logger);
                        if (convertResult != 0)
                        {
                            logger.Error("Convert failed");
                            return convertResult;
                        }
                    }

                    // Run compile unless --noCompile
                    if (!noCompile)
                    {
                        logger.Info("Compiling scripts...");
                        int compileResult = CompileCommand.Execute(targetNames, clean, null, null, logger);
                        if (compileResult != 0)
                        {
                            logger.Warning("Some scripts failed to compile, continuing with pack");
                            // Don't abort on compile errors unless explicitly requested
                        }
                    }

                    // Get cache directory
                    var cacheDir = Path.Combine(rootDir, ".kotorcli", "cache", targetName);
                    if (!Directory.Exists(cacheDir))
                    {
                        logger.Error($"Cache directory not found: {cacheDir}");
                        logger.Info("Run 'kotorcli convert' and 'kotorcli compile' first");
                        return 1;
                    }

                    // Determine output file
                    var outputFilename = config.ResolveTargetValue(target, "file");
                    if (outputFilename == null || !(outputFilename is string outputFileStr))
                    {
                        logger.Error("No output file specified for target");
                        return 1;
                    }
                    var outputFile = Path.IsPathRooted(outputFileStr) ? outputFileStr : Path.Combine(rootDir, outputFileStr);

                    logger.Info($"Output file: {outputFile}");

                    // Check if output file exists and should be overwritten
                    if (File.Exists(outputFile))
                    {
                        // Find newest source file to compare timestamps
                        var sources = config.GetTargetSources(target);
                        var includePatterns = sources.ContainsKey("include") ? sources["include"] : new List<string>();
                        DateTime newestSourceTime = DateTime.MinValue;

                        foreach (var pattern in includePatterns)
                        {
                            var matches = FindFilesMatchingPattern(rootDir, pattern);
                            foreach (var match in matches)
                            {
                                if (File.Exists(match))
                                {
                                    var fileTime = File.GetLastWriteTime(match);
                                    if (fileTime > newestSourceTime)
                                    {
                                        newestSourceTime = fileTime;
                                    }
                                }
                            }
                        }

                        var outputTime = File.GetLastWriteTime(outputFile);
                        if (newestSourceTime > outputTime)
                        {
                            logger.Info("Source files are newer than output, will overwrite");
                        }
                        else
                        {
                            logger.Info("Output file is up to date, skipping pack");
                            continue;
                        }
                    }

                    // Collect files from cache
                    var cacheFiles = Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories)
                        .Where(f => File.Exists(f))
                        .ToList();

                    // Apply filter patterns
                    var sources2 = config.GetTargetSources(target);
                    var filterPatterns = sources2.ContainsKey("filter") ? sources2["filter"] : new List<string>();
                    var filteredFiles = new List<string>();

                    foreach (var cacheFile in cacheFiles)
                    {
                        var fileName = Path.GetFileName(cacheFile);
                        bool shouldFilter = false;

                        foreach (var pattern in filterPatterns)
                        {
                            if (MatchPattern(fileName, pattern))
                            {
                                shouldFilter = true;
                                logger.Debug($"Filtering out: {fileName}");
                                break;
                            }
                        }

                        if (!shouldFilter)
                        {
                            filteredFiles.Add(cacheFile);
                        }
                    }

                    logger.Info($"Packing {filteredFiles.Count} files");

                    if (filteredFiles.Count == 0)
                    {
                        logger.Warning("No files to pack");
                        continue;
                    }

                    // Determine archive type from file extension
                    var extension = Path.GetExtension(outputFile).ToLowerInvariant().TrimStart('.');
                    ERFType erfType;
                    ResourceType fileFormat;

                    if (extension == "mod" || extension == "erf" || extension == "sav")
                    {
                        // Create ERF archive
                        try
                        {
                            if (extension == "mod")
                            {
                                erfType = ERFType.MOD;
                                fileFormat = ResourceType.MOD;
                            }
                            else if (extension == "sav")
                            {
                                erfType = ERFType.MOD; // SAV files use MOD format
                                fileFormat = ResourceType.SAV;
                            }
                            else
                            {
                                erfType = ERFType.ERF;
                                fileFormat = ResourceType.ERF;
                            }

                            var erf = new ERF(erfType);

                            // Add files to ERF
                            foreach (var cacheFile in filteredFiles)
                            {
                                try
                                {
                                    // Determine resref and restype from filename
                                    var fileName = Path.GetFileName(cacheFile);
                                    var stem = Path.GetFileNameWithoutExtension(cacheFile);
                                    var ext = Path.GetExtension(cacheFile).TrimStart('.');

                                    // Handle files with multiple extensions (e.g., "module.ifo.json" -> "module.ifo")
                                    string resref;
                                    string actualExt = ext;

                                    // Check if stem contains a dot (indicating multiple extensions)
                                    if (stem.Contains("."))
                                    {
                                        var parts = stem.Split('.');
                                        if (parts.Length >= 2)
                                        {
                                            // Last part before the final extension is the resource extension
                                            actualExt = parts[parts.Length - 1];
                                            resref = string.Join(".", parts.Take(parts.Length - 1));
                                        }
                                        else
                                        {
                                            resref = stem;
                                        }
                                    }
                                    else
                                    {
                                        resref = stem;
                                    }

                                    // Get ResourceType from extension
                                    ResourceType restype;
                                    try
                                    {
                                        restype = ResourceType.FromExtension(actualExt);
                                        if (restype.IsInvalid)
                                        {
                                            logger.Warning($"Unknown resource type for {fileName}, skipping");
                                            continue;
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        logger.Warning($"Failed to determine resource type for {fileName}, skipping");
                                        continue;
                                    }

                                    // Read file data
                                    byte[] fileData = File.ReadAllBytes(cacheFile);

                                    // Add to ERF
                                    erf.SetData(resref, restype, fileData);
                                }
                                catch (Exception ex)
                                {
                                    logger.Warning($"Failed to add {Path.GetFileName(cacheFile)} to ERF: {ex.Message}");
                                    continue;
                                }
                            }

                            // Write ERF
                            var outputDir = Path.GetDirectoryName(outputFile);
                            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
                            {
                                Directory.CreateDirectory(outputDir);
                            }

                            ERFAuto.WriteErf(erf, outputFile, fileFormat);
                            logger.Info($"Successfully packed {outputFile}");
                        }
                        catch (Exception ex)
                        {
                            logger.Error($"Failed to pack ERF: {ex.Message}");
                            if (logger.IsDebug)
                            {
                                logger.Debug($"Stack trace: {ex.StackTrace}");
                            }
                            return 1;
                        }
                    }
                    else if (extension == "rim")
                    {
                        logger.Error("RIM packing not yet implemented");
                        return 1;
                    }
                    else
                    {
                        logger.Error($"Unsupported output file type: {extension}");
                        return 1;
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to pack: {ex.Message}");
                if (logger.IsDebug)
                {
                    logger.Debug($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        /// <summary>
        /// Find files matching a glob pattern.
        /// Matching PyKotor: glob.glob() behavior
        /// </summary>
        private static List<string> FindFilesMatchingPattern(string rootDir, string pattern)
        {
            var results = new List<string>();

            try
            {
                // Handle different pattern types
                if (pattern.Contains("**"))
                {
                    // Recursive pattern - search all subdirectories
                    var searchPattern = pattern.Replace("**/", "").Replace("/**", "").Replace("**", "*");
                    if (Directory.Exists(rootDir))
                    {
                        // Expand ** to recursive search
                        var basePattern = pattern;
                        if (basePattern.StartsWith("**/"))
                        {
                            basePattern = basePattern.Substring(3);
                        }
                        var filePattern = Path.GetFileName(basePattern);
                        var dirPattern = Path.GetDirectoryName(basePattern);

                        if (string.IsNullOrEmpty(dirPattern) || dirPattern == ".")
                        {
                            // Search all directories
                            results.AddRange(Directory.GetFiles(rootDir, filePattern, SearchOption.AllDirectories));
                        }
                        else
                        {
                            // Search in specific subdirectory pattern
                            var searchDirs = Directory.GetDirectories(rootDir, dirPattern, SearchOption.AllDirectories);
                            foreach (var dir in searchDirs)
                            {
                                results.AddRange(Directory.GetFiles(dir, filePattern, SearchOption.TopDirectoryOnly));
                            }
                        }
                    }
                }
                else if (pattern.Contains("*") || pattern.Contains("?"))
                {
                    // Simple wildcard pattern
                    var directory = Path.GetDirectoryName(Path.Combine(rootDir, pattern));
                    var filePattern = Path.GetFileName(pattern);

                    if (string.IsNullOrEmpty(directory) || directory == ".")
                    {
                        directory = rootDir;
                    }

                    if (Directory.Exists(directory))
                    {
                        results.AddRange(Directory.GetFiles(directory, filePattern, SearchOption.TopDirectoryOnly));
                    }
                }
                else
                {
                    // Exact file path
                    var fullPath = Path.Combine(rootDir, pattern);
                    if (File.Exists(fullPath))
                    {
                        results.Add(fullPath);
                    }
                }
            }
            catch (Exception)
            {
                // Ignore errors and continue
            }

            return results;
        }

        /// <summary>
        /// Match a path against a glob pattern.
        /// Matching PyKotor: fnmatch.fnmatch() behavior
        /// </summary>
        private static bool MatchPattern(string path, string pattern)
        {
            // Simple pattern matching - convert glob pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Get a value from a TOML table with type conversion.
        /// </summary>
        private static T GetTomlValue<T>(TomlTable table, string key)
        {
            if (table.TryGetValue(key, out object value))
            {
                if (value is T direct)
                {
                    return direct;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            return default(T);
        }
    }
}
