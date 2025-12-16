using System.Collections.Generic;

namespace BioWareEngines.Core.Interfaces.Components
{
    /// <summary>
    /// Component for trigger volumes.
    /// </summary>
    /// <remarks>
    /// Trigger Component Interface:
    /// - Based on swkotor2.exe trigger system
    /// - Located via string references: "Trigger" @ 0x007bc548, "Trigger List" @ 0x007bd280
    /// - "LinkedTo" @ 0x007c13a0, "LinkedToModule" @ 0x007bd7bc (trigger transition links)
    /// - Trigger events: "EVENT_ENTERED_TRIGGER" @ 0x007bcbcc, "EVENT_LEFT_TRIGGER" @ 0x007bcc00
    /// - Event dispatching: FUN_004dcfb0 @ 0x004dcfb0 handles trigger events (EVENT_ENTERED_TRIGGER case 2, EVENT_LEFT_TRIGGER case 3)
    /// - Original implementation: Triggers are invisible volumes defined by polygon geometry
    /// - Triggers fire events (OnEnter, OnExit) when entities enter/exit trigger volume
    /// - TriggerType: 0=generic, 1=transition, 2=trap
    /// - Transitions link to other areas/modules (LinkedTo, LinkedToModule)
    /// - Traps can be detected (TrapDetected), disarmed (TrapDisarmed) with DCs (TrapDetectDC, TrapDisarmDC)
    /// - FireOnce triggers only fire once (HasFired tracks state)
    /// - Script events: OnEnter, OnExit, OnHeartbeat, OnClick, OnDisarm, OnTrapTriggered
    /// </remarks>
    public interface ITriggerComponent : IComponent
    {
        /// <summary>
        /// The geometry vertices defining the trigger volume.
        /// </summary>
        IList<System.Numerics.Vector3> Geometry { get; set; }

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

