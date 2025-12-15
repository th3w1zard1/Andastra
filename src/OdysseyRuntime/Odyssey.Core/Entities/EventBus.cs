using System;
using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Entities
{
    /// <summary>
    /// Event bus implementation for routing entity and world events.
    /// </summary>
    /// <remarks>
    /// Event Bus System:
    /// - Based on swkotor2.exe event system
    /// - Located via string references: "OnHeartbeat" @ 0x007bd720, "Mod_OnHeartbeat" @ 0x007be840
    /// - Script event types (CSWSSCRIPTEVENT_EVENTTYPE_ON_*): "CSWSSCRIPTEVENT_EVENTTYPE_ON_EQUIP_ITEM" @ 0x007bc594,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_LEVEL_UP" @ 0x007bc5bc, "CSWSSCRIPTEVENT_EVENTTYPE_ON_DESTROYPLAYERCREATURE" @ 0x007bc5ec,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_REST" @ 0x007bc620, "CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN" @ 0x007bc64c,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESPAWN_BUTTON_PRESSED" @ 0x007bc678, "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_DYING" @ 0x007bc6ac,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_PATH_BLOCKED" @ 0x007bc6d8, "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc704,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc72c, "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc754,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_USED" @ 0x007bc7d8, "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM" @ 0x007bc7fc,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc820, "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc844,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED" @ 0x007bc868, "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc89c,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc8c4, "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc8f0,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_EXIT" @ 0x007bc974, "CSWSSCRIPTEVENT_EVENTTYPE_ON_PLAYER_ENTER" @ 0x007bc9a0,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT" @ 0x007bc9cc, "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER" @ 0x007bc9f8,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT" @ 0x007bca24, "CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH" @ 0x007bca54,
    ///   "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED" @ 0x007bca78
    /// - Original implementation: Events fire for various game state changes (damage, death, perception, etc.)
    /// - Script events: OnHeartbeat, OnPerception, OnAttacked, OnDamaged, OnDeath, OnModuleLoad, OnModuleStart, etc.
    /// - Game events: Damage, death, door opened/closed, combat events, inventory changes, etc.
    /// - Event routing: Events are queued and dispatched each frame, routed to subscribed handlers
    /// - Script execution: FireScriptEvent method triggers script execution on entities with matching event hooks
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

