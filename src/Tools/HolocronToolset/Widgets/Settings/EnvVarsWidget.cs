using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace HolocronToolset.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py
    // Original: class EnvVarsWidget(QWidget):
    public partial class EnvVarsWidget : UserControl
    {
        private DataGrid _tableWidget;
        private Button _addButton;
        private Button _editButton;
        private Button _removeButton;
        private GlobalSettings _settings;
        private List<EnvironmentVariable> _environmentVariables;

        public EnvVarsWidget()
        {
            InitializeComponent();
            _settings = new GlobalSettings();
            _environmentVariables = new List<EnvironmentVariable>();
            SetupUI();
            PopulateEnvironmentVariables();
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
            var panel = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

            _tableWidget = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = true,
                CanUserResizeColumns = true,
                CanUserSortColumns = true
            };
            _tableWidget.Columns.Add(new DataGridTextColumn { Header = "Key", Binding = new Avalonia.Data.Binding("Key") });
            _tableWidget.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Avalonia.Data.Binding("Value") });

            _addButton = new Button { Content = "Add" };
            _addButton.Click += (s, e) => AddEnvironmentVariable();
            _editButton = new Button { Content = "Edit" };
            _editButton.Click += (s, e) => EditEnvironmentVariable();
            _removeButton = new Button { Content = "Remove" };
            _removeButton.Click += (s, e) => RemoveEnvironmentVariable();

            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            buttonPanel.Children.Add(_addButton);
            buttonPanel.Children.Add(_editButton);
            buttonPanel.Children.Add(_removeButton);

            panel.Children.Add(_tableWidget);
            panel.Children.Add(buttonPanel);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _tableWidget = this.FindControl<DataGrid>("tableWidget");
            _addButton = this.FindControl<Button>("addButton");
            _editButton = this.FindControl<Button>("editButton");
            _removeButton = this.FindControl<Button>("removeButton");

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
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py
        // Original: def populate_environment_variables(self):
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:189-202
        // Original: def save_environment_variable(self, key: str, value: str):
        private void SaveEnvironmentVariable(string key, string value)
        {
            var envVars = _settings.AppEnvVariables;
            envVars[key] = value;
            _settings.AppEnvVariables = envVars;
        }

        public void Save()
        {
            // Save all environment variables to settings
            var envVars = new Dictionary<string, string>();
            foreach (var envVar in _environmentVariables)
            {
                if (!string.IsNullOrWhiteSpace(envVar.Key))
                {
                    envVars[envVar.Key] = envVar.Value ?? "";
                }
            }
            _settings.AppEnvVariables = envVars;
        }

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
