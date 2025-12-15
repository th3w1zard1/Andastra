using System.Collections.Generic;
using System.Numerics;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Represents a game area (module area) with rooms and objects.
    /// </summary>
    /// <remarks>
    /// Area Interface:
    /// - Based on swkotor2.exe area system
    /// - Located via string references: "Area" @ 0x007be340, "AreaName" @ 0x007be340, "AREANAME" @ 0x007be1dc
    /// - "AreaId" @ 0x007bef48, "AreaMap" @ 0x007bd118, "AreaMapResX" @ 0x007bd10c, "AreaMapResY" @ 0x007bd100
    /// - "AreaProperties" @ 0x007bd228, "AreaEffectList" @ 0x007bd0d4, "AreaList" @ 0x007c0b7c
    /// - "EVENT_AREA_TRANSITION" @ 0x007bcbdc, "EVENT_REMOVE_FROM_AREA" @ 0x007bcddc
    /// - Error messages: "Area %s is not a valid area." @ 0x007c22bc, "Area %s not valid." @ 0x007c22dc
    /// - Coordinate validation: "X co-ordinate outside of area, should be in [%f, %f]" @ 0x007c224c
    /// - "Y co-ordinate outside of area, should be in [%f, %f]" @ 0x007c2284
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles area events including EVENT_AREA_TRANSITION (case 0x1a) and EVENT_REMOVE_FROM_AREA (case 4)
    /// - Save game integration: FUN_004eb750 @ 0x004eb750 saves AREANAME to save game NFO file
    /// - Pathfinding module: "?nwsareapathfind.cpp" @ 0x007be3ff indicates area pathfinding implementation
    /// - Area number tracking: "AreaNumber" @ 0x007c7324 used for area identification
    /// - Original implementation: Areas loaded from ARE (area properties) and GIT (instances) files
    /// - ARE file format: GFF with "ARE " signature containing area static properties (lighting, fog, grass)
    /// - GIT file format: GFF with "GIT " signature containing dynamic object instances
    /// - Areas contain entities (creatures, doors, placeables, triggers, waypoints, sounds)
    /// - Navigation mesh (walkmesh) provides pathfinding and collision detection
    /// - Based on ARE/GIT file format documentation in vendor/PyKotor/wiki/
    /// </remarks>
    public interface IArea
    {
        /// <summary>
        /// The resource reference name of this area.
        /// </summary>
        string ResRef { get; }

        /// <summary>
        /// The display name of the area.
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// The tag of the area.
        /// </summary>
        string Tag { get; }

        /// <summary>
        /// All creatures in this area.
        /// </summary>
        IEnumerable<IEntity> Creatures { get; }

        /// <summary>
        /// All placeables in this area.
        /// </summary>
        IEnumerable<IEntity> Placeables { get; }

        /// <summary>
        /// All doors in this area.
        /// </summary>
        IEnumerable<IEntity> Doors { get; }

        /// <summary>
        /// All triggers in this area.
        /// </summary>
        IEnumerable<IEntity> Triggers { get; }

        /// <summary>
        /// All waypoints in this area.
        /// </summary>
        IEnumerable<IEntity> Waypoints { get; }

        /// <summary>
        /// All sounds in this area.
        /// </summary>
        IEnumerable<IEntity> Sounds { get; }

        /// <summary>
        /// Gets an object by tag within this area.
        /// </summary>
        IEntity GetObjectByTag(string tag, int nth = 0);

        /// <summary>
        /// Gets the walkmesh navigation system for this area.
        /// </summary>
        INavigationMesh NavigationMesh { get; }

        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        bool IsPointWalkable(Vector3 point);

        /// <summary>
        /// Projects a point onto the walkmesh.
        /// </summary>
        bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height);

        /// <summary>
        /// Gets or sets whether the area is unescapable (players cannot leave).
        /// TRUE means the area cannot be escaped, FALSE means it can be escaped.
        /// </summary>
        bool IsUnescapable { get; set; }
    }
}

