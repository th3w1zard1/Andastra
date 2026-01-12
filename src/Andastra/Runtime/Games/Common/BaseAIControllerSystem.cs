using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base AI controller system shared across all BioWare engines.
    /// Manages AI behavior for all NPCs in the world.
    /// </summary>
    /// <remarks>
    /// Base AI Controller System:
    /// - Common AI system-level management across all engines
    /// - Handles perception updates, heartbeat scripts, combat detection
    /// - Provides foundation for engine-specific AI behaviors
    ///
    /// Common functionality across engines:
    /// - Combat detection via perception system (sight/hearing of hostile entities)
    /// - Player control detection
    /// - Conversation state checking
    /// - Heartbeat script timing
    /// - Perception update timing
    ///
    /// Engine-specific implementations:
    /// - Odyssey: swkotor.exe/swkotor2.exe specific AI behavior
    /// - Aurora: nwmain.exe specific AI behavior
    /// - Eclipse: daorigins.exe/DragonAge2.exe specific AI behavior
    /// - Infinity: / specific AI behavior
    /// </remarks>
    [PublicAPI]
    public abstract class BaseAIControllerSystem
    {
        protected readonly IWorld _world;
        protected readonly Dictionary<IEntity, float> _heartbeatTimers;
        protected readonly Dictionary<IEntity, float> _perceptionTimers;

        /// <summary>
        /// Heartbeat interval in seconds (common across engines).
        /// </summary>
        protected const float HeartbeatInterval = 6.0f;

        /// <summary>
        /// Perception update interval in seconds (common across engines).
        /// </summary>
        protected const float PerceptionUpdateInterval = 0.5f;

        protected BaseAIControllerSystem([NotNull] IWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _heartbeatTimers = new Dictionary<IEntity, float>();
            _perceptionTimers = new Dictionary<IEntity, float>();
        }

        /// <summary>
        /// Updates AI for all NPCs in the world.
        /// </summary>
        public virtual void Update(float deltaTime)
        {
            if (_world.CurrentArea == null)
            {
                return;
            }

            // Get all creatures in the current area
            var creatures = GetCreaturesInCurrentArea();

            foreach (var entity in creatures)
            {
                UpdateCreatureAI(entity, deltaTime);
            }
        }

        /// <summary>
        /// Gets all creatures in the current area.
        /// Engine-specific implementations may override for different area query methods.
        /// </summary>
        protected virtual IEnumerable<IEntity> GetCreaturesInCurrentArea()
        {
            return _world.GetEntitiesInRadius(
                System.Numerics.Vector3.Zero,
                float.MaxValue,
                ObjectType.Creature);
        }

        /// <summary>
        /// Updates AI for a single creature.
        /// </summary>
        protected virtual void UpdateCreatureAI(IEntity creature, float deltaTime)
        {
            // Skip if creature is invalid or is player-controlled
            if (creature == null || !creature.IsValid)
            {
                return;
            }

            // Check if this is a player character (skip AI)
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
                HandleIdleAI(creature);
            }
        }

        /// <summary>
        /// Checks if creature is player-controlled.
        /// Common across all engines - checks entity tags and flags.
        /// </summary>
        protected virtual bool IsPlayerControlled(IEntity creature)
        {
            if (creature == null)
            {
                return false;
            }

            // Check if creature has a tag indicating it's the player
            string tag = creature.Tag ?? string.Empty;
            return tag.Equals("Player", StringComparison.OrdinalIgnoreCase) ||
                   tag.Equals("PC", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if creature is in conversation.
        /// Engine-specific implementations may override for different dialogue systems.
        /// </summary>
        protected virtual bool IsInConversation(IEntity creature)
        {
            // Default: no conversation check
            // Engine-specific implementations should override this
            return false;
        }

        /// <summary>
        /// Checks if creature is in combat.
        /// Common implementation across all engines using perception system.
        /// </summary>
        /// <remarks>
        /// Combat detection logic:
        /// 1. Check if creature's HP is below max (recently damaged)
        /// 2. Check perception for hostile creatures (seen or heard)
        /// 3. Verify hostile entities are alive
        ///
        /// This is common across all BioWare engines:
        /// - Odyssey: swkotor.exe/swkotor2.exe use perception-based combat detection
        /// - Aurora: nwmain.exe uses similar perception-based detection
        /// - Eclipse: daorigins.exe uses perception and combat state tracking
        /// - Infinity:  uses perception and threat assessment
        /// </remarks>
        protected virtual bool IsInCombat(IEntity creature)
        {
            if (creature == null || !creature.IsValid)
            {
                return false;
            }

            // Check if creature's HP is below max (recently damaged)
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
                // Get faction component to check hostility
                IFactionComponent faction = creature.GetComponent<IFactionComponent>();
                if (faction != null)
                {
                    // Check all seen objects for hostile creatures
                    foreach (IEntity seenEntity in perception.GetSeenObjects())
                    {
                        if (seenEntity == null || !seenEntity.IsValid)
                        {
                            continue;
                        }

                        // Check if this entity is hostile
                        if (faction.IsHostile(seenEntity))
                        {
                            // Verify the hostile entity is alive
                            IStatsComponent seenStats = seenEntity.GetComponent<IStatsComponent>();
                            if (seenStats != null && seenStats.CurrentHP > 0)
                            {
                                // Found a hostile, alive creature that we can see
                                return true;
                            }
                        }
                    }

                    // Check all heard objects for hostile creatures
                    foreach (IEntity heardEntity in perception.GetHeardObjects())
                    {
                        if (heardEntity == null || !heardEntity.IsValid)
                        {
                            continue;
                        }

                        // Check if this entity is hostile
                        if (faction.IsHostile(heardEntity))
                        {
                            // Verify the hostile entity is alive
                            IStatsComponent heardStats = heardEntity.GetComponent<IStatsComponent>();
                            if (heardStats != null && heardStats.CurrentHP > 0)
                            {
                                // Found a hostile, alive creature that we can hear
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Updates heartbeat timer for a creature.
        /// </summary>
        protected virtual void UpdateHeartbeat(IEntity creature, float deltaTime)
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

        /// <summary>
        /// Fires heartbeat script for a creature.
        /// Engine-specific implementations must override to fire script events.
        /// </summary>
        protected abstract void FireHeartbeatScript(IEntity creature);

        /// <summary>
        /// Updates perception timer for a creature.
        /// </summary>
        protected virtual void UpdatePerception(IEntity creature, float deltaTime)
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

        /// <summary>
        /// Checks perception for a creature.
        /// Engine-specific implementations may override for different perception systems.
        /// </summary>
        protected abstract void CheckPerception(IEntity creature);

        /// <summary>
        /// Handles combat AI for a creature.
        /// Engine-specific implementations may override for different combat behaviors.
        /// </summary>
        protected abstract void HandleCombatAI(IEntity creature);

        /// <summary>
        /// Handles idle AI for a creature.
        /// Engine-specific implementations may override for different idle behaviors.
        /// </summary>
        protected virtual void HandleIdleAI(IEntity creature)
        {
            // Default: no idle behavior
            // Engine-specific implementations should override this
        }

        /// <summary>
        /// Cleans up AI state for a destroyed entity.
        /// </summary>
        public virtual void OnEntityDestroyed(IEntity entity)
        {
            if (entity != null)
            {
                _heartbeatTimers.Remove(entity);
                _perceptionTimers.Remove(entity);
            }
        }
    }
}

