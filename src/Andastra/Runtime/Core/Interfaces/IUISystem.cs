using System;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Interface for UI system that manages game UI screens and overlays.
    /// </summary>
    /// <remarks>
    /// UI System Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe UI system
    /// - Located via string references: GUI panels, UI screens, upgrade screens
    /// - Original implementation: Manages UI screen state, screen transitions, modal dialogs
    /// - UI screens: Upgrade screen, inventory screen, character screen, dialogue screen, etc.
    /// - Screen management: Push/pop screen stack, modal overlays, screen transitions
    /// - Based on swkotor2.exe: UI system manages GUI panels and screen state
    /// </remarks>
    public interface IUISystem
    {
        /// <summary>
        /// Shows the upgrade screen for item modification.
        /// </summary>
        /// <param name="item">Item to upgrade (OBJECT_INVALID for all items).</param>
        /// <param name="character">Character whose skills will be used (OBJECT_INVALID for player).</param>
        /// <param name="disableItemCreation">If true, disable item creation screen.</param>
        /// <param name="disableUpgrade">If true, force straight to item creation and disable upgrading.</param>
        /// <param name="override2DA">Override 2DA file name (empty string for default).</param>
        void ShowUpgradeScreen(uint item, uint character, bool disableItemCreation, bool disableUpgrade, string override2DA);

        /// <summary>
        /// Gets whether the upgrade screen is currently visible.
        /// </summary>
        bool IsUpgradeScreenVisible { get; }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        void HideUpgradeScreen();
    }
}

