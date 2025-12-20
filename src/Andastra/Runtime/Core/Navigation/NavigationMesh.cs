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
    /// WHAT IS A WALKMESH?
    /// 
    /// A walkmesh is a special kind of 3D model that tells the game where characters can walk.
    /// Think of it like an invisible floor map. The game world has beautiful 3D graphics that you see,
    /// but the walkmesh is a simpler version made of triangles that the game uses to figure out where
    /// things can move.
    /// 
    /// The walkmesh is made of triangles (three-sided shapes) that cover the ground. Each triangle
    /// has three points (called vertices) that define its corners. The game uses these triangles
    /// to answer questions like: "Can a character walk here?" and "What is the height of the ground
    /// at this point?"
    /// </para>
    /// 
    /// <para>
    /// WHAT IS A BWM FILE?
    /// 
    /// BWM stands for "BioWare Walkmesh". It is a file format that stores all the walkmesh data.
    /// The file contains:
    /// 
    /// 1. A header that says "BWM V1.0" (this identifies it as a walkmesh file)
    /// 2. A list of all the points (vertices) that make up the triangles
    /// 3. A list of all the triangles (faces), where each triangle is defined by three numbers
    ///    that point to which vertices make up that triangle
    /// 4. Adjacency information that tells which triangles are next to each other
    /// 5. Surface materials that tell what kind of surface each triangle is (dirt, stone, water, etc.)
    /// 6. An AABB tree (a special data structure that makes searching faster)
    /// 
    /// Based on swkotor2.exe: WriteBWMFile @ 0x0055aef0 writes BWM files with "BWM V1.0" signature
    /// (located via "BWM V1.0" @ 0x007c061c). The file format is documented in vendor/PyKotor/wiki/BWM-File-Format.md
    /// </para>
    /// 
    /// <para>
    /// HOW DOES THE DATA WORK?
    /// 
    /// Vertices: These are 3D points (x, y, z coordinates) that define where corners of triangles are.
    /// For example, a vertex might be at position (10.5, 20.3, 5.0) in the game world.
    /// 
    /// Face Indices: Each triangle (face) is defined by three numbers that point to vertices.
    /// For example, if face 0 has indices [5, 12, 8], it means the triangle uses vertices 5, 12, and 8.
    /// 
    /// Adjacency: This tells which triangles share edges. If triangle 0 shares an edge with triangle 5,
    /// the adjacency data stores that information. This is used for pathfinding - the game can move
    /// from one triangle to its neighbors. Adjacency is stored as: adjacency_index = face_index * 3 + edge_index,
    /// where -1 means no neighbor on that edge.
    /// 
    /// Surface Materials: Each triangle has a material number (0-30) that tells what kind of surface it is.
    /// Material 4 is stone (walkable), material 7 is non-walkable, material 6 is shallow water (walkable),
    /// material 17 is deep water (not walkable), etc. The game looks up these materials in a file called
    /// surfacemat.2da to determine if characters can walk on them.
    /// </para>
    /// 
    /// <para>
    /// HOW DOES FACEFINDING WORK?
    /// 
    /// When the game needs to find which triangle contains a specific point (like where a character is standing),
    /// it uses the FindFaceAt function. This is based on swkotor2.exe: FUN_004f4260 @ 0x004f4260.
    /// 
    /// The algorithm works like this:
    /// 1. It takes a point (x, y, z) and tries to find which triangle contains it
    /// 2. It creates a vertical range around the point (z + tolerance to z - tolerance) because the point
    ///    might not be exactly on the triangle surface
    /// 3. If a hint face index is provided (a guess about which triangle might contain the point), it tests
    ///    that triangle first - this is an optimization because characters usually stay on the same triangle
    ///    for multiple frames
    /// 4. It tests each triangle to see if the point is inside it by:
    ///    a. Testing if a vertical line through the point intersects the triangle (using AABB tree if available)
    ///    b. If it finds a match, it returns that triangle's index
    /// 5. If no triangle is found, it returns -1
    /// 
    /// The original implementation uses FUN_0055b300 @ 0x0055b300 to test if a point is inside a triangle's
    /// AABB (Axis-Aligned Bounding Box - a box that contains the triangle), and then FUN_00575f60 @ 0x00575f60
    /// to test the actual triangle intersection using the AABB tree.
    /// </para>
    /// 
    /// <para>
    /// HOW DOES HEIGHT CALCULATION WORK?
    /// 
    /// When the game needs to know the height of the ground at a specific (x, y) position, it uses height
    /// calculation functions. This is based on swkotor2.exe: FUN_0055b1d0 @ 0x0055b1d0 and FUN_0055b210 @ 0x0055b210.
    /// 
    /// The algorithm works like this:
    /// 1. First, it finds which triangle contains the (x, y) point using FindFaceAt
    /// 2. Once it knows which triangle, it uses the triangle's three vertices to calculate the height
    /// 3. It uses a mathematical formula called "plane equation" to figure out the z (height) value
    /// 
    /// The plane equation works like this:
    /// - A triangle defines a flat plane in 3D space
    /// - The equation for a plane is: ax + by + cz + d = 0
    /// - The normal vector (a, b, c) is calculated from the triangle's edges
    /// - Once we know a, b, c, and d, we can solve for z: z = (-d - ax - by) / c
    /// 
    /// The original implementation uses FUN_004d6b10 @ 0x004d6b10 to calculate the height from a plane equation.
    /// If the triangle is vertical (c is very close to 0), it returns the average height of the three vertices.
    /// </para>
    /// 
    /// <para>
    /// HOW DOES RAYCASTING WORK?
    /// 
    /// Raycasting is like shooting an invisible laser and seeing what it hits. The game uses this to check
    /// if there's a clear line of sight between two points, or to find where a ray hits the walkmesh.
    /// This is based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts.
    /// 
    /// The raycast algorithm works in two stages:
    /// 
    /// Stage 1: AABB Tree Traversal (if available)
    /// - The walkmesh has an AABB tree, which is like a tree structure that organizes triangles into boxes
    /// - Starting from the root, it tests if the ray hits the box
    /// - If it hits, it tests the two child boxes (left and right)
    /// - It keeps going down the tree until it reaches a leaf node (a single triangle)
    /// - This is much faster than testing every triangle
    /// - Based on swkotor2.exe: FUN_00575350 @ 0x00575350 (AABB tree traversal)
    /// 
    /// Stage 2: Ray-Triangle Intersection
    /// - For each triangle that might be hit (from the AABB tree, or all triangles if no tree),
    ///   it tests if the ray actually hits the triangle
    /// - The algorithm works like this:
    ///   a. Calculate the triangle's normal vector (a vector pointing perpendicular to the triangle)
    ///   b. Create a plane equation from the triangle
    ///   c. Check if the ray crosses the plane (one endpoint on each side)
    ///   d. Calculate where the ray hits the plane
    ///   e. Test if that hit point is inside the triangle using edge tests
    /// - Based on swkotor2.exe: FUN_004d9030 @ 0x004d9030 (ray-triangle intersection)
    /// 
    /// The AABB-ray intersection test (FUN_004d7400 @ 0x004d7400) uses the "slab method":
    /// - It tests the ray against each axis (X, Y, Z) separately
    /// - For each axis, it finds where the ray enters and exits the box
    /// - If the ray enters all three axes before exiting any, it hits the box
    /// </para>
    /// 
    /// <para>
    /// HOW DOES PATHFINDING WORK?
    /// 
    /// Pathfinding uses the A* algorithm on the walkmesh adjacency graph. The algorithm works like this:
    /// 
    /// 1. Start at the triangle containing the starting point
    /// 2. Use A* to find a path through adjacent triangles to the destination triangle
    /// 3. A* keeps a list of triangles to explore, sorted by how promising they are
    /// 4. For each triangle, it calculates a "score" = distance traveled + estimated distance to goal
    /// 5. It explores the most promising triangles first
    /// 6. When it reaches the destination, it traces back through the path
    /// 
    /// If direct walkmesh pathfinding fails, the game uses grid-based pathfinding for initial/terminal
    /// path segments. Error messages from swkotor2.exe indicate this:
    /// - "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    /// - "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
    /// </para>
    /// 
    /// <para>
    /// OPTIMIZATIONS:
    /// 
    /// 1. AABB Tree: Instead of testing every triangle, the tree organizes them into boxes, making
    ///    searches much faster (from O(n) to O(log n) where n is the number of triangles)
    /// 
    /// 2. Hint Face Index: When finding a face, if you provide a guess (hint), it tests that triangle
    ///    first. This is fast because characters usually stay on the same triangle for multiple frames.
    /// 
    /// 3. Normalized Direction Caching: When raycasting, the direction vector is normalized once
    ///    and reused for all intersection tests, avoiding repeated calculations.
    /// 
    /// 4. Early Termination: If a raycast finds an exact hit (distance = 0), it stops immediately
    ///    instead of checking more triangles.
    /// 
    /// 5. Distance-Based AABB Traversal: When traversing the AABB tree, it tests the closer child
    ///    first. This is an optimization over the original flag-based ordering (FUN_00575350 uses
    ///    param_4[1] flag to determine order), but maintains correctness while improving performance.
    /// </para>
    /// 
    /// <para>
    /// EDGE CASES HANDLED:
    /// 
    /// - Empty mesh: Returns false immediately for all queries
    /// - Zero or invalid direction vectors: Rejected before processing
    /// - Degenerate triangles (zero area): Skipped during intersection tests
    /// - Ray starting inside triangle: Handled with tolerance checks
    /// - Ray on triangle surface: Returns as hit with distance 0
    /// - Invalid face/vertex indices: Validated before use
    /// - Vertical triangles (normal.Z ≈ 0): Uses average height of vertices
    /// - Ray parallel to triangle plane: Rejected (no intersection possible)
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
        /// <param name="vertices">Vertex array for the mesh</param>
        /// <param name="faceIndices">Face indices array (3 indices per face)</param>
        /// <param name="surfaceMaterials">Surface materials array (one per face)</param>
        /// <param name="faceCount">Number of faces in the mesh</param>
        /// <returns>Root node of the AABB tree, or null if no faces</returns>
        /// <remarks>
        /// Based on swkotor2.exe walkmesh AABB tree construction.
        /// Recursively builds a binary tree by splitting faces along the longest axis.
        /// Leaf nodes contain face indices, internal nodes contain bounding boxes.
        /// </remarks>
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
        /// <remarks>
        /// Raycast Implementation:
        /// - Based on swkotor2.exe walkmesh raycast system
        /// - Located via string references: "Raycast" @ navigation mesh functions
        /// - Original implementation: UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks
        /// - Comprehensive edge case handling: empty mesh, zero direction, degenerate triangles, ray on surface
        /// - Optimizations: normalized direction caching, early termination, optimized AABB traversal
        /// - Handles all edge cases: degenerate triangles, ray starting inside triangle, precision issues
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
        /// Finds the face index at a given position using 2D projection.
        /// </summary>
        /// <summary>
        /// Finds the triangle (face) that contains the given 2D position (x, y).
        /// Based on swkotor2.exe: FUN_004f4260 @ 0x004f4260.
        /// </summary>
        /// <remarks>
        /// This function finds which triangle contains a point by:
        /// 1. If an AABB tree exists, it uses that for fast spatial search (O(log n) instead of O(n))
        /// 2. Otherwise, it tests every triangle until it finds one that contains the point
        /// 3. The test is done in 2D (only x and y coordinates) because we're finding which triangle
        ///    is directly below or above the point
        /// 
        /// The original implementation (FUN_004f4260) works like this:
        /// - Takes a position (x, y, z) and creates a vertical range: z + tolerance to z - tolerance
        /// - If a hint face index is provided, tests that triangle first (optimization)
        /// - Tests each triangle using FUN_0055b300 which checks AABB intersection, then triangle intersection
        /// - Returns the first triangle that contains the point, or -1 if none found
        /// </remarks>
        public int FindFaceAt(Vector3 position)
        {
            // Use AABB tree if available for faster search
            // The AABB tree organizes triangles into a tree structure, making searches much faster
            // Instead of testing all triangles (O(n)), we test only a few (O(log n))
            if (_aabbRoot != null)
            {
                return FindFaceAabb(_aabbRoot, position);
            }

            // Brute force fallback: test every triangle until we find one that contains the point
            // This is slower (O(n)) but works when no AABB tree is available
            for (int i = 0; i < _faceCount; i++)
            {
                if (PointInFace2d(position, i))
                {
                    return i;
                }
            }

            return -1; // No triangle found
        }

        private int FindFaceAabb(AabbNode node, Vector3 point)
        {
            // Test if point is within AABB bounds (2D)
            if (point.X < node.BoundsMin.X || point.X > node.BoundsMax.X ||
                point.Y < node.BoundsMin.Y || point.Y > node.BoundsMax.Y)
            {
                return -1;
            }

            // Leaf node - test point against face
            if (node.FaceIndex >= 0)
            {
                if (PointInFace2d(point, node.FaceIndex))
                {
                    return node.FaceIndex;
                }
                return -1;
            }

            // Internal node - test children
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

        private bool PointInFace2d(Vector3 point, int faceIndex)
        {
            int baseIdx = faceIndex * 3;
            Vector3 v1 = _vertices[_faceIndices[baseIdx]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v3 = _vertices[_faceIndices[baseIdx + 2]];

            // Same-side test (2D projection)
            float d1 = Sign2d(point, v1, v2);
            float d2 = Sign2d(point, v2, v3);
            float d3 = Sign2d(point, v3, v1);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

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
        /// </summary>
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
                return IsWalkable(hitFace);
            }

            return true;  // No obstruction
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface. Finds the height of the ground at a given (x, y) position.
        /// Based on swkotor2.exe: FUN_0055b1d0 @ 0x0055b1d0 and FUN_0055b210 @ 0x0055b210.
        /// </summary>
        /// <remarks>
        /// HOW HEIGHT CALCULATION WORKS:
        /// 
        /// When you have an (x, y) position and want to know the height (z coordinate) of the ground
        /// at that point, you need to:
        /// 1. Find which triangle contains that (x, y) point
        /// 2. Use the triangle's three vertices to calculate the height using the plane equation
        /// 
        /// The plane equation works because a triangle defines a flat plane in 3D space.
        /// The equation is: ax + by + cz + d = 0
        /// Where (a, b, c) is the normal vector of the triangle, and d is calculated from a vertex.
        /// 
        /// Once we have the plane equation, we can solve for z:
        /// z = (-d - ax - by) / c
        /// 
        /// This gives us the exact height of the plane at the given (x, y) position.
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
        /// Determines the Z (height) coordinate for a given (x, y) point on a triangle's plane.
        /// Based on swkotor2.exe: FUN_004d6b10 @ 0x004d6b10 (plane equation height calculation).
        /// </summary>
        /// <remarks>
        /// HOW THE PLANE EQUATION WORKS:
        /// 
        /// A triangle defines a flat plane in 3D space. The plane can be described by the equation:
        /// ax + by + cz + d = 0
        /// 
        /// Where:
        /// - (a, b, c) is the normal vector of the triangle (perpendicular to its surface)
        /// - d is calculated from a point on the plane (one of the triangle's vertices)
        /// 
        /// To calculate the normal vector:
        /// 1. Take two edges of the triangle (edge1 = v2 - v1, edge2 = v3 - v1)
        /// 2. Calculate the cross product of these edges
        /// 3. This gives us a vector perpendicular to the triangle
        /// 
        /// To calculate d:
        /// d = -(a * v1.x + b * v1.y + c * v1.z)
        /// 
        /// Once we have the plane equation, we can solve for z when we know x and y:
        /// z = (-d - ax - by) / c
        /// 
        /// Special case: If the triangle is vertical (normal.Z is very close to 0), we can't
        /// divide by c. In this case, we return the average height of the three vertices.
        /// </remarks>
        /// <param name="v1">First vertex of the triangle</param>
        /// <param name="v2">Second vertex of the triangle</param>
        /// <param name="v3">Third vertex of the triangle</param>
        /// <param name="x">X coordinate of the point</param>
        /// <param name="y">Y coordinate of the point</param>
        /// <returns>The Z (height) coordinate at the given (x, y) point</returns>
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

