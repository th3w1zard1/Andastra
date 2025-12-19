using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Engines.Common;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.GFF;

namespace Andastra.Runtime.Games.Aurora
{
    /// <summary>
    /// Aurora Engine (Neverwinter Nights) module loader implementation.
    /// </summary>
    /// <remarks>
    /// Module Loading Process:
    /// - Based on nwmain.exe: CNWSModule::LoadModule (needs Ghidra address verification)
    /// - Based on nwmain.exe: CServerExoApp::LoadModule (needs Ghidra address verification)
    /// - Located via string references: "MODULES" (needs Ghidra verification), "Module.ifo" (needs Ghidra verification)
    /// - Cross-engine: Similar functions in swkotor.exe (Odyssey), daorigins.exe (Eclipse)
    /// - Inheritance: BaseEngineModule (Runtime.Games.Common) implements common module loading/unloading
    ///   - Aurora: AuroraModuleLoader : BaseEngineModule (Runtime.Games.Aurora) - Aurora-specific module file formats (Module.ifo, ARE, GIT, HAK files)
    /// 
    /// Aurora Module Loading Sequence (Reverse Engineered):
    /// 1. Validate module name (non-null, non-empty)
    /// 2. Check if module exists (HasModule) - checks for Module.ifo in module directory or HAK files
    /// 3. Unload current module if one is loaded
    /// 4. Load Module.ifo (module info file) - contains module properties, entry area, area list
    /// 5. Load HAK files (hak packs) - additional module resources
    /// 6. Load entry area (ARE file) - area properties and geometry
    /// 7. Load GIT file for entry area - entity instances (creatures, placeables, doors, triggers, waypoints)
    /// 8. Initialize module state - set module flags, initialize scripting system
    /// 9. Spawn entities from GIT - create creatures, placeables, doors, triggers, waypoints
    /// 10. Set current module/area/navigation mesh
    /// 11. Trigger module load scripts (OnModuleLoad, OnClientEnter)
    /// 
    /// Aurora-Specific Features:
    /// - HAK file support: Module resources can be in HAK (Hak Archive) files
    /// - Module.ifo format: GFF-based module information file
    /// - Tile-based areas: Aurora uses tile-based area construction (different from Odyssey)
    /// - Enhanced scripting: Module-level scripts (OnModuleLoad, OnModuleHeartbeat, OnClientEnter, OnClientLeave)
    /// - Module overrides: Module-specific resource overrides in module directory
    /// 
    /// Resource Loading Precedence (Aurora):
    /// 1. Module overrides (module-specific directory)
    /// 2. HAK files (in load order)
    /// 3. Base game resources
    /// 
    /// Ghidra Reverse Engineering Requirements:
    /// - nwmain.exe: CNWSModule::LoadModule function address and implementation
    /// - nwmain.exe: CServerExoApp::LoadModule function address and implementation
    /// - nwmain.exe: Module state flags and bit patterns
    /// - nwmain.exe: HAK file loading sequence and resource precedence
    /// - nwmain.exe: Module.ifo parsing and module property extraction
    /// - nwmain.exe: Area loading from Module.ifo area list
    /// - nwmain.exe: Entity spawning from GIT files
    /// - nwmain.exe: Module script execution (OnModuleLoad, OnModuleHeartbeat, etc.)
    /// - nwmain.exe: String references: "MODULES", "Module.ifo", "HAK", module state flags
    /// - nwn2main.exe: Similar analysis for Neverwinter Nights 2
    /// 
    /// Module State Management (Aurora):
    /// - ModuleLoaded flag: Set when module resources are loaded
    /// - ModuleRunning flag: Set when module starts running (after entity spawning)
    /// - Module state transitions: Idle -> Loading -> Loaded -> Running -> Unloading -> Idle
    /// - Module flags stored in CServerExoApp or CNWSModule structure (needs Ghidra verification)
    /// 
    /// File Formats (Aurora):
    /// - Module.ifo: GFF format - module information, properties, area list, entry point
    /// - ARE: Area properties file - area geometry, properties, environmental settings
    /// - GIT: Game instance template - entity instances (creatures, placeables, doors, triggers, waypoints)
    /// - HAK: Hak Archive - compressed resource archive (similar to ERF format)
    /// - 2DA: Two-dimensional array - game data tables (appearance, feats, etc.)
    /// 
    /// Cross-Engine Comparison:
    /// - Odyssey: IFO/LYT/VIS/GIT/ARE files, module state flags similar to Aurora
    /// - Aurora: Module.ifo/ARE/GIT/HAK files, tile-based areas, enhanced scripting
    /// - Eclipse: UnrealScript packages, message-based loading, different architecture
    /// - Infinity: ARE/WED/GAM files, simpler module system
    /// </remarks>
    public class AuroraModuleLoader : BaseEngineModule
    {
        private readonly Installation _installation;
        private GFFStruct _currentModuleInfo;
        private AuroraArea _currentAuroraArea;
        private List<string> _loadedHakFiles;

