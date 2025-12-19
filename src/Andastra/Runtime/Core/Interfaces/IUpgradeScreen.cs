using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Interface for upgrade screen that handles item upgrade UI and logic.
    /// </summary>
    /// <remarks>
    /// Upgrade Screen Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe upgrade screen system
    /// - Located via string references: "upgradeitems_p" @ 0x007d09e4, "BTN_UPGRADEITEM" @ 0x007d09d4
    /// - "BTN_UPGRADEITEMS" @ 0x007d0b58, "BTN_CREATEITEMS" @ 0x007d0b48
    /// - Original implementation: Upgrade screen allows players to modify weapons and armor
    /// - Upgrade types: Crystals (lightsabers), modifications (weapons/armor), components
    /// - Upgrade slots: Items have upgrade slots (UpgradeType field in UTI)
    /// - Upgrade items: Defined in upgradeitems.2da, upcrystals.2da, etc.
    /// - Item creation: Can create items from components (if enabled)
    /// - Skill requirements: Character skills affect upgrade success (not implemented in original)
    /// - Based on swkotor2.exe: ShowUpgradeScreen function @ routine ID 850
    /// </remarks>
    public interface IUpgradeScreen
    {
        /// <summary>
        /// Gets or sets the item being upgraded (null for all items).
        /// </summary>
        IEntity TargetItem { get; set; }

        /// <summary>
        /// Gets or sets the character whose skills will be used (null for player).
        /// </summary>
        IEntity Character { get; set; }

        /// <summary>
        /// Gets or sets whether item creation is disabled.
        /// </summary>
        bool DisableItemCreation { get; set; }

        /// <summary>
        /// Gets or sets whether upgrading is disabled (forces item creation).
        /// </summary>
        bool DisableUpgrade { get; set; }

        /// <summary>
        /// Gets or sets the override 2DA file name (empty for default).
        /// </summary>
        string Override2DA { get; set; }

        /// <summary>
        /// Gets whether the upgrade screen is visible.
        /// </summary>
        bool IsVisible { get; }

        /// <summary>
        /// Shows the upgrade screen.
        /// </summary>
        void Show();

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        void Hide();

        /// <summary>
        /// Gets available upgrade items for a given item and upgrade slot.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>List of available upgrade items (ResRefs).</returns>
        List<string> GetAvailableUpgrades(IEntity item, int upgradeSlot);

        /// <summary>
        /// Applies an upgrade to an item.
        /// </summary>
        /// <param name="item">Item to upgrade.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <param name="upgradeResRef">ResRef of upgrade item to apply.</param>
        /// <returns>True if upgrade was successful.</returns>
        bool ApplyUpgrade(IEntity item, int upgradeSlot, string upgradeResRef);

        /// <summary>
        /// Removes an upgrade from an item.
        /// </summary>
        /// <param name="item">Item to modify.</param>
        /// <param name="upgradeSlot">Upgrade slot index (0-based).</param>
        /// <returns>True if upgrade was removed.</returns>
        bool RemoveUpgrade(IEntity item, int upgradeSlot);
    }
}

