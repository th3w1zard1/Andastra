using System;
using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using KotorCLI.Configuration;
using KotorCLI.Logging;
using Tomlyn.Model;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Convert command - Convert all JSON sources to their GFF counterparts.
    /// </summary>
    public static class ConvertCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var convertCommand = new Command("convert", "Convert all JSON sources to their GFF counterparts");
            var targetsArgument = new Argument<string[]>("targets");
            convertCommand.Add(targetsArgument);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before converting");
            convertCommand.Options.Add(cleanOption);

            convertCommand.SetAction(parseResult =>
            {
                var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                var clean = parseResult.GetValue(cleanOption);

                var logger = new StandardLogger();
                var exitCode = Execute(targets, clean, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(convertCommand);
        }

        internal static int Execute(string[] targetNames, bool clean, ILogger logger)
        {
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
                    logger.Info($"Converting target: {targetName}");

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

                    // Find JSON files to convert
                    var jsonFiles = new List<string>();
                    foreach (var pattern in includePatterns)
                    {
                        var patternPath = Path.Combine(rootDir, pattern.Replace("**", "*"));
                        var matches = Directory.GetFiles(rootDir, patternPath.Replace(rootDir + Path.DirectorySeparatorChar, ""), SearchOption.AllDirectories);
                        foreach (var match in matches)
                        {
                            if (match.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                // Check against exclude patterns
                                var excluded = excludePatterns.Any(excludePattern =>
                                {
                                    var excludePath = Path.Combine(rootDir, excludePattern);
                                    return MatchPattern(match, excludePath);
                                });

                                if (!excluded)
                                {
                                    jsonFiles.Add(match);
                                }
                            }
                        }
                    }

                    logger.Info($"Found {jsonFiles.Count} JSON files to convert");

                    var convertedCount = 0;
                    var failedCount = 0;

                    // Convert each JSON file
                    foreach (var jsonFile in jsonFiles)
                    {
                        try
                        {
                            // Determine output file (remove .json extension, restore original extension)
                            var outputFile = jsonFile.Substring(0, jsonFile.Length - 5); // Remove .json

                            // Check if JSON file is newer than output file
                            if (File.Exists(outputFile))
                            {
                                var jsonTime = File.GetLastWriteTime(jsonFile);
                                var outputTime = File.GetLastWriteTime(outputFile);
                                if (jsonTime <= outputTime)
                                {
                                    logger.Debug($"Skipping {Path.GetFileName(jsonFile)} (up to date)");
                                    continue;
                                }
                            }

                            // Read JSON GFF and convert to binary GFF
                            var gff = GFFAuto.ReadGff(jsonFile, fileFormat: ResourceType.GFF_JSON);
                            GFFAuto.WriteGff(gff, outputFile, ResourceType.GFF);
                            convertedCount++;
                        }
                        catch (Exception ex)
                        {
                            logger.Warning($"Failed to convert {Path.GetFileName(jsonFile)}: {ex.Message}");
                            failedCount++;
                        }
                    }

                    logger.Info($"Converted {convertedCount} files (failed: {failedCount})");
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to convert: {ex.Message}");
                if (logger.IsDebug)
                {
                    logger.Debug($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        private static bool MatchPattern(string path, string pattern)
        {
            // Simple pattern matching - convert glob pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
        }

        private static T GetTomlValue<T>(Tomlyn.Model.TomlTable table, string key)
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
