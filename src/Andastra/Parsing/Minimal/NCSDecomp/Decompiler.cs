//
// Decompiler settings and utilities - UI is in NCSDecomp project (Avalonia)
//
using System;
using System.Collections.Generic;
using System.IO;

namespace Andastra.Parsing.Formats.NCS.NCSDecomp
{
    // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:104-175
    // Original: public class Decompiler extends JFrame ... public static Settings settings = new Settings(); ... static { ... }
    /// <summary>
    /// Static settings and utilities for the NCS decompiler.
    /// The actual UI is implemented in the NCSDecomp project using Avalonia.
    /// </summary>
    public static class Decompiler
    {
        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:106-108
        // Original: public static Settings settings = new Settings(); public static final double screenWidth = Toolkit.getDefaultToolkit().getScreenSize().getWidth(); public static final double screenHeight = Toolkit.getDefaultToolkit().getScreenSize().getHeight();
        public static Settings settings;
        public static readonly double screenWidth;
        public static readonly double screenHeight;

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:121-122
        // Original: private static final String[] LOG_LEVELS = {"TRACE", "DEBUG", "INFO", "WARNING", "ERROR"}; private static final int DEFAULT_LOG_LEVEL_INDEX = 2; // INFO
        public static readonly string[] LogLevels = { "TRACE", "DEBUG", "INFO", "WARNING", "ERROR" };
        public const int DefaultLogLevelIndex = 2; // INFO

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:152-153
        // Original: private static final String CARD_EMPTY = "empty"; private static final String CARD_TABS = "tabs";
        public const string CardEmpty = "empty";
        public const string CardTabs = "tabs";

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:156-158
        // Original: private static final String PROJECT_URL = "https://bolabaden.org"; private static final String GITHUB_URL = "https://github.com/bolabaden"; private static final String SPONSOR_URL = "https://github.com/sponsors/th3w1zard1";
        public const string ProjectUrl = "https://bolabaden.org";
        public const string GitHubUrl = "https://github.com/bolabaden";
        public const string SponsorUrl = "https://github.com/sponsors/th3w1zard1";

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:124-129
        // Original: private enum LogSeverity { TRACE, DEBUG, INFO, WARNING, ERROR }
        /// <summary>
        /// Log severity levels for UI log filtering.
        /// </summary>
        public enum LogSeverity
        {
            TRACE,
            DEBUG,
            INFO,
            WARNING,
            ERROR
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:150-175
        // Original: static { settings.load(); String outputDir = settings.getProperty("Output Directory"); ... }
        static Decompiler()
        {
            // Default screen dimensions (will be overridden by Avalonia UI)
            screenWidth = 1920;
            screenHeight = 1080;
            (Decompiler.settings = new Settings()).Load();
            string outputDir = Decompiler.settings.GetProperty("Output Directory");
            // If output directory is not set or empty, use default: ./ncsdecomp_converted
            if (outputDir == null || outputDir.Equals("") || !new NcsFile(outputDir).IsDirectory())
            {
                string defaultOutputDir = new NcsFile(new NcsFile(JavaSystem.GetProperty("user.dir")), "ncsdecomp_converted").GetAbsolutePath();
                // If default doesn't exist, try to create it, otherwise prompt user
                NcsFile defaultDir = new NcsFile(defaultOutputDir);
                if (!defaultDir.Exists())
                {
                    if (defaultDir.Mkdirs())
                    {
                        Decompiler.settings.SetProperty("Output Directory", defaultOutputDir);
                    }
                    else
                    {
                        // If we can't create it, prompt user
                        Decompiler.settings.SetProperty("Output Directory", ChooseOutputDirectory());
                    }
                }
                else
                {
                    Decompiler.settings.SetProperty("Output Directory", defaultOutputDir);
                }
                Decompiler.settings.Save();
            }
            // Apply game variant setting to FileDecompiler
            string gameVariant = Decompiler.settings.GetProperty("Game Variant", "k1").ToLower();
            FileDecompiler.isK2Selected = gameVariant.Equals("k2") || gameVariant.Equals("tsl") || gameVariant.Equals("2");
            FileDecompiler.preferSwitches = bool.Parse(Decompiler.settings.GetProperty("Prefer Switches", "false"));
            FileDecompiler.strictSignatures = bool.Parse(Decompiler.settings.GetProperty("Strict Signatures", "false"));
        }

        public static void Exit()
        {
            JavaSystem.Exit(0);
        }

