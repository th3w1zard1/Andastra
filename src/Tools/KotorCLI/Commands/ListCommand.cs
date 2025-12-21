using System;
using System.CommandLine;
using System.Linq;
using KotorCLI.Configuration;
using KotorCLI.Logging;
using Tomlyn.Model;

namespace KotorCLI.Commands
{
    /// <summary>
    /// List command - List all targets defined in kotorcli.cfg.
    /// </summary>
    public static class ListCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var listCommand = new Command("list", "List all targets defined in kotorcli.cfg");
            var targetsArgument = new Argument<string[]>("targets");
            listCommand.Add(targetsArgument);
            var quietOption = new Option<bool>("--quiet", "List only target names");
            listCommand.Options.Add(quietOption);
            var verboseOption = new Option<bool>("--verbose", "List source files as well");
            listCommand.Options.Add(verboseOption);
            
            listCommand.SetAction(parseResult =>
            {
                var targets = parseResult.GetValue(targetsArgument) ?? Array.Empty<string>();
                var quiet = parseResult.GetValue(quietOption);
                var verbose = parseResult.GetValue(verboseOption);
                
                var logger = new StandardLogger();
                var exitCode = Execute(targets, quiet, verbose, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(listCommand);
        }

        private static int Execute(string[] targets, bool quiet, bool verbose, ILogger logger)
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
                var allTargets = config.GetTargets();

                if (allTargets.Count == 0)
                {
                    logger.Info("No targets defined in kotorcli.cfg");
                    return 0;
                }

                var targetsToShow = targets.Length > 0
                    ? allTargets.Where(t => targets.Contains(t.GetValueOrDefault("name")?.ToString() ?? "")).ToList()
                    : allTargets;

                if (targetsToShow.Count == 0)
                {
                    logger.Warning($"No targets found matching: {string.Join(", ", targets)}");
                    return 1;
                }

                foreach (var target in targetsToShow)
                {
                    var targetName = GetTomlValue<string>(target, "name") ?? "unnamed";
                    var targetFile = GetTomlValue<string>(target, "file") ?? "";
                    var targetDescription = GetTomlValue<string>(target, "description") ?? "";

                    if (quiet)
                    {
                        logger.Info(targetName);
                    }
                    else
                    {
                        logger.Info($"Target: {targetName}");
                        if (!string.IsNullOrEmpty(targetDescription))
                        {
                            logger.Info($"  Description: {targetDescription}");
                        }
                        logger.Info($"  File: {targetFile}");

                        if (verbose)
                        {
                            // TODO: List source files when verbose mode is enabled
                            logger.Info("TODO: STUB -   Source files: (verbose listing not yet implemented)");
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to list targets: {ex.Message}");
                return 1;
            }
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
