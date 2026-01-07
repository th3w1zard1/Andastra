using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Eclipse.Components
{
    /// <summary>
    /// Component for placeable entities (containers, furniture, etc.) in Eclipse engine.
    /// </summary>
    /// <remarks>
    /// Eclipse Placeable Component:
    /// - Inherits from BasePlaceableComponent (common functionality)
    /// - Eclipse-specific implementation for daorigins.exe, DragonAge2.exe, , 
    /// - Based on CCPlaceable class (daorigins.exe, DragonAge2.exe)
    /// - PlaceableList @ 0x00af5028 (daorigins.exe) - Placeable list in area data
    /// - CPlaceable @ 0x00b0d488 (daorigins.exe) - Placeable class name
    /// - COMMAND_GETPLACEABLE* and COMMAND_SETPLACEABLE* functions (daorigins.exe)
    /// - Eclipse uses UnrealScript message passing system instead of direct function calls
    /// - Placeables have appearance, useability, locks, inventory, HP, physics-based interactions
    /// - Script events: OnUsed, OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// - Containers (HasInventory=true) can store items, open/close states
    /// - Lock system: KeyRequired flag, KeyName tag, LockDC difficulty class
    /// - Eclipse-specific: Physics-based placeables, state-based system, different trap system, treasure categories
    /// </remarks>
    public class EclipsePlaceableComponent : BasePlaceableComponent
    {
        public EclipsePlaceableComponent()
        {
            KeyTag = string.Empty;
            Conversation = string.Empty;
        }

        /// <summary>
        /// Template resource reference (Eclipse-specific).
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Placeable base type (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Base Type Property:
        /// - Eclipse-specific: Base type of placeable
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLEBASETYPE @ 0x00af2f60
        /// </remarks>
        public int BaseType { get; set; }

        /// <summary>
        /// Placeable action (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Action Property:
        /// - Eclipse-specific: Current action state of placeable
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLEACTION @ 0x00af2f80, COMMAND_SETPLACEABLEACTIONRESULT @ 0x00af2f9c
        /// </remarks>
        public int Action { get; set; }

        /// <summary>
        /// Placeable state (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// State Property:
        /// - Eclipse-specific: Current state of placeable (state-based system)
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLESTATE @ 0x00af3034, COMMAND_SETPLACEABLESTATE @ 0x00af3018
        /// </remarks>
        public int State { get; set; }

        /// <summary>
        /// Treasure category (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Treasure Category Property:
        /// - Eclipse-specific: Category for treasure generation
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLETREASURECATEGORY @ 0x00af2e90
        /// </remarks>
        public int TreasureCategory { get; set; }

        /// <summary>
        /// Treasure rank (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Treasure Rank Property:
        /// - Eclipse-specific: Rank for treasure generation
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLETREASURERANK @ 0x00af2eb8
        /// </remarks>
        public int TreasureRank { get; set; }

        /// <summary>
        /// Pick lock level (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Pick Lock Level Property:
        /// - Eclipse-specific: Level required to pick lock
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLEPICKLOCKLEVEL @ 0x00af2edc
        /// </remarks>
        public int PickLockLevel { get; set; }

        /// <summary>
        /// Auto remove key flag (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Auto Remove Key Property:
        /// - Eclipse-specific: Whether key is automatically removed after use
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLEAUTOREMOVEKEY @ 0x00af2f3c
        /// </remarks>
        public bool AutoRemoveKey { get; set; }

        /// <summary>
        /// Popup text (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Popup Text Property:
        /// - Eclipse-specific: Text displayed when examining placeable
        /// - Based on daorigins.exe: COMMAND_GETPLACEABLEPOPUPTEXT @ 0x00af2e70
        /// </remarks>
        public string PopupText { get; set; }
    }
}

