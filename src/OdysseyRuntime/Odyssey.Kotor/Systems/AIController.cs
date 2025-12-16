using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using BioWareEngines.Core.Actions;
using BioWareEngines.Core.Combat;
using BioWareEngines.Core.Enums;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Interfaces.Components;

namespace BioWareEngines.Kotor.Systems
{
    /// <summary>
    /// AI controller system for NPCs.
    /// Handles perception, combat behavior, and action queue management for non-player creatures.
    /// </summary>
    /// <remarks>
    /// AI Controller System:
    /// - Based on swkotor2.exe AI system
    /// - Located via string references: "OnHeartbeat" @ 0x007c1f60, "OnPerception" @ 0x007c1f64
    /// - "OnCombatRoundEnd" @ 0x007c1f68, "OnDamaged" @ 0x007c1f6c, "OnDeath" @ 0x007c1f70
    /// - AI state: "PT_AISTATE" @ 0x007c1768 (party AI state in PARTYTABLE), "AISTATE" @ 0x007c81f8, "AIState" @ 0x007c4090
    /// - AI scripts: "aiscripts" @ 0x007c4fd0 (AI script directory/resource)
    /// - Pathfinding errors:
    ///   - "?The Path find has Failed... Why?" @ 0x007c055f
    ///   - "Bailed the desired position is unsafe." @ 0x007c0584
    ///   - "    failed to grid based pathfind from the creatures position to the starting path point." @ 0x007be510
    ///   - "    failed to grid based pathfind from the ending path point ot the destiantion." @ 0x007be4b8
    /// - Script hooks: "k_def_pathfail01" @ 0x007c52fc (pathfinding failure script example)
    /// - Debug: "    AI Level: " @ 0x007cb174 (AI level debug display)
    /// - Original implementation: FUN_004eb750 @ 0x004eb750 (creature AI update loop)
    /// - FUN_005226d0 @ 0x005226d0 (process heartbeat scripts), FUN_004dfbb0 @ 0x004dfbb0 (perception checks)
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
    public class AIController
    {
        private readonly IWorld _world;
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScriptEvent;
        private readonly Dictionary<IEntity, float> _heartbeatTimers;
        private const float HeartbeatInterval = 6.0f; // 6 seconds between heartbeats
        private const float PerceptionUpdateInterval = 0.5f; // Check perception every 0.5 seconds
        private readonly Dictionary<IEntity, float> _perceptionTimers;

        public AIController([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScriptEvent)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _fireScriptEvent = fireScriptEvent ?? throw new ArgumentNullException("fireScriptEvent");
            _heartbeatTimers = new Dictionary<IEntity, float>();
            _perceptionTimers = new Dictionary<IEntity, float>();
        }

        /// <summary>
        /// Updates AI for all NPCs in the world.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (_world.CurrentArea == null)
            {
                return;
            }

            // Get all creatures in the current area
            var creatures = _world.GetEntitiesInRadius(
                Vector3.Zero, 
                float.MaxValue, 
                ObjectType.Creature);

            foreach (var entity in creatures)
            {
                UpdateCreatureAI(entity, deltaTime);
            }
        }

        private void UpdateCreatureAI(IEntity creature, float deltaTime)
        {
            // Skip if creature is invalid or is player-controlled
            if (creature == null || !creature.IsValid)
            {
                return;
            }

            // Check if this is a player character (skip AI)
            // In KOTOR, player characters are controlled by player input
            // We can check this via a component or tag
            if (IsPlayerControlled(creature))
            {
                return;
            }

            // Check if creature is in conversation (skip AI during dialogue)
            if (IsInConversation(creature))
            {
                return;
            }

            // Process action queue first
            IActionQueueComponent actionQueue = creature.GetComponent<IActionQueueComponent>();
            if (actionQueue != null && actionQueue.CurrentAction != null)
            {
                // Action queue is processing, let it continue
                return;
            }

            // Update heartbeat timer
            UpdateHeartbeat(creature, deltaTime);

            // Update perception
            UpdatePerception(creature, deltaTime);

            // Default combat behavior
            if (IsInCombat(creature))
            {
                HandleCombatAI(creature);
            }
            else
            {
                // Idle behavior (could be random walk, patrol, etc.)
                // For now, creatures just stand still when not in combat
            }
        }

        private bool IsPlayerControlled(IEntity creature)
        {
            // Check if creature has a tag indicating it's the player
            // In KOTOR, player characters typically have specific tags
            string tag = creature.Tag ?? string.Empty;
            return tag.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
                   tag.Equals("PC", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsInConversation(IEntity creature)
        {
            // Check if creature is currently in a dialogue conversation
            // This would be tracked by the dialogue system
            // For now, return false (dialogue system would set a flag)
            return false;
        }

        private bool IsInCombat(IEntity creature)
        {
            // Check if creature is in combat
            // This could be determined by:
            // 1. Combat system tracking active combatants
            // 2. Creature has enemies in perception list
            // 3. Creature's HP is below max (recently damaged)

            IStatsComponent stats = creature.GetComponent<IStatsComponent>();
            if (stats != null && stats.CurrentHP < stats.MaxHP)
            {
                // Recently damaged, likely in combat
                return true;
            }

            // Check perception for hostile creatures
            IPerceptionComponent perception = creature.GetComponent<IPerceptionComponent>();
            if (perception != null)
            {
                // Perception system would track seen/heard enemies
                // For now, simplified check
            }

            return false;
        }

        private void UpdateHeartbeat(IEntity creature, float deltaTime)
        {
            if (!_heartbeatTimers.ContainsKey(creature))
            {
                _heartbeatTimers[creature] = 0f;
            }

            _heartbeatTimers[creature] += deltaTime;

            if (_heartbeatTimers[creature] >= HeartbeatInterval)
            {
                _heartbeatTimers[creature] = 0f;
                FireHeartbeatScript(creature);
            }
        }

        private void FireHeartbeatScript(IEntity creature)
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

        private void UpdatePerception(IEntity creature, float deltaTime)
        {
            if (!_perceptionTimers.ContainsKey(creature))
            {
                _perceptionTimers[creature] = 0f;
            }

            _perceptionTimers[creature] += deltaTime;

            if (_perceptionTimers[creature] >= PerceptionUpdateInterval)
            {
                _perceptionTimers[creature] = 0f;
                CheckPerception(creature);
            }
        }

        private void CheckPerception(IEntity creature)
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

        private void HandleCombatAI(IEntity creature)
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
        /// Cleans up AI state for a destroyed entity.
        /// </summary>
        public void OnEntityDestroyed(IEntity entity)
        {
            if (entity != null)
            {
                _heartbeatTimers.Remove(entity);
                _perceptionTimers.Remove(entity);
            }
        }
    }
}

