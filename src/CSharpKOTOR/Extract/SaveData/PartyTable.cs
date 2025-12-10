using System;
using System.Collections.Generic;
using System.IO;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;

namespace CSharpKOTOR.Extract.SaveData
{
    // Supporting entries matching PyKotor savedata.py
    public class JournalEntry
    {
        public int Date { get; set; } = -1;
        public string PlotId { get; set; } = string.Empty;
        public int State { get; set; } = -1;
        public int Time { get; set; } = -1;
    }

    public class AvailableNPCEntry
    {
        public bool NpcAvailable { get; set; }
        public bool NpcSelected { get; set; }
    }

    public class PartyMemberEntry
    {
        public bool IsLeader { get; set; }
        public int Index { get; set; } = -1;
    }

    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/extract/savedata.py:582-1020
    // Original: class PartyTable
    public class PartyTable
    {
        public List<PartyMemberEntry> Members { get; } = new List<PartyMemberEntry>();
        public List<AvailableNPCEntry> AvailableNpcs { get; } = new List<AvailableNPCEntry>();
        public int ControlledNpc { get; set; } = -1;
        public int AiState { get; set; }
        public int FollowState { get; set; }
        public bool SoloMode { get; set; }

        public int Gold { get; set; }
        public int XpPool { get; set; }
        public int TimePlayed { get; set; } = -1;

        public List<JournalEntry> JournalEntries { get; } = new List<JournalEntry>();
        public int JournalSortOrder { get; set; }

        public GFFList PazaakCards { get; set; } = new GFFList();
        public GFFList PazaakDecks { get; set; } = new GFFList();

        public int LastGuiPanel { get; set; }
        public GFFList FeedbackMessages { get; set; } = new GFFList();
        public GFFList DialogMessages { get; set; } = new GFFList();
        public byte[] TutorialWindowsShown { get; set; } = Array.Empty<byte>();

        public bool CheatUsed { get; set; }
        public GFFList CostMultiplierList { get; set; } = new GFFList();

        // K2-specific
        public List<int> Influence { get; } = new List<int>();
        public int ItemComponents { get; set; }
        public int ItemChemicals { get; set; }
        public string PcName { get; set; } = string.Empty;

        // Additional fields preserved verbatim
        public Dictionary<string, Tuple<GFFFieldType, object>> AdditionalFields { get; private set; } = new Dictionary<string, Tuple<GFFFieldType, object>>();

        private readonly string _partyTablePath;

        public PartyTable(string folderPath)
        {
            _partyTablePath = Path.Combine(folderPath, "partytable.res");
        }

