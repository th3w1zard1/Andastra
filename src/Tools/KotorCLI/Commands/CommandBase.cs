using System;
using System.CommandLine;
using System.IO;
using KotorCLI.Configuration;
using KotorCLI.Logging;

namespace KotorCLI.Commands
{
    /// <summary>
    /// Base class for all KotorCLI commands with common functionality.
    /// </summary>
    public abstract class CommandBase
    {
        protected static ILogger GetLogger(bool verbose, bool debug, bool quiet, bool noColor)
        {
            if (quiet)
            {
                return new QuietLogger();
            }
            if (debug)
            {
                return new DebugLogger(noColor);
            }
            if (verbose)
            {
                return new VerboseLogger(noColor);
            }
            return new StandardLogger(noColor);
        }

        protected static KotorCLIConfig LoadConfig(ILogger logger, string configPath = null)
        {
            if (configPath == null)
            {
                configPath = ConfigFileFinder.FindConfigFile();
            }

            if (configPath == null)
            {
                logger.Error("This is not a kotorcli repository. Please run 'kotorcli init'");
                return null;
            }

            try
            {
                return new KotorCLIConfig(configPath);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to load configuration: {ex.Message}");
                if (logger.IsDebug)
                {
                    logger.Debug($"Stack trace: {ex.StackTrace}");
                }
                return null;
            }
        }

        protected static bool PromptYesNo(ILogger logger, string message, bool? defaultYes, bool autoYes, bool autoNo)
        {
            if (autoYes)
            {
                return true;
            }
            if (autoNo)
            {
                return false;
            }

            string prompt = message;
            if (defaultYes.HasValue)
            {
                prompt += defaultYes.Value ? " [Y/n]" : " [y/N]";
            }
            prompt += ": ";

            logger.Info(prompt);
            string response = Console.ReadLine()?.Trim().ToLowerInvariant();
            
            if (string.IsNullOrEmpty(response) && defaultYes.HasValue)
            {
                return defaultYes.Value;
            }

            return response == "y" || response == "yes";
        }
    }
}

