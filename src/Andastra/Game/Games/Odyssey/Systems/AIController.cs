using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Systems
{
    /// <summary>
    /// AI controller system for NPCs in Odyssey engine (KOTOR, KOTOR 2, Jade Empire).
    /// Handles perception, combat behavior, and action queue management for non-player creatures.
    /// </summary>
    /// <remarks>
    /// Odyssey AI Controller System:
    /// - Based on swkotor.exe/swkotor2.exe AI system
    /// - Located via string references: ["OnHeartbeat"] @ (K1: TODO: Find this address, TSL: 0x007c1f60), ["OnPerception"] @ (K1: TODO: Find this address, TSL: 0x007c1f64)
    /// - ["OnCombatRoundEnd"] @ (K1: TODO: Find this address, TSL: 0x007c1f68), ["OnDamaged"] @ (K1: TODO: Find this address, TSL: 0x007c1f6c), ["OnDeath"] @ (K1: TODO: Find this address, TSL: 0x007c1f70)
    /// - AI state: "PT_AISTATE" @ 0x007c1768 (party AI state in PARTYTABLE, swkotor2.exe), ["AISTATE"] @ (K1: TODO: Find this address, TSL: 0x007c81f8, ["AIState"] @ (K1: TODO: Find this address, TSL: 0x007c4090)
    /// - AI scripts: ["aiscripts"] @ (K1: TODO: Find this address, TSL: 0x007c4fd0) - AI script directory/resource
    /// - Pathfinding errors:
    ///   - "?The Path find has Failed... Why?" @ (K1: TODO: Find this address, TSL: 0x007c055f)
    ///   - "Bailed the desired position is unsafe." @ (K1: TODO: Find this address, TSL: 0x007c0584)
    ///   - "    failed to grid based pathfind from the creatures position to the starting path point." @ (K1: TODO: Find this address, TSL: 0x007be510)
    ///   - "    failed to grid based pathfind from the ending path point ot the destiantion." @ (K1: TODO: Find this address, TSL: 0x007be4b8)
    /// - Script hooks: "k_def_pathfail01" @ (K1: TODO: Find this address, TSL: 0x007c52fc) - pathfinding failure script example
    /// - Debug: "    AI Level: " @ (K1: TODO: Find this address, TSL: 0x007cb174) - AI level debug display
    /// - Original implementation: [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004eb750) - creature AI update loop
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x005226d0) - process heartbeat scripts
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004dfbb0) - perception checks
    /// - AI operates through action queue population based on perception and scripts
    /// - Heartbeat scripts: Fire every 6 seconds (HeartbeatInterval), can queue actions, check conditions
    /// - Perception system: Detects enemies via sight/hearing, fires OnPerception events
    /// - Perception update: Checks every 0.5 seconds (PerceptionUpdateInterval) for efficiency
    /// - Combat behavior: Default combat AI engages nearest enemy, uses combat rounds
    /// - Action queue: FIFO queue per entity, current action executes until complete or interrupted
    /// - AI levels: 0=Passive, 1=Defensive, 2=Normal, 3=Aggressive (stored in PT_AISTATE for party members)
    /// - Party AI: Party members use AI controller when not player-controlled (PT_AISTATE from PARTYTABLE)
    /// - Based on KOTOR AI behavior from vendor/PyKotor/wiki/ and plan documentation
    /// </remarks>
    public class AIController : Runtime.Games.Common.BaseAIControllerSystem
    {
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScriptEvent;
        private readonly Dictionary<IEntity, IdleState> _idleStates;
        private readonly Dictionary<IEntity, float> _idleTimers;
        private readonly Random _random;

        // Idle behavior constants (Odyssey-specific values based on swkotor2.exe behavior)
        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Idle behavior timing and distances
        // Located via string references: ActionRandomWalk implementation in swkotor2.exe
        // Original implementation: [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x00508260) - load ActionList, ActionRandomWalk action type
        private const float IdleWanderRadius = 5.0f; // Maximum distance to wander from spawn point (swkotor2.exe default)
        private const float IdleWanderInterval = 10.0f; // Seconds between wander decisions (swkotor2.exe default)
        private const float IdleLookAroundInterval = 5.0f; // Seconds between look-around actions (Odyssey-specific)
        private const float IdleAnimationInterval = 8.0f; // Seconds between idle animation triggers (Odyssey-specific)
        private const float PatrolWaitTime = 2.0f; // Seconds to wait at patrol waypoint (swkotor2.exe default)

        public AIController([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScriptEvent)
            : base(world)
        {
            _fireScriptEvent = fireScriptEvent ?? throw new ArgumentNullException(nameof(fireScriptEvent));
            _idleStates = new Dictionary<IEntity, IdleState>();
            _idleTimers = new Dictionary<IEntity, float>();
            _random = new Random();
        }

        /// <summary>
        /// Fires heartbeat script for a creature (Odyssey-specific: uses ScriptEvent system).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x005226d0) - process heartbeat scripts
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
        /// Checks perception for a creature (Odyssey-specific: uses walkmesh line-of-sight).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004dfbb0) - perception checks
        /// </summary>
        protected override void CheckPerception(IEntity creature)
        {
            IPerceptionComponent perception = creature.GetComponent<IPerceptionComponent>();
            if (perception == null)
            {
                return;
            }

            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            if (transform == null)
            {
                return;
            }

            float sightRange = perception.SightRange;
            float hearingRange = perception.HearingRange;
            float maxRange = Math.Max(sightRange, hearingRange);

            // Get all creatures in perception range
            var nearbyCreatures = _world.GetEntitiesInRadius(
                transform.Position,
                maxRange,
                ObjectType.Creature);

            foreach (var other in nearbyCreatures)
            {
                if (other == creature || !other.IsValid)
                {
                    continue;
                }

                // Check if we can see/hear this creature
                bool canSee = CanSee(creature, other, sightRange);
                bool canHear = CanHear(creature, other, hearingRange);

                if (canSee || canHear)
                {
                    // Fire OnPerception event
                    _fireScriptEvent(creature, ScriptEvent.OnPerception, other);
                }
            }
        }

        /// <summary>
        /// Checks if subject can see target (Odyssey-specific: uses walkmesh line-of-sight).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) perception system with walkmesh raycast.
        /// </summary>
        private bool CanSee(IEntity subject, IEntity target, float range)
        {
            ITransformComponent subjectTransform = subject.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            float distance = Vector3.Distance(subjectTransform.Position, targetTransform.Position);
            if (distance > range)
            {
                return false;
            }

            // Line-of-sight check through walkmesh
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) perception system
            // Located via string references: Line-of-sight checks in perception functions
            // Original implementation: Uses walkmesh raycast to check if target is visible
            if (_world.CurrentArea != null)
            {
                INavigationMesh navMesh = _world.CurrentArea.NavigationMesh;
                if (navMesh != null)
                {
                    // Check line-of-sight from subject eye position to target eye position
                    Vector3 subjectEye = subjectTransform.Position + Vector3.UnitY * 1.5f; // Approximate eye height
                    Vector3 targetEye = targetTransform.Position + Vector3.UnitY * 1.5f;

                    // Test if line-of-sight is blocked by walkmesh
                    if (!navMesh.TestLineOfSight(subjectEye, targetEye))
                    {
                        return false; // Line-of-sight blocked
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if subject can hear target (Odyssey-specific: distance-based hearing).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) perception system.
        /// </summary>
        private bool CanHear(IEntity subject, IEntity target, float range)
        {
            ITransformComponent subjectTransform = subject.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            float distance = Vector3.Distance(subjectTransform.Position, targetTransform.Position);
            return distance <= range;
        }

        /// <summary>
        /// Handles combat AI for a creature (Odyssey-specific: attack nearest enemy).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Default combat AI engages nearest enemy.
        /// </summary>
        protected override void HandleCombatAI(IEntity creature)
        {
            // Find nearest enemy
            IEntity nearestEnemy = FindNearestEnemy(creature);
            if (nearestEnemy != null)
            {
                // Queue attack action
                IActionQueueComponent actionQueue = creature.GetComponent<IActionQueueComponent>();
                if (actionQueue != null)
                {
                    // Check if we're already attacking this target
                    IAction currentAction = actionQueue.CurrentAction;
                    if (currentAction is ActionAttack attackAction)
                    {
                        // Already attacking, continue
                        return;
                    }

                    // Queue new attack
                    var attack = new ActionAttack(nearestEnemy.ObjectId);
                    actionQueue.Add(attack);
                }
            }
        }

        private IEntity FindNearestEnemy(IEntity creature)
        {
            ITransformComponent transform = creature.GetComponent<ITransformComponent>();
            IFactionComponent faction = creature.GetComponent<IFactionComponent>();
            if (transform == null || faction == null)
            {
                return null;
            }

            // Get all creatures in range
            var candidates = _world.GetEntitiesInRadius(
                transform.Position,
                50.0f, // Max combat range
                ObjectType.Creature);

            IEntity nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate == creature || !candidate.IsValid)
                {
                    continue;
                }

                // Check if hostile
                if (!faction.IsHostile(candidate))
                {
                    continue;
                }

                // Check if alive
                IStatsComponent stats = candidate.GetComponent<IStatsComponent>();
                if (stats != null && stats.CurrentHP <= 0)
                {
                    continue;
                }

                // Calculate distance
                ITransformComponent candidateTransform = candidate.GetComponent<ITransformComponent>();
                if (candidateTransform != null)
                {
                    float distance = Vector3.Distance(transform.Position, candidateTransform.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = candidate;
                    }
                }
            }

            return nearest;
        }

        /// <summary>
        /// Updates AI for a single creature, including idle behavior timing.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x004eb750) - creature AI update loop
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
        /// Handles idle AI for a creature (Odyssey-specific: patrol routes, random wandering, look-around, idle animations).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Idle behavior system with patrol routes and random wandering
        /// Located via string references: "ActionList" @ 0x007bebdc, "ActionType" @ 0x007bf7f8
        /// Original implementation: [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x00508260) - load ActionList, ActionRandomWalk action type
        /// </summary>
        /// <remarks>
        /// Odyssey Idle Behavior:
        /// - Checks for patrol waypoints assigned to creature (via tag pattern matching)
        /// - If patrol waypoints exist, follows patrol route in sequence
        /// - If no patrol, performs random wandering within spawn radius using ActionRandomWalk
        /// - Plays idle animations periodically when standing still
        /// - Performs look-around behavior to make NPCs appear more alive
        /// - Uses action queue to queue movement actions (ActionMoveToLocation, ActionRandomWalk)
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) idle behavior patterns from reverse engineering
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Patrol route following via waypoint entities
        /// Located via string references: Waypoint entities in GIT file, patrol route patterns
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - ActionRandomWalk implementation
        /// Located via string references: "ActionList" @ 0x007bebdc, "ActionType" @ 0x007bf7f8
        /// Original implementation: [TODO: Function name] @ (K1: TODO: Find this address, TSL: 0x00508260) - load ActionList, ActionRandomWalk action type
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

            // Use ActionRandomWalk for random wandering
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ActionRandomWalk action type in ActionList
            // Original implementation: Picks random direction and distance, uses pathfinding to reach target
            var randomWalkAction = new ActionRandomWalk(IdleWanderRadius, 0f); // 0 = unlimited duration
            actionQueue.Add(randomWalkAction);

            idleState.LastWanderTime = 0f; // Reset timer
        }

        /// <summary>
        /// Handles look-around behavior to make NPCs appear more alive.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Idle behavior patterns for NPCs
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Animation system with idle animations
        /// Located via string references: Animation playback in creature update loop
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

            // Play idle animation (animation ID 0 is typically idle/stand in Odyssey)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Animation ID 0 is typically idle/stand animation
            // Original implementation: Plays idle animation when no other animation is active
            if (animation.CurrentAnimation == -1 || animation.AnimationComplete)
            {
                animation.PlayAnimation(0, 1.0f, true); // Play idle animation, looping
            }

            idleState.LastIdleAnimationTime = 0f; // Reset timer
        }

        /// <summary>
        /// Gets the spawn position for a creature (used as wander center).
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Creature spawn position tracking
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Waypoint entities in GIT file, patrol route patterns
        /// Located via string references: Waypoint entities loaded from GIT file
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) - Patrol route pattern matching via waypoint tags
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

