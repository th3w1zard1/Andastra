using Andastra.Parsing.Common;
using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Widgets;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:23
    // Original: class UTWEditor(Editor):
    public class UTWEditor : Editor
    {
        private UTW _utw;

        // UI Controls - Basic
        private LocalizedStringEdit _nameEdit;
        private TextBox _tagEdit;
        private Button _tagGenerateButton;
        private TextBox _resrefEdit;
        private Button _resrefGenerateButton;

        // UI Controls - Advanced
        private CheckBox _isNoteCheckbox;
        private CheckBox _noteEnabledCheckbox;
        private TextBox _noteEdit;
        private Button _noteChangeButton;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:24-63
        // Original: def __init__(self, parent, installation):
        public UTWEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Waypoint Editor", "waypoint",
                new[] { ResourceType.UTW },
                new[] { ResourceType.UTW },
                installation)
        {
            _utw = new UTW();
            InitializeComponent();
            SetupSignals();
            if (installation != null)
            {
                SetupInstallation(installation);
            }
            New();
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
            else
            {
                // Try to find controls from XAML
                _nameEdit = EditorHelpers.FindControlSafe<LocalizedStringEdit>(this, "nameEdit");
                _tagEdit = EditorHelpers.FindControlSafe<TextBox>(this, "tagEdit");
                _tagGenerateButton = EditorHelpers.FindControlSafe<Button>(this, "tagGenerateButton");
                _resrefEdit = EditorHelpers.FindControlSafe<TextBox>(this, "resrefEdit");
                _resrefGenerateButton = EditorHelpers.FindControlSafe<Button>(this, "resrefGenerateButton");
                _isNoteCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "isNoteCheckbox");
                _noteEnabledCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "noteEnabledCheckbox");
                _noteEdit = EditorHelpers.FindControlSafe<TextBox>(this, "noteEdit");
                _noteChangeButton = EditorHelpers.FindControlSafe<Button>(this, "noteChangeButton");
                _commentsEdit = EditorHelpers.FindControlSafe<TextBox>(this, "commentsEdit");

                // If any critical controls are missing, fall back to programmatic UI
                if (_nameEdit == null || _tagEdit == null || _resrefEdit == null ||
                    _isNoteCheckbox == null || _noteEnabledCheckbox == null ||
                    _commentsEdit == null)
                {
                    SetupProgrammaticUI();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:65-68
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_tagGenerateButton != null)
            {
                _tagGenerateButton.Click += (s, e) => GenerateTag();
            }
            if (_resrefGenerateButton != null)
            {
                _resrefGenerateButton.Click += (s, e) => GenerateResref();
            }
            if (_noteChangeButton != null)
            {
                _noteChangeButton.Click += (s, e) => ChangeNote();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:70-72
        // Original: def _setup_installation(self, installation: HTInstallation):
        private void SetupInstallation(HTInstallation installation)
        {
            if (_nameEdit != null)
            {
                _nameEdit.SetInstallation(installation);
            }
        }

        private void SetupProgrammaticUI()
        {
            var scrollViewer = new ScrollViewer();
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Tab Control
            var tabControl = new TabControl();

            // Basic Tab
            var basicTab = new TabItem { Header = "Basic" };
            var basicPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Name
            var nameLabel = new TextBlock { Text = "Name:" };
            _nameEdit = new LocalizedStringEdit();
            if (_installation != null)
            {
                _nameEdit.SetInstallation(_installation);
            }
            basicPanel.Children.Add(nameLabel);
            basicPanel.Children.Add(_nameEdit);

            // Tag
            var tagLabel = new TextBlock { Text = "Tag:" };
            var tagPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _tagEdit = new TextBox();
            _tagGenerateButton = new Button { Content = "-", Width = 26 };
            _tagGenerateButton.Click += (s, e) => GenerateTag();
            tagPanel.Children.Add(_tagEdit);
            tagPanel.Children.Add(_tagGenerateButton);
            basicPanel.Children.Add(tagLabel);
            basicPanel.Children.Add(tagPanel);

            // ResRef
            var resrefLabel = new TextBlock { Text = "ResRef:" };
            var resrefPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _resrefEdit = new TextBox { MaxLength = 16 };
            _resrefGenerateButton = new Button { Content = "-", Width = 26 };
            _resrefGenerateButton.Click += (s, e) => GenerateResref();
            resrefPanel.Children.Add(_resrefEdit);
            resrefPanel.Children.Add(_resrefGenerateButton);
            basicPanel.Children.Add(resrefLabel);
            basicPanel.Children.Add(resrefPanel);

            basicTab.Content = basicPanel;
            tabControl.Items.Add(basicTab);

            // Advanced Tab
            var advancedTab = new TabItem { Header = "Advanced" };
            var advancedPanel = new StackPanel { Orientation = Orientation.Vertical };

            _isNoteCheckbox = new CheckBox { Content = "Is a Map Note" };
            _noteEnabledCheckbox = new CheckBox { Content = "Map Note is Enabled" };

            // Map Note
            var noteLabel = new TextBlock { Text = "Map Note:" };
            var notePanel = new StackPanel { Orientation = Orientation.Horizontal };
            _noteEdit = new TextBox();
            _noteChangeButton = new Button { Content = "...", Width = 26 };
            _noteChangeButton.Click += (s, e) => ChangeNote();
            notePanel.Children.Add(_noteEdit);
            notePanel.Children.Add(_noteChangeButton);

            advancedPanel.Children.Add(_isNoteCheckbox);
            advancedPanel.Children.Add(_noteEnabledCheckbox);
            advancedPanel.Children.Add(noteLabel);
            advancedPanel.Children.Add(notePanel);

            advancedTab.Content = advancedPanel;
            tabControl.Items.Add(advancedTab);

            // Comments Tab
            var commentsTab = new TabItem { Header = "Comments" };
            _commentsEdit = new TextBox
            {
                AcceptsReturn = true,
                AcceptsTab = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            };
            commentsTab.Content = _commentsEdit;
            tabControl.Items.Add(commentsTab);

            mainPanel.Children.Add(tabControl);
            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:74-84
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            try
            {
                var utw = UTWAuto.ReadUtw(data);
                LoadUTW(utw);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load UTW: {ex}");
                New();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:86-113
        // Original: def _loadUTW(self, utw: UTW):
        private void LoadUTW(UTW utw)
        {
            _utw = utw;

            // Basic
            if (_nameEdit != null)
            {
                _nameEdit.SetLocString(utw.Name);
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = utw.Tag ?? "";
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = utw.ResRef?.ToString() ?? "";
            }

            // Advanced
            if (_isNoteCheckbox != null)
            {
                _isNoteCheckbox.IsChecked = utw.HasMapNote;
            }
            if (_noteEnabledCheckbox != null)
            {
                _noteEnabledCheckbox.IsChecked = utw.MapNoteEnabled;
            }
            if (_noteEdit != null)
            {
                // Map note is a LocalizedString, but we'll display it as text for now
                // If it's a string ref, show the string ref; otherwise show the text
                if (utw.MapNote != null && utw.MapNote.StringRef == -1)
                {
                    _noteEdit.Text = utw.MapNote.ToString();
                }
                else if (utw.MapNote != null && _installation != null)
                {
                    _noteEdit.Text = _installation.String(utw.MapNote);
                }
                else if (utw.MapNote != null)
                {
                    _noteEdit.Text = $"StringRef: {utw.MapNote.StringRef}";
                }
                else
                {
                    _noteEdit.Text = "";
                }
            }

            // Comments
            if (_commentsEdit != null)
            {
                _commentsEdit.Text = utw.Comment ?? "";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:115-150
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching Python: utw: UTW = deepcopy(self._utw)
            var utw = CopyUtw(_utw);

            // Matching Python: utw.name = self.ui.nameEdit.locstring()
            if (_nameEdit != null)
            {
                utw.Name = _nameEdit.GetLocString();
            }

            // Matching Python: utw.tag = self.ui.tagEdit.text()
            if (_tagEdit != null)
            {
                utw.Tag = _tagEdit.Text ?? "";
            }

            // Matching Python: utw.resref = ResRef(self.ui.resrefEdit.text())
            if (_resrefEdit != null)
            {
                utw.ResRef = new ResRef(_resrefEdit.Text ?? "");
            }

            // Matching Python: utw.has_map_note = self.ui.isNoteCheckbox.isChecked()
            if (_isNoteCheckbox != null)
            {
                utw.HasMapNote = _isNoteCheckbox.IsChecked == true;
            }

            // Matching Python: utw.map_note_enabled = self.ui.noteEnabledCheckbox.isChecked()
            if (_noteEnabledCheckbox != null)
            {
                System.Console.WriteLine($"[BUILD DEBUG] _noteEnabledCheckbox.IsChecked = {_noteEnabledCheckbox.IsChecked}");
                utw.MapNoteEnabled = _noteEnabledCheckbox.IsChecked == true;
                System.Console.WriteLine($"[BUILD DEBUG] utw.MapNoteEnabled = {utw.MapNoteEnabled}");
            }

            // Matching Python: utw.map_note = self.ui.noteEdit.locstring / LocalizedString(self.ui.noteEdit.text())
            if (_noteEdit != null && !string.IsNullOrEmpty(_noteEdit.Text))
            {
                utw.MapNote = LocalizedString.FromEnglish(_noteEdit.Text);
            }

            // Matching Python: utw.comment = self.ui.commentsEdit.toPlainText()
            if (_commentsEdit != null)
            {
                utw.Comment = _commentsEdit.Text ?? "";
            }

            // Matching Python: gff: GFF = dismantle_utw(utw); write_gff(gff, data)
            byte[] data = UTWAuto.BytesUtw(utw);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching Python: deepcopy(self._utw)
        private static UTW CopyUtw(UTW source)
        {
            var copy = new UTW();
            copy.Name = CopyLocalizedString(source.Name);
            copy.Tag = source.Tag;
            copy.ResRef = source.ResRef;
            copy.HasMapNote = source.HasMapNote;
            copy.MapNoteEnabled = source.MapNoteEnabled;
            copy.MapNote = CopyLocalizedString(source.MapNote);
            copy.AppearanceId = source.AppearanceId;
            copy.PaletteId = source.PaletteId;
            copy.Comment = source.Comment;
            copy.LinkedTo = source.LinkedTo;
            copy.Description = CopyLocalizedString(source.Description);
            return copy;
        }

        private static LocalizedString CopyLocalizedString(LocalizedString source)
        {
            if (source == null)
            {
                return LocalizedString.FromInvalid();
            }
            var copy = new LocalizedString(source.StringRef);
            foreach (var (language, gender, text) in source)
            {
                copy.SetData(language, gender, text);
            }
            return copy;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:152-154
        // Original: def new(self):
        public override void New()
        {
            base.New();
            LoadUTW(new UTW());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:156-160
        // Original: def change_name(self):
        // Note: Name change is handled by LocalizedStringEdit's edit button

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:162-169
        // Original: def change_note(self):
        private async void ChangeNote()
        {
            if (_installation == null)
            {
                return;
            }

            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            LocalizedString currentNote = LocalizedString.FromInvalid();
            if (_noteEdit != null && !string.IsNullOrEmpty(_noteEdit.Text))
            {
                if (int.TryParse(_noteEdit.Text, out int stringRef) && stringRef >= 0)
                {
                    currentNote = new LocalizedString(stringRef);
                }
                else
                {
                    currentNote = LocalizedString.FromEnglish(_noteEdit.Text);
                }
            }

            var dialog = new LocalizedStringDialog(parentWindow, _installation, currentNote);
            var result = await dialog.ShowDialog<bool>(parentWindow);
            if (result)
            {
                var newNote = dialog.LocString;
                if (_noteEdit != null)
                {
                    if (newNote.StringRef == -1)
                    {
                        _noteEdit.Text = newNote.ToString();
                    }
                    else if (_installation != null)
                    {
                        _noteEdit.Text = _installation.String(newNote);
                    }
                    else
                    {
                        _noteEdit.Text = $"StringRef: {newNote.StringRef}";
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:171-174
        // Original: def generate_tag(self):
        private void GenerateTag()
        {
            if (_resrefEdit != null && string.IsNullOrEmpty(_resrefEdit.Text))
            {
                GenerateResref();
            }
            if (_tagEdit != null && _resrefEdit != null)
            {
                _tagEdit.Text = _resrefEdit.Text;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:176-180
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                if (!string.IsNullOrEmpty(_resname))
                {
                    _resrefEdit.Text = _resname;
                }
                else
                {
                    _resrefEdit.Text = "m00xx_way_000";
                }
            }
        }

        public override void SaveAs()
        {
            Save();
        }

        // Public properties for testing - matching Python's self.ui structure
        public LocalizedStringEdit NameEdit => _nameEdit;
        public TextBox TagEdit => _tagEdit;
        public Button TagGenerateButton => _tagGenerateButton;
        public TextBox ResrefEdit => _resrefEdit;
        public Button ResrefGenerateButton => _resrefGenerateButton;
        public CheckBox IsNoteCheckbox => _isNoteCheckbox;
        public CheckBox NoteEnabledCheckbox => _noteEnabledCheckbox;
        public TextBox NoteEdit => _noteEdit;
        public Button NoteChangeButton => _noteChangeButton;
        public TextBox CommentsEdit => _commentsEdit;
        public UTW Utw => _utw;
    }
}
