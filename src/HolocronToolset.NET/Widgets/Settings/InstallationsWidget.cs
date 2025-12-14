using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:38
    // Original: class InstallationsWidget(QWidget):
    public partial class InstallationsWidget : UserControl
    {
        private ListBox _pathList;
        private Button _addPathButton;
        private Button _removePathButton;
        private Border _pathFrame;
        private TextBox _pathNameEdit;
        private TextBox _pathDirEdit;
        private CheckBox _pathTslCheckbox;
        private GlobalSettings _settings;

        public InstallationsWidget()
        {
            InitializeComponent();
            _settings = new GlobalSettings();
            SetupValues();
            SetupSignals();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _pathList = this.FindControl<ListBox>("pathList");
            _addPathButton = this.FindControl<Button>("addPathButton");
            _removePathButton = this.FindControl<Button>("removePathButton");
            _pathFrame = this.FindControl<Border>("pathFrame");
            _pathNameEdit = this.FindControl<TextBox>("pathNameEdit");
            _pathDirEdit = this.FindControl<TextBox>("pathDirEdit");
            _pathTslCheckbox = this.FindControl<CheckBox>("pathTslCheckbox");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:57-62
        // Original: def setup_values(self):
        private void SetupValues()
        {
            if (_pathList != null)
            {
                _pathList.Items.Clear();
                var installations = _settings.Installations();
                foreach (var installation in installations.Values)
                {
                    _pathList.Items.Add(installation.ContainsKey("name") ? installation["name"]?.ToString() : "Unknown");
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:64-74
        // Original: def setup_signals(self):
        private void SetupSignals()
        {
            if (_addPathButton != null)
            {
                _addPathButton.Click += (s, e) => AddNewInstallation();
            }
            if (_removePathButton != null)
            {
                _removePathButton.Click += (s, e) => RemoveSelectedInstallation();
            }
            if (_pathNameEdit != null)
            {
                _pathNameEdit.TextChanged += (s, e) => UpdateInstallation();
            }
            if (_pathDirEdit != null)
            {
                _pathDirEdit.TextChanged += (s, e) => UpdateInstallation();
            }
            if (_pathTslCheckbox != null)
            {
                _pathTslCheckbox.IsCheckedChanged += (s, e) => UpdateInstallation();
            }
            if (_pathList != null)
            {
                _pathList.SelectionChanged += (s, e) => InstallationSelected();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:76-87
        // Original: def save(self):
        public void Save()
        {
            // Save installations to settings
            // This will be implemented when settings persistence is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:89-94
        // Original: def add_new_installation(self):
        private void AddNewInstallation()
        {
            if (_pathList != null)
            {
                _pathList.Items.Add("New");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:96-102
        // Original: def remove_selected_installation(self):
        private void RemoveSelectedInstallation()
        {
            if (_pathList?.SelectedItem != null)
            {
                _pathList.Items.Remove(_pathList.SelectedItem);
            }
            if (_pathList?.SelectedItem == null && _pathFrame != null)
            {
                _pathFrame.IsEnabled = false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:107-119
        // Original: def update_installation(self):
        private void UpdateInstallation()
        {
            // Update installation data when fields change
            // This will be implemented when data binding is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:121-133
        // Original: def installation_selected(self):
        private void InstallationSelected()
        {
            if (_pathList?.SelectedItem != null && _pathFrame != null)
            {
                _pathFrame.IsEnabled = true;
                // Load installation data into fields
                if (_pathNameEdit != null)
                {
                    _pathNameEdit.Text = _pathList.SelectedItem?.ToString() ?? "";
                }
            }
        }
    }
}
