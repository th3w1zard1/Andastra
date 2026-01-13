using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource;
using HolocronToolset.Common;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:31
    // Original: class TwoDAEditor(Editor):
    public partial class TwoDAEditor : Editor
    {
        private ObservableCollection<ObservableCollection<string>> _sourceData;
        private CollectionViewSource _filteredData;
        private DataGrid _twodaTable;
        private TextBox _filterEdit;
        private Panel _filterBox;
        private VerticalHeaderOption _verticalHeaderOption;
        private string _verticalHeaderColumn;
        private List<string> _columnHeaders; // Store column headers separately

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:32-64
        // Original: def __init__(self, parent, installation):
        public TwoDAEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "2DA Editor", "none",
                new[] { ResourceType.TwoDA, ResourceType.TwoDA_CSV, ResourceType.TwoDA_JSON },
                new[] { ResourceType.TwoDA, ResourceType.TwoDA_CSV, ResourceType.TwoDA_JSON },
                installation)
        {
            _sourceData = new ObservableCollection<ObservableCollection<string>>();
            _filteredData = new CollectionViewSource { Source = _sourceData };
            _verticalHeaderOption = VerticalHeaderOption.None;
            _verticalHeaderColumn = "";
            _columnHeaders = new List<string>();

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

            // Filter box
            var filterBoxPanel = new StackPanel { Orientation = Orientation.Horizontal, IsVisible = false };
            _filterEdit = new TextBox { Watermark = "Filter..." };
            var filterButton = new Button { Content = "Filter" };
            filterButton.Click += (s, e) => DoFilter(_filterEdit?.Text ?? "");
            filterBoxPanel.Children.Add(_filterEdit);
            filterBoxPanel.Children.Add(filterButton);
            _filterBox = filterBoxPanel;
            mainPanel.Children.Add(_filterBox);

            // Table
            _twodaTable = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                SelectionMode = DataGridSelectionMode.Extended
            };
            mainPanel.Children.Add(_twodaTable);

            Content = mainPanel;
        }

        private void SetupUI()
        {
            // If controls are already initialized (e.g., by SetupProgrammaticUI), skip control finding
            if (_twodaTable != null && _filterEdit != null && _filterBox != null)
            {
                if (_twodaTable.ItemsSource == null)
                {
                    _twodaTable.ItemsSource = _filteredData.View;
                }
                return;
            }

            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // Try to find controls from XAML if available
                _twodaTable = this.FindControl<DataGrid>("twodaTable");
                _filterEdit = this.FindControl<TextBox>("filterEdit");
                var filterBoxExpander = this.FindControl<Expander>("filterBox");
                if (filterBoxExpander != null)
                {
                    // Find the content panel inside the expander
                    var content = filterBoxExpander.Content as Panel;
                    if (content != null)
                    {
                        _filterBox = content;
                    }
                }
            }
            catch
            {
                // XAML controls not available - controls should already be initialized by SetupProgrammaticUI
                // If not, ensure minimal setup
                if (_twodaTable == null)
                {
                    _twodaTable = new DataGrid
                    {
                        AutoGenerateColumns = false,
                        CanUserReorderColumns = false,
                        CanUserResizeColumns = true,
                        SelectionMode = DataGridSelectionMode.Extended
                    };
                }
                if (_filterEdit == null)
                {
                    _filterEdit = new TextBox { Watermark = "Filter..." };
                }
                if (_filterBox == null)
                {
                    _filterBox = new StackPanel { Orientation = Orientation.Horizontal };
                }
            }

            if (_twodaTable != null)
            {
                if (_twodaTable.ItemsSource == null)
                {
                    _twodaTable.ItemsSource = _filteredData.View;
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:117-126
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_filterEdit != null)
            {
                _filterEdit.TextChanged += (s, e) => DoFilter(_filterEdit.Text);
            }

            if (_twodaTable != null)
            {
                _twodaTable.SelectionChanged += (s, e) => { /* Handle selection */ };
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:128-148
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            try
            {
                LoadMain(data);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load file: {ex}");
                New();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:150-179
        // Original: def _load_main(self, data):
        private void LoadMain(byte[] data)
        {
            var reader = new TwoDABinaryReader(data);
            var twoda = reader.Load();
            var headers = new List<string> { "" };
            headers.AddRange(twoda.GetHeaders());

            _sourceData.Clear();
            _columnHeaders.Clear();
            if (_twodaTable != null)
            {
                _twodaTable.Columns.Clear();
            }

            // Store column headers
            _columnHeaders.AddRange(twoda.GetHeaders());

            // Create columns in DataGrid if available
            if (_twodaTable != null)
            {
                foreach (var header in headers)
                {
                    _twodaTable.Columns.Add(new DataGridTextColumn
                    {
                        Header = header,
                        Binding = new Binding($"[{_twodaTable.Columns.Count}]"),
                        IsReadOnly = false
                    });
                }
            }

            // Load rows
            for (int i = 0; i < twoda.GetHeight(); i++)
            {
                var row = new ObservableCollection<string> { twoda.GetLabel(i) };
                foreach (var header in twoda.GetHeaders())
                {
                    row.Add(twoda.GetCellString(i, header) ?? "");
                }
                _sourceData.Add(row);
            }

            ResetVerticalHeaders();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:203-218
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            var twoda = new TwoDA();

            // Get column headers from stored headers or DataGrid columns
            List<string> headers = new List<string>();
            if (_columnHeaders.Count > 0)
            {
                headers.AddRange(_columnHeaders);
            }
            else if (_twodaTable != null && _twodaTable.Columns.Count > 1)
            {
                // Extract headers from DataGrid columns (skip first column which is label)
                for (int i = 1; i < _twodaTable.Columns.Count; i++)
                {
                    string header = _twodaTable.Columns[i].Header?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(header))
                    {
                        headers.Add(header);
                    }
                }
            }
            else if (_sourceData.Count > 0 && _sourceData[0].Count > 1)
            {
                // Fallback: generate column names from data structure
                for (int i = 1; i < _sourceData[0].Count; i++)
                {
                    headers.Add($"Column{i}");
                }
            }

            // Add columns to 2DA
            foreach (var header in headers)
            {
                twoda.AddColumn(header);
            }

            // Add rows
            foreach (var row in _sourceData)
            {
                if (row.Count > 0)
                {
                    int rowIndex = twoda.AddRow();
                    twoda.SetLabel(rowIndex, row[0] ?? "");
                    // Add cell values (skip index 0 which is the label)
                    for (int j = 1; j < row.Count && j - 1 < headers.Count; j++)
                    {
                        twoda.SetCellString(rowIndex, headers[j - 1], row[j] ?? "");
                    }
                }
            }

            ResourceType twodaType = _restype ?? ResourceType.TwoDA;
            byte[] data = TwoDAAuto.Bytes2DA(twoda, twodaType);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:220-224
        // Original: def new(self):
        public override void New()
        {
            base.New();
            // Set default restype to 2DA for new files
            _restype = ResourceType.TwoDA;
            _sourceData.Clear();
            _columnHeaders.Clear();
            if (_twodaTable != null)
            {
                _twodaTable.Columns.Clear();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:239-243
        // Original: def do_filter(self, text):
        public void DoFilter(string text)
        {
            if (_filteredData.View == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                _filteredData.View.Filter = null;
            }
            else
            {
                string filterText = text.ToLowerInvariant();
                _filteredData.View.Filter = item =>
                {
                    if (item is ObservableCollection<string> row)
                    {
                        return row.Any(cell => cell?.ToLowerInvariant().Contains(filterText) ?? false);
                    }
                    return false;
                };
            }

            _filteredData.View.Refresh();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:245-253
        // Original: def toggle_filter(self):
        public void ToggleFilter()
        {
            if (_filterBox != null)
            {
                _filterBox.IsVisible = !_filterBox.IsVisible;
                if (_filterBox.IsVisible && _filterEdit != null)
                {
                    _filterEdit.Focus();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:255-280
        // Original: def copy_selection(self):
        public void CopySelection()
        {
            // Copy selected cells to clipboard
            // Implementation will be added when clipboard support is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:282-303
        // Original: def paste_selection(self):
        public void PasteSelection()
        {
            // Paste from clipboard
            // Implementation will be added when clipboard support is available
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:305-308
        // Original: def insert_row(self):
        public void InsertRow()
        {
            int columnCount = _twodaTable.Columns.Count;
            var newRow = new ObservableCollection<string>();
            for (int i = 0; i < columnCount; i++)
            {
                newRow.Add("");
            }
            _sourceData.Add(newRow);
            SetItemDisplayData(_sourceData.Count - 1);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:310-316
        // Original: def duplicate_row(self):
        public void DuplicateRow()
        {
            if (_twodaTable.SelectedItem is ObservableCollection<string> selectedRow)
            {
                var newRow = new ObservableCollection<string>(selectedRow);
                _sourceData.Add(newRow);
                SetItemDisplayData(_sourceData.Count - 1);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:318-324
        // Original: def set_item_display_data(self, rowIndex):
        private void SetItemDisplayData(int rowIndex)
        {
            if (rowIndex >= 0 && rowIndex < _sourceData.Count && _sourceData[rowIndex].Count > 0)
            {
                if (string.IsNullOrEmpty(_sourceData[rowIndex][0]))
                {
                    _sourceData[rowIndex][0] = rowIndex.ToString();
                }
            }
            ResetVerticalHeaders();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:326-330
        // Original: def remove_selected_rows(self):
        public void RemoveSelectedRows()
        {
            var selectedItems = _twodaTable.SelectedItems?.Cast<ObservableCollection<string>>().ToList() ?? new List<ObservableCollection<string>>();
            foreach (var item in selectedItems)
            {
                _sourceData.Remove(item);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:332-335
        // Original: def redo_row_labels(self):
        public void RedoRowLabels()
        {
            for (int i = 0; i < _sourceData.Count; i++)
            {
                if (_sourceData[i].Count > 0)
                {
                    _sourceData[i][0] = i.ToString();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:337-344
        // Original: def set_vertical_header_option(self, option, column=None):
        public void SetVerticalHeaderOption(VerticalHeaderOption option, string column = null)
        {
            _verticalHeaderOption = option;
            _verticalHeaderColumn = column ?? "";
            ResetVerticalHeaders();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:346-420
        // Original: def reset_vertical_headers(self):
        private void ResetVerticalHeaders()
        {
            // Vertical header implementation will be added when DataGrid vertical header support is available
        }

        public override void SaveAs()
        {
            Save();
        }
    }

    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/twoda.py:454-458
    // Original: class VerticalHeaderOption(IntEnum):
    public enum VerticalHeaderOption
    {
        RowIndex = 0,
        RowLabel = 1,
        CellValue = 2,
        None = 3
    }
}
