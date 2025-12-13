//
// Decompiler settings and utilities - UI is in NCSDecomp project (Avalonia)
//
using System;
using System.Collections.Generic;
using System.IO;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
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
            if (outputDir == null || outputDir.Equals("") || !new File(outputDir).IsDirectory())
            {
                string defaultOutputDir = new File(new File(JavaSystem.GetProperty("user.dir")), "ncsdecomp_converted").GetAbsolutePath();
                // If default doesn't exist, try to create it, otherwise prompt user
                File defaultDir = new File(defaultOutputDir);
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
        // Original: public static String chooseOutputDirectory() { JFileChooser jFC = new JFileChooser(settings.getProperty("Output Directory")); ... }
        // Note: C# version simplified for CLI compatibility - UI version is in NCSDecomp MainWindow
        public static string ChooseOutputDirectory()
        {
            // Synchronous version for compatibility - returns current setting
            // The async version with UI is in the NCSDecomp MainWindow
            return Decompiler.settings.GetProperty("Output Directory").Equals("")
                ? JavaSystem.GetProperty("user.dir")
                : Decompiler.settings.GetProperty("Output Directory");
        }
    }
}




