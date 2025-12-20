using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using HolocronToolset.Data;
using HolocronToolset.Utils;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Andastra.Parsing.Mods.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.GFF;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:35
    // Original: class TSLPatchDataEditor(QDialog):
    public partial class TSLPatchDataEditorDialog : Window
    {
        private HTInstallation _installation;
        private string _tslpatchdataPath;
        private TextBox _pathEdit;
        private TreeView _fileTree;
        private TabControl _configTabs;
        private Button _generateButton;
        private Button _previewButton;
        private Button _saveButton;
        
        // General settings controls
        private TextBox _modNameEdit;
        private TextBox _modAuthorEdit;
        private TextBox _modDescriptionEdit;
        private CheckBox _installToOverrideCheck;
        private CheckBox _backupFilesCheck;
        private CheckBox _confirmOverwritesCheck;
        
        // INI Preview control
        private TextBox _iniPreviewText;
        
        // GFF Fields controls
        private ListBox _gffFileList;
        private TreeView _gffFieldsTree;
        private List<ModificationsGFF> _gffModifications = new List<ModificationsGFF>();
        
        // Scripts controls
        private ListBox _scriptList;
        // Dictionary to store full file paths for scripts (key: filename, value: full path)
        // This allows us to copy the actual files during generation
        private Dictionary<string, string> _scriptPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // TLK StrRef controls
        private TreeView _tlkStringTree;
        // Dictionary to store TLK string entries (key: token name, value: TLKStringEntry)
        private Dictionary<string, TLKStringEntry> _tlkStrings = new Dictionary<string, TLKStringEntry>(StringComparer.OrdinalIgnoreCase);
        private int _nextTlkTokenId = 0;
        
        // 2DA Memory controls
        private ListBox _twodaList;
        private TreeView _twodaTokensTree;
        // Dictionary to store 2DA memory entries (key: 2DA filename, value: List of 2DAMemoryTokenEntry)
        private Dictionary<string, List<TwoDAMemoryTokenEntry>> _twodaMemoryTokens = new Dictionary<string, List<TwoDAMemoryTokenEntry>>(StringComparer.OrdinalIgnoreCase);
        private int _nextTwodaTokenId = 0;

        // Public parameterless constructor for XAML
        public TSLPatchDataEditorDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:38-61
        // Original: def __init__(self, parent, installation=None, tslpatchdata_path=None):
        public TSLPatchDataEditorDialog(Window parent, HTInstallation installation, string tslpatchdataPath = null)
        {
            InitializeComponent();
            Title = "TSLPatchData Editor - Create HoloPatcher Mod";
            Width = 1400;
            Height = 900;
            _installation = installation;
            _tslpatchdataPath = tslpatchdataPath ?? "tslpatchdata";
            SetupUI();
            if (!string.IsNullOrEmpty(_tslpatchdataPath) && Directory.Exists(_tslpatchdataPath))
            {
                LoadExistingConfig();
            }
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

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
            }
        }

        private void SetupProgrammaticUI()
        {
            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };

            var headerPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            headerPanel.Children.Add(new TextBlock { Text = "TSLPatchData Folder:", FontWeight = Avalonia.Media.FontWeight.Bold });
            _pathEdit = new TextBox { Text = _tslpatchdataPath, MinWidth = 300 };
            var browseButton = new Button { Content = "Browse..." };
            browseButton.Click += (s, e) => BrowseTslpatchdataPath();
            var createButton = new Button { Content = "Create New" };
            createButton.Click += (s, e) => CreateNewTslpatchdata();
            headerPanel.Children.Add(_pathEdit);
            headerPanel.Children.Add(browseButton);
            headerPanel.Children.Add(createButton);
            mainPanel.Children.Add(headerPanel);

            var splitter = new Grid();
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            var leftPanel = new StackPanel();
            leftPanel.Children.Add(new TextBlock { Text = "Files to Package:", FontWeight = Avalonia.Media.FontWeight.Bold });
            _fileTree = new TreeView();
            leftPanel.Children.Add(_fileTree);
            Grid.SetColumn(leftPanel, 0);
            splitter.Children.Add(leftPanel);

            _configTabs = new TabControl();
            CreateGeneralTab();
            Create2DAMemoryTab();
            CreateTLKStrRefTab();
            CreateGFFFieldsTab();
            CreateScriptsTab();
            CreateINIPreviewTab();
            Grid.SetColumn(_configTabs, 1);
            splitter.Children.Add(_configTabs);

            mainPanel.Children.Add(splitter);

            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            _generateButton = new Button { Content = "Generate TSLPatchData" };
            _generateButton.Click += (s, e) => GenerateTslpatchdata();
            _previewButton = new Button { Content = "Preview INI" };
            _previewButton.Click += (s, e) => PreviewIni();
            _saveButton = new Button { Content = "Save Configuration" };
            _saveButton.Click += (s, e) => SaveConfiguration();
            buttonPanel.Children.Add(_generateButton);
            buttonPanel.Children.Add(_previewButton);
            buttonPanel.Children.Add(_saveButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _pathEdit = this.FindControl<TextBox>("pathEdit");
            _fileTree = this.FindControl<TreeView>("fileTree");
            _configTabs = this.FindControl<TabControl>("configTabs");
            _generateButton = this.FindControl<Button>("generateButton");
            _previewButton = this.FindControl<Button>("previewButton");
            _saveButton = this.FindControl<Button>("saveButton");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:160-212
        // Original: def _create_general_tab(self):
        private void CreateGeneralTab()
        {
            var tab = new TabItem { Header = "General Settings" };
            var content = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            // Mod Information Group (using Expander instead of GroupBox - Avalonia doesn't have GroupBox)
            var modInfoGroup = new Expander { Header = "Mod Information", IsExpanded = true, Margin = new Avalonia.Thickness(0, 0, 0, 10) };
            var modInfoLayout = new StackPanel { Spacing = 5 };

            // Mod name
            var nameLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            nameLayout.Children.Add(new TextBlock { Text = "Mod Name:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 100 });
            _modNameEdit = new TextBox { MinWidth = 300 };
            nameLayout.Children.Add(_modNameEdit);
            modInfoLayout.Children.Add(nameLayout);

            // Mod author
            var authorLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            authorLayout.Children.Add(new TextBlock { Text = "Author:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 100 });
            _modAuthorEdit = new TextBox { MinWidth = 300 };
            authorLayout.Children.Add(_modAuthorEdit);
            modInfoLayout.Children.Add(authorLayout);

            // Description
            modInfoLayout.Children.Add(new TextBlock { Text = "Description:" });
            _modDescriptionEdit = new TextBox { MinHeight = 100, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            modInfoLayout.Children.Add(_modDescriptionEdit);

            modInfoGroup.Content = modInfoLayout;
            content.Children.Add(modInfoGroup);

            // Installation Options Group (using Expander instead of GroupBox - Avalonia doesn't have GroupBox)
            var installOptionsGroup = new Expander { Header = "Installation Options", IsExpanded = true, Margin = new Avalonia.Thickness(0, 0, 0, 10) };
            var installOptionsLayout = new StackPanel { Spacing = 5 };

            _installToOverrideCheck = new CheckBox { Content = "Install files to Override folder", IsChecked = true };
            installOptionsLayout.Children.Add(_installToOverrideCheck);

            _backupFilesCheck = new CheckBox { Content = "Backup original files", IsChecked = true };
            installOptionsLayout.Children.Add(_backupFilesCheck);

            _confirmOverwritesCheck = new CheckBox { Content = "Confirm before overwriting files", IsChecked = true };
            installOptionsLayout.Children.Add(_confirmOverwritesCheck);

            installOptionsGroup.Content = installOptionsLayout;
            content.Children.Add(installOptionsGroup);

            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
        }

        private void Create2DAMemoryTab()
        {
            var tab = new TabItem { Header = "2DA Memory" };
            var content = new StackPanel();
            // TODO: Add 2DA memory controls
            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:268-303
        // Original: def _create_tlk_strref_tab(self):
        private void CreateTLKStrRefTab()
        {
            var tab = new TabItem { Header = "TLK StrRef" };
            var content = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            // Header
            content.Children.Add(new TextBlock 
            { 
                Text = "TLK String References:", 
                FontWeight = Avalonia.Media.FontWeight.Bold 
            });
            content.Children.Add(new TextBlock 
            { 
                Text = "Manage string references that will be added to dialog.tlk." 
            });

            // TLK string tree
            _tlkStringTree = new TreeView();
            // Note: Avalonia TreeView doesn't have built-in column headers like QTreeWidget
            // We'll use a DataGrid or custom TreeViewItem with formatted content
            // For now, we'll use TreeView with custom item templates
            _tlkStringTree.MinHeight = 300;
            content.Children.Add(_tlkStringTree);

            // Buttons
            var btnLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var addStrBtn = new Button { Content = "Add TLK String" };
            addStrBtn.Click += (s, e) => AddTlkString();
            btnLayout.Children.Add(addStrBtn);
            
            var editStrBtn = new Button { Content = "Edit String" };
            editStrBtn.Click += (s, e) => EditTlkString();
            btnLayout.Children.Add(editStrBtn);
            
            var removeStrBtn = new Button { Content = "Remove String" };
            removeStrBtn.Click += (s, e) => RemoveTlkString();
            btnLayout.Children.Add(removeStrBtn);
            
            var openTlkEditorBtn = new Button { Content = "Open TLK Editor" };
            openTlkEditorBtn.Click += (s, e) => OpenTlkEditor();
            btnLayout.Children.Add(openTlkEditorBtn);
            
            btnLayout.Children.Add(new TextBlock()); // Spacer
            content.Children.Add(btnLayout);

            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
            
            RefreshTlkStringTree();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:305-348
        // Original: def _create_gff_fields_tab(self):
        private void CreateGFFFieldsTab()
        {
            var tab = new TabItem { Header = "GFF Fields" };
            var content = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            // Header
            content.Children.Add(new TextBlock 
            { 
                Text = "GFF Field Modifications:", 
                FontWeight = Avalonia.Media.FontWeight.Bold 
            });
            content.Children.Add(new TextBlock 
            { 
                Text = "View and edit fields that will be modified in GFF files." 
            });

            // Splitter for file list and field modifications
            var splitter = new Grid();
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            // Left: File list
            var leftWidget = new StackPanel { Spacing = 5 };
            leftWidget.Children.Add(new TextBlock { Text = "Modified GFF Files:" });
            _gffFileList = new ListBox();
            _gffFileList.SelectionChanged += OnGffFileSelected;
            leftWidget.Children.Add(_gffFileList);
            Grid.SetColumn(leftWidget, 0);
            splitter.Children.Add(leftWidget);

            // Right: Field modifications
            var rightWidget = new StackPanel { Spacing = 5 };
            rightWidget.Children.Add(new TextBlock { Text = "Field Modifications:" });
            _gffFieldsTree = new TreeView();
            rightWidget.Children.Add(_gffFieldsTree);

            var btnLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var openGffEditorBtn = new Button { Content = "Open in GFF Editor" };
            openGffEditorBtn.Click += (s, e) => OpenGffEditor();
            btnLayout.Children.Add(openGffEditorBtn);
            btnLayout.Children.Add(new TextBlock()); // Spacer
            rightWidget.Children.Add(btnLayout);

            Grid.SetColumn(rightWidget, 1);
            splitter.Children.Add(rightWidget);

            content.Children.Add(splitter);

            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
        }

        // Helper class for tree view items
        private class GFFFieldItem
        {
            public string FieldPath { get; set; }
            public string OldValue { get; set; }
            public string NewValue { get; set; }
            public string Type { get; set; }
        }
        
        // Helper class for TLK string entries
        private class TLKStringEntry
        {
            public string TokenName { get; set; }
            public string Text { get; set; }
            public string Sound { get; set; }
            public int TokenId { get; set; }
            public bool IsReplacement { get; set; }
            public int ModIndex { get; set; } // For replacements, this is the StrRef to replace
            public List<string> UsedBy { get; set; } = new List<string>(); // List of files/sections that use this token
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:350-373
        // Original: def _create_scripts_tab(self):
        private void CreateScriptsTab()
        {
            var tab = new TabItem { Header = "Scripts" };
            var content = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            // Header labels
            content.Children.Add(new TextBlock { Text = "Scripts:", FontWeight = Avalonia.Media.FontWeight.Bold });
            content.Children.Add(new TextBlock { Text = "Compiled scripts (.ncs) that will be installed." });

            // Script list
            _scriptList = new ListBox { MinHeight = 300 };
            content.Children.Add(_scriptList);

            // Buttons
            var btnLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var addScriptBtn = new Button { Content = "Add Script" };
            addScriptBtn.Click += async (s, e) => await AddScript();
            btnLayout.Children.Add(addScriptBtn);
            
            var removeScriptBtn = new Button { Content = "Remove Script" };
            removeScriptBtn.Click += (s, e) => RemoveScript();
            btnLayout.Children.Add(removeScriptBtn);
            
            btnLayout.Children.Add(new TextBlock()); // Spacer
            content.Children.Add(btnLayout);

            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:375-394
        // Original: def _create_ini_preview_tab(self):
        private void CreateINIPreviewTab()
        {
            var tab = new TabItem { Header = "INI Preview" };
            var content = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            content.Children.Add(new TextBlock { Text = "changes.ini Preview:", FontWeight = Avalonia.Media.FontWeight.Bold });

            _iniPreviewText = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
                FontFamily = new Avalonia.Media.FontFamily("Consolas, Courier New, monospace"),
                MinHeight = 400
            };
            content.Children.Add(_iniPreviewText);

            var btnLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var refreshPreviewBtn = new Button { Content = "Refresh Preview" };
            refreshPreviewBtn.Click += (s, e) => UpdateIniPreview();
            btnLayout.Children.Add(refreshPreviewBtn);
            btnLayout.Children.Add(new TextBlock()); // Spacer
            content.Children.Add(btnLayout);

            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:397-403
        // Original: def _browse_tslpatchdata_path(self):
        private async void BrowseTslpatchdataPath()
        {
            if (_pathEdit == null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            // Get initial directory from current path edit text if it's a valid path
            string initialDirectory = _pathEdit.Text;
            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                // Use the current path as initial directory
            }
            else if (!string.IsNullOrEmpty(initialDirectory))
            {
                // If it's not empty but doesn't exist, try to get the parent directory
                string parentDir = Path.GetDirectoryName(initialDirectory);
                if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                {
                    initialDirectory = parentDir;
                }
                else
                {
                    // Use current working directory or user's home
                    initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }
            }
            else
            {
                // Use current working directory or user's home
                initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            try
            {
                var options = new FolderPickerOpenOptions
                {
                    Title = "Select TSLPatchData Folder",
                    AllowMultiple = false
                };

                // Set initial directory if available
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    var storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
                    if (storageFolder != null)
                    {
                        options.SuggestedStartLocation = storageFolder;
                    }
                }

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (folders != null && folders.Count > 0)
                {
                    string selectedPath = folders[0].Path.LocalPath;
                    if (!string.IsNullOrWhiteSpace(selectedPath))
                    {
                        _tslpatchdataPath = selectedPath;
                        _pathEdit.Text = selectedPath;
                        // Reload existing configuration if the folder exists
                        if (Directory.Exists(_tslpatchdataPath))
                        {
                            LoadExistingConfig();
                        }
                    }
                }
            }
            catch
            {
                // Ignore errors - user may have cancelled or dialog failed
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:405-420
        // Original: def _create_new_tslpatchdata(self):
        private async void CreateNewTslpatchdata()
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            try
            {
                // Open folder picker to select location for new TSLPatchData
                var options = new FolderPickerOpenOptions
                {
                    Title = "Select Location for New TSLPatchData",
                    AllowMultiple = false
                };

                // Get initial directory from current path edit text if it's a valid path
                string initialDirectory = null;
                if (!string.IsNullOrEmpty(_pathEdit?.Text))
                {
                    string currentPath = _pathEdit.Text;
                    if (Directory.Exists(currentPath))
                    {
                        initialDirectory = currentPath;
                    }
                    else
                    {
                        // Try parent directory
                        string parentDir = Path.GetDirectoryName(currentPath);
                        if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir))
                        {
                            initialDirectory = parentDir;
                        }
                    }
                }

                // If no valid initial directory, use user's home directory
                if (string.IsNullOrEmpty(initialDirectory))
                {
                    initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                }

                // Set initial directory if available
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    var storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
                    if (storageFolder != null)
                    {
                        options.SuggestedStartLocation = storageFolder;
                    }
                }

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (folders == null || folders.Count == 0)
                {
                    // User cancelled
                    return;
                }

                string selectedPath = folders[0].Path.LocalPath;
                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    return;
                }

                // Create tslpatchdata subdirectory in selected location
                string tslpatchdataPath = Path.Combine(selectedPath, "tslpatchdata");
                
                // Create directory if it doesn't exist (exist_ok=True in Python)
                if (!Directory.Exists(tslpatchdataPath))
                {
                    Directory.CreateDirectory(tslpatchdataPath);
                }

                // Update path
                _tslpatchdataPath = tslpatchdataPath;
                if (_pathEdit != null)
                {
                    _pathEdit.Text = tslpatchdataPath;
                }

                // Show success message
                var msgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Created",
                    $"New tslpatchdata folder created at:\n{tslpatchdataPath}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Success);
                await msgBox.ShowAsync();
            }
            catch (Exception ex)
            {
                // Show error message
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Failed to create TSLPatchData folder:\n{ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:422-430
        // Original: def _load_existing_config(self):
        private void LoadExistingConfig()
        {
            if (string.IsNullOrEmpty(_tslpatchdataPath) || !Directory.Exists(_tslpatchdataPath))
            {
                return;
            }

            string iniPath = Path.Combine(_tslpatchdataPath, "changes.ini");
            if (!File.Exists(iniPath))
            {
                return;
            }

            try
            {
                // Read INI file
                string[] iniLines = File.ReadAllLines(iniPath, Encoding.UTF8);
                bool inCompileListSection = false;
                List<string> scripts = new List<string>();

                foreach (string line in iniLines)
                {
                    string trimmedLine = line.Trim();
                    
                    // Check for section headers
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        inCompileListSection = trimmedLine.Equals("[CompileList]", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    // If we're in CompileList section and line is not empty or a comment
                    if (inCompileListSection && !string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith(";") && !trimmedLine.StartsWith("#"))
                    {
                        // Script entries in CompileList are just filenames (without path)
                        scripts.Add(trimmedLine);
                    }
                }

                // Populate script list
                if (_scriptList != null)
                {
                    _scriptList.Items.Clear();
                    _scriptPaths.Clear();
                    foreach (string script in scripts)
                    {
                        _scriptList.Items.Add(script);
                        // Note: When loading from INI, we don't have the full paths
                        // The user will need to re-add scripts if they want to generate the mod
                        // This matches PyKotor behavior where only filenames are stored in INI
                    }
                }

                // Load TLK strings from INI
                LoadTlkStringsFromIni(iniLines);

                // Load other settings from INI using ConfigReader
                try
                {
                    var logger = new Andastra.Parsing.Logger.PatchLogger();
                    var configReader = Andastra.Parsing.Reader.ConfigReader.FromFilepath(iniPath, logger, _tslpatchdataPath);
                    var config = new Andastra.Parsing.PatcherConfig();
                    config = configReader.Load(config);

                    // Load general settings
                    if (config.Settings != null)
                    {
                        if (_modNameEdit != null)
                        {
                            _modNameEdit.Text = config.Settings.ModName ?? "";
                        }
                        if (_modAuthorEdit != null)
                        {
                            _modAuthorEdit.Text = config.Settings.Author ?? "";
                        }
                    }

                    // Load GFF modifications
                    _gffModifications = config.PatchesGFF.ToList();
                    RefreshGffFileList();
                }
                catch
                {
                    // If ConfigReader fails, continue with basic loading
                }

                UpdateIniPreview();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error loading existing config: {ex.Message}");
            }
        }

        private void RefreshGffFileList()
        {
            if (_gffFileList == null)
            {
                return;
            }

            _gffFileList.Items.Clear();
            foreach (var modGff in _gffModifications)
            {
                if (!string.IsNullOrEmpty(modGff.SourceFile))
                {
                    _gffFileList.Items.Add(modGff.SourceFile);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:521-523
        // Original: def _on_gff_file_selected(self, item):
        private void OnGffFileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (_gffFileList == null || _gffFieldsTree == null)
            {
                return;
            }

            var selectedItem = _gffFileList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedItem))
            {
                _gffFieldsTree.Items.Clear();
                return;
            }

            // Find the ModificationsGFF for the selected file
            var modGff = _gffModifications.FirstOrDefault(m => m.SourceFile == selectedItem);
            if (modGff == null)
            {
                _gffFieldsTree.Items.Clear();
                return;
            }

            // Populate tree view with field modifications
            var treeItems = new List<TreeViewItem>();
            foreach (var modifier in modGff.Modifiers)
            {
                string headerText = "";
                if (modifier is ModifyFieldGFF modifyField)
                {
                    headerText = $"{modifyField.Path} = {GetFieldValueString(modifyField.Value)} ({GetModifierTypeString(modifier)})";
                }
                else if (modifier is AddFieldGFF addField)
                {
                    headerText = $"Add Field: {addField.Label} ({addField.FieldType}) at {addField.Path}";
                }
                else if (modifier is AddStructToListGFF addStruct)
                {
                    headerText = $"Add Struct to List at {addStruct.Path}";
                }
                else if (modifier is Memory2DAModifierGFF mem2DA)
                {
                    headerText = $"2DAMEMORY{mem2DA.DestTokenId} = {mem2DA.Path}";
                }
                else
                {
                    headerText = $"{GetModifierTypeString(modifier)}: {modifier.GetType().Name}";
                }

                var item = new TreeViewItem
                {
                    Header = new TextBlock { Text = headerText }
                };
                treeItems.Add(item);
            }

            _gffFieldsTree.Items.Clear();
            foreach (var item in treeItems)
            {
                _gffFieldsTree.Items.Add(item);
            }
        }

        private string GetFieldValueString(Andastra.Parsing.Mods.FieldValue value)
        {
            if (value == null)
            {
                return "";
            }

            // Try to get a string representation of the value
            if (value is Andastra.Parsing.Mods.FieldValueConstant constant)
            {
                return constant.Value?.ToString() ?? "";
            }
            else if (value is Andastra.Parsing.Mods.FieldValue2DAMemory mem2DA)
            {
                return $"2DAMEMORY{mem2DA.TokenId}";
            }
            else if (value is Andastra.Parsing.Mods.FieldValueTLKStrRef tlkStrRef)
            {
                return $"TLK StrRef: {tlkStrRef.StrRef}";
            }

            return value.ToString();
        }

        private string GetModifierTypeString(ModifyGFF modifier)
        {
            if (modifier is ModifyFieldGFF)
            {
                return "Modify";
            }
            else if (modifier is AddFieldGFF)
            {
                return "Add Field";
            }
            else if (modifier is AddStructToListGFF)
            {
                return "Add Struct";
            }
            else if (modifier is Memory2DAModifierGFF)
            {
                return "2DAMEMORY";
            }

            return modifier.GetType().Name;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:525-527
        // Original: def _open_gff_editor(self):
        private async void OpenGffEditor()
        {
            if (_gffFileList == null || _installation == null)
            {
                return;
            }

            var selectedItem = _gffFileList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedItem))
            {
                var msgBox = MessageBoxManager.GetMessageBoxStandard(
                    "No File Selected",
                    "Please select a GFF file from the list first.",
                    ButtonEnum.Ok,
                    Icon.Warning);
                await msgBox.ShowAsync();
                return;
            }

            // Try to find the file in the installation
            string filePath = null;
            try
            {
                // Check if file exists in tslpatchdata folder
                string tslpatchdataFile = Path.Combine(_tslpatchdataPath, selectedItem);
                if (File.Exists(tslpatchdataFile))
                {
                    filePath = tslpatchdataFile;
                }
                else if (_installation != null)
                {
                    // Try to find in installation
                    var resource = _installation.Resource(selectedItem, ResourceType.GFF);
                    if (resource != null)
                    {
                        filePath = resource.Filepath;
                    }
                }
            }
            catch
            {
                // File not found
            }

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "File Not Found",
                    $"Could not find GFF file: {selectedItem}\n\nPlease ensure the file exists in the tslpatchdata folder or installation.",
                    ButtonEnum.Ok,
                    Icon.Error);
                await errorBox.ShowAsync();
                return;
            }

            // Open GFF editor
            try
            {
                var result = WindowUtils.OpenResourceEditor(
                    filepath: filePath,
                    resname: Path.GetFileNameWithoutExtension(selectedItem),
                    restype: ResourceType.GFF,
                    installation: _installation,
                    parentWindow: this);

                if (result != null && result.Item2 != null)
                {
                    result.Item2.Show();
                }
            }
            catch (Exception ex)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Failed to open GFF editor:\n{ex.Message}",
                    ButtonEnum.Ok,
                    Icon.Error);
                await errorBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:529-534
        // Original: def _add_script(self):
        private async Task AddScript()
        {
            if (_scriptList == null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            try
            {
                var options = new FilePickerOpenOptions
                {
                    Title = "Select Scripts (.ncs)",
                    AllowMultiple = true,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Scripts")
                        {
                            Patterns = new[] { "*.ncs" }
                        },
                        new FilePickerFileType("All Files")
                        {
                            Patterns = new[] { "*" }
                        }
                    }
                };

                // Set initial directory if available
                if (!string.IsNullOrEmpty(_tslpatchdataPath) && Directory.Exists(_tslpatchdataPath))
                {
                    var storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(_tslpatchdataPath);
                    if (storageFolder != null)
                    {
                        options.SuggestedStartLocation = storageFolder;
                    }
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (files != null && files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        string filePath = file.Path.LocalPath;
                        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                        {
                            // Add just the filename to the list (matching PyKotor behavior)
                            string fileName = Path.GetFileName(filePath);
                            
                            // Check if script is already in the list
                            bool alreadyExists = false;
                            foreach (var item in _scriptList.Items)
                            {
                                if (item is string existingScript && existingScript.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                                {
                                    alreadyExists = true;
                                    break;
                                }
                            }

                            if (!alreadyExists)
                            {
                                _scriptList.Items.Add(fileName);
                                // Store the full path for later copying during generation
                                _scriptPaths[fileName] = filePath;
                            }
                        }
                    }
                    
                    // Update INI preview to reflect changes
                    UpdateIniPreview();
                }
            }
            catch (Exception ex)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Failed to add script:\n{ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorBox.ShowAsync();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:536-540
        // Original: def _remove_script(self):
        private void RemoveScript()
        {
            if (_scriptList == null)
            {
                return;
            }

            var selectedItem = _scriptList.SelectedItem;
            if (selectedItem != null)
            {
                string scriptName = selectedItem as string;
                if (!string.IsNullOrEmpty(scriptName))
                {
                    // Remove from list and dictionary
                    _scriptList.Items.Remove(selectedItem);
                    _scriptPaths.Remove(scriptName);
                    // Update INI preview to reflect changes
                    UpdateIniPreview();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:483-497
        // Original: def _add_tlk_string(self):
        private async void AddTlkString()
        {
            var dialog = new TLKStringEditDialog(this, null);
            var result = await dialog.ShowDialog<bool>(this);
            if (result)
            {
                var entry = dialog.TlkEntry;
                if (entry != null && !string.IsNullOrWhiteSpace(entry.TokenName))
                {
                    // Check if token name already exists
                    if (_tlkStrings.ContainsKey(entry.TokenName))
                    {
                        var errorBox = MessageBoxManager.GetMessageBoxStandard(
                            "Error",
                            $"A TLK string with token name '{entry.TokenName}' already exists.",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Error);
                        await errorBox.ShowAsync();
                        return;
                    }
                    
                    // Assign token ID if not set
                    if (entry.TokenId == 0)
                    {
                        entry.TokenId = _nextTlkTokenId++;
                    }
                    
                    _tlkStrings[entry.TokenName] = entry;
                    RefreshTlkStringTree();
                    UpdateIniPreview();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:487-489
        // Original: def _edit_tlk_string(self):
        private async void EditTlkString()
        {
            var selectedItem = _tlkStringTree?.SelectedItem;
            if (selectedItem == null)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Selection",
                    "Please select a TLK string to edit.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Information);
                await errorBox.ShowAsync();
                return;
            }
            
            // Get the token name from the selected item
            string tokenName = null;
            if (selectedItem is TreeViewItem treeItem)
            {
                tokenName = treeItem.Header?.ToString();
            }
            else if (selectedItem is string str)
            {
                tokenName = str;
            }
            
            if (string.IsNullOrWhiteSpace(tokenName) || !_tlkStrings.ContainsKey(tokenName))
            {
                return;
            }
            
            var existingEntry = _tlkStrings[tokenName];
            var dialog = new TLKStringEditDialog(this, existingEntry);
            var result = await dialog.ShowDialog<bool>(this);
            if (result)
            {
                var entry = dialog.TlkEntry;
                if (entry != null && !string.IsNullOrWhiteSpace(entry.TokenName))
                {
                    // If token name changed, remove old entry and add new one
                    if (entry.TokenName != tokenName && _tlkStrings.ContainsKey(entry.TokenName))
                    {
                        var errorBox = MessageBoxManager.GetMessageBoxStandard(
                            "Error",
                            $"A TLK string with token name '{entry.TokenName}' already exists.",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Error);
                        await errorBox.ShowAsync();
                        return;
                    }
                    
                    if (entry.TokenName != tokenName)
                    {
                        _tlkStrings.Remove(tokenName);
                    }
                    
                    // Preserve token ID and used by list
                    entry.TokenId = existingEntry.TokenId;
                    entry.UsedBy = existingEntry.UsedBy;
                    
                    _tlkStrings[entry.TokenName] = entry;
                    RefreshTlkStringTree();
                    UpdateIniPreview();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:491-496
        // Original: def _remove_tlk_string(self):
        private void RemoveTlkString()
        {
            var selectedItem = _tlkStringTree?.SelectedItem;
            if (selectedItem == null)
            {
                return;
            }
            
            // Get the token name from the selected item
            string tokenName = null;
            if (selectedItem is TreeViewItem treeItem)
            {
                tokenName = treeItem.Header?.ToString();
            }
            else if (selectedItem is string str)
            {
                tokenName = str;
            }
            
            if (!string.IsNullOrWhiteSpace(tokenName) && _tlkStrings.ContainsKey(tokenName))
            {
                _tlkStrings.Remove(tokenName);
                RefreshTlkStringTree();
                UpdateIniPreview();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:498-519
        // Original: def _open_tlk_editor(self):
        private void OpenTlkEditor()
        {
            if (_installation == null)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Installation",
                    "No installation loaded.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Warning);
                errorBox.ShowAsync();
                return;
            }
            
            string tlkPath = Path.Combine(_installation.Path(), "dialog.tlk");
            if (File.Exists(tlkPath))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(tlkPath);
                    WindowUtils.OpenResourceEditor(
                        tlkPath,
                        "dialog",
                        ResourceType.TLK,
                        data,
                        _installation,
                        this);
                }
                catch (Exception ex)
                {
                    var errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        $"Failed to open TLK editor:\n{ex.Message}",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    errorBox.ShowAsync();
                }
            }
            else
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Not Found",
                    "dialog.tlk not found in installation.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Warning);
                errorBox.ShowAsync();
            }
        }
        
        private void RefreshTlkStringTree()
        {
            if (_tlkStringTree == null)
            {
                return;
            }
            
            _tlkStringTree.Items.Clear();
            
            foreach (var kvp in _tlkStrings.OrderBy(x => x.Key))
            {
                var entry = kvp.Value;
                // Create a formatted string for display: "TokenName | Text Preview | Used By: file1, file2"
                string textPreview = entry.Text ?? "";
                if (textPreview.Length > 60)
                {
                    textPreview = textPreview.Substring(0, 57) + "...";
                }
                textPreview = textPreview.Replace("\n", "\\n").Replace("\r", "\\r");
                
                string usedByText = entry.UsedBy != null && entry.UsedBy.Count > 0 
                    ? string.Join(", ", entry.UsedBy.Take(3))
                    : "(not used)";
                if (entry.UsedBy != null && entry.UsedBy.Count > 3)
                {
                    usedByText += $" (+{entry.UsedBy.Count - 3} more)";
                }
                
                string displayText = $"{entry.TokenName} | {textPreview} | Used By: {usedByText}";
                _tlkStringTree.Items.Add(displayText);
            }
        }

        private void GenerateTslpatchdata()
        {
            // TODO: Generate TSLPatchData files
            System.Console.WriteLine("Generate TSLPatchData not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:566-571
        // Original: def _preview_ini(self):
        private void PreviewIni()
        {
            // Switch to INI Preview tab
            if (_configTabs != null && _configTabs.Items.Count > 0)
            {
                int previewTabIndex = _configTabs.Items.Count - 1; // Last tab is INI Preview
                _configTabs.SelectedIndex = previewTabIndex;
            }
            UpdateIniPreview();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:542-564
        // Original: def _update_ini_preview(self):
        private void UpdateIniPreview()
        {
            if (_iniPreviewText == null)
            {
                return;
            }

            // Generate preview from current configuration
            var previewLines = new StringBuilder();
            
            // Settings section
            previewLines.AppendLine("[settings]");
            string modName = _modNameEdit?.Text?.Trim() ?? "My Mod";
            string modAuthor = _modAuthorEdit?.Text?.Trim() ?? "Unknown";
            previewLines.AppendLine($"modname={modName}");
            previewLines.AppendLine($"author={modAuthor}");
            previewLines.AppendLine();

            // GFF files section
            previewLines.AppendLine("[GFFList]");
            previewLines.AppendLine("; Files to be patched");
            if (_gffModifications != null && _gffModifications.Count > 0)
            {
                foreach (var modGff in _gffModifications)
                {
                    string identifier = modGff.ReplaceFile ? $"Replace{modGff.SourceFile}" : modGff.SourceFile ?? "Unknown";
                    previewLines.AppendLine($"{identifier}={modGff.SourceFile ?? "Unknown"}");
                }
            }
            previewLines.AppendLine();

            // 2DAMEMORY section
            previewLines.AppendLine("[2DAMEMORY]");
            previewLines.AppendLine("; 2DA memory tokens");
            previewLines.AppendLine();

            // TLKList section
            previewLines.AppendLine("[TLKList]");
            previewLines.AppendLine("; TLK string additions");
            if (_tlkStrings != null && _tlkStrings.Count > 0)
            {
                // Check if we have any replacements
                bool hasReplacements = _tlkStrings.Values.Any(e => e.IsReplacement);
                if (hasReplacements)
                {
                    previewLines.AppendLine("ReplaceFile0=replace.tlk");
                }
                
                // Add append entries (non-replacements)
                var appendEntries = _tlkStrings.Values.Where(e => !e.IsReplacement).OrderBy(e => e.TokenId).ToList();
                foreach (var entry in appendEntries)
                {
                    // Truncate text for comment (max 60 chars)
                    string textPreview = (entry.Text ?? "").Substring(0, Math.Min(60, (entry.Text ?? "").Length));
                    textPreview = textPreview.Replace("\n", "\\n").Replace("\r", "\\r");
                    
                    // Build comment with text and sound (if present)
                    var commentParts = new List<string>();
                    if (!string.IsNullOrEmpty(textPreview))
                    {
                        commentParts.Add($"\"{textPreview}\"");
                    }
                    if (!string.IsNullOrEmpty(entry.Sound))
                    {
                        commentParts.Add($"sound={entry.Sound}");
                    }
                    
                    string comment = commentParts.Count > 0 ? string.Join(" | ", commentParts) : "(empty entry)";
                    
                    // Add the line with comment: StrRef{modIndex}={tokenId}  ; comment
                    previewLines.AppendLine($"StrRef{entry.ModIndex}={entry.TokenId}  ; {comment}");
                }
                
                // Add replacement section if needed
                if (hasReplacements)
                {
                    previewLines.AppendLine();
                    previewLines.AppendLine("[replace.tlk]");
                    var replacementEntries = _tlkStrings.Values.Where(e => e.IsReplacement).OrderBy(e => e.TokenId).ToList();
                    foreach (var entry in replacementEntries)
                    {
                        previewLines.AppendLine($"{entry.TokenId}={entry.ModIndex}");
                    }
                }
            }
            previewLines.AppendLine();

            // InstallList section
            previewLines.AppendLine("[InstallList]");
            previewLines.AppendLine("; Files to install");
            previewLines.AppendLine();

            // 2DAList section
            previewLines.AppendLine("[2DAList]");
            previewLines.AppendLine("; 2DA files to patch");
            previewLines.AppendLine();

            // CompileList section
            previewLines.AppendLine("[CompileList]");
            previewLines.AppendLine("; Scripts to compile");
            if (_scriptList != null && _scriptList.Items.Count > 0)
            {
                foreach (var item in _scriptList.Items)
                {
                    if (item is string scriptName && !string.IsNullOrWhiteSpace(scriptName))
                    {
                        previewLines.AppendLine(scriptName);
                    }
                }
            }
            previewLines.AppendLine();

            _iniPreviewText.Text = previewLines.ToString();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:573-588
        // Original: def _save_configuration(self):
        private async void SaveConfiguration()
        {
            try
            {
                // Ensure tslpatchdata directory exists
                if (string.IsNullOrEmpty(_tslpatchdataPath))
                {
                    var msgBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        "TSLPatchData path is not set. Please specify a path first.",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    await msgBox.ShowAsync();
                    return;
                }

                // Create directory if it doesn't exist
                if (!Directory.Exists(_tslpatchdataPath))
                {
                    Directory.CreateDirectory(_tslpatchdataPath);
                }

                // Build and update INI preview
                UpdateIniPreview();

                // Get the INI content from preview
                string iniContent = _iniPreviewText?.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(iniContent))
                {
                    // If preview is empty, generate basic content
                    var sb = new StringBuilder();
                    sb.AppendLine("[settings]");
                    string modName = _modNameEdit?.Text?.Trim() ?? "My Mod";
                    string modAuthor = _modAuthorEdit?.Text?.Trim() ?? "Unknown";
                    sb.AppendLine($"modname={modName}");
                    sb.AppendLine($"author={modAuthor}");
                    sb.AppendLine();
                    iniContent = sb.ToString();
                }

                // Write to changes.ini
                string iniPath = Path.Combine(_tslpatchdataPath, "changes.ini");
                File.WriteAllText(iniPath, iniContent, Encoding.UTF8);

                // Show success message
                var successBox = MessageBoxManager.GetMessageBoxStandard(
                    "Saved",
                    $"Configuration saved to:\n{iniPath}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Success);
                await successBox.ShowAsync();
            }
            catch (Exception ex)
            {
                // Show error message
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Failed to save configuration:\n{ex.Message}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorBox.ShowAsync();
            }
        }
        
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:422-430
        // Original: def _load_existing_config(self) - TLK loading part
        private void LoadTlkStringsFromIni(string[] iniLines)
        {
            if (iniLines == null || iniLines.Length == 0)
            {
                return;
            }
            
            _tlkStrings.Clear();
            _nextTlkTokenId = 0;
            
            bool inTlkListSection = false;
            bool inReplaceTlkSection = false;
            Dictionary<int, int> replaceMappings = new Dictionary<int, int>(); // tokenId -> modIndex
            
            foreach (string line in iniLines)
            {
                string trimmedLine = line.Trim();
                
                // Check for section headers
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    string sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    inTlkListSection = sectionName.Equals("TLKList", StringComparison.OrdinalIgnoreCase);
                    inReplaceTlkSection = sectionName.Equals("replace.tlk", StringComparison.OrdinalIgnoreCase);
                    continue;
                }
                
                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                {
                    continue;
                }
                
                if (inReplaceTlkSection)
                {
                    // Parse replacement mappings: tokenId=modIndex
                    int equalsIndex = trimmedLine.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string tokenIdStr = trimmedLine.Substring(0, equalsIndex).Trim();
                        string modIndexStr = trimmedLine.Substring(equalsIndex + 1).Trim();
                        if (int.TryParse(tokenIdStr, out int tokenId) && int.TryParse(modIndexStr, out int modIndex))
                        {
                            replaceMappings[tokenId] = modIndex;
                        }
                    }
                }
                else if (inTlkListSection)
                {
                    // Check for ReplaceFile directive
                    if (trimmedLine.StartsWith("ReplaceFile", StringComparison.OrdinalIgnoreCase))
                    {
                        // ReplaceFile0=replace.tlk - we'll handle this when processing [replace.tlk] section
                        continue;
                    }
                    
                    // Parse StrRef entries: StrRef{modIndex}={tokenId}  ; comment
                    if (trimmedLine.StartsWith("StrRef", StringComparison.OrdinalIgnoreCase))
                    {
                        int equalsIndex = trimmedLine.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            string leftPart = trimmedLine.Substring(0, equalsIndex).Trim();
                            string rightPart = trimmedLine.Substring(equalsIndex + 1).Trim();
                            
                            // Extract modIndex from "StrRef{modIndex}"
                            if (leftPart.StartsWith("StrRef", StringComparison.OrdinalIgnoreCase))
                            {
                                string modIndexStr = leftPart.Substring(6); // Skip "StrRef"
                                if (int.TryParse(modIndexStr, out int modIndex))
                                {
                                    // Extract tokenId from right part (before comment)
                                    int commentIndex = rightPart.IndexOf(';');
                                    string tokenIdStr = commentIndex >= 0 
                                        ? rightPart.Substring(0, commentIndex).Trim()
                                        : rightPart.Trim();
                                    
                                    if (int.TryParse(tokenIdStr, out int tokenId))
                                    {
                                        // Extract text and sound from comment if present
                                        string text = "";
                                        string sound = "";
                                        if (commentIndex >= 0)
                                        {
                                            string comment = rightPart.Substring(commentIndex + 1).Trim();
                                            // Parse comment: "text" | sound=soundname
                                            if (comment.StartsWith("\"") && comment.Contains("\""))
                                            {
                                                int endQuote = comment.IndexOf('"', 1);
                                                if (endQuote > 0)
                                                {
                                                    text = comment.Substring(1, endQuote - 1);
                                                    comment = comment.Substring(endQuote + 1).Trim();
                                                }
                                            }
                                            if (comment.StartsWith("sound=", StringComparison.OrdinalIgnoreCase))
                                            {
                                                sound = comment.Substring(6).Trim();
                                            }
                                        }
                                        
                                        // Generate token name from tokenId
                                        string tokenName = $"StrRef{tokenId}";
                                        
                                        var entry = new TLKStringEntry
                                        {
                                            TokenName = tokenName,
                                            TokenId = tokenId,
                                            ModIndex = modIndex,
                                            Text = text,
                                            Sound = sound,
                                            IsReplacement = false
                                        };
                                        
                                        _tlkStrings[tokenName] = entry;
                                        if (tokenId >= _nextTlkTokenId)
                                        {
                                            _nextTlkTokenId = tokenId + 1;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            
            // Process replacement mappings
            foreach (var kvp in replaceMappings)
            {
                int tokenId = kvp.Key;
                int modIndex = kvp.Value;
                string tokenName = $"StrRef{tokenId}";
                
                var entry = new TLKStringEntry
                {
                    TokenName = tokenName,
                    TokenId = tokenId,
                    ModIndex = modIndex,
                    Text = "",
                    Sound = "",
                    IsReplacement = true
                };
                
                _tlkStrings[tokenName] = entry;
                if (tokenId >= _nextTlkTokenId)
                {
                    _nextTlkTokenId = tokenId + 1;
                }
            }
            
            RefreshTlkStringTree();
        }
    }
    
    // Dialog for adding/editing TLK strings
    // Matching PyKotor implementation pattern for dialogs
    internal class TLKStringEditDialog : Window
    {
        public TLKStringEntry TlkEntry { get; private set; }
        private TextBox _tokenNameEdit;
        private TextBox _textEdit;
        private TextBox _soundEdit;
        private CheckBox _isReplacementCheck;
        private NumericUpDown _modIndexSpin;
        private Button _okButton;
        private Button _cancelButton;
        private TLKStringEntry _originalEntry;
        
        public TLKStringEditDialog(Window parent, TLKStringEntry existingEntry = null)
        {
            Title = existingEntry == null ? "Add TLK String" : "Edit TLK String";
            Width = 500;
            Height = 400;
            _originalEntry = existingEntry;
            
            InitializeComponent();
        }
        
        private void InitializeComponent()
        {
            var mainPanel = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(15) };
            
            // Token Name
            var tokenNameLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            tokenNameLayout.Children.Add(new TextBlock { Text = "Token Name:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 });
            _tokenNameEdit = new TextBox { MinWidth = 300 };
            if (_originalEntry != null)
            {
                _tokenNameEdit.Text = _originalEntry.TokenName;
            }
            else
            {
                _tokenNameEdit.Text = "StrRef0";
            }
            tokenNameLayout.Children.Add(_tokenNameEdit);
            mainPanel.Children.Add(tokenNameLayout);
            
            // Text
            mainPanel.Children.Add(new TextBlock { Text = "Text:" });
            _textEdit = new TextBox 
            { 
                MinHeight = 100, 
                AcceptsReturn = true, 
                TextWrapping = Avalonia.Media.TextWrapping.Wrap 
            };
            if (_originalEntry != null)
            {
                _textEdit.Text = _originalEntry.Text ?? "";
            }
            mainPanel.Children.Add(_textEdit);
            
            // Sound ResRef
            var soundLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            soundLayout.Children.Add(new TextBlock { Text = "Sound ResRef:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 });
            _soundEdit = new TextBox { MinWidth = 300, MaxLength = 16 };
            if (_originalEntry != null)
            {
                _soundEdit.Text = _originalEntry.Sound ?? "";
            }
            soundLayout.Children.Add(_soundEdit);
            mainPanel.Children.Add(soundLayout);
            
            // Is Replacement
            _isReplacementCheck = new CheckBox { Content = "Replace existing StrRef (instead of append)" };
            if (_originalEntry != null)
            {
                _isReplacementCheck.IsChecked = _originalEntry.IsReplacement;
            }
            mainPanel.Children.Add(_isReplacementCheck);
            
            // Mod Index (for replacements)
            var modIndexLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            modIndexLayout.Children.Add(new TextBlock { Text = "StrRef to Replace:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 });
            _modIndexSpin = new NumericUpDown { Minimum = 0, Maximum = 999999, MinWidth = 300 };
            if (_originalEntry != null && _originalEntry.IsReplacement)
            {
                _modIndexSpin.Value = _originalEntry.ModIndex;
            }
            modIndexLayout.Children.Add(_modIndexSpin);
            mainPanel.Children.Add(modIndexLayout);
            
            // Update Mod Index visibility based on replacement checkbox
            _isReplacementCheck.Checked += (s, e) => modIndexLayout.IsVisible = true;
            _isReplacementCheck.Unchecked += (s, e) => modIndexLayout.IsVisible = false;
            modIndexLayout.IsVisible = _originalEntry != null && _originalEntry.IsReplacement;
            
            // Buttons
            var buttonLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right };
            _okButton = new Button { Content = "OK", MinWidth = 80 };
            _okButton.Click += (s, e) => OnOk();
            _cancelButton = new Button { Content = "Cancel", MinWidth = 80 };
            _cancelButton.Click += (s, e) => Close(false);
            buttonLayout.Children.Add(_okButton);
            buttonLayout.Children.Add(_cancelButton);
            mainPanel.Children.Add(buttonLayout);
            
            Content = mainPanel;
        }
        
        private void OnOk()
        {
            string tokenName = _tokenNameEdit?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(tokenName))
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    "Token name cannot be empty.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }
            
            TlkEntry = new TLKStringEntry
            {
                TokenName = tokenName,
                Text = _textEdit?.Text ?? "",
                Sound = _soundEdit?.Text ?? "",
                IsReplacement = _isReplacementCheck?.IsChecked == true,
                ModIndex = _isReplacementCheck?.IsChecked == true ? (int)(_modIndexSpin?.Value ?? 0) : 0
            };
            
            if (_originalEntry != null)
            {
                TlkEntry.TokenId = _originalEntry.TokenId;
                TlkEntry.UsedBy = _originalEntry.UsedBy;
            }
            
            // Close with true result for ShowDialog<bool> support
            Close(true);
        }
    }
}
