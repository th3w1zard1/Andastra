using Andastra.Parsing.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing;
using Andastra.Parsing.Extract;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using DLGType = Andastra.Parsing.Resource.Generics.DLG.DLG;
using DLGHelper = Andastra.Parsing.Resource.Generics.DLG.DLGHelper;
using Andastra.Parsing.Formats.Capsule;
using Avalonia.Controls.Primitives;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Utils;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;
using Window = Avalonia.Controls.Window;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:38
    // Original: class UTPEditor(Editor):
    public class UTPEditor : Editor
    {
        private UTP _utp;

        // UI Controls - Basic
        private TextBox _nameEdit;
        private Button _nameEditBtn;
        private TextBox _tagEdit;
        private Button _tagGenerateBtn;
        private TextBox _resrefEdit;
        private Button _resrefGenerateBtn;
        private ComboBox _appearanceSelect;
        private TextBox _conversationEdit;
        private Button _conversationModifyBtn;
        private Button _inventoryBtn;
        private TextBlock _inventoryCountLabel;

        // UI Controls - Advanced
        private CheckBox _hasInventoryCheckbox;
        private CheckBox _partyInteractCheckbox;
        private CheckBox _useableCheckbox;
        private CheckBox _min1HpCheckbox;
        private CheckBox _plotCheckbox;
        private CheckBox _staticCheckbox;
        private CheckBox _notBlastableCheckbox;
        private ComboBox _factionSelect;
        private NumericUpDown _animationStateSpin;
        private NumericUpDown _currentHpSpin;
        private NumericUpDown _maxHpSpin;
        private NumericUpDown _hardnessSpin;
        private NumericUpDown _fortitudeSpin;
        private NumericUpDown _reflexSpin;
        private NumericUpDown _willSpin;

        // UI Controls - Lock
        private CheckBox _needKeyCheckbox;
        private CheckBox _removeKeyCheckbox;
        private TextBox _keyEdit;
        private CheckBox _lockedCheckbox;
        private NumericUpDown _openLockSpin;
        private NumericUpDown _difficultySpin;
        private NumericUpDown _difficultyModSpin;

        // UI Controls - Scripts
        private Dictionary<string, TextBox> _scriptFields;
        private List<string> _relevantScriptResnames;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation: Expose UI controls for testing
        // Original: editor.ui.tagEdit, editor.ui.resrefEdit, etc.
        public TextBox TagEdit => _tagEdit;
        public Button TagGenerateBtn => _tagGenerateBtn;
        public TextBox ResrefEdit => _resrefEdit;
        public Button ResrefGenerateBtn => _resrefGenerateBtn;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:39-84
        // Original: def __init__(self, parent, installation):
        public UTPEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Placeable Editor", "placeable",
                new[] { ResourceType.UTP, ResourceType.BTP },
                new[] { ResourceType.UTP, ResourceType.BTP },
                installation)
        {
            _installation = installation;
            _utp = new UTP();
            _scriptFields = new Dictionary<string, TextBox>();
            _relevantScriptResnames = new List<string>();

            InitializeComponent();
            SetupUI();
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
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:86-109
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

            // Appearance
            var appearanceLabel = new TextBlock { Text = "Appearance:" };
            _appearanceSelect = new ComboBox();
            basicPanel.Children.Add(appearanceLabel);
            basicPanel.Children.Add(_appearanceSelect);

            // Conversation
            var conversationLabel = new TextBlock { Text = "Conversation:" };
            _conversationEdit = new TextBox();
            _conversationModifyBtn = new Button { Content = "Edit" };
            _conversationModifyBtn.Click += (s, e) => EditConversation();
            basicPanel.Children.Add(conversationLabel);
            basicPanel.Children.Add(_conversationEdit);
            basicPanel.Children.Add(_conversationModifyBtn);

            // Inventory
            _inventoryBtn = new Button { Content = "Edit Inventory" };
            _inventoryBtn.Click += (s, e) => OpenInventory();
            _inventoryCountLabel = new TextBlock { Text = "Total Items: 0" };
            basicPanel.Children.Add(_inventoryBtn);
            basicPanel.Children.Add(_inventoryCountLabel);

            basicGroup.Content = basicPanel;
            mainPanel.Children.Add(basicGroup);

            // Advanced Group
            var advancedGroup = new Expander { Header = "Advanced", IsExpanded = false };
            var advancedPanel = new StackPanel { Orientation = Orientation.Vertical };

            _hasInventoryCheckbox = new CheckBox { Content = "Has Inventory" };
            _partyInteractCheckbox = new CheckBox { Content = "Party Interact" };
            _useableCheckbox = new CheckBox { Content = "Useable" };
            _min1HpCheckbox = new CheckBox { Content = "Min 1 HP" };
            _plotCheckbox = new CheckBox { Content = "Plot" };
            _staticCheckbox = new CheckBox { Content = "Static" };
            _notBlastableCheckbox = new CheckBox { Content = "Not Blastable" };
            var factionLabel = new TextBlock { Text = "Faction:" };
            _factionSelect = new ComboBox();
            var animationStateLabel = new TextBlock { Text = "Animation State:" };
            _animationStateSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var currentHpLabel = new TextBlock { Text = "Current HP:" };
            _currentHpSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };
            var maxHpLabel = new TextBlock { Text = "Maximum HP:" };
            _maxHpSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };
            var hardnessLabel = new TextBlock { Text = "Hardness:" };
            _hardnessSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var fortitudeLabel = new TextBlock { Text = "Fortitude:" };
            _fortitudeSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var reflexLabel = new TextBlock { Text = "Reflex:" };
            _reflexSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var willLabel = new TextBlock { Text = "Will:" };
            _willSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };

            advancedPanel.Children.Add(_hasInventoryCheckbox);
            advancedPanel.Children.Add(_partyInteractCheckbox);
            advancedPanel.Children.Add(_useableCheckbox);
            advancedPanel.Children.Add(_min1HpCheckbox);
            advancedPanel.Children.Add(_plotCheckbox);
            advancedPanel.Children.Add(_staticCheckbox);
            advancedPanel.Children.Add(_notBlastableCheckbox);
            advancedPanel.Children.Add(factionLabel);
            advancedPanel.Children.Add(_factionSelect);
            advancedPanel.Children.Add(animationStateLabel);
            advancedPanel.Children.Add(_animationStateSpin);
            advancedPanel.Children.Add(currentHpLabel);
            advancedPanel.Children.Add(_currentHpSpin);
            advancedPanel.Children.Add(maxHpLabel);
            advancedPanel.Children.Add(_maxHpSpin);
            advancedPanel.Children.Add(hardnessLabel);
            advancedPanel.Children.Add(_hardnessSpin);
            advancedPanel.Children.Add(fortitudeLabel);
            advancedPanel.Children.Add(_fortitudeSpin);
            advancedPanel.Children.Add(reflexLabel);
            advancedPanel.Children.Add(_reflexSpin);
            advancedPanel.Children.Add(willLabel);
            advancedPanel.Children.Add(_willSpin);

            advancedGroup.Content = advancedPanel;
            mainPanel.Children.Add(advancedGroup);

            // Lock Group
            var lockGroup = new Expander { Header = "Lock", IsExpanded = false };
            var lockPanel = new StackPanel { Orientation = Orientation.Vertical };

            _needKeyCheckbox = new CheckBox { Content = "Key Required" };
            _removeKeyCheckbox = new CheckBox { Content = "Auto Remove Key" };
            var keyLabel = new TextBlock { Text = "Key Name:" };
            _keyEdit = new TextBox();
            _lockedCheckbox = new CheckBox { Content = "Locked" };
            var openLockLabel = new TextBlock { Text = "Unlock DC:" };
            _openLockSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var difficultyLabel = new TextBlock { Text = "Unlock Difficulty:" };
            _difficultySpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var difficultyModLabel = new TextBlock { Text = "Unlock Difficulty Mod:" };
            _difficultyModSpin = new NumericUpDown { Minimum = -128, Maximum = 127 };

            lockPanel.Children.Add(_needKeyCheckbox);
            lockPanel.Children.Add(_removeKeyCheckbox);
            lockPanel.Children.Add(keyLabel);
            lockPanel.Children.Add(_keyEdit);
            lockPanel.Children.Add(_lockedCheckbox);
            lockPanel.Children.Add(openLockLabel);
            lockPanel.Children.Add(_openLockSpin);
            lockPanel.Children.Add(difficultyLabel);
            lockPanel.Children.Add(_difficultySpin);
            lockPanel.Children.Add(difficultyModLabel);
            lockPanel.Children.Add(_difficultyModSpin);

            lockGroup.Content = lockPanel;
            mainPanel.Children.Add(lockGroup);

            // Scripts Group
            var scriptsGroup = new Expander { Header = "Scripts", IsExpanded = false };
            var scriptsPanel = new StackPanel { Orientation = Orientation.Vertical };

            string[] scriptNames = { "OnClosed", "OnDamaged", "OnDeath", "OnEndDialog", "OnOpenFailed",
                "OnHeartbeat", "OnInventory", "OnMelee", "OnOpen", "OnLock", "OnUnlock", "OnUsed", "OnUserDefined" };
            foreach (string scriptName in scriptNames)
            {
                var scriptLabel = new TextBlock { Text = scriptName + ":" };
                var scriptEdit = new TextBox();
                _scriptFields[scriptName] = scriptEdit;
                scriptsPanel.Children.Add(scriptLabel);
                scriptsPanel.Children.Add(scriptEdit);
            }

            scriptsGroup.Content = scriptsPanel;
            mainPanel.Children.Add(scriptsGroup);

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
            // Use reflection to find controls by name if they were loaded from XAML
            // This matches PyKotor behavior where UI elements are found by name after XAML loading

            // Basic controls
            _nameEdit = this.FindControl<TextBox>("NameEdit") ?? this.FindControl<TextBox>("nameEdit");
            _nameEditBtn = this.FindControl<Button>("NameEditBtn") ?? this.FindControl<Button>("nameEditBtn");
            _tagEdit = this.FindControl<TextBox>("TagEdit") ?? this.FindControl<TextBox>("tagEdit");
            _tagGenerateBtn = this.FindControl<Button>("TagGenerateBtn") ?? this.FindControl<Button>("tagGenerateBtn");
            _resrefEdit = this.FindControl<TextBox>("ResrefEdit") ?? this.FindControl<TextBox>("resrefEdit");
            _resrefGenerateBtn = this.FindControl<Button>("ResrefGenerateBtn") ?? this.FindControl<Button>("resrefGenerateBtn");
            _appearanceSelect = this.FindControl<ComboBox>("AppearanceSelect") ?? this.FindControl<ComboBox>("appearanceSelect");
            _conversationEdit = this.FindControl<TextBox>("ConversationEdit") ?? this.FindControl<TextBox>("conversationEdit");
            _conversationModifyBtn = this.FindControl<Button>("ConversationModifyBtn") ?? this.FindControl<Button>("conversationModifyBtn");
            _inventoryBtn = this.FindControl<Button>("InventoryBtn") ?? this.FindControl<Button>("inventoryBtn");
            _inventoryCountLabel = this.FindControl<TextBlock>("InventoryCountLabel") ?? this.FindControl<TextBlock>("inventoryCountLabel");

            // Advanced controls
            _hasInventoryCheckbox = this.FindControl<CheckBox>("HasInventoryCheckbox") ?? this.FindControl<CheckBox>("hasInventoryCheckbox");
            _partyInteractCheckbox = this.FindControl<CheckBox>("PartyInteractCheckbox") ?? this.FindControl<CheckBox>("partyInteractCheckbox");
            _useableCheckbox = this.FindControl<CheckBox>("UseableCheckbox") ?? this.FindControl<CheckBox>("useableCheckbox");
            _min1HpCheckbox = this.FindControl<CheckBox>("Min1HpCheckbox") ?? this.FindControl<CheckBox>("min1HpCheckbox");
            _plotCheckbox = this.FindControl<CheckBox>("PlotCheckbox") ?? this.FindControl<CheckBox>("plotCheckbox");
            _staticCheckbox = this.FindControl<CheckBox>("StaticCheckbox") ?? this.FindControl<CheckBox>("staticCheckbox");
            _notBlastableCheckbox = this.FindControl<CheckBox>("NotBlastableCheckbox") ?? this.FindControl<CheckBox>("notBlastableCheckbox");
            _factionSelect = this.FindControl<ComboBox>("FactionSelect") ?? this.FindControl<ComboBox>("factionSelect");
            _animationStateSpin = this.FindControl<NumericUpDown>("AnimationStateSpin") ?? this.FindControl<NumericUpDown>("animationStateSpin");
            _currentHpSpin = this.FindControl<NumericUpDown>("CurrentHpSpin") ?? this.FindControl<NumericUpDown>("currentHpSpin");
            _maxHpSpin = this.FindControl<NumericUpDown>("MaxHpSpin") ?? this.FindControl<NumericUpDown>("maxHpSpin");
            _hardnessSpin = this.FindControl<NumericUpDown>("HardnessSpin") ?? this.FindControl<NumericUpDown>("hardnessSpin");
            _fortitudeSpin = this.FindControl<NumericUpDown>("FortitudeSpin") ?? this.FindControl<NumericUpDown>("fortitudeSpin");
            _reflexSpin = this.FindControl<NumericUpDown>("ReflexSpin") ?? this.FindControl<NumericUpDown>("reflexSpin");
            _willSpin = this.FindControl<NumericUpDown>("WillSpin") ?? this.FindControl<NumericUpDown>("willSpin");

            // Lock controls
            _needKeyCheckbox = this.FindControl<CheckBox>("NeedKeyCheckbox") ?? this.FindControl<CheckBox>("needKeyCheckbox");
            _removeKeyCheckbox = this.FindControl<CheckBox>("RemoveKeyCheckbox") ?? this.FindControl<CheckBox>("removeKeyCheckbox");
            _keyEdit = this.FindControl<TextBox>("KeyEdit") ?? this.FindControl<TextBox>("keyEdit");
            _lockedCheckbox = this.FindControl<CheckBox>("LockedCheckbox") ?? this.FindControl<CheckBox>("lockedCheckbox");
            _openLockSpin = this.FindControl<NumericUpDown>("OpenLockSpin") ?? this.FindControl<NumericUpDown>("openLockSpin");
            _difficultySpin = this.FindControl<NumericUpDown>("DifficultySpin") ?? this.FindControl<NumericUpDown>("difficultySpin");
            _difficultyModSpin = this.FindControl<NumericUpDown>("DifficultyModSpin") ?? this.FindControl<NumericUpDown>("difficultyModSpin");

            // Script controls - find by name pattern
            string[] scriptNames = { "OnClosed", "OnDamaged", "OnDeath", "OnEndDialog", "OnOpenFailed",
                "OnHeartbeat", "OnInventory", "OnMelee", "OnOpen", "OnLock", "OnUnlock", "OnUsed", "OnUserDefined" };

            foreach (string scriptName in scriptNames)
            {
                var scriptEdit = this.FindControl<TextBox>(scriptName + "Edit") ?? this.FindControl<TextBox>(scriptName.ToLower() + "Edit");
                if (scriptEdit != null)
                {
                    _scriptFields[scriptName] = scriptEdit;
                }
            }

            // Comments control
            _commentsEdit = this.FindControl<TextBox>("CommentsEdit") ?? this.FindControl<TextBox>("commentsEdit");

            // Set up event handlers for controls that were found from XAML
            if (_nameEditBtn != null)
            {
                _nameEditBtn.Click += (s, e) => EditName();
            }
            if (_tagGenerateBtn != null)
            {
                _tagGenerateBtn.Click += (s, e) => GenerateTag();
            }
            if (_resrefGenerateBtn != null)
            {
                _resrefGenerateBtn.Click += (s, e) => GenerateResref();
            }
            if (_conversationModifyBtn != null)
            {
                _conversationModifyBtn.Click += (s, e) => EditConversation();
            }
            if (_inventoryBtn != null)
            {
                _inventoryBtn.Click += (s, e) => OpenInventory();
            }

            // Set up context menus for script fields if they were found from XAML
            if (_installation != null)
            {
                SetupFileContextMenus();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:110-166
        // Original: def _setup_installation(self, installation):
        private void SetupInstallation(HTInstallation installation)
        {
            _installation = installation;

            // Matching PyKotor implementation: Load required 2da files if they have not been loaded already
            List<string> required = new List<string> { HTInstallation.TwoDAPlaceables, HTInstallation.TwoDAFactions };
            installation.HtBatchCache2DA(required);

            // Matching PyKotor implementation: appearances: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_PLACEABLES)
            TwoDA appearances = installation.HtGetCache2DA(HTInstallation.TwoDAPlaceables);
            if (_appearanceSelect != null && appearances != null)
            {
                _appearanceSelect.Items.Clear();
                List<string> appearanceLabels = appearances.GetColumn("label");
                foreach (string label in appearanceLabels)
                {
                    _appearanceSelect.Items.Add(label);
                }
            }

            // Matching PyKotor implementation: factions: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_FACTIONS)
            TwoDA factions = installation.HtGetCache2DA(HTInstallation.TwoDAFactions);
            if (_factionSelect != null && factions != null)
            {
                _factionSelect.Items.Clear();
                List<string> factionLabels = factions.GetColumn("label");
                foreach (string label in factionLabels)
                {
                    _factionSelect.Items.Add(label);
                }
            }

            // Matching PyKotor implementation: self.ui.notBlastableCheckbox.setVisible(installation.tsl)
            if (_notBlastableCheckbox != null)
            {
                _notBlastableCheckbox.IsVisible = installation.Tsl;
            }
            if (_difficultySpin != null)
            {
                _difficultySpin.IsVisible = installation.Tsl;
            }
            if (_difficultyModSpin != null)
            {
                _difficultyModSpin.IsVisible = installation.Tsl;
            }

            // Matching PyKotor implementation: self._installation.setup_file_context_menu(...)
            SetupFileContextMenus();

            // Matching PyKotor implementation: self.relevant_script_resnames = sorted(...)
            if (installation != null && !string.IsNullOrEmpty(base._filepath))
            {
                HashSet<FileResource> scriptResources = installation.GetRelevantResources(ResourceType.NCS, base._filepath);
                _relevantScriptResnames = scriptResources
                    .Select(r => r.ResName.ToLowerInvariant())
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();
            }
            else
            {
                _relevantScriptResnames = new List<string>();
            }
        }

        // Matching PyKotor implementation: self._installation.setup_file_context_menu(...)
        private void SetupFileContextMenus()
        {
            if (_installation == null)
            {
                return;
            }

            // Setup context menus for script TextBoxes (NSS/NCS files)
            foreach (var kvp in _scriptFields)
            {
                SetupScriptTextBoxContextMenu(kvp.Value, kvp.Key + " Script");
            }

            // Setup context menu for conversation field (DLG files)
            if (_conversationEdit != null)
            {
                SetupConversationTextBoxContextMenu(_conversationEdit);
            }
        }

        // Create context menu for script TextBox controls
        private void SetupScriptTextBoxContextMenu(TextBox textBox, string scriptTypeName)
        {
            if (textBox == null)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItems = new List<MenuItem>();

            // "Open in Editor" menu item
            var openInEditorItem = new MenuItem
            {
                Header = "Open in Editor",
                IsEnabled = false
            };
            openInEditorItem.Click += (sender, e) => OpenScriptInEditor(textBox, scriptTypeName);
            menuItems.Add(openInEditorItem);

            // Enable/disable based on whether script name is set
            textBox.TextChanged += (sender, e) =>
            {
                string text = textBox.Text ?? string.Empty;
                openInEditorItem.IsEnabled = !string.IsNullOrWhiteSpace(text);
            };

            foreach (var item in menuItems)
            {
                contextMenu.Items.Add(item);
            }
            textBox.ContextMenu = contextMenu;
        }

        // Create context menu for conversation TextBox control
        private void SetupConversationTextBoxContextMenu(TextBox textBox)
        {
            if (textBox == null)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItems = new List<MenuItem>();

            // "Open in Editor" menu item
            var openInEditorItem = new MenuItem
            {
                Header = "Open in Editor",
                IsEnabled = false
            };
            openInEditorItem.Click += (sender, e) => EditConversation();
            menuItems.Add(openInEditorItem);

            // Enable/disable based on whether conversation name is set
            textBox.TextChanged += (sender, e) =>
            {
                string text = textBox.Text ?? string.Empty;
                openInEditorItem.IsEnabled = !string.IsNullOrWhiteSpace(text);
            };

            foreach (var item in menuItems)
            {
                contextMenu.Items.Add(item);
            }
            textBox.ContextMenu = contextMenu;
        }

        // Open script in editor
        private void OpenScriptInEditor(TextBox textBox, string scriptTypeName)
        {
            if (_installation == null || textBox == null)
            {
                return;
            }

            string resname = textBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(resname))
            {
                return;
            }

            // Try NCS first, then NSS
            var search = _installation.Resource(resname, ResourceType.NCS)
                         ?? _installation.Resource(resname, ResourceType.NSS);

            if (search != null)
            {
                WindowUtils.OpenResourceEditor(
                    search.FilePath,
                    search.ResName,
                    search.ResType,
                    search.Data,
                    _installation,
                    this
                );
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:168-268
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The UTP file data is empty or invalid.");
            }

            var gff = GFF.FromBytes(data);
            _utp = UTPHelpers.ConstructUtp(gff);
            LoadUTP(_utp);
            UpdateItemCount();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:182-268
        // Original: def _loadUTP(self, utp):
        private void LoadUTP(UTP utp)
        {
            _utp = utp;

            // Basic
            if (_nameEdit != null)
            {
                _nameEdit.Text = _installation != null ? _installation.String(utp.Name) : utp.Name.StringRef.ToString();
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = utp.Tag;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = utp.ResRef.ToString();
            }
            if (_appearanceSelect != null)
            {
                _appearanceSelect.SelectedIndex = utp.AppearanceId;
            }
            if (_conversationEdit != null)
            {
                _conversationEdit.Text = utp.Conversation.ToString();
            }

            // Advanced
            if (_hasInventoryCheckbox != null) _hasInventoryCheckbox.IsChecked = utp.HasInventory;
            if (_partyInteractCheckbox != null) _partyInteractCheckbox.IsChecked = utp.PartyInteract;
            if (_useableCheckbox != null) _useableCheckbox.IsChecked = utp.Useable;
            if (_min1HpCheckbox != null) _min1HpCheckbox.IsChecked = utp.Min1Hp;
            if (_plotCheckbox != null) _plotCheckbox.IsChecked = utp.Plot;
            if (_staticCheckbox != null) _staticCheckbox.IsChecked = utp.Static;
            if (_notBlastableCheckbox != null) _notBlastableCheckbox.IsChecked = utp.NotBlastable;
            if (_factionSelect != null) _factionSelect.SelectedIndex = utp.FactionId;
            if (_animationStateSpin != null) _animationStateSpin.Value = utp.AnimationState;
            if (_currentHpSpin != null) _currentHpSpin.Value = utp.CurrentHp;
            if (_maxHpSpin != null) _maxHpSpin.Value = utp.MaximumHp;
            if (_hardnessSpin != null) _hardnessSpin.Value = utp.Hardness;
            if (_fortitudeSpin != null) _fortitudeSpin.Value = utp.Fortitude;
            if (_reflexSpin != null) _reflexSpin.Value = utp.Reflex;
            if (_willSpin != null) _willSpin.Value = utp.Will;

            // Lock
            if (_needKeyCheckbox != null) _needKeyCheckbox.IsChecked = utp.KeyRequired;
            if (_removeKeyCheckbox != null) _removeKeyCheckbox.IsChecked = utp.AutoRemoveKey;
            if (_keyEdit != null) _keyEdit.Text = utp.KeyName;
            if (_lockedCheckbox != null) _lockedCheckbox.IsChecked = utp.Locked;
            if (_openLockSpin != null) _openLockSpin.Value = utp.UnlockDc;
            if (_difficultySpin != null) _difficultySpin.Value = utp.UnlockDiff;
            if (_difficultyModSpin != null) _difficultyModSpin.Value = utp.UnlockDiffMod;

            // Scripts - populate with relevant resources first, then set values
            // Matching PyKotor implementation: self.ui.onClosedEdit.populate_combo_box(self.relevant_script_resnames)
            if (_installation != null && !string.IsNullOrEmpty(base._filepath))
            {
                // Populate script fields with relevant resources (for autocomplete-like behavior)
                // TODO: STUB - Note: In Python, these are ComboBoxes with populate_combo_box, but in C# we use TextBox
                // TODO:  So we'll just set the text value - autocomplete would require a different control
            }

            // Set script values from UTP
            if (_scriptFields.ContainsKey("OnClosed") && _scriptFields["OnClosed"] != null)
                _scriptFields["OnClosed"].Text = utp.OnClosed.ToString();
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                _scriptFields["OnDamaged"].Text = utp.OnDamaged.ToString();
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                _scriptFields["OnDeath"].Text = utp.OnDeath.ToString();
            if (_scriptFields.ContainsKey("OnEndDialog") && _scriptFields["OnEndDialog"] != null)
                _scriptFields["OnEndDialog"].Text = utp.OnEndDialog.ToString();
            if (_scriptFields.ContainsKey("OnOpenFailed") && _scriptFields["OnOpenFailed"] != null)
                _scriptFields["OnOpenFailed"].Text = utp.OnOpenFailed.ToString();
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                _scriptFields["OnHeartbeat"].Text = utp.OnHeartbeat.ToString();
            if (_scriptFields.ContainsKey("OnInventory") && _scriptFields["OnInventory"] != null)
                _scriptFields["OnInventory"].Text = utp.OnInventory.ToString();
            if (_scriptFields.ContainsKey("OnMelee") && _scriptFields["OnMelee"] != null)
                _scriptFields["OnMelee"].Text = utp.OnMelee.ToString();
            if (_scriptFields.ContainsKey("OnOpen") && _scriptFields["OnOpen"] != null)
                _scriptFields["OnOpen"].Text = utp.OnOpen.ToString();
            if (_scriptFields.ContainsKey("OnLock") && _scriptFields["OnLock"] != null)
                _scriptFields["OnLock"].Text = utp.OnLock.ToString();
            if (_scriptFields.ContainsKey("OnUnlock") && _scriptFields["OnUnlock"] != null)
                _scriptFields["OnUnlock"].Text = utp.OnUnlock.ToString();
            if (_scriptFields.ContainsKey("OnUsed") && _scriptFields["OnUsed"] != null)
                _scriptFields["OnUsed"].Text = utp.OnUsed.ToString();
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                _scriptFields["OnUserDefined"].Text = utp.OnUserDefined.ToString();

            // Comments
            if (_commentsEdit != null) _commentsEdit.Text = utp.Comment;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:270-346
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching Python: utp: UTP = deepcopy(self._utp)
            var utp = CopyUtp(_utp);

            // Basic - read from UI controls (matching Python which always reads from UI)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:291
            // Python: utp.name = self.ui.nameEdit.locstring()
            // In C#, nameEdit is TextBox (read-only), LocalizedString is stored in _utp.Name and updated via EditName()
            // So we use utp.Name from the copy (which preserves the value set by EditName())
            // Note: This matches Python behavior where locstring() returns the stored LocalizedString
            utp.Name = utp.Name ?? LocalizedString.FromInvalid();
            utp.Tag = _tagEdit?.Text ?? "";
            utp.ResRef = new ResRef(_resrefEdit?.Text ?? "");
            utp.AppearanceId = _appearanceSelect?.SelectedIndex ?? 0;
            utp.Conversation = new ResRef(_conversationEdit?.Text ?? "");
            utp.HasInventory = _hasInventoryCheckbox?.IsChecked == true;

            // Advanced - read from UI controls
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:298-312
            utp.Min1Hp = _min1HpCheckbox?.IsChecked == true;
            utp.PartyInteract = _partyInteractCheckbox?.IsChecked == true;
            utp.Useable = _useableCheckbox?.IsChecked == true;
            utp.Plot = _plotCheckbox?.IsChecked == true;
            utp.Static = _staticCheckbox?.IsChecked == true;
            utp.NotBlastable = _notBlastableCheckbox?.IsChecked == true;
            utp.FactionId = _factionSelect?.SelectedIndex ?? 0;
            utp.AnimationState = (int)(_animationStateSpin?.Value ?? 0);
            utp.CurrentHp = (int)(_currentHpSpin?.Value ?? 0);
            utp.MaximumHp = (int)(_maxHpSpin?.Value ?? 0);
            utp.Hardness = (int)(_hardnessSpin?.Value ?? 0);
            utp.Fortitude = (int)(_fortitudeSpin?.Value ?? 0);
            utp.Reflex = (int)(_reflexSpin?.Value ?? 0);
            utp.Will = (int)(_willSpin?.Value ?? 0);

            // Lock - read from UI controls
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:314-321
            utp.Locked = _lockedCheckbox?.IsChecked == true;
            utp.UnlockDc = (int)(_openLockSpin?.Value ?? 0);
            utp.UnlockDiff = (int)(_difficultySpin?.Value ?? 0);
            utp.UnlockDiffMod = (int)(_difficultyModSpin?.Value ?? 0);
            utp.KeyRequired = _needKeyCheckbox?.IsChecked == true;
            utp.AutoRemoveKey = _removeKeyCheckbox?.IsChecked == true;
            utp.KeyName = _keyEdit?.Text ?? "";

            // Scripts - read from UI controls
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:323-337
            if (_scriptFields.ContainsKey("OnClosed") && _scriptFields["OnClosed"] != null)
                utp.OnClosed = new ResRef(_scriptFields["OnClosed"].Text);
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                utp.OnDamaged = new ResRef(_scriptFields["OnDamaged"].Text);
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                utp.OnDeath = new ResRef(_scriptFields["OnDeath"].Text);
            if (_scriptFields.ContainsKey("OnEndDialog") && _scriptFields["OnEndDialog"] != null)
                utp.OnEndDialog = new ResRef(_scriptFields["OnEndDialog"].Text);
            if (_scriptFields.ContainsKey("OnOpenFailed") && _scriptFields["OnOpenFailed"] != null)
                utp.OnOpenFailed = new ResRef(_scriptFields["OnOpenFailed"].Text);
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                utp.OnHeartbeat = new ResRef(_scriptFields["OnHeartbeat"].Text);
            if (_scriptFields.ContainsKey("OnInventory") && _scriptFields["OnInventory"] != null)
                utp.OnInventory = new ResRef(_scriptFields["OnInventory"].Text);
            if (_scriptFields.ContainsKey("OnMelee") && _scriptFields["OnMelee"] != null)
                utp.OnMelee = new ResRef(_scriptFields["OnMelee"].Text);
            if (_scriptFields.ContainsKey("OnOpen") && _scriptFields["OnOpen"] != null)
                utp.OnOpen = new ResRef(_scriptFields["OnOpen"].Text);
            if (_scriptFields.ContainsKey("OnLock") && _scriptFields["OnLock"] != null)
                utp.OnLock = new ResRef(_scriptFields["OnLock"].Text);
            if (_scriptFields.ContainsKey("OnUnlock") && _scriptFields["OnUnlock"] != null)
                utp.OnUnlock = new ResRef(_scriptFields["OnUnlock"].Text);
            if (_scriptFields.ContainsKey("OnUsed") && _scriptFields["OnUsed"] != null)
                utp.OnUsed = new ResRef(_scriptFields["OnUsed"].Text);
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                utp.OnUserDefined = new ResRef(_scriptFields["OnUserDefined"].Text);

            // Comments - read from UI controls
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:340
            utp.Comment = _commentsEdit?.Text ?? "";

            // Matching Python: gff: GFF = dismantle_utp(utp); write_gff(gff, data)
            BioWareGame game = _installation?.Game ?? BioWareGame.K2;
            var gff = UTPHelpers.DismantleUtp(utp, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTP);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching Python: deepcopy(self._utp)
        private static UTP CopyUtp(UTP source)
        {
            // Use Dismantle/Construct pattern for reliable deep copy (matching Python deepcopy behavior)
            BioWareGame game = BioWareGame.K2; // Default game for serialization
            var gff = UTPHelpers.DismantleUtp(source, game);
            return UTPHelpers.ConstructUtp(gff);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:348-350
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _utp = new UTP();
            LoadUTP(_utp);
            UpdateItemCount();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:352-355
        // Original: def update_item_count(self):
        private void UpdateItemCount()
        {
            if (_inventoryCountLabel != null && _utp != null)
            {
                int count = _utp.Inventory != null ? _utp.Inventory.Count : 0;
                _inventoryCountLabel.Text = $"Total Items: {count}";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:357-363
        // Original: def change_name(self):
        private void EditName()
        {
            if (_installation == null) return;
            var dialog = new LocalizedStringDialog(this, _installation, _utp.Name);
            if (dialog.ShowDialog())
            {
                _utp.Name = dialog.LocString;
                if (_nameEdit != null)
                {
                    _nameEdit.Text = _installation.String(_utp.Name);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:365-368
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:370-374
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = !string.IsNullOrEmpty(base._resname) ? base._resname : "m00xx_plc_000";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:376-406
        // Original: def edit_conversation(self):
        private void EditConversation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:378
            // Original: resname = self.ui.conversationEdit.currentText()
            string resname = _conversationEdit?.Text?.Trim() ?? "";
            byte[] data = null;
            string filepath = null;

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:381-383
            // Original: if not resname or not resname.strip():
            if (string.IsNullOrEmpty(resname))
            {
                var errorBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                    "Failed to open DLG Editor",
                    "Conversation field cannot be blank.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                errorBox.ShowAsync();
                return;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:385-386
            // Original: assert self._installation is not None
            if (_installation == null)
            {
                return;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:386
            // Original: search: ResourceResult | None = self._installation.resource(resname, ResourceType.DLG)
            var search = _installation.Resource(resname, ResourceType.DLG);

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:387-401
            // Original: if search is None:
            if (search == null)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:388-393
                // Original: msgbox: int = QMessageBox(...).exec()
                var msgBox = MsBox.Avalonia.MessageBoxManager.GetMessageBoxStandard(
                    "DLG file not found",
                    "Do you wish to create a file in the override?",
                    ButtonEnum.YesNo,
                    MsBox.Avalonia.Enums.Icon.Question);
                var result = msgBox.ShowAsync().Result;

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:394
                // Original: if QMessageBox.StandardButton.Yes == msgbox:
                if (result == ButtonResult.Yes)
                {
                    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:395-401
                    // Original: data = bytearray(); write_gff(dismantle_dlg(DLG()), data); filepath = ...
                    var dlg = new DLGType();
                    var gff = DLGHelper.DismantleDlg(dlg, _installation.Game);
                    data = GFFAuto.BytesGff(gff, ResourceType.DLG);
                    filepath = System.IO.Path.Combine(_installation.OverridePath(), $"{resname}.dlg");
                    File.WriteAllBytes(filepath, data);
                }
            }
            else
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:402-403
                // Original: resname, restype, filepath, data = search
                resname = search.ResName;
                filepath = search.FilePath;
                data = search.Data;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:405-406
            // Original: if data is not None: open_resource_editor(...)
            if (data != null)
            {
                WindowUtils.OpenResourceEditor(filepath, resname, ResourceType.DLG, data, _installation, this);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:408-440
        // Original: def open_inventory(self):
        private void OpenInventory()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:418-420
            // Original: if self._installation is None: self.blink_window(); return
            if (_installation == null)
            {
                return;
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:421-426
            // Original: capsules: list[Capsule] = []; with suppress(Exception): root: str = Module.filepath_to_root(...)
            var capsules = new List<Capsule>();
            try
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:423
                // Original: root: str = Module.filepath_to_root(self._filepath)
                string root = null;
                if (!string.IsNullOrEmpty(_filepath))
                {
                    root = Module.FilepathToRoot(_filepath);
                }

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:424
                // Original: moduleNames: list[str] = [path for path in self._installation.module_names() if root in path and path != self._filepath]
                var moduleNames = _installation.ModuleNames();
                var matchingModules = new List<string>();
                foreach (var kvp in moduleNames)
                {
                    string modulePath = kvp.Value ?? kvp.Key;
                    if (root != null && modulePath.Contains(root) && modulePath != _filepath)
                    {
                        matchingModules.Add(kvp.Key);
                    }
                }

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:425
                // Original: newCapsules: list[Capsule] = [Capsule(self._installation.module_path() / mod_filename) for mod_filename in moduleNames]
                foreach (string modFilename in matchingModules)
                {
                    string modulePath = System.IO.Path.Combine(_installation.ModulePath(), modFilename);
                    if (File.Exists(modulePath))
                    {
                        try
                        {
                            var capsule = new Capsule(modulePath, createIfNotExist: false);
                            capsules.Add(capsule);
                        }
                        catch
                        {
                            // Skip invalid capsule files
                        }
                    }
                }
            }
            catch
            {
                // Matching PyKotor implementation: suppress(Exception) - ignore errors
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:428-437
            // Original: inventoryEditor = InventoryEditor(self, self._installation, capsules, [], self._utp.inventory, {}, droid=False, hide_equipment=True)
            var inventoryEditor = new InventoryDialog(
                this,
                _installation,
                capsules,
                new List<string>(), // folders parameter
                _utp.Inventory ?? new List<InventoryItem>(),
                new Dictionary<EquipmentSlot, InventoryItem>(), // equipment parameter
                droid: false,
                hideEquipment: true,
                isStore: false
            );

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:438-440
            // Original: if inventoryEditor.exec(): self._utp.inventory = inventoryEditor.inventory; self.update_item_count()
            if (inventoryEditor.ShowDialog())
            {
                _utp.Inventory = inventoryEditor.Inventory ?? new List<InventoryItem>();
                UpdateItemCount();
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
