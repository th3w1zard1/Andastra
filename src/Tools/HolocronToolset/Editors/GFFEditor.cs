using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using System.Numerics;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using HolocronToolset.Common;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:47
    // Original: class GFFEditor(Editor):
    public partial class GFFEditor : Editor
    {
        private GFF _gff;
        private TreeView _treeView;
        private Panel _fieldBox;
        private ComboBox _typeCombo;
        private TextBox _labelEdit;
        private Panel _pages;
        private NumericUpDown _intSpin;
        private NumericUpDown _floatSpin;
        private TextBox _lineEdit;
        private TextBox _textEdit;
        private NumericUpDown _xVec3Spin;
        private NumericUpDown _yVec3Spin;
        private NumericUpDown _zVec3Spin;
        private NumericUpDown _xVec4Spin;
        private NumericUpDown _yVec4Spin;
        private NumericUpDown _zVec4Spin;
        private NumericUpDown _wVec4Spin;
        private NumericUpDown _stringrefSpin;
        private ListBox _substringList;
        private Button _addSubstringButton;
        private Button _removeSubstringButton;
        private TextBox _substringEdit;
        private GFFTreeNodeViewModel _selectedNode;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:48-81
        // Original: def __init__(self, parent, installation):
        public GFFEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "GFF Editor", "none",
                GetSupportedTypes(),
                GetSupportedTypes(),
                installation)
        {
            InitializeComponent();
            SetupUI();
            SetupSignals();
            Width = 400;
            Height = 250;
            New();
        }

        private static ResourceType[] GetSupportedTypes()
        {
            // Get all GFF resource types
            return new[]
            {
                ResourceType.GFF,
                ResourceType.GFF_XML,
                ResourceType.ARE,
                ResourceType.IFO,
                ResourceType.UTC,
                ResourceType.UTD,
                ResourceType.UTE,
                ResourceType.UTI,
                ResourceType.UTM,
                ResourceType.UTP,
                ResourceType.UTS,
                ResourceType.UTT,
                ResourceType.UTW,
                ResourceType.DLG,
                ResourceType.GIT,
                ResourceType.JRL,
                ResourceType.PTH
            };
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
            var mainSplitter = new Grid();
            mainSplitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            mainSplitter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Tree view
            _treeView = new TreeView();
            Grid.SetColumn(_treeView, 0);
            mainSplitter.Children.Add(_treeView);

            // Right panel
            var rightPanel = new StackPanel();
            Grid.SetColumn(rightPanel, 1);
            mainSplitter.Children.Add(rightPanel);

            // Field box
            _fieldBox = new StackPanel { IsEnabled = false };
            _labelEdit = new TextBox { Watermark = "Label" };
            _typeCombo = new ComboBox();
            _typeCombo.ItemsSource = Enum.GetValues(typeof(GFFFieldType)).Cast<GFFFieldType>().Select(t => t.ToString()).ToList();
            _pages = new StackPanel { Orientation = Orientation.Vertical };

            // Integer page
            _intSpin = new NumericUpDown();
            _pages.Children.Add(_intSpin);

            // Float page
            _floatSpin = new NumericUpDown();
            _pages.Children.Add(_floatSpin);

            // Line edit page
            _lineEdit = new TextBox();
            _pages.Children.Add(_lineEdit);

            // Text edit page
            _textEdit = new TextBox { AcceptsReturn = true };
            _pages.Children.Add(_textEdit);

            // Vector3 page
            var vec3Panel = new StackPanel { Orientation = Orientation.Horizontal };
            _xVec3Spin = new NumericUpDown();
            _yVec3Spin = new NumericUpDown();
            _zVec3Spin = new NumericUpDown();
            vec3Panel.Children.Add(_xVec3Spin);
            vec3Panel.Children.Add(_yVec3Spin);
            vec3Panel.Children.Add(_zVec3Spin);
            _pages.Children.Add(vec3Panel);

            // Vector4 page
            var vec4Panel = new StackPanel { Orientation = Orientation.Horizontal };
            _xVec4Spin = new NumericUpDown();
            _yVec4Spin = new NumericUpDown();
            _zVec4Spin = new NumericUpDown();
            _wVec4Spin = new NumericUpDown();
            vec4Panel.Children.Add(_xVec4Spin);
            vec4Panel.Children.Add(_yVec4Spin);
            vec4Panel.Children.Add(_zVec4Spin);
            vec4Panel.Children.Add(_wVec4Spin);
            _pages.Children.Add(vec4Panel);

            // StringRef page
            _stringrefSpin = new NumericUpDown();
            _substringList = new ListBox();
            _addSubstringButton = new Button { Content = "Add" };
            _removeSubstringButton = new Button { Content = "Remove" };
            _substringEdit = new TextBox();
            var stringrefPanel = new StackPanel();
            stringrefPanel.Children.Add(_stringrefSpin);
            stringrefPanel.Children.Add(_substringList);
            stringrefPanel.Children.Add(_addSubstringButton);
            stringrefPanel.Children.Add(_removeSubstringButton);
            stringrefPanel.Children.Add(_substringEdit);
            _pages.Children.Add(stringrefPanel);

            if (_fieldBox is Panel fieldBoxPanel)
            {
                fieldBoxPanel.Children.Add(new TextBlock { Text = "Label:" });
                fieldBoxPanel.Children.Add(_labelEdit);
                fieldBoxPanel.Children.Add(new TextBlock { Text = "Type:" });
                fieldBoxPanel.Children.Add(_typeCombo);
                if (_pages is Panel pagesPanel)
                {
                    fieldBoxPanel.Children.Add(pagesPanel);
                }
            }

            rightPanel.Children.Add(_fieldBox);

            Content = mainSplitter;
        }

        private void SetupUI()
        {
            // If controls are already initialized (e.g., by SetupProgrammaticUI), skip control finding
            if (_treeView != null && _fieldBox != null && _typeCombo != null && _labelEdit != null)
            {
                return;
            }

            // Use try-catch to handle cases where XAML controls might not be available (e.g., in tests)
            try
            {
                // Try to find controls from XAML if available
                _treeView = this.FindControl<TreeView>("treeView");
                var fieldBoxBorder = this.FindControl<Border>("fieldBox");
                if (fieldBoxBorder != null && fieldBoxBorder.Child is Panel fieldBoxPanel)
                {
                    _fieldBox = fieldBoxPanel;
                }
                _typeCombo = this.FindControl<ComboBox>("typeCombo");
                _labelEdit = this.FindControl<TextBox>("labelEdit");
                var pagesControl = this.FindControl<ContentControl>("pages");
                // Create pages panel if not found
                if (_pages == null)
                {
                    _pages = new StackPanel();
                }
            }
            catch
            {
                // XAML controls not available - controls should already be initialized by SetupProgrammaticUI
                // If not, ensure minimal setup
                if (_treeView == null)
                {
                    _treeView = new TreeView();
                }
                if (_fieldBox == null)
                {
                    _fieldBox = new StackPanel();
                }
                if (_typeCombo == null)
                {
                    _typeCombo = new ComboBox();
                }
                if (_labelEdit == null)
                {
                    _labelEdit = new TextBox();
                }
                if (_pages == null)
                {
                    _pages = new StackPanel();
                }
            }
            // Note: Individual page controls will be created programmatically
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:83-118
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_treeView != null)
            {
                _treeView.SelectionChanged += (s, e) => SelectionChanged();
            }

            if (_intSpin != null)
            {
                _intSpin.ValueChanged += (s, e) => UpdateData();
            }

            if (_floatSpin != null)
            {
                _floatSpin.ValueChanged += (s, e) => UpdateData();
            }

            if (_lineEdit != null)
            {
                _lineEdit.LostFocus += (s, e) => UpdateData();
            }

            if (_textEdit != null)
            {
                _textEdit.TextChanged += (s, e) => UpdateData();
            }

            if (_xVec3Spin != null)
            {
                _xVec3Spin.ValueChanged += (s, e) => UpdateData();
            }

            if (_yVec3Spin != null)
            {
                _yVec3Spin.ValueChanged += (s, e) => UpdateData();
            }

            if (_zVec3Spin != null)
            {
                _zVec3Spin.ValueChanged += (s, e) => UpdateData();
            }

            if (_xVec4Spin != null)
            {
                _xVec4Spin.ValueChanged += (s, e) => UpdateData();
            }

            if (_yVec4Spin != null)
            {
                _yVec4Spin.ValueChanged += (s, e) => UpdateData();
            }

            if (_zVec4Spin != null)
            {
                _zVec4Spin.ValueChanged += (s, e) => UpdateData();
            }

            if (_wVec4Spin != null)
            {
                _wVec4Spin.ValueChanged += (s, e) => UpdateData();
            }

            if (_labelEdit != null)
            {
                _labelEdit.LostFocus += (s, e) => UpdateData();
            }

            if (_typeCombo != null)
            {
                _typeCombo.SelectionChanged += (s, e) => TypeChanged();
            }

            if (_addSubstringButton != null)
            {
                _addSubstringButton.Click += (s, e) => AddSubstring();
            }

            if (_removeSubstringButton != null)
            {
                _removeSubstringButton.Click += (s, e) => RemoveSubstring();
            }

            if (_substringEdit != null)
            {
                _substringEdit.TextChanged += (s, e) => SubstringEdited();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:120-142
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            if (data == null || data.Length == 0)
            {
                GFFContent content = GFFContent.GFF;
                if (!string.IsNullOrEmpty(resref))
                {
                    // Try to determine content type from resname
                    content = GFFContentExtensions.FromResName(resref);
                }
                _gff = new GFF(content);
                LoadGff(_gff);
                return;
            }
            try
            {
                _gff = GFF.FromBytes(data);
                LoadGff(_gff);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load GFF: {ex}");
                GFFContent content = GFFContent.GFF;
                _gff = new GFF(content);
                LoadGff(_gff);
            }
        }

        private void LoadGff(GFF gff)
        {
            if (_treeView == null)
            {
                return;
            }

            var rootNode = new GFFTreeNodeViewModel("[ROOT]", GFFFieldType.Struct, null, gff.Root);
            LoadStruct(rootNode, gff.Root);
            _treeView.ItemsSource = new[] { rootNode };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:144-169
        // Original: def _load_struct(self, node, gff_struct):
        private void LoadStruct(GFFTreeNodeViewModel node, GFFStruct gffStruct)
        {
            foreach ((string label, GFFFieldType ftype, object value) in gffStruct)
            {
                var childNode = new GFFTreeNodeViewModel("", ftype, label, value);
                node.Children.Add(childNode);

                if (ftype == GFFFieldType.List && value is GFFList gffList)
                {
                    LoadList(childNode, gffList);
                }
                else if (ftype == GFFFieldType.Struct && value is GFFStruct childStruct)
                {
                    LoadStruct(childNode, childStruct);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:171-185
        // Original: def _load_list(self, node, gff_list):
        private void LoadList(GFFTreeNodeViewModel node, GFFList gffList)
        {
            foreach (var gffStruct in gffList)
            {
                var childNode = new GFFTreeNodeViewModel("", GFFFieldType.Struct, null, gffStruct);
                childNode.StructId = gffStruct.StructId;
                node.Children.Add(childNode);
                LoadStruct(childNode, gffStruct);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:187-205
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            if (_gff == null)
            {
                return Tuple.Create(new byte[0], new byte[0]);
            }

            if (_treeView?.ItemsSource is IEnumerable<GFFTreeNodeViewModel> items && items.Any())
            {
                var rootNode = items.First();
                BuildStruct(rootNode, _gff.Root);
            }

            ResourceType gffType = _restype ?? ResourceType.GFF;
            byte[] data = GFFAuto.BytesGff(_gff, gffType);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:207-261
        // Original: def _build_struct(self, item, gff_struct):
        private void BuildStruct(GFFTreeNodeViewModel item, GFFStruct gffStruct)
        {
            foreach (var child in item.Children)
            {
                string label = child.Label ?? "";
                GFFFieldType ftype = child.FieldType;
                object value = child.Value;

                if (ftype == GFFFieldType.UInt8)
                {
                    gffStruct.SetUInt8(label, Convert.ToByte(value));
                }
                else if (ftype == GFFFieldType.UInt16)
                {
                    gffStruct.SetUInt16(label, Convert.ToUInt16(value));
                }
                else if (ftype == GFFFieldType.UInt32)
                {
                    gffStruct.SetUInt32(label, Convert.ToUInt32(value));
                }
                else if (ftype == GFFFieldType.UInt64)
                {
                    gffStruct.SetUInt64(label, Convert.ToUInt64(value));
                }
                else if (ftype == GFFFieldType.Int8)
                {
                    gffStruct.SetInt8(label, Convert.ToSByte(value));
                }
                else if (ftype == GFFFieldType.Int16)
                {
                    gffStruct.SetInt16(label, Convert.ToInt16(value));
                }
                else if (ftype == GFFFieldType.Int32)
                {
                    gffStruct.SetInt32(label, Convert.ToInt32(value));
                }
                else if (ftype == GFFFieldType.Int64)
                {
                    gffStruct.SetInt64(label, Convert.ToInt64(value));
                }
                else if (ftype == GFFFieldType.Single)
                {
                    gffStruct.SetSingle(label, Convert.ToSingle(value));
                }
                else if (ftype == GFFFieldType.Double)
                {
                    gffStruct.SetDouble(label, Convert.ToDouble(value));
                }
                else if (ftype == GFFFieldType.ResRef)
                {
                    gffStruct.SetResRef(label, value as ResRef ?? ResRef.FromBlank());
                }
                else if (ftype == GFFFieldType.String)
                {
                    gffStruct.SetString(label, value?.ToString() ?? "");
                }
                else if (ftype == GFFFieldType.LocalizedString)
                {
                    gffStruct.SetLocString(label, value as LocalizedString ?? LocalizedString.FromInvalid());
                }
                else if (ftype == GFFFieldType.Binary)
                {
                    gffStruct.SetBinary(label, value as byte[] ?? new byte[0]);
                }
                else if (ftype == GFFFieldType.Vector3)
                {
                    gffStruct.SetVector3(label, value is Vector3 v3 ? v3 : new Vector3(0, 0, 0));
                }
                else if (ftype == GFFFieldType.Vector4)
                {
                    gffStruct.SetVector4(label, value is Vector4 v4 ? v4 : new Vector4(0, 0, 0, 0));
                }
                else if (ftype == GFFFieldType.Struct && value is GFFStruct childStruct)
                {
                    var newStruct = new GFFStruct(childStruct.StructId);
                    gffStruct.SetStruct(label, newStruct);
                    BuildStruct(child, newStruct);
                }
                else if (ftype == GFFFieldType.List)
                {
                    var newList = new GFFList();
                    gffStruct.SetList(label, newList);
                    BuildList(child, newList);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:262-272
        // Original: def _build_list(self, item, gff_list):
        private void BuildList(GFFTreeNodeViewModel item, GFFList gffList)
        {
            foreach (var child in item.Children)
            {
                int structId = child.StructId;
                var gffStruct = gffList.Add(structId);
                BuildStruct(child, gffStruct);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:274-282
        // Original: def new(self):
        public override void New()
        {
            base.New();
            GFFContent content = GFFContent.GFF;
            if (!string.IsNullOrEmpty(_resname))
            {
                content = GFFContentExtensions.FromResName(_resname);
            }
            _gff = new GFF(content);
            if (_treeView != null)
            {
                var rootNode = new GFFTreeNodeViewModel("[ROOT]", GFFFieldType.Struct, null, _gff.Root);
                LoadStruct(rootNode, _gff.Root);
                _treeView.ItemsSource = new[] { rootNode };
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:284-292
        // Original: def selection_changed(self, selected):
        private void SelectionChanged()
        {
            if (_treeView?.SelectedItem is GFFTreeNodeViewModel selectedNode)
            {
                _selectedNode = selectedNode;
                LoadItem(selectedNode);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:294-450
        // Original: def load_item(self, item):
        private void LoadItem(GFFTreeNodeViewModel item)
        {
            if (item.Label == null)
            {
                if (_fieldBox != null)
                {
                    _fieldBox.IsEnabled = false;
                }
                return;
            }

            if (_fieldBox != null)
            {
                _fieldBox.IsEnabled = true;
            }

            if (_typeCombo != null)
            {
                _typeCombo.SelectedItem = item.FieldType.ToString();
            }

            if (_labelEdit != null)
            {
                _labelEdit.Text = item.Label ?? "";
            }

            // Show appropriate editor based on field type
            if (item.FieldType == GFFFieldType.Int8 || item.FieldType == GFFFieldType.Int16 ||
                item.FieldType == GFFFieldType.Int32 || item.FieldType == GFFFieldType.Int64 ||
                item.FieldType == GFFFieldType.UInt8 || item.FieldType == GFFFieldType.UInt16 ||
                item.FieldType == GFFFieldType.UInt32 || item.FieldType == GFFFieldType.UInt64)
            {
                if (_intSpin != null)
                {
                    _intSpin.Value = Convert.ToDecimal(item.Value ?? 0);
                }
            }
            else if (item.FieldType == GFFFieldType.Single || item.FieldType == GFFFieldType.Double)
            {
                if (_floatSpin != null)
                {
                    _floatSpin.Value = Convert.ToDecimal(item.Value ?? 0);
                }
            }
            else if (item.FieldType == GFFFieldType.ResRef)
            {
                if (_lineEdit != null)
                {
                    _lineEdit.Text = item.Value?.ToString() ?? "";
                }
            }
            else if (item.FieldType == GFFFieldType.String)
            {
                if (_textEdit != null)
                {
                    _textEdit.Text = item.Value?.ToString() ?? "";
                }
            }
            else if (item.FieldType == GFFFieldType.Vector3 && item.Value is Vector3 vec3)
            {
                if (_xVec3Spin != null)
                {
                    _xVec3Spin.Value = Convert.ToDecimal(vec3.X);
                }
                if (_yVec3Spin != null)
                {
                    _yVec3Spin.Value = Convert.ToDecimal(vec3.Y);
                }
                if (_zVec3Spin != null)
                {
                    _zVec3Spin.Value = Convert.ToDecimal(vec3.Z);
                }
            }
            else if (item.FieldType == GFFFieldType.Vector4 && item.Value is Vector4 vec4)
            {
                if (_xVec4Spin != null)
                {
                    _xVec4Spin.Value = Convert.ToDecimal(vec4.X);
                }
                if (_yVec4Spin != null)
                {
                    _yVec4Spin.Value = Convert.ToDecimal(vec4.Y);
                }
                if (_zVec4Spin != null)
                {
                    _zVec4Spin.Value = Convert.ToDecimal(vec4.Z);
                }
                if (_wVec4Spin != null)
                {
                    _wVec4Spin.Value = Convert.ToDecimal(vec4.W);
                }
            }
            else if (item.FieldType == GFFFieldType.LocalizedString)
            {
                // LocalizedString editing will be implemented when LocalizedString support is available
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/gff.py:452-796
        // Original: def update_data(self):
        private void UpdateData()
        {
            if (_selectedNode == null)
            {
                return;
            }

            if (_labelEdit != null)
            {
                _selectedNode.Label = _labelEdit.Text;
            }

            // Update value based on field type
            if (_selectedNode.FieldType == GFFFieldType.Int8 || _selectedNode.FieldType == GFFFieldType.Int16 ||
                _selectedNode.FieldType == GFFFieldType.Int32 || _selectedNode.FieldType == GFFFieldType.Int64 ||
                _selectedNode.FieldType == GFFFieldType.UInt8 || _selectedNode.FieldType == GFFFieldType.UInt16 ||
                _selectedNode.FieldType == GFFFieldType.UInt32 || _selectedNode.FieldType == GFFFieldType.UInt64)
            {
                if (_intSpin != null)
                {
                    _selectedNode.Value = Convert.ToInt32(_intSpin.Value ?? 0);
                }
            }
            else if (_selectedNode.FieldType == GFFFieldType.Single || _selectedNode.FieldType == GFFFieldType.Double)
            {
                if (_floatSpin != null)
                {
                    _selectedNode.Value = Convert.ToDouble(_floatSpin.Value ?? 0);
                }
            }
            else if (_selectedNode.FieldType == GFFFieldType.ResRef)
            {
                if (_lineEdit != null)
                {
                    _selectedNode.Value = new ResRef(_lineEdit.Text);
                }
            }
            else if (_selectedNode.FieldType == GFFFieldType.String)
            {
                if (_textEdit != null)
                {
                    _selectedNode.Value = _textEdit.Text;
                }
            }
            else if (_selectedNode.FieldType == GFFFieldType.Vector3)
            {
                if (_xVec3Spin != null && _yVec3Spin != null && _zVec3Spin != null)
                {
                    _selectedNode.Value = new Vector3(
                        Convert.ToSingle(_xVec3Spin.Value ?? 0),
                        Convert.ToSingle(_yVec3Spin.Value ?? 0),
                        Convert.ToSingle(_zVec3Spin.Value ?? 0));
                }
            }
            else if (_selectedNode.FieldType == GFFFieldType.Vector4)
            {
                if (_xVec4Spin != null && _yVec4Spin != null && _zVec4Spin != null && _wVec4Spin != null)
                {
                    _selectedNode.Value = new Vector4(
                        Convert.ToSingle(_xVec4Spin.Value ?? 0),
                        Convert.ToSingle(_yVec4Spin.Value ?? 0),
                        Convert.ToSingle(_zVec4Spin.Value ?? 0),
                        Convert.ToSingle(_wVec4Spin.Value ?? 0));
                }
            }

            RefreshItemText(_selectedNode);
        }

        private void RefreshItemText(GFFTreeNodeViewModel item)
        {
            if (item.Label == null)
            {
                item.Text = "[ROOT]";
            }
            else
            {
                string label = item.Label ?? "";
                string valueStr = item.Value?.ToString() ?? "";
                item.Text = $"{label}: {valueStr}";
            }
        }

        private void TypeChanged()
        {
            // Handle type change - will be implemented when needed
        }

        private void AddSubstring()
        {
            // Add substring - will be implemented when LocalizedString support is available
        }

        private void RemoveSubstring()
        {
            // Remove substring - will be implemented when LocalizedString support is available
        }

        private void SubstringEdited()
        {
            // Handle substring edit - will be implemented when LocalizedString support is available
        }

        public override void SaveAs()
        {
            Save();
        }
    }

    // ViewModel for GFF tree nodes
    public class GFFTreeNodeViewModel
    {
        public string Text { get; set; }
        public GFFFieldType FieldType { get; set; }
        public string Label { get; set; }
        public object Value { get; set; }
        public int StructId { get; set; }
        public List<GFFTreeNodeViewModel> Children { get; set; }

        public GFFTreeNodeViewModel(string text, GFFFieldType fieldType, string label, object value)
        {
            Text = text;
            FieldType = fieldType;
            Label = label;
            Value = value;
            Children = new List<GFFTreeNodeViewModel>();
            StructId = -1;
        }
    }
}
