using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.NCS;
using Andastra.Parsing.Formats.NCS.Compiler;
using KotorCLI.Configuration;
using KotorCLI.Logging;
using Tomlyn.Model;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Compile command - Compile all nss sources for target.
    /// Matching PyKotor implementation at Tools/KotorCLI/src/kotorcli/commands/compile.py
    /// </summary>
    public static class CompileCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var compileCommand = new Command("compile", "Compile all nss sources for target");
            var targetsArgument = new Argument<string[]>("targets");
            targetsArgument.Description = "Targets to compile (use 'all' for all targets)";
            compileCommand.Add(targetsArgument);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before compiling");
            compileCommand.Options.Add(cleanOption);
            var fileOption = new Option<string[]>("--file", "Compile specific file(s)");
            compileCommand.Options.Add(fileOption);
            var skipCompileOption = new Option<string[]>("--skipCompile", "Don't compile specific file(s)");
            compileCommand.Options.Add(skipCompileOption);

            compileCommand.SetAction(parseResult =>
            {
                var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                var clean = parseResult.GetValue(cleanOption);
                var files = parseResult.GetValue(fileOption);
                var skipCompile = parseResult.GetValue(skipCompileOption);

                var logger = new StandardLogger();
                var exitCode = Execute(targets, clean, files, skipCompile, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(compileCommand);
        }

        internal static int Execute(string[] targetNames, bool clean, string[] files, string[] skipCompile, ILogger logger)
        {
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

                // Check for external compiler (optional)
                string externalCompiler = FindNssCompiler();
                bool useExternal = !string.IsNullOrEmpty(externalCompiler);

                if (useExternal)
                {
                    logger.Info($"Using external compiler: {externalCompiler}");
                }
                else
                {
                    logger.Info("Using built-in NSS compiler");
                }

                // Determine game version (default to K2 which is more compatible)
                BioWareGame game = GetGameFromConfig(config);
                logger.Debug($"Compiling for game: {(game.IsK1() ? "K1" : "K2")}");

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
                    logger.Info($"Compiling target: {targetName}");

                    // Get cache directory
                    var cacheDir = Path.Combine(rootDir, ".kotorcli", "cache", targetName);
                    if (clean && Directory.Exists(cacheDir))
                    {
                        logger.Info($"Cleaning cache: {cacheDir}");
                        Directory.Delete(cacheDir, true);
                    }
                    Directory.CreateDirectory(cacheDir);

                    // Get source patterns
                    var sources = config.GetTargetSources(target);
                    var includePatterns = sources.ContainsKey("include") ? sources["include"] : new List<string>();
                    var excludePatterns = sources.ContainsKey("exclude") ? sources["exclude"] : new List<string>();
                    var skipCompilePatterns = sources.ContainsKey("skipCompile") ? sources["skipCompile"] : new List<string>();

                    // Add command-line skipCompile patterns
                    if (skipCompile != null && skipCompile.Length > 0)
                    {
                        skipCompilePatterns.AddRange(skipCompile);
                    }

                    // Find NSS files to compile
                    var nssFiles = new List<string>();
                    if (files != null && files.Length > 0)
                    {
                        // Specific files specified
                        foreach (var fileSpec in files)
                        {
                            var filePath = Path.IsPathRooted(fileSpec) ? fileSpec : Path.Combine(rootDir, fileSpec);
                            if (File.Exists(filePath) && filePath.EndsWith(".nss", StringComparison.OrdinalIgnoreCase))
                            {
                                nssFiles.Add(filePath);
                            }
                            else
                            {
                                // Try to find by name
                                foreach (var pattern in includePatterns)
                                {
                                    var patternPath = Path.Combine(rootDir, pattern);
                                    var matches = FindFilesMatchingPattern(rootDir, pattern);
                                    foreach (var match in matches)
                                    {
                                        var matchPath = new FileInfo(match);
                                        if (matchPath.Name == fileSpec || matchPath.Name == fileSpec + ".nss")
                                        {
                                            nssFiles.Add(match);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Find all NSS files matching patterns
                        foreach (var pattern in includePatterns)
                        {
                            var expandedPattern = config.ResolveTargetValue(target, "sources", pattern);
                            if (expandedPattern is string patternStr)
                            {
                                var matches = FindFilesMatchingPattern(rootDir, patternStr);
                                foreach (var match in matches)
                                {
                                    var matchPath = new FileInfo(match);
                                    if (matchPath.Extension.Equals(".nss", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Check against exclude patterns
                                        bool excluded = false;
                                        foreach (var excludePattern in excludePatterns)
                                        {
                                            var expandedExclude = config.ResolveTargetValue(target, "sources", excludePattern);
                                            if (expandedExclude is string excludeStr && MatchPattern(match, Path.Combine(rootDir, excludeStr)))
                                            {
                                                excluded = true;
                                                break;
                                            }
                                        }

                                        // Check against skipCompile patterns
                                        if (!excluded)
                                        {
                                            foreach (var skipPattern in skipCompilePatterns)
                                            {
                                                if (MatchPattern(matchPath.Name, skipPattern))
                                                {
                                                    excluded = true;
                                                    logger.Debug($"Skipping compilation: {matchPath.Name}");
                                                    break;
                                                }
                                            }
                                        }

                                        if (!excluded)
                                        {
                                            nssFiles.Add(match);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    logger.Info($"Found {nssFiles.Count} scripts to compile");

                    if (nssFiles.Count == 0)
                    {
                        logger.Warning("No scripts found to compile");
                        continue;
                    }

                    // Compile scripts
                    int compiledCount = 0;
                    int errorCount = 0;

                    if (useExternal && !string.IsNullOrEmpty(externalCompiler))
                    {
                        // Use external compiler
                        foreach (var nssPath in nssFiles)
                        {
                            try
                            {
                                var nssFile = new FileInfo(nssPath);
                                var outputFile = Path.Combine(cacheDir, Path.ChangeExtension(nssFile.Name, ".ncs"));

                                // Build compiler command
                                var startInfo = new ProcessStartInfo
                                {
                                    FileName = externalCompiler,
                                    Arguments = $"\"{nssPath}\" -o \"{outputFile}\"",
                                    WorkingDirectory = rootDir,
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };

                                using (var process = Process.Start(startInfo))
                                {
                                    if (process != null)
                                    {
                                        process.WaitForExit();
                                        string stdout = process.StandardOutput.ReadToEnd();
                                        string stderr = process.StandardError.ReadToEnd();

                                        if (process.ExitCode == 0)
                                        {
                                            logger.Debug($"Compiled: {nssFile.Name} -> {Path.GetFileName(outputFile)}");
                                            compiledCount++;
                                        }
                                        else
                                        {
                                            logger.Error($"Compilation failed for {nssFile.Name}:");
                                            if (!string.IsNullOrEmpty(stdout))
                                            {
                                                logger.Error(stdout);
                                            }
                                            if (!string.IsNullOrEmpty(stderr))
                                            {
                                                logger.Error(stderr);
                                            }
                                            errorCount++;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Failed to compile {Path.GetFileName(nssPath)}: {ex.Message}");
                                errorCount++;
                            }
                        }
                    }
                    else
                    {
                        // Use built-in compiler
                        foreach (var nssPath in nssFiles)
                        {
                            try
                            {
                                var nssFile = new FileInfo(nssPath);
                                var outputFile = Path.Combine(cacheDir, Path.ChangeExtension(nssFile.Name, ".ncs"));

                                // Read NSS source
                                string nssSource = File.ReadAllText(nssPath, Encoding.UTF8);

                                // Build library lookup paths (include parent directory for #include resolution)
                                var parentDir = Path.GetDirectoryName(nssPath);
                                var libraryLookup = new List<string>();
                                if (!string.IsNullOrEmpty(parentDir))
                                {
                                    libraryLookup.Add(parentDir);
                                }

                                // Compile using built-in compiler
                                NCS ncs = NCSAuto.CompileNss(nssSource, game, null, null, libraryLookup);
                                NCSAuto.WriteNcs(ncs, outputFile);

                                logger.Debug($"Compiled: {nssFile.Name} -> {Path.GetFileName(outputFile)}");
                                compiledCount++;
                            }
                            catch (Exception ex)
                            {
                                logger.Error($"Compilation failed for {Path.GetFileName(nssPath)}: {ex.Message}");
                                if (logger.IsDebug)
                                {
                                    logger.Debug($"Stack trace: {ex.StackTrace}");
                                }
                                errorCount++;
                            }
                        }
                    }

                    logger.Info($"Compiled {compiledCount} scripts, {errorCount} errors");

                    if (errorCount > 0)
                    {
                        logger.Warning("Some scripts failed to compile");
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to compile: {ex.Message}");
                if (logger.IsDebug)
                {
                    logger.Debug($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        /// <summary>
        /// Determine which game version to compile for (K1 or K2).
        /// Matching PyKotor: get_game_from_config()
        /// </summary>
        private static BioWareGame GetGameFromConfig(KotorCLIConfig config)
        {
            // This could be enhanced to read from config
            // For now, default to K2 which is more compatible
            // Matching PyKotor: default to Game.K2
            return BioWareGame.TSL;
        }

        /// <summary>
        /// Find the NWScript compiler executable.
        /// Matching PyKotor: find_nss_compiler()
        /// Searches for external compilers in this order:
        /// 1. nwnnsscomp (preferred, compatible with both K1 and K2)
        /// 2. nwnsc (legacy, Neverwinter Nights compiler)
        /// </summary>
        private static string FindNssCompiler()
        {
            string[] compilerNames;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                compilerNames = new[] { "nwnnsscomp.exe", "nwnsc.exe" };
            }
            else
            {
                compilerNames = new[] { "nwnnsscomp", "nwnsc" };
            }

            // Check PATH
            foreach (var name in compilerNames)
            {
                string path = FindInPath(name);
                if (!string.IsNullOrEmpty(path))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// Find an executable in the system PATH.
        /// </summary>
        private static string FindInPath(string executableName)
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            string[] paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }

                string fullPath = Path.Combine(path, executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
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
