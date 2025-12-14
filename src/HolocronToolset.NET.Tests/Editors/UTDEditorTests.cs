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
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
    // Original: Comprehensive tests for UTD Editor
    [Collection("Avalonia Test Collection")]
    public class UTDEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTDEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
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

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtdEditorLoadExistingFile()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
            // Original: def test_utd_editor_load_existing_file(qtbot, installation, test_files_dir):
            // This test requires actual UTD test files - skipping for now
            var editor = new UTDEditor(null, null);
            editor.Should().NotBeNull();
        }

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtdEditorSaveLoadRoundtrip()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
            // Original: def test_utd_editor_save_load_roundtrip(qtbot, installation, test_files_dir):
            // This test requires actual UTD test files - skipping for now
            var editor = new UTDEditor(null, null);
            editor.Should().NotBeNull();
        }
    }
}
