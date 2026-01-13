using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Reflection;
using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action to move an entity to another object.
    /// Based on NWScript ActionMoveToObject semantics.
    /// </summary>
    /// <remarks>
    /// Move To Object Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) movement action system
    /// - Original implementation: FUN_00508260 @ 0x00508260 (load ActionList from GFF)
    /// - Located via string reference: "ActionList" @ 0x007bebdc, "MOVETO" @ 0x007b6b24
    /// - Moves actor towards target object within specified range
    /// - Uses direct movement (no pathfinding) - follows target if it moves
    /// - Faces target when within range (Y-up: Atan2(Y, X) for 2D plane facing)
    /// - Walk/run speed determined by entity stats (WalkSpeed/RunSpeed from appearance.2da)
    /// - Action parameters stored as ActionId, GroupActionId, NumParams, Paramaters (Type/Value pairs)
    /// </remarks>
    public class ActionMoveToObject : ActionBase
    {
        private readonly uint _targetObjectId;
        private readonly bool _run;
        private readonly float _range;
        private const float ArrivalThreshold = 0.1f;
        private BaseCreatureCollisionDetector _collisionDetector;

        // Bump counter tracking (matches offset 0x268 in swkotor2.exe entity structure)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 tracks bump count at param_1[0xe0] + 0x268
        // Located via string reference: "aborted walking, Maximum number of bumps happened" @ 0x007c0458
        // Maximum bumps: 5 (aborts movement if exceeded)
        private const string BumpCounterKey = "ActionMoveToObject_BumpCounter";
        private const string LastBlockingCreatureKey = "ActionMoveToObject_LastBlockingCreature";
        private const int MaxBumps = 5;

        public ActionMoveToObject(uint targetObjectId, bool run = false, float range = 1.0f)
            : base(ActionType.MoveToObject)
        {
            _targetObjectId = targetObjectId;
            _run = run;
            _range = range;
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
                        catch (FileNotFoundException)
                        {
                            // Assembly file or dependency is missing - continue searching other assemblies
                            // This can happen when assembly references are broken
                            continue;
                        }
                        catch (FileLoadException)
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
            if (transform == null)
            {
                return ActionStatus.Failed;
            }

            // Get target entity
            IEntity target = actor.World.GetEntity(_targetObjectId);
            if (target == null || !target.IsValid)
            {
                return ActionStatus.Failed;
            }

            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (targetTransform == null)
            {
                return ActionStatus.Failed;
            }

            Vector3 toTarget = targetTransform.Position - transform.Position;
            toTarget.Y = 0; // Ignore vertical
            float distance = toTarget.Length();

            // If we're within range, we're done
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionMoveToObject implementation
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
            //   - Direct movement: Moves directly to target (no pathfinding), follows if target moves
            //   - Position tracking: Current position at offsets 0x25 (X), 0x26 (Y), 0x27 (Z) in entity structure
            //   - Orientation: Facing stored at offsets 0x28 (X), 0x29 (Y), 0x2a (Z) as direction vector
            //   - Distance calculation: Uses 2D distance (X/Z plane, ignores Y) for movement calculations
            //   - Walkmesh projection: Projects position to walkmesh surface using FUN_004f5070 (walkmesh height lookup)
            //   - Direction normalization: Uses FUN_004d8390 to normalize direction vectors
            //   - Creature collision: Checks for collisions with other creatures along movement path using FUN_005479f0
            //   - Bump counter: Tracks number of creature bumps (stored at offset 0x268 in entity structure)
            //   - Maximum bumps: If bump count exceeds 5, aborts movement and clears path (frees path array, sets path length to 0)
            //   - Total blocking: If same creature blocks repeatedly (local_c0 == entity ID at offset 0x254), aborts movement
            //   - Path completion: Returns 1 when movement is complete (local_d0 flag), 0 if still in progress
            //   - Movement distance: Calculates movement distance based on speed and remaining distance to target
            //   - Final position: Updates entity position to final projected position on walkmesh
            // Action completes when within specified range of target object
            if (distance <= _range)
            {
                // Face target
                if (distance > ArrivalThreshold)
                {
                    Vector3 direction = Vector3.Normalize(toTarget);
                    // Y-up system: Atan2(Y, X) for 2D plane facing
                    transform.Facing = (float)Math.Atan2(direction.Y, direction.X);
                }
                return ActionStatus.Complete;
            }

            // Move towards target
            IStatsComponent stats = actor.GetComponent<IStatsComponent>();
            float speed = stats != null
                ? (_run ? stats.RunSpeed : stats.WalkSpeed)
                : (_run ? 5.0f : 2.5f);

            Vector3 direction2 = Vector3.Normalize(toTarget);
            float moveDistance = speed * deltaTime;
            float targetDistance = distance - _range;

            if (moveDistance > targetDistance)
            {
                moveDistance = targetDistance;
            }

            Vector3 currentPosition = transform.Position;
            Vector3 newPosition = currentPosition + direction2 * moveDistance;

            // Project position to walkmesh surface (matches FUN_004f5070 in swkotor2.exe)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 projects positions to walkmesh after movement
            // Located via string references: Walkmesh projection in movement system
            // Original implementation: FUN_004f5070 projects 3D position to walkmesh surface height
            IArea area = actor.World.CurrentArea;
            if (area != null && area.NavigationMesh != null)
            {
                Vector3 projectedPos;
                float height;
                if (area.NavigationMesh.ProjectToSurface(newPosition, out projectedPos, out height))
                {
                    newPosition = projectedPos;
                }
            }

            // Get or create collision detector (lazy initialization with world access)
            BaseCreatureCollisionDetector collisionDetector = GetOrCreateCollisionDetector(actor.World);

            // Check for creature collisions along movement path
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005479f0 @ 0x005479f0 checks for creature collisions
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
            // Implementation: Uses proper bounding box collision detection (not simplified radius-based check)
            // - GetCreatureBoundingBox() retrieves engine-specific bounding boxes from appearance.2da hitradius
            // - CheckLineSegmentVsBoundingBox() performs line-segment vs axis-aligned bounding box intersection
            // - Uses Minkowski sum to expand creature bounding box by actor's bounding box for accurate collision
            // - Matches original engine behavior: FUN_005479f0 uses bounding box width/height from entity structure
            // - K1 (swkotor.exe): Radius at offset +8 from bounding box pointer (0x340)
            // - K2 (swkotor2.exe): Width at +0x14, height at +0xbc from bounding box pointer (0x380)
            // Exclude target object from collision checking (we're moving towards it)
            uint blockingCreatureId;
            Vector3 collisionNormal;
            bool hasCollision = collisionDetector.CheckCreatureCollision(actor, currentPosition, newPosition, out blockingCreatureId, out collisionNormal, _targetObjectId);

            if (hasCollision)
            {
                // Get bump counter (stored at offset 0x268 in swkotor2.exe entity structure)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 tracks bump count at param_1[0xe0] + 0x268
                int bumpCount = GetBumpCounter(actor);
                bumpCount++;
                SetBumpCounter(actor, bumpCount);

                // Check if maximum bumps exceeded
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement aborts movement if bump count > 5
                // Located via string reference: "aborted walking, Maximum number of bumps happened" @ 0x007c0458
                // Original implementation: If bump count > 5, clears path array and sets path length to 0
                if (bumpCount > MaxBumps)
                {
                    // Abort movement - maximum bumps exceeded
                    ClearBumpCounter(actor);
                    return ActionStatus.Failed;
                }

                // Check if same creature blocks repeatedly (matches offset 0x254 in swkotor2.exe)
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement checks if local_c0 == entity ID at offset 0x254
                // Original implementation: If same creature blocks repeatedly, aborts movement
                uint lastBlockingCreature = GetLastBlockingCreature(actor);
                if (blockingCreatureId != 0x7F000000 && blockingCreatureId == lastBlockingCreature)
                {
                    // Abort movement - same creature blocking repeatedly
                    ClearBumpCounter(actor);
                    return ActionStatus.Failed;
                }

                SetLastBlockingCreature(actor, blockingCreatureId);

                // Try to navigate around the blocking creature
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): UpdateCreatureMovement @ 0x0054be70 calls FindPathAroundObstacle @ 0x0061c390 for pathfinding around obstacles
                // Located via string reference: "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
                // Original implementation: FindPathAroundObstacle @ 0x0061c390 finds alternative path around blocking creature
                // Function signature: `float* FindPathAroundObstacle(void* this, int* movingCreature, void* blockingCreature)`
                // Equivalent functions:
                //   - swkotor.exe: FindPathAroundObstacle @ 0x005d0840 (called from UpdateCreatureMovement @ 0x00516630)
                //   - nwmain.exe: CPathfindInformation obstacle avoidance in pathfinding system
                //   - daorigins.exe/DragonAge2.exe: Dynamic obstacle system with cover integration
                // For ActionMoveToObject, we use direct movement but still try to avoid obstacles when blocked
                IArea currentArea = actor.World.CurrentArea;
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
                            CreatureBoundingBox blockingBoundingBox = collisionDetector.GetCreatureBoundingBoxPublic(blockingCreature);
                            // Use the larger of width/depth as avoidance radius, with safety margin
                            float avoidanceRadius = Math.Max(blockingBoundingBox.Width, blockingBoundingBox.Depth) * 0.5f + 0.5f;

                            // Create obstacle info
                            var obstacles = new List<Interfaces.ObstacleInfo>
                            {
                                new Interfaces.ObstacleInfo(obstaclePosition, avoidanceRadius)
                            };

                            // Try to find path around obstacle from current position to target
                            IList<Vector3> newPath = currentArea.NavigationMesh.FindPathAroundObstacles(
                                transform.Position,
                                targetTransform.Position,
                                obstacles);

                            if (newPath != null && newPath.Count > 0)
                            {
                                // Found alternative path around obstacle - use it for direct movement
                                // For ActionMoveToObject, we'll adjust direction to follow the first waypoint
                                Vector3 firstWaypoint = newPath[0];
                                Vector3 adjustedDirection = firstWaypoint - transform.Position;
                                adjustedDirection.Y = 0; // Ignore vertical
                                if (adjustedDirection.LengthSquared() > 0.001f)
                                {
                                    adjustedDirection = Vector3.Normalize(adjustedDirection);
                                    // Use adjusted direction for movement this frame
                                    Vector3 adjustedNewPosition = currentPosition + adjustedDirection * moveDistance;

                                    // Project to walkmesh
                                    Vector3 projectedPos;
                                    float height;
                                    if (currentArea.NavigationMesh.ProjectToSurface(adjustedNewPosition, out projectedPos, out height))
                                    {
                                        adjustedNewPosition = projectedPos;
                                    }

                                    // Check if adjusted path is clear
                                    uint adjustedBlockingId;
                                    Vector3 adjustedNormal;
                                    bool adjustedHasCollision = collisionDetector.CheckCreatureCollision(
                                        actor, currentPosition, adjustedNewPosition, out adjustedBlockingId, out adjustedNormal, _targetObjectId);

                                    if (!adjustedHasCollision)
                                    {
                                        // Adjusted path is clear - use it
                                        transform.Position = adjustedNewPosition;
                                        transform.Facing = (float)Math.Atan2(adjustedDirection.Y, adjustedDirection.X);
                                        ClearBumpCounter(actor);
                                        return ActionStatus.InProgress;
                                    }
                                }
                            }
                        }
                    }
                }

                // Could not find path around obstacle - action fails
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): If FUN_0054a1f0 returns null, movement is aborted
                return ActionStatus.Failed;
            }

            // Clear bump counter if no collision
            ClearBumpCounter(actor);

            transform.Position = newPosition;
            // Y-up system: Atan2(Y, X) for 2D plane facing
            transform.Facing = (float)Math.Atan2(direction2.Y, direction2.X);

            return ActionStatus.InProgress;
        }

        /// <summary>
        /// Returns the collision radius for an entity derived from its bounding box.
        /// </summary>
        /// <remarks>
        /// Collision Radius Calculation:
        /// - Based on swkotor.exe and swkotor2.exe reverse engineering via Ghidra MCP
        /// - K1 (swkotor.exe): FUN_004ed6e0 @ 0x004ed6e0 updates bounding box from appearance.2da
        ///   - Radius stored at offset +8 from bounding box pointer (at offset 0x340)
        ///   - FUN_004f1310 @ 0x004f1310: `*(float *)(*(int *)(iVar3 + 0x340) + 8)` gets radius for collision distance
        ///   - Uses appearance.2da HITRADIUS column, defaults to 0.6f (0x3f19999a) if not found
        /// - K2 (swkotor2.exe): FUN_005479f0 @ 0x005479f0 uses width at +0x14 and height at +0xbc
        ///   - Width and depth are half-extents, radius is typically max(width, depth) for horizontal collision
        /// - Implementation: Uses collision detector's GetCreatureBoundingBoxPublic to get engine-specific bounding box
        ///   - Derives radius as maximum of width and depth (horizontal extent)
        ///   - Falls back to default 0.6f if bounding box cannot be determined
        /// - This ensures 1:1 parity with original engine behavior through proper collision detector abstraction
        /// </remarks>
        private float GetEntityCollisionRadius(IEntity entity, IWorld world)
        {
            if (entity == null)
            {
                return 0.6f; // Default radius matching K1/K2 default (0x3f19999a = 0.6f)
            }

            // Get or create collision detector (lazy initialization with world access)
            BaseCreatureCollisionDetector collisionDetector = GetOrCreateCollisionDetector(world);

            // Use collision detector to get bounding box (handles engine-specific logic)
            // Based on swkotor.exe: FUN_004f1310 gets radius from bounding box at offset +8
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_005479f0 uses width at +0x14 for collision
            CreatureBoundingBox boundingBox = collisionDetector.GetCreatureBoundingBoxPublic(entity);

            // Derive radius from bounding box
            // For horizontal collision (2D movement), use maximum of width and depth
            // This matches original engine behavior where radius represents horizontal extent
            // K1 uses radius directly, K2 uses max(width, depth) for horizontal collision
            float radius = Math.Max(boundingBox.Width, boundingBox.Depth);

            // Ensure minimum radius (matches K1/K2 default of 0.6f)
            // Based on swkotor.exe: Default width is 0.6f (0x3f19999a) if not found in appearance.2da
            if (radius < 0.6f)
            {
                radius = 0.6f;
            }

            return radius;
        }

        /// <summary>
        /// Gets the bump counter for an entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Bump counter stored at offset 0x268 in entity structure.
        /// </summary>
        private int GetBumpCounter(IEntity entity)
        {
            if (entity is Entity concreteEntity)
            {
                return concreteEntity.GetData<int>(BumpCounterKey, 0);
            }
            return 0;
        }

        /// <summary>
        /// Sets the bump counter for an entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Bump counter stored at offset 0x268 in entity structure.
        /// </summary>
        private void SetBumpCounter(IEntity entity, int count)
        {
            if (entity is Entity concreteEntity)
            {
                concreteEntity.SetData(BumpCounterKey, count);
            }
        }

        /// <summary>
        /// Clears the bump counter for an entity.
        /// </summary>
        private void ClearBumpCounter(IEntity entity)
        {
            if (entity is Entity concreteEntity)
            {
                concreteEntity.SetData(BumpCounterKey, 0);
            }
        }

        /// <summary>
        /// Gets the last blocking creature ObjectId for an entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Stored at offset 0x254 in entity structure.
        /// </summary>
        private uint GetLastBlockingCreature(IEntity entity)
        {
            if (entity is Entity concreteEntity)
            {
                return concreteEntity.GetData<uint>(LastBlockingCreatureKey, 0x7F000000);
            }
            return 0x7F000000; // OBJECT_INVALID
        }

        /// <summary>
        /// Sets the last blocking creature ObjectId for an entity.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Stored at offset 0x254 in entity structure.
        /// </summary>
        private void SetLastBlockingCreature(IEntity entity, uint creatureId)
        {
            if (entity is Entity concreteEntity)
            {
                concreteEntity.SetData(LastBlockingCreatureKey, creatureId);
            }
        }
    }
}

