using System;

namespace Andastra.Parsing.Formats.BWM
{
    /// <summary>
    /// Represents the type of walkmesh file. This determines how the walkmesh is used in the game
    /// and what features it supports.
    /// </summary>
    /// <remarks>
    /// WHAT IS A WALKMESH TYPE?
    /// 
    /// A walkmesh type tells the game what kind of walkmesh this is. There are two types:
    /// - AreaModel (WOK): Used for entire areas/modules
    /// - PlaceableOrDoor (PWK/DWK): Used for placeable objects and doors
    /// 
    /// WHY DOES IT MATTER?
    /// 
    /// The walkmesh type determines:
    /// 1. Whether an AABB tree is built (only AreaModel walkmeshes get AABB trees)
    /// 2. How vertices are stored (world coordinates vs local coordinates)
    /// 3. What features are available (pathfinding, adjacency, etc.)
    /// 
    /// AREAMODEL (WOK FILES):
    /// 
    /// AreaModel walkmeshes are used for entire game areas/modules. They have:
    /// - Vertices in world coordinates (absolute positions in the game world)
    /// - An AABB tree for fast spatial queries (pathfinding, height calculation, raycasting)
    /// - Adjacency information (which triangles share edges, needed for pathfinding)
    /// - Perimeter edges with transitions (for connecting areas and placing doors)
    /// - Support for all navigation features (pathfinding, height calculation, line of sight)
    /// 
    /// When a walkmesh is converted to a NavigationMesh, the converter checks if the type is
    /// AreaModel before building the AABB tree. If the type is not AreaModel, the AABB tree
    /// is not built, and navigation features will not work correctly.
    /// 
    /// PLACEABLEORDOOR (PWK/DWK FILES):
    /// 
    /// PlaceableOrDoor walkmeshes are used for individual objects (placeables like chests, tables,
    /// or doors). They have:
    /// - Vertices in local coordinates (relative to the object's position)
    /// - No AABB tree (these walkmeshes are small, so brute force is fast enough)
    /// - No adjacency information (not needed for collision-only walkmeshes)
    /// - No pathfinding support (characters don't pathfind on placeables, they just collide with them)
    /// - Only collision detection (characters can't walk through them)
    /// 
    /// CRITICAL BUG FIX:
    /// 
    /// There was a bug where indoor map levels/modules were not walkable even though they had
    /// the correct surface materials. The problem was that the walkmesh type was not being set
    /// to AreaModel when processing walkmeshes in IndoorMap.ProcessBwm().
    /// 
    /// When the walkmesh type is not AreaModel, the BwmToNavigationMeshConverter and
    /// BwmToEclipseNavigationMeshConverter classes skip building the AABB tree. They check:
    /// 
    /// if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
    /// {
    ///     aabbRoot = BuildAabbTree(bwm, vertices, faces);
    /// }
    /// 
    /// Without an AABB tree, the navigation mesh cannot efficiently find which triangle contains
    /// a given point. The AABB tree is a data structure that organizes triangles into boxes,
    /// making it fast to search for triangles. Without it, the system would need to check every
    /// single triangle, which is too slow for real-time gameplay.
    /// 
    /// THE FIX:
    /// 
    /// In IndoorMap.ProcessBwm(), we now always set:
    /// bwm.WalkmeshType = BWMType.AreaModel;
    /// 
    /// This ensures that when the walkmesh is converted to a NavigationMesh, the AABB tree will
    /// be built, and all navigation features will work correctly.
    /// 
    /// ORIGINAL IMPLEMENTATION:
    /// 
    /// Based on swkotor2.exe: The walkmesh type is stored in the BWM file header. When loading
    /// a walkmesh, the engine checks the type to determine how to process it. Area walkmeshes
    /// (WOK) get full navigation support, while placeable/door walkmeshes (PWK/DWK) are used
    /// only for collision detection.
    /// 
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:96-124
    /// Original: class BWMType(IntEnum)
    /// </remarks>
    public enum BWMType
    {
        /// <summary>
        /// Placeable or door walkmesh (PWK/DWK files).
        /// Used for individual objects like chests, tables, and doors.
        /// Collision-only, no AABB tree, no pathfinding support.
        /// </summary>
        PlaceableOrDoor = 0,

        /// <summary>
        /// Area model walkmesh (WOK files).
        /// Used for entire game areas/modules.
        /// Full navigation support: AABB tree, adjacency, pathfinding, height calculation, raycasting.
        /// </summary>
        AreaModel = 1
    }
}

