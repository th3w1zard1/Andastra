namespace Odyssey.Core.Enums
{
    /// <summary>
    /// Script event types that can trigger NWScript execution.
    /// </summary>
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
        OnConversation,
        OnDisturbed,
        OnBlocked,
        OnEndRound,
        OnSpellCastAt,
        OnAttacked,
        OnRested,
        OnEndDialogue,

        // Door events
        OnOpen,
        OnClose,
        OnFailToOpen,
        OnClick,
        OnLock,
        OnUnlock,
        OnDisarm,
        OnTrapTriggered,

        // Placeable events
        OnUsed,

        // Trigger events

        // Encounter events
        OnExhausted,

        // Store events
        OnStoreOpen,
        OnStoreClose
    }
}

