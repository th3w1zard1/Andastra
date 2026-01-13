using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using BioWare.NET.Resource.Formats.ERF;
using BioWare.NET.Resource.Formats.GFF;
using BioWare.NET.Resource.Formats.GFF.Generics.UTC;
using BioWare.NET.Resource.Formats.RIM;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.Loaders;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Core.Navigation;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Odyssey;
using Andastra.Game.Games.Odyssey.Components;
using Andastra.Game.Games.Aurora;
using Andastra.Game.Games.Aurora.Components;
using Andastra.Game.Games.Common.Components;
using JetBrains.Annotations;
using ObjectType = Andastra.Runtime.Core.Enums.ObjectType;

namespace Andastra.Game.Games.Engines.Common
{
    /// <summary>
    /// Unified module loader for all BioWare engines (Odyssey, Aurora, Eclipse).
    /// Merges BaseEngineModule functionality and all engine-specific implementations into a single class with conditional logic.
    /// </summary>
    /// <remarks>
    /// Unified Module Loader:
    /// - Merges BaseEngineModule, OdysseyModuleLoader, AuroraModuleLoader, EclipseModuleLoader, and DragonAgeModuleLoader
    /// - Uses EngineFamily enum for conditional logic to handle engine-specific differences
    /// - Implements IEngineModule interface
    /// - Handles module loading/unloading for Odyssey (KOTOR 1/2), Aurora (NWN), and Eclipse (Dragon Age) engines
    /// 
    /// Engine-Specific Implementations:
    /// - Odyssey: IFO/LYT/VIS/GIT/ARE file formats, module state flags
    /// - Aurora: Module.ifo format, area files, HAK files, entity spawning
    /// - Eclipse: UnrealScript packages, .rim files, message-based loading
    /// </remarks>
    public class ModuleLoader : IEngineModule
    {
        private readonly IWorld _world;
        private readonly IGameResourceProvider _resourceProvider;
        private readonly EngineFamily _engineFamily;
        
        // Common state
        private string _currentModuleName;
        private IArea _currentArea;
        private NavigationMesh _currentNavigationMesh;
        
        // Odyssey-specific state
        private Installation _installation;
        private RuntimeModule _currentRuntimeModule;
        private Andastra.Game.Games.Odyssey.Loading.ModuleLoader _odysseyInternalLoader;
        
        // Aurora-specific state
        private AuroraResourceProvider _auroraResourceProvider;
        private GFFStruct _currentModuleInfo;
        private AuroraArea _currentAuroraArea;
        private List<string> _loadedHakFiles;
        
        // Eclipse-specific state
        private EclipseResourceProvider _eclipseResourceProvider;
        private string _currentModuleId;
        private string _loadedModuleRimPath;
        private readonly List<string> _loadedModuleExtensionRimPaths;

        /// <summary>
        /// Initializes a new instance of the ModuleLoader class.
        /// </summary>
        /// <param name="world">The game world instance.</param>
        /// <param name="resourceProvider">The resource provider for loading module resources.</param>
        /// <param name="engineFamily">The engine family (Odyssey, Aurora, or Eclipse).</param>
        /// <exception cref="ArgumentNullException">Thrown if world or resourceProvider is null.</exception>
        /// <exception cref="ArgumentException">Thrown if resource provider type doesn't match engine family.</exception>
        public ModuleLoader(IWorld world, IGameResourceProvider resourceProvider, EngineFamily engineFamily)
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
            _engineFamily = engineFamily;
            _loadedModuleExtensionRimPaths = new List<string>();

