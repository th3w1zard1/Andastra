using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Core.Navigation;
using BioWareEngines.Content.Interfaces;

namespace BioWareEngines.Engines.Common
{
    /// <summary>
    /// Abstract base class for module management across all engines.
    /// </summary>
    /// <remarks>
    /// Base Engine Module:
    /// - Based on swkotor2.exe module management system
    /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58, "MODULE" @ module loading
    /// - Module loading: FUN_006caab0 @ 0x006caab0 sets module state flags, manages module transitions
    /// - Module management: Loads module areas, navigation meshes, entities from GIT files
    /// - Original implementation: Module system loads areas, entities, navigation meshes, manages module state
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
    /// </remarks>
    public abstract class BaseEngineModule : IEngineModule
    {
        protected readonly IWorld _world;
        protected readonly IGameResourceProvider _resourceProvider;
        protected string _currentModuleName;
        protected IArea _currentArea;
        protected NavigationMesh _currentNavigationMesh;

        protected BaseEngineModule(IWorld world, IGameResourceProvider resourceProvider)
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
        }

        [CanBeNull]
        public string CurrentModuleName
        {
            get { return _currentModuleName; }
            protected set { _currentModuleName = value; }
        }

        [CanBeNull]
        public IArea CurrentArea
        {
            get { return _currentArea; }
            protected set { _currentArea = value; }
        }

        [CanBeNull]
        public NavigationMesh CurrentNavigationMesh
        {
            get { return _currentNavigationMesh; }
            protected set { _currentNavigationMesh = value; }
        }

        public abstract Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null);

        public virtual void UnloadModule()
        {
            if (_currentModuleName != null)
            {
                OnUnloadModule();
                _currentModuleName = null;
                _currentArea = null;
                _currentNavigationMesh = null;
            }
        }

        public abstract bool HasModule(string moduleName);

        protected abstract void OnUnloadModule();
    }
}

