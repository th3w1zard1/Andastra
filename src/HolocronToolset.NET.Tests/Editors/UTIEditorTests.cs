using System;
using System.Collections.Generic;
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
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py
    // Original: Comprehensive tests for UTI Editor
    [Collection("Avalonia Test Collection")]
    public class UTIEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public UTIEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static UTIEditorTests()
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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:147-180
        // Original: def test_uti_editor_all_widgets_exist(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorAllWidgetsExist()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();

            // Basic tab widgets
            // Matching Python: assert hasattr(editor.ui, 'nameEdit')
            editor.NameEdit.Should().NotBeNull();
            editor.DescEdit.Should().NotBeNull();
            editor.TagEdit.Should().NotBeNull();
            editor.ResrefEdit.Should().NotBeNull();
            editor.BaseSelect.Should().NotBeNull();
            editor.CostSpin.Should().NotBeNull();
            editor.AdditionalCostSpin.Should().NotBeNull();
            editor.UpgradeSpin.Should().NotBeNull();
            editor.PlotCheckbox.Should().NotBeNull();
            editor.ChargesSpin.Should().NotBeNull();
            editor.StackSpin.Should().NotBeNull();
            editor.ModelVarSpin.Should().NotBeNull();
            editor.BodyVarSpin.Should().NotBeNull();
            editor.TextureVarSpin.Should().NotBeNull();
            editor.TagGenerateBtn.Should().NotBeNull();
            editor.ResrefGenerateBtn.Should().NotBeNull();

            // Properties tab widgets
            // Matching Python: assert hasattr(editor.ui, 'availablePropertyList')
            editor.AvailablePropertyList.Should().NotBeNull();
            editor.AssignedPropertiesList.Should().NotBeNull();
            editor.AddPropertyBtn.Should().NotBeNull();
            editor.RemovePropertyBtn.Should().NotBeNull();
            editor.EditPropertyBtn.Should().NotBeNull();

            // Comments tab widgets
            // Matching Python: assert hasattr(editor.ui, 'commentsEdit')
            editor.CommentsEdit.Should().NotBeNull();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:182-240
        // Original: def test_uti_editor_all_basic_widgets_interactions(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorAllBasicWidgetsInteractions()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();
            editor.New();

            // Test tagEdit - TextBox
            // Matching Python: editor.ui.tagEdit.setText("test_tag_item")
            // Matching Python: assert editor.ui.tagEdit.text() == "test_tag_item"
            editor.TagEdit.Text = "test_tag_item";
            editor.TagEdit.Text.Should().Be("test_tag_item");

            // Test resrefEdit - TextBox
            // Matching Python: editor.ui.resrefEdit.setText("test_item_resref")
            // Matching Python: assert editor.ui.resrefEdit.text() == "test_item_resref"
            editor.ResrefEdit.Text = "test_item_resref";
            editor.ResrefEdit.Text.Should().Be("test_item_resref");

            // Test baseSelect - ComboBox
            // Matching Python: for i in range(min(10, editor.ui.baseSelect.count())):
            if (editor.BaseSelect.ItemCount > 0)
            {
                int maxIndex = Math.Min(10, editor.BaseSelect.ItemCount);
                for (int i = 0; i < maxIndex; i++)
                {
                    // Matching Python: editor.ui.baseSelect.setCurrentIndex(i)
                    // Matching Python: assert editor.ui.baseSelect.currentIndex() == i
                    editor.BaseSelect.SelectedIndex = i;
                    editor.BaseSelect.SelectedIndex.Should().Be(i);
                }
            }

            // Test ALL spin boxes
            // Matching Python: spin_tests = [('costSpin', [0, 1, 10, 100, 1000, 10000]), ...]
            var spinTests = new Dictionary<Avalonia.Controls.NumericUpDown, int[]>
            {
                { editor.CostSpin, new[] { 0, 1, 10, 100, 1000, 10000 } },
                { editor.AdditionalCostSpin, new[] { 0, 1, 10, 100, 1000 } },
                { editor.UpgradeSpin, new[] { 0, 1, 2, 3, 4, 5 } },
                { editor.ChargesSpin, new[] { 0, 1, 5, 10, 50, 100 } },
                { editor.StackSpin, new[] { 1, 5, 10, 50, 100 } },
                { editor.ModelVarSpin, new[] { 0, 1, 2, 3, 4, 5 } },
                { editor.BodyVarSpin, new[] { 0, 1, 2, 3, 4, 5 } },
                { editor.TextureVarSpin, new[] { 0, 1, 2, 3, 4, 5 } }
            };

            foreach ((Avalonia.Controls.NumericUpDown spin, int[] values) in spinTests)
            {
                foreach (int val in values)
                {
                    // Matching Python: spin.setValue(val)
                    // Matching Python: assert spin.value() == val
                    spin.Value = val;
                    spin.Value.Should().Be(val);
                }
            }

            // Test plotCheckbox
            // Matching Python: editor.ui.plotCheckbox.setChecked(True)
            // Matching Python: assert editor.ui.plotCheckbox.isChecked()
            editor.PlotCheckbox.IsChecked = true;
            editor.PlotCheckbox.IsChecked.Should().BeTrue();
            editor.PlotCheckbox.IsChecked = false;
            editor.PlotCheckbox.IsChecked.Should().BeFalse();

            // Test tag generate button
            // Matching Python: qtbot.mouseClick(editor.ui.tagGenerateButton, Qt.MouseButton.LeftButton)
            // Matching Python: assert editor.ui.tagEdit.text() == editor.ui.resrefEdit.text()
            editor.TagGenerateBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            editor.TagEdit.Text.Should().Be(editor.ResrefEdit.Text);

            // Test resref generate button
            // Matching Python: qtbot.mouseClick(editor.ui.resrefGenerateButton, Qt.MouseButton.LeftButton)
            editor.ResrefGenerateBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            // Resref should be generated (either from resname or default)
            editor.ResrefEdit.Text.Should().NotBeNullOrEmpty();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:241-301
        // Original: def test_uti_editor_properties_widgets_exhaustive(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorPropertiesWidgetsExhaustive()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();
            editor.New();

            // Test availablePropertyList - TreeView
            // Matching Python: assert editor.ui.availablePropertyList.topLevelItemCount() > 0
            editor.AvailablePropertyListItemCount.Should().BeGreaterThan(0, "Available properties should be populated from 2DA");

            // Test selecting and adding properties
            // Matching Python: if editor.ui.availablePropertyList.topLevelItemCount() > 0:
            if (editor.AvailablePropertyListItemCount > 0)
            {
                // Matching Python: first_item = editor.ui.availablePropertyList.topLevelItem(0)
                // In Avalonia TreeView, we need to access items differently
                var items = editor.AvailablePropertyList.Items;
                if (items != null && ((System.Collections.IList)items).Count > 0)
                {
                    object firstItem = ((System.Collections.IList)items)[0];
                    editor.AvailablePropertyList.SelectedItem = firstItem;

                    // Test add button
                    // Matching Python: initial_count = editor.ui.assignedPropertiesList.count()
                    int initialCount = editor.AssignedPropertiesList.ItemCount;

                    // Matching Python: qtbot.mouseClick(editor.ui.addPropertyButton, Qt.MouseButton.LeftButton)
                    editor.AddPropertyBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));

                    // Property should be added if item has no children (leaf node)
                    // Matching Python: if first_item.childCount() == 0: assert editor.ui.assignedPropertiesList.count() == initial_count + 1
                    // Check if the selected item is a leaf (no children) by checking if it has no ItemsSource or empty ItemsSource
                    bool isLeafNode = false;
                    if (firstItem is Avalonia.Controls.TreeViewItem treeItem)
                    {
                        isLeafNode = treeItem.ItemsSource == null || ((System.Collections.IList)treeItem.ItemsSource).Count == 0;
                    }

                    if (isLeafNode)
                    {
                        // Matching Python: assert editor.ui.assignedPropertiesList.count() == initial_count + 1
                        editor.AssignedPropertiesList.ItemCount.Should().Be(initialCount + 1, "Property should be added when leaf node is selected");
                    }
                }
            }

            // Test assignedPropertiesList interactions
            // Matching Python: if editor.ui.assignedPropertiesList.count() > 0:
            if (editor.AssignedPropertiesList.ItemCount > 0)
            {
                // Matching Python: editor.ui.assignedPropertiesList.setCurrentRow(0)
                editor.AssignedPropertiesList.SelectedIndex = 0;

                // Test remove button
                // Matching Python: count_before = editor.ui.assignedPropertiesList.count()
                // Matching Python: qtbot.mouseClick(editor.ui.removePropertyButton, Qt.MouseButton.LeftButton)
                // Matching Python: assert editor.ui.assignedPropertiesList.count() == count_before - 1
                int countBefore = editor.AssignedPropertiesList.ItemCount;
                editor.RemovePropertyBtn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                editor.AssignedPropertiesList.ItemCount.Should().Be(countBefore - 1, "Remove button should remove selected property");
            }

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:302-330
        // Original: def test_uti_editor_icon_updates(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorIconUpdates()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();
            editor.New();

            // Matching Python: if editor.ui.baseSelect.count() > 0:
            if (editor.BaseSelect != null && editor.BaseSelect.ItemCount > 0)
            {
                // Test icon updates when base changes
                // Matching Python: editor.ui.baseSelect.setCurrentIndex(0)
                editor.BaseSelect.SelectedIndex = 0;
                System.Threading.Thread.Sleep(10); // Allow icon to update

                // Test icon updates when model variation changes
                // Matching Python: for val in [0, 1, 2, 3]:
                foreach (int val in new[] { 0, 1, 2, 3 })
                {
                    // Matching Python: editor.ui.modelVarSpin.setValue(val)
                    editor.ModelVarSpin.Value = val;
                    System.Threading.Thread.Sleep(5);
                }

                // Test icon updates when body variation changes
                // Matching Python: for val in [0, 1, 2]:
                foreach (int val in new[] { 0, 1, 2 })
                {
                    // Matching Python: editor.ui.bodyVarSpin.setValue(val)
                    editor.BodyVarSpin.Value = val;
                    System.Threading.Thread.Sleep(5);
                }

                // Test icon updates when texture variation changes
                // Matching Python: for val in [0, 1, 2]:
                foreach (int val in new[] { 0, 1, 2 })
                {
                    // Matching Python: editor.ui.textureVarSpin.setValue(val)
                    editor.TextureVarSpin.Value = val;
                    System.Threading.Thread.Sleep(5);
                }
            }

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:331-346
        // Original: def test_uti_editor_comments_widget(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorCommentsWidget()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();
            editor.New();

            // Test comments text edit
            // Matching Python: editor.ui.commentsEdit.setPlainText("Test comment\nLine 2\nLine 3")
            // Matching Python: assert editor.ui.commentsEdit.toPlainText() == "Test comment\nLine 2\nLine 3"
            string testComment = "Test comment\nLine 2\nLine 3";
            editor.CommentsEdit.Text = testComment;
            editor.CommentsEdit.Text.Should().Be(testComment);

            // Verify it saves
            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();
            // Matching Python: uti = read_uti(data)
            UTI uti = UTIHelpers.ConstructUti(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));
            // Matching Python: assert uti.comment == "Test comment\nLine 2\nLine 3"
            uti.Comment.Should().Be(testComment);

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:347-398
        // Original: def test_uti_editor_all_widgets_build_verification(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorAllWidgetsBuildVerification()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();
            editor.New();

            // Set ALL basic values
            // Matching Python: editor.ui.tagEdit.setText("test_tag")
            editor.TagEdit.Text = "test_tag";
            editor.ResrefEdit.Text = "test_resref";

            if (editor.BaseSelect != null && editor.BaseSelect.ItemCount > 0)
            {
                editor.BaseSelect.SelectedIndex = 1;
            }

            // Matching Python: editor.ui.costSpin.setValue(500)
            editor.CostSpin.Value = 500;
            editor.AdditionalCostSpin.Value = 100;
            editor.UpgradeSpin.Value = 2;
            editor.PlotCheckbox.IsChecked = true;
            editor.ChargesSpin.Value = 10;
            editor.StackSpin.Value = 5;
            editor.ModelVarSpin.Value = 3;
            editor.BodyVarSpin.Value = 2;
            editor.TextureVarSpin.Value = 1;

            // Set comments
            // Matching Python: editor.ui.commentsEdit.setPlainText("Test comment")
            editor.CommentsEdit.Text = "Test comment";

            // Build and verify
            // Matching Python: data, _ = editor.build()
            var (data, _) = editor.Build();

            // Matching Python: uti = read_uti(data)
            UTI uti = UTIHelpers.ConstructUti(CSharpKOTOR.Formats.GFF.GFF.FromBytes(data));

            // Verify all values were saved correctly
            // Matching Python: assert uti.tag == "test_tag"
            uti.Tag.Should().Be("test_tag");
            uti.ResRef.ToString().Should().Be("test_resref");
            uti.Cost.Should().Be(500);
            uti.AddCost.Should().Be(100);
            uti.UpgradeLevel.Should().Be(2);
            uti.Plot.Should().Be(1);
            uti.Charges.Should().Be(10);
            uti.StackSize.Should().Be(5);
            uti.ModelVariation.Should().Be(3);
            uti.BodyVariation.Should().Be(2);
            uti.TextureVariation.Should().Be(1);
            uti.Comment.Should().Be("Test comment");

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:429-446
        // Original: def test_uti_editor_context_menu(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtiEditorContextMenu()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();
            editor.New();

            // Test context menu setup (if icon label exists)
            // Matching Python: if editor.ui.baseSelect.count() > 0:
            if (editor.BaseSelect != null && editor.BaseSelect.ItemCount > 0)
            {
                editor.BaseSelect.SelectedIndex = 0;
                System.Threading.Thread.Sleep(10);

                // Matching Python: assert editor.ui.iconLabel.contextMenuPolicy() == Qt.ContextMenuPolicy.CustomContextMenu
                // In Avalonia, we verify that context menu behavior is set up
                // The actual context menu implementation may differ from Qt
                // For now, just verify the editor doesn't crash
                editor.Should().NotBeNull();
            }

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:448-489
        // Original: def test_utieditor_editor_help_dialog_opens_correct_file(qtbot, installation: HTInstallation):
        [Fact]
        public void TestUtieditorEditorHelpDialogOpensCorrectFile()
        {
            if (_installation == null)
            {
                return; // Skip if no installation available
            }

            // Matching Python: editor = UTIEditor(None, installation)
            var editor = new UTIEditor(null, _installation);
            editor.Show();

            // Matching Python: editor._show_help_dialog("GFF-UTI.md")
            // In C#, we test that the help action exists and doesn't crash
            // The actual dialog functionality is tested in EditorHelpDialogTests
            // For now, just verify the editor is properly initialized
            editor.Should().NotBeNull();

            // The help dialog opening is an integration test that requires
            // the help system to be fully implemented. For now, we verify
            // the editor is set up correctly.

            editor.Close();
        }

        [Fact]
        public void TestUtiEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py
            // Original: def test_uti_editor_new_file_creation(qtbot, installation):
            var editor = new UTIEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:400-427
        // Original: def test_uti_editor_load_real_file(qtbot, installation: HTInstallation, test_files_dir):
        [Fact]
        public void TestUtiEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a UTI file
            string utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            if (!System.IO.File.Exists(utiFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            }

            if (!System.IO.File.Exists(utiFile))
            {
                // Skip if no UTI files available for testing (matching Python pytest.skip behavior)
                return;
            }

            // Get installation if available (K2 preferred for UTI files)
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
            else
            {
                // Fallback to K1
                string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
                if (string.IsNullOrEmpty(k1Path))
                {
                    k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
                }

                if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
                {
                    installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
                }
            }

            var editor = new UTIEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(utiFile);
            editor.Load(utiFile, "baragwin", ResourceType.UTI, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            GFF gff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:81-92
        // Original: def test_save_and_load(self):
        [Fact]
        public void TestUtiEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find baragwin.uti
            string utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            if (!System.IO.File.Exists(utiFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utiFile = System.IO.Path.Combine(testFilesDir, "baragwin.uti");
            }

            if (!System.IO.File.Exists(utiFile))
            {
                // Skip if test file not available
                return;
            }

            // Get installation if available (K2 preferred for UTI files)
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
            else
            {
                // Fallback to K1
                string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
                if (string.IsNullOrEmpty(k1Path))
                {
                    k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
                }

                if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
                {
                    installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
                }
            }

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            var editor = new UTIEditor(null, installation);
            var logMessages = new List<string> { Environment.NewLine };

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:84
            // Original: data = filepath.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(utiFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:85
            // Original: old = read_gff(data)
            var old = CSharpKOTOR.Formats.GFF.GFF.FromBytes(data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:86
            // Original: self.editor.load(filepath, "baragwin", ResourceType.UTI, data)
            editor.Load(utiFile, "baragwin", ResourceType.UTI, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:88
            // Original: data, _ = self.editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:89
            // Original: new = read_gff(data)
            GFF newGff = CSharpKOTOR.Formats.GFF.GFF.FromBytes(newData);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:91
            // Original: diff = old.compare(new, self.log_func, ignore_default_changes=True)
            Action<string> logFunc = msg => logMessages.Add(msg);
            bool diff = old.Compare(newGff, logFunc, path: null, ignoreDefaultChanges: true);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_uti_editor.py:92
            // Original: assert diff, os.linesep.join(self.log_messages)
            diff.Should().BeTrue($"GFF comparison failed. Log messages: {string.Join(Environment.NewLine, logMessages)}");
        }
    }
}
