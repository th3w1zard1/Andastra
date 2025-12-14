using System;
using System.Linq;
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

        // Helper method to get test file and installation (matching Python test pattern)
        private (string utmFile, HTInstallation installation) GetTestFileAndInstallation()
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

            return (utmFile, installation);
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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:77-99
        // Original: def test_utm_editor_manipulate_resref(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateResref()
        {
            var (utmFile, installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utmFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: editor.ui.resrefEdit.setText("modified_resref")
            editor.ResrefEdit.Should().NotBeNull("ResrefEdit should be initialized");
            editor.ResrefEdit.Text = "modified_resref";

            // Matching PyKotor implementation: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching PyKotor implementation: modified_utm = read_utm(data)
            var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

            // Matching PyKotor implementation: assert str(modified_utm.resref) == "modified_resref"
            modifiedUtm.ResRef.ToString().Should().Be("modified_resref");

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, data);

            // Matching PyKotor implementation: assert editor.ui.resrefEdit.text() == "modified_resref"
            editor.ResrefEdit.Text.Should().Be("modified_resref");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:101-125
        // Original: def test_utm_editor_manipulate_id_spin(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateIdSpin()
        {
            var (utmFile, installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utmFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: test_id_values = [0, 1, 5, 10, 100, 255]
            int[] testIdValues = { 0, 1, 5, 10, 100, 255 };

            // Matching PyKotor implementation: for val in test_id_values:
            foreach (int val in testIdValues)
            {
                // Matching PyKotor implementation: editor.ui.idSpin.setValue(val)
                editor.IdSpin.Should().NotBeNull("IdSpin should be initialized");
                editor.IdSpin.Value = (decimal)val;
                // Verify the value was set correctly
                editor.IdSpin.Value.Should().Be((decimal)val, "IdSpin value should be set correctly");

                // Matching PyKotor implementation: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching PyKotor implementation: modified_utm = read_utm(data)
                var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

                // Matching PyKotor implementation: assert modified_utm.id == val
                modifiedUtm.Id.Should().Be(val);

                // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, data)
                editor.Load(utmFile, "m_chano", ResourceType.UTM, data);

                // Matching PyKotor implementation: assert editor.ui.idSpin.value() == val
                editor.IdSpin.Value.Should().Be(val);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:127-152
        // Original: def test_utm_editor_manipulate_markup_spins(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateMarkupSpins()
        {
            var (utmFile, installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utmFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: test_markup_values = [0, 10, 25, 50, 100, 200]
            int[] testMarkupValues = { 0, 10, 25, 50, 100, 200 };

            // Matching PyKotor implementation: for val in test_markup_values: (mark up)
            foreach (int val in testMarkupValues)
            {
                // Matching PyKotor implementation: editor.ui.markUpSpin.setValue(val)
                editor.MarkUpSpin.Should().NotBeNull("MarkUpSpin should be initialized");
                editor.MarkUpSpin.Value = val;

                // Matching PyKotor implementation: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching PyKotor implementation: modified_utm = read_utm(data)
                var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

                // Matching PyKotor implementation: assert modified_utm.mark_up == val
                modifiedUtm.MarkUp.Should().Be(val);
            }

            // Matching PyKotor implementation: for val in test_markup_values: (mark down)
            foreach (int val in testMarkupValues)
            {
                // Matching PyKotor implementation: editor.ui.markDownSpin.setValue(val)
                editor.MarkDownSpin.Should().NotBeNull("MarkDownSpin should be initialized");
                editor.MarkDownSpin.Value = val;

                // Matching PyKotor implementation: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching PyKotor implementation: modified_utm = read_utm(data)
                var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

                // Matching PyKotor implementation: assert modified_utm.mark_down == val
                modifiedUtm.MarkDown.Should().Be(val);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:154-176
        // Original: def test_utm_editor_manipulate_on_open_script(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateOnOpenScript()
        {
            var (utmFile, installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utmFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: editor.ui.onOpenEdit.setText("test_on_open")
            editor.OnOpenEdit.Should().NotBeNull("OnOpenEdit should be initialized");
            editor.OnOpenEdit.Text = "test_on_open";

            // Matching PyKotor implementation: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching PyKotor implementation: modified_utm = read_utm(data)
            var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

            // Matching PyKotor implementation: assert str(modified_utm.on_open) == "test_on_open"
            modifiedUtm.OnOpenScript.ToString().Should().Be("test_on_open");

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, data);

            // Matching PyKotor implementation: assert editor.ui.onOpenEdit.text() == "test_on_open"
            editor.OnOpenEdit.Text.Should().Be("test_on_open");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:178-211
        // Original: def test_utm_editor_manipulate_store_flag_select(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateStoreFlagSelect()
        {
            var (utmFile, installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utmFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: for i in range(editor.ui.storeFlagSelect.count()):
            editor.StoreFlagSelect.Should().NotBeNull("StoreFlagSelect should be initialized");
            for (int i = 0; i < editor.StoreFlagSelect.ItemCount; i++)
            {
                // Matching PyKotor implementation: editor.ui.storeFlagSelect.setCurrentIndex(i)
                editor.StoreFlagSelect.SelectedIndex = i;

                // Matching PyKotor implementation: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching PyKotor implementation: modified_utm = read_utm(data)
                var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

                // Matching PyKotor implementation: expected_can_buy = bool((i + 1) & 1)
                // Matching PyKotor implementation: expected_can_sell = bool((i + 1) & 2)
                bool expectedCanBuy = ((i + 1) & 1) != 0;
                bool expectedCanSell = ((i + 1) & 2) != 0;

                // Matching PyKotor implementation: assert modified_utm.can_buy == expected_can_buy
                // Matching PyKotor implementation: assert modified_utm.can_sell == expected_can_sell
                modifiedUtm.CanBuy.Should().Be(expectedCanBuy);
                modifiedUtm.CanSell.Should().Be(expectedCanSell);

                // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, data)
                editor.Load(utmFile, "m_chano", ResourceType.UTM, data);

                // Matching PyKotor implementation: assert editor.ui.storeFlagSelect.currentIndex() == i
                editor.StoreFlagSelect.SelectedIndex.Should().Be(i);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utm_editor.py:217-248
        // Original: def test_utm_editor_manipulate_comments(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtmEditorManipulateComments()
        {
            var (utmFile, installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utmFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTMEditor(None, installation)
            var editor = new UTMEditor(null, installation);

            // Matching PyKotor implementation: original_data = utm_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utmFile);

            // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, original_data)
            editor.Load(utmFile, "m_chano", ResourceType.UTM, originalData);

            // Matching PyKotor implementation: test_comments = ["", "Test comment", "Multi\nline\ncomment", ...]
            string[] testComments = {
                "",
                "Test comment",
                "Multi\nline\ncomment",
                "Comment with special chars !@#$%^&*()",
                string.Join("", Enumerable.Repeat("Very long comment ", 100))
            };

            // Matching PyKotor implementation: for comment in test_comments:
            foreach (string comment in testComments)
            {
                // Matching PyKotor implementation: editor.ui.commentsEdit.setPlainText(comment)
                editor.CommentsEdit.Should().NotBeNull("CommentsEdit should be initialized");
                editor.CommentsEdit.Text = comment;

                // Matching PyKotor implementation: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching PyKotor implementation: modified_utm = read_utm(data)
                var modifiedUtm = UTMHelpers.ConstructUtm(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

                // Matching PyKotor implementation: assert modified_utm.comment == comment
                modifiedUtm.Comment.Should().Be(comment);

                // Matching PyKotor implementation: editor.load(utm_file, "m_chano", ResourceType.UTM, data)
                editor.Load(utmFile, "m_chano", ResourceType.UTM, data);

                // Matching PyKotor implementation: assert editor.ui.commentsEdit.toPlainText() == comment
                editor.CommentsEdit.Text.Should().Be(comment);
            }
        }
    }
}
