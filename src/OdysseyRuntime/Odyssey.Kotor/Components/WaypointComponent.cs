using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for waypoint entities.
    /// </summary>
    /// <remarks>
    /// Waypoint Component:
    /// - Based on swkotor2.exe waypoint system
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
    /// </remarks>
    public class WaypointComponent : IComponent
    {
        public IEntity Owner { get; set; }

        public void OnAttach() { }
        public void OnDetach() { }

        public WaypointComponent()
        {
            TemplateResRef = string.Empty;
            MapNote = string.Empty;
        }

        /// <summary>
        /// Template resource reference.
        /// </summary>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Map note text.
        /// </summary>
        public string MapNote { get; set; }

        /// <summary>
        /// Whether the map note is enabled.
        /// </summary>
        public bool MapNoteEnabled { get; set; }

        /// <summary>
        /// Whether this waypoint has a map note.
        /// </summary>
        public bool HasMapNote { get; set; }

        /// <summary>
        /// Appearance type (for visual representation in editor).
        /// </summary>
        public int Appearance { get; set; }

        /// <summary>
        /// Description (localized string reference).
        /// </summary>
        public int Description { get; set; }
    }
}
