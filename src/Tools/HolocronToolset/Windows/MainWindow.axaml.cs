using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Utils;
using HolocronToolset.Widgets;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using FileResource = Andastra.Parsing.Extract.FileResource;
using Control = Avalonia.Controls.Control;
using ResourceList = HolocronToolset.Widgets.ResourceList;
using MenuItem = Avalonia.Controls.MenuItem;
using TabItem = Avalonia.Controls.TabItem;
using TabControl = Avalonia.Controls.TabControl;
using Button = Avalonia.Controls.Button;
using GlobalSettings = HolocronToolset.Data.GlobalSettings;
using UpdateManager = HolocronToolset.Windows.UpdateManager;

namespace HolocronToolset.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:199
    // Original: class ToolWindow(QMainWindow):
    public partial class MainWindow : Window
    {
        private HTInstallation _active;
        private Dictionary<string, HTInstallation> _installations;
        private GlobalSettings _settings;
        private int _previousGameComboIndex;
        private UpdateManager _updateManager;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:227
        // Original: self.update_manager: UpdateManager = UpdateManager(silent=True)
        public UpdateManager UpdateManager => _updateManager;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
        // Original: self.ui = Ui_MainWindow(); self.ui.setupUi(self)
        public MainWindowUi Ui { get; private set; }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:206
        // Original: self.active: HTInstallation | None = None
        public HTInstallation Active => _active;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:207
        // Original: self.installations: dict[str, HTInstallation] = {}
        public Dictionary<string, HTInstallation> Installations => _installations;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:208
        // Original: self.settings: GlobalSettings = GlobalSettings()
        public GlobalSettings Settings => _settings;

        // UI Widgets - will be populated from XAML or created programmatically
        private ComboBox _gameCombo;
        private TabControl _resourceTabs;
        private ResourceList _coreWidget;
        private ResourceList _modulesWidget;
        private ResourceList _overrideWidget;
        private ResourceList _savesWidget;
        private ResourceList _texturesWidget;
        private Button _openButton;
        private Button _extractButton;
        private Button _specialActionButton;
        private Button _erfEditorButton;
        private TabItem _coreTab;
        private TabItem _modulesTab;
        private TabItem _overrideTab;
        private MenuItem _actionNewDLG;
        private MenuItem _actionNewUTC;
        private MenuItem _actionNewNSS;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:206-276
        // Original: def __init__(self):
        public MainWindow()
        {
            InitializeComponent();
            _active = null;
            _installations = new Dictionary<string, HTInstallation>();
            _settings = new GlobalSettings();
            _previousGameComboIndex = 0;
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:227
            // Original: self.update_manager: UpdateManager = UpdateManager(silent=True)
            _updateManager = new UpdateManager(silent: true);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:821
            // Original: self.setWindowTitle(f"{tr('Holocron Toolset')} ({qtpy.API_NAME})")
            Title = "Holocron Toolset";

            SetupUI();
            SetupSignals();
            ReloadSettings();
            UnsetInstallation();
        }

        private void InitializeComponent()
        {
            bool xamlLoaded = false;
            try
            {
                AvaloniaXamlLoader.Load(this);
                xamlLoaded = true;
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            // Try to find controls from XAML (with try-catch for each to handle test scenarios)
            try
            {
                _gameCombo = this.FindControl<ComboBox>("gameCombo");
            }
            catch { }

            try
            {
                _resourceTabs = this.FindControl<TabControl>("resourceTabs");
            }
            catch { }

            try
            {
                _openButton = this.FindControl<Button>("openButton");
            }
            catch { }

            try
            {
                _extractButton = this.FindControl<Button>("extractButton");
            }
            catch { }

            try
            {
                _specialActionButton = this.FindControl<Button>("specialActionButton");
            }
            catch { }

            try
            {
                _erfEditorButton = this.FindControl<Button>("erfEditorButton");
            }
            catch { }

            // Find resource list widgets
            try
            {
                _coreWidget = this.FindControl<ResourceList>("coreWidget");
            }
            catch { }

            try
            {
                _modulesWidget = this.FindControl<ResourceList>("modulesWidget");
            }
            catch { }

            try
            {
                _overrideWidget = this.FindControl<ResourceList>("overrideWidget");
            }
            catch { }

            try
            {
                _savesWidget = this.FindControl<ResourceList>("savesWidget");
            }
            catch { }

            try
            {
                _texturesWidget = this.FindControl<ResourceList>("texturesWidget");
            }
            catch { }

            // Find tab items
            try
            {
                _coreTab = this.FindControl<TabItem>("coreTab");
            }
            catch { }

            try
            {
                _modulesTab = this.FindControl<TabItem>("modulesTab");
            }
            catch { }

            try
            {
                _overrideTab = this.FindControl<TabItem>("overrideTab");
            }
            catch { }

            // Find menu items
            try
            {
                _actionNewDLG = this.FindControl<MenuItem>("actionNewDLG");
            }
            catch { }

            try
            {
                _actionNewUTC = this.FindControl<MenuItem>("actionNewUTC");
            }
            catch { }

            try
            {
                _actionNewNSS = this.FindControl<MenuItem>("actionNewNSS");
            }
            catch { }

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
            }

            // Initially hide ERF editor button (matching PyKotor: self.erf_editor_button.hide())
            if (_erfEditorButton != null)
            {
                _erfEditorButton.IsVisible = false;
            }
        }

        private void SetupProgrammaticUI()
        {
            // Create basic UI structure programmatically
            var mainPanel = new StackPanel();

            // Game selection combo
            _gameCombo = new ComboBox();
            _gameCombo.Items.Add("[None]");
            mainPanel.Children.Add(_gameCombo);

            // Resource tabs
            _resourceTabs = new TabControl();
            _resourceTabs.Items.Add(new TabItem { Header = "Core", Content = new TextBlock { Text = "Core Tab" } });
            _resourceTabs.Items.Add(new TabItem { Header = "Modules", Content = new TextBlock { Text = "Modules Tab" } });
            _resourceTabs.Items.Add(new TabItem { Header = "Override", Content = new TextBlock { Text = "Override Tab" } });
            _resourceTabs.Items.Add(new TabItem { Header = "Textures", Content = new TextBlock { Text = "Textures Tab" } });
            _resourceTabs.Items.Add(new TabItem { Header = "Saves", Content = new TextBlock { Text = "Saves Tab" } });
            mainPanel.Children.Add(_resourceTabs);

            // Action buttons
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
            _openButton = new Button { Content = "Open Selected" };
            _extractButton = new Button { Content = "Extract Selected" };
            _specialActionButton = new Button { Content = "Designer" };
            _erfEditorButton = new Button { Content = "ERF Editor" };
            buttonPanel.Children.Add(_openButton);
            buttonPanel.Children.Add(_extractButton);
            buttonPanel.Children.Add(_specialActionButton);
            buttonPanel.Children.Add(_erfEditorButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Initialize widgets if not already done
            if (_coreWidget == null)
            {
                _coreWidget = new ResourceList();
            }
            if (_modulesWidget == null)
            {
                _modulesWidget = new ResourceList();
            }
            if (_overrideWidget == null)
            {
                _overrideWidget = new ResourceList();
            }
            if (_savesWidget == null)
            {
                _savesWidget = new ResourceList();
            }
            if (_texturesWidget == null)
            {
                _texturesWidget = new ResourceList();
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
            // Original: self.ui = Ui_MainWindow(); self.ui.setupUi(self)
            // Create UI wrapper exposing all controls
            Ui = new MainWindowUi
            {
                GameCombo = _gameCombo,
                ResourceTabs = _resourceTabs,
                CoreWidget = _coreWidget,
                ModulesWidget = _modulesWidget,
                OverrideWidget = _overrideWidget,
                SavesWidget = _savesWidget,
                TexturesWidget = _texturesWidget,
                CoreTab = _coreTab,
                ModulesTab = _modulesTab,
                OverrideTab = _overrideTab,
                ActionNewDLG = _actionNewDLG,
                ActionNewUTC = _actionNewUTC,
                ActionNewNSS = _actionNewNSS
            };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:485-665
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_gameCombo != null)
            {
                _gameCombo.SelectionChanged += (sender, e) =>
                {
                    if (_gameCombo.SelectedIndex >= 0)
                    {
                        ChangeActiveInstallation(_gameCombo.SelectedIndex);
                    }
                };
            }

            if (_openButton != null)
            {
                _openButton.Click += (sender, e) => OnOpenResources(GetActiveResourceWidget().SelectedResources());
            }

            if (_extractButton != null)
            {
                _extractButton.Click += (sender, e) => OnExtractResources(GetActiveResourceWidget().SelectedResources());
            }

            if (_specialActionButton != null)
            {
                _specialActionButton.Click += (sender, e) => OpenModuleDesigner();
            }

            if (_erfEditorButton != null)
            {
                _erfEditorButton.Click += (sender, e) => OpenModuleTabErfEditor();
            }

            // Connect tab control selection changed event
            if (_resourceTabs != null)
            {
                _resourceTabs.SelectionChanged += (sender, e) => OnTabChanged();
            }

            // Connect ResourceList events (matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:495-555)
            ConnectResourceListEvents();

            // Connect menu actions from XAML
            ConnectMenuActions();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:495-555
        // Original: Connect signals from ResourceList widgets to MainWindow handlers
        private void ConnectResourceListEvents()
        {
            // Connect coreWidget events (matching PyKotor lines 495-497)
            if (_coreWidget != null)
            {
                _coreWidget.RefreshClicked += (sender, e) => OnCoreRefresh();
                _coreWidget.ResourceDoubleClicked += (sender, e) => OnOpenResources(e.Resources, e.UseSpecializedEditor);
            }

            // Connect modulesWidget events (matching PyKotor lines 499-503)
            if (_modulesWidget != null)
            {
                _modulesWidget.SectionChanged += (sender, section) => OnModuleChanged(section);
                _modulesWidget.ReloadClicked += (sender, section) => OnModuleReload(section);
                _modulesWidget.RefreshClicked += (sender, e) => OnModuleRefresh();
                _modulesWidget.ResourceDoubleClicked += (sender, e) => OnOpenResources(e.Resources, e.UseSpecializedEditor);
            }

            // Connect savesWidget events (matching PyKotor lines 506-510)
            if (_savesWidget != null)
            {
                _savesWidget.SectionChanged += (sender, section) => OnSavePathChanged(section);
                _savesWidget.ReloadClicked += (sender, section) => OnSaveReload(section);
                _savesWidget.RefreshClicked += (sender, e) => OnSaveRefresh();
                _savesWidget.ResourceDoubleClicked += (sender, e) => OnOpenResources(e.Resources, e.UseSpecializedEditor);
            }

            // Connect overrideWidget events (matching PyKotor lines 546-550)
            if (_overrideWidget != null)
            {
                _overrideWidget.SectionChanged += (sender, section) => OnOverrideChanged(section);
                _overrideWidget.ReloadClicked += (sender, section) => OnOverrideReload(section);
                _overrideWidget.RefreshClicked += (sender, e) => OnOverrideRefresh();
                _overrideWidget.ResourceDoubleClicked += (sender, e) => OnOpenResources(e.Resources, e.UseSpecializedEditor);
            }

            // Connect texturesWidget events (matching PyKotor lines 553-554)
            if (_texturesWidget != null)
            {
                _texturesWidget.SectionChanged += (sender, section) => OnTexturesChanged(section);
                _texturesWidget.ResourceDoubleClicked += (sender, e) => OnOpenResources(e.Resources, e.UseSpecializedEditor);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:485-665
        // Original: Connect menu actions
        private void ConnectMenuActions()
        {
            // Find menu items from XAML and connect them
            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // File menu
                var actionNewTLK = this.FindControl<MenuItem>("actionNewTLK");
                if (actionNewTLK != null)
                {
                    actionNewTLK.Click += (s, e) => { /* TODO: Open TLK editor */ };
                }

                var actionSettings = this.FindControl<MenuItem>("actionSettings");
                if (actionSettings != null)
                {
                    actionSettings.Click += (s, e) => OpenSettingsDialog();
                }

                var actionExit = this.FindControl<MenuItem>("actionExit");
                if (actionExit != null)
                {
                    actionExit.Click += (s, e) => Close();
                }

                var openAction = this.FindControl<MenuItem>("openAction");
                if (openAction != null)
                {
                    openAction.Click += (s, e) => OpenFromFile();
                }

                // Help menu
                var actionHelpAbout = this.FindControl<MenuItem>("actionHelpAbout");
                if (actionHelpAbout != null)
                {
                    actionHelpAbout.Click += (s, e) => OpenAboutDialog();
                }

                var actionHelpUpdates = this.FindControl<MenuItem>("actionHelpUpdates");
                if (actionHelpUpdates != null)
                {
                    actionHelpUpdates.Click += (s, e) => _updateManager?.CheckForUpdates(silent: false);
                }

                var actionInstructions = this.FindControl<MenuItem>("actionInstructions");
                if (actionInstructions != null)
                {
                    actionInstructions.Click += (s, e) => OpenInstructionsWindow();
                }

                // Tools menu
                var actionModuleDesigner = this.FindControl<MenuItem>("actionModuleDesigner");
                if (actionModuleDesigner != null)
                {
                    actionModuleDesigner.Click += (s, e) => OpenModuleDesigner();
                }

                var actionFileSearch = this.FindControl<MenuItem>("actionFileSearch");
                if (actionFileSearch != null)
                {
                    actionFileSearch.Click += (s, e) => OpenFileSearchDialog();
                }

                var actionCloneModule = this.FindControl<MenuItem>("actionCloneModule");
                if (actionCloneModule != null)
                {
                    actionCloneModule.Click += (s, e) => OpenCloneModuleDialog();
                }

                // Edit menu
                var actionEditTLK = this.FindControl<MenuItem>("actionEditTLK");
                if (actionEditTLK != null)
                {
                    actionEditTLK.Click += (s, e) => OpenActiveTalktable();
                }

                var actionEditJRL = this.FindControl<MenuItem>("actionEditJRL");
                if (actionEditJRL != null)
                {
                    actionEditJRL.Click += (s, e) => OpenActiveJournal();
                }
            }
            catch
            {
                // XAML controls not available - menu actions will not be connected in test scenarios
                // This is acceptable for headless test environments
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:823-846
        // Original: def on_open_resources(...):
        private void OnOpenResources(List<FileResource> resources, bool? useSpecializedEditor = null)
        {
            if (_active == null || resources == null || resources.Count == 0)
            {
                return;
            }

            foreach (var resource in resources)
            {
                WindowUtils.OpenResourceEditor(
                    resource.FilePath,
                    resource.ResName,
                    resource.ResType,
                    resource.GetData(),
                    _active,
                    this,
                    useSpecializedEditor);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1952-2007
        // Original: def on_extract_resources(...):
        private void OnExtractResources(List<FileResource> resources)
        {
            // Extract resources - will be implemented when file dialogs are available
            // For now, just log
            System.Console.WriteLine($"Extracting {resources?.Count ?? 0} resources");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1131-1259
        // Original: def change_active_installation(...):
        public void ChangeActiveInstallation(int index)
        {
            if (index < 0)
            {
                return;
            }

            int prevIndex = _previousGameComboIndex;
            if (index == 0)
            {
                UnsetInstallation();
                _previousGameComboIndex = 0;
                return;
            }

            string name = _gameCombo?.SelectedItem?.ToString() ?? "";
            if (string.IsNullOrEmpty(name) || name == "[None]")
            {
                return;
            }

            // Get installation path from settings
            var installations = _settings.Installations();
            if (!installations.ContainsKey(name))
            {
                // Installation not configured - would prompt user in full implementation
                _gameCombo.SelectedIndex = prevIndex;
                return;
            }

            var installData = installations[name];
            string path = installData.ContainsKey("path") ? installData["path"]?.ToString() ?? "" : "";
            bool tsl = installData.ContainsKey("tsl") && installData["tsl"] is bool tslVal && tslVal;

            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                // Path not set or invalid - would prompt user in full implementation
                _gameCombo.SelectedIndex = prevIndex;
                return;
            }

            // Create or get installation
            if (!_installations.ContainsKey(name))
            {
                _active = new HTInstallation(path, name, tsl);
                _installations[name] = _active;
            }
            else
            {
                _active = _installations[name];
            }

            // Enable tabs
            if (_resourceTabs != null)
            {
                _resourceTabs.IsEnabled = true;
            }

            // Refresh lists
            RefreshCoreList(reload: false);
            RefreshSavesList(reload: false);
            RefreshModuleList(reload: false);
            RefreshOverrideList(reload: false);

            UpdateMenus();
            _previousGameComboIndex = index;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1657-1700
        // Original: def unset_installation(self):
        public void UnsetInstallation()
        {
            if (_gameCombo != null)
            {
                _gameCombo.SelectionChanged -= (sender, e) => { };
                _gameCombo.SelectedIndex = 0;
            }

            if (_coreWidget != null)
            {
                _coreWidget.SetResources(new List<FileResource>());
            }
            if (_modulesWidget != null)
            {
                _modulesWidget.SetResources(new List<FileResource>());
            }
            if (_overrideWidget != null)
            {
                _overrideWidget.SetResources(new List<FileResource>());
            }

            if (_resourceTabs != null)
            {
                _resourceTabs.IsEnabled = false;
            }

            UpdateMenus();
            _active = null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1370-1432
        // Original: def update_menus(self):
        public void UpdateMenus()
        {
            // Update menu states based on active installation
            // Enable/disable menu items based on whether installation is active
            bool hasInstallation = _active != null;

            if (_actionNewDLG != null)
            {
                _actionNewDLG.IsEnabled = hasInstallation;
            }
            if (_actionNewUTC != null)
            {
                _actionNewUTC.IsEnabled = hasInstallation;
            }
            if (_actionNewNSS != null)
            {
                _actionNewNSS.IsEnabled = hasInstallation;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1705-1716
        // Original: def refresh_core_list(...):
        public void RefreshCoreList(bool reload = true)
        {
            if (_active == null || _coreWidget == null)
            {
                return;
            }

            try
            {
                // Get core resources from installation
                var resources = _active.CoreResources();
                _coreWidget.SetResources(resources);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to refresh core list: {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1851-1869
        // Original: def refresh_saves_list(...):
        public void RefreshSavesList(bool reload = true)
        {
            if (_active == null || _savesWidget == null)
            {
                return;
            }

            try
            {
                // Get saves from installation
                var saveLocations = _active.SaveLocations();
                var sections = new List<string>();
                foreach (var location in saveLocations)
                {
                    sections.Add(location);
                }
                _savesWidget.SetSections(sections);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to refresh saves list: {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1721-1740
        // Original: def refresh_module_list(...):
        public void RefreshModuleList(bool reload = true, List<object> moduleItems = null)
        {
            if (_active == null || _modulesWidget == null)
            {
                return;
            }

            try
            {
                if (moduleItems != null)
                {
                    // Use provided module items (for testing)
                    var sections = new List<string>();
                    foreach (var item in moduleItems)
                    {
                        sections.Add(item.ToString());
                    }
                    _modulesWidget.SetSections(sections);
                }
                else
                {
                    // Get modules from installation
                    var moduleNames = _active.ModuleNames();
                    var sections = new List<string>();
                    foreach (var moduleName in moduleNames.Keys)
                    {
                        sections.Add(moduleName);
                    }
                    _modulesWidget.SetSections(sections);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to refresh module list: {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1832-1840
        // Original: def refresh_override_list(...):
        public void RefreshOverrideList(bool reload = true)
        {
            if (_active == null || _overrideWidget == null)
            {
                return;
            }

            try
            {
                // Get override directories from installation
                var overrideList = _active.OverrideList();
                var sections = new List<string>();
                foreach (var dir in overrideList)
                {
                    sections.Add(dir);
                }
                _overrideWidget.SetSections(sections);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to refresh override list: {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1581-1583
        // Original: def reload_settings(self):
        public void ReloadSettings()
        {
            ReloadInstallations();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1646-1654
        // Original: def reload_installations(self):
        public void ReloadInstallations()
        {
            if (_gameCombo == null)
            {
                return;
            }

            _gameCombo.Items.Clear();
            _gameCombo.Items.Add("[None]");

            var installations = _settings.Installations();
            foreach (var installName in installations.Keys)
            {
                _gameCombo.Items.Add(installName);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1567-1579
        // Original: def get_active_resource_widget(self):
        public ResourceList GetActiveResourceWidget()
        {
            if (_resourceTabs == null)
            {
                return _coreWidget ?? new ResourceList();
            }

            int currentIndex = _resourceTabs.SelectedIndex;
            if (currentIndex == 0)
            {
                return _coreWidget ?? new ResourceList();
            }
            else if (currentIndex == 1)
            {
                return _modulesWidget ?? new ResourceList();
            }
            else if (currentIndex == 2)
            {
                return _overrideWidget ?? new ResourceList();
            }
            else if (currentIndex == 3)
            {
                return _texturesWidget ?? new ResourceList();
            }
            else if (currentIndex == 4)
            {
                return _savesWidget ?? new ResourceList();
            }

            return _coreWidget ?? new ResourceList();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:100-106
        // Original: def get_active_resource_tab(self):
        public Control GetActiveResourceTab()
        {
            if (_resourceTabs?.SelectedItem is TabItem selectedTab)
            {
                return selectedTab;
            }
            return _coreTab;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
        // Original: def get_active_tab_index(self):
        public int GetActiveTabIndex()
        {
            if (_resourceTabs != null)
            {
                return _resourceTabs.SelectedIndex;
            }
            return 0;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1606-1611
        // Original: def on_tab_changed(self):
        public void OnTabChanged()
        {
            // Handle tab change - update UI state based on active tab
            // Show/hide ERF editor button on modules tab
            if (_resourceTabs?.SelectedItem == _modulesTab)
            {
                // Show ERF editor button when on modules tab
                if (_erfEditorButton != null)
                {
                    _erfEditorButton.IsVisible = true;
                }
            }
            else
            {
                // Hide ERF editor button when not on modules tab
                if (_erfEditorButton != null)
                {
                    _erfEditorButton.IsVisible = false;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1439-1455
        // Original: def open_module_designer(self):
        private void OpenModuleDesigner()
        {
            if (_active == null)
            {
                return;
            }

            // Module designer will be implemented when available
            System.Console.WriteLine("Module Designer not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:2166-2177
        // Original: def open_from_file(self):
        private void OpenFromFile()
        {
            // File dialog will be implemented when available
            System.Console.WriteLine("Open from file not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:873-875
        // Original: def on_core_refresh(self):
        public void OnCoreRefresh()
        {
            RefreshCoreList(reload: true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:877-881
        // Original: def on_module_changed(self, new_module_file: str):
        public void OnModuleChanged(string newModuleFile)
        {
            OnModuleReload(newModuleFile);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:884-901
        // Original: def on_module_reload(self, module_file: str):
        public void OnModuleReload(string moduleFile)
        {
            if (_active == null || string.IsNullOrWhiteSpace(moduleFile))
            {
                return;
            }

            try
            {
                var resources = _active.ModuleResources(moduleFile);
                _modulesWidget?.SetResources(resources);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to reload module '{moduleFile}': {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:917-918
        // Original: def on_module_refresh(self):
        public void OnModuleRefresh()
        {
            RefreshModuleList(reload: true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1100-1105
        // Original: def on_override_changed(self, new_directory: str):
        public void OnOverrideChanged(string newDirectory)
        {
            if (_active == null)
            {
                return;
            }
            _overrideWidget?.SetResources(_active.OverrideResources(newDirectory));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1107-1121
        // Original: def on_override_reload(self, file_or_folder: str):
        public void OnOverrideReload(string fileOrFolder)
        {
            if (_active == null)
            {
                return;
            }

            try
            {
                var overridePath = _active.OverridePath();
                var fileOrFolderPath = Path.Combine(overridePath, fileOrFolder);
                if (File.Exists(fileOrFolderPath))
                {
                    var relFolderpath = Path.GetDirectoryName(fileOrFolderPath);
                    _active.ReloadOverrideFile(fileOrFolderPath);
                    _overrideWidget?.SetResources(_active.OverrideResources(relFolderpath ?? ""));
                }
                else if (Directory.Exists(fileOrFolderPath))
                {
                    _active.LoadOverride(fileOrFolder);
                    _overrideWidget?.SetResources(_active.OverrideResources(fileOrFolder));
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to reload override '{fileOrFolder}': {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1123-1124
        // Original: def on_override_refresh(self):
        public void OnOverrideRefresh()
        {
            RefreshOverrideList(reload: true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:921-933
        // Original: def on_savepath_changed(self, new_save_dir: str):
        public void OnSavePathChanged(string newSaveDir)
        {
            if (_active == null || string.IsNullOrWhiteSpace(newSaveDir))
            {
                return;
            }

            try
            {
                // Clear the saves widget model (matching PyKotor: self.ui.savesWidget.modules_model.invisibleRootItem().removeRows(...))
                if (_savesWidget != null)
                {
                    _savesWidget.SetResources(new List<FileResource>());
                }

                // Load saves for the new directory and update the widget
                // Note: In PyKotor, this calls active.load_saves() and checks if new_save_dir_path is in active.saves
                // For now, we'll refresh the saves list with the new directory
                RefreshSavesList(reload: true);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to change save path to '{newSaveDir}': {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:974-979
        // Original: def on_save_reload(self, save_dir: str):
        public void OnSaveReload(string saveDir)
        {
            if (string.IsNullOrWhiteSpace(saveDir))
            {
                return;
            }

            System.Console.WriteLine($"Reloading save directory '{saveDir}'");
            // In PyKotor, this just calls on_savepath_changed
            OnSavePathChanged(saveDir);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1126-1128
        // Original: def on_textures_changed(self, texturepackName: str):
        private void OnTexturesChanged(string texturepackName)
        {
            if (_active == null)
            {
                return;
            }
            _texturesWidget?.SetResources(_active.TexturepackResources(texturepackName));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
        // Original: def on_save_refresh(self):
        public void OnSaveRefresh()
        {
            RefreshSavesList(reload: true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
        // Original: def on_module_file_updated(self, file_path: str, event_type: str):
        public void OnModuleFileUpdated(string filePath, string eventType)
        {
            if (_active == null || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (eventType == "deleted")
            {
                RefreshModuleList(reload: true);
            }
            else if (eventType == "modified")
            {
                OnModuleReload(filePath);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
        // Original: def on_override_file_updated(self, file_path: str, event_type: str):
        public void OnOverrideFileUpdated(string filePath, string eventType)
        {
            if (_active == null || string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            if (eventType == "deleted")
            {
                RefreshOverrideList(reload: true);
            }
            else if (eventType == "modified")
            {
                OnOverrideReload(filePath);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1475-1486
        // Original: def open_active_talktable(self):
        private async void OpenActiveTalktable()
        {
            if (_active == null)
            {
                return;
            }

            var tlkPath = Path.Combine(_active.Path, "dialog.tlk");
            if (!File.Exists(tlkPath))
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1479-1483
                // Original: QMessageBox(QMessageBox.Icon.Information, "dialog.tlk not found", f"Could not open the TalkTable editor, dialog.tlk not found at the expected location<br><br>{c_filepath}.").exec()
                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                    "dialog.tlk not found",
                    $"Could not open the TalkTable editor, dialog.tlk not found at the expected location\n\n{tlkPath}.",
                    ButtonEnum.Ok,
                    Icon.Info);
                await messageBox.ShowAsync();
                return;
            }

            var fileInfo = new FileInfo(tlkPath);
            var resource = new FileResource("dialog", ResourceType.TLK, (int)fileInfo.Length, 0, tlkPath);
            WindowUtils.OpenResourceEditor(resource, _active, this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1489-1505
        // Original: def open_active_journal(self):
        private void OpenActiveJournal()
        {
            if (_active == null)
            {
                return;
            }

            // Search for global.jrl in OVERRIDE and CHITIN locations
            var jrlIdent = new ResourceIdentifier("global", ResourceType.JRL);
            var journalResources = _active.Locations(
                new List<ResourceIdentifier> { jrlIdent },
                new[] { SearchLocation.OVERRIDE, SearchLocation.CHITIN });

            if (journalResources == null || !journalResources.ContainsKey(jrlIdent) || journalResources[jrlIdent].Count == 0)
            {
                // TODO: Show MessageBox when MessageBox.Avalonia is available
                System.Console.WriteLine("Could not open the journal editor: 'global.jrl' not found.");
                return;
            }

            var relevant = journalResources[jrlIdent];
            if (relevant.Count > 1)
            {
                // Multiple journal files found - show FileSelectionWindow for user selection
                var selectionWindow = new Dialogs.FileSelectionWindow(relevant, _active, this);
                selectionWindow.Show();
                WindowUtils.AddWindow(selectionWindow);
                return;
            }

            // Get the first (or only) journal location result
            var locationResult = relevant[0];
            
            // Ensure FileResource is set on LocationResult
            FileResource fileResource = locationResult.FileResource;
            if (fileResource == null)
            {
                // Create FileResource from LocationResult
                if (!File.Exists(locationResult.FilePath))
                {
                    System.Console.WriteLine($"Journal file not found at path: {locationResult.FilePath}");
                    return;
                }

                var fileInfo = new FileInfo(locationResult.FilePath);
                fileResource = new FileResource(
                    jrlIdent.ResName,
                    jrlIdent.ResType,
                    (int)fileInfo.Length,
                    locationResult.Offset,
                    locationResult.FilePath);
                locationResult.SetFileResource(fileResource);
            }

            // Open the journal editor with the resource
            WindowUtils.OpenResourceEditor(
                fileResource,
                _active,
                this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1508-1514
        // Original: def open_file_search_dialog(self):
        private void OpenFileSearchDialog()
        {
            if (_active == null)
            {
                return;
            }

            // TODO: Implement file search dialog when available
            System.Console.WriteLine("File search dialog not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1585-1604
        // Original: def _open_module_tab_erf_editor(self):
        private void OpenModuleTabErfEditor()
        {
            if (_active == null)
            {
                return;
            }

            ResourceList reslist = GetActiveResourceWidget();
            if (reslist == null || reslist != _modulesWidget)
            {
                return;
            }

            // Get the selected module filename from the section combo
            string filename = null;
            if (reslist.Ui?.SectionCombo != null && reslist.Ui.SectionCombo.SelectedItem != null)
            {
                filename = reslist.Ui.SectionCombo.SelectedItem.ToString();
            }

            if (string.IsNullOrEmpty(filename))
            {
                return;
            }

            // Construct the full path to the module file
            string modulePath = _active.ModulePath();
            string erfFilepath = Path.Combine(modulePath, filename);
            if (!File.Exists(erfFilepath))
            {
                return;
            }

            // Create ResourceIdentifier from path
            var resIdent = ResourceIdentifier.FromPath(erfFilepath);
            if (resIdent.ResType == null)
            {
                return;
            }

            // Create FileResource for the module file
            var fileInfo = new FileInfo(erfFilepath);
            var erfFileResource = new FileResource(
                resIdent.ResName,
                resIdent.ResType,
                (int)fileInfo.Length,
                0x0,
                erfFilepath);

            // Open the ERF editor
            WindowUtils.OpenResourceEditor(
                erfFileResource,
                _active,
                this,
                useSpecializedEditor: null);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1517-1522
        // Original: def open_indoor_map_builder(self):
        private void OpenIndoorMapBuilder()
        {
            if (_active == null)
            {
                return;
            }

            var builder = new IndoorBuilderWindow(null, _active);
            builder.Show();
            WindowUtils.AddWindow(builder);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1525-1535
        // Original: def open_kotordiff(self):
        private void OpenKotordiff()
        {
            var kotordiffWindow = new KotorDiffWindow(null, _installations, _active);
            kotordiffWindow.Show();
            WindowUtils.AddWindow(kotordiffWindow);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1546-1552
        // Original: def open_instructions_window(self):
        private void OpenInstructionsWindow()
        {
            var window = new HelpWindow(null);
            window.Show();
            WindowUtils.AddWindow(window);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1554-1556
        // Original: def open_about_dialog(self):
        private void OpenAboutDialog()
        {
            var dialog = new Dialogs.AboutDialog(this);
            dialog.ShowDialog(this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1457-1472
        // Original: def open_settings_dialog(self):
        private void OpenSettingsDialog()
        {
            var dialog = new Dialogs.SettingsDialog(this);
            dialog.ShowDialog(this);
            // TODO: Reload installations if settings were edited
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
        // Original: def open_clone_module_dialog(self):
        private void OpenCloneModuleDialog()
        {
            if (_active == null)
            {
                return;
            }

            // Create installations dictionary with active installation
            var installations = new Dictionary<string, HTInstallation>();
            if (_active != null)
            {
                installations[_active.Name] = _active;
            }
            // Add other installations if available
            foreach (var kvp in _installations)
            {
                if (!installations.ContainsKey(kvp.Key))
                {
                    installations[kvp.Key] = kvp.Value;
                }
            }

            var dialog = new Dialogs.CloneModuleDialog(this, _active, installations);
            dialog.ShowDialog(this);
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py
    // Original: self.ui = Ui_MainWindow() - UI wrapper class exposing all controls
    public class MainWindowUi
    {
        public ComboBox GameCombo { get; set; }
        public TabControl ResourceTabs { get; set; }
        public ResourceList CoreWidget { get; set; }
        public ResourceList ModulesWidget { get; set; }
        public ResourceList OverrideWidget { get; set; }
        public ResourceList SavesWidget { get; set; }
        public ResourceList TexturesWidget { get; set; }
        public TabItem CoreTab { get; set; }
        public TabItem ModulesTab { get; set; }
        public TabItem OverrideTab { get; set; }
        public MenuItem ActionNewDLG { get; set; }
        public MenuItem ActionNewUTC { get; set; }
        public MenuItem ActionNewNSS { get; set; }
    }
}
