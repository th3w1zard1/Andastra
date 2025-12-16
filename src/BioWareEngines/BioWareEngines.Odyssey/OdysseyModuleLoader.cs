using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Navigation;
using BioWareEngines.Core.Module;
using BioWareEngines.Engines.Common;
using BioWareEngines.Content.Interfaces;
using BioWareEngines.Content.ResourceProviders;
using AuroraEngine.Common.Installation;

namespace BioWareEngines.Engines.Odyssey
{
    /// <summary>
    /// Odyssey Engine module loader implementation for KOTOR 1/2.
    /// </summary>
    /// <remarks>
    /// Module Loading Process:
    /// - Based on swkotor2.exe module loading system
    /// - Located via string references: "MODULES:" @ 0x007b58b4, "MODULES" @ 0x007c6bc4
    /// - Directory setup: FUN_00633270 @ 0x00633270 (sets up MODULES, OVERRIDE, SAVES, etc. directory aliases)
    /// - Module loading order: IFO (module info) -> LYT (layout) -> VIS (visibility) -> GIT (instances) -> ARE (area properties)
    /// - Original engine uses "MODULES:" prefix for module directory access
    /// - Module resources loaded from: MODULES:\{moduleName}\module.ifo, MODULES:\{moduleName}\{moduleName}.lyt, etc.
    /// </remarks>
    public class OdysseyModuleLoader : BaseEngineModule
    {
        private readonly Installation _installation;
        private readonly Odyssey.Kotor.Loading.ModuleLoader _internalLoader;
        private RuntimeModule _currentRuntimeModule;

        public OdysseyModuleLoader(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
        {
            // Extract Installation from GameResourceProvider
            if (resourceProvider is GameResourceProvider gameResourceProvider)
            {
                _installation = gameResourceProvider.Installation;
            }
            else
            {
                throw new ArgumentException("Resource provider must be GameResourceProvider for Odyssey engine", nameof(resourceProvider));
            }

            // Create internal loader (will be replaced with direct implementation later)
            _internalLoader = new Odyssey.Kotor.Loading.ModuleLoader(_installation);
        }

        public override async Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            }

            progressCallback?.Invoke(0.0f);

            // Load module using internal loader
            _currentRuntimeModule = _internalLoader.LoadModule(moduleName);

            // Update base class state
            _currentModuleName = moduleName;

            // Set current area (first area in module, or entry area)
            if (_currentRuntimeModule != null && _currentRuntimeModule.Areas.Count > 0)
            {
                _currentArea = _currentRuntimeModule.Areas[0];
            }
            else if (_currentRuntimeModule != null && !string.IsNullOrEmpty(_currentRuntimeModule.EntryArea))
            {
                // Load entry area if not already loaded
                RuntimeArea entryArea = _internalLoader.LoadArea(
                    new AuroraEngine.Common.Module(moduleName, _installation),
                    _currentRuntimeModule.EntryArea);
                if (entryArea != null)
                {
                    _currentRuntimeModule.AddArea(entryArea);
                    _currentArea = entryArea;
                }
            }

            // Set navigation mesh from current area
            if (_currentArea is RuntimeArea runtimeArea && runtimeArea.NavigationMesh != null)
            {
                _currentNavigationMesh = runtimeArea.NavigationMesh;
            }

            progressCallback?.Invoke(1.0f);
        }

        public override bool HasModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            try
            {
                var module = new AuroraEngine.Common.Module(moduleName, _installation);
                return module.Info() != null;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnUnloadModule()
        {
            if (_currentRuntimeModule != null)
            {
                // Clean up module resources
                _currentRuntimeModule = null;
            }
        }

        /// <summary>
        /// Gets the current runtime module (Odyssey-specific).
        /// </summary>
        [CanBeNull]
        public RuntimeModule CurrentRuntimeModule
        {
            get { return _currentRuntimeModule; }
        }
    }
}

