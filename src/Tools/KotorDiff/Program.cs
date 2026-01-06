// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py:97-140
// Original: def main(): ...
using System;
using System.IO;
using Avalonia;
using KotorDiff.Cli;

namespace KotorDiff
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py:52
    // Original: CURRENT_VERSION = "1.0.0"
    public static class Program
    {
        public const string CURRENT_VERSION = "1.0.0";

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py:121-127
        // Original: def is_running_from_temp(): ...
        private static bool IsRunningFromTemp()
        {
            string appPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string tempDir = Path.GetTempPath();
            return appPath.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py:97-118
        // Original: def main(): ...
        public static int Main(string[] args)
        {
            if (IsRunningFromTemp())
            {
                string errorMsg = "This application cannot be run from within a zip or temporary directory. Please extract it to a permanent location before running.";
                Console.Error.WriteLine($"[Error] {errorMsg}");
                return 1;
            }

            try
            {
                var cmdlineArgs = CliParser.ParseArgs(args);
                bool forceCli = CliParser.HasCliPaths(cmdlineArgs) && !cmdlineArgs.Gui;

                if (forceCli)
                {
                    CliExecutor.ExecuteCli(cmdlineArgs);
                    return 0;
                }
                else
                {
                    // GUI mode - launch Avalonia application
                    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py:108-120
                    // Original: from kotordiff.gui import KotorDiffApp; app = KotorDiffApp(); app.root.mainloop()
                    try
                    {
                        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
                        return 0;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[Warning] GUI not available: {ex.GetType().Name}: {ex.Message}");
                        Console.WriteLine("[Info] Use --help to see CLI options");
                        return 0;
                    }
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"[CRASH] {e.GetType().Name}: {e.Message}");
                Console.Error.WriteLine(e.StackTrace);
                return 1;
            }
        }

        // Build Avalonia application for GUI mode
        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py:108-120
        // Original: app = KotorDiffApp(); app.root.mainloop()
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}

