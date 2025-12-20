using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:69-77
    // Original: QFontDialog.getFont() - provides font selection dialog
    public partial class FontDialog : Window
    {
        private ComboBox _fontFamilyComboBox;
        private ComboBox _fontSizeComboBox;
        private CheckBox _boldCheckBox;
        private CheckBox _italicCheckBox;
        private TextBlock _previewTextBlock;
        private Button _okButton;
        private Button _cancelButton;

        public FontInfo SelectedFont { get; private set; }
        public bool DialogResult { get; private set; }

        // Public parameterless constructor for XAML
        public FontDialog() : this(null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/settings/widgets/application.py:69-77
        // Original: def select_font(self): QFontDialog.getFont(current_font, self)
        public FontDialog(Window parent)
        {
            InitializeComponent();
            Title = "Select Font";
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
            var mainPanel = new StackPanel { Margin = new Avalonia.Thickness(15), Spacing = 15 };

            // Font Family Selection
            var fontFamilyPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
            fontFamilyPanel.Children.Add(new TextBlock { Text = "Font:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, MinWidth = 80 });
            _fontFamilyComboBox = new ComboBox { MinWidth = 250 };
            PopulateFontFamilies();
            _fontFamilyComboBox.SelectionChanged += (s, e) => UpdatePreview();
            fontFamilyPanel.Children.Add(_fontFamilyComboBox);
            mainPanel.Children.Add(fontFamilyPanel);

            // Font Size Selection
            var fontSizePanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
            fontSizePanel.Children.Add(new TextBlock { Text = "Size:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, MinWidth = 80 });
            _fontSizeComboBox = new ComboBox { MinWidth = 100, IsEditable = true };
            PopulateFontSizes();
            _fontSizeComboBox.SelectionChanged += (s, e) => UpdatePreview();
            // Note: ComboBox doesn't have TextChanged in Avalonia - use observable pattern for editable ComboBox
            _fontSizeComboBox.GetObservable(ComboBox.TextProperty).Subscribe(text => UpdatePreview());
            fontSizePanel.Children.Add(_fontSizeComboBox);
            mainPanel.Children.Add(fontSizePanel);

            // Font Style Options
            var stylePanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 15 };
            _boldCheckBox = new CheckBox { Content = "Bold" };
            _boldCheckBox.Checked += (s, e) => UpdatePreview();
            _boldCheckBox.Unchecked += (s, e) => UpdatePreview();
            _italicCheckBox = new CheckBox { Content = "Italic" };
            _italicCheckBox.Checked += (s, e) => UpdatePreview();
            _italicCheckBox.Unchecked += (s, e) => UpdatePreview();
            stylePanel.Children.Add(_boldCheckBox);
            stylePanel.Children.Add(_italicCheckBox);
            mainPanel.Children.Add(stylePanel);

            // Preview
            var previewLabel = new TextBlock { Text = "Preview:", Margin = new Avalonia.Thickness(0, 10, 0, 5) };
            mainPanel.Children.Add(previewLabel);
            _previewTextBlock = new TextBlock
            {
                Text = "AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz 0123456789",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Padding = new Avalonia.Thickness(10),
                Background = new SolidColorBrush(Colors.White),
                Foreground = new SolidColorBrush(Colors.Black),
                MinHeight = 80
            };
            // Wrap TextBlock in Border to add border styling (TextBlock doesn't support BorderBrush/BorderThickness directly)
            var previewBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Avalonia.Thickness(1),
                Child = _previewTextBlock
            };
            mainPanel.Children.Add(previewBorder);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Avalonia.Thickness(0, 15, 0, 0) };
            _okButton = new Button { Content = "OK", MinWidth = 80 };
            _okButton.Click += (s, e) => OnOkClicked();
            _cancelButton = new Button { Content = "Cancel", MinWidth = 80 };
            _cancelButton.Click += (s, e) => OnCancelClicked();
            buttonPanel.Children.Add(_okButton);
            buttonPanel.Children.Add(_cancelButton);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Find controls from XAML if available
            _fontFamilyComboBox = this.FindControl<ComboBox>("fontFamilyComboBox");
            _fontSizeComboBox = this.FindControl<ComboBox>("fontSizeComboBox");
            _boldCheckBox = this.FindControl<CheckBox>("boldCheckBox");
            _italicCheckBox = this.FindControl<CheckBox>("italicCheckBox");
            _previewTextBlock = this.FindControl<TextBlock>("previewTextBlock");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_fontFamilyComboBox != null)
            {
                PopulateFontFamilies();
                _fontFamilyComboBox.SelectionChanged += (s, e) => UpdatePreview();
            }
            if (_fontSizeComboBox != null)
            {
                PopulateFontSizes();
                _fontSizeComboBox.SelectionChanged += (s, e) => UpdatePreview();
                // TODO: determine if this is 1:1 equivalent with TextChanged in avalonia, or if there's a closer equivalent.
                _fontSizeComboBox.GetObservable(ComboBox.TextProperty).Subscribe(text => UpdatePreview());
            }
            if (_boldCheckBox != null)
            {
                _boldCheckBox.Checked += (s, e) => UpdatePreview();
                _boldCheckBox.Unchecked += (s, e) => UpdatePreview();
            }
            if (_italicCheckBox != null)
            {
                _italicCheckBox.Checked += (s, e) => UpdatePreview();
                _italicCheckBox.Unchecked += (s, e) => UpdatePreview();
            }
            if (_okButton != null)
            {
                _okButton.Click += (s, e) => OnOkClicked();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => OnCancelClicked();
            }
        }

        private void PopulateFontFamilies()
        {
            if (_fontFamilyComboBox == null)
            {
                return;
            }

            _fontFamilyComboBox.Items.Clear();
            var fonts = FontManager.Current.SystemFonts
                .Select(f => f.Name)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            foreach (var fontName in fonts)
            {
                _fontFamilyComboBox.Items.Add(fontName);
            }

            // Select first font by default
            if (_fontFamilyComboBox.Items.Count > 0)
            {
                _fontFamilyComboBox.SelectedIndex = 0;
            }
        }

        private void PopulateFontSizes()
        {
            if (_fontSizeComboBox == null)
            {
                return;
            }

            _fontSizeComboBox.Items.Clear();
            int[] commonSizes = { 8, 9, 10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 48, 60, 72 };
            foreach (var size in commonSizes)
            {
                _fontSizeComboBox.Items.Add(size.ToString());
            }

            // Select 12pt by default
            _fontSizeComboBox.SelectedItem = "12";
        }

        // Matching PyKotor implementation - sets initial font from QApplication.font()
        public void SetCurrentFont(FontInfo currentFont)
        {
            if (currentFont == null)
            {
                return;
            }

            // Set font family
            if (_fontFamilyComboBox != null)
            {
                string familyName = currentFont.FamilyName ?? "Arial";
                var matchingItem = _fontFamilyComboBox.Items.Cast<object>()
                    .FirstOrDefault(item => item.ToString().Equals(familyName, StringComparison.OrdinalIgnoreCase));
                if (matchingItem != null)
                {
                    _fontFamilyComboBox.SelectedItem = matchingItem;
                }
                else if (_fontFamilyComboBox.Items.Count > 0)
                {
                    _fontFamilyComboBox.SelectedIndex = 0;
                }
            }

            // Set font size
            if (_fontSizeComboBox != null)
            {
                double fontSize = currentFont.Size > 0 ? currentFont.Size : 12;
                string fontSizeStr = ((int)fontSize).ToString();
                if (_fontSizeComboBox.Items.Contains(fontSizeStr))
                {
                    _fontSizeComboBox.SelectedItem = fontSizeStr;
                }
                else
                {
                    _fontSizeComboBox.Text = fontSizeStr;
                }
            }

            // Set bold
            if (_boldCheckBox != null)
            {
                _boldCheckBox.IsChecked = currentFont.IsBold;
            }

            // Set italic
            if (_italicCheckBox != null)
            {
                _italicCheckBox.IsChecked = currentFont.IsItalic;
            }

            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (_previewTextBlock == null)
            {
                return;
            }

            try
            {
                string familyName = _fontFamilyComboBox?.SelectedItem?.ToString() ?? "Arial";
                double fontSize = 12;

                if (_fontSizeComboBox != null)
                {
                    string sizeText = _fontSizeComboBox.SelectedItem?.ToString() ?? _fontSizeComboBox.Text ?? "12";
                    if (!double.TryParse(sizeText, out fontSize) || fontSize <= 0)
                    {
                        fontSize = 12;
                    }
                }

                var fontFamily = new FontFamily(familyName);
                var fontWeight = (_boldCheckBox?.IsChecked == true) ? FontWeight.Bold : FontWeight.Normal;
                var fontStyle = (_italicCheckBox?.IsChecked == true) ? FontStyle.Italic : FontStyle.Normal;

                _previewTextBlock.FontFamily = fontFamily;
                _previewTextBlock.FontSize = fontSize;
                _previewTextBlock.FontWeight = fontWeight;
                _previewTextBlock.FontStyle = fontStyle;
            }
            catch
            {
                // Ignore errors in preview update
            }
        }

        private void OnOkClicked()
        {
            try
            {
                string familyName = _fontFamilyComboBox?.SelectedItem?.ToString() ?? "Arial";
                double fontSize = 12;

                if (_fontSizeComboBox != null)
                {
                    string sizeText = _fontSizeComboBox.SelectedItem?.ToString() ?? _fontSizeComboBox.Text ?? "12";
                    if (!double.TryParse(sizeText, out fontSize) || fontSize <= 0)
                    {
                        fontSize = 12;
                    }
                }

                bool isBold = _boldCheckBox?.IsChecked == true;
                bool isItalic = _italicCheckBox?.IsChecked == true;

                SelectedFont = new FontInfo
                {
                    FamilyName = familyName,
                    Size = fontSize,
                    IsBold = isBold,
                    IsItalic = isItalic
                };
                DialogResult = true;
                Close();
            }
            catch
            {
                // If font creation fails, use default
                SelectedFont = new FontInfo
                {
                    FamilyName = "Arial",
                    Size = 12,
                    IsBold = false,
                    IsItalic = false
                };
                DialogResult = true;
                Close();
            }
        }

        private void OnCancelClicked()
        {
            DialogResult = false;
            Close();
        }
    }

    // Helper class to hold font information
    // Matching PyKotor QFont structure
    public class FontInfo
    {
        public string FamilyName { get; set; } = "Arial";
        public double Size { get; set; } = 12;
        public bool IsBold { get; set; } = false;
        public bool IsItalic { get; set; } = false;

        public FontFamily GetFontFamily()
        {
            return new FontFamily(FamilyName);
        }

        public FontWeight GetFontWeight()
        {
            return IsBold ? FontWeight.Bold : FontWeight.Normal;
        }

        public FontStyle GetFontStyle()
        {
            return IsItalic ? FontStyle.Italic : FontStyle.Normal;
        }
    }
}

