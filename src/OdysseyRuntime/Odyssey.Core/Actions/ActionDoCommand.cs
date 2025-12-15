using System;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;

namespace Odyssey.Core.Actions
{
    /// <summary>
    /// Action that executes a stored script action.
    /// This is used by DelayCommand and AssignCommand.
    /// </summary>
    /// <remarks>
    /// Do Command Action:
    /// - Based on swkotor2.exe AssignCommand/DelayCommand system
    /// - Original implementation: Executes a stored action/script command on entity
    /// - Used by AssignCommand (execute on different entity) and DelayCommand (execute after delay)
    /// - Stores command as closure/action delegate, executes when action runs
    /// - STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand semantics
    /// </remarks>
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
            // Based on swkotor2.exe: AssignCommand/DelayCommand implementation
            // Located via string references: "AssignCommand" NWScript function, "DelayCommand" NWScript function
            // Original implementation: Executes stored action/script command on entity
            // Used by AssignCommand (execute on different entity) and DelayCommand (execute after delay)
            // STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand semantics
            // Command executes immediately when action runs (no delay in ActionDoCommand itself)
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

