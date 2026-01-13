using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for creature stats (HP, abilities, etc.)
    /// </summary>
    /// <remarks>
    /// Stats Component Interface:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse)
    /// - Base interface for engine-specific implementations (OdysseyStatsComponent, AuroraStatsComponent, EclipseStatsComponent)
    /// - Engine-specific details and function addresses are documented in implementation classes, not in this interface
    ///
    /// Common functionality across all engines:
    /// - HP: Current and maximum hit points (D20 system)
    /// - Abilities: STR, DEX, CON, INT, WIS, CHA (D20 standard abilities, modifiers = (score - 10) / 2)
    /// - BaseAttackBonus: Base attack bonus from class levels
    /// - ArmorClass: Total AC = 10 + AC modifiers (armor, shield, DEX, etc.) - exact calculation varies by engine
    /// - Saves: Fortitude, Reflex, Will (D20 saving throws)
    /// - WalkSpeed/RunSpeed: Movement speeds in meters per second
    /// - Skills: GetSkillRank returns skill rank (0 = untrained, positive = trained rank) - skill count varies by engine
    /// - IsDead: True when CurrentHP <= 0
    /// - Level: Character level (total class levels)
    /// - HasSpell: Checks if creature knows a spell/ability
    ///
    /// Engine-specific notes:
    /// - Force Points (CurrentFP, MaxFP): Odyssey-specific (KOTOR/TSL only). Aurora/Eclipse implementations return 0.
    /// - Skill systems: Odyssey has 8 skills, Aurora has 27 skills, Eclipse varies
    /// - AC calculation: Exact formula varies (Aurora includes size modifiers, others may differ)
    /// - HP regeneration: Implementation details vary by engine
    ///
    /// For engine-specific implementation details and function addresses, see:
    /// - Odyssey: StatsComponent (swkotor.exe, swkotor2.exe)
    /// - Aurora: AuroraStatsComponent (nwmain.exe)
    /// - Eclipse: EclipseStatsComponent (daorigins.exe, DragonAge2.exe)
    /// </remarks>
    public interface IStatsComponent : IComponent
    {
        /// <summary>
        /// Current hit points.
        /// </summary>
        int CurrentHP { get; set; }

        /// <summary>
        /// Maximum hit points.
        /// </summary>
        int MaxHP { get; set; }

        /// <summary>
        /// Current Force points.
        /// </summary>
        int CurrentFP { get; set; }

        /// <summary>
        /// Maximum Force points.
        /// </summary>
        int MaxFP { get; set; }

        /// <summary>
        /// Gets an ability score.
        /// </summary>
        int GetAbility(Ability ability);

        /// <summary>
        /// Sets an ability score.
        /// </summary>
        void SetAbility(Ability ability, int value);

        /// <summary>
        /// Gets the modifier for an ability.
        /// </summary>
        int GetAbilityModifier(Ability ability);

        /// <summary>
        /// Whether the creature is dead.
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// Base attack bonus.
        /// </summary>
        int BaseAttackBonus { get; }

        /// <summary>
        /// Armor class / defense.
        /// </summary>
        int ArmorClass { get; }

        /// <summary>
        /// Fortitude save.
        /// </summary>
        int FortitudeSave { get; }

        /// <summary>
        /// Reflex save.
        /// </summary>
        int ReflexSave { get; }

        /// <summary>
        /// Will save.
        /// </summary>
        int WillSave { get; }

        /// <summary>
        /// Walk speed in meters per second.
        /// </summary>
        float WalkSpeed { get; }

        /// <summary>
        /// Run speed in meters per second.
        /// </summary>
        float RunSpeed { get; }

        /// <summary>
        /// Gets the skill rank for a given skill.
        /// </summary>
        /// <param name="skill">Skill ID (SKILL_SECURITY = 6, etc.)</param>
        /// <returns>Skill rank, or 0 if untrained, or -1 if skill doesn't exist</returns>
        int GetSkillRank(int skill);

        /// <summary>
        /// Checks if the creature knows a spell/Force power.
        /// </summary>
        /// <param name="spellId">Spell ID (row index in spells.2da)</param>
        /// <returns>True if the creature knows the spell, false otherwise</returns>
        bool HasSpell(int spellId);

        /// <summary>
        /// Gets all known spells/Force powers.
        /// </summary>
        /// <returns>Enumerable of spell IDs (row indices in spells.2da)</returns>
        System.Collections.Generic.IEnumerable<int> GetKnownSpells();

        /// <summary>
        /// Character level (total class levels).
        /// </summary>
        int Level { get; }

        /// <summary>
        /// Adds an AC bonus from an effect.
        /// </summary>
        /// <param name="bonus">AC bonus to add</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AC effects modify total AC calculation
        /// Located via string references: "ArmorClass" @ 0x007c42a8, "EffectACIncrease" @ routine 115
        /// Original implementation: AC effects add to total AC (10 + DEX + Armor + Natural + Deflection + Effects)
        /// </remarks>
        void AddEffectACBonus(int bonus);

        /// <summary>
        /// Removes an AC bonus from an effect.
        /// </summary>
        /// <param name="bonus">AC bonus to remove</param>
        void RemoveEffectACBonus(int bonus);

        /// <summary>
        /// Adds an attack bonus from an effect.
        /// </summary>
        /// <param name="bonus">Attack bonus to add</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Attack effects modify total attack bonus
        /// Located via string references: "EffectAttackIncrease" @ routine 118
        /// Original implementation: Attack effects add to total attack (BAB + STR/DEX + Effects)
        /// </remarks>
        void AddEffectAttackBonus(int bonus);

        /// <summary>
        /// Removes an attack bonus from an effect.
        /// </summary>
        /// <param name="bonus">Attack bonus to remove</param>
        void RemoveEffectAttackBonus(int bonus);
    }
}

