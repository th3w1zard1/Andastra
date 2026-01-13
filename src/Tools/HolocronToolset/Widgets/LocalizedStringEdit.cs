using BioWare.NET.Common;
using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;

namespace HolocronToolset.Widgets
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/locstring.py
    // Original: class LocalizedStringLineEdit(QWidget):
    public partial class LocalizedStringEdit : UserControl
    {
        private TextBox _locstringText;
        private Button _editButton;
        private HTInstallation _installation;
        private LocalizedString _locstring;

        public LocalizedStringEdit()
        {
            InitializeComponent();
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

            if (xamlLoaded)
            {
                try
                {
                    _locstringText = this.FindControl<TextBox>("locstringText");
                    _editButton = this.FindControl<Button>("editButton");
                }
                catch
                {
                    // Controls not found - create programmatic UI
                    SetupProgrammaticUI();
                    return;
                }
            }
            else
            {
                SetupProgrammaticUI();
                return;
            }

            if (_editButton != null)
            {
                _editButton.Click += (s, e) => EditLocString();
            }
        }

        private void SetupProgrammaticUI()
        {
            // Create UI controls programmatically for test scenarios
            _locstringText = new TextBox { IsReadOnly = true, Watermark = "Localized String" };
            _editButton = new Button { Content = "Edit" };
            _editButton.Click += (s, e) => EditLocString();

            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            panel.Children.Add(_locstringText);
            panel.Children.Add(_editButton);
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/locstring.py
        // Original: def set_installation(self, installation):
        public void SetInstallation(HTInstallation installation)
        {
            _installation = installation;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/locstring.py
        // Original: def set_locstring(self, locstring):
        public void SetLocString(LocalizedString locstring)
        {
            _locstring = locstring ?? LocalizedString.FromInvalid();
            UpdateText();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/widgets/edit/locstring.py
        // Original: def locstring(self) -> LocalizedString:
        public LocalizedString GetLocString()
        {
            return _locstring ?? LocalizedString.FromInvalid();
        }

        private void UpdateText()
        {
            if (_locstringText == null || _locstring == null)
            {
                return;
            }

            if (_locstring.StringRef == -1)
            {
                _locstringText.Text = _locstring.ToString();
            }
            else if (_installation != null)
            {
                _locstringText.Text = _installation.String(_locstring);
            }
            else
            {
                _locstringText.Text = $"StringRef: {_locstring.StringRef}";
            }
        }

        private async void EditLocString()
        {
            if (_installation == null)
            {
                return;
            }

            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            var dialog = new LocalizedStringDialog(parentWindow, _installation, _locstring);
            var result = await dialog.ShowDialog<bool>(parentWindow);
            if (result)
            {
                _locstring = dialog.LocString;
                UpdateText();
            }
        }
    }
}
