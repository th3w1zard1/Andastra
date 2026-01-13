using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.RIM;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource.Formats.TPC;
using BioWare.NET.Resource.Formats.TwoDA;

namespace BioWare.NET.Tools
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

                // For unknown file formats, validate basic file properties
                // Check that file is readable and has content
                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    return (false, "File is empty (0 bytes)");
                }

                // Read header bytes for format-specific validation
                byte[] header;
                try
                {
                    using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        int headerSize = Math.Min(64, (int)fileInfo.Length); // Read up to 64 bytes for format detection
                        header = new byte[headerSize];
                        int bytesRead = stream.Read(header, 0, headerSize);
                        if (bytesRead == 0)
                        {
                            return (false, "File is not readable or corrupted");
                        }
                    }
                }
                catch (IOException)
                {
                    return (false, "File is not readable (I/O error)");
                }
                catch (UnauthorizedAccessException)
                {
                    return (false, "File is not readable (access denied)");
                }

                // Perform format-specific validation based on file extension
                (bool isValid, string message) formatResult = ValidateFormatByHeader(suffix, header, fileInfo.Length);
                if (!formatResult.isValid)
                {
                    return formatResult;
                }

                return (true, formatResult.message);
            }
            catch (Exception e)
            {
                return (false, $"Validation failed: {e.Message}");
            }
        }

        /// <summary>
        /// Validates file format by checking header bytes/magic numbers.
        /// Implements format-specific validation for common game file formats.
        /// </summary>
        /// <param name="suffix">File extension (lowercase, including dot).</param>
        /// <param name="header">File header bytes (at least 16 bytes, up to 64 bytes if available).</param>
        /// <param name="fileSize">Total file size in bytes.</param>
        /// <returns>Tuple of (isValid, message) indicating validation result.</returns>
        /// <remarks>
        /// Format-specific validation based on file format specifications:
        /// - NCS: "NCS V1.0" header + 0x42 magic byte
        /// - MDL: Binary MDL starts with 0x00000000
        /// - WAV: "RIFF" header
        /// - KEY: "KEY " + "V1  "/"V1.1" header
        /// - BWM: "BWM V1.0" header
        /// - TEX: May contain DDS magic or TPC format
        /// - MDX: Binary format (extension-based, validated via MDL reader)
        /// </remarks>
        private static (bool isValid, string message) ValidateFormatByHeader(string suffix, byte[] header, long fileSize)
        {
            if (header == null || header.Length < 4)
            {
                return (true, $"File exists and is readable ({fileSize} bytes, insufficient header for validation)");
            }

            // Validate based on file extension
            switch (suffix)
            {
                case ".ncs":
                    // NCS format: "NCS V1.0" (8 bytes) + 0x42 magic byte at offset 8
                    if (header.Length < 13)
                    {
                        return (false, "NCS file too small (requires at least 13 bytes for header)");
                    }

                    // Check "NCS " signature (4 bytes)
                    if (header[0] != (byte)'N' || header[1] != (byte)'C' || header[2] != (byte)'S' || header[3] != (byte)' ')
                    {
                        return (false, "Invalid NCS file signature (expected 'NCS ')");
                    }

                    // Check "V1.0" version (4 bytes at offset 4)
                    if (header.Length >= 8)
                    {
                        if (header[4] != (byte)'V' || header[5] != (byte)'1' || header[6] != (byte)'.' || header[7] != (byte)'0')
                        {
                            return (false, "Invalid NCS version (expected 'V1.0')");
                        }
                    }

                    // Check magic byte 0x42 at offset 8
                    if (header.Length >= 9 && header[8] != 0x42)
                    {
                        return (false, "Invalid NCS header magic byte (expected 0x42 at offset 8)");
                    }

                    return (true, "Valid NCS file");

                case ".mdl":
                    // Binary MDL starts with 0x00000000 (4 bytes)
                    // ASCII MDL starts with text content
                    if (header[0] == 0 && header[1] == 0 && header[2] == 0 && header[3] == 0)
                    {
                        // Binary MDL - validate minimum header size
                        if (fileSize < 12)
                        {
                            return (false, "MDL file too small (binary MDL requires at least 12 bytes for header)");
                        }
                        return (true, "Valid binary MDL file");
                    }
                    else
                    {
                        // ASCII MDL - check if it starts with readable text
                        // ASCII MDL files typically start with "beginmodel" or similar keywords
                        string headerText = System.Text.Encoding.ASCII.GetString(header, 0, Math.Min(header.Length, 64)).Trim();
                        if (string.IsNullOrWhiteSpace(headerText))
                        {
                            return (false, "Invalid MDL file (neither binary nor ASCII format detected)");
                        }
                        // Allow ASCII MDL (may start with various keywords)
                        return (true, "Valid ASCII MDL file");
                    }

                case ".mdx":
                    // MDX files are paired with MDL files and have binary format
                    // MDX files don't have a standard header signature, so we validate they're not empty
                    if (fileSize == 0)
                    {
                        return (false, "MDX file is empty");
                    }
                    return (true, "Valid MDX file (format validation limited without paired MDL)");

                case ".wav":
                    // WAV files use RIFF format: "RIFF" (4 bytes) + file size + "WAVE" (4 bytes)
                    if (header.Length < 12)
                    {
                        return (false, "WAV file too small (requires at least 12 bytes for header)");
                    }

                    // Check "RIFF" signature
                    if (header[0] != (byte)'R' || header[1] != (byte)'I' || header[2] != (byte)'F' || header[3] != (byte)'F')
                    {
                        return (false, "Invalid WAV file signature (expected 'RIFF')");
                    }

                    // Check "WAVE" at offset 8 (after RIFF + size)
                    if (header.Length >= 12)
                    {
                        if (header[8] != (byte)'W' || header[9] != (byte)'A' || header[10] != (byte)'V' || header[11] != (byte)'E')
                        {
                            return (false, "Invalid WAV file format (expected 'WAVE' chunk at offset 8)");
                        }
                    }

                    return (true, "Valid WAV file");

                case ".key":
                    // KEY format: "KEY " (4 bytes) + "V1  " or "V1.1" (4 bytes)
                    if (header.Length < 8)
                    {
                        return (false, "KEY file too small (requires at least 8 bytes for header)");
                    }

                    // Check "KEY " signature
                    if (header[0] != (byte)'K' || header[1] != (byte)'E' || header[2] != (byte)'Y' || header[3] != (byte)' ')
                    {
                        return (false, "Invalid KEY file signature (expected 'KEY ')");
                    }

                    // Check version "V1  " or "V1.1"
                    if (header.Length >= 8)
                    {
                        bool validVersion = false;
                        // Check for "V1  " (V1 followed by 3 spaces)
                        if (header[4] == (byte)'V' && header[5] == (byte)'1' && header[6] == (byte)' ' && header[7] == (byte)' ')
                        {
                            validVersion = true;
                        }
                        // Check for "V1.1"
                        else if (header.Length >= 8 && header[4] == (byte)'V' && header[5] == (byte)'1' && header[6] == (byte)'.' && header[7] == (byte)'1')
                        {
                            validVersion = true;
                        }

                        if (!validVersion)
                        {
                            return (false, "Invalid KEY file version (expected 'V1  ' or 'V1.1')");
                        }
                    }

                    return (true, "Valid KEY file");

                case ".bwm":
                case ".wok":
                    // BWM/WOK format: "BWM V1.0" (8 bytes)
                    if (header.Length < 8)
                    {
                        return (false, "BWM/WOK file too small (requires at least 8 bytes for header)");
                    }

                    // Check "BWM " signature (4 bytes)
                    if (header[0] != (byte)'B' || header[1] != (byte)'W' || header[2] != (byte)'M' || header[3] != (byte)' ')
                    {
                        return (false, "Invalid BWM/WOK file signature (expected 'BWM ')");
                    }

                    // Check "V1.0" version (4 bytes at offset 4)
                    if (header[4] != (byte)'V' || header[5] != (byte)'1' || header[6] != (byte)'.' || header[7] != (byte)'0')
                    {
                        return (false, "Invalid BWM/WOK version (expected 'V1.0')");
                    }

                    return (true, "Valid BWM/WOK file");

                case ".tex":
                    // TEX files may contain DDS format or be TPC-based
                    // Check for DDS magic "DDS " (4 bytes)
                    if (header.Length >= 4)
                    {
                        if (header[0] == (byte)'D' && header[1] == (byte)'D' && header[2] == (byte)'S' && header[3] == (byte)' ')
                        {
                            return (true, "Valid TEX file (DDS format detected)");
                        }
                    }

                    // TPC-based TEX files don't have a standard header at the start
                    // If we have sufficient size, it might be a valid TEX file
                    if (fileSize >= 128)
                    {
                        // TPC header is 128 bytes, so files smaller than this might be invalid
                        // For files >= 128 bytes, we can't definitively validate without full parsing
                        return (true, "Valid TEX file (format validation limited without full parsing)");
                    }

                    return (false, "TEX file too small (minimum 128 bytes for TPC-based format)");

                case ".lyt":
                case ".vis":
                    // LYT and VIS files are text-based formats without standard headers
                    // Validate they contain readable text content
                    if (fileSize == 0)
                    {
                        return (false, $"{suffix.ToUpperInvariant()} file is empty");
                    }

                    // Check if file starts with printable ASCII characters
                    bool hasTextContent = false;
                    for (int i = 0; i < Math.Min(header.Length, 32); i++)
                    {
                        byte b = header[i];
                        if ((b >= 32 && b <= 126) || b == 9 || b == 10 || b == 13) // Printable ASCII or whitespace
                        {
                            hasTextContent = true;
                            break;
                        }
                    }

                    if (!hasTextContent)
                    {
                        return (false, $"{suffix.ToUpperInvariant()} file does not appear to contain valid text content");
                    }

                    return (true, $"Valid {suffix.ToUpperInvariant()} file");

                case ".bif":
                    // BIF format: "BIFFV1  " or "BIFFV1.1" (8 bytes)
                    if (header.Length < 8)
                    {
                        return (false, "BIF file too small (requires at least 8 bytes for header)");
                    }

                    // Check "BIFF" signature (4 bytes)
                    if (header[0] != (byte)'B' || header[1] != (byte)'I' || header[2] != (byte)'F' || header[3] != (byte)'F')
                    {
                        return (false, "Invalid BIF file signature (expected 'BIFF')");
                    }

                    // Check version "V1  " or "V1.1"
                    if (header.Length >= 8)
                    {
                        bool validVersion = false;
                        // Check for "V1  " (V1 followed by 3 spaces)
                        if (header[4] == (byte)'V' && header[5] == (byte)'1' && header[6] == (byte)' ' && header[7] == (byte)' ')
                        {
                            validVersion = true;
                        }
                        // Check for "V1.1"
                        else if (header.Length >= 8 && header[4] == (byte)'V' && header[5] == (byte)'1' && header[6] == (byte)'.' && header[7] == (byte)'1')
                        {
                            validVersion = true;
                        }

                        if (!validVersion)
                        {
                            return (false, "Invalid BIF file version (expected 'V1  ' or 'V1.1')");
                        }
                    }

                    return (true, "Valid BIF file");

                default:
                    // For unknown formats, return generic validation
                    return (true, $"File exists and is readable ({fileSize} bytes, format '{suffix}' validation not implemented)");
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

        /// <summary>
        /// Generates a unified diff between two texts using proper diff algorithm with hunk formatting.
        /// </summary>
        /// <param name="text1">First text to compare.</param>
        /// <param name="text2">Second text to compare.</param>
        /// <param name="file1Path">Path to first file (for header).</param>
        /// <param name="file2Path">Path to second file (for header).</param>
        /// <param name="contextLines">Number of context lines to include around changes.</param>
        /// <returns>Unified diff string in standard format.</returns>
        /// <remarks>
        /// Unified diff format:
        /// - Header lines: "--- file1" and "+++ file2"
        /// - Hunk headers: "@@ -start1,count1 +start2,count2 @@"
        /// - Line prefixes: ' ' (unchanged), '-' (deleted), '+' (added)
        /// - Groups changes into hunks with context lines around changes
        /// - Uses LCS (Longest Common Subsequence) algorithm to find optimal diff
        /// </remarks>
        private static string GenerateUnifiedDiff(string text1, string text2, string file1Path, string file2Path, int contextLines)
        {
            string[] lines1 = text1.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            string[] lines2 = text2.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            // Handle empty files
            if (lines1.Length == 0 && lines2.Length == 0)
            {
                return ""; // Files are identical (both empty)
            }

            // Compute diff operations using LCS algorithm
            var diffOps = ComputeDiffOperations(lines1, lines2);

            // Handle identical files
            bool hasChanges = false;
            foreach (var op in diffOps)
            {
                if (op.Type != DiffOperationType.Equal)
                {
                    hasChanges = true;
                    break;
                }
            }

            if (!hasChanges)
            {
                return ""; // Files are identical
            }

            var result = new StringBuilder();
            result.AppendLine($"--- {file1Path}");
            result.AppendLine($"+++ {file2Path}");

            // Group diff operations into hunks with context
            var hunks = GroupIntoHunks(diffOps, lines1, lines2, contextLines);

            // Output each hunk
            foreach (var hunk in hunks)
            {
                // Write hunk header: @@ -start1,count1 +start2,count2 @@
                // Line numbers are 1-based in unified diff format
                result.AppendLine($"@@ -{hunk.Start1 + 1},{hunk.Count1} +{hunk.Start2 + 1},{hunk.Count2} @@");

                // Write hunk lines
                foreach (var line in hunk.Lines)
                {
                    result.AppendLine(line);
                }
            }

            return result.ToString();
        }

        /// <summary>
        /// Diff operation type.
        /// </summary>
        private enum DiffOperationType
        {
            Equal,      // Lines are identical
            Delete,     // Line exists only in file1 (deleted)
            Insert,     // Line exists only in file2 (inserted)
            Replace     // Lines are different (replaced)
        }

        /// <summary>
        /// Represents a single diff operation.
        /// </summary>
        private struct DiffOperation
        {
            public DiffOperationType Type;
            public int Length; // Number of lines in this operation

            public DiffOperation(DiffOperationType type, int length)
            {
                Type = type;
                Length = length;
            }
        }

        /// <summary>
        /// Represents a hunk (grouped set of diff operations with context).
        /// </summary>
        private struct Hunk
        {
            public int Start1;      // Starting line index in file1 (0-based)
            public int Count1;      // Number of lines in file1 covered by this hunk
            public int Start2;      // Starting line index in file2 (0-based)
            public int Count2;      // Number of lines in file2 covered by this hunk
            public List<string> Lines; // Formatted hunk lines (with prefixes: ' ', '-', '+')

            public Hunk(int start1, int start2)
            {
                Start1 = start1;
                Start2 = start2;
                Count1 = 0;
                Count2 = 0;
                Lines = new List<string>();
            }
        }

        /// <summary>
        /// Computes diff operations using LCS (Longest Common Subsequence) algorithm.
        /// </summary>
        private static List<DiffOperation> ComputeDiffOperations(string[] lines1, string[] lines2)
        {
            var operations = new List<DiffOperation>();

            // Use dynamic programming to compute LCS and generate diff operations
            // This is a simplified Myers-like algorithm implementation
            int n = lines1.Length;
            int m = lines2.Length;

            // Build LCS table using dynamic programming
            int[,] lcs = new int[n + 1, m + 1];
            for (int i = 0; i <= n; i++)
            {
                for (int j = 0; j <= m; j++)
                {
                    if (i == 0 || j == 0)
                    {
                        lcs[i, j] = 0;
                    }
                    else if (lines1[i - 1] == lines2[j - 1])
                    {
                        lcs[i, j] = lcs[i - 1, j - 1] + 1;
                    }
                    else
                    {
                        lcs[i, j] = Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
                    }
                }
            }

            // Backtrack to generate diff operations
            int i2 = n;
            int j2 = m;
            while (i2 > 0 || j2 > 0)
            {
                if (i2 > 0 && j2 > 0 && lines1[i2 - 1] == lines2[j2 - 1])
                {
                    // Lines match - add Equal operation
                    if (operations.Count > 0 && operations[0].Type == DiffOperationType.Equal)
                    {
                        // Extend existing Equal operation
                        var lastOp = operations[0];
                        operations[0] = new DiffOperation(DiffOperationType.Equal, lastOp.Length + 1);
                    }
                    else
                    {
                        // Insert new Equal operation at front (we're building backwards)
                        operations.Insert(0, new DiffOperation(DiffOperationType.Equal, 1));
                    }
                    i2--;
                    j2--;
                }
                else if (j2 > 0 && (i2 == 0 || lcs[i2, j2 - 1] >= lcs[i2 - 1, j2]))
                {
                    // Line inserted in file2
                    if (operations.Count > 0 && operations[0].Type == DiffOperationType.Insert)
                    {
                        var lastOp = operations[0];
                        operations[0] = new DiffOperation(DiffOperationType.Insert, lastOp.Length + 1);
                    }
                    else
                    {
                        operations.Insert(0, new DiffOperation(DiffOperationType.Insert, 1));
                    }
                    j2--;
                }
                else if (i2 > 0)
                {
                    // Line deleted from file1
                    if (operations.Count > 0 && operations[0].Type == DiffOperationType.Delete)
                    {
                        var lastOp = operations[0];
                        operations[0] = new DiffOperation(DiffOperationType.Delete, lastOp.Length + 1);
                    }
                    else
                    {
                        operations.Insert(0, new DiffOperation(DiffOperationType.Delete, 1));
                    }
                    i2--;
                }
            }

            // Merge adjacent Delete+Insert operations into Replace operations
            var mergedOperations = new List<DiffOperation>();
            for (int i = 0; i < operations.Count; i++)
            {
                if (i < operations.Count - 1 &&
                    operations[i].Type == DiffOperationType.Delete &&
                    operations[i + 1].Type == DiffOperationType.Insert)
                {
                    // Merge Delete and Insert into Replace
                    int replaceLength = Math.Min(operations[i].Length, operations[i + 1].Length);
                    mergedOperations.Add(new DiffOperation(DiffOperationType.Replace, replaceLength));
                    if (operations[i].Length > replaceLength)
                    {
                        mergedOperations.Add(new DiffOperation(DiffOperationType.Delete, operations[i].Length - replaceLength));
                    }
                    if (operations[i + 1].Length > replaceLength)
                    {
                        mergedOperations.Add(new DiffOperation(DiffOperationType.Insert, operations[i + 1].Length - replaceLength));
                    }
                    i++; // Skip the Insert operation we just merged
                }
                else
                {
                    mergedOperations.Add(operations[i]);
                }
            }

            return mergedOperations;
        }

        /// <summary>
        /// Groups diff operations into hunks with context lines.
        /// </summary>
        private static List<Hunk> GroupIntoHunks(List<DiffOperation> operations, string[] lines1, string[] lines2, int contextLines)
        {
            var hunks = new List<Hunk>();
            if (operations.Count == 0)
            {
                return hunks;
            }

            // Build list of all line edits with their positions
            var lineEdits = new List<(DiffOperationType type, int line1, int line2, string lineText1, string lineText2)>();
            int line1Pos = 0;
            int line2Pos = 0;
            foreach (var op in operations)
            {
                for (int i = 0; i < op.Length; i++)
                {
                    string text1 = null;
                    string text2 = null;
                    int l1 = -1;
                    int l2 = -1;

                    switch (op.Type)
                    {
                        case DiffOperationType.Equal:
                            l1 = line1Pos;
                            l2 = line2Pos;
                            if (line1Pos < lines1.Length) text1 = lines1[line1Pos];
                            if (line2Pos < lines2.Length) text2 = lines2[line2Pos];
                            line1Pos++;
                            line2Pos++;
                            break;
                        case DiffOperationType.Delete:
                            l1 = line1Pos;
                            if (line1Pos < lines1.Length) text1 = lines1[line1Pos];
                            line1Pos++;
                            break;
                        case DiffOperationType.Insert:
                            l2 = line2Pos;
                            if (line2Pos < lines2.Length) text2 = lines2[line2Pos];
                            line2Pos++;
                            break;
                        case DiffOperationType.Replace:
                            l1 = line1Pos;
                            l2 = line2Pos;
                            if (line1Pos < lines1.Length) text1 = lines1[line1Pos];
                            if (line2Pos < lines2.Length) text2 = lines2[line2Pos];
                            line1Pos++;
                            line2Pos++;
                            break;
                    }

                    lineEdits.Add((op.Type, l1, l2, text1, text2));
                }
            }

            // Group into hunks
            int editIdx = 0;
            while (editIdx < lineEdits.Count)
            {
                // Skip to next change
                while (editIdx < lineEdits.Count && lineEdits[editIdx].type == DiffOperationType.Equal)
                {
                    editIdx++;
                }

                if (editIdx >= lineEdits.Count) break;

                // Find change region start
                int changeStart = editIdx;
                var changeStartEdit = lineEdits[changeStart];
                int hunkStart1 = changeStartEdit.line1 >= 0 ? changeStartEdit.line1 : 0;
                int hunkStart2 = changeStartEdit.line2 >= 0 ? changeStartEdit.line2 : 0;

                // Find change region end
                int changeEnd = changeStart;
                while (changeEnd < lineEdits.Count && lineEdits[changeEnd].type != DiffOperationType.Equal)
                {
                    changeEnd++;
                }

                // Calculate context before (go backwards from changeStart)
                int contextBeforeCount = 0;
                int contextStart = changeStart;
                while (contextStart > 0 && contextBeforeCount < contextLines)
                {
                    contextStart--;
                    if (lineEdits[contextStart].type == DiffOperationType.Equal)
                    {
                        contextBeforeCount++;
                        var edit = lineEdits[contextStart];
                        if (edit.line1 >= 0) hunkStart1 = edit.line1;
                        if (edit.line2 >= 0) hunkStart2 = edit.line2;
                    }
                    else
                    {
                        break;
                    }
                }

                // Calculate context after (go forwards from changeEnd)
                int contextAfterCount = 0;
                int contextEnd = changeEnd;
                while (contextEnd < lineEdits.Count && contextAfterCount < contextLines)
                {
                    if (lineEdits[contextEnd].type == DiffOperationType.Equal)
                    {
                        contextAfterCount++;
                        contextEnd++;
                    }
                    else
                    {
                        break;
                    }
                }

                // Build hunk
                var hunk = new Hunk(hunkStart1, hunkStart2);
                int hunkLine1 = hunkStart1;
                int hunkLine2 = hunkStart2;

                for (int i = contextStart; i < contextEnd; i++)
                {
                    var edit = lineEdits[i];
                    switch (edit.type)
                    {
                        case DiffOperationType.Equal:
                            hunk.Lines.Add($" {edit.lineText1 ?? ""}");
                            hunkLine1++;
                            hunkLine2++;
                            break;
                        case DiffOperationType.Delete:
                            hunk.Lines.Add($"-{edit.lineText1 ?? ""}");
                            hunkLine1++;
                            break;
                        case DiffOperationType.Insert:
                            hunk.Lines.Add($"+{edit.lineText2 ?? ""}");
                            hunkLine2++;
                            break;
                        case DiffOperationType.Replace:
                            hunk.Lines.Add($"-{edit.lineText1 ?? ""}");
                            hunk.Lines.Add($"+{edit.lineText2 ?? ""}");
                            hunkLine1++;
                            hunkLine2++;
                            break;
                    }
                }

                hunk.Count1 = hunkLine1 - hunkStart1;
                hunk.Count2 = hunkLine2 - hunkStart2;
                hunks.Add(hunk);

                editIdx = contextEnd;
            }

            return hunks;
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

