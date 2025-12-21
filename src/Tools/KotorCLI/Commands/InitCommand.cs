using System;
using System.CommandLine;
using System.Diagnostics;
using System.IO;
using System.Text;
using KotorCLI.Configuration;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Init command - Create a new kotorcli package.
    /// </summary>
    public static class InitCommand
    {
        private const string DefaultConfigTemplate = @"# KotorCLI Package Configuration
# This file uses TOML format and is compatible with cli syntax

[package]
name = ""{0}""
description = ""{1}""
version = ""1.0.0""
author = ""{2}""

# Default file pattern - inherited by targets if not specified
file = ""$target.mod""

  [package.sources]
  include = ""src/**/*.{{nss,json,ncs,utc,uti,utp,utd,ute,utm,uts,utt,utw,git,are,ifo,dlg,gui}}""
  exclude = ""**/test_*.nss""

  [package.rules]
  ""*.nss"" = ""src/scripts""
  ""*.ncs"" = ""src/scripts""
  ""*.utc"" = ""src/blueprints/creatures""
  ""*.uti"" = ""src/blueprints/items""
  ""*.utp"" = ""src/blueprints/placeables""
  ""*.utd"" = ""src/blueprints/doors""
  ""*.ute"" = ""src/blueprints/encounters""
  ""*.utm"" = ""src/blueprints/merchants""
  ""*.uts"" = ""src/blueprints/sounds""
  ""*.utt"" = ""src/blueprints/triggers""
  ""*.utw"" = ""src/blueprints/waypoints""
  ""*.dlg"" = ""src/dialogs""
  ""*.git"" = ""src/areas""
  ""*.are"" = ""src/areas""
  ""*.ifo"" = ""src""
  ""*.gui"" = ""src/gui""
  ""*"" = ""src""

[target]
name = ""default""
file = ""{3}""
description = ""Default target""
";

        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var initCommand = new Command("init", "Create a new kotorcli package");
            var dirArgument = new Argument<string>("dir");
            initCommand.Add(dirArgument);
            var fileArgument = new Argument<string>("file");
            initCommand.Add(fileArgument);
            var defaultOption = new Option<bool>("--default", "Skip package generation dialog");
            initCommand.Options.Add(defaultOption);
            var vcsOption = new Option<string>("--vcs", "Version control system to use");
            initCommand.Options.Add(vcsOption);
            var initFileOption = new Option<string>("--file", "File to unpack into the package");
            initCommand.Options.Add(initFileOption);
            
            initCommand.SetAction(parseResult =>
            {
                var dir = parseResult.GetValue(dirArgument) ?? ".";
                var file = parseResult.GetValue(fileArgument);
                var defaultMode = parseResult.GetValue(defaultOption);
                var vcs = parseResult.GetValue(vcsOption);
                if (string.IsNullOrEmpty(vcs))
                {
                    vcs = "git";
                }
                var initFile = parseResult.GetValue(initFileOption) ?? file;
                
                var logger = new StandardLogger();
                var exitCode = Execute(dir, initFile, defaultMode, vcs, logger);
                Environment.Exit(exitCode);
            });

            rootCommand.Add(initCommand);
        }

        private static int Execute(string dir, string initFile, bool defaultMode, string vcs, ILogger logger)
        {
            var targetDir = Path.GetFullPath(string.IsNullOrEmpty(dir) ? "." : dir);
            Directory.CreateDirectory(targetDir);

            var configPath = Path.Combine(targetDir, "kotorcli.cfg");
            if (File.Exists(configPath) && !defaultMode)
            {
                logger.Warning($"Package already initialized at {targetDir}");
                Console.Write("Overwrite existing configuration? (yes/no) [no]: ");
                var overwrite = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (overwrite != "yes" && overwrite != "y")
                {
                    logger.Info("Initialization cancelled");
                    return 0;
                }
            }

            logger.Info($"Initializing kotorcli package in {targetDir}");

            string packageName, description, author, targetFile;
            if (defaultMode)
            {
                packageName = Path.GetFileName(targetDir);
                description = "";
                author = "";
                targetFile = $"{packageName}.mod";
            }
            else
            {
                logger.Info("\nPackage Configuration");
                logger.Info("---------------------");
                packageName = Prompt("Package name", Path.GetFileName(targetDir));
                description = Prompt("Description", "");
                author = Prompt("Author", "");

                logger.Info("\nTarget Configuration");
                logger.Info("--------------------");
                targetFile = Prompt("Target filename (e.g., mymod.mod, myhak.hak)", $"{packageName}.mod");
            }

            var configContent = string.Format(DefaultConfigTemplate, packageName, description, author, targetFile);
            File.WriteAllText(configPath, configContent, Encoding.UTF8);
            logger.Info($"Created {configPath}");

            CreateDirectoryStructure(targetDir, logger);
            CreateGitIgnore(targetDir, logger);

            if (vcs == "git")
            {
                InitializeGitRepo(targetDir, logger);
            }

            if (!string.IsNullOrEmpty(initFile))
            {
                // TODO: Call unpack command to unpack initial file
                logger.Info($"Note: Unpacking initial file not yet implemented. File: {initFile}");
            }

            logger.Info($"\nPackage initialized successfully in {targetDir}");
            logger.Info("Next steps:");
            logger.Info("  1. Edit kotorcli.cfg to configure your package");
            logger.Info("  2. Place your source files in the src/ directory");
            logger.Info("  3. Run 'kotorcli list' to see available targets");
            logger.Info("  4. Run 'kotorcli pack' to build your module");

            return 0;
        }

        private static string Prompt(string message, string defaultValue)
        {
            var promptText = string.IsNullOrEmpty(defaultValue) ? $"{message}: " : $"{message} [{defaultValue}]: ";
            Console.Write(promptText);
            var response = Console.ReadLine()?.Trim();
            return string.IsNullOrEmpty(response) ? defaultValue : response;
        }

        private static void CreateDirectoryStructure(string targetDir, ILogger logger)
        {
            var srcDir = Path.Combine(targetDir, "src");
            Directory.CreateDirectory(srcDir);
            logger.Info($"Created {srcDir}");

            var subdirs = new[]
            {
                "src/scripts",
                "src/dialogs",
                "src/areas",
                "src/blueprints/creatures",
                "src/blueprints/items",
                "src/blueprints/placeables",
                "src/blueprints/doors",
                "src/blueprints/encounters",
                "src/blueprints/merchants",
                "src/blueprints/sounds",
                "src/blueprints/triggers",
                "src/blueprints/waypoints",
                "src/gui",
            };

            foreach (var subdir in subdirs)
            {
                var dirPath = Path.Combine(targetDir, subdir);
                Directory.CreateDirectory(dirPath);
            }

            var kotorcliDir = Path.Combine(targetDir, ".kotorcli");
            Directory.CreateDirectory(kotorcliDir);
        }

        private static void CreateGitIgnore(string targetDir, ILogger logger)
        {
            var gitignoreContent = @"# KotorCLI
.kotorcli/cache/
.kotorcli/user.cfg
*.log

# Build output
*.mod
*.erf
*.hak
*.rim
*.bif
*.key

# Compiled scripts
*.ncs
";
            var gitignorePath = Path.Combine(targetDir, ".gitignore");
            if (!File.Exists(gitignorePath))
            {
                File.WriteAllText(gitignorePath, gitignoreContent, Encoding.UTF8);
                logger.Info($"Created {gitignorePath}");
            }
        }

        private static void InitializeGitRepo(string targetDir, ILogger logger)
        {
            try
            {
                var startInfo = new ProcessStartInfo("git", "init")
                {
                    WorkingDirectory = targetDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    process.WaitForExit();
                    if (process.ExitCode == 0)
                    {
                        logger.Info("Initialized git repository");

                        var gitattributesContent = @"# Auto detect text files and perform LF normalization
* text=auto

# Scripts should use LF
*.nss text eol=lf
*.ncs binary
";
                        var gitattributesPath = Path.Combine(targetDir, ".gitattributes");
                        File.WriteAllText(gitattributesPath, gitattributesContent, Encoding.UTF8);
                        logger.Info($"Created {gitattributesPath}");
                    }
                    else
                    {
                        logger.Warning("Failed to initialize git repository");
                    }
                }
            }
            catch (Exception)
            {
                logger.Warning("Git not found, skipping repository initialization");
            }
        }
    }
}
