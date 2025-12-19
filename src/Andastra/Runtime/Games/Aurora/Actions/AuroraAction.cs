using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Games.Common.Actions;

namespace Andastra.Runtime.Games.Aurora.Actions
{
    /// <summary>
    /// Base class for Aurora engine actions (nwmain.exe, nwn2main.exe).
    /// </summary>
    /// <remarks>
    /// Aurora Action Implementation:
    /// - Based on nwmain.exe and nwn2main.exe action system
    /// - Located via string references: "ActionList" @ 0x140df11e0 (nwmain.exe)
    /// - Original implementation: 
    ///   - CNWSObject::LoadActionQueue @ 0x1404963f0 (nwmain.exe) - loads ActionList from GFF
    ///   - CNWSObject::SaveActionQueue @ 0x140499910 (nwmain.exe) - saves ActionList to GFF
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector, 6=byte (Aurora-specific)
    /// - Parameters stored as Type/Value pairs in GFF structure
    /// - Actions are executed by entities, return status (Complete, InProgress, Failed)
    /// - Actions update each frame until they complete or fail
    /// - Action types defined in ActionType enum (Move, Attack, UseObject, SpeakString, etc.)
    /// - Group IDs allow batching/clearing related actions together
    /// - CNWSObject class structure: Actions stored in CExoLinkedList at offset +0x100
    /// </remarks>
    public abstract class AuroraAction : BaseAction
    {
        protected AuroraAction(ActionType type) : base(type)
        {
        }
    }
}

