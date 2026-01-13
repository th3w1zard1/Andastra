using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BioWare.NET.Extract.Installation;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Engines.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Engines.Odyssey
{
    /// <summary>
    /// Odyssey Engine module loader implementation for KOTOR 1/2.
    /// </summary>
    /// <remarks>
    /// Module Loading Process:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_006caab0 @ 0x006caab0 (server command parser, handles module loading commands)
    /// - Located via string references: "MODULES:" @ 0x007b58b4, "MODULES" @ 0x007c6bc4, "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
    /// - Cross-engine: Similar functions in swkotor.exe (K1), nwmain.exe (Aurora), daorigins.exe (Eclipse)
    /// - Inheritance: BaseEngineModule (Runtime.Games.Common) implements common module loading/unloading
    ///   - Odyssey: OdysseyModuleLoader : BaseEngineModule (Runtime.Games.Odyssey) - Odyssey-specific module file formats (IFO, LYT, VIS, GIT, ARE)
    /// - Directory setup: FUN_00633270 @ 0x00633270 (sets up MODULES, OVERRIDE, SAVES, etc. directory aliases)
    /// - Module loading order: IFO (module info) -> LYT (layout) -> VIS (visibility) -> GIT (instances) -> ARE (area properties)
    /// - Original engine uses "MODULES:" prefix for module directory access
    /// - Module resources loaded from: MODULES:\{moduleName}\module.ifo, MODULES:\{moduleName}\{moduleName}.lyt, etc.
    /// - Module state: Sets ModuleLoaded flag when module loaded, ModuleRunning flag when module starts running
    /// </remarks>
    public class OdysseyModuleLoader : BaseEngineModule
    {
        private readonly Installation _installation;
        private readonly Andastra.Runtime.Engines.Odyssey.Loading.ModuleLoader _internalLoader;
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
            _internalLoader = new Andastra.Runtime.Engines.Odyssey.Loading.ModuleLoader(_installation);
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
            if (_currentRuntimeModule != null)
            {
                var areasList = _currentRuntimeModule.Areas.ToList();
                if (areasList.Count > 0)
                {
                    _currentArea = areasList[0];
                }
            }
            else if (_currentRuntimeModule != null && !string.IsNullOrEmpty(_currentRuntimeModule.EntryArea))
            {
                // Load entry area if not already loaded
                RuntimeArea entryArea = _internalLoader.LoadArea(
                    new BioWare.NET.Extract.Installation.Module(moduleName, _installation),
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
                _currentNavigationMesh = runtimeArea.NavigationMesh as NavigationMesh;
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
                var module = new BioWare.NET.Extract.Installation.Module(moduleName, _installation);
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

