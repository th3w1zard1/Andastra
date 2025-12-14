using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Widgets;
using HolocronToolset.NET.Widgets.Edit;

namespace HolocronToolset.NET.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:16
    // Original: class IndoorMapSettings(QDialog):
    public partial class IndoorMapSettingsDialog : Window
    {
        private HTInstallation _installation;
        private object _indoorMap; // TODO: Use IndoorMap type when available
        private List<object> _kits; // TODO: Use List<Kit> when available
        private LocalizedStringEdit _nameEdit;
        private ColorEdit _colorEdit;
        private TextBox _warpCodeEdit;
        private ComboBox _skyboxSelect;
        private Button _okButton;
        private Button _cancelButton;

        // Public parameterless constructor for XAML
        public IndoorMapSettingsDialog() : this(null, null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:17-95
        // Original: def __init__(self, parent, installation, indoor_map, kits):
        public IndoorMapSettingsDialog(Window parent, HTInstallation installation, object indoorMap, List<object> kits)
        {
            InitializeComponent();
            _installation = installation;
            _indoorMap = indoorMap;
            _kits = kits ?? new List<object>();
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
            
            var nameLabel = new TextBlock { Text = "Name:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _nameEdit = new LocalizedStringEdit();
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
            // Find controls from XAML
            _nameEdit = this.FindControl<LocalizedStringEdit>("nameEdit");
            _colorEdit = this.FindControl<ColorEdit>("colorEdit");
            _warpCodeEdit = this.FindControl<TextBox>("warpCodeEdit");
            _skyboxSelect = this.FindControl<ComboBox>("skyboxSelect");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_nameEdit != null && _installation != null)
            {
                // TODO: Set installation when LocalizedStringEdit.SetInstallation is available
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

            // TODO: Load data when IndoorMap type is available
            // For now, just populate skybox selector
            if (_skyboxSelect != null)
            {
                _skyboxSelect.Items.Clear();
                _skyboxSelect.Items.Add("[None]");

                // Add skyboxes from kits
                // TODO: Implement when Kit type is available
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/indoor_settings.py:89-95
        // Original: def accept(self):
        private void Accept()
        {
            // TODO: Save data when IndoorMap type is available
            // if (_indoorMap != null)
            // {
            //     _indoorMap.Name = _nameEdit?.GetLocString();
            //     _indoorMap.Lighting = _colorEdit?.GetColor();
            //     _indoorMap.ModuleId = _warpCodeEdit?.Text ?? "";
            //     _indoorMap.Skybox = _skyboxSelect?.SelectedItem?.ToString() ?? "";
            // }

            Close();
        }
    }
}
