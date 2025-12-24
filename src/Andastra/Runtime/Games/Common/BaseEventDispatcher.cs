using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of event dispatching shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Event Dispatcher Implementation:
    /// - Common event system for area and object events across all engines
    /// - Maps event IDs to string names for debugging and logging
    /// - Routes events to appropriate handlers based on event type
    ///
    /// Based on reverse engineering of multiple BioWare engines.
    /// Common event dispatching patterns identified across all engines.
    ///
    /// Common event types across engines:
    /// - Area events: AREA_TRANSITION, REMOVE_FROM_AREA
    /// - Object events: OPEN_OBJECT, CLOSE_OBJECT, LOCK_OBJECT, UNLOCK_OBJECT
    /// - Combat events: ON_DAMAGED, ON_DEATH, ON_ATTACKED
    /// - Script events: ON_HEARTBEAT, ON_DIALOGUE, ON_SPAWN
    /// - Custom events: Engine-specific event types
    /// </remarks>
    [PublicAPI]
    public abstract class BaseEventDispatcher
    {
        /// <summary>
        /// Dispatches an event to the appropriate handler.
        /// </summary>
        /// <param name="sourceEntity">The entity that triggered the event (may be null).</param>
        /// <param name="targetEntity">The entity that should receive the event.</param>
        /// <param name="eventType">The type of event being dispatched.</param>
        /// <param name="eventSubtype">Additional event subtype information.</param>
        /// <remarks>
        /// Common event dispatching pattern across all engines.
        /// Maps event IDs to string names and routes to appropriate handlers.
        /// Handles both area-wide events and entity-specific events.
        /// </remarks>
        public abstract void DispatchEvent(IEntity sourceEntity, IEntity targetEntity, int eventType, int eventSubtype);

        /// <summary>
        /// Gets the string name for an event type.
        /// </summary>
        /// <remarks>
        /// Used for debugging and logging event dispatching.
        /// Returns "Event(eventId)" for unknown event types.
        /// </remarks>
        protected abstract string GetEventName(int eventType);

        /// <summary>
        /// Gets the string name for an event subtype.
        /// </summary>
        /// <remarks>
        /// Used for script event subtypes (ON_HEARTBEAT, ON_PERCEPTION, etc.).
        /// Returns "EventType(subtypeId)" for unknown subtypes.
        /// </remarks>
        protected abstract string GetEventSubtypeName(int eventSubtype);

        /// <summary>
        /// Handles area transition events.
        /// </summary>
        /// <param name="entity">The entity being transitioned.</param>
        /// <param name="targetArea">The target area ResRef.</param>
        /// <param name="sourceEntity">Optional source entity (door/trigger) that triggered the transition. Used to resolve LinkedTo waypoint positioning.</param>
        /// <remarks>
        /// EVENT_AREA_TRANSITION (0x1a): Entity entering/leaving area.
        /// Updates area membership and triggers transition scripts.
        /// If sourceEntity is provided and has a LinkedTo field, positions entity at that waypoint.
        /// </remarks>
        protected abstract void HandleAreaTransition(IEntity entity, string targetArea, IEntity sourceEntity = null);

        /// <summary>
        /// Handles object manipulation events.
        /// </summary>
        /// <remarks>
        /// OPEN_OBJECT (7), CLOSE_OBJECT (6), LOCK_OBJECT (0xd), UNLOCK_OBJECT (0xc).
        /// Updates object state and triggers associated scripts.
        /// </remarks>
        protected abstract void HandleObjectEvent(IEntity entity, int eventType);

        /// <summary>
        /// Handles combat-related events.
        /// </summary>
        /// <param name="entity">The entity receiving the combat event.</param>
        /// <param name="eventType">The combat event type (EVENT_ON_MELEE_ATTACKED, EVENT_DESTROY_OBJECT, etc.).</param>
        /// <param name="sourceEntity">The entity that triggered the combat event (attacker, damager, etc.). May be null if not available.</param>
        /// <remarks>
        /// ON_DAMAGED (4), ON_DEATH (10), ON_ATTACKED (0xf), etc.
        /// Triggers combat scripts and AI behaviors.
        /// Common pattern across all engines: combat events fire appropriate script events.
        /// </remarks>
        protected abstract void HandleCombatEvent(IEntity entity, int eventType, IEntity sourceEntity = null);

        /// <summary>
        /// Handles script hook events.
        /// </summary>
        /// <param name="entity">The entity to execute the script event on.</param>
        /// <param name="eventType">The event type (typically EVENT_SIGNAL_EVENT = 10).</param>
        /// <param name="eventSubtype">The script event subtype (ON_HEARTBEAT, ON_PERCEPTION, etc.).</param>
        /// <param name="sourceEntity">The entity that triggered the event (optional, used as triggerer in script execution).</param>
        /// <remarks>
        /// ON_HEARTBEAT (0), ON_PERCEPTION (1), ON_SPELL_CAST_AT (2), etc.
        /// Executes entity-specific scripts based on event type.
        /// Source entity is passed as triggerer to script execution context.
        /// </remarks>
        protected abstract void HandleScriptEvent(IEntity entity, int eventType, int eventSubtype, IEntity sourceEntity = null);

        /// <summary>
        /// Queues an event for later processing.
        /// </summary>
        /// <remarks>
        /// Events may be queued for processing in the next frame or script execution cycle.
        /// Prevents recursive event dispatching and ensures proper execution order.
        /// </remarks>
        public abstract void QueueEvent(IEntity sourceEntity, IEntity targetEntity, int eventType, int eventSubtype);

        /// <summary>
        /// Processes queued events.
        /// </summary>
        /// <remarks>
        /// Called during script execution phase to process accumulated events.
        /// Ensures events are handled in the correct order and context.
        /// </remarks>
        public abstract void ProcessQueuedEvents();
    }
}
