using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using HolocronToolset.Data;
using HolocronToolset.Widgets;
using HolocronToolset.Widgets.Edit;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:16
    // Original: class IndoorMapSettings(QDialog):
    public partial class IndoorMapSettingsDialog : Window
    {
        private HTInstallation _installation;
        private IndoorMap _indoorMap;
        private List<Kit> _kits;
        private LocalizedStringEdit _nameEdit;
        private ColorEdit _colorEdit;
        private TextBox _warpCodeEdit;
        private ComboBox _skyboxSelect;
        private ComboBox _gameTypeSelect;
        private Button _okButton;
        private Button _cancelButton;

        // Public parameterless constructor for XAML
        public IndoorMapSettingsDialog() : this(null, null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:17-95
        // Original: def __init__(self, parent, installation, indoor_map, kits):
        public IndoorMapSettingsDialog(Window parent, HTInstallation installation, IndoorMap indoorMap, List<Kit> kits)
        {
            InitializeComponent();
            _installation = installation;
            _indoorMap = indoorMap;
            _kits = kits ?? new List<Kit>();
            SetupUI();
            LoadIndoorMapData();
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
            Title = "Module Settings";
            Width = 285;
            Height = 200;

            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(10), Spacing = 10 };

            // Form layout
            var formPanel = new Grid();
            formPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            formPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            formPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            formPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var nameLabel = new TextBlock { Text = "Name:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _nameEdit = new LocalizedStringEdit();
            if (_installation != null)
            {
                _nameEdit.SetInstallation(_installation);
            }
            Grid.SetRow(nameLabel, 0);
            Grid.SetColumn(nameLabel, 0);
            Grid.SetRow(_nameEdit, 0);
            Grid.SetColumn(_nameEdit, 1);
            formPanel.Children.Add(nameLabel);
            formPanel.Children.Add(_nameEdit);

            var lightingLabel = new TextBlock { Text = "Lighting:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _colorEdit = new ColorEdit();
            Grid.SetRow(lightingLabel, 1);
            Grid.SetColumn(lightingLabel, 0);
            Grid.SetRow(_colorEdit, 1);
            Grid.SetColumn(_colorEdit, 1);
            formPanel.Children.Add(lightingLabel);
            formPanel.Children.Add(_colorEdit);

            var warpCodeLabel = new TextBlock { Text = "Warp Code:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _warpCodeEdit = new TextBox { MaxLength = 6 };
            Grid.SetRow(warpCodeLabel, 2);
            Grid.SetColumn(warpCodeLabel, 0);
            Grid.SetRow(_warpCodeEdit, 2);
            Grid.SetColumn(_warpCodeEdit, 1);
            formPanel.Children.Add(warpCodeLabel);
            formPanel.Children.Add(_warpCodeEdit);

            var skyboxLabel = new TextBlock { Text = "Skybox:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _skyboxSelect = new ComboBox();
            Grid.SetRow(skyboxLabel, 3);
            Grid.SetColumn(skyboxLabel, 0);
            Grid.SetRow(_skyboxSelect, 3);
            Grid.SetColumn(_skyboxSelect, 1);
            formPanel.Children.Add(skyboxLabel);
            formPanel.Children.Add(_skyboxSelect);

            var gameTypeLabel = new TextBlock { Text = "Target Game:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _gameTypeSelect = new ComboBox();
            Grid.SetRow(gameTypeLabel, 4);
            Grid.SetColumn(gameTypeLabel, 0);
            Grid.SetRow(_gameTypeSelect, 4);
            Grid.SetColumn(_gameTypeSelect, 1);
            formPanel.Children.Add(gameTypeLabel);
            formPanel.Children.Add(_gameTypeSelect);

            mainPanel.Children.Add(formPanel);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 5 };
            _okButton = new Button { Content = "OK", Width = 75 };
            _okButton.Click += (s, e) => Accept();
            _cancelButton = new Button { Content = "Cancel", Width = 75 };
            _cancelButton.Click += (s, e) => Close();
            buttonPanel.Children.Add(_okButton);
            buttonPanel.Children.Add(_cancelButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // If controls are already initialized (e.g., by SetupProgrammaticUI), skip control finding
            if (_nameEdit != null && _colorEdit != null && _warpCodeEdit != null && _skyboxSelect != null && _gameTypeSelect != null && _okButton != null && _cancelButton != null)
            {
                return;
            }

            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // Find controls from XAML
                _nameEdit = this.FindControl<LocalizedStringEdit>("nameEdit");
                _colorEdit = this.FindControl<ColorEdit>("colorEdit");
                _warpCodeEdit = this.FindControl<TextBox>("warpCodeEdit");
                _skyboxSelect = this.FindControl<ComboBox>("skyboxSelect");
                _gameTypeSelect = this.FindControl<ComboBox>("gameTypeSelect");
                _okButton = this.FindControl<Button>("okButton");
                _cancelButton = this.FindControl<Button>("cancelButton");
            }
            catch
            {
                // XAML controls not available - create programmatic UI for tests
                SetupProgrammaticUI();
                return; // SetupProgrammaticUI already connects events
            }

            if (_nameEdit != null && _installation != null)
            {
                _nameEdit.SetInstallation(_installation);
            }

            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Accept();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:63-85
        // Original: Load indoor map data into UI
        private void LoadIndoorMapData()
        {
            if (_indoorMap == null)
            {
                return;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:63-85
            // Original: self.ui.nameEdit.set_locstring(indoor_map.name)
            if (_nameEdit != null)
            {
                _nameEdit.SetLocString(_indoorMap.Name);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:64
            // Original: self.ui.colorEdit.set_color(indoor_map.lighting)
            if (_colorEdit != null)
            {
                _colorEdit.SetColor(_indoorMap.Lighting);
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:65
            // Original: self.ui.warpCodeEdit.setText(indoor_map.module_id)
            if (_warpCodeEdit != null)
            {
                _warpCodeEdit.Text = _indoorMap.ModuleId;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:68-85
            // Original: Populate skybox selector
            if (_skyboxSelect != null)
            {
                _skyboxSelect.Items.Clear();
                _skyboxSelect.Items.Add("[None]");

                // Add skyboxes from kits
                if (_kits != null)
                {
                    foreach (var kit in _kits)
                    {
                        if (kit?.Skyboxes != null)
                        {
                            foreach (var skybox in kit.Skyboxes.Keys)
                            {
                                _skyboxSelect.Items.Add(skybox);
                            }
                        }
                    }
                }

                // Set current skybox
                if (!string.IsNullOrEmpty(_indoorMap.Skybox))
                {
                    for (int i = 0; i < _skyboxSelect.Items.Count; i++)
                    {
                        var item = _skyboxSelect.Items[i];
                        if (item != null && item.ToString() == _indoorMap.Skybox)
                        {
                            _skyboxSelect.SelectedIndex = i;
                            break;
                        }
                    }
                }
                else
                {
                    _skyboxSelect.SelectedIndex = 0; // Default to [None]
                }
            }

            // Populate and set target game type selector
            if (_gameTypeSelect != null)
            {
                _gameTypeSelect.Items.Clear();
                _gameTypeSelect.Items.Add("Use Installation Default");
                _gameTypeSelect.Items.Add("Knights of the Old Republic (K1)");
                _gameTypeSelect.Items.Add("The Sith Lords (TSL/K2)");

                // Set current selection based on indoor_map.TargetGameType
                if (_indoorMap.TargetGameType == null)
                {
                    _gameTypeSelect.SelectedIndex = 0; // Use Installation Default
                }
                else if (_indoorMap.TargetGameType == true)
                {
                    _gameTypeSelect.SelectedIndex = 2; // TSL/K2
                }
                else
                {
                    _gameTypeSelect.SelectedIndex = 1; // K1
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:89-95
        // Original: def accept(self):
        private void Accept()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:92-95
            // Original: self._indoorMap.name = self.ui.nameEdit.locstring()
            if (_indoorMap != null)
            {
                if (_nameEdit != null)
                {
                    _indoorMap.Name = _nameEdit.GetLocString();
                }

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:93
                // Original: self._indoorMap.lighting = self.ui.colorEdit.color()
                if (_colorEdit != null)
                {
                    _indoorMap.Lighting = _colorEdit.GetColor();
                }

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:94
                // Original: self._indoorMap.module_id = self.ui.warpCodeEdit.text()
                if (_warpCodeEdit != null)
                {
                    _indoorMap.ModuleId = _warpCodeEdit.Text ?? "";
                }

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:95
                // Original: self._indoorMap.skybox = self.ui.skyboxSelect.currentData()
                if (_skyboxSelect != null && _skyboxSelect.SelectedIndex >= 0)
                {
                    var selectedItem = _skyboxSelect.Items[_skyboxSelect.SelectedIndex];
                    _indoorMap.Skybox = selectedItem?.ToString() ?? "";
                }

                // Get target_game_type from combo box (null = use installation default, true = TSL, false = K1)
                if (_gameTypeSelect != null && _gameTypeSelect.SelectedIndex >= 0)
                {
                    var selectedItem = _gameTypeSelect.Items[_gameTypeSelect.SelectedIndex];
                    // Items are stored with data: null, false, true for indices 0, 1, 2
                    if (_gameTypeSelect.SelectedIndex == 0)
                    {
                        _indoorMap.TargetGameType = null; // Use Installation Default
                    }
                    else if (_gameTypeSelect.SelectedIndex == 1)
                    {
                        _indoorMap.TargetGameType = false; // K1
                    }
                    else if (_gameTypeSelect.SelectedIndex == 2)
                    {
                        _indoorMap.TargetGameType = true; // TSL/K2
                    }
                }
            }

            Close();
        }
    }
}
