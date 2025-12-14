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

        // Matching PyKotor implementation: Expose UI controls for testing
        public TextBox NameEdit => _nameEdit;
        public TextBox DescEdit => _descEdit;
        public TextBox TagEdit => _tagEdit;
        public TextBox ResrefEdit => _resrefEdit;
        public ComboBox BaseSelect => _baseSelect;
        public NumericUpDown CostSpin => _costSpin;
        public NumericUpDown AdditionalCostSpin => _additionalCostSpin;
        public NumericUpDown UpgradeSpin => _upgradeSpin;
        public CheckBox PlotCheckbox => _plotCheckbox;
        public NumericUpDown ChargesSpin => _chargesSpin;
        public NumericUpDown StackSpin => _stackSpin;
        public NumericUpDown ModelVarSpin => _modelVarSpin;
        public NumericUpDown BodyVarSpin => _bodyVarSpin;
        public NumericUpDown TextureVarSpin => _textureVarSpin;
        public Button TagGenerateBtn => _tagGenerateBtn;
        public Button ResrefGenerateBtn => _resrefGenerateBtn;
        public TreeView AvailablePropertyList => _availablePropertyList;
        public ListBox AssignedPropertiesList => _assignedPropertiesList;
        public Button AddPropertyBtn => _addPropertyBtn;
        public Button RemovePropertyBtn => _removePropertyBtn;
        public Button EditPropertyBtn => _editPropertyBtn;
        public TextBox CommentsEdit => _commentsEdit;

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

                // Try to find controls from XAML
                _nameEdit = this.FindControl<TextBox>("nameEdit");
                _nameEditBtn = this.FindControl<Button>("nameEditBtn");
                _descEdit = this.FindControl<TextBox>("descEdit");
                _descEditBtn = this.FindControl<Button>("descEditBtn");
                _tagEdit = this.FindControl<TextBox>("tagEdit");
                _tagGenerateBtn = this.FindControl<Button>("tagGenerateBtn");
                _resrefEdit = this.FindControl<TextBox>("resrefEdit");
                _resrefGenerateBtn = this.FindControl<Button>("resrefGenerateBtn");
                _baseSelect = this.FindControl<ComboBox>("baseSelect");
                _costSpin = this.FindControl<NumericUpDown>("costSpin");
                _additionalCostSpin = this.FindControl<NumericUpDown>("additionalCostSpin");
                _upgradeSpin = this.FindControl<NumericUpDown>("upgradeSpin");
                _plotCheckbox = this.FindControl<CheckBox>("plotCheckbox");
                _chargesSpin = this.FindControl<NumericUpDown>("chargesSpin");
                _stackSpin = this.FindControl<NumericUpDown>("stackSpin");
                _modelVarSpin = this.FindControl<NumericUpDown>("modelVarSpin");
                _bodyVarSpin = this.FindControl<NumericUpDown>("bodyVarSpin");
                _textureVarSpin = this.FindControl<NumericUpDown>("textureVarSpin");
                _availablePropertyList = this.FindControl<TreeView>("availablePropertyList");
                _assignedPropertiesList = this.FindControl<ListBox>("assignedPropertiesList");
                _addPropertyBtn = this.FindControl<Button>("addPropertyBtn");
                _removePropertyBtn = this.FindControl<Button>("removePropertyBtn");
                _editPropertyBtn = this.FindControl<Button>("editPropertyBtn");
                _commentsEdit = this.FindControl<TextBox>("commentsEdit");
            }
            catch
            {
                // XAML not available or controls not found - will use programmatic UI
                xamlLoaded = false;
            }

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
            }
            else
            {
                // XAML loaded, set up signals
                SetupSignals();
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:89-105
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            if (_tagGenerateBtn != null)
            {
                _tagGenerateBtn.Click += (s, e) => GenerateTag();
            }
            if (_resrefGenerateBtn != null)
            {
                _resrefGenerateBtn.Click += (s, e) => GenerateResref();
            }
            if (_editPropertyBtn != null)
            {
                _editPropertyBtn.Click += (s, e) => EditSelectedProperty();
            }
            if (_removePropertyBtn != null)
            {
                _removePropertyBtn.Click += (s, e) => RemoveSelectedProperty();
            }
            if (_addPropertyBtn != null)
            {
                _addPropertyBtn.Click += (s, e) => AddSelectedProperty();
            }
            if (_modelVarSpin != null)
            {
                _modelVarSpin.ValueChanged += (s, e) => UpdateIcon();
            }
            if (_bodyVarSpin != null)
            {
                _bodyVarSpin.ValueChanged += (s, e) => UpdateIcon();
            }
            if (_textureVarSpin != null)
            {
                _textureVarSpin.ValueChanged += (s, e) => UpdateIcon();
            }
            if (_baseSelect != null)
            {
                _baseSelect.SelectionChanged += (s, e) => UpdateIcon();
            }
            if (_nameEditBtn != null)
            {
                _nameEditBtn.Click += (s, e) => EditName();
            }
            if (_descEditBtn != null)
            {
                _descEditBtn.Click += (s, e) => EditDescription();
            }
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
        // Original: uti: UTI = deepcopy(self._uti)
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation: deepcopy(self._uti) to preserve original values
            // Since C# 7.3 doesn't have deepcopy, manually copy the UTI
            var uti = CopyUTI(_uti);

            // Basic - read from UI controls (matching Python which always reads from UI)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:201-214
            // Note: Python reads from LocalizedString widgets (locstring()), but we use TextBox
            // Since we can't reconstruct LocalizedString from text, preserve original from copy
            // In a full implementation, this would read from a LocalizedString widget
            uti.Name = uti.Name ?? LocalizedString.FromInvalid();
            uti.Description = uti.Description ?? LocalizedString.FromInvalid();
            uti.Tag = _tagEdit?.Text ?? uti.Tag ?? "";
            uti.ResRef = _resrefEdit != null && !string.IsNullOrEmpty(_resrefEdit.Text)
                ? new ResRef(_resrefEdit.Text)
                : uti.ResRef;
            uti.BaseItem = _baseSelect?.SelectedIndex ?? uti.BaseItem;
            uti.Cost = _costSpin?.Value != null ? (int)_costSpin.Value : uti.Cost;
            uti.AddCost = _additionalCostSpin?.Value != null ? (int)_additionalCostSpin.Value : uti.AddCost;
            uti.UpgradeLevel = _upgradeSpin?.Value != null ? (int)_upgradeSpin.Value : uti.UpgradeLevel;
            uti.Plot = (_plotCheckbox?.IsChecked ?? (uti.Plot != 0)) ? 1 : 0;
            uti.Charges = _chargesSpin?.Value != null ? (int)_chargesSpin.Value : uti.Charges;
            uti.StackSize = _stackSpin?.Value != null ? (int)_stackSpin.Value : uti.StackSize;
            uti.ModelVariation = _modelVarSpin?.Value != null ? (int)_modelVarSpin.Value : uti.ModelVariation;
            uti.BodyVariation = _bodyVarSpin?.Value != null ? (int)_bodyVarSpin.Value : uti.BodyVariation;
            uti.TextureVariation = _textureVarSpin?.Value != null ? (int)_textureVarSpin.Value : uti.TextureVariation;

            // Properties - read from UI list
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:215-221
            uti.Properties.Clear();
            if (_assignedPropertiesList?.Items != null)
            {
                foreach (var item in _assignedPropertiesList.Items)
                {
                    if (item is PropertyListItem propItem && propItem.Property != null)
                    {
                        uti.Properties.Add(propItem.Property);
                    }
                }
            }

            // Comments
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:224
            uti.Comment = _commentsEdit?.Text ?? "";

            // Build GFF
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:226-230
            Game game = _installation?.Game ?? Game.K2;
            var gff = UTIHelpers.DismantleUti(uti, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTI);
            return Tuple.Create(data, new byte[0]);
        }


        // Matching PyKotor implementation: deepcopy equivalent for C# 7.3
        // Original: uti: UTI = deepcopy(self._uti)
        private UTI CopyUTI(UTI source)
        {
            // Deep copy LocalizedString objects (they're reference types)
            LocalizedString copyName = source.Name != null
                ? new LocalizedString(source.Name.StringRef, new Dictionary<int, string>(GetSubstringsDict(source.Name)))
                : null;
            LocalizedString copyDesc = source.Description != null
                ? new LocalizedString(source.Description.StringRef, new Dictionary<int, string>(GetSubstringsDict(source.Description)))
                : null;
            LocalizedString copyDescUnid = source.DescriptionUnidentified != null
                ? new LocalizedString(source.DescriptionUnidentified.StringRef, new Dictionary<int, string>(GetSubstringsDict(source.DescriptionUnidentified)))
                : null;

            var copy = new UTI
            {
                ResRef = source.ResRef,
                BaseItem = source.BaseItem,
                Name = copyName,
                Description = copyDesc,
                DescriptionUnidentified = copyDescUnid,
                Cost = source.Cost,
                StackSize = source.StackSize,
                Charges = source.Charges,
                Plot = source.Plot,
                AddCost = source.AddCost,
                Stolen = source.Stolen,
                Identified = source.Identified,
                ItemType = source.ItemType,
                BaseItemType = source.BaseItemType,
                UpgradeLevel = source.UpgradeLevel,
                BodyVariation = source.BodyVariation,
                TextureVariation = source.TextureVariation,
                ModelVariation = source.ModelVariation,
                PaletteId = source.PaletteId,
                Comment = source.Comment,
                Tag = source.Tag
            };

            // Copy properties
            foreach (var prop in source.Properties)
            {
                copy.Properties.Add(new UTIProperty
                {
                    PropertyName = prop.PropertyName,
                    Subtype = prop.Subtype,
                    CostTable = prop.CostTable,
                    CostValue = prop.CostValue,
                    Param1 = prop.Param1,
                    Param1Value = prop.Param1Value,
                    ChanceAppear = prop.ChanceAppear,
                    UpgradeType = prop.UpgradeType
                });
            }

            // Copy upgrades
            foreach (var upgrade in source.Upgrades)
            {
                copy.Upgrades.Add(new UTIUpgrade
                {
                    Upgrade = upgrade.Upgrade,
                    Name = upgrade.Name,
                    Description = upgrade.Description
                });
            }

            return copy;
        }

        // Helper to extract substrings dictionary from LocalizedString for copying
        private Dictionary<int, string> GetSubstringsDict(LocalizedString locString)
        {
            var dict = new Dictionary<int, string>();
            if (locString != null)
            {
                foreach ((Language lang, Gender gender, string text) in locString)
                {
                    int substringId = LocalizedString.SubstringId(lang, gender);
                    dict[substringId] = text;
                }
            }
            return dict;
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
