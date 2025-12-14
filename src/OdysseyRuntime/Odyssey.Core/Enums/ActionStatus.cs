namespace Odyssey.Core.Enums
{
    /// <summary>
    /// Status returned by action execution.
    /// </summary>
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

