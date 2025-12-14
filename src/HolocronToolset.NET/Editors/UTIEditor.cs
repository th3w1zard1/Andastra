using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:45
    // Original: class UTIEditor(Editor):
    public class UTIEditor : Editor
    {
        private UTI _uti;
        private HTInstallation _installation;

        // UI Controls - Basic
        private TextBox _nameEdit;
        private Button _nameEditBtn;
        private TextBox _descEdit;
        private Button _descEditBtn;
        private TextBox _tagEdit;
        private Button _tagGenerateBtn;
        private TextBox _resrefEdit;
        private Button _resrefGenerateBtn;
        private ComboBox _baseSelect;
        private NumericUpDown _costSpin;
        private NumericUpDown _additionalCostSpin;
        private NumericUpDown _upgradeSpin;
        private CheckBox _plotCheckbox;
        private NumericUpDown _chargesSpin;
        private NumericUpDown _stackSpin;
        private NumericUpDown _modelVarSpin;
        private NumericUpDown _bodyVarSpin;
        private NumericUpDown _textureVarSpin;

        // UI Controls - Properties
        private TreeView _availablePropertyList;
        private ListBox _assignedPropertiesList;
        private Button _addPropertyBtn;
        private Button _removePropertyBtn;
        private Button _editPropertyBtn;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:46-87
        // Original: def __init__(self, parent, installation):
        public UTIEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Item Editor", "item",
                new[] { ResourceType.UTI },
                new[] { ResourceType.UTI },
                installation)
        {
            _installation = installation;
            _uti = new UTI();

            InitializeComponent();
            SetupUI();
            MinWidth = 700;
            MinHeight = 350;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:89-106
        // Original: def _setup_signals(self):
        private void SetupProgrammaticUI()
        {
            var scrollViewer = new ScrollViewer();
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Basic Group
            var basicGroup = new Expander { Header = "Basic", IsExpanded = true };
            var basicPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Name
            var nameLabel = new TextBlock { Text = "Name:" };
            _nameEdit = new TextBox { IsReadOnly = true };
            _nameEditBtn = new Button { Content = "Edit Name" };
            _nameEditBtn.Click += (s, e) => EditName();
            basicPanel.Children.Add(nameLabel);
            basicPanel.Children.Add(_nameEdit);
            basicPanel.Children.Add(_nameEditBtn);

            // Description
            var descLabel = new TextBlock { Text = "Description:" };
            _descEdit = new TextBox { IsReadOnly = true };
            _descEditBtn = new Button { Content = "Edit Description" };
            _descEditBtn.Click += (s, e) => EditDescription();
            basicPanel.Children.Add(descLabel);
            basicPanel.Children.Add(_descEdit);
            basicPanel.Children.Add(_descEditBtn);

            // Tag
            var tagLabel = new TextBlock { Text = "Tag:" };
            _tagEdit = new TextBox();
            _tagGenerateBtn = new Button { Content = "Generate" };
            _tagGenerateBtn.Click += (s, e) => GenerateTag();
            basicPanel.Children.Add(tagLabel);
            basicPanel.Children.Add(_tagEdit);
            basicPanel.Children.Add(_tagGenerateBtn);

            // ResRef
            var resrefLabel = new TextBlock { Text = "ResRef:" };
            _resrefEdit = new TextBox();
            _resrefGenerateBtn = new Button { Content = "Generate" };
            _resrefGenerateBtn.Click += (s, e) => GenerateResref();
            basicPanel.Children.Add(resrefLabel);
            basicPanel.Children.Add(_resrefEdit);
            basicPanel.Children.Add(_resrefGenerateBtn);

            // Base Item
            var baseLabel = new TextBlock { Text = "Base Item:" };
            _baseSelect = new ComboBox();
            _baseSelect.SelectionChanged += (s, e) => UpdateIcon();
            basicPanel.Children.Add(baseLabel);
            basicPanel.Children.Add(_baseSelect);

            // Cost
            var costLabel = new TextBlock { Text = "Cost:" };
            _costSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue };
            var additionalCostLabel = new TextBlock { Text = "Additional Cost:" };
            _additionalCostSpin = new NumericUpDown { Minimum = 0, Maximum = int.MaxValue };
            var upgradeLabel = new TextBlock { Text = "Upgrade Level:" };
            _upgradeSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            _plotCheckbox = new CheckBox { Content = "Plot" };
            var chargesLabel = new TextBlock { Text = "Charges:" };
            _chargesSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var stackLabel = new TextBlock { Text = "Stack Size:" };
            _stackSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };

            basicPanel.Children.Add(costLabel);
            basicPanel.Children.Add(_costSpin);
            basicPanel.Children.Add(additionalCostLabel);
            basicPanel.Children.Add(_additionalCostSpin);
            basicPanel.Children.Add(upgradeLabel);
            basicPanel.Children.Add(_upgradeSpin);
            basicPanel.Children.Add(_plotCheckbox);
            basicPanel.Children.Add(chargesLabel);
            basicPanel.Children.Add(_chargesSpin);
            basicPanel.Children.Add(stackLabel);
            basicPanel.Children.Add(_stackSpin);

            // Variations (for armor items)
            var modelVarLabel = new TextBlock { Text = "Model Variation:" };
            _modelVarSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            _modelVarSpin.ValueChanged += (s, e) => UpdateIcon();
            var bodyVarLabel = new TextBlock { Text = "Body Variation:" };
            _bodyVarSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            _bodyVarSpin.ValueChanged += (s, e) => UpdateIcon();
            var textureVarLabel = new TextBlock { Text = "Texture Variation:" };
            _textureVarSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            _textureVarSpin.ValueChanged += (s, e) => UpdateIcon();

            basicPanel.Children.Add(modelVarLabel);
            basicPanel.Children.Add(_modelVarSpin);
            basicPanel.Children.Add(bodyVarLabel);
            basicPanel.Children.Add(_bodyVarSpin);
            basicPanel.Children.Add(textureVarLabel);
            basicPanel.Children.Add(_textureVarSpin);

            basicGroup.Content = basicPanel;
            mainPanel.Children.Add(basicGroup);

            // Properties Group
            var propertiesGroup = new Expander { Header = "Properties", IsExpanded = false };
            var propertiesPanel = new StackPanel { Orientation = Orientation.Vertical };

            var availableLabel = new TextBlock { Text = "Available Properties:" };
            _availablePropertyList = new TreeView();
            var assignedLabel = new TextBlock { Text = "Assigned Properties:" };
            _assignedPropertiesList = new ListBox();
            var propertyButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _addPropertyBtn = new Button { Content = "Add" };
            _addPropertyBtn.Click += (s, e) => AddSelectedProperty();
            _removePropertyBtn = new Button { Content = "Remove" };
            _removePropertyBtn.Click += (s, e) => RemoveSelectedProperty();
            _editPropertyBtn = new Button { Content = "Edit" };
            _editPropertyBtn.Click += (s, e) => EditSelectedProperty();
            propertyButtonsPanel.Children.Add(_addPropertyBtn);
            propertyButtonsPanel.Children.Add(_removePropertyBtn);
            propertyButtonsPanel.Children.Add(_editPropertyBtn);

            propertiesPanel.Children.Add(availableLabel);
            propertiesPanel.Children.Add(_availablePropertyList);
            propertiesPanel.Children.Add(assignedLabel);
            propertiesPanel.Children.Add(_assignedPropertiesList);
            propertiesPanel.Children.Add(propertyButtonsPanel);
            propertiesGroup.Content = propertiesPanel;
            mainPanel.Children.Add(propertiesGroup);

            // Comments Group
            var commentsGroup = new Expander { Header = "Comments", IsExpanded = false };
            var commentsPanel = new StackPanel { Orientation = Orientation.Vertical };
            var commentsLabel = new TextBlock { Text = "Comment:" };
            _commentsEdit = new TextBox { AcceptsReturn = true, AcceptsTab = true };
            commentsPanel.Children.Add(commentsLabel);
            commentsPanel.Children.Add(_commentsEdit);
            commentsGroup.Content = commentsPanel;
            mainPanel.Children.Add(commentsGroup);

            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        private void SetupUI()
        {
            // Try to find controls from XAML if available
            // For now, programmatic UI is set up in SetupProgrammaticUI
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:155-196
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The UTI file data is empty or invalid.");
            }

            var gff = GFF.FromBytes(data);
            _uti = UTIHelpers.ConstructUti(gff);
            LoadUTI(_uti);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:167-196
        // Original: def _loadUTI(self, uti):
        private void LoadUTI(UTI uti)
        {
            _uti = uti;

            // Basic
            if (_nameEdit != null)
            {
                _nameEdit.Text = _installation != null ? _installation.String(uti.Name) : uti.Name.StringRef.ToString();
            }
            if (_descEdit != null)
            {
                _descEdit.Text = _installation != null ? _installation.String(uti.Description) : uti.Description.StringRef.ToString();
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = uti.Tag;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = uti.ResRef.ToString();
            }
            if (_baseSelect != null)
            {
                _baseSelect.SelectedIndex = uti.BaseItem;
            }
            if (_costSpin != null)
            {
                _costSpin.Value = uti.Cost;
            }
            if (_additionalCostSpin != null)
            {
                _additionalCostSpin.Value = uti.AddCost;
            }
            if (_upgradeSpin != null)
            {
                _upgradeSpin.Value = uti.UpgradeLevel;
            }
            if (_plotCheckbox != null)
            {
                _plotCheckbox.IsChecked = uti.Plot != 0;
            }
            if (_chargesSpin != null)
            {
                _chargesSpin.Value = uti.Charges;
            }
            if (_stackSpin != null)
            {
                _stackSpin.Value = uti.StackSize;
            }
            if (_modelVarSpin != null)
            {
                _modelVarSpin.Value = uti.ModelVariation;
            }
            if (_bodyVarSpin != null)
            {
                _bodyVarSpin.Value = uti.BodyVariation;
            }
            if (_textureVarSpin != null)
            {
                _textureVarSpin.Value = uti.TextureVariation;
            }

            // Properties
            if (_assignedPropertiesList != null)
            {
                _assignedPropertiesList.Items.Clear();
                if (uti.Properties != null)
                {
                    foreach (var prop in uti.Properties)
                    {
                        string summary = PropertySummary(prop);
                        _assignedPropertiesList.Items.Add(new PropertyListItem { Text = summary, Property = prop });
                    }
                }
            }

            // Comments
            if (_commentsEdit != null)
            {
                _commentsEdit.Text = uti.Comment;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:197-230
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Basic
            _uti.Name = _uti.Name ?? LocalizedString.FromInvalid();
            _uti.Description = _uti.Description ?? LocalizedString.FromInvalid();
            _uti.Tag = _tagEdit?.Text ?? "";
            _uti.ResRef = new ResRef(_resrefEdit?.Text ?? "");
            _uti.BaseItem = _baseSelect?.SelectedIndex ?? 0;
            _uti.Cost = (int)(_costSpin?.Value ?? 0);
            _uti.AddCost = (int)(_additionalCostSpin?.Value ?? 0);
            _uti.UpgradeLevel = (int)(_upgradeSpin?.Value ?? 0);
            _uti.Plot = (_plotCheckbox?.IsChecked ?? false) ? 1 : 0;
            _uti.Charges = (int)(_chargesSpin?.Value ?? 0);
            _uti.StackSize = (int)(_stackSpin?.Value ?? 0);
            _uti.ModelVariation = (int)(_modelVarSpin?.Value ?? 0);
            _uti.BodyVariation = (int)(_bodyVarSpin?.Value ?? 0);
            _uti.TextureVariation = (int)(_textureVarSpin?.Value ?? 0);

            // Properties
            _uti.Properties.Clear();
            if (_assignedPropertiesList?.Items != null)
            {
                foreach (var item in _assignedPropertiesList.Items)
                {
                    if (item is PropertyListItem propItem && propItem.Property != null)
                    {
                        _uti.Properties.Add(propItem.Property);
                    }
                }
            }

            // Comments
            _uti.Comment = _commentsEdit?.Text ?? "";

            // Build GFF
            Game game = _installation?.Game ?? Game.K2;
            var gff = UTIHelpers.DismantleUti(_uti, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTI);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:232-234
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _uti = new UTI();
            LoadUTI(_uti);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:236-240
        // Original: def change_name(self):
        private void EditName()
        {
            if (_installation == null) return;
            var dialog = new LocalizedStringDialog(this, _installation, _uti.Name);
            if (dialog.ShowDialog())
            {
                _uti.Name = dialog.LocString;
                if (_nameEdit != null)
                {
                    _nameEdit.Text = _installation.String(_uti.Name);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:242-246
        // Original: def change_desc(self):
        private void EditDescription()
        {
            if (_installation == null) return;
            var dialog = new LocalizedStringDialog(this, _installation, _uti.Description);
            if (dialog.ShowDialog())
            {
                _uti.Description = dialog.LocString;
                if (_descEdit != null)
                {
                    _descEdit.Text = _installation.String(_uti.Description);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:248-252
        // Original: def generate_tag(self):
        private void GenerateTag()
        {
            if (string.IsNullOrEmpty(_resrefEdit?.Text))
            {
                GenerateResref();
            }
            if (_tagEdit != null && _resrefEdit != null)
            {
                _tagEdit.Text = _resrefEdit.Text;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:254-258
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = !string.IsNullOrEmpty(base._resname) ? base._resname : "m00xx_itm_000";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:260-268
        // Original: def edit_selected_property(self):
        private void EditSelectedProperty()
        {
            // Placeholder for property editor dialog
            // Will be implemented when PropertyEditor dialog is available
            System.Console.WriteLine("Property editor dialog not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:270-301
        // Original: def add_selected_property(self):
        private void AddSelectedProperty()
        {
            // Placeholder for adding properties
            // Will be implemented when property management is fully available
            System.Console.WriteLine("Add property not yet fully implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:303-307
        // Original: def remove_selected_property(self):
        private void RemoveSelectedProperty()
        {
            if (_assignedPropertiesList?.SelectedItem != null)
            {
                _assignedPropertiesList.Items.Remove(_assignedPropertiesList.SelectedItem);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:309-330
        // Original: def property_summary(self, uti_property):
        private string PropertySummary(UTIProperty prop)
        {
            // Simplified property summary - full implementation would use installation to get names from 2DA files
            return $"Property {prop.PropertyName}: Subtype {prop.Subtype}";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:427-435
        // Original: def on_update_icon(self, *args, **kwargs):
        private void UpdateIcon()
        {
            // Placeholder for icon update
            // Will be implemented when icon loading is available
            System.Console.WriteLine("Icon update not yet implemented");
        }

        // Helper class for property list items
        private class PropertyListItem
        {
            public string Text { get; set; }
            public UTIProperty Property { get; set; }

            public override string ToString()
            {
                return Text;
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
