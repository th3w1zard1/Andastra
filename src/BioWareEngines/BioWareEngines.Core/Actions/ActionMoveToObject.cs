using System;
using System.Numerics;
using BioWareEngines.Core.Entities;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Core.Actions
{
    /// <summary>
    /// Action to move an entity to another object.
    /// Based on NWScript ActionMoveToObject semantics.
    /// </summary>
    /// <remarks>
    /// Move To Object Action:
    /// - Based on swkotor2.exe movement action system
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

        // Bump counter tracking (matches offset 0x268 in swkotor2.exe entity structure)
        // Based on swkotor2.exe: FUN_0054be70 @ 0x0054be70 tracks bump count at param_1[0xe0] + 0x268
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
            // Based on swkotor2.exe: ActionMoveToObject implementation
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
            // Based on swkotor2.exe: FUN_0054be70 @ 0x0054be70 projects positions to walkmesh after movement
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
                    return ActionStatus.Failed;
                }

                SetLastBlockingCreature(actor, blockingCreatureId);

                // Try to navigate around the blocking creature
                // Based on swkotor2.exe: FUN_0054be70 calls FUN_0054a1f0 for pathfinding around obstacles
                // For now, we'll just stop movement and let the action fail
                // TODO: Implement pathfinding around obstacles (FUN_0054a1f0)
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
                // Skip self and target
                if (entity.ObjectId == actor.ObjectId || entity.ObjectId == _targetObjectId)
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
                // For now, use a simple radius-based collision check
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
        /// Based on swkotor2.exe: GetCreatureRadius @ 0x007bb128
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

            // Try to get appearance type from creature component using reflection
            // Note: This requires KOTOR-specific component, but we use reflection to avoid dependency
            // The entity itself may have an appearance-related property
            var entityType = entity.GetType();
            var appearanceTypeProp = entityType.GetProperty("AppearanceType");
            if (appearanceTypeProp != null)
            {
                try
                {
                    // Try to get appearance data from world if available
                    // This would require IWorld to expose GameDataManager, which is KOTOR-specific
                    // For now, use size-based defaults
                    // Size categories: 0=Small, 1=Medium, 2=Large, 3=Huge, 4=Gargantuan
                    // Default radii: Small=0.3, Medium=0.5, Large=0.7, Huge=1.0, Gargantuan=1.5
                    // We'll use a default of 0.5f for medium creatures
                }
                catch
                {
                    // Ignore reflection errors
                }
            }

            // Default radius for medium creatures (most common)
            // Based on swkotor2.exe: Default creature radius is approximately 0.5 units
            return 0.5f;
        }

        /// <summary>
        /// Gets the bump counter for an entity.
        /// Based on swkotor2.exe: Bump counter stored at offset 0x268 in entity structure.
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
        /// Based on swkotor2.exe: Bump counter stored at offset 0x268 in entity structure.
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
        /// Based on swkotor2.exe: Stored at offset 0x254 in entity structure.
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
        /// Based on swkotor2.exe: Stored at offset 0x254 in entity structure.
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

