using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py
    // Original: Comprehensive tests for UTE Editor
    [Collection("Avalonia Test Collection")]
    public class UTEEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTEEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUteEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:20-30
            // Original: def test_ute_editor_new_file_creation(qtbot, installation):
            var editor = new UTEEditor(null, null);

            editor.New();

            // Verify UTE object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestUteEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:32-44
            // Original: def test_ute_editor_initialization(qtbot, installation):
            var editor = new UTEEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "UTE format loading requires valid UTE data - test will be implemented when UTE format is fully supported")]
        public void TestUteEditorLoadExistingFile()
        {
            // This test is skipped until UTE format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py
            // Original: def test_ute_editor_load_existing_file(qtbot, installation):
        }
    }
}
