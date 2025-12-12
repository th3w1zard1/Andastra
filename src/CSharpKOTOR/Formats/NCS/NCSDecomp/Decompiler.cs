// 
// Decompiler settings and utilities - UI is in NCSDecomp project (Avalonia)
//
using System;
using System.Collections.Generic;
using System.IO;

namespace CSharpKOTOR.Formats.NCS.NCSDecomp
{
    /// <summary>
    /// Static settings and utilities for the NCS decompiler.
    /// The actual UI is implemented in the NCSDecomp project using Avalonia.
    /// </summary>
    public static class Decompiler
    {
        public static Settings settings;
        public static readonly double screenWidth;
        public static readonly double screenHeight;

        static Decompiler()
        {
            // Default screen dimensions (will be overridden by Avalonia UI)
            screenWidth = 1920;
            screenHeight = 1080;
            (Decompiler.settings = new Settings()).Load();
            string outputDir = Decompiler.settings.GetProperty("Output Directory");
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                // Default to current directory if output directory is not set
                // The Avalonia UI will prompt the user to select a directory
                Decompiler.settings.SetProperty("Output Directory", Directory.GetCurrentDirectory());
                Decompiler.settings.Save();
            }
        }

        public static void Exit()
        {
            JavaSystem.Exit(0);
        }

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




