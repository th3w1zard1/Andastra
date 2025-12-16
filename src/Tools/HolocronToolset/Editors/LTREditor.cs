using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing.Formats.LTR;
using Andastra.Parsing.Resources;
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
            PopulateComboBoxes();
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
                CanUserResizeColumns = true
            };
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "Char", Binding = new Binding("[0]") });
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "Start", Binding = new Binding("[1]") });
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "Middle", Binding = new Binding("[2]") });
            _tableSingles.Columns.Add(new DataGridTextColumn { Header = "End", Binding = new Binding("[3]") });
            singlesTab.Content = _tableSingles;

            // Doubles table
            _tableDoubles = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true
            };
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Prev", Binding = new Binding("[0]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Char", Binding = new Binding("[1]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Start", Binding = new Binding("[2]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "Middle", Binding = new Binding("[3]") });
            _tableDoubles.Columns.Add(new DataGridTextColumn { Header = "End", Binding = new Binding("[4]") });
            doublesTab.Content = _tableDoubles;

            // Triples table
            _tableTriples = new DataGrid
            {
                AutoGenerateColumns = false,
                CanUserReorderColumns = false,
                CanUserResizeColumns = true
            };
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Prev2", Binding = new Binding("[0]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Prev1", Binding = new Binding("[1]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Char", Binding = new Binding("[2]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Start", Binding = new Binding("[3]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "Middle", Binding = new Binding("[4]") });
            _tableTriples.Columns.Add(new DataGridTextColumn { Header = "End", Binding = new Binding("[5]") });
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
            var singlesData = new List<List<string>>();
            var doublesData = new List<List<string>>();
            var triplesData = new List<List<string>>();

            // Singles
            foreach (char c in charSet)
            {
                string charStr = c.ToString();
                singlesData.Add(new List<string>
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
                    doublesData.Add(new List<string>
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
                        triplesData.Add(new List<string>
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

            if (_tableSingles != null)
            {
                _tableSingles.ItemsSource = singlesData;
            }
            if (_tableDoubles != null)
            {
                _tableDoubles.ItemsSource = doublesData;
            }
            if (_tableTriples != null)
            {
                _tableTriples.ItemsSource = triplesData;
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
            // Adding rows is handled by UpdateUIFromLTR - rows are generated from LTR data
            UpdateUIFromLTR();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:241-246
        // Original: def removeSingleRow(self):
        private void RemoveSingleRow()
        {
            // Removing rows is not applicable - rows are generated from LTR data
            // This would require modifying the LTR structure itself
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:248-250
        // Original: def addDoubleRow(self):
        private void AddDoubleRow()
        {
            // Adding rows is handled by UpdateUIFromLTR
            UpdateUIFromLTR();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:252-257
        // Original: def removeDoubleRow(self):
        private void RemoveDoubleRow()
        {
            // Removing rows is not applicable
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:259-261
        // Original: def addTripleRow(self):
        private void AddTripleRow()
        {
            // Adding rows is handled by UpdateUIFromLTR
            UpdateUIFromLTR();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ltr.py:263-268
        // Original: def removeTripleRow(self):
        private void RemoveTripleRow()
        {
            // Removing rows is not applicable
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
