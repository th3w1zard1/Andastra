using System;
using System.Text;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_erf_editor.py
    // Original: Comprehensive tests for ERF Editor
    [Collection("Avalonia Test Collection")]
    public class ERFEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public ERFEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestErfEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_erf_editor.py:21
            // Original: def test_erf_editor_new_file_creation(qtbot, installation):
            var editor = new ERFEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestErfEditorLoadExistingFile()
        {
            var editor = new ERFEditor(null, null);

            // Create minimal ERF data (simplified for testing)
            byte[] testData = new byte[0]; // Will be implemented when ERF format is fully supported

            editor.Load("test.erf", "test", ResourceType.ERF, testData);

            // Verify content loaded (will be implemented when UI is complete)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_erf_editor.py:93-103
        // Original: def test_save_and_load(self):
        [Fact]
        public void TestErfEditorSaveLoadRoundtrip()
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

            string erfFile = System.IO.Path.Combine(testFilesDir, "001EBO_dlg.erf");
            if (!System.IO.File.Exists(erfFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                erfFile = System.IO.Path.Combine(testFilesDir, "001EBO_dlg.erf");
            }

            if (!System.IO.File.Exists(erfFile))
            {
                return; // Skip if test file not available
            }

            var editor = new ERFEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(erfFile);
            var oldErf = CSharpKOTOR.Formats.ERF.ERFAuto.ReadErf(originalData);

            editor.Load(erfFile, "001EBO_dlg", ResourceType.ERF, originalData);

            var (newData, _) = editor.Build();
            var newErf = CSharpKOTOR.Formats.ERF.ERFAuto.ReadErf(newData);

            // Compare ERF files - check resource count
            oldErf.Count.Should().Be(newErf.Count, "Resource count should match");

            // Compare each resource
            for (int i = 0; i < oldErf.Count; i++)
            {
                var oldResource = oldErf[i];
                var newResource = newErf[i];

                oldResource.ResRef.ToString().Should().Be(newResource.ResRef.ToString(), $"Resource {i} ResRef should match");
                oldResource.ResType.Should().Be(newResource.ResType, $"Resource {i} ResType should match");
                oldResource.Data.Should().Equal(newResource.Data, $"Resource {i} Data should match");
            }
        }
    }
}
