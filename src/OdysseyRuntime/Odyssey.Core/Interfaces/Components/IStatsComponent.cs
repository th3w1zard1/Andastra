using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for creature stats (HP, abilities, etc.)
    /// </summary>
    public interface IStatsComponent : IComponent
    {
        /// <summary>
        /// Current hit points.
        /// </summary>
        int CurrentHP { get; set; }
        
        /// <summary>
        /// Maximum hit points.
        /// </summary>
        int MaxHP { get; }

        /// <summary>
        /// Current Force points.
        /// </summary>
        int CurrentFP { get; set; }

        /// <summary>
        /// Maximum Force points.
        /// </summary>
        int MaxFP { get; }
        
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
    }
}

