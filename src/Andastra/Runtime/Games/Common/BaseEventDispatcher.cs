using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;

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
    /// Based on reverse engineering of:
    /// - swkotor.exe: Event dispatching functions
    /// - swkotor2.exe: DispatchEvent @ 0x004dcfb0 with comprehensive event mapping
    /// - nwmain.exe: Aurora event system
    /// - daorigins.exe: Eclipse event system with additional events
    /// - DragonAge2.exe/MassEffect.exe/MassEffect2.exe: Enhanced event systems
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
        /// Based on DispatchEvent @ 0x004dcfb0 in swkotor2.exe and similar functions in other engines.
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
        /// <remarks>
        /// EVENT_AREA_TRANSITION (0x1a): Entity entering/leaving area.
        /// Updates area membership and triggers transition scripts.
        /// </remarks>
        protected abstract void HandleAreaTransition(IEntity entity, string targetArea);

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
        /// <remarks>
        /// ON_DAMAGED (4), ON_DEATH (10), ON_ATTACKED (0xf), etc.
        /// Triggers combat scripts and AI behaviors.
        /// </remarks>
        protected abstract void HandleCombatEvent(IEntity entity, int eventType);

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
