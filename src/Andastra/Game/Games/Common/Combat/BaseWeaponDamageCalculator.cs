using System;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Games.Common.Combat
{
    /// <summary>
    /// Abstract base class for weapon damage calculation across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Weapon Damage Calculator:
    /// - Common functionality shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Base classes MUST only contain functionality that is identical across ALL engines
    /// - Engine-specific details MUST be in subclasses (OdysseyWeaponDamageCalculator, AuroraWeaponDamageCalculator, EclipseWeaponDamageCalculator)
    /// - Common: Dice rolling, damage calculation structure, ability modifier application, offhand logic, critical multiplier, minimum damage enforcement
    /// - Engine-specific: 2DA table lookups, finesse feat checking, lightsaber detection, weapon slot numbers, base item data structures
    /// - Damage formula (common): Roll(damagedice * damagedie) + damagebonus + ability modifier
    /// - Offhand attacks: Get half ability modifier (abilityMod / 2) - common across engines
    /// - Critical hits: Multiply damage by critmult - common across engines
    /// - Unarmed damage: 1d3 (1 die, size 3) if no weapon equipped - common across engines
    /// </remarks>
    public abstract class BaseWeaponDamageCalculator
    {
        protected readonly Random _random;

        /// <summary>
        /// Gets the main hand weapon slot number (engine-specific).
        /// </summary>
        protected abstract int MainHandWeaponSlot { get; }

        /// <summary>
        /// Gets the offhand weapon slot number (engine-specific).
        /// </summary>
        protected abstract int OffHandWeaponSlot { get; }

        /// <summary>
        /// Gets the unarmed damage die count (engine-specific, default 1).
        /// </summary>
        protected virtual int UnarmedDamageDice => 1;

        /// <summary>
        /// Gets the unarmed damage die size (engine-specific, default 3).
        /// </summary>
        protected virtual int UnarmedDamageDie => 3;

        protected BaseWeaponDamageCalculator()
        {
            _random = new Random();
        }

        protected BaseWeaponDamageCalculator(int seed)
        {
            _random = new Random(seed);
        }

        /// <summary>
        /// Calculates weapon damage for an attack.
        /// </summary>
        /// <param name="attacker">The attacking entity.</param>
        /// <param name="isOffhand">Whether this is an offhand attack.</param>
        /// <param name="isCritical">Whether this is a critical hit.</param>
        /// <returns>Total damage amount.</returns>
        public int CalculateDamage(IEntity attacker, bool isOffhand = false, bool isCritical = false)
        {
            if (attacker == null)
            {
                return 0;
            }

            // Get equipped weapon
            IInventoryComponent inventory = attacker.GetComponent<IInventoryComponent>();
            if (inventory == null)
            {
                return 0;
            }

            // Get weapon from appropriate slot (engine-specific slot numbers)
            int weaponSlot = isOffhand ? OffHandWeaponSlot : MainHandWeaponSlot;
            IEntity weapon = inventory.GetItemInSlot(weaponSlot);

            // If no weapon in main hand and not offhand, try offhand
            if (weapon == null && !isOffhand)
            {
                weapon = inventory.GetItemInSlot(OffHandWeaponSlot);
            }

            if (weapon == null)
            {
                // Unarmed damage (engine-specific dice)
                return RollDice(UnarmedDamageDice, UnarmedDamageDie);
            }

            // Get base item ID from weapon (common pattern)
            int baseItemId = GetBaseItemId(weapon);
            if (baseItemId < 0)
            {
                // Fallback to unarmed
                return RollDice(UnarmedDamageDice, UnarmedDamageDie);
            }

            // Get damage dice from 2DA table (engine-specific)
            int damageDice;
            int damageDie;
            int damageBonus;
            if (!GetDamageDiceFromTable(baseItemId, out damageDice, out damageDie, out damageBonus))
            {
                // Fallback to unarmed if table lookup fails
                return RollDice(UnarmedDamageDice, UnarmedDamageDie);
            }

            // Roll damage dice (common)
            int rolledDamage = RollDice(damageDice, damageDie);

            // Add damage bonus (common)
            int totalDamage = rolledDamage + damageBonus;

            // Add ability modifier (common pattern, engine-specific ability selection)
            IStatsComponent stats = attacker.GetComponent<IStatsComponent>();
            if (stats != null)
            {
                // Determine which ability score to use for damage (engine-specific)
                Ability attackAbility = DetermineDamageAbility(attacker, weapon, baseItemId);

                int abilityMod = stats.GetAbilityModifier(attackAbility);

                // Offhand attacks get half ability modifier (common)
                if (isOffhand)
                {
                    abilityMod = abilityMod / 2;
                }

                totalDamage += abilityMod;
            }

            // Apply critical multiplier (common pattern, engine-specific multiplier lookup)
            if (isCritical)
            {
                int critMult = GetCriticalMultiplier(baseItemId);
                totalDamage *= critMult;
            }

            // Minimum 1 damage (common)
            return Math.Max(1, totalDamage);
        }

        /// <summary>
        /// Gets damage dice information from the engine-specific 2DA table.
        /// </summary>
        /// <param name="baseItemId">The base item ID.</param>
        /// <param name="damageDice">Output: Number of damage dice.</param>
        /// <param name="damageDie">Output: Size of damage die.</param>
        /// <param name="damageBonus">Output: Base damage bonus.</param>
        /// <returns>True if successful, false otherwise.</returns>
        protected abstract bool GetDamageDiceFromTable(int baseItemId, out int damageDice, out int damageDie, out int damageBonus);

        /// <summary>
        /// Determines which ability score to use for weapon damage calculation (engine-specific).
        /// </summary>
        /// <param name="attacker">The attacking entity.</param>
        /// <param name="weapon">The weapon entity.</param>
        /// <param name="baseItemId">The base item ID.</param>
        /// <returns>The ability score to use (DEX for ranged/finesse, STR otherwise).</returns>
        protected abstract Ability DetermineDamageAbility(IEntity attacker, IEntity weapon, int baseItemId);

        /// <summary>
        /// Gets the critical multiplier for a weapon from the engine-specific 2DA table.
        /// </summary>
        /// <param name="baseItemId">The base item ID.</param>
        /// <returns>The critical multiplier (default 2).</returns>
        protected abstract int GetCriticalMultiplier(int baseItemId);

        /// <summary>
        /// Gets the base item ID from a weapon entity (common pattern).
        /// </summary>
        protected virtual int GetBaseItemId(IEntity weapon)
        {
            if (weapon == null)
            {
                return -1;
            }

            // Get BaseItem ID from ItemComponent (common across engines)
            var itemComponent = weapon.GetComponent<IItemComponent>();
            if (itemComponent != null)
            {
                return itemComponent.BaseItem;
            }

            // Fallback: try entity data (common pattern)
            if (weapon is Core.Entities.Entity entity)
            {
                return entity.GetData<int>("BaseItem", -1);
            }

            return -1;
        }

        /// <summary>
        /// Rolls dice (e.g., 2d6 = RollDice(2, 6)) - common across all engines.
        /// </summary>
        protected int RollDice(int count, int dieSize)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += _random.Next(1, dieSize + 1);
            }
            return total;
        }

        /// <summary>
        /// Gets the critical threat range for a weapon from the engine-specific 2DA table.
        /// </summary>
        /// <param name="weapon">The weapon entity.</param>
        /// <returns>The critical threat range (default 20).</returns>
        public virtual int GetCriticalThreatRange(IEntity weapon)
        {
            if (weapon == null)
            {
                return 20; // Default
            }

            int baseItemId = GetBaseItemId(weapon);
            if (baseItemId < 0)
            {
                return 20;
            }

            return GetCriticalThreatRangeFromTable(baseItemId);
        }

        /// <summary>
        /// Gets the critical threat range from the engine-specific 2DA table.
        /// </summary>
        /// <param name="baseItemId">The base item ID.</param>
        /// <returns>The critical threat range (default 20).</returns>
        protected abstract int GetCriticalThreatRangeFromTable(int baseItemId);

        /// <summary>
        /// Gets the critical multiplier for a weapon (public API).
        /// </summary>
        /// <param name="weapon">The weapon entity.</param>
        /// <returns>The critical multiplier (default 2).</returns>
        public virtual int GetCriticalMultiplier(IEntity weapon)
        {
            if (weapon == null)
            {
                return 2; // Default
            }

            int baseItemId = GetBaseItemId(weapon);
            if (baseItemId < 0)
            {
                return 2;
            }

            return GetCriticalMultiplier(baseItemId);
        }
    }
}

