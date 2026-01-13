using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Navigation;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Odyssey;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Kotor2
{
    /// <summary>
    /// KOTOR 2 (swkotor2.exe) specific navigation mesh implementation.
    /// </summary>
    /// <remarks>
    /// KOTOR 2 Navigation Mesh Implementation:
    /// - Game-specific implementation of Odyssey walkmesh system
    /// - Based on reverse engineering of swkotor2.exe navigation functions
    /// - Inherits common Odyssey walkmesh functionality from OdysseyNavigationMesh
    /// 
    /// KOTOR 2 specific function addresses (swkotor2.exe):
    /// - Walkmesh projection: FUN_004f5070 @ 0x004f5070
    ///   Projects positions to walkmesh surface after movement
    ///   Signature: `float10 __thiscall FUN_004f5070(void *param_1, float *param_2, int param_3, int *param_4, int *param_5)`
    ///   - param_1: Walkmesh object pointer (this)
    ///   - param_2: Input point (float[3] = x, y, z)
    ///   - param_3: Projection mode (0 = 3D projection, 1 = 2D projection)
    ///   - param_4: Output face index pointer (optional, can be null)
    ///   - param_5: Additional parameter (used for 2D projection)
    ///   - Returns: Height (float10) at projected point
    ///   Called from 34 locations including:
    ///   - UpdateCreatureMovement @ 0x0054be70 (line-of-sight and pathfinding)
    ///   - FUN_00553970 @ 0x00553970 (creature movement)
    ///   - FUN_005522e0 @ 0x005522e0 (entity positioning)
    ///   - FUN_004dc300 @ 0x004dc300 (area transition projection)
    ///   - FUN_00517d50 @ 0x00517d50 (AI pathfinding)
    /// 
    /// - FindFaceAt: FUN_004f4260 @ 0x004f4260
    ///   Finds face containing point using AABB tree or brute force
    ///   Signature: `int __thiscall FUN_004f4260(void *this, float *param_1, undefined4 *param_2, int *param_3)`
    ///   - Uses vertical tolerance (_DAT_007b56f8) for point-face matching
    ///   - Returns face pointer or null
    /// 
    /// - UpdateCreatureMovement @ 0x0054be70
    ///   Performs walkmesh raycasts for visibility checks and obstacle avoidance
    ///   Signature: `undefined4 __thiscall FUN_0054be70(int *param_1, float *param_2, float *param_3, float *param_4, int *param_5)`
    ///   - Handles creature movement, pathfinding, and line-of-sight checks
    ///   - Calls FUN_004f5070 for walkmesh projection
    ///   - Calls FindPathAroundObstacle @ 0x0061c390 when creature collision detected
    /// 
    /// - FindPathAroundObstacle @ 0x0061c390
    ///   Pathfinding around obstacles
    ///   Signature: `float* FindPathAroundObstacle(void* this, int* movingCreature, void* blockingCreature)`
    ///   Called from UpdateCreatureMovement @ 0x0054be70 (line 183) when creature collision detected
    /// 
    /// - Pathfinding error messages:
    ///   - "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   - "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
    /// 
    /// - BWM format signature: "BWM V1.0" @ 0x007c061c
    /// 
    /// KOTOR 2 specific behavior:
    /// - Uses same BWM walkmesh format as KOTOR 1
    /// - Same surface material walkability rules
    /// - Same A* pathfinding algorithm
    /// - Same walkmesh projection logic
    /// - Function addresses differ from KOTOR 1 but behavior is identical
    /// </remarks>
    [PublicAPI]
    public class Kotor2NavigationMesh : OdysseyNavigationMesh
    {
        /// <summary>
        /// Creates an empty KOTOR 2 navigation mesh.
        /// </summary>
        public Kotor2NavigationMesh()
            : base()
        {
        }

        /// <summary>
        /// Creates a KOTOR 2 navigation mesh from walkmesh data.
        /// </summary>
        /// <param name="vertices">Walkmesh vertices.</param>
        /// <param name="faceIndices">Face vertex indices (3 per face).</param>
        /// <param name="adjacency">Face adjacency data (3 per face, -1 = no neighbor).</param>
        /// <param name="surfaceMaterials">Surface material indices per face.</param>
        /// <param name="aabbRoot">AABB tree root for spatial acceleration.</param>
        public Kotor2NavigationMesh(
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