        public void Load()
        {
            byte[] data = File.ReadAllBytes(_partyTablePath);
            GFF gff = GFF.FromBytes(data);
            GFFStruct root = gff.Root;

            var processed = new HashSet<string>();

            T Acquire<T>(string label, T def)
            {
                if (root.Exists(label)) processed.Add(label);
                return root.Acquire(label, def);
            }

            // Party composition
            int _ = Acquire("PT_NUM_MEMBERS", 0); // ignored; derived from list
            ControlledNpc = Acquire("PT_CONTROLLED_NPC", -1);
            AiState = Acquire("PT_AISTATE", 0);
            FollowState = Acquire("PT_FOLLOWSTATE", 0);
            SoloMode = Acquire("PT_SOLOMODE", 0) != 0;

            Members.Clear();
            if (root.Exists("PT_MEMBERS"))
            {
                processed.Add("PT_MEMBERS");
                var list = root.GetList("PT_MEMBERS");
                if (list != null)
                {
                    foreach (var s in list)
                    {
                        var entry = new PartyMemberEntry
                        {
                            IsLeader = s.Acquire("PT_IS_LEADER", 0) != 0,
                            Index = s.Acquire("PT_MEMBER_ID", -1)
                        };
                        Members.Add(entry);
                    }
                }
            }

            AvailableNpcs.Clear();
            if (root.Exists("PT_AVAIL_NPCS"))
            {
                processed.Add("PT_AVAIL_NPCS");
                var list = root.GetList("PT_AVAIL_NPCS");
                if (list != null)
                {
                    foreach (var s in list)
                    {
                        var entry = new AvailableNPCEntry
                        {
                            NpcAvailable = s.Acquire("PT_NPC_AVAIL", 0) != 0,
                            NpcSelected = s.Acquire("PT_NPC_SELECT", 0) != 0
                        };
                        AvailableNpcs.Add(entry);
                    }
                }
            }

            // Resources
            Gold = Acquire("PT_GOLD", 0);
            XpPool = Acquire("PT_XP_POOL", 0);
            TimePlayed = Acquire("PT_PLAYEDSECONDS", -1);

            // Journal
            JournalEntries.Clear();
            if (root.Exists("JNL_Entries"))
            {
                processed.Add("JNL_Entries");
                var list = root.GetList("JNL_Entries");
                if (list != null)
                {
                    foreach (var s in list)
                    {
                        var entry = new JournalEntry
                        {
                            PlotId = s.Acquire("JNL_PlotID", string.Empty),
                            State = s.Acquire("JNL_State", -1),
                            Date = s.Acquire("JNL_Date", -1),
                            Time = s.Acquire("JNL_Time", -1)
                        };
                        JournalEntries.Add(entry);
                    }
                }
            }
            JournalSortOrder = Acquire("JNL_SORT_ORDER", 0);

            // Pazaak
            PazaakCards = Acquire("PT_PAZAAKCARDS", new GFFList());
            PazaakDecks = Acquire("PT_PAZAAKDECKS", new GFFList());

            // UI / messages
            LastGuiPanel = Acquire("PT_LAST_GUI_PNL", 0);
            FeedbackMessages = Acquire("PT_FB_MSG_LIST", new GFFList());
            DialogMessages = Acquire("PT_DLG_MSG_LIST", new GFFList());
            TutorialWindowsShown = Acquire("PT_TUT_WND_SHOWN", Array.Empty<byte>());

            // Cheats
            CheatUsed = Acquire("PT_CHEAT_USED", 0) != 0;

            // Economy
            CostMultiplierList = Acquire("PT_COST_MULT_LIS", new GFFList());

            // K2 fields
            ItemComponents = Acquire("PT_ITEM_COMPONEN", 0);
            ItemChemicals = Acquire("PT_ITEM_CHEMICAL", 0);
            PcName = Acquire("PT_PCNAME", string.Empty);

            Influence.Clear();
            if (root.Exists("PT_INFLUENCE"))
            {
                processed.Add("PT_INFLUENCE");
                var list = root.GetList("PT_INFLUENCE");
                if (list != null)
                {
                    foreach (var s in list)
                    {
                        Influence.Add(s.Acquire("PT_NPC_INFLUENCE", 0));
                    }
                }
            }

            // Additional fields
            AdditionalFields = new Dictionary<string, Tuple<GFFFieldType, object>>();
            foreach (var (label, fieldType, value) in root)
            {
                if (!processed.Contains(label))
                {
                    AdditionalFields[label] = Tuple.Create(fieldType, value);
                }
            }
        }

