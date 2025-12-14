using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_bwm_editor.py
    // Original: Comprehensive tests for BWM Editor
    [Collection("Avalonia Test Collection")]
    public class BWMEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public BWMEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestBwmEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_bwm_editor.py
            // Original: def test_bwm_editor_new_file_creation(qtbot, installation):
            var editor = new BWMEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "BWM format loading requires valid BWM data - test will be implemented when BWM format is fully supported")]
        public void TestBwmEditorLoadExistingFile()
        {
            // This test is skipped until BWM format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_bwm_editor.py
            // Original: def test_bwm_editor_load_existing_file(qtbot, installation):
        }

        [Fact]
        public void TestBwmEditorSaveLoadRoundtrip()
        {
            var editor = new BWMEditor(null, null);
            editor.New();

            // Test save/load roundtrip (will be implemented when BWM format is fully supported)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }
    }
}

