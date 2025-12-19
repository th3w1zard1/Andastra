using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Parsing;
using Andastra.Parsing.Installation;

namespace Andastra.Runtime.Engines.Odyssey.UI
{
    /// <summary>
    /// Upgrade screen implementation for KOTOR 2: TSL (swkotor2.exe).
    /// </summary>
    /// <remarks>
    /// K2 Upgrade Screen Implementation:
    /// - Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 (constructor loads "upgradeitems_p")
    /// - Based on swkotor2.exe: FUN_00730970 @ 0x00730970 (constructor loads "upcrystals" @ 0x00730c40)
    /// - Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 (upgrade button click handler)
    /// - Based on swkotor2.exe: FUN_00729640 @ 0x00729640 (ApplyUpgrade implementation)
    /// - Located via string references: "upgradeitems_p" @ 0x007d09e4, "upcrystals" @ 0x007d09c8
    /// - Uses "upgradeitems_p" for regular items (not "upgradeitems" like K1)
    /// - Uses "upcrystals" for lightsabers (same as K1)
    /// - Has 6 upgrade slots for lightsabers (K1 has 4)
    /// - Inventory checking: FUN_0055f2a0 @ 0x0055f2a0
    /// - Stack count check: param_1[0xb3] - item stack size
    /// - Upgrade storage offset: 0x3d54
    /// - Upgrade list offset: 0x3d3c
    /// </remarks>
    public class K2UpgradeScreen : OdysseyUpgradeScreenBase
    {
        /// <summary>
        /// Initializes a new instance of the K2 upgrade screen.
        /// </summary>
        /// <param name="installation">Game installation for accessing 2DA files.</param>
        /// <param name="world">World context for entity access.</param>
        public K2UpgradeScreen(Installation installation, IWorld world)
            : base(installation, world)
        {
        }

        /// <summary>
        /// Gets the upgrade table name for regular items (not lightsabers).
        /// </summary>
        /// <returns>Table name for regular item upgrades.</returns>
        /// <remarks>
        /// K2 uses "upgradeitems_p" (not "upgradeitems" like K1).
        /// Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 line 37 - loads "upgradeitems_p"
        /// </remarks>
        protected override string GetRegularUpgradeTableName()
        {
            return "upgradeitems_p";
        }

        /// <summary>
        /// Applies an upgrade to an item.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <param name="upgradeResRef">ResRef of upgrade item to apply.</param>
        /// <returns>True if upgrade was successful.</returns>
        /// <remarks>
        /// Apply Upgrade Logic (K2):
        /// - Based on swkotor2.exe: FUN_00729640 @ 0x00729640 (ApplyUpgrade implementation)
        /// - Called from: FUN_0072e260 @ 0x0072e260 line 215
        /// - Original implementation:
        ///   1. Checks if upgrade item is already in upgrade list (offset 0x3d3c)
        ///   2. If found in list, removes from list and uses that item
        ///   3. If stack count < 2, removes from inventory (FUN_0055f3a0 @ 0x0055f3a0)
        ///   4. If stack count >= 2, decrements stack (FUN_00569d60 @ 0x00569d60)
        ///   5. Adds upgrade to slot array (offset 0x3d54)
        ///   6. Applies upgrade properties to item (FUN_0055e160 @ 0x0055e160)
        /// - Stack count check: param_1[0xb3] - item stack size
        /// - Character from: DAT_008283d4
        /// </remarks>
        public override bool ApplyUpgrade(IEntity item, int upgradeSlot, string upgradeResRef)
        {
            if (item == null || string.IsNullOrEmpty(upgradeResRef))
            {
                return false;
            }

            if (upgradeSlot < 0)
            {
                return false;
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return false;
            }

            // Check if slot is already occupied
            // Based on swkotor2.exe: FUN_00729640 @ 0x00729640 line 12 - checks upgrade list at offset 0x3d3c
            var existingUpgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == upgradeSlot);
            if (existingUpgrade != null)
            {
                // Slot is occupied, cannot apply upgrade
                return false;
            }

