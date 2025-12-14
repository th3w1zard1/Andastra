using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:30
    // Original: class CloneModuleDialog(QDialog):
    public partial class CloneModuleDialog : Window
    {
        private HTInstallation _active;
        private Dictionary<string, HTInstallation> _installations;
        private string _selectedModuleRoot;
        private string _identifier;
        private string _prefix;
        private string _name;
        private bool _copyTextures;
        private bool _copyLightmaps;
        private bool _keepDoors;
        private bool _keepPlaceables;
        private bool _keepSounds;
        private bool _keepPathing;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:31-83
        // Original: def __init__(self, parent, active, installations):
        public CloneModuleDialog(
            Window parent = null,
            HTInstallation active = null,
            Dictionary<string, HTInstallation> installations = null)
        {
            InitializeComponent();
            _active = active;
            _installations = installations ?? new Dictionary<string, HTInstallation>();
            SetupUI();
            LoadModules();
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
            Title = "Clone Module";
            Width = 500;
            Height = 500;

            var panel = new StackPanel();
            var titleLabel = new TextBlock
            {
                Text = "Clone Module",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            var createButton = new Button { Content = "Create" };
            createButton.Click += (sender, e) => Ok();
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (sender, e) => Close();

            panel.Children.Add(titleLabel);
            panel.Children.Add(createButton);
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        private ComboBox _moduleSelect;
        private TextBox _moduleRootEdit;
        private TextBox _nameEdit;
        private TextBox _filenameEdit;
        private TextBox _prefixEdit;
        private CheckBox _keepDoorsCheckbox;
        private CheckBox _keepPlaceablesCheckbox;
        private CheckBox _keepSoundsCheckbox;
        private CheckBox _keepPathingCheckbox;
        private CheckBox _copyTexturesCheckbox;
        private CheckBox _copyLightmapsCheckbox;
        private Button _createButton;
        private Button _cancelButton;

        private void SetupUI()
        {
            // Find controls from XAML
            _moduleSelect = this.FindControl<ComboBox>("moduleSelect");
            _moduleRootEdit = this.FindControl<TextBox>("moduleRootEdit");
            _nameEdit = this.FindControl<TextBox>("nameEdit");
            _filenameEdit = this.FindControl<TextBox>("filenameEdit");
            _prefixEdit = this.FindControl<TextBox>("prefixEdit");
            _keepDoorsCheckbox = this.FindControl<CheckBox>("keepDoorsCheckbox");
            _keepPlaceablesCheckbox = this.FindControl<CheckBox>("keepPlaceablesCheckbox");
            _keepSoundsCheckbox = this.FindControl<CheckBox>("keepSoundsCheckbox");
            _keepPathingCheckbox = this.FindControl<CheckBox>("keepPathingCheckbox");
            _copyTexturesCheckbox = this.FindControl<CheckBox>("copyTexturesCheckbox");
            _copyLightmapsCheckbox = this.FindControl<CheckBox>("copyLightmapsCheckbox");
            _createButton = this.FindControl<Button>("createButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_createButton != null)
            {
                _createButton.Click += (s, e) => Ok();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
            if (_moduleSelect != null)
            {
                _moduleSelect.SelectionChanged += (s, e) => OnModuleSelectionChanged();
            }
        }

        private void OnModuleSelectionChanged()
        {
            if (_moduleSelect?.SelectedItem != null)
            {
                // Update module root edit when selection changes
                // This will be implemented when module data is available
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:150-174
        // Original: def load_modules(self):
        private void LoadModules()
        {
            var options = new Dictionary<string, ModuleOption>();
            foreach (var installation in _installations.Values)
            {
                var moduleNames = installation.ModuleNames();
                foreach (var kvp in moduleNames)
                {
                    string filename = kvp.Key;
                    string name = kvp.Value;
                    string root = System.IO.Path.GetFileNameWithoutExtension(filename);
                    if (!options.ContainsKey(root))
                    {
                        options[root] = new ModuleOption(name, root, new List<string>(), installation);
                    }
                    options[root].Files.Add(filename);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:85-148
        // Original: def ok(self):
        private void Ok()
        {
            // Clone module - will be implemented when module cloning is available
            System.Console.WriteLine("Module cloning not yet fully implemented");
            Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:23-27
        // Original: class ModuleOption(NamedTuple):
        public class ModuleOption
        {
            public string Name { get; set; }
            public string Root { get; set; }
            public List<string> Files { get; set; }
            public HTInstallation Installation { get; set; }

            public ModuleOption(string name, string root, List<string> files, HTInstallation installation)
            {
                Name = name;
                Root = root;
                Files = files;
                Installation = installation;
            }
        }
    }
}
