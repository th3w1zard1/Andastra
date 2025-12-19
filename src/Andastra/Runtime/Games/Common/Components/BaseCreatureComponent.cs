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

