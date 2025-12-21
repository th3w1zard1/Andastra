using System;
using System.CommandLine;
using KotorCLI.Commands;

namespace KotorCLI
{
    /// <summary>
    /// KotorCLI - A build tool for KOTOR projects (cli-compatible syntax).
    /// Comprehensive implementation ported from PyKotor's KotorCLI tool.
    /// </summary>
    public class Program
    {
        private const string Version = "1.2.0";

        public static int Main(string[] args)
        {
            var rootCommand = new RootCommand("KotorCLI - A build tool for KOTOR projects (cli-compatible syntax)")
            {
                Description = "Command-line tool for converting KOTOR modules, ERFs, and haks between binary and text-based source files"
            };

            // Global options
            var versionOption = new Option<bool>(
                new[] { "--version", "-v" },
                "Show version information"
            );
            rootCommand.AddGlobalOption(versionOption);

            var verboseOption = new Option<bool>(
                new[] { "--verbose" },
                "Increase feedback verbosity"
            );
            rootCommand.AddGlobalOption(verboseOption);

            var debugOption = new Option<bool>(
                new[] { "--debug" },
                "Enable debug logging (implies --verbose)"
            );
            rootCommand.AddGlobalOption(debugOption);

            var quietOption = new Option<bool>(
                new[] { "--quiet" },
                "Disable all logging except errors"
            );
            rootCommand.AddGlobalOption(quietOption);

            var noColorOption = new Option<bool>(
                new[] { "--no-color" },
                "Disable color output"
            );
            rootCommand.AddGlobalOption(noColorOption);

            var yesOption = new Option<bool>(
                new[] { "--yes" },
                "Automatically answer yes to all prompts"
            );
            rootCommand.AddGlobalOption(yesOption);

            var noOption = new Option<bool>(
                new[] { "--no" },
                "Automatically answer no to all prompts"
            );
            rootCommand.AddGlobalOption(noOption);

            // Add all command handlers
            ConfigCommand.AddToRootCommand(rootCommand);
            InitCommand.AddToRootCommand(rootCommand);
            ListCommand.AddToRootCommand(rootCommand);
            UnpackCommand.AddToRootCommand(rootCommand);
            ConvertCommand.AddToRootCommand(rootCommand);
            CompileCommand.AddToRootCommand(rootCommand);
            PackCommand.AddToRootCommand(rootCommand);
            InstallCommand.AddToRootCommand(rootCommand);
            LaunchCommand.AddToRootCommand(rootCommand);
            ExtractCommand.AddToRootCommand(rootCommand);
            ListArchiveCommand.AddToRootCommand(rootCommand);
            CreateArchiveCommand.AddToRootCommand(rootCommand);
            SearchArchiveCommand.AddToRootCommand(rootCommand);
            FormatConvertCommands.AddToRootCommand(rootCommand);
            ScriptToolCommands.AddToRootCommand(rootCommand);
            ResourceToolCommands.AddToRootCommand(rootCommand);
            UtilityCommands.AddToRootCommand(rootCommand);
            ValidationCommands.AddToRootCommand(rootCommand);
            KeyPackCommand.AddToRootCommand(rootCommand);

            // Handle version option
            rootCommand.SetHandler((bool version) =>
            {
                if (version)
                {
                    Console.WriteLine($"KotorCLI {Version}");
                    Environment.Exit(0);
                }
            }, versionOption);

            try
            {
                return rootCommand.Invoke(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (args.Contains("--debug"))
                {
                    Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }
    }
}