        /// <summary>
        /// Initializes a new instance of the AuroraModuleLoader class.
        /// </summary>
        /// <param name="world">The game world instance.</param>
        /// <param name="resourceProvider">The resource provider for loading module resources.</param>
        /// <exception cref="ArgumentException">Thrown if resource provider is not GameResourceProvider.</exception>
        public AuroraModuleLoader(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
        {
            // Extract Installation from GameResourceProvider
            if (resourceProvider is GameResourceProvider gameResourceProvider)
            {
                _installation = gameResourceProvider.Installation;
            }
            else
            {
                throw new ArgumentException("Resource provider must be GameResourceProvider for Aurora engine", nameof(resourceProvider));
            }

            _loadedHakFiles = new List<string>();
        }

        /// <summary>
        /// Loads an Aurora module by name.
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to load.</param>
        /// <param name="progressCallback">Optional callback for loading progress (0.0 to 1.0).</param>
        /// <returns>Task that completes when the module is loaded.</returns>
        /// <remarks>
        /// Aurora Module Loading Sequence:
        /// 1. Validate module name (0.0 progress)
        /// 2. Check if module exists (HasModule)
        /// 3. Unload current module if loaded
        /// 4. Load Module.ifo (0.1-0.2 progress)
        /// 5. Load HAK files (0.2-0.3 progress)
        /// 6. Load entry area ARE file (0.3-0.5 progress)
        /// 7. Load entry area GIT file (0.5-0.6 progress)
        /// 8. Initialize area and navigation mesh (0.6-0.7 progress)
        /// 9. Spawn entities from GIT (0.7-0.9 progress)
        /// 10. Set current module/area/navigation mesh (0.9 progress)
        /// 11. Trigger module load scripts (0.95 progress)
        /// 12. Complete (1.0 progress)
        /// 
        /// Based on nwmain.exe: CNWSModule::LoadModule implementation pattern.
        /// </remarks>
        public override async Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            }

            progressCallback?.Invoke(0.0f);

            // Check if module exists
            if (!HasModule(moduleName))
            {
                throw new InvalidOperationException($"Module '{moduleName}' does not exist or cannot be loaded");
            }

            // Unload current module if one is loaded
            if (_currentModuleName != null)
            {
                UnloadModule();
            }

