namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for waypoint entities.
    /// </summary>
    /// <remarks>
    /// Waypoint Component Interface:
    /// - Common interface for waypoint functionality across all BioWare engines
    /// - Base implementation: BaseWaypointComponent in Runtime.Games.Common.Components
    /// - Engine-specific implementations:
    ///   - Odyssey: OdysseyWaypointComponent (swkotor.exe, swkotor2.exe)
    ///   - Aurora: AuroraWaypointComponent (nwmain.exe)
    ///   - Eclipse: EclipseWaypointComponent (daorigins.exe, DragonAge2.exe) - if waypoints are supported
    ///   - Infinity: InfinityWaypointComponent (MassEffect.exe, MassEffect2.exe) - if waypoints are supported
    /// - Common functionality: Template reference, Map notes, Basic waypoint operations
    /// - Waypoints are invisible markers used for scripting and navigation
    /// - Waypoints can have map notes for player reference (displayed on minimap when MapNoteEnabled is true)
    /// - GetWaypointByTag functions find waypoints by tag (searches all waypoints in current area)
    /// - Waypoints used for: Module transitions (LinkedTo field), script positioning, area navigation, party spawning
    /// - STARTWAYPOINT: Special waypoint tag used for module entry positioning (party spawns at STARTWAYPOINT if no TransitionDestination)
    /// </remarks>
    public interface IWaypointComponent : IComponent
    {
        /// <summary>
        /// Template resource reference.
        /// </summary>
        string TemplateResRef { get; set; }

        /// <summary>
        /// Map note text.
        /// </summary>
        string MapNote { get; set; }

        /// <summary>
        /// Whether the map note is enabled.
        /// </summary>
        bool MapNoteEnabled { get; set; }

        /// <summary>
        /// Whether this waypoint has a map note.
        /// </summary>
        bool HasMapNote { get; set; }
    }
}

