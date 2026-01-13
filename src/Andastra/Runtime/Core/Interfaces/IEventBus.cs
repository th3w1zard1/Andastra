using System;
using Andastra.Runtime.Core.Enums;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Event bus for routing entity and world events.
    /// </summary>
    /// <remarks>
    /// Event Bus Interface:
    /// - Common interface for event bus implementations across all BioWare engines
    /// - Engine-specific implementations:
    ///   - Odyssey: OdysseyEventBus (Runtime.Games.Odyssey) - based on swkotor.exe/swkotor2.exe
    ///   - Aurora: AuroraEventBus (Runtime.Games.Aurora) - based on nwmain.exe/nwn2main.exe
    ///   - Eclipse: EclipseEventBus (Runtime.Games.Eclipse) - based on daorigins.exe/DragonAge2.exe//
    ///   - Infinity: InfinityEventBus (Runtime.Games.Infinity) - based on  executables
    /// - All implementations inherit from BaseEventBus (Runtime.Games.Common) which contains common functionality
    /// - Based on reverse engineering of event systems across all BioWare engines
    /// - Located via string references: Script event constants (EVENT_ON_*, CSWSSCRIPTEVENT_EVENTTYPE_*)
    /// - Subscribe/Unsubscribe: Register/unregister event handlers for specific event types
    /// - Publish: Immediately dispatches event to all subscribers (synchronous)
    /// - QueueEvent: Queues event for deferred dispatch at frame boundary (prevents re-entrancy issues)
    /// - DispatchQueuedEvents: Processes all queued events (called at frame boundary)
    /// - FireScriptEvent: Fires script event on entity (triggers script hooks like OnHeartbeat, OnAttacked, etc.)
    /// - Script events: Heartbeat, OnNotice, Attacked, Damaged, Death, Dialogue, etc.
    /// - Event system decouples components and allows script-driven behavior
    /// - Event routing: Engine-specific implementations route script events to entity handlers
    ///   - Odyssey: 0x004dcfb0 @ 0x004dcfb0 (swkotor2.exe) routes script events to entity handlers
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

