using System;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Components;

namespace Odyssey.Kotor.Combat
{
    /// <summary>
    /// Attack result types.
    /// </summary>
    public enum AttackResult
    {
        Miss = 0,
        Hit = 1,
        CriticalHit = 2,
        AutomaticMiss = 3,  // Natural 1
        AutomaticHit = 4   // Natural 20
    }

    /// <summary>
    /// Result of an attack roll.
    /// </summary>
    public class AttackRollResult
    {
        public int D20Roll { get; set; }
        public int TotalAttack { get; set; }
        public int TargetDefense { get; set; }
        public AttackResult Result { get; set; }
        public bool IsCriticalThreat { get; set; }
        public int ConfirmationRoll { get; set; }
        public int TotalDamage { get; set; }
        public int BaseDamage { get; set; }
        public int BonusDamage { get; set; }
        public bool Resisted { get; set; }
    }

    /// <summary>
    /// Handles D20 combat calculations for KOTOR.
    /// </summary>
    /// <remarks>
    /// KOTOR D20 Combat System:
    /// 
    /// Attack Roll:
    /// - Roll d20 + Attack Bonus vs Defense
    /// - Natural 1 always misses
    /// - Natural 20 always hits (and threatens critical)
    /// 
    /// Attack Bonus = BAB + STR mod (melee) or DEX mod (ranged) + modifiers
    /// Defense = 10 + DEX mod + Armor + Deflection + Class bonus
    /// 
    /// Critical Hits:
    /// - Threat range (usually 19-20 or 20)
    /// - Confirmation roll: d20 + attack bonus vs defense
    /// - If confirmed: damage x multiplier (usually x2)
    /// 
    /// Damage:
    /// - Weapon dice + STR mod (melee) + bonuses
    /// - Resistances reduce damage
    /// </remarks>
    public class DamageCalculator
    {
        private readonly Random _random;

        public DamageCalculator()
        {
            _random = new Random();
        }

        public DamageCalculator(int seed)
        {
            _random = new Random(seed);
        }

        /// <summary>
        /// Rolls a d20.
        /// </summary>
        public int RollD20()
        {
            return _random.Next(1, 21);
        }

        /// <summary>
        /// Rolls dice in "NdX" format (e.g., "2d6").
        /// </summary>
        public int RollDice(string diceString)
        {
            if (string.IsNullOrEmpty(diceString))
            {
                return 0;
            }

            // Parse "NdX" format
            int dIndex = diceString.IndexOf('d');
            if (dIndex <= 0)
            {
                return 0;
            }

            int numDice, dieSides;
            if (!int.TryParse(diceString.Substring(0, dIndex), out numDice) ||
                !int.TryParse(diceString.Substring(dIndex + 1), out dieSides))
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < numDice; i++)
            {
                total += _random.Next(1, dieSides + 1);
            }
            return total;
        }

        /// <summary>
        /// Resolves an attack roll.
        /// </summary>
        public AttackRollResult ResolveAttack(AttackAction attack)
        {
            if (attack == null || attack.Attacker == null || attack.Target == null)
            {
                return new AttackRollResult { Result = AttackResult.Miss };
            }

            var result = new AttackRollResult();

            // Get target defense
            var targetStats = attack.Target.GetComponent<IStatsComponent>();
            result.TargetDefense = targetStats != null ? targetStats.ArmorClass : 10;

            // Roll attack
            result.D20Roll = RollD20();
            result.TotalAttack = result.D20Roll + attack.AttackBonus;

            // Check for automatic hit/miss
            if (result.D20Roll == 1)
            {
                result.Result = AttackResult.AutomaticMiss;
                return result;
            }

            if (result.D20Roll == 20)
            {
                result.Result = AttackResult.AutomaticHit;
                result.IsCriticalThreat = true;
            }
            else if (result.D20Roll >= attack.CriticalThreat)
            {
                // Check if hit
                if (result.TotalAttack >= result.TargetDefense)
                {
                    result.Result = AttackResult.Hit;
                    result.IsCriticalThreat = true;
                }
                else
                {
                    result.Result = AttackResult.Miss;
                    return result;
                }
            }
            else
            {
                // Normal attack roll
                if (result.TotalAttack >= result.TargetDefense)
                {
                    result.Result = AttackResult.Hit;
                }
                else
                {
                    result.Result = AttackResult.Miss;
                    return result;
                }
            }

            // Critical confirmation
            if (result.IsCriticalThreat)
            {
                result.ConfirmationRoll = RollD20();
                int totalConfirm = result.ConfirmationRoll + attack.AttackBonus;
                if (totalConfirm >= result.TargetDefense)
                {
                    result.Result = AttackResult.CriticalHit;
                }
            }

            // Calculate damage
            result.BaseDamage = RollDice(attack.WeaponDamageRoll);
            result.BonusDamage = attack.DamageBonus;
            result.TotalDamage = result.BaseDamage + result.BonusDamage;

            // Apply critical multiplier
            if (result.Result == AttackResult.CriticalHit)
            {
                result.TotalDamage *= attack.CriticalMultiplier;
            }

            // Minimum 1 damage on a hit
            if (result.TotalDamage < 1)
            {
                result.TotalDamage = 1;
            }

            return result;
        }

