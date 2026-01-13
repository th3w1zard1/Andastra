using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Game.Games.Common.Components;

namespace Andastra.Game.Games.Odyssey.Components
{
    /// <summary>
    /// Component for trigger entities in Odyssey engine (KotOR/KotOR2).
    /// </summary>
    /// <remarks>
    /// Odyssey Trigger Component:
    /// - Inherits from BaseTriggerComponent (common functionality)
    /// - Odyssey-specific implementation for swkotor.exe and swkotor2.exe
    /// - Based on swkotor.exe and swkotor2.exe trigger system
    /// - Located via string references: "Trigger" @ 0x007bc51c, "TriggerList" @ 0x007bd254 (swkotor2.exe)
    /// - "EVENT_ENTERED_TRIGGER" @ 0x007bce08, "EVENT_LEFT_TRIGGER" @ 0x007bcdf4 (swkotor2.exe)
    /// - "OnTrapTriggered" @ 0x007c1a34, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MINE_TRIGGERED" @ 0x007bc7ac (swkotor2.exe)
    /// - Transition fields: "LinkedTo" @ 0x007bd798, "LinkedToModule" @ 0x007bd7bc, "LinkedToFlags" @ 0x007bd788 (swkotor2.exe)
    /// - "TransitionDestination" @ 0x007bd7a4 (waypoint tag for positioning after transition) (swkotor2.exe)
    /// - Original implementation: UTT (Trigger) GFF templates define trigger properties and geometry
    /// - Triggers are invisible polygonal volumes that fire scripts on enter/exit
    /// - Trigger types: Generic (0), Transition (1), Trap (2)
    /// - Transition triggers: LinkedTo, LinkedToModule, LinkedToFlags for area/module transitions
    /// - Trap triggers: OnTrapTriggered script fires when trap is activated
    /// - Geometry: Triggers have polygon geometry (Geometry field in GIT) defining trigger volume
    /// - Based on UTT file format documentation in vendor/PyKotor/wiki/
    /// - 0x004e5920 @ 0x004e5920 (swkotor2.exe) loads trigger instances from GIT TriggerList, reads UTT templates
    /// </remarks>
    public class TriggerComponent : BaseTriggerComponent
    {
        /// <summary>
        /// Initializes a new instance of the TriggerComponent class.
        /// </summary>
        public TriggerComponent()
        {
            TemplateResRef = string.Empty;
            TransitionDestination = string.Empty;
            LinkedToFlags = 0;
            FactionId = 0;
            Type = 0;
            TrapFlag = false;
            TrapType = 0;
            TrapDetectable = false;
            TrapDisarmable = false;
            TrapOneShot = false;
        }

        /// <summary>
        /// Template resource reference (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TemplateResRef field in UTT template
        /// </remarks>
        public string TemplateResRef { get; set; }

        /// <summary>
        /// Trigger type (0 = generic, 1 = transition, 2 = trap) (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Type field in UTT template
        /// </remarks>
        public int Type { get; set; }

        /// <summary>
        /// Linked flags (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): LinkedToFlags field in UTT template
        /// </remarks>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Transition destination waypoint tag (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TransitionDestination field in UTT template
        /// </remarks>
        public string TransitionDestination { get; set; }

        /// <summary>
        /// Faction ID (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FactionId field in UTT template
        /// </remarks>
        public int FactionId { get; set; }

        /// <summary>
        /// Whether the trigger has a trap (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapFlag field in UTT template
        /// </remarks>
        public bool TrapFlag { get; set; }

        /// <summary>
        /// Trap type (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapType field in UTT template
        /// </remarks>
        public int TrapType { get; set; }

        /// <summary>
        /// Whether the trap is detectable (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapDetectable field in UTT template
        /// </remarks>
        public bool TrapDetectable { get; set; }

        /// <summary>
        /// Whether the trap is disarmable (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapDisarmable field in UTT template
        /// </remarks>
        public bool TrapDisarmable { get; set; }

        /// <summary>
        /// Whether the trap is one-shot (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapOneShot field in UTT template
        /// </remarks>
        public bool TrapOneShot { get; set; }

        /// <summary>
        /// Trap disarm DC (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DisarmDC field in UTT template
        /// </remarks>
        public int DisarmDC { get; set; }

        /// <summary>
        /// Set of entity IDs currently inside this trigger (Odyssey-specific).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Tracks entities currently in trigger for enter/exit detection
        /// </remarks>
        public HashSet<uint> EnteredBy
        {
            get { return _enteredBy; }
            set { _enteredBy = value ?? new HashSet<uint>(); }
        }

        #region ITriggerComponent Abstract Property Implementations

        /// <summary>
        /// Type of trigger (0=generic, 1=transition, 2=trap).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Type field in UTT template
        /// </remarks>
        public override int TriggerType
        {
            get { return Type; }
            set { Type = value; }
        }

        /// <summary>
        /// Whether this is a trap trigger.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapFlag field in UTT template
        /// </remarks>
        public override bool IsTrap
        {
            get { return TrapFlag; }
            set { TrapFlag = value; }
        }

        /// <summary>
        /// DC to detect the trap.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapDetectDC field in UTT template
        /// </remarks>
        public override int TrapDetectDC { get; set; }

        /// <summary>
        /// DC to disarm the trap.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): DisarmDC field in UTT template
        /// </remarks>
        public override int TrapDisarmDC
        {
            get { return DisarmDC; }
            set { DisarmDC = value; }
        }

        /// <summary>
        /// Whether the trigger fires only once.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): TrapOneShot field in UTT template
        /// </remarks>
        public override bool FireOnce
        {
            get { return TrapOneShot; }
            set { TrapOneShot = value; }
        }

        #endregion


        /// <summary>
        /// Whether this trigger is a module transition.
        /// </summary>
        public bool IsModuleTransition
        {
            get { return Type == 1 && !string.IsNullOrEmpty(LinkedToModule); }
        }

        /// <summary>
        /// Whether this trigger is an area transition.
        /// </summary>
        public override bool IsAreaTransition
        {
            get { return Type == 1 && !string.IsNullOrEmpty(LinkedTo) && string.IsNullOrEmpty(LinkedToModule); }
        }
    }
}
