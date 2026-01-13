using System.Collections.Generic;
using System.Numerics;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Information about an obstacle to avoid during pathfinding.
    /// </summary>
    public struct ObstacleInfo
    {
        public Vector3 Position;
        public float Radius;

        public ObstacleInfo(Vector3 position, float radius)
        {
            Position = position;
            Radius = radius;
        }
    }

    /// <summary>
    /// Navigation mesh for pathfinding and collision.
    /// </summary>
    /// <remarks>
    /// Navigation Mesh Interface:
    /// - Common interface for all BioWare engine navigation systems
    /// - Implemented through three-tier inheritance structure:
    ///   1. Tier 1 (BaseNavigationMesh): Common functionality shared across ALL engines
    ///      - Location: Runtime.Games.Common.BaseNavigationMesh
    ///      - Contains: Common line-of-sight algorithms, AABB helpers, abstract method contracts
    ///   2. Tier 2 (Engine-specific): Common functionality within an engine family
    ///      - OdysseyNavigationMesh: Common walkmesh functionality for swkotor.exe and swkotor2.exe
    ///      - EclipseNavigationMesh: Common navigation functionality for daorigins.exe and DragonAge2.exe
    ///      - AuroraNavigationMesh: Common navigation functionality for nwmain.exe and nwn2main.exe
    ///   3. Tier 3 (Game-specific): Game-specific implementations
    ///      - Kotor1NavigationMesh: swkotor.exe specific function addresses and behavior
    ///      - Kotor2NavigationMesh: swkotor2.exe specific function addresses and behavior
    ///      - DragonAgeOriginsNavigationMesh: daorigins.exe specific function addresses and behavior
    ///      - DragonAge2NavigationMesh: DragonAge2.exe specific function addresses and behavior
    /// 
    /// Cross-engine navigation system overview:
    /// - Odyssey Engine (swkotor.exe, swkotor2.exe): BWM walkmesh format
    ///   - BWM format: Binary walkmesh format (WOK = area walkmesh, PWK = placeable walkmesh, DWK = door walkmesh)
    ///   - Located via string references: "BWM V1.0" @ 0x007c061c (swkotor2.exe BWM format signature)
    ///   - Triangle-based mesh for walkable surfaces
    ///   - A* pathfinding algorithm on face adjacency graph
    ///   - Surface material walkability based on surfacemat.2da
    /// 
    /// - Eclipse Engine (daorigins.exe, DragonAge2.exe): Advanced navigation system
    ///   - Dynamic obstacle support (movable objects, destructible environments)
    ///   - Real-time pathfinding with cover and tactical positioning
    ///   - Physics-aware navigation with collision avoidance
    ///   - Real-time mesh updates for environmental changes
    ///   - Multi-level navigation (ground, elevated surfaces)
    /// 
    /// - Aurora Engine (nwmain.exe, nwn2main.exe): Tile-based navigation
    ///   - Tile-based pathfinding system
    ///   - Blocking tile checks for line of sight
    /// 
    /// Core navigation mesh operations:
    /// - FindPath: A* pathfinding algorithm from start to goal position
    /// - FindFaceAt: Finds walkmesh face index containing given position
    /// - GetFaceCenter: Returns center point of walkmesh face
    /// - GetAdjacentFaces: Returns neighboring faces for pathfinding traversal
    /// - IsWalkable: Checks if face is walkable (based on surface material lookup in surfacemat.2da)
    /// - GetSurfaceMaterial: Returns surface material index (used for sound effects, walkability)
    /// - Raycast: Line intersection test against walkmesh
    /// - TestLineOfSight: Determines if line between two points is unobstructed
    /// - ProjectToSurface: Projects point onto walkmesh surface (for height correction)
    /// - FindPathAroundObstacles: Finds path while avoiding dynamic obstacles
    /// </remarks>
    public interface INavigationMesh
    {
        /// <summary>
        /// Finds a path from start to goal.
        /// </summary>
        IList<Vector3> FindPath(Vector3 start, Vector3 goal);

        /// <summary>
        /// Finds the face index at a given position.
        /// </summary>
        int FindFaceAt(Vector3 position);

        /// <summary>
        /// Gets the center point of a face.
        /// </summary>
        Vector3 GetFaceCenter(int faceIndex);

        /// <summary>
        /// Gets adjacent faces for a given face.
        /// </summary>
        IEnumerable<int> GetAdjacentFaces(int faceIndex);

        /// <summary>
        /// Checks if a face is walkable.
        /// </summary>
        bool IsWalkable(int faceIndex);

        /// <summary>
        /// Gets the surface material of a face.
        /// </summary>
        int GetSurfaceMaterial(int faceIndex);

        /// <summary>
        /// Performs a raycast against the mesh.
        /// </summary>
        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace);

        /// <summary>
        /// Tests line of sight between two points.
        /// </summary>
        bool TestLineOfSight(Vector3 from, Vector3 to);

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        bool ProjectToSurface(Vector3 point, out Vector3 result, out float height);

        /// <summary>
        /// Projects a point onto the walkmesh (alias for ProjectToSurface for compatibility).
        /// </summary>
        bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height);

        /// <summary>
        /// Projects a point onto the navigation mesh, returning the projected point or null.
        /// </summary>
        Vector3? ProjectPoint(Vector3 point);

        /// <summary>
        /// Checks if a point is walkable (finds face and checks if it's walkable).
        /// </summary>
        bool IsPointWalkable(Vector3 point);

        /// <summary>
        /// Finds a path from start to goal while avoiding obstacles.
        /// </summary>
        /// <remarks>
        /// Obstacle avoidance pathfinding:
        /// - Odyssey Engine: FindPathAroundObstacle function
        ///   - swkotor.exe: FindPathAroundObstacle @ 0x005d0840 (called from UpdateCreatureMovement @ 0x00516630, line 254)
        ///   - swkotor2.exe: FindPathAroundObstacle @ 0x0061c390 (called from UpdateCreatureMovement @ 0x0054be70, line 183)
        /// - Eclipse Engine: Dynamic obstacle-aware pathfinding with real-time updates
        /// - Aurora Engine: Tile-based obstacle avoidance
        /// </remarks>
        /// <param name="start">Start position.</param>
        /// <param name="goal">Goal position.</param>
        /// <param name="obstacles">List of obstacle positions to avoid (with optional radii).</param>
        /// <returns>Path waypoints, or null if no path found.</returns>
        IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<ObstacleInfo> obstacles);
    }
}

