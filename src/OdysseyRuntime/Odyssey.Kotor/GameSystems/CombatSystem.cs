using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.GameSystems
{
    /// <summary>
    /// Handles combat resolution and timing.
    /// </summary>
    /// <remarks>
    /// Combat follows a round-based model (~3 second rounds).
    /// Attack resolution uses D20 mechanics.
    /// </remarks>
    public class CombatSystem
    {
        private readonly IWorld _world;
        private readonly Random _random;
        private readonly Dictionary<uint, CombatRound> _activeRounds;
        private readonly List<uint> _roundsToRemove;

        /// <summary>
        /// Round duration in seconds.
        /// </summary>
        public const float RoundDuration = 3.0f;

        public CombatSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _random = new Random();
            _activeRounds = new Dictionary<uint, CombatRound>();
            _roundsToRemove = new List<uint>();
        }

        /// <summary>
        /// Starts combat between an attacker and target.
        /// </summary>
        public void StartCombat(IEntity attacker, IEntity target)
        {
            if (attacker == null || target == null)
            {
                return;
            }

            // Don't start if already in combat with this target
            CombatRound existingRound;
            if (_activeRounds.TryGetValue(attacker.ObjectId, out existingRound))
            {
                if (existingRound.Target == target)
                {
                    return;
                }
            }

            var round = new CombatRound
            {
                Attacker = attacker,
                Target = target,
                State = CombatRoundState.Starting,
                TimeInState = 0f,
                RoundNumber = 0
            };

            _activeRounds[attacker.ObjectId] = round;

            // Mark creature as in combat
            var creature = attacker.GetComponent<CreatureComponent>();
            if (creature != null)
            {
                creature.IsInCombat = true;
                creature.CombatState = CombatRoundState.Starting;
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

            _activeRounds.Remove(entity.ObjectId);

            var creature = entity.GetComponent<CreatureComponent>();
            if (creature != null)
            {
                creature.IsInCombat = false;
                creature.CombatState = CombatRoundState.NotInCombat;
            }
        }

        /// <summary>
        /// Checks if entity is in combat.
        /// </summary>
        public bool IsInCombat(IEntity entity)
        {
            return entity != null && _activeRounds.ContainsKey(entity.ObjectId);
        }

        /// <summary>
        /// Updates all active combat rounds.
        /// </summary>
        public void Update(float deltaTime)
        {
            _roundsToRemove.Clear();

            foreach (var kvp in _activeRounds)
            {
                uint attackerId = kvp.Key;
                CombatRound round = kvp.Value;

                round.TimeInState += deltaTime;

                // State machine
                switch (round.State)
                {
                    case CombatRoundState.Starting:
                        UpdateStarting(round);
                        break;
                    case CombatRoundState.FirstAttack:
                        UpdateFirstAttack(round);
                        break;
                    case CombatRoundState.SecondAttack:
                        UpdateSecondAttack(round);
                        break;
                    case CombatRoundState.Cooldown:
                        UpdateCooldown(round);
                        break;
                    case CombatRoundState.Finished:
                        if (ShouldContinueCombat(round))
                        {
                            StartNewRound(round);
                        }
                        else
                        {
                            _roundsToRemove.Add(attackerId);
                        }
                        break;
                }
            }

            // Remove finished rounds
            foreach (uint id in _roundsToRemove)
            {
                var round = _activeRounds[id];
                EndCombat(round.Attacker);
            }
        }

        private void UpdateStarting(CombatRound round)
        {
            if (round.TimeInState >= 0.5f)
            {
                round.State = CombatRoundState.FirstAttack;
                round.TimeInState = 0f;
                ExecuteAttack(round, false);
            }
        }

        private void UpdateFirstAttack(CombatRound round)
        {
            if (round.TimeInState >= 1.0f)
            {
                if (HasOffhandWeapon(round.Attacker))
                {
                    round.State = CombatRoundState.SecondAttack;
                    round.TimeInState = 0f;
                    ExecuteAttack(round, true);
                }
                else
                {
                    round.State = CombatRoundState.Cooldown;
                    round.TimeInState = 0f;
                }
            }
        }

        private void UpdateSecondAttack(CombatRound round)
        {
            if (round.TimeInState >= 1.0f)
            {
                round.State = CombatRoundState.Cooldown;
                round.TimeInState = 0f;
            }
        }

        private void UpdateCooldown(CombatRound round)
        {
            if (round.TimeInState >= 0.5f)
            {
                round.State = CombatRoundState.Finished;
                round.TimeInState = 0f;
                round.RoundNumber++;

                // Fire OnEndRound script
                FireEndRoundEvent(round);
            }
        }

        private void ExecuteAttack(CombatRound round, bool offhand)
        {
            var attack = new Attack
            {
                Attacker = round.Attacker,
                Target = round.Target,
                IsOffhand = offhand
            };

            // Resolve attack
            attack.Result = ResolveAttack(attack);

            // Apply damage if hit
            if (attack.Result == AttackResult.Hit || attack.Result == AttackResult.CriticalHit)
            {
                attack.Damage = CalculateDamage(attack);
                attack.IsCritical = attack.Result == AttackResult.CriticalHit;
                ApplyDamage(attack);
            }

            // Store attack result
            if (offhand)
            {
                round.Attack2 = attack;
            }
            else
            {
                round.Attack1 = attack;
            }
        }

        private AttackResult ResolveAttack(Attack attack)
        {
            int roll = Roll(1, 20);

            // Natural 20 = always hit (check for crit)
            if (roll == 20)
            {
                int critConfirm = Roll(1, 20);
                int attackBonus = GetAttackBonus(attack.Attacker, attack.IsOffhand);
                int targetAC = GetArmorClass(attack.Target);

                if (critConfirm + attackBonus >= targetAC)
                {
                    return AttackResult.CriticalHit;
                }
                return AttackResult.Hit;
            }

            // Natural 1 = always miss
            if (roll == 1)
            {
                return AttackResult.Miss;
            }

            // Normal attack roll
            int bonus = GetAttackBonus(attack.Attacker, attack.IsOffhand);
            int ac = GetArmorClass(attack.Target);

            if (roll + bonus >= ac)
            {
                return AttackResult.Hit;
            }

            return AttackResult.Miss;
        }

        private int GetAttackBonus(IEntity attacker, bool offhand)
        {
            var creature = attacker.GetComponent<CreatureComponent>();
            if (creature == null)
            {
                return 0;
            }

            int bonus = creature.GetBaseAttackBonus();

            // Add strength/dexterity modifier
            // Melee uses Strength, ranged uses Dexterity
            bonus += creature.GetModifier(creature.Strength);

            // Two-weapon penalty
            if (offhand)
            {
                bonus -= GetTwoWeaponPenalty(attacker);
            }

            return bonus;
        }

        private int GetArmorClass(IEntity target)
        {
            // Base AC = 10
            int ac = 10;

            var creature = target.GetComponent<CreatureComponent>();
            if (creature != null)
            {
                // Dexterity modifier
                ac += creature.GetModifier(creature.Dexterity);

                // Natural AC
                ac += creature.NaturalAC;
            }

            // Equipment bonuses would be added here

            return ac;
        }

        private int GetTwoWeaponPenalty(IEntity attacker)
        {
            // Base penalty for two-weapon fighting
            // Reduced by feats
            return 6;
        }

        private bool HasOffhandWeapon(IEntity entity)
        {
            // Check if entity has an offhand weapon equipped
            return false; // Simplified for now
        }

        private int CalculateDamage(Attack attack)
        {
            // Base weapon damage
            int baseDamage = Roll(1, 8); // Default 1d8

            var creature = attack.Attacker.GetComponent<CreatureComponent>();
            if (creature != null)
            {
                // Strength modifier
                int strMod = creature.GetModifier(creature.Strength);
                if (attack.IsOffhand)
                {
                    strMod /= 2;
                }
                baseDamage += strMod;
            }

            // Critical multiplier
            if (attack.IsCritical)
            {
                baseDamage *= 2;
            }

            return Math.Max(1, baseDamage);
        }

        private void ApplyDamage(Attack attack)
        {
            var creature = attack.Target.GetComponent<CreatureComponent>();
            if (creature != null)
            {
                creature.CurrentHP -= attack.Damage;

                if (creature.CurrentHP <= 0)
                {
                    // Target is dead
                    creature.CurrentHP = 0;
                    OnEntityDeath(attack.Target, attack.Attacker);
                }
            }
        }

        private void OnEntityDeath(IEntity victim, IEntity killer)
        {
            // Fire death script would happen here
            // Remove from combat
            EndCombat(victim);
        }

        private void FireEndRoundEvent(CombatRound round)
        {
            // Fire OnEndRound script would happen here
        }

        private bool ShouldContinueCombat(CombatRound round)
        {
            // Check if combat should continue
            if (round.Target == null)
            {
                return false;
            }

            var targetCreature = round.Target.GetComponent<CreatureComponent>();
            if (targetCreature != null && targetCreature.CurrentHP <= 0)
            {
                return false;
            }

            // Check distance
            var attackerTransform = round.Attacker.GetComponent<TransformComponent>();
            var targetTransform = round.Target.GetComponent<TransformComponent>();
            if (attackerTransform != null && targetTransform != null)
            {
                float distance = Vector3.Distance(attackerTransform.Position, targetTransform.Position);
                if (distance > 50f) // Max attack range
                {
                    return false;
                }
            }

            return true;
        }

        private void StartNewRound(CombatRound round)
        {
            round.State = CombatRoundState.Starting;
            round.TimeInState = 0f;
            round.Attack1 = null;
            round.Attack2 = null;
        }

        private int Roll(int count, int sides)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += _random.Next(1, sides + 1);
            }
            return total;
        }
    }

    /// <summary>
    /// Represents an active combat round.
    /// </summary>
    public class CombatRound
    {
        public IEntity Attacker { get; set; }
        public IEntity Target { get; set; }
        public CombatRoundState State { get; set; }
        public float TimeInState { get; set; }
        public int RoundNumber { get; set; }
        public Attack Attack1 { get; set; }
        public Attack Attack2 { get; set; }
        public bool IsDuel { get; set; }
    }

    /// <summary>
    /// Represents a single attack within a round.
    /// </summary>
    public class Attack
    {
        public IEntity Attacker { get; set; }
        public IEntity Target { get; set; }
        public AttackResult Result { get; set; }
        public int Damage { get; set; }
        public DamageType DamageType { get; set; }
        public bool IsCritical { get; set; }
        public bool IsOffhand { get; set; }
    }

    /// <summary>
    /// Attack resolution result.
    /// </summary>
    public enum AttackResult
    {
        Invalid,
        Hit,
        CriticalHit,
        Miss,
        Parried,
        Deflected,
        AutomaticHit
    }

    /// <summary>
    /// Types of damage.
    /// </summary>
    public enum DamageType
    {
        Bludgeoning,
        Piercing,
        Slashing,
        Sonic,
        Fire,
        Cold,
        Electrical,
        Acid,
        Ion,
        Energy,
        Dark,
        Light
    }
}