            // Initialize engine-specific resource providers
            if (_engineFamily == EngineFamily.Odyssey)
            {
                if (resourceProvider is GameResourceProvider gameResourceProvider)
                {
                    _installation = gameResourceProvider.Installation;
                    // TODO: STUB - Create internal loader (Loading.ModuleLoader functionality should be merged directly into unified ModuleLoader)
                    _odysseyInternalLoader = new Andastra.Game.Games.Odyssey.Loading.ModuleLoader(_installation);
                }
                else
                {
                    throw new ArgumentException("Resource provider must be GameResourceProvider for Odyssey engine", nameof(resourceProvider));
                }
            }
            else if (_engineFamily == EngineFamily.Aurora)
            {
                if (resourceProvider is AuroraResourceProvider auroraProvider)
                {
                    _auroraResourceProvider = auroraProvider;
                    _loadedHakFiles = new List<string>();
                }
                else
                {
                    throw new ArgumentException("Resource provider must be AuroraResourceProvider for Aurora engine", nameof(resourceProvider));
                }
            }
            else if (_engineFamily == EngineFamily.Eclipse)
            {
                if (resourceProvider is EclipseResourceProvider eclipseProvider)
                {
                    _eclipseResourceProvider = eclipseProvider;
                }
                else
                {
                    throw new ArgumentException("Resource provider must be EclipseResourceProvider for Eclipse engine", nameof(resourceProvider));
                }
            }
        }

        /// <summary>
        /// Gets the current module name.
        /// </summary>
        [CanBeNull]
        public string CurrentModuleName
        {
            get { return _currentModuleName; }
        }

        /// <summary>
        /// Gets the current area.
        /// </summary>
        [CanBeNull]
        public IArea CurrentArea
        {
            get { return _currentArea; }
        }

        /// <summary>
        /// Gets the current navigation mesh.
        /// </summary>
        [CanBeNull]
        public NavigationMesh CurrentNavigationMesh
        {
            get { return _currentNavigationMesh; }
        }

