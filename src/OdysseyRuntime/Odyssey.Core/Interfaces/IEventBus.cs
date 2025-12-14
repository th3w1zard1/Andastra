using System;
using Odyssey.Core.Enums;

namespace Odyssey.Core.Interfaces
{
    /// <summary>
    /// Event bus for routing entity and world events.
    /// </summary>
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

