using System;
using CSharpKOTOR.Common;
using CSharpKOTOR.Resource.Generics;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:24-49
        // Original: def test_utm_editor_manipulate_name_locstring(qtbot, installation: HTInstallation, test_files_dir: Path):
        // Note: This test loads an existing file and verifies it works, similar to other editor tests
        [Fact]
        public void TestUtmEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
            testFilesDir = System.IO.Path.GetFullPath(testFilesDir);

            // Try to find a UTM file
            // Matching PyKotor implementation: utm_file = test_files_dir / "m_chano.utm"
            string[] utmFiles = new string[0];
            if (System.IO.Directory.Exists(testFilesDir))
            {
                utmFiles = System.IO.Directory.GetFiles(testFilesDir, "*.utm", System.IO.SearchOption.AllDirectories);
            }

            // Try alternative location
            if (utmFiles.Length == 0)
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                testFilesDir = System.IO.Path.GetFullPath(testFilesDir);
                if (System.IO.Directory.Exists(testFilesDir))
                {
                    utmFiles = System.IO.Directory.GetFiles(testFilesDir, "*.utm", System.IO.SearchOption.AllDirectories);
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

            if (utmFiles.Length == 0)
            {
                // Matching PyKotor implementation: Try to get one from installation
                if (installation == null)
                {
                    // Skip if no UTM files available and no installation
                    return;
                }

                // Try to get a UTM resource from installation
                var queries = new System.Collections.Generic.List<CSharpKOTOR.Resources.ResourceIdentifier>
                {
                    new CSharpKOTOR.Resources.ResourceIdentifier("", ResourceType.UTM)
                };
                var utmResourcesDict = installation.Resources(queries);
                var utmResources = new System.Collections.Generic.List<CSharpKOTOR.Installation.ResourceResult>();
                foreach (var kvp in utmResourcesDict)
                {
                    if (kvp.Value != null)
                    {
                        utmResources.Add(kvp.Value);
                    }
                }

                if (utmResources.Count == 0)
                {
                    // Skip if no UTM resources found
                    return;
                }

                // Matching PyKotor implementation: Use first UTM resource
                var utmResource = utmResources[0];
                var resourceResult = installation.Resource(utmResource.ResName, utmResource.ResType);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    // Skip if could not load UTM data
                    return;
                }

                var editor = new UTMEditor(null, installation);
                // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
                editor.Load(
                    utmResource.FilePath ?? "module.utm",
                    utmResource.ResName,
                    ResourceType.UTM,
                    resourceResult.Data
                );

                // Matching PyKotor implementation: Verify editor loaded the data
                editor.Should().NotBeNull();

                // Matching PyKotor implementation: Build and verify it works
                var (data, _) = editor.Build();
                data.Should().NotBeNull();
                data.Length.Should().BeGreaterThan(0);

                // Matching PyKotor implementation: Verify we can read it back
                var gff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
                gff.Should().NotBeNull();
            }
            else
            {
                // Matching PyKotor implementation: utm_file = test_files_dir / "m_chano.utm"
                string utmFile = utmFiles[0];
                byte[] originalData = System.IO.File.ReadAllBytes(utmFile);
                string resref = System.IO.Path.GetFileNameWithoutExtension(utmFile);

                var editor = new UTMEditor(null, installation);
                // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
                editor.Load(utmFile, resref, ResourceType.UTM, originalData);

                // Matching PyKotor implementation: Verify editor loaded the data
                editor.Should().NotBeNull();

                // Matching PyKotor implementation: Build and verify it works
                var (data, _) = editor.Build();
                data.Should().NotBeNull();
                data.Length.Should().BeGreaterThan(0);

                // Matching PyKotor implementation: Verify we can read it back
                var gff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
                gff.Should().NotBeNull();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:24-49
        // Original: def test_utm_editor_manipulate_name_locstring(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateNameLocstring()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
            testFilesDir = System.IO.Path.GetFullPath(testFilesDir);

            // Try to find m_chano.utm
            string utmFile = System.IO.Path.Combine(testFilesDir, "m_chano.utm");
            if (!System.IO.File.Exists(utmFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                testFilesDir = System.IO.Path.GetFullPath(testFilesDir);
                utmFile = System.IO.Path.Combine(testFilesDir, "m_chano.utm");
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

            if (!System.IO.File.Exists(utmFile))
            {
                // Skip if test file not available (matching Python pytest.skip behavior)
                return;
            }

            if (installation == null)
            {
                // Skip if no installation available (needed for LocalizedString operations)
                return;
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: original_utm = read_utm(original_data)
            var originalUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(originalData));

            // Matching PyKotor implementation: new_name = LocalizedString.from_english("Modified Merchant Name")
            // Matching PyKotor implementation: editor.ui.nameEdit.set_locstring(new_name)
            var newName = LocalizedString.FromEnglish("Modified Merchant Name");
            editor.NameEdit.Should().NotBeNull("NameEdit should be initialized");
            editor.NameEdit.SetLocString(newName);

            // Matching PyKotor implementation: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching PyKotor implementation: modified_utm = read_utm(data)
            var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

            // Matching PyKotor implementation: assert modified_utm.name.get(Language.ENGLISH, Gender.MALE) == "Modified Merchant Name"
            modifiedUtm.Name.Get(Language.English, Gender.Male, false).Should().Be("Modified Merchant Name");

            // Matching PyKotor implementation: assert modified_utm.name.get(Language.ENGLISH, Gender.MALE) != original_utm.name.get(Language.ENGLISH, Gender.MALE)
            var originalName = originalUtm.Name.Get(Language.English, Gender.Male, false);
            modifiedUtm.Name.Get(Language.English, Gender.Male, false).Should().NotBe(originalName);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, data);

            // Matching PyKotor implementation: assert editor.ui.nameEdit.locstring().get(Language.ENGLISH, Gender.MALE) == "Modified Merchant Name"
            editor.NameEdit.Should().NotBeNull("NameEdit should be initialized after load");
            var loadedName = editor.NameEdit.GetLocString();
            loadedName.Get(Language.English, Gender.Male, false).Should().Be("Modified Merchant Name");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:51-75
        // Original: def test_utm_editor_manipulate_tag(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateTag()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
            testFilesDir = System.IO.Path.GetFullPath(testFilesDir);

            // Try to find m_chano.utm
            string utmFile = System.IO.Path.Combine(testFilesDir, "m_chano.utm");
            if (!System.IO.File.Exists(utmFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                testFilesDir = System.IO.Path.GetFullPath(testFilesDir);
                utmFile = System.IO.Path.Combine(testFilesDir, "m_chano.utm");
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

            if (!System.IO.File.Exists(utmFile))
            {
                // Skip if test file not available (matching Python pytest.skip behavior)
                return;
            }

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: original_utm = read_utm(original_data)
            var originalUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(originalData));

            // Matching PyKotor implementation: editor.ui.tagEdit.setText("modified_tag")
            editor.TagEdit.Should().NotBeNull("TagEdit should be initialized");
            editor.TagEdit.Text = "modified_tag";

            // Matching PyKotor implementation: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching PyKotor implementation: modified_utm = read_utm(data)
            var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

            // Matching PyKotor implementation: assert modified_utm.tag == "modified_tag"
            modifiedUtm.Tag.Should().Be("modified_tag");

            // Matching PyKotor implementation: assert modified_utm.tag != original_utm.tag
            modifiedUtm.Tag.Should().NotBe(originalUtm.Tag);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, data);

            // Matching PyKotor implementation: assert editor.ui.tagEdit.text() == "modified_tag"
            editor.TagEdit.Text.Should().Be("modified_tag");
        }
    }
}
