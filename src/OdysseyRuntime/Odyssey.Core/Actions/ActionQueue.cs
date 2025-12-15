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
    /// - Located via string references: "ActionList" @ 0x007bebdc (action list GFF field), "ActionId" @ 0x007bebd0 (action ID field)
    /// - "ActionType" @ 0x007bf7f8 (action type field), "GroupActionId" @ 0x007bebc0 (group action ID field)
    /// - "ActionTimer" @ 0x007bf820 (action timer field), "SchedActionList" @ 0x007bf99c (scheduled action list field)
    /// - "ParryActions" @ 0x007bfa18 (parry actions field), "Action" @ 0x007c7150 (action field), "ACTION" @ 0x007cd138 (action constant)
    /// - Action parameter fields: "ActionParam1" @ 0x007c36c8, "ActionParam2" @ 0x007c36b8, "ActionParam3" @ 0x007c36a8
    /// - "ActionParam4" @ 0x007c3698, "ActionParam5" @ 0x007c3688 (action parameter fields)
    /// - "ActionParam1b" @ 0x007c3670, "ActionParam2b" @ 0x007c3660, "ActionParam3b" @ 0x007c3650
    /// - "ActionParam4b" @ 0x007c3640, "ActionParam5b" @ 0x007c3630 (action parameter boolean fields)
    /// - "ActionParamStrA" @ 0x007c3620, "ActionParamStrB" @ 0x007c3610 (action parameter string fields)
    /// - Original implementation: FUN_00508260 @ 0x00508260 (load ActionList from GFF, parses ActionId, GroupActionId, NumParams, Paramaters)
    /// - FUN_00505bc0 @ 0x00505bc0 (save ActionList to GFF, writes ActionId, GroupActionId, NumParams, Paramaters array)
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector
    /// - Original implementation: Entities maintain action queue with current action and pending actions
    /// - Actions processed sequentially: Current action executes until complete, then next action dequeued
    /// - Action types: Move, Attack, UseObject, SpeakString, PlayAnimation, etc. (defined in ActionType enum)
    /// - Action parameters stored in ActionParam1-5, ActionParamStrA/B, ActionParam1b-5b fields (stored as Type/Value pairs in GFF)
    /// - GroupActionId: Allows batching/clearing related actions together (ClearAllActions clears actions by group ID)
    /// - ClearAllActions NWScript function: Clears all actions from entity's action queue
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

