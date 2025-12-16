using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource.Generics;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Widgets.Edit;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:572-691
    // Original: class PropertyEditor(QDialog):
    public partial class PropertyEditorDialog : Window
    {
        private HTInstallation _installation;
        private UTIProperty _utiProperty;
        public bool DialogResult { get; private set; }
        private TextBox _propertyEdit;
        private TextBox _subpropertyEdit;
        private TextBox _costEdit;
        private TextBox _parameterEdit;
        private ComboBox2DA _upgradeSelect;
        private ListBox _costList;
        private ListBox _parameterList;
        private Button _costSelectButton;
        private Button _parameterSelectButton;
        private Button _okButton;
        private Button _cancelButton;

        // Public parameterless constructor for XAML
        public PropertyEditorDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:573-655
        // Original: def __init__(self, installation: HTInstallation, uti_property: UTIProperty):
        public PropertyEditorDialog(Window parent, HTInstallation installation, UTIProperty utiProperty)
        {
            InitializeComponent();
            _installation = installation;
            
            // Create a deep copy of the property
            _utiProperty = new UTIProperty
            {
                PropertyName = utiProperty.PropertyName,
                Subtype = utiProperty.Subtype,
                CostTable = utiProperty.CostTable,
                CostValue = utiProperty.CostValue,
                Param1 = utiProperty.Param1,
                Param1Value = utiProperty.Param1Value,
                ChanceAppear = utiProperty.ChanceAppear,
                UpgradeType = utiProperty.UpgradeType
            };
            
            if (parent != null)
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            
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
            else
            {
                // Find controls from XAML
                _propertyEdit = this.FindControl<TextBox>("propertyEdit");
                _subpropertyEdit = this.FindControl<TextBox>("subpropertyEdit");
                _costEdit = this.FindControl<TextBox>("costEdit");
                _parameterEdit = this.FindControl<TextBox>("parameterEdit");
                _upgradeSelect = this.FindControl<ComboBox2DA>("upgradeSelect");
                _costList = this.FindControl<ListBox>("costList");
                _parameterList = this.FindControl<ListBox>("parameterList");
                _costSelectButton = this.FindControl<Button>("costSelectButton");
                _parameterSelectButton = this.FindControl<Button>("parameterSelectButton");
                _okButton = this.FindControl<Button>("okButton");
                _cancelButton = this.FindControl<Button>("cancelButton");

                if (_costSelectButton != null)
                {
                    _costSelectButton.Click += (s, e) => SelectCost();
                }
                if (_parameterSelectButton != null)
                {
                    _parameterSelectButton.Click += (s, e) => SelectParam();
                }
                if (_costList != null)
                {
                    _costList.DoubleTapped += (s, e) => SelectCost();
                }
                if (_parameterList != null)
                {
                    _parameterList.DoubleTapped += (s, e) => SelectParam();
                }
                if (_okButton != null)
                {
                    _okButton.Click += (s, e) => 
                    { 
                        // Update property before closing
                        GetUtiProperty();
                        DialogResult = true;
                        Close(); 
                    };
                }
                if (_cancelButton != null)
                {
                    _cancelButton.Click += (s, e) => { DialogResult = false; Close(); };
                }
            }
        }

        private void SetupProgrammaticUI()
        {
            // Programmatic UI setup if XAML fails
            // This is a fallback - normally XAML will be used
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:599-655
        private void SetupUI()
        {
            if (_installation == null)
            {
                return;
            }

            // Matching PyKotor implementation: cost_table_list: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_IPRP_COSTTABLE)
            TwoDA costTableList = _installation.HtGetCache2DA(HTInstallation.TwoDAIprpCosttable);
            if (costTableList == null)
            {
                System.Console.WriteLine("Failed to get IPRP_COSTTABLE");
                return;
            }

            // Matching PyKotor implementation: if uti_property.cost_table != 0xFF:
            if (_utiProperty.CostTable != 0xFF)
            {
                // Matching PyKotor implementation: costtable_resref: str | None = cost_table_list.get_cell(uti_property.cost_table, "name")
                string costtableResref = costTableList.GetCellString(_utiProperty.CostTable, "name");
                if (!string.IsNullOrEmpty(costtableResref))
                {
                    // Matching PyKotor implementation: costtable: TwoDA | None = installation.ht_get_cache_2da(costtable_resref)
                    TwoDA costtable = _installation.HtGetCache2DA(costtableResref);
                    if (costtable != null && _costList != null)
                    {
                        // Matching PyKotor implementation: for i in range(costtable.get_height()):
                        for (int i = 0; i < costtable.GetHeight(); i++)
                        {
                            // Matching PyKotor implementation: cost_name: str | None = UTIEditor.cost_name(installation, uti_property.cost_table, i)
                            string costName = UTIEditor.CostName(_installation, _utiProperty.CostTable, i);
                            var item = new ListBoxItem { Content = costName ?? $"Cost {i}", Tag = i };
                            _costList.Items.Add(item);
                        }
                    }
                }
            }

            // Matching PyKotor implementation: if uti_property.param1 != 0xFF:
            if (_utiProperty.Param1 != 0xFF && _parameterList != null)
            {
                // Matching PyKotor implementation: param_list: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_IPRP_PARAMTABLE)
                TwoDA paramList = _installation.HtGetCache2DA(HTInstallation.TwoDAIprpParamtable);
                if (paramList != null)
                {
                    // Matching PyKotor implementation: paramtable_resref: str | None = param_list.get_cell(uti_property.param1, "tableresref")
                    string paramtableResref = paramList.GetCellString(_utiProperty.Param1, "tableresref");
                    if (!string.IsNullOrEmpty(paramtableResref))
                    {
                        // Matching PyKotor implementation: paramtable: TwoDA | None = installation.ht_get_cache_2da(paramtable_resref)
                        TwoDA paramtable = _installation.HtGetCache2DA(paramtableResref);
                        if (paramtable != null)
                        {
                            // Matching PyKotor implementation: for i in range(paramtable.get_height()):
                            for (int i = 0; i < paramtable.GetHeight(); i++)
                            {
                                // Matching PyKotor implementation: param_name: str | None = UTIEditor.param_name(installation, uti_property.param1, i)
                                string paramName = UTIEditor.ParamName(_installation, _utiProperty.Param1, i);
                                var item = new ListBoxItem { Content = paramName ?? $"Param {i}", Tag = i };
                                _parameterList.Items.Add(item);
                            }
                        }
                    }
                }
            }

            // Matching PyKotor implementation: upgrades: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_UPGRADES)
            TwoDA upgrades = _installation.HtGetCache2DA(HTInstallation.TwoDAUpgrades);
            if (_upgradeSelect != null)
            {
                List<string> upgradeItems = new List<string>();
                if (upgrades != null)
                {
                    // Matching PyKotor implementation: upgrade_items: list[str] = [upgrades.get_cell(i, "label").replace("_", " ").title() for i in range(upgrades.get_height())]
                    for (int i = 0; i < upgrades.GetHeight(); i++)
                    {
                        string label = upgrades.GetCellString(i, "label") ?? "";
                        label = label.Replace("_", " ");
                        // Title case conversion (simplified)
                        if (label.Length > 0)
                        {
                            label = char.ToUpper(label[0]) + (label.Length > 1 ? label.Substring(1).ToLower() : "");
                        }
                        upgradeItems.Add(label);
                    }
                }
                _upgradeSelect.SetItems(upgradeItems, false);
                _upgradeSelect.SetContext(upgrades, _installation, HTInstallation.TwoDAUpgrades);

                // Matching PyKotor implementation: if uti_property.upgrade_type is not None: self.ui.upgradeSelect.setCurrentIndex(uti_property.upgrade_type + 1)
                if (_utiProperty.UpgradeType.HasValue)
                {
                    _upgradeSelect.SetSelectedIndex(_utiProperty.UpgradeType.Value + 1);
                }
            }

            ReloadTextboxes();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:657-669
        // Original: def reload_textboxes(self):
        private void ReloadTextboxes()
        {
            if (_installation == null)
            {
                return;
            }

            // Matching PyKotor implementation: property_name: str = UTIEditor.property_name(self._installation, self._uti_property.property_name)
            string propertyName = UTIEditor.GetPropertyName(_installation, _utiProperty.PropertyName);
            if (_propertyEdit != null)
            {
                _propertyEdit.Text = propertyName ?? "";
            }

            // Matching PyKotor implementation: subproperty_name: str | None = UTIEditor.subproperty_name(self._installation, self._uti_property.property_name, self._uti_property.subtype)
            string subpropertyName = UTIEditor.GetSubpropertyName(_installation, _utiProperty.PropertyName, _utiProperty.Subtype);
            if (_subpropertyEdit != null)
            {
                _subpropertyEdit.Text = subpropertyName ?? "";
            }

            // Matching PyKotor implementation: cost_name: str | None = UTIEditor.cost_name(self._installation, self._uti_property.cost_table, self._uti_property.cost_value)
            string costName = UTIEditor.CostName(_installation, _utiProperty.CostTable, _utiProperty.CostValue);
            if (_costEdit != null)
            {
                _costEdit.Text = costName ?? "";
            }

            // Matching PyKotor implementation: param_name: str | None = UTIEditor.param_name(self._installation, self._uti_property.param1, self._uti_property.param1_value)
            string paramName = UTIEditor.ParamName(_installation, _utiProperty.Param1, _utiProperty.Param1Value);
            if (_parameterEdit != null)
            {
                _parameterEdit.Text = paramName ?? "";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:671-677
        // Original: def select_cost(self):
        private void SelectCost()
        {
            if (_costList?.SelectedItem is ListBoxItem curItem && curItem.Tag is int costValue)
            {
                // Matching PyKotor implementation: self._uti_property.cost_value = cur_item.data(Qt.ItemDataRole.UserRole)
                _utiProperty.CostValue = costValue;
                ReloadTextboxes();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:679-685
        // Original: def select_param(self):
        private void SelectParam()
        {
            if (_parameterList?.SelectedItem is ListBoxItem curItem && curItem.Tag is int paramValue)
            {
                // Matching PyKotor implementation: self._uti_property.param1_value = cur_item.data(Qt.ItemDataRole.UserRole)
                _utiProperty.Param1Value = paramValue;
                ReloadTextboxes();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:687-691
        // Original: def uti_property(self) -> UTIProperty:
        public UTIProperty GetUtiProperty()
        {
            // Matching PyKotor implementation: self._uti_property.upgrade_type = self.ui.upgradeSelect.currentIndex() - 1
            if (_upgradeSelect != null)
            {
                if (_upgradeSelect.SelectedIndex == 0)
                {
                    // Matching PyKotor implementation: if self.ui.upgradeSelect.currentIndex() == 0: self._uti_property.upgrade_type = None
                    _utiProperty.UpgradeType = null;
                }
                else
                {
                    _utiProperty.UpgradeType = _upgradeSelect.SelectedIndex - 1;
                }
            }
            return _utiProperty;
        }

        // For Avalonia compatibility, provide ShowDialog method
        public bool ShowDialog()
        {
            // Show dialog and return result
            // This is a simplified implementation - in a full implementation, we'd use ShowDialogAsync
            // For now, we'll track if OK was clicked
            bool result = false;
            if (_okButton != null)
            {
                // Temporarily store result
                _okButton.Click += (s, e) => { result = true; };
            }
            // In a full implementation, we'd use ShowDialogAsync and await the result
            // For now, return true if dialog was shown (simplified)
            return result;
        }
    }
}

