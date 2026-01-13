using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Combat
{
    /// <summary>
    /// Type of attack.
    /// </summary>
    /// <remarks>
    /// Attack Type Enum:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) combat system
    /// - Located via string references: Combat attack type classification
    /// - Melee: Unarmed or melee weapon attacks (uses STR modifier)
    /// - Ranged: Blaster or thrown weapon attacks (uses DEX modifier)
    /// - Force: Force power attacks (uses WIS modifier for DC)
    /// - Touch: Touch attacks (ignores armor, uses DEX for AC instead of full AC)
    /// </remarks>
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
    /// <remarks>
    /// Damage Type Enum:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) damage system
    /// - Located via string references: "DamageList" @ 0x007bf89c, "DamageValue" @ 0x007bf890
    /// - "DamageFlags" @ 0x007c01a4 (damage flags field), "DamageDie" @ 0x007c2d30 (damage die field)
    /// - "DamageDice" @ 0x007c2d3c (damage dice count field), "OffHandDamageMod" @ 0x007c2e18 (offhand damage modifier)
    /// - "OnDamaged" @ 0x007c1a80 (damaged script event), "b_damage" @ 0x007c2178 (damage constant)
    /// - "DamageDebugText" @ 0x007bf82c (damage debug text field)
    /// - Damage application: "POISONTRACE: Applying HP damage: %d\n" @ 0x007bf088 (HP damage trace)
    /// - "POISONTRACE: Applying FP damage: %d\n" @ 0x007bf060 (Force point damage trace)
    /// - Ability damage: "POISONTRACE: Applying STR damage: %d\n" @ 0x007bf038 (Strength damage)
    /// - "POISONTRACE: Applying DEX damage: %d\n" @ 0x007bf010 (Dexterity damage)
    /// - "POISONTRACE: Applying CON damage: %d\n" @ 0x007befe8 (Constitution damage)
    /// - "POISONTRACE: Applying INT damage: %d\n" @ 0x007befc0 (Intelligence damage)
    /// - "POISONTRACE: Applying WIS damage: %d\n" @ 0x007bef98 (Wisdom damage)
    /// - "POISONTRACE: Applying CHR damage: %d\n" @ 0x007bef70 (Charisma damage)
    /// - "POISONTRACE: Ability Damage duration = %d seconds\n" @ 0x007bf0b0 (ability damage duration)
    /// - Physical: Kinetic damage (melee weapons, unarmed)
    /// - Energy: Energy damage (blasters, lightsabers)
    /// - Fire/Cold/Electrical/Sonic: Elemental damage types
    /// - Ion: Ion damage (extra damage vs droids, see combat damage calculations)
    /// - DarkSide/LightSide: Force damage alignment
    /// - Universal: Bypasses all resistance (rare, used for special effects)
    /// - Damage types used for: Resistance/immunity checks, visual effects, damage reduction calculations
    /// - Damage calculation: Base damage from weapon + ability modifier + effect bonuses - damage reduction
    /// - Damage reduction: Applied per damage type (DR/Physical, DR/Energy, etc.)
    /// - Event firing: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 fires when entity takes damage
    /// - "ScriptDamaged" @ 0x007bee70 (damaged script hook field in creature templates)
    /// </remarks>
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
