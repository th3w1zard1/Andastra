using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;

namespace HolocronToolset.Dialogs
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

        // Public parameterless constructor for XAML
        public CloneModuleDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:31-83
        // Original: def __init__(self, parent, active, installations):
        public CloneModuleDialog(
            Window parent,
            HTInstallation active,
            Dictionary<string, HTInstallation> installations)
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

            // Create all UI controls programmatically for test scenarios
            _moduleSelect = new ComboBox();
            _moduleRootEdit = new TextBox { IsReadOnly = true, Watermark = "Module Root" };
            _nameEdit = new TextBox { Watermark = "Name" };
            _filenameEdit = new TextBox { Watermark = "Filename" };
            _prefixEdit = new TextBox { Watermark = "Prefix", MaxLength = 3 };
            _keepDoorsCheckbox = new CheckBox { Content = "Keep Doors" };
            _keepPlaceablesCheckbox = new CheckBox { Content = "Keep Placeables" };
            _keepSoundsCheckbox = new CheckBox { Content = "Keep Sounds" };
            _keepPathingCheckbox = new CheckBox { Content = "Keep Pathing" };
            _copyTexturesCheckbox = new CheckBox { Content = "Copy Textures" };
            _copyLightmapsCheckbox = new CheckBox { Content = "Copy Lightmaps" };
            _createButton = new Button { Content = "Create" };
            _cancelButton = new Button { Content = "Cancel" };

            // Connect events
            _createButton.Click += (s, e) => Ok();
            _cancelButton.Click += (s, e) => Close();
            _moduleSelect.SelectionChanged += (s, e) => OnModuleSelectionChanged();
            _filenameEdit.TextChanged += (s, e) => SetPrefixFromFilename();

            // Create UI wrapper for testing
            Ui = new CloneModuleDialogUi
            {
                ModuleSelect = _moduleSelect,
                ModuleRootEdit = _moduleRootEdit,
                NameEdit = _nameEdit,
                FilenameEdit = _filenameEdit,
                PrefixEdit = _prefixEdit,
                KeepDoorsCheckbox = _keepDoorsCheckbox,
                KeepPlaceablesCheckbox = _keepPlaceablesCheckbox,
                KeepSoundsCheckbox = _keepSoundsCheckbox,
                KeepPathingCheckbox = _keepPathingCheckbox,
                CopyTexturesCheckbox = _copyTexturesCheckbox,
                CopyLightmapsCheckbox = _copyLightmapsCheckbox,
                CreateButton = _createButton,
                CancelButton = _cancelButton
            };

            var panel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };
            var titleLabel = new TextBlock
            {
                Text = "Clone Module",
                FontSize = 18,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            panel.Children.Add(titleLabel);
            panel.Children.Add(new TextBlock { Text = "Module:" });
            panel.Children.Add(_moduleSelect);
            panel.Children.Add(new TextBlock { Text = "Module Root:" });
            panel.Children.Add(_moduleRootEdit);
            panel.Children.Add(new TextBlock { Text = "Name:" });
            panel.Children.Add(_nameEdit);
            panel.Children.Add(new TextBlock { Text = "Filename:" });
            panel.Children.Add(_filenameEdit);
            panel.Children.Add(new TextBlock { Text = "Prefix:" });
            panel.Children.Add(_prefixEdit);
            panel.Children.Add(_keepDoorsCheckbox);
            panel.Children.Add(_keepPlaceablesCheckbox);
            panel.Children.Add(_keepSoundsCheckbox);
            panel.Children.Add(_keepPathingCheckbox);
            panel.Children.Add(_copyTexturesCheckbox);
            panel.Children.Add(_copyLightmapsCheckbox);
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            buttonPanel.Children.Add(_createButton);
            buttonPanel.Children.Add(_cancelButton);
            panel.Children.Add(buttonPanel);
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:66-68
        // Original: self.ui = Ui_Dialog()
        // Expose UI widgets for testing
        public CloneModuleDialogUi Ui { get; private set; }

        private void SetupUI()
        {
            // If Ui is already initialized (e.g., by SetupProgrammaticUI), skip control finding
            if (Ui != null)
            {
                return;
            }

            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            Ui = new CloneModuleDialogUi();
            
            try
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
            }
            catch
            {
                // XAML controls not available - create programmatic UI for tests
                SetupProgrammaticUI();
                return; // SetupProgrammaticUI already sets up Ui and connects events
            }

            // Create UI wrapper for testing
            Ui.ModuleSelect = _moduleSelect;
            Ui.ModuleRootEdit = _moduleRootEdit;
            Ui.NameEdit = _nameEdit;
            Ui.FilenameEdit = _filenameEdit;
            Ui.PrefixEdit = _prefixEdit;
            Ui.KeepDoorsCheckbox = _keepDoorsCheckbox;
            Ui.KeepPlaceablesCheckbox = _keepPlaceablesCheckbox;
            Ui.KeepSoundsCheckbox = _keepSoundsCheckbox;
            Ui.KeepPathingCheckbox = _keepPathingCheckbox;
            Ui.CopyTexturesCheckbox = _copyTexturesCheckbox;
            Ui.CopyLightmapsCheckbox = _copyLightmapsCheckbox;
            Ui.CreateButton = _createButton;
            Ui.CancelButton = _cancelButton;

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
            if (_filenameEdit != null && _prefixEdit != null)
            {
                _filenameEdit.TextChanged += (s, e) => SetPrefixFromFilename();
                // Set initial prefix if filename already has text
                if (!string.IsNullOrEmpty(_filenameEdit.Text))
                {
                    SetPrefixFromFilename();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:176-181
        // Original: def changed_module(self, index: int):
        private void OnModuleSelectionChanged()
        {
            if (_moduleSelect?.SelectedItem is ModuleOption option)
            {
                _moduleRootEdit.Text = option.Root;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:183-184
        // Original: def set_prefix_from_filename(self): self.ui.prefixEdit.setText(self.ui.filenameEdit.text().upper()[:3])
        private void SetPrefixFromFilename()
        {
            if (_filenameEdit == null || _prefixEdit == null)
            {
                return;
            }

            string filename = _filenameEdit.Text ?? "";
            // Generate prefix: take first 3 characters, convert to uppercase
            // Matching Python: filename.upper()[:3]
            string prefix = "";
            if (filename.Length > 0)
            {
                // Take up to 3 characters, convert to uppercase
                int length = Math.Min(3, filename.Length);
                prefix = filename.Substring(0, length).ToUpperInvariant();
            }
            _prefixEdit.Text = prefix;
        }

        // Public method for testing to manually trigger prefix update
        public void UpdatePrefixFromFilename()
        {
            SetPrefixFromFilename();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:150-174
        // Original: def load_modules(self):
        private void LoadModules()
        {
            if (_moduleSelect == null)
            {
                return;
            }

            var options = new Dictionary<string, ModuleOption>();
            foreach (var installation in _installations.Values)
            {
                var moduleNames = installation.ModuleNames();
                foreach (var kvp in moduleNames)
                {
                    string filename = kvp.Key;
                    string name = kvp.Value ?? "";
                    // Matching PyKotor: Module.filepath_to_root(filename)
                    string root = Andastra.Parsing.Installation.Installation.GetModuleRoot(filename);
                    if (!options.ContainsKey(root))
                    {
                        options[root] = new ModuleOption(name, root, new List<string>(), installation);
                    }
                    options[root].Files.Add(filename);
                }
            }

            // Add options to ComboBox
            foreach (var option in options.Values)
            {
                _moduleSelect.Items.Add(option);
            }

            // Set first item as selected if available
            if (_moduleSelect.Items.Count > 0)
            {
                _moduleSelect.SelectedIndex = 0;
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
                Files = files ?? new List<string>();
                Installation = installation;
            }

            public override string ToString()
            {
                return Name;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/clone_module.py:66-68
        // Original: self.ui = Ui_Dialog()
        // UI wrapper class for testing access
        public class CloneModuleDialogUi
        {
            public ComboBox ModuleSelect { get; set; }
            public TextBox ModuleRootEdit { get; set; }
            public TextBox NameEdit { get; set; }
            public TextBox FilenameEdit { get; set; }
            public TextBox PrefixEdit { get; set; }
            public CheckBox KeepDoorsCheckbox { get; set; }
            public CheckBox KeepPlaceablesCheckbox { get; set; }
            public CheckBox KeepSoundsCheckbox { get; set; }
            public CheckBox KeepPathingCheckbox { get; set; }
            public CheckBox CopyTexturesCheckbox { get; set; }
            public CheckBox CopyLightmapsCheckbox { get; set; }
            public Button CreateButton { get; set; }
            public Button CancelButton { get; set; }
        }
    }
}
