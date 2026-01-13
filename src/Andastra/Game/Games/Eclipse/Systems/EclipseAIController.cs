using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common;
using Andastra.Runtime.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Engines.Eclipse.Systems
{
    /// <summary>
    /// AI controller system for NPCs in Eclipse engine (Dragon Age series,  series).
    /// Handles perception, combat behavior, and action queue management for non-player creatures.
    /// </summary>
    /// <remarks>
    /// Eclipse AI Controller System:
    /// - Based on daorigins.exe/DragonAge2.exe AI systems
    /// - Located via string references: "OnHeartbeat" @ 0x00af4fd4 (daorigins.exe)
    /// - DragonAge2.exe string references: "PackageAI" @ 0x00bf468c, "SetBehaviourMessage" @ 0x00bfc8b4
    /// - DragonAge2.exe combat strings: "CombatTarget" @ 0x00bf4dc0, "InCombat" @ 0x00bf4c10, "Combatant" @ 0x00bf4664
    /// - DragonAge2.exe game mode: "GameModeCombat" @ 0x00beaf3c, "BInCombatMode" @ 0x00beeed2
    /// - Original implementation: Reverse engineered from daorigins.exe/DragonAge2.exe
    /// - AI operates through action queue population based on perception and scripts
    /// - Heartbeat scripts: Fire every 6 seconds (HeartbeatInterval), can queue actions, check conditions
    /// - Perception system: Detects enemies via sight/hearing, fires OnPerception events
    /// - Perception update: Checks every 0.5 seconds (PerceptionUpdateInterval) for efficiency
    /// - Combat behavior: Complex tactical AI with positioning and abilities
    /// - Action queue: FIFO queue per entity, current action executes until complete or interrupted
    /// - Based on Dragon Age/ AI behavior from reverse engineering
    /// - Eclipse uses UnrealScript message passing system instead of direct script execution
    /// - UnrealScript events: OnHeartbeat, OnPerception, OnCombatRoundEnd, OnDamaged, OnDeath
    /// - Event processing: Events routed through UnrealScript's native event dispatcher system
    /// - Event queuing: Events are queued and processed at frame boundaries to prevent re-entrancy
    /// </remarks>
    public class EclipseAIController : BaseAIControllerSystem
    {
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScriptEvent;
        private readonly Dictionary<IEntity, IdleState> _idleStates;
        private readonly Dictionary<IEntity, float> _idleTimers;
        private readonly Random _random;

        // Idle behavior constants (Eclipse-specific values based on daorigins.exe/DragonAge2.exe behavior)
        // Based on daorigins.exe: Idle behavior timing and distances
        // Located via reverse engineering: AI behavior patterns in daorigins.exe
        // Original implementation: Eclipse uses simpler idle behavior than Odyssey/Aurora (no patrol routes in base implementation)
        private const float IdleWanderRadius = 4.0f; // Maximum distance to wander from spawn point (Eclipse default, smaller than Odyssey/Aurora)
        private const float IdleWanderInterval = 8.0f; // Seconds between wander decisions (Eclipse default)
        private const float IdleLookAroundInterval = 4.0f; // Seconds between look-around actions (Eclipse-specific)
        private const float IdleAnimationInterval = 6.0f; // Seconds between idle animation triggers (Eclipse-specific)

        public EclipseAIController([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScriptEvent)
            : base(world)
        {
            _fireScriptEvent = fireScriptEvent ?? throw new ArgumentNullException(nameof(fireScriptEvent));
            _idleStates = new Dictionary<IEntity, IdleState>();
            _idleTimers = new Dictionary<IEntity, float>();
            _random = new Random();
        }

        /// <summary>
        /// Fires heartbeat script for a creature (Eclipse-specific: uses UnrealScript message passing system).
        /// Based on daorigins.exe: OnHeartbeat system @ 0x00af4fd4
        /// </summary>
        /// <remarks>
        /// Eclipse Heartbeat Script Firing:
        /// - Based on daorigins.exe: OnHeartbeat @ 0x00af4fd4 (daorigins.exe)
        /// - Eclipse uses UnrealScript message passing instead of direct script execution
        /// - Event processing flow:
        ///   1. Check if creature has OnHeartbeat script hook
        ///   2. Fire script event via UnrealScript message passing system
        ///   3. Event is queued and processed at frame boundary
        ///   4. UnrealScript event dispatcher notifies registered listeners
        ///   5. Script execution triggered on entities with matching event hooks
        /// - Event data structures (from reverse engineering):
        ///   - daorigins.exe: "EventListeners" @ 0x00ae8194, "EventId" @ 0x00ae81a4, "EventScripts" @ 0x00ae81bc
        ///   - DragonAge2.exe: "EventListeners" @ 0x00bf543c, "EventId" @ 0x00bf544c, "EventScripts" @ 0x00bf5464
        ///   - : Uses UnrealScript BioEventDispatcher interface
        ///   - : Uses UnrealScript BioEventDispatcher interface
        /// - Command processing: "COMMAND_SIGNALEVENT" @ 0x00af4180 (daorigins.exe) handles event commands
        /// - UnrealScript integration: Events routed through UnrealScript's native event dispatcher system
        /// - Event queuing: Events are queued and processed at frame boundaries to prevent re-entrancy
        /// - Script execution: Script events trigger UnrealScript execution on entities with matching event hooks
        /// </remarks>
        protected override void FireHeartbeatScript(IEntity creature)
        {
            if (creature == null || !creature.IsValid)
            {
                return;
            }

            // Check if creature has OnHeartbeat script hook
            IScriptHooksComponent scriptHooks = creature.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                string heartbeatScript = scriptHooks.GetScript(ScriptEvent.OnHeartbeat);
                if (!string.IsNullOrEmpty(heartbeatScript))
                {
                    // Fire heartbeat script event via UnrealScript message passing system
                    // Based on daorigins.exe: OnHeartbeat @ 0x00af4fd4
                    // Eclipse uses UnrealScript message passing instead of direct script execution
                    // Event is queued and processed at frame boundary via UnrealScript event dispatcher
                    _fireScriptEvent(creature, ScriptEvent.OnHeartbeat, null);
                }
            }
        }

        /// <summary>
        /// Checks perception for a creature (Eclipse-specific: uses UnrealScript perception system).
        /// Based on daorigins.exe: Perception system
        /// </summary>
        /// <remarks>
        /// Eclipse Perception System:
        /// - Based on daorigins.exe/DragonAge2.exe perception systems
        /// - DragonAge2.exe: Enhanced perception with improved line-of-sight calculations
        /// - Eclipse uses UnrealScript-based perception with different architecture than Odyssey/Aurora
        /// - Perception checks:
        ///   1. Get all creatures in perception range (sight/hearing)
        ///   2. Check line-of-sight for sight-based perception (uses navigation mesh raycast)
        ///   3. Check distance for hearing-based perception
        ///   4. Fire OnPerception event for newly detected entities
        /// - Perception component tracks seen/heard objects and updates state
        /// - OnPerception events fire when entities are first detected (not on every check)
        /// - Event processing: Events routed through UnrealScript's native event dispatcher system
        /// - Based on reverse engineering of daorigins.exe perception system
        /// </remarks>
        protected override void CheckPerception(IEntity creature)
        {
            if (creature == null || !creature.IsValid)
            {
                return;
            }

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
                    // Update perception component state
                    perception.UpdatePerception(other, canSee, canHear);

                    // Fire OnPerception event if this is a new detection
                    // Based on daorigins.exe: OnPerception event firing
                    // Eclipse uses UnrealScript message passing for perception events
                    if ((canSee && !perception.WasSeen(other)) || (canHear && !perception.WasHeard(other)))
                    {
                        _fireScriptEvent(creature, ScriptEvent.OnPerception, other);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if subject can see target (Eclipse-specific: uses navigation mesh line-of-sight).
        /// Based on daorigins.exe perception system with navigation mesh raycast.
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

            // Line-of-sight check through navigation mesh
            // Based on daorigins.exe perception system
            // Eclipse uses navigation mesh for line-of-sight checks (similar to Odyssey walkmesh)
            if (_world.CurrentArea != null)
            {
                INavigationMesh navMesh = _world.CurrentArea.NavigationMesh;
                if (navMesh != null)
                {
                    // Check line-of-sight from subject eye position to target eye position
                    Vector3 subjectEye = subjectTransform.Position + Vector3.UnitY * 1.5f; // Approximate eye height
                    Vector3 targetEye = targetTransform.Position + Vector3.UnitY * 1.5f;

                    // Test if line-of-sight is blocked by navigation mesh
                    if (!navMesh.TestLineOfSight(subjectEye, targetEye))
                    {
                        return false; // Line-of-sight blocked
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks if subject can hear target (Eclipse-specific: distance-based hearing).
        /// Based on daorigins.exe perception system.
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
        /// Handles combat AI for a creature (Eclipse-specific: complex tactical combat with abilities and positioning).
        /// Based on daorigins.exe: Combat system with abilities and positioning
        /// </summary>
        /// <remarks>
        /// Eclipse Combat AI:
        /// - Based on daorigins.exe/DragonAge2.exe combat systems
        /// - DragonAge2.exe: Enhanced combat AI with improved party coordination and ability selection
        /// - DragonAge2.exe combat strings: "CombatTarget" @ 0x00bf4dc0, "InCombat" @ 0x00bf4c10, "Combatant" @ 0x00bf4664
        /// - Eclipse uses complex tactical AI with abilities, positioning, and party coordination
        /// - Combat behavior:
        ///   1. Find nearest enemy within combat range
        ///   2. Check if already attacking this target (continue if so)
        ///   3. Queue attack action if no current action
        ///   4. Consider ability usage (future enhancement: tactical ability selection)
        ///   5. Consider positioning (future enhancement: tactical positioning for party coordination)
        /// - Tactical features (future enhancements):
        ///   - Ability selection based on combat situation (ranged vs melee, enemy type, etc.)
        ///   - Positioning for flanking, cover, or party coordination
        ///   - Party coordination (tank, DPS, support roles)
        ///   - Threat assessment and target prioritization
        /// - Current implementation: Basic combat AI that attacks nearest enemy
        /// - Based on reverse engineering of daorigins.exe combat system
        /// </remarks>
        protected override void HandleCombatAI(IEntity creature)
        {
            if (creature == null || !creature.IsValid)
            {
                return;
            }

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
                    // Based on daorigins.exe: Combat AI queues attack actions
                    // Eclipse uses ActionAttack similar to Odyssey/Aurora
                    var attack = new ActionAttack(nearestEnemy.ObjectId);
                    actionQueue.Add(attack);
                }
            }
        }

        /// <summary>
        /// Finds the nearest enemy for a creature.
        /// Based on daorigins.exe: Enemy detection and target selection
        /// </summary>
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
                50.0f, // Max combat range (Eclipse default, similar to Odyssey)
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
        /// Based on daorigins.exe: AI update loop
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
        /// Handles idle AI for a creature (Eclipse-specific: random wandering, look-around, idle animations).
        /// Based on daorigins.exe: Idle behavior system
        /// </summary>
        /// <remarks>
        /// Eclipse Idle Behavior:
        /// - Based on daorigins.exe/DragonAge2.exe idle behavior systems
        /// - DragonAge2.exe: Uses same idle behavior patterns as daorigins.exe with UnrealScript integration
        /// - Eclipse uses simpler idle behavior than Odyssey/Aurora (no patrol routes in base implementation)
        /// - Idle behavior:
        ///   1. Random wandering within spawn radius using ActionMoveToLocation
        ///   2. Look-around behavior to make NPCs appear more alive
        ///   3. Idle animations when standing still
        /// - Uses action queue to queue movement actions (ActionMoveToLocation)
        /// - Based on reverse engineering of daorigins.exe idle behavior patterns
        /// - Note: Eclipse does not use patrol routes in base implementation (unlike Odyssey/Aurora)
        ///   - Patrol routes may be implemented via UnrealScript or game-specific systems
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
                    LastIdleAnimationTime = 0f
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

            // Handle random wandering behavior
            HandleRandomWanderBehavior(creature, idleState, actionQueue, deltaTime);

            // Handle look-around behavior
            HandleLookAroundBehavior(creature, idleState, actionQueue, deltaTime);

            // Handle idle animations
            HandleIdleAnimations(creature, idleState, deltaTime);
        }

        /// <summary>
        /// Handles random wandering behavior for a creature.
        /// Based on daorigins.exe: Random movement via ActionMoveToLocation
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

            // Project destination to navigation mesh if available (Eclipse uses navigation mesh for pathfinding)
            if (_world.CurrentArea != null && _world.CurrentArea.NavigationMesh != null)
            {
                Vector3? projected = _world.CurrentArea.NavigationMesh.ProjectPoint(destination);
                if (projected.HasValue)
                {
                    destination = projected.Value;
                }
            }

            // Queue movement action (Eclipse uses ActionMoveToLocation, not ActionRandomWalk)
            // Based on daorigins.exe: Movement actions use ActionMoveToLocation
            var moveAction = new ActionMoveToLocation(destination, false);
            actionQueue.Add(moveAction);

            idleState.LastWanderTime = 0f; // Reset timer
        }

        /// <summary>
        /// Handles look-around behavior to make NPCs appear more alive.
        /// Based on daorigins.exe: Idle behavior patterns for NPCs
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
            float lookDistance = 2.5f; // Shorter look distance for Eclipse
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
        /// Based on daorigins.exe: Animation system with idle animations
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

            // Play idle animation (animation ID 0 is typically idle/stand in Eclipse)
            // Based on daorigins.exe: Animation ID 0 is typically idle/stand animation
            // Original implementation: Plays idle animation when no other animation is active
            if (animation.CurrentAnimation == -1 || animation.AnimationComplete)
            {
                animation.PlayAnimation(0, 1.0f, true); // Play idle animation, looping
            }

            idleState.LastIdleAnimationTime = 0f; // Reset timer
        }

        /// <summary>
        /// Gets the spawn position for a creature (used as wander center).
        /// Based on daorigins.exe: Creature spawn position tracking
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
        /// Checks if creature is in conversation (Eclipse-specific: uses UnrealScript conversation system).
        /// Based on daorigins.exe: Conversation state checking
        /// </summary>
        protected override bool IsInConversation(IEntity creature)
        {
            if (creature == null || !creature.IsValid)
            {
                return false;
            }

            // Check if creature has conversation state data
            // Based on daorigins.exe: Conversation state tracking
            // Eclipse uses UnrealScript conversation system with state tracking
            // Conversation state stored in entity data or area conversation state
            if (creature is Entity concreteEntity)
            {
                bool inConversation = concreteEntity.GetData<bool>("InConversation", false);
                if (inConversation)
                {
                    return true;
                }
            }

            return false;
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
        }
    }
}

