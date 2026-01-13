using System.Collections.Generic;
using BioWare.NET.Resource.Formats.TwoDA;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Common.Components
{
    /// <summary>
    /// Base class for creature components shared between Odyssey and Aurora engines.
    /// </summary>
    /// <remarks>
    /// Base Creature Component:
    /// - Common functionality shared between Odyssey (swkotor.exe, swkotor2.exe) and Aurora (nwmain.exe, nwn2main.exe)
    /// - Base classes MUST only contain functionality that is identical across BOTH engines
    /// - Engine-specific details MUST be in subclasses:
    ///   - Odyssey: CreatureComponent (single FeatList, KnownPowers for force powers)
    ///   - Aurora: AuroraCreatureComponent (FeatList + BonusFeatList, no force powers)
    /// - Common: TemplateResRef, Tag, Conversation, Appearance, Vital Statistics, Attributes, Combat properties, Classes, Equipment
    /// - Engine-specific: Feat storage (Odyssey: single list, Aurora: two lists), Force powers (Odyssey only)
    /// - Cross-engine analysis:
    ///   - Odyssey: swkotor.exe, swkotor2.exe - single feat list, force powers
    ///   - Aurora: nwmain.exe, nwn2main.exe - two feat lists (normal + bonus), no force powers
    ///   - Eclipse: daorigins.exe, DragonAge2.exe, ,  - uses talents/abilities, not feats
    ///   - Infinity: Uses different character system, not yet reverse engineered
    /// </remarks>
    public abstract class BaseCreatureComponent : IComponent
    {
        public IEntity Owner { get; set; }

        public virtual void OnAttach() { }
        public virtual void OnDetach() { }

        protected BaseCreatureComponent()
        {
            TemplateResRef = string.Empty;
            Tag = string.Empty;
            Conversation = string.Empty;
            ClassList = new List<BaseCreatureClass>();
            EquippedItems = new Dictionary<int, string>();
            FeatDailyUses = new Dictionary<int, int>();
        }

        /// <summary>
        /// Template resource reference (UTC file ResRef).
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
        /// </summary>
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
        /// List of class levels (engine-specific class type).
        /// </summary>
        public List<BaseCreatureClass> ClassList { get; set; }

        /// <summary>
        /// Gets total character level.
        /// </summary>
        public int GetTotalLevel()
        {
            int total = 0;
            foreach (BaseCreatureClass cls in ClassList)
            {
                total += cls.Level;
            }
            return total;
        }

        /// <summary>
        /// Gets base attack bonus using simplified calculation (levels only).
        /// </summary>
        /// <remarks>
        /// This is a simplified implementation that just adds levels.
        /// For accurate BAB calculation, use GetBaseAttackBonus(IGameDataProvider) instead.
        /// </remarks>
        public int GetBaseAttackBonus()
        {
            int bab = 0;
            foreach (BaseCreatureClass cls in ClassList)
            {
                // Simplified BAB calculation - just adds levels
                // Full implementation requires game data tables - use GetBaseAttackBonus(IGameDataProvider) for accurate calculation
                bab += cls.Level;
            }
            return bab;
        }

        /// <summary>
        /// Gets base attack bonus using classes.2da for accurate calculation.
        /// Based on reverse engineering of swkotor.exe, swkotor2.exe, nwmain.exe:
        /// - Each class has an attackbonustable column in classes.2da that references a BAB progression table
        /// - BAB progression tables (e.g., cls_atk_jedi_guardian.2da) contain BAB values per level
        /// - For multi-class characters, BAB from all classes is summed together
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005d63d0 reads classes.2da, loads attack bonus tables, calculates BAB per level
        /// </summary>
        /// <param name="gameDataProvider">Game data provider for accessing 2DA tables.</param>
        /// <returns>Total base attack bonus from all class levels.</returns>
        /// <remarks>
        /// Based on reverse engineering of swkotor.exe, swkotor2.exe, nwmain.exe:
        /// - swkotor2.exe: 0x005d63d0 @ 0x005d63d0 reads "attackbonustable" column from classes.2da
        /// - Attack bonus tables are named like "cls_atk_jedi_guardian" (referenced in classes.2da)
        /// - Each attack bonus table has rows for each level (row 0 = level 1, row 1 = level 2, etc.)
        /// - Table columns typically include "BAB" or "Value" column with the BAB value for that level
        /// - Multi-class BAB: Sum of BAB from all classes (e.g., 5 Fighter levels + 3 Wizard levels = Fighter BAB + Wizard BAB)
        /// - Based on D&D 3.5 rules: BAB is calculated per class and summed for multi-class characters
        /// </remarks>
        public int GetBaseAttackBonus(IGameDataProvider gameDataProvider)
        {
            if (gameDataProvider == null || ClassList == null || ClassList.Count == 0)
            {
                // Fallback to simplified calculation if game data provider is not available
                return GetBaseAttackBonus();
            }

            int totalBab = 0;

            // Get classes.2da table
            TwoDA classesTable = gameDataProvider.GetTable("classes");
            if (classesTable == null)
            {
                // Fallback to simplified calculation if classes.2da is not available
                return GetBaseAttackBonus();
            }

            // Calculate BAB for each class
            foreach (BaseCreatureClass cls in ClassList)
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

                TwoDARow classRow = classesTable.GetRow(cls.ClassId);
                if (classRow == null)
                {
                    continue; // Skip if class row not found
                }

                // Get attack bonus table name from classes.2da
                string attackBonusTableName = classRow.GetString("attackbonustable");
                if (string.IsNullOrEmpty(attackBonusTableName))
                {
                    continue; // Skip if attack bonus table name is missing
                }

                // Load the attack bonus table
                TwoDA attackBonusTable = gameDataProvider.GetTable(attackBonusTableName);
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

                    TwoDARow babRow = attackBonusTable.GetRow(rowIndex);
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
                        // Try to find first integer column that's not "level" or "Label"
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

        #endregion

        #region Feats

        /// <summary>
        /// Daily feat usage tracking (common across engines).
        /// Key: Feat ID, Value: Remaining uses today
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe, nwmain.exe: Feat daily usage tracking system
        /// Located via string references: Feat usage tracking in creature data structure
        /// Original implementation: Tracks remaining uses per day for feats with daily limits
        /// Daily uses reset when creature rests or new day begins
        /// Common across Odyssey and Aurora engines
        /// </remarks>
        public Dictionary<int, int> FeatDailyUses { get; set; }

        /// <summary>
        /// Checks if creature has a feat (engine-specific implementation).
        /// </summary>
        /// <param name="featId">The feat ID to check for.</param>
        /// <returns>True if the creature has the feat, false otherwise.</returns>
        /// <remarks>
        /// Engine-specific implementations:
        /// - Odyssey: Checks single FeatList
        /// - Aurora: Checks FeatList and BonusFeatList
        /// </remarks>
        public abstract bool HasFeat(int featId);

        /// <summary>
        /// Checks if a feat is currently usable (has remaining daily uses if limited).
        /// Common implementation shared across engines.
        /// </summary>
        /// <param name="featId">The feat ID to check.</param>
        /// <param name="gameDataProvider">GameDataProvider to look up feat data (engine-specific interface).</param>
        /// <returns>True if the feat is usable, false if exhausted or restricted.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe, nwmain.exe: GetHasFeat usability checking
        /// Located via string references: Feat usability checking in GetHasFeat function
        /// Original implementation: Checks if feat has remaining daily uses
        /// - If feat has UsesPerDay = -1: Always usable (unlimited or special handling)
        /// - If feat has UsesPerDay = 0: Never usable (disabled feat)
        /// - If feat has UsesPerDay > 0: Usable if remaining uses > 0
        /// - Feats with special handling (UsesPerDay = -1) may have class-level-based limits
        /// Common logic shared between Odyssey and Aurora engines
        /// </remarks>
        public virtual bool IsFeatUsable(int featId, object gameDataProvider)
        {
            if (!HasFeat(featId))
            {
                return false;
            }

            if (gameDataProvider == null)
            {
                // If GameDataProvider not available, assume feat is usable if creature has it
                return true;
            }

            // Engine-specific feat data lookup
            IFeatData featData = GetFeatData(featId, gameDataProvider);
            if (featData == null)
            {
                // Feat data not found, assume usable if creature has it
                return true;
            }

            // Check daily usage limits (common logic)
            if (featData.UsesPerDay == -1)
            {
                // Unlimited uses or special handling (e.g., based on class levels)
                return IsSpecialFeatUsable(featId, gameDataProvider);
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
        /// Interface for feat data (common properties needed for usability checking).
        /// </summary>
        public interface IFeatData
        {
            int UsesPerDay { get; }
        }

        /// <summary>
        /// Gets feat data from game data provider (engine-specific implementation).
        /// </summary>
        /// <param name="featId">The feat ID to look up.</param>
        /// <param name="gameDataProvider">GameDataProvider instance (engine-specific type).</param>
        /// <returns>IFeatData if found, null otherwise.</returns>
        protected abstract IFeatData GetFeatData(int featId, object gameDataProvider);

        /// <summary>
        /// Gets the level for a specific class ID.
        /// </summary>
        /// <param name="classId">The class ID to look up.</param>
        /// <returns>The level in that class, or 0 if the creature doesn't have that class.</returns>
        protected int GetClassLevel(int classId)
        {
            if (ClassList == null)
            {
                return 0;
            }

            foreach (BaseCreatureClass cls in ClassList)
            {
                if (cls != null && cls.ClassId == classId)
                {
                    return cls.Level;
                }
            }

            return 0;
        }

        /// <summary>
        /// Gets the class ID for a feat (engine-specific implementation).
        /// Some feats are associated with specific classes (e.g., Stunning Fist with Monk).
        /// </summary>
        /// <param name="featId">The feat ID to look up.</param>
        /// <param name="gameDataProvider">GameDataProvider to look up feat data.</param>
        /// <returns>The class ID associated with the feat, or -1 if not class-specific.</returns>
        /// <remarks>
        /// This method should be overridden by engine-specific implementations to provide
        /// feat-to-class mappings. Base implementation returns -1 (not class-specific).
        /// </remarks>
        protected virtual int GetFeatClassId(int featId, object gameDataProvider)
        {
            // Base implementation: Not class-specific
            // Engine-specific implementations should override this to provide feat-to-class mappings
            return -1;
        }

        /// <summary>
        /// Checks if a special feat (UsesPerDay = -1) is currently usable.
        /// Some feats have uses based on class levels rather than fixed daily limits.
        /// </summary>
        /// <param name="featId">The feat ID to check.</param>
        /// <param name="gameDataProvider">GameDataProvider to look up feat and class data.</param>
        /// <returns>True if the feat is usable, false otherwise.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe, nwmain.exe: Special feat usage calculation
        /// Located via string references: Class-level-based feat usage in creature data
        /// Original implementation: Some feats (e.g., Stunning Fist) have uses per day = class level
        /// 
        /// Special feat usage calculation:
        /// - Feats with UsesPerDay = -1 have uses based on class levels
        /// - Stunning Fist: Uses per day = monk level
        /// - Other special feats may use different class levels or total level
        /// - Uses are tracked in FeatDailyUses dictionary (max uses = class level)
        /// </remarks>
        protected virtual bool IsSpecialFeatUsable(int featId, object gameDataProvider)
        {
            // Special feats with UsesPerDay = -1 typically have uses based on class levels
            // Check if we have remaining uses for this special feat

            // Get the class ID associated with this feat (if any)
            int classId = GetFeatClassId(featId, gameDataProvider);
            int maxUses = 0;

            if (classId >= 0)
            {
                // Feat is associated with a specific class - use that class's level
                maxUses = GetClassLevel(classId);
            }
            else
            {
                // Feat is not class-specific - use total level as fallback
                // This handles general special feats that may use total character level
                maxUses = GetTotalLevel();
            }

            // If creature has no levels, feat is not usable
            if (maxUses <= 0)
            {
                return false;
            }

            // Check remaining uses for this special feat
            int remainingUses;
            if (!FeatDailyUses.TryGetValue(featId, out remainingUses))
            {
                // First time using this feat today - initialize with max uses (class level)
                remainingUses = maxUses;
                FeatDailyUses[featId] = remainingUses;
            }

            // Feat is usable if we have remaining uses
            return remainingUses > 0;
        }

        /// <summary>
        /// Records usage of a feat (decrements daily uses if limited).
        /// Common implementation shared across engines.
        /// </summary>
        /// <param name="featId">The feat ID that was used.</param>
        /// <param name="gameDataProvider">GameDataProvider to look up feat data.</param>
        /// <returns>True if the feat was successfully used, false if it couldn't be used.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe, nwmain.exe: Feat usage tracking
        /// Located via string references: Feat usage decrement in creature data
        /// Original implementation: Decrements remaining uses when feat is used
        /// </remarks>
        public virtual bool UseFeat(int featId, object gameDataProvider)
        {
            if (!IsFeatUsable(featId, gameDataProvider))
            {
                return false;
            }

            if (gameDataProvider == null)
            {
                return true;
            }

            IFeatData featData = GetFeatData(featId, gameDataProvider);
            if (featData == null)
            {
                return true;
            }

            // Decrement uses based on feat type
            if (featData.UsesPerDay > 0)
            {
                // Regular feat with fixed daily limit
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
            else if (featData.UsesPerDay == -1)
            {
                // Special feat with class-level-based uses
                // Decrement remaining uses (max uses = class level, tracked in IsSpecialFeatUsable)
                int remainingUses;
                if (FeatDailyUses.TryGetValue(featId, out remainingUses) && remainingUses > 0)
                {
                    remainingUses--;
                    FeatDailyUses[featId] = remainingUses;
                }
                // If not tracked yet, IsSpecialFeatUsable will initialize it on next check
            }
            // Feats with UsesPerDay = 0 are disabled and shouldn't reach here

            return true;
        }

        /// <summary>
        /// Resets daily feat uses (called when creature rests or new day begins).
        /// Common implementation shared across engines.
        /// </summary>
        /// <param name="gameDataProvider">GameDataProvider to look up feat data.</param>
        /// <param name="getFeatList">Function to get the list of feat IDs for this creature (engine-specific).</param>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe, nwmain.exe: Daily feat use reset on rest
        /// Located via string references: Feat usage reset in rest system
        /// Original implementation: Resets all feat daily uses to maximum when creature rests
        /// </remarks>
        public virtual void ResetDailyFeatUses(object gameDataProvider, System.Func<System.Collections.Generic.IEnumerable<int>> getFeatList)
        {
            if (gameDataProvider == null || getFeatList == null)
            {
                return;
            }

            // Reset uses for all feats the creature has
            foreach (int featId in getFeatList())
            {
                IFeatData featData = GetFeatData(featId, gameDataProvider);
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

        #endregion
    }

    /// <summary>
    /// Base class for creature class information shared between engines.
    /// </summary>
    public class BaseCreatureClass
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
}

