using System.Collections.Generic;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Game.Games.Common.Actions
{
    /// <summary>
    /// Base class for action queues shared across BioWare engines that use GFF-based action serialization.
    /// </summary>
    /// <remarks>
    /// Base Action Queue Implementation:
    /// Common action queue system shared across Odyssey (swkotor.exe, swkotor2.exe) and Aurora (nwmain.exe, nwn2main.exe).
    /// 
    /// Common structure across engines:
    /// - FIFO queue: Actions processed sequentially, current action executes until complete
    /// - ActionList GFF field: List of actions stored in entity GFF structures
    /// - Action structure: ActionId (uint32), GroupActionId (int16), NumParams (int16), Paramaters array
    /// - Parameter types: 1=int, 2=float, 3=object/uint32, 4=string, 5=location/vector
    /// - GroupActionId: Allows batching/clearing related actions together
    /// - Instruction count tracking: Accumulates instruction count from script executions during action processing
    /// 
    /// Engine-specific implementations:
    /// - Odyssey (swkotor.exe, swkotor2.exe): OdysseyActionQueue - specific function addresses for GFF loading/saving
    /// - Aurora (nwmain.exe, nwn2main.exe): AuroraActionQueue - CNWSObject::LoadActionQueue/SaveActionQueue methods
    /// - Eclipse (daorigins.exe, DragonAge2.exe, ): Uses ActionFramework (different architecture)
    /// - Infinity (, ): May use different system (needs investigation)
    /// 
    /// All engine-specific details (function addresses, GFF field offsets, implementation specifics) are in subclasses.
    /// This base class contains only functionality that is identical across engines using GFF-based action serialization.
    /// </remarks>
    public abstract class BaseActionQueue : IActionQueue
    {
        protected IEntity _owner;
        protected readonly LinkedList<IAction> _queue;
        protected IAction _current;
        protected int _lastInstructionCount;
        protected int _accumulatedInstructionCount;

        protected BaseActionQueue()
        {
            _queue = new LinkedList<IAction>();
        }

        protected BaseActionQueue(IEntity owner) : this()
        {
            _owner = owner;
        }

        // IComponent implementation
        public IEntity Owner
        {
            get { return _owner; }
            set { _owner = value; }
        }

        public virtual void OnAttach()
        {
            // Initialize if needed
        }

        public virtual void OnDetach()
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
            int instructionsExecuted = 0;

            // Get next action if we don't have one
            if (_current == null && _queue.Count > 0)
            {
                _current = _queue.First.Value;
                _queue.RemoveFirst();
            }

            if (_current == null)
            {
                // Store accumulated instruction count from this frame
                _lastInstructionCount = _accumulatedInstructionCount;
                return instructionsExecuted;
            }

            // Execute current action
            ActionStatus status = _current.Update(_owner, deltaTime);

            if (status != ActionStatus.InProgress)
            {
                // Action complete or failed - dispose and move to next
                _current.Dispose();
                _current = null;
            }

            // Store accumulated instruction count from this frame
            _lastInstructionCount = _accumulatedInstructionCount;
            return instructionsExecuted;
        }

        /// <summary>
        /// Adds instruction count from script execution to the accumulator.
        /// </summary>
        public void AddInstructionCount(int count)
        {
            if (count > 0)
            {
                _accumulatedInstructionCount += count;
            }
        }

        /// <summary>
        /// Gets the instruction count from the last Process() call.
        /// </summary>
        public int GetLastInstructionCount()
        {
            return _lastInstructionCount;
        }

        /// <summary>
        /// Resets the instruction count accumulator for the current frame.
        /// </summary>
        public void ResetInstructionCount()
        {
            _accumulatedInstructionCount = 0;
            _lastInstructionCount = 0;
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

        public void Update(IEntity entity, float deltaTime)
        {
            Process(deltaTime);
        }
    }
}

