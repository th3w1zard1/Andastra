using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using FontInfo = HolocronToolset.Dialogs.FontInfo;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:27
    // Original: class ApplicationSettingsWidget(SettingsWidget):
    public partial class ApplicationSettingsWidget : UserControl
    {
        private Button _resetAttributesButton;
        private TextBlock _currentFontLabel;
        private Button _fontButton;
        private DataGrid _tableWidget;
        private Button _addButton;
        private Button _editButton;
        private Button _removeButton;
        private StackPanel _verticalLayoutMisc;
        private StackPanel _verticalLayout3;
        private GlobalSettings _settings;
        private List<EnvironmentVariable> _environmentVariables;

        public ApplicationSettingsWidget()
        {
            InitializeComponent();
            _settings = new GlobalSettings();
            _environmentVariables = new List<EnvironmentVariable>();
            SetupUI();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _resetAttributesButton = this.FindControl<Button>("resetAttributesButton");
            _currentFontLabel = this.FindControl<TextBlock>("currentFontLabel");
            _fontButton = this.FindControl<Button>("fontButton");
            _tableWidget = this.FindControl<DataGrid>("tableWidget");
            _addButton = this.FindControl<Button>("addButton");
            _editButton = this.FindControl<Button>("editButton");
            _removeButton = this.FindControl<Button>("removeButton");
            _verticalLayoutMisc = this.FindControl<StackPanel>("verticalLayout_misc");
            _verticalLayout3 = this.FindControl<StackPanel>("verticalLayout_3");
        }

        private void SetupUI()
        {
            if (_resetAttributesButton != null)
            {
                _resetAttributesButton.Click += (s, e) => ResetAttributes();
            }
            if (_fontButton != null)
            {
                _fontButton.Click += (s, e) => SelectFont();
            }
            if (_addButton != null)
            {
                _addButton.Click += (s, e) => AddEnvironmentVariable();
            }
            if (_editButton != null)
            {
                _editButton.Click += (s, e) => EditEnvironmentVariable();
            }
            if (_removeButton != null)
            {
                _removeButton.Click += (s, e) => RemoveEnvironmentVariable();
            }

            UpdateFontLabel();
            PopulateAll();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:57-67
        // Original: def update_font_label(self):
        private void UpdateFontLabel()
        {
            if (_currentFontLabel == null)
            {
                return;
            }

            string fontString = _settings.GlobalFont;
            if (!string.IsNullOrEmpty(fontString))
            {
                try
                {
                    // Parse font string (format: "Family,Size,Style,Weight" or similar)
                    // For simplicity, we'll store as "Family|Size|Style|Weight"
                    var parts = fontString.Split('|');
                    if (parts.Length >= 2)
                    {
                        string family = parts[0];
                        if (double.TryParse(parts[1], out double size))
                        {
                            _currentFontLabel.Text = $"Current Font: {family}, {size} pt";
                            return;
                        }
                    }
                }
                catch
                {
                    // If parsing fails, fall through to default
                }
            }

            _currentFontLabel.Text = "Current Font: Default";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:69-77
        // Original: def select_font(self): QFontDialog.getFont(current_font, self)
        private async void SelectFont()
        {
            Window parentWindow = GetParentWindow();
            if (parentWindow == null)
            {
                return;
            }

            // Get current font from settings or use default
            FontInfo currentFont = null;
            string fontString = _settings.GlobalFont;
            if (!string.IsNullOrEmpty(fontString))
            {
                try
                {
                    // Parse font string (format: "Family|Size|Style|Weight")
                    var parts = fontString.Split('|');
                    if (parts.Length >= 2)
                    {
                        string family = parts[0].Trim();
                        if (double.TryParse(parts[1].Trim(), out double size))
                        {
                            bool isBold = false;
                            bool isItalic = false;

                            // Parse weight if available
                            if (parts.Length >= 4 && int.TryParse(parts[3].Trim(), out int weightValue))
                            {
                                isBold = weightValue >= 700;
                            }

                            // Parse style if available
                            if (parts.Length >= 3)
                            {
                                string styleStr = parts[2].Trim().ToLowerInvariant();
                                if (styleStr.Contains("italic"))
                                {
                                    isItalic = true;
                                }
                            }

                            currentFont = new FontInfo
                            {
                                FamilyName = family,
                                Size = size,
                                IsBold = isBold,
                                IsItalic = isItalic
                            };
                        }
                    }
                }
                catch
                {
                    // Use default font if parsing fails
                }
            }

            // Create and show font dialog
            var fontDialog = new Dialogs.FontDialog(parentWindow);
            if (currentFont != null)
            {
                fontDialog.SetCurrentFont(currentFont);
            }

            // Show dialog and wait for result
            await fontDialog.ShowDialog(parentWindow);

            // If user clicked OK, save the font
            if (fontDialog.DialogResult && fontDialog.SelectedFont != null)
            {
                var selectedFont = fontDialog.SelectedFont;

                // Save font as string (format: "Family|Size|Style|Weight")
                // Matching PyKotor: self.settings.settings.setValue("GlobalFont", font.toString())
                string fontStringToSave = $"{selectedFont.FamilyName ?? "Arial"}|{selectedFont.Size}|" +
                    $"{(selectedFont.IsItalic ? "Italic" : "Normal")}|" +
                    $"{(selectedFont.IsBold ? "700" : "400")}";

                _settings.GlobalFont = fontStringToSave;

                // Update the label
                UpdateFontLabel();

                // Apply the font globally to the application
                // Matching PyKotor: QApplication.setFont(font) applies globally to all widgets
                // In Avalonia, we apply fonts via styles to achieve the same effect
                HolocronToolset.Utils.FontApplicationHelper.ApplyGlobalFont(fontStringToSave);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:79-126
        // Original: def populate_all(self):
        private void PopulateAll()
        {
            // Populate environment variables from settings
            PopulateEnvironmentVariables();

            // Populate miscellaneous settings
            // This will be implemented when settings are fully available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:97-102
        // Original: def populate_all(self) - environment variables section
        private void PopulateEnvironmentVariables()
        {
            if (_tableWidget == null)
            {
                return;
            }

            // Load environment variables from settings
            var envVarsDict = _settings.AppEnvVariables;
            _environmentVariables = new List<EnvironmentVariable>();

            foreach (var kvp in envVarsDict)
            {
                _environmentVariables.Add(new EnvironmentVariable(kvp.Key, kvp.Value));
            }

            _tableWidget.ItemsSource = _environmentVariables;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:41
        // Original: def reset_attributes(self):
        private void ResetAttributes()
        {
            // Reset all attributes to defaults
            // This will be implemented when settings are fully available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:181-201
        // Original: def add_environment_variable(self):
        private async void AddEnvironmentVariable()
        {
            Window parentWindow = GetParentWindow();
            if (parentWindow == null)
            {
                return;
            }

            var dialog = new EnvVariableDialog(parentWindow);
            await dialog.ShowDialog(parentWindow);

            // Get data from dialog after it closes
            var result = dialog.GetData();
            if (result != null && !string.IsNullOrWhiteSpace(result.Item1))
            {
                string key = result.Item1.Trim();
                string value = result.Item2 ?? "";

                // Check if key already exists
                if (_environmentVariables.Any(ev => ev.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                {
                    var msgBox = MessageBoxManager.GetMessageBoxStandard(
                        "Duplicate Variable",
                        $"An environment variable with key '{key}' already exists.",
                        ButtonEnum.Ok,
                        Icon.Warning);
                    await msgBox.ShowAsync();
                    return;
                }

                // Add to list
                var newVar = new EnvironmentVariable(key, value);
                _environmentVariables.Add(newVar);
                _tableWidget.ItemsSource = null;
                _tableWidget.ItemsSource = _environmentVariables;

                // Save to settings
                SaveEnvironmentVariable(key, value);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:203-236
        // Original: def edit_environment_variable(self):
        private async void EditEnvironmentVariable()
        {
            if (_tableWidget == null || _tableWidget.SelectedItem == null)
            {
                var msgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Edit Variable",
                    "Please select a variable to edit.",
                    ButtonEnum.Ok,
                    Icon.Warning);
                await msgBox.ShowAsync();
                return;
            }

            var selectedVar = _tableWidget.SelectedItem as EnvironmentVariable;
            if (selectedVar == null)
            {
                return;
            }

            Window parentWindow = GetParentWindow();
            if (parentWindow == null)
            {
                return;
            }

            string oldKey = selectedVar.Key;
            var dialog = new EnvVariableDialog(parentWindow);
            dialog.SetData(selectedVar.Key, selectedVar.Value);
            await dialog.ShowDialog(parentWindow);

            // Get data from dialog after it closes
            var result = dialog.GetData();
            if (result != null && !string.IsNullOrWhiteSpace(result.Item1))
            {
                string newKey = result.Item1.Trim();
                string newValue = result.Item2 ?? "";

                // If key changed, check for duplicates
                if (!oldKey.Equals(newKey, StringComparison.OrdinalIgnoreCase))
                {
                    if (_environmentVariables.Any(ev => ev.Key.Equals(newKey, StringComparison.OrdinalIgnoreCase)))
                    {
                        var msgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Duplicate Variable",
                            $"An environment variable with key '{newKey}' already exists.",
                            ButtonEnum.Ok,
                            Icon.Warning);
                        await msgBox.ShowAsync();
                        return;
                    }

                    // Remove old key from settings
                    RemoveEnvironmentVariableFromSettings(oldKey);
                }

                // Update the variable
                selectedVar.Key = newKey;
                selectedVar.Value = newValue;

                // Refresh the DataGrid
                _tableWidget.ItemsSource = null;
                _tableWidget.ItemsSource = _environmentVariables;

                // Save to settings
                SaveEnvironmentVariable(newKey, newValue);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:244-263
        // Original: def remove_environment_variable(self):
        private async void RemoveEnvironmentVariable()
        {
            if (_tableWidget == null || _tableWidget.SelectedItem == null)
            {
                var msgBox = MessageBoxManager.GetMessageBoxStandard(
                    "Remove Variable",
                    "Please select a variable to remove.",
                    ButtonEnum.Ok,
                    Icon.Warning);
                await msgBox.ShowAsync();
                return;
            }

            var selectedVar = _tableWidget.SelectedItem as EnvironmentVariable;
            if (selectedVar == null)
            {
                return;
            }

            string key = selectedVar.Key;

            // Remove from the list
            _environmentVariables.Remove(selectedVar);
            _tableWidget.ItemsSource = null;
            _tableWidget.ItemsSource = _environmentVariables;

            // Remove from settings
            RemoveEnvironmentVariableFromSettings(key);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:238-242
        // Original: def remove_environment_variable_from_settings(self, key: str):
        private void RemoveEnvironmentVariableFromSettings(string key)
        {
            var envVars = _settings.AppEnvVariables;
            if (envVars.ContainsKey(key))
            {
                envVars.Remove(key);
                _settings.AppEnvVariables = envVars;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:135-139
        // Original: def save_environment_variable(self, key: str, value: str):
        private void SaveEnvironmentVariable(string key, string value)
        {
            var envVars = _settings.AppEnvVariables;
            envVars[key] = value;
            _settings.AppEnvVariables = envVars;
        }

        // Helper method to get the parent window for dialogs
        private Window GetParentWindow()
        {
            Control current = this;
            while (current != null)
            {
                if (current is Window window)
                {
                    return window;
                }
                current = current.Parent as Control;
            }
            return null;
        }
    }
}
