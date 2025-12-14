using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py
    // Original: Comprehensive tests for MDL Editor
    [Collection("Avalonia Test Collection")]
    public class MDLEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public MDLEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestMdlEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py:18-28
            // Original: def test_mdl_editor_new_file_creation(qtbot, installation):
            var editor = new MDLEditor(null, null);

            editor.New();

            // Verify MDL object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestMdlEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py:30-39
            // Original: def test_mdl_editor_initialization(qtbot, installation):
            var editor = new MDLEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "MDL format loading requires valid MDL data and MDX file - test will be implemented when MDL format is fully supported")]
        public void TestMdlEditorLoadExistingFile()
        {
            // This test is skipped until MDL format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_mdl_editor.py
            // Original: def test_mdl_editor_load_existing_file(qtbot, installation):
        }
    }
}
