namespace Andastra.Runtime.Core.Enums
{
    /// <summary>
    /// Types of actions that can be queued on entities.
    /// </summary>
    /// <remarks>
    /// Action Type Enum:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) action system
    /// - Located via string references: "ActionType" @ 0x007bf7f8, "ActionList" @ 0x007bebdc
    /// - Original implementation: Action types stored in GFF ActionList structures
    /// - Action types correspond to NWScript action functions (ActionMoveToLocation, ActionAttack, etc.)
    /// - Actions stored with parameters (ActionParam1-5, ActionParamStrA/B) for execution
    /// - 0x00508260 @ 0x00508260 (load ActionList), 0x00505bc0 @ 0x00505bc0 (save ActionList)
    /// </remarks>
    public enum ActionType
    {
        Invalid = 0,
        MoveToPoint = 1,
        MoveToObject = 2,
        AttackObject = 3,
        CastSpellAtObject = 4,
        CastSpellAtLocation = 5,
        OpenDoor = 6,
        CloseDoor = 7,
        UseObject = 8,
        Wait = 9,
        PlayAnimation = 10,
        SpeakString = 11,
        JumpToLocation = 12,
        JumpToObject = 13,
        DoCommand = 14,
        EquipItem = 15,
        UnequipItem = 16,
        GiveItem = 17,
        TakeItem = 18,
        StartConversation = 19,
        PauseConversation = 20,
        ResumeConversation = 21,
        FollowLeader = 22,
        FollowOwner = 23,
        RandomWalk = 24,
        InteractObject = 25,
        DestroyObject = 26,
        UseItem = 27
    }
}

