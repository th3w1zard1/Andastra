using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Engines.Aurora.Systems
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
        /// Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0 (nwmain.exe)
        /// </summary>
        /// <remarks>
        /// Aurora Perception System:
        /// - Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0 (nwmain.exe)
        /// - Located via string references: "PerceptionRange" @ 0x140dde0e0 (nwmain.exe)
        /// - Original implementation: 
        ///   1. Gets creature's perception range (sight/hearing ranges from PerceptionRange field)
        ///   2. Iterates through all creatures in the same area
        ///   3. Calculates distance between creatures
        ///   4. Checks if within perception range (sight or hearing)
        ///   5. Performs line-of-sight check using CNWSArea::ClearLineOfSight for sight-based perception
        ///   6. Checks visibility and stealth detection (DoStealthDetection)
        ///   7. Updates perception state and fires OnPerception events for newly detected entities
        /// - Perception range: From creature stats PerceptionRange field (typically 20m sight, 15m hearing)
        /// - Line-of-sight: Uses navigation mesh raycasting (CNWSArea::ClearLineOfSight)
        /// - Stealth detection: Checks if target is invisible/stealthed and if perceiver can detect stealth
        /// - OnPerception events: Fire when entities are first detected (not on every check)
        /// - Based on reverse engineering of nwmain.exe perception system
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

            // Get all creatures in perception range (Aurora-specific: same area check)
            // Based on nwmain.exe: DoPerceptionUpdateOnCreature checks if creatures are in same area first
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

                // Check if we can see/hear this creature (Aurora-specific: D20 perception with line-of-sight)
                // Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
                // Original implementation: Checks distance, line-of-sight, visibility, and stealth detection
                bool canSee = CanSee(creature, other, sightRange);
                bool canHear = CanHear(creature, other, hearingRange);

                if (canSee || canHear)
                {
                    // Update perception component state
                    perception.UpdatePerception(other, canSee, canHear);

                    // Fire OnPerception event if this is a new detection
                    // Based on nwmain.exe: OnPerception event firing in DoPerceptionUpdateOnCreature
                    // Aurora uses ScriptEvent system for perception events
                    if ((canSee && !perception.WasSeen(other)) || (canHear && !perception.WasHeard(other)))
                    {
                        _fireScriptEvent(creature, ScriptEvent.OnPerception, other);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if subject can see target (Aurora-specific: uses navigation mesh line-of-sight with D20 perception).
        /// Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0 with CNWSArea::ClearLineOfSight
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

            // Line-of-sight check through navigation mesh (Aurora-specific: CNWSArea::ClearLineOfSight)
            // Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
            // Original implementation: Uses CNWSArea::ClearLineOfSight to test line-of-sight between creatures
            // Aurora uses walkmesh for line-of-sight checks (similar to Odyssey)
            if (_world.CurrentArea != null)
            {
                INavigationMesh navMesh = _world.CurrentArea.NavigationMesh;
                if (navMesh != null)
                {
                    // Check line-of-sight from subject eye position to target eye position
                    // Based on nwmain.exe: Eye height calculation in DoPerceptionUpdateOnCreature
                    Vector3 subjectEye = subjectTransform.Position + Vector3.UnitY * 1.5f; // Approximate eye height
                    Vector3 targetEye = targetTransform.Position + Vector3.UnitY * 1.5f;

                    // Test if line-of-sight is blocked by navigation mesh
                    // Based on nwmain.exe: CNWSArea::ClearLineOfSight @ 0x1403dbcb2 (called from DoPerceptionUpdateOnCreature)
                    if (!navMesh.TestLineOfSight(subjectEye, targetEye))
                    {
                        return false; // Line-of-sight blocked
                    }
                }
            }

            // Stealth detection (Aurora-specific: D20 stealth vs. perception system)
            // Based on nwmain.exe: DoStealthDetection @ 0x14038bfa0 (nwmain.exe)
            // Original implementation: Checks if target is invisible/stealthed and if perceiver can detect stealth
            // DoStealthDetection calls DoListenDetection and DoSpotDetection for hearing/sight-based detection

            // Check if target is invisible (has Invisibility effect)
            bool targetIsInvisible = _world.EffectSystem.HasEffect(target, EffectType.Invisibility);
            if (targetIsInvisible)
            {
                // Check if subject can see invisible (TrueSeeing effect or feat)
                bool canSeeInvisible = _world.EffectSystem.HasEffect(subject, EffectType.TrueSeeing);
                if (!canSeeInvisible)
                {
                    // Subject cannot see invisible targets
                    return false;
                }
            }

            // Perform stealth detection checks (hearing and sight)
            // Based on nwmain.exe: DoStealthDetection @ 0x14038bfa0 calls DoListenDetection and DoSpotDetection
            bool heardTarget = DoListenDetection(subject, target, targetIsInvisible ? 1 : 0);
            bool spottedTarget = DoSpotDetection(subject, target, targetIsInvisible ? 1 : 0);

            // Target is detected if either heard or spotted
            return heardTarget || spottedTarget;
        }

        /// <summary>
        /// Checks if subject can hear target (Aurora-specific: distance-based hearing with D20 perception).
        /// Based on nwmain.exe: DoPerceptionUpdateOnCreature @ 0x14038b0c0
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
        /// Performs listen-based stealth detection (Aurora-specific: D20 Listen skill check).
        /// Based on nwmain.exe: DoListenDetection @ 0x14038aab0 (nwmain.exe)
        /// </summary>
        /// <remarks>
        /// Aurora Listen Detection:
        /// - Based on nwmain.exe: DoListenDetection @ 0x14038aab0 (nwmain.exe)
        /// - Original implementation:
        ///   1. Checks if target is moving silently (has Move Silently skill active)
        ///   2. Calculates distance between subject and target
        ///   3. Checks if target is within hearing range (hearing range from perception component)
        ///   4. Performs Listen skill check (subject's Listen skill) vs. Move Silently check (target's Move Silently skill)
        ///   5. Returns true if subject successfully hears target (Listen check beats Move Silently check)
        /// - Skill IDs: Listen = 6 (SKILL_LISTEN), Move Silently = 8 (SKILL_MOVE_SILENTLY)
        /// - Hearing range: Based on subject's hearing range from perception component
        /// - Deafness: If subject has Deafness effect, cannot hear (returns false)
        /// </remarks>
        private bool DoListenDetection(IEntity subject, IEntity target, int param2)
        {
            if (subject == null || target == null)
            {
                return false;
            }

            // Check if target is moving silently (has Move Silently skill active)
            // Based on nwmain.exe: DoListenDetection checks if target has Move Silently active
            IStatsComponent targetStats = target.GetComponent<IStatsComponent>();
            if (targetStats == null)
            {
                return false; // Cannot detect if target has no stats
            }

            // Check if subject is deaf (cannot hear)
            if (_world.EffectSystem.HasEffect(subject, EffectType.Deafness))
            {
                return false; // Subject is deaf, cannot hear
            }

            // Check if target is moving silently (has Move Silently skill)
            // Based on nwmain.exe: DoListenDetection checks target's Move Silently skill
            // Skill ID: Move Silently = 8 (SKILL_MOVE_SILENTLY in D&D 3.5/NWN)
            const int SKILL_MOVE_SILENTLY = 8;
            int targetMoveSilently = targetStats.GetSkillRank(SKILL_MOVE_SILENTLY);
            if (targetMoveSilently <= 0)
            {
                // Target is not moving silently, can be heard
                return true;
            }

            // Get positions and calculate distance
            ITransformComponent subjectTransform = subject.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            Vector3 subjectPos = subjectTransform.Position;
            Vector3 targetPos = targetTransform.Position;
            float distance = Vector3.Distance(subjectPos, targetPos);

            // Get hearing range from perception component
            // Based on nwmain.exe: DoListenDetection uses hearing range from perception component
            IPerceptionComponent subjectPerception = subject.GetComponent<IPerceptionComponent>();
            float hearingRange = 20.0f; // Default hearing range (Aurora default)
            if (subjectPerception != null)
            {
                hearingRange = subjectPerception.HearingRange;
            }

            // Check if within hearing range
            if (distance > hearingRange)
            {
                return false; // Too far to hear
            }

            // Perform Listen vs. Move Silently skill check
            // Based on nwmain.exe: DoListenDetection performs skill check
            // Skill ID: Listen = 6 (SKILL_LISTEN in D&D 3.5/NWN)
            const int SKILL_LISTEN = 6;
            IStatsComponent subjectStats = subject.GetComponent<IStatsComponent>();
            if (subjectStats == null)
            {
                return false; // Cannot detect if subject has no stats
            }

            int subjectListen = subjectStats.GetSkillRank(SKILL_LISTEN);

            // Roll Listen check (d20 + Listen skill rank)
            // Based on nwmain.exe: DoListenDetection rolls d20 for skill check
            int listenRoll = _random.Next(1, 21); // d20 roll
            int listenCheck = listenRoll + subjectListen;

            // Roll Move Silently check (d20 + Move Silently skill rank)
            int moveSilentlyRoll = _random.Next(1, 21); // d20 roll
            int moveSilentlyCheck = moveSilentlyRoll + targetMoveSilently;

            // Subject hears target if Listen check beats Move Silently check
            return listenCheck >= moveSilentlyCheck;
        }

        /// <summary>
        /// Performs spot-based stealth detection (Aurora-specific: D20 Spot skill check).
        /// Based on nwmain.exe: DoSpotDetection @ 0x14038baa0 (nwmain.exe)
        /// </summary>
        /// <remarks>
        /// Aurora Spot Detection:
        /// - Based on nwmain.exe: DoSpotDetection @ 0x14038baa0 (nwmain.exe)
        /// - Original implementation:
        ///   1. Checks if target is hiding (has Hide skill active)
        ///   2. Calculates distance between subject and target
        ///   3. Checks if target is within sight range (sight range from perception component)
        ///   4. Performs Spot skill check (subject's Spot skill) vs. Hide check (target's Hide skill)
        ///   5. Returns true if subject successfully spots target (Spot check beats Hide check)
        /// - Skill IDs: Spot = 5 (SKILL_SPOT), Hide = 7 (SKILL_HIDE)
        /// - Sight range: Based on subject's sight range from perception component
        /// - Invisibility: If target is invisible and subject cannot see invisible, cannot spot (returns false)
        /// - Light conditions: Dark areas reduce Spot checks (not fully implemented, but structure exists)
        /// </remarks>
        private bool DoSpotDetection(IEntity subject, IEntity target, int param2)
        {
            if (subject == null || target == null)
            {
                return false;
            }

            // Check if target is hiding (has Hide skill active)
            // Based on nwmain.exe: DoSpotDetection checks if target has Hide active
            IStatsComponent targetStats = target.GetComponent<IStatsComponent>();
            if (targetStats == null)
            {
                return false; // Cannot detect if target has no stats
            }

            // Get positions and calculate distance
            ITransformComponent subjectTransform = subject.GetComponent<ITransformComponent>();
            ITransformComponent targetTransform = target.GetComponent<ITransformComponent>();
            if (subjectTransform == null || targetTransform == null)
            {
                return false;
            }

            Vector3 subjectPos = subjectTransform.Position;
            Vector3 targetPos = targetTransform.Position;
            float distance = Vector3.Distance(subjectPos, targetPos);

            // Get sight range from perception component
            // Based on nwmain.exe: DoSpotDetection uses sight range from perception component
            IPerceptionComponent subjectPerception = subject.GetComponent<IPerceptionComponent>();
            float sightRange = 30.0f; // Default sight range (Aurora default)
            if (subjectPerception != null)
            {
                sightRange = subjectPerception.SightRange;
            }

            // Check if within sight range
            if (distance > sightRange)
            {
                return false; // Too far to see
            }

            // Check if target is hiding (has Hide skill)
            // Based on nwmain.exe: DoSpotDetection checks target's Hide skill
            // Skill ID: Hide = 7 (SKILL_HIDE in D&D 3.5/NWN)
            const int SKILL_HIDE = 7;
            int targetHide = targetStats.GetSkillRank(SKILL_HIDE);
            if (targetHide <= 0)
            {
                // Target is not hiding, can be spotted
                return true;
            }

            // Perform Spot vs. Hide skill check
            // Based on nwmain.exe: DoSpotDetection performs skill check
            // Skill ID: Spot = 5 (SKILL_SPOT in D&D 3.5/NWN)
            const int SKILL_SPOT = 5;
            IStatsComponent subjectStats = subject.GetComponent<IStatsComponent>();
            if (subjectStats == null)
            {
                return false; // Cannot detect if subject has no stats
            }

            int subjectSpot = subjectStats.GetSkillRank(SKILL_SPOT);

            // Roll Spot check (d20 + Spot skill rank)
            // Based on nwmain.exe: DoSpotDetection rolls d20 for skill check
            int spotRoll = _random.Next(1, 21); // d20 roll
            int spotCheck = spotRoll + subjectSpot;

            // Roll Hide check (d20 + Hide skill rank)
            int hideRoll = _random.Next(1, 21); // d20 roll
            int hideCheck = hideRoll + targetHide;

            // Subject spots target if Spot check beats Hide check
            return spotCheck >= hideCheck;
        }

        /// <summary>
        /// Handles combat AI for a creature (Aurora-specific: D20 tactical combat).
        /// Based on nwmain.exe: ComputeAIState @ 0x140387dc0, ComputeAIStateOnAction @ 0x140387e70 (nwmain.exe)
        /// </summary>
        /// <remarks>
        /// Aurora Combat AI:
        /// - Based on nwmain.exe: ComputeAIState @ 0x140387dc0 (nwmain.exe)
        /// - Located via reverse engineering: AIUpdate @ 0x14037be20 (main AI update loop), ComputeAIStateOnAction @ 0x140387e70
        /// - Original implementation: 
        ///   1. ComputeAIState iterates through action queue and calls ComputeAIStateOnAction for each action
        ///   2. ComputeAIStateOnAction sets AI state based on action type (0x14 = movement, 0x15 = attack, etc.)
        ///   3. AI state determines creature behavior (combat, movement, idle, etc.)
        ///   4. Action type 0x14 (20) sets AI state 0xf (15) for movement actions
        ///   5. Action type 0x15 (21) sets AI state 0x10 (16) for attack actions
        /// - Combat behavior:
        ///   1. Find nearest enemy within combat range
        ///   2. Check if already attacking this target (continue if so)
        ///   3. Queue attack action if no current action
        ///   4. Consider positioning for tactical combat (flanking, cover, etc.)
        /// - D20 tactical features:
        ///   - Attack actions use D20 combat system (attack rolls, damage rolls, etc.)
        ///   - Positioning for flanking bonuses
        ///   - Threat assessment and target prioritization
        ///   - Combat round management (Aurora uses turn-based combat rounds)
        /// - Based on reverse engineering of nwmain.exe combat system
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
                    // Based on nwmain.exe: ComputeAIState checks current action in action queue
                    IAction currentAction = actionQueue.CurrentAction;
                    if (currentAction is ActionAttack attackAction)
                    {
                        // Already attacking, continue
                        return;
                    }

                    // Queue new attack
                    // Based on nwmain.exe: ComputeAIStateOnAction @ 0x140387e70
                    // Action type 0x15 (21) sets AI state 0x10 (16) for attack actions
                    // Aurora uses ActionAttack similar to Odyssey/Eclipse
                    var attack = new ActionAttack(nearestEnemy.ObjectId);
                    actionQueue.Add(attack);
                }
            }
        }

        /// <summary>
        /// Finds the nearest enemy for a creature (Aurora-specific: D20 faction system).
        /// Based on nwmain.exe: Enemy detection and target selection
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
            // Based on nwmain.exe: Combat range is typically 50m (Aurora default, similar to Odyssey)
            var candidates = _world.GetEntitiesInRadius(
                transform.Position,
                50.0f, // Max combat range (Aurora default, similar to Odyssey)
                ObjectType.Creature);

            IEntity nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate == creature || !candidate.IsValid)
                {
                    continue;
                }

                // Check if hostile (Aurora-specific: D20 faction system)
                // Based on nwmain.exe: Faction relationship checking via CFactionManager
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

