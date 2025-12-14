using System.Collections.Generic;
using System.Numerics;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Represents a game area (module area) with rooms and objects.
    /// </summary>
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
    }
}