            try
            {
                // Load Module.ifo (module information file)
                progressCallback?.Invoke(0.1f);
                _currentModuleInfo = await LoadModuleInfoAsync(moduleName);
                if (_currentModuleInfo == null)
                {
                    throw new InvalidOperationException($"Failed to load Module.ifo for module '{moduleName}'");
                }

                // Load HAK files (hak packs)
                progressCallback?.Invoke(0.2f);
                await LoadHakFilesAsync(moduleName);

                // Get entry area from Module.ifo
                string entryAreaResRef = GetEntryAreaResRef(_currentModuleInfo);
                if (string.IsNullOrEmpty(entryAreaResRef))
                {
                    throw new InvalidOperationException($"Module '{moduleName}' has no entry area specified");
                }

                // Load entry area ARE file
                progressCallback?.Invoke(0.3f);
                byte[] areData = await LoadAreaFileAsync(entryAreaResRef);
                if (areData == null)
                {
                    throw new InvalidOperationException($"Failed to load ARE file for area '{entryAreaResRef}'");
                }

                // Load entry area GIT file
                progressCallback?.Invoke(0.5f);
                byte[] gitData = await LoadGitFileAsync(entryAreaResRef);
                if (gitData == null)
                {
                    throw new InvalidOperationException($"Failed to load GIT file for area '{entryAreaResRef}'");
                }

                // Create Aurora area from ARE and GIT data
                progressCallback?.Invoke(0.6f);
                _currentAuroraArea = new AuroraArea(entryAreaResRef, areData, gitData);

                // Set navigation mesh from area
                if (_currentAuroraArea.NavigationMesh != null)
                {
                    _currentNavigationMesh = _currentAuroraArea.NavigationMesh as NavigationMesh;
                }

                // Spawn entities from GIT (creatures, placeables, doors, triggers, waypoints)
                progressCallback?.Invoke(0.7f);
                await SpawnEntitiesFromGitAsync(_currentAuroraArea, gitData);

                // Set current module/area
                _currentModuleName = moduleName;
                _currentArea = _currentAuroraArea;

                // Trigger module load scripts (OnModuleLoad, OnClientEnter)
                progressCallback?.Invoke(0.95f);
                await TriggerModuleLoadScriptsAsync(moduleName);

                progressCallback?.Invoke(1.0f);
            }
            catch (Exception ex)
            {
                // Clean up on error
                UnloadModule();
                throw new InvalidOperationException($"Failed to load module '{moduleName}': {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Checks if an Aurora module exists and can be loaded.
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to check.</param>
        /// <returns>True if the module exists and can be loaded, false otherwise.</returns>
        /// <remarks>
        /// Aurora Module Existence Check:
        /// - Validates module name (non-null, non-empty)
        /// - Checks for Module.ifo in module directory: MODULES\{moduleName}\Module.ifo
        /// - Also checks HAK files for Module.ifo (Aurora-specific)
        /// - Returns false for invalid input or missing modules
        /// 
        /// Based on nwmain.exe: Module existence checking pattern.
        /// </remarks>
        public override bool HasModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            try
            {
                // Check for Module.ifo in module directory
                string modulePath = _installation.ModulePath();
                string moduleIfoPath = Path.Combine(modulePath, moduleName, "Module.ifo");
                if (File.Exists(moduleIfoPath))
                {
                    return true;
                }

                // Check HAK files for Module.ifo (Aurora-specific)
                // TODO: Implement HAK file checking when HAK file parsing is available
                // For now, check if module directory exists
                string moduleDirPath = Path.Combine(modulePath, moduleName);
                if (Directory.Exists(moduleDirPath))
                {
                    // Module directory exists, assume module is valid
                    // Full validation requires Module.ifo parsing
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Unloads the current Aurora module (engine-specific cleanup).
        /// </summary>
        /// <remarks>
        /// Aurora Module Unloading Sequence:
        /// - Clean up entities (NPCs, objects, triggers, waypoints)
        /// - Unload areas and navigation meshes
        /// - Free HAK file resources
        /// - Clear module info
        /// - Reset module state
        /// 
        /// Called by base class UnloadModule() before resetting common state.
        /// Based on nwmain.exe: Module unloading pattern.
        /// </remarks>
        protected override void OnUnloadModule()
        {
            // Clean up Aurora-specific resources
            if (_currentAuroraArea != null)
            {
                // Clean up area entities (handled by area cleanup)
                _currentAuroraArea = null;
            }

            // Clear module info
            _currentModuleInfo = null;

            // Clear HAK file list
            _loadedHakFiles.Clear();
        }

        /// <summary>
        /// Loads Module.ifo file for the specified module.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <returns>The parsed Module.ifo GFF structure, or null if loading fails.</returns>
        /// <remarks>
        /// Module.ifo Format (Aurora):
        /// - GFF format file containing module information
        /// - Contains: Module properties, entry area, area list, module scripts, etc.
        /// - Loaded from: MODULES\{moduleName}\Module.ifo or HAK files
        /// 
        /// Based on nwmain.exe: Module.ifo loading and parsing.
        /// </remarks>
        private async Task<GFFStruct> LoadModuleInfoAsync(string moduleName)
        {
            try
            {
                // Try to load Module.ifo from module directory
                string modulePath = _installation.ModulePath();
                string moduleIfoPath = Path.Combine(modulePath, moduleName, "Module.ifo");
                
                if (File.Exists(moduleIfoPath))
                {
                    byte[] ifoData = await Task.Run(() => File.ReadAllBytes(moduleIfoPath));
                    var gff = new GFF(ifoData);
                    return gff.Root;
                }

                // TODO: Try loading from HAK files when HAK parsing is available
                // For now, return null if file doesn't exist
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Loads HAK files (hak packs) for the specified module.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <returns>Task that completes when HAK files are loaded.</returns>
        /// <remarks>
        /// HAK Files (Aurora):
        /// - Hak Archive files containing additional module resources
        /// - Loaded in order specified in Module.ifo
        /// - Resources in HAK files have lower precedence than module overrides
        /// 
        /// Based on nwmain.exe: HAK file loading sequence.
        /// TODO: Implement HAK file loading when HAK file parsing is available.
        /// </remarks>
        private async Task LoadHakFilesAsync(string moduleName)
        {
            // TODO: Load HAK files from Module.ifo HAK list
            // For now, this is a placeholder
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets the entry area resource reference from Module.ifo.
        /// </summary>
        /// <param name="moduleInfo">The Module.ifo GFF structure.</param>
        /// <returns>The entry area resource reference, or null if not found.</returns>
        /// <remarks>
        /// Entry Area (Aurora):
        /// - Specified in Module.ifo structure
        /// - First area loaded when module starts
        /// - Player spawns in entry area
        /// 
        /// Based on nwmain.exe: Entry area extraction from Module.ifo.
        /// </remarks>
        private string GetEntryAreaResRef(GFFStruct moduleInfo)
        {
            if (moduleInfo == null)
            {
                return null;
            }

            // TODO: Extract entry area from Module.ifo structure
            // Module.ifo structure needs Ghidra analysis to determine exact field names
            // For now, return null (needs implementation)
            return null;
        }

        /// <summary>
        /// Loads an ARE file for the specified area.
        /// </summary>
        /// <param name="areaResRef">The area resource reference.</param>
        /// <returns>The ARE file data, or null if loading fails.</returns>
        /// <remarks>
        /// ARE File (Aurora):
        /// - Area properties file containing area geometry, properties, environmental settings
        /// - Loaded from: Module directory, HAK files, or base resources
        /// - Resource precedence: Module override -> HAK files -> Base resources
        /// 
        /// Based on nwmain.exe: ARE file loading.
        /// </remarks>
        private async Task<byte[]> LoadAreaFileAsync(string areaResRef)
        {
            try
            {
                // Try to load ARE file using resource provider
                byte[] areData = _resourceProvider.LoadResource(areaResRef, "ARE");
                if (areData != null && areData.Length > 0)
                {
                    return await Task.FromResult(areData);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Loads a GIT file for the specified area.
        /// </summary>
        /// <param name="areaResRef">The area resource reference.</param>
        /// <returns>The GIT file data, or null if loading fails.</returns>
        /// <remarks>
        /// GIT File (Aurora):
        /// - Game instance template containing entity instances
        /// - Contains: Creatures, placeables, doors, triggers, waypoints, sounds
        /// - Loaded from: Module directory, HAK files, or base resources
        /// - Resource precedence: Module override -> HAK files -> Base resources
        /// 
        /// Based on nwmain.exe: GIT file loading.
        /// </remarks>
        private async Task<byte[]> LoadGitFileAsync(string areaResRef)
        {
            try
            {
                // Try to load GIT file using resource provider
                byte[] gitData = _resourceProvider.LoadResource(areaResRef, "GIT");
                if (gitData != null && gitData.Length > 0)
                {
                    return await Task.FromResult(gitData);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Spawns entities from GIT file data.
        /// </summary>
        /// <param name="area">The area to spawn entities in.</param>
        /// <param name="gitData">The GIT file data.</param>
        /// <returns>Task that completes when entities are spawned.</returns>
        /// <remarks>
        /// Entity Spawning (Aurora):
        /// - Parses GIT file to extract entity instances
        /// - Creates entities: Creatures, placeables, doors, triggers, waypoints, sounds
        /// - Adds entities to world and area
        /// 
        /// Based on nwmain.exe: Entity spawning from GIT files.
        /// TODO: Implement GIT parsing and entity spawning when GIT format parsing is available.
        /// </remarks>
        private async Task SpawnEntitiesFromGitAsync(AuroraArea area, byte[] gitData)
        {
            // TODO: Parse GIT file and spawn entities
            // GIT format parsing needs to be implemented
            // For now, this is a placeholder
            await Task.CompletedTask;
        }

        /// <summary>
        /// Triggers module load scripts (OnModuleLoad, OnClientEnter).
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <returns>Task that completes when scripts are triggered.</returns>
        /// <remarks>
        /// Module Scripts (Aurora):
        /// - OnModuleLoad: Called when module is loaded
        /// - OnClientEnter: Called when client enters module
        /// - OnModuleHeartbeat: Called periodically while module is running
        /// 
        /// Based on nwmain.exe: Module script execution.
        /// TODO: Implement script execution when scripting system is available.
        /// </remarks>
        private async Task TriggerModuleLoadScriptsAsync(string moduleName)
        {
            // TODO: Execute module scripts (OnModuleLoad, OnClientEnter)
            // Script execution needs scripting system integration
            // For now, this is a placeholder
            await Task.CompletedTask;
        }

        /// <summary>
        /// Gets the current Aurora area (Aurora-specific).
        /// </summary>
        [CanBeNull]
        public AuroraArea CurrentAuroraArea
        {
            get { return _currentAuroraArea; }
        }

        /// <summary>
        /// Gets the current Module.ifo structure (Aurora-specific).
        /// </summary>
        [CanBeNull]
        public GFFStruct CurrentModuleInfo
        {
            get { return _currentModuleInfo; }
        }

        /// <summary>
        /// Gets the list of loaded HAK files (Aurora-specific).
        /// </summary>
        public IReadOnlyList<string> LoadedHakFiles
        {
            get { return _loadedHakFiles.AsReadOnly(); }
        }
    }
}

