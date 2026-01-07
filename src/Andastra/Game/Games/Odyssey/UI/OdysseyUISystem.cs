using System;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Engines.Odyssey.UI
{
    /// <summary>
    /// UI system implementation for Odyssey engine (KOTOR/TSL).
    /// </summary>
    /// <remarks>
    /// UI System Implementation:
    /// - Odyssey-specific UI system implementation inheriting from BaseUISystem
    /// - Based on swkotor.exe and swkotor2.exe UI systems
    /// - Located via string references: GUI panels, UI screens, upgrade screens
    /// - Original implementation: Manages UI screen state, screen transitions, modal dialogs
    /// - UI screens: Upgrade screen, inventory screen, character screen, dialogue screen, etc.
    /// - Screen management: Push/pop screen stack, modal overlays, screen transitions
    ///
    /// Based on reverse engineering:
    /// - swkotor2.exe: ShowUpgradeScreen @ 0x00680cb0 creates upgrade selection screen ("upgradesel_p") and upgrade items screen ("upgradeitems_p")
    /// - swkotor.exe: Similar upgrade screen functionality with K1-specific GUI panels
    /// - Original creates two GUI panels: upgrade selection screen for item type filtering, upgrade items screen for item modification
    /// - GUI manager functions: FUN_0040bf90 adds to GUI manager, FUN_00638bb0 sets screen mode
    /// </remarks>
    public class OdysseyUISystem : BaseUISystem
    {
        private readonly IUpgradeScreen _upgradeScreen;

        /// <summary>
        /// Initializes a new instance of the UI system.
        /// </summary>
        /// <param name="installation">Game installation for accessing game data.</param>
        /// <param name="world">World context for entity access.</param>
        public OdysseyUISystem(Installation installation, IWorld world)
            : base(world)
        {
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }

            // Create appropriate upgrade screen based on game type
            // K1 uses K1UpgradeScreen (swkotor.exe), K2 uses K2UpgradeScreen (swkotor2.exe)
            if (installation.Game == Andastra.Parsing.Common.BioWareGame.K1)
            {
                _upgradeScreen = new K1UpgradeScreen(installation, world);
            }
            else if (installation.Game == Andastra.Parsing.Common.BioWareGame.TSL)
            {
                _upgradeScreen = new K2UpgradeScreen(installation, world);
            }
            else
            {
                // Fallback to K2 for unknown game types
                _upgradeScreen = new K2UpgradeScreen(installation, world);
            }
        }

        /// <summary>
        /// Odyssey-specific implementation of upgrade screen display.
        /// </summary>
        /// <param name="item">Item to upgrade (validated by base class).</param>
        /// <param name="character">Character whose skills will be used (validated by base class).</param>
        /// <param name="disableItemCreation">If true, disable item creation screen.</param>
        /// <param name="disableUpgrade">If true, force straight to item creation and disable upgrading.</param>
        /// <param name="override2DA">Override 2DA file name (empty string for default).</param>
        /// <remarks>
        /// Based on swkotor2.exe: ShowUpgradeScreen @ 0x00680cb0
        /// Original implementation:
        /// - Creates upgrade selection screen GUI ("upgradesel_p") with item type filters
        /// - Creates upgrade items screen GUI ("upgradeitems_p") with item list and upgrade buttons
        /// - Sets flags in GUI object: item ID (offset 0x629), character ID (offset 0x18a8), disableItemCreation (offset 0x18c8), disableUpgrade (offset 0x18cc)
        /// - Shows screen via GUI manager (FUN_0040bf90 adds to GUI manager, FUN_00638bb0 sets screen mode)
        /// </remarks>
        protected override void ShowUpgradeScreenImpl(uint item, uint character, bool disableItemCreation, bool disableUpgrade, string override2DA)
        {
            // OBJECT_INVALID = 0x7FFFFFFF (uint.MaxValue)
            const uint ObjectInvalid = 0x7FFFFFFF;

            // Resolve item entity (base class already validated, but we need the entity for the upgrade screen)
            IEntity itemEntity = null;
            if (item != 0 && item != ObjectInvalid)
            {
                itemEntity = _world.GetEntity(item);
            }

            // Resolve character entity
            IEntity characterEntity = null;
            if (character != 0 && character != ObjectInvalid)
            {
                characterEntity = _world.GetEntity(character);
            }

            // Configure upgrade screen
            // Original creates two GUI panels: upgrade selection screen and upgrade items screen
            _upgradeScreen.TargetItem = itemEntity;
            _upgradeScreen.Character = characterEntity;
            _upgradeScreen.DisableItemCreation = disableItemCreation;
            _upgradeScreen.DisableUpgrade = disableUpgrade;
            _upgradeScreen.Override2DA = override2DA;

            // Show upgrade screen
            // Original shows screen via GUI manager (FUN_0040bf90 adds to GUI manager, FUN_00638bb0 sets screen mode)
            _upgradeScreen.Show();
        }

        /// <summary>
        /// Gets whether the upgrade screen is currently visible.
        /// </summary>
        public override bool IsUpgradeScreenVisible
        {
            get { return _upgradeScreen.IsVisible; }
        }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        public override void HideUpgradeScreen()
        {
            _upgradeScreen.Hide();
        }
    }
}

