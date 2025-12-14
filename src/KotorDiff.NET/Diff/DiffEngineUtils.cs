// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:78-110
// Original: def is_kotor_install_dir(...), def get_module_root(...), etc.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KotorDiff.NET.Diff
{
    // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:78-80
    // Original: def is_kotor_install_dir(path: Path) -> bool | None: ...
    public static class DiffEngineUtils
    {
        public static bool IsKotorInstallDir(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                return false;
            }
            string chitinKey = Path.Combine(path, "chitin.key");
            return File.Exists(chitinKey);
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:88-93
        // Original: def get_module_root(module_filepath: Path) -> str: ...
        public static string GetModuleRoot(string moduleFilepath)
        {
            string root = Path.GetFileNameWithoutExtension(moduleFilepath).ToLowerInvariant();
            if (root.EndsWith("_s"))
            {
                root = root.Substring(0, root.Length - 2);
            }
            if (root.EndsWith("_dlg"))
            {
                root = root.Substring(0, root.Length - 4);
            }
            return root;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1035-1057
        // Original: def is_text_content(data: bytes) -> bool: ...
        public static bool IsTextContent(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return true;
            }

            try
            {
                // Try to decode as UTF-8 first
                Encoding.UTF8.GetString(data);
                return true;
            }
            catch (DecoderFallbackException)
            {
                // Try Windows-1252 (common for KOTOR text files)
                try
                {
                    Encoding.GetEncoding(1252).GetString(data);
                    return true;
                }
                catch (DecoderFallbackException)
                {
                    // Check for high ratio of printable ASCII characters
                    const int PRINTABLE_ASCII_MIN = 32;
                    const int PRINTABLE_ASCII_MAX = 126;
                    const double TEXT_THRESHOLD = 0.7;

                    int printableCount = data.Count(b => 
                        (b >= PRINTABLE_ASCII_MIN && b <= PRINTABLE_ASCII_MAX) || 
                        b == 9 || b == 10 || b == 13);
                    return (double)printableCount / data.Length > TEXT_THRESHOLD;
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1060-1068
        // Original: def read_text_lines(filepath: Path) -> list[str]: ...
        public static List<string> ReadTextLines(string filepath)
        {
            try
            {
                return File.ReadAllLines(filepath, Encoding.UTF8).ToList();
            }
            catch (Exception)
            {
                try
                {
                    return File.ReadAllLines(filepath, Encoding.GetEncoding(1252)).ToList();
                }
                catch (Exception)
                {
                    return new List<string>();
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1205-1210
        // Original: def should_skip_rel(_rel: str) -> bool: ...
        public static bool ShouldSkipRel(string rel)
        {
            return false; // Currently unused but kept for future filtering capabilities
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1175-1187
        // Original: def visual_length(...): ...
        /// <summary>
        /// Calculate visual length of string accounting for tabs.
        /// </summary>
        public static int VisualLength(string s, int tabLength = 8)
        {
            if (string.IsNullOrEmpty(s) || !s.Contains("\t"))
            {
                return s?.Length ?? 0;
            }

            string[] parts = s.Split('\t');
            int visLength = parts.Sum(part => part.Length);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                visLength += tabLength - (parts[i].Length % tabLength);
            }
            return visLength;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1190-1196
        // Original: def walk_files(root: Path) -> set[str]: ...
        /// <summary>
        /// Walk all files in a directory tree.
        /// </summary>
        public static HashSet<string> WalkFiles(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root) && !File.Exists(root))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (File.Exists(root))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFileName(root).ToLowerInvariant() };
            }

            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(root, file).Replace('\\', '/').ToLowerInvariant();
                files.Add(relPath);
            }
            return files;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1213-1233
        // Original: def print_udiff(...): ...
        /// <summary>
        /// Print unified diff between two files.
        /// </summary>
        public static void PrintUdiff(string fromFile, string toFile, string labelFrom, string labelTo, Action<string> logFunc)
        {
            if (logFunc == null)
            {
                logFunc = Console.WriteLine;
            }

            List<string> a = ReadTextLines(fromFile);
            List<string> b = ReadTextLines(toFile);
            if (a.Count == 0 && b.Count == 0)
            {
                return;
            }

            // Simple unified diff implementation
            // For a full implementation, would need a proper diff library
            int maxLines = Math.Max(a.Count, b.Count);
            for (int i = 0; i < maxLines; i++)
            {
                string lineA = i < a.Count ? a[i] : "";
                string lineB = i < b.Count ? b[i] : "";
                if (lineA != lineB)
                {
                    if (i < a.Count)
                    {
                        logFunc($"--- {labelFrom}:{i + 1}");
                        logFunc($"-{lineA}");
                    }
                    if (i < b.Count)
                    {
                        logFunc($"+++ {labelTo}:{i + 1}");
                        logFunc($"+{lineB}");
                    }
                }
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:2252-2288
        // Original: def should_include_in_filtered_diff(...): ...
        /// <summary>
        /// Check if a file should be included based on filter criteria.
        /// </summary>
        public static bool ShouldIncludeInFilteredDiff(string filePath, List<string> filters)
        {
            if (filters == null || filters.Count == 0)
            {
                return true; // No filters means include everything
            }

            var filePathInfo = new FileInfo(filePath);
            foreach (string filterPattern in filters)
            {
                var filterPath = new FileInfo(filterPattern);

                // Direct filename match: check via filename equality or filename containment
                if (string.Equals(filterPath.Name, filePathInfo.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Check parent directory names
                var fileParent = filePathInfo.Directory;
                while (fileParent != null)
                {
                    if (string.Equals(filterPath.Name, fileParent.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                    fileParent = fileParent.Parent;
                }

                // Module name match (for .rim/.mod/.erf files)
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".rim" || ext == ".mod" || ext == ".erf")
                {
                    try
                    {
                        string root = GetModuleRoot(filePath);
                        if (!string.IsNullOrEmpty(filterPath.Name) && 
                            string.Equals(filterPath.Name, root, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        // Continue to next filter
                    }
                }
            }

            return false;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1199-1202
        // Original: def ext_of(path: Path) -> str: ...
        public static string ExtOf(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext.StartsWith(".") ? ext.Substring(1) : ext;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1070-1100
        // Original: def compare_text_content(...): ...
        public static bool CompareTextContent(byte[] data1, byte[] data2, string where)
        {

            string text1;
            string text2;

            try
            {
                text1 = Encoding.UTF8.GetString(data1);
                text2 = Encoding.UTF8.GetString(data2);
            }
            catch (Exception)
            {
                try
                {
                    text1 = Encoding.GetEncoding(1252).GetString(data1);
                    text2 = Encoding.GetEncoding(1252).GetString(data2);
                }
                catch (Exception)
                {
                    // Last resort - treat as binary
                    return data1.SequenceEqual(data2);
                }
            }

            if (text1 == text2)
            {
                return true;
            }

            // Simple line-by-line diff for now
            var lines1 = text1.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var lines2 = text2.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            bool hasDiff = false;
            int maxLines = Math.Max(lines1.Length, lines2.Length);

            for (int i = 0; i < maxLines; i++)
            {
                string line1 = i < lines1.Length ? lines1[i] : "";
                string line2 = i < lines2.Length ? lines2[i] : "";

                if (line1 != line2)
                {
                    hasDiff = true;
                    break; // Found difference, no need to continue
                }
            }

            return !hasDiff;
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1102-1108
        // Original: def generate_hash(data: bytes) -> str: ...
        public static string CalculateSha256(byte[] data)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(data);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            }
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:113-130
        // Original: def _is_readonly_source(source_path: Path) -> bool: ...
        /// <summary>
        /// Check if a source path is read-only (RIM, ERF, BIF, etc.).
        /// </summary>
        public static bool IsReadonlySource(string sourcePath)
        {
            if (string.IsNullOrEmpty(sourcePath))
            {
                return false;
            }

            string sourceLower = sourcePath.ToLowerInvariant();
            string suffix = Path.GetExtension(sourcePath).ToLowerInvariant();

            // RIM and ERF files are read-only
            if (suffix == ".rim" || suffix == ".erf")
            {
                return true;
            }

            // Files in BIF archives (chitin references)
            return sourceLower.Contains("chitin") || sourceLower.Contains("bif") || sourceLower.Contains("data");
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:133-152
        // Original: def _determine_tslpatchdata_source(...): ...
        /// <summary>
        /// Determine which source file should be copied to tslpatchdata.
        /// </summary>
        public static string DetermineTslpatchdataSource(string file1Path, string file2Path = null)
        {
            // Implement 2-way logic (use vanilla/base version)
            // N-way comparison uses the first path as the base source
            return $"vanilla ({file1Path.Replace('/', Path.DirectorySeparatorChar)})";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:155-261
        // Original: def _determine_destination_for_source(...): ...
        /// <summary>
        /// Determine the proper TSLPatcher destination based on resource resolution order.
        /// </summary>
        public static string DetermineDestinationForSource(
            string sourcePath,
            string resourceName = null,
            bool verbose = true,
            Action<string> logFunc = null,
            string locationType = null,
            string sourceFilepath = null)
        {
            if (logFunc == null)
            {
                logFunc = _ => { };
            }

            string displayName = !string.IsNullOrEmpty(resourceName) ? resourceName : Path.GetFileName(sourcePath);

            // PRIORITY 1: Use explicit location_type if provided (resolution-aware path)
            if (!string.IsNullOrEmpty(locationType))
            {
                if (locationType == "Override folder")
                {
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in Override");
                        logFunc("    +-- Destination: Override (highest priority)");
                    }
                    return "Override";
                }

                if (locationType == "Modules (.mod)")
                {
                    // Resource is in a .mod file - patch directly to that .mod
                    string actualFilepath = !string.IsNullOrEmpty(sourceFilepath) ? sourceFilepath : sourcePath;
                    string destination = $"modules\\{Path.GetFileName(actualFilepath)}";
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in {Path.GetFileName(actualFilepath)}");
                        logFunc($"    +-- Destination: {destination} (patch .mod directly)");
                    }
                    return destination;
                }

                if (locationType == "Modules (.rim)" || locationType == "Modules (.rim/_s.rim/_dlg.erf)")
                {
                    // Resource is in read-only .rim/.erf - redirect to corresponding .mod
                    string actualFilepath = !string.IsNullOrEmpty(sourceFilepath) ? sourceFilepath : sourcePath;
                    string moduleRoot = GetModuleRoot(actualFilepath);
                    string destination = $"modules\\{moduleRoot}.mod";
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in {Path.GetFileName(actualFilepath)} (read-only)");
                        logFunc($"    +-- Destination: {destination} (.mod overrides .rim/.erf)");
                    }
                    return destination;
                }

                if (locationType == "Chitin BIFs")
                {
                    // Resource only in BIFs - must go to Override (can't modify BIFs)
                    if (verbose)
                    {
                        logFunc($"    +-- Resolution: {displayName} found in Chitin BIFs (read-only)");
                        logFunc("    +-- Destination: Override (BIFs cannot be modified)");
                    }
                    return "Override";
                }

                // Unknown location type - log warning and fall through to path inference
                if (verbose)
                {
                    logFunc($"    +-- Warning: Unknown location_type '{locationType}', using path inference");
                }
            }

            // FALLBACK: Path-based inference (for non-resolution-aware code paths)
            var sourceFileInfo = !string.IsNullOrEmpty(sourceFilepath) ? new FileInfo(sourceFilepath) : new FileInfo(sourcePath);
            var parentNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (sourceFileInfo.Directory != null)
            {
                var dir = sourceFileInfo.Directory;
                while (dir != null)
                {
                    parentNames.Add(dir.Name);
                    dir = dir.Parent;
                }
            }

            if (parentNames.Contains("override"))
            {
                // Determine if it's a read-only source (RIM/ERF)
                if (!IsReadonlySource(sourcePath))
                {
                    // MOD file - can patch directly
                    string destination = $"modules\\{Path.GetFileName(sourcePath)}";
                    if (verbose)
                    {
                        logFunc($"    +-- Path inference: {displayName} in writable .mod");
                        logFunc($"    +-- Destination: {destination} (patch directly)");
                    }
                    return destination;
                }
                // Read-only module file - redirect to .mod
                string moduleRoot2 = GetModuleRoot(sourcePath);
                string destination2 = $"modules\\{moduleRoot2}.mod";
                if (verbose)
                {
                    logFunc($"    +-- Path inference: {displayName} in read-only {Path.GetExtension(sourcePath)}");
                    logFunc($"    +-- Destination: {destination2} (.mod overrides read-only)");
                }
                return destination2;
            }

            // BIF/chitin sources go to Override
            if (IsReadonlySource(sourcePath))
            {
                if (verbose)
                {
                    logFunc($"    +-- Path inference: {displayName} in read-only BIF/chitin");
                    logFunc("    +-- Destination: Override (read-only source)");
                }
                return "Override";
            }

            // Default to Override for other cases
            if (verbose)
            {
                logFunc($"    +-- Path inference: {displayName} (no specific location detected)");
                logFunc("    +-- Destination: Override (default)");
            }
            return "Override";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1110-1118
        // Original: def is_capsule_file(filename: str) -> bool: ...
        public static bool IsCapsuleFile(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return false;
            }
            string ext = Path.GetExtension(filename).ToLowerInvariant();
            return ext == ".erf" || ext == ".mod" || ext == ".rim";
        }

        // Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:1906-1928
        // Original: def should_use_composite_for_file(...): ...
        public static bool ShouldUseCompositeForFile(string filePath, string otherFilePath)
        {
            // Check if this file is a .rim file (not in rims folder)
            if (!IsCapsuleFile(Path.GetFileName(filePath)))
            {
                return false;
            }
            string parentName = Path.GetDirectoryName(filePath);
            if (parentName != null && Path.GetFileName(parentName).ToLowerInvariant() == "rims")
            {
                return false;
            }
            if (Path.GetExtension(filePath).ToLowerInvariant() != ".rim")
            {
                return false;
            }

            // Check if the other file is a .mod file (not in rims folder)
            if (!IsCapsuleFile(Path.GetFileName(otherFilePath)))
            {
                return false;
            }
            string otherParentName = Path.GetDirectoryName(otherFilePath);
            if (otherParentName != null && Path.GetFileName(otherParentName).ToLowerInvariant() == "rims")
            {
                return false;
            }
            return Path.GetExtension(otherFilePath).ToLowerInvariant() == ".mod";
        }
    }
}

