// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:61-257
// Original: class TestDataHelper: ...
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using static CSharpKOTOR.Formats.GFF.GFFAuto;
using CSharpKOTOR.Formats.SSF;
using static CSharpKOTOR.Formats.SSF.SSFAuto;
using CSharpKOTOR.Formats.TwoDA;
using static CSharpKOTOR.Formats.TwoDA.TwoDAAuto;
using CSharpKOTOR.Formats.TLK;
using static CSharpKOTOR.Formats.TLK.TLKAuto;
using CSharpKOTOR.Resources;
using KotorDiff.NET.App;

namespace KotorDiff.NET.Tests.Helpers
{
    /// <summary>
    /// Helper class for creating test data files and directories.
    /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:61-180
    /// </summary>
    public static class TestDataHelper
    {
        /// <summary>
        /// Create a complete test environment with temp directories.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:65-80
        /// </summary>
        public static (DirectoryInfo tempDir, DirectoryInfo vanillaDir, DirectoryInfo moddedDir, DirectoryInfo tslpatchdataDir) CreateTestEnv()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var tempDir = Directory.CreateDirectory(tempPath);
            var vanillaDir = tempDir.CreateSubdirectory("vanilla");
            var moddedDir = tempDir.CreateSubdirectory("modded");
            var tslpatchdataDir = tempDir.CreateSubdirectory("tslpatchdata");

            return (tempDir, vanillaDir, moddedDir, tslpatchdataDir);
        }

