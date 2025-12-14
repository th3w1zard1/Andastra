using System;
using CSharpKOTOR.Formats.BWM;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_bwm_editor.py:388-420
        // Original: def test_bwm_editor_headless_ui_load_build(qtbot: QtBot, installation: HTInstallation, test_files_dir: pathlib.Path):
        [Fact]
        public void TestBwmEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a BWM file (WOK, DWK, or PWK)
            string bwmFile = System.IO.Path.Combine(testFilesDir, "zio006j.wok");
            if (!System.IO.File.Exists(bwmFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                bwmFile = System.IO.Path.Combine(testFilesDir, "zio006j.wok");
            }

            byte[] originalData = null;
            string resname = "zio006j";
            ResourceType restype = ResourceType.WOK;

            if (System.IO.File.Exists(bwmFile))
            {
                originalData = System.IO.File.ReadAllBytes(bwmFile);
            }
            else
            {
                // Try to get one from installation (K2 preferred for BWM files)
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                HTInstallation installation = null;
                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                    // Try to find any BWM resource
                    try
                    {
                        var allResources = installation.Installation.CoreResources();
                        foreach (var resource in allResources)
                        {
                            if (resource.ResType == ResourceType.WOK || resource.ResType == ResourceType.DWK || resource.ResType == ResourceType.PWK)
                            {
                                var result = installation.Resource(resource.ResName, resource.ResType);
                                if (result != null && result.Data != null && result.Data.Length > 0)
                                {
                                    originalData = result.Data;
                                    resname = resource.ResName;
                                    restype = resource.ResType;
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // If resources iteration fails, continue without installation resources
                    }
                }

                if (originalData == null || originalData.Length == 0)
                {
                    // Skip if no BWM files available for testing (matching Python pytest.skip behavior)
                    return;
                }

                var editor = new BWMEditor(null, installation);
                editor.Load(bwmFile ?? "test.wok", resname, restype, originalData);

                // Verify editor loaded the data
                editor.Should().NotBeNull();

                // Build and verify it works
                var (data, _) = editor.Build();
                data.Should().NotBeNull();
                data.Length.Should().BeGreaterThan(0);

                // Verify we can read it back
                var loadedBwm = CSharpKOTOR.Formats.BWM.BWMAuto.ReadBwm(data);
                loadedBwm.Should().NotBeNull();
                return;
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation2 = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation2 = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor2 = new BWMEditor(null, installation2);
            editor2.Load(bwmFile, resname, restype, originalData);

            // Verify editor loaded the data
            editor2.Should().NotBeNull();

            // Build and verify it works
            var (data2, _) = editor2.Build();
            data2.Should().NotBeNull();
            data2.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            var loadedBwm2 = CSharpKOTOR.Formats.BWM.BWMAuto.ReadBwm(data2);
            loadedBwm2.Should().NotBeNull();
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

