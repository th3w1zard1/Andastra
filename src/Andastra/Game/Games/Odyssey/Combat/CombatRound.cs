using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Engines.Odyssey.Combat
{
    /// <summary>
    /// Combat round phases.
    /// </summary>
    public enum CombatRoundPhase
    {
        /// <summary>
        /// Round starting, init animations.
        /// </summary>
        Starting = 0,

        /// <summary>
        /// Primary attack (~0.5s into round).
        /// </summary>
        FirstAttack = 1,

        /// <summary>
        /// Offhand/extra attack (~1.5s into round).
        /// </summary>
        SecondAttack = 2,

        /// <summary>
        /// Return to ready stance (~2.5s).
        /// </summary>
        Cooldown = 3,

        /// <summary>
        /// Round complete (3.0s).
        /// </summary>
        Finished = 4
    }

    /// <summary>
    /// Represents a combat round for a creature.
    /// </summary>
    /// <remarks>
    /// KOTOR Combat Round (~3 seconds):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): CSWSCombatRound class
    /// - Located via string reference: "CombatRoundData" @ 0x007bf6b4
    /// - Original implementation: FUN_00529470 @ 0x00529470 (save CombatRoundData to GFF)
    ///   - Saves all combat round state fields in specific order:
    ///     - RoundStarted (byte @ offset 0xa84), SpellCastRound (byte @ offset 0xa88), DeflectArrow (byte @ offset 0xac8), WeaponSucks (byte @ offset 0xacc)
    ///     - DodgeTarget (int32 @ offset 0xafc), NewAttackTarget (int32 @ offset 0xadc), Engaged (float @ offset 0xb08), Master (float @ offset 0xb0c), MasterID (int32 @ offset 0xb10)
    ///     - RoundPaused (byte @ offset 0xaa8), RoundPausedBy (int32 @ offset 0xaac), InfinitePause (byte @ offset 0xab4), PauseTimer (float @ offset 0xab0)
    ///     - Timer (float @ offset 0xa94), RoundLength (float @ offset 0xa9c), OverlapAmount (float @ offset 0xaa0), BleedTimer (float @ offset 0xaa4)
    ///     - CurrentAttack (byte @ offset 0xabc), AttackID (uint16 @ offset 0xa80), AttackGroup (byte @ offset 0xac4), ParryIndex (float @ offset 0xad0)
    ///     - NumAOOs (float @ offset 0xad4), NumCleaves (float @ offset 0xad8), OnHandAttacks (float @ offset 0xae0), OffHandAttacks (float @ offset 0xae4)
    ///     - AdditAttacks (float @ offset 0xaf0), EffectAttacks (float @ offset 0xaf4), ParryActions (byte @ offset 0xaf8), OffHandTaken (float @ offset 0xae8), ExtraTaken (float @ offset 0xaec)
    ///   - AttackList: Saves 5 attack entries (FUN_00527530 saves each attack struct, iterates 5 times with offset increment 0x17c per attack)
    ///   - SpecAttackList: Saves special attack list (SpecialAttack uint16 per entry, iterates through list at offset 0xa68, count at offset 0xa6c)
    ///   - SpecAttackIdList: Saves special attack ID list (SpecialAttackId uint16 per entry, iterates through list at offset 0xa74, count at offset 0xa78)
    ///   - SchedActionList: Saves scheduled action list (FUN_005270f0 saves each scheduled action, iterates through linked list at offset 0xb00)
    /// - FUN_005226d0 @ 0x005226d0 (creature save function, saves CombatRoundData if round is active)
    ///   - Original implementation: Checks if CombatRoundData is active (*(int *)(*(void **)((int)this + 0x10dc) + 0xa84) == 1)
    ///   - If active, calls FUN_00529470 to save CombatRoundData to GFF structure
    ///   - Saves creature state including: DetectMode, StealthMode, CreatureSize, IsDestroyable, IsRaiseable, DeadSelectable
    ///   - Saves all script hooks: ScriptHeartbeat, ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked, ScriptEndDialogue
    ///   - Saves Equip_ItemList (20 slots), ItemList (inventory items), PerceptionList, CombatRoundData, AreaId, AmbientAnimState, Animation, CreatnScrptFird, PM_IsDisguised, PM_Appearance, Listening, ForceAlwaysUpdate, Position/Orientation, JoiningXP, BonusForcePoints, AssignedPup, PlayerCreated, FollowInfo, ActionList
    /// - FUN_005fb0f0 @ 0x005fb0f0 (reference to CombatRoundData usage)
    /// - CombatRoundData GFF fields (from FUN_00529470):
    ///   - "RoundStarted" (byte): Whether round has started
    ///   - "SpellCastRound" (byte): Spell cast during round flag
    ///   - "DeflectArrow" (byte): Deflect arrow ability flag
    ///   - "WeaponSucks" (byte): Weapon sucks flag
    ///   - "DodgeTarget" (int32): Target being dodged
    ///   - "NewAttackTarget" (int32): New attack target ID
    ///   - "Engaged" (float): Engaged flag
    ///   - "Master" (float): Master entity ID
    ///   - "MasterID" (int32): Master ID
    ///   - "RoundPaused" (byte): Round paused flag
    ///   - "RoundPausedBy" (int32): Entity that paused the round
    ///   - "InfinitePause" (byte): Infinite pause flag
    ///   - "PauseTimer" (float): Pause timer value
    ///   - "Timer" (float): Round timer
    ///   - "RoundLength" (float): Round length (3.0 seconds)
    ///   - "OverlapAmount" (float): Overlap amount
    ///   - "BleedTimer" (float): Bleed timer
    ///   - "CurrentAttack" (byte): Current attack index
    ///   - "AttackID" (uint16): Attack ID
    ///   - "AttackGroup" (byte): Attack group
    ///   - "ParryIndex" (float): Parry index
    ///   - "NumAOOs" (float): Number of attacks of opportunity
    ///   - "NumCleaves" (float): Number of cleaves
    ///   - "OnHandAttacks" (float): Main hand attacks count
    ///   - "OffHandAttacks" (float): Offhand attacks count
    ///   - "AdditAttacks" (float): Additional attacks count
    ///   - "EffectAttacks" (float): Effect-based attacks count
    ///   - "ParryActions" (byte): Parry actions flag
    ///   - "OffHandTaken" (float): Offhand attack taken flag
    ///   - "ExtraTaken" (float): Extra attack taken flag
    ///   - "AttackList" (list): List of 5 attack entries
    ///   - "SpecAttackList" (list): Special attack list
    ///   - "SpecAttackIdList" (list): Special attack ID list
    ///   - "SchedActionList" (list): Scheduled action list
    /// - Error messages:
    ///   - "CSWSCombatRound::EndCombatRound - %x Combat Slave (%x) not found!" @ 0x007bfb80
    ///   - "CSWSCombatRound::IncrementTimer - %s Timer is negative at %d; Ending combat round and resetting" @ 0x007bfbc8
    ///   - "CSWSCombatRound::IncrementTimer - %s Master IS found (%x) and round has expired (%d %d); Resetting" @ 0x007bfc28
    ///   - "CSWSCombatRound::IncrementTimer - %s Master cannot be found and round has expired; Resetting" @ 0x007bfc90
    ///   - "CSWSCombatRound::DecrementPauseTimer - %s Master cannot be found expire the round; Resetting" @ 0x007bfcf0
    /// - 3-second rounds with timer-based attack scheduling
    /// - Round timing: Timer checks at 0.5s, 1.5s, 2.5s, 3.0s
    /// - Starting (0.0s): Initialize animations
    /// - FirstAttack (0.5s): Primary attack
    /// - SecondAttack (1.5s): Offhand/counter if dual wielding
    /// - Cooldown (2.5s): Return to ready
    /// - Finished (3.0s): Complete
    ///
    /// Attacks per round based on BAB:
    /// - BAB 1-5: 1 attack
    /// - BAB 6-10: 2 attacks (iterative: +0, -5)
    /// - BAB 11-15: 3 attacks (iterative: +0, -5, -10)
    /// - BAB 16+: 4 attacks (iterative: +0, -5, -10, -15)
    ///
    /// Dual wielding adds extra attacks but with penalties:
    /// - Offhand: -10 base penalty, -4 with Two-Weapon Fighting feat
    /// - Main hand with offhand: -6 base penalty, -2 with TWF feat
    /// </remarks>
    public class CombatRound
    {
        /// <summary>
        /// Duration of a full combat round in seconds.
        /// </summary>
        public const float RoundDuration = 3.0f;

        /// <summary>
        /// Time of first attack phase.
        /// </summary>
        public const float FirstAttackTime = 0.5f;

        /// <summary>
        /// Time of second attack phase.
        /// </summary>
        public const float SecondAttackTime = 1.5f;

        /// <summary>
        /// Time of cooldown phase.
        /// </summary>
        public const float CooldownTime = 2.5f;

        private readonly List<AttackAction> _scheduledAttacks;
        private int _currentAttackIndex;

        public CombatRound(IEntity attacker, IEntity target)
        {
            Attacker = attacker ?? throw new ArgumentNullException("attacker");
            Target = target ?? throw new ArgumentNullException("target");
            _scheduledAttacks = new List<AttackAction>();
            _currentAttackIndex = 0;

            Phase = CombatRoundPhase.Starting;
            ElapsedTime = 0f;
            IsComplete = false;
        }

        /// <summary>
        /// The entity making attacks.
        /// </summary>
        public IEntity Attacker { get; private set; }

        /// <summary>
        /// The target entity.
        /// </summary>
        public IEntity Target { get; private set; }

        /// <summary>
        /// Current round phase.
        /// </summary>
        public CombatRoundPhase Phase { get; private set; }

        /// <summary>
        /// Elapsed time in this round.
        /// </summary>
        public float ElapsedTime { get; private set; }

        /// <summary>
        /// Whether the round is complete.
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Scheduled attacks for this round.
        /// </summary>
        public IReadOnlyList<AttackAction> ScheduledAttacks
        {
            get { return _scheduledAttacks; }
        }

        /// <summary>
        /// Number of attacks scheduled.
        /// </summary>
        public int AttackCount
        {
            get { return _scheduledAttacks.Count; }
        }

        /// <summary>
        /// Schedules an attack for this round.
        /// </summary>
        public void ScheduleAttack(AttackAction attack)
        {
            if (attack != null)
            {
                _scheduledAttacks.Add(attack);
            }
        }

        /// <summary>
        /// Updates the combat round.
        /// </summary>
        /// <param name="deltaTime">Time since last update</param>
        /// <returns>Attack to execute this frame, or null</returns>
        public AttackAction Update(float deltaTime)
        {
            if (IsComplete)
            {
                return null;
            }

            ElapsedTime += deltaTime;
            AttackAction attackToExecute = null;

            // Check phase transitions and execute attacks
            if (Phase == CombatRoundPhase.Starting && ElapsedTime >= FirstAttackTime)
            {
                Phase = CombatRoundPhase.FirstAttack;
                attackToExecute = GetNextAttack();
            }
            else if (Phase == CombatRoundPhase.FirstAttack && ElapsedTime >= SecondAttackTime)
            {
                Phase = CombatRoundPhase.SecondAttack;
                attackToExecute = GetNextAttack();
            }
            else if (Phase == CombatRoundPhase.SecondAttack && ElapsedTime >= CooldownTime)
            {
                Phase = CombatRoundPhase.Cooldown;
                // Execute remaining attacks
                attackToExecute = GetNextAttack();
            }
            else if (ElapsedTime >= RoundDuration)
            {
                Phase = CombatRoundPhase.Finished;
                IsComplete = true;
            }

            return attackToExecute;
        }

        /// <summary>
        /// Gets the next scheduled attack.
        /// </summary>
        private AttackAction GetNextAttack()
        {
            if (_currentAttackIndex < _scheduledAttacks.Count)
            {
                return _scheduledAttacks[_currentAttackIndex++];
            }
            return null;
        }

        /// <summary>
        /// Aborts the combat round.
        /// </summary>
        public void Abort()
        {
            IsComplete = true;
            Phase = CombatRoundPhase.Finished;
        }

        /// <summary>
        /// Gets the progress through the round (0-1).
        /// </summary>
        public float Progress
        {
            get { return Math.Min(1f, ElapsedTime / RoundDuration); }
        }
    }

    /// <summary>
    /// Represents a single attack action within a round.
    /// </summary>
    public class AttackAction
    {
        public AttackAction(IEntity attacker, IEntity target)
        {
            Attacker = attacker;
            Target = target;
            AttackBonus = 0;
            IsOffhand = false;
            WeaponDamageRoll = "1d4";
            DamageBonus = 0;
        }

        /// <summary>
        /// The attacking entity.
        /// </summary>
        public IEntity Attacker { get; set; }

        /// <summary>
        /// The target entity.
        /// </summary>
        public IEntity Target { get; set; }

        /// <summary>
        /// Total attack bonus for this attack.
        /// </summary>
        public int AttackBonus { get; set; }

        /// <summary>
        /// Whether this is an offhand attack.
        /// </summary>
        public bool IsOffhand { get; set; }

        /// <summary>
        /// Weapon damage roll (e.g., "1d8").
        /// </summary>
        public string WeaponDamageRoll { get; set; }

        /// <summary>
        /// Bonus damage (strength, effects, etc.).
        /// </summary>
        public int DamageBonus { get; set; }

        /// <summary>
        /// Critical threat range (default 20).
        /// </summary>
        public int CriticalThreat { get; set; } = 20;

        /// <summary>
        /// Critical multiplier (default x2).
        /// </summary>
        public int CriticalMultiplier { get; set; } = 2;

        /// <summary>
        /// Damage type (from baseitems.2da).
        /// </summary>
        public int DamageType { get; set; }
    }
}
