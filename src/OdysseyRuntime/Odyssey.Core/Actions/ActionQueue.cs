using System.Collections.Generic;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// FIFO action queue for entity actions.
    /// </summary>
    /// <remarks>
    /// Action Queue System:
    /// - Based on swkotor2.exe action system
    /// - Located via string references: "ActionList" @ 0x007bebdc, "ActionId" @ 0x007bebd0, "ActionType" @ 0x007bf7f8
    /// - Original implementation: FUN_00508260 @ 0x00508260 (load ActionList from GFF)
    /// - FUN_00505bc0 @ 0x00505bc0 (save ActionList to GFF)
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector
    /// - Original implementation: Entities maintain action queue with current action and pending actions
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc.
    /// - Action parameters stored in ActionParam1-5, ActionParamStrA/B fields (stored as Type/Value pairs)
    /// </remarks>
    public class ActionQueue : IActionQueue
    {
        private IEntity _owner;
        private readonly LinkedList<IAction> _queue;
        private IAction _current;

        public ActionQueue()
        {
            _queue = new LinkedList<IAction>();
        }

        public ActionQueue(IEntity owner) : this()
        {
            _owner = owner;
        }

        // IComponent implementation
        public IEntity Owner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public void OnAttach()
        {
            // Initialize if needed
        }

        public void OnDetach()
        {
            Clear();
        }

        public IAction Current { get { return _current; } }
        public bool HasActions { get { return _current != null || _queue.Count > 0; } }
        public int Count { get { return _queue.Count + (_current != null ? 1 : 0); } }

        public void Add(IAction action)
        {
            if (action == null)
            {
                return;
            }

            action.Owner = _owner;
            _queue.AddLast(action);
        }

        public void AddFront(IAction action)
        {
            if (action == null)
            {
                return;
            }

            action.Owner = _owner;

            if (_current != null)
            {
                _queue.AddFirst(_current);
            }
            _current = action;
        }

        public void Clear()
        {
            if (_current != null)
            {
                _current.Dispose();
                _current = null;
            }

            foreach (IAction action in _queue)
            {
                action.Dispose();
            }
            _queue.Clear();
        }

        public void ClearByGroupId(int groupId)
        {
            if (_current != null && _current.GroupId == groupId)
            {
                _current.Dispose();
                _current = null;
            }

            LinkedListNode<IAction> node = _queue.First;
            while (node != null)
            {
                LinkedListNode<IAction> next = node.Next;
                if (node.Value.GroupId == groupId)
                {
                    node.Value.Dispose();
                    _queue.Remove(node);
                }
                node = next;
            }
        }

        public int Process(float deltaTime)
        {
            // Based on swkotor2.exe: Action queue processing implementation
            // Located via string references: "ActionList" @ 0x007bebdc, "ActionId" @ 0x007bebd0
            // Original implementation: FUN_00508260 @ 0x00508260 loads ActionList from GFF
            // Actions processed sequentially: Current action executes until complete, then next action dequeued
            // Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
            int instructionsExecuted = 0;

            // Get next action if we don't have one
            // Original engine: Dequeues next action when current completes
            if (_current == null && _queue.Count > 0)
            {
                _current = _queue.First.Value;
                _queue.RemoveFirst();
            }

            if (_current == null)
            {
                return instructionsExecuted;
            }

            // Execute current action
            // Original engine: Action.Update() called each frame until status != InProgress
            ActionStatus status = _current.Update(_owner, deltaTime);
            instructionsExecuted++; // Simplified - real implementation would track VM instructions

            if (status != ActionStatus.InProgress)
            {
                // Action complete or failed - dispose and move to next
                // Original engine: Action removed from queue when complete/failed
                _current.Dispose();
                _current = null;
            }

            return instructionsExecuted;
        }

        public IEnumerable<IAction> GetAllActions()
        {
            if (_current != null)
            {
                yield return _current;
            }

            foreach (IAction action in _queue)
            {
                yield return action;
            }
        }
    }
}

