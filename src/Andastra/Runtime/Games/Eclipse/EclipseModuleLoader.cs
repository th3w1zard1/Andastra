using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Andastra.Parsing.Common;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Common;
using JetBrains.Annotations;

namespace Andastra.Runtime.Engines.Eclipse
{
    /// <summary>
    /// Abstract base class for Eclipse Engine module loader implementations.
    /// </summary>
    /// <remarks>
    /// Eclipse Module Loader Base:
    /// - Based on Eclipse/Unreal Engine module/package loading
    /// - Eclipse uses UnrealScript message passing (LoadModuleMessage) vs Odyssey direct file loading
    /// - Module system uses MODULES/WRITE_MODULES strings similar to Odyssey but different implementation
    /// - Architecture: Message-based module loading vs Odyssey direct file I/O
    /// - Game-specific implementations: DragonAgeOriginsModuleLoader, DragonAge2ModuleLoader, ModuleLoader, 2ModuleLoader
    /// </remarks>
    public abstract class EclipseModuleLoader : BaseEngineModule
    {
        protected readonly EclipseResourceProvider _eclipseResourceProvider;
        protected string _currentModuleId;

        protected EclipseModuleLoader(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
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

        public override async Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            }

            progressCallback?.Invoke(0.0f);

            // Load module using game-specific implementation
            await LoadModuleInternalAsync(moduleName, progressCallback);

            // Set both base class module name and Eclipse-specific module ID
            CurrentModuleName = moduleName;
            _currentModuleId = moduleName;
            progressCallback?.Invoke(1.0f);
        }

        protected abstract Task LoadModuleInternalAsync(string moduleName, [CanBeNull] Action<float> progressCallback);

        protected override void OnUnloadModule()
        {
            _currentModuleId = null;
        }

        /// <summary>
        /// Gets the current module ID (Eclipse-specific).
        /// </summary>
        [CanBeNull]
        public string CurrentModuleId
        {
            get { return _currentModuleId; }
        }

        /// <summary>
        /// Helper method to check if a module directory exists in the MODULES path.
        /// Common across all Eclipse games that use MODULES directory.
        /// </summary>
        protected bool HasModuleInModulesDirectory(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            try
            {
                // Eclipse games may use packages or modules directory
                // Check packages directory first (Dragon Age, )
                string packagesPath = _eclipseResourceProvider.PackagePath();
                if (System.IO.Directory.Exists(packagesPath))
                {
                    // Check for module in packages
                    string modulePackagePath = System.IO.Path.Combine(packagesPath, moduleName);
                    if (System.IO.Directory.Exists(modulePackagePath))
                    {
                        return true;
                    }
                }

                // Fallback: Check modules directory (if it exists for Eclipse games)
                // Note: Some Eclipse games may not use modules directory
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Helper method to get the full module path from packages or modules directory.
        /// Common across all Eclipse games.
        /// </summary>
        protected string GetModulePath(string moduleName)
        {
            // Eclipse games use packages directory
            string packagesPath = _eclipseResourceProvider.PackagePath();
            return System.IO.Path.Combine(packagesPath, moduleName);
        }
    }
}

