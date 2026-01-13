using System;
using System.Collections.Generic;
using System.Numerics;

namespace Andastra.Runtime.Core.Navigation
{
    /// <summary>
    /// Factory for creating NavigationMesh instances from BioWare.NET BWM data.
    /// </summary>
    /// <remarks>
    /// Navigation Mesh Factory:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) walkmesh/navigation system
    /// - Located via string references: "walkmesh" pathfinding functions, "nwsareapathfind.cpp" @ 0x007be3ff
    /// - BWM file format: "BWM V1.0" @ 0x007c061c (BWM file signature)
    /// - Error messages:
    ///   - "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   - "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
    ///   - "ERROR: opening a Binary walkmesh file for writeing that already exists (File: %s)" @ 0x007c0630
    /// - Original implementation: Creates navigation mesh from BWM (BioWare Walkmesh) data
    /// - BWM file structure: Header with "BWM V1.0" signature, vertex data, face data, adjacency data, AABB tree
    /// - Adjacency encoding: adjacency_index = face_index * 3 + edge_index, -1 = no neighbor
    /// - Surface materials determine walkability (0-30 range, lookup via surfacemat.2da)
    /// - AABB tree: Spatial acceleration structure for efficient face queries (FindFaceAt, Raycast)
    /// - Pathfinding uses A* algorithm on walkmesh adjacency graph
    /// - Grid-based pathfinding used for initial/terminal path segments when direct walkmesh path fails
    /// - Based on BWM file format documentation in vendor/PyKotor/wiki/BWM-File-Format.md
    /// </remarks>
    public static class NavigationMeshFactory
    {
        /// <summary>
        /// Creates a NavigationMesh from raw walkmesh data.
        /// </summary>
        /// <param name="vertices">Array of vertex positions</param>
        /// <param name="faceIndices">Triangle indices (3 per face)</param>
        /// <param name="adjacency">Adjacency data (3 per face, -1 = no neighbor)</param>
        /// <param name="surfaceMaterials">Surface material per face</param>
        /// <param name="aabbNodes">AABB tree nodes (can be null)</param>
        public static NavigationMesh Create(
            Vector3[] vertices,
            int[] faceIndices,
            int[] adjacency,
            int[] surfaceMaterials,
            AabbNodeData[] aabbNodes)
        {
            NavigationMesh.AabbNode aabbRoot = null;

            if (aabbNodes != null && aabbNodes.Length > 0)
            {
                // Build AABB tree from flat node array
                var nodeMap = new Dictionary<int, NavigationMesh.AabbNode>();
                for (int i = 0; i < aabbNodes.Length; i++)
                {
                    AabbNodeData data = aabbNodes[i];
                    var node = new NavigationMesh.AabbNode
                    {
                        BoundsMin = data.BoundsMin,
                        BoundsMax = data.BoundsMax,
                        FaceIndex = data.FaceIndex
                    };
                    nodeMap[i] = node;
                }

                // Link children
                for (int i = 0; i < aabbNodes.Length; i++)
                {
                    AabbNodeData data = aabbNodes[i];
                    NavigationMesh.AabbNode node = nodeMap[i];

                    if (data.LeftChildIndex >= 0 && data.LeftChildIndex < aabbNodes.Length)
                    {
                        node.Left = nodeMap[data.LeftChildIndex];
                    }
                    if (data.RightChildIndex >= 0 && data.RightChildIndex < aabbNodes.Length)
                    {
                        node.Right = nodeMap[data.RightChildIndex];
                    }
                }

                // Find root (first node, or node with no parent)
                if (aabbNodes.Length > 0)
                {
                    // Root is typically the first node added
                    aabbRoot = nodeMap[0];
                }
            }

            return new NavigationMesh(vertices, faceIndices, adjacency, surfaceMaterials, aabbRoot);
        }

        /// <summary>
        /// Creates a NavigationMesh from a simple triangle list (no AABB tree).
        /// Adjacency is computed automatically.
        /// </summary>
        public static NavigationMesh CreateFromTriangles(
            Vector3[] vertices,
            int[] faceIndices,
            int[] surfaceMaterials)
        {
            int faceCount = faceIndices.Length / 3;
            int[] adjacency = new int[faceCount * 3];

            // Initialize all adjacencies to -1 (no neighbor)
            for (int i = 0; i < adjacency.Length; i++)
            {
                adjacency[i] = -1;
            }

            // Build edge-to-face mapping
            var edgeToFace = new Dictionary<EdgeKey, EdgeInfo>();

            for (int face = 0; face < faceCount; face++)
            {
                int baseIdx = face * 3;
                int v0 = faceIndices[baseIdx];
                int v1 = faceIndices[baseIdx + 1];
                int v2 = faceIndices[baseIdx + 2];

                // Edge 0: v0 -> v1
                ProcessEdge(edgeToFace, adjacency, v0, v1, face, 0);
                // Edge 1: v1 -> v2
                ProcessEdge(edgeToFace, adjacency, v1, v2, face, 1);
                // Edge 2: v2 -> v0
                ProcessEdge(edgeToFace, adjacency, v2, v0, face, 2);
            }

            return new NavigationMesh(vertices, faceIndices, adjacency, surfaceMaterials, null);
        }

        private static void ProcessEdge(
            Dictionary<EdgeKey, EdgeInfo> edgeToFace,
            int[] adjacency,
            int v0, int v1,
            int face, int edge)
        {
            var key = new EdgeKey(v0, v1);

            EdgeInfo existing;
            if (edgeToFace.TryGetValue(key, out existing))
            {
                // Found adjacent face - link them
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
    }

    /// <summary>
    /// Raw AABB node data for building the tree.
    /// </summary>
    public struct AabbNodeData
    {
        public Vector3 BoundsMin;
        public Vector3 BoundsMax;
        public int FaceIndex;       // -1 for internal nodes
        public int LeftChildIndex;  // -1 for no child
        public int RightChildIndex; // -1 for no child

        public AabbNodeData(Vector3 boundsMin, Vector3 boundsMax, int faceIndex, int leftChild, int rightChild)
        {
            BoundsMin = boundsMin;
            BoundsMax = boundsMax;
            FaceIndex = faceIndex;
            LeftChildIndex = leftChild;
            RightChildIndex = rightChild;
        }
    }
}

