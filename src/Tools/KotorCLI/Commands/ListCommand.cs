using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.CommandLine;
using KotorCLI.Logging;
using KotorCLI.Configuration;
using Tomlyn.Model;

namespace KotorCLI.Commands
{
    public static class ListCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var listCommand = new Command("list", "List all targets defined in kotorcli.cfg");
            var targetsArgument = new Argument<string[]>("targets", () => new string[0], "Specific targets to list");
            listCommand.AddArgument(targetsArgument);
            var quietOption = new Option<bool>("--quiet", "List only target names");
            listCommand.AddOption(quietOption);
            var verboseOption = new Option<bool>("--verbose", "List source files as well");
            listCommand.AddOption(verboseOption);
            listCommand.SetHandler((string[] targets, bool quiet, bool verbose) =>
            {
                var logger = new StandardLogger();
                try
                {
                    ExecuteList(targets, quiet, verbose, logger);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error listing targets: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        logger.Error($"Inner exception: {ex.InnerException.Message}");
                    }
                    Environment.Exit(1);
                }
            }, targetsArgument, quietOption, verboseOption);
            rootCommand.AddCommand(listCommand);
        }

        private static void ExecuteList(string[] targetNames, bool quiet, bool verbose, ILogger logger)
        {
            // Find kotorcli.cfg
            string configPath = ConfigFileFinder.FindConfigFile();
            if (configPath == null)
            {
                logger.Error("kotorcli.cfg not found. Please run this command from a directory containing kotorcli.cfg or a subdirectory.");
                return;
            }

            // Load configuration
            KotorCLIConfig config;
            try
            {
                config = new KotorCLIConfig(configPath);
            }
            catch (FileNotFoundException ex)
            {
                logger.Error($"Configuration file not found: {ex.Message}");
                return;
            }
            catch (Exception ex)
            {
                logger.Error($"Error loading configuration: {ex.Message}");
                return;
            }

            // Get targets
            List<TomlTable> targets;
            if (targetNames != null && targetNames.Length > 0)
            {
                // Get specific targets
                targets = new List<TomlTable>();
                foreach (var targetName in targetNames)
                {
                    var target = config.GetTarget(targetName);
                    if (target == null)
                    {
                        logger.Warning($"Target not found: {targetName}");
                    }
                    else
                    {
                        targets.Add(target);
                    }
                }

                if (targets.Count == 0)
                {
                    logger.Error("No valid targets found");
                    return;
                }
            }
            else
            {
                // Get all targets
                targets = config.GetTargets();
                if (targets.Count == 0)
                {
                    logger.Info("No targets defined in kotorcli.cfg");
                    return;
                }
            }

            // Display targets
            if (quiet)
            {
                // Quiet mode: just list target names
                foreach (var target in targets)
                {
                    string name = GetTargetName(target);
                    logger.Info(name);
                }
            }
            else if (verbose)
            {
                // Verbose mode: list targets with source files
                foreach (var target in targets)
                {
                    string name = GetTargetName(target);
                    logger.Info($"Target: {name}");

                    // Get source patterns
                    var sources = config.GetTargetSources(target);
                    if (sources["include"].Count > 0 || sources["exclude"].Count > 0 || sources["filter"].Count > 0 || sources["skipCompile"].Count > 0)
                    {
                        logger.Info("  Sources:");
                        if (sources["include"].Count > 0)
                        {
                            logger.Info($"    Include: {string.Join(", ", sources["include"])}");
                        }
                        if (sources["exclude"].Count > 0)
                        {
                            logger.Info($"    Exclude: {string.Join(", ", sources["exclude"])}");
                        }
                        if (sources["filter"].Count > 0)
                        {
                            logger.Info($"    Filter: {string.Join(", ", sources["filter"])}");
                        }
                        if (sources["skipCompile"].Count > 0)
                        {
                            logger.Info($"    Skip Compile: {string.Join(", ", sources["skipCompile"])}");
                        }
                    }

                    // Get other target properties
                    if (target.TryGetValue("parent", out object parentObj) && parentObj is string parent)
                    {
                        logger.Info($"  Parent: {parent}");
                    }

                    if (target.TryGetValue("output", out object outputObj))
                    {
                        string output = config.ResolveTargetValue(target, "output")?.ToString() ?? outputObj.ToString();
                        logger.Info($"  Output: {output}");
                    }

                    logger.Info("");
                }
            }
            else
            {
                // Default mode: list targets with key information
                foreach (var target in targets)
                {
                    string name = GetTargetName(target);
                    logger.Info($"Target: {name}");

                    // Show parent if exists
                    if (target.TryGetValue("parent", out object parentObj) && parentObj is string parent)
                    {
                        logger.Info($"  Parent: {parent}");
                    }

                    // Show output if exists
                    if (target.TryGetValue("output", out object outputObj))
                    {
                        string output = config.ResolveTargetValue(target, "output")?.ToString() ?? outputObj.ToString();
                        logger.Info($"  Output: {output}");
                    }

                    // Show source count
                    var sources = config.GetTargetSources(target);
                    int includeCount = sources["include"].Count;
                    int excludeCount = sources["exclude"].Count;
                    if (includeCount > 0 || excludeCount > 0)
                    {
                        logger.Info($"  Sources: {includeCount} include, {excludeCount} exclude");
                    }
                }
            }
        }

        private static string GetTargetName(TomlTable target)
        {
            if (target.TryGetValue("name", out object nameObj) && nameObj is string name)
            {
                return name;
            }
            return "unnamed";
        }
    }
}

