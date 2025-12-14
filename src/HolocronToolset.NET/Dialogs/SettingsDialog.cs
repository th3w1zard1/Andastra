using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Dialogs
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

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Settings",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            var closeButton = new Button
            {
                Content = "Close",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            closeButton.Click += (sender, e) => Close();

            panel.Children.Add(titleLabel);
            panel.Children.Add(closeButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/settings.py:42-43
            // Original: self.ui = settings.Ui_Dialog(); self.ui.setupUi(self)
            // Find all controls from XAML and expose via Ui property
            var settingsTree = this.FindControl<TreeView>("settingsTree");
            var settingsStack = this.FindControl<ContentControl>("settingsStack");
            var okButton = this.FindControl<Button>("okButton");
            var cancelButton = this.FindControl<Button>("cancelButton");

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
