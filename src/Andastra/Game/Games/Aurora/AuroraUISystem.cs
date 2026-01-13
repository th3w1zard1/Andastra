using System;
using BioWare.NET;
using BioWare.NET.Extract.Installation;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// UI system implementation for Aurora engine (Neverwinter Nights).
    /// </summary>
    /// <remarks>
    /// UI System Implementation:
    /// - Aurora-specific UI system implementation inheriting from BaseUISystem
    /// - Based on nwmain.exe UI system
    /// - Aurora engine uses scene-based GUI system with multiple panel types
    /// - GUI message system for screen transitions (ShowInventoryGUIMessage, HideInventoryGUIMessage, etc.)
    /// - Panel types: inventory, character sheet, dialogue, journal, spellbook, etc.
    ///
    /// Based on reverse engineering:
    /// - nwmain.exe: GUI system with scene-based panels and message-driven screen management
    ///   - GUI panels: sceneGUI_PNL_INV, sceneGUI_PNL_CHRSHT, sceneGUI_PNL_DIALOG, etc.
    ///   - Screen management via GUI messages: PushGUIScreenMessage, PopGUIScreenMessage
    ///   - No upgrade screen functions found (searched for "upgrade", "ShowUpgrade", "UpgradeScreen" - only found unrelated "hydro_pwhash_upgrade")
    ///   - Inventory GUI: CNWSPlayerInventoryGUI @ 0x1404b7bf0, OpenInventory @ 0x14046ad80, CloseInventory @ 0x140465f80
    ///   - GUI panel example: ShowExamplePanel @ 0x1401f0a20 demonstrates panel creation pattern
    ///
    /// Note: Aurora engine (nwmain.exe) does not have upgrade screens like Odyssey or Eclipse (Dragon Age).
    /// This implementation uses AuroraUpgradeScreen which provides graceful no-op behavior
    /// for upgrade screen operations, maintaining API consistency with other engines.
    /// </remarks>
    public class AuroraUISystem : BaseUISystem
    {
        private readonly IUpgradeScreen _upgradeScreen;

        /// <summary>
        /// Initializes a new instance of the UI system.
        /// </summary>
        /// <param name="installation">Game installation for accessing game data.</param>
        /// <param name="world">World context for entity access.</param>
        public AuroraUISystem(Installation installation, IWorld world)
            : base(world)
        {
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }

            // Create Aurora upgrade screen
            // Aurora engine does not have upgrade screens, but we use AuroraUpgradeScreen
            // to maintain API consistency and provide graceful no-op behavior
            _upgradeScreen = new AuroraUpgradeScreen(installation, world);
        }

        /// <summary>
        /// Aurora-specific implementation of upgrade screen display.
        /// </summary>
        /// <param name="item">Item to upgrade (validated by base class).</param>
        /// <param name="character">Character whose skills will be used (validated by base class).</param>
        /// <param name="disableItemCreation">If true, disable item creation screen.</param>
        /// <param name="disableUpgrade">If true, force straight to item creation and disable upgrading.</param>
        /// <param name="override2DA">Override 2DA file name (empty string for default).</param>
        /// <remarks>
        /// Based on reverse engineering:
        /// - nwmain.exe: No upgrade screen functions found (searched for "upgrade", "ShowUpgrade", "UpgradeScreen")
        /// - Aurora engine uses different item modification systems (crafting, enchanting, identification)
        /// - This method delegates to AuroraUpgradeScreen which provides graceful no-op behavior
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
            // Aurora engine does not have upgrade screens, but we maintain API consistency
            // by delegating to AuroraUpgradeScreen which provides graceful no-op behavior
            _upgradeScreen.TargetItem = itemEntity;
            _upgradeScreen.Character = characterEntity;
            _upgradeScreen.DisableItemCreation = disableItemCreation;
            _upgradeScreen.DisableUpgrade = disableUpgrade;
            _upgradeScreen.Override2DA = override2DA;

            // Show upgrade screen (no-op for Aurora engine)
            // AuroraUpgradeScreen.Show() sets _isVisible = true but does not display any UI
            // This maintains API consistency while correctly reflecting that Aurora has no upgrade screens
            _upgradeScreen.Show();
        }

        /// <summary>
        /// Gets whether the upgrade screen is currently visible.
        /// </summary>
        /// <remarks>
        /// Aurora engine does not have upgrade screens, so this returns the visibility state
        /// from AuroraUpgradeScreen (which will always be false after Hide() is called).
        /// </remarks>
        public override bool IsUpgradeScreenVisible
        {
            get { return _upgradeScreen.IsVisible; }
        }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        /// <remarks>
        /// Aurora engine does not have upgrade screens, but we delegate to AuroraUpgradeScreen
        /// to maintain API consistency. This is effectively a no-op but maintains proper state.
        /// </remarks>
        public override void HideUpgradeScreen()
        {
            _upgradeScreen.Hide();
        }
    }
}

