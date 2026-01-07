using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Parsing.Formats.BWM;
using Andastra.Runtime.Games.Eclipse;

namespace Andastra.Runtime.Games.Eclipse.Loading
{
    /// <summary>
    /// Converts Andastra.Parsing BWM walkmesh data to EclipseNavigationMesh.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WHAT THIS CONVERTER DOES:
    /// 
    /// This converter takes a BWM (BioWare Walkmesh) file that has been parsed from a game file
    /// and converts it into an EclipseNavigationMesh. The EclipseNavigationMesh is a special type
    /// of navigation mesh used by the Eclipse engine (Dragon Age: Origins, Dragon Age 2) that
    /// supports advanced features like dynamic obstacles, destructible terrain, and multi-level
    /// navigation surfaces.
    /// 
    /// The conversion process:
    /// 1. Extracts all vertices from the BWM faces
    /// 2. Builds face indices (which vertices belong to which triangles)
    /// 3. Converts surface materials (what kind of surface each triangle is)
    /// 4. Computes adjacency (which triangles share edges)
    /// 5. Builds an AABB tree (for fast spatial queries)
    /// 
    /// CRITICAL: The walkmesh type must be AreaModel (WOK) for the AABB tree to be built. If the
    /// walkmesh type is PlaceableOrDoor (PWK/DWK), the AABB tree will not be built, and the
    /// navigation mesh will not work correctly for pathfinding and spatial queries.
    /// </para>
    /// 
    /// <para>
    /// ECLIPSE-SPECIFIC FEATURES:
    /// 
    /// The Eclipse engine has more advanced navigation features than the Odyssey engine:
    /// 
    /// 1. MULTI-LEVEL NAVIGATION:
    ///    - Supports navigation on multiple height levels (ground, platforms, elevated surfaces)
    ///    - Characters can navigate on different floors of a building
    ///    - Pathfinding considers all levels when finding routes
    /// 
    /// 2. DYNAMIC OBSTACLES:
    ///    - Objects can be moved or destroyed at runtime
    ///    - The navigation mesh updates to reflect these changes
    ///    - Pathfinding avoids dynamic obstacles automatically
    /// 
    /// 3. DESTRUCTIBLE TERRAIN:
    ///    - Terrain can be destroyed (walls, floors, etc.)
    ///    - Destroyed terrain becomes non-walkable
    ///    - The navigation mesh updates to reflect destroyed areas
    /// 
    /// 4. PHYSICS-AWARE NAVIGATION:
    ///    - Considers physics objects when pathfinding
    ///    - Avoids collisions with moving objects
    ///    - Integrates with the physics engine for accurate collision detection
    /// </para>
    /// 
    /// <para>
    /// BWM FILE FORMAT:
    /// 
    /// The BWM file format is the same across all BioWare engines (Odyssey, Aurora, Eclipse):
    /// 
    /// - Header: "BWM V1.0" signature (8 bytes) - identifies the file as a BWM
    /// - Walkmesh type: 0 = PWK/DWK (placeable/door), 1 = WOK (area walkmesh)
    /// - Vertices: Array of float3 (x, y, z) positions - all the points that make up triangles
    /// - Faces: Array of uint32 triplets (vertex indices per triangle) - which vertices form triangles
    /// - Materials: Array of uint32 (SurfaceMaterial ID per face) - what kind of surface each triangle is
    /// - Adjacency: Array of int32 triplets (face/edge pairs, -1 = no neighbor) - which triangles share edges
    /// - AABB tree: Spatial acceleration structure for efficient queries - only for WOK files
    /// 
    /// The converter reads this data and converts it into the format needed by EclipseNavigationMesh.
    /// </para>
    /// 
    /// <para>
    /// VERTEX DEDUPLICATION:
    /// 
    /// When converting, the converter deduplicates vertices. This means if multiple triangles share
    /// the same vertex position, only one copy of that vertex is stored. This saves memory and
    /// makes the data structure more efficient.
    /// 
    /// HOW IT WORKS:
    /// - For each face, get its three vertices
    /// - Check if we've seen this vertex position before (using a dictionary)
    /// - If yes, use the existing vertex index
    /// - If no, add the vertex to the list and use the new index
    /// 
    /// This ensures that each unique vertex position is stored only once, even if it's used by
    /// multiple triangles.
    /// </para>
    /// 
    /// <para>
    /// ADJACENCY COMPUTATION:
    /// 
    /// Adjacency tells which triangles share edges. This is critical for pathfinding because the
    /// pathfinding algorithm needs to know which triangles can be reached from the current triangle.
    /// 
    /// HOW IT WORKS:
    /// 1. Get all walkable faces from the BWM
    /// 2. For each face, check each of its three edges
    /// 3. Find other faces that share the same edge (same two vertices)
    /// 4. Store the adjacency information: faceIndex * 3 + edgeIndex
    /// 
    /// The adjacency array has one entry per edge (3 per face). Each entry contains:
    /// - The index of the adjacent face's edge (if there is one)
    /// - -1 if there is no adjacent face (edge is on the perimeter)
    /// </para>
    /// 
    /// <para>
    /// AABB TREE BUILDING:
    /// 
    /// The AABB tree is a spatial acceleration structure that makes it fast to find which triangles
    /// contain a point or which triangles a ray might hit. Without it, we would have to check every
    /// triangle, which is very slow when there are thousands of triangles.
    /// 
    /// HOW IT WORKS:
    /// 1. If the BWM already has an AABB tree, convert it to EclipseNavigationMesh format
    /// 2. If not, build one from the face data
    /// 3. The tree organizes triangles into boxes (AABBs)
    /// 4. Each box contains smaller boxes, which contain even smaller boxes, and so on
    /// 5. At the bottom, each box contains just one triangle
    /// 
    /// CRITICAL: The AABB tree is only built if the walkmesh type is AreaModel (WOK). If the
    /// walkmesh type is PlaceableOrDoor (PWK/DWK), the AABB tree is not built. This is because
    /// placeable/door walkmeshes are small and can be checked directly without needing a tree.
    /// </para>
    /// 
    /// <para>
    /// MERGING MULTIPLE MESHES:
    /// 
    /// The Merge() function combines multiple EclipseNavigationMesh instances into a single mesh.
    /// This is used when building a complete area from multiple room walkmeshes.
    /// 
    /// HOW IT WORKS:
    /// 1. Combine all vertices from all meshes into one array
    /// 2. Reindex faces to use the combined vertex array
    /// 3. Preserve internal adjacencies (reindex to new face indices)
    /// 4. Detect and connect cross-mesh adjacencies (edges shared between different meshes)
    /// 5. Combine materials from all meshes
    /// 6. Build a new AABB tree from the combined geometry
    /// 
    /// CROSS-MESH ADJACENCY DETECTION:
    /// 
    /// When merging meshes, we need to find edges that are shared between different meshes and
    /// link them in the adjacency array. This allows pathfinding to cross room boundaries.
    /// 
    /// HOW IT WORKS:
    /// 1. Build a map of all edges to the faces that contain them
    /// 2. Find edges that appear in multiple faces (potential cross-mesh connections)
    /// 3. For each such edge, check if the faces are from different meshes
    /// 4. If yes, and both faces are walkable, connect them in the adjacency array
    /// 5. Only connect walkable faces to ensure proper pathfinding
    /// </para>
    /// 
    /// <para>
    /// MATERIAL PRESERVATION:
    /// 
    /// CRITICAL: Surface materials must be preserved during conversion. If materials are lost or
    /// changed, faces that should be walkable might become non-walkable, causing the bug where
    /// "levels/modules are NOT walkable despite having the right surface material."
    /// 
    /// The converter explicitly copies the Material property from each BWMFace to the surface
    /// materials array. This ensures that walkability is maintained during conversion.
    /// </para>
    /// 
    /// <para>
    /// Based on BWM file format documentation:
    /// - vendor/PyKotor/wiki/BWM-File-Format.md
    /// </para>
    /// </remarks>
    public static class BwmToEclipseNavigationMeshConverter
    {
        /// <summary>
        /// Converts an Andastra.Parsing BWM to an EclipseNavigationMesh.
        /// </summary>
        /// <param name="bwm">The source BWM data from Andastra.Parsing</param>
        /// <returns>An EclipseNavigationMesh ready for pathfinding and collision</returns>
        public static EclipseNavigationMesh Convert(BWM bwm)
        {
            if (bwm == null)
            {
                throw new ArgumentNullException(nameof(bwm));
            }

            if (bwm.Faces.Count == 0)
            {
                // Empty walkmesh
                return new EclipseNavigationMesh();
            }

            // Extract vertices and build face indices
            var vertexList = new List<Vector3>();
            var vertexMap = new Dictionary<string, int>();
            var faceIndices = new List<int>();
            var surfaceMaterials = new List<int>();

            foreach (BWMFace face in bwm.Faces)
            {
                // Get or add each vertex
                int v1Idx = GetOrAddVertex(vertexList, vertexMap, new Vector3(face.V1.X, face.V1.Y, face.V1.Z));
                int v2Idx = GetOrAddVertex(vertexList, vertexMap, new Vector3(face.V2.X, face.V2.Y, face.V2.Z));
                int v3Idx = GetOrAddVertex(vertexList, vertexMap, new Vector3(face.V3.X, face.V3.Y, face.V3.Z));

                faceIndices.Add(v1Idx);
                faceIndices.Add(v2Idx);
                faceIndices.Add(v3Idx);

                // Convert surface material
                surfaceMaterials.Add((int)face.Material);
            }

            Vector3[] vertices = vertexList.ToArray();
            int[] faces = faceIndices.ToArray();
            int[] materials = surfaceMaterials.ToArray();

            // Compute adjacency from BWM adjacency data
            int[] adjacency = ComputeAdjacency(bwm);

            // Build AABB tree for area walkmeshes
            EclipseNavigationMesh.AabbNode aabbRoot = null;
            if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
            {
                aabbRoot = BuildAabbTree(bwm, vertices, faces);
            }

            return new EclipseNavigationMesh(vertices, faces, adjacency, materials, aabbRoot);
        }