        public void Save()
        {
            GFF gff = new GFF(GFFContent.PT);
            GFFStruct root = gff.Root;

            // Party composition
            root.SetInt32("PT_NUM_MEMBERS", Members.Count);
            root.SetInt32("PT_CONTROLLED_NPC", ControlledNpc);
            root.SetInt32("PT_AISTATE", AiState);
            root.SetInt32("PT_FOLLOWSTATE", FollowState);
            root.SetUInt8("PT_SOLOMODE", (byte)(SoloMode ? 1 : 0));

            if (Members.Count > 0)
            {
                var list = new GFFList();
                foreach (var m in Members)
                {
                    var s = list.Add();
                    s.SetUInt8("PT_IS_LEADER", (byte)(m.IsLeader ? 1 : 0));
                    s.SetInt32("PT_MEMBER_ID", m.Index);
                }
                root.SetList("PT_MEMBERS", list);
            }

            if (AvailableNpcs.Count > 0)
            {
                var list = new GFFList();
                foreach (var npc in AvailableNpcs)
                {
                    var s = list.Add();
                    s.SetUInt8("PT_NPC_AVAIL", (byte)(npc.NpcAvailable ? 1 : 0));
                    s.SetUInt8("PT_NPC_SELECT", (byte)(npc.NpcSelected ? 1 : 0));
                }
                root.SetList("PT_AVAIL_NPCS", list);
            }

            // Resources
            root.SetInt32("PT_GOLD", Gold);
            root.SetInt32("PT_XP_POOL", XpPool);
            root.SetInt32("PT_PLAYEDSECONDS", TimePlayed);

            // Journal
            if (JournalEntries.Count > 0)
            {
                var list = new GFFList();
                foreach (var j in JournalEntries)
                {
                    var s = list.Add();
                    s.SetString("JNL_PlotID", j.PlotId);
                    s.SetInt32("JNL_State", j.State);
                    s.SetInt32("JNL_Date", j.Date);
                    s.SetInt32("JNL_Time", j.Time);
                }
                root.SetList("JNL_Entries", list);
            }
            root.SetInt32("JNL_SORT_ORDER", JournalSortOrder);

            // Pazaak
            if (PazaakCards.Count > 0) root.SetList("PT_PAZAAKCARDS", PazaakCards);
            if (PazaakDecks.Count > 0) root.SetList("PT_PAZAAKDECKS", PazaakDecks);

            // UI / messages
            root.SetInt32("PT_LAST_GUI_PNL", LastGuiPanel);
            if (FeedbackMessages.Count > 0) root.SetList("PT_FB_MSG_LIST", FeedbackMessages);
            if (DialogMessages.Count > 0) root.SetList("PT_DLG_MSG_LIST", DialogMessages);
            if (TutorialWindowsShown != null && TutorialWindowsShown.Length > 0) root.SetBinary("PT_TUT_WND_SHOWN", TutorialWindowsShown);

            // Cheats
            root.SetUInt8("PT_CHEAT_USED", (byte)(CheatUsed ? 1 : 0));

            // Economy
            if (CostMultiplierList.Count > 0) root.SetList("PT_COST_MULT_LIS", CostMultiplierList);

            // K2 fields
            root.SetInt32("PT_ITEM_COMPONEN", ItemComponents);
            root.SetInt32("PT_ITEM_CHEMICAL", ItemChemicals);
            if (!string.IsNullOrEmpty(PcName)) root.SetString("PT_PCNAME", PcName);

            if (Influence.Count > 0)
            {
                var list = new GFFList();
                foreach (var val in Influence)
                {
                    var s = list.Add();
                    s.SetInt32("PT_NPC_INFLUENCE", val);
                }
                root.SetList("PT_INFLUENCE", list);
            }

            // Additional fields
            foreach (var kvp in AdditionalFields)
            {
                string label = kvp.Key;
                GFFFieldType type = kvp.Value.Item1;
                object value = kvp.Value.Item2;
                switch (type)
                {
                    case GFFFieldType.UInt8: root.SetUInt8(label, Convert.ToByte(value)); break;
                    case GFFFieldType.Int8: root.SetInt8(label, Convert.ToSByte(value)); break;
                    case GFFFieldType.UInt16: root.SetUInt16(label, Convert.ToUInt16(value)); break;
                    case GFFFieldType.Int16: root.SetInt16(label, Convert.ToInt16(value)); break;
                    case GFFFieldType.UInt32: root.SetUInt32(label, Convert.ToUInt32(value)); break;
                    case GFFFieldType.Int32: root.SetInt32(label, Convert.ToInt32(value)); break;
                    case GFFFieldType.UInt64: root.SetUInt64(label, Convert.ToUInt64(value)); break;
                    case GFFFieldType.Int64: root.SetInt64(label, Convert.ToInt64(value)); break;
                    case GFFFieldType.Single: root.SetSingle(label, Convert.ToSingle(value)); break;
                    case GFFFieldType.Double: root.SetDouble(label, Convert.ToDouble(value)); break;
                    case GFFFieldType.String: root.SetString(label, value?.ToString() ?? string.Empty); break;
                    case GFFFieldType.ResRef: root.SetResRef(label, value as ResRef ?? ResRef.FromBlank()); break;
                    case GFFFieldType.LocalizedString: root.SetLocString(label, value as LocalizedString ?? LocalizedString.FromInvalid()); break;
                    case GFFFieldType.Binary: root.SetBinary(label, value as byte[] ?? Array.Empty<byte>()); break;
                    case GFFFieldType.Vector3: root.SetVector3(label, value is Vector3 v3 ? v3 : new Vector3()); break;
                    case GFFFieldType.Vector4: root.SetVector4(label, value is Vector4 v4 ? v4 : new Vector4()); break;
                    case GFFFieldType.Struct: root.SetStruct(label, value as GFFStruct ?? new GFFStruct()); break;
                    case GFFFieldType.List: root.SetList(label, value as GFFList ?? new GFFList()); break;
                    default: break;
                }
            }

            byte[] bytes = new GFFBinaryWriter(gff).Write();
            File.WriteAllBytes(_partyTablePath, bytes);
        }
    }
}

