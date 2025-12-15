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
            // Original implementation: Uses walkmesh A* pathfinding to find path to destination
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

            transform.Position += direction * moveDistance;
            // Set facing to match movement direction (Y-up system: Atan2(Y, X) for 2D plane)
            transform.Facing = (float)Math.Atan2(direction.Y, direction.X);

            return ActionStatus.InProgress;
        }
    }
}

