using System.Collections.Generic;

namespace Andastra.Runtime.Core.Interfaces.Components
{
    /// <summary>
    /// Component for trigger volumes.
    /// </summary>
    /// <remarks>
    /// Trigger Component Interface:
    /// Common interface for trigger volumes across all BioWare engines (Odyssey, Aurora, Eclipse).
    /// Implementation is split into engine-specific subclasses that inherit from BaseTriggerComponent:
    /// - Odyssey: TriggerComponent (swkotor.exe, swkotor2.exe)
    /// - Aurora: AuroraTriggerComponent (nwmain.exe)
    /// - Eclipse: EclipseTriggerComponent (daorigins.exe, DragonAge2.exe)
    ///
    /// Cross-Engine Analysis:
    ///
    /// Odyssey Engine (swkotor.exe, swkotor2.exe):
    /// - Based on UTT (Trigger) GFF template format
    /// - swkotor.exe: "Trigger" @ 0x007442e4, "TriggerList" @ 0x0074768c
    ///   - "EVENT_ENTERED_TRIGGER" @ 0x00744bd0, "EVENT_LEFT_TRIGGER" @ 0x00744bbc
    ///   - "OnTrapTriggered" @ 0x007495ec
    /// - swkotor2.exe: "Trigger" @ 0x007bc51c, "TriggerList" @ 0x007bd254
    ///   - "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "EVENT_LEFT_TRIGGER" @ 0x007bcdf4
    ///   - "OnTrapTriggered" @ 0x007c1a34, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac
    ///   - Transition fields: "LinkedTo" @ 0x007bd798, "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788
    ///   - "TransitionDestination" @ 0x007bd7a4 (waypoint tag for positioning after transition)
    ///   - 0x004e5920 @ 0x004e5920 loads trigger instances from GIT TriggerList, reads UTT templates
    ///   - 0x004dcfb0 @ 0x004dcfb0 handles trigger events (EVENT_ENTERED_TRIGGER case 2, EVENT_LEFT_TRIGGER case 3)
    ///
    /// Aurora Engine (nwmain.exe):
    /// - Based on CNWSTrigger class
    /// - "TriggerList" @ 0x140ddb800 (GFF list field in GIT)
    /// - CNWSTrigger::LoadTrigger @ 0x140502ac0 loads trigger data from GFF
    ///   - Loads Geometry (polygon vertices), Type, LinkedTo, LinkedToModule, LinkedToFlags
    ///   - Loads TrapDetectable, TrapDetectDC, TrapDisarmable, TrapDisarmDC, TrapOneShot
    ///   - Loads script hooks (OnEnter, OnExit, OnHeartbeat, OnClick, OnDisarm, OnTrapTriggered)
    /// - CNWSTrigger::SaveTrigger @ 0x1404c9b40 saves trigger data to GFF
    /// - LoadTriggers @ 0x140362d80 loads trigger list from area GIT
    /// - SaveTriggers @ 0x1403680a0 saves trigger list to area GIT
    ///
    /// Eclipse Engine (daorigins.exe, DragonAge2.exe):
    /// - Based on CCTrigger class (inherits from CTrigger)
    /// - daorigins.exe: "TriggerList" @ 0x00af5060, "CTrigger" @ 0x00b0d4cc
    /// - DragonAge2.exe: "TriggerList" @ 0x00bf4a44, "CTrigger" @ 0x00c23674
    /// - COMMAND_GETTRIGGER* and COMMAND_SETTRIGGER* functions provide script API
    /// - Similar structure to Aurora but with Eclipse-specific field names and storage
    ///
    /// Common Functionality (shared across all engines):
    /// - Triggers are invisible volumes defined by polygon geometry (Geometry field)
    /// - Triggers fire events (OnEnter, OnExit) when entities enter/exit trigger volume
    /// - TriggerType: 0=generic, 1=transition, 2=trap (consistent across all engines)
    /// - Transitions link to other areas/modules (LinkedTo, LinkedToModule)
    /// - Traps can be detected (TrapDetected), disarmed (TrapDisarmed) with DCs (TrapDetectDC, TrapDisarmDC)
    /// - FireOnce triggers only fire once (HasFired tracks state)
    /// - Script events: OnEnter, OnExit, OnHeartbeat, OnClick, OnDisarm, OnTrapTriggered
    /// - Point-in-polygon test: Uses ray casting algorithm (common across all engines, projects to X/Z plane)
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
        /// Whether this trigger is an area transition (Type == 1 and has LinkedTo but no LinkedToModule).
        /// </summary>
        bool IsAreaTransition { get; }

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

