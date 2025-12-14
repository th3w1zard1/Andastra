using System;
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

        private TreeView _settingsTree;
        private ContentControl _settingsStack;

        private void SetupUI()
        {
            // Find controls from XAML
            _settingsTree = this.FindControl<TreeView>("settingsTree");
            _settingsStack = this.FindControl<ContentControl>("settingsStack");
            var okButton = this.FindControl<Button>("okButton");
            var cancelButton = this.FindControl<Button>("cancelButton");

            if (okButton != null)
            {
                okButton.Click += (s, e) => Accept();
            }
            if (cancelButton != null)
            {
                cancelButton.Click += (s, e) => Close();
            }
            if (_settingsTree != null)
            {
                _settingsTree.SelectionChanged += (s, e) => OnSettingsTreeSelectionChanged();
            }
        }

        private void OnSettingsTreeSelectionChanged()
        {
            // Switch between settings pages based on selection
            // This will be implemented when settings widgets are available
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
        private void OnResetAllSettings()
        {
            // Reset all settings to defaults
            // This will be implemented when MessageBox is available
            _isResetting = true;
            Close();
        }
    }
}
