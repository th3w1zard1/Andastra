using Andastra.Runtime.Core.Interfaces;
using Andastra.Game.Games.Common.Actions;

namespace Andastra.Game.Games.Odyssey.Actions
{
    /// <summary>
    /// Action queue implementation for Odyssey engine (swkotor.exe, swkotor2.exe).
    /// </summary>
    /// <remarks>
    /// Odyssey Action Queue Implementation:
    /// - Based on swkotor.exe and swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc (swkotor2.exe), "ActionList" @ 0x00745ea0 (swkotor.exe)
    /// - Original implementation: 
    ///   - swkotor2.exe: 0x00508260 @ 0x00508260 (load ActionList from GFF), 0x00505bc0 @ 0x00505bc0 (save ActionList to GFF)
    ///   - swkotor.exe: 0x004cecb0 @ 0x004cecb0 (load ActionList from GFF), 0x004cc7e0 @ 0x004cc7e0 (save ActionList to GFF)
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc.
    /// - Action parameters stored in ActionParam1-5, ActionParamStrA/B fields
    /// - GroupActionId: Allows batching/clearing related actions together
    /// - Instruction count tracking: Accumulates instruction count from script executions during action processing
    /// </remarks>
    public class OdysseyActionQueue : BaseActionQueue
    {
        public OdysseyActionQueue() : base()
        {
        }

        public OdysseyActionQueue(IEntity owner) : base(owner)
        {
        }
    }
}

