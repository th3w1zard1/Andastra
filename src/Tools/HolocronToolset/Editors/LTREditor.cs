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
using Avalonia.Media;
using Avalonia.Styling;
using BioWare.NET.Resource.Formats.LTR;
using BioWare.NET.Resource;
using HolocronToolset.Common;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:28
    // Original: class LTREditor(Editor):
    public partial class LTREditor : Editor
    {
        private LTR _ltr;
        private bool _autoResizeEnabled;
        private bool _alternateRowColorsEnabled;
        private Style _alternatingRowStyle;

        // Data collections for tables
        private ObservableCollection<List<string>> _singlesData;
        private ObservableCollection<List<string>> _doublesData;
        private ObservableCollection<List<string>> _triplesData;

        // UI controls
        private DataGrid _tableSingles;
        private DataGrid _tableDoubles;
        private DataGrid _tableTriples;
        private ComboBox _comboBoxSingleChar;
        private ComboBox _comboBoxDoubleChar;
        private ComboBox _comboBoxDoublePrevChar;
        private ComboBox _comboBoxTripleChar;
        private ComboBox _comboBoxTriplePrev1Char;
        private ComboBox _comboBoxTriplePrev2Char;
        private NumericUpDown _spinBoxSingleStart;
        private NumericUpDown _spinBoxSingleMiddle;
        private NumericUpDown _spinBoxSingleEnd;
        private NumericUpDown _spinBoxDoubleStart;
        private NumericUpDown _spinBoxDoubleMiddle;
        private NumericUpDown _spinBoxDoubleEnd;
        private NumericUpDown _spinBoxTripleStart;
        private NumericUpDown _spinBoxTripleMiddle;
        private NumericUpDown _spinBoxTripleEnd;
        private Button _buttonSetSingle;
        private Button _buttonSetDouble;
        private Button _buttonSetTriple;
        private Button _buttonGenerate;
        private Button _buttonAddSingle;
        private Button _buttonRemoveSingle;
        private Button _buttonAddDouble;
        private Button _buttonRemoveDouble;
        private Button _buttonAddTriple;
        private Button _buttonRemoveTriple;
        private TextBox _lineEditGeneratedName;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:29-54
        // Original: def __init__(self, parent, installation):
        public LTREditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "LTR Editor", "ltr", new[] { ResourceType.LTR }, new[] { ResourceType.LTR }, installation)
        {
            InitializeComponent();
            SetupUI();
            SetupSignals();
            Width = 800;
            Height = 600;

            _ltr = new LTR();
            _autoResizeEnabled = true;
            _singlesData = new ObservableCollection<List<string>>();
            _doublesData = new ObservableCollection<List<string>>();
            _triplesData = new ObservableCollection<List<string>>();
            PopulateComboBoxes();
            SetupTableSorting();
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

            // Create tab control for singles, doubles, triples
            var tabControl = new TabControl();
            var singlesTab = new TabItem { Header = "Singles" };
            var doublesTab = new TabItem { Header = "Doubles" };
            var triplesTab = new TabItem { Header = "Triples" };

            // Singles table
            _tableSingles = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                SelectionMode = DataGridSelectionMode.Extended
            };
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "Char", Binding = new Binding("[0]") });
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "Start", Binding = new Binding("[1]") });
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "Middle", Binding = new Binding("[2]") });
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "End", Binding = new Binding("[3]") });
            _tableSingles.ItemsSource = _singlesData;
            singlesTab.Content = _tableSingles;

            // Doubles table
            _tableDoubles = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                SelectionMode = DataGridSelectionMode.Extended
            };
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Prev", Binding = new Binding("[0]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Char", Binding = new Binding("[1]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Start", Binding = new Binding("[2]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Middle", Binding = new Binding("[3]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "End", Binding = new Binding("[4]") });
            _tableDoubles.ItemsSource = _doublesData;
            doublesTab.Content = _tableDoubles;

            // Triples table
            _tableTriples = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true,
                SelectionMode = DataGridSelectionMode.Extended
            };
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Prev2", Binding = new Binding("[0]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Prev1", Binding = new Binding("[1]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Char", Binding = new Binding("[2]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Start", Binding = new Binding("[3]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Middle", Binding = new Binding("[4]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "End", Binding = new Binding("[5]") });
            _tableTriples.ItemsSource = _triplesData;
            triplesTab.Content = _tableTriples;

            tabControl.ItemsSource = new[] { singlesTab, doublesTab, triplesTab };

            // Controls panel
            var controlsPanel = new StackPanel { Orientation = Orientation.Vertical };
            _comboBoxSingleChar = new ComboBox();
            _spinBoxSingleStart = new NumericUpDown { Minimum = 0, Maximum = 1, Increment = 0.01m };
            _spinBoxSingleMiddle = new NumericUpDown { Minimum = 0, Maximum = 1, Increment = 0.01m };
            _spinBoxSingleEnd = new NumericUpDown { Minimum = 0, Maximum = 1, Increment = 0.01m };
            _buttonSetSingle = new Button { Content = "Set Single" };
            controlsPanel.Children.Add(new TextBlock { Text = "Single Character:" });
            controlsPanel.Children.Add(_comboBoxSingleChar);
            controlsPanel.Children.Add(new TextBlock { Text = "Start:" });
            controlsPanel.Children.Add(_spinBoxSingleStart);
            controlsPanel.Children.Add(new TextBlock { Text = "Middle:" });
            controlsPanel.Children.Add(_spinBoxSingleMiddle);
            controlsPanel.Children.Add(new TextBlock { Text = "End:" });
            controlsPanel.Children.Add(_spinBoxSingleEnd);
            controlsPanel.Children.Add(_buttonSetSingle);

            // Generate name
            _lineEditGeneratedName = new TextBox { IsReadOnly = true };
            _buttonGenerate = new Button { Content = "Generate Name" };
            controlsPanel.Children.Add(_buttonGenerate);
            controlsPanel.Children.Add(_lineEditGeneratedName);

            var splitter = new Grid();
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            splitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(tabControl, 0);
            Grid.SetColumn(controlsPanel, 1);
            splitter.Children.Add(tabControl);
            splitter.Children.Add(controlsPanel);

            mainPanel.Children.Add(splitter);
            Content = mainPanel;
        }

        private void SetupUI()
        {
            // Try to find controls from XAML if available
            _tableSingles = EditorHelpers.FindControlSafe<DataGrid>(this, "TableSingles");
            _tableDoubles = EditorHelpers.FindControlSafe<DataGrid>(this, "TableDoubles");
            _tableTriples = EditorHelpers.FindControlSafe<DataGrid>(this, "TableTriples");
            
            // Initialize data collections if tables exist
            if (_tableSingles != null && _tableSingles.ItemsSource == null)
            {
                _tableSingles.ItemsSource = _singlesData;
            }
            if (_tableDoubles != null && _tableDoubles.ItemsSource == null)
            {
                _tableDoubles.ItemsSource = _doublesData;
            }
            if (_tableTriples != null && _tableTriples.ItemsSource == null)
            {
                _tableTriples.ItemsSource = _triplesData;
            }
            _comboBoxSingleChar = EditorHelpers.FindControlSafe<ComboBox>(this, "ComboBoxSingleChar");
            _comboBoxDoubleChar = EditorHelpers.FindControlSafe<ComboBox>(this, "ComboBoxDoubleChar");
            _comboBoxDoublePrevChar = EditorHelpers.FindControlSafe<ComboBox>(this, "ComboBoxDoublePrevChar");
            _comboBoxTripleChar = EditorHelpers.FindControlSafe<ComboBox>(this, "ComboBoxTripleChar");
            _comboBoxTriplePrev1Char = EditorHelpers.FindControlSafe<ComboBox>(this, "ComboBoxTriplePrev1Char");
            _comboBoxTriplePrev2Char = EditorHelpers.FindControlSafe<ComboBox>(this, "ComboBoxTriplePrev2Char");
            _spinBoxSingleStart = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxSingleStart");
            _spinBoxSingleMiddle = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxSingleMiddle");
            _spinBoxSingleEnd = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxSingleEnd");
            _spinBoxDoubleStart = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxDoubleStart");
            _spinBoxDoubleMiddle = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxDoubleMiddle");
            _spinBoxDoubleEnd = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxDoubleEnd");
            _spinBoxTripleStart = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxTripleStart");
            _spinBoxTripleMiddle = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxTripleMiddle");
            _spinBoxTripleEnd = EditorHelpers.FindControlSafe<NumericUpDown>(this, "SpinBoxTripleEnd");
            _buttonSetSingle = EditorHelpers.FindControlSafe<Button>(this, "ButtonSetSingle");
            _buttonSetDouble = EditorHelpers.FindControlSafe<Button>(this, "ButtonSetDouble");
            _buttonSetTriple = EditorHelpers.FindControlSafe<Button>(this, "ButtonSetTriple");
            _buttonGenerate = EditorHelpers.FindControlSafe<Button>(this, "ButtonGenerate");
            _buttonAddSingle = EditorHelpers.FindControlSafe<Button>(this, "ButtonAddSingle");
            _buttonRemoveSingle = EditorHelpers.FindControlSafe<Button>(this, "ButtonRemoveSingle");
            _buttonAddDouble = EditorHelpers.FindControlSafe<Button>(this, "ButtonAddDouble");
            _buttonRemoveDouble = EditorHelpers.FindControlSafe<Button>(this, "ButtonRemoveDouble");
            _buttonAddTriple = EditorHelpers.FindControlSafe<Button>(this, "ButtonAddTriple");
            _buttonRemoveTriple = EditorHelpers.FindControlSafe<Button>(this, "ButtonRemoveTriple");
            _lineEditGeneratedName = EditorHelpers.FindControlSafe<TextBox>(this, "LineEditGeneratedName");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:56-78
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_buttonSetSingle != null)
            {
                _buttonSetSingle.Click += (s, e) => SetSingleCharacter();
            }
            if (_buttonSetDouble != null)
            {
                _buttonSetDouble.Click += (s, e) => SetDoubleCharacter();
            }
            if (_buttonSetTriple != null)
            {
                _buttonSetTriple.Click += (s, e) => SetTripleCharacter();
            }
            if (_buttonGenerate != null)
            {
                _buttonGenerate.Click += (s, e) => GenerateName();
            }
            if (_buttonAddSingle != null)
            {
                _buttonAddSingle.Click += (s, e) => AddSingleRow();
            }
            if (_buttonRemoveSingle != null)
            {
                _buttonRemoveSingle.Click += (s, e) => RemoveSingleRow();
            }
            if (_buttonAddDouble != null)
            {
                _buttonAddDouble.Click += (s, e) => AddDoubleRow();
            }
            if (_buttonRemoveDouble != null)
            {
                _buttonRemoveDouble.Click += (s, e) => RemoveDoubleRow();
            }
            if (_buttonAddTriple != null)
            {
                _buttonAddTriple.Click += (s, e) => AddTripleRow();
            }
            if (_buttonRemoveTriple != null)
            {
                _buttonRemoveTriple.Click += (s, e) => RemoveTripleRow();
            }
            
            // Setup header context menus for tables
            SetupHeaderContextMenus();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:37-39
        // Original: self.ui.tableSingles.setSortingEnabled(True)
        private void SetupTableSorting()
        {
            if (_tableSingles != null)
            {
                _tableSingles.CanUserSortColumns = true;
            }
            if (_tableDoubles != null)
            {
                _tableDoubles.CanUserSortColumns = true;
            }
            if (_tableTriples != null)
            {
                _tableTriples.CanUserSortColumns = true;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:67-78
        // Original: hor_header.customContextMenuRequested.connect(self.show_header_context_menu)
        private void SetupHeaderContextMenus()
        {
            // Setup context menu for each table
            SetupTableContextMenu(_tableSingles);
            SetupTableContextMenu(_tableDoubles);
            SetupTableContextMenu(_tableTriples);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:124-148
        // Original: def show_header_context_menu(self, position: QPoint):
        private void SetupTableContextMenu(DataGrid table)
        {
            if (table == null)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItems = new List<MenuItem>();

            // "Auto-fit Columns" menu item (checkable)
            // Note: Avalonia MenuItem doesn't have IsChecked property like Qt QAction
            // The functionality still works, but we can't show the checked state in the menu
            var autoFitItem = new MenuItem
            {
                Header = "Auto-fit Columns"
            };
            autoFitItem.Click += (sender, e) =>
            {
                ToggleAutoFitColumns(!_autoResizeEnabled);
            };
            menuItems.Add(autoFitItem);

            // "Toggle Alternate Row Colors" menu item
            var alternateRowColorsItem = new MenuItem
            {
                Header = "Toggle Alternate Row Colors"
            };
            alternateRowColorsItem.Click += (sender, e) =>
            {
                ToggleAlternateRowColors();
            };
            menuItems.Add(alternateRowColorsItem);

            contextMenu.ItemsSource = menuItems;
            table.ContextMenu = contextMenu;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:80-88
        // Original: def populateComboBoxes(self):
        private void PopulateComboBoxes()
        {
            string charSet = LTR.CharacterSet;
            var charList = charSet.Select(c => c.ToString()).ToList();

            if (_comboBoxSingleChar != null)
            {
                _comboBoxSingleChar.ItemsSource = charList;
            }
            if (_comboBoxDoubleChar != null)
            {
                _comboBoxDoubleChar.ItemsSource = charList;
            }
            if (_comboBoxDoublePrevChar != null)
            {
                _comboBoxDoublePrevChar.ItemsSource = charList;
            }
            if (_comboBoxTripleChar != null)
            {
                _comboBoxTripleChar.ItemsSource = charList;
            }
            if (_comboBoxTriplePrev1Char != null)
            {
                _comboBoxTriplePrev1Char.ItemsSource = charList;
            }
            if (_comboBoxTriplePrev2Char != null)
            {
                _comboBoxTriplePrev2Char.ItemsSource = charList;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:90-122
        // Original: def updateUIFromLTR(self):
        private void UpdateUIFromLTR()
        {
            string charSet = LTR.CharacterSet;
            
            // Clear existing data
            _singlesData.Clear();
            _doublesData.Clear();
            _triplesData.Clear();

            // Singles
            foreach (char c in charSet)
            {
                string charStr = c.ToString();
                _singlesData.Add(new List<string>
                {
                    charStr,
                    _ltr.GetSinglesStart(charStr).ToString("F4"),
                    _ltr.GetSinglesMiddle(charStr).ToString("F4"),
                    _ltr.GetSinglesEnd(charStr).ToString("F4")
                });
            }

            // Doubles
            foreach (char prevChar in charSet)
            {
                foreach (char c in charSet)
                {
                    string prevStr = prevChar.ToString();
                    string charStr = c.ToString();
                    _doublesData.Add(new List<string>
                    {
                        prevStr,
                        charStr,
                        _ltr.GetDoublesStart(prevStr, charStr).ToString("F4"),
                        _ltr.GetDoublesMiddle(prevStr, charStr).ToString("F4"),
                        _ltr.GetDoublesEnd(prevStr, charStr).ToString("F4")
                    });
                }
            }

            // Triples
            foreach (char prev2Char in charSet)
            {
                foreach (char prev1Char in charSet)
                {
                    foreach (char c in charSet)
                    {
                        string prev2Str = prev2Char.ToString();
                        string prev1Str = prev1Char.ToString();
                        string charStr = c.ToString();
                        _triplesData.Add(new List<string>
                        {
                            prev2Str,
                            prev1Str,
                            charStr,
                            _ltr.GetTriplesStart(prev2Str, prev1Str, charStr).ToString("F4"),
                            _ltr.GetTriplesMiddle(prev2Str, prev1Str, charStr).ToString("F4"),
                            _ltr.GetTriplesEnd(prev2Str, prev1Str, charStr).ToString("F4")
                        });
                    }
                }
            }
            
            // Auto-fit columns if enabled
            if (_autoResizeEnabled)
            {
                AutoFitColumns();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:200-208
        // Original: def setSingleCharacter(self):
        private void SetSingleCharacter()
        {
            if (_comboBoxSingleChar?.SelectedItem is string char_ &&
                _spinBoxSingleStart?.Value.HasValue == true &&
                _spinBoxSingleMiddle?.Value.HasValue == true &&
                _spinBoxSingleEnd?.Value.HasValue == true)
            {
                _ltr.SetSinglesStart(char_, (float)_spinBoxSingleStart.Value.Value);
                _ltr.SetSinglesMiddle(char_, (float)_spinBoxSingleMiddle.Value.Value);
                _ltr.SetSinglesEnd(char_, (float)_spinBoxSingleEnd.Value.Value);
                UpdateUIFromLTR();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:210-219
        // Original: def setDoubleCharacter(self):
        private void SetDoubleCharacter()
        {
            if (_comboBoxDoublePrevChar?.SelectedItem is string prevChar &&
                _comboBoxDoubleChar?.SelectedItem is string char_ &&
                _spinBoxDoubleStart?.Value.HasValue == true &&
                _spinBoxDoubleMiddle?.Value.HasValue == true &&
                _spinBoxDoubleEnd?.Value.HasValue == true)
            {
                _ltr.SetDoublesStart(prevChar, char_, (float)_spinBoxDoubleStart.Value.Value);
                _ltr.SetDoublesMiddle(prevChar, char_, (float)_spinBoxDoubleMiddle.Value.Value);
                _ltr.SetDoublesEnd(prevChar, char_, (float)_spinBoxDoubleEnd.Value.Value);
                UpdateUIFromLTR();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:221-231
        // Original: def setTripleCharacter(self):
        private void SetTripleCharacter()
        {
            if (_comboBoxTriplePrev2Char?.SelectedItem is string prev2Char &&
                _comboBoxTriplePrev1Char?.SelectedItem is string prev1Char &&
                _comboBoxTripleChar?.SelectedItem is string char_ &&
                _spinBoxTripleStart?.Value.HasValue == true &&
                _spinBoxTripleMiddle?.Value.HasValue == true &&
                _spinBoxTripleEnd?.Value.HasValue == true)
            {
                _ltr.SetTriplesStart(prev2Char, prev1Char, char_, (float)_spinBoxTripleStart.Value.Value);
                _ltr.SetTriplesMiddle(prev2Char, prev1Char, char_, (float)_spinBoxTripleMiddle.Value.Value);
                _ltr.SetTriplesEnd(prev2Char, prev1Char, char_, (float)_spinBoxTripleEnd.Value.Value);
                UpdateUIFromLTR();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:233-235
        // Original: def generateName(self):
        private void GenerateName()
        {
            string generatedName = _ltr.Generate();
            if (_lineEditGeneratedName != null)
            {
                _lineEditGeneratedName.Text = generatedName;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:237-240
        // Original: def addSingleRow(self):
        private void AddSingleRow()
        {
            // In Python, this adds a row to the table widget
            // Since our table is bound to LTR data, we'll add an empty row to the collection
            if (_singlesData != null)
            {
                _singlesData.Add(new List<string> { "", "0.0000", "0.0000", "0.0000" });
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:241-246
        // Original: def removeSingleRow(self):
        private void RemoveSingleRow()
        {
            if (_tableSingles?.SelectedItems == null || _singlesData == null)
            {
                return;
            }
            
            // Get selected items and remove them in reverse order to maintain indices
            var selectedItems = _tableSingles.SelectedItems.Cast<List<string>>().ToList();
            foreach (var item in selectedItems)
            {
                _singlesData.Remove(item);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:248-250
        // Original: def addDoubleRow(self):
        private void AddDoubleRow()
        {
            // In Python, this adds a row to the table widget
            if (_doublesData != null)
            {
                _doublesData.Add(new List<string> { "", "", "0.0000", "0.0000", "0.0000" });
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:252-257
        // Original: def removeDoubleRow(self):
        private void RemoveDoubleRow()
        {
            if (_tableDoubles?.SelectedItems == null || _doublesData == null)
            {
                return;
            }
            
            // Get selected items and remove them in reverse order to maintain indices
            var selectedItems = _tableDoubles.SelectedItems.Cast<List<string>>().ToList();
            foreach (var item in selectedItems)
            {
                _doublesData.Remove(item);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:259-261
        // Original: def addTripleRow(self):
        private void AddTripleRow()
        {
            // In Python, this adds a row to the table widget
            if (_triplesData != null)
            {
                _triplesData.Add(new List<string> { "", "", "", "0.0000", "0.0000", "0.0000" });
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:263-268
        // Original: def removeTripleRow(self):
        private void RemoveTripleRow()
        {
            if (_tableTriples?.SelectedItems == null || _triplesData == null)
            {
                return;
            }
            
            // Get selected items and remove them in reverse order to maintain indices
            var selectedItems = _tableTriples.SelectedItems.Cast<List<string>>().ToList();
            foreach (var item in selectedItems)
            {
                _triplesData.Remove(item);
            }
        }
        
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:150-156
        // Original: def toggle_alternate_row_colors(self):
        // Python implementation: Toggles setAlternatingRowColors() on QTableWidget
        // Avalonia implementation: Uses Style with :nth-child(2n) selector to target even rows
        public void ToggleAlternateRowColors()
        {
            _alternateRowColorsEnabled = !_alternateRowColorsEnabled;

            // Create alternating row style if it doesn't exist
            if (_alternatingRowStyle == null)
            {
                // Style targets DataGridRow elements that are even children (2n = every 2nd element)
                // Based on Avalonia Style API: https://docs.avaloniaui.net/docs/guides/styles-and-resources/style-selector-syntax
                _alternatingRowStyle = new Style(x => x.OfType<DataGridRow>().NthChild(2, 0))
                {
                    Setters =
                    {
                        new Setter(DataGridRow.BackgroundProperty, new SolidColorBrush(Color.FromRgb(240, 240, 240)))
                    }
                };
            }

            foreach (var table in new[] { _tableSingles, _tableDoubles, _tableTriples })
            {
                if (table != null)
                {
                    if (_alternateRowColorsEnabled)
                    {
                        // Add alternating row style
                        if (!table.Styles.Contains(_alternatingRowStyle))
                        {
                            table.Styles.Add(_alternatingRowStyle);
                        }
                    }
                    else
                    {
                        // Remove alternating row style
                        table.Styles.Remove(_alternatingRowStyle);
                    }
                }
            }
        }
        
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:157-163
        // Original: def reset_column_widths(self):
        private void ResetColumnWidths()
        {
            foreach (var table in new[] { _tableSingles, _tableDoubles, _tableTriples })
            {
                if (table != null)
                {
                    foreach (var column in table.Columns)
                    {
                        column.Width = DataGridLength.Auto;
                    }
                }
            }
        }
        
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:164-186
        // Original: def auto_fit_columns(self, table: QTableWidget):
        private void AutoFitColumns(DataGrid table = null)
        {
            var tables = table != null ? new[] { table } : new[] { _tableSingles, _tableDoubles, _tableTriples };
            
            foreach (var tbl in tables)
            {
                if (tbl == null) continue;
                
                // Resize columns to fit content
                // Note: Avalonia DataGrid doesn't have SizeToCells, so we use Auto which sizes to content
                foreach (var column in tbl.Columns)
                {
                    column.Width = DataGridLength.Auto;
                }
            }
        }
        
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:187-199
        // Original: def toggle_auto_fit_columns(self, state: bool | None = None):
        public void ToggleAutoFitColumns(bool? state = null)
        {
            _autoResizeEnabled = state.HasValue ? state.Value : !_autoResizeEnabled;
            
            if (_autoResizeEnabled)
            {
                AutoFitColumns();
            }
            else
            {
                ResetColumnWidths();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:270-289
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            if (data == null || data.Length == 0)
            {
                _ltr = new LTR();
                UpdateUIFromLTR();
                return;
            }
            try
            {
                _ltr = LTRAuto.ReadLtr(data, 0, null);
                UpdateUIFromLTR();
                ToggleAutoFitColumns(true);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load LTR: {ex}");
                _ltr = new LTR();
                UpdateUIFromLTR();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:282-283
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            byte[] data = LTRAuto.BytesLtr(_ltr);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:285-289
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _ltr = new LTR();
            if (_lineEditGeneratedName != null)
            {
                _lineEditGeneratedName.Text = "";
            }
            UpdateUIFromLTR();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
