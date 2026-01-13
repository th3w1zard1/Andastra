using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Aurora.Components
{
    /// <summary>
    /// Aurora Engine (Neverwinter Nights) specific waypoint component implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Waypoint Component:
    /// - Based on nwmain.exe waypoint system
    /// - CNWSWaypoint constructor @ 0x140508d60, LoadWaypoint @ 0x140509f80, SaveWaypoint @ 0x14050a4d0
    /// - String references: "WaypointList" @ 0x140ddb7a0, "Tag" @ 0x140dca0f0, "LocalizedName", "MapNote", "MapNoteEnabled"
    /// - LoadWaypoint loads: Tag (CExoString at this+0x20), LocalizedName (CExoLocString at this+800), Position, Orientation, HasMapNote (bool at this+0x308), MapNoteEnabled (bool at this+0x30c), MapNote (CExoLocString at this+0x310)
    /// - SaveWaypoint saves: Tag, LocalizedName, Position (X/Y/Z), Orientation (X/Y/Z), HasMapNote, MapNoteEnabled, MapNote
    /// - Waypoints are invisible markers used for scripting and navigation (GetWaypointByTag NWScript function)
    /// - Waypoints can have map notes for player reference (displayed on minimap when MapNoteEnabled is true)
    /// - GetWaypointByTag NWScript function finds waypoints by tag (searches all waypoints in current area)
    /// - Waypoints used for: Module transitions (LinkedTo field), script positioning, area navigation, party spawning
    /// - STARTWAYPOINT: Special waypoint tag used for module entry positioning (party spawns at STARTWAYPOINT if no TransitionDestination)
    ///
    /// Aurora-specific details:
    /// - LocalizedName: CExoLocString-based localized name (handled via entity DisplayName property, not in component)
    /// - Tag: CExoString-based tag (handled via entity Tag property, not in component)
    /// - All common waypoint functionality is in BaseWaypointComponent
    /// </remarks>
    public class AuroraWaypointComponent : BaseWaypointComponent
    {
        public AuroraWaypointComponent()
        {
        }
    }
}

