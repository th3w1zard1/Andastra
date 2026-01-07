using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Component for placeable entities (containers, furniture, etc.) in Aurora engine.
    /// </summary>
    /// <remarks>
    /// Aurora Placeable Component:
    /// - Inherits from BasePlaceableComponent (common functionality)
    /// - Aurora-specific implementation for nwmain.exe
    /// - CNWSPlaceable::LoadPlaceable @ 0x1404b4900 (nwmain.exe) - Loads placeable data from GFF
    /// - CNWSPlaceable::SavePlaceable @ 0x1404b6a60 (nwmain.exe) - Saves placeable data to GFF
    /// - LoadPlaceables @ 0x1403619e0 (nwmain.exe) - Loads placeable list from GIT
    /// - SavePlaceables @ 0x140367260 (nwmain.exe) - Saves placeable list to GIT
    /// - Located via string reference: "Placeable List" @ 0x140ddb7c0 (GFF list field in GIT)
    /// - Based on UTP file format (GFF with "UTP " signature), similar to Odyssey
    /// - Placeables have appearance, useability, locks, inventory, HP, traps, lighting
    /// - Script events: OnUsed, OnOpen, OnClose, OnLock, OnUnlock, OnDamaged, OnDeath
    /// - Containers (HasInventory=true) can store items, open/close states
    /// - Lock system: KeyRequired flag, KeyName tag, LockDC difficulty class
    /// - Aurora-specific: GroundPile, Portrait, LightState, Description, Portal, trap system differences
    /// </remarks>
    public class AuroraPlaceableComponent : BasePlaceableComponent
    {
        public AuroraPlaceableComponent()
        {
            KeyTag = string.Empty;
            Conversation = string.Empty;
        }

        /// <summary>
        /// Template resource reference (Aurora-specific).
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Ground pile flag (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Ground Pile Property:
        /// - Aurora-specific: Whether placeable is a ground pile (items drop on ground)
        /// - Based on nwmain.exe: "GroundPile" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 58)
        /// </remarks>
        public bool GroundPile { get; set; }

        /// <summary>
        /// Portrait ID or ResRef (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Portrait Property:
        /// - Aurora-specific: Portrait ID or ResRef for placeable
        /// - Based on nwmain.exe: "Portrait" or "PortraitId" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, lines 79-84)
        /// </remarks>
        public string Portrait { get; set; }

        /// <summary>
        /// Light state (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Light State Property:
        /// - Aurora-specific: Whether placeable light is on
        /// - Based on nwmain.exe: "LightState" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 90)
        /// </remarks>
        public bool LightState { get; set; }

        /// <summary>
        /// Description (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Description Property:
        /// - Aurora-specific: Description text for placeable
        /// - Based on nwmain.exe: "Description" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 92)
        /// </remarks>
        public string Description { get; set; }

        /// <summary>
        /// Portal destination (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Portal Property:
        /// - Aurora-specific: Portal destination tag for placeable
        /// - Based on nwmain.exe: "Portal" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 94)
        /// </remarks>
        public string Portal { get; set; }

        /// <summary>
        /// Whether a key is required (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Key Required Property:
        /// - Aurora-specific: Whether a key is required to unlock
        /// - Based on nwmain.exe: "KeyRequired" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 70)
        /// </remarks>
        public bool KeyRequired { get; set; }

        /// <summary>
        /// Close lock DC (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Close Lock DC Property:
        /// - Aurora-specific: Difficulty class for closing lock
        /// - Based on nwmain.exe: "CloseLockDC" field in CNWSPlaceable (SavePlaceable @ 0x1404b6a60, line 73)
        /// </remarks>
        public int CloseLockDC { get; set; }
    }
}

