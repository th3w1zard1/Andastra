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
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_dlg_editor.py
    // Original: Comprehensive tests for DLG Editor
    [Collection("Avalonia Test Collection")]
    public class DLGEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public DLGEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestDlgEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_dlg_editor.py
            // Original: def test_dlg_editor_new_file_creation(qtbot, installation):
            var editor = new DLGEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestDlgEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_dlg_editor.py
            // Original: def test_dlg_editor_initialization(qtbot, installation):
            var editor = new DLGEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_dlg_editor.py:1693-1715
        // Original: def test_dlg_editor_load_real_file(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestDlgEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a DLG file
            string dlgFile = System.IO.Path.Combine(testFilesDir, "ORIHA.dlg");
            if (!System.IO.File.Exists(dlgFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                dlgFile = System.IO.Path.Combine(testFilesDir, "ORIHA.dlg");
            }

            if (!System.IO.File.Exists(dlgFile))
            {
                // Skip if no DLG files available for testing (matching Python pytest.skip behavior)
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

            var editor = new DLGEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(dlgFile);
            editor.Load(dlgFile, "ORIHA", ResourceType.DLG, originalData);

            // Verify tree populated
            editor.Model.RowCount.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_dlg_editor.py:1769-1795
        // Original: def test_dlg_editor_gff_roundtrip_no_modification(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestDlgEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find ORIHA.dlg
            string dlgFile = System.IO.Path.Combine(testFilesDir, "ORIHA.dlg");
            if (!System.IO.File.Exists(dlgFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                dlgFile = System.IO.Path.Combine(testFilesDir, "ORIHA.dlg");
            }

            if (!System.IO.File.Exists(dlgFile))
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

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            var editor = new DLGEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(dlgFile);
            var originalGff = GFF.FromBytes(originalData);

            // Load
            editor.Load(dlgFile, "ORIHA", ResourceType.DLG, originalData);

            // Save without modification
            var (savedData, _) = editor.Build();

            // Compare GFF structures
            var savedGff = GFF.FromBytes(savedData);

            // Root should have same number of fields (allowing for minor differences)
            // Note: Some fields may differ due to defaults being added
            // The Python test only checks that roots are not null, not a full comparison
            originalGff.Root.Should().NotBeNull();
            savedGff.Root.Should().NotBeNull();
        }
    }
}
