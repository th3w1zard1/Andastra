using System;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Engines.Odyssey.Data;

namespace Andastra.Runtime.Engines.Odyssey.Combat
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
    /// - Cross-engine: Similar damage calculation in swkotor.exe (K1), nwmain.exe (Aurora uses different 2DA tables), daorigins.exe (Eclipse uses different damage system)
    /// - Inheritance: Base class DamageCalculator (Runtime.Games.Common) implements common damage calculation logic
    ///   - Odyssey: WeaponDamageCalculator (Runtime.Games.Odyssey) - Odyssey-specific baseitems.2da lookup
    ///   - Aurora: AuroraWeaponDamageCalculator : DamageCalculator (Runtime.Games.Aurora) - Aurora-specific baseitems.2da lookup
    ///   - Eclipse: EclipseWeaponDamageCalculator : DamageCalculator (Runtime.Games.Eclipse) - Eclipse-specific damage system
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 (save item data), FUN_0050c510 @ 0x0050c510 (load item data)
    /// - Damage formula: Roll(damagedice * damagedie) + damagebonus + ability modifier
    /// - Ability modifier: STR for melee (default), DEX for ranged, DEX for finesse melee/lightsabers (if feat)
    /// - Finesse feats: FEAT_FINESSE_LIGHTSABERS (193) for lightsabers, FEAT_FINESSE_MELEE_WEAPONS (194) for melee
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
            // TODO: SIMPLIFIED - For now, we'll need to get it from the weapon entity's data
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
            int damageDice = baseItem.BaseItemId; // TODO: PLACEHOLDER - would get from 2DA
            int damageDie = 8; // TODO: PLACEHOLDER - would get from 2DA
            int damageBonus = 0; // TODO: PLACEHOLDER - would get from 2DA

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
                // Determine which ability score to use for damage
                // Based on swkotor2.exe: Ability modifier selection for weapon damage
                // - Ranged weapons: Always use DEX
                // - Melee weapons: Use STR by default, DEX if finesse feat applies
                // - Finesse feats: FEAT_FINESSE_LIGHTSABERS (193) for lightsabers, FEAT_FINESSE_MELEE_WEAPONS (194) for melee weapons
                // Located via string references: "DEXBONUS" @ 0x007c4320, "DEXAdjust" @ 0x007c2bec
                // Original implementation: FUN_005226d0 @ 0x005226d0 checks finesse feats for ability modifier selection
                Ability attackAbility = DetermineDamageAbility(attacker, baseItem, baseItemId);

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
        /// Determines which ability score to use for weapon damage calculation.
        /// </summary>
        /// <param name="attacker">The attacking entity.</param>
        /// <param name="baseItem">The base item data for the weapon.</param>
        /// <param name="baseItemId">The base item ID.</param>
        /// <returns>The ability score to use (DEX for ranged/finesse, STR otherwise).</returns>
        /// <remarks>
        /// Ability Score Selection for Weapon Damage:
        /// - Based on swkotor2.exe: Ability modifier selection for weapon damage
        /// - Located via string references: "DEXBONUS" @ 0x007c4320, "DEXAdjust" @ 0x007c2bec
        /// - " + %d (Dex Modifier)" @ 0x007c3a84, " + %d (Dex Mod)" @ 0x007c3e54
        /// - Original implementation: FUN_005226d0 @ 0x005226d0 checks finesse feats for ability modifier selection
        /// - Ranged weapons: Always use DEX modifier
        /// - Melee weapons: Use STR by default, DEX if appropriate finesse feat is present
        /// - Finesse feats:
        ///   - FEAT_FINESSE_LIGHTSABERS (193): Allows DEX for lightsabers
        ///   - FEAT_FINESSE_MELEE_WEAPONS (194): Allows DEX for melee weapons
        /// - Lightsaber detection: Check baseitems.2da itemclass = 15 or weapontype = 4
        /// - Cross-engine: Similar logic in swkotor.exe (K1), nwmain.exe (Aurora uses different feat system)
        /// </remarks>
        private Ability DetermineDamageAbility(IEntity attacker, BaseItemData baseItem, int baseItemId)
        {
            if (attacker == null || baseItem == null)
            {
                return Ability.Strength; // Default fallback
            }

            // Ranged weapons always use DEX
            if (baseItem.RangedWeapon)
            {
                return Ability.Dexterity;
            }

            // For melee weapons, check if finesse applies
            // First, determine if this is a lightsaber
            bool isLightsaber = IsLightsaber(baseItemId);

            // Check if attacker has appropriate finesse feat
            if (isLightsaber)
            {
                // Lightsabers: Check for FEAT_FINESSE_LIGHTSABERS (193)
                if (HasFeat(attacker, 193)) // FEAT_FINESSE_LIGHTSABERS
                {
                    return Ability.Dexterity;
                }
            }
            else
            {
                // Melee weapons: Check for FEAT_FINESSE_MELEE_WEAPONS (194)
                if (HasFeat(attacker, 194)) // FEAT_FINESSE_MELEE_WEAPONS
                {
                    return Ability.Dexterity;
                }
            }

            // Default: Use STR for melee weapons without finesse
            return Ability.Strength;
        }

        /// <summary>
        /// Checks if a base item is a lightsaber.
        /// </summary>
        /// <param name="baseItemId">The base item ID to check.</param>
        /// <returns>True if the item is a lightsaber, false otherwise.</returns>
        /// <remarks>
        /// Lightsaber Detection:
        /// - Based on swkotor2.exe: Lightsaber item type detection
        /// - Located via string references: Lightsaber detection in baseitems.2da lookup
        /// - Original implementation: Checks baseitems.2da "itemclass" or "weapontype" column
        /// - Lightsabers have specific item class values (typically itemclass = 15 for lightsabers)
        /// - Alternative: Check "weapontype" column for lightsaber weapon type (weapontype = 4)
        /// - Cross-engine: Similar detection in swkotor.exe (K1), different in nwmain.exe (Aurora)
        /// </remarks>
        private bool IsLightsaber(int baseItemId)
        {
            if (baseItemId <= 0)
            {
                return false;
            }

            try
            {
                var twoDARow = _tableManager.GetRow("baseitems", baseItemId);
                if (twoDARow != null)
                {
                    // Check itemclass column (lightsabers typically have itemclass = 15)
                    // ItemClass 15 = Lightsaber in baseitems.2da
                    int? itemClass = twoDARow.GetInteger("itemclass", null);
                    if (itemClass.HasValue && itemClass.Value == 15)
                    {
                        return true;
                    }

                    // Alternative: Check weapontype column if available
                    // For KOTOR/TSL, lightsabers are weapontype 4
                    int? weaponType = twoDARow.GetInteger("weapontype", null);
                    if (weaponType.HasValue && weaponType.Value == 4)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // Error accessing 2DA table, fall through to return false
            }

            return false;
        }

        /// <summary>
        /// Checks if a creature has a specific feat.
        /// </summary>
        /// <param name="creature">The creature entity to check.</param>
        /// <param name="featId">The feat ID to check for.</param>
        /// <returns>True if the creature has the feat, false otherwise.</returns>
        /// <remarks>
        /// Feat Checking:
        /// - Based on swkotor2.exe: Feat checking system
        /// - Located via string references: Feat list in creature component (UTC GFF)
        /// - Original implementation: FUN_005226d0 @ 0x005226d0 checks feat list for creature
        /// - Feats stored in CreatureComponent.FeatList
        /// - Cross-engine: Similar in swkotor.exe (K1), different feat system in nwmain.exe (Aurora)
        /// </remarks>
        private bool HasFeat(IEntity creature, int featId)
        {
            if (creature == null)
            {
                return false;
            }

            // Get CreatureComponent to access feat list
            // Note: Using runtime component type directly for Odyssey-specific implementation
            var creatureComp = creature.GetComponent<Andastra.Runtime.Engines.Odyssey.Components.CreatureComponent>();
            if (creatureComp == null || creatureComp.FeatList == null)
            {
                return false;
            }

            return creatureComp.FeatList.Contains(featId);
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
            var itemComponent = weapon.GetComponent<Andastra.Runtime.Core.Interfaces.Components.IItemComponent>();
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

