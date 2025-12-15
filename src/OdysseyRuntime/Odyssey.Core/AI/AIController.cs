using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Odyssey.Core.Actions;
using Odyssey.Core.Combat;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.AI
{
    /// <summary>
    /// AI controller for NPCs.
    /// </summary>
    /// <remarks>
    /// AI Controller:
    /// - Based on swkotor2.exe AI system
    /// - Located via string references: "OnHeartbeat" @ 0x007beeb0 (heartbeat script field), "Heartbeat" @ 0x007c1a90
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT" @ 0x007bc9a4 (heartbeat script event type, 0x0)
    /// - "HeartbeatInterval" @ 0x007c38e8 (heartbeat timing interval field)
    /// - "AIState" @ 0x007c4090 (AI state field), "AISTATE" @ 0x007c81f8 (AI state constant)
    /// - Debug: "    AI Level: " @ 0x007cb174 (AI debug output)
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 saves heartbeat script and interval
    /// - Heartbeat timing: Default 6.0 seconds between heartbeat script executions (HeartbeatInterval field)
    /// - AI behavior: NPCs process action queues, execute heartbeat scripts, and respond to combat
    /// - Combat AI: NPCs attack nearest enemy when in combat (default combat behavior)
    /// - Perception AI: NPCs respond to perception events (OnPerception, OnNotice) from PerceptionSystem
    /// - Action queue: NPCs populate action queues based on AI state (combat, idle, following)
    /// - Default behavior: If no actions queued, NPCs idle or follow party leader
    /// - Pathfinding failure: "k_def_pathfail01" @ 0x007c52fc (default pathfinding failure script)
    /// </remarks>
    public class AIController
    {
        private const float DefaultHeartbeatInterval = 6.0f; // seconds

        private readonly IWorld _world;
        private readonly CombatSystem _combatSystem;
        private readonly Dictionary<IEntity, AIState> _aiStates;

        public AIController(IWorld world, CombatSystem combatSystem)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _combatSystem = combatSystem ?? throw new ArgumentNullException("combatSystem");
            _aiStates = new Dictionary<IEntity, AIState>();
        }

        /// <summary>
        /// Updates AI for all NPCs.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        public void Update(float deltaTime)
        {
            foreach (IEntity entity in _world.GetAllEntities())
            {
                // Only process NPCs (not player-controlled)
                if (IsPlayerControlled(entity))
                {
                    continue;
                }

                UpdateAI(entity, deltaTime);
            }
        }

        /// <summary>
        /// Updates AI for a single entity.
        /// </summary>
        private void UpdateAI(IEntity creature, float deltaTime)
        {
            // Skip if in conversation
            if (IsInConversation(creature))
            {
                return;
            }

            // Get or create AI state
            if (!_aiStates.TryGetValue(creature, out AIState state))
            {
                state = new AIState();
                _aiStates[creature] = state;
            }

            // Process action queue first
            IActionQueueComponent actionQueue = creature.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Update(creature, deltaTime);
                if (actionQueue.CurrentAction != null)
                {
                    return; // Still processing action
                }
            }

            // Update heartbeat timer
            state.TimeSinceHeartbeat += deltaTime;

            // Execute heartbeat script
            if (state.TimeSinceHeartbeat >= DefaultHeartbeatInterval)
            {
                state.TimeSinceHeartbeat = 0f;
                ExecuteHeartbeatScript(creature);
            }

            // Default combat behavior
            if (_combatSystem.IsInCombat(creature))
            {
                IEntity nearestEnemy = FindNearestEnemy(creature);
                if (nearestEnemy != null && actionQueue != null)
                {
                    actionQueue.Add(new ActionAttack(nearestEnemy.ObjectId));
                }
            }
        }

        /// <summary>
        /// Checks if entity is player-controlled.
        /// </summary>
        private bool IsPlayerControlled(IEntity entity)
        {
            // TODO: Check if entity is PC or party member under player control
            // For now, assume all creatures are NPCs unless marked otherwise
            return false;
        }

        /// <summary>
        /// Checks if entity is in conversation.
        /// </summary>
        private bool IsInConversation(IEntity entity)
        {
            // TODO: Check if entity is in active dialogue
            return false;
        }

        /// <summary>
        /// Executes heartbeat script for entity.
        /// </summary>
        private void ExecuteHeartbeatScript(IEntity entity)
        {
            string script = entity.GetScript(ScriptEvent.OnHeartbeat);
            if (!string.IsNullOrEmpty(script))
            {
                // TODO: Execute script with entity as caller
                // ScriptExecutor.ExecuteScript(script, entity)
            }
        }

        /// <summary>
        /// Finds nearest enemy for entity.
        /// </summary>
        private IEntity FindNearestEnemy(IEntity creature)
        {
            IFactionComponent faction = creature.GetComponent<IFactionComponent>();
            if (faction == null)
            {
                return null;
            }

            IEntity nearest = null;
            float nearestDist = float.MaxValue;

            // Get all creatures in perception range
            IPerceptionComponent perception = creature.GetComponent<IPerceptionComponent>();
            float range = perception != null ? perception.SightRange : 20.0f;

            IEnumerable<IEntity> nearbyEntities = _world.GetEntitiesInRadius(creature.Position, range, ObjectType.Creature);

            foreach (IEntity other in nearbyEntities)
            {
                if (other == creature || !other.IsValid)
                {
                    continue;
                }

                // Check if hostile
                if (!faction.IsHostile(other))
                {
                    continue;
                }

                // Check if alive
                IStatsComponent stats = other.GetComponent<IStatsComponent>();
                if (stats != null && stats.CurrentHP <= 0)
                {
                    continue;
                }

                // Check distance
                float dist = Vector3.Distance(creature.Position, other.Position);
                if (dist < nearestDist)
                {
                    nearest = other;
                    nearestDist = dist;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Clears AI state for an entity (when destroyed).
        /// </summary>
        public void ClearAIState(IEntity entity)
        {
            _aiStates.Remove(entity);
        }
    }

    /// <summary>
    /// AI state for an entity.
    /// </summary>
    internal class AIState
    {
        public float TimeSinceHeartbeat { get; set; }
    }
}

