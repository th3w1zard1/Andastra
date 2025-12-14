using System;
using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Odyssey.Kotor.Systems
{
    /// <summary>
    /// System that fires OnHeartbeat script events at regular intervals.
    /// KOTOR uses a 6-second heartbeat interval.
    /// </summary>
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
            foreach (var kvp in _timers)
            {
                var entity = kvp.Key;
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
            foreach (var entity in entitiesToRemove)
            {
                _timers.Remove(entity);
            }

            // Fire heartbeats (limited per frame)
            int fired = 0;
            foreach (var entity in heartbeatsToFire)
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
            var scriptHooks = entity.GetComponent<IScriptHooksComponent>();
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
            foreach (var entity in _world.GetEntitiesOfType(ObjectType.Creature) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            // Register placeables
            foreach (var entity in _world.GetEntitiesOfType(ObjectType.Placeable) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            // Register doors
            foreach (var entity in _world.GetEntitiesOfType(ObjectType.Door) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            // Register triggers
            foreach (var entity in _world.GetEntitiesOfType(ObjectType.Trigger) ?? Array.Empty<IEntity>())
            {
                RegisterEntity(entity);
            }

            Console.WriteLine("[HeartbeatSystem] Registered " + _timers.Count + " entities");
        }

        private bool IsEntityValid(IEntity entity)
        {
            // Check if entity is still in the world
            var found = _world.GetEntity(entity.ObjectId);
            return found != null;
        }

        private void FireHeartbeat(IEntity entity)
        {
            if (_fireScript == null)
            {
                return;
            }

            var scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks == null || string.IsNullOrEmpty(scriptHooks.GetScript(ScriptEvent.OnHeartbeat)))
            {
                return;
            }

            // Fire the heartbeat script
            _fireScript(entity, ScriptEvent.OnHeartbeat, null);
        }
    }
}

