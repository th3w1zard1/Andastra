using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common.Combat
{
    /// <summary>
    /// Abstract base class for combat managers across all engines.
    /// </summary>
    /// <remarks>
    /// Base Combat Manager:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyCombatManager, AuroraCombatManager, EclipseCombatManager)
    /// - Common: Combat state tracking, attack target tracking, combat initiation/ending
    /// </remarks>
    public abstract class BaseCombatManager
    {
        protected readonly IWorld _world;
        protected readonly Dictionary<uint, IEntity> _currentTargets;
        protected readonly Dictionary<uint, IEntity> _lastAttackers;

        /// <summary>
        /// Event fired when an attack is made.
        /// </summary>
        public event EventHandler<CombatEventArgs> OnAttack;

        /// <summary>
        /// Event fired when an entity is damaged.
        /// </summary>
        public event EventHandler<CombatEventArgs> OnDamage;

        /// <summary>
        /// Event fired when an entity dies.
        /// </summary>
        public event EventHandler<CombatEventArgs> OnDeath;

        /// <summary>
        /// Event fired when a combat round ends.
        /// </summary>
        public event EventHandler<CombatEventArgs> OnRoundEnd;

        protected BaseCombatManager(IWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            _world = world;
            _currentTargets = new Dictionary<uint, IEntity>();
            _lastAttackers = new Dictionary<uint, IEntity>();
        }

        /// <summary>
        /// Gets the combat state of an entity (engine-specific implementation).
        /// Returns engine-specific combat state enum value as int.
        /// </summary>
        public abstract int GetCombatState(IEntity entity);

        /// <summary>
        /// Checks if an entity is in combat.
        /// </summary>
        public virtual bool IsInCombat(IEntity entity)
        {
            return GetCombatState(entity) != 0; // 0 = Idle/None
        }

        /// <summary>
        /// Gets the current attack target for an entity.
        /// </summary>
        [CanBeNull]
        public IEntity GetAttackTarget(IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            IEntity target;
            if (_currentTargets.TryGetValue(entity.ObjectId, out target))
            {
                return target;
            }
            return null;
        }

        /// <summary>
        /// Gets the last attacker of an entity.
        /// </summary>
        [CanBeNull]
        public IEntity GetLastAttacker(IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            IEntity attacker;
            if (_lastAttackers.TryGetValue(entity.ObjectId, out attacker))
            {
                return attacker;
            }
            return null;
        }

        /// <summary>
        /// Initiates combat between attacker and target.
        /// </summary>
        public abstract void InitiateCombat(IEntity attacker, IEntity target);

        /// <summary>
        /// Ends combat for an entity.
        /// </summary>
        public abstract void EndCombat(IEntity entity);

        /// <summary>
        /// Updates combat system (call each frame).
        /// </summary>
        public abstract void Update(float deltaTime);

        /// <summary>
        /// Fires the OnAttack event.
        /// </summary>
        protected void FireOnAttack(CombatEventArgs args)
        {
            OnAttack?.Invoke(this, args);
        }

        /// <summary>
        /// Fires the OnDamage event.
        /// </summary>
        protected void FireOnDamage(CombatEventArgs args)
        {
            OnDamage?.Invoke(this, args);
        }

        /// <summary>
        /// Fires the OnDeath event.
        /// </summary>
        protected void FireOnDeath(CombatEventArgs args)
        {
            OnDeath?.Invoke(this, args);
        }

        /// <summary>
        /// Fires the OnRoundEnd event.
        /// </summary>
        protected void FireOnRoundEnd(CombatEventArgs args)
        {
            OnRoundEnd?.Invoke(this, args);
        }
    }

    /// <summary>
    /// Combat event arguments (common across all engines).
    /// </summary>
    public class CombatEventArgs : EventArgs
    {
        public IEntity Attacker { get; set; }
        public IEntity Target { get; set; }
        public object AttackResult { get; set; } // Engine-specific attack result type
    }
}

