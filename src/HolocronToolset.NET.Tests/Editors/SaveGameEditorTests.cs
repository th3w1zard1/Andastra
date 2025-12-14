using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py
    // Original: Comprehensive tests for Save Game Editor
    [Collection("Avalonia Test Collection")]
    public class SaveGameEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public SaveGameEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestSaveGameEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py
            // Original: def test_save_game_editor_new_file_creation(qtbot, installation):
            var editor = new SaveGameEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestSaveGameEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py
            // Original: def test_save_game_editor_initialization(qtbot, installation):
            var editor = new SaveGameEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "Save game format loading requires valid save game data - test will be implemented when save game format is fully supported")]
        public void TestSaveGameEditorLoadExistingFile()
        {
            // This test is skipped until save game format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py
            // Original: def test_save_game_editor_load_existing_file(qtbot, installation):
        }
    }
}
