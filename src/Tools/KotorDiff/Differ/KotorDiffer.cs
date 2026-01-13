// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:69-437
// Original: class KotorDiffer: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BioWare.NET.Resource.Formats.Capsule;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.LIP;
using BioWare.NET.Resource.Formats.SSF;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource.Formats.TwoDA;
using KotorDiff.Diff;
using JetBrains.Annotations;

namespace KotorDiff.Differ
{
    /// <summary>
    /// Enhanced differ for KOTOR installations.
    /// 1:1 port of KotorDiffer from vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:69-437
    /// </summary>
    public class KotorDiffer
    {
        private readonly HashSet<string> _gffTypes;
        private static readonly string[] gffExtCollection = new [] {
                "are",
                "dlg",
                "fac",
                "git",
                "gff",
                "gui",
                "ifo",
                "jrl",
                "utc",
                "utd",
                "ute",
                "uti",
                "utm",
                "utp",
                "utw",
            };

        public KotorDiffer()
        {
            // GFF types: all GFF-based file formats
            _gffTypes = new HashSet<string>(
            gffExtCollection, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Compare two KOTOR installations and return comprehensive diff results.
        /// </summary>
        public DiffResult DiffInstallations(string path1, string path2)
        {
            var result = new DiffResult();

            // Check if paths are KOTOR installations
            if (!IsKotorInstall(path1))
            {
                result.AddError($"Path {path1} is not a valid KOTOR installation");
                return result;
            }
            if (!IsKotorInstall(path2))
            {
                result.AddError($"Path {path2} is not a valid KOTOR installation");
                return result;
            }

            // Compare key directories and files
            DiffDialogTlk(path1, path2, result);
            DiffDirectory(Path.Combine(path1, "Override"), Path.Combine(path2, "Override"), result);
            DiffDirectory(Path.Combine(path1, "Modules"), Path.Combine(path2, "Modules"), result);

            // Optional directories
            string rims1 = Path.Combine(path1, "rims");
            string rims2 = Path.Combine(path2, "rims");
            if (Directory.Exists(rims1) || Directory.Exists(rims2))
            {
                DiffDirectory(rims1, rims2, result);
            }
            string lips1 = Path.Combine(path1, "Lips");
            string lips2 = Path.Combine(path2, "Lips");
            if (Directory.Exists(lips1) || Directory.Exists(lips2))
            {
                DiffDirectory(lips1, lips2, result);
            }

            return result;
        }

        private static bool IsKotorInstall(string path)
        {
            return Directory.Exists(path) && File.Exists(Path.Combine(path, "chitin.key"));
        }

        private void DiffDialogTlk(string path1, string path2, DiffResult result)
        {
            string tlk1Path = Path.Combine(path1, "dialog.tlk");
            string tlk2Path = Path.Combine(path2, "dialog.tlk");

            if (File.Exists(tlk1Path) && File.Exists(tlk2Path))
            {
                var change = DiffTlkFiles(tlk1Path, tlk2Path, "dialog.tlk");
                if (change != null)
                {
                    result.AddChange(change);
                }
            }
            else if (File.Exists(tlk1Path) && !File.Exists(tlk2Path))
            {
                result.AddChange(new FileChange("dialog.tlk", "removed", "tlk"));
            }
            else if (!File.Exists(tlk1Path) && File.Exists(tlk2Path))
            {
                result.AddChange(new FileChange("dialog.tlk", "added", "tlk"));
            }
        }

        private void DiffDirectory(string dir1, string dir2, DiffResult result)
        {
            if (!Directory.Exists(dir1) && !Directory.Exists(dir2))
            {
                return;
            }

            // Get all files from both directories
            var files1 = new HashSet<string>();
            var files2 = new HashSet<string>();

            if (Directory.Exists(dir1))
            {
                foreach (string file in Directory.EnumerateFiles(dir1, "*", SearchOption.AllDirectories))
                {
                    files1.Add(Path.GetRelativePath(dir1, file));
                }
            }
            if (Directory.Exists(dir2))
            {
                foreach (string file in Directory.EnumerateFiles(dir2, "*", SearchOption.AllDirectories))
                {
                    files2.Add(Path.GetRelativePath(dir2, file));
                }
            }

            // Find added, removed, and common files
            var addedFiles = files2.Except(files1).ToList();
            var removedFiles = files1.Except(files2).ToList();
            var commonFiles = files1.Intersect(files2).ToList();

            // Process each type of change
            foreach (string filePath in addedFiles)
            {
                string fullPath = Path.Combine(Path.GetFileName(dir2) ?? "", filePath);
                result.AddChange(new FileChange(fullPath, "added", GetResourceType(filePath)));
            }

            foreach (string filePath in removedFiles)
            {
                string fullPath = Path.Combine(Path.GetFileName(dir1) ?? "", filePath);
                result.AddChange(new FileChange(fullPath, "removed", GetResourceType(filePath)));
            }

            foreach (string filePath in commonFiles)
            {
                string file1 = Path.Combine(dir1, filePath);
                string file2 = Path.Combine(dir2, filePath);
                var change = DiffFiles(file1, file2, Path.Combine(Path.GetFileName(dir1) ?? "", filePath));
                if (change != null)
                {
                    result.AddChange(change);
                }
            }
        }

        [CanBeNull]
        private FileChange DiffFiles(string file1, string file2, string relativePath)
        {
            try
            {
                if (DiffEngineUtils.IsCapsuleFile(Path.GetFileName(file1)))
                {
                    return DiffCapsuleFiles(file1, file2, relativePath);
                }
                else
                {
                    return DiffRegularFiles(file1, file2, relativePath);
                }
            }
            catch (Exception e)
            {
                // Return an error change
                var errorChange = new FileChange(relativePath, "error", GetResourceType(file1));
                errorChange.DiffLines = new List<string> { $"Error comparing files: {e.Message}" };
                return errorChange;
            }
        }

        [CanBeNull]
        private FileChange DiffCapsuleFiles(string file1, string file2, string relativePath)
        {
            try
            {
                var capsule1 = new Capsule(file1);
                var capsule2 = new Capsule(file2);

                // Get resources from both capsules
                var resources1 = new Dictionary<string, CapsuleResource>();
                var resources2 = new Dictionary<string, CapsuleResource>();

                foreach (var res in capsule1)
                {
                    resources1[res.ResName] = res;
                }
                foreach (var res in capsule2)
                {
                    resources2[res.ResName] = res;
                }

                // Check if contents are different
                if (!resources1.Keys.ToHashSet().SetEquals(resources2.Keys.ToHashSet()))
                {
                    return new FileChange(relativePath, "modified", "capsule");
                }

                // Compare individual resources
                foreach (string resname in resources1.Keys)
                {
                    if (resources2.ContainsKey(resname))
                    {
                        var res1 = resources1[resname];
                        var res2 = resources2[resname];
                        if (!CompareResourceData(res1, res2))
                        {
                            return new FileChange(relativePath, "modified", "capsule");
                        }
                    }
                }

                return null; // No changes
            }
            catch (Exception)
            {
                // Fall back to hash comparison
                return DiffByHash(file1, file2, relativePath);
            }
        }

        [CanBeNull]
        private FileChange DiffRegularFiles(string file1, string file2, string relativePath)
        {
            string ext = Path.GetExtension(file1).ToLowerInvariant().TrimStart('.');

            if (_gffTypes.Contains(ext))
            {
                return DiffGffFiles(file1, file2, relativePath);
            }
            else if (ext == "2da")
            {
                return Diff2DAFiles(file1, file2, relativePath);
            }
            else if (ext == "tlk")
            {
                return DiffTlkFiles(file1, file2, relativePath);
            }
            else if (ext == "ssf")
            {
                return DiffSsfFiles(file1, file2, relativePath);
            }
            else if (ext == "lip")
            {
                return DiffLipFiles(file1, file2, relativePath);
            }
            else
            {
                return DiffByHash(file1, file2, relativePath);
            }
        }

        [CanBeNull]
        private FileChange DiffGffFiles(string file1, string file2, string relativePath)
        {
            try
            {
                var gff1 = new GFFBinaryReader(file1).Load();
                var gff2 = new GFFBinaryReader(file2).Load();

                // Convert to text representation
                string text1 = GffToText(gff1);
                string text2 = GffToText(gff2);

                if (text1 != text2)
                {
                    var diffLines = GenerateUnifiedDiffLines(text1, text2, $"original/{relativePath}", $"modified/{relativePath}");
                    var change = new FileChange(relativePath, "modified", "gff");
                    change.OldContent = text1;
                    change.NewContent = text2;
                    change.DiffLines = diffLines;
                    return change;
                }

                return null;
            }
            catch (Exception)
            {
                return DiffByHash(file1, file2, relativePath);
            }
        }

        [CanBeNull]
        private FileChange Diff2DAFiles(string file1, string file2, string relativePath)
        {
            try
            {
                var twoda1 = new TwoDABinaryReader(file1).Load();
                var twoda2 = new TwoDABinaryReader(file2).Load();

                // Convert to text representation
                string text1 = TwoDaToText(twoda1);
                string text2 = TwoDaToText(twoda2);

                if (text1 != text2)
                {
                    var diffLines = GenerateUnifiedDiffLines(text1, text2, $"original/{relativePath}", $"modified/{relativePath}");
                    var change = new FileChange(relativePath, "modified", "2da");
                    change.OldContent = text1;
                    change.NewContent = text2;
                    change.DiffLines = diffLines;
                    return change;
                }

                return null;
            }
            catch (Exception)
            {
                return DiffByHash(file1, file2, relativePath);
            }
        }

        [CanBeNull]
        private FileChange DiffTlkFiles(string file1, string file2, string relativePath)
        {
            try
            {
                var tlk1 = new TLKBinaryReader(file1).Load();
                var tlk2 = new TLKBinaryReader(file2).Load();

                // Convert to text representation
                string text1 = TlkToText(tlk1);
                string text2 = TlkToText(tlk2);

                if (text1 != text2)
                {
                    var diffLines = GenerateUnifiedDiffLines(text1, text2, $"original/{relativePath}", $"modified/{relativePath}");
                    var change = new FileChange(relativePath, "modified", "tlk");
                    change.OldContent = text1;
                    change.NewContent = text2;
                    change.DiffLines = diffLines;
                    return change;
                }

                return null;
            }
            catch (Exception)
            {
                return DiffByHash(file1, file2, relativePath);
            }
        }

        [CanBeNull]
        private FileChange DiffSsfFiles(string file1, string file2, string relativePath)
        {
            try
            {
                var ssf1 = new SSFBinaryReader(file1).Load();
                var ssf2 = new SSFBinaryReader(file2).Load();

                // Simple comparison - compare byte arrays
                byte[] data1 = File.ReadAllBytes(file1);
                byte[] data2 = File.ReadAllBytes(file2);
                if (!data1.SequenceEqual(data2))
                {
                    return new FileChange(relativePath, "modified", "ssf");
                }

                return null;
            }
            catch (Exception)
            {
                return DiffByHash(file1, file2, relativePath);
            }
        }

        [CanBeNull]
        private FileChange DiffLipFiles(string file1, string file2, string relativePath)
        {
            try
            {
                var lip1 = new LIPBinaryReader(file1).Load();
                var lip2 = new LIPBinaryReader(file2).Load();

                // Simple comparison - compare byte arrays
                byte[] data1 = File.ReadAllBytes(file1);
                byte[] data2 = File.ReadAllBytes(file2);
                if (!data1.SequenceEqual(data2))
                {
                    return new FileChange(relativePath, "modified", "lip");
                }

                return null;
            }
            catch (Exception)
            {
                return DiffByHash(file1, file2, relativePath);
            }
        }

        [CanBeNull]
        private FileChange DiffByHash(string file1, string file2, string relativePath)
        {
            try
            {
                byte[] hash1 = CalculateSha256(file1);
                byte[] hash2 = CalculateSha256(file2);

                if (!hash1.SequenceEqual(hash2))
                {
                    return new FileChange(relativePath, "modified", GetResourceType(file1));
                }

                return null;
            }
            catch (Exception)
            {
                var errorChange = new FileChange(relativePath, "error", GetResourceType(file1));
                errorChange.DiffLines = new List<string> { "Error calculating hash" };
                return errorChange;
            }
        }

        private static bool CompareResourceData(CapsuleResource res1, CapsuleResource res2)
        {
            return res1.Data.SequenceEqual(res2.Data);
        }

        private static string GetResourceType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant().TrimStart('.');
            return ext;
        }

        private string GffToText(GFF gff)
        {
            // Convert GFF object to text representation for diffing
            // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:381-383
            // Original: def _gff_to_text(self, gff_obj: gff.GFF) -> str: return str(gff_obj.root)
            var sb = new StringBuilder();
            SerializeGffStruct(gff.Root, sb, 0);
            return sb.ToString();
        }

        private static void SerializeGffStruct(GFFStruct gffStruct, StringBuilder sb, int indent)
        {
            string indentStr = new string(' ', indent * 2);
            foreach (var (label, fieldType, value) in gffStruct)
            {
                sb.Append(indentStr);
                sb.Append(label);
                sb.Append(" (");
                sb.Append(fieldType);
                sb.Append("): ");

                if (value == null)
                {
                    sb.AppendLine("null");
                }
                else if (fieldType == GFFFieldType.Struct)
                {
                    sb.AppendLine("{");
                    SerializeGffStruct(value as GFFStruct, sb, indent + 1);
                    sb.Append(indentStr);
                    sb.AppendLine("}");
                }
                else if (fieldType == GFFFieldType.List)
                {
                    sb.AppendLine("[");
                    if (value is GFFList list)
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            sb.Append(indentStr + "  ");
                            sb.AppendLine($"Item {i}:");
                            SerializeGffStruct(list[i], sb, indent + 2);
                        }
                    }
                    sb.Append(indentStr);
                    sb.AppendLine("]");
                }
                else
                {
                    sb.AppendLine(value.ToString());
                }
            }
        }

