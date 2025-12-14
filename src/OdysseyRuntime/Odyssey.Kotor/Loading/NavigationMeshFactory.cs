using System;
using System.Collections.Generic;
using System.Numerics;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.BWM;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Module;
using Odyssey.Core.Navigation;

namespace Odyssey.Kotor.Loading
{
    /// <summary>
    /// Factory for creating NavigationMesh from BWM walkmesh data.
    /// </summary>
    /// <remarks>
    /// BWM File Format (from spec):
    /// - Type 1: Area walkmesh (WOK) - Includes AABB tree + walkable adjacency + perimeter edges
    /// - Type 0: Placeable/door walkmesh (PWK/DWK) - Collision + hook vectors
    /// 
    /// Adjacency encoding: adjacency = faceIndex * 3 + edgeIndex
    /// Decode: face = adjacency / 3, edge = adjacency % 3
    /// </remarks>
    public class NavigationMeshFactory
    {
        /// <summary>
        /// Creates a combined navigation mesh from all room walkmeshes in a module.
        /// </summary>
        [CanBeNull]
        public INavigationMesh CreateFromModule(Module module, List<RoomInfo> rooms)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return null;
            }

            // Collect all vertices and faces from all room walkmeshes
            var allVertices = new List<Vector3>();
            var allFaceIndices = new List<int>();
            var allAdjacency = new List<int>();
            var allSurfaceMaterials = new List<int>();

            int vertexOffset = 0;
            int faceOffset = 0;

            foreach (var room in rooms)
            {
                string wokResRef = room.ModelName;
                if (string.IsNullOrEmpty(wokResRef))
                {
                    continue;
                }

                // Try to load the walkmesh for this room
                BWM bwm = LoadWalkmesh(module, wokResRef);
                if (bwm == null || bwm.Faces.Count == 0)
                {
                    continue;
                }

                // Get room transform
                Vector3 roomPosition = room.Position;

                // Build vertex index map for this walkmesh
                var vertexIndexMap = new Dictionary<CSharpKOTOR.Common.Vector3, int>();
                
                // Process faces from this walkmesh
                foreach (var face in bwm.Faces)
                {
                    // Add vertices (with room offset applied)
                    int idx1 = AddVertex(allVertices, face.V1, roomPosition, vertexIndexMap);
                    int idx2 = AddVertex(allVertices, face.V2, roomPosition, vertexIndexMap);
                    int idx3 = AddVertex(allVertices, face.V3, roomPosition, vertexIndexMap);

                    // Add face indices
                    allFaceIndices.Add(idx1);
                    allFaceIndices.Add(idx2);
                    allFaceIndices.Add(idx3);

                    // Add surface material
                    allSurfaceMaterials.Add((int)face.Material);
                }

                // Process adjacency
                // Note: Adjacency references within same walkmesh need to be remapped
                var walkableFaces = bwm.WalkableFaces();
                for (int i = 0; i < bwm.Faces.Count; i++)
                {
                    var face = bwm.Faces[i];
                    var adj = bwm.Adjacencies(face);

                    // For each edge, compute the remapped adjacency
                    allAdjacency.Add(RemapAdjacency(adj.Item1, bwm.Faces, faceOffset));
                    allAdjacency.Add(RemapAdjacency(adj.Item2, bwm.Faces, faceOffset));
                    allAdjacency.Add(RemapAdjacency(adj.Item3, bwm.Faces, faceOffset));
                }

                vertexOffset = allVertices.Count;
                faceOffset += bwm.Faces.Count;
            }

            if (allFaceIndices.Count == 0)
            {
                return null;
            }

            // Build AABB tree for spatial queries
            var aabbRoot = BuildAabbTree(allVertices, allFaceIndices, allSurfaceMaterials);

