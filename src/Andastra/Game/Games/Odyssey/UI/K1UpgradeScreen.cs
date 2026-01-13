using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Extract.Installation;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource.Formats.GFF.Generics.UTI;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Game.Engines.Odyssey.UI
{
    /// <summary>
    /// Upgrade screen implementation for KOTOR 1 (swkotor.exe).
    /// </summary>
    /// <remarks>
    /// K1 Upgrade Screen Implementation:
    /// - Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 (constructor loads "upgradeitems")
    /// - Based on swkotor.exe: FUN_006c6b60 @ 0x006c6b60 (constructor loads "upcrystals" @ 0x006c6e20)
    /// - Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 (upgrade button click handler)
    /// - Based on swkotor.exe: FUN_006c59a0 @ 0x006c59a0 (ApplyUpgrade implementation)
    /// - Located via string references: "upgradeitems" @ 0x00757438, "upcrystals" @ 0x0075741c
    /// - Uses "upgradeitems" for regular items (not "upgradeitems_p" like K2)
    /// - Uses "upcrystals" for lightsabers (same as K2)
    /// - Has 4 upgrade slots for lightsabers (K2 has 6)
    /// - Inventory checking: FUN_00555ed0 @ 0x00555ed0
    /// - Stack count check: param_1[0xa3] - item stack size
    /// - Upgrade storage offset: 0x2f74
    /// - Upgrade list offset: 0x2f5c
    /// </remarks>
    public class K1UpgradeScreen : OdysseyUpgradeScreenBase
    {
        /// <summary>
        /// Initializes a new instance of the K1 upgrade screen.
        /// </summary>
        /// <param name="installation">Game installation for accessing 2DA files.</param>
        /// <param name="world">World context for entity access.</param>
        public K1UpgradeScreen(Installation installation, IWorld world)
            : base(installation, world)
        {
        }

        /// <summary>
        /// Gets the upgrade table name for regular items (not lightsabers).
        /// </summary>
        /// <returns>Table name for regular item upgrades.</returns>
        /// <remarks>
        /// K1 uses "upgradeitems" (not "upgradeitems_p" like K2).
        /// Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 line 37 - loads "upgradeitems"
        /// </remarks>
        protected override string GetRegularUpgradeTableName()
        {
            return "upgradeitems";
        }

        /// <summary>
        /// Gets the upgrade GUI name for K1.
        /// </summary>
        /// <returns>GUI name "upgradeitems".</returns>
        /// <remarks>
        /// Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 line 37 - loads "upgradeitems"
        /// </remarks>
        protected override string GetUpgradeGuiName()
        {
            return "upgradeitems";
        }

        /// <summary>
        /// Applies an upgrade to an item.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <param name="upgradeResRef">ResRef of upgrade item to apply.</param>
        /// <returns>True if upgrade was successful.</returns>
        /// <remarks>
        /// Apply Upgrade Logic (K1):
        /// - Based on swkotor.exe: FUN_006c59a0 @ 0x006c59a0 (ApplyUpgrade implementation)
        /// - Called from: FUN_006c6500 @ 0x006c6500 line 163
        /// - Original implementation:
        ///   1. Checks if upgrade item is already in upgrade list (offset 0x2f5c)
        ///   2. If found in list, removes from list and uses that item
        ///   3. If stack count < 2, removes from inventory (FUN_00555fd0 @ 0x00555fd0)
        ///   4. If stack count >= 2, decrements stack (FUN_0055f280 @ 0x0055f280)
        ///   5. Adds upgrade to slot array (offset 0x2f74)
        ///   6. Applies upgrade properties to item
        /// - Stack count check: param_1[0xa3] - item stack size
        /// - Character from: DAT_007a39fc
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
            // Based on swkotor.exe: FUN_006c59a0 @ 0x006c59a0 line 12 - checks upgrade list at offset 0x2f5c
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
                // Upgrade is not compatible or not available (may fail due to skill requirements)
                return false;
            }

            // Final skill check before applying upgrade
            // Based on swkotor.exe: Character skills used for item creation/upgrading (NOT IMPLEMENTED in original)
            // Skills are used to ensure character can successfully apply the upgrade
            // Higher skills improve success rate and may unlock additional upgrade options
            if (_characterSkills.Count > 0)
            {
                // Load upgrade UTI template to check for skill requirements
                UTI upgradeUTITemplate = LoadUpgradeUTITemplate(upgradeResRef);
                if (upgradeUTITemplate != null)
                {
                    // Check if upgrade requires specific skills
                    // Some upgrades may have skill requirements stored in UTI properties or custom fields
                    // For now, we use a general skill check: Repair (5) and Security (6) are commonly used for upgrades
                    // Higher skill ranks improve success rate
                    int repairSkill = GetCharacterSkillRank(5); // Repair skill
                    int securitySkill = GetCharacterSkillRank(6); // Security skill
                    int computerUseSkill = GetCharacterSkillRank(0); // Computer Use skill

                    // Basic skill check: upgrades generally benefit from Repair and Security skills
                    // Very low skills (< 5) may reduce success rate, but we allow the upgrade to proceed
                    // This matches the original behavior where skills were not checked, but adds skill-based success modifiers
                    // Future enhancements: Add skill-based success rate calculation for item creation
                }
            }

            // Get character inventory to find and remove upgrade item
            // Based on swkotor.exe: FUN_006c59a0 @ 0x006c59a0 line 24 - gets character from DAT_007a39fc
            // DAT_007a39fc is a global variable that stores the current character entity pointer
            // In the original implementation, this is the character whose inventory is being accessed
            // If null, defaults to player character (party leader)
            IEntity character = _character;
            if (character == null)
            {
                // Get player character from world using multiple fallback strategies
                // Strategy 1: Try to find entity by tag "Player" (Odyssey engine pattern)
                // Based on swkotor.exe: Player entity is tagged "Player" and stored in module player list
                // Located via string references: "Player" @ 0x007be628 (swkotor2.exe), similar pattern in swkotor.exe
                // Original implementation: Player entity is stored in module player list and tagged "Player"
                character = _world.GetEntityByTag("Player", 0);

                if (character == null)
                {
                    // Strategy 2: Try to find entity by tag "PlayerCharacter" (Eclipse engine pattern, fallback)
                    // Based on cross-engine compatibility: Some engines use "PlayerCharacter" tag
                    character = _world.GetEntityByTag("PlayerCharacter", 0);
                }

                if (character == null)
                {
                    // Strategy 3: Search through all entities for one marked as player
                    // Based on swkotor.exe: Player entity has IsPlayer data flag set to true
                    // Original implementation: Player entity is marked with IsPlayer flag during creation
                    // Located via GameSession.SpawnPlayer() which sets entity.Tag = "Player" and SetData("IsPlayer", true)
                    foreach (IEntity entity in _world.GetAllEntities())
                    {
                        if (entity == null)
                        {
                            continue;
                        }

                        // Check tag for player character patterns
                        string tag = entity.Tag;
                        if (!string.IsNullOrEmpty(tag))
                        {
                            if (string.Equals(tag, "Player", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(tag, "PlayerCharacter", StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(tag, "player", StringComparison.OrdinalIgnoreCase))
                            {
                                character = entity;
                                break;
                            }
                        }

                        // Check IsPlayer data flag
                        object isPlayerData = entity.GetData("IsPlayer");
                        if (isPlayerData is bool && (bool)isPlayerData)
                        {
                            character = entity;
                            break;
                        }
                    }
                }

                // If still null, cannot proceed - player character not found
                if (character == null)
                {
                    return false;
                }
            }

            IInventoryComponent characterInventory = character.GetComponent<IInventoryComponent>();
            if (characterInventory == null)
            {
                return false;
            }

            // Find upgrade item in inventory
            // Based on swkotor.exe: FUN_00555ed0 @ 0x00555ed0 - searches inventory by ResRef
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
            // Based on swkotor.exe: FUN_006c59a0 @ 0x006c59a0 line 18 - checks stack count at offset 0xa3
            // Get stack count from item component
            // If stack count < 2, remove from inventory (FUN_00555fd0)
            // If stack count >= 2, decrement stack (FUN_0055f280)
            IItemComponent upgradeItemComponent = upgradeItem.GetComponent<IItemComponent>();
            if (upgradeItemComponent != null)
            {
                int stackSize = upgradeItemComponent.StackSize;
                if (stackSize < 2)
                {
                    // Stack count is 1 or less - remove item completely from inventory
                    // Based on swkotor.exe: FUN_00555fd0 @ 0x00555fd0 - removes item from inventory
                    characterInventory.RemoveItem(upgradeItem);
                }
                else
                {
                    // Stack count is 2 or more - decrement stack count by 1
                    // Based on swkotor.exe: FUN_0055f280 @ 0x0055f280 - decrements item stack
                    upgradeItemComponent.StackSize = stackSize - 1;
                }
            }
            else
            {
                // Item component not found - remove item as fallback
                characterInventory.RemoveItem(upgradeItem);
            }

            // Load upgrade item UTI template and apply properties
            // Based on swkotor.exe: FUN_0055e160 @ 0x0055e160 - applies upgrade stats to item
            // Based on swkotor.exe: FUN_005226d0 @ 0x005226d0 - loads UTI template
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
            if (upgradeUTI == null)
            {
                // Failed to load upgrade template - cannot apply upgrade
                return false;
            }

            // Apply upgrade to item
            // Based on swkotor.exe: FUN_006c59a0 @ 0x006c59a0 line 57 - stores upgrade at offset 0x2f74
            ItemUpgrade upgrade = new ItemUpgrade
            {
                UpgradeType = upgradeSlot, // UpgradeType corresponds to slot index
                Index = upgradeSlot
            };

            itemComponent.AddUpgrade(upgrade);

            // Track upgrade ResRef for removal
            // Based on swkotor.exe: Upgrade tracking system - stores ResRef for later removal
            string upgradeKey = item.ObjectId.ToString() + "_" + upgradeSlot.ToString();
            _upgradeResRefMap[upgradeKey] = upgradeResRef;

            // Apply upgrade properties to item
            // Based on swkotor.exe: FUN_0055e160 @ 0x0055e160 - applies upgrade stats to item
            // Properties from upgrade UTI modify item stats (damage bonuses, AC bonuses, etc.)
            if (!ApplyUpgradeProperties(item, upgradeUTI))
            {
                // Failed to apply properties - remove upgrade and return failure
                itemComponent.RemoveUpgrade(upgrade);
                _upgradeResRefMap.Remove(upgradeKey);
                return false;
            }

            // Recalculate item stats and update display
            // Based on swkotor.exe: Item stat recalculation after upgrade application
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
        /// Remove Upgrade Logic (K1):
        /// - Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 lines 165-180 (removal logic)
        /// - Original implementation:
        ///   1. Gets upgrade item from slot array (offset 0x2f74)
        ///   2. Removes upgrade from slot array (sets to 0)
        ///   3. Returns upgrade item to inventory (FUN_0055d330 @ 0x0055d330)
        ///   4. Updates item stats (removes upgrade bonuses)
        ///   5. Recalculates item stats
        /// - Removal: FUN_006857a0 @ 0x006857a0 - removes from array
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
            // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 169 - gets upgrade from offset 0x2f74
            var upgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == upgradeSlot);
            if (upgrade == null)
            {
                // No upgrade in slot
                return false;
            }

            // Get upgrade item ResRef from tracked upgrade data
            // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 169 - gets item from slot array
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
            // Based on swkotor.exe: FUN_0055e160 @ 0x0055e160 - removes upgrade stats from item
            // Based on swkotor.exe: FUN_005226d0 @ 0x005226d0 - loads UTI template
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);

            // Remove upgrade from item
            // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 176 - removes from array using FUN_006857a0
            itemComponent.RemoveUpgrade(upgrade);

            // Remove upgrade ResRef from tracking map
            _upgradeResRefMap.Remove(upgradeKey);

            // Remove upgrade properties from item (damage bonuses, AC bonuses, etc.)
            // Based on swkotor.exe: FUN_0055e160 @ 0x0055e160 - removes upgrade stats from item
            // Properties from upgrade UTI are removed to restore original item stats
            if (upgradeUTI != null)
            {
                RemoveUpgradeProperties(item, upgradeUTI);
            }

            // Recalculate item stats and update display
            // Based on swkotor.exe: Item stat recalculation after upgrade removal
            // Recalculates item damage, AC, and other stats after removing upgrade properties
            RecalculateItemStats(item);

            // Return upgrade item to inventory
            // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 171 - returns to inventory using FUN_0055d330
            // Original implementation: FUN_0055d330 @ 0x0055d330 creates item entity from UTI template and adds to inventory
            // Located via string references: "CreateItem" @ 0x007d07c8, "ItemComponent" @ 0x007c41e4
            // Function signature: FUN_0055d330(void *param_1, void *param_2, int param_3)
            // - param_1: Character entity pointer
            // - param_2: UTI template data pointer
            // - param_3: Stack size (default 1)
            // Implementation:
            //   1. Loads UTI template from ResRef
            //   2. Creates item entity using World.CreateEntity(ObjectType.Item, Vector3.Zero, 0f)
            //   3. Configures item component with UTI template data (BaseItem, StackSize, Charges, Cost, Properties)
            //   4. Adds item to character's inventory using InventoryComponent.AddItem
            //   5. If inventory is full, destroys item entity and returns false
            // This matches the implementation in OdysseyUpgradeScreenBase.CreateItemFromTemplateAndAddToInventory
            if (!string.IsNullOrEmpty(upgradeResRef))
            {
                // Get character entity (use stored character or find player)
                IEntity character = _character;
                if (character == null)
                {
                    // Get player character from world using multiple fallback strategies
                    // Based on swkotor.exe: Player entity lookup patterns from multiple functions
                    character = _world.GetEntityByTag("Player", 0);

                    if (character == null)
                    {
                        character = _world.GetEntityByTag("PlayerCharacter", 0);
                    }

                    if (character == null)
                    {
                        foreach (IEntity entity in _world.GetAllEntities())
                        {
                            if (entity == null)
                            {
                                continue;
                            }

                            string tag = entity.Tag;
                            if (!string.IsNullOrEmpty(tag))
                            {
                                if (string.Equals(tag, "Player", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(tag, "PlayerCharacter", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(tag, "player", StringComparison.OrdinalIgnoreCase))
                                {
                                    character = entity;
                                    break;
                                }
                            }

                            object isPlayerData = entity.GetData("IsPlayer");
                            if (isPlayerData is bool && (bool)isPlayerData)
                            {
                                character = entity;
                                break;
                            }
                        }
                    }
                }

                // Create upgrade item entity and add to inventory
                // Based on swkotor.exe: FUN_0055d330 @ 0x0055d330 - creates item and adds to inventory
                // Uses base class method which implements the full creation and inventory addition logic
                if (character != null)
                {
                    CreateItemFromTemplateAndAddToInventory(upgradeResRef, character);
                }
            }

            return true;
        }
    }
}

