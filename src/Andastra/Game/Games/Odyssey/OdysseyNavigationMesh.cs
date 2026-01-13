using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Navigation;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey
{
    /// <summary>
    /// Odyssey Engine walkmesh implementation for navigation and collision detection.
    /// </summary>
    /// <remarks>
    /// Odyssey Navigation Mesh Implementation (Engine-Common):
    /// - Common walkmesh functionality shared by both KOTOR 1 and KOTOR 2
    /// - Based on walkmesh format used in KotOR/KotOR2
    /// - Provides pathfinding and collision detection
    /// - Supports point projection to walkable surfaces
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Walkmesh loading and navigation functions
    /// - swkotor2.exe: Walkmesh projection (FUN_004f5070 @ 0x004f5070)
    /// - swkotor2.exe: Line-of-sight raycast (UpdateCreatureMovement @ 0x0054be70) - performs walkmesh raycasts for visibility checks
    /// - Walkmesh binary format: Vertices, faces, adjacency information
    ///
    /// Walkmesh features:
    /// - Triangle-based mesh for walkable surfaces
    /// - Collision detection against unwalkable geometry
    /// - Pathfinding support with A* algorithm on face adjacency graph
    /// - Point projection for accurate positioning
    /// - Raycast for line-of-sight and collision detection
    /// - Surface material walkability based on surfacemat.2da
    /// - Same walkmesh data structures and algorithms
    ///
    /// Game-specific implementations:
    /// - Kotor1NavigationMesh: swkotor.exe specific function addresses and behavior
    /// - Kotor2NavigationMesh: swkotor2.exe specific function addresses and behavior
    ///
    /// Note: Function addresses and game-specific behavior differences are documented
    /// in the game-specific subclasses (Kotor1NavigationMesh, Kotor2NavigationMesh).
    /// This class contains only functionality that is identical between both games.
    /// </remarks>
    [PublicAPI]
    public class OdysseyNavigationMesh : BaseNavigationMesh
    {
        // Walkmesh data structures
        private Vector3[] _vertices;
        private int[] _faceIndices;        // 3 vertex indices per face
        private int[] _adjacency;          // 3 adjacency entries per face (-1 = no neighbor)
        private int[] _surfaceMaterials;   // Material per face
        private NavigationMesh.AabbNode _aabbRoot;
        private int _faceCount;

        // Surface material walkability lookup (based on surfacemat.2da)
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

        /// <summary>
        /// Creates an empty Odyssey navigation mesh.
        /// </summary>
        public OdysseyNavigationMesh()
        {
            _vertices = new Vector3[0];
            _faceIndices = new int[0];
            _adjacency = new int[0];
            _surfaceMaterials = new int[0];
            _aabbRoot = null;
            _faceCount = 0;
        }

        /// <summary>
        /// Creates an Odyssey navigation mesh from walkmesh data.
        /// </summary>
        /// <param name="vertices">Walkmesh vertices.</param>
        /// <param name="faceIndices">Face vertex indices (3 per face).</param>
        /// <param name="adjacency">Face adjacency data (3 per face, -1 = no neighbor).</param>
        /// <param name="surfaceMaterials">Surface material indices per face.</param>
        /// <param name="aabbRoot">AABB tree root for spatial acceleration.</param>
        public OdysseyNavigationMesh(
            Vector3[] vertices,
            int[] faceIndices,
            int[] adjacency,
            int[] surfaceMaterials,
            NavigationMesh.AabbNode aabbRoot)
        {
            _vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
            _faceIndices = faceIndices ?? throw new ArgumentNullException(nameof(faceIndices));
            _adjacency = adjacency ?? new int[0];
            _surfaceMaterials = surfaceMaterials ?? throw new ArgumentNullException(nameof(surfaceMaterials));
            _aabbRoot = aabbRoot;
            _faceCount = faceIndices.Length / 3;
        }
        /// <summary>
        /// Tests if a point is on walkable ground.
        /// </summary>
        /// <remarks>
        /// Based on walkmesh projection logic in swkotor2.exe.
        /// Checks if point can be projected onto a walkable triangle.
        /// 
        /// Algorithm:
        /// 1. Find face at point using FindFaceAt (2D projection)
        /// 2. Check if face is walkable
        /// 3. Project point to face and verify it's within face bounds
        /// </remarks>
        public override bool IsPointWalkable(Vector3 point)
        {
            // Find face at point (2D projection)
            int faceIndex = FindFaceAt(point);
            if (faceIndex < 0)
            {
                return false;
            }

            // Check if face is walkable
            if (!IsWalkable(faceIndex))
            {
                return false;
            }

            // Point is on walkable surface
            return true;
        }

        /// <summary>
        /// Projects a point onto the walkmesh surface.
        /// </summary>
        /// <remarks>
        /// Based on FUN_004f5070 @ 0x004f5070 in swkotor2.exe.
        /// 
        /// Ghidra analysis (swkotor2.exe: 0x004f5070):
        /// - Signature: `float10 __thiscall FUN_004f5070(void *param_1, float *param_2, int param_3, int *param_4, int *param_5)`
        /// - param_1: Walkmesh object pointer (this)
        /// - param_2: Input point (float[3] = x, y, z)
        /// - param_3: Projection mode (0 = 3D projection, 1 = 2D projection)
        /// - param_4: Output face index pointer (optional, can be null)
        /// - param_5: Additional parameter (used for 2D projection)
        /// - Returns: Height (float10) at projected point
        /// 
        /// Algorithm:
        /// 1. Calls FUN_004f4260 to find face at point (uses AABB tree or brute force)
        /// 2. If face found (iVar1 != 0):
        ///    - If param_3 != 0: Calls FUN_0055b210 for 2D projection (XZ plane)
        ///    - Otherwise: Calls FUN_0055b1d0 for 3D projection (full 3D plane)
        /// 3. Returns height at projected point
        /// 
        /// FUN_004f4260 (FindFaceAt):
        /// - Searches for face containing point using AABB tree or brute force
        /// - Checks point against faces with vertical tolerance (_DAT_007b56f8)
        /// - Returns face pointer or null
        /// 
        /// FUN_0055b1d0 (3D Projection):
        /// - Projects point onto 3D plane of face
        /// - Uses face normal and plane equation
        /// - Returns height (Z coordinate) at projected point
        /// 
        /// FUN_0055b210 (2D Projection):
        /// - Projects point onto XZ plane (2D projection)
        /// - Uses barycentric coordinates for height interpolation
        /// - Returns height (Z coordinate) at projected point
        /// 
        /// Called from:
        /// - UpdateCreatureMovement @ 0x0054be70 (line-of-sight and pathfinding)
        /// - FUN_00553970 @ 0x00553970 (creature movement)
        /// - FUN_005522e0 @ 0x005522e0 (entity positioning)
        /// - FUN_004dc300 @ 0x004dc300 (area transition projection)
        /// - FUN_00517d50 @ 0x00517d50 (AI pathfinding)
        /// - And 29 other call sites throughout the engine
        /// 
        /// This implementation:
        /// - Uses FindFaceAt to locate face (equivalent to FUN_004f4260)
        /// - Projects point onto face plane using barycentric interpolation (equivalent to FUN_0055b1d0)
        /// - Returns projected position and height
        /// </remarks>
        public override bool ProjectToWalkmesh(Vector3 point, out Vector3 result, out float height)
        {
            result = point;
            height = point.Y;

            // Find face at point (equivalent to FUN_004f4260)
            int faceIndex = FindFaceAt(point);
            if (faceIndex < 0)
            {
                return false;
            }

            // Get face vertices
            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _faceIndices.Length)
            {
                return false;
            }

            Vector3 v1 = _vertices[_faceIndices[baseIdx]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v3 = _vertices[_faceIndices[baseIdx + 2]];

            // Project point onto face plane (equivalent to FUN_0055b1d0 - 3D projection)
            // Calculate face normal
            Vector3 edge1 = v2 - v1;
            Vector3 edge2 = v3 - v1;
            Vector3 normal = Vector3.Cross(edge1, edge2);

            // Avoid division by zero for vertical faces
            if (Math.Abs(normal.Z) < 1e-6f)
            {
                // Vertical face - use average height
                height = (v1.Y + v2.Y + v3.Y) / 3f;
                result = new Vector3(point.X, point.Y, height);
                return true;
            }

            // Plane equation: ax + by + cz + d = 0
            // Solve for z: z = (-d - ax - by) / c
            float d = -Vector3.Dot(normal, v1);
            float z = (-d - normal.X * point.X - normal.Y * point.Y) / normal.Z;

            height = z;
            result = new Vector3(point.X, point.Y, z);
            return true;
        }

        /// <summary>
        /// Finds a path between two points.
        /// </summary>
        /// <remarks>
        /// Implements A* pathfinding on walkmesh triangles.
        /// Returns waypoints for entity movement.
        /// Delegates to the INavigationMesh.FindPath implementation.
        /// </remarks>
        public bool FindPath(Vector3 start, Vector3 end, out Vector3[] waypoints)
        {
            IList<Vector3> path = FindPath(start, end);
            if (path != null && path.Count > 0)
            {
                waypoints = path.ToArray();
                return true;
            }
            waypoints = null;
            return false;
        }

        /// <summary>
        /// Gets the height at a specific point.
        /// </summary>
        /// <remarks>
        /// Returns the walkable height at the given X,Z coordinates.
        /// Returns false if point is not over walkable surface.
        /// 
        /// Based on FUN_004f5070 @ 0x004f5070 in swkotor2.exe.
        public override Vector3? ProjectPoint(Vector3 point)
        {
            if (ProjectToWalkmesh(point, out Vector3 result, out float height))
            {
                return result;
            }
            return null;
        }

        /// Uses same projection logic as ProjectToWalkmesh but only returns height.
        /// </remarks>
        public bool GetHeightAtPoint(Vector3 point, out float height)
        {
            Vector3 projected;
            if (ProjectToWalkmesh(point, out projected, out height))
            {
                // Verify the projected point is on a walkable face
                int faceIndex = FindFaceAt(projected);
                if (faceIndex >= 0 && IsWalkable(faceIndex))
                {
                    return true;
                }
            }

            height = point.Y;
            return false;
        }

        /// <summary>
        /// Checks line of sight between two points.
        /// </summary>
        /// <remarks>
        /// Tests if line segment between points doesn't intersect unwalkable geometry.
        /// Used for AI perception and projectile collision.
        /// 
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks.
        /// The original implementation:
        /// 1. Performs a raycast from start to end position
        /// 2. If a hit is found, checks if the hit face is walkable (walkable faces don't block line of sight)
        /// 3. Also checks if the hit point is very close to the destination (within tolerance) - if so, line of sight is considered clear
        /// 4. Returns true if no obstruction or if the obstruction is walkable/close to destination
        /// 
        /// This implementation matches the behavior used by:
        /// - Perception system for AI visibility checks
        /// - Projectile collision detection
        /// - Movement collision detection
        /// </remarks>
        /// <summary>
        /// Checks line of sight between two points.
        /// </summary>
        /// <remarks>
        /// Odyssey-specific: Uses base class common algorithm with walkable face checks.
        /// </remarks>
        public new bool HasLineOfSight(Vector3 start, Vector3 end)
        {
            return base.HasLineOfSight(start, end);
        }

        /// <summary>
        /// Odyssey-specific check: walkable faces don't block line of sight.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe: walkable faces allow line of sight
        /// (e.g., through doorways, over walkable terrain).
        /// </remarks>
        protected override bool CheckHitBlocksLineOfSight(Vector3 hitPoint, int hitFace, Vector3 start, Vector3 end)
        {
            // Check if the hit face is walkable - walkable faces don't block line of sight
            // This allows entities to see through walkable surfaces (e.g., through doorways, over walkable terrain)
            if (hitFace >= 0 && IsWalkable(hitFace))
            {
                return true; // Hit a walkable face, line of sight is clear
            }

            // Hit a non-walkable face that blocks line of sight
            return false;
        }

        // INavigationMesh interface methods

        /// <summary>
        /// Finds a path from start to goal using A* algorithm on walkmesh adjacency graph.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe walkmesh pathfinding system.
        /// Implements A* pathfinding algorithm over face adjacency graph.
        /// Uses face centers as waypoints and applies path smoothing for natural movement.
        /// 
        /// Algorithm:
        /// 1. Find start and goal faces
        /// 2. Validate both are walkable
        /// 3. If same face, return direct path
        /// 4. Run A* search over face adjacency graph
        /// 5. Reconstruct path from face sequence
        /// 6. Apply path smoothing using line-of-sight checks
        /// 
        /// Based on reverse engineering of:
        /// - swkotor.exe: Walkmesh pathfinding functions
        /// - swkotor2.exe: A* pathfinding implementation on walkmesh adjacency
        /// - Error messages: "failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
        /// - Error messages: "failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
        /// </remarks>
        public override IList<Vector3> FindPath(Vector3 start, Vector3 goal)
        {
            // Find faces containing start and goal positions
            int startFace = FindFaceAt(start);
            int goalFace = FindFaceAt(goal);

            // Validate both positions are on walkable surfaces
            if (startFace < 0 || goalFace < 0)
            {
                return null;  // Not on walkable surface
            }

            if (!IsWalkable(startFace) || !IsWalkable(goalFace))
            {
                return null;  // Start or goal is not walkable
            }

            // Same face - direct path
            if (startFace == goalFace)
            {
                return new List<Vector3> { start, goal };
            }

            // A* pathfinding over face adjacency graph
            var openSet = new SortedSet<FaceScore>(new FaceScoreComparer());
            var cameFrom = new Dictionary<int, int>();
            var gScore = new Dictionary<int, float>();
            var fScore = new Dictionary<int, float>();
            var inOpenSet = new HashSet<int>();

            // Initialize start node
            gScore[startFace] = 0f;
            fScore[startFace] = Heuristic(startFace, goalFace);
            openSet.Add(new FaceScore(startFace, fScore[startFace]));
            inOpenSet.Add(startFace);

            // A* main loop
            while (openSet.Count > 0)
            {
                // Get face with lowest f-score
                FaceScore currentScore = GetMin(openSet);
                openSet.Remove(currentScore);
                int current = currentScore.FaceIndex;
                inOpenSet.Remove(current);

                // Goal reached - reconstruct path
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
                        continue;  // Neighbor is not walkable
                    }

                    // Calculate tentative g-score
                    float tentativeG;
                    if (gScore.TryGetValue(current, out float currentG))
                    {
                        tentativeG = currentG + EdgeCost(current, neighbor);
                    }
                    else
                    {
                        tentativeG = EdgeCost(current, neighbor);
                    }

                    // Update if this path is better
                    float neighborG;
                    if (!gScore.TryGetValue(neighbor, out neighborG) || tentativeG < neighborG)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeG;
                        float newF = tentativeG + Heuristic(neighbor, goalFace);
                        fScore[neighbor] = newF;

                        // Add to open set if not already there
                        if (!inOpenSet.Contains(neighbor))
                        {
                            openSet.Add(new FaceScore(neighbor, newF));
                            inOpenSet.Add(neighbor);
                        }
                    }
                }
            }

            // No path found
            return null;
        }

        /// <summary>
        /// Finds a path from start to goal while avoiding obstacles.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FindPathAroundObstacle @ 0x0061c390 - pathfinding around obstacles
        /// Function signature: `float* FindPathAroundObstacle(void* this, int* movingCreature, void* blockingCreature)`
        /// Called from UpdateCreatureMovement @ 0x0054be70 (line 183) when creature collision detected
        /// Equivalent in swkotor.exe: FindPathAroundObstacle @ 0x005d0840 (called from UpdateCreatureMovement @ 0x00516630, line 254)
        /// </summary>
        public override IList<Vector3> FindPathAroundObstacles(Vector3 start, Vector3 goal, IList<ObstacleInfo> obstacles)
        {
            // Find faces containing start and goal positions
            int startFace = FindFaceAt(start);
            int goalFace = FindFaceAt(goal);

            // Validate both positions are on walkable surfaces
            if (startFace < 0 || goalFace < 0)
            {
                return null;  // Not on walkable surface
            }

            if (!IsWalkable(startFace) || !IsWalkable(goalFace))
            {
                return null;  // Start or goal is not walkable
            }

            // Same face - check if obstacle blocks direct path
            if (startFace == goalFace)
            {
                if (obstacles != null && obstacles.Count > 0)
                {
                    Vector3 direction = goal - start;
                    float distance = direction.Length();
                    if (distance > 0.001f)
                    {
                        direction = Vector3.Normalize(direction);
                        // Check if any obstacle blocks the direct path
                        foreach (ObstacleInfo obstacle in obstacles)
                        {
                            Vector3 toObstacle = obstacle.Position - start;
                            float projectionLength = Vector3.Dot(toObstacle, direction);
                            if (projectionLength >= 0f && projectionLength <= distance)
                            {
                                Vector3 closestPoint = start + direction * projectionLength;
                                float distanceToObstacle = Vector3.Distance(closestPoint, obstacle.Position);
                                if (distanceToObstacle < obstacle.Radius)
                                {
                                    // Obstacle blocks direct path - try adjacent faces
                                    return FindPathAroundObstacleOnSameFace(start, goal, startFace, obstacles);
                                }
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

            // A* pathfinding over face adjacency graph (same as FindPath but with obstacle avoidance)
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
                FaceScore currentScore = GetMin(openSet);
                openSet.Remove(currentScore);
                int current = currentScore.FaceIndex;
                inOpenSet.Remove(current);

                if (current == goalFace)
                {
                    return ReconstructPath(cameFrom, current, start, goal);
                }

                foreach (int neighbor in GetAdjacentFaces(current))
                {
                    if (neighbor < 0 || neighbor >= _faceCount)
                    {
                        continue;
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

            // No path found - try with expanded obstacle radius
            if (obstacles != null && obstacles.Count > 0)
            {
                var expandedObstacles = new List<ObstacleInfo>();
                foreach (ObstacleInfo obstacle in obstacles)
                {
                    expandedObstacles.Add(new ObstacleInfo(obstacle.Position, obstacle.Radius * 1.5f));
                }
                return FindPathAroundObstacles(start, goal, expandedObstacles);
            }

            return null;
        }

        /// <summary>
        /// Finds a path around an obstacle when start and goal are on the same face.
        /// </summary>
        private IList<Vector3> FindPathAroundObstacleOnSameFace(Vector3 start, Vector3 goal, int faceIndex, IList<ObstacleInfo> obstacles)
        {
            var candidateFaces = new List<int>();
            foreach (int neighbor in GetAdjacentFaces(faceIndex))
            {
                if (neighbor >= 0 && neighbor < _faceCount && IsWalkable(neighbor))
                {
                    bool isBlocked = false;
                    if (obstacles != null)
                    {
                        Vector3 neighborCenter = GetFaceCenter(neighbor);
                        foreach (ObstacleInfo obstacle in obstacles)
                        {
                            if (Vector3.Distance(neighborCenter, obstacle.Position) < obstacle.Radius)
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
                return null;
            }

            foreach (int candidateFace in candidateFaces)
            {
                Vector3 candidateCenter = GetFaceCenter(candidateFace);
                IList<Vector3> path = FindPathAroundObstacles(candidateCenter, goal, obstacles);
                if (path != null && path.Count > 0)
                {
                    var fullPath = new List<Vector3> { start };
                    foreach (Vector3 waypoint in path)
                    {
                        fullPath.Add(waypoint);
                    }
                    return fullPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Builds a set of face indices that are blocked by obstacles.
        /// </summary>
        private HashSet<int> BuildBlockedFacesSet(IList<ObstacleInfo> obstacles)
        {
            var blockedFaces = new HashSet<int>();

            foreach (ObstacleInfo obstacle in obstacles)
            {
                for (int i = 0; i < _faceCount; i++)
                {
                    if (!IsWalkable(i))
                    {
                        continue;
                    }

                    Vector3 faceCenter = GetFaceCenter(i);
                    float distanceToObstacle = Vector3.Distance(faceCenter, obstacle.Position);

                    if (distanceToObstacle < (obstacle.Radius + 0.5f))
                    {
                        blockedFaces.Add(i);
                    }
                }
            }

            return blockedFaces;
        }

        /// <summary>
        /// Gets the minimum element from a sorted set.
        /// </summary>
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

        /// <summary>
        /// Calculates heuristic distance between two faces.
        /// </summary>
        /// <remarks>
        /// Uses Euclidean distance between face centers as heuristic.
        /// This is admissible for A* (never overestimates actual path cost).
        /// </remarks>
        private float Heuristic(int from, int to)
        {
            Vector3 fromCenter = GetFaceCenter(from);
            Vector3 toCenter = GetFaceCenter(to);
            return Vector3.Distance(fromCenter, toCenter);
        }

        /// <summary>
        /// Calculates the cost of traversing from one face to an adjacent face.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe walkmesh edge cost calculation.
        /// Base cost is distance, modified by surface material (water, mud, etc. cost more).
        /// </remarks>
        private float EdgeCost(int from, int to)
        {
            // Base cost is distance between face centers
            float dist = Vector3.Distance(GetFaceCenter(from), GetFaceCenter(to));

            // Apply surface material cost modifier
            float surfaceMod = GetSurfaceCost(to);
            return dist * surfaceMod;
        }

        /// <summary>
        /// Gets the pathfinding cost modifier for a surface material.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe surface material cost modifiers.
        /// Different materials have different movement costs:
        /// - Normal surfaces: 1.0 (no modifier)
        /// - Water, puddles, swamp, mud: 1.5 (slightly slower)
        /// - Bottomless pit: 10.0 (very high cost - avoid if possible)
        /// </remarks>
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

        /// <summary>
        /// Reconstructs the path from A* search results.
        /// </summary>
        /// <remarks>
        /// Converts face path to waypoint sequence.
        /// Adds start and goal positions, with face centers as intermediate waypoints.
        /// Applies path smoothing to remove redundant waypoints.
        /// </remarks>
        private IList<Vector3> ReconstructPath(Dictionary<int, int> cameFrom, int current, Vector3 start, Vector3 goal)
        {
            // Build face path from goal to start
            var facePath = new List<int> { current };
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                facePath.Add(current);
            }
            facePath.Reverse();  // Reverse to get start to goal

            // Convert face path to waypoints
            var path = new List<Vector3>();
            path.Add(start);

            // Add face centers as intermediate waypoints
            for (int i = 1; i < facePath.Count - 1; i++)
            {
                path.Add(GetFaceCenter(facePath[i]));
            }

            path.Add(goal);

            // Apply funnel algorithm for smoother paths
            return SmoothPath(path);
        }

        /// <summary>
        /// Smooths the path by removing redundant waypoints.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe path smoothing.
        /// Uses line-of-sight checks to remove waypoints that can be skipped.
        /// Results in more natural movement paths.
        /// </remarks>
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

                // If line of sight is clear, skip this waypoint
                if (!HasLineOfSight(prev, next))
                {
                    // Can't skip - add the waypoint
                    smoothed.Add(path[i]);
                }
            }

            smoothed.Add(path[path.Count - 1]);
            return smoothed;
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

        /// <summary>
        /// Finds the face index at a given position using 2D projection.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe walkmesh face lookup.
        /// Uses AABB tree for spatial acceleration when available, falls back to brute force.
        /// Tests if point is within face bounds using 2D point-in-triangle test.
        /// </remarks>
        public override int FindFaceAt(Vector3 position)
        {
            // Use AABB tree if available for faster spatial queries
            if (_aabbRoot != null)
            {
                return FindFaceAabb(_aabbRoot, position);
            }

            // Brute force fallback - test all faces
            for (int i = 0; i < _faceCount; i++)
            {
                if (PointInFace2d(position, i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds face using AABB tree traversal for spatial acceleration.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) walkmesh AABB tree structure.
        /// Recursively traverses the tree, testing point against bounding boxes first,
        /// then testing actual triangles in leaf nodes.
        /// </remarks>
        private int FindFaceAabb(NavigationMesh.AabbNode node, Vector3 point)
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

        /// <summary>
        /// Tests if a point is within a face (2D projection).
        /// </summary>
        /// <remarks>
        /// Uses same-side test for point-in-triangle detection.
        /// Based on standard point-in-triangle algorithm used in swkotor2.exe walkmesh.
        /// </remarks>
        private bool PointInFace2d(Vector3 point, int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _faceCount)
            {
                return false;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _faceIndices.Length)
            {
                return false;
            }

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

        /// <summary>
        /// Calculates the 2D sign for point-in-triangle test.
        /// </summary>
        private float Sign2d(Vector3 p1, Vector3 p2, Vector3 p3)
        {
            return (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
        }

        /// <summary>
        /// Gets the center point of a face.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe walkmesh face center calculation.
        /// Returns the centroid (average) of the three vertices.
        /// </remarks>
        public override Vector3 GetFaceCenter(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _faceCount)
            {
                return Vector3.Zero;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _faceIndices.Length)
            {
                return Vector3.Zero;
            }

            Vector3 v1 = _vertices[_faceIndices[baseIdx]];
            Vector3 v2 = _vertices[_faceIndices[baseIdx + 1]];
            Vector3 v3 = _vertices[_faceIndices[baseIdx + 2]];

            return (v1 + v2 + v3) / 3.0f;
        }

        /// <summary>
        /// Gets adjacent faces for a given face.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe walkmesh adjacency lookup.
        /// Adjacency encoding: adjacency_index = face_index * 3 + edge_index, -1 = no neighbor
        /// Returns the face indices of neighboring faces that share an edge.
        /// </remarks>
        public override IEnumerable<int> GetAdjacentFaces(int faceIndex)
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
                    if (encoded < 0)
                    {
                        yield return -1;  // No neighbor
                    }
                    else
                    {
                        // Decode adjacency: face index = encoded / 3, edge = encoded % 3
                        yield return encoded / 3;
                    }
                }
                else
                {
                    yield return -1;
                }
            }
        }

        /// <summary>
        /// Checks if a face is walkable based on its surface material.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe walkmesh walkability checks.
        /// Surface materials are looked up from surfacemat.2da to determine walkability.
        /// Walkable materials include dirt, grass, stone, wood, water, carpet, metal, etc.
        /// Non-walkable materials include lava, deep water, non-walk surfaces, etc.
        /// </remarks>
        public override bool IsWalkable(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _surfaceMaterials.Length)
            {
                return false;
            }

            int material = _surfaceMaterials[faceIndex];
            return WalkableMaterials.Contains(material);
        }

        /// <summary>
        /// Gets the surface material index for a given face.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe walkmesh surface material lookup.
        /// Surface materials are stored per-face and correspond to entries in surfacemat.2da.
        /// Material indices range from 0-30, with specific meanings:
        /// - 0: Undefined
        /// - 1: Dirt (walkable)
        /// - 3: Grass (walkable)
        /// - 4: Stone (walkable)
        /// - 7: NonWalk (non-walkable)
        /// - 15: Lava (non-walkable)
        /// - 17: DeepWater (non-walkable)
        /// - etc.
        /// </remarks>
        public override int GetSurfaceMaterial(int faceIndex)
        {
            if (faceIndex < 0 || faceIndex >= _surfaceMaterials.Length)
            {
                return 0; // Return undefined material for invalid face index
            }

            return _surfaceMaterials[faceIndex];
        }

        /// <summary>
        /// Performs a raycast against the walkmesh.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 performs walkmesh raycasts for visibility checks.
        /// Uses AABB tree for spatial acceleration when available, falls back to brute force.
        /// Returns the closest hit point and face index along the ray.
        /// </remarks>
        public override bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            if (_faceCount == 0)
            {
                return false;
            }

            // Normalize direction
            float dirLength = direction.Length();
            if (dirLength < 1e-6f)
            {
                return false;
            }
            Vector3 normalizedDir = direction / dirLength;

            // Use AABB tree if available for faster spatial queries
            if (_aabbRoot != null)
            {
                return RaycastAabb(_aabbRoot, origin, normalizedDir, maxDistance, out hitPoint, out hitFace);
            }

            // Brute force fallback - test all faces
            float bestDist = maxDistance;
            for (int i = 0; i < _faceCount; i++)
            {
                float dist;
                if (RayTriangleIntersect(origin, normalizedDir, i, bestDist, out dist))
                {
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        hitFace = i;
                        hitPoint = origin + normalizedDir * dist;
                    }
                }
            }

            return hitFace >= 0;
        }

        /// <summary>
        /// Performs raycast using AABB tree traversal for spatial acceleration.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) walkmesh AABB tree structure.
        /// Recursively traverses the tree, testing ray against bounding boxes first,
        /// then testing actual triangles in leaf nodes.
        /// </remarks>
        private bool RaycastAabb(NavigationMesh.AabbNode node, Vector3 origin, Vector3 direction, float maxDist, out Vector3 hitPoint, out int hitFace)
        {
            hitPoint = Vector3.Zero;
            hitFace = -1;

            if (node == null)
            {
                return false;
            }

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


        /// <summary>
        /// Tests if a ray intersects a triangle face.
        /// </summary>
        /// <remarks>
        /// Uses the Möller-Trumbore algorithm for ray-triangle intersection.
        /// Based on standard ray-triangle intersection used in swkotor2.exe walkmesh collision.
        /// </remarks>
        private bool RayTriangleIntersect(Vector3 origin, Vector3 direction, int faceIndex, float maxDist, out float distance)
        {
            distance = 0f;

            if (faceIndex < 0 || faceIndex >= _faceCount)
            {
                return false;
            }

            int baseIdx = faceIndex * 3;
            if (baseIdx + 2 >= _faceIndices.Length)
            {
                return false;
            }

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

        public override bool TestLineOfSight(Vector3 from, Vector3 to)
        {
            return HasLineOfSight(from, to);
        }

        public override bool ProjectToSurface(Vector3 point, out Vector3 result, out float height)
        {
            return ProjectToWalkmesh(point, out result, out height);
        }
    }
}