        /// <summary>
        /// Converts an Andastra.Parsing BWM to EclipseNavigationMesh with a position offset.
        /// Used when placing room walkmeshes in the world.
        /// </summary>
        public static EclipseNavigationMesh ConvertWithOffset(BWM bwm, Vector3 offset)
        {
            if (bwm == null)
            {
                throw new ArgumentNullException(nameof(bwm));
            }

            if (bwm.Faces.Count == 0)
            {
                return new EclipseNavigationMesh();
            }

            var vertexList = new List<Vector3>();
            var vertexMap = new Dictionary<string, int>();
            var faceIndices = new List<int>();
            var surfaceMaterials = new List<int>();

            foreach (BWMFace face in bwm.Faces)
            {
                // Apply offset when converting vertices
                int v1Idx = GetOrAddVertexWithOffset(vertexList, vertexMap, new Vector3(face.V1.X, face.V1.Y, face.V1.Z), offset);
                int v2Idx = GetOrAddVertexWithOffset(vertexList, vertexMap, new Vector3(face.V2.X, face.V2.Y, face.V2.Z), offset);
                int v3Idx = GetOrAddVertexWithOffset(vertexList, vertexMap, new Vector3(face.V3.X, face.V3.Y, face.V3.Z), offset);

                faceIndices.Add(v1Idx);
                faceIndices.Add(v2Idx);
                faceIndices.Add(v3Idx);

                surfaceMaterials.Add((int)face.Material);
            }

            Vector3[] vertices = vertexList.ToArray();
            int[] faces = faceIndices.ToArray();
            int[] materials = surfaceMaterials.ToArray();
            int[] adjacency = ComputeAdjacency(bwm);

            EclipseNavigationMesh.AabbNode aabbRoot = null;
            if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
            {
                aabbRoot = BuildAabbTree(bwm, vertices, faces);
            }

            return new EclipseNavigationMesh(vertices, faces, adjacency, materials, aabbRoot);
        }

