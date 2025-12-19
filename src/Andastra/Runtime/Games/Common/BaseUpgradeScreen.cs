using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Formats.GFF;
using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of upgrade screen functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Upgrade Screen Implementation:
    /// - Common upgrade screen properties and methods across all engines
    /// - Handles item upgrade UI and logic for modifying weapons and armor
    /// - Provides base for engine-specific upgrade screen implementations
    /// - Cross-engine analysis: All engines that support item upgrades share common patterns
    /// - Common functionality: Upgrade slot management, inventory checking, property application
    /// - Engine-specific: 2DA file names, upgrade slot counts, UI implementation details
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: FUN_006c7630 (constructor), FUN_006c6500 (button handler), FUN_006c59a0 (ApplyUpgrade)
    /// - swkotor2.exe: FUN_00731a00 (constructor), FUN_0072e260 (button handler), FUN_00729640 (ApplyUpgrade)
    /// - daorigins.exe: ItemUpgrade, GUIItemUpgrade, COMMAND_OPENITEMUPGRADEGUI
    /// - DragonAge2.exe: ItemUpgrade, GUIItemUpgrade, UpgradePrereqType, GetAbilityUpgradedValue
    ///
    /// Common structure across engines:
    /// - TargetItem: Item being upgraded (null for all items)
    /// - Character: Character whose skills/inventory are used
    /// - DisableItemCreation/DisableUpgrade: UI mode flags
    /// - Override2DA: Custom 2DA file override
    /// - IsVisible: Screen visibility state
    /// - GetAvailableUpgrades: Returns compatible upgrades from inventory
    /// - ApplyUpgrade/RemoveUpgrade: Modifies item properties
    /// </remarks>
    [PublicAPI]
    public abstract class BaseUpgradeScreen : IUpgradeScreen
    {
        protected readonly Installation _installation;
        protected readonly IWorld _world;
        protected IEntity _targetItem;
        protected IEntity _character;
        protected bool _disableItemCreation;
        protected bool _disableUpgrade;
        protected string _override2DA;
        protected bool _isVisible;

        // Track upgrade ResRefs by item+slot for removal
        // Key: item ObjectId + "_" + upgradeSlot, Value: upgrade ResRef
        protected readonly Dictionary<string, string> _upgradeResRefMap = new Dictionary<string, string>();

        /// <summary>
        /// Initializes a new instance of the upgrade screen.
        /// </summary>
        /// <param name="installation">Game installation for accessing 2DA files.</param>
        /// <param name="world">World context for entity access.</param>
        protected BaseUpgradeScreen(Installation installation, IWorld world)
        {
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            _installation = installation;
            _world = world;
            _isVisible = false;
            _override2DA = string.Empty;
        }

        /// <summary>
        /// Gets or sets the item being upgraded (null for all items).
        /// </summary>
        public IEntity TargetItem
        {
            get { return _targetItem; }
            set { _targetItem = value; }
        }

        /// <summary>
        /// Gets or sets the character whose skills will be used (null for player).
        /// </summary>
        public IEntity Character
        {
            get { return _character; }
            set { _character = value; }
        }

        /// <summary>
        /// Gets or sets whether item creation is disabled.
        /// </summary>
        public bool DisableItemCreation
        {
            get { return _disableItemCreation; }
            set { _disableItemCreation = value; }
        }

        /// <summary>
        /// Gets or sets whether upgrading is disabled (forces item creation).
        /// </summary>
        public bool DisableUpgrade
        {
            get { return _disableUpgrade; }
            set { _disableUpgrade = value; }
        }

        /// <summary>
        /// Gets or sets the override 2DA file name (empty for default).
        /// </summary>
        public string Override2DA
        {
            get { return _override2DA; }
            set { _override2DA = value ?? string.Empty; }
        }

        /// <summary>
        /// Gets whether the upgrade screen is visible.
        /// </summary>
        public bool IsVisible
        {
            get { return _isVisible; }
        }

        /// <summary>
        /// Shows the upgrade screen.
        /// </summary>
        public abstract void Show();

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        public abstract void Hide();

        /// <summary>
        /// Gets available upgrade items for a given item and upgrade slot.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>List of available upgrade items (ResRefs).</returns>
        public abstract List<string> GetAvailableUpgrades(IEntity item, int upgradeSlot);

        /// <summary>
        /// Applies an upgrade to an item.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <param name="upgradeResRef">ResRef of upgrade item to apply.</param>
        /// <returns>True if upgrade was successful.</returns>
        public abstract bool ApplyUpgrade(IEntity item, int upgradeSlot, string upgradeResRef);

        /// <summary>
        /// Removes an upgrade from an item.
        /// </summary>
        /// <param name="item">Item to modify.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if upgrade was removed.</returns>
        public abstract bool RemoveUpgrade(IEntity item, int upgradeSlot);

        /// <summary>
        /// Gets the upgrade table name for regular items (not lightsabers).
        /// </summary>
        /// <returns>Table name for regular item upgrades.</returns>
        protected abstract string GetRegularUpgradeTableName();

        /// <summary>
        /// Gets the upgrade table name for lightsabers.
        /// </summary>
        /// <returns>Table name for lightsaber upgrades.</returns>
        protected virtual string GetLightsaberUpgradeTableName()
        {
            // Default: Most engines use "upcrystals" for lightsabers
            return "upcrystals";
        }

        /// <summary>
        /// Determines if an item is a lightsaber by checking baseitems.2da.
        /// </summary>
        /// <param name="baseItemId">Base item ID from baseitems.2da.</param>
        /// <returns>True if the item is a lightsaber, false otherwise.</returns>
        protected virtual bool IsLightsaberItem(int baseItemId)
        {
            if (baseItemId <= 0)
            {
                return false;
            }

            try
            {
                // Load baseitems.2da to check item class
                ResourceResult baseitemsResult = _installation.Resource("baseitems", ResourceType.TwoDA, null, null);
                if (baseitemsResult != null && baseitemsResult.Data != null)
                {
                    using (var stream = new MemoryStream(baseitemsResult.Data))
                    {
                        var reader = new TwoDABinaryReader(stream);
                        TwoDA baseitems = reader.Load();

                        if (baseitems != null && baseItemId >= 0 && baseItemId < baseitems.GetHeight())
                        {
                            TwoDARow row = baseitems.GetRow(baseItemId);

                            // Check itemclass column (lightsabers typically have itemclass = 15)
                            int? itemClass = row.GetInteger("itemclass", null);
                            if (itemClass.HasValue && itemClass.Value == 15)
                            {
                                return true;
                            }

                            // Alternative: Check weapontype column if available
                            int? weaponType = row.GetInteger("weapontype", null);
                            if (weaponType.HasValue && weaponType.Value == 4)
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Error loading baseitems.2da, fall through
            }

            return false;
        }

        /// <summary>
        /// Loads an upgrade item UTI template from the installation.
        /// </summary>
        /// <param name="upgradeResRef">ResRef of the upgrade item template to load.</param>
        /// <returns>UTI template if loaded successfully, null otherwise.</returns>
        protected UTI LoadUpgradeUTITemplate(string upgradeResRef)
        {
            if (string.IsNullOrEmpty(upgradeResRef))
            {
                return null;
            }

            try
            {
                // Normalize ResRef (ensure .uti extension if needed)
                string normalizedResRef = upgradeResRef;
                if (!normalizedResRef.EndsWith(".uti", StringComparison.OrdinalIgnoreCase))
                {
                    normalizedResRef = normalizedResRef + ".uti";
                }

                // Load UTI resource from installation
                ResourceResult utiResult = _installation.Resource(normalizedResRef, ResourceType.UTI, null, null);
                if (utiResult == null || utiResult.Data == null || utiResult.Data.Length == 0)
                {
                    return null;
                }

                // Parse UTI GFF data
                using (var stream = new MemoryStream(utiResult.Data))
                {
                    var reader = new GFFBinaryReader(stream);
                    GFF gff = reader.Load();
                    if (gff == null)
                    {
                        return null;
                    }

                    // Construct UTI from GFF
                    UTI utiTemplate = UTIHelpers.ConstructUti(gff);
                    return utiTemplate;
                }
            }
            catch (Exception)
            {
                // Error loading or parsing UTI template
                return null;
            }
        }

        /// <summary>
        /// Applies properties from an upgrade UTI template to an item.
        /// </summary>
        /// <param name="item">Item to apply upgrade properties to.</param>
        /// <param name="upgradeUTI">UTI template of the upgrade item.</param>
        /// <returns>True if properties were applied successfully.</returns>
        protected bool ApplyUpgradeProperties(IEntity item, UTI upgradeUTI)
        {
            if (item == null || upgradeUTI == null)
            {
                return false;
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return false;
            }

            // Extract properties from upgrade UTI template and add to item
            foreach (var utiProp in upgradeUTI.Properties)
            {
                // Convert UTI property to ItemProperty
                var itemProperty = new ItemProperty
                {
                    PropertyType = utiProp.PropertyName,
                    Subtype = utiProp.Subtype,
                    CostTable = utiProp.CostTable,
                    CostValue = utiProp.CostValue,
                    Param1 = utiProp.Param1,
                    Param1Value = utiProp.Param1Value
                };

                // Add property to item
                itemComponent.AddProperty(itemProperty);
            }

            return true;
        }

        /// <summary>
        /// Removes properties from an item that match those in an upgrade UTI template.
        /// </summary>
        /// <param name="item">Item to remove upgrade properties from.</param>
        /// <param name="upgradeUTI">UTI template of the upgrade item to remove.</param>
        /// <returns>True if properties were removed successfully.</returns>
        protected bool RemoveUpgradeProperties(IEntity item, UTI upgradeUTI)
        {
            if (item == null || upgradeUTI == null)
            {
                return false;
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return false;
            }

            // Remove properties from item that match upgrade UTI template
            var propertiesToRemove = new List<ItemProperty>();
            foreach (var itemProp in itemComponent.Properties)
            {
                // Check if this property matches any property in the upgrade UTI
                foreach (var utiProp in upgradeUTI.Properties)
                {
                    if (itemProp.PropertyType == utiProp.PropertyName &&
                        itemProp.Subtype == utiProp.Subtype &&
                        itemProp.CostTable == utiProp.CostTable &&
                        itemProp.CostValue == utiProp.CostValue &&
                        itemProp.Param1 == utiProp.Param1 &&
                        itemProp.Param1Value == utiProp.Param1Value)
                    {
                        // Property matches upgrade property - mark for removal
                        propertiesToRemove.Add(itemProp);
                        break; // Only remove first matching property (in case of duplicates)
                    }
                }
            }

            // Remove matched properties from item
            foreach (var propToRemove in propertiesToRemove)
            {
                itemComponent.RemoveProperty(propToRemove);
            }

            return true;
        }

        /// <summary>
        /// Recalculates item stats after applying or removing upgrades.
        /// </summary>
        /// <param name="item">Item to recalculate stats for.</param>
        protected virtual void RecalculateItemStats(IEntity item)
        {
            if (item == null)
            {
                return;
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return;
            }

            // Recalculate item stats based on current properties and upgrades
            // Stats affected by upgrades include:
            // - Damage bonuses (weapon damage, critical hit bonuses)
            // - AC bonuses (armor class improvements)
            // - Saving throw bonuses
            // - Skill bonuses
            // - Ability score bonuses
            // - Other property-based stat modifications

            // Note: Full stat calculation would require:
            // 1. Base item stats from baseitems.2da
            // 2. Property effect calculation (from itempropdef.2da)
            // 3. Cumulative bonus application
            // 4. UI display update

            // This method provides the framework for stat recalculation
            // Engine-specific implementations can override to provide full stat calculation
        }

        /// <summary>
        /// Gets character inventory ResRefs for upgrade availability checking.
        /// </summary>
        /// <param name="character">Character to get inventory from (null uses player).</param>
        /// <returns>Set of inventory item ResRefs (normalized, lowercase, no extension).</returns>
        protected HashSet<string> GetCharacterInventoryResRefs(IEntity character)
        {
            HashSet<string> inventoryResRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (character == null)
            {
                // Get player character from world using multiple fallback strategies
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

            // Collect all inventory items from character
            if (character != null)
            {
                IInventoryComponent characterInventory = character.GetComponent<IInventoryComponent>();
                if (characterInventory != null)
                {
                    foreach (IEntity inventoryItem in characterInventory.GetAllItems())
                    {
                        IItemComponent invItemComponent = inventoryItem.GetComponent<IItemComponent>();
                        if (invItemComponent != null && !string.IsNullOrEmpty(invItemComponent.TemplateResRef))
                        {
                            // Normalize ResRef (remove extension, lowercase)
                            string resRef = invItemComponent.TemplateResRef.ToLowerInvariant();
                            if (resRef.EndsWith(".uti"))
                            {
                                resRef = resRef.Substring(0, resRef.Length - 4);
                            }
                            inventoryResRefs.Add(resRef);
                        }
                    }
                }
            }

            return inventoryResRefs;
        }
    }
}

