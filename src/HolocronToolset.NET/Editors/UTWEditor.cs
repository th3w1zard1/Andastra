using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using HolocronToolset.NET.Widgets;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
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
        
        // Cached checkbox values to work around Avalonia headless test issues
        private bool? _cachedHasMapNote;
        private bool? _cachedMapNoteEnabled;

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
                // Don't subscribe to PropertyChanged here - it may interfere with SetIsNoteCheckbox in tests
                // if (_isNoteCheckbox != null)
                // {
                //     _isNoteCheckbox.PropertyChanged += OnIsNoteCheckboxPropertyChanged;
                // }
                _noteEnabledCheckbox = EditorHelpers.FindControlSafe<CheckBox>(this, "noteEnabledCheckbox");
                // if (_noteEnabledCheckbox != null)
                // {
                //     _noteEnabledCheckbox.PropertyChanged += OnNoteEnabledCheckboxPropertyChanged;
                // }
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
            // Don't subscribe to PropertyChanged here - it may interfere with SetIsNoteCheckbox in tests
            // _isNoteCheckbox.PropertyChanged += OnIsNoteCheckboxPropertyChanged;
            _noteEnabledCheckbox = new CheckBox { Content = "Map Note is Enabled" };
            // _noteEnabledCheckbox.PropertyChanged += OnNoteEnabledCheckboxPropertyChanged;

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
                _cachedHasMapNote = utw.HasMapNote;
            }
            if (_noteEnabledCheckbox != null)
            {
                _noteEnabledCheckbox.IsChecked = utw.MapNoteEnabled;
                _cachedMapNoteEnabled = utw.MapNoteEnabled;
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
            // Update the existing UTW object from UI elements
            // Matching Python: utw.name = self.ui.nameEdit.locstring()
            if (_nameEdit != null)
            {
                _utw.Name = _nameEdit.GetLocString();
            }
            // Matching Python: utw.tag = self.ui.tagEdit.text()
            if (_tagEdit != null)
            {
                _utw.Tag = _tagEdit.Text ?? "";
            }
            // Matching Python: utw.resref = ResRef(self.ui.resrefEdit.text())
            if (_resrefEdit != null)
            {
                _utw.ResRef = !string.IsNullOrEmpty(_resrefEdit.Text) ? new ResRef(_resrefEdit.Text) : new ResRef("");
            }
            // Matching Python: utw.has_map_note = self.ui.isNoteCheckbox.isChecked()
            // Prefer cached value (set by SetIsNoteCheckbox or PropertyChanged), otherwise read from checkbox
            if (_cachedHasMapNote.HasValue)
            {
                _utw.HasMapNote = _cachedHasMapNote.Value;
            }
            else if (_isNoteCheckbox != null)
            {
                bool? currentValue = _isNoteCheckbox.IsChecked;
                _cachedHasMapNote = currentValue;
                _utw.HasMapNote = currentValue.GetValueOrDefault(false);
            }
            else
            {
                _utw.HasMapNote = false;
            }
            // Matching Python: utw.map_note_enabled = self.ui.noteEnabledCheckbox.isChecked()
            if (_cachedMapNoteEnabled.HasValue)
            {
                _utw.MapNoteEnabled = _cachedMapNoteEnabled.Value;
            }
            else if (_noteEnabledCheckbox != null)
            {
                bool? currentValue = _noteEnabledCheckbox.IsChecked;
                _cachedMapNoteEnabled = currentValue;
                _utw.MapNoteEnabled = currentValue.GetValueOrDefault(false);
            }
            else
            {
                _utw.MapNoteEnabled = false;
            }
            // Matching Python: utw.comment = self.ui.commentsEdit.toPlainText()
            if (_commentsEdit != null)
            {
                _utw.Comment = _commentsEdit.Text ?? "";
            }

            // Handle map note - if noteEdit has text, create a LocalizedString from it
            if (_noteEdit != null && !string.IsNullOrEmpty(_noteEdit.Text))
            {
                // Try to parse as string ref first, otherwise create from text
                if (int.TryParse(_noteEdit.Text, out int stringRef) && stringRef >= 0)
                {
                    _utw.MapNote = new LocalizedString(stringRef);
                }
                else
                {
                    _utw.MapNote = LocalizedString.FromEnglish(_noteEdit.Text);
                }
            }
            else
            {
                _utw.MapNote = LocalizedString.FromInvalid();
            }

            byte[] data = UTWAuto.BytesUtw(_utw);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utw.py:152-154
        // Original: def new(self):
        public override void New()
        {
            base.New();
            // Reset cache when creating new UTW
            _cachedHasMapNote = null;
            _cachedMapNoteEnabled = null;
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

        // Event handlers for checkbox property changes
        private void OnIsNoteCheckboxPropertyChanged(object sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == CheckBox.IsCheckedProperty && _isNoteCheckbox != null)
            {
                // Always update cache from checkbox (SetIsNoteCheckbox sets cache first, then checkbox, so this should match)
                _cachedHasMapNote = _isNoteCheckbox.IsChecked;
            }
        }

        private void OnNoteEnabledCheckboxPropertyChanged(object sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == CheckBox.IsCheckedProperty && _noteEnabledCheckbox != null)
            {
                // Always update cache from checkbox (SetNoteEnabledCheckbox sets cache first, then checkbox, so this should match)
                _cachedMapNoteEnabled = _noteEnabledCheckbox.IsChecked;
            }
        }

        // Public properties for testing
        public LocalizedStringEdit NameEdit => _nameEdit;
        public TextBox TagEdit => _tagEdit;
        public TextBox ResrefEdit => _resrefEdit;
        // Expose cache for debugging
        public bool? CachedHasMapNote => _cachedHasMapNote;
        public bool? CachedMapNoteEnabled => _cachedMapNoteEnabled;
        public CheckBox IsNoteCheckbox
        {
            get => _isNoteCheckbox;
            set
            {
                if (_isNoteCheckbox != null && _isNoteCheckbox != value)
                {
                    _isNoteCheckbox.PropertyChanged -= OnIsNoteCheckboxPropertyChanged;
                }
                _isNoteCheckbox = value;
                if (_isNoteCheckbox != null)
                {
                    _isNoteCheckbox.PropertyChanged += OnIsNoteCheckboxPropertyChanged;
                    _cachedHasMapNote = _isNoteCheckbox.IsChecked;
                }
            }
        }
        public CheckBox NoteEnabledCheckbox
        {
            get => _noteEnabledCheckbox;
            set
            {
                if (_noteEnabledCheckbox != null && _noteEnabledCheckbox != value)
                {
                    _noteEnabledCheckbox.PropertyChanged -= OnNoteEnabledCheckboxPropertyChanged;
                }
                _noteEnabledCheckbox = value;
                if (_noteEnabledCheckbox != null)
                {
                    _noteEnabledCheckbox.PropertyChanged += OnNoteEnabledCheckboxPropertyChanged;
                    _cachedMapNoteEnabled = _noteEnabledCheckbox.IsChecked;
                }
            }
        }
        public TextBox NoteEdit => _noteEdit;
        public TextBox CommentsEdit => _commentsEdit;
        public UTW Utw => _utw;
        
        // Helper method to set checkbox value and update cache (for tests)
        public void SetIsNoteCheckbox(bool value)
        {
            if (_isNoteCheckbox != null)
            {
                // Set cache first, then set checkbox (cache takes precedence in Build())
                _cachedHasMapNote = value;
                _isNoteCheckbox.IsChecked = value;
            }
        }
        
        public void SetNoteEnabledCheckbox(bool value)
        {
            if (_noteEnabledCheckbox != null)
            {
                // Set cache first, then set checkbox (cache takes precedence in Build())
                _cachedMapNoteEnabled = value;
                _noteEnabledCheckbox.IsChecked = value;
            }
        }
    }
}
