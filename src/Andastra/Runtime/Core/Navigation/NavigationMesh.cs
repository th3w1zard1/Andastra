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
    /// Navigation/Walkmesh System:
    /// - Based on swkotor2.exe pathfinding/walkmesh system
    /// - WriteBWMFile @ 0x0055aef0 - Writes BWM file with "BWM V1.0" signature (located via "BWM V1.0" @ 0x007c061c)
    /// - ValidateBWMHeader @ 0x006160c0 - Validates BWM file header signature (located via "BWM V1.0" @ 0x007c061c)
    /// - Located via string references: "walkmesh" (pathfinding functions), "nwsareapathfind.cpp" @ 0x007be3ff
    /// - BWM file format: "BWM V1.0" @ 0x007c061c (BWM file signature)
    /// - Error messages:
    ///   - "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   - "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
    ///   - "ERROR: opening a Binary walkmesh file for writeing that already exists (File: %s)" @ 0x007c0630
    /// - Original implementation: BWM (BioWare Walkmesh) files contain triangle mesh with adjacency data
    /// - Based on BWM file format documentation in vendor/PyKotor/wiki/BWM-File-Format.md
    /// - BWM file structure: Header (136 bytes) with "BWM V1.0" signature (8 bytes), vertex data (12 bytes per vertex), face data (12 bytes per face), edge data (4 bytes * 3 per face), adjacency data (4 bytes per face), AABB tree
    /// - Adjacency encoding: adjacency_index = face_index * 3 + edge_index, -1 = no neighbor
    /// - Surface materials determine walkability (0-30 range, lookup via surfacemat.2da)
    /// - Pathfinding uses A* algorithm on walkmesh adjacency graph
    /// - Grid-based pathfinding used for initial/terminal path segments when direct walkmesh path fails
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
        /// TODO: SIMPLIFIED - Full implementation would handle all edge cases and optimizations
        /// </summary>
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
        /// Based on swkotor2.exe: FUN_0054a1f0 @ 0x0054a1f0 - pathfinding around obstacles
        /// Located via string references:
        ///   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
        ///   - Called from FUN_0054be70 @ 0x0054be70 when creature collision is detected
        /// Original implementation:
        ///   - Function signature: `undefined4 FUN_0054a1f0(void *this, float *param_1, float *param_2, float *param_3, float *param_4)`
        ///   - param_1: Current position (float[3])
        ///   - param_2: Destination position (float[3])
        ///   - param_3: Obstacle position (float[3]) - position of blocking creature
        ///   - param_4: Obstacle radius (float) - radius to avoid around obstacle
        ///   - Returns: New path waypoints array, or null if no path found
        ///   - Implementation: Marks faces within obstacle radius as temporarily blocked, then runs A* pathfinding
        ///   - Uses FUN_004f5070 for walkmesh projection and FUN_004d8390 for vector normalization
        ///   - If pathfinding fails, attempts to find alternative routes by expanding obstacle avoidance radius
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
        public int FindFaceAt(Vector3 position)
        {
            // Use AABB tree if available
            if (_aabbRoot != null)
            {
                return FindFaceAabb(_aabbRoot, position);
            }

            // Brute force fallback
            for (int i = 0; i < _faceCount; i++)
            {
                if (PointInFace2d(position, i))
                {
                    return i;
                }
            }

            return -1;
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
        /// Performs a raycast against the mesh.
        /// </summary>
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            if (_aabbRoot != null)
            {
                return RaycastAabb(_aabbRoot, origin, direction, maxDistance, out hitPoint, out hitFace);
            }

            // Brute force fallback
            float bestDist = maxDistance;
            for (int i = 0; i < _faceCount; i++)
            {
                float dist;
                if (RayTriangleIntersect(origin, direction, i, bestDist, out dist))
                {
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        hitFace = i;
                        hitPoint = origin + direction * dist;
                    }
                }
            }

            return hitFace >= 0;
        }

        private bool RaycastAabb(AabbNode node, Vector3 origin, Vector3 direction, float maxDist, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            // Test ray against AABB bounds
            if (!RayAabbIntersect(origin, direction, node.BoundsMin, node.BoundsMax, maxDist))
            {
                return false;
            }

            // Leaf node - test ray against face
            if (node.FaceIndex >= 0)
            {
                float dist;
                if (RayTriangleIntersect(origin, direction, node.FaceIndex, maxDist, out dist))
                {
                    hitPoint = origin + direction * dist;
                    hitFace = node.FaceIndex;
                    return true;
                }
                return false;
            }

            // Internal node - test children
            float bestDist = maxDist;
            bool hit = false;

            if (node.Left != null)
            {
                Vector3 leftHit;
                int leftFace;
                if (RaycastAabb(node.Left, origin, direction, bestDist, out leftHit, out leftFace))
                {
                    float dist = Vector3.Distance(origin, leftHit);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        hitPoint = leftHit;
                        hitFace = leftFace;
                        hit = true;
                    }
                }
            }

            if (node.Right != null)
            {
                Vector3 rightHit;
                int rightFace;
                if (RaycastAabb(node.Right, origin, direction, bestDist, out rightHit, out rightFace))
                {
                    float dist = Vector3.Distance(origin, rightHit);
                    if (dist < bestDist)
                    {
                        hitPoint = rightHit;
                        hitFace = rightFace;
                        hit = true;
                    }
                }
            }

            return hit;
        }

        private bool RayAabbIntersect(Vector3 origin, Vector3 direction, Vector3 bbMin, Vector3 bbMax, float maxDist)
        {
            // Avoid division by zero
            float invDirX = direction.X != 0f ? 1f / direction.X : float.MaxValue;
            float invDirY = direction.Y != 0f ? 1f / direction.Y : float.MaxValue;
            float invDirZ = direction.Z != 0f ? 1f / direction.Z : float.MaxValue;

            float tmin = (bbMin.X - origin.X) * invDirX;
            float tmax = (bbMax.X - origin.X) * invDirX;

            if (invDirX < 0)
            {
                float temp = tmin;
                tmin = tmax;
                tmax = temp;
            }

            float tymin = (bbMin.Y - origin.Y) * invDirY;
            float tymax = (bbMax.Y - origin.Y) * invDirY;

            if (invDirY < 0)
            {
                float temp = tymin;
                tymin = tymax;
                tymax = temp;
            }

            if (tmin > tymax || tymin > tmax)
            {
                return false;
            }

            if (tymin > tmin) tmin = tymin;
            if (tymax < tmax) tmax = tymax;

            float tzmin = (bbMin.Z - origin.Z) * invDirZ;
            float tzmax = (bbMax.Z - origin.Z) * invDirZ;

            if (invDirZ < 0)
            {
                float temp = tzmin;
                tzmin = tzmax;
                tzmax = temp;
            }

            if (tmin > tzmax || tzmin > tmax)
            {
                return false;
            }

            if (tzmin > tmin) tmin = tzmin;

            if (tmin < 0) tmin = tmax;
            return tmin >= 0 && tmin <= maxDist;
        }

        private bool RayTriangleIntersect(Vector3 origin, Vector3 direction, int faceIndex, float maxDist, out float distance)
        {
            distance = 0f;

            int baseIdx = faceIndex * 3;
            Vector3 v0 = _vertices[_faceIndices[baseIdx]];
            Vector3 v1 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 2]];

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;

            Vector3 h = Vector3.Cross(direction, edge2);
            float a = Vector3.Dot(edge1, h);

            // Ray is parallel to triangle
            if (Math.Abs(a) < 1e-6f)
            {
                return false;
            }

            float f = 1f / a;
            Vector3 s = origin - v0;
            float u = f * Vector3.Dot(s, h);

            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(direction, q);

            if (v < 0f || u + v > 1f)
            {
                return false;
            }

            float t = f * Vector3.Dot(edge2, q);

            if (t > 1e-6f && t < maxDist)
            {
                distance = t;
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
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        public bool ProjectToSurface(Vector3 point, out Vector3 result, out float height)
        {
            result = point;
            height = 0f;

            int faceIndex = FindFaceAt(point);
            if (faceIndex < 0)
            {
                return false;
            }

            // Get face vertices
            int baseIdx = faceIndex * 3;
            Vector3 v1 = _vertices[_faceIndices[baseIdx]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v3 = _vertices[_faceIndices[baseIdx + 2]];

            // Calculate height at point using barycentric interpolation
            float z = DetermineZ(v1, v2, v3, point.X, point.Y);
            height = z;
            result = new Vector3(point.X, point.Y, z);
            return true;
        }

        private float DetermineZ(Vector3 v1, Vector3 v2, Vector3 v3, float x, float y)
        {
            // Calculate face normal
            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            Vector3 normal = Vector3.Cross(edge1, edge2);

            // Avoid division by zero for vertical faces
            if (Math.Abs(normal.Z) < 1e-6f)
            {
                return (v1.Z + v2.Z + v3.Z) / 3f;
            }

            // Plane equation: ax + by + cz + d = 0
            // Solve for z: z = (-d - ax - by) / c
            float d = -Vector3.Dot(normal, v1);
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

