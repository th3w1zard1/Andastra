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
    /// - Located via string references: Area loading and management functions
    /// - Original implementation: Areas loaded from ARE (area properties) and GIT (instances) files
    /// - ARE file format: GFF with "ARE " signature containing area static properties
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

