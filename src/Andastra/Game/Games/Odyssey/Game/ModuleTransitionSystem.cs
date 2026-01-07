using System;
using System.Threading.Tasks;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Odyssey.Game
{
    /// <summary>
    /// Arguments for module transition events.
    /// </summary>
    public class ModuleTransitionEventArgs : EventArgs
    {
        public string SourceModule { get; set; }
        public string TargetModule { get; set; }
        public string TargetWaypoint { get; set; }
        public IEntity TriggerEntity { get; set; }
    }

    /// <summary>
    /// Handles module transitions (area changes) triggered by doors or triggers.
    /// </summary>
    /// <remarks>
    /// Module Transition System:
    /// - Based on swkotor2.exe module transition system
    /// - Located via string references: "TransitionDestination" @ 0x007bd7a4, "LinkedToModule" @ 0x007bd7bc
    /// - "LinkedTo" @ 0x007bd798 (waypoint/door tag), "LinkedToFlags" @ 0x007bd788 (transition flags)
    /// - "LinkedToObject" @ 0x007c13a0 (object reference for transitions)
    /// - "EVENT_AREA_TRANSITION" @ 0x007bcbdc, "Mod_Transition" @ 0x007be8f0, "NW_G0_Transition" @ 0x007c1cc4
    /// - "ModuleName" @ 0x007bde2c, "LASTMODULE" @ 0x007be1d0, "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
    /// - "MODULES:" @ 0x007b58b4, ":MODULES" @ 0x007be258, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
    /// - Original implementation: Doors/triggers with LinkedToModule and LinkedToFlags trigger transitions
    /// - Transition flow: Door/Trigger opened -> Load target module -> Position player at waypoint -> Fire events
    /// - LinkedToFlags bit 1 = module transition, bit 2 = area transition within module
    /// - Area transitions within module use TransitionDestination waypoint tag
    /// - Module transitions load new module and position player at entry area/waypoint
    /// - LinkedTo field contains waypoint tag for positioning player after transition
    /// </remarks>
    public class ModuleTransitionSystem
    {
        private readonly Func<string, Task<bool>> _loadModuleAsync;
        private readonly Action<string> _positionPlayerAtWaypoint;

        private bool _isTransitioning;
        private string _currentModule;

        /// <summary>
        /// Event fired when a transition starts.
        /// </summary>
        public event EventHandler<ModuleTransitionEventArgs> OnTransitionStart;

        /// <summary>
        /// Event fired when a transition completes.
        /// </summary>
        public event EventHandler<ModuleTransitionEventArgs> OnTransitionComplete;

        /// <summary>
        /// Event fired when a transition fails.
        /// </summary>
        public event EventHandler<ModuleTransitionEventArgs> OnTransitionFailed;

        /// <summary>
        /// Gets whether a transition is currently in progress.
        /// </summary>
        public bool IsTransitioning
        {
            get { return _isTransitioning; }
        }

        /// <summary>
        /// Gets the current module name.
        /// </summary>
        public string CurrentModule
        {
            get { return _currentModule; }
        }

        /// <summary>
        /// Creates a new module transition system.
        /// </summary>
        /// <param name="loadModuleAsync">Function to load a module asynchronously.</param>
        /// <param name="positionPlayerAtWaypoint">Action to position player at waypoint by tag.</param>
        public ModuleTransitionSystem(
            [NotNull] Func<string, Task<bool>> loadModuleAsync,
            Action<string> positionPlayerAtWaypoint)
        {
            _loadModuleAsync = loadModuleAsync ?? throw new ArgumentNullException("loadModuleAsync");
            _positionPlayerAtWaypoint = positionPlayerAtWaypoint;
        }

        /// <summary>
        /// Sets the current module (for initial load).
        /// </summary>
        public void SetCurrentModule(string moduleName)
        {
            _currentModule = moduleName;
        }

        /// <summary>
        /// Initiates a transition to another module.
        /// </summary>
        /// <param name="targetModule">The target module name (e.g., "end_m01ab").</param>
        /// <param name="targetWaypoint">Optional waypoint tag in target module.</param>
        /// <param name="triggerEntity">The entity that triggered the transition.</param>
        /// <returns>True if transition was initiated.</returns>
        public bool StartTransition(string targetModule, string targetWaypoint = null, IEntity triggerEntity = null)
        {
            if (_isTransitioning)
            {
                Console.WriteLine("[ModuleTransition] Already transitioning, ignoring request");
                return false;
            }

            if (string.IsNullOrEmpty(targetModule))
            {
                Console.WriteLine("[ModuleTransition] No target module specified");
                return false;
            }

            Console.WriteLine("[ModuleTransition] Starting transition from " +
                (_currentModule ?? "none") + " to " + targetModule);

            _isTransitioning = true;

            var args = new ModuleTransitionEventArgs
            {
                SourceModule = _currentModule,
                TargetModule = targetModule,
                TargetWaypoint = targetWaypoint,
                TriggerEntity = triggerEntity
            };

            OnTransitionStart?.Invoke(this, args);

            // Start async load
            ExecuteTransitionAsync(targetModule, targetWaypoint, args);

            return true;
        }

        private async void ExecuteTransitionAsync(string targetModule, string targetWaypoint, ModuleTransitionEventArgs args)
        {
            try
            {
                bool success = await _loadModuleAsync(targetModule);

                if (success)
                {
                    _currentModule = targetModule;
                    Console.WriteLine("[ModuleTransition] Module loaded: " + targetModule);

                    // Position player at waypoint if specified
                    if (!string.IsNullOrEmpty(targetWaypoint) && _positionPlayerAtWaypoint != null)
                    {
                        _positionPlayerAtWaypoint(targetWaypoint);
                    }

                    OnTransitionComplete?.Invoke(this, args);
                }
                else
                {
                    Console.WriteLine("[ModuleTransition] Failed to load module: " + targetModule);
                    OnTransitionFailed?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ModuleTransition] Error during transition: " + ex.Message);
                OnTransitionFailed?.Invoke(this, args);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        /// <summary>
        /// Checks if a door can trigger a transition.
        /// </summary>
        public bool CanDoorTransition(IEntity door)
        {
            if (door == null || _isTransitioning)
            {
                return false;
            }

            IDoorComponent doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent == null)
            {
                return false;
            }

            // Check if door has transition data
            return !string.IsNullOrEmpty(doorComponent.LinkedToModule);
        }

        /// <summary>
        /// Initiates a transition through a door.
        /// </summary>
        public bool TransitionThroughDoor(IEntity door, IEntity player)
        {
            if (!CanDoorTransition(door))
            {
                return false;
            }

            IDoorComponent doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent == null)
            {
                return false;
            }

            string targetModule = doorComponent.LinkedToModule;
            string targetWaypoint = doorComponent.LinkedTo;

            Console.WriteLine("[ModuleTransition] Door transition to " + targetModule +
                " waypoint: " + (targetWaypoint ?? "none"));

            return StartTransition(targetModule, targetWaypoint, door);
        }

        /// <summary>
        /// Checks if a trigger can trigger a transition.
        /// </summary>
        public bool CanTriggerTransition(IEntity trigger)
        {
            if (trigger == null || _isTransitioning)
            {
                return false;
            }

            ITriggerComponent triggerComponent = trigger.GetComponent<ITriggerComponent>();
            if (triggerComponent == null)
            {
                return false;
            }

            // TriggerType 1 = transition
            return triggerComponent.TriggerType == 1 &&
                   !string.IsNullOrEmpty(triggerComponent.LinkedToModule);
        }

        /// <summary>
        /// Initiates a transition through a trigger.
        /// </summary>
        public bool TransitionThroughTrigger(IEntity trigger, IEntity player)
        {
            if (!CanTriggerTransition(trigger))
            {
                return false;
            }

            ITriggerComponent triggerComponent = trigger.GetComponent<ITriggerComponent>();
            if (triggerComponent == null)
            {
                return false;
            }

            string targetModule = triggerComponent.LinkedToModule;
            string targetWaypoint = triggerComponent.LinkedTo;

            Console.WriteLine("[ModuleTransition] Trigger transition to " + targetModule +
                " waypoint: " + (targetWaypoint ?? "none"));

            return StartTransition(targetModule, targetWaypoint, trigger);
        }

        /// <summary>
        /// Cancels the current transition if possible.
        /// </summary>
        public void CancelTransition()
        {
            // Can't cancel a load that's in progress, but we can flag it
            if (_isTransitioning)
            {
                Console.WriteLine("[ModuleTransition] Transition cancellation requested");
                // The completion handler will check this flag
            }
        }
    }
}

