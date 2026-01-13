using System;
using System.Linq;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;

namespace Andastra.Game.Engines.Odyssey.Combat
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
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) combat system
    /// - Located via string references: "DamageValue" @ 0x007bf890, "DamageList" @ 0x007bf89c
    /// - "ScriptDamaged" @ 0x007bee70, "OnDamaged" @ 0x007c1a80, "OnDamage" @ 0x007cb410
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14
    /// - Damage types: "DAM_HP" @ 0x007bf130, "DAM_STR" @ 0x007bf120 (ability damage)
    /// - Damage fields: "DamageDie" @ 0x007c2d30, "DamageDice" @ 0x007c2d3c, "DamageFlags" @ 0x007c01a4
    /// - "DamageMult" @ 0x007c3974, "DamageDebugText" @ 0x007bf82c
    /// - Damage modifiers: "OnHandDamageMod" @ 0x007c2e40, "OffHandDamageMod" @ 0x007c2e18
    /// - "FuryDamageBonus" @ 0x007c4150 (Jedi Guardian Fury form damage bonus)
    /// - Damage display strings:
    ///   - "Damage Roll: %d" @ 0x007c3d3c, "Damage Roll: %d (Critical x%d)" @ 0x007c3d4c
    ///   - " + %d (Effect Damage Bonus)" @ 0x007c3cd8
    ///   - " + %d (Effect Damage Bonus) (Critical x%d)" @ 0x007c3cf4
    ///   - " + %d (Special Attack Damage Bonus)" @ 0x007c3b60
    ///   - " + %d (Special Attack Damage Bonus) (Critical x%d)" @ 0x007c3b84
    ///   - " + %d (Power/Improved Power Attack Damage Bonus)" @ 0x007c3c10
    ///   - " + %d (Power/Improved Power Attack Damage Bonus) (Critical x%d)" @ 0x007c3c48
    ///   - " + %d (Sneak Attack Damage)" @ 0x007c3d20
    /// - Poison/ability damage trace: "POISONTRACE: Applying HP damage: %d\n" @ 0x007bf088
    ///   - Similar traces for STR, DEX, CON, INT, WIS, CHR, FP damage
    /// - Visual effects: "DamageHitVisual" @ 0x007c4dbc, "IPRP_DAMAGECOST" @ 0x007c4c38
    /// - Script hooks: "k_def_damage01" @ 0x007c7eb0 (damage defense script example)
    /// - Original implementation: D20 attack/damage resolution with critical hits, resistances
    /// - Damage calculation functions in combat system handle d20 rolls, modifiers, criticals
    ///
    /// Attack Roll:
    /// - Roll d20 + Attack Bonus vs Defense
    /// - Natural 1 always misses (automatic miss)
    /// - Natural 20 always hits (automatic hit) and threatens critical
    /// - Critical threat confirmed with second d20 roll + attack bonus vs defense
    ///
    /// Attack Bonus = BAB + STR mod (melee) or DEX mod (ranged/finesse) + weapon bonus + modifiers
    /// Defense = 10 + DEX mod + Armor bonus + Natural AC + Deflection + Class bonus + shield bonus
    ///
    /// Critical Hits:
    /// - Threat range (usually 19-20 or 20, from weapon properties)
    /// - Confirmation roll: d20 + attack bonus vs defense (must hit to confirm)
    /// - If confirmed: damage x multiplier (usually x2, from weapon properties)
    ///
    /// Damage:
    /// - Weapon dice (e.g., 1d8) + STR mod (melee) or DEX mod (ranged) + bonuses
    /// - Two-handed weapons: 1.5x STR bonus
    /// - Offhand weapons: 0.5x STR bonus
    /// - Resistances reduce damage (stacking, minimum 0)
    /// - Immunities negate damage completely
    /// </remarks>
    public class DamageCalculator
    {
        private readonly Random _random;
        private readonly EffectSystem _effectSystem;

        public DamageCalculator()
        {
            _random = new Random();
            _effectSystem = null;
        }

        public DamageCalculator(int seed)
        {
            _random = new Random(seed);
            _effectSystem = null;
        }

        public DamageCalculator(EffectSystem effectSystem)
        {
            _random = new Random();
            _effectSystem = effectSystem;
        }

        public DamageCalculator(int seed, EffectSystem effectSystem)
        {
            _random = new Random(seed);
            _effectSystem = effectSystem;
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
            IStatsComponent targetStats = attack.Target.GetComponent<IStatsComponent>();
            result.TargetDefense = targetStats != null ? targetStats.ArmorClass : 10;

            // Roll attack
            result.D20Roll = RollD20();
            result.TotalAttack = result.D20Roll + attack.AttackBonus;

            // Check for automatic hit/miss
            // Natural 1 always misses, even with AssuredHit effect
            if (result.D20Roll == 1)
            {
                result.Result = AttackResult.AutomaticMiss;
                return result;
            }

            // Check for AssuredHit effect
            // Based on swkotor.exe: AssuredHit effect guarantees attack hits (bypasses AC check)
            // Located via string references: EffectAssuredHit @ routine 51
            // Original implementation: Effect flag that forces attack to hit unless natural 1
            bool hasAssuredHit = false;
            if (_effectSystem != null && attack.Attacker != null)
            {
                hasAssuredHit = _effectSystem.HasEffect(attack.Attacker, EffectType.AssuredHit);
            }

            // If AssuredHit is active, force a hit (natural 1 already handled above)
            if (hasAssuredHit)
            {
                result.Result = AttackResult.Hit;
                // Check if this would be a critical threat based on roll
                if (result.D20Roll >= attack.CriticalThreat)
                {
                    result.IsCriticalThreat = true;
                }
            }
            else if (result.D20Roll == 20)
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
            IStatsComponent stats = attacker.GetComponent<IStatsComponent>();
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
            StatsComponent stats = target.GetComponent<StatsComponent>();
            if (stats == null)
            {
                return 0;
            }

            // Apply damage resistances based on damageType
            int finalDamage = ApplyDamageResistances(target, damage, (DamageType)damageType);

            // Apply damage
            int actualDamage = stats.TakeDamage(finalDamage);
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

            StatsComponent stats = target.GetComponent<StatsComponent>();
            if (stats == null)
            {
                return false;
            }

            int roll = RollD20();
            return stats.MakeSavingThrow(saveType, dc, roll);
        }

        /// <summary>
        /// Applies damage resistances to reduce damage.
        /// </summary>
        /// <param name="target">The entity taking damage</param>
        /// <param name="damage">Base damage amount</param>
        /// <param name="damageType">Type of damage</param>
        /// <returns>Damage after resistances are applied</returns>
        private int ApplyDamageResistances(IEntity target, int damage, DamageType damageType)
        {
            if (target == null || damage <= 0 || _effectSystem == null)
            {
                return damage;
            }

            // Universal damage bypasses all resistances
            if (damageType == DamageType.Universal)
            {
                return damage;
            }

            int finalDamage = damage;
            int totalResistance = 0;
            bool hasImmunity = false;

            // Check all active effects for resistances/immunities
            foreach (var activeEffect in _effectSystem.GetEffects(target))
            {
                var effect = activeEffect.Effect;

                // Check for damage immunity
                if (effect.Type == EffectType.DamageImmunity)
                {
                    DamageType effectDamageType = (DamageType)effect.SubType;
                    if (effectDamageType == damageType || effectDamageType == DamageType.Universal)
                    {
                        hasImmunity = true;
                        break; // Immunity completely negates damage
                    }
                }

                // Check for damage resistance
                if (effect.Type == EffectType.DamageResistance && !hasImmunity)
                {
                    DamageType effectDamageType = (DamageType)effect.SubType;
                    if (effectDamageType == damageType || effectDamageType == DamageType.Universal)
                    {
                        // Add resistance amount (can stack)
                        totalResistance += effect.Amount;
                    }
                }
            }

            // Immunity completely negates damage
            if (hasImmunity)
            {
                return 0;
            }

            // Apply resistance (subtract from damage, minimum 0)
            finalDamage = Math.Max(0, finalDamage - totalResistance);

            return finalDamage;
        }
    }
}
