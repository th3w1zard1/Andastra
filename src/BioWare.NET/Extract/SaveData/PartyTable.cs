using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;

namespace BioWare.NET.Extract.SaveData
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
        /// <summary>
        /// Converts a Runtime PartyState to Parsing PartyTable format.
        /// </summary>
        public static PartyTable FromRuntimePartyState(object runtimeState)
        {
            if (runtimeState == null) return new PartyTable("");

            var runtimeType = runtimeState.GetType();
            var partyTable = new PartyTable("");

            // Extract basic properties using reflection
            SetPropertyIfExists(runtimeState, runtimeType, "Gold", partyTable, "Gold");
            SetPropertyIfExists(runtimeState, runtimeType, "ExperiencePoints", partyTable, "XpPool");
            SetPropertyIfExists(runtimeState, runtimeType, "ControlledNPC", partyTable, "ControlledNpc");
            SetPropertyIfExists(runtimeState, runtimeType, "SoloMode", partyTable, "SoloMode");
            SetPropertyIfExists(runtimeState, runtimeType, "CheatUsed", partyTable, "CheatUsed");
            SetPropertyIfExists(runtimeState, runtimeType, "AIState", partyTable, "AiState");
            SetPropertyIfExists(runtimeState, runtimeType, "FollowState", partyTable, "FollowState");
            SetPropertyIfExists(runtimeState, runtimeType, "ItemComponent", partyTable, "ItemComponents");
            SetPropertyIfExists(runtimeState, runtimeType, "ItemChemical", partyTable, "ItemChemicals");

            // Extract play time
            var playTimeProp = runtimeType.GetProperty("PlayTime");
            if (playTimeProp != null)
            {
                var playTime = playTimeProp.GetValue(runtimeState);
                if (playTime is TimeSpan ts)
                {
                    partyTable.TimePlayed = (int)ts.TotalSeconds;
                }
            }

            // Extract selected party
            var selectedPartyProp = runtimeType.GetProperty("SelectedParty");
            if (selectedPartyProp != null)
            {
                var selectedParty = selectedPartyProp.GetValue(runtimeState) as System.Collections.IList;
                if (selectedParty != null)
                {
                    for (int i = 0; i < selectedParty.Count; i++)
                    {
                        var memberEntry = new PartyMemberEntry { Index = i };
                        partyTable.Members.Add(memberEntry);
                    }
                }
            }

            // Extract available members
            var availableMembersProp = runtimeType.GetProperty("AvailableMembers");
            if (availableMembersProp != null)
            {
                var availableMembers = availableMembersProp.GetValue(runtimeState) as System.Collections.IDictionary;
                if (availableMembers != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in availableMembers)
                    {
                        var memberState = entry.Value;
                        if (memberState != null)
                        {
                            var memberType = memberState.GetType();
                            var isAvailableProp = memberType.GetProperty("IsAvailable");
                            var isSelectableProp = memberType.GetProperty("IsSelectable");

                            if (isAvailableProp != null && isSelectableProp != null)
                            {
                                var npcEntry = new AvailableNPCEntry
                                {
                                    NpcAvailable = (bool)isAvailableProp.GetValue(memberState),
                                    NpcSelected = (bool)isSelectableProp.GetValue(memberState)
                                };
                                partyTable.AvailableNpcs.Add(npcEntry);
                            }
                        }
                    }
                }
            }

            // Extract influence
            var influenceProp = runtimeType.GetProperty("Influence");
            if (influenceProp != null)
            {
                var influence = influenceProp.GetValue(runtimeState) as System.Collections.IList;
                if (influence != null)
                {
                    foreach (var val in influence)
                    {
                        partyTable.Influence.Add((int)val);
                    }
                }
            }

            return partyTable;
        }

        /// <summary>
        /// Converts Parsing PartyTable to Runtime PartyState format.
        /// </summary>
        /// <param name="partyTable">The party table to convert.</param>
        /// <param name="runtimeType">The runtime type to create an instance of.</param>
        /// <param name="memberIdToResRef">Optional function to convert member ID (float) to ResRef (string). If null, uses fallback mapping.</param>
        public static object ToRuntimePartyState(PartyTable partyTable, Type runtimeType, Func<float, string> memberIdToResRef = null)
        {
            if (partyTable == null || runtimeType == null) return null;

            var runtimeState = Activator.CreateInstance(runtimeType);

            // Set basic properties
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "Gold", "Gold");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "XpPool", "ExperiencePoints");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "ControlledNpc", "ControlledNPC");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "SoloMode", "SoloMode");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "CheatUsed", "CheatUsed");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "AiState", "AIState");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "FollowState", "FollowState");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "ItemComponents", "ItemComponent");
            SetRuntimePropertyIfExists(runtimeState, runtimeType, partyTable, "ItemChemicals", "ItemChemical");

            // Set play time
            var playTimeProp = runtimeType.GetProperty("PlayTime");
            if (playTimeProp != null)
            {
                playTimeProp.SetValue(runtimeState, TimeSpan.FromSeconds(partyTable.TimePlayed));
            }

            // Set selected party - convert member indices to ResRefs
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): [TODO: Name this function] @ K1(0x0057dcd0, TSL: TODO: Find this address, NWN:EE: TODO: Find this address) (LoadPartyTable function) reads PT_MEMBER_ID and converts to ResRef
            // Original implementation: partytable.2da row index = member ID (0-11 for K2, 0-8 for K1)
            // Row label in partytable.2da is the NPC ResRef (e.g., "bastila" for K1, "atton" for K2)
            var selectedPartyProp = runtimeType.GetProperty("SelectedParty");
            if (selectedPartyProp != null)
            {
                var selectedParty = new List<string>();
                foreach (var member in partyTable.Members)
                {
                    // Convert member index back to ResRef using lookup function or fallback
                    string memberResRef = GetResRefFromMemberId((float)member.Index, memberIdToResRef);
                    if (!string.IsNullOrEmpty(memberResRef))
                    {
                        selectedParty.Add(memberResRef);
                    }
                }
                selectedPartyProp.SetValue(runtimeState, selectedParty);
            }

            // Set available members - convert indices to ResRefs
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): PT_AVAIL_NPCS list index corresponds to member ID (0-11 for K2, 0-8 for K1)
            var availableMembersProp = runtimeType.GetProperty("AvailableMembers");
            if (availableMembersProp != null)
            {
                var availableMembers = new Dictionary<string, object>();
                var partyMemberStateType = runtimeType.Assembly.GetType("BioWare.NET.Extract.SaveData.PartyMemberState");
                if (partyMemberStateType != null)
                {
                    for (int i = 0; i < partyTable.AvailableNpcs.Count; i++)
                    {
                        var npcEntry = partyTable.AvailableNpcs[i];
                        var memberState = Activator.CreateInstance(partyMemberStateType);
                        var isAvailableProp = partyMemberStateType.GetProperty("IsAvailable");
                        var isSelectableProp = partyMemberStateType.GetProperty("IsSelectable");

                        if (isAvailableProp != null) isAvailableProp.SetValue(memberState, npcEntry.NpcAvailable);
                        if (isSelectableProp != null) isSelectableProp.SetValue(memberState, npcEntry.NpcSelected);

                        // Convert member index to ResRef using lookup function or fallback
                        string memberResRef = GetResRefFromMemberId((float)i, memberIdToResRef);
                        if (!string.IsNullOrEmpty(memberResRef))
                        {
                            availableMembers[memberResRef] = memberState;
                        }
                    }
                }
                availableMembersProp.SetValue(runtimeState, availableMembers);
            }

            // Set influence
            var influenceProp = runtimeType.GetProperty("Influence");
            if (influenceProp != null)
            {
                influenceProp.SetValue(runtimeState, partyTable.Influence);
            }

            return runtimeState;
        }

        private static void SetPropertyIfExists(object source, Type sourceType, string sourceProp, object target, string targetProp)
        {
            var prop = sourceType.GetProperty(sourceProp);
            if (prop != null)
            {
                var value = prop.GetValue(source);
                var targetType = target.GetType();
                var targetPropInfo = targetType.GetProperty(targetProp);
                if (targetPropInfo != null && targetPropInfo.CanWrite)
                {
                    targetPropInfo.SetValue(target, value);
                }
            }
        }

        private static void SetRuntimePropertyIfExists(object target, Type targetType, object source, string sourceProp, string targetProp)
        {
            var sourceType = source.GetType();
            var prop = sourceType.GetProperty(sourceProp);
            if (prop != null)
            {
                var value = prop.GetValue(source);
                var targetPropInfo = targetType.GetProperty(targetProp);
                if (targetPropInfo != null && targetPropInfo.CanWrite)
                {
                    targetPropInfo.SetValue(target, value);
                }
            }
        }

        /// <summary>
        /// Converts a member ID to a ResRef using the provided lookup function or fallback mapping.
        /// </summary>
        /// <remarks>
        /// Member IDs: -1 = Player, 0-8 = NPC slots (K1), 0-11 = NPC slots (K2)
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): [TODO: Name this function] @ K1(0x0057dcd0, TSL: TODO: Find this address, NWN:EE: TODO: Find this address) (LoadPartyTable function) reads PT_MEMBER_ID and converts to ResRef
        /// Original implementation: partytable.2da row index = member ID (0-11 for K2, 0-8 for K1)
        /// Row label in partytable.2da is the NPC ResRef (e.g., "bastila" for K1, "atton" for K2)
        /// Located via string reference: "PARTYTABLE" @ K1(0x007c1910, TSL: TODO: Find this address, NWN:EE: TODO: Find this address)
        /// 
        /// CRITICAL: K1 and K2 have different NPCs for the same member IDs (e.g., member ID 0 = "bastila" in K1, "atton" in K2).
        /// Without partytable.2da or a lookup function, we cannot determine the correct ResRef.
        /// </remarks>
        /// <param name="memberId">The member ID to convert (float, -1 = player, 0-11 = NPC slots).</param>
        /// <param name="memberIdToResRef">Optional lookup function. If provided, uses this function. Otherwise uses fallback.</param>
        /// <returns>The ResRef string, or empty string if conversion fails.</returns>
        private static string GetResRefFromMemberId(float memberId, Func<float, string> memberIdToResRef)
        {
            // Use provided lookup function if available
            if (memberIdToResRef != null)
            {
                string resRef = memberIdToResRef(memberId);
                if (!string.IsNullOrEmpty(resRef))
                {
                    return resRef;
                }
            }

            // Player character (member ID = -1)
            // Based on nwscript.nss constants: NPC_PLAYER = -1
            // Player ResRefs are typically "player", "pc", or start with "pc_"
            if (memberId < 0.0f || System.Math.Abs(memberId - (-1.0f)) < 0.001f)
            {
                return "player"; // Default player ResRef
            }

            int memberIdInt = (int)memberId;

            // Fallback: Hardcoded mapping for common NPCs when lookup function not available
            // WARNING: This is game-specific and may be incorrect for K1 vs K2
            // K1 NPCs (0-8)
            if (memberIdInt >= 0 && memberIdInt <= 8)
            {
                var k1Mapping = new Dictionary<int, string>
                {
                    { 0, "bastila" },      // NPC_BASTILA
                    { 1, "canderous" },   // NPC_CANDEROUS
                    { 2, "carth" },        // NPC_CARTH
                    { 3, "hk47" },         // NPC_HK_47
                    { 4, "jolee" },        // NPC_JOLEE
                    { 5, "juhani" },      // NPC_JUHANI
                    { 6, "mission" },     // NPC_MISSION
                    { 7, "t3m4" },        // NPC_T3_M4
                    { 8, "zaalbar" }      // NPC_ZAALBAR
                };

                if (k1Mapping.ContainsKey(memberIdInt))
                {
                    return k1Mapping[memberIdInt];
                }
            }

            // K2 NPCs (0-11) - some overlap with K1 but different ResRefs for same IDs
            if (memberIdInt >= 0 && memberIdInt <= 11)
            {
                var k2Mapping = new Dictionary<int, string>
                {
                    { 0, "atton" },       // NPC_ATTON
                    { 1, "bao" },         // NPC_BAO_DUR
                    { 2, "carth" },       // NPC_CARTH (same as K1)
                    { 3, "handmaiden" },  // NPC_HANDMAIDEN
                    { 4, "hk47" },        // NPC_HK_47 (same as K1)
                    { 5, "kreia" },       // NPC_KREIA
                    { 6, "mira" },        // NPC_MIRA
                    { 7, "t3m4" },        // NPC_T3_M4 (same as K1)
                    { 8, "visas" },       // NPC_VISAS
                    { 9, "disciple" },    // NPC_DISCIPLE
                    { 10, "g0t0" },       // NPC_G0T0
                    { 11, "hanharr" }     // NPC_HANHARR
                };

                if (k2Mapping.TryGetValue(memberIdInt, out string id))
                {
                    return id;
                }
            }

            // If member ID is out of range or not found in mappings, return empty string
            // This matches original engine behavior when member ID cannot be resolved
            // Original engine always has partytable.2da available, so this fallback should rarely be hit
            return "";
        }

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

        public string GetPath()
        {
            return _partyTablePath;
        }

        public void Load()
        {
            if (!File.Exists(_partyTablePath))
            {
                return;
            }
            byte[] data = File.ReadAllBytes(_partyTablePath);
            if (data.Length == 0)
            {
                return;
            }
            try
            {
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
            catch (Exception)
            {
                // If loading fails, just leave fields at defaults
                // This matches Python behavior where loading invalid files doesn't crash
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
                    case GFFFieldType.Vector3:
                        if (value is Vector3 v3)
                        {
                            root.SetVector3(label, new Vector3(v3.X, v3.Y, v3.Z));
                        }
                        else
                        {
                            root.SetVector3(label, Vector3.Zero);
                        }
                        break;
                    case GFFFieldType.Vector4:
                        if (value is Vector4 v4)
                        {
                            root.SetVector4(label, new Vector4(v4.X, v4.Y, v4.Z, v4.W));
                        }
                        else
                        {
                            root.SetVector4(label, Vector4.Zero);
                        }
                        break;
                    case GFFFieldType.Struct: root.SetStruct(label, value as GFFStruct ?? new GFFStruct()); break;
                    case GFFFieldType.List: root.SetList(label, value as GFFList ?? new GFFList()); break;
                    default: break;
                }
            }

            byte[] bytes = new GFFBinaryWriter(gff).Write();
            File.WriteAllBytes(_partyTablePath, bytes);
        }

        /// <summary>
        /// Serializes a Runtime PartyState directly to GFF bytes.
        /// </summary>
        public static byte[] SerializeRuntimePartyState(object runtimeState)
        {
            if (runtimeState == null) return Array.Empty<byte>();

            var runtimeType = runtimeState.GetType();

            // Create temporary PartyTable instance to use its serialization
            var partyTable = new PartyTable("");

            // Extract basic properties using reflection
            SetPropertyIfExists(runtimeState, runtimeType, "Gold", partyTable, "Gold");
            SetPropertyIfExists(runtimeState, runtimeType, "ExperiencePoints", partyTable, "XpPool");
            SetPropertyIfExists(runtimeState, runtimeType, "ControlledNPC", partyTable, "ControlledNpc");
            SetPropertyIfExists(runtimeState, runtimeType, "SoloMode", partyTable, "SoloMode");
            SetPropertyIfExists(runtimeState, runtimeType, "CheatUsed", partyTable, "CheatUsed");
            SetPropertyIfExists(runtimeState, runtimeType, "AIState", partyTable, "AiState");
            SetPropertyIfExists(runtimeState, runtimeType, "FollowState", partyTable, "FollowState");
            SetPropertyIfExists(runtimeState, runtimeType, "ItemComponent", partyTable, "ItemComponents");
            SetPropertyIfExists(runtimeState, runtimeType, "ItemChemical", partyTable, "ItemChemicals");

            // Extract play time
            var playTimeProp = runtimeType.GetProperty("PlayTime");
            if (playTimeProp != null)
            {
                var playTime = playTimeProp.GetValue(runtimeState);
                if (playTime is TimeSpan ts)
                {
                    partyTable.TimePlayed = (int)ts.TotalSeconds;
                }
            }

            // Extract selected party
            var selectedPartyProp = runtimeType.GetProperty("SelectedParty");
            if (selectedPartyProp != null)
            {
                var selectedParty = selectedPartyProp.GetValue(runtimeState) as System.Collections.IList;
                if (selectedParty != null)
                {
                    for (int i = 0; i < selectedParty.Count; i++)
                    {
                        var memberEntry = new PartyMemberEntry { Index = i };
                        partyTable.Members.Add(memberEntry);
                    }
                }
            }

            // Extract available members
            var availableMembersProp = runtimeType.GetProperty("AvailableMembers");
            if (availableMembersProp != null)
            {
                var availableMembers = availableMembersProp.GetValue(runtimeState) as System.Collections.IDictionary;
                if (availableMembers != null)
                {
                    foreach (System.Collections.DictionaryEntry entry in availableMembers)
                    {
                        var memberState = entry.Value;
                        if (memberState != null)
                        {
                            var memberType = memberState.GetType();
                            var isAvailableProp = memberType.GetProperty("IsAvailable");
                            var isSelectableProp = memberType.GetProperty("IsSelectable");

                            if (isAvailableProp != null && isSelectableProp != null)
                            {
                                var npcEntry = new AvailableNPCEntry
                                {
                                    NpcAvailable = (bool)isAvailableProp.GetValue(memberState),
                                    NpcSelected = (bool)isSelectableProp.GetValue(memberState)
                                };
                                partyTable.AvailableNpcs.Add(npcEntry);
                            }
                        }
                    }
                }
            }

            // Extract influence
            var influenceProp = runtimeType.GetProperty("Influence");
            if (influenceProp != null)
            {
                var influence = influenceProp.GetValue(runtimeState) as System.Collections.IList;
                if (influence != null)
                {
                    foreach (var val in influence)
                    {
                        partyTable.Influence.Add((int)val);
                    }
                }
            }

            // Serialize using existing PartyTable.Save method
            partyTable.Save();
            return System.IO.File.ReadAllBytes(partyTable.GetPath());
        }

        /// <summary>
        /// Deserializes GFF bytes directly to Runtime PartyState.
        /// </summary>
        /// <param name="data">The GFF bytes to deserialize.</param>
        /// <param name="runtimeType">The runtime type to create an instance of.</param>
        /// <param name="memberIdToResRef">Optional function to convert member ID (float) to ResRef (string). If null, uses fallback mapping.</param>
        public static object DeserializeRuntimePartyState(byte[] data, Type runtimeType, Func<float, string> memberIdToResRef = null)
        {
            if (data == null || data.Length == 0 || runtimeType == null) return null;

            try
            {
                // Create temporary file for PartyTable to load from
                string tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "temp_partytable_" + Guid.NewGuid().ToString("N") + ".res");
                System.IO.File.WriteAllBytes(tempPath, data);

                // Load using PartyTable
                var partyTable = new PartyTable(tempPath);
                partyTable.Load();

                // Convert to Runtime format using ToRuntimePartyState helper
                var runtimeState = ToRuntimePartyState(partyTable, runtimeType, memberIdToResRef);

                // Clean up temp file
                try { System.IO.File.Delete(tempPath); } catch { }

                return runtimeState;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
