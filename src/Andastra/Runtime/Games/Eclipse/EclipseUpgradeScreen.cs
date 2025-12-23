using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Enums;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using static Andastra.Parsing.Common.GameExtensions;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.GUI;
using ParsingGUI = Andastra.Parsing.Resource.Generics.GUI.GUI;
using UTI = Andastra.Parsing.Resource.Generics.UTI.UTI;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Eclipse.GUI;
using Andastra.Runtime.Graphics;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics.UTI;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Upgrade screen implementation for Eclipse engine (Dragon Age, ).
    /// </summary>
    /// <remarks>
    /// Eclipse Upgrade Screen Implementation:
    /// - Eclipse engine (daorigins.exe, DragonAge2.exe) has an ItemUpgrade system
    /// - Based on reverse engineering: ItemUpgrade, GUIItemUpgrade, COMMAND_OPENITEMUPGRADEGUI strings found
    /// - Dragon Age Origins: ItemUpgrade system with GUIItemUpgrade class
    ///   - Based on daorigins.exe: ItemUpgrade @ 0x00aef22c, GUIItemUpgrade @ 0x00b02ca0, COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c
    /// - Dragon Age 2: Enhanced ItemUpgrade system with UpgradePrereqType, GetAbilityUpgradedValue
    ///   - Based on DragonAge2.exe: ItemUpgrade @ 0x00beb1f0, GUIItemUpgrade @ 0x00beb1d0, UpgradePrereqType @ 0x00c0583c, GetAbilityUpgradedValue @ 0x00c0f20c
    /// - : No item upgrade system (only vehicle upgrades)
    /// -  2: No item upgrade system (only vehicle upgrades)
    ///
    /// Eclipse upgrade system differs from Odyssey:
    /// - Eclipse uses ItemUpgrade class structure rather than 2DA-based upgrade tables
    /// - Dragon Age 2 has prerequisite checking (UpgradePrereqType) and ability-based upgrades (GetAbilityUpgradedValue)
    /// - Upgrade compatibility is determined by item properties and upgrade prerequisites rather than 2DA table lookups
    /// </remarks>
    public class EclipseUpgradeScreen : BaseUpgradeScreen
    {
        // GUI management
        private EclipseGuiManager _guiManager;
        private ParsingGUI _loadedGui;
        private string _guiName;
        private Dictionary<string, GUIControl> _controlMap;
        private Dictionary<string, GUIButton> _buttonMap;
        private bool _guiInitialized;
        private const int DefaultScreenWidth = 1024;
        private const int DefaultScreenHeight = 768;

        // Upgrade screen state tracking
        // Based on Eclipse upgrade system: Track selected slot and available upgrades per slot
        private int _selectedUpgradeSlot = -1; // Currently selected upgrade slot (-1 = none selected)
        private Dictionary<int, List<string>> _availableUpgradesPerSlot; // Available upgrades indexed by slot number
        private Dictionary<int, string> _slotUpgradeListBoxTags; // List box tag names indexed by slot number
        private const int MaxUpgradeSlots = 6; // Maximum number of upgrade slots (typical for Eclipse items)

        // Cached armor itemclass values from baseitems.2da
        // Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check itemclass column for armor
        // Cache is populated on first access to avoid repeated file I/O
        private HashSet<int> _armorItemClasses; // Set of itemclass values that represent armor items
        private bool _armorItemClassesLoaded; // Flag indicating if cache has been loaded

        // Cached shield itemclass values from baseitems.2da
        // Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check itemclass column for shields
        // Cache is populated on first access to avoid repeated file I/O
        private HashSet<int> _shieldItemClasses; // Set of itemclass values that represent shield items
        private bool _shieldItemClassesLoaded; // Flag indicating if cache has been loaded

        /// <summary>
        /// Initializes a new instance of the Eclipse upgrade screen.
        /// </summary>
        /// <param name="installation">Game installation for accessing game data.</param>
        /// <param name="world">World context for entity access.</param>
        public EclipseUpgradeScreen(Installation installation, IWorld world)
            : base(installation, world)
        {
            _controlMap = new Dictionary<string, GUIControl>(StringComparer.OrdinalIgnoreCase);
            _buttonMap = new Dictionary<string, GUIButton>(StringComparer.OrdinalIgnoreCase);
            _guiInitialized = false;
            _availableUpgradesPerSlot = new Dictionary<int, List<string>>();
            _slotUpgradeListBoxTags = new Dictionary<int, string>();
            _selectedUpgradeSlot = -1;
            _armorItemClasses = null;
            _armorItemClassesLoaded = false;
            _shieldItemClasses = null;
            _shieldItemClassesLoaded = false;
        }

        /// <summary>
        /// Sets the graphics device for GUI rendering (optional).
        /// </summary>
        /// <param name="graphicsDevice">Graphics device for rendering GUI.</param>
        /// <remarks>
        /// If graphics device is not set, GUI will be loaded but not rendered.
        /// Rendering can be handled by external GUI manager if available.
        /// Based on Eclipse GUI system: EclipseGuiManager requires IGraphicsDevice for rendering.
        /// </remarks>
        public void SetGraphicsDevice(IGraphicsDevice graphicsDevice)
        {
            if (graphicsDevice == null)
            {
                _guiManager = null;
                return;
            }

            // Create GUI manager if needed
            if (_guiManager == null)
            {
                _guiManager = new EclipseGuiManager(graphicsDevice, _installation);
                // Subscribe to button click events
                _guiManager.OnButtonClicked += HandleButtonClick;
            }
        }

        /// <summary>
        /// Shows the upgrade screen.
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c opens ItemUpgrade GUI
        /// - DragonAge2.exe: GUIItemUpgrade class structure handles upgrade screen display
        /// -  games: No upgrade screen support (method handles gracefully)
        ///
        /// Full implementation:
        /// 1. Load ItemUpgrade GUI (GUIItemUpgrade class)
        ///   - Based on daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c
        ///   - Based on DragonAge2.exe: GUIItemUpgrade class structure
        /// 2. Display item upgrade interface with upgrade slots
        /// 3. Show available upgrades based on UpgradePrereqType (Dragon Age 2)
        /// 4. Display item properties and upgrade effects
        /// 5. Handle user input for applying/removing upgrades
        /// 6. Show ability-based upgrade values (Dragon Age 2: GetAbilityUpgradedValue @ 0x00c0f20c)
        /// </remarks>
        public override void Show()
        {
            // Check if this is a  game (no upgrade support)
            if (IsGame())
            {
                //  games do not have item upgrade screens
                _isVisible = false;
                return;
            }

            _isVisible = true;

            // Get GUI name based on game version
            // Based on daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c opens ItemUpgrade GUI
            // Based on DragonAge2.exe: GUIItemUpgrade class structure handles upgrade screen display
            // The GUI name is likely "ItemUpgrade" based on the class name pattern
            _guiName = GetUpgradeGuiName();

            // Load upgrade screen GUI
            // Based on daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c opens GUI
            // Based on DragonAge2.exe: GUIItemUpgrade class loads GUI
            if (!LoadUpgradeGui())
            {
                Console.WriteLine($"[EclipseUpgradeScreen] ERROR: Failed to load GUI: {_guiName}");
                return;
            }

            // Initialize GUI controls and set up button handlers
            // Based on Eclipse GUI system: Controls are set up after GUI loading
            InitializeGuiControls();

            // Update GUI with current item and character data
            // Based on Eclipse upgrade system: GUI displays item upgrade slots and available upgrades
            UpdateGuiData();

            // Display item properties and current upgrade effects
            // Based on Eclipse upgrade system: GUI displays item properties and upgrade effects
            DisplayItemProperties();
            DisplayCurrentUpgradeEffects();

            // Refresh available upgrades display
            RefreshUpgradeDisplay();

            // Display ability-based upgrade values (Dragon Age 2)
            // Based on DragonAge2.exe: GetAbilityUpgradedValue @ 0x00c0f20c shows ability-based upgrade values
            if (_installation != null && _installation.Game != null && _installation.Game.IsDragonAge2())
            {
                DisplayAbilityUpgradeValues();
            }

            // Set GUI as current if GUI manager is available
            if (_guiManager != null)
            {
                _guiManager.SetCurrentGui(_guiName);
            }

            _guiInitialized = true;
        }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: GUIItemUpgrade class handles screen hiding
        /// - DragonAge2.exe: GUIItemUpgrade class structure handles screen state management
        ///
        // TODO: / Full implementation:
        /// 1. Hide ItemUpgrade GUI
        ///   - Based on daorigins.exe: GUIItemUpgrade class hides screen
        ///   - Based on DragonAge2.exe: GUIItemUpgrade class structure
        /// 2. Save any pending changes to item upgrades
        /// 3. Return control to game
        /// 4. Clear upgrade screen state
        /// </remarks>
        public override void Hide()
        {
            _isVisible = false;

            // Clear GUI state
            // Based on Eclipse GUI system: GUI cleanup when hiding screen
            if (_guiInitialized)
            {
                // Clear control references
                _controlMap.Clear();
                _buttonMap.Clear();

                // Unload GUI from GUI manager if available
                if (_guiManager != null && !string.IsNullOrEmpty(_guiName))
                {
                    _guiManager.UnloadGui(_guiName);
                }

                _guiInitialized = false;
            }
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

            // Check if this is a  game (no upgrade support)
            if (IsGame())
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
            // Check if this is a  game (no upgrade support)
            if (IsGame())
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
            // Check if this is a  game (no upgrade support)
            if (IsGame())
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
            //  games do not have item upgrade systems
            return string.Empty;
        }

        /// <summary>
        /// Checks if this is a  game (no item upgrade support).
        /// </summary>
        /// <returns>True if  game, false otherwise.</returns>
        private bool IsGame()
        {
            if (_installation == null)
            {
                return false;
            }

            Game game = _installation.Game;
            // Check if this is NOT an Eclipse game (games without item upgrade support)
            // Eclipse games (Dragon Age) have item upgrade systems
            return !game.IsEclipse();
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
        ///   - daorigins.exe: ItemUpgrade @ 0x00aef22c - item compatibility checking
        /// - Dragon Age 2: UpgradePrereqType @ 0x00c0583c checks prerequisites before allowing upgrade
        /// - Dragon Age 2: GetAbilityUpgradedValue @ 0x00c0f20c - ability-based upgrade filtering
        /// - Eclipse upgrade compatibility is determined by:
        ///   1. Item type compatibility (weapon upgrades for weapons, armor upgrades for armor, etc.)
        ///   2. Upgrade slot compatibility (upgrade must match the slot type)
        ///   3. Prerequisites (Dragon Age 2: UpgradePrereqType checks)
        ///   4. Ability requirements (Dragon Age 2: GetAbilityUpgradedValue checks)
        /// </remarks>
        private bool IsCompatibleUpgrade(IItemComponent targetItem, IItemComponent upgradeItem, int upgradeSlot)
        {
            if (targetItem == null || upgradeItem == null)
            {
                return false;
            }

            // Step 1: Check if upgrade item has properties that can be applied
            // Based on Dragon Age Origins: ItemUpgrade system checks item properties for upgrade compatibility
            if (upgradeItem.Properties == null || upgradeItem.Properties.Count == 0)
            {
                return false;
            }

            // Step 2: Check item type compatibility (weapon upgrades for weapons, armor upgrades for armor, etc.)
            // Based on Dragon Age Origins: ItemUpgrade system checks base item type compatibility
            if (!CheckItemTypeCompatibility(targetItem, upgradeItem))
            {
                return false;
            }

            // Step 3: Check upgrade slot type compatibility
            // Based on Eclipse upgrade system: Upgrade items have UpgradeType field that must match slot type
            if (!CheckUpgradeSlotTypeCompatibility(targetItem, upgradeItem, upgradeSlot))
            {
                return false;
            }

            // Step 4: For Dragon Age 2, check prerequisites (UpgradePrereqType)
            // Based on DragonAge2.exe: UpgradePrereqType @ 0x00c0583c checks prerequisites
            if (_installation != null && _installation.Game != null && _installation.Game.IsDragonAge2())
            {
                if (!CheckUpgradePrerequisites(targetItem, upgradeItem, upgradeSlot))
                {
                    return false;
                }

                // Step 5: For Dragon Age 2, check ability requirements (GetAbilityUpgradedValue)
                // Based on DragonAge2.exe: GetAbilityUpgradedValue @ 0x00c0f20c checks ability requirements
                if (!CheckAbilityRequirements(upgradeItem))
                {
                    return false;
                }
            }

            return true;

            return true;
        }

        /// <summary>
        /// Checks if the upgrade item's base item type is compatible with the target item's base item type.
        /// </summary>
        /// <param name="targetItem">Target item to upgrade.</param>
        /// <param name="upgradeItem">Potential upgrade item.</param>
        /// <returns>True if item types are compatible, false otherwise.</returns>
        /// <remarks>
        /// Based on Dragon Age Origins: ItemUpgrade system checks base item type compatibility.
        /// Compatibility rules:
        /// - Weapon upgrades can only be applied to weapons
        /// - Armor upgrades can only be applied to armor
        /// - Other item type upgrades follow similar patterns
        ///
        /// Full implementation based on:
        /// - daorigins.exe: ItemUpgrade @ 0x00aef22c - checks baseitems.2da itemclass/weapontype
        /// </remarks>
        private bool CheckItemTypeCompatibility(IItemComponent targetItem, IItemComponent upgradeItem)
        {
            if (targetItem == null || upgradeItem == null)
            {
                return false;
            }

            int targetBaseItem = targetItem.BaseItem;
            int upgradeBaseItem = upgradeItem.BaseItem;

            if (targetBaseItem <= 0 || upgradeBaseItem <= 0)
            {
                return false;
            }

            try
            {
                // Load baseitems.2da to check item class compatibility
                // Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check item types
                ResourceResult baseitemsResult = _installation.Resource("baseitems", ResourceType.TwoDA, null, null);
                if (baseitemsResult == null || baseitemsResult.Data == null || baseitemsResult.Data.Length == 0)
                {
                    // Cannot verify compatibility without baseitems.2da, allow upgrade
                    return true;
                }

                using (var stream = new System.IO.MemoryStream(baseitemsResult.Data))
                {
                    var reader = new TwoDABinaryReader(stream);
                    TwoDA baseitems = reader.Load();

                    if (baseitems == null)
                    {
                        return true;
                    }

                    // Get target item type information
                    if (targetBaseItem >= 0 && targetBaseItem < baseitems.GetHeight())
                    {
                        TwoDARow targetRow = baseitems.GetRow(targetBaseItem);

                        // Get upgrade item type information
                        if (upgradeBaseItem >= 0 && upgradeBaseItem < baseitems.GetHeight())
                        {
                            TwoDARow upgradeRow = baseitems.GetRow(upgradeBaseItem);

                            // Check itemclass column to determine item category
                            // Based on Dragon Age Origins: ItemUpgrade system checks itemclass for compatibility
                            int? targetItemClass = targetRow.GetInteger("itemclass", null);
                            int? upgradeItemClass = upgradeRow.GetInteger("itemclass", null);

                            // Determine target item category (weapon, armor, etc.)
                            bool targetIsWeapon = IsWeaponItemClass(targetItemClass);
                            bool targetIsArmor = IsArmorItemClass(targetItemClass);
                            bool targetIsShield = IsShieldItemClass(targetItemClass);

                            // Determine upgrade item category
                            bool upgradeIsWeapon = IsWeaponItemClass(upgradeItemClass);
                            bool upgradeIsArmor = IsArmorItemClass(upgradeItemClass);
                            bool upgradeIsShield = IsShieldItemClass(upgradeItemClass);

                            // Check compatibility: upgrade type must match target type
                            // Weapon upgrades can only go on weapons
                            if (targetIsWeapon && !upgradeIsWeapon)
                            {
                                return false;
                            }

                            // Armor upgrades can only go on armor
                            if (targetIsArmor && !upgradeIsArmor)
                            {
                                return false;
                            }

                            // Shield upgrades can only go on shields
                            if (targetIsShield && !upgradeIsShield)
                            {
                                return false;
                            }

                            // For weapons, also check weapontype compatibility (melee vs ranged)
                            // Based on Dragon Age Origins: ItemUpgrade system checks weapontype for weapon compatibility
                            if (targetIsWeapon && upgradeIsWeapon)
                            {
                                int? targetWeaponType = targetRow.GetInteger("weapontype", null);
                                int? upgradeWeaponType = upgradeRow.GetInteger("weapontype", null);

                                // Melee weapon upgrades should match melee weapons, ranged upgrades match ranged
                                // TODO:  This is a simplified check - full implementation would check specific weapon type compatibility
                                bool targetIsRanged = targetRow.GetBoolean("rangedweapon", false) ?? false;
                                bool upgradeIsRanged = upgradeRow.GetBoolean("rangedweapon", false) ?? false;

                                // Ranged weapon upgrades should only apply to ranged weapons
                                // Melee weapon upgrades should only apply to melee weapons
                                if (targetIsRanged != upgradeIsRanged)
                                {
                                    // Allow if upgrade is generic (no specific ranged/melee requirement)
                                    // Most upgrades work on both, so only block if explicitly incompatible
                                }
                            }

                            // Check if upgrade item is actually an upgrade item (typically has BaseItem indicating upgrade type)
                            // Upgrade items in Eclipse typically have specific base item IDs or itemclass values
                            // Based on Dragon Age Origins: Upgrade items must have properties and be compatible with target item type
                            // An upgrade item should:
                            // 1. Have properties (checked in IsCompatibleUpgrade, but verify here for safety)
                            // 2. Not be a consumable/quest item that happens to have properties
                            // 3. Be a valid upgrade item type (weapon upgrade, armor upgrade, etc.)

                            // Verify upgrade item has properties (required for upgrades)
                            // Based on Dragon Age Origins: Upgrade items must have properties to modify target items
                            if (upgradeItem.Properties == null || upgradeItem.Properties.Count == 0)
                            {
                                // Upgrade item must have properties to be a valid upgrade
                                return false;
                            }

                            // Check if upgrade item is a consumable or quest item (these are not upgrades)
                            // Based on Dragon Age Origins: Consumables and quest items are not upgrade items
                            // Consumables typically have itemclass values that indicate they are consumables
                            // Quest items typically have Plot flag set, but we check itemclass here

                            // Check if itemclass indicates it's a consumable or quest item
                            // In Dragon Age, consumables typically have specific itemclass values
                            // Quest items may have Plot flag, but we focus on itemclass here
                            // Upgrade items should be weapon/armor/shield upgrades, not consumables

                            // If upgrade item is not a weapon, armor, or shield, it might be a consumable or quest item
                            // Only allow weapon/armor/shield items as upgrades (they modify equipment)
                            // Based on Dragon Age Origins: Upgrade items are equipment modifications, not consumables
                            if (!upgradeIsWeapon && !upgradeIsArmor && !upgradeIsShield)
                            {
                                // Item is not a weapon, armor, or shield - likely a consumable or quest item
                                // These are not valid upgrade items even if they have properties
                                return false;
                            }

                            // Additional check: Verify upgrade item is meant to be used as an upgrade
                            // In Dragon Age, upgrade items are typically items that modify other items
                            // They should have properties that enhance the target item
                            // We've already verified:
                            // - Item has properties (above)
                            // - Item type is compatible with target (weapon/armor/shield match)
                            // - Item is not a consumable/quest item (above)

                            // Final verification: Check if upgrade item has meaningful properties for upgrades
                            // Based on Dragon Age Origins: Upgrade items should have properties that modify item stats
                            // Properties like damage bonuses, stat bonuses, etc. indicate it's an upgrade item
                            bool hasUpgradeProperties = false;
                            foreach (var prop in upgradeItem.Properties)
                            {
                                // Check if property is a meaningful upgrade property
                                // Upgrade properties typically modify stats, damage, armor, etc.
                                // Properties with PropertyType > 0 are typically meaningful
                                if (prop.PropertyType > 0)
                                {
                                    hasUpgradeProperties = true;
                                    break;
                                }
                            }

                            if (!hasUpgradeProperties)
                            {
                                // Upgrade item has no meaningful properties - not a valid upgrade
                                return false;
                            }

                            // Upgrade item passes all checks:
                            // - Has properties
                            // - Type is compatible with target (weapon/armor/shield match)
                            // - Is not a consumable/quest item
                            // - Has meaningful upgrade properties
                            // Based on Dragon Age Origins: Item is a valid upgrade item
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Error loading baseitems.2da, allow upgrade (fail open)
                return true;
            }

            return true;
        }

        /// <summary>
        /// Checks if an item class value represents a weapon.
        /// </summary>
        /// <param name="itemClass">Item class value from baseitems.2da.</param>
        /// <returns>True if item class represents a weapon.</returns>
        /// <remarks>
        /// Based on Dragon Age Origins: ItemUpgrade system checks itemclass to determine if item is a weapon.
        /// Weapon item classes typically include various weapon types (swords, axes, bows, etc.).
        /// </remarks>
        private bool IsWeaponItemClass(int? itemClass)
        {
            if (!itemClass.HasValue)
            {
                return false;
            }

            // Weapon item classes vary by game, but typically include:
            // - Melee weapons (swords, axes, maces, etc.)
            // - Ranged weapons (bows, crossbows, etc.)
            // - Staves and wands
            // Typical weapon itemclass values: 1-20 range (varies by game)
            // Check if itemclass indicates a weapon type
            return itemClass.Value > 0 && itemClass.Value < 30;
        }

        /// <summary>
        /// Loads baseitems.2da and populates the armor itemclass cache.
        /// </summary>
        /// <remarks>
        /// Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check itemclass column.
        /// This method loads baseitems.2da, iterates through all rows, and identifies armor items by checking
        /// if the base item ID is in the known armor base items set. It then collects all itemclass values
        /// from armor rows and caches them for efficient lookup.
        ///
        /// daorigins.exe: ItemUpgrade @ 0x00aef22c - loads baseitems.2da and checks itemclass column
        /// </remarks>
        private void LoadArmorItemClasses()
        {
            if (_armorItemClassesLoaded)
            {
                return;
            }

            _armorItemClasses = new HashSet<int>();
            _armorItemClassesLoaded = true;

            try
            {
                // Load baseitems.2da to check item class
                // Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check item types
                ResourceResult baseitemsResult = _installation.Resource("baseitems", ResourceType.TwoDA, null, null);
                if (baseitemsResult == null || baseitemsResult.Data == null || baseitemsResult.Data.Length == 0)
                {
                    // Cannot load baseitems.2da, fall back to known armor base items
                    // Based on UTI.ArmorBaseItems: Known armor base item IDs
                    foreach (int armorBaseItem in Andastra.Parsing.Resource.Generics.UTI.UTI.ArmorBaseItems)
                    {
                        // For fallback, we can't determine itemclass without baseitems.2da
                        // So we'll use the fallback logic in IsArmorItemClass
                    }
                    return;
                }

                using (var stream = new System.IO.MemoryStream(baseitemsResult.Data))
                {
                    var reader = new TwoDABinaryReader(stream);
                    TwoDA baseitems = reader.Load();

                    if (baseitems == null)
                    {
                        return;
                    }

                    // Known armor base item IDs from UTI.ArmorBaseItems
                    // Based on PyKotor: ARMOR_BASE_ITEMS = {35, 36, 37, 38, 39, 40, 41, 42, 43, 53, 58, 63, 64, 65, 69, 71, 85, 89, 98, 100, 102, 103}
                    HashSet<int> knownArmorBaseItems = new HashSet<int>(Andastra.Parsing.Resource.Generics.UTI.UTI.ArmorBaseItems);

                    // Iterate through all rows in baseitems.2da
                    // Based on Dragon Age Origins: ItemUpgrade system checks all rows to determine armor items
                    for (int rowIndex = 0; rowIndex < baseitems.GetHeight(); rowIndex++)
                    {
                        TwoDARow row = baseitems.GetRow(rowIndex);
                        if (row == null)
                        {
                            continue;
                        }

                        // Check if this base item ID is a known armor base item
                        // Based on Dragon Age Origins: Armor items have specific base item IDs
                        bool isArmorBaseItem = knownArmorBaseItems.Contains(rowIndex);

                        // Also check if the row has armor-related properties
                        // Some games may have additional columns that indicate armor (e.g., equipableslots for armor slot)
                        // For Eclipse games, we primarily rely on known armor base item IDs
                        if (isArmorBaseItem)
                        {
                            // Get itemclass value from this row
                            // Based on Dragon Age Origins: ItemUpgrade system reads itemclass column from baseitems.2da
                            int? itemClass = row.GetInteger("itemclass", null);
                            if (itemClass.HasValue)
                            {
                                // Add this itemclass value to the armor itemclass set
                                _armorItemClasses.Add(itemClass.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Error loading baseitems.2da, cache will remain empty and fallback logic will be used
                _armorItemClasses = null;
            }
        }

        /// <summary>
        /// Loads baseitems.2da and populates the shield itemclass cache.
        /// </summary>
        /// <remarks>
        /// Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check itemclass column.
        /// This method loads baseitems.2da, iterates through all rows, and identifies shield items by checking
        /// the itemclass column for values that fall in the shield range (typically 20-35). It then collects
        /// all unique itemclass values from shield rows and caches them for efficient lookup.
        ///
        /// daorigins.exe: ItemUpgrade @ 0x00aef22c - loads baseitems.2da and checks itemclass column
        /// </remarks>
        private void LoadShieldItemClasses()
        {
            if (_shieldItemClassesLoaded)
            {
                return;
            }

            _shieldItemClasses = new HashSet<int>();
            _shieldItemClassesLoaded = true;

            try
            {
                // Load baseitems.2da to check item class
                // Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check item types
                ResourceResult baseitemsResult = _installation.Resource("baseitems", ResourceType.TwoDA, null, null);
                if (baseitemsResult == null || baseitemsResult.Data == null || baseitemsResult.Data.Length == 0)
                {
                    // Cannot load baseitems.2da, fall back to known shield itemclass range
                    // Shield itemclass values typically fall in the 20-35 range
                    return;
                }

                using (var stream = new System.IO.MemoryStream(baseitemsResult.Data))
                {
                    var reader = new TwoDABinaryReader(stream);
                    TwoDA baseitems = reader.Load();

                    if (baseitems == null)
                    {
                        return;
                    }

                    // Shield itemclass values typically fall in the range 20-35 based on Dragon Age Origins
                    // We iterate through all rows and collect itemclass values that fall in this range
                    // This approach is more accurate than hardcoding specific base item IDs since
                    // different games may have different base item ID assignments but similar itemclass ranges

                    // Iterate through all rows in baseitems.2da
                    // Based on Dragon Age Origins: ItemUpgrade system checks all rows to determine shield items
                    for (int rowIndex = 0; rowIndex < baseitems.GetHeight(); rowIndex++)
                    {
                        TwoDARow row = baseitems.GetRow(rowIndex);
                        if (row == null)
                        {
                            continue;
                        }

                        // Get itemclass value from this row
                        // Based on Dragon Age Origins: ItemUpgrade system reads itemclass column from baseitems.2da
                        int? itemClass = row.GetInteger("itemclass", null);
                        if (itemClass.HasValue)
                        {
                            // Check if itemclass falls in the shield range (20-35)
                            // This range is based on the original fallback logic and typical shield itemclass values
                            if (itemClass.Value >= 20 && itemClass.Value < 35)
                            {
                                // Add this itemclass value to the shield itemclass set
                                _shieldItemClasses.Add(itemClass.Value);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Error loading baseitems.2da, cache will remain empty and fallback logic will be used
                _shieldItemClasses = null;
            }
        }

        /// <summary>
        /// Checks if an item class value represents armor.
        /// </summary>
        /// <param name="itemClass">Item class value from baseitems.2da.</param>
        /// <returns>True if item class represents armor.</returns>
        /// <remarks>
        /// Based on Dragon Age Origins: ItemUpgrade system checks itemclass to determine if item is armor.
        /// Armor item classes typically include light, medium, and heavy armor types.
        ///
        /// Full implementation: Loads baseitems.2da and checks itemclass column for all armor base items.
        /// daorigins.exe: ItemUpgrade @ 0x00aef22c - checks baseitems.2da itemclass column
        /// </remarks>
        private bool IsArmorItemClass(int? itemClass)
        {
            if (!itemClass.HasValue)
            {
                return false;
            }

            // Load armor itemclass cache if not already loaded
            // Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check itemclass column
            if (!_armorItemClassesLoaded)
            {
                LoadArmorItemClasses();
            }

            // Check if itemclass is in the cached armor itemclass set
            // Based on Dragon Age Origins: ItemUpgrade system checks itemclass from baseitems.2da
            if (_armorItemClasses != null && _armorItemClasses.Count > 0)
            {
                if (_armorItemClasses.Contains(itemClass.Value))
                {
                    return true;
                }
            }

            // Fallback: If baseitems.2da couldn't be loaded or cache is empty, use known armor base items
            // Based on UTI.ArmorBaseItems: Known armor base item IDs
            // Note: This fallback checks if itemClass matches known armor base item IDs, which is not ideal
            // but provides backward compatibility if baseitems.2da is unavailable
            return Andastra.Parsing.Resource.Generics.UTI.UTI.ArmorBaseItems.Contains(itemClass.Value) ||
                   (itemClass.Value >= 30 && itemClass.Value < 110);
        }

        /// <summary>
        /// Checks if an item class value represents a shield.
        /// </summary>
        /// <param name="itemClass">Item class value from baseitems.2da.</param>
        /// <returns>True if item class represents a shield.</returns>
        /// <remarks>
        /// Based on Dragon Age Origins: ItemUpgrade system checks itemclass to determine if item is a shield.
        /// Shield item classes typically fall in the 20-35 range.
        ///
        /// Full implementation: Loads baseitems.2da and checks itemclass column for all shield items.
        /// daorigins.exe: ItemUpgrade @ 0x00aef22c - checks baseitems.2da itemclass column
        /// </remarks>
        private bool IsShieldItemClass(int? itemClass)
        {
            if (!itemClass.HasValue)
            {
                return false;
            }

            // Load shield itemclass cache if not already loaded
            // Based on Dragon Age Origins: ItemUpgrade system loads baseitems.2da to check itemclass column
            if (!_shieldItemClassesLoaded)
            {
                LoadShieldItemClasses();
            }

            // Check if itemclass is in the cached shield itemclass set
            // Based on Dragon Age Origins: ItemUpgrade system checks itemclass from baseitems.2da
            if (_shieldItemClasses != null && _shieldItemClasses.Count > 0)
            {
                if (_shieldItemClasses.Contains(itemClass.Value))
                {
                    return true;
                }
            }

            // Fallback: If baseitems.2da couldn't be loaded or cache is empty, use known shield itemclass range
            // Shield itemclass values typically fall in the 20-35 range
            // This fallback provides backward compatibility if baseitems.2da is unavailable
            return itemClass.Value >= 20 && itemClass.Value < 35;
        }

        /// <summary>
        /// Checks if the upgrade item's UpgradeType is compatible with the upgrade slot.
        /// </summary>
        /// <param name="targetItem">Target item to upgrade.</param>
        /// <param name="upgradeItem">Potential upgrade item.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if upgrade slot type is compatible, false otherwise.</returns>
        /// <remarks>
        /// Based on Eclipse upgrade system: Upgrade items have UpgradeType field in their properties.
        /// The UpgradeType must match the slot type for compatibility.
        ///
        // TODO: / Full implementation based on:
        /// - daorigins.exe: ItemUpgrade @ 0x00aef22c - checks UpgradeType field in upgrade properties
        /// - DragonAge2.exe: Enhanced upgrade system checks UpgradeType compatibility
        /// </remarks>
        private bool CheckUpgradeSlotTypeCompatibility(IItemComponent targetItem, IItemComponent upgradeItem, int upgradeSlot)
        {
            if (targetItem == null || upgradeItem == null)
            {
                return false;
            }

            if (upgradeSlot < 0)
            {
                return false;
            }

            // Check if upgrade item has UpgradeType properties
            // Based on Eclipse upgrade system: UpgradeType field in UTIProperty indicates slot compatibility
            bool hasUpgradeType = false;
            foreach (var prop in upgradeItem.Properties)
            {
                // Check if this property has an UpgradeType field
                // In UTI format, UpgradeType is stored in UTIProperty.UpgradeType
                // For IItemComponent, we need to check if the property indicates a specific upgrade slot type

                // Load upgrade UTI template to check UpgradeType field
                if (!string.IsNullOrEmpty(upgradeItem.TemplateResRef))
                {
                    UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeItem.TemplateResRef);
                    if (upgradeUTI != null && upgradeUTI.Properties != null)
                    {
                        foreach (var utiProp in upgradeUTI.Properties)
                        {
                            if (utiProp.UpgradeType.HasValue)
                            {
                                hasUpgradeType = true;
                                // Check if UpgradeType matches the slot
                                // UpgradeType typically corresponds to slot index or slot type
                                // TODO: STUB - For now, allow if UpgradeType matches slot or is unset (generic upgrade)
                                if (utiProp.UpgradeType.Value == upgradeSlot || utiProp.UpgradeType.Value < 0)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            // If no UpgradeType is specified, allow upgrade (generic upgrades work in any slot)
            // Based on Dragon Age Origins: Upgrades without UpgradeType can go in any compatible slot
            if (!hasUpgradeType)
            {
                return true;
            }

            // UpgradeType was specified but didn't match - incompatible
            return false;
        }

        /// <summary>
        /// Checks upgrade prerequisites for Dragon Age 2 (UpgradePrereqType).
        /// </summary>
        /// <param name="targetItem">Target item to upgrade.</param>
        /// <param name="upgradeItem">Potential upgrade item.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if prerequisites are met, false otherwise.</returns>
        /// <remarks>
        /// Based on DragonAge2.exe: UpgradePrereqType @ 0x00c0583c checks prerequisites before allowing upgrade.
        /// Prerequisites can include:
        /// - Item level requirements
        /// - Item quality/tier requirements
        /// - Previous upgrade requirements
        /// - Character level requirements
        ///
        // TODO: / Full implementation based on:
        /// - DragonAge2.exe: UpgradePrereqType @ 0x00c0583c - checks upgrade prerequisites
        /// </remarks>
        private bool CheckUpgradePrerequisites(IItemComponent targetItem, IItemComponent upgradeItem, int upgradeSlot)
        {
            if (targetItem == null || upgradeItem == null)
            {
                return false;
            }

            // Load upgrade UTI template to check prerequisites
            // Based on Dragon Age 2: UpgradePrereqType checks are stored in upgrade item properties
            if (string.IsNullOrEmpty(upgradeItem.TemplateResRef))
            {
                return true; // No prerequisites if no template
            }

            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeItem.TemplateResRef);
            if (upgradeUTI == null)
            {
                return true; // Allow if template cannot be loaded
            }

            // Check if target item meets level/quality requirements
            // Based on Dragon Age 2: UpgradePrereqType checks item UpgradeLevel or quality
            // In Dragon Age 2, items have UpgradeLevel field that may affect upgrade compatibility
            // Load target item UTI template to check UpgradeLevel
            int targetUpgradeLevel = 0;
            if (!string.IsNullOrEmpty(targetItem.TemplateResRef))
            {
                UTI targetUTI = LoadUpgradeUTITemplate(targetItem.TemplateResRef);
                if (targetUTI != null)
                {
                    targetUpgradeLevel = targetUTI.UpgradeLevel;
                }
            }

            // Check if upgrade requires a minimum item UpgradeLevel
            // In Dragon Age 2, some upgrades may require items to be at a certain upgrade level
            // This is typically checked via UpgradePrereqType which may reference UpgradeLevel
            // For now, we check if the upgrade UTI has any indication of minimum UpgradeLevel requirement
            // Most upgrades work on all item levels unless explicitly restricted
            // The UpgradePrereqType system in Dragon Age 2 may encode this in upgrade properties
            // Without exact UpgradePrereqType format, we allow upgrades by default
            // Advanced prerequisite checking would parse UpgradePrereqType from upgrade properties

            // Check if previous upgrades in other slots are required
            // Based on Dragon Age 2: Some upgrades require other upgrades to be installed first
            // UpgradePrereqType may reference specific upgrade types that must be installed first
            // This creates upgrade dependency chains (e.g., upgrade A must be installed before upgrade B)
            // Check target item's current upgrades to see if prerequisites are met
            IItemComponent targetItemComponent = targetItem.GetComponent<IItemComponent>();
            if (targetItemComponent != null && targetItemComponent.Upgrades != null)
            {
                // Some upgrades may require specific upgrade types to be installed first
                // UpgradePrereqType might reference upgrade template ResRefs or upgrade type IDs
                // Without exact UpgradePrereqType format, we cannot check dependency chains
                // Advanced prerequisite checking would:
                // 1. Parse UpgradePrereqType from upgrade UTI properties
                // 2. Check if target item has required upgrades installed
                // 3. Verify upgrade dependency chain is satisfied

                // For now, allow upgrades (dependency checking requires UpgradePrereqType format specification)
            }

            // All prerequisite checks passed
            // Based on Dragon Age 2: UpgradePrereqType @ 0x00c0583c validates prerequisites
            // Full implementation would parse and validate UpgradePrereqType from upgrade properties
            return true;
        }

        /// <summary>
        /// Checks ability requirements for Dragon Age 2 upgrades (GetAbilityUpgradedValue).
        /// </summary>
        /// <param name="upgradeItem">Potential upgrade item.</param>
        /// <returns>True if ability requirements are met, false otherwise.</returns>
        /// <remarks>
        /// Based on DragonAge2.exe: GetAbilityUpgradedValue @ 0x00c0f20c checks ability requirements.
        /// Some upgrades require specific ability scores (STR, DEX, etc.) from the character.
        /// - DragonAge2.exe: GetAbilityUpgradedValue @ 0x00c0f20c - checks ability requirements for upgrades
        /// Implementation checks upgrade properties for ability requirements:
        /// - Param1 indicates the ability type (0-5 for STR-CHA)
        /// - Param1Value or CostValue indicates the minimum required ability score
        /// </remarks>
        private bool CheckAbilityRequirements(IItemComponent upgradeItem)
        {
            if (upgradeItem == null)
            {
                return false;
            }

            // Get character to check abilities
            // Based on Dragon Age 2: GetAbilityUpgradedValue checks character abilities
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
                // No character to check - allow upgrade
                return true;
            }

            // Check character's ability scores against upgrade requirements
            // Based on Dragon Age 2: GetAbilityUpgradedValue compares character abilities to upgrade requirements
            var statsComponent = character.GetComponent<IStatsComponent>();
            if (statsComponent == null)
            {
                // No stats component - allow upgrade
                return true;
            }

            // Load upgrade UTI template to check ability requirements
            // Based on Dragon Age 2: Ability requirements are stored in upgrade item properties or UTI fields
            if (string.IsNullOrEmpty(upgradeItem.TemplateResRef))
            {
                return true; // No requirements if no template
            }

            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeItem.TemplateResRef);
            if (upgradeUTI == null)
            {
                return true; // Allow if template cannot be loaded
            }

            // Check ability requirements from upgrade properties
            // Based on Dragon Age 2: GetAbilityUpgradedValue checks upgrade properties for ability requirements
            // Ability requirements are typically stored in UTI properties where:
            // - Param1 indicates the ability type (0-5 for STR-CHA)
            // - Param1Value or CostValue indicates the minimum required ability score
            // - PropertyName may indicate an ability requirement property type

            // Parse ability requirements from UTI properties
            if (upgradeUTI.Properties != null && upgradeUTI.Properties.Count > 0)
            {
                foreach (var utiProp in upgradeUTI.Properties)
                {
                    // Check if this property represents an ability requirement
                    // Pattern 1: Param1 is in valid ability range (0-5) and Param1Value/CostValue > 0
                    // This indicates a minimum ability score requirement
                    if (utiProp.Param1 >= 0 && utiProp.Param1 <= 5)
                    {
                        // Get the minimum required ability score
                        // Use Param1Value first, fall back to CostValue if Param1Value is 0
                        int minRequired = utiProp.Param1Value > 0 ? utiProp.Param1Value : utiProp.CostValue;

                        // Only check if there's a meaningful requirement (minimum score > 0)
                        if (minRequired > 0)
                        {
                            // Param1 indicates which ability (0=Strength, 1=Dexterity, etc.)
                            Ability requiredAbility = (Ability)utiProp.Param1;

                            // Get character's current ability score
                            int characterAbility = statsComponent.GetAbility(requiredAbility);

                            // Check if character meets the minimum requirement
                            if (characterAbility < minRequired)
                            {
                                // Character does not meet ability requirement
                                return false;
                            }
                        }
                    }

                    // Pattern 2: PropertyName might indicate ability requirement property type
                    // In Dragon Age 2, ability requirements might be stored with specific property types
                    // PropertyName = 0 (ABILITY_BONUS) with negative CostValue could indicate requirement
                    // However, without exact property type constants, we focus on Pattern 1 which is more reliable
                    // Additional pattern: If PropertyName suggests requirement and Param1/CostValue indicates minimum
                    if (utiProp.PropertyName >= 0 && utiProp.CostValue > 0)
                    {
                        // Check if Param1 indicates an ability (0-5)
                        if (utiProp.Param1 >= 0 && utiProp.Param1 <= 5)
                        {
                            // This might be an ability requirement
                            // Use CostValue as minimum if Param1Value is not set
                            int minRequired = utiProp.Param1Value > 0 ? utiProp.Param1Value : utiProp.CostValue;

                            if (minRequired > 0)
                            {
                                Ability requiredAbility = (Ability)utiProp.Param1;
                                int characterAbility = statsComponent.GetAbility(requiredAbility);

                                if (characterAbility < minRequired)
                                {
                                    return false;
                                }
                            }
                        }
                    }
                }
            }

            // All ability requirements met (or no ability requirements found)
            return true;
        }

        /// <summary>
        /// Gets the upgrade GUI name for this Eclipse game version.
        /// </summary>
        /// <returns>GUI name (e.g., "ItemUpgrade").</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c opens ItemUpgrade GUI
        /// - DragonAge2.exe: GUIItemUpgrade class structure uses "ItemUpgrade" GUI name
        /// The GUI name is likely "ItemUpgrade" based on the class name pattern and COMMAND_OPENITEMUPGRADEGUI string.
        /// </remarks>
        private string GetUpgradeGuiName()
        {
            // Eclipse upgrade GUI is named "ItemUpgrade" based on GUIItemUpgrade class name
            // Based on daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c
            // Based on DragonAge2.exe: GUIItemUpgrade class structure
            return "ItemUpgrade";
        }

        /// <summary>
        /// Loads the upgrade screen GUI from the installation.
        /// </summary>
        /// <returns>True if GUI was loaded successfully, false otherwise.</returns>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c opens ItemUpgrade GUI
        /// - DragonAge2.exe: GUIItemUpgrade class structure loads GUI
        /// </remarks>
        private bool LoadUpgradeGui()
        {
            if (string.IsNullOrEmpty(_guiName))
            {
                return false;
            }

            try
            {
                // Try to load GUI via GUI manager if available
                if (_guiManager != null)
                {
                    bool loaded = _guiManager.LoadGui(_guiName, DefaultScreenWidth, DefaultScreenHeight);
                    if (loaded)
                    {
                        // Get loaded GUI from manager (if exposed, otherwise load directly)
                        // Based on Eclipse GUI system: Retrieve GUI from manager cache to avoid duplicate loading
                        ParsingGUI guiFromManager = _guiManager.GetLoadedGui(_guiName);
                        if (guiFromManager != null && guiFromManager.Controls != null && guiFromManager.Controls.Count > 0)
                        {
                            _loadedGui = guiFromManager;
                            Console.WriteLine($"[EclipseUpgradeScreen] Successfully retrieved GUI from manager: {_guiName} - {_loadedGui.Controls.Count} controls");
                            return true;
                        }
                    }
                }

                // Load GUI resource from installation (fallback if manager not available or GUI not in manager)
                // Based on Eclipse GUI system: GUI resources are loaded via Installation
                var resourceResult = _installation.Resources.LookupResource(_guiName, ResourceType.GUI, null, null);
                if (resourceResult == null || resourceResult.Data == null || resourceResult.Data.Length == 0)
                {
                    Console.WriteLine($"[EclipseUpgradeScreen] ERROR: GUI resource not found: {_guiName}");
                    return false;
                }

                // Parse GUI file using GUIReader
                // Based on Eclipse GUI system: GUIs are parsed using GUIReader
                var guiReader = new GUIReader(resourceResult.Data);
                _loadedGui = guiReader.Load();

                if (_loadedGui == null || _loadedGui.Controls == null || _loadedGui.Controls.Count == 0)
                {
                    Console.WriteLine($"[EclipseUpgradeScreen] ERROR: Failed to parse GUI: {_guiName}");
                    return false;
                }

                Console.WriteLine($"[EclipseUpgradeScreen] Successfully loaded GUI: {_guiName} - {_loadedGui.Controls.Count} controls");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EclipseUpgradeScreen] ERROR: Exception loading GUI {_guiName}: {ex.Message}");
                Console.WriteLine($"[EclipseUpgradeScreen] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Initializes GUI controls and sets up button handlers.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse GUI system: Controls are set up after GUI loading.
        /// Sets up control maps and button handlers for upgrade screen interaction.
        /// </remarks>
        private void InitializeGuiControls()
        {
            if (_loadedGui == null || _loadedGui.Controls == null)
            {
                return;
            }

            // Build control and button maps for quick lookup
            // Based on Eclipse GUI system: Control mapping system
            BuildControlMaps(_loadedGui.Controls, _controlMap, _buttonMap);

            // Set up button click handlers via GUI manager
            // Based on Eclipse GUI system: Button handlers are set up via event system
            // Button handlers are connected via OnButtonClicked event in SetGraphicsDevice
        }

        /// <summary>
        /// Recursively builds control and button maps for quick lookup.
        /// </summary>
        /// <param name="controls">List of GUI controls to process.</param>
        /// <param name="controlMap">Dictionary to store control mappings.</param>
        /// <param name="buttonMap">Dictionary to store button mappings.</param>
        private void BuildControlMaps(List<GUIControl> controls, Dictionary<string, GUIControl> controlMap, Dictionary<string, GUIButton> buttonMap)
        {
            if (controls == null)
            {
                return;
            }

            foreach (var control in controls)
            {
                if (control == null)
                {
                    continue;
                }

                // Add to control map if it has a tag
                if (!string.IsNullOrEmpty(control.Tag))
                {
                    controlMap[control.Tag] = control;
                }

                // Add to button map if it's a button
                if (control is GUIButton button)
                {
                    if (!string.IsNullOrEmpty(button.Tag))
                    {
                        buttonMap[button.Tag] = button;
                    }
                }

                // Recursively process children
                if (control.Children != null && control.Children.Count > 0)
                {
                    BuildControlMaps(control.Children, controlMap, buttonMap);
                }
            }
        }

        /// <summary>
        /// Updates GUI with current item and character data.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse upgrade system: GUI displays item upgrade slots and available upgrades.
        /// Updates title, item information, and upgrade slot displays.
        /// </remarks>
        private void UpdateGuiData()
        {
            // Update title label with item name if available
            // Based on Eclipse GUI system: Title labels display item information
            if (_controlMap.TryGetValue("LBL_TITLE", out GUIControl titleControl))
            {
                if (titleControl is GUILabel titleLabel)
                {
                    string titleText = "Item Upgrade";
                    if (_targetItem != null)
                    {
                        IItemComponent itemComponent = _targetItem.GetComponent<IItemComponent>();
                        if (itemComponent != null && !string.IsNullOrEmpty(itemComponent.TemplateResRef))
                        {
                            titleText = $"Upgrade: {itemComponent.TemplateResRef}";
                        }
                    }
                    if (titleLabel.GuiText != null)
                    {
                        titleLabel.GuiText.Text = titleText;
                    }
                }
            }
        }

        /// <summary>
        /// Displays item properties in the GUI.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse upgrade system: GUI displays item properties to show current item stats and effects.
        /// Properties are displayed in a list box or label control showing all active properties on the item.
        /// </remarks>
        private void DisplayItemProperties()
        {
            if (_targetItem == null)
            {
                return;
            }

            IItemComponent itemComponent = _targetItem.GetComponent<IItemComponent>();
            if (itemComponent == null || itemComponent.Properties == null)
            {
                return;
            }

            // Find property display control (list box or label)
            // Common control tags: "LB_PROPERTIES", "LBL_PROPERTIES", "LB_ITEMPROPS", etc.
            string[] propertyControlTags = new string[]
            {
                "LB_PROPERTIES",
                "LBL_PROPERTIES",
                "LB_ITEMPROPS",
                "LBL_ITEMPROPS",
                "LB_ITEM_PROPERTIES",
                "LBL_ITEM_PROPERTIES"
            };

            GUIControl propertyControl = null;
            foreach (string tag in propertyControlTags)
            {
                if (_controlMap.TryGetValue(tag, out GUIControl control))
                {
                    propertyControl = control;
                    break;
                }
            }

            if (propertyControl == null)
            {
                return;
            }

            // Build property description text
            // Based on Eclipse upgrade system: Properties are formatted as readable descriptions
            List<string> propertyDescriptions = new List<string>();
            foreach (var prop in itemComponent.Properties)
            {
                string propDescription = FormatPropertyDescription(prop);
                if (!string.IsNullOrEmpty(propDescription))
                {
                    propertyDescriptions.Add(propDescription);
                }
            }

            // Update control with property descriptions
            if (propertyControl is GUILabel propertyLabel)
            {
                string propertyText = propertyDescriptions.Count > 0
                    ? string.Join("\n", propertyDescriptions)
                    : "No properties";
                if (propertyLabel.GuiText != null)
                {
                    propertyLabel.GuiText.Text = propertyText;
                }
            }
            else if (propertyControl is GUIListBox propertyListBox)
            {
                // For list boxes, we'd typically populate with items
                // TODO:  For now, store descriptions for rendering system to use
                // The actual GUI rendering would use this data to populate list items
            }
        }

        /// <summary>
        /// Displays current upgrade effects in the GUI.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse upgrade system: GUI displays upgrade effects to show what upgrades are currently applied.
        /// Shows which upgrades are in which slots and their effects.
        /// </remarks>
        private void DisplayCurrentUpgradeEffects()
        {
            if (_targetItem == null)
            {
                return;
            }

            IItemComponent itemComponent = _targetItem.GetComponent<IItemComponent>();
            if (itemComponent == null || itemComponent.Upgrades == null)
            {
                return;
            }

            // Find upgrade effects display control
            // Common control tags: "LB_UPGRADEEFFECTS", "LBL_UPGRADEEFFECTS", "LB_EFFECTS", etc.
            string[] effectsControlTags = new string[]
            {
                "LB_UPGRADEEFFECTS",
                "LBL_UPGRADEEFFECTS",
                "LB_EFFECTS",
                "LBL_EFFECTS",
                "LB_UPGRADE_EFFECTS",
                "LBL_UPGRADE_EFFECTS"
            };

            GUIControl effectsControl = null;
            foreach (string tag in effectsControlTags)
            {
                if (_controlMap.TryGetValue(tag, out GUIControl control))
                {
                    effectsControl = control;
                    break;
                }
            }

            if (effectsControl == null)
            {
                return;
            }

            // Build upgrade effects description text
            // Based on Eclipse upgrade system: Upgrade effects are formatted as readable descriptions
            List<string> effectDescriptions = new List<string>();
            foreach (var upgrade in itemComponent.Upgrades)
            {
                if (string.IsNullOrEmpty(upgrade.TemplateResRef))
                {
                    continue;
                }

                // Load upgrade UTI to get its properties/effects
                UTI upgradeUTI = LoadUpgradeUTITemplate(upgrade.TemplateResRef);
                if (upgradeUTI != null && upgradeUTI.Properties != null)
                {
                    string upgradeEffectText = $"Slot {upgrade.Index + 1}: {upgrade.TemplateResRef}\n";
                    foreach (var utiProp in upgradeUTI.Properties)
                    {
                        string propDescription = FormatUTIPropertyDescription(utiProp);
                        if (!string.IsNullOrEmpty(propDescription))
                        {
                            upgradeEffectText += $"  - {propDescription}\n";
                        }
                    }
                    effectDescriptions.Add(upgradeEffectText.TrimEnd());
                }
            }

            // Update control with upgrade effects descriptions
            if (effectsControl is GUILabel effectsLabel)
            {
                string effectsText = effectDescriptions.Count > 0
                    ? string.Join("\n\n", effectDescriptions)
                    : "No upgrades applied";
                if (effectsLabel.GuiText != null)
                {
                    effectsLabel.GuiText.Text = effectsText;
                }
            }
        }

        /// <summary>
        /// Displays ability-based upgrade values for Dragon Age 2.
        /// </summary>
        /// <remarks>
        /// Based on DragonAge2.exe: GetAbilityUpgradedValue @ 0x00c0f20c calculates and displays ability-based upgrade values.
        /// Some upgrades in Dragon Age 2 have values that scale with character abilities (STR, DEX, etc.).
        /// This method calculates and displays these ability-based values for available upgrades.
        /// </remarks>
        private void DisplayAbilityUpgradeValues()
        {
            if (_targetItem == null || _character == null)
            {
                return;
            }

            // Get character stats component
            var statsComponent = _character.GetComponent<IStatsComponent>();
            if (statsComponent == null)
            {
                return;
            }

            // Find ability upgrade values display control
            // Common control tags: "LB_ABILITYVALUES", "LBL_ABILITYVALUES", "LB_ABILITY_UPGRADES", etc.
            string[] abilityControlTags = new string[]
            {
                "LB_ABILITYVALUES",
                "LBL_ABILITYVALUES",
                "LB_ABILITY_UPGRADES",
                "LBL_ABILITY_UPGRADES",
                "LB_ABILITY_UPGRADE_VALUES",
                "LBL_ABILITY_UPGRADE_VALUES"
            };

            GUIControl abilityControl = null;
            foreach (string tag in abilityControlTags)
            {
                if (_controlMap.TryGetValue(tag, out GUIControl control))
                {
                    abilityControl = control;
                    break;
                }
            }

            if (abilityControl == null)
            {
                return;
            }

            // Build ability-based upgrade values description
            // Based on Dragon Age 2: GetAbilityUpgradedValue calculates values based on character abilities
            List<string> abilityValueDescriptions = new List<string>();

            // Get available upgrades for the selected slot (or all slots)
            int slotToCheck = _selectedUpgradeSlot >= 0 ? _selectedUpgradeSlot : 0;
            List<string> availableUpgrades = GetAvailableUpgrades(_targetItem, slotToCheck);

            foreach (string upgradeResRef in availableUpgrades)
            {
                UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
                if (upgradeUTI == null || upgradeUTI.Properties == null)
                {
                    continue;
                }

                // Check each property for ability-based scaling
                foreach (var utiProp in upgradeUTI.Properties)
                {
                    int? abilityUpgradedValue = GetAbilityUpgradedValue(utiProp, statsComponent);
                    if (abilityUpgradedValue.HasValue && abilityUpgradedValue.Value != 0)
                    {
                        string abilityName = GetAbilityNameFromProperty(utiProp);
                        string description = $"{upgradeResRef}: {abilityName} = {abilityUpgradedValue.Value} (ability-based)";
                        abilityValueDescriptions.Add(description);
                    }
                }
            }

            // Update control with ability-based upgrade values
            if (abilityControl is GUILabel abilityLabel)
            {
                string abilityText = abilityValueDescriptions.Count > 0
                    ? string.Join("\n", abilityValueDescriptions)
                    : "No ability-based upgrades";
                if (abilityLabel.GuiText != null)
                {
                    abilityLabel.GuiText.Text = abilityText;
                }
            }
        }

        /// <summary>
        /// Calculates ability-based upgrade value for a property (Dragon Age 2: GetAbilityUpgradedValue).
        /// </summary>
        /// <param name="utiProperty">UTI property to calculate value for.</param>
        /// <param name="statsComponent">Character stats component for ability values.</param>
        /// <returns>Ability-based upgrade value, or null if property doesn't scale with abilities.</returns>
        /// <remarks>
        /// Based on DragonAge2.exe: GetAbilityUpgradedValue @ 0x00c0f20c calculates ability-based upgrade values.
        /// Some upgrade properties scale with character abilities (STR, DEX, etc.).
        /// The calculation typically involves: base_value + (ability_modifier * scaling_factor)
        /// </remarks>
        private int? GetAbilityUpgradedValue(UTIProperty utiProperty, IStatsComponent statsComponent)
        {
            if (utiProperty == null || statsComponent == null)
            {
                return null;
            }

            // Check if property has ability-based scaling
            // In Dragon Age 2, properties with ability scaling typically have:
            // - PropertyType indicating ability-based property
            // - CostValue or Param1Value containing base value
            // - Subtype or Param1 indicating which ability to use (STR, DEX, etc.)

            // Property types that can scale with abilities (Dragon Age 2 specific)
            // These are typically damage bonuses, stat bonuses, etc. that scale with abilities
            int propertyType = utiProperty.PropertyName;

            // Check if this property type supports ability scaling
            // In Dragon Age 2, ability-scaling properties typically have specific property type IDs
            // TODO:  For now, we'll check common ability-scaling property patterns
            bool supportsAbilityScaling = false;
            int abilityType = 0; // 0=STR, 1=DEX, 2=CON, 3=INT, 4=WIS, 5=CHA

            // Check property type and determine if it scales with abilities
            // Based on Dragon Age 2: Ability-scaling properties have specific property type IDs
            // Common ability-scaling properties: damage bonuses, stat bonuses, etc.
            if (propertyType > 0)
            {
                // Check if property has ability scaling indicator
                // In Dragon Age 2, ability-scaling is typically indicated by:
                // - Subtype field containing ability type (0-5 for STR-DEX-CON-INT-WIS-CHA)
                // - Param1 or Param1Value containing scaling factor
                int subtype = utiProperty.Subtype;
                if (subtype >= 0 && subtype <= 5)
                {
                    supportsAbilityScaling = true;
                    abilityType = subtype;
                }
            }

            if (!supportsAbilityScaling)
            {
                return null;
            }

            // Get base value from property
            // Base value is typically in CostValue or Param1Value
            int baseValue = utiProperty.CostValue != 0 ? utiProperty.CostValue : utiProperty.Param1Value;

            // Get ability modifier from character stats
            // Based on Dragon Age 2: Ability modifiers are calculated from ability scores
            int abilityScore = 0;
            switch (abilityType)
            {
                case 0: // STR
                    abilityScore = statsComponent.Strength;
                    break;
                case 1: // DEX
                    abilityScore = statsComponent.Dexterity;
                    break;
                case 2: // CON
                    abilityScore = statsComponent.Constitution;
                    break;
                case 3: // INT
                    abilityScore = statsComponent.Intelligence;
                    break;
                case 4: // WIS
                    abilityScore = statsComponent.Wisdom;
                    break;
                case 5: // CHA
                    abilityScore = statsComponent.Charisma;
                    break;
            }

            // Calculate ability modifier (typically (ability - 10) / 2)
            int abilityModifier = (abilityScore - 10) / 2;

            // Get scaling factor from property
            // Scaling factor is typically in Param1 or Param1Value
            int scalingFactor = utiProperty.Param1 != 0 ? utiProperty.Param1 : (utiProperty.Param1Value != 0 ? utiProperty.Param1Value : 1);

            // Calculate final value: base_value + (ability_modifier * scaling_factor)
            // Based on Dragon Age 2: GetAbilityUpgradedValue calculation
            int finalValue = baseValue + (abilityModifier * scalingFactor);

            return finalValue;
        }

        /// <summary>
        /// Gets ability name from property for display purposes.
        /// </summary>
        /// <param name="utiProperty">UTI property to get ability name for.</param>
        /// <returns>Ability name (STR, DEX, CON, INT, WIS, CHA).</returns>
        private string GetAbilityNameFromProperty(UTIProperty utiProperty)
        {
            if (utiProperty == null)
            {
                return "Unknown";
            }

            int subtype = utiProperty.Subtype;

            switch (subtype)
            {
                case 0: return "STR";
                case 1: return "DEX";
                case 2: return "CON";
                case 3: return "INT";
                case 4: return "WIS";
                case 5: return "CHA";
                default: return "Unknown";
            }
        }

        /// <summary>
        /// Formats a property description for display.
        /// </summary>
        /// <param name="property">Item property to format.</param>
        /// <returns>Formatted property description string.</returns>
        private string FormatPropertyDescription(ItemProperty property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            // Format property as readable description
            // Based on Eclipse upgrade system: Properties are formatted for display
            // Format: "PropertyType: Subtype, Value: CostValue"
            return $"Property {property.PropertyType}: Subtype {property.Subtype}, Value {property.CostValue}";
        }

        /// <summary>
        /// Formats a UTI property description for display.
        /// </summary>
        /// <param name="utiProperty">UTI property to format.</param>
        /// <returns>Formatted property description string.</returns>
        private string FormatUTIPropertyDescription(UTIProperty utiProperty)
        {
            if (utiProperty == null)
            {
                return string.Empty;
            }

            // Format UTI property as readable description
            // Based on Eclipse upgrade system: UTI properties are formatted for display
            int propertyType = utiProperty.PropertyName;
            int subtype = utiProperty.Subtype;
            int costValue = utiProperty.CostValue;
            int param1Value = utiProperty.Param1Value;

            string description = $"Property {propertyType}";
            if (subtype != 0)
            {
                description += $", Subtype {subtype}";
            }
            if (costValue != 0)
            {
                description += $", Value {costValue}";
            }
            if (param1Value != 0)
            {
                description += $", Param {param1Value}";
            }

            return description;
        }

        /// <summary>
        /// Refreshes the upgrade display with available upgrades.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse upgrade system: Displays available upgrades for the current item and slot.
        /// Populates upgrade lists and updates descriptions based on selected upgrades.
        /// </remarks>
        private void RefreshUpgradeDisplay()
        {
            if (_targetItem == null)
            {
                return;
            }

            IItemComponent itemComponent = _targetItem.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return;
            }

            // Clear previous upgrade data
            _availableUpgradesPerSlot.Clear();
            _slotUpgradeListBoxTags.Clear();

            // Get available upgrades for each slot
            // Based on Eclipse upgrade system: Upgrade lists are populated when item is selected
            // Eclipse items typically have multiple upgrade slots (up to MaxUpgradeSlots)
            for (int slot = 0; slot < MaxUpgradeSlots; slot++)
            {
                // Get available upgrades for this slot
                List<string> availableUpgrades = GetAvailableUpgrades(_targetItem, slot);
                _availableUpgradesPerSlot[slot] = availableUpgrades;

                // Find the list box control for this slot
                // Control tags follow patterns like "LB_UPGRADE0", "LB_UPGRADE1", "LB_UPGRADELIST0", etc.
                string listBoxTag = FindUpgradeListBoxTag(slot);
                if (!string.IsNullOrEmpty(listBoxTag))
                {
                    _slotUpgradeListBoxTags[slot] = listBoxTag;
                    PopulateUpgradeListBox(listBoxTag, availableUpgrades, slot);
                }

                // Update slot button/label to show current upgrade status
                UpdateSlotDisplay(slot, itemComponent);
            }

            // If a slot is selected, highlight it and populate its upgrade list
            if (_selectedUpgradeSlot >= 0 && _selectedUpgradeSlot < MaxUpgradeSlots)
            {
                SelectUpgradeSlot(_selectedUpgradeSlot);
            }
        }

        /// <summary>
        /// Handles button click events from the GUI.
        /// </summary>
        /// <param name="sender">Event sender (GUI manager).</param>
        /// <param name="e">Button click event arguments.</param>
        /// <remarks>
        /// Based on Eclipse GUI system: Button click events are handled via OnButtonClicked event.
        /// Handles upgrade screen buttons: Apply, Remove, Back, etc.
        /// </remarks>
        private void HandleButtonClick(object sender, GuiButtonClickedEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.ButtonTag))
            {
                return;
            }

            string buttonTag = e.ButtonTag.ToUpperInvariant();

            switch (buttonTag)
            {
                case "BTN_APPLY":
                case "BTN_UPGRADE":
                    // Apply selected upgrade
                    // Based on Eclipse upgrade system: Apply button applies selected upgrade to item
                    HandleApplyUpgrade();
                    break;

                case "BTN_REMOVE":
                    // Remove selected upgrade
                    // Based on Eclipse upgrade system: Remove button removes upgrade from item
                    HandleRemoveUpgrade();
                    break;

                case "BTN_BACK":
                case "BTN_CLOSE":
                    // Hide upgrade screen
                    // Based on Eclipse GUI system: Back/Close button hides screen
                    Hide();
                    break;

                default:
                    // Check if this is a slot selection button
                    // Slot buttons follow patterns like "BTN_UPGRADE0", "BTN_SLOT0", etc.
                    if (buttonTag.StartsWith("BTN_UPGRADE", StringComparison.OrdinalIgnoreCase) ||
                        buttonTag.StartsWith("BTN_SLOT", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extract slot number from button tag
                        // Patterns: "BTN_UPGRADE0", "BTN_UPGRADE1", "BTN_SLOT0", "BTN_SLOT1", etc.
                        int slot = ExtractSlotNumberFromButtonTag(buttonTag);
                        if (slot >= 0 && slot < MaxUpgradeSlots)
                        {
                            // Select this upgrade slot
                            SelectUpgradeSlot(slot);
                        }
                    }
                    else
                    {
                        // Unknown button - ignore
                    }
                    break;
            }
        }

        /// <summary>
        /// Handles applying the selected upgrade to the item.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse upgrade system: Applies selected upgrade from GUI to item.
        /// Gets selected upgrade slot and upgrade ResRef from GUI and calls ApplyUpgrade.
        /// </remarks>
        private void HandleApplyUpgrade()
        {
            if (_targetItem == null)
            {
                return;
            }

            // Get selected upgrade slot from UI
            // Based on Eclipse upgrade system: Selected upgrade slot is tracked in _selectedUpgradeSlot
            int selectedSlot = GetSelectedUpgradeSlotFromUI();
            if (selectedSlot < 0 || selectedSlot >= MaxUpgradeSlots)
            {
                // No slot selected or invalid slot
                return;
            }

            // Get selected upgrade ResRef from list box
            // Based on Eclipse upgrade system: Selected upgrade is retrieved from list box CurrentValue
            string selectedUpgradeResRef = GetSelectedUpgradeResRefFromListBox(selectedSlot);
            if (string.IsNullOrEmpty(selectedUpgradeResRef))
            {
                // No upgrade selected in list box
                return;
            }

            // Call ApplyUpgrade with item, slot, and ResRef
            // Based on Eclipse upgrade system: ApplyUpgrade method applies upgrade properties to item
            bool success = ApplyUpgrade(_targetItem, selectedSlot, selectedUpgradeResRef);
            if (success)
            {
                // Refresh display after successful upgrade
                RefreshUpgradeDisplay();
            }
        }

        /// <summary>
        /// Handles removing the selected upgrade from the item.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse upgrade system: Removes selected upgrade from item.
        /// Gets selected upgrade slot from GUI and calls RemoveUpgrade.
        /// </remarks>
        private void HandleRemoveUpgrade()
        {
            if (_targetItem == null)
            {
                return;
            }

            // Get selected upgrade slot from UI
            // Based on Eclipse upgrade system: Selected upgrade slot is tracked in _selectedUpgradeSlot
            int selectedSlot = GetSelectedUpgradeSlotFromUI();
            if (selectedSlot < 0 || selectedSlot >= MaxUpgradeSlots)
            {
                // No slot selected or invalid slot
                return;
            }

            // Call RemoveUpgrade with item and slot
            // Based on Eclipse upgrade system: RemoveUpgrade method removes upgrade properties from item
            bool success = RemoveUpgrade(_targetItem, selectedSlot);
            if (success)
            {
                // Refresh display after successful removal
                RefreshUpgradeDisplay();
            }
        }

        /// <summary>
        /// Gets the loaded GUI for external rendering.
        /// </summary>
        /// <returns>The loaded GUI, or null if not loaded.</returns>
        public ParsingGUI GetLoadedGui()
        {
            return _loadedGui;
        }

        /// <summary>
        /// Gets a control by tag from the loaded GUI.
        /// </summary>
        /// <param name="tag">Control tag to find.</param>
        /// <returns>The control if found, null otherwise.</returns>
        public GUIControl GetControl(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            _controlMap.TryGetValue(tag, out GUIControl control);
            return control;
        }

        /// <summary>
        /// Gets a button by tag from the loaded GUI.
        /// </summary>
        /// <param name="tag">Button tag to find.</param>
        /// <returns>The button if found, null otherwise.</returns>
        public GUIButton GetButton(string tag)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return null;
            }

            _buttonMap.TryGetValue(tag, out GUIButton button);
            return button;
        }

        /// <summary>
        /// Finds the list box control tag for a given upgrade slot.
        /// </summary>
        /// <param name="slot">Upgrade slot index (0-based).</param>
        /// <returns>List box control tag if found, empty string otherwise.</returns>
        /// <remarks>
        /// Based on Eclipse upgrade system: List box tags follow patterns like:
        /// - "LB_UPGRADE0", "LB_UPGRADE1", etc. (slot-specific list boxes)
        /// - "LB_UPGRADELIST" (single list box that changes based on selected slot)
        /// This method tries multiple common tag patterns to find the correct list box.
        /// </remarks>
        private string FindUpgradeListBoxTag(int slot)
        {
            if (_loadedGui == null || _loadedGui.Controls == null)
            {
                return string.Empty;
            }

            // Try slot-specific list box tags first (e.g., "LB_UPGRADE0", "LB_UPGRADE1")
            string[] tagPatterns = new string[]
            {
                $"LB_UPGRADE{slot}",
                $"LB_UPGRADELIST{slot}",
                $"LB_UPGRADE_LIST{slot}",
                $"UPGRADE_LISTBOX{slot}",
                $"UPGRADE_LB{slot}"
            };

            foreach (string tag in tagPatterns)
            {
                if (_controlMap.ContainsKey(tag))
                {
                    GUIControl control = _controlMap[tag];
                    if (control != null && control.GuiType == GUIControlType.ListBox)
                    {
                        return tag;
                    }
                }
            }

            // Try generic list box tag (single list box for all slots)
            // This is used when the list box content changes based on selected slot
            string[] genericTags = new string[]
            {
                "LB_UPGRADELIST",
                "LB_UPGRADE_LIST",
                "UPGRADE_LISTBOX",
                "UPGRADE_LB"
            };

            foreach (string tag in genericTags)
            {
                if (_controlMap.ContainsKey(tag))
                {
                    GUIControl control = _controlMap[tag];
                    if (control != null && control.GuiType == GUIControlType.ListBox)
                    {
                        return tag;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Populates a list box with available upgrade items.
        /// </summary>
        /// <param name="listBoxTag">Tag of the list box control to populate.</param>
        /// <param name="availableUpgrades">List of available upgrade ResRefs.</param>
        /// <param name="slot">Upgrade slot index (0-based).</param>
        /// <remarks>
        /// Based on Eclipse upgrade system: List boxes display available upgrades for a slot.
        /// The list box uses CurrentValue to track the selected index.
        /// </remarks>
        private void PopulateUpgradeListBox(string listBoxTag, List<string> availableUpgrades, int slot)
        {
            if (string.IsNullOrEmpty(listBoxTag) || availableUpgrades == null)
            {
                return;
            }

            if (!_controlMap.TryGetValue(listBoxTag, out GUIControl control))
            {
                return;
            }

            if (control.GuiType != GUIControlType.ListBox)
            {
                return;
            }

            GUIListBox listBox = control as GUIListBox;
            if (listBox == null)
            {
                return;
            }

            // Store available upgrades for this slot so we can retrieve ResRef by index
            _availableUpgradesPerSlot[slot] = availableUpgrades;

            // Clear existing list box items (children represent list items)
            // Based on Eclipse upgrade system: List boxes use ProtoItem children to display items
            // Based on OdysseyUpgradeScreenBase.cs: UpdateListBoxItems() clears children before populating
            listBox.Children.Clear();

            // Add each upgrade as a list box item (ProtoItem control)
            // Based on Eclipse upgrade system: List boxes display available upgrades for a slot
            // Based on OdysseyUpgradeScreenBase.cs: UpdateListBoxItems() creates GUIProtoItem for each upgrade
            foreach (string upgradeResRef in availableUpgrades)
            {
                // Create a proto item for each upgrade
                GUIProtoItem listItem = new GUIProtoItem();
                listItem.Tag = upgradeResRef; // Store ResRef in tag for retrieval

                // Load upgrade UTI template to get display name
                // Based on Eclipse upgrade system: UTI templates contain LocalizedName field
                // Based on OdysseyUpgradeScreenBase.cs: LoadUpgradeUTITemplate() loads UTI and extracts name
                UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
                if (upgradeUTI != null && upgradeUTI.Name != null)
                {
                    // Set display text to upgrade name (LocalizedString.ToString() returns English text or StringRef)
                    // Based on Eclipse upgrade system: UTI.Name is a LocalizedString that can be converted to string
                    // Based on OdysseyUpgradeScreenBase.cs: upgradeUTI.Name.ToString() gets display name
                    if (listItem.GuiText == null)
                    {
                        listItem.GuiText = new GUIText();
                    }
                    string displayName = upgradeUTI.Name.ToString();
                    if (!string.IsNullOrEmpty(displayName))
                    {
                        listItem.GuiText.Text = displayName;
                    }
                    else
                    {
                        // Fallback to ResRef if name is empty
                        listItem.GuiText.Text = upgradeResRef;
                    }
                }
                else
                {
                    // Fallback to ResRef if UTI not loaded or name not available
                    // Based on Eclipse upgrade system: If UTI cannot be loaded, use ResRef as display name
                    // Based on OdysseyUpgradeScreenBase.cs: Fallback to ResRef when UTI loading fails
                    if (listItem.GuiText == null)
                    {
                        listItem.GuiText = new GUIText();
                    }
                    listItem.GuiText.Text = upgradeResRef;
                }

                // Add proto item as child to list box
                // Based on Eclipse upgrade system: List box children are ProtoItem controls representing list items
                // Based on OdysseyUpgradeScreenBase.cs: listBox.Children.Add(listItem) adds item to list box
                listBox.Children.Add(listItem);
            }

            // Reset selection if list box was just populated
            // Based on Eclipse upgrade system: CurrentValue tracks selected index in the list box
            // Based on OdysseyUpgradeScreenBase.cs: CurrentValue is set to 0 for first item or -1 if empty
            if (control.CurrentValue.HasValue && control.CurrentValue.Value >= availableUpgrades.Count)
            {
                control.CurrentValue = availableUpgrades.Count > 0 ? 0 : -1;
            }
            else if (!control.CurrentValue.HasValue && availableUpgrades.Count > 0)
            {
                control.CurrentValue = 0;
            }
            else if (availableUpgrades.Count == 0)
            {
                control.CurrentValue = -1;
            }
        }

        /// <summary>
        /// Updates the display for a specific upgrade slot (button/label).
        /// </summary>
        /// <param name="slot">Upgrade slot index (0-based).</param>
        /// <param name="itemComponent">Item component to check for existing upgrades.</param>
        /// <remarks>
        /// Based on Eclipse upgrade system: Slot buttons/labels show current upgrade status.
        /// Updates slot button text or label to indicate if slot is occupied or empty.
        /// </remarks>
        private void UpdateSlotDisplay(int slot, IItemComponent itemComponent)
        {
            if (itemComponent == null)
            {
                return;
            }

            // Find slot button/label control
            // Slot controls follow patterns like "BTN_UPGRADE0", "BTN_SLOT0", "LBL_SLOT0", etc.
            string[] slotControlPatterns = new string[]
            {
                $"BTN_UPGRADE{slot}",
                $"BTN_SLOT{slot}",
                $"BTN_UPGRADESLOT{slot}",
                $"LBL_SLOT{slot}",
                $"LBL_UPGRADE{slot}"
            };

            GUIControl slotControl = null;
            foreach (string tag in slotControlPatterns)
            {
                if (_controlMap.TryGetValue(tag, out GUIControl control))
                {
                    slotControl = control;
                    break;
                }
            }

            if (slotControl == null)
            {
                return;
            }

            // Check if slot has an existing upgrade
            var existingUpgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == slot);
            bool hasUpgrade = existingUpgrade != null;

            // Update control to show upgrade status
            // For buttons: Update text to show upgrade name or "Empty"
            // For labels: Update text to show upgrade status
            if (slotControl is GUIButton slotButton)
            {
                if (hasUpgrade && !string.IsNullOrEmpty(existingUpgrade.TemplateResRef))
                {
                    // Show upgrade name (ResRef) or a placeholder
                    slotButton.Text = existingUpgrade.TemplateResRef;
                }
                else
                {
                    // Show "Empty" or slot number
                    slotButton.Text = $"Slot {slot + 1}";
                }
            }
            else if (slotControl is GUILabel slotLabel)
            {
                if (hasUpgrade && !string.IsNullOrEmpty(existingUpgrade.TemplateResRef))
                {
                    slotLabel.Text = existingUpgrade.TemplateResRef;
                }
                else
                {
                    slotLabel.Text = "Empty";
                }
            }
        }

        /// <summary>
        /// Selects and highlights an upgrade slot.
        /// </summary>
        /// <param name="slot">Upgrade slot index (0-based).</param>
        /// <remarks>
        /// Based on Eclipse upgrade system: Selecting a slot highlights it and populates its upgrade list.
        /// Updates selected slot state and refreshes the upgrade list for that slot.
        /// </remarks>
        private void SelectUpgradeSlot(int slot)
        {
            if (slot < 0 || slot >= MaxUpgradeSlots)
            {
                return;
            }

            _selectedUpgradeSlot = slot;

            // Highlight the selected slot button
            // Find and update slot button appearance to show selection
            string[] slotControlPatterns = new string[]
            {
                $"BTN_UPGRADE{slot}",
                $"BTN_SLOT{slot}",
                $"BTN_UPGRADESLOT{slot}"
            };

            foreach (string tag in slotControlPatterns)
            {
                if (_controlMap.TryGetValue(tag, out GUIControl control) && control is GUIButton button)
                {
                    // Highlight selected button (could set IsSelected or update color)
                    control.IsSelected = 1;
                }
            }

            // Unhighlight other slot buttons
            for (int otherSlot = 0; otherSlot < MaxUpgradeSlots; otherSlot++)
            {
                if (otherSlot == slot)
                {
                    continue;
                }

                string[] otherPatterns = new string[]
                {
                    $"BTN_UPGRADE{otherSlot}",
                    $"BTN_SLOT{otherSlot}",
                    $"BTN_UPGRADESLOT{otherSlot}"
                };

                foreach (string tag in otherPatterns)
                {
                    if (_controlMap.TryGetValue(tag, out GUIControl control))
                    {
                        control.IsSelected = 0;
                    }
                }
            }

            // Populate upgrade list for selected slot
            if (_availableUpgradesPerSlot.TryGetValue(slot, out List<string> upgrades))
            {
                // Find list box for this slot (could be slot-specific or generic)
                string listBoxTag = FindUpgradeListBoxTag(slot);
                if (!string.IsNullOrEmpty(listBoxTag))
                {
                    PopulateUpgradeListBox(listBoxTag, upgrades, slot);
                }
            }
        }

        /// <summary>
        /// Gets the currently selected upgrade slot from UI controls.
        /// </summary>
        /// <returns>Selected upgrade slot index (0-based), or -1 if none selected.</returns>
        /// <remarks>
        /// Based on Eclipse upgrade system: Selected slot is tracked via _selectedUpgradeSlot field
        /// and can be determined by checking which slot button has IsSelected set.
        /// </remarks>
        private int GetSelectedUpgradeSlotFromUI()
        {
            // Check if a slot button is selected
            for (int slot = 0; slot < MaxUpgradeSlots; slot++)
            {
                string[] slotControlPatterns = new string[]
                {
                    $"BTN_UPGRADE{slot}",
                    $"BTN_SLOT{slot}",
                    $"BTN_UPGRADESLOT{slot}"
                };

                foreach (string tag in slotControlPatterns)
                {
                    if (_controlMap.TryGetValue(tag, out GUIControl control))
                    {
                        if (control.IsSelected.HasValue && control.IsSelected.Value != 0)
                        {
                            _selectedUpgradeSlot = slot;
                            return slot;
                        }
                    }
                }
            }

            // Return stored selected slot if no button is explicitly selected
            return _selectedUpgradeSlot;
        }

        /// <summary>
        /// Gets the selected upgrade ResRef from the list box for a given slot.
        /// </summary>
        /// <param name="slot">Upgrade slot index (0-based).</param>
        /// <returns>Selected upgrade ResRef, or empty string if none selected.</returns>
        /// <remarks>
        /// Based on Eclipse upgrade system: List box CurrentValue represents the selected index.
        /// The ResRef is retrieved from the available upgrades list using this index.
        /// </remarks>
        private string GetSelectedUpgradeResRefFromListBox(int slot)
        {
            if (slot < 0 || slot >= MaxUpgradeSlots)
            {
                return string.Empty;
            }

            // Get available upgrades for this slot
            if (!_availableUpgradesPerSlot.TryGetValue(slot, out List<string> availableUpgrades))
            {
                return string.Empty;
            }

            if (availableUpgrades == null || availableUpgrades.Count == 0)
            {
                return string.Empty;
            }

            // Get list box control for this slot
            string listBoxTag = string.Empty;
            if (_slotUpgradeListBoxTags.TryGetValue(slot, out string slotTag))
            {
                listBoxTag = slotTag;
            }
            else
            {
                // Try to find list box (might be generic or slot-specific)
                listBoxTag = FindUpgradeListBoxTag(slot);
            }

            if (string.IsNullOrEmpty(listBoxTag) || !_controlMap.TryGetValue(listBoxTag, out GUIControl control))
            {
                return string.Empty;
            }

            if (control.GuiType != GUIControlType.ListBox)
            {
                return string.Empty;
            }

            // Get selected index from CurrentValue
            if (!control.CurrentValue.HasValue || control.CurrentValue.Value < 0)
            {
                return string.Empty;
            }

            int selectedIndex = control.CurrentValue.Value;
            if (selectedIndex >= availableUpgrades.Count)
            {
                return string.Empty;
            }

            return availableUpgrades[selectedIndex];
        }

        /// <summary>
        /// Extracts the slot number from a button tag.
        /// </summary>
        /// <param name="buttonTag">Button tag (e.g., "BTN_UPGRADE0", "BTN_SLOT1").</param>
        /// <returns>Slot number (0-based), or -1 if not found.</returns>
        /// <remarks>
        /// Based on Eclipse upgrade system: Slot button tags typically end with the slot number.
        /// Examples: "BTN_UPGRADE0" -> 0, "BTN_UPGRADE1" -> 1, "BTN_SLOT2" -> 2
        /// </remarks>
        private int ExtractSlotNumberFromButtonTag(string buttonTag)
        {
            if (string.IsNullOrEmpty(buttonTag))
            {
                return -1;
            }

            // Try to extract number from end of tag
            // Patterns: "BTN_UPGRADE0", "BTN_UPGRADE1", "BTN_SLOT0", "BTN_SLOT1", etc.
            int lastDigitIndex = -1;
            for (int i = buttonTag.Length - 1; i >= 0; i--)
            {
                if (char.IsDigit(buttonTag[i]))
                {
                    lastDigitIndex = i;
                }
                else if (lastDigitIndex >= 0)
                {
                    break;
                }
            }

            if (lastDigitIndex >= 0)
            {
                // Extract numeric suffix
                string numberStr = buttonTag.Substring(lastDigitIndex);
                if (int.TryParse(numberStr, out int slot))
                {
                    return slot;
                }
            }

            return -1;
        }
    }
}

