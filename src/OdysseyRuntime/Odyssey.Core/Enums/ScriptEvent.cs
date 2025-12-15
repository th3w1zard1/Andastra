namespace Odyssey.Core.Enums
{
    /// <summary>
    /// Script event types that can trigger NWScript execution.
    /// </summary>
    /// <remarks>
    /// Script Event Types:
    /// - Based on swkotor2.exe script event system
    /// - Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_*" @ 0x007bc594+
    /// - Original implementation: Script events trigger NWScript execution when game events occur
    /// - Events stored in GFF structures as script ResRef fields (e.g., ScriptHeartbeat, ScriptOnNotice)
    /// - Module events: OnModuleLoad, OnModuleStart, OnClientEnter, etc.
    /// - Creature events: OnSpawn, OnDeath, OnDamaged, OnHeartbeat, OnPerception, OnAttacked, etc.
    /// - Door events: OnOpen, OnClose, OnLock, OnUnlock, etc.
    /// - Placeable events: OnUsed, OnDamaged, OnDeath, etc.
    /// - Trigger events: OnEnter, OnExit
    /// - Script hooks component stores script ResRefs mapped to these event types
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
        OnCombatRoundEnd,
        OnEndCombatRound,
        OnConversation,
        OnDisturbed,
        OnBlocked,
        OnEndRound,
        OnSpellCastAt,
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
        OnStoreClose
    }
}

