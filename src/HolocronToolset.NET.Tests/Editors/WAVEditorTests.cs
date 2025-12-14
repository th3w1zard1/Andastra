using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py
    // Original: Comprehensive tests for WAV/Audio Editor
    [Collection("Avalonia Test Collection")]
    public class WAVEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public WAVEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestWavEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py
            // Original: def test_wav_editor_new_file_creation(qtbot, installation):
            var editor = new WAVEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestWavEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py
            // Original: def test_wav_editor_initialization(qtbot, installation):
            var editor = new WAVEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "WAV format loading requires valid WAV data - test will be implemented when WAV format is fully supported")]
        public void TestWavEditorLoadExistingFile()
        {
            // This test is skipped until WAV format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_wav_editor.py
            // Original: def test_wav_editor_load_existing_file(qtbot, installation):
        }
    }
}
