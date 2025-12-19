using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using HolocronToolset.Data;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:184
    // Original: class EnvVariableDialog(QDialog):
    public partial class EnvVariableDialog : Window
    {
        private ComboBox _nameEdit;
        private TextBox _valueEdit;
        private Button _browseDirButton;
        private Button _browseFileButton;
        private Button _okButton;
        private Button _cancelButton;
        private TextBlock _docLinkLabel;
        private TextBox _descriptionEdit;

        // Public parameterless constructor for XAML
        public EnvVariableDialog() : this(null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:185-258
        // Original: def __init__(self, parent: QWidget | None = None):
        public EnvVariableDialog(Window parent)
        {
            InitializeComponent();
            Title = "Edit Qt Environment Variable";
            SetupUI();
            UpdateDescriptionAndCompleter();
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

            var namePanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            namePanel.Children.Add(new TextBlock { Text = "Variable name:" });
            _nameEdit = new ComboBox { IsEditable = true, MinWidth = 300 };
            PopulateEnvVarNames();
            namePanel.Children.Add(_nameEdit);
            mainPanel.Children.Add(namePanel);

            var valuePanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            valuePanel.Children.Add(new TextBlock { Text = "Variable value:" });
            _valueEdit = new TextBox { MinWidth = 300 };
            _valueEdit.TextChanged += (s, e) => CheckValueValidity();
            valuePanel.Children.Add(_valueEdit);
            mainPanel.Children.Add(valuePanel);

            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            _browseDirButton = new Button { Content = "Browse Directory..." };
            _browseDirButton.Click += (s, e) => BrowseDirectory();
            _browseFileButton = new Button { Content = "Browse File..." };
            _browseFileButton.Click += (s, e) => BrowseFile();
            _okButton = new Button { Content = "OK" };
            _okButton.Click += (s, e) => Close();
            _cancelButton = new Button { Content = "Cancel" };
            _cancelButton.Click += (s, e) => Close();
            buttonPanel.Children.Add(_browseDirButton);
            buttonPanel.Children.Add(_browseFileButton);
            buttonPanel.Children.Add(_okButton);
            buttonPanel.Children.Add(_cancelButton);
            mainPanel.Children.Add(buttonPanel);

            _docLinkLabel = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            mainPanel.Children.Add(_docLinkLabel);

            _descriptionEdit = new TextBox { IsReadOnly = true, AcceptsReturn = true, MinHeight = 50 };
            mainPanel.Children.Add(_descriptionEdit);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _nameEdit = this.FindControl<ComboBox>("nameEdit");
            _valueEdit = this.FindControl<TextBox>("valueEdit");
            _browseDirButton = this.FindControl<Button>("browseDirButton");
            _browseFileButton = this.FindControl<Button>("browseFileButton");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");
            _docLinkLabel = this.FindControl<TextBlock>("docLinkLabel");
            _descriptionEdit = this.FindControl<TextBox>("descriptionEdit");

            if (_nameEdit != null)
            {
                PopulateEnvVarNames();
                _nameEdit.SelectionChanged += (s, e) => UpdateDescriptionAndCompleter();
                _nameEdit.TextChanged += (s, e) => UpdateDescriptionAndCompleter();
            }
            if (_browseDirButton != null)
            {
                _browseDirButton.Click += (s, e) => BrowseDirectory();
            }
            if (_browseFileButton != null)
            {
                _browseFileButton.Click += (s, e) => BrowseFile();
            }
            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Close();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
            if (_valueEdit != null)
            {
                _valueEdit.TextChanged += (s, e) => CheckValueValidity();
            }
        }

        private void PopulateEnvVarNames()
        {
            if (_nameEdit == null)
            {
                return;
            }

            _nameEdit.Items.Clear();
            foreach (var envVar in EnvVar.ENV_VARS)
            {
                _nameEdit.Items.Add(envVar.Name);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:260-280
        // Original: def update_description_and_completer(self):
        private void UpdateDescriptionAndCompleter()
        {
            if (_nameEdit == null)
            {
                return;
            }

            string selectedName = _nameEdit.SelectedItem?.ToString() ?? _nameEdit.Text;
            if (string.IsNullOrEmpty(selectedName))
            {
                if (_descriptionEdit != null)
                {
                    _descriptionEdit.Text = "";
                }
                if (_docLinkLabel != null)
                {
                    _docLinkLabel.Text = "";
                }
                return;
            }

            // Find the matching environment variable
            var currentVar = EnvVar.ENV_VARS.FirstOrDefault(
                var => var.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase)
            );

            if (currentVar != null)
            {
                // Update description
                if (_descriptionEdit != null)
                {
                    _descriptionEdit.Text = currentVar.Description;
                }

                // Update documentation link
                if (_docLinkLabel != null)
                {
                    _docLinkLabel.Text = $"Documentation: {currentVar.DocLink}";
                }

                // Note: Avalonia doesn't have a built-in AutoCompleteBox like Qt's QCompleter
                // The value field will remain a TextBox, but we can show possible values in tooltip or description
                // For now, we'll rely on the validation to guide users
            }
            else
            {
                // Custom variable name entered
                if (_descriptionEdit != null)
                {
                    _descriptionEdit.Text = "Custom environment variable (not in predefined list)";
                }
                if (_docLinkLabel != null)
                {
                    _docLinkLabel.Text = "";
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:283-310
        // Original: def check_value_validity(self):
        private void CheckValueValidity()
        {
            if (_valueEdit == null || _nameEdit == null)
            {
                return;
            }

            string selectedName = _nameEdit.SelectedItem?.ToString() ?? _nameEdit.Text;
            if (string.IsNullOrEmpty(selectedName))
            {
                _valueEdit.BorderBrush = null;
                return;
            }

            // Find the matching environment variable
            var currentVar = EnvVar.ENV_VARS.FirstOrDefault(
                var => var.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase)
            );

            if (currentVar == null || string.IsNullOrEmpty(currentVar.PossibleValues))
            {
                // No validation rules or custom variable
                _valueEdit.BorderBrush = null;
                return;
            }

            string value = _valueEdit.Text ?? "";
            bool isValid = false;

            // Check validation based on possible values
            if (currentVar.PossibleValues == "Any positive integer")
            {
                if (int.TryParse(value, out int intValue))
                {
                    isValid = intValue > 0;
                }
            }
            else if (currentVar.PossibleValues == "Any positive floating-point value")
            {
                if (double.TryParse(value, out double doubleValue))
                {
                    isValid = doubleValue > 0;
                }
            }
            else if (currentVar.PossibleValues == "Any valid directory path" || 
                     currentVar.PossibleValues == "Any valid file path" ||
                     currentVar.PossibleValues == "System path" ||
                     currentVar.PossibleValues == "Any valid size" ||
                     currentVar.PossibleValues == "Any valid theme name" ||
                     currentVar.PossibleValues == "Any valid renderer name" ||
                     currentVar.PossibleValues == "Any valid style name" ||
                     currentVar.PossibleValues == "Any valid profile name" ||
                     currentVar.PossibleValues == "Any valid layout identifier" ||
                     currentVar.PossibleValues == "Any valid server path" ||
                     currentVar.PossibleValues == "Any valid framebuffer device" ||
                     currentVar.PossibleValues == "Category filter string" ||
                     currentVar.PossibleValues == "proxy settings" ||
                     currentVar.PossibleValues == "Any valid Chromium flag" ||
                     currentVar.PossibleValues == "Comma-separated list of scale factors" ||
                     currentVar.PossibleValues == "Comma-separated list of plugins")
            {
                // These are free-form values, so always valid (non-empty check only)
                isValid = !string.IsNullOrWhiteSpace(value) || value == "None";
            }
            else
            {
                // Check if value is in the list of possible values
                var possibleValues = currentVar.PossibleValues.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries);
                isValid = possibleValues.Contains(value, StringComparer.OrdinalIgnoreCase) || 
                         value == "None" || 
                         string.IsNullOrWhiteSpace(value);
            }

            // Set border color to indicate validity
            if (isValid || string.IsNullOrWhiteSpace(value))
            {
                _valueEdit.BorderBrush = null;
            }
            else
            {
                _valueEdit.BorderBrush = new SolidColorBrush(Colors.Red);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:312-315
        // Original: def browse_directory(self):
        private async void BrowseDirectory()
        {
            if (_valueEdit == null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            // Get initial directory from current value if it's a valid path
            string initialDirectory = _valueEdit.Text;
            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                // Use the current value as initial directory
            }
            else if (!string.IsNullOrEmpty(initialDirectory) && File.Exists(initialDirectory))
            {
                // If it's a file path, use its directory
                initialDirectory = Path.GetDirectoryName(initialDirectory);
            }
            else
            {
                // Use current working directory or user's home
                initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            try
            {
                var options = new FolderPickerOpenOptions
                {
                    Title = "Select Directory",
                    AllowMultiple = false
                };

                // Set initial directory if available
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    var storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
                    if (storageFolder != null)
                    {
                        options.SuggestedStartLocation = storageFolder;
                    }
                }

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
                if (folders != null && folders.Count > 0)
                {
                    _valueEdit.Text = folders[0].Path.LocalPath;
                    CheckValueValidity();
                }
            }
            catch
            {
                // Ignore errors - user may have cancelled
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:317-320
        // Original: def browse_file(self):
        private async void BrowseFile()
        {
            if (_valueEdit == null)
            {
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            // Get initial directory from current value if it's a valid path
            string initialDirectory = _valueEdit.Text;
            if (!string.IsNullOrEmpty(initialDirectory) && File.Exists(initialDirectory))
            {
                // Use the directory of the current file
                initialDirectory = Path.GetDirectoryName(initialDirectory);
            }
            else if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                // If it's already a directory, use it
            }
            else
            {
                // Use current working directory or user's home
                initialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }

            try
            {
                var options = new FilePickerOpenOptions
                {
                    Title = "Select File",
                    AllowMultiple = false
                };

                // Set initial directory if available
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                {
                    var storageFolder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDirectory);
                    if (storageFolder != null)
                    {
                        options.SuggestedStartLocation = storageFolder;
                    }
                }

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (files != null && files.Count > 0)
                {
                    _valueEdit.Text = files[0].Path.LocalPath;
                    CheckValueValidity();
                }
            }
            catch
            {
                // Ignore errors - user may have cancelled
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:322-323
        // Original: def get_data(self) -> tuple[str, str]:
        public Tuple<string, string> GetData()
        {
            string name = _nameEdit?.SelectedItem?.ToString() ?? "";
            string value = _valueEdit?.Text ?? "";
            return Tuple.Create(name, value);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:325-327
        // Original: def set_data(self, name: str, value: str):
        public void SetData(string name, string value)
        {
            if (_nameEdit != null)
            {
                _nameEdit.SelectedItem = name;
            }
            if (_valueEdit != null)
            {
                _valueEdit.Text = value;
            }
        }
    }
}
