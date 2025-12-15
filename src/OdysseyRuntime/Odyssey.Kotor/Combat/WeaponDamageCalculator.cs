using System;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Kotor.Data;

namespace Odyssey.Kotor.Combat
{
    /// <summary>
    /// Calculates weapon damage from equipped items using baseitems.2da.
    /// </summary>
    /// <remarks>
    /// Weapon Damage Calculator:
    /// - Based on swkotor2.exe weapon damage calculation
    /// - Located via string references: "damagedice" @ 0x007c2e60, "damagedie" @ 0x007c2e70, "damagebonus" @ 0x007c2e80
    /// - "DamageDice" @ 0x007c2d3c, "DamageDie" @ 0x007c2d30 (damage dice fields)
    /// - "BaseItem" @ 0x007c2e90 (base item ID in item GFF), "weapontype" @ 0x007c2ea0
    /// - "OnHandDamageMod" @ 0x007c2e40, "OffHandDamageMod" @ 0x007c2e18 (damage modifiers)
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 (save item data), FUN_0050c510 @ 0x0050c510 (load item data)
    /// - Damage formula: Roll(damagedice * damagedie) + damagebonus + ability modifier
    /// - Ability modifier: STR for melee, DEX for ranged (or STR if weapon has finesse property)
    /// - Offhand attacks: Get half ability modifier (abilityMod / 2)
    /// - Critical hits: Multiply damage by critmult from baseitems.2da (crithitmult column)
    /// - Based on baseitems.2da columns: numdice/damagedice (dice count), dietoroll/damagedie (die size), damagebonus, crithitmult, critthreat
    /// - Weapon lookup: Get equipped weapon from inventory (RIGHTWEAPON slot 4, LEFTWEAPON slot 5), get BaseItem ID, lookup in baseitems.2da
    /// - Unarmed damage: 1d3 (1 die, size 3) if no weapon equipped
    /// </remarks>
    public class WeaponDamageCalculator
    {
        private readonly TwoDATableManager _tableManager;
        private readonly Random _random;

        public WeaponDamageCalculator(TwoDATableManager tableManager)
        {
            _tableManager = tableManager ?? throw new ArgumentNullException("tableManager");
            _random = new Random();
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

            // Get weapon from appropriate slot
            int weaponSlot = isOffhand ? 5 : 4; // LEFTWEAPON = 5, RIGHTWEAPON = 4
            IEntity weapon = inventory.GetItemInSlot(weaponSlot);
            
            // If no weapon in main hand and not offhand, try offhand
            if (weapon == null && !isOffhand)
            {
                weapon = inventory.GetItemInSlot(5); // Try left weapon
            }

            if (weapon == null)
            {
                // Unarmed damage (1d3)
                return RollDice(1, 3);
            }

            // Get base item ID from weapon
            // Note: This would come from the weapon's UTI template
            // For now, we'll need to get it from the weapon entity's data
            int baseItemId = GetBaseItemId(weapon);
            if (baseItemId < 0)
            {
                // Fallback to unarmed
                return RollDice(1, 3);
            }

            // Get base item data
            BaseItemData baseItem = _tableManager.GetBaseItem(baseItemId);
            if (baseItem == null)
            {
                return RollDice(1, 3); // Fallback
            }

            // Get damage dice from baseitems.2da
            // Note: Column names may vary - using common names
            int damageDice = baseItem.BaseItemId; // Placeholder - would get from 2DA
            int damageDie = 8; // Placeholder - would get from 2DA
            int damageBonus = 0; // Placeholder - would get from 2DA

            // Try to get from 2DA table directly
            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    // Column names from baseitems.2da (may vary - try multiple names)
                    // damagedice/numdice = number of dice
                    // damagedie/dietoroll = die size
                    // damagebonus = base damage bonus
                    damageDice = twoDARow.GetInteger("numdice", null) ?? 
                                 twoDARow.GetInteger("damagedice", 1) ?? 1;
                    damageDie = twoDARow.GetInteger("dietoroll", null) ?? 
                               twoDARow.GetInteger("damagedie", 8) ?? 8;
                    damageBonus = twoDARow.GetInteger("damagebonus", 0) ?? 0;
                }
            }
            catch
            {
                // Fallback values
                damageDice = 1;
                damageDie = 8;
                damageBonus = 0;
            }

            // Roll damage dice
            int rolledDamage = RollDice(damageDice, damageDie);

            // Add damage bonus
            int totalDamage = rolledDamage + damageBonus;

            // Add ability modifier
            IStatsComponent stats = attacker.GetComponent<IStatsComponent>();
            if (stats != null)
            {
                // Check if ranged weapon
                bool isRanged = baseItem.RangedWeapon;
                
                // Use DEX for ranged, STR for melee (simplified - would check for finesse)
                Ability attackAbility = isRanged ? Ability.Dexterity : Ability.Strength;
                int abilityMod = stats.GetAbilityModifier(attackAbility);
                
                // Offhand attacks get half ability modifier
                if (isOffhand)
                {
                    abilityMod = abilityMod / 2;
                }
                
                totalDamage += abilityMod;
            }

            // Apply critical multiplier
            if (isCritical)
            {
                int critMult = 2; // Default
                try
                {
                    var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                    if (twoDARow != null)
                    {
                        critMult = twoDARow.GetInteger("crithitmult", 2) ?? 2;
                    }
                }
                catch
                {
                    // Use default
                }
                
                totalDamage *= critMult;
            }

            return Math.Max(1, totalDamage); // Minimum 1 damage
        }

        /// <summary>
        /// Gets the base item ID from a weapon entity.
        /// </summary>
        private int GetBaseItemId(IEntity weapon)
        {
            if (weapon == null)
            {
                return -1;
            }

            // Get BaseItem ID from ItemComponent
            // Based on swkotor2.exe: BaseItem field in UTI GFF template
            // Located via string reference: "BaseItem" @ 0x007c2f34
            var itemComponent = weapon.GetComponent<Odyssey.Core.Interfaces.Components.IItemComponent>();
            if (itemComponent != null)
            {
                return itemComponent.BaseItem;
            }

            // Fallback: try entity data
            if (weapon is Core.Entities.Entity entity)
            {
                return entity.GetData<int>("BaseItem", -1);
            }

            return -1;
        }

        /// <summary>
        /// Rolls dice (e.g., 2d6 = RollDice(2, 6)).
        /// </summary>
        private int RollDice(int count, int dieSize)
        {
            int total = 0;
            for (int i = 0; i < count; i++)
            {
                total += _random.Next(1, dieSize + 1);
            }
            return total;
        }

        /// <summary>
        /// Gets the critical threat range for a weapon.
        /// </summary>
        public int GetCriticalThreatRange(IEntity weapon)
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

            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    return twoDARow.GetInteger("critthreat", 20) ?? 20;
                }
            }
            catch
            {
                // Fallback
            }

            return 20;
        }

        /// <summary>
        /// Gets the critical multiplier for a weapon.
        /// </summary>
        public int GetCriticalMultiplier(IEntity weapon)
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

            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    return twoDARow.GetInteger("crithitmult", 2) ?? 2;
                }
            }
            catch
            {
                // Fallback
            }

            return 2;
        }
    }
}

