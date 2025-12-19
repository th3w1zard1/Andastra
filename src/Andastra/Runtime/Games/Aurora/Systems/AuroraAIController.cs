using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Engines.Aurora.Systems
{
    /// <summary>
    /// AI controller system for NPCs in Aurora engine (Neverwinter Nights, Neverwinter Nights 2).
    /// Handles perception, combat behavior, and action queue management for non-player creatures.
    /// </summary>
    /// <remarks>
    /// Aurora AI Controller System:
    /// - Based on nwmain.exe AI system
    /// - Located via string references: "OnHeartbeat" @ 0x140ddb2b8 (nwmain.exe), "ScriptHeartbeat" @ 0x140dddb10 (nwmain.exe)
    /// - Original implementation: ComputeAIState @ 0x140387dc0 (nwmain.exe) - computes AI state based on actions
    /// - ComputeAIStateOnAction @ 0x140387e70 (nwmain.exe) - processes AI state for specific actions
    /// - AIUpdate @ 0x14037be20 (nwmain.exe) - main AI update loop that calls ComputeAIState
    /// - AI operates through action queue population based on perception and scripts
    /// - Heartbeat scripts: Fire every 6 seconds (HeartbeatInterval), can queue actions, check conditions
    /// - Perception system: Detects enemies via sight/hearing, fires OnPerception events
    /// - Perception update: Checks every 0.5 seconds (PerceptionUpdateInterval) for efficiency
    /// - Combat behavior: D20-based tactical AI with positioning
    /// - Action queue: FIFO queue per entity, current action executes until complete or interrupted
    /// - Idle behavior: Patrol routes via waypoints, random wandering, look-around, idle animations
    /// - Based on NWN AI behavior from reverse engineering
    /// </remarks>
    public class AuroraAIController : BaseAIControllerSystem
    {
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScriptEvent;
        private readonly Dictionary<IEntity, IdleState> _idleStates;
        private readonly Dictionary<IEntity, float> _idleTimers;
        private readonly Random _random;

        // Idle behavior constants (Aurora-specific values based on nwmain.exe behavior)
        // Based on nwmain.exe: Idle behavior timing and distances from AIUpdate and ComputeAIState
        // Located via reverse engineering: nwmain.exe @ 0x14037be20 (AIUpdate), 0x140387dc0 (ComputeAIState)
        // Original implementation: Action queue processing in AIUpdate, action type 0x14 sets AI state 0xf
        private const float IdleWanderRadius = 6.0f; // Maximum distance to wander from spawn point (Aurora default, slightly larger than Odyssey)
        private const float IdleWanderInterval = 12.0f; // Seconds between wander decisions (Aurora default, longer than Odyssey)
        private const float IdleLookAroundInterval = 6.0f; // Seconds between look-around actions (Aurora-specific)
        private const float IdleAnimationInterval = 10.0f; // Seconds between idle animation triggers (Aurora-specific)
        private const float PatrolWaitTime = 3.0f; // Seconds to wait at patrol waypoint (Aurora default, longer than Odyssey)

        public AuroraAIController([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScriptEvent)
            : base(world)
        {
            _fireScriptEvent = fireScriptEvent ?? throw new ArgumentNullException(nameof(fireScriptEvent));
            _idleStates = new Dictionary<IEntity, IdleState>();
            _idleTimers = new Dictionary<IEntity, float>();
            _random = new Random();
        }

        /// <summary>
        /// Fires heartbeat script for a creature (Aurora-specific: uses ScriptEvent system).
        /// Based on nwmain.exe: ScriptHeartbeat system
        /// </summary>
        protected override void FireHeartbeatScript(IEntity creature)
        {
            IScriptHooksComponent scriptHooks = creature.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                string heartbeatScript = scriptHooks.GetScript(ScriptEvent.OnHeartbeat);
                if (!string.IsNullOrEmpty(heartbeatScript))
                {
                    _fireScriptEvent(creature, ScriptEvent.OnHeartbeat, null);
                }
            }
        }

        /// <summary>
        /// Checks perception for a creature (Aurora-specific: uses D20 perception system).
        /// Based on nwmain.exe: PerceptionRange system
        /// </summary>
        protected override void CheckPerception(IEntity creature)
        {
            // TODO: STUB - Implement Aurora-specific perception checking
            // Based on nwmain.exe: PerceptionRange @ 0x140dde0e0
            // Aurora uses D20-based perception with skill checks
        }

        /// <summary>
        /// Handles combat AI for a creature (Aurora-specific: D20 tactical combat).
        /// Based on nwmain.exe: ComputeAIState system with tactical positioning
        /// </summary>
        protected override void HandleCombatAI(IEntity creature)
        {
            // TODO: STUB - Implement Aurora-specific combat AI
            // Based on nwmain.exe: ComputeAIState @ 0x140387dc0
            // Aurora uses D20-based tactical combat with positioning
        }

        /// <summary>
        /// Updates AI for a single creature, including idle behavior timing.
        /// Based on nwmain.exe: AIUpdate @ 0x14037be20 (main AI update loop)
        /// </summary>
        protected override void UpdateCreatureAI(IEntity creature, float deltaTime)
        {
            // Update idle timer
            if (!_idleTimers.ContainsKey(creature))
            {
                _idleTimers[creature] = 0f;
            }
            _idleTimers[creature] += deltaTime;

            // Call base implementation
            base.UpdateCreatureAI(creature, deltaTime);
        }

        /// <summary>
        /// Handles idle AI for a creature (Aurora-specific: patrol routes, random wandering, look-around, idle animations).
        /// Based on nwmain.exe: Idle behavior system with patrol routes and random wandering
        /// Located via reverse engineering: AIUpdate @ 0x14037be20, ComputeAIState @ 0x140387dc0, ComputeAIStateOnAction @ 0x140387e70
        /// Original implementation: Action queue processing in AIUpdate, action types processed by ComputeAIStateOnAction
        /// </summary>
        /// <remarks>
        /// Aurora Idle Behavior:
        /// - Checks for patrol waypoints assigned to creature (via tag pattern matching, similar to Odyssey)
        /// - If patrol waypoints exist, follows patrol route in sequence
        /// - If no patrol, performs random wandering within spawn radius using ActionMoveToLocation
        /// - Plays idle animations periodically when standing still
        /// - Performs look-around behavior to make NPCs appear more alive
        /// - Uses action queue to queue movement actions (ActionMoveToLocation)
        /// - Based on nwmain.exe idle behavior patterns from reverse engineering
        /// - Action type 0x14 (20) in ComputeAIStateOnAction sets AI state 0xf (15) for movement actions
        /// </remarks>
        protected override void HandleIdleAI(IEntity creature)
        {
            if (creature == null || !creature.IsValid)
            {
                return;
            }

            // Get delta time for this creature
            float deltaTime = _idleTimers.ContainsKey(creature) ? _idleTimers[creature] : 0f;
            if (deltaTime > 0.1f)
            {
                // Reset timer (will be updated in UpdateCreatureAI)
                _idleTimers[creature] = 0f;
            }

            // Get or create idle state
            if (!_idleStates.TryGetValue(creature, out IdleState idleState))
            {
                idleState = new IdleState
                {
                    SpawnPosition = GetSpawnPosition(creature),
                    LastWanderTime = 0f,
                    LastLookAroundTime = 0f,
                    LastIdleAnimationTime = 0f,
                    PatrolWaypoints = GetPatrolWaypoints(creature),
                    CurrentPatrolIndex = 0
                };
                _idleStates[creature] = idleState;
            }

            // Check if we have an action queue and it's processing
            IActionQueueComponent actionQueue = creature.GetComponent<IActionQueueComponent>();
            if (actionQueue != null && actionQueue.CurrentAction != null)
            {
                // Action is processing, update idle timers but don't queue new actions
                return;
            }

            // Update idle behavior based on state
            if (idleState.PatrolWaypoints != null && idleState.PatrolWaypoints.Count > 0)
            {
                HandlePatrolBehavior(creature, idleState, actionQueue, deltaTime);
            }
            else
            {
                HandleRandomWanderBehavior(creature, idleState, actionQueue, deltaTime);
            }

            // Handle look-around behavior
            HandleLookAroundBehavior(creature, idleState, actionQueue, deltaTime);

            // Handle idle animations
            HandleIdleAnimations(creature, idleState, deltaTime);
        }

        /// <summary>
        /// Handles patrol behavior for a creature following waypoint route.
        /// Based on nwmain.exe: Patrol route following via waypoint entities
        /// Located via reverse engineering: Waypoint entities in area data, patrol route patterns
        /// </summary>
        private void HandlePatrolBehavior(IEntity creature, IdleState idleState, IActionQueueComponent actionQueue, float deltaTime)
        {
            if (actionQueue == null || idleState.PatrolWaypoints == null || idleState.PatrolWaypoints.Count == 0)
            {
                return;
            }

            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Get current patrol waypoint
            IEntity currentWaypoint = idleState.PatrolWaypoints[idleState.CurrentPatrolIndex];
            if (currentWaypoint == null || !currentWaypoint.IsValid)
            {
                // Waypoint invalid, advance to next
                idleState.CurrentPatrolIndex = (idleState.CurrentPatrolIndex + 1) % idleState.PatrolWaypoints.Count;
                return;
            }

            ITransformComponent waypointTransform = currentWaypoint.GetComponent<ITransformComponent>();
            if (waypointTransform == null)
            {
                return;
            }

            // Check if we've reached the current waypoint
            float distanceToWaypoint = Vector3.Distance(transform.Position, waypointTransform.Position);
            if (distanceToWaypoint < 1.0f)
            {
                // Reached waypoint, wait then advance to next
                if (idleState.PatrolWaitTimer <= 0f)
                {
                    idleState.PatrolWaitTimer = PatrolWaitTime;
                }
                else
                {
                    idleState.PatrolWaitTimer -= deltaTime;
                    if (idleState.PatrolWaitTimer <= 0f)
                    {
                        // Advance to next waypoint
                        idleState.CurrentPatrolIndex = (idleState.CurrentPatrolIndex + 1) % idleState.PatrolWaypoints.Count;
                        idleState.PatrolWaitTimer = 0f;
                    }
                }
            }
            else
            {
                // Not at waypoint yet, queue movement if we don't have one
                if (actionQueue.CurrentAction == null)
                {
                    var moveAction = new ActionMoveToLocation(waypointTransform.Position, false);
                    actionQueue.Add(moveAction);
                }
            }
        }

        /// <summary>
        /// Handles random wandering behavior for a creature.
        /// Based on nwmain.exe: Random movement via ActionMoveToLocation
        /// Located via reverse engineering: AIUpdate @ 0x14037be20 processes action queue
        /// Original implementation: Picks random direction and distance, uses pathfinding to reach target
        /// </summary>
        private void HandleRandomWanderBehavior(IEntity creature, IdleState idleState, IActionQueueComponent actionQueue, float deltaTime)
        {
            if (actionQueue == null)
            {
                return;
            }

            // Update wander timer
            idleState.LastWanderTime += deltaTime;

            // Check if it's time to make a new wander decision
            if (idleState.LastWanderTime < IdleWanderInterval)
            {
                return;
            }

            // Only queue new wander if we don't have an action
            if (actionQueue.CurrentAction != null)
            {
                return;
            }

            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Generate random destination within wander radius
            float angle = (float)(_random.NextDouble() * Math.PI * 2.0);
            float distance = (float)(_random.NextDouble() * IdleWanderRadius);
            Vector3 offset = new Vector3(
                (float)Math.Cos(angle) * distance,
                0f,
                (float)Math.Sin(angle) * distance
            );
            Vector3 destination = idleState.SpawnPosition + offset;

            // Project destination to navigation mesh if available (Aurora uses walkmesh for pathfinding)
            if (_world.CurrentArea != null && _world.CurrentArea.NavigationMesh != null)
            {
                Vector3? projected = _world.CurrentArea.NavigationMesh.ProjectPoint(destination);
                if (projected.HasValue)
                {
                    destination = projected.Value;
                }
            }

            // Queue movement action (Aurora uses ActionMoveToLocation, not ActionRandomWalk)
            // Based on nwmain.exe: Action type 0x14 (20) sets AI state 0xf (15) for movement
            var moveAction = new ActionMoveToLocation(destination, false);
            actionQueue.Add(moveAction);

            idleState.LastWanderTime = 0f; // Reset timer
        }

        /// <summary>
        /// Handles look-around behavior to make NPCs appear more alive.
        /// Based on nwmain.exe: Idle behavior patterns for NPCs
        /// </summary>
        private void HandleLookAroundBehavior(IEntity creature, IdleState idleState, IActionQueueComponent actionQueue, float deltaTime)
        {
            if (actionQueue == null || actionQueue.CurrentAction != null)
            {
                return;
            }

            idleState.LastLookAroundTime += deltaTime;
            if (idleState.LastLookAroundTime < IdleLookAroundInterval)
            {
                return;
            }

            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Randomly look in a direction (brief movement to face that direction)
            float randomAngle = (float)(_random.NextDouble() * Math.PI * 2.0);
            float lookDistance = 3.0f;
            Vector3 lookTarget = transform.Position + new Vector3(
                (float)Math.Cos(randomAngle) * lookDistance,
                0f,
                (float)Math.Sin(randomAngle) * lookDistance
            );

            // Queue a brief look action (face the direction)
            var lookAction = new ActionMoveToLocation(lookTarget, false);
            actionQueue.Add(lookAction);

            idleState.LastLookAroundTime = 0f;
        }

        /// <summary>
        /// Handles idle animations for a creature.
        /// Based on nwmain.exe: Animation system with idle animations
        /// Located via reverse engineering: Animation playback in creature update loop
        /// </summary>
        private void HandleIdleAnimations(IEntity creature, IdleState idleState, float deltaTime)
        {
            IActionQueueComponent actionQueue = creature.GetComponent<IActionQueueComponent>();
            if (actionQueue != null && actionQueue.CurrentAction != null)
            {
                // Don't play idle animations while moving
                return;
            }

            IAnimationComponent animation = creature.GetComponent<IAnimationComponent>();
            if (animation == null)
            {
                return;
            }

            // Update animation timer
            idleState.LastIdleAnimationTime += deltaTime;

            if (idleState.LastIdleAnimationTime < IdleAnimationInterval)
            {
                return;
            }

            // Play idle animation (animation ID 0 is typically idle/stand in Aurora)
            // Based on nwmain.exe: Animation ID 0 is typically idle/stand animation
            // Original implementation: Plays idle animation when no other animation is active
            if (animation.CurrentAnimation == -1 || animation.AnimationComplete)
            {
                animation.PlayAnimation(0, 1.0f, true); // Play idle animation, looping
            }

            idleState.LastIdleAnimationTime = 0f; // Reset timer
        }

        /// <summary>
        /// Gets the spawn position for a creature (used as wander center).
        /// Based on nwmain.exe: Creature spawn position tracking
        /// </summary>
        private Vector3 GetSpawnPosition(IEntity creature)
        {
            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform != null)
            {
                return transform.Position;
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Gets patrol waypoints for a creature (if assigned via tag pattern matching).
        /// Based on nwmain.exe: Waypoint entities in area data, patrol route patterns
        /// Located via reverse engineering: Waypoint entities loaded from area files
        /// Original implementation: Creatures with patrol tags (e.g., "PATROL_01") look for waypoints with matching prefix
        /// </summary>
        private List<IEntity> GetPatrolWaypoints(IEntity creature)
        {
            List<IEntity> waypoints = new List<IEntity>();

            if (_world.CurrentArea == null)
            {
                return waypoints;
            }

            // Check if creature has a patrol tag (e.g., "PATROL_01" would look for waypoints "PATROL_01_01", "PATROL_01_02", etc.)
            // Based on nwmain.exe: Patrol route pattern matching via waypoint tags
            string creatureTag = creature.Tag;
            if (string.IsNullOrEmpty(creatureTag))
            {
                return waypoints;
            }

            // Search for waypoints with matching prefix
            foreach (IEntity waypoint in _world.CurrentArea.Waypoints)
            {
                if (waypoint == null || !waypoint.IsValid)
                {
                    continue;
                }

                string waypointTag = waypoint.Tag;
                if (!string.IsNullOrEmpty(waypointTag) && waypointTag.StartsWith(creatureTag + "_", StringComparison.OrdinalIgnoreCase))
                {
                    waypoints.Add(waypoint);
                }
            }

            // Sort waypoints by tag suffix to ensure correct order
            waypoints.Sort((a, b) =>
            {
                string tagA = a.Tag ?? string.Empty;
                string tagB = b.Tag ?? string.Empty;
                return string.Compare(tagA, tagB, StringComparison.OrdinalIgnoreCase);
            });

            return waypoints;
        }

        /// <summary>
        /// Cleans up idle state for a destroyed entity.
        /// </summary>
        public override void OnEntityDestroyed(IEntity entity)
        {
            base.OnEntityDestroyed(entity);
            if (entity != null)
            {
                _idleStates.Remove(entity);
                _idleTimers.Remove(entity);
            }
        }

        /// <summary>
        /// Internal state tracking for idle behavior.
        /// </summary>
        private class IdleState
        {
            public Vector3 SpawnPosition { get; set; }
            public float LastWanderTime { get; set; }
            public float LastLookAroundTime { get; set; }
            public float LastIdleAnimationTime { get; set; }
            public List<IEntity> PatrolWaypoints { get; set; }
            public int CurrentPatrolIndex { get; set; }
            public float PatrolWaitTimer { get; set; }
        }
    }
}

