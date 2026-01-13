using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource.Formats.GFF.Generics.UTI;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Widgets;
using JetBrains.Annotations;
using GFFAuto = BioWare.NET.Resource.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:45
    // Original: class UTIEditor(Editor):
    public class UTIEditor : Editor
    {
        private UTI _uti;

        // UI Controls - Basic
        private LocalizedStringEdit _nameEdit;
        private LocalizedStringEdit _descEdit;
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
        private Image _iconLabel;

        // UI Controls - Properties
        private TreeView _availablePropertyList;
        private ListBox _assignedPropertiesList;
        private Button _addPropertyBtn;
        private Button _removePropertyBtn;
        private Button _editPropertyBtn;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation: Expose UI controls for testing
        public LocalizedStringEdit NameEdit => _nameEdit;
        public LocalizedStringEdit DescEdit => _descEdit;
        public TextBox TagEdit => _tagEdit;
        public TextBox ResrefEdit => _resrefEdit;
        public ComboBox BaseSelect => _baseSelect;
        // Property to expose ItemCount for testing (matching Python's count())
        public int BaseSelectItemCount
        {
            get
            {
                if (_baseSelect?.Items == null)
                {
                    return 0;
                }
                if (_baseSelect.Items is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }
                // Fallback: count items manually
                int count = 0;
                foreach (var item in _baseSelect.Items)
                {
                    count++;
                }
                return count;
            }
        }
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
        // Property to expose ItemCount for testing (matching Python's topLevelItemCount())
        public int AvailablePropertyListItemCount
        {
            get
            {
                if (_availablePropertyList?.Items == null)
                {
                    return 0;
                }
                if (_availablePropertyList.Items is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }
                // Fallback: count items manually
                int count = 0;
                foreach (var item in _availablePropertyList.Items)
                {
                    count++;
                }
                return count;
            }
        }
        public ListBox AssignedPropertiesList => _assignedPropertiesList;
        // Property to expose ItemCount for testing (matching Python's count())
        public int AssignedPropertiesListItemCount
        {
            get
            {
                if (_assignedPropertiesList?.Items == null)
                {
                    return 0;
                }
                if (_assignedPropertiesList.Items is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }
                // Fallback: count items manually
                int count = 0;
                foreach (var item in _assignedPropertiesList.Items)
                {
                    count++;
                }
                return count;
            }
        }
        public Button AddPropertyBtn => _addPropertyBtn;
        public Button RemovePropertyBtn => _removePropertyBtn;
        public Button EditPropertyBtn => _editPropertyBtn;
        public TextBox CommentsEdit => _commentsEdit;
        public Image IconLabel => _iconLabel;

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
            // SetupInstallation is now called from InitializeComponent after UI is set up
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
                _nameEdit = this.FindControl<LocalizedStringEdit>("nameEdit");
                _descEdit = this.FindControl<LocalizedStringEdit>("descEdit");
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
                _iconLabel = this.FindControl<Image>("iconLabel");
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

            // Setup installation after UI is initialized
            if (_installation != null)
            {
                SetupInstallation(_installation);
                // Set installation on LocalizedStringEdit widgets
                if (_nameEdit != null)
                {
                    _nameEdit.SetInstallation(_installation);
                }
                if (_descEdit != null)
                {
                    _descEdit.SetInstallation(_installation);
                }
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
            _nameEdit = new LocalizedStringEdit();
            if (_installation != null)
            {
                _nameEdit.SetInstallation(_installation);
            }
            basicPanel.Children.Add(nameLabel);
            basicPanel.Children.Add(_nameEdit);

            // Description
            var descLabel = new TextBlock { Text = "Description:" };
            _descEdit = new LocalizedStringEdit();
            if (_installation != null)
            {
                _descEdit.SetInstallation(_installation);
            }
            basicPanel.Children.Add(descLabel);
            basicPanel.Children.Add(_descEdit);

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

            // Icon Label (shows item icon based on base item and variations)
            _iconLabel = new Image
            {
                Width = 32,
                Height = 32,
                Margin = new Avalonia.Thickness(0, 5, 0, 5)
            };
            basicPanel.Children.Add(_iconLabel);

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
            _editPropertyBtn.Click += async (s, e) => await EditSelectedProperty();
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
            // TODO: STUB - For now, programmatic UI is set up in SetupProgrammaticUI
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
                _editPropertyBtn.Click += async (s, e) => await EditSelectedProperty();
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
            // Note: Name and Description editing is handled by LocalizedStringEdit's built-in edit button
            // Matching PyKotor: LocalizedStringLineEdit has its own edit button that opens LocalizedStringDialog
            
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:98-99
            // Original: self.ui.availablePropertyList.doubleClicked.connect(self.on_available_property_list_double_clicked)
            // Original: self.ui.assignedPropertiesList.doubleClicked.connect(self.on_assigned_property_list_double_clicked)
            if (_availablePropertyList != null)
            {
                _availablePropertyList.DoubleTapped += (s, e) => OnAvailablePropertyListDoubleClicked();
            }
            if (_assignedPropertiesList != null)
            {
                _assignedPropertiesList.DoubleTapped += (s, e) => OnAssignedPropertyListDoubleClicked();
            }
            
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:82
            // Original: QShortcut("Del", self).activated.connect(self.on_del_shortcut)
            // Note: In Avalonia, we handle KeyDown event instead of QShortcut
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Avalonia.Input.Key.Delete && _assignedPropertiesList != null && _assignedPropertiesList.IsFocused)
                {
                    OnDelShortcut();
                    e.Handled = true;
                }
            };
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:167-196
        // Original: def _loadUTI(self, uti):
        private void LoadUTI(UTI uti)
        {
            _uti = uti;

            // Basic
            // Matching PyKotor implementation: self.ui.nameEdit.set_locstring(uti.name)
            if (_nameEdit != null)
            {
                _nameEdit.SetLocString(uti.Name);
            }
            // Matching PyKotor implementation: self.ui.descEdit.set_locstring(uti.description)
            if (_descEdit != null)
            {
                _descEdit.SetLocString(uti.Description);
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

            // Update icon display after loading UTI data
            UpdateIcon();
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
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:210-211
            // Original: uti.name = self.ui.nameEdit.locstring()
            // Original: uti.description = self.ui.descEdit.locstring()
            if (_nameEdit != null)
            {
                uti.Name = _nameEdit.GetLocString();
            }
            if (_descEdit != null)
            {
                uti.Description = _descEdit.GetLocString();
            }
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
                        // Create a deep copy of the property to avoid reference issues
                        var propCopy = new UTIProperty
                        {
                            PropertyName = propItem.Property.PropertyName,
                            Subtype = propItem.Property.Subtype,
                            CostTable = propItem.Property.CostTable,
                            CostValue = propItem.Property.CostValue,
                            Param1 = propItem.Property.Param1,
                            Param1Value = propItem.Property.Param1Value,
                            ChanceAppear = propItem.Property.ChanceAppear,
                            UpgradeType = propItem.Property.UpgradeType
                        };
                        uti.Properties.Add(propCopy);
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
        // Note: Name editing is now handled by LocalizedStringEdit's built-in edit button
        // The widget opens LocalizedStringDialog internally, so this method is no longer needed
        // However, we keep it for backwards compatibility if needed elsewhere
        private void EditName()
        {
            // LocalizedStringEdit handles editing internally via its edit button
            // This method is kept for compatibility but is no longer called
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:242-246
        // Original: def change_desc(self):
        // Note: Description editing is now handled by LocalizedStringEdit's built-in edit button
        // The widget opens LocalizedStringDialog internally, so this method is no longer needed
        // However, we keep it for backwards compatibility if needed elsewhere
        private void EditDescription()
        {
            // LocalizedStringEdit handles editing internally via its edit button
            // This method is kept for compatibility but is no longer called
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
        private async System.Threading.Tasks.Task EditSelectedProperty()
        {
            // Matching PyKotor implementation: if not self.ui.assignedPropertiesList.selectedItems(): return
            if (_assignedPropertiesList?.SelectedItem == null)
            {
                return;
            }

            // Matching PyKotor implementation: uti_property: UTIProperty = self.ui.assignedPropertiesList.selectedItems()[0].data(Qt.ItemDataRole.UserRole)
            if (!(_assignedPropertiesList.SelectedItem is PropertyListItem selectedItem) || selectedItem.Property == null)
            {
                return;
            }

            // Matching PyKotor implementation: dialog = PropertyEditor(self._installation, uti_property)
            var dialog = new PropertyEditorDialog(this, _installation, selectedItem.Property);
            
            // Matching PyKotor implementation: if not dialog.exec(): return
            // Use ShowDialogAsync for proper modal dialog handling
            var resultObj = await dialog.ShowDialogAsync(this);
            bool result = resultObj is bool b ? b : false;
            if (!result)
            {
                return;
            }
            
            // Matching PyKotor implementation: self.ui.assignedPropertiesList.selectedItems()[0].setData(Qt.ItemDataRole.UserRole, dialog.uti_property())
            // Matching PyKotor implementation: self.ui.assignedPropertiesList.selectedItems()[0].setText(self.property_summary(dialog.uti_property()))
            UTIProperty updatedProperty = dialog.GetUtiProperty();
            selectedItem.Property = updatedProperty;
            selectedItem.Text = PropertySummary(updatedProperty);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:270-301
        // Original: def add_selected_property(self):
        private void AddSelectedProperty()
        {
            // Matching PyKotor implementation: if not self.ui.availablePropertyList.selectedItems(): return
            if (_availablePropertyList?.SelectedItem == null)
            {
                return;
            }

            // Matching PyKotor implementation: item: QTreeWidgetItem = self.ui.availablePropertyList.selectedItems()[0]
            // Matching PyKotor implementation: property_id: int = item.data(0, Qt.ItemDataRole.UserRole)
            // Matching PyKotor implementation: subtype_id: int = item.data(0, Qt.ItemDataRole.UserRole + 1)
            if (_availablePropertyList.SelectedItem is TreeViewItem selectedItem && selectedItem.Tag is PropertyTreeItemData itemData)
            {
                int propertyId = itemData.PropertyIndex;
                int subtypeId = itemData.SubPropertyIndex;
                AddPropertyMain(propertyId, subtypeId);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:280-301
        // Original: def _add_property_main(self, property_id: int, subtype_id: int):
        private void AddPropertyMain(int propertyId, int subtypeId)
        {
            if (_installation == null)
            {
                return;
            }

            // Matching PyKotor implementation: itemprops: TwoDA | None = self._installation.ht_get_cache_2da(HTInstallation.TwoDA_ITEM_PROPERTIES)
            TwoDA itemProps = _installation.HtGetCache2DA(HTInstallation.TwoDAItemProperties);
            if (itemProps == null)
            {
                return;
            }

            // Matching PyKotor implementation: uti_property = UTIProperty()
            var utiProperty = new UTIProperty();
            // Matching PyKotor implementation: uti_property.property_name = property_id
            utiProperty.PropertyName = propertyId;
            // Matching PyKotor implementation: uti_property.subtype = subtype_id
            utiProperty.Subtype = subtypeId;

            // Matching PyKotor implementation: uti_property.cost_table = itemprops.get_row(property_id).get_integer("costtableresref", 255)
            TwoDARow propertyRow = itemProps.GetRow(propertyId);
            int? costTableNullable = propertyRow.GetInteger("costtableresref", 255);
            utiProperty.CostTable = costTableNullable ?? 255;
            
            // Matching PyKotor implementation: uti_property.cost_value = 0
            utiProperty.CostValue = 0;
            
            // Matching PyKotor implementation: uti_property.param1 = itemprops.get_row(property_id).get_integer("param1resref", 255)
            int? param1Nullable = propertyRow.GetInteger("param1resref", 255);
            utiProperty.Param1 = param1Nullable ?? 255;
            
            // Matching PyKotor implementation: uti_property.param1_value = 0
            utiProperty.Param1Value = 0;
            
            // Matching PyKotor implementation: uti_property.chance_appear = 100
            utiProperty.ChanceAppear = 100;

            // Matching PyKotor implementation: text: str = self.property_summary(uti_property)
            string text = PropertySummary(utiProperty);
            
            // Matching PyKotor implementation: item = QListWidgetItem(text)
            // Matching PyKotor implementation: item.setData(Qt.ItemDataRole.UserRole, uti_property)
            // Matching PyKotor implementation: self.ui.assignedPropertiesList.addItem(item)
            var listItem = new PropertyListItem { Text = text, Property = utiProperty };
            if (_assignedPropertiesList != null)
            {
                _assignedPropertiesList.Items.Add(listItem);
            }
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
            if (_installation == null)
            {
                return $"Property {prop.PropertyName}: Subtype {prop.Subtype}";
            }

            // Matching PyKotor implementation: prop_name: str = UTIEditor.property_name(self._installation, uti_property.property_name)
            string propName = GetPropertyName(_installation, prop.PropertyName);
            
            // Matching PyKotor implementation: subprop_name: str | None = UTIEditor.subproperty_name(self._installation, uti_property.property_name, uti_property.subtype)
            string subpropName = GetSubpropertyName(_installation, prop.PropertyName, prop.Subtype);
            
            // Matching PyKotor implementation: cost_name: str | None = UTIEditor.cost_name(self._installation, uti_property.cost_table, uti_property.cost_value)
            string costName = CostName(_installation, prop.CostTable, prop.CostValue);

            // Matching PyKotor implementation: if cost_name and subprop_name: return f"{prop_name}: {subprop_name} [{cost_name}]"
            if (!string.IsNullOrEmpty(costName) && !string.IsNullOrEmpty(subpropName))
            {
                return $"{propName}: {subpropName} [{costName}]";
            }
            
            // Matching PyKotor implementation: if subprop_name: return f"{prop_name}: {subprop_name}"
            if (!string.IsNullOrEmpty(subpropName))
            {
                return $"{propName}: {subpropName}";
            }
            
            // Matching PyKotor implementation: if cost_name: return f"{prop_name}: [{cost_name}]"
            if (!string.IsNullOrEmpty(costName))
            {
                return $"{propName}: [{costName}]";
            }
            
            // Matching PyKotor implementation: return f"{prop_name}"
            return propName;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:488-514
        // Original: @staticmethod def cost_name(installation: HTInstallation, cost: int, value: int) -> str | None:
        public static string CostName(HTInstallation installation, int cost, int value)
        {
            TwoDA costTableList = installation.HtGetCache2DA(HTInstallation.TwoDAIprpCosttable);
            if (costTableList == null)
            {
                System.Console.WriteLine("Failed to retrieve IPRP_COSTTABLE 2DA.");
                return null;
            }

            string costtableName = costTableList.GetCellString(cost, "name");
            if (string.IsNullOrEmpty(costtableName))
            {
                System.Console.WriteLine($"Failed to retrieve costtable 'name' for cost '{cost}'.");
                return null;
            }

            TwoDA costtable = installation.HtGetCache2DA(costtableName);
            if (costtable == null)
            {
                System.Console.WriteLine($"Failed to retrieve '{costtableName}' 2DA.");
                return null;
            }

            try
            {
                TwoDARow row = costtable.GetRow(value);
                int? stringref = row.GetInteger("name");
                if (stringref.HasValue)
                {
                    return installation.GetStringFromStringRef(stringref.Value);
                }
            }
            catch (Exception)
            {
                System.Console.WriteLine("Could not get the costtable 2DA row/value");
            }
            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:516-557
        // Original: @staticmethod def param_name(installation: HTInstallation, paramtable: int, param: int) -> str | None:
        public static string ParamName(HTInstallation installation, int paramtable, int param)
        {
            // Get the IPRP_PARAMTABLE TwoDA
            TwoDA paramtableList = installation.HtGetCache2DA(HTInstallation.TwoDAIprpParamtable);
            if (paramtableList == null)
            {
                System.Console.WriteLine("Failed to retrieve IPRP_PARAMTABLE 2DA.");
                return null;
            }

            try
            {
                // Get the specific parameter table TwoDA
                string tableResref = paramtableList.GetCellString(paramtable, "tableresref");
                if (string.IsNullOrEmpty(tableResref))
                {
                    System.Console.WriteLine($"Failed to retrieve table_resref for paramtable: '{paramtable}'.");
                    return null;
                }

                TwoDA paramtable2da = installation.HtGetCache2DA(tableResref);
                if (paramtable2da == null)
                {
                    System.Console.WriteLine($"Failed to retrieve 2DA file: {tableResref}.");
                    return null;
                }

                // Get the string reference for the parameter name
                TwoDARow paramRow = paramtable2da.GetRow(param);
                int? stringref = paramRow.GetInteger("name");
                if (stringref.HasValue)
                {
                    return installation.GetStringFromStringRef(stringref.Value);
                }
                else
                {
                    System.Console.WriteLine($"Failed to get 'name' value for param '{param}' in '{tableResref}'");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Exception getting param name: {ex.Message}");
            }
            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:427-435
        // Original: def on_update_icon(self, *args, **kwargs):
        private void UpdateIcon()
        {
            if (_installation == null)
            {
                return;
            }

            // Matching PyKotor implementation: base_item: int = self.ui.baseSelect.currentIndex()
            int baseItem = _baseSelect?.SelectedIndex ?? 0;
            
            // Matching PyKotor implementation: model_variation: int = self.ui.modelVarSpin.value()
            int modelVariation = _modelVarSpin?.Value != null ? (int)_modelVarSpin.Value : 0;
            
            // Matching PyKotor implementation: texture_variation: int = self.ui.textureVarSpin.value()
            int textureVariation = _textureVarSpin?.Value != null ? (int)_textureVarSpin.Value : 0;

            // Matching PyKotor implementation: pixmap: QPixmap | None = self._installation.get_item_icon(base_item, model_variation, texture_variation)
            var bitmap = _installation.GetItemIcon(baseItem, modelVariation, textureVariation);
            
            // Matching PyKotor implementation: if pixmap is not None:
            // Matching PyKotor implementation: self.ui.iconLabel.setPixmap(pixmap)
            if (bitmap != null && _iconLabel != null)
            {
                _iconLabel.Source = bitmap;
            }
            
            // Matching PyKotor implementation: self.ui.iconLabel.setToolTip(self._generate_icon_tooltip(as_html=True))
            if (_iconLabel != null)
            {
                string tooltip = GenerateIconTooltip(true);
                Avalonia.Controls.ToolTip.SetTip(_iconLabel, tooltip);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:332-352
        // Original: def _generate_icon_tooltip(self, *, as_html: bool = False) -> str:
        private string GenerateIconTooltip(bool asHtml = false)
        {
            if (_installation == null)
            {
                return "";
            }

            // Matching PyKotor implementation: base_item: int = self.ui.baseSelect.currentIndex()
            int baseItem = _baseSelect?.SelectedIndex ?? 0;
            
            // Matching PyKotor implementation: model_variation: int = self.ui.modelVarSpin.value()
            int modelVariation = _modelVarSpin?.Value != null ? (int)_modelVarSpin.Value : 0;
            
            // Matching PyKotor implementation: texture_variation: int = self.ui.textureVarSpin.value()
            int textureVariation = _textureVarSpin?.Value != null ? (int)_textureVarSpin.Value : 0;

            // Matching PyKotor implementation: base_item_name: str = self._installation.get_item_base_name(base_item)
            string baseItemName = _installation.GetItemBaseName(baseItem);
            
            // Matching PyKotor implementation: model_var_name: str = self._installation.get_model_var_name(model_variation)
            string modelVarName = _installation.GetModelVarName(modelVariation);
            
            // Matching PyKotor implementation: texture_var_name: str = self._installation.get_texture_var_name(texture_variation)
            string textureVarName = _installation.GetTextureVarName(textureVariation);
            
            // Matching PyKotor implementation: icon_path: str = self._installation.get_item_icon_path(base_item, model_variation, texture_variation)
            string iconPath = _installation.GetItemIconPath(baseItem, modelVariation, textureVariation);

            if (asHtml)
            {
                // Matching PyKotor implementation: tooltip = f"<b>Base Item:</b> {base_item_name} (ID: {base_item})<br>" ...
                return $"<b>Base Item:</b> {baseItemName} (ID: {baseItem})<br>" +
                       $"<b>Model Variation:</b> {modelVarName} (ID: {modelVariation})<br>" +
                       $"<b>Texture Variation:</b> {textureVarName} (ID: {textureVariation})<br>" +
                       $"<b>Icon Name:</b> {iconPath}";
            }
            else
            {
                // Matching PyKotor implementation: tooltip = f"Base Item: {base_item_name} (ID: {base_item})\n" ...
                return $"Base Item: {baseItemName} (ID: {baseItem})\n" +
                       $"Model Variation: {modelVarName} (ID: {modelVariation})\n" +
                       $"Texture Variation: {textureVarName} (ID: {textureVariation})\n" +
                       $"Icon Name: {iconPath}";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:106-154
        // Original: def _setup_installation(self, installation):
        private void SetupInstallation(HTInstallation installation)
        {
            if (installation == null)
            {
                return;
            }

            _installation = installation;

            // Set installation on LocalizedStringEdit widgets
            if (_nameEdit != null)
            {
                _nameEdit.SetInstallation(installation);
            }
            if (_descEdit != null)
            {
                _descEdit.SetInstallation(installation);
            }

            // Matching PyKotor implementation: required: list[str] = [HTInstallation.TwoDA_BASEITEMS, HTInstallation.TwoDA_ITEM_PROPERTIES]
            var required = new List<string> { HTInstallation.TwoDABaseitems, HTInstallation.TwoDAItemProperties };
            installation.HtBatchCache2DA(required);

            // Matching PyKotor implementation: baseitems: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_BASEITEMS)
            TwoDA baseitems = installation.HtGetCache2DA(HTInstallation.TwoDABaseitems);
            if (baseitems == null)
            {
                System.Console.WriteLine("Failed to retrieve BASEITEMS 2DA.");
            }
            else
            {
                // Matching PyKotor implementation: self.ui.baseSelect.set_items(baseitems.get_column("label"))
                if (_baseSelect != null)
                {
                    _baseSelect.Items.Clear();
                    for (int i = 0; i < baseitems.GetHeight(); i++)
                    {
                        string label = baseitems.GetCellString(i, "label") ?? "";
                        _baseSelect.Items.Add(label);
                    }
                }
            }

            // Matching PyKotor implementation: self.ui.availablePropertyList.clear()
            if (_availablePropertyList == null)
            {
                System.Console.WriteLine("AvailablePropertyList is null - cannot populate properties");
                return;
            }

            _availablePropertyList.Items.Clear();

            // Matching PyKotor implementation: item_properties: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_ITEM_PROPERTIES)
            TwoDA itemProperties = installation.HtGetCache2DA(HTInstallation.TwoDAItemProperties);
            if (itemProperties == null)
            {
                System.Console.WriteLine("Failed to retrieve ITEM_PROPERTIES 2DA.");
                return;
            }

            if (itemProperties != null)
            {
                // Matching PyKotor implementation: for i in range(item_properties.get_height()):
                for (int i = 0; i < itemProperties.GetHeight(); i++)
                {
                    // Matching PyKotor implementation: prop_name: str = UTIEditor.property_name(installation, i)
                    string propName = GetPropertyName(installation, i);
                    
                    // Matching PyKotor implementation: item = QTreeWidgetItem([prop_name])
                    var item = new TreeViewItem
                    {
                        Header = propName
                    };
                    // Store property index and subproperty index in Tag (using a simple object)
                    item.Tag = new PropertyTreeItemData { PropertyIndex = i, SubPropertyIndex = i };

                    // Matching PyKotor implementation: subtype_resname: str = item_properties.get_cell(i, "subtyperesref")
                    string subtypeResname = itemProperties.GetCellString(i, "subtyperesref") ?? "";
                    if (string.IsNullOrEmpty(subtypeResname))
                    {
                        // No subtype, just add the item
                        if (_availablePropertyList != null)
                        {
                            _availablePropertyList.Items.Add(item);
                        }
                        continue;
                    }

                    // Matching PyKotor implementation: subtype: TwoDA | None = installation.ht_get_cache_2da(subtype_resname)
                    TwoDA subtype = installation.HtGetCache2DA(subtypeResname);
                    if (subtype == null)
                    {
                        System.Console.WriteLine($"Failed to retrieve subtype '{subtypeResname}' for property name '{propName}' at index {i}. Skipping...");
                        if (_availablePropertyList != null)
                        {
                            _availablePropertyList.Items.Add(item);
                        }
                        continue;
                    }

                    // Matching PyKotor implementation: for j in range(subtype.get_height()):
                    var childItems = new List<TreeViewItem>();
                    for (int j = 0; j < subtype.GetHeight(); j++)
                    {
                        // Matching PyKotor implementation: name: None | str = UTIEditor.subproperty_name(installation, i, j)
                        string name = GetSubpropertyName(installation, i, j);
                        if (string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name))
                        {
                            // Matching PyKotor implementation: if not name or not name.strip(): continue
                            continue;
                        }

                        // Matching PyKotor implementation: child = QTreeWidgetItem([name])
                        var child = new TreeViewItem
                        {
                            Header = name
                        };
                        // Matching PyKotor implementation: child.setData(0, Qt.ItemDataRole.UserRole, i)
                        // Matching PyKotor implementation: child.setData(0, Qt.ItemDataRole.UserRole + 1, j)
                        child.Tag = new PropertyTreeItemData { PropertyIndex = i, SubPropertyIndex = j };
                        childItems.Add(child);
                    }
                    item.ItemsSource = childItems;
                    if (_availablePropertyList != null)
                    {
                        _availablePropertyList.Items.Add(item);
                    }
                }
            }
        }

        // Helper class to store property tree item data
        private class PropertyTreeItemData
        {
            public int PropertyIndex { get; set; }
            public int SubPropertyIndex { get; set; }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:451-464
        // Original: @staticmethod def property_name(installation: HTInstallation, prop: int) -> str:
        public static string GetPropertyName(HTInstallation installation, int prop)
        {
            // Matching PyKotor implementation: properties: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_ITEM_PROPERTIES)
            TwoDA properties = installation.HtGetCache2DA(HTInstallation.TwoDAItemProperties);
            if (properties == null)
            {
                System.Console.WriteLine("Failed to retrieve ITEM_PROPERTIES 2DA.");
                return "Unknown";
            }

            // Matching PyKotor implementation: stringref: int | None = properties.get_row(prop).get_integer("name")
            TwoDARow row = properties.GetRow(prop);
            int? stringrefNullable = row.GetInteger("name");
            if (!stringrefNullable.HasValue)
            {
                System.Console.WriteLine($"Failed to retrieve name stringref for property {prop}.");
                return "Unknown";
            }
            int stringref = stringrefNullable.Value;

            // Matching PyKotor implementation: return installation.talktable().string(stringref)
            return installation.GetStringFromStringRef(stringref);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:466-485
        // Original: @staticmethod def subproperty_name(installation: HTInstallation, prop: int, subprop: int) -> None | str:
        [CanBeNull]
        public static string GetSubpropertyName(HTInstallation installation, int prop, int subprop)
        {
            // Matching PyKotor implementation: properties: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_ITEM_PROPERTIES)
            TwoDA properties = installation.HtGetCache2DA(HTInstallation.TwoDAItemProperties);
            if (properties == null)
            {
                System.Console.WriteLine("Failed to retrieve ITEM_PROPERTIES 2DA.");
                return null;
            }

            // Matching PyKotor implementation: subtype_resname: str | None = properties.get_cell(prop, "subtyperesref")
            TwoDARow propRow = properties.GetRow(prop);
            string subtypeResname = propRow.GetString("subtyperesref") ?? "";
            if (string.IsNullOrEmpty(subtypeResname))
            {
                System.Console.WriteLine($"Failed to retrieve subtype_resname for property {prop}.");
                return null;
            }

            // Matching PyKotor implementation: subproperties: TwoDA | None = installation.ht_get_cache_2da(subtype_resname)
            TwoDA subproperties = installation.HtGetCache2DA(subtypeResname);
            if (subproperties == null)
            {
                return null;
            }

            // Matching PyKotor implementation: header_strref: Literal["name", "string_ref"] = "name" if "name" in subproperties.get_headers() else "string_ref"
            string headerStrref = subproperties.GetHeaders().Contains("name") ? "name" : "string_ref";

            // Matching PyKotor implementation: name_strref: int | None = subproperties.get_row(subprop).get_integer(header_strref)
            TwoDARow subpropRow = subproperties.GetRow(subprop);
            int? nameStrrefNullable = subpropRow.GetInteger(headerStrref);
            if (nameStrrefNullable.HasValue)
            {
                // Matching PyKotor implementation: return installation.talktable().string(name_strref)
                return installation.GetStringFromStringRef(nameStrrefNullable.Value);
            }

            // Matching PyKotor implementation: return subproperties.get_cell(subprop, "label") if name_strref is None else installation.talktable().string(name_strref)
            return subpropRow.GetString("label") ?? "";
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:437-441
        // Original: def on_available_property_list_double_clicked(self):
        private void OnAvailablePropertyListDoubleClicked()
        {
            // Matching PyKotor implementation: for item in self.ui.availablePropertyList.selectedItems():
            // Matching PyKotor implementation:     if item.childCount() != 0: continue
            // Matching PyKotor implementation:     self.add_selected_property()
            if (_availablePropertyList?.SelectedItem is TreeViewItem selectedItem)
            {
                // Check if it's a leaf node (no children)
                bool isLeafNode = selectedItem.ItemsSource == null || 
                                  (selectedItem.ItemsSource is System.Collections.IList list && list.Count == 0);
                
                if (isLeafNode)
                {
                    AddSelectedProperty();
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:443-444
        // Original: def on_assigned_property_list_double_clicked(self):
        private async void OnAssignedPropertyListDoubleClicked()
        {
            // Matching PyKotor implementation: self.edit_selected_property()
            await EditSelectedProperty();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/uti.py:446-449
        // Original: def on_del_shortcut(self):
        private void OnDelShortcut()
        {
            // Matching PyKotor implementation: if not self.ui.assignedPropertiesList.hasFocus(): return
            // Matching PyKotor implementation: self.remove_selected_property()
            if (_assignedPropertiesList != null && _assignedPropertiesList.IsFocused)
            {
                RemoveSelectedProperty();
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
