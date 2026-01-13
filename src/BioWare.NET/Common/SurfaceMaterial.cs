using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;

namespace BioWare.NET.Common
{
    // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1037-1091
    // Original: class SurfaceMaterial(IntEnum)
    public enum SurfaceMaterial
    {
        // as according to 'surfacemat.2da'
        Undefined = 0,
        Dirt = 1,
        Obscuring = 2,
        Grass = 3,
        Stone = 4,
        Wood = 5,
        Water = 6,
        NonWalk = 7,
        Transparent = 8,
        Carpet = 9,
        Metal = 10,
        Puddles = 11,
        Swamp = 12,
        Mud = 13,
        Leaves = 14,
        Lava = 15,
        BottomlessPit = 16,
        DeepWater = 17,
        Door = 18,
        NonWalkGrass = 19,
        SurfaceMaterial20 = 20,
        SurfaceMaterial21 = 21,
        SurfaceMaterial22 = 22,
        SurfaceMaterial23 = 23,
        SurfaceMaterial24 = 24,
        SurfaceMaterial25 = 25,
        SurfaceMaterial26 = 26,
        SurfaceMaterial27 = 27,
        SurfaceMaterial28 = 28,
        SurfaceMaterial29 = 29,
        Trigger = 30
    }

    // Matching PyKotor implementation at vendor/HoloLSP/vendor/pykotor/common/geometry.py:1073-1091
    // Original: def walkable(self) -> bool
    // CRITICAL: This must match NavigationMesh.WalkableMaterials exactly!
    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Surface material walkability is hardcoded in the engine
    // Located via cross-reference: NavigationMesh.IsWalkable checks material IDs against walkable set
    // Original implementation: Material walkability is determined by material ID lookup, not surfacemat.2da
    public static class SurfaceMaterialExtensions
    {
        // Walkable materials (must match NavigationMesh.WalkableMaterials exactly)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Material IDs 1, 3, 4, 5, 6, 9, 10, 11, 12, 13, 14, 16, 18, 20, 21, 22, 30 are walkable
        // Material 16 (BottomlessPit): Walkable but dangerous, 10x pathfinding cost, AI avoids if possible
        // Material 20 (Sand): Walkable, normal movement
        // Material 21 (BareBones): Walkable, normal movement
        // Material 22 (StoneBridge): Walkable, normal movement
        // These materials were missing from the original PyKotor implementation but are present in the game engine
        private static readonly HashSet<SurfaceMaterial> WalkableMaterials = new HashSet<SurfaceMaterial>
        {
            SurfaceMaterial.Dirt,           // 1: Walkable, normal movement
            SurfaceMaterial.Grass,          // 3: Walkable, normal movement
            SurfaceMaterial.Stone,          // 4: Walkable, normal movement, default for generated walkmeshes
            SurfaceMaterial.Wood,           // 5: Walkable, normal movement
            SurfaceMaterial.Water,          // 6: Walkable, slower movement, 1.5x pathfinding cost (shallow water)
            SurfaceMaterial.Carpet,         // 9: Walkable, normal movement
            SurfaceMaterial.Metal,          // 10: Walkable, normal movement
            SurfaceMaterial.Puddles,        // 11: Walkable, slower movement, 1.5x pathfinding cost
            SurfaceMaterial.Swamp,          // 12: Walkable, slower movement, 1.5x pathfinding cost
            SurfaceMaterial.Mud,            // 13: Walkable, slower movement, 1.5x pathfinding cost
            SurfaceMaterial.Leaves,         // 14: Walkable, normal movement
            SurfaceMaterial.BottomlessPit,  // 16: Walkable but dangerous, 10x pathfinding cost, AI avoids if possible
            SurfaceMaterial.Door,           // 18: Walkable, normal movement
            SurfaceMaterial.SurfaceMaterial20, // 20: Sand - Walkable, normal movement
            SurfaceMaterial.SurfaceMaterial21, // 21: BareBones - Walkable, normal movement
            SurfaceMaterial.SurfaceMaterial22, // 22: StoneBridge - Walkable, normal movement
            SurfaceMaterial.Trigger         // 30: Walkable, PyKotor extended material
        };

        /// <summary>
        /// Checks if a surface material is walkable.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Material walkability is hardcoded in the engine based on material ID.
        /// This method must match NavigationMesh.WalkableMaterials exactly to ensure consistency.
        /// When BWM.WalkableFaces() is called, it uses this method to filter faces, so any mismatch
        /// will cause incorrect walkability determination in the indoor map builder and other tools.
        /// </remarks>
        public static bool Walkable(this SurfaceMaterial material)
        {
            return WalkableMaterials.Contains(material);
        }
    }
}
