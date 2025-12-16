using System;
using System.Text;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_tlk_editor.py
    // Original: Comprehensive tests for TLK Editor
    [Collection("Avalonia Test Collection")]
    public class TLKEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public TLKEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestTlkEditorNewFileCreation()
        {
            var editor = new TLKEditor(null, null);
            editor.New();

            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_tlk_editor.py:382-418
        // Original: def test_tlk_editor_load_real_file(qtbot: QtBot, installation: HTInstallation, test_files_dir):
        [Fact]
        public void TestTlkEditorLoadExistingFile()
        {
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

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string tlkFile = System.IO.Path.Combine(testFilesDir, "dialog.tlk");
            if (!System.IO.File.Exists(tlkFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                tlkFile = System.IO.Path.Combine(testFilesDir, "dialog.tlk");
            }

            if (!System.IO.File.Exists(tlkFile))
            {
                return; // Skip if test file not available
            }

            var editor = new TLKEditor(null, installation);

            // Read file data once and verify it's valid
            byte[] fileData = System.IO.File.ReadAllBytes(tlkFile);
            fileData.Length.Should().BeGreaterThan(0, "TLK file should not be empty");

            // Verify we can read it before loading in editor
            var testTlk = AuroraEngine.Common.Formats.TLK.TLKAuto.ReadTlk(fileData);
            testTlk.Count.Should().BeGreaterThan(0, "TLK should have entries");

            // Load in editor
            editor.Load(tlkFile, "dialog", ResourceType.TLK, fileData);

            // Verify entries loaded - access via reflection since _sourceEntries is private
            var sourceEntriesField = typeof(TLKEditor).GetField("_sourceEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            sourceEntriesField.Should().NotBeNull("_sourceEntries field should exist");
            var sourceEntries = sourceEntriesField.GetValue(editor);
            sourceEntries.Should().NotBeNull("Source entries collection should not be null");
            
            // Get Count property via reflection since we don't know the exact generic type
            var countProperty = sourceEntries.GetType().GetProperty("Count");
            countProperty.Should().NotBeNull("Source entries should have Count property");
            int entryCount = (int)countProperty.GetValue(sourceEntries);
            entryCount.Should().BeGreaterThan(0, "Editor should have loaded entries");
            entryCount.Should().Be(testTlk.Count, $"Editor should have {testTlk.Count} entries, got {entryCount}");

            // Verify the editor can build the file
            var (data, _) = editor.Build();
            data.Should().NotBeNull("Build should return data");
            data.Length.Should().BeGreaterThan(0, "Build should return non-empty data");

            // Verify the built TLK can be read back
            var builtTlk = AuroraEngine.Common.Formats.TLK.TLKAuto.ReadTlk(data);
            builtTlk.Should().NotBeNull("Built TLK should not be null");
            builtTlk.Count.Should().Be(testTlk.Count, "Built TLK should have same entry count");
        }
    }
}
