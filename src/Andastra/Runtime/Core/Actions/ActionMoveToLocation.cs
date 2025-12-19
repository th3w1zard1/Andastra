using System;
using System.Collections.Generic;
using System.Numerics;
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

        // Bump counter tracking (matches offset 0x268 in swkotor2.exe entity structure)
        // Based on swkotor2.exe: FUN_0054be70 @ 0x0054be70 tracks bump count at param_1[0xe0] + 0x268
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
            // Walking collision checking: FUN_0054be70 @ 0x0054be70
            // Located via string references:
            //   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
            //   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
            //   - "aborted walking, Maximum number of bumps happened" @ 0x007c0458
            // Original implementation (from decompiled FUN_0054be70):
            //   - Function signature: `undefined4 FUN_0054be70(int *param_1, float *param_2, float *param_3, float *param_4, int *param_5)`
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
            // Based on swkotor2.exe: FUN_0054be70 @ 0x0054be70 projects positions to walkmesh after movement
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
            uint blockingCreatureId;
            Vector3 collisionNormal;
            bool hasCollision = CheckCreatureCollision(actor, currentPosition, newPosition, out blockingCreatureId, out collisionNormal);

            if (hasCollision)
            {
                // Get bump counter (stored at offset 0x268 in swkotor2.exe entity structure)
                // Based on swkotor2.exe: FUN_0054be70 @ 0x0054be70 tracks bump count at param_1[0xe0] + 0x268
                int bumpCount = GetBumpCounter(actor);
                bumpCount++;
                SetBumpCounter(actor, bumpCount);

                // Check if maximum bumps exceeded
                // Based on swkotor2.exe: FUN_0054be70 aborts movement if bump count > 5
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
                // Based on swkotor2.exe: FUN_0054be70 checks if local_c0 == entity ID at offset 0x254
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
                // Based on swkotor2.exe: FUN_0054be70 calls FUN_0054a1f0 for pathfinding around obstacles
                // TODO: SIMPLIFIED - For now, we'll just stop movement and let the action fail
                // TODO: Implement pathfinding around obstacles (FUN_0054a1f0)
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
        /// Checks for creature collisions along a movement path.
        /// Based on swkotor2.exe: FUN_005479f0 @ 0x005479f0
        /// </summary>
        /// <param name="actor">The entity moving.</param>
        /// <param name="startPos">Start position of movement.</param>
        /// <param name="endPos">End position of movement.</param>
        /// <param name="blockingCreatureId">Output: ObjectId of blocking creature (0x7F000000 if none).</param>
        /// <param name="collisionNormal">Output: Collision normal vector.</param>
        /// <returns>True if collision detected, false if path is clear.</returns>
        private bool CheckCreatureCollision(IEntity actor, Vector3 startPos, Vector3 endPos, out uint blockingCreatureId, out Vector3 collisionNormal)
        {
            blockingCreatureId = 0x7F000000; // OBJECT_INVALID
            collisionNormal = Vector3.Zero;

            // Based on swkotor2.exe: FUN_005479f0 @ 0x005479f0
            // Located via string references:
            //   - "aborted walking, Bumped into this creature at this position already." @ 0x007c03c0
            //   - "aborted walking, we are totaly blocked. can't get around this creature at all." @ 0x007c0408
            // Original implementation: Checks if movement path intersects with other creatures
            // Uses FUN_004e17a0 and FUN_004f5290 for collision detection with creature bounding boxes
            // Checks creature positions and handles blocking detection
            // Returns 0 if collision detected, 1 if clear

            IWorld world = actor.World;
            if (world == null)
            {
                return false;
            }

            // Get all creatures in the area
            IArea area = world.CurrentArea;
            if (area == null)
            {
                return false;
            }

            // Calculate movement direction and distance
            Vector3 movementDir = endPos - startPos;
            float movementDistance = movementDir.Length();
            if (movementDistance < 0.001f)
            {
                return false; // No movement, no collision
            }

            Vector3 normalizedDir = Vector3.Normalize(movementDir);

            // Check collision with all creatures in the area
            // Based on swkotor2.exe: FUN_005479f0 iterates through creature list
            // Uses bounding box collision detection (FUN_004e17a0, FUN_004f5290)
            foreach (IEntity entity in world.GetAllEntities())
            {
                // Skip self
                if (entity.ObjectId == actor.ObjectId)
                {
                    continue;
                }

                // Only check creatures
                if ((entity.ObjectType & ObjectType.Creature) == 0)
                {
                    continue;
                }

                ITransformComponent entityTransform = entity.GetComponent<ITransformComponent>();
                if (entityTransform == null)
                {
                    continue;
                }

                // Get creature bounding box
                // Based on swkotor2.exe: FUN_005479f0 uses creature bounding box from entity structure
                // Bounding box stored at offset 0x380 + 0x14 (width), 0x380 + 0xbc (height)
                // TODO: SIMPLIFIED - For now, use a simple radius-based collision check
                // TODO: Implement proper bounding box collision (FUN_004e17a0, FUN_004f5290)
                float creatureRadius = GetCreatureRadius(entity);

                Vector3 entityPos = entityTransform.Position;

                // Check if movement path intersects with creature bounding sphere
                // Simple line-sphere intersection test
                Vector3 toEntity = entityPos - startPos;
                float projectionLength = Vector3.Dot(toEntity, normalizedDir);

                // Clamp projection to movement segment
                if (projectionLength < 0 || projectionLength > movementDistance)
                {
                    continue; // Entity is outside movement path
                }

                // Calculate closest point on movement path to entity
                Vector3 closestPoint = startPos + normalizedDir * projectionLength;
                float distanceToEntity = Vector3.Distance(closestPoint, entityPos);

                // Check if distance is less than combined radii
                float actorRadius = GetCreatureRadius(actor);
                if (distanceToEntity < (actorRadius + creatureRadius))
                {
                    // Collision detected
                    blockingCreatureId = entity.ObjectId;
                    collisionNormal = Vector3.Normalize(entityPos - closestPoint);
                    return true;
                }
            }

            return false; // No collision
        }

        /// <summary>
        /// Gets the creature radius for collision detection.
        /// Based on swkotor2.exe: FUN_0065a380 @ 0x0065a380 (GetCreatureRadius) calls FUN_0041d2c0 to lookup "hitradius" from appearance.2da
        /// Located via string references: "GetCreatureRadius" @ 0x007bb128
        /// Original implementation: Gets creature collision radius from appearance.2da hitradius column
        /// Falls back to size-based defaults if appearance data unavailable
        /// </summary>
        private float GetCreatureRadius(IEntity entity)
        {
            if (entity == null)
            {
                return 0.5f; // Default radius
            }

            // Get appearance type from entity
            // Based on swkotor2.exe: FUN_0065a380 @ 0x0065a380 (GetCreatureRadius) gets appearance type from entity
            // Located via string references: "AppearanceType" @ 0x007c84c8, "Appearance_Type" @ 0x007c40f0
            // Original implementation: Gets appearance type from entity structure at offset 0x224 + 0x18
            int appearanceType = -1;

            // First, try to get appearance type from IRenderableComponent (engine-agnostic)
            // Based on swkotor2.exe: Appearance data stored in renderable component
            // IRenderableComponent.AppearanceRow is the index into appearance.2da
            Core.Interfaces.Components.IRenderableComponent renderable = entity.GetComponent<Core.Interfaces.Components.IRenderableComponent>();
            if (renderable != null)
            {
                appearanceType = renderable.AppearanceRow;
            }

            // If not found, try to get appearance type from engine-specific creature component using reflection
            // Note: This works with engine-specific components (OdysseyCreatureComponent, AuroraCreatureComponent, etc.)
            // Based on swkotor2.exe: Creature component stores AppearanceType property
            if (appearanceType < 0)
            {
                var entityType = entity.GetType();
                var appearanceTypeProp = entityType.GetProperty("AppearanceType");
                if (appearanceTypeProp != null)
                {
                    try
                    {
                        object appearanceTypeValue = appearanceTypeProp.GetValue(entity);
                        if (appearanceTypeValue is int)
                        {
                            appearanceType = (int)appearanceTypeValue;
                        }
                    }
                    catch
                    {
                        // Ignore reflection errors
                    }
                }
            }

            // If we have a valid appearance type and world has GameDataProvider, use it
            // Based on swkotor2.exe: FUN_0065a380 @ 0x0065a380 calls FUN_0041d2c0 to lookup "hitradius" from appearance.2da
            // Original implementation: Looks up hitradius column from appearance.2da table using appearanceType as row index
            // Cross-engine: All engines (Odyssey, Aurora, Eclipse, Infinity) use IGameDataProvider for appearance data lookup
            if (appearanceType >= 0 && entity.World != null && entity.World.GameDataProvider != null)
            {
                return entity.World.GameDataProvider.GetCreatureRadius(appearanceType, 0.5f);
            }

            // Default radius for medium creatures (most common)
            // Based on swkotor2.exe: Default creature radius is approximately 0.5 units
            // Falls back to default if appearance data unavailable or GameDataProvider not available
            return 0.5f;
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

