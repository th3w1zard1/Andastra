using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using KotorCLI.Configuration;
using KotorCLI.Logging;
using Tomlyn.Model;
using Environment = System.Environment;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Install command - Convert, compile, pack, and install target.
    /// </summary>
    public static class InstallCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var installCommand = new Command("install", "Convert, compile, pack, and install target");
            var targetsArgument = new Argument<string[]>("targets");
            targetsArgument.Description = "Targets to install (use 'all' for all targets)";
            installCommand.Add(targetsArgument);
            var installDirOption = new Option<string>("--installDir", "The location of the KOTOR user directory");
            installCommand.Options.Add(installDirOption);
            var noPackOption = new Option<bool>("--noPack", "Do not re-pack the file (implies --noConvert and --noCompile)");
            installCommand.Options.Add(noPackOption);
            var cleanOption = new Option<bool>("--clean", "Clear the cache before packing");
            installCommand.Options.Add(cleanOption);
            
            installCommand.SetAction(parseResult =>
            {
                var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                var installDir = parseResult.GetValue(installDirOption);
                var noPack = parseResult.GetValue(noPackOption);
                var clean = parseResult.GetValue(cleanOption);
                
                var logger = new StandardLogger();
                var exitCode = Execute(targets, installDir, noPack, clean, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(installCommand);
        }

        private static int Execute(string[] targetNames, string installDir, bool noPack, bool clean, ILogger logger)
        {
            // Find config file
            var configPath = ConfigFileFinder.FindConfigFile();
            if (configPath == null)
            {
                logger.Error("This is not a kotorcli repository. Please run 'kotorcli init'");
                return 1;
            }

            try
            {
                var config = new KotorCLIConfig(configPath);
                var configDir = Path.GetDirectoryName(configPath);

                // Determine targets to install
                List<TomlTable> targetsToInstall = new List<TomlTable>();
                if (targetNames.Length == 0 || (targetNames.Length == 1 && targetNames[0].ToLowerInvariant() == "all"))
                {
                    // Install all targets
                    targetsToInstall = config.GetTargets();
                    if (targetsToInstall.Count == 0)
                    {
                        logger.Error("No targets found in configuration");
                        return 1;
                    }
                    logger.Info($"Installing all {targetsToInstall.Count} target(s)");
                }
                else
                {
                    // Install specified targets
                    foreach (string targetName in targetNames)
                    {
                        var target = config.GetTarget(targetName);
                        if (target == null)
                        {
                            logger.Error($"Target not found: {targetName}");
                            return 1;
                        }
                        targetsToInstall.Add(target);
                    }
                }

                // Determine installation directory
                string kotorInstallDir = DetermineInstallationDirectory(installDir, logger);
                if (string.IsNullOrEmpty(kotorInstallDir))
                {
                    logger.Error("Could not determine KOTOR installation directory. Please specify --installDir");
                    return 1;
                }

                // Verify installation directory exists and contains chitin.key
                string chitinPath = Path.Combine(kotorInstallDir, "chitin.key");
                if (!File.Exists(chitinPath))
                {
                    logger.Error($"Installation directory does not appear to be valid (chitin.key not found): {kotorInstallDir}");
                    return 1;
                }

                logger.Info($"Using installation directory: {kotorInstallDir}");

                // Get modules directory
                string modulesDir = Path.Combine(kotorInstallDir, "modules");
                if (!Directory.Exists(modulesDir))
                {
                    logger.Info($"Modules directory does not exist, creating: {modulesDir}");
                    try
                    {
                        Directory.CreateDirectory(modulesDir);
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"Failed to create modules directory: {ex.Message}");
                        return 1;
                    }
                }

                // Process each target
                int successCount = 0;
                int failCount = 0;

                foreach (var target in targetsToInstall)
                {
                    string targetName = GetTomlValue<string>(target, "name") ?? "unnamed";
                    logger.Info($"\nInstalling target: {targetName}");

                    // Get target file path
                    var targetFileObj = config.ResolveTargetValue(target, "file");
                    if (targetFileObj == null)
                    {
                        logger.Error($"  Target '{targetName}' has no 'file' property defined");
                        failCount++;
                        continue;
                    }

                    string targetFileStr = targetFileObj.ToString();
                    if (string.IsNullOrEmpty(targetFileStr))
                    {
                        logger.Error($"  Target '{targetName}' has empty 'file' property");
                        failCount++;
                        continue;
                    }

                    // Resolve file path (may be relative to config directory)
                    string targetFilePath;
                    if (Path.IsPathRooted(targetFileStr))
                    {
                        targetFilePath = targetFileStr;
                    }
                    else
                    {
                        targetFilePath = Path.Combine(configDir, targetFileStr);
                    }

                    // If --noPack is not specified, ensure the file exists (pack should have been run)
                    if (!noPack)
                    {
                        // Check if file exists
                        if (!File.Exists(targetFilePath))
                        {
                            logger.Warning($"  Packed file not found: {targetFilePath}");
                            logger.Info($"  Run 'kotorcli pack {targetName}' first to create the packed file");
                            failCount++;
                            continue;
                        }
                    }
                    else
                    {
                        // With --noPack, file must exist
                        if (!File.Exists(targetFilePath))
                        {
                            logger.Error($"  File not found: {targetFilePath}");
                            failCount++;
                            continue;
                        }
                    }

                    // Determine destination filename
                    string fileName = Path.GetFileName(targetFilePath);
                    string destPath = Path.Combine(modulesDir, fileName);

                    // Check if file already exists
                    if (File.Exists(destPath))
                    {
                        logger.Info($"  File already exists in modules directory: {fileName}");
                        logger.Info($"  Overwriting existing file...");
                    }

                    // Copy file to modules directory
                    try
                    {
                        File.Copy(targetFilePath, destPath, overwrite: true);
                        logger.Info($"  Successfully installed: {fileName} -> {destPath}");
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"  Failed to copy file: {ex.Message}");
                        failCount++;
                    }
                }

                // Summary
                logger.Info($"\nInstallation complete: {successCount} succeeded, {failCount} failed");
                return failCount > 0 ? 1 : 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Error during installation: {ex.Message}");
                if (logger is VerboseLogger)
                {
                    logger.Error($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }

        /// <summary>
        /// Determines the KOTOR installation directory.
        /// Priority:
        /// 1. User-specified --installDir
        /// 2. Environment variable KOTOR_PATH or K1_PATH
        /// 3. Common installation locations
        /// </summary>
        private static string DetermineInstallationDirectory(string userSpecifiedDir, ILogger logger)
        {
            // 1. User-specified directory
            if (!string.IsNullOrEmpty(userSpecifiedDir))
            {
                if (Directory.Exists(userSpecifiedDir))
                {
                    return Path.GetFullPath(userSpecifiedDir);
                }
                logger.Warning($"Specified installation directory does not exist: {userSpecifiedDir}");
            }

            // 2. Environment variables
            string kotorPath = Environment.GetEnvironmentVariable("KOTOR_PATH");
            if (string.IsNullOrEmpty(kotorPath))
            {
                kotorPath = Environment.GetEnvironmentVariable("K1_PATH");
            }
            if (!string.IsNullOrEmpty(kotorPath) && Directory.Exists(kotorPath))
            {
                string chitinPath = Path.Combine(kotorPath, "chitin.key");
                if (File.Exists(chitinPath))
                {
                    return Path.GetFullPath(kotorPath);
                }
            }

            // 3. Common installation locations
            string[] commonPaths = {
                @"C:\Program Files (x86)\Steam\steamapps\common\swkotor",
                @"C:\Program Files\Steam\steamapps\common\swkotor",
                @"C:\Program Files (x86)\LucasArts\SWKotOR",
                @"C:\Program Files\LucasArts\SWKotOR",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SWKotOR"),
            };

            foreach (string path in commonPaths)
            {
                if (Directory.Exists(path))
                {
                    string chitinPath = Path.Combine(path, "chitin.key");
                    if (File.Exists(chitinPath))
                    {
                        return Path.GetFullPath(path);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Helper method to get a TOML value with type safety.
        /// </summary>
        private static T GetTomlValue<T>(TomlTable table, string key)
        {
            if (table.TryGetValue(key, out object value))
            {
                if (value is T typedValue)
                {
                    return typedValue;
                }
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default(T);
                }
            }
            return default(T);
        }
    }
}
