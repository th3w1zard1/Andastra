using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Resource.Generics;
using AuroraEngine.Common.Resources;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using GFFAuto = AuroraEngine.Common.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:51
    // Original: class UTCEditor(Editor):
    public class UTCEditor : Editor
    {
        private UTC _utc;
        private HTInstallation _installation;

        // UI Controls - Basic
        private TextBox _firstNameEdit;
        private Button _firstNameRandomBtn;
        private TextBox _lastNameEdit;
        private Button _lastNameRandomBtn;
        private TextBox _tagEdit;
        private Button _tagGenerateBtn;
        private TextBox _resrefEdit;
        private ComboBox _appearanceSelect;
        private ComboBox _soundsetSelect;
        private ComboBox _portraitSelect;
        private Slider _alignmentSlider;
        private TextBox _conversationEdit;
        private Button _conversationModifyBtn;
        private Button _inventoryBtn;
        private TextBlock _inventoryCountLabel;

        // UI Controls - Advanced
        private CheckBox _disarmableCheckbox;
        private CheckBox _noPermDeathCheckbox;
        private CheckBox _min1HpCheckbox;
        private CheckBox _plotCheckbox;
        private CheckBox _isPcCheckbox;
        private CheckBox _noReorientateCheckbox;
        private CheckBox _noBlockCheckbox;
        private CheckBox _hologramCheckbox;
        private ComboBox _raceSelect;
        private ComboBox _subraceSelect;
        private ComboBox _speedSelect;
        private ComboBox _factionSelect;
        private ComboBox _genderSelect;
        private ComboBox _perceptionSelect;
        private NumericUpDown _challengeRatingSpin;
        private NumericUpDown _blindSpotSpin;
        private NumericUpDown _multiplierSetSpin;

        // UI Controls - Stats
        private NumericUpDown _strengthSpin;
        private NumericUpDown _dexteritySpin;
        private NumericUpDown _constitutionSpin;
        private NumericUpDown _intelligenceSpin;
        private NumericUpDown _wisdomSpin;
        private NumericUpDown _charismaSpin;
        private NumericUpDown _computerUseSpin;
        private NumericUpDown _demolitionsSpin;
        private NumericUpDown _stealthSpin;
        private NumericUpDown _awarenessSpin;
        private NumericUpDown _persuadeSpin;
        private NumericUpDown _repairSpin;
        private NumericUpDown _securitySpin;
        private NumericUpDown _treatInjurySpin;
        private NumericUpDown _fortitudeSpin;
        private NumericUpDown _reflexSpin;
        private NumericUpDown _willSpin;
        private NumericUpDown _armorClassSpin;
        private NumericUpDown _baseHpSpin;
        private NumericUpDown _currentHpSpin;
        private NumericUpDown _maxHpSpin;
        private NumericUpDown _currentFpSpin;
        private NumericUpDown _maxFpSpin;

        // UI Controls - Classes
        private ComboBox _class1Select;
        private NumericUpDown _class1LevelSpin;
        private ComboBox _class2Select;
        private NumericUpDown _class2LevelSpin;

        // UI Controls - Feats and Powers
        private ListBox _featList;
        private ListBox _powerList;

        // UI Controls - Scripts
        private Dictionary<string, TextBox> _scriptFields;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:52-105
        // Original: def __init__(self, parent, installation):
        public UTCEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Creature Editor", "creature",
                new[] { ResourceType.UTC, ResourceType.BTC, ResourceType.BIC },
                new[] { ResourceType.UTC, ResourceType.BTC, ResourceType.BIC },
                installation)
        {
            _installation = installation;
            _utc = new UTC();
            _scriptFields = new Dictionary<string, TextBox>();

            InitializeComponent();
            SetupUI();
            MinWidth = 798;
            MinHeight = 553;
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:186-218
        // Original: def _setup_signals(self):
        private void SetupProgrammaticUI()
        {
            var scrollViewer = new ScrollViewer();
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Basic Group
            var basicGroup = new Expander { Header = "Basic", IsExpanded = true };
            var basicPanel = new StackPanel { Orientation = Orientation.Vertical };

            // First Name
            var firstNameLabel = new TextBlock { Text = "First Name:" };
            _firstNameEdit = new TextBox { IsReadOnly = true };
            _firstNameRandomBtn = new Button { Content = "Random" };
            _firstNameRandomBtn.Click += (s, e) => RandomizeFirstName();
            basicPanel.Children.Add(firstNameLabel);
            basicPanel.Children.Add(_firstNameEdit);
            basicPanel.Children.Add(_firstNameRandomBtn);

            // Last Name
            var lastNameLabel = new TextBlock { Text = "Last Name:" };
            _lastNameEdit = new TextBox { IsReadOnly = true };
            _lastNameRandomBtn = new Button { Content = "Random" };
            _lastNameRandomBtn.Click += (s, e) => RandomizeLastName();
            basicPanel.Children.Add(lastNameLabel);
            basicPanel.Children.Add(_lastNameEdit);
            basicPanel.Children.Add(_lastNameRandomBtn);

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
            basicPanel.Children.Add(resrefLabel);
            basicPanel.Children.Add(_resrefEdit);

            // Appearance
            var appearanceLabel = new TextBlock { Text = "Appearance:" };
            _appearanceSelect = new ComboBox();
            basicPanel.Children.Add(appearanceLabel);
            basicPanel.Children.Add(_appearanceSelect);

            // Soundset
            var soundsetLabel = new TextBlock { Text = "Soundset:" };
            _soundsetSelect = new ComboBox();
            basicPanel.Children.Add(soundsetLabel);
            basicPanel.Children.Add(_soundsetSelect);

            // Portrait
            var portraitLabel = new TextBlock { Text = "Portrait:" };
            _portraitSelect = new ComboBox();
            _portraitSelect.SelectionChanged += (s, e) => PortraitChanged();
            basicPanel.Children.Add(portraitLabel);
            basicPanel.Children.Add(_portraitSelect);

            // Alignment
            var alignmentLabel = new TextBlock { Text = "Alignment:" };
            _alignmentSlider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
            _alignmentSlider.ValueChanged += (s, e) => PortraitChanged();
            basicPanel.Children.Add(alignmentLabel);
            basicPanel.Children.Add(_alignmentSlider);

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

            _disarmableCheckbox = new CheckBox { Content = "Disarmable" };
            _noPermDeathCheckbox = new CheckBox { Content = "No Perm Death" };
            _min1HpCheckbox = new CheckBox { Content = "Min 1 HP" };
            _plotCheckbox = new CheckBox { Content = "Plot" };
            _isPcCheckbox = new CheckBox { Content = "Is PC" };
            _noReorientateCheckbox = new CheckBox { Content = "No Reorientate" };
            _noBlockCheckbox = new CheckBox { Content = "No Block" };
            _hologramCheckbox = new CheckBox { Content = "Hologram" };

            var raceLabel = new TextBlock { Text = "Race:" };
            _raceSelect = new ComboBox();
            var subraceLabel = new TextBlock { Text = "Subrace:" };
            _subraceSelect = new ComboBox();
            var speedLabel = new TextBlock { Text = "Speed:" };
            _speedSelect = new ComboBox();
            var factionLabel = new TextBlock { Text = "Faction:" };
            _factionSelect = new ComboBox();
            var genderLabel = new TextBlock { Text = "Gender:" };
            _genderSelect = new ComboBox();
            var perceptionLabel = new TextBlock { Text = "Perception:" };
            _perceptionSelect = new ComboBox();
            var challengeRatingLabel = new TextBlock { Text = "Challenge Rating:" };
            _challengeRatingSpin = new NumericUpDown { Minimum = 0, Maximum = decimal.MaxValue };
            var blindSpotLabel = new TextBlock { Text = "Blind Spot:" };
            _blindSpotSpin = new NumericUpDown { Minimum = 0, Maximum = decimal.MaxValue };
            var multiplierSetLabel = new TextBlock { Text = "Multiplier Set:" };
            _multiplierSetSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };

            advancedPanel.Children.Add(_disarmableCheckbox);
            advancedPanel.Children.Add(_noPermDeathCheckbox);
            advancedPanel.Children.Add(_min1HpCheckbox);
            advancedPanel.Children.Add(_plotCheckbox);
            advancedPanel.Children.Add(_isPcCheckbox);
            advancedPanel.Children.Add(_noReorientateCheckbox);
            advancedPanel.Children.Add(_noBlockCheckbox);
            advancedPanel.Children.Add(_hologramCheckbox);
            advancedPanel.Children.Add(raceLabel);
            advancedPanel.Children.Add(_raceSelect);
            advancedPanel.Children.Add(subraceLabel);
            advancedPanel.Children.Add(_subraceSelect);
            advancedPanel.Children.Add(speedLabel);
            advancedPanel.Children.Add(_speedSelect);
            advancedPanel.Children.Add(factionLabel);
            advancedPanel.Children.Add(_factionSelect);
            advancedPanel.Children.Add(genderLabel);
            advancedPanel.Children.Add(_genderSelect);
            advancedPanel.Children.Add(perceptionLabel);
            advancedPanel.Children.Add(_perceptionSelect);
            advancedPanel.Children.Add(challengeRatingLabel);
            advancedPanel.Children.Add(_challengeRatingSpin);
            advancedPanel.Children.Add(blindSpotLabel);
            advancedPanel.Children.Add(_blindSpotSpin);
            advancedPanel.Children.Add(multiplierSetLabel);
            advancedPanel.Children.Add(_multiplierSetSpin);

            advancedGroup.Content = advancedPanel;
            mainPanel.Children.Add(advancedGroup);

            // Stats Group
            var statsGroup = new Expander { Header = "Stats", IsExpanded = false };
            var statsPanel = new StackPanel { Orientation = Orientation.Vertical };

            var strengthLabel = new TextBlock { Text = "Strength:" };
            _strengthSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var dexterityLabel = new TextBlock { Text = "Dexterity:" };
            _dexteritySpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var constitutionLabel = new TextBlock { Text = "Constitution:" };
            _constitutionSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var intelligenceLabel = new TextBlock { Text = "Intelligence:" };
            _intelligenceSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var wisdomLabel = new TextBlock { Text = "Wisdom:" };
            _wisdomSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var charismaLabel = new TextBlock { Text = "Charisma:" };
            _charismaSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };

            var computerUseLabel = new TextBlock { Text = "Computer Use:" };
            _computerUseSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var demolitionsLabel = new TextBlock { Text = "Demolitions:" };
            _demolitionsSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var stealthLabel = new TextBlock { Text = "Stealth:" };
            _stealthSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var awarenessLabel = new TextBlock { Text = "Awareness:" };
            _awarenessSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var persuadeLabel = new TextBlock { Text = "Persuade:" };
            _persuadeSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var repairLabel = new TextBlock { Text = "Repair:" };
            _repairSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var securityLabel = new TextBlock { Text = "Security:" };
            _securitySpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var treatInjuryLabel = new TextBlock { Text = "Treat Injury:" };
            _treatInjurySpin = new NumericUpDown { Minimum = 0, Maximum = 255 };

            var fortitudeLabel = new TextBlock { Text = "Fortitude Bonus:" };
            _fortitudeSpin = new NumericUpDown { Minimum = -32768, Maximum = 32767 };
            var reflexLabel = new TextBlock { Text = "Reflex Bonus:" };
            _reflexSpin = new NumericUpDown { Minimum = -32768, Maximum = 32767 };
            var willLabel = new TextBlock { Text = "Will Bonus:" };
            _willSpin = new NumericUpDown { Minimum = -32768, Maximum = 32767 };
            var armorClassLabel = new TextBlock { Text = "Natural AC:" };
            _armorClassSpin = new NumericUpDown { Minimum = 0, Maximum = 255 };
            var baseHpLabel = new TextBlock { Text = "Base HP:" };
            _baseHpSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };
            var currentHpLabel = new TextBlock { Text = "Current HP:" };
            _currentHpSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };
            var maxHpLabel = new TextBlock { Text = "Max HP:" };
            _maxHpSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };
            var currentFpLabel = new TextBlock { Text = "Current FP:" };
            _currentFpSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };
            var maxFpLabel = new TextBlock { Text = "Max FP:" };
            _maxFpSpin = new NumericUpDown { Minimum = 0, Maximum = 32767 };

            statsPanel.Children.Add(strengthLabel);
            statsPanel.Children.Add(_strengthSpin);
            statsPanel.Children.Add(dexterityLabel);
            statsPanel.Children.Add(_dexteritySpin);
            statsPanel.Children.Add(constitutionLabel);
            statsPanel.Children.Add(_constitutionSpin);
            statsPanel.Children.Add(intelligenceLabel);
            statsPanel.Children.Add(_intelligenceSpin);
            statsPanel.Children.Add(wisdomLabel);
            statsPanel.Children.Add(_wisdomSpin);
            statsPanel.Children.Add(charismaLabel);
            statsPanel.Children.Add(_charismaSpin);
            statsPanel.Children.Add(computerUseLabel);
            statsPanel.Children.Add(_computerUseSpin);
            statsPanel.Children.Add(demolitionsLabel);
            statsPanel.Children.Add(_demolitionsSpin);
            statsPanel.Children.Add(stealthLabel);
            statsPanel.Children.Add(_stealthSpin);
            statsPanel.Children.Add(awarenessLabel);
            statsPanel.Children.Add(_awarenessSpin);
            statsPanel.Children.Add(persuadeLabel);
            statsPanel.Children.Add(_persuadeSpin);
            statsPanel.Children.Add(repairLabel);
            statsPanel.Children.Add(_repairSpin);
            statsPanel.Children.Add(securityLabel);
            statsPanel.Children.Add(_securitySpin);
            statsPanel.Children.Add(treatInjuryLabel);
            statsPanel.Children.Add(_treatInjurySpin);
            statsPanel.Children.Add(fortitudeLabel);
            statsPanel.Children.Add(_fortitudeSpin);
            statsPanel.Children.Add(reflexLabel);
            statsPanel.Children.Add(_reflexSpin);
            statsPanel.Children.Add(willLabel);
            statsPanel.Children.Add(_willSpin);
            statsPanel.Children.Add(armorClassLabel);
            statsPanel.Children.Add(_armorClassSpin);
            statsPanel.Children.Add(baseHpLabel);
            statsPanel.Children.Add(_baseHpSpin);
            statsPanel.Children.Add(currentHpLabel);
            statsPanel.Children.Add(_currentHpSpin);
            statsPanel.Children.Add(maxHpLabel);
            statsPanel.Children.Add(_maxHpSpin);
            statsPanel.Children.Add(currentFpLabel);
            statsPanel.Children.Add(_currentFpSpin);
            statsPanel.Children.Add(maxFpLabel);
            statsPanel.Children.Add(_maxFpSpin);

            statsGroup.Content = statsPanel;
            mainPanel.Children.Add(statsGroup);

            // Classes Group
            var classesGroup = new Expander { Header = "Classes", IsExpanded = false };
            var classesPanel = new StackPanel { Orientation = Orientation.Vertical };

            var class1Label = new TextBlock { Text = "Class 1:" };
            _class1Select = new ComboBox();
            var class1LevelLabel = new TextBlock { Text = "Class 1 Level:" };
            _class1LevelSpin = new NumericUpDown { Minimum = 0, Maximum = 50 };
            var class2Label = new TextBlock { Text = "Class 2:" };
            _class2Select = new ComboBox();
            var class2LevelLabel = new TextBlock { Text = "Class 2 Level:" };
            _class2LevelSpin = new NumericUpDown { Minimum = 0, Maximum = 50 };

            classesPanel.Children.Add(class1Label);
            classesPanel.Children.Add(_class1Select);
            classesPanel.Children.Add(class1LevelLabel);
            classesPanel.Children.Add(_class1LevelSpin);
            classesPanel.Children.Add(class2Label);
            classesPanel.Children.Add(_class2Select);
            classesPanel.Children.Add(class2LevelLabel);
            classesPanel.Children.Add(_class2LevelSpin);

            classesGroup.Content = classesPanel;
            mainPanel.Children.Add(classesGroup);

            // Feats and Powers Group
            var featsPowersGroup = new Expander { Header = "Feats and Powers", IsExpanded = false };
            var featsPowersPanel = new StackPanel { Orientation = Orientation.Vertical };

            var featLabel = new TextBlock { Text = "Feats:" };
            _featList = new ListBox();
            var powerLabel = new TextBlock { Text = "Powers:" };
            _powerList = new ListBox();

            featsPowersPanel.Children.Add(featLabel);
            featsPowersPanel.Children.Add(_featList);
            featsPowersPanel.Children.Add(powerLabel);
            featsPowersPanel.Children.Add(_powerList);

            featsPowersGroup.Content = featsPowersPanel;
            mainPanel.Children.Add(featsPowersGroup);

            // Scripts Group
            var scriptsGroup = new Expander { Header = "Scripts", IsExpanded = false };
            var scriptsPanel = new StackPanel { Orientation = Orientation.Vertical };

            string[] scriptNames = { "OnBlocked", "OnAttacked", "OnNotice", "OnDialog", "OnDamaged",
                "OnDisturbed", "OnDeath", "OnEndRound", "OnEndDialog", "OnHeartbeat", "OnSpawn", "OnSpell", "OnUserDefined" };
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:365-535
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The UTC file data is empty or invalid.");
            }

            var gff = GFF.FromBytes(data);
            _utc = UTCHelpers.ConstructUtc(gff);
            LoadUTC(_utc);
            UpdateItemCount();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:376-535
        // Original: def _load_utc(self, utc):
        private void LoadUTC(UTC utc)
        {
            _utc = utc;

            // Basic
            if (_firstNameEdit != null)
            {
                _firstNameEdit.Text = _installation != null ? _installation.String(utc.FirstName) : utc.FirstName.StringRef.ToString();
            }
            if (_lastNameEdit != null)
            {
                _lastNameEdit.Text = _installation != null ? _installation.String(utc.LastName) : utc.LastName.StringRef.ToString();
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = utc.Tag;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = utc.ResRef.ToString();
            }
            if (_appearanceSelect != null)
            {
                _appearanceSelect.SelectedIndex = utc.AppearanceId;
            }
            if (_soundsetSelect != null)
            {
                _soundsetSelect.SelectedIndex = utc.SoundsetId;
            }
            if (_portraitSelect != null)
            {
                _portraitSelect.SelectedIndex = utc.PortraitId;
            }
            if (_alignmentSlider != null)
            {
                _alignmentSlider.Value = utc.Alignment;
            }
            if (_conversationEdit != null)
            {
                _conversationEdit.Text = utc.Conversation.ToString();
            }

            // Advanced
            if (_disarmableCheckbox != null) _disarmableCheckbox.IsChecked = utc.Disarmable;
            if (_noPermDeathCheckbox != null) _noPermDeathCheckbox.IsChecked = utc.NoPermDeath;
            if (_min1HpCheckbox != null) _min1HpCheckbox.IsChecked = utc.Min1Hp;
            if (_plotCheckbox != null) _plotCheckbox.IsChecked = utc.Plot;
            if (_isPcCheckbox != null) _isPcCheckbox.IsChecked = utc.IsPc;
            if (_noReorientateCheckbox != null) _noReorientateCheckbox.IsChecked = utc.NotReorienting;
            if (_noBlockCheckbox != null) _noBlockCheckbox.IsChecked = utc.IgnoreCrePath;
            if (_hologramCheckbox != null) _hologramCheckbox.IsChecked = utc.Hologram;
            if (_raceSelect != null) _raceSelect.SelectedIndex = utc.RaceId;
            if (_subraceSelect != null) _subraceSelect.SelectedIndex = utc.SubraceId;
            if (_speedSelect != null) _speedSelect.SelectedIndex = utc.WalkrateId;
            if (_factionSelect != null) _factionSelect.SelectedIndex = utc.FactionId;
            if (_genderSelect != null) _genderSelect.SelectedIndex = utc.GenderId;
            if (_perceptionSelect != null) _perceptionSelect.SelectedIndex = utc.PerceptionId;
            if (_challengeRatingSpin != null) _challengeRatingSpin.Value = (decimal?)utc.ChallengeRating;
            if (_blindSpotSpin != null) _blindSpotSpin.Value = (decimal?)utc.Blindspot;
            if (_multiplierSetSpin != null) _multiplierSetSpin.Value = utc.MultiplierSet;

            // Stats
            if (_strengthSpin != null) _strengthSpin.Value = utc.Strength;
            if (_dexteritySpin != null) _dexteritySpin.Value = utc.Dexterity;
            if (_constitutionSpin != null) _constitutionSpin.Value = utc.Constitution;
            if (_intelligenceSpin != null) _intelligenceSpin.Value = utc.Intelligence;
            if (_wisdomSpin != null) _wisdomSpin.Value = utc.Wisdom;
            if (_charismaSpin != null) _charismaSpin.Value = utc.Charisma;
            if (_computerUseSpin != null) _computerUseSpin.Value = utc.ComputerUse;
            if (_demolitionsSpin != null) _demolitionsSpin.Value = utc.Demolitions;
            if (_stealthSpin != null) _stealthSpin.Value = utc.Stealth;
            if (_awarenessSpin != null) _awarenessSpin.Value = utc.Awareness;
            if (_persuadeSpin != null) _persuadeSpin.Value = utc.Persuade;
            if (_repairSpin != null) _repairSpin.Value = utc.Repair;
            if (_securitySpin != null) _securitySpin.Value = utc.Security;
            if (_treatInjurySpin != null) _treatInjurySpin.Value = utc.TreatInjury;
            if (_fortitudeSpin != null) _fortitudeSpin.Value = utc.FortitudeBonus;
            if (_reflexSpin != null) _reflexSpin.Value = utc.ReflexBonus;
            if (_willSpin != null) _willSpin.Value = utc.WillpowerBonus;
            if (_armorClassSpin != null) _armorClassSpin.Value = utc.NaturalAc;
            if (_baseHpSpin != null) _baseHpSpin.Value = utc.Hp;
            if (_currentHpSpin != null) _currentHpSpin.Value = utc.CurrentHp;
            if (_maxHpSpin != null) _maxHpSpin.Value = utc.MaxHp;
            if (_currentFpSpin != null) _currentFpSpin.Value = utc.Fp;
            if (_maxFpSpin != null) _maxFpSpin.Value = utc.MaxFp;

            // Classes
            if (utc.Classes != null && utc.Classes.Count >= 1)
            {
                if (_class1Select != null) _class1Select.SelectedIndex = utc.Classes[0].ClassId;
                if (_class1LevelSpin != null) _class1LevelSpin.Value = utc.Classes[0].ClassLevel;
            }
            if (utc.Classes != null && utc.Classes.Count >= 2)
            {
                if (_class2Select != null) _class2Select.SelectedIndex = utc.Classes[1].ClassId + 1; // +1 for "[Unset]" placeholder
                if (_class2LevelSpin != null) _class2LevelSpin.Value = utc.Classes[1].ClassLevel;
            }

            // Feats
            if (_featList != null)
            {
                _featList.Items.Clear();
                // Feats would be loaded from 2DA and checked based on utc.Feats
                // Simplified for now - full implementation would populate from installation
            }

            // Powers
            if (_powerList != null)
            {
                _powerList.Items.Clear();
                // Powers would be loaded from 2DA and checked based on utc.Classes powers
                // Simplified for now - full implementation would populate from installation
            }

            // Scripts
            if (_scriptFields.ContainsKey("OnBlocked") && _scriptFields["OnBlocked"] != null)
                _scriptFields["OnBlocked"].Text = utc.OnBlocked.ToString();
            if (_scriptFields.ContainsKey("OnAttacked") && _scriptFields["OnAttacked"] != null)
                _scriptFields["OnAttacked"].Text = utc.OnAttacked.ToString();
            if (_scriptFields.ContainsKey("OnNotice") && _scriptFields["OnNotice"] != null)
                _scriptFields["OnNotice"].Text = utc.OnNotice.ToString();
            if (_scriptFields.ContainsKey("OnDialog") && _scriptFields["OnDialog"] != null)
                _scriptFields["OnDialog"].Text = utc.OnDialog.ToString();
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                _scriptFields["OnDamaged"].Text = utc.OnDamaged.ToString();
            if (_scriptFields.ContainsKey("OnDisturbed") && _scriptFields["OnDisturbed"] != null)
                _scriptFields["OnDisturbed"].Text = utc.OnDisturbed.ToString();
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                _scriptFields["OnDeath"].Text = utc.OnDeath.ToString();
            if (_scriptFields.ContainsKey("OnEndRound") && _scriptFields["OnEndRound"] != null)
                _scriptFields["OnEndRound"].Text = utc.OnEndRound.ToString();
            if (_scriptFields.ContainsKey("OnEndDialog") && _scriptFields["OnEndDialog"] != null)
                _scriptFields["OnEndDialog"].Text = utc.OnEndDialog.ToString();
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                _scriptFields["OnHeartbeat"].Text = utc.OnHeartbeat.ToString();
            if (_scriptFields.ContainsKey("OnSpawn") && _scriptFields["OnSpawn"] != null)
                _scriptFields["OnSpawn"].Text = utc.OnSpawn.ToString();
            if (_scriptFields.ContainsKey("OnSpell") && _scriptFields["OnSpell"] != null)
                _scriptFields["OnSpell"].Text = utc.OnSpell.ToString();
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                _scriptFields["OnUserDefined"].Text = utc.OnUserDefined.ToString();

            // Comments
            if (_commentsEdit != null) _commentsEdit.Text = utc.Comment;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:545-663
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Basic
            _utc.FirstName = _utc.FirstName ?? LocalizedString.FromInvalid();
            _utc.LastName = _utc.LastName ?? LocalizedString.FromInvalid();
            _utc.Tag = _tagEdit?.Text ?? "";
            _utc.ResRef = new ResRef(_resrefEdit?.Text ?? "");
            _utc.AppearanceId = _appearanceSelect?.SelectedIndex ?? 0;
            _utc.SoundsetId = _soundsetSelect?.SelectedIndex ?? 0;
            _utc.Conversation = new ResRef(_conversationEdit?.Text ?? "");
            _utc.PortraitId = _portraitSelect?.SelectedIndex ?? 0;
            _utc.Alignment = (int)(_alignmentSlider?.Value ?? 50);

            // Advanced
            _utc.Disarmable = _disarmableCheckbox?.IsChecked ?? false;
            _utc.NoPermDeath = _noPermDeathCheckbox?.IsChecked ?? false;
            _utc.Min1Hp = _min1HpCheckbox?.IsChecked ?? false;
            _utc.Plot = _plotCheckbox?.IsChecked ?? false;
            _utc.IsPc = _isPcCheckbox?.IsChecked ?? false;
            _utc.NotReorienting = _noReorientateCheckbox?.IsChecked ?? false;
            _utc.IgnoreCrePath = _noBlockCheckbox?.IsChecked ?? false;
            _utc.Hologram = _hologramCheckbox?.IsChecked ?? false;
            _utc.RaceId = _raceSelect?.SelectedIndex ?? 0;
            _utc.SubraceId = _subraceSelect?.SelectedIndex ?? 0;
            _utc.WalkrateId = _speedSelect?.SelectedIndex ?? 0;
            _utc.FactionId = _factionSelect?.SelectedIndex ?? 0;
            _utc.GenderId = _genderSelect?.SelectedIndex ?? 0;
            _utc.PerceptionId = _perceptionSelect?.SelectedIndex ?? 0;
            _utc.ChallengeRating = (float)(_challengeRatingSpin?.Value ?? 0);
            _utc.Blindspot = (float)(_blindSpotSpin?.Value ?? 0);
            _utc.MultiplierSet = (int)(_multiplierSetSpin?.Value ?? 0);

            // Stats
            _utc.Strength = (int)(_strengthSpin?.Value ?? 0);
            _utc.Dexterity = (int)(_dexteritySpin?.Value ?? 0);
            _utc.Constitution = (int)(_constitutionSpin?.Value ?? 0);
            _utc.Intelligence = (int)(_intelligenceSpin?.Value ?? 0);
            _utc.Wisdom = (int)(_wisdomSpin?.Value ?? 0);
            _utc.Charisma = (int)(_charismaSpin?.Value ?? 0);
            _utc.ComputerUse = (int)(_computerUseSpin?.Value ?? 0);
            _utc.Demolitions = (int)(_demolitionsSpin?.Value ?? 0);
            _utc.Stealth = (int)(_stealthSpin?.Value ?? 0);
            _utc.Awareness = (int)(_awarenessSpin?.Value ?? 0);
            _utc.Persuade = (int)(_persuadeSpin?.Value ?? 0);
            _utc.Repair = (int)(_repairSpin?.Value ?? 0);
            _utc.Security = (int)(_securitySpin?.Value ?? 0);
            _utc.TreatInjury = (int)(_treatInjurySpin?.Value ?? 0);
            _utc.FortitudeBonus = (int)(_fortitudeSpin?.Value ?? 0);
            _utc.ReflexBonus = (int)(_reflexSpin?.Value ?? 0);
            _utc.WillpowerBonus = (int)(_willSpin?.Value ?? 0);
            _utc.NaturalAc = (int)(_armorClassSpin?.Value ?? 0);
            _utc.Hp = (int)(_baseHpSpin?.Value ?? 0);
            _utc.CurrentHp = (int)(_currentHpSpin?.Value ?? 0);
            _utc.MaxHp = (int)(_maxHpSpin?.Value ?? 0);
            _utc.Fp = (int)(_currentFpSpin?.Value ?? 0);
            _utc.MaxFp = (int)(_maxFpSpin?.Value ?? 0);

            // Classes
            _utc.Classes.Clear();
            if (_class1Select?.SelectedIndex >= 0)
            {
                int classId = _class1Select.SelectedIndex;
                int classLevel = (int)(_class1LevelSpin?.Value ?? 0);
                _utc.Classes.Add(new UTCClass(classId, classLevel));
            }
            if (_class2Select?.SelectedIndex > 0) // > 0 because 0 is "[Unset]"
            {
                int classId = _class2Select.SelectedIndex - 1;
                int classLevel = (int)(_class2LevelSpin?.Value ?? 0);
                _utc.Classes.Add(new UTCClass(classId, classLevel));
            }

            // Feats - would need to be populated from checked items in _featList
            // Simplified for now - full implementation would read from list

            // Powers - would need to be populated from checked items in _powerList
            // Simplified for now - full implementation would read from list
            if (_utc.Classes.Count > 0)
            {
                // Powers would be added to the last class
            }

            // Scripts
            if (_scriptFields.ContainsKey("OnBlocked") && _scriptFields["OnBlocked"] != null)
                _utc.OnBlocked = new ResRef(_scriptFields["OnBlocked"].Text);
            if (_scriptFields.ContainsKey("OnAttacked") && _scriptFields["OnAttacked"] != null)
                _utc.OnAttacked = new ResRef(_scriptFields["OnAttacked"].Text);
            if (_scriptFields.ContainsKey("OnNotice") && _scriptFields["OnNotice"] != null)
                _utc.OnNotice = new ResRef(_scriptFields["OnNotice"].Text);
            if (_scriptFields.ContainsKey("OnDialog") && _scriptFields["OnDialog"] != null)
                _utc.OnDialog = new ResRef(_scriptFields["OnDialog"].Text);
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                _utc.OnDamaged = new ResRef(_scriptFields["OnDamaged"].Text);
            if (_scriptFields.ContainsKey("OnDisturbed") && _scriptFields["OnDisturbed"] != null)
                _utc.OnDisturbed = new ResRef(_scriptFields["OnDisturbed"].Text);
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                _utc.OnDeath = new ResRef(_scriptFields["OnDeath"].Text);
            if (_scriptFields.ContainsKey("OnEndRound") && _scriptFields["OnEndRound"] != null)
                _utc.OnEndRound = new ResRef(_scriptFields["OnEndRound"].Text);
            if (_scriptFields.ContainsKey("OnEndDialog") && _scriptFields["OnEndDialog"] != null)
                _utc.OnEndDialog = new ResRef(_scriptFields["OnEndDialog"].Text);
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                _utc.OnHeartbeat = new ResRef(_scriptFields["OnHeartbeat"].Text);
            if (_scriptFields.ContainsKey("OnSpawn") && _scriptFields["OnSpawn"] != null)
                _utc.OnSpawn = new ResRef(_scriptFields["OnSpawn"].Text);
            if (_scriptFields.ContainsKey("OnSpell") && _scriptFields["OnSpell"] != null)
                _utc.OnSpell = new ResRef(_scriptFields["OnSpell"].Text);
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                _utc.OnUserDefined = new ResRef(_scriptFields["OnUserDefined"].Text);

            // Comments
            _utc.Comment = _commentsEdit?.Text ?? "";

            // Build GFF
            Game game = _installation?.Game ?? Game.K2;
            var gff = UTCHelpers.DismantleUtc(_utc, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTC);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:665-668
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _utc = new UTC();
            LoadUTC(_utc);
            UpdateItemCount();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:670-676
        // Original: def randomize_first_name(self):
        private void RandomizeFirstName()
        {
            // Placeholder for LTR name generation
            // Will be implemented when LTR support is available
            System.Console.WriteLine("First name randomization not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:678-683
        // Original: def randomize_last_name(self):
        private void RandomizeLastName()
        {
            // Placeholder for LTR name generation
            // Will be implemented when LTR support is available
            System.Console.WriteLine("Last name randomization not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:685-686
        // Original: def generate_tag(self):
        private void GenerateTag()
        {
            if (_tagEdit != null && _resrefEdit != null)
            {
                _tagEdit.Text = _resrefEdit.Text;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:688-710
        // Original: def portrait_changed(self, _actual_combo_index):
        private void PortraitChanged()
        {
            // Placeholder for portrait preview update
            // Will be implemented when portrait loading is available
            System.Console.WriteLine("Portrait update not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:758-799
        // Original: def edit_conversation(self):
        private void EditConversation()
        {
            // Placeholder for conversation editor
            // Will be implemented when DLG editor is available
            System.Console.WriteLine("Conversation editor not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:801-835
        // Original: def open_inventory(self):
        private void OpenInventory()
        {
            // Placeholder for inventory editor
            // Will be implemented when InventoryEditor dialog is available
            System.Console.WriteLine("Inventory editor not yet implemented");
            // For now, just update the count
            UpdateItemCount();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:837-838
        // Original: def update_item_count(self):
        private void UpdateItemCount()
        {
            if (_inventoryCountLabel != null && _utc != null)
            {
                int count = _utc.Inventory != null ? _utc.Inventory.Count : 0;
                _inventoryCountLabel.Text = $"Total Items: {count}";
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