        /// <summary>
        /// Clean up test environment.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:83-86
        /// </summary>
        public static void CleanupTestEnv(DirectoryInfo tempDir)
        {
            if (tempDir != null && tempDir.Exists)
            {
                try
                {
                    tempDir.Delete(recursive: true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Create a basic 2DA file with headers and rows.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:89-102
        /// </summary>
        public static TwoDA CreateBasic2DA(List<string> headers, List<(string label, Dictionary<string, string> cells)> rows)
        {
            var twoda = new TwoDA(headers);
            foreach (var (label, cells) in rows)
            {
                var cellsObj = new Dictionary<string, object>();
                foreach (var kvp in cells)
                {
                    cellsObj[kvp.Key] = kvp.Value;
                }
                twoda.AddRow(label, cellsObj);
            }
            return twoda;
        }

        /// <summary>
        /// Create a basic GFF file with root-level fields.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:105-148
        /// </summary>
        public static GFF CreateBasicGFF(Dictionary<string, (GFFFieldType fieldType, object value)> fields)
        {
            var gff = new GFF();
            foreach (var kvp in fields)
            {
                string fieldName = kvp.Key;
                var (fieldType, value) = kvp.Value;

                switch (fieldType)
                {
                    case GFFFieldType.UInt8:
                        gff.Root.SetUInt8(fieldName, (byte)value);
                        break;
                    case GFFFieldType.Int8:
                        gff.Root.SetInt8(fieldName, (sbyte)value);
                        break;
                    case GFFFieldType.UInt16:
                        gff.Root.SetUInt16(fieldName, (ushort)value);
                        break;
                    case GFFFieldType.Int16:
                        gff.Root.SetInt16(fieldName, (short)value);
                        break;
                    case GFFFieldType.UInt32:
                        gff.Root.SetUInt32(fieldName, (uint)value);
                        break;
                    case GFFFieldType.Int32:
                        gff.Root.SetInt32(fieldName, (int)value);
                        break;
                    case GFFFieldType.Int64:
                        gff.Root.SetInt64(fieldName, (long)value);
                        break;
                    case GFFFieldType.Single:
                        gff.Root.SetSingle(fieldName, (float)value);
                        break;
                    case GFFFieldType.Double:
                        gff.Root.SetDouble(fieldName, (double)value);
                        break;
                    case GFFFieldType.String:
                        gff.Root.SetString(fieldName, (string)value);
                        break;
                    case GFFFieldType.ResRef:
                        gff.Root.SetResRef(fieldName, (ResRef)value);
                        break;
                    case GFFFieldType.LocalizedString:
                        gff.Root.SetLocString(fieldName, (LocalizedString)value);
                        break;
                    case GFFFieldType.Vector3:
                        gff.Root.SetVector3(fieldName, (Vector3)value);
                        break;
                    case GFFFieldType.Vector4:
                        gff.Root.SetVector4(fieldName, (Vector4)value);
                        break;
                    case GFFFieldType.Struct:
                        gff.Root.SetStruct(fieldName, (GFFStruct)value);
                        break;
                    case GFFFieldType.List:
                        gff.Root.SetList(fieldName, (GFFList)value);
                        break;
                }
            }
            return gff;
        }

        /// <summary>
        /// Create a basic TLK file with entries.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:151-164
        /// </summary>
        public static TLK CreateBasicTLK(List<(string text, string soundResRef)> entries)
        {
            var tlk = new TLK();
            tlk.Resize(entries.Count);
            for (int idx = 0; idx < entries.Count; idx++)
            {
                var (text, sound) = entries[idx];
                tlk.Replace(idx, text, sound);
            }
            return tlk;
        }

        /// <summary>
        /// Create a basic SSF file with sound mappings.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:167-179
        /// </summary>
        public static SSF CreateBasicSSF(Dictionary<SSFSound, int> soundMappings)
        {
            var ssf = new SSF();
            foreach (var kvp in soundMappings)
            {
                ssf.SetData(kvp.Key, kvp.Value);
            }
            return ssf;
        }

        /// <summary>
        /// Run the diff engine and return generated INI content.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:182-212
        /// </summary>
        public static string RunDiff(
            DirectoryInfo vanillaDir,
            DirectoryInfo moddedDir,
            DirectoryInfo tslpatchdataDir,
            string iniFilename = "changes.ini",
            bool loggingEnabled = false)
        {
            var config = new KotorDiffConfig
            {
                Paths = new List<object> { vanillaDir.FullName, moddedDir.FullName },
                TslPatchDataPath = tslpatchdataDir,
                IniFilename = iniFilename,
                CompareHashes = true,
                LoggingEnabled = loggingEnabled,
                UseIncrementalWriter = true
            };

            var result = KotorDiff.NET.App.DiffApplicationHelpers.HandleDiff(config);
            int exitCode = KotorDiff.NET.App.DiffApplicationHelpers.FormatComparisonOutput(result.comparison, config);

            var iniPath = Path.Combine(tslpatchdataDir.FullName, iniFilename);
            if (File.Exists(iniPath))
            {
                return File.ReadAllText(iniPath, Encoding.UTF8);
            }
            return string.Empty;
        }

        /// <summary>
        /// Assert that a section exists in INI.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:215-217
        /// </summary>
        public static void AssertIniSectionExists(string iniContent, string sectionName, Xunit.Assert assert)
        {
            string sectionHeader = $"[{sectionName}]";
            Xunit.Assert.Contains(sectionHeader, iniContent);
        }

        /// <summary>
        /// Assert that a key exists in a section, optionally check value.
        /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:220-256
        /// </summary>
        public static void AssertIniKeyValue(
            string iniContent,
            string sectionName,
            string key,
            string expectedValue = null)
        {
            string[] lines = iniContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            bool inSection = false;
            bool foundKey = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed == $"[{sectionName}]")
                {
                    inSection = true;
                    continue;
                }
                if (inSection)
                {
                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        // Entered a new section
                        break;
                    }
                    if (trimmed.Contains("="))
                    {
                        string[] parts = trimmed.Split(new[] { '=' }, 2);
                        string lineKey = parts[0].Trim();
                        if (lineKey == key)
                        {
                            foundKey = true;
                            if (expectedValue != null)
                            {
                                string lineValue = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                                Xunit.Assert.Equal(expectedValue, lineValue);
                            }
                            break;
                        }
                    }
                }
            }

            Xunit.Assert.True(foundKey, $"Key '{key}' should exist in section [{sectionName}]");
        }
    }
}

