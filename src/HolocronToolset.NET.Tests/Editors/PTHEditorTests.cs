using System;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_pth_editor.py
    // Original: Comprehensive tests for PTH Editor
    [Collection("Avalonia Test Collection")]
    public class PTHEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public PTHEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestPthEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_pth_editor.py:20-30
            // Original: def test_pth_editor_new_file_creation(qtbot, installation):
            var editor = new PTHEditor(null, null);

            editor.New();

            // Verify PTH object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestPthEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_pth_editor.py:32-44
            // Original: def test_pth_editor_initialization(qtbot, installation):
            var editor = new PTHEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_pth_editor.py:207-230
        // Original: def test_pth_editor_save_load_roundtrip(qtbot, installation: HTInstallation):
        [Fact]
        public void TestPthEditorSaveLoadRoundtrip()
        {
            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new PTHEditor(null, installation);

            editor.New();

            // Add nodes and edges
            editor.AddNode(0.0f, 0.0f);
            editor.AddNode(10.0f, 10.0f);
            editor.AddNode(20.0f, 20.0f);
            editor.AddEdge(0, 1);
            editor.AddEdge(1, 2);

            // Build
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Load it back
            // Note: PTH loading requires LYT file, so we skip loading for now
            // Just verify build works
            var loadedPth = CSharpKOTOR.Resource.Generics.PTHAuto.ReadPth(data);
            loadedPth.Should().NotBeNull();
            loadedPth.Count.Should().Be(3, "Should have 3 nodes");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_pth_editor.py:232-251
        // Original: def test_pth_editor_multiple_save_load_cycles(qtbot, installation: HTInstallation):
        [Fact]
        public void TestPthEditorMultipleSaveLoadCycles()
        {
            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new PTHEditor(null, installation);

            editor.New();

            // Perform multiple cycles
            for (int cycle = 0; cycle < 3; cycle++)
            {
                // Clear and add nodes
                editor.New();
                for (int i = 0; i <= cycle; i++)
                {
                    editor.AddNode((float)(i * 10), (float)(i * 10));
                }

                // Save
                var (data, _) = editor.Build();
                var loadedPth = CSharpKOTOR.Resource.Generics.PTHAuto.ReadPth(data);

                // Verify nodes were preserved
                loadedPth.Count.Should().Be(cycle + 1, $"Should have {cycle + 1} nodes after cycle {cycle}");
            }
        }
    }
}
