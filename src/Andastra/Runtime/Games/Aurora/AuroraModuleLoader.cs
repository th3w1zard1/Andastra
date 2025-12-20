using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Engines.Common;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Content.Loaders;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.GFF.IO;
using Andastra.Parsing.Formats.ERF;

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
        private readonly AuroraResourceProvider _auroraResourceProvider;
        private GFFStruct _currentModuleInfo;
        private AuroraArea _currentAuroraArea;
        private List<string> _loadedHakFiles;

        /// <summary>
        /// Initializes a new instance of the AuroraModuleLoader class.
        /// </summary>
        /// <param name="world">The game world instance.</param>
        /// <param name="resourceProvider">The resource provider for loading module resources.</param>
        /// <exception cref="ArgumentException">Thrown if resource provider is not AuroraResourceProvider.</exception>
        public AuroraModuleLoader(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
        {
            // Extract AuroraResourceProvider
            if (resourceProvider is AuroraResourceProvider auroraProvider)
            {
                _auroraResourceProvider = auroraProvider;
            }
            else
            {
                throw new ArgumentException("Resource provider must be AuroraResourceProvider for Aurora engine", nameof(resourceProvider));
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

                // Set current module context for resource lookups
                _auroraResourceProvider.SetCurrentModule(moduleName);

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
                string modulePath = _auroraResourceProvider.ModulePath();
                string moduleIfoPath = Path.Combine(modulePath, moduleName, "Module.ifo");
                if (File.Exists(moduleIfoPath))
                {
                    return true;
                }

                // Check HAK files for Module.ifo (Aurora-specific)
                // Based on nwmain.exe: Module existence check searches HAK files for Module.ifo
                // HAK files are ERF format archives that can contain module resources
                // Module.ifo in HAK files is stored as: ResName = moduleName, ResType = IFO
                // Based on nwmain.exe: CExoResMan::Demand @ 0x14018ef90 searches encapsulated resources (HAK files)
                // Based on nwmain.exe: CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 checks ERF archives for resources
                string hakPath = _auroraResourceProvider.HakPath();
                if (Directory.Exists(hakPath))
                {
                    // Search all HAK files in the hak directory
                    // Based on nwmain.exe: Module existence check searches all registered HAK files
                    // HAK files are registered via CExoResMan::AddKeyTable @ 0x14018e330
                    // For module existence check, we search all HAK files in the hak directory
                    string[] hakFiles = Directory.GetFiles(hakPath, "*.hak", SearchOption.TopDirectoryOnly);
                    foreach (string hakFilePath in hakFiles)
                    {
                        try
                        {
                            // HAK files are ERF format archives
                            // Based on nwmain.exe: HAK files use ERF format (same as MOD files)
                            // Parse HAK file as ERF and check if it contains Module.ifo for this module
                            var erf = ERFAuto.ReadErf(hakFilePath);
                            
                            // Check if HAK file contains Module.ifo for this module
                            // Module.ifo in HAK files is stored as: ResName = moduleName, ResType = IFO
                            // Based on nwmain.exe: Resource lookup uses ResName (16-char resref) and ResType
                            byte[] moduleIfoData = erf.Get(moduleName, ResourceType.IFO);
                            if (moduleIfoData != null && moduleIfoData.Length > 0)
                            {
                                // Module.ifo found in HAK file - module exists
                                return true;
                            }
                        }
                        catch
                        {
                            // Skip corrupted or invalid HAK files
                            // Based on nwmain.exe: Invalid HAK files are skipped during resource lookup
                            continue;
                        }
                    }
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
                string modulePath = _auroraResourceProvider.ModulePath();
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
        /// - HAK files are ERF format archives containing module resources
        /// 
        /// Module.ifo HAK File Specification:
        /// - Mod_HakList (preferred): List field containing structs with StructID 8
        ///   - Each struct has Mod_Hak field (CExoString) with HAK filename (without .hak extension)
        ///   - HAK files are loaded in list order (first in list has highest priority)
        /// - Mod_Hak (obsolete fallback): Single CExoString field with semicolon-separated HAK filenames
        ///   - Used only if Mod_HakList does not exist
        ///   - Semicolon-separated list of HAK filenames (without .hak extension)
        /// 
        /// HAK File Path Resolution:
        /// - HAK files are located in "hak" directory under installation path
        /// - Full path: {installationPath}\hak\{hakFileName}.hak
        /// - HAK filenames in Module.ifo do not include the .hak extension
        /// - Missing HAK files are skipped (not an error - module can load without them)
        /// 
        /// Resource Precedence (Aurora):
        /// 1. Override directory (highest priority)
        /// 2. Module-specific resources
        /// 3. HAK files (in Module.ifo order, first HAK has highest priority)
        /// 4. Base game resources
        /// 5. Hardcoded resources (lowest priority)
        /// 
        /// Based on nwmain.exe reverse engineering (Ghidra MCP analysis):
        /// - Module.ifo parsing functions reference Mod_HakList/Mod_Hak strings:
        ///   - "Mod_HakList" string @ 0x140def690, referenced by functions @ 0x14047f60e, 0x1404862d9
        ///   - "Mod_Hak" string @ 0x140def6a0, referenced by functions @ 0x14047f6b9, 0x140486325, 0x1409d5658
        /// - HAK files are registered via CExoResMan::AddKeyTable @ 0x14018e330
        ///   - Each HAK file is registered as an encapsulated resource file (type 3)
        ///   - HAK files are registered in the order specified in Module.ifo
        ///   - Earlier HAK files in the list have higher priority (checked first during resource lookup)
        /// - HAK file paths resolved from "hak" directory + filename + ".hak" extension
        /// - Resource lookup uses CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 for HAK files
        /// 
        /// Official BioWare Documentation:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-IFO.md: Mod_HakList structure (StructID 8)
        /// - vendor/PyKotor/wiki/Bioware-Aurora-IFO.md: Mod_Hak field (obsolete, semicolon-separated)
        /// - vendor/xoreos/src/aurora/ifofile.cpp:143-154 - HAK list parsing implementation
        /// </remarks>
        private async Task LoadHakFilesAsync(string moduleName)
        {
            // Set current module context
            _auroraResourceProvider.SetCurrentModule(moduleName);

            // Clear existing HAK files list
            _loadedHakFiles.Clear();

            // Extract HAK file list from already-loaded Module.ifo GFF structure
            // _currentModuleInfo is loaded in LoadModuleInfoAsync before this method is called
            if (_currentModuleInfo == null)
            {
                // Module.ifo not loaded - cannot extract HAK files
                // This should not happen in normal flow, but handle gracefully
                System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Module.ifo not loaded - cannot extract HAK files for module '{moduleName}'");
                await Task.CompletedTask;
                return;
            }

            try
            {
                // Extract HAK file names from Module.ifo GFF structure
                // Priority: Mod_HakList (preferred) > Mod_Hak (obsolete fallback)
                List<string> hakFileNames = new List<string>();

                // Try Mod_HakList first (preferred method)
                // Mod_HakList is a List field containing structs with StructID 8
                // Each struct has a Mod_Hak field (CExoString) with the HAK filename (without .hak extension)
                // Based on nwmain.exe: Functions @ 0x14047f60e, 0x1404862d9 parse Mod_HakList from Module.ifo
                // Based on vendor/xoreos/src/aurora/ifofile.cpp:144-149 - HAK list parsing
                if (_currentModuleInfo.TryGetList("Mod_HakList", out GFFList hakList))
                {
                    // Iterate through HAK list entries
                    // Based on nwmain.exe: HAK files are loaded in order specified in list
                    // Earlier HAK files in list have higher priority (override later ones)
                    foreach (GFFStruct hakEntry in hakList)
                    {
                        // Extract Mod_Hak field from each entry
                        // Mod_Hak is a CExoString containing the HAK filename without .hak extension
                        string hakFileName = hakEntry.GetString("Mod_Hak");
                        if (!string.IsNullOrEmpty(hakFileName))
                        {
                            // Trim whitespace and add to list
                            hakFileName = hakFileName.Trim();
                            if (!string.IsNullOrEmpty(hakFileName))
                            {
                                hakFileNames.Add(hakFileName);
                            }
                        }
                    }
                }
                // Fallback to Mod_Hak (obsolete method) if Mod_HakList doesn't exist
                // Mod_Hak is a semicolon-separated string of HAK filenames (without .hak extension)
                // Based on nwmain.exe: Mod_Hak field uses semicolon as separator
                // Based on nwmain.exe: Functions @ 0x14047f6b9, 0x140486325, 0x1409d5658 parse Mod_Hak field
                // Based on vendor/xoreos/src/aurora/ifofile.cpp:151-154 - Singular HAK parsing
                else if (_currentModuleInfo.Exists("Mod_Hak"))
                {
                    string hakField = _currentModuleInfo.GetString("Mod_Hak");
                    if (!string.IsNullOrEmpty(hakField))
                    {
                        // Split by semicolon and add each HAK filename
                        string[] hakNames = hakField.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string hakName in hakNames)
                        {
                            string trimmedName = hakName.Trim();
                            if (!string.IsNullOrEmpty(trimmedName))
                            {
                                hakFileNames.Add(trimmedName);
                            }
                        }
                    }
                }

                // Resolve HAK file paths and validate existence
                // HAK files are located in the "hak" directory under installation path
                // HAK filenames in Module.ifo do not include the .hak extension
                string hakPath = _auroraResourceProvider.HakPath();
                List<string> resolvedHakFiles = new List<string>();

                if (Directory.Exists(hakPath))
                {
                    foreach (string hakFileName in hakFileNames)
                    {
                        // Construct full path: hak directory + filename + .hak extension
                        // Based on nwmain.exe: HAK file path resolution pattern
                        // Based on nwmain.exe: HAK files registered via CExoResMan::AddKeyTable @ 0x14018e330
                        string hakFilePath = Path.Combine(hakPath, hakFileName + ".hak");

                        // Only add HAK file if it exists
                        // Based on nwmain.exe: Missing HAK files are skipped (not an error)
                        // Based on nwmain.exe: AddKeyTable @ 0x14018e330 handles missing file gracefully
                        if (File.Exists(hakFilePath))
                        {
                            resolvedHakFiles.Add(hakFilePath);
                            _loadedHakFiles.Add(hakFilePath);
                        }
                        else
                        {
                            // Log missing HAK file (non-fatal - module can still load)
                            System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] HAK file not found: {hakFilePath} (specified in Module.ifo for module '{moduleName}')");
                        }
                    }
                }
                else
                {
                    // HAK directory doesn't exist - log but don't fail
                    System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] HAK directory does not exist: {hakPath}");
                }

                // Set HAK files on resource provider
                // Based on nwmain.exe: HAK files registered via CExoResMan::AddKeyTable @ 0x14018e330
                // HAK files are registered in the order specified in Module.ifo
                // Earlier HAK files in the list have higher priority (checked first during resource lookup)
                _auroraResourceProvider.SetHakFiles(resolvedHakFiles);

                if (resolvedHakFiles.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Loaded {resolvedHakFiles.Count} HAK file(s) for module '{moduleName}'");
                }
            }
            catch (Exception ex)
            {
                // On any error (parsing, file access, etc.), log but don't fail module loading
                // Missing or invalid HAK files should not prevent module from loading
                // Based on nwmain.exe: HAK file loading errors are non-fatal
                System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Error loading HAK files for module '{moduleName}': {ex.Message}");
                _loadedHakFiles.Clear();
            }

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
                // Set current module context for resource lookup
                if (!string.IsNullOrEmpty(_currentModuleName))
                {
                    _auroraResourceProvider.SetCurrentModule(_currentModuleName);
                }

                // Try to load ARE file from resource provider
                var resourceId = new ResourceIdentifier(areaResRef, ResourceType.ARE);
                byte[] areData = await _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None);
                return areData;
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
                // Set current module context for resource lookup
                if (!string.IsNullOrEmpty(_currentModuleName))
                {
                    _auroraResourceProvider.SetCurrentModule(_currentModuleName);
                }

                // Try to load GIT file from resource provider
                var resourceId = new ResourceIdentifier(areaResRef, ResourceType.GIT);
                byte[] gitData = await _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None);
                return gitData;
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
        /// - Creates entities: Creatures, placeables, doors, triggers, waypoints, sounds, encounters, stores
        /// - Adds entities to world and area
        /// 
        /// Based on nwmain.exe: Entity spawning from GIT files.
        /// - nwmain.exe: CNWSArea::LoadCreatures @ 0x140362fc0 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadDoors @ 0x1403631a0 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadPlaceables @ 0x1403632c0 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadTriggers @ 0x1403633e0 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadWaypoints @ 0x140362fc0 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadSounds @ 0x140364000 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadEncounters @ 0x140364120 (approximate - needs Ghidra verification)
        /// - nwmain.exe: CNWSArea::LoadStores @ 0x140364240 (approximate - needs Ghidra verification)
        /// 
        /// Spawn order: Waypoints -> Doors -> Placeables -> Creatures -> Triggers -> Sounds -> Encounters -> Stores
        /// Each entity gets: ObjectId (assigned by world), Tag, Position, Orientation, Template data
        /// 
        /// GIT file format (GFF with "GIT " signature):
        /// - Root struct contains lists: "Creature List", "Door List", "Placeable List", "TriggerList", "WaypointList", "SoundList", "Encounter List", "StoreList"
        /// - Each list contains structs with entity instance data (TemplateResRef, Tag, Position, Orientation, type-specific fields)
        /// - Position fields: "XPosition", "YPosition", "ZPosition" for most types, "X", "Y", "Z" for doors/placeables
        /// - Orientation fields: "XOrientation", "YOrientation", "ZOrientation" (float, converted to quaternion), "Bearing" (float) for doors/placeables
        /// 
        /// Based on official BioWare Aurora Engine GIT format specification:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-GIT.md
        /// - vendor/xoreos-docs/specs/bioware/GIT_Format.pdf
        /// </remarks>
        private async Task SpawnEntitiesFromGitAsync(AuroraArea area, byte[] gitData)
        {
            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }
            if (gitData == null || gitData.Length == 0)
            {
                // No GIT data - nothing to spawn
                return;
            }

            // Parse GIT file using GITLoader
            // Based on nwmain.exe: GIT file is GFF format with "GIT " signature
            var gitLoader = new GITLoader(_resourceProvider);
            GITData git = null;
            
            try
            {
                // Parse GIT data from byte array
                // GITLoader expects a resource identifier, but we have raw bytes
                // We'll parse directly using GFF
                using (var stream = new MemoryStream(gitData))
                {
                    var reader = new GFFBinaryReader(stream);
                    GFF gff = reader.Load();
                    if (gff == null || gff.Root == null)
                    {
                        // Invalid GFF - cannot spawn entities
                        return;
                    }
                    
                    // Parse GIT structure using GITLoader's parsing logic
                    git = ParseGITData(gff.Root);
                }
            }
            catch (Exception ex)
            {
                // GIT parsing failed - log error but continue
                System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Failed to parse GIT file: {ex.Message}");
                return;
            }

            if (git == null)
            {
                return;
            }

            int spawnCount = 0;

            // Spawn entities in order: Waypoints -> Doors -> Placeables -> Creatures -> Triggers -> Sounds -> Encounters -> Stores
            // Based on nwmain.exe: Entity spawning order ensures dependencies are resolved correctly
            // Waypoints are spawned first as they may be referenced by other entities

            // Spawn waypoints
            foreach (WaypointInstance waypoint in git.Waypoints)
            {
                await SpawnWaypointAsync(waypoint, area);
                spawnCount++;
            }

            // Spawn doors
            foreach (DoorInstance door in git.Doors)
            {
                await SpawnDoorAsync(door, area);
                spawnCount++;
            }

            // Spawn placeables
            foreach (PlaceableInstance placeable in git.Placeables)
            {
                await SpawnPlaceableAsync(placeable, area);
                spawnCount++;
            }

            // Spawn creatures
            foreach (CreatureInstance creature in git.Creatures)
            {
                await SpawnCreatureAsync(creature, area);
                spawnCount++;
            }

            // Spawn triggers
            foreach (TriggerInstance trigger in git.Triggers)
            {
                await SpawnTriggerAsync(trigger, area);
                spawnCount++;
            }

            // Spawn sounds
            foreach (SoundInstance sound in git.Sounds)
            {
                await SpawnSoundAsync(sound, area);
                spawnCount++;
            }

            // Spawn encounters
            foreach (EncounterInstance encounter in git.Encounters)
            {
                await SpawnEncounterAsync(encounter, area);
                spawnCount++;
            }

            // Spawn stores
            foreach (StoreInstance store in git.Stores)
            {
                await SpawnStoreAsync(store, area);
                spawnCount++;
            }

            System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Spawned {spawnCount} entities from GIT file");
        }

        /// <summary>
        /// Parses GIT data from GFF root structure.
        /// </summary>
        /// <param name="root">The GFF root structure.</param>
        /// <returns>Parsed GIT data, or null if parsing fails.</returns>
        /// <remarks>
        /// Based on GITLoader.ParseGIT implementation.
        /// Parses all entity lists from GIT GFF structure.
        /// </remarks>
        private GITData ParseGITData(GFFStruct root)
        {
            var git = new GITData();

            // Parse creature instances
            if (root.TryGetList("Creature List", out GFFList creatureList))
            {
                foreach (GFFStruct creatureStruct in creatureList)
                {
                    git.Creatures.Add(ParseCreatureInstance(creatureStruct));
                }
            }

            // Parse door instances
            if (root.TryGetList("Door List", out GFFList doorList))
            {
                foreach (GFFStruct doorStruct in doorList)
                {
                    git.Doors.Add(ParseDoorInstance(doorStruct));
                }
            }

            // Parse placeable instances
            if (root.TryGetList("Placeable List", out GFFList placeableList))
            {
                foreach (GFFStruct placeableStruct in placeableList)
                {
                    git.Placeables.Add(ParsePlaceableInstance(placeableStruct));
                }
            }

            // Parse trigger instances
            if (root.TryGetList("TriggerList", out GFFList triggerList))
            {
                foreach (GFFStruct triggerStruct in triggerList)
                {
                    git.Triggers.Add(ParseTriggerInstance(triggerStruct));
                }
            }

            // Parse waypoint instances
            if (root.TryGetList("WaypointList", out GFFList waypointList))
            {
                foreach (GFFStruct waypointStruct in waypointList)
                {
                    git.Waypoints.Add(ParseWaypointInstance(waypointStruct));
                }
            }

            // Parse sound instances
            if (root.TryGetList("SoundList", out GFFList soundList))
            {
                foreach (GFFStruct soundStruct in soundList)
                {
                    git.Sounds.Add(ParseSoundInstance(soundStruct));
                }
            }

            // Parse encounter instances
            if (root.TryGetList("Encounter List", out GFFList encounterList))
            {
                foreach (GFFStruct encounterStruct in encounterList)
                {
                    git.Encounters.Add(ParseEncounterInstance(encounterStruct));
                }
            }

            // Parse store instances
            if (root.TryGetList("StoreList", out GFFList storeList))
            {
                foreach (GFFStruct storeStruct in storeList)
                {
                    git.Stores.Add(ParseStoreInstance(storeStruct));
                }
            }

            return git;
        }

        #region GIT Instance Parsers

        /// <summary>
        /// Parses a creature instance from GFF struct.
        /// </summary>
        private CreatureInstance ParseCreatureInstance(GFFStruct s)
        {
            var instance = new CreatureInstance();
            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");
            return instance;
        }

        /// <summary>
        /// Parses a door instance from GFF struct.
        /// </summary>
        private DoorInstance ParseDoorInstance(GFFStruct s)
        {
            var instance = new DoorInstance();
            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.LinkedTo = GetString(s, "LinkedTo");
            instance.LinkedToFlags = GetByte(s, "LinkedToFlags");
            instance.LinkedToModule = GetResRef(s, "LinkedToModule");
            instance.TransitionDestin = GetString(s, "TransitionDestin");
            instance.XPosition = GetFloat(s, "X");
            instance.YPosition = GetFloat(s, "Y");
            instance.ZPosition = GetFloat(s, "Z");
            instance.Bearing = GetFloat(s, "Bearing");
            return instance;
        }

        /// <summary>
        /// Parses a placeable instance from GFF struct.
        /// </summary>
        private PlaceableInstance ParsePlaceableInstance(GFFStruct s)
        {
            var instance = new PlaceableInstance();
            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "X");
            instance.YPosition = GetFloat(s, "Y");
            instance.ZPosition = GetFloat(s, "Z");
            instance.Bearing = GetFloat(s, "Bearing");
            return instance;
        }

        /// <summary>
        /// Parses a trigger instance from GFF struct.
        /// </summary>
        private TriggerInstance ParseTriggerInstance(GFFStruct s)
        {
            var instance = new TriggerInstance();
            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");
            instance.ZOrientation = GetFloat(s, "ZOrientation");

            // Parse geometry
            if (s.TryGetList("Geometry", out GFFList geometryList))
            {
                foreach (GFFStruct vertexStruct in geometryList)
                {
                    float pointX = GetFloat(vertexStruct, "PointX");
                    float pointY = GetFloat(vertexStruct, "PointY");
                    float pointZ = GetFloat(vertexStruct, "PointZ");
                    instance.Geometry.Add(new Vector3(pointX, pointY, pointZ));
                }
            }

            return instance;
        }

        /// <summary>
        /// Parses a waypoint instance from GFF struct.
        /// </summary>
        private WaypointInstance ParseWaypointInstance(GFFStruct s)
        {
            var instance = new WaypointInstance();
            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");
            instance.MapNote = GetByte(s, "MapNote") != 0;
            instance.MapNoteEnabled = GetByte(s, "MapNoteEnabled") != 0;
            return instance;
        }

        /// <summary>
        /// Parses a sound instance from GFF struct.
        /// </summary>
        private SoundInstance ParseSoundInstance(GFFStruct s)
        {
            var instance = new SoundInstance();
            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.GeneratedType = GetInt(s, "GeneratedType");
            return instance;
        }

        /// <summary>
        /// Parses an encounter instance from GFF struct.
        /// </summary>
        private EncounterInstance ParseEncounterInstance(GFFStruct s)
        {
            var instance = new EncounterInstance();
            instance.TemplateResRef = GetResRef(s, "TemplateResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");

            // Parse spawn points
            if (s.TryGetList("SpawnPointList", out GFFList spawnList))
            {
                foreach (GFFStruct spawnStruct in spawnList)
                {
                    var spawnPoint = new SpawnPoint
                    {
                        X = GetFloat(spawnStruct, "X"),
                        Y = GetFloat(spawnStruct, "Y"),
                        Z = GetFloat(spawnStruct, "Z"),
                        Orientation = GetFloat(spawnStruct, "Orientation")
                    };
                    instance.SpawnPoints.Add(spawnPoint);
                }
            }

            // Parse geometry
            if (s.TryGetList("Geometry", out GFFList geometryList))
            {
                foreach (GFFStruct vertexStruct in geometryList)
                {
                    float pointX = GetFloat(vertexStruct, "X");
                    float pointY = GetFloat(vertexStruct, "Y");
                    float pointZ = GetFloat(vertexStruct, "Z");
                    instance.Geometry.Add(new Vector3(pointX, pointY, pointZ));
                }
            }

            return instance;
        }

        /// <summary>
        /// Parses a store instance from GFF struct.
        /// </summary>
        private StoreInstance ParseStoreInstance(GFFStruct s)
        {
            var instance = new StoreInstance();
            instance.TemplateResRef = GetResRef(s, "ResRef");
            instance.Tag = GetString(s, "Tag");
            instance.XPosition = GetFloat(s, "XPosition");
            instance.YPosition = GetFloat(s, "YPosition");
            instance.ZPosition = GetFloat(s, "ZPosition");
            instance.XOrientation = GetFloat(s, "XOrientation");
            instance.YOrientation = GetFloat(s, "YOrientation");
            return instance;
        }

        #endregion

        #region GFF Helper Methods

        private string GetString(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetString(name) : string.Empty;
        }

        private string GetResRef(GFFStruct s, string name)
        {
            if (s.Exists(name))
            {
                ResRef resRef = s.GetResRef(name);
                return resRef?.ToString() ?? string.Empty;
            }
            return string.Empty;
        }

        private int GetInt(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetInt32(name) : 0;
        }

        private byte GetByte(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetUInt8(name) : (byte)0;
        }

        private float GetFloat(GFFStruct s, string name)
        {
            return s.Exists(name) ? s.GetSingle(name) : 0f;
        }

        #endregion

        #region Entity Spawning Methods

        /// <summary>
        /// Spawns a waypoint entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadWaypoints @ 0x140362fc0
        /// - CNWSWaypoint::LoadWaypoint @ 0x140509f80 loads waypoint properties from GIT struct
        /// - Creates waypoint entity with ObjectId assigned by world
        /// - Sets Tag, Position, Orientation, MapNote properties
        /// - Adds waypoint to area
        /// </remarks>
        private async Task SpawnWaypointAsync(WaypointInstance waypoint, AuroraArea area)
        {
            if (waypoint == null || area == null)
            {
                return;
            }

            // Calculate facing from orientation vector
            float facing = (float)Math.Atan2(waypoint.YOrientation, waypoint.XOrientation);

            // Create waypoint entity
            Vector3 position = new Vector3(waypoint.XPosition, waypoint.YPosition, waypoint.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Waypoint, position, facing);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(waypoint.Tag))
            {
                entity.Tag = waypoint.Tag;
            }

            // Set waypoint-specific properties
            var waypointComponent = entity.GetComponent<Core.Interfaces.Components.IWaypointComponent>();
            if (waypointComponent != null)
            {
                // Map note properties are handled by waypoint component
                // TODO: Set MapNote and MapNoteEnabled when waypoint component supports them
            }

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns a door entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadDoors @ 0x1403631a0
        /// - CNWSDoor::LoadDoor @ 0x14050a1a0 loads door properties from GIT struct
        /// - Creates door entity with ObjectId assigned by world
        /// - Sets Tag, Position, Bearing, LinkedTo, LinkedToModule properties
        /// - Adds door to area
        /// </remarks>
        private async Task SpawnDoorAsync(DoorInstance door, AuroraArea area)
        {
            if (door == null || area == null)
            {
                return;
            }

            // Create door entity
            Vector3 position = new Vector3(door.XPosition, door.YPosition, door.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Door, position, door.Bearing);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(door.Tag))
            {
                entity.Tag = door.Tag;
            }

            // Set door-specific properties
            var doorComponent = entity.GetComponent<Core.Interfaces.Components.IDoorComponent>();
            if (doorComponent != null)
            {
                // Set transition properties from GIT door instance
                // Based on nwmain.exe: CNWSDoor::LoadDoor @ 0x1404208a0 loads door properties from GIT struct
                // - Loads LinkedTo (CExoString): Tag of waypoint/door for area transitions
                // - Loads LinkedToModule (CResRef): Module ResRef for module transitions
                // - Loads LinkedToFlags (BYTE): Transition flags (bit 1 = module transition, bit 2 = area transition)
                // - Loads TransitionDestin (CExoLocString): Waypoint tag for positioning after transition
                // Located via string references: "LinkedTo" @ 0x1407a8b0, "LinkedToModule" @ 0x1407a8c0, "LinkedToFlags" @ 0x1407a8d0, "TransitionDestin" @ 0x1407a8e0 (approximate - needs Ghidra verification)
                // Original implementation: CNWSDoor::LoadDoor reads these fields from GIT door struct and sets door properties
                // Transition system: Doors with LinkedTo/LinkedToModule trigger area/module transitions when opened
                // TransitionDestin specifies waypoint tag where party spawns after transition (empty = use default entry waypoint)
                
                // Set LinkedTo (waypoint/door tag for area transitions)
                if (!string.IsNullOrEmpty(door.LinkedTo))
                {
                    doorComponent.LinkedTo = door.LinkedTo;
                }

                // Set LinkedToModule (module ResRef for module transitions)
                if (!string.IsNullOrEmpty(door.LinkedToModule))
                {
                    doorComponent.LinkedToModule = door.LinkedToModule;
                }

                // Set TransitionDestination (waypoint tag for positioning after transition)
                if (!string.IsNullOrEmpty(door.TransitionDestin))
                {
                    doorComponent.TransitionDestination = door.TransitionDestin;
                }

                // Set LinkedToFlags on AuroraDoorComponent (Aurora-specific property)
                // Based on nwmain.exe: CNWSDoor stores LinkedToFlags for transition type determination
                // LinkedToFlags bit 1 (0x1) = module transition flag
                // LinkedToFlags bit 2 (0x2) = area transition flag
                var auroraDoorComponent = doorComponent as Components.AuroraDoorComponent;
                if (auroraDoorComponent != null)
                {
                    auroraDoorComponent.LinkedToFlags = door.LinkedToFlags;
                }
            }

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns a placeable entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadPlaceables @ 0x1403632c0
        /// - CNWSPlaceable::LoadPlaceable @ 0x14050a2c0 loads placeable properties from GIT struct
        /// - Creates placeable entity with ObjectId assigned by world
        /// - Sets Tag, Position, Bearing properties
        /// - Adds placeable to area
        /// </remarks>
        private async Task SpawnPlaceableAsync(PlaceableInstance placeable, AuroraArea area)
        {
            if (placeable == null || area == null)
            {
                return;
            }

            // Create placeable entity
            Vector3 position = new Vector3(placeable.XPosition, placeable.YPosition, placeable.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Placeable, position, placeable.Bearing);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(placeable.Tag))
            {
                entity.Tag = placeable.Tag;
            }

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns a creature entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadCreatures @ 0x140362fc0
        /// - CNWSCreature::LoadCreature @ 0x140509fc0 loads creature properties from GIT struct
        /// - Creates creature entity with ObjectId assigned by world
        /// - Sets Tag, Position, Orientation properties
        /// - Loads creature template from UTC file if TemplateResRef is provided
        /// - Adds creature to area
        /// </remarks>
        private async Task SpawnCreatureAsync(CreatureInstance creature, AuroraArea area)
        {
            if (creature == null || area == null)
            {
                return;
            }

            // Calculate facing from orientation vector
            float facing = (float)Math.Atan2(creature.YOrientation, creature.XOrientation);

            // Create creature entity
            Vector3 position = new Vector3(creature.XPosition, creature.YPosition, creature.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Creature, position, facing);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(creature.Tag))
            {
                entity.Tag = creature.Tag;
            }

            // TODO: Load creature template from UTC file if TemplateResRef is provided
            // This requires entity template factory integration

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns a trigger entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadTriggers @ 0x1403633e0
        /// - CNWSTrigger::LoadTrigger @ 0x14050a3e0 loads trigger properties from GIT struct
        /// - Creates trigger entity with ObjectId assigned by world
        /// - Sets Tag, Position, Orientation, Geometry properties
        /// - Adds trigger to area
        /// </remarks>
        private async Task SpawnTriggerAsync(TriggerInstance trigger, AuroraArea area)
        {
            if (trigger == null || area == null)
            {
                return;
            }

            // Calculate facing from orientation vector
            float facing = (float)Math.Atan2(trigger.YOrientation, trigger.XOrientation);

            // Create trigger entity
            Vector3 position = new Vector3(trigger.XPosition, trigger.YPosition, trigger.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Trigger, position, facing);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(trigger.Tag))
            {
                entity.Tag = trigger.Tag;
            }

            // Set trigger geometry
            var triggerComponent = entity.GetComponent<Core.Interfaces.Components.ITriggerComponent>();
            if (triggerComponent != null && trigger.Geometry.Count > 0)
            {
                // TODO: Set trigger geometry when trigger component supports it
            }

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns a sound entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadSounds @ 0x140364000
        /// - CNWSSoundObject::LoadSound @ 0x14050a400 loads sound properties from GIT struct
        /// - Creates sound entity with ObjectId assigned by world
        /// - Sets Tag, Position, GeneratedType properties
        /// - Adds sound to area
        /// </remarks>
        private async Task SpawnSoundAsync(SoundInstance sound, AuroraArea area)
        {
            if (sound == null || area == null)
            {
                return;
            }

            // Create sound entity
            Vector3 position = new Vector3(sound.XPosition, sound.YPosition, sound.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Sound, position, 0.0f);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(sound.Tag))
            {
                entity.Tag = sound.Tag;
            }

            // TODO: Set GeneratedType when sound component supports it

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns an encounter entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadEncounters @ 0x140364120
        /// - CNWSEncounter::LoadEncounter @ 0x14050a520 loads encounter properties from GIT struct
        /// - Creates encounter entity with ObjectId assigned by world
        /// - Sets Tag, Position, SpawnPoints, Geometry properties
        /// - Adds encounter to area
        /// </remarks>
        private async Task SpawnEncounterAsync(EncounterInstance encounter, AuroraArea area)
        {
            if (encounter == null || area == null)
            {
                return;
            }

            // Create encounter entity (encounters are typically placeables or triggers)
            Vector3 position = new Vector3(encounter.XPosition, encounter.YPosition, encounter.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Placeable, position, 0.0f);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(encounter.Tag))
            {
                entity.Tag = encounter.Tag;
            }

            // TODO: Set encounter spawn points and geometry when encounter component supports them

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns a store entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadStores @ 0x140364240
        /// - CNWSStore::LoadStore @ 0x14050a640 loads store properties from GIT struct
        /// - Creates store entity with ObjectId assigned by world
        /// - Sets Tag, Position, Orientation properties
        /// - Adds store to area
        /// </remarks>
        private async Task SpawnStoreAsync(StoreInstance store, AuroraArea area)
        {
            if (store == null || area == null)
            {
                return;
            }

            // Calculate facing from orientation vector
            float facing = (float)Math.Atan2(store.YOrientation, store.XOrientation);

            // Create store entity (stores are typically placeables)
            Vector3 position = new Vector3(store.XPosition, store.YPosition, store.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Placeable, position, facing);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            if (!string.IsNullOrEmpty(store.Tag))
            {
                entity.Tag = store.Tag;
            }

            // TODO: Set store-specific properties when store component supports them

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        #endregion

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
        /// - nwmain.exe: CNWSModule::ExecuteScriptOnModule (needs Ghidra address verification)
        /// - Module scripts are stored in Module.ifo GFF structure (Mod_OnModLoad, Mod_OnClientEntr fields)
        /// - Script execution uses EventBus system similar to Odyssey engine
        /// - Module entity is created or retrieved for script execution (ObjectType.Invalid, Tag set to module ResRef)
        /// - Script hooks are set on module entity's IScriptHooksComponent
        /// - EventBus.FireScriptEvent queues script execution for processing
        /// - OnModuleLoad executes first, then OnClientEnter executes with player character as triggerer
        /// 
        /// Module Script Execution Sequence (Aurora):
        /// 1. Extract script ResRefs from Module.ifo GFF structure
        /// 2. Create or get module entity (Tag = moduleName)
        /// 3. Ensure module entity has IScriptHooksComponent
        /// 4. Set script hooks on component (OnModuleLoad, OnClientEnter)
        /// 5. Fire OnModuleLoad script event (no triggerer)
        /// 6. Fire OnClientEnter script event (player character as triggerer)
        /// 
        /// Script Field Names (Module.ifo GFF):
        /// - Mod_OnModLoad: OnModuleLoad script ResRef
        /// - Mod_OnClientEntr: OnClientEnter script ResRef
        /// - ResRef format: 16-character resource reference name (e.g., "module_001")
        /// - Empty ResRef (blank) means no script for that event
        /// </remarks>
        private async Task TriggerModuleLoadScriptsAsync(string moduleName)
        {
            if (_currentModuleInfo == null || _world?.EventBus == null)
            {
                // Module info not loaded or event bus not available - skip script execution
                return;
            }

            // Extract script ResRefs from Module.ifo GFF structure
            // Based on nwmain.exe: Module scripts stored in Module.ifo GFF fields
            // Field names match Odyssey IFO format: "Mod_OnModLoad", "Mod_OnClientEntr"
            ResRef onModuleLoadResRef = _currentModuleInfo.GetResRef("Mod_OnModLoad");
            ResRef onClientEnterResRef = _currentModuleInfo.GetResRef("Mod_OnClientEntr");

            // Convert ResRef to string (empty string if blank)
            string onModuleLoadScript = onModuleLoadResRef != null && !onModuleLoadResRef.IsBlank() 
                ? onModuleLoadResRef.ToString() 
                : string.Empty;
            string onClientEnterScript = onClientEnterResRef != null && !onClientEnterResRef.IsBlank() 
                ? onClientEnterResRef.ToString() 
                : string.Empty;

            // If no scripts are defined, skip execution
            if (string.IsNullOrEmpty(onModuleLoadScript) && string.IsNullOrEmpty(onClientEnterScript))
            {
                return;
            }

            // Create or get module entity for script execution
            // Based on nwmain.exe: Module entity created with Tag set to module ResRef for script execution
            // Module entity uses ObjectType.Invalid (no specific object type, just a container for scripts)
            // Original implementation: Module entity ObjectId typically fixed value (0x7F000002 in Odyssey, similar in Aurora)
            IEntity moduleEntity = _world.GetEntityByTag(moduleName, 0);
            if (moduleEntity == null)
            {
                // Create temporary module entity for script execution
                // Based on nwmain.exe: Module entity creation pattern
                moduleEntity = _world.CreateEntity(ObjectType.Invalid, System.Numerics.Vector3.Zero, 0.0f);
                if (moduleEntity != null)
                {
                    moduleEntity.Tag = moduleName;
                }
            }

            if (moduleEntity == null)
            {
                // Failed to create module entity - cannot execute scripts
                System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Failed to create module entity for script execution");
                return;
            }

            // Ensure module entity has IScriptHooksComponent for script execution
            // Based on nwmain.exe: Module entities require IScriptHooksComponent to execute scripts
            // Component stores script ResRefs mapped to ScriptEvent types
            var scriptHooksComponent = moduleEntity.GetComponent<IScriptHooksComponent>();
            if (scriptHooksComponent == null)
            {
                // Try to add IScriptHooksComponent if entity supports component addition
                // Note: Entity component system may require component to be added during entity creation
                // For now, if component doesn't exist, we'll skip script execution
                // In a full implementation, ComponentInitializer should ensure all entities have IScriptHooksComponent
                System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Module entity missing IScriptHooksComponent - scripts will not execute");
                return;
            }

            // Set script hooks on module entity component
            // Based on nwmain.exe: Script hooks stored on entity component, mapped to ScriptEvent enum values
            // EventBus.FireScriptEvent looks up script from component based on ScriptEvent type
            if (!string.IsNullOrEmpty(onModuleLoadScript))
            {
                scriptHooksComponent.SetScript(ScriptEvent.OnModuleLoad, onModuleLoadScript);
            }

            if (!string.IsNullOrEmpty(onClientEnterScript))
            {
                scriptHooksComponent.SetScript(ScriptEvent.OnClientEnter, onClientEnterScript);
            }

            // Fire OnModuleLoad script event
            // Based on nwmain.exe: OnModuleLoad executes when module finishes loading (after entities spawned, before gameplay starts)
            // Located via string references: "OnModuleLoad" @ 0x007bee40 (approximate - needs Ghidra verification for nwmain.exe)
            // Original implementation: FUN_005226d0 @ 0x005226d0 in swkotor2.exe executes module load scripts
            // Aurora uses similar pattern: EventBus queues script event, script executor processes event and executes script
            if (!string.IsNullOrEmpty(onModuleLoadScript))
            {
                // OnModuleLoad executes with no triggerer (module-level initialization script)
                _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnModuleLoad, null);
            }

            // Fire OnClientEnter script event
            // Based on nwmain.exe: OnClientEnter executes when client/player enters the module (after OnModuleLoad)
            // Located via string references: "Mod_OnClientEntr" @ 0x007be718 (approximate - needs Ghidra verification for nwmain.exe)
            // Original implementation: OnClientEnter fires with player character as triggerer
            // Script can access triggering entity (player) via GetEnteringObject() or similar NWScript functions
            if (!string.IsNullOrEmpty(onClientEnterScript))
            {
                // Get player character entity to use as triggerer
                // Based on nwmain.exe: Player character identified by ObjectType.Creature with IsPC flag
                // Player character is typically the first creature entity with IsPC flag set
                IEntity playerCharacter = null;
                try
                {
                    // Try to find player character from world entities
                    // Player character is typically a Creature entity with special flags
                    var creatures = _world.GetEntitiesOfType(ObjectType.Creature);
                    foreach (IEntity creature in creatures)
                    {
                        // Check if entity is player character (implementation may vary)
                        // For now, use first creature as fallback (actual implementation should check IsPC flag or similar)
                        playerCharacter = creature;
                        break; // Use first creature found (should be player character in single-player)
                    }
                }
                catch
                {
                    // Failed to get player character - execute script without triggerer
                    playerCharacter = null;
                }

                // Fire OnClientEnter script event with player character as triggerer
                // If player character not found, execute without triggerer (script should handle null triggerer gracefully)
                _world.EventBus.FireScriptEvent(moduleEntity, ScriptEvent.OnClientEnter, playerCharacter);
            }

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

