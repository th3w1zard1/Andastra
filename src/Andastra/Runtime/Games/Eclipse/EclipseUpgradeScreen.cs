using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource.Generics;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Upgrade screen implementation for Eclipse engine (Dragon Age, Mass Effect).
    /// </summary>
    /// <remarks>
    /// Eclipse Upgrade Screen Implementation:
    /// - Eclipse engine (daorigins.exe, DragonAge2.exe) has an ItemUpgrade system
    /// - Based on reverse engineering: ItemUpgrade, GUIItemUpgrade, COMMAND_OPENITEMUPGRADEGUI strings found
    /// - Dragon Age Origins: ItemUpgrade system with GUIItemUpgrade class
    ///   - Based on daorigins.exe: ItemUpgrade @ 0x00aef22c, GUIItemUpgrade @ 0x00b02ca0, COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c
    /// - Dragon Age 2: Enhanced ItemUpgrade system with UpgradePrereqType, GetAbilityUpgradedValue
    ///   - Based on DragonAge2.exe: ItemUpgrade @ 0x00beb1f0, GUIItemUpgrade @ 0x00beb1d0, UpgradePrereqType @ 0x00c0583c, GetAbilityUpgradedValue @ 0x00c0f20c
    /// - Mass Effect: No item upgrade system (only vehicle upgrades)
    /// - Mass Effect 2: No item upgrade system (only vehicle upgrades)
    ///
    /// Eclipse upgrade system differs from Odyssey:
    /// - Eclipse uses ItemUpgrade class structure rather than 2DA-based upgrade tables
    /// - Dragon Age 2 has prerequisite checking (UpgradePrereqType) and ability-based upgrades (GetAbilityUpgradedValue)
    /// - Upgrade compatibility is determined by item properties and upgrade prerequisites rather than 2DA table lookups
    /// </remarks>
    public class EclipseUpgradeScreen : BaseUpgradeScreen
    {
        /// <summary>
        /// Initializes a new instance of the Eclipse upgrade screen.
        /// </summary>
        /// <param name="installation">Game installation for accessing game data.</param>
        /// <param name="world">World context for entity access.</param>
        public EclipseUpgradeScreen(Installation installation, IWorld world)
            : base(installation, world)
        {
        }

        /// <summary>
        /// Shows the upgrade screen.
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c opens ItemUpgrade GUI
        /// - DragonAge2.exe: GUIItemUpgrade class structure handles upgrade screen display
        /// - Mass Effect games: No upgrade screen support (method handles gracefully)
        /// </remarks>
        public override void Show()
        {
            // Check if this is a Mass Effect game (no upgrade support)
            if (IsMassEffectGame())
            {
                // Mass Effect games do not have item upgrade screens
                _isVisible = false;
                return;
            }

            _isVisible = true;
            // TODO: STUB - UI rendering system not yet implemented
            // In full implementation, this would:
            // 1. Load ItemUpgrade GUI (GUIItemUpgrade class)
            //   - Based on daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c
            //   - Based on DragonAge2.exe: GUIItemUpgrade class structure
            // 2. Display item upgrade interface with upgrade slots
            // 3. Show available upgrades based on UpgradePrereqType (Dragon Age 2)
            // 4. Display item properties and upgrade effects
            // 5. Handle user input for applying/removing upgrades
            // 6. Show ability-based upgrade values (Dragon Age 2: GetAbilityUpgradedValue @ 0x00c0f20c)
        }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: GUIItemUpgrade class handles screen hiding
        /// - DragonAge2.exe: GUIItemUpgrade class structure handles screen state management
        /// </remarks>
        public override void Hide()
        {
            _isVisible = false;
            // TODO: STUB - UI rendering system not yet implemented
            // In full implementation, this would:
            // 1. Hide ItemUpgrade GUI
            //   - Based on daorigins.exe: GUIItemUpgrade class hides screen
            //   - Based on DragonAge2.exe: GUIItemUpgrade class structure
            // 2. Save any pending changes to item upgrades
            // 3. Return control to game
            // 4. Clear upgrade screen state
        }

        /// <summary>
        /// Gets available upgrade items for a given item and upgrade slot.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>List of available upgrade items (ResRefs).</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - Dragon Age Origins: ItemUpgrade system checks item compatibility and inventory
        /// - Dragon Age 2: UpgradePrereqType @ 0x00c0583c suggests prerequisite checking for upgrade compatibility
        /// - Dragon Age 2: GetAbilityUpgradedValue @ 0x00c0f20c suggests ability-based upgrade filtering
        /// - Eclipse upgrade system uses item properties and upgrade prerequisites rather than 2DA table lookups
        /// </remarks>
        public override List<string> GetAvailableUpgrades(IEntity item, int upgradeSlot)
        {
            List<string> availableUpgrades = new List<string>();

            // Check if this is a Mass Effect game (no upgrade support)
            if (IsMassEffectGame())
            {
                return availableUpgrades;
            }

            if (item == null)
            {
                return availableUpgrades;
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return availableUpgrades;
            }

            if (upgradeSlot < 0)
            {
                return availableUpgrades;
            }

            // Get character inventory ResRefs using base class helper
            // Based on Dragon Age Origins: ItemUpgrade system checks inventory for compatible upgrades
            // Based on Dragon Age 2: UpgradePrereqType checks prerequisites before allowing upgrade
            HashSet<string> inventoryResRefs = GetCharacterInventoryResRefs(_character);

            // Eclipse upgrade system: Check each inventory item to see if it's a compatible upgrade
            // Unlike Odyssey's 2DA-based system, Eclipse uses item properties and prerequisites
            IEntity character = _character;
            if (character == null)
            {
                // Get player character from world
                character = _world.GetEntityByTag("Player", 0);
                if (character == null)
                {
                    character = _world.GetEntityByTag("PlayerCharacter", 0);
                }
            }

            if (character == null)
            {
                return availableUpgrades;
            }

            IInventoryComponent characterInventory = character.GetComponent<IInventoryComponent>();
            if (characterInventory == null)
            {
                return availableUpgrades;
            }

            // Iterate through inventory items to find compatible upgrades
            // Based on Dragon Age Origins: ItemUpgrade system checks item compatibility
            // Based on Dragon Age 2: UpgradePrereqType @ 0x00c0583c checks prerequisites
            foreach (IEntity inventoryItem in characterInventory.GetAllItems())
            {
                IItemComponent invItemComponent = inventoryItem.GetComponent<IItemComponent>();
                if (invItemComponent == null)
                {
                    continue;
                }

                // Check if this inventory item is a compatible upgrade for the target item
                // Eclipse upgrade system: Compatibility is determined by:
                // 1. Item type compatibility (weapon upgrades for weapons, armor upgrades for armor, etc.)
                // 2. Upgrade slot compatibility (upgrade must match the slot type)
                // 3. Prerequisites (Dragon Age 2: UpgradePrereqType checks)
                if (IsCompatibleUpgrade(itemComponent, invItemComponent, upgradeSlot))
                {
                    string resRef = invItemComponent.TemplateResRef;
                    if (!string.IsNullOrEmpty(resRef))
                    {
                        // Normalize ResRef (remove extension, lowercase)
                        resRef = resRef.ToLowerInvariant();
                        if (resRef.EndsWith(".uti"))
                        {
                            resRef = resRef.Substring(0, resRef.Length - 4);
                        }
                        if (!string.IsNullOrEmpty(resRef) && !availableUpgrades.Contains(resRef, StringComparer.OrdinalIgnoreCase))
                        {
                            availableUpgrades.Add(resRef);
                        }
                    }
                }
            }

            return availableUpgrades;
        }

        /// <summary>
        /// Applies an upgrade to an item.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <param name="upgradeResRef">ResRef of upgrade item to apply.</param>
        /// <returns>True if upgrade was successful.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: ItemUpgrade system applies upgrade properties to item
        /// - DragonAge2.exe: Enhanced upgrade system with prerequisites checks before applying
        /// - Eclipse upgrade system: Applies upgrade properties and updates item stats
        /// </remarks>
        public override bool ApplyUpgrade(IEntity item, int upgradeSlot, string upgradeResRef)
        {
            // Check if this is a Mass Effect game (no upgrade support)
            if (IsMassEffectGame())
            {
                return false;
            }

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
            var existingUpgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == upgradeSlot);
            if (existingUpgrade != null)
            {
                // Slot is occupied, cannot apply upgrade
                return false;
            }

            // Check if upgrade is compatible and available
            List<string> availableUpgrades = GetAvailableUpgrades(item, upgradeSlot);
            if (!availableUpgrades.Contains(upgradeResRef, StringComparer.OrdinalIgnoreCase))
            {
                // Upgrade is not compatible or not available
                return false;
            }

            // Get character inventory to find and remove upgrade item
            IEntity character = _character;
            if (character == null)
            {
                character = _world.GetEntityByTag("Player", 0);
                if (character == null)
                {
                    character = _world.GetEntityByTag("PlayerCharacter", 0);
                }
            }

            if (character == null)
            {
                return false;
            }

            IInventoryComponent characterInventory = character.GetComponent<IInventoryComponent>();
            if (characterInventory == null)
            {
                return false;
            }

            // Find upgrade item in inventory
            // Based on Dragon Age Origins: ItemUpgrade system finds upgrade item in inventory
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

            // Load upgrade UTI template and apply properties
            // Based on Dragon Age Origins: ItemUpgrade system loads upgrade template and applies properties
            // Based on Dragon Age 2: Enhanced upgrade system applies properties with prerequisite checks
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
            if (upgradeUTI == null)
            {
                return false;
            }

            // Apply upgrade properties to item
            // Based on Dragon Age Origins: ItemUpgrade system applies upgrade properties
            bool propertiesApplied = ApplyUpgradeProperties(item, upgradeUTI);
            if (!propertiesApplied)
            {
                return false;
            }

            // Add upgrade to item's upgrade list
            // Based on Dragon Age Origins: ItemUpgrade system tracks upgrades in item
            IItemComponent invUpgradeComponent = upgradeItem.GetComponent<IItemComponent>();
            if (invUpgradeComponent != null)
            {
                var upgrade = new ItemUpgrade
                {
                    Index = upgradeSlot,
                    TemplateResRef = upgradeResRef
                };
                itemComponent.AddUpgrade(upgrade);

                // Track upgrade ResRef for removal
                string upgradeKey = item.ObjectId.ToString() + "_" + upgradeSlot.ToString();
                _upgradeResRefMap[upgradeKey] = upgradeResRef;
            }

            // Remove upgrade item from inventory (consume it)
            // Based on Dragon Age Origins: ItemUpgrade system consumes upgrade item
            characterInventory.RemoveItem(upgradeItem);

            // Recalculate item stats after applying upgrade
            // Based on Dragon Age Origins: ItemUpgrade system recalculates item stats
            // Based on Dragon Age 2: GetAbilityUpgradedValue @ 0x00c0f20c recalculates ability-based stats
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
        /// Based on reverse engineering:
        /// - daorigins.exe: ItemUpgrade system removes upgrade properties from item
        /// - DragonAge2.exe: Enhanced upgrade system removes upgrade and recalculates stats
        /// </remarks>
        public override bool RemoveUpgrade(IEntity item, int upgradeSlot)
        {
            // Check if this is a Mass Effect game (no upgrade support)
            if (IsMassEffectGame())
            {
                return false;
            }

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

            // Find upgrade in item's upgrade list
            var existingUpgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == upgradeSlot);
            if (existingUpgrade == null)
            {
                // No upgrade in this slot
                return false;
            }

            // Get upgrade ResRef for removal
            string upgradeKey = item.ObjectId.ToString() + "_" + upgradeSlot.ToString();
            string upgradeResRef = existingUpgrade.TemplateResRef;
            if (string.IsNullOrEmpty(upgradeResRef) && _upgradeResRefMap.ContainsKey(upgradeKey))
            {
                upgradeResRef = _upgradeResRefMap[upgradeKey];
            }

            // Load upgrade UTI template to remove properties
            if (!string.IsNullOrEmpty(upgradeResRef))
            {
                UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
                if (upgradeUTI != null)
                {
                    // Remove upgrade properties from item
                    // Based on Dragon Age Origins: ItemUpgrade system removes upgrade properties
                    RemoveUpgradeProperties(item, upgradeUTI);
                }
            }

            // Remove upgrade from item's upgrade list
            // Based on Dragon Age Origins: ItemUpgrade system removes upgrade from item
            itemComponent.RemoveUpgrade(existingUpgrade);

            // Remove from tracking map
            if (_upgradeResRefMap.ContainsKey(upgradeKey))
            {
                _upgradeResRefMap.Remove(upgradeKey);
            }

            // Recalculate item stats after removing upgrade
            // Based on Dragon Age Origins: ItemUpgrade system recalculates item stats
            RecalculateItemStats(item);

            return true;
        }

        /// <summary>
        /// Gets the upgrade table name for regular items (not lightsabers).
        /// </summary>
        /// <returns>Table name for regular item upgrades.</returns>
        /// <remarks>
        /// Eclipse engine does not use 2DA-based upgrade tables like Odyssey.
        /// Eclipse uses ItemUpgrade class structure with item properties and prerequisites.
        /// This method returns empty string as Eclipse does not use 2DA upgrade tables.
        /// </remarks>
        protected override string GetRegularUpgradeTableName()
        {
            // Eclipse engine uses a different upgrade system structure
            // Eclipse uses ItemUpgrade class with item properties rather than 2DA tables
            // Dragon Age games use item properties and prerequisites for upgrade compatibility
            // Mass Effect games do not have item upgrade systems
            return string.Empty;
        }

        /// <summary>
        /// Checks if this is a Mass Effect game (no item upgrade support).
        /// </summary>
        /// <returns>True if Mass Effect game, false otherwise.</returns>
        private bool IsMassEffectGame()
        {
            if (_installation == null)
            {
                return false;
            }

            Game game = _installation.Game;
            return game.IsMassEffect();
        }

        /// <summary>
        /// Checks if an inventory item is a compatible upgrade for a target item.
        /// </summary>
        /// <param name="targetItem">Target item to upgrade.</param>
        /// <param name="upgradeItem">Potential upgrade item from inventory.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if upgrade is compatible, false otherwise.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - Dragon Age Origins: ItemUpgrade system checks item type compatibility
        /// - Dragon Age 2: UpgradePrereqType @ 0x00c0583c checks prerequisites before allowing upgrade
        /// - Eclipse upgrade compatibility is determined by:
        ///   1. Item type compatibility (weapon upgrades for weapons, armor upgrades for armor, etc.)
        ///   2. Upgrade slot compatibility (upgrade must match the slot type)
        ///   3. Prerequisites (Dragon Age 2: UpgradePrereqType checks)
        /// </remarks>
        private bool IsCompatibleUpgrade(IItemComponent targetItem, IItemComponent upgradeItem, int upgradeSlot)
        {
            if (targetItem == null || upgradeItem == null)
            {
                return false;
            }

            // Check if upgrade item has upgrade-related properties
            // Eclipse upgrade items typically have specific property types that indicate they are upgrades
            // This is a simplified check - full implementation would check item properties more thoroughly
            // Based on Dragon Age Origins: ItemUpgrade system checks item properties for upgrade compatibility
            // Based on Dragon Age 2: UpgradePrereqType @ 0x00c0583c checks prerequisites

            // For now, we'll use a basic compatibility check:
            // - Upgrade items should have properties that can be applied to the target item
            // - Item types should be compatible (weapon upgrades for weapons, armor upgrades for armor)
            // - Full implementation would check UpgradePrereqType (Dragon Age 2) and other prerequisites

            // Basic compatibility: Check if upgrade item has properties that can be applied
            if (upgradeItem.Properties == null || upgradeItem.Properties.Count == 0)
            {
                return false;
            }

            // Check item type compatibility
            // Weapon upgrades should only apply to weapons, armor upgrades to armor, etc.
            // This is a simplified check - full implementation would check base item types more thoroughly
            int targetBaseItem = targetItem.BaseItem;
            int upgradeBaseItem = upgradeItem.BaseItem;

            // For now, allow any upgrade item with properties to be considered compatible
            // Full implementation would check:
            // - UpgradePrereqType (Dragon Age 2) for prerequisite checking
            // - Item type compatibility (weapon/armor/etc.)
            // - Upgrade slot type compatibility
            // - Ability requirements (Dragon Age 2: GetAbilityUpgradedValue @ 0x00c0f20c)

            return true;
        }
    }
}

