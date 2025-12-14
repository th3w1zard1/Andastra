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
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py
    // Original: Comprehensive tests for UTP Editor
    [Collection("Avalonia Test Collection")]
    public class UTPEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTPEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtpEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py
            // Original: def test_utp_editor_new_file_creation(qtbot, installation):
            var editor = new UTPEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtpEditorLoadExistingFile()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py
            // Original: def test_utp_editor_load_existing_file(qtbot, installation, test_files_dir):
            // This test requires actual UTP test files - skipping for now
            var editor = new UTPEditor(null, null);
            editor.Should().NotBeNull();
        }

        [Fact(Skip = "Requires valid GFF data - will be enabled when test files are available")]
        public void TestUtpEditorSaveLoadRoundtrip()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py
            // Original: def test_utp_editor_save_load_roundtrip(qtbot, installation, test_files_dir):
            // This test requires actual UTP test files - skipping for now
            var editor = new UTPEditor(null, null);
            editor.Should().NotBeNull();
        }
    }
}
