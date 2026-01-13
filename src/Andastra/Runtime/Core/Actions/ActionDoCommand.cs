using System;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Core.Actions
{
    /// <summary>
    /// Action that executes a stored script action.
    /// This is used by DelayCommand and AssignCommand.
    /// </summary>
    /// <remarks>
    /// Do Command Action:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) AssignCommand/DelayCommand system
    /// - Located via string references: "DelayCommand" @ 0x007be900 (delay command script field), "Commandable" @ 0x007bec3c (commandable flag)
    /// - "deleted %d trace commands" @ 0x007b95d4 (command cleanup debug message)
    /// - Original implementation: Executes a stored action/script command on entity
    /// - Used by AssignCommand NWScript function (execute action on different entity immediately)
    /// - Used by DelayCommand NWScript function (execute action after specified delay)
    /// - STORE_STATE opcode in NCS VM stores stack/local state (stack pointer, local variables) for DelayCommand closure semantics
    /// - Action execution: Command executes immediately when action runs (no delay in ActionDoCommand itself - delay handled by DelayScheduler)
    /// - DelayCommand flow: DelayScheduler queues ActionDoCommand for execution after delay, preserves execution context (stack/locals)
    /// - AssignCommand flow: ActionDoCommand added to target entity's action queue, executes immediately
    /// - Command storage: Command stored as action delegate/closure, captures execution context (caller, variables, stack state)
    /// - Error handling: Command execution errors are caught and logged, action marked as Failed
    /// - Based on NWScript function implementations: AssignCommand (routine ID varies by game), DelayCommand (routine ID varies by game)
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
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): AssignCommand/DelayCommand implementation
            // Located via string references: "DelayCommand" @ 0x007be900, "AssignCommand" NWScript function
            // Original implementation: Executes stored action/script command on entity
            // Used by AssignCommand (execute on different entity) and DelayCommand (execute after delay)
            // STORE_STATE opcode in NCS VM stores stack/local state for DelayCommand semantics
            // Command executes immediately when action runs (no delay in ActionDoCommand itself)
            // DelayCommand: Uses DelayScheduler to queue action for later execution
            // AssignCommand: Executes command on different entity immediately (via action queue)
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

