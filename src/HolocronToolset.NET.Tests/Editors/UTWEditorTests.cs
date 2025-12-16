using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py
    // Original: Comprehensive tests for UTW Editor
    [Collection("Avalonia Test Collection")]
    public class UTWEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTWEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtwEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:20-30
            // Original: def test_utw_editor_new_file_creation(qtbot, installation):
            var editor = new UTWEditor(null, null);

            editor.New();

            // Verify UTW object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestUtwEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:32-44
            // Original: def test_utw_editor_initialization(qtbot, installation):
            var editor = new UTWEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:258-287
        // Original: def test_utw_editor_save_load_roundtrip_identity(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorSaveLoadRoundtrip()
        {
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            var editor = new UTWEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            byte[] data = System.IO.File.ReadAllBytes(utwFile);
            var oldGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, data);

            var (newData, _) = editor.Build();

            GFF newGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(newData);

            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = oldGff.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:24-49
        // Original: def test_utw_editor_manipulate_name_locstring(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorManipulateNameLocstring()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTWEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utwFile);
            var originalUtw = UTWAuto.ReadUtw(originalData);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, originalData);

            // Modify name
            var newName = LocalizedString.FromEnglish("Modified Waypoint Name");
            if (editor.NameEdit != null)
            {
                editor.NameEdit.SetLocString(newName);
            }

            // Save and verify
            var (data, _) = editor.Build();
            var modifiedUtw = UTWAuto.ReadUtw(data);
            modifiedUtw.Name.Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male).Should().Be("Modified Waypoint Name");
            modifiedUtw.Name.Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male).Should().NotBe(originalUtw.Name.Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male));

            // Load back and verify
            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, data);
            if (editor.NameEdit != null)
            {
                editor.NameEdit.GetLocString().Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male).Should().Be("Modified Waypoint Name");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:51-75
        // Original: def test_utw_editor_manipulate_tag(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorManipulateTag()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTWEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utwFile);
            var originalUtw = UTWAuto.ReadUtw(originalData);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, originalData);

            // Modify tag
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text = "modified_tag";
            }

            // Save and verify
            var (data, _) = editor.Build();
            var modifiedUtw = UTWAuto.ReadUtw(data);
            modifiedUtw.Tag.Should().Be("modified_tag");
            modifiedUtw.Tag.Should().NotBe(originalUtw.Tag);

            // Load back and verify
            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, data);
            if (editor.TagEdit != null)
            {
                editor.TagEdit.Text.Should().Be("modified_tag");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:77-99
        // Original: def test_utw_editor_manipulate_resref(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorManipulateResref()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTWEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utwFile);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, originalData);

            // Modify resref
            if (editor.ResrefEdit != null)
            {
                editor.ResrefEdit.Text = "modified_resref";
            }

            // Save and verify
            var (data, _) = editor.Build();
            var modifiedUtw = UTWAuto.ReadUtw(data);
            modifiedUtw.ResRef.ToString().Should().Be("modified_resref");

            // Load back and verify
            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, data);
            if (editor.ResrefEdit != null)
            {
                editor.ResrefEdit.Text.Should().Be("modified_resref");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:105-126
        // Original: def test_utw_editor_manipulate_is_note_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorManipulateIsNoteCheckbox()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTWEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utwFile);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, originalData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:118-121
            // Original: editor.ui.isNoteCheckbox.setChecked(True)
            // Original: data, _ = editor.build()
            // Original: modified_utw = read_utw(data)
            // Original: assert modified_utw.has_map_note
            editor.IsNoteCheckbox.Should().NotBeNull("IsNoteCheckbox should be initialized");
            // Set checkbox value (matching Python: editor.ui.isNoteCheckbox.setChecked(True))
            // Use both methods to ensure it works in headless mode
            editor.IsNoteCheckbox.IsChecked = true;
            editor.IsNoteCheckbox.SetCurrentValue(CheckBox.IsCheckedProperty, true);
            // Force property update by reading it back
            var checkValue = editor.IsNoteCheckbox.GetValue(CheckBox.IsCheckedProperty);
            var isCheckedProp = editor.IsNoteCheckbox.IsChecked;
            var (data1, _) = editor.Build();
            var modifiedUtw1 = UTWAuto.ReadUtw(data1);
            // If headless mode didn't register the change, the test will fail here
            // This is a known limitation - in real usage with a UI, the checkbox works correctly
            modifiedUtw1.HasMapNote.Should().BeTrue($"HasMapNote should be true after setting checkbox (GetValue: {checkValue}, IsChecked: {isCheckedProp})");

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:123-126
            // Original: editor.ui.isNoteCheckbox.setChecked(False)
            // Original: data, _ = editor.build()
            // Original: modified_utw = read_utw(data)
            // Original: assert not modified_utw.has_map_note
            editor.IsNoteCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            var modifiedUtw2 = UTWAuto.ReadUtw(data2);
            modifiedUtw2.HasMapNote.Should().BeFalse("HasMapNote should be false after unchecking");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:128-149
        // Original: def test_utw_editor_manipulate_note_enabled_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorManipulateNoteEnabledCheckboxSimple()
        {
            // Simple test without file loading
            var editor = new UTWEditor(null, null);
            editor.New();

            editor.NoteEnabledCheckbox.Should().NotBeNull("NoteEnabledCheckbox should be initialized");
            
            // Verify initial state
            editor.NoteEnabledCheckbox.IsChecked.Should().BeFalse("Checkbox should start unchecked");
            
            // Set checkbox to true
            editor.NoteEnabledCheckbox.IsChecked = true;
            editor.NoteEnabledCheckbox.IsChecked.Should().BeTrue("Checkbox should be true after setting");
            
            // Build and verify
            var (data1, _) = editor.Build();
            var modifiedUtw1 = UTWAuto.ReadUtw(data1);
            modifiedUtw1.MapNoteEnabled.Should().BeTrue("MapNoteEnabled should be true after setting checkbox");

            // Set checkbox to false
            editor.NoteEnabledCheckbox.IsChecked = false;
            editor.NoteEnabledCheckbox.IsChecked.Should().BeFalse("Checkbox should be false after unchecking");
            
            // Build and verify
            var (data2, _) = editor.Build();
            var modifiedUtw2 = UTWAuto.ReadUtw(data2);
            modifiedUtw2.MapNoteEnabled.Should().BeFalse("MapNoteEnabled should be false after unchecking");
        }

        [Fact]
        public void TestUtwEditorManipulateNoteEnabledCheckbox()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTWEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utwFile);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, originalData);

            // Toggle checkbox
            editor.NoteEnabledCheckbox.Should().NotBeNull("NoteEnabledCheckbox should be initialized");
            editor.NoteEnabledCheckbox.IsChecked = true;
            editor.NoteEnabledCheckbox.IsChecked.Should().BeTrue("Checkbox should be checked after setting");
            
            var (data1, _) = editor.Build();
            var modifiedUtw1 = UTWAuto.ReadUtw(data1);
            modifiedUtw1.MapNoteEnabled.Should().BeTrue("MapNoteEnabled should be true after setting checkbox");

            editor.NoteEnabledCheckbox.IsChecked = false;
            var (data2, _) = editor.Build();
            var modifiedUtw2 = UTWAuto.ReadUtw(data2);
            modifiedUtw2.MapNoteEnabled.Should().BeFalse("MapNoteEnabled should be false after unchecking");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utw_editor.py:186-217
        // Original: def test_utw_editor_manipulate_comments(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtwEditorManipulateComments()
        {
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
            {
                installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            if (installation == null)
            {
                return; // Skip if no installation available
            }

            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            if (!System.IO.File.Exists(utwFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utwFile = System.IO.Path.Combine(testFilesDir, "tar05_sw05aa10.utw");
            }

            if (!System.IO.File.Exists(utwFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTWEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(utwFile);

            editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, originalData);

            // Modify comments
            var testComments = new[]
            {
                "",
                "Test comment",
                "Multi\nline\ncomment",
                "Comment with special chars !@#$%^&*()",
                new string('A', 1000), // Very long comment
            };

            foreach (var comment in testComments)
            {
                if (editor.CommentsEdit != null)
                {
                    editor.CommentsEdit.Text = comment;
                }

                // Save and verify
                var (data, _) = editor.Build();
                var modifiedUtw = UTWAuto.ReadUtw(data);
                modifiedUtw.Comment.Should().Be(comment);

                // Load back and verify
                editor.Load(utwFile, "tar05_sw05aa10", ResourceType.UTW, data);
                if (editor.CommentsEdit != null)
                {
                    editor.CommentsEdit.Text.Should().Be(comment);
                }
            }
        }
    }
}
