using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Games.Common.Components;

namespace Andastra.Runtime.Games.Aurora.Components
{
    /// <summary>
    /// Aurora engine-specific trigger component implementation.
    /// </summary>
    /// <remarks>
    /// Aurora Trigger Component:
    /// - Inherits from BaseTriggerComponent for common trigger functionality
    /// - Aurora-specific: CNWSTrigger class, GFF field names, transition system
    /// - Based on nwmain.exe trigger system (Neverwinter Nights)
    /// - Original implementation: CNWSTrigger class in nwmain.exe
    /// - CNWSTrigger::LoadTrigger @ 0x1404c8a00 (nwmain.exe: load trigger data from GFF)
    ///   - Loads Geometry (polygon vertices), Type, LinkedTo, LinkedToModule, LinkedToFlags
    ///   - Loads TrapDetectable, TrapDetectDC, TrapDisarmable, TrapDisarmDC, TrapOneShot
    ///   - Loads script hooks (OnEnter, OnExit, OnHeartbeat, OnClick, OnDisarm, OnTrapTriggered)
    /// - CNWSTrigger::SaveTrigger @ 0x1404c9b40 (nwmain.exe: save trigger data to GFF)
    ///   - Saves Geometry, Type, LinkedTo, LinkedToModule, LinkedToFlags
    ///   - Saves TrapDetectable, TrapDetectDC, TrapDisarmable, TrapDisarmDC, TrapOneShot
    ///   - Saves script hooks and position (X, Y, Z)
    /// - LoadTriggers @ 0x140362b20 (nwmain.exe: load trigger list from area GIT)
    /// - SaveTriggers @ 0x1403680a0 (nwmain.exe: save trigger list to area GIT)
    /// - Located via string reference: "Trigger List" @ 0x140ddb800 (GFF list field in GIT)
    /// - Triggers have enter/exit detection, script firing, transitions, traps
    /// - Script events: OnEnter, OnExit, OnHeartbeat, OnClick, OnDisarm, OnTrapTriggered (fired via EventBus)
    /// - Trigger types: 0=generic, 1=transition, 2=trap (same as Odyssey)
    /// - Transition triggers: LinkedTo, LinkedToModule, LinkedToFlags for area/module transitions
    /// - Trap triggers: Can be detected/disarmed with DCs, fire OnTrapTriggered script
    /// </remarks>
    public class AuroraTriggerComponent : BaseTriggerComponent
    {
        /// <summary>
        /// Linked flags for transitions (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: LinkedToFlags field in CNWSTrigger
        /// </remarks>
        public int LinkedToFlags { get; set; }

        /// <summary>
        /// Trigger type (0 = generic, 1 = transition, 2 = trap) (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Type field in CNWSTrigger
        /// </remarks>
        public int Type { get; set; }

        /// <summary>
        /// Trap detect DC (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: TrapDetectDC field in CNWSTrigger
        /// </remarks>
        private int _trapDetectDC;

        /// <summary>
        /// Trap disarm DC (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: TrapDisarmDC field in CNWSTrigger
        /// </remarks>
        private int _trapDisarmDC;

        /// <summary>
        /// Whether trap is one-shot (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: TrapOneShot field in CNWSTrigger
        /// </remarks>
        private bool _trapOneShot;

        /// <summary>
        /// Whether trigger is a trap (Aurora-specific).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Type == 2 indicates trap trigger
        /// </remarks>
        private bool _isTrap;

        #region ITriggerComponent Abstract Property Implementations

        /// <summary>
        /// Type of trigger (0=generic, 1=transition, 2=trap).
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: Type field in CNWSTrigger
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
        /// Based on nwmain.exe: Type == 2 indicates trap trigger
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
        /// Based on nwmain.exe: TrapDetectDC field in CNWSTrigger
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
        /// Based on nwmain.exe: TrapDisarmDC field in CNWSTrigger
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
        /// Based on nwmain.exe: TrapOneShot field in CNWSTrigger
        /// </remarks>
        public override bool FireOnce
        {
            get { return _trapOneShot; }
            set { _trapOneShot = value; }
        }

        #endregion
    }
}

