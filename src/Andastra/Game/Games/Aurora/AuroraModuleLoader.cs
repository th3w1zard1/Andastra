using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.GFF.Generics.UTC;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.Loaders;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Game.Games.Common;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Navigation;
using Andastra.Game.Games.Odyssey.Components;
using Andastra.Game.Games.Aurora.Components;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;
using ObjectType = Andastra.Runtime.Core.Enums.ObjectType;

namespace Andastra.Game.Games.Aurora
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
                    var gff = GFF.FromBytes(ifoData);
                    return gff.Root;
                }

                // Try loading from HAK files (Aurora-specific)
                // Based on nwmain.exe: Module.ifo can be stored in HAK files
                // Module.ifo in HAK files is stored as: ResName = moduleName, ResType = IFO
                // Based on nwmain.exe: CExoResMan::Demand @ 0x14018ef90 searches encapsulated resources (HAK files)
                // Based on nwmain.exe: CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 checks ERF archives for resources
                // Resource lookup precedence: Module directory -> HAK files -> Base game resources
                // When Module.ifo is not in module directory, search all HAK files in hak directory
                // This is necessary because Module.ifo might be in a HAK file that's not yet listed in any Module.ifo
                // (since we don't have Module.ifo yet to know which HAK files to load)
                string hakPath = _auroraResourceProvider.HakPath();
                if (Directory.Exists(hakPath))
                {
                    // Search all HAK files in the hak directory
                    // Based on nwmain.exe: Module.ifo loading searches all registered HAK files
                    // HAK files are registered via CExoResMan::AddKeyTable @ 0x14018e330
                    // For Module.ifo loading, we search all HAK files in the hak directory
                    // HAK files are searched in directory enumeration order (not Module.ifo order, since we don't have Module.ifo yet)
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
                            // Based on nwmain.exe: CExoResMan::ServiceFromEncapsulated @ 0x140192cf0 extracts resources from ERF archives
                            byte[] moduleIfoData = erf.Get(moduleName, ResourceType.IFO);
                            if (moduleIfoData != null && moduleIfoData.Length > 0)
                            {
                                // Module.ifo found in HAK file - parse and return
                                // Based on nwmain.exe: Module.ifo is GFF format, parsed using CResGFF
                                var gff = GFF.FromBytes(moduleIfoData);
                                if (gff?.Root != null)
                                {
                                    return gff.Root;
                                }
                            }
                        }
                        catch
                        {
                            // Skip corrupted or invalid HAK files
                            // Based on nwmain.exe: Invalid HAK files are skipped during resource lookup
                            // Continue searching other HAK files
                            continue;
                        }
                    }
                }

                // Module.ifo not found in module directory or HAK files
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
        /// - Specified in Module.ifo structure as Mod_Entry_Area field (CResRef type)
        /// - First area loaded when module starts
        /// - Player spawns in entry area
        ///
        /// Module.ifo Field (Aurora):
        /// - Mod_Entry_Area: CResRef (ResRef) - Starting area ResRef where player spawns
        /// - Field type: ResRef (16-character resource reference name)
        /// - Default value: Blank ResRef (empty string) if field is missing
        ///
        /// Based on nwmain.exe: Entry area extraction from Module.ifo.
        /// - nwmain.exe: CNWSModule::SaveModule @ 0x1404861e3 line 59 writes Mod_Entry_Area using CResGFF::WriteFieldCResRef
        /// - nwmain.exe: String reference "Mod_Entry_Area" @ 0x140def8c0, referenced by function @ 0x1404863ce
        /// - Original implementation: CResGFF::WriteFieldCResRef(param_1, param_2, *(CResRef **)(param_3 + 0x108), "Mod_Entry_Area")
        /// - Reading implementation: CResGFF::ReadFieldCResRef or similar function reads Mod_Entry_Area from GFF structure
        ///
        /// Official BioWare Documentation:
        /// - vendor/PyKotor/wiki/Bioware-Aurora-IFO.md: Mod_Entry_Area field (CResRef type, page 97-99)
        /// - vendor/PyKotor/wiki/GFF-IFO.md: Mod_Entry_Area field (ResRef type, starting area ResRef)
        /// - vendor/xoreos/src/aurora/ifofile.cpp:203 - _entryArea = ifoTop.getString("Mod_Entry_Area")
        ///
        /// Reference Implementations:
        /// - src/Andastra/Parsing/Resource/Formats/GFF/Generics/IFOHelpers.cs:173 - root.Acquire&lt;ResRef&gt;("Mod_Entry_Area", ResRef.FromBlank())
        /// - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/generics/ifo.py:137 - root.acquire("Mod_Entry_Area", ResRef.from_blank())
        /// </remarks>
        private string GetEntryAreaResRef(GFFStruct moduleInfo)
        {
            if (moduleInfo == null)
            {
                return null;
            }

            // Extract Mod_Entry_Area field from Module.ifo GFF structure
            // Based on nwmain.exe: Mod_Entry_Area is a CResRef (ResRef) field
            // Based on nwmain.exe: CNWSModule::SaveModule @ 0x1404861e3 line 59 writes Mod_Entry_Area as CResRef
            // Based on vendor/xoreos/src/aurora/ifofile.cpp:203 - reads Mod_Entry_Area as string (ResRef converted to string)
            // Based on src/Andastra/Parsing/Resource/Formats/GFF/Generics/IFOHelpers.cs:173 - reads Mod_Entry_Area as ResRef
            // GetResRef returns ResRef.FromBlank() if field doesn't exist (never returns null)
            ResRef entryAreaResRef = moduleInfo.GetResRef("Mod_Entry_Area");

            // Check if ResRef is blank (empty or field doesn't exist)
            // Based on nwmain.exe: Blank ResRef means no entry area specified (module load will fail)
            // Based on vendor/xoreos/src/aurora/ifofile.cpp:203 - empty string means no entry area
            // Based on src/Andastra/Runtime/Games/Odyssey/Loading/ModuleLoader.cs:215-218 - checks Exists first, then converts to string
            if (entryAreaResRef == null || entryAreaResRef.IsBlank())
            {
                return null;
            }

            // Convert ResRef to string and return
            // Based on nwmain.exe: ResRef is converted to string for area resource lookup
            // Based on vendor/xoreos/src/aurora/ifofile.cpp:203 - ResRef stored as string
            // Based on src/Andastra/Runtime/Games/Odyssey/Loading/ModuleLoader.cs:218 - entryAreaRef.ToString()
            return entryAreaResRef.ToString();
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
            // Based on nwmain.exe: CNWSTrigger::SaveTrigger @ 0x140504290 lines 140-142
            // Geometry points are stored relative to trigger position (PointX/Y/Z - Position)
            // When loading, we must add position back to get absolute coordinates
            // Based on nwmain.exe: CNWSTrigger::LoadTrigger loads Geometry list and adds position to each point
            // Based on vendor/xoreos/src/engines/nwn2/trigger.cpp:108 - position + glm::vec3(x, y, z)
            if (s.TryGetList("Geometry", out GFFList geometryList))
            {
                Vector3 triggerPosition = new Vector3(instance.XPosition, instance.YPosition, instance.ZPosition);
                foreach (GFFStruct vertexStruct in geometryList)
                {
                    float pointX = GetFloat(vertexStruct, "PointX");
                    float pointY = GetFloat(vertexStruct, "PointY");
                    float pointZ = GetFloat(vertexStruct, "PointZ");
                    // Add trigger position to relative coordinates to get absolute world coordinates
                    // Based on nwmain.exe: CNWSTrigger::LoadTrigger adds position to geometry points
                    Vector3 absolutePoint = triggerPosition + new Vector3(pointX, pointY, pointZ);
                    instance.Geometry.Add(absolutePoint);
                }
            }

            return instance;
        }

        /// <summary>
        /// Parses a waypoint instance from GFF struct.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSWaypoint::LoadWaypoint @ 0x140509f80
        /// - Line 67: Reads HasMapNote as BYTE (boolean)
        /// - Lines 68-82: If HasMapNote is true:
        ///   - Line 69: Reads MapNoteEnabled as BYTE (boolean)
        ///   - Lines 71-76: Reads MapNote as CExoLocString (LocalizedString)
        ///   - Lines 79-81: Sets HasMapNote (offset 0x308), MapNoteEnabled (offset 0x30c), MapNote (offset 0x310)
        /// </remarks>
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

            // Parse HasMapNote, MapNoteEnabled, and MapNote according to nwmain.exe behavior
            // Based on nwmain.exe: CNWSWaypoint::LoadWaypoint @ 0x140509f80 line 67
            instance.HasMapNote = GetByte(s, "HasMapNote") != 0;

            // Based on nwmain.exe: Only read MapNote and MapNoteEnabled if HasMapNote is true
            if (instance.HasMapNote)
            {
                // Based on nwmain.exe: CNWSWaypoint::LoadWaypoint @ 0x140509f80 line 69
                instance.MapNoteEnabled = GetByte(s, "MapNoteEnabled") != 0;

                // Based on nwmain.exe: CNWSWaypoint::LoadWaypoint @ 0x140509f80 lines 71-76
                // MapNote is a CExoLocString (LocalizedString), not a byte
                LocalizedString mapNoteLocString = s.GetLocString("MapNote");
                if (mapNoteLocString != null && !mapNoteLocString.IsInvalid)
                {
                    instance.MapNoteText = mapNoteLocString.ToString();
                }
                else
                {
                    instance.MapNoteText = string.Empty;
                }
            }
            else
            {
                // If HasMapNote is false, MapNoteEnabled should be false and MapNoteText should be empty
                instance.MapNoteEnabled = false;
                instance.MapNoteText = string.Empty;
            }

            // Legacy property for backwards compatibility
            instance.MapNote = instance.HasMapNote;

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
            // Based on nwmain.exe: CNWSWaypoint::LoadWaypoint @ 0x140509f80
            // Sets HasMapNote (offset 0x308), MapNoteEnabled (offset 0x30c), MapNote (offset 0x310)
            var waypointComponent = entity.GetComponent<IWaypointComponent>();
            if (waypointComponent != null)
            {
                // Set map note properties from GIT waypoint instance
                // Based on nwmain.exe: CNWSWaypoint::LoadWaypoint @ 0x140509f80 lines 79-81
                // GIT MapNote, MapNoteEnabled, HasMapNote override template values (if present)
                waypointComponent.HasMapNote = waypoint.HasMapNote;
                if (waypoint.HasMapNote && !string.IsNullOrEmpty(waypoint.MapNoteText))
                {
                    waypointComponent.MapNote = waypoint.MapNoteText;
                    waypointComponent.MapNoteEnabled = waypoint.MapNoteEnabled;
                }
                else
                {
                    waypointComponent.MapNote = string.Empty;
                    waypointComponent.MapNoteEnabled = false;
                }
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
            var doorComponent = entity.GetComponent<IDoorComponent>();
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

            // Initialize creature component
            // Based on nwmain.exe: Creature entities have CNWSCreatureStats component attached
            var creatureComponent = new AuroraCreatureComponent();
            entity.AddComponent(creatureComponent);

            // Load UTC template if TemplateResRef is provided
            // Based on nwmain.exe: CNWSCreature::LoadCreature @ 0x1403975e0
            // Template loading: Loads UTC file and applies properties to creature
            if (!string.IsNullOrEmpty(creature.TemplateResRef))
            {
                await LoadCreatureTemplateAsync(entity, creatureComponent, creature.TemplateResRef);
            }

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
            // Based on nwmain.exe: CNWSTrigger::LoadTrigger @ 0x140502ac0 loads Geometry list from GIT
            // Geometry is stored as absolute world coordinates (relative coordinates + trigger position)
            // Based on nwmain.exe: CNWSTrigger stores geometry vertices at offset 0x3b0 (array pointer) and count at 0x3a8
            // Based on nwmain.exe: CNWSTrigger::SaveTrigger @ 0x140504290 lines 129-147 saves Geometry list
            // Geometry vertices are used for point-in-polygon tests to detect when entities enter/exit trigger volume
            var triggerComponent = entity.GetComponent<ITriggerComponent>();
            if (triggerComponent != null && trigger.Geometry.Count > 0)
            {
                // Set geometry on trigger component
                // BaseTriggerComponent.Geometry property accepts IList<Vector3> and stores vertices for ContainsPoint tests
                // Based on nwmain.exe: Trigger geometry is essential for trigger enter/exit detection
                // Based on nwmain.exe: CNWSTrigger uses geometry vertices for point-in-polygon collision detection
                triggerComponent.Geometry = trigger.Geometry;
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

            // Set GeneratedType from GIT
            // Based on nwmain.exe: CNWSSoundObject::Save @ 0x1404f54c0 line 30
            // GeneratedType stored at offset 0x328 in CNWSSoundObject structure
            // Written as DWORD: CResGFF::WriteFieldDWORD(param_1, param_2, (uint)(byte)this[0x328], "GeneratedType")
            // GeneratedType values: 0 = manually placed, 1 = autogenerated from area ambient sound
            // Based on vendor/PyKotor/wiki/Bioware-Aurora-SoundObject.md: Table 2.3
            var soundComponent = entity.GetComponent<ISoundComponent>();
            if (soundComponent != null)
            {
                soundComponent.GeneratedType = sound.GeneratedType;
            }

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns an encounter entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadEncounters @ 0x140360cb0
        /// - CNWSEncounter::LoadEncounter @ 0x14043c490 loads encounter properties from GIT struct
        /// - CNWSEncounter::ReadEncounterFromGff @ 0x14043d1c0 reads encounter data from GFF
        /// - Creates encounter entity with ObjectId assigned by world
        /// - Sets Tag, Position, SpawnPoints, Geometry properties
        /// - SpawnPointList loaded from GIT: X, Y, Z, Orientation fields (nwmain.exe: 0x14043dc52 lines 170-230)
        /// - Geometry loaded from GIT: List of vertices with X, Y, Z fields
        /// - Adds encounter to area
        ///
        /// GIT Encounter Structure (nwmain.exe: CNWSEncounter::ReadEncounterFromGff):
        /// - XPosition, YPosition, ZPosition: Encounter position
        /// - SpawnPointList: List of spawn points with X, Y, Z, Orientation fields
        /// - Geometry: List of vertices with X, Y, Z fields defining encounter polygon area
        /// - Active, Reset, ResetTime, SpawnOption, MaxCreatures, RecCreatures, PlayerOnly, Faction, Difficulty fields
        ///
        /// Based on nwmain.exe: CNWSEncounter::ReadEncounterFromGff @ 0x14043d1c0:
        /// - Lines 170-230: Loads SpawnPointList from GIT, reads X, Y, Z, Orientation for each spawn point
        /// - Spawn points are stored in encounter object at offset 0x3a0 (CEncounterSpawnPoint array)
        /// - Geometry vertices are loaded similarly (referenced in SaveEncounter @ 0x14043e760)
        /// </remarks>
        private async Task SpawnEncounterAsync(EncounterInstance encounter, AuroraArea area)
        {
            if (encounter == null || area == null)
            {
                return;
            }

            // Create encounter entity
            // Based on nwmain.exe: CNWSArea::LoadEncounters @ 0x140360cb0 line 48
            // Creates CNWSEncounter object with ObjectId from GIT or default (0x7f000000)
            Vector3 position = new Vector3(encounter.XPosition, encounter.YPosition, encounter.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Encounter, position, 0.0f);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            // Based on nwmain.exe: CNWSEncounter::ReadEncounterFromGff @ 0x14043d1c0 line 86
            // Tag is read from GIT struct and set on encounter object
            if (!string.IsNullOrEmpty(encounter.Tag))
            {
                entity.Tag = encounter.Tag;
            }

            // Get or create encounter component
            // Based on nwmain.exe: Encounter entities have CNWSEncounter component
            // Using Odyssey EncounterComponent since Aurora and Odyssey share the same encounter structure
            var encounterComponent = entity.GetComponent<EncounterComponent>();
            if (encounterComponent == null)
            {
                // Add encounter component if it doesn't exist
                // Based on nwmain.exe: Encounter entities always have encounter component
                encounterComponent = new EncounterComponent();
                entity.AddComponent(encounterComponent);
            }

            // Set encounter geometry from GIT
            // Based on nwmain.exe: CNWSEncounter::ReadEncounterFromGff loads Geometry list from GIT
            // Geometry is a list of vertices (X, Y, Z) defining the encounter polygon area
            // Referenced in SaveEncounter @ 0x14043e760 line 0x14043ea4e (Geometry string @ 0x140de9540)
            if (encounter.Geometry != null && encounter.Geometry.Count > 0)
            {
                encounterComponent.Vertices = new List<Vector3>(encounter.Geometry);
            }
            else
            {
                encounterComponent.Vertices = new List<Vector3>();
            }

            // Set encounter spawn points from GIT
            // Based on nwmain.exe: CNWSEncounter::ReadEncounterFromGff @ 0x14043dc52 lines 170-230
            // SpawnPointList is loaded from GIT with X, Y, Z, Orientation fields for each spawn point
            // Spawn points are stored in encounter object at offset 0x3a0 (CEncounterSpawnPoint array)
            // Each spawn point has: X, Y, Z (position), Orientation (facing angle in radians)
            if (encounter.SpawnPoints != null && encounter.SpawnPoints.Count > 0)
            {
                encounterComponent.SpawnPoints = new List<EncounterSpawnPoint>();
                foreach (SpawnPoint spawnPoint in encounter.SpawnPoints)
                {
                    var encounterSpawnPoint = new EncounterSpawnPoint
                    {
                        Position = new Vector3(spawnPoint.X, spawnPoint.Y, spawnPoint.Z),
                        Orientation = spawnPoint.Orientation
                    };
                    encounterComponent.SpawnPoints.Add(encounterSpawnPoint);
                }
            }
            else
            {
                encounterComponent.SpawnPoints = new List<EncounterSpawnPoint>();
            }

            // Add entity to area
            // Based on nwmain.exe: CNWSArea::LoadEncounters @ 0x140360cb0 line 58
            // CNWSEncounter::AddToArea adds encounter to area after loading
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Spawns a store entity from GIT instance data.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSArea::LoadStores @ 0x1403623c0
        /// - CNWSStore::LoadStore @ 0x1404fbbf0 loads store properties from GIT struct and UTM template
        /// - Creates store entity with ObjectId assigned by world
        /// - Sets Tag, Position, Orientation properties from GIT
        /// - Loads UTM template and sets store properties: MarkUp, MarkDown, StoreGold, IdentifyPrice, MaxBuyPrice, BlackMarket, BM_MarkDown
        /// - Loads item lists: ItemsForSale from UTM ItemList, WillNotBuy/WillOnlyBuy from GIT
        /// - Sets OnOpenStore script from UTM template
        /// - Adds store to area
        ///
        /// Store properties loaded from UTM template (nwmain.exe: CNWSStore::LoadFromTemplate @ 0x1404fbb11):
        /// - MarkUp: Store markup percentage (buy price multiplier)
        /// - MarkDown: Store markdown percentage (sell price multiplier)
        /// - StoreGold: Maximum gold store can use to buy items (-1 = unlimited)
        /// - IdentifyPrice: Price to identify items (default 100)
        /// - MaxBuyPrice: Maximum price for items to buy (-1 = no limit)
        /// - BlackMarket: Whether store is a black market
        /// - BM_MarkDown: Black market markdown percentage
        /// - OnOpenStore: Script to run when store opens
        /// - ItemList: List of items for sale (InventoryRes, Infinite flags)
        ///
        /// Store properties loaded from GIT struct (nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0):
        /// - Tag: Store tag identifier
        /// - LocName: Localized store name
        /// - WillNotBuy: List of base item types store won't buy
        /// - WillOnlyBuy: List of base item types store will only buy
        /// </remarks>
        private async Task SpawnStoreAsync(StoreInstance store, AuroraArea area)
        {
            if (store == null || area == null)
            {
                return;
            }

            // Calculate facing from orientation vector
            float facing = (float)Math.Atan2(store.YOrientation, store.XOrientation);

            // Create store entity (stores are typically placeables in Aurora)
            Vector3 position = new Vector3(store.XPosition, store.YPosition, store.ZPosition);
            IEntity entity = _world.CreateEntity(ObjectType.Placeable, position, facing);

            if (entity == null)
            {
                return;
            }

            // Set tag from GIT
            // Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0 line 45 - CNWSObject::SetTag
            if (!string.IsNullOrEmpty(store.Tag))
            {
                entity.Tag = store.Tag;
            }

            // Initialize store component
            // Based on nwmain.exe: Store entities have CNWSStore component attached
            // Using Odyssey StoreComponent since it has the same properties as Aurora stores
            var storeComponent = new StoreComponent();
            entity.AddComponent(storeComponent);

            // Load UTM template if TemplateResRef is provided
            // Based on nwmain.exe: CNWSStore::LoadFromTemplate @ 0x1404fbb11
            // Template loading: Loads UTM file and applies properties to store
            if (!string.IsNullOrEmpty(store.TemplateResRef))
            {
                await LoadStoreTemplateAsync(entity, storeComponent, store.TemplateResRef);
            }

            // Add entity to area
            area.AddEntity(entity);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Loads UTC creature template and applies properties to creature component.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSCreature::LoadCreature @ 0x1403975e0
        /// - Loads UTC file from TemplateResRef
        /// - Calls CNWSCreatureStats::LoadCreatureStats to load properties from UTC GFF
        /// - Sets all creature properties: stats, appearance, feats, classes, inventory, equipment, scripts
        /// - Applies template data to AuroraCreatureComponent
        ///
        /// UTC file format (GFF with "UTC " signature):
        /// - TemplateResRef: ResRef - Template resource reference (self-reference)
        /// - Tag: String - Creature tag identifier
        /// - Conversation: ResRef - Dialogue file ResRef
        /// - FirstName, LastName: LocString - Localized name
        /// - Race, SubraceIndex: INT - Race and subrace IDs
        /// - Appearance_Type: INT - Appearance type ID
        /// - Gender: INT - Gender ID
        /// - FactionID: INT - Faction ID
        /// - WalkRate: INT - Walk speed multiplier
        /// - SoundSetFile: INT - Soundset ID
        /// - PortraitId: INT - Portrait ID (0xffff = use Portrait ResRef)
        /// - Portrait: ResRef - Portrait file ResRef
        /// - Str, Dex, Con, Int, Wis, Cha: INT - Ability scores
        /// - CurrentHitPoints, MaxHitPoints, HitPoints: INT - Hit points
        /// - ForcePoints, CurrentForce: INT - Force points (Odyssey only)
        /// - NaturalAC: INT - Natural armor class bonus
        /// - refbonus, willbonus, fortbonus: INT - Save bonuses
        /// - GoodEvil: INT - Alignment
        /// - ChallengeRating: FLOAT - Challenge rating
        /// - ClassList: List - List of classes with levels
        /// - FeatList: List - List of feat IDs
        /// - SkillList: List - List of skill ranks
        /// - Equip_ItemList: List - Equipped items by slot
        /// - ItemList: List - Inventory items
        /// - Script hooks: ResRef - OnHeartbeat, OnDeath, OnSpawn, etc.
        ///
        /// Based on nwmain.exe: CNWSCreature::LoadCreature @ 0x1403975e0:
        /// - Line 36: Reads TemplateResRef from GIT struct
        /// - If TemplateResRef is not blank, loads UTC file and applies properties
        /// - CNWSCreatureStats::LoadCreatureStats loads stats from UTC GFF
        /// - Properties are applied to creature object after template loading
        ///
        /// Based on swkotor.exe, swkotor2.exe: UTC template loading:
        /// - swkotor.exe: 0x005026d0, swkotor2.exe: 0x005261b0 - Load creature from UTC template
        /// - UTCHelpers.ConstructUtc parses UTC GFF and creates UTC object
        /// - All UTC properties are applied to creature component
        /// </remarks>
        private async Task LoadCreatureTemplateAsync(IEntity entity, AuroraCreatureComponent creatureComponent, string utcResRef)
        {
            try
            {
                // Set current module context for resource lookup
                if (!string.IsNullOrEmpty(_currentModuleName))
                {
                    _auroraResourceProvider.SetCurrentModule(_currentModuleName);
                }

                // Load UTC file from resource provider
                // Based on nwmain.exe: Resource loading for creature templates
                var resourceId = new ResourceIdentifier(utcResRef, ResourceType.UTC);
                byte[] utcData = await _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None);

                if (utcData == null || utcData.Length == 0)
                {
                    // Creature template not found - use defaults
                    creatureComponent.TemplateResRef = utcResRef;
                    return;
                }

                // Parse UTC GFF
                // Based on nwmain.exe: CResGFF::LoadFromBuffer loads GFF data
                var gff = GFF.FromBytes(utcData);
                var utc = UTCHelpers.ConstructUtc(gff);

                // Set template ResRef
                creatureComponent.TemplateResRef = utcResRef;

                // Apply UTC template properties to creature component
                // Based on nwmain.exe: CNWSCreatureStats::LoadCreatureStats applies UTC properties
                // Based on swkotor.exe, swkotor2.exe: UTC properties applied to creature object

                // Basic properties
                if (!string.IsNullOrEmpty(utc.Tag))
                {
                    creatureComponent.Tag = utc.Tag;
                }

                if (utc.Conversation != null && !utc.Conversation.IsBlank())
                {
                    creatureComponent.Conversation = utc.Conversation.ToString();
                }

                // Appearance properties
                creatureComponent.RaceId = utc.RaceId;
                creatureComponent.AppearanceType = utc.AppearanceId;
                creatureComponent.BodyVariation = utc.BodyVariation;
                creatureComponent.TextureVar = utc.TextureVariation;
                creatureComponent.PortraitId = utc.PortraitId;

                // Vital statistics
                creatureComponent.CurrentHP = utc.CurrentHp;
                creatureComponent.MaxHP = utc.MaxHp;
                creatureComponent.WalkRate = utc.WalkrateId;
                creatureComponent.NaturalAC = utc.NaturalAc;
                creatureComponent.PerceptionRange = utc.PerceptionId;

                // Ability scores
                creatureComponent.Strength = utc.Strength;
                creatureComponent.Dexterity = utc.Dexterity;
                creatureComponent.Constitution = utc.Constitution;
                creatureComponent.Intelligence = utc.Intelligence;
                creatureComponent.Wisdom = utc.Wisdom;
                creatureComponent.Charisma = utc.Charisma;

                // Combat properties
                creatureComponent.FactionId = utc.FactionId;
                creatureComponent.ChallengeRating = utc.ChallengeRating;
                creatureComponent.IsImmortal = utc.Plot; // Plot flag = immortal
                creatureComponent.NoPermDeath = utc.NoPermDeath;
                creatureComponent.Disarmable = utc.Disarmable;
                creatureComponent.Interruptable = utc.Interruptable;

                // Classes
                // Based on nwmain.exe: CNWSCreatureStats::LoadCreatureStats loads ClassList from UTC
                creatureComponent.ClassList.Clear();
                foreach (var utcClass in utc.Classes)
                {
                    var creatureClass = new AuroraCreatureClass
                    {
                        ClassId = utcClass.ClassId,
                        Level = utcClass.ClassLevel
                    };
                    creatureComponent.ClassList.Add(creatureClass);
                }

                // Feats
                // Based on nwmain.exe: CNWSCreatureStats::LoadCreatureStats loads FeatList from UTC
                // Aurora uses two feat lists: FeatList (normal) and BonusFeatList (bonus)
                //
                // Implementation note: The UTC GFF format only contains a single "FeatList" field that contains
                // all feats for the creature template. The UTC format does not distinguish between normal and
                // bonus feats at the template level.
                //
                // In the original engine (nwmain.exe), bonus feats are determined at runtime based on:
                // - Class bonus feats (from classes.2da BonusFeatsTable column)
                // - Race bonus feats (from racialtypes.2da)
                // - Level-based bonus feats
                // - Item properties that grant bonus feats
                //
                // Template-loaded feats (from UTC FeatList) are all treated as normal feats, as they represent
                // feats explicitly assigned to the creature template. Bonus feats should be populated separately
                // during creature initialization based on the creature's classes, race, and level.
                creatureComponent.FeatList.Clear();
                foreach (int featId in utc.Feats)
                {
                    creatureComponent.FeatList.Add(featId);
                }
                // BonusFeatList is populated at runtime based on class/race/level bonuses, not from UTC template
                creatureComponent.BonusFeatList.Clear();

                // Equipment
                // Based on nwmain.exe: CNWSCreature::LoadCreature loads Equip_ItemList from UTC
                // Equipment is applied to creature's inventory component
                creatureComponent.EquippedItems.Clear();
                foreach (var equipmentKvp in utc.Equipment)
                {
                    int slot = (int)equipmentKvp.Key;
                    if (equipmentKvp.Value != null && equipmentKvp.Value.ResRef != null && !equipmentKvp.Value.ResRef.IsBlank())
                    {
                        creatureComponent.EquippedItems[slot] = equipmentKvp.Value.ResRef.ToString();
                    }
                }

                // Note: Inventory items from UTC ItemList would be added to creature's inventory component
                // This requires IInventoryComponent integration which is beyond the scope of template loading
                // Inventory items are typically added during creature initialization or via script

                // Note: Script hooks from UTC (OnHeartbeat, OnDeath, OnSpawn, etc.) would be set on entity's IScriptHooksComponent
                // This requires script hooks component integration
                // Script hooks are typically set during creature initialization or via script system
            }
            catch
            {
                // Creature template loading failed - use defaults
                creatureComponent.TemplateResRef = utcResRef;
            }
        }

        /// <summary>
        /// Loads UTM store template and applies properties to store component.
        /// </summary>
        /// <remarks>
        /// Based on nwmain.exe: CNWSStore::LoadFromTemplate @ 0x1404fbb11
        /// - Loads UTM file from TemplateResRef
        /// - Calls CNWSStore::LoadStore to load properties from UTM GFF
        /// - Sets MarkUp, MarkDown, StoreGold, IdentifyPrice, MaxBuyPrice, BlackMarket, BM_MarkDown
        /// - Loads ItemList and creates store inventory items
        /// - Sets OnOpenStore script
        ///
        /// UTM file format (GFF with "UTM " signature):
        /// - MarkUp: INT - Store markup percentage (buy price multiplier)
        /// - MarkDown: INT - Store markdown percentage (sell price multiplier)
        /// - StoreGold: INT - Maximum gold store can use to buy items (-1 = unlimited, default -1)
        /// - IdentifyPrice: INT - Price to identify items (default 100)
        /// - MaxBuyPrice: INT - Maximum price for items to buy (-1 = no limit, default -1)
        /// - BlackMarket: BYTE (as BOOL) - Whether store is a black market (default 0)
        /// - BM_MarkDown: INT - Black market markdown percentage (default 0)
        /// - OnOpenStore: ResRef - Script to run when store opens
        /// - ItemList: List - List of items for sale (struct with InventoryRes, Infinite)
        /// - LocName: LocString - Localized store name
        /// - Tag: String - Store tag identifier
        ///
        /// Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0:
        /// - Line 55: BlackMarket = ReadFieldBYTEasBOOL("BlackMarket", 0)
        /// - Line 57: BM_MarkDown = ReadFieldINT("BM_MarkDown", 0)
        /// - Line 59: MarkDown = ReadFieldINT("MarkDown", 0)
        /// - Line 61: MarkUp = ReadFieldINT("MarkUp", 0)
        /// - Line 63: StoreGold = ReadFieldINT("StoreGold", -1)
        /// - Line 65: IdentifyPrice = ReadFieldINT("IdentifyPrice", 100)
        /// - Line 67: MaxBuyPrice = ReadFieldINT("MaxBuyPrice", -1)
        /// - Line 48-52: LocName = ReadFieldCExoLocString("LocName")
        /// - Lines 69-96: WillNotBuy/WillOnlyBuy lists from "WillNotBuy"/"WillOnlyBuy" fields
        /// </remarks>
        private async Task LoadStoreTemplateAsync(IEntity entity, Andastra.Game.Games.Odyssey.Components.StoreComponent storeComponent, string utmResRef)
        {
            try
            {
                // Set current module context for resource lookup
                if (!string.IsNullOrEmpty(_currentModuleName))
                {
                    _auroraResourceProvider.SetCurrentModule(_currentModuleName);
                }

                // Load UTM file from resource provider
                // Based on nwmain.exe: Resource loading for store templates
                var resourceId = new ResourceIdentifier(utmResRef, ResourceType.UTM);
                byte[] utmData = await _resourceProvider.GetResourceBytesAsync(resourceId, System.Threading.CancellationToken.None);

                if (utmData == null || utmData.Length == 0)
                {
                    // Store template not found - use defaults
                    storeComponent.TemplateResRef = utmResRef;
                    return;
                }

                // Parse UTM GFF
                // Based on nwmain.exe: CResGFF::LoadFromBuffer loads GFF data
                var gff = GFF.FromBytes(utmData);
                var utm = BioWare.NET.Resource.Formats.GFF.Generics.UTM.UTMHelpers.ConstructUtm(gff);

                // Set template ResRef
                storeComponent.TemplateResRef = utmResRef;

                // Set store properties from UTM template
                // Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0
                // MarkUp: Line 61 - ReadFieldINT("MarkUp", 0)
                storeComponent.MarkUp = utm.MarkUp != 0 ? utm.MarkUp : 100; // Default to 100 if 0

                // MarkDown: Line 59 - ReadFieldINT("MarkDown", 0)
                storeComponent.MarkDown = utm.MarkDown != 0 ? utm.MarkDown : 100; // Default to 100 if 0

                // CanBuy: Derived from BuySellFlag bit 0
                storeComponent.CanBuy = utm.CanBuy;

                // OnOpenStore: Line 36 - ReadFieldResRef("OnOpenStore")
                if (utm.OnOpenScript != null && !utm.OnOpenScript.IsBlank())
                {
                    storeComponent.OnOpenStore = utm.OnOpenScript.ToString();
                }

                // Load items for sale from UTM ItemList
                // Based on nwmain.exe: CNWSStore::LoadStore processes ItemList
                // ItemList parsing: Lines 100-150 (approximate, needs verification)
                storeComponent.ItemsForSale = new List<StoreItem>();
                foreach (var utmItem in utm.Items)
                {
                    if (utmItem.ResRef != null && !utmItem.ResRef.IsBlank())
                    {
                        var storeItem = new StoreItem
                        {
                            ResRef = utmItem.ResRef.ToString(),
                            StackSize = 1, // UTM doesn't store stack size, default to 1
                            Infinite = utmItem.Infinite != 0
                        };
                        storeComponent.ItemsForSale.Add(storeItem);
                    }
                }

                // Load store properties from UTM template
                // Based on nwmain.exe: CNWSStore::LoadStore @ 0x1404fbbf0
                // - Line 63: StoreGold = ReadFieldINT("StoreGold", -1) - default -1 (unlimited)
                storeComponent.Gold = utm.StoreGold;
                // - Line 65: IdentifyPrice = ReadFieldINT("IdentifyPrice", 100) - default 100
                // Based on Bioware Aurora Store Format: IdentifyPrice -1 = store will not identify items, 0+ = price to identify
                storeComponent.IdentifyPrice = utm.IdentifyPrice;
                storeComponent.CanIdentify = utm.IdentifyPrice >= 0;
                // - Line 67: MaxBuyPrice = ReadFieldINT("MaxBuyPrice", -1) - default -1 (no limit)
                storeComponent.MaxBuyPrice = utm.MaxBuyPrice;

                // Note: Additional properties from GIT (BlackMarket, BM_MarkDown, WillNotBuy, WillOnlyBuy)
                // may be loaded from GIT store struct in the future to override UTM defaults
                // For now, these are loaded from UTM only (BlackMarket and BM_MarkDown are not in Odyssey UTM format)
                // WillNotBuy and WillOnlyBuy are item type restrictions that would need to be loaded separately
            }
            catch
            {
                // Store template loading failed - use defaults
                storeComponent.TemplateResRef = utmResRef;
            }
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
                // Add IScriptHooksComponent to module entity if it doesn't exist
                // Based on nwmain.exe: All entities support script hooks, including module entities
                // AuroraEntity.AttachCommonComponents() ensures all entities have IScriptHooksComponent
                // For entities created with CreateEntity, we need to explicitly add the component
                // Component can be added at runtime - entity component system supports dynamic component addition
                scriptHooksComponent = new BaseScriptHooksComponent();
                moduleEntity.AddComponent<IScriptHooksComponent>(scriptHooksComponent);

                // Verify component was successfully added
                scriptHooksComponent = moduleEntity.GetComponent<IScriptHooksComponent>();
                if (scriptHooksComponent == null)
                {
                    // Failed to add component - cannot execute scripts
                    System.Diagnostics.Debug.WriteLine($"[AuroraModuleLoader] Failed to add IScriptHooksComponent to module entity - scripts will not execute");
                    return;
                }
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
            // Original implementation: 0x005226d0 @ 0x005226d0 in swkotor2.exe executes module load scripts
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
                // Original implementation: Iterates through all creatures and finds the one with IsPC flag set to true
                // nwmain.exe: CNWSModule::OnClientEnter checks creature IsPC flag from UTC template data
                // IsPC flag is stored in UTC file (Creature template) and loaded into entity data during creature creation
                IEntity playerCharacter = null;
                try
                {
                    // Try to find player character from world entities
                    // Based on nwmain.exe: Player character is identified by IsPC flag in creature template (UTC file)
                    // IsPC flag is loaded from UTC template and stored in entity data during creature creation
                    // AuroraEntityTemplateFactory sets IsPC from UTC template: entity.SetData("IsPC", GetIntField(root, "IsPC", 0) != 0)
                    var creatures = _world.GetEntitiesOfType(ObjectType.Creature);
                    foreach (IEntity creature in creatures)
                    {
                        // Check if entity is player character by checking IsPC flag
                        // Based on nwmain.exe: CNWSCreature::GetIsPC checks IsPC flag from creature template
                        // Original implementation: Checks UTC template IsPC field (boolean, stored as byte in GFF)
                        // nwmain.exe: IsPC flag is read from UTC template during creature creation and stored in CNWSCreature structure
                        // Current implementation: IsPC is stored as entity data during template loading (AuroraEntityTemplateFactory)
                        if (creature is Runtime.Core.Entities.Entity concreteEntity)
                        {
                            bool isPC = concreteEntity.GetData<bool>("IsPC", false);
                            if (isPC)
                            {
                                playerCharacter = creature;
                                break; // Found player character - use it as triggerer
                            }
                        }
                    }
                }
                catch
                {
                    // Failed to get player character - execute script without triggerer
                    playerCharacter = null;
                }

                // Fire OnClientEnter script event with player character as triggerer
                // If player character not found, execute without triggerer (script should handle null triggerer gracefully)
                // Based on nwmain.exe: OnClientEnter fires even if player character not found (null triggerer)
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
