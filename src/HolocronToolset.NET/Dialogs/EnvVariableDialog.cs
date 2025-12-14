using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace HolocronToolset.NET.Dialogs
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

            // TODO: Populate with actual ENV_VARS list when available
            // For now, add a few common ones
            _nameEdit.Items.Add("QT_AUTO_SCREEN_SCALE_FACTOR");
            _nameEdit.Items.Add("QT_ENABLE_HIGHDPI_SCALING");
            _nameEdit.Items.Add("QT_SCALE_FACTOR");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:260-280
        // Original: def update_description_and_completer(self):
        private void UpdateDescriptionAndCompleter()
        {
            // TODO: Update description and completer based on selected variable when ENV_VARS list is available
            if (_descriptionEdit != null && _nameEdit != null)
            {
                _descriptionEdit.Text = $"Description for {_nameEdit.SelectedItem}";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:283-310
        // Original: def check_value_validity(self):
        private void CheckValueValidity()
        {
            // TODO: Check value validity based on variable type when ENV_VARS list is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:312-315
        // Original: def browse_directory(self):
        private void BrowseDirectory()
        {
            // TODO: Show folder browser dialog when available
            System.Console.WriteLine("Browse directory not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/env_vars.py:317-320
        // Original: def browse_file(self):
        private void BrowseFile()
        {
            // TODO: Show file browser dialog when available
            System.Console.WriteLine("Browse file not yet implemented");
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