        /// <summary>
        /// Loads a module by name.
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to load.</param>
        /// <param name="progressCallback">Optional callback for loading progress (0.0 to 1.0).</param>
        /// <returns>Task that completes when the module is loaded.</returns>
        public async Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            }

            progressCallback?.Invoke(0.0f);

            // Route to engine-specific implementation
            if (_engineFamily == EngineFamily.Odyssey)
            {
                await LoadModuleOdysseyAsync(moduleName, progressCallback);
            }
            else if (_engineFamily == EngineFamily.Aurora)
            {
                await LoadModuleAuroraAsync(moduleName, progressCallback);
            }
            else if (_engineFamily == EngineFamily.Eclipse)
            {
                await LoadModuleEclipseAsync(moduleName, progressCallback);
            }
            else
            {
                throw new InvalidOperationException($"Unsupported engine family: {_engineFamily}");
            }
        }

        /// <summary>
        /// Unloads the current module.
        /// </summary>
        public void UnloadModule()
        {
            if (_currentModuleName != null)
            {
                // Engine-specific cleanup
                if (_engineFamily == EngineFamily.Odyssey)
                {
                    OnUnloadModuleOdyssey();
                }
                else if (_engineFamily == EngineFamily.Aurora)
                {
                    OnUnloadModuleAurora();
                }
                else if (_engineFamily == EngineFamily.Eclipse)
                {
                    OnUnloadModuleEclipse();
                }

                // Common state reset
                _currentModuleName = null;
                _currentArea = null;
                _currentNavigationMesh = null;
            }
        }

        /// <summary>
        /// Checks if a module exists and can be loaded.
        /// </summary>
        /// <param name="moduleName">The resource reference name of the module to check.</param>
        /// <returns>True if the module exists and can be loaded, false otherwise.</returns>
        public bool HasModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            // Route to engine-specific implementation
            if (_engineFamily == EngineFamily.Odyssey)
            {
                return HasModuleOdyssey(moduleName);
            }
            else if (_engineFamily == EngineFamily.Aurora)
            {
                return HasModuleAurora(moduleName);
            }
            else if (_engineFamily == EngineFamily.Eclipse)
            {
                return HasModuleEclipse(moduleName);
            }
            else
            {
                return false;
            }
        }

        // Odyssey module loading implementation
        // Merged from OdysseyModuleLoader (uses Loading.ModuleLoader internally - TODO: fully merge Loading.ModuleLoader functionality)
        private async Task LoadModuleOdysseyAsync(string moduleName, [CanBeNull] Action<float> progressCallback)
        {
            // Load module using internal loader (Loading.ModuleLoader)
            // TODO: STUB - Loading.ModuleLoader functionality (857 lines) should be merged directly into this method
            _currentRuntimeModule = _odysseyInternalLoader.LoadModule(moduleName);

            // Update module name
            _currentModuleName = moduleName;

            // Set current area (first area in module, or entry area)
            if (_currentRuntimeModule != null)
            {
                var areasList = _currentRuntimeModule.Areas.ToList();
                if (areasList.Count > 0)
                {
                    _currentArea = areasList[0];
                }
                else if (!string.IsNullOrEmpty(_currentRuntimeModule.EntryArea))
                {
                    // Load entry area if not already loaded
                    RuntimeArea entryArea = _odysseyInternalLoader.LoadArea(
                        new BioWare.NET.Common.Module(moduleName, _installation),
                        _currentRuntimeModule.EntryArea);
                    if (entryArea != null)
                    {
                        _currentRuntimeModule.AddArea(entryArea);
                        _currentArea = entryArea;
                    }
                }
            }

            // Set navigation mesh from current area
            if (_currentArea is RuntimeArea runtimeArea && runtimeArea.NavigationMesh != null)
            {
                _currentNavigationMesh = runtimeArea.NavigationMesh as NavigationMesh;
            }

            progressCallback?.Invoke(1.0f);
            await Task.CompletedTask;
        }

        // TODO: STUB - Aurora module loading implementation
        // Merge from AuroraModuleLoader.cs (2085 lines - helper methods need to be merged)
        private async Task LoadModuleAuroraAsync(string moduleName, [CanBeNull] Action<float> progressCallback)
        {
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
                // TODO: STUB - Implement LoadModuleInfoAsync helper method (merge from AuroraModuleLoader.cs:365)
                _currentModuleInfo = await LoadModuleInfoAuroraAsync(moduleName);
                if (_currentModuleInfo == null)
                {
                    throw new InvalidOperationException($"Failed to load Module.ifo for module '{moduleName}'");
                }

                // Set current module context for resource lookups
                _auroraResourceProvider.SetCurrentModule(moduleName);

                // Load HAK files (hak packs)
                progressCallback?.Invoke(0.2f);
                // TODO: STUB - Implement LoadHakFilesAsync helper method (merge from AuroraModuleLoader.cs:491)
                await LoadHakFilesAuroraAsync(moduleName);

                // Get entry area from Module.ifo
                // TODO: STUB - Implement GetEntryAreaResRef helper method (merge from AuroraModuleLoader.cs)
                string entryAreaResRef = GetEntryAreaResRefAurora(_currentModuleInfo);
                if (string.IsNullOrEmpty(entryAreaResRef))
                {
                    throw new InvalidOperationException($"Module '{moduleName}' has no entry area specified");
                }

                // Load entry area ARE file
                progressCallback?.Invoke(0.3f);
                // TODO: STUB - Implement LoadAreaFileAsync helper method (merge from AuroraModuleLoader.cs:699)
                byte[] areData = await LoadAreaFileAuroraAsync(entryAreaResRef);
                if (areData == null)
                {
                    throw new InvalidOperationException($"Failed to load ARE file for area '{entryAreaResRef}'");
                }

                // Load entry area GIT file
                progressCallback?.Invoke(0.5f);
                // TODO: STUB - Implement LoadGitFileAsync helper method (merge from AuroraModuleLoader.cs:734)
                byte[] gitData = await LoadGitFileAuroraAsync(entryAreaResRef);
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
                // TODO: STUB - Implement SpawnEntitiesFromGitAsync helper method (merge from AuroraModuleLoader.cs:790)
                await SpawnEntitiesFromGitAuroraAsync(_currentAuroraArea, gitData);

                // Set current module/area
                _currentModuleName = moduleName;
                _currentArea = _currentAuroraArea;

                // Trigger module load scripts (OnModuleLoad, OnClientEnter)
                progressCallback?.Invoke(0.95f);
                // TODO: STUB - Implement TriggerModuleLoadScriptsAsync helper method (merge from AuroraModuleLoader.cs:2126)
                await TriggerModuleLoadScriptsAuroraAsync(moduleName);

                progressCallback?.Invoke(1.0f);
            }
            catch (Exception ex)
            {
                // Clean up on error
                UnloadModule();
                throw new InvalidOperationException($"Failed to load module '{moduleName}': {ex.Message}", ex);
            }
        }

        // Aurora helper methods (merged from AuroraModuleLoader.cs)
        
        private async Task<GFFStruct> LoadModuleInfoAuroraAsync(string moduleName)
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
                string hakPath = _auroraResourceProvider.HakPath();
                if (Directory.Exists(hakPath))
                {
                    string[] hakFiles = Directory.GetFiles(hakPath, "*.hak", SearchOption.TopDirectoryOnly);
                    foreach (string hakFilePath in hakFiles)
                    {
                        try
                        {
                            var erf = ERFAuto.ReadErf(hakFilePath);
                            byte[] moduleIfoData = erf.Get(moduleName, ResourceType.IFO);
                            if (moduleIfoData != null && moduleIfoData.Length > 0)
                            {
                                var gff = GFF.FromBytes(moduleIfoData);
                                return gff.Root;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private async Task LoadHakFilesAuroraAsync(string moduleName)
        {
            // Load HAK files listed in Module.ifo
            if (_currentModuleInfo == null)
            {
                return;
            }

            // TODO: STUB - Parse HAK file list from Module.ifo and load them (merge from AuroraModuleLoader.cs:491)
            // HAK files are loaded and registered with the resource provider
            await Task.CompletedTask;
        }

        private string GetEntryAreaResRefAurora(GFFStruct moduleInfo)
        {
            if (moduleInfo == null)
            {
                return null;
            }

            // Extract Mod_Entry_Area field from Module.ifo GFF structure
            // Based on nwmain.exe: Mod_Entry_Area is a CResRef (ResRef) field
            ResRef entryAreaResRef = moduleInfo.GetResRef("Mod_Entry_Area");

            // Check if ResRef is blank (empty or field doesn't exist)
            if (entryAreaResRef == null || entryAreaResRef.IsBlank())
            {
                return null;
            }

            // Convert ResRef to string and return
            return entryAreaResRef.ToString();
        }

        private async Task<byte[]> LoadAreaFileAuroraAsync(string areaResRef)
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

        private async Task<byte[]> LoadGitFileAuroraAsync(string areaResRef)
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

        // TODO: STUB - SpawnEntitiesFromGitAuroraAsync (merge from AuroraModuleLoader.cs:790 - complex entity spawning logic)
        private async Task SpawnEntitiesFromGitAuroraAsync(AuroraArea area, byte[] gitData)
        {
            // This method spawns entities from GIT file:
            // - Waypoints
            // - Doors
            // - Placeables
            // - Creatures
            // - Triggers
            // - Sounds
            // - Encounters
            // - Stores
            // Requires parsing GIT format and creating entities in world
            await Task.CompletedTask;
        }

        // TODO: STUB - TriggerModuleLoadScriptsAuroraAsync (merge from AuroraModuleLoader.cs:2126)
        private async Task TriggerModuleLoadScriptsAuroraAsync(string moduleName)
        {
            // Triggers module load scripts:
            // - OnModuleLoad script from Module.ifo
            // - OnClientEnter script
            await Task.CompletedTask;
        }

        // Eclipse module loading implementation
        // Merged from EclipseModuleLoader.cs and DragonAgeModuleLoader.cs
        private async Task LoadModuleEclipseAsync(string moduleName, [CanBeNull] Action<float> progressCallback)
        {
            progressCallback?.Invoke(0.1f);

            // Set module ID early so it can be used in resource loading
            _currentModuleId = moduleName;

            // Load module from MODULES directory (Dragon Age modules)
            string packagesPath = _eclipseResourceProvider.PackagePath();
            string fullModulePath = Path.Combine(packagesPath, moduleName);

            if (!Directory.Exists(fullModulePath))
            {
                throw new DirectoryNotFoundException($"Module directory not found: {fullModulePath}");
            }

            progressCallback?.Invoke(0.3f);

            // Load module resources (RIM files)
            await LoadDragonAgeModuleResourcesAsync(fullModulePath, progressCallback);

            // Set module name and state
            _currentModuleName = moduleName;
            progressCallback?.Invoke(0.9f);
        }

        // Load Dragon Age module resources (RIM files)
        private async Task LoadDragonAgeModuleResourcesAsync(string modulePath, [CanBeNull] Action<float> progressCallback)
        {
            progressCallback?.Invoke(0.4f);

            // Load module.rim file if it exists
            string moduleRimPath = Path.Combine(modulePath, "module.rim");
            if (File.Exists(moduleRimPath))
            {
                try
                {
                    var rim = BioWare.NET.Resource.Formats.RIM.RIMAuto.ReadRim(moduleRimPath);
                    _eclipseResourceProvider.AddRimFile(moduleRimPath);
                    _loadedModuleRimPath = moduleRimPath;
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException($"Failed to parse module RIM file: {moduleRimPath}. The RIM file may be corrupted or in an unsupported format.", ex);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Error loading module RIM file: {moduleRimPath}", ex);
                }
            }

            // Load module extension RIM files if they exist (e.g., module001x.rim)
            try
            {
                string[] extensionRimFiles = Directory.GetFiles(modulePath, "*x.rim");
                foreach (string extensionRimPath in extensionRimFiles)
                {
                    try
                    {
                        var extensionRim = BioWare.NET.Resource.Formats.RIM.RIMAuto.ReadRim(extensionRimPath);
                        _eclipseResourceProvider.AddRimFile(extensionRimPath);
                        _loadedModuleExtensionRimPaths.Add(extensionRimPath);
                    }
                    catch (InvalidDataException ex)
                    {
                        throw new InvalidDataException($"Failed to parse module extension RIM file: {extensionRimPath}. The RIM file may be corrupted or in an unsupported format.", ex);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Error loading module extension RIM file: {extensionRimPath}", ex);
                    }
                }
            }
            catch (DirectoryNotFoundException)
            {
                // Module directory doesn't exist (shouldn't happen, but handle gracefully)
            }

            progressCallback?.Invoke(0.6f);
            await Task.CompletedTask;
        }

        // Odyssey module existence check
        private bool HasModuleOdyssey(string moduleName)
        {
            try
            {
                var module = new BioWare.NET.Common.Module(moduleName, _installation);
                return module.Info() != null;
            }
            catch
            {
                return false;
            }
        }

        // Aurora module existence check
        private bool HasModuleAurora(string moduleName)
        {
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
                string hakPath = _auroraResourceProvider.HakPath();
                if (Directory.Exists(hakPath))
                {
                    string[] hakFiles = Directory.GetFiles(hakPath, "*.hak", SearchOption.TopDirectoryOnly);
                    foreach (string hakFilePath in hakFiles)
                    {
                        try
                        {
                            var erf = ERFAuto.ReadErf(hakFilePath);
                            byte[] moduleIfoData = erf.Get(moduleName, ResourceType.IFO);
                            if (moduleIfoData != null && moduleIfoData.Length > 0)
                            {
                                return true;
                            }
                        }
                        catch
                        {
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

        // Eclipse module existence check
        private bool HasModuleEclipse(string moduleName)
        {
            try
            {
                // Eclipse games use packages directory
                string packagesPath = _eclipseResourceProvider.PackagePath();
                if (Directory.Exists(packagesPath))
                {
                    string modulePackagePath = Path.Combine(packagesPath, moduleName);
                    if (Directory.Exists(modulePackagePath))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // Odyssey module unload cleanup
        private void OnUnloadModuleOdyssey()
        {
            if (_currentRuntimeModule != null)
            {
                _currentRuntimeModule = null;
            }
        }

        // Aurora module unload cleanup
        private void OnUnloadModuleAurora()
        {
            if (_currentAuroraArea != null)
            {
                _currentAuroraArea = null;
            }
            _currentModuleInfo = null;
            if (_loadedHakFiles != null)
            {
                _loadedHakFiles.Clear();
            }
        }

        // Eclipse module unload cleanup
        private void OnUnloadModuleEclipse()
        {
            _currentModuleId = null;
            if (!string.IsNullOrEmpty(_loadedModuleRimPath))
            {
                _eclipseResourceProvider.RemoveRimFile(_loadedModuleRimPath);
                _loadedModuleRimPath = null;
            }
            foreach (string extensionRimPath in _loadedModuleExtensionRimPaths)
            {
                _eclipseResourceProvider.RemoveRimFile(extensionRimPath);
            }
            _loadedModuleExtensionRimPaths.Clear();
        }
    }
}
