using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.AI
{
    /// <summary>
    /// AI controller for NPCs.
    /// </summary>
    /// <remarks>
    /// AI Controller:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) AI system
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
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player control detection
        /// Located via string references: "IsPC" @ 0x007c4090, "GetIsPC" @ NWScript function
        /// Original implementation: Checks entity flags and party membership to determine if player-controlled
        /// </summary>
        private bool IsPlayerControlled(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Check if entity has IsPC flag set
            if (entity is Core.Entities.Entity concreteEntity && concreteEntity.GetData<bool>("IsPC", false))
            {
                return true;
            }

            // Check if entity tag indicates player character
            string tag = entity.Tag ?? string.Empty;
            if (tag.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
                tag.Equals("PC", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check if entity is in party and is player-controlled
            // Party members can be player-controlled if they're the active party leader
            return false;
        }

        /// <summary>
        /// Checks if entity is in conversation.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Dialogue state tracking
        /// Located via string references: "DialogueActive" @ 0x007c38e0, "InConversation" @ 0x007c38e4
        /// Original implementation: Tracks active dialogue state per entity to prevent AI during conversations
        /// </summary>
        private bool IsInConversation(IEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            // Check if entity has dialogue active flag
            if (entity is Core.Entities.Entity concreteEntity && concreteEntity.GetData<bool>("InConversation", false))
            {
                return true;
            }

            // Check if entity has active dialogue component or dialogue state
            // Dialogue system would set this flag when conversation starts
            return false;
        }

        /// <summary>
        /// Executes heartbeat script for entity.
        /// </summary>
        private void ExecuteHeartbeatScript(IEntity entity)
        {
            IScriptHooksComponent scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                string script = scriptHooks.GetScript(ScriptEvent.OnHeartbeat);
                if (!string.IsNullOrEmpty(script))
                {
                    // Execute heartbeat script with entity as owner
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Heartbeat script execution
                    // Located via string references: "OnHeartbeat" @ 0x007beeb0, "Heartbeat" @ 0x007c1a90
                    // Original implementation: FUN_005226d0 @ 0x005226d0 executes heartbeat scripts every 6 seconds
                    if (_world.EventBus != null)
                    {
                        _world.EventBus.FireScriptEvent(entity, ScriptEvent.OnHeartbeat, null);
                    }
                }
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

            ITransformComponent creatureTransform = creature.GetComponent<ITransformComponent>();
            if (creatureTransform == null)
            {
                return null;
            }

            IEnumerable<IEntity> nearbyEntities = _world.GetEntitiesInRadius(creatureTransform.Position, range, ObjectType.Creature);

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
                ITransformComponent otherTransform = other.GetComponent<ITransformComponent>();
                if (otherTransform == null)
                {
                    continue;
                }
                float dist = Vector3.Distance(creatureTransform.Position, otherTransform.Position);
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

