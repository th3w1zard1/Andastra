using System;
using System.Collections.Generic;
using System.Numerics;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;

namespace Odyssey.Core.Triggers
{
    /// <summary>
    /// Trigger volume system for OnEnter/OnExit events.
    /// </summary>
    /// <remarks>
    /// Trigger System:
    /// - Based on swkotor2.exe trigger system
    /// - Located via string references: "OnEnter" @ 0x007bee60, "OnExit" @ 0x007bee70
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ENTER" @ 0x007bc9b8, "CSWSSCRIPTEVENT_EVENTTYPE_ON_EXIT" @ 0x007bc9cc
    /// - "Trigger" @ 0x007c1a80, "TriggerType" @ 0x007c38d0, "TriggerActive" @ 0x007c38dc
    /// - Original implementation: FUN_005226d0 @ 0x005226d0 saves trigger data including polygon geometry
    /// - Trigger geometry: Polygon defined by vertices in 2D (X/Z plane, Y is height)
    /// - Point-in-polygon test: Uses ray casting algorithm to determine if point is inside polygon
    /// - Trigger events: OnEnter fires when entity enters trigger volume, OnExit when entity leaves
    /// - FireOnce: If true, trigger only fires once and then becomes inactive
    /// - Trigger activation: Triggers can be activated/deactivated via script
    /// - Trigger detection: Checks all creatures in area against all triggers each frame
    /// </remarks>
    public class TriggerSystem
    {
        private readonly IWorld _world;
        private readonly Dictionary<ITriggerComponent, HashSet<IEntity>> _occupants;

        public TriggerSystem(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException("world");
            _occupants = new Dictionary<ITriggerComponent, HashSet<IEntity>>();
        }

        /// <summary>
        /// Updates the trigger system.
        /// </summary>
        /// <param name="deltaTime">Time since last frame in seconds.</param>
        public void Update(float deltaTime)
        {
            if (_world.CurrentArea == null)
            {
                return;
            }

            // Get all triggers in current area
            IEnumerable<IEntity> triggers = _world.GetEntitiesOfType(ObjectType.Trigger);

            foreach (IEntity triggerEntity in triggers)
            {
                ITriggerComponent trigger = triggerEntity.GetComponent<ITriggerComponent>();
                if (trigger == null)
                {
                    continue;
                }

                // Check if trigger is enabled
                if (!trigger.IsEnabled)
                {
                    continue;
                }

                UpdateTrigger(triggerEntity, trigger);
            }
        }

        /// <summary>
        /// Updates a single trigger.
        /// </summary>
        private void UpdateTrigger(IEntity triggerEntity, ITriggerComponent trigger)
        {
            if (!_occupants.ContainsKey(trigger))
            {
                _occupants[trigger] = new HashSet<IEntity>();
            }

            HashSet<IEntity> currentOccupants = GetEntitiesInTrigger(triggerEntity, trigger);
            HashSet<IEntity> previousOccupants = _occupants[trigger];

            // Check for enters
            foreach (IEntity entity in currentOccupants)
            {
                if (!previousOccupants.Contains(entity))
                {
                    if (ShouldFireForEntity(triggerEntity, trigger, entity))
                    {
                        FireOnEnter(triggerEntity, entity);

                        if (trigger.FireOnce)
                        {
                            trigger.HasFired = true;
                        }
                    }
                }
            }

            // Check for exits
            foreach (IEntity entity in previousOccupants)
            {
                if (!currentOccupants.Contains(entity))
                {
                    FireOnExit(triggerEntity, entity);
                }
            }

            _occupants[trigger] = currentOccupants;
        }

