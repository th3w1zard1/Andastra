using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BioWare.NET.Extract.SaveData;
using BioWare.NET.Resource;
using HolocronToolset.Data;

namespace HolocronToolset.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:58
    // Original: class SaveGameEditor(Editor):
    public class SAVEditor : Editor
    {
        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:96-102
        // Original: self._save_folder: SaveFolderEntry | None = None
        private SaveFolderEntry _saveFolder;
        private SaveInfo _saveInfo;
        private PartyTable _partyTable;
        private GlobalVars _globalVars;
        private SaveNestedCapsule _nestedCapsule;

        // UI Controls - Save Info
        private TextBox _lineEditSaveName;
        private TextBox _lineEditAreaName;
        private TextBox _lineEditLastModule;
        private NumericUpDown _spinBoxTimePlayed;
        private TextBox _lineEditPCName;
        private TextBox _lineEditPortrait0;
        private TextBox _lineEditPortrait1;
        private TextBox _lineEditPortrait2;

        // UI Controls - Party Table
        private NumericUpDown _spinBoxGold;
        private NumericUpDown _spinBoxXPPool;
        private NumericUpDown _spinBoxComponents;
        private NumericUpDown _spinBoxChemicals;
        private ListBox _listWidgetPartyMembers;

        // UI Controls - Global Vars
        private DataGrid _tableWidgetBooleans;
        private DataGrid _tableWidgetNumbers;
        private DataGrid _tableWidgetStrings;
        private DataGrid _tableWidgetLocations;

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:71-122
        // Original: def __init__(self, parent, installation):
        public SAVEditor(Window parent = null, HTInstallation installation = null)
            : base(parent, "Save Game Editor", "savegame",
                new[] { ResourceType.SAV },
                new[] { ResourceType.SAV },
                installation)
        {
            InitializeComponent();
            SetupUI();
            Width = 1200;
            Height = 800;
            New();
        }

        private void InitializeComponent()
        {
            if (!TryLoadXaml())
            {
                SetupUI();
            }
            else
            {
                // Try to find controls from XAML
                _lineEditSaveName = this.FindControl<TextBox>("lineEditSaveName");
                _lineEditAreaName = this.FindControl<TextBox>("lineEditAreaName");
                _lineEditLastModule = this.FindControl<TextBox>("lineEditLastModule");
                _spinBoxTimePlayed = this.FindControl<NumericUpDown>("spinBoxTimePlayed");
                _lineEditPCName = this.FindControl<TextBox>("lineEditPCName");
                _lineEditPortrait0 = this.FindControl<TextBox>("lineEditPortrait0");
                _lineEditPortrait1 = this.FindControl<TextBox>("lineEditPortrait1");
                _lineEditPortrait2 = this.FindControl<TextBox>("lineEditPortrait2");
                _spinBoxGold = this.FindControl<NumericUpDown>("spinBoxGold");
                _spinBoxXPPool = this.FindControl<NumericUpDown>("spinBoxXPPool");
                _spinBoxComponents = this.FindControl<NumericUpDown>("spinBoxComponents");
                _spinBoxChemicals = this.FindControl<NumericUpDown>("spinBoxChemicals");
                _listWidgetPartyMembers = this.FindControl<ListBox>("listWidgetPartyMembers");
                _tableWidgetBooleans = this.FindControl<DataGrid>("tableWidgetBooleans");
                _tableWidgetNumbers = this.FindControl<DataGrid>("tableWidgetNumbers");
                _tableWidgetStrings = this.FindControl<DataGrid>("tableWidgetStrings");
                _tableWidgetLocations = this.FindControl<DataGrid>("tableWidgetLocations");
            }
        }

        private void SetupUI()
        {
            var panel = new StackPanel();
            Content = panel;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:189-259
        // Original: def load(self, filepath, resref, restype, data):
        public override void Load(string filepath, string resref, ResourceType restype, byte[] data)
        {
            base.Load(filepath, resref, restype, data);
            LoadSaveGame(filepath);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:189-259
        // Original: def load(self, filepath, resref, restype, data):
        private void LoadSaveGame(string filepath)
        {
            try
            {
                // Matching PyKotor implementation: Determine if this is a folder or a file
                // Original: path = Path(filepath); if path.is_file() and path.name.upper() == "SAVEGAME.SAV": save_folder = path.parent; else: save_folder = path
                string saveFolder;
                if (File.Exists(filepath) && Path.GetFileName(filepath).ToUpperInvariant() == "SAVEGAME.SAV")
                {
                    saveFolder = Path.GetDirectoryName(filepath);
                }
                else
                {
                    saveFolder = filepath;
                }

                // Matching PyKotor implementation: self._save_folder = SaveFolderEntry(str(save_folder)); self._save_folder.load()
                _saveFolder = new SaveFolderEntry(saveFolder);
                _saveFolder.Load();

                // Matching PyKotor implementation: Extract individual components
                // Original: self._save_info = self._save_folder.save_info
                _saveInfo = _saveFolder.SaveInfo;
                _partyTable = _saveFolder.PartyTable;
                _globalVars = _saveFolder.GlobalVars;
                _nestedCapsule = _saveFolder.NestedCapsule;

                // Matching PyKotor implementation: Populate UI
                // Original: self.populate_save_info(); self.populate_party_table(); self.populate_global_vars()
                PopulateSaveInfo();
                PopulatePartyTable();
                PopulateGlobalVars();
            }
            catch (Exception)
            {
                // Matching PyKotor implementation: Error handling
                New();
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:261-269
        // Original: def build(self) -> tuple[bytes, bytes]:
        public override Tuple<byte[], byte[]> Build()
        {
            // Matching PyKotor implementation: Save games are folder-based, so we return empty
            // Original: return b"", b""
            return Tuple.Create(new byte[0], new byte[0]);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:306-324
        // Original: def new(self):
        public override void New()
        {
            base.New();
            // Matching PyKotor implementation: Clear save data structures
            // Original: self._save_folder = None; self._save_info = None; etc.
            _saveFolder = null;
            _saveInfo = null;
            _partyTable = null;
            _globalVars = null;
            _nestedCapsule = null;

            // Matching PyKotor implementation: Clear UI
            // Original: self.clear_save_info(); self.clear_party_table(); self.clear_global_vars()
            ClearSaveInfo();
            ClearPartyTable();
            ClearGlobalVars();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:327-339
        // Original: def populate_save_info(self):
        public void PopulateSaveInfo()
        {
            if (_saveInfo == null)
            {
                return;
            }

            if (_lineEditSaveName != null)
            {
                _lineEditSaveName.Text = _saveInfo.SavegameName ?? "";
            }
            if (_lineEditAreaName != null)
            {
                _lineEditAreaName.Text = _saveInfo.AreaName ?? "";
            }
            if (_lineEditLastModule != null)
            {
                _lineEditLastModule.Text = _saveInfo.LastModule ?? "";
            }
            if (_spinBoxTimePlayed != null)
            {
                _spinBoxTimePlayed.Value = _saveInfo.TimePlayed;
            }
            if (_lineEditPCName != null)
            {
                _lineEditPCName.Text = _saveInfo.PcName ?? "";
            }
            if (_lineEditPortrait0 != null)
            {
                _lineEditPortrait0.Text = _saveInfo.Portrait0?.ToString() ?? "";
            }
            if (_lineEditPortrait1 != null)
            {
                _lineEditPortrait1.Text = _saveInfo.Portrait1?.ToString() ?? "";
            }
            if (_lineEditPortrait2 != null)
            {
                _lineEditPortrait2.Text = _saveInfo.Portrait2?.ToString() ?? "";
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:355-364
        // Original: def clear_save_info(self):
        private void ClearSaveInfo()
        {
            if (_lineEditSaveName != null) _lineEditSaveName.Text = "";
            if (_lineEditAreaName != null) _lineEditAreaName.Text = "";
            if (_lineEditLastModule != null) _lineEditLastModule.Text = "";
            if (_spinBoxTimePlayed != null) _spinBoxTimePlayed.Value = 0;
            if (_lineEditPCName != null) _lineEditPCName.Text = "";
            if (_lineEditPortrait0 != null) _lineEditPortrait0.Text = "";
            if (_lineEditPortrait1 != null) _lineEditPortrait1.Text = "";
            if (_lineEditPortrait2 != null) _lineEditPortrait2.Text = "";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:382-427
        // Original: def populate_party_table(self):
        public void PopulatePartyTable()
        {
            if (_partyTable == null)
            {
                return;
            }

            // Matching PyKotor implementation: Resources
            // Original: self.ui.spinBoxGold.setValue(self._party_table.pt_gold)
            if (_spinBoxGold != null)
            {
                _spinBoxGold.Value = _partyTable.Gold;
            }
            if (_spinBoxXPPool != null)
            {
                _spinBoxXPPool.Value = _partyTable.XpPool;
            }
            if (_spinBoxComponents != null)
            {
                _spinBoxComponents.Value = _partyTable.ItemComponents;
            }
            if (_spinBoxChemicals != null)
            {
                _spinBoxChemicals.Value = _partyTable.ItemChemicals;
            }

            // Matching PyKotor implementation: Party members
            // Original: sorted_members = sorted(self._party_table.pt_members, key=lambda m: (not m.is_leader, m.index if m.index >= 0 else 999))
            if (_listWidgetPartyMembers != null)
            {
                _listWidgetPartyMembers.Items.Clear();
                var sortedMembers = _partyTable.Members.OrderBy(m => !m.IsLeader).ThenBy(m => m.Index >= 0 ? m.Index : 999).ToList();
                foreach (var member in sortedMembers)
                {
                    string charName = GetPartyMemberName(member);
                    string leaderText = member.IsLeader ? " [Leader]" : "";
                    string displayText = charName + leaderText;
                    _listWidgetPartyMembers.Items.Add(displayText);
                }
            }
        }

        // Matching PyKotor implementation: Helper to get party member name
        private string GetPartyMemberName(PartyMemberEntry member)
        {
            // TODO:  Simplified - full implementation would resolve from nested capsule
            if (member.Index == -1)
            {
                return "PC";
            }
            return $"Member #{member.Index}";
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:858-872
        // Original: def clear_party_table(self):
        private void ClearPartyTable()
        {
            if (_spinBoxGold != null) _spinBoxGold.Value = 0;
            if (_spinBoxXPPool != null) _spinBoxXPPool.Value = 0;
            if (_spinBoxComponents != null) _spinBoxComponents.Value = 0;
            if (_spinBoxChemicals != null) _spinBoxChemicals.Value = 0;
            if (_listWidgetPartyMembers != null) _listWidgetPartyMembers.Items.Clear();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:873-993
        // Original: def populate_global_vars(self):
        public void PopulateGlobalVars()
        {
            if (_globalVars == null)
            {
                return;
            }

            // Matching PyKotor implementation: Populate boolean table
            // Original: for i, (name, value) in enumerate(self._global_vars.global_booleans.items()):
            if (_tableWidgetBooleans != null)
            {
                var boolList = new List<object>();
                foreach (var pair in _globalVars.GlobalBools)
                {
                    boolList.Add(new { Name = pair.Item1, Value = pair.Item2 });
                }
                _tableWidgetBooleans.ItemsSource = boolList;
            }

            // Matching PyKotor implementation: Populate number table
            if (_tableWidgetNumbers != null)
            {
                var numList = new List<object>();
                foreach (var pair in _globalVars.GlobalNumbers)
                {
                    numList.Add(new { Name = pair.Item1, Value = pair.Item2 });
                }
                _tableWidgetNumbers.ItemsSource = numList;
            }

            // Matching PyKotor implementation: Populate string table
            if (_tableWidgetStrings != null)
            {
                var strList = new List<object>();
                foreach (var pair in _globalVars.GlobalStrings)
                {
                    strList.Add(new { Name = pair.Item1, Value = pair.Item2 });
                }
                _tableWidgetStrings.ItemsSource = strList;
            }

            // Matching PyKotor implementation: Populate location table
            if (_tableWidgetLocations != null)
            {
                var locList = new List<object>();
                foreach (var pair in _globalVars.GlobalLocations)
                {
                    locList.Add(new { Name = pair.Item1, Value = pair.Item2 });
                }
                _tableWidgetLocations.ItemsSource = locList;
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/gui/editors/savegame.py:1042-1055
        // Original: def clear_global_vars(self):
        private void ClearGlobalVars()
        {
            if (_tableWidgetBooleans != null) _tableWidgetBooleans.ItemsSource = null;
            if (_tableWidgetNumbers != null) _tableWidgetNumbers.ItemsSource = null;
            if (_tableWidgetStrings != null) _tableWidgetStrings.ItemsSource = null;
            if (_tableWidgetLocations != null) _tableWidgetLocations.ItemsSource = null;
        }

        // Expose private fields for testing (matching Python's _save_info, _party_table, etc.)
        public SaveInfo SaveInfo => _saveInfo;
        public PartyTable PartyTable => _partyTable;
        public GlobalVars GlobalVars => _globalVars;
        public SaveNestedCapsule NestedCapsule => _nestedCapsule;

        public override void SaveAs()
        {
            Save();
        }
    }
}