        private static string TwoDaToText(TwoDA twoda)
        {
            var sb = new StringBuilder();
            // Write headers
            var headers = twoda.GetHeaders();
            sb.AppendLine(string.Join("\t", headers));
            // Write rows
            int height = twoda.GetHeight();
            for (int i = 0; i < height; i++)
            {
                var row = twoda.GetRow(i);
                var values = new List<string>();
                foreach (string header in headers)
                {
                    values.Add(row.GetString(header));
                }
                sb.AppendLine(string.Join("\t", values));
            }
            return sb.ToString();
        }

        private static string TlkToText(TLK tlk)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < tlk.Count; i++)
            {
                var entry = tlk.Entries[i];
                sb.AppendLine($"{i}: {entry.Text} [{entry.Voiceover}]");
            }
            return sb.ToString();
        }

        private List<string> GenerateUnifiedDiffLines(string text1, string text2, string fromFile, string toFile)
        {
            var leftLines = SplitLines(text1);
            var rightLines = SplitLines(text2);

            // Use Myers diff algorithm to find differences
            var diff = MyersDiff(leftLines, rightLines);

            // Format as unified diff
            return FormatUnifiedDiff(diff, leftLines, rightLines, fromFile, toFile);
        }

        /// <summary>
        /// Splits text into lines, preserving line endings like Python's splitlines(keepends=True)
        /// </summary>
        private static List<string> SplitLines(string text)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                return lines;
            }

            int start = 0;
            int i = 0;

            while (i < text.Length)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        // CRLF
                        lines.Add(text.Substring(start, i - start + 2));
                        i += 2;
                    }
                    else
                    {
                        // CR only
                        lines.Add(text.Substring(start, i - start + 1));
                        i++;
                    }
                    start = i;
                }
                else if (text[i] == '\n')
                {
                    // LF only
                    lines.Add(text.Substring(start, i - start + 1));
                    i++;
                    start = i;
                }
                else
                {
                    i++;
                }
            }

            // Add remaining text if any
            if (start < text.Length)
            {
                lines.Add(text.Substring(start));
            }

            return lines;
        }

        /// <summary>
        /// Represents a single diff operation
        /// </summary>
        private class DiffOperation
        {
            public enum OperationType { Equal, Insert, Delete }

            public OperationType Type { get; }
            public int OldIndex { get; }
            public int NewIndex { get; }
            public string Line { get; }

            public DiffOperation(OperationType type, int oldIndex, int newIndex, string line)
            {
                Type = type;
                OldIndex = oldIndex;
                NewIndex = newIndex;
                Line = line;
            }
        }

        /// <summary>
        /// Myers diff algorithm implementation
        /// </summary>
        private List<DiffOperation> MyersDiff(List<string> oldLines, List<string> newLines)
        {
            int n = oldLines.Count;
            int m = newLines.Count;
            int max = n + m;

            var trace = new List<int[]>();
            var operations = new List<DiffOperation>();

            // Initialize the trace for backtracking
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
                        x = trace[d - 1][k + 1 + d - 1];
                    }
                    else
                    {
                        x = trace[d - 1][k - 1 + d - 1] + 1;
                    }

                    int y = x - k;

                    // Move diagonally as far as possible
                    while (x < n && y < m && oldLines[x] == newLines[y])
                    {
                        x++;
                        y++;
                    }

                    trace[d][k + d] = x;

                    if (x >= n && y >= m)
                    {
                        // Found the end, reconstruct the path
                        return ReconstructPath(trace, d, k, oldLines, newLines);
                    }
                }
            }

            // This should never happen for valid inputs
            throw new InvalidOperationException("Diff algorithm failed to find a path");
        }

        /// <summary>
        /// Reconstruct the diff operations from the Myers algorithm trace
        /// </summary>
        private static List<DiffOperation> ReconstructPath(List<int[]> trace, int d, int k, List<string> oldLines, List<string> newLines)
        {
            var operations = new List<DiffOperation>();
            int x = oldLines.Count;
            int y = newLines.Count;

            for (int currentD = d; currentD > 0; currentD--)
            {
                int prevK = k;
                int prevX = trace[currentD - 1][k + currentD - 1];

                if (k == -currentD || (k != currentD && trace[currentD - 1][k - 1 + currentD - 1] < trace[currentD - 1][k + 1 + currentD - 1]))
                {
                    prevK = k + 1;
                }
                else
                {
                    prevK = k - 1;
                }

                int prevY = prevX - prevK;

                // Add operations for the diagonal move
                while (x > prevX && y > prevY)
                {
                    x--;
                    y--;
                    operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Equal, x, y, oldLines[x]));
                }

                if (currentD > 0)
                {
                    if (x > prevX)
                    {
                        // Delete operation
                        x--;
                        operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Delete, x, y, oldLines[x]));
                    }
                    else if (y > prevY)
                    {
                        // Insert operation
                        y--;
                        operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Insert, x, y, newLines[y]));
                    }
                }

                k = prevK;
            }

            // Add remaining equal operations at the beginning
            while (x > 0 && y > 0 && oldLines[x - 1] == newLines[y - 1])
            {
                x--;
                y--;
                operations.Insert(0, new DiffOperation(DiffOperation.OperationType.Equal, x, y, oldLines[x]));
            }

            return operations;
        }

        /// <summary>
        /// Format diff operations as unified diff output
        /// </summary>
        private List<string> FormatUnifiedDiff(List<DiffOperation> operations, List<string> oldLines, List<string> newLines, string fromFile, string toFile)
        {
            var result = new List<string>();

            if (operations.Count == 0)
            {
                return result;
            }

            // Find hunks (groups of changes with context)
            var hunks = FindHunks(operations, oldLines.Count, newLines.Count);

            if (hunks.Count == 0)
            {
                return result;
            }

            // Add diff headers
            result.Add($"--- {fromFile}");
            result.Add($"+++ {toFile}");

            foreach (var hunk in hunks)
            {
                result.AddRange(FormatHunk(hunk, operations, oldLines, newLines));
            }

            return result;
        }

        /// <summary>
        /// Represents a hunk of changes in the diff
        /// </summary>
        private class Hunk
        {
            public int OldStart { get; set; }
            public int OldCount { get; set; }
            public int NewStart { get; set; }
            public int NewCount { get; set; }
            public int StartOpIndex { get; set; }
            public int EndOpIndex { get; set; }
        }

        /// <summary>
        /// Find hunks of changes with context lines
        /// </summary>
        private static List<Hunk> FindHunks(List<DiffOperation> operations, int oldCount, int newCount)
        {
            var hunks = new List<Hunk>();
            const int CONTEXT_LINES = 3;

            int i = 0;
            while (i < operations.Count)
            {
                // Skip equal operations until we find a change
                while (i < operations.Count && operations[i].Type == DiffOperation.OperationType.Equal)
                {
                    i++;
                }

                if (i >= operations.Count)
                {
                    break;
                }

                // Found the start of a change hunk
                var hunk = new Hunk();
                hunk.StartOpIndex = Math.Max(0, i - CONTEXT_LINES);

                // Find the end of this change hunk
                int changeStart = i;
                while (i < operations.Count && operations[i].Type != DiffOperation.OperationType.Equal)
                {
                    i++;
                }
                int changeEnd = i - 1;

                hunk.EndOpIndex = Math.Min(operations.Count - 1, changeEnd + CONTEXT_LINES);

                // Calculate line numbers
                hunk.OldStart = 1;
                hunk.NewStart = 1;

                for (int j = 0; j < hunk.StartOpIndex; j++)
                {
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Delete)
                    {
                        hunk.OldStart++;
                    }
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Insert)
                    {
                        hunk.NewStart++;
                    }
                }

                // Count lines in hunk
                hunk.OldCount = 0;
                hunk.NewCount = 0;

                for (int j = hunk.StartOpIndex; j <= hunk.EndOpIndex; j++)
                {
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Delete)
                    {
                        hunk.OldCount++;
                    }
                    if (operations[j].Type == DiffOperation.OperationType.Equal || operations[j].Type == DiffOperation.OperationType.Insert)
                    {
                        hunk.NewCount++;
                    }
                }

                hunks.Add(hunk);
            }

            return hunks;
        }

        /// <summary>
        /// Format a single hunk as unified diff lines
        /// </summary>
        private static List<string> FormatHunk(Hunk hunk, List<DiffOperation> operations, List<string> oldLines, List<string> newLines)
        {
            var lines = new List<string>();

            // Add hunk header
            lines.Add($"@@ -{hunk.OldStart},{hunk.OldCount} +{hunk.NewStart},{hunk.NewCount} @@");

            // Add the lines in the hunk
            for (int i = hunk.StartOpIndex; i <= hunk.EndOpIndex; i++)
            {
                var op = operations[i];
                switch (op.Type)
                {
                    case DiffOperation.OperationType.Equal:
                        lines.Add($" {op.Line.TrimEnd('\r', '\n')}");
                        break;
                    case DiffOperation.OperationType.Delete:
                        lines.Add($"-{op.Line.TrimEnd('\r', '\n')}");
                        break;
                    case DiffOperation.OperationType.Insert:
                        lines.Add($"+{op.Line.TrimEnd('\r', '\n')}");
                        break;
                }
            }

            return lines;
        }

        private static byte[] CalculateSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                return sha256.ComputeHash(stream);
            }
        }

        /// <summary>
        /// Test method to verify the diff algorithm works correctly
        /// </summary>
        public static void TestDiffAlgorithm()
        {
            var differ = new KotorDiffer();

            // Test case 1: Simple addition
            string text1 = "line1\nline2\nline3";
            string text2 = "line1\nline2\nline3\nline4";
            var result1 = differ.GenerateUnifiedDiffLines(text1, text2, "original", "modified");
            Console.WriteLine("Test 1 - Simple addition:");
            foreach (var line in result1)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();

            // Test case 2: Simple deletion
            string text3 = "line1\nline2\nline3\nline4";
            string text4 = "line1\nline2\nline3";
            var result2 = differ.GenerateUnifiedDiffLines(text3, text4, "original", "modified");
            Console.WriteLine("Test 2 - Simple deletion:");
            foreach (var line in result2)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();

            // Test case 3: Modification
            string text5 = "line1\nline2\nline3";
            string text6 = "line1\nmodified_line2\nline3";
            var result3 = differ.GenerateUnifiedDiffLines(text5, text6, "original", "modified");
            Console.WriteLine("Test 3 - Modification:");
            foreach (var line in result3)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine();

            // Test case 4: Complex changes with context
            string text7 = "common1\ncommon2\nold_line1\nold_line2\ncommon3\ncommon4";
            string text8 = "common1\ncommon2\nnew_line1\nnew_line2\ncommon3\ncommon4";
            var result4 = differ.GenerateUnifiedDiffLines(text7, text8, "original", "modified");
            Console.WriteLine("Test 4 - Complex changes:");
            foreach (var line in result4)
            {
                Console.WriteLine(line);
            }
        }

    }
}

