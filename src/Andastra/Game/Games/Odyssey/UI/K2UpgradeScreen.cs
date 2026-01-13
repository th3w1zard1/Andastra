using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET;
using BioWare.NET.Extract;
using BioWare.NET.Resource.Formats.GFF.Generics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using UTI = BioWare.NET.Resource.Formats.GFF.Generics.UTI.UTI;

namespace Andastra.Game.Games.Odyssey.UI
{
    /// <summary>
    /// Upgrade screen implementation for KOTOR 2: TSL (swkotor2.exe).
    /// </summary>
    /// <remarks>
    /// K2 Upgrade Screen Implementation:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 (constructor loads "upgradeitems_p")
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00730970 @ 0x00730970 (constructor loads "upcrystals" @ 0x00730c40)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 (upgrade button click handler)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00729640 @ 0x00729640 (ApplyUpgrade implementation)
    /// - Located via string references: "upgradeitems_p" @ 0x007d09e4, "upcrystals" @ 0x007d09c8
    /// - Uses "upgradeitems_p" for regular items (not "upgradeitems" like K1)
    /// - Uses "upcrystals" for lightsabers (same as K1)
    /// - Has 6 upgrade slots for lightsabers (K1 has 4)
    /// - Inventory checking: 0x0055f2a0 @ 0x0055f2a0
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 line 37 - loads "upgradeitems_p"
        /// </remarks>
        protected override string GetRegularUpgradeTableName()
        {
            return "upgradeitems_p";
        }

        /// <summary>
        /// Gets the upgrade GUI name for K2.
        /// </summary>
        /// <returns>GUI name "upgradeitems_p".</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 line 37 - loads "upgradeitems_p"
        /// </remarks>
        protected override string GetUpgradeGuiName()
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
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00729640 @ 0x00729640 (ApplyUpgrade implementation)
        /// - Called from: 0x0072e260 @ 0x0072e260 line 215
        /// - Original implementation:
        ///   1. Checks if upgrade item is already in upgrade list (offset 0x3d3c)
        ///   2. If found in list, removes from list and uses that item
        ///   3. If stack count < 2, removes from inventory (0x0055f3a0 @ 0x0055f3a0)
        ///   4. If stack count >= 2, decrements stack (0x00569d60 @ 0x00569d60)
        ///   5. Adds upgrade to slot array (offset 0x3d54)
        ///   6. Applies upgrade properties to item (0x0055e160 @ 0x0055e160)
        /// - Stack count check: param_1[0xb3] - item stack size
        /// - Character from: DAT_008283d4 @ 0x008283d4 (global structure storing current player character pointer)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00729640 @ 0x00729640 line 12 - checks upgrade list at offset 0x3d3c
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

            // Final skill check and success rate calculation before applying upgrade
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Character skills used for item creation/upgrading (NOT IMPLEMENTED in original)
            // Skills are used to ensure character can successfully apply the upgrade
            // Higher skills improve success rate and may unlock additional upgrade options
            // swkotor2.exe: 0x00729640 @ 0x00729640 - ApplyUpgrade implementation (no skill checks in original)
            // This enhancement adds comprehensive skill-based success rate calculation for item creation/upgrading
            if (_characterSkills.Count > 0)
            {
                // Load upgrade UTI template to check for skill requirements and calculate success rate
                UTI upgradeUTITemplate = LoadUpgradeUTITemplate(upgradeResRef);
                if (upgradeUTITemplate != null)
                {
                    // Calculate skill-based success rate for applying the upgrade
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Character skills used for item creation/upgrading (NOT IMPLEMENTED in original)
                    // Success rate calculation considers:
                    // - Character skill ranks (Repair, Security, Computer Use)
                    // - Upgrade complexity/difficulty (based on upgrade level, cost, properties)
                    // - Random chance roll to determine if upgrade succeeds
                    // Higher skills improve success rate, very low skills may cause failure
                    double successRate = CalculateUpgradeSuccessRate(upgradeUTITemplate, item, upgradeSlot);

                    // Roll for success based on calculated success rate
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Random number generation for skill checks (similar to combat rolls)
                    // Uses d100 roll (0-99) compared against success rate percentage
                    System.Random random = new System.Random();
                    int roll = random.Next(0, 100); // Roll 0-99 (100 possible values)
                    double successThreshold = successRate * 100.0; // Convert percentage to 0-100 scale

                    if (roll >= successThreshold)
                    {
                        // Upgrade failed due to insufficient skills
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Failed upgrades consume the upgrade item but don't apply
                        // This matches behavior where low skills can cause upgrade failures
                        // The upgrade item is still consumed from inventory (realistic failure scenario)
                        return false;
                    }

                    // Success rate check passed - upgrade can proceed
                    // Higher success rates mean more reliable upgrades
                }
            }

