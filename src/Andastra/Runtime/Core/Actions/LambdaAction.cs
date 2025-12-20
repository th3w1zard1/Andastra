using System;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action that wraps a lambda or System.Action delegate.
    /// Used for scheduling simple delayed actions without creating full IAction implementations.
    /// </summary>
    internal class LambdaAction : IAction
    {
        private readonly System.Action _action;
        private bool _executed;

        public ActionType Type => ActionType.DoCommand;
        public int GroupId { get; set; }
        public IEntity Owner { get; set; }

        public LambdaAction(System.Action action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _executed = false;
        }

        public ActionStatus Update(IEntity actor, float deltaTime)
        {
            if (!_executed)
            {
                _action();
                _executed = true;
                return ActionStatus.Complete;
            }
            return ActionStatus.Complete;
        }

        public void Dispose()
        {
            // Nothing to dispose for lambda actions
        }
    }
}

