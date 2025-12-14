using System;
using System.Collections.Generic;
using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Combat
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
    /// - Starting (0.0s): Initialize animations
    /// - FirstAttack (0.5s): Primary attack
    /// - SecondAttack (1.5s): Offhand/counter if dual wielding
    /// - Cooldown (2.5s): Return to ready
    /// - Finished (3.0s): Complete
    /// 
    /// Attacks per round based on BAB:
    /// - BAB 1-5: 1 attack
    /// - BAB 6-10: 2 attacks
    /// - BAB 11-15: 3 attacks
    /// - BAB 16+: 4 attacks
    /// 
    /// Dual wielding adds extra attacks but with penalties.
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
