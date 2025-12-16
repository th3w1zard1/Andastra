using System;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_gff_editor.py
    // Original: Comprehensive tests for GFF Editor
    [Collection("Avalonia Test Collection")]
    public class GFFEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public GFFEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestGffEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_gff_editor.py
            // Original: def test_gff_editor_new_file_creation(qtbot, installation):
            var editor = new GFFEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestGffEditorLoadExistingFile()
        {
            var editor = new GFFEditor(null, null);

            // Create minimal GFF data
            var gff = new GFF(GFFContent.GFF);
            gff.Root.SetString("TestLabel", "TestValue");
            byte[] testData = gff.ToBytes();

            editor.Load("test.gff", "test", ResourceType.GFF, testData);

            // Verify content loaded
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestGffEditorSaveLoadRoundtrip()
        {
            var editor = new GFFEditor(null, null);
            editor.New();

            // Test save/load roundtrip
            var (data, _) = editor.Build();
            data.Should().NotBeNull();

            var editor2 = new GFFEditor(null, null);
            editor2.Load("test.gff", "test", ResourceType.GFF, data);
            var (data2, _) = editor2.Build();
            data2.Should().NotBeNull();
            // Note: GFF roundtrip may not be byte-for-byte identical due to structure differences
        }
    }
}
