using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

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

        private void CreateTLKStrRefTab()
        {
            var tab = new TabItem { Header = "TLK StrRef" };
            var content = new StackPanel();
            // TODO: Add TLK StrRef controls
            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
        }

        private void CreateGFFFieldsTab()
        {
            var tab = new TabItem { Header = "GFF Fields" };
            var content = new StackPanel();
            // TODO: Add GFF fields controls
            tab.Content = content;
            if (_configTabs != null)
            {
                _configTabs.Items.Add(tab);
            }
        }

        private void CreateScriptsTab()
        {
            var tab = new TabItem { Header = "Scripts" };
            var content = new StackPanel();
            // TODO: Add scripts controls
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/tslpatchdata_editor.py:150-459
        // Original: Various methods for TSLPatchData generation
        private void BrowseTslpatchdataPath()
        {
            // TODO: Implement folder browser dialog when available
            System.Console.WriteLine("Browse TSLPatchData path not yet implemented");
        }

        private void CreateNewTslpatchdata()
        {
            // TODO: Create new TSLPatchData folder structure
            System.Console.WriteLine("Create new TSLPatchData not yet implemented");
        }

        private void LoadExistingConfig()
        {
            // TODO: Load existing TSLPatchData configuration
            System.Console.WriteLine($"Loading existing config from {_tslpatchdataPath}");
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
            previewLines.AppendLine("[GFF files]");
            previewLines.AppendLine("; Files to be patched");
            previewLines.AppendLine();

            // 2DAMEMORY section
            previewLines.AppendLine("[2DAMEMORY]");
            previewLines.AppendLine("; 2DA memory tokens");
            previewLines.AppendLine();

            // TLKList section
            previewLines.AppendLine("[TLKList]");
            previewLines.AppendLine("; TLK string additions");
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
    }
}
