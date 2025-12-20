using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.Data;

namespace HolocronToolset.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/preview_3d.py:13
    // Original: class ModelRendererSettings(Settings):
    public partial class Preview3DWidget : UserControl
    {
        private ModelRendererSettings _settings;
        private CheckBox _utcShowByDefault;
        private NumericUpDown _backgroundColour;

        public Preview3DWidget()
        {
            _settings = new ModelRendererSettings();
            InitializeComponent();
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

            _utcShowByDefault = new CheckBox { Content = "UTC Show By Default" };
            _backgroundColour = new NumericUpDown { Minimum = 0, Maximum = 0xFFFFFFFF, Value = 0 };

            panel.Children.Add(_utcShowByDefault);
            panel.Children.Add(new TextBlock { Text = "Background Colour:" });
            panel.Children.Add(_backgroundColour);

            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _utcShowByDefault = this.FindControl<CheckBox>("utcShowByDefault");
            _backgroundColour = this.FindControl<NumericUpDown>("backgroundColour");
        }

        private void SetupValues()
        {
            // Matching PyKotor implementation: Load values from ModelRendererSettings
            if (_utcShowByDefault != null)
            {
                _utcShowByDefault.IsChecked = _settings.UtcShowByDefault;
            }
            if (_backgroundColour != null)
            {
                _backgroundColour.Value = _settings.BackgroundColour;
            }
        }

        // Matching PyKotor implementation: Save values from UI controls to ModelRendererSettings
        public void Save()
        {
            if (_utcShowByDefault != null)
            {
                _settings.UtcShowByDefault = _utcShowByDefault.IsChecked ?? false;
            }
            if (_backgroundColour != null)
            {
                _settings.BackgroundColour = (int)(_backgroundColour.Value ?? 0);
            }
        }
    }
}
