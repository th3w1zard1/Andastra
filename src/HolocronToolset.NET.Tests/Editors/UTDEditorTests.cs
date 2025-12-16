using System;
using System.Collections.Generic;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Resource.Generics;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;
using GFFAuto = AuroraEngine.Common.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
    // Original: Comprehensive tests for UTD Editor
    [Collection("Avalonia Test Collection")]
    public class UTDEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public UTDEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static UTDEditorTests()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            if (!string.IsNullOrEmpty(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                _installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }
            else
            {
                // Fallback to K1
                string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
                if (string.IsNullOrEmpty(k1Path))
                {
                    k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
                }

                if (!string.IsNullOrEmpty(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
                {
                    _installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
                }
            }
        }

        private static (string utdFile, HTInstallation installation) GetTestFileAndInstallation()
        {
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            if (!System.IO.File.Exists(utdFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            }

            return (utdFile, _installation);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:57-81
        // Original: def test_utd_editor_manipulate_tag(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateTag()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTDEditor(None, installation)
            var editor = new UTDEditor(null, installation);

            // Matching PyKotor implementation: original_data = utd_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);

            // Matching PyKotor implementation: editor.load(utd_file, "naldoor001", ResourceType.UTD, original_data)
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Matching PyKotor implementation: original_utd = read_utd(original_data)
            UTD originalUtd = UTDHelpers.ConstructUtd(AuroraEngine.Common.Formats.GFF.GFF.FromBytes(originalData));

            // Modify tag
            // Matching PyKotor implementation: editor.ui.tagEdit.setText("modified_tag")
            editor.TagEdit.Text = "modified_tag";

            // Save and verify
            // Matching PyKotor implementation: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching PyKotor implementation: modified_utd = read_utd(data)
            UTD modifiedUtd = UTDHelpers.ConstructUtd(AuroraEngine.Common.Formats.GFF.GFF.FromBytes(data));

            // Matching PyKotor implementation: assert modified_utd.tag == "modified_tag"
            // Matching PyKotor implementation: assert modified_utd.tag != original_utd.tag
            modifiedUtd.Tag.Should().Be("modified_tag");
            modifiedUtd.Tag.Should().NotBe(originalUtd.Tag);

            // Load back and verify
            // Matching PyKotor implementation: editor.load(utd_file, "naldoor001", ResourceType.UTD, data)
            // Matching PyKotor implementation: assert editor.ui.tagEdit.text() == "modified_tag"
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
            editor.TagEdit.Text.Should().Be("modified_tag");
        }

        [Fact]
        public void TestUtdEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
            // Original: def test_utd_editor_new_file_creation(qtbot, installation):
            var editor = new UTDEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:30-55
        // Original: def test_utd_editor_manipulate_name_locstring(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        // Note: This test loads an existing UTD file and verifies it works, similar to UTI/UTC/UTP/UTS test patterns
        [Fact]
        public void TestUtdEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find naldoor001.utd (used in Python tests)
            string utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            if (!System.IO.File.Exists(utdFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            }

            if (!System.IO.File.Exists(utdFile))
            {
                // Skip if no UTD files available for testing (matching Python pytest.skip behavior)
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

            var editor = new UTDEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            GFF gff = AuroraEngine.Common.Formats.GFF.GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1130-1154
        // Original: def test_utd_editor_gff_roundtrip_comparison(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find naldoor001.utd
            string utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            if (!System.IO.File.Exists(utdFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            }

            if (!System.IO.File.Exists(utdFile))
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

            var editor = new UTDEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1140
            // Original: original_data = utd_file.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(utdFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1141
            // Original: original_gff = read_gff(original_data)
            var old = AuroraEngine.Common.Formats.GFF.GFF.FromBytes(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1142
            // Original: editor.load(utd_file, "naldoor001", ResourceType.UTD, original_data)
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1145
            // Original: data, _ = editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1146
            // Original: new_gff = read_gff(data)
            GFF newGff = AuroraEngine.Common.Formats.GFF.GFF.FromBytes(newData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1153
            // Original: diff = original_gff.compare(new_gff, log_func, ignore_default_changes=True)
            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1154
            // Original: assert diff, f"GFF comparison failed:\n{chr(10).join(log_messages)}"
            diff.Should().BeTrue("GFF comparison failed. Log messages: " + string.Join(Environment.NewLine, logMessages));
        }
    }
}
