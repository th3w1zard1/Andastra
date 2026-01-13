using System;
using System.Collections.Generic;
using System.Numerics;
using BioWare.NET.Resource.Formats.BWM;
using Andastra.Runtime.Core.Navigation;

namespace Andastra.Runtime.Content.Converters
{
    /// <summary>
    /// Converts BioWare.NET BWM walkmesh data to Odyssey NavigationMesh.
    /// </summary>
    /// <remarks>
    /// BWM to NavigationMesh Converter:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) walkmesh/navigation system
    /// - WriteBWMFile @ 0x0055aef0 - Writes BWM file with "BWM V1.0" signature (located via "BWM V1.0" @ 0x007c061c)
    /// - ValidateBWMHeader @ 0x006160c0 - Validates BWM file header signature (located via "BWM V1.0" @ 0x007c061c)
    /// - Located via string references: "nwsareapathfind.cpp" @ 0x007be3ff (pathfinding implementation file reference)
    /// - Pathfinding errors: "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    /// - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
    /// - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
    /// - Original implementation: Converts BWM walkmesh data into navigation mesh for pathfinding
    /// - WOK files (area walkmesh) include AABB trees and walkable adjacency
    /// - PWK/DWK files (placeable/door walkmesh) are collision-only, no AABB tree
    /// - Walkmesh adjacency encoding: faceIndex * 3 + edgeIndex, -1 = no neighbor
    /// - BWM file structure: Header (136 bytes) with "BWM V1.0" signature, face data (12 bytes per face), edge data (4 bytes * 3 per face), vertex data (12 bytes per vertex), adjacency data (4 bytes per face)
    /// - Based on BWM file format documentation in vendor/PyKotor/wiki/BWM-File-Format.md
    /// </remarks>
    public static class BwmToNavigationMeshConverter
    {
        /// <summary>
        /// Converts an BioWare.NET BWM to an Odyssey NavigationMesh.
        /// </summary>
        /// <param name="bwm">The source BWM data from BioWare.NET</param>
        /// <returns>A NavigationMesh ready for pathfinding and collision</returns>
        public static NavigationMesh Convert(BWM bwm)
        {
            if (bwm == null)
            {
                throw new ArgumentNullException("bwm");
            }

            if (bwm.Faces.Count == 0)
            {
                // Empty walkmesh
                return new NavigationMesh(
                    new Vector3[0],
                    new int[0],
                    new int[0],
                    new int[0],
                    null);
            }

            // Extract vertices and build face indices
            var vertexList = new List<Vector3>();
            var vertexMap = new Dictionary<string, int>();
            var faceIndices = new List<int>();
            var surfaceMaterials = new List<int>();

            foreach (BWMFace face in bwm.Faces)
            {
                // Get or add each vertex
                // Vertices are deduplicated - if the same vertex position appears in multiple faces,
                // we reuse the same vertex index. This saves memory and ensures proper adjacency.
                int v1Idx = GetOrAddVertex(vertexList, vertexMap, new Vector3(face.V1.X, face.V1.Y, face.V1.Z));
                int v2Idx = GetOrAddVertex(vertexList, vertexMap, new Vector3(face.V2.X, face.V2.Y, face.V2.Z));
                int v3Idx = GetOrAddVertex(vertexList, vertexMap, new Vector3(face.V3.X, face.V3.Y, face.V3.Z));

                // Add face indices (3 indices per triangle)
                faceIndices.Add(v1Idx);
                faceIndices.Add(v2Idx);
                faceIndices.Add(v3Idx);

                // CRITICAL: Convert surface material - MUST be preserved for walkability!
                // The material determines if the face is walkable. If this is not preserved,
                // faces that should be walkable will become non-walkable, causing the bug where
                // "levels/modules are NOT walkable despite having the right surface material."
                // 
                // Material is cast to int because NavigationMesh uses int[] for materials,
                // while BWM uses SurfaceMaterial enum. The enum values match the material IDs (0-30).
                surfaceMaterials.Add((int)face.Material);
            }

            Vector3[] vertices = vertexList.ToArray();
            int[] faces = faceIndices.ToArray();
            int[] materials = surfaceMaterials.ToArray();

            // Compute adjacency from BWM adjacency data
            int[] adjacency = ComputeAdjacency(bwm);

            // Build AABB tree for area walkmeshes
            NavigationMesh.AabbNode aabbRoot = null;
            if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
            {
                aabbRoot = BuildAabbTree(bwm, vertices, faces);
            }

            return new NavigationMesh(vertices, faces, adjacency, materials, aabbRoot);
        }

