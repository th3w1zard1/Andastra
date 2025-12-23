using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common.Components;

using ClassData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.ClassData;
using FeatData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.FeatData;

namespace Andastra.Runtime.Engines.Odyssey.Components
{
    /// <summary>
    /// Component for creature entities (NPCs and PCs).
    /// </summary>
    /// <remarks>
    /// Creature Component:
    /// - Based on swkotor2.exe creature system
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
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 (save creature data to GFF)
    /// - FUN_004dfbb0 @ 0x004dfbb0 (load creature instances from GIT)
    /// - FUN_005261b0 @ 0x005261b0 (load creature from UTC template)
    /// - FUN_0050c510 @ 0x0050c510 (load creature script hooks from GFF)
    ///   - Original implementation (from decompiled FUN_0050c510):
    ///     - Function signature: `void FUN_0050c510(void *this, void *param_1, uint *param_2)`
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
    ///     - Uses FUN_00412f30 to read GFF string fields, FUN_00630c50 to store strings
    ///     - Script hooks stored as ResRef strings (16 bytes, null-terminated)
    /// - Creatures have appearance, stats, equipment, classes, feats, force powers
    /// - Based on UTC file format (GFF with "UTC " signature)
    /// - Script events: OnHeartbeat, OnPerception, OnAttacked, OnDamaged, OnDeath, etc.
    /// - Equip_ItemList stores equipped items, ItemList stores inventory, PerceptionList stores perception data
    /// - CombatRoundData stores combat state (FUN_00529470 saves combat round data)
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
        /// Based on swkotor2.exe: FUN_0050c510 @ 0x0050c510 loads creature data including ScriptDialogue field
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
        /// Based on reverse engineering of swkotor.exe, swkotor2.exe:
        /// - Each class has an attackbonustable column in classes.2da that references a BAB progression table
        /// - BAB progression tables (e.g., cls_atk_jedi_guardian.2da) contain BAB values per level
        /// - For multi-class characters, BAB from all classes is summed together
        /// - Based on swkotor2.exe: FUN_005d63d0 @ 0x005d63d0 reads classes.2da, loads attack bonus tables, calculates BAB per level
        /// </summary>
        /// <param name="gameDataManager">GameDataManager to look up class data and attack bonus tables.</param>
        /// <returns>Total base attack bonus from all class levels, or simplified calculation if game data unavailable.</returns>
        /// <remarks>
        /// Based on reverse engineering of swkotor.exe, swkotor2.exe:
        /// - swkotor2.exe: FUN_005d63d0 @ 0x005d63d0 reads "attackbonustable" column from classes.2da
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
            Parsing.Formats.TwoDA.TwoDA classesTable = gameDataManager.GetTable("classes");
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

                Parsing.Formats.TwoDA.TwoDARow classRow = classesTable.GetRow(cls.ClassId);
                if (classRow == null)
                {
                    continue; // Skip if class row not found
                }

                // Get attack bonus table name from classes.2da
                // Based on swkotor2.exe: FUN_005d63d0 reads "attackbonustable" column from classes.2da
                string attackBonusTableName = classRow.GetString("attackbonustable");
                if (string.IsNullOrEmpty(attackBonusTableName) || attackBonusTableName == "****")
                {
                    continue; // Skip if attack bonus table name is missing or invalid
                }

                // Load the attack bonus table
                Parsing.Formats.TwoDA.TwoDA attackBonusTable = gameDataManager.GetTable(attackBonusTableName);
                if (attackBonusTable == null)
                {
                    continue; // Skip if attack bonus table not found
                }

                // Sum BAB from all levels in this class (levels 1 through cls.Level)
                // Based on swkotor2.exe: BAB is looked up per level from the attack bonus table
                for (int level = 1; level <= cls.Level; level++)
                {
                    // Level is 1-based, table rows are 0-based
                    int rowIndex = level - 1;
                    if (rowIndex < 0 || rowIndex >= attackBonusTable.GetHeight())
                    {
                        continue; // Skip if level out of range
                    }

                    Parsing.Formats.TwoDA.TwoDARow babRow = attackBonusTable.GetRow(rowIndex);
                    if (babRow == null)
                    {
                        continue; // Skip if row not found
                    }

                    // Get BAB value from row
                    // Column names may vary: "BAB", "Value", "attackbonus", or similar
                    // Based on swkotor2.exe: Attack bonus tables typically have "BAB" or "Value" column
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
                        // Based on swkotor2.exe: Attack bonus tables may have different column structures
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
        /// Odyssey-specific feat-to-class mappings:
        /// - Most special feats in Odyssey are not class-specific
        /// - Engine-specific implementations can override this to provide specific mappings
        /// - Base implementation returns -1 (uses total level)
        /// </remarks>
        protected override int GetFeatClassId(int featId, object gameDataProvider)
        {
            // Odyssey-specific feat-to-class mappings would go here
            // TODO: STUB - For now, return -1 to use total level as fallback
            // Future enhancement: Add specific feat-to-class mappings if needed

            // Note: Stunning Fist is an Aurora (Neverwinter Nights) feat, not Odyssey
            // Odyssey uses different feat system (force powers, etc.)

            return -1;
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
