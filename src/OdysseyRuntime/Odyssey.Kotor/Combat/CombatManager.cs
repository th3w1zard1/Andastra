using System;
using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Components;
using Odyssey.Kotor.Systems;
using Odyssey.Kotor.Data;

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
    /// - Based on swkotor2.exe combat system
    /// - Located via string references: "CombatRoundData" @ 0x007bf6b4, "CombatInfo" @ 0x007c2e60
    /// - "InCombatHPBase" @ 0x007bf224, "OutOfCombatHPBase" @ 0x007bf210, "InCombatFPBase" @ 0x007bf4fc
    /// - "?OutOfCombatFPBase" @ 0x007bf50f (out-of-combat force points base)
    /// - "CombatAnimations" @ 0x007c4ea4, "Combat Movement" @ 0x007c8670, "Combat_Round" @ 0x007cb318
    /// - "End Of Combat Round" @ 0x007c843c, "HD0:combat" @ 0x007cbed8 (combat debug header)
    /// - CSWSCombatRound error messages:
    ///   - "CSWSCombatRound::EndCombatRound - %x Combat Slave (%x) not found!" @ 0x007bfb80
    ///   - "CSWSCombatRound::IncrementTimer - %s Timer is negative at %d; Ending combat round and resetting" @ 0x007bfbc8
    ///   - "CSWSCombatRound::IncrementTimer - %s Master IS found (%x) and round has expired (%d %d); Resetting" @ 0x007bfc28
    ///   - "CSWSCombatRound::IncrementTimer - %s Master cannot be found and round has expired; Resetting" @ 0x007bfc90
    ///   - "CSWSCombatRound::DecrementPauseTimer - %s Master cannot be found expire the round; Resetting" @ 0x007bfcf0
    /// - GUI references: "BTN_COMBAT" @ 0x007c9044, "LB_COMBAT" @ 0x007c9104
    /// - "combatreticle" @ 0x007ccfb0, "LBL_COMBATBG3" @ 0x007cd2c0, "LBL_COMBATBG2" @ 0x007cd2d0
    /// - Combat round functions: FUN_005226d0 @ 0x005226d0 (combat round management)
    /// - Combat info functions: FUN_005d9670 @ 0x005d9670, FUN_005d7fc0 @ 0x005d7fc0
    /// - Attack event: "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4, "ScriptAttacked" @ 0x007bee80
    /// - Original implementation: CSWSCombatRound class manages 3-second combat rounds
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
        private readonly GameDataManager _gameDataManager;

        private readonly Dictionary<uint, CombatState> _combatStates;
        private readonly Dictionary<uint, CombatRound> _activeRounds;
        private readonly Dictionary<uint, IEntity> _currentTargets;
        private readonly Dictionary<uint, IEntity> _lastAttackers; // Tracks last attacker for each entity

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

        public CombatManager(IWorld world, FactionManager factionManager, GameDataManager gameDataManager = null)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _factionManager = factionManager;
            _gameDataManager = gameDataManager;
            _damageCalc = new DamageCalculator();
            _combatStates = new Dictionary<uint, CombatState>();
            _activeRounds = new Dictionary<uint, CombatRound>();
            _currentTargets = new Dictionary<uint, IEntity>();
            _lastAttackers = new Dictionary<uint, IEntity>();
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
        /// Gets the last attacker of an entity.
        /// </summary>
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
            IStatsComponent stats = attacker.GetComponent<IStatsComponent>();
            int bab = stats != null ? stats.BaseAttackBonus : 0;

            // Check for dual wielding
            bool isDualWielding = IsDualWielding(attacker);
            bool hasTWF = HasFeat(attacker, 2); // FEAT_TWO_WEAPON_FIGHTING = 2

            int numAttacks = _damageCalc.CalculateAttacksPerRound(bab, isDualWielding);

            for (int i = 0; i < numAttacks; i++)
            {
                bool isOffhand = isDualWielding && i == numAttacks - 1;
                int attackBonus = _damageCalc.CalculateIterativeAttackBonus(bab, i, isOffhand, hasTWF);

                // Get weapon stats from equipped weapon
                WeaponStats weaponStats = GetWeaponStats(attacker, isOffhand);

                var attack = new AttackAction(attacker, target)
                {
                    AttackBonus = attackBonus,
                    IsOffhand = isOffhand,
                    WeaponDamageRoll = weaponStats.DamageRoll,
                    DamageBonus = _damageCalc.CalculateMeleeDamageBonus(attacker, false, isOffhand),
                    CriticalThreat = weaponStats.CriticalThreat,
                    CriticalMultiplier = weaponStats.CriticalMultiplier
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
                        IStatsComponent targetStats = round.Target.GetComponent<IStatsComponent>();
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

            // Track last attacker
            _lastAttackers[attack.Target.ObjectId] = attack.Attacker;

            // Fire OnPhysicalAttacked script event on target (fires regardless of hit/miss)
            // Based on swkotor2.exe: EVENT_ON_MELEE_ATTACKED fires OnMeleeAttacked script
            // Located via string references: "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4 (case 0xf), "OnMeleeAttacked" @ 0x007c1a5c, "ScriptAttacked" @ 0x007bee80
            // Original implementation: EVENT_ON_MELEE_ATTACKED fires on target entity when attacked (before damage is applied)
            IEventBus eventBus = _world.EventBus;
            if (eventBus != null)
            {
                eventBus.FireScriptEvent(attack.Target, ScriptEvent.OnPhysicalAttacked, attack.Attacker);
            }

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

                // Fire OnDamaged script event on target
                // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED fires when entity takes damage
                // Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4), "ScriptDamaged" @ 0x007bee70, "OnDamaged" @ 0x007c1a80
                if (eventBus != null && actualDamage > 0)
                {
                    eventBus.FireScriptEvent(attack.Target, ScriptEvent.OnDamaged, attack.Attacker);
                }

                // Fire damage event
                OnDamage?.Invoke(this, new CombatEventArgs
                {
                    Attacker = attack.Attacker,
                    Target = attack.Target,
                    AttackResult = result
                });

                // Check for death
                IStatsComponent targetStats = attack.Target.GetComponent<IStatsComponent>();
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
            AwardExperience(killer, victim);
        }

        #endregion

        #region Utility

        /// <summary>
        /// Gets all entities currently in combat.
        /// </summary>
        public IEnumerable<IEntity> GetEntitiesInCombat()
        {
            foreach (KeyValuePair<uint, CombatState> kvp in _combatStates)
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

        /// <summary>
        /// Checks if a creature is dual wielding (has weapons in both hands).
        /// </summary>
        private bool IsDualWielding(IEntity creature)
        {
            if (creature == null)
            {
                return false;
            }

            IInventoryComponent inventory = creature.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return false;
            }

            // INVENTORY_SLOT_RIGHTWEAPON = 4, INVENTORY_SLOT_LEFTWEAPON = 5
            IEntity rightWeapon = inventory.GetItemInSlot(4);
            IEntity leftWeapon = inventory.GetItemInSlot(5);

            return (rightWeapon != null && leftWeapon != null);
        }

        /// <summary>
        /// Checks if a creature has a specific feat.
        /// </summary>
        private bool HasFeat(IEntity creature, int featId)
        {
            if (creature == null)
            {
                return false;
            }

            CreatureComponent creatureComp = creature.GetComponent<CreatureComponent>();
            if (creatureComp == null || creatureComp.FeatList == null)
            {
                return false;
            }

            return creatureComp.FeatList.Contains(featId);
        }

        /// <summary>
        /// Gets weapon stats from an equipped weapon.
        /// </summary>
        /// <remarks>
        /// Weapon Stats Retrieval:
        /// - Based on swkotor2.exe weapon system
        /// - Original implementation: Gets weapon damage, critical threat, and multiplier from baseitems.2da
        /// - INVENTORY_SLOT_RIGHTWEAPON = 4, INVENTORY_SLOT_LEFTWEAPON = 5
        /// - Falls back to unarmed damage (1d4) if no weapon equipped
        /// </remarks>
        private WeaponStats GetWeaponStats(IEntity creature, bool isOffhand)
        {
            if (creature == null)
            {
                return WeaponStats.Unarmed();
            }

            IInventoryComponent inventory = creature.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return WeaponStats.Unarmed();
            }

            // Get weapon from appropriate slot
            int weaponSlot = isOffhand ? 5 : 4; // INVENTORY_SLOT_LEFTWEAPON : INVENTORY_SLOT_RIGHTWEAPON
            IEntity weapon = inventory.GetItemInSlot(weaponSlot);

            if (weapon == null)
            {
                return WeaponStats.Unarmed();
            }

            IItemComponent itemComp = weapon.GetComponent<IItemComponent>();
            if (itemComp == null || _gameDataManager == null)
            {
                return WeaponStats.Unarmed();
            }

            // Get base item data
            BaseItemData baseItem = _gameDataManager.GetBaseItem(itemComp.BaseItem);
            if (baseItem == null)
            {
                return WeaponStats.Unarmed();
            }

            // Build damage roll string (e.g., "1d8")
            string damageRoll = baseItem.NumDice + "d" + baseItem.DieToRoll;
            if (baseItem.NumDice <= 0 || baseItem.DieToRoll <= 0)
            {
                damageRoll = "1d4"; // Fallback to unarmed
            }

            return new WeaponStats
            {
                DamageRoll = damageRoll,
                CriticalThreat = baseItem.CriticalThreat > 0 ? baseItem.CriticalThreat : 20,
                CriticalMultiplier = baseItem.CriticalMultiplier > 0 ? baseItem.CriticalMultiplier : 2
            };
        }

        /// <summary>
        /// Weapon statistics for combat calculations.
        /// </summary>
        private class WeaponStats
        {
            public string DamageRoll { get; set; }
            public int CriticalThreat { get; set; }
            public int CriticalMultiplier { get; set; }

            public static WeaponStats Unarmed()
            {
                return new WeaponStats
                {
                    DamageRoll = "1d4",
                    CriticalThreat = 20,
                    CriticalMultiplier = 2
                };
            }
        }

        /// <summary>
        /// Awards experience points to the killer based on victim's Challenge Rating.
        /// </summary>
        private void AwardExperience(IEntity killer, IEntity victim)
        {
            if (killer == null || victim == null)
            {
                return;
            }

            // Get victim's Challenge Rating
            CreatureComponent victimComp = victim.GetComponent<CreatureComponent>();
            if (victimComp == null)
            {
                return;
            }

            float cr = victimComp.ChallengeRating;
            if (cr <= 0)
            {
                return; // No XP for CR 0 or negative
            }

            // Calculate XP: Base formula is CR * 100, with diminishing returns
            // KOTOR uses a simplified XP system compared to D&D 3.5
            int xpAwarded = (int)(cr * 100);

            // Apply party sharing (if killer is in a party, share XP)
            // For now, just award to the killer
            StatsComponent killerStats = killer.GetComponent<StatsComponent>();
            if (killerStats != null)
            {
                // Award XP and check for level up
                int oldLevel = killerStats.Level;
                killerStats.Experience += xpAwarded;
                
                Console.WriteLine("[CombatManager] Awarding " + xpAwarded + " XP to " + killer.Tag + " for killing " + victim.Tag + " (CR " + cr + ")");
                
                // Check for level up
                if (killerStats.CanLevelUp())
                {
                    Console.WriteLine("[CombatManager] " + killer.Tag + " can level up! (Level " + oldLevel + " -> " + (oldLevel + 1) + ")");
                    // Fire OnPlayerLevelUp script event
                    if (_world != null && _world.EventBus != null)
                    {
                        _world.EventBus.FireScriptEvent(killer, ScriptEvent.OnPlayerLevelUp, killer);
                    }
                }
            }
        }

        #endregion
    }
}
