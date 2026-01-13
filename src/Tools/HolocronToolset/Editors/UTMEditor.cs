using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.Capsule;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Widgets;
using InventoryItem = BioWare.NET.Common.InventoryItem;
using GFFAuto = BioWare.NET.Resource.Formats.GFF.GFFAuto;
using UTM = BioWare.NET.Resource.Formats.GFF.Generics.UTM.UTM;
using UTMHelpers = BioWare.NET.Resource.Formats.GFF.Generics.UTM.UTMHelpers;
using UTMItem = BioWare.NET.Resource.Formats.GFF.Generics.UTM.UTMItem;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:27
    // Original: class UTMEditor(Editor):
    public class UTMEditor : Editor
    {
        private UTM _utm;

        // UI Controls - Basic
        private LocalizedStringEdit _nameEdit;
        // Matching PyKotor implementation: Expose nameEdit for testing (Python uses editor.ui.nameEdit)
        public LocalizedStringEdit NameEdit => _nameEdit;
        private Button _nameEditBtn;
        private TextBox _tagEdit;
        // Matching PyKotor implementation: Expose tagEdit for testing (Python uses editor.ui.tagEdit)
        public TextBox TagEdit => _tagEdit;
        private Button _tagGenerateBtn;
        private TextBox _resrefEdit;
        // Matching PyKotor implementation: Expose resrefEdit for testing
        public TextBox ResrefEdit => _resrefEdit;
        private Button _resrefGenerateBtn;
        private NumericUpDown _idSpin;
        // Matching PyKotor implementation: Expose idSpin for testing
        public NumericUpDown IdSpin => _idSpin;
        private Button _inventoryButton;

        // UI Controls - Pricing
        private NumericUpDown _markUpSpin;
        // Matching PyKotor implementation: Expose markUpSpin for testing
        public NumericUpDown MarkUpSpin => _markUpSpin;
        private NumericUpDown _markDownSpin;
        // Matching PyKotor implementation: Expose markDownSpin for testing
        public NumericUpDown MarkDownSpin => _markDownSpin;

        // UI Controls - Store
        private TextBox _onOpenEdit;
        // Matching PyKotor implementation: Expose onOpenEdit for testing
        public TextBox OnOpenEdit => _onOpenEdit;
        private ComboBox _storeFlagSelect;
        // Matching PyKotor implementation: Expose storeFlagSelect for testing
        public ComboBox StoreFlagSelect => _storeFlagSelect;

        // UI Controls - Comments
        private TextBox _commentsEdit;
        // Matching PyKotor implementation: Expose commentsEdit for testing
        public TextBox CommentsEdit => _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:28-68
        // Original: def __init__(self, parent, installation):
        public UTMEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Merchant Editor", "merchant",
                new[] { ResourceType.UTM, ResourceType.BTM },
                new[] { ResourceType.UTM, ResourceType.BTM },
                installation)
        {
            _installation = installation;
            _utm = new UTM();

            InitializeComponent();
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

                // Try to find controls from XAML
                _nameEdit = this.FindControl<LocalizedStringEdit>("nameEdit");
                _nameEditBtn = this.FindControl<Button>("nameEditBtn");
                _tagEdit = this.FindControl<TextBox>("tagEdit");
                _tagGenerateBtn = this.FindControl<Button>("tagGenerateButton");
                _resrefEdit = this.FindControl<TextBox>("resrefEdit");
                _resrefGenerateBtn = this.FindControl<Button>("resrefGenerateButton");
                _idSpin = this.FindControl<NumericUpDown>("idSpin");
                _inventoryButton = this.FindControl<Button>("inventoryButton");
                _markUpSpin = this.FindControl<NumericUpDown>("markUpSpin");
                _markDownSpin = this.FindControl<NumericUpDown>("markDownSpin");
                _onOpenEdit = this.FindControl<TextBox>("onOpenEdit");
                _storeFlagSelect = this.FindControl<ComboBox>("storeFlagSelect");
                // Ensure ComboBox has items populated (even if loaded from XAML)
                if (_storeFlagSelect != null && _storeFlagSelect.Items.Count == 0)
                {
                    _storeFlagSelect.Items.Add("Only Buy");
                    _storeFlagSelect.Items.Add("Only Sell");
                    _storeFlagSelect.Items.Add("Buy and Sell");
                }
                _commentsEdit = this.FindControl<TextBox>("commentsEdit");

                // Check if all critical controls were found
                if (_nameEdit == null || _tagEdit == null || _resrefEdit == null || _idSpin == null)
                {
                    xamlLoaded = false; // Some controls missing, use programmatic UI
                }
            }
            catch
            {
                // XAML not available or controls not found - will use programmatic UI
                xamlLoaded = false;
            }

            if (!xamlLoaded)
            {
                SetupProgrammaticUI();
                // Setup signals after programmatic UI is created
                SetupSignals();
            }
            else
            {
                // XAML loaded, set up signals
                SetupSignals();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:70-74
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
            if (_inventoryButton != null)
            {
                _inventoryButton.Click += (s, e) => OpenInventory();
            }
            if (_nameEditBtn != null)
            {
                _nameEditBtn.Click += (s, e) => ChangeName();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:76-93
        // Original: def _setup_installation(self, installation):
        private void SetupInstallation(HTInstallation installation)
        {
            _installation = installation;
            if (_nameEdit != null)
            {
                _nameEdit.SetInstallation(installation);
            }
        }

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
            _nameEditBtn = new Button { Content = "Edit Name" };
            _nameEditBtn.Click += (s, e) => ChangeName();
            basicPanel.Children.Add(nameLabel);
            basicPanel.Children.Add(_nameEdit);
            basicPanel.Children.Add(_nameEditBtn);

            // Tag
            var tagLabel = new TextBlock { Text = "Tag:" };
            _tagEdit = new TextBox();
            _tagGenerateBtn = new Button { Content = "-" };
            _tagGenerateBtn.Click += (s, e) => GenerateTag();
            var tagPanel = new StackPanel { Orientation = Orientation.Horizontal };
            tagPanel.Children.Add(_tagEdit);
            tagPanel.Children.Add(_tagGenerateBtn);
            basicPanel.Children.Add(tagLabel);
            basicPanel.Children.Add(tagPanel);

            // ResRef
            var resrefLabel = new TextBlock { Text = "ResRef:" };
            _resrefEdit = new TextBox { MaxLength = 16 };
            _resrefGenerateBtn = new Button { Content = "-" };
            _resrefGenerateBtn.Click += (s, e) => GenerateResref();
            var resrefPanel = new StackPanel { Orientation = Orientation.Horizontal };
            resrefPanel.Children.Add(_resrefEdit);
            resrefPanel.Children.Add(_resrefGenerateBtn);
            basicPanel.Children.Add(resrefLabel);
            basicPanel.Children.Add(resrefPanel);

            // ID
            var idLabel = new TextBlock { Text = "ID:" };
            _idSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue };
            basicPanel.Children.Add(idLabel);
            basicPanel.Children.Add(_idSpin);

            // Inventory Button
            _inventoryButton = new Button { Content = "Edit Inventory" };
            _inventoryButton.Click += (s, e) => OpenInventory();
            basicPanel.Children.Add(_inventoryButton);

            basicGroup.Content = basicPanel;
            mainPanel.Children.Add(basicGroup);

            // Pricing Group
            var pricingGroup = new Expander { Header = "Pricing", IsExpanded = true };
            var pricingPanel = new StackPanel { Orientation = Orientation.Vertical };

            var markUpLabel = new TextBlock { Text = "Mark Up:" };
            _markUpSpin = new NumericUpDown { Minimum = 0, Maximum = 1000000 };
            var markDownLabel = new TextBlock { Text = "Mark Down:" };
            _markDownSpin = new NumericUpDown { Minimum = 0, Maximum = 1000000 };

            pricingPanel.Children.Add(markUpLabel);
            pricingPanel.Children.Add(_markUpSpin);
            pricingPanel.Children.Add(markDownLabel);
            pricingPanel.Children.Add(_markDownSpin);

            pricingGroup.Content = pricingPanel;
            mainPanel.Children.Add(pricingGroup);

            // Store Group
            var storeGroup = new Expander { Header = "Store", IsExpanded = true };
            var storePanel = new StackPanel { Orientation = Orientation.Vertical };

            var onOpenLabel = new TextBlock { Text = "OnOpenStore:" };
            _onOpenEdit = new TextBox { MaxLength = 16 };
            var storeLabel = new TextBlock { Text = "Store:" };
            _storeFlagSelect = new ComboBox();
            _storeFlagSelect.Items.Add("Only Buy");
            _storeFlagSelect.Items.Add("Only Sell");
            _storeFlagSelect.Items.Add("Buy and Sell");

            storePanel.Children.Add(onOpenLabel);
            storePanel.Children.Add(_onOpenEdit);
            storePanel.Children.Add(storeLabel);
            storePanel.Children.Add(_storeFlagSelect);

            storeGroup.Content = storePanel;
            mainPanel.Children.Add(storeGroup);

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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:95-105
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            // Matching PyKotor implementation: utm: UTM = read_utm(data); self._loadUTM(utm)
            var gff = GFF.FromBytes(data);
            _utm = UTMHelpers.ConstructUtm(gff);
            LoadUTM(_utm);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:107-136
        // Original: def _loadUTM(self, utm: UTM):
        private void LoadUTM(UTM utm)
        {
            // Matching PyKotor implementation: self._utm = utm
            _utm = utm;

            // Basic
            if (_nameEdit != null)
            {
                _nameEdit.SetLocString(utm.Name);
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = utm.Tag;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = utm.ResRef.ToString();
            }
            if (_idSpin != null)
            {
                _idSpin.Value = (decimal)utm.Id;
            }
            if (_markUpSpin != null)
            {
                _markUpSpin.Value = (decimal)utm.MarkUp;
            }
            if (_markDownSpin != null)
            {
                _markDownSpin.Value = (decimal)utm.MarkDown;
            }
            if (_onOpenEdit != null)
            {
                _onOpenEdit.Text = utm.OnOpenScript.ToString();
            }
            if (_storeFlagSelect != null)
            {
                // Matching PyKotor implementation: self.ui.storeFlagSelect.setCurrentIndex((int(utm.can_buy) + int(utm.can_sell) * 2) - 1)
                int index = (utm.CanBuy ? 1 : 0) + (utm.CanSell ? 2 : 0) - 1;
                if (index >= 0 && index < _storeFlagSelect.Items.Count)
                {
                    _storeFlagSelect.SelectedIndex = index;
                }
            }

            // Comments
            if (_commentsEdit != null)
            {
                _commentsEdit.Text = utm.Comment;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:138-173
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation: utm: UTM = deepcopy(self._utm)
            var utm = CopyUTM(_utm);

            // Basic - read from UI controls (matching Python which always reads from UI)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:155-167
            utm.Name = _nameEdit?.GetLocString() ?? utm.Name ?? LocalizedString.FromInvalid();
            utm.Tag = _tagEdit?.Text ?? utm.Tag ?? "";
            // Matching PyKotor implementation: utm.resref = ResRef(self.ui.resrefEdit.text())
            // Python always reads from UI, even if empty (creates blank ResRef)
            utm.ResRef = _resrefEdit != null ? new ResRef(_resrefEdit.Text ?? "") : utm.ResRef;
            // Matching PyKotor implementation: utm.id = self.ui.idSpin.value()
            // Python always reads from UI, even if 0
            // Note: NumericUpDown.Value is decimal?, but Python's QSpinBox.value() always returns an int
            // Matching PyKotor implementation: Python always reads from UI without null checks
            // Python: utm.id = self.ui.idSpin.value() - always returns int, never None
            // Matching PyKotor implementation: Python always reads from UI
            // Python: utm.id = self.ui.idSpin.value() - always returns int, never None
            if (_idSpin != null)
            {
                // Always read from UI (matching Python behavior)
                // Directly read from _idSpin to match Python's direct UI access
                // Python's QSpinBox.value() always returns an int, never None
                // NumericUpDown.Value is decimal?, so we need to handle null
                var value = _idSpin.Value;
                utm.Id = value.HasValue ? (int)value.Value : 0;
            }
            // Matching PyKotor implementation: utm.mark_up = self.ui.markUpSpin.value()
            if (_markUpSpin != null && _markUpSpin.Value.HasValue)
            {
                utm.MarkUp = (int)_markUpSpin.Value.Value;
            }
            else if (_markUpSpin != null)
            {
                utm.MarkUp = 0;
            }
            // Matching PyKotor implementation: utm.mark_down = self.ui.markDownSpin.value()
            if (_markDownSpin != null && _markDownSpin.Value.HasValue)
            {
                utm.MarkDown = (int)_markDownSpin.Value.Value;
            }
            else if (_markDownSpin != null)
            {
                utm.MarkDown = 0;
            }
            // Matching PyKotor implementation: utm.on_open = ResRef(self.ui.onOpenEdit.text())
            // Python always reads from UI, even if empty (creates blank ResRef)
            utm.OnOpenScript = _onOpenEdit != null ? new ResRef(_onOpenEdit.Text ?? "") : utm.OnOpenScript;

            // Matching PyKotor implementation: utm.can_buy = bool((self.ui.storeFlagSelect.currentIndex() + 1) & 1)
            // Matching PyKotor implementation: utm.can_sell = bool((self.ui.storeFlagSelect.currentIndex() + 1) & 2)
            // Python always reads from UI without null checks - currentIndex() returns -1 if nothing selected
            // In Avalonia, SelectedIndex can be 0 (first item), so we need to check for >= 0, not > 0
            int index = -1;
            if (_storeFlagSelect != null)
            {
                // SelectedIndex can be 0 (first item), so we check >= 0
                index = _storeFlagSelect.SelectedIndex >= 0 ? _storeFlagSelect.SelectedIndex : -1;
            }
            int flagValue = index + 1; // -1 + 1 = 0, 0 + 1 = 1, 1 + 1 = 2, 2 + 1 = 3
            utm.CanBuy = (flagValue & 1) != 0;
            utm.CanSell = (flagValue & 2) != 0;

            // Comments
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:167
            utm.Comment = _commentsEdit?.Text ?? utm.Comment ?? "";

            // Build GFF
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:169-173
            var game = _installation?.Game ?? Game.K2;
            var gff = UTMHelpers.DismantleUtm(utm, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTM);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation: Deep copy helper
        // Original: utm: UTM = deepcopy(self._utm)
        private UTM CopyUTM(UTM source)
        {
            // Deep copy LocalizedString objects (they're reference types)
            LocalizedString copyName = source.Name != null
                ? new LocalizedString(source.Name.StringRef, new Dictionary<int, string>(GetSubstringsDict(source.Name)))
                : null;

            var copy = new UTM
            {
                ResRef = source.ResRef,
                Name = copyName,
                Tag = source.Tag,
                MarkUp = source.MarkUp,
                MarkDown = source.MarkDown,
                OnOpenScript = source.OnOpenScript,
                Comment = source.Comment,
                Id = source.Id,
                CanBuy = source.CanBuy,
                CanSell = source.CanSell
            };

            // Copy items
            foreach (var item in source.Items)
            {
                copy.Items.Add(new UTMItem
                {
                    ResRef = item.ResRef,
                    Infinite = item.Infinite,
                    Droppable = item.Droppable
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:175-177
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _utm = new UTM();
            LoadUTM(_utm);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:179-182
        // Original: def change_name(self):
        private void ChangeName()
        {
            if (_installation == null) return;
            var dialog = new LocalizedStringDialog(this, _installation, _utm.Name);
            if (dialog.ShowDialog())
            {
                _utm.Name = dialog.LocString;
                if (_nameEdit != null)
                {
                    _nameEdit.SetLocString(_utm.Name);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:184-187
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:189-193
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = !string.IsNullOrEmpty(base._resname) ? base._resname : "m00xx_mer_000";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:195-221
        // Original: def open_inventory(self):
        private void OpenInventory()
        {
            if (_installation == null) return;

            // Matching PyKotor implementation: capsules: list[Capsule] = []
            var capsules = new List<Capsule>();

            try
            {
                // Matching PyKotor implementation: root: str = Module.filepath_to_root(self._filepath)
                string root = null;
                if (!string.IsNullOrEmpty(base._filepath))
                {
                    root = Module.FilepathToRoot(base._filepath);
                }

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utm.py:199-205
                // Original: case_root = root.casefold()
                // Original: module_names: CaseInsensitiveDict[str] = self._installation.module_names()
                // Original: filepath_str = str(self._filepath)
                // Original: capsulesPaths: list[str] = [path for path in module_names if case_root in path and path != filepath_str]
                // Original: capsules.extend([Capsule(self._installation.module_path() / path) for path in capsulesPaths])
                if (root != null)
                {
                    string caseRoot = root.ToLowerInvariant();
                    var moduleNames = _installation.ModuleNames();
                    string filepathStr = base._filepath ?? "";
                    string filepathFilename = !string.IsNullOrEmpty(filepathStr) ? System.IO.Path.GetFileName(filepathStr) : "";
                    
                    foreach (var kvp in moduleNames)
                    {
                        // kvp.Key is the module filename (e.g., "danm13.rim"), kvp.Value is the area name
                        // Matching PyKotor: for path in module_names - iterates over keys (filenames)
                        string moduleFilename = kvp.Key;
                        string moduleFilenameLower = moduleFilename.ToLowerInvariant();
                        
                        // Check if root is contained in module filename and it's not the same as the current filepath
                        // Matching PyKotor: case_root in path and path != filepath_str
                        if (moduleFilenameLower.Contains(caseRoot) && moduleFilename != filepathFilename)
                        {
                            // Matching PyKotor implementation: Capsule(self._installation.module_path() / path)
                            string fullModulePath = System.IO.Path.Combine(_installation.ModulePath(), moduleFilename);
                            if (File.Exists(fullModulePath))
                            {
                                try
                                {
                                    var capsule = new Capsule(fullModulePath, createIfNotExist: false);
                                    capsules.Add(capsule);
                                }
                                catch
                                {
                                    // Skip invalid capsule files
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Matching PyKotor implementation: print(format_exception_with_variables(e, message="This exception has been suppressed."))
                System.Console.WriteLine($"Exception suppressed: {ex.Message}");
            }

            // Convert UTMItem list to InventoryItem list for the dialog
            // Matching PyKotor implementation: inventoryEditor = InventoryEditor(..., self._utm.inventory, ...)
            var inventoryItems = new List<InventoryItem>();
            foreach (var utmItem in _utm.Items)
            {
                inventoryItems.Add(new InventoryItem(utmItem.ResRef));
            }

            // Matching PyKotor implementation: inventoryEditor = InventoryEditor(...)
            var inventoryEditor = new InventoryDialog(
                this,
                _installation,
                capsules,
                new List<string>(), // folders parameter
                inventoryItems,
                new Dictionary<EquipmentSlot, InventoryItem>(), // equipment parameter
                droid: false,
                hideEquipment: true,
                isStore: true
            );

            if (inventoryEditor.ShowDialog())
            {
                // Matching PyKotor implementation: self._utm.inventory = inventoryEditor.inventory
                // Convert InventoryItem list back to UTMItem list
                _utm.Items.Clear();
                if (inventoryEditor.Inventory != null)
                {
                    foreach (var invItem in inventoryEditor.Inventory)
                    {
                        var utmItem = new UTMItem
                        {
                            ResRef = invItem.ResRef
                        };
                        _utm.Items.Add(utmItem);
                    }
                }
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
