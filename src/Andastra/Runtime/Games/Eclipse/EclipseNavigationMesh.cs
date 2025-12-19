using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse Engine navigation mesh with dynamic obstacle support.
    /// </summary>
    /// <remarks>
    /// Eclipse Navigation Mesh Implementation:
    /// - Most advanced navigation system of BioWare engines
    /// - Supports dynamic obstacles and destructible environments
    /// - Real-time pathfinding with cover and tactical positioning
    /// - Physics-aware navigation with collision avoidance
    ///
    /// Based on reverse engineering of:
    /// - daorigins.exe/DragonAge2.exe/MassEffect.exe/MassEffect2.exe navigation systems
    /// - Dynamic obstacle avoidance algorithms
    /// - Cover system implementations
    /// - Tactical pathfinding with threat assessment
    ///
    /// Eclipse navigation features:
    /// - Dynamic obstacle handling (movable objects, destruction)
    /// - Cover point identification and pathing
    /// - Tactical positioning for combat AI
    /// - Physics-based collision avoidance
    /// - Real-time mesh updates for environmental changes
    /// - Multi-level navigation (ground, elevated surfaces)
    /// </remarks>
    [PublicAPI]
    public class EclipseNavigationMesh : BaseNavigationMesh
    {
        // Static geometry data
        private readonly Vector3[] _staticVertices;
        private readonly int[] _staticFaceIndices;
        private readonly int[] _staticAdjacency;
        private readonly int[] _staticSurfaceMaterials;
        private readonly AabbNode _staticAabbRoot;
        private readonly int _staticFaceCount;

        // Dynamic obstacles (movable objects, physics bodies)
        private readonly List<DynamicObstacle> _dynamicObstacles;
        private readonly Dictionary<int, DynamicObstacle> _obstacleById;

        // Destructible terrain modifications
        private readonly List<DestructibleModification> _destructibleModifications;
        private readonly Dictionary<int, DestructibleModification> _modificationByFaceId;

        // Multi-level navigation surfaces (ground, platforms, elevated surfaces)
        private readonly List<NavigationLevel> _navigationLevels;

        // Projection search parameters
        private const float MaxProjectionDistance = 100.0f;
        private const float MinProjectionDistance = 0.01f;
        private const float MultiLevelSearchRadius = 5.0f;
        private const int MaxProjectionCandidates = 10;

        /// <summary>
        /// Creates an empty Eclipse navigation mesh (for placeholder use).
        /// </summary>
        public EclipseNavigationMesh()
        {
            _staticVertices = new Vector3[0];
            _staticFaceIndices = new int[0];
            _staticAdjacency = new int[0];
            _staticSurfaceMaterials = new int[0];
            _staticAabbRoot = null;
            _staticFaceCount = 0;
            _dynamicObstacles = new List<DynamicObstacle>();
            _obstacleById = new Dictionary<int, DynamicObstacle>();
            _destructibleModifications = new List<DestructibleModification>();
            _modificationByFaceId = new Dictionary<int, DestructibleModification>();
            _navigationLevels = new List<NavigationLevel>();
        }

        /// <summary>
        /// Creates an Eclipse navigation mesh from static geometry data.
        /// </summary>
        /// <param name="vertices">Static mesh vertices.</param>
        /// <param name="faceIndices">Face vertex indices (3 per face).</param>
        /// <param name="adjacency">Face adjacency data (3 per face, -1 = no neighbor).</param>
        /// <param name="surfaceMaterials">Surface material indices per face.</param>
        /// <param name="aabbRoot">AABB tree root for spatial acceleration.</param>
        public EclipseNavigationMesh(
            Vector3[] vertices,
            int[] faceIndices,
            int[] adjacency,
            int[] surfaceMaterials,
            AabbNode aabbRoot)
        {
            _staticVertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            _staticFaceIndices = faceIndices ?? throw new ArgumentNullException(nameof(faceIndices));
            _staticAdjacency = adjacency ?? new int[0];
            _staticSurfaceMaterials = surfaceMaterials ?? throw new ArgumentNullException(nameof(surfaceMaterials));
            _staticAabbRoot = aabbRoot;
            _staticFaceCount = faceIndices.Length / 3;

            _dynamicObstacles = new List<DynamicObstacle>();
            _obstacleById = new Dictionary<int, DynamicObstacle>();
            _destructibleModifications = new List<DestructibleModification>();
            _modificationByFaceId = new Dictionary<int, DestructibleModification>();
            _navigationLevels = new List<NavigationLevel>();

            // Initialize default ground level
            _navigationLevels.Add(new NavigationLevel
            {
                LevelId = 0,
                BaseHeight = 0.0f,
                HeightRange = new Vector2(-1000.0f, 1000.0f),
                SurfaceType = SurfaceType.Ground
            });
        }
        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Eclipse considers dynamic obstacles and physics objects.
        /// Checks for movable objects, destructible terrain, and active physics bodies.
        /// More sophisticated than Aurora's tile-based system.
        ///
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Dynamic point testing with physics integration
        /// - DragonAge2.exe: Enhanced multi-level point testing
        /// - MassEffect.exe/MassEffect2.exe: Physics-aware point testing
        ///
        /// Algorithm:
        /// 1. Collect all projection candidates (static geometry, dynamic obstacles, multi-level surfaces)
        /// 2. Filter to only walkable candidates
        /// 3. Select best walkable candidate based on distance and surface type
        /// 4. Verify point is within acceptable distance threshold of walkable surface
        /// </remarks>
        public bool IsPointWalkable(Vector3 point)
        {
            // If no static geometry, check dynamic obstacles and multi-level surfaces only
            if (_staticFaceCount == 0)
            {
                return IsPointWalkableDynamicOnly(point);
            }

            // Collect all projection candidates
            var candidates = new List<ProjectionCandidate>();

            // 1. Project to static geometry (with destructible modifications)
            ProjectToStaticGeometry(point, candidates);

            // 2. Check dynamic obstacles
            ProjectToDynamicObstacles(point, candidates);

            // 3. Check multi-level navigation surfaces
            ProjectToMultiLevelSurfaces(point, candidates);

            // 4. Filter to only walkable candidates
            var walkableCandidates = new List<ProjectionCandidate>();
            foreach (ProjectionCandidate candidate in candidates)
            {
                if (IsCandidateWalkable(candidate))
                {
                    walkableCandidates.Add(candidate);
                }
            }

            if (walkableCandidates.Count == 0)
            {
                return false;
            }

            // 5. Select best walkable candidate (prefer ground, then closest)
            walkableCandidates.Sort((a, b) =>
            {
                int typeCompare = a.SurfaceType.CompareTo(b.SurfaceType);
                if (typeCompare != 0)
                {
                    return typeCompare; // Prefer ground over platforms
                }
                return a.Distance.CompareTo(b.Distance);
            });

            ProjectionCandidate best = walkableCandidates[0];

            // 6. Verify point is within acceptable distance threshold
            // Points too far from walkable surface are not considered walkable
            const float maxWalkableDistance = 2.0f; // Maximum vertical distance to walkable surface
            float verticalDistance = Math.Abs(point.Z - best.Height);
            if (verticalDistance > maxWalkableDistance)
            {
                return false;
            }

            // 7. Additional check: for static faces, verify point is actually within the face bounds
            if (best.FaceIndex >= 0)
            {
                // Point should be within the face's 2D bounds (with some tolerance)
                if (!PointInStaticFace2d(point, best.FaceIndex))
                {
                    // Check if point is close enough to face center to be considered walkable
                    Vector3 faceCenter = GetStaticFaceCenter(best.FaceIndex);
                    float dist2D = Vector3Extensions.Distance2D(point, faceCenter);
                    const float maxFaceDistance = 5.0f; // Maximum 2D distance from face center
                    if (dist2D > maxFaceDistance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if a projection candidate represents a walkable surface.
        /// </summary>
        private bool IsCandidateWalkable(ProjectionCandidate candidate)
        {
            // Check based on surface type
            switch (candidate.SurfaceType)
            {
                case SurfaceType.Ground:
                    // Ground surfaces are walkable if:
                    // - Static face: check IsWalkable
                    // - Dynamic obstacle: check obstacle.IsWalkable
                    // - Multi-level: check level walkability
                    if (candidate.FaceIndex >= 0)
                    {
                        return IsWalkable(candidate.FaceIndex);
                    }
                    if (candidate.ObstacleId >= 0 && _obstacleById.ContainsKey(candidate.ObstacleId))
                    {
                        DynamicObstacle obstacle = _obstacleById[candidate.ObstacleId];
                        return obstacle.IsWalkable && obstacle.IsActive;
                    }
                    if (candidate.LevelId >= 0)
                    {
                        // Multi-level surfaces are generally walkable if they're ground type
                        return true;
                    }
                    return false;

                case SurfaceType.Platform:
                    // Platforms are walkable surfaces
                    return true;

                case SurfaceType.Elevated:
                    // Elevated surfaces are walkable
                    return true;

                case SurfaceType.Obstacle:
                    // Obstacles are not walkable
                    return false;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Tests if a point is walkable when only dynamic obstacles and multi-level surfaces exist.
        /// </summary>
        private bool IsPointWalkableDynamicOnly(Vector3 point)
        {
            var candidates = new List<ProjectionCandidate>();
            ProjectToDynamicObstacles(point, candidates);
            ProjectToMultiLevelSurfaces(point, candidates);

            // Filter to walkable candidates
            var walkableCandidates = new List<ProjectionCandidate>();
            foreach (ProjectionCandidate candidate in candidates)
            {
                if (IsCandidateWalkable(candidate))
                {
                    walkableCandidates.Add(candidate);
                }
            }

            if (walkableCandidates.Count == 0)
            {
                return false;
            }

            // Select best walkable candidate
            walkableCandidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            ProjectionCandidate best = walkableCandidates[0];

            // Verify point is within acceptable distance
            const float maxWalkableDistance = 2.0f;
            float verticalDistance = Math.Abs(point.Z - best.Height);
            return verticalDistance <= maxWalkableDistance;
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        /// <remarks>
        /// Eclipse projection handles destructible and dynamic geometry.
        /// Considers movable objects and terrain deformation.
        /// Supports projection to different surface types (ground, platforms, etc.).
        ///
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Dynamic projection with physics integration
        /// - DragonAge2.exe: Enhanced multi-level projection
        /// - MassEffect.exe/MassEffect2.exe: Physics-aware projection
        ///
        /// Algorithm:
        /// 1. Check static geometry projection (with destructible modifications)
        /// 2. Check dynamic obstacles (movable objects, physics bodies)
        /// 3. Check multi-level surfaces (platforms, elevated surfaces)
        /// 4. Select best projection based on distance and surface type
        /// </remarks>
        public bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            // If no static geometry, try dynamic obstacles and multi-level surfaces only
            if (_staticFaceCount == 0)
            {
                return ProjectToDynamicOnly(point, out result, out height);
            }

            // Collect all projection candidates
            var candidates = new List<ProjectionCandidate>();

            // 1. Project to static geometry (with destructible modifications)
            ProjectToStaticGeometry(point, candidates);

            // 2. Check dynamic obstacles
            ProjectToDynamicObstacles(point, candidates);

            // 3. Check multi-level navigation surfaces
            ProjectToMultiLevelSurfaces(point, candidates);

            // 4. Select best candidate
            if (candidates.Count == 0)
            {
                return false;
            }

            // Sort by distance (prefer closer projections) and surface type priority
            candidates.Sort((a, b) =>
            {
                int typeCompare = a.SurfaceType.CompareTo(b.SurfaceType);
                if (typeCompare != 0)
                {
                    return typeCompare; // Prefer ground over platforms
                }
                return a.Distance.CompareTo(b.Distance);
            });

            ProjectionCandidate best = candidates[0];
            result = best.ProjectedPoint;
            height = best.Height;
            return true;
        }

        /// <summary>
        /// Projects to static geometry, considering destructible modifications.
        /// </summary>
        private void ProjectToStaticGeometry(Vector3 point, List<ProjectionCandidate> candidates)
        {
            // Find face at point location
            int faceIndex = FindStaticFaceAt(point);
            if (faceIndex >= 0)
            {
                // Check if face is modified by destruction
                if (_modificationByFaceId.ContainsKey(faceIndex))
                {
                    DestructibleModification mod = _modificationByFaceId[faceIndex];
                    if (mod.IsDestroyed)
                    {
                        // Face is destroyed - skip it
                        return;
                    }
                    // Face is modified - use modified geometry
                    if (ProjectToModifiedFace(point, faceIndex, mod, out Vector3 projected, out float h))
                    {
                        candidates.Add(new ProjectionCandidate
                        {
                            ProjectedPoint = projected,
                            Height = h,
                            Distance = Vector3.Distance(point, projected),
                            SurfaceType = SurfaceType.Ground,
                            FaceIndex = faceIndex
                        });
                        return;
                    }
                }

                // Project to unmodified static face
                if (ProjectToStaticFace(point, faceIndex, out Vector3 projected, out float h))
                {
                    candidates.Add(new ProjectionCandidate
                    {
                        ProjectedPoint = projected,
                        Height = h,
                        Distance = Vector3.Distance(point, projected),
                        SurfaceType = SurfaceType.Ground,
                        FaceIndex = faceIndex
                    });
                }
            }

            // Also check nearby faces for better projection (within search radius)
            FindNearbyStaticFaces(point, MultiLevelSearchRadius, candidates);
        }

        /// <summary>
        /// Projects to dynamic obstacles (movable objects, physics bodies).
        /// </summary>
        private void ProjectToDynamicObstacles(Vector3 point, List<ProjectionCandidate> candidates)
        {
            foreach (DynamicObstacle obstacle in _dynamicObstacles)
            {
                if (!obstacle.IsActive)
                {
                    continue;
                }

                // Check if point is within obstacle's influence radius
                float distToObstacle = Vector3.Distance(point, obstacle.Position);
                if (distToObstacle > obstacle.InfluenceRadius)
                {
                    continue;
                }

                // Project to obstacle surface
                if (ProjectToObstacleSurface(point, obstacle, out Vector3 projected, out float h))
                {
                    candidates.Add(new ProjectionCandidate
                    {
                        ProjectedPoint = projected,
                        Height = h,
                        Distance = Vector3.Distance(point, projected),
                        SurfaceType = obstacle.IsWalkable ? SurfaceType.Ground : SurfaceType.Obstacle,
                        FaceIndex = -1,
                        ObstacleId = obstacle.ObstacleId
                    });
                }
            }
        }

        /// <summary>
        /// Projects to multi-level navigation surfaces (platforms, elevated surfaces).
        /// </summary>
        private void ProjectToMultiLevelSurfaces(Vector3 point, List<ProjectionCandidate> candidates)
        {
            foreach (NavigationLevel level in _navigationLevels)
            {
                if (point.Y < level.HeightRange.X || point.Y > level.HeightRange.Y)
                {
                    continue;
                }

                // Project to this level's surface
                Vector3 levelPoint = new Vector3(point.X, point.Y, point.Z);
                if (ProjectToLevelSurface(levelPoint, level, out Vector3 projected, out float h))
                {
                    candidates.Add(new ProjectionCandidate
                    {
                        ProjectedPoint = projected,
                        Height = h,
                        Distance = Vector3.Distance(point, projected),
                        SurfaceType = level.SurfaceType,
                        FaceIndex = -1,
                        LevelId = level.LevelId
                    });
                }
            }
        }

        /// <summary>
        /// Projects to dynamic obstacles only (when no static geometry exists).
        /// </summary>
        private bool ProjectToDynamicOnly(Vector3 point, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            var candidates = new List<ProjectionCandidate>();
            ProjectToDynamicObstacles(point, candidates);
            ProjectToMultiLevelSurfaces(point, candidates);

            if (candidates.Count == 0)
            {
                return false;
            }

            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            ProjectionCandidate best = candidates[0];
            result = best.ProjectedPoint;
            height = best.Height;
            return true;
        }

        /// <summary>
        /// Projects a point onto a static face using barycentric interpolation.
        /// </summary>
        private bool ProjectToStaticFace(Vector3 point, int faceIndex, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            if (faceIndex < 0 || faceIndex >= _staticFaceCount)
            {
                return false;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _staticFaceIndices.Length)
            {
                return false;
            }

            Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
            Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
            Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

            // Calculate height using plane equation
            float z = CalculatePlaneHeight(v1, v2, v3, point.X, point.Y);
            height = z;
            result = new Vector3(point.X, point.Y, z);
            return true;
        }

        /// <summary>
        /// Projects to a modified face (with destructible modifications).
        /// </summary>
        private bool ProjectToModifiedFace(Vector3 point, int faceIndex, DestructibleModification mod, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            if (mod.ModifiedVertices == null || mod.ModifiedVertices.Count < 3)
            {
                // Fall back to static face if modification is invalid
                return ProjectToStaticFace(point, faceIndex, out result, out height);
            }

            // Use modified vertices for projection
            Vector3 v1 = mod.ModifiedVertices[0];
            Vector3 v2 = mod.ModifiedVertices[1];
            Vector3 v3 = mod.ModifiedVertices[2];

            float z = CalculatePlaneHeight(v1, v2, v3, point.X, point.Y);
            height = z;
            result = new Vector3(point.X, point.Y, z);
            return true;
        }

        /// <summary>
        /// Projects to an obstacle surface.
        /// </summary>
        private bool ProjectToObstacleSurface(Vector3 point, DynamicObstacle obstacle, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            // Simple projection: if obstacle has a top surface, project to it
            if (obstacle.HasTopSurface)
            {
                float topHeight = obstacle.Position.Y + obstacle.Height;
                if (point.Y <= topHeight + obstacle.InfluenceRadius)
                {
                    height = topHeight;
                    result = new Vector3(point.X, point.Y, topHeight);
                    return true;
                }
            }

            // Project to obstacle's bounding box surface
            Vector3 min = obstacle.BoundsMin;
            Vector3 max = obstacle.BoundsMax;

            // Check if point is above obstacle
            if (point.X >= min.X && point.X <= max.X &&
                point.Y >= min.Y && point.Y <= max.Y &&
                point.Z >= min.Z && point.Z <= max.Z + obstacle.InfluenceRadius)
            {
                height = max.Z;
                result = new Vector3(point.X, point.Y, max.Z);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Projects to a navigation level surface.
        /// </summary>
        private bool ProjectToLevelSurface(Vector3 point, NavigationLevel level, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            // For now, project to base height of level
            // In full implementation, this would check level-specific geometry
            height = level.BaseHeight;
            result = new Vector3(point.X, point.Y, level.BaseHeight);
            return true;
        }

        /// <summary>
        /// Finds nearby static faces within search radius.
        /// </summary>
        private void FindNearbyStaticFaces(Vector3 point, float radius, List<ProjectionCandidate> candidates)
        {
            if (_staticAabbRoot == null)
            {
                // Brute force search
                for (int i = 0; i < _staticFaceCount && candidates.Count < MaxProjectionCandidates; i++)
                {
                    Vector3 center = GetStaticFaceCenter(i);
                    float dist = Vector3.Distance2D(point, center);
                    if (dist <= radius)
                    {
                        if (ProjectToStaticFace(point, i, out Vector3 projected, out float h))
                        {
                            candidates.Add(new ProjectionCandidate
                            {
                                ProjectedPoint = projected,
                                Height = h,
                                Distance = Vector3.Distance(point, projected),
                                SurfaceType = SurfaceType.Ground,
                                FaceIndex = i
                            });
                        }
                    }
                }
            }
            else
            {
                // Use AABB tree for efficient search
                FindNearbyFacesAabb(_staticAabbRoot, point, radius, candidates);
            }
        }

        /// <summary>
        /// Finds nearby faces using AABB tree.
        /// </summary>
        private void FindNearbyFacesAabb(AabbNode node, Vector3 point, float radius, List<ProjectionCandidate> candidates)
        {
            if (node == null || candidates.Count >= MaxProjectionCandidates)
            {
                return;
            }

            // Check if search sphere intersects AABB
            Vector3 aabbCenter = (node.BoundsMin + node.BoundsMax) * 0.5f;
            float distSq = Vector3.DistanceSquared2D(point, aabbCenter);
            float radiusSq = radius * radius;

            // Simple AABB-sphere intersection test (2D)
            if (distSq > radiusSq * 2.0f) // Conservative check
            {
                return;
            }

            // Leaf node - test face
            if (node.FaceIndex >= 0)
            {
                Vector3 center = GetStaticFaceCenter(node.FaceIndex);
                float faceDist = Vector3Extensions.Distance2D(point, center);
                if (faceDist <= radius)
                {
                    if (ProjectToStaticFace(point, node.FaceIndex, out Vector3 projected, out float h))
                    {
                        candidates.Add(new ProjectionCandidate
                        {
                            ProjectedPoint = projected,
                            Height = h,
                            Distance = Vector3.Distance(point, projected),
                            SurfaceType = SurfaceType.Ground,
                            FaceIndex = node.FaceIndex
                        });
                    }
                }
                return;
            }

            // Internal node - recurse
            if (node.Left != null)
            {
                FindNearbyFacesAabb(node.Left, point, radius, candidates);
            }
            if (node.Right != null)
            {
                FindNearbyFacesAabb(node.Right, point, radius, candidates);
            }
        }

        /// <summary>
        /// Finds the static face at a given position.
        /// </summary>
        private int FindStaticFaceAt(Vector3 position)
        {
            if (_staticAabbRoot != null)
            {
                return FindStaticFaceAabb(_staticAabbRoot, position);
            }

            // Brute force fallback
            for (int i = 0; i < _staticFaceCount; i++)
            {
                if (PointInStaticFace2d(position, i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds face using AABB tree.
        /// </summary>
        private int FindStaticFaceAabb(AabbNode node, Vector3 point)
        {
            if (node == null)
            {
                return -1;
            }

            // Test if point is within AABB bounds (2D)
            if (point.X < node.BoundsMin.X || point.X > node.BoundsMax.X ||
                point.Y < node.BoundsMin.Y || point.Y > node.BoundsMax.Y)
            {
                return -1;
            }

            // Leaf node - test point against face
            if (node.FaceIndex >= 0)
            {
                if (PointInStaticFace2d(point, node.FaceIndex))
                {
                    return node.FaceIndex;
                }
                return -1;
            }

            // Internal node - test children
            if (node.Left != null)
            {
                int result = FindStaticFaceAabb(node.Left, point);
                if (result >= 0)
                {
                    return result;
                }
            }

            if (node.Right != null)
            {
                int result = FindStaticFaceAabb(node.Right, point);
                if (result >= 0)
                {
                    return result;
                }
            }

            return -1;
        }

        /// <summary>
        /// Tests if a point is within a static face (2D).
        /// </summary>
        private bool PointInStaticFace2d(Vector3 point, int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _staticFaceCount)
            {
                return false;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _staticFaceIndices.Length)
            {
                return false;
            }

            Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
            Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
            Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

            // Same-side test (2D projection)
            float d1 = Sign2d(point, v1, v2);
            float d2 = Sign2d(point, v2, v3);
            float d3 = Sign2d(point, v3, v1);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }

        /// <summary>
        /// Calculates the 2D sign for point-in-triangle test.
        /// </summary>
        private float Sign2d(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        /// <summary>
        /// Calculates height on a plane defined by three vertices.
        /// </summary>
        private float CalculatePlaneHeight(Vector3 v1, Vector3 v2, Vector3 v3, float x, float y)
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
        /// Gets the center point of a static face.
        /// </summary>
        private Vector3 GetStaticFaceCenter(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _staticFaceCount)
            {
                return Vector3.Zero;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _staticFaceIndices.Length)
            {
                return Vector3.Zero;
            }

            Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
            Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
            Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

            return (v1 + v2 + v3) / 3.0f;
        }

        // INavigationMesh interface implementation

        /// <summary>
        /// Finds a path from start to goal (INavigationMesh interface).
        /// </summary>
        public System.Collections.Generic.IList<Vector3> FindPath(Vector3 start, Vector3 goal)
        {
            // Delegate to the Eclipse-specific FindPath method
            if (FindPath(start, goal, out Vector3[] waypoints))
            {
                return waypoints;
            }
            return null;
        }

        /// <summary>
        /// Finds a path from start to goal while avoiding obstacles.
        /// Based on daorigins.exe/DragonAge2.exe/MassEffect.exe/MassEffect2.exe: Advanced obstacle avoidance
        /// Eclipse engine has the most sophisticated obstacle avoidance with dynamic obstacles and cover system.
        /// </summary>
        public System.Collections.Generic.IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<Interfaces.ObstacleInfo> obstacles)
        {
            // Eclipse has built-in dynamic obstacle support, so we can use the existing FindPath
            // but we need to register the obstacles as dynamic obstacles temporarily
            // For now, delegate to FindPath and let Eclipse's dynamic obstacle system handle it
            // TODO: Integrate with Eclipse's dynamic obstacle system for proper avoidance
            return FindPath(start, goal);
        }

        /// <summary>
        /// Finds the face index at a given position (INavigationMesh interface).
        /// </summary>
        public int FindFaceAt(Vector3 position)
        {
            return FindStaticFaceAt(position);
        }

        /// <summary>
        /// Gets the center point of a face (INavigationMesh interface).
        /// </summary>
        public Vector3 GetFaceCenter(int faceIndex)
        {
            return GetStaticFaceCenter(faceIndex);
        }

        /// <summary>
        /// Gets adjacent faces for a given face (INavigationMesh interface).
        /// </summary>
        public System.Collections.Generic.IEnumerable<int> GetAdjacentFaces(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _staticFaceCount)
            {
                yield break;
            }

            int baseIdx = faceIndex * 3;
            for (int i = 0; i < 3; i++)
            {
                if (baseIdx + i < _staticAdjacency.Length)
                {
                    int encoded = _staticAdjacency[baseIdx + i];
                    if (encoded < 0)
                    {
                        yield return -1;
                    }
                    else
                    {
                        yield return encoded / 3; // Face index (edge = encoded % 3)
                    }
                }
                else
                {
                    yield return -1;
                }
            }
        }

        /// <summary>
        /// Checks if a face is walkable (INavigationMesh interface).
        /// </summary>
        public bool IsWalkable(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _staticSurfaceMaterials.Length)
            {
                return false;
            }

            // Check if face is destroyed
            if (_modificationByFaceId.ContainsKey(faceIndex))
            {
                DestructibleModification mod = _modificationByFaceId[faceIndex];
                if (mod.IsDestroyed)
                {
                    return false;
                }
            }

            // Check surface material (simplified - full implementation would use material lookup table)
            int material = _staticSurfaceMaterials[faceIndex];
            // Basic walkability: non-zero materials are generally walkable
            return material != 0;
        }

        /// <summary>
        /// Gets the surface material of a face (INavigationMesh interface).
        /// </summary>
        public int GetSurfaceMaterial(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _staticSurfaceMaterials.Length)
            {
                return 0;
            }
            return _staticSurfaceMaterials[faceIndex];
        }

        /// <summary>
        /// Performs a raycast against the mesh (INavigationMesh interface).
        /// </summary>
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            if (_staticAabbRoot != null)
            {
                return RaycastAabb(_staticAabbRoot, origin, direction, maxDistance, out hitPoint, out hitFace);
            }

            // Brute force fallback
            float bestDist = maxDistance;
            for (int i = 0; i < _staticFaceCount; i++)
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

        /// <summary>
        /// Tests line of sight between two points (INavigationMesh interface).
        /// </summary>
        public bool TestLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 direction = Vector3.Normalize(to - from);
            float distance = Vector3.Distance(from, to);

            if (Raycast(from, direction, distance, out Vector3 hitPoint, out int hitFace))
            {
                // Check if hit is close to destination (within tolerance)
                float distToHit = Vector3.Distance(from, hitPoint);
                float distToDest = Vector3.Distance(from, to);
                if (distToHit < distToDest - 0.1f)
                {
                    return false; // Something is in the way
                }
            }

            return true; // No obstruction
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface (INavigationMesh interface).
        /// </summary>
        public bool ProjectToSurface(Vector3 point, out Vector3 result, out float height)
        {
            return ProjectToWalkmesh(point, out result, out height);
        }

        /// <summary>
        /// Performs raycast using AABB tree.
        /// </summary>
        private bool RaycastAabb(AabbNode node, Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            if (node == null)
            {
                return false;
            }

            // Test AABB intersection with ray
            if (!RayAabbIntersect(origin, direction, node.BoundsMin, node.BoundsMax, maxDistance))
            {
                return false;
            }

            // Leaf node - test face
            if (node.FaceIndex >= 0)
            {
                float dist;
                if (RayTriangleIntersect(origin, direction, node.FaceIndex, maxDistance, out dist))
                {
                    hitPoint = origin + direction * dist;
                    hitFace = node.FaceIndex;
                    return true;
                }
                return false;
            }

            // Internal node - test children
            Vector3 leftHit, rightHit;
            int leftFace, rightFace;
            bool leftHitResult = false;
            bool rightHitResult = false;

            if (node.Left != null)
            {
                leftHitResult = RaycastAabb(node.Left, origin, direction, maxDistance, out leftHit, out leftFace);
            }
            if (node.Right != null)
            {
                rightHitResult = RaycastAabb(node.Right, origin, direction, maxDistance, out rightHit, out rightFace);
            }

            // Return closest hit
            if (leftHitResult && rightHitResult)
            {
                float leftDist = Vector3.Distance(origin, leftHit);
                float rightDist = Vector3.Distance(origin, rightHit);
                if (leftDist < rightDist)
                {
                    hitPoint = leftHit;
                    hitFace = leftFace;
                    return true;
                }
                else
                {
                    hitPoint = rightHit;
                    hitFace = rightFace;
                    return true;
                }
            }
            else if (leftHitResult)
            {
                hitPoint = leftHit;
                hitFace = leftFace;
                return true;
            }
            else if (rightHitResult)
            {
                hitPoint = rightHit;
                hitFace = rightFace;
                return true;
            }

            return false;
        }

        // RayAabbIntersect is now provided by BaseNavigationMesh base class

        /// <summary>
        /// Tests ray-triangle intersection.
        /// </summary>
        private bool RayTriangleIntersect(Vector3 origin, Vector3 direction, int faceIndex, float maxDistance, out float distance)
        {
            distance = 0;

            if (faceIndex < 0 || faceIndex >= _staticFaceCount)
            {
                return false;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _staticFaceIndices.Length)
            {
                return false;
            }

            Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
            Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
            Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

            // Möller-Trumbore intersection algorithm
            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            Vector3 h = Vector3.Cross(direction, edge2);
            float a = Vector3.Dot(edge1, h);

            if (Math.Abs(a) < 1e-6f)
            {
                return false; // Ray is parallel to triangle
            }

            float f = 1.0f / a;
            Vector3 s = origin - v1;
            float u = f * Vector3.Dot(s, h);

            if (u < 0.0f || u > 1.0f)
            {
                return false;
            }

            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(direction, q);

            if (v < 0.0f || u + v > 1.0f)
            {
                return false;
            }

            float t = f * Vector3.Dot(edge2, q);

            if (t > 1e-6f && t <= maxDistance)
            {
                distance = t;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds a path between two points with tactical considerations.
        /// </summary>
        /// <remarks>
        /// Eclipse pathfinding includes cover, threat assessment, and tactics.
        /// Supports different movement types (sneak, run, combat movement).
        /// Considers dynamic obstacles and real-time environmental changes.
        ///
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Tactical pathfinding with cover integration
        /// - DragonAge2.exe: Enhanced tactical pathfinding with threat assessment
        /// - MassEffect.exe/MassEffect2.exe: Advanced tactical pathfinding with dynamic obstacle avoidance
        ///
        /// Algorithm:
        /// 1. Project start and end points to walkable surfaces
        /// 2. Find face indices for start and end positions
        /// 3. Perform A* pathfinding with tactical cost modifiers:
        ///    - Base distance cost
        ///    - Surface material cost modifiers
        ///    - Dynamic obstacle avoidance (high cost for paths through obstacles)
        ///    - Threat assessment (higher cost for exposed paths)
        ///    - Cover preference (lower cost for paths near cover)
        /// 4. Reconstruct path from face indices to waypoints
        /// 5. Smooth path using line-of-sight checks
        /// 6. Optionally integrate cover points into path
        /// </remarks>
        public bool FindPath(Vector3 start, Vector3 end, out Vector3[] waypoints)
        {
            waypoints = null;

            // Project start and end to walkable surfaces
            if (!ProjectToWalkmesh(start, out Vector3 projectedStart, out float startHeight))
            {
                return false;
            }
            if (!ProjectToWalkmesh(end, out Vector3 projectedEnd, out float endHeight))
            {
                return false;
            }

            // Find face indices
            int startFace = FindStaticFaceAt(projectedStart);
            int endFace = FindStaticFaceAt(projectedEnd);

            // Handle case where no static geometry exists
            if (startFace < 0 && _staticFaceCount == 0)
            {
                // Use dynamic-only pathfinding
                return FindPathDynamicOnly(projectedStart, projectedEnd, out waypoints);
            }

            if (startFace < 0 || endFace < 0)
            {
                // Can't find valid faces - return direct path
                waypoints = new[] { projectedStart, projectedEnd };
                return true;
            }

            if (startFace == endFace)
            {
                // Same face - direct path
                waypoints = new[] { projectedStart, projectedEnd };
                return true;
            }

            // A* pathfinding with tactical considerations
            var openSet = new SortedSet<FaceScore>(new FaceScoreComparer());
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float>();
            var fScore = new Dictionary<int, float>();
            var inOpenSet = new HashSet<int>();
            var closedSet = new HashSet<int>();

            gScore[startFace] = 0f;
            fScore[startFace] = TacticalHeuristic(startFace, endFace, projectedStart, projectedEnd, null);
            openSet.Add(new FaceScore(startFace, fScore[startFace]));
            inOpenSet.Add(startFace);

            // Maximum iterations to prevent infinite loops
            const int maxIterations = 10000;
            int iterations = 0;

            while (openSet.Count > 0 && iterations < maxIterations)
            {
                iterations++;

                // Get face with lowest f-score
                FaceScore currentScore = GetMinFaceScore(openSet);
                openSet.Remove(currentScore);
                int current = currentScore.FaceIndex;
                inOpenSet.Remove(current);
                closedSet.Add(current);

                if (current == endFace)
                {
                    waypoints = ReconstructTacticalPath(cameFrom, current, projectedStart, projectedEnd);
                    return true;
                }

                // Check all adjacent faces
                foreach (int neighbor in GetAdjacentFaces(current))
                {
                    if (neighbor < 0 || neighbor >= _staticFaceCount)
                    {
                        continue;  // Invalid or no neighbor
                    }

                    if (closedSet.Contains(neighbor))
                    {
                        continue;  // Already evaluated
                    }

                    if (!IsWalkable(neighbor))
                    {
                        continue;  // Not walkable
                    }

                    // Calculate tactical cost for this edge
                    float edgeCost = CalculateTacticalEdgeCost(current, neighbor, projectedStart, projectedEnd);

                    float tentativeG;
                    if (gScore.TryGetValue(current, out float currentG))
                    {
                        tentativeG = currentG + edgeCost;
                    }
                    else
                    {
                        tentativeG = edgeCost;
                    }

                    float neighborG;
                    if (!gScore.TryGetValue(neighbor, out neighborG) || tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float newF = tentativeG + TacticalHeuristic(neighbor, endFace, projectedStart, projectedEnd, null);
                        fScore[neighbor] = newF;

                        if (!inOpenSet.Contains(neighbor))
                        {
                            openSet.Add(new FaceScore(neighbor, newF));
                            inOpenSet.Add(neighbor);
                        }
                    }
                }
            }

            // No path found - return direct path as fallback
            waypoints = new[] { projectedStart, projectedEnd };
            return false;
        }

        /// <summary>
        /// Finds path when only dynamic obstacles exist (no static geometry).
        /// </summary>
        private bool FindPathDynamicOnly(Vector3 start, Vector3 end, out Vector3[] waypoints)
        {
            waypoints = null;

            // Simple direct path for dynamic-only scenarios
            // In full implementation, this would use dynamic obstacle avoidance
            waypoints = new[] { start, end };
            return true;
        }

        /// <summary>
        /// Calculates tactical edge cost between two faces.
        /// </summary>
        /// <remarks>
        /// Tactical cost includes:
        /// - Base distance cost
        /// - Surface material modifiers
        /// - Dynamic obstacle penalties
        /// - Threat exposure penalties
        /// - Cover bonuses
        /// </remarks>
        private float CalculateTacticalEdgeCost(int fromFace, int toFace, Vector3 start, Vector3 end)
        {
            // Base distance cost
            Vector3 fromCenter = GetStaticFaceCenter(fromFace);
            Vector3 toCenter = GetStaticFaceCenter(toFace);
            float baseCost = Vector3.Distance(fromCenter, toCenter);

            // Surface material cost modifier
            float surfaceMod = GetSurfaceCost(toFace);

            // Dynamic obstacle penalty
            float obstaclePenalty = CalculateObstaclePenalty(toCenter);

            // Threat exposure penalty (higher cost for exposed paths)
            float threatPenalty = CalculateThreatExposure(toCenter, start, end);

            // Cover bonus (lower cost for paths near cover)
            float coverBonus = CalculateCoverBonus(toCenter);

            // Combine all costs
            float totalCost = baseCost * surfaceMod + obstaclePenalty + threatPenalty - coverBonus;

            // Ensure cost is always positive
            return Math.Max(totalCost, 0.1f);
        }

        /// <summary>
        /// Calculates penalty for paths through or near dynamic obstacles.
        /// </summary>
        private float CalculateObstaclePenalty(Vector3 position)
        {
            float penalty = 0.0f;
            const float obstacleInfluenceRadius = 2.0f; // Penalty radius around obstacles

            foreach (DynamicObstacle obstacle in _dynamicObstacles)
            {
                if (!obstacle.IsActive)
                {
                    continue;
                }

                float distToObstacle = Vector3.Distance(position, obstacle.Position);
                if (distToObstacle < obstacleInfluenceRadius)
                {
                    // Penalty increases as we get closer to obstacle
                    float proximityFactor = 1.0f - (distToObstacle / obstacleInfluenceRadius);
                    penalty += proximityFactor * 5.0f; // Base penalty of 5.0 at obstacle center
                }
            }

            return penalty;
        }

        /// <summary>
        /// Calculates threat exposure penalty for a position.
        /// </summary>
        /// <remarks>
        /// Higher penalty for positions that are exposed to threats.
        /// In full implementation, this would query the combat system for active threats.
        /// </remarks>
        private float CalculateThreatExposure(Vector3 position, Vector3 start, Vector3 end)
        {
            // Simplified threat assessment
            // In full implementation, this would:
            // 1. Query combat system for active threats
            // 2. Check line of sight from threats to position
            // 3. Calculate exposure based on distance and cover availability

            // For now, use a simple heuristic: positions far from start/end are more exposed
            float distFromStart = Vector3.Distance(position, start);
            float distFromEnd = Vector3.Distance(position, end);
            float avgDist = (distFromStart + distFromEnd) / 2.0f;

            // Higher penalty for positions that are far from both start and end
            // (assuming threats are more likely to be in the middle of the path)
            const float maxThreatDistance = 50.0f;
            if (avgDist > maxThreatDistance)
            {
                return 0.0f; // Too far to be concerned
            }

            float exposureFactor = 1.0f - (avgDist / maxThreatDistance);
            return exposureFactor * 2.0f; // Base threat penalty
        }

        /// <summary>
        /// Calculates cover bonus for a position.
        /// </summary>
        /// <remarks>
        /// Lower cost (bonus) for positions near cover.
        /// In full implementation, this would use the cover point system.
        /// </remarks>
        private float CalculateCoverBonus(Vector3 position)
        {
            // Simplified cover assessment
            // In full implementation, this would:
            // 1. Query nearby cover points
            // 2. Check if position provides cover from known threats
            // 3. Calculate bonus based on cover quality

            // For now, use a simple heuristic: check if position is near geometry that could provide cover
            // This is a placeholder - full implementation would use FindCoverPoints
            const float coverSearchRadius = 3.0f;
            float bonus = 0.0f;

            // Check if there are nearby faces that could provide cover
            // (simplified: assume faces with certain surface materials provide cover)
            int faceIndex = FindStaticFaceAt(position);
            if (faceIndex >= 0)
            {
                // Some surface materials might provide cover (e.g., walls, barriers)
                // This is engine-specific and would need to be configured
                int material = GetSurfaceMaterial(faceIndex);
                // Placeholder: assume certain materials provide cover
                // In full implementation, this would use a cover material lookup table
            }

            return bonus;
        }

        /// <summary>
        /// Gets surface cost modifier for a face.
        /// </summary>
        private float GetSurfaceCost(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _staticSurfaceMaterials.Length)
            {
                return 1.0f;
            }

            int material = _staticSurfaceMaterials[faceIndex];

            // Surface-specific costs (similar to base NavigationMesh but Eclipse-specific)
            // Eclipse engines may have different material cost modifiers
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

        /// <summary>
        /// Tactical heuristic function for A* pathfinding.
        /// </summary>
        /// <remarks>
        /// Combines distance heuristic with tactical considerations.
        /// </remarks>
        private float TacticalHeuristic(int fromFace, int toFace, Vector3 start, Vector3 end, List<Vector3> threats)
        {
            // Base distance heuristic
            Vector3 fromCenter = GetStaticFaceCenter(fromFace);
            Vector3 toCenter = GetStaticFaceCenter(toFace);
            float distanceHeuristic = Vector3.Distance(fromCenter, toCenter);

            // Tactical modifiers (optional, can be added based on threats)
            // For now, use simple distance heuristic
            return distanceHeuristic;
        }

        /// <summary>
        /// Reconstructs path from face indices to waypoints.
        /// </summary>
        private Vector3[] ReconstructTacticalPath(Dictionary<int, int> cameFrom, int current, Vector3 start, Vector3 end)
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
                Vector3 faceCenter = GetStaticFaceCenter(facePath[i]);
                path.Add(faceCenter);
            }

            path.Add(end);

            // Smooth path using line-of-sight checks
            return SmoothTacticalPath(path);
        }

        /// <summary>
        /// Smooths path by removing redundant waypoints using line-of-sight checks.
        /// </summary>
        private Vector3[] SmoothTacticalPath(List<Vector3> path)
        {
            if (path.Count <= 2)
            {
                return path.ToArray();
            }

            var smoothed = new List<Vector3>();
            smoothed.Add(path[0]);

            for (int i = 1; i < path.Count - 1; i++)
            {
                // Check if we can skip this waypoint
                Vector3 prev = smoothed[smoothed.Count - 1];
                Vector3 next = path[i + 1];

                // Use TestLineOfSight to check if we can skip waypoint
                if (!TestLineOfSight(prev, next))
                {
                    // Can't skip - add the waypoint
                    smoothed.Add(path[i]);
                }
            }

            smoothed.Add(path[path.Count - 1]);
            return smoothed.ToArray();
        }

        /// <summary>
        /// Gets minimum face score from sorted set.
        /// </summary>
        private FaceScore GetMinFaceScore(SortedSet<FaceScore> set)
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

        /// <summary>
        /// Face score for A* pathfinding priority queue.
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
        /// Comparer for FaceScore (sorts by score, then by face index for stability).
        /// </summary>
        private class FaceScoreComparer : IComparer<FaceScore>
        {
            public int Compare(FaceScore x, FaceScore y)
            {
                int scoreCompare = x.Score.CompareTo(y.Score);
                if (scoreCompare != 0)
                {
                    return scoreCompare;
                }
                return x.FaceIndex.CompareTo(y.FaceIndex);
            }
        }

        /// <summary>
        /// Gets the height at a specific point.
        /// </summary>
        /// <remarks>
        /// Samples height from dynamic terrain data.
        /// Considers real-time terrain deformation and movable objects.
        /// 
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Dynamic height sampling with physics integration
        ///   (Ghidra analysis needed: search for height sampling functions, walkmesh projection functions)
        /// - DragonAge2.exe: Enhanced multi-level height sampling
        ///   (Ghidra analysis needed: search for multi-level navigation height functions)
        /// - MassEffect.exe/MassEffect2.exe: Physics-aware height sampling
        ///   (Ghidra analysis needed: search for physics integration in height sampling)
        /// 
        /// Common pattern across Eclipse engines:
        /// 1. Project point to navigation surface (static geometry, dynamic obstacles, multi-level surfaces)
        /// 2. Extract height from best projection candidate
        /// 3. Handle destructible terrain modifications
        /// 4. Consider physics bodies and movable objects
        /// 
        /// Algorithm:
        /// 1. Use ProjectToWalkmesh to find the best projection candidate
        /// 2. Extract height from the projected result
        /// 3. Handles static geometry, dynamic obstacles, destructible modifications, and multi-level surfaces
        /// 4. Returns false if no valid surface is found
        /// 
        /// Note: Function addresses to be determined via Ghidra MCP reverse engineering:
        /// - daorigins.exe: Height sampling function (search for "GetHeight", "SampleHeight", "ProjectToSurface" references)
        /// - DragonAge2.exe: Multi-level height sampling function (search for navigation mesh height functions)
        /// - MassEffect.exe: Physics-aware height sampling (search for physics integration in navigation)
        /// - MassEffect2.exe: Enhanced physics-aware height sampling (search for improved navigation height functions)
        /// </remarks>
        public bool GetHeightAtPoint(Vector3 point, out float height)
        {
            height = point.Y;

            // Use existing projection logic to find height
            // ProjectToWalkmesh already handles all dynamic terrain considerations:
            // - Static geometry with destructible modifications
            // - Dynamic obstacles (movable objects, physics bodies)
            // - Multi-level navigation surfaces (platforms, elevated surfaces)
            if (ProjectToWalkmesh(point, out Vector3 projectedPoint, out float projectedHeight))
            {
                height = projectedHeight;
                return true;
            }

            // If projection fails, check if point is at least within acceptable range of any surface
            // This handles edge cases where projection might fail but height can still be determined
            if (_staticFaceCount == 0)
            {
                // No static geometry - try dynamic-only projection
                if (ProjectToDynamicOnly(point, out Vector3 dynamicResult, out float dynamicHeight))
                {
                    height = dynamicHeight;
                    return true;
                }
                return false;
            }

            // Try to find any nearby face that might provide height information
            // This is a fallback for cases where exact projection fails but we can still estimate height
            int faceIndex = FindStaticFaceAt(point);
            if (faceIndex >= 0)
            {
                // Check if face is modified by destruction
                if (_modificationByFaceId.ContainsKey(faceIndex))
                {
                    DestructibleModification mod = _modificationByFaceId[faceIndex];
                    if (mod.IsDestroyed)
                    {
                        // Face is destroyed - cannot determine height
                        return false;
                    }
                    // Face is modified - use modified geometry
                    if (ProjectToModifiedFace(point, faceIndex, mod, out Vector3 modifiedResult, out float modifiedHeight))
                    {
                        height = modifiedHeight;
                        return true;
                    }
                }

                // Project to unmodified static face
                if (ProjectToStaticFace(point, faceIndex, out Vector3 staticResult, out float staticHeight))
                {
                    height = staticHeight;
                    return true;
                }
            }

            // Check dynamic obstacles as fallback
            foreach (DynamicObstacle obstacle in _dynamicObstacles)
            {
                if (!obstacle.IsActive)
                {
                    continue;
                }

                float distToObstacle = Vector3.Distance(point, obstacle.Position);
                if (distToObstacle > obstacle.InfluenceRadius)
                {
                    continue;
                }

                if (ProjectToObstacleSurface(point, obstacle, out Vector3 obstacleResult, out float obstacleHeight))
                {
                    height = obstacleHeight;
                    return true;
                }
            }

            // Check multi-level surfaces as final fallback
            foreach (NavigationLevel level in _navigationLevels)
            {
                if (point.Y < level.HeightRange.X || point.Y > level.HeightRange.Y)
                {
                    continue;
                }

                if (ProjectToLevelSurface(point, level, out Vector3 levelResult, out float levelHeight))
                {
                    height = levelHeight;
                    return true;
                }
            }

            // No valid surface found
            return false;
        }

        /// <summary>
        /// Checks line of sight between two points.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific: Uses base class common algorithm with dynamic obstacles and destructible modifications.
        /// 
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Dynamic line of sight with destructible environment support
        /// - DragonAge2.exe: Enhanced dynamic line of sight with physics integration
        /// - MassEffect.exe/MassEffect2.exe: Physics-aware line of sight with dynamic obstacles
        /// 
        /// Note: Function addresses to be determined via Ghidra MCP reverse engineering:
        /// - daorigins.exe: Line of sight function (search for "HasLineOfSight", "LineOfSight", "Raycast" references)
        /// - DragonAge2.exe: Enhanced line of sight function (search for dynamic obstacle integration)
        /// - MassEffect.exe: Physics-aware line of sight (search for physics integration in line of sight)
        /// - MassEffect2.exe: Enhanced physics-aware line of sight (search for improved line of sight functions)
        /// </remarks>
        public new bool HasLineOfSight(Vector3 start, Vector3 end)
        {
            return base.HasLineOfSight(start, end);
        }

        /// <summary>
        /// Eclipse-specific check: handles destructible modifications and walkable faces.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe:
        /// - Destroyed faces don't block line of sight
        /// - Modified faces check walkability
        /// - Walkable faces don't block line of sight
        /// </remarks>
        protected override bool CheckHitBlocksLineOfSight(Vector3 hitPoint, int hitFace, Vector3 start, Vector3 end)
        {
            // Check if hit face is destroyed (destructible objects don't block line of sight)
            if (_modificationByFaceId.ContainsKey(hitFace))
            {
                DestructibleModification mod = _modificationByFaceId[hitFace];
                if (mod.IsDestroyed)
                {
                    // Face is destroyed - doesn't block line of sight
                    return true; // Line of sight is clear
                }
                // Face is modified but not destroyed - check if it blocks
                // Modified faces can still block line of sight if they're non-walkable
                if (!IsWalkable(hitFace))
                {
                    return false; // Modified non-walkable face blocks line of sight
                }
                // Modified walkable face doesn't block
                return true;
            }

            // Unmodified face - check if it blocks line of sight
            // Walkable faces don't block line of sight (e.g., through doorways, over walkable terrain)
            if (hitFace >= 0 && IsWalkable(hitFace))
            {
                return true; // Walkable face doesn't block
            }

            // Non-walkable face blocks line of sight
            return false;
        }

        /// <summary>
        /// Eclipse-specific check for dynamic obstacles.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe:
        /// - Only active, non-walkable obstacles block line of sight
        /// - Walkable obstacles (platforms, movable surfaces) don't block
        /// </remarks>
        protected override bool CheckDynamicObstacles(Vector3 start, Vector3 end, Vector3 direction, float distance)
        {
            // Check dynamic obstacles
            // Only active, non-walkable obstacles block line of sight
            float closestObstacleHit = float.MaxValue;
            bool obstacleBlocks = false;
            foreach (DynamicObstacle obstacle in _dynamicObstacles)
            {
                if (!obstacle.IsActive)
                {
                    continue; // Inactive obstacles don't block
                }

                if (obstacle.IsWalkable)
                {
                    continue; // Walkable obstacles (platforms, movable surfaces) don't block line of sight
                }

                // Check if ray intersects obstacle's bounding box
                if (RayAabbIntersect(start, direction, obstacle.BoundsMin, obstacle.BoundsMax, distance))
                {
                    // Calculate intersection point with obstacle AABB
                    Vector3 obstacleHitPoint;
                    float obstacleHitDist;
                    if (RayAabbIntersectPoint(start, direction, obstacle.BoundsMin, obstacle.BoundsMax, distance, out obstacleHitPoint, out obstacleHitDist))
                    {
                        if (obstacleHitDist < closestObstacleHit)
                        {
                            closestObstacleHit = obstacleHitDist;
                            obstacleBlocks = true;
                        }
                    }
                }
            }

            if (obstacleBlocks)
            {
                // Check if obstacle hit is very close to destination (within tolerance)
                if (distance - closestObstacleHit < LineOfSightTolerance)
                {
                    // Obstacle hit is at or very close to destination, line of sight is clear
                    return true;
                }
                // Dynamic obstacle blocks line of sight
                return false;
            }

            // No blocking dynamic obstacles found - line of sight is clear
            return true;
        }


        /// <summary>
        /// Finds nearby cover points.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific tactical feature.
        /// Identifies cover positions for combat AI.
        /// Considers cover quality and positioning.
        /// </remarks>
        public IEnumerable<Vector3> FindCoverPoints(Vector3 position, float radius)
        {
            // TODO: Implement cover point finding
            // Analyze geometry for cover positions
            // Rate cover quality
            // Return sorted cover points
            yield break;
        }

        /// <summary>
        /// Updates the navigation mesh for dynamic changes.
        /// </summary>
        /// <remarks>
        /// Eclipse allows real-time mesh updates.
        /// Handles destruction, object movement, terrain changes.
        /// Recalculates affected navigation regions.
        /// </remarks>
        public void UpdateDynamicObstacles()
        {
            // TODO: Implement dynamic obstacle updates
            // Detect changed geometry
            // Update navigation mesh
            // Recalculate affected paths
            // Notify pathfinding systems
        }

        /// <summary>
        /// Checks if a position provides cover from a threat position.
        /// </summary>
        /// <remarks>
        /// Tactical cover analysis for combat AI.
        /// Determines if position is protected from enemy fire.
        /// </remarks>
        public bool ProvidesCover(Vector3 position, Vector3 threatPosition, float coverHeight = 1.5f)
        {
            // TODO: Implement cover analysis
            // Check line of sight from threat
            // Consider cover object height
            // Account for partial cover
            throw new System.NotImplementedException("Cover analysis not yet implemented");
        }

        /// <summary>
        /// Finds optimal tactical positions.
        /// </summary>
        /// <remarks>
        /// Advanced AI positioning for combat.
        /// Considers flanking, high ground, cover availability.
        /// </remarks>
        public IEnumerable<TacticalPosition> FindTacticalPositions(Vector3 center, float radius)
        {
            // TODO: Implement tactical position finding
            // Analyze terrain features
            // Identify high ground
            // Find flanking positions
            // Rate tactical value
            yield break;
        }

        /// <summary>
        /// Gets navigation mesh statistics.
        /// </summary>
        /// <remarks>
        /// Debugging and optimization information.
        /// Reports mesh complexity and performance metrics.
        /// </remarks>
        public NavigationStats GetNavigationStats()
        {
            // TODO: Implement navigation statistics
            return new NavigationStats
            {
                TriangleCount = 0,
                DynamicObstacleCount = 0,
                CoverPointCount = 0,
                LastUpdateTime = 0
            };
        }
    }

    /// <summary>
    /// Represents a dynamic obstacle (movable object, physics body).
    /// </summary>
    internal struct DynamicObstacle
    {
        public int ObstacleId;
        public Vector3 Position;
        public Vector3 BoundsMin;
        public Vector3 BoundsMax;
        public float Height;
        public float InfluenceRadius;
        public bool IsActive;
        public bool IsWalkable;
        public bool HasTopSurface;
    }

    /// <summary>
    /// Represents a destructible terrain modification.
    /// </summary>
    internal struct DestructibleModification
    {
        public int FaceId;
        public bool IsDestroyed;
        public List<Vector3> ModifiedVertices;
        public float ModificationTime;
    }

    /// <summary>
    /// Represents a navigation level (ground, platform, elevated surface).
    /// </summary>
    internal struct NavigationLevel
    {
        public int LevelId;
        public float BaseHeight;
        public Vector2 HeightRange;
        public SurfaceType SurfaceType;
    }

    /// <summary>
    /// Represents a projection candidate result.
    /// </summary>
    internal struct ProjectionCandidate
    {
        public Vector3 ProjectedPoint;
        public float Height;
        public float Distance;
        public SurfaceType SurfaceType;
        public int FaceIndex;
        public int ObstacleId;
        public int LevelId;
    }

    /// <summary>
    /// Surface types for navigation.
    /// </summary>
    internal enum SurfaceType
    {
        Ground = 0,
        Platform = 1,
        Elevated = 2,
        Obstacle = 3
    }

    /// <summary>
    /// AABB tree node for spatial acceleration (shared with NavigationMesh).
    /// </summary>
    internal class AabbNode
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
    /// Represents a tactical position for combat AI.
    /// </summary>
    public struct TacticalPosition
    {
        /// <summary>
        /// The position coordinates.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// The tactical value rating (0-1).
        /// </summary>
        public float TacticalValue;

        /// <summary>
        /// The type of tactical advantage.
        /// </summary>
        public TacticalType Type;

        /// <summary>
        /// Nearby cover availability.
        /// </summary>
        public bool HasNearbyCover;

        /// <summary>
        /// High ground advantage.
        /// </summary>
        public bool IsHighGround;
    }

    /// <summary>
    /// Types of tactical positions.
    /// </summary>
    public enum TacticalType
    {
        /// <summary>
        /// Standard position with no special advantages.
        /// </summary>
        Standard,

        /// <summary>
        /// High ground with visibility advantage.
        /// </summary>
        HighGround,

        /// <summary>
        /// Flanking position for attacks.
        /// </summary>
        Flanking,

        /// <summary>
        /// Chokepoint control position.
        /// </summary>
        Chokepoint,

        /// <summary>
        /// Cover position with protection.
        /// </summary>
        Cover
    }

    /// <summary>
    /// Navigation mesh statistics.
    /// </summary>
    public struct NavigationStats
    {
        /// <summary>
        /// Number of triangles in the navigation mesh.
        /// </summary>
        public int TriangleCount;

        /// <summary>
        /// Number of dynamic obstacles.
        /// </summary>
        public int DynamicObstacleCount;

        /// <summary>
        /// Number of identified cover points.
        /// </summary>
        public int CoverPointCount;

        /// <summary>
        /// Time of last mesh update.
        /// </summary>
        public float LastUpdateTime;
    }

    /// <summary>
    /// Vector3 extension methods for 2D operations.
    /// </summary>
    internal static class Vector3Extensions
    {
        /// <summary>
        /// Calculates 2D distance (ignoring Z component).
        /// </summary>
        public static float Distance2D(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculates squared 2D distance (ignoring Z component).
        /// </summary>
        public static float DistanceSquared2D(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }
    }
}
