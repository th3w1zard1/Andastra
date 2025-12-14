using System;
using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Combat
{
    /// <summary>
    /// Combat state for an entity.
    /// </summary>
    public enum CombatState
    {
        /// <summary>
        /// Not in combat.
        /// </summary>
        None,

        /// <summary>
        /// In combat but not currently attacking.
        /// </summary>
        InCombat,

        /// <summary>
        /// Currently executing an attack.
        /// </summary>
        Attacking,

        /// <summary>
        /// Waiting for combat round to end.
        /// </summary>
        Waiting
    }

    /// <summary>
    /// D20-based combat system for KOTOR.
    /// Handles combat rounds, attack resolution, damage calculation, and effects.
    /// </summary>
    /// <remarks>
    /// KOTOR Combat System Overview:
    /// - 3-second combat rounds (approximately)
    /// - D20 attack roll + attack bonus vs defense
    /// - Critical hits on natural 20 (threatened), confirm with second roll
    /// - Damage = weapon damage + modifiers - damage reduction
    /// - Effects have duration in rounds or permanent
    /// 
    /// Combat Round Phases:
    /// 1. Determine attack order (initiative)
    /// 2. Process queued attacks
    /// 3. Resolve each attack (roll, damage, effects)
    /// 4. Update combat state
    /// </remarks>
    public class CombatSystem
    {
        /// <summary>
        /// Combat round duration in seconds.
        /// </summary>
        public const float RoundDuration = 3.0f;

        /// <summary>
        /// Random number generator for dice rolls.
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// The world containing combat entities.
        /// </summary>
        private readonly IWorld _world;

        /// <summary>
        /// Currently active combat encounters.
        /// </summary>
        private readonly Dictionary<uint, CombatEncounter> _encounters;

        /// <summary>
        /// Time accumulator for combat round timing.
        /// </summary>
        private float _roundTimer;

        /// <summary>
        /// Current combat round number.
        /// </summary>
        public int CurrentRound { get; private set; }

        /// <summary>
        /// Event fired when an attack is resolved.
        /// </summary>
        public event Action<AttackResult> OnAttackResolved;

        /// <summary>
        /// Event fired when damage is dealt.
        /// </summary>
        public event Action<DamageResult> OnDamageDealt;

        /// <summary>
        /// Event fired when an entity dies.
        /// </summary>
        public event Action<IEntity, IEntity> OnEntityDeath;

        /// <summary>
        /// Event fired when combat begins for an entity.
        /// </summary>
        public event Action<IEntity> OnCombatStart;

        /// <summary>
        /// Event fired when combat ends for an entity.
        /// </summary>
        public event Action<IEntity> OnCombatEnd;

        public CombatSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _random = new Random();
            _encounters = new Dictionary<uint, CombatEncounter>();
            _roundTimer = 0f;
            CurrentRound = 0;
        }

        /// <summary>
        /// Updates the combat system.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        public void Update(float deltaTime)
        {
            _roundTimer += deltaTime;

            // Check for round advancement
            if (_roundTimer >= RoundDuration)
            {
                _roundTimer -= RoundDuration;
                CurrentRound++;
                ProcessCombatRound();
            }

            // Update ongoing combat encounters
            foreach (var encounter in _encounters.Values)
            {
                UpdateEncounter(encounter, deltaTime);
            }

            // Cleanup finished encounters
            CleanupFinishedEncounters();
        }

        /// <summary>
        /// Initiates combat between two entities.
        /// </summary>
        public void InitiateCombat(IEntity attacker, IEntity target)
        {
            if (attacker == null || target == null)
            {
                return;
            }

            // Get or create encounter for attacker
            CombatEncounter encounter;
            if (!_encounters.TryGetValue(attacker.ObjectId, out encounter))
            {
                encounter = new CombatEncounter(attacker);
                _encounters[attacker.ObjectId] = encounter;

                if (OnCombatStart != null)
                {
                    OnCombatStart(attacker);
                }
            }

            encounter.SetTarget(target);

            // Also ensure target is aware of combat
            if (!_encounters.ContainsKey(target.ObjectId))
            {
                var targetEncounter = new CombatEncounter(target);
                _encounters[target.ObjectId] = targetEncounter;

                if (OnCombatStart != null)
                {
                    OnCombatStart(target);
                }
            }
        }

        /// <summary>
        /// Removes an entity from combat.
        /// </summary>
        public void ExitCombat(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            if (_encounters.Remove(entity.ObjectId))
            {
                if (OnCombatEnd != null)
                {
                    OnCombatEnd(entity);
                }
            }
        }

        /// <summary>
        /// Checks if an entity is in combat.
        /// </summary>
        public bool IsInCombat(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            return _encounters.ContainsKey(entity.ObjectId);
        }

        /// <summary>
        /// Gets the current target for an entity.
        /// </summary>
        public IEntity GetTarget(IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            CombatEncounter encounter;
            if (_encounters.TryGetValue(entity.ObjectId, out encounter))
            {
                return encounter.CurrentTarget;
            }

            return null;
        }

        /// <summary>
        /// Performs a melee attack.
        /// </summary>
        public AttackResult PerformMeleeAttack(IEntity attacker, IEntity target, int attackBonus = 0)
        {
            return PerformAttack(attacker, target, AttackType.Melee, attackBonus);
        }

        /// <summary>
        /// Performs a ranged attack.
        /// </summary>
        public AttackResult PerformRangedAttack(IEntity attacker, IEntity target, int attackBonus = 0)
        {
            return PerformAttack(attacker, target, AttackType.Ranged, attackBonus);
        }

        /// <summary>
        /// Performs an attack roll.
        /// </summary>
        public AttackResult PerformAttack(IEntity attacker, IEntity target, AttackType type, int attackBonus = 0)
        {
            var result = new AttackResult();
            result.Attacker = attacker;
            result.Target = target;
            result.AttackType = type;

            if (attacker == null || target == null)
            {
                result.Success = false;
                result.Reason = "Invalid attacker or target";
                return result;
            }

            // Get attacker and target stats
            var attackerStats = GetCombatStats(attacker);
            var targetStats = GetCombatStats(target);

            // Calculate total attack bonus
            int totalAttackBonus = attackBonus + attackerStats.AttackBonus;

            // Roll d20
            result.NaturalRoll = RollD20();

            // Check for critical threat (natural 20)
            result.IsCriticalThreat = result.NaturalRoll >= attackerStats.CriticalThreatRange;

            // Calculate total attack roll
            result.TotalRoll = result.NaturalRoll + totalAttackBonus;
            result.TargetDefense = targetStats.Defense;

            // Determine hit
            bool naturalHit = result.NaturalRoll == 20;
            bool naturalMiss = result.NaturalRoll == 1;
            bool regularHit = !naturalMiss && result.TotalRoll >= targetStats.Defense;

            result.Success = naturalHit || regularHit;

            if (naturalMiss)
            {
                result.Success = false;
                result.Reason = "Natural 1 - automatic miss";
            }

            // Confirm critical if threatened
            if (result.Success && result.IsCriticalThreat)
            {
                int confirmRoll = RollD20() + totalAttackBonus;
                result.IsCriticalHit = confirmRoll >= targetStats.Defense;
            }

            // Fire attack resolved event
            if (OnAttackResolved != null)
            {
                OnAttackResolved(result);
            }

            return result;
        }

        /// <summary>
        /// Deals damage to a target.
        /// </summary>
        public DamageResult DealDamage(IEntity source, IEntity target, int baseDamage, DamageType damageType, int multiplier = 1)
        {
            var result = new DamageResult();
            result.Source = source;
            result.Target = target;
            result.DamageType = damageType;
            result.BaseDamage = baseDamage;
            result.Multiplier = multiplier;

            if (target == null)
            {
                result.FinalDamage = 0;
                return result;
            }

            var targetStats = GetCombatStats(target);

            // Calculate damage reduction
            int damageReduction = GetDamageReduction(target, damageType);
            result.DamageReduction = damageReduction;

            // Apply critical multiplier
            int totalDamage = baseDamage * multiplier;

            // Apply damage reduction
            result.FinalDamage = Math.Max(0, totalDamage - damageReduction);

            // Apply damage to target
            ApplyDamage(target, result.FinalDamage);

            // Fire damage dealt event
            if (OnDamageDealt != null)
            {
                OnDamageDealt(result);
            }

            // Check for death
            var stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null && stats.CurrentHP <= 0)
            {
                HandleDeath(source, target);
            }

            return result;
        }

        /// <summary>
        /// Applies direct damage to a target (bypasses attack roll).
        /// </summary>
        public void ApplyDamage(IEntity target, int damage)
        {
            if (target == null || damage <= 0)
            {
                return;
            }

            var stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                stats.CurrentHP -= damage;
            }
        }

        /// <summary>
        /// Heals a target.
        /// </summary>
        public void ApplyHealing(IEntity target, int amount)
        {
            if (target == null || amount <= 0)
            {
                return;
            }

            var stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                stats.CurrentHP = Math.Min(stats.CurrentHP + amount, stats.MaxHP);
            }
        }

        /// <summary>
        /// Performs a saving throw.
        /// </summary>
        public SavingThrowResult PerformSavingThrow(IEntity entity, SavingThrowType type, int dc, int bonus = 0)
        {
            var result = new SavingThrowResult();
            result.Entity = entity;
            result.Type = type;
            result.DC = dc;

            if (entity == null)
            {
                result.Success = false;
                return result;
            }

            var stats = GetCombatStats(entity);

            // Get base save
            int baseSave = 0;
            switch (type)
            {
                case SavingThrowType.Fortitude:
                    baseSave = stats.FortitudeSave;
                    break;
                case SavingThrowType.Reflex:
                    baseSave = stats.ReflexSave;
                    break;
                case SavingThrowType.Will:
                    baseSave = stats.WillSave;
                    break;
            }

            // Roll d20
            result.NaturalRoll = RollD20();
            result.TotalRoll = result.NaturalRoll + baseSave + bonus;

            // Natural 20 always succeeds, natural 1 always fails
            if (result.NaturalRoll == 20)
            {
                result.Success = true;
            }
            else if (result.NaturalRoll == 1)
            {
                result.Success = false;
            }
            else
            {
                result.Success = result.TotalRoll >= dc;
            }

            return result;
        }

        #region Private Methods

        private void ProcessCombatRound()
        {
            foreach (var encounter in _encounters.Values)
            {
                ProcessEncounterRound(encounter);
            }
        }

        private void ProcessEncounterRound(CombatEncounter encounter)
        {
            if (encounter.CurrentTarget == null || !encounter.CurrentTarget.IsValid)
            {
                return;
            }

            // Perform attack if in range
            var attackResult = PerformMeleeAttack(encounter.Combatant, encounter.CurrentTarget);

            if (attackResult.Success)
            {
                // Roll damage
                int baseDamage = RollDamage(encounter.Combatant);
                int multiplier = attackResult.IsCriticalHit ? 2 : 1;

                DealDamage(encounter.Combatant, encounter.CurrentTarget, baseDamage, DamageType.Physical, multiplier);
            }

            encounter.RoundsSinceCombatStart++;
        }

        private void UpdateEncounter(CombatEncounter encounter, float deltaTime)
        {
            // Update encounter timing
            encounter.TimeSinceLastAction += deltaTime;
        }

        private void CleanupFinishedEncounters()
        {
            var toRemove = new List<uint>();

            foreach (var kvp in _encounters)
            {
                var encounter = kvp.Value;

                // Remove if combatant is dead or invalid
                if (!encounter.Combatant.IsValid)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                var stats = encounter.Combatant.GetComponent<Interfaces.Components.IStatsComponent>();
                if (stats != null && stats.CurrentHP <= 0)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                // Remove if target is dead or invalid
                if (encounter.CurrentTarget != null &&
                    (!encounter.CurrentTarget.IsValid || !IsAlive(encounter.CurrentTarget)))
                {
                    encounter.SetTarget(null);

                    // If no target, exit combat after delay
                    if (encounter.TimeSinceLastAction > 5.0f)
                    {
                        toRemove.Add(kvp.Key);
                    }
                }
            }

            foreach (var id in toRemove)
            {
                CombatEncounter encounter;
                if (_encounters.TryGetValue(id, out encounter))
                {
                    _encounters.Remove(id);
                    if (OnCombatEnd != null)
                    {
                        OnCombatEnd(encounter.Combatant);
                    }
                }
            }
        }

        private void HandleDeath(IEntity killer, IEntity victim)
        {
            if (OnEntityDeath != null)
            {
                OnEntityDeath(victim, killer);
            }

            ExitCombat(victim);
        }

        private CombatStats GetCombatStats(IEntity entity)
        {
            var stats = new CombatStats();

            if (entity == null)
            {
                return stats;
            }

            var statsComponent = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (statsComponent != null)
            {
                stats.AttackBonus = statsComponent.GetAbility(Ability.Strength); // Simplified
                stats.Defense = 10 + statsComponent.GetAbility(Ability.Dexterity); // Simplified AC
                stats.FortitudeSave = statsComponent.GetAbility(Ability.Constitution);
                stats.ReflexSave = statsComponent.GetAbility(Ability.Dexterity);
                stats.WillSave = statsComponent.GetAbility(Ability.Wisdom);
            }

            stats.CriticalThreatRange = 20; // Default critical threat on natural 20
            stats.CriticalMultiplier = 2;

            return stats;
        }

        private int GetDamageReduction(IEntity entity, DamageType type)
        {
            // Simplified damage reduction
            return 0;
        }

        private int RollD20()
        {
            return _random.Next(1, 21);
        }

        private int RollDamage(IEntity attacker)
        {
            // Simplified weapon damage (1d8+1)
            return _random.Next(1, 9) + 1;
        }

        private bool IsAlive(IEntity entity)
        {
            if (entity == null || !entity.IsValid)
            {
                return false;
            }

            var stats = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            return stats != null && stats.CurrentHP > 0;
        }

        #endregion
    }

    /// <summary>
    /// Combat encounter for a single entity.
    /// </summary>
    public class CombatEncounter
    {
        public IEntity Combatant { get; }
        public IEntity CurrentTarget { get; private set; }
        public CombatState State { get; set; }
        public int RoundsSinceCombatStart { get; set; }
        public float TimeSinceLastAction { get; set; }

        public CombatEncounter(IEntity combatant)
        {
            Combatant = combatant;
            State = CombatState.InCombat;
            RoundsSinceCombatStart = 0;
            TimeSinceLastAction = 0f;
        }

        public void SetTarget(IEntity target)
        {
            CurrentTarget = target;
            TimeSinceLastAction = 0f;
        }
    }

    /// <summary>
    /// Combat statistics for an entity.
    /// </summary>
    public struct CombatStats
    {
        public int AttackBonus;
        public int Defense;
        public int FortitudeSave;
        public int ReflexSave;
        public int WillSave;
        public int CriticalThreatRange;
        public int CriticalMultiplier;
    }
}
