using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;

namespace Andastra.Runtime.Core.Triggers
{
    /// <summary>
    /// Trigger volume system for OnEnter/OnExit events.
    /// </summary>
    /// <remarks>
    /// Trigger System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) trigger system
    /// - LoadTriggerList @ 0x004e5920 - Loads trigger list from GIT GFF into area (located via "TriggerList" @ 0x007bd254)
    ///   - Iterates through "TriggerList" GFF list, reads ObjectId, TemplateResRef, position (XPosition, YPosition, ZPosition), geometry polygon, LinkedToModule, TransitionDestination, LinkedTo, LinkedToFlags. Creates trigger entities and adds to area
    /// - SaveTriggerList @ 0x004e2b20 - Saves trigger list from area to GFF save data (located via "TriggerList" @ 0x007bd254)
    /// - SaveEntityState @ 0x005226d0 - Saves entity state including trigger data (located via "TriggerList" @ 0x007bd254)
    /// - Located via string references: "Trigger" @ 0x007bc51c, "TriggerList" @ 0x007bd254
    /// - Script events: "OnEnter" @ 0x007bd708, "OnExit" @ 0x007bd700, "OnClick" @ 0x007c1a20
    /// - "ScriptOnEnter" @ 0x007c1d40, "ScriptOnExit" @ 0x007c1d30 (trigger script ResRef fields)
    /// - Event types: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER" @ 0x007bc9b8 (0xc), "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT" @ 0x007bc9cc (0xd)
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc9e0 (0x1e), "EVENT_ENTERED_TRIGGER" @ 0x007bce08 (event type 2)
    /// - "EVENT_LEFT_TRIGGER" @ 0x007bcdf4 (event type 3), "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles script event dispatching
    ///   - Case 2 = EVENT_ENTERED_TRIGGER (fires OnEnter scripts)
    ///   - Case 3 = EVENT_LEFT_TRIGGER (fires OnExit scripts)
    ///   - Case 0x1e (30) = CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED (fires OnClick scripts for doors/placeables/triggers)
    /// - Trigger geometry: Polygon defined by vertices in 2D (X/Z plane, Y is height) stored in UTT template Geometry field
    /// - Point-in-polygon test: Uses ray casting algorithm to determine if point is inside polygon
    /// - Trigger events: OnEnter fires when entity enters trigger volume, OnExit when entity leaves, OnClick when trigger is clicked
    /// - FireOnce: If true, trigger only fires once and then becomes inactive (HasFired flag prevents multiple firings)
    /// - Trigger activation: Triggers can be activated/deactivated via script (IsEnabled flag)
    /// - Trigger detection: Checks all creatures in area against all triggers each frame
    /// - Trigger types: 0=generic, 1=transition, 2=trap (TriggerType field in UTT template)
    /// - Trap triggers: Can be disarmed via Security skill check, fire OnDisarm script event
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
            // Record entering object for GetEnteringObject() function
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetEnteringObject tracks last entity that entered trigger/door/placeable
            // Located via string references: "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "OnEnter" @ 0x007bd708
            // Original implementation: Stores last entering entity ID for script queries
            // Store in entity's custom data to avoid circular dependencies
            if (triggerEntity is Entities.Entity triggerEntityImpl)
            {
                triggerEntityImpl.SetData("LastEnteringObjectId", entity.ObjectId);
            }

            IScriptHooksComponent scriptHooks = triggerEntity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                string script = scriptHooks.GetScript(ScriptEvent.OnEnter);
                if (!string.IsNullOrEmpty(script))
                {
                    // Execute script with trigger as owner and entering entity as triggerer
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Trigger OnEnter script execution
                    // Located via string references: "OnEnter" @ 0x007bee60
                    // Original implementation: FUN_005226d0 @ 0x005226d0 executes trigger scripts with entity context
                    if (_world.EventBus != null)
                    {
                        _world.EventBus.FireScriptEvent(triggerEntity, ScriptEvent.OnEnter, entity);
                    }
                }
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
            // Record exiting object for GetExitingObject() function
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GetExitingObject tracks last entity that exited trigger/door/placeable
            // Located via string references: "EVENT_LEFT_TRIGGER" @ 0x007bcdf4, "OnExit" @ 0x007bd700
            // Original implementation: Stores last exiting entity ID for script queries
            // Store in entity's custom data to avoid circular dependencies
            if (triggerEntity is Entities.Entity triggerEntityImpl)
            {
                triggerEntityImpl.SetData("LastExitingObjectId", entity.ObjectId);
            }

            IScriptHooksComponent scriptHooks = triggerEntity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                string script = scriptHooks.GetScript(ScriptEvent.OnExit);
                if (!string.IsNullOrEmpty(script))
                {
                    // Execute script with trigger as owner and exiting entity as triggerer
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Trigger OnExit script execution
                    // Located via string references: "OnExit" @ 0x007bee70
                    // Original implementation: FUN_005226d0 @ 0x005226d0 executes trigger scripts with entity context
                    if (_world.EventBus != null)
                    {
                        _world.EventBus.FireScriptEvent(triggerEntity, ScriptEvent.OnExit, entity);
                    }
                }
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
    public class TriggerEvent : Interfaces.IGameEvent
    {
        public IEntity Trigger { get; set; }
        public IEntity Entity { get; set; }
        public TriggerEventType Type { get; set; }
    }
}