            return new NavigationMesh(
                allVertices.ToArray(),
                allFaceIndices.ToArray(),
                allAdjacency.ToArray(),
                allSurfaceMaterials.ToArray(),
                aabbRoot
            );
        }

        /// <summary>
        /// Creates a navigation mesh from a single BWM.
        /// </summary>
        [CanBeNull]
        public INavigationMesh CreateFromBwm(BWM bwm, Vector3 offset)
        {
            if (bwm == null || bwm.Faces.Count == 0)
            {
                return null;
            }

            var vertices = new List<Vector3>();
            var faceIndices = new List<int>();
            var adjacency = new List<int>();
            var surfaceMaterials = new List<int>();

            var vertexIndexMap = new Dictionary<CSharpKOTOR.Common.Vector3, int>();

            foreach (var face in bwm.Faces)
            {
                int idx1 = AddVertex(vertices, face.V1, offset, vertexIndexMap);
                int idx2 = AddVertex(vertices, face.V2, offset, vertexIndexMap);
                int idx3 = AddVertex(vertices, face.V3, offset, vertexIndexMap);

                faceIndices.Add(idx1);
                faceIndices.Add(idx2);
                faceIndices.Add(idx3);

                surfaceMaterials.Add((int)face.Material);
            }

            // Process adjacency
            for (int i = 0; i < bwm.Faces.Count; i++)
            {
                var face = bwm.Faces[i];
                var adj = bwm.Adjacencies(face);

                adjacency.Add(RemapAdjacency(adj.Item1, bwm.Faces, 0));
                adjacency.Add(RemapAdjacency(adj.Item2, bwm.Faces, 0));
                adjacency.Add(RemapAdjacency(adj.Item3, bwm.Faces, 0));
            }

            var aabbRoot = BuildAabbTree(vertices, faceIndices, surfaceMaterials);

            return new NavigationMesh(
                vertices.ToArray(),
                faceIndices.ToArray(),
                adjacency.ToArray(),
                surfaceMaterials.ToArray(),
                aabbRoot
            );
        }

        /// <summary>
        /// Loads a walkmesh from the module.
        /// </summary>
        [CanBeNull]
        private BWM LoadWalkmesh(Module module, string resRef)
        {
            // Try to find WOK resource (area walkmesh)
            // WOK files are typically named after the room model
            try
            {
                // WOK files are usually stored with the module
                // For now, try to load from installation
                var installation = module.Installation;
                if (installation == null)
                {
                    return null;
                }

                // Search for WOK resource
                var wokResource = installation.Resource(resRef, ResourceType.WOK, 
                    new[] { SearchLocation.CHITIN, SearchLocation.CUSTOM_MODULES });

                if (wokResource == null || wokResource.Data == null)
                {
                    return null;
                }

                return BWMAuto.ReadBwm(wokResource.Data);
            }
            catch (Exception)
            {
                // Failed to load walkmesh
                return null;
            }
        }

        /// <summary>
        /// Adds a vertex to the list, returning its index.
        /// </summary>
        private int AddVertex(List<Vector3> vertices, CSharpKOTOR.Common.Vector3 v, Vector3 offset, 
            Dictionary<CSharpKOTOR.Common.Vector3, int> indexMap)
        {
            // Check if vertex already exists
            if (indexMap.TryGetValue(v, out int existingIndex))
            {
                return existingIndex;
            }

            // Add new vertex with offset
            int newIndex = vertices.Count;
            vertices.Add(new Vector3(v.X + offset.X, v.Y + offset.Y, v.Z + offset.Z));
            indexMap[v] = newIndex;
            return newIndex;
        }

        /// <summary>
        /// Remaps adjacency to account for face offset in combined mesh.
        /// </summary>
        private int RemapAdjacency(BWMAdjacency adj, List<BWMFace> originalFaces, int faceOffset)
        {
            if (adj == null)
            {
                return -1;
            }

            // Find the face index in the original list
            int originalFaceIndex = -1;
            for (int i = 0; i < originalFaces.Count; i++)
            {
                if (ReferenceEquals(originalFaces[i], adj.Face))
                {
                    originalFaceIndex = i;
                    break;
                }
            }

            if (originalFaceIndex < 0)
            {
                return -1;
            }

            // Encode as (face * 3 + edge) with offset
            return (originalFaceIndex + faceOffset) * 3 + adj.Edge;
        }

        /// <summary>
        /// Builds an AABB tree for the navigation mesh.
        /// </summary>
        [CanBeNull]
        private NavigationMesh.AabbNode BuildAabbTree(List<Vector3> vertices, List<int> faceIndices, List<int> materials)
        {
            int faceCount = faceIndices.Count / 3;
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

            return BuildAabbTreeRecursive(vertices, faceIndices, materials, faceList, 0);
        }

        /// <summary>
        /// Recursively builds the AABB tree.
        /// </summary>
        private NavigationMesh.AabbNode BuildAabbTreeRecursive(List<Vector3> vertices, List<int> faceIndices, 
            List<int> materials, List<int> faceList, int depth)
        {
            const int MaxDepth = 32;
            
            if (faceList.Count == 0)
            {
                return null;
            }

            // Calculate bounds
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

            var node = new NavigationMesh.AabbNode
            {
                BoundsMin = bbMin,
                BoundsMax = bbMax
            };

            // Leaf node if only one face or max depth reached
            if (faceList.Count == 1 || depth >= MaxDepth)
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

            // Split point is center of bounding box
            float splitValue = (GetAxisValue(bbMin, splitAxis) + GetAxisValue(bbMax, splitAxis)) * 0.5f;

            // Partition faces
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
                float nextSplitValue = (GetAxisValue(bbMin, nextAxis) + GetAxisValue(bbMax, nextAxis)) * 0.5f;
                
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

                // Still degenerate - just split in half
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

            node.FaceIndex = -1; // Internal node
            node.Left = BuildAabbTreeRecursive(vertices, faceIndices, materials, leftFaces, depth + 1);
            node.Right = BuildAabbTreeRecursive(vertices, faceIndices, materials, rightFaces, depth + 1);

            return node;
        }

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
