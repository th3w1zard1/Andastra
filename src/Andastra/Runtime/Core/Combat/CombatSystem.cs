using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common.Combat;

namespace Andastra.Runtime.Core.Combat
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
    /// - Based on swkotor2.exe combat system
    /// - EndCombatRound @ 0x00529c30 - Ends combat round and resets combat state (located via "CSWSCombatRound::EndCombatRound - %x Combat Slave (%x) not found!" @ 0x007bfb80)
    /// - SaveEntityState @ 0x005226d0 - Saves entity state including CombatRoundData (located via "CombatRoundData" @ 0x007bf6b4)
    /// - Located via string references: "CombatRoundData" @ 0x007bf6b4, "CombatInfo" @ 0x007c2e60
    /// - Combat round class: "CSWSCombatRound" with timer management functions
    /// - Error messages: "CSWSCombatRound::EndCombatRound - %x Combat Slave (%x) not found!" @ 0x007bfb80
    /// - "CSWSCombatRound::IncrementTimer - %s Timer is negative at %d; Ending combat round and resetting" @ 0x007bfbc8
    /// - "CSWSCombatRound::IncrementTimer - %s Master IS found (%x) and round has expired (%d %d); Resetting" @ 0x007bfc28
    /// - "CSWSCombatRound::IncrementTimer - %s Master cannot be found and round has expired; Resetting" @ 0x007bfc90
    /// - "CSWSCombatRound::DecrementPauseTimer - %s Master cannot be found expire the round; Resetting" @ 0x007bfcf0
    /// - Original implementation: 3-second combat rounds with timer-based attack scheduling
    /// - Round timer: Increments each frame, expires after RoundDuration (3.0 seconds)
    /// - Master/Slave: Combat encounters have master entity that controls round timing (MasterID field in CombatRoundData)
    /// - Timer validation: Original engine checks for negative timers and resets if invalid
    /// - Master tracking: If master entity is not found, round is reset
    /// - CombatRoundData fields: RoundStarted (byte), Timer (float), RoundLength (float), MasterID (int32), RoundPaused (byte), RoundPausedBy (int32), PauseTimer (float), CurrentAttack (byte), AttackID (uint16), AttackList (5 entries), SpecAttackList, SchedActionList
    /// - D20 attack roll + attack bonus vs defense (ArmorClass)
    /// - Natural 20 always hits (critical threat), natural 1 always misses
    /// - Critical hits on natural 20 (threatened), confirm with second roll vs defense
    /// - Damage = weapon damage + modifiers - damage reduction
    /// - Script hooks: "ScriptAttacked" @ 0x007bee80 fires on attack, "ScriptDamaged" @ 0x007bee70 fires on damage
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
            foreach (CombatEncounter encounter in _encounters.Values)
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
            CombatStats attackerStats = GetCombatStats(attacker);
            CombatStats targetStats = GetCombatStats(target);

            // Calculate total attack bonus
            int totalAttackBonus = attackBonus + attackerStats.AttackBonus;

            // Roll d20
            result.NaturalRoll = RollD20();

            // Check for automatic miss (natural 1 always misses, even with AssuredHit)
            bool naturalMiss = result.NaturalRoll == 1;
            if (naturalMiss)
            {
                result.Success = false;
                result.Reason = "Natural 1 - automatic miss";
                return result;
            }

            // Check for AssuredHit effect
            // Based on swkotor.exe: AssuredHit effect guarantees attack hits (bypasses AC check)
            // Located via string references: EffectAssuredHit @ routine 51
            // Original implementation: Effect flag that forces attack to hit unless natural 1
            bool hasAssuredHit = false;
            if (_world != null && _world.EffectSystem != null && attacker != null)
            {
                hasAssuredHit = _world.EffectSystem.HasEffect(attacker, EffectType.AssuredHit);
            }

            // Check for critical threat (natural 20)
            result.IsCriticalThreat = result.NaturalRoll >= attackerStats.CriticalThreatRange;

            // Calculate total attack roll
            result.TotalRoll = result.NaturalRoll + totalAttackBonus;
            result.TargetDefense = targetStats.Defense;

            // Determine hit
            if (hasAssuredHit)
            {
                // AssuredHit forces a hit (natural 1 already handled above)
                result.Success = true;
                result.Reason = "AssuredHit effect - automatic hit";
            }
            else
            {
                bool naturalHit = result.NaturalRoll == 20;
                bool regularHit = result.TotalRoll >= targetStats.Defense;
                result.Success = naturalHit || regularHit;
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

            CombatStats targetStats = GetCombatStats(target);

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
            Interfaces.Components.IStatsComponent stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
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

            Interfaces.Components.IStatsComponent stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
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

            Interfaces.Components.IStatsComponent stats = target.GetComponent<Interfaces.Components.IStatsComponent>();
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

            CombatStats stats = GetCombatStats(entity);

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
            foreach (CombatEncounter encounter in _encounters.Values)
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
            AttackResult attackResult = PerformMeleeAttack(encounter.Combatant, encounter.CurrentTarget);

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

            foreach (KeyValuePair<uint, CombatEncounter> kvp in _encounters)
            {
                CombatEncounter encounter = kvp.Value;

                // Remove if combatant is dead or invalid
                if (!encounter.Combatant.IsValid)
                {
                    toRemove.Add(kvp.Key);
                    continue;
                }

                Interfaces.Components.IStatsComponent stats = encounter.Combatant.GetComponent<Interfaces.Components.IStatsComponent>();
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

            foreach (uint id in toRemove)
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

            Interfaces.Components.IStatsComponent statsComponent = entity.GetComponent<Interfaces.Components.IStatsComponent>();
            if (statsComponent != null)
            {
                stats.AttackBonus = statsComponent.BaseAttackBonus;
                stats.Defense = statsComponent.ArmorClass;
                stats.FortitudeSave = statsComponent.FortitudeSave;
                stats.ReflexSave = statsComponent.ReflexSave;
                stats.WillSave = statsComponent.WillSave;
            }

            stats.CriticalThreatRange = 20; // Default critical threat on natural 20
            stats.CriticalMultiplier = 2;

            return stats;
        }

        private int GetDamageReduction(IEntity entity, DamageType type)
        {
            // TODO:  Simplified damage reduction
            return 0;
        }

        private int RollD20()
        {
            return _random.Next(1, 21);
        }

        private int RollDamage(IEntity attacker)
        {
            if (attacker == null)
            {
                return 0;
            }

            // Try to get weapon damage calculator from world (engine-specific)
            // Based on swkotor2.exe: Weapon damage calculation uses baseitems.2da
            // Located via string references: "DamageDice" @ 0x007c2d3c, "DamageDie" @ 0x007c2d30
            // Original implementation: FUN_005d7fc0 @ 0x005d7fc0 saves DamageDice/DamageDie to GFF
            // Damage formula: Roll(damagedice * damagedie) + damagebonus + ability modifier
            var calculator = GetWeaponDamageCalculator();
            if (calculator != null)
            {
                // Use weapon damage calculator (handles equipped weapons, unarmed, ability modifiers, etc.)
                // isOffhand = false (main hand attack), isCritical = false (will be applied in DealDamage if critical)
                return calculator.CalculateDamage(attacker, isOffhand: false, isCritical: false);
            }

            // Fallback: Calculate damage directly from equipped weapon
            return CalculateWeaponDamageFallback(attacker);
        }

        /// <summary>
        /// Gets the weapon damage calculator from the world (engine-specific).
        /// </summary>
        /// <returns>The weapon damage calculator, or null if not available.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: Weapon damage calculation system
        /// Uses reflection to find engine-specific weapon damage calculator from world
        /// Engine-specific implementations: WeaponDamageCalculator (Odyssey), AuroraWeaponDamageCalculator (Aurora), EclipseWeaponDamageCalculator (Eclipse)
        /// </remarks>
        private BaseWeaponDamageCalculator GetWeaponDamageCalculator()
        {
            if (_world == null)
            {
                return null;
            }

            // Try to get weapon damage calculator from world using reflection
            // Engine-specific worlds may have a WeaponDamageCalculator property
            try
            {
                Type worldType = _world.GetType();
                
                // Try common property names
                System.Reflection.PropertyInfo calculatorProp = worldType.GetProperty("WeaponDamageCalculator");
                if (calculatorProp != null)
                {
                    object calculator = calculatorProp.GetValue(_world);
                    if (calculator is BaseWeaponDamageCalculator baseCalculator)
                    {
                        return baseCalculator;
                    }
                }

                // Try method to get calculator
                System.Reflection.MethodInfo getCalculatorMethod = worldType.GetMethod("GetWeaponDamageCalculator");
                if (getCalculatorMethod != null)
                {
                    object calculator = getCalculatorMethod.Invoke(_world, null);
                    if (calculator is BaseWeaponDamageCalculator baseCalculator)
                    {
                        return baseCalculator;
                    }
                }
            }
            catch
            {
                // Reflection failed, fall through to fallback
            }

            return null;
        }

        /// <summary>
        /// Fallback weapon damage calculation when calculator is not available.
        /// </summary>
        /// <param name="attacker">The attacking entity.</param>
        /// <returns>Damage amount.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: Fallback damage calculation
        /// Calculates damage directly from equipped weapon data
        /// Uses entity data fields: "DamageDice", "DamageDie", "DamageBonus"
        /// Falls back to unarmed damage (1d3) if no weapon equipped
        /// </remarks>
        private int CalculateWeaponDamageFallback(IEntity attacker)
        {
            if (attacker == null)
            {
                return 0;
            }

            // Get equipped weapon from inventory
            Interfaces.Components.IInventoryComponent inventory = attacker.GetComponent<Interfaces.Components.IInventoryComponent>();
            if (inventory == null)
            {
                // Unarmed damage: 1d3 (common across all engines)
                return RollDice(1, 3);
            }

            // Try main hand weapon slot (engine-specific, but common slots are 4 for main hand, 5 for offhand)
            // Based on swkotor2.exe: RIGHTWEAPON slot 4, LEFTWEAPON slot 5
            IEntity weapon = inventory.GetItemInSlot(4); // Main hand
            if (weapon == null)
            {
                weapon = inventory.GetItemInSlot(5); // Offhand
            }

            if (weapon == null)
            {
                // Unarmed damage: 1d3 (common across all engines)
                return RollDice(1, 3);
            }

            // Get damage dice from weapon entity data
            // Based on swkotor2.exe: DamageDice and DamageDie stored in entity data
            // Located via string references: "DamageDice" @ 0x007c2d3c, "DamageDie" @ 0x007c2d30
            int damageDice = 1;
            int damageDie = 8;
            int damageBonus = 0;

            if (weapon.HasData("DamageDice"))
            {
                object diceObj = weapon.GetData("DamageDice");
                if (diceObj is int dice)
                {
                    damageDice = dice;
                }
            }

            if (weapon.HasData("DamageDie"))
            {
                object dieObj = weapon.GetData("DamageDie");
                if (dieObj is int die)
                {
                    damageDie = die;
                }
            }

            if (weapon.HasData("DamageBonus"))
            {
                object bonusObj = weapon.GetData("DamageBonus");
                if (bonusObj is int bonus)
                {
                    damageBonus = bonus;
                }
            }

            // Roll damage dice
            int rolledDamage = RollDice(damageDice, damageDie);

            // Add damage bonus
            int totalDamage = rolledDamage + damageBonus;

            // Add ability modifier (STR for melee, DEX for ranged - simplified)
            Interfaces.Components.IStatsComponent stats = attacker.GetComponent<Interfaces.Components.IStatsComponent>();
            if (stats != null)
            {
                // Simplified: Use STR modifier (melee default)
                // Full implementation would check if weapon is ranged and use DEX, or check finesse feats
                int strMod = stats.GetAbilityModifier(Core.Enums.Ability.Strength);
                totalDamage += strMod;
            }

            // Minimum 1 damage
            return Math.Max(1, totalDamage);
        }

        /// <summary>
        /// Rolls dice (e.g., 2d6 = RollDice(2, 6)).
        /// </summary>
        /// <param name="count">Number of dice to roll.</param>
        /// <param name="dieSize">Size of each die (e.g., 6 for d6).</param>
        /// <returns>Total of all dice rolls.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: Dice rolling system
        /// Common across all engines: Rolls multiple dice and sums the results
        /// Used for weapon damage, unarmed damage, and other dice-based calculations
        /// </remarks>
        private int RollDice(int count, int dieSize)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += _random.Next(1, dieSize + 1);
            }
            return total;
        }

        private bool IsAlive(IEntity entity)
        {
            if (entity == null || !entity.IsValid)
            {
                return false;
            }

            Interfaces.Components.IStatsComponent stats = entity.GetComponent<Interfaces.Components.IStatsComponent>();
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
