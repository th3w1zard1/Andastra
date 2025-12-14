using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using CSharpKOTOR.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py
    // Original: Comprehensive tests for FileSearcher dialog
    [Collection("Avalonia Test Collection")]
    public class FileSearcherDialogTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public FileSearcherDialogTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static FileSearcherDialogTests()
        {
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            if (!string.IsNullOrEmpty(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                _installation = new HTInstallation(k1Path, "Test");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:14-61
        // Original: def test_search_dialog_all_widgets_exist(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestSearchDialogAllWidgetsExist()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();
            var installations = new Dictionary<string, HTInstallation> { { "Test Install", _installation } };
            var dialog = new FileSearcherDialog(parent, installations);
            dialog.Show();

            // Installation selector
            dialog.Ui.InstallationSelect.Should().NotBeNull();

            // Search text
            dialog.Ui.SearchTextEdit.Should().NotBeNull();

            // Radio buttons
            dialog.Ui.CaseSensitiveRadio.Should().NotBeNull();
            dialog.Ui.CaseInsensitiveRadio.Should().NotBeNull();

            // Checkboxes
            dialog.Ui.FilenamesOnlyCheck.Should().NotBeNull();
            dialog.Ui.CoreCheck.Should().NotBeNull();
            dialog.Ui.ModulesCheck.Should().NotBeNull();
            dialog.Ui.OverrideCheck.Should().NotBeNull();
            dialog.Ui.SelectAllCheck.Should().NotBeNull();

            // ALL resource type checkboxes
            dialog.Ui.TypeARECheck.Should().NotBeNull();
            dialog.Ui.TypeGITCheck.Should().NotBeNull();
            dialog.Ui.TypeIFOCheck.Should().NotBeNull();
            dialog.Ui.TypeVISCheck.Should().NotBeNull();
            dialog.Ui.TypeLYTCheck.Should().NotBeNull();
            dialog.Ui.TypeDLGCheck.Should().NotBeNull();
            dialog.Ui.TypeJRLCheck.Should().NotBeNull();
            dialog.Ui.TypeUTCCheck.Should().NotBeNull();
            dialog.Ui.TypeUTDCheck.Should().NotBeNull();
            dialog.Ui.TypeUTECheck.Should().NotBeNull();
            dialog.Ui.TypeUTICheck.Should().NotBeNull();
            dialog.Ui.TypeUTPCheck.Should().NotBeNull();
            dialog.Ui.TypeUTMCheck.Should().NotBeNull();
            dialog.Ui.TypeUTSCheck.Should().NotBeNull();
            dialog.Ui.TypeUTTCheck.Should().NotBeNull();
            dialog.Ui.TypeUTWCheck.Should().NotBeNull();
            dialog.Ui.Type2DACheck.Should().NotBeNull();
            dialog.Ui.TypeNSSCheck.Should().NotBeNull();
            dialog.Ui.TypeNCSCheck.Should().NotBeNull();

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:63-137
        // Original: def test_search_dialog_all_checkboxes_exhaustive(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestSearchDialogAllCheckboxesExhaustive()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();
            var installations = new Dictionary<string, HTInstallation> { { "Test Install", _installation } };
            var dialog = new FileSearcherDialog(parent, installations);
            dialog.Show();

            // Test Select All checkbox - should toggle ALL type checkboxes
            dialog.Ui.SelectAllCheck.IsChecked = true;
            dialog.Ui.TypeARECheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeGITCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeIFOCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeVISCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeLYTCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeDLGCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeJRLCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTCCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTDCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTECheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTICheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTPCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTMCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTSCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTTCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTWCheck.IsChecked.Should().BeTrue();
            dialog.Ui.Type2DACheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeNSSCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeNCSCheck.IsChecked.Should().BeTrue();

            dialog.Ui.SelectAllCheck.IsChecked = false;
            dialog.Ui.TypeARECheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeGITCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeIFOCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeVISCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeLYTCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeDLGCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeJRLCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTCCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTDCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTECheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTICheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTPCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTMCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTSCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTTCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeUTWCheck.IsChecked.Should().BeFalse();
            dialog.Ui.Type2DACheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeNSSCheck.IsChecked.Should().BeFalse();
            dialog.Ui.TypeNCSCheck.IsChecked.Should().BeFalse();

            // Test individual checkboxes
            dialog.Ui.TypeUTCCheck.IsChecked = true;
            dialog.Ui.TypeUTCCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeDLGCheck.IsChecked.Should().BeFalse(); // Others should remain unchecked

            dialog.Ui.TypeDLGCheck.IsChecked = true;
            dialog.Ui.TypeDLGCheck.IsChecked.Should().BeTrue();
            dialog.Ui.TypeUTCCheck.IsChecked.Should().BeTrue(); // Previous should remain

            // Test location checkboxes
            dialog.Ui.CoreCheck.IsChecked = true;
            dialog.Ui.CoreCheck.IsChecked.Should().BeTrue();

            dialog.Ui.ModulesCheck.IsChecked = true;
            dialog.Ui.ModulesCheck.IsChecked.Should().BeTrue();

            dialog.Ui.OverrideCheck.IsChecked = true;
            dialog.Ui.OverrideCheck.IsChecked.Should().BeTrue();

            dialog.Ui.FilenamesOnlyCheck.IsChecked = true;
            dialog.Ui.FilenamesOnlyCheck.IsChecked.Should().BeTrue();

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:139-158
        // Original: def test_search_dialog_all_radio_buttons(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestSearchDialogAllRadioButtons()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();
            var installations = new Dictionary<string, HTInstallation> { { "Test Install", _installation } };
            var dialog = new FileSearcherDialog(parent, installations);
            dialog.Show();

            // Test case sensitive radio
            dialog.Ui.CaseSensitiveRadio.IsChecked = true;
            dialog.Ui.CaseSensitiveRadio.IsChecked.Should().BeTrue();
            dialog.Ui.CaseInsensitiveRadio.IsChecked.Should().BeFalse();

            // Test case insensitive radio
            dialog.Ui.CaseInsensitiveRadio.IsChecked = true;
            dialog.Ui.CaseInsensitiveRadio.IsChecked.Should().BeTrue();
            dialog.Ui.CaseSensitiveRadio.IsChecked.Should().BeFalse();

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:160-337
        // Original: def test_search_dialog_query_construction_exhaustive(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestSearchDialogQueryConstructionExhaustive()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();
            var installations = new Dictionary<string, HTInstallation> { { "Test Install", _installation } };
            var dialog = new FileSearcherDialog(parent, installations);
            dialog.Show();

            // Test 1: All checkboxes checked
            dialog.Ui.SelectAllCheck.IsChecked = true;
            // Wait a moment for event to process (Avalonia processes events synchronously in tests)
            System.Threading.Thread.Sleep(10);
            dialog.Ui.SearchTextEdit.Text = "test_search";
            dialog.Ui.CaseSensitiveRadio.IsChecked = true;
            dialog.Ui.FilenamesOnlyCheck.IsChecked = true;
            dialog.Ui.CoreCheck.IsChecked = true;
            dialog.Ui.ModulesCheck.IsChecked = true;
            dialog.Ui.OverrideCheck.IsChecked = true;

            // Manually construct query to verify UI state (without calling OnSearch which triggers search)
            var checkTypes = new List<ResourceType>();
            if (dialog.Ui.TypeARECheck.IsChecked == true) checkTypes.Add(ResourceType.ARE);
            if (dialog.Ui.TypeGITCheck.IsChecked == true) checkTypes.Add(ResourceType.GIT);
            if (dialog.Ui.TypeIFOCheck.IsChecked == true) checkTypes.Add(ResourceType.IFO);
            if (dialog.Ui.TypeVISCheck.IsChecked == true) checkTypes.Add(ResourceType.VIS);
            if (dialog.Ui.TypeLYTCheck.IsChecked == true) checkTypes.Add(ResourceType.LYT);
            if (dialog.Ui.TypeDLGCheck.IsChecked == true) checkTypes.Add(ResourceType.DLG);
            if (dialog.Ui.TypeJRLCheck.IsChecked == true) checkTypes.Add(ResourceType.JRL);
            if (dialog.Ui.TypeUTCCheck.IsChecked == true) checkTypes.Add(ResourceType.UTC);
            if (dialog.Ui.TypeUTDCheck.IsChecked == true) checkTypes.Add(ResourceType.UTD);
            if (dialog.Ui.TypeUTECheck.IsChecked == true) checkTypes.Add(ResourceType.UTE);
            if (dialog.Ui.TypeUTICheck.IsChecked == true) checkTypes.Add(ResourceType.UTI);
            if (dialog.Ui.TypeUTPCheck.IsChecked == true) checkTypes.Add(ResourceType.UTP);
            if (dialog.Ui.TypeUTMCheck.IsChecked == true) checkTypes.Add(ResourceType.UTM);
            if (dialog.Ui.TypeUTSCheck.IsChecked == true) checkTypes.Add(ResourceType.UTS);
            if (dialog.Ui.TypeUTTCheck.IsChecked == true) checkTypes.Add(ResourceType.UTT);
            if (dialog.Ui.TypeUTWCheck.IsChecked == true) checkTypes.Add(ResourceType.UTW);
            if (dialog.Ui.Type2DACheck.IsChecked == true) checkTypes.Add(ResourceType.TwoDA);
            if (dialog.Ui.TypeNSSCheck.IsChecked == true) checkTypes.Add(ResourceType.NSS);
            if (dialog.Ui.TypeNCSCheck.IsChecked == true) checkTypes.Add(ResourceType.NCS);

            var query = new FileSearchQuery
            {
                Installation = dialog.Ui.GetCurrentInstallation(),
                CaseSensitive = dialog.Ui.CaseSensitiveRadio.IsChecked == true,
                FilenamesOnly = dialog.Ui.FilenamesOnlyCheck.IsChecked == true,
                Text = dialog.Ui.SearchTextEdit.Text,
                SearchCore = dialog.Ui.CoreCheck.IsChecked == true,
                SearchModules = dialog.Ui.ModulesCheck.IsChecked == true,
                SearchOverride = dialog.Ui.OverrideCheck.IsChecked == true,
                CheckTypes = checkTypes
            };

            query.Text.Should().Be("test_search");
            query.CaseSensitive.Should().BeTrue();
            query.FilenamesOnly.Should().BeTrue();
            query.SearchCore.Should().BeTrue();
            query.SearchModules.Should().BeTrue();
            query.SearchOverride.Should().BeTrue();
            query.CheckTypes.Count.Should().Be(19); // All types checked
            query.CheckTypes.Should().Contain(ResourceType.UTC);
            query.CheckTypes.Should().Contain(ResourceType.DLG);
            query.CheckTypes.Should().Contain(ResourceType.ARE);

            // Test 2: Only specific types checked
            dialog.Ui.SelectAllCheck.IsChecked = false;
            dialog.Ui.TypeUTCCheck.IsChecked = true;
            dialog.Ui.TypeDLGCheck.IsChecked = true;
            dialog.Ui.Type2DACheck.IsChecked = true;

            checkTypes = new List<ResourceType>();
            if (dialog.Ui.TypeARECheck.IsChecked == true) checkTypes.Add(ResourceType.ARE);
            if (dialog.Ui.TypeGITCheck.IsChecked == true) checkTypes.Add(ResourceType.GIT);
            if (dialog.Ui.TypeIFOCheck.IsChecked == true) checkTypes.Add(ResourceType.IFO);
            if (dialog.Ui.TypeVISCheck.IsChecked == true) checkTypes.Add(ResourceType.VIS);
            if (dialog.Ui.TypeLYTCheck.IsChecked == true) checkTypes.Add(ResourceType.LYT);
            if (dialog.Ui.TypeDLGCheck.IsChecked == true) checkTypes.Add(ResourceType.DLG);
            if (dialog.Ui.TypeJRLCheck.IsChecked == true) checkTypes.Add(ResourceType.JRL);
            if (dialog.Ui.TypeUTCCheck.IsChecked == true) checkTypes.Add(ResourceType.UTC);
            if (dialog.Ui.TypeUTDCheck.IsChecked == true) checkTypes.Add(ResourceType.UTD);
            if (dialog.Ui.TypeUTECheck.IsChecked == true) checkTypes.Add(ResourceType.UTE);
            if (dialog.Ui.TypeUTICheck.IsChecked == true) checkTypes.Add(ResourceType.UTI);
            if (dialog.Ui.TypeUTPCheck.IsChecked == true) checkTypes.Add(ResourceType.UTP);
            if (dialog.Ui.TypeUTMCheck.IsChecked == true) checkTypes.Add(ResourceType.UTM);
            if (dialog.Ui.TypeUTSCheck.IsChecked == true) checkTypes.Add(ResourceType.UTS);
            if (dialog.Ui.TypeUTTCheck.IsChecked == true) checkTypes.Add(ResourceType.UTT);
            if (dialog.Ui.TypeUTWCheck.IsChecked == true) checkTypes.Add(ResourceType.UTW);
            if (dialog.Ui.Type2DACheck.IsChecked == true) checkTypes.Add(ResourceType.TwoDA);
            if (dialog.Ui.TypeNSSCheck.IsChecked == true) checkTypes.Add(ResourceType.NSS);
            if (dialog.Ui.TypeNCSCheck.IsChecked == true) checkTypes.Add(ResourceType.NCS);

            var query2 = new FileSearchQuery
            {
                Installation = dialog.Ui.GetCurrentInstallation(),
                CaseSensitive = dialog.Ui.CaseSensitiveRadio.IsChecked == true,
                FilenamesOnly = dialog.Ui.FilenamesOnlyCheck.IsChecked == true,
                Text = dialog.Ui.SearchTextEdit.Text,
                SearchCore = dialog.Ui.CoreCheck.IsChecked == true,
                SearchModules = dialog.Ui.ModulesCheck.IsChecked == true,
                SearchOverride = dialog.Ui.OverrideCheck.IsChecked == true,
                CheckTypes = checkTypes
            };

            query2.CheckTypes.Count.Should().Be(3);
            query2.CheckTypes.Should().Contain(ResourceType.UTC);
            query2.CheckTypes.Should().Contain(ResourceType.DLG);
            query2.CheckTypes.Should().Contain(ResourceType.TwoDA);
            query2.CheckTypes.Should().NotContain(ResourceType.ARE);

            // Test 3: Case insensitive
            dialog.Ui.CaseInsensitiveRadio.IsChecked = true;
            var query3 = new FileSearchQuery
            {
                Installation = dialog.Ui.GetCurrentInstallation(),
                CaseSensitive = dialog.Ui.CaseSensitiveRadio.IsChecked == true,
                FilenamesOnly = dialog.Ui.FilenamesOnlyCheck.IsChecked == true,
                Text = dialog.Ui.SearchTextEdit.Text,
                SearchCore = dialog.Ui.CoreCheck.IsChecked == true,
                SearchModules = dialog.Ui.ModulesCheck.IsChecked == true,
                SearchOverride = dialog.Ui.OverrideCheck.IsChecked == true,
                CheckTypes = checkTypes
            };
            query3.CaseSensitive.Should().BeFalse();

            // Test 4: Only core, no modules/override
            dialog.Ui.CoreCheck.IsChecked = true;
            dialog.Ui.ModulesCheck.IsChecked = false;
            dialog.Ui.OverrideCheck.IsChecked = false;
            var query4 = new FileSearchQuery
            {
                Installation = dialog.Ui.GetCurrentInstallation(),
                CaseSensitive = dialog.Ui.CaseSensitiveRadio.IsChecked == true,
                FilenamesOnly = dialog.Ui.FilenamesOnlyCheck.IsChecked == true,
                Text = dialog.Ui.SearchTextEdit.Text,
                SearchCore = dialog.Ui.CoreCheck.IsChecked == true,
                SearchModules = dialog.Ui.ModulesCheck.IsChecked == true,
                SearchOverride = dialog.Ui.OverrideCheck.IsChecked == true,
                CheckTypes = checkTypes
            };
            query4.SearchCore.Should().BeTrue();
            query4.SearchModules.Should().BeFalse();
            query4.SearchOverride.Should().BeFalse();

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:339-365
        // Original: def test_search_dialog_installation_selector(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestSearchDialogInstallationSelector()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();
            var installations = new Dictionary<string, HTInstallation>
            {
                { "Installation 1", _installation },
                { "Installation 2", _installation } // Using same for test
            };
            var dialog = new FileSearcherDialog(parent, installations);
            dialog.Show();

            dialog.Ui.InstallationSelect.Items.Count.Should().Be(2);

            // Test switching installations
            dialog.Ui.InstallationSelect.SelectedIndex = 0;
            if (dialog.Ui.InstallationSelect.SelectedItem is ComboBoxItem item0)
            {
                item0.Content?.ToString().Should().Be("Installation 1");
            }

            dialog.Ui.InstallationSelect.SelectedIndex = 1;
            if (dialog.Ui.InstallationSelect.SelectedItem is ComboBoxItem item1)
            {
                item1.Content?.ToString().Should().Be("Installation 2");
            }

            // Verify data is correct
            dialog.Ui.GetCurrentInstallation().Should().Be(_installation);

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:367-393
        // Original: def test_search_dialog_text_input(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestSearchDialogTextInput()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();
            var installations = new Dictionary<string, HTInstallation> { { "Test Install", _installation } };
            var dialog = new FileSearcherDialog(parent, installations);
            dialog.Show();

            // Test various text inputs
            var testTexts = new[]
            {
                "",
                "simple",
                "test with spaces",
                "test_with_underscores",
                "test-with-dashes",
                "test123numbers",
                "TEST_UPPERCASE",
                "test\nmultiline",
                "special!@#$%^&*()chars",
            };

            foreach (var text in testTexts)
            {
                dialog.Ui.SearchTextEdit.Text = text;
                dialog.Ui.SearchTextEdit.Text.Should().Be(text);
            }

            dialog.Close();
        }
    }
}
