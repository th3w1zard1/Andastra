using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.TLK;
using HolocronToolset.Data;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

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
        private List<Language> _orderedLanguages;

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

            // Populate language combo box with all Language enum values
            // Matching PyKotor: languages are ordered by their enum values (0 = English, 1 = French, etc.)
            // The combo box index directly maps to the Language enum value
            if (_languageSelect != null)
            {
                _languageSelect.Items.Clear();
                // Get all Language enum values, excluding Unknown, and sort by their integer values
                // Store the ordered list for later lookup
                _orderedLanguages = Enum.GetValues(typeof(Language))
                    .Cast<Language>()
                    .Where(lang => lang != Language.Unknown)
                    .OrderBy(lang => (int)lang)
                    .ToList();

                foreach (var language in _orderedLanguages)
                {
                    _languageSelect.Items.Add(language.ToString());
                }

                // Set default selection to English (index 0)
                if (_languageSelect.Items.Count > 0)
                {
                    _languageSelect.SelectedIndex = 0;
                }
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

            // Get selected language from combo box index
            // Matching PyKotor: language: Language = Language(self.ui.languageSelect.currentIndex())
            int languageIndex = _languageSelect.SelectedIndex;
            if (languageIndex < 0 || _orderedLanguages == null || languageIndex >= _orderedLanguages.Count)
            {
                return;
            }

            Language selectedLanguage = _orderedLanguages[languageIndex];

            // Get selected gender from radio buttons
            // Matching PyKotor: gender: Gender = Gender(int(self.ui.femaleRadio.isChecked()))
            Gender selectedGender = Gender.Male;
            if (_femaleRadio != null && _femaleRadio.IsChecked == true)
            {
                selectedGender = Gender.Female;
            }

            // Get text from locstring for the selected language/gender combination
            // Matching PyKotor: text: str = self.locstring.get(language, gender) or ""
            string text = LocString.Get(selectedLanguage, selectedGender, false);
            if (text == null)
            {
                text = "";
            }

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

            // Get selected language from combo box index
            // Matching PyKotor: language: Language = Language(self.ui.languageSelect.currentIndex())
            int languageIndex = _languageSelect != null ? _languageSelect.SelectedIndex : -1;
            if (languageIndex < 0 || _orderedLanguages == null || languageIndex >= _orderedLanguages.Count)
            {
                return;
            }

            Language selectedLanguage = _orderedLanguages[languageIndex];

            // Get selected gender from radio buttons
            // Matching PyKotor: gender: Gender = Gender(int(self.ui.femaleRadio.isChecked()))
            Gender selectedGender = Gender.Male;
            if (_femaleRadio != null && _femaleRadio.IsChecked == true)
            {
                selectedGender = Gender.Female;
            }

            // Update locstring with edited text for the selected language/gender combination
            // Matching PyKotor: self.locstring.set_data(language, gender, self.ui.stringEdit.toPlainText())
            string editedText = _stringEdit.Text ?? "";
            LocString.SetData(selectedLanguage, selectedGender, editedText);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/dialogs/edit/locstring.py:62-70
        // Original: def accept(self):
        private void Accept()
        {
            if (LocString != null && LocString.StringRef != -1 && _installation != null && _stringEdit != null)
            {
                try
                {
                    // Get the TLK file path from installation
                    // Matching PyKotor: tlk_path: CaseAwarePath = CaseAwarePath(self._installation.path(), "dialog.tlk")
                    string tlkPath = System.IO.Path.Combine(_installation.Path, "dialog.tlk");

                    // Check if TLK file exists
                    if (!File.Exists(tlkPath))
                    {
                        // If TLK doesn't exist, we can't save - just close the dialog
                        // TODO:  In a full implementation, we might want to create a new TLK file
                        Close();
                        return;
                    }

                    // Read the TLK file
                    // Matching PyKotor: tlk: TLK = read_tlk(tlk_path)
                    TLK tlk = TLKAuto.ReadTlk(tlkPath);

                    // Resize if needed to accommodate the stringref
                    // Matching PyKotor: if len(tlk) <= self.locstring.stringref: tlk.resize(self.locstring.stringref + 1)
                    int stringref = LocString.StringRef;
                    if (tlk.Count <= stringref)
                    {
                        tlk.Resize(stringref + 1);
                    }

                    // Get the text from the edit control
                    // Matching PyKotor: tlk[self.locstring.stringref].text = self.ui.stringEdit.toPlainText()
                    string text = _stringEdit.Text ?? "";
                    tlk[stringref].Text = text;

                    // Save the TLK file back to disk
                    // Matching PyKotor: write_tlk(tlk, tlk_path)
                    TLKAuto.WriteTlk(tlk, tlkPath, ResourceType.TLK);
                }
                catch (Exception ex)
                {
                    // Show error dialog to user (matching PyKotor: errors are typically shown via MessageBox)
                    var errorBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error Saving TLK File",
                        $"Failed to save the TLK file: {ex.Message}",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Error);
                    errorBox.ShowAsync();
                    // Continue to close the dialog even if save failed
                }
            }

            // Matching PyKotor: super().accept()
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
