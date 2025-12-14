using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Utils;
using HolocronToolset.NET.Widgets;
using FileResource = CSharpKOTOR.Resources.FileResource;

namespace HolocronToolset.NET.Windows
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
                // Try to find controls from XAML
                _gameCombo = this.FindControl<ComboBox>("gameCombo");
                _resourceTabs = this.FindControl<TabControl>("resourceTabs");
                _openButton = this.FindControl<Button>("openButton");
                _extractButton = this.FindControl<Button>("extractButton");
                _specialActionButton = this.FindControl<Button>("specialActionButton");
                
                // Find resource list widgets
                _coreWidget = this.FindControl<ResourceList>("coreWidget");
                _modulesWidget = this.FindControl<ResourceList>("modulesWidget");
                _overrideWidget = this.FindControl<ResourceList>("overrideWidget");
                _savesWidget = this.FindControl<ResourceList>("savesWidget");
                _texturesWidget = this.FindControl<ResourceList>("texturesWidget");
            }
            catch
            {
                // XAML not available - will use programmatic UI
            }

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
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
            buttonPanel.Children.Add(_openButton);
            buttonPanel.Children.Add(_extractButton);
            buttonPanel.Children.Add(_specialActionButton);
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
        private void ChangeActiveInstallation(int index)
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
        private void UnsetInstallation()
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
        private void UpdateMenus()
        {
            // Update menu states based on active installation
            // This will be implemented when menus are available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1705-1716
        // Original: def refresh_core_list(...):
        private void RefreshCoreList(bool reload = true)
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
        private void RefreshSavesList(bool reload = true)
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
        private void RefreshModuleList(bool reload = true)
        {
            if (_active == null || _modulesWidget == null)
            {
                return;
            }

            try
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
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to refresh module list: {ex}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1832-1840
        // Original: def refresh_override_list(...):
        private void RefreshOverrideList(bool reload = true)
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
        private void ReloadSettings()
        {
            ReloadInstallations();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1646-1654
        // Original: def reload_installations(self):
        private void ReloadInstallations()
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
        private ResourceList GetActiveResourceWidget()
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
        private void OnCoreRefresh()
        {
            RefreshCoreList(reload: true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:877-881
        // Original: def on_module_changed(self, new_module_file: str):
        private void OnModuleChanged(string newModuleFile)
        {
            OnModuleReload(newModuleFile);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:884-901
        // Original: def on_module_reload(self, module_file: str):
        private void OnModuleReload(string moduleFile)
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
        private void OnModuleRefresh()
        {
            RefreshModuleList(reload: true);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1100-1105
        // Original: def on_override_changed(self, new_directory: str):
        private void OnOverrideChanged(string newDirectory)
        {
            if (_active == null)
            {
                return;
            }
            _overrideWidget?.SetResources(_active.OverrideResources(newDirectory));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1107-1121
        // Original: def on_override_reload(self, file_or_folder: str):
        private void OnOverrideReload(string fileOrFolder)
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
        private void OnOverrideRefresh()
        {
            RefreshOverrideList(reload: true);
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1475-1486
        // Original: def open_active_talktable(self):
        private void OpenActiveTalktable()
        {
            if (_active == null)
            {
                return;
            }

            var tlkPath = Path.Combine(_active.Path, "dialog.tlk");
            if (!File.Exists(tlkPath))
            {
                // TODO: Show MessageBox when available
                System.Console.WriteLine($"dialog.tlk not found at {tlkPath}");
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

            // TODO: Implement journal opening when JRL editor is available
            System.Console.WriteLine("Journal editor not yet implemented");
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
            // About dialog will be implemented when available
            System.Console.WriteLine("About dialog not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/windows/main.py:1457-1472
        // Original: def open_settings_dialog(self):
        private void OpenSettingsDialog()
        {
            // Settings dialog will be implemented when available
            System.Console.WriteLine("Settings dialog not yet implemented");
        }
    }
}
