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
    /// - Common interface for item upgrade systems across all BioWare engines
    /// - Upgrade screen allows players to modify weapons and armor
    /// - Upgrade types: Crystals (lightsabers), modifications (weapons/armor), components
    /// - Upgrade slots: Items have upgrade slots (UpgradeType field in UTI)
    /// - Upgrade items: Defined in 2DA files (engine-specific table names)
    /// - Item creation: Can create items from components (if enabled)
    /// - Skill requirements: Character skills affect upgrade success (engine-specific)
    ///
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe/swkotor2.exe): Full upgrade screen system with 2DA-based upgrades
    /// - Aurora (nwmain.exe): No upgrade screen system (uses different item modification)
    /// - Eclipse (daorigins.exe/DragonAge2.exe): ItemUpgrade system with GUIItemUpgrade class
    /// - Infinity: No upgrade screen system (uses different item modification)
    ///
    /// Base implementation: BaseUpgradeScreen in Runtime.Games.Common
    /// Engine implementations: OdysseyUpgradeScreenBase, AuroraUpgradeScreen, EclipseUpgradeScreen, InfinityUpgradeScreen
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

