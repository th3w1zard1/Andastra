using System;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Eclipse
{
    /// <summary>
    /// Eclipse Engine event bus implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Event Bus System:
    /// - Based on daorigins.exe (Dragon Age: Origins), DragonAge2.exe (Dragon Age 2),
    ///    (), and  ( 2) event systems
    /// - Eclipse engine uses UnrealScript-based event dispatching (different from Odyssey's NWScript)
    /// - Event system architecture:
    ///   - Event data structures: "EventListeners" @ 0x00ae8194 (daorigins.exe), 0x00bf543c (DragonAge2.exe)
    ///   - Event identifiers: "EventId" @ 0x00ae81a4 (daorigins.exe), 0x00bf544c (DragonAge2.exe)
    ///   - Event scripts: "EventScripts" @ 0x00ae81bc (daorigins.exe), 0x00bf5464 (DragonAge2.exe)
    ///   - Enabled events: "EnabledEvents" @ 0x00ae81ac (daorigins.exe), 0x00bf5454 (DragonAge2.exe)
    ///   - Event list: "EventList" @ 0x00aedb74 (daorigins.exe), 0x00c01250 (DragonAge2.exe)
    /// - Command-based event system: "COMMAND_SIGNALEVENT" @ 0x00af4180 (daorigins.exe) processes event commands
    ///   - Command processing: Events are dispatched through UnrealScript command system
    ///   - Event commands: COMMAND_HANDLEEVENT, COMMAND_SETEVENTSCRIPT, COMMAND_ENABLEEVENT, etc.
    /// - UnrealScript event dispatcher: Uses BioEventDispatcher interface
    ///   - Function names (UnrealScript): "intUBioEventDispatcherexecDispatch" (: 0x117e7b90)
    ///   - Function names (UnrealScript): "intUBioEventDispatcherexecSubscribe" (: 0x117e7c28)
    ///   - Function names (UnrealScript): "intUBioEventDispatcherexecUnsubscribe" (: 0x117e7bd8)
    ///   - Note: These are UnrealScript interface functions, not direct C++ addresses
    /// - Event processing pattern:
    ///   1. Events are queued via QueueEvent (inherited from BaseEventBus)
    ///   2. Events are processed at frame boundaries via DispatchQueuedEvents
    ///   3. Event listeners are notified through UnrealScript event dispatcher system
    ///   4. Script execution triggered on entities with matching event hooks
    /// - Common event types: OnHeartbeat, OnPerception, OnDamaged, OnDeath, OnDialogue, etc.
    /// - Event routing: Events are queued and dispatched at frame boundaries (same pattern as Odyssey/Aurora)
    /// - Script execution: FireScriptEvent triggers UnrealScript execution on entities with matching event hooks
    /// - Inheritance: Inherits from BaseEventBus (Runtime.Games.Common) with Eclipse-specific event handling
    /// - Differences from Odyssey/Aurora:
    ///   - Uses UnrealScript instead of NWScript for script execution
    ///   - Event system integrated with UnrealScript's native event dispatcher
    ///   - Command-based event processing through COMMAND_SIGNALEVENT
    ///   - Event data stored in UnrealScript object properties (EventListeners, EventScripts, etc.)
    /// </remarks>
    [PublicAPI]
    public class EclipseEventBus : BaseEventBus
    {
        /// <summary>
        /// Initializes a new instance of the EclipseEventBus class.
        /// </summary>
        /// <remarks>
        /// Initializes Eclipse-specific event bus.
        /// Event system uses UnrealScript event dispatcher architecture.
        /// </remarks>
        public EclipseEventBus()
        {
        }

        /// <summary>
        /// Fires a script event on an entity.
        /// </summary>
        /// <param name="entity">The entity to fire the event on.</param>
        /// <param name="eventType">The script event type.</param>
        /// <param name="triggerer">The triggering entity (optional).</param>
        /// <remarks>
        /// Eclipse Script Event Firing Implementation:
        /// - Based on daorigins.exe/DragonAge2.exe// event systems
        /// - Event processing flow:
        ///   1. Event is created with entity, event type, and triggerer
        ///   2. Event is queued via QueueEvent (inherited from BaseEventBus)
        ///   3. Event is processed at frame boundary via DispatchQueuedEvents
        ///   4. UnrealScript event dispatcher notifies registered listeners
        ///   5. Script execution triggered on entities with matching event hooks
        /// - Event data structures (from Ghidra reverse engineering):
        ///   - daorigins.exe: "EventListeners" @ 0x00ae8194, "EventId" @ 0x00ae81a4, "EventScripts" @ 0x00ae81bc
        ///   - DragonAge2.exe: "EventListeners" @ 0x00bf543c, "EventId" @ 0x00bf544c, "EventScripts" @ 0x00bf5464
        ///   - : Uses UnrealScript BioEventDispatcher interface
        ///   - : Uses UnrealScript BioEventDispatcher interface
        /// - Command processing: "COMMAND_SIGNALEVENT" @ 0x00af4180 (daorigins.exe) handles event commands
        /// - UnrealScript integration: Events routed through UnrealScript's native event dispatcher system
        /// - Event queuing: Events are queued and processed at frame boundaries to prevent re-entrancy
        /// - Script execution: Script events trigger UnrealScript execution on entities with matching event hooks
        /// - Event types: Supports all ScriptEvent enum values (OnHeartbeat, OnPerception, OnDamaged, OnDeath, etc.)
        /// - Inheritance: Uses BaseEventBus.QueueEvent for event queuing, BaseEventBus.DispatchQueuedEvents for processing
        /// </remarks>
        public override void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer = null)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            // Create script event args and queue for processing
            // Based on Eclipse engines: Events are queued and processed at frame boundary
            // Event queuing pattern matches Odyssey/Aurora but uses UnrealScript for execution
            var evt = new ScriptEventArgs(entity, eventType, triggerer);
            ((IEventBus)this).QueueEvent(evt);
        }

        /// <summary>
        /// Event args for script events.
        /// </summary>
        /// <remarks>
        /// Eclipse-specific script event arguments structure.
        /// Based on daorigins.exe/DragonAge2.exe// event data structure.
        /// Event structure contains:
        /// - Entity: Target entity for the event (OBJECT_SELF equivalent)
        /// - EventType: Script event type (OnHeartbeat, OnPerception, etc.)
        /// - Triggerer: Entity that triggered the event (optional)
        /// Event data is passed to UnrealScript event dispatcher for processing.
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
}

