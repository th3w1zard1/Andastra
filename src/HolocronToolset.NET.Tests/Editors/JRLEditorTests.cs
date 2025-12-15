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
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_jrl_editor.py
    // Original: Comprehensive tests for JRL Editor
    [Collection("Avalonia Test Collection")]
    public class JRLEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public JRLEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestJrlEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_jrl_editor.py
            // Original: def test_jrl_editor_new_file_creation(qtbot, installation):
            var editor = new JRLEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestJrlEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_jrl_editor.py
            // Original: def test_jrl_editor_initialization(qtbot, installation):
            var editor = new JRLEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_jrl_editor.py:201-225
        // Original: def test_jrl_editor_headless_ui_load_build(qtbot: QtBot, installation: HTInstallation, test_files_dir: pathlib.Path):
        [Fact]
        public void TestJrlEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a JRL file
            string jrlFile = System.IO.Path.Combine(testFilesDir, "global.jrl");
            if (!System.IO.File.Exists(jrlFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                jrlFile = System.IO.Path.Combine(testFilesDir, "global.jrl");
            }

            if (!System.IO.File.Exists(jrlFile))
            {
                // Skip if no JRL files available for testing (matching Python pytest.skip behavior)
                // In Xunit, we can't skip mid-test, so we'll just skip the assertions
                // The test will still pass but won't test anything
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

            var editor = new JRLEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(jrlFile);
            editor.Load(jrlFile, "global", ResourceType.JRL, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();
            editor.ModelRowCount.Should().BeGreaterThan(0);

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            GFF gff = GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_jrl_editor.py:94-105
        // Original: def test_save_and_load(self):
        [Fact]
        public void TestJrlEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string jrlFile = System.IO.Path.Combine(testFilesDir, "global.jrl");
            if (!System.IO.File.Exists(jrlFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                jrlFile = System.IO.Path.Combine(testFilesDir, "global.jrl");
            }

            if (!System.IO.File.Exists(jrlFile))
            {
                // Skip if test file not available (matching Python pytest.skip behavior)
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

            var editor = new JRLEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            byte[] data = System.IO.File.ReadAllBytes(jrlFile);
            var old = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
            editor.Load(jrlFile, "global", ResourceType.JRL, data);
            var (newData, _) = editor.Build();
            GFF newGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(newData);

            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: false);
            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }
    }
}
