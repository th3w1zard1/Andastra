using System;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py
    // Original: Comprehensive tests for UTE Editor
    [Collection("Avalonia Test Collection")]
    public class UTEEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTEEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUteEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:20-30
            // Original: def test_ute_editor_new_file_creation(qtbot, installation):
            var editor = new UTEEditor(null, null);

            editor.New();

            // Verify UTE object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestUteEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:32-44
            // Original: def test_ute_editor_initialization(qtbot, installation):
            var editor = new UTEEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:24-49
        // Original: def test_ute_editor_manipulate_name_locstring(qtbot, installation: HTInstallation, test_files_dir: Path):
        // Note: This test loads an existing file and verifies it works, similar to other editor tests
        [Fact]
        public void TestUteEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
            testFilesDir = System.IO.Path.GetFullPath(testFilesDir);

            // Try to find a UTE file
            // Matching PyKotor implementation: ute_file = test_files_dir / "newtransition.ute"
            string[] uteFiles = new string[0];
            if (System.IO.Directory.Exists(testFilesDir))
            {
                uteFiles = System.IO.Directory.GetFiles(testFilesDir, "*.ute", System.IO.SearchOption.AllDirectories);
            }

            // Try alternative location
            if (uteFiles.Length == 0)
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                testFilesDir = System.IO.Path.GetFullPath(testFilesDir);
                if (System.IO.Directory.Exists(testFilesDir))
                {
                    uteFiles = System.IO.Directory.GetFiles(testFilesDir, "*.ute", System.IO.SearchOption.AllDirectories);
                }
            }

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
            else
            {
                // Fallback to K2
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                }
            }

            if (uteFiles.Length == 0)
            {
                // Matching PyKotor implementation: Try to get one from installation
                if (installation == null)
                {
                    // Skip if no UTE files available and no installation
                    return;
                }

                // Try to get a UTE resource from installation
                var queries = new System.Collections.Generic.List<Andastra.Parsing.Resources.ResourceIdentifier>
                {
                    new Andastra.Parsing.Resources.ResourceIdentifier("", ResourceType.UTE)
                };
                var uteResourcesDict = installation.Resources(queries);
                var uteResources = new System.Collections.Generic.List<Andastra.Parsing.Installation.ResourceResult>();
                foreach (var kvp in uteResourcesDict)
                {
                    if (kvp.Value != null)
                    {
                        uteResources.Add(kvp.Value);
                    }
                }

                if (uteResources.Count == 0)
                {
                    // Skip if no UTE resources found
                    return;
                }

                // Matching PyKotor implementation: Use first UTE resource
                Andastra.Parsing.Installation.ResourceResult uteResource = uteResources[0];
                var resourceResult = installation.Resource(uteResource.ResName, uteResource.ResType);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    // Skip if could not load UTE data
                    return;
                }

                var editor = new UTEEditor(null, installation);
                // Matching PyKotor implementation: editor.load(ute_file, "newtransition", ResourceType.UTE, original_data)
                editor.Load(
                    uteResource.FilePath ?? "module.ute",
                    uteResource.ResName,
                    ResourceType.UTE,
                    resourceResult.Data
                );

                // Matching PyKotor implementation: Verify editor loaded the data
                editor.Should().NotBeNull();

                // Matching PyKotor implementation: Build and verify it works
                var (data, _) = editor.Build();
                data.Should().NotBeNull();
                data.Length.Should().BeGreaterThan(0);

                // Matching PyKotor implementation: Verify we can read it back
                Andastra.Parsing.Formats.GFF.GFF gff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
                gff.Should().NotBeNull();
            }
            else
            {
                // Matching PyKotor implementation: ute_file = test_files_dir / "newtransition.ute"
                string uteFile = uteFiles[0];
                byte[] originalData = System.IO.File.ReadAllBytes(uteFile);
                string resref = System.IO.Path.GetFileNameWithoutExtension(uteFile);

                var editor = new UTEEditor(null, installation);
                // Matching PyKotor implementation: editor.load(ute_file, "newtransition", ResourceType.UTE, original_data)
                editor.Load(uteFile, resref, ResourceType.UTE, originalData);

                // Matching PyKotor implementation: Verify editor loaded the data
                editor.Should().NotBeNull();

                // Matching PyKotor implementation: Build and verify it works
                var (data, _) = editor.Build();
                data.Should().NotBeNull();
                data.Length.Should().BeGreaterThan(0);

                // Matching PyKotor implementation: Verify we can read it back
                Andastra.Parsing.Formats.GFF.GFF gff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
                gff.Should().NotBeNull();
            }
        }
    }
}
