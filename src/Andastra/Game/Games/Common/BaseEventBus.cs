using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of event bus shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Event Bus Implementation:
    /// - Common event system for inter-object communication
    /// - Type-safe event subscription and firing
    /// - Supports both immediate and queued event processing
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Event dispatching systems
    /// - swkotor2.exe: Event routing and script event handling
    /// - nwmain.exe: Aurora event systems
    /// - daorigins.exe: Eclipse event management
    /// - Common event patterns: Script events, combat events, interaction events
    ///
    /// Common functionality across engines:
    /// - Event subscription by type (generic type safety)
    /// - Event firing (immediate or queued)
    /// - Event handler management (add/remove)
    /// - Event filtering and routing
    /// - Performance considerations (avoid recursion, handle large numbers of events)
    /// </remarks>
    [PublicAPI]
    public abstract class BaseEventBus : IEventBus
    {
        protected readonly Dictionary<Type, List<Delegate>> _eventHandlers = new Dictionary<Type, List<Delegate>>();
        protected readonly Queue<EventEntry> _queuedEvents = new Queue<EventEntry>();

        protected struct EventEntry
        {
            public Type EventType;
            public object EventData;
        }

        /// <summary>
        /// Subscribes to events of a specific type.
        /// </summary>
        /// <typeparam name="T">The event type to subscribe to.</typeparam>
        /// <param name="handler">The event handler delegate.</param>
        /// <remarks>
        /// Common subscription mechanism across all engines.
        /// Type-safe event handling prevents runtime errors.
        /// Handlers are stored and called when events are fired.
        /// </remarks>
        public virtual void Subscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var eventType = typeof(T);
            if (!_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _eventHandlers[eventType] = handlers;
            }

            handlers.Add(handler);
        }

        /// <summary>
        /// Unsubscribes from events of a specific type.
        /// </summary>
        /// <typeparam name="T">The event type to unsubscribe from.</typeparam>
        /// <param name="handler">The event handler delegate to remove.</param>
        /// <remarks>
        /// Removes specific handler from event subscriptions.
        /// Important for cleanup to prevent memory leaks.
        /// </remarks>
        public virtual void Unsubscribe<T>(Action<T> handler) where T : IGameEvent
        {
            if (handler == null)
                return;

            var eventType = typeof(T);
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                if (handlers.Count == 0)
                    _eventHandlers.Remove(eventType);
            }
        }

        /// <summary>
        /// Fires an event immediately.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="eventData">The event data.</param>
        /// <remarks>
        /// Immediate event firing for time-critical events.
        /// Calls all subscribed handlers synchronously.
        /// Use with caution to avoid recursion and performance issues.
        /// </remarks>
        public virtual void Fire<T>(T eventData) where T : class
        {
            if (eventData == null)
                return;

            var eventType = typeof(T);
            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                // Create a copy to avoid modification during iteration
                var handlersCopy = new List<Delegate>(handlers);
                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        ((Action<T>)handler)(eventData);
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue processing other handlers
                        HandleEventError(eventType, ex);
                    }
                }
            }
        }

        /// <summary>
        /// Queues an event for later processing.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="eventData">The event data.</param>
        /// <remarks>
        /// Queues events for processing in the next frame or update cycle.
        /// Prevents recursion and ensures proper execution order.
        /// Common pattern for script events and complex interactions.
        /// </remarks>
        public virtual void Queue<T>(T eventData) where T : class
        {
            if (eventData == null)
                return;

            _queuedEvents.Enqueue(new EventEntry
            {
                EventType = typeof(T),
                EventData = eventData
            });
        }

        /// <summary>
        /// Processes all queued events.
        /// </summary>
        /// <remarks>
        /// Called during the update cycle to process queued events.
        /// Clears the queue after processing.
        /// Engine-specific subclasses can control when this is called.
        /// </remarks>
        public virtual void ProcessQueuedEvents()
        {
            while (_queuedEvents.Count > 0)
            {
                var entry = _queuedEvents.Dequeue();
                ProcessQueuedEvent(entry);
            }
        }

        /// <summary>
        /// Processes a single queued event.
        /// </summary>
        /// <remarks>
        /// Internal method to process individual queued events.
        /// Can be overridden for engine-specific processing logic.
        /// </remarks>
        protected virtual void ProcessQueuedEvent(EventEntry entry)
        {
            if (_eventHandlers.TryGetValue(entry.EventType, out var handlers))
            {
                var handlersCopy = new List<Delegate>(handlers);
                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        // Use dynamic invoke for queued events
                        handler.DynamicInvoke(entry.EventData);
                    }
                    catch (Exception ex)
                    {
                        HandleEventError(entry.EventType, ex);
                    }
                }
            }
        }

        /// <summary>
        /// Clears all queued events without processing them.
        /// </summary>
        /// <remarks>
        /// Emergency cleanup method.
        /// Used when resetting world state or handling errors.
        /// </remarks>
        public virtual void ClearQueuedEvents()
        {
            _queuedEvents.Clear();
        }

        /// <summary>
        /// Gets the number of queued events.
        /// </summary>
        /// <remarks>
        /// Useful for debugging and performance monitoring.
        /// Indicates event processing backlog.
        /// </remarks>
        public virtual int GetQueuedEventCount()
        {
            return _queuedEvents.Count;
        }

        /// <summary>
        /// Handles event processing errors.
        /// </summary>
        /// <remarks>
        /// Common error handling for event system.
        /// Logs errors but continues processing.
        /// Engine-specific subclasses can add additional error reporting.
        /// </remarks>
        protected virtual void HandleEventError(Type eventType, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Event processing error for {eventType.Name}: {ex.Message}");
        }

        /// <summary>
        /// Gets event system statistics.
        /// </summary>
        /// <remarks>
        /// Returns handler counts and queue statistics.
        /// Useful for debugging and performance monitoring.
        /// </remarks>
        public virtual EventBusStats GetStats()
        {
            var stats = new EventBusStats
            {
                HandlerCount = 0,
                QueuedEventCount = _queuedEvents.Count,
                HandlersByType = new Dictionary<string, int>()
            };

            foreach (var kvp in _eventHandlers)
            {
                var typeName = kvp.Key.Name;
                var count = kvp.Value.Count;
                stats.HandlerCount += count;
                stats.HandlersByType[typeName] = count;
            }

            return stats;
        }

        /// <summary>
        /// Publishes an event to all subscribers.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="gameEvent">The game event to publish.</param>
        /// <remarks>
        /// Immediate event publishing for time-critical events.
        /// Calls all subscribed handlers synchronously.
        /// </remarks>
        void IEventBus.Publish<T>(T gameEvent)
        {
            if (gameEvent == null)
            {
                throw new ArgumentNullException(nameof(gameEvent));
            }
            // IGameEvent is always a reference type, so this cast is safe
            object eventObj = gameEvent;
            FireInternal(eventObj, typeof(T));
        }

        /// <summary>
        /// Queues an event for deferred dispatch at frame boundary.
        /// </summary>
        /// <typeparam name="T">The event type.</typeparam>
        /// <param name="gameEvent">The game event to queue.</param>
        /// <remarks>
        /// Queues events for processing in the next frame or update cycle.
        /// Prevents recursion and ensures proper execution order.
        /// </remarks>
        void IEventBus.QueueEvent<T>(T gameEvent)
        {
            if (gameEvent == null)
            {
                throw new ArgumentNullException(nameof(gameEvent));
            }
            // IGameEvent is always a reference type, so this cast is safe
            object eventObj = gameEvent;
            QueueInternal(eventObj, typeof(T));
        }

        private void FireInternal(object eventData, Type eventType)
        {
            if (eventData == null)
                return;

            if (_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                var handlersCopy = new List<Delegate>(handlers);
                foreach (var handler in handlersCopy)
                {
                    try
                    {
                        handler.DynamicInvoke(eventData);
                    }
                    catch (Exception ex)
                    {
                        HandleEventError(eventType, ex);
                    }
                }
            }
        }

        private void QueueInternal(object eventData, Type eventType)
        {
            if (eventData == null)
                return;

            _queuedEvents.Enqueue(new EventEntry
            {
                EventType = eventType,
                EventData = eventData
            });
        }

        /// <summary>
        /// Dispatches all queued events.
        /// </summary>
        /// <remarks>
        /// Called during the update cycle to process queued events.
        /// Clears the queue after processing.
        /// </remarks>
        public virtual void DispatchQueuedEvents()
        {
            ProcessQueuedEvents();
        }

        /// <summary>
        /// Fires a script event on an entity.
        /// </summary>
        /// <param name="entity">The entity to fire the event on.</param>
        /// <param name="eventType">The script event type.</param>
        /// <param name="triggerer">The triggering entity (optional).</param>
        /// <remarks>
        /// Fires script events that trigger script hooks on entities.
        /// Engine-specific subclasses should implement script execution.
        /// </remarks>
        public abstract void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer = null);
    }

    /// <summary>
    /// Event bus statistics for debugging and monitoring.
    /// </summary>
    public struct EventBusStats
    {
        /// <summary>
        /// Total number of event handlers.
        /// </summary>
        public int HandlerCount;

        /// <summary>
        /// Number of queued events.
        /// </summary>
        public int QueuedEventCount;

        /// <summary>
        /// Number of handlers by event type.
        /// </summary>
        public Dictionary<string, int> HandlersByType;
    }
}
