// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli_utils.py:13-75
// Original: def normalize_path_arg(path_str: str | None) -> str | None: ...
using System;
using System.IO;

namespace KotorDiff.NET.Cli
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli_utils.py:13-50
    // Original: def normalize_path_arg(path_str: str | None) -> str | None: ...
    public static class CliUtils
    {
        public static string NormalizePathArg(string pathStr)
        {
            if (string.IsNullOrEmpty(pathStr))
            {
                return null;
            }

            pathStr = pathStr.Trim();

            if (string.IsNullOrEmpty(pathStr))
            {
                return null;
            }

            // Handle Windows PowerShell quote escaping issues
            if (pathStr.Contains("\"") && pathStr.Contains(" "))
            {
                int quoteSpaceIdx = pathStr.IndexOf("\" ");
                if (quoteSpaceIdx > 0)
                {
                    pathStr = pathStr.Substring(0, quoteSpaceIdx);
                }
            }

            // Strip quotes if present
            if ((pathStr.StartsWith("\"") && pathStr.EndsWith("\"")) ||
                (pathStr.StartsWith("'") && pathStr.EndsWith("'")))
            {
                pathStr = pathStr.Substring(1, pathStr.Length - 2);
            }

            // Remove any remaining quotes
            pathStr = pathStr.Replace("\"", "").Replace("'", "");

            // Strip trailing backslashes
            pathStr = pathStr.TrimEnd('\\', '/');
            pathStr = pathStr.Trim();

            return string.IsNullOrEmpty(pathStr) ? null : pathStr;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli_utils.py:52-54
        // Original: def is_kotor_install_dir(path: Path) -> bool | None: ...
        public static bool IsKotorInstallDir(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }

            string chitinKey = Path.Combine(path, "chitin.key");
            return File.Exists(chitinKey);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli_utils.py:57-60
        // Original: def prompt_for_path(title: str) -> str: ...
        public static string PromptForPath(string title)
        {
            Console.Write($"{title}: ");
            string userInput = Console.ReadLine()?.Trim() ?? "";
            return NormalizePathArg(userInput) ?? "";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/cli_utils.py:63-75
        // Original: def print_path_error_with_help(path: Path, parser: argparse.ArgumentParser) -> None: ...
        public static void PrintPathErrorWithHelp(string path, bool showHelp = false)
        {
            Console.WriteLine($"Invalid path: {path}");
            string pathStr = path ?? "";
            if (pathStr.Contains("\"") || (path != null && !Directory.Exists(Path.GetDirectoryName(path))))
            {
                Console.WriteLine("\nNote: If using paths with spaces and trailing backslashes in PowerShell:");
                Console.WriteLine("  - Remove trailing backslash: --path1=\"C:\\Program Files\\folder\"");
                Console.WriteLine("  - Or double the backslash: --path1=\"C:\\Program Files\\folder\\\\\"");
                Console.WriteLine("  - Or use forward slashes: --path1=\"C:/Program Files/folder/\"");
            }
            if (showHelp)
            {
                // Help text would be printed by the parser
                Console.WriteLine("Use --help for usage information.");
            }
        }
    }
}

