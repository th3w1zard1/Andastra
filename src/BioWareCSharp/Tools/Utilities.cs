using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BioWareCSharp.Common.Formats.ERF;
using BioWareCSharp.Common.Formats.GFF;
using BioWareCSharp.Common.Formats.RIM;
using BioWareCSharp.Common.Formats.TLK;
using BioWareCSharp.Common.Formats.TPC;
using BioWareCSharp.Common.Formats.TwoDA;

namespace BioWareCSharp.Common.Tools
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py
    // Original: Utility command functions for file operations, validation, and analysis
    public static class Utilities
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:25-63
        // Original: def diff_files(file1_path: Path, file2_path: Path, *, output_path: Path | None = None, context_lines: int = 3) -> str:
        public static string DiffFiles(string file1Path, string file2Path, string outputPath = null, int contextLines = 3)
        {
            string suffix = Path.GetExtension(file1Path).ToLowerInvariant();

            // Structured comparison for known formats
            if (suffix == ".gff" || suffix == ".utc" || suffix == ".uti" || suffix == ".dlg" || suffix == ".are" || suffix == ".git" || suffix == ".ifo")
            {
                return DiffGffFiles(file1Path, file2Path, outputPath, contextLines);
            }
            if (suffix == ".2da")
            {
                return Diff2daFiles(file1Path, file2Path, outputPath, contextLines);
            }
            if (suffix == ".tlk")
            {
                return DiffTlkFiles(file1Path, file2Path, outputPath, contextLines);
            }

            // Fallback to binary/text comparison
            return DiffBinaryFiles(file1Path, file2Path, outputPath, contextLines);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:66-100
        // Original: def _diff_gff_files(...) -> str:
        private static string DiffGffFiles(string file1Path, string file2Path, string outputPath, int contextLines)
        {
            try
            {
                byte[] data1 = File.ReadAllBytes(file1Path);
                byte[] data2 = File.ReadAllBytes(file2Path);
                var reader1 = new GFFBinaryReader(data1);
                var reader2 = new GFFBinaryReader(data2);
                GFF gff1 = reader1.Load();
                GFF gff2 = reader2.Load();

                // Use GFF's compare method for structured comparison
                string text1 = GffToText(gff1);
                string text2 = GffToText(gff2);

                // Simple unified diff (simplified version)
                string result = GenerateUnifiedDiff(text1, text2, file1Path, file2Path, contextLines);

                if (!string.IsNullOrEmpty(outputPath))
                {
                    File.WriteAllText(outputPath, result, System.Text.Encoding.UTF8);
                }

                return result;
            }
            catch
            {
                // Fallback to binary diff on error
                return DiffBinaryFiles(file1Path, file2Path, outputPath, contextLines);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:103-135
        // Original: def _diff_2da_files(...) -> str:
        private static string Diff2daFiles(string file1Path, string file2Path, string outputPath, int contextLines)
        {
            try
            {
                byte[] data1 = File.ReadAllBytes(file1Path);
                byte[] data2 = File.ReadAllBytes(file2Path);
                var reader1 = new TwoDABinaryReader(data1);
                var reader2 = new TwoDABinaryReader(data2);
                TwoDA twoda1 = reader1.Load();
                TwoDA twoda2 = reader2.Load();

                string text1 = TwoDAToText(twoda1);
                string text2 = TwoDAToText(twoda2);

                string result = GenerateUnifiedDiff(text1, text2, file1Path, file2Path, contextLines);

                if (!string.IsNullOrEmpty(outputPath))
                {
                    File.WriteAllText(outputPath, result, System.Text.Encoding.UTF8);
                }

                return result;
            }
            catch
            {
                return DiffBinaryFiles(file1Path, file2Path, outputPath, contextLines);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:138-170
        // Original: def _diff_tlk_files(...) -> str:
        private static string DiffTlkFiles(string file1Path, string file2Path, string outputPath, int contextLines)
        {
            try
            {
                byte[] data1 = File.ReadAllBytes(file1Path);
                byte[] data2 = File.ReadAllBytes(file2Path);
                var reader1 = new TLKBinaryReader(data1);
                var reader2 = new TLKBinaryReader(data2);
                TLK tlk1 = reader1.Load();
                TLK tlk2 = reader2.Load();

                string text1 = TlkToText(tlk1);
                string text2 = TlkToText(tlk2);

                string result = GenerateUnifiedDiff(text1, text2, file1Path, file2Path, contextLines);

                if (!string.IsNullOrEmpty(outputPath))
                {
                    File.WriteAllText(outputPath, result, System.Text.Encoding.UTF8);
                }

                return result;
            }
            catch
            {
                return DiffBinaryFiles(file1Path, file2Path, outputPath, contextLines);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:173-195
        // Original: def _diff_binary_files(...) -> str:
        private static string DiffBinaryFiles(string file1Path, string file2Path, string outputPath, int contextLines)
        {
            byte[] data1 = File.Exists(file1Path) ? File.ReadAllBytes(file1Path) : new byte[0];
            byte[] data2 = File.Exists(file2Path) ? File.ReadAllBytes(file2Path) : new byte[0];

            string result;
            if (data1.SequenceEqual(data2))
            {
                result = $"Files are identical: {Path.GetFileName(file1Path)} and {Path.GetFileName(file2Path)}\n";
            }
            else
            {
                result = $"Files differ:\n  {Path.GetFileName(file1Path)}: {data1.Length} bytes\n  {Path.GetFileName(file2Path)}: {data2.Length} bytes\n";
            }

            if (!string.IsNullOrEmpty(outputPath))
            {
                File.WriteAllText(outputPath, result, System.Text.Encoding.UTF8);
            }

            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:198-233
        // Original: def grep_in_file(file_path: Path, pattern: str, *, case_sensitive: bool = False) -> list[tuple[int, str]]:
        public static List<(int lineNumber, string lineText)> GrepInFile(string filePath, string pattern, bool caseSensitive = false)
        {
            string suffix = Path.GetExtension(filePath).ToLowerInvariant();

            // Handle structured formats
            if (suffix == ".gff" || suffix == ".utc" || suffix == ".uti" || suffix == ".dlg" || suffix == ".are" || suffix == ".git" || suffix == ".ifo")
            {
                return GrepInGff(filePath, pattern, caseSensitive);
            }
            if (suffix == ".2da")
            {
                return GrepIn2da(filePath, pattern, caseSensitive);
            }
            if (suffix == ".tlk")
            {
                return GrepInTlk(filePath, pattern, caseSensitive);
            }

            // Fallback to text file search
            return GrepInTextFile(filePath, pattern, caseSensitive);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:236-258
        // Original: def _grep_in_text_file(...) -> list[tuple[int, str]]:
        private static List<(int, string)> GrepInTextFile(string filePath, string pattern, bool caseSensitive)
        {
            var matches = new List<(int, string)>();
            string searchText = caseSensitive ? pattern : pattern.ToLowerInvariant();

            try
            {
                using (var reader = new StreamReader(filePath, System.Text.Encoding.UTF8, true))
                {
                    int lineNum = 1;
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string searchLine = caseSensitive ? line : line.ToLowerInvariant();
                        if (searchLine.Contains(searchText))
                        {
                            matches.Add((lineNum, line));
                        }
                        lineNum++;
                    }
                }
            }
            catch
            {
                // Try binary search
                byte[] data = File.ReadAllBytes(filePath);
                byte[] searchBytes = System.Text.Encoding.UTF8.GetBytes(caseSensitive ? pattern : pattern.ToLowerInvariant());
                if (ContainsBytes(data, searchBytes))
                {
                    matches.Add((0, $"Pattern found in binary file: {Path.GetFileName(filePath)}"));
                }
            }

            return matches;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:261-272
        // Original: def _grep_in_gff(...) -> list[tuple[int, str]]:
        private static List<(int, string)> GrepInGff(string filePath, string pattern, bool caseSensitive)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                var reader = new GFFBinaryReader(data);
                GFF gff = reader.Load();
                string text = GffToText(gff);
                return GrepInTextContent(text, pattern, caseSensitive);
            }
            catch
            {
                return new List<(int, string)>();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:275-286
        // Original: def _grep_in_2da(...) -> list[tuple[int, str]]:
        private static List<(int, string)> GrepIn2da(string filePath, string pattern, bool caseSensitive)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                var reader = new TwoDABinaryReader(data);
                TwoDA twoda = reader.Load();
                string text = TwoDAToText(twoda);
                return GrepInTextContent(text, pattern, caseSensitive);
            }
            catch
            {
                return new List<(int, string)>();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:289-300
        // Original: def _grep_in_tlk(...) -> list[tuple[int, str]]:
        private static List<(int, string)> GrepInTlk(string filePath, string pattern, bool caseSensitive)
        {
            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                var reader = new TLKBinaryReader(data);
                TLK tlk = reader.Load();
                string text = TlkToText(tlk);
                return GrepInTextContent(text, pattern, caseSensitive);
            }
            catch
            {
                return new List<(int, string)>();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:303-317
        // Original: def _grep_in_text_content(...) -> list[tuple[int, str]]:
        private static List<(int, string)> GrepInTextContent(string content, string pattern, bool caseSensitive)
        {
            var matches = new List<(int, string)>();
            string searchText = caseSensitive ? pattern : pattern.ToLowerInvariant();

            string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string searchLine = caseSensitive ? line : line.ToLowerInvariant();
                if (searchLine.Contains(searchText))
                {
                    matches.Add((i + 1, line));
                }
            }

            return matches;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:320-370
        // Original: def get_file_stats(file_path: Path) -> dict[str, int | str]:
        public static Dictionary<string, object> GetFileStats(string filePath)
        {
            var stats = new Dictionary<string, object>
            {
                ["path"] = filePath,
                ["size"] = File.Exists(filePath) ? new FileInfo(filePath).Length : 0L,
                ["exists"] = File.Exists(filePath)
            };

            if (!File.Exists(filePath))
            {
                return stats;
            }

            string suffix = Path.GetExtension(filePath).ToLowerInvariant();

            // Add format-specific statistics
            if (suffix == ".gff" || suffix == ".utc" || suffix == ".uti" || suffix == ".dlg" || suffix == ".are" || suffix == ".git" || suffix == ".ifo")
            {
                try
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    var reader = new GFFBinaryReader(data);
                    GFF gff = reader.Load();
                    stats["type"] = "GFF";
                    stats["field_count"] = gff.Root != null ? gff.Root.Count : 0;
                }
                catch
                {
                    // Ignore errors
                }
            }
            else if (suffix == ".2da")
            {
                try
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    var reader = new TwoDABinaryReader(data);
                    TwoDA twoda = reader.Load();
                    stats["type"] = "2DA";
                    stats["row_count"] = twoda != null ? twoda.Rows.Count : 0;
                    stats["column_count"] = twoda != null ? twoda.Headers.Count : 0;
                }
                catch
                {
                    // Ignore errors
                }
            }
            else if (suffix == ".tlk")
            {
                try
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    var reader = new TLKBinaryReader(data);
                    TLK tlk = reader.Load();
                    stats["type"] = "TLK";
                    stats["string_count"] = tlk != null ? tlk.Entries.Count : 0;
                }
                catch
                {
                    // Ignore errors
                }
            }

            return stats;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:373-421
        // Original: def validate_file(file_path: Path) -> tuple[bool, str]:
        public static (bool isValid, string message) ValidateFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return (false, $"File does not exist: {filePath}");
            }

            string suffix = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                if (suffix == ".gff" || suffix == ".utc" || suffix == ".uti" || suffix == ".dlg" || suffix == ".are" || suffix == ".git" || suffix == ".ifo")
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    var reader = new GFFBinaryReader(data);
                    reader.Load();
                    return (true, "Valid GFF file");
                }
                if (suffix == ".2da")
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    var reader = new TwoDABinaryReader(data);
                    reader.Load();
                    return (true, "Valid 2DA file");
                }
                if (suffix == ".tlk")
                {
                    byte[] data = File.ReadAllBytes(filePath);
                    var reader = new TLKBinaryReader(data);
                    reader.Load();
                    return (true, "Valid TLK file");
                }
                if (suffix == ".erf" || suffix == ".mod" || suffix == ".sav")
                {
                    var erf = ERFAuto.ReadErf(filePath);
                    return (true, "Valid ERF file");
                }
                if (suffix == ".rim")
                {
                    var rim = RIMAuto.ReadRim(filePath);
                    return (true, "Valid RIM file");
                }
                if (suffix == ".tpc")
                {
                    var tpc = TPCAuto.ReadTpc(filePath);
                    return (true, "Valid TPC file");
                }

                return (true, "File exists (format validation not implemented)");
            }
            catch (Exception e)
            {
                return (false, $"Validation failed: {e.Message}");
            }
        }

        // Helper functions for text conversion
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:425-429
        // Original: def _gff_to_text(gff: GFF) -> str:
        private static string GffToText(GFF gff)
        {
            var lines = new List<string>();
            if (gff.Root != null)
            {
                GffStructToText(gff.Root, lines, "");
            }
            return string.Join("\n", lines);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:432-445
        // Original: def _gff_struct_to_text(struct: GFFStruct, lines: list[str], indent: str) -> None:
        private static void GffStructToText(GFFStruct gffStruct, List<string> lines, string indent)
        {
            foreach ((string label, GFFFieldType fieldType, object value) in gffStruct)
            {
                string fieldTypeName = fieldType.ToString();
                string valueStr = value?.ToString() ?? "null";
                lines.Add($"{indent}{label} ({fieldTypeName}): {valueStr}");

                if (fieldType == GFFFieldType.Struct && value is GFFStruct nestedStruct)
                {
                    GffStructToText(nestedStruct, lines, indent + "  ");
                }
                else if (fieldType == GFFFieldType.List && value is GFFList list)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        lines.Add($"{indent}  [{i}]");
                        if (list[i] is GFFStruct listStruct)
                        {
                            GffStructToText(listStruct, lines, indent + "    ");
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:448-457
        // Original: def _2da_to_text(twoda) -> str:
        private static string TwoDAToText(TwoDA twoda)
        {
            var lines = new List<string>();
            if (twoda != null && twoda.Headers != null)
            {
                lines.Add(string.Join("\t", twoda.Headers));
                foreach (var row in twoda.Rows)
                {
                    var values = twoda.Headers.Select(header =>
                    {
                        try { return row.GetString(header) ?? ""; }
                        catch { return ""; }
                    }).ToList();
                    lines.Add(string.Join("\t", values));
                }
            }
            return string.Join("\n", lines);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/utilities.py:460-466
        // Original: def _tlk_to_text(tlk) -> str:
        private static string TlkToText(TLK tlk)
        {
            var lines = new List<string>();
            if (tlk != null && tlk.Entries != null)
            {
                for (int i = 0; i < tlk.Entries.Count; i++)
                {
                    var entry = tlk.Entries[i];
                    string text = entry.Text ?? "";
                    lines.Add($"{i}: {text}");
                }
            }
            return string.Join("\n", lines);
        }

        // Simple unified diff generator (simplified version)
        private static string GenerateUnifiedDiff(string text1, string text2, string file1Path, string file2Path, int contextLines)
        {
            string[] lines1 = text1.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            string[] lines2 = text2.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            var result = new StringBuilder();
            result.AppendLine($"--- {file1Path}");
            result.AppendLine($"+++ {file2Path}");

            // Simple line-by-line comparison (full diff algorithm would be more complex)
            int maxLen = Math.Max(lines1.Length, lines2.Length);
            for (int i = 0; i < maxLen; i++)
            {
                string line1 = i < lines1.Length ? lines1[i] : null;
                string line2 = i < lines2.Length ? lines2[i] : null;

                if (line1 == line2)
                {
                    result.AppendLine($" {line1 ?? ""}");
                }
                else
                {
                    if (line1 != null)
                    {
                        result.AppendLine($"-{line1}");
                    }
                    if (line2 != null)
                    {
                        result.AppendLine($"+{line2}");
                    }
                }
            }

            return result.ToString();
        }

        private static bool ContainsBytes(byte[] data, byte[] pattern)
        {
            if (pattern.Length == 0) return true;
            if (pattern.Length > data.Length) return false;

            for (int i = 0; i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return true;
            }
            return false;
        }
    }
}

