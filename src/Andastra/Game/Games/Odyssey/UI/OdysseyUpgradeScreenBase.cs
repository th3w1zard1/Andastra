using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BioWare.NET;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.TwoDA;
using BioWare.NET.Extract;
using BioWare.NET.Common;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics;
using BioWare.NET.Resource.Formats.GFF.Generics.GUI;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Odyssey.Components;
using Andastra.Game.Games.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UTI = BioWare.NET.Resource.Formats.GFF.Generics.UTI.UTI;

namespace Andastra.Game.Games.Odyssey.UI
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
    /// - Based on swkotor.exe: 0x006c7630 (constructor), 0x006c6500 (button handler), 0x006c59a0 (ApplyUpgrade)
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 (constructor), 0x0072e260 (button handler), 0x00729640 (ApplyUpgrade)
    /// </remarks>
    public abstract class OdysseyUpgradeScreenBase : BaseUpgradeScreen
    {
        // GUI management
        private BioWare.NET.Resource.Formats.GFF.Generics.GUI.GUI _loadedGui;
        private string _guiName;
        private Dictionary<string, GUIControl> _controlMap;
        private Dictionary<string, GUIButton> _buttonMap;
        private GraphicsDevice _graphicsDevice;
        private bool _guiInitialized;

        // Upgrade slot and list box state tracking
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - tracks selected upgrade slot and list box selection
        // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - tracks selected upgrade slot and list box selection
        private int? _selectedUpgradeSlot;
        private List<string> _currentUpgradeList; // List of upgrade ResRefs currently displayed in LB_ITEMS

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
            _selectedUpgradeSlot = null;
            _currentUpgradeList = new List<string>();
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00680cb0 @ 0x00680cb0 (ShowUpgradeScreen)
        /// Based on swkotor.exe: Similar upgrade screen display logic
        /// Original implementation:
        /// - Creates upgrade screen object (0x00733bc0 for K2, 0x006c78d0 for K1)
        /// - Sets flags: item ID (offset 0x629), character ID (offset 0x18a8), disableItemCreation (offset 0x18c8), disableUpgrade (offset 0x18cc)
        /// - Calls 0x0067c8f0 to initialize the screen
        /// - Calls 0x0040bf90 to add to GUI manager (0x0040bf90 @ 0x0040bf90)
        /// - Calls 0x00638bb0 to set screen mode (0x00638bb0 @ 0x00638bb0)
        /// - Loads GUI: "upgradeitems_p" for K2, "upgradeitems" for K1
        /// - Sets up controls: LBL_TITLE, LB_ITEMS, LB_DESCRIPTION, BTN_UPGRADEITEM, BTN_BACK
        /// </remarks>
        public override void Show()
        {
            _isVisible = true;

            // Extract character skills when screen is shown
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Character skills used for item creation/upgrading (NOT IMPLEMENTED in original)
            // Skills are extracted when character is set, but we also extract them here to ensure they're up-to-date
            ExtractCharacterSkills();

            // Get GUI name based on game version
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 line 37 - loads "upgradeitems_p"
            // Based on swkotor.exe: 0x006c7630 @ 0x006c7630 line 37 - loads "upgradeitems"
            _guiName = GetUpgradeGuiName();

            // Load upgrade screen GUI
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 - constructor loads GUI
            // Based on swkotor.exe: 0x006c7630 @ 0x006c7630 - constructor loads GUI
            if (!LoadUpgradeGui())
            {
                Console.WriteLine($"[OdysseyUpgradeScreen] ERROR: Failed to load GUI: {_guiName}");
                return;
            }

            // Initialize GUI controls and set up button handlers
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 lines 39-64 - sets up controls
            // Based on swkotor.exe: 0x006c7630 @ 0x006c7630 lines 39-64 - sets up controls
            InitializeGuiControls();

            // Update GUI with current item and character data
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00680cb0 @ 0x00680cb0 lines 40-41 - sets item ID and character ID
            UpdateGuiData();

            // Refresh available upgrades display
            RefreshUpgradeDisplay();

            _guiInitialized = true;
        }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GUI hiding logic (inverse of ShowUpgradeScreen)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GUI cleanup when hiding screen
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
        protected override string GetRegularUpgradeTableName()
        {
            // This should be overridden by derived classes (K1UpgradeScreen, K2UpgradeScreen)
            throw new NotImplementedException("GetRegularUpgradeTableName must be implemented by derived class");
        }

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
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055f2a0 @ 0x0055f2a0 - searches inventory by ResRef
                // Based on swkotor.exe: 0x00555ed0 @ 0x00555ed0 - searches inventory by ResRef
                // These functions iterate through inventory items and compare ResRefs
                HashSet<string> inventoryResRefs = GetCharacterInventoryResRefs(_character);

                // Get column headers to check if required columns exist
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - uses "UpgradeType" and "Template" columns
                // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - uses "UpgradeType" and "Template" columns
                List<string> headers = upgradeTable.GetHeaders();
                bool hasUpgradeTypeColumn = headers.Contains("UpgradeType", StringComparer.OrdinalIgnoreCase);
                bool hasTemplateColumn = headers.Contains("Template", StringComparer.OrdinalIgnoreCase);

                if (!hasUpgradeTypeColumn || !hasTemplateColumn)
                {
                    // Required columns missing from upgrade table
                    return availableUpgrades;
                }

                // Iterate through upgrade table rows and filter by compatibility
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 lines 100-128, 144-172, 271-299
                // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 lines 88-116
                // Loop through all rows in upgrade table
                for (int rowIndex = 0; rowIndex < upgradeTable.GetHeight(); rowIndex++)
                {
                    TwoDARow row = upgradeTable.GetRow(rowIndex);

                    // Check UpgradeType compatibility (this is the slot index)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 102 - checks "UpgradeType" column
                    // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 90 - checks "UpgradeType" column
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
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 107, 151, 278 - compares UpgradeType with slot
                    // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 95 - compares UpgradeType with slot
                    bool slotMatches = false;
                    if (string.IsNullOrWhiteSpace(upgradeTypeValue) || upgradeTypeValue == "****")
                    {
                        // Empty UpgradeType - special case: for lightsaber crystals (slot 1), UpgradeType can be 0/empty
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 107 - checks if UpgradeType == 0
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
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 108, 152, 279 - uses "Template" column
                    // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 96, 98 - uses "Template" column
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
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00631140 @ 0x00631140 - ResRef normalization
                    // Based on swkotor.exe: 0x005e6080 @ 0x005e6080 - ResRef normalization
                    string normalizedResRef = upgradeResRef.ToLowerInvariant();
                    if (normalizedResRef.EndsWith(".uti"))
                    {
                        normalizedResRef = normalizedResRef.Substring(0, normalizedResRef.Length - 4);
                    }

                    // Check if upgrade item is in inventory
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055f2a0 @ 0x0055f2a0 - searches inventory by ResRef, returns item ID or 0x7f000000
                    // Based on swkotor.exe: 0x00555ed0 @ 0x00555ed0 - searches inventory by ResRef, returns item ID or 0x7f000000
                    // 0x0055f2a0/0x00555ed0 iterate through inventory items and compare ResRefs using string comparison
                    if (inventoryResRefs.Contains(normalizedResRef))
                    {
                        // Check skill requirements for upgrade (if character skills are available)
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Character skills used for item creation/upgrading (NOT IMPLEMENTED in original)
                        // Skills are used to filter available upgrades based on skill requirements
                        // If upgrade table has skill requirement columns (e.g., "RequiredSkill", "RequiredSkillRank"), check them
                        // Common skills used: Repair (5), Security (6), Computer Use (0), Demolitions (1)
                        bool meetsSkillRequirements = true;
                        if (_characterSkills.Count > 0)
                        {
                            // Check for skill requirement columns in upgrade table
                            // Some upgrade tables may have columns like "RequiredSkill" and "RequiredSkillRank"
                            // If present, check if character meets the skill requirement
                            if (headers.Contains("RequiredSkill", StringComparer.OrdinalIgnoreCase))
                            {
                                try
                                {
                                    string requiredSkillStr = row.GetString("RequiredSkill");
                                    if (!string.IsNullOrWhiteSpace(requiredSkillStr) && requiredSkillStr != "****")
                                    {
                                        // Try to parse as skill ID (0-7 for KOTOR)
                                        int? requiredSkillId = row.GetInteger("RequiredSkill", null);
                                        if (requiredSkillId.HasValue)
                                        {
                                            // Check if upgrade table has RequiredSkillRank column
                                            int requiredSkillRank = 0;
                                            if (headers.Contains("RequiredSkillRank", StringComparer.OrdinalIgnoreCase))
                                            {
                                                int? requiredRank = row.GetInteger("RequiredSkillRank", null);
                                                if (requiredRank.HasValue)
                                                {
                                                    requiredSkillRank = requiredRank.Value;
                                                }
                                            }

                                            // Check if character meets skill requirement
                                            int characterSkillRank = GetCharacterSkillRank(requiredSkillId.Value);
                                            if (characterSkillRank < requiredSkillRank)
                                            {
                                                meetsSkillRequirements = false;
                                            }
                                        }
                                    }
                                }
                                catch (KeyNotFoundException)
                                {
                                    // RequiredSkill column missing or invalid - skip skill check
                                }
                            }
                        }

                        // Upgrade is compatible, available in inventory, and meets skill requirements
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 79, 118, 162, 289 - checks if item found in inventory
                        // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 66, 106 - checks if item found in inventory
                        if (meetsSkillRequirements && !availableUpgrades.Contains(normalizedResRef, StringComparer.OrdinalIgnoreCase))
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
        public override bool ApplyUpgrade(IEntity item, int upgradeSlot, string upgradeResRef)
        {
            // This should be overridden by derived classes (K1UpgradeScreen, K2UpgradeScreen)
            throw new NotImplementedException("ApplyUpgrade must be implemented by derived class");
        }

        /// <summary>
        /// Removes an upgrade from an item.
        /// </summary>
        /// <param name="item">Item to modify.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if upgrade was removed.</returns>
        /// <remarks>
        /// Remove Upgrade Logic (Odyssey Engine - K1 and K2):
        /// - Based on swkotor.exe: 0x006c6500 @ 0x006c6500 lines 165-180 (removal logic)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 lines 217-230 (removal logic)
        /// - Original implementation flow:
        ///   1. Gets upgrade item from slot array (K1: offset 0x2f74, K2: offset 0x3d54)
        ///   2. Clears flag/bit associated with upgrade slot
        ///   3. Checks if upgrade is in upgrade list (K1: offset 0x2f68, K2: offset 0x3d48)
        ///   4. If found in list: removes from array (K1: 0x006857a0, K2: 0x00431ec0)
        ///   5. Returns upgrade item to inventory (K1: 0x0055d330, K2: 0x00567ce0)
        ///   6. Removes from upgrade list (K1: 0x00671c00, K2: 0x00482570)
        ///   7. Sets slot to 0 (clears upgrade from slot array)
        ///   8. Updates item stats (removes upgrade bonuses)
        ///   9. Recalculates item stats
        /// - The logic is identical between K1 and K2, only function addresses differ
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
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 169 - gets upgrade from offset 0x2f74
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 218 - gets upgrade from offset 0x3d54
            var upgrade = itemComponent.Upgrades.FirstOrDefault(u => u.Index == upgradeSlot);
            if (upgrade == null)
            {
                // No upgrade in slot
                return false;
            }

            // Get upgrade item ResRef from tracked upgrade data
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 169 - gets item from slot array
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 218 - gets item from slot array
            // We track upgrade ResRefs in _upgradeResRefMap for removal
            string upgradeKey = item.ObjectId.ToString() + "_" + upgradeSlot.ToString();
            string upgradeResRef = null;
            if (!_upgradeResRefMap.TryGetValue(upgradeKey, out upgradeResRef))
            {
                // Upgrade ResRef not found in tracking map - cannot remove properties
                // Still remove upgrade from item, but cannot restore properties
                // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 176 - removes from array using 0x006857a0
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 219 - removes from array using 0x00431ec0
                itemComponent.RemoveUpgrade(upgrade);
                return true;
            }

            // Load upgrade UTI template to remove properties
            // Based on swkotor.exe: 0x0055e160 @ 0x0055e160 - removes upgrade stats from item
            // Based on swkotor.exe: 0x005226d0 @ 0x005226d0 - loads UTI template
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055e160 @ 0x0055e160 - removes upgrade stats from item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 - loads UTI template
            UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);

            // Remove upgrade from item
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 176 - removes from array using 0x006857a0
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 219 - removes from array using 0x00431ec0
            // This clears the upgrade slot and removes it from the item's upgrade list
            itemComponent.RemoveUpgrade(upgrade);

            // Remove upgrade ResRef from tracking map
            // Clean up tracking data after removal
            _upgradeResRefMap.Remove(upgradeKey);

            // Remove upgrade properties from item (damage bonuses, AC bonuses, etc.)
            // Based on swkotor.exe: 0x0055e160 @ 0x0055e160 - removes upgrade stats from item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0055e160 @ 0x0055e160 - removes upgrade stats from item
            // Properties from upgrade UTI are removed to restore original item stats
            if (upgradeUTI != null)
            {
                RemoveUpgradeProperties(item, upgradeUTI);
            }

            // Recalculate item stats and update display
            // Based on swkotor.exe: Item stat recalculation after upgrade removal
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item stat recalculation after upgrade removal
            // Recalculates item damage, AC, and other stats after removing upgrade properties
            RecalculateItemStats(item);

            // Return upgrade item to inventory
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 171 - returns to inventory using 0x0055d330
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 221 - returns to inventory using 0x00567ce0
            // swkotor2.exe: 0x00567ce0 @ 0x00567ce0 - creates item entity and adds to inventory
            // swkotor.exe: 0x0055d330 @ 0x0055d330 - creates item entity and adds to inventory
            // Original implementation: Creates upgrade item entity from UTI template and adds to character's inventory
            CreateItemFromTemplateAndAddToInventory(upgradeResRef, _character);

            return true;
        }

        /// <summary>
        /// Gets the upgrade GUI name for this game version.
        /// </summary>
        /// <returns>GUI name (e.g., "upgradeitems_p" for K2, "upgradeitems" for K1).</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 line 37 - uses "upgradeitems_p"
        /// Based on swkotor.exe: 0x006c7630 @ 0x006c7630 line 37 - uses "upgradeitems"
        /// </remarks>
        protected abstract string GetUpgradeGuiName();

        /// <summary>
        /// Loads the upgrade screen GUI from the installation.
        /// </summary>
        /// <returns>True if GUI was loaded successfully, false otherwise.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 lines 37-38 - loads GUI using 0x00406e90 and 0x0040a810
        /// Based on swkotor.exe: 0x006c7630 @ 0x006c7630 lines 37-38 - loads GUI using 0x00406d80 and 0x0040a680
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
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00406e90 @ 0x00406e90 - loads GUI resource
                // Based on swkotor.exe: 0x00406d80 @ 0x00406d80 - loads GUI resource
                ResourceResult guiResult = _installation.Resource(_guiName, ResourceType.GUI, null, null);
                if (guiResult == null || guiResult.Data == null || guiResult.Data.Length == 0)
                {
                    Console.WriteLine($"[OdysseyUpgradeScreen] ERROR: GUI resource not found: {_guiName}");
                    return false;
                }

                // Parse GUI file using GUIReader
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GUI parsing system
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 lines 39-64 - sets up controls
        /// Based on swkotor.exe: 0x006c7630 @ 0x006c7630 lines 39-64 - sets up controls
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Control mapping system
            // Based on swkotor.exe: Control mapping system
            BuildControlMaps(_loadedGui.Controls, _controlMap, _buttonMap);

            // Set up button click handlers
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 lines 80-82 - sets button handlers
            // Based on swkotor.exe: 0x006c7630 @ 0x006c7630 lines 80-82 - sets button handlers
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00680cb0 @ 0x00680cb0 lines 40-41 - sets item ID (offset 0x629) and character ID (offset 0x18a8)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00731a00 @ 0x00731a00 line 39 - sets LBL_TITLE
            // Based on swkotor.exe: 0x006c7630 @ 0x006c7630 line 39 - sets LBL_TITLE
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
        /// Sets the selected upgrade slot and refreshes the upgrade list.
        /// </summary>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - slot selection handler
        /// Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - slot selection handler
        /// Original implementation:
        /// - Sets selected slot index
        /// - Gets available upgrades for that slot
        /// - Populates LB_ITEMS list box with upgrade items
        /// </remarks>
        public void SetSelectedUpgradeSlot(int upgradeSlot)
        {
            if (upgradeSlot < 0)
            {
                _selectedUpgradeSlot = null;
                _currentUpgradeList.Clear();
                RefreshUpgradeDisplay();
                return;
            }

            _selectedUpgradeSlot = upgradeSlot;
            RefreshUpgradeDisplay();
        }

        /// <summary>
        /// Refreshes the upgrade display with available upgrades.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - populates upgrade list
        /// Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - populates upgrade list
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

            if (!_selectedUpgradeSlot.HasValue)
            {
                // No slot selected - clear list box
                _currentUpgradeList.Clear();
                UpdateListBoxItems();
                return;
            }

            // Get available upgrades for the selected slot
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - gets available upgrades
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - gets available upgrades
            _currentUpgradeList = GetAvailableUpgrades(_targetItem, _selectedUpgradeSlot.Value);

            // Populate list box with available upgrades
            UpdateListBoxItems();
        }

        /// <summary>
        /// Updates the LB_ITEMS list box with the current upgrade list.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - populates LB_ITEMS list box
        /// Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - populates LB_ITEMS list box
        /// Original implementation:
        /// - Clears existing list box items
        /// - Adds each upgrade ResRef as a list box item
        /// - Sets list box item text to upgrade name (from UTI template)
        /// </remarks>
        private void UpdateListBoxItems()
        {
            if (!_controlMap.TryGetValue("LB_ITEMS", out GUIControl listBoxControl))
            {
                return;
            }

            if (!(listBoxControl is GUIListBox listBox))
            {
                return;
            }

            // Clear existing list box items (children represent list items)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - clears list box before populating
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - clears list box before populating
            listBox.Children.Clear();

            // Add each upgrade as a list box item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - adds items to list box
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - adds items to list box
            foreach (string upgradeResRef in _currentUpgradeList)
            {
                // Create a proto item for each upgrade
                GUIProtoItem listItem = new GUIProtoItem();
                listItem.Tag = upgradeResRef; // Store ResRef in tag for retrieval

                // Load upgrade UTI template to get display name
                UTI upgradeUTI = LoadUpgradeUTITemplate(upgradeResRef);
                if (upgradeUTI != null && upgradeUTI.Name != null)
                {
                    // Set display text to upgrade name (LocalizedString.ToString() returns English text or StringRef)
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
                    if (listItem.GuiText == null)
                    {
                        listItem.GuiText = new GUIText();
                    }
                    listItem.GuiText.Text = upgradeResRef;
                }

                listBox.Children.Add(listItem);
            }
        }

        /// <summary>
        /// Handles button click events from the GUI.
        /// </summary>
        /// <param name="buttonTag">Tag of the button that was clicked.</param>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - button click handler
        /// Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - button click handler
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
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 215 - calls ApplyUpgrade
                    // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 163 - calls ApplyUpgrade
                    HandleApplyUpgrade();
                    break;

                case "BTN_BACK":
                    // Hide upgrade screen
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - hides screen
                    // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - hides screen
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 215 - calls ApplyUpgrade
        /// Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 163 - calls ApplyUpgrade
        /// Original implementation:
        /// - Gets selected upgrade slot (offset 0x18d0 in upgrade screen object)
        /// - Gets selected upgrade ResRef from LB_ITEMS list box (CurrentValue property)
        /// - Calls ApplyUpgrade with item, slot, and ResRef
        /// - Refreshes upgrade display if successful
        /// </remarks>
        private void HandleApplyUpgrade()
        {
            if (_targetItem == null)
            {
                return;
            }

            // Check if upgrade slot is selected
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 216 - checks selected slot
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 164 - checks selected slot
            if (!_selectedUpgradeSlot.HasValue)
            {
                // No upgrade slot selected - cannot apply upgrade
                return;
            }

            int upgradeSlot = _selectedUpgradeSlot.Value;

            // Get selected upgrade ResRef from list box
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 217 - gets selected item from LB_ITEMS
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 165 - gets selected item from LB_ITEMS
            // List box CurrentValue property contains the selected item index
            string selectedUpgradeResRef = null;
            if (_controlMap.TryGetValue("LB_ITEMS", out GUIControl listBoxControl))
            {
                if (listBoxControl is GUIListBox listBox)
                {
                    // Get selected index from CurrentValue property
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - uses CurrentValue to get selected index
                    // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - uses CurrentValue to get selected index
                    if (listBox.CurrentValue.HasValue && listBox.CurrentValue.Value >= 0 && listBox.CurrentValue.Value < _currentUpgradeList.Count)
                    {
                        int selectedIndex = listBox.CurrentValue.Value;
                        selectedUpgradeResRef = _currentUpgradeList[selectedIndex];
                    }
                    else if (listBox.Children != null && listBox.Children.Count > 0)
                    {
                        // Fallback: Check for item with IsSelected flag set
                        // Based on original GUI system: Items may have IsSelected property set
                        foreach (GUIControl child in listBox.Children)
                        {
                            if (child != null && child.IsSelected.HasValue && child.IsSelected.Value != 0)
                            {
                                // This item is selected - use its tag (ResRef)
                                if (!string.IsNullOrEmpty(child.Tag))
                                {
                                    selectedUpgradeResRef = child.Tag;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(selectedUpgradeResRef))
            {
                // No upgrade selected from list box - cannot apply upgrade
                return;
            }

            // Apply upgrade to item
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 line 215 - calls ApplyUpgrade
            // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 line 163 - calls ApplyUpgrade
            bool success = ApplyUpgrade(_targetItem, upgradeSlot, selectedUpgradeResRef);

            if (success)
            {
                // Refresh upgrade display to reflect changes
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x0072e260 @ 0x0072e260 - refreshes display after applying upgrade
                // Based on swkotor.exe: 0x006c6500 @ 0x006c6500 - refreshes display after applying upgrade
                RefreshUpgradeDisplay();
            }
        }

        /// <summary>
        /// Gets the loaded GUI for external rendering.
        /// </summary>
        /// <returns>The loaded GUI, or null if not loaded.</returns>
        public BioWare.NET.Resource.Formats.GFF.Generics.GUI.GUI GetLoadedGui()
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
        /// Creates an item entity from a UTI template ResRef and adds it to the character's inventory.
        /// </summary>
        /// <param name="templateResRef">ResRef of the item template to create.</param>
        /// <param name="character">Character to add the item to (null uses player).</param>
        /// <returns>True if item was created and added successfully, false otherwise.</returns>
        /// <remarks>
        /// Based on swkotor.exe: 0x0055d330 @ 0x0055d330 - creates item entity and adds to inventory
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00567ce0 @ 0x00567ce0 - creates item entity and adds to inventory
        /// Original implementation:
        /// - Loads UTI template from installation
        /// - Creates item entity using World.CreateEntity
        /// - Configures item component with UTI template data
        /// - Adds item to character's inventory
        /// - If inventory is full, item entity is destroyed
        /// </remarks>
        protected bool CreateItemFromTemplateAndAddToInventory(string templateResRef, IEntity character)
        {
            if (string.IsNullOrEmpty(templateResRef) || _world == null)
            {
                return false;
            }

            // Get character entity (use player if null)
            if (character == null)
            {
                // Get player character from world using multiple fallback strategies
                // Based on swkotor.exe: Player entity retrieval patterns
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player entity retrieval patterns
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

            if (character == null)
            {
                // Character not found - cannot add item to inventory
                return false;
            }

            // Get character's inventory component
            IInventoryComponent characterInventory = character.GetComponent<IInventoryComponent>();
            if (characterInventory == null)
            {
                // Character has no inventory - cannot add item
                return false;
            }

            // Load UTI template from installation
            // Based on swkotor.exe: 0x005226d0 @ 0x005226d0 - loads UTI template
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x005226d0 @ 0x005226d0 - loads UTI template
            UTI utiTemplate = LoadUpgradeUTITemplate(templateResRef);
            if (utiTemplate == null)
            {
                // UTI template not found or failed to load
                return false;
            }

            // Create item entity
            // Based on swkotor.exe: 0x0055d330 @ 0x0055d330 - creates item entity using World.CreateEntity
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00567ce0 @ 0x00567ce0 - creates item entity using World.CreateEntity
            IEntity itemEntity = _world.CreateEntity(Runtime.Core.Enums.ObjectType.Item, System.Numerics.Vector3.Zero, 0f);
            if (itemEntity == null)
            {
                // Failed to create item entity
                return false;
            }

            // Set tag from template or use template name
            // Based on swkotor.exe: Item tag assignment from UTI template
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item tag assignment from UTI template
            if (!string.IsNullOrEmpty(utiTemplate.Tag))
            {
                itemEntity.Tag = utiTemplate.Tag;
            }
            else
            {
                itemEntity.Tag = templateResRef;
            }

            // Add item component with UTI template data
            // Based on swkotor.exe: 0x0055d330 @ 0x0055d330 - configures item component
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00567ce0 @ 0x00567ce0 - configures item component
            // Located via string references: "ItemComponent" @ 0x007c41e4 (swkotor2.exe)
            var itemComponent = new OdysseyItemComponent
            {
                BaseItem = utiTemplate.BaseItem,
                StackSize = utiTemplate.StackSize,
                Charges = utiTemplate.Charges,
                Cost = utiTemplate.Cost,
                Identified = utiTemplate.Identified != 0,
                TemplateResRef = templateResRef
            };

            // Convert UTI properties to ItemProperty
            // Based on swkotor.exe: Property conversion from UTI template
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Property conversion from UTI template
            foreach (var utiProp in utiTemplate.Properties)
            {
                var prop = new ItemProperty
                {
                    PropertyType = utiProp.PropertyName,
                    Subtype = utiProp.Subtype,
                    CostTable = utiProp.CostTable,
                    CostValue = utiProp.CostValue,
                    Param1 = utiProp.Param1,
                    Param1Value = utiProp.Param1Value
                };
                itemComponent.AddProperty(prop);
            }

            // Convert UTI upgrades to ItemUpgrade (if any)
            // Based on swkotor.exe: Upgrade conversion from UTI template
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Upgrade conversion from UTI template
            for (int i = 0; i < utiTemplate.Upgrades.Count; i++)
            {
                var utiUpgrade = utiTemplate.Upgrades[i];
                var upgrade = new ItemUpgrade
                {
                    UpgradeType = i, // Index-based upgrade type
                    Index = i
                };
                itemComponent.AddUpgrade(upgrade);
            }

            // Add item component to entity
            itemEntity.AddComponent(itemComponent);

            // Add item to character's inventory
            // Based on swkotor.exe: 0x0055d330 @ 0x0055d330 - adds item to inventory, destroys if full
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00567ce0 @ 0x00567ce0 - adds item to inventory, destroys if full
            // Original implementation: If inventory is full, item entity is destroyed and function returns false
            if (!characterInventory.AddItem(itemEntity))
            {
                // Inventory full - destroy item entity
                // Based on swkotor.exe: 0x0055d330 @ 0x0055d330 - destroys item if inventory full
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00567ce0 @ 0x00567ce0 - destroys item if inventory full
                _world.DestroyEntity(itemEntity.ObjectId);
                return false;
            }

            // Item successfully created and added to inventory
            return true;
        }

        /// <summary>
        /// Gets the character entity for upgrade operations (common to K1 and K2).
        /// </summary>
        /// <returns>Character entity, or null if not found.</returns>
        /// <remarks>
        /// Character Retrieval Logic (Odyssey Engine - K1 and K2):
        /// - Based on swkotor.exe: DAT_007a39fc - global structure storing current player character pointer
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DAT_008283d4 - global structure storing current player character pointer
        /// - Original implementation: Both games use a global structure to store the current player character
        /// - The upgrade screen accesses the character from this global structure
        /// - Common pattern: Get character from stored _character field, or find player entity from world
        /// </remarks>
        protected IEntity GetCharacterEntity()
        {
            // First, try to use stored character
            if (_character != null)
            {
                return _character;
            }

            // Get player character from world
            // Based on swkotor.exe: Player entity is tagged "Player"
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player entity is tagged "Player"
            // Located via string references: "Player" @ 0x007be628 (swkotor2.exe), "Mod_PlayerList" @ 0x007be060 (swkotor2.exe)
            IEntity character = _world.GetEntityByTag("Player", 0);

            if (character == null)
            {
                // Fallback: Search through all entities for one marked as player
                // Based on swkotor.exe: Player entity has IsPlayer data flag set to true
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player entity has IsPlayer data flag set to true
                // Original implementation: Player entity is marked with IsPlayer flag during creation
                foreach (IEntity entity in _world.GetAllEntities())
                {
                    if (entity == null)
                    {
                        continue;
                    }

                    // Check tag-based identification
                    string tag = entity.Tag;
                    if (!string.IsNullOrEmpty(tag))
                    {
                        if (string.Equals(tag, "Player", StringComparison.OrdinalIgnoreCase) ||
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

            return character;
        }

    }
}

