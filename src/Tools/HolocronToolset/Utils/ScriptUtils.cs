using System;
using System.IO;
using HolocronToolset.Data;

namespace HolocronToolset.Utils
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_utils.py:34
    // Original: class NoOpRegistrySpoofer:
    public class NoOpRegistrySpoofer : IDisposable
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_utils.py:35-45
        // Original: def __enter__(self) -> Self: / def __exit__(...):
        public NoOpRegistrySpoofer()
        {
            System.Console.WriteLine("Enter NoOpRegistrySpoofer");
        }

        public void Dispose()
        {
            System.Console.WriteLine("Exit NoOpRegistrySpoofer");
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_utils.py:48-68
    // Original: def setup_extract_path() -> Path:
    public static class ScriptUtils
    {
        public static string SetupExtractPath()
        {
            var settings = new GlobalSettings();
            string extractPath = settings.GetValue<string>("ExtractPath", "");

            if (string.IsNullOrEmpty(extractPath) || !Directory.Exists(extractPath))
            {
                // Prompt user for directory - will be implemented when file dialogs are available
                // TODO: STUB - For now, use temp directory
                extractPath = Path.Combine(Path.GetTempPath(), "HolocronToolset");
                Directory.CreateDirectory(extractPath);
            }

            settings.SetValue("ExtractPath", extractPath);
            return extractPath;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/utils/script_utils.py:71-109
        // Original: def handle_permission_error(...):
        public static void HandlePermissionError(NoOpRegistrySpoofer regSpoofer, string installationPath, Exception e)
        {
            // Handle permission errors - will be implemented when MessageBox is available
            System.Console.WriteLine($"Permission error: {e}");
        }
    }
}
