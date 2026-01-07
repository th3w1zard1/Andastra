using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Eclipse.Components
{
    /// <summary>
    /// Eclipse engine-specific trigger component implementation.
    /// </summary>
    /// <remarks>
    /// Eclipse Trigger Component:
    /// - Inherits from BaseTriggerComponent for common trigger functionality
    /// - Eclipse-specific: CCTrigger class, GFF field names, transition system
    /// - Based on daorigins.exe and DragonAge2.exe trigger system
    /// - Original implementation: CCTrigger class in daorigins.exe and DragonAge2.exe
    /// - TriggerList @ 0x00af5040 (daorigins.exe: trigger list in area)
    /// - CTrigger @ 0x00b0d4a0 (daorigins.exe: trigger class)
    /// - COMMAND_GETTRIGGER* and COMMAND_SETTRIGGER* functions (daorigins.exe: script commands)
    /// - Triggers have enter/exit detection, script firing, transitions, traps
    /// - Script events: OnEnter, OnExit, OnHeartbeat, OnClick, OnDisarm, OnTrapTriggered (fired via EventBus)
    /// - Trigger types: 0=generic, 1=transition, 2=trap (same as Odyssey/Aurora)
    /// - Transition triggers: LinkedTo, LinkedToModule, LinkedToFlags for area/module transitions
    /// - Trap triggers: Can be detected/disarmed with DCs, fire OnTrapTriggered script
    /// - Eclipse engine uses similar trigger system to Aurora but with different field names and storage
    /// </remarks>
    public class EclipseTriggerComponent : BaseTriggerComponent
    {
        /// <summary>
        /// Linked flags for transitions (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: LinkedToFlags field in CCTrigger
        /// </remarks>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Trigger type (0 = generic, 1 = transition, 2 = trap) (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Type field in CCTrigger
        /// </remarks>
        public int Type { get; set; }

        /// <summary>
        /// Trap detect DC (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TrapDetectDC field in CCTrigger
        /// </remarks>
        private int _trapDetectDC;

        /// <summary>
        /// Trap disarm DC (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TrapDisarmDC field in CCTrigger
        /// </remarks>
        private int _trapDisarmDC;

        /// <summary>
        /// Whether trap is one-shot (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TrapOneShot field in CCTrigger
        /// </remarks>
        private bool _trapOneShot;

        /// <summary>
        /// Whether trigger is a trap (Eclipse-specific).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Type == 2 indicates trap trigger
        /// </remarks>
        private bool _isTrap;

        #region ITriggerComponent Abstract Property Implementations

        /// <summary>
        /// Type of trigger (0=generic, 1=transition, 2=trap).
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Type field in CCTrigger
        /// </remarks>
        public override int TriggerType
        {
            get { return Type; }
            set
            {
                Type = value;
                _isTrap = (Type == 2);
            }
        }

        /// <summary>
        /// Whether this is a trap trigger.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: Type == 2 indicates trap trigger
        /// </remarks>
        public override bool IsTrap
        {
            get { return _isTrap; }
            set
            {
                _isTrap = value;
                if (_isTrap)
                {
                    Type = 2;
                }
                else if (Type == 2)
                {
                    Type = 0;
                }
            }
        }

        /// <summary>
        /// DC to detect the trap.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TrapDetectDC field in CCTrigger
        /// </remarks>
        public override int TrapDetectDC
        {
            get { return _trapDetectDC; }
            set { _trapDetectDC = value; }
        }

        /// <summary>
        /// DC to disarm the trap.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TrapDisarmDC field in CCTrigger
        /// </remarks>
        public override int TrapDisarmDC
        {
            get { return _trapDisarmDC; }
            set { _trapDisarmDC = value; }
        }

        /// <summary>
        /// Whether the trigger fires only once.
        /// </summary>
        /// <remarks>
        /// Based on daorigins.exe and DragonAge2.exe: TrapOneShot field in CCTrigger
        /// </remarks>
        public override bool FireOnce
        {
            get { return _trapOneShot; }
            set { _trapOneShot = value; }
        }

        #endregion
    }
}

