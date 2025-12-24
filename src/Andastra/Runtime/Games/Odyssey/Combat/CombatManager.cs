using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Party;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Engines.Odyssey.Data;
using Andastra.Runtime.Engines.Odyssey.Systems;
using Andastra.Runtime.Games.Common.Combat;
using BaseItemData = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager.BaseItemData;

namespace Andastra.Runtime.Engines.Odyssey.Combat
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
    /// Combat event arguments (Odyssey-specific: uses AttackRollResult).
    /// </summary>
    public class OdysseyCombatEventArgs : Andastra.Runtime.Games.Common.Combat.CombatEventArgs
    {
        public new AttackRollResult AttackResult
        {
            get { return (AttackRollResult)base.AttackResult; }
            set { base.AttackResult = value; }
        }
    }

    /// <summary>
    /// Manages combat encounters in Odyssey engine (KOTOR 1/2).
    /// </summary>
    /// <remarks>
    /// Inheritance: Base class BaseCombatManager (Runtime.Games.Common) - abstract combat manager, Odyssey override (Runtime.Games.Odyssey) - D20-based combat with rounds
    /// </remarks>
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
    public class CombatManager : BaseCombatManager
    {
        private readonly DamageCalculator _damageCalc;
        private readonly FactionManager _factionManager;
        private readonly GameDataManager _gameDataManager;
        private readonly PartySystem _partySystem;

        private readonly Dictionary<uint, CombatState> _combatStates;
        private readonly Dictionary<uint, CombatRound> _activeRounds;

        /// <summary>
        /// Maximum distance (in meters) for party members to receive XP from combat.
        /// Based on KOTOR mechanics: party members within ~30 meters receive shared XP.
        /// </summary>
        private const float MaxXpShareDistance = 30.0f;

        /// <summary>
        /// XP multiplier for Challenge Rating calculation.
        /// KOTOR uses a simplified XP formula: XP = CR * XpPerChallengeRating.
        /// This is a simplified version compared to full D&D 3.5 XP calculation.
        /// Based on swkotor2.exe: XP award system uses CR * 100 formula.
        /// </summary>
        private const int XpPerChallengeRating = 100;

        /// <summary>
        /// Event fired when an attack is made (Odyssey-specific type).
        /// </summary>
        public new event EventHandler<OdysseyCombatEventArgs> OnAttack;

        /// <summary>
        /// Event fired when an entity is damaged (Odyssey-specific type).
        /// </summary>
        public new event EventHandler<OdysseyCombatEventArgs> OnDamage;

        /// <summary>
        /// Event fired when an entity dies (Odyssey-specific type).
        /// </summary>
        public new event EventHandler<OdysseyCombatEventArgs> OnDeath;

        /// <summary>
        /// Event fired when a combat round ends (Odyssey-specific type).
        /// </summary>
        public new event EventHandler<OdysseyCombatEventArgs> OnRoundEnd;

        public CombatManager(IWorld world, FactionManager factionManager, PartySystem partySystem, GameDataManager gameDataManager = null)
            : base(world)
        {
            _factionManager = factionManager;
            _partySystem = partySystem;
            _gameDataManager = gameDataManager;
            _damageCalc = new DamageCalculator();
            _combatStates = new Dictionary<uint, CombatState>();
            _activeRounds = new Dictionary<uint, CombatRound>();
        }

        #region State Access

        /// <summary>
        /// Gets the combat state of an entity (base class implementation - returns int).
        /// </summary>
        public override int GetCombatState(IEntity entity)
        {
            if (entity == null)
            {
                return (int)CombatState.Idle;
            }

            CombatState state;
            if (_combatStates.TryGetValue(entity.ObjectId, out state))
            {
                return (int)state;
            }
            return (int)CombatState.Idle;
        }

        /// <summary>
        /// Gets the combat state of an entity (Odyssey-specific: returns CombatState enum).
        /// </summary>
        public CombatState GetCombatStateEnum(IEntity entity)
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
        /// Internal method to get combat state as int (for compatibility).
        /// </summary>
        private int GetCombatStateInternal(IEntity entity)
        {
            if (entity == null)
            {
                return (int)CombatState.Idle;
            }

            CombatState state;
            if (_combatStates.TryGetValue(entity.ObjectId, out state))
            {
                return (int)state;
            }
            return (int)CombatState.Idle;
        }

        /// <summary>
        /// Checks if an entity is in combat (Odyssey-specific override).
        /// </summary>
        public new bool IsInCombat(IEntity entity)
        {
            return GetCombatStateEnum(entity) == CombatState.InCombat;
        }

        /// <summary>
        /// Checks if an entity is in "real" combat (has an active combat round).
        /// Based on swkotor2.exe: GetIsInCombat with bOnlyCountReal=TRUE checks for active CombatRoundData
        /// Located via string reference: "CombatRoundData" @ 0x007bf6b4
        /// Original implementation: FUN_005226d0 @ 0x005226d0 checks if CombatRoundData is active
        /// (*(int *)(*(void **)((int)this + 0x10dc) + 0xa84) == 1) where 0xa84 is RoundStarted offset
        /// "Real" combat means actively fighting (has active combat round), vs "fake" combat (just targeted, no active round)
        /// </summary>
        /// <param name="entity">The entity to check</param>
        /// <returns>True if entity has an active combat round (real combat), false otherwise</returns>
        public bool IsInRealCombat(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Check if entity has an active combat round
            // "Real" combat requires an active combat round, not just combat state
            CombatRound round = GetActiveRound(entity);
            if (round != null && !round.IsComplete)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the active combat round for an entity (Odyssey-specific: CombatRound type).
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
        /// Initiates combat between attacker and target (Odyssey-specific: D20 combat rounds).
        /// </summary>
        public override void InitiateCombat(IEntity attacker, IEntity target)
        {
            if (attacker == null || target == null)
            {
                return;
            }

            // Set combat states
            SetCombatState(attacker, CombatState.InCombat);
            SetCombatState(target, CombatState.InCombat);

            // Set attack target (base class handles storage)
            _currentTargets[attacker.ObjectId] = target;
            _lastAttackers[target.ObjectId] = attacker;

            // Create combat round (Odyssey-specific)
            StartNewRound(attacker, target);

            // Set temporary hostility (Odyssey-specific)
            if (_factionManager != null)
            {
                _factionManager.SetTemporaryHostile(target, attacker, true);
            }
        }

        /// <summary>
        /// Ends combat for an entity (Odyssey-specific: clears combat rounds).
        /// </summary>
        public override void EndCombat(IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _combatStates.Remove(entity.ObjectId);
            _activeRounds.Remove(entity.ObjectId);
            _currentTargets.Remove(entity.ObjectId);
            _lastAttackers.Remove(entity.ObjectId);
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
        /// <summary>
        /// Updates combat system (call each frame) - Odyssey-specific: D20 combat rounds.
        /// </summary>
        public override void Update(float deltaTime)
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
                if (attacker == null || GetCombatStateEnum(attacker) == CombatState.Dead)
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
                    OnRoundEnd?.Invoke(this, new OdysseyCombatEventArgs
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
            OnAttack?.Invoke(this, new OdysseyCombatEventArgs
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
                OnDamage?.Invoke(this, new OdysseyCombatEventArgs
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

            // Fire OnDeath script event
            // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH fires when entity dies
            // Located via string references: "OnDeath" script field, death event handling
            // Original implementation: OnDeath script fires on victim entity with killer as triggerer
            IEventBus eventBus = _world.EventBus;
            if (eventBus != null)
            {
                eventBus.FireScriptEvent(victim, ScriptEvent.OnDeath, killer);
            }

            // Fire death event
            OnDeath?.Invoke(this, new OdysseyCombatEventArgs
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
        /// Awards experience points to party members based on victim's Challenge Rating.
        /// XP is shared equally among all active party members who participated in combat.
        /// </summary>
        /// <remarks>
        /// XP Sharing System (KOTOR):
        /// - Based on swkotor2.exe: XP award system
        /// - Located via string references: "XP" @ 0x007c18a4 (PT_XP_POOL in PARTYTABLE.res)
        /// - Original implementation: XP is shared equally among all active party members within range
        /// - Participation criteria:
        ///   1. Member is in active party
        ///   2. Member is within MaxXpShareDistance of victim (default 30 meters)
        ///   3. Member is alive and not dead
        /// - XP calculation: CR * XpPerChallengeRating (simplified KOTOR formula, not full D&D 3.5)
        ///   - Formula: XP = ChallengeRating * 100
        ///   - This is the standard KOTOR XP calculation, simplified from D&D 3.5 rules
        ///   - Based on swkotor2.exe: XP award system uses CR * 100 formula
        /// - Each participating member receives full XP amount (not divided)
        /// - Original engine behavior: All party members in range receive full XP, not split
        /// </remarks>
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

            // Calculate XP using KOTOR's simplified formula: CR * XpPerChallengeRating
            // This is a simplified version compared to full D&D 3.5 XP calculation.
            // Based on swkotor2.exe: XP award system uses CR * 100 formula.
            // Formula: XP = ChallengeRating * XpPerChallengeRating
            int xpAwarded = (int)(cr * XpPerChallengeRating);

            // Get victim position for distance calculations
            ITransformComponent victimTransform = victim.GetComponent<ITransformComponent>();
            Vector3 victimPosition = victimTransform != null ? victimTransform.Position : Vector3.Zero;

            // Determine which party members participated in combat
            List<IEntity> participatingMembers = GetParticipatingPartyMembers(killer, victim, victimPosition);

            if (participatingMembers.Count == 0)
            {
                // No party members eligible - award to killer only (fallback)
                AwardXpToEntity(killer, xpAwarded, victim, cr);
                return;
            }

            // Award XP to all participating party members
            // KOTOR behavior: Each member receives full XP amount (not divided)
            foreach (IEntity member in participatingMembers)
            {
                AwardXpToEntity(member, xpAwarded, victim, cr);
            }
        }

        /// <summary>
        /// Determines which party members participated in combat and are eligible for XP.
        /// </summary>
        /// <param name="killer">The entity that killed the victim.</param>
        /// <param name="victim">The entity that was killed.</param>
        /// <param name="victimPosition">The position of the victim.</param>
        /// <returns>List of party members who participated in combat.</returns>
        private List<IEntity> GetParticipatingPartyMembers(IEntity killer, IEntity victim, Vector3 victimPosition)
        {
            var participatingMembers = new List<IEntity>();

            if (_partySystem == null)
            {
                // No party system - fallback to killer only
                if (killer != null)
                {
                    participatingMembers.Add(killer);
                }
                return participatingMembers;
            }

            // Get all active party members
            IReadOnlyList<PartyMember> activeParty = _partySystem.ActiveParty;
            if (activeParty == null || activeParty.Count == 0)
            {
                // No active party - fallback to killer only
                if (killer != null)
                {
                    participatingMembers.Add(killer);
                }
                return participatingMembers;
            }

            // Check each active party member for participation
            foreach (PartyMember member in activeParty)
            {
                if (member == null || member.Entity == null)
                {
                    continue;
                }

                IEntity memberEntity = member.Entity;

                // Skip dead members
                IStatsComponent memberStats = memberEntity.GetComponent<IStatsComponent>();
                if (memberStats == null || memberStats.IsDead)
                {
                    continue;
                }

                // Check if member participated in combat
                bool participated = false;

                // Method 1: Member was the killer
                if (memberEntity.ObjectId == killer.ObjectId)
                {
                    participated = true;
                }
                // Method 2: Member was in combat with victim
                else if (AreInCombatWith(memberEntity, victim))
                {
                    participated = true;
                }
                // Method 3: Member is within range of victim (proximity-based participation)
                else
                {
                    ITransformComponent memberTransform = memberEntity.GetComponent<ITransformComponent>();
                    if (memberTransform != null)
                    {
                        float distanceSquared = Vector3.DistanceSquared(memberTransform.Position, victimPosition);
                        float maxDistanceSquared = MaxXpShareDistance * MaxXpShareDistance;
                        if (distanceSquared <= maxDistanceSquared)
                        {
                            participated = true;
                        }
                    }
                }

                if (participated)
                {
                    participatingMembers.Add(memberEntity);
                }
            }

            return participatingMembers;
        }

        /// <summary>
        /// Awards XP to a single entity and handles level-up logic.
        /// </summary>
        /// <param name="entity">The entity to award XP to.</param>
        /// <param name="xpAmount">The amount of XP to award.</param>
        /// <param name="victim">The victim that was killed (for logging).</param>
        /// <param name="cr">The challenge rating of the victim (for logging).</param>
        private void AwardXpToEntity(IEntity entity, int xpAmount, IEntity victim, float cr)
        {
            if (entity == null)
            {
                return;
            }

            StatsComponent entityStats = entity.GetComponent<StatsComponent>();
            if (entityStats == null)
            {
                return;
            }

            // Award XP and check for level up
            int oldLevel = entityStats.Level;
            entityStats.Experience += xpAmount;

            Console.WriteLine("[CombatManager] Awarding " + xpAmount + " XP to " + entity.Tag + " for killing " + (victim != null ? victim.Tag : "unknown") + " (CR " + cr + ")");

            // Check for level up
            if (entityStats.CanLevelUp())
            {
                Console.WriteLine("[CombatManager] " + entity.Tag + " can level up! (Level " + oldLevel + " -> " + (oldLevel + 1) + ")");
                // Fire OnPlayerLevelUp script event
                // Based on swkotor2.exe: CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP fires when player levels up
                // Located via string references: "OnPlayerLevelUp" @ 0x007c1a90, "LevelUp" @ 0x007c1a9c
                // Original implementation: OnPlayerLevelUp script fires on entity when it levels up
                if (_world != null && _world.EventBus != null)
                {
                    _world.EventBus.FireScriptEvent(entity, ScriptEvent.OnPlayerLevelUp, entity);
                }
            }
        }

        #endregion
    }
}
