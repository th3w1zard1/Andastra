using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using KotorCLI.Logging;
using Tomlyn;
using Tomlyn.Model;

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
                var exitCode = Execute(key, value, global, local, get, set, unset, list, logger);
                Environment.Exit(exitCode);
            }, keyArgument, valueArgument, globalOption, localOption, getOption, setOption, unsetOption, listOption);

            rootCommand.AddCommand(configCommand);
        }

        private static int Execute(string key, string value, bool global, bool local, bool get, bool set, bool unset, bool list, ILogger logger)
        {
            // Determine which config file to use
            string configPath;
            string scope;
            if (local)
            {
                configPath = GetLocalConfigPath();
                scope = "local";
            }
            else
            {
                configPath = GetGlobalConfigPath();
                scope = "global";
            }

            // Load existing configuration
            var configData = LoadConfigFile(configPath);

            // Handle list operation
            if (list)
            {
                if (configData == null || configData.Count == 0)
                {
                    logger.Info($"No {scope} configuration set");
                    return 0;
                }

                logger.Info($"{scope.Substring(0, 1).ToUpperInvariant() + scope.Substring(1)} configuration ({configPath}):");
                var sortedKeys = configData.Keys.OrderBy(k => k);
                foreach (var configKey in sortedKeys)
                {
                    var configValue = FormatConfigValue(configData[configKey]);
                    logger.Info($"  {configKey} = {configValue}");
                }
                return 0;
            }

            // Handle unset operation
            if (unset)
            {
                if (string.IsNullOrEmpty(key))
                {
                    logger.Error("Key required for --unset operation");
                    return 1;
                }

                if (configData != null && configData.ContainsKey(key))
                {
                    configData.Remove(key);
                    SaveConfigFile(configPath, configData);
                    logger.Info($"Unset {scope} config: {key}");
                    return 0;
                }
                logger.Warning($"Key not found in {scope} config: {key}");
                return 0;
            }

            // Handle get operation (default if only key provided)
            if (!string.IsNullOrEmpty(key) && string.IsNullOrEmpty(value) && !set)
            {
                if (configData == null || !configData.ContainsKey(key))
                {
                    logger.Info($"{key} is not set in {scope} config");
                }
                else
                {
                    var configValue = FormatConfigValue(configData[key]);
                    logger.Info(configValue);
                }
                return 0;
            }

            // Handle set operation (default if both key and value provided)
            if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(value))
            {
                if (configData == null)
                {
                    configData = new Dictionary<string, object>();
                }
                configData[key] = value;
                SaveConfigFile(configPath, configData);
                logger.Info($"Set {scope} config: {key} = {value}");
                return 0;
            }

            // No operation specified
            logger.Error("No configuration operation specified. Use --get, --set, --unset, or --list");
            return 1;
        }

        /// <summary>
        /// Get the configuration directory based on platform.
        /// </summary>
        private static string GetConfigDir()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var appData = Environment.GetEnvironmentVariable("APPDATA");
                if (string.IsNullOrEmpty(appData))
                {
                    appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Roaming");
                }
                return Path.Combine(appData, "kotorcli");
            }
            else
            {
                // Linux, Mac
                var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                if (string.IsNullOrEmpty(xdgConfigHome))
                {
                    xdgConfigHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                }
                return Path.Combine(xdgConfigHome, "kotorcli");
            }
        }

        /// <summary>
        /// Get the global configuration file path.
        /// </summary>
        private static string GetGlobalConfigPath()
        {
            return Path.Combine(GetConfigDir(), "user.cfg");
        }

        /// <summary>
        /// Get the local configuration file path (in current package).
        /// </summary>
        private static string GetLocalConfigPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), ".kotorcli", "user.cfg");
        }

        /// <summary>
        /// Load a configuration file.
        /// </summary>
        private static Dictionary<string, object> LoadConfigFile(string configPath)
        {
            if (!File.Exists(configPath))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                var content = File.ReadAllText(configPath, Encoding.UTF8);
                var tomlTable = Toml.ToModel(content);
                return ConvertTomlTableToDictionary(tomlTable);
            }
            catch (Exception)
            {
                return new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// Convert a TomlTable to a Dictionary for easier manipulation.
        /// </summary>
        private static Dictionary<string, object> ConvertTomlTableToDictionary(TomlTable table)
        {
            var result = new Dictionary<string, object>();
            if (table == null)
            {
                return result;
            }

            foreach (var kvp in table)
            {
                result[kvp.Key] = ConvertTomlValue(kvp.Value);
            }

            return result;
        }

        /// <summary>
        /// Convert a TOML value to a plain object.
        /// </summary>
        private static object ConvertTomlValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is TomlTable table)
            {
                return ConvertTomlTableToDictionary(table);
            }

            if (value is TomlArray array)
            {
                return array.Select(v => ConvertTomlValue(v)).ToList();
            }

            return value;
        }

        /// <summary>
        /// Save a configuration file.
        /// </summary>
        private static void SaveConfigFile(string configPath, Dictionary<string, object> data)
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Convert dictionary to TOML string
            var tomlContent = new StringBuilder();
            foreach (var kvp in data.OrderBy(k => k.Key))
            {
                var value = FormatTomlValue(kvp.Value);
                tomlContent.AppendLine($"{kvp.Key} = {value}");
            }

            File.WriteAllText(configPath, tomlContent.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Format a value for TOML output.
        /// </summary>
        private static string FormatTomlValue(object value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value is string str)
            {
                // Escape quotes and backslashes
                var escaped = str.Replace("\\", "\\\\").Replace("\"", "\\\"");
                return $"\"{escaped}\"";
            }

            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            if (value is int || value is long)
            {
                return value.ToString();
            }

            if (value is float || value is double || value is decimal)
            {
                return value.ToString();
            }

            // Default: convert to string
            var strValue = value.ToString();
            var escapedValue = strValue.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escapedValue}\"";
        }

        /// <summary>
        /// Format a config value for display.
        /// </summary>
        private static string FormatConfigValue(object value)
        {
            if (value == null)
            {
                return "\"\"";
            }

            if (value is string str)
            {
                return $"\"{str}\"";
            }

            if (value is bool b)
            {
                return b ? "true" : "false";
            }

            return value.ToString();
        }
    }
}

