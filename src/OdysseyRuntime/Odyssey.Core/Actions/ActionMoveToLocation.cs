using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Actions
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
            //   - Distance calculation: Uses 2D distance (X/Z plane, ignores Y) for movement calculations
            //   - Walkmesh projection: Projects position to walkmesh surface using FUN_004f5070 (walkmesh height lookup)
            //   - Direction normalization: Uses FUN_004d8390 to normalize direction vectors
            //   - Creature collision: Checks for collisions with other creatures along path using FUN_005479f0
            //   - Bump counter: Tracks number of creature bumps (stored at offset 0x268 in entity structure)
            //   - Maximum bumps: If bump count exceeds 5, aborts movement and clears path (frees path array, sets path length to 0)
            //   - Total blocking: If same creature blocks repeatedly (local_c0 == entity ID at offset 0x254), aborts movement
            //   - Path completion: Returns 1 when path is complete (local_d0 flag), 0 if still in progress
            //   - Path waypoint iteration: Advances through path waypoints, projects each position to walkmesh
            //   - Movement distance: Calculates movement distance based on speed and remaining distance to waypoint
            //   - Final position: Updates entity position to final projected position on walkmesh
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

            Vector3 newPosition = transform.Position + direction * moveDistance;
            
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
            
            transform.Position = newPosition;
            // Set facing to match movement direction (Y-up system: Atan2(Y, X) for 2D plane)
            transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

            return ActionStatus.InProgress;
        }
    }
}

