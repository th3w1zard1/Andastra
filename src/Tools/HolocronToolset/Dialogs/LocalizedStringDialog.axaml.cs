using Andastra.Parsing.Common;
using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Andastra.Parsing;
using HolocronToolset.Data;

namespace HolocronToolset.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:20
    // Original: class LocalizedStringDialog(QDialog):
    public partial class LocalizedStringDialog : Window
    {
        private HTInstallation _installation;
        public LocalizedString LocString { get; private set; }

        // Public parameterless constructor for XAML
        public LocalizedStringDialog() : this(null, null, null)
        {
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:21-107
        // Original: def __init__(self, parent, installation, locstring):
        public LocalizedStringDialog(Window parent, HTInstallation installation, LocalizedString locstring)
        {
            InitializeComponent();
            _installation = installation;
            LocString = locstring ?? LocalizedString.FromInvalid();
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
            Title = "Localized String Editor";
            Width = 500;
            Height = 400;

            var panel = new StackPanel();
            var stringrefLabel = new TextBlock { Text = "StringRef:" };
            var stringrefSpin = new NumericUpDown { Minimum = -1, Maximum = 999999 };
            var stringEdit = new TextBox { AcceptsReturn = true, Watermark = "Text" };
            var okButton = new Button { Content = "OK" };
            okButton.Click += (s, e) => { LocString = LocString ?? LocalizedString.FromInvalid(); Close(); };
            var cancelButton = new Button { Content = "Cancel" };
            cancelButton.Click += (s, e) => Close();

            panel.Children.Add(stringrefLabel);
            panel.Children.Add(stringrefSpin);
            panel.Children.Add(stringEdit);
            panel.Children.Add(okButton);
            panel.Children.Add(cancelButton);
            Content = panel;
        }

        private NumericUpDown _stringrefSpin;
        private Button _stringrefNewButton;
        private Button _stringrefNoneButton;
        private ComboBox _languageSelect;
        private RadioButton _maleRadio;
        private RadioButton _femaleRadio;
        private TextBox _stringEdit;
        private Button _okButton;
        private Button _cancelButton;

        private void SetupUI()
        {
            // Find controls from XAML
            _stringrefSpin = this.FindControl<NumericUpDown>("stringrefSpin");
            _stringrefNewButton = this.FindControl<Button>("stringrefNewButton");
            _stringrefNoneButton = this.FindControl<Button>("stringrefNoneButton");
            _languageSelect = this.FindControl<ComboBox>("languageSelect");
            _maleRadio = this.FindControl<RadioButton>("maleRadio");
            _femaleRadio = this.FindControl<RadioButton>("femaleRadio");
            _stringEdit = this.FindControl<TextBox>("stringEdit");
            _okButton = this.FindControl<Button>("okButton");
            _cancelButton = this.FindControl<Button>("cancelButton");

            if (_okButton != null)
            {
                _okButton.Click += (s, e) => Accept();
            }
            if (_cancelButton != null)
            {
                _cancelButton.Click += (s, e) => Close();
            }
            if (_stringrefNoneButton != null)
            {
                _stringrefNoneButton.Click += (s, e) => NoTlkString();
            }
            if (_stringrefNewButton != null)
            {
                _stringrefNewButton.Click += (s, e) => NewTlkString();
            }
            if (_stringrefSpin != null)
            {
                _stringrefSpin.ValueChanged += (s, e) => StringRefChanged((int)_stringrefSpin.Value);
            }
            if (_maleRadio != null)
            {
                _maleRadio.IsCheckedChanged += (s, e) => SubstringChanged();
            }
            if (_femaleRadio != null)
            {
                _femaleRadio.IsCheckedChanged += (s, e) => SubstringChanged();
            }
            if (_languageSelect != null)
            {
                _languageSelect.SelectionChanged += (s, e) => SubstringChanged();
            }
            if (_stringEdit != null)
            {
                _stringEdit.TextChanged += (s, e) => StringEdited();
            }

            // Load current locstring values
            if (LocString != null && _stringrefSpin != null)
            {
                _stringrefSpin.Value = LocString.StringRef;
                StringRefChanged(LocString.StringRef);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:75-85
        // Original: def stringref_changed(self, stringref: int):
        private void StringRefChanged(int stringref)
        {
            var substringFrame = this.FindControl<Control>("substringFrame");
            if (substringFrame != null)
            {
                substringFrame.IsVisible = stringref == -1;
            }

            if (LocString != null)
            {
                LocString.StringRef = stringref;
            }

            if (stringref == -1)
            {
                UpdateText();
            }
            else if (_installation != null && _stringEdit != null)
            {
                _stringEdit.Text = _installation.String(LocString);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:87-88
        // Original: def new_tlk_string(self):
        private void NewTlkString()
        {
            if (_installation != null && _stringrefSpin != null)
            {
                try
                {
                    var talkTable = _installation.TalkTable();
                    int size = talkTable.Size();
                    _stringrefSpin.Value = size;
                }
                catch
                {
                    // If we can't get the talktable size, fall back to a default value
                    _stringrefSpin.Value = 1000;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:90-91
        // Original: def no_tlk_string(self):
        private void NoTlkString()
        {
            if (_stringrefSpin != null)
            {
                _stringrefSpin.Value = -1;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:93-94
        // Original: def substring_changed(self):
        private void SubstringChanged()
        {
            UpdateText();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:96-100
        // Original: def _update_text(self):
        private void UpdateText()
        {
            if (LocString == null || _languageSelect == null || _stringEdit == null)
            {
                return;
            }

            // TODO: Implement language/gender selection when Language/Gender enums are available
            var text = LocString.ToString();
            _stringEdit.Text = text;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:102-106
        // Original: def string_edited(self):
        private void StringEdited()
        {
            if (LocString == null || LocString.StringRef != -1 || _stringEdit == null)
            {
                return;
            }

            // TODO: Update locstring with edited text when language/gender selection is implemented
            // For now, just store the text
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:62-70
        // Original: def accept(self):
        private void Accept()
        {
            // TODO: Save to TLK if stringref != -1
            // For now, just close
            Close();
        }

        public bool ShowDialog()
        {
            // Show dialog and return result
            // This will be implemented when dialog system is available
            return true;
        }
    }
}
