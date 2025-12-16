using System;
using AuroraEngine.Common.Formats.SSF;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ssf_editor.py
    // Original: Comprehensive tests for SSF Editor
    [Collection("Avalonia Test Collection")]
    public class SSFEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public SSFEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestSsfEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ssf_editor.py
            // Original: def test_ssf_editor_new_file_creation(qtbot, installation):
            var editor = new SSFEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestSsfEditorLoadExistingFile()
        {
            var editor = new SSFEditor(null, null);

            // Create minimal SSF data
            var ssf = new SSF();
            ssf.SetData(SSFSound.BATTLE_CRY_1, 100);
            ssf.SetData(SSFSound.SELECT_1, 200);
            byte[] testData = ssf.ToBytes();

            editor.Load("test.ssf", "test", ResourceType.SSF, testData);

            // Verify content loaded
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        [Fact]
        public void TestSsfEditorSaveLoadRoundtrip()
        {
            var editor = new SSFEditor(null, null);
            editor.New();

            // Test save/load roundtrip
            var (data, _) = editor.Build();
            data.Should().NotBeNull();

            var editor2 = new SSFEditor(null, null);
            editor2.Load("test.ssf", "test", ResourceType.SSF, data);
            var (data2, _) = editor2.Build();
            data2.Should().Equal(data);
        }
    }
}
