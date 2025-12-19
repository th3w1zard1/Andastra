using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Infinity.Components
{
    /// <summary>
    /// Infinity Engine (Mass Effect, Mass Effect 2) specific waypoint component implementation.
    /// </summary>
    /// <remarks>
    /// Infinity Waypoint Component:
    /// - Based on MassEffect.exe, MassEffect2.exe waypoint system
    /// - Waypoint system similar to Aurora/Odyssey but may have engine-specific differences
    /// - Waypoints are invisible markers used for scripting and navigation
    /// - Waypoints can have map notes for player reference (displayed on minimap when MapNoteEnabled is true)
    /// - Waypoints used for: Module transitions (LinkedTo field), script positioning, area navigation, party spawning
    ///
    /// Infinity-specific details:
    /// - All common waypoint functionality is in BaseWaypointComponent
    /// - Engine-specific properties (if any) will be added here after reverse engineering
    /// </remarks>
    public class InfinityWaypointComponent : BaseWaypointComponent
    {
        public InfinityWaypointComponent()
        {
        }
    }
}