        /// <summary>
        /// Calculates the number of attacks per round based on BAB.
        /// </summary>
        public int CalculateAttacksPerRound(int baseAttackBonus, bool isDualWielding = false)
        {
            // Base attacks from BAB
            int attacks;
            if (baseAttackBonus >= 16)
            {
                attacks = 4;
            }
            else if (baseAttackBonus >= 11)
            {
                attacks = 3;
            }
            else if (baseAttackBonus >= 6)
            {
                attacks = 2;
            }
            else
            {
                attacks = 1;
            }

            // Dual wielding adds an extra attack
            if (isDualWielding)
            {
                attacks++;
            }

            return attacks;
        }

        /// <summary>
        /// Calculates attack bonuses for multiple attacks in a round.
        /// </summary>
        /// <param name="baseAttackBonus">Base attack bonus</param>
        /// <param name="attackNumber">Which attack (0 = first, 1 = second, etc.)</param>
        /// <param name="isOffhand">Whether this is an offhand attack</param>
        /// <param name="hasTwoWeaponFighting">Has Two-Weapon Fighting feat</param>
        /// <returns>Attack bonus for this attack</returns>
        public int CalculateIterativeAttackBonus(int baseAttackBonus, int attackNumber, bool isOffhand, bool hasTwoWeaponFighting = false)
        {
            // Iterative attacks get -5 per attack after the first
            int iterativePenalty = attackNumber * 5;
            int bonus = baseAttackBonus - iterativePenalty;

            // Dual wielding penalties
            if (isOffhand)
            {
                // Offhand: -10 base, -4 with TWF feat
                bonus -= hasTwoWeaponFighting ? 4 : 10;
            }
            else if (attackNumber > 0)
            {
                // Main hand with offhand: -6 base, -2 with TWF feat
                bonus -= hasTwoWeaponFighting ? 2 : 6;
            }

            return bonus;
        }

        /// <summary>
        /// Calculates damage bonus from strength for melee.
        /// </summary>
        public int CalculateMeleeDamageBonus(IEntity attacker, bool isTwoHanded = false, bool isOffhand = false)
        {
            var stats = attacker.GetComponent<IStatsComponent>();
            if (stats == null)
            {
                return 0;
            }

            int strMod = stats.GetAbilityModifier(Ability.Strength);

            if (isTwoHanded)
            {
                // Two-handed: 1.5x STR bonus
                return (int)(strMod * 1.5f);
            }
            else if (isOffhand)
            {
                // Offhand: 0.5x STR bonus
                return strMod / 2;
            }
            else
            {
                return strMod;
            }
        }

        /// <summary>
        /// Applies damage to a target.
        /// </summary>
        /// <returns>Actual damage dealt after resistances</returns>
        public int ApplyDamage(IEntity target, int damage, int damageType)
        {
            if (target == null || damage <= 0)
            {
                return 0;
            }

            // Get stats component
            var stats = target.GetComponent<StatsComponent>();
            if (stats == null)
            {
                return 0;
            }

            // TODO: Apply damage resistances based on damageType

            // Apply damage
            int actualDamage = stats.TakeDamage(damage);
            return actualDamage;
        }

        /// <summary>
        /// Makes a saving throw.
        /// </summary>
        /// <param name="target">Entity making the save</param>
        /// <param name="saveType">0=Fort, 1=Ref, 2=Will</param>
        /// <param name="dc">Difficulty class</param>
        /// <returns>True if save succeeded</returns>
        public bool MakeSavingThrow(IEntity target, int saveType, int dc)
        {
            if (target == null)
            {
                return false;
            }

            var stats = target.GetComponent<StatsComponent>();
            if (stats == null)
            {
                return false;
            }

            int roll = RollD20();
            return stats.MakeSavingThrow(saveType, dc, roll);
        }
    }
}
