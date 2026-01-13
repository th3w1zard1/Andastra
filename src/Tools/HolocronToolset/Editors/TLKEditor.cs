using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Window;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Common.Language;
using BioWare.NET.Resource.Formats.TLK;
using BioWare.NET.Resource;
using HolocronToolset.Common;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:56
    // Original: class TLKEditor(Editor):
    public partial class TLKEditor : Editor
    {
        private ObservableCollection<TLKEntryViewModel> _sourceEntries;
        private CollectionViewSource _filteredEntries;
        private Language _language;
        private TextBox _textEdit;
        private TextBox _soundEdit;
        private TextBox _searchEdit;
        private Button _searchButton;
        private NumericUpDown _jumpSpinbox;
        private Button _jumpButton;
        private DataGrid _talkTable;
        private Control _searchBox;
        private Control _jumpBox;
        private TLKEntryViewModel _selectedEntry;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:57-95
        // Original: def __init__(self, parent, installation):
        public TLKEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "TLK Editor", "none",
                new[] { ResourceType.TLK, ResourceType.TLK_XML, ResourceType.TLK_JSON },
                new[] { ResourceType.TLK, ResourceType.TLK_XML, ResourceType.TLK_JSON },
                installation)
        {
            _sourceEntries = new ObservableCollection<TLKEntryViewModel>();
            _filteredEntries = new CollectionViewSource { Source = _sourceEntries };
            _language = Language.English;

            InitializeComponent();
            SetupUI();
            SetupSignals();
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
        }

        private void SetupProgrammaticUI()
        {
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Search box
            var searchBoxPanel = new StackPanel { Orientation = Orientation.Horizontal, IsVisible = false };
            _searchBox = searchBoxPanel;
            _searchEdit = new TextBox { Watermark = "Search..." };
            _searchButton = new Button { Content = "Search" };
            searchBoxPanel.Children.Add(_searchEdit);
            searchBoxPanel.Children.Add(_searchButton);
            mainPanel.Children.Add(searchBoxPanel);

            // Jump box
            var jumpBoxPanel = new StackPanel { Orientation = Orientation.Horizontal, IsVisible = false };
            _jumpBox = jumpBoxPanel;
            _jumpSpinbox = new NumericUpDown { Minimum = 0, Maximum = 0 };
            _jumpButton = new Button { Content = "Go" };
            jumpBoxPanel.Children.Add(_jumpSpinbox);
            jumpBoxPanel.Children.Add(_jumpButton);
            mainPanel.Children.Add(jumpBoxPanel);

            // Table
            _talkTable = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                SelectionMode = DataGridSelectionMode.Single
            };
            _talkTable.Columns.Add(new DataGridTextColumn
            {
                Header = "Text",
                Binding = new Binding("Text"),
                IsReadOnly = false
            });
            _talkTable.Columns.Add(new DataGridTextColumn
            {
                Header = "Sound",
                Binding = new Binding("Sound"),
                IsReadOnly = false
            });
            mainPanel.Children.Add(_talkTable);

            // Bottom panel
            var bottomPanel = new StackPanel { Orientation = Orientation.Vertical };
            _textEdit = new TextBox { AcceptsReturn = true, Watermark = "Text" };
            _soundEdit = new TextBox { MaxLength = 16, Watermark = "Sound ResRef" };
            bottomPanel.Children.Add(new TextBlock { Text = "Text:" });
            bottomPanel.Children.Add(_textEdit);
            bottomPanel.Children.Add(new TextBlock { Text = "Sound:" });
            bottomPanel.Children.Add(_soundEdit);
            mainPanel.Children.Add(bottomPanel);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // If controls are already initialized (e.g., by SetupProgrammaticUI), skip control finding
            if (_talkTable != null && _textEdit != null && _soundEdit != null && _searchEdit != null && _searchButton != null && _jumpSpinbox != null && _jumpButton != null)
            {
                if (_talkTable.ItemsSource == null)
                {
                    _talkTable.ItemsSource = _filteredEntries.View;
                }
                return;
            }

            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // Try to find controls from XAML if available
                _talkTable = this.FindControl<DataGrid>("talkTable");
                _textEdit = this.FindControl<TextBox>("textEdit");
                _soundEdit = this.FindControl<TextBox>("soundEdit");
                _searchEdit = this.FindControl<TextBox>("searchEdit");
                _searchButton = this.FindControl<Button>("searchButton");
                _jumpSpinbox = this.FindControl<NumericUpDown>("jumpSpinbox");
                _jumpButton = this.FindControl<Button>("jumpButton");
                var searchBoxBorder = this.FindControl<Border>("searchBox");
                if (searchBoxBorder != null)
                {
                    _searchBox = searchBoxBorder;
                }
                var jumpBoxBorder = this.FindControl<Border>("jumpBox");
                if (jumpBoxBorder != null)
                {
                    _jumpBox = jumpBoxBorder;
                }
            }
            catch
            {
                // XAML controls not available - controls should already be initialized by SetupProgrammaticUI
                // If not, ensure minimal setup
                if (_talkTable == null)
                {
                    _talkTable = new DataGrid
                    {
                        AutoGenerateColumns = false,
                        CanUserReorderColumns = false,
                        CanUserResizeColumns = true,
                        SelectionMode = DataGridSelectionMode.Single
                    };
                }
                if (_textEdit == null)
                {
                    _textEdit = new TextBox { AcceptsReturn = true };
                }
                if (_soundEdit == null)
                {
                    _soundEdit = new TextBox { MaxLength = 16 };
                }
                if (_searchEdit == null)
                {
                    _searchEdit = new TextBox { Watermark = "Search..." };
                }
                if (_searchButton == null)
                {
                    _searchButton = new Button { Content = "Search" };
                }
                if (_jumpSpinbox == null)
                {
                    _jumpSpinbox = new NumericUpDown { Minimum = 0, Maximum = 0 };
                }
                if (_jumpButton == null)
                {
                    _jumpButton = new Button { Content = "Go" };
                }
                if (_searchBox == null)
                {
                    _searchBox = new StackPanel { Orientation = Orientation.Horizontal };
                }
                if (_jumpBox == null)
                {
                    _jumpBox = new StackPanel { Orientation = Orientation.Horizontal };
                }
            }

            if (_talkTable != null)
            {
                if (_talkTable.ItemsSource == null)
                {
                    _talkTable.ItemsSource = _filteredEntries.View;
                }
            }
            else if (_talkTable == null && Content is StackPanel panel)
            {
                // Ensure table is set up if created programmatically
                foreach (var child in panel.Children)
                {
                    if (child is DataGrid dg)
                    {
                        _talkTable = dg;
                        if (_talkTable.ItemsSource == null)
                        {
                            _talkTable.ItemsSource = _filteredEntries.View;
                        }
                        break;
                    }
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:97-166
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_jumpButton != null)
            {
                _jumpButton.Click += (s, e) => OnJumpSpinboxGoto();
            }

            if (_jumpSpinbox != null)
            {
                _jumpSpinbox.ValueChanged += (s, e) => OnJumpSpinboxGoto();
                _jumpSpinbox.KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter || e.Key == Key.Return)
                    {
                        OnJumpSpinboxGoto();
                        e.Handled = true;
                    }
                };
            }

            if (_searchButton != null)
            {
                _searchButton.Click += (s, e) => DoFilter(_searchEdit?.Text ?? "");
            }

            if (_talkTable != null)
            {
                _talkTable.SelectionChanged += (s, e) => SelectionChanged();
            }

            if (_textEdit != null)
            {
                _textEdit.TextChanged += (s, e) => UpdateEntry();
            }

            if (_soundEdit != null)
            {
                _soundEdit.TextChanged += (s, e) => UpdateEntry();
            }
        }

        private void OnJumpSpinboxGoto()
        {
            if (_jumpSpinbox == null || _talkTable == null)
            {
                return;
            }

            int sourceRow = (int)(_jumpSpinbox.Value ?? 0);
            if (sourceRow < 0 || sourceRow >= _sourceEntries.Count)
            {
                return;
            }

            var entry = _sourceEntries[sourceRow];
            if (entry != null)
            {
                _talkTable.SelectedItem = entry;
                _talkTable.ScrollIntoView(entry, null);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:223-239
        // Original: def change_language(self, language):
        public void ChangeLanguage(Language language)
        {
            _language = language;

            // Only reload if we have revert data (file was loaded)
            if (_revert == null || _revert.Length == 0)
            {
                return;
            }

            var tlk = TLKAuto.ReadTlk(_revert);
            tlk.Language = language;
            LoadTLK(tlk);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:241-255
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            if (data == null || data.Length == 0)
            {
                _sourceEntries.Clear();
                return;
            }

            // TODO:  Load TLK synchronously for now (can be made async later)
            try
            {
                var tlk = TLKAuto.ReadTlk(data);
                _language = tlk.Language;
                LoadTLK(tlk);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error loading TLK: {ex}");
            }
        }

        private void LoadTLK(TLK tlk)
        {
            _sourceEntries.Clear();
            _language = tlk.Language;

            // Load entries in batches for performance
            int batchSize = 200;
            for (int i = 0; i < tlk.Entries.Count; i++)
            {
                var entry = tlk.Entries[i];
                _sourceEntries.Add(new TLKEntryViewModel(i, entry.Text, entry.Voiceover.ToString()));

                // Yield to UI thread periodically
                if (i % batchSize == 0 && i > 0)
                {
                    Thread.Sleep(1);
                }
            }

            if (_jumpSpinbox != null)
            {
                _jumpSpinbox.Maximum = _sourceEntries.Count;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:352-376
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _sourceEntries.Clear();
            if (_textEdit != null)
            {
                _textEdit.IsEnabled = false;
            }
            if (_soundEdit != null)
            {
                _soundEdit.IsEnabled = false;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:361-376
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            var tlk = new TLK(_language);

            foreach (var entry in _sourceEntries)
            {
                tlk.Entries.Add(entry.ToTLKEntry());
            }

            ResourceType tlkType = _restype ?? ResourceType.TLK;
            byte[] data = TLKAuto.BytesTlk(tlk, tlkType);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:378-379
        // Original: def insert(self):
        public void Insert()
        {
            int newIndex = _sourceEntries.Count;
            _sourceEntries.Add(new TLKEntryViewModel(newIndex, "", ""));
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:381-385
        // Original: def do_filter(self, text):
        public void DoFilter(string text)
        {
            if (_filteredEntries.View == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                _filteredEntries.View.Filter = null;
            }
            else
            {
                string filterText = text.ToLowerInvariant();
                _filteredEntries.View.Filter = item =>
                {
                    if (item is TLKEntryViewModel entry)
                    {
                        return entry.Text.ToLowerInvariant().Contains(filterText) ||
                               entry.Sound.ToLowerInvariant().Contains(filterText);
                    }
                    return false;
                };
            }

            _filteredEntries.View.Refresh();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:387-392
        // Original: def toggle_filter_box(self):
        public void ToggleFilterBox()
        {
            if (_searchBox != null)
            {
                _searchBox.IsVisible = !_searchBox.IsVisible;
                if (_searchBox.IsVisible && _searchEdit != null)
                {
                    _searchEdit.Focus();
                    _searchEdit.SelectAll();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:394-399
        // Original: def toggle_goto_box(self):
        public void ToggleGotoBox()
        {
            if (_jumpBox != null)
            {
                _jumpBox.IsVisible = !_jumpBox.IsVisible;
                if (_jumpBox.IsVisible && _jumpSpinbox != null)
                {
                    _jumpSpinbox.Focus();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:401-427
        // Original: def selection_changed(self):
        private void SelectionChanged()
        {
            if (_talkTable == null)
            {
                return;
            }

            _selectedEntry = _talkTable.SelectedItem as TLKEntryViewModel;

            if (_selectedEntry == null)
            {
                if (_textEdit != null)
                {
                    _textEdit.IsEnabled = false;
                }
                if (_soundEdit != null)
                {
                    _soundEdit.IsEnabled = false;
                }
                return;
            }

            if (_textEdit != null)
            {
                _textEdit.IsEnabled = true;
                _textEdit.Text = _selectedEntry.Text;
            }
            if (_soundEdit != null)
            {
                _soundEdit.IsEnabled = true;
                _soundEdit.Text = _selectedEntry.Sound;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/tlk.py:429-444
        // Original: def update_entry(self):
        private void UpdateEntry()
        {
            if (_selectedEntry == null)
            {
                return;
            }

            if (_textEdit != null)
            {
                _selectedEntry.Text = _textEdit.Text;
            }
            if (_soundEdit != null)
            {
                _selectedEntry.Sound = _soundEdit.Text;
            }
        }

        public override void SaveAs()
        {
            Save();
        }

        public Language Language => _language;
    }
}
