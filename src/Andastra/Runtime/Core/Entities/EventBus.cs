using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Entities
{
    /// <summary>
    /// Odyssey Engine event bus implementation.
    /// </summary>
    /// <remarks>
    /// Odyssey Event Bus System:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) event system
    /// - Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles all object event dispatching
    /// - Located via string references: "EventQueue" @ 0x007bce74, "EventId" @ 0x007bce48, "EventData" @ 0x007bce3c
    /// - Debug output: "DRF Event Added: %s(%s) %s(%s) %s %s\n" @ 0x007bc55c (event logging format)
    /// - Original implementation (from decompiled 0x004dcfb0):
    ///   1. Switch on event type (param_2) to map to EVENT_* string constants:
    ///      - Case 1: "EVENT_TIMED_EVENT"
    ///      - Case 2: "EVENT_ENTERED_TRIGGER"
    ///      - Case 3: "EVENT_LEFT_TRIGGER"
    ///      - Case 4: "EVENT_REMOVE_FROM_AREA"
    ///      - Case 5: "EVENT_APPLY_EFFECT"
    ///      - Case 6: "EVENT_CLOSE_OBJECT"
    ///      - Case 7: "EVENT_OPEN_OBJECT"
    ///      - Case 8: "EVENT_SPELL_IMPACT"
    ///      - Case 9: "EVENT_PLAY_ANIMATION"
    ///      - Case 10: "EVENT_SIGNAL_EVENT" (triggers script event type switch)
    ///      - Case 0xb: "EVENT_DESTROY_OBJECT"
    ///      - Case 0xc: "EVENT_UNLOCK_OBJECT"
    ///      - Case 0xd: "EVENT_LOCK_OBJECT"
    ///      - Case 0xe: "EVENT_REMOVE_EFFECT"
    ///      - Case 0xf: "EVENT_ON_MELEE_ATTACKED"
    ///      - Case 0x10: "EVENT_DECREMENT_STACKSIZE"
    ///      - Case 0x11: "EVENT_SPAWN_BODY_BAG"
    ///      - Case 0x12: "EVENT_FORCED_ACTION"
    ///      - Case 0x13: "EVENT_ITEM_ON_HIT_SPELL_IMPACT"
    ///      - Case 0x14: "EVENT_BROADCAST_AOO"
    ///      - Case 0x15: "EVENT_BROADCAST_SAFE_PROJECTILE"
    ///      - Case 0x16: "EVENT_FEEDBACK_MESSAGE"
    ///      - Case 0x17: "EVENT_ABILITY_EFFECT_APPLIED"
    ///      - Case 0x18: "EVENT_SUMMON_CREATURE"
    ///      - Case 0x19: "EVENT_ACQUIRE_ITEM"
    ///      - Case 0x1a: "EVENT_AREA_TRANSITION"
    ///      - Case 0x1b: "EVENT_CONTROLLER_RUMBLE"
    ///   2. If event type is 10 (EVENT_SIGNAL_EVENT), switch on param_3 to map to CSWSSCRIPTEVENT_EVENTTYPE_ON_*:
    ///      - Case 0: "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT"
    ///      - Case 1: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION"
    ///      - Case 2: "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT"
    ///      - Case 4: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED"
    /// - Inheritance: Inherits from BaseEventBus (Runtime.Games.Common) with Odyssey-specific event handling
    ///      - Case 5: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED"
    ///      - Case 7: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE"
    ///      - Case 8: "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN"
    ///      - Case 9: "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED"
    ///      - Case 10: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH"
    ///      - Case 0xb: "CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT"
    ///      - Case 0xc: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER"
    ///      - Case 0xd: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT"
    ///      - Case 0xe: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER"
    ///      - Case 0xf: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT"
    ///      - Case 0x10: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START"
    ///      - Case 0x11: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD"
    ///      - Case 0x12: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM"
    ///      - Case 0x13: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM"
    ///      - Case 0x14: "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM"
    ///      - Case 0x15: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED"
    ///      - Case 0x16: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN"
    ///      - Case 0x17: "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE"
    ///      - Case 0x18: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM"
    ///      - Case 0x19: "CSWSSCRIPTEVENT_EVENTTYPE_ON_USED"
    ///      - Case 0x1a: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED"
    ///      - Case 0x1b: "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED"
    ///      - Case 0x1c: "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED"
    ///      - Case 0x1d: "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED"
    ///      - Case 0x1e: "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED"
    ///      - Case 0x1f: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED"
    ///      - Case 0x20: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING"
    ///      - Case 0x21: "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED"
    ///      - Case 0x22: "CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN"
    ///      - Case 0x23: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST"
    ///      - Case 0x24: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE"
    ///      - Case 0x25: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP"
    ///      - Case 0x26: "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM"
    ///   3. Formats event data and logs with "DRF Event Added: %s(%s) %s(%s) %s %s\n" format
    /// - Script event types (CSWSSCRIPTEVENT_EVENTTYPE_ON_*): 
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594 (0x26)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP" @ 0x007bc5bc (0x25)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec (0x24)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST" @ 0x007bc620 (0x23)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN" @ 0x007bc64c (0x22)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED" @ 0x007bc678 (0x21)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING" @ 0x007bc6ac (0x20)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED" @ 0x007bc6d8 (0x1f)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704 (0x1e)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c (0x1d)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754 (0x1c)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_USED" @ 0x007bc7d8 (0x19)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM" @ 0x007bc7fc (0x18)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820 (0x17)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844 (0x16)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED" @ 0x007bc868 (0x15)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c (0x14)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4 (0x13)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0 (0x12)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c (0x11)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948 (0x10)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT" @ 0x007bc974 (0xf)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER" @ 0x007bc9a0 (0xe)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT" @ 0x007bc9cc (0xd)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER" @ 0x007bc9f8 (0xc)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT" @ 0x007bca24 (0xb)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH" @ 0x007bca54 (0xa)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED" @ 0x007bca78 (0x9)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN" @ 0x007bca9c (0x8)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4 (0x7)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISTURBED" @ 0x007bcaec (0x5)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPELLCASTAT" @ 0x007bcb3c (0x2)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68 (0x1)
    ///   - "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT" @ 0x007bcb90 (0x0)
    /// - Object events (EVENT_*): "EVENT_OPEN_OBJECT" @ 0x007bcda0 (case 7), "EVENT_CLOSE_OBJECT" @ 0x007bcdb4 (case 6),
    ///   "EVENT_LOCK_OBJECT" @ 0x007bcd20 (case 0xd), "EVENT_UNLOCK_OBJECT" @ 0x007bcd34 (case 0xc),
    ///   "EVENT_DESTROY_OBJECT" @ 0x007bcd48 (case 0xb), "EVENT_SPELL_IMPACT" @ 0x007bcd8c (case 8),
    ///   "EVENT_PLAY_ANIMATION" @ 0x007bcd74 (case 9), "EVENT_SIGNAL_EVENT" @ 0x007bcd60 (case 10),
    ///   "EVENT_REMOVE_FROM_AREA" @ 0x007bcddc (case 4), "EVENT_APPLY_EFFECT" @ 0x007bcdc8 (case 5),
    ///   "EVENT_LEFT_TRIGGER" @ 0x007bcdf4 (case 3), "EVENT_ENTERED_TRIGGER" @ 0x007bce08 (case 2),
    ///   "EVENT_TIMED_EVENT" @ 0x007bce20 (case 1), "EVENT_ON_MELEE_ATTACKED" @ 0x007bccf4 (case 0xf),
    ///   "EVENT_REMOVE_EFFECT" @ 0x007bcd0c (case 0xe), "EVENT_ACQUIRE_ITEM" @ 0x007bcbf4 (case 0x19),
    ///   "EVENT_AREA_TRANSITION" @ 0x007bcbdc (case 0x1a), "EVENT_CONTROLLER_RUMBLE" @ 0x007bcbc4 (case 0x1b)
    /// - Original implementation: 0x004dcfb0 @ 0x004dcfb0 dispatches events via switch statement based on event type
    ///   - Original implementation (from decompiled 0x004dcfb0):
    ///     - Function signature: `void 0x004dcfb0(uint param_1, uint param_2, undefined2 *param_3)`
    ///     - param_1: Target entity ID (OBJECT_SELF)
    ///     - param_2: Event type (1-27, maps to EVENT_* constants)
    ///     - param_3: Script event type (for EVENT_SIGNAL_EVENT, maps to CSWSSCRIPTEVENT_EVENTTYPE_ON_*)
    ///     - Calls 0x004dcd90 to resolve entity IDs to entity names/strings
    ///     - Switch on param_2 to map event type to EVENT_* string constant
    ///     - If param_2 == 10 (EVENT_SIGNAL_EVENT), switch on *param_3 to map to CSWSSCRIPTEVENT_EVENTTYPE_ON_* string constant
    ///     - Formats debug log: "DRF Event Added: %s(%s) %s(%s) %s %s\n" with event name, entity names, and event data
    ///     - Routes event to script execution system for entities with matching event hooks
    /// - Events are queued ("EventQueue" @ 0x007bce74) and dispatched each frame
    /// - Event routing: Events fire for various game state changes (damage, death, perception, etc.)
    /// - Script execution: FireScriptEvent method triggers script execution on entities with matching event hooks
    /// - Event structure: Events contain Entity (OBJECT_SELF), EventType, and Triggerer (entity that triggered event)
    /// - 0x004dcfb0 formats event name from type, constructs event data, and routes to script execution system
    /// </remarks>
    public class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers;
        private readonly Queue<IGameEvent> _eventQueue;
        private readonly object _lock = new object();

        public EventBus()
        {
            _subscribers = new Dictionary<Type, List<Delegate>>();
            _eventQueue = new Queue<IGameEvent>();
        }

        public void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
            {
                throw new ArgumentNullException("handler");
            }

            lock (_lock)
            {
                Type type = typeof(T);
                if (!_subscribers.TryGetValue(type, out List<Delegate> handlers))
                {
                    handlers = new List<Delegate>();
                    _subscribers[type] = handlers;
                }
                handlers.Add(handler);
            }
        }

        public void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
            {
                return;
            }

            lock (_lock)
            {
                Type type = typeof(T);
                if (_subscribers.TryGetValue(type, out List<Delegate> handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            List<Delegate> handlers;
            lock (_lock)
            {
                Type type = typeof(T);
                if (!_subscribers.TryGetValue(type, out handlers))
                {
                    return;
                }
                // Copy to avoid modification during iteration
                handlers = new List<Delegate>(handlers);
            }

            foreach (Delegate handler in handlers)
            {
                var typedHandler = handler as Action<T>;
                if (typedHandler != null)
                {
                    typedHandler(gameEvent);
                }
            }
        }

        public void QueueEvent<T>(T gameEvent) where T : IGameEvent
        {
            lock (_lock)
            {
                _eventQueue.Enqueue(gameEvent);
            }
        }

        public void DispatchQueuedEvents()
        {
            List<IGameEvent> eventsToDispatch;
            lock (_lock)
            {
                if (_eventQueue.Count == 0)
                {
                    return;
                }

                eventsToDispatch = new List<IGameEvent>();
                while (_eventQueue.Count > 0)
                {
                    eventsToDispatch.Add(_eventQueue.Dequeue());
                }
            }

            foreach (IGameEvent evt in eventsToDispatch)
            {
                DispatchEvent(evt);
            }
        }

        private void DispatchEvent(IGameEvent gameEvent)
        {
            Type type = gameEvent.GetType();
            List<Delegate> handlers;
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(type, out handlers))
                {
                    return;
                }
                handlers = new List<Delegate>(handlers);
            }

            foreach (Delegate handler in handlers)
            {
                handler.DynamicInvoke(gameEvent);
            }
        }

        public void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer = null)
        {
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Script event firing implementation
            // Event dispatching: 0x004dcfb0 @ 0x004dcfb0 handles all script event dispatching
            // Located via string references: "EventQueue" @ 0x007bce74, "EventId" @ 0x007bce48, "EventData" @ 0x007bce3c
            // Original implementation: 0x004dcfb0 formats event name from type (e.g., "EVENT_OPEN_OBJECT" for case 7),
            // constructs event data structure with source entity, target entity, event type, routes to script execution system
            // Script events fire on entities with matching event hooks (ScriptHeartbeat, ScriptOnNotice, ScriptOnOpen, etc.)
            // Events are queued ("EventQueue" @ 0x007bce74) and dispatched each frame
            // Event structure contains: source entity ObjectId, target entity ObjectId, event type identifier
            // Debug output: "DRF Event Added: %s(%s) %s(%s) %s %s\n" @ 0x007bc55c logs event firing
            var evt = new ScriptEventArgs(entity, eventType, triggerer);
            QueueEvent(evt);
        }
    }

    /// <summary>
    /// Event args for script events.
    /// </summary>
    public class ScriptEventArgs : IGameEvent
    {
        public ScriptEventArgs(IEntity entity, ScriptEvent eventType, IEntity triggerer)
        {
            Entity = entity;
            EventType = eventType;
            Triggerer = triggerer;
        }

        public IEntity Entity { get; }
        public ScriptEvent EventType { get; }
        public IEntity Triggerer { get; }
    }
}

