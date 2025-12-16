using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Andastra.Parsing;
using KotorColor = Andastra.Parsing.Color;

namespace HolocronToolset.Widgets.Edit
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/color.py:9
    // Original: class ColorEdit(QWidget):
    public partial class ColorEdit : UserControl
    {
        private KotorColor _color;
        private bool _allowAlpha;
        private Button _editButton;
        private NumericUpDown _colorSpin;
        private Border _colorLabel;

        // Public parameterless constructor for XAML
        public ColorEdit() : this(null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/color.py:10-22
        // Original: def __init__(self, parent: QWidget):
        public ColorEdit(Control parent)
        {
            InitializeComponent();
            _color = new KotorColor(1.0f, 1.0f, 1.0f, 0.0f);
            _allowAlpha = false;
            SetupUI();
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
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            _colorLabel = new Border { Width = 16, Height = 16, Background = new SolidColorBrush(Avalonia.Media.Color.FromRgb(255, 255, 255)) };
            _colorSpin = new NumericUpDown { Minimum = 0, Maximum = 0xFFFFFFFF, Width = 100 };
            _editButton = new Button { Content = "Edit" };
            _editButton.Click += (s, e) => OpenColorDialog();
            _colorSpin.ValueChanged += (s, e) => OnColorChange((int)(_colorSpin.Value ?? 0));
            panel.Children.Add(_colorLabel);
            panel.Children.Add(_colorSpin);
            panel.Children.Add(_editButton);
            Content = panel;
        }

        private void SetupUI()
        {
            // Find controls from XAML
            _editButton = this.FindControl<Button>("editButton");
            _colorSpin = this.FindControl<NumericUpDown>("colorSpin");
            _colorLabel = this.FindControl<Border>("colorLabel");

            if (_editButton != null)
            {
                _editButton.Click += (s, e) => OpenColorDialog();
            }
            if (_colorSpin != null)
            {
                _colorSpin.ValueChanged += (s, e) => OnColorChange((int)(_colorSpin.Value ?? 0));
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/color.py:23-37
        // Original: def open_color_dialog(self):
        private void OpenColorDialog()
        {
            // TODO: Implement color dialog when Avalonia color picker is available
            // For now, just update the color
            System.Console.WriteLine("Color dialog not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/color.py:39-50
        // Original: def _on_color_change(self, value: int):
        private void OnColorChange(int value)
        {
            _color = KotorColor.FromRgbaInteger(value);
            if (!_allowAlpha)
            {
                _color.A = 0.0f;
            }

            if (_colorLabel != null)
            {
                var avaloniaColor = Avalonia.Media.Color.FromRgb(
                    (byte)(_color.R * 255),
                    (byte)(_color.G * 255),
                    (byte)(_color.B * 255)
                );
                _colorLabel.Background = new SolidColorBrush(avaloniaColor);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/color.py:52-54
        // Original: def set_color(self, color: Color):
        public void SetColor(KotorColor color)
        {
            _color = color;
            if (_colorSpin != null)
            {
                _colorSpin.Value = _allowAlpha ? color.ToRgbaInteger() : color.ToRgbInteger();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/color.py:56-57
        // Original: def color(self) -> Color:
        public KotorColor GetColor()
        {
            return _color;
        }

        public bool AllowAlpha
        {
            get => _allowAlpha;
            set => _allowAlpha = value;
        }
    }
}
