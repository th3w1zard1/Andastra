using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:18
    // Original: class SettingsDialog(QDialog):
    public partial class SettingsDialog : Window
    {
        private bool _isResetting;
        private bool _installationEdited;
        private GlobalSettings _settings;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:42-43
        // Original: self.ui = settings.Ui_Dialog(); self.ui.setupUi(self)
        public SettingsDialogUi Ui { get; private set; }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:38
        // Original: self._is_resetting: bool = False
        public bool IsResetting => _isResetting;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:19-125
        // Original: def __init__(self, parent):
        public SettingsDialog(Window parent = null)
        {
            InitializeComponent();
            _isResetting = false;
            _installationEdited = false;
            _settings = new GlobalSettings();
            SetupUI();
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
            Title = "Settings";
            Width = 600;
            Height = 500;

            // Create programmatic UI matching XAML structure
            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Create splitter grid
            var splitGrid = new Grid();
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(splitGrid, 0);
            mainGrid.Children.Add(splitGrid);

            // Create settings tree
            var settingsTree = new TreeView();
            settingsTree.Items.Add(new TreeViewItem { Header = "Installations", IsSelected = true });
            settingsTree.Items.Add(new TreeViewItem { Header = "GIT Editor" });
            settingsTree.Items.Add(new TreeViewItem { Header = "Module Designer" });
            settingsTree.Items.Add(new TreeViewItem { Header = "Misc" });
            settingsTree.Items.Add(new TreeViewItem { Header = "Application" });
            Grid.SetColumn(settingsTree, 0);
            splitGrid.Children.Add(settingsTree);

            // Create settings stack
            var settingsStack = new ContentControl();
            Grid.SetColumn(settingsStack, 1);
            splitGrid.Children.Add(settingsStack);

            // Create button grid
            var buttonGrid = new Grid();
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(buttonGrid, 1);
            mainGrid.Children.Add(buttonGrid);

            var okButton = new Button { Content = "OK", Width = 75, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Avalonia.Thickness(0, 0, 5, 0) };
            Grid.SetColumn(okButton, 0);
            buttonGrid.Children.Add(okButton);

            var cancelButton = new Button { Content = "Cancel", Width = 75, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            Grid.SetColumn(cancelButton, 1);
            buttonGrid.Children.Add(cancelButton);

            // Create placeholder pages
            var installationsPage = new Control();
            var gitEditorPage = new Control();
            var miscPage = new Control();
            var moduleDesignerPage = new Control();
            var applicationSettingsPage = new Control();

            Ui = new SettingsDialogUi
            {
                SettingsTree = settingsTree,
                SettingsStack = settingsStack,
                InstallationsPage = installationsPage,
                GitEditorPage = gitEditorPage,
                MiscPage = miscPage,
                ModuleDesignerPage = moduleDesignerPage,
                ApplicationSettingsPage = applicationSettingsPage,
                OkButton = okButton,
                CancelButton = cancelButton
            };

            // Set up signals
            okButton.Click += (s, e) => Accept();
            cancelButton.Click += (s, e) => Close();

            var pageDict = new Dictionary<string, Control>
            {
                { "Installations", Ui.InstallationsPage },
                { "GIT Editor", Ui.GitEditorPage },
                { "Misc", Ui.MiscPage },
                { "Module Designer", Ui.ModuleDesignerPage },
                { "Application", Ui.ApplicationSettingsPage }
            };

            settingsTree.SelectionChanged += (s, e) => OnSettingsTreeSelectionChanged(pageDict);

            // Set initial page
            settingsStack.Content = installationsPage;

            Content = mainGrid;
        }

        private void SetupUI()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:42-43
            // Original: self.ui = settings.Ui_Dialog(); self.ui.setupUi(self)
            // Find all controls from XAML and expose via Ui property
            TreeView settingsTree = null;
            ContentControl settingsStack = null;
            Button okButton = null;
            Button cancelButton = null;
            
            try
            {
                settingsTree = this.FindControl<TreeView>("settingsTree");
                settingsStack = this.FindControl<ContentControl>("settingsStack");
                okButton = this.FindControl<Button>("okButton");
                cancelButton = this.FindControl<Button>("cancelButton");
            }
            catch (InvalidOperationException)
            {
                // XAML not loaded or controls not found - create programmatic UI
                SetupProgrammaticUI();
                return;
            }

            // Create placeholder pages for testing
            var installationsPage = new Control();
            var gitEditorPage = new Control();
            var miscPage = new Control();
            var moduleDesignerPage = new Control();
            var applicationSettingsPage = new Control();

            Ui = new SettingsDialogUi
            {
                SettingsTree = settingsTree,
                SettingsStack = settingsStack,
                InstallationsPage = installationsPage,
                GitEditorPage = gitEditorPage,
                MiscPage = miscPage,
                ModuleDesignerPage = moduleDesignerPage,
                ApplicationSettingsPage = applicationSettingsPage,
                OkButton = okButton,
                CancelButton = cancelButton
            };

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:52-58
            // Original: self.page_dict: dict[str, QWidget]
            var pageDict = new Dictionary<string, Control>
            {
                { "Installations", Ui.InstallationsPage },
                { "GIT Editor", Ui.GitEditorPage },
                { "Misc", Ui.MiscPage },
                { "Module Designer", Ui.ModuleDesignerPage },
                { "Application", Ui.ApplicationSettingsPage }
            };

            if (Ui.OkButton != null)
            {
                Ui.OkButton.Click += (s, e) => Accept();
            }
            if (Ui.CancelButton != null)
            {
                Ui.CancelButton.Click += (s, e) => Close();
            }
            if (Ui.SettingsTree != null)
            {
                Ui.SettingsTree.SelectionChanged += (s, e) => OnSettingsTreeSelectionChanged(pageDict);
            }

            // Set initial page
            if (Ui.SettingsStack != null && Ui.InstallationsPage != null)
            {
                Ui.SettingsStack.Content = Ui.InstallationsPage;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:75-92
        // Original: def on_page_change(self, page_tree_item: QTreeWidgetItem):
        private void OnSettingsTreeSelectionChanged(Dictionary<string, Control> pageDict)
        {
            if (Ui?.SettingsTree?.SelectedItem is TreeViewItem item)
            {
                string pageName = item.Header?.ToString() ?? "";
                if (pageDict.ContainsKey(pageName) && Ui.SettingsStack != null)
                {
                    Ui.SettingsStack.Content = pageDict[pageName];
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:116-125
        // Original: def accept(self):
        public void Accept()
        {
            // Save settings
            if (!_isResetting)
            {
                // Save all settings widgets
                // This will be implemented when settings widgets are available
            }
            Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:94-111
        // Original: def on_reset_all_settings(self):
        public void OnResetAllSettings()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:95-111
            // Original: QMessageBox.question and QMessageBox.information
            // In C#, we'll use a simple approach - clear settings and close
            // For full implementation, would use Avalonia MessageBox
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:104
            // Original: GlobalSettings().settings.clear()
            _settings.Clear();
            _isResetting = true;
            Close();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:42-43
    // Original: self.ui = settings.Ui_Dialog() - UI wrapper class exposing all controls
    public class SettingsDialogUi
    {
        public TreeView SettingsTree { get; set; }
        public ContentControl SettingsStack { get; set; }
        public Control InstallationsPage { get; set; }
        public Control GitEditorPage { get; set; }
        public Control MiscPage { get; set; }
        public Control ModuleDesignerPage { get; set; }
        public Control ApplicationSettingsPage { get; set; }
        public Button OkButton { get; set; }
        public Button CancelButton { get; set; }
    }
}
