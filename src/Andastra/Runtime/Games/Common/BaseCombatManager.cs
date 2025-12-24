using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of combat management shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Combat Manager Implementation:
    /// - Common combat resolution and turn management across all engines
    /// - Handles attack rolls, damage calculation, effect application
    /// - Provides framework for initiative, rounds, and combat state
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Combat system functions
    /// - swkotor2.exe: Advanced combat mechanics with force powers, feats
    /// - nwmain.exe: D20-based combat system
    /// - daorigins.exe: Tactical combat with positioning and abilities
    /// - DragonAge2.exe: Enhanced tactical combat with improved party coordination, ability system, and real-time action
    ///   Combat state strings: "GameModeCombat" @ 0x00beaf3c, "InCombat" @ 0x00bf4c10, "CombatTarget" @ 0x00bf4dc0
    ///   "Combatant" @ 0x00bf4664, "Combat_%u" @ 0x00be0ba4, "BInCombatMode" @ 0x00beeed2, "AutoPauseCombat" @ 0x00bf6f9c
    ///   Ability system: "PerformAbilityMessage" @ 0x00be3010, "PerformAbilityOnTargetUnderCursorMessage" @ 0x00be45b0
    ///   "PerformAbilityOnSelectedTargetMessage" @ 0x00be4708, "PerformAbilityOnPlayerMessage" @ 0x00be4754
    ///   "FireAOEAbilityMessage" @ 0x00be4790, "CancelAbilityTargetingMessage" @ 0x00be4604
    ///   Weapon system: "SwitchWeaponSetMessage" @ 0x00be3bbc, "SwitchWeaponSetNowMessage" @ 0x00be3bec
    ///   "ActiveWeaponSet" @ 0x00bf4ca0, "CanSwitchWeaponSets" @ 0x00bf4bc8
    ///   "InactiveWeaponSetOwners" @ 0x00beb1a0, "InactiveWeaponSetItems" @ 0x00beb1b8
    ///   Damage display: "ShowDamageFloaties" @ 0x00bf6f6c, "TargetAttack" @ 0x00bf6878
    ///   Architecture: Eclipse engine uses UnrealScript message-based combat system (different from Odyssey/Aurora NCS VM)
    ///   Combat features: Real-time action combat with tactical pause, ability combos, weapon set switching
    ///   Party coordination: Enhanced AI for party member coordination and ability selection
    /// - Common combat concepts: Initiative, rounds, attacks, damage, effects
    ///
    /// Common functionality across engines:
    /// - Combat state management (in/out of combat)
    /// - Initiative tracking and turn order
    /// - Attack resolution and hit/miss determination
    /// - Damage calculation and application
    /// - Effect queuing and duration tracking
    /// - Combat event generation and handling
    /// - Round-based or real-time combat modes
    /// </remarks>
    [PublicAPI]
    public abstract class BaseCombatManager
    {
        protected readonly IWorld _world;
        protected readonly List<IEntity> _combatants = new List<IEntity>();
        protected bool _isCombatActive;

        protected BaseCombatManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// Gets whether combat is currently active.
        /// </summary>
        public bool IsCombatActive => _isCombatActive;

        /// <summary>
        /// Gets all current combatants.
        /// </summary>
        public IEnumerable<IEntity> Combatants => _combatants;

        /// <summary>
        /// Starts combat with the given participants.
        /// </summary>
        /// <remarks>
        /// Common combat initialization across all engines.
        /// Sets up initiative, positions combatants, triggers combat start events.
        /// Engine-specific subclasses handle engine-specific setup (party AI, etc.).
        /// </remarks>
        public virtual void StartCombat(IEnumerable<IEntity> participants)
        {
            if (_isCombatActive)
                return;

            _combatants.Clear();
            _combatants.AddRange(participants);

            _isCombatActive = true;
            OnCombatStarted();
        }

        /// <summary>
        /// Ends the current combat.
        /// </summary>
        /// <remarks>
        /// Common combat cleanup.
        /// Awards XP, removes temporary effects, triggers victory/defeat events.
        /// Engine-specific subclasses handle engine-specific rewards.
        /// </remarks>
        public virtual void EndCombat()
        {
            if (!_isCombatActive)
                return;

            _isCombatActive = false;
            OnCombatEnded();
            _combatants.Clear();
        }

        /// <summary>
        /// Adds a combatant to the current combat.
        /// </summary>
        /// <remarks>
        /// Allows dynamic joining of combat.
        /// Recalculates initiative and turn order.
        /// Common across all engines.
        /// </remarks>
        public virtual void AddCombatant(IEntity entity)
        {
            if (entity == null || _combatants.Contains(entity))
                return;

            _combatants.Add(entity);
            OnCombatantAdded(entity);
        }

        /// <summary>
        /// Removes a combatant from combat.
        /// </summary>
        /// <remarks>
        /// Handles combatant defeat or withdrawal.
        /// May end combat if no valid combatants remain.
        /// Common across all engines.
        /// </remarks>
        public virtual void RemoveCombatant(IEntity entity)
        {
            if (entity == null || !_combatants.Contains(entity))
                return;

            _combatants.Remove(entity);
            OnCombatantRemoved(entity);

            // Check if combat should end
            if (_combatants.Count == 0)
            {
                EndCombat();
            }
        }

        /// <summary>
        /// Resolves an attack from attacker to target.
        /// </summary>
        /// <remarks>
        /// Common attack resolution framework.
        /// Calculates hit/miss, damage, critical hits.
        /// Engine-specific subclasses implement engine-specific rules (D20, etc.).
        /// </remarks>
        public abstract CombatResult ResolveAttack(IEntity attacker, IEntity target, AttackData attack);

        /// <summary>
        /// Applies damage to a target.
        /// </summary>
        /// <remarks>
        /// Common damage application.
        /// Handles damage types, resistances, immunities.
        /// Triggers damage events and death checks.
        /// </remarks>
        public abstract void ApplyDamage(IEntity target, DamageData damage);

        /// <summary>
        /// Updates the combat state.
        /// </summary>
        /// <remarks>
        /// Called each frame during combat.
        /// Handles turn progression, effect ticks, AI updates.
        /// Engine-specific timing and mechanics.
        /// </remarks>
        public abstract void Update(float deltaTime);

        /// <summary>
        /// Called when combat starts.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific combat start logic.
        /// Subclasses can override for additional setup.
        /// </remarks>
        protected virtual void OnCombatStarted()
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when combat ends.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific combat end logic.
        /// Subclasses can override for cleanup.
        /// </remarks>
        protected virtual void OnCombatEnded()
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when a combatant is added.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific combatant addition.
        /// Subclasses can override for setup.
        /// </remarks>
        protected virtual void OnCombatantAdded(IEntity entity)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when a combatant is removed.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific combatant removal.
        /// Subclasses can override for cleanup.
        /// </remarks>
        protected virtual void OnCombatantRemoved(IEntity entity)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Gets combat statistics.
        /// </summary>
        /// <remarks>
        /// Returns combat metrics for debugging.
        /// Useful for performance monitoring and testing.
        /// </remarks>
        public virtual CombatStats GetStats()
        {
            return new CombatStats
            {
                IsActive = _isCombatActive,
                CombatantCount = _combatants.Count,
                Duration = 0 // Engine-specific implementation
            };
        }
    }

    /// <summary>
    /// Combat result data.
    /// </summary>
    public struct CombatResult
    {
        /// <summary>
        /// Whether the attack hit.
        /// </summary>
        public bool Hit;

        /// <summary>
        /// Whether it was a critical hit.
        /// </summary>
        public bool Critical;

        /// <summary>
        /// Damage dealt (if any).
        /// </summary>
        public DamageData Damage;

        /// <summary>
        /// Attack roll result.
        /// </summary>
        public int AttackRoll;

        /// <summary>
        /// Defense/AC value.
        /// </summary>
        public int DefenseValue;
    }

    /// <summary>
    /// Attack data structure.
    /// </summary>
    public struct AttackData
    {
        /// <summary>
        /// Attack bonus.
        /// </summary>
        public int AttackBonus;

        /// <summary>
        /// Damage dice and type.
        /// </summary>
        public string DamageDice;

        /// <summary>
        /// Damage bonus.
        /// </summary>
        public int DamageBonus;

        /// <summary>
        /// Damage type.
        /// </summary>
        public DamageType DamageType;

        /// <summary>
        /// Weapon or attack type.
        /// </summary>
        public string AttackType;
    }

    /// <summary>
    /// Damage data structure.
    /// </summary>
    public struct DamageData
    {
        /// <summary>
        /// Amount of damage.
        /// </summary>
        public int Amount;

        /// <summary>
        /// Type of damage.
        /// </summary>
        public DamageType Type;

        /// <summary>
        /// Source of the damage.
        /// </summary>
        public IEntity Source;
    }

    /// <summary>
    /// Damage type enumeration.
    /// </summary>
    public enum DamageType
    {
        /// <summary>
        /// Physical damage.
        /// </summary>
        Physical,

        /// <summary>
        /// Fire damage.
        /// </summary>
        Fire,

        /// <summary>
        /// Cold damage.
        /// </summary>
        Cold,

        /// <summary>
        /// Electric damage.
        /// </summary>
        Electric,

        /// <summary>
        /// Acid damage.
        /// </summary>
        Acid,

        /// <summary>
        /// Sonic damage.
        /// </summary>
        Sonic,

        /// <summary>
        /// Force damage (energy).
        /// </summary>
        Force,

        /// <summary>
        /// Poison damage.
        /// </summary>
        Poison,

        /// <summary>
        /// Healing (negative damage).
        /// </summary>
        Healing
    }

    /// <summary>
    /// Combat statistics.
    /// </summary>
    public struct CombatStats
    {
        /// <summary>
        /// Whether combat is active.
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// Number of combatants.
        /// </summary>
        public int CombatantCount;

        /// <summary>
        /// Combat duration in seconds.
        /// </summary>
        public float Duration;
    }
}
