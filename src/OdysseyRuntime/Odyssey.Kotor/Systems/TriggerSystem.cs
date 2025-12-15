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
    /// <remarks>
    /// Trigger System:
    /// - Based on swkotor2.exe trigger system
    /// - Located via string references: "Trigger" @ 0x007bc51c, "TriggerList" @ 0x007bd254
    /// - "$tItrigger" @ 0x007bb979 (trigger variable name), "CB_TRIGGERS" @ 0x007d29c8 (trigger checkbox GUI)
    /// - Script events: "OnEnter" @ 0x007c1d40, "OnExit" @ 0x007c1d30 (trigger script event hooks)
    /// - "ScriptOnEnter" @ 0x007beebc, "ScriptOnExit" @ 0x007beec0 (trigger script ResRef fields)
    /// - "OnTrapTriggered" @ 0x007c1a34 (trap triggered script event), "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac
    /// - "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "EVENT_LEFT_TRIGGER" @ 0x007bcdf4
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles EVENT_ENTERED_TRIGGER (case 2) and EVENT_LEFT_TRIGGER (case 3)
    /// - Trigger loading: FUN_004e08e0 @ 0x004e08e0 loads trigger instances from GIT TriggerList, reads UTT templates
    /// - Original implementation: Triggers have polygon geometry, detect creature entry/exit
    /// - Trigger detection: Updates every frame, checks if creature position is inside trigger polygon
    /// - Script events: OnEnter (entity enters), OnExit (entity exits), OnClick, OnDisarm, OnTrapTriggered
    /// - Trigger geometry stored as polygon vertices in GFF structure (Geometry field in UTT template)
    /// - Trigger types: 0=generic, 1=transition, 2=trap (TriggerType field)
    /// - FireOnce triggers: Only fire once (HasFired flag prevents multiple firings)
    /// - Trigger polygon: 2D polygon projected onto walkmesh, point-in-polygon test for entry/exit detection
    /// - Transition triggers: TriggerType=1 triggers can link to waypoints/areas for area transitions
    /// - Trap triggers: TriggerType=2 triggers can have trap scripts (OnTrapTriggered) and disarm DC
    /// </remarks>
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
            IEnumerable<IEntity> triggers = _world.GetEntitiesOfType(ObjectType.Trigger);
            if (triggers == null)
            {
                return;
            }

            // Get all creatures that can trigger
            IEnumerable<IEntity> creatures = _world.GetEntitiesOfType(ObjectType.Creature);
            if (creatures == null)
            {
                return;
            }

            foreach (IEntity trigger in triggers)
            {
                UpdateTrigger(trigger, creatures);
            }
        }

        private void UpdateTrigger(IEntity trigger, IEnumerable<IEntity> creatures)
        {
            ITriggerComponent triggerComponent = trigger.GetComponent<ITriggerComponent>();
            if (triggerComponent == null)
            {
                return;
            }

            // Get or create occupant set for this trigger
            if (!_occupants.TryGetValue(trigger, out HashSet<IEntity> previousOccupants))
            {
                previousOccupants = new HashSet<IEntity>();
                _occupants[trigger] = previousOccupants;
            }

            var currentOccupants = new HashSet<IEntity>();

            // Check each creature
            foreach (IEntity creature in creatures)
            {
                ITransformComponent transform = creature.GetComponent<ITransformComponent>();
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
            foreach (IEntity entity in currentOccupants)
            {
                if (!previousOccupants.Contains(entity))
                {
                    OnEntityEntered(trigger, entity, triggerComponent);
                }
            }

            // Fire OnExit for entities that left
            foreach (IEntity entity in previousOccupants)
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
            if (_occupants.TryGetValue(trigger, out HashSet<IEntity> occupants))
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
            foreach (KeyValuePair<IEntity, HashSet<IEntity>> kvp in _occupants)
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

