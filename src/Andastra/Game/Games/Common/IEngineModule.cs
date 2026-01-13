using System;
using System.Threading.Tasks;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Navigation;
using JetBrains.Annotations;

namespace Andastra.Game.Engines.Common
{
    /// <summary>
    /// Base interface for module management across all engines.
    /// </summary>
    /// <remarks>
    /// Engine Module Interface:
    /// - Common contract shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Defines the interface for module management that all engines must provide
    /// - Engine-specific implementations must be in concrete classes (OdysseyModuleLoader, AuroraModuleLoader, EclipseModuleLoader, InfinityModuleLoader)
    /// 
    /// Cross-Engine Module Management Patterns (Reverse Engineered):
    /// 
    /// Common Patterns Identified Across All Engines:
    /// 1. Module Loading Sequence:
    ///    - All engines: Validate module name -> Check module existence -> Load module resources -> Initialize module state -> Set current module
    ///    - Odyssey (swkotor.exe, swkotor2.exe): IFO -> LYT -> VIS -> GIT -> ARE -> Spawn entities -> Set ModuleLoaded flag
    ///    - Aurora (nwmain.exe): Module.ifo -> Area files -> HAK files -> Entity spawning -> Module state initialization
    ///    - Eclipse (daorigins.exe, DragonAge2.exe, , ): UnrealScript LoadModuleMessage -> Package loading -> Area streaming
    ///    - Infinity (.exe, .exe, .exe): ARE -> WED -> GAM -> BIF resources -> Entity spawning
    /// 
    /// 2. Module State Management:
    ///    - All engines track: Current module name, Current area, Navigation mesh, Module loaded state
    ///    - State transitions: Idle -> Loading -> Loaded -> Running -> Unloading -> Idle
    ///    - Module flags: ModuleLoaded, ModuleRunning (common across Odyssey/Aurora)
    /// 
    /// 3. Resource Loading:
    ///    - All engines: Module resources loaded from installation-specific paths (MODULES directory)
    ///    - Resource precedence: Override -> Module-specific -> Base game resources
    ///    - Common resource types: Scripts, Areas, Entities, Navigation data
    /// 
    /// 4. Module Unloading:
    ///    - All engines: Clean up entities -> Unload areas -> Clear navigation mesh -> Reset module state -> Free resources
    ///    - Entity cleanup order: Player entities last (if applicable), then NPCs, then objects, then triggers/waypoints
    /// 
    /// 5. Module Existence Checking:
    ///    - All engines: Check if module directory/resource exists in installation
    ///    - Odyssey: Check for module.ifo in MODULES:\{moduleName}\
    ///    - Aurora: Check for Module.ifo in module directory or HAK files
    ///    - Eclipse: Check for module package/rim file existence
    ///    - Infinity: Check for ARE file existence
    /// 
    /// Reverse Engineering Notes:
    /// - Engine-specific implementations document executable-specific addresses and details.
    /// - Base interface documents only common patterns across all engines.
    /// 
    /// Inheritance Structure:
    /// - BaseEngineModule (Runtime.Games.Common): Common module loading/unloading, state management, resource tracking
    ///   - OdysseyModuleLoader : BaseEngineModule (Runtime.Games.Odyssey) - IFO/LYT/VIS/GIT/ARE file formats
    ///   - AuroraModuleLoader : BaseEngineModule (Runtime.Games.Aurora) - Module.ifo, HAK files, Aurora-specific formats
    ///   - EclipseModuleLoader : BaseEngineModule (Runtime.Games.Eclipse) - UnrealScript packages, .rim files
    ///   - InfinityModuleLoader : BaseEngineModule (Runtime.Games.Infinity) - ARE/WED/GAM file formats
    /// </remarks>
    public interface IEngineModule
    {
        /// <summary>
        /// Gets the current module name.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Tracks the currently loaded module's resource reference name.
        /// Returns null when no module is loaded.
        /// </remarks>
        [CanBeNull]
        string CurrentModuleName { get; }

        /// <summary>
        /// Gets the current area.
        /// </summary>
        /// <remarks>
        /// Common across all engines: The active area within the current module.
        /// Typically the entry area or the area the player is currently in.
        /// Returns null when no module/area is loaded.
        /// </remarks>
        [CanBeNull]
        IArea CurrentArea { get; }

        /// <summary>
        /// Gets the current navigation mesh.
        /// </summary>
        /// <remarks>
        /// Common across all engines: Navigation/walkmesh data for pathfinding in the current area.
        /// Returns null when no navigation mesh is available.
        /// </remarks>
        [CanBeNull]
        NavigationMesh CurrentNavigationMesh { get; }

        /// <summary>
        /// Loads a module by name.
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to load.</param>
        /// <param name="progressCallback">Optional callback for loading progress (0.0 to 1.0).</param>
        /// <returns>Task that completes when the module is loaded.</returns>
        /// <remarks>
        /// Common loading sequence across all engines:
        /// 1. Validate module name (non-null, non-empty)
        /// 2. Check if module exists (HasModule)
        /// 3. Unload current module if one is loaded
        /// 4. Load module resources (engine-specific file formats)
        /// 5. Initialize module state
        /// 6. Load entry area (if applicable)
        /// 7. Spawn entities from module data
        /// 8. Set current module/area/navigation mesh
        /// 9. Trigger module load events/scripts
        /// 
        /// Progress reporting: Engines typically report progress at key milestones:
        /// - 0.0: Start loading
        /// - 0.1-0.3: Resource loading
        /// - 0.4-0.6: Area initialization
        /// - 0.7-0.9: Entity spawning
        /// - 1.0: Complete
        /// </remarks>
        Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null);

        /// <summary>
        /// Unloads the current module.
        /// </summary>
        /// <remarks>
        /// Common unloading sequence across all engines:
        /// 1. Clean up entities (NPCs, objects, triggers, waypoints)
        /// 2. Unload areas and navigation meshes
        /// 3. Free module resources
        /// 4. Reset module state (clear current module name, area, navigation mesh)
        /// 5. Trigger module unload events/scripts (if applicable)
        /// 
        /// No-op if no module is currently loaded.
        /// </remarks>
        void UnloadModule();

        /// <summary>
        /// Checks if a module exists and can be loaded.
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to check.</param>
        /// <returns>True if the module exists and can be loaded, false otherwise.</returns>
        /// <remarks>
        /// Common checking pattern across all engines:
        /// - Validates module name (non-null, non-empty)
        /// - Checks if module resource exists in installation
        /// - Engine-specific: Checks for module directory, module info file, or module package
        /// - Returns false for invalid input or missing modules
        /// </remarks>
        bool HasModule(string moduleName);
    }
}


