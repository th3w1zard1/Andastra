using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Combat
{
    /// <summary>
    /// Type of attack.
    /// </summary>
    public enum AttackType
    {
        /// <summary>
        /// Melee attack (unarmed or melee weapon).
        /// </summary>
        Melee,

        /// <summary>
        /// Ranged attack (blaster, thrown weapon).
        /// </summary>
        Ranged,

        /// <summary>
        /// Force power attack.
        /// </summary>
        Force,

        /// <summary>
        /// Touch attack (ignores armor).
        /// </summary>
        Touch
    }

    /// <summary>
    /// Type of damage.
    /// </summary>
    public enum DamageType
    {
        /// <summary>
        /// Physical damage (kinetic).
        /// </summary>
        Physical,

        /// <summary>
        /// Energy damage (blaster, lightsaber).
        /// </summary>
        Energy,

        /// <summary>
        /// Fire damage.
        /// </summary>
        Fire,

        /// <summary>
        /// Cold damage.
        /// </summary>
        Cold,

        /// <summary>
        /// Electrical damage.
        /// </summary>
        Electrical,

        /// <summary>
        /// Sonic damage.
        /// </summary>
        Sonic,

        /// <summary>
        /// Ion damage (extra vs droids).
        /// </summary>
        Ion,

        /// <summary>
        /// Dark side Force damage.
        /// </summary>
        DarkSide,

        /// <summary>
        /// Light side Force damage.
        /// </summary>
        LightSide,

        /// <summary>
        /// Universal (bypasses all resistance).
        /// </summary>
        Universal
    }

    /// <summary>
    /// Type of saving throw.
    /// </summary>
    public enum SavingThrowType
    {
        /// <summary>
        /// Fortitude save (resists physical effects).
        /// </summary>
        Fortitude,

        /// <summary>
        /// Reflex save (avoids area effects).
        /// </summary>
        Reflex,

        /// <summary>
        /// Will save (resists mental effects).
        /// </summary>
        Will
    }

    /// <summary>
    /// Result of an attack roll.
    /// </summary>
    public class AttackResult
    {
        /// <summary>
        /// The attacking entity.
        /// </summary>
        public IEntity Attacker { get; set; }

        /// <summary>
        /// The target entity.
        /// </summary>
        public IEntity Target { get; set; }

        /// <summary>
        /// Type of attack performed.
        /// </summary>
        public AttackType AttackType { get; set; }

        /// <summary>
        /// Whether the attack hit.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The natural d20 roll (1-20).
        /// </summary>
        public int NaturalRoll { get; set; }

        /// <summary>
        /// Total attack roll (natural + bonuses).
        /// </summary>
        public int TotalRoll { get; set; }

        /// <summary>
        /// Target's defense value.
        /// </summary>
        public int TargetDefense { get; set; }

        /// <summary>
        /// Whether this roll threatened a critical hit.
        /// </summary>
        public bool IsCriticalThreat { get; set; }

        /// <summary>
        /// Whether the critical was confirmed.
        /// </summary>
        public bool IsCriticalHit { get; set; }

        /// <summary>
        /// Reason for miss (if applicable).
        /// </summary>
        public string Reason { get; set; }
    }

    /// <summary>
    /// Result of damage dealt.
    /// </summary>
    public class DamageResult
    {
        /// <summary>
        /// The damage source.
        /// </summary>
        public IEntity Source { get; set; }

        /// <summary>
        /// The damage target.
        /// </summary>
        public IEntity Target { get; set; }

        /// <summary>
        /// Type of damage dealt.
        /// </summary>
        public DamageType DamageType { get; set; }

        /// <summary>
        /// Base damage before modifiers.
        /// </summary>
        public int BaseDamage { get; set; }

        /// <summary>
        /// Critical hit multiplier applied.
        /// </summary>
        public int Multiplier { get; set; }

        /// <summary>
        /// Damage reduction applied.
        /// </summary>
        public int DamageReduction { get; set; }

        /// <summary>
        /// Final damage after all modifiers.
        /// </summary>
        public int FinalDamage { get; set; }

        /// <summary>
        /// Whether the damage was completely absorbed.
        /// </summary>
        public bool WasAbsorbed { get { return FinalDamage == 0 && BaseDamage > 0; } }
    }

    /// <summary>
    /// Result of a saving throw.
    /// </summary>
    public class SavingThrowResult
    {
        /// <summary>
        /// The entity making the save.
        /// </summary>
        public IEntity Entity { get; set; }

        /// <summary>
        /// Type of saving throw.
        /// </summary>
        public SavingThrowType Type { get; set; }

        /// <summary>
        /// The difficulty class to beat.
        /// </summary>
        public int DC { get; set; }

        /// <summary>
        /// Whether the save succeeded.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// The natural d20 roll.
        /// </summary>
        public int NaturalRoll { get; set; }

        /// <summary>
        /// Total saving throw roll.
        /// </summary>
        public int TotalRoll { get; set; }
    }
}