        /// <summary>
        /// Converts an BioWare.NET BWM to NavigationMesh with a position offset.
        /// Used when placing room walkmeshes in the world.
        /// </summary>
        public static NavigationMesh ConvertWithOffset(BWM bwm, Vector3 offset)
        {
            if (bwm == null)
            {
                throw new ArgumentNullException("bwm");
            }

            if (bwm.Faces.Count == 0)
            {
                return new NavigationMesh(
                    new Vector3[0],
                    new int[0],
                    new int[0],
                    new int[0],
                    null);
            }

            var vertexList = new List<Vector3>();
            var vertexMap = new Dictionary<string, int>();
            var faceIndices = new List<int>();
            var surfaceMaterials = new List<int>();

            foreach (BWMFace face in bwm.Faces)
            {
                // Apply offset when converting vertices
                // This is used when placing room walkmeshes in the world - the offset is the room's position.
                // The vertices are translated by the offset to position them correctly in world space.
                int v1Idx = GetOrAddVertexWithOffset(vertexList, vertexMap, new Vector3(face.V1.X, face.V1.Y, face.V1.Z), offset);
                int v2Idx = GetOrAddVertexWithOffset(vertexList, vertexMap, new Vector3(face.V2.X, face.V2.Y, face.V2.Z), offset);
                int v3Idx = GetOrAddVertexWithOffset(vertexList, vertexMap, new Vector3(face.V3.X, face.V3.Y, face.V3.Z), offset);

                faceIndices.Add(v1Idx);
                faceIndices.Add(v2Idx);
                faceIndices.Add(v3Idx);

                // CRITICAL: Convert surface material - MUST be preserved even when applying offset!
                // The offset only affects vertex positions, NOT materials. Materials must remain unchanged
                // to preserve walkability. If materials are lost or changed, faces become non-walkable.
                surfaceMaterials.Add((int)face.Material);
            }

            Vector3[] vertices = vertexList.ToArray();
            int[] faces = faceIndices.ToArray();
            int[] materials = surfaceMaterials.ToArray();
            int[] adjacency = ComputeAdjacency(bwm);

            NavigationMesh.AabbNode aabbRoot = null;
            if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
            {
                aabbRoot = BuildAabbTree(bwm, vertices, faces);
            }

            return new NavigationMesh(vertices, faces, adjacency, materials, aabbRoot);
        }