        // Matching DeNCS implementation at vendor/DeNCS/src/main/java/com/kotor/resource/formats/ncs/Decompiler.java:2289-2304
        // Original: public static String chooseOutputDirectory() { JFileChooser jFC = new JFileChooser(settings.getProperty("Output Directory")); jFC.setFileSelectionMode(JFileChooser.DIRECTORIES_ONLY); ... }
        // Based on swkotor2.exe: Directory selection for output files
        // Original implementation: Shows JFileChooser dialog to select output directory
        // C# implementation: CLI-compatible console-based directory chooser (UI version is in NCSDecomp MainWindow)
        /// <summary>
        /// Prompts the user to choose an output directory via console input (CLI-compatible).
        /// The UI version with file picker dialog is in NCSDecomp MainWindow.ChooseOutputDirectoryAsync().
        /// </summary>
        /// <returns>The selected or validated directory path.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: Directory selection for output files
        /// Original Java implementation: Uses JFileChooser to show GUI dialog
        /// C# CLI implementation: Uses console prompts for headless/CLI compatibility
        /// - Prompts user to enter directory path
        /// - Validates path exists or can be created
        /// - Offers to create directory if it doesn't exist
        /// - Handles errors gracefully with fallback to current setting
        /// - Returns validated directory path or current setting as fallback
        /// </remarks>
        public static string ChooseOutputDirectory()
        {
            // Get initial directory from settings or use current working directory
            string initialDirectory = Decompiler.settings.GetProperty("Output Directory");
            if (string.IsNullOrEmpty(initialDirectory))
            {
                initialDirectory = JavaSystem.GetProperty("user.dir");
            }

            // Try to use console input if available (CLI mode)
            // If console is not available (e.g., in GUI mode during static initialization), fall back to default
            bool consoleAvailable = false;
            try
            {
                // Check if console is available (not redirected and can write/read)
                // Based on .NET Console API: IsOutputRedirected checks if output is redirected
                // We also need to check if we can actually interact with the console
                if (!System.Console.IsOutputRedirected)
                {
                    // Try to write to console to verify it's available
                    // If this succeeds, console is likely available for interaction
                    System.Console.Out.Flush();
                    consoleAvailable = true;
                }
            }
            catch
            {
                // Console check failed - assume not available
                consoleAvailable = false;
            }

            if (!consoleAvailable)
            {
                // Console not available - return current setting or default
                // This handles cases where static constructor runs before UI is available
                // or when running in a non-interactive environment
                return string.IsNullOrEmpty(initialDirectory) || !new NcsFile(initialDirectory).IsDirectory()
                    ? JavaSystem.GetProperty("user.dir")
                    : initialDirectory;
            }

            // Console is available - prompt user for directory
            System.Console.Out.WriteLine();
            System.Console.Out.WriteLine("=== NCSDecomp Output Directory Selection ===");
            System.Console.Out.WriteLine("Please enter the output directory path for decompiled files.");
            System.Console.Out.WriteLine("Press Enter to use the default directory, or type a custom path.");
            System.Console.Out.WriteLine();

            // Show current/default directory
            string defaultDir = new NcsFile(new NcsFile(JavaSystem.GetProperty("user.dir")), "ncsdecomp_converted").GetAbsolutePath();
            System.Console.Out.WriteLine($"Current setting: {initialDirectory}");
            System.Console.Out.WriteLine($"Default directory: {defaultDir}");
            System.Console.Out.Write("Enter directory path (or press Enter for default): ");

            string userInput = System.Console.In.ReadLine();
            if (userInput == null)
            {
                userInput = string.Empty;
            }
            userInput = userInput.Trim();

            // If user pressed Enter without input, use default
            if (string.IsNullOrEmpty(userInput))
            {
                userInput = defaultDir;
            }

            // Validate and process the directory path
            NcsFile selectedDir = new NcsFile(userInput);
            string absolutePath = selectedDir.GetAbsolutePath();

            // Check if directory exists
            if (selectedDir.Exists() && selectedDir.IsDirectory())
            {
                // Directory exists and is valid
                System.Console.Out.WriteLine($"Using existing directory: {absolutePath}");
                return absolutePath;
            }

            // Directory doesn't exist - check if parent exists
            NcsFile parentDir = selectedDir.GetParentFile();
            if (parentDir != null && parentDir.Exists() && parentDir.IsDirectory())
            {
                // Parent exists - offer to create the directory
                System.Console.Out.WriteLine($"Directory does not exist: {absolutePath}");
                System.Console.Out.Write("Create this directory? (Y/n): ");
                string createResponse = System.Console.In.ReadLine();
                if (createResponse == null)
                {
                    createResponse = string.Empty;
                }
                createResponse = createResponse.Trim().ToLower();

                if (string.IsNullOrEmpty(createResponse) || createResponse == "y" || createResponse == "yes")
                {
                    // Try to create the directory
                    if (selectedDir.Mkdirs())
                    {
                        System.Console.Out.WriteLine($"Created directory: {absolutePath}");
                        return absolutePath;
                    }
                    else
                    {
                        System.Console.Out.WriteLine($"ERROR: Failed to create directory: {absolutePath}");
                        System.Console.Out.WriteLine("Falling back to default directory.");
                        return defaultDir;
                    }
                }
                else
                {
                    // User declined to create - use default
                    System.Console.Out.WriteLine("Using default directory instead.");
                    return defaultDir;
                }
            }

            // Parent doesn't exist or path is invalid
            System.Console.Out.WriteLine($"ERROR: Invalid directory path or parent directory does not exist: {absolutePath}");
            System.Console.Out.WriteLine("Falling back to default directory.");

            // Try to create default directory if it doesn't exist
            NcsFile defaultDirFile = new NcsFile(defaultDir);
            if (!defaultDirFile.Exists())
            {
                if (defaultDirFile.Mkdirs())
                {
                    System.Console.Out.WriteLine($"Created default directory: {defaultDir}");
                }
                else
                {
                    System.Console.Out.WriteLine($"WARNING: Failed to create default directory: {defaultDir}");
                    System.Console.Out.WriteLine("Using current working directory instead.");
                    return JavaSystem.GetProperty("user.dir");
                }
            }

            return defaultDir;
        }
    }
}