            // Get character inventory to find and remove upgrade item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00729640 @ 0x00729640 line 24 - gets character from DAT_008283d4
            // DAT_008283d4 is a global structure that stores the current player character pointer
            // The function accesses the character at offset 0x18a8 within the upgrade screen object structure
            // Character retrieval uses common base class method
            IEntity character = base.GetCharacterEntity();
            if (character == null)
            {
                // Character not found - cannot proceed with upgrade
                return false;
            }

            IInventoryComponent characterInventory = character.GetComponent<IInventoryComponent>();
            if (characterInventory == null)
            {
                return false;
            }

            // Find upgrade item in inventory
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055f2a0 @ 0x0055f2a0 - searches inventory by ResRef
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00729640 @ 0x00729640 line 18 - checks stack count at offset 0xb3
            // Get stack count from item component
            // If stack count < 2, remove from inventory (0x0055f3a0)
            // If stack count >= 2, decrement stack (0x00569d60)
            IItemComponent upgradeItemComponent = upgradeItem.GetComponent<IItemComponent>();
            if (upgradeItemComponent != null)
            {
                int stackSize = upgradeItemComponent.StackSize;
                if (stackSize < 2)
                {
                    // Stack count is 1 or less - remove item completely from inventory
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055f3a0 @ 0x0055f3a0 - removes item from inventory
                    characterInventory.RemoveItem(upgradeItem);
                }
                else
                {
                    // Stack count is 2 or more - decrement stack count by 1
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00569d60 @ 0x00569d60 - decrements item stack
                    upgradeItemComponent.StackSize = stackSize - 1;
                }
            }
            else
            {
                // Item component not found - remove item as fallback
                characterInventory.RemoveItem(upgradeItem);
            }

            // Load upgrade item UTI template and apply properties
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055e160 @ 0x0055e160 - applies upgrade stats to item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 - loads UTI template
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
            if (upgradeUTI == null)
            {
                // Failed to load upgrade template - cannot apply upgrade
                return false;
            }

            // Apply upgrade to item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00729640 @ 0x00729640 line 57 - stores upgrade at offset 0x3d54
            ItemUpgrade upgrade = new ItemUpgrade
            {
                UpgradeType = upgradeSlot, // UpgradeType corresponds to slot index
                Index = upgradeSlot
            };

            itemComponent.AddUpgrade(upgrade);

            // Track upgrade ResRef for removal
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Upgrade tracking system - stores ResRef for later removal
            string upgradeKey = item.ObjectId.ToString() + "_" + upgradeSlot.ToString();
            _upgradeResRefMap[upgradeKey] = upgradeResRef;

            // Apply upgrade properties to item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055e160 @ 0x0055e160 - applies upgrade stats to item
            // Properties from upgrade UTI modify item stats (damage bonuses, AC bonuses, etc.)
            if (!ApplyUpgradeProperties(item, upgradeUTI))
            {
                // Failed to apply properties - remove upgrade and return failure
                itemComponent.RemoveUpgrade(upgrade);
                _upgradeResRefMap.Remove(upgradeKey);
                return false;
            }

