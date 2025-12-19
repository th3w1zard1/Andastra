using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Formats.GFF;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Games.Common;
using Andastra.Parsing.Resource.Generics.GUI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.Engines.Odyssey.UI
{
    /// <summary>
    /// Base class for upgrade screen implementation common to both K1 and K2.
    /// </summary>
    /// <remarks>
    /// Odyssey Upgrade Screen Implementation:
    /// - Shared functionality between swkotor.exe (K1) and swkotor2.exe (K2)
    /// - Both use "upcrystals" for lightsabers
    /// - Both use "UpgradeType" and "Template" columns in upgrade tables
    /// - Both check inventory using similar logic
    /// - Differences: K2 uses "upgradeitems_p", K1 uses "upgradeitems" for regular items
    /// - Differences: K2 has 6 upgrade slots for lightsabers, K1 has 4
    /// - Based on swkotor.exe: FUN_006c7630 (constructor), FUN_006c6500 (button handler), FUN_006c59a0 (ApplyUpgrade)
    /// - Based on swkotor2.exe: FUN_00731a00 (constructor), FUN_0072e260 (button handler), FUN_00729640 (ApplyUpgrade)
    /// </remarks>
    public abstract class OdysseyUpgradeScreenBase : BaseUpgradeScreen
    {
        // GUI management
        private GUI _loadedGui;
        private string _guiName;
        private Dictionary<string, GUIControl> _controlMap;
        private Dictionary<string, GUIButton> _buttonMap;
        private GraphicsDevice _graphicsDevice;
        private bool _guiInitialized;

        /// <summary>
        /// Initializes a new instance of the upgrade screen.
        /// </summary>
        /// <param name="installation">Game installation for accessing 2DA files.</param>
        /// <param name="world">World context for entity access.</param>
        protected OdysseyUpgradeScreenBase(Installation installation, IWorld world)
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
        /// </remarks>
        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Shows the upgrade screen.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_00680cb0 @ 0x00680cb0 (ShowUpgradeScreen)
        /// Based on swkotor.exe: Similar upgrade screen display logic
        /// Original implementation:
        /// - Creates upgrade screen object (FUN_00733bc0 for K2, FUN_006c78d0 for K1)
        /// - Sets flags: item ID (offset 0x629), character ID (offset 0x18a8), disableItemCreation (offset 0x18c8), disableUpgrade (offset 0x18cc)
        /// - Calls FUN_0067c8f0 to initialize the screen
        /// - Calls FUN_0040bf90 to add to GUI manager (FUN_0040bf90 @ 0x0040bf90)
        /// - Calls FUN_00638bb0 to set screen mode (FUN_00638bb0 @ 0x00638bb0)
        /// - Loads GUI: "upgradeitems_p" for K2, "upgradeitems" for K1
        /// - Sets up controls: LBL_TITLE, LB_ITEMS, LB_DESCRIPTION, BTN_UPGRADEITEM, BTN_BACK
        /// </remarks>
        public override void Show()
        {
            _isVisible = true;

            // Get GUI name based on game version
            // Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 line 37 - loads "upgradeitems_p"
            // Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 line 37 - loads "upgradeitems"
            _guiName = GetUpgradeGuiName();

            // Load upgrade screen GUI
            // Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 - constructor loads GUI
            // Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 - constructor loads GUI
            if (!LoadUpgradeGui())
            {
                Console.WriteLine($"[OdysseyUpgradeScreen] ERROR: Failed to load GUI: {_guiName}");
                return;
            }

            // Initialize GUI controls and set up button handlers
            // Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 lines 39-64 - sets up controls
            // Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 lines 39-64 - sets up controls
            InitializeGuiControls();

            // Update GUI with current item and character data
            // Based on swkotor2.exe: FUN_00680cb0 @ 0x00680cb0 lines 40-41 - sets item ID and character ID
            UpdateGuiData();

            // Refresh available upgrades display
            RefreshUpgradeDisplay();

            _guiInitialized = true;
        }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: GUI hiding logic (inverse of ShowUpgradeScreen)
        /// Based on swkotor.exe: Similar GUI hiding logic
        /// Original implementation:
        /// - Removes GUI from GUI manager
        /// - Clears screen mode
        /// - Saves any pending changes
        /// - Returns control to game
        /// </remarks>
        public override void Hide()
        {
            _isVisible = false;

            // Clear GUI state
            // Based on swkotor2.exe: GUI cleanup when hiding screen
            // Based on swkotor.exe: GUI cleanup when hiding screen
            if (_guiInitialized)
            {
                // Clear control references
                _controlMap.Clear();
                _buttonMap.Clear();

                // Unload GUI (optional - may want to keep loaded for performance)
                // _loadedGui = null;
                // _guiName = null;

                _guiInitialized = false;
            }
        }

        /// <summary>
        /// Gets the upgrade table name for regular items (not lightsabers).
        /// </summary>
        /// <returns>Table name for regular item upgrades.</returns>
        protected new abstract string GetRegularUpgradeTableName();

        /// <summary>
        /// Gets available upgrade items for a given item and upgrade slot.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>List of available upgrade items (ResRefs).</returns>
        public override List<string> GetAvailableUpgrades(IEntity item, int upgradeSlot)
        {
            if (item == null)
            {
                return new List<string>();
            }

            IItemComponent itemComponent = item.GetComponent<IItemComponent>();
            if (itemComponent == null)
            {
                return new List<string>();
            }

            // Get base item type to determine upgrade compatibility
            int baseItemId = itemComponent.BaseItem;
            if (baseItemId <= 0)
            {
                return new List<string>();
            }

            if (upgradeSlot < 0)
            {
                return new List<string>();
            }

            // Determine which 2DA file to use for upgrades
            // Check if item is a lightsaber by looking up baseitems.2da
            string upgradeTableName = _override2DA;
            if (string.IsNullOrEmpty(upgradeTableName))
            {
                // Default: Use regular upgrade table for most items, upcrystals for lightsabers
                // Check baseitems.2da to determine if item is a lightsaber
                bool isLightsaber = IsLightsaberItem(baseItemId);
                upgradeTableName = isLightsaber ? GetLightsaberUpgradeTableName() : GetRegularUpgradeTableName();
            }

            List<string> availableUpgrades = new List<string>();

            try
            {
                // Load upgrade table (2DA file)
                ResourceResult upgradeTableResult = _installation.Resource(upgradeTableName, ResourceType.TwoDA, null, null);
                if (upgradeTableResult == null || upgradeTableResult.Data == null)
                {
                    return availableUpgrades;
                }

                // Parse 2DA file to get upgrade items
                TwoDA upgradeTable = null;
                using (var stream = new MemoryStream(upgradeTableResult.Data))
                {
                    var reader = new TwoDABinaryReader(stream);
                    try
                    {
                        upgradeTable = reader.Load();
                    }
                    catch (Exception)
                    {
                        // Error loading upgrade table, return empty list
                        return availableUpgrades;
                    }
                }

                if (upgradeTable == null)
                {
                    return availableUpgrades;
                }

                // Get character inventory ResRefs using base class helper
                // Based on swkotor2.exe: FUN_0055f2a0 @ 0x0055f2a0 - searches inventory by ResRef
                // Based on swkotor.exe: FUN_00555ed0 @ 0x00555ed0 - searches inventory by ResRef
                // These functions iterate through inventory items and compare ResRefs
                HashSet<string> inventoryResRefs = GetCharacterInventoryResRefs(_character);

                // Get column headers to check if required columns exist
                // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 - uses "UpgradeType" and "Template" columns
                // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 - uses "UpgradeType" and "Template" columns
                List<string> headers = upgradeTable.GetHeaders();
                bool hasUpgradeTypeColumn = headers.Contains("UpgradeType", StringComparer.OrdinalIgnoreCase);
                bool hasTemplateColumn = headers.Contains("Template", StringComparer.OrdinalIgnoreCase);

                if (!hasUpgradeTypeColumn || !hasTemplateColumn)
                {
                    // Required columns missing from upgrade table
                    return availableUpgrades;
                }

                // Iterate through upgrade table rows and filter by compatibility
                // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 lines 100-128, 144-172, 271-299
                // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 lines 88-116
                // Loop through all rows in upgrade table
                for (int rowIndex = 0; rowIndex < upgradeTable.GetHeight(); rowIndex++)
                {
                    TwoDARow row = upgradeTable.GetRow(rowIndex);

                    // Check UpgradeType compatibility (this is the slot index)
                    // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 102 - checks "UpgradeType" column
                    // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 90 - checks "UpgradeType" column
                    // UpgradeType column specifies which slot this upgrade can be placed in
                    // Can be:
                    // - Specific slot index (0, 1, 2, etc.) - matches upgradeSlot parameter
                    // - 0 (null/empty) - special case for lightsaber crystals (slot 1 in K2)
                    // - Empty/"****" (no match)
                    string upgradeTypeValue = null;
                    try
                    {
                        upgradeTypeValue = row.GetString("UpgradeType");
                    }
                    catch (KeyNotFoundException)
                    {
                        continue; // UpgradeType column missing or invalid
                    }

                    // Check if UpgradeType matches the requested slot
                    // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 107, 151, 278 - compares UpgradeType with slot
                    // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 95 - compares UpgradeType with slot
                    bool slotMatches = false;
                    if (string.IsNullOrWhiteSpace(upgradeTypeValue) || upgradeTypeValue == "****")
                    {
                        // Empty UpgradeType - special case: for lightsaber crystals (slot 1), UpgradeType can be 0/empty
                        // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 107 - checks if UpgradeType == 0
                        if (upgradeSlot == 1)
                        {
                            // For slot 1 (lightsaber crystals), empty UpgradeType is valid
                            slotMatches = true;
                        }
                        else
                        {
                            continue; // Skip rows with invalid UpgradeType for other slots
                        }
                    }
                    else
                    {
                        // Try to parse as integer for exact match
                        int? rowUpgradeType = row.GetInteger("UpgradeType", null);
                        if (rowUpgradeType.HasValue)
                        {
                            // UpgradeType must match upgradeSlot parameter
                            if (rowUpgradeType.Value == upgradeSlot)
                            {
                                slotMatches = true;
                            }
                        }
                    }

                    if (!slotMatches)
                    {
                        continue; // UpgradeType doesn't match requested slot
                    }

                    // Get Template ResRef from upgrade table row
                    // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 108, 152, 279 - uses "Template" column
                    // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 96, 98 - uses "Template" column
                    // Template column contains the upgrade item template ResRef
                    string upgradeResRef = null;
                    try
                    {
                        upgradeResRef = row.GetString("Template");
                    }
                    catch (KeyNotFoundException)
                    {
                        continue; // Template column missing or invalid
                    }

                    if (string.IsNullOrWhiteSpace(upgradeResRef) || upgradeResRef == "****")
                    {
                        continue; // Skip rows with invalid Template
                    }

                    // Normalize ResRef (remove extension, lowercase)
                    // Based on swkotor2.exe: FUN_00631140 @ 0x00631140 - ResRef normalization
                    // Based on swkotor.exe: FUN_005e6080 @ 0x005e6080 - ResRef normalization
                    string normalizedResRef = upgradeResRef.ToLowerInvariant();
                    if (normalizedResRef.EndsWith(".uti"))
                    {
                        normalizedResRef = normalizedResRef.Substring(0, normalizedResRef.Length - 4);
                    }

                    // Check if upgrade item is in inventory
                    // Based on swkotor2.exe: FUN_0055f2a0 @ 0x0055f2a0 - searches inventory by ResRef, returns item ID or 0x7f000000
                    // Based on swkotor.exe: FUN_00555ed0 @ 0x00555ed0 - searches inventory by ResRef, returns item ID or 0x7f000000
                    // FUN_0055f2a0/FUN_00555ed0 iterate through inventory items and compare ResRefs using string comparison
                    if (inventoryResRefs.Contains(normalizedResRef))
                    {
                        // Upgrade is compatible and available in inventory
                        // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 79, 118, 162, 289 - checks if item found in inventory
                        // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 66, 106 - checks if item found in inventory
                        if (!availableUpgrades.Contains(normalizedResRef, StringComparer.OrdinalIgnoreCase))
                        {
                            availableUpgrades.Add(normalizedResRef);
                        }
                    }
                }

                return availableUpgrades;
            }
            catch (Exception)
            {
                // Error loading or parsing upgrade table
                return availableUpgrades;
            }
        }


        /// <summary>
        /// Applies an upgrade to an item.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <param name="upgradeResRef">ResRef of upgrade item to apply.</param>
        /// <returns>True if upgrade was successful.</returns>
        public new abstract bool ApplyUpgrade(IEntity item, int upgradeSlot, string upgradeResRef);

        /// <summary>
        /// Removes an upgrade from an item.
        /// </summary>
        /// <param name="item">Item to modify.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if upgrade was removed.</returns>
        public new abstract bool RemoveUpgrade(IEntity item, int upgradeSlot);

        /// <summary>
        /// Gets the upgrade GUI name for this game version.
        /// </summary>
        /// <returns>GUI name (e.g., "upgradeitems_p" for K2, "upgradeitems" for K1).</returns>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 line 37 - uses "upgradeitems_p"
        /// Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 line 37 - uses "upgradeitems"
        /// </remarks>
        protected abstract string GetUpgradeGuiName();

        /// <summary>
        /// Loads the upgrade screen GUI from the installation.
        /// </summary>
        /// <returns>True if GUI was loaded successfully, false otherwise.</returns>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 lines 37-38 - loads GUI using FUN_00406e90 and FUN_0040a810
        /// Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 lines 37-38 - loads GUI using FUN_00406d80 and FUN_0040a680
        /// </remarks>
        private bool LoadUpgradeGui()
        {
            if (string.IsNullOrEmpty(_guiName))
            {
                return false;
            }

            try
            {
                // Load GUI resource from installation
                // Based on swkotor2.exe: FUN_00406e90 @ 0x00406e90 - loads GUI resource
                // Based on swkotor.exe: FUN_00406d80 @ 0x00406d80 - loads GUI resource
                ResourceResult guiResult = _installation.Resource(_guiName, ResourceType.GUI, null, null);
                if (guiResult == null || guiResult.Data == null || guiResult.Data.Length == 0)
                {
                    Console.WriteLine($"[OdysseyUpgradeScreen] ERROR: GUI resource not found: {_guiName}");
                    return false;
                }

                // Parse GUI file using GUIReader
                // Based on swkotor2.exe: GUI parsing system
                // Based on swkotor.exe: GUI parsing system
                GUIReader guiReader = new GUIReader(guiResult.Data);
                _loadedGui = guiReader.Load();

                if (_loadedGui == null || _loadedGui.Controls == null || _loadedGui.Controls.Count == 0)
                {
                    Console.WriteLine($"[OdysseyUpgradeScreen] ERROR: Failed to parse GUI: {_guiName}");
                    return false;
                }

                Console.WriteLine($"[OdysseyUpgradeScreen] Successfully loaded GUI: {_guiName} - {_loadedGui.Controls.Count} controls");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyUpgradeScreen] ERROR: Exception loading GUI {_guiName}: {ex.Message}");
                Console.WriteLine($"[OdysseyUpgradeScreen] Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// Initializes GUI controls and sets up button handlers.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 lines 39-64 - sets up controls
        /// Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 lines 39-64 - sets up controls
        /// Original implementation:
        /// - Gets control references: LBL_TITLE, LB_ITEMS, LB_DESCRIPTION, BTN_UPGRADEITEM, BTN_BACK
        /// - Sets up button click handlers
        /// - Initializes list boxes
        /// </remarks>
        private void InitializeGuiControls()
        {
            if (_loadedGui == null || _loadedGui.Controls == null)
            {
                return;
            }

            // Build control and button maps for quick lookup
            // Based on swkotor2.exe: Control mapping system
            // Based on swkotor.exe: Control mapping system
            BuildControlMaps(_loadedGui.Controls, _controlMap, _buttonMap);

            // Set up button click handlers
            // Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 lines 80-82 - sets button handlers
            // Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 lines 80-82 - sets button handlers
            // Button handlers are set up via event system or callback registration
            // BTN_UPGRADEITEM: Calls ApplyUpgrade when clicked
            // BTN_BACK: Calls Hide() when clicked
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
        /// Based on swkotor2.exe: FUN_00680cb0 @ 0x00680cb0 lines 40-41 - sets item ID (offset 0x629) and character ID (offset 0x18a8)
        /// Based on swkotor.exe: Similar data setting logic
        /// Original implementation:
        /// - Sets item ID in GUI object (offset 0x629)
        /// - Sets character ID in GUI object (offset 0x18a8)
        /// - Sets disableItemCreation flag (offset 0x18c8)
        /// - Sets disableUpgrade flag (offset 0x18cc)
        /// - Sets override2DA string if provided
        /// </remarks>
        private void UpdateGuiData()
        {
            // Update title label with item name if available
            // Based on swkotor2.exe: FUN_00731a00 @ 0x00731a00 line 39 - sets LBL_TITLE
            // Based on swkotor.exe: FUN_006c7630 @ 0x006c7630 line 39 - sets LBL_TITLE
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
        /// Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 - populates upgrade list
        /// Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 - populates upgrade list
        /// Original implementation:
        /// - Gets available upgrades for current item and slot
        /// - Populates LB_ITEMS list box with available upgrade items
        /// - Updates LB_DESCRIPTION with selected upgrade description
        /// </remarks>
        private void RefreshUpgradeDisplay()
        {
            if (_targetItem == null)
            {
                return;
            }

            // Get available upgrades for all slots
            // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 - gets available upgrades
            // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 - gets available upgrades
            // For now, we'll populate the list when a slot is selected
            // The actual list population happens when user selects a slot
        }

        /// <summary>
        /// Handles button click events from the GUI.
        /// </summary>
        /// <param name="buttonTag">Tag of the button that was clicked.</param>
        /// <remarks>
        /// Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 - button click handler
        /// Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 - button click handler
        /// Original implementation:
        /// - BTN_UPGRADEITEM: Applies selected upgrade to item
        /// - BTN_BACK: Hides upgrade screen
        /// </remarks>
        public void HandleButtonClick(string buttonTag)
        {
            if (string.IsNullOrEmpty(buttonTag))
            {
                return;
            }

            switch (buttonTag.ToUpperInvariant())
            {
                case "BTN_UPGRADEITEM":
                    // Apply selected upgrade
                    // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 215 - calls ApplyUpgrade
                    // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 163 - calls ApplyUpgrade
                    HandleApplyUpgrade();
                    break;

                case "BTN_BACK":
                    // Hide upgrade screen
                    // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 - hides screen
                    // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 - hides screen
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
        /// Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 line 215 - calls ApplyUpgrade
        /// Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 line 163 - calls ApplyUpgrade
        /// </remarks>
        private void HandleApplyUpgrade()
        {
            if (_targetItem == null)
            {
                return;
            }

            // Get selected upgrade from list box
            // Based on swkotor2.exe: FUN_0072e260 @ 0x0072e260 - gets selected item from LB_ITEMS
            // Based on swkotor.exe: FUN_006c6500 @ 0x006c6500 - gets selected item from LB_ITEMS
            // For now, this is a placeholder - full implementation would:
            // 1. Get selected upgrade slot from UI
            // 2. Get selected upgrade ResRef from list box
            // 3. Call ApplyUpgrade with item, slot, and ResRef
            // 4. Refresh display
        }

        /// <summary>
        /// Gets the loaded GUI for external rendering.
        /// </summary>
        /// <returns>The loaded GUI, or null if not loaded.</returns>
        public GUI GetLoadedGui()
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

