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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:306-329
        // Original: def test_are_editor_manipulate_map_axis(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateMapAxis()
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

            // Matching Python: for axis in [0, 1, 2, 3]:
            int[] axes = { 0, 1, 2, 3 };
            foreach (int axis in axes)
            {
                // Matching Python: editor.ui.mapAxisSelect.setCurrentIndex(axis)
                if (editor.MapAxisSelect != null)
                {
                    editor.MapAxisSelect.SelectedIndex = axis;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert modified_are.north_axis == ARENorthAxis(axis)
                modifiedAre.NorthAxis.Should().Be((ARENorthAxis)axis);

                // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
                editor.Load(areFile, "tat001", ResourceType.ARE, data);

                // Matching Python: assert editor.ui.mapAxisSelect.currentIndex() == axis
                if (editor.MapAxisSelect != null)
                {
                    editor.MapAxisSelect.SelectedIndex.Should().Be(axis);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:331-351
        // Original: def test_are_editor_manipulate_map_zoom_spin(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateMapZoomSpin()
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

            // Matching Python: test_values = [1, 2, 5, 10, 20, 50]
            int[] testValues = { 1, 2, 5, 10, 20, 50 };
            foreach (int val in testValues)
            {
                // Matching Python: editor.ui.mapZoomSpin.setValue(val)
                if (editor.MapZoomSpin != null)
                {
                    editor.MapZoomSpin.Value = val;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert abs(modified_are.map_zoom - float(val)) < 0.001
                System.Math.Abs((double)modifiedAre.MapZoom - val).Should().BeLessThan(0.001);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:353-373
        // Original: def test_are_editor_manipulate_map_res_x_spin(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateMapResXSpin()
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

            // Matching Python: test_values = [128, 256, 512, 1024, 2048]
            int[] testValues = { 128, 256, 512, 1024, 2048 };
            foreach (int val in testValues)
            {
                // Matching Python: editor.ui.mapResXSpin.setValue(val)
                if (editor.MapResXSpin != null)
                {
                    editor.MapResXSpin.Value = val;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert modified_are.map_res_x == val
                modifiedAre.MapResX.Should().Be(val);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:375-426
        // Original: def test_are_editor_manipulate_map_image_points(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateMapImagePoints()
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

            // Matching Python: test_points = [(Vector2(0.1, 0.1), Vector2(0.9, 0.9)), ...]
            var testPoints = new[]
            {
                (new System.Numerics.Vector2(0.1f, 0.1f), new System.Numerics.Vector2(0.9f, 0.9f)),
                (new System.Numerics.Vector2(0.1f, 0.2f), new System.Numerics.Vector2(0.8f, 0.9f)),
                (new System.Numerics.Vector2(0.25f, 0.25f), new System.Numerics.Vector2(0.75f, 0.75f)),
                (new System.Numerics.Vector2(0.5f, 0.5f), new System.Numerics.Vector2(0.5f, 0.5f))
            };

            foreach (var (point1, point2) in testPoints)
            {
                // Matching Python: editor.ui.mapImageX1Spin.setValue(point1.x)
                if (editor.MapImageX1Spin != null)
                {
                    editor.MapImageX1Spin.Value = (decimal)point1.X;
                }
                // Matching Python: editor.ui.mapImageY1Spin.setValue(point1.y)
                if (editor.MapImageY1Spin != null)
                {
                    editor.MapImageY1Spin.Value = (decimal)point1.Y;
                }
                // Matching Python: editor.ui.mapImageX2Spin.setValue(point2.x)
                if (editor.MapImageX2Spin != null)
                {
                    editor.MapImageX2Spin.Value = (decimal)point2.X;
                }
                // Matching Python: editor.ui.mapImageY2Spin.setValue(point2.y)
                if (editor.MapImageY2Spin != null)
                {
                    editor.MapImageY2Spin.Value = (decimal)point2.Y;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert abs(modified_are.map_point_1.x - point1.x) < 0.001
                System.Math.Abs(modifiedAre.MapPoint1.X - point1.X).Should().BeLessThan(0.001f);
                System.Math.Abs(modifiedAre.MapPoint1.Y - point1.Y).Should().BeLessThan(0.001f);
                System.Math.Abs(modifiedAre.MapPoint2.X - point2.X).Should().BeLessThan(0.001f);
                System.Math.Abs(modifiedAre.MapPoint2.Y - point2.Y).Should().BeLessThan(0.001f);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:424-455
        // Original: def test_are_editor_manipulate_map_world_points(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateMapWorldPoints()
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

            // Matching Python: test_points = [(Vector2(0.0, 0.0), Vector2(10.0, 10.0)), ...]
            var testPoints = new[]
            {
                (new System.Numerics.Vector2(0.0f, 0.0f), new System.Numerics.Vector2(10.0f, 10.0f)),
                (new System.Numerics.Vector2(-10.0f, -10.0f), new System.Numerics.Vector2(10.0f, 10.0f)),
                (new System.Numerics.Vector2(100.0f, 200.0f), new System.Numerics.Vector2(300.0f, 400.0f))
            };

            foreach (var (point1, point2) in testPoints)
            {
                // Matching Python: editor.ui.mapWorldX1Spin.setValue(point1.x)
                if (editor.MapWorldX1Spin != null)
                {
                    editor.MapWorldX1Spin.Value = (decimal)point1.X;
                }
                // Matching Python: editor.ui.mapWorldY1Spin.setValue(point1.y)
                if (editor.MapWorldY1Spin != null)
                {
                    editor.MapWorldY1Spin.Value = (decimal)point1.Y;
                }
                // Matching Python: editor.ui.mapWorldX2Spin.setValue(point2.x)
                if (editor.MapWorldX2Spin != null)
                {
                    editor.MapWorldX2Spin.Value = (decimal)point2.X;
                }
                // Matching Python: editor.ui.mapWorldY2Spin.setValue(point2.y)
                if (editor.MapWorldY2Spin != null)
                {
                    editor.MapWorldY2Spin.Value = (decimal)point2.Y;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert abs(modified_are.world_point_1.x - point1.x) < 0.001
                System.Math.Abs(modifiedAre.WorldPoint1.X - point1.X).Should().BeLessThan(0.001f);
                System.Math.Abs(modifiedAre.WorldPoint1.Y - point1.Y).Should().BeLessThan(0.001f);
                System.Math.Abs(modifiedAre.WorldPoint2.X - point2.X).Should().BeLessThan(0.001f);
                System.Math.Abs(modifiedAre.WorldPoint2.Y - point2.Y).Should().BeLessThan(0.001f);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:461-482
        // Original: def test_are_editor_manipulate_fog_enabled_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateFogEnabledCheckbox()
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

            // Matching Python: editor.ui.fogEnabledCheck.setChecked(True)
            if (editor.FogEnabledCheck != null)
            {
                editor.FogEnabledCheck.IsChecked = true;
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.fog_enabled
            modifiedAre.FogEnabled.Should().BeTrue();

            // Matching Python: editor.ui.fogEnabledCheck.setChecked(False)
            if (editor.FogEnabledCheck != null)
            {
                editor.FogEnabledCheck.IsChecked = false;
            }

            // Matching Python: data, _ = editor.build()
            var (data2, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre2 = AREHelpers.ReadAre(data2);

            // Matching Python: assert not modified_are.fog_enabled
            modifiedAre2.FogEnabled.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:484-513
        // Original: def test_are_editor_manipulate_fog_color(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateFogColor()
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

            // Matching Python: test_colors = [Color(1.0, 0.0, 0.0), ...]
            var testColors = new[]
            {
                new Andastra.Parsing.Common.Color(1.0f, 0.0f, 0.0f),  // Red
                new Andastra.Parsing.Common.Color(0.0f, 1.0f, 0.0f),  // Green
                new Andastra.Parsing.Common.Color(0.0f, 0.0f, 1.0f),  // Blue
                new Andastra.Parsing.Common.Color(0.5f, 0.5f, 0.5f),  // Gray
                new Andastra.Parsing.Common.Color(1.0f, 1.0f, 1.0f)   // White
            };

            foreach (var color in testColors)
            {
                // Matching Python: editor.ui.fogColorEdit.set_color(color)
                if (editor.FogColorEdit != null)
                {
                    editor.FogColorEdit.SetColor(color);
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert abs(modified_are.fog_color.r - color.r) < 0.01
                System.Math.Abs(modifiedAre.FogColor.R - color.R).Should().BeLessThan(0.01f);
                System.Math.Abs(modifiedAre.FogColor.G - color.G).Should().BeLessThan(0.01f);
                System.Math.Abs(modifiedAre.FogColor.B - color.B).Should().BeLessThan(0.01f);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:515-541
        // Original: def test_are_editor_manipulate_fog_near_far_spins(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestAreEditorManipulateFogNearFarSpins()
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

            // Matching Python: test_near = [0.0, 1.0, 5.0, 10.0, 50.0]
            double[] testNear = { 0.0, 1.0, 5.0, 10.0, 50.0 };
            foreach (double nearVal in testNear)
            {
                // Matching Python: editor.ui.fogNearSpin.setValue(near_val)
                if (editor.FogNearSpin != null)
                {
                    editor.FogNearSpin.Value = (decimal)nearVal;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert abs(modified_are.fog_near - near_val) < 0.001
                System.Math.Abs(modifiedAre.FogNear - (float)nearVal).Should().BeLessThan(0.001f);
            }

            // Matching Python: test_far = [10.0, 50.0, 100.0, 500.0, 1000.0]
            double[] testFar = { 10.0, 50.0, 100.0, 500.0, 1000.0 };
            foreach (double farVal in testFar)
            {
                // Matching Python: editor.ui.fogFarSpin.setValue(far_val)
                if (editor.FogFarSpin != null)
                {
                    editor.FogFarSpin.Value = (decimal)farVal;
                }

                // Matching Python: data, _ = editor.build()
                var (data, _) = editor.Build();

                // Matching Python: modified_are = read_are(data)
                var modifiedAre = AREHelpers.ReadAre(data);

                // Matching Python: assert abs(modified_are.fog_far - far_val) < 0.001
                System.Math.Abs(modifiedAre.FogFar - (float)farVal).Should().BeLessThan(0.001f);
            }
        }

        // TODO: STUB - Implement test_are_editor_manipulate_sun_colors (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:543-574)
        // Original: def test_are_editor_manipulate_sun_colors(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating sun ambient, diffuse, and dynamic light colors.
        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:543-574
        // Original: def test_are_editor_manipulate_sun_colors(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating sun color fields (ambient, diffuse, dynamic light).
        [Fact]
        public void TestAreEditorManipulateSunColors()
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Get test files directory
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

            var editor = new AREEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Test SunAmbient color
            // Matching Python: sun_ambient = Color(0.8, 0.6, 0.4)
            var sunAmbientColor = new Color(0.8f, 0.6f, 0.4f);
            if (editor.AmbientColorEdit != null)
            {
                editor.AmbientColorEdit.SetColor(sunAmbientColor);
            }
            var (data, _) = editor.Build();
            var modifiedAre = AREHelpers.ReadAre(data);
            System.Math.Abs(modifiedAre.SunAmbient.R - sunAmbientColor.R).Should().BeLessThan(0.01f);
            System.Math.Abs(modifiedAre.SunAmbient.G - sunAmbientColor.G).Should().BeLessThan(0.01f);
            System.Math.Abs(modifiedAre.SunAmbient.B - sunAmbientColor.B).Should().BeLessThan(0.01f);

            // Test SunDiffuse color
            // Matching Python: sun_diffuse = Color(0.9, 0.7, 0.5)
            var sunDiffuseColor = new Color(0.9f, 0.7f, 0.5f);
            if (editor.DiffuseColorEdit != null)
            {
                editor.DiffuseColorEdit.SetColor(sunDiffuseColor);
            }
            (data, _) = editor.Build();
            modifiedAre = AREHelpers.ReadAre(data);
            System.Math.Abs(modifiedAre.SunDiffuse.R - sunDiffuseColor.R).Should().BeLessThan(0.01f);
            System.Math.Abs(modifiedAre.SunDiffuse.G - sunDiffuseColor.G).Should().BeLessThan(0.01f);
            System.Math.Abs(modifiedAre.SunDiffuse.B - sunDiffuseColor.B).Should().BeLessThan(0.01f);

            // Test DynamicLight color
            // Matching Python: dynamic_light = Color(0.2, 0.3, 0.4)
            var dynamicLightColor = new Color(0.2f, 0.3f, 0.4f);
            if (editor.DynamicColorEdit != null)
            {
                editor.DynamicColorEdit.SetColor(dynamicLightColor);
            }
            (data, _) = editor.Build();
            modifiedAre = AREHelpers.ReadAre(data);
            System.Math.Abs(modifiedAre.DynamicLight.R - dynamicLightColor.R).Should().BeLessThan(0.01f);
            System.Math.Abs(modifiedAre.DynamicLight.G - dynamicLightColor.G).Should().BeLessThan(0.01f);
            System.Math.Abs(modifiedAre.DynamicLight.B - dynamicLightColor.B).Should().BeLessThan(0.01f);
        }

        // TODO: STUB - Implement test_are_editor_manipulate_wind_power (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:576-596)
        // Original: def test_are_editor_manipulate_wind_power(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating wind power combo box.
        [Fact]
        public void TestAreEditorManipulateWindPower()
        {
            // TODO: STUB - Implement wind power combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:576-596
            throw new NotImplementedException("TestAreEditorManipulateWindPower: Wind power manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_weather_checkboxes (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:598-652)
        // Original: def test_are_editor_manipulate_weather_checkboxes(qtbot: QtBot, tsl_installation: HTInstallation, test_files_dir: Path): Test manipulating rain, snow, and lightning checkboxes (TSL-only).
        [Fact]
        public void TestAreEditorManipulateWeatherCheckboxes()
        {
            // TODO: STUB - Implement weather checkbox manipulation tests (rain, snow, lightning - TSL only)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:598-652
            throw new NotImplementedException("TestAreEditorManipulateWeatherCheckboxes: Weather checkbox manipulation tests not yet implemented (TSL-only features)");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_shadows_checkbox (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:654-675)
        // Original: def test_are_editor_manipulate_shadows_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating shadows checkbox.
        [Fact]
        public void TestAreEditorManipulateShadowsCheckbox()
        {
            // TODO: STUB - Implement shadows checkbox manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:654-675
            throw new NotImplementedException("TestAreEditorManipulateShadowsCheckbox: Shadows checkbox manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_shadow_opacity_spin (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:677-697)
        // Original: def test_are_editor_manipulate_shadow_opacity_spin(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating shadow opacity spin box.
        [Fact]
        public void TestAreEditorManipulateShadowOpacitySpin()
        {
            // TODO: STUB - Implement shadow opacity spin box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:677-697
            throw new NotImplementedException("TestAreEditorManipulateShadowOpacitySpin: Shadow opacity spin box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_grass_texture (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:703-723)
        // Original: def test_are_editor_manipulate_grass_texture(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating grass texture field.
        [Fact]
        public void TestAreEditorManipulateGrassTexture()
        {
            // TODO: STUB - Implement grass texture field manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:703-723
            throw new NotImplementedException("TestAreEditorManipulateGrassTexture: Grass texture manipulation test not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:725-757
        // Original: def test_are_editor_manipulate_grass_colors(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating grass color fields.
        [Fact]
        public void TestAreEditorManipulateGrassColors()
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

            // Test grass diffuse color
            // Matching Python: diffuse_color = Color(0.3, 0.5, 0.2)
            var diffuseColor = new Color(0.3f, 0.5f, 0.2f);
            // Matching Python: editor.ui.grassDiffuseEdit.set_color(diffuse_color)
            editor.GrassDiffuseEdit.SetColor(diffuseColor);
            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();
            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);
            // Matching Python: assert abs(modified_are.grass_diffuse.r - diffuse_color.r) < 0.01
            System.Math.Abs(modifiedAre.GrassDiffuse.R - diffuseColor.R).Should().BeLessThan(0.01f);

            // Test grass ambient color
            // Matching Python: ambient_color = Color(0.1, 0.1, 0.1)
            var ambientColor = new Color(0.1f, 0.1f, 0.1f);
            // Matching Python: editor.ui.grassAmbientEdit.set_color(ambient_color)
            editor.GrassAmbientEdit.SetColor(ambientColor);
            // Matching Python: data, _ = editor.build()
            (data, _) = editor.Build();
            // Matching Python: modified_are = read_are(data)
            modifiedAre = AREHelpers.ReadAre(data);
            // Matching Python: assert abs(modified_are.grass_ambient.r - ambient_color.r) < 0.01
            System.Math.Abs(modifiedAre.GrassAmbient.R - ambientColor.R).Should().BeLessThan(0.01f);

            // Test grass emissive color (TSL only)
            // Matching Python: if installation.tsl:
            if (installation != null && installation.Tsl)
            {
                // Matching Python: emissive_color = Color(0.0, 0.0, 0.0)
                var emissiveColor = new Color(0.0f, 0.0f, 0.0f);
                // Matching Python: editor.ui.grassEmissiveEdit.set_color(emissive_color)
                editor.GrassEmissiveEdit.SetColor(emissiveColor);
                // Matching Python: data, _ = editor.build()
                (data, _) = editor.Build();
                // Matching Python: modified_are = read_are(data)
                modifiedAre = AREHelpers.ReadAre(data);
                // Matching Python: assert abs(modified_are.grass_emissive.r - emissive_color.r) < 0.01
                System.Math.Abs(modifiedAre.GrassEmissive.R - emissiveColor.R).Should().BeLessThan(0.01f);
            }
        }

        // TODO: STUB - Implement test_are_editor_manipulate_grass_density_size_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:759-785)
        // Original: def test_are_editor_manipulate_grass_density_size_spins(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating grass density and size spin boxes.
        [Fact]
        public void TestAreEditorManipulateGrassDensitySizeSpins()
        {
            // TODO: STUB - Implement grass density and size spin boxes manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:759-785
            throw new NotImplementedException("TestAreEditorManipulateGrassDensitySizeSpins: Grass density/size spin boxes manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_grass_probability_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:787-824)
        // Original: def test_are_editor_manipulate_grass_probability_spins(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating grass probability spin boxes (LL, LR, UL, UR).
        [Fact]
        public void TestAreEditorManipulateGrassProbabilitySpins()
        {
            // TODO: STUB - Implement grass probability spin boxes manipulation test (LL, LR, UL, UR)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:787-824
            throw new NotImplementedException("TestAreEditorManipulateGrassProbabilitySpins: Grass probability spin boxes manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_dirt_colors (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:826-855)
        // Original: def test_are_editor_manipulate_dirt_colors(qtbot: QtBot, tsl_installation: HTInstallation, test_files_dir: Path): Test manipulating dirt color fields (TSL only).
        [Fact]
        public void TestAreEditorManipulateDirtColors()
        {
            // TODO: STUB - Implement dirt color fields manipulation test (TSL only - dirtColor1Edit, dirtColor2Edit, dirtColor3Edit)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:826-855
            throw new NotImplementedException("TestAreEditorManipulateDirtColors: Dirt color manipulation test not yet implemented (TSL-only features)");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_dirt_formula_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:857-885)
        // Original: def test_are_editor_manipulate_dirt_formula_spins(qtbot: QtBot, tsl_installation: HTInstallation, test_files_dir: Path): Test manipulating dirt formula spin boxes (TSL only).
        [Fact]
        public void TestAreEditorManipulateDirtFormulaSpins()
        {
            // TODO: STUB - Implement dirt formula spin boxes manipulation test (TSL only - dirtFormula1Spin, dirtFormula2Spin, dirtFormula3Spin)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:857-885
            throw new NotImplementedException("TestAreEditorManipulateDirtFormulaSpins: Dirt formula spin boxes manipulation test not yet implemented (TSL-only features)");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_dirt_function_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:887-915)
        // Original: def test_are_editor_manipulate_dirt_function_spins(qtbot: QtBot, tsl_installation: HTInstallation, test_files_dir: Path): Test manipulating dirt function spin boxes (TSL only).
        [Fact]
        public void TestAreEditorManipulateDirtFunctionSpins()
        {
            // TODO: STUB - Implement dirt function spin boxes manipulation test (TSL only - dirtFunction1Spin, dirtFunction2Spin, dirtFunction3Spin)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:887-915
            throw new NotImplementedException("TestAreEditorManipulateDirtFunctionSpins: Dirt function spin boxes manipulation test not yet implemented (TSL-only features)");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_dirt_size_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:917-945)
        // Original: def test_are_editor_manipulate_dirt_size_spins(qtbot: QtBot, tsl_installation: HTInstallation, test_files_dir: Path): Test manipulating dirt size spin boxes (TSL only).
        [Fact]
        public void TestAreEditorManipulateDirtSizeSpins()
        {
            // TODO: STUB - Implement dirt size spin boxes manipulation test (TSL only - dirtSize1Spin, dirtSize2Spin, dirtSize3Spin)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:917-945
            throw new NotImplementedException("TestAreEditorManipulateDirtSizeSpins: Dirt size spin boxes manipulation test not yet implemented (TSL-only features)");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:951-973
        // Original: def test_are_editor_manipulate_on_enter_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating on enter script field.
        [Fact]
        public void TestAreEditorManipulateOnEnterScript()
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

            // Matching Python: editor.ui.onEnterSelect.set_combo_box_text("test_on_enter")
            if (editor.OnEnterSelect != null)
            {
                editor.OnEnterSelect.Text = "test_on_enter";
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert str(modified_are.on_enter) == "test_on_enter"
            modifiedAre.OnEnter.ToString().Should().Be("test_on_enter");

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data)
            editor.Load(areFile, "tat001", ResourceType.ARE, data);

            // Matching Python: assert editor.ui.onEnterSelect.currentText() == "test_on_enter"
            if (editor.OnEnterSelect != null)
            {
                editor.OnEnterSelect.Text.Should().Be("test_on_enter");
            }
        }

        // TODO: STUB - Implement test_are_editor_manipulate_on_exit_script (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:975-993)
        // Original: def test_are_editor_manipulate_on_exit_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating on exit script field.
        [Fact]
        public void TestAreEditorManipulateOnExitScript()
        {
            // TODO: STUB - Implement on exit script field manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:975-993
            throw new NotImplementedException("TestAreEditorManipulateOnExitScript: On exit script manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_on_heartbeat_script (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:995-1013)
        // Original: def test_are_editor_manipulate_on_heartbeat_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating on heartbeat script field.
        [Fact]
        public void TestAreEditorManipulateOnHeartbeatScript()
        {
            // TODO: STUB - Implement on heartbeat script field manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:995-1013
            throw new NotImplementedException("TestAreEditorManipulateOnHeartbeatScript: On heartbeat script manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_on_user_defined_script (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1015-1033)
        // Original: def test_are_editor_manipulate_on_user_defined_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating on user defined script field.
        [Fact]
        public void TestAreEditorManipulateOnUserDefinedScript()
        {
            // TODO: STUB - Implement on user defined script field manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1015-1033
            throw new NotImplementedException("TestAreEditorManipulateOnUserDefinedScript: On user defined script manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_comments (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1039-1070)
        // Original: def test_are_editor_manipulate_comments(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating comments field.
        [Fact]
        public void TestAreEditorManipulateComments()
        {
            // TODO: STUB - Implement comments field manipulation test (empty, single line, multi-line, special chars, very long)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1039-1070
            throw new NotImplementedException("TestAreEditorManipulateComments: Comments field manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_all_basic_fields_combination (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1076-1113)
        // Original: def test_are_editor_manipulate_all_basic_fields_combination(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating all basic fields simultaneously.
        [Fact]
        public void TestAreEditorManipulateAllBasicFieldsCombination()
        {
            // TODO: STUB - Implement combination test for all basic fields manipulated simultaneously
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1076-1113
            throw new NotImplementedException("TestAreEditorManipulateAllBasicFieldsCombination: All basic fields combination test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_all_weather_fields_combination (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1115-1163)
        // Original: def test_are_editor_manipulate_all_weather_fields_combination(qtbot: QtBot, tsl_installation: HTInstallation, test_files_dir: Path): Test manipulating all weather fields simultaneously (TSL only).
        [Fact]
        public void TestAreEditorManipulateAllWeatherFieldsCombination()
        {
            // TODO: STUB - Implement combination test for all weather fields manipulated simultaneously (TSL only)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1115-1163
            throw new NotImplementedException("TestAreEditorManipulateAllWeatherFieldsCombination: All weather fields combination test not yet implemented (TSL-only features)");
        }

        // TODO: STUB - Implement test_are_editor_manipulate_all_map_fields_combination (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1165-1204)
        // Original: def test_are_editor_manipulate_all_map_fields_combination(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test manipulating all map fields simultaneously.
        [Fact]
        public void TestAreEditorManipulateAllMapFieldsCombination()
        {
            // TODO: STUB - Implement combination test for all map fields manipulated simultaneously
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1165-1204
            throw new NotImplementedException("TestAreEditorManipulateAllMapFieldsCombination: All map fields combination test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_save_load_roundtrip_identity (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1210-1242)
        // Original: def test_are_editor_save_load_roundtrip_identity(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test that save/load roundtrip preserves all data exactly.
        [Fact]
        public void TestAreEditorSaveLoadRoundtripIdentity()
        {
            // TODO: STUB - Implement save/load roundtrip identity test (preserves all data exactly without modifications)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1210-1242
            throw new NotImplementedException("TestAreEditorSaveLoadRoundtripIdentity: Save/load roundtrip identity test not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1244-1288
        // Original: def test_are_editor_save_load_roundtrip_with_modifications(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test save/load roundtrip with modifications preserves changes.
        [Fact]
        public void TestAreEditorSaveLoadRoundtripWithModifications()
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

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Get test files directory
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

            // Matching Python: are_file = test_files_dir / "tat001.are"
            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: Make modifications
            // Matching Python: editor.ui.tagEdit.setText("modified_roundtrip")
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text = "modified_roundtrip";
            }
            // Matching Python: editor.ui.alphaTestSpin.setValue(200)
            if (editor.AlphaTestSpin != null)
            {
                editor.AlphaTestSpin.Value = 200;
            }
            // Matching Python: editor.ui.fogEnabledCheck.setChecked(True)
            if (editor.FogEnabledCheck != null)
            {
                editor.FogEnabledCheck.IsChecked = true;
            }
            // Matching Python: editor.ui.fogNearSpin.setValue(10.0)
            if (editor.FogNearSpin != null)
            {
                editor.FogNearSpin.Value = 10.0M;
            }
            // Matching Python: editor.ui.fogFarSpin.setValue(200.0)
            if (editor.FogFarSpin != null)
            {
                editor.FogFarSpin.Value = 200.0M;
            }
            // Matching Python: editor.ui.commentsEdit.setPlainText("Roundtrip test comment")
            if (editor.CommentsEdit != null)
            {
                editor.CommentsEdit.Text = "Roundtrip test comment";
            }

            // Matching Python: Save
            // Matching Python: data1, _ = editor.build()
            var (data1, _) = editor.Build();
            // Matching Python: saved_are1 = read_are(data1)
            var savedAre1 = AREHelpers.ReadAre(data1);

            // Matching Python: Load saved data
            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, data1)
            editor.Load(areFile, "tat001", ResourceType.ARE, data1);

            // Matching Python: Verify modifications preserved
            // Matching Python: assert editor.ui.tagEdit.text() == "modified_roundtrip"
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text.Should().Be("modified_roundtrip");
            }
            // Matching Python: assert editor.ui.alphaTestSpin.value() == 200
            if (editor.AlphaTestSpin != null)
            {
                editor.AlphaTestSpin.Value.Should().Be(200);
            }
            // Matching Python: assert editor.ui.fogEnabledCheck.isChecked()
            if (editor.FogEnabledCheck != null)
            {
                editor.FogEnabledCheck.IsChecked.Should().BeTrue();
            }
            // Matching Python: assert editor.ui.fogNearSpin.value() == 10.0
            if (editor.FogNearSpin != null)
            {
                System.Math.Abs((double)(editor.FogNearSpin.Value ?? 0) - 10.0).Should().BeLessThan(0.001);
            }
            // Matching Python: assert editor.ui.fogFarSpin.value() == 200.0
            if (editor.FogFarSpin != null)
            {
                System.Math.Abs((double)(editor.FogFarSpin.Value ?? 0) - 200.0).Should().BeLessThan(0.001);
            }
            // Matching Python: assert editor.ui.commentsEdit.toPlainText() == "Roundtrip test comment"
            if (editor.CommentsEdit != null)
            {
                editor.CommentsEdit.Text.Should().Be("Roundtrip test comment");
            }

            // Matching Python: Save again
            // Matching Python: data2, _ = editor.build()
            var (data2, _) = editor.Build();
            // Matching Python: saved_are2 = read_are(data2)
            var savedAre2 = AREHelpers.ReadAre(data2);

            // Matching Python: Verify second save matches first
            // Matching Python: assert saved_are2.tag == saved_are1.tag
            savedAre2.Tag.Should().Be(savedAre1.Tag);
            // Matching Python: assert saved_are2.alpha_test == saved_are1.alpha_test
            savedAre2.AlphaTest.Should().Be(savedAre1.AlphaTest);
            // Matching Python: assert saved_are2.fog_enabled == saved_are1.fog_enabled
            savedAre2.FogEnabled.Should().Be(savedAre1.FogEnabled);
            // Matching Python: assert saved_are2.comment == saved_are1.comment
            savedAre2.Comment.Should().Be(savedAre1.Comment);
        }

        // TODO: STUB - Implement test_are_editor_multiple_save_load_cycles (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1290-1321)
        // Original: def test_are_editor_multiple_save_load_cycles(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test multiple save/load cycles preserve data correctly.
        [Fact]
        public void TestAreEditorMultipleSaveLoadCycles()
        {
            // TODO: STUB - Implement multiple save/load cycles test (5 cycles with different modifications each time)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1290-1321
            throw new NotImplementedException("TestAreEditorMultipleSaveLoadCycles: Multiple save/load cycles test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_minimum_values (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1327-1364)
        // Original: def test_are_editor_minimum_values(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test setting all fields to minimum values.
        [Fact]
        public void TestAreEditorMinimumValues()
        {
            // TODO: STUB - Implement minimum values edge case test (all fields set to minimums)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1327-1364
            throw new NotImplementedException("TestAreEditorMinimumValues: Minimum values edge case test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_maximum_values (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1366-1397)
        // Original: def test_are_editor_maximum_values(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test setting all fields to maximum values.
        [Fact]
        public void TestAreEditorMaximumValues()
        {
            // TODO: STUB - Implement maximum values edge case test (all fields set to maximums)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1366-1397
            throw new NotImplementedException("TestAreEditorMaximumValues: Maximum values edge case test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_empty_strings (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1399-1432)
        // Original: def test_are_editor_empty_strings(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test handling of empty strings in text fields.
        [Fact]
        public void TestAreEditorEmptyStrings()
        {
            // TODO: STUB - Implement empty strings edge case test (all text fields set to empty)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1399-1432
            throw new NotImplementedException("TestAreEditorEmptyStrings: Empty strings edge case test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_special_characters_in_text_fields (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1434-1458)
        // Original: def test_are_editor_special_characters_in_text_fields(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test handling of special characters in text fields.
        [Fact]
        public void TestAreEditorSpecialCharactersInTextFields()
        {
            // TODO: STUB - Implement special characters edge case test (newlines, tabs, etc. in text fields)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1434-1458
            throw new NotImplementedException("TestAreEditorSpecialCharactersInTextFields: Special characters edge case test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_gff_roundtrip_comparison (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1464-1497)
        // Original: def test_are_editor_gff_roundtrip_comparison(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test GFF roundtrip comparison like resource tests.
        [Fact]
        public void TestAreEditorGffRoundtripComparison()
        {
            // TODO: STUB - Implement GFF roundtrip comparison test (validates GFF structure preservation)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1464-1497
            throw new NotImplementedException("TestAreEditorGffRoundtripComparison: GFF roundtrip comparison test not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1499-1527
        // Original: def test_are_editor_gff_roundtrip_with_modifications(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test GFF roundtrip with modifications still produces valid GFF.
        [Fact]
        public void TestAreEditorGffRoundtripWithModifications()
        {
            // Get test files directory
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
                return; // Skip if test file not available (matching Python pytest.skip behavior)
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

            // Matching Python: editor = AREEditor(None, installation)
            var editor = new AREEditor(null, installation);

            // Matching Python: original_data = are_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(areFile);

            // Matching Python: editor.load(are_file, "tat001", ResourceType.ARE, original_data)
            editor.Load(areFile, "tat001", ResourceType.ARE, originalData);

            // Matching Python: editor.ui.tagEdit.setText("modified_gff_test")
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text = "modified_gff_test";
            }

            // Matching Python: editor.ui.alphaTestSpin.setValue(150)
            if (editor.AlphaTestSpin != null)
            {
                editor.AlphaTestSpin.Value = 150;
            }

            // Matching Python: editor.ui.fogEnabledCheck.setChecked(True)
            if (editor.FogEnabledCheck != null)
            {
                editor.FogEnabledCheck.IsChecked = true;
            }

            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: new_gff = read_gff(data)
            // Verify it's valid GFF
            GFF newGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
            newGff.Should().NotBeNull();

            // Matching Python: modified_are = read_are(data)
            var modifiedAre = AREHelpers.ReadAre(data);

            // Matching Python: assert modified_are.tag == "modified_gff_test"
            modifiedAre.Tag.Should().Be("modified_gff_test");

            // Matching Python: assert modified_are.alpha_test == 150
            modifiedAre.AlphaTest.Should().Be(150);

            // Matching Python: assert modified_are.fog_enabled
            modifiedAre.FogEnabled.Should().BeTrue();
        }

        // TODO: STUB - Implement test_are_editor_new_file_all_defaults (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1562-1578)
        // Original: def test_are_editor_new_file_all_defaults(qtbot: QtBot, installation: HTInstallation): Test new file has correct defaults.
        [Fact]
        public void TestAreEditorNewFileAllDefaults()
        {
            // TODO: STUB - Implement new file defaults test (verifies correct defaults for new ARE file)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1562-1578
            throw new NotImplementedException("TestAreEditorNewFileAllDefaults: New file defaults test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_minimap_redo_on_map_axis_change (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1584-1600)
        // Original: def test_are_editor_minimap_redo_on_map_axis_change(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test that minimap redoes when map axis changes.
        [Fact]
        public void TestAreEditorMinimapRedoOnMapAxisChange()
        {
            // TODO: STUB - Implement minimap redo on map axis change test (verifies signal connection)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1584-1600
            throw new NotImplementedException("TestAreEditorMinimapRedoOnMapAxisChange: Minimap redo on map axis change test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_minimap_redo_on_map_world_change (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1602-1617)
        // Original: def test_are_editor_minimap_redo_on_map_world_change(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test that minimap redoes when map world coordinates change.
        [Fact]
        public void TestAreEditorMinimapRedoOnMapWorldChange()
        {
            // TODO: STUB - Implement minimap redo on map world change test (verifies signal connection)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1602-1617
            throw new NotImplementedException("TestAreEditorMinimapRedoOnMapWorldChange: Minimap redo on map world change test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_name_dialog_integration (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1623-1638)
        // Original: def test_are_editor_name_dialog_integration(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test name dialog integration.
        [Fact]
        public void TestAreEditorNameDialogIntegration()
        {
            // TODO: STUB - Implement name dialog integration test (verifies change_name method exists and is callable)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1623-1638
            throw new NotImplementedException("TestAreEditorNameDialogIntegration: Name dialog integration test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_specifics (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1645-1666)
        // Original: def test_are_editor_specifics(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Specific granular tests for ARE Editor.
        [Fact]
        public void TestAreEditorSpecifics()
        {
            // TODO: STUB - Implement ARE editor specifics test (granular tag manipulation test)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1645-1666
            throw new NotImplementedException("TestAreEditorSpecifics: ARE editor specifics test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_help_dialog_opens_correct_file (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1669-1695)
        // Original: def test_are_editor_help_dialog_opens_correct_file(qtbot: QtBot, installation: HTInstallation): Test that AREEditor help dialog opens and displays the correct help file.
        [Fact]
        public void TestAreEditorHelpDialogOpensCorrectFile()
        {
            // TODO: STUB - Implement help dialog test (verifies GFF-ARE.md help file opens correctly, not 'Help File Not Found')
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1669-1695
            throw new NotImplementedException("TestAreEditorHelpDialogOpensCorrectFile: Help dialog test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_comprehensive_gff_roundtrip (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1702-1803)
        // Original: def test_are_editor_comprehensive_gff_roundtrip(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Comprehensive test that validates ALL GFF fields are preserved through editor roundtrip.
        [Fact]
        public void TestAreEditorComprehensiveGffRoundtrip()
        {
            // TODO: STUB - Implement comprehensive GFF roundtrip test (validates ALL GFF fields preserved, compares every field recursively)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1702-1803
            throw new NotImplementedException("TestAreEditorComprehensiveGffRoundtrip: Comprehensive GFF roundtrip test not yet implemented");
        }

        // TODO: STUB - Implement test_are_editor_map_coordinates_roundtrip (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1806-1898)
        // Original: def test_are_editor_map_coordinates_roundtrip(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path): Test that map coordinates are preserved exactly through roundtrip.
        [Fact]
        public void TestAreEditorMapCoordinatesRoundtrip()
        {
            // TODO: STUB - Implement map coordinates roundtrip test (validates MapPt1X, MapPt1Y, MapPt2X, MapPt2Y, WorldPt1X, WorldPt1Y, WorldPt2X, WorldPt2Y, NorthAxis, MapZoom, MapResX preserved exactly)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_are_editor.py:1806-1898
            throw new NotImplementedException("TestAreEditorMapCoordinatesRoundtrip: Map coordinates roundtrip test not yet implemented");
        }

        // Comprehensive tests for AREHelpers with all game types
        // Testing game-specific field handling for Odyssey (K1/K2), Aurora (NWN), and Eclipse (DA/ME)

        [Theory]
        [InlineData(Game.K1)]
        [InlineData(Game.K2)]
        [InlineData(Game.K1_XBOX)]
        [InlineData(Game.K2_XBOX)]
        [InlineData(Game.K1_IOS)]
        [InlineData(Game.K2_IOS)]
        [InlineData(Game.K1_ANDROID)]
        [InlineData(Game.K2_ANDROID)]
        [InlineData(Game.NWN)]
        [InlineData(Game.NWN2)]
        [InlineData(Game.DA)]
        [InlineData(Game.DA2)]
        [InlineData(Game.ME)]
        [InlineData(Game.ME2)]
        [InlineData(Game.ME3)]
        public void TestAreHelpersColorFieldsRoundtripForAllGameTypes(Game game)
        {
            // Test that all color fields (SunAmbient, SunDiffuse, DynamicLight, FogColor, GrassAmbient, GrassDiffuse)
            // are correctly written and read for ALL game types
            var are = new ARE();
            
            // Set all color fields
            are.SunAmbient = new Color(0.8f, 0.6f, 0.4f);
            are.SunDiffuse = new Color(0.9f, 0.7f, 0.5f);
            are.DynamicLight = new Color(0.2f, 0.3f, 0.4f);
            are.FogColor = new Color(0.1f, 0.2f, 0.3f);
            are.GrassAmbient = new Color(0.4f, 0.5f, 0.6f);
            are.GrassDiffuse = new Color(0.5f, 0.6f, 0.7f);
            are.GrassEmissive = new Color(0.0f, 0.0f, 0.0f); // Will only be written for K2

            // Dismantle ARE with specific game type
            var bytes = AREHelpers.BytesAre(are, game);
            
            // Reconstruct ARE
            var reconstructedAre = AREHelpers.ReadAre(bytes);
            
            // Verify all color fields are preserved (written for ALL game types)
            System.Math.Abs(reconstructedAre.SunAmbient.R - are.SunAmbient.R).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.SunAmbient.G - are.SunAmbient.G).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.SunAmbient.B - are.SunAmbient.B).Should().BeLessThan(0.01f);
            
            System.Math.Abs(reconstructedAre.SunDiffuse.R - are.SunDiffuse.R).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.SunDiffuse.G - are.SunDiffuse.G).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.SunDiffuse.B - are.SunDiffuse.B).Should().BeLessThan(0.01f);
            
            System.Math.Abs(reconstructedAre.DynamicLight.R - are.DynamicLight.R).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.DynamicLight.G - are.DynamicLight.G).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.DynamicLight.B - are.DynamicLight.B).Should().BeLessThan(0.01f);
            
            System.Math.Abs(reconstructedAre.FogColor.R - are.FogColor.R).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.FogColor.G - are.FogColor.G).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.FogColor.B - are.FogColor.B).Should().BeLessThan(0.01f);
            
            System.Math.Abs(reconstructedAre.GrassAmbient.R - are.GrassAmbient.R).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.GrassAmbient.G - are.GrassAmbient.G).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.GrassAmbient.B - are.GrassAmbient.B).Should().BeLessThan(0.01f);
            
            System.Math.Abs(reconstructedAre.GrassDiffuse.R - are.GrassDiffuse.R).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.GrassDiffuse.G - are.GrassDiffuse.G).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.GrassDiffuse.B - are.GrassDiffuse.B).Should().BeLessThan(0.01f);
        }

        [Theory]
        [InlineData(Game.K1)]
        [InlineData(Game.K1_XBOX)]
        [InlineData(Game.K1_IOS)]
        [InlineData(Game.K1_ANDROID)]
        [InlineData(Game.NWN)]
        [InlineData(Game.NWN2)]
        [InlineData(Game.DA)]
        [InlineData(Game.DA2)]
        [InlineData(Game.ME)]
        [InlineData(Game.ME2)]
        [InlineData(Game.ME3)]
        public void TestAreHelpersK2SpecificFieldsNotWrittenForNonK2Games(Game game)
        {
            // Test that Grass_Emissive is NOT written for non-K2 games
            var are = new ARE();
            are.GrassEmissive = new Color(1.0f, 1.0f, 1.0f); // Set to non-default value
            
            // Dismantle ARE with non-K2 game type
            var bytes = AREHelpers.BytesAre(are, game);
            
            // Reconstruct ARE
            var reconstructedAre = AREHelpers.ReadAre(bytes);
            
            // For non-K2 games, GrassEmissive should be default (0,0,0) because it wasn't written
            // Note: This test verifies that K2-specific fields are conditionally written
            // The field may still be read as 0 if not present in the GFF
            if (!game.IsK2())
            {
                // GrassEmissive should not be written for non-K2 games
                // When reading back, it will be 0 if not present
                reconstructedAre.GrassEmissive.R.Should().Be(0.0f);
                reconstructedAre.GrassEmissive.G.Should().Be(0.0f);
                reconstructedAre.GrassEmissive.B.Should().Be(0.0f);
            }
        }

        [Theory]
        [InlineData(Game.K2)]
        [InlineData(Game.K2_XBOX)]
        [InlineData(Game.K2_IOS)]
        [InlineData(Game.K2_ANDROID)]
        public void TestAreHelpersK2SpecificFieldsWrittenForK2Games(Game game)
        {
            // Test that Grass_Emissive IS written for K2 games
            var are = new ARE();
            are.GrassEmissive = new Color(0.7f, 0.8f, 0.9f);
            
            // Dismantle ARE with K2 game type
            var bytes = AREHelpers.BytesAre(are, game);
            
            // Reconstruct ARE
            var reconstructedAre = AREHelpers.ReadAre(bytes);
            
            // For K2 games, GrassEmissive should be preserved
            System.Math.Abs(reconstructedAre.GrassEmissive.R - are.GrassEmissive.R).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.GrassEmissive.G - are.GrassEmissive.G).Should().BeLessThan(0.01f);
            System.Math.Abs(reconstructedAre.GrassEmissive.B - are.GrassEmissive.B).Should().BeLessThan(0.01f);
        }

        [Fact]
        public void TestAreHelpersComprehensiveRoundtripAllGameTypes()
        {
            // Comprehensive roundtrip test for all game types with all basic fields
            var gameTypes = new[]
            {
                Game.K1, Game.K2, Game.K1_XBOX, Game.K2_XBOX,
                Game.K1_IOS, Game.K2_IOS, Game.K1_ANDROID, Game.K2_ANDROID,
                Game.NWN, Game.NWN2,
                Game.DA, Game.DA2,
                Game.ME, Game.ME2, Game.ME3
            };

            foreach (var game in gameTypes)
            {
                var are = new ARE();
                
                // Set all basic fields
                are.Tag = "test_tag_" + game.ToString();
                are.Name = LocalizedString.FromEnglish("Test Area " + game.ToString());
                are.AlphaTest = 100;
                are.CameraStyle = 1;
                are.DefaultEnvMap = new ResRef("envmap");
                are.Unescapable = true;
                are.DisableTransit = false;
                are.StealthXp = true;
                are.StealthXpMax = 500;
                are.StealthXpLoss = 10;
                are.GrassTexture = new ResRef("grass");
                are.GrassDensity = 0.5f;
                are.GrassSize = 1.0f;
                are.GrassProbLL = 0.25f;
                are.GrassProbLR = 0.25f;
                are.GrassProbUL = 0.25f;
                are.GrassProbUR = 0.25f;
                are.FogEnabled = true;
                are.FogNear = 10.0f;
                are.FogFar = 200.0f;
                are.WindPower = 1;
                are.ShadowOpacity = new ResRef("shadow");
                are.OnEnter = new ResRef("onenter");
                are.OnExit = new ResRef("onexit");
                are.OnHeartbeat = new ResRef("onheartbeat");
                are.OnUserDefined = new ResRef("onuserdefined");
                are.LoadScreenID = 5;
                are.Comment = "Test comment for " + game.ToString();
                
                // Set all color fields
                are.SunAmbient = new Color(0.8f, 0.6f, 0.4f);
                are.SunDiffuse = new Color(0.9f, 0.7f, 0.5f);
                are.DynamicLight = new Color(0.2f, 0.3f, 0.4f);
                are.FogColor = new Color(0.1f, 0.2f, 0.3f);
                are.GrassAmbient = new Color(0.4f, 0.5f, 0.6f);
                are.GrassDiffuse = new Color(0.5f, 0.6f, 0.7f);
                are.GrassEmissive = new Color(0.0f, 0.0f, 0.0f);
                
                // Map fields
                are.NorthAxis = ARENorthAxis.PositiveY;
                are.MapZoom = 2;
                are.MapResX = 512;
                are.MapPoint1 = new System.Numerics.Vector2(0.1f, 0.2f);
                are.MapPoint2 = new System.Numerics.Vector2(0.9f, 0.8f);
                are.WorldPoint1 = new System.Numerics.Vector2(100.0f, 200.0f);
                are.WorldPoint2 = new System.Numerics.Vector2(300.0f, 400.0f);
                
                // Roundtrip
                var bytes = AREHelpers.BytesAre(are, game);
                var reconstructedAre = AREHelpers.ReadAre(bytes);
                
                // Verify all basic fields are preserved
                reconstructedAre.Tag.Should().Be(are.Tag);
                reconstructedAre.AlphaTest.Should().Be(are.AlphaTest);
                reconstructedAre.CameraStyle.Should().Be(are.CameraStyle);
                reconstructedAre.Unescapable.Should().Be(are.Unescapable);
                reconstructedAre.DisableTransit.Should().Be(are.DisableTransit);
                reconstructedAre.StealthXp.Should().Be(are.StealthXp);
                reconstructedAre.StealthXpMax.Should().Be(are.StealthXpMax);
                reconstructedAre.StealthXpLoss.Should().Be(are.StealthXpLoss);
                reconstructedAre.FogEnabled.Should().Be(are.FogEnabled);
                System.Math.Abs(reconstructedAre.FogNear - are.FogNear).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.FogFar - are.FogFar).Should().BeLessThan(0.001f);
                reconstructedAre.WindPower.Should().Be(are.WindPower);
                reconstructedAre.LoadScreenID.Should().Be(are.LoadScreenID);
                reconstructedAre.Comment.Should().Be(are.Comment);
                
                // Verify map fields
                reconstructedAre.NorthAxis.Should().Be(are.NorthAxis);
                reconstructedAre.MapZoom.Should().Be(are.MapZoom);
                reconstructedAre.MapResX.Should().Be(are.MapResX);
                System.Math.Abs(reconstructedAre.MapPoint1.X - are.MapPoint1.X).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.MapPoint1.Y - are.MapPoint1.Y).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.MapPoint2.X - are.MapPoint2.X).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.MapPoint2.Y - are.MapPoint2.Y).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.WorldPoint1.X - are.WorldPoint1.X).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.WorldPoint1.Y - are.WorldPoint1.Y).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.WorldPoint2.X - are.WorldPoint2.X).Should().BeLessThan(0.001f);
                System.Math.Abs(reconstructedAre.WorldPoint2.Y - are.WorldPoint2.Y).Should().BeLessThan(0.001f);
            }
        }

        [Fact]
        public void TestAreHelpersEdgeCaseColors()
        {
            // Test edge cases for color fields: black, white, and extreme values
            var testCases = new[]
            {
                new { Name = "Black", Color = new Color(0.0f, 0.0f, 0.0f) },
                new { Name = "White", Color = new Color(1.0f, 1.0f, 1.0f) },
                new { Name = "Red", Color = new Color(1.0f, 0.0f, 0.0f) },
                new { Name = "Green", Color = new Color(0.0f, 1.0f, 0.0f) },
                new { Name = "Blue", Color = new Color(0.0f, 0.0f, 1.0f) },
                new { Name = "Gray", Color = new Color(0.5f, 0.5f, 0.5f) }
            };

            foreach (var testCase in testCases)
            {
                var are = new ARE();
                are.SunAmbient = testCase.Color;
                are.SunDiffuse = testCase.Color;
                are.DynamicLight = testCase.Color;
                are.FogColor = testCase.Color;
                are.GrassAmbient = testCase.Color;
                are.GrassDiffuse = testCase.Color;
                
                // Test for all game types
                foreach (var game in new[] { Game.K1, Game.K2, Game.NWN, Game.DA, Game.ME })
                {
                    var bytes = AREHelpers.BytesAre(are, game);
                    var reconstructedAre = AREHelpers.ReadAre(bytes);
                    
                    System.Math.Abs(reconstructedAre.SunAmbient.R - testCase.Color.R).Should().BeLessThan(0.01f, 
                        $"SunAmbient {testCase.Name} failed for {game}");
                    System.Math.Abs(reconstructedAre.SunDiffuse.R - testCase.Color.R).Should().BeLessThan(0.01f,
                        $"SunDiffuse {testCase.Name} failed for {game}");
                    System.Math.Abs(reconstructedAre.DynamicLight.R - testCase.Color.R).Should().BeLessThan(0.01f,
                        $"DynamicLight {testCase.Name} failed for {game}");
                    System.Math.Abs(reconstructedAre.FogColor.R - testCase.Color.R).Should().BeLessThan(0.01f,
                        $"FogColor {testCase.Name} failed for {game}");
                    System.Math.Abs(reconstructedAre.GrassAmbient.R - testCase.Color.R).Should().BeLessThan(0.01f,
                        $"GrassAmbient {testCase.Name} failed for {game}");
                    System.Math.Abs(reconstructedAre.GrassDiffuse.R - testCase.Color.R).Should().BeLessThan(0.01f,
                        $"GrassDiffuse {testCase.Name} failed for {game}");
                }
            }
        }

        [Fact]
        public void TestAreHelpersDefaultValues()
        {
            // Test that default ARE values are correctly handled
            var are = new ARE(); // All defaults
            
            foreach (var game in new[] { Game.K1, Game.K2, Game.NWN, Game.DA, Game.ME })
            {
                var bytes = AREHelpers.BytesAre(are, game);
                var reconstructedAre = AREHelpers.ReadAre(bytes);
                
                // Verify defaults are preserved
                reconstructedAre.Tag.Should().BeEmpty();
                reconstructedAre.AlphaTest.Should().Be(0);
                reconstructedAre.CameraStyle.Should().Be(0);
                reconstructedAre.Unescapable.Should().BeFalse();
                reconstructedAre.DisableTransit.Should().BeFalse();
                reconstructedAre.StealthXp.Should().BeFalse();
                reconstructedAre.FogEnabled.Should().BeFalse();
                reconstructedAre.WindPower.Should().Be(0);
                reconstructedAre.LoadScreenID.Should().Be(0);
                
                // Color defaults should be black (0,0,0)
                reconstructedAre.SunAmbient.R.Should().Be(0.0f);
                reconstructedAre.SunAmbient.G.Should().Be(0.0f);
                reconstructedAre.SunAmbient.B.Should().Be(0.0f);
                reconstructedAre.SunDiffuse.R.Should().Be(0.0f);
                reconstructedAre.SunDiffuse.G.Should().Be(0.0f);
                reconstructedAre.SunDiffuse.B.Should().Be(0.0f);
                reconstructedAre.DynamicLight.R.Should().Be(0.0f);
                reconstructedAre.DynamicLight.G.Should().Be(0.0f);
                reconstructedAre.DynamicLight.B.Should().Be(0.0f);
                reconstructedAre.FogColor.R.Should().Be(0.0f);
                reconstructedAre.FogColor.G.Should().Be(0.0f);
                reconstructedAre.FogColor.B.Should().Be(0.0f);
                reconstructedAre.GrassAmbient.R.Should().Be(0.0f);
                reconstructedAre.GrassAmbient.G.Should().Be(0.0f);
                reconstructedAre.GrassAmbient.B.Should().Be(0.0f);
                reconstructedAre.GrassDiffuse.R.Should().Be(0.0f);
                reconstructedAre.GrassDiffuse.G.Should().Be(0.0f);
                reconstructedAre.GrassDiffuse.B.Should().Be(0.0f);
            }
        }
    }
}

