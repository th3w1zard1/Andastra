using System;
using System.Threading.Tasks;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Navigation;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Common
{
    /// <summary>
    /// Abstract base class for module management across all engines.
    /// </summary>
    /// <remarks>
    /// Base Engine Module:
    /// - Common module loading and state management framework shared across all BioWare engines
    /// - Provides foundation for engine-specific module systems
    /// - Implements common patterns identified through cross-engine reverse engineering
    /// 
    /// Common Module Management Patterns (Implemented in Base Class):
    /// 1. Module State Tracking:
    ///    - Current module name, area, and navigation mesh (common across all engines)
    ///    - Module state validation and null checking
    ///    - State transitions: Idle -> Loading -> Loaded -> Running -> Unloading -> Idle
    /// 
    /// 2. Module Unloading:
    ///    - Common unloading sequence: Validate state -> Clean up -> Reset state
    ///    - Engine-specific cleanup handled by OnUnloadModule() override
    ///    - State reset: Clear module name, area, navigation mesh
    /// 
    /// 3. Resource Provider Integration:
    ///    - Common resource provider interface for all engines
    ///    - Resource loading abstraction (engine-specific implementations in subclasses)
    /// 
    /// 4. World Integration:
    ///    - Common world interface for entity management
    ///    - Module state synchronization with world state
    /// 
    /// Cross-Engine Analysis (Reverse Engineered Patterns):
    /// - Odyssey: IFO/LYT/VIS/GIT/ARE file formats, module state flags
    /// - Aurora: Module.ifo format, area files, entity spawning
    /// - Eclipse: UnrealScript-based module loading (message-based architecture)
    /// - Infinity: ARE/WED/GAM file formats
    /// Common patterns: Module state flags, loading sequences, entity spawning
    /// 
    /// Inheritance Structure:
    /// - BaseEngineModule (Runtime.Games.Common): Common module state management, unloading sequence, resource provider integration
    ///   - OdysseyModuleLoader : BaseEngineModule (Runtime.Games.Odyssey)
    ///     - Engine-specific: IFO/LYT/VIS/GIT/ARE file loading, Odyssey module state flags
    ///   - AuroraModuleLoader : BaseEngineModule (Runtime.Games.Aurora)
    ///     - Engine-specific: Module.ifo, HAK files, Aurora module formats
    ///   - EclipseModuleLoader : BaseEngineModule (Runtime.Games.Eclipse)
    ///     - Engine-specific: UnrealScript packages, .rim files, message-based loading
    ///   - InfinityModuleLoader : BaseEngineModule (Runtime.Games.Infinity)
    ///     - Engine-specific: ARE/WED/GAM file formats, BIF archives
    /// 
    /// Ghidra Reverse Engineering Requirements:
    /// - Verify module state flag addresses and bit patterns across all engines
    /// - Document module loading function addresses and calling conventions
    /// - Analyze resource loading sequences and file format parsing
    /// - Identify common vs engine-specific module management patterns
    /// - Verify module unloading sequences and cleanup order
    /// </remarks>
    public abstract class BaseEngineModule : IEngineModule
    {
        protected readonly IWorld _world;
        protected readonly IGameResourceProvider _resourceProvider;
        protected string _currentModuleName;
        protected IArea _currentArea;
        protected NavigationMesh _currentNavigationMesh;

        /// <summary>
        /// Initializes a new instance of the BaseEngineModule class.
        /// </summary>
        /// <param name="world">The game world instance.</param>
        /// <param name="resourceProvider">The resource provider for loading module resources.</param>
        /// <exception cref="ArgumentNullException">Thrown if world or resourceProvider is null.</exception>
        /// <remarks>
        /// Common initialization pattern across all engines:
        /// - Validates required dependencies (world, resource provider)
        /// - Initializes module state to Idle (no module loaded)
        /// - Sets up resource provider for engine-specific resource loading
        /// </remarks>
        protected BaseEngineModule(IWorld world, IGameResourceProvider resourceProvider)
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }

            if (resourceProvider == null)
            {
                throw new ArgumentNullException(nameof(resourceProvider));
            }

            _world = world;
            _resourceProvider = resourceProvider;
        }

        /// <summary>
        /// Gets or sets the current module name.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Resource reference name of the currently loaded module.
        /// Returns null when no module is loaded (Idle state).
        /// </remarks>
        [CanBeNull]
        public string CurrentModuleName
        {
            get { return _currentModuleName; }
            protected set { _currentModuleName = value; }
        }

        /// <summary>
        /// Gets or sets the current area.
        /// </summary>
        /// <remarks>
        /// Common across all engines: The active area within the current module.
        /// Typically the entry area or the area the player is currently in.
        /// Returns null when no module/area is loaded.
        /// </remarks>
        [CanBeNull]
        public IArea CurrentArea
        {
            get { return _currentArea; }
            protected set { _currentArea = value; }
        }

        /// <summary>
        /// Gets or sets the current navigation mesh.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Navigation/walkmesh data for pathfinding in the current area.
        /// Returns null when no navigation mesh is available.
        /// </remarks>
        [CanBeNull]
        public NavigationMesh CurrentNavigationMesh
        {
            get { return _currentNavigationMesh; }
            protected set { _currentNavigationMesh = value; }
        }

        /// <summary>
        /// Loads a module by name (engine-specific implementation required).
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to load.</param>
        /// <param name="progressCallback">Optional callback for loading progress (0.0 to 1.0).</param>
        /// <returns>Task that completes when the module is loaded.</returns>
        /// <remarks>
        /// Common loading sequence (implemented in subclasses):
        /// 1. Validate module name (non-null, non-empty)
        /// 2. Check if module exists (HasModule)
        /// 3. Unload current module if one is loaded
        /// 4. Load module resources (engine-specific file formats)
        /// 5. Initialize module state
        /// 6. Load entry area (if applicable)
        /// 7. Spawn entities from module data
        /// 8. Set current module/area/navigation mesh (via protected setters)
        /// 9. Trigger module load events/scripts
        /// 
        /// Progress reporting: Engines typically report progress at key milestones:
        /// - 0.0: Start loading
        /// - 0.1-0.3: Resource loading
        /// - 0.4-0.6: Area initialization
        /// - 0.7-0.9: Entity spawning
        /// - 1.0: Complete
        /// </remarks>
        public abstract Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null);

        /// <summary>
        /// Unloads the current module.
        /// </summary>
        /// <remarks>
        /// Common unloading sequence (implemented in base class):
        /// 1. Check if module is loaded (currentModuleName != null)
        /// 2. Call engine-specific cleanup (OnUnloadModule override)
        /// 3. Reset module state (clear module name, area, navigation mesh)
        /// 
        /// Engine-specific cleanup (OnUnloadModule) should handle:
        /// - Entity cleanup (NPCs, objects, triggers, waypoints)
        /// - Area unloading
        /// - Resource freeing
        /// - Module state flag clearing
        /// 
        /// No-op if no module is currently loaded.
        /// </remarks>
        public virtual void UnloadModule()
        {
            if (_currentModuleName != null)
            {
                // Engine-specific cleanup (entities, areas, resources)
                OnUnloadModule();

                // Common state reset (all engines)
                _currentModuleName = null;
                _currentArea = null;
                _currentNavigationMesh = null;
            }
        }

        /// <summary>
        /// Checks if a module exists and can be loaded (engine-specific implementation required).
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to check.</param>
        /// <returns>True if the module exists and can be loaded, false otherwise.</returns>
        /// <remarks>
        /// Common checking pattern (implemented in subclasses):
        /// - Validates module name (non-null, non-empty)
        /// - Checks if module resource exists in installation
        /// - Engine-specific: Checks for module directory, module info file, or module package
        /// - Returns false for invalid input or missing modules
        /// </remarks>
        public abstract bool HasModule(string moduleName);

        /// <summary>
        /// Called when unloading a module to perform engine-specific cleanup.
        /// </summary>
        /// <remarks>
        /// Engine-specific cleanup should handle:
        /// - Entity cleanup: Remove NPCs, objects, triggers, waypoints from world
        /// - Area unloading: Unload area data and navigation meshes
        /// - Resource freeing: Free module-specific resources
        /// - State flag clearing: Clear engine-specific module state flags
        /// - Event triggering: Trigger module unload events/scripts (if applicable)
        /// 
        /// Called by UnloadModule() before resetting common state.
        /// Base implementation does nothing - subclasses must override for engine-specific cleanup.
        /// </remarks>
        protected abstract void OnUnloadModule();
    }
}

