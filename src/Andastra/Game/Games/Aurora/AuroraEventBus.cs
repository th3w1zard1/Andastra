using System;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora
{
    /// <summary>
    /// Aurora Engine event bus implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Event Bus System:
    /// - Based on nwmain.exe (Neverwinter Nights) and nwn2main.exe (Neverwinter Nights 2) event systems
    /// - Aurora engine uses similar event dispatching patterns to Odyssey but with Aurora-specific event types
    /// - Event system supports NWScript event hooks similar to Odyssey's script events
    /// - Common event types: OnHeartbeat, OnPerception, OnDamaged, OnDeath, OnDialogue, etc.
    /// - Event routing: Events are queued and dispatched at frame boundaries
    /// - Script execution: FireScriptEvent triggers NWScript execution on entities with matching event hooks
    /// - Inheritance: Inherits from BaseEventBus (Andastra.Runtime.Games.Common) with Aurora-specific event handling
    ///
    /// Reverse Engineered Function Addresses (nwmain.exe):
    /// - CServerAIMaster::AddEventDeltaTime @ 0x1405570b0: Main function for adding events to the event queue
    ///   - Signature: int __thiscall AddEventDeltaTime(CServerAIMaster *this, uint param_1, uint param_2, uint param_3, uint param_4, uint param_5, void *param_6)
    ///   - param_1: Days component of delta time
    ///   - param_2: Milliseconds component of delta time
    ///   - param_3: Event ID (CallerId)
    ///   - param_4: Object ID (ObjectId)
    ///   - param_5: Event type identifier
    ///   - param_6: Event data pointer (CScriptEvent* for script events)
    ///   - Returns: 1 on success, 0 on failure
    ///   - Implementation: Adds event to sorted linked list based on world time, handles pause state logging
    ///   - Event queue: CExoLinkedList at offset 0x80 in CServerAIMaster
    ///   - Located via string reference: "EventQueue" @ 0x140dfc4c0, "EventId" @ 0x140dfc578, "EventData" @ 0x140dfc580
    ///   - Debug output: "Event added while paused:  EventId: %d   CallerId: %x    ObjectId: %x\n" @ 0x140dfc460
    ///
    /// - CNWSArea::EventHandler @ 0x14035cf50: Handles events for area objects
    ///   - Signature: void __thiscall EventHandler(CNWSArea *this, uint param_1, uint param_2, void *param_3, uint param_4, uint param_5)
    ///   - param_1: Event type constant (DAT_140dfc148, DAT_140dfc170, DAT_140dfc184, DAT_140dfc1b4)
    ///   - param_2: Caller ID (triggering entity)
    ///   - param_3: Event data pointer (CScriptEvent* for script events)
    ///   - param_4: Object ID
    ///   - param_5: Additional parameter
    ///   - Implementation: Routes events to appropriate handlers, triggers script execution via RunEventScript
    ///   - Script events: Checks CScriptEvent type (short at param_3 offset 0), routes to OnHeartbeat (0), OnUserDefined (0xb), OnPlayerEnter (0xc), OnPlayerExit (0xd)
    ///   - Located via string reference: "EventHandler" @ 0x140ddb0c0
    ///
    /// - CNWSCreature::EventHandler @ 0x14038d7d0: Handles events for creature objects
    ///   - Signature: void __thiscall EventHandler(CNWSCreature *this, uint param_1, uint param_2, void *param_3, uint param_4, uint param_5)
    ///   - Implementation: Routes creature-specific events (combat, perception, damage, death, etc.)
    ///   - Script events: Routes to OnHeartbeat, OnPerception, OnDamaged, OnDeath, OnDialogue, etc.
    ///   - Located via string reference: "EventHandler" @ 0x140ddb0c0
    ///
    /// - CNWSDoor::EventHandler @ 0x14041e760: Handles events for door objects
    ///   - Signature: void __thiscall EventHandler(CNWSDoor *this, uint param_1, uint param_2, void *param_3, uint param_4, uint param_5)
    ///   - Implementation: Routes door-specific events (open, close, lock, unlock, etc.)
    ///   - Script events: Routes to OnOpen, OnClose, OnLock, OnUnlock, OnFailToOpen, OnDisarm
    ///   - Located via string reference: "OnHeartbeat" @ 0x140ddb2b8, "OnDeath" @ 0x140dc9738, "OnDamaged" @ 0x140de76b0
    ///
    /// - CNWSPlaceable::EventHandler @ 0x1404b2310: Handles events for placeable objects
    ///   - Signature: void __thiscall EventHandler(CNWSPlaceable *this, uint param_1, uint param_2, void *param_3, uint param_4, uint param_5)
    ///   - Implementation: Routes placeable-specific events (use, click, etc.)
    ///   - Script events: Routes to OnUsed, OnClick, OnHeartbeat, OnDeath, OnDamaged
    ///
    /// - CNWSItem::EventHandler @ 0x140466f70: Handles events for item objects
    ///   - Signature: void __thiscall EventHandler(CNWSItem *this, uint param_1, uint param_2, void *param_3, uint param_4, uint param_5)
    ///   - Implementation: Routes item-specific events (acquire, lose, activate, etc.)
    ///   - Script events: Routes to OnAcquireItem, OnLoseItem, OnActivateItem, OnInventoryDisturbed
    ///
    /// - CNWSModule::EventHandler @ 0x14047c0b0: Handles events for module objects
    ///   - Signature: void __thiscall EventHandler(CNWSModule *this, uint param_1, uint param_2, void *param_3, uint param_4, uint param_5)
    ///   - Implementation: Routes module-level events (load, start, heartbeat, etc.)
    ///   - Script events: Routes to OnModuleLoad, OnModuleStart, Mod_OnHeartbeat
    ///   - Located via string reference: "Mod_OnHeartbeat" @ 0x140defa98
    ///
    /// - CNWSArea::RunEventScript @ 0x140364ec0: Executes event scripts on areas
    ///   - Signature: int __thiscall RunEventScript(CNWSArea *this, int param_1, CExoString *param_2)
    ///   - param_1: Event script index (0-3 for OnHeartbeat, OnUserDefined, OnPlayerEnter, OnPlayerExit)
    ///   - param_2: Optional script ResRef override (null to use stored script)
    ///   - Implementation: Retrieves script ResRef from area object, calls CVirtualMachine::RunScript
    ///   - Script storage: CExoString at offsets 0x1d8, 0x1e8, 0x1f8, 0x200 in CNWSArea
    ///   - Located via string reference: "ScriptEventID" @ 0x140dc1580
    ///
    /// - CServerAIMaster::UpdateState @ 0x140559cf0: Processes queued events each frame
    ///   - Implementation: Iterates through event queue, dispatches events to appropriate object EventHandler methods
    ///   - Event processing: Calls GetGameObject to resolve object IDs, then calls EventHandler on the object
    ///   - Frame boundary: Called during main game loop update cycle
    ///
    /// Event Constants (nwmain.exe):
    /// - DAT_140dfc170: Script event type constant (CScriptEvent events)
    /// - DAT_140dfc148: Area script situation event type
    /// - DAT_140dfc184: Area add placeable event type
    /// - DAT_140dfc1b4: Area destroy event type
    ///
    /// CScriptEvent Structure:
    /// - Offset 0x0: short eventType (0 = OnHeartbeat, 0xb = OnUserDefined, 0xc = OnPlayerEnter, 0xd = OnPlayerExit)
    /// - Methods: GetInteger, GetFloat, GetString, GetObjectID, SetInteger, SetFloat, SetString, SetObjectID
    /// - Located via string references: "CScriptEvent" mangled names, "LoadEvent" @ 0x1404c6ba0, "SaveEvent" @ 0x1404c7070
    ///
    /// Event Queue Structure:
    /// - CExoLinkedList at CServerAIMaster offset 0x80
    /// - Event nodes contain: world time (days, milliseconds), event ID, object ID, event type, event data pointer
    /// - Events sorted by world time for proper sequencing
    /// - Located via string reference: "EventQueue" @ 0x140dfc4c0
    ///
    /// Script Event Hooks:
    /// - Entities store script ResRefs for event hooks (OnHeartbeat, OnPerception, OnDamaged, OnDeath, etc.)
    /// - Script execution: CVirtualMachine::RunScript called with entity as OBJECT_SELF, triggerer as parameter
    /// - Event routing: EventHandler methods check for script hooks and execute scripts if present
    /// - Located via string references: "OnHeartbeat" @ 0x140ddb2b8, "OnDeath" @ 0x140dc9738, "OnDamaged" @ 0x140de76b0
    ///
    /// Event Processing Flow:
    /// 1. AddEventDeltaTime called to queue event with delta time
    /// 2. Event added to sorted linked list based on world time
    /// 3. UpdateState called each frame to process queued events
    /// 4. Events dispatched to appropriate object EventHandler methods
    /// 5. EventHandler routes to script execution if entity has matching event hook
    /// 6. CVirtualMachine::RunScript executes NCS bytecode with entity as caller
    ///
    /// Differences from Odyssey:
    /// - Aurora uses CServerAIMaster for event queue management (Odyssey uses direct event dispatching)
    /// - Aurora events include world time components (days, milliseconds) for precise scheduling
    /// - Aurora EventHandler methods are per-object-type (CNWSArea, CNWSCreature, etc.) rather than centralized
    /// - Aurora uses CScriptEvent structure for script event data (Odyssey uses simpler event type constants)
    /// </remarks>
    [PublicAPI]
    public class AuroraEventBus : BaseEventBus
    {
        /// <summary>
        /// Initializes a new instance of the AuroraEventBus class.
        /// </summary>
        /// <remarks>
        /// Initializes Aurora-specific event bus.
        /// Event system uses CServerAIMaster event queue architecture.
        /// Based on nwmain.exe: CServerAIMaster event queue initialization
        /// </remarks>
        public AuroraEventBus()
        {
        }

        /// <summary>
        /// Fires a script event on an entity.
        /// </summary>
        /// <param name="entity">The entity to fire the event on.</param>
        /// <param name="eventType">The script event type.</param>
        /// <param name="triggerer">The triggering entity (optional).</param>
        /// <remarks>
        /// Based on nwmain.exe: Aurora script event firing implementation
        /// Event dispatching: CServerAIMaster::AddEventDeltaTime @ 0x1405570b0 (nwmain.exe) handles event queueing
        /// Located via string references: "EventQueue" @ 0x140dfc4c0, "EventId" @ 0x140dfc578, "EventData" @ 0x140dfc580
        /// Original implementation: AddEventDeltaTime queues events with delta time (days, milliseconds) for precise scheduling
        /// Events are processed at frame boundaries via CServerAIMaster::UpdateState @ 0x140559cf0
        /// Event routing: Events dispatched to appropriate object EventHandler methods (CNWSArea::EventHandler @ 0x14035cf50, etc.)
        /// Script events trigger NWScript execution on entities with matching event hooks (OnHeartbeat, OnPerception, OnDamaged, etc.)
        /// Events are queued ("EventQueue" @ 0x140dfc4c0) and dispatched each frame to prevent re-entrancy
        /// Event structure: Events contain Event ID (CallerId), Object ID (ObjectId), Event type, Event data (CScriptEvent*)
        /// Debug output: "Event added while paused:  EventId: %d   CallerId: %x    ObjectId: %x\n" @ 0x140dfc460 logs event firing
        /// Script execution: EventHandler methods check for script hooks and execute scripts via CVirtualMachine::RunScript
        /// Event data: CScriptEvent structure contains event type (short at offset 0x0) and event parameters
        /// </remarks>
        public override void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer = null)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            // Create script event args and queue for processing
            // Based on nwmain.exe: CServerAIMaster::AddEventDeltaTime @ 0x1405570b0 queues events with delta time
            // Events are queued and processed at frame boundary via CServerAIMaster::UpdateState @ 0x140559cf0
            // Event routing: Events dispatched to appropriate object EventHandler methods
            // Script events trigger NWScript execution on entities with matching event hooks
            var evt = new ScriptEventArgs(entity, eventType, triggerer);
            ((IEventBus)this).QueueEvent(evt);
        }

        /// <summary>
        /// Event args for script events.
        /// </summary>
        /// <remarks>
        /// Aurora-specific script event arguments structure.
        /// Based on nwmain.exe CScriptEvent structure and event data format.
        /// CScriptEvent structure: Contains event type (short at offset 0x0) and event parameters
        /// Event types: 0 = OnHeartbeat, 0xb = OnUserDefined, 0xc = OnPlayerEnter, 0xd = OnPlayerExit
        /// Located via string references: "CScriptEvent" mangled names, "LoadEvent" @ 0x1404c6ba0, "SaveEvent" @ 0x1404c7070
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

