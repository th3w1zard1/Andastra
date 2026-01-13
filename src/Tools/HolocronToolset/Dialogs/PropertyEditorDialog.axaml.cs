using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource.Formats.GFF.Generics;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Widgets.Edit;
using UTC = BioWare.NET.Resource.Formats.GFF.Generics.UTC.UTC;
using UTI = BioWare.NET.Resource.Formats.GFF.Generics.UTI.UTI;
using UTIProperty = BioWare.NET.Resource.Formats.GFF.Generics.UTI.UTIProperty;

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
                        // Close with true result for ShowDialogAsync<bool> support
                        Close(true);
                    };
                }
                if (_cancelButton != null)
                {
                    _cancelButton.Click += (s, e) =>
                    {
                        DialogResult = false;
                        // Close with false result for ShowDialogAsync<bool> support
                        Close(false);
                    };
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

            // Matching PyKotor implementation: cost_table_list: 2DA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_IPRP_COSTTABLE)
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
                    // Matching PyKotor implementation: costtable: 2DA | None = installation.ht_get_cache_2da(costtable_resref)
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
                // Matching PyKotor implementation: param_list: 2DA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_IPRP_PARAMTABLE)
                TwoDA paramList = _installation.HtGetCache2DA(HTInstallation.TwoDAIprpParamtable);
                if (paramList != null)
                {
                    // Matching PyKotor implementation: paramtable_resref: str | None = param_list.get_cell(uti_property.param1, "tableresref")
                    string paramtableResref = paramList.GetCellString(_utiProperty.Param1, "tableresref");
                    if (!string.IsNullOrEmpty(paramtableResref))
                    {
                        // Matching PyKotor implementation: paramtable: 2DA | None = installation.ht_get_cache_2da(paramtable_resref)
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

            // Matching PyKotor implementation: upgrades: 2DA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_UPGRADES)
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
                        label = ToTitleCase(label);
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

        // Matching Python's str.title() method behavior
        // Converts string to title case where first letter of each word is capitalized
        // Examples: "hello world" -> "Hello World", "test_string" -> "Test String" (after replace)
        // This matches PyKotor's .title() behavior for upgrade label formatting
        // Only capitalizes if the first character is a letter; preserves numbers and other characters
        private static string ToTitleCase(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            // Split by space to get individual words (after replace("_", " "), we have space-separated words)
            // Using StringSplitOptions.None to preserve empty entries (for multiple consecutive spaces)
            string[] words = input.Split(new[] { ' ' }, StringSplitOptions.None);

            // Process each word: capitalize first letter if it's a letter, lowercase the rest
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    char firstChar = words[i][0];
                    if (char.IsLetter(firstChar))
                    {
                        // First character is a letter - capitalize it and lowercase the rest
                        words[i] = char.ToUpperInvariant(firstChar) +
                                   (words[i].Length > 1 ? words[i].Substring(1).ToLowerInvariant() : "");
                    }
                    else
                    {
                        // First character is not a letter (number, symbol, etc.) - only lowercase the rest
                        // This matches Python's .title() behavior: "123test" stays "123test", not "123Test"
                        if (words[i].Length > 1)
                        {
                            // Lowercase any letters in the rest of the word
                            System.Text.StringBuilder sb = new System.Text.StringBuilder(words[i].Length);
                            sb.Append(firstChar);
                            for (int j = 1; j < words[i].Length; j++)
                            {
                                char c = words[i][j];
                                sb.Append(char.IsLetter(c) ? char.ToLowerInvariant(c) : c);
                            }
                            words[i] = sb.ToString();
                        }
                    }
                }
            }

            // Rejoin with spaces (preserving original spacing structure)
            return string.Join(" ", words);
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:238-244
        // Original: if not dialog.exec(): return
        // PyKotor's QDialog.exec() is a blocking modal dialog that returns QDialog.DialogCode.Accepted (true) or Rejected (false)
        // This synchronous method provides the same behavior for compatibility with existing code

        /// <summary>
        /// Shows the dialog modally and returns true if the user clicked OK, false if Cancel was clicked or the dialog was closed.
        /// This is a blocking synchronous method that matches PyKotor's QDialog.exec() behavior.
        /// </summary>
        /// <param name="parent">The parent window for the dialog. If null, the dialog will be shown without a parent.</param>
        /// <returns>True if OK was clicked, false if Cancel was clicked or the dialog was closed.</returns>
        public new bool ShowDialog(Window parent = null)
        {
            // Use ShowDialogAsync and block synchronously to match Qt's exec() behavior
            // This provides proper modal dialog behavior while maintaining compatibility with synchronous code
            Task<bool> dialogTask = ShowDialogAsync(parent);
            return dialogTask.GetAwaiter().GetResult();
        }

        /// <summary>
        /// Shows the dialog modally asynchronously and returns a Task that completes with true if the user clicked OK, false if Cancel was clicked or the dialog was closed.
        /// This is the recommended method for async/await code.
        /// </summary>
        /// <param name="parent">The parent window for the dialog. If null, the dialog will be shown without a parent.</param>
        /// <returns>A Task that completes with true if OK was clicked, false if Cancel was clicked or the dialog was closed.</returns>
        public async Task<bool> ShowDialogAsync(Window parent = null)
        {
            // Set parent if provided for proper modal behavior
            if (parent != null)
            {
                // ShowDialogAsync will handle setting the parent relationship
                // The result will be the value passed to Close() when the dialog closes
                var resultObj = await ShowDialogAsync(parent);
                bool result = resultObj is bool b ? b : false;
                DialogResult = result;
                return result;
            }
            else
            {
                // No parent - show as top-level window
                // In Avalonia, we still need a parent for ShowDialogAsync, so we'll find the main window
                Window mainWindow = null;
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    mainWindow = desktop.MainWindow;
                }

                if (mainWindow != null)
                {
                    var resultObj = await ShowDialogAsync(mainWindow);
                    bool result = resultObj is bool b ? b : false;
                    DialogResult = result;
                    return result;
                }
                else
                {
                    // Fallback: show non-modally and track result via Closed event
                    // This should rarely happen, but provides a fallback
                    bool result = false;
                    EventHandler closedHandler = null;
                    closedHandler = (s, e) =>
                    {
                        this.Closed -= closedHandler;
                        result = DialogResult;
                    };
                    this.Closed += closedHandler;
                    Show();
                    // Wait for dialog to close
                    // Note: This is not ideal but provides fallback behavior
                    while (this.IsVisible)
                    {
                        await Task.Delay(10);
                    }
                    return result;
                }
            }
        }
    }
}


