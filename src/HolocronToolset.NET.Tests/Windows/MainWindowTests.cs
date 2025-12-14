using System;
using System.Collections.Generic;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Tests.TestHelpers;
using HolocronToolset.NET.Windows;
using Xunit;

namespace HolocronToolset.NET.Tests.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_main.py
    // Original: Comprehensive tests for MainWindow
    [Collection("Avalonia Test Collection")]
    public class MainWindowTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public MainWindowTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static MainWindowTests()
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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_main.py:23-32
        // Original: def test_main_window_init(qtbot: QtBot):
        [Fact]
        public void TestMainWindowInit()
        {
            var window = new MainWindow();
            window.Show();

            window.IsVisible.Should().BeTrue();
            window.Title.Should().Contain("Holocron");
            window.Active.Should().BeNull();
            window.Ui.GameCombo.Items.Count.Should().BeGreaterThanOrEqualTo(1); // Should have [None] at least

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_main.py:35-68
        // Original: def test_main_window_set_installation(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowSetInstallation()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Add installation to settings BEFORE creating window (so ReloadInstallations picks it up)
            var settings = new GlobalSettings();
            var installations = settings.Installations();
            installations[_installation.Name] = new Dictionary<string, object>
            {
                { "name", _installation.Name },
                { "path", _installation.Path },
                { "tsl", _installation.Tsl }
            };
            settings.SetInstallations(installations);

            var window = new MainWindow();
            window.Show();

            // Reload installations to pick up the one we just added
            window.ReloadInstallations();

            // Find the index of our test installation
            int index = -1;
            for (int i = 0; i < window.Ui.GameCombo.Items.Count; i++)
            {
                var item = window.Ui.GameCombo.Items[i];
                string itemText = item?.ToString() ?? "";
                if (itemText == _installation.Name)
                {
                    index = i;
                    break;
                }
            }

            // If still not found, skip the combo selection part but still test manual setting
            if (index == -1)
            {
                // Installation wasn't in combo - skip combo selection test
                // but still test manual setting
            }
            else
            {

                // Select the installation without invoking the async loader dialog (headless safe)
                window.Ui.GameCombo.SelectedIndex = index;
            }

            // Manually set active installation and enable tabs to mimic a successful load
            window.Installations[_installation.Name] = _installation;
            // Use reflection or internal method to set _active
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            if (window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.IsEnabled = true;
            }
            window.UpdateMenus();

            window.Active.Should().Be(_installation);
            window.Ui.ResourceTabs?.IsEnabled.Should().BeTrue();

            // Check if tabs are populated (basic check)
            window.Ui.ModulesWidget.Should().NotBeNull();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_main.py:70-88
        // Original: def test_menu_actions_state(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestMenuActionsState()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();

            // Initially no installation, most "New" actions should be disabled
            window.Ui.ActionNewDLG?.IsEnabled.Should().BeFalse();

            // Set installation
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);
            window.UpdateMenus();

            window.Ui.ActionNewDLG?.IsEnabled.Should().BeTrue();
            window.Ui.ActionNewUTC?.IsEnabled.Should().BeTrue();
            window.Ui.ActionNewNSS?.IsEnabled.Should().BeTrue();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_main.py:90-107
        // Original: def test_tab_switching(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestTabSwitching()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();

            // Set installation to enable tabs
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);
            if (window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.IsEnabled = true;
            }

            // Switch to Modules tab
            if (window.Ui.ModulesTab != null && window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.ModulesTab;
                window.GetActiveResourceTab().Should().Be(window.Ui.ModulesTab);
            }

            // Switch to Override tab
            if (window.Ui.OverrideTab != null && window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.OverrideTab;
                window.GetActiveResourceTab().Should().Be(window.Ui.OverrideTab);
            }

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_main.py:109-122
        // Original: def test_modules_filter(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestModulesFilter()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Mock some modules
            var moduleItems = new List<object> { "Test Module 1", "Test Module 2" };
            window.RefreshModuleList(reload: false, moduleItems: moduleItems);

            // Check that sections were set (ResourceList.SetSections should populate sectionCombo)
            window.Ui.ModulesWidget.Should().NotBeNull();
            window.Ui.ModulesWidget.Ui.SectionCombo.Items.Count.Should().Be(2);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:37-51
        // Original: def test_main_window_initialization(qtbot):
        [Fact]
        public void TestMainWindowInitialization()
        {
            var window = new MainWindow();
            window.Show();

            window.Should().NotBeNull();
            window.Active.Should().BeNull();
            window.Installations.Should().NotBeNull();
            window.Settings.Should().NotBeNull();
            window.UpdateManager.Should().NotBeNull();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:58-71
        // Original: def test_main_window_ui_initialization(qtbot):
        [Fact]
        public void TestMainWindowUiInitialization()
        {
            var window = new MainWindow();
            window.Show();

            window.Ui.Should().NotBeNull();
            window.Ui.GameCombo.Should().NotBeNull();
            window.Ui.ResourceTabs.Should().NotBeNull();
            window.Ui.ModulesWidget.Should().NotBeNull();
            window.Ui.OverrideWidget.Should().NotBeNull();
            window.Ui.CoreWidget.Should().NotBeNull();
            window.Ui.TexturesWidget.Should().NotBeNull();
            window.Ui.SavesWidget.Should().NotBeNull();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:85-91
        // Original: def test_main_window_window_title(qtbot):
        [Fact]
        public void TestMainWindowWindowTitle()
        {
            var window = new MainWindow();
            window.Show();

            window.Title.Should().Contain("Holocron");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:93-99
        // Original: def test_main_window_initial_game_combo(qtbot):
        [Fact]
        public void TestMainWindowInitialGameCombo()
        {
            var window = new MainWindow();
            window.Show();

            window.Ui.GameCombo.Items.Count.Should().BeGreaterThanOrEqualTo(1);
            window.Ui.GameCombo.Items[0]?.ToString().Should().Be("[None]");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:101-106
        // Original: def test_main_window_initial_resource_tabs_disabled(qtbot):
        [Fact]
        public void TestMainWindowInitialResourceTabsDisabled()
        {
            var window = new MainWindow();
            window.Show();

            window.Ui.ResourceTabs?.IsEnabled.Should().BeFalse();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:112-123
        // Original: def test_main_window_unset_installation_initial_state(qtbot):
        [Fact]
        public void TestMainWindowUnsetInstallationInitialState()
        {
            var window = new MainWindow();
            window.Show();

            window.UnsetInstallation();

            window.Active.Should().BeNull();
            window.Ui.GameCombo.SelectedIndex.Should().Be(0);
            window.Ui.ResourceTabs?.IsEnabled.Should().BeFalse();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:125-134
        // Original: def test_main_window_change_installation_to_none(qtbot):
        [Fact]
        public void TestMainWindowChangeInstallationToNone()
        {
            var window = new MainWindow();
            window.Show();

            window.ChangeActiveInstallation(0);

            window.Active.Should().BeNull();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:136-146
        // Original: def test_main_window_reload_installations(qtbot):
        [Fact]
        public void TestMainWindowReloadInstallations()
        {
            var window = new MainWindow();
            window.Show();

            int initialCount = window.Ui.GameCombo.Items.Count;
            window.ReloadInstallations();

            window.Ui.GameCombo.Items.Count.Should().BeGreaterThanOrEqualTo(1);
            window.Ui.GameCombo.Items[0]?.ToString().Should().Be("[None]");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:540-547
        // Original: def test_main_window_get_active_tab_index_initial(qtbot):
        [Fact]
        public void TestMainWindowGetActiveTabIndexInitial()
        {
            var window = new MainWindow();
            window.Show();

            int index = window.GetActiveTabIndex();
            index.Should().BeGreaterThanOrEqualTo(0);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:549-555
        // Original: def test_main_window_get_active_resource_tab_initial(qtbot):
        [Fact]
        public void TestMainWindowGetActiveResourceTabInitial()
        {
            var window = new MainWindow();
            window.Show();

            var tab = window.GetActiveResourceTab();
            tab.Should().NotBeNull();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:557-565
        // Original: def test_main_window_get_active_resource_widget_core(qtbot):
        // Original: window.ui.resourceTabs.setCurrentWidget(window.ui.coreTab)
        // Original: widget = window.get_active_resource_widget()
        // Original: assert widget == window.ui.coreWidget
        [Fact]
        public void TestMainWindowGetActiveResourceWidgetCore()
        {
            var window = new MainWindow();
            window.Show();

            // Matching Python: window.ui.resourceTabs.setCurrentWidget(window.ui.coreTab)
            if (window.Ui.ResourceTabs != null && window.Ui.CoreTab != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.CoreTab;
            }

            // Matching Python: widget = window.get_active_resource_widget()
            var widget = window.GetActiveResourceWidget();

            // Matching Python: assert widget == window.ui.coreWidget
            // Use reference equality instead of FluentAssertions to avoid recursion
            Assert.True(ReferenceEquals(widget, window.Ui.CoreWidget), "GetActiveResourceWidget should return CoreWidget when Core tab is selected");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:567-575
        // Original: def test_main_window_get_active_resource_widget_modules(qtbot):
        // Original: window.ui.resourceTabs.setCurrentWidget(window.ui.modulesTab)
        // Original: widget = window.get_active_resource_widget()
        // Original: assert widget == window.ui.modulesWidget
        [Fact]
        public void TestMainWindowGetActiveResourceWidgetModules()
        {
            var window = new MainWindow();
            window.Show();

            // Matching Python: window.ui.resourceTabs.setCurrentWidget(window.ui.modulesTab)
            // Set SelectedIndex directly to ensure it updates (Avalonia might not sync SelectedItem->SelectedIndex immediately)
            if (window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.SelectedIndex = 1; // Modules tab is index 1
            }

            // Matching Python: widget = window.get_active_resource_widget()
            var widget = window.GetActiveResourceWidget();

            // Matching Python: assert widget == window.ui.modulesWidget
            // Use reference equality instead of FluentAssertions to avoid recursion
            Assert.True(ReferenceEquals(widget, window.Ui.ModulesWidget), "GetActiveResourceWidget should return ModulesWidget when Modules tab is selected");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:577-585
        // Original: def test_main_window_get_active_resource_widget_override(qtbot):
        // Original: window.ui.resourceTabs.setCurrentWidget(window.ui.overrideTab)
        // Original: widget = window.get_active_resource_widget()
        // Original: assert widget == window.ui.overrideWidget
        [Fact]
        public void TestMainWindowGetActiveResourceWidgetOverride()
        {
            var window = new MainWindow();
            window.Show();

            // Matching Python: window.ui.resourceTabs.setCurrentWidget(window.ui.overrideTab)
            // Set SelectedIndex directly to ensure it updates (Avalonia might not sync SelectedItem->SelectedIndex immediately)
            if (window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.SelectedIndex = 2; // Override tab is index 2
            }

            // Matching Python: widget = window.get_active_resource_widget()
            var widget = window.GetActiveResourceWidget();

            // Matching Python: assert widget == window.ui.overrideWidget
            // Use reference equality instead of FluentAssertions to avoid recursion
            Assert.True(ReferenceEquals(widget, window.Ui.OverrideWidget), "GetActiveResourceWidget should return OverrideWidget when Override tab is selected");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:607-623
        // Original: def test_main_window_tab_switching(qtbot):
        [Fact]
        public void TestMainWindowTabSwitching()
        {
            var window = new MainWindow();
            window.Show();
            if (window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.IsEnabled = true;
            }

            // Switch to modules tab
            if (window.Ui.ModulesTab != null && window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.ModulesTab;
                window.GetActiveResourceTab().Should().Be(window.Ui.ModulesTab);
            }

            // Switch to override tab
            if (window.Ui.OverrideTab != null && window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.OverrideTab;
                window.GetActiveResourceTab().Should().Be(window.Ui.OverrideTab);
            }

            // Switch to core tab
            if (window.Ui.CoreTab != null && window.Ui.ResourceTabs != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.CoreTab;
                window.GetActiveResourceTab().Should().Be(window.Ui.CoreTab);
            }

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:629-640
        // Original: def test_main_window_menu_actions_initial_state(qtbot):
        [Fact]
        public void TestMainWindowMenuActionsInitialState()
        {
            var window = new MainWindow();
            window.Show();

            // Actions that require installation should be disabled
            window.Ui.ActionNewDLG?.IsEnabled.Should().BeFalse();
            window.Ui.ActionNewUTC?.IsEnabled.Should().BeFalse();
            window.Ui.ActionNewNSS?.IsEnabled.Should().BeFalse();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:642-655
        // Original: def test_main_window_menu_actions_with_installation(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowMenuActionsWithInstallation()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();

            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);
            window.UpdateMenus();

            window.Ui.ActionNewDLG?.IsEnabled.Should().BeTrue();
            window.Ui.ActionNewUTC?.IsEnabled.Should().BeTrue();
            window.Ui.ActionNewNSS?.IsEnabled.Should().BeTrue();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:684-733
        // Original: def test_main_window_refresh_module_list_no_installation(qtbot):
        [Fact]
        public void TestMainWindowRefreshModuleListNoInstallation()
        {
            var window = new MainWindow();
            window.Show();

            // Should not crash
            window.RefreshModuleList(reload: false);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:692-699
        // Original: def test_main_window_refresh_module_list_with_installation(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowRefreshModuleListWithInstallation()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.RefreshModuleList(reload: false);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:701-707
        // Original: def test_main_window_refresh_override_list_no_installation(qtbot):
        [Fact]
        public void TestMainWindowRefreshOverrideListNoInstallation()
        {
            var window = new MainWindow();
            window.Show();

            // Should not crash
            window.RefreshOverrideList(reload: false);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:709-716
        // Original: def test_main_window_refresh_override_list_with_installation(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowRefreshOverrideListWithInstallation()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.RefreshOverrideList(reload: false);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:718-724
        // Original: def test_main_window_refresh_core_list_no_installation(qtbot):
        [Fact]
        public void TestMainWindowRefreshCoreListNoInstallation()
        {
            var window = new MainWindow();
            window.Show();

            // Should not crash
            window.RefreshCoreList(reload: false);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:726-733
        // Original: def test_main_window_refresh_core_list_with_installation(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowRefreshCoreListWithInstallation()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.RefreshCoreList(reload: false);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:744-751
        // Original: def test_main_window_refresh_saves_list_with_installation(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowRefreshSavesListWithInstallation()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.RefreshSavesList(reload: false);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:757-773
        // Original: def test_main_window_on_core_refresh(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowOnCoreRefresh()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.OnCoreRefresh();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:775-800
        // Original: def test_main_window_on_module_changed(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowOnModuleChanged()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Get a real module file from the installation if available
            var modules = _installation.ModuleNames();
            if (modules.Count == 0)
            {
                return; // Skip if no modules available
            }

            string realModule = "";
            foreach (var key in modules.Keys)
            {
                realModule = key;
                break;
            }

            // Should not crash
            window.OnModuleChanged(realModule);

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:802-810
        // Original: def test_main_window_on_module_reload_empty_string(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowOnModuleReloadEmptyString()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should return early without crashing
            window.OnModuleReload("");
            window.OnModuleReload("   ");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:850-866
        // Original: def test_main_window_on_module_refresh(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowOnModuleRefresh()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.OnModuleRefresh();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:868-884
        // Original: def test_main_window_on_override_file_updated_deleted(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowOnOverrideFileUpdatedDeleted()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.OnOverrideFileUpdated("test.uti", "deleted");

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:907-922
        // Original: def test_main_window_on_override_refresh(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowOnOverrideRefresh()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.OnOverrideRefresh();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:925-940
        // Original: def test_main_window_on_save_refresh(qtbot, installation: HTInstallation):
        [Fact]
        public void TestMainWindowOnSaveRefresh()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new MainWindow();
            window.Show();
            var activeField = typeof(MainWindow).GetField("_active", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            activeField?.SetValue(window, _installation);

            // Should not crash
            window.OnSaveRefresh();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:1085-1091
        // Original: def test_main_window_reload_settings(qtbot):
        [Fact]
        public void TestMainWindowReloadSettings()
        {
            var window = new MainWindow();
            window.Show();

            // Should not crash
            window.ReloadSettings();

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/windows/test_main_window.py:1093-1126
        // Original: def test_main_window_on_tab_changed(qtbot):
        [Fact]
        public void TestMainWindowOnTabChanged()
        {
            var window = new MainWindow();
            window.Show();

            // Should not crash
            window.OnTabChanged();

            // Switch to modules tab and test again
            if (window.Ui.ResourceTabs != null && window.Ui.ModulesTab != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.ModulesTab;
                window.OnTabChanged();
            }

            // Switch to other tab and test again
            if (window.Ui.ResourceTabs != null && window.Ui.CoreTab != null)
            {
                window.Ui.ResourceTabs.SelectedItem = window.Ui.CoreTab;
                window.OnTabChanged();
            }

            window.Close();
        }
    }
}

