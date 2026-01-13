using System;
using System.IO;
using System.Threading.Tasks;
using BioWare.NET.Extract;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Engines.Eclipse.DragonAge
{
    /// <summary>
    /// Base module loader for Dragon Age series (DA:O and DA2).
    /// </summary>
    /// <remarks>
    /// Dragon Age Module Loading (Common):
    /// - Both DA:O and DA2 use MODULES directory structure
    /// - DA:O: LoadModule @ 0x00b17da4, MODULES @ 0x00ad9810, WRITE_MODULES @ 0x00ad98d8
    /// - DA2: LoadModuleMessage @ 0x00bf5df8, MODULES: @ 0x00bf5d10, WRITE_MODULES: @ 0x00bf5d24
    /// - Module format: .rim files, area files, etc.
    /// </remarks>
    public abstract class DragonAgeModuleLoader : EclipseModuleLoader
    {
        private string _loadedModuleRimPath;
        private readonly System.Collections.Generic.List<string> _loadedModuleExtensionRimPaths = new System.Collections.Generic.List<string>();

        protected DragonAgeModuleLoader(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
        {
        }

        public override bool HasModule(string moduleName)
        {
            // Dragon Age modules are in MODULES directory (both DA:O and DA2)
            return HasModuleInModulesDirectory(moduleName);
        }

        protected override async Task LoadModuleInternalAsync(string moduleName, [CanBeNull] Action<float> progressCallback)
        {
            progressCallback?.Invoke(0.1f);

            // Set module ID early so it can be used in resource loading
            _currentModuleId = moduleName;

            // Load module from MODULES directory (common for both DA:O and DA2)
            string fullModulePath = GetModulePath(moduleName);

            if (!System.IO.Directory.Exists(fullModulePath))
            {
                throw new System.IO.DirectoryNotFoundException($"Module directory not found: {fullModulePath}");
            }

            progressCallback?.Invoke(0.3f);

            // Load module resources
            // Dragon Age modules contain: .rim files, area files, etc.
            await LoadDragonAgeModuleResourcesAsync(fullModulePath, progressCallback);

            progressCallback?.Invoke(0.9f);
        }

        /// <summary>
        /// Cleans up module-specific resources when the module is unloaded.
        /// Removes RIM files from the resource provider.
        /// </summary>
        protected override void OnUnloadModule()
        {
            // Remove module RIM files from resource provider
            // This ensures resources from unloaded modules are not accessible
            if (!string.IsNullOrEmpty(_loadedModuleRimPath))
            {
                _eclipseResourceProvider.RemoveRimFile(_loadedModuleRimPath);
                _loadedModuleRimPath = null;
            }

            // Remove all extension RIM files
            foreach (string extensionRimPath in _loadedModuleExtensionRimPaths)
            {
                _eclipseResourceProvider.RemoveRimFile(extensionRimPath);
            }
            _loadedModuleExtensionRimPaths.Clear();

            // Call base cleanup
            base.OnUnloadModule();
        }

        /// <summary>
        /// Load Dragon Age-specific module resources.
        /// Override in subclasses for game-specific differences.
        /// </summary>
        /// <remarks>
        /// Based on Eclipse engine: Module loading system
        /// Located via string references: MODULES @ 0x00ad9810 (daorigins.exe), MODULES: @ 0x00bf5d10 (DragonAge2.exe)
        /// Original implementation: Loads module.rim files, area files, and other module resources
        /// Module format: Dragon Age uses .rim (Resource Information Manifest) files containing area data
        /// </remarks>
        protected virtual async Task LoadDragonAgeModuleResourcesAsync(string modulePath, [CanBeNull] Action<float> progressCallback)
        {
            // Load Dragon Age module resources
            // Based on Eclipse engine: Module loading system
            // Module structure: MODULES\{moduleName}\module.rim, area files, etc.

            progressCallback?.Invoke(0.4f);

            // Load module.rim file if it exists
            // Based on Eclipse Engine: Module RIM files contain module-specific resources
            // daorigins.exe: Module loading system loads module.rim files from MODULES directory
            // DragonAge2.exe: Similar module.rim loading system
            // RIM files are Resource Information Manifest files containing area data, scripts, and other module resources
            string moduleRimPath = System.IO.Path.Combine(modulePath, "module.rim");
            if (System.IO.File.Exists(moduleRimPath))
            {
                try
                {
                    // Parse the RIM file to validate it's a valid RIM archive
                    // This ensures the file is properly formatted before adding to resource provider
                    var rim = BioWare.NET.Resource.Formats.RIM.RIMAuto.ReadRim(moduleRimPath);

                    // Add the module RIM file to the resource provider
                    // This makes all resources in the RIM file available for lookup
                    // Resources in module.rim will be searched after override but before global RIM files
                    // (RIM files are searched in reverse order, so module.rim gets higher priority than global RIMs)
                    _eclipseResourceProvider.AddRimFile(moduleRimPath);
                    _loadedModuleRimPath = moduleRimPath;

                    // The RIM file is now loaded and resources are accessible via the resource provider
                }
                catch (System.IO.InvalidDataException ex)
                {
                    // RIM file is corrupted or invalid format
                    // Throw exception to prevent loading corrupted module data
                    throw new System.IO.InvalidDataException($"Failed to parse module RIM file: {moduleRimPath}. The RIM file may be corrupted or in an unsupported format.", ex);
                }
                catch (System.Exception ex)
                {
                    // Unexpected error reading RIM file
                    throw new System.Exception($"Error loading module RIM file: {moduleRimPath}", ex);
                }
            }

            // Load module extension RIM files if they exist (e.g., module001x.rim, modulex.rim)
            // Extension RIM files are marked with 'x' suffix and extend base module RIM files
            // Based on RIM file format: Extension RIMs have IsExtension flag and extend base RIM resources
            // Extension RIMs are loaded after base RIMs and override/extend base RIM resources
            // Extension RIMs have filenames ending in 'x' (e.g., module001x.rim) per RIM format specification
            try
            {
                // Search for any RIM files ending in 'x' in the module directory
                // This matches the RIM format specification where extension RIMs end with 'x'
                string[] extensionRimFiles = System.IO.Directory.GetFiles(modulePath, "*x.rim");
                foreach (string extensionRimPath in extensionRimFiles)
                {
                    try
                    {
                        // Parse the extension RIM file to validate it's a valid RIM archive
                        var extensionRim = BioWare.NET.Resource.Formats.RIM.RIMAuto.ReadRim(extensionRimPath);

                        // Add the extension RIM file to the resource provider
                        // Extension RIMs are added after base RIM, so they override base RIM resources
                        // (RIM files are searched in reverse order, so extension RIM gets highest priority)
                        _eclipseResourceProvider.AddRimFile(extensionRimPath);

                        // Track all extension RIM paths for cleanup
                        _loadedModuleExtensionRimPaths.Add(extensionRimPath);
                    }
                    catch (System.IO.InvalidDataException ex)
                    {
                        // Extension RIM file is corrupted or invalid format
                        // Log error but continue (base module RIM may still work)
                        throw new System.IO.InvalidDataException($"Failed to parse module extension RIM file: {extensionRimPath}. The RIM file may be corrupted or in an unsupported format.", ex);
                    }
                    catch (System.Exception ex)
                    {
                        // Unexpected error reading extension RIM file
                        throw new System.Exception($"Error loading module extension RIM file: {extensionRimPath}", ex);
                    }
                }
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                // Module directory doesn't exist (shouldn't happen, but handle gracefully)
            }

            progressCallback?.Invoke(0.6f);

            // Load area files from module directory
            // Areas are typically in subdirectories or as separate files
            // This will be implemented when area loading system is complete

            await Task.CompletedTask;
        }
    }
}

