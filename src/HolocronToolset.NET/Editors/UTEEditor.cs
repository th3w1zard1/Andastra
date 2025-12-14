using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resource.Generics;
using CSharpKOTOR.Resources;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using HolocronToolset.NET.Widgets;
using HolocronToolset.NET.Widgets.Edit;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:26
    // Original: class UTEEditor(Editor):
    public class UTEEditor : Editor
    {
        private UTE _ute;
        private HTInstallation _installation;
        private List<string> _relevantCreatureResnames;
        private List<string> _relevantScriptResnames;

        // UI Controls - Basic
        private LocalizedStringEdit _nameEdit;
        private Button _nameEditBtn;
        private TextBox _tagEdit;
        private Button _tagGenerateBtn;
        private TextBox _resrefEdit;
        private Button _resrefGenerateBtn;
        private ComboBox2DA _difficultySelect;
        private ComboBox _spawnSelect;
        private NumericUpDown _minCreatureSpin;
        private NumericUpDown _maxCreatureSpin;

        // UI Controls - Advanced
        private CheckBox _activeCheckbox;
        private CheckBox _playerOnlyCheckbox;
        private ComboBox2DA _factionSelect;
        private CheckBox _respawnsCheckbox;
        private CheckBox _infiniteRespawnCheckbox;
        private NumericUpDown _respawnTimeSpin;
        private NumericUpDown _respawnCountSpin;

        // UI Controls - Creatures
        private DataGrid _creatureTable;
        private Button _addCreatureButton;
        private Button _removeCreatureButton;

        // UI Controls - Scripts
        private ComboBox _onEnterSelect;
        private ComboBox _onExitSelect;
        private ComboBox _onExhaustedEdit;
        private ComboBox _onHeartbeatSelect;
        private ComboBox _onUserDefinedSelect;

        // UI Controls - Comments
        private TextBox _commentsEdit;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:27-66
        // Original: def __init__(self, parent, installation):
        public UTEEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Encounter Editor", "encounter",
                new[] { ResourceType.UTE, ResourceType.BTE },
                new[] { ResourceType.UTE, ResourceType.BTE },
                installation)
        {
            _installation = installation;
            _ute = new UTE();
            _relevantCreatureResnames = new List<string>();
            _relevantScriptResnames = new List<string>();

            InitializeComponent();
            SetupSignals();
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
                _difficultySelect = this.FindControl<ComboBox2DA>("difficultySelect");
                _spawnSelect = this.FindControl<ComboBox>("spawnSelect");
                _minCreatureSpin = this.FindControl<NumericUpDown>("minCreatureSpin");
                _maxCreatureSpin = this.FindControl<NumericUpDown>("maxCreatureSpin");
                _activeCheckbox = this.FindControl<CheckBox>("activeCheckbox");
                _playerOnlyCheckbox = this.FindControl<CheckBox>("playerOnlyCheckbox");
                _factionSelect = this.FindControl<ComboBox2DA>("factionSelect");
                _respawnsCheckbox = this.FindControl<CheckBox>("respawnsCheckbox");
                _infiniteRespawnCheckbox = this.FindControl<CheckBox>("infiniteRespawnCheckbox");
                _respawnTimeSpin = this.FindControl<NumericUpDown>("respawnTimeSpin");
                _respawnCountSpin = this.FindControl<NumericUpDown>("respawnCountSpin");
                _creatureTable = this.FindControl<DataGrid>("creatureTable");
                _addCreatureButton = this.FindControl<Button>("addCreatureButton");
                _removeCreatureButton = this.FindControl<Button>("removeCreatureButton");
                _onEnterSelect = this.FindControl<ComboBox>("onEnterSelect");
                _onExitSelect = this.FindControl<ComboBox>("onExitSelect");
                _onExhaustedEdit = this.FindControl<ComboBox>("onExhaustedEdit");
                _onHeartbeatSelect = this.FindControl<ComboBox>("onHeartbeatSelect");
                _onUserDefinedSelect = this.FindControl<ComboBox>("onUserDefinedSelect");
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:68-85
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
            if (_infiniteRespawnCheckbox != null)
            {
                _infiniteRespawnCheckbox.IsCheckedChanged += (s, e) => SetInfiniteRespawn();
            }
            if (_spawnSelect != null)
            {
                _spawnSelect.SelectionChanged += (s, e) => SetContinuous();
            }
            if (_addCreatureButton != null)
            {
                _addCreatureButton.Click += (s, e) => AddCreature();
            }
            if (_removeCreatureButton != null)
            {
                _removeCreatureButton.Click += (s, e) => RemoveSelectedCreature();
            }
            if (_nameEditBtn != null)
            {
                _nameEditBtn.Click += (s, e) => ChangeName();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:87-131
        // Original: def _setup_installation(self, installation):
        private void SetupInstallation(HTInstallation installation)
        {
            _installation = installation;
            if (_nameEdit != null)
            {
                _nameEdit.SetInstallation(installation);
            }

            // Matching PyKotor implementation: difficulties: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_ENC_DIFFICULTIES)
            // TODO: Get TwoDA when available
            if (_difficultySelect != null)
            {
                // TODO: Get difficulties from installation when TwoDA is available
                // For now, just clear and set up empty
                _difficultySelect.Items.Clear();
            }

            // Matching PyKotor implementation: factions: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_FACTIONS)
            if (_factionSelect != null)
            {
                // TODO: Get factions from installation when TwoDA is available
                // For now, just clear and set up empty
                _factionSelect.Items.Clear();
            }

            // Matching PyKotor implementation: self._installation.setup_file_context_menu(...)
            // TODO: Setup file context menus when available

            // Matching PyKotor implementation: self.relevant_creature_resnames = sorted(...)
            if (installation != null && !string.IsNullOrEmpty(base._filepath))
            {
                // TODO: Get relevant creature resources when get_relevant_resources is available
                _relevantCreatureResnames = new List<string>();
            }
        }

        private void SetupProgrammaticUI()
        {
            var scrollViewer = new ScrollViewer();
            var tabControl = new TabControl();

            // Basic Tab
            var basicTab = new TabItem { Header = "Basic" };
            var basicPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Name
            var nameLabel = new TextBlock { Text = "Name:" };
            try
            {
                _nameEdit = new LocalizedStringEdit();
                if (_installation != null)
                {
                    _nameEdit.SetInstallation(_installation);
                }
                basicPanel.Children.Add(_nameEdit);
            }
            catch
            {
                // If LocalizedStringEdit fails to initialize, use a simple TextBox
                _nameEdit = null;
                var nameTextBox = new TextBox();
                basicPanel.Children.Add(nameTextBox);
            }
            _nameEditBtn = new Button { Content = "Edit Name" };
            _nameEditBtn.Click += (s, e) => ChangeName();
            basicPanel.Children.Add(nameLabel);
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

            // Difficulty
            var difficultyLabel = new TextBlock { Text = "Difficulty:" };
            _difficultySelect = new ComboBox2DA();
            basicPanel.Children.Add(difficultyLabel);
            basicPanel.Children.Add(_difficultySelect);

            // Spawn Option
            var spawnLabel = new TextBlock { Text = "Spawn Option:" };
            _spawnSelect = new ComboBox();
            _spawnSelect.Items.Add("Single Shot");
            _spawnSelect.Items.Add("Continuous");
            _spawnSelect.SelectionChanged += (s, e) => SetContinuous();
            basicPanel.Children.Add(spawnLabel);
            basicPanel.Children.Add(_spawnSelect);

            // Min/Max Creatures
            var minCreatureLabel = new TextBlock { Text = "Min Creatures:" };
            _minCreatureSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue };
            var maxCreatureLabel = new TextBlock { Text = "Max Creatures:" };
            _maxCreatureSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue };
            basicPanel.Children.Add(minCreatureLabel);
            basicPanel.Children.Add(_minCreatureSpin);
            basicPanel.Children.Add(maxCreatureLabel);
            basicPanel.Children.Add(_maxCreatureSpin);

            basicTab.Content = basicPanel;
            tabControl.Items.Add(basicTab);

            // Advanced Tab
            var advancedTab = new TabItem { Header = "Advanced" };
            var advancedPanel = new StackPanel { Orientation = Orientation.Vertical };

            _activeCheckbox = new CheckBox { Content = "Active" };
            _playerOnlyCheckbox = new CheckBox { Content = "Player Triggered Only" };

            var factionLabel = new TextBlock { Text = "Faction:" };
            _factionSelect = new ComboBox2DA();

            _respawnsCheckbox = new CheckBox { Content = "Respawns" };
            _infiniteRespawnCheckbox = new CheckBox { Content = "Infinite Respawns" };
            _infiniteRespawnCheckbox.IsCheckedChanged += (s, e) => SetInfiniteRespawn();

            var respawnTimeLabel = new TextBlock { Text = "Respawn Time (s):" };
            _respawnTimeSpin = new NumericUpDown { Minimum = int.MinValue, Maximum = int.MaxValue };
            var respawnCountLabel = new TextBlock { Text = "Number of Respawns:" };
            _respawnCountSpin = new NumericUpDown { Minimum = 0, Maximum = 99999 };

            advancedPanel.Children.Add(_activeCheckbox);
            advancedPanel.Children.Add(_playerOnlyCheckbox);
            advancedPanel.Children.Add(factionLabel);
            advancedPanel.Children.Add(_factionSelect);
            advancedPanel.Children.Add(_respawnsCheckbox);
            advancedPanel.Children.Add(_infiniteRespawnCheckbox);
            advancedPanel.Children.Add(respawnTimeLabel);
            advancedPanel.Children.Add(_respawnTimeSpin);
            advancedPanel.Children.Add(respawnCountLabel);
            advancedPanel.Children.Add(_respawnCountSpin);

            advancedTab.Content = advancedPanel;
            tabControl.Items.Add(advancedTab);

            // Creatures Tab
            var creaturesTab = new TabItem { Header = "Creatures" };
            var creaturesPanel = new StackPanel { Orientation = Orientation.Vertical };

            _creatureTable = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = false,
                GridLinesVisibility = DataGridGridLinesVisibility.All,
                SelectionMode = DataGridSelectionMode.Single
            };

            // Add columns
            _creatureTable.Columns.Add(new DataGridCheckBoxColumn { Header = "SingleSpawn", Binding = new Avalonia.Data.Binding("SingleSpawn") });
            _creatureTable.Columns.Add(new DataGridTextColumn { Header = "CR", Binding = new Avalonia.Data.Binding("CR") });
            _creatureTable.Columns.Add(new DataGridTextColumn { Header = "Appearance", Binding = new Avalonia.Data.Binding("Appearance") });
            _creatureTable.Columns.Add(new DataGridTextColumn { Header = "ResRef", Binding = new Avalonia.Data.Binding("ResRef") });

            var creatureButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal };
            _removeCreatureButton = new Button { Content = "Remove" };
            _removeCreatureButton.Click += (s, e) => RemoveSelectedCreature();
            _addCreatureButton = new Button { Content = "Add" };
            _addCreatureButton.Click += (s, e) => AddCreature();

            creatureButtonsPanel.Children.Add(_removeCreatureButton);
            creatureButtonsPanel.Children.Add(_addCreatureButton);

            creaturesPanel.Children.Add(_creatureTable);
            creaturesPanel.Children.Add(creatureButtonsPanel);

            creaturesTab.Content = creaturesPanel;
            tabControl.Items.Add(creaturesTab);

            // Scripts Tab
            var scriptsTab = new TabItem { Header = "Scripts" };
            var scriptsPanel = new StackPanel { Orientation = Orientation.Vertical };

            var onEnterLabel = new TextBlock { Text = "OnEnter:" };
            _onEnterSelect = new ComboBox();
            var onExitLabel = new TextBlock { Text = "OnExit:" };
            _onExitSelect = new ComboBox();
            var onExhaustedLabel = new TextBlock { Text = "OnExhausted:" };
            _onExhaustedEdit = new ComboBox();
            var onHeartbeatLabel = new TextBlock { Text = "OnHeartbeat:" };
            _onHeartbeatSelect = new ComboBox();
            var onUserDefinedLabel = new TextBlock { Text = "OnUserDefined:" };
            _onUserDefinedSelect = new ComboBox();

            scriptsPanel.Children.Add(onEnterLabel);
            scriptsPanel.Children.Add(_onEnterSelect);
            scriptsPanel.Children.Add(onExitLabel);
            scriptsPanel.Children.Add(_onExitSelect);
            scriptsPanel.Children.Add(onExhaustedLabel);
            scriptsPanel.Children.Add(_onExhaustedEdit);
            scriptsPanel.Children.Add(onHeartbeatLabel);
            scriptsPanel.Children.Add(_onHeartbeatSelect);
            scriptsPanel.Children.Add(onUserDefinedLabel);
            scriptsPanel.Children.Add(_onUserDefinedSelect);

            scriptsTab.Content = scriptsPanel;
            tabControl.Items.Add(scriptsTab);

            // Comments Tab
            var commentsTab = new TabItem { Header = "Comments" };
            var commentsPanel = new StackPanel { Orientation = Orientation.Vertical };
            var commentsLabel = new TextBlock { Text = "Comment:" };
            _commentsEdit = new TextBox { AcceptsReturn = true, AcceptsTab = true };
            commentsPanel.Children.Add(commentsLabel);
            commentsPanel.Children.Add(_commentsEdit);
            commentsTab.Content = commentsPanel;
            tabControl.Items.Add(commentsTab);

            scrollViewer.Content = tabControl;
            Content = scrollViewer;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:133-143
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            // Matching PyKotor implementation: ute: UTE = read_ute(data); self._loadUTE(ute)
            var gff = GFF.FromBytes(data);
            _ute = UTEHelpers.ConstructUte(gff);
            LoadUTE(_ute);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:145-217
        // Original: def _loadUTE(self, ute: UTE):
        private void LoadUTE(UTE ute)
        {
            // Matching PyKotor implementation: self._ute = ute
            _ute = ute;

            // Basic
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:163-170
            if (_nameEdit != null)
            {
                _nameEdit.SetLocString(ute.Name);
            }
            if (_tagEdit != null)
            {
                _tagEdit.Text = ute.Tag;
            }
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = ute.ResRef.ToString();
            }
            if (_difficultySelect != null)
            {
                _difficultySelect.SetSelectedIndex(ute.DifficultyId);
            }
            if (_spawnSelect != null)
            {
                // Matching PyKotor implementation: self.ui.spawnSelect.setCurrentIndex(int(ute.single_shot))
                _spawnSelect.SelectedIndex = ute.SingleShot ? 1 : 0;
            }
            if (_minCreatureSpin != null)
            {
                _minCreatureSpin.Value = ute.RecCreatures;
            }
            if (_maxCreatureSpin != null)
            {
                _maxCreatureSpin.Value = ute.MaxCreatures;
            }

            // Advanced
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:172-179
            if (_activeCheckbox != null)
            {
                _activeCheckbox.IsChecked = ute.Active;
            }
            if (_playerOnlyCheckbox != null)
            {
                _playerOnlyCheckbox.IsChecked = ute.PlayerOnly != 0;
            }
            if (_factionSelect != null)
            {
                _factionSelect.SetSelectedIndex(ute.FactionId);
            }
            if (_respawnsCheckbox != null)
            {
                _respawnsCheckbox.IsChecked = ute.Reset != 0;
            }
            if (_infiniteRespawnCheckbox != null)
            {
                // Matching PyKotor implementation: self.ui.infiniteRespawnCheckbox.setChecked(ute.respawns == -1)
                _infiniteRespawnCheckbox.IsChecked = ute.Respawns == -1;
            }
            if (_respawnTimeSpin != null)
            {
                _respawnTimeSpin.Value = ute.ResetTime;
            }
            if (_respawnCountSpin != null)
            {
                _respawnCountSpin.Value = ute.Respawns;
            }

            // Creatures
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:181-190
            if (_creatureTable != null)
            {
                var creatureList = new List<object>();
                foreach (var creature in ute.Creatures)
                {
                    creatureList.Add(new
                    {
                        SingleSpawn = creature.SingleSpawnBool,
                        CR = creature.ChallengeRating,
                        Appearance = creature.AppearanceId,
                        ResRef = creature.ResRef.ToString()
                    });
                }
                _creatureTable.ItemsSource = creatureList;
            }

            // Scripts
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:192-197
            if (_onEnterSelect != null)
            {
                _onEnterSelect.Text = ute.OnEntered.ToString();
            }
            if (_onExitSelect != null)
            {
                _onExitSelect.Text = ute.OnExit.ToString();
            }
            if (_onExhaustedEdit != null)
            {
                _onExhaustedEdit.Text = ute.OnExhausted.ToString();
            }
            if (_onHeartbeatSelect != null)
            {
                _onHeartbeatSelect.Text = ute.OnHeartbeat.ToString();
            }
            if (_onUserDefinedSelect != null)
            {
                _onUserDefinedSelect.Text = ute.OnUserDefined.ToString();
            }

            // Populate script combo boxes
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:199-214
            if (_installation != null && !string.IsNullOrEmpty(base._filepath))
            {
                // TODO: Get relevant script resources when get_relevant_resources is available
                _relevantScriptResnames = new List<string>();
                if (_onEnterSelect != null)
                {
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onEnterSelect.Items.Add(resname);
                    }
                }
                if (_onExitSelect != null)
                {
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onExitSelect.Items.Add(resname);
                    }
                }
                if (_onExhaustedEdit != null)
                {
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onExhaustedEdit.Items.Add(resname);
                    }
                }
                if (_onHeartbeatSelect != null)
                {
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onHeartbeatSelect.Items.Add(resname);
                    }
                }
                if (_onUserDefinedSelect != null)
                {
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onUserDefinedSelect.Items.Add(resname);
                    }
                }
            }

            // Comments
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:217
            if (_commentsEdit != null)
            {
                _commentsEdit.Text = ute.Comment;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:219-285
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation: ute: UTE = deepcopy(self._ute)
            var ute = CopyUTE(_ute);

            // Basic - read from UI controls (matching Python which always reads from UI)
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:236-243
            ute.Name = _nameEdit?.GetLocString() ?? ute.Name ?? LocalizedString.FromInvalid();
            ute.Tag = _tagEdit?.Text ?? ute.Tag ?? "";
            ute.ResRef = _resrefEdit != null && !string.IsNullOrEmpty(_resrefEdit.Text)
                ? new ResRef(_resrefEdit.Text)
                : ute.ResRef;
            ute.DifficultyId = _difficultySelect?.SelectedIndex ?? ute.DifficultyId;
            // Matching PyKotor implementation: ute.single_shot = bool(self.ui.spawnSelect.currentIndex())
            ute.SingleShot = _spawnSelect?.SelectedIndex == 1;
            ute.RecCreatures = _minCreatureSpin?.Value != null ? (int)_minCreatureSpin.Value : ute.RecCreatures;
            ute.MaxCreatures = _maxCreatureSpin?.Value != null ? (int)_maxCreatureSpin.Value : ute.MaxCreatures;

            // Advanced
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:245-251
            ute.Active = _activeCheckbox?.IsChecked ?? ute.Active;
            ute.PlayerOnly = (_playerOnlyCheckbox?.IsChecked ?? (ute.PlayerOnly != 0)) ? 1 : 0;
            ute.FactionId = _factionSelect?.SelectedIndex ?? ute.FactionId;
            ute.Reset = (_respawnsCheckbox?.IsChecked ?? (ute.Reset != 0)) ? 1 : 0;
            ute.Respawns = _respawnCountSpin?.Value != null ? (int)_respawnCountSpin.Value : ute.Respawns;
            ute.ResetTime = _respawnTimeSpin?.Value != null ? (int)_respawnTimeSpin.Value : ute.ResetTime;

            // Creatures
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:253-269
            ute.Creatures.Clear();
            if (_creatureTable?.ItemsSource != null)
            {
                foreach (var item in (System.Collections.IEnumerable)_creatureTable.ItemsSource)
                {
                    // Extract creature data from DataGrid row
                    // Use reflection to get properties from anonymous type
                    var creature = new UTECreature();
                    var itemType = item.GetType();
                    var resRefProp = itemType.GetProperty("ResRef");
                    var appearanceProp = itemType.GetProperty("Appearance");
                    var crProp = itemType.GetProperty("CR");
                    var singleSpawnProp = itemType.GetProperty("SingleSpawn");
                    
                    if (resRefProp != null)
                    {
                        var resRefValue = resRefProp.GetValue(item);
                        if (resRefValue != null)
                        {
                            creature.ResRef = new ResRef(resRefValue.ToString());
                        }
                    }
                    if (appearanceProp != null)
                    {
                        var appearanceValue = appearanceProp.GetValue(item);
                        if (appearanceValue != null && int.TryParse(appearanceValue.ToString(), out int appearance))
                        {
                            creature.Appearance = appearance;
                        }
                    }
                    if (crProp != null)
                    {
                        var crValue = crProp.GetValue(item);
                        if (crValue != null)
                        {
                            if (float.TryParse(crValue.ToString(), out float cr))
                            {
                                creature.CR = (int)cr;
                            }
                        }
                    }
                    if (singleSpawnProp != null)
                    {
                        var singleSpawnValue = singleSpawnProp.GetValue(item);
                        if (singleSpawnValue != null && bool.TryParse(singleSpawnValue.ToString(), out bool singleSpawn))
                        {
                            creature.SingleSpawn = singleSpawn ? 1 : 0;
                        }
                    }
                    
                    ute.Creatures.Add(creature);
                }
            }
            // If table is empty or not set up, preserve existing creatures from _ute
            if (ute.Creatures.Count == 0)
            {
                foreach (var creature in _ute.Creatures)
                {
                    ute.Creatures.Add(new UTECreature
                    {
                        ResRef = creature.ResRef,
                        Appearance = creature.Appearance,
                        SingleSpawn = creature.SingleSpawn,
                        CR = creature.CR,
                        GuaranteedCount = creature.GuaranteedCount
                    });
                }
            }

            // Scripts
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:271-276
            ute.OnEntered = _onEnterSelect != null && !string.IsNullOrEmpty(_onEnterSelect.Text)
                ? new ResRef(_onEnterSelect.Text)
                : ute.OnEntered;
            ute.OnExit = _onExitSelect != null && !string.IsNullOrEmpty(_onExitSelect.Text)
                ? new ResRef(_onExitSelect.Text)
                : ute.OnExit;
            ute.OnExhausted = _onExhaustedEdit != null && !string.IsNullOrEmpty(_onExhaustedEdit.Text)
                ? new ResRef(_onExhaustedEdit.Text)
                : ute.OnExhausted;
            ute.OnHeartbeat = _onHeartbeatSelect != null && !string.IsNullOrEmpty(_onHeartbeatSelect.Text)
                ? new ResRef(_onHeartbeatSelect.Text)
                : ute.OnHeartbeat;
            ute.OnUserDefined = _onUserDefinedSelect != null && !string.IsNullOrEmpty(_onUserDefinedSelect.Text)
                ? new ResRef(_onUserDefinedSelect.Text)
                : ute.OnUserDefined;

            // Comments
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:279
            ute.Comment = _commentsEdit?.Text ?? ute.Comment ?? "";

            // Build GFF
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:281-285
            var game = _installation?.Game ?? Game.K2;
            var gff = UTEHelpers.DismantleUte(ute, game);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.UTE);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation: Deep copy helper
        // Original: ute: UTE = deepcopy(self._ute)
        private UTE CopyUTE(UTE source)
        {
            // Deep copy LocalizedString objects (they're reference types)
            LocalizedString copyName = source.Name != null
                ? new LocalizedString(source.Name.StringRef, new Dictionary<int, string>(GetSubstringsDict(source.Name)))
                : null;

            var copy = new UTE
            {
                ResRef = source.ResRef,
                Tag = source.Tag,
                Comment = source.Comment,
                Active = source.Active,
                DifficultyId = source.DifficultyId,
                DifficultyIndex = source.DifficultyIndex,
                Faction = source.Faction,
                MaxCreatures = source.MaxCreatures,
                RecCreatures = source.RecCreatures,
                Respawn = source.Respawn,
                RespawnTime = source.RespawnTime,
                Reset = source.Reset,
                ResetTime = source.ResetTime,
                PlayerOnly = source.PlayerOnly,
                SingleSpawn = source.SingleSpawn,
                OnEnteredScript = source.OnEnteredScript,
                OnExitScript = source.OnExitScript,
                OnExhaustedScript = source.OnExhaustedScript,
                OnHeartbeatScript = source.OnHeartbeatScript,
                OnUserDefinedScript = source.OnUserDefinedScript,
                Name = copyName,
                PaletteId = source.PaletteId
            };

            // Copy creatures
            foreach (var creature in source.Creatures)
            {
                copy.Creatures.Add(new UTECreature
                {
                    ResRef = creature.ResRef,
                    Appearance = creature.Appearance,
                    SingleSpawn = creature.SingleSpawn,
                    CR = creature.CR,
                    GuaranteedCount = creature.GuaranteedCount
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:287-289
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _ute = new UTE();
            LoadUTE(_ute);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:291-294
        // Original: def change_name(self):
        private void ChangeName()
        {
            if (_installation == null) return;
            var dialog = new LocalizedStringDialog(this, _installation, _ute.Name);
            if (dialog.ShowDialog())
            {
                _ute.Name = dialog.LocString;
                if (_nameEdit != null)
                {
                    _nameEdit.SetLocString(_ute.Name);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:296-299
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:301-305
        // Original: def generate_resref(self):
        private void GenerateResref()
        {
            if (_resrefEdit != null)
            {
                _resrefEdit.Text = !string.IsNullOrEmpty(base._resname) ? base._resname : "m00xx_enc_000";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:307-321
        // Original: def set_infinite_respawn(self):
        private void SetInfiniteRespawn()
        {
            if (_infiniteRespawnCheckbox?.IsChecked == true)
            {
                SetInfiniteRespawnMain(val: -1, enabled: false);
            }
            else
            {
                SetInfiniteRespawnMain(val: 0, enabled: true);
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:313-321
        // Original: def _set_infinite_respawn_main(self, val: int, *, enabled: bool):
        private void SetInfiniteRespawnMain(int val, bool enabled)
        {
            if (_respawnCountSpin != null)
            {
                _respawnCountSpin.Minimum = val;
                _respawnCountSpin.Value = val;
                _respawnCountSpin.IsEnabled = enabled;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:323-328
        // Original: def set_continuous(self, *args, **kwargs):
        private void SetContinuous()
        {
            bool isContinuous = _spawnSelect?.SelectedIndex == 1;
            if (_respawnsCheckbox != null)
            {
                _respawnsCheckbox.IsEnabled = isContinuous;
            }
            if (_infiniteRespawnCheckbox != null)
            {
                _infiniteRespawnCheckbox.IsEnabled = isContinuous;
            }
            if (_respawnCountSpin != null)
            {
                _respawnCountSpin.IsEnabled = isContinuous;
            }
            if (_respawnTimeSpin != null)
            {
                _respawnTimeSpin.IsEnabled = isContinuous;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:330-376
        // Original: def add_creature(self, *args, resname: str = "", appearance_id: int = 0, challenge: float = 0.0, single: bool = False):
        private void AddCreature(string resname = "", int appearanceId = 0, float challenge = 0.0f, bool single = false)
        {
            if (_creatureTable == null) return;

            // Create a simple object to represent the creature row
            var creatureRow = new
            {
                SingleSpawn = single,
                CR = challenge,
                Appearance = appearanceId,
                ResRef = resname
            };

            // Add to ItemsSource
            var currentList = _creatureTable.ItemsSource as System.Collections.IList;
            if (currentList == null)
            {
                var newList = new List<object>();
                if (_creatureTable.ItemsSource != null)
                {
                    foreach (var item in (System.Collections.IEnumerable)_creatureTable.ItemsSource)
                    {
                        newList.Add(item);
                    }
                }
                newList.Add(creatureRow);
                _creatureTable.ItemsSource = newList;
            }
            else
            {
                currentList.Add(creatureRow);
            }
            
            // Note: Creatures are stored in DataGrid ItemsSource, Build() will read from there
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:378-392
        // Original: def remove_selected_creature(self):
        private void RemoveSelectedCreature()
        {
            if (_creatureTable == null) return;

            // Try to get selected item
            var selectedItem = _creatureTable.SelectedItem;
            if (selectedItem != null)
            {
                var currentList = _creatureTable.ItemsSource as System.Collections.IList;
                if (currentList != null)
                {
                    currentList.Remove(selectedItem);
                }
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
