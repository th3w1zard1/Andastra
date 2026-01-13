using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Extract;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using HolocronToolset.Widgets;
using HolocronToolset.Widgets.Edit;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using GFFAuto = BioWare.NET.Resource.Formats.GFF.GFFAuto;
using Window = Avalonia.Controls.Window;
using UTE = BioWare.NET.Resource.Formats.GFF.Generics.UTE;
using Avalonia;
using TextBlock = Avalonia.Controls.TextBlock;
using LocalizedString = BioWare.NET.Common.LocalizedString;
using ResourceType = BioWare.NET.Resource.ResourceType;
using Button = Avalonia.Controls.Button;
using ComboBox = Avalonia.Controls.ComboBox;
using NumericUpDown = Avalonia.Controls.NumericUpDown;
using CheckBox = Avalonia.Controls.CheckBox;
using DataGrid = Avalonia.Controls.DataGrid;
using TabControl = Avalonia.Controls.TabControl;
using TabItem = Avalonia.Controls.TabItem;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:26
    // Original: class UTEEditor(Editor):
    public class UTEEditor : Editor
    {
        // Data model for creature table rows
        // Matching PyKotor implementation: creature table uses widgets in cells
        // C# uses DataGrid with bindings, so we need a proper data model
        private class CreatureRow
        {
            public bool SingleSpawn { get; set; }
            public float CR { get; set; }
            public int Appearance { get; set; }
            public string ResRef { get; set; }
        }

        private UTE _ute;
        private List<string> _relevantCreatureResnames;
        private List<string> _relevantScriptResnames;
        private ObservableCollection<CreatureRow> _creatureRows;

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
            _creatureRows = new ObservableCollection<CreatureRow>();

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
            TwoDA difficulties = installation.HtGetCache2DA(HTInstallation.TwoDAEncDifficulties);
            if (_difficultySelect != null)
            {
                _difficultySelect.Items.Clear();
                if (difficulties != null)
                {
                    List<string> difficultyLabels = difficulties.GetColumn("label");
                    _difficultySelect.SetItems(difficultyLabels, sortAlphabetically: false);
                    _difficultySelect.SetContext(difficulties, installation, HTInstallation.TwoDAEncDifficulties);
                }
            }

            // Matching PyKotor implementation: factions: TwoDA | None = installation.ht_get_cache_2da(HTInstallation.TwoDA_FACTIONS)
            TwoDA factions = installation.HtGetCache2DA(HTInstallation.TwoDAFactions);
            if (_factionSelect != null)
            {
                _factionSelect.Items.Clear();
                if (factions != null)
                {
                    List<string> factionLabels = factions.GetColumn("label");
                    _factionSelect.SetItems(factionLabels, sortAlphabetically: false);
                    _factionSelect.SetContext(factions, installation, HTInstallation.TwoDAFactions);
                }
            }

            // Matching PyKotor implementation: self._installation.setup_file_context_menu(...)
            SetupFileContextMenus();

            // Matching PyKotor implementation: self.relevant_creature_resnames = sorted(...)
            if (installation != null && !string.IsNullOrEmpty(base._filepath))
            {
                HashSet<FileResource> creatureResources = installation.GetRelevantResources(ResourceType.UTC, base._filepath);
                _relevantCreatureResnames = creatureResources
                    .Select(r => r.ResName.ToLowerInvariant())
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();
            }
            else
            {
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

            // Add columns with proper bindings
            _creatureTable.Columns.Add(new DataGridCheckBoxColumn { Header = "SingleSpawn", Binding = new Avalonia.Data.Binding("SingleSpawn") });
            _creatureTable.Columns.Add(new DataGridTextColumn { Header = "CR", Binding = new Avalonia.Data.Binding("CR") });
            _creatureTable.Columns.Add(new DataGridTextColumn { Header = "Appearance", Binding = new Avalonia.Data.Binding("Appearance") });
            _creatureTable.Columns.Add(new DataGridTextColumn { Header = "ResRef", Binding = new Avalonia.Data.Binding("ResRef") });

            // Set ItemsSource to ObservableCollection for proper binding
            _creatureTable.ItemsSource = _creatureRows;

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
                // Python: 0 = Single Shot, 1 = Continuous
                // C#: 0 = Single Shot, 1 = Continuous
                _spawnSelect.SelectedIndex = ute.SingleShot ? 1 : 0;
                // Ensure respawn fields are properly enabled/disabled based on spawn mode
                SetContinuous();
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
            if (_creatureTable != null && _creatureRows != null)
            {
                _creatureRows.Clear();
                foreach (var creature in ute.Creatures)
                {
                    _creatureRows.Add(new CreatureRow
                    {
                        SingleSpawn = creature.SingleSpawnBool,
                        CR = creature.ChallengeRating,
                        Appearance = creature.AppearanceId,
                        ResRef = creature.ResRef.ToString()
                    });
                }
            }

            // Scripts
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:192-214
            // First, get relevant script resources and populate combo boxes
            if (_installation != null && !string.IsNullOrEmpty(base._filepath))
            {
                HashSet<FileResource> scriptResources = _installation.GetRelevantResources(ResourceType.NCS, base._filepath);
                _relevantScriptResnames = scriptResources
                    .Select(r => r.ResName.ToLowerInvariant())
                    .Distinct()
                    .OrderBy(r => r)
                    .ToList();

                // Populate all script combo boxes with relevant script resources (matching Python populate_combo_box)
                if (_onEnterSelect != null)
                {
                    _onEnterSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onEnterSelect.Items.Add(resname);
                    }
                }
                if (_onExitSelect != null)
                {
                    _onExitSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onExitSelect.Items.Add(resname);
                    }
                }
                if (_onExhaustedEdit != null)
                {
                    _onExhaustedEdit.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onExhaustedEdit.Items.Add(resname);
                    }
                }
                if (_onHeartbeatSelect != null)
                {
                    _onHeartbeatSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onHeartbeatSelect.Items.Add(resname);
                    }
                }
                if (_onUserDefinedSelect != null)
                {
                    _onUserDefinedSelect.Items.Clear();
                    foreach (var resname in _relevantScriptResnames)
                    {
                        _onUserDefinedSelect.Items.Add(resname);
                    }
                }
            }

            // Then set the text values (matching Python set_combo_box_text)
            // This must be done after populating items to ensure the text is set correctly
            if (_onEnterSelect != null)
            {
                string onEnterText = ute.OnEntered.ToString();
                _onEnterSelect.Text = onEnterText;
                // Try to select the item if it exists in the list
                if (!string.IsNullOrEmpty(onEnterText))
                {
                    int index = _onEnterSelect.Items.IndexOf(onEnterText);
                    if (index >= 0)
                    {
                        _onEnterSelect.SelectedIndex = index;
                    }
                }
            }
            if (_onExitSelect != null)
            {
                string onExitText = ute.OnExit.ToString();
                _onExitSelect.Text = onExitText;
                if (!string.IsNullOrEmpty(onExitText))
                {
                    int index = _onExitSelect.Items.IndexOf(onExitText);
                    if (index >= 0)
                    {
                        _onExitSelect.SelectedIndex = index;
                    }
                }
            }
            if (_onExhaustedEdit != null)
            {
                string onExhaustedText = ute.OnExhausted.ToString();
                _onExhaustedEdit.Text = onExhaustedText;
                if (!string.IsNullOrEmpty(onExhaustedText))
                {
                    int index = _onExhaustedEdit.Items.IndexOf(onExhaustedText);
                    if (index >= 0)
                    {
                        _onExhaustedEdit.SelectedIndex = index;
                    }
                }
            }
            if (_onHeartbeatSelect != null)
            {
                string onHeartbeatText = ute.OnHeartbeat.ToString();
                _onHeartbeatSelect.Text = onHeartbeatText;
                if (!string.IsNullOrEmpty(onHeartbeatText))
                {
                    int index = _onHeartbeatSelect.Items.IndexOf(onHeartbeatText);
                    if (index >= 0)
                    {
                        _onHeartbeatSelect.SelectedIndex = index;
                    }
                }
            }
            if (_onUserDefinedSelect != null)
            {
                string onUserDefinedText = ute.OnUserDefined.ToString();
                _onUserDefinedSelect.Text = onUserDefinedText;
                if (!string.IsNullOrEmpty(onUserDefinedText))
                {
                    int index = _onUserDefinedSelect.Items.IndexOf(onUserDefinedText);
                    if (index >= 0)
                    {
                        _onUserDefinedSelect.SelectedIndex = index;
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
            if (_creatureRows != null)
            {
                foreach (var row in _creatureRows)
                {
                    var creature = new UTECreature();
                    creature.ResRef = !string.IsNullOrEmpty(row.ResRef) ? new ResRef(row.ResRef) : ResRef.FromBlank();
                    creature.Appearance = row.Appearance;
                    creature.CR = (int)row.CR;
                    creature.SingleSpawn = row.SingleSpawn ? 1 : 0;
                    ute.Creatures.Add(creature);
                }
            }
            // If table is empty or not set up, preserve existing creatures from _ute
            if (ute.Creatures.Count == 0 && _ute != null)
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
            LocalizedString currentName = _nameEdit?.GetLocString() ?? _ute?.Name ?? LocalizedString.FromInvalid();
            var dialog = new LocalizedStringDialog(this, _installation, currentName);
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
            if (_creatureRows == null)
            {
                _creatureRows = new ObservableCollection<CreatureRow>();
            }

            // Create a new creature row
            var creatureRow = new CreatureRow
            {
                SingleSpawn = single,
                CR = challenge,
                Appearance = appearanceId,
                ResRef = resname
            };

            // Add to ObservableCollection (DataGrid is bound to this)
            _creatureRows.Add(creatureRow);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ute.py:378-392
        // Original: def remove_selected_creature(self):
        private void RemoveSelectedCreature()
        {
            if (_creatureTable == null || _creatureRows == null) return;

            // Try to get selected item
            var selectedItem = _creatureTable.SelectedItem;
            if (selectedItem is CreatureRow creatureRow)
            {
                _creatureRows.Remove(creatureRow);
            }
            else if (selectedItem != null)
            {
                // Fallback: try to find by index
                int selectedIndex = _creatureTable.SelectedIndex;
                if (selectedIndex >= 0 && selectedIndex < _creatureRows.Count)
                {
                    _creatureRows.RemoveAt(selectedIndex);
                }
            }
        }

        public override void SaveAs()
        {
            Save();
        }

        // Setup context menus for file resource fields (scripts and creatures)
        // Based on PyKotor implementation: self._installation.setup_file_context_menu(...)
        // Provides right-click menus to open, create, or view referenced files
        private void SetupFileContextMenus()
        {
            if (_installation == null)
            {
                return;
            }

            // Setup context menus for script ComboBoxes (NSS/NCS files)
            SetupScriptComboBoxContextMenu(_onEnterSelect, "OnEnter Script");
            SetupScriptComboBoxContextMenu(_onExitSelect, "OnExit Script");
            SetupScriptComboBoxContextMenu(_onExhaustedEdit, "OnExhausted Script");
            SetupScriptComboBoxContextMenu(_onHeartbeatSelect, "OnHeartbeat Script");
            SetupScriptComboBoxContextMenu(_onUserDefinedSelect, "OnUserDefined Script");

            // Setup context menu for creature table (UTP files)
            SetupCreatureTableContextMenu();
        }

        // Create context menu for script ComboBox controls
        // Allows opening existing scripts in editor, creating new scripts, or viewing resource details
        private void SetupScriptComboBoxContextMenu(ComboBox comboBox, string scriptTypeName)
        {
            if (comboBox == null)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItems = new List<MenuItem>();

            // "Open in Editor" menu item - opens the script if it exists
            var openInEditorItem = new MenuItem
            {
                Header = "Open in Editor",
                IsEnabled = false
            };
            openInEditorItem.Click += (sender, e) => OpenScriptInEditor(comboBox, scriptTypeName);
            menuItems.Add(openInEditorItem);

            // Enable/disable based on whether script name is set
            // Note: ComboBox in Avalonia doesn't have TextChanged, use SelectionChanged or TextBox instead
            // For ComboBox, we'll use SelectionChanged and also check Text property if available
            comboBox.SelectionChanged += (sender, e) =>
            {
                string text = comboBox.SelectedItem?.ToString() ?? comboBox.Text ?? string.Empty;
                openInEditorItem.IsEnabled = !string.IsNullOrWhiteSpace(text);
            };

            // "Create New Script" menu item - creates a new NSS file
            var createNewItem = new MenuItem
            {
                Header = "Create New Script"
            };
            createNewItem.Click += (sender, e) => CreateNewScript(comboBox, scriptTypeName);
            menuItems.Add(createNewItem);

            // "View Resource Location" menu item - shows where the script is located
            var viewLocationItem = new MenuItem
            {
                Header = "View Resource Location",
                IsEnabled = false
            };
            viewLocationItem.Click += (sender, e) => ViewScriptResourceLocation(comboBox, scriptTypeName);
            menuItems.Add(viewLocationItem);

            // Enable/disable based on whether script name is set
            // Note: ComboBox in Avalonia doesn't have TextChanged, use SelectionChanged or TextBox instead
            comboBox.SelectionChanged += (sender, e) =>
            {
                string text = comboBox.SelectedItem?.ToString() ?? comboBox.Text ?? string.Empty;
                viewLocationItem.IsEnabled = !string.IsNullOrWhiteSpace(text);
            };

            // AddRange doesn't exist in Avalonia ItemCollection, use a loop instead
            foreach (var item in menuItems)
            {
                contextMenu.Items.Add(item);
            }
            // Add separator after first menu items (Separator is not a MenuItem, so add directly to Items collection)
            contextMenu.Items.Insert(menuItems.Count - 1, new Separator());
            
            comboBox.ContextMenu = contextMenu;
        }

        // Open the script referenced in the ComboBox in an appropriate editor
        private void OpenScriptInEditor(ComboBox comboBox, string scriptTypeName)
        {
            if (comboBox == null || _installation == null)
            {
                return;
            }

            string scriptName = comboBox.Text?.Trim();
            if (string.IsNullOrEmpty(scriptName))
            {
                return;
            }

            try
            {
                // Try to find the script resource (NSS source preferred, fallback to NCS)
                var resourceResult = _installation.Resource(scriptName, ResourceType.NSS, null);
                ResourceType resourceType = ResourceType.NSS;
                
                if (resourceResult == null)
                {
                    // Try compiled version
                    resourceResult = _installation.Resource(scriptName, ResourceType.NCS, null);
                    resourceType = ResourceType.NCS;
                }

                if (resourceResult == null)
                {
                    // Resource not found - show message or create new
                    System.Console.WriteLine($"Script '{scriptName}' not found in installation.");
                    // Optionally create new script here
                    return;
                }

                // Open the script in the NSS editor
                var fileResource = new FileResource(
                    scriptName,
                    resourceType,
                    resourceResult.Data.Length,
                    0,
                    resourceResult.FilePath
                );

                HolocronToolset.Editors.WindowUtils.OpenResourceEditor(fileResource, _installation, this);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error opening script '{scriptName}': {ex.Message}");
            }
        }

        // Create a new script file and open it in the editor
        private void CreateNewScript(ComboBox comboBox, string scriptTypeName)
        {
            if (_installation == null)
            {
                return;
            }

            try
            {
                // Generate a default script name if not already set
                string scriptName = comboBox.Text?.Trim();
                if (string.IsNullOrEmpty(scriptName))
                {
                    // Generate based on encounter resref and script type
                    string baseName = !string.IsNullOrEmpty(_resrefEdit?.Text) 
                        ? _resrefEdit.Text 
                        : "m00xx_enc_000";
                    scriptName = $"{baseName}_{scriptTypeName.ToLowerInvariant().Replace(" ", "_")}";
                }

                // Limit to 16 characters (ResRef max length)
                if (scriptName.Length > 16)
                {
                    scriptName = scriptName.Substring(0, 16);
                }

                // Create a new NSS editor with empty content
                var nssEditor = new NSSEditor(this, _installation);
                nssEditor.New();
                
                // Show the editor - user will set the resref when saving
                HolocronToolset.Editors.WindowUtils.AddWindow(nssEditor, show: true);

                // Update the combo box with the suggested script name
                // User can change this before saving
                comboBox.Text = scriptName;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error creating new script: {ex.Message}");
            }
        }

        // View the location/details of the script resource
        private void ViewScriptResourceLocation(ComboBox comboBox, string scriptTypeName)
        {
            if (comboBox == null || _installation == null)
            {
                return;
            }

            string scriptName = comboBox.Text?.Trim();
            if (string.IsNullOrEmpty(scriptName))
            {
                return;
            }

            try
            {
                // Find the script resource
                var locations = _installation.Locations(
                    new List<ResourceIdentifier> { new ResourceIdentifier(scriptName, ResourceType.NSS) },
                    null,
                    null);

                var nssIdentifier = new ResourceIdentifier(scriptName, ResourceType.NSS);
                if (locations.Count > 0 && locations.ContainsKey(nssIdentifier) &&
                    locations[nssIdentifier].Count > 0)
                {
                    var foundLocations = locations[nssIdentifier];
                    // Show dialog with all found locations
                    var dialog = new ResourceLocationDialog(
                        this,
                        scriptName,
                        ResourceType.NSS,
                        foundLocations,
                        _installation);
                    dialog.ShowDialog(this);
                }
                else
                {
                    // Try compiled version
                    locations = _installation.Locations(
                        new List<ResourceIdentifier> { new ResourceIdentifier(scriptName, ResourceType.NCS) },
                        null,
                        null);

                    var ncsIdentifier = new ResourceIdentifier(scriptName, ResourceType.NCS);
                    if (locations.Count > 0 && locations.ContainsKey(ncsIdentifier) &&
                        locations[ncsIdentifier].Count > 0)
                    {
                        var foundLocations = locations[ncsIdentifier];
                        // Show dialog with all found locations
                        var dialog = new ResourceLocationDialog(
                            this,
                            scriptName,
                            ResourceType.NCS,
                            foundLocations,
                            _installation);
                        dialog.ShowDialog(this);
                    }
                    else
                    {
                        // Show "not found" message
                        var msgBox = MessageBoxManager.GetMessageBoxStandard(
                            "Resource Not Found",
                            $"Script '{scriptName}' not found in installation.\n\nSearched for:\n- {scriptName}.nss\n- {scriptName}.ncs",
                            ButtonEnum.Ok,
                            MsBox.Avalonia.Enums.Icon.Info);
                        msgBox.ShowAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error viewing script location '{scriptName}': {ex.Message}");
            }
        }

        // Setup context menu for creature table (UTP files)
        private void SetupCreatureTableContextMenu()
        {
            if (_creatureTable == null)
            {
                return;
            }

            var contextMenu = new ContextMenu();
            var menuItems = new List<MenuItem>();

            // "Open Creature in Editor" menu item
            var openCreatureItem = new MenuItem
            {
                Header = "Open Creature in Editor",
                IsEnabled = false
            };
            openCreatureItem.Click += (sender, e) => OpenCreatureInEditor();
            menuItems.Add(openCreatureItem);

            // "Create New Creature" menu item
            var createNewCreatureItem = new MenuItem
            {
                Header = "Create New Creature"
            };
            createNewCreatureItem.Click += (sender, e) => CreateNewCreature();
            menuItems.Add(createNewCreatureItem);

            // "View Creature Resource Location" menu item
            var viewCreatureLocationItem = new MenuItem
            {
                Header = "View Creature Resource Location",
                IsEnabled = false
            };
            viewCreatureLocationItem.Click += (sender, e) => ViewCreatureResourceLocation();
            menuItems.Add(viewCreatureLocationItem);

            // AddRange doesn't exist in Avalonia ItemCollection, use a loop instead
            foreach (var item in menuItems)
            {
                contextMenu.Items.Add(item);
            }
            // Add separator after first menu items (Separator is not a MenuItem, so add directly to Items collection)
            contextMenu.Items.Insert(menuItems.Count - 1, new Separator());
            _creatureTable.ContextMenu = contextMenu;

            // Update menu enabled state when selection changes
            _creatureTable.SelectionChanged += (sender, e) =>
            {
                bool hasSelection = _creatureTable.SelectedItem != null;
                openCreatureItem.IsEnabled = hasSelection;
                viewCreatureLocationItem.IsEnabled = hasSelection;
            };
        }

        // Open the selected creature in the UTP editor
        private void OpenCreatureInEditor()
        {
            if (_creatureTable?.SelectedItem == null || _installation == null)
            {
                return;
            }

            try
            {
                // Extract ResRef from selected row
                var selectedItem = _creatureTable.SelectedItem;
                var itemType = selectedItem.GetType();
                var resRefProp = itemType.GetProperty("ResRef");
                
                if (resRefProp == null)
                {
                    return;
                }

                var resRefValue = resRefProp.GetValue(selectedItem);
                if (resRefValue == null || string.IsNullOrEmpty(resRefValue.ToString()))
                {
                    return;
                }

                string creatureResRef = resRefValue.ToString().Trim();

                // Find the creature resource (UTP)
                var resourceResult = _installation.Resource(creatureResRef, ResourceType.UTP, null);
                
                if (resourceResult == null)
                {
                    System.Console.WriteLine($"Creature '{creatureResRef}' not found in installation.");
                    return;
                }

                // Open the creature in the UTP editor
                var fileResource = new FileResource(
                    creatureResRef,
                    ResourceType.UTP,
                    resourceResult.Data.Length,
                    0,
                    resourceResult.FilePath
                );

                HolocronToolset.Editors.WindowUtils.OpenResourceEditor(fileResource, _installation, this);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error opening creature: {ex.Message}");
            }
        }

        // Create a new creature file and open it in the editor
        private void CreateNewCreature()
        {
            if (_installation == null)
            {
                return;
            }

            try
            {
                // Generate a default creature name based on encounter resref
                string baseName = !string.IsNullOrEmpty(_resrefEdit?.Text) 
                    ? _resrefEdit.Text 
                    : "m00xx_enc_000";
                string creatureName = $"{baseName}_cre_000";

                // Limit to 16 characters (ResRef max length)
                if (creatureName.Length > 16)
                {
                    creatureName = creatureName.Substring(0, 16);
                }

                // Create a new UTP editor
                var utpEditor = new UTPEditor(this, _installation);
                utpEditor.New();

                // Show the editor
                HolocronToolset.Editors.WindowUtils.AddWindow(utpEditor, show: true);

                // Optionally add the new creature to the encounter table
                // User can manually add it via the Add button after creating
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error creating new creature: {ex.Message}");
            }
        }

        // View the location/details of the selected creature resource
        private void ViewCreatureResourceLocation()
        {
            if (_creatureTable?.SelectedItem == null || _installation == null)
            {
                return;
            }

            try
            {
                // Extract ResRef from selected row
                var selectedItem = _creatureTable.SelectedItem;
                var itemType = selectedItem.GetType();
                var resRefProp = itemType.GetProperty("ResRef");
                
                if (resRefProp == null)
                {
                    return;
                }

                var resRefValue = resRefProp.GetValue(selectedItem);
                if (resRefValue == null || string.IsNullOrEmpty(resRefValue.ToString()))
                {
                    return;
                }

                string creatureResRef = resRefValue.ToString().Trim();

                // Find the creature resource location
                var utpIdentifier = new ResourceIdentifier(creatureResRef, ResourceType.UTP);
                var locations = _installation.Locations(
                    new List<ResourceIdentifier> { utpIdentifier },
                    null,
                    null);

                if (locations.Count > 0 && locations.ContainsKey(utpIdentifier) &&
                    locations[utpIdentifier].Count > 0)
                {
                    var foundLocations = locations[utpIdentifier];
                    // Show dialog with all found locations
                    var dialog = new ResourceLocationDialog(
                        this,
                        creatureResRef,
                        ResourceType.UTP,
                        foundLocations,
                        _installation);
                    dialog.ShowDialog(this);
                }
                else
                {
                    // Show "not found" message
                    var msgBox = MessageBoxManager.GetMessageBoxStandard(
                        "Resource Not Found",
                        $"Creature '{creatureResRef}' not found in installation.\n\nSearched for:\n- {creatureResRef}.utp",
                        ButtonEnum.Ok,
                        MsBox.Avalonia.Enums.Icon.Info);
                    msgBox.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error viewing creature location: {ex.Message}");
            }
        }
    }
}
