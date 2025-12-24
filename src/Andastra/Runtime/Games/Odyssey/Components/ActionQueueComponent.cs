using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Odyssey.Components
{
    /// <summary>
    /// Component that wraps an ActionQueue for entity action management.
    /// </summary>
    /// <remarks>
    /// Action Queue Component:
    /// - Based on swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc, "ActionId" @ 0x007bebd0
    /// - Original implementation: Entities maintain action queue with current action and pending actions
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Wraps ActionQueue class to provide IActionQueueComponent interface
    /// </remarks>
    public class ActionQueueComponent : IActionQueueComponent
    {
        private readonly ActionQueue _actionQueue;
        private IEntity _owner;

        public ActionQueueComponent()
        {
            _actionQueue = new ActionQueue();
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

