using System;
using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;

namespace CSharpKOTOR.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:127-1843
    // Original: class BWM(ComparableMixin)
    public class BWM : IEquatable<BWM>
    {
        public BWMType WalkmeshType { get; set; }
        public List<BWMFace> Faces { get; set; }
        public Vector3 Position { get; set; }
        public Vector3 RelativeHook1 { get; set; }
        public Vector3 RelativeHook2 { get; set; }
        public Vector3 AbsoluteHook1 { get; set; }
        public Vector3 AbsoluteHook2 { get; set; }

        public BWM()
        {
            // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:193-226
            // Original: def __init__(self)
            WalkmeshType = BWMType.AreaModel;
            Faces = new List<BWMFace>();
            Position = Vector3.FromNull();
            RelativeHook1 = Vector3.FromNull();
            RelativeHook2 = Vector3.FromNull();
            AbsoluteHook1 = Vector3.FromNull();
            AbsoluteHook2 = Vector3.FromNull();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:228-239
        // Original: def __eq__(self, other)
        public override bool Equals(object obj)
        {
            return obj is BWM other && Equals(other);
        }

        public bool Equals(BWM other)
        {
            if (other == null)
            {
                return false;
            }
            return WalkmeshType == other.WalkmeshType &&
                   Faces.SequenceEqual(other.Faces) &&
                   Position.Equals(other.Position) &&
                   RelativeHook1.Equals(other.RelativeHook1) &&
                   RelativeHook2.Equals(other.RelativeHook2) &&
                   AbsoluteHook1.Equals(other.AbsoluteHook1) &&
                   AbsoluteHook2.Equals(other.AbsoluteHook2);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:241-252
        // Original: def __hash__(self)
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(WalkmeshType);
            foreach (var face in Faces)
            {
                hash.Add(face);
            }
            hash.Add(Position);
            hash.Add(RelativeHook1);
            hash.Add(RelativeHook2);
            hash.Add(AbsoluteHook1);
            hash.Add(AbsoluteHook2);
            return hash.ToHashCode();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:254-273
        // Original: def walkable_faces(self) -> list[BWMFace]
        public List<BWMFace> WalkableFaces()
        {
            return Faces.Where(face => face.Material.Walkable()).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:275-291
        // Original: def unwalkable_faces(self) -> list[BWMFace]
        public List<BWMFace> UnwalkableFaces()
        {
            return Faces.Where(face => !face.Material.Walkable()).ToList();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:293-311
        // Original: def vertices(self) -> list[Vector3]
        public List<Vector3> Vertices()
        {
            List<Vector3> vertices = new List<Vector3>();
            foreach (var face in Faces)
            {
                if (!face.V1.Within(vertices))
                {
                    vertices.Add(face.V1);
                }
                if (!face.V2.Within(vertices))
                {
                    vertices.Add(face.V2);
                }
                if (!face.V3.Within(vertices))
                {
                    vertices.Add(face.V3);
                }
            }
            return vertices;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:313-346
        // Original: def aabbs(self) -> list[BWMNodeAABB]
        public List<BWMNodeAABB> Aabbs()
        {
            // PWK/DWK files don't have AABB trees (only WOK/AreaModel do)
            if (WalkmeshType == BWMType.PlaceableOrDoor)
            {
                return new List<BWMNodeAABB>();
            }

            // Empty walkmeshes cannot generate AABB trees
            if (Faces.Count == 0)
            {
                return new List<BWMNodeAABB>();
            }

            List<BWMNodeAABB> aabbs = new List<BWMNodeAABB>();
            _AabbsRec(aabbs, new List<BWMFace>(Faces), 0);
            return aabbs;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:348-480
        // Original: def _aabbs_rec(self, aabbs: list[BWMNodeAABB], faces: list[BWMFace], rlevel: int = 0) -> BWMNodeAABB
        private BWMNodeAABB _AabbsRec(List<BWMNodeAABB> aabbs, List<BWMFace> faces, int rlevel = 0)
        {
            const int maxLevel = 128;
            if (rlevel > maxLevel)
            {
                throw new ArgumentException($"recursion level must not exceed {maxLevel}, but is currently at level {rlevel}");
            }

            if (faces.Count == 0)
            {
                throw new ArgumentException("face_list must not be empty");
            }

            // Calculate bounding box
            Vector3 bbmin = new Vector3(100000.0f, 100000.0f, 100000.0f);
            Vector3 bbmax = new Vector3(-100000.0f, -100000.0f, -100000.0f);
            Vector3 bbcentre = Vector3.FromNull();
            foreach (var face in faces)
            {
                foreach (var vertex in new[] { face.V1, face.V2, face.V3 })
                {
                    for (int axis = 0; axis < 3; axis++)
                    {
                        bbmin[axis] = Math.Min(bbmin[axis], vertex[axis]);
                        bbmax[axis] = Math.Max(bbmax[axis], vertex[axis]);
                    }
                }
                bbcentre = bbcentre + face.Centre();
            }
            bbcentre = bbcentre / faces.Count;

            // Only one face left - this node is a leaf
            if (faces.Count == 1)
            {
                var leaf = new BWMNodeAABB(bbmin, bbmax, faces[0], 0, null, null);
                aabbs.Add(leaf);
                return leaf;
            }

            // Find longest axis
            int splitAxis = 0;
            Vector3 bbSize = bbmax - bbmin;
            if (bbSize.Y > bbSize.X)
            {
                splitAxis = 1;
            }
            if (bbSize.Z > bbSize.Y)
            {
                splitAxis = 2;
            }

            // Change axis in case points are coplanar with the split plane
            bool changeAxis = true;
            foreach (var face in faces)
            {
                Vector3 centre = face.Centre();
                changeAxis = changeAxis && Math.Abs(centre[splitAxis] - bbcentre[splitAxis]) < 1e-6f;
            }
            if (changeAxis)
            {
                splitAxis = splitAxis == 2 ? 0 : splitAxis + 1;
            }

            // Put faces on the left and right side of the split plane into separate lists
            List<BWMFace> facesLeft = new List<BWMFace>();
            List<BWMFace> facesRight = new List<BWMFace>();
            int testedAxes = 1;
            while (true)
            {
                facesLeft.Clear();
                facesRight.Clear();
                foreach (var face in faces)
                {
                    Vector3 centre = face.Centre();
                    if (centre[splitAxis] < bbcentre[splitAxis])
                    {
                        facesLeft.Add(face);
                    }
                    else
                    {
                        facesRight.Add(face);
                    }
                }

                if (facesLeft.Count > 0 && facesRight.Count > 0)
                {
                    break;
                }

                splitAxis = splitAxis == 2 ? 0 : splitAxis + 1;
                testedAxes++;
                if (testedAxes == 3)
                {
                    // All faces have the same center - create a single leaf node with all faces
                    if (faces.Count == 1)
                    {
                        var leaf = new BWMNodeAABB(bbmin, bbmax, faces[0], 0, null, null);
                        aabbs.Add(leaf);
                        return leaf;
                    }
                    else
                    {
                        // Multiple faces with same center - create a single leaf with first face
                        var leaf = new BWMNodeAABB(bbmin, bbmax, faces[0], 0, null, null);
                        aabbs.Add(leaf);
                        return leaf;
                    }
                }
            }

            var aabb = new BWMNodeAABB(bbmin, bbmax, null, splitAxis + 1, null, null);
            aabbs.Add(aabb);

            // Recursively build left and right subtrees
            if (facesLeft.Count > 0)
            {
                var leftChild = _AabbsRec(aabbs, facesLeft, rlevel + 1);
                aabb.Left = leftChild;
            }
            else
            {
                if (facesRight.Count > 0)
                {
                    var leaf = new BWMNodeAABB(bbmin, bbmax, facesRight[0], 0, null, null);
                    aabbs.Add(leaf);
                    aabb.Left = leaf;
                }
            }

            if (facesRight.Count > 0)
            {
                var rightChild = _AabbsRec(aabbs, facesRight, rlevel + 1);
                aabb.Right = rightChild;
            }
            else
            {
                if (facesLeft.Count > 0)
                {
                    var leaf = new BWMNodeAABB(bbmin, bbmax, facesLeft[0], 0, null, null);
                    aabbs.Add(leaf);
                    aabb.Right = leaf;
                }
            }

            return aabb;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:482-562
        // Original: def edges(self) -> list[BWMEdge]
        public List<BWMEdge> Edges()
        {
            List<BWMFace> walkable = WalkableFaces();
            // OPTIMIZATION: Compute all adjacencies in batch
            List<Tuple<BWMAdjacency, BWMAdjacency, BWMAdjacency>> adjacencies = _ComputeAllAdjacencies(walkable);

            // Build mapping from walkable face index to overall face index
            Dictionary<int, int> walkableToOverall = new Dictionary<int, int>();
            for (int walkableIdx = 0; walkableIdx < walkable.Count; walkableIdx++)
            {
                int overallIdx = _IndexByIdentity(walkable[walkableIdx]);
                walkableToOverall[walkableIdx] = overallIdx;
            }

            HashSet<int> visited = new HashSet<int>();
            List<BWMEdge> edges = new List<BWMEdge>();
            List<int> perimeters = new List<int>();

            for (int i = 0; i < walkable.Count; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int overallFaceIdx = walkableToOverall[i];
                    int edgeIndex = overallFaceIdx * 3 + j;
                    var adj = adjacencies[i];
                    BWMAdjacency adjItem = j == 0 ? adj.Item1 : (j == 1 ? adj.Item2 : adj.Item3);
                    if (adjItem != null || visited.Contains(edgeIndex))
                    {
                        continue;
                    }

                    int nextFace = overallFaceIdx;
                    int nextEdge = j;
                    int perimeterLength = 0;
                    while (nextFace != -1)
                    {
                        int? walkableIdxForFace = null;
                        foreach (var kvp in walkableToOverall)
                        {
                            if (kvp.Value == nextFace)
                            {
                                walkableIdxForFace = kvp.Key;
                                break;
                            }
                        }
                        if (walkableIdxForFace.HasValue)
                        {
                            var adjEdge = walkableIdxForFace.Value < adjacencies.Count ? adjacencies[walkableIdxForFace.Value] : null;
                            if (adjEdge != null)
                            {
                                BWMAdjacency adjEdgeItem = nextEdge == 0 ? adjEdge.Item1 : (nextEdge == 1 ? adjEdge.Item2 : adjEdge.Item3);
                                if (adjEdgeItem != null)
                                {
                                    int adjEdgeIndex = _IndexByIdentity(adjEdgeItem.Face) * 3 + adjEdgeItem.Edge;
                                    nextFace = adjEdgeIndex / 3;
                                    nextEdge = ((adjEdgeIndex % 3) + 1) % 3;
                                    continue;
                                }
                            }
                        }
                        edgeIndex = nextFace * 3 + nextEdge;
                        if (visited.Contains(edgeIndex))
                        {
                            nextFace = -1;
                            if (edges.Count > 0)
                            {
                                edges[edges.Count - 1].Final = true;
                            }
                            perimeters.Add(perimeterLength);
                            continue;
                        }
                        int faceId = edgeIndex / 3;
                        int edgeId = edgeIndex % 3;
                        int? transition = null;
                        if (edgeId == 0 && Faces[faceId].Trans1.HasValue)
                        {
                            transition = Faces[faceId].Trans1;
                        }
                        if (edgeId == 1 && Faces[faceId].Trans2.HasValue)
                        {
                            transition = Faces[faceId].Trans2;
                        }
                        if (edgeId == 2 && Faces[faceId].Trans3.HasValue)
                        {
                            transition = Faces[faceId].Trans3;
                        }
                        edges.Add(new BWMEdge(Faces[nextFace], edgeId, transition ?? -1));
                        perimeterLength++;
                        visited.Add(edgeIndex);
                        nextEdge = (edgeIndex + 1) % 3;
                    }
                }
            }

            return edges;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:564-640
        // Original: def raycast(self, origin: Vector3, direction: Vector3, max_distance: float = float("inf"), materials: set[SurfaceMaterial] | None = None) -> tuple[BWMFace, float] | None
        public Tuple<BWMFace, float> Raycast(Vector3 origin, Vector3 direction, float maxDistance = float.MaxValue, HashSet<SurfaceMaterial> materials = null)
        {
            if (Faces.Count == 0)
            {
                return null;
            }

            // Default to walkable materials if not specified
            if (materials == null)
            {
                materials = new HashSet<SurfaceMaterial>();
                foreach (SurfaceMaterial mat in Enum.GetValues(typeof(SurfaceMaterial)))
                {
                    if (mat.Walkable())
                    {
                        materials.Add(mat);
                    }
                }
            }

            // For placeable/door walkmeshes, test all faces directly
            if (WalkmeshType == BWMType.PlaceableOrDoor)
            {
                return _RaycastBruteForce(origin, direction, maxDistance, materials);
            }

            // For area walkmeshes, use AABB tree
            List<BWMNodeAABB> aabbs = Aabbs();
            if (aabbs.Count == 0)
            {
                return _RaycastBruteForce(origin, direction, maxDistance, materials);
            }

            // Find root node (node with no parent)
            HashSet<object> childNodes = new HashSet<object>();
            foreach (var aabb in aabbs)
            {
                if (aabb.Left != null)
                {
                    childNodes.Add(aabb.Left);
                }
                if (aabb.Right != null)
                {
                    childNodes.Add(aabb.Right);
                }
            }

            List<BWMNodeAABB> rootNodes = aabbs.Where(aabb => !childNodes.Contains(aabb)).ToList();
            BWMNodeAABB root;
            if (rootNodes.Count == 0)
            {
                root = aabbs.Count > 0 ? aabbs[0] : null;
                if (root == null)
                {
                    return _RaycastBruteForce(origin, direction, maxDistance, materials);
                }
            }
            else
            {
                root = rootNodes[0];
            }

            // Traverse AABB tree
            return _RaycastAabb(root, origin, direction, maxDistance, materials);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:642-687
        // Original: def _raycast_aabb(self, node: BWMNodeAABB, origin: Vector3, direction: Vector3, max_distance: float, materials: set[SurfaceMaterial]) -> tuple[BWMFace, float] | None
        private Tuple<BWMFace, float> _RaycastAabb(BWMNodeAABB node, Vector3 origin, Vector3 direction, float maxDistance, HashSet<SurfaceMaterial> materials)
        {
            // Test ray against AABB bounds
            if (!_RayAabbIntersect(origin, direction, node.BbMin, node.BbMax, maxDistance))
            {
                return null;
            }

            // If leaf node, test ray against face
            if (node.Face != null)
            {
                if (!materials.Contains(node.Face.Material))
                {
                    return null;
                }
                float? distance = _RayTriangleIntersect(origin, direction, node.Face, maxDistance);
                if (distance.HasValue)
                {
                    return Tuple.Create(node.Face, distance.Value);
                }
                return null;
            }

            // Internal node: test children
            Tuple<BWMFace, float> bestResult = null;
            float bestDistance = maxDistance;

            if (node.Left != null)
            {
                var result = _RaycastAabb(node.Left, origin, direction, bestDistance, materials);
                if (result != null)
                {
                    float dist = result.Item2;
                    if (dist < bestDistance)
                    {
                        bestResult = result;
                        bestDistance = dist;
                    }
                }
            }

            if (node.Right != null)
            {
                var result = _RaycastAabb(node.Right, origin, direction, bestDistance, materials);
                if (result != null)
                {
                    float dist = result.Item2;
                    if (dist < bestDistance)
                    {
                        bestResult = result;
                        bestDistance = dist;
                    }
                }
            }

            return bestResult;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:689-711
        // Original: def _raycast_brute_force(self, origin: Vector3, direction: Vector3, max_distance: float, materials: set[SurfaceMaterial]) -> tuple[BWMFace, float] | None
        private Tuple<BWMFace, float> _RaycastBruteForce(Vector3 origin, Vector3 direction, float maxDistance, HashSet<SurfaceMaterial> materials)
        {
            Tuple<BWMFace, float> bestResult = null;
            float bestDistance = maxDistance;

            foreach (var face in Faces)
            {
                if (!materials.Contains(face.Material))
                {
                    continue;
                }
                float? distance = _RayTriangleIntersect(origin, direction, face, bestDistance);
                if (distance.HasValue && distance.Value < bestDistance)
                {
                    bestResult = Tuple.Create(face, distance.Value);
                    bestDistance = distance.Value;
                }
            }

            return bestResult;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:713-772
        // Original: def _ray_aabb_intersect(self, origin: Vector3, direction: Vector3, bb_min: Vector3, bb_max: Vector3, max_distance: float) -> bool
        private bool _RayAabbIntersect(Vector3 origin, Vector3 direction, Vector3 bbMin, Vector3 bbMax, float maxDistance)
        {
            // Avoid division by zero
            Vector3 invDir = new Vector3(
                direction.X != 0.0f ? 1.0f / direction.X : float.MaxValue,
                direction.Y != 0.0f ? 1.0f / direction.Y : float.MaxValue,
                direction.Z != 0.0f ? 1.0f / direction.Z : float.MaxValue
            );

            float tmin = (bbMin.X - origin.X) * invDir.X;
            float tmax = (bbMax.X - origin.X) * invDir.X;

            if (invDir.X < 0)
            {
                float temp = tmin;
                tmin = tmax;
                tmax = temp;
            }

            float tymin = (bbMin.Y - origin.Y) * invDir.Y;
            float tymax = (bbMax.Y - origin.Y) * invDir.Y;

            if (invDir.Y < 0)
            {
                float temp = tymin;
                tymin = tymax;
                tymax = temp;
            }

            if (tmin > tymax || tymin > tmax)
            {
                return false;
            }

            if (tymin > tmin)
            {
                tmin = tymin;
            }
            if (tymax < tmax)
            {
                tmax = tymax;
            }

            float tzmin = (bbMin.Z - origin.Z) * invDir.Z;
            float tzmax = (bbMax.Z - origin.Z) * invDir.Z;

            if (invDir.Z < 0)
            {
                float temp = tzmin;
                tzmin = tzmax;
                tzmax = temp;
            }

            if (tmin > tzmax || tzmin > tmax)
            {
                return false;
            }

            if (tzmin > tmin)
            {
                tmin = tzmin;
            }
            if (tzmax < tmax)
            {
                tmax = tzmax;
            }

            // Check if intersection is within max_distance
            if (tmin < 0)
            {
                tmin = tmax;
            }
            if (tmin < 0 || tmin > maxDistance)
            {
                return false;
            }

            return true;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:774-827
        // Original: def _ray_triangle_intersect(self, origin: Vector3, direction: Vector3, face: BWMFace, max_distance: float) -> float | None
        private float? _RayTriangleIntersect(Vector3 origin, Vector3 direction, BWMFace face, float maxDistance)
        {
            Vector3 v0 = face.V1;
            Vector3 v1 = face.V2;
            Vector3 v2 = face.V3;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;

            Vector3 h = Vector3.Cross(direction, edge2);
            float a = edge1.Dot(h);

            // Ray is parallel to triangle
            if (Math.Abs(a) < 1e-6f)
            {
                return null;
            }

            float f = 1.0f / a;
            Vector3 s = origin - v0;
            float u = f * s.Dot(h);

            if (u < 0.0f || u > 1.0f)
            {
                return null;
            }

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * direction.Dot(q);

            if (v < 0.0f || u + v > 1.0f)
            {
                return null;
            }

            // Intersection found, compute distance
            float t = f * edge2.Dot(q);

            if (t > 1e-6f && t < maxDistance)
            {
                return t;
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:829-880
        // Original: def point_in_face_2d(self, point: Vector3, face: BWMFace) -> bool
        public bool PointInFace2d(Vector3 point, BWMFace face)
        {
            // Use sign-based method (same-side test)
            float Sign(Vector3 p1, Vector3 p2, Vector3 p3)
            {
                return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
            }

            Vector3 v1 = face.V1;
            Vector3 v2 = face.V2;
            Vector3 v3 = face.V3;

            float d1 = Sign(point, v1, v2);
            float d2 = Sign(point, v2, v3);
            float d3 = Sign(point, v3, v1);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            // Point is inside if all signs are same (not both positive and negative)
            return !(hasNeg && hasPos);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:882-935
        // Original: def get_height_at(self, x: float, y: float, materials: set[SurfaceMaterial] | None = None) -> float | None
        public float? GetHeightAt(float x, float y, HashSet<SurfaceMaterial> materials = null)
        {
            // Default to walkable materials if not specified
            if (materials == null)
            {
                materials = new HashSet<SurfaceMaterial>();
                foreach (SurfaceMaterial mat in Enum.GetValues(typeof(SurfaceMaterial)))
                {
                    if (mat.Walkable())
                    {
                        materials.Add(mat);
                    }
                }
            }

            // Find face containing the point
            BWMFace face = FindFaceAt(x, y, materials);
            if (face == null)
            {
                return null;
            }

            // Check if face is flat (all vertices have same Z)
            if (Math.Abs(face.V1.Z - face.V2.Z) < 1e-6f && Math.Abs(face.V2.Z - face.V3.Z) < 1e-6f)
            {
                return face.V1.Z;
            }

            // Use face's determine_z method to compute Z coordinate
            try
            {
                return face.DetermineZ(x, y);
            }
            catch (DivideByZeroException)
            {
                // Fallback: if determine_z fails (degenerate case), return average Z
                return (face.V1.Z + face.V2.Z + face.V3.Z) / 3.0f;
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:937-1004
        // Original: def find_face_at(self, x: float, y: float, materials: set[SurfaceMaterial] | None = None) -> BWMFace | None
        public BWMFace FindFaceAt(float x, float y, HashSet<SurfaceMaterial> materials = null)
        {
            Vector3 point = new Vector3(x, y, 0.0f);

            // Default to walkable materials if not specified
            if (materials == null)
            {
                materials = new HashSet<SurfaceMaterial>();
                foreach (SurfaceMaterial mat in Enum.GetValues(typeof(SurfaceMaterial)))
                {
                    if (mat.Walkable())
                    {
                        materials.Add(mat);
                    }
                }
            }

            // For placeable/door walkmeshes, test all faces directly
            if (WalkmeshType == BWMType.PlaceableOrDoor)
            {
                return _FindFaceBruteForce(point, materials);
            }

            // For area walkmeshes, use AABB tree
            List<BWMNodeAABB> aabbs = Aabbs();
            if (aabbs.Count == 0)
            {
                return _FindFaceBruteForce(point, materials);
            }

            // Find root node (node with no parent)
            HashSet<object> childNodes = new HashSet<object>();
            foreach (var aabb in aabbs)
            {
                if (aabb.Left != null)
                {
                    childNodes.Add(aabb.Left);
                }
                if (aabb.Right != null)
                {
                    childNodes.Add(aabb.Right);
                }
            }

            List<BWMNodeAABB> rootNodes = aabbs.Where(aabb => !childNodes.Contains(aabb)).ToList();
            BWMNodeAABB root;
            if (rootNodes.Count == 0)
            {
                root = aabbs.Count > 0 ? aabbs[0] : null;
                if (root == null)
                {
                    return _FindFaceBruteForce(point, materials);
                }
            }
            else
            {
                root = rootNodes[0];
            }

            // Traverse AABB tree
            return _FindFaceAabb(root, point, materials);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1006-1039
        // Original: def _find_face_aabb(self, node: BWMNodeAABB, point: Vector3, materials: set[SurfaceMaterial]) -> BWMFace | None
        private BWMFace _FindFaceAabb(BWMNodeAABB node, Vector3 point, HashSet<SurfaceMaterial> materials)
        {
            // Test if point is in AABB bounds (only check X and Y for 2D point-in-face)
            if (!(node.BbMin.X <= point.X && point.X <= node.BbMax.X && node.BbMin.Y <= point.Y && point.Y <= node.BbMax.Y))
            {
                return null;
            }

            // If leaf node, test point against face
            if (node.Face != null)
            {
                if (!materials.Contains(node.Face.Material))
                {
                    return null;
                }
                if (PointInFace2d(point, node.Face))
                {
                    return node.Face;
                }
                return null;
            }

            // Internal node: test children
            if (node.Left != null)
            {
                var result = _FindFaceAabb(node.Left, point, materials);
                if (result != null)
                {
                    return result;
                }
            }

            if (node.Right != null)
            {
                var result = _FindFaceAabb(node.Right, point, materials);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1041-1052
        // Original: def _find_face_brute_force(self, point: Vector3, materials: set[SurfaceMaterial]) -> BWMFace | None
        private BWMFace _FindFaceBruteForce(Vector3 point, HashSet<SurfaceMaterial> materials)
        {
            foreach (var face in Faces)
            {
                if (!materials.Contains(face.Material))
                {
                    continue;
                }
                if (PointInFace2d(point, face))
                {
                    return face;
                }
            }
            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1054-1076
        // Original: def _index_by_identity(self, face: BWMFace) -> int
        private int _IndexByIdentity(BWMFace face)
        {
            for (int i = 0; i < Faces.Count; i++)
            {
                if (ReferenceEquals(Faces[i], face))
                {
                    return i;
                }
            }
            throw new ArgumentException("Face not found in faces list");
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1078-1155
        // Original: def _compute_all_adjacencies(self, walkable: list[BWMFace]) -> list[tuple[BWMAdjacency | None, BWMAdjacency | None, BWMAdjacency | None]]
        private List<Tuple<BWMAdjacency, BWMAdjacency, BWMAdjacency>> _ComputeAllAdjacencies(List<BWMFace> walkable)
        {
            // Build edge-to-faces mapping using a custom key for edge pairs
            Dictionary<string, List<Tuple<BWMFace, int>>> edgeToFaces = new Dictionary<string, List<Tuple<BWMFace, int>>>();

            // Define edge vertices for each face edge
            // Edge 0: v1->v2, Edge 1: v2->v3, Edge 2: v3->v1
            foreach (var face in walkable)
            {
                // Edge 0: v1->v2
                string edge0 = GetEdgeKey(face.V1, face.V2);
                if (!edgeToFaces.ContainsKey(edge0))
                {
                    edgeToFaces[edge0] = new List<Tuple<BWMFace, int>>();
                }
                edgeToFaces[edge0].Add(Tuple.Create(face, 0));

                // Edge 1: v2->v3
                string edge1 = GetEdgeKey(face.V2, face.V3);
                if (!edgeToFaces.ContainsKey(edge1))
                {
                    edgeToFaces[edge1] = new List<Tuple<BWMFace, int>>();
                }
                edgeToFaces[edge1].Add(Tuple.Create(face, 1));

                // Edge 2: v3->v1
                string edge2 = GetEdgeKey(face.V3, face.V1);
                if (!edgeToFaces.ContainsKey(edge2))
                {
                    edgeToFaces[edge2] = new List<Tuple<BWMFace, int>>();
                }
                edgeToFaces[edge2].Add(Tuple.Create(face, 2));
            }

            // Now compute adjacencies for each face by looking up edges
            List<Tuple<BWMAdjacency, BWMAdjacency, BWMAdjacency>> result = new List<Tuple<BWMAdjacency, BWMAdjacency, BWMAdjacency>>();

            foreach (var face in walkable)
            {
                BWMAdjacency adj1 = null;
                BWMAdjacency adj2 = null;
                BWMAdjacency adj3 = null;

                // Check edge 0 (v1->v2)
                string edge0 = GetEdgeKey(face.V1, face.V2);
                if (edgeToFaces.ContainsKey(edge0))
                {
                    foreach (var tuple in edgeToFaces[edge0])
                    {
                        BWMFace otherFace = tuple.Item1;
                        int otherEdge = tuple.Item2;
                        if (!ReferenceEquals(otherFace, face))
                        {
                            adj1 = new BWMAdjacency(otherFace, otherEdge);
                            break;
                        }
                    }
                }

                // Check edge 1 (v2->v3)
                string edge1 = GetEdgeKey(face.V2, face.V3);
                if (edgeToFaces.ContainsKey(edge1))
                {
                    foreach (var tuple in edgeToFaces[edge1])
                    {
                        BWMFace otherFace = tuple.Item1;
                        int otherEdge = tuple.Item2;
                        if (!ReferenceEquals(otherFace, face))
                        {
                            adj2 = new BWMAdjacency(otherFace, otherEdge);
                            break;
                        }
                    }
                }

                // Check edge 2 (v3->v1)
                string edge2 = GetEdgeKey(face.V3, face.V1);
                if (edgeToFaces.ContainsKey(edge2))
                {
                    foreach (var tuple in edgeToFaces[edge2])
                    {
                        BWMFace otherFace = tuple.Item1;
                        int otherEdge = tuple.Item2;
                        if (!ReferenceEquals(otherFace, face))
                        {
                            adj3 = new BWMAdjacency(otherFace, otherEdge);
                            break;
                        }
                    }
                }

                result.Add(Tuple.Create(adj1, adj2, adj3));
            }

            return result;
        }

        // Helper method to create a unique key for an edge (frozenset equivalent)
        private string GetEdgeKey(Vector3 v1, Vector3 v2)
        {
            // Create a consistent key regardless of vertex order
            if (v1.X < v2.X || (Math.Abs(v1.X - v2.X) < 1e-6f && (v1.Y < v2.Y || (Math.Abs(v1.Y - v2.Y) < 1e-6f && v1.Z < v2.Z))))
            {
                return $"{v1.X},{v1.Y},{v1.Z}:{v2.X},{v2.Y},{v2.Z}";
            }
            return $"{v2.X},{v2.Y},{v2.Z}:{v1.X},{v1.Y},{v1.Z}";
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1157-1221
        // Original: def adjacencies(self, face: BWMFace) -> tuple[BWMAdjacency | None, BWMAdjacency | None, BWMAdjacency | None]
        public Tuple<BWMAdjacency, BWMAdjacency, BWMAdjacency> Adjacencies(BWMFace face)
        {
            List<BWMFace> walkable = WalkableFaces();
            List<Vector3> adj1 = new List<Vector3> { face.V1, face.V2 };
            List<Vector3> adj2 = new List<Vector3> { face.V2, face.V3 };
            List<Vector3> adj3 = new List<Vector3> { face.V3, face.V1 };

            BWMAdjacency adjIndex1 = null;
            BWMAdjacency adjIndex2 = null;
            BWMAdjacency adjIndex3 = null;

            int Matches(BWMFace faceObj, List<Vector3> edges)
            {
                int flag = 0x00;
                if (edges.Any(e => e.Equals(faceObj.V1)))
                {
                    flag += 0x01;
                }
                if (edges.Any(e => e.Equals(faceObj.V2)))
                {
                    flag += 0x02;
                }
                if (edges.Any(e => e.Equals(faceObj.V3)))
                {
                    flag += 0x04;
                }
                int edge = -1;
                if (flag == 0x03)
                {
                    edge = 0;
                }
                if (flag == 0x06)
                {
                    edge = 1;
                }
                if (flag == 0x05)
                {
                    edge = 2;
                }
                return edge;
            }

            foreach (var other in walkable)
            {
                if (ReferenceEquals(other, face))
                {
                    continue;
                }
                int edgeMatch1 = Matches(other, adj1);
                int edgeMatch2 = Matches(other, adj2);
                int edgeMatch3 = Matches(other, adj3);

                if (edgeMatch1 != -1)
                {
                    adjIndex1 = new BWMAdjacency(other, edgeMatch1);
                }
                if (edgeMatch2 != -1)
                {
                    adjIndex2 = new BWMAdjacency(other, edgeMatch2);
                }
                if (edgeMatch3 != -1)
                {
                    adjIndex3 = new BWMAdjacency(other, edgeMatch3);
                }
            }

            return Tuple.Create(adjIndex1, adjIndex2, adjIndex3);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1223-1248
        // Original: def box(self) -> tuple[Vector3, Vector3]
        public Tuple<Vector3, Vector3> Box()
        {
            Vector3 bbmin = new Vector3(1000000, 1000000, 1000000);
            Vector3 bbmax = new Vector3(-1000000, -1000000, -1000000);
            foreach (var vertex in Vertices())
            {
                _HandleVertex(ref bbmin, vertex, ref bbmax);
            }
            return Tuple.Create(bbmin, bbmax);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1250-1273
        // Original: def _handle_vertex(self, bbmin: Vector3, vertex: Vector3, bbmax: Vector3)
        private void _HandleVertex(ref Vector3 bbmin, Vector3 vertex, ref Vector3 bbmax)
        {
            bbmin.X = Math.Min(bbmin.X, vertex.X);
            bbmin.Y = Math.Min(bbmin.Y, vertex.Y);
            bbmin.Z = Math.Min(bbmin.Z, vertex.Z);
            bbmax.X = Math.Max(bbmax.X, vertex.X);
            bbmax.Y = Math.Max(bbmax.Y, vertex.Y);
            bbmax.Z = Math.Max(bbmax.Z, vertex.Z);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1275-1303
        // Original: def faceAt(self, x: float, y: float) -> BWMFace | None
        public BWMFace FaceAt(float x, float y)
        {
            foreach (var face in Faces)
            {
                Vector3 v1 = face.V1;
                Vector3 v2 = face.V2;
                Vector3 v3 = face.V3;

                // Formula taken from: https://www.w3resource.com/python-exercises/basic/python-basic-1-exercise-40.php
                float c1 = (v2.X - v1.X) * (y - v1.Y) - (v2.Y - v1.Y) * (x - v1.X);
                float c2 = (v3.X - v2.X) * (y - v2.Y) - (v3.Y - v2.Y) * (x - v2.X);
                float c3 = (v1.X - v3.X) * (y - v3.Y) - (v1.Y - v3.Y) * (x - v3.X);

                if ((c1 < 0 && c2 < 0 && c3 < 0) || (c1 > 0 && c2 > 0 && c3 > 0))
                {
                    return face;
                }
            }
            return null;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1305-1322
        // Original: def translate(self, x: float, y: float, z: float)
        public void Translate(float x, float y, float z)
        {
            // In Python, Vector3 is a class, so modifying vertex.x modifies the actual object
            // In C#, Vector3 is a struct, so we need to modify vertices in faces directly
            // Since vertices may be shared, we need to track which vertices we've processed
            Dictionary<Vector3, Vector3> vertexMap = new Dictionary<Vector3, Vector3>();
            foreach (var face in Faces)
            {
                if (!vertexMap.ContainsKey(face.V1))
                {
                    vertexMap[face.V1] = new Vector3(face.V1.X + x, face.V1.Y + y, face.V1.Z + z);
                }
                face.V1 = vertexMap[face.V1];

                if (!vertexMap.ContainsKey(face.V2))
                {
                    vertexMap[face.V2] = new Vector3(face.V2.X + x, face.V2.Y + y, face.V2.Z + z);
                }
                face.V2 = vertexMap[face.V2];

                if (!vertexMap.ContainsKey(face.V3))
                {
                    vertexMap[face.V3] = new Vector3(face.V3.X + x, face.V3.Y + y, face.V3.Z + z);
                }
                face.V3 = vertexMap[face.V3];
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1324-1341
        // Original: def rotate(self, degrees: float)
        public void Rotate(float degrees)
        {
            float radians = (float)(degrees * Math.PI / 180.0);
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);

            // In Python, Vector3 is a class, so modifying vertex.x modifies the actual object
            // In C#, Vector3 is a struct, so we need to modify vertices in faces directly
            Dictionary<Vector3, Vector3> vertexMap = new Dictionary<Vector3, Vector3>();
            foreach (var face in Faces)
            {
                if (!vertexMap.ContainsKey(face.V1))
                {
                    float vx = face.V1.X;
                    float vy = face.V1.Y;
                    vertexMap[face.V1] = new Vector3(vx * cos - vy * sin, vx * sin + vy * cos, face.V1.Z);
                }
                face.V1 = vertexMap[face.V1];

                if (!vertexMap.ContainsKey(face.V2))
                {
                    float vx = face.V2.X;
                    float vy = face.V2.Y;
                    vertexMap[face.V2] = new Vector3(vx * cos - vy * sin, vx * sin + vy * cos, face.V2.Z);
                }
                face.V2 = vertexMap[face.V2];

                if (!vertexMap.ContainsKey(face.V3))
                {
                    float vx = face.V3.X;
                    float vy = face.V3.Y;
                    vertexMap[face.V3] = new Vector3(vx * cos - vy * sin, vx * sin + vy * cos, face.V3.Z);
                }
                face.V3 = vertexMap[face.V3];
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1343-1367
        // Original: def change_lyt_indexes(self, old: int, new: int | None)
        public void ChangeLytIndexes(int old, int? newIndex)
        {
            foreach (var face in Faces)
            {
                if (face.Trans1 == old)
                {
                    face.Trans1 = newIndex;
                }
                if (face.Trans2 == old)
                {
                    face.Trans2 = newIndex;
                }
                if (face.Trans3 == old)
                {
                    face.Trans3 = newIndex;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/bwm_data.py:1369-1394
        // Original: def flip(self, x: bool, y: bool)
        public void Flip(bool x, bool y)
        {
            if (!x && !y)
            {
                return;
            }

            // In Python, Vector3 is a class, so modifying vertex.x modifies the actual object
            // In C#, Vector3 is a struct, so we need to modify vertices in faces directly
            Dictionary<Vector3, Vector3> vertexMap = new Dictionary<Vector3, Vector3>();
            foreach (var face in Faces)
            {
                if (!vertexMap.ContainsKey(face.V1))
                {
                    float vx = x ? -face.V1.X : face.V1.X;
                    float vy = y ? -face.V1.Y : face.V1.Y;
                    vertexMap[face.V1] = new Vector3(vx, vy, face.V1.Z);
                }
                face.V1 = vertexMap[face.V1];

                if (!vertexMap.ContainsKey(face.V2))
                {
                    float vx = x ? -face.V2.X : face.V2.X;
                    float vy = y ? -face.V2.Y : face.V2.Y;
                    vertexMap[face.V2] = new Vector3(vx, vy, face.V2.Z);
                }
                face.V2 = vertexMap[face.V2];

                if (!vertexMap.ContainsKey(face.V3))
                {
                    float vx = x ? -face.V3.X : face.V3.X;
                    float vy = y ? -face.V3.Y : face.V3.Y;
                    vertexMap[face.V3] = new Vector3(vx, vy, face.V3.Z);
                }
                face.V3 = vertexMap[face.V3];
            }

            // Fix the face normals
            if (x != y)
            {
                foreach (var face in Faces)
                {
                    Vector3 v1 = face.V1;
                    Vector3 v2 = face.V2;
                    Vector3 v3 = face.V3;
                    face.V1 = v3;
                    face.V2 = v2;
                    face.V3 = v1;
                }
            }
        }
    }
}

