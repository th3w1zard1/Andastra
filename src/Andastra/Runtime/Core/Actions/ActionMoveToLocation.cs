using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to move an entity to a location via pathfinding.
    /// </summary>
    /// <remarks>
    /// Move To Location Action:
    /// - Based on swkotor2.exe movement action system
    /// - Original implementation: FUN_00508260 @ 0x00508260 (load ActionList from GFF)
    /// - Located via string reference: "ActionList" @ 0x007bebdc, "MOVETO" @ 0x007b6b24
    /// - Original implementation: Uses walkmesh pathfinding to find path to destination
    /// - Follows path waypoints, facing movement direction (Y-up: Atan2(Y, X))
    /// - Walk/run speed determined by entity stats (WalkSpeed/RunSpeed from appearance.2da)
    /// - Pathfinding uses A* algorithm on walkmesh adjacency graph
    /// - Action parameters stored as ActionId, GroupActionId, NumParams, Paramaters (Type/Value pairs)
    /// - FUN_00505bc0 @ 0x00505bc0 saves ActionList to GFF structure
    /// - SchedActionList @ 0x007bf99c: Scheduled actions with timers for delayed execution
    /// </remarks>
    public class ActionMoveToLocation : ActionBase
    {
        private readonly Vector3 _destination;
        private readonly bool _run;
        private IList<Vector3> _path;
        private int _pathIndex;
        private const float ArrivalThreshold = 0.5f;
        private BaseCreatureCollisionDetector _collisionDetector;

        // Bump counter tracking (matches offset 0x268 in swkotor2.exe entity structure)
        // Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 tracks bump count at param_1[0xe0] + 0x268
        // Located via string reference: "aborted walking, Maximum number of bumps happened" @ 0x007c0458
        // Maximum bumps: 5 (aborts movement if exceeded)
        private const string BumpCounterKey = "ActionMoveToLocation_BumpCounter";
        private const string LastBlockingCreatureKey = "ActionMoveToLocation_LastBlockingCreature";
        private const int MaxBumps = 5;

        public ActionMoveToLocation(Vector3 destination, bool run = false)
            : base(ActionType.MoveToPoint)
        {
            _destination = destination;
            _run = run;
            // Collision detector is created lazily on first use when we have access to the world
        }

        /// <summary>
        /// Gets or creates the appropriate collision detector for the current engine.
        /// Uses reflection to create engine-specific detectors without Core depending on Games namespace.
        /// </summary>
        /// <param name="world">The world instance to determine engine type from.</param>
        /// <returns>The collision detector for the current engine.</returns>
        /// <remarks>
        /// Engine Detection:
        /// - Checks world's type namespace to determine engine (Odyssey, Aurora, Eclipse)
        /// - Uses reflection to instantiate engine-specific collision detector classes
        /// - Falls back to DefaultCreatureCollisionDetector if engine type cannot be determined
        /// - Based on swkotor.exe, swkotor2.exe, nwmain.exe, daorigins.exe collision systems
        /// - Original implementation: FUN_005479f0 @ 0x005479f0 (swkotor2.exe) uses creature bounding box
        /// - swkotor.exe: UpdateCreatureMovement @ 0x00516630 uses FUN_005479f0 equivalent
        /// - nwmain.exe: CPathfindInformation class with creature collision checking
        /// - daorigins.exe/DragonAge2.exe: PhysX-based collision detection system
        /// </remarks>
        private BaseCreatureCollisionDetector GetOrCreateCollisionDetector(IWorld world)
        {
            if (_collisionDetector != null)
            {
                return _collisionDetector;
            }

            if (world == null)
            {
                // No world available, use default detector
                _collisionDetector = new DefaultCreatureCollisionDetector();
                return _collisionDetector;
            }

            // Determine engine type from world's namespace
            Type worldType = world.GetType();
            string worldNamespace = worldType.Namespace ?? string.Empty;
            string detectorTypeName = null;
            string detectorNamespace = null;

            // Check for Odyssey engine (KOTOR games)
            if (worldNamespace.Contains("Odyssey"))
            {
                detectorNamespace = "Andastra.Runtime.Games.Odyssey.Collision";
                detectorTypeName = "OdysseyCreatureCollisionDetector";
            }
            // Check for Aurora engine (NWN games)
            else if (worldNamespace.Contains("Aurora"))
            {
                detectorNamespace = "Andastra.Runtime.Games.Aurora.Collision";
                detectorTypeName = "AuroraCreatureCollisionDetector";
            }
            // Check for Eclipse engine (Dragon Age games)
            else if (worldNamespace.Contains("Eclipse"))
            {
                detectorNamespace = "Andastra.Runtime.Games.Eclipse.Collision";
                detectorTypeName = "EclipseCreatureCollisionDetector";
            }

            // Try to create engine-specific detector using reflection
            if (detectorTypeName != null && detectorNamespace != null)
            {
                try
                {
                    // Construct full type name
                    string fullTypeName = detectorNamespace + "." + detectorTypeName;

                    // Search all loaded assemblies for the detector type
                    // The detector may be in a different assembly than the world type
                    Type detectorType = null;
                    foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            detectorType = assembly.GetType(fullTypeName);
                            if (detectorType != null)
                            {
                                break; // Found the type
                            }
                        }
                        catch (ReflectionTypeLoadException)
                        {
                            // Assembly has types that couldn't be loaded - continue searching other assemblies
                            // This can happen when dependencies are missing or types are in unavailable assemblies
                            continue;
                        }
                        catch (TypeLoadException)
                        {
                            // Specific type couldn't be loaded from this assembly - continue searching
                            // The type may exist in a different assembly
                            continue;
                        }
                        catch (BadImageFormatException)
                        {
                            // Assembly is corrupted or has invalid format - skip this assembly and continue
                            // This can happen with mixed architecture assemblies or corrupted DLLs
                            continue;
                        }
                        catch (System.IO.FileNotFoundException)
                        {
                            // Assembly file or dependency is missing - continue searching other assemblies
                            // This can happen when assembly references are broken
                            continue;
                        }
                        catch (System.IO.FileLoadException)
                        {
                            // Assembly failed to load - continue searching other assemblies
                            // This can happen with version conflicts or loading issues
                            continue;
                        }
                        catch (System.ArgumentException)
                        {
                            // Invalid type name format (shouldn't happen with our constructed name, but handle gracefully)
                            // Continue searching other assemblies
                            continue;
                        }
                        // Note: We intentionally don't catch general Exception to allow unexpected critical errors to propagate
                        // This ensures we don't silently ignore serious problems while continuing to search
                    }

                    if (detectorType != null)
                    {
                        // Create instance using parameterless constructor
                        object detectorInstance = Activator.CreateInstance(detectorType);
                        if (detectorInstance is BaseCreatureCollisionDetector detector)
                        {
                            _collisionDetector = detector;
                            return _collisionDetector;
                        }
                    }
                }
                catch
                {
                    // Reflection failed, fall through to default detector
                }
            }

            // Fall back to default detector if engine type cannot be determined or reflection fails
            _collisionDetector = new DefaultCreatureCollisionDetector();
            return _collisionDetector;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            ITransformComponent transform = actor.GetComponent<ITransformComponent>();
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();

            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Try to find path if we don't have one
            // Based on swkotor2.exe: Movement pathfinding implementation
            // Located via string references: "MOVETO" @ 0x007b6b24, "ActionList" @ 0x007bebdc
            // Walking collision checking: UpdateCreatureMovement @ 0x0054be70
            // Located via string references:
            //   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
            //   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
            //   - "aborted walking, Maximum number of bumps happened" @ 0x007c0458
            // Original implementation (from decompiled UpdateCreatureMovement):
            //   - Function signature: `int UpdateCreatureMovement(int* creature, float* currentPosition, float* destination, float* movementDirection, int* additionalParam)`
            //   - param_1: Entity pointer (this pointer for creature object)
            //   - param_2: Output position (final position after movement)
            //   - param_3: Output position (intermediate position)
            //   - param_4: Output direction vector (normalized movement direction)
            //   - param_5: Output parameter (unused in this context)
            //   - Path following: Iterates through path waypoints stored at offset 0x90 in entity structure
            //   - Path indices: Current waypoint index at offset 0x9c, path length at offset 0x8c
            //   - Position tracking: Current position at offsets 0x25 (X), 0x26 (Y), 0x27 (Z) in entity structure
            //   - Orientation: Facing stored at offsets 0x28 (X), 0x29 (Y), 0x2a (Z) as direction vector
            //   - Distance calculation: Uses 2D distance (X/Y plane, ignores Z) for movement calculations
            //   - Walkmesh projection: Projects position to walkmesh surface using FUN_004f5070 (walkmesh height lookup)
            //   - Direction normalization: Uses FUN_004d8390 to normalize direction vectors
            //   - Creature collision: Checks for collisions with other creatures along path using FUN_005479f0
            //   - Bump counter: Tracks number of creature bumps (stored at offset 0x268 in entity structure)
            //   - Maximum bumps: If bump count exceeds 5, aborts movement and clears path (frees path array, sets path length to 0)
            //   - Total blocking: If same creature blocks repeatedly (local_c0 == entity ID at offset 0x254), aborts movement
            //   - Path completion: Returns 1 when path is complete (local_d0 flag), 0 if still in progress
            //   - Path waypoint iteration: Advances through path waypoints (increments by 2, not 1), projects each position to walkmesh
            //   - Movement distance: Calculates movement distance based on speed and remaining distance to waypoint
            //   - Final position: Updates entity position to final projected position on walkmesh
            //   - Special case: When path length is 4, checks final waypoint and may reverse direction if facing wrong way
            // Pathfinding searches walkmesh adjacency graph for valid path
            // If no path found, original engine attempts direct movement (may fail if blocked)
            if (_path == null)
            {
                IArea area = actor.World.CurrentArea;
                if (area != null && area.NavigationMesh != null)
                {
                    _path = area.NavigationMesh.FindPath(transform.Position, _destination);
                }

                if (_path == null || _path.Count == 0)
                {
                    // No path found - try direct movement as fallback
                    // Original engine: Falls back to direct movement if pathfinding fails
                    _path = new List<Vector3> { _destination };
                }
                _pathIndex = 0;
            }

            if (_pathIndex >= _path.Count)
            {
                return ActionStatus.Complete;
            }

            Vector3 target = _path[_pathIndex];
            Vector3 toTarget = target - transform.Position;
            toTarget.Y = 0; // Ignore vertical for direction
            float distance = toTarget.Length();

            if (distance < ArrivalThreshold)
            {
                _pathIndex++;
                if (_pathIndex >= _path.Count)
                {
                    return ActionStatus.Complete;
                }
                return ActionStatus.InProgress;
            }

            // Calculate movement
            float speed = stats != null
                ? (_run ? stats.RunSpeed : stats.WalkSpeed)
                : (_run ? 5.0f : 2.5f);

            Vector3 direction = Vector3.Normalize(toTarget);
            float moveDistance = speed * deltaTime;

            if (moveDistance > distance)
            {
                moveDistance = distance;
            }

            Vector3 currentPosition = transform.Position;
            Vector3 newPosition = currentPosition + direction * moveDistance;

            // Project position to walkmesh surface (matches FUN_004f5070 in swkotor2.exe)
            // Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 projects positions to walkmesh after movement
            // Located via string references: Walkmesh projection in movement system
            // Original implementation: FUN_004f5070 projects 3D position to walkmesh surface height
            IArea currentArea = actor.World.CurrentArea;
            if (currentArea != null && currentArea.NavigationMesh != null)
            {
                Vector3 projectedPos;
                float height;
                if (currentArea.NavigationMesh.ProjectToSurface(newPosition, out projectedPos, out height))
                {
                    newPosition = projectedPos;
                }
            }

            // Check for creature collisions along movement path
            // Based on swkotor2.exe: FUN_005479f0 @ 0x005479f0 checks for creature collisions
            // Located via string references:
            //   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
            //   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
            //   - "aborted walking, Maximum number of bumps happened" @ 0x007c0458
            // Original implementation: FUN_005479f0 checks if movement path intersects with other creatures
            // Function signature: `undefined4 FUN_005479f0(void *this, float *param_1, float *param_2, undefined4 *param_3, uint *param_4)`
            // param_1: Start position (float[3])
            // param_2: End position (float[3])
            // param_3: Output collision normal (float[3]) or null
            // param_4: Output blocking creature ObjectId (uint) or null
            // Returns: 0 if collision detected, 1 if path is clear
            // Uses FUN_004e17a0 and FUN_004f5290 for collision detection with creature bounding boxes
            // Implementation: Uses proper bounding box collision detection instead of simplified radius-based check
            BaseCreatureCollisionDetector collisionDetector = GetOrCreateCollisionDetector(actor.World);
            uint blockingCreatureId;
            Vector3 collisionNormal;
            bool hasCollision = collisionDetector.CheckCreatureCollision(actor, currentPosition, newPosition, out blockingCreatureId, out collisionNormal);

            if (hasCollision)
            {
                // Get bump counter (stored at offset 0x268 in swkotor2.exe entity structure)
                // Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 tracks bump count at param_1[0xe0] + 0x268
                int bumpCount = GetBumpCounter(actor);
                bumpCount++;
                SetBumpCounter(actor, bumpCount);

                // Check if maximum bumps exceeded
                // Based on swkotor2.exe: UpdateCreatureMovement aborts movement if bump count > 5
                // Located via string reference: "aborted walking, Maximum number of bumps happened" @ 0x007c0458
                // Original implementation: If bump count > 5, clears path array and sets path length to 0
                if (bumpCount > MaxBumps)
                {
                    // Abort movement - maximum bumps exceeded
                    ClearBumpCounter(actor);
                    _path = null; // Clear path (matches original: sets path length to 0)
                    _pathIndex = 0;
                    return ActionStatus.Failed;
                }

                // Check if same creature blocks repeatedly (matches offset 0x254 in swkotor2.exe)
                // Based on swkotor2.exe: UpdateCreatureMovement checks if local_c0 == entity ID at offset 0x254
                // Original implementation: If same creature blocks repeatedly, aborts movement
                uint lastBlockingCreature = GetLastBlockingCreature(actor);
                if (blockingCreatureId != 0x7F000000 && blockingCreatureId == lastBlockingCreature)
                {
                    // Abort movement - same creature blocking repeatedly
                    ClearBumpCounter(actor);
                    _path = null; // Clear path (matches original: sets path length to 0)
                    _pathIndex = 0;
                    return ActionStatus.Failed;
                }

                SetLastBlockingCreature(actor, blockingCreatureId);

                // Try to navigate around the blocking creature
                // Based on swkotor2.exe: UpdateCreatureMovement @ 0x0054be70 calls FindPathAroundObstacle @ 0x0061c390 for pathfinding around obstacles
                // Located via string reference: "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
                // Original implementation: FindPathAroundObstacle @ 0x0061c390 finds alternative path around blocking creature
                // Function signature: `float* FindPathAroundObstacle(void* this, int* movingCreature, void* blockingCreature)`
                // this: Pathfinding context object (contains obstacle polygon data)
                // movingCreature: Entity pointer (int offset to entity structure)
                // blockingCreature: Blocking creature object pointer
                // Returns: float* pointer to new path waypoints, or DAT_007c52ec (null) if no path found
                // Implementation details (from Ghidra analysis):
                //   - Line 57: Loads "k_def_pathfail01" string for error reporting
                //   - Line 75: Calls FUN_0061a670 to set up obstacle avoidance polygon from creature bounding box
                //   - Line 80-85: Calls FUN_0061b7d0 to check if start/end points are within obstacle polygon
                //   - Line 92: Calls FUN_0061bcb0 to validate path against obstacles
                //   - Line 100-105: Calls FUN_0061c1e0 to get waypoints, then FUN_0061c2c0 to check path segments
                //   - Line 131-142: Calls FUN_0061b520 to insert waypoints into path to avoid obstacles
                //   - If pathfinding fails, returns null pointer (DAT_007c52ec)
                // Helper functions:
                //   - FUN_0061b7d0: Checks if point is within obstacle polygon (6-sided polygon test)
                //   - FUN_0061bcb0: Validates entire path against obstacles
                //   - FUN_0061c2c0: Checks path segments for collisions (calls FUN_0061b310 per segment)
                //   - FUN_0061b310: Checks single path segment for creature collision
                //   - FUN_0061b520: Inserts waypoints into existing path array to route around obstacles
                //   - FUN_0061c1e0: Gets waypoint array from pathfinding context
                // Equivalent functions in other engines:
                //   - swkotor.exe: FindPathAroundObstacle @ 0x005d0840 (called from UpdateCreatureMovement @ 0x00516630, line 254)
                //   - nwmain.exe: CPathfindInformation class with obstacle avoidance in pathfinding system
                //   - daorigins.exe/DragonAge2.exe: Advanced dynamic obstacle system (different architecture)
                if (currentArea != null && currentArea.NavigationMesh != null)
                {
                    // Get blocking creature's position and bounding box
                    IEntity blockingCreature = actor.World.GetEntity(blockingCreatureId);
                    if (blockingCreature != null)
                    {
                        ITransformComponent blockingTransform = blockingCreature.GetComponent<ITransformComponent>();
                        if (blockingTransform != null)
                        {
                            Vector3 obstaclePosition = blockingTransform.Position;

                            // Get creature bounding box to determine avoidance radius
                            // Use collision detector to get proper bounding box
                            BaseCreatureCollisionDetector collisionDetectorForObstacle = GetOrCreateCollisionDetector(actor.World);
                            CreatureBoundingBox blockingBoundingBox = collisionDetectorForObstacle.GetCreatureBoundingBoxPublic(blockingCreature);
                            // Use the larger of width/depth as avoidance radius, with safety margin
                            float avoidanceRadius = Math.Max(blockingBoundingBox.Width, blockingBoundingBox.Depth) * 0.5f + 0.5f;

                            // Create obstacle info
                            var obstacles = new List<Interfaces.ObstacleInfo>
                            {
                                new Interfaces.ObstacleInfo(obstaclePosition, avoidanceRadius)
                            };

                            // Try to find path around obstacle from current position to destination
                            IList<Vector3> newPath = currentArea.NavigationMesh.FindPathAroundObstacles(
                                transform.Position,
                                _destination,
                                obstacles);

                            if (newPath != null && newPath.Count > 0)
                            {
                                // Found alternative path around obstacle - use it
                                _path = newPath;
                                _pathIndex = 0;
                                // Reset bump counter since we found a way around
                                ClearBumpCounter(actor);
                                return ActionStatus.InProgress;
                            }
                        }
                    }
                }

                // Could not find path around obstacle - action fails
                // Based on swkotor2.exe: If FUN_0054a1f0 returns null, movement is aborted
                // Located via string reference: "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
                return ActionStatus.Failed;
            }

            // Clear bump counter if no collision
            ClearBumpCounter(actor);

            transform.Position = newPosition;
            // Set facing to match movement direction (Y-up system: Atan2(Y, X) for 2D plane)
            transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

            return ActionStatus.InProgress;
        }
        /// <summary>
        /// Gets the bump counter for an entity.
        /// Based on swkotor2.exe: Bump counter stored at offset 0x268 in entity structure.
        /// </summary>
        private int GetBumpCounter(IEntity entity)
        {
            if (entity is Entities.Entity concreteEntity)
            {
                return concreteEntity.GetData<int>(BumpCounterKey, 0);
            }
            return 0;
        }

        /// <summary>
        /// Sets the bump counter for an entity.
        /// Based on swkotor2.exe: Bump counter stored at offset 0x268 in entity structure.
        /// </summary>
        private void SetBumpCounter(IEntity entity, int count)
        {
            if (entity is Entities.Entity concreteEntity)
            {
                concreteEntity.SetData(BumpCounterKey, count);
            }
        }

        /// <summary>
        /// Clears the bump counter for an entity.
        /// </summary>
        private void ClearBumpCounter(IEntity entity)
        {
            if (entity is Entities.Entity concreteEntity)
            {
                concreteEntity.SetData(BumpCounterKey, 0);
            }
        }

        /// <summary>
        /// Gets the last blocking creature ObjectId for an entity.
        /// Based on swkotor2.exe: Stored at offset 0x254 in entity structure.
        /// </summary>
        private uint GetLastBlockingCreature(IEntity entity)
        {
            if (entity is Entities.Entity concreteEntity)
            {
                return concreteEntity.GetData<uint>(LastBlockingCreatureKey, 0x7F000000);
            }
            return 0x7F000000; // OBJECT_INVALID
        }

        /// <summary>
        /// Sets the last blocking creature ObjectId for an entity.
        /// Based on swkotor2.exe: Stored at offset 0x254 in entity structure.
        /// </summary>
        private void SetLastBlockingCreature(IEntity entity, uint creatureId)
        {
            if (entity is Entities.Entity concreteEntity)
            {
                concreteEntity.SetData(LastBlockingCreatureKey, creatureId);
            }
        }
    }
}