            // Check if upgrade is compatible with item
            List<string> availableUpgrades = GetAvailableUpgrades(item, upgradeSlot);
            if (!availableUpgrades.Contains(upgradeResRef, StringComparer.OrdinalIgnoreCase))
            {
                // Upgrade is not compatible or not available
                return false;
            }

            // Get character inventory to find and remove upgrade item
            // Based on swkotor2.exe: FUN_00729640 @ 0x00729640 line 24 - gets character from DAT_008283d4
            IEntity character = _character;
            if (character == null)
            {
                // TODO: Get player character from world
                return false;
            }

            IInventoryComponent characterInventory = character.GetComponent<IInventoryComponent>();
            if (characterInventory == null)
            {
                return false;
            }

            // Find upgrade item in inventory
            // Based on swkotor2.exe: FUN_0055f2a0 @ 0x0055f2a0 - searches inventory by ResRef
            IEntity upgradeItem = null;
            foreach (IEntity inventoryItem in characterInventory.GetAllItems())
            {
                IItemComponent invItemComponent = inventoryItem.GetComponent<IItemComponent>();
                if (invItemComponent != null && !string.IsNullOrEmpty(invItemComponent.TemplateResRef))
                {
                    string resRef = invItemComponent.TemplateResRef.ToLowerInvariant();
                    if (resRef.EndsWith(".uti"))
                    {
                        resRef = resRef.Substring(0, resRef.Length - 4);
                    }
                    if (resRef.Equals(upgradeResRef, StringComparison.OrdinalIgnoreCase))
                    {
                        upgradeItem = inventoryItem;
                        break;
                    }
                }
            }

            if (upgradeItem == null)
            {
                // Upgrade item not found in inventory
                return false;
            }

            // Check stack count and remove from inventory
            // Based on swkotor2.exe: FUN_00729640 @ 0x00729640 line 18 - checks stack count at offset 0xb3
            // Get stack count from item component
            // If stack count < 2, remove from inventory (FUN_0055f3a0)
            // If stack count >= 2, decrement stack (FUN_00569d60)
            IItemComponent upgradeItemComponent = upgradeItem.GetComponent<IItemComponent>();
            if (upgradeItemComponent != null)
            {
                int stackSize = upgradeItemComponent.StackSize;
                if (stackSize < 2)
                {
                    // Stack count is 1 or less - remove item completely from inventory
                    // Based on swkotor2.exe: FUN_0055f3a0 @ 0x0055f3a0 - removes item from inventory
                    characterInventory.RemoveItem(upgradeItem);
                }
                else
                {
                    // Stack count is 2 or more - decrement stack count by 1
                    // Based on swkotor2.exe: FUN_00569d60 @ 0x00569d60 - decrements item stack
                    upgradeItemComponent.StackSize = stackSize - 1;
                }
            }
            else
            {
                // Item component not found - remove item as fallback
                characterInventory.RemoveItem(upgradeItem);
            }

            // Load upgrade item UTI template and apply properties
            // Based on swkotor2.exe: FUN_0055e160 @ 0x0055e160 - applies upgrade stats to item
            // Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 - loads UTI template
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
            if (upgradeUTI == null)
            {
                // Failed to load upgrade template - cannot apply upgrade
                return false;
            }

            // Apply upgrade to item
            // Based on swkotor2.exe: FUN_00729640 @ 0x00729640 line 57 - stores upgrade at offset 0x3d54
            ItemUpgrade upgrade = new ItemUpgrade
            {
                UpgradeType = upgradeSlot, // UpgradeType corresponds to slot index
                Index = upgradeSlot
            };

            itemComponent.AddUpgrade(upgrade);

            // Track upgrade ResRef for removal
            // Based on swkotor2.exe: Upgrade tracking system - stores ResRef for later removal
            string upgradeKey = item.ObjectId.ToString() + "_" + upgradeSlot.ToString();
            _upgradeResRefMap[upgradeKey] = upgradeResRef;

