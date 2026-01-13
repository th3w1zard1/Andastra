using BioWare.NET.Common;
using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using BioWare.NET;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource;
using Avalonia.Controls.Primitives;
using HolocronToolset.Data;
using HolocronToolset.Dialogs;
using Window = Avalonia.Controls.Window;
using IFO = BioWare.NET.Resource.Formats.GFF.Generics.IFO;
using NumericUpDown = Avalonia.Controls.NumericUpDown;
using TextBox = Avalonia.Controls.TextBox;
using Button = Avalonia.Controls.Button;
using ScrollViewer = Avalonia.Controls.ScrollViewer;
using StackPanel = Avalonia.Controls.StackPanel;
using Expander = Avalonia.Controls.Expander;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:22
    // Original: class IFOEditor(Editor):
    public class IFOEditor : Editor
    {
        private IFO _ifo;

        // Public property to access IFO for testing (matching Python's self.ifo)
        public IFO Ifo => _ifo;

        // Public properties for testing (matching Python's public attributes)
        public TextBox TagEdit => _tagEdit;
        public TextBox VoIdEdit => _voIdEdit;
        public TextBox HakEdit => _hakEdit;
        public TextBox EntryResrefEdit => _entryResrefEdit;
        public NumericUpDown EntryXSpin => _entryXSpin;
        public NumericUpDown EntryYSpin => _entryYSpin;
        public NumericUpDown EntryZSpin => _entryZSpin;
        public NumericUpDown EntryDirSpin => _entryDirSpin;
        public NumericUpDown DawnHourSpin => _dawnHourSpin;
        public NumericUpDown DuskHourSpin => _duskHourSpin;
        public NumericUpDown TimeScaleSpin => _timeScaleSpin;
        public NumericUpDown StartMonthSpin => _startMonthSpin;
        public NumericUpDown StartDaySpin => _startDaySpin;
        public NumericUpDown StartHourSpin => _startHourSpin;
        public NumericUpDown StartYearSpin => _startYearSpin;
        public NumericUpDown XpScaleSpin => _xpScaleSpin;
        public Dictionary<string, TextBox> ScriptFields => _scriptFields;

        // UI Controls - Basic Info
        private TextBox _nameEdit;
        private Button _nameEditBtn;
        private TextBox _tagEdit;
        private TextBox _voIdEdit;
        private TextBox _hakEdit;
        private TextBox _descEdit;
        private Button _descEditBtn;

        // UI Controls - Entry Point
        private TextBox _entryResrefEdit;
        private NumericUpDown _entryXSpin;
        private NumericUpDown _entryYSpin;
        private NumericUpDown _entryZSpin;
        private NumericUpDown _entryDirSpin;

        // UI Controls - Time Settings
        private NumericUpDown _dawnHourSpin;
        private NumericUpDown _duskHourSpin;
        private NumericUpDown _timeScaleSpin;
        private NumericUpDown _startMonthSpin;
        private NumericUpDown _startDaySpin;
        private NumericUpDown _startHourSpin;
        private NumericUpDown _startYearSpin;
        private NumericUpDown _xpScaleSpin;

        // UI Controls - Scripts
        private Dictionary<string, TextBox> _scriptFields;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:25-38
        // Original: def __init__(self, parent, installation):
        public IFOEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Module Info Editor", "ifo",
                new[] { ResourceType.IFO },
                new[] { ResourceType.IFO },
                installation)
        {
            InitializeComponent();
            SetupUI();
            AddHelpAction(); // Auto-detects "GFF-IFO.md" for IFO
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

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:39-177
        // Original: def setup_ui(self):
        private void SetupProgrammaticUI()
        {
            var scrollViewer = new ScrollViewer();
            var mainPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Basic Info Group
            var basicGroup = new Expander { Header = "Basic Information", IsExpanded = true };
            var basicPanel = new StackPanel { Orientation = Orientation.Vertical };

            // Module Name
            var nameLabel = new TextBlock { Text = "Module Name:" };
            _nameEdit = new TextBox { IsReadOnly = true };
            _nameEditBtn = new Button { Content = "Edit Name" };
            _nameEditBtn.Click += (s, e) => EditName();
            basicPanel.Children.Add(nameLabel);
            basicPanel.Children.Add(_nameEdit);
            basicPanel.Children.Add(_nameEditBtn);

            // Tag
            var tagLabel = new TextBlock { Text = "Tag:" };
            _tagEdit = new TextBox();
            _tagEdit.TextChanged += (s, e) => OnValueChanged();
            basicPanel.Children.Add(tagLabel);
            basicPanel.Children.Add(_tagEdit);

            // VO ID
            var voIdLabel = new TextBlock { Text = "VO ID:" };
            _voIdEdit = new TextBox();
            _voIdEdit.TextChanged += (s, e) => OnValueChanged();
            basicPanel.Children.Add(voIdLabel);
            basicPanel.Children.Add(_voIdEdit);

            // Hak
            var hakLabel = new TextBlock { Text = "Hak:" };
            _hakEdit = new TextBox();
            _hakEdit.TextChanged += (s, e) => OnValueChanged();
            basicPanel.Children.Add(hakLabel);
            basicPanel.Children.Add(_hakEdit);

            // Description
            var descLabel = new TextBlock { Text = "Description:" };
            _descEdit = new TextBox { IsReadOnly = true, AcceptsReturn = true };
            _descEditBtn = new Button { Content = "Edit Description" };
            _descEditBtn.Click += (s, e) => EditDescription();
            basicPanel.Children.Add(descLabel);
            basicPanel.Children.Add(_descEdit);
            basicPanel.Children.Add(_descEditBtn);

            basicGroup.Content = basicPanel;
            mainPanel.Children.Add(basicGroup);

            // Entry Point Group
            var entryGroup = new Expander { Header = "Entry Point", IsExpanded = true };
            var entryPanel = new StackPanel { Orientation = Orientation.Vertical };

            var entryResrefLabel = new TextBlock { Text = "Area ResRef:" };
            _entryResrefEdit = new TextBox();
            _entryResrefEdit.TextChanged += (s, e) => OnValueChanged();
            entryPanel.Children.Add(entryResrefLabel);
            entryPanel.Children.Add(_entryResrefEdit);

            var entryXLabel = new TextBlock { Text = "Entry X:" };
            _entryXSpin = new NumericUpDown { Minimum = -99999, Maximum = 99999, Increment = 0.1m };
            _entryXSpin.ValueChanged += (s, e) => OnValueChanged();
            entryPanel.Children.Add(entryXLabel);
            entryPanel.Children.Add(_entryXSpin);

            var entryYLabel = new TextBlock { Text = "Entry Y:" };
            _entryYSpin = new NumericUpDown { Minimum = -99999, Maximum = 99999, Increment = 0.1m };
            _entryYSpin.ValueChanged += (s, e) => OnValueChanged();
            entryPanel.Children.Add(entryYLabel);
            entryPanel.Children.Add(_entryYSpin);

            var entryZLabel = new TextBlock { Text = "Entry Z:" };
            _entryZSpin = new NumericUpDown { Minimum = -99999, Maximum = 99999, Increment = 0.1m };
            _entryZSpin.ValueChanged += (s, e) => OnValueChanged();
            entryPanel.Children.Add(entryZLabel);
            entryPanel.Children.Add(_entryZSpin);

            var entryDirLabel = new TextBlock { Text = "Entry Direction:" };
            _entryDirSpin = new NumericUpDown { Minimum = -3.14159m, Maximum = 3.14159m, Increment = 0.01m };
            _entryDirSpin.ValueChanged += (s, e) => OnValueChanged();
            entryPanel.Children.Add(entryDirLabel);
            entryPanel.Children.Add(_entryDirSpin);

            entryGroup.Content = entryPanel;
            mainPanel.Children.Add(entryGroup);

            // Time Settings Group
            var timeGroup = new Expander { Header = "Time Settings", IsExpanded = true };
            var timePanel = new StackPanel { Orientation = Orientation.Vertical };

            var dawnHourLabel = new TextBlock { Text = "Dawn Hour:" };
            _dawnHourSpin = new NumericUpDown { Minimum = 0, Maximum = 23 };
            _dawnHourSpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(dawnHourLabel);
            timePanel.Children.Add(_dawnHourSpin);

            var duskHourLabel = new TextBlock { Text = "Dusk Hour:" };
            _duskHourSpin = new NumericUpDown { Minimum = 0, Maximum = 23 };
            _duskHourSpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(duskHourLabel);
            timePanel.Children.Add(_duskHourSpin);

            var timeScaleLabel = new TextBlock { Text = "Time Scale:" };
            _timeScaleSpin = new NumericUpDown { Minimum = 0, Maximum = 100 };
            _timeScaleSpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(timeScaleLabel);
            timePanel.Children.Add(_timeScaleSpin);

            var startMonthLabel = new TextBlock { Text = "Start Month:" };
            _startMonthSpin = new NumericUpDown { Minimum = 1, Maximum = 12 };
            _startMonthSpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(startMonthLabel);
            timePanel.Children.Add(_startMonthSpin);

            var startDayLabel = new TextBlock { Text = "Start Day:" };
            _startDaySpin = new NumericUpDown { Minimum = 1, Maximum = 31 };
            _startDaySpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(startDayLabel);
            timePanel.Children.Add(_startDaySpin);

            var startHourLabel = new TextBlock { Text = "Start Hour:" };
            _startHourSpin = new NumericUpDown { Minimum = 0, Maximum = 23 };
            _startHourSpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(startHourLabel);
            timePanel.Children.Add(_startHourSpin);

            var startYearLabel = new TextBlock { Text = "Start Year:" };
            _startYearSpin = new NumericUpDown { Minimum = 0, Maximum = 9999 };
            _startYearSpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(startYearLabel);
            timePanel.Children.Add(_startYearSpin);

            var xpScaleLabel = new TextBlock { Text = "XP Scale:" };
            _xpScaleSpin = new NumericUpDown { Minimum = 0, Maximum = 100 };
            _xpScaleSpin.ValueChanged += (s, e) => OnValueChanged();
            timePanel.Children.Add(xpScaleLabel);
            timePanel.Children.Add(_xpScaleSpin);

            timeGroup.Content = timePanel;
            mainPanel.Children.Add(timeGroup);

            // Scripts Group
            var scriptGroup = new Expander { Header = "Scripts", IsExpanded = true };
            var scriptPanel = new StackPanel { Orientation = Orientation.Vertical };
            _scriptFields = new Dictionary<string, TextBox>();

            string[] scriptNames = {
                "on_heartbeat", "on_load", "on_start", "on_enter", "on_leave",
                "on_activate_item", "on_acquire_item", "on_user_defined", "on_unacquire_item",
                "on_player_death", "on_player_dying", "on_player_levelup", "on_player_respawn",
                "on_player_rest", "start_movie"
            };

            foreach (string scriptName in scriptNames)
            {
                var label = new TextBlock { Text = scriptName.Replace("_", " ").ToUpperInvariant() + ":" };
                var edit = new TextBox();
                edit.TextChanged += (s, e) => OnValueChanged();
                _scriptFields[scriptName] = edit;
                scriptPanel.Children.Add(label);
                scriptPanel.Children.Add(edit);
            }

            scriptGroup.Content = scriptPanel;
            mainPanel.Children.Add(scriptGroup);

            scrollViewer.Content = mainPanel;
            Content = scrollViewer;
        }

        private void SetupUI()
        {
            // Try to find controls from XAML if available (only if not already set by programmatic UI)
            if (_nameEdit == null) _nameEdit = EditorHelpers.FindControlSafe<TextBox>(this, "NameEdit");
            if (_nameEditBtn == null) _nameEditBtn = EditorHelpers.FindControlSafe<Button>(this, "NameEditBtn");
            if (_tagEdit == null) _tagEdit = EditorHelpers.FindControlSafe<TextBox>(this, "TagEdit");
            if (_voIdEdit == null) _voIdEdit = EditorHelpers.FindControlSafe<TextBox>(this, "VoIdEdit");
            if (_hakEdit == null) _hakEdit = EditorHelpers.FindControlSafe<TextBox>(this, "HakEdit");
            if (_descEdit == null) _descEdit = EditorHelpers.FindControlSafe<TextBox>(this, "DescEdit");
            if (_descEditBtn == null) _descEditBtn = EditorHelpers.FindControlSafe<Button>(this, "DescEditBtn");
            if (_entryResrefEdit == null) _entryResrefEdit = EditorHelpers.FindControlSafe<TextBox>(this, "EntryResrefEdit");
            if (_entryXSpin == null) _entryXSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "EntryXSpin");
            if (_entryYSpin == null) _entryYSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "EntryYSpin");
            if (_entryZSpin == null) _entryZSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "EntryZSpin");
            if (_entryDirSpin == null) _entryDirSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "EntryDirSpin");
            if (_dawnHourSpin == null) _dawnHourSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "DawnHourSpin");
            if (_duskHourSpin == null) _duskHourSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "DuskHourSpin");
            if (_timeScaleSpin == null) _timeScaleSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "TimeScaleSpin");
            if (_startMonthSpin == null) _startMonthSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "StartMonthSpin");
            if (_startDaySpin == null) _startDaySpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "StartDaySpin");
            if (_startHourSpin == null) _startHourSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "StartHourSpin");
            if (_startYearSpin == null) _startYearSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "StartYearSpin");
            if (_xpScaleSpin == null) _xpScaleSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "XpScaleSpin");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:179-195
        // Original: def edit_name(self) and def edit_description(self):
        private void EditName()
        {
            if (_ifo == null || _installation == null)
            {
                return;
            }
            var dialog = new LocalizedStringDialog(this, _installation, _ifo.ModName);
            if (dialog.ShowDialog())
            {
                _ifo.ModName = dialog.LocString;
                _ifo.Name = _ifo.ModName; // Alias
                UpdateUIFromIFO();
            }
        }

        private void EditDescription()
        {
            if (_ifo == null || _installation == null)
            {
                return;
            }
            var dialog = new LocalizedStringDialog(this, _installation, _ifo.Description);
            if (dialog.ShowDialog())
            {
                _ifo.Description = dialog.LocString;
                UpdateUIFromIFO();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:197-201
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            if (data == null || data.Length == 0)
            {
                _ifo = new IFO();
                UpdateUIFromIFO();
                return;
            }

            try
            {
                var gff = GFF.FromBytes(data);
                _ifo = IFOHelpers.ConstructIfo(gff);
                UpdateUIFromIFO();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Failed to load IFO: {ex}");
                _ifo = new IFO();
                UpdateUIFromIFO();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:203-210
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            if (_ifo == null)
            {
                return Tuple.Create(new byte[0], new byte[0]);
            }

            var gff = IFOHelpers.DismantleIfo(_ifo);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.IFO);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:212-216
        // Original: def new(self):
        public override void New()
        {
            base.New();
            _ifo = new IFO();
            UpdateUIFromIFO();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:218-249
        // Original: def update_ui_from_ifo(self):
        private void UpdateUIFromIFO()
        {
            if (_ifo == null)
            {
                return;
            }

            // Basic Info
            if (_nameEdit != null)
            {
                // Get localized string from installation if available
                if (_installation != null)
                {
                    string nameText = _installation.String(_ifo.ModName) ?? "";
                    _nameEdit.Text = nameText;
                }
                else
                {
                    // Fallback to string representation if no installation
                    _nameEdit.Text = _ifo.ModName?.ToString() ?? "";
                }
            }
            if (_tagEdit != null) _tagEdit.Text = _ifo.Tag ?? "";
            if (_voIdEdit != null) _voIdEdit.Text = _ifo.VoId ?? "";
            if (_hakEdit != null) _hakEdit.Text = _ifo.Hak ?? "";
            if (_descEdit != null)
            {
                if (_installation != null)
                {
                    string descText = _installation.String(_ifo.Description) ?? "";
                    _descEdit.Text = descText;
                }
                else
                {
                    // Fallback to string representation if no installation
                    _descEdit.Text = _ifo.Description?.ToString() ?? "";
                }
            }

            // Entry Point
            if (_entryResrefEdit != null) _entryResrefEdit.Text = _ifo.ResRef.ToString();
            if (_entryXSpin != null) _entryXSpin.Value = (decimal)_ifo.EntryX;
            if (_entryYSpin != null) _entryYSpin.Value = (decimal)_ifo.EntryY;
            if (_entryZSpin != null) _entryZSpin.Value = (decimal)_ifo.EntryZ;
            if (_entryDirSpin != null) _entryDirSpin.Value = (decimal)_ifo.EntryDirection;

            // Time Settings
            if (_dawnHourSpin != null) _dawnHourSpin.Value = _ifo.DawnHour;
            if (_duskHourSpin != null) _duskHourSpin.Value = _ifo.DuskHour;
            if (_timeScaleSpin != null) _timeScaleSpin.Value = _ifo.TimeScale;
            if (_startMonthSpin != null) _startMonthSpin.Value = _ifo.StartMonth;
            if (_startDaySpin != null) _startDaySpin.Value = _ifo.StartDay;
            if (_startHourSpin != null) _startHourSpin.Value = _ifo.StartHour;
            if (_startYearSpin != null) _startYearSpin.Value = _ifo.StartYear;
            if (_xpScaleSpin != null) _xpScaleSpin.Value = _ifo.XpScale;

            // Scripts
            if (_scriptFields != null)
            {
                if (_scriptFields.ContainsKey("on_heartbeat") && _scriptFields["on_heartbeat"] != null)
                    _scriptFields["on_heartbeat"].Text = _ifo.OnHeartbeat.ToString();
                if (_scriptFields.ContainsKey("on_load") && _scriptFields["on_load"] != null)
                    _scriptFields["on_load"].Text = _ifo.OnLoad.ToString();
                if (_scriptFields.ContainsKey("on_start") && _scriptFields["on_start"] != null)
                    _scriptFields["on_start"].Text = _ifo.OnStart.ToString();
                if (_scriptFields.ContainsKey("on_enter") && _scriptFields["on_enter"] != null)
                    _scriptFields["on_enter"].Text = _ifo.OnClientEnter.ToString();
                if (_scriptFields.ContainsKey("on_leave") && _scriptFields["on_leave"] != null)
                    _scriptFields["on_leave"].Text = _ifo.OnClientLeave.ToString();
                if (_scriptFields.ContainsKey("on_activate_item") && _scriptFields["on_activate_item"] != null)
                    _scriptFields["on_activate_item"].Text = _ifo.OnActivateItem.ToString();
                if (_scriptFields.ContainsKey("on_acquire_item") && _scriptFields["on_acquire_item"] != null)
                    _scriptFields["on_acquire_item"].Text = _ifo.OnAcquireItem.ToString();
                if (_scriptFields.ContainsKey("on_user_defined") && _scriptFields["on_user_defined"] != null)
                    _scriptFields["on_user_defined"].Text = _ifo.OnUserDefined.ToString();
                if (_scriptFields.ContainsKey("on_unacquire_item") && _scriptFields["on_unacquire_item"] != null)
                    _scriptFields["on_unacquire_item"].Text = _ifo.OnUnacquireItem.ToString();
                if (_scriptFields.ContainsKey("on_player_death") && _scriptFields["on_player_death"] != null)
                    _scriptFields["on_player_death"].Text = _ifo.OnPlayerDeath.ToString();
                if (_scriptFields.ContainsKey("on_player_dying") && _scriptFields["on_player_dying"] != null)
                    _scriptFields["on_player_dying"].Text = _ifo.OnPlayerDying.ToString();
                if (_scriptFields.ContainsKey("on_player_levelup") && _scriptFields["on_player_levelup"] != null)
                    _scriptFields["on_player_levelup"].Text = _ifo.OnPlayerLevelUp.ToString();
                if (_scriptFields.ContainsKey("on_player_respawn") && _scriptFields["on_player_respawn"] != null)
                    _scriptFields["on_player_respawn"].Text = _ifo.OnPlayerRespawn.ToString();
                if (_scriptFields.ContainsKey("on_player_rest") && _scriptFields["on_player_rest"] != null)
                    _scriptFields["on_player_rest"].Text = _ifo.OnPlayerRest.ToString();
                if (_scriptFields.ContainsKey("start_movie") && _scriptFields["start_movie"] != null)
                    _scriptFields["start_movie"].Text = _ifo.StartMovie.ToString();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ifo.py:251-288
        // Original: def on_value_changed(self):
        public void OnValueChanged()
        {
            if (_ifo == null)
            {
                return;
            }

            // Basic Info
            if (_tagEdit != null) _ifo.Tag = _tagEdit.Text ?? "";
            if (_voIdEdit != null) _ifo.VoId = _voIdEdit.Text ?? "";
            if (_hakEdit != null) _ifo.Hak = _hakEdit.Text ?? "";

            // Entry Point
            if (_entryResrefEdit != null)
            {
                try
                {
                    _ifo.ResRef = new ResRef(_entryResrefEdit.Text ?? "");
                    _ifo.EntryArea = _ifo.ResRef; // Alias
                }
                catch
                {
                    // Skip invalid ResRef values
                }
            }
            if (_entryXSpin != null && _entryXSpin.Value.HasValue)
                _ifo.EntryX = (float)_entryXSpin.Value.Value;
            if (_entryYSpin != null && _entryYSpin.Value.HasValue)
                _ifo.EntryY = (float)_entryYSpin.Value.Value;
            if (_entryZSpin != null && _entryZSpin.Value.HasValue)
                _ifo.EntryZ = (float)_entryZSpin.Value.Value;
            if (_entryDirSpin != null && _entryDirSpin.Value.HasValue)
            {
                _ifo.EntryDirection = (float)_entryDirSpin.Value.Value;
                // Update direction components from angle
                _ifo.EntryDirectionX = (float)System.Math.Cos(_ifo.EntryDirection);
                _ifo.EntryDirectionY = (float)System.Math.Sin(_ifo.EntryDirection);
            }

            // Time Settings
            if (_dawnHourSpin != null && _dawnHourSpin.Value.HasValue)
                _ifo.DawnHour = (int)_dawnHourSpin.Value.Value;
            if (_duskHourSpin != null && _duskHourSpin.Value.HasValue)
                _ifo.DuskHour = (int)_duskHourSpin.Value.Value;
            if (_timeScaleSpin != null && _timeScaleSpin.Value.HasValue)
                _ifo.TimeScale = (int)_timeScaleSpin.Value.Value;
            if (_startMonthSpin != null && _startMonthSpin.Value.HasValue)
                _ifo.StartMonth = (int)_startMonthSpin.Value.Value;
            if (_startDaySpin != null && _startDaySpin.Value.HasValue)
                _ifo.StartDay = (int)_startDaySpin.Value.Value;
            if (_startHourSpin != null && _startHourSpin.Value.HasValue)
                _ifo.StartHour = (int)_startHourSpin.Value.Value;
            if (_startYearSpin != null && _startYearSpin.Value.HasValue)
                _ifo.StartYear = (int)_startYearSpin.Value.Value;
            if (_xpScaleSpin != null && _xpScaleSpin.Value.HasValue)
                _ifo.XpScale = (int)_xpScaleSpin.Value.Value;

            // Scripts
            if (_scriptFields != null)
            {
                if (_scriptFields.ContainsKey("on_heartbeat") && _scriptFields["on_heartbeat"] != null)
                {
                    try { _ifo.OnHeartbeat = new ResRef(_scriptFields["on_heartbeat"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_load") && _scriptFields["on_load"] != null)
                {
                    try { _ifo.OnLoad = new ResRef(_scriptFields["on_load"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_start") && _scriptFields["on_start"] != null)
                {
                    try { _ifo.OnStart = new ResRef(_scriptFields["on_start"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_enter") && _scriptFields["on_enter"] != null)
                {
                    try { _ifo.OnClientEnter = new ResRef(_scriptFields["on_enter"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_leave") && _scriptFields["on_leave"] != null)
                {
                    try { _ifo.OnClientLeave = new ResRef(_scriptFields["on_leave"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_activate_item") && _scriptFields["on_activate_item"] != null)
                {
                    try { _ifo.OnActivateItem = new ResRef(_scriptFields["on_activate_item"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_acquire_item") && _scriptFields["on_acquire_item"] != null)
                {
                    try { _ifo.OnAcquireItem = new ResRef(_scriptFields["on_acquire_item"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_user_defined") && _scriptFields["on_user_defined"] != null)
                {
                    try { _ifo.OnUserDefined = new ResRef(_scriptFields["on_user_defined"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_unacquire_item") && _scriptFields["on_unacquire_item"] != null)
                {
                    try { _ifo.OnUnacquireItem = new ResRef(_scriptFields["on_unacquire_item"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_player_death") && _scriptFields["on_player_death"] != null)
                {
                    try { _ifo.OnPlayerDeath = new ResRef(_scriptFields["on_player_death"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_player_dying") && _scriptFields["on_player_dying"] != null)
                {
                    try { _ifo.OnPlayerDying = new ResRef(_scriptFields["on_player_dying"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_player_levelup") && _scriptFields["on_player_levelup"] != null)
                {
                    try { _ifo.OnPlayerLevelUp = new ResRef(_scriptFields["on_player_levelup"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_player_respawn") && _scriptFields["on_player_respawn"] != null)
                {
                    try { _ifo.OnPlayerRespawn = new ResRef(_scriptFields["on_player_respawn"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("on_player_rest") && _scriptFields["on_player_rest"] != null)
                {
                    try { _ifo.OnPlayerRest = new ResRef(_scriptFields["on_player_rest"].Text ?? ""); } catch { }
                }
                if (_scriptFields.ContainsKey("start_movie") && _scriptFields["start_movie"] != null)
                {
                    try { _ifo.StartMovie = new ResRef(_scriptFields["start_movie"].Text ?? ""); } catch { }
                }
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
