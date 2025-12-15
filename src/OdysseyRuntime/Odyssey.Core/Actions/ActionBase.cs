using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Base class for all actions.
    /// </summary>
    /// <remarks>
    /// Action Base:
    /// - Based on swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc (action list GFF field), "ActionId" @ 0x007bebd0 (action ID field)
    /// - "ActionType" @ 0x007bf7f8 (action type field), "GroupActionId" @ 0x007bebc0 (group action ID field)
    /// - "ActionTimer" @ 0x007bf820 (action timer field), "SchedActionList" @ 0x007bf99c (scheduled action list field)
    /// - "ParryActions" @ 0x007bfa18 (parry actions field), "Action" @ 0x007c7150 (action field), "ACTION" @ 0x007cd138 (action constant)
    /// - Action parameter fields: "ActionParam1" @ 0x007c36c8, "ActionParam2" @ 0x007c36b8, "ActionParam3" @ 0x007c36a8
    /// - "ActionParam4" @ 0x007c3698, "ActionParam5" @ 0x007c3688 (action parameter fields)
    /// - "ActionParam1b" @ 0x007c3670, "ActionParam2b" @ 0x007c3660, "ActionParam3b" @ 0x007c3650
    /// - "ActionParam4b" @ 0x007c3640, "ActionParam5b" @ 0x007c3630 (action parameter boolean fields)
    /// - "ActionParamStrA" @ 0x007c3620, "ActionParamStrB" @ 0x007c3610 (action parameter string fields)
    /// - "ActionStrRef" @ 0x007d2e4c (action string reference field)
    /// - GUI: "Action Menu" @ 0x007c8480 (action menu), "CB_ACTIONMENU" @ 0x007d29d4 (action menu checkbox)
    /// - "LBL_A_ACTION" @ 0x007d090c (action label), "LBL_ACTIONDESC" @ 0x007cd280 (action description label)
    /// - "LBL_ACTIONDESCBG" @ 0x007cd26c (action description background label)
    /// - Animation: "i_noaction" @ 0x007c8704 (no action animation)
    /// - Debug: "(%d > %d) Scripts: (%s), Action: %d [Flags: %02x], OID: %08x, Tag: %s" @ 0x007c7650 (action debug output format)
    /// - Original implementation: FUN_00508260 @ 0x00508260 (load ActionList from GFF, parses ActionId, GroupActionId, NumParams, Paramaters)
    /// - FUN_00505bc0 @ 0x00505bc0 (save ActionList to GFF, writes ActionId, GroupActionId, NumParams, Paramaters array)
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector
    /// - Parameters stored as Type/Value pairs in GFF (ActionParam1-5 for numeric, ActionParamStrA/B for strings, ActionParam1b-5b for booleans)
    /// - Original implementation: Actions are executed by entities, return status (Complete, InProgress, Failed)
    /// - Actions update each frame until they complete or fail
    /// - Action types defined in ActionType enum (Move, Attack, UseObject, SpeakString, etc.)
    /// - Group IDs allow batching/clearing related actions together (GroupActionId field)
    /// - EVENT_FORCED_ACTION @ 0x007bccac (forced action event constant)
    /// </remarks>
    public abstract class ActionBase : IAction
    {
        protected float ElapsedTime;

        protected ActionBase(ActionType type)
        {
            Type = type;
            GroupId = -1;
        }

        public ActionType Type { get; }
        public int GroupId { get; set; }
        public IEntity Owner { get; set; }

        public ActionStatus Update(IEntity actor, float deltaTime)
        {
            ElapsedTime += deltaTime;
            return ExecuteInternal(actor, deltaTime);
        }

        protected abstract ActionStatus ExecuteInternal(IEntity actor, float deltaTime);

        public virtual void Dispose()
        {
            // Override in derived classes if cleanup is needed
        }
    }
}