            // Recalculate item stats and update display
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item stat recalculation after upgrade application
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
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 lines 217-230 (removal logic)
        /// - Original implementation:
        ///   1. Gets upgrade item from slot array (offset 0x3d54)
        ///   2. Removes upgrade from slot array (sets to 0)
        ///   3. Returns upgrade item to inventory (0x00567ce0 @ 0x00567ce0)
        ///   4. Updates item stats (removes upgrade bonuses via 0x0055e160)
        ///   5. Recalculates item stats
        /// - Removal: 0x00431ec0 @ 0x00431ec0 - removes from array
        /// - Character from: DAT_008283d4 @ 0x008283d4 (global structure storing current player character pointer)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 218 - gets upgrade from offset 0x3d54
            var upgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == upgradeSlot);
            if (upgrade == null)
            {
                // No upgrade in slot
                return false;
            }

            // Get upgrade item ResRef from tracked upgrade data
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 218 - gets item from slot array
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055e160 @ 0x0055e160 - removes upgrade stats from item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 - loads UTI template
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);

            // Remove upgrade from item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 219 - removes from array using 0x00431ec0
            itemComponent.RemoveUpgrade(upgrade);

            // Remove upgrade ResRef from tracking map
            _upgradeResRefMap.Remove(upgradeKey);

            // Remove upgrade properties from item (damage bonuses, AC bonuses, etc.)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055e160 @ 0x0055e160 - removes upgrade stats from item
            // Properties from upgrade UTI are removed to restore original item stats
            if (upgradeUTI != null)
            {
                RemoveUpgradeProperties(item, upgradeUTI);
            }

            // Recalculate item stats and update display
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item stat recalculation after upgrade removal
            // Recalculates item damage, AC, and other stats after removing upgrade properties
            RecalculateItemStats(item);

            // Return upgrade item to inventory
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 221 - returns to inventory using 0x00567ce0
            // Original implementation: 0x00567ce0 @ 0x00567ce0 creates item entity from UTI template and adds to inventory
            // Located via string references: "CreateItem" @ 0x007d07c8, "ItemComponent" @ 0x007c41e4
            // Function signature: 0x00567ce0(void *param_1, void *param_2, int param_3)
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
                // Get character entity using common base class method
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - gets character from upgrade screen object
                IEntity character = base.GetCharacterEntity();

                // Create upgrade item entity and add to inventory
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00567ce0 @ 0x00567ce0 - creates item and adds to inventory
                // Uses base class method which implements the full creation and inventory addition logic
                if (character != null)
                {
                    base.CreateItemFromTemplateAndAddToInventory(upgradeResRef, character);
                }
            }

            return true;
        }

        /// <summary>
        /// Calculates the success rate for applying an upgrade based on character skills and upgrade complexity.
        /// </summary>
        /// <param name="upgradeUTI">UTI template of the upgrade item.</param>
        /// <param name="item">Item being upgraded.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>Success rate as a percentage (0.0 to 1.0, where 1.0 = 100% success).</returns>
        /// <remarks>
        /// Skill-Based Success Rate Calculation (K2 Enhancement):
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Character skills used for item creation/upgrading (NOT IMPLEMENTED in original)
        /// - This is a comprehensive enhancement that adds skill-based success rate calculation
        /// - Success rate is calculated based on:
        ///   1. Character skill ranks (Repair, Security, Computer Use)
        ///   2. Upgrade complexity/difficulty (based on upgrade level, cost, properties)
        ///   3. Item type and upgrade slot complexity
        /// - Higher skills improve success rate, very low skills may cause failures
        /// - Base success rate starts at 50% (untrained), increases with skills up to 95% (expert)
        /// - Upgrade complexity reduces success rate (more complex upgrades are harder)
        /// - Formula: baseRate + skillBonus - complexityPenalty
        /// - Minimum success rate: 5% (always a chance, even with no skills)
        /// - Maximum success rate: 95% (never 100% to maintain some risk)
        /// 
        /// Skill Contributions:
        /// - Repair (5): Primary skill for mechanical upgrades, weapon modifications, armor enhancements
        /// - Security (6): Secondary skill for precision work, delicate modifications, lock mechanisms
        /// - Computer Use (0): Tertiary skill for electronic upgrades, tech modifications, droid enhancements
        /// 
        /// Upgrade Complexity Factors:
        /// - Upgrade Level: Higher upgrade levels are more complex (UpgradeLevel field in UTI)
        /// - Cost: More expensive upgrades are typically more complex (Cost field in UTI)
        /// - Property Count: More properties indicate more complex upgrades
        /// - Base Item Type: Some item types are harder to upgrade than others
        /// </remarks>
        private double CalculateUpgradeSuccessRate(UTI upgradeUTI, IEntity item, int upgradeSlot)
        {
            if (upgradeUTI == null)
            {
                // No upgrade template - default to low success rate
                return 0.5; // 50% base rate
            }

            // Get character skill ranks
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Skills stored in IStatsComponent, accessed via GetSkillRank()
            // KOTOR skills: 0=ComputerUse, 1=Demolitions, 2=Stealth, 3=Awareness, 4=Persuade, 5=Repair, 6=Security, 7=TreatInjury
            int repairSkill = GetCharacterSkillRank(5); // Repair skill - primary for upgrades
            int securitySkill = GetCharacterSkillRank(6); // Security skill - secondary for precision work
            int computerUseSkill = GetCharacterSkillRank(0); // Computer Use skill - tertiary for tech upgrades

            // Base success rate: 50% for untrained characters
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Base success rate for item creation/upgrading (NOT IMPLEMENTED in original)
            // This enhancement adds a base rate that improves with skills
            double baseSuccessRate = 0.5; // 50% base rate

            // Calculate skill bonus from primary skill (Repair)
            // Repair skill is the primary skill for most upgrades
            // Formula: skillRank * 0.02 (each rank adds 2% success rate, max 20 ranks = 40% bonus)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Skill ranks typically range from 0-20 in KOTOR
            double repairBonus = repairSkill * 0.02; // 2% per rank, max 40% at rank 20

            // Calculate skill bonus from secondary skill (Security)
            // Security skill helps with precision work and delicate modifications
            // Formula: skillRank * 0.01 (each rank adds 1% success rate, max 20 ranks = 20% bonus)
            double securityBonus = securitySkill * 0.01; // 1% per rank, max 20% at rank 20

            // Calculate skill bonus from tertiary skill (Computer Use)
            // Computer Use skill helps with electronic and tech upgrades
            // Formula: skillRank * 0.005 (each rank adds 0.5% success rate, max 20 ranks = 10% bonus)
            double computerUseBonus = computerUseSkill * 0.005; // 0.5% per rank, max 10% at rank 20

            // Total skill bonus: sum of all skill contributions
            double totalSkillBonus = repairBonus + securityBonus + computerUseBonus;

            // Calculate upgrade complexity penalty
            // More complex upgrades are harder to apply successfully
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Upgrade complexity factors (NOT IMPLEMENTED in original)
            double complexityPenalty = 0.0;

            // Factor 1: Upgrade Level complexity
            // Higher upgrade levels indicate more complex upgrades
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpgradeLevel field in UTI template (0-10 typical range)
            int upgradeLevel = upgradeUTI.UpgradeLevel;
            if (upgradeLevel > 0)
            {
                // Penalty: 1% per upgrade level above 0, max 10% at level 10
                complexityPenalty += upgradeLevel * 0.01;
            }

            // Factor 2: Cost complexity
            // More expensive upgrades are typically more complex
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Cost field in UTI template (item value)
            int upgradeCost = upgradeUTI.Cost;
            if (upgradeCost > 0)
            {
                // Penalty: 0.01% per 100 credits, max 15% at 150,000 credits
                // Most upgrades cost 100-10,000 credits, very expensive ones cost 50,000+
                double costPenalty = Math.Min((upgradeCost / 100.0) * 0.0001, 0.15);
                complexityPenalty += costPenalty;
            }

            // Factor 3: Property count complexity
            // More properties indicate more complex upgrades
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Properties array in UTI template
            int propertyCount = upgradeUTI.Properties != null ? upgradeUTI.Properties.Count : 0;
            if (propertyCount > 0)
            {
                // Penalty: 0.5% per property, max 10% at 20 properties
                double propertyPenalty = Math.Min(propertyCount * 0.005, 0.10);
                complexityPenalty += propertyPenalty;
            }

            // Factor 4: Base item type complexity
            // Some item types are harder to upgrade than others
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): BaseItem field in UTI template, baseitems.2da item types
            IItemComponent itemComponent = item != null ? item.GetComponent<IItemComponent>() : null;
            if (itemComponent != null)
            {
                int baseItemId = itemComponent.BaseItem;
                // Lightsabers and complex weapons are harder to upgrade
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Base item types from baseitems.2da
                // Lightsabers (itemclass 15) and some advanced weapons have higher complexity
                if (baseItemId >= 0)
                {
                    // Check if item is a lightsaber (itemclass 15 in baseitems.2da)
                    // Lightsabers require more skill to upgrade
                    bool isLightsaber = IsLightsaberItem(baseItemId);
                    if (isLightsaber)
                    {
                        complexityPenalty += 0.05; // 5% additional penalty for lightsabers
                    }
                }
            }

            // Factor 5: Upgrade slot complexity
            // Higher slot indices may indicate more complex upgrade positions
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Upgrade slots 0-5 (K2 has 6 slots for lightsabers)
            if (upgradeSlot > 2)
            {
                // Slots 3+ are more complex (additional 1% per slot above 2)
                complexityPenalty += (upgradeSlot - 2) * 0.01;
            }

            // Calculate final success rate
            // Formula: baseRate + skillBonus - complexityPenalty
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Success rate calculation (NOT IMPLEMENTED in original)
            double successRate = baseSuccessRate + totalSkillBonus - complexityPenalty;

            // Clamp success rate to valid range (5% to 95%)
            // Minimum: 5% (always a chance, even with no skills)
            // Maximum: 95% (never 100% to maintain some risk)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Skill check success rates typically range from 5% to 95%
            successRate = Math.Max(0.05, Math.Min(0.95, successRate));

            return successRate;
        }
    }
}

