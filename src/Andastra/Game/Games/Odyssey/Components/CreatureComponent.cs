using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Components;

using ClassData = Andastra.Game.Games.Odyssey.Data.GameDataManager.ClassData;
using FeatData = Andastra.Game.Games.Odyssey.Data.GameDataManager.FeatData;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Component for creature entities (NPCs and PCs).
    /// </summary>
    /// <remarks>
    /// Creature Component:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) creature system
    /// - Located via string references: "Creature" @ 0x007bc544, "Creature List" @ 0x007bd01c
    /// - "CreatureSize" @ 0x007bf680, "CreatureSpeed" @ 0x007c4b8c
    /// - "CreatureList" @ 0x007c0c80, "RecCreatures" @ 0x007c0cb4, "MaxCreatures" @ 0x007c0cc4 (encounter creature lists)
    /// - "GetCreatureRadius" @ 0x007bb128 (creature collision radius calculation)
    /// - Script events: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec
    /// - "EVENT_SUMMON_CREATURE" @ 0x007bcc08 (creature summoning event)
    /// - Error messages:
    ///   - "Creature template '%s' doesn't exist.\n" @ 0x007bf78c
    ///   - "Cannot set creature %s to faction %d because faction does not exist! Setting to Hostile1." @ 0x007bf2a8
    ///   - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc
    ///   - "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40
    ///   - "Tried to reduce XP of creature '%s' to '%d'. Cannot reduce XP." @ 0x007c3fa8
    /// - Pathfinding errors:
    ///   - "    failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
    ///   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
    /// - Original implementation: 0x005226d0 @ 0x005226d0 (save creature data to GFF)
    /// - 0x004dfbb0 @ 0x004dfbb0 (load creature instances from GIT)
    /// - 0x005261b0 @ 0x005261b0 (load creature from UTC template)
    /// - 0x0050c510 @ 0x0050c510 (load creature script hooks from GFF)
    ///   - Original implementation (from decompiled 0x0050c510):
    ///     - Function signature: `void 0x0050c510(void *this, void *param_1, uint *param_2)`
    ///     - param_1: GFF structure pointer
    ///     - param_2: GFF field pointer
    ///     - Reads script ResRef fields from GFF and stores at offsets in creature object:
    ///       - "ScriptHeartbeat" @ this + 0x270 (OnHeartbeat script)
    ///       - "ScriptOnNotice" @ this + 0x278 (OnPerception script)
    ///       - "ScriptSpellAt" @ this + 0x280 (OnSpellCastAt script)
    ///       - "ScriptAttacked" @ this + 0x288 (OnAttacked script)
    ///       - "ScriptDamaged" @ this + 0x290 (OnDamaged script)
    ///       - "ScriptDisturbed" @ this + 0x298 (OnDisturbed script)
    ///       - "ScriptEndRound" @ this + 0x2a0 (OnEndRound script)
    ///       - "ScriptDialogue" @ this + 0x2a8 (OnDialogue script)
    ///       - "ScriptSpawn" @ this + 0x2b0 (OnSpawn script)
    ///       - "ScriptRested" @ this + 0x2b8 (OnRested script)
    ///       - "ScriptDeath" @ this + 0x2c0 (OnDeath script)
    ///       - "ScriptUserDefine" @ this + 0x2c8 (OnUserDefined script)
    ///       - "ScriptOnBlocked" @ this + 0x2d0 (OnBlocked script)
    ///       - "ScriptEndDialogue" @ this + 0x2d8 (OnEndDialogue script)
    ///     - Uses 0x00412f30 to read GFF string fields, 0x00630c50 to store strings
    ///     - Script hooks stored as ResRef strings (16 bytes, null-terminated)
    /// - Creatures have appearance, stats, equipment, classes, feats, force powers
    /// - Based on UTC file format (GFF with "UTC " signature)
    /// - Script events: OnHeartbeat, OnPerception, OnAttacked, OnDamaged, OnDeath, etc.
    /// - Equip_ItemList stores equipped items, ItemList stores inventory, PerceptionList stores perception data
    /// - CombatRoundData stores combat state (0x00529470 saves combat round data)
    /// </remarks>
    public class CreatureComponent : BaseCreatureComponent
    {

        public CreatureComponent()
        {
            TemplateResRef = string.Empty;
            Tag = string.Empty;
            Conversation = string.Empty;
            FeatList = new List<int>();
            ClassList = new List<CreatureClass>();
            EquippedItems = new Dictionary<int, string>();
            KnownPowers = new List<int>();
            FeatDailyUses = new Dictionary<int, int>(); // Feat ID -> remaining uses today
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Creature tag.
        /// </summary>
        public string Tag { get; set; }

        #region Appearance

        /// <summary>
        /// Racial type ID (index into racialtypes.2da).
        /// </summary>
        public int RaceId { get; set; }

        /// <summary>
        /// Appearance type (index into appearance.2da).
        /// </summary>
        public int AppearanceType { get; set; }

        /// <summary>
        /// Body variation index.
        /// </summary>
        public int BodyVariation { get; set; }

        /// <summary>
        /// Texture variation index.
        /// </summary>
        public int TextureVar { get; set; }

        /// <summary>
        /// Portrait ID.
        /// </summary>
        public int PortraitId { get; set; }

        /// <summary>
        /// Gender (0 = Male, 1 = Female).
        /// </summary>
        public int Gender { get; set; }

        #endregion

        #region Vital Statistics

        /// <summary>
        /// Current hit points.
        /// </summary>
        public int CurrentHP { get; set; }

        /// <summary>
        /// Maximum hit points.
        /// </summary>
        public int MaxHP { get; set; }

        /// <summary>
        /// Current force points.
        /// </summary>
        public int CurrentForce { get; set; }

        /// <summary>
        /// Maximum force points.
        /// </summary>
        public int MaxForce { get; set; }

        /// <summary>
        /// Walk speed multiplier.
        /// </summary>
        public float WalkRate { get; set; }

        /// <summary>
        /// Natural armor class bonus.
        /// </summary>
        public int NaturalAC { get; set; }

        #endregion

        #region Attributes

        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Constitution { get; set; }
        public int Intelligence { get; set; }
        public int Wisdom { get; set; }
        public int Charisma { get; set; }

        /// <summary>
        /// Gets the attribute modifier for an ability score.
        /// </summary>
        public int GetModifier(int abilityScore)
        {
            return (abilityScore - 10) / 2;
        }

        #endregion

        #region Combat

        /// <summary>
        /// Faction ID (for hostility checks).
        /// </summary>
        public int FactionId { get; set; }

        /// <summary>
        /// Conversation file (dialogue ResRef).
        /// Stored in UTC template as "ScriptDialogue" field.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0050c510 @ 0x0050c510 loads creature data including ScriptDialogue field
        /// Located via string reference: "ScriptDialogue" @ 0x007bee40, "Conversation" @ 0x007c1abc
        /// </remarks>
        public string Conversation { get; set; }

        /// <summary>
        /// Perception range for sight.
        /// </summary>
        public float PerceptionRange { get; set; }

        /// <summary>
        /// Challenge rating.
        /// </summary>
        public float ChallengeRating { get; set; }

        /// <summary>
        /// Whether creature is immortal.
        /// </summary>
        public bool IsImmortal { get; set; }

        /// <summary>
        /// Whether creature has no death script.
        /// </summary>
        public bool NoPermDeath { get; set; }

        /// <summary>
        /// Whether creature is disarmable.
        /// </summary>
        public bool Disarmable { get; set; }

        /// <summary>
        /// Whether creature is interruptable.
        /// </summary>
        public bool Interruptable { get; set; }

        #endregion

        #region Classes and Levels

        /// <summary>
        /// List of class levels.
        /// </summary>
        public List<CreatureClass> ClassList { get; set; }

        /// <summary>
        /// Gets total character level.
        /// </summary>
        public int GetTotalLevel()
        {
            int total = 0;
            foreach (CreatureClass cls in ClassList)
            {
                total += cls.Level;
            }
            return total;
        }

        /// <summary>
        /// Gets base attack bonus using simplified calculation (fallback when GameDataManager unavailable).
        /// </summary>
        /// <returns>Simplified BAB calculation (sum of all class levels).</returns>
        public int GetBaseAttackBonus()
        {
            int bab = 0;
            foreach (CreatureClass cls in ClassList)
            {
                // Simplified BAB calculation - just adds levels
                // Full implementation requires game data tables - use GetBaseAttackBonus(GameDataManager) for accurate calculation
                bab += cls.Level;
            }
            return bab;
        }

        /// <summary>
        /// Gets base attack bonus using classes.2da for accurate calculation.
        /// Based on verified components of swkotor.exe, swkotor2.exe:
        /// - Each class has an attackbonustable column in classes.2da that references a BAB progression table
        /// - BAB progression tables (e.g., cls_atk_jedi_guardian.2da) contain BAB values per level
        /// - For multi-class characters, BAB from all classes is summed together
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005d63d0 @ 0x005d63d0 reads classes.2da, loads attack bonus tables, calculates BAB per level
        /// </summary>
        /// <param name="gameDataManager">GameDataManager to look up class data and attack bonus tables.</param>
        /// <returns>Total base attack bonus from all class levels, or simplified calculation if game data unavailable.</returns>
        /// <remarks>
        /// Based on verified components of swkotor.exe, swkotor2.exe:
        /// - swkotor2.exe: 0x005d63d0 @ 0x005d63d0 reads "attackbonustable" column from classes.2da
        /// - Attack bonus tables are named like "cls_atk_jedi_guardian" (referenced in classes.2da)
        /// - Each attack bonus table has rows for each level (row 0 = level 1, row 1 = level 2, etc.)
        /// - Table columns typically include "BAB" or "Value" column with the BAB value for that level
        /// - Multi-class BAB: Sum of BAB from all classes (e.g., 5 Fighter levels + 3 Wizard levels = Fighter BAB + Wizard BAB)
        /// - Based on D&D 3.5 rules: BAB is calculated per class and summed for multi-class characters
        /// </remarks>
        public int GetBaseAttackBonus(Data.GameDataManager gameDataManager)
        {
            if (gameDataManager == null || ClassList == null || ClassList.Count == 0)
            {
                // Fallback to simplified calculation if game data provider is not available
                return GetBaseAttackBonus();
            }

            int totalBab = 0;

            // Get classes.2da table
            BioWare.NET.Resource.Formats.TwoDA.TwoDA classesTable = gameDataManager.GetTable("classes");
            if (classesTable == null)
            {
                // Fallback to simplified calculation if classes.2da is not available
                return GetBaseAttackBonus();
            }

            // Calculate BAB for each class
            foreach (CreatureClass cls in ClassList)
            {
                if (cls.Level <= 0)
                {
                    continue; // Skip invalid levels
                }

                // Get class data from classes.2da
                if (cls.ClassId < 0 || cls.ClassId >= classesTable.GetHeight())
                {
                    continue; // Skip invalid class IDs
                }

                BioWare.NET.Resource.Formats.TwoDA.TwoDARow classRow = classesTable.GetRow(cls.ClassId);
                if (classRow == null)
                {
                    continue; // Skip if class row not found
                }

                // Get attack bonus table name from classes.2da
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005d63d0 reads "attackbonustable" column from classes.2da
                string attackBonusTableName = classRow.GetString("attackbonustable");
                if (string.IsNullOrEmpty(attackBonusTableName) || attackBonusTableName == "****")
                {
                    continue; // Skip if attack bonus table name is missing or invalid
                }

                // Load the attack bonus table
                BioWare.NET.Resource.Formats.TwoDA.TwoDA attackBonusTable = gameDataManager.GetTable(attackBonusTableName);
                if (attackBonusTable == null)
                {
                    continue; // Skip if attack bonus table not found
                }

                // Sum BAB from all levels in this class (levels 1 through cls.Level)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): BAB is looked up per level from the attack bonus table
                for (int level = 1; level <= cls.Level; level++)
                {
                    // Level is 1-based, table rows are 0-based
                    int rowIndex = level - 1;
                    if (rowIndex < 0 || rowIndex >= attackBonusTable.GetHeight())
                    {
                        continue; // Skip if level out of range
                    }

                    BioWare.NET.Resource.Formats.TwoDA.TwoDARow babRow = attackBonusTable.GetRow(rowIndex);
                    if (babRow == null)
                    {
                        continue; // Skip if row not found
                    }

                    // Get BAB value from row
                    // Column names may vary: "BAB", "Value", "attackbonus", or similar
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Attack bonus tables typically have "BAB" or "Value" column
                    int? babValue = babRow.GetInteger("BAB");
                    if (!babValue.HasValue)
                    {
                        babValue = babRow.GetInteger("Value");
                    }
                    if (!babValue.HasValue)
                    {
                        babValue = babRow.GetInteger("attackbonus");
                    }
                    if (!babValue.HasValue)
                    {
                        // Try to find first integer column that's not "level", "Label", or "Name"
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Attack bonus tables may have different column structures
                        try
                        {
                            var allData = babRow.GetData();
                            foreach (var kvp in allData)
                            {
                                string colName = kvp.Key;
                                if (colName != "Label" && colName != "Name" && colName != "level" &&
                                    colName != "Level" && !string.IsNullOrWhiteSpace(kvp.Value) && kvp.Value != "****")
                                {
                                    babValue = babRow.GetInteger(colName);
                                    if (babValue.HasValue)
                                    {
                                        break; // Found a valid BAB value
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // If we can't iterate columns, continue to next level
                        }
                    }

                    // Add BAB value for this level (default to 0 if not found)
                    totalBab += babValue ?? 0;
                }
            }

            return totalBab;
        }

        /// <summary>
        /// Gets total levels in Force-using classes (caster level for Force powers).
        /// </summary>
        /// <param name="gameDataManager">GameDataManager to look up class data.</param>
        /// <returns>Sum of levels in all Force-using classes.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Caster level for Force powers is the sum of levels in Force-using classes
        /// Force-using classes are determined by the "forcedie" column in classes.2da (if forcedie > 0, class is Force-using)
        /// Force-using classes include: Jedi Guardian (3), Jedi Consular (4), Jedi Sentinel (5),
        /// Jedi Master (12), Jedi Watchman (13), Jedi Weapon Master (11),
        /// Sith Lord (15), Sith Marauder (14), Sith Assassin (16)
        /// </remarks>
        public int GetForceUsingClassLevels(Data.GameDataManager gameDataManager)
        {
            if (gameDataManager == null || ClassList == null)
            {
                return 0;
            }

            int totalLevel = 0;
            foreach (CreatureClass cls in ClassList)
            {
                // Look up class data to check if it's a Force-using class
                ClassData classData = gameDataManager.GetClass(cls.ClassId);
                if (classData != null && classData.ForceUser)
                {
                    totalLevel += cls.Level;
                }
            }

            return totalLevel;
        }

        #endregion

        #region Feats and Powers

        /// <summary>
        /// List of feat IDs.
        /// </summary>
        public List<int> FeatList { get; set; }

        /// <summary>
        /// List of known force powers.
        /// </summary>
        public List<int> KnownPowers { get; set; }

        /// <summary>
        /// Daily feat usage tracking.
        /// Key: Feat ID, Value: Remaining uses today
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Feat daily usage tracking system
        /// Located via string references: Feat usage tracking in creature data structure
        /// Original implementation: Tracks remaining uses per day for feats with daily limits
        /// Daily uses reset when creature rests or new day begins
        /// </remarks>
        public Dictionary<int, int> FeatDailyUses { get; set; }

        /// <summary>
        /// Checks if creature has a feat.
        /// </summary>
        public override bool HasFeat(int featId)
        {
            return FeatList.Contains(featId);
        }

        /// <summary>
        /// Checks if a feat is currently usable (has remaining daily uses if limited).
        /// </summary>
        /// <param name="featId">The feat ID to check.</param>
        /// <param name="gameDataManager">GameDataManager to look up feat data.</param>
        /// <returns>True if the feat is usable, false if exhausted or restricted.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: GetHasFeat usability checking
        /// Located via string references: Feat usability checking in GetHasFeat function
        /// Original implementation: Checks if feat has remaining daily uses
        /// - If feat has UsesPerDay = -1: Always usable (unlimited or special handling)
        /// - If feat has UsesPerDay = 0: Never usable (disabled feat)
        /// - If feat has UsesPerDay > 0: Usable if remaining uses > 0
        /// - Feats with special handling (UsesPerDay = -1) may have class-level-based limits
        /// </remarks>
        public bool IsFeatUsable(int featId, Data.GameDataManager gameDataManager)
        {
            if (!HasFeat(featId))
            {
                return false;
            }

            if (gameDataManager == null)
            {
                // If GameDataManager not available, assume feat is usable if creature has it
                return true;
            }

            FeatData featData = gameDataManager.GetFeat(featId);
            if (featData == null)
            {
                // Feat data not found, assume usable if creature has it
                return true;
            }

            // Check daily usage limits
            if (featData.UsesPerDay == -1)
            {
                // Unlimited uses or special handling (e.g., based on class levels)
                // For special handling feats, check if they have remaining uses based on class levels
                // Example: Stunning Fist uses per day = monk level
                return IsSpecialFeatUsable(featId, gameDataManager);
            }
            else if (featData.UsesPerDay == 0)
            {
                // Feat disabled (0 uses per day)
                return false;
            }
            else
            {
                // Feat has daily limit - check remaining uses
                int remainingUses;
                if (!FeatDailyUses.TryGetValue(featId, out remainingUses))
                {
                    // First time using this feat today - initialize with max uses
                    remainingUses = featData.UsesPerDay;
                    FeatDailyUses[featId] = remainingUses;
                }

                return remainingUses > 0;
            }
        }

        /// <summary>
        /// Gets the class ID for a feat (Odyssey-specific implementation).
        /// Maps feats to their associated classes for special feat usage calculation.
        /// </summary>
        /// <param name="featId">The feat ID to look up.</param>
        /// <param name="gameDataProvider">GameDataProvider to look up feat data.</param>
        /// <returns>The class ID associated with the feat, or -1 if not class-specific.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Feat-to-class mapping for special feat usage calculation
        /// Located via string references: "classfeat" @ classes.2da column, featgain.2da class-specific columns
        /// Original implementation: Determines which class a feat is associated with for calculating uses per day
        ///
        /// Odyssey-specific feat-to-class mappings:
        /// 1. Check classes.2da "classfeat" column - each class may have a class-specific feat
        /// 2. Check featgain.2da - if feat is only available through one class's feat gain table, it's class-specific
        /// 3. Most special feats in Odyssey are not class-specific (return -1 to use total level)
        ///
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005d63d0 @ 0x005d63d0 reads classes.2da and checks classfeat column
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0060d1d0 @ 0x0060d1d0 reads featgain.2da for class-specific feat availability
        /// </remarks>
        protected override int GetFeatClassId(int featId, object gameDataProvider)
        {
            if (featId < 0)
            {
                return -1; // Invalid feat ID
            }

            if (!(gameDataProvider is Data.GameDataManager gameDataManager))
            {
                // GameDataManager not available - cannot determine class association
                return -1;
            }

            // Method 1: Check classes.2da "classfeat" column
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005d63d0 @ 0x005d63d0 reads "classfeat" column from classes.2da
            // Each class may have a class-specific feat defined in the "classfeat" column
            // If the feat ID matches a class's classfeat value, that class is associated with the feat
            BioWare.NET.Resource.Formats.TwoDA.TwoDA classesTable = gameDataManager.GetTable("classes");
            if (classesTable != null)
            {
                // Iterate through all classes to find if any class has this feat as its classfeat
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Classes.2da has rows for each class (0=Soldier, 1=Scout, 2=Scoundrel, 3=JediGuardian, etc.)
                for (int classId = 0; classId < classesTable.GetHeight(); classId++)
                {
                    BioWare.NET.Resource.Formats.TwoDA.TwoDARow classRow = classesTable.GetRow(classId);
                    if (classRow == null)
                        continue;
                    // Check if this class has the feat as its class-specific feat
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): "classfeat" column contains the feat ID for class-specific feats
                    int? classFeatId = classRow.GetInteger("classfeat");
                    if (classFeatId == featId)
                    {
                        // Found class association via classfeat column
                        return classId;
                    }
                }
            }

            // Method 2: Check featgain.2da to see if feat is only available through one class
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0060d1d0 @ 0x0060d1d0 reads featgain.2da for class-specific feat availability
            // If a feat appears only in one class's feat gain table and not in others, it's class-specific
            BioWare.NET.Resource.Formats.TwoDA.TwoDA featgainTable = gameDataManager.GetTable("featgain");
            if (featgainTable != null && classesTable != null)
            {
                int? foundClassId = null;
                bool foundInMultipleClasses = false;

                // Check each class's feat gain table
                for (int classId = 0; classId < classesTable.GetHeight(); classId++)
                {
                    BioWare.NET.Resource.Formats.TwoDA.TwoDARow classRow = classesTable.GetRow(classId);
                    if (classRow == null)
                    {
                        continue;
                    }

                    // Get FeatGain row label from classes.2da
                    string featGainLabel = classRow.GetString("featgain");
                    if (string.IsNullOrEmpty(featGainLabel) || featGainLabel == "****")
                    {
                        continue;
                    }

                    // Find the row in featgain.2da by label
                    BioWare.NET.Resource.Formats.TwoDA.TwoDARow featgainRow = featgainTable.FindRow(featGainLabel);
                    if (featgainRow == null)
                    {
                        continue;
                    }

                    // Get class column prefix (e.g., "soldier", "scout", "jedi_guardian")
                    string classColumnPrefix = GetClassColumnNameForFeatGain(classId);
                    if (string.IsNullOrEmpty(classColumnPrefix))
                    {
                        continue;
                    }

                    // Check if feat appears in this class's feat gain columns (_REG and _BON)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0060d1d0 reads from "_REG" and "_BON" columns (loop 0 to 0x32 = 50)
                    bool foundInThisClass = false;

                    // Check indexed columns (_REG0, _REG1, ..., _REG49, _BON0, _BON1, ..., _BON49)
                    for (int i = 0; i < 50; i++)
                    {
                        string regColumnName = classColumnPrefix + "_REG" + i;
                        string bonColumnName = classColumnPrefix + "_BON" + i;

                        int? regFeatId = featgainRow.GetInteger(regColumnName);
                        int? bonFeatId = featgainRow.GetInteger(bonColumnName);

                        if ((regFeatId.HasValue && regFeatId.Value == featId) ||
                            (bonFeatId.HasValue && bonFeatId.Value == featId))
                        {
                            foundInThisClass = true;
                            break;
                        }
                    }

                    // Also check single _REG and _BON columns (if indexed columns not used)
                    if (!foundInThisClass)
                    {
                        string regColumnName = classColumnPrefix + "_REG";
                        string bonColumnName = classColumnPrefix + "_BON";

                        string regFeats = featgainRow.GetString(regColumnName);
                        string bonFeats = featgainRow.GetString(bonColumnName);

                        if (!string.IsNullOrEmpty(regFeats) && regFeats != "****" && regFeats != "*")
                        {
                            string[] featIdStrings = regFeats.Split(new[] { ',', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                            foreach (string featIdStr in featIdStrings)
                            {
                                int parsedFeatId;
                                if (int.TryParse(featIdStr, out parsedFeatId) && parsedFeatId == featId)
                                {
                                    foundInThisClass = true;
                                    break;
                                }
                            }
                        }

                        if (!foundInThisClass && !string.IsNullOrEmpty(bonFeats) && bonFeats != "****" && bonFeats != "*")
                        {
                            string[] featIdStrings = bonFeats.Split(new[] { ',', ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
                            foreach (string featIdStr in featIdStrings)
                            {
                                int parsedFeatId;
                                if (int.TryParse(featIdStr, out parsedFeatId) && parsedFeatId == featId)
                                {
                                    foundInThisClass = true;
                                    break;
                                }
                            }
                        }
                    }

                    if (foundInThisClass)
                    {
                        if (foundClassId.HasValue)
                        {
                            // Feat found in multiple classes - not class-specific
                            foundInMultipleClasses = true;
                            break;
                        }
                        else
                        {
                            foundClassId = classId;
                        }
                    }
                }

                // If feat found in exactly one class's feat gain table, return that class ID
                if (foundClassId.HasValue && !foundInMultipleClasses)
                {
                    return foundClassId.Value;
                }
            }

            // Method 3: Check feat.2da for class-specific indicators
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Some feats may have class-specific requirements in feat.2da
            // Check if feat has minlevelclass requirement that might indicate class specificity
            Data.GameDataManager.FeatData featData = gameDataManager.GetFeat(featId);
            if (featData != null && featData.MinLevelClass > 0)
            {
                // Feat has minimum class level requirement - might be class-specific
                // However, minlevelclass is just a level requirement, not a class ID
                // We can't determine the class from this alone, so we still return -1
                // Note: In practice, most feats with minlevelclass are not class-specific
                // They just require a certain level in any class
            }

            // Feat is not class-specific - return -1 to use total level as fallback
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Most special feats in Odyssey use total character level for uses per day
            return -1;
        }

        /// <summary>
        /// Gets the class column name prefix for featgain.2da lookup.
        /// Maps class ID to the column name prefix used in featgain.2da (e.g., "soldier", "jedi_guardian").
        /// </summary>
        /// <param name="classId">The class ID.</param>
        /// <returns>The column name prefix, or null if class ID is unknown.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0060d1d0 @ 0x0060d1d0 uses class column names in featgain.2da
        /// Class column name mapping:
        /// - Soldier (0) -> "soldier"
        /// - Scout (1) -> "scout"
        /// - Scoundrel (2) -> "scoundrel"
        /// - Jedi Guardian (3) -> "jedi_guardian"
        /// - Jedi Consular (4) -> "jedi_consular"
        /// - Jedi Sentinel (5) -> "jedi_sentinel"
        /// - Prestige classes (K2 only): Jedi Master (12), Jedi Watchman (13), Jedi Weapon Master (11),
        ///   Sith Lord (15), Sith Marauder (14), Sith Assassin (16)
        /// </remarks>
        private string GetClassColumnNameForFeatGain(int classId)
        {
            switch (classId)
            {
                case 0: // Soldier
                    return "soldier";
                case 1: // Scout
                    return "scout";
                case 2: // Scoundrel
                    return "scoundrel";
                case 3: // Jedi Guardian
                    return "jedi_guardian";
                case 4: // Jedi Consular
                    return "jedi_consular";
                case 5: // Jedi Sentinel
                    return "jedi_sentinel";
                case 11: // Jedi Weapon Master (K2 prestige class)
                    return "jedi_weapon_master";
                case 12: // Jedi Master (K2 prestige class)
                    return "jedi_master";
                case 13: // Jedi Watchman (K2 prestige class)
                    return "jedi_watchman";
                case 14: // Sith Marauder (K2 prestige class)
                    return "sith_marauder";
                case 15: // Sith Lord (K2 prestige class)
                    return "sith_lord";
                case 16: // Sith Assassin (K2 prestige class)
                    return "sith_assassin";
                default:
                    // Unknown class ID - try to get from classes.2da label
                    return null;
            }
        }

        /// <summary>
        /// Gets feat data from game data provider (Odyssey-specific implementation).
        /// </summary>
        protected override BaseCreatureComponent.IFeatData GetFeatData(int featId, object gameDataProvider)
        {
            if (gameDataProvider == null)
            {
                return null;
            }

            var gameDataManager = gameDataProvider as Data.GameDataManager;
            if (gameDataManager == null)
            {
                return null;
            }

            FeatData featData = gameDataManager.GetFeat(featId);
            if (featData == null)
            {
                return null;
            }

            // Create adapter to convert FeatData to IFeatData
            return new FeatDataAdapter(featData);
        }

        /// <summary>
        /// Adapter to convert Odyssey FeatData to IFeatData interface.
        /// </summary>
        private class FeatDataAdapter : BaseCreatureComponent.IFeatData
        {
            private readonly FeatData _featData;

            public FeatDataAdapter(FeatData featData)
            {
                _featData = featData ?? throw new System.ArgumentNullException("featData");
            }

            public int UsesPerDay => _featData.UsesPerDay;
        }

        /// <summary>
        /// Records usage of a feat (decrements daily uses if limited).
        /// </summary>
        /// <param name="featId">The feat ID that was used.</param>
        /// <param name="gameDataManager">GameDataManager to look up feat data.</param>
        /// <returns>True if the feat was successfully used, false if it couldn't be used.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Feat usage tracking
        /// Located via string references: Feat usage decrement in creature data
        /// Original implementation: Decrements remaining uses when feat is used
        /// </remarks>
        public bool UseFeat(int featId, Data.GameDataManager gameDataManager)
        {
            if (!IsFeatUsable(featId, gameDataManager))
            {
                return false;
            }

            if (gameDataManager == null)
            {
                return true;
            }

            FeatData featData = gameDataManager.GetFeat(featId);
            if (featData == null)
            {
                return true;
            }

            // Only decrement if feat has daily limit
            if (featData.UsesPerDay > 0)
            {
                int remainingUses;
                if (!FeatDailyUses.TryGetValue(featId, out remainingUses))
                {
                    remainingUses = featData.UsesPerDay;
                }

                if (remainingUses > 0)
                {
                    remainingUses--;
                    FeatDailyUses[featId] = remainingUses;
                }
            }
            // Special feats (UsesPerDay = -1) don't need tracking here
            // They may have their own usage tracking elsewhere

            return true;
        }

        /// <summary>
        /// Resets daily feat uses (called when creature rests or new day begins).
        /// </summary>
        /// <param name="gameDataManager">GameDataManager to look up feat data.</param>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Daily feat use reset on rest
        /// Located via string references: Feat usage reset in rest system
        /// Original implementation: Resets all feat daily uses to maximum when creature rests
        /// </remarks>
        public void ResetDailyFeatUses(Data.GameDataManager gameDataManager)
        {
            if (gameDataManager == null || FeatList == null)
            {
                return;
            }

            // Reset uses for all feats the creature has
            foreach (int featId in FeatList)
            {
                FeatData featData = gameDataManager.GetFeat(featId);
                if (featData != null && featData.UsesPerDay > 0)
                {
                    FeatDailyUses[featId] = featData.UsesPerDay;
                }
            }
        }

        #endregion

        #region Equipment

        /// <summary>
        /// Equipped items by slot.
        /// </summary>
        public Dictionary<int, string> EquippedItems { get; set; }

        #endregion

        #region AI Behavior

        /// <summary>
        /// Whether creature is currently in combat.
        /// </summary>
        public bool IsInCombat { get; set; }

        /// <summary>
        /// Time since last heartbeat.
        /// </summary>
        public float TimeSinceHeartbeat { get; set; }

        /// <summary>
        /// Combat round state.
        /// </summary>
        public CombatRoundState CombatState { get; set; }

        /// <summary>
        /// Whether creature is currently in stealth mode.
        /// </summary>
        /// <remarks>
        /// swkotor2.exe: StealthMode stored at offset +0x511 as byte (boolean)
        /// - 0x00542bf0 @ 0x00542bf0 sets StealthMode: *(char *)((int)this + 0x511) = param_1
        /// - 0x005143a0 @ 0x005143a0 checks StealthMode: if (*(char *)((int)this + 0x511) == '\x01')
        /// - 0x005226d0 @ 0x005226d0 (SerializeCreature_K2) saves StealthMode to GFF as byte field "StealthMode"
        /// - 0x005223a0 @ 0x005223a0 loads StealthMode from GFF field "StealthMode" and sets at offset +0x511
        /// - Stealth mode allows creatures to avoid detection, gain stealth XP, lose stealth on damage
        /// - TSL only feature (not present in K1)
        /// </remarks>
        public bool StealthMode { get; set; }

        /// <summary>
        /// Whether creature is currently in detect mode.
        /// </summary>
        /// <remarks>
        /// swkotor2.exe: DetectMode stored at offset +0x510 as byte (boolean)
        /// - 0x00542bd0 @ 0x00542bd0 sets DetectMode: *(char *)((int)this + 0x510) = param_1
        /// - 0x005226d0 @ 0x005226d0 (SerializeCreature_K2) saves DetectMode to GFF as byte field "DetectMode"
        /// - 0x005223a0 @ 0x005223a0 loads DetectMode from GFF field "DetectMode" and sets at offset +0x510
        /// - Detect mode allows creatures to detect stealthed enemies
        /// </remarks>
        public bool DetectMode { get; set; }

        #endregion
    }

    /// <summary>
    /// Creature class information.
    /// </summary>
    public class CreatureClass
    {
        /// <summary>
        /// Class ID (index into classes.2da).
        /// </summary>
        public int ClassId { get; set; }

        /// <summary>
        /// Level in this class.
        /// </summary>
        public int Level { get; set; }
    }

    /// <summary>
    /// Combat round state for creatures.
    /// </summary>
    public enum CombatRoundState
    {
        NotInCombat,
        Starting,
        FirstAttack,
        SecondAttack,
        Cooldown,
        Finished
    }
}
