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

            // Global options (add as regular options to root command)
            var versionOption = new Option<bool>("--version", "Show version information");
            rootCommand.Options.Add(versionOption);

            var verboseOption = new Option<bool>(
                "--verbose",
                "Increase feedback verbosity"
            );
            rootCommand.Options.Add(verboseOption);

            var debugOption = new Option<bool>(
                "--debug",
                "Enable debug logging (implies --verbose)"
            );
            rootCommand.Options.Add(debugOption);

            var quietOption = new Option<bool>(
                "--quiet",
                "Disable all logging except errors"
            );
            rootCommand.Options.Add(quietOption);

            var noColorOption = new Option<bool>(
                "--no-color",
                "Disable color output"
            );
            rootCommand.Options.Add(noColorOption);

            var yesOption = new Option<bool>(
                "--yes",
                "Automatically answer yes to all prompts"
            );
            rootCommand.Options.Add(yesOption);

            var noOption = new Option<bool>(
                "--no",
                "Automatically answer no to all prompts"
            );
            rootCommand.Options.Add(noOption);

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
            rootCommand.SetAction(parseResult =>
            {
                var version = parseResult.GetValue(versionOption);
                if (version)
                {
                    Console.WriteLine($"KotorCLI {Version}");
                    Environment.Exit(0);
                }
            });

            try
            {
                var parseResult = rootCommand.Parse(args);
                return parseResult.Invoke();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                if (args.Length > 0 && (args[0] == "--debug" || Array.IndexOf(args, "--debug") >= 0))
                {
                    Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                }
                return 1;
            }
        }
    }
}
