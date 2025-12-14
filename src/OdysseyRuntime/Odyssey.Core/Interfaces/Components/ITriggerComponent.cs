using System.Collections.Generic;

namespace Odyssey.Core.Interfaces.Components
{
    /// <summary>
    /// Component for trigger volumes.
    /// </summary>
    public interface ITriggerComponent : IComponent
    {
        /// <summary>
        /// The geometry vertices defining the trigger volume.
        /// </summary>
        IList<System.Numerics.Vector3> Geometry { get; }

        /// <summary>
        /// Whether the trigger is currently enabled.
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// Type of trigger (0=generic, 1=transition, 2=trap).
        /// </summary>
        int TriggerType { get; set; }

        /// <summary>
        /// For transition triggers, the destination tag.
        /// </summary>
        string LinkedTo { get; set; }

        /// <summary>
        /// For transition triggers, the destination module.
        /// </summary>
        string LinkedToModule { get; set; }

        /// <summary>
        /// Whether this is a trap trigger.
        /// </summary>
        bool IsTrap { get; set; }

        /// <summary>
        /// Whether the trap is active.
        /// </summary>
        bool TrapActive { get; set; }

        /// <summary>
        /// Whether the trap has been detected.
        /// </summary>
        bool TrapDetected { get; set; }

        /// <summary>
        /// Whether the trap has been disarmed.
        /// </summary>
        bool TrapDisarmed { get; set; }

        /// <summary>
        /// DC to detect the trap.
        /// </summary>
        int TrapDetectDC { get; set; }

        /// <summary>
        /// DC to disarm the trap.
        /// </summary>
        int TrapDisarmDC { get; set; }

        /// <summary>
        /// Whether the trigger fires only once.
        /// </summary>
        bool FireOnce { get; set; }

        /// <summary>
        /// Whether the trigger has already been fired (for FireOnce triggers).
        /// </summary>
        bool HasFired { get; set; }

        /// <summary>
        /// Tests if a point is inside the trigger volume.
        /// </summary>
        bool ContainsPoint(System.Numerics.Vector3 point);

        /// <summary>
        /// Tests if an entity is inside the trigger volume.
        /// </summary>
        bool ContainsEntity(IEntity entity);
    }
}

