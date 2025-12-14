using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Odyssey.Kotor.Systems
{
    /// <summary>
    /// System for detecting entity entry/exit into trigger volumes.
    /// Fires OnEnter and OnExit script events.
    /// </summary>
    public class TriggerSystem
    {
        private readonly IWorld _world;
        private readonly Dictionary<IEntity, HashSet<IEntity>> _occupants;
        private readonly Action<IEntity, ScriptEvent, IEntity> _fireScript;

        /// <summary>
        /// Creates a new trigger system.
        /// </summary>
        /// <param name="world">The game world.</param>
        /// <param name="fireScript">Callback to fire script events.</param>
        public TriggerSystem([NotNull] IWorld world, Action<IEntity, ScriptEvent, IEntity> fireScript)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _fireScript = fireScript;
            _occupants = new Dictionary<IEntity, HashSet<IEntity>>();
        }

        /// <summary>
        /// Updates the trigger system, checking for entity entry/exit.
        /// </summary>
        public void Update()
        {
            // Get all trigger entities
            var triggers = _world.GetEntitiesOfType(ObjectType.Trigger);
            if (triggers == null)
            {
                return;
            }

            // Get all creatures that can trigger
            var creatures = _world.GetEntitiesOfType(ObjectType.Creature);
            if (creatures == null)
            {
                return;
            }

            foreach (var trigger in triggers)
            {
                UpdateTrigger(trigger, creatures);
            }
        }

        private void UpdateTrigger(IEntity trigger, IEnumerable<IEntity> creatures)
        {
            var triggerComponent = trigger.GetComponent<ITriggerComponent>();
            if (triggerComponent == null)
            {
                return;
            }

            // Get or create occupant set for this trigger
            if (!_occupants.TryGetValue(trigger, out var previousOccupants))
            {
                previousOccupants = new HashSet<IEntity>();
                _occupants[trigger] = previousOccupants;
            }

            var currentOccupants = new HashSet<IEntity>();

            // Check each creature
            foreach (var creature in creatures)
            {
                var transform = creature.GetComponent<ITransformComponent>();
                if (transform == null)
                {
                    continue;
                }

                // Check if creature is inside trigger polygon
                if (IsInsideTrigger(transform.Position, triggerComponent))
                {
                    currentOccupants.Add(creature);
                }
            }

            // Fire OnEnter for new occupants
            foreach (var entity in currentOccupants)
            {
                if (!previousOccupants.Contains(entity))
                {
                    OnEntityEntered(trigger, entity, triggerComponent);
                }
            }

            // Fire OnExit for entities that left
            foreach (var entity in previousOccupants)
            {
                if (!currentOccupants.Contains(entity))
                {
                    OnEntityExited(trigger, entity, triggerComponent);
                }
            }

            // Update occupants
            _occupants[trigger] = currentOccupants;
        }

        private bool IsInsideTrigger(System.Numerics.Vector3 position, ITriggerComponent trigger)
        {
            if (trigger.Geometry.Count < 3)
            {
                return false;
            }

            // Use the trigger's ContainsPoint method
            return trigger.ContainsPoint(position);
        }

        private void OnEntityEntered(IEntity trigger, IEntity entity, ITriggerComponent triggerComponent)
        {
            // Check if this is a one-shot trigger that already fired
            if (triggerComponent.FireOnce && triggerComponent.HasFired)
            {
                return;
            }

            Console.WriteLine("[TriggerSystem] Entity " + entity.Tag + " entered trigger " + trigger.Tag);

            // Fire OnEnter script
            if (_fireScript != null)
            {
                _fireScript(trigger, ScriptEvent.OnEnter, entity);
            }

            // Mark as triggered for one-shot triggers
            if (triggerComponent.FireOnce)
            {
                triggerComponent.HasFired = true;
            }
        }

        private void OnEntityExited(IEntity trigger, IEntity entity, ITriggerComponent triggerComponent)
        {
            Console.WriteLine("[TriggerSystem] Entity " + entity.Tag + " exited trigger " + trigger.Tag);

            // Fire OnExit script
            if (_fireScript != null)
            {
                _fireScript(trigger, ScriptEvent.OnExit, entity);
            }
        }

        /// <summary>
        /// Clears all tracked occupants.
        /// </summary>
        public void Clear()
        {
            _occupants.Clear();
        }

        /// <summary>
        /// Gets the entities currently inside a trigger.
        /// </summary>
        public IReadOnlyCollection<IEntity> GetOccupants(IEntity trigger)
        {
            if (_occupants.TryGetValue(trigger, out var occupants))
            {
                return occupants;
            }
            return Array.Empty<IEntity>();
        }

        /// <summary>
        /// Checks if an entity is inside any trigger.
        /// </summary>
        public IEntity GetTriggerAt(IEntity entity)
        {
            foreach (var kvp in _occupants)
            {
                if (kvp.Value.Contains(entity))
                {
                    return kvp.Key;
                }
            }
            return null;
        }
    }
}