        /// <summary>
        /// Merges multiple EclipseNavigationMesh instances into a single mesh.
        /// Used to combine room walkmeshes for a complete area.
        /// </summary>
        public static EclipseNavigationMesh Merge(IList<EclipseNavigationMesh> meshes)
        {
            if (meshes == null || meshes.Count == 0)
            {
                return new EclipseNavigationMesh();
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

            foreach (EclipseNavigationMesh mesh in meshes)
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
            EclipseNavigationMesh.AabbNode aabbRoot = null;
            if (combinedVertices.Count > 0 && combinedFaceIndices.Count > 0)
            {
                aabbRoot = BuildAabbTreeFromFaces(
                    combinedVertices.ToArray(),
                    combinedFaceIndices.ToArray(),
                    combinedMaterials.ToArray(),
                    combinedFaceIndices.Count / 3);
            }

            return new EclipseNavigationMesh(
                combinedVertices.ToArray(),
                combinedFaceIndices.ToArray(),
                combinedAdjacency.ToArray(),
                combinedMaterials.ToArray(),
                aabbRoot);
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

        private static EclipseNavigationMesh.AabbNode BuildAabbTree(BWM bwm, Vector3[] vertices, int[] faces)
        {
            // Use Andastra.Parsing's AABB generation
            List<BWMNodeAABB> aabbs = bwm.Aabbs();
            if (aabbs.Count == 0)
            {
                // Build AABB tree from faces if BWM doesn't have one
                return BuildAabbTreeFromFaces(vertices, faces, new int[faces.Length / 3], faces.Length / 3);
            }

            // Build a map from BWMFace to face index
            var faceToIndex = new Dictionary<BWMFace, int>();
            for (int i = 0; i < bwm.Faces.Count; i++)
            {
                faceToIndex[bwm.Faces[i]] = i;
            }

            // Convert AABB nodes
            var nodeMap = new Dictionary<BWMNodeAABB, EclipseNavigationMesh.AabbNode>();

            foreach (BWMNodeAABB aabb in aabbs)
            {
                int faceIndex = -1;
                if (aabb.Face != null && faceToIndex.ContainsKey(aabb.Face))
                {
                    faceIndex = faceToIndex[aabb.Face];
                }

                var node = new EclipseNavigationMesh.AabbNode
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
                EclipseNavigationMesh.AabbNode node = nodeMap[aabb];
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

        /// <summary>
        /// Detects and connects matching edges between different meshes.
        /// Finds edges that share the same vertex positions (within tolerance) and links them in the adjacency array.
        /// Only connects walkable faces to ensure proper pathfinding connectivity.
        /// </summary>
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

        /// <summary>
        /// Builds an AABB tree from face data for spatial acceleration.
        /// Uses recursive top-down construction with longest-axis splitting.
        /// </summary>
        private static EclipseNavigationMesh.AabbNode BuildAabbTreeFromFaces(
            Vector3[] vertices,
            int[] faceIndices,
            int[] surfaceMaterials,
            int faceCount)
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
        private static EclipseNavigationMesh.AabbNode BuildAabbTreeRecursive(
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

            var node = new EclipseNavigationMesh.AabbNode
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
                int baseIdx = faceIdx * 3;
                // Calculate face center
                Vector3 v0 = vertices[faceIndices[baseIdx]];
                Vector3 v1 = vertices[faceIndices[baseIdx + 1]];
                Vector3 v2 = vertices[faceIndices[baseIdx + 2]];
                Vector3 center = (v0 + v1 + v2) / 3.0f;

                float centerValue = GetAxisValue(center, splitAxis);
                if (centerValue < splitValue)
                {
                    leftFaces.Add(faceIdx);
                }
                else
                {
                    rightFaces.Add(faceIdx);
                }
            }

            // Handle degenerate case (all faces on one side)
            if (leftFaces.Count == 0 || rightFaces.Count == 0)
            {
                // Try splitting by median
                faceList.Sort((a, b) =>
                {
                    int baseA = a * 3;
                    int baseB = b * 3;
                    Vector3 centerA = (vertices[faceIndices[baseA]] + vertices[faceIndices[baseA + 1]] + vertices[faceIndices[baseA + 2]]) / 3.0f;
                    Vector3 centerB = (vertices[faceIndices[baseB]] + vertices[faceIndices[baseB + 1]] + vertices[faceIndices[baseB + 2]]) / 3.0f;
                    return GetAxisValue(centerA, splitAxis).CompareTo(GetAxisValue(centerB, splitAxis));
                });

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
    }
}

