using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Games.Odyssey;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Odyssey.Kotor1
{
    /// <summary>
    /// KOTOR 1 (swkotor.exe) specific navigation mesh implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 1 Navigation Mesh Implementation:
    /// - Game-specific implementation of Odyssey walkmesh system
    /// - Based on reverse engineering of swkotor.exe navigation functions
    /// - Inherits common Odyssey walkmesh functionality from OdysseyNavigationMesh
    /// 
    /// KOTOR 1 specific function addresses (swkotor.exe):
    /// - FindPathAroundObstacle @ 0x005d0840 - pathfinding around obstacles
    ///   Called from UpdateCreatureMovement @ 0x00516630 (line 254) when creature collision detected
    /// - UpdateCreatureMovement @ 0x00516630 - creature movement and pathfinding
    ///   Performs walkmesh raycasts for visibility checks and obstacle avoidance
    /// - Pathfinding error messages:
    ///   - "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007456e0
    ///   - "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x00745688
    /// 
    /// KOTOR 1 specific behavior:
    /// - Uses same BWM walkmesh format as KOTOR 2
    /// - Same surface material walkability rules
    /// - Same A* pathfinding algorithm
    /// - Same walkmesh projection logic
    /// - Function addresses differ from KOTOR 2 but behavior is identical
    /// </remarks>
    [PublicAPI]
    public class Kotor1NavigationMesh : OdysseyNavigationMesh
    {
        /// <summary>
        /// Creates an empty KOTOR 1 navigation mesh.
        /// </summary>
        public Kotor1NavigationMesh()
            : base()
        {
        }

        /// <summary>
        /// Creates a KOTOR 1 navigation mesh from walkmesh data.
        /// </summary>
        /// <param name="vertices">Walkmesh vertices.</param>
        /// <param name="faceIndices">Face vertex indices (3 per face).</param>
        /// <param name="adjacency">Face adjacency data (3 per face, -1 = no neighbor).</param>
        /// <param name="surfaceMaterials">Surface material indices per face.</param>
        /// <param name="aabbRoot">AABB tree root for spatial acceleration.</param>
        public Kotor1NavigationMesh(
            Vector3[] vertices,
            int[] faceIndices,
            int[] adjacency,
            int[] surfaceMaterials,
            NavigationMesh.AabbNode aabbRoot)
            : base(vertices, faceIndices, adjacency, surfaceMaterials, aabbRoot)
        {
        }
    }
}

