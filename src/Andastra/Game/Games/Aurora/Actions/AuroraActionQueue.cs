using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Actions;

namespace Andastra.Game.Games.Aurora.Actions
{
    /// <summary>
    /// Action queue implementation for Aurora engine (nwmain.exe, nwn2main.exe).
    /// </summary>
    /// <remarks>
    /// Aurora Action Queue Implementation:
    /// - Based on nwmain.exe and nwn2main.exe action system
    /// - Located via string references: "ActionList" @ 0x140df11e0 (nwmain.exe)
    /// - Original implementation: 
    ///   - CNWSObject::LoadActionQueue @ 0x1404963f0 (nwmain.exe) - loads ActionList from GFF
    ///   - CNWSObject::SaveActionQueue @ 0x140499910 (nwmain.exe) - saves ActionList to GFF
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector, 6=byte (Aurora-specific)
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc.
    /// - Action parameters stored in GFF structure as Type/Value pairs
    /// - GroupActionId: Allows batching/clearing related actions together
    /// - CNWSObject class structure: Actions stored in CExoLinkedList at offset +0x100
    /// - Instruction count tracking: Accumulates instruction count from script executions during action processing
    /// </remarks>
    public class AuroraActionQueue : BaseActionQueue
    {
        public AuroraActionQueue() : base()
        {
        }

        public AuroraActionQueue(IEntity owner) : base(owner)
        {
        }
    }
}

