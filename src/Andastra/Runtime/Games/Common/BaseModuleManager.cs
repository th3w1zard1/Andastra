using System;
using System.Collections.Generic;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Runtime.Games.Common
{
    /// <summary>
    /// Base implementation of module management shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Module Manager Implementation:
    /// - Common module loading and transition framework
    /// - Handles area loading, entity spawning, resource management
    /// - Provides foundation for engine-specific module systems
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Basic module loading with ARE/GIT files
    /// - swkotor2.exe: Enhanced module system with transitions
    /// - nwmain.exe: Aurora module management
    /// - daorigins.exe: Eclipse module streaming
    /// - DragonAge2.exe: Eclipse module management with message-based loading
    ///   Module Loading: LoadModuleMessage @ 0x00bf5df8 (UnrealScript message class for module loading)
    ///   Module Directories: MODULES: @ 0x00bf5d10, WRITE_MODULES: @ 0x00bf5d24 (module directory identifiers)
    ///   Module Data Structures: ModuleID @ 0x00be9688, ModuleStartupInfo @ 0x00bebb64, ModuleInfoList @ 0x00bfa278
    ///   Module Functions: GetMainModuleName @ 0x00c0ed00, GetCurrentModuleName @ 0x00c0ed24
    ///   Module Class: CModule @ 0x00c236b4 (core module class managing module state, areas, and resources)
    ///   Module Path Resolution: lpModuleName_00fd0250 @ 0x00fd0250 (resolves module paths from MODULES directory, Core directory, executable directory)
    ///   Architecture: UnrealScript message-based module loading via CClientExoApp::HandleMessage(LoadModuleMessage)
    ///   Module Loading Flow: LoadModuleMessage sent to CClientExoApp -> Module path resolution -> Load module.rim -> Initialize ModuleStartupInfo -> Add to ModuleInfoList
    ///   Module Format: .rim files (Resource Information Manifest) containing area data, scripts, and resources in MODULES\{moduleName}\ directory
    ///   Module Management: Uses ModuleInfoList to track loaded modules, ModuleStartupInfo for initialization data, ModuleID for identification
    ///   Module Path Resolution: Checks MODULES\{moduleName}, then Core\{moduleName}, then executable directory using Windows API (PathFileExistsW, PathIsDirectoryW, GetModuleFileNameW)
    ///   Differences from DA:O: Message-based loading (LoadModuleMessage) vs direct LoadModule call, same .rim file format and MODULES directory structure
    /// - Common concepts: Module definition, area loading, entity spawning, transitions
    ///
    /// Common functionality across engines:
    /// - Module loading and unloading
    /// - Area management within modules
    /// - Entity spawning and cleanup
    /// - Resource loading for modules
    /// - Transition handling between modules
    /// - Save/load integration
    /// - Performance considerations for large modules
    /// </remarks>
    [PublicAPI]
    public abstract class BaseModuleManager
    {
        protected readonly IWorld _world;
        protected IModule _currentModule;
        protected readonly Dictionary<string, IModule> _loadedModules = new Dictionary<string, IModule>();

        protected BaseModuleManager(IWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// Gets the currently loaded module.
        /// </summary>
        public IModule CurrentModule => _currentModule;

        /// <summary>
        /// Loads a module by name.
        /// </summary>
        /// <remarks>
        /// Common module loading framework.
        /// Loads module definition, areas, entities.
        /// Handles resource loading and initialization.
        /// Engine-specific subclasses implement format-specific loading.
        /// </remarks>
        public virtual void LoadModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));

            // Check if already loaded
            if (_loadedModules.TryGetValue(moduleName, out var existingModule))
            {
                if (_currentModule == existingModule)
                    return; // Already current

                SetCurrentModule(existingModule);
                return;
            }

            // Load new module
            var module = LoadModuleFromResources(moduleName);
            if (module == null)
                throw new InvalidOperationException($"Failed to load module: {moduleName}");

            _loadedModules[moduleName] = module;
            SetCurrentModule(module);
        }

        /// <summary>
        /// Unloads a module.
        /// </summary>
        /// <remarks>
        /// Cleans up module resources and entities.
        /// Optionally keeps in cache for quick reloading.
        /// Common across all engines.
        /// </remarks>
        public virtual void UnloadModule(string moduleName, bool keepInCache = false)
        {
            if (!_loadedModules.TryGetValue(moduleName, out var module))
                return;

            if (_currentModule == module)
            {
                // Unload current module
                UnloadCurrentModule();
            }

            if (!keepInCache)
            {
                _loadedModules.Remove(moduleName);
                DisposeModule(module);
            }
        }

        /// <summary>
        /// Transitions to a new module.
        /// </summary>
        /// <remarks>
        /// Handles module transitions with proper cleanup and loading.
        /// Supports transition effects and loading screens.
        /// Engine-specific transition mechanics.
        /// </remarks>
        public virtual void TransitionToModule(string moduleName, string entryPoint = null)
        {
            // Unload current module with transition effects
            if (_currentModule != null)
            {
                OnModuleTransitionOut(_currentModule);
            }

            // Load new module
            LoadModule(moduleName);

            // Set entry point if specified
            if (!string.IsNullOrEmpty(entryPoint) && _currentModule != null)
            {
                SetEntryPoint(entryPoint);
            }

            // Initialize new module
            if (_currentModule != null)
            {
                OnModuleTransitionIn(_currentModule);
            }
        }

        /// <summary>
        /// Updates the module manager.
        /// </summary>
        /// <remarks>
        /// Handles module-specific updates.
        /// Processes loading, transitions, cleanup.
        /// Called each frame.
        /// </remarks>
        public virtual void Update(float deltaTime)
        {
            // Update current module - modules don't have Update method, handled by world

            // Process any pending module operations
            ProcessPendingOperations();
        }

        /// <summary>
        /// Saves module state.
        /// </summary>
        /// <remarks>
        /// Saves current module and loaded module states.
        /// Includes entity positions, area states, etc.
        /// Common across all engines.
        /// </remarks>
        public virtual ModuleSaveData SaveState()
        {
            return new ModuleSaveData
            {
                CurrentModuleName = _currentModule?.ResRef,
                LoadedModuleNames = new List<string>(_loadedModules.Keys)
            };
        }

        /// <summary>
        /// Loads module state.
        /// </summary>
        /// <remarks>
        /// Restores module loading state from save data.
        /// Reloads necessary modules and sets current module.
        /// </remarks>
        public virtual void LoadState(ModuleSaveData saveData)
        {
            // Load previously loaded modules
            foreach (var moduleName in saveData.LoadedModuleNames)
            {
                if (!_loadedModules.ContainsKey(moduleName))
                {
                    try
                    {
                        var module = LoadModuleFromResources(moduleName);
                        if (module != null)
                        {
                            _loadedModules[moduleName] = module;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue
                        System.Diagnostics.Debug.WriteLine($"Failed to reload module {moduleName}: {ex.Message}");
                    }
                }
            }

            // Set current module
            if (!string.IsNullOrEmpty(saveData.CurrentModuleName))
            {
                if (_loadedModules.TryGetValue(saveData.CurrentModuleName, out var module))
                {
                    SetCurrentModule(module);
                }
            }
        }

        /// <summary>
        /// Loads a module from resources.
        /// </summary>
        /// <remarks>
        /// Engine-specific module loading implementation.
        /// Subclasses implement format-specific loading (IFO, MOD, etc.).
        /// </remarks>
        protected abstract IModule LoadModuleFromResources(string moduleName);

        /// <summary>
        /// Sets the current module.
        /// </summary>
        /// <remarks>
        /// Updates world state for the new module.
        /// Handles area loading and entity spawning.
        /// </remarks>
        protected virtual void SetCurrentModule(IModule module)
        {
            var previousModule = _currentModule;
            _currentModule = module;

            OnCurrentModuleChanged(previousModule, module);
        }

        /// <summary>
        /// Unloads the current module.
        /// </summary>
        /// <remarks>
        /// Cleans up current module resources.
        /// Prepares for new module loading.
        /// </remarks>
        protected virtual void UnloadCurrentModule()
        {
            if (_currentModule != null)
            {
                OnModuleUnloading(_currentModule);
                // Note: Module stays in cache, just not current
            }
        }

        /// <summary>
        /// Sets the player entry point in the current module.
        /// </summary>
        /// <remarks>
        /// Positions player at specified entry point.
        /// Handles transition effects and camera positioning.
        /// </remarks>
        protected virtual void SetEntryPoint(string entryPoint)
        {
            // Default implementation - subclasses can override
        }

        /// <summary>
        /// Processes pending module operations.
        /// </summary>
        /// <remarks>
        /// Handles asynchronous loading, cleanup, etc.
        /// Called during update loop.
        /// </remarks>
        protected virtual void ProcessPendingOperations()
        {
            // Default: no pending operations
        }

        /// <summary>
        /// Disposes of a module.
        /// </summary>
        /// <remarks>
        /// Complete cleanup of module resources.
        /// Called when module is permanently unloaded.
        /// </remarks>
        protected virtual void DisposeModule(IModule module)
        {
            if (module is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Called when the current module changes.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific module change logic.
        /// Subclasses can override for additional handling.
        /// </remarks>
        protected virtual void OnCurrentModuleChanged(IModule previousModule, IModule newModule)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when a module is being unloaded.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific cleanup logic.
        /// Subclasses can override for additional cleanup.
        /// </remarks>
        protected virtual void OnModuleUnloading(IModule module)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called during module transition out.
        /// </summary>
        /// <remarks>
        /// Hook for transition effects and cleanup.
        /// Subclasses can override for transition handling.
        /// </remarks>
        protected virtual void OnModuleTransitionOut(IModule module)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called during module transition in.
        /// </summary>
        /// <remarks>
        /// Hook for initialization and transition effects.
        /// Subclasses can override for transition handling.
        /// </remarks>
        protected virtual void OnModuleTransitionIn(IModule module)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Gets module manager statistics.
        /// </summary>
        /// <remarks>
        /// Returns module loading and memory statistics.
        /// Useful for debugging and performance monitoring.
        /// </remarks>
        public virtual ModuleStats GetStats()
        {
            return new ModuleStats
            {
                LoadedModuleCount = _loadedModules.Count,
                CurrentModuleName = _currentModule?.ResRef
            };
        }
    }

    /// <summary>
    /// Module save data structure.
    /// </summary>
    public class ModuleSaveData
    {
        /// <summary>
        /// Name of the current module.
        /// </summary>
        public string CurrentModuleName { get; set; }

        /// <summary>
        /// Names of all loaded modules.
        /// </summary>
        public List<string> LoadedModuleNames { get; set; } = new List<string>();
    }

    /// <summary>
    /// Module manager statistics.
    /// </summary>
    public struct ModuleStats
    {
        /// <summary>
        /// Number of loaded modules.
        /// </summary>
        public int LoadedModuleCount;

        /// <summary>
        /// Name of the current module.
        /// </summary>
        public string CurrentModuleName;
    }
}
