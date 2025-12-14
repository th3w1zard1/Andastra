using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using HolocronToolset.NET.Config;

namespace HolocronToolset.NET.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/about.py:15
    // Original: class About(QDialog):
    public partial class AboutDialog : Window
    {
        private TextBlock _aboutLabel;
        private Button _closeButton;
        private Image _image;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/about.py:16-55
        // Original: def __init__(self, parent):
        public AboutDialog() : this(null)
        {
        }

        public AboutDialog(Window parent)
        {
            InitializeComponent();
            SetupUI();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _aboutLabel = this.FindControl<TextBlock>("aboutLabel");
            _closeButton = this.FindControl<Button>("closeButton");
            _image = this.FindControl<Image>("image");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/about.py:50-52
        // Original: self.ui.aboutLabel.setText(self.ui.aboutLabel.text().replace("X.X.X", LOCAL_PROGRAM_INFO["currentVersion"]))
        private void SetupUI()
        {
            if (_closeButton != null)
            {
                _closeButton.Click += (sender, e) => Close();
            }

            if (_aboutLabel != null)
            {
                // Replace version placeholder with actual version
                string text = _aboutLabel.Text ?? "";
                text = text.Replace("X.X.X", ConfigInfo.CurrentVersion);
                _aboutLabel.Text = text;
            }

            // TODO: Load icon image when resources are available
            // _image.Source = new Bitmap("path/to/sith.png");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/about.py:54-55
        // Original: def showEvent(self, event: QShowEvent): self.setFixedSize(self.size())
        protected override void OnOpened(EventArgs e)
        {
            base.OnOpened(e);
            // Set fixed size when shown
            CanResize = false;
        }
    }
}
