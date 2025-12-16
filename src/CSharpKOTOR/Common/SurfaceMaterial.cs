using System;
using System.Collections.Generic;
using System.Linq;

namespace BioWareCSharp.Common
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
    public static class SurfaceMaterialExtensions
    {
        private static readonly HashSet<SurfaceMaterial> WalkableMaterials = new HashSet<SurfaceMaterial>
        {
            SurfaceMaterial.Dirt,
            SurfaceMaterial.Grass,
            SurfaceMaterial.Stone,
            SurfaceMaterial.Wood,
            SurfaceMaterial.Water,
            SurfaceMaterial.Carpet,
            SurfaceMaterial.Metal,
            SurfaceMaterial.Puddles,
            SurfaceMaterial.Swamp,
            SurfaceMaterial.Mud,
            SurfaceMaterial.Leaves,
            SurfaceMaterial.Door,
            SurfaceMaterial.Trigger
        };

        public static bool Walkable(this SurfaceMaterial material)
        {
            return WalkableMaterials.Contains(material);
        }
    }
}

