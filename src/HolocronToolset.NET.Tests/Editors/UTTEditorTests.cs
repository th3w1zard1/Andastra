using System;
using System.Collections.Generic;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py
    // Original: Comprehensive tests for UTT Editor
    [Collection("Avalonia Test Collection")]
    public class UTTEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTTEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUttEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py
            // Original: def test_utt_editor_new_file_creation(qtbot, installation):
            var editor = new UTTEditor(null, null);

            editor.New();

            // Verify UTT object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestUttEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py
            // Original: def test_utt_editor_initialization(qtbot, installation):
            var editor = new UTTEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py:24-49
        // Original: def test_utt_editor_manipulate_name_locstring(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUttEditorManipulateNameLocstring()
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

            string uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            if (!System.IO.File.Exists(uttFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            }

            if (!System.IO.File.Exists(uttFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTTEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(uttFile);
            var originalUtt = CSharpKOTOR.Resource.Generics.UTTAuto.ReadUtt(originalData);

            editor.Load(uttFile, "newtransition9", ResourceType.UTT, originalData);

            // Modify name
            var newName = LocalizedString.FromEnglish("Modified Trigger Name");
            editor.NameEdit.Should().NotBeNull("NameEdit should be initialized");
            editor.NameEdit.SetLocString(newName);

            // Save and verify
            var (data, _) = editor.Build();
            var modifiedUtt = CSharpKOTOR.Resource.Generics.UTTAuto.ReadUtt(data);
            modifiedUtt.Name.Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male).Should().Be("Modified Trigger Name");
            modifiedUtt.Name.Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male).Should().NotBe(originalUtt.Name.Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male));

            // Load back and verify
            editor.Load(uttFile, "newtransition9", ResourceType.UTT, data);
            editor.NameEdit.GetLocString().Get(CSharpKOTOR.Common.Language.English, CSharpKOTOR.Common.Gender.Male).Should().Be("Modified Trigger Name");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py:51-75
        // Original: def test_utt_editor_manipulate_tag(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUttEditorManipulateTag()
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

            string uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            if (!System.IO.File.Exists(uttFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            }

            if (!System.IO.File.Exists(uttFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTTEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(uttFile);
            var originalUtt = CSharpKOTOR.Resource.Generics.UTTAuto.ReadUtt(originalData);

            editor.Load(uttFile, "newtransition9", ResourceType.UTT, originalData);

            // Modify tag
            editor.TagEdit.Should().NotBeNull("TagEdit should be initialized");
            editor.TagEdit.Text = "modified_tag";

            // Save and verify
            var (data, _) = editor.Build();
            var modifiedUtt = CSharpKOTOR.Resource.Generics.UTTAuto.ReadUtt(data);
            modifiedUtt.Tag.Should().Be("modified_tag");
            modifiedUtt.Tag.Should().NotBe(originalUtt.Tag);

            // Load back and verify
            editor.Load(uttFile, "newtransition9", ResourceType.UTT, data);
            editor.TagEdit.Text.Should().Be("modified_tag");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py:77-99
        // Original: def test_utt_editor_manipulate_resref(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUttEditorManipulateResref()
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

            string uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            if (!System.IO.File.Exists(uttFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            }

            if (!System.IO.File.Exists(uttFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTTEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(uttFile);

            editor.Load(uttFile, "newtransition9", ResourceType.UTT, originalData);

            // Modify resref
            editor.ResrefEdit.Should().NotBeNull("ResrefEdit should be initialized");
            editor.ResrefEdit.Text = "modified_resref";

            // Save and verify
            var (data, _) = editor.Build();
            var modifiedUtt = CSharpKOTOR.Resource.Generics.UTTAuto.ReadUtt(data);
            modifiedUtt.ResRef.ToString().Should().Be("modified_resref");

            // Load back and verify
            editor.Load(uttFile, "newtransition9", ResourceType.UTT, data);
            editor.ResrefEdit.Text.Should().Be("modified_resref");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py:101-125
        // Original: def test_utt_editor_manipulate_cursor(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUttEditorManipulateCursor()
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

            string uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            if (!System.IO.File.Exists(uttFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            }

            if (!System.IO.File.Exists(uttFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTTEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(uttFile);

            editor.Load(uttFile, "newtransition9", ResourceType.UTT, originalData);

            // Test cursor selection
            var cursorSelectField = typeof(UTTEditor).GetField("_cursorSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var cursorSelect = cursorSelectField?.GetValue(editor) as HolocronToolset.NET.Widgets.Edit.ComboBox2DA;
            cursorSelect.Should().NotBeNull("CursorSelect should be initialized");

            if (cursorSelect != null && cursorSelect.Items.Count > 0)
            {
                int maxIndex = Math.Min(5, cursorSelect.Items.Count);
                for (int i = 0; i < maxIndex; i++)
                {
                    cursorSelect.SetSelectedIndex(i);

                    // Save and verify
                    var (data, _) = editor.Build();
                    var modifiedUtt = CSharpKOTOR.Resource.Generics.UTTAuto.ReadUtt(data);
                    modifiedUtt.Cursor.Should().Be(i);

                    // Load back and verify
                    editor.Load(uttFile, "newtransition9", ResourceType.UTT, data);
                    cursorSelect = cursorSelectField?.GetValue(editor) as HolocronToolset.NET.Widgets.Edit.ComboBox2DA;
                    cursorSelect.Should().NotBeNull();
                    cursorSelect.SelectedIndex.Should().Be(i);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py:127-151
        // Original: def test_utt_editor_manipulate_type(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUttEditorManipulateType()
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

            string uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            if (!System.IO.File.Exists(uttFile))
            {
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            }

            if (!System.IO.File.Exists(uttFile))
            {
                return; // Skip if test file not available
            }

            var editor = new UTTEditor(null, installation);
            byte[] originalData = System.IO.File.ReadAllBytes(uttFile);

            editor.Load(uttFile, "newtransition9", ResourceType.UTT, originalData);

            // Test type selection
            var typeSelectField = typeof(UTTEditor).GetField("_typeSelect", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var typeSelect = typeSelectField?.GetValue(editor) as Avalonia.Controls.ComboBox;
            typeSelect.Should().NotBeNull("TypeSelect should be initialized");

            if (typeSelect != null && typeSelect.Items.Count > 0)
            {
                int maxIndex = Math.Min(3, typeSelect.Items.Count);
                for (int i = 0; i < maxIndex; i++)
                {
                    typeSelect.SelectedIndex = i;

                    // Save and verify
                    var (data, _) = editor.Build();
                    var modifiedUtt = CSharpKOTOR.Resource.Generics.UTTAuto.ReadUtt(data);
                    modifiedUtt.TypeId.Should().Be(i);

                    // Load back and verify
                    editor.Load(uttFile, "newtransition9", ResourceType.UTT, data);
                    typeSelect = typeSelectField?.GetValue(editor) as Avalonia.Controls.ComboBox;
                    typeSelect.Should().NotBeNull();
                    typeSelect.SelectedIndex.Should().Be(i);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utt_editor.py:713-742
        // Original: def test_utt_editor_save_load_roundtrip_identity(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUttEditorSaveLoadRoundtrip()
        {
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            string uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            if (!System.IO.File.Exists(uttFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                uttFile = System.IO.Path.Combine(testFilesDir, "newtransition9.utt");
            }

            if (!System.IO.File.Exists(uttFile))
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

            var editor = new UTTEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            byte[] data = System.IO.File.ReadAllBytes(uttFile);
            var oldGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);

            editor.Load(uttFile, "newtransition9", ResourceType.UTT, data);

            var (newData, _) = editor.Build();

            GFF newGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(newData);

            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = oldGff.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }
    }
}
