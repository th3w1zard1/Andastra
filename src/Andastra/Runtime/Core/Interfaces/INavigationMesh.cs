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
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - TODO: this should NOT specify swkotor2.exe unless it specifies the other exes as well!!!
    /// - Based on swkotor2.exe walkmesh/navigation system
    /// - Located via string references: "BWM V1.0" @ 0x007c061c (BWM format signature), "BWM" (walkmesh format), walkmesh-related functions
    /// - BWM format: Binary walkmesh format (WOK = area walkmesh, PWK = placeable walkmesh, DWK = door walkmesh)
    /// - FindPath: A* pathfinding algorithm from start to goal position
    /// - FindFaceAt: Finds walkmesh face index containing given position
    /// - GetFaceCenter: Returns center point of walkmesh face
    /// - GetAdjacentFaces: Returns neighboring faces for pathfinding traversal
    /// - IsWalkable: Checks if face is walkable (based on surface material lookup in surfacemat.2da)
    /// - GetSurfaceMaterial: Returns surface material index (used for sound effects, walkability)
    /// - Raycast: Line intersection test against walkmesh
    /// - TestLineOfSight: Determines if line between two points is unobstructed
    /// - ProjectToSurface: Projects point onto walkmesh surface (for height correction)
    /// - Walkmesh projection: FUN_004f5070 @ 0x004f5070 projects positions to walkmesh surface after movement
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
        /// Finds a path from start to goal while avoiding obstacles.
        /// Based on swkotor2.exe: FUN_0054a1f0 @ 0x0054a1f0 - pathfinding around obstacles
        /// </summary>
        /// <param name="start">Start position.</param>
        /// <param name="goal">Goal position.</param>
        /// <param name="obstacles">List of obstacle positions to avoid (with optional radii).</param>
        /// <returns>Path waypoints, or null if no path found.</returns>
        IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<ObstacleInfo> obstacles);
    }
}

