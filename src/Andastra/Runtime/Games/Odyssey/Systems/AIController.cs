using System;
using System.Collections.Generic;
using System.Numerics;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common;

namespace Andastra.Runtime.Engines.Odyssey.Systems
{
    /// <summary>
    /// AI controller system for NPCs in Odyssey engine (KOTOR, KOTOR 2, Jade Empire).
    /// Handles perception, combat behavior, and action queue management for non-player creatures.
    /// </summary>
    /// <remarks>
    /// Odyssey AI Controller System:
    /// - Based on swkotor.exe/swkotor2.exe AI system
    /// - Located via string references: "OnHeartbeat" @ 0x007c1f60 (swkotor2.exe), "OnPerception" @ 0x007c1f64 (swkotor2.exe)
    /// - "OnCombatRoundEnd" @ 0x007c1f68 (swkotor2.exe), "OnDamaged" @ 0x007c1f6c (swkotor2.exe), "OnDeath" @ 0x007c1f70 (swkotor2.exe)
    /// - AI state: "PT_AISTATE" @ 0x007c1768 (party AI state in PARTYTABLE, swkotor2.exe), "AISTATE" @ 0x007c81f8 (swkotor2.exe), "AIState" @ 0x007c4090 (swkotor2.exe)
    /// - AI scripts: "aiscripts" @ 0x007c4fd0 (AI script directory/resource, swkotor2.exe)
    /// - Pathfinding errors:
    ///   - "?The Path find has Failed... Why?" @ 0x007c055f (swkotor2.exe)
    ///   - "Bailed the desired position is unsafe." @ 0x007c0584 (swkotor2.exe)
    ///   - "    failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510 (swkotor2.exe)
    ///   - "    failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8 (swkotor2.exe)
    /// - Script hooks: "k_def_pathfail01" @ 0x007c52fc (pathfinding failure script example, swkotor2.exe)
    /// - Debug: "    AI Level: " @ 0x007cb174 (AI level debug display, swkotor2.exe)
    /// - Original implementation: FUN_004eb750 @ 0x004eb750 (creature AI update loop, swkotor2.exe)
    /// - FUN_005226d0 @ 0x005226d0 (process heartbeat scripts, swkotor2.exe), FUN_004dfbb0 @ 0x004dfbb0 (perception checks, swkotor2.exe)
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
    public class AIController : BaseAIControllerSystem
    {
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScriptEvent;

        public AIController([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScriptEvent)
            : base(world)
        {
            _fireScriptEvent = fireScriptEvent ?? throw new ArgumentNullException(nameof(fireScriptEvent));
        }

        /// <summary>
        /// Fires heartbeat script for a creature (Odyssey-specific: uses ScriptEvent system).
        /// Based on swkotor2.exe: FUN_005226d0 @ 0x005226d0 (process heartbeat scripts)
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
        /// Based on swkotor2.exe: FUN_004dfbb0 @ 0x004dfbb0 (perception checks)
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
        /// Based on swkotor2.exe perception system with walkmesh raycast.
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
            // Based on swkotor2.exe perception system
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
        /// Based on swkotor2.exe perception system.
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
        /// Based on swkotor2.exe: Default combat AI engages nearest enemy.
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
        /// Handles idle AI for a creature (Odyssey-specific: no idle behavior yet).
        /// TODO: SIMPLIFIED - For now, creatures just stand still when not in combat
        /// </summary>
        protected override void HandleIdleAI(IEntity creature)
        {
            // Idle behavior (could be random walk, patrol, etc.)
            // TODO: SIMPLIFIED - For now, creatures just stand still when not in combat
        }
    }
}

