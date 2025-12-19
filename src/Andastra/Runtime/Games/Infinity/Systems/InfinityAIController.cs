using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Engines.Infinity.Systems
{
    /// <summary>
    /// AI controller system for NPCs in Infinity engine (Baldur's Gate, Icewind Dale, Planescape: Torment).
    /// Handles perception, combat behavior, and action queue management for non-player creatures.
    /// </summary>
    /// <remarks>
    /// Infinity AI Controller System:
    /// - Based on Infinity engine AI system (Baldur's Gate, Icewind Dale, Planescape: Torment)
    /// - Original implementation: TODO: stub - needs reverse engineering
    /// - AI operates through action queue population based on perception and scripts
    /// - Heartbeat scripts: Fire every 6 seconds (HeartbeatInterval), can queue actions, check conditions
    /// - Perception system: Detects enemies via sight/hearing, fires OnPerception events
    /// - Perception update: Checks every 0.5 seconds (PerceptionUpdateInterval) for efficiency
    /// - Combat behavior: Real-time with pause tactical combat
    /// - Action queue: FIFO queue per entity, current action executes until complete or interrupted
    /// - Idle behavior: Patrol routes, random wandering, idle animations, look-around behavior
    /// - Based on Infinity engine AI behavior (needs reverse engineering)
    /// </remarks>
    public class InfinityAIController : BaseAIControllerSystem
    {
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScriptEvent;
        private readonly Dictionary<IEntity, IdleState> _idleStates;
        private readonly Dictionary<IEntity, float> _idleTimers;
        private readonly Random _random;

        // Idle behavior constants
        private const float IdleWanderRadius = 5.0f; // Maximum distance to wander from spawn point
        private const float IdleWanderInterval = 10.0f; // Seconds between wander decisions
        private const float IdleLookAroundInterval = 5.0f; // Seconds between look-around actions
        private const float IdleAnimationInterval = 8.0f; // Seconds between idle animation triggers
        private const float PatrolWaitTime = 2.0f; // Seconds to wait at patrol waypoint

        public InfinityAIController([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScriptEvent)
            : base(world)
        {
            _fireScriptEvent = fireScriptEvent ?? throw new ArgumentNullException(nameof(fireScriptEvent));
            _idleStates = new Dictionary<IEntity, IdleState>();
            _idleTimers = new Dictionary<IEntity, float>();
            _random = new Random();
        }

        /// <summary>
        /// Fires heartbeat script for a creature (Infinity-specific: uses script system).
        /// Based on Infinity engine: Heartbeat script system
        /// </summary>
        protected override void FireHeartbeatScript(IEntity creature)
        {
            // TODO: STUB - Implement Infinity-specific heartbeat script firing
            // Infinity engine uses its own script system
        }

        /// <summary>
        /// Checks perception for a creature (Infinity-specific: uses perception system).
        /// Based on Infinity engine: Perception system
        /// </summary>
        protected override void CheckPerception(IEntity creature)
        {
            // TODO: STUB - Implement Infinity-specific perception checking
            // Infinity engine uses its own perception system
        }

        /// <summary>
        /// Handles combat AI for a creature (Infinity-specific: real-time with pause tactical combat).
        /// Based on Infinity engine: Combat system
        /// </summary>
        protected override void HandleCombatAI(IEntity creature)
        {
            // TODO: STUB - Implement Infinity-specific combat AI
            // Infinity engine uses real-time with pause tactical combat
        }

        /// <summary>
        /// Updates AI for a single creature, including idle behavior timing.
        /// </summary>
        protected override void UpdateCreatureAI(IEntity creature, float deltaTime)
        {
            // Store deltaTime for idle behavior (HandleIdleAI doesn't receive it)
            _idleTimers[creature] = deltaTime;

            // Call base implementation
            base.UpdateCreatureAI(creature, deltaTime);
        }

        /// <summary>
        /// Handles idle AI for a creature (Infinity-specific: patrol routes, random wandering, idle animations).
        /// Based on Infinity engine: Idle behavior system with patrol routes and random wandering
        /// </summary>
        /// <remarks>
        /// Infinity Idle Behavior:
        /// - Checks for patrol waypoints assigned to creature (via tag or component)
        /// - If patrol waypoints exist, follows patrol route in sequence
        /// - If no patrol, performs random wandering within spawn radius
        /// - Plays idle animations periodically when standing still
        /// - Performs look-around behavior to make NPCs appear more alive
        /// - Uses action queue to queue movement actions
        /// - Based on Infinity engine idle behavior patterns
        /// </remarks>
        protected override void HandleIdleAI(IEntity creature)
        {
            if (creature == null || !creature.IsValid)
            {
                return;
            }

            // Get delta time for this creature (stored in UpdateCreatureAI)
            float deltaTime = _idleTimers.ContainsKey(creature) ? _idleTimers[creature] : 0.016f; // Default to ~60fps if not set

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

            // Project destination to navigation mesh if available
            if (_world.CurrentArea != null && _world.CurrentArea.NavigationMesh != null)
            {
                Vector3? projected = _world.CurrentArea.NavigationMesh.ProjectPoint(destination);
                if (projected.HasValue)
                {
                    destination = projected.Value;
                }
            }

            // Queue movement action
            var moveAction = new ActionMoveToLocation(destination, false);
            actionQueue.Add(moveAction);

            idleState.LastWanderTime = 0f; // Reset timer
        }

        /// <summary>
        /// Handles look-around behavior to make NPCs appear more alive.
        /// </summary>
        private void HandleLookAroundBehavior(IEntity creature, IdleState idleState, IActionQueueComponent actionQueue, float deltaTime)
        {
            if (actionQueue == null || actionQueue.CurrentAction != null)
            {
                return;
            }

            // Update look-around timer
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

            // Randomly look in a direction
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

            idleState.LastLookAroundTime = 0f; // Reset timer
        }

        /// <summary>
        /// Handles idle animations for a creature.
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

            // Play idle animation (animation ID 0 is typically idle/stand)
            // Infinity engine uses different animation IDs, but 0 is a safe default for idle
            if (animation.CurrentAnimation == -1 || animation.AnimationComplete)
            {
                animation.PlayAnimation(0, 1.0f, true); // Play idle animation, looping
            }

            idleState.LastIdleAnimationTime = 0f; // Reset timer
        }

        /// <summary>
        /// Gets the spawn position for a creature (used as wander center).
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
        /// Gets patrol waypoints for a creature (if assigned via tag or component).
        /// </summary>
        private List<IEntity> GetPatrolWaypoints(IEntity creature)
        {
            List<IEntity> waypoints = new List<IEntity>();

            if (_world.CurrentArea == null)
            {
                return waypoints;
            }

            // Check if creature has a patrol tag (e.g., "PATROL_01" would look for waypoints "PATROL_01_01", "PATROL_01_02", etc.)
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

