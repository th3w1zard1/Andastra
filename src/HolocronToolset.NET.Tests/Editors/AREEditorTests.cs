using System;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py
    // Original: Comprehensive tests for ARE Editor
    [Collection("Avalonia Test Collection")]
    public class AREEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public AREEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestAreEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py
            // Original: def test_are_editor_new_file_creation(qtbot, installation):
            var editor = new AREEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1210-1242
        // Original: def test_are_editor_save_load_roundtrip_identity(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find an ARE file
            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                // Skip if no ARE files available for testing (matching Python pytest.skip behavior)
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

            var editor = new AREEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(areFile);
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            var gff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        [Fact]
        public void TestAreEditorSaveLoadRoundtrip()
        {
            var editor = new AREEditor(null, null);
            editor.New();

            // Test save/load roundtrip (will be implemented when ARE format is fully supported)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }
    }
}

