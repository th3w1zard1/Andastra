using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Aurora.Actions;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Aurora.Components
{
    /// <summary>
    /// Component that wraps an AuroraActionQueue for entity action management in Aurora engine.
    /// </summary>
    /// <remarks>
    /// Aurora Action Queue Component:
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
    /// - Wraps AuroraActionQueue class to provide IActionQueueComponent interface
    /// </remarks>
    public class AuroraActionQueueComponent : IActionQueueComponent
    {
        private readonly AuroraActionQueue _actionQueue;
        private IEntity _owner;

        public AuroraActionQueueComponent()
        {
            _actionQueue = new AuroraActionQueue();
        }

        public IEntity Owner
        {
            get { return _owner; }
            set
            {
                _owner = value;
                _actionQueue.Owner = value;
            }
        }

        public void OnAttach()
        {
            _actionQueue.OnAttach();
        }

        public void OnDetach()
        {
            _actionQueue.OnDetach();
        }

        public IAction CurrentAction
        {
            get { return _actionQueue.Current; }
        }

        public int Count
        {
            get { return _actionQueue.Count; }
        }

        public void Add(IAction action)
        {
            _actionQueue.Add(action);
        }

        public void Clear()
        {
            _actionQueue.Clear();
        }

        public void Update(IEntity entity, float deltaTime)
        {
            // Process action queue (updates current action and dequeues when complete)
            // Based on nwmain.exe: CNWSObject::UpdateActionQueue processes actions each frame
            _actionQueue.Process(deltaTime);
        }

        public int GetLastInstructionCount()
        {
            return _actionQueue.GetLastInstructionCount();
        }

        public void ResetInstructionCount()
        {
            _actionQueue.ResetInstructionCount();
        }

        public void AddInstructionCount(int count)
        {
            _actionQueue.AddInstructionCount(count);
        }
    }
}

