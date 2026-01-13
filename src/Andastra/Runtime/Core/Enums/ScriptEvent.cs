namespace Andastra.Runtime.Core.Enums
{
    /// <summary>
    /// Script event types that can trigger NWScript execution.
    /// </summary>
    /// <remarks>
    /// Script Event Types:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) script event system
    /// - Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_*" constants throughout executable
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles script event dispatching (switch on event type)
    /// - Original implementation: Script events trigger NWScript execution when game events occur
    /// - Events stored in GFF structures as script ResRef fields (e.g., ScriptHeartbeat @ 0x007beeb0, ScriptOnNotice @ 0x007beea0)
    /// - Module events:
    ///   - OnModuleLoad: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c (0x14)
    ///   - OnModuleStart: "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948 (0x15)
    ///   - OnClientEnter: "Mod_OnClientEntrance" @ 0x007be718
    /// - Creature events:
    ///   - OnSpawn: "CSWSSCRIPTEVENT_EVENTTYPE_ON_SPAWN_IN" @ 0x007bc7d0 (0x8), "ScriptSpawn" @ 0x007bee30
    ///   - OnDeath: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DEATH" @ 0x007bc7e4 (0xa), "ScriptDeath" @ 0x007bee20
    ///   - OnDamaged: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DAMAGED" @ 0x007bcb14 (0x4), "ScriptDamaged" @ 0x007bee70
    ///   - OnHeartbeat: "CSWSSCRIPTEVENT_EVENTTYPE_ON_HEARTBEAT" @ 0x007bc9a4 (0x0), "ScriptHeartbeat" @ 0x007beeb0
    ///   - OnPerception: "CSWSSCRIPTEVENT_EVENTTYPE_ON_PERCEPTION" @ 0x007bcb68 (0x1), "ScriptOnNotice" @ 0x007beea0
    ///   - OnAttacked: "ScriptAttacked" @ 0x007bee80
    ///   - OnRested: "CSWSSCRIPTEVENT_EVENTTYPE_ON_RESTED" @ 0x007bc7c0 (0x9)
    ///   - OnEndDialogue: "ScriptEndDialogue" @ 0x007bede0
    /// - Door events:
    ///   - OnOpen: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OPEN" @ 0x007bc7f8 (0x16), "ScriptOnOpen" @ 0x007c1a54
    ///   - OnClose: "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLOSE" @ 0x007bc80c (0x17), "ScriptOnClose" @ 0x007c1a8c
    ///   - OnLock: "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOCKED" @ 0x007bc850 (0x1c)
    ///   - OnUnlock: "CSWSSCRIPTEVENT_EVENTTYPE_ON_UNLOCKED" @ 0x007bc864 (0x1d)
    ///   - OnFailToOpen: "CSWSSCRIPTEVENT_EVENTTYPE_ON_FAIL_TO_OPEN" @ 0x007bc888 (0x22)
    ///   - OnDisarm: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DISARM" @ 0x007bc7e8 (0x18)
    /// - Placeable events:
    ///   - OnUsed: "CSWSSCRIPTEVENT_EVENTTYPE_ON_USED" @ 0x007bc7fc (0x19), "ScriptOnUsed" @ 0x007beeb8
    ///   - OnClick: "CSWSSCRIPTEVENT_EVENTTYPE_ON_CLICKED" @ 0x007bc9e0 (0x1e), "ScriptOnClick" @ 0x007c1a20
    /// - Trigger events:
    ///   - OnEnter: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_ENTER" @ 0x007bc9b8 (0xc), "ScriptOnEnter" @ 0x007c1d40
    ///   - OnExit: "CSWSSCRIPTEVENT_EVENTTYPE_ON_OBJECT_EXIT" @ 0x007bc9cc (0xd), "ScriptOnExit" @ 0x007c1d30
    /// - Dialogue events:
    ///   - OnConversation: "CSWSSCRIPTEVENT_EVENTTYPE_ON_DIALOGUE" @ 0x007bcac4 (0x7), "ScriptDialogue" @ 0x007bee40
    /// - Item events:
    ///   - OnAcquireItem: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACQUIRE_ITEM" @ 0x007bc7b0 (0x13)
    ///   - OnLoseItem: "CSWSSCRIPTEVENT_EVENTTYPE_ON_LOSE_ITEM" @ 0x007bc7c4 (0x14)
    ///   - OnActivateItem: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ACTIVATE_ITEM" @ 0x007bc7a8 (0x12)
    ///   - OnInventoryDisturbed: "CSWSSCRIPTEVENT_EVENTTYPE_ON_INVENTORY_DISTURBED" @ 0x007bc778 (0x1b)
    /// - Encounter events:
    ///   - OnExhausted: "CSWSSCRIPTEVENT_EVENTTYPE_ON_ENCOUNTER_EXHAUSTED" @ 0x007bc7d4 (0x15)
    /// - User-defined events:
    ///   - OnUserDefined: "CSWSSCRIPTEVENT_EVENTTYPE_ON_USER_DEFINED_EVENT" @ 0x007bc7dc (0xb)
    /// - Script hooks component stores script ResRefs mapped to these event types
    /// - Event execution: ScriptExecutor executes NCS bytecode with entity as caller (OBJECT_SELF), triggerer as parameter
    /// </remarks>
    public enum ScriptEvent
    {
        // Module events
        OnModuleLoad,
        OnModuleStart,
        OnModuleLeave,
        OnModuleHeartbeat,
        OnClientEnter,
        OnClientLeave,
        OnPlayerDying,
        OnPlayerDeath,
        OnPlayerRespawn,
        OnPlayerLevelUp,
        OnPlayerRest,
        OnAcquireItem,
        OnUnacquireItem,
        OnActivateItem,
        OnUserDefined,
        OnSpawnButtonDown,

        // Area events
        OnEnter,
        OnExit,
        OnAreaHeartbeat,

        // Creature events
        OnSpawn,
        OnDeath,
        OnDamaged,
        OnHeartbeat,
        OnPerception,
        OnNotice, // Aurora alias for OnPerception (maps to ScriptOnNotice)
        OnCombatRoundEnd,
        OnEndCombatRound,
        OnConversation,
        OnDialogue, // Aurora alias for OnConversation (maps to ScriptDialogue)
        OnDisturbed,
        OnBlocked,
        OnEndRound,
        OnSpellCastAt,
        OnSpellAt, // Aurora alias for OnSpellCastAt (maps to ScriptSpellAt)
        OnAttacked,
        OnPhysicalAttacked,
        OnRested,
        OnEndDialogue,

        // Door events
        OnOpen,
        OnClose,
        OnClosed,
        OnFailToOpen,
        OnDoorClick,
        OnClick,
        OnLock,
        OnUnlock,
        OnDisarm,
        OnTrapTriggered,

        // Placeable events
        OnUsed,

        // Trigger events

        // Encounter events
        OnEncounterEntered,
        OnExhausted,

        // Store events
        OnStoreOpen,
        OnOpenStore,
        OnStoreClose
    }
}

