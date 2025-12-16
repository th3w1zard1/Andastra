using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py
    // Original: Comprehensive tests for UTS Editor
    [Collection("Avalonia Test Collection")]
    public class UTSEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTSEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtsEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py
            // Original: def test_uts_editor_new_file_creation(qtbot, installation):
            var editor = new UTSEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:25-50
        // Original: def test_uts_editor_manipulate_name_locstring(qtbot, installation: HTInstallation, test_files_dir: Path):
        // Note: This test loads an existing UTS file and verifies it works, similar to UTI/UTC/UTP test patterns
        [Fact]
        public void TestUtsEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find low_air_01.uts (used in Python tests)
            string utsFile = System.IO.Path.Combine(testFilesDir, "low_air_01.uts");
            if (!System.IO.File.Exists(utsFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utsFile = System.IO.Path.Combine(testFilesDir, "low_air_01.uts");
            }

            if (!System.IO.File.Exists(utsFile))
            {
                // Skip if no UTS files available for testing (matching Python pytest.skip behavior)
                return;
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }
            else
            {
                // Fallback to K2
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                }
            }

            var editor = new UTSEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(utsFile);
            editor.Load(utsFile, "low_air_01", ResourceType.UTS, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            GFF gff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1053-1077
        // Original: def test_uts_editor_gff_roundtrip_comparison(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtsEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find low_air_01.uts
            string utsFile = System.IO.Path.Combine(testFilesDir, "low_air_01.uts");
            if (!System.IO.File.Exists(utsFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utsFile = System.IO.Path.Combine(testFilesDir, "low_air_01.uts");
            }

            if (!System.IO.File.Exists(utsFile))
            {
                // Skip if test file not available
                return;
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }
            else
            {
                // Fallback to K2
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                }
            }

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            var editor = new UTSEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1063
            // Original: original_data = uts_file.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(utsFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1064
            // Original: original_gff = read_gff(original_data)
            var old = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1065
            // Original: editor.load(uts_file, "low_air_01", ResourceType.UTS, original_data)
            editor.Load(utsFile, "low_air_01", ResourceType.UTS, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1068
            // Original: data, _ = editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1069
            // Original: new_gff = read_gff(data)
            GFF newGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(newData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1076
            // Original: diff = original_gff.compare(new_gff, log_func, ignore_default_changes=True)
            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py:1077
            // Original: assert diff, f"GFF comparison failed:\n{chr(10).join(log_messages)}"
            diff.Should().BeTrue("GFF comparison failed. Log messages: " + string.Join(Environment.NewLine, logMessages));
        }
    }
}
