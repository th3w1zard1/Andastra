using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Data;

namespace HolocronToolset.NET.Widgets.Settings
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/preview_3d.py:13
    // Original: class ModelRendererSettings(Settings):
    public partial class Preview3DWidget : UserControl
    {
        private object _settings; // TODO: Use ModelRendererSettings type when available
        private CheckBox _utcShowByDefault;
        private NumericUpDown _backgroundColour;

        public Preview3DWidget()
        {
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
            // TODO: Load values from ModelRendererSettings when available
            if (_utcShowByDefault != null)
            {
                _utcShowByDefault.IsChecked = false;
            }
            if (_backgroundColour != null)
            {
                _backgroundColour.Value = 0;
            }
        }

        public void Save()
        {
            // TODO: Save values to ModelRendererSettings when available
        }
    }
}
