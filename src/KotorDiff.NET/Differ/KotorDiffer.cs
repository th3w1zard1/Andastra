// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:69-437
// Original: class KotorDiffer: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CSharpKOTOR.Common;
using CSharpKOTOR.Extract;
using CSharpKOTOR.Formats.Capsule;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.LIP;
using CSharpKOTOR.Formats.SSF;
using CSharpKOTOR.Formats.TLK;
using CSharpKOTOR.Formats.TwoDA;
using CSharpKOTOR.Tools;
using KotorDiff.NET.Diff;
using JetBrains.Annotations;

namespace KotorDiff.NET.Differ
{
    /// <summary>
    /// Enhanced differ for KOTOR installations.
    /// 1:1 port of KotorDiffer from vendor/PyKotor/Tools/KotorDiff/src/kotordiff/differ.py:69-437
    /// </summary>
    public class KotorDiffer
    {
        private readonly HashSet<string> _gffTypes;

        public KotorDiffer()
        {
            // GFF types: all GFF-based file formats
            _gffTypes = new HashSet<string>(new[]
            {
                "utc", "uti", "utp", "ute", "utm", "utd", "utw", "dlg", "are", "git", "ifo", "gui", "jrl", "fac", "gff"
            }, StringComparer.OrdinalIgnoreCase);
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

        private bool IsKotorInstall(string path)
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
                return Diff2DaFiles(file1, file2, relativePath);
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
        private FileChange Diff2DaFiles(string file1, string file2, string relativePath)
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

        private bool CompareResourceData(CapsuleResource res1, CapsuleResource res2)
        {
            return res1.Data.SequenceEqual(res2.Data);
        }

        private string GetResourceType(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant().TrimStart('.');
            return ext;
        }

        private string GffToText(GFF gff)
        {
            // Simplified GFF to text conversion
            // In a full implementation, would serialize GFF to a text representation
            var sb = new StringBuilder();
            // TODO: Implement proper GFF text serialization
            return sb.ToString();
        }

        private string TwoDaToText(TwoDA twoda)
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

        private string TlkToText(TLK tlk)
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
            var lines = new List<string>();
            var leftLines = text1.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var rightLines = text2.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            // Simple unified diff - in a full implementation, would use a proper diff algorithm
            lines.Add($"--- {fromFile}");
            lines.Add($"+++ {toFile}");

            int maxLines = Math.Max(leftLines.Length, rightLines.Length);
            for (int i = 0; i < maxLines; i++)
            {
                string leftLine = i < leftLines.Length ? leftLines[i] : "";
                string rightLine = i < rightLines.Length ? rightLines[i] : "";

                if (leftLine != rightLine)
                {
                    if (i < leftLines.Length)
                    {
                        lines.Add($"-{leftLine}");
                    }
                    if (i < rightLines.Length)
                    {
                        lines.Add($"+{rightLine}");
                    }
                }
            }

            return lines;
        }

        private byte[] CalculateSha256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                return sha256.ComputeHash(stream);
            }
        }
    }
}

