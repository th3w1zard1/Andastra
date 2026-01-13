using System;
using BioWare.NET;
using BioWare.NET.Extract.Installation;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Eclipse
{
    /// <summary>
    /// UI system implementation for Eclipse engine (Dragon Age, ).
    /// </summary>
    /// <remarks>
    /// UI System Implementation:
    /// - Eclipse-specific UI system implementation inheriting from BaseUISystem
    /// - Based on daorigins.exe, DragonAge2.exe, ,  UI systems
    /// - Eclipse engine uses advanced UI system with crafting, inventory, and character progression screens
    /// - Enhanced screen management with transitions and cinematic overlays
    ///
    /// Based on reverse engineering:
    /// - daorigins.exe: Advanced UI system with crafting screens and inventory management
    ///   - ItemUpgrade system with GUIItemUpgrade class (daorigins.exe: ItemUpgrade @ 0x00aef22c, GUIItemUpgrade @ 0x00b02ca0, COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c)
    /// - DragonAge2.exe: Enhanced UI system with character progression and ability screens
    ///   - ItemUpgrade system with GUIItemUpgrade class (DragonAge2.exe: ItemUpgrade @ 0x00beb1f0, GUIItemUpgrade @ 0x00beb1d0, UpgradePrereqType @ 0x00c0583c, GetAbilityUpgradedValue @ 0x00c0f20c)
    /// - : Modern UI system with cinematic overlays and dialogue system
    ///   - No item upgrade system (only vehicle upgrades)
    /// - : Advanced UI system with inventory, character, and mission screens
    ///   - No item upgrade system (only vehicle upgrades)
    ///
    /// Note: Dragon Age games (Origins, DA2) have ItemUpgrade systems similar to Odyssey but with different implementation.
    ///  games do not have item upgrade systems.
    /// </remarks>
    public class EclipseUISystem : BaseUISystem
    {
        private readonly IUpgradeScreen _upgradeScreen;

        /// <summary>
        /// Initializes a new instance of the UI system.
        /// </summary>
        /// <param name="installation">Game installation for accessing game data.</param>
        /// <param name="world">World context for entity access.</param>
        public EclipseUISystem(Installation installation, IWorld world)
            : base(world)
        {
            if (installation == null)
            {
                throw new ArgumentNullException("installation");
            }

            // Create Eclipse upgrade screen
            // Dragon Age games support item upgrades,  games do not
            _upgradeScreen = new EclipseUpgradeScreen(installation, world);
        }

        /// <summary>
        /// Eclipse-specific implementation of upgrade screen display.
        /// </summary>
        /// <param name="item">Item to upgrade (validated by base class).</param>
        /// <param name="character">Character whose skills will be used (validated by base class).</param>
        /// <param name="disableItemCreation">If true, disable item creation screen.</param>
        /// <param name="disableUpgrade">If true, force straight to item creation and disable upgrading.</param>
        /// <param name="override2DA">Override 2DA file name (empty string for default).</param>
        /// <remarks>
        /// Based on reverse engineering:
        /// - daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c opens ItemUpgrade GUI
        /// - DragonAge2.exe: GUIItemUpgrade class structure handles upgrade screen display
        /// -  games: No upgrade screen support (method will handle gracefully)
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
            // Based on daorigins.exe: COMMAND_OPENITEMUPGRADEGUI opens GUIItemUpgrade screen
            _upgradeScreen.TargetItem = itemEntity;
            _upgradeScreen.Character = characterEntity;
            _upgradeScreen.DisableItemCreation = disableItemCreation;
            _upgradeScreen.DisableUpgrade = disableUpgrade;
            _upgradeScreen.Override2DA = override2DA;

            // Show upgrade screen
            // Based on daorigins.exe: COMMAND_OPENITEMUPGRADEGUI @ 0x00af1c7c
            // Based on DragonAge2.exe: GUIItemUpgrade class handles screen display
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

