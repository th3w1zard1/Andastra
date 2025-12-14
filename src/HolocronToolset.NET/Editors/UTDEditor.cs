using System;
using System.Collections.Generic;
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
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:32
    // Original: class UTDEditor(Editor):
    public class UTDEditor : Editor
    {
        private UTD _utd;
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

        // UI Controls - Advanced
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:33-82
        // Original: def __init__(self, parent, installation):
        public UTDEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Door Editor", "door",
                new[] { ResourceType.UTD, ResourceType.BTD },
                new[] { ResourceType.UTD, ResourceType.BTD },
                installation)
        {
            _installation = installation;
            _utd = new UTD();
            _scriptFields = new Dictionary<string, TextBox>();

            InitializeComponent();
            SetupUI();
            Width = 654;
            Height = 495;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:84-105
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

            basicGroup.Content = basicPanel;
            mainPanel.Children.Add(basicGroup);

            // Advanced Group
            var advancedGroup = new Expander { Header = "Advanced", IsExpanded = false };
            var advancedPanel = new StackPanel { Orientation = Orientation.Vertical };

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
            var willLabel = new TextBlock { Text = "Willpower:" };
            _willSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };

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

            string[] scriptNames = { "OnClick", "OnClosed", "OnDamaged", "OnDeath", "OnOpenFailed",
                "OnHeartbeat", "OnMelee", "OnOpen", "OnUnlock", "OnUserDefined" };
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:172-264
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The UTD file data is empty or invalid.");
            }

            var gff = GFF.FromBytes(data);
            _utd = UTDHelpers.ConstructUtd(gff);
            LoadUTD(_utd);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:184-264
        // Original: def _loadUTD(self, utd):
        private void LoadUTD(UTD utd)
        {
            _utd = utd;

            // Basic
            if (_nameEdit != null)
            {
                _nameEdit.Text = _installation != null ? _installation.String(utd.Name) : utd.Name.StringRef.ToString();
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = utd.Tag;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = utd.ResRef.ToString();
            }
            if (_appearanceSelect != null)
            {
                _appearanceSelect.SelectedIndex = utd.AppearanceId;
            }
            if (_conversationEdit != null)
            {
                _conversationEdit.Text = utd.Conversation.ToString();
            }

            // Advanced
            if (_min1HpCheckbox != null) _min1HpCheckbox.IsChecked = utd.Min1Hp;
            if (_plotCheckbox != null) _plotCheckbox.IsChecked = utd.Plot;
            if (_staticCheckbox != null) _staticCheckbox.IsChecked = utd.Static;
            if (_notBlastableCheckbox != null) _notBlastableCheckbox.IsChecked = utd.NotBlastable;
            if (_factionSelect != null) _factionSelect.SelectedIndex = utd.FactionId;
            if (_animationStateSpin != null) _animationStateSpin.Value = utd.AnimationState;
            if (_currentHpSpin != null) _currentHpSpin.Value = utd.CurrentHp;
            if (_maxHpSpin != null) _maxHpSpin.Value = utd.MaximumHp;
            if (_hardnessSpin != null) _hardnessSpin.Value = utd.Hardness;
            if (_fortitudeSpin != null) _fortitudeSpin.Value = utd.Fortitude;
            if (_reflexSpin != null) _reflexSpin.Value = utd.Reflex;
            if (_willSpin != null) _willSpin.Value = utd.Willpower;

            // Lock
            if (_needKeyCheckbox != null) _needKeyCheckbox.IsChecked = utd.KeyRequired;
            if (_removeKeyCheckbox != null) _removeKeyCheckbox.IsChecked = utd.AutoRemoveKey;
            if (_keyEdit != null) _keyEdit.Text = utd.KeyName;
            if (_lockedCheckbox != null) _lockedCheckbox.IsChecked = utd.Locked;
            if (_openLockSpin != null) _openLockSpin.Value = utd.UnlockDc;
            if (_difficultySpin != null) _difficultySpin.Value = utd.UnlockDiff;
            if (_difficultyModSpin != null) _difficultyModSpin.Value = utd.UnlockDiffMod;

            // Scripts
            if (_scriptFields.ContainsKey("OnClick") && _scriptFields["OnClick"] != null)
                _scriptFields["OnClick"].Text = utd.OnClick.ToString();
            if (_scriptFields.ContainsKey("OnClosed") && _scriptFields["OnClosed"] != null)
                _scriptFields["OnClosed"].Text = utd.OnClosed.ToString();
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                _scriptFields["OnDamaged"].Text = utd.OnDamaged.ToString();
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                _scriptFields["OnDeath"].Text = utd.OnDeath.ToString();
            if (_scriptFields.ContainsKey("OnOpenFailed") && _scriptFields["OnOpenFailed"] != null)
                _scriptFields["OnOpenFailed"].Text = utd.OnOpenFailed.ToString();
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                _scriptFields["OnHeartbeat"].Text = utd.OnHeartbeat.ToString();
            if (_scriptFields.ContainsKey("OnMelee") && _scriptFields["OnMelee"] != null)
                _scriptFields["OnMelee"].Text = utd.OnMelee.ToString();
            if (_scriptFields.ContainsKey("OnOpen") && _scriptFields["OnOpen"] != null)
                _scriptFields["OnOpen"].Text = utd.OnOpen.ToString();
            if (_scriptFields.ContainsKey("OnUnlock") && _scriptFields["OnUnlock"] != null)
                _scriptFields["OnUnlock"].Text = utd.OnUnlock.ToString();
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                _scriptFields["OnUserDefined"].Text = utd.OnUserDefined.ToString();

            // Comments
            if (_commentsEdit != null) _commentsEdit.Text = utd.Comment;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:265-330
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Basic
            _utd.Name = _utd.Name ?? LocalizedString.FromInvalid(); // Preserve existing or create new
            _utd.Tag = _tagEdit?.Text ?? "";
            _utd.ResRef = new ResRef(_resrefEdit?.Text ?? "");
            _utd.AppearanceId = _appearanceSelect?.SelectedIndex ?? 0;
            _utd.Conversation = new ResRef(_conversationEdit?.Text ?? "");

            // Advanced
            _utd.Min1Hp = _min1HpCheckbox?.IsChecked ?? false;
            _utd.Plot = _plotCheckbox?.IsChecked ?? false;
            _utd.Static = _staticCheckbox?.IsChecked ?? false;
            _utd.NotBlastable = _notBlastableCheckbox?.IsChecked ?? false;
            _utd.FactionId = _factionSelect?.SelectedIndex ?? 0;
            _utd.AnimationState = (int)(_animationStateSpin?.Value ?? 0);
            _utd.CurrentHp = (int)(_currentHpSpin?.Value ?? 0);
            _utd.MaximumHp = (int)(_maxHpSpin?.Value ?? 0);
            _utd.Hardness = (int)(_hardnessSpin?.Value ?? 0);
            _utd.Fortitude = (int)(_fortitudeSpin?.Value ?? 0);
            _utd.Reflex = (int)(_reflexSpin?.Value ?? 0);
            _utd.Willpower = (int)(_willSpin?.Value ?? 0);

            // Lock
            _utd.Locked = _lockedCheckbox?.IsChecked ?? false;
            _utd.UnlockDc = (int)(_openLockSpin?.Value ?? 0);
            _utd.UnlockDiff = (int)(_difficultySpin?.Value ?? 0);
            _utd.UnlockDiffMod = (int)(_difficultyModSpin?.Value ?? 0);
            _utd.KeyRequired = _needKeyCheckbox?.IsChecked ?? false;
            _utd.AutoRemoveKey = _removeKeyCheckbox?.IsChecked ?? false;
            _utd.KeyName = _keyEdit?.Text ?? "";

            // Scripts
            if (_scriptFields.ContainsKey("OnClick") && _scriptFields["OnClick"] != null)
                _utd.OnClick = new ResRef(_scriptFields["OnClick"].Text);
            if (_scriptFields.ContainsKey("OnClosed") && _scriptFields["OnClosed"] != null)
                _utd.OnClosed = new ResRef(_scriptFields["OnClosed"].Text);
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                _utd.OnDamaged = new ResRef(_scriptFields["OnDamaged"].Text);
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                _utd.OnDeath = new ResRef(_scriptFields["OnDeath"].Text);
            if (_scriptFields.ContainsKey("OnOpenFailed") && _scriptFields["OnOpenFailed"] != null)
                _utd.OnOpenFailed = new ResRef(_scriptFields["OnOpenFailed"].Text);
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                _utd.OnHeartbeat = new ResRef(_scriptFields["OnHeartbeat"].Text);
            if (_scriptFields.ContainsKey("OnMelee") && _scriptFields["OnMelee"] != null)
                _utd.OnMelee = new ResRef(_scriptFields["OnMelee"].Text);
            if (_scriptFields.ContainsKey("OnOpen") && _scriptFields["OnOpen"] != null)
                _utd.OnOpen = new ResRef(_scriptFields["OnOpen"].Text);
            if (_scriptFields.ContainsKey("OnUnlock") && _scriptFields["OnUnlock"] != null)
                _utd.OnUnlock = new ResRef(_scriptFields["OnUnlock"].Text);
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                _utd.OnUserDefined = new ResRef(_scriptFields["OnUserDefined"].Text);

            // Comments
            _utd.Comment = _commentsEdit?.Text ?? "";

            // Build GFF
            Game game = _installation?.Game ?? Game.K2;
            var gff = UTDHelpers.DismantleUtd(_utd, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTD);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:332-334
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _utd = new UTD();
            LoadUTD(_utd);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:336-340
        // Original: def change_name(self):
        private void EditName()
        {
            if (_installation == null) return;
            var dialog = new LocalizedStringDialog(this, _installation, _utd.Name);
            if (dialog.ShowDialog())
            {
                _utd.Name = dialog.LocString;
                if (_nameEdit != null)
                {
                    _nameEdit.Text = _installation.String(_utd.Name);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:342-345
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:347-351
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = !string.IsNullOrEmpty(base._resname) ? base._resname : "m00xx_dor_000";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utd.py:353-393
        // Original: def edit_conversation(self):
        private void EditConversation()
        {
            // Placeholder for conversation editor
            // Will be implemented when DLG editor is available
            System.Console.WriteLine("Conversation editor not yet implemented");
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
