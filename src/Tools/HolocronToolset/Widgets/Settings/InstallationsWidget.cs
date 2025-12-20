using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;

namespace HolocronToolset.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:38
    // Original: class InstallationsWidget(QWidget):
    public partial class InstallationsWidget : UserControl
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:39
        // Original: sig_settings_edited: ClassVar[Signal] = Signal()
        // Event emitted when installations are edited
        public event EventHandler SettingsEdited;

        private ListBox _pathList;
        private Button _addPathButton;
        private Button _removePathButton;
        private Border _pathFrame;
        private TextBox _pathNameEdit;
        private TextBox _pathDirEdit;
        private CheckBox _pathTslCheckbox;
        private GlobalSettings _settings;
        
        // Store installation data: name -> {path, tsl}
        private Dictionary<string, Dictionary<string, object>> _installationData;

        public InstallationsWidget()
        {
            InitializeComponent();
            _settings = new GlobalSettings();
            _installationData = new Dictionary<string, Dictionary<string, object>>();
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
                _installationData.Clear();
                var installations = _settings.Installations();
                foreach (var kvp in installations)
                {
                    string name = kvp.Key;
                    var installData = kvp.Value;
                    _pathList.Items.Add(name);
                    // Store installation data
                    _installationData[name] = new Dictionary<string, object>
                    {
                        { "name", name },
                        { "path", installData.ContainsKey("path") ? installData["path"] : "" },
                        { "tsl", installData.ContainsKey("tsl") ? installData["tsl"] : false }
                    };
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
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:76-87
            // Original: def save(self): installations: dict[str, dict[str, str]] = {}
            Dictionary<string, Dictionary<string, object>> installations = new Dictionary<string, Dictionary<string, object>>();

            // Matching PyKotor implementation: for row in range(self.installations_model.rowCount()):
            foreach (var item in _pathList.Items)
            {
                string itemText = item?.ToString() ?? "";
                if (string.IsNullOrEmpty(itemText))
                {
                    continue;
                }

                // Get installation data for this name
                if (_installationData.ContainsKey(itemText))
                {
                    var installData = new Dictionary<string, object>(_installationData[itemText]);
                    installData["name"] = itemText;
                    installations[itemText] = installData;
                }
                else
                {
                    // New installation without data - create default entry
                    installations[itemText] = new Dictionary<string, object>
                    {
                        { "name", itemText },
                        { "path", "" },
                        { "tsl", false }
                    };
                }
            }

            // Matching PyKotor implementation: self.settings.settings.setValue("installations", installations)
            _settings.SetInstallations(installations);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:89-94
        // Original: def add_new_installation(self):
        private void AddNewInstallation()
        {
            if (_pathList != null)
            {
                // Matching PyKotor implementation: item: QStandardItem = QStandardItem(tr("New"))
                string newName = "New";
                _pathList.Items.Add(newName);
                
                // Matching PyKotor implementation: item.setData({"path": "", "tsl": False})
                _installationData[newName] = new Dictionary<string, object>
                {
                    { "name", newName },
                    { "path", "" },
                    { "tsl", false }
                };
                
                // Matching PyKotor implementation: self.sig_settings_edited.emit()
                SettingsEdited?.Invoke(this, EventArgs.Empty);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:96-102
        // Original: def remove_selected_installation(self):
        private void RemoveSelectedInstallation()
        {
            if (_pathList?.SelectedItem != null)
            {
                string selectedName = _pathList.SelectedItem.ToString();
                _pathList.Items.Remove(_pathList.SelectedItem);
                
                // Remove from installation data
                if (_installationData.ContainsKey(selectedName))
                {
                    _installationData.Remove(selectedName);
                }
                
                // Matching PyKotor implementation: self.sig_settings_edited.emit()
                SettingsEdited?.Invoke(this, EventArgs.Empty);
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
            if (_pathList?.SelectedItem == null)
            {
                return;
            }

            string selectedName = _pathList.SelectedItem.ToString();
            if (string.IsNullOrEmpty(selectedName))
            {
                return;
            }

            // Matching PyKotor implementation: index: QModelIndex = self.ui.pathList.selectedIndexes()[0]
            // Get or create installation data
            if (!_installationData.ContainsKey(selectedName))
            {
                _installationData[selectedName] = new Dictionary<string, object>
                {
                    { "name", selectedName },
                    { "path", "" },
                    { "tsl", false }
                };
            }

            var data = _installationData[selectedName];
            
            // Matching PyKotor implementation: data["path"] = self.ui.pathDirEdit.text()
            if (_pathDirEdit != null)
            {
                data["path"] = _pathDirEdit.Text ?? "";
            }
            
            // Matching PyKotor implementation: data["tsl"] = self.ui.pathTslCheckbox.isChecked()
            if (_pathTslCheckbox != null)
            {
                data["tsl"] = _pathTslCheckbox.IsChecked ?? false;
            }

            // Matching PyKotor implementation: item.setText(self.ui.pathNameEdit.text())
            if (_pathNameEdit != null)
            {
                string newName = _pathNameEdit.Text ?? "";
                if (!string.IsNullOrEmpty(newName) && newName != selectedName)
                {
                    // Name changed - update the item in the list
                    int index = _pathList.Items.IndexOf(selectedName);
                    if (index >= 0)
                    {
                        _pathList.Items[index] = newName;
                        
                        // Update dictionary key
                        if (_installationData.ContainsKey(selectedName))
                        {
                            var oldData = _installationData[selectedName];
                            _installationData.Remove(selectedName);
                            oldData["name"] = newName;
                            _installationData[newName] = oldData;
                        }
                    }
                }
            }

            // Matching PyKotor implementation: self.sig_settings_edited.emit()
            SettingsEdited?.Invoke(this, EventArgs.Empty);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/installations.py:121-133
        // Original: def installation_selected(self):
        private void InstallationSelected()
        {
            if (_pathList?.SelectedItem != null && _pathFrame != null)
            {
                _pathFrame.IsEnabled = true;
                
                string selectedName = _pathList.SelectedItem.ToString();
                
                // Matching PyKotor implementation: item_text: str = item.text()
                // Matching PyKotor implementation: item_data: dict[str, Any] = item.data()
                if (_installationData.ContainsKey(selectedName))
                {
                    var itemData = _installationData[selectedName];
                    
                    // Matching PyKotor implementation: self.ui.pathNameEdit.setText(item_text)
                    if (_pathNameEdit != null)
                    {
                        _pathNameEdit.Text = selectedName;
                    }
                    
                    // Matching PyKotor implementation: self.ui.pathDirEdit.setText(item_data["path"])
                    if (_pathDirEdit != null)
                    {
                        _pathDirEdit.Text = itemData.ContainsKey("path") ? itemData["path"]?.ToString() ?? "" : "";
                    }
                    
                    // Matching PyKotor implementation: self.ui.pathTslCheckbox.setChecked(bool(item_data["tsl"]))
                    if (_pathTslCheckbox != null)
                    {
                        bool tslValue = false;
                        if (itemData.ContainsKey("tsl") && itemData["tsl"] is bool tsl)
                        {
                            tslValue = tsl;
                        }
                        else if (itemData.ContainsKey("tsl"))
                        {
                            bool.TryParse(itemData["tsl"]?.ToString(), out tslValue);
                        }
                        _pathTslCheckbox.IsChecked = tslValue;
                    }
                }
                else
                {
                    // No data for this installation - load defaults
                    if (_pathNameEdit != null)
                    {
                        _pathNameEdit.Text = selectedName;
                    }
                    if (_pathDirEdit != null)
                    {
                        _pathDirEdit.Text = "";
                    }
                    if (_pathTslCheckbox != null)
                    {
                        _pathTslCheckbox.IsChecked = false;
                    }
                }
            }
            else if (_pathFrame != null)
            {
                _pathFrame.IsEnabled = false;
            }
        }
    }
}
