using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;

namespace HolocronToolset.NET.Widgets
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
            AvaloniaXamlLoader.Load(this);
            _locstringText = this.FindControl<TextBox>("locstringText");
            _editButton = this.FindControl<Button>("editButton");

            if (_editButton != null)
            {
                _editButton.Click += (s, e) => EditLocString();
            }
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
