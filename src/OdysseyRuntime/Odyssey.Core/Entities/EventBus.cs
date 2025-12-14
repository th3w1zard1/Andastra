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
    /// - Located via string references: Event firing functions handle script events and game events
    /// - Original implementation: Events fire for various game state changes (damage, death, perception, etc.)
    /// - Script events: OnHeartbeat, OnPerception, OnAttacked, OnDamaged, OnDeath, etc. (see ScriptEvent enum)
    /// - Game events: Damage, death, door opened/closed, combat events, etc.
    /// - Events are routed to subscribed handlers and can trigger script execution
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

