using System;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py
    // Original: Comprehensive tests for NSS Editor
    [Collection("Avalonia Test Collection")]
    public class NSSEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public NSSEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestNssEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py
            // Original: def test_nss_editor_new_file_creation(qtbot, installation):
            var editor = new NSSEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestNssEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py:75-96
            // Original: def test_nss_editor_document_layout(qtbot, installation):
            var editor = new NSSEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact(Skip = "NSS format loading requires valid NSS data - test will be implemented when NSS format is fully supported")]
        public void TestNssEditorLoadExistingFile()
        {
            // This test is skipped until NSS format loading is fully implemented
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_nss_editor.py
            // Original: def test_nss_editor_load_existing_file(qtbot, installation):
        }
    }
}