        /// <summary>
        /// Merges multiple NavigationMesh instances into a single mesh.
        /// Used to combine room walkmeshes for a complete area.
        /// 
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// This function takes multiple navigation meshes (each representing a room or area) and combines
        /// them into one big navigation mesh. This is needed because a game module is made up of multiple
        /// rooms, and each room has its own walkmesh. To do pathfinding across the entire module, we need
        /// to combine all the room walkmeshes into one.
        /// 
        /// HOW IT WORKS:
        /// 
        /// STEP 1: Combine Vertices
        /// - Takes all vertices from all meshes and puts them into one big list
        /// - Each mesh's vertices are added in order
        /// - We keep track of how many vertices we've added so far (vertexOffset)
        /// 
        /// STEP 2: Reindex Face Indices
        /// - Each face has three vertex indices pointing to vertices in that mesh's vertex array
        /// - When we combine meshes, the vertex indices need to change because vertices are now in
        ///   a different array
        /// - For each face index, we add the vertexOffset to it: newIndex = oldIndex + vertexOffset
        /// - This makes the face point to the correct vertex in the combined array
        /// 
        /// STEP 3: Preserve Internal Adjacencies
        /// - Adjacency is encoded as: faceIndex * 3 + edgeIndex
        /// - Each mesh has its own face indices (0, 1, 2, ...)
        /// - When we combine meshes, face indices need to change
        /// - We decode the adjacency: oldFaceIndex = adj / 3, edgeIndex = adj % 3
        /// - We reindex: newFaceIndex = oldFaceIndex + faceOffset
        /// - We re-encode: newAdj = newFaceIndex * 3 + edgeIndex
        /// - This preserves connections between triangles within each mesh
        /// 
        /// STEP 4: Combine Materials
        /// - Simply copies all materials from all meshes into one list
        /// - Materials don't need reindexing because they're per-face, not per-vertex
        /// 
        /// STEP 5: Detect Cross-Mesh Adjacencies
        /// - Finds edges that match between different meshes (e.g., where two rooms connect)
        /// - Links these edges in the adjacency array so pathfinding can cross between meshes
        /// - This is critical for allowing characters to walk from one room to another
        /// 
        /// STEP 6: Build Combined AABB Tree
        /// - Creates a new AABB tree for the combined mesh
        /// - This makes spatial queries (finding faces, raycasting) fast on the combined mesh
        /// 
        /// WHY REINDEXING IS NEEDED:
        /// 
        /// Each mesh has its own arrays with indices starting at 0. When we combine them:
        /// - Mesh 1: vertices [0, 1, 2], faces [0, 1, 2]
        /// - Mesh 2: vertices [0, 1, 2], faces [0, 1, 2]
        /// - Combined: vertices [0, 1, 2, 3, 4, 5], faces [0, 1, 2, 3, 4, 5]
        /// 
        /// Mesh 2's face indices (0, 1, 2) need to become (3, 4, 5) to point to the correct vertices
        /// in the combined array. This is what reindexing does.
        /// 
        /// EDGE CASES HANDLED:
        /// 
        /// - Empty mesh list: Returns empty navigation mesh
        /// - Single mesh: Returns it unchanged (no merging needed)
        /// - Adjacency with -1 (no neighbor): Preserved as -1
        /// - Only walkable faces are connected in cross-mesh adjacencies
        /// </summary>
        /// <param name="meshes">List of navigation meshes to merge</param>
        /// <returns>A single navigation mesh containing all the combined data</returns>
        public static NavigationMesh Merge(IList<NavigationMesh> meshes)
        {
            if (meshes == null || meshes.Count == 0)
            {
                return new NavigationMesh(
                    new Vector3[0],
                    new int[0],
                    new int[0],
                    new int[0],
                    null);
            }

            if (meshes.Count == 1)
            {
                return meshes[0];
            }

            // Combine all vertices from all meshes
            var combinedVertices = new List<Vector3>();
            var combinedFaceIndices = new List<int>();
            var combinedAdjacency = new List<int>();
            var combinedMaterials = new List<int>();

            int vertexOffset = 0;
            int faceOffset = 0;

            foreach (NavigationMesh mesh in meshes)
            {
                // Get mesh data using public accessors
                IReadOnlyList<Vector3> vertices = mesh.Vertices;
                IReadOnlyList<int> faceIndices = mesh.FaceIndices;
                IReadOnlyList<int> adjacency = mesh.Adjacency;
                IReadOnlyList<int> materials = mesh.SurfaceMaterials;

                // Add vertices to combined list
                foreach (Vector3 vertex in vertices)
                {
                    combinedVertices.Add(vertex);
                }

                // Reindex faces to use combined vertex array
                for (int i = 0; i < faceIndices.Count; i++)
                {
                    combinedFaceIndices.Add(faceIndices[i] + vertexOffset);
                }

                // Preserve internal adjacencies (reindex to new face indices)
                for (int i = 0; i < adjacency.Count; i++)
                {
                    int adj = adjacency[i];
                    if (adj >= 0)
                    {
                        // Adjacency is encoded as: faceIndex * 3 + edgeIndex
                        // Reindex faceIndex to new combined face index
                        int oldFaceIndex = adj / 3;
                        int edgeIndex = adj % 3;
                        int newFaceIndex = oldFaceIndex + faceOffset;
                        combinedAdjacency.Add(newFaceIndex * 3 + edgeIndex);
                    }
                    else
                    {
                        // No neighbor - preserve as -1
                        combinedAdjacency.Add(-1);
                    }
                }

                // Add materials
                foreach (int material in materials)
                {
                    combinedMaterials.Add(material);
                }

                // Update offsets for next mesh
                vertexOffset += vertices.Count;
                faceOffset += mesh.FaceCount;
            }

            // Detect and connect cross-mesh adjacencies
            // Find matching edges between different meshes and link them in the adjacency array
            DetectAndConnectCrossMeshAdjacencies(
                combinedVertices,
                combinedFaceIndices,
                combinedAdjacency,
                combinedMaterials);

            // Build AABB tree from combined geometry
            // Uses recursive top-down construction with longest-axis splitting for efficient spatial queries
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) walkmesh AABB tree construction
            Vector3[] combinedVerticesArray = combinedVertices.ToArray();
            int[] combinedFaceIndicesArray = combinedFaceIndices.ToArray();
            int[] combinedMaterialsArray = combinedMaterials.ToArray();
            int combinedFaceCount = combinedFaceIndicesArray.Length / 3;
            NavigationMesh.AabbNode aabbRoot = NavigationMesh.BuildAabbTreeFromFaces(
                combinedVerticesArray,
                combinedFaceIndicesArray,
                combinedMaterialsArray,
                combinedFaceCount);

