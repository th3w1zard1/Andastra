using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse Engine (Dragon Age Origins, Dragon Age 2) specific waypoint component implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Waypoint Component:
    /// - Based on daorigins.exe, DragonAge2.exe waypoint system
    /// - Waypoint system similar to Aurora/Odyssey but may have engine-specific differences
    /// - Waypoints are invisible markers used for scripting and navigation
    /// - Waypoints can have map notes for player reference (displayed on minimap when MapNoteEnabled is true)
    /// - Waypoints used for: Module transitions (LinkedTo field), script positioning, area navigation, party spawning
    ///
    /// Eclipse-specific details:
    /// - All common waypoint functionality is in BaseWaypointComponent
    /// - Engine-specific properties (if any) will be added here after reverse engineering
    /// </remarks>
    public class EclipseWaypointComponent : BaseWaypointComponent
    {
        public EclipseWaypointComponent()
        {
        }
    }
}