            // Apply upgrade properties to item
            // Based on swkotor2.exe: FUN_0055e160 @ 0x0055e160 - applies upgrade stats to item
            // Properties from upgrade UTI modify item stats (damage bonuses, AC bonuses, etc.)
            if (!ApplyUpgradeProperties(item, upgradeUTI))
            {
                // Failed to apply properties - remove upgrade and return failure
                itemComponent.RemoveUpgrade(upgrade);
                _upgradeResRefMap.Remove(upgradeKey);
                return false;
            }

            // Recalculate item stats and update display
            // Based on swkotor2.exe: Item stat recalculation after upgrade application
            // Recalculates item damage, AC, and other stats based on new properties
            RecalculateItemStats(item);

            return true;
        }

        /// <summary>
        /// Removes an upgrade from an item.
        /// </summary>
        /// <param name="item">Item to modify.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if upgrade was removed.</returns>
        /// <remarks>
        /// Remove Upgrade Logic (K2):
        /// - Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 lines 217-230 (removal logic)
        /// - Original implementation:
        ///   1. Gets upgrade item from slot array (offset 0x3d54)
        ///   2. Removes upgrade from slot array (sets to 0)
        ///   3. Returns upgrade item to inventory (FUN_00567ce0 @ 0x00567ce0)
        ///   4. Updates item stats (removes upgrade bonuses via FUN_0055e160)
        ///   5. Recalculates item stats
        /// - Removal: FUN_00431ec0 @ 0x00431ec0 - removes from array
        /// </remarks>
        public override bool RemoveUpgrade(IEntity item, int upgradeSlot)
        {
            if (item == null)
            {
                return false;
            }

            if (upgradeSlot < 0)
            {
                return false;
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return false;
            }

            // Find upgrade in slot
            // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 218 - gets upgrade from offset 0x3d54
            var upgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == upgradeSlot);
            if (upgrade == null)
            {
                // No upgrade in slot
                return false;
            }

            // Get upgrade item ResRef from tracked upgrade data
            // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 218 - gets item from slot array
            // We track upgrade ResRefs in _upgradeResRefMap for removal
            string upgradeKey = item.ObjectId.ToString() + "_" + upgradeSlot.ToString();
            string upgradeResRef = null;
            if (!_upgradeResRefMap.TryGetValue(upgradeKey, out upgradeResRef))
            {
                // Upgrade ResRef not found in tracking map - cannot remove properties
                // Still remove upgrade from item, but cannot restore properties
                itemComponent.RemoveUpgrade(upgrade);
                return true;
            }

            // Load upgrade UTI template to remove properties
            // Based on swkotor2.exe: FUN_0055e160 @ 0x0055e160 - removes upgrade stats from item
            // Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 - loads UTI template
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);

            // Remove upgrade from item
            // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 219 - removes from array using FUN_00431ec0
            itemComponent.RemoveUpgrade(upgrade);

            // Remove upgrade ResRef from tracking map
            _upgradeResRefMap.Remove(upgradeKey);

            // Remove upgrade properties from item (damage bonuses, AC bonuses, etc.)
            // Based on swkotor2.exe: FUN_0055e160 @ 0x0055e160 - removes upgrade stats from item
            // Properties from upgrade UTI are removed to restore original item stats
            if (upgradeUTI != null)
            {
                RemoveUpgradeProperties(item, upgradeUTI);
            }

            // Recalculate item stats and update display
            // Based on swkotor2.exe: Item stat recalculation after upgrade removal
            // Recalculates item damage, AC, and other stats after removing upgrade properties
            RecalculateItemStats(item);

            // Return upgrade item to inventory
            // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 221 - returns to inventory using FUN_00567ce0
            // Note: Full implementation would create upgrade item entity and add to inventory
            // This is handled by the calling code or inventory system

            return true;
        }
    }
}

