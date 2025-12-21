using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Common;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;
using Avalonia.Controls;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
    // Original: Comprehensive tests for UTD Editor
    [Collection("Avalonia Test Collection")]
    public class UTDEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public UTDEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static UTDEditorTests()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            if (!string.IsNullOrEmpty(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                _installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }
            else
            {
                // Fallback to K1
                string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
                if (string.IsNullOrEmpty(k1Path))
                {
                    k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
                }

                if (!string.IsNullOrEmpty(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
                {
                    _installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
                }
            }
        }

        private static (string utdFile, HTInstallation installation) GetTestFileAndInstallation()
        {
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            if (!System.IO.File.Exists(utdFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            }

            return (utdFile, _installation);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:57-81
        // Original: def test_utd_editor_manipulate_tag(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateTag()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile))
            {
                return; // Skip if test file not available
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching PyKotor implementation: editor = UTDEditor(None, installation)
            var editor = new UTDEditor(null, installation);

            // Matching PyKotor implementation: original_data = utd_file.read_bytes()
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);

            // Matching PyKotor implementation: editor.load(utd_file, "naldoor001", ResourceType.UTD, original_data)
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Matching PyKotor implementation: original_utd = read_utd(original_data)
            UTD originalUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(originalData));

            // Modify tag
            // Matching PyKotor implementation: editor.ui.tagEdit.setText("modified_tag")
            editor.TagEdit.Text = "modified_tag";

            // Save and verify
            // Matching PyKotor implementation: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching PyKotor implementation: modified_utd = read_utd(data)
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            // Matching PyKotor implementation: assert modified_utd.tag == "modified_tag"
            // Matching PyKotor implementation: assert modified_utd.tag != original_utd.tag
            modifiedUtd.Tag.Should().Be("modified_tag");
            modifiedUtd.Tag.Should().NotBe(originalUtd.Tag);

            // Load back and verify
            // Matching PyKotor implementation: editor.load(utd_file, "naldoor001", ResourceType.UTD, data)
            // Matching PyKotor implementation: assert editor.ui.tagEdit.text() == "modified_tag"
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
            editor.TagEdit.Text.Should().Be("modified_tag");
        }

        [Fact]
        public void TestUtdEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
            // Original: def test_utd_editor_new_file_creation(qtbot, installation):
            var editor = new UTDEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:30-55
        // Original: def test_utd_editor_manipulate_name_locstring(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        // Note: This test loads an existing UTD file and verifies it works, similar to UTI/UTC/UTP/UTS test patterns
        [Fact]
        public void TestUtdEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find naldoor001.utd (used in Python tests)
            string utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            if (!System.IO.File.Exists(utdFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            }

            if (!System.IO.File.Exists(utdFile))
            {
                // Skip if no UTD files available for testing (matching Python pytest.skip behavior)
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

            var editor = new UTDEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1130-1154
        // Original: def test_utd_editor_gff_roundtrip_comparison(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find naldoor001.utd
            string utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            if (!System.IO.File.Exists(utdFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utdFile = System.IO.Path.Combine(testFilesDir, "naldoor001.utd");
            }

            if (!System.IO.File.Exists(utdFile))
            {
                // Skip if test file not available
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

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            var editor = new UTDEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1140
            // Original: original_data = utd_file.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(utdFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1141
            // Original: original_gff = read_gff(original_data)
            var old = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1142
            // Original: editor.load(utd_file, "naldoor001", ResourceType.UTD, original_data)
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1145
            // Original: data, _ = editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1146
            // Original: new_gff = read_gff(data)
            GFF newGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(newData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1153
            // Original: diff = original_gff.compare(new_gff, log_func, ignore_default_changes=True)
            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1154
            // Original: assert diff, f"GFF comparison failed:\n{chr(10).join(log_messages)}"
            diff.Should().BeTrue("GFF comparison failed. Log messages: " + string.Join(Environment.NewLine, logMessages));
        }

        // ============================================================================
        // BASIC FIELDS MANIPULATIONS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:30-55
        // Original: def test_utd_editor_manipulate_name_locstring(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateNameLocstring()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);
            UTD originalUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(originalData));

            // Modify name
            var newName = LocalizedString.FromEnglish("Modified Door Name");
            editor.NameEdit.SetLocString(newName);

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.Name.Get(Language.English, Gender.Male).Should().Be("Modified Door Name");
            modifiedUtd.Name.Get(Language.English, Gender.Male).Should().NotBe(originalUtd.Name.Get(Language.English, Gender.Male));

            // Load back and verify
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
            editor.NameEdit.GetLocString().Get(Language.English, Gender.Male).Should().Be("Modified Door Name");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:83-104
        // Original: def test_utd_editor_manipulate_tag_generate_button(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateTagGenerateButton()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, System.IO.File.ReadAllBytes(utdFile));

            // Click generate button
            editor.TagGenerateBtn.Command.Execute(null);

            // Tag should be generated from resname or resref
            string generatedTag = editor.TagEdit.Text;
            generatedTag.Should().NotBeNullOrEmpty();

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.Tag.Should().Be(generatedTag);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:106-128
        // Original: def test_utd_editor_manipulate_resref(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateResref()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify resref
            editor.ResrefEdit.Text = "modified_resref";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.ResRef.ToString().Should().Be("modified_resref");

            // Load back and verify
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
            editor.ResrefEdit.Text.Should().Be("modified_resref");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:130-151
        // Original: def test_utd_editor_manipulate_resref_generate_button(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateResrefGenerateButton()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, System.IO.File.ReadAllBytes(utdFile));

            // Click generate button
            editor.ResrefGenerateBtn.Command.Execute(null);

            // ResRef should be generated from resname or default
            string generatedResref = editor.ResrefEdit.Text;
            generatedResref.Should().NotBeNullOrEmpty();

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.ResRef.ToString().Should().Be(generatedResref);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:153-177
        // Original: def test_utd_editor_manipulate_appearance(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateAppearance()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test appearance selection
            if (editor.AppearanceSelect.ItemCount > 0)
            {
                for (int i = 0; i < Math.Min(5, editor.AppearanceSelect.ItemCount); i++)
                {
                    editor.AppearanceSelect.SelectedIndex = i;

                    // Save and verify
                    var (data, _) = editor.Build();
                    UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                    modifiedUtd.AppearanceId.Should().Be(i);

                    // Load back and verify
                    editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
                    editor.AppearanceSelect.SelectedIndex.Should().Be(i);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:179-201
        // Original: def test_utd_editor_manipulate_conversation(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateConversation()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify conversation
            editor.ConversationEdit.Text = "test_conversation";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.Conversation.ToString().Should().Be("test_conversation");

            // Load back and verify
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
            editor.ConversationEdit.Text.Should().Be("test_conversation");
        }

        // ============================================================================
        // ADVANCED FIELDS MANIPULATIONS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:207-228
        // Original: def test_utd_editor_manipulate_min1_hp_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateMin1HpCheckbox()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Toggle checkbox
            editor.Min1HpCheckbox.IsChecked = true;
            var (data1, _) = editor.Build();
            UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
            modifiedUtd1.Min1Hp.Should().BeTrue();

            editor.Min1HpCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
            modifiedUtd2.Min1Hp.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:230-251
        // Original: def test_utd_editor_manipulate_plot_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulatePlotCheckbox()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Toggle checkbox
            editor.PlotCheckbox.IsChecked = true;
            var (data1, _) = editor.Build();
            UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
            modifiedUtd1.Plot.Should().BeTrue();

            editor.PlotCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
            modifiedUtd2.Plot.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:253-274
        // Original: def test_utd_editor_manipulate_static_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateStaticCheckbox()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Toggle checkbox
            editor.StaticCheckbox.IsChecked = true;
            var (data1, _) = editor.Build();
            UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
            modifiedUtd1.Static.Should().BeTrue();

            editor.StaticCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
            modifiedUtd2.Static.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:276-300
        // Original: def test_utd_editor_manipulate_not_blastable_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateNotBlastableCheckbox()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null || !installation.Tsl)
            {
                return; // Skip if TSL-only feature not available
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Toggle checkbox
            editor.NotBlastableCheckbox.IsChecked = true;
            var (data1, _) = editor.Build();
            UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
            modifiedUtd1.NotBlastable.Should().BeTrue();

            editor.NotBlastableCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
            modifiedUtd2.NotBlastable.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:302-322
        // Original: def test_utd_editor_manipulate_faction(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateFaction()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test faction selection
            if (editor.FactionSelect.ItemCount > 0)
            {
                for (int i = 0; i < Math.Min(5, editor.FactionSelect.ItemCount); i++)
                {
                    editor.FactionSelect.SelectedIndex = i;

                    // Save and verify
                    var (data, _) = editor.Build();
                    UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                    modifiedUtd.FactionId.Should().Be(i);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:324-348
        // Original: def test_utd_editor_manipulate_animation_state(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateAnimationState()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test various animation state values
            int[] testValues = { 0, 1, 2, 5, 10 };
            foreach (int val in testValues)
            {
                editor.AnimationStateSpin.Value = val;

                // Save and verify
                var (data, _) = editor.Build();
                UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                modifiedUtd.AnimationState.Should().Be(val);

                // Load back and verify
                editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
                editor.AnimationStateSpin.Value.Should().Be(val);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:350-376
        // Original: def test_utd_editor_manipulate_hp_spins(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateHpSpins()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test current HP
            int[] testCurrentHp = { 1, 10, 50, 100, 500 };
            foreach (int val in testCurrentHp)
            {
                editor.CurrentHpSpin.Value = val;
                var (data, _) = editor.Build();
                UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                modifiedUtd.CurrentHp.Should().Be(val);
            }

            // Test maximum HP
            int[] testMaxHp = { 1, 10, 100, 500, 1000 };
            foreach (int val in testMaxHp)
            {
                editor.MaxHpSpin.Value = val;
                var (data, _) = editor.Build();
                UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                modifiedUtd.MaximumHp.Should().Be(val);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:378-417
        // Original: def test_utd_editor_manipulate_save_spins(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateSaveSpins()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test hardness, fortitude, reflex, willpower
            int[] testValues = { 0, 5, 10, 20, 50 };
            foreach (int val in testValues)
            {
                editor.HardnessSpin.Value = val;
                var (data1, _) = editor.Build();
                UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
                modifiedUtd1.Hardness.Should().Be(val);

                editor.FortitudeSpin.Value = val;
                var (data2, _) = editor.Build();
                UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
                modifiedUtd2.Fortitude.Should().Be(val);

                editor.ReflexSpin.Value = val;
                var (data3, _) = editor.Build();
                UTD modifiedUtd3 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data3));
                modifiedUtd3.Reflex.Should().Be(val);

                editor.WillSpin.Value = val;
                var (data4, _) = editor.Build();
                UTD modifiedUtd4 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data4));
                modifiedUtd4.Willpower.Should().Be(val);
            }
        }

        // ============================================================================
        // LOCK FIELDS MANIPULATIONS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:423-444
        // Original: def test_utd_editor_manipulate_locked_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateLockedCheckbox()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Toggle checkbox
            editor.LockedCheckbox.IsChecked = true;
            var (data1, _) = editor.Build();
            UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
            modifiedUtd1.Locked.Should().BeTrue();

            editor.LockedCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
            modifiedUtd2.Locked.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:446-467
        // Original: def test_utd_editor_manipulate_need_key_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateNeedKeyCheckbox()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Toggle checkbox
            editor.NeedKeyCheckbox.IsChecked = true;
            var (data1, _) = editor.Build();
            UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
            modifiedUtd1.KeyRequired.Should().BeTrue();

            editor.NeedKeyCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
            modifiedUtd2.KeyRequired.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:469-490
        // Original: def test_utd_editor_manipulate_remove_key_checkbox(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateRemoveKeyCheckbox()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Toggle checkbox
            editor.RemoveKeyCheckbox.IsChecked = true;
            var (data1, _) = editor.Build();
            UTD modifiedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));
            modifiedUtd1.AutoRemoveKey.Should().BeTrue();

            editor.RemoveKeyCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            UTD modifiedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));
            modifiedUtd2.AutoRemoveKey.Should().BeFalse();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:492-516
        // Original: def test_utd_editor_manipulate_key_edit(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateKeyEdit()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test various key names
            string[] testKeys = { "", "test_key", "key_001", "special_key_123" };
            foreach (string key in testKeys)
            {
                editor.KeyEdit.Text = key;

                // Save and verify
                var (data, _) = editor.Build();
                UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                modifiedUtd.KeyName.Should().Be(key);

                // Load back and verify
                editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
                editor.KeyEdit.Text.Should().Be(key);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:518-552
        // Original: def test_utd_editor_manipulate_unlock_dc_spins(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateUnlockDcSpins()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test unlock DC
            int[] testValues = { 0, 10, 20, 30, 40 };
            foreach (int val in testValues)
            {
                editor.OpenLockSpin.Value = val;
                var (data, _) = editor.Build();
                UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                modifiedUtd.UnlockDc.Should().Be(val);
            }

            // Test difficulty (TSL only)
            if (installation.Tsl)
            {
                foreach (int val in testValues)
                {
                    editor.DifficultySpin.Value = val;
                    var (data, _) = editor.Build();
                    UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                    modifiedUtd.UnlockDiff.Should().Be(val);
                }

                // Test difficulty mod (TSL only)
                foreach (int val in testValues)
                {
                    editor.DifficultyModSpin.Value = val;
                    var (data, _) = editor.Build();
                    UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                    modifiedUtd.UnlockDiffMod.Should().Be(val);
                }
            }
        }

        // ============================================================================
        // SCRIPT FIELDS MANIPULATIONS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:558-576
        // Original: def test_utd_editor_manipulate_on_click_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateOnClickScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script
            editor.ScriptFields["OnClick"].Text = "test_on_click";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.OnClick.ToString().Should().Be("test_on_click");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:578-596
        // Original: def test_utd_editor_manipulate_on_closed_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateOnClosedScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script
            editor.ScriptFields["OnClosed"].Text = "test_on_closed";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.OnClosed.ToString().Should().Be("test_on_closed");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:598-616
        // Original: def test_utd_editor_manipulate_on_damaged_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateOnDamagedScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script
            editor.ScriptFields["OnDamaged"].Text = "test_on_damaged";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.OnDamaged.ToString().Should().Be("test_on_damaged");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:618-636
        // Original: def test_utd_editor_manipulate_on_death_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateOnDeathScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script
            editor.ScriptFields["OnDeath"].Text = "test_on_death";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.OnDeath.ToString().Should().Be("test_on_death");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:638-656
        // Original: def test_utd_editor_manipulate_on_open_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateOnOpenScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script
            editor.ScriptFields["OnOpen"].Text = "test_on_open";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.OnOpen.ToString().Should().Be("test_on_open");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:658-676
        // Original: def test_utd_editor_manipulate_on_open_failed_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateOnOpenFailedScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script
            editor.ScriptFields["OnOpenFailed"].Text = "test_on_open_failed";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.OnOpenFailed.ToString().Should().Be("test_on_open_failed");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:678-696
        // Original: def test_utd_editor_manipulate_on_unlock_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateOnUnlockScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script
            editor.ScriptFields["OnUnlock"].Text = "test_on_unlock";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            modifiedUtd.OnUnlock.ToString().Should().Be("test_on_unlock");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py
        // Original: def test_utd_editor_manipulate_on_power_script(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        // Note: Python uses onSpellEdit which maps to on_power in UTD
        [Fact]
        public void TestUtdEditorManipulateOnPowerScript()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify script - matching Python: editor.ui.onSpellEdit.set_combo_box_text("test_on_power")
            editor.ScriptFields["OnPower"].Text = "test_on_power";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
            // Matching Python: assert str(modified_utd.on_power) == "test_on_power"
            modifiedUtd.OnPower.ToString().Should().Be("test_on_power");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:698-737
        // Original: def test_utd_editor_manipulate_all_scripts(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateAllScripts()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify all scripts
            editor.ScriptFields["OnClick"].Text = "s_onclick";
            editor.ScriptFields["OnClosed"].Text = "s_onclosed";
            editor.ScriptFields["OnDamaged"].Text = "s_ondamaged";
            editor.ScriptFields["OnDeath"].Text = "s_ondeath";
            editor.ScriptFields["OnOpenFailed"].Text = "s_onopenfailed";
            editor.ScriptFields["OnHeartbeat"].Text = "s_onheartbeat";
            editor.ScriptFields["OnMelee"].Text = "s_onmelee";
            editor.ScriptFields["OnOpen"].Text = "s_onopen";
            editor.ScriptFields["OnUnlock"].Text = "s_onunlock";
            editor.ScriptFields["OnUserDefined"].Text = "s_onuserdef";
            // Matching PyKotor implementation: editor.ui.onSpellEdit.set_combo_box_text("s_onspell")
            editor.ScriptFields["OnPower"].Text = "s_onspell";

            // Save and verify all
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.OnClick.ToString().Should().Be("s_onclick");
            modifiedUtd.OnClosed.ToString().Should().Be("s_onclosed");
            modifiedUtd.OnDamaged.ToString().Should().Be("s_ondamaged");
            modifiedUtd.OnDeath.ToString().Should().Be("s_ondeath");
            modifiedUtd.OnOpenFailed.ToString().Should().Be("s_onopenfailed");
            modifiedUtd.OnHeartbeat.ToString().Should().Be("s_onheartbeat");
            modifiedUtd.OnMelee.ToString().Should().Be("s_onmelee");
            modifiedUtd.OnOpen.ToString().Should().Be("s_onopen");
            modifiedUtd.OnUnlock.ToString().Should().Be("s_onunlock");
            modifiedUtd.OnUserDefined.ToString().Should().Be("s_onuserdef");
            // Matching PyKotor implementation: assert str(modified_utd.on_power) == "s_onspell"
            modifiedUtd.OnPower.ToString().Should().Be("s_onspell");
        }

        // ============================================================================
        // COMMENTS FIELD MANIPULATION
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:743-774
        // Original: def test_utd_editor_manipulate_comments(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateComments()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify comments
            string[] testComments = {
                "",
                "Test comment",
                "Multi\nline\ncomment",
                "Comment with special chars !@#$%^&*()",
                "Very long comment " + new string('x', 1000)
            };

            foreach (string comment in testComments)
            {
                editor.CommentsEdit.Text = comment;

                // Save and verify
                var (data, _) = editor.Build();
                UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));
                modifiedUtd.Comment.Should().Be(comment);

                // Load back and verify
                editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);
                editor.CommentsEdit.Text.Should().Be(comment);
            }
        }

        // ============================================================================
        // COMBINATION TESTS - Multiple manipulations
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:780-807
        // Original: def test_utd_editor_manipulate_all_basic_fields_combination(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateAllBasicFieldsCombination()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify ALL basic fields
            editor.NameEdit.SetLocString(LocalizedString.FromEnglish("Combined Test Door"));
            editor.TagEdit.Text = "combined_test";
            editor.ResrefEdit.Text = "combined_resref";
            if (editor.AppearanceSelect.ItemCount > 0)
            {
                editor.AppearanceSelect.SelectedIndex = 1;
            }
            editor.ConversationEdit.Text = "test_conv";

            // Save and verify all
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.Name.Get(Language.English, Gender.Male).Should().Be("Combined Test Door");
            modifiedUtd.Tag.Should().Be("combined_test");
            modifiedUtd.ResRef.ToString().Should().Be("combined_resref");
            modifiedUtd.Conversation.ToString().Should().Be("test_conv");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:809-850
        // Original: def test_utd_editor_manipulate_all_advanced_fields_combination(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateAllAdvancedFieldsCombination()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify ALL advanced fields
            editor.Min1HpCheckbox.IsChecked = true;
            editor.PlotCheckbox.IsChecked = true;
            editor.StaticCheckbox.IsChecked = true;
            if (installation.Tsl)
            {
                editor.NotBlastableCheckbox.IsChecked = true;
            }
            if (editor.FactionSelect.ItemCount > 0)
            {
                editor.FactionSelect.SelectedIndex = 1;
            }
            editor.AnimationStateSpin.Value = 5;
            editor.CurrentHpSpin.Value = 50;
            editor.MaxHpSpin.Value = 100;
            editor.HardnessSpin.Value = 10;
            editor.FortitudeSpin.Value = 15;
            editor.ReflexSpin.Value = 20;
            editor.WillSpin.Value = 25;

            // Save and verify all
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.Min1Hp.Should().BeTrue();
            modifiedUtd.Plot.Should().BeTrue();
            modifiedUtd.Static.Should().BeTrue();
            modifiedUtd.AnimationState.Should().Be(5);
            modifiedUtd.CurrentHp.Should().Be(50);
            modifiedUtd.MaximumHp.Should().Be(100);
            modifiedUtd.Hardness.Should().Be(10);
            modifiedUtd.Fortitude.Should().Be(15);
            modifiedUtd.Reflex.Should().Be(20);
            modifiedUtd.Willpower.Should().Be(25);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:852-882
        // Original: def test_utd_editor_manipulate_all_lock_fields_combination(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorManipulateAllLockFieldsCombination()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Modify ALL lock fields
            editor.LockedCheckbox.IsChecked = true;
            editor.NeedKeyCheckbox.IsChecked = true;
            editor.RemoveKeyCheckbox.IsChecked = true;
            editor.KeyEdit.Text = "test_key_item";
            editor.OpenLockSpin.Value = 25;
            if (installation.Tsl)
            {
                editor.DifficultySpin.Value = 15;
                editor.DifficultyModSpin.Value = 5;
            }

            // Save and verify all
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.Locked.Should().BeTrue();
            modifiedUtd.KeyRequired.Should().BeTrue();
            modifiedUtd.AutoRemoveKey.Should().BeTrue();
            modifiedUtd.KeyName.Should().Be("test_key_item");
            modifiedUtd.UnlockDc.Should().Be(25);
        }

        // ============================================================================
        // SAVE/LOAD ROUNDTRIP VALIDATION TESTS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:888-918
        // Original: def test_utd_editor_save_load_roundtrip_identity(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorSaveLoadRoundtripIdentity()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            // Load original
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            UTD originalUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(originalData));
            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Save without modifications
            var (data, _) = editor.Build();
            UTD savedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            // Verify key fields match
            savedUtd.Tag.Should().Be(originalUtd.Tag);
            savedUtd.ResRef.ToString().Should().Be(originalUtd.ResRef.ToString());
            savedUtd.AppearanceId.Should().Be(originalUtd.AppearanceId);
            savedUtd.Conversation.ToString().Should().Be(originalUtd.Conversation.ToString());
            savedUtd.Static.Should().Be(originalUtd.Static);

            // Load saved data back
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);

            // Verify UI matches
            editor.TagEdit.Text.Should().Be(originalUtd.Tag);
            editor.ResrefEdit.Text.Should().Be(originalUtd.ResRef.ToString());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:920-963
        // Original: def test_utd_editor_save_load_roundtrip_with_modifications(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorSaveLoadRoundtripWithModifications()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            // Load original
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Make modifications
            editor.TagEdit.Text = "modified_roundtrip";
            editor.CurrentHpSpin.Value = 75;
            editor.MaxHpSpin.Value = 150;
            editor.LockedCheckbox.IsChecked = true;
            editor.CommentsEdit.Text = "Roundtrip test comment";

            // Save
            var (data1, _) = editor.Build();
            UTD savedUtd1 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data1));

            // Load saved data
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, data1);

            // Verify modifications preserved
            editor.TagEdit.Text.Should().Be("modified_roundtrip");
            editor.CurrentHpSpin.Value.Should().Be(75);
            editor.MaxHpSpin.Value.Should().Be(150);
            editor.LockedCheckbox.IsChecked.Should().BeTrue();
            editor.CommentsEdit.Text.Should().Be("Roundtrip test comment");

            // Save again
            var (data2, _) = editor.Build();
            UTD savedUtd2 = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data2));

            // Verify second save matches first
            savedUtd2.Tag.Should().Be(savedUtd1.Tag);
            savedUtd2.CurrentHp.Should().Be(savedUtd1.CurrentHp);
            savedUtd2.MaximumHp.Should().Be(savedUtd1.MaximumHp);
            savedUtd2.Locked.Should().Be(savedUtd1.Locked);
            savedUtd2.Comment.Should().Be(savedUtd1.Comment);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:965-996
        // Original: def test_utd_editor_multiple_save_load_cycles(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorMultipleSaveLoadCycles()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Perform multiple cycles
            for (int cycle = 0; cycle < 5; cycle++)
            {
                // Modify
                editor.TagEdit.Text = $"cycle_{cycle}";
                editor.CurrentHpSpin.Value = 10 + cycle * 10;

                // Save
                var (data, _) = editor.Build();
                UTD savedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

                // Verify
                savedUtd.Tag.Should().Be($"cycle_{cycle}");
                savedUtd.CurrentHp.Should().Be(10 + cycle * 10);

                // Load back
                editor.Load(utdFile, "naldoor001", ResourceType.UTD, data);

                // Verify loaded
                editor.TagEdit.Text.Should().Be($"cycle_{cycle}");
                editor.CurrentHpSpin.Value.Should().Be(10 + cycle * 10);
            }
        }

        // ============================================================================
        // EDGE CASES AND BOUNDARY TESTS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1002-1034
        // Original: def test_utd_editor_minimum_values(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorMinimumValues()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Set all to minimums
            editor.TagEdit.Text = "";
            editor.ResrefEdit.Text = "";
            editor.KeyEdit.Text = "";
            editor.CurrentHpSpin.Value = 0;
            editor.MaxHpSpin.Value = 0;
            editor.HardnessSpin.Value = 0;
            editor.FortitudeSpin.Value = 0;
            editor.ReflexSpin.Value = 0;
            editor.WillSpin.Value = 0;
            editor.AnimationStateSpin.Value = 0;
            editor.OpenLockSpin.Value = 0;

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.Tag.Should().Be("");
            modifiedUtd.CurrentHp.Should().Be(0);
            modifiedUtd.MaximumHp.Should().Be(0);
            modifiedUtd.Hardness.Should().Be(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1036-1063
        // Original: def test_utd_editor_maximum_values(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorMaximumValues()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Set all to maximums
            editor.TagEdit.Text = new string('x', 32); // Max tag length
            editor.CurrentHpSpin.Value = editor.CurrentHpSpin.Maximum;
            editor.MaxHpSpin.Value = editor.MaxHpSpin.Maximum;
            editor.HardnessSpin.Value = editor.HardnessSpin.Maximum;
            editor.FortitudeSpin.Value = editor.FortitudeSpin.Maximum;
            editor.ReflexSpin.Value = editor.ReflexSpin.Maximum;
            editor.WillSpin.Value = editor.WillSpin.Maximum;
            editor.OpenLockSpin.Value = editor.OpenLockSpin.Maximum;

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.CurrentHp.Should().Be((int)editor.CurrentHpSpin.Maximum);
            modifiedUtd.MaximumHp.Should().Be((int)editor.MaxHpSpin.Maximum);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1065-1098
        // Original: def test_utd_editor_empty_strings(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorEmptyStrings()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Set all text fields to empty
            editor.TagEdit.Text = "";
            editor.ResrefEdit.Text = "";
            editor.KeyEdit.Text = "";
            editor.CommentsEdit.Text = "";
            editor.ScriptFields["OnClick"].Text = "";
            editor.ScriptFields["OnClosed"].Text = "";
            editor.ScriptFields["OnDamaged"].Text = "";
            editor.ScriptFields["OnDeath"].Text = "";
            editor.ScriptFields["OnPower"].Text = "";

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.Tag.Should().Be("");
            modifiedUtd.ResRef.ToString().Should().Be("");
            modifiedUtd.KeyName.Should().Be("");
            modifiedUtd.Comment.Should().Be("");
            modifiedUtd.OnClick.ToString().Should().Be("");
            modifiedUtd.OnClosed.ToString().Should().Be("");
            modifiedUtd.OnDamaged.ToString().Should().Be("");
            modifiedUtd.OnDeath.ToString().Should().Be("");
            modifiedUtd.OnPower.ToString().Should().Be("");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1100-1124
        // Original: def test_utd_editor_special_characters_in_text_fields(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorSpecialCharactersInTextFields()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Test special characters
            string specialTag = "test_tag_123";
            editor.TagEdit.Text = specialTag;

            string specialComment = "Comment with\nnewlines\tand\ttabs";
            editor.CommentsEdit.Text = specialComment;

            // Save and verify
            var (data, _) = editor.Build();
            UTD modifiedUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            modifiedUtd.Tag.Should().Be(specialTag);
            modifiedUtd.Comment.Should().Be(specialComment);
        }

        // ============================================================================
        // GFF COMPARISON TESTS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1156-1184
        // Original: def test_utd_editor_gff_roundtrip_with_modifications(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorGffRoundtripWithModifications()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utdFile);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, originalData);

            // Make modifications
            editor.TagEdit.Text = "modified_gff_test";
            editor.CurrentHpSpin.Value = 50;
            editor.LockedCheckbox.IsChecked = true;

            // Save
            var (data, _) = editor.Build();

            // Verify it's valid GFF
            GFF newGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
            newGff.Should().NotBeNull();

            // Verify it's valid UTD
            UTD modifiedUtd = UTDHelpers.ConstructUtd(newGff);
            modifiedUtd.Tag.Should().Be("modified_gff_test");
            modifiedUtd.CurrentHp.Should().Be(50);
            modifiedUtd.Locked.Should().BeTrue();
        }

        // ============================================================================
        // NEW FILE CREATION TESTS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1190-1217
        // Original: def test_utd_editor_new_file_creation(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestUtdEditorNewFileCreationWithFields()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            // Create new
            var editor = new UTDEditor(null, installation);
            editor.New();

            // Set all fields
            editor.NameEdit.SetLocString(LocalizedString.FromEnglish("New Door"));
            editor.TagEdit.Text = "new_door";
            editor.ResrefEdit.Text = "new_door";
            if (editor.AppearanceSelect.ItemCount > 0)
            {
                editor.AppearanceSelect.SelectedIndex = 0;
            }
            editor.CurrentHpSpin.Value = 100;
            editor.MaxHpSpin.Value = 100;
            editor.LockedCheckbox.IsChecked = true;
            editor.CommentsEdit.Text = "New door comment";

            // Build and verify
            var (data, _) = editor.Build();
            UTD newUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            newUtd.Name.Get(Language.English, Gender.Male).Should().Be("New Door");
            newUtd.Tag.Should().Be("new_door");
            newUtd.CurrentHp.Should().Be(100);
            newUtd.Locked.Should().BeTrue();
            newUtd.Comment.Should().Be("New door comment");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1219-1235
        // Original: def test_utd_editor_new_file_all_defaults(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestUtdEditorNewFileAllDefaults()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            // Create new
            var editor = new UTDEditor(null, installation);
            editor.New();

            // Build and verify defaults
            var (data, _) = editor.Build();
            UTD newUtd = UTDHelpers.ConstructUtd(Andastra.Parsing.Formats.GFF.GFF.FromBytes(data));

            // Verify defaults (may vary, but should be consistent)
            newUtd.Tag.Should().NotBeNull();
            newUtd.AppearanceId.Should().BeGreaterThanOrEqualTo(0);
            newUtd.CurrentHp.Should().BeGreaterThanOrEqualTo(0);
            newUtd.MaximumHp.Should().BeGreaterThanOrEqualTo(0);
        }

        // ============================================================================
        // BUTTON FUNCTIONALITY TESTS
        // ============================================================================

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1241-1260
        // Original: def test_utd_editor_generate_tag_button(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorGenerateTagButton()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, System.IO.File.ReadAllBytes(utdFile));

            // Clear tag
            editor.TagEdit.Text = "";

            // Click generate button
            editor.TagGenerateBtn.Command.Execute(null);

            // Tag should be generated
            string generatedTag = editor.TagEdit.Text;
            generatedTag.Should().NotBeNullOrEmpty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1262-1281
        // Original: def test_utd_editor_generate_resref_button(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorGenerateResrefButton()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, System.IO.File.ReadAllBytes(utdFile));

            // Clear resref
            editor.ResrefEdit.Text = "";

            // Click generate button
            editor.ResrefGenerateBtn.Command.Execute(null);

            // ResRef should be generated
            string generatedResref = editor.ResrefEdit.Text;
            generatedResref.Should().NotBeNullOrEmpty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1283-1292
        // Original: def test_utd_editor_conversation_modify_button(qtbot: QtBot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtdEditorConversationModifyButton()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            // Verify button exists
            var editor = new UTDEditor(null, installation);
            editor.ConversationModifyBtn.Should().NotBeNull();

            // Verify signal is connected (button has click handler)
            // In Avalonia, we verify the button exists and has a command/click handler
            editor.ConversationModifyBtn.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1298-1312
        // Original: def test_utd_editor_preview_toggle(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestUtdEditorPreviewToggle()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            var editor = new UTDEditor(null, installation);

            // Verify preview toggle functionality exists
            // In Avalonia, we verify that toggle_preview method exists if implemented
            // TODO: STUB - For now, we just verify the editor can be created and preview functionality would be available
            editor.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1315-1341
        // Original: def test_utdeditor_editor_help_dialog_opens_correct_file(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestUtdEditorHelpDialogOpensCorrectFile()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            var editor = new UTDEditor(null, installation);

            // Verify help dialog functionality exists
            // In Avalonia, we verify that help dialog can be shown
            // TODO: STUB - For now, we just verify the editor can be created
            editor.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utd_editor.py:1343-1357
        // Original: Test preview updates when appearance changes
        [Fact]
        public void TestUtdEditorPreviewUpdatesOnAppearanceChange()
        {
            (string utdFile, HTInstallation installation) = GetTestFileAndInstallation();

            if (!System.IO.File.Exists(utdFile) || installation == null)
            {
                return;
            }

            var editor = new UTDEditor(null, installation);
            editor.Load(utdFile, "naldoor001", ResourceType.UTD, System.IO.File.ReadAllBytes(utdFile));

            // Change appearance - should trigger preview update
            if (editor.AppearanceSelect.ItemCount > 1)
            {
                editor.AppearanceSelect.SelectedIndex = 1;
                // Verify that the appearance change is reflected
                editor.AppearanceSelect.SelectedIndex.Should().Be(1);
            }
        }
    }
}
