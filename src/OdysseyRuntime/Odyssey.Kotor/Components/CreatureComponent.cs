using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for creature entities (NPCs and PCs).
    /// </summary>
    /// <remarks>
    /// Based on UTC file format documentation.
    /// Stores creature-specific data: stats, appearance, equipment, etc.
    /// </remarks>
    public class CreatureComponent : IComponent
    {
        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public CreatureComponent()
        {
            TemplateResRef = string.Empty;
            Tag = string.Empty;
            FeatList = new List<int>();
            ClassList = new List<CreatureClass>();
            EquippedItems = new Dictionary<int, string>();
            KnownPowers = new List<int>();
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
            foreach (var cls in ClassList)
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
            foreach (var cls in ClassList)
            {
                // Simplified BAB calculation based on class type
                // Full implementation would use classes.2da
                bab += cls.Level;
            }
            return bab;
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
        /// Checks if creature has a feat.
        /// </summary>
        public bool HasFeat(int featId)
        {
            return FeatList.Contains(featId);
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
