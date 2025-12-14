using System;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action that executes a stored script action.
    /// This is used by DelayCommand and AssignCommand.
    /// </summary>
    public class ActionDoCommand : ActionBase
    {
        private readonly Action<IEntity> _command;

        public ActionDoCommand(Action<IEntity> command)
            : base(ActionType.DoCommand)
        {
            _command = command;
        }

        protected override ActionStatus ExecuteInternal(IEntity actor, float deltaTime)
        {
            if (_command != null)
            {
                try
                {
                    _command(actor);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[ActionDoCommand] Error executing command: " + ex.Message);
                    return ActionStatus.Failed;
                }
            }
            return ActionStatus.Complete;
        }
    }
}

