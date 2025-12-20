namespace Andastra.Runtime.Core.Movement
{
    /// <summary>
    /// Cursor mode enumeration for player input handling.
    /// </summary>
    public enum CursorMode
    {
        /// <summary>
        /// Default cursor mode (no special action).
        /// </summary>
        Default,

        /// <summary>
        /// Walk to position.
        /// </summary>
        Walk,

        /// <summary>
        /// Run to position.
        /// </summary>
        Run,

        /// <summary>
        /// Attack target.
        /// </summary>
        Attack,

        /// <summary>
        /// Talk to entity.
        /// </summary>
        Talk,

        /// <summary>
        /// Use/interact with entity.
        /// </summary>
        Use,

        /// <summary>
        /// Open door.
        /// </summary>
        Door,

        /// <summary>
        /// Pick up item.
        /// </summary>
        Pickup,

        /// <summary>
        /// Transition through door/area.
        /// </summary>
        Transition,

        /// <summary>
        /// Cannot walk to position.
        /// </summary>
        NoWalk
    }
}

