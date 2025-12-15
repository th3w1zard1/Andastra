using System;
using System.Collections.Generic;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py
    // Original: Comprehensive tests for UTW Editor
    [Collection("Avalonia Test Collection")]
    public class UTWEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTWEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtwEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:20-30
            // Original: def test_utw_editor_new_file_creation(qtbot, installation):
            var editor = new UTWEditor(null, null);

            editor.New();

            // Verify UTW object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestUtwEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:32-44
            // Original: def test_utw_editor_initialization(qtbot, installation):
            var editor = new UTWEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:258-287
        // Original: def test_utw_editor_save_load_roundtrip_identity(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorSaveLoadRoundtrip()
        {
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            var editor = new UTWEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            byte[] data = System.IO.File.ReadAllBytes(utwFile);
            var oldGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, data);

            var (newData, _) = editor.Build();

            GFF newGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(newData);

            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = oldGff.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }
    }
}
