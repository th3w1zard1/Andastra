namespace Andastra.Runtime.Core.Enums
{
    /// <summary>
    /// Status returned by action execution.
    /// </summary>
    /// <remarks>
    /// Action Status Enum:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) action execution system
    /// - Actions return status after Update() call: InProgress (continue), Complete (done), Failed (abort)
    /// - Action queue processes actions sequentially: current action updates until Complete or Failed
    /// - InProgress: Action continues execution (called again next frame)
    /// - Complete: Action finished successfully, next action dequeued
    /// - Failed: Action failed (e.g., pathfinding failed, target invalid), next action dequeued
    /// - Action execution: FUN_00508260 @ 0x00508260 loads actions from GFF, FUN_00505bc0 @ 0x00505bc0 saves actions to GFF
    /// - Action queue processing: Actions updated each frame until status changes from InProgress
    /// </remarks>
    public enum ActionStatus
    {
        /// <summary>Action is still running and needs more time.</summary>
        InProgress,

        /// <summary>Action completed successfully.</summary>
        Complete,

        /// <summary>Action failed and should be removed from queue.</summary>
        Failed
    }
}

