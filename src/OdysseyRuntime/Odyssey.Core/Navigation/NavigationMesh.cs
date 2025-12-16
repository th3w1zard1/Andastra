using System;
using System.Collections.Generic;
using System.Numerics;
using BioWareEngines.Core.Interfaces;

namespace BioWareEngines.Core.Navigation
{
    /// <summary>
    /// Navigation mesh for pathfinding and collision detection.
    /// Wraps BWM data from CSharpKOTOR with A* pathfinding on walkmesh adjacency.
    /// </summary>
    /// <remarks>
    /// Navigation/Walkmesh System:
    /// - Based on swkotor2.exe pathfinding/walkmesh system
    /// - Located via string references: "walkmesh" (pathfinding functions), "nwsareapathfind.cpp" @ 0x007be3ff
    /// - BWM file format: "BWM V1.0" @ 0x007c061c (BWM file signature)
    /// - Error messages:
    ///   - "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   - "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
    ///   - "ERROR: opening a Binary walkmesh file for writeing that already exists (File: %s)" @ 0x007c0630
    /// - Original implementation: BWM (BioWare Walkmesh) files contain triangle mesh with adjacency data
    /// - Based on BWM file format documentation in vendor/PyKotor/wiki/BWM-File-Format.md
    /// - BWM file structure: Header with "BWM V1.0" signature, vertex data, face data, adjacency data, AABB tree
    /// - Adjacency encoding: adjacency_index = face_index * 3 + edge_index, -1 = no neighbor
    /// - Surface materials determine walkability (0-30 range, lookup via surfacemat.2da)
    /// - Pathfinding uses A* algorithm on walkmesh adjacency graph
    /// - Grid-based pathfinding used for initial/terminal path segments when direct walkmesh path fails
    /// </remarks>
    public class NavigationMesh : INavigationMesh
    {
        private readonly Vector3[] _vertices;
        private readonly int[] _faceIndices;        // 3 vertex indices per face
        private readonly int[] _adjacency;          // 3 adjacency entries per face (-1 = no neighbor)
        private readonly int[] _surfaceMaterials;   // Material per face
        private readonly AabbNode _aabbRoot;
        private readonly int _faceCount;
        private readonly int _walkableFaceCount;

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
        /// Creates an empty navigation mesh (for placeholder use).
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
        /// </summary>
        public void BuildFromTriangles(List<Vector3> vertices, List<int> indices)
        {
            // Note: This mutates the mesh which should ideally be immutable
            // For placeholder use only
        }

        /// <summary>
        /// Performs a raycast and returns the hit point (simplified overload).
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
                // Same face - direct path
                return new List<Vector3> { start, goal };
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

            return null;  // No path found
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

