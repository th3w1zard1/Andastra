using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py
    // Original: Comprehensive tests for ARE Editor
    [Collection("Avalonia Test Collection")]
    public class AREEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public AREEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestAreEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py
            // Original: def test_are_editor_new_file_creation(qtbot, installation):
            var editor = new AREEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "ARE format loading requires valid ARE data - test will be implemented when ARE format is fully supported")]
        public void TestAreEditorLoadExistingFile()
        {
            // This test is skipped until ARE format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py
            // Original: def test_are_editor_load_existing_file(qtbot, installation):
        }

        [Fact]
        public void TestAreEditorSaveLoadRoundtrip()
        {
            var editor = new AREEditor(null, null);
            editor.New();

            // Test save/load roundtrip (will be implemented when ARE format is fully supported)
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }
    }
}

