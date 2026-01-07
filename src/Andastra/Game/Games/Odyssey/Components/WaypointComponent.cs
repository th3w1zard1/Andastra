using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Odyssey.Components
{
    /// <summary>
    /// Odyssey Engine (KotOR, KotOR2) specific waypoint component implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Waypoint Component:
    /// - Based on swkotor.exe, swkotor2.exe waypoint system
    /// - Located via string references: "WaypointList" @ 0x007bd288 (GIT waypoint list), "Waypoint" @ 0x007bc510 (waypoint entity type)
    /// - "WaypointList" @ 0x007bd060 (waypoint list variant), "STARTWAYPOINT" @ 0x007be034 (start waypoint constant)
    /// - "MapNote" @ 0x007bd10c (map note text field), "MapNoteEnabled" @ 0x007bd118 (map note enabled flag)
    /// - Error messages: "Waypoint template %s doesn't exist.\n" @ 0x007c0f24 (waypoint template not found error)
    /// - Original implementation: FUN_004e08e0 @ 0x004e08e0 loads waypoint instances from GIT
    /// - Waypoint loading: FUN_004e08e0 @ 0x004e08e0 reads waypoint instances from GIT WaypointList, loads UTW templates
    /// - Waypoints are invisible markers used for scripting and navigation (GetWaypointByTag NWScript function)
    /// - UTW file format: GFF with "UTW " signature containing waypoint data (Tag, XPosition, YPosition, ZPosition, MapNote, MapNoteEnabled)
    /// - Waypoints can have map notes for player reference (displayed on minimap when MapNoteEnabled is true)
    /// - GetWaypointByTag NWScript function finds waypoints by tag (searches all waypoints in current area)
    /// - Waypoints used for: Module transitions (LinkedTo field), script positioning, area navigation, party spawning
    /// - STARTWAYPOINT: Special waypoint tag used for module entry positioning (party spawns at STARTWAYPOINT if no TransitionDestination)
    /// - Based on UTW file format documentation in vendor/PyKotor/wiki/
    ///
    /// Odyssey-specific properties:
    /// - Appearance: Appearance type (for visual representation in editor)
    /// - Description: Localized string reference (int) for waypoint description
    /// </remarks>
    public class OdysseyWaypointComponent : BaseWaypointComponent
    {
        private int _appearance;
        private int _description;

        public OdysseyWaypointComponent()
        {
            _appearance = 0;
            _description = 0;
        }

        /// <summary>
        /// Appearance type (for visual representation in editor).
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Appearance identifier used in toolset for visual representation.
        /// Based on Appearance field in UTW GFF structure.
        /// </remarks>
        public int Appearance
        {
            get => _appearance;
            set => _appearance = value;
        }

        /// <summary>
        /// Description (localized string reference).
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Localized string reference (int) for waypoint description.
        /// Based on Description field in UTW GFF structure.
        /// </remarks>
        public int Description
        {
            get => _description;
            set => _description = value;
        }
    }
}
