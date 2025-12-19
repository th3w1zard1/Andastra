using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Games.Common.Components
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
    ///   - Eclipse: daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe - uses talents/abilities, not feats
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
        /// Gets base attack bonus.
        /// </summary>
        public int GetBaseAttackBonus()
        {
            int bab = 0;
            foreach (BaseCreatureClass cls in ClassList)
            {
                // Simplified BAB calculation based on class type
                // Full implementation would use classes.2da
                bab += cls.Level;
            }
            return bab;
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
        /// - Odyssey: Checks single FeatList (swkotor.exe, swkotor2.exe)
        /// - Aurora: Checks FeatList and BonusFeatList (nwmain.exe: CNWSCreatureStats::HasFeat @ 0x14040b900)
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
        protected interface IFeatData
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
        /// For now, we assume special feats are always usable if the creature has them
        /// Full implementation would check specific feat types and calculate uses based on class levels
        /// </remarks>
        protected virtual bool IsSpecialFeatUsable(int featId, object gameDataProvider)
        {
            // Special feats with UsesPerDay = -1 typically have uses based on class levels
            // For now, we assume they're always usable if the creature has the feat
            // Full implementation would check specific feat types:
            // - Stunning Fist: Uses per day = monk level
            // - Other special feats may have different calculations
            return true;
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

