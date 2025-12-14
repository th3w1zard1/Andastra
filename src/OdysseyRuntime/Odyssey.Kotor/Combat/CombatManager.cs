using System;
using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Components;
using Odyssey.Kotor.Systems;

namespace Odyssey.Kotor.Combat
{
    /// <summary>
    /// Combat state for an entity.
    /// </summary>
    public enum CombatState
    {
        /// <summary>
        /// Not in combat.
        /// </summary>
        Idle = 0,

        /// <summary>
        /// In combat, actively fighting.
        /// </summary>
        InCombat = 1,

        /// <summary>
        /// Fleeing from combat.
        /// </summary>
        Fleeing = 2,

        /// <summary>
        /// Dead.
        /// </summary>
        Dead = 3
    }

    /// <summary>
    /// Combat event arguments.
    /// </summary>
    public class CombatEventArgs : EventArgs
    {
        public IEntity Attacker { get; set; }
        public IEntity Target { get; set; }
        public AttackRollResult AttackResult { get; set; }
    }

    /// <summary>
    /// Manages combat encounters in KOTOR.
    /// </summary>
    /// <remarks>
    /// Combat System Overview:
    /// 
    /// 1. Combat Initiation:
    ///    - Perception detects hostile
    ///    - Attack action queued
    ///    - Combat state set to InCombat
    /// 
    /// 2. Combat Round (~3 seconds):
    ///    - Schedule attacks based on BAB
    ///    - Execute attacks at appropriate times
    ///    - Fire OnAttacked/OnDamaged scripts
    /// 
    /// 3. Combat Resolution:
    ///    - Apply damage
    ///    - Check for death
    ///    - Fire OnDeath script
    ///    - Award XP
    /// 
    /// 4. Combat End:
    ///    - No hostiles in perception
    ///    - Clear combat state
    ///    - Fire OnCombatRoundEnd
    /// </remarks>
    public class CombatManager
    {
        private readonly IWorld _world;
        private readonly DamageCalculator _damageCalc;
        private readonly FactionManager _factionManager;

        private readonly Dictionary<uint, CombatState> _combatStates;
        private readonly Dictionary<uint, CombatRound> _activeRounds;
        private readonly Dictionary<uint, IEntity> _currentTargets;

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

        public CombatManager(IWorld world, FactionManager factionManager)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _factionManager = factionManager;
            _damageCalc = new DamageCalculator();
            _combatStates = new Dictionary<uint, CombatState>();
            _activeRounds = new Dictionary<uint, CombatRound>();
            _currentTargets = new Dictionary<uint, IEntity>();
        }

        #region State Access

        /// <summary>
        /// Gets the combat state of an entity.
        /// </summary>
        public CombatState GetCombatState(IEntity entity)
        {
            if (entity == null)
            {
                return CombatState.Idle;
            }

            CombatState state;
            if (_combatStates.TryGetValue(entity.ObjectId, out state))
            {
                return state;
            }
            return CombatState.Idle;
        }

        /// <summary>
        /// Checks if an entity is in combat.
        /// </summary>
        public bool IsInCombat(IEntity entity)
        {
            return GetCombatState(entity) == CombatState.InCombat;
        }

        /// <summary>
        /// Gets the current attack target for an entity.
        /// </summary>
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
        /// Gets the active combat round for an entity.
        /// </summary>
        public CombatRound GetActiveRound(IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            CombatRound round;
            if (_activeRounds.TryGetValue(entity.ObjectId, out round))
            {
                return round;
            }
            return null;
        }

        #endregion

        #region Combat Control

        /// <summary>
        /// Initiates combat between attacker and target.
        /// </summary>
        public void InitiateCombat(IEntity attacker, IEntity target)
        {
            if (attacker == null || target == null)
            {
                return;
            }

            // Set combat states
            SetCombatState(attacker, CombatState.InCombat);
            SetCombatState(target, CombatState.InCombat);

            // Set attack target
            _currentTargets[attacker.ObjectId] = target;

            // Create combat round
            StartNewRound(attacker, target);

            // Set temporary hostility
            if (_factionManager != null)
            {
                _factionManager.SetTemporaryHostile(target, attacker, true);
            }
        }

        /// <summary>
        /// Ends combat for an entity.
        /// </summary>
        public void EndCombat(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _combatStates.Remove(entity.ObjectId);
            _activeRounds.Remove(entity.ObjectId);
            _currentTargets.Remove(entity.ObjectId);
        }

        /// <summary>
        /// Sets the combat state for an entity.
        /// </summary>
        private void SetCombatState(IEntity entity, CombatState state)
        {
            if (entity != null)
            {
                _combatStates[entity.ObjectId] = state;
            }
        }

