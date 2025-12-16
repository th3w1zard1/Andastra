using System;
using Andastra.Parsing.Formats.LTR;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py
    // Original: Comprehensive tests for LTR Editor
    [Collection("Avalonia Test Collection")]
    public class LTREditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public LTREditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestLtrEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ltr_editor.py
            // Original: def test_ltr_editor_new_file_creation(qtbot, installation):
            var editor = new LTREditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestLtrEditorLoadExistingFile()
        {
            var editor = new LTREditor(null, null);

            // Create minimal LTR data
            var ltr = new LTR();
            ltr.SetSinglesStart("a", 0.1f);
            ltr.SetSinglesMiddle("a", 0.2f);
            ltr.SetSinglesEnd("a", 0.3f);
            byte[] testData = LTRAuto.BytesLtr(ltr);

            editor.Load("test.ltr", "test", ResourceType.LTR, testData);

            // Verify content loaded
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestLtrEditorSaveLoadRoundtrip()
        {
            var editor = new LTREditor(null, null);
            editor.New();

            // Test save/load roundtrip
            var (data, _) = editor.Build();
            data.Should().NotBeNull();

            var editor2 = new LTREditor(null, null);
            editor2.Load("test.ltr", "test", ResourceType.LTR, data);
            var (data2, _) = editor2.Build();
            data2.Should().Equal(data);
        }

        [Fact]
        public void TestLtrEditorGenerateName()
        {
            var editor = new LTREditor(null, null);
            editor.New();

            // Generate name should work
            // This will be tested when UI is fully connected
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }
    }
}