        /// <summary>
        /// Gets all entities currently in the trigger volume.
        /// </summary>
        private HashSet<IEntity> GetEntitiesInTrigger(IEntity triggerEntity, ITriggerComponent trigger)
        {
            HashSet<IEntity> result = new HashSet<IEntity>();

            // Get all creatures in the area
            IEnumerable<IEntity> creatures = _world.GetEntitiesOfType(ObjectType.Creature);

            foreach (IEntity creature in creatures)
            {
                ITransformComponent creatureTransform = creature.GetComponent<ITransformComponent>();
                if (creatureTransform != null && IsPointInTrigger(creatureTransform.Position, triggerEntity, trigger))
                {
                    result.Add(creature);
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a point is inside the trigger polygon.
        /// </summary>
        private bool IsPointInTrigger(Vector3 point, IEntity triggerEntity, ITriggerComponent trigger)
        {
            if (trigger.Geometry == null || trigger.Geometry.Count < 3)
            {
                return false;
            }

            // Transform point to trigger's local space (if trigger has transform)
            ITransformComponent triggerTransform = triggerEntity.GetComponent<ITransformComponent>();
            if (triggerTransform == null)
            {
                return false;
            }
            Vector3 localPoint = point - triggerTransform.Position;

            // 2D point-in-polygon test (ignore Y, use X/Z plane)
            return IsPointInPolygon2D(localPoint, trigger.Geometry);
        }

        /// <summary>
        /// 2D point-in-polygon test using ray casting algorithm.
        /// </summary>
        private bool IsPointInPolygon2D(Vector3 point, IList<Vector3> polygon)
        {
            // Ray casting algorithm: count intersections with polygon edges
            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; j = i++)
            {
                Vector3 vi = polygon[i];
                Vector3 vj = polygon[j];

                // Check if ray from point to right intersects edge
                if (((vi.Z > point.Z) != (vj.Z > point.Z)) &&
                    (point.X < (vj.X - vi.X) * (point.Z - vi.Z) / (vj.Z - vi.Z) + vi.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        /// <summary>
        /// Checks if trigger should fire for entity.
        /// </summary>
        private bool ShouldFireForEntity(IEntity triggerEntity, ITriggerComponent trigger, IEntity entity)
        {
            // Don't fire if already fired and FireOnce is true
            if (trigger.FireOnce && trigger.HasFired)
            {
                return false;
            }

            // Check trigger conditions (could add more complex logic here)
            return true;
        }

        /// <summary>
        /// Fires OnEnter event.
        /// </summary>
        private void FireOnEnter(IEntity triggerEntity, IEntity entity)
        {
            string script = triggerEntity.GetScript(ScriptEvent.OnEnter);
            if (!string.IsNullOrEmpty(script))
            {
                // TODO: Execute script with trigger and entering entity context
                // ScriptExecutor.ExecuteScript(script, triggerEntity, entity)
            }

            // Fire world event
            if (_world.EventBus != null)
            {
                _world.EventBus.Publish(new TriggerEvent
                {
                    Trigger = triggerEntity,
                    Entity = entity,
                    Type = TriggerEventType.Enter
                });
            }
        }

        /// <summary>
        /// Fires OnExit event.
        /// </summary>
        private void FireOnExit(IEntity triggerEntity, IEntity entity)
        {
            string script = triggerEntity.GetScript(ScriptEvent.OnExit);
            if (!string.IsNullOrEmpty(script))
            {
                // TODO: Execute script with trigger and exiting entity context
                // ScriptExecutor.ExecuteScript(script, triggerEntity, entity)
            }

            // Fire world event
            if (_world.EventBus != null)
            {
                _world.EventBus.Publish(new TriggerEvent
                {
                    Trigger = triggerEntity,
                    Entity = entity,
                    Type = TriggerEventType.Exit
                });
            }
        }
    }

    /// <summary>
    /// Trigger event type.
    /// </summary>
    public enum TriggerEventType
    {
        Enter,
        Exit
    }

    /// <summary>
    /// Trigger event.
    /// </summary>
    public class TriggerEvent
    {
        public IEntity Trigger { get; set; }
        public IEntity Entity { get; set; }
        public TriggerEventType Type { get; set; }
    }
}

