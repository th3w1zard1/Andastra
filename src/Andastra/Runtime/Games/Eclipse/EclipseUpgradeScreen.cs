using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Generics.GUI;
using ParsingGUI = Andastra.Parsing.Resource.Generics.GUI.GUI;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Eclipse.GUI;
using Andastra.Runtime.Graphics;

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

            // Refresh available upgrades display
            RefreshUpgradeDisplay();

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
        /// Full implementation:
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
            return game.Is();
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
                        // For now, we'll also load directly for compatibility
                    }
                }

                // Load GUI resource from installation
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

            // Get available upgrades for all slots
            // Based on Eclipse upgrade system: Upgrade lists are populated when item is selected
            // The actual list population happens when user selects a slot
            // For now, this is a placeholder - full implementation would populate list boxes
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
                    // Unknown button - ignore
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

            // Get selected upgrade from GUI controls
            // Based on Eclipse upgrade system: Selected upgrade is retrieved from list box or slot selection
            // For now, this is a placeholder - full implementation would:
            // 1. Get selected upgrade slot from UI
            // 2. Get selected upgrade ResRef from list box
            // 3. Call ApplyUpgrade with item, slot, and ResRef
            // 4. Refresh display
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

            // Get selected upgrade slot from GUI controls
            // Based on Eclipse upgrade system: Selected upgrade slot is retrieved from UI
            // For now, this is a placeholder - full implementation would:
            // 1. Get selected upgrade slot from UI
            // 2. Call RemoveUpgrade with item and slot
            // 3. Refresh display
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
    }
}

