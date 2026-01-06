// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/engine.py:78-110
// Original: def is_kotor_install_dir(...), def get_module_root(...), etc.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KotorDiff.Diff
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
        /// Complete implementation matching Python's difflib.unified_diff format.
        /// Uses Myers diff algorithm to find minimal set of changes with proper context lines.
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

            // Generate unified diff using Myers diff algorithm (matching Python difflib.unified_diff)
            var diffLines = GenerateUnifiedDiffLines(a, b, labelFrom, labelTo);
            foreach (string line in diffLines)
            {
                logFunc(line);
            }
        }

        /// <summary>
        /// Represents a single diff operation in the Myers algorithm.
        /// </summary>
        private class DiffOp
        {
            public enum OpType { Equal, Insert, Delete }

            public OpType Type { get; }
            public int OldIndex { get; }
            public int NewIndex { get; }
            public string Line { get; }

            public DiffOp(OpType type, int oldIndex, int newIndex, string line)
            {
                Type = type;
                OldIndex = oldIndex;
                NewIndex = newIndex;
                Line = line;
            }
        }

        /// <summary>
        /// Represents a hunk of changes in the unified diff format.
        /// </summary>
        private class DiffHunk
        {
            public int OldStart { get; set; }
            public int OldCount { get; set; }
            public int NewStart { get; set; }
            public int NewCount { get; set; }
            public int StartOpIndex { get; set; }
            public int EndOpIndex { get; set; }
        }

        /// <summary>
        /// Generate unified diff lines matching Python's difflib.unified_diff format.
        /// Uses Myers diff algorithm to find minimal set of changes with context lines.
        /// Reference: Python difflib.unified_diff implementation
        /// </summary>
        private static List<string> GenerateUnifiedDiffLines(List<string> oldLines, List<string> newLines, string fromFile, string toFile)
        {
            var result = new List<string>();

            // Use Myers diff algorithm to find minimal set of changes
            var operations = ComputeMyersDiff(oldLines, newLines);

            if (operations.Count == 0)
            {
                // Files are identical
                return result;
            }

            // Find hunks (groups of changes with context lines)
            // Python difflib.unified_diff uses default context=3
            const int CONTEXT_LINES = 3;
            var hunks = FindUnifiedDiffHunks(operations, oldLines.Count, newLines.Count, CONTEXT_LINES);

            if (hunks.Count == 0)
            {
                // No changes found (shouldn't happen if operations.Count > 0, but handle gracefully)
                return result;
            }

            // Add diff headers (matching Python difflib.unified_diff format)
            // Python format: "--- fromfile\n+++ tofile" (no timestamps by default)
            result.Add($"--- {fromFile}");
            result.Add($"+++ {toFile}");

            // Format each hunk
            foreach (var hunk in hunks)
            {
                result.AddRange(FormatUnifiedDiffHunk(hunk, operations, oldLines, newLines));
            }

            return result;
        }

        /// <summary>
        /// Myers diff algorithm implementation - finds minimal set of changes.
        /// Based on "An O(ND) Difference Algorithm and Its Variations" by Eugene W. Myers.
        /// This is the same algorithm used by Python's difflib.
        /// </summary>
        private static List<DiffOp> ComputeMyersDiff(List<string> oldLines, List<string> newLines)
        {
            int n = oldLines.Count;
            int m = newLines.Count;
            int max = n + m;

            // Trace array for backtracking: trace[d][k] stores the x-coordinate at depth d, diagonal k
            var trace = new List<int[]>();

            // Initialize trace
            for (int d = 0; d <= max; d++)
            {
                trace.Add(new int[2 * d + 1]);
                for (int k = -d; k <= d; k += 2)
                {
                    int x;
                    if (d == 0)
                    {
                        x = 0;
                    }
                    else if (k == -d || (k != d && trace[d - 1][k - 1 + d - 1] < trace[d - 1][k + 1 + d - 1]))
                    {
                        // Came from diagonal k+1 (down/insert)
                        x = trace[d - 1][k + 1 + d - 1];
                    }
                    else
                    {
                        // Came from diagonal k-1 (right/delete)
                        x = trace[d - 1][k - 1 + d - 1] + 1;
                    }

                    int y = x - k;

                    // Move diagonally as far as possible (find matching lines)
                    while (x < n && y < m && oldLines[x] == newLines[y])
                    {
                        x++;
                        y++;
                    }

                    trace[d][k + d] = x;

                    // Check if we've reached the end
                    if (x >= n && y >= m)
                    {
                        // Found the end, reconstruct the path
                        return ReconstructMyersPath(trace, d, k, oldLines, newLines);
                    }
                }
            }

            // This should never happen for valid inputs
            throw new InvalidOperationException("Myers diff algorithm failed to find a path");
        }

        /// <summary>
        /// Reconstruct the diff operations from the Myers algorithm trace.
        /// </summary>
        private static List<DiffOp> ReconstructMyersPath(List<int[]> trace, int finalD, int finalK, List<string> oldLines, List<string> newLines)
        {
            var operations = new List<DiffOp>();
            int x = oldLines.Count;
            int y = newLines.Count;
            int k = finalK;

            // Reconstruct path backwards from the end
            for (int currentD = finalD; currentD > 0; currentD--)
            {
                int prevK = k;
                int prevX;

                // Determine which diagonal we came from
                if (k == -currentD || (k != currentD && trace[currentD - 1][k - 1 + currentD - 1] < trace[currentD - 1][k + 1 + currentD - 1]))
                {
                    // Came from diagonal k+1 (down/insert)
                    prevK = k + 1;
                    prevX = trace[currentD - 1][k + 1 + currentD - 1];
                }
                else
                {
                    // Came from diagonal k-1 (right/delete)
                    prevK = k - 1;
                    prevX = trace[currentD - 1][k - 1 + currentD - 1] + 1;
                }

                int prevY = prevX - prevK;

                // Add operations for the diagonal move (equal lines)
                while (x > prevX && y > prevY)
                {
                    x--;
                    y--;
                    operations.Insert(0, new DiffOp(DiffOp.OpType.Equal, x, y, oldLines[x]));
                }

                // Add the change operation (insert or delete)
                if (x > prevX)
                {
                    // Delete operation (line removed from old)
                    x--;
                    operations.Insert(0, new DiffOp(DiffOp.OpType.Delete, x, y, oldLines[x]));
                }
                else if (y > prevY)
                {
                    // Insert operation (line added to new)
                    y--;
                    operations.Insert(0, new DiffOp(DiffOp.OpType.Insert, x, y, newLines[y]));
                }

                k = prevK;
            }

            // Add remaining equal operations at the beginning
            while (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
            {
                x--;
                y--;
                operations.Insert(0, new DiffOp(DiffOp.OpType.Equal, x, y, oldLines[x]));
            }

            return operations;
        }

        /// <summary>
        /// Find hunks of changes with context lines (matching Python difflib.unified_diff default context=3).
        /// Groups consecutive changes together and adds context lines before and after.
        /// </summary>
        private static List<DiffHunk> FindUnifiedDiffHunks(List<DiffOp> operations, int oldCount, int newCount, int contextLines)
        {
            var hunks = new List<DiffHunk>();

            int i = 0;
            while (i < operations.Count)
            {
                // Skip equal operations until we find a change
                while (i < operations.Count && operations[i].Type == DiffOp.OpType.Equal)
                {
                    i++;
                }

                if (i >= operations.Count)
                {
                    break;
                }

                // Found the start of a change hunk
                var hunk = new DiffHunk();
                hunk.StartOpIndex = Math.Max(0, i - contextLines);

                // Find the end of this change hunk
                int changeStart = i;
                while (i < operations.Count && operations[i].Type != DiffOp.OpType.Equal)
                {
                    i++;
                }
                int changeEnd = i - 1;

                hunk.EndOpIndex = Math.Min(operations.Count - 1, changeEnd + contextLines);

                // Calculate line numbers (1-based for unified diff format)
                hunk.OldStart = 1;
                hunk.NewStart = 1;

                // Count lines before the hunk start
                for (int j = 0; j < hunk.StartOpIndex; j++)
                {
                    if (operations[j].Type == DiffOp.OpType.Equal || operations[j].Type == DiffOp.OpType.Delete)
                    {
                        hunk.OldStart++;
                    }
                    if (operations[j].Type == DiffOp.OpType.Equal || operations[j].Type == DiffOp.OpType.Insert)
                    {
                        hunk.NewStart++;
                    }
                }

                // Count lines in the hunk
                hunk.OldCount = 0;
                hunk.NewCount = 0;

                for (int j = hunk.StartOpIndex; j <= hunk.EndOpIndex; j++)
                {
                    if (operations[j].Type == DiffOp.OpType.Equal || operations[j].Type == DiffOp.OpType.Delete)
                    {
                        hunk.OldCount++;
                    }
                    if (operations[j].Type == DiffOp.OpType.Equal || operations[j].Type == DiffOp.OpType.Insert)
                    {
                        hunk.NewCount++;
                    }
                }

                hunks.Add(hunk);
            }

            return hunks;
        }

        /// <summary>
        /// Format a single hunk as unified diff lines (matching Python difflib.unified_diff format).
        /// Format: @@ -oldStart,oldCount +newStart,newCount @@
        /// Lines: space for context, - for deleted, + for inserted
        /// </summary>
        private static List<string> FormatUnifiedDiffHunk(DiffHunk hunk, List<DiffOp> operations, List<string> oldLines, List<string> newLines)
        {
            var lines = new List<string>();

            // Add hunk header: @@ -oldStart,oldCount +newStart,newCount @@
            // Python format uses 1-based line numbers
            lines.Add($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");

            // Add the lines in the hunk
            for (int i = hunk.StartOpIndex; i <= hunk.EndOpIndex; i++)
            {
                var op = operations[i];
                string lineContent = op.Line;

                // Remove trailing newline/carriage return if present (lines are already split)
                // Python difflib.unified_diff with lineterm="" doesn't include newlines in the diff output
                lineContent = lineContent.TrimEnd('\r', '\n');

                switch (op.Type)
                {
                    case DiffOp.OpType.Equal:
                        // Context line: starts with space
                        lines.Add($" {lineContent}");
                        break;
                    case DiffOp.OpType.Delete:
                        // Deleted line: starts with minus
                        lines.Add($"-{lineContent}");
                        break;
                    case DiffOp.OpType.Insert:
                        // Inserted line: starts with plus
                        lines.Add($"+{lineContent}");
                        break;
                }
            }

            return lines;
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

            // TODO:  Simple line-by-line diff for now
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