            return new NavigationMesh(
                combinedVerticesArray,
                combinedFaceIndicesArray,
                combinedAdjacency.ToArray(),
                combinedMaterialsArray,
                aabbRoot);
        }

        /// <summary>
        /// Detects and connects matching edges between different meshes.
        /// Finds edges that share the same vertex positions (within tolerance) and links them in the adjacency array.
        /// Only connects walkable faces to ensure proper pathfinding connectivity.
        /// 
        /// WHAT THIS FUNCTION DOES:
        /// 
        /// When we merge multiple meshes (e.g., multiple room walkmeshes), each mesh has its own internal
        /// adjacencies (connections between triangles within that mesh). However, we also need to connect
        /// triangles that are on the boundaries between different meshes. For example, if two rooms connect
        /// at a doorway, the triangles on the edge of room 1 need to be connected to the triangles on the
        /// edge of room 2, so pathfinding can find a path from one room to the other.
        /// 
        /// HOW IT WORKS:
        /// 
        /// STEP 1: Build Edge-to-Face Mapping
        /// - For each triangle, get its three edges (v0->v1, v1->v2, v2->v0)
        /// - Create an EdgeKey for each edge (order-independent vertex pair with tolerance)
        /// - Store which face and which edge index this is in a dictionary
        /// - Only process walkable faces (non-walkable faces don't need pathfinding connections)
        /// 
        /// STEP 2: Find Matching Edges
        /// - Look through the dictionary for edges that appear in multiple faces
        /// - If an edge appears in exactly two faces from different meshes, they should be connected
        /// - The EdgeKey uses tolerance to handle floating-point precision issues (vertices might be
        ///   slightly different due to rounding errors)
        /// 
        /// STEP 3: Connect Matching Edges
        /// - For each matching edge pair:
        ///   - Get the two faces: face1 and face2
        ///   - Get the edge indices: edge1 and edge2
        ///   - Encode adjacency: face1's edge1 should point to face2's edge2
        ///   - Encode adjacency: face2's edge2 should point to face1's edge1
        ///   - Update the adjacency array with these connections
        /// 
        /// WHY TOLERANCE IS NEEDED:
        /// 
        /// Due to floating-point precision, two vertices that should be the same might have slightly
        /// different coordinates (e.g., 1.0 vs 1.0000001). The tolerance (0.001 units) allows us to
        /// treat vertices as "the same" if they're very close together.
        /// 
        /// WHY ONLY WALKABLE FACES:
        /// 
        /// Non-walkable faces (walls, obstacles) don't need pathfinding connections. Characters can't
        /// walk through them, so there's no point in connecting them. Only walkable faces need to be
        /// connected so pathfinding can find routes between them.
        /// 
        /// EDGE CASES HANDLED:
        /// 
        /// - Null arrays: Returns early
        /// - Empty mesh: Returns early
        /// - Edges that appear in more than 2 faces: Only connects the first two (others are ignored)
        /// - Edges that appear in only 1 face: No connection needed (perimeter edge)
        /// - Floating-point precision: Uses tolerance to match vertices
        /// </summary>
        /// <param name="vertices">Combined vertex array from all meshes</param>
        /// <param name="faceIndices">Combined face indices array</param>
        /// <param name="adjacency">Combined adjacency array (will be modified to add cross-mesh connections)</param>
        /// <param name="materials">Combined surface materials array</param>
        private static void DetectAndConnectCrossMeshAdjacencies(
            List<Vector3> vertices,
            List<int> faceIndices,
            List<int> adjacency,
            List<int> materials)
        {
            if (vertices == null || faceIndices == null || adjacency == null || materials == null)
            {
                return;
            }

            int faceCount = faceIndices.Count / 3;
            if (faceCount == 0)
            {
                return;
            }

            // Tolerance for vertex position matching (accounts for floating-point precision)
            const float vertexTolerance = 0.001f;

            // Build edge-to-face mapping for all faces
            // Key: edge (order-independent vertex pair), Value: list of (faceIndex, edgeIndex) pairs
            var edgeToFaces = new Dictionary<EdgeKey, List<EdgeFaceInfo>>();

            for (int faceIdx = 0; faceIdx < faceCount; faceIdx++)
            {
                int baseIdx = faceIdx * 3;
                int v0 = faceIndices[baseIdx];
                int v1 = faceIndices[baseIdx + 1];
                int v2 = faceIndices[baseIdx + 2];

                // Get vertex positions
                Vector3 p0 = vertices[v0];
                Vector3 p1 = vertices[v1];
                Vector3 p2 = vertices[v2];

                // Check if face is walkable (only connect walkable faces)
                bool isWalkable = IsFaceWalkable(materials, faceIdx);

                // Edge 0: v0 -> v1
                var edge0 = new EdgeKey(p0, p1, vertexTolerance);
                AddEdgeToMap(edgeToFaces, edge0, faceIdx, 0, isWalkable);

                // Edge 1: v1 -> v2
                var edge1 = new EdgeKey(p1, p2, vertexTolerance);
                AddEdgeToMap(edgeToFaces, edge1, faceIdx, 1, isWalkable);

                // Edge 2: v2 -> v0
                var edge2 = new EdgeKey(p2, p0, vertexTolerance);
                AddEdgeToMap(edgeToFaces, edge2, faceIdx, 2, isWalkable);
            }

            // Find edges that appear in multiple faces (potential cross-mesh connections)
            foreach (var kvp in edgeToFaces)
            {
                List<EdgeFaceInfo> edgeFaces = kvp.Value;

                // Need at least 2 faces to form a connection
                if (edgeFaces.Count < 2)
                {
                    continue;
                }

                // Find pairs of walkable faces that should be connected
                for (int i = 0; i < edgeFaces.Count; i++)
                {
                    EdgeFaceInfo face1 = edgeFaces[i];
                    if (!face1.IsWalkable)
                    {
                        continue;
                    }

                    // Check if this edge already has an adjacency
                    int face1AdjIdx = face1.FaceIndex * 3 + face1.EdgeIndex;
                    if (face1AdjIdx < adjacency.Count && adjacency[face1AdjIdx] >= 0)
                    {
                        // Already has a neighbor - skip
                        continue;
                    }

                    // Find the best matching walkable face
                    for (int j = i + 1; j < edgeFaces.Count; j++)
                    {
                        EdgeFaceInfo face2 = edgeFaces[j];
                        if (!face2.IsWalkable)
                        {
                            continue;
                        }

                        // Check if face2's edge already has an adjacency
                        int face2AdjIdx = face2.FaceIndex * 3 + face2.EdgeIndex;
                        if (face2AdjIdx < adjacency.Count && adjacency[face2AdjIdx] >= 0)
                        {
                            // Already has a neighbor - skip
                            continue;
                        }

                        // Connect the two faces bidirectionally
                        // Face1's edge -> Face2's edge
                        if (face1AdjIdx < adjacency.Count)
                        {
                            adjacency[face1AdjIdx] = face2.FaceIndex * 3 + face2.EdgeIndex;
                        }

                        // Face2's edge -> Face1's edge
                        if (face2AdjIdx < adjacency.Count)
                        {
                            adjacency[face2AdjIdx] = face1.FaceIndex * 3 + face1.EdgeIndex;
                        }

                        // Only connect to the first available neighbor
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Adds an edge to the edge-to-face mapping dictionary.
        /// </summary>
        private static void AddEdgeToMap(
            Dictionary<EdgeKey, List<EdgeFaceInfo>> edgeToFaces,
            EdgeKey edge,
            int faceIndex,
            int edgeIndex,
            bool isWalkable)
        {
            List<EdgeFaceInfo> edgeFaces;
            if (!edgeToFaces.TryGetValue(edge, out edgeFaces))
            {
                edgeFaces = new List<EdgeFaceInfo>();
                edgeToFaces[edge] = edgeFaces;
            }

            edgeFaces.Add(new EdgeFaceInfo(faceIndex, edgeIndex, isWalkable));
        }

        /// <summary>
        /// Checks if a face is walkable based on its surface material.
        /// </summary>
        private static bool IsFaceWalkable(List<int> materials, int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= materials.Count)
            {
                return false;
            }

            int material = materials[faceIndex];

            // Walkable materials (matching NavigationMesh.WalkableMaterials)
            switch (material)
            {
                case 1:  // Dirt
                case 3:  // Grass
                case 4:  // Stone
                case 5:  // Wood
                case 6:  // Water (shallow)
                case 9:  // Carpet
                case 10: // Metal
                case 11: // Puddles
                case 12: // Swamp
                case 13: // Mud
                case 14: // Leaves
                case 16: // BottomlessPit (walkable but dangerous)
                case 18: // Door
                case 20: // Sand
                case 21: // BareBones
                case 22: // StoneBridge
                case 30: // Trigger (PyKotor extended)
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Edge key for cross-mesh adjacency detection.
        /// Uses order-independent vertex pair with tolerance for floating-point comparison.
        /// </summary>
        private struct EdgeKey : IEquatable<EdgeKey>
        {
            public readonly Vector3 V0;
            public readonly Vector3 V1;
            public readonly float Tolerance;

            public EdgeKey(Vector3 v0, Vector3 v1, float tolerance)
            {
                Tolerance = tolerance;

                // Order vertices consistently (min/max) for order-independent comparison
                if (CompareVertices(v0, v1) < 0)
                {
                    V0 = v0;
                    V1 = v1;
                }
                else
                {
                    V0 = v1;
                    V1 = v0;
                }
            }

            public bool Equals(EdgeKey other)
            {
                // Compare with tolerance
                return Vector3DistanceSquared(V0, other.V0) <= Tolerance * Tolerance &&
                       Vector3DistanceSquared(V1, other.V1) <= Tolerance * Tolerance;
            }

            public override bool Equals(object obj)
            {
                return obj is EdgeKey && Equals((EdgeKey)obj);
            }

            public override int GetHashCode()
            {
                // Hash based on quantized positions (rounded to tolerance)
                int x0 = (int)(V0.X / Tolerance);
                int y0 = (int)(V0.Y / Tolerance);
                int z0 = (int)(V0.Z / Tolerance);
                int x1 = (int)(V1.X / Tolerance);
                int y1 = (int)(V1.Y / Tolerance);
                int z1 = (int)(V1.Z / Tolerance);

                // Combine hashes
                int hash = x0;
                hash = (hash * 397) ^ y0;
                hash = (hash * 397) ^ z0;
                hash = (hash * 397) ^ x1;
                hash = (hash * 397) ^ y1;
                hash = (hash * 397) ^ z1;
                return hash;
            }

            private static int CompareVertices(Vector3 a, Vector3 b)
            {
                // Compare by X, then Y, then Z
                int cmp = a.X.CompareTo(b.X);
                if (cmp != 0) return cmp;
                cmp = a.Y.CompareTo(b.Y);
                if (cmp != 0) return cmp;
                return a.Z.CompareTo(b.Z);
            }

            private static float Vector3DistanceSquared(Vector3 a, Vector3 b)
            {
                float dx = a.X - b.X;
                float dy = a.Y - b.Y;
                float dz = a.Z - b.Z;
                return dx * dx + dy * dy + dz * dz;
            }
        }

        /// <summary>
        /// Information about a face and edge for cross-mesh adjacency detection.
        /// </summary>
        private struct EdgeFaceInfo
        {
            public int FaceIndex;
            public int EdgeIndex;
            public bool IsWalkable;

            public EdgeFaceInfo(int faceIndex, int edgeIndex, bool isWalkable)
            {
                FaceIndex = faceIndex;
                EdgeIndex = edgeIndex;
                IsWalkable = isWalkable;
            }
        }

        private static int GetOrAddVertex(
            List<Vector3> vertices,
            Dictionary<string, int> vertexMap,
            Vector3 v)
        {
            string key = string.Format("{0:F6},{1:F6},{2:F6}", v.X, v.Y, v.Z);
            int index;
            if (vertexMap.TryGetValue(key, out index))
            {
                return index;
            }

            index = vertices.Count;
            vertices.Add(v);
            vertexMap[key] = index;
            return index;
        }

        private static int GetOrAddVertexWithOffset(
            List<Vector3> vertices,
            Dictionary<string, int> vertexMap,
            Vector3 v,
            Vector3 offset)
        {
            float x = v.X + offset.X;
            float y = v.Y + offset.Y;
            float z = v.Z + offset.Z;

            string key = string.Format("{0:F6},{1:F6},{2:F6}", x, y, z);
            int index;
            if (vertexMap.TryGetValue(key, out index))
            {
                return index;
            }

            index = vertices.Count;
            vertices.Add(new Vector3(x, y, z));
            vertexMap[key] = index;
            return index;
        }

        private static int[] ComputeAdjacency(BWM bwm)
        {
            int faceCount = bwm.Faces.Count;
            int[] adjacency = new int[faceCount * 3];

            // Initialize to -1 (no neighbor)
            for (int i = 0; i < adjacency.Length; i++)
            {
                adjacency[i] = -1;
            }

            // Get walkable faces for adjacency computation
            List<BWMFace> walkable = bwm.WalkableFaces();
            if (walkable.Count == 0)
            {
                return adjacency;
            }

            // Build a map from face reference to index
            var faceToIndex = new Dictionary<BWMFace, int>();
            for (int i = 0; i < bwm.Faces.Count; i++)
            {
                faceToIndex[bwm.Faces[i]] = i;
            }

            // Compute adjacencies for each walkable face
            foreach (BWMFace face in walkable)
            {
                int faceIdx;
                if (!faceToIndex.TryGetValue(face, out faceIdx))
                {
                    continue;
                }

                Tuple<BWMAdjacency, BWMAdjacency, BWMAdjacency> adj = bwm.Adjacencies(face);

                // Edge 0 adjacency
                if (adj.Item1 != null && faceToIndex.ContainsKey(adj.Item1.Face))
                {
                    int neighborIdx = faceToIndex[adj.Item1.Face];
                    int neighborEdge = adj.Item1.Edge;
                    adjacency[faceIdx * 3 + 0] = neighborIdx * 3 + neighborEdge;
                }

                // Edge 1 adjacency
                if (adj.Item2 != null && faceToIndex.ContainsKey(adj.Item2.Face))
                {
                    int neighborIdx = faceToIndex[adj.Item2.Face];
                    int neighborEdge = adj.Item2.Edge;
                    adjacency[faceIdx * 3 + 1] = neighborIdx * 3 + neighborEdge;
                }

                // Edge 2 adjacency
                if (adj.Item3 != null && faceToIndex.ContainsKey(adj.Item3.Face))
                {
                    int neighborIdx = faceToIndex[adj.Item3.Face];
                    int neighborEdge = adj.Item3.Edge;
                    adjacency[faceIdx * 3 + 2] = neighborIdx * 3 + neighborEdge;
                }
            }

            return adjacency;
        }

        private static NavigationMesh.AabbNode BuildAabbTree(BWM bwm, Vector3[] vertices, int[] faces)
        {
            // Use BioWare.NET's AABB generation
            List<BWMNodeAABB> aabbs = bwm.Aabbs();
            if (aabbs.Count == 0)
            {
                return null;
            }

            // Build a map from BWMFace to face index
            var faceToIndex = new Dictionary<BWMFace, int>();
            for (int i = 0; i < bwm.Faces.Count; i++)
            {
                faceToIndex[bwm.Faces[i]] = i;
            }

            // Convert AABB nodes
            var nodeMap = new Dictionary<BWMNodeAABB, NavigationMesh.AabbNode>();

            foreach (BWMNodeAABB aabb in aabbs)
            {
                int faceIndex = -1;
                if (aabb.Face != null && faceToIndex.ContainsKey(aabb.Face))
                {
                    faceIndex = faceToIndex[aabb.Face];
                }

                var node = new NavigationMesh.AabbNode
                {
                    BoundsMin = new Vector3(aabb.BbMin.X, aabb.BbMin.Y, aabb.BbMin.Z),
                    BoundsMax = new Vector3(aabb.BbMax.X, aabb.BbMax.Y, aabb.BbMax.Z),
                    FaceIndex = faceIndex
                };
                nodeMap[aabb] = node;
            }

            // Link children
            foreach (BWMNodeAABB aabb in aabbs)
            {
                NavigationMesh.AabbNode node = nodeMap[aabb];
                if (aabb.Left != null && nodeMap.ContainsKey(aabb.Left))
                {
                    node.Left = nodeMap[aabb.Left];
                }
                if (aabb.Right != null && nodeMap.ContainsKey(aabb.Right))
                {
                    node.Right = nodeMap[aabb.Right];
                }
            }

            // Find root (first node is typically the root)
            if (aabbs.Count > 0)
            {
                return nodeMap[aabbs[0]];
            }

            return null;
        }
    }
}
