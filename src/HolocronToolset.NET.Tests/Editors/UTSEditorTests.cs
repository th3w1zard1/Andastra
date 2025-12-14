using System;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
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

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtsEditorLoadExistingFile()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py
            // Original: def test_uts_editor_load_existing_file(qtbot, installation, test_files_dir):
            // This test requires actual UTS test files - skipping for now
            var editor = new UTSEditor(null, null);
            editor.Should().NotBeNull();
        }

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtsEditorSaveLoadRoundtrip()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uts_editor.py
            // Original: def test_uts_editor_save_load_roundtrip(qtbot, installation, test_files_dir):
            // This test requires actual UTS test files - skipping for now
            var editor = new UTSEditor(null, null);
            editor.Should().NotBeNull();
        }
    }
}
