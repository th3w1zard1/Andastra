using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for creature stats (HP, abilities, etc.)
    /// </summary>
    /// <remarks>
    /// Stats Component Interface:
    /// - Based on swkotor2.exe stats system
    /// - Located via string references: "CurrentHP" @ 0x007c1b40, "Max_HPs" @ 0x007cb714, "ArmorClassColumn" @ 0x007c1230, "ArmorClass" @ 0x007c0b10
    /// - HP fields: "CurrentHP" @ 0x007c1b40 (current HP field), "Max_HPs" @ 0x007cb714 (max HP field)
    /// - "InCombatHPBase" @ 0x007bf224 (in-combat HP base), "OutOfCombatHPBase" @ 0x007bf210 (out-of-combat HP base)
    /// - "TimePerHP" @ 0x007bf234 (time per HP regeneration), "Min1HP" @ 0x007c1b28 (minimum 1 HP flag)
    /// - "DAM_HP" @ 0x007bf130 (HP damage constant), "POISONTRACE: Applying HP damage: %d\n" @ 0x007bf088 (HP damage trace)
    /// - Debug: "    HP: " @ 0x007caf24, "    MaxHP: " @ 0x007cb15c, "CurrentHP: " @ 0x007cb168
    /// - HP: Current and maximum hit points (D20 system)
    /// - FP: Current and maximum Force points (KOTOR/TSL system)
    /// - Abilities: STR, DEX, CON, INT, WIS, CHA (D20 standard abilities, modifiers = (score - 10) / 2)
    /// - BaseAttackBonus: Base attack bonus from class levels
    /// - ArmorClass: Total AC = 10 + AC modifiers (armor, shield, DEX, etc.)
    /// - Saves: Fortitude, Reflex, Will (D20 saving throws)
    /// - WalkSpeed/RunSpeed: Movement speeds in meters per second
    /// - Skills: GetSkillRank returns skill rank (0 = untrained, positive = trained rank)
    /// - IsDead: True when CurrentHP <= 0
    /// - HP regeneration: InCombatHPBase and OutOfCombatHPBase control HP regen rates (TimePerHP for regen timing)
    /// - Min1HP: Prevents HP from going below 1 (creatures cannot die from ability damage, only HP damage)
    /// - Based on swkotor2.exe stats calculation from classes.2da and appearance.2da tables
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
        /// Character level (total class levels).
        /// </summary>
        int Level { get; }
    }
}

