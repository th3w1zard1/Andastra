using System;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py
    // Original: Comprehensive tests for UTC Editor
    [Collection("Avalonia Test Collection")]
    public class UTCEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTCEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtcEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py
            // Original: def test_utc_editor_new_file_creation(qtbot, installation):
            var editor = new UTCEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtcEditorLoadExistingFile()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py
            // Original: def test_utc_editor_load_existing_file(qtbot, installation, test_files_dir):
            // This test requires actual UTC test files - skipping for now
            var editor = new UTCEditor(null, null);
            editor.Should().NotBeNull();
        }

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtcEditorSaveLoadRoundtrip()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py
            // Original: def test_utc_editor_save_load_roundtrip(qtbot, installation, test_files_dir):
            // This test requires actual UTC test files - skipping for now
            var editor = new UTCEditor(null, null);
            editor.Should().NotBeNull();
        }
    }
}
