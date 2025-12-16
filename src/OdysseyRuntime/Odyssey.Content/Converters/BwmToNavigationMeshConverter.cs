using System;
using System.Collections.Generic;
using System.Numerics;
using AuroraEngine.Common.Formats.BWM;
using Odyssey.Core.Navigation;

namespace Odyssey.Content.Converters
{
    /// <summary>
    /// Converts CSharpKOTOR BWM walkmesh data to Odyssey NavigationMesh.
    /// </summary>
    /// <remarks>
    /// BWM to NavigationMesh Converter:
    /// - Based on swkotor2.exe walkmesh/navigation system
    /// - Located via string references: "nwsareapathfind.cpp" @ 0x007be3ff (pathfinding implementation file reference)
    /// - Pathfinding errors: "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    /// - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
    /// - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
    /// - Original implementation: Converts BWM walkmesh data into navigation mesh for pathfinding
    /// - WOK files (area walkmesh) include AABB trees and walkable adjacency
    /// - PWK/DWK files (placeable/door walkmesh) are collision-only, no AABB tree
    /// - Walkmesh adjacency encoding: faceIndex * 3 + edgeIndex, -1 = no neighbor
    /// - Based on BWM file format documentation in vendor/PyKotor/wiki/BWM-File-Format.md
    /// </remarks>
    public static class BwmToNavigationMeshConverter
    {
        /// <summary>
        /// Converts a CSharpKOTOR BWM to an Odyssey NavigationMesh.
        /// </summary>
        /// <param name="bwm">The source BWM data from CSharpKOTOR</param>
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
            NavigationMesh.AabbNode aabbRoot = null;
            if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
            {
                aabbRoot = BuildAabbTree(bwm, vertices, faces);
            }

            return new NavigationMesh(vertices, faces, adjacency, materials, aabbRoot);
        }

        /// <summary>
        /// Converts a CSharpKOTOR BWM to NavigationMesh with a position offset.
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
        /// </summary>
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
            
            // Note: Cross-mesh adjacency detection is not implemented
            // This would require finding matching edges between meshes and updating adjacency
            // For now, meshes are combined but remain separate islands
            
            // Build a simple AABB tree from combined geometry
            // For a full implementation, we would rebuild the AABB tree properly
            // For now, we'll create a mesh without an AABB tree (it will use brute force)
            NavigationMesh.AabbNode aabbRoot = null;
            
            return new NavigationMesh(
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

        private static NavigationMesh.AabbNode BuildAabbTree(BWM bwm, Vector3[] vertices, int[] faces)
        {
            // Use CSharpKOTOR's AABB generation
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
