using Andastra.Parsing.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Andastra.Parsing.Installation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.LTR;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using UTCHelpers = Andastra.Parsing.Resource.Generics.UTC.UTCHelpers;
using UTCClass = Andastra.Parsing.Resource.Generics.UTC.UTCClass;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Widgets;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;
using UTC = Andastra.Parsing.Resource.Generics.UTC.UTC;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using Andastra.Parsing.Formats.Capsule;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:51
    // Original: class UTCEditor(Editor):
    public class UTCEditor : Editor
    {
        private UTC _utc;
        private UTCEditorSettings _settings;
        private static GlobalSettings _globalSettings;

        // UI Controls - Basic
        private LocalizedStringEdit _firstNameEdit;
        private Button _firstNameRandomBtn;
        private LocalizedStringEdit _lastNameEdit;
        private Button _lastNameRandomBtn;
        private TextBox _tagEdit;
        private Button _tagGenerateBtn;
        private TextBox _resrefEdit;
        private ComboBox _appearanceSelect;
        private ComboBox _soundsetSelect;
        private ComboBox _portraitSelect;
        private Image _portraitPicture;
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
        private Expander _commentsExpander; // For tab title update testing

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:52-105
        // Original: def __init__(self, parent, installation):
        public UTCEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Creature Editor", "creature",
                new[] { ResourceType.UTC, ResourceType.BTC, ResourceType.BIC },
                new[] { ResourceType.UTC, ResourceType.BTC, ResourceType.BIC },
                installation)
        {
            _installation = installation;
            _utc = new Andastra.Parsing.Resource.Generics.UTC.UTC();
            _scriptFields = new Dictionary<string, TextBox>();
            _settings = new UTCEditorSettings();
            _globalSettings = new GlobalSettings();

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
            _firstNameEdit = new LocalizedStringEdit();
            _firstNameRandomBtn = new Button { Content = "Random" };
            _firstNameRandomBtn.Click += (s, e) => RandomizeFirstName();
            basicPanel.Children.Add(firstNameLabel);
            basicPanel.Children.Add(_firstNameEdit);
            basicPanel.Children.Add(_firstNameRandomBtn);

            // Last Name
            var lastNameLabel = new TextBlock { Text = "Last Name:" };
            _lastNameEdit = new LocalizedStringEdit();
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

            // Portrait Picture
            _portraitPicture = new Image
            {
                Width = 64,
                Height = 64,
                Stretch = Avalonia.Media.Stretch.Uniform
            };
            basicPanel.Children.Add(_portraitPicture);

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
            // Matching PyKotor implementation: Comments tab with title update functionality
            // Original: self.ui.commentsTab in tabWidget, _update_comments_tab_title() method
            _commentsExpander = new Expander { Header = "Comments", IsExpanded = false };
            var commentsPanel = new StackPanel { Orientation = Orientation.Vertical };
            var commentsLabel = new TextBlock { Text = "Comment:" };
            _commentsEdit = new TextBox { AcceptsReturn = true, AcceptsTab = true };
            // Matching PyKotor: Wire up text changed event to update tab title
            // Original: self.ui.comments.textChanged.connect(self._update_comments_tab_title)
            _commentsEdit.TextChanged += (s, e) => UpdateCommentsTabTitle();
            commentsPanel.Children.Add(commentsLabel);
            commentsPanel.Children.Add(_commentsEdit);
            _commentsExpander.Content = commentsPanel;
            mainPanel.Children.Add(_commentsExpander);

            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        private void SetupUI()
        {
            // Try to find controls from XAML if available
            // TODO: STUB - For now, programmatic UI is set up in SetupProgrammaticUI
        }

        /// <summary>
        /// Updates the Comments tab/expander title with a notification badge if comments are not blank.
        /// Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:558-564
        /// Original: def _update_comments_tab_title(self): Updates tab title with "*" indicator
        /// </summary>
        private void UpdateCommentsTabTitle()
        {
            // Matching PyKotor: comments = self.ui.comments.toPlainText()
            string comments = _commentsEdit?.Text ?? "";

            // Matching PyKotor: if comments: setTabText("Comments *") else: setTabText("Comments")
            if (_commentsExpander != null)
            {
                if (!string.IsNullOrWhiteSpace(comments))
                {
                    // Matching PyKotor: self.ui.tabWidget.setTabText(..., "Comments *")
                    _commentsExpander.Header = "Comments *";
                }
                else
                {
                    // Matching PyKotor: self.ui.tabWidget.setTabText(..., "Comments")
                    _commentsExpander.Header = "Comments";
                }
            }
        }

        /// <summary>
        /// Gets the Comments Expander for testing.
        /// Matching PyKotor: editor.ui.commentsTab for tab title testing
        /// </summary>
        public Expander CommentsExpander => _commentsExpander;

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
                _firstNameEdit.SetInstallation(_installation);
                _firstNameEdit.SetLocString(utc.FirstName);
            }
            if (_lastNameEdit != null)
            {
                _lastNameEdit.SetInstallation(_installation);
                _lastNameEdit.SetLocString(utc.LastName);
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
            if (_featList != null && _installation != null)
            {
                _featList.Items.Clear();
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:316-329
                // Original: feats: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_FEATS)
                TwoDA feats = _installation.HtGetCache2DA(HTInstallation.TwoDAFeats);
                if (feats != null)
                {
                    // First, uncheck all existing items
                    foreach (var existingItem in _featList.Items)
                    {
                        if (existingItem is CheckableListItem item)
                        {
                            item.IsChecked = false;
                        }
                    }

                    // Add all feats from 2DA
                    for (int i = 0; i < feats.GetHeight(); i++)
                    {
                        int featId = i;
                        int stringRef = feats.GetCellInt(i, "name", 0) ?? 0;
                        string text;
                        if (stringRef != 0 && _installation.TalkTable() != null)
                        {
                            text = _installation.TalkTable().GetString(stringRef);
                        }
                        else
                        {
                            text = feats.GetCellString(i, "label");
                        }
                        if (string.IsNullOrEmpty(text))
                        {
                            text = $"[Unused Feat ID: {featId}]";
                        }

                        var item = new CheckableListItem(text, featId);
                        _featList.Items.Add(item);
                    }

                    // Check feats that are in utc.Feats
                    if (utc.Feats != null)
                    {
                        foreach (int featId in utc.Feats)
                        {
                            var item = GetFeatItem(featId);
                            if (item == null)
                            {
                                // Modded feat not in 2DA - add it
                                item = new CheckableListItem($"[Modded Feat ID: {featId}]", featId);
                                _featList.Items.Add(item);
                            }
                            item.IsChecked = true;
                        }
                    }
                }
            }

            // Powers
            if (_powerList != null && _installation != null)
            {
                _powerList.Items.Clear();
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:331-345
                // Original: powers: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_POWERS)
                TwoDA powers = _installation.HtGetCache2DA(HTInstallation.TwoDAPowers);
                if (powers != null)
                {
                    // First, uncheck all existing items
                    foreach (var existingItem in _powerList.Items)
                    {
                        if (existingItem is CheckableListItem item)
                        {
                            item.IsChecked = false;
                        }
                    }

                    // Add all powers from 2DA
                    for (int i = 0; i < powers.GetHeight(); i++)
                    {
                        int powerId = i;
                        int stringRef = powers.GetCellInt(i, "name", 0) ?? 0;
                        string text;
                        if (stringRef != 0 && _installation.TalkTable() != null)
                        {
                            text = _installation.TalkTable().GetString(stringRef);
                        }
                        else
                        {
                            text = powers.GetCellString(i, "label");
                        }
                        if (!string.IsNullOrEmpty(text))
                        {
                            text = text.Replace("_", " ").Replace("XXX", "").Replace("\n", "");
                            // Title case
                            if (text.Length > 0)
                            {
                                text = char.ToUpper(text[0]) + (text.Length > 1 ? text.Substring(1).ToLower() : "");
                            }
                        }
                        if (string.IsNullOrEmpty(text))
                        {
                            text = $"[Unused Power ID: {powerId}]";
                        }

                        var item = new CheckableListItem(text, powerId);
                        _powerList.Items.Add(item);
                    }

                    // Check powers that are in utc.Classes powers
                    if (utc.Classes != null)
                    {
                        foreach (var utcClass in utc.Classes)
                        {
                            if (utcClass.Powers != null)
                            {
                                foreach (int powerId in utcClass.Powers)
                                {
                                    var item = GetPowerItem(powerId);
                                    if (item == null)
                                    {
                                        // Modded power not in 2DA - add it
                                        item = new CheckableListItem($"[Modded Power ID: {powerId}]", powerId);
                                        _powerList.Items.Add(item);
                                    }
                                    item.IsChecked = true;
                                }
                            }
                        }
                    }
                }
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
            if (_commentsEdit != null)
            {
                _commentsEdit.Text = utc.Comment;
                // Matching PyKotor: self._update_comments_tab_title() after loading comments
                UpdateCommentsTabTitle();
            }

            // Update portrait preview after loading all data
            PortraitChanged();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:545-663
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching Python: utc: UTC = deepcopy(self._utc)
            var utc = CopyUtc(_utc);

            // Basic - read from UI controls (matching Python which always reads from UI)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:561-562
            // Python: utc.first_name = self.ui.firstnameEdit.locstring()
            // In C#, firstNameEdit/lastNameEdit are LocalizedStringEdit widgets that store the LocalizedString
            utc.FirstName = _firstNameEdit?.GetLocString() ?? utc.FirstName ?? LocalizedString.FromInvalid();
            utc.LastName = _lastNameEdit?.GetLocString() ?? utc.LastName ?? LocalizedString.FromInvalid();
            utc.Tag = _tagEdit?.Text ?? "";
            utc.ResRef = new ResRef(_resrefEdit?.Text ?? "");
            utc.AppearanceId = _appearanceSelect?.SelectedIndex ?? 0;
            utc.SoundsetId = _soundsetSelect?.SelectedIndex ?? 0;
            utc.Conversation = new ResRef(_conversationEdit?.Text ?? "");
            utc.PortraitId = _portraitSelect?.SelectedIndex ?? 0;
            utc.Alignment = (int)(_alignmentSlider?.Value ?? 50);

            // Advanced - read from UI controls
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:569-576
            utc.Disarmable = _disarmableCheckbox?.IsChecked == true;
            utc.NoPermDeath = _noPermDeathCheckbox?.IsChecked == true;
            utc.Min1Hp = _min1HpCheckbox?.IsChecked == true;
            utc.Plot = _plotCheckbox?.IsChecked == true;
            utc.IsPc = _isPcCheckbox?.IsChecked == true;
            utc.NotReorienting = _noReorientateCheckbox?.IsChecked == true;
            utc.IgnoreCrePath = _noBlockCheckbox?.IsChecked == true;
            utc.Hologram = _hologramCheckbox?.IsChecked == true;
            utc.RaceId = _raceSelect?.SelectedIndex ?? 0;
            utc.SubraceId = _subraceSelect?.SelectedIndex ?? 0;
            utc.WalkrateId = _speedSelect?.SelectedIndex ?? 0;
            utc.FactionId = _factionSelect?.SelectedIndex ?? 0;
            utc.GenderId = _genderSelect?.SelectedIndex ?? 0;
            utc.PerceptionId = _perceptionSelect?.SelectedIndex ?? 0;
            utc.ChallengeRating = (float)(_challengeRatingSpin?.Value ?? 0);
            utc.Blindspot = (float)(_blindSpotSpin?.Value ?? 0);
            utc.MultiplierSet = (int)(_multiplierSetSpin?.Value ?? 0);

            // Stats - read from UI controls
            utc.Strength = (int)(_strengthSpin?.Value ?? 0);
            utc.Dexterity = (int)(_dexteritySpin?.Value ?? 0);
            utc.Constitution = (int)(_constitutionSpin?.Value ?? 0);
            utc.Intelligence = (int)(_intelligenceSpin?.Value ?? 0);
            utc.Wisdom = (int)(_wisdomSpin?.Value ?? 0);
            utc.Charisma = (int)(_charismaSpin?.Value ?? 0);
            utc.ComputerUse = (int)(_computerUseSpin?.Value ?? 0);
            utc.Demolitions = (int)(_demolitionsSpin?.Value ?? 0);
            utc.Stealth = (int)(_stealthSpin?.Value ?? 0);
            utc.Awareness = (int)(_awarenessSpin?.Value ?? 0);
            utc.Persuade = (int)(_persuadeSpin?.Value ?? 0);
            utc.Repair = (int)(_repairSpin?.Value ?? 0);
            utc.Security = (int)(_securitySpin?.Value ?? 0);
            utc.TreatInjury = (int)(_treatInjurySpin?.Value ?? 0);
            utc.FortitudeBonus = (int)(_fortitudeSpin?.Value ?? 0);
            utc.ReflexBonus = (int)(_reflexSpin?.Value ?? 0);
            utc.WillpowerBonus = (int)(_willSpin?.Value ?? 0);
            utc.NaturalAc = (int)(_armorClassSpin?.Value ?? 0);
            utc.Hp = (int)(_baseHpSpin?.Value ?? 0);
            utc.CurrentHp = (int)(_currentHpSpin?.Value ?? 0);
            utc.MaxHp = (int)(_maxHpSpin?.Value ?? 0);
            utc.Fp = (int)(_currentFpSpin?.Value ?? 0);
            utc.MaxFp = (int)(_maxFpSpin?.Value ?? 0);

            // Classes - read from UI controls
            utc.Classes.Clear();
            if (_class1Select?.SelectedIndex >= 0)
            {
                int classId = _class1Select.SelectedIndex;
                int classLevel = (int)(_class1LevelSpin?.Value ?? 0);
                utc.Classes.Add(new UTCClass(classId, classLevel));
            }
            if (_class2Select?.SelectedIndex > 0) // > 0 because 0 is "[Unset]"
            {
                int classId = _class2Select.SelectedIndex - 1;
                int classLevel = (int)(_class2LevelSpin?.Value ?? 0);
                utc.Classes.Add(new UTCClass(classId, classLevel));
            }

            // Feats - read from checked items in _featList
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:640-646
            // Original: utc.feats = []; for i in range(self.ui.featList.count()): ... if item.checkState() == Qt.CheckState.Checked: utc.feats.append(item.data(Qt.ItemDataRole.UserRole))
            utc.Feats.Clear();
            if (_featList != null)
            {
                foreach (var item in _featList.Items)
                {
                    if (item is CheckableListItem checkableItem && checkableItem.IsChecked)
                    {
                        utc.Feats.Add(checkableItem.Id);
                    }
                }
            }

            // Powers - read from checked items in _powerList and add to last class
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:647-653
            // Original: powers: list[int] = utc.classes[-1].powers; for i in range(self.ui.powerList.count()): ... if item.checkState() == Qt.CheckState.Checked: powers.append(item.data(Qt.ItemDataRole.UserRole))
            if (utc.Classes.Count > 0)
            {
                var lastClass = utc.Classes[utc.Classes.Count - 1];
                if (lastClass.Powers == null)
                {
                    lastClass.Powers = new List<int>();
                }
                else
                {
                    lastClass.Powers.Clear();
                }

                if (_powerList != null)
                {
                    foreach (var item in _powerList.Items)
                    {
                        if (item is CheckableListItem checkableItem && checkableItem.IsChecked)
                        {
                            lastClass.Powers.Add(checkableItem.Id);
                        }
                    }
                }
            }

            // Scripts - read from UI controls
            if (_scriptFields.ContainsKey("OnBlocked") && _scriptFields["OnBlocked"] != null)
                utc.OnBlocked = new ResRef(_scriptFields["OnBlocked"].Text);
            if (_scriptFields.ContainsKey("OnAttacked") && _scriptFields["OnAttacked"] != null)
                utc.OnAttacked = new ResRef(_scriptFields["OnAttacked"].Text);
            if (_scriptFields.ContainsKey("OnNotice") && _scriptFields["OnNotice"] != null)
                utc.OnNotice = new ResRef(_scriptFields["OnNotice"].Text);
            if (_scriptFields.ContainsKey("OnDialog") && _scriptFields["OnDialog"] != null)
                utc.OnDialog = new ResRef(_scriptFields["OnDialog"].Text);
            if (_scriptFields.ContainsKey("OnDamaged") && _scriptFields["OnDamaged"] != null)
                utc.OnDamaged = new ResRef(_scriptFields["OnDamaged"].Text);
            if (_scriptFields.ContainsKey("OnDisturbed") && _scriptFields["OnDisturbed"] != null)
                utc.OnDisturbed = new ResRef(_scriptFields["OnDisturbed"].Text);
            if (_scriptFields.ContainsKey("OnDeath") && _scriptFields["OnDeath"] != null)
                utc.OnDeath = new ResRef(_scriptFields["OnDeath"].Text);
            if (_scriptFields.ContainsKey("OnEndRound") && _scriptFields["OnEndRound"] != null)
                utc.OnEndRound = new ResRef(_scriptFields["OnEndRound"].Text);
            if (_scriptFields.ContainsKey("OnEndDialog") && _scriptFields["OnEndDialog"] != null)
                utc.OnEndDialog = new ResRef(_scriptFields["OnEndDialog"].Text);
            if (_scriptFields.ContainsKey("OnHeartbeat") && _scriptFields["OnHeartbeat"] != null)
                utc.OnHeartbeat = new ResRef(_scriptFields["OnHeartbeat"].Text);
            if (_scriptFields.ContainsKey("OnSpawn") && _scriptFields["OnSpawn"] != null)
                utc.OnSpawn = new ResRef(_scriptFields["OnSpawn"].Text);
            if (_scriptFields.ContainsKey("OnSpell") && _scriptFields["OnSpell"] != null)
                utc.OnSpell = new ResRef(_scriptFields["OnSpell"].Text);
            if (_scriptFields.ContainsKey("OnUserDefined") && _scriptFields["OnUserDefined"] != null)
                utc.OnUserDefined = new ResRef(_scriptFields["OnUserDefined"].Text);

            // Comments - read from UI controls
            utc.Comment = _commentsEdit?.Text ?? "";

            // Matching Python: gff: GFF = dismantle_utc(utc); write_gff(gff, data)
            Game game = _installation?.Game ?? Game.K2;
            var gff = Andastra.Parsing.Resource.Generics.UTC.UTCHelpers.DismantleUtc(utc, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTC);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching Python: deepcopy(self._utc)
        private static Andastra.Parsing.Resource.Generics.UTC.UTC CopyUtc(Andastra.Parsing.Resource.Generics.UTC.UTC source)
        {
            // Use Dismantle/Construct pattern for reliable deep copy (matching Python deepcopy behavior)
            Game game = Game.K2; // Default game for serialization
            var gff = Andastra.Parsing.Resource.Generics.UTC.UTCHelpers.DismantleUtc(source, game);
            return Andastra.Parsing.Resource.Generics.UTC.UTCHelpers.ConstructUtc(gff);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:665-668
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _utc = new Andastra.Parsing.Resource.Generics.UTC.UTC();
            LoadUTC(_utc);
            UpdateItemCount();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:670-676
        // Original: def randomize_first_name(self):
        private void RandomizeFirstName()
        {
            if (_installation == null)
            {
                System.Console.WriteLine("Cannot randomize first name: installation is not set");
                return;
            }

            // Determine LTR file based on gender: "humanf" if gender is 1 (female), "humanm" if male (0)
            // Matching Python: ltr_resname: Literal["humanf", "humanm"] = "humanf" if self.ui.genderSelect.currentIndex() == 1 else "humanm"
            int genderIndex = _genderSelect?.SelectedIndex ?? 0;
            string ltrResname = (genderIndex == 1) ? "humanf" : "humanm";

            try
            {
                // Load LTR resource from installation
                // Matching Python: ltr: LTR = read_ltr(self._installation.resource(ltr_resname, ResourceType.LTR).data)
                var resourceResult = _installation.Resource(ltrResname, ResourceType.LTR, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    System.Console.WriteLine($"Cannot randomize first name: LTR resource '{ltrResname}' not found");
                    return;
                }

                // Read LTR file
                LTR ltr = LTRAuto.ReadLtr(resourceResult.Data);

                // Generate random name
                // Matching Python: ltr.generate()
                string generatedName = ltr.Generate();

                // Update LocalizedString
                // Matching Python: locstring: LocalizedString = self.ui.firstnameEdit.locstring()
                // Matching Python: locstring.stringref = -1
                // Matching Python: locstring.set_data(Language.ENGLISH, Gender.MALE, ltr.generate())
                if (_utc.FirstName == null)
                {
                    _utc.FirstName = LocalizedString.FromInvalid();
                }
                _utc.FirstName.StringRef = -1;
                _utc.FirstName.SetData(Language.English, Gender.Male, generatedName);

                // Update UI display
                // Matching Python: self.ui.firstnameEdit.set_locstring(locstring)
                if (_firstNameEdit != null)
                {
                    _firstNameEdit.SetLocString(_utc.FirstName);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error randomizing first name: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:678-683
        // Original: def randomize_last_name(self):
        private void RandomizeLastName()
        {
            if (_installation == null)
            {
                System.Console.WriteLine("Cannot randomize last name: installation is not set");
                return;
            }

            // Always use "humanl" for last names
            // Matching Python: ltr: LTR = read_ltr(self._installation.resource("humanl", ResourceType.LTR).data)
            string ltrResname = "humanl";

            try
            {
                // Load LTR resource from installation
                var resourceResult = _installation.Resource(ltrResname, ResourceType.LTR, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    System.Console.WriteLine($"Cannot randomize last name: LTR resource '{ltrResname}' not found");
                    return;
                }

                // Read LTR file
                LTR ltr = LTRAuto.ReadLtr(resourceResult.Data);

                // Generate random name
                // Matching Python: ltr.generate()
                string generatedName = ltr.Generate();

                // Update LocalizedString
                // Matching Python: locstring: LocalizedString = self.ui.lastnameEdit.locstring()
                // Matching Python: locstring.stringref = -1
                // Matching Python: locstring.set_data(Language.ENGLISH, Gender.MALE, ltr.generate())
                if (_utc.LastName == null)
                {
                    _utc.LastName = LocalizedString.FromInvalid();
                }
                _utc.LastName.StringRef = -1;
                _utc.LastName.SetData(Language.English, Gender.Male, generatedName);

                // Update UI display
                // Matching Python: self.ui.lastnameEdit.set_locstring(locstring)
                if (_lastNameEdit != null)
                {
                    _lastNameEdit.SetLocString(_utc.LastName);
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error randomizing last name: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
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
            if (_portraitPicture == null)
            {
                return;
            }

            int index = _portraitSelect?.SelectedIndex ?? 0;

            // Matching Python: if index == 0, create blank image
            if (index == 0)
            {
                // Create blank 64x64 RGB image (black)
                var blankBitmap = new WriteableBitmap(
                    new PixelSize(64, 64),
                    new Vector(96, 96),
                    PixelFormat.Rgba8888,
                    AlphaFormat.Premul);
                using (var lockedBitmap = blankBitmap.Lock())
                {
                    // Fill with black (zeros)
                    System.Runtime.InteropServices.Marshal.Copy(
                        new byte[64 * 64 * 4], 0, lockedBitmap.Address, 64 * 64 * 4);
                }
                _portraitPicture.Source = blankBitmap;
                ToolTip.SetTip(_portraitPicture, GeneratePortraitTooltip());
                return;
            }

            // Build pixmap from index
            var bitmap = BuildPortraitBitmap(index);
            if (bitmap != null)
            {
                _portraitPicture.Source = bitmap;
            }
            else
            {
                // Fallback to blank image if build failed
                var blankBitmap = new WriteableBitmap(
                    new PixelSize(64, 64),
                    new Vector(96, 96),
                    PixelFormat.Rgba8888,
                    AlphaFormat.Premul);
                using (var lockedBitmap = blankBitmap.Lock())
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        new byte[64 * 64 * 4], 0, lockedBitmap.Address, 64 * 64 * 4);
                }
                _portraitPicture.Source = blankBitmap;
            }

            // Set tooltip
            ToolTip.SetTip(_portraitPicture, GeneratePortraitTooltip());
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:712-756
        // Original: def _build_pixmap(self, index: int) -> QPixmap:
        /// <summary>
        /// Builds a portrait bitmap based on character alignment.
        ///
        /// Builds the portrait bitmap by:
        ///     1. Getting the character's alignment value
        ///     2. Looking up the character's portrait reference in the portraits 2DA based on alignment
        ///     3. Loading the texture for the portrait reference
        ///     4. Converting the texture to a Bitmap.
        /// </summary>
        /// <param name="index">The character index to build a portrait for.</param>
        /// <returns>A Bitmap of the character portrait, or null if loading fails.</returns>
        private Bitmap BuildPortraitBitmap(int index)
        {
            if (_installation == null)
            {
                return null;
            }

            // Get alignment value
            int alignment = (int)(_alignmentSlider?.Value ?? 50);

            // Get portraits 2DA
            TwoDA portraits = _installation.HtGetCache2DA(HTInstallation.TwoDAPortraits);
            if (portraits == null)
            {
                System.Console.WriteLine("Cannot build portrait: portraits.2da not found");
                return null;
            }

            // Get base portrait resref
            string portrait = portraits.GetCellString(index, "baseresref");
            if (string.IsNullOrEmpty(portrait))
            {
                System.Console.WriteLine($"Cannot build portrait: baseresref not found for index {index}");
                return null;
            }

            // Check alignment-based variants (matching Python logic)
            // Python: if 40 >= alignment > 30 and portraits.get_cell(index, "baseresrefe"):
            if (alignment <= 40 && alignment > 30)
            {
                string variant = portraits.GetCellString(index, "baseresrefe");
                if (!string.IsNullOrEmpty(variant))
                {
                    portrait = variant;
                }
            }
            // Python: elif 30 >= alignment > 20 and portraits.get_cell(index, "baseresrefve"):
            else if (alignment <= 30 && alignment > 20)
            {
                string variant = portraits.GetCellString(index, "baseresrefve");
                if (!string.IsNullOrEmpty(variant))
                {
                    portrait = variant;
                }
            }
            // Python: elif 20 >= alignment > 10 and portraits.get_cell(index, "baseresrefvve"):
            else if (alignment <= 20 && alignment > 10)
            {
                string variant = portraits.GetCellString(index, "baseresrefvve");
                if (!string.IsNullOrEmpty(variant))
                {
                    portrait = variant;
                }
            }
            // Python: elif alignment <= 10 and portraits.get_cell(index, "baseresrefvvve"):
            else if (alignment <= 10)
            {
                string variant = portraits.GetCellString(index, "baseresrefvvve");
                if (!string.IsNullOrEmpty(variant))
                {
                    portrait = variant;
                }
            }

            // Load texture from installation
            // Matching Python: texture: TPC | None = self._installation.texture(portrait, [SearchLocation.TEXTURES_GUI])
            TPC texture = _installation.Texture(portrait, new[] { SearchLocation.TEXTURES_GUI });
            if (texture == null)
            {
                System.Console.WriteLine($"Cannot build portrait: texture '{portrait}' not found");
                // Return blank image on failure (matching Python behavior)
                var blankBitmap = new WriteableBitmap(
                    new PixelSize(64, 64),
                    new Vector(96, 96),
                    PixelFormat.Rgba8888,
                    AlphaFormat.Premul);
                using (var lockedBitmap = blankBitmap.Lock())
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        new byte[64 * 64 * 4], 0, lockedBitmap.Address, 64 * 64 * 4);
                }
                return blankBitmap;
            }

            // Get first mipmap from first layer
            // Note: DXT decompression is handled automatically by ConvertTpcMipmapToAvaloniaBitmap
            // Matching Python: mipmap: TPCMipmap = texture.get(0, 0)
            if (texture.Layers == null || texture.Layers.Count == 0 ||
                texture.Layers[0].Mipmaps == null || texture.Layers[0].Mipmaps.Count == 0)
            {
                System.Console.WriteLine($"Cannot build portrait: texture '{portrait}' has no mipmaps");
                var blankBitmap = new WriteableBitmap(
                    new PixelSize(64, 64),
                    new Vector(96, 96),
                    PixelFormat.Rgba8888,
                    AlphaFormat.Premul);
                using (var lockedBitmap = blankBitmap.Lock())
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        new byte[64 * 64 * 4], 0, lockedBitmap.Address, 64 * 64 * 4);
                }
                return blankBitmap;
            }

            TPCMipmap mipmap = texture.Layers[0].Mipmaps[0];

            // Convert TPC mipmap to Avalonia Bitmap
            // Matching Python: image = QImage(bytes(mipmap.data), mipmap.width, mipmap.height, texture.format().to_qimage_format())
            // Matching Python: return QPixmap.fromImage(image).transformed(QTransform().scale(1, -1))
            // Note: Python flips vertically with scale(1, -1), but Avalonia handles this differently
            var bitmap = HTInstallation.ConvertTpcMipmapToAvaloniaBitmap(mipmap);
            if (bitmap == null)
            {
                System.Console.WriteLine($"Cannot build portrait: failed to convert texture '{portrait}' to bitmap");
                var blankBitmap = new WriteableBitmap(
                    new PixelSize(64, 64),
                    new Vector(96, 96),
                    PixelFormat.Rgba8888,
                    AlphaFormat.Premul);
                using (var lockedBitmap = blankBitmap.Lock())
                {
                    System.Runtime.InteropServices.Marshal.Copy(
                        new byte[64 * 64 * 4], 0, lockedBitmap.Address, 64 * 64 * 4);
                }
                return blankBitmap;
            }

            return bitmap;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:112-120
        // Original: def _generate_portrait_tooltip(self, *, as_html: bool = False) -> str:
        /// <summary>
        /// Generates a detailed tooltip for the portrait picture.
        /// </summary>
        /// <returns>The tooltip text.</returns>
        private string GeneratePortraitTooltip()
        {
            string portrait = GetPortraitResref();
            // Matching Python: tooltip = f"<b>Portrait:</b> {portrait}<br>" "<br><i>Right-click for more options.</i>"
            // For Avalonia, we use plain text (HTML not supported in standard tooltips)
            return $"Portrait: {portrait}\n\nRight-click for more options.";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:146-160
        // Original: def _get_portrait_resref(self) -> str:
        /// <summary>
        /// Gets the portrait resref based on the selected index and alignment.
        /// </summary>
        /// <returns>The portrait resref string.</returns>
        private string GetPortraitResref()
        {
            if (_installation == null)
            {
                return "Unknown";
            }

            int index = _portraitSelect?.SelectedIndex ?? 0;
            int alignment = (int)(_alignmentSlider?.Value ?? 50);

            TwoDA portraits = _installation.HtGetCache2DA(HTInstallation.TwoDAPortraits);
            if (portraits == null)
            {
                return "Unknown";
            }

            string result = portraits.GetCellString(index, "baseresref");
            if (string.IsNullOrEmpty(result))
            {
                return "Unknown";
            }

            // Check alignment-based variants (matching Python logic)
            if (alignment <= 40 && alignment > 30)
            {
                string variant = portraits.GetCellString(index, "baseresrefe");
                if (!string.IsNullOrEmpty(variant))
                {
                    result = variant;
                }
            }
            else if (alignment <= 30 && alignment > 20)
            {
                string variant = portraits.GetCellString(index, "baseresrefve");
                if (!string.IsNullOrEmpty(variant))
                {
                    result = variant;
                }
            }
            else if (alignment <= 20 && alignment > 10)
            {
                string variant = portraits.GetCellString(index, "baseresrefvve");
                if (!string.IsNullOrEmpty(variant))
                {
                    result = variant;
                }
            }
            else if (alignment <= 10)
            {
                string variant = portraits.GetCellString(index, "baseresrefvvve");
                if (!string.IsNullOrEmpty(variant))
                {
                    result = variant;
                }
            }

            return result;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:758-799
        // Original: def edit_conversation(self):
        private void EditConversation()
        {
            if (_installation == null)
            {
                System.Console.WriteLine("Installation is not set");
                return;
            }

            string resname = _conversationEdit?.Text ?? "";
            if (string.IsNullOrEmpty(resname))
            {
                // Matching PyKotor: QMessageBox(QMessageBox.Icon.Critical, "Invalid Dialog Reference", "Conversation field cannot be blank.").exec()
                var messageBox = MessageBoxManager.GetMessageBoxStandard(
                    "Invalid Dialog Reference",
                    "Conversation field cannot be blank.",
                    ButtonEnum.Ok,
                    MsBox.Avalonia.Enums.Icon.Error);
                messageBox.ShowAsync();
                return;
            }

            // Search for the DLG resource
            ResourceResult search = _installation.Resource(resname, ResourceType.DLG);
            string filepath = null;
            byte[] data = null;

            if (search == null)
            {
                // DLG not found - ask to create new
                // Matching PyKotor: QMessageBox asking "Do you wish to create a new dialog in the 'Override' folder?"
                var createDialog = MessageBoxManager.GetMessageBoxStandard(
                    "DLG file not found",
                    "Do you wish to create a new dialog in the 'Override' folder?",
                    ButtonEnum.YesNo,
                    MsBox.Avalonia.Enums.Icon.Question);
                var result = createDialog.ShowAsync().GetAwaiter().GetResult();

                if (result == ButtonResult.Yes)
                {
                    // Create blank DLG file in override folder
                    string overridePath = _installation.OverridePath();
                    if (!string.IsNullOrEmpty(overridePath))
                    {
                        filepath = System.IO.Path.Combine(overridePath, $"{resname}.dlg");
                        Game game = _installation.Game;
                        var blankDlg = new Andastra.Parsing.Resource.Generics.DLG.DLG();
                        var gff = Andastra.Parsing.Resource.Generics.DLG.DLGHelper.DismantleDlg(blankDlg, game);
                        data = GFFAuto.BytesGff(gff, ResourceType.DLG);
                        System.IO.File.WriteAllBytes(filepath, data);
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                filepath = search.FilePath;
                if (search.Data != null)
                {
                    data = search.Data;
                }
                else if (!string.IsNullOrEmpty(filepath) && System.IO.File.Exists(filepath))
                {
                    data = System.IO.File.ReadAllBytes(filepath);
                }
            }

            if (data == null || string.IsNullOrEmpty(filepath))
            {
                System.Console.WriteLine($"Data/filepath cannot be null in EditConversation() (resname={resname}, filepath={filepath})");
                return;
            }

            // Open DLG editor
            // Matching PyKotor: open_resource_editor(filepath, resname, ResourceType.DLG, data, self._installation, self)
            HolocronToolset.Editors.WindowUtils.OpenResourceEditor(
                filepath,
                resname,
                ResourceType.DLG,
                data,
                _installation,
                this);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:801-835
        // Original: def open_inventory(self):
        private void OpenInventory()
        {
            if (_installation == null || _utc == null)
            {
                System.Console.WriteLine("Installation or UTC is not set");
                return;
            }

            // Determine if droid (race ID 0 = Droid)
            bool droid = _raceSelect?.SelectedIndex == 0;

            // Load capsules to search
            // Matching PyKotor: capsules_to_search based on filepath type
            List<Andastra.Parsing.Formats.Capsule.Capsule> capsulesToSearch = new List<Andastra.Parsing.Formats.Capsule.Capsule>();

            if (_filepath != null)
            {
                if (Andastra.Parsing.Tools.FileHelpers.IsSavFile(_filepath))
                {
                    // Search capsules inside the .sav outer capsule
                    // Matching PyKotor: capsules_to_search = [Capsule(res.filepath()) for res in Capsule(self._filepath) if is_capsule_file(res.filename()) and res.inside_capsule]
                    try
                    {
                        var outerCapsule = new Andastra.Parsing.Formats.Capsule.Capsule(_filepath);
                        foreach (var res in outerCapsule)
                        {
                            // Check if the resource name (resname + extension) is a capsule file
                            string resourceFilename = $"{res.ResName}.{res.ResType.Extension}";
                            if (Andastra.Parsing.Tools.FileHelpers.IsCapsuleFile(resourceFilename))
                            {
                                // The resource is inside a capsule (since we're iterating through a capsule)
                                // Construct the nested capsule path: outerCapsulePath/resourceFilename
                                string nestedCapsulePath = System.IO.Path.Combine(_filepath, resourceFilename);
                                try
                                {
                                    capsulesToSearch.Add(new Andastra.Parsing.Formats.Capsule.Capsule(nestedCapsulePath));
                                }
                                catch
                                {
                                    // Skip invalid capsules
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Failed to load outer capsule
                    }
                }
                else if (Andastra.Parsing.Tools.FileHelpers.IsCapsuleFile(_filepath))
                {
                    // Get capsules matching the module
                    // Matching PyKotor: capsules_to_search = Module.get_capsules_tuple_matching(self._installation, self._filepath.name)
                    // This finds all capsules in the module that match the current file's module
                    try
                    {
                        string root = null;
                        if (!string.IsNullOrEmpty(_filepath))
                        {
                            // Extract root from filepath (similar to Module.filepath_to_root)
                            string filename = System.IO.Path.GetFileName(_filepath);
                            if (filename.Contains("_"))
                            {
                                root = filename.Substring(0, filename.IndexOf('_'));
                            }
                            else if (filename.Contains("."))
                            {
                                root = filename.Substring(0, filename.IndexOf('.'));
                            }
                        }

                        if (root != null)
                        {
                            string caseRoot = root.ToLowerInvariant();
                            var moduleNames = _installation.ModuleNames();
                            string filepathFilename = System.IO.Path.GetFileName(_filepath) ?? "";

                            foreach (var kvp in moduleNames)
                            {
                                string moduleFilename = kvp.Key;
                                string moduleFilenameLower = moduleFilename.ToLowerInvariant();

                                // Check if root is contained in module filename and it's not the same as the current filepath
                                if (moduleFilenameLower.Contains(caseRoot) && moduleFilename != filepathFilename)
                                {
                                    string fullModulePath = System.IO.Path.Combine(_installation.ModulePath(), moduleFilename);
                                    if (System.IO.File.Exists(fullModulePath))
                                    {
                                        try
                                        {
                                            var capsule = new Andastra.Parsing.Formats.Capsule.Capsule(fullModulePath, createIfNotExist: false);
                                            capsulesToSearch.Add(capsule);
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
                    catch
                    {
                        // Failed to get module capsules - continue with empty list
                    }
                }
            }

            // Create inventory dialog
            // Matching PyKotor: InventoryEditor(self, self._installation, capsules_to_search, [], self._utc.inventory, self._utc.equipment, droid=droid)
            var inventoryDialog = new InventoryDialog(
                this,
                _installation,
                capsulesToSearch,
                new List<string>(), // folders - not used in UTC editor
                _utc.Inventory ?? new List<InventoryItem>(),
                _utc.Equipment ?? new Dictionary<EquipmentSlot, InventoryItem>(),
                droid: droid);

            // Show dialog and update if OK was clicked
            // Matching PyKotor: if inventory_editor.exec(): self._utc.inventory = inventory_editor.inventory; self._utc.equipment = inventory_editor.equipment; self.update_item_count(); self.update3dPreview()
            bool result = inventoryDialog.ShowDialog();
            if (result)
            {
                _utc.Inventory = inventoryDialog.Inventory;
                _utc.Equipment = inventoryDialog.Equipment;
                UpdateItemCount();
                // Note: update3dPreview() would be called here if 3D preview is implemented
            }
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:840-851
        // Original: def get_feat_item(self, feat_id: int) -> QListWidgetItem | None:
        private CheckableListItem GetFeatItem(int featId)
        {
            if (_featList == null)
            {
                return null;
            }

            foreach (var item in _featList.Items)
            {
                if (item is CheckableListItem checkableItem && checkableItem.Id == featId)
                {
                    return checkableItem;
                }
            }
            return null;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/utc.py:853-864
        // Original: def get_power_item(self, power_id: int) -> QListWidgetItem | None:
        private CheckableListItem GetPowerItem(int powerId)
        {
            if (_powerList == null)
            {
                return null;
            }

            foreach (var item in _powerList.Items)
            {
                if (item is CheckableListItem checkableItem && checkableItem.Id == powerId)
                {
                    return checkableItem;
                }
            }
            return null;
        }

        public override void SaveAs()
        {
            Save();
        }

        // Expose Settings for testing (matching Python implementation)
        public UTCEditorSettings Settings => _settings;

        // Expose GlobalSettings for testing (matching Python implementation)
        public GlobalSettings GlobalSettings => _globalSettings;
    }

    // Helper class for checkable list items in Avalonia ListBox
    // Matching PyKotor QListWidgetItem with checkable state and UserRole data
    public class CheckableListItem : Avalonia.Controls.ContentControl
    {
        private CheckBox _checkBox;
        private TextBlock _textBlock;
        private int _id;
        private bool _isChecked;

        public int Id
        {
            get { return _id; }
            set { _id = value; }
        }

        public bool IsChecked
        {
            get { return _isChecked; }
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    if (_checkBox != null)
                    {
                        _checkBox.IsChecked = value;
                    }
                }
            }
        }

        public string Text
        {
            get { return _textBlock?.Text ?? ""; }
            set
            {
                if (_textBlock != null)
                {
                    _textBlock.Text = value ?? "";
                }
            }
        }

        public CheckableListItem(string text, int id)
        {
            _id = id;
            _isChecked = false;

            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(2)
            };

            _checkBox = new CheckBox
            {
                IsChecked = false,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            _checkBox.Checked += (s, e) => { _isChecked = true; };
            _checkBox.Unchecked += (s, e) => { _isChecked = false; };

            _textBlock = new TextBlock
            {
                Text = text ?? "",
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0)
            };

            panel.Children.Add(_checkBox);
            panel.Children.Add(_textBlock);
            Content = panel;
        }
    }
}

