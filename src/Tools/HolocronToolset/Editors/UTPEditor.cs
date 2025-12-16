using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resources;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:38
    // Original: class UTPEditor(Editor):
    public class UTPEditor : Editor
    {
        private UTP _utp;
        private HTInstallation _installation;

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

        // UI Controls - Comments
        private TextBox _commentsEdit;

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

            InitializeComponent();
            SetupUI();
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
            // For now, programmatic UI is set up in SetupProgrammaticUI
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

            // Scripts
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
            // Basic
            _utp.Name = _utp.Name ?? LocalizedString.FromInvalid();
            _utp.Tag = _tagEdit?.Text ?? "";
            _utp.ResRef = new ResRef(_resrefEdit?.Text ?? "");
            _utp.AppearanceId = _appearanceSelect?.SelectedIndex ?? 0;
            _utp.Conversation = new ResRef(_conversationEdit?.Text ?? "");
            _utp.HasInventory = _hasInventoryCheckbox?.IsChecked ?? false;

            // Advanced
            _utp.Min1Hp = _min1HpCheckbox?.IsChecked ?? false;
            _utp.PartyInteract = _partyInteractCheckbox?.IsChecked ?? false;
            _utp.Useable = _useableCheckbox?.IsChecked ?? false;
            _utp.Plot = _plotCheckbox?.IsChecked ?? false;
            _utp.Static = _staticCheckbox?.IsChecked ?? false;
            _utp.NotBlastable = _notBlastableCheckbox?.IsChecked ?? false;
            _utp.FactionId = _factionSelect?.SelectedIndex ?? 0;
            _utp.AnimationState = (int)(_animationStateSpin?.Value ?? 0);
            _utp.CurrentHp = (int)(_currentHpSpin?.Value ?? 0);
            _utp.MaximumHp = (int)(_maxHpSpin?.Value ?? 0);
            _utp.Hardness = (int)(_hardnessSpin?.Value ?? 0);
            _utp.Fortitude = (int)(_fortitudeSpin?.Value ?? 0);
            _utp.Reflex = (int)(_reflexSpin?.Value ?? 0);
            _utp.Will = (int)(_willSpin?.Value ?? 0);

            // Lock
            _utp.Locked = _lockedCheckbox?.IsChecked ?? false;
            _utp.UnlockDc = (int)(_openLockSpin?.Value ?? 0);
            _utp.UnlockDiff = (int)(_difficultySpin?.Value ?? 0);
            _utp.UnlockDiffMod = (int)(_difficultyModSpin?.Value ?? 0);
            _utp.KeyRequired = _needKeyCheckbox?.IsChecked ?? false;
            _utp.AutoRemoveKey = _removeKeyCheckbox?.IsChecked ?? false;
            _utp.KeyName = _keyEdit?.Text ?? "";

            // Scripts
            if (_scriptFields.ContainsKey("OnClosed") && _scriptFields["OnClosed"] != null)
                _utp.OnClosed = new ResRef(_scriptFields["OnClosed"].Text);
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                _utp.OnDamaged = new ResRef(_scriptFields["OnDamaged"].Text);
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                _utp.OnDeath = new ResRef(_scriptFields["OnDeath"].Text);
            if (_scriptFields.ContainsKey("OnEndDialog") && _scriptFields["OnEndDialog"] != null)
                _utp.OnEndDialog = new ResRef(_scriptFields["OnEndDialog"].Text);
            if (_scriptFields.ContainsKey("OnOpenFailed") && _scriptFields["OnOpenFailed"] != null)
                _utp.OnOpenFailed = new ResRef(_scriptFields["OnOpenFailed"].Text);
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                _utp.OnHeartbeat = new ResRef(_scriptFields["OnHeartbeat"].Text);
            if (_scriptFields.ContainsKey("OnInventory") && _scriptFields["OnInventory"] != null)
                _utp.OnInventory = new ResRef(_scriptFields["OnInventory"].Text);
            if (_scriptFields.ContainsKey("OnMelee") && _scriptFields["OnMelee"] != null)
                _utp.OnMelee = new ResRef(_scriptFields["OnMelee"].Text);
            if (_scriptFields.ContainsKey("OnOpen") && _scriptFields["OnOpen"] != null)
                _utp.OnOpen = new ResRef(_scriptFields["OnOpen"].Text);
            if (_scriptFields.ContainsKey("OnLock") && _scriptFields["OnLock"] != null)
                _utp.OnLock = new ResRef(_scriptFields["OnLock"].Text);
            if (_scriptFields.ContainsKey("OnUnlock") && _scriptFields["OnUnlock"] != null)
                _utp.OnUnlock = new ResRef(_scriptFields["OnUnlock"].Text);
            if (_scriptFields.ContainsKey("OnUsed") && _scriptFields["OnUsed"] != null)
                _utp.OnUsed = new ResRef(_scriptFields["OnUsed"].Text);
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                _utp.OnUserDefined = new ResRef(_scriptFields["OnUserDefined"].Text);

            // Comments
            _utp.Comment = _commentsEdit?.Text ?? "";

            // Build GFF
            Game game = _installation?.Game ?? Game.K2;
            var gff = UTPHelpers.DismantleUtp(_utp, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTP);
            return Tuple.Create(data, new byte[0]);
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
            // Placeholder for conversation editor
            // Will be implemented when DLG editor is available
            System.Console.WriteLine("Conversation editor not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utp.py:408-440
        // Original: def open_inventory(self):
        private void OpenInventory()
        {
            // Placeholder for inventory editor
            // Will be implemented when InventoryEditor dialog is available
            System.Console.WriteLine("Inventory editor not yet implemented");
            // For now, just update the count
            UpdateItemCount();
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
