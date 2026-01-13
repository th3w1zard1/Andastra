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
    /// Based on verified components of:
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
    ///
    /// Engine-Specific Implementations (Merged):
    /// - Odyssey Engine: Based on swkotor.exe/swkotor2.exe event dispatching (0x004dcfb0)
    /// - Aurora Engine: Based on nwmain.exe/nwn2main.exe event systems (CServerAIMaster::AddEventDeltaTime)
    /// - Eclipse Engine: Based on daorigins.exe/DragonAge2.exe event systems (UnrealScript BioEventDispatcher)
    ///
    /// All engine-specific event bus classes (OdysseyEventBus, AuroraEventBus, EclipseEventBus) have been
    /// merged into this base class since their implementations are identical. Engine-specific differences
    /// are documented in method comments and handled by the script execution system, not the event bus itself.
    /// </remarks>
    [PublicAPI]
    public class BaseEventBus : IEventBus
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
        /// Common implementation across all engines: Events are queued and processed at frame boundaries.
        ///
        /// Odyssey Engine Implementation (swkotor.exe, swkotor2.exe):
        /// - Event dispatching: 0x004dcfb0 @ 0x004dcfb0 (swkotor2.exe) handles all object event dispatching
        /// - Located via string references: "EventQueue" @ 0x007bce74, "EventId" @ 0x007bce48, "EventData" @ 0x007bce3c
        /// - Debug output: "DRF Event Added: %s(%s) %s(%s) %s %s\n" @ 0x007bc55c (event logging format)
        /// - Original implementation: 0x004dcfb0 formats event name from type, constructs event data structure,
        ///   routes to script execution system for entities with matching event hooks
        /// - Events are queued ("EventQueue" @ 0x007bce74) and dispatched each frame
        /// - Event routing: Events fire for various game state changes (damage, death, perception, etc.)
        /// - Script execution: FireScriptEvent method triggers script execution on entities with matching event hooks
        /// - Event structure: Events contain Entity (OBJECT_SELF), EventType, and Triggerer (entity that triggered event)
        ///
        /// Aurora Engine Implementation (nwmain.exe, nwn2main.exe):
        /// - Event dispatching: CServerAIMaster::AddEventDeltaTime @ 0x1405570b0 (nwmain.exe) handles event queueing
        /// - Located via string references: "EventQueue" @ 0x140dfc4c0, "EventId" @ 0x140dfc578, "EventData" @ 0x140dfc580
        /// - Original implementation: AddEventDeltaTime queues events with delta time (days, milliseconds) for precise scheduling
        /// - Events are processed at frame boundaries via CServerAIMaster::UpdateState @ 0x140559cf0
        /// - Event routing: Events dispatched to appropriate object EventHandler methods (CNWSArea::EventHandler @ 0x14035cf50, etc.)
        /// - Script events trigger NWScript execution on entities with matching event hooks (OnHeartbeat, OnPerception, OnDamaged, etc.)
        /// - Events are queued ("EventQueue" @ 0x140dfc4c0) and dispatched each frame to prevent re-entrancy
        /// - Event structure: Events contain Event ID (CallerId), Object ID (ObjectId), Event type, Event data (CScriptEvent*)
        /// - Debug output: "Event added while paused:  EventId: %d   CallerId: %x    ObjectId: %x\n" @ 0x140dfc460 logs event firing
        /// - Script execution: EventHandler methods check for script hooks and execute scripts via CVirtualMachine::RunScript
        /// - Event data: CScriptEvent structure contains event type (short at offset 0x0) and event parameters
        ///
        /// Eclipse Engine Implementation (daorigins.exe, DragonAge2.exe):
        /// - Event data structures: "EventListeners" @ 0x00ae8194 (daorigins.exe), 0x00bf543c (DragonAge2.exe)
        /// - Event identifiers: "EventId" @ 0x00ae81a4 (daorigins.exe), 0x00bf544c (DragonAge2.exe)
        /// - Event scripts: "EventScripts" @ 0x00ae81bc (daorigins.exe), 0x00bf5464 (DragonAge2.exe)
        /// - Command-based event system: "COMMAND_SIGNALEVENT" @ 0x00af4180 (daorigins.exe) processes event commands
        /// - Command processing: Events are dispatched through UnrealScript command system
        /// - UnrealScript event dispatcher: Uses BioEventDispatcher interface
        /// - Event processing pattern:
        ///   1. Events are queued via QueueEvent (inherited from BaseEventBus)
        ///   2. Events are processed at frame boundaries via DispatchQueuedEvents
        ///   3. Event listeners are notified through UnrealScript event dispatcher system
        ///   4. Script execution triggered on entities with matching event hooks
        /// - Uses UnrealScript instead of NWScript for script execution
        /// - Event system integrated with UnrealScript's native event dispatcher
        /// - Command-based event processing through COMMAND_SIGNALEVENT
        /// - Event data stored in UnrealScript object properties (EventListeners, EventScripts, etc.)
        ///
        /// Common Implementation (All Engines):
        /// - All engines queue events and process them at frame boundaries
        /// - All engines use the same event structure: Entity, EventType, Triggerer
        /// - All engines prevent re-entrancy by queuing events instead of firing immediately
        /// - All engines route script events to entities with matching event hooks
        /// </remarks>
        public virtual void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer = null)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            // Common implementation across all engines: Create script event args and queue for processing
            // Events are queued and processed at frame boundary to prevent re-entrancy
            // Engine-specific differences are in script execution (NWScript vs UnrealScript), not in event queuing
            var evt = new ScriptEventArgs(entity, eventType, triggerer);
            ((IEventBus)this).QueueEvent(evt);
        }

        /// <summary>
        /// Event args for script events.
        /// </summary>
        /// <remarks>
        /// Common script event arguments structure used by all engines.
        /// Engine-specific differences:
        /// - Odyssey: Event data routed to NWScript execution system
        /// - Aurora: Event data routed to NWScript execution system via CScriptEvent structure
        /// - Eclipse: Event data routed to UnrealScript execution system via BioEventDispatcher
        /// </remarks>
        private class ScriptEventArgs : IGameEvent
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
