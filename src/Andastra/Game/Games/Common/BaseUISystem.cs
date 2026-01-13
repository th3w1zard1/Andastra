using System;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of UI system functionality shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base UI System Implementation:
    /// - Common UI system framework across all engines
    /// - Provides base implementation for screen management and visibility tracking
    /// - Engine-specific subclasses implement engine-specific UI features
    ///
    /// Based on reverse engineering of UI systems across engines:
    /// - Odyssey: GUI panel-based UI system with screen stack
    /// - Aurora: Scene-based GUI system with message-based transitions
    /// - Eclipse: Advanced UI system with crafting and character progression screens
    /// - Infinity: Modern UI system with cinematic overlays
    ///
    /// Common functionality across engines:
    /// - Screen visibility tracking
    /// - Screen show/hide operations
    /// - Entity validation for UI operations
    /// - Screen state management
    /// </remarks>
    [PublicAPI]
    public abstract class BaseUISystem : IUISystem
    {
        protected readonly IWorld _world;

        /// <summary>
        /// Initializes a new instance of the base UI system.
        /// </summary>
        /// <param name="world">World context for entity access.</param>
        protected BaseUISystem(IWorld world)
        {
            if (world == null)
            {
                throw new ArgumentNullException("world");
            }

            _world = world;
        }

        /// <summary>
        /// Shows the upgrade screen for item modification.
        /// </summary>
        /// <param name="item">Item to upgrade (OBJECT_INVALID for all items).</param>
        /// <param name="character">Character whose skills will be used (OBJECT_INVALID for player).</param>
        /// <param name="disableItemCreation">If true, disable item creation screen.</param>
        /// <param name="disableUpgrade">If true, force straight to item creation and disable upgrading.</param>
        /// <param name="override2DA">Override 2DA file name (empty string for default).</param>
        /// <remarks>
        /// Base implementation provides common validation logic.
        /// Engine-specific subclasses implement the actual screen display.
        /// </remarks>
        public virtual void ShowUpgradeScreen(uint item, uint character, bool disableItemCreation, bool disableUpgrade, string override2DA)
        {
            // Common validation: OBJECT_INVALID = 0x7FFFFFFF (uint.MaxValue)
            const uint ObjectInvalid = 0x7FFFFFFF;

            // Validate item entity if specified
            if (item != 0 && item != ObjectInvalid)
            {
                IEntity itemEntity = _world.GetEntity(item);
                if (itemEntity == null)
                {
                    // Item not found, return early (matches original engine behavior)
                    return;
                }
            }

            // Validate character entity if specified
            if (character != 0 && character != ObjectInvalid)
            {
                IEntity characterEntity = _world.GetEntity(character);
                if (characterEntity == null)
                {
                    // Character not found, return early
                    return;
                }
            }

            // Engine-specific implementation handles actual screen display
            ShowUpgradeScreenImpl(item, character, disableItemCreation, disableUpgrade, override2DA ?? string.Empty);
        }

        /// <summary>
        /// Engine-specific implementation of upgrade screen display.
        /// </summary>
        /// <param name="item">Item to upgrade (validated).</param>
        /// <param name="character">Character whose skills will be used (validated).</param>
        /// <param name="disableItemCreation">If true, disable item creation screen.</param>
        /// <param name="disableUpgrade">If true, force straight to item creation and disable upgrading.</param>
        /// <param name="override2DA">Override 2DA file name (empty string for default).</param>
        protected abstract void ShowUpgradeScreenImpl(uint item, uint character, bool disableItemCreation, bool disableUpgrade, string override2DA);

        /// <summary>
        /// Gets whether the upgrade screen is currently visible.
        /// </summary>
        public abstract bool IsUpgradeScreenVisible { get; }

        /// <summary>
        /// Hides the upgrade screen.
        /// </summary>
        public abstract void HideUpgradeScreen();
    }
}

