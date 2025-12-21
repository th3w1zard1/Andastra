using System;
using System.CommandLine;
using System.IO;
using System.Linq;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Config command - Get, set, or unset user-defined configuration options.
    /// </summary>
    public static class ConfigCommand
    {
        public static void AddToRootCommand(RootCommand rootCommand)
        {
            var configCommand = new Command("config", "Get, set, or unset user-defined configuration options");

            var keyArgument = new Argument<string>("key", "Configuration key") { Arity = ArgumentArity.ZeroOrOne };
            configCommand.AddArgument(keyArgument);

            var valueArgument = new Argument<string>("value", "Configuration value") { Arity = ArgumentArity.ZeroOrOne };
            configCommand.AddArgument(valueArgument);

            var globalOption = new Option<bool>(
                new[] { "--global" },
                "Apply to all packages (default)"
            );
            configCommand.AddOption(globalOption);

            var localOption = new Option<bool>(
                new[] { "--local" },
                "Apply to current package only"
            );
            configCommand.AddOption(localOption);

            var getOption = new Option<bool>(
                new[] { "--get" },
                "Get the value of key (default when value not passed)"
            );
            configCommand.AddOption(getOption);

            var setOption = new Option<bool>(
                new[] { "--set" },
                "Set key to value (default when value is passed)"
            );
            configCommand.AddOption(setOption);

            var unsetOption = new Option<bool>(
                new[] { "--unset" },
                "Delete the key/value pair for key"
            );
            configCommand.AddOption(unsetOption);

            var listOption = new Option<bool>(
                new[] { "--list" },
                "List all key/value pairs in the config file"
            );
            configCommand.AddOption(listOption);

            configCommand.SetHandler((string key, string value, bool global, bool local, bool get, bool set, bool unset, bool list) =>
            {
                var logger = new StandardLogger();
                Execute(key, value, global, local, get, set, unset, list, logger);
            }, keyArgument, valueArgument, globalOption, localOption, getOption, setOption, unsetOption, listOption);

            rootCommand.AddCommand(configCommand);
        }

        private static int Execute(string key, string value, bool global, bool local, bool get, bool set, bool unset, bool list, ILogger logger)
        {
            // TODO: Implement config command
            logger.Info("Config command not yet implemented");
            return 0;
        }
    }
}

