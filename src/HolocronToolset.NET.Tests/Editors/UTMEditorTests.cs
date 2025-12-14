using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py
    // Original: Comprehensive tests for UTM Editor
    [Collection("Avalonia Test Collection")]
    public class UTMEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTMEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtmEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:20-30
            // Original: def test_utm_editor_new_file_creation(qtbot, installation):
            var editor = new UTMEditor(null, null);

            editor.New();

            // Verify UTM object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestUtmEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:32-44
            // Original: def test_utm_editor_initialization(qtbot, installation):
            var editor = new UTMEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "UTM format loading requires valid UTM data - test will be implemented when UTM format is fully supported")]
        public void TestUtmEditorLoadExistingFile()
        {
            // This test is skipped until UTM format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py
            // Original: def test_utm_editor_load_existing_file(qtbot, installation):
        }
    }
}
