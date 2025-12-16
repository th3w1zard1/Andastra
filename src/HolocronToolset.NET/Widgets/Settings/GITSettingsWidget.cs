using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Widgets.Edit;
using AuroraEngine.Common.Common;
using KotorColor = AuroraEngine.Common.Common.Color;

namespace HolocronToolset.NET.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/git.py:15
    // Original: class GITWidget(SettingsWidget):
    public partial class GITSettingsWidget : UserControl
    {
        private GITSettings _settings;
        private Button _coloursResetButton;
        private Button _controlsResetButton;

        public GITSettingsWidget()
        {
            InitializeComponent();
            _settings = new GITSettings();
            SetupUI();
            SetupValues();
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

            _coloursResetButton = new Button { Content = "Reset Colours" };
            _coloursResetButton.Click += (s, e) => ResetColours();
            _controlsResetButton = new Button { Content = "Reset Controls" };
            _controlsResetButton.Click += (s, e) => ResetControls();

            panel.Children.Add(_coloursResetButton);
            panel.Children.Add(_controlsResetButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _coloursResetButton = this.FindControl<Button>("coloursResetButton");
            _controlsResetButton = this.FindControl<Button>("controlsResetButton");

            if (_coloursResetButton != null)
            {
                _coloursResetButton.Click += (s, e) => ResetColours();
            }
            if (_controlsResetButton != null)
            {
                _controlsResetButton.Click += (s, e) => ResetControls();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/git.py:58-60
        // Original: def _setupColourValues(self):
        private void SetupColourValues()
        {
            // TODO: Setup colour values when GITSettings and ColorEdit widgets are available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/git.py:62-64
        // Original: def _setupBindValues(self):
        private void SetupBindValues()
        {
            // TODO: Setup bind values when GITSettings and SetBindWidget are available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/git.py:66-68
        // Original: def setup_values(self):
        private void SetupValues()
        {
            SetupColourValues();
            SetupBindValues();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/git.py:70-72
        // Original: def resetColours(self):
        private void ResetColours()
        {
            if (_settings != null)
            {
                _settings.ResetMaterialColors();
                SetupColourValues();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/git.py:74-76
        // Original: def resetControls(self):
        private void ResetControls()
        {
            if (_settings != null)
            {
                _settings.ResetControls();
                SetupBindValues();
            }
        }

        public void Save()
        {
            // Settings are saved automatically via property setters
            if (_settings != null)
            {
                _settings.Save();
            }
        }
    }
}
