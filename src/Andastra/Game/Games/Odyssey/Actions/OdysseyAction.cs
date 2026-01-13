using Andastra.Runtime.Core.Enums;
using Andastra.Game.Games.Common.Actions;

namespace Andastra.Game.Games.Odyssey.Actions
{
    /// <summary>
    /// Base class for Odyssey engine actions (swkotor.exe, swkotor2.exe).
    /// </summary>
    /// <remarks>
    /// Odyssey Action Implementation:
    /// - Based on swkotor.exe and swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc (swkotor2.exe), "ActionList" @ 0x00745ea0 (swkotor.exe)
    /// - Original implementation: 
    ///   - swkotor2.exe: FUN_00508260 @ 0x00508260 (load ActionList from GFF), FUN_00505bc0 @ 0x00505bc0 (save ActionList to GFF)
    ///   - swkotor.exe: FUN_004cecb0 @ 0x004cecb0 (load ActionList from GFF), FUN_004cc7e0 @ 0x004cc7e0 (save ActionList to GFF)
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector
    /// - Parameters stored as Type/Value pairs in GFF (ActionParam1-5 for numeric, ActionParamStrA/B for strings, ActionParam1b-5b for booleans)
    /// - Actions are executed by entities, return status (Complete, InProgress, Failed)
    /// - Actions update each frame until they complete or fail
    /// - Action types defined in ActionType enum (Move, Attack, UseObject, SpeakString, etc.)
    /// - Group IDs allow batching/clearing related actions together
    /// - EVENT_FORCED_ACTION @ 0x007bccac (swkotor2.exe), @ 0x00744a74 (swkotor.exe) (forced action event constant)
    /// </remarks>
    public abstract class OdysseyAction : BaseAction
    {
        protected OdysseyAction(ActionType type) : base(type)
        {
        }
    }
}

