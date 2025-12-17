using System;
using System.Collections.Generic;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;
using Andastra.Parsing.Common;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py
    // Original: Comprehensive tests for ARE Editor
    [Collection("Avalonia Test Collection")]
    public class AREEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public AREEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestAreEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py
            // Original: def test_are_editor_new_file_creation(qtbot, installation):
            var editor = new AREEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1210-1242
        // Original: def test_are_editor_save_load_roundtrip_identity(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find an ARE file
            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                // Skip if no ARE files available for testing (matching Python pytest.skip behavior)
                return;
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

            var editor = new AREEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(areFile);
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            GFF gff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1210-1242
        // Original: def test_are_editor_save_load_roundtrip_identity(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find tat001.are
            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                // Skip if test file not available (matching Python pytest.skip behavior)
                return;
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

            if (installation == null)
            {
                // Skip if no installation available (needed for LocalizedString operations)
                return;
            }

            var editor = new AREEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1220
            // Original: original_data = are_file.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(areFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1221
            // Original: original_are = read_are(original_data)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1222
            // Original: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            // We'll compare GFF directly instead of ARE object for more comprehensive comparison
            var old = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1222
            // Original: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1225
            // Original: data, _ = editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1226
            // Original: saved_are = read_are(data)
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1228-1234
            // Original: assert saved_are.tag == original_are.tag ...
            // Instead of comparing ARE objects directly, we'll do GFF comparison like UTIEditor test
            GFF newGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(newData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:91
            // Original: diff = old.compare(new, self.log_func, ignore_default_changes=True)
            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:92
            // Original: assert diff, os.linesep.join(self.log_messages)
            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:28-54
        // Original: def test_are_editor_manipulate_name_locstring(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateNameLocstring()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: original_are = read_are(original_data)
            var originalAre = AREHelpers.ReadAre(originalData);

            // Matching Python: new_name = LocalizedString.from_english("Modified Area Name")
            var newName = LocalizedString.FromEnglish("Modified Area Name");

            // Matching Python: editor.ui.nameEdit.set_locstring(new_name)
            if (editor.NameEdit != null)
            {
                editor.NameEdit.SetLocString(newName);
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.name.get(Language.ENGLISH, Gender.MALE) == "Modified Area Name"
            modifiedAre.Name.Get(Language.English, Gender.Male).Should().Be("Modified Area Name");

            // Matching Python: assert modified_are.name.get(Language.ENGLISH, Gender.MALE) != original_are.name.get(Language.ENGLISH, Gender.MALE)
            modifiedAre.Name.Get(Language.English, Gender.Male).Should().NotBe(originalAre.Name.Get(Language.English, Gender.Male));

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
            editor.Load(areFile, "tat001", ResourceType.ARE, data);

            // Matching Python: assert editor.ui.nameEdit.locstring().get(Language.ENGLISH, Gender.MALE) == "Modified Area Name"
            if (editor.NameEdit != null)
            {
                editor.NameEdit.GetLocString().Get(Language.English, Gender.Male).Should().Be("Modified Area Name");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:56-80
        // Original: def test_are_editor_manipulate_tag(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateTag()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: original_are = read_are(original_data)
            var originalAre = AREHelpers.ReadAre(originalData);

            // Matching Python: editor.ui.tagEdit.setText("modified_tag")
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text = "modified_tag";
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.tag == "modified_tag"
            modifiedAre.Tag.Should().Be("modified_tag");

            // Matching Python: assert modified_are.tag != original_are.tag
            modifiedAre.Tag.Should().NotBe(originalAre.Tag);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
            editor.Load(areFile, "tat001", ResourceType.ARE, data);

            // Matching Python: assert editor.ui.tagEdit.text() == "modified_tag"
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text.Should().Be("modified_tag");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:82-103
        // Original: def test_are_editor_manipulate_tag_generate_button(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateTagGenerateButton()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, are_file.read_bytes())
            editor.Load(areFile, "tat001", ResourceType.ARE, System.IO.File.ReadAllBytes(areFile));

            // Matching Python: qtbot.mouseClick(editor.ui.tagGenerateButton, Qt.MouseButton.LeftButton)
            // In Avalonia headless, we can directly invoke the click handler
            if (editor.TagGenerateButton != null)
            {
                // Simulate button click by directly calling the handler
                editor.TagGenerateButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            }

            // Matching Python: assert editor.ui.tagEdit.text() == "tat001"
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text.Should().Be("tat001");
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.tag == "tat001"
            modifiedAre.Tag.Should().Be("tat001");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:105-130
        // Original: def test_are_editor_manipulate_camera_style(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateCameraStyle()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: original_are = read_are(original_data)
            var originalAre = AREHelpers.ReadAre(originalData);

            // Matching Python: if editor.ui.cameraStyleSelect.count() > 0: for i in range(min(5, editor.ui.cameraStyleSelect.count())):
            if (editor.CameraStyleSelect != null && editor.CameraStyleSelect.ItemCount > 0)
            {
                int maxIndex = System.Math.Min(5, editor.CameraStyleSelect.ItemCount);
                for (int i = 0; i < maxIndex; i++)
                {
                    // Matching Python: editor.ui.cameraStyleSelect.setCurrentIndex(i)
                    editor.CameraStyleSelect.SelectedIndex = i;

                    // Matching Python: data, _ = editor.build()
                    var (data, _) = editor.Build();

                    // Matching Python: modified_are = read_are(data)
                    var modifiedAre = AREHelpers.ReadAre(data);

                    // Matching Python: assert modified_are.camera_style == i
                    modifiedAre.CameraStyle.Should().Be(i);

                    // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
                    editor.Load(areFile, "tat001", ResourceType.ARE, data);

                    // Matching Python: assert editor.ui.cameraStyleSelect.currentIndex() == i
                    editor.CameraStyleSelect.SelectedIndex.Should().Be(i);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:132-156
        // Original: def test_are_editor_manipulate_envmap(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateEnvmap()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: test_envmaps = ["test_envmap", "another_env", "env123", ""]
            string[] testEnvmaps = { "test_envmap", "another_env", "env123", "" };

            // Matching Python: for envmap in test_envmaps:
            foreach (string envmap in testEnvmaps)
            {
                // Matching Python: editor.ui.envmapEdit.setText(envmap)
                if (editor.EnvmapEdit != null)
                {
                    editor.EnvmapEdit.Text = envmap;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert str(modified_are.default_envmap) == envmap
                modifiedAre.DefaultEnvMap.ToString().Should().Be(envmap);

                // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
                editor.Load(areFile, "tat001", ResourceType.ARE, data);

                // Matching Python: assert editor.ui.envmapEdit.text() == envmap
                if (editor.EnvmapEdit != null)
                {
                    editor.EnvmapEdit.Text.Should().Be(envmap);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:158-184
        // Original: def test_are_editor_manipulate_disable_transit_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateDisableTransitCheckbox()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: original_are = read_are(original_data)
            var originalAre = AREHelpers.ReadAre(originalData);

            // Matching Python: editor.ui.disableTransitCheck.setChecked(True)
            if (editor.DisableTransitCheck != null)
            {
                editor.DisableTransitCheck.IsChecked = true;
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.disable_transit
            modifiedAre.DisableTransit.Should().BeTrue();

            // Matching Python: editor.ui.disableTransitCheck.setChecked(False)
            if (editor.DisableTransitCheck != null)
            {
                editor.DisableTransitCheck.IsChecked = false;
            }

            // Matching Python: data, _ = editor.build()
            var (data2, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre2 = AREHelpers.ReadAre(data2);

            // Matching Python: assert not modified_are.disable_transit
            modifiedAre2.DisableTransit.Should().BeFalse();

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
            editor.Load(areFile, "tat001", ResourceType.ARE, data2);

            // Matching Python: assert editor.ui.disableTransitCheck.isChecked() == False
            if (editor.DisableTransitCheck != null)
            {
                editor.DisableTransitCheck.IsChecked.Should().BeFalse();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:186-207
        // Original: def test_are_editor_manipulate_unescapable_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateUnescapableCheckbox()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: editor.ui.unescapableCheck.setChecked(True)
            if (editor.UnescapableCheck != null)
            {
                editor.UnescapableCheck.IsChecked = true;
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.unescapable
            modifiedAre.Unescapable.Should().BeTrue();

            // Matching Python: editor.ui.unescapableCheck.setChecked(False)
            if (editor.UnescapableCheck != null)
            {
                editor.UnescapableCheck.IsChecked = false;
            }

            // Matching Python: data, _ = editor.build()
            var (data2, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre2 = AREHelpers.ReadAre(data2);

            // Matching Python: assert not modified_are.unescapable
            modifiedAre2.Unescapable.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:209-233
        // Original: def test_are_editor_manipulate_alpha_test_spin(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateAlphaTestSpin()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: test_values = [0, 1, 50, 100, 255]
            int[] testValues = { 0, 1, 50, 100, 255 };

            // Matching Python: for val in test_values:
            foreach (int val in testValues)
            {
                // Matching Python: editor.ui.alphaTestSpin.setValue(val)
                if (editor.AlphaTestSpin != null)
                {
                    editor.AlphaTestSpin.Value = val;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert modified_are.alpha_test == val
                modifiedAre.AlphaTest.Should().Be(val);

                // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
                editor.Load(areFile, "tat001", ResourceType.ARE, data);

                // Matching Python: assert editor.ui.alphaTestSpin.value() == val
                if (editor.AlphaTestSpin != null)
                {
                    ((int)editor.AlphaTestSpin.Value).Should().Be(val);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:235-256
        // Original: def test_are_editor_manipulate_stealth_xp_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateStealthXpCheckbox()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: editor.ui.stealthCheck.setChecked(True)
            if (editor.StealthCheck != null)
            {
                editor.StealthCheck.IsChecked = true;
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.stealth_xp
            modifiedAre.StealthXp.Should().BeTrue();

            // Matching Python: editor.ui.stealthCheck.setChecked(False)
            if (editor.StealthCheck != null)
            {
                editor.StealthCheck.IsChecked = false;
            }

            // Matching Python: data, _ = editor.build()
            var (data2, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre2 = AREHelpers.ReadAre(data2);

            // Matching Python: assert not modified_are.stealth_xp
            modifiedAre2.StealthXp.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:258-278
        // Original: def test_are_editor_manipulate_stealth_xp_max_spin(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateStealthXpMaxSpin()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: test_values = [0, 10, 50, 100, 1000]
            int[] testValues = { 0, 10, 50, 100, 1000 };
            foreach (int val in testValues)
            {
                // Matching Python: editor.ui.stealthMaxSpin.setValue(val)
                if (editor.StealthMaxSpin != null)
                {
                    editor.StealthMaxSpin.Value = val;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert modified_are.stealth_xp_max == val
                modifiedAre.StealthXpMax.Should().Be(val);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:280-300
        // Original: def test_are_editor_manipulate_stealth_xp_loss_spin(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateStealthXpLossSpin()
        {
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            if (!System.IO.File.Exists(areFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                areFile = System.IO.Path.Combine(testFilesDir, "tat001.are");
            }

            if (!System.IO.File.Exists(areFile))
            {
                return; // Skip if test file not available
            }

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: test_values = [0, 5, 10, 25, 50]
            int[] testValues = { 0, 5, 10, 25, 50 };
            foreach (int val in testValues)
            {
                // Matching Python: editor.ui.stealthLossSpin.setValue(val)
                if (editor.StealthLossSpin != null)
                {
                    editor.StealthLossSpin.Value = val;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert modified_are.stealth_xp_loss == val
                modifiedAre.StealthXpLoss.Should().Be(val);
            }
        }
    }
}

