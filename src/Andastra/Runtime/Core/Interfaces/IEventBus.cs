using System;
using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Event bus for routing entity and world events.
    /// </summary>
    /// <remarks>
    /// Event Bus Interface:
    /// - TODO: lookup data from daorigins.exe/dragonage2.exe/masseffect.exe/masseffect2.exe/swkotor.exe/swkotor2.exe and split into subclass'd inheritence structures appropriately. parent class(es) should contain common code.
    /// - Based on swkotor2.exe script event system
    /// - Located via string references: Script event constants (EVENT_ON_*, CSWSSCRIPTEVENT_EVENTTYPE_*)
    /// - Subscribe/Unsubscribe: Register/unregister event handlers for specific event types
    /// - Publish: Immediately dispatches event to all subscribers (synchronous)
    /// - QueueEvent: Queues event for deferred dispatch at frame boundary (prevents re-entrancy issues)
    /// - DispatchQueuedEvents: Processes all queued events (called at frame boundary)
    /// - FireScriptEvent: Fires script event on entity (triggers script hooks like OnHeartbeat, OnAttacked, etc.)
    /// - Script events: Heartbeat, OnNotice, Attacked, Damaged, Death, Dialogue, etc.
    /// - Event system decouples components and allows script-driven behavior
    /// - Event routing: FUN_004dcfb0 @ 0x004dcfb0 routes script events to entity handlers (case 0xa = OnDeath, case 0x1 = OnHeartbeat, etc.)
    /// </remarks>
    public interface IEventBus
    {
        /// <summary>
        /// Subscribes to events of a specific type.
        /// </summary>
        void Subscribe<T>(Action<T> handler) where T : IGameEvent;

        /// <summary>
        /// Unsubscribes from events of a specific type.
        /// </summary>
        void Unsubscribe<T>(Action<T> handler) where T : IGameEvent;

        /// <summary>
        /// Publishes an event to all subscribers.
        /// </summary>
        void Publish<T>(T gameEvent) where T : IGameEvent;

        /// <summary>
        /// Queues an event for deferred dispatch at frame boundary.
        /// </summary>
        void QueueEvent<T>(T gameEvent) where T : IGameEvent;

        /// <summary>
        /// Dispatches all queued events.
        /// </summary>
        void DispatchQueuedEvents();

        /// <summary>
        /// Fires a script event on an entity.
        /// </summary>
        void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer = null);
    }

    /// <summary>
    /// Base interface for all game events.
    /// </summary>
    public interface IGameEvent
    {
        /// <summary>
        /// The entity this event relates to (if any).
        /// </summary>
        IEntity Entity { get; }
    }
}