        /// <summary>
        /// Starts a new combat round.
        /// </summary>
        private void StartNewRound(IEntity attacker, IEntity target)
        {
            var round = new CombatRound(attacker, target);

            // Schedule attacks based on BAB
            var stats = attacker.GetComponent<IStatsComponent>();
            int bab = stats != null ? stats.BaseAttackBonus : 0;

            // TODO: Check for dual wielding
            bool isDualWielding = false;
            bool hasTWF = false; // Check for Two-Weapon Fighting feat

            int numAttacks = _damageCalc.CalculateAttacksPerRound(bab, isDualWielding);

            for (int i = 0; i < numAttacks; i++)
            {
                bool isOffhand = isDualWielding && i == numAttacks - 1;
                int attackBonus = _damageCalc.CalculateIterativeAttackBonus(bab, i, isOffhand, hasTWF);

                var attack = new AttackAction(attacker, target)
                {
                    AttackBonus = attackBonus,
                    IsOffhand = isOffhand,
                    WeaponDamageRoll = "1d8", // TODO: Get from equipped weapon
                    DamageBonus = _damageCalc.CalculateMeleeDamageBonus(attacker, false, isOffhand),
                    CriticalThreat = 20, // TODO: Get from weapon
                    CriticalMultiplier = 2
                };

                round.ScheduleAttack(attack);
            }

            _activeRounds[attacker.ObjectId] = round;
        }

        #endregion

        #region Update

        /// <summary>
        /// Updates all active combat rounds.
        /// </summary>
        public void Update(float deltaTime)
        {
            // Copy keys to avoid modification during iteration
            var attackerIds = new List<uint>(_activeRounds.Keys);

            foreach (uint attackerId in attackerIds)
            {
                CombatRound round;
                if (!_activeRounds.TryGetValue(attackerId, out round))
                {
                    continue;
                }

                // Check if attacker is dead
                IEntity attacker = _world.GetEntity(attackerId);
                if (attacker == null || GetCombatState(attacker) == CombatState.Dead)
                {
                    _activeRounds.Remove(attackerId);
                    continue;
                }

                // Update round
                AttackAction attack = round.Update(deltaTime);

                // Execute attack if one is ready
                if (attack != null)
                {
                    ExecuteAttack(attack);
                }

                // Check if round is complete
                if (round.IsComplete)
                {
                    OnRoundEnd?.Invoke(this, new CombatEventArgs
                    {
                        Attacker = attacker,
                        Target = round.Target
                    });

                    // Start new round if still in combat
                    if (IsInCombat(attacker) && round.Target != null)
                    {
                        var targetStats = round.Target.GetComponent<IStatsComponent>();
                        if (targetStats != null && !targetStats.IsDead)
                        {
                            StartNewRound(attacker, round.Target);
                        }
                        else
                        {
                            // Target dead, end combat
                            EndCombat(attacker);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Executes a single attack.
        /// </summary>
        private void ExecuteAttack(AttackAction attack)
        {
            if (attack == null || attack.Attacker == null || attack.Target == null)
            {
                return;
            }

            // Resolve attack roll
            AttackRollResult result = _damageCalc.ResolveAttack(attack);

            // Fire attack event
            OnAttack?.Invoke(this, new CombatEventArgs
            {
                Attacker = attack.Attacker,
                Target = attack.Target,
                AttackResult = result
            });

            // Apply damage if hit
            if (result.Result == AttackResult.Hit ||
                result.Result == AttackResult.CriticalHit ||
                result.Result == AttackResult.AutomaticHit)
            {
                int actualDamage = _damageCalc.ApplyDamage(attack.Target, result.TotalDamage, attack.DamageType);

                // Fire damage event
                OnDamage?.Invoke(this, new CombatEventArgs
                {
                    Attacker = attack.Attacker,
                    Target = attack.Target,
                    AttackResult = result
                });

                // Check for death
                var targetStats = attack.Target.GetComponent<IStatsComponent>();
                if (targetStats != null && targetStats.IsDead)
                {
                    HandleDeath(attack.Target, attack.Attacker);
                }
            }
        }

        /// <summary>
        /// Handles entity death.
        /// </summary>
        private void HandleDeath(IEntity victim, IEntity killer)
        {
            SetCombatState(victim, CombatState.Dead);

            // Fire death event
            OnDeath?.Invoke(this, new CombatEventArgs
            {
                Attacker = killer,
                Target = victim
            });

            // End combat for victim
            EndCombat(victim);

            // Award XP to killer
            // TODO: Calculate XP based on CR
        }

        #endregion

        #region Utility

        /// <summary>
        /// Gets all entities currently in combat.
        /// </summary>
        public IEnumerable<IEntity> GetEntitiesInCombat()
        {
            foreach (var kvp in _combatStates)
            {
                if (kvp.Value == CombatState.InCombat)
                {
                    IEntity entity = _world.GetEntity(kvp.Key);
                    if (entity != null)
                    {
                        yield return entity;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if two entities are in combat with each other.
        /// </summary>
        public bool AreInCombatWith(IEntity a, IEntity b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            IEntity targetOfA = GetAttackTarget(a);
            IEntity targetOfB = GetAttackTarget(b);

            return (targetOfA == b) || (targetOfB == a);
        }

        /// <summary>
        /// Forces an entity to target a new enemy.
        /// </summary>
        public void SetAttackTarget(IEntity attacker, IEntity newTarget)
        {
            if (attacker == null)
            {
                return;
            }

            _currentTargets[attacker.ObjectId] = newTarget;

            if (newTarget != null && IsInCombat(attacker))
            {
                // Abort current round and start new one
                CombatRound round;
                if (_activeRounds.TryGetValue(attacker.ObjectId, out round))
                {
                    round.Abort();
                }
                StartNewRound(attacker, newTarget);
            }
        }

        #endregion
    }
}
