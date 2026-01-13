using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using BioWare.NET.Common;
using HolocronToolset.Dialogs;
using KotorColor = BioWare.NET.Common.ParsingColor;

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
            // If controls are already initialized (e.g., by SetupProgrammaticUI), skip control finding
            if (_editButton != null && _colorSpin != null && _colorLabel != null)
            {
                return;
            }

            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // Find controls from XAML
                _editButton = this.FindControl<Button>("editButton");
                _colorSpin = this.FindControl<NumericUpDown>("colorSpin");
                _colorLabel = this.FindControl<Border>("colorLabel");
            }
            catch
            {
                // XAML controls not available - create programmatic UI for tests
                SetupProgrammaticUI();
                return; // SetupProgrammaticUI already connects events
            }

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
        private async void OpenColorDialog()
        {
            // Matching PyKotor: init_color: Color = Color.from_rgba_integer(self.ui.colorSpin.value())
            KotorColor initColor = KotorColor.FromRgbaInteger((int)(_colorSpin?.Value ?? 0));

            // Matching PyKotor: init_qcolor: QColor = QColor(int(init_color.r * 255), int(init_color.g * 255), int(init_color.b * 255), int(init_color.a * 255))
            // Convert KotorColor (0.0-1.0 float) to Avalonia Color (0-255 byte)
            Avalonia.Media.Color initAvaloniaColor = Avalonia.Media.Color.FromArgb(
                (byte)(initColor.A * 255),
                (byte)(initColor.R * 255),
                (byte)(initColor.G * 255),
                (byte)(initColor.B * 255)
            );

            // Get parent window for dialog
            Window parentWindow = null;
            Control current = this;
            while (current != null)
            {
                if (current is Window window)
                {
                    parentWindow = window;
                    break;
                }
                current = current.Parent as Control;
            }

            // If no parent found, try to get main window
            if (parentWindow == null && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                parentWindow = desktop.MainWindow;
            }

            // Matching PyKotor: dialog: QColorDialog = QColorDialog(QColor(...))
            // Matching PyKotor: dialog.setOption(QColorDialog.ColorDialogOption.ShowAlphaChannel, on=self.allow_alpha)
            var dialog = new ColorPickerDialog(parentWindow, initAvaloniaColor, _allowAlpha);

            // Matching PyKotor: if dialog.exec():
            bool result = await dialog.ShowDialogAsync(parentWindow);
            if (result)
            {
                // Matching PyKotor: qcolor = dialog.selectedColor()
                Avalonia.Media.Color selectedAvaloniaColor = dialog.GetSelectedColor();

                // Matching PyKotor: color: Color = Color(qcolor.redF(), qcolor.greenF(), qcolor.blueF())
                // Create KotorColor with RGB from Avalonia Color (alpha defaults to 1.0, matching PyKotor behavior)
                KotorColor selectedColor = new KotorColor(
                    selectedAvaloniaColor.R / 255f,
                    selectedAvaloniaColor.G / 255f,
                    selectedAvaloniaColor.B / 255f
                );

                // Matching PyKotor: if self.allow_alpha:
                //     self.ui.colorSpin.setValue(color.rgb_integer() + (qcolor.alpha() << 24))
                // else:
                //     self.ui.colorSpin.setValue(color.rgb_integer())
                if (_allowAlpha)
                {
                    if (_colorSpin != null)
                    {
                        // Add alpha channel from Avalonia Color manually (matching PyKotor: qcolor.alpha() << 24)
                        _colorSpin.Value = selectedColor.ToRgbInteger() + (selectedAvaloniaColor.A << 24);
                    }
                }
                else
                {
                    if (_colorSpin != null)
                    {
                        _colorSpin.Value = selectedColor.ToRgbInteger();
                    }
                }
            }
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
