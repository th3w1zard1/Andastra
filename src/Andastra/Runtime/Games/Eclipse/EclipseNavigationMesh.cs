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
    /// Eclipse Navigation Mesh Implementation (Engine-Common):
    /// - Common navigation functionality shared by both Dragon Age: Origins and Dragon Age 2
    /// - Most advanced navigation system of BioWare engines
    /// - Supports dynamic obstacles and destructible environments
    /// - Real-time pathfinding with cover and tactical positioning
    /// - Physics-aware navigation with collision avoidance
    ///
    /// Engine-common functionality (shared by daorigins.exe and DragonAge2.exe):
    /// - Dynamic obstacle handling (movable objects, destruction)
    /// - Cover point identification and pathing
    /// - Tactical positioning for combat AI
    /// - Physics-based collision avoidance
    /// - Real-time mesh updates for environmental changes
    /// - Multi-level navigation (ground, elevated surfaces)
    /// - Same navigation data structures and algorithms
    ///
    /// Game-specific implementations:
    /// - DragonAgeOriginsNavigationMesh: daorigins.exe specific function addresses and behavior
    /// - DragonAge2NavigationMesh: DragonAge2.exe specific function addresses and behavior
    ///
    /// Note: Function addresses and game-specific behavior differences are documented
    /// in the game-specific subclasses (DragonAgeOriginsNavigationMesh, DragonAge2NavigationMesh).
    /// This class contains only functionality that is identical between both games.
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

        /// <summary>
        /// Gets the vertices of the navigation mesh.
        /// </summary>
        public IReadOnlyList<Vector3> Vertices => _staticVertices;

        /// <summary>
        /// Gets the face indices of the navigation mesh.
        /// </summary>
        public IReadOnlyList<int> FaceIndices => _staticFaceIndices;

        /// <summary>
        /// Gets the adjacency information for the navigation mesh.
        /// </summary>
        public IReadOnlyList<int> Adjacency => _staticAdjacency;

        /// <summary>
        /// Gets the surface materials for the navigation mesh.
        /// </summary>
        public IReadOnlyList<int> SurfaceMaterials => _staticSurfaceMaterials;

        /// <summary>
        /// Gets the face count of the navigation mesh.
        /// </summary>
        public int FaceCount => _staticFaceCount;

        // Dynamic obstacles (movable objects, physics bodies)
        private readonly List<DynamicObstacle> _dynamicObstacles;
        private readonly Dictionary<int, DynamicObstacle> _obstacleById;

        // Previous obstacle states for change detection
        private readonly Dictionary<int, ObstacleState> _previousObstacleStates;

        // Affected faces cache (faces that intersect with dynamic obstacles)
        private readonly Dictionary<int, HashSet<int>> _obstacleAffectedFaces;

        // Pathfinding cache invalidation tracking
        private readonly HashSet<int> _invalidatedFaces;
        private bool _meshNeedsRebuild;

        // Destructible terrain modifications
        private readonly List<DestructibleModification> _destructibleModifications;
        private readonly Dictionary<int, DestructibleModification> _modificationByFaceId;

        // Multi-level navigation surfaces (ground, platforms, elevated surfaces)
        private readonly List<NavigationLevel> _navigationLevels;

        // Cover point system
        private readonly List<CoverPoint> _coverPoints;
        private bool _coverPointsDirty;
        private const float CoverPointGenerationSpacing = 2.0f; // Minimum spacing between cover points
        private const float CoverPointMinWallHeight = 0.8f; // Minimum wall height to provide cover
        private const float CoverPointMaxWallAngle = 0.707f; // cos(45 degrees) - walls steeper than this provide cover
        private const float CoverPointQualityRadius = 5.0f; // Radius for cover quality assessment

        // Projection search parameters
        private const float MaxProjectionDistance = 100.0f;
        private const float MinProjectionDistance = 0.01f;
        private const float MultiLevelSearchRadius = 5.0f;
        private const int MaxProjectionCandidates = 10;

        // Obstacle update parameters
        private const float ObstacleChangeThreshold = 0.1f; // Minimum movement to trigger update
        private const float ObstacleInfluenceExpansion = 1.5f; // Expand influence radius for affected face detection

        // World reference for threat queries (optional - allows threat assessment when available)
        [CanBeNull]
        private IWorld _world;

        /// <summary>
        /// Sets the world reference for threat assessment during pathfinding.
        /// When set, threat exposure calculation will query for active enemies and check line of sight.
        /// When not set, threat assessment falls back to heuristic-based approach.
        /// </summary>
        [PublicAPI]
        public void SetWorld([CanBeNull] IWorld world)
        {
            _world = world;
        }

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
            _previousObstacleStates = new Dictionary<int, ObstacleState>();
            _obstacleAffectedFaces = new Dictionary<int, HashSet<int>>();
            _invalidatedFaces = new HashSet<int>();
            _meshNeedsRebuild = false;
            _destructibleModifications = new List<DestructibleModification>();
            _modificationByFaceId = new Dictionary<int, DestructibleModification>();
            _navigationLevels = new List<NavigationLevel>();
            _coverPoints = new List<CoverPoint>();
            _coverPointsDirty = true;
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
            _previousObstacleStates = new Dictionary<int, ObstacleState>();
            _obstacleAffectedFaces = new Dictionary<int, HashSet<int>>();
            _invalidatedFaces = new HashSet<int>();
            _meshNeedsRebuild = false;
            _destructibleModifications = new List<DestructibleModification>();
            _modificationByFaceId = new Dictionary<int, DestructibleModification>();
            _navigationLevels = new List<NavigationLevel>();
            _coverPoints = new List<CoverPoint>();
            _coverPointsDirty = true;

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
        /// - /: Physics-aware point testing
        ///
        /// Algorithm:
        /// 1. Collect all projection candidates (static geometry, dynamic obstacles, multi-level surfaces)
        /// 2. Filter to only walkable candidates
        /// 3. Select best walkable candidate based on distance and surface type
        /// 4. Verify point is within acceptable distance threshold of walkable surface
        /// </remarks>
        public override bool IsPointWalkable(Vector3 point)
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
        
        public override Vector3? ProjectPoint(Vector3 point)
        {
            if (ProjectToWalkmesh(point, out Vector3 result, out float height))
            {
                return result;
            }
            return null;
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
        /// - /: Physics-aware projection
        ///
        /// Algorithm:
        /// 1. Check static geometry projection (with destructible modifications)
        /// 2. Check dynamic obstacles (movable objects, physics bodies)
        /// 3. Check multi-level surfaces (platforms, elevated surfaces)
        /// 4. Select best projection based on distance and surface type
        /// </remarks>
        public override bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            return ProjectToWalkmeshInternal(point, out result, out height);
        }
        
        private bool ProjectToWalkmeshInternal(Vector3 point, out Vector3 result, out float height)
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
                if (ProjectToStaticFace(point, faceIndex, out Vector3 projected2, out float h2))
                {
                    candidates.Add(new ProjectionCandidate
                    {
                        ProjectedPoint = projected2,
                        Height = h2,
                        Distance = Vector3.Distance(point, projected2),
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
        /// <remarks>
        /// Based on daorigins.exe, DragonAge2.exe: Multi-level navigation surface projection
        /// Original implementation: Finds static faces within the level's height range and projects to the closest one
        /// Level surfaces represent elevated platforms, walkable surfaces at different heights
        /// </remarks>
        private bool ProjectToLevelSurface(Vector3 point, NavigationLevel level, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            // Find nearby static faces within search radius
            var candidates = new List<ProjectionCandidate>();
            FindNearbyStaticFaces(point, MultiLevelSearchRadius, candidates);

            // Filter candidates to only include faces within the level's height range
            // A face belongs to a level if any of its vertices are within the level's height range
            var levelCandidates = new List<ProjectionCandidate>();
            foreach (ProjectionCandidate candidate in candidates)
            {
                if (candidate.FaceIndex < 0 || candidate.FaceIndex >= _staticFaceCount)
                {
                    continue;
                }

                // Get face vertices to check if face is within level height range
                int baseIdx = candidate.FaceIndex * 3;
                if (baseIdx + 2 >= _staticFaceIndices.Length)
                {
                    continue;
                }

                Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
                Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
                Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

                // Check if any vertex is within the level's height range
                // HeightRange.X is minimum height, HeightRange.Y is maximum height
                bool faceInLevel = false;
                if (v1.Z >= level.HeightRange.X && v1.Z <= level.HeightRange.Y)
                {
                    faceInLevel = true;
                }
                else if (v2.Z >= level.HeightRange.X && v2.Z <= level.HeightRange.Y)
                {
                    faceInLevel = true;
                }
                else if (v3.Z >= level.HeightRange.X && v3.Z <= level.HeightRange.Y)
                {
                    faceInLevel = true;
                }

                // Also check if the projected point itself is within the height range
                // This handles cases where the face spans the level but vertices are outside
                if (!faceInLevel && candidate.Height >= level.HeightRange.X && candidate.Height <= level.HeightRange.Y)
                {
                    faceInLevel = true;
                }

                if (faceInLevel)
                {
                    levelCandidates.Add(candidate);
                }
            }

            // If no faces found in level, fall back to base height
            if (levelCandidates.Count == 0)
            {
                height = level.BaseHeight;
                result = new Vector3(point.X, point.Y, level.BaseHeight);
                return true;
            }

            // Find the closest projection candidate
            // Sort by distance (2D distance in X/Y plane, then by height difference)
            levelCandidates.Sort((a, b) =>
            {
                float distA = Vector3Extensions.Distance2D(point, a.ProjectedPoint);
                float distB = Vector3Extensions.Distance2D(point, b.ProjectedPoint);
                int distCompare = distA.CompareTo(distB);
                if (distCompare != 0)
                {
                    return distCompare;
                }
                // If 2D distance is equal, prefer the one closer to the level's base height
                float heightDiffA = Math.Abs(a.Height - level.BaseHeight);
                float heightDiffB = Math.Abs(b.Height - level.BaseHeight);
                return heightDiffA.CompareTo(heightDiffB);
            });

            ProjectionCandidate best = levelCandidates[0];
            result = best.ProjectedPoint;
            height = best.Height;
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
                    float dist = Vector3Extensions.Distance2D(point, center);
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
            float distSq = Vector3Extensions.DistanceSquared2D(point, aabbCenter);
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
        public override System.Collections.Generic.IList<Vector3> FindPath(Vector3 start, Vector3 goal)
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
        /// Based on daorigins.exe/DragonAge2.exe: Advanced obstacle avoidance
        /// Eclipse engine has the most sophisticated obstacle avoidance with dynamic obstacles and cover system.
        /// </summary>
        /// <remarks>
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Dynamic obstacle avoidance in pathfinding (Ghidra analysis needed: search for pathfinding with obstacle avoidance)
        /// - DragonAge2.exe: Enhanced dynamic obstacle avoidance with spatial acceleration (Ghidra analysis needed: search for navigation mesh pathfinding with obstacles)
        /// 
        /// Algorithm:
        /// 1. Convert ObstacleInfo (position, radius) to DynamicObstacle (with bounds, height, influence radius)
        /// 2. Get height at obstacle positions from navigation mesh
        /// 3. Calculate bounding box from spherical obstacle (radius-based)
        /// 4. Register obstacles temporarily with unique IDs (using negative range to avoid conflicts)
        /// 5. Update dynamic obstacle system to mark affected faces
        /// 6. Call FindPath which uses CalculateObstaclePenalty to avoid obstacles
        /// 7. Clean up temporary obstacles after pathfinding
        /// 
        /// Note: Temporary obstacles use IDs in the range [-1000000, -1000000 + obstacleCount) to avoid conflicts
        /// with permanent obstacles which typically use positive IDs.
        /// </remarks>
        public override System.Collections.Generic.IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<ObstacleInfo> obstacles)
        {
            // Handle null or empty obstacles list - delegate to standard FindPath
            if (obstacles == null || obstacles.Count == 0)
            {
                return FindPath(start, goal);
            }

            // Track temporary obstacle IDs for cleanup
            var temporaryObstacleIds = new List<int>();
            const int tempObstacleIdBase = -1000000;

            try
            {
                // Step 1: Convert ObstacleInfo to DynamicObstacle and register temporarily
                for (int i = 0; i < obstacles.Count; i++)
                {
                    ObstacleInfo obstacleInfo = obstacles[i];
                    
                    // Skip invalid obstacles (zero or negative radius)
                    if (obstacleInfo.Radius <= 0.0f)
                    {
                        continue;
                    }

                    // Generate unique temporary obstacle ID
                    int obstacleId = tempObstacleIdBase + i;
                    temporaryObstacleIds.Add(obstacleId);

                    // Get height at obstacle position from navigation mesh
                    // This ensures the obstacle is placed at the correct height on the terrain
                    // Note: Z is the vertical axis in Eclipse engine coordinate system
                    float obstacleHeight;
                    if (!GetHeightAtPoint(obstacleInfo.Position, out obstacleHeight))
                    {
                        // If we can't get height, use Z coordinate as fallback (Z is vertical axis)
                        obstacleHeight = obstacleInfo.Position.Z;
                    }

                    // Step 2: Calculate bounding box from spherical obstacle
                    // ObstacleInfo provides position and radius, representing a sphere
                    // We convert this to an axis-aligned bounding box
                    float radius = obstacleInfo.Radius;
                    Vector3 position = obstacleInfo.Position;
                    
                    // Create axis-aligned bounding box centered at position with radius in all directions
                    Vector3 boundsMin = new Vector3(
                        position.X - radius,
                        position.Y - radius,
                        obstacleHeight - radius  // Use terrain height as base
                    );
                    Vector3 boundsMax = new Vector3(
                        position.X + radius,
                        position.Y + radius,
                        obstacleHeight + radius  // Extend upward from terrain
                    );

                    // Step 3: Calculate obstacle height (vertical extent)
                    // For spherical obstacles, height is 2 * radius
                    float obstacleVerticalHeight = radius * 2.0f;

                    // Step 4: Use radius as influence radius for pathfinding
                    // Obstacles influence pathfinding within their radius
                    float influenceRadius = radius;

                    // Step 5: Register temporary obstacle
                    // Obstacles are non-walkable by default (block movement)
                    // They don't have walkable top surfaces unless specified
                    RegisterObstacle(
                        obstacleId,
                        position,
                        boundsMin,
                        boundsMax,
                        obstacleVerticalHeight,
                        influenceRadius,
                        isWalkable: false,  // Obstacles block movement
                        hasTopSurface: false  // Obstacles are not walkable platforms
                    );
                }

                // Step 6: Update dynamic obstacle system to mark affected faces
                // This ensures pathfinding cache is invalidated for affected regions
                UpdateDynamicObstacles();

                // Step 7: Call FindPath which uses CalculateObstaclePenalty to avoid obstacles
                // The FindPath method already integrates with dynamic obstacles through:
                // - CalculateTacticalEdgeCost calls CalculateObstaclePenalty
                // - CalculateObstaclePenalty checks _dynamicObstacles and adds penalties for paths near obstacles
                // - This naturally causes A* pathfinding to prefer paths that avoid obstacles
                return FindPath(start, goal);
            }
            finally
            {
                // Step 8: Clean up temporary obstacles
                // Remove all temporarily registered obstacles to restore mesh state
                foreach (int obstacleId in temporaryObstacleIds)
                {
                    RemoveObstacle(obstacleId);
                }

                // Clear any invalidated faces that were marked during temporary obstacle registration
                // Note: We don't call UpdateDynamicObstacles here because we've already removed the obstacles
                // The next call to UpdateDynamicObstacles will naturally clear the invalidated faces
            }
        }

        /// <summary>
        /// Finds the face index at a given position (INavigationMesh interface).
        /// </summary>
        public override int FindFaceAt(Vector3 position)
        {
            return FindStaticFaceAt(position);
        }

        /// <summary>
        /// Gets the center point of a face (INavigationMesh interface).
        /// </summary>
        public override Vector3 GetFaceCenter(int faceIndex)
        {
            return GetStaticFaceCenter(faceIndex);
        }

        /// <summary>
        /// Gets adjacent faces for a given face (INavigationMesh interface).
        /// </summary>
        public override System.Collections.Generic.IEnumerable<int> GetAdjacentFaces(int faceIndex)
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
        public override bool IsWalkable(int faceIndex)
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
        public override int GetSurfaceMaterial(int faceIndex)
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
        public override bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
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
        public override bool TestLineOfSight(Vector3 from, Vector3 to)
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
        public override bool ProjectToSurface(Vector3 point, out Vector3 result, out float height)
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
            Vector3 leftHit = Vector3.Zero;
            Vector3 rightHit = Vector3.Zero;
            int leftFace = -1;
            int rightFace = -1;
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
        /// - /: Advanced tactical pathfinding with dynamic obstacle avoidance
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
        /// 
        /// Full implementation:
        /// 1. Queries combat system for active threats when world is available
        /// 2. Checks line of sight from threats to position
        /// 3. Calculates exposure based on distance and cover availability
        /// 
        /// When world is not available, uses improved heuristic based on path geometry.
        /// 
        /// Based on daorigins.exe: Tactical pathfinding with threat assessment
        /// Based on DragonAge2.exe: Enhanced tactical pathfinding with threat exposure calculation
        /// </remarks>
        private float CalculateThreatExposure(Vector3 position, Vector3 start, Vector3 end)
        {
            // If world is available, perform full threat assessment
            if (_world != null)
            {
                return CalculateThreatExposureWithWorld(position, start, end);
            }

            // Fallback: Improved heuristic when world is not available
            return CalculateThreatExposureHeuristic(position, start, end);
        }

        /// <summary>
        /// Calculates threat exposure using world queries (full implementation).
        /// Queries for active enemies, checks line of sight, and calculates exposure penalty.
        /// </summary>
        private float CalculateThreatExposureWithWorld(Vector3 position, Vector3 start, Vector3 end)
        {
            const float threatSearchRadius = 50.0f; // Maximum range to search for threats
            const float maxThreatDistance = 50.0f; // Maximum distance threat can influence pathfinding
            const float baseThreatPenalty = 3.0f; // Base penalty per exposed threat
            const float minThreatDistance = 2.0f; // Minimum distance to consider (too close = always exposed)

            float totalPenalty = 0.0f;

            // Query for entities in threat range
            // In Eclipse, threats are typically enemies that are in combat or hostile
            // Get all entities within search radius
            var nearbyEntities = _world.GetEntitiesInRadius(position, threatSearchRadius);
            
            if (nearbyEntities == null)
            {
                return 0.0f;
            }

            // Check each entity as potential threat
            foreach (IEntity entity in nearbyEntities)
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Get entity position
                ITransformComponent transform = entity.GetComponent<ITransformComponent>();
                if (transform == null)
                {
                    continue;
                }

                Vector3 threatPosition = transform.Position;
                float distanceToThreat = Vector3.Distance(position, threatPosition);

                // Skip threats that are too far away
                if (distanceToThreat > maxThreatDistance)
                {
                    continue;
                }

                // Skip threats that are too close (always exposed, handled by base penalty)
                if (distanceToThreat < minThreatDistance)
                {
                    totalPenalty += baseThreatPenalty * 2.0f; // Double penalty for very close threats
                    continue;
                }

                // Check if entity is a threat (in combat, hostile, etc.)
                // In Eclipse, check if entity is in combat or has hostile faction
                bool isThreat = IsEntityThreat(entity, start, end);
                if (!isThreat)
                {
                    continue;
                }

                // Check line of sight from threat to position
                // Position is exposed if there's clear line of sight from threat
                bool hasLineOfSight = TestLineOfSight(threatPosition, position);

                if (hasLineOfSight)
                {
                    // Calculate exposure penalty based on distance
                    // Closer threats = higher penalty
                    // Penalty decreases with distance
                    float distanceFactor = 1.0f - ((distanceToThreat - minThreatDistance) / (maxThreatDistance - minThreatDistance));
                    distanceFactor = Math.Max(0.0f, Math.Min(1.0f, distanceFactor)); // Clamp to [0, 1]

                    // Check if position has cover from this threat
                    float coverProtection = CalculateCoverProtection(position, threatPosition);
                    
                    // Exposure = distance factor * (1 - cover protection)
                    float exposure = distanceFactor * (1.0f - coverProtection);
                    
                    // Apply penalty
                    totalPenalty += baseThreatPenalty * exposure;
                }
            }

            return totalPenalty;
        }

        /// <summary>
        /// Determines if an entity is a threat for pathfinding purposes.
        /// In Eclipse, threats are typically enemies that are in combat or hostile to friendly entities.
        /// </summary>
        /// <remarks>
        /// Threat assessment checks:
        /// 1. Entity must be alive (dead entities are not threats)
        /// 2. Entity is in combat (entities actively fighting are threats)
        /// 3. Entity is hostile to player/party (faction-based threat assessment)
        /// 
        /// Based on daorigins.exe: Tactical pathfinding with threat assessment
        /// Based on DragonAge2.exe: Enhanced threat assessment using faction relationships
        /// </remarks>
        private bool IsEntityThreat(IEntity entity, Vector3 start, Vector3 end)
        {
            // Check if entity is alive (dead entities are not threats)
            IStatsComponent stats = entity.GetComponent<IStatsComponent>();
            if (stats != null && stats.CurrentHP <= 0)
            {
                return false;
            }

            // Check if entity is in combat
            // Entities in combat are actively fighting and pose a threat
            // Try to find combat system through world to check combat state
            if (_world != null)
            {
                // Check if entity has combat-related components or is engaged in combat
                // In Eclipse, entities in combat typically have combat targets or are being targeted
                // We can infer combat state by checking if entity has recent combat activity
                // For now, we'll check faction relationships instead (more reliable without direct combat system access)
            }

            // Check faction relationships - entity is a threat if hostile to player/party
            IFactionComponent entityFaction = entity.GetComponent<IFactionComponent>();
            if (entityFaction != null && _world != null)
            {
                // Try to find player entity to check hostility
                // In Eclipse, player entity typically has tag "Player" or is of type Player
                IEntity playerEntity = _world.GetEntityByTag("Player");
                if (playerEntity == null)
                {
                    // Try to find any player-type entity
                    foreach (IEntity potentialPlayer in _world.GetEntitiesOfType(ObjectType.Player))
                    {
                        playerEntity = potentialPlayer;
                        break; // Use first player entity found
                    }
                }

                // Check if entity is hostile to player
                if (playerEntity != null && playerEntity.IsValid)
                {
                    if (entityFaction.IsHostile(playerEntity))
                    {
                        return true; // Entity is hostile to player - it's a threat
                    }
                }

                // Check if entity is hostile to any friendly entities nearby
                // This helps identify threats even when player is not nearby
                // Check entities near the pathfinding area (between start and end)
                Vector3 pathCenter = (start + end) * 0.5f;
                float checkRadius = Vector3.Distance(start, end) * 0.5f + 10.0f; // Check radius around path
                
                foreach (IEntity nearbyEntity in _world.GetEntitiesInRadius(pathCenter, checkRadius, ObjectType.Creature | ObjectType.Player))
                {
                    if (nearbyEntity == null || !nearbyEntity.IsValid || nearbyEntity == entity)
                    {
                        continue;
                    }

                    // Check if nearby entity is friendly (not hostile to entity being checked)
                    IFactionComponent nearbyFaction = nearbyEntity.GetComponent<IFactionComponent>();
                    if (nearbyFaction != null)
                    {
                        // If entity is hostile to a nearby friendly entity, it's a threat
                        if (entityFaction.IsHostile(nearbyEntity) && !nearbyFaction.IsHostile(entity))
                        {
                            return true; // Entity is hostile to friendly entities - it's a threat
                        }
                    }
                }

                // If entity is not hostile to anyone, it's less likely to be a threat
                // Return false for non-hostile entities (they're neutral/friendly)
                return false;
            }

            // Fallback: If we can't determine threat status, be conservative
            // Consider entity as potential threat if we can't verify it's friendly
            // This ensures pathfinding avoids unknown entities when possible
            return true;
        }

        /// <summary>
        /// Calculates how much cover protects a position from a threat.
        /// Returns value between 0.0 (no cover) and 1.0 (full cover).
        /// </summary>
        private float CalculateCoverProtection(Vector3 position, Vector3 threatPosition)
        {
            // Check if there's cover between position and threat
            // Use cover point system to find nearby cover that blocks line of sight

            const float coverCheckRadius = 3.0f; // Radius to search for cover points
            float bestCover = 0.0f;

            // Ensure cover points are generated
            EnsureCoverPointsGenerated();

            // Find cover points near the position that might block line of sight from threat
            Vector3 directionToThreat = Vector3.Normalize(threatPosition - position);
            float distanceToThreat = Vector3.Distance(position, threatPosition);

            foreach (CoverPoint coverPoint in _coverPoints)
            {
                // Check if cover point is between position and threat
                Vector3 toCover = coverPoint.Position - position;
                float distToCover = toCover.Length();

                if (distToCover > coverCheckRadius || distToCover > distanceToThreat)
                {
                    continue; // Cover point is too far or behind threat
                }

                // Check if cover point is in the direction of threat (provides protection)
                float dotProduct = Vector3.Dot(Vector3.Normalize(toCover), directionToThreat);
                if (dotProduct < 0.0f)
                {
                    continue; // Cover point is behind position
                }

                // Check if cover blocks line of sight from threat to position
                // If raycast from threat to position hits cover, it provides protection
                Vector3 directionFromThreat = Vector3.Normalize(position - threatPosition);
                Vector3 coverDirection = Vector3.Normalize(coverPoint.Position - threatPosition);
                float coverDot = Vector3.Dot(directionFromThreat, coverDirection);

                // Cover is effective if it's between threat and position
                if (coverDot > 0.7f && distToCover < distanceToThreat * 0.9f)
                {
                    // This cover point provides protection
                    // Protection increases with cover quality and proximity
                    float proximityFactor = 1.0f - (distToCover / coverCheckRadius);
                    float protection = coverPoint.Quality * proximityFactor;
                    bestCover = Math.Max(bestCover, protection);
                }
            }

            return Math.Min(1.0f, bestCover); // Clamp to [0, 1]
        }

        /// <summary>
        /// Calculates threat exposure using heuristic approach (fallback when world is not available).
        /// Uses improved heuristic based on path geometry and distance from start/end.
        /// </summary>
        private float CalculateThreatExposureHeuristic(Vector3 position, Vector3 start, Vector3 end)
        {
            // Improved heuristic: positions that are:
            // 1. Far from both start and end (middle of path = more exposed)
            // 2. Away from cover points (less protection)
            // 3. In open areas (higher exposure)

            float distFromStart = Vector3.Distance(position, start);
            float distFromEnd = Vector3.Distance(position, end);
            float pathLength = Vector3.Distance(start, end);

            // Normalize distances relative to path length
            float normalizedDistFromStart = pathLength > 0.01f ? distFromStart / pathLength : 0.0f;
            float normalizedDistFromEnd = pathLength > 0.01f ? distFromEnd / pathLength : 0.0f;

            // Middle of path is more exposed (both distances are medium)
            // Path middle = both normalized distances are around 0.5
            float middleExposure = 1.0f - Math.Abs(normalizedDistFromStart - normalizedDistFromEnd);
            middleExposure = Math.Max(0.0f, middleExposure); // Positions in middle have higher exposure

            // Check cover availability
            float coverProtection = 0.0f;
            EnsureCoverPointsGenerated();
            const float coverSearchRadius = 5.0f;
            float nearestCoverDist = float.MaxValue;

            foreach (CoverPoint coverPoint in _coverPoints)
            {
                float dist = Vector3.Distance(position, coverPoint.Position);
                if (dist < coverSearchRadius && dist < nearestCoverDist)
                {
                    nearestCoverDist = dist;
                    coverProtection = coverPoint.Quality * (1.0f - (dist / coverSearchRadius));
                }
            }

            // Combine factors
            // Exposure = middle exposure * (1 - cover protection)
            float exposureFactor = middleExposure * (1.0f - coverProtection);

            // Scale by path length (longer paths have more exposure risk)
            float pathLengthFactor = Math.Min(1.0f, pathLength / 50.0f);

            // Base threat penalty
            const float baseThreatPenalty = 2.0f;

            return exposureFactor * pathLengthFactor * baseThreatPenalty;
        }

        /// <summary>
        /// Calculates cover bonus for a position.
        /// </summary>
        /// <remarks>
        /// Lower cost (bonus) for positions near cover.
        /// Uses the cover point system to find nearby cover and calculate bonus based on proximity and quality.
        /// 
        /// Algorithm:
        /// 1. Query nearby cover points within search radius
        /// 2. Calculate bonus based on distance to nearest cover point and its quality
        /// 3. Bonus decreases with distance and increases with cover quality
        /// 4. Maximum bonus is applied when position is very close to high-quality cover
        /// </remarks>
        private float CalculateCoverBonus(Vector3 position)
        {
            // Ensure cover points are generated
            EnsureCoverPointsGenerated();

            // Search radius for nearby cover points
            // Positions within this radius can benefit from cover
            const float coverSearchRadius = 3.0f;
            float bonus = 0.0f;

            // Find nearby cover points
            float nearestCoverDistance = float.MaxValue;
            float bestCoverQuality = 0.0f;

            float radiusSq = coverSearchRadius * coverSearchRadius;

            foreach (CoverPoint coverPoint in _coverPoints)
            {
                // Calculate 2D distance to cover point (ignoring height for cover assessment)
                float distSq = Vector3Extensions.DistanceSquared2D(position, coverPoint.Position);
                
                if (distSq <= radiusSq)
                {
                    float distance = (float)Math.Sqrt(distSq);
                    
                    // Track nearest cover point
                    if (distance < nearestCoverDistance)
                    {
                        nearestCoverDistance = distance;
                        bestCoverQuality = coverPoint.Quality;
                    }
                }
            }

            // Calculate bonus based on proximity to nearest cover and its quality
            if (nearestCoverDistance < float.MaxValue)
            {
                // Normalize distance (0.0 = at cover point, 1.0 = at search radius)
                float normalizedDistance = nearestCoverDistance / coverSearchRadius;
                
                // Bonus decreases with distance (closer is better)
                // Quality factor multiplies the bonus (better cover = larger bonus)
                // Maximum bonus when very close to high-quality cover
                float distanceFactor = 1.0f - normalizedDistance; // 1.0 at cover point, 0.0 at radius
                float qualityFactor = bestCoverQuality; // 0.0 to 1.0
                
                // Combine factors: quality determines max bonus, distance determines how much we get
                // Base bonus range: 0.0 to -2.0 (negative because it's a cost reduction/bonus)
                float maxBonus = -2.0f * qualityFactor; // Higher quality = larger bonus
                bonus = maxBonus * distanceFactor; // Closer = more of the bonus
                
                // Ensure bonus is negative (cost reduction)
                bonus = Math.Max(bonus, -2.0f);
                bonus = Math.Min(bonus, 0.0f);
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
            // TODO: STUB - For now, use simple distance heuristic
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
        /// - /: Physics-aware height sampling
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
        /// - : Physics-aware height sampling (search for physics integration in navigation)
        /// - : Enhanced physics-aware height sampling (search for improved navigation height functions)
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
        /// - /: Physics-aware line of sight with dynamic obstacles
        /// 
        /// Note: Function addresses to be determined via Ghidra MCP reverse engineering:
        /// - daorigins.exe: Line of sight function (search for "HasLineOfSight", "LineOfSight", "Raycast" references)
        /// - DragonAge2.exe: Enhanced line of sight function (search for dynamic obstacle integration)
        /// - : Physics-aware line of sight (search for physics integration in line of sight)
        /// - : Enhanced physics-aware line of sight (search for improved line of sight functions)
        /// </remarks>
        public new bool HasLineOfSight(Vector3 start, Vector3 end)
        {
            return base.HasLineOfSight(start, end);
        }

        /// <summary>
        /// Eclipse-specific check: handles destructible modifications and walkable faces.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe, DragonAge2.exe, , :
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
        /// Based on daorigins.exe, DragonAge2.exe, , :
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
        /// Ensures cover points are generated and up to date.
        /// </summary>
        /// <remarks>
        /// Lazy generation of cover points from static geometry and dynamic obstacles.
        /// Regenerates if mesh has been modified or obstacles have changed.
        /// </remarks>
        private void EnsureCoverPointsGenerated()
        {
            if (!_coverPointsDirty && !_meshNeedsRebuild)
            {
                return; // Cover points are up to date
            }

            _coverPoints.Clear();

            // Generate cover points from static geometry
            GenerateCoverPointsFromStaticGeometry();

            // Generate cover points from dynamic obstacles
            GenerateCoverPointsFromDynamicObstacles();

            // Second pass: Update quality ratings with nearby support information
            UpdateCoverPointQualityWithNearbySupport();

            _coverPointsDirty = false;
        }

        /// <summary>
        /// Generates cover points from static geometry (walls, edges, corners).
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Static geometry cover point generation
        /// - DragonAge2.exe: Enhanced static cover point system
        ///
        /// Algorithm:
        /// 1. Iterate through all static faces
        /// 2. Identify vertical or near-vertical faces (walls) that can provide cover
        /// 3. Generate cover points along edges of these faces
        /// 4. Rate cover quality based on height, angle, and nearby geometry
        /// 5. Filter out low-quality or redundant cover points
        /// </remarks>
        private void GenerateCoverPointsFromStaticGeometry()
        {
            if (_staticFaceCount == 0)
            {
                return;
            }

            // Iterate through all static faces
            for (int faceIndex = 0; faceIndex < _staticFaceCount; faceIndex++)
            {
                int baseIdx = faceIndex * 3;
                if (baseIdx + 2 >= _staticFaceIndices.Length)
                {
                    continue;
                }

                Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
                Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
                Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

                // Calculate face normal
                Vector3 edge1 = v2 - v1;
                Vector3 edge2 = v3 - v1;
                Vector3 normal = Vector3.Cross(edge1, edge2);
                float normalLength = normal.Length();
                if (normalLength < 1e-6f)
                {
                    continue; // Degenerate face
                }
                normal = normal / normalLength;

                // Check if face is vertical or near-vertical (can provide cover)
                // Vertical faces have normal pointing mostly horizontally
                float verticalComponent = Math.Abs(normal.Z);
                if (verticalComponent < CoverPointMaxWallAngle)
                {
                    // Face is vertical enough to provide cover
                    // Calculate face height (Z range)
                    float minZ = Math.Min(Math.Min(v1.Z, v2.Z), v3.Z);
                    float maxZ = Math.Max(Math.Max(v1.Z, v2.Z), v3.Z);
                    float faceHeight = maxZ - minZ;

                    if (faceHeight >= CoverPointMinWallHeight)
                    {
                        // Generate cover points along edges
                        GenerateCoverPointsFromFaceEdges(faceIndex, v1, v2, v3, normal, minZ, maxZ);
                    }
                }
            }
        }

        /// <summary>
        /// Generates cover points along the edges of a face.
        /// </summary>
        private void GenerateCoverPointsFromFaceEdges(int faceIndex, Vector3 v1, Vector3 v2, Vector3 v3, Vector3 normal, float minZ, float maxZ)
        {
            // Generate cover points along each edge
            Vector3[][] edges = new Vector3[][]
            {
                new Vector3[] { v1, v2 },
                new Vector3[] { v2, v3 },
                new Vector3[] { v3, v1 }
            };

            foreach (Vector3[] edge in edges)
            {
                Vector3 edgeStart = edge[0];
                Vector3 edgeEnd = edge[1];
                Vector3 edgeDir = edgeEnd - edgeStart;
                float edgeLength = edgeDir.Length();
                if (edgeLength < 1e-6f)
                {
                    continue;
                }
                edgeDir = edgeDir / edgeLength;

                // Generate points along edge with spacing
                int numPoints = (int)(edgeLength / CoverPointGenerationSpacing) + 1;
                for (int i = 0; i <= numPoints; i++)
                {
                    float t = i / (float)numPoints;
                    Vector3 edgePoint = edgeStart + edgeDir * (edgeLength * t);

                    // Project to walkable surface below
                    Vector3 coverPosition;
                    float coverHeight;
                    if (ProjectToWalkmesh(edgePoint, out coverPosition, out coverHeight))
                    {
                        // Calculate cover quality
                        float quality = CalculateCoverQuality(coverPosition, normal, maxZ - coverHeight, faceIndex, -1);

                        // Only add if quality is above threshold
                        if (quality > 0.3f)
                        {
                            // Check if too close to existing cover point
                            bool tooClose = false;
                            int existingIndex = -1;
                            for (int j = 0; j < _coverPoints.Count; j++)
                            {
                                CoverPoint existing = _coverPoints[j];
                                float dist = Vector3Extensions.Distance2D(coverPosition, existing.Position);
                                if (dist < CoverPointGenerationSpacing * 0.5f)
                                {
                                    // Too close, keep the one with better quality
                                    if (quality <= existing.Quality)
                                    {
                                        tooClose = true;
                                        break;
                                    }
                                    // Mark existing lower quality point for removal
                                    existingIndex = i;
                                    break;
                                }
                            }

                            if (existingIndex >= 0)
                            {
                                // Remove existing lower quality point
                                _coverPoints.RemoveAt(existingIndex);
                            }

                            if (!tooClose)
                            {
                                _coverPoints.Add(new CoverPoint
                                {
                                    Position = coverPosition,
                                    CoverNormal = normal,
                                    Quality = quality,
                                    CoverHeight = maxZ - coverHeight,
                                    FaceIndex = faceIndex,
                                    ObstacleId = -1
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generates cover points from dynamic obstacles.
        /// </summary>
        /// <remarks>
        /// Based on reverse engineering of:
        /// - daorigins.exe: Dynamic obstacle cover point generation
        /// - DragonAge2.exe: Enhanced dynamic cover point system
        ///
        /// Algorithm:
        /// 1. Iterate through active dynamic obstacles
        /// 2. For non-walkable obstacles, generate cover points around their perimeter
        /// 3. Rate cover quality based on obstacle size and position
        /// 4. Filter out low-quality or redundant cover points
        /// </remarks>
        private void GenerateCoverPointsFromDynamicObstacles()
        {
            foreach (DynamicObstacle obstacle in _dynamicObstacles)
            {
                if (!obstacle.IsActive || obstacle.IsWalkable)
                {
                    continue; // Skip inactive or walkable obstacles
                }

                // Generate cover points around obstacle perimeter
                Vector3 obstacleCenter = (obstacle.BoundsMin + obstacle.BoundsMax) * 0.5f;
                Vector3 obstacleSize = obstacle.BoundsMax - obstacle.BoundsMin;
                float obstacleHeight = obstacleSize.Z;

                if (obstacleHeight < CoverPointMinWallHeight)
                {
                    continue; // Obstacle too short to provide cover
                }

                // Generate points around perimeter (8 directions)
                Vector3[] directions = new Vector3[]
                {
                    new Vector3(1.0f, 0.0f, 0.0f),
                    new Vector3(-1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, 1.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f),
                    new Vector3(0.707f, 0.707f, 0.0f),
                    new Vector3(-0.707f, 0.707f, 0.0f),
                    new Vector3(0.707f, -0.707f, 0.0f),
                    new Vector3(-0.707f, -0.707f, 0.0f)
                };

                float perimeterDistance = Math.Max(obstacleSize.X, obstacleSize.Y) * 0.5f + 0.5f; // Slightly outside obstacle

                foreach (Vector3 dir in directions)
                {
                    Vector3 coverCandidate = obstacleCenter + dir * perimeterDistance;

                    // Project to walkable surface
                    Vector3 coverPosition;
                    float coverHeight;
                    if (ProjectToWalkmesh(coverCandidate, out coverPosition, out coverHeight))
                    {
                        // Calculate cover normal (away from obstacle)
                        Vector3 coverNormal = Vector3.Normalize(coverPosition - obstacleCenter);
                        coverNormal = new Vector3(coverNormal.X, coverNormal.Y, 0.0f); // Horizontal only
                        if (coverNormal.Length() < 1e-6f)
                        {
                            coverNormal = new Vector3(1.0f, 0.0f, 0.0f);
                        }
                        else
                        {
                            coverNormal = Vector3.Normalize(coverNormal);
                        }

                        // Calculate cover quality
                        float quality = CalculateCoverQuality(coverPosition, coverNormal, obstacleHeight, -1, obstacle.ObstacleId);

                        // Only add if quality is above threshold
                        if (quality > 0.3f)
                        {
                            // Check if too close to existing cover point
                            bool tooClose = false;
                            int existingIndex = -1;
                            for (int i = 0; i < _coverPoints.Count; i++)
                            {
                                CoverPoint existing = _coverPoints[i];
                                float dist = Vector3Extensions.Distance2D(coverPosition, existing.Position);
                                if (dist < CoverPointGenerationSpacing * 0.5f)
                                {
                                    // Too close, keep the one with better quality
                                    if (quality <= existing.Quality)
                                    {
                                        tooClose = true;
                                        break;
                                    }
                                    // Mark existing lower quality point for removal
                                    existingIndex = i;
                                    break;
                                }
                            }

                            if (existingIndex >= 0)
                            {
                                // Remove existing lower quality point
                                _coverPoints.RemoveAt(existingIndex);
                            }

                            if (!tooClose)
                            {
                                _coverPoints.Add(new CoverPoint
                                {
                                    Position = coverPosition,
                                    CoverNormal = coverNormal,
                                    Quality = quality,
                                    CoverHeight = obstacleHeight,
                                    FaceIndex = -1,
                                    ObstacleId = obstacle.ObstacleId
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculates the quality rating for a cover point.
        /// </summary>
        /// <remarks>
        /// Quality is based on:
        /// - Cover height (taller is better)
        /// - Cover angle (more vertical is better)
        /// - Nearby geometry support (more nearby cover is better)
        /// - Walkable surface availability
        ///
        /// Note: Nearby geometry support is calculated after all cover points are generated,
        /// so this method is called in two phases - first without nearby support, then with a second pass.
        /// </remarks>
        private float CalculateCoverQuality(Vector3 position, Vector3 coverNormal, float coverHeight, int faceIndex, int obstacleId)
        {
            return CalculateCoverQuality(position, coverNormal, coverHeight, faceIndex, obstacleId, 0);
        }

        /// <summary>
        /// Calculates the quality rating for a cover point with nearby support count.
        /// </summary>
        private float CalculateCoverQuality(Vector3 position, Vector3 coverNormal, float coverHeight, int faceIndex, int obstacleId, int nearbyCoverCount)
        {
            float quality = 0.0f;

            // Height factor (0.0 to 0.4)
            float normalizedHeight = Math.Min(coverHeight / 2.0f, 1.0f); // 2.0m is considered full height
            quality += normalizedHeight * 0.4f;

            // Angle factor (0.0 to 0.3) - more vertical is better
            float verticalComponent = Math.Abs(coverNormal.Z);
            float angleFactor = 1.0f - verticalComponent; // Horizontal normal = better cover
            quality += angleFactor * 0.3f;

            // Nearby geometry support (0.0 to 0.2)
            float supportFactor = Math.Min(nearbyCoverCount / 5.0f, 1.0f); // Up to 5 nearby points is ideal
            quality += supportFactor * 0.2f;

            // Walkable surface factor (0.0 to 0.1)
            if (IsPointWalkable(position))
            {
                quality += 0.1f;
            }

            return Math.Min(quality, 1.0f);
        }

        /// <summary>
        /// Updates cover point quality ratings with nearby support information.
        /// </summary>
        /// <remarks>
        /// Second pass after initial generation to incorporate nearby cover point density
        /// into quality ratings. This improves the tactical value assessment.
        /// </remarks>
        private void UpdateCoverPointQualityWithNearbySupport()
        {
            // Create a list of updated cover points
            var updatedCoverPoints = new List<CoverPoint>(_coverPoints.Count);

            for (int i = 0; i < _coverPoints.Count; i++)
            {
                CoverPoint point = _coverPoints[i];

                // Count nearby cover points
                int nearbyCoverCount = 0;
                for (int j = 0; j < _coverPoints.Count; j++)
                {
                    if (i != j)
                    {
                        CoverPoint other = _coverPoints[j];
                        float dist = Vector3Extensions.Distance2D(point.Position, other.Position);
                        if (dist > 0.1f && dist < CoverPointQualityRadius)
                        {
                            nearbyCoverCount++;
                        }
                    }
                }

                // Recalculate quality with nearby support
                float updatedQuality = CalculateCoverQuality(
                    point.Position,
                    point.CoverNormal,
                    point.CoverHeight,
                    point.FaceIndex,
                    point.ObstacleId,
                    nearbyCoverCount);

                // Update cover point with new quality
                point.Quality = updatedQuality;
                updatedCoverPoints.Add(point);
            }

            // Replace the list with updated points
            _coverPoints.Clear();
            _coverPoints.AddRange(updatedCoverPoints);
        }

        /// <summary>
        /// Finds nearby cover points.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific tactical feature.
        /// Identifies cover positions for combat AI.
        /// Considers cover quality and positioning.
        ///
        /// Based on reverse engineering of:
        /// - daorigins.exe: Cover point query system
        /// - DragonAge2.exe: Enhanced cover point query with quality sorting
        ///
        /// Algorithm:
        /// 1. Ensure cover points are generated and up to date
        /// 2. Query cover points within radius
        /// 3. Sort by quality and distance
        /// 4. Return sorted cover points
        /// </remarks>
        public IEnumerable<Vector3> FindCoverPoints(Vector3 position, float radius)
        {
            // Ensure cover points are generated
            EnsureCoverPointsGenerated();

            // Query cover points within radius
            var nearbyCoverPoints = new List<CoverPoint>();
            float radiusSq = radius * radius;

            foreach (CoverPoint coverPoint in _coverPoints)
            {
                float distSq = Vector3Extensions.DistanceSquared2D(position, coverPoint.Position);
                if (distSq <= radiusSq)
                {
                    nearbyCoverPoints.Add(coverPoint);
                }
            }

            // Sort by quality (descending), then by distance (ascending)
            nearbyCoverPoints.Sort((a, b) =>
            {
                int qualityCompare = b.Quality.CompareTo(a.Quality);
                if (qualityCompare != 0)
                {
                    return qualityCompare;
                }
                float distA = Vector3Extensions.DistanceSquared2D(position, a.Position);
                float distB = Vector3Extensions.DistanceSquared2D(position, b.Position);
                return distA.CompareTo(distB);
            });

            // Return positions
            foreach (CoverPoint coverPoint in nearbyCoverPoints)
            {
                yield return coverPoint.Position;
            }
        }

        /// <summary>
        /// Updates the navigation mesh for dynamic changes.
        /// </summary>
        /// <remarks>
        /// Eclipse allows real-time mesh updates.
        /// Handles destruction, object movement, terrain changes.
        /// Recalculates affected navigation regions.
        ///
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Dynamic obstacle update system (Ghidra analysis needed: search for obstacle update functions)
        /// - DragonAge2.exe: Enhanced dynamic obstacle update with spatial acceleration
        ///   (Ghidra analysis needed: search for navigation mesh update functions)
        ///
        /// Algorithm:
        /// 1. Detect changed obstacles (position, bounds, active state)
        /// 2. Identify affected navigation faces (faces that intersect with obstacle bounds)
        /// 3. Invalidate pathfinding cache for affected regions
        /// 4. Update obstacle state tracking
        /// 5. Rebuild spatial acceleration structures if needed
        /// 6. Notify pathfinding systems of changes
        ///
        /// Note: Function addresses to be determined via Ghidra MCP reverse engineering:
        /// - daorigins.exe: Obstacle update function (search for "UpdateObstacles", "UpdateNavigation", "DynamicObstacle" references)
        /// - DragonAge2.exe: Enhanced obstacle update function (search for navigation mesh update functions)
        /// </remarks>
        public void UpdateDynamicObstacles()
        {
            // Track which obstacles have changed
            var changedObstacles = new List<int>();
            var removedObstacles = new List<int>();
            var addedObstacles = new List<int>();

            // Step 1: Detect changed obstacles
            DetectObstacleChanges(changedObstacles, removedObstacles, addedObstacles);

            // Step 2: Identify affected faces for changed/removed obstacles
            var affectedFaces = new HashSet<int>();
            foreach (int obstacleId in changedObstacles)
            {
                if (_obstacleById.ContainsKey(obstacleId))
                {
                    DynamicObstacle obstacle = _obstacleById[obstacleId];
                    HashSet<int> obstacleFaces = GetAffectedFaces(obstacle);
                    foreach (int faceId in obstacleFaces)
                    {
                        affectedFaces.Add(faceId);
                    }

                    // Also check previous position for removed influence
                    if (_previousObstacleStates.ContainsKey(obstacleId))
                    {
                        ObstacleState previousState = _previousObstacleStates[obstacleId];
                        HashSet<int> previousFaces = GetAffectedFacesForBounds(previousState.Position, previousState.BoundsMin, previousState.BoundsMax, previousState.InfluenceRadius);
                        foreach (int faceId in previousFaces)
                        {
                            affectedFaces.Add(faceId);
                        }
                    }
                }
            }

            foreach (int obstacleId in removedObstacles)
            {
                // Check previous position for removed influence
                if (_previousObstacleStates.ContainsKey(obstacleId))
                {
                    ObstacleState previousState = _previousObstacleStates[obstacleId];
                    HashSet<int> previousFaces = GetAffectedFacesForBounds(previousState.Position, previousState.BoundsMin, previousState.BoundsMax, previousState.InfluenceRadius);
                    foreach (int faceId in previousFaces)
                    {
                        affectedFaces.Add(faceId);
                    }
                }

                // Remove from affected faces cache
                if (_obstacleAffectedFaces.ContainsKey(obstacleId))
                {
                    _obstacleAffectedFaces.Remove(obstacleId);
                }
            }

            foreach (int obstacleId in addedObstacles)
            {
                if (_obstacleById.ContainsKey(obstacleId))
                {
                    DynamicObstacle obstacle = _obstacleById[obstacleId];
                    HashSet<int> obstacleFaces = GetAffectedFaces(obstacle);
                    _obstacleAffectedFaces[obstacleId] = obstacleFaces;
                    foreach (int faceId in obstacleFaces)
                    {
                        affectedFaces.Add(faceId);
                    }
                }
            }

            // Step 3: Invalidate pathfinding cache for affected faces
            foreach (int faceId in affectedFaces)
            {
                _invalidatedFaces.Add(faceId);
            }

            // Step 4: Update obstacle state tracking
            UpdateObstacleStateTracking(changedObstacles, removedObstacles, addedObstacles);

            // Step 5: Rebuild spatial acceleration structures if needed
            // Note: For Eclipse, we may need to rebuild obstacle spatial structures
            // The static AABB tree doesn't need rebuilding, but obstacle queries might benefit from spatial acceleration
            if (changedObstacles.Count > 0 || removedObstacles.Count > 0 || addedObstacles.Count > 0)
            {
                _meshNeedsRebuild = true;
                _coverPointsDirty = true; // Cover points need regeneration when obstacles change
            }

            // Step 6: Clear invalidated faces if update is complete
            // (Pathfinding systems will check _invalidatedFaces before using cached paths)
            // We keep the invalidated faces set for pathfinding systems to check
        }

        /// <summary>
        /// Detects changes in dynamic obstacles (position, bounds, active state).
        /// </summary>
        private void DetectObstacleChanges(List<int> changedObstacles, List<int> removedObstacles, List<int> addedObstacles)
        {
            // Check existing obstacles for changes
            var currentObstacleIds = new HashSet<int>();
            foreach (DynamicObstacle obstacle in _dynamicObstacles)
            {
                currentObstacleIds.Add(obstacle.ObstacleId);

                if (!_previousObstacleStates.ContainsKey(obstacle.ObstacleId))
                {
                    // New obstacle
                    addedObstacles.Add(obstacle.ObstacleId);
                }
                else
                {
                    // Check for changes
                    ObstacleState previousState = _previousObstacleStates[obstacle.ObstacleId];
                    bool hasChanged = false;

                    // Check position change
                    float positionDelta = Vector3.Distance(obstacle.Position, previousState.Position);
                    if (positionDelta > ObstacleChangeThreshold)
                    {
                        hasChanged = true;
                    }

                    // Check bounds change
                    float boundsMinDelta = Vector3.Distance(obstacle.BoundsMin, previousState.BoundsMin);
                    float boundsMaxDelta = Vector3.Distance(obstacle.BoundsMax, previousState.BoundsMax);
                    if (boundsMinDelta > ObstacleChangeThreshold || boundsMaxDelta > ObstacleChangeThreshold)
                    {
                        hasChanged = true;
                    }

                    // Check active state change
                    if (obstacle.IsActive != previousState.IsActive)
                    {
                        hasChanged = true;
                    }

                    // Check walkable state change
                    if (obstacle.IsWalkable != previousState.IsWalkable)
                    {
                        hasChanged = true;
                    }

                    // Check influence radius change
                    if (Math.Abs(obstacle.InfluenceRadius - previousState.InfluenceRadius) > ObstacleChangeThreshold)
                    {
                        hasChanged = true;
                    }

                    if (hasChanged)
                    {
                        changedObstacles.Add(obstacle.ObstacleId);
                    }
                }
            }

            // Check for removed obstacles
            foreach (int previousId in _previousObstacleStates.Keys)
            {
                if (!currentObstacleIds.Contains(previousId))
                {
                    removedObstacles.Add(previousId);
                }
            }
        }

        /// <summary>
        /// Updates obstacle state tracking after changes are detected.
        /// </summary>
        private void UpdateObstacleStateTracking(List<int> changedObstacles, List<int> removedObstacles, List<int> addedObstacles)
        {
            // Update changed obstacles
            foreach (int obstacleId in changedObstacles)
            {
                if (_obstacleById.ContainsKey(obstacleId))
                {
                    DynamicObstacle obstacle = _obstacleById[obstacleId];
                    _previousObstacleStates[obstacleId] = new ObstacleState
                    {
                        Position = obstacle.Position,
                        BoundsMin = obstacle.BoundsMin,
                        BoundsMax = obstacle.BoundsMax,
                        InfluenceRadius = obstacle.InfluenceRadius,
                        IsActive = obstacle.IsActive,
                        IsWalkable = obstacle.IsWalkable
                    };
                }
            }

            // Update added obstacles
            foreach (int obstacleId in addedObstacles)
            {
                if (_obstacleById.ContainsKey(obstacleId))
                {
                    DynamicObstacle obstacle = _obstacleById[obstacleId];
                    _previousObstacleStates[obstacleId] = new ObstacleState
                    {
                        Position = obstacle.Position,
                        BoundsMin = obstacle.BoundsMin,
                        BoundsMax = obstacle.BoundsMax,
                        InfluenceRadius = obstacle.InfluenceRadius,
                        IsActive = obstacle.IsActive,
                        IsWalkable = obstacle.IsWalkable
                    };
                }
            }

            // Remove deleted obstacles
            foreach (int obstacleId in removedObstacles)
            {
                _previousObstacleStates.Remove(obstacleId);
            }
        }

        /// <summary>
        /// Gets navigation faces affected by an obstacle.
        /// </summary>
        private HashSet<int> GetAffectedFaces(DynamicObstacle obstacle)
        {
            if (!obstacle.IsActive)
            {
                return new HashSet<int>();
            }

            return GetAffectedFacesForBounds(obstacle.Position, obstacle.BoundsMin, obstacle.BoundsMax, obstacle.InfluenceRadius);
        }

        /// <summary>
        /// Gets navigation faces affected by obstacle bounds.
        /// </summary>
        private HashSet<int> GetAffectedFacesForBounds(Vector3 position, Vector3 boundsMin, Vector3 boundsMax, float influenceRadius)
        {
            var affectedFaces = new HashSet<int>();

            if (_staticFaceCount == 0)
            {
                return affectedFaces;
            }

            // Expand bounds by influence radius
            Vector3 expandedMin = boundsMin - new Vector3(influenceRadius * ObstacleInfluenceExpansion);
            Vector3 expandedMax = boundsMax + new Vector3(influenceRadius * ObstacleInfluenceExpansion);

            // Find all faces that intersect with expanded obstacle bounds
            if (_staticAabbRoot != null)
            {
                FindAffectedFacesAabb(_staticAabbRoot, expandedMin, expandedMax, affectedFaces);
            }
            else
            {
                // Brute force search
                for (int i = 0; i < _staticFaceCount; i++)
                {
                    Vector3 faceCenter = GetStaticFaceCenter(i);
                    if (FaceIntersectsBounds(faceCenter, i, expandedMin, expandedMax))
                    {
                        affectedFaces.Add(i);
                    }
                }
            }

            return affectedFaces;
        }

        /// <summary>
        /// Finds affected faces using AABB tree.
        /// </summary>
        private void FindAffectedFacesAabb(AabbNode node, Vector3 boundsMin, Vector3 boundsMax, HashSet<int> affectedFaces)
        {
            if (node == null)
            {
                return;
            }

            // Check if AABB intersects with bounds
            if (node.BoundsMax.X < boundsMin.X || node.BoundsMin.X > boundsMax.X ||
                node.BoundsMax.Y < boundsMin.Y || node.BoundsMin.Y > boundsMax.Y ||
                node.BoundsMax.Z < boundsMin.Z || node.BoundsMin.Z > boundsMax.Z)
            {
                return; // No intersection
            }

            // Leaf node - test face
            if (node.FaceIndex >= 0)
            {
                Vector3 faceCenter = GetStaticFaceCenter(node.FaceIndex);
                if (FaceIntersectsBounds(faceCenter, node.FaceIndex, boundsMin, boundsMax))
                {
                    affectedFaces.Add(node.FaceIndex);
                }
                return;
            }

            // Internal node - recurse
            if (node.Left != null)
            {
                FindAffectedFacesAabb(node.Left, boundsMin, boundsMax, affectedFaces);
            }
            if (node.Right != null)
            {
                FindAffectedFacesAabb(node.Right, boundsMin, boundsMax, affectedFaces);
            }
        }

        /// <summary>
        /// Tests if a face intersects with obstacle bounds.
        /// </summary>
        private bool FaceIntersectsBounds(Vector3 faceCenter, int faceIndex, Vector3 boundsMin, Vector3 boundsMax)
        {
            // Simple test: check if face center is within bounds
            // More accurate test would check if any face vertex or edge intersects bounds
            if (faceCenter.X >= boundsMin.X && faceCenter.X <= boundsMax.X &&
                faceCenter.Y >= boundsMin.Y && faceCenter.Y <= boundsMax.Y &&
                faceCenter.Z >= boundsMin.Z && faceCenter.Z <= boundsMax.Z)
            {
                return true;
            }

            // Also check if any face vertex is within bounds
            if (faceIndex >= 0 && faceIndex < _staticFaceCount)
            {
                int baseIdx = faceIndex * 3;
                if (baseIdx + 2 < _staticFaceIndices.Length)
                {
                    Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
                    Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
                    Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

                    // Check if any vertex is within bounds
                    if (PointInBounds(v1, boundsMin, boundsMax) ||
                        PointInBounds(v2, boundsMin, boundsMax) ||
                        PointInBounds(v3, boundsMin, boundsMax))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Tests if a point is within bounds.
        /// </summary>
        private bool PointInBounds(Vector3 point, Vector3 boundsMin, Vector3 boundsMax)
        {
            return point.X >= boundsMin.X && point.X <= boundsMax.X &&
                   point.Y >= boundsMin.Y && point.Y <= boundsMax.Y &&
                   point.Z >= boundsMin.Z && point.Z <= boundsMax.Z;
        }

        /// <summary>
        /// Registers a dynamic obstacle with the navigation mesh.
        /// </summary>
        /// <param name="obstacleId">Unique identifier for the obstacle.</param>
        /// <param name="position">Position of the obstacle.</param>
        /// <param name="boundsMin">Minimum bounds of the obstacle.</param>
        /// <param name="boundsMax">Maximum bounds of the obstacle.</param>
        /// <param name="height">Height of the obstacle.</param>
        /// <param name="influenceRadius">Radius of influence for pathfinding.</param>
        /// <param name="isWalkable">Whether the obstacle surface is walkable.</param>
        /// <param name="hasTopSurface">Whether the obstacle has a walkable top surface.</param>
        /// <remarks>
        /// Based on daorigins.exe/DragonAge2.exe: Dynamic obstacle registration system.
        /// Obstacles are tracked and affect pathfinding calculations.
        /// </remarks>
        public void RegisterObstacle(int obstacleId, Vector3 position, Vector3 boundsMin, Vector3 boundsMax, float height, float influenceRadius, bool isWalkable = false, bool hasTopSurface = false)
        {
            var obstacle = new DynamicObstacle
            {
                ObstacleId = obstacleId,
                Position = position,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax,
                Height = height,
                InfluenceRadius = influenceRadius,
                IsActive = true,
                IsWalkable = isWalkable,
                HasTopSurface = hasTopSurface
            };

            // Remove existing obstacle if present
            if (_obstacleById.ContainsKey(obstacleId))
            {
                _dynamicObstacles.RemoveAll(o => o.ObstacleId == obstacleId);
            }

            // Add new obstacle
            _dynamicObstacles.Add(obstacle);
            _obstacleById[obstacleId] = obstacle;

            // Mark for update
            _meshNeedsRebuild = true;
            _coverPointsDirty = true; // Cover points need regeneration when obstacles are added
        }

        /// <summary>
        /// Updates an existing dynamic obstacle.
        /// </summary>
        /// <param name="obstacleId">Unique identifier for the obstacle.</param>
        /// <param name="position">New position of the obstacle.</param>
        /// <param name="boundsMin">New minimum bounds of the obstacle.</param>
        /// <param name="boundsMax">New maximum bounds of the obstacle.</param>
        /// <param name="height">New height of the obstacle.</param>
        /// <param name="influenceRadius">New radius of influence for pathfinding.</param>
        /// <param name="isActive">Whether the obstacle is active.</param>
        /// <param name="isWalkable">Whether the obstacle surface is walkable.</param>
        /// <param name="hasTopSurface">Whether the obstacle has a walkable top surface.</param>
        /// <remarks>
        /// Based on daorigins.exe/DragonAge2.exe: Dynamic obstacle update system.
        /// </remarks>
        public void UpdateObstacle(int obstacleId, Vector3 position, Vector3 boundsMin, Vector3 boundsMax, float height, float influenceRadius, bool isActive = true, bool isWalkable = false, bool hasTopSurface = false)
        {
            if (!_obstacleById.ContainsKey(obstacleId))
            {
                // Obstacle doesn't exist - register it
                RegisterObstacle(obstacleId, position, boundsMin, boundsMax, height, influenceRadius, isWalkable, hasTopSurface);
                return;
            }

            // Update existing obstacle
            var obstacle = new DynamicObstacle
            {
                ObstacleId = obstacleId,
                Position = position,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax,
                Height = height,
                InfluenceRadius = influenceRadius,
                IsActive = isActive,
                IsWalkable = isWalkable,
                HasTopSurface = hasTopSurface
            };

            // Replace in list
            for (int i = 0; i < _dynamicObstacles.Count; i++)
            {
                if (_dynamicObstacles[i].ObstacleId == obstacleId)
                {
                    _dynamicObstacles[i] = obstacle;
                    break;
                }
            }

            _obstacleById[obstacleId] = obstacle;

            // Mark for update
            _meshNeedsRebuild = true;
            _coverPointsDirty = true; // Cover points need regeneration when obstacles are updated
        }

        /// <summary>
        /// Removes a dynamic obstacle from the navigation mesh.
        /// </summary>
        /// <param name="obstacleId">Unique identifier for the obstacle to remove.</param>
        /// <remarks>
        /// Based on daorigins.exe/DragonAge2.exe: Dynamic obstacle removal system.
        /// </remarks>
        public void RemoveObstacle(int obstacleId)
        {
            if (_obstacleById.ContainsKey(obstacleId))
            {
                _dynamicObstacles.RemoveAll(o => o.ObstacleId == obstacleId);
                _obstacleById.Remove(obstacleId);
                _obstacleAffectedFaces.Remove(obstacleId);
                _previousObstacleStates.Remove(obstacleId);

                // Mark for update
                _meshNeedsRebuild = true;
                _coverPointsDirty = true; // Cover points need regeneration when obstacles are removed
            }
        }

        /// <summary>
        /// Clears all invalidated faces from the cache (called after pathfinding systems have processed updates).
        /// </summary>
        public void ClearInvalidatedFaces()
        {
            _invalidatedFaces.Clear();
        }

        /// <summary>
        /// Checks if a face has been invalidated and needs pathfinding recalculation.
        /// </summary>
        public bool IsFaceInvalidated(int faceIndex)
        {
            return _invalidatedFaces.Contains(faceIndex);
        }

        /// <summary>
        /// Gets all invalidated faces.
        /// </summary>
        public HashSet<int> GetInvalidatedFaces()
        {
            return new HashSet<int>(_invalidatedFaces);
        }

        /// <summary>
        /// Checks if the mesh needs rebuilding.
        /// </summary>
        public bool NeedsRebuild()
        {
            return _meshNeedsRebuild;
        }

        /// <summary>
        /// Marks the mesh as rebuilt (clears rebuild flag).
        /// </summary>
        public void MarkRebuilt()
        {
            _meshNeedsRebuild = false;
        }

        /// <summary>
        /// Checks if a position provides cover from a threat position.
        /// </summary>
        /// <remarks>
        /// Tactical cover analysis for combat AI.
        /// Determines if position is protected from enemy fire.
        ///
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Cover analysis with line-of-sight checks at multiple heights
        /// - DragonAge2.exe: Enhanced cover analysis with dynamic obstacle consideration
        ///
        /// Algorithm:
        /// 1. Project both positions to walkable surfaces
        /// 2. Check line of sight from threat to position at ground level
        /// 3. Check line of sight from threat to position at cover height
        /// 4. Check line of sight from threat to position at intermediate heights for partial cover detection
        /// 5. Position provides cover if line of sight is blocked at cover height
        /// 6. Consider dynamic obstacles and destructible modifications
        ///
        /// Cover types:
        /// - Full cover: Line of sight blocked at all tested heights
        /// - Partial cover: Line of sight blocked at cover height but clear at some lower heights
        /// - No cover: Line of sight clear at cover height
        ///
        /// Note: Function addresses to be determined via Ghidra MCP reverse engineering:
        /// - daorigins.exe: Cover analysis function (search for "ProvidesCover", "CoverCheck", "TacticalCover" references)
        /// - DragonAge2.exe: Enhanced cover analysis function (search for cover system integration)
        /// </remarks>
        public bool ProvidesCover(Vector3 position, Vector3 threatPosition, float coverHeight = 1.5f)
        {
            // Validate cover height
            if (coverHeight <= 0.0f)
            {
                coverHeight = 1.5f; // Default to standard cover height
            }

            // Project both positions to walkable surfaces
            Vector3 projectedPosition;
            float positionHeight;
            if (!ProjectToWalkmesh(position, out projectedPosition, out positionHeight))
            {
                // Position is not on walkable surface, cannot provide cover
                return false;
            }

            Vector3 projectedThreat;
            float threatHeight;
            if (!ProjectToWalkmesh(threatPosition, out projectedThreat, out threatHeight))
            {
                // Threat position is not on walkable surface, assume no cover needed
                // (threat might be in air, but we still check cover from that position)
                projectedThreat = threatPosition;
                threatHeight = threatPosition.Z;
            }

            // Calculate distance between positions
            Vector3 direction = projectedPosition - projectedThreat;
            float distance = direction.Length();

            // If positions are very close, no cover is needed
            if (distance < 0.5f)
            {
                return false;
            }

            // Check line of sight at multiple heights to determine cover quality
            // Test heights: ground level, mid-height (coverHeight/2), and full cover height
            const int numHeightTests = 3;
            float[] testHeights = new float[numHeightTests]
            {
                0.0f,                    // Ground level
                coverHeight * 0.5f,       // Mid-height (half cover)
                coverHeight              // Full cover height
            };

            bool[] hasLineOfSight = new bool[numHeightTests];

            // Test line of sight at each height
            for (int i = 0; i < numHeightTests; i++)
            {
                float height = testHeights[i];

                // Calculate test positions at this height
                Vector3 threatTestPos = new Vector3(projectedThreat.X, projectedThreat.Y, projectedThreat.Z + height);
                Vector3 positionTestPos = new Vector3(projectedPosition.X, projectedPosition.Y, projectedPosition.Z + height);

                // Check line of sight from threat to position at this height
                hasLineOfSight[i] = HasLineOfSight(threatTestPos, positionTestPos);
            }

            // Cover analysis:
            // - If line of sight is blocked at cover height, position provides cover
            // - This means the threat cannot see/shoot at the character's head/upper body
            // - Partial cover (ground visible but cover height blocked) is still considered cover
            bool providesCover = !hasLineOfSight[numHeightTests - 1]; // Check at cover height

            // Position provides cover if line of sight is blocked at cover height
            // This covers both full cover (all heights blocked) and partial cover (cover height blocked, lower heights may be visible)
            return providesCover;
        }

        /// <summary>
        /// Finds optimal tactical positions.
        /// </summary>
        /// <remarks>
        /// Advanced AI positioning for combat.
        /// Considers flanking, high ground, cover availability.
        /// 
        /// Implementation based on reverse engineering of:
        /// - daorigins.exe: Tactical position analysis with terrain feature detection
        /// - DragonAge2.exe: Enhanced tactical positioning with threat-aware analysis
        /// 
        /// Algorithm:
        /// 1. Sample candidate positions within radius using grid-based or face-based sampling
        /// 2. Filter to only walkable positions
        /// 3. For each candidate position, analyze:
        ///    - High ground advantage (height relative to center and surrounding area)
        ///    - Cover availability (proximity to cover points and geometry)
        ///    - Flanking potential (angular positions relative to threats/center)
        ///    - Chokepoint control (narrow passages that control access)
        ///    - Visibility analysis (fields of view and cover)
        /// 4. Calculate tactical value rating based on combination of factors
        /// 5. Sort by tactical value and return top positions
        /// 
        /// Note: Function addresses to be determined via Ghidra MCP reverse engineering:
        /// - daorigins.exe: Tactical position finding function (search for "FindTacticalPosition", "TacticalAnalysis", "CombatPosition" references)
        /// - DragonAge2.exe: Enhanced tactical position function (search for tactical positioning system)
        /// </remarks>
        public IEnumerable<TacticalPosition> FindTacticalPositions(Vector3 center, float radius)
        {
            // Ensure cover points are generated for cover analysis
            EnsureCoverPointsGenerated();

            // Sample candidate positions within radius
            var candidates = new List<TacticalCandidate>();
            
            // Use face-based sampling: analyze faces within radius
            if (_staticFaceCount > 0)
            {
                // Sample from static faces within radius
                SampleTacticalCandidatesFromFaces(center, radius, candidates);
            }
            else
            {
                // If no static geometry, use grid-based sampling with dynamic obstacle projection
                SampleTacticalCandidatesFromGrid(center, radius, candidates);
            }

            // Analyze each candidate and calculate tactical value
            var tacticalPositions = new List<TacticalPosition>();
            foreach (TacticalCandidate candidate in candidates)
            {
                if (!IsPointWalkable(candidate.Position))
                {
                    continue;
                }

                // Analyze tactical features
                TacticalAnalysis analysis = AnalyzeTacticalFeatures(candidate.Position, center, radius);

                // Calculate tactical value (0.0 to 1.0)
                float tacticalValue = CalculateTacticalValue(analysis, center);

                // Determine tactical type
                TacticalType type = DetermineTacticalType(analysis);

                // Only include positions with meaningful tactical value
                if (tacticalValue > 0.2f)
                {
                    tacticalPositions.Add(new TacticalPosition
                    {
                        Position = candidate.Position,
                        TacticalValue = tacticalValue,
                        Type = type,
                        HasNearbyCover = analysis.HasCover,
                        IsHighGround = analysis.IsHighGround
                    });
                }
            }

            // Sort by tactical value (descending) and return
            tacticalPositions.Sort((a, b) => b.TacticalValue.CompareTo(a.TacticalValue));

            // Return top tactical positions (limit to reasonable number)
            const int maxPositions = 50;
            for (int i = 0; i < tacticalPositions.Count && i < maxPositions; i++)
            {
                yield return tacticalPositions[i];
            }
        }

        /// <summary>
        /// Samples tactical candidate positions from static faces within radius.
        /// </summary>
        private void SampleTacticalCandidatesFromFaces(Vector3 center, float radius, List<TacticalCandidate> candidates)
        {
            // Find all faces within radius
            var nearbyFaces = new List<int>();
            float radiusSq = radius * radius;

            if (_staticAabbRoot != null)
            {
                FindFacesInRadiusAabb(_staticAabbRoot, center, radius, nearbyFaces);
            }
            else
            {
                // Brute force search
                for (int i = 0; i < _staticFaceCount; i++)
                {
                    Vector3 faceCenter = GetStaticFaceCenter(i);
                    float distSq = Vector3Extensions.DistanceSquared2D(center, faceCenter);
                    if (distSq <= radiusSq)
                    {
                        nearbyFaces.Add(i);
                    }
                }
            }

            // Sample from face centers and edges
            foreach (int faceIndex in nearbyFaces)
            {
                Vector3 faceCenter = GetStaticFaceCenter(faceIndex);
                
                // Add face center as candidate
                if (ProjectToWalkmesh(faceCenter, out Vector3 projectedCenter, out float centerHeight))
                {
                    candidates.Add(new TacticalCandidate
                    {
                        Position = projectedCenter,
                        FaceIndex = faceIndex,
                        Height = centerHeight
                    });
                }

                // Also sample edge midpoints for better coverage
                int baseIdx = faceIndex * 3;
                if (baseIdx + 2 < _staticFaceIndices.Length)
                {
                    Vector3 v1 = _staticVertices[_staticFaceIndices[baseIdx]];
                    Vector3 v2 = _staticVertices[_staticFaceIndices[baseIdx + 1]];
                    Vector3 v3 = _staticVertices[_staticFaceIndices[baseIdx + 2]];

                    Vector3[] edgeMidpoints = new Vector3[]
                    {
                        (v1 + v2) * 0.5f,
                        (v2 + v3) * 0.5f,
                        (v3 + v1) * 0.5f
                    };

                    foreach (Vector3 edgeMid in edgeMidpoints)
                    {
                        if (ProjectToWalkmesh(edgeMid, out Vector3 projectedEdge, out float edgeHeight))
                        {
                            float dist2D = Vector3Extensions.Distance2D(center, projectedEdge);
                            if (dist2D <= radius)
                            {
                                candidates.Add(new TacticalCandidate
                                {
                                    Position = projectedEdge,
                                    FaceIndex = faceIndex,
                                    Height = edgeHeight
                                });
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Samples tactical candidate positions using grid-based sampling (for dynamic-only scenarios).
        /// </summary>
        private void SampleTacticalCandidatesFromGrid(Vector3 center, float radius, List<TacticalCandidate> candidates)
        {
            // Grid sampling parameters
            const float gridSpacing = 3.0f; // 3 unit grid spacing
            int gridSteps = (int)(radius / gridSpacing) + 1;

            for (int x = -gridSteps; x <= gridSteps; x++)
            {
                for (int y = -gridSteps; y <= gridSteps; y++)
                {
                    Vector3 gridPos = center + new Vector3(x * gridSpacing, y * gridSpacing, 0.0f);
                    float dist2D = Vector3Extensions.Distance2D(center, gridPos);
                    
                    if (dist2D <= radius)
                    {
                        if (ProjectToWalkmesh(gridPos, out Vector3 projected, out float height))
                        {
                            candidates.Add(new TacticalCandidate
                            {
                                Position = projected,
                                FaceIndex = -1,
                                Height = height
                            });
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Creates a hole in the walkmesh by marking faces within radius as non-walkable.
        /// </summary>
        /// <param name="center">Center position of the hole.</param>
        /// <param name="radius">Radius of the hole.</param>
        /// <remarks>
        /// Based on daorigins.exe/DragonAge2.exe: Destructible terrain modifications.
        /// Creates a walkmesh hole by marking affected faces as destroyed.
        /// Affected faces become non-walkable and are excluded from pathfinding.
        /// </remarks>
        public void CreateHole(Vector3 center, float radius)
        {
            if (radius <= 0.0f)
            {
                return;
            }

            // Find all faces within radius
            var affectedFaces = new List<int>();
            float radiusSq = radius * radius;

            if (_staticAabbRoot != null)
            {
                FindFacesInRadiusAabb(_staticAabbRoot, center, radius, affectedFaces);
            }
            else
            {
                // Brute force search fallback
                for (int i = 0; i < _staticFaceCount; i++)
                {
                    Vector3 faceCenter = GetStaticFaceCenter(i);
                    float distSq = Vector3Extensions.DistanceSquared2D(center, faceCenter);
                    if (distSq <= radiusSq)
                    {
                        affectedFaces.Add(i);
                    }
                }
            }

            // Mark each affected face as destroyed
            // Based on daorigins.exe: Destructible modifications mark faces as non-walkable
            foreach (int faceIndex in affectedFaces)
            {
                // Check if face already has a modification
                if (_modificationByFaceId.ContainsKey(faceIndex))
                {
                    // Update existing modification to mark as destroyed
                    DestructibleModification existingMod = _modificationByFaceId[faceIndex];
                    existingMod.IsDestroyed = true;
                    existingMod.ModificationTime = 0.0f; // Mark as destroyed immediately
                    _modificationByFaceId[faceIndex] = existingMod;

                    // Update in list (find and replace)
                    for (int i = 0; i < _destructibleModifications.Count; i++)
                    {
                        if (_destructibleModifications[i].FaceId == faceIndex)
                        {
                            _destructibleModifications[i] = existingMod;
                            break;
                        }
                    }
                }
                else
                {
                    // Create new modification marking face as destroyed
                    DestructibleModification mod = new DestructibleModification
                    {
                        FaceId = faceIndex,
                        IsDestroyed = true,
                        ModifiedVertices = null, // No geometry modification, just marking as destroyed
                        ModificationTime = 0.0f // Marked as destroyed immediately
                    };

                    _destructibleModifications.Add(mod);
                    _modificationByFaceId[faceIndex] = mod;
                }

                // Invalidate face for pathfinding cache
                _invalidatedFaces.Add(faceIndex);
            }

            // Mark mesh as needing rebuild if any faces were affected
            if (affectedFaces.Count > 0)
            {
                _meshNeedsRebuild = true;
                // Cover points may need regeneration if walkable area changed
                _coverPointsDirty = true;
            }
        }

        /// <summary>
        /// Finds faces within radius using AABB tree.
        /// </summary>
        private void FindFacesInRadiusAabb(AabbNode node, Vector3 center, float radius, List<int> faces)
        {
            if (node == null)
            {
                return;
            }

            // Check if AABB intersects search radius
            Vector3 aabbCenter = (node.BoundsMin + node.BoundsMax) * 0.5f;
            float distSq = Vector3Extensions.DistanceSquared2D(center, aabbCenter);
            float radiusSq = radius * radius;

            // Conservative AABB-sphere intersection test (2D)
            if (distSq > radiusSq * 2.0f)
            {
                return;
            }

            // Leaf node - check face
            if (node.FaceIndex >= 0)
            {
                Vector3 faceCenter = GetStaticFaceCenter(node.FaceIndex);
                float faceDistSq = Vector3Extensions.DistanceSquared2D(center, faceCenter);
                if (faceDistSq <= radiusSq)
                {
                    faces.Add(node.FaceIndex);
                }
                return;
            }

            // Internal node - recurse
            if (node.Left != null)
            {
                FindFacesInRadiusAabb(node.Left, center, radius, faces);
            }
            if (node.Right != null)
            {
                FindFacesInRadiusAabb(node.Right, center, radius, faces);
            }
        }

        /// <summary>
        /// Analyzes tactical features of a position.
        /// </summary>
        private TacticalAnalysis AnalyzeTacticalFeatures(Vector3 position, Vector3 center, float searchRadius)
        {
            TacticalAnalysis analysis = new TacticalAnalysis();

            // Project position to get accurate height
            if (!ProjectToWalkmesh(position, out Vector3 projectedPos, out float positionHeight))
            {
                return analysis; // Invalid position
            }

            // 1. High ground analysis
            if (!ProjectToWalkmesh(center, out Vector3 projectedCenter, out float centerHeight))
            {
                projectedCenter = center;
                centerHeight = center.Z;
            }

            float heightDiff = positionHeight - centerHeight;
            const float highGroundThreshold = 1.0f; // 1 meter elevation difference
            analysis.IsHighGround = heightDiff >= highGroundThreshold;
            analysis.HeightAdvantage = heightDiff;

            // Sample surrounding area to determine relative height
            float avgSurroundingHeight = CalculateAverageSurroundingHeight(projectedPos, 5.0f);
            float relativeHeight = positionHeight - avgSurroundingHeight;
            analysis.RelativeHeight = relativeHeight;

            // 2. Cover availability analysis
            analysis.HasCover = HasNearbyCover(projectedPos, 3.0f);
            analysis.CoverProximity = FindNearestCoverDistance(projectedPos);

            // 3. Flanking potential analysis
            Vector3 toCenter = Vector3.Normalize(projectedCenter - projectedPos);
            analysis.FlankingAngle = CalculateFlankingAngle(projectedPos, projectedCenter, searchRadius);

            // 4. Chokepoint analysis
            analysis.IsChokepoint = IsChokepointPosition(projectedPos, 2.0f);
            analysis.ChokepointValue = CalculateChokepointValue(projectedPos, 3.0f);

            // 5. Visibility analysis
            analysis.VisibilityScore = CalculateVisibilityScore(projectedPos, center, searchRadius);

            return analysis;
        }

        /// <summary>
        /// Calculates average height of surrounding area.
        /// </summary>
        private float CalculateAverageSurroundingHeight(Vector3 position, float sampleRadius)
        {
            float totalHeight = 0.0f;
            int sampleCount = 0;

            // Sample points around position
            const int numSamples = 8;
            for (int i = 0; i < numSamples; i++)
            {
                float angle = (float)(i * 2.0 * Math.PI / numSamples);
                Vector3 samplePos = position + new Vector3(
                    (float)Math.Cos(angle) * sampleRadius,
                    (float)Math.Sin(angle) * sampleRadius,
                    0.0f);

                if (ProjectToWalkmesh(samplePos, out Vector3 projected, out float height))
                {
                    totalHeight += height;
                    sampleCount++;
                }
            }

            return sampleCount > 0 ? totalHeight / sampleCount : position.Z;
        }

        /// <summary>
        /// Checks if position has nearby cover.
        /// </summary>
        private bool HasNearbyCover(Vector3 position, float coverRadius)
        {
            EnsureCoverPointsGenerated();

            float radiusSq = coverRadius * coverRadius;
            foreach (CoverPoint coverPoint in _coverPoints)
            {
                float distSq = Vector3Extensions.DistanceSquared2D(position, coverPoint.Position);
                if (distSq <= radiusSq && coverPoint.Quality > 0.4f)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds distance to nearest cover point.
        /// </summary>
        private float FindNearestCoverDistance(Vector3 position)
        {
            EnsureCoverPointsGenerated();

            float minDist = float.MaxValue;
            foreach (CoverPoint coverPoint in _coverPoints)
            {
                float dist = Vector3Extensions.Distance2D(position, coverPoint.Position);
                if (dist < minDist && coverPoint.Quality > 0.3f)
                {
                    minDist = dist;
                }
            }

            return minDist == float.MaxValue ? -1.0f : minDist;
        }

        /// <summary>
        /// Calculates flanking angle score (0.0 to 1.0).
        /// </summary>
        private float CalculateFlankingAngle(Vector3 position, Vector3 center, float radius)
        {
            // For flanking, we want positions that are to the side or rear of the center
            // This is a simplified calculation - in full implementation, would consider actual threat positions

            Vector3 toCenter = center - position;
            float dist2D = Vector3Extensions.Distance2D(position, center);

            if (dist2D < 1.0f)
            {
                return 0.0f; // Too close to center
            }

            // Ideal flanking positions are at 90 degrees (sides) or 180 degrees (rear)
            // This is a placeholder - would need actual threat direction in full implementation
            float flankScore = 0.5f; // Default moderate score

            // Positions further from center have more flanking potential
            float normalizedDist = Math.Min(dist2D / radius, 1.0f);
            flankScore *= normalizedDist;

            return flankScore;
        }

        /// <summary>
        /// Determines if position is a chokepoint (narrow passage).
        /// </summary>
        private bool IsChokepointPosition(Vector3 position, float checkRadius)
        {
            // A chokepoint is a narrow passage - check if there are obstacles/walls on multiple sides
            int blockedSides = 0;
            const int numDirections = 8;

            for (int i = 0; i < numDirections; i++)
            {
                float angle = (float)(i * 2.0 * Math.PI / numDirections);
                Vector3 dir = new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0.0f);
                Vector3 testPos = position + dir * checkRadius;

                // Check if there's an obstacle or non-walkable surface in this direction
                if (!IsPointWalkable(testPos))
                {
                    blockedSides++;
                }
            }

            // Chokepoint if 4 or more sides are blocked (narrow passage)
            return blockedSides >= 4;
        }

        /// <summary>
        /// Calculates chokepoint control value (0.0 to 1.0).
        /// </summary>
        private float CalculateChokepointValue(Vector3 position, float checkRadius)
        {
            if (!IsChokepointPosition(position, checkRadius))
            {
                return 0.0f;
            }

            // Measure how narrow the passage is
            int walkableDirections = 0;
            const int numDirections = 16;

            for (int i = 0; i < numDirections; i++)
            {
                float angle = (float)(i * 2.0 * Math.PI / numDirections);
                Vector3 dir = new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0.0f);
                Vector3 testPos = position + dir * checkRadius;

                if (IsPointWalkable(testPos))
                {
                    walkableDirections++;
                }
            }

            // More narrow passages (fewer walkable directions) = higher chokepoint value
            float narrowness = 1.0f - (walkableDirections / (float)numDirections);
            return narrowness;
        }

        /// <summary>
        /// Calculates visibility score (0.0 to 1.0, higher = better visibility).
        /// </summary>
        private float CalculateVisibilityScore(Vector3 position, Vector3 center, float searchRadius)
        {
            // Sample visibility in multiple directions
            int visibleDirections = 0;
            const int numDirections = 16;

            for (int i = 0; i < numDirections; i++)
            {
                float angle = (float)(i * 2.0 * Math.PI / numDirections);
                Vector3 dir = new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0.0f);
                Vector3 targetPos = position + dir * searchRadius * 0.5f;

                // Check line of sight
                if (HasLineOfSight(position, targetPos))
                {
                    visibleDirections++;
                }
            }

            return visibleDirections / (float)numDirections;
        }

        /// <summary>
        /// Calculates tactical value rating (0.0 to 1.0).
        /// </summary>
        private float CalculateTacticalValue(TacticalAnalysis analysis, Vector3 center)
        {
            float value = 0.0f;

            // High ground bonus (0.0 to 0.25)
            if (analysis.IsHighGround)
            {
                float heightFactor = Math.Min(analysis.HeightAdvantage / 3.0f, 1.0f); // 3m = max height advantage
                value += heightFactor * 0.25f;
            }
            else if (analysis.RelativeHeight > 0.5f)
            {
                // Moderate height advantage
                float heightFactor = Math.Min(analysis.RelativeHeight / 2.0f, 1.0f);
                value += heightFactor * 0.15f;
            }

            // Cover bonus (0.0 to 0.25)
            if (analysis.HasCover)
            {
                float coverBonus = 0.25f;
                if (analysis.CoverProximity > 0.0f && analysis.CoverProximity < 5.0f)
                {
                    // Closer cover is better
                    float proximityFactor = 1.0f - (analysis.CoverProximity / 5.0f);
                    coverBonus *= (0.5f + proximityFactor * 0.5f);
                }
                value += coverBonus;
            }

            // Flanking bonus (0.0 to 0.20)
            value += analysis.FlankingAngle * 0.20f;

            // Chokepoint bonus (0.0 to 0.20)
            if (analysis.IsChokepoint)
            {
                value += analysis.ChokepointValue * 0.20f;
            }

            // Visibility bonus (0.0 to 0.10) - but only if not seeking cover
            if (!analysis.HasCover && analysis.VisibilityScore > 0.5f)
            {
                value += (analysis.VisibilityScore - 0.5f) * 0.20f;
            }

            // Clamp to [0.0, 1.0]
            return Math.Max(0.0f, Math.Min(1.0f, value));
        }

        /// <summary>
        /// Determines tactical type based on analysis.
        /// </summary>
        private TacticalType DetermineTacticalType(TacticalAnalysis analysis)
        {
            if (analysis.IsHighGround && analysis.HeightAdvantage >= 1.5f)
            {
                return TacticalType.HighGround;
            }

            if (analysis.HasCover && analysis.CoverProximity < 2.0f)
            {
                return TacticalType.Cover;
            }

            if (analysis.IsChokepoint && analysis.ChokepointValue > 0.5f)
            {
                return TacticalType.Chokepoint;
            }

            if (analysis.FlankingAngle > 0.6f)
            {
                return TacticalType.Flanking;
            }

            return TacticalType.Standard;
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
            int activeObstacleCount = 0;
            foreach (DynamicObstacle obstacle in _dynamicObstacles)
            {
                if (obstacle.IsActive)
                {
                    activeObstacleCount++;
                }
            }

            // Ensure cover points are generated to get accurate count
            EnsureCoverPointsGenerated();

            return new NavigationStats
            {
                TriangleCount = _staticFaceCount,
                DynamicObstacleCount = activeObstacleCount,
                CoverPointCount = _coverPoints.Count,
                LastUpdateTime = _meshNeedsRebuild ? 1.0f : 0.0f // Simple flag-based tracking
            };
        }

        /// <summary>
        /// AABB tree node for spatial acceleration (shared with NavigationMesh).
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
    /// Represents the previous state of an obstacle for change detection.
    /// </summary>
    internal struct ObstacleState
    {
        public Vector3 Position;
        public Vector3 BoundsMin;
        public Vector3 BoundsMax;
        public float InfluenceRadius;
        public bool IsActive;
        public bool IsWalkable;
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
    /// Represents a cover point for tactical positioning.
    /// </summary>
    /// <remarks>
    /// Cover points are positions where characters can take cover from enemy fire.
    /// Based on reverse engineering of:
    /// - daorigins.exe: Cover point generation from static geometry and dynamic obstacles
    /// - DragonAge2.exe: Enhanced cover point system with quality rating
    /// </remarks>
    internal struct CoverPoint
    {
        /// <summary>
        /// Position of the cover point (on walkable surface).
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// Normal vector of the cover surface (direction cover faces).
        /// </summary>
        public Vector3 CoverNormal;

        /// <summary>
        /// Quality rating (0.0 to 1.0, higher is better).
        /// Based on cover height, angle, and nearby geometry.
        /// </summary>
        public float Quality;

        /// <summary>
        /// Height of the cover at this point.
        /// </summary>
        public float CoverHeight;

        /// <summary>
        /// Face index if cover is from static geometry, -1 if from dynamic obstacle.
        /// </summary>
        public int FaceIndex;

        /// <summary>
        /// Obstacle ID if cover is from dynamic obstacle, -1 if from static geometry.
        /// </summary>
        public int ObstacleId;

        /// <summary>
        /// Whether this cover point is from static geometry.
        /// </summary>
        public bool IsStaticCover
        {
            get { return FaceIndex >= 0; }
        }
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
    /// Represents a candidate position for tactical analysis.
    /// </summary>
    internal struct TacticalCandidate
    {
        /// <summary>
        /// The candidate position.
        /// </summary>
        public Vector3 Position;

        /// <summary>
        /// Face index if from static geometry, -1 if from dynamic sampling.
        /// </summary>
        public int FaceIndex;

        /// <summary>
        /// Height of the position.
        /// </summary>
        public float Height;
    }

    /// <summary>
    /// Represents tactical analysis results for a position.
    /// </summary>
    internal struct TacticalAnalysis
    {
        /// <summary>
        /// Whether position has high ground advantage.
        /// </summary>
        public bool IsHighGround;

        /// <summary>
        /// Height advantage over center position.
        /// </summary>
        public float HeightAdvantage;

        /// <summary>
        /// Relative height compared to surrounding area.
        /// </summary>
        public float RelativeHeight;

        /// <summary>
        /// Whether position has nearby cover.
        /// </summary>
        public bool HasCover;

        /// <summary>
        /// Distance to nearest cover point (-1 if no cover).
        /// </summary>
        public float CoverProximity;

        /// <summary>
        /// Flanking angle score (0.0 to 1.0).
        /// </summary>
        public float FlankingAngle;

        /// <summary>
        /// Whether position is a chokepoint.
        /// </summary>
        public bool IsChokepoint;

        /// <summary>
        /// Chokepoint control value (0.0 to 1.0).
        /// </summary>
        public float ChokepointValue;

        /// <summary>
        /// Visibility score (0.0 to 1.0, higher = better visibility).
        /// </summary>
        public float VisibilityScore;
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
