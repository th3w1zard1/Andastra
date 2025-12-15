using Odyssey.Core.Interfaces;

namespace Odyssey.Kotor.Components
{
    /// <summary>
    /// Component for waypoint entities.
    /// </summary>
    /// <remarks>
    /// Waypoint Component:
    /// - Based on swkotor2.exe waypoint system
    /// - Located via string references: Waypoint functions handle waypoint lookup and navigation
    /// - Original implementation: Waypoints are invisible markers used for scripting and navigation
    /// - UTW file format: GFF with "UTW " signature containing waypoint data
    /// - Waypoints can have map notes for player reference
    /// - GetWaypointByTag NWScript function finds waypoints by tag
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
