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
using HolocronToolset.Editors;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using BioWare.NET.TSLPatcher.Mods;
using BioWare.NET.TSLPatcher.Mods.GFF;
using BioWare.NET.TSLPatcher.Mods.TLK;
using BioWare.NET.TSLPatcher.Mods.NCS;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TLK;

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

        // 2DA modifications
        private List<Modifications2DA> _twodaModifications = new List<Modifications2DA>();

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
            var mainPanel = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

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
            var content = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

            // Mod Information Group (using Expander instead of GroupBox - Avalonia doesn't have GroupBox)
            var modInfoGroup = new Expander { Header = "Mod Information", IsExpanded = true, Margin = new Thickness(0, 0, 0, 10) };
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
            var installOptionsGroup = new Expander { Header = "Installation Options", IsExpanded = true, Margin = new Thickness(0, 0, 0, 10) };
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:214-266
        // Original: def _create_2da_memory_tab(self):
        private void Create2DAMemoryTab()
        {
            var tab = new TabItem { Header = "2DA Memory" };
            var content = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

            // Header
            content.Children.Add(new TextBlock
            {
                Text = "2DA Memory Tokens:",
                FontWeight = Avalonia.Media.FontWeight.Bold
            });
            content.Children.Add(new TextBlock
            {
                Text = "Track row numbers in 2DA files that will change during installation."
            });

            // Splitter for 2DA list and token details
            var splitter = new Grid();
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });

            // Left: List of 2DA files
            var leftWidget = new StackPanel { Spacing = 5 };
            leftWidget.Children.Add(new TextBlock { Text = "2DA Files:" });
            _twodaList = new ListBox { MinHeight = 300 };
            _twodaList.SelectionChanged += On2DASelected;
            leftWidget.Children.Add(_twodaList);

            var add2daBtn = new Button { Content = "Add 2DA Memory Token" };
            add2daBtn.Click += (s, e) => Add2DAMemory();
            leftWidget.Children.Add(add2daBtn);

            Grid.SetColumn(leftWidget, 0);
            splitter.Children.Add(leftWidget);

            // Right: Token details
            var rightWidget = new StackPanel { Spacing = 5 };
            rightWidget.Children.Add(new TextBlock { Text = "Memory Tokens:" });
            _twodaTokensTree = new TreeView { MinHeight = 300 };
            // Note: Avalonia TreeView doesn't have built-in column headers like QTreeWidget
            // We'll use custom item formatting to show Token Name, Column, Row Label, Used By
            rightWidget.Children.Add(_twodaTokensTree);

            var tokenBtnLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            var addTokenBtn = new Button { Content = "Add Token" };
            addTokenBtn.Click += (s, e) => Add2DAToken();
            tokenBtnLayout.Children.Add(addTokenBtn);

            var editTokenBtn = new Button { Content = "Edit Token" };
            editTokenBtn.Click += (s, e) => Edit2DAToken();
            tokenBtnLayout.Children.Add(editTokenBtn);

            var removeTokenBtn = new Button { Content = "Remove Token" };
            removeTokenBtn.Click += (s, e) => Remove2DAToken();
            tokenBtnLayout.Children.Add(removeTokenBtn);

            rightWidget.Children.Add(tokenBtnLayout);

            Grid.SetColumn(rightWidget, 1);
            splitter.Children.Add(rightWidget);

            content.Children.Add(splitter);
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
            var content = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

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

            // TLK string tree with column headers
            // Matching PyKotor implementation: QTreeWidget with columns ["Token Name", "String", "Used By"]
            var treeContainer = new StackPanel { Spacing = 0 };

            // Column headers (matching QTreeWidget header labels)
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150, GridUnitType.Pixel) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200, GridUnitType.Pixel) });

            var headerBorder = new Border
            {
                Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(240, 240, 240)),
                BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(5, 3, 5, 3)
            };

            var headerPanel = new StackPanel { Orientation = Orientation.Horizontal };
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Token Name",
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Width = 150
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "String",
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Margin = new Thickness(5, 0, 0, 0)
            });
            headerPanel.Children.Add(new TextBlock
            {
                Text = "Used By",
                FontWeight = Avalonia.Media.FontWeight.Bold,
                Width = 200,
                Margin = new Thickness(5, 0, 0, 0)
            });

            headerBorder.Child = headerPanel;
            treeContainer.Children.Add(headerBorder);

            // TreeView for data rows
            _tlkStringTree = new TreeView
            {
                MinHeight = 300
            };
            treeContainer.Children.Add(_tlkStringTree);

            content.Children.Add(treeContainer);

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
            var content = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

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


        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:350-373
        // Original: def _create_scripts_tab(self):
        private void CreateScriptsTab()
        {
            var tab = new TabItem { Header = "Scripts" };
            var content = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

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
            var content = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

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

                // Load 2DA memory tokens from INI
                Load2DAMemoryFromIni(iniLines);

                // Load other settings from INI using ConfigReader
                try
                {
                    var logger = new BioWare.NET.Logger.PatchLogger();
                    var configReader = BioWare.NET.Reader.ConfigReader.FromFilePath(iniPath, logger, _tslpatchdataPath);
                    var config = configReader.Config ?? new BioWare.NET.Config.PatcherConfig();
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

                    // Load 2DA modifications
                    _twodaModifications = config.Patches2DA.ToList();
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

        private string GetFieldValueString(FieldValue value)
        {
            if (value == null)
            {
                return "";
            }

            // Try to get a string representation of the value
            if (value is FieldValueConstant constant)
            {
                return constant.Stored?.ToString() ?? "";
            }
            else if (value is FieldValue2DAMemory mem2DA)
            {
                return $"2DAMEMORY{mem2DA.TokenId}";
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
                    MsBox.Avalonia.Enums.Icon.Warning);
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
                        filePath = resource.FilePath;
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
                    MsBox.Avalonia.Enums.Icon.Error);
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
                    MsBox.Avalonia.Enums.Icon.Error);
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
                    MsBox.Avalonia.Enums.Icon.Info);
                await errorBox.ShowAsync();
                return;
            }

            // Get the token name from the selected item
            string tokenName = null;
            if (selectedItem is TreeViewItem treeItem)
            {
                // Token name is stored in Tag property
                tokenName = treeItem.Tag as string;
                // Fallback: try to extract from header if Tag is not set
                if (string.IsNullOrWhiteSpace(tokenName) && treeItem.Header is Grid headerGrid && headerGrid.Children.Count > 0)
                {
                    if (headerGrid.Children[0] is TextBlock tokenBlock)
                    {
                        tokenName = tokenBlock.Text;
                    }
                }
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
                // Token name is stored in Tag property
                tokenName = treeItem.Tag as string;
                // Fallback: try to extract from header if Tag is not set
                if (string.IsNullOrWhiteSpace(tokenName) && treeItem.Header is Grid headerGrid && headerGrid.Children.Count > 0)
                {
                    if (headerGrid.Children[0] is TextBlock tokenBlock)
                    {
                        tokenName = tokenBlock.Text;
                    }
                }
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

            string tlkPath = Path.Combine(_installation.Path, "dialog.tlk");
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:460-481
        // Original: def _add_2da_memory(self), _on_2da_selected(self), _add_2da_token(self), _edit_2da_token(self), _remove_2da_token(self):
        private async void Add2DAMemory()
        {
            if (_installation == null)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Installation",
                    "No installation loaded.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Warning);
                await errorBox.ShowAsync();
                return;
            }

            // Show dialog to select 2DA file
            var dialog = new TwoDAMemorySelectDialog(this, _installation);
            var result = await dialog.ShowDialog<bool>(this);
            if (result && !string.IsNullOrWhiteSpace(dialog.Selected2DAFile))
            {
                string twodaFile = dialog.Selected2DAFile;

                // Check if 2DA file already exists in the list
                if (!_twodaMemoryTokens.ContainsKey(twodaFile))
                {
                    _twodaMemoryTokens[twodaFile] = new List<TwoDAMemoryTokenEntry>();
                    Refresh2DAList();
                }

                // Select the newly added 2DA file
                if (_twodaList != null)
                {
                    foreach (var item in _twodaList.Items)
                    {
                        if (item is string itemStr && itemStr.Equals(twodaFile, StringComparison.OrdinalIgnoreCase))
                        {
                            _twodaList.SelectedItem = item;
                            break;
                        }
                    }
                }
            }
        }

        private void On2DASelected(object sender, SelectionChangedEventArgs e)
        {
            if (_twodaList == null || _twodaTokensTree == null)
            {
                return;
            }

            var selectedItem = _twodaList.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedItem))
            {
                _twodaTokensTree.Items.Clear();
                return;
            }

            // Load and display tokens for selected 2DA
            Refresh2DATokensTree(selectedItem);
        }

        private async void Add2DAToken()
        {
            if (_twodaList == null || _twodaList.SelectedItem == null)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "No 2DA Selected",
                    "Please select a 2DA file from the list first.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                await errorBox.ShowAsync();
                return;
            }

            string twodaFile = _twodaList.SelectedItem as string;
            if (string.IsNullOrEmpty(twodaFile))
            {
                return;
            }

            // Show dialog to add token
            var dialog = new TwoDAMemoryTokenEditDialog(this, null, twodaFile, _installation);
            var result = await dialog.ShowDialog<bool>(this);
            if (result && dialog.TokenEntry != null)
            {
                var entry = dialog.TokenEntry;

                // Assign token ID if not set
                if (entry.TokenId == 0)
                {
                    entry.TokenId = _nextTwodaTokenId++;
                }
                else if (entry.TokenId >= _nextTwodaTokenId)
                {
                    _nextTwodaTokenId = entry.TokenId + 1;
                }

                // Generate token name if not set
                if (string.IsNullOrWhiteSpace(entry.TokenName))
                {
                    entry.TokenName = $"2DAMEMORY{entry.TokenId}";
                }

                if (!_twodaMemoryTokens.ContainsKey(twodaFile))
                {
                    _twodaMemoryTokens[twodaFile] = new List<TwoDAMemoryTokenEntry>();
                }

                _twodaMemoryTokens[twodaFile].Add(entry);
                Refresh2DATokensTree(twodaFile);
                UpdateIniPreview();
            }
        }

        private async void Edit2DAToken()
        {
            if (_twodaTokensTree == null || _twodaList == null)
            {
                return;
            }

            var selectedItem = _twodaTokensTree.SelectedItem;
            if (selectedItem == null)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Selection",
                    "Please select a token to edit.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                await errorBox.ShowAsync();
                return;
            }

            // Get token entry from selected item
            TwoDAMemoryTokenEntry tokenEntry = null;
            if (selectedItem is TreeViewItem treeItem && treeItem.Tag is TwoDAMemoryTokenEntry entry)
            {
                tokenEntry = entry;
            }
            else if (selectedItem is string tokenName)
            {
                // Try to find by token name
                string selectedTwodaFile = _twodaList.SelectedItem as string;
                if (!string.IsNullOrEmpty(selectedTwodaFile) && _twodaMemoryTokens.ContainsKey(selectedTwodaFile))
                {
                    tokenEntry = _twodaMemoryTokens[selectedTwodaFile].FirstOrDefault(t => t.TokenName == tokenName);
                }
            }

            if (tokenEntry == null)
            {
                return;
            }

            string twodaFile = _twodaList.SelectedItem as string;
            var dialog = new TwoDAMemoryTokenEditDialog(this, tokenEntry, twodaFile, _installation);
            var result = await dialog.ShowDialog<bool>(this);
            if (result && dialog.TokenEntry != null)
            {
                // Update the entry (it's already in the list, so just refresh)
                Refresh2DATokensTree(twodaFile);
                UpdateIniPreview();
            }
        }

        private void Remove2DAToken()
        {
            if (_twodaTokensTree == null || _twodaList == null)
            {
                return;
            }

            var selectedItem = _twodaTokensTree.SelectedItem;
            if (selectedItem == null)
            {
                return;
            }

            // Get token entry from selected item
            TwoDAMemoryTokenEntry tokenEntry = null;
            if (selectedItem is TreeViewItem treeItem && treeItem.Tag is TwoDAMemoryTokenEntry entry)
            {
                tokenEntry = entry;
            }
            else if (selectedItem is string tokenName)
            {
                // Try to find by token name
                string twodaFile = _twodaList.SelectedItem as string;
                if (!string.IsNullOrEmpty(twodaFile) && _twodaMemoryTokens.ContainsKey(twodaFile))
                {
                    tokenEntry = _twodaMemoryTokens[twodaFile].FirstOrDefault(t => t.TokenName == tokenName);
                }
            }

            if (tokenEntry != null)
            {
                string twodaFile = _twodaList.SelectedItem as string;
                if (!string.IsNullOrEmpty(twodaFile) && _twodaMemoryTokens.ContainsKey(twodaFile))
                {
                    _twodaMemoryTokens[twodaFile].Remove(tokenEntry);
                    Refresh2DATokensTree(twodaFile);
                    UpdateIniPreview();
                }
            }
        }

        private void Refresh2DAList()
        {
            if (_twodaList == null)
            {
                return;
            }

            _twodaList.Items.Clear();
            foreach (var twodaFile in _twodaMemoryTokens.Keys.OrderBy(x => x))
            {
                _twodaList.Items.Add(twodaFile);
            }
        }

        private void Refresh2DATokensTree(string twodaFile)
        {
            if (_twodaTokensTree == null || string.IsNullOrEmpty(twodaFile))
            {
                return;
            }

            _twodaTokensTree.Items.Clear();

            if (!_twodaMemoryTokens.ContainsKey(twodaFile))
            {
                return;
            }

            var tokens = _twodaMemoryTokens[twodaFile];
            foreach (var token in tokens.OrderBy(t => t.TokenId))
            {
                // Create formatted display: "TokenName | Column | RowLabel | Used By: file1, file2"
                string usedByText = token.UsedBy != null && token.UsedBy.Count > 0
                    ? string.Join(", ", token.UsedBy.Take(3))
                    : "(not used)";
                if (token.UsedBy != null && token.UsedBy.Count > 3)
                {
                    usedByText += $" (+{token.UsedBy.Count - 3} more)";
                }

                string displayText = $"{token.TokenName} | {token.ColumnName ?? "(none)"} | {token.RowLabel ?? "(none)"} | Used By: {usedByText}";

                var item = new TreeViewItem
                {
                    Header = new TextBlock { Text = displayText },
                    Tag = token
                };

                _twodaTokensTree.Items.Add(item);
            }
        }

        private void RefreshTlkStringTree()
        {
            if (_tlkStringTree == null)
            {
                return;
            }

            _tlkStringTree.Items.Clear();

            // Matching PyKotor implementation: QTreeWidget with columns ["Token Name", "String", "Used By"]
            // Create TreeViewItem objects with custom Grid layout for columns
            foreach (var kvp in _tlkStrings.OrderBy(x => x.Key))
            {
                var entry = kvp.Value;

                // Prepare text preview (truncate if too long)
                string textPreview = entry.Text ?? "";
                if (textPreview.Length > 60)
                {
                    textPreview = textPreview.Substring(0, 57) + "...";
                }
                // Replace newlines for display
                textPreview = textPreview.Replace("\n", "\\n").Replace("\r", "\\r");

                // Prepare "Used By" text
                string usedByText = entry.UsedBy != null && entry.UsedBy.Count > 0
                    ? string.Join(", ", entry.UsedBy.Take(3))
                    : "(not used)";
                if (entry.UsedBy != null && entry.UsedBy.Count > 3)
                {
                    usedByText += $" (+{entry.UsedBy.Count - 3} more)";
                }

                // Create TreeViewItem with Grid layout for columns
                var itemGrid = new Grid();
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150, GridUnitType.Pixel) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200, GridUnitType.Pixel) });

                // Token Name column
                var tokenNameBlock = new TextBlock
                {
                    Text = entry.TokenName ?? "",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 2, 5, 2)
                };
                Grid.SetColumn(tokenNameBlock, 0);
                itemGrid.Children.Add(tokenNameBlock);

                // String column
                var stringBlock = new TextBlock
                {
                    Text = textPreview,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(5, 2, 5, 2)
                };
                Grid.SetColumn(stringBlock, 1);
                itemGrid.Children.Add(stringBlock);

                // Used By column
                var usedByBlock = new TextBlock
                {
                    Text = usedByText,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(5, 2, 5, 2)
                };
                Grid.SetColumn(usedByBlock, 2);
                itemGrid.Children.Add(usedByBlock);

                // Create TreeViewItem with the Grid as header
                var treeItem = new TreeViewItem
                {
                    Header = itemGrid,
                    Tag = entry.TokenName // Store token name for easy retrieval
                };

                _tlkStringTree.Items.Add(treeItem);
            }
        }

        private async void GenerateTslpatchdata()
        {
            try
            {
                // Validate tslpatchdata path
                if (string.IsNullOrEmpty(_tslpatchdataPath))
                {
                    var errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        "TSLPatchData path is not set. Please specify a path first.",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    await errorBox.ShowAsync();
                    return;
                }

                // Create tslpatchdata directory if it doesn't exist
                if (!Directory.Exists(_tslpatchdataPath))
                {
                    Directory.CreateDirectory(_tslpatchdataPath);
                }

                // Collect all files to install
                var installFiles = new List<InstallFile>();
                var allFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Copy scripts to tslpatchdata folder and add to install list
                foreach (var scriptEntry in _scriptPaths)
                {
                    string scriptName = scriptEntry.Key;
                    string sourcePath = scriptEntry.Value;

                    if (File.Exists(sourcePath))
                    {
                        string destPath = Path.Combine(_tslpatchdataPath, scriptName);
                        File.Copy(sourcePath, destPath, overwrite: true);
                        allFilePaths.Add(scriptName);

                        // Scripts go to Override folder by default
                        string destination = _installToOverrideCheck?.IsChecked == true ? "Override" : ".";
                        installFiles.Add(new InstallFile(scriptName, replaceExisting: false, destination: destination));
                    }
                }

                // Collect GFF files that need to be installed
                foreach (var modGff in _gffModifications)
                {
                    if (!string.IsNullOrEmpty(modGff.SourceFile))
                    {
                        // Try to find the GFF file in tslpatchdata or installation
                        string gffSourcePath = null;
                        string tslpatchdataFile = Path.Combine(_tslpatchdataPath, modGff.SourceFile);

                        if (File.Exists(tslpatchdataFile))
                        {
                            gffSourcePath = tslpatchdataFile;
                        }
                        else if (_installation != null)
                        {
                            try
                            {
                                var resource = _installation.Resource(modGff.SourceFile, ResourceType.GFF);
                                if (resource != null)
                                {
                                    gffSourcePath = resource.FilePath;
                                }
                            }
                            catch
                            {
                                // Resource not found
                            }
                        }

                        // If we found the file and it's not already in tslpatchdata, copy it
                        if (!string.IsNullOrEmpty(gffSourcePath) && File.Exists(gffSourcePath))
                        {
                            string destPath = Path.Combine(_tslpatchdataPath, modGff.SourceFile);
                            if (!File.Exists(destPath) || !gffSourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Copy(gffSourcePath, destPath, overwrite: true);
                            }
                            allFilePaths.Add(modGff.SourceFile);

                            // GFF files go to Override folder by default
                            string destination = _installToOverrideCheck?.IsChecked == true ? "Override" : ".";
                            installFiles.Add(new InstallFile(modGff.SourceFile, replaceExisting: false, destination: destination));
                        }
                    }
                }

                // Build ModificationsByType from UI state
                var modificationsByType = new ModificationsByType();

                // Add GFF modifications
                modificationsByType.Gff = _gffModifications.ToList();

                // Build TLK modifications from _tlkStrings and generate append.tlk file
                if (_tlkStrings.Count > 0)
                {
                    var tlkModifiers = new List<ModifyTLK>();
                    var appendEntries = new List<TLKStringEntry>();

                    foreach (var tlkEntry in _tlkStrings.Values)
                    {
                        var modifyTlk = new ModifyTLK(tlkEntry.TokenId, tlkEntry.IsReplacement)
                        {
                            Text = tlkEntry.Text ?? "",
                            Sound = tlkEntry.Sound ?? "",
                            ModIndex = tlkEntry.IsReplacement ? tlkEntry.ModIndex : tlkEntry.TokenId
                        };
                        tlkModifiers.Add(modifyTlk);

                        // Collect non-replacement entries for append.tlk
                        if (!tlkEntry.IsReplacement)
                        {
                            appendEntries.Add(tlkEntry);
                        }
                    }

                    // Generate append.tlk file for non-replacement entries
                    if (appendEntries.Count > 0)
                    {
                        var appendTlk = new TLK();
                        appendTlk.Resize(appendEntries.Count);

                        // Sort by token ID for consistent ordering
                        var sortedAppends = appendEntries.OrderBy(e => e.TokenId).ToList();

                        for (int i = 0; i < sortedAppends.Count; i++)
                        {
                            var entry = sortedAppends[i];
                            string text = entry.Text ?? "";
                            string sound = entry.Sound ?? "";
                            appendTlk.Replace(i, text, sound);
                        }

                        // Write append.tlk to tslpatchdata folder
                        string appendPath = Path.Combine(_tslpatchdataPath, "append.tlk");
                        var writer = new TLKBinaryWriter(appendTlk);
                        byte[] tlkData = writer.Write();
                        File.WriteAllBytes(appendPath, tlkData);

                        // Add append.tlk to install list
                        string destination = _installToOverrideCheck?.IsChecked == true ? "Override" : ".";
                        installFiles.Add(new InstallFile("append.tlk", replaceExisting: false, destination: destination));
                        allFilePaths.Add("append.tlk");
                    }

                    var modTlk = new ModificationsTLK(
                        filename: ModificationsTLK.DEFAULT_SOURCEFILE,
                        replace: false,
                        modifiers: tlkModifiers);
                    modificationsByType.Tlk.Add(modTlk);
                }

                // Build NCS modifications from scripts
                foreach (var scriptEntry in _scriptPaths)
                {
                    string scriptName = scriptEntry.Key;
                    // NCS files don't need modifications by default - they're just installed
                    // But we can create an empty ModificationsNCS if needed for the INI
                    var modNcs = new ModificationsNCS(scriptName, replace: false, modifiers: new List<ModifyNCS>());
                    modificationsByType.Ncs.Add(modNcs);
                }

                // Add 2DA modifications if any exist
                // Unlike memory tokens (_twodaMemoryTokens), these are actual 2DA file modifications
                if (_twodaModifications.Count > 0)
                {
                    modificationsByType.Twoda = _twodaModifications.ToList();

                    // Copy any 2DA files that need to be installed
                    foreach (var mod2da in _twodaModifications)
                    {
                        string twodaFile = mod2da.SaveAs;
                        if (!string.IsNullOrEmpty(twodaFile))
                        {
                            // Try to find the 2DA file in tslpatchdata or installation
                            string twodaSourcePath = null;
                            string tslpatchdataFile = Path.Combine(_tslpatchdataPath, twodaFile);

                            if (File.Exists(tslpatchdataFile))
                            {
                                twodaSourcePath = tslpatchdataFile;
                            }
                            else if (_installation != null)
                            {
                                try
                                {
                                    var resource = _installation.Resource(twodaFile, ResourceType.TwoDA);
                                    if (resource != null)
                                    {
                                        twodaSourcePath = resource.FilePath;
                                    }
                                }
                                catch
                                {
                                    // Resource not found
                                }
                            }

                            // If we found the file and it's not already in tslpatchdata, copy it
                            if (!string.IsNullOrEmpty(twodaSourcePath) && File.Exists(twodaSourcePath))
                            {
                                string destPath = Path.Combine(_tslpatchdataPath, twodaFile);
                                if (!File.Exists(destPath) || !twodaSourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    File.Copy(twodaSourcePath, destPath, overwrite: true);
                                }
                                allFilePaths.Add(twodaFile);

                                // 2DA files go to Override folder by default
                                string destination = _installToOverrideCheck?.IsChecked == true ? "Override" : ".";
                                installFiles.Add(new InstallFile(twodaFile, replaceExisting: false, destination: destination));
                            }
                        }
                    }
                }

                // Add install files
                modificationsByType.Install = installFiles;

                // Generate changes.ini using TSLPatcherINISerializer
                var serializer = new TSLPatcherINISerializer();
                string iniContent = serializer.Serialize(
                    modificationsByType,
                    includeHeader: true,
                    includeSettings: true,
                    verbose: false);

                // Add custom settings section with mod name and author
                var settingsLines = new List<string>();
                settingsLines.Add("[Settings]");

                string modName = _modNameEdit?.Text?.Trim();
                if (!string.IsNullOrEmpty(modName))
                {
                    settingsLines.Add($"modname={modName}");
                }

                string modAuthor = _modAuthorEdit?.Text?.Trim();
                if (!string.IsNullOrEmpty(modAuthor))
                {
                    settingsLines.Add($"author={modAuthor}");
                }

                string modDescription = _modDescriptionEdit?.Text?.Trim();
                if (!string.IsNullOrEmpty(modDescription))
                {
                    // Description can be multi-line, escape it properly
                    string escapedDescription = modDescription.Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\r");
                    settingsLines.Add($"description={escapedDescription}");
                }

                // Add installation options
                if (_backupFilesCheck?.IsChecked == true)
                {
                    settingsLines.Add("backup=1");
                }

                if (_confirmOverwritesCheck?.IsChecked == true)
                {
                    settingsLines.Add("confirmoverwrite=1");
                }

                settingsLines.Add("LogLevel=3");
                settingsLines.Add("");

                // Replace the default [Settings] section with our custom one
                // Find the [Settings] section in the generated INI and replace it
                var iniLines = iniContent.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();
                int settingsIndex = iniLines.FindIndex(line => line.Trim().Equals("[Settings]", StringComparison.OrdinalIgnoreCase));

                if (settingsIndex >= 0)
                {
                    // Remove old settings section (until next section or end)
                    int removeCount = 1; // Remove [Settings] line
                    for (int i = settingsIndex + 1; i < iniLines.Count; i++)
                    {
                        string line = iniLines[i].Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                        {
                            removeCount++;
                            if (string.IsNullOrEmpty(line))
                            {
                                break; // Stop at first blank line after settings
                            }
                        }
                        else if (line.StartsWith("["))
                        {
                            break; // Stop at next section
                        }
                        else
                        {
                            removeCount++;
                        }
                    }
                    iniLines.RemoveRange(settingsIndex, removeCount);
                    iniLines.InsertRange(settingsIndex, settingsLines);
                }
                else
                {
                    // If no [Settings] section found, insert after header
                    int insertIndex = iniLines.FindIndex(line => line.Trim().StartsWith("["));
                    if (insertIndex < 0)
                    {
                        insertIndex = iniLines.Count;
                    }
                    iniLines.InsertRange(insertIndex, settingsLines);
                }

                iniContent = string.Join("\n", iniLines);

                // Write changes.ini to tslpatchdata folder
                string iniPath = Path.Combine(_tslpatchdataPath, "changes.ini");
                File.WriteAllText(iniPath, iniContent, Encoding.UTF8);

                // Show success message
                var successBox = MessageBoxManager.GetMessageBoxStandard(
                    "Success",
                    $"TSLPatchData generated successfully at:\n{_tslpatchdataPath}\n\n" +
                    $"Files included:\n" +
                    $"- {installFiles.Count} file(s) to install\n" +
                    $"- {_gffModifications.Count} GFF modification(s)\n" +
                    $"- {_twodaModifications.Count} 2DA modification(s)\n" +
                    $"- {_tlkStrings.Count} TLK string(s)\n" +
                    $"- {_scriptPaths.Count} script(s)\n\n" +
                    $"You can now distribute this folder with HoloPatcher/TSLPatcher.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Success);
                await successBox.ShowAsync();
            }
            catch (Exception ex)
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    $"Failed to generate TSLPatchData:\n{ex.Message}\n\n{ex.StackTrace}",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                await errorBox.ShowAsync();
            }
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

            // 2DAList section
            previewLines.AppendLine("[2DAList]");
            previewLines.AppendLine("; 2DA files to be patched");
            if (_twodaModifications != null && _twodaModifications.Count > 0)
            {
                foreach (var mod2da in _twodaModifications)
                {
                    string filename = mod2da.SaveAs ?? "Unknown";
                    previewLines.AppendLine($"Table0={filename}");
                }
            }
            previewLines.AppendLine();

            // 2DAMEMORY section
            previewLines.AppendLine("[2DAMEMORY]");
            previewLines.AppendLine("; 2DA memory tokens");
            if (_twodaMemoryTokens != null && _twodaMemoryTokens.Count > 0)
            {
                // For each 2DA file with memory tokens
                foreach (var kvp in _twodaMemoryTokens.OrderBy(x => x.Key))
                {
                    string twodaFile = kvp.Key;
                    var tokens = kvp.Value;
                    if (tokens != null && tokens.Count > 0)
                    {
                        // Create section for this 2DA file (format: [2DA:filename.2da])
                        previewLines.AppendLine();
                        previewLines.AppendLine($"[2DA:{twodaFile}]");
                        previewLines.AppendLine($"FileName={twodaFile}");

                        foreach (var token in tokens.OrderBy(t => t.TokenId))
                        {
                            // Format: 2DAMEMORY#=ColumnName|RowLabel
                            // Or: 2DAMEMORY#=RowLabel (if no column specified)
                            string value = "";
                            if (!string.IsNullOrEmpty(token.ColumnName) && !string.IsNullOrEmpty(token.RowLabel))
                            {
                                value = $"{token.ColumnName}|{token.RowLabel}";
                            }
                            else if (!string.IsNullOrEmpty(token.RowLabel))
                            {
                                value = token.RowLabel;
                            }
                            else if (!string.IsNullOrEmpty(token.ColumnName))
                            {
                                value = token.ColumnName;
                            }

                            if (!string.IsNullOrEmpty(value))
                            {
                                previewLines.AppendLine($"{token.TokenName}={value}");
                            }
                        }
                    }
                }
            }
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

        // Load 2DA memory tokens from INI file
        private void Load2DAMemoryFromIni(string[] iniLines)
        {
            if (iniLines == null || iniLines.Length == 0)
            {
                return;
            }

            _twodaMemoryTokens.Clear();
            _nextTwodaTokenId = 0;

            string currentSection = null;
            string current2DAFile = null;

            foreach (string line in iniLines)
            {
                string trimmedLine = line.Trim();

                // Check for section headers
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    string sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    currentSection = sectionName;

                    // Check if this is a 2DA section (format: [2DA:filename.2da])
                    if (sectionName.StartsWith("2DA:", StringComparison.OrdinalIgnoreCase))
                    {
                        current2DAFile = sectionName.Substring(4); // Skip "2DA:"
                        if (!_twodaMemoryTokens.ContainsKey(current2DAFile))
                        {
                            _twodaMemoryTokens[current2DAFile] = new List<TwoDAMemoryTokenEntry>();
                        }
                    }
                    else
                    {
                        current2DAFile = null;
                    }
                    continue;
                }

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                {
                    continue;
                }

                // Process 2DA memory tokens in 2DA sections
                if (!string.IsNullOrEmpty(current2DAFile) && currentSection != null && currentSection.StartsWith("2DA:", StringComparison.OrdinalIgnoreCase))
                {
                    int equalsIndex = trimmedLine.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string key = trimmedLine.Substring(0, equalsIndex).Trim();
                        string value = trimmedLine.Substring(equalsIndex + 1).Trim();

                        // Skip FileName key
                        if (key.Equals("FileName", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        // Check if key is a 2DAMEMORY token (format: 2DAMEMORY#)
                        if (key.StartsWith("2DAMEMORY", StringComparison.OrdinalIgnoreCase))
                        {
                            string idPart = key.Substring(9); // Skip "2DAMEMORY"
                            if (int.TryParse(idPart, out int tokenId))
                            {
                                // Parse value (format: ColumnName|RowLabel or just RowLabel)
                                string columnName = "";
                                string rowLabel = "";

                                int pipeIndex = value.IndexOf('|');
                                if (pipeIndex > 0)
                                {
                                    columnName = value.Substring(0, pipeIndex).Trim();
                                    rowLabel = value.Substring(pipeIndex + 1).Trim();
                                }
                                else
                                {
                                    rowLabel = value;
                                }

                                var entry = new TwoDAMemoryTokenEntry
                                {
                                    TokenName = key,
                                    TokenId = tokenId,
                                    ColumnName = columnName,
                                    RowLabel = rowLabel
                                };

                                _twodaMemoryTokens[current2DAFile].Add(entry);

                                if (tokenId >= _nextTwodaTokenId)
                                {
                                    _nextTwodaTokenId = tokenId + 1;
                                }
                            }
                        }
                    }
                }
            }

            Refresh2DAList();
        }
    }

    // Dialog for selecting 2DA file for memory token tracking
    // Matching PyKotor implementation pattern for dialogs
    internal class TwoDAMemorySelectDialog : Window
    {
        public string Selected2DAFile { get; private set; }
        private ListBox _twodaList;
        private Button _okButton;
        private Button _cancelButton;
        private HTInstallation _installation;

        public TwoDAMemorySelectDialog(Window parent, HTInstallation installation)
        {
            Title = "Select 2DA File";
            Width = 500;
            Height = 400;
            _installation = installation;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var mainPanel = new StackPanel { Spacing = 10, Margin = new Thickness(15) };

            mainPanel.Children.Add(new TextBlock
            {
                Text = "Select a 2DA file to track memory tokens:",
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

            _twodaList = new ListBox { MinHeight = 250 };

            // Populate with 2DA files from installation
            if (_installation != null)
            {
                try
                {
                    var twodaFiles = new List<string>();
                    // Common 2DA files in KotOR/TSL
                    string[] common2DAFiles =
                    {
                        "appearance.2da", "baseitems.2da", "classes.2da", "feat.2da", "spells.2da",
                        "placeables.2da", "upgrade.2da", "upcrystals.2da", "heads.2da", "portraits.2da",
                        "damagetypes.2da", "ambientmusic.2da", "ambientsound.2da", "reputations.2da",
                        "description.2da", "environment.2da", "ranges.2da", "soundset.2da", "skills.2da"
                    };

                    foreach (var fileName in common2DAFiles)
                    {
                        var resource = _installation.Resource(fileName, ResourceType.TwoDA, null);
                        if (resource != null)
                        {
                            twodaFiles.Add(fileName);
                        }
                    }

                    // Sort and add to list
                    foreach (var fileName in twodaFiles.OrderBy(x => x))
                    {
                        _twodaList.Items.Add(fileName);
                    }
                }
                catch
                {
                    // Ignore errors
                }
            }

            mainPanel.Children.Add(_twodaList);

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
            var selectedItem = _twodaList?.SelectedItem;
            if (selectedItem != null)
            {
                Selected2DAFile = selectedItem.ToString();
                Close(true);
            }
            else
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "No Selection",
                    "Please select a 2DA file.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Info);
                errorBox.ShowAsync();
            }
        }
    }

    // Dialog for adding/editing 2DA memory tokens
    // Matching PyKotor implementation pattern for dialogs
    internal class TwoDAMemoryTokenEditDialog : Window
    {
        public TwoDAMemoryTokenEntry TokenEntry { get; private set; }
        private TextBox _tokenNameEdit;
        private TextBox _columnNameEdit;
        private TextBox _rowLabelEdit;
        private Button _okButton;
        private Button _cancelButton;
        private TwoDAMemoryTokenEntry _originalEntry;
        private string _twodaFile;
        private HTInstallation _installation;

        public TwoDAMemoryTokenEditDialog(Window parent, TwoDAMemoryTokenEntry existingEntry, string twodaFile, HTInstallation installation)
        {
            Title = existingEntry == null ? "Add 2DA Memory Token" : "Edit 2DA Memory Token";
            Width = 500;
            Height = 300;
            _originalEntry = existingEntry;
            _twodaFile = twodaFile;
            _installation = installation;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var mainPanel = new StackPanel { Spacing = 10, Margin = new Thickness(15) };

            mainPanel.Children.Add(new TextBlock
            {
                Text = $"2DA File: {_twodaFile ?? "(none)"}",
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

            // Token Name
            var tokenNameLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            tokenNameLayout.Children.Add(new TextBlock { Text = "Token Name:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 });
            _tokenNameEdit = new TextBox { MinWidth = 300 };
            if (_originalEntry != null)
            {
                _tokenNameEdit.Text = _originalEntry.TokenName ?? "";
            }
            else
            {
                _tokenNameEdit.Text = "2DAMEMORY0";
            }
            tokenNameLayout.Children.Add(_tokenNameEdit);
            mainPanel.Children.Add(tokenNameLayout);

            // Column Name
            var columnLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            columnLayout.Children.Add(new TextBlock { Text = "Column Name:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 });
            _columnNameEdit = new TextBox { MinWidth = 300 };
            if (_originalEntry != null)
            {
                _columnNameEdit.Text = _originalEntry.ColumnName ?? "";
            }
            columnLayout.Children.Add(_columnNameEdit);
            mainPanel.Children.Add(columnLayout);

            // Row Label
            var rowLabelLayout = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            rowLabelLayout.Children.Add(new TextBlock { Text = "Row Label:", VerticalAlignment = VerticalAlignment.Center, MinWidth = 120 });
            _rowLabelEdit = new TextBox { MinWidth = 300 };
            if (_originalEntry != null)
            {
                _rowLabelEdit.Text = _originalEntry.RowLabel ?? "";
            }
            rowLabelLayout.Children.Add(_rowLabelEdit);
            mainPanel.Children.Add(rowLabelLayout);

            mainPanel.Children.Add(new TextBlock
            {
                Text = "Note: Column Name and Row Label specify which cell value to store in the token.",
                FontStyle = Avalonia.Media.FontStyle.Italic,
                Foreground = Avalonia.Media.Brushes.Gray
            });

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

            // Extract token ID from token name if it matches pattern 2DAMEMORY#
            int tokenId = 0;
            if (tokenName.StartsWith("2DAMEMORY", StringComparison.OrdinalIgnoreCase))
            {
                string idPart = tokenName.Substring(9); // Skip "2DAMEMORY"
                if (!int.TryParse(idPart, out tokenId))
                {
                    var errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        "Token name must be in format 2DAMEMORY# where # is a number.",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    errorBox.ShowAsync();
                    return;
                }
            }
            else
            {
                var errorBox = MessageBoxManager.GetMessageBoxStandard(
                    "Error",
                    "Token name must start with '2DAMEMORY' followed by a number.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            TokenEntry = new TwoDAMemoryTokenEntry
            {
                TokenName = tokenName,
                TokenId = tokenId,
                ColumnName = _columnNameEdit?.Text?.Trim() ?? "",
                RowLabel = _rowLabelEdit?.Text?.Trim() ?? ""
            };

            if (_originalEntry != null)
            {
                TokenEntry.UsedBy = _originalEntry.UsedBy;
            }

            Close(true);
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
            var mainPanel = new StackPanel { Spacing = 10, Margin = new Thickness(15) };

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

    // Helper class for TLK string entries
    internal class TLKStringEntry
    {
        public string TokenName { get; set; }
        public string Text { get; set; }
        public string Sound { get; set; }
        public int TokenId { get; set; }
        public bool IsReplacement { get; set; }
        public int ModIndex { get; set; } // For replacements, this is the StrRef to replace
        public List<string> UsedBy { get; set; } = new List<string>(); // List of files/sections that use this token
    }

    // Helper class for 2DA memory token entries
    internal class TwoDAMemoryTokenEntry
    {
        public string TokenName { get; set; } // e.g., "2DAMEMORY0"
        public int TokenId { get; set; }
        public string ColumnName { get; set; } // Column name in the 2DA file
        public string RowLabel { get; set; } // Row label/identifier in the 2DA file
        public List<string> UsedBy { get; set; } = new List<string>(); // List of files/sections that use this token
    }
}
