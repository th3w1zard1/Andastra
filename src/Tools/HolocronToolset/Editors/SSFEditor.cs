using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using BioWare.NET.Extract;
using BioWare.NET.Resource.Formats.SSF;
using BioWare.NET.Resource;
using HolocronToolset.Common;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:21
    // Original: class SSFEditor(Editor):
    public partial class SSFEditor : Editor
    {
        // UI controls - all spin boxes
        private NumericUpDown _battlecry1StrrefSpin;
        private NumericUpDown _battlecry2StrrefSpin;
        private NumericUpDown _battlecry3StrrefSpin;
        private NumericUpDown _battlecry4StrrefSpin;
        private NumericUpDown _battlecry5StrrefSpin;
        private NumericUpDown _battlecry6StrrefSpin;
        private NumericUpDown _select1StrrefSpin;
        private NumericUpDown _select2StrrefSpin;
        private NumericUpDown _select3StrrefSpin;
        private NumericUpDown _attack1StrrefSpin;
        private NumericUpDown _attack2StrrefSpin;
        private NumericUpDown _attack3StrrefSpin;
        private NumericUpDown _pain1StrrefSpin;
        private NumericUpDown _pain2StrrefSpin;
        private NumericUpDown _lowHpStrrefSpin;
        private NumericUpDown _deadStrrefSpin;
        private NumericUpDown _criticalStrrefSpin;
        private NumericUpDown _immuneStrrefSpin;
        private NumericUpDown _layMineStrrefSpin;
        private NumericUpDown _disarmMineStrrefSpin;
        private NumericUpDown _beginStealthStrrefSpin;
        private NumericUpDown _beginSearchStrrefSpin;
        private NumericUpDown _beginUnlockStrrefSpin;
        private NumericUpDown _unlockSuccessStrrefSpin;
        private NumericUpDown _unlockFailedStrrefSpin;
        private NumericUpDown _partySeparatedStrrefSpin;
        private NumericUpDown _rejoinPartyStrrefSpin;
        private NumericUpDown _poisonedStrrefSpin;

        // Text boxes for displaying sound/text from talktable
        private Dictionary<SSFSound, (TextBox soundEdit, TextBox textEdit)> _soundTextPairs;

        private TalkTable _talktable;
        private TextBlock _talktableLabel;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:22-61
        // Original: def __init__(self, parent, installation):
        public SSFEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Soundset Editor", "soundset", new[] { ResourceType.SSF }, new[] { ResourceType.SSF }, installation)
        {
            _talktable = installation != null ? new TalkTable(System.IO.Path.Combine(installation.Path, "dialog.tlk")) : null;
            _soundTextPairs = new Dictionary<SSFSound, (TextBox, TextBox)>();
            InitializeComponent();
            SetupUI();
            SetupSignals();
            UpdateTalktableLabel();
            New();
            MinWidth = 577;
            MinHeight = 437;
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

        private void SetupProgrammaticUI()
        {
            var mainPanel = new ScrollViewer
            {
                Content = new StackPanel { Orientation = Orientation.Vertical }
            };

            var stackPanel = mainPanel.Content as StackPanel;
            if (stackPanel == null)
            {
                return;
            }

            // Add talktable selection button at the top
            var talktablePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var talktableButton = new Button
            {
                Content = "Select Talk Table",
                Width = 150,
                Height = 30
            };
            talktableButton.Click += async (s, e) => await SelectTalkTable();
            talktablePanel.Children.Add(talktableButton);

            _talktableLabel = new TextBlock
            {
                Text = _talktable != null ? $"Using: {System.IO.Path.GetFileName(_talktable.Path)}" : "Using installation talktable",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            talktablePanel.Children.Add(_talktableLabel);
            stackPanel.Children.Add(talktablePanel);

            // Create all spin boxes and text boxes programmatically
            CreateSoundControl(stackPanel, "Battle Cry 1", SSFSound.BATTLE_CRY_1, out _battlecry1StrrefSpin);
            CreateSoundControl(stackPanel, "Battle Cry 2", SSFSound.BATTLE_CRY_2, out _battlecry2StrrefSpin);
            CreateSoundControl(stackPanel, "Battle Cry 3", SSFSound.BATTLE_CRY_3, out _battlecry3StrrefSpin);
            CreateSoundControl(stackPanel, "Battle Cry 4", SSFSound.BATTLE_CRY_4, out _battlecry4StrrefSpin);
            CreateSoundControl(stackPanel, "Battle Cry 5", SSFSound.BATTLE_CRY_5, out _battlecry5StrrefSpin);
            CreateSoundControl(stackPanel, "Battle Cry 6", SSFSound.BATTLE_CRY_6, out _battlecry6StrrefSpin);
            CreateSoundControl(stackPanel, "Select 1", SSFSound.SELECT_1, out _select1StrrefSpin);
            CreateSoundControl(stackPanel, "Select 2", SSFSound.SELECT_2, out _select2StrrefSpin);
            CreateSoundControl(stackPanel, "Select 3", SSFSound.SELECT_3, out _select3StrrefSpin);
            CreateSoundControl(stackPanel, "Attack 1", SSFSound.ATTACK_GRUNT_1, out _attack1StrrefSpin);
            CreateSoundControl(stackPanel, "Attack 2", SSFSound.ATTACK_GRUNT_2, out _attack2StrrefSpin);
            CreateSoundControl(stackPanel, "Attack 3", SSFSound.ATTACK_GRUNT_3, out _attack3StrrefSpin);
            CreateSoundControl(stackPanel, "Pain 1", SSFSound.PAIN_GRUNT_1, out _pain1StrrefSpin);
            CreateSoundControl(stackPanel, "Pain 2", SSFSound.PAIN_GRUNT_2, out _pain2StrrefSpin);
            CreateSoundControl(stackPanel, "Low HP", SSFSound.LOW_HEALTH, out _lowHpStrrefSpin);
            CreateSoundControl(stackPanel, "Dead", SSFSound.DEAD, out _deadStrrefSpin);
            CreateSoundControl(stackPanel, "Critical", SSFSound.CRITICAL_HIT, out _criticalStrrefSpin);
            CreateSoundControl(stackPanel, "Immune", SSFSound.TARGET_IMMUNE, out _immuneStrrefSpin);
            CreateSoundControl(stackPanel, "Lay Mine", SSFSound.LAY_MINE, out _layMineStrrefSpin);
            CreateSoundControl(stackPanel, "Disarm Mine", SSFSound.DISARM_MINE, out _disarmMineStrrefSpin);
            CreateSoundControl(stackPanel, "Begin Stealth", SSFSound.BEGIN_STEALTH, out _beginStealthStrrefSpin);
            CreateSoundControl(stackPanel, "Begin Search", SSFSound.BEGIN_SEARCH, out _beginSearchStrrefSpin);
            CreateSoundControl(stackPanel, "Begin Unlock", SSFSound.BEGIN_UNLOCK, out _beginUnlockStrrefSpin);
            CreateSoundControl(stackPanel, "Unlock Success", SSFSound.UNLOCK_SUCCESS, out _unlockSuccessStrrefSpin);
            CreateSoundControl(stackPanel, "Unlock Failed", SSFSound.UNLOCK_FAILED, out _unlockFailedStrrefSpin);
            CreateSoundControl(stackPanel, "Party Separated", SSFSound.SEPARATED_FROM_PARTY, out _partySeparatedStrrefSpin);
            CreateSoundControl(stackPanel, "Rejoin Party", SSFSound.REJOINED_PARTY, out _rejoinPartyStrrefSpin);
            CreateSoundControl(stackPanel, "Poisoned", SSFSound.POISONED, out _poisonedStrrefSpin);

            Content = mainPanel;
        }

        private void CreateSoundControl(Panel parent, string label, SSFSound sound, out NumericUpDown spinBox)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text = label, Width = 150 });

            spinBox = new NumericUpDown { Minimum = -1, Maximum = int.MaxValue, Width = 100 };
            panel.Children.Add(spinBox);

            var soundEdit = new TextBox { Width = 150, IsReadOnly = true };
            var textEdit = new TextBox { Width = 200, IsReadOnly = true };
            panel.Children.Add(soundEdit);
            panel.Children.Add(textEdit);

            _soundTextPairs[sound] = (soundEdit, textEdit);
            parent.Children.Add(panel);
        }

        private void SetupUI()
        {
            // Try to find controls from XAML if available
            _battlecry1StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Battlecry1StrrefSpin");
            _battlecry2StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Battlecry2StrrefSpin");
            _battlecry3StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Battlecry3StrrefSpin");
            _battlecry4StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Battlecry4StrrefSpin");
            _battlecry5StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Battlecry5StrrefSpin");
            _battlecry6StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Battlecry6StrrefSpin");
            _select1StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Select1StrrefSpin");
            _select2StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Select2StrrefSpin");
            _select3StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Select3StrrefSpin");
            _attack1StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Attack1StrrefSpin");
            _attack2StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Attack2StrrefSpin");
            _attack3StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Attack3StrrefSpin");
            _pain1StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Pain1StrrefSpin");
            _pain2StrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "Pain2StrrefSpin");
            _lowHpStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "LowHpStrrefSpin");
            _deadStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "DeadStrrefSpin");
            _criticalStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "CriticalStrrefSpin");
            _immuneStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "ImmuneStrrefSpin");
            _layMineStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "LayMineStrrefSpin");
            _disarmMineStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "DisarmMineStrrefSpin");
            _beginStealthStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "BeginStealthStrrefSpin");
            _beginSearchStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "BeginSearchStrrefSpin");
            _beginUnlockStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "BeginUnlockStrrefSpin");
            _unlockSuccessStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "UnlockSuccessStrrefSpin");
            _unlockFailedStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "UnlockFailedStrrefSpin");
            _partySeparatedStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "PartySeparatedStrrefSpin");
            _rejoinPartyStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "RejoinPartyStrrefSpin");
            _poisonedStrrefSpin = EditorHelpers.FindControlSafe<NumericUpDown>(this, "PoisonedStrrefSpin");

            // Try to find text boxes from XAML
            if (_soundTextPairs.Count == 0)
            {
                // Initialize pairs if not already done
                _soundTextPairs[SSFSound.BATTLE_CRY_1] = (EditorHelpers.FindControlSafe<TextBox>(this, "Battlecry1SoundEdit"), EditorHelpers.FindControlSafe<TextBox>(this, "Battlecry1TextEdit"));
                // Add more pairs as needed
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:63-104
        // Original: def _setup_signals(self):
        private void SetupSignals()
        {
            // Connect value changed events for all spin boxes
            if (_battlecry1StrrefSpin != null) _battlecry1StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_battlecry2StrrefSpin != null) _battlecry2StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_battlecry3StrrefSpin != null) _battlecry3StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_battlecry4StrrefSpin != null) _battlecry4StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_battlecry5StrrefSpin != null) _battlecry5StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_battlecry6StrrefSpin != null) _battlecry6StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_select1StrrefSpin != null) _select1StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_select2StrrefSpin != null) _select2StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_select3StrrefSpin != null) _select3StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_attack1StrrefSpin != null) _attack1StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_attack2StrrefSpin != null) _attack2StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_attack3StrrefSpin != null) _attack3StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_pain1StrrefSpin != null) _pain1StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_pain2StrrefSpin != null) _pain2StrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_lowHpStrrefSpin != null) _lowHpStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_deadStrrefSpin != null) _deadStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_criticalStrrefSpin != null) _criticalStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_immuneStrrefSpin != null) _immuneStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_layMineStrrefSpin != null) _layMineStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_disarmMineStrrefSpin != null) _disarmMineStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_beginStealthStrrefSpin != null) _beginStealthStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_beginSearchStrrefSpin != null) _beginSearchStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_beginUnlockStrrefSpin != null) _beginUnlockStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_unlockSuccessStrrefSpin != null) _unlockSuccessStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_unlockFailedStrrefSpin != null) _unlockFailedStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_partySeparatedStrrefSpin != null) _partySeparatedStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_rejoinPartyStrrefSpin != null) _rejoinPartyStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
            if (_poisonedStrrefSpin != null) _poisonedStrrefSpin.ValueChanged += (s, e) => UpdateTextBoxes();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:106-157
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            if (data == null || data.Length == 0)
            {
                // Empty SSF - all values will be 0
                return;
            }
            SSF ssf;
            try
            {
                ssf = SSFAuto.ReadSsf(data);
            }
            catch
            {
                // If loading fails, create empty SSF
                ssf = new SSF();
            }

            if (_battlecry1StrrefSpin != null) _battlecry1StrrefSpin.Value = ssf.Get(SSFSound.BATTLE_CRY_1) ?? 0;
            if (_battlecry2StrrefSpin != null) _battlecry2StrrefSpin.Value = ssf.Get(SSFSound.BATTLE_CRY_2) ?? 0;
            if (_battlecry3StrrefSpin != null) _battlecry3StrrefSpin.Value = ssf.Get(SSFSound.BATTLE_CRY_3) ?? 0;
            if (_battlecry4StrrefSpin != null) _battlecry4StrrefSpin.Value = ssf.Get(SSFSound.BATTLE_CRY_4) ?? 0;
            if (_battlecry5StrrefSpin != null) _battlecry5StrrefSpin.Value = ssf.Get(SSFSound.BATTLE_CRY_5) ?? 0;
            if (_battlecry6StrrefSpin != null) _battlecry6StrrefSpin.Value = ssf.Get(SSFSound.BATTLE_CRY_6) ?? 0;
            if (_select1StrrefSpin != null) _select1StrrefSpin.Value = ssf.Get(SSFSound.SELECT_1) ?? 0;
            if (_select2StrrefSpin != null) _select2StrrefSpin.Value = ssf.Get(SSFSound.SELECT_2) ?? 0;
            if (_select3StrrefSpin != null) _select3StrrefSpin.Value = ssf.Get(SSFSound.SELECT_3) ?? 0;
            if (_attack1StrrefSpin != null) _attack1StrrefSpin.Value = ssf.Get(SSFSound.ATTACK_GRUNT_1) ?? 0;
            if (_attack2StrrefSpin != null) _attack2StrrefSpin.Value = ssf.Get(SSFSound.ATTACK_GRUNT_2) ?? 0;
            if (_attack3StrrefSpin != null) _attack3StrrefSpin.Value = ssf.Get(SSFSound.ATTACK_GRUNT_3) ?? 0;
            if (_pain1StrrefSpin != null) _pain1StrrefSpin.Value = ssf.Get(SSFSound.PAIN_GRUNT_1) ?? 0;
            if (_pain2StrrefSpin != null) _pain2StrrefSpin.Value = ssf.Get(SSFSound.PAIN_GRUNT_2) ?? 0;
            if (_lowHpStrrefSpin != null) _lowHpStrrefSpin.Value = ssf.Get(SSFSound.LOW_HEALTH) ?? 0;
            if (_deadStrrefSpin != null) _deadStrrefSpin.Value = ssf.Get(SSFSound.DEAD) ?? 0;
            if (_criticalStrrefSpin != null) _criticalStrrefSpin.Value = ssf.Get(SSFSound.CRITICAL_HIT) ?? 0;
            if (_immuneStrrefSpin != null) _immuneStrrefSpin.Value = ssf.Get(SSFSound.TARGET_IMMUNE) ?? 0;
            if (_layMineStrrefSpin != null) _layMineStrrefSpin.Value = ssf.Get(SSFSound.LAY_MINE) ?? 0;
            if (_disarmMineStrrefSpin != null) _disarmMineStrrefSpin.Value = ssf.Get(SSFSound.DISARM_MINE) ?? 0;
            if (_beginStealthStrrefSpin != null) _beginStealthStrrefSpin.Value = ssf.Get(SSFSound.BEGIN_STEALTH) ?? 0;
            if (_beginSearchStrrefSpin != null) _beginSearchStrrefSpin.Value = ssf.Get(SSFSound.BEGIN_SEARCH) ?? 0;
            if (_beginUnlockStrrefSpin != null) _beginUnlockStrrefSpin.Value = ssf.Get(SSFSound.BEGIN_UNLOCK) ?? 0;
            if (_unlockFailedStrrefSpin != null) _unlockFailedStrrefSpin.Value = ssf.Get(SSFSound.UNLOCK_FAILED) ?? 0;
            if (_unlockSuccessStrrefSpin != null) _unlockSuccessStrrefSpin.Value = ssf.Get(SSFSound.UNLOCK_SUCCESS) ?? 0;
            if (_partySeparatedStrrefSpin != null) _partySeparatedStrrefSpin.Value = ssf.Get(SSFSound.SEPARATED_FROM_PARTY) ?? 0;
            if (_rejoinPartyStrrefSpin != null) _rejoinPartyStrrefSpin.Value = ssf.Get(SSFSound.REJOINED_PARTY) ?? 0;
            if (_poisonedStrrefSpin != null) _poisonedStrrefSpin.Value = ssf.Get(SSFSound.POISONED) ?? 0;

            UpdateTextBoxes();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:159-210
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            SSF ssf = new SSF();

            if (_battlecry1StrrefSpin != null) ssf.SetData(SSFSound.BATTLE_CRY_1, (int)(_battlecry1StrrefSpin.Value ?? 0));
            if (_battlecry2StrrefSpin != null) ssf.SetData(SSFSound.BATTLE_CRY_2, (int)(_battlecry2StrrefSpin.Value ?? 0));
            if (_battlecry3StrrefSpin != null) ssf.SetData(SSFSound.BATTLE_CRY_3, (int)(_battlecry3StrrefSpin.Value ?? 0));
            if (_battlecry4StrrefSpin != null) ssf.SetData(SSFSound.BATTLE_CRY_4, (int)(_battlecry4StrrefSpin.Value ?? 0));
            if (_battlecry5StrrefSpin != null) ssf.SetData(SSFSound.BATTLE_CRY_5, (int)(_battlecry5StrrefSpin.Value ?? 0));
            if (_battlecry6StrrefSpin != null) ssf.SetData(SSFSound.BATTLE_CRY_6, (int)(_battlecry6StrrefSpin.Value ?? 0));
            if (_select1StrrefSpin != null) ssf.SetData(SSFSound.SELECT_1, (int)(_select1StrrefSpin.Value ?? 0));
            if (_select2StrrefSpin != null) ssf.SetData(SSFSound.SELECT_2, (int)(_select2StrrefSpin.Value ?? 0));
            if (_select3StrrefSpin != null) ssf.SetData(SSFSound.SELECT_3, (int)(_select3StrrefSpin.Value ?? 0));
            if (_attack1StrrefSpin != null) ssf.SetData(SSFSound.ATTACK_GRUNT_1, (int)(_attack1StrrefSpin.Value ?? 0));
            if (_attack2StrrefSpin != null) ssf.SetData(SSFSound.ATTACK_GRUNT_2, (int)(_attack2StrrefSpin.Value ?? 0));
            if (_attack3StrrefSpin != null) ssf.SetData(SSFSound.ATTACK_GRUNT_3, (int)(_attack3StrrefSpin.Value ?? 0));
            if (_pain1StrrefSpin != null) ssf.SetData(SSFSound.PAIN_GRUNT_1, (int)(_pain1StrrefSpin.Value ?? 0));
            if (_pain2StrrefSpin != null) ssf.SetData(SSFSound.PAIN_GRUNT_2, (int)(_pain2StrrefSpin.Value ?? 0));
            if (_lowHpStrrefSpin != null) ssf.SetData(SSFSound.LOW_HEALTH, (int)(_lowHpStrrefSpin.Value ?? 0));
            if (_deadStrrefSpin != null) ssf.SetData(SSFSound.DEAD, (int)(_deadStrrefSpin.Value ?? 0));
            if (_criticalStrrefSpin != null) ssf.SetData(SSFSound.CRITICAL_HIT, (int)(_criticalStrrefSpin.Value ?? 0));
            if (_immuneStrrefSpin != null) ssf.SetData(SSFSound.TARGET_IMMUNE, (int)(_immuneStrrefSpin.Value ?? 0));
            if (_layMineStrrefSpin != null) ssf.SetData(SSFSound.LAY_MINE, (int)(_layMineStrrefSpin.Value ?? 0));
            if (_disarmMineStrrefSpin != null) ssf.SetData(SSFSound.DISARM_MINE, (int)(_disarmMineStrrefSpin.Value ?? 0));
            if (_beginStealthStrrefSpin != null) ssf.SetData(SSFSound.BEGIN_STEALTH, (int)(_beginStealthStrrefSpin.Value ?? 0));
            if (_beginSearchStrrefSpin != null) ssf.SetData(SSFSound.BEGIN_SEARCH, (int)(_beginSearchStrrefSpin.Value ?? 0));
            if (_beginUnlockStrrefSpin != null) ssf.SetData(SSFSound.BEGIN_UNLOCK, (int)(_beginUnlockStrrefSpin.Value ?? 0));
            if (_unlockFailedStrrefSpin != null) ssf.SetData(SSFSound.UNLOCK_FAILED, (int)(_unlockFailedStrrefSpin.Value ?? 0));
            if (_unlockSuccessStrrefSpin != null) ssf.SetData(SSFSound.UNLOCK_SUCCESS, (int)(_unlockSuccessStrrefSpin.Value ?? 0));
            if (_partySeparatedStrrefSpin != null) ssf.SetData(SSFSound.SEPARATED_FROM_PARTY, (int)(_partySeparatedStrrefSpin.Value ?? 0));
            if (_rejoinPartyStrrefSpin != null) ssf.SetData(SSFSound.REJOINED_PARTY, (int)(_rejoinPartyStrrefSpin.Value ?? 0));
            if (_poisonedStrrefSpin != null) ssf.SetData(SSFSound.POISONED, (int)(_poisonedStrrefSpin.Value ?? 0));

            byte[] data = SSFAuto.BytesSsf(ssf);
            return Tuple.Create(data, new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:212-242
        // Original: def new(self):
        public override void New()
        {
            base.New();
            // Reset all spin boxes to 0
            if (_battlecry1StrrefSpin != null) _battlecry1StrrefSpin.Value = 0;
            if (_battlecry2StrrefSpin != null) _battlecry2StrrefSpin.Value = 0;
            if (_battlecry3StrrefSpin != null) _battlecry3StrrefSpin.Value = 0;
            if (_battlecry4StrrefSpin != null) _battlecry4StrrefSpin.Value = 0;
            if (_battlecry5StrrefSpin != null) _battlecry5StrrefSpin.Value = 0;
            if (_battlecry6StrrefSpin != null) _battlecry6StrrefSpin.Value = 0;
            if (_select1StrrefSpin != null) _select1StrrefSpin.Value = 0;
            if (_select2StrrefSpin != null) _select2StrrefSpin.Value = 0;
            if (_select3StrrefSpin != null) _select3StrrefSpin.Value = 0;
            if (_attack1StrrefSpin != null) _attack1StrrefSpin.Value = 0;
            if (_attack2StrrefSpin != null) _attack2StrrefSpin.Value = 0;
            if (_attack3StrrefSpin != null) _attack3StrrefSpin.Value = 0;
            if (_pain1StrrefSpin != null) _pain1StrrefSpin.Value = 0;
            if (_pain2StrrefSpin != null) _pain2StrrefSpin.Value = 0;
            if (_lowHpStrrefSpin != null) _lowHpStrrefSpin.Value = 0;
            if (_deadStrrefSpin != null) _deadStrrefSpin.Value = 0;
            if (_criticalStrrefSpin != null) _criticalStrrefSpin.Value = 0;
            if (_immuneStrrefSpin != null) _immuneStrrefSpin.Value = 0;
            if (_layMineStrrefSpin != null) _layMineStrrefSpin.Value = 0;
            if (_disarmMineStrrefSpin != null) _disarmMineStrrefSpin.Value = 0;
            if (_beginSearchStrrefSpin != null) _beginSearchStrrefSpin.Value = 0;
            if (_beginUnlockStrrefSpin != null) _beginUnlockStrrefSpin.Value = 0;
            if (_beginStealthStrrefSpin != null) _beginStealthStrrefSpin.Value = 0;
            if (_unlockSuccessStrrefSpin != null) _unlockSuccessStrrefSpin.Value = 0;
            if (_unlockFailedStrrefSpin != null) _unlockFailedStrrefSpin.Value = 0;
            if (_partySeparatedStrrefSpin != null) _partySeparatedStrrefSpin.Value = 0;
            if (_rejoinPartyStrrefSpin != null) _rejoinPartyStrrefSpin.Value = 0;
            if (_poisonedStrrefSpin != null) _poisonedStrrefSpin.Value = 0;
            UpdateTextBoxes();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:244-296
        // Original: def update_text_boxes(self):
        private void UpdateTextBoxes()
        {
            if (_talktable == null)
            {
                return;
            }

            var stringrefs = new List<int>();
            var soundMap = new Dictionary<int, SSFSound>();

            // Collect all stringrefs and map them to sounds
            void AddStringRef(NumericUpDown spin, SSFSound sound)
            {
                if (spin != null && spin.Value.HasValue)
                {
                    int strref = (int)spin.Value.Value;
                    stringrefs.Add(strref);
                    soundMap[strref] = sound;
                }
            }

            AddStringRef(_battlecry1StrrefSpin, SSFSound.BATTLE_CRY_1);
            AddStringRef(_battlecry2StrrefSpin, SSFSound.BATTLE_CRY_2);
            AddStringRef(_battlecry3StrrefSpin, SSFSound.BATTLE_CRY_3);
            AddStringRef(_battlecry4StrrefSpin, SSFSound.BATTLE_CRY_4);
            AddStringRef(_battlecry5StrrefSpin, SSFSound.BATTLE_CRY_5);
            AddStringRef(_battlecry6StrrefSpin, SSFSound.BATTLE_CRY_6);
            AddStringRef(_select1StrrefSpin, SSFSound.SELECT_1);
            AddStringRef(_select2StrrefSpin, SSFSound.SELECT_2);
            AddStringRef(_select3StrrefSpin, SSFSound.SELECT_3);
            AddStringRef(_attack1StrrefSpin, SSFSound.ATTACK_GRUNT_1);
            AddStringRef(_attack2StrrefSpin, SSFSound.ATTACK_GRUNT_2);
            AddStringRef(_attack3StrrefSpin, SSFSound.ATTACK_GRUNT_3);
            AddStringRef(_pain1StrrefSpin, SSFSound.PAIN_GRUNT_1);
            AddStringRef(_pain2StrrefSpin, SSFSound.PAIN_GRUNT_2);
            AddStringRef(_lowHpStrrefSpin, SSFSound.LOW_HEALTH);
            AddStringRef(_deadStrrefSpin, SSFSound.DEAD);
            AddStringRef(_criticalStrrefSpin, SSFSound.CRITICAL_HIT);
            AddStringRef(_immuneStrrefSpin, SSFSound.TARGET_IMMUNE);
            AddStringRef(_layMineStrrefSpin, SSFSound.LAY_MINE);
            AddStringRef(_disarmMineStrrefSpin, SSFSound.DISARM_MINE);
            AddStringRef(_beginStealthStrrefSpin, SSFSound.BEGIN_STEALTH);
            AddStringRef(_beginSearchStrrefSpin, SSFSound.BEGIN_SEARCH);
            AddStringRef(_beginUnlockStrrefSpin, SSFSound.BEGIN_UNLOCK);
            AddStringRef(_unlockSuccessStrrefSpin, SSFSound.UNLOCK_SUCCESS);
            AddStringRef(_unlockFailedStrrefSpin, SSFSound.UNLOCK_FAILED);
            AddStringRef(_partySeparatedStrrefSpin, SSFSound.SEPARATED_FROM_PARTY);
            AddStringRef(_rejoinPartyStrrefSpin, SSFSound.REJOINED_PARTY);
            AddStringRef(_poisonedStrrefSpin, SSFSound.POISONED);

            // Batch lookup from talktable
            var results = _talktable.Batch(stringrefs);

            // Update text boxes
            foreach (var kvp in results)
            {
                int strref = kvp.Key;
                var result = kvp.Value;
                if (soundMap.TryGetValue(strref, out SSFSound sound) && _soundTextPairs.TryGetValue(sound, out var pair))
                {
                    pair.soundEdit.Text = result.Sound?.ToString() ?? "";
                    pair.textEdit.Text = result.Text ?? "";
                }
            }
        }

        private void UpdateTalktableLabel()
        {
            if (_talktableLabel != null)
            {
                _talktableLabel.Text = _talktable != null ? $"Using: {System.IO.Path.GetFileName(_talktable.Path)}" : "Using installation talktable";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/ssf.py:298-302
        // Original: def select_talk_table(self):
        private async Task SelectTalkTable()
        {
            // Get the top-level window for file dialog
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
            {
                return;
            }

            // Create file picker options for TLK file selection
            var options = new FilePickerOpenOptions
            {
                Title = "Select Talk Table (TLK) File",
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Talk Table Files")
                    {
                        Patterns = new[] { "*.tlk" },
                        MimeTypes = new[] { "application/octet-stream" }
                    },
                    new FilePickerFileType("All Files")
                    {
                        Patterns = new[] { "*.*" }
                    }
                }
            };

            try
            {
                // Show file dialog
                var files = await topLevel.StorageProvider.OpenFilePickerAsync(options);
                if (files == null || files.Count == 0)
                {
                    return;
                }

                // Load the selected talktable
                string tlkPath = files[0].Path.LocalPath;
                if (File.Exists(tlkPath))
                {
                    _talktable = new TalkTable(tlkPath);
                    UpdateTalktableLabel();
                    UpdateTextBoxes();
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash
                System.Console.WriteLine($"Failed to select talktable: {ex.Message}");
            }
        }

        public override void SaveAs()
        {
            Save();
        }
    }
}
