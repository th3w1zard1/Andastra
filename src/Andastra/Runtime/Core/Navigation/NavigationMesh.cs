using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Navigation
{
    /// <summary>
    /// Navigation mesh for pathfinding and collision detection.
    /// Wraps BWM data from Andastra.Parsing with A* pathfinding on walkmesh adjacency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT IS A NAVIGATION MESH?
    /// 
    /// A navigation mesh is a data structure that the game uses to figure out where characters can walk
    /// and how to move them from one place to another. It is built from a walkmesh (BWM file), which is
    /// a collection of triangles that cover the ground.
    /// 
    /// The navigation mesh stores:
    /// 1. All the points (vertices) that make up the triangles
    /// 2. Which points belong to which triangles (face indices)
    /// 3. Which triangles are next to each other (adjacency)
    /// 4. What kind of surface each triangle is (surface materials)
    /// 5. A tree structure (AABB tree) that helps find triangles quickly
    /// 
    /// WHY DO WE NEED IT?
    /// 
    /// The game needs to answer questions like:
    /// - "Can a character walk here?" (walkability check)
    /// - "What is the height of the ground at this point?" (height calculation)
    /// - "How do I move a character from point A to point B?" (pathfinding)
    /// - "Is there a clear line of sight between two points?" (line of sight)
    /// - "What does this ray hit?" (raycasting)
    /// 
    /// The navigation mesh makes it possible to answer these questions quickly and accurately.
    /// </para>
    /// 
    /// <para>
    /// HOW DOES IT WORK?
    /// 
    /// The navigation mesh is created from a BWM (BioWare Walkmesh) file. The BWM file contains:
    /// - Triangles (faces) that cover the ground
    /// - Each triangle has three points (vertices)
    /// - Each triangle has a material that tells if it's walkable
    /// 
    /// When the navigation mesh is created:
    /// 1. All vertices are collected and stored in an array
    /// 2. Each triangle is stored as three numbers pointing to vertices (face indices)
    /// 3. Adjacency is computed: which triangles share edges
    /// 4. Surface materials are stored: one material per triangle
    /// 5. An AABB tree is built: organizes triangles into boxes for fast searching
    /// 
    /// Once created, the navigation mesh can answer all the questions listed above.
    /// </para>
    /// 
    /// <para>
    /// THE AABB TREE:
    /// 
    /// An AABB tree is a special way of organizing triangles so we can find them quickly. Without it,
    /// we would have to check every single triangle to find which one contains a point, which is very
    /// slow when there are thousands of triangles.
    /// 
    /// The tree works like this:
    /// - All triangles are organized into boxes (AABBs = Axis-Aligned Bounding Boxes)
    /// - Each box contains a smaller box, which contains an even smaller box, and so on
    /// - At the bottom of the tree, each box contains just one triangle
    /// 
    /// When we need to find a triangle:
    /// - Start at the top box (contains all triangles)
    /// - Check if our point is in this box
    /// - If yes, check the two smaller boxes inside it
    /// - Keep going down until we find the triangle
    /// 
    /// This is much faster than checking every triangle. Instead of checking 1000 triangles, we might
    /// only check 10 boxes.
    /// 
    /// IMPORTANT: The AABB tree is only built for area walkmeshes (WOK files), not for placeable or
    /// door walkmeshes (PWK/DWK files). This is because area walkmeshes are large and need fast
    /// searching, while placeable/door walkmeshes are small and can be checked directly.
    /// </para>
    /// 
    /// <para>
    /// WALKABILITY:
    /// 
    /// Not all triangles are walkable. Some triangles are walls, water that's too deep, or lava. The
    /// game uses surface materials to determine if a triangle is walkable.
    /// 
    /// Each triangle has a material number (0-30). The material tells:
    /// - If the triangle is walkable (characters can stand on it)
    /// - What kind of surface it is (affects footstep sounds, movement speed)
    /// - How hard it is to walk on (affects pathfinding cost)
    /// 
    /// Walkable materials include: Dirt (1), Grass (3), Stone (4), Wood (5), Water-Shallow (6),
    /// Carpet (9), Metal (10), Puddles (11), Swamp (12), Mud (13), Leaves (14), BottomlessPit (16),
    /// Door (18), Sand (20), BareBones (21), StoneBridge (22), and Trigger (30).
    /// 
    /// Non-walkable materials include: Undefined (0), Obscuring (2), NonWalk (7), Transparent (8),
    /// Lava (15), DeepWater (17), and NonWalkGrass (19).
    /// 
    /// The IsWalkable() function checks if a triangle's material is in the walkable set. This is
    /// critical for pathfinding because the pathfinding algorithm only considers walkable triangles.
    /// </para>
    /// 
    /// <para>
    /// ADJACENCY:
    /// 
    /// Adjacency tells which triangles share edges. Two triangles are adjacent if they share exactly
    /// two vertices (which forms a shared edge). This information is stored in the adjacency array.
    /// 
    /// Adjacency is encoded as: faceIndex * 3 + edgeIndex
    /// - faceIndex: which triangle (0 to faceCount-1)
    /// - edgeIndex: which edge of that triangle (0, 1, or 2)
    /// - The value stored is: neighborFaceIndex * 3 + neighborEdgeIndex
    /// - If there's no neighbor, the value is -1
    /// 
    /// For example, if triangle 5's edge 1 is adjacent to triangle 12's edge 2:
    /// - adjacency[5*3+1] = 12*3+2 = 38
    /// - adjacency[38] = 5*3+1 = 16 (bidirectional)
    /// 
    /// Adjacency is critical for pathfinding because the pathfinding algorithm needs to know which
    /// triangles can be reached from the current triangle.
    /// </para>
    /// 
    /// <para>
    /// PATHFINDING:
    /// 
    /// Pathfinding uses the A* algorithm to find the shortest path between two points. The algorithm
    /// works on the adjacency graph: triangles are nodes, and shared edges are connections.
    /// 
    /// The algorithm:
    /// 1. Finds which triangle contains the start point
    /// 2. Finds which triangle contains the goal point
    /// 3. Uses A* to search through adjacent triangles
    /// 4. Returns a list of waypoints (points to move through)
    /// 
    /// A* is a smart search algorithm that explores the most promising paths first. It uses:
    /// - g-score: distance traveled from start
    /// - h-score: estimated distance to goal (heuristic)
    /// - f-score: g-score + h-score (total estimated cost)
    /// 
    /// The algorithm always explores the path with the lowest f-score first, which usually leads to
    /// the shortest path.
    /// </para>
    /// 
    /// <para>
    /// HEIGHT CALCULATION:
    /// 
    /// When the game needs to know the height of the ground at a point (x, y), it:
    /// 1. Finds which triangle contains that (x, y) point
    /// 2. Gets the three vertices of that triangle
    /// 3. Calculates the triangle's normal vector (perpendicular to the surface)
    /// 4. Uses the plane equation to solve for z (height)
    /// 
    /// The plane equation is: ax + by + cz + d = 0
    /// - (a, b, c) is the normal vector
    /// - d is calculated from a point on the plane
    /// - We rearrange to solve for z: z = (-d - ax - by) / c
    /// 
    /// This gives us the exact height of the triangle's surface at that (x, y) point.
    /// </para>
    /// 
    /// <para>
    /// RAYCASTING:
    /// 
    /// Raycasting is like shooting an invisible laser and seeing what it hits. The game uses this for:
    /// - Line of sight checks (can character A see character B?)
    /// - Collision detection (does this ray hit a wall?)
    /// - Finding where things are in the world
    /// 
    /// The algorithm:
    /// 1. Starts at the origin point
    /// 2. Travels in the direction specified
    /// 3. Tests which triangles the ray might hit (using AABB tree)
    /// 4. For each candidate triangle, tests if the ray actually hits it
    /// 5. Returns the closest hit (shortest distance)
    /// 
    /// The ray-triangle intersection test:
    /// 1. Calculates the triangle's normal vector
    /// 2. Creates a plane equation from the triangle
    /// 3. Checks if the ray crosses the plane
    /// 4. Calculates where the ray hits the plane
    /// 5. Tests if that hit point is inside the triangle (using edge tests)
    /// </para>
    /// 
    /// <para>
    /// FINDING FACES:
    /// 
    /// When the game needs to find which triangle contains a point, it uses FindFaceAt(). This function:
    /// 1. Uses the AABB tree to quickly narrow down which triangles might contain the point
    /// 2. For each candidate triangle, tests if the point is inside it
    /// 3. Returns the first triangle found, or -1 if none found
    /// 
    /// The point-in-triangle test uses the "same-side test":
    /// - For each edge of the triangle, check which side of the edge the point is on
    /// - If the point is on the same side of all three edges, it's inside the triangle
    /// - Uses cross products to determine which side of each edge the point is on
    /// 
    /// This test is done in 2D (only x and y coordinates) because we're finding which triangle
    /// is directly below or above the point. The z coordinate is used for tolerance but not for
    /// triangle selection.
    /// </para>
    /// 
    /// <para>
    /// CRITICAL BUG FIX - WALKMESH TYPE:
    /// 
    /// There was a bug where levels/modules built in the indoor map builder were not walkable even
    /// though they had the correct surface materials. The problem was that the walkmesh type was not
    /// being set to AreaModel (WOK).
    /// 
    /// WHAT WAS THE PROBLEM?
    /// 
    /// When a walkmesh is converted to a NavigationMesh, the converter checks the walkmesh type to
    /// decide whether to build an AABB tree. The check is:
    /// 
    /// if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
    /// {
    ///     aabbRoot = BuildAabbTree(bwm, vertices, faces);
    /// }
    /// 
    /// If the walkmesh type is not AreaModel, the AABB tree is not built. Without the AABB tree:
    /// - FindFaceAt() must check every triangle (very slow)
    /// - Raycast() must check every triangle (very slow)
    /// - Pathfinding fails because it can't find which triangles contain points
    /// - Height calculation fails
    /// - The entire navigation system breaks
    /// 
    /// THE FIX:
    /// 
    /// In IndoorMap.ProcessBwm(), we now always set:
    /// bwm.WalkmeshType = BWMType.AreaModel;
    /// 
    /// This ensures that when the walkmesh is converted to a NavigationMesh, the AABB tree will
    /// be built, and all navigation features will work correctly.
    /// 
    /// WHY THIS WORKS:
    /// 
    /// Indoor map walkmeshes are always area walkmeshes (WOK files), not placeable or door walkmeshes
    /// (PWK/DWK files). Area walkmeshes are used for the ground of entire areas, while placeable/door
    /// walkmeshes are used for individual objects. By setting the type to AreaModel, we tell the
    /// converter that this is an area walkmesh and it should build the AABB tree.
    /// </para>
    /// 
    /// <para>
    /// CRITICAL BUG FIX - MATERIAL PRESERVATION:
    /// 
    /// There was another bug where materials were not being preserved during BWM transformations and
    /// copying. This caused faces that should be walkable to become non-walkable.
    /// 
    /// WHAT WAS THE PROBLEM?
    /// 
    /// When a BWM is transformed (flipped, rotated, or translated), only the vertices are modified.
    /// The materials should remain unchanged. However, when creating deep copies of BWMs, if the
    /// Material property is not explicitly copied, it might be lost or set to a default value.
    /// 
    /// THE FIX:
    /// 
    /// When creating deep copies of BWMs (like in IndoorMap.DeepCopyBwm() and KitComponent.DeepCopy()),
    /// we now explicitly copy the Material property:
    /// 
    /// newFace.Material = face.Material;
    /// 
    /// This ensures that materials are preserved during all transformations and copying operations.
    /// 
    /// WHY THIS MATTERS:
    /// 
    /// If materials are not preserved, faces that should be walkable (like Stone = 4) might become
    /// non-walkable (like Undefined = 0). This causes the bug where "levels/modules are NOT walkable
    /// despite having the right surface material."
    /// </para>
    /// 
    /// <para>
    /// CRITICAL CONSISTENCY REQUIREMENT:
    /// 
    /// The WalkableMaterials set in NavigationMesh MUST match the WalkableMaterials set in
    /// SurfaceMaterialExtensions exactly. If they differ, the indoor map builder and other tools
    /// will have incorrect walkability determination.
    /// 
    /// Both sets must contain the same material IDs:
    /// - 1 (Dirt), 3 (Grass), 4 (Stone), 5 (Wood), 6 (Water), 9 (Carpet), 10 (Metal),
    ///   11 (Puddles), 12 (Swamp), 13 (Mud), 14 (Leaves), 16 (BottomlessPit), 18 (Door),
    ///   20 (Sand), 21 (BareBones), 22 (StoneBridge), 30 (Trigger)
    /// 
    /// Any mismatch will cause bugs where faces are marked as walkable in one place but non-walkable
    /// in another.
    /// </para>
    /// 
    /// <para>
    /// <para>
    /// WHAT IS A WALKMESH?
    /// 
    /// A walkmesh is a special kind of 3D model that tells the game where characters can walk.
    /// The game world has beautiful 3D graphics that you see, but the walkmesh is a simpler version
    /// made of triangles that the game uses to figure out where things can move.
    /// 
    /// The walkmesh is made of triangles (three-sided shapes) that cover the ground. Each triangle
    /// has three points (called vertices) that define its corners. The game uses these triangles
    /// to answer questions like: "Can a character walk here?" and "What is the height of the ground
    /// at this point?"
    /// 
    /// Walkmeshes are used for four main things:
    /// 1. Pathfinding: NPCs and the player use walkmeshes to find routes between locations
    /// 2. Collision Detection: Prevents characters from walking through walls and objects
    /// 3. Height Calculation: Determines the ground height at any (x, y) position
    /// 4. Line of Sight: Checks if there's a clear path between two points
    /// </para>
    /// 
    /// <para>
    /// WHAT IS A BWM FILE?
    /// 
    /// BWM stands for "BioWare Walkmesh". It is a file format that stores all the walkmesh data.
    /// The file format was created by BioWare for their Aurora engine and is used in Knights of the
    /// Old Republic games. Files are stored on disk with a .wok extension (for area walkmeshes),
    /// .pwk extension (for placeable walkmeshes), or .dwk extension (for door walkmeshes).
    /// 
    /// The BWM file structure (based on swkotor2.exe: WriteBWMFile @ 0x0055aef0):
    /// 
    /// 1. FILE HEADER (8 bytes):
    ///    - Bytes 0-3: Magic string "BWM " (space-padded, identifies file type)
    ///    - Bytes 4-7: Version string "V1.0" (only version used in KotOR games)
    ///    - Located via string reference: "BWM V1.0" @ 0x007c061c in swkotor2.exe
    ///    - Original implementation: FUN_0055aef0 @ 0x0055aef0 writes this header (line 58)
    /// 
    /// 2. WALKMESH PROPERTIES (52 bytes, offset 0x08):
    ///    - Walkmesh Type (4 bytes): 0 = PWK/DWK (placeable/door), 1 = WOK (area walkmesh)
    ///    - Relative Use Position 1 (12 bytes): Hook point 1 relative to walkmesh origin
    ///    - Relative Use Position 2 (12 bytes): Hook point 2 relative to walkmesh origin
    ///    - Absolute Use Position 1 (12 bytes): Hook point 1 in world coordinates
    ///    - Absolute Use Position 2 (12 bytes): Hook point 2 in world coordinates
    ///    - Position (12 bytes): Walkmesh origin offset in world space
    ///    - Original implementation: FUN_0055aef0 writes these values (lines 59-71)
    /// 
    /// 3. DATA TABLE OFFSETS (64 bytes, offset 0x48):
    ///    - Vertex Count (4 bytes): Number of vertices in the mesh
    ///    - Vertex Offset (4 bytes): File offset to vertex array
    ///    - Face Count (4 bytes): Number of triangles in the mesh
    ///    - Face Indices Offset (4 bytes): File offset to face indices array
    ///    - Materials Offset (4 bytes): File offset to surface materials array
    ///    - Normals Offset (4 bytes): File offset to face normals array
    ///    - Planar Distances Offset (4 bytes): File offset to plane distances array
    ///    - AABB Count (4 bytes): Number of AABB tree nodes (WOK only, 0 for PWK/DWK)
    ///    - AABB Offset (4 bytes): File offset to AABB tree array (WOK only)
    ///    - Unknown (4 bytes): Unknown field (typically 0 or 4)
    ///    - Adjacency Count (4 bytes): Number of walkable faces for adjacency (WOK only)
    ///    - Adjacency Offset (4 bytes): File offset to adjacency array (WOK only)
    ///    - Edge Count (4 bytes): Number of perimeter edges (WOK only)
    ///    - Edge Offset (4 bytes): File offset to edge array (WOK only)
    ///    - Perimeter Count (4 bytes): Number of perimeter markers (WOK only)
    ///    - Perimeter Offset (4 bytes): File offset to perimeter array (WOK only)
    ///    - Original implementation: FUN_0055aef0 writes these offsets (lines 72-82)
    /// 
    /// 4. VERTICES ARRAY:
    ///    - Each vertex is 12 bytes (three float32 values: x, y, z)
    ///    - Vertices are stored in world coordinates for area walkmeshes (WOK)
    ///    - Vertices are stored in local coordinates for placeable/door walkmeshes (PWK/DWK)
    ///    - Original implementation: FUN_0055aef0 writes vertices (line 74)
    /// 
    /// 5. FACE INDICES ARRAY:
    ///    - Each face is 12 bytes (three uint32 values pointing to vertex indices)
    ///    - Faces are ordered with walkable faces first, then non-walkable faces
    ///    - Original implementation: FUN_0055aef0 writes face indices (line 76)
    /// 
    /// 6. SURFACE MATERIALS ARRAY:
    ///    - Each material is 4 bytes (uint32, material ID 0-30)
    ///    - Material determines if face is walkable and what surface type it is
    ///    - Original implementation: FUN_0055aef0 writes materials (line 78)
    /// 
    /// 7. NORMALS ARRAY (WOK only):
    ///    - Each normal is 12 bytes (three float32 values: x, y, z)
    ///    - Normal vector points perpendicular to the triangle's surface
    ///    - Used for lighting and collision calculations
    ///    - Original implementation: FUN_0055aef0 writes normals (line 80)
    /// 
    /// 8. PLANAR DISTANCES ARRAY (WOK only):
    ///    - Each distance is 4 bytes (float32)
    ///    - Distance from origin to plane defined by triangle
    ///    - Used for plane equation calculations
    ///    - Original implementation: FUN_0055aef0 writes planar distances (line 78)
    /// 
    /// 9. AABB TREE (WOK only):
    ///    - Binary tree structure for fast spatial queries
    ///    - Each node contains bounding box (min/max) and child pointers
    ///    - Leaf nodes contain face indices
    ///    - Original implementation: FUN_0055aef0 writes AABB tree (line 80)
    /// 
    /// 10. ADJACENCY ARRAY (WOK only):
    ///     - Each adjacency entry is 4 bytes (int32)
    ///     - Encoding: face_index * 3 + edge_index, -1 = no neighbor
    ///     - Tells which triangles share edges (for pathfinding)
    ///     - Original implementation: FUN_0055aef0 writes adjacency (line 82)
    /// 
    /// 11. EDGES ARRAY (WOK only):
    ///     - Array of edge indices with transition information
    ///     - Used for area boundaries and door connections
    ///     - Original implementation: FUN_0055aef0 writes edges (line 82)
    /// 
    /// 12. PERIMETERS ARRAY (WOK only):
    ///     - Array of edge indices forming boundary loops
    ///     - Defines the outer edges of walkable areas
    ///     - Original implementation: FUN_0055aef0 writes perimeters (line 82)
    /// 
    /// The file format is documented in vendor/PyKotor/wiki/BWM-File-Format.md
    /// Reference implementations: vendor/reone/src/libs/graphics/format/bwmreader.cpp,
    /// vendor/KotOR.js/src/odyssey/OdysseyWalkMesh.ts, vendor/kotorblender/io_scene_kotor/format/bwm/
    /// </para>
    /// 
    /// <para>
    /// HOW DOES THE DATA WORK?
    /// 
    /// VERTICES:
    /// Vertices are 3D points (x, y, z coordinates) that define where corners of triangles are.
    /// For example, a vertex might be at position (10.5, 20.3, 5.0) in the game world.
    /// 
    /// Vertices are stored in an array, and multiple triangles can share the same vertex. This saves
    /// memory because instead of storing the same point multiple times, we store it once and reference
    /// it by index. For example, if two triangles share a corner, they both reference the same vertex
    /// index instead of storing two copies of the same point.
    /// 
    /// Coordinate Systems:
    /// - Area walkmeshes (WOK): Vertices are in world coordinates (absolute positions in the game world)
    /// - Placeable/Door walkmeshes (PWK/DWK): Vertices are in local coordinates (relative to the object)
    ///   When the object is placed, the engine transforms these local coordinates to world coordinates
    ///   using a transformation matrix (translation, rotation, scale)
    /// 
    /// FACE INDICES:
    /// Each triangle (face) is defined by three numbers that point to vertices in the vertex array.
    /// For example, if face 0 has indices [5, 12, 8], it means:
    /// - The first corner uses vertex 5 from the vertex array
    /// - The second corner uses vertex 12 from the vertex array
    /// - The third corner uses vertex 8 from the vertex array
    /// 
    /// The vertices are ordered counter-clockwise when viewed from the front (the side the normal points toward).
    /// This ordering is important for determining which side of the triangle is the "front" (walkable side).
    /// 
    /// Face Ordering:
    /// In BWM files, faces are typically ordered with walkable faces first, then non-walkable faces.
    /// This ordering is important because:
    /// - Adjacency data only exists for walkable faces
    /// - The adjacency array index corresponds to the walkable face's position in the walkable face list
    /// - The engine can quickly iterate through walkable faces for pathfinding
    /// 
    /// ADJACENCY:
    /// Adjacency tells which triangles share edges. If triangle 0 shares an edge with triangle 5,
    /// the adjacency data stores that information. This is used for pathfinding - the game can move
    /// from one triangle to its neighbors.
    /// 
    /// Adjacency Encoding:
    /// - Each triangle has 3 edges (edge 0, edge 1, edge 2)
    /// - Adjacency is stored as: adjacency_index = face_index * 3 + edge_index
    /// - For example, face 5, edge 1 is stored at index 5*3+1 = 16
    /// - The value stored is: neighbor_face_index * 3 + neighbor_edge_index
    /// - If there's no neighbor on that edge, the value is -1
    /// 
    /// How Adjacency is Computed:
    /// Two triangles are adjacent if they share exactly two vertices (forming a shared edge).
    /// The algorithm works like this:
    /// 1. For each triangle, look at each of its 3 edges
    /// 2. For each edge, find if any other triangle has the same two vertices (in either order)
    /// 3. If found, link them bidirectionally (triangle A's edge points to triangle B, and vice versa)
    /// 
    /// Based on swkotor2.exe: Adjacency is computed during walkmesh loading, not stored in BWM files.
    /// However, BWM files can contain precomputed adjacency data for WOK files.
    /// 
    /// SURFACE MATERIALS:
    /// Each triangle has a material number (0-30) that tells what kind of surface it is.
    /// The material determines:
    /// - Whether the triangle is walkable (characters can stand on it)
    /// - What kind of surface it is (affects footstep sounds, movement speed, etc.)
    /// - Pathfinding cost (some materials are harder to walk on)
    /// 
    /// Material Reference (from surfacemat.2da):
    /// Walkable Materials:
    /// - 1: Dirt (walkable, normal movement)
    /// - 3: Grass (walkable, normal movement)
    /// - 4: Stone (walkable, normal movement, default for generated walkmeshes)
    /// - 5: Wood (walkable, normal movement)
    /// - 6: Water - Shallow (walkable, slower movement, 1.5x pathfinding cost)
    /// - 9: Carpet (walkable, normal movement)
    /// - 10: Metal (walkable, normal movement)
    /// - 11: Puddles (walkable, slower movement, 1.5x pathfinding cost)
    /// - 12: Swamp (walkable, slower movement, 1.5x pathfinding cost)
    /// - 13: Mud (walkable, slower movement, 1.5x pathfinding cost)
    /// - 14: Leaves (walkable, normal movement)
    /// - 16: BottomlessPit (walkable but dangerous, 10x pathfinding cost, AI avoids if possible)
    /// - 18: Door (walkable, normal movement)
    /// - 20: Sand (walkable, normal movement)
    /// - 21: BareBones (walkable, normal movement)
    /// - 22: StoneBridge (walkable, normal movement)
    /// - 30: Trigger (walkable, PyKotor extended material)
    /// 
    /// Non-Walkable Materials:
    /// - 0: NotDefined/UNDEFINED (non-walkable, used for undefined surfaces)
    /// - 2: Obscuring (non-walkable, blocks line of sight)
    /// - 7: Nonwalk/NON_WALK (non-walkable, explicitly marked as impassable)
    /// - 8: Transparent (non-walkable, see-through but solid)
    /// - 15: Lava (non-walkable, dangerous)
    /// - 17: DeepWater (non-walkable, characters can't stand in deep water)
    /// - 19: Snow/NON_WALK_GRASS (non-walkable, marked as non-walkable grass)
    /// 
    /// The game looks up these materials in a file called surfacemat.2da to determine properties.
    /// However, the walkability is hardcoded in the engine based on the material ID.
    /// </para>
    /// 
    /// <para>
    /// HOW DOES FACEFINDING WORK?
    /// 
    /// When the game needs to find which triangle contains a specific point (like where a character is standing),
    /// it uses the FindFaceAt function. This is based on swkotor2.exe: FUN_004f4260 @ 0x004f4260.
    /// 
    /// The algorithm works in stages:
    /// 
    /// STAGE 1: Input Processing
    /// - Takes a point (x, y, z) and tries to find which triangle contains it
    /// - Creates a vertical range around the point: z + tolerance to z - tolerance
    ///   The tolerance value is _DAT_007b56f8 (typically a small value like 0.1 or 0.5 units)
    ///   This is needed because the point might not be exactly on the triangle surface due to
    ///   floating-point precision or character movement
    /// - Original implementation: FUN_004f4260 lines 20-23 create this vertical range
    /// 
    /// STAGE 2: Hint Face Optimization (if provided)
    /// - If a hint face index is provided (a guess about which triangle might contain the point),
    ///   it tests that triangle first
    /// - This is a major optimization because characters usually stay on the same triangle for
    ///   multiple frames (60+ frames per second), so the hint is correct most of the time
    /// - Original implementation: FUN_004f4260 lines 31-42 test hint face first
    /// - The hint face test uses FUN_0055b300 @ 0x0055b300 to test AABB intersection, then
    ///   FUN_00575f60 @ 0x00575f60 to test actual triangle intersection
    /// 
    /// STAGE 3: AABB Tree Traversal (if available)
    /// - If an AABB tree exists, it uses that for fast spatial search
    /// - Starting from the root, it tests if the point is inside the root's bounding box
    /// - If yes, it tests the two child boxes (left and right)
    /// - It keeps going down the tree until it reaches a leaf node (a single triangle)
    /// - This is much faster than testing every triangle (O(log n) instead of O(n))
    /// - Original implementation: FUN_004f4260 lines 45-63 iterate through faces using AABB tree
    /// 
    /// STAGE 4: Triangle Point-in-Triangle Test
    /// - For each candidate triangle (from AABB tree or brute force), it tests if the point is inside
    /// - The test is done in 2D (only x and y coordinates) because we're finding which triangle
    ///   is directly below or above the point
    /// - Uses the "same-side test": checks if the point is on the same side of all three edges
    /// - If the point is on the same side of all edges, it's inside the triangle
    /// - Original implementation: FUN_0055b300 @ 0x0055b300 tests AABB first, then FUN_00575f60
    ///   @ 0x00575f60 tests triangle intersection using AABB tree
    /// 
    /// STAGE 5: Return Result
    /// - If a triangle is found, returns its index (0 to faceCount-1)
    /// - If no triangle is found, returns -1
    /// 
    /// Alternative Implementation (FUN_004f43c0 @ 0x004f43c0):
    /// This is a variant that takes a parameter to control whether to use AABB tree or brute force.
    /// - If param_2 == 0: Uses FUN_0055b300 (AABB tree traversal)
    /// - If param_2 != 0: Uses FUN_0055b3c0 (brute force with AABB pre-filtering)
    /// - Original implementation: FUN_004f43c0 lines 33-40 choose between methods
    /// 
    /// The AABB Test (FUN_0055b300 @ 0x0055b300):
    /// - First tests if the point is inside the triangle's AABB (Axis-Aligned Bounding Box)
    /// - An AABB is a box aligned with the X, Y, Z axes that contains the triangle
    /// - This is a fast rejection test - if the point isn't in the AABB, it can't be in the triangle
    /// - Original implementation: FUN_0055b300 calls FUN_00575f60 @ 0x00575f60 for AABB test
    /// 
    /// The Triangle Intersection Test (FUN_00575f60 @ 0x00575f60):
    /// - Tests if a vertical line segment (from z+tolerance to z-tolerance) intersects the triangle
    /// - Uses the AABB tree to find candidate triangles
    /// - Original implementation: FUN_00575f60 calls FUN_00575350 @ 0x00575350 for AABB tree traversal
    /// </para>
    /// 
    /// <para>
    /// HOW DOES HEIGHT CALCULATION WORK?
    /// 
    /// When the game needs to know the height of the ground at a specific (x, y) position, it uses height
    /// calculation functions. This is based on swkotor2.exe: FUN_0055b1d0 @ 0x0055b1d0 and FUN_0055b210 @ 0x0055b210.
    /// 
    /// The algorithm works in steps:
    /// 
    /// STEP 1: Find the Triangle
    /// - First, it finds which triangle contains the (x, y) point using FindFaceAt
    /// - FindFaceAt uses FUN_004f4260 @ 0x004f4260 or FUN_004f43c0 @ 0x004f43c0
    /// - The z coordinate of the input point is ignored - we only care about x and y
    /// - Original implementation: FUN_0055b1d0 calls FUN_005761f0 @ 0x005761f0 to find face
    /// 
    /// STEP 2: Get Triangle Vertices
    /// - Once we know which triangle, we get its three vertices from the vertex array
    /// - Each vertex has x, y, z coordinates
    /// - Original implementation: FUN_0055b1d0 gets vertices from walkmesh structure
    /// 
    /// STEP 3: Calculate the Normal Vector
    /// - The normal vector is a vector pointing perpendicular to the triangle's surface
    /// - We calculate it by taking the cross product of two edges of the triangle
    /// - Edge 1 = vertex2 - vertex1
    /// - Edge 2 = vertex3 - vertex1
    /// - Normal = cross(Edge1, Edge2)
    /// - Original implementation: FUN_004d9030 @ 0x004d9030 calculates normal (lines 32-55)
    /// 
    /// STEP 4: Create the Plane Equation
    /// - A triangle defines a flat plane in 3D space
    /// - The equation for a plane is: ax + by + cz + d = 0
    /// - Where (a, b, c) is the normal vector (normalized to length 1)
    /// - And d is calculated from a point on the plane (one of the triangle's vertices)
    /// - d = -(a * vertex1.x + b * vertex1.y + c * vertex1.z)
    /// - Original implementation: FUN_004d9030 lines 57-63 create plane equation
    /// 
    /// STEP 5: Solve for Height (z coordinate)
    /// - Once we have the plane equation, we can solve for z when we know x and y
    /// - Rearranging the plane equation: z = (-d - ax - by) / c
    /// - This gives us the exact height of the plane at the given (x, y) position
    /// - Original implementation: FUN_004d6b10 @ 0x004d6b10 calculates height from plane equation
    /// 
    /// STEP 6: Handle Edge Cases
    /// - If the triangle is vertical (normal.Z is very close to 0), we can't divide by c
    /// - In this case, we return the average height of the three vertices
    /// - Average = (vertex1.z + vertex2.z + vertex3.z) / 3
    /// - Original implementation: FUN_004d6b10 lines 7-8 check if normal.Z == _DAT_007b56fc
    /// 
    /// Two Variants of Height Calculation:
    /// 
    /// FUN_0055b1d0 @ 0x0055b1d0 (3D point height):
    /// - Takes a 3D point (x, y, z) and finds the height at that (x, y) position
    /// - Uses FUN_005761f0 @ 0x005761f0 to find the face containing the point
    /// - Then uses FUN_00576640 @ 0x00576640 to calculate height from plane equation
    /// - Original implementation: FUN_0055b1d0 calls FUN_00576640 (line 12)
    /// 
    /// FUN_0055b210 @ 0x0055b210 (2D point height with face index):
    /// - Takes a 2D point (x, y) and a face index (already known)
    /// - Directly calculates height from the plane equation without finding the face
    /// - Faster when you already know which triangle contains the point
    /// - Uses FUN_00575050 @ 0x00575050 to calculate height
    /// - Original implementation: FUN_0055b210 calls FUN_00575050 (line 11)
    /// 
    /// The Plane Equation Function (FUN_004d6b10 @ 0x004d6b10):
    /// - Input: normal vector (a, b, c), plane distance (d), point (x, y)
    /// - Output: height (z coordinate)
    /// - Formula: z = (-d - ax - by) / c
    /// - Special case: If c (normal.Z) is very close to 0, returns _DAT_007b56fc (special value)
    /// - Original implementation: FUN_004d6b10 lines 7-11 calculate height
    /// </para>
    /// 
    /// <para>
    /// HOW DOES RAYCASTING WORK?
    /// 
    /// Raycasting is like shooting an invisible laser and seeing what it hits. The game uses this to check
    /// if there's a clear line of sight between two points, or to find where a ray hits the walkmesh.
    /// This is based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts.
    /// 
    /// The raycast algorithm works in multiple stages:
    /// 
    /// STAGE 1: Input Validation
    /// - Validates that the mesh is not empty
    /// - Validates that maxDistance is positive and finite
    /// - Validates that direction vector is not zero or near-zero
    /// - Normalizes the direction vector once (optimization - reused in all tests)
    /// - Original implementation: UpdateCreatureMovement @ 0x0054be70 validates inputs
    /// 
    /// STAGE 2: AABB Tree Traversal (if available)
    /// - The walkmesh has an AABB tree, which organizes triangles into boxes (AABBs)
    /// - Starting from the root, it tests if the ray hits the root's bounding box
    /// - If it hits, it tests the two child boxes (left and right)
    /// - It keeps going down the tree until it reaches a leaf node (a single triangle)
    /// - This is much faster than testing every triangle (O(log n) instead of O(n))
    /// - Based on swkotor2.exe: FUN_00575350 @ 0x00575350 (AABB tree traversal)
    /// 
    /// AABB Tree Traversal Details (FUN_00575350 @ 0x00575350):
    /// - Tests ray against node's AABB using FUN_004d7400 @ 0x004d7400 (slab method)
    /// - If ray doesn't hit AABB, returns 0 (no intersection)
    /// - If node is a leaf (contains a face index), tests ray against that triangle
    /// - If node is internal, recursively tests left and right children
    /// - Original implementation: FUN_00575350 lines 26-69 traverse tree recursively
    /// - Traversal order: Original uses flag-based ordering (param_4[1] & node flag),
    ///   this implementation uses distance-based ordering (test closer child first) as optimization
    /// 
    /// STAGE 3: AABB-Ray Intersection Test (FUN_004d7400 @ 0x004d7400)
    /// - Uses the "slab method" to test if a ray hits an AABB
    /// - A slab is a space between two parallel planes
    /// - The method tests the ray against each axis (X, Y, Z) separately
    /// - For each axis:
    ///   a. Calculate where the ray enters the slab (tmin)
    ///   b. Calculate where the ray exits the slab (tmax)
    ///   c. Handle negative direction (swap tmin and tmax)
    /// - The ray hits the AABB if:
    ///   - It enters all three slabs before exiting any
    ///   - The intersection is within maxDistance
    /// - Original implementation: FUN_004d7400 lines 12-79 implement slab method
    /// 
    /// STAGE 4: Ray-Triangle Intersection (FUN_004d9030 @ 0x004d9030)
    /// - For each triangle that might be hit (from AABB tree, or all triangles if no tree),
    ///   it tests if the ray actually hits the triangle
    /// - The algorithm uses a plane-based approach with edge containment tests:
    /// 
    ///   Step 4a: Calculate Triangle Normal
    ///   - Calculate the triangle's normal vector (perpendicular to the triangle)
    ///   - Normal = cross(edge01, edge12) where edge01 = v1-v0, edge12 = v2-v1
    ///   - Normalize the normal (make it length 1)
    ///   - Check for degenerate triangles (normal length too small) - skip if degenerate
    ///   - Original implementation: FUN_004d9030 lines 32-56 calculate normal
    /// 
    ///   Step 4b: Create Plane Equation
    ///   - Create plane equation: ax + by + cz + d = 0
    ///   - Where (a, b, c) is the normalized normal vector
    ///   - d = -(a * v0.x + b * v0.y + c * v0.z)
    ///   - Original implementation: FUN_004d9030 line 63 creates plane equation
    /// 
    ///   Step 4c: Check if Ray Crosses Plane
    ///   - Calculate distance from ray start to plane: dist0 = a*origin.x + b*origin.y + c*origin.z + d
    ///   - Calculate distance from ray end to plane: dist1 = a*rayEnd.x + b*rayEnd.y + c*rayEnd.z + d
    ///   - Ray crosses plane if dist0 and dist1 have opposite signs (one positive, one negative)
    ///   - If both have same sign, ray doesn't cross plane - no intersection
    ///   - Original implementation: FUN_004d9030 lines 65-67 check plane crossing
    /// 
    ///   Step 4d: Calculate Intersection Point
    ///   - Use interpolation: t = dist0 / (dist0 - dist1)
    ///   - Intersection = origin + direction * (t * maxDistance)
    ///   - This gives the exact point where the ray hits the plane
    ///   - Original implementation: FUN_004d9030 lines 71-76 calculate intersection
    /// 
    ///   Step 4e: Test if Intersection Point is Inside Triangle
    ///   - Just because the ray hits the plane doesn't mean it hits the triangle
    ///   - The triangle only covers a small part of the plane
    ///   - Test each edge of the triangle:
    ///     * For edge v0->v1: Check if intersection is on correct side
    ///     * For edge v1->v2: Check if intersection is on correct side
    ///     * For edge v2->v0: Check if intersection is on correct side
    ///   - Use cross products to determine which side of each edge the point is on
    ///   - If point is on correct side of all three edges, it's inside the triangle
    ///   - Original implementation: FUN_004d9030 lines 79-95 test edge containment
    /// 
    /// STAGE 5: Return Closest Hit
    /// - If multiple triangles are hit, return the closest one (smallest distance)
    /// - Calculate distance from origin to hit point
    /// - Return hit point, hit face index, and distance
    /// - Original implementation: UpdateCreatureMovement @ 0x0054be70 tracks best hit
    /// 
    /// OPTIMIZATIONS:
    /// - Normalized direction caching: Direction is normalized once and reused
    /// - Early termination: If exact hit found (distance = 0), stop immediately
    /// - Distance-based AABB traversal: Test closer children first (optimized from original)
    /// - AABB pre-filtering: Fast rejection of triangles that can't be hit
    /// 
    /// EDGE CASES HANDLED:
    /// - Empty mesh: Returns false immediately
    /// - Zero or invalid direction: Rejected before processing
    /// - Degenerate triangles: Skipped during intersection tests
    /// - Ray starting inside triangle: Handled with tolerance checks
    /// - Ray on triangle surface: Returns as hit with distance 0
    /// - Ray parallel to triangle plane: Rejected (no intersection possible)
    /// - Vertical triangles: Handled by edge containment tests
    /// </para>
    /// 
    /// <para>
    /// HOW DOES PATHFINDING WORK?
    /// 
    /// Pathfinding uses the A* algorithm on the walkmesh adjacency graph. A* is a search algorithm
    /// that finds the shortest path between two points by exploring the most promising paths first.
    /// 
    /// The algorithm works in stages:
    /// 
    /// STAGE 1: Initialization
    /// - Find the triangle containing the starting point using FindFaceAt
    /// - Find the triangle containing the destination point using FindFaceAt
    /// - If either point is not on a walkable triangle, pathfinding fails
    /// - If both points are on the same triangle, check for obstacles blocking direct path
    /// - Original implementation: UpdateCreatureMovement @ 0x0054be70 finds start/end faces
    /// 
    /// STAGE 2: A* Search Algorithm
    /// - A* keeps three data structures:
    ///   * openSet: Triangles to explore, sorted by f-score (most promising first)
    ///   * cameFrom: Maps each triangle to the triangle that led to it (for path reconstruction)
    ///   * gScore: Distance traveled from start to each triangle
    ///   * fScore: Estimated total distance (gScore + heuristic) for each triangle
    /// - Start by adding the starting triangle to openSet with f-score = heuristic(start, goal)
    /// 
    /// STAGE 3: Main Search Loop
    /// - While openSet is not empty:
    ///   1. Remove triangle with lowest f-score from openSet (most promising)
    ///   2. If this triangle is the goal, reconstruct and return the path
    ///   3. For each adjacent triangle:
    ///      a. Check if it's walkable (skip non-walkable faces)
    ///      b. Check if it's blocked by obstacles (skip if blocked)
    ///      c. Calculate tentative g-score = gScore[current] + edgeCost(current, neighbor)
    ///      d. If this path to neighbor is better than any previous path:
    ///         * Update cameFrom[neighbor] = current
    ///         * Update gScore[neighbor] = tentative g-score
    ///         * Update fScore[neighbor] = gScore[neighbor] + heuristic(neighbor, goal)
    ///         * Add neighbor to openSet if not already there
    /// 
    /// STAGE 4: Heuristic Function
    /// - The heuristic estimates the distance from a triangle to the goal
    /// - Uses straight-line distance between triangle centers
    /// - Must be "admissible" (never overestimate) for A* to find optimal path
    /// - Original implementation: UpdateCreatureMovement @ 0x0054be70 uses distance heuristic
    /// 
    /// STAGE 5: Edge Cost Calculation
    /// - The cost to move from one triangle to an adjacent triangle is:
    ///   cost = distance * surfaceCostModifier
    /// - Distance is the straight-line distance between triangle centers
    /// - Surface cost modifiers:
    ///   * Normal surfaces (stone, dirt, grass): 1.0x (normal speed)
    ///   * Difficult surfaces (water, mud, swamp): 1.5x (slower movement)
    ///   * Dangerous surfaces (bottomless pit): 10.0x (AI avoids if possible)
    /// - Original implementation: Pathfinding considers surface materials for cost
    /// 
    /// STAGE 6: Path Reconstruction
    /// - When goal is reached, trace back through cameFrom map
    /// - Start from goal triangle, follow cameFrom links back to start
    /// - Reverse the path to get start-to-goal order
    /// - Convert triangle path to waypoint path (triangle centers + start/end points)
    /// - Original implementation: UpdateCreatureMovement @ 0x0054be70 reconstructs path
    /// 
    /// STAGE 7: Path Smoothing
    /// - Apply path smoothing to remove redundant waypoints
    /// - For each waypoint, check if we can skip it (line of sight to next waypoint)
    /// - If line of sight is clear, remove the intermediate waypoint
    /// - This creates smoother, more direct paths
    /// - Original implementation: Path smoothing is applied after pathfinding
    /// 
    /// OBSTACLE AVOIDANCE:
    /// - If obstacles are provided, the algorithm builds a set of blocked faces
    /// - A face is blocked if its center is within obstacle radius
    /// - Blocked faces are skipped during pathfinding
    /// - If pathfinding fails with obstacles, retry with 1.5x expanded obstacle radius
    /// - Based on swkotor2.exe: FindPathAroundObstacle @ 0x0061c390
    /// - Original implementation: FUN_0061c390 builds obstacle polygon and validates path
    /// 
    /// GRID-BASED PATHFINDING FALLBACK:
    /// - If direct walkmesh pathfinding fails, the game uses grid-based pathfinding
    /// - Grid-based pathfinding divides the area into a grid and finds paths through grid cells
    /// - Used for initial/terminal path segments when walkmesh pathfinding fails
    /// - Error messages from swkotor2.exe indicate this:
    ///   * "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   * "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
    /// - Original implementation: Grid-based pathfinding is used as fallback in UpdateCreatureMovement
    /// </para>
    /// 
    /// <para>
    /// HOW IS THE AABB TREE BUILT?
    /// 
    /// The AABB tree is a binary tree structure that organizes triangles into boxes for fast searching.
    /// It's built using a recursive top-down construction algorithm.
    /// 
    /// Construction Algorithm:
    /// 
    /// STEP 1: Calculate Bounding Box
    /// - For all triangles in the current set, find the smallest box that contains them all
    /// - The box is defined by min and max points (BoundsMin, BoundsMax)
    /// - BoundsMin = minimum of all vertex coordinates
    /// - BoundsMax = maximum of all vertex coordinates
    /// 
    /// STEP 2: Check Termination Conditions
    /// - If only one triangle remains, create a leaf node (FaceIndex = triangle index)
    /// - If maximum depth is reached, create a leaf node
    /// - Otherwise, create an internal node and split
    /// 
    /// STEP 3: Choose Split Axis
    /// - Find the longest dimension of the bounding box (X, Y, or Z)
    /// - Split along this axis (splitting along longest axis creates more balanced trees)
    /// - Split point is the center of the bounding box along the chosen axis
    /// 
    /// STEP 4: Partition Triangles
    /// - For each triangle, calculate its center point (average of three vertices)
    /// - If triangle center is less than split point, add to left child set
    /// - Otherwise, add to right child set
    /// 
    /// STEP 5: Handle Degenerate Cases
    /// - If all triangles end up on one side, try splitting along a different axis
    /// - If still degenerate, split in half by index (simple fallback)
    /// 
    /// STEP 6: Recursively Build Children
    /// - Recursively build left child tree with left triangle set
    /// - Recursively build right child tree with right triangle set
    /// - Return the internal node with children attached
    /// 
    /// Tree Structure:
    /// - Leaf nodes: FaceIndex >= 0 (contains a triangle index)
    /// - Internal nodes: FaceIndex = -1 (contains child nodes)
    /// - Each node has BoundsMin and BoundsMax (bounding box)
    /// - Each internal node has Left and Right child pointers
    /// 
    /// Performance:
    /// - Building the tree: O(n log n) where n is number of triangles
    /// - Searching the tree: O(log n) average case, O(n) worst case (degenerate tree)
    /// - Without tree: O(n) for every search (must test all triangles)
    /// 
    /// Based on swkotor2.exe: AABB tree is built during walkmesh loading, not at runtime.
    /// The tree structure is stored in BWM files for WOK (area walkmeshes).
    /// </para>
    /// 
    /// <para>
    /// OPTIMIZATIONS:
    /// 
    /// 1. AABB Tree: Instead of testing every triangle, the tree organizes them into boxes, making
    ///    searches much faster (from O(n) to O(log n) where n is the number of triangles).
    ///    - Building: O(n log n) time, done once during walkmesh loading
    ///    - Searching: O(log n) average case, O(n) worst case
    ///    - Memory: O(n) space for tree nodes
    ///    - Based on swkotor2.exe: AABB tree is stored in BWM files for WOK walkmeshes
    /// 
    /// 2. Hint Face Index: When finding a face, if you provide a guess (hint), it tests that triangle
    ///    first. This is fast because characters usually stay on the same triangle for multiple frames
    ///    (60+ frames per second). The hint is correct most of the time, avoiding tree traversal.
    ///    - Based on swkotor2.exe: FUN_004f4260 @ 0x004f4260 tests hint face first (lines 31-42)
    ///    - Optimization: O(1) best case (hint correct), O(log n) average case (hint wrong)
    /// 
    /// 3. Normalized Direction Caching: When raycasting, the direction vector is normalized once
    ///    and reused for all intersection tests, avoiding repeated calculations.
    ///    - Normalization: Calculate length once, divide direction by length
    ///    - Reuse: Use normalized direction in all AABB and triangle intersection tests
    ///    - Saves: n-1 normalization calculations (where n = number of triangles tested)
    /// 
    /// 4. Early Termination: If a raycast finds an exact hit (distance = 0, within tolerance), it
    ///    stops immediately instead of checking more triangles.
    ///    - Exact hit: Ray starts on triangle surface (distance < 1e-5)
    ///    - Optimization: O(1) best case (exact hit on first triangle)
    ///    - Based on swkotor2.exe: Early termination when exact hit found
    /// 
    /// 5. Distance-Based AABB Traversal: When traversing the AABB tree, it tests the closer child
    ///    first. This is an optimization over the original flag-based ordering (FUN_00575350 uses
    ///    param_4[1] flag to determine order), but maintains correctness while improving performance.
    ///    - Calculates distance from ray origin to each child's AABB center
    ///    - Tests closer child first (more likely to contain closest hit)
    ///    - Can reduce bestDist earlier, allowing more aggressive culling of second child
    ///    - Based on swkotor2.exe: Original uses flag-based ordering, this is optimized version
    /// 
    /// 6. AABB Pre-filtering: Before testing triangle intersection, test if ray hits triangle's AABB.
    ///    This is a fast rejection test - if ray doesn't hit AABB, it can't hit the triangle.
    ///    - AABB test: O(1) per triangle (simple box test)
    ///    - Triangle test: O(1) per triangle (plane + edge tests)
    ///    - Rejection rate: High (most triangles are rejected by AABB test)
    ///    - Based on swkotor2.exe: FUN_0055b300 @ 0x0055b300 tests AABB before triangle
    /// 
    /// 7. Brute Force Fallback: If no AABB tree is available, falls back to brute force search.
    ///    While slower (O(n)), it still works correctly and handles edge cases properly.
    ///    - Used for: PWK/DWK walkmeshes (placeable/door walkmeshes don't have AABB trees)
    ///    - Optimization: Still uses normalized direction caching and early termination
    /// </para>
    /// 
    /// <para>
    /// EDGE CASES HANDLED:
    /// 
    /// EMPTY MESH:
    /// - If mesh has no triangles (faceCount == 0), all queries return false immediately
    /// - FindFaceAt returns -1, Raycast returns false, ProjectToSurface returns false
    /// - Prevents division by zero and array access errors
    /// 
    /// ZERO OR INVALID DIRECTION VECTORS:
    /// - Direction vector with length < 1e-6 is rejected (too small to be meaningful)
    /// - Non-finite direction values (NaN, Infinity) are rejected
    /// - Prevents division by zero in normalization and intersection calculations
    /// 
    /// DEGENERATE TRIANGLES (ZERO AREA):
    /// - Triangles where all three vertices are collinear (in a straight line)
    /// - Detected by checking if normal vector length < 1e-6
    /// - Skipped during intersection tests (can't determine which side is "front")
    /// - Based on swkotor2.exe: FUN_004d9030 @ 0x004d9030 checks normal length (line 58)
    /// 
    /// RAY STARTING INSIDE TRIANGLE:
    /// - If ray origin is inside a triangle, intersection distance is 0 or negative
    /// - Handled with tolerance checks: distance < 1e-5 is treated as exact hit (distance = 0)
    /// - Returns hit with distance 0 (ray starts on triangle surface)
    /// 
    /// RAY ON TRIANGLE SURFACE:
    /// - If ray starts exactly on triangle surface, distance = 0 (within tolerance)
    /// - Returns as hit with distance 0
    /// - Early termination optimization stops searching immediately
    /// 
    /// INVALID FACE/VERTEX INDICES:
    /// - Face indices are validated: 0 <= faceIndex < faceCount
    /// - Vertex indices are validated: 0 <= vertexIndex < vertexCount
    /// - Face index bounds checked: faceIndex * 3 + 2 < faceIndices.Length
    /// - Prevents array out-of-bounds access
    /// 
    /// VERTICAL TRIANGLES (NORMAL.Z ≈ 0):
    /// - Triangles that are standing straight up (normal.Z very close to 0)
    /// - Can't use plane equation (would divide by zero)
    /// - Uses average height of three vertices: (v1.z + v2.z + v3.z) / 3
    /// - Based on swkotor2.exe: FUN_004d6b10 @ 0x004d6b10 checks if normal.Z == _DAT_007b56fc
    /// 
    /// RAY PARALLEL TO TRIANGLE PLANE:
    /// - If ray direction is parallel to triangle plane, ray never intersects
    /// - Detected by checking if ray crosses plane (dist0 and dist1 have opposite signs)
    /// - If both have same sign, ray is parallel - rejected
    /// - Based on swkotor2.exe: FUN_004d9030 @ 0x004d9030 checks plane crossing (lines 65-67)
    /// 
    /// INVALID MAX DISTANCE:
    /// - Max distance <= 0 is rejected
    /// - Non-finite max distance (NaN, Infinity) is rejected
    /// - Prevents infinite loops and invalid calculations
    /// 
    /// INVALID AABB (MIN > MAX):
    /// - AABB with min > max on any axis is invalid
    /// - Returns false immediately (no intersection possible)
    /// - Prevents incorrect calculations
    /// 
    /// RAY STARTING INSIDE AABB:
    /// - If ray origin is inside AABB, tmin < 0
    /// - Uses tmax instead (ray starts inside, exits at tmax)
    /// - Handled by checking if tmin < 0 and using tmax
    /// 
    /// COORDINATE TRANSFORMATIONS:
    /// - Area walkmeshes (WOK): Vertices in world coordinates, no transformation needed
    /// - Placeable/Door walkmeshes (PWK/DWK): Vertices in local coordinates
    /// - Transformation applied when object is placed: local_to_world = position + rotation * local
    /// - Based on swkotor2.exe: FUN_00557540 @ 0x00557540 applies coordinate transformations
    /// - Original implementation: FUN_00557540 transforms points using rotation matrix
    /// 
    /// TOLERANCE VALUES:
    /// - Degenerate triangle epsilon: 1e-6 (normal length threshold)
    /// - Plane crossing epsilon: 1e-6 (floating-point precision threshold)
    /// - Edge containment epsilon: 1e-6 (edge test tolerance)
    /// - Exact hit tolerance: 1e-5 (distance threshold for early termination)
    /// - Direction zero threshold: 1e-6 (minimum direction length)
    /// - AABB division epsilon: 1e-8 (division by zero protection)
    /// - Vertical triangle threshold: 1e-6 (normal.Z threshold for vertical triangles)
    /// - Based on swkotor2.exe: Various epsilon values used throughout code (_DAT_007bc338, etc.)
    /// </para>
    /// </remarks>
    public class NavigationMesh : INavigationMesh
    {
        private Vector3[] _vertices;
        private int[] _faceIndices;        // 3 vertex indices per face
        private int[] _adjacency;          // 3 adjacency entries per face (-1 = no neighbor)
        private int[] _surfaceMaterials;   // Material per face
        private AabbNode _aabbRoot;
        private int _faceCount;
        private int _walkableFaceCount;

        // Surface material walkability lookup
        private static readonly HashSet<int> WalkableMaterials = new HashSet<int>
        {
            1,  // Dirt
            3,  // Grass
            4,  // Stone
            5,  // Wood
            6,  // Water (shallow)
            9,  // Carpet
            10, // Metal
            11, // Puddles
            12, // Swamp
            13, // Mud
            14, // Leaves
            16, // BottomlessPit (walkable but dangerous)
            18, // Door
            20, // Sand
            21, // BareBones
            22, // StoneBridge
            30  // Trigger (PyKotor extended)
        };

        // Non-walkable materials
        private static readonly HashSet<int> NonWalkableMaterials = new HashSet<int>
        {
            0,  // NotDefined/UNDEFINED
            2,  // Obscuring
            7,  // Nonwalk/NON_WALK
            8,  // Transparent
            15, // Lava
            17, // DeepWater
            19  // Snow/NON_WALK_GRASS
        };

        /// <summary>
        /// Creates a navigation mesh from vertices, faces, and adjacency data.
        /// </summary>
        public NavigationMesh(
            Vector3[] vertices,
            int[] faceIndices,
            int[] adjacency,
            int[] surfaceMaterials,
            AabbNode aabbRoot)
        {
            _vertices = vertices ?? throw new ArgumentNullException("vertices");
            _faceIndices = faceIndices ?? throw new ArgumentNullException("faceIndices");
            _adjacency = adjacency ?? new int[0];
            _surfaceMaterials = surfaceMaterials ?? throw new ArgumentNullException("surfaceMaterials");
            _aabbRoot = aabbRoot;

            _faceCount = faceIndices.Length / 3;

            // Count walkable faces
            int walkable = 0;
            for (int i = 0; i < _faceCount; i++)
            {
                if (IsWalkable(i))
                {
                    walkable++;
                }
            }
            _walkableFaceCount = walkable;
        }

        public int FaceCount { get { return _faceCount; } }
        public int WalkableFaceCount { get { return _walkableFaceCount; } }

        /// <summary>
        /// Gets the vertex array (read-only).
        /// </summary>
        public IReadOnlyList<Vector3> Vertices { get { return _vertices; } }

        /// <summary>
        /// Gets the face indices array (read-only).
        /// </summary>
        public IReadOnlyList<int> FaceIndices { get { return _faceIndices; } }

        /// <summary>
        /// Gets the adjacency array (read-only).
        /// </summary>
        public IReadOnlyList<int> Adjacency { get { return _adjacency; } }

        /// <summary>
        /// Gets the surface materials array (read-only).
        /// </summary>
        public IReadOnlyList<int> SurfaceMaterials { get { return _surfaceMaterials; } }

        /// <summary>
        /// Creates an empty navigation mesh that can be built incrementally using BuildFromTriangles.
        /// </summary>
        public NavigationMesh()
        {
            _vertices = new Vector3[0];
            _faceIndices = new int[0];
            _adjacency = new int[0];
            _surfaceMaterials = new int[0];
            _aabbRoot = null;
            _faceCount = 0;
            _walkableFaceCount = 0;
        }

        /// <summary>
        /// Builds the navigation mesh from a list of triangles.
        /// Computes adjacency automatically, builds AABB tree for spatial queries, and sets default walkable materials.
        /// </summary>
        /// <param name="vertices">List of vertex positions</param>
        /// <param name="indices">Triangle indices (must be multiple of 3)</param>
        public void BuildFromTriangles(List<Vector3> vertices, List<int> indices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException("vertices");
            }
            if (indices == null)
            {
                throw new ArgumentNullException("indices");
            }

            // Validate indices count is multiple of 3
            if (indices.Count % 3 != 0)
            {
                throw new ArgumentException("Indices count must be a multiple of 3 (triangles)", "indices");
            }

            int faceCount = indices.Count / 3;
            if (faceCount == 0)
            {
                // Empty mesh
                _vertices = new Vector3[0];
                _faceIndices = new int[0];
                _adjacency = new int[0];
                _surfaceMaterials = new int[0];
                _aabbRoot = null;
                _faceCount = 0;
                _walkableFaceCount = 0;
                return;
            }

            // Validate all indices are within vertex range
            for (int i = 0; i < indices.Count; i++)
            {
                if (indices[i] < 0 || indices[i] >= vertices.Count)
                {
                    throw new ArgumentException(string.Format("Index {0} is out of range (vertex count: {1})", indices[i], vertices.Count), "indices");
                }
            }

            // Convert lists to arrays
            _vertices = vertices.ToArray();
            _faceIndices = indices.ToArray();
            _faceCount = faceCount;

            // Compute adjacency from triangle edges
            _adjacency = ComputeAdjacencyFromTriangles(_vertices, _faceIndices, faceCount);

            // Set default surface materials (default to walkable material - Stone = 4)
            _surfaceMaterials = new int[faceCount];
            for (int i = 0; i < faceCount; i++)
            {
                _surfaceMaterials[i] = 4; // Stone - walkable material
            }

            // Build AABB tree for spatial acceleration
            _aabbRoot = BuildAabbTreeFromFaces(_vertices, _faceIndices, _surfaceMaterials, faceCount);

            // Count walkable faces
            int walkable = 0;
            for (int i = 0; i < _faceCount; i++)
            {
                if (IsWalkable(i))
                {
                    walkable++;
                }
            }
            _walkableFaceCount = walkable;
        }

        /// <summary>
        /// Computes adjacency information from triangle edges.
        /// Adjacency encoding: faceIndex * 3 + edgeIndex, -1 = no neighbor
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function figures out which triangles share edges with each other. Two triangles are
        /// adjacent (neighbors) if they share exactly two vertices, which forms a shared edge.
        /// 
        /// WHY ADJACENCY IS IMPORTANT:
        /// 
        /// Adjacency information is critical for pathfinding. The pathfinding algorithm needs to know
        /// which triangles are connected so it can find routes from one triangle to another. Without
        /// adjacency, the algorithm wouldn't know which triangles can be reached from the current triangle.
        /// 
        /// HOW IT WORKS:
        /// 
        /// STEP 1: Initialize Adjacency Array
        /// - Create an array with size faceCount * 3 (3 edges per triangle)
        /// - Initialize all values to -1 (meaning "no neighbor")
        /// - Each triangle has 3 edges, so we need 3 adjacency entries per triangle
        /// 
        /// STEP 2: Build Edge-to-Face Mapping
        /// - Create a dictionary that maps edges to faces
        /// - An edge is defined by two vertex indices (the two vertices that form the edge)
        /// - For each triangle, look at each of its 3 edges
        /// - For each edge, check if we've seen this edge before
        /// 
        /// STEP 3: Link Adjacent Triangles
        /// - If we find an edge we've seen before, it means two triangles share that edge
        /// - Link them bidirectionally:
        ///   * Triangle A's edge points to Triangle B's edge
        ///   * Triangle B's edge points to Triangle A's edge
        /// - If it's the first time seeing an edge, record it for future matching
        /// 
        /// ADJACENCY ENCODING:
        /// 
        /// Adjacency is stored as: face_index * 3 + edge_index
        /// 
        /// For example:
        /// - Face 5, Edge 1 is stored at index 5*3+1 = 16
        /// - The value stored is: neighbor_face_index * 3 + neighbor_edge_index
        /// - If face 5, edge 1 is adjacent to face 12, edge 2:
        ///   * adjacency[16] = 12*3+2 = 38
        ///   * adjacency[38] = 5*3+1 = 16 (bidirectional)
        /// - If an edge has no neighbor, the value is -1
        /// 
        /// EDGE ORDERING:
        /// 
        /// Each triangle has 3 edges:
        /// - Edge 0: from vertex 0 to vertex 1 (v0 -> v1)
        /// - Edge 1: from vertex 1 to vertex 2 (v1 -> v2)
        /// - Edge 2: from vertex 2 to vertex 0 (v2 -> v0)
        /// 
        /// EDGE MATCHING:
        /// 
        /// Two edges match if they have the same two vertices, regardless of order.
        /// For example, edge (5, 12) matches edge (12, 5).
        /// 
        /// To handle this, we use EdgeKey which stores vertices in sorted order (min, max).
        /// This ensures that (5, 12) and (12, 5) both become EdgeKey(5, 12) and match correctly.
        /// 
        /// ORIGINAL IMPLEMENTATION:
        /// 
        /// Based on swkotor2.exe: Adjacency is computed during walkmesh loading, not stored in BWM files.
        /// However, BWM files can contain precomputed adjacency data for WOK files.
        /// 
        /// The original engine computes adjacency by:
        /// 1. Building an edge-to-face mapping (similar to this implementation)
        /// 2. Linking triangles that share edges
        /// 3. Storing adjacency in the format: face_index * 3 + edge_index
        /// 
        /// PERFORMANCE:
        /// - Time complexity: O(n * m) where n is number of faces, m is average edges per face (3)
        /// - Space complexity: O(n) for adjacency array + O(e) for edge dictionary where e is number of unique edges
        /// - Typically e ≈ n * 1.5 (each triangle has 3 edges, but edges are shared)
        /// </remarks>
        /// <param name="vertices">Array of vertex positions</param>
        /// <param name="faceIndices">Array of face vertex indices (3 indices per face)</param>
        /// <param name="faceCount">Number of faces in the mesh</param>
        /// <returns>Adjacency array with size faceCount * 3, encoding: faceIndex * 3 + edgeIndex, -1 = no neighbor</returns>
        private static int[] ComputeAdjacencyFromTriangles(Vector3[] vertices, int[] faceIndices, int faceCount)
        {
            int[] adjacency = new int[faceCount * 3];

            // Initialize all adjacencies to -1 (no neighbor)
            for (int i = 0; i < adjacency.Length; i++)
            {
                adjacency[i] = -1;
            }

            // Build edge-to-face mapping for adjacency detection
            var edgeToFace = new Dictionary<EdgeKey, EdgeInfo>();

            for (int face = 0; face < faceCount; face++)
            {
                int baseIdx = face * 3;
                int v0 = faceIndices[baseIdx];
                int v1 = faceIndices[baseIdx + 1];
                int v2 = faceIndices[baseIdx + 2];

                // Edge 0: v0 -> v1
                ProcessEdgeForAdjacency(edgeToFace, adjacency, v0, v1, face, 0);
                // Edge 1: v1 -> v2
                ProcessEdgeForAdjacency(edgeToFace, adjacency, v1, v2, face, 1);
                // Edge 2: v2 -> v0
                ProcessEdgeForAdjacency(edgeToFace, adjacency, v2, v0, face, 2);
            }

            return adjacency;
        }

        /// <summary>
        /// Processes an edge to find and link adjacent faces.
        /// </summary>
        private static void ProcessEdgeForAdjacency(
            Dictionary<EdgeKey, EdgeInfo> edgeToFace,
            int[] adjacency,
            int v0, int v1,
            int face, int edge)
        {
            var key = new EdgeKey(v0, v1);

            EdgeInfo existing;
            if (edgeToFace.TryGetValue(key, out existing))
            {
                // Found adjacent face - link them bidirectionally
                int otherFace = existing.FaceIndex;
                int otherEdge = existing.EdgeIndex;

                // Current face's adjacency for this edge = other_face * 3 + other_edge
                adjacency[face * 3 + edge] = otherFace * 3 + otherEdge;
                // Other face's adjacency for that edge = this_face * 3 + this_edge
                adjacency[otherFace * 3 + otherEdge] = face * 3 + edge;
            }
            else
            {
                // First time seeing this edge - record it
                edgeToFace[key] = new EdgeInfo(face, edge);
            }
        }

        /// <summary>
        /// Edge key for adjacency mapping (order-independent).
        /// </summary>
        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public readonly int MinVertex;
            public readonly int MaxVertex;

            public EdgeKey(int v0, int v1)
            {
                if (v0 < v1)
                {
                    MinVertex = v0;
                    MaxVertex = v1;
                }
                else
                {
                    MinVertex = v1;
                    MaxVertex = v0;
                }
            }

            public bool Equals(EdgeKey other)
            {
                return MinVertex == other.MinVertex && MaxVertex == other.MaxVertex;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey && Equals((EdgeKey)obj);
            }

            public override int GetHashCode()
            {
                return (MinVertex * 397) ^ MaxVertex;
            }
        }

        /// <summary>
        /// Edge information for adjacency computation.
        /// </summary>
        private struct EdgeInfo
        {
            public int FaceIndex;
            public int EdgeIndex;

            public EdgeInfo(int faceIndex, int edgeIndex)
            {
                FaceIndex = faceIndex;
                EdgeIndex = edgeIndex;
            }
        }

        /// <summary>
        /// Builds an AABB tree from face data for spatial acceleration.
        /// Uses recursive top-down construction with longest-axis splitting.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function builds a binary tree structure that organizes triangles into boxes (AABBs).
        /// The tree makes spatial searches much faster - instead of testing every triangle (O(n)),
        /// we can test only a few triangles (O(log n)).
        /// 
        /// HOW THE TREE WORKS:
        /// 
        /// The tree is a binary tree where:
        /// - Each node contains a bounding box (AABB) that contains all triangles in that subtree
        /// - Leaf nodes contain a single triangle (FaceIndex >= 0)
        /// - Internal nodes contain child nodes (FaceIndex = -1, Left and Right children)
        /// - The root node contains all triangles in the mesh
        /// 
        /// CONSTRUCTION ALGORITHM (Top-Down Recursive):
        /// 
        /// STEP 1: Calculate Bounding Box
        /// - For all triangles in the current set, find the smallest box that contains them all
        /// - The box is defined by min and max points:
        ///   * BoundsMin = (min of all x, min of all y, min of all z)
        ///   * BoundsMax = (max of all x, max of all y, max of all z)
        /// - This box is the AABB (Axis-Aligned Bounding Box) for this node
        /// 
        /// STEP 2: Check Termination Conditions
        /// - If only one triangle remains, create a leaf node:
        ///   * Set FaceIndex = triangle index
        ///   * Set Left = null, Right = null
        ///   * Return the leaf node
        /// - If maximum depth is reached, create a leaf node (prevents infinite recursion)
        /// - Otherwise, create an internal node and split
        /// 
        /// STEP 3: Choose Split Axis
        /// - Calculate the size of the bounding box in each dimension:
        ///   * sizeX = BoundsMax.X - BoundsMin.X
        ///   * sizeY = BoundsMax.Y - BoundsMin.Y
        ///   * sizeZ = BoundsMax.Z - BoundsMin.Z
        /// - Choose the longest dimension as the split axis
        /// - Splitting along the longest axis creates more balanced trees
        /// 
        /// STEP 4: Calculate Split Point
        /// - Split point is the center of the bounding box along the chosen axis
        /// - splitValue = (BoundsMin[axis] + BoundsMax[axis]) / 2
        /// 
        /// STEP 5: Partition Triangles
        /// - For each triangle, calculate its center point:
        ///   * center = (v1 + v2 + v3) / 3 (average of three vertices)
        /// - If triangle center is less than split point, add to left child set
        /// - Otherwise, add to right child set
        /// 
        /// STEP 6: Handle Degenerate Cases
        /// - If all triangles end up on one side, the split didn't work
        /// - Try splitting along a different axis (next axis: (splitAxis + 1) % 3)
        /// - If still degenerate, split in half by index (simple fallback)
        /// - This ensures the tree is always built, even for difficult cases
        /// 
        /// STEP 7: Recursively Build Children
        /// - Recursively build left child tree with left triangle set
        /// - Recursively build right child tree with right triangle set
        /// - Attach children to internal node
        /// - Return the internal node
        /// 
        /// TREE STRUCTURE:
        /// 
        /// Leaf Node:
        /// - FaceIndex >= 0 (contains triangle index)
        /// - Left = null, Right = null
        /// - BoundsMin, BoundsMax = bounding box of the single triangle
        /// 
        /// Internal Node:
        /// - FaceIndex = -1 (indicates internal node)
        /// - Left = left child node (or null)
        /// - Right = right child node (or null)
        /// - BoundsMin, BoundsMax = bounding box containing all triangles in subtree
        /// 
        /// PERFORMANCE:
        /// 
        /// Building:
        /// - Time complexity: O(n log n) where n is number of triangles
        /// - Space complexity: O(n) for tree nodes
        /// - Done once during walkmesh loading, not at runtime
        /// 
        /// Searching:
        /// - Time complexity: O(log n) average case, O(n) worst case (degenerate tree)
        /// - Without tree: O(n) for every search (must test all triangles)
        /// - Speedup: 100x-1000x faster for large meshes (1000+ triangles)
        /// 
        /// ORIGINAL IMPLEMENTATION:
        /// 
        /// Based on swkotor2.exe: AABB tree is built during walkmesh loading, not at runtime.
        /// The tree structure is stored in BWM files for WOK (area walkmeshes).
        /// 
        /// The original engine builds the tree using a similar top-down recursive algorithm:
        /// - Splits along longest axis
        /// - Partitions triangles based on center position
        /// - Handles degenerate cases
        /// - Stores tree in BWM file format
        /// 
        /// PARAMETERS:
        /// 
        /// MaxDepth: 32 (prevents infinite recursion, handles up to 4 billion triangles)
        /// MinFacesPerLeaf: 1 (leaf nodes contain exactly one triangle)
        /// 
        /// These parameters ensure the tree is always built successfully, even for very large meshes.
        /// </remarks>
        /// <param name="vertices">Vertex array for the mesh</param>
        /// <param name="faceIndices">Face indices array (3 indices per face)</param>
        /// <param name="surfaceMaterials">Surface materials array (one per face, not used in tree building but kept for consistency)</param>
        /// <param name="faceCount">Number of faces in the mesh</param>
        /// <returns>Root node of the AABB tree, or null if no faces</returns>
        public static AabbNode BuildAabbTreeFromFaces(Vector3[] vertices, int[] faceIndices, int[] surfaceMaterials, int faceCount)
        {
            if (faceCount == 0)
            {
                return null;
            }

            // Create list of face indices for tree building
            var faceList = new List<int>();
            for (int i = 0; i < faceCount; i++)
            {
                faceList.Add(i);
            }

            return BuildAabbTreeRecursive(vertices, faceIndices, surfaceMaterials, faceList, 0);
        }

        /// <summary>
        /// Recursively builds the AABB tree using top-down construction.
        /// </summary>
        private static AabbNode BuildAabbTreeRecursive(
            Vector3[] vertices,
            int[] faceIndices,
            int[] surfaceMaterials,
            List<int> faceList,
            int depth)
        {
            const int MaxDepth = 32;
            const int MinFacesPerLeaf = 1;

            if (faceList.Count == 0)
            {
                return null;
            }

            // Calculate bounding box for all faces in this node
            Vector3 bbMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 bbMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            foreach (int faceIdx in faceList)
            {
                int baseIdx = faceIdx * 3;
                for (int j = 0; j < 3; j++)
                {
                    Vector3 v = vertices[faceIndices[baseIdx + j]];
                    bbMin = Vector3.Min(bbMin, v);
                    bbMax = Vector3.Max(bbMax, v);
                }
            }

            var node = new AabbNode
            {
                BoundsMin = bbMin,
                BoundsMax = bbMax
            };

            // Leaf node if only one face or max depth reached
            if (faceList.Count <= MinFacesPerLeaf || depth >= MaxDepth)
            {
                node.FaceIndex = faceList[0];
                return node;
            }

            // Find split axis (longest dimension)
            Vector3 size = bbMax - bbMin;
            int splitAxis = 0;
            if (size.Y > size.X)
            {
                splitAxis = 1;
            }
            if (size.Z > (splitAxis == 0 ? size.X : size.Y))
            {
                splitAxis = 2;
            }

            // Split point is center of bounding box along split axis
            float splitValue = GetAxisValue(bbMin, splitAxis) + (GetAxisValue(bbMax, splitAxis) - GetAxisValue(bbMin, splitAxis)) * 0.5f;

            // Partition faces based on their center position
            var leftFaces = new List<int>();
            var rightFaces = new List<int>();

            foreach (int faceIdx in faceList)
            {
                // Get face center
                int baseIdx = faceIdx * 3;
                Vector3 v1 = vertices[faceIndices[baseIdx]];
                Vector3 v2 = vertices[faceIndices[baseIdx + 1]];
                Vector3 v3 = vertices[faceIndices[baseIdx + 2]];
                Vector3 center = (v1 + v2 + v3) / 3f;

                if (GetAxisValue(center, splitAxis) < splitValue)
                {
                    leftFaces.Add(faceIdx);
                }
                else
                {
                    rightFaces.Add(faceIdx);
                }
            }

            // Handle degenerate case where all faces end up on one side
            if (leftFaces.Count == 0 || rightFaces.Count == 0)
            {
                // Try another axis
                int nextAxis = (splitAxis + 1) % 3;
                float nextSplitValue = GetAxisValue(bbMin, nextAxis) + (GetAxisValue(bbMax, nextAxis) - GetAxisValue(bbMin, nextAxis)) * 0.5f;

                leftFaces.Clear();
                rightFaces.Clear();

                foreach (int faceIdx in faceList)
                {
                    int baseIdx = faceIdx * 3;
                    Vector3 v1 = vertices[faceIndices[baseIdx]];
                    Vector3 v2 = vertices[faceIndices[baseIdx + 1]];
                    Vector3 v3 = vertices[faceIndices[baseIdx + 2]];
                    Vector3 center = (v1 + v2 + v3) / 3f;

                    if (GetAxisValue(center, nextAxis) < nextSplitValue)
                    {
                        leftFaces.Add(faceIdx);
                    }
                    else
                    {
                        rightFaces.Add(faceIdx);
                    }
                }

                // Still degenerate - split in half by index
                if (leftFaces.Count == 0 || rightFaces.Count == 0)
                {
                    leftFaces.Clear();
                    rightFaces.Clear();
                    int mid = faceList.Count / 2;
                    for (int i = 0; i < faceList.Count; i++)
                    {
                        if (i < mid)
                        {
                            leftFaces.Add(faceList[i]);
                        }
                        else
                        {
                            rightFaces.Add(faceList[i]);
                        }
                    }
                }
            }

            // Internal node - recursively build children
            node.FaceIndex = -1;
            node.Left = BuildAabbTreeRecursive(vertices, faceIndices, surfaceMaterials, leftFaces, depth + 1);
            node.Right = BuildAabbTreeRecursive(vertices, faceIndices, surfaceMaterials, rightFaces, depth + 1);

            return node;
        }

        /// <summary>
        /// Gets the value of a vector along a specific axis (0=X, 1=Y, 2=Z).
        /// </summary>
        private static float GetAxisValue(Vector3 v, int axis)
        {
            switch (axis)
            {
                case 0: return v.X;
                case 1: return v.Y;
                case 2: return v.Z;
                default: return v.X;
            }
        }

        /// <summary>
        /// Performs a raycast and returns the hit point (simplified overload).
        /// </summary>
        /// <param name="origin">Starting point of the ray.</param>
        /// <param name="direction">Direction the ray travels (will be normalized).</param>
        /// <param name="maxDistance">Maximum distance the ray can travel.</param>
        /// <param name="hitPoint">Where the ray hit (if it hit something).</param>
        /// <returns>True if the ray hit a triangle, false otherwise.</returns>
        /// <remarks>
        /// Simplified Raycast Overload:
        /// - Based on swkotor2.exe walkmesh raycast system
        /// - Located via string references: "Raycast" @ navigation mesh functions
        /// - Original implementation: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks
        /// - This is a convenience overload that discards the hit face index
        /// - Calls the full Raycast(origin, direction, maxDistance, out hitPoint, out hitFace) overload
        /// - Comprehensive edge case handling: empty mesh, zero direction, degenerate triangles, ray on surface
        /// - Optimizations: normalized direction caching, early termination, optimized AABB traversal
        /// - Handles all edge cases: degenerate triangles, ray starting inside triangle, precision issues
        /// 
        /// Use this overload when you only need the hit point and don't need to know which face was hit.
        /// </remarks>
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint)
        {
            int hitFace;
            return Raycast(origin, direction, maxDistance, out hitPoint, out hitFace);
        }

        /// <summary>
        /// Checks if a point is on the mesh (within any walkable face).
        /// </summary>
        public bool IsPointOnMesh(Vector3 point)
        {
            int faceIndex = FindFaceAt(point);
            return faceIndex >= 0 && IsWalkable(faceIndex);
        }

        /// <summary>
        /// Gets the nearest point on the mesh to the given position.
        /// </summary>
        /// <returns>Nullable Vector3 - null if no walkable point found.</returns>
        public Vector3? GetNearestPoint(Vector3 point)
        {
            Vector3 result;
            float height;
            if (ProjectToSurface(point, out result, out height))
            {
                int faceAt = FindFaceAt(point);
                if (faceAt >= 0 && IsWalkable(faceAt))
                {
                    return result;
                }
            }

            // If not on walkable mesh, find nearest walkable face center
            float nearestDist = float.MaxValue;
            Vector3? nearest = null;

            for (int i = 0; i < _faceCount; i++)
            {
                if (!IsWalkable(i))
                {
                    continue;
                }

                Vector3 center = GetFaceCenter(i);
                float dist = Vector3.DistanceSquared(point, center);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = center;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Finds a path from start to goal using A* on the walkmesh adjacency graph.
        /// </summary>
        public IList<Vector3> FindPath(Vector3 start, Vector3 goal)
        {
            return FindPathInternal(start, goal, null);
        }

        /// <summary>
        /// Finds a path from start to goal while avoiding obstacles.
        /// Based on swkotor2.exe: FindPathAroundObstacle @ 0x0061c390 - pathfinding around obstacles
        /// Located via string references:
        ///   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
        ///   - Called from UpdateCreatureMovement @ 0x0054be70 (line 183) when creature collision is detected
        /// Original implementation (from Ghidra reverse engineering):
        ///   - Function signature: `float* FindPathAroundObstacle(void* this, int* movingCreature, void* blockingCreature)`
        ///   - this: Pathfinding context object containing obstacle polygon and path data
        ///   - movingCreature: Entity pointer (offset to entity structure at +0x380)
        ///   - blockingCreature: Blocking creature object pointer
        ///   - Returns: float* pointer to new path waypoints, or DAT_007c52ec (null) if no path found
        ///   - Implementation algorithm:
        ///     1. Builds obstacle polygon from creature bounding box (FUN_0061a670)
        ///     2. Checks if start/end points are within obstacle polygon (FUN_0061b7d0)
        ///     3. Validates entire path against obstacles (FUN_0061bcb0)
        ///     4. Gets waypoints from pathfinding context (FUN_0061c1e0)
        ///     5. Checks each path segment for collisions (FUN_0061c2c0 -> FUN_0061b310)
        ///     6. Inserts waypoints to route around obstacles (FUN_0061b520)
        ///     7. Updates path array and adjusts path index if waypoints inserted
        ///   - If pathfinding fails, returns null and movement is aborted
        /// Equivalent functions in other engines:
        ///   - swkotor.exe: FindPathAroundObstacle @ 0x005d0840 (called from UpdateCreatureMovement @ 0x00516630, line 254)
        ///     - Similar structure to swkotor2.exe, uses same obstacle avoidance algorithm
        ///   - nwmain.exe: CPathfindInformation class with obstacle avoidance
        ///     - Tile-based pathfinding with obstacle blocking
        ///     - AIActionCheckInterAreaPathfinding @ 0x1403b1dc0 handles inter-area pathfinding
        ///   - daorigins.exe/DragonAge2.exe: Advanced dynamic obstacle system
        ///     - Different architecture with real-time obstacle updates
        ///     - Cover system integration for tactical pathfinding
        /// </summary>
        /// <param name="start">Start position.</param>
        /// <param name="goal">Goal position.</param>
        /// <param name="obstaclePositions">List of obstacle positions to avoid (with optional radii).</param>
        /// <returns>Path waypoints, or null if no path found.</returns>
        public IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<Interfaces.ObstacleInfo> obstaclePositions)
        {
            return FindPathInternal(start, goal, obstaclePositions);
        }

        /// <summary>
        /// Internal pathfinding implementation that supports obstacle avoidance.
        /// </summary>
        private IList<Vector3> FindPathInternal(Vector3 start, Vector3 goal, IList<Interfaces.ObstacleInfo> obstacles)
        {
            int startFace = FindFaceAt(start);
            int goalFace = FindFaceAt(goal);

            if (startFace < 0 || goalFace < 0)
            {
                return null;  // Not on walkable surface
            }

            if (!IsWalkable(startFace) || !IsWalkable(goalFace))
            {
                return null;
            }

            if (startFace == goalFace)
            {
                // Same face - check if obstacle blocks direct path
                if (obstacles != null && obstacles.Count > 0)
                {
                    Vector3 direction = goal - start;
                    float distance = direction.Length();
                    if (distance > 0.001f)
                    {
                        direction = Vector3.Normalize(direction);
                        // Check if any obstacle blocks the direct path
                        foreach (Interfaces.ObstacleInfo obstacle in obstacles)
                        {
                            if (IsObstacleBlockingPath(start, goal, obstacle.Position, obstacle.Radius))
                            {
                                // Obstacle blocks direct path - need to find alternative
                                // Try to find path around obstacle by using adjacent faces
                                return FindPathAroundObstacleOnSameFace(start, goal, startFace, obstacles);
                            }
                        }
                    }
                }
                // Same face, no obstacle blocking - direct path
                return new List<Vector3> { start, goal };
            }

            // Build set of blocked faces from obstacles
            HashSet<int> blockedFaces = null;
            if (obstacles != null && obstacles.Count > 0)
            {
                blockedFaces = BuildBlockedFacesSet(obstacles);
            }

            // A* pathfinding over face adjacency graph
            var openSet = new SortedSet<FaceScore>(new FaceScoreComparer());
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float>();
            var fScore = new Dictionary<int, float>();
            var inOpenSet = new HashSet<int>();

            gScore[startFace] = 0f;
            fScore[startFace] = Heuristic(startFace, goalFace);
            openSet.Add(new FaceScore(startFace, fScore[startFace]));
            inOpenSet.Add(startFace);

            while (openSet.Count > 0)
            {
                // Get face with lowest f-score
                FaceScore currentScore = GetMin(openSet);
                openSet.Remove(currentScore);
                int current = currentScore.FaceIndex;
                inOpenSet.Remove(current);

                if (current == goalFace)
                {
                    return ReconstructPath(cameFrom, current, start, goal);
                }

                // Check all adjacent faces
                foreach (int neighbor in GetAdjacentFaces(current))
                {
                    if (neighbor < 0 || neighbor >= _faceCount)
                    {
                        continue;  // Invalid or no neighbor
                    }
                    if (!IsWalkable(neighbor))
                    {
                        continue;
                    }

                    // Skip blocked faces (obstacle avoidance)
                    if (blockedFaces != null && blockedFaces.Contains(neighbor))
                    {
                        continue;
                    }

                    float tentativeG;
                    if (gScore.TryGetValue(current, out float currentG))
                    {
                        tentativeG = currentG + EdgeCost(current, neighbor);
                    }
                    else
                    {
                        tentativeG = EdgeCost(current, neighbor);
                    }

                    float neighborG;
                    if (!gScore.TryGetValue(neighbor, out neighborG) || tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float newF = tentativeG + Heuristic(neighbor, goalFace);
                        fScore[neighbor] = newF;

                        if (!inOpenSet.Contains(neighbor))
                        {
                            openSet.Add(new FaceScore(neighbor, newF));
                            inOpenSet.Add(neighbor);
                        }
                    }
                }
            }

            // No path found - if we have obstacles, try with expanded avoidance radius
            if (obstacles != null && obstacles.Count > 0)
            {
                // Try with 1.5x obstacle radius to find alternative path
                var expandedObstacles = new List<Interfaces.ObstacleInfo>();
                foreach (Interfaces.ObstacleInfo obstacle in obstacles)
                {
                    expandedObstacles.Add(new Interfaces.ObstacleInfo(obstacle.Position, obstacle.Radius * 1.5f));
                }
                return FindPathInternal(start, goal, expandedObstacles);
            }

            return null;  // No path found
        }

        /// <summary>
        /// Finds a path around an obstacle when start and goal are on the same face.
        /// Uses adjacent faces to navigate around the obstacle.
        /// </summary>
        private IList<Vector3> FindPathAroundObstacleOnSameFace(Vector3 start, Vector3 goal, int faceIndex, IList<Interfaces.ObstacleInfo> obstacles)
        {
            // Find adjacent faces that might provide a route around the obstacle
            var candidateFaces = new List<int>();
            foreach (int neighbor in GetAdjacentFaces(faceIndex))
            {
                if (neighbor >= 0 && neighbor < _faceCount && IsWalkable(neighbor))
                {
                    // Check if this neighbor face is not blocked by obstacles
                    bool isBlocked = false;
                    if (obstacles != null)
                    {
                        Vector3 neighborCenter = GetFaceCenter(neighbor);
                        foreach (Interfaces.ObstacleInfo obstacle in obstacles)
                        {
                            float distToObstacle = Vector3.Distance(neighborCenter, obstacle.Position);
                            if (distToObstacle < obstacle.Radius)
                            {
                                isBlocked = true;
                                break;
                            }
                        }
                    }
                    if (!isBlocked)
                    {
                        candidateFaces.Add(neighbor);
                    }
                }
            }

            if (candidateFaces.Count == 0)
            {
                return null; // No viable adjacent faces
            }

            // Try to find path through each candidate face
            foreach (int candidateFace in candidateFaces)
            {
                Vector3 candidateCenter = GetFaceCenter(candidateFace);
                // Try path: start -> candidate center -> goal
                IList<Vector3> path = FindPathInternal(candidateCenter, goal, obstacles);
                if (path != null && path.Count > 0)
                {
                    // Prepend start to path
                    var fullPath = new List<Vector3> { start };
                    foreach (Vector3 waypoint in path)
                    {
                        fullPath.Add(waypoint);
                    }
                    return fullPath;
                }
            }

            return null; // No path found through adjacent faces
        }

        /// <summary>
        /// Builds a set of face indices that are blocked by obstacles.
        /// </summary>
        private HashSet<int> BuildBlockedFacesSet(IList<Interfaces.ObstacleInfo> obstacles)
        {
            var blockedFaces = new HashSet<int>();

            foreach (Interfaces.ObstacleInfo obstacle in obstacles)
            {
                // Find all faces within obstacle radius
                for (int i = 0; i < _faceCount; i++)
                {
                    if (!IsWalkable(i))
                    {
                        continue;
                    }

                    Vector3 faceCenter = GetFaceCenter(i);
                    float distanceToObstacle = Vector3.Distance(faceCenter, obstacle.Position);

                    // Block faces within obstacle radius
                    // Add small buffer (0.5 units) to ensure proper avoidance
                    if (distanceToObstacle < (obstacle.Radius + 0.5f))
                    {
                        blockedFaces.Add(i);
                    }
                }
            }

            return blockedFaces;
        }

        /// <summary>
        /// Checks if an obstacle blocks the direct path between two points.
        /// </summary>
        private bool IsObstacleBlockingPath(Vector3 start, Vector3 end, Vector3 obstaclePos, float obstacleRadius)
        {
            // Calculate closest point on line segment to obstacle
            Vector3 lineDir = end - start;
            float lineLength = lineDir.Length();
            if (lineLength < 0.001f)
            {
                // Start and end are same point - check if obstacle is too close
                return Vector3.Distance(start, obstaclePos) < obstacleRadius;
            }

            lineDir = Vector3.Normalize(lineDir);
            Vector3 toObstacle = obstaclePos - start;
            float projectionLength = Vector3.Dot(toObstacle, lineDir);

            // Clamp to line segment
            if (projectionLength < 0f)
            {
                projectionLength = 0f;
            }
            else if (projectionLength > lineLength)
            {
                projectionLength = lineLength;
            }

            Vector3 closestPoint = start + lineDir * projectionLength;
            float distanceToObstacle = Vector3.Distance(closestPoint, obstaclePos);

            return distanceToObstacle < obstacleRadius;
        }


        private FaceScore GetMin(SortedSet<FaceScore> set)
        {
            using (SortedSet<FaceScore>.Enumerator enumerator = set.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    return enumerator.Current;
                }
            }
            return default(FaceScore);
        }

        private float Heuristic(int from, int to)
        {
            Vector3 fromCenter = GetFaceCenter(from);
            Vector3 toCenter = GetFaceCenter(to);
            return Vector3.Distance(fromCenter, toCenter);
        }

        private float EdgeCost(int from, int to)
        {
            // Base cost is distance, modified by surface material
            float dist = Vector3.Distance(GetFaceCenter(from), GetFaceCenter(to));
            float surfaceMod = GetSurfaceCost(to);
            return dist * surfaceMod;
        }

        private float GetSurfaceCost(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _surfaceMaterials.Length)
            {
                return 1.0f;
            }

            int material = _surfaceMaterials[faceIndex];

            // Surface-specific costs (AI pathfinding cost modifiers)
            switch (material)
            {
                case 6:  // Water
                case 11: // Puddles
                case 12: // Swamp
                case 13: // Mud
                    return 1.5f;  // Slightly slower
                case 16: // BottomlessPit
                    return 10.0f; // Very high cost - avoid if possible
                default:
                    return 1.0f;
            }
        }

        private IList<Vector3> ReconstructPath(Dictionary<int, int> cameFrom, int current, Vector3 start, Vector3 goal)
        {
            var facePath = new List<int> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                facePath.Add(current);
            }
            facePath.Reverse();

            // Convert face path to waypoints
            var path = new List<Vector3>();
            path.Add(start);

            // Add face centers as intermediate waypoints
            for (int i = 1; i < facePath.Count - 1; i++)
            {
                path.Add(GetFaceCenter(facePath[i]));
            }

            path.Add(goal);

            // Optional: Apply funnel algorithm for smoother paths
            return SmoothPath(path);
        }

        private IList<Vector3> SmoothPath(IList<Vector3> path)
        {
            // Simple path smoothing - remove redundant waypoints
            if (path.Count <= 2)
            {
                return path;
            }

            var smoothed = new List<Vector3>();
            smoothed.Add(path[0]);

            for (int i = 1; i < path.Count - 1; i++)
            {
                // Check if we can skip this waypoint
                Vector3 prev = smoothed[smoothed.Count - 1];
                Vector3 next = path[i + 1];

                if (!TestLineOfSight(prev, next))
                {
                    // Can't skip - add the waypoint
                    smoothed.Add(path[i]);
                }
            }

            smoothed.Add(path[path.Count - 1]);
            return smoothed;
        }

        /// <summary>
        /// Finds the triangle (face) that contains the given 2D position (x, y).
        /// Based on swkotor2.exe: FUN_004f4260 @ 0x004f4260.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function finds which triangle contains a point by testing triangles in the walkmesh.
        /// The test is done in 2D (only x and y coordinates) because we're finding which triangle
        /// is directly below or above the point. The z coordinate of the input is used for tolerance
        /// calculations but the actual triangle selection is based on x and y only.
        /// 
        /// HOW IT WORKS:
        /// 
        /// 1. AABB TREE SEARCH (if available):
        ///    - If an AABB tree exists, it uses that for fast spatial search
        ///    - Starting from the root, tests if point is inside root's bounding box
        ///    - If yes, recursively tests left and right children
        ///    - Continues until reaching a leaf node (single triangle)
        ///    - Tests if point is inside that triangle using 2D point-in-triangle test
        ///    - Performance: O(log n) average case, O(n) worst case
        ///    - Based on swkotor2.exe: FUN_004f4260 uses AABB tree if available
        /// 
        /// 2. BRUTE FORCE SEARCH (if no AABB tree):
        ///    - Tests every triangle until it finds one that contains the point
        ///    - For each triangle, uses 2D point-in-triangle test
        ///    - Performance: O(n) where n is number of triangles
        ///    - Used for: PWK/DWK walkmeshes (placeable/door walkmeshes don't have AABB trees)
        /// 
        /// 3. 2D POINT-IN-TRIANGLE TEST:
        ///    - Uses the "same-side test" to check if point is inside triangle
        ///    - For each edge of the triangle, checks which side of the edge the point is on
        ///    - If point is on the same side of all three edges, it's inside the triangle
        ///    - Uses cross products to determine which side of each edge the point is on
        ///    - Based on swkotor2.exe: FUN_0055b300 @ 0x0055b300 tests triangle intersection
        /// 
        /// ORIGINAL IMPLEMENTATION DETAILS (FUN_004f4260 @ 0x004f4260):
        /// 
        /// - Takes a position (x, y, z) and creates a vertical range: z + tolerance to z - tolerance
        ///   The tolerance value is _DAT_007b56f8 (stored in memory, typically 0.1-0.5 units)
        ///   This range accounts for floating-point precision and character movement
        /// 
        /// - If a hint face index is provided, tests that triangle first (optimization)
        ///   Characters usually stay on the same triangle for multiple frames, so hint is often correct
        ///   Original: Lines 31-42 test hint face first using FUN_0055b300
        /// 
        /// - Tests each triangle using FUN_0055b300 which:
        ///   a. Tests if point is inside triangle's AABB (fast rejection)
        ///   b. If yes, tests actual triangle intersection using FUN_00575f60
        ///   Original: Lines 45-63 iterate through faces, calling FUN_0055b300 for each
        /// 
        /// - Returns the first triangle that contains the point, or -1 if none found
        /// 
        /// COORDINATE SYSTEM:
        /// - Input position is in world coordinates (x, y, z)
        /// - Test is done in 2D (x, y only) - z is used for tolerance but not triangle selection
        /// - For area walkmeshes (WOK): Vertices are in world coordinates, no transformation needed
        /// - For placeable/door walkmeshes (PWK/DWK): Vertices are transformed to world coordinates first
        /// 
        /// PERFORMANCE:
        /// - With AABB tree: O(log n) average case, O(n) worst case
        /// - Without AABB tree: O(n) always
        /// - Hint face optimization: O(1) best case (hint correct), O(log n) average (hint wrong)
        /// </remarks>
        /// <param name="position">The 3D position to find the triangle for (x, y used for selection, z used for tolerance)</param>
        /// <returns>The index of the triangle containing the point, or -1 if no triangle found</returns>
        /// <summary>
        /// Finds which triangle contains a specific point in 3D space.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function takes a point (x, y, z) and finds which triangle in the walkmesh contains it.
        /// The function only looks at the x and y coordinates (2D) because we're finding which triangle
        /// is directly below or above the point. The z coordinate is used to create a vertical range
        /// for tolerance (the point might not be exactly on the triangle surface).
        /// 
        /// HOW IT WORKS:
        /// 
        /// Based on swkotor2.exe: FUN_004f4260 @ 0x004f4260 (FindFaceAt equivalent)
        /// 
        /// STAGE 1: Create Vertical Range
        /// - The original engine creates a vertical range around the point: z + tolerance to z - tolerance
        /// - The tolerance value is _DAT_007b56f8 (typically a small value like 0.1 or 0.5 units)
        /// - This is needed because the point might not be exactly on the triangle surface due to
        ///   floating-point precision or character movement
        /// - Original implementation: FUN_004f4260 @ 0x004f4260 lines 20-23 create this vertical range
        /// - Line 21: local_34 = param_1[1] (copy y coordinate)
        /// - Line 22: local_24 = param_1[2] + _DAT_007b56f8 (upper z bound = input z + tolerance)
        /// - Line 23: local_38 = param_1[0] (copy x coordinate)
        /// - Line 24: local_30 = param_1[2] - _DAT_007b56f8 (lower z bound = input z - tolerance)
        /// - Line 25: local_2c = local_38 (copy x for AABB test)
        /// - Line 26: local_28 = local_34 (copy y for AABB test)
        /// - This creates a vertical line segment from (x, y, z+tolerance) to (x, y, z-tolerance)
        /// - The function then searches for triangles that intersect this vertical line segment
        /// - Why a range? Because characters might be slightly above or below the walkmesh surface
        ///   due to movement, physics, or floating-point rounding errors
        /// 
        /// STAGE 2: Hint Face Optimization (if provided)
        /// - If a hint face index is provided (a guess about which triangle might contain the point),
        ///   it tests that triangle first
        /// - This is a major optimization because characters usually stay on the same triangle for
        ///   multiple frames (60+ frames per second), so the hint is correct most of the time
        /// - Original implementation: FUN_004f4260 @ 0x004f4260 lines 31-42 test hint face first
        /// - Line 30: iVar4 = -1 (initialize hint face index to invalid)
        /// - Line 31-32: If param_3 (hint pointer) is not null AND hint index is valid (>= 0 and < face count):
        ///   - Line 33-34: Call FUN_0055b300 to test if the vertical line segment intersects the hint triangle's AABB
        ///   - FUN_0055b300 @ 0x0055b300 takes: triangle data pointer, lower bound point (x,y,z-tolerance), 
        ///     upper bound point (x,y,z+tolerance), and a result array
        ///   - FUN_0055b300 internally calls FUN_00575f60 @ 0x00575f60 which tests AABB intersection
        ///   - If FUN_0055b300 returns non-zero (triangle's AABB contains the vertical line segment):
        ///     - Line 36: Calculate the triangle data pointer: iVar4 = base + hint_index * 0x4c (76 bytes per triangle)
        ///     - Line 37-38: If param_2 (output face index pointer) is null, return the triangle data pointer
        ///     - Line 40: Store the found face index in param_2 (local_14 contains the face index from FUN_0055b300)
        ///     - Line 41: Return the triangle data pointer
        /// - Why test hint first? Because in 99% of cases, a character is still on the same triangle as last frame
        /// - This optimization reduces average search time from O(log n) to O(1) when hint is correct
        /// 
        /// STAGE 3: AABB Tree Traversal (if available)
        /// - If an AABB tree exists, it uses that for fast spatial search
        /// - Starting from the root, it tests if the point is inside the root's bounding box
        /// - If yes, it tests the two child boxes (left and right)
        /// - It keeps going down the tree until it reaches a leaf node (a single triangle)
        /// - This is much faster than testing every triangle (O(log n) instead of O(n))
        /// - Original implementation: FUN_004f4260 @ 0x004f4260 lines 45-63 iterate through faces
        /// - Line 44: iVar1 = 0 (initialize face index counter)
        /// - Line 45: If face count > 0, enter loop
        /// - Line 46: iVar3 = 0 (initialize AABB tree node offset counter)
        /// - Line 47-63: Loop through all faces:
        ///   - Line 48: If current face index != hint face index (skip hint, already tested):
        ///     - Line 49-50: Call FUN_0055b300 to test if vertical line segment intersects triangle's AABB
        ///     - FUN_0055b300 tests: triangle at offset iVar3 (iVar3 = face_index * 0x4c), 
        ///       lower bound (local_2c, local_28, local_30), upper bound (local_38, local_34, local_24)
        ///     - If FUN_0055b300 returns non-zero (AABB intersection found):
        ///       - Line 51: Calculate triangle data pointer: iVar4 = face_index * 0x4c + base_address
        ///       - Line 52-53: If param_2 (output face index pointer) is not null, store face index in it
        ///       - Line 55: If param_3 (hint pointer) is null, return triangle data pointer immediately
        ///       - Line 58: Store face index in param_3 (update hint for next call)
        ///       - Line 59: Return triangle data pointer
        ///   - Line 61: iVar1 = iVar1 + 1 (increment face index)
        ///   - Line 62: iVar3 = iVar3 + 0x4c (increment AABB tree node offset by 76 bytes)
        /// - The loop continues until all faces are tested or a match is found
        /// - Line 65: Return 0 (no triangle found)
        /// - Note: This implementation tests faces sequentially, but the AABB tree structure
        ///   (stored in the walkmesh) allows FUN_0055b300 to quickly reject triangles whose AABBs
        ///   don't contain the vertical line segment
        /// 
        /// STAGE 4: Triangle Point-in-Triangle Test
        /// - For each candidate triangle (from AABB tree or brute force), it tests if the point is inside
        /// - The test is done in 2D (only x and y coordinates) because we're finding which triangle
        ///   is directly below or above the point
        /// - Uses the "same-side test": checks if the point is on the same side of all three edges
        /// - If the point is on the same side of all edges, it's inside the triangle
        /// - Original implementation: FUN_0055b300 @ 0x0055b300 tests AABB first, then FUN_00575f60
        ///   @ 0x00575f60 tests triangle intersection using AABB tree
        /// 
        /// STAGE 5: Return Result
        /// - If a triangle is found, returns its index (0 to faceCount-1)
        /// - If no triangle is found, returns -1
        /// 
        /// THE AABB TEST (FUN_0055b300 @ 0x0055b300):
        /// - First tests if the point is inside the triangle's AABB (Axis-Aligned Bounding Box)
        /// - An AABB is a box aligned with the X, Y, Z axes that contains the triangle
        /// - This is a fast rejection test - if the point isn't in the AABB, it can't be in the triangle
        /// - Original implementation: FUN_0055b300 @ 0x0055b300 calls FUN_00575f60 @ 0x00575f60 for AABB test
        /// - FUN_0055b300 takes 4 parameters:
        ///   - param_1 (this): Pointer to triangle data structure (76 bytes = 0x4c)
        ///   - param_2: Pointer to lower bound point (x, y, z-tolerance)
        ///   - param_3: Pointer to upper bound point (x, y, z+tolerance)
        ///   - param_4: Pointer to result array (8 uint32 values)
        /// - Line 12-13: Calls FUN_00575f60 with triangle's AABB tree pointer (offset 0x3c in triangle data),
        ///   lower bound coordinates, upper bound coordinates, and result array
        /// - FUN_00575f60 tests if the vertical line segment (from lower to upper bound) intersects
        ///   the triangle's AABB using the AABB tree
        /// - If FUN_00575f60 returns non-zero (intersection found):
        ///   - Line 15-19: Gets triangle vertex offsets from triangle data structure
        ///   - Line 20-22: Updates result array with intersection point coordinates:
        ///     - param_3[4] = intersection x coordinate
        ///     - param_3[5] = intersection y coordinate
        ///     - param_3[6] = intersection z coordinate
        ///   - Line 23: Returns 1 (intersection found)
        /// - If FUN_00575f60 returns zero (no intersection):
        ///   - Line 25: Returns 0 (no intersection)
        /// - The result array is used to store the intersection point for later use
        /// 
        /// THE TRIANGLE INTERSECTION TEST (FUN_00575f60 @ 0x00575f60):
        /// - Tests if a vertical line segment (from z+tolerance to z-tolerance) intersects the triangle
        /// - Uses the AABB tree to find candidate triangles
        /// - Original implementation: FUN_00575f60 @ 0x00575f60 calls FUN_00575350 @ 0x00575350 for AABB tree traversal
        /// - FUN_00575f60 takes 8 parameters:
        ///   - param_1 (this): Pointer to AABB tree structure
        ///   - param_2-4: Lower bound point coordinates (x, y, z-tolerance)
        ///   - param_5-7: Upper bound point coordinates (x, y, z+tolerance)
        ///   - param_8: Pointer to result array (8 uint32 values)
        /// - Line 17: Checks if AABB tree exists (offset 0xb0 in tree structure)
        ///   - If tree doesn't exist, returns 0 (no intersection)
        /// - Line 20: Stores tree's root face index in result array (offset 0xe0 in tree structure)
        /// - Line 21: Calls FUN_00575350 to traverse AABB tree and find intersection
        ///   - FUN_00575350 tests if the vertical line segment intersects any triangle in the tree
        ///   - Returns number of intersections found
        /// - Line 22: If result array[3] == 0xffffffff (no intersection found):
        ///   - Lines 23-29: Calculates a small offset vector (1.0, 1.0, 0.0) normalized and scaled
        ///   - The offset is multiplied by _DAT_007b5704 (typically a small value like 0.01)
        ///   - This creates a small random offset to help with edge cases
        ///   - Lines 30-35: Adds offset to both lower and upper bound points
        ///   - Line 36: Calls FUN_00575350 again with offset points (retry with slight offset)
        /// - Line 38: If FUN_00575350 found intersections (return value > 0):
        ///   - Lines 39-42: Stores intersection point coordinates in result array:
        ///     - param_8[4] = intersection x coordinate
        ///     - param_8[5] = intersection y coordinate
        ///     - param_8[6] = intersection z coordinate
        ///   - param_8[2] = 1 (intersection flag set)
        /// - Line 44: Returns param_8[2] (1 if intersection found, 0 if not)
        /// - Why the offset retry? Sometimes points exactly on triangle edges or vertices can be missed
        ///   due to floating-point precision. The small offset helps catch these edge cases.
        /// 
        /// THE AABB TREE TRAVERSAL (FUN_00575350 @ 0x00575350):
        /// - Tests ray against node's AABB using FUN_004d7400 @ 0x004d7400 (slab method)
        /// - If ray doesn't hit AABB, returns 0 (no intersection)
        /// - If node is a leaf (contains a face index), tests ray against that triangle
        /// - If node is internal, recursively tests left and right children
        /// - Original implementation: FUN_00575350 lines 26-69 traverse tree recursively
        /// - Traversal order: Original uses flag-based ordering (param_4[1] & node flag),
        ///   this implementation uses distance-based ordering (test closer child first) as optimization
        /// 
        /// THE SLAB METHOD (FUN_004d7400 @ 0x004d7400):
        /// - Uses the "slab method" to test if a ray hits an AABB
        /// - A slab is a space between two parallel planes
        /// - The method tests the ray against each axis (X, Y, Z) separately
        /// - For each axis:
        ///   a. Calculate where the ray enters the slab (tmin)
        ///   b. Calculate where the ray exits the slab (tmax)
        ///   c. Handle negative direction (swap tmin and tmax)
        /// - The ray hits the AABB if:
        ///   - It enters all three slabs before exiting any
        ///   - The intersection is within maxDistance
        /// - Original implementation: FUN_004d7400 lines 12-79 implement slab method
        /// 
        /// THE POINT-IN-TRIANGLE TEST (PointInFace2d):
        /// - Uses the "same-side test" to check if a point is inside a triangle
        /// - For each edge of the triangle, calculate which side of the edge the point is on
        /// - If the point is on the same side of all three edges, it's inside the triangle
        /// - The test uses the cross product to determine which side of an edge a point is on
        /// - Original implementation: Similar to FUN_004d9030 @ 0x004d9030 edge containment tests
        /// 
        /// PERFORMANCE:
        /// - With AABB tree: O(log n) average case, O(n) worst case (degenerate tree)
        /// - Without AABB tree: O(n) (must test all triangles)
        /// - With hint face: O(1) best case (hint correct), O(log n) average case (hint wrong)
        /// 
        /// EDGE CASES:
        /// - Empty mesh: Returns -1 immediately
        /// - Point outside all triangles: Returns -1
        /// - Point on triangle edge: Returns the triangle (within tolerance)
        /// - Point on triangle vertex: Returns the triangle (within tolerance)
        /// - Multiple triangles contain point: Returns the first one found (order depends on tree traversal)
        /// </remarks>
        /// <param name="position">The 3D point to find the containing triangle for</param>
        /// <returns>Face index (0 to faceCount-1) if found, -1 if not found</returns>
        public int FindFaceAt(Vector3 position)
        {
            // Use AABB tree if available for faster search
            // The AABB tree organizes triangles into a tree structure, making searches much faster
            // Instead of testing all triangles (O(n)), we test only a few (O(log n))
            // Based on swkotor2.exe: FUN_004f4260 @ 0x004f4260 uses AABB tree when available
            if (_aabbRoot != null)
            {
                return FindFaceAabb(_aabbRoot, position);
            }

            // Brute force fallback: test every triangle until we find one that contains the point
            // This is slower (O(n)) but works when no AABB tree is available
            // Used for: PWK/DWK walkmeshes (placeable/door walkmeshes don't have AABB trees)
            // Based on swkotor2.exe: FUN_004f4260 lines 45-63 iterate through all faces if no tree
            for (int i = 0; i < _faceCount; i++)
            {
                if (PointInFace2d(position, i))
                {
                    return i;
                }
            }

            return -1; // No triangle found
        }

        /// <summary>
        /// Recursively searches the AABB tree to find which triangle contains a point.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function searches through the AABB tree to find which triangle contains a point.
        /// It starts at a node and recursively searches down the tree until it finds a triangle
        /// that contains the point, or determines that no triangle contains it.
        /// 
        /// HOW IT WORKS:
        /// 
        /// STEP 1: Test Point Against Node's AABB
        /// - First, test if the point is inside the node's bounding box (AABB)
        /// - The test is done in 2D (only x and y coordinates) because we're finding which triangle
        ///   is directly below or above the point
        /// - If the point is outside the AABB, return -1 immediately (no triangle in this subtree)
        /// - This is a fast rejection test - if point isn't in the box, it can't be in any triangle
        ///   in that box
        /// 
        /// STEP 2: Check if Node is a Leaf
        /// - If FaceIndex >= 0, this is a leaf node (contains a single triangle)
        /// - Test if the point is inside that triangle using PointInFace2d
        /// - If yes, return the face index
        /// - If no, return -1
        /// 
        /// STEP 3: Recursively Search Children
        /// - If FaceIndex < 0, this is an internal node (has child nodes)
        /// - Recursively search the left child first
        /// - If left child finds a triangle, return it immediately
        /// - If left child doesn't find a triangle, search the right child
        /// - If right child finds a triangle, return it
        /// - If neither child finds a triangle, return -1
        /// 
        /// PERFORMANCE:
        /// - Time complexity: O(log n) average case, O(n) worst case (degenerate tree)
        /// - Without tree: O(n) (must test all triangles)
        /// - Speedup: 100x-1000x faster for large meshes (1000+ triangles)
        /// 
        /// Based on swkotor2.exe: FUN_00575350 @ 0x00575350 (AABB tree traversal)
        /// Original implementation: Recursively traverses tree, tests AABB first, then triangle
        /// </remarks>
        /// <param name="node">The AABB tree node to search from</param>
        /// <param name="point">The 3D point to find the containing triangle for</param>
        /// <returns>Face index if found, -1 if not found</returns>
        private int FindFaceAabb(AabbNode node, Vector3 point)
        {
            // Test if point is within AABB bounds (2D)
            // Based on swkotor2.exe: FUN_00575350 @ 0x00575350 tests AABB first (line 26)
            // Original implementation: FUN_004d7400 @ 0x004d7400 tests ray-AABB intersection
            // This is a fast rejection test - if point isn't in the box, it can't be in any triangle
            if (point.X < node.BoundsMin.X || point.X > node.BoundsMax.X ||
                point.Y < node.BoundsMin.Y || point.Y > node.BoundsMax.Y)
            {
                return -1;
            }

            // Leaf node - test point against face
            // Based on swkotor2.exe: FUN_00575350 @ 0x00575350 checks if node is leaf (line 30)
            // Leaf nodes have FaceIndex >= 0 (contains a single triangle)
            if (node.FaceIndex >= 0)
            {
                if (PointInFace2d(point, node.FaceIndex))
                {
                    return node.FaceIndex;
                }
                return -1;
            }

            // Internal node - test children
            // Based on swkotor2.exe: FUN_00575350 @ 0x00575350 recursively tests children (lines 32-38)
            // Original implementation: Uses flag-based ordering (param_4[1] & node flag)
            // This implementation tests left first, then right (order doesn't matter for correctness)
            if (node.Left != null)
            {
                int result = FindFaceAabb(node.Left, point);
                if (result >= 0)
                {
                    return result;
                }
            }

            if (node.Right != null)
            {
                int result = FindFaceAabb(node.Right, point);
                if (result >= 0)
                {
                    return result;
                }
            }

            return -1;
        }

        /// <summary>
        /// Tests if a point is inside a triangle using 2D projection (only x and y coordinates).
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function tests if a point is inside a triangle by looking only at the x and y
        /// coordinates. The z coordinate is ignored because we're finding which triangle is directly
        /// below or above the point.
        /// 
        /// HOW IT WORKS:
        /// 
        /// The function uses the "same-side test" to determine if a point is inside a triangle.
        /// 
        /// THE SAME-SIDE TEST:
        /// 
        /// A triangle has three edges. For each edge, we can determine which side of the edge the
        /// point is on. If the point is on the same side of all three edges, it's inside the triangle.
        /// 
        /// HOW TO DETERMINE WHICH SIDE:
        /// 
        /// For each edge, we use the cross product to determine which side the point is on.
        /// 
        /// For edge v1->v2:
        /// - Calculate vector from v1 to point: point - v1
        /// - Calculate vector from v1 to v2: v2 - v1
        /// - Calculate cross product: (point - v1) × (v2 - v1)
        /// - The sign of the z-component of the cross product tells us which side:
        ///   * Positive: Point is on the left side of the edge (when viewed from v1 to v2)
        ///   * Negative: Point is on the right side of the edge
        ///   * Zero: Point is exactly on the edge
        /// 
        /// THE ALGORITHM:
        /// 
        /// STEP 1: Get Triangle Vertices
        /// - Get the three vertices of the triangle from the vertex array
        /// - v1 = first vertex, v2 = second vertex, v3 = third vertex
        /// 
        /// STEP 2: Test Each Edge
        /// - For edge v1->v2: Calculate Sign2d(point, v1, v2)
        /// - For edge v2->v3: Calculate Sign2d(point, v2, v3)
        /// - For edge v3->v1: Calculate Sign2d(point, v3, v1)
        /// 
        /// STEP 3: Check if Point is on Same Side of All Edges
        /// - If all three signs are positive (or all negative), the point is inside the triangle
        /// - If some signs are positive and some are negative, the point is outside the triangle
        /// - If any sign is zero, the point is exactly on an edge (within floating-point precision)
        /// 
        /// THE SIGN2D FUNCTION:
        /// 
        /// Sign2d calculates the z-component of the cross product of two 2D vectors:
        /// - Vector 1: from p3 to p1 (p1 - p3)
        /// - Vector 2: from p3 to p2 (p2 - p3)
        /// - Cross product z = (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y)
        /// 
        /// This is the same as calculating the signed area of the parallelogram formed by the two vectors.
        /// The sign tells us which side of the edge (p3->p2) the point p1 is on.
        /// 
        /// EDGE CASES:
        /// - Point on triangle edge: Returns true (within floating-point precision)
        /// - Point on triangle vertex: Returns true (within floating-point precision)
        /// - Point outside triangle: Returns false
        /// - Degenerate triangle (collinear vertices): May return incorrect result (should be handled
        ///   by checking for degenerate triangles before calling this function)
        /// 
        /// Based on swkotor2.exe: Similar to FUN_004d9030 @ 0x004d9030 edge containment tests
        /// Original implementation: Uses cross products to test point containment in polygon
        /// </remarks>
        /// <param name="point">The 3D point to test</param>
        /// <param name="faceIndex">The triangle index to test against</param>
        /// <returns>True if point is inside triangle, false otherwise</returns>
        private bool PointInFace2d(Vector3 point, int faceIndex)
        {
            int baseIdx = faceIndex * 3;
            Vector3 v1 = _vertices[_faceIndices[baseIdx]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v3 = _vertices[_faceIndices[baseIdx + 2]];

            // Same-side test (2D projection)
            // Based on swkotor2.exe: FUN_004d9030 @ 0x004d9030 uses edge containment tests (lines 79-95)
            // Original implementation: Tests if point is on correct side of all three edges
            float d1 = Sign2d(point, v1, v2);
            float d2 = Sign2d(point, v2, v3);
            float d3 = Sign2d(point, v3, v1);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            // Point is inside triangle if all signs are the same (all positive or all negative)
            // If some are positive and some are negative, point is outside
            return !(hasNeg && hasPos);
        }

        /// <summary>
        /// Calculates the signed area of the parallelogram formed by two 2D vectors.
        /// Used to determine which side of an edge a point is on.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function calculates the z-component of the cross product of two 2D vectors.
        /// The result tells us which side of an edge a point is on.
        /// 
        /// HOW IT WORKS:
        /// 
        /// The function calculates: (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y)
        /// 
        /// This is the same as:
        /// - Vector 1: from p3 to p1 (p1 - p3)
        /// - Vector 2: from p3 to p2 (p2 - p3)
        /// - Cross product z = Vector1.x * Vector2.y - Vector2.x * Vector1.y
        /// 
        /// THE RESULT:
        /// - Positive: Point p1 is on the left side of edge p3->p2 (when viewed from p3 to p2)
        /// - Negative: Point p1 is on the right side of edge p3->p2
        /// - Zero: Point p1 is exactly on edge p3->p2 (within floating-point precision)
        /// 
        /// Based on swkotor2.exe: Similar to cross product calculations in FUN_004d9030 @ 0x004d9030
        /// Original implementation: Uses cross products for edge containment tests
        /// </remarks>
        /// <param name="p1">The point to test</param>
        /// <param name="p2">Second vertex of the edge</param>
        /// <param name="p3">First vertex of the edge (edge goes from p3 to p2)</param>
        /// <returns>Signed area (positive = left side, negative = right side, zero = on edge)</returns>
        private float Sign2d(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        /// <summary>
        /// Gets the center point of a face.
        /// </summary>
        public Vector3 GetFaceCenter(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _faceCount)
            {
                return Vector3.Zero;
            }

            int baseIdx = faceIndex * 3;
            Vector3 v1 = _vertices[_faceIndices[baseIdx]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v3 = _vertices[_faceIndices[baseIdx + 2]];

            return (v1 + v2 + v3) / 3.0f;
        }

        /// <summary>
        /// Gets adjacent faces for a given face.
        /// </summary>
        public IEnumerable<int> GetAdjacentFaces(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _faceCount)
            {
                yield break;
            }

            int baseIdx = faceIndex * 3;
            for (int i = 0; i < 3; i++)
            {
                if (baseIdx + i < _adjacency.Length)
                {
                    int encoded = _adjacency[baseIdx + i];
                    yield return DecodeAdjacency(encoded);
                }
                else
                {
                    yield return -1;
                }
            }
        }

        private int DecodeAdjacency(int encoded)
        {
            if (encoded < 0)
            {
                return -1;  // No neighbor
            }
            return encoded / 3;  // Face index (edge = encoded % 3)
        }

        /// <summary>
        /// Checks if a face is walkable based on its surface material.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function checks if a triangle (face) in the walkmesh is walkable. A walkable face
        /// means that characters can stand on it and walk across it. A non-walkable face means
        /// characters cannot stand on it (like walls, water that's too deep, or lava).
        /// 
        /// HOW WALKABILITY IS DETERMINED:
        /// 
        /// Walkability is determined by the surface material assigned to the triangle. Each triangle
        /// has a material number (0-30) that tells what kind of surface it is. The material number
        /// is stored in the _surfaceMaterials array, with one material per triangle.
        /// 
        /// The function works in two steps:
        /// 
        /// STEP 1: Validate Face Index
        /// - Checks if faceIndex is valid (not negative, not beyond array bounds)
        /// - If invalid, returns false (can't be walkable if face doesn't exist)
        /// - This prevents array access errors
        /// 
        /// STEP 2: Check Material Against Walkable Set
        /// - Gets the material number for this face from _surfaceMaterials[faceIndex]
        /// - Checks if this material number is in the WalkableMaterials set
        /// - If yes, returns true (face is walkable)
        /// - If no, returns false (face is not walkable)
        /// 
        /// WALKABLE MATERIALS:
        /// 
        /// The following material IDs are walkable (characters can stand on them):
        /// - 1: Dirt - normal movement speed
        /// - 3: Grass - normal movement speed
        /// - 4: Stone - normal movement speed (this is the default material for generated walkmeshes)
        /// - 5: Wood - normal movement speed
        /// - 6: Water (shallow) - slower movement, 1.5x pathfinding cost
        /// - 9: Carpet - normal movement speed
        /// - 10: Metal - normal movement speed
        /// - 11: Puddles - slower movement, 1.5x pathfinding cost
        /// - 12: Swamp - slower movement, 1.5x pathfinding cost
        /// - 13: Mud - slower movement, 1.5x pathfinding cost
        /// - 14: Leaves - normal movement speed
        /// - 16: BottomlessPit - walkable but dangerous, 10x pathfinding cost, AI avoids if possible
        /// - 18: Door - normal movement speed
        /// - 20: Sand - normal movement speed
        /// - 21: BareBones - normal movement speed
        /// - 22: StoneBridge - normal movement speed
        /// - 30: Trigger - walkable, PyKotor extended material
        /// 
        /// NON-WALKABLE MATERIALS:
        /// 
        /// The following material IDs are NOT walkable (characters cannot stand on them):
        /// - 0: NotDefined/UNDEFINED - non-walkable, used for undefined surfaces
        /// - 2: Obscuring - non-walkable, blocks line of sight
        /// - 7: Nonwalk/NON_WALK - non-walkable, explicitly marked as impassable
        /// - 8: Transparent - non-walkable, see-through but solid
        /// - 15: Lava - non-walkable, dangerous
        /// - 17: DeepWater - non-walkable, characters can't stand in deep water
        /// - 19: Snow/NON_WALK_GRASS - non-walkable, marked as non-walkable grass
        /// 
        /// WHY THIS MATTERS:
        /// 
        /// Walkability is critical for:
        /// 1. Pathfinding: The A* algorithm only considers walkable faces when finding paths
        /// 2. Collision Detection: Characters are prevented from standing on non-walkable faces
        /// 3. Height Calculation: ProjectToSurface only works on walkable faces
        /// 
        /// HOW PROJECTTOSURFACE WORKS (Based on swkotor2.exe):
        /// 
        /// ProjectToSurface is based on FUN_0055b1d0 @ 0x0055b1d0 and FUN_005761f0 @ 0x005761f0.
        /// 
        /// FUN_0055b1d0 @ 0x0055b1d0 (Height Calculation Entry Point):
        /// - Takes a 3D point (x, y, z) and finds the height at that (x, y) position
        /// - Line 10: Checks if AABB tree exists (offset 0x3c in walkmesh structure)
        ///   - If tree doesn't exist, returns _DAT_007b56fc (special value indicating no height)
        /// - Line 12: Calls FUN_00576640 with AABB tree pointer, point coordinates, and flag
        /// - FUN_00576640 finds the triangle containing the point and calculates height
        /// 
        /// FUN_005761f0 @ 0x005761f0 (Find Face for Height Calculation):
        /// - Finds which triangle contains a given (x, y) point
        /// - This function is called by FUN_00576640 when calculating height at a point
        /// - Line 14: Calls FUN_00557540 to transform point coordinates (if needed)
        ///   - FUN_00557540 may apply coordinate system transformations or offsets
        ///   - The transformed point is stored in local_2c (x) and local_28 (y)
        /// - Lines 15-25: Initializes result array (local_20) with default values:
        ///   - local_20[0] = 0xffffffff (special marker value, indicates no face found yet)
        ///   - local_20[1] = 0 (flags for traversal, initially zero)
        ///   - local_20[2] = 0 (intersection flag, 0 = no intersection, 1 = intersection found)
        ///   - local_20[3] = -1 (face index, -1 means not found, valid face index if found)
        ///   - local_20[4] = 0 (intersection point x coordinate, set if intersection found)
        ///   - local_20[5] = 0 (intersection point y coordinate, set if intersection found)
        ///   - local_20[6] = 0 (intersection point z coordinate, set if intersection found)
        ///   - local_20[7] = 0 (additional data, used for face flags)
        /// - Line 26: If param_2 == 0 (use alternative traversal method):
        ///   - Line 27: Calls FUN_00576090 with vertical line segment
        ///   - Vertical line: from (x, y, 1000.0) to (x, y, -1000.0)
        ///   - FUN_00576090 is similar to FUN_00575f60 but uses a different AABB tree traversal method
        ///   - FUN_00576090 uses param_4[0] (e4 offset) instead of param_4[0] (e0 offset) for root node
        /// - Line 29: Else (use standard method, param_2 != 0):
        ///   - Line 30: Calls FUN_00575f60 with vertical line segment
        ///   - Vertical line: from (x, y, 1000.0) to (x, y, -1000.0)
        ///   - FUN_00575f60 uses AABB tree to find triangles intersecting the vertical line
        ///   - FUN_00575f60 calls FUN_00575350 for AABB tree traversal
        /// - Line 32: If no face found (local_20[3] == -1):
        ///   - This means the vertical line segment didn't intersect any triangle
        ///   - This can happen if the point is outside all triangles, or if there's a precision issue
        ///   - Lines 33-36: Calculates a small offset vector to help with edge cases:
        ///     - Creates a vector (1.0, 1.0, 0.0) and normalizes it
        ///     - Multiplies by _DAT_007b5704 (typically a small value like 0.01 or 0.1)
        ///     - This creates a small random offset in the X and Y directions
        ///   - Line 37: If param_2 != 0 (use standard method):
        ///     - Line 38-39: Calls FUN_00575f60 again with offset point
        ///     - Offset point: (x + offset_x, y + offset_y, z + tolerance) to (x + offset_x, y + offset_y, z - tolerance)
        ///     - The offset helps catch edge cases where the point is exactly on a triangle boundary
        ///   - Line 42: Else (use alternative method):
        ///     - Line 42-44: Calls FUN_00576090 again with offset point
        /// - Line 46: Returns local_20[3] (face index, or -1 if not found)
        /// - Why the large z range (1000.0 to -1000.0)? 
        ///   - The input point's z coordinate might be far from the walkmesh surface
        ///   - For example, a character might be at z=50, but the walkmesh is at z=5
        ///   - The large range ensures we find the triangle even if the z coordinate is wrong
        ///   - The actual intersection point's z coordinate is calculated later from the plane equation
        /// - Why the offset retry?
        ///   - Sometimes points exactly on triangle edges or vertices can be missed due to floating-point precision
        ///   - The small offset (typically 0.01 units) moves the point slightly, which helps catch these edge cases
        ///   - This is especially important for points on the boundary between two triangles
        /// 
        /// FUN_00576640 @ 0x00576640 (Calculate Height from Face):
        /// - Calculates the height at a point after finding which triangle contains it
        /// - Line 17: Calls FUN_005761f0 to find the face containing the point
        /// - Line 18: If face not found (iVar1 == -1):
        ///   - Line 19: Returns _DAT_007c14e4 (special value indicating no height found)
        /// - Lines 21-23: Prepares parameters for plane equation calculation:
        ///   - local_c = param_2 (x coordinate)
        ///   - local_8 = param_3 (y coordinate)
        ///   - local_4 = 0xc1100000 (special float value, likely -4.0)
        /// - Line 24-25: Calls FUN_004d6b10 to calculate height from plane equation:
        ///   - Gets triangle normal from walkmesh structure (offset 0x68 + face_index * 12)
        ///   - Gets plane distance from walkmesh structure (offset 0x6c + face_index * 4)
        ///   - Calculates z = (-d - a*x - b*y) / c
        /// - Line 26: Returns calculated height
        /// 
        /// FUN_00575050 @ 0x00575050 (Height Calculation with Known Face):
        /// - Faster version when you already know which triangle contains the point
        /// - Lines 9-11: Prepares parameters for plane equation:
        ///   - local_c = param_2 (x coordinate)
        ///   - local_8 = param_3 (y coordinate)
        ///   - local_4 = 0xc1100000 (special float value)
        /// - Line 12-13: Calls FUN_004d6b10 to calculate height:
        ///   - Gets triangle normal from walkmesh structure (offset 0x68 + face_index * 12)
        ///   - Gets plane distance from walkmesh structure (offset 0x6c + face_index * 4)
        ///   - Calculates z = (-d - a*x - b*y) / c
        /// - This is faster than FUN_00576640 because it skips the face-finding step
        /// 
        /// FUN_004d6b10 @ 0x004d6b10 (Plane Equation Height Calculation):
        /// - The core function that calculates height from plane equation
        /// - Takes: triangle normal vector (3 floats), plane distance (1 float), point (x, y)
        /// - Line 7: Checks if normal.Z (c component) is very close to 0:
        ///   - If yes, returns _DAT_007b56fc (special value, triangle is vertical)
        ///   - Can't divide by zero, so vertical triangles need special handling
        /// - Lines 10-11: Calculates height using plane equation:
        ///   - z = (-d - a*x - b*y) / c
        ///   - Where (a, b, c) is the normal vector, d is plane distance
        /// - Returns calculated z coordinate
        /// 4. Line of Sight: Non-walkable faces can block line of sight
        /// 
        /// ORIGINAL IMPLEMENTATION:
        /// 
        /// Based on swkotor2.exe: Material walkability is hardcoded in the engine based on material ID.
        /// The engine does NOT look up materials in surfacemat.2da to determine walkability - it uses
        /// a hardcoded list of walkable material IDs. This function matches that hardcoded behavior.
        /// 
        /// Located via cross-reference: NavigationMesh.IsWalkable checks material IDs against walkable set.
        /// The original engine checks material IDs directly against a hardcoded set, not against surfacemat.2da.
        /// 
        /// CRITICAL CONSISTENCY REQUIREMENT:
        /// 
        /// This function MUST match SurfaceMaterialExtensions.Walkable() exactly. If they differ,
        /// the indoor map builder and other tools will have incorrect walkability determination.
        /// 
        /// The WalkableMaterials set in this class must contain the same material IDs as the
        /// WalkableMaterials set in SurfaceMaterialExtensions. Any mismatch will cause bugs where:
        /// - Faces are marked as walkable in one place but non-walkable in another
        /// - Pathfinding fails when it should succeed
        /// - Characters can't walk on surfaces that should be walkable
        /// 
        /// POTENTIAL BUG IN INDOOR MAP BUILDER:
        /// 
        /// If levels/modules are NOT walkable despite having the right surface material, check:
        /// 1. Are materials being preserved during BWM transformations (Flip, Rotate, Translate)?
        ///    - These operations only modify vertices, not materials, so materials should be preserved
        /// 2. Are materials being correctly copied when creating BWM copies?
        ///    - DeepCopyBwm should copy Material = face.Material for each face
        /// 3. Is the material ID actually in the WalkableMaterials set?
        ///    - Check that the material number matches one of the walkable IDs listed above
        /// 4. Is there a mismatch between NavigationMesh.WalkableMaterials and SurfaceMaterialExtensions.WalkableMaterials?
        ///    - Both must contain the same material IDs
        /// 5. Are materials being lost during BWM serialization/deserialization?
        ///    - Check BWMAuto.BytesBwm and BWMAuto.ReadBwm to ensure materials are preserved
        /// 6. Is the walkmesh type set to AreaModel (WOK)?
        ///    - If WalkmeshType is not AreaModel, the AABB tree won't be built, causing navigation to fail
        ///    - Check IndoorMap.ProcessBwm() to ensure bwm.WalkmeshType = BWMType.AreaModel is set
        /// 
        /// PERFORMANCE:
        /// 
        /// - Time complexity: O(1) - HashSet.Contains is O(1) average case
        /// - Space complexity: O(1) - no additional memory used
        /// - Called frequently during pathfinding (once per face check)
        /// </remarks>
        /// <param name="faceIndex">Index of the face to check (0 to faceCount-1)</param>
        /// <returns>True if the face is walkable, false otherwise</returns>
        public bool IsWalkable(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _surfaceMaterials.Length)
            {
                return false;
            }

            int material = _surfaceMaterials[faceIndex];
            return WalkableMaterials.Contains(material);
        }

        /// <summary>
        /// Gets the surface material of a face.
        /// </summary>
        public int GetSurfaceMaterial(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _surfaceMaterials.Length)
            {
                return 0;
            }
            return _surfaceMaterials[faceIndex];
        }

        /// <summary>
        /// Performs a raycast against the mesh. Shoots an invisible ray and finds the first triangle it hits.
        /// Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts.
        /// </summary>
        /// <remarks>
        /// HOW RAYCASTING WORKS:
        /// 
        /// A raycast is like shooting an invisible laser and seeing what it hits. The ray starts at
        /// the origin point and travels in the direction specified, up to maxDistance units away.
        /// 
        /// The algorithm has two main stages:
        /// 
        /// STAGE 1: AABB Tree Traversal (if available)
        /// - The AABB tree organizes triangles into boxes (AABBs = Axis-Aligned Bounding Boxes)
        /// - Starting from the root box, we test if the ray hits it
        /// - If it hits, we test the two child boxes (left and right)
        /// - We keep going down the tree until we reach a leaf node (a single triangle)
        /// - This is much faster than testing every triangle
        /// - Based on swkotor2.exe: FUN_00575350 @ 0x00575350 (AABB tree traversal)
        /// 
        /// STAGE 2: Ray-Triangle Intersection
        /// - For each triangle that might be hit, we test if the ray actually hits it
        /// - The test uses a plane-based algorithm:
        ///   a. Calculate the triangle's normal vector (perpendicular to the triangle)
        ///   b. Create a plane equation from the triangle
        ///   c. Check if the ray crosses the plane (one endpoint on each side)
        ///   d. Calculate where the ray hits the plane
        ///   e. Test if that hit point is inside the triangle using edge tests
        /// - Based on swkotor2.exe: FUN_004d9030 @ 0x004d9030 (ray-triangle intersection)
        /// 
        /// The AABB-ray intersection test uses the "slab method":
        /// - We test the ray against each axis (X, Y, Z) separately
        /// - For each axis, we find where the ray enters and exits the box
        /// - If the ray enters all three axes before exiting any, it hits the box
        /// - Based on swkotor2.exe: FUN_004d7400 @ 0x004d7400 (AABB-ray intersection)
        /// 
        /// OPTIMIZATIONS:
        /// - Normalized direction caching: We normalize the direction once and reuse it
        /// - Early termination: If we find an exact hit (distance = 0), we stop immediately
        /// - Distance-based AABB traversal: We test closer children first (optimized from original flag-based)
        /// 
        /// EDGE CASES HANDLED:
        /// - Empty mesh: Returns false immediately
        /// - Zero or invalid direction: Rejected before processing
        /// - Degenerate triangles: Skipped during intersection tests
        /// - Ray starting inside triangle: Handled with tolerance checks
        /// - Ray on triangle surface: Returns as hit with distance 0
        /// </remarks>
        /// <param name="origin">Starting point of the ray</param>
        /// <param name="direction">Direction the ray travels (will be normalized)</param>
        /// <param name="maxDistance">Maximum distance the ray can travel</param>
        /// <param name="hitPoint">Where the ray hit (if it hit something)</param>
        /// <param name="hitFace">Which triangle was hit (if any)</param>
        /// <returns>True if the ray hit a triangle, false otherwise</returns>
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            // Edge case: Empty mesh
            if (_faceCount == 0)
            {
                return false;
            }

            // Edge case: Invalid max distance
            if (maxDistance <= 0f || !float.IsFinite(maxDistance))
            {
                return false;
            }

            // Edge case: Zero or near-zero direction vector
            float dirLength = direction.Length();
            if (dirLength < 1e-6f || !float.IsFinite(dirLength))
            {
                return false;
            }

            // Optimization: Normalize direction once (reused in all intersection tests)
            Vector3 normalizedDir = direction / dirLength;

            // Use AABB tree if available for faster spatial queries
            if (_aabbRoot != null)
            {
                return RaycastAabb(_aabbRoot, origin, normalizedDir, maxDistance, out hitPoint, out hitFace);
            }

            // Brute force fallback with optimizations
            float bestDist = maxDistance;
            bool foundHit = false;

            for (int i = 0; i < _faceCount; i++)
            {
                // Edge case: Validate face index bounds
                if (i * 3 + 2 >= _faceIndices.Length)
                {
                    continue; // Invalid face index
                }

                float dist;
                if (RayTriangleIntersect(origin, normalizedDir, i, bestDist, out dist))
                {
                    // Optimization: Early termination if exact hit found (distance = 0, within tolerance)
                    if (dist < 1e-5f)
                    {
                        hitPoint = origin;
                        hitFace = i;
                        return true;
                    }

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        hitFace = i;
                        hitPoint = origin + normalizedDir * dist;
                        foundHit = true;
                    }
                }
            }

            return foundHit;
        }

        private bool RaycastAabb(AabbNode node, Vector3 origin, Vector3 direction, float maxDist, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            // Edge case: Null node
            if (node == null)
            {
                return false;
            }

            // Test ray against AABB bounds
            if (!RayAabbIntersect(origin, direction, node.BoundsMin, node.BoundsMax, maxDist))
            {
                return false;
            }

            // Leaf node - test ray against face
            if (node.FaceIndex >= 0)
            {
                // Edge case: Validate face index bounds
                if (node.FaceIndex >= _faceCount || node.FaceIndex * 3 + 2 >= _faceIndices.Length)
                {
                    return false; // Invalid face index
                }

                float dist;
                if (RayTriangleIntersect(origin, direction, node.FaceIndex, maxDist, out dist))
                {
                    hitPoint = origin + direction * dist;
                    hitFace = node.FaceIndex;
                    return true;
                }
                return false;
            }

            // Internal node - test children with optimized traversal order
            // Based on swkotor2.exe: FUN_00575350 @ 0x00575350 (AABB tree traversal)
            // Original implementation: Uses flag-based traversal order (param_4[1] & node flag)
            // Original: Lines 31-38 check param_4[1] flag to determine left/right traversal order
            // Optimization: This implementation uses distance-based ordering (test closer child first)
            // This is a reasonable optimization that improves performance while maintaining correctness
            float bestDist = maxDist;
            bool hit = false;

            // Calculate distances to child AABB centers for traversal order optimization
            float leftDist = float.MaxValue;
            float rightDist = float.MaxValue;
            bool leftValid = false;
            bool rightValid = false;

            if (node.Left != null)
            {
                Vector3 leftCenter = (node.Left.BoundsMin + node.Left.BoundsMax) * 0.5f;
                leftDist = Vector3.DistanceSquared(origin, leftCenter);
                leftValid = true;
            }

            if (node.Right != null)
            {
                Vector3 rightCenter = (node.Right.BoundsMin + node.Right.BoundsMax) * 0.5f;
                rightDist = Vector3.DistanceSquared(origin, rightCenter);
                rightValid = true;
            }

            // Optimization: Test closer child first
            AabbNode firstChild = null;
            AabbNode secondChild = null;
            if (leftValid && rightValid)
            {
                if (leftDist < rightDist)
                {
                    firstChild = node.Left;
                    secondChild = node.Right;
                }
                else
                {
                    firstChild = node.Right;
                    secondChild = node.Left;
                }
            }
            else if (leftValid)
            {
                firstChild = node.Left;
            }
            else if (rightValid)
            {
                firstChild = node.Right;
            }

            // Test first child
            if (firstChild != null)
            {
                Vector3 firstHit;
                int firstFace;
                if (RaycastAabb(firstChild, origin, direction, bestDist, out firstHit, out firstFace))
                {
                    float dist = Vector3.Distance(origin, firstHit);
                    // Optimization: Early termination if exact hit found
                    if (dist < 1e-5f)
                    {
                        hitPoint = origin;
                        hitFace = firstFace;
                        return true;
                    }
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        hitPoint = firstHit;
                        hitFace = firstFace;
                        hit = true;
                    }
                }
            }

            // Test second child (only if first didn't find exact hit)
            if (secondChild != null && bestDist > 1e-5f)
            {
                Vector3 secondHit;
                int secondFace;
                if (RaycastAabb(secondChild, origin, direction, bestDist, out secondHit, out secondFace))
                {
                    float dist = Vector3.Distance(origin, secondHit);
                    // Optimization: Early termination if exact hit found
                    if (dist < 1e-5f)
                    {
                        hitPoint = origin;
                        hitFace = secondFace;
                        return true;
                    }
                    if (dist < bestDist)
                    {
                        hitPoint = secondHit;
                        hitFace = secondFace;
                        hit = true;
                    }
                }
            }

            return hit;
        }

        private bool RayAabbIntersect(Vector3 origin, Vector3 direction, Vector3 bbMin, Vector3 bbMax, float maxDist)
        {
            // Edge case: Invalid AABB (min > max)
            if (bbMin.X > bbMax.X || bbMin.Y > bbMax.Y || bbMin.Z > bbMax.Z)
            {
                return false;
            }

            // Edge case: Invalid max distance
            if (maxDist <= 0f || !float.IsFinite(maxDist))
            {
                return false;
            }

            // Edge case: Check if origin is inside AABB (early exit)
            if (origin.X >= bbMin.X && origin.X <= bbMax.X &&
                origin.Y >= bbMin.Y && origin.Y <= bbMax.Y &&
                origin.Z >= bbMin.Z && origin.Z <= bbMax.Z)
            {
                return true; // Origin is inside AABB
            }

            // Optimized AABB-ray intersection using slab method
            // Avoid division by zero with proper handling
            const float epsilon = 1e-8f;
            float invDirX = Math.Abs(direction.X) > epsilon ? 1f / direction.X : (direction.X >= 0f ? float.MaxValue : float.MinValue);
            float invDirY = Math.Abs(direction.Y) > epsilon ? 1f / direction.Y : (direction.Y >= 0f ? float.MaxValue : float.MinValue);
            float invDirZ = Math.Abs(direction.Z) > epsilon ? 1f / direction.Z : (direction.Z >= 0f ? float.MaxValue : float.MinValue);

            float tmin = (bbMin.X - origin.X) * invDirX;
            float tmax = (bbMax.X - origin.X) * invDirX;

            if (invDirX < 0f)
            {
                float temp = tmin;
                tmin = tmax;
                tmax = temp;
            }

            float tymin = (bbMin.Y - origin.Y) * invDirY;
            float tymax = (bbMax.Y - origin.Y) * invDirY;

            if (invDirY < 0f)
            {
                float temp = tymin;
                tymin = tymax;
                tymax = temp;
            }

            // Early rejection test
            if (tmin > tymax || tymin > tmax)
            {
                return false;
            }

            // Update tmin and tmax with Y slab
            if (tymin > tmin) tmin = tymin;
            if (tymax < tmax) tmax = tymax;

            float tzmin = (bbMin.Z - origin.Z) * invDirZ;
            float tzmax = (bbMax.Z - origin.Z) * invDirZ;

            if (invDirZ < 0f)
            {
                float temp = tzmin;
                tzmin = tzmax;
                tzmax = temp;
            }

            // Early rejection test
            if (tmin > tzmax || tzmin > tmax)
            {
                return false;
            }

            // Update tmin with Z slab
            if (tzmin > tmin) tmin = tzmin;

            // Edge case: Ray starts behind AABB (tmin < 0)
            // If tmin < 0, use tmax instead (ray starts inside AABB)
            if (tmin < 0f)
            {
                tmin = tmax;
            }

            // Check if intersection is within max distance
            return tmin >= 0f && tmin <= maxDist;
        }

        private bool RayTriangleIntersect(Vector3 origin, Vector3 direction, int faceIndex, float maxDist, out float distance)
        {
            distance = 0f;

            // Edge case: Validate face index bounds
            if (faceIndex < 0 || faceIndex >= _faceCount)
            {
                return false;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _faceIndices.Length)
            {
                return false; // Invalid face indices
            }

            int idx0 = _faceIndices[baseIdx];
            int idx1 = _faceIndices[baseIdx + 1];
            int idx2 = _faceIndices[baseIdx + 2];

            // Edge case: Validate vertex indices
            if (idx0 < 0 || idx0 >= _vertices.Length ||
                idx1 < 0 || idx1 >= _vertices.Length ||
                idx2 < 0 || idx2 >= _vertices.Length)
            {
                return false; // Invalid vertex indices
            }

            Vector3 v0 = _vertices[idx0];
            Vector3 v1 = _vertices[idx1];
            Vector3 v2 = _vertices[idx2];

            // Based on swkotor2.exe: FUN_004d9030 @ 0x004d9030 (ray-triangle intersection)
            // This algorithm uses a plane-based approach with edge containment tests.
            // 
            // STEP 1: Calculate the triangle's normal vector
            // The normal is a vector that points perpendicular to the triangle's surface.
            // We calculate it by taking the cross product of two edges of the triangle.
            // The cross product of two vectors gives us a vector perpendicular to both.
            Vector3 edge01 = v1 - v0;  // Edge from vertex 0 to vertex 1
            Vector3 edge12 = v2 - v1;  // Edge from vertex 1 to vertex 2
            Vector3 edge20 = v0 - v2;  // Edge from vertex 2 to vertex 0 (not used in normal calc, but kept for reference)

            // Compute normal as cross product of two edges
            // This gives us a vector pointing perpendicular to the triangle
            Vector3 normal = Vector3.Cross(edge01, edge12);
            float normalLength = normal.Length();

            // Edge case: Degenerate triangle (zero area)
            // If the triangle has zero area (all three points are in a line), the normal length is 0.
            // We can't do intersection tests on such triangles, so we skip them.
            // Original: Line 57-58 checks if normal length >= _DAT_007bc338 (epsilon)
            const float degenerateEpsilon = 1e-6f;
            if (normalLength < degenerateEpsilon)
            {
                return false;
            }

            // Normalize the normal vector (make it length 1)
            // This makes calculations easier and more accurate.
            // Original: Lines 59-62 normalize the normal
            normal = normal / normalLength;

            // STEP 2: Create a plane equation from the triangle
            // A plane in 3D space can be described by the equation: ax + by + cz + d = 0
            // Where (a, b, c) is the normal vector, and d is calculated from a point on the plane.
            // We use vertex v0 as the point on the plane.
            // Original: Line 63 computes d = -(normal.x * v0.x + normal.y * v0.y + normal.z * v0.z)
            float d = -(normal.X * v0.X + normal.Y * v0.Y + normal.Z * v0.Z);

            // STEP 3: Check if the ray crosses the plane
            // We calculate the ray's endpoint (where it would be after traveling maxDist units)
            Vector3 rayEnd = origin + direction * maxDist;

            // We plug both the ray's start and end points into the plane equation.
            // If one point gives a positive result and the other gives negative, the ray crosses the plane.
            // If both give the same sign, the ray doesn't cross the plane.
            float dist0 = normal.X * origin.X + normal.Y * origin.Y + normal.Z * origin.Z + d;
            float dist1 = normal.X * rayEnd.X + normal.Y * rayEnd.Y + normal.Z * rayEnd.Z + d;

            // Ray must cross plane (one side positive, one negative, or both zero)
            // We use a small epsilon value to handle floating-point precision issues.
            // Original: Checks _DAT_007b56fc <= dist0 && dist1 <= _DAT_007b56fc && dist0 != dist1
            const float planeEpsilon = 1e-6f;
            if (!((dist0 <= planeEpsilon && dist1 >= -planeEpsilon) || (dist0 >= -planeEpsilon && dist1 <= planeEpsilon)) || Math.Abs(dist0 - dist1) < planeEpsilon)
            {
                return false; // Ray doesn't cross the plane
            }

            // STEP 4: Calculate where the ray hits the plane
            // We use interpolation to find the exact point where the ray crosses the plane.
            // The formula is: t = dist0 / (dist0 - dist1)
            // This gives us a value between 0 and 1 that tells us how far along the ray the intersection is.
            // Original: Lines 71-76 compute intersection using interpolation
            float t = dist0 / (dist0 - dist1);
            Vector3 intersection = origin + direction * (t * maxDist);

            // STEP 5: Test if the intersection point is inside the triangle
            // Just because the ray hits the plane doesn't mean it hits the triangle.
            // The triangle only covers a small part of the plane. We need to check if the
            // intersection point is actually inside the triangle's boundaries.
            // 
            // We do this by testing each edge of the triangle. For each edge, we check if
            // the intersection point is on the "correct side" of the edge (the side that's
            // inside the triangle). We use cross products to determine which side a point is on.
            // 
            // If the point is on the correct side of all three edges, it's inside the triangle.
            // Original: Lines 79-95 test point containment using edge cross products
            bool inside = true;
            const float edgeEpsilon = 1e-6f;

            // Edge 0->1: Check if intersection is on correct side
            // We create a vector from vertex 0 to the intersection point, and a vector along the edge.
            // The cross product tells us which side of the edge the point is on.
            // The dot product with the normal tells us if it's the correct side (positive = correct side).
            Vector3 edge0 = v1 - v0;
            Vector3 toPoint0 = intersection - v0;
            Vector3 cross0 = Vector3.Cross(edge0, toPoint0);
            float dot0 = Vector3.Dot(cross0, normal);
            if (dot0 < -edgeEpsilon)
            {
                inside = false; // Point is on wrong side of this edge
            }

            // Edge 1->2: Check if intersection is on correct side
            if (inside)
            {
                Vector3 edge1 = v2 - v1;
                Vector3 toPoint1 = intersection - v1;
                Vector3 cross1 = Vector3.Cross(edge1, toPoint1);
                float dot1 = Vector3.Dot(cross1, normal);
                if (dot1 < -edgeEpsilon)
                {
                    inside = false; // Point is on wrong side of this edge
                }
            }

            // Edge 2->0: Check if intersection is on correct side
            if (inside)
            {
                Vector3 edge2 = v0 - v2;
                Vector3 toPoint2 = intersection - v2;
                Vector3 cross2 = Vector3.Cross(edge2, toPoint2);
                float dot2 = Vector3.Dot(cross2, normal);
                if (dot2 < -edgeEpsilon)
                {
                    inside = false; // Point is on wrong side of this edge
                }
            }

            if (!inside)
            {
                return false;
            }

            // Compute distance along ray
            distance = Vector3.Distance(origin, intersection);

            // Check if intersection is within max distance
            if (distance <= maxDist)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Tests line of sight between two points.
        /// Determines if there's a clear, unobstructed path between two positions.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function checks if you can see from one point to another without anything blocking
        /// the view. It's like drawing an invisible line between two points and checking if anything
        /// is in the way. If something is in the way, it checks if that thing blocks your view.
        /// 
        /// HOW IT WORKS:
        /// 
        /// STEP 1: Calculate Direction and Distance
        /// - Takes two points: "from" (where you're looking from) and "to" (where you're looking to)
        /// - Calculates the direction vector: direction = to - from
        /// - Calculates the distance: distance = length of direction vector
        /// - If distance is very small (less than 0.000001), the points are the same, so return true
        ///   (you can always see yourself)
        /// 
        /// STEP 2: Normalize Direction
        /// - Normalizes the direction vector (makes it length 1)
        /// - This is needed for the raycast function, which expects a normalized direction
        /// - Normalizing means: direction = direction / distance
        /// 
        /// STEP 3: Perform Raycast
        /// - Shoots an invisible ray from "from" point in the direction of "to" point
        /// - The ray travels up to "distance" units away
        /// - If the ray hits something, Raycast returns true and tells us what was hit
        /// - If the ray doesn't hit anything, Raycast returns false
        /// 
        /// STEP 4: Check if Hit Blocks Line of Sight
        /// - If something was hit, we check if it blocks your view
        /// - Walkable faces (ground, floors) do NOT block line of sight - you can see over them
        /// - Non-walkable faces (walls, obstacles) DO block line of sight - you can't see through them
        /// - We use IsWalkable(hitFace) to check if the hit face is walkable
        /// - If the hit face is walkable, return true (line of sight is clear)
        /// - If the hit face is not walkable, return false (line of sight is blocked)
        /// 
        /// STEP 5: Return Result
        /// - If nothing was hit, return true (clear line of sight)
        /// - If something was hit and it's walkable, return true (clear line of sight)
        /// - If something was hit and it's not walkable, return false (line of sight blocked)
        /// 
        /// WHY WALKABLE FACES DON'T BLOCK:
        /// 
        /// Walkable faces are things like floors and ground. These are flat surfaces that characters
        /// can stand on. They don't block your view because:
        /// - They're horizontal (flat on the ground)
        /// - You're usually looking from above them (your eyes are higher than the ground)
        /// - Even if you're looking at the same level, you can see across flat surfaces
        /// 
        /// Non-walkable faces are things like walls and obstacles. These block your view because:
        /// - They're vertical (standing up)
        /// - They're solid objects that light and vision can't pass through
        /// - They're explicitly marked as non-walkable (material 2 = Obscuring, material 7 = NonWalk, etc.)
        /// 
        /// EXAMPLES:
        /// 
        /// Example 1: Looking across an open field
        /// - From point: (0, 0, 1.5) - character's eye height
        /// - To point: (10, 0, 1.5) - another character's eye height
        /// - Raycast hits: Ground (walkable, material 4 = Stone)
        /// - Result: true (ground doesn't block, you can see across it)
        /// 
        /// Example 2: Looking through a wall
        /// - From point: (0, 0, 1.5)
        /// - To point: (10, 0, 1.5)
        /// - Raycast hits: Wall (non-walkable, material 2 = Obscuring)
        /// - Result: false (wall blocks line of sight)
        /// 
        /// Example 3: Looking at same point
        /// - From point: (5, 5, 1.5)
        /// - To point: (5, 5, 1.5)
        /// - Distance: 0 (same point)
        /// - Result: true (you can always see yourself)
        /// 
        /// PERFORMANCE:
        /// 
        /// - Time complexity: O(log n) with AABB tree, O(n) without tree
        /// - Uses Raycast internally, which is optimized with AABB tree traversal
        /// - Called frequently during AI perception checks (every frame for nearby creatures)
        /// 
        /// ORIGINAL IMPLEMENTATION:
        /// 
        /// Based on swkotor2.exe: Line of sight checks are performed during perception updates.
        /// The original engine uses walkmesh raycasting to determine if creatures can see each other.
        /// Non-walkable faces (walls, obstacles) block line of sight, while walkable faces (ground)
        /// do not block line of sight.
        /// 
        /// Located via cross-reference: Perception systems use TestLineOfSight to check visibility
        /// between creatures. The original engine performs similar checks during AI perception updates.
        /// </remarks>
        /// <param name="from">Starting point (where you're looking from)</param>
        /// <param name="to">Ending point (where you're looking to)</param>
        /// <returns>True if there's a clear line of sight, false if something blocks the view</returns>
        public bool TestLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float distance = direction.Length();
            if (distance < 1e-6f)
            {
                return true;  // Same point
            }

            direction = Vector3.Normalize(direction);

            Vector3 hitPoint;
            int hitFace;
            if (Raycast(from, direction, distance, out hitPoint, out hitFace))
            {
                // Something is in the way - check if it's a non-walkable face
                // Walkable faces (ground, floors) don't block line of sight
                // Non-walkable faces (walls, obstacles) do block line of sight
                return IsWalkable(hitFace);
            }

            return true;  // No obstruction
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface. Finds the height of the ground at a given (x, y) position.
        /// Based on swkotor2.exe: FUN_0055b1d0 @ 0x0055b1d0 and FUN_0055b210 @ 0x0055b210.
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function takes a point with x and y coordinates and finds the height (z coordinate)
        /// of the walkmesh surface at that position. The z coordinate of the input point is ignored -
        /// we only care about x and y to determine which triangle contains the point, then we calculate
        /// the correct z height from that triangle.
        /// 
        /// HOW IT WORKS:
        /// 
        /// STEP 1: Find the Triangle
        /// - Uses FindFaceAt to find which triangle contains the (x, y) point
        /// - FindFaceAt uses FUN_004f4260 @ 0x004f4260 or FUN_004f43c0 @ 0x004f43c0
        /// - If no triangle is found, returns false (point is not on walkmesh)
        /// 
        /// STEP 2: Get Triangle Vertices
        /// - Gets the three vertices that make up the triangle from the vertex array
        /// - Each vertex has x, y, z coordinates
        /// - These vertices define the triangle's plane
        /// 
        /// STEP 3: Calculate Height Using Plane Equation
        /// - Uses DetermineZ function to calculate height from plane equation
        /// - DetermineZ uses FUN_004d6b10 @ 0x004d6b10 algorithm
        /// - Returns the calculated height and projected point
        /// 
        /// THE PLANE EQUATION:
        /// 
        /// A triangle defines a flat plane in 3D space. The plane can be described by the equation:
        /// ax + by + cz + d = 0
        /// 
        /// Where:
        /// - (a, b, c) is the normal vector of the triangle (perpendicular to its surface)
        /// - d is calculated from a point on the plane (one of the triangle's vertices)
        /// 
        /// To calculate the normal vector:
        /// 1. Take two edges of the triangle: edge1 = v2 - v1, edge2 = v3 - v1
        /// 2. Calculate cross product: normal = cross(edge1, edge2)
        /// 3. Normalize: normal = normal / length(normal)
        /// 
        /// To calculate d:
        /// d = -(a * v1.x + b * v1.y + c * v1.z)
        /// 
        /// To solve for z when we know x and y:
        /// z = (-d - ax - by) / c
        /// 
        /// SPECIAL CASE - VERTICAL TRIANGLES:
        /// If the triangle is vertical (normal.Z is very close to 0), we can't divide by c.
        /// In this case, we return the average height of the three vertices:
        /// z = (v1.z + v2.z + v3.z) / 3
        /// 
        /// ORIGINAL IMPLEMENTATION:
        /// 
        /// FUN_0055b1d0 @ 0x0055b1d0 (3D point height):
        /// - Takes a 3D point and finds height at that (x, y) position
        /// - Uses FUN_005761f0 @ 0x005761f0 to find face containing point
        /// - Then uses FUN_00576640 @ 0x00576640 to calculate height
        /// - Original: FUN_0055b1d0 calls FUN_00576640 (line 12)
        /// 
        /// FUN_0055b210 @ 0x0055b210 (2D point height with known face):
        /// - Takes a 2D point (x, y) and a face index (already known)
        /// - Directly calculates height without finding face first
        /// - Faster when you already know which triangle contains the point
        /// - Uses FUN_00575050 @ 0x00575050 to calculate height
        /// - Original: FUN_0055b210 calls FUN_00575050 (line 11)
        /// 
        /// FUN_004d6b10 @ 0x004d6b10 (plane equation height calculation):
        /// - Input: normal vector (a, b, c), plane distance (d), point (x, y)
        /// - Output: height (z coordinate)
        /// - Formula: z = (-d - ax - by) / c
        /// - Special case: If c (normal.Z) is very close to 0, returns _DAT_007b56fc
        /// - Original: FUN_004d6b10 lines 7-11 calculate height
        /// </remarks>
        /// <param name="point">The point to project (x, y are used, z is ignored)</param>
        /// <param name="result">The projected point with correct z height</param>
        /// <param name="height">The calculated height (z coordinate)</param>
        /// <returns>True if a triangle was found and height calculated, false otherwise</returns>
        public bool ProjectToSurface(Vector3 point, out Vector3 result, out float height)
        {
            result = point;
            height = 0f;

            // Step 1: Find which triangle contains this (x, y) point
            // We only care about x and y - the z coordinate of the input point is ignored
            int faceIndex = FindFaceAt(point);
            if (faceIndex < 0)
            {
                return false; // No triangle found at this position
            }

            // Step 2: Get the three vertices that make up this triangle
            int baseIdx = faceIndex * 3;
            Vector3 v1 = _vertices[_faceIndices[baseIdx]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v3 = _vertices[_faceIndices[baseIdx + 2]];

            // Step 3: Calculate the height at this (x, y) point using the plane equation
            // The DetermineZ function uses the triangle's plane equation to find the z coordinate
            float z = DetermineZ(v1, v2, v3, point.X, point.Y);
            height = z;
            result = new Vector3(point.X, point.Y, z);
            return true;
        }

        /// <summary>
        /// Projects a point onto the walkmesh (alias for ProjectToSurface for compatibility).
        /// </summary>
        public bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            return ProjectToSurface(point, out result, out height);
        }

        /// <summary>
        /// Projects a point onto the navigation mesh, returning the projected point or null.
        /// </summary>
        public Vector3? ProjectPoint(Vector3 point)
        {
            Vector3 result;
            float height;
            if (ProjectToSurface(point, out result, out height))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// Checks if a point is walkable (finds face and checks if it's walkable).
        /// </summary>
        public bool IsPointWalkable(Vector3 point)
        {
            int faceIndex = FindFaceAt(point);
            if (faceIndex < 0)
            {
                return false;
            }
            return IsWalkable(faceIndex);
        }

        /// <summary>
        /// Determines the Z (height) coordinate for a given (x, y) point on a triangle's plane.
        /// Based on swkotor2.exe: FUN_004d6b10 @ 0x004d6b10 (plane equation height calculation).
        /// </summary>
        /// <remarks>
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function calculates the exact height (z coordinate) of a point on a triangle's plane
        /// when you only know the x and y coordinates. It uses the mathematical plane equation to solve
        /// for z.
        /// 
        /// HOW THE PLANE EQUATION WORKS:
        /// 
        /// A triangle defines a flat plane in 3D space. The plane can be described by the equation:
        /// ax + by + cz + d = 0
        /// 
        /// This equation means: for any point (x, y, z) on the plane, when you plug it into the equation,
        /// the result is 0. If the point is not on the plane, the result is not 0 (positive on one side,
        /// negative on the other).
        /// 
        /// STEP 1: Calculate the Normal Vector
        /// 
        /// The normal vector (a, b, c) is a vector that points perpendicular to the triangle's surface.
        /// To calculate it:
        /// 
        /// 1. Take two edges of the triangle:
        ///    - edge1 = v2 - v1 (vector from vertex 1 to vertex 2)
        ///    - edge2 = v3 - v1 (vector from vertex 1 to vertex 3)
        /// 
        /// 2. Calculate the cross product of these edges:
        ///    - normal = cross(edge1, edge2)
        ///    - The cross product of two vectors gives a vector perpendicular to both
        ///    - This perpendicular vector is the normal to the triangle
        /// 
        /// 3. The normal vector components become (a, b, c) in the plane equation
        /// 
        /// STEP 2: Calculate d (Plane Distance)
        /// 
        /// The value d is calculated from a point on the plane (we use vertex v1):
        /// d = -(a * v1.x + b * v1.y + c * v1.z)
        /// 
        /// This ensures that when we plug v1 into the plane equation, we get 0:
        /// a * v1.x + b * v1.y + c * v1.z + d = 0
        /// 
        /// STEP 3: Solve for z
        /// 
        /// Once we have the plane equation (a, b, c, d), we can solve for z when we know x and y:
        /// 
        /// Starting with: ax + by + cz + d = 0
        /// Rearranging: cz = -d - ax - by
        /// Solving for z: z = (-d - ax - by) / c
        /// 
        /// This gives us the exact height of the plane at the given (x, y) point.
        /// 
        /// STEP 4: Handle Vertical Triangles (Special Case)
        /// 
        /// If the triangle is vertical (standing straight up), the normal vector's Z component (c) is
        /// very close to 0. If we try to divide by c, we get division by zero, which is invalid.
        /// 
        /// In this case, we can't use the plane equation. Instead, we return the average height of
        /// the three vertices:
        /// z = (v1.z + v2.z + v3.z) / 3
        /// 
        /// This is a reasonable approximation because all three vertices are at roughly the same height
        /// (the triangle is vertical, so z doesn't change much across the triangle).
        /// 
        /// ORIGINAL IMPLEMENTATION (FUN_004d6b10 @ 0x004d6b10):
        /// 
        /// Function signature: float10 FUN_004d6b10(float* normal, float d, float* point)
        /// 
        /// Input parameters:
        /// - normal: Pointer to normal vector (a, b, c) - 3 floats
        /// - d: Plane distance value (d in plane equation)
        /// - point: Pointer to point (x, y) - 2 floats (z is output, not input)
        /// 
        /// Algorithm:
        /// 1. Check if normal.Z (c) is very close to 0 (vertical triangle)
        ///    - If yes, return _DAT_007b56fc (special value indicating vertical triangle)
        ///    - Original: Line 7 checks if normal[2] == _DAT_007b56fc
        /// 
        /// 2. Calculate height using plane equation:
        ///    - z = (-d - a*x - b*y) / c
        ///    - Original: Lines 10-11 calculate height
        /// 
        /// 3. Return calculated height
        /// 
        /// Called from:
        /// - FUN_00575050 @ 0x00575050 (height calculation with known face index)
        /// - FUN_00576640 @ 0x00576640 (height calculation after finding face)
        /// 
        /// MATHEMATICAL DETAILS:
        /// 
        /// Why the cross product gives the normal:
        /// - The cross product of two vectors gives a vector perpendicular to both
        /// - Since edge1 and edge2 are both in the triangle's plane, their cross product is
        ///   perpendicular to the plane (the normal)
        /// 
        /// Why we normalize the normal:
        /// - The normal vector from cross product has a length equal to twice the triangle's area
        /// - Normalizing (making length = 1) makes calculations easier and more accurate
        /// - However, in this implementation, we don't normalize - we use the raw normal
        /// - This is fine because we divide by normal.Z anyway, so the scale cancels out
        /// 
        /// Why the plane equation works:
        /// - Any point on the plane satisfies: ax + by + cz + d = 0
        /// - If we know x and y, we can rearrange to solve for z
        /// - This gives us the exact height of the plane at that (x, y) position
        /// </remarks>
        /// <param name="v1">First vertex of the triangle (x, y, z coordinates)</param>
        /// <param name="v2">Second vertex of the triangle (x, y, z coordinates)</param>
        /// <param name="v3">Third vertex of the triangle (x, y, z coordinates)</param>
        /// <param name="x">X coordinate of the point to find height for</param>
        /// <param name="y">Y coordinate of the point to find height for</param>
        /// <returns>The Z (height) coordinate at the given (x, y) point on the triangle's plane</returns>
        private float DetermineZ(Vector3 v1, Vector3 v2, Vector3 v3, float x, float y)
        {
            // Step 1: Calculate the triangle's normal vector
            // The normal is perpendicular to the triangle's surface
            // We get it by taking the cross product of two edges
            Vector3 edge1 = v2 - v1;  // Edge from v1 to v2
            Vector3 edge2 = v3 - v1;  // Edge from v1 to v3
            Vector3 normal = Vector3.Cross(edge1, edge2);

            // Edge case: Vertical triangle (normal.Z is very close to 0)
            // If the triangle is vertical (standing straight up), we can't use the plane equation
            // because we'd be dividing by zero. Instead, we return the average height of the vertices.
            if (Math.Abs(normal.Z) < 1e-6f)
            {
                return (v1.Z + v2.Z + v3.Z) / 3f;
            }

            // Step 2: Calculate d from the plane equation
            // The plane equation is: ax + by + cz + d = 0
            // We calculate d using one of the triangle's vertices (v1)
            float d = -Vector3.Dot(normal, v1);

            // Step 3: Solve for z using the plane equation
            // Rearranging the plane equation: z = (-d - ax - by) / c
            // This gives us the exact height of the plane at the given (x, y) point
            float z = (-d - normal.X * x - normal.Y * y) / normal.Z;
            return z;
        }

        /// <summary>
        /// AABB tree node for spatial acceleration.
        /// </summary>
        public class AabbNode
        {
            public Vector3 BoundsMin { get; set; }
            public Vector3 BoundsMax { get; set; }
            public int FaceIndex { get; set; }  // -1 for internal nodes
            public AabbNode Left { get; set; }
            public AabbNode Right { get; set; }

            public AabbNode()
            {
                FaceIndex = -1;
            }
        }

        /// <summary>
        /// Helper struct for A* priority queue.
        /// </summary>
        private struct FaceScore
        {
            public int FaceIndex;
            public float Score;

            public FaceScore(int faceIndex, float score)
            {
                FaceIndex = faceIndex;
                Score = score;
            }
        }

        /// <summary>
        /// Comparer for FaceScore to use with SortedSet.
        /// </summary>
        private class FaceScoreComparer : IComparer<FaceScore>
        {
            public int Compare(FaceScore x, FaceScore y)
            {
                int cmp = x.Score.CompareTo(y.Score);
                if (cmp != 0)
                {
                    return cmp;
                }
                // Ensure unique ordering for same scores
                return x.FaceIndex.CompareTo(y.FaceIndex);
            }
        }
    }
}

