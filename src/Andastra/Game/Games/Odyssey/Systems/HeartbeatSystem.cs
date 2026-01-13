using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Odyssey.Systems
{
    /// <summary>
    /// System that fires OnHeartbeat script events at regular intervals.
    /// KOTOR uses a 6-second heartbeat interval.
    /// </summary>
    /// <remarks>
    /// Heartbeat System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) heartbeat system
    /// - Located via string references: "ScriptHeartbeat" @ 0x007beeb0 (heartbeat script field), "OnHeartbeat" @ 0x007bd720 (heartbeat script event)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT" @ 0x007bcb90 (heartbeat script event type, 0x0)
    /// - "HEARTBEAT" @ 0x007c1348 (heartbeat constant), "HeartbeatTime" @ 0x007c0c30 (heartbeat timer field)
    /// - "HeartbeatDay" @ 0x007c0c40 (heartbeat day field), "Mod_OnHeartbeat" @ 0x007be840 (module heartbeat script)
    /// - "HeartbeatInterval" @ 0x007c38e8 (heartbeat interval field, default 6.0 seconds)
    /// - Original implementation: 0x005226d0 @ 0x005226d0 (save creature data to GFF)
    ///   - Saves ScriptHeartbeat script ResRef field to GFF structure (via 0x004139e0)
    ///   - ScriptHeartbeat stored at offset 0x270 in creature object structure
    ///   - Also saves other script hooks: ScriptOnNotice, ScriptSpellAt, ScriptAttacked, ScriptDamaged, ScriptDisturbed, ScriptEndRound, ScriptDialogue, ScriptSpawn, ScriptRested, ScriptDeath, ScriptUserDefine, ScriptOnBlocked, ScriptEndDialogue
    ///   - Saves creature position (XPosition, YPosition, ZPosition), orientation (XOrientation, YOrientation, ZOrientation)
    ///   - Saves creature stats, inventory, perception list, combat round data, and other creature state
    /// - Fires OnHeartbeat script events every 6 seconds for entities with heartbeat scripts
    /// - Each entity has its own heartbeat timer stored in GFF structure (HeartbeatTime field)
    /// - Timer starts at 0 or random stagger (0-6 seconds) to prevent all heartbeats firing simultaneously
    /// - Module heartbeat script (Mod_OnHeartbeat) fires for area/module-level heartbeat events
    /// - Heartbeat script execution: ScriptExecutor executes ScriptHeartbeat script with entity as caller
    /// - Registration: Entities are registered when they have a ScriptHeartbeat script (checked via IScriptHooksComponent)
    /// - Performance: MaxHeartbeatsPerFrame limits heartbeats processed per frame (default 10) to prevent slowdowns
    /// - Invalid entity cleanup: Removes entities that no longer exist in the world (checks IsValid flag)
    /// </remarks>
    public class HeartbeatSystem
    {
        private readonly IWorld _world;
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScript;
        private readonly Dictionary<IEntity, float> _timers;

        /// <summary>
        /// Heartbeat interval in seconds (KOTOR uses 6 seconds).
        /// </summary>
        public const float HeartbeatInterval = 6.0f;

        /// <summary>
        /// Maximum heartbeats to process per frame to prevent slowdowns.
        /// </summary>
        public int MaxHeartbeatsPerFrame { get; set; } = 10;

        /// <summary>
        /// Creates a new heartbeat system.
        /// </summary>
        /// <param name="world">The game world.</param>
        /// <param name="fireScript">Callback to fire script events.</param>
        public HeartbeatSystem([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScript)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _fireScript = fireScript;
            _timers = new Dictionary<IEntity, float>();
        }

        /// <summary>
        /// Updates the heartbeat system, firing OnHeartbeat scripts as needed.
        /// </summary>
        /// <param name="deltaTime">Time since last update.</param>
        public void Update(float deltaTime)
        {
            var entitiesToRemove = new List<IEntity>();
            var heartbeatsToFire = new List<IEntity>();

            // Update all tracked timers
            foreach (KeyValuePair<IEntity, float> kvp in _timers)
            {
                IEntity entity = kvp.Key;
                float timer = kvp.Value;

                // Check if entity still exists
                if (!IsEntityValid(entity))
                {
                    entitiesToRemove.Add(entity);
                    continue;
                }

                // Update timer
                timer += deltaTime;

                // Check if heartbeat should fire
                if (timer >= HeartbeatInterval)
                {
                    heartbeatsToFire.Add(entity);
                    timer -= HeartbeatInterval;
                }

                _timers[entity] = timer;
            }

            // Remove invalid entities
            foreach (IEntity entity in entitiesToRemove)
            {
                _timers.Remove(entity);
            }

            // Fire heartbeats (limited per frame)
            int fired = 0;
            foreach (IEntity entity in heartbeatsToFire)
            {
                if (fired >= MaxHeartbeatsPerFrame)
                {
                    // Defer remaining heartbeats to next frame
                    break;
                }

                FireHeartbeat(entity);
                fired++;
            }
        }

        /// <summary>
        /// Registers an entity for heartbeat events.
        /// </summary>
        /// <param name="entity">The entity to register.</param>
        /// <param name="startDelay">Initial delay before first heartbeat (0-6 seconds).</param>
        public void RegisterEntity([NotNull] IEntity entity, float startDelay = 0f)
        {
            if (entity == null)
            {
                throw new ArgumentNullException("entity");
            }

            // Only register if entity has a heartbeat script
            IScriptHooksComponent scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks == null || string.IsNullOrEmpty(scriptHooks.GetScript(ScriptEvent.OnHeartbeat)))
            {
                return;
            }

            // Stagger start times to prevent all heartbeats firing at once
            float stagger = startDelay;
            if (stagger <= 0)
            {
                // Random stagger between 0 and heartbeat interval
                stagger = (float)(new Random().NextDouble() * HeartbeatInterval);
            }

            _timers[entity] = stagger;
        }

        /// <summary>
        /// Unregisters an entity from heartbeat events.
        /// </summary>
        public void UnregisterEntity(IEntity entity)
        {
            if (entity != null)
            {
                _timers.Remove(entity);
            }
        }

        /// <summary>
        /// Forces an immediate heartbeat for an entity.
        /// </summary>
        public void ForceHeartbeat(IEntity entity)
        {
            if (entity != null && IsEntityValid(entity))
            {
                FireHeartbeat(entity);
                // Reset timer
                if (_timers.ContainsKey(entity))
                {
                    _timers[entity] = 0;
                }
            }
        }

        /// <summary>
        /// Clears all tracked entities.
        /// </summary>
        public void Clear()
        {
            _timers.Clear();
        }

        /// <summary>
        /// Gets the time until the next heartbeat for an entity.
        /// </summary>
        public float GetTimeUntilHeartbeat(IEntity entity)
        {
            if (entity != null && _timers.TryGetValue(entity, out float timer))
            {
                return HeartbeatInterval - timer;
            }
            return -1;
        }

        /// <summary>
        /// Registers all entities in the world that have heartbeat scripts.
        /// </summary>
        public void RegisterAllEntities()
        {
            // Register creatures
            foreach (IEntity entity in _world.GetEntitiesOfType(ObjectType.Creature) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            // Register placeables
            foreach (IEntity entity in _world.GetEntitiesOfType(ObjectType.Placeable) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            // Register doors
            foreach (IEntity entity in _world.GetEntitiesOfType(ObjectType.Door) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            // Register triggers
            foreach (IEntity entity in _world.GetEntitiesOfType(ObjectType.Trigger) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            Console.WriteLine("[HeartbeatSystem] Registered " + _timers.Count + " entities");
        }

        private bool IsEntityValid(IEntity entity)
        {
            // Check if entity is still in the world
            IEntity found = _world.GetEntity(entity.ObjectId);
            return found != null;
        }

        private void FireHeartbeat(IEntity entity)
        {
            if (_fireScript == null)
            {
                return;
            }

            IScriptHooksComponent scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks == null || string.IsNullOrEmpty(scriptHooks.GetScript(ScriptEvent.OnHeartbeat)))
            {
                return;
            }

            // Fire the heartbeat script
            _fireScript(entity, ScriptEvent.OnHeartbeat, null);
        }
    }
}

