using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Content.Interfaces;

namespace Andastra.Runtime.Engines.Common
{
    /// <summary>
    /// Abstract base class for module management across all engines.
    /// </summary>
    /// <remarks>
    /// Base Engine Module:
    /// - Common module loading and state management framework shared across all BioWare engines
    /// - Provides foundation for engine-specific module systems
    /// - Common concepts across all engines:
    ///   - Module loading and unloading
    ///   - Current module/area/navigation mesh tracking
    ///   - Module state management (Idle, Loaded, Running states)
    ///   - Resource loading for modules
    /// - Cross-engine analysis:
    ///   - Odyssey (swkotor.exe, swkotor2.exe): IFO/LYT/VIS/GIT/ARE file formats, module state flags
    ///   - Aurora (nwmain.exe): Module.ifo format, area files, entity spawning
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, MassEffect.exe, MassEffect2.exe): UnrealScript-based module loading
    ///   - Infinity (BaldurGate.exe, IcewindDale.exe, PlanescapeTorment.exe): ARE/WED/GAM file formats
    /// - Inheritance: Base class BaseEngineModule (Runtime.Games.Common) implements common module loading/unloading
    ///   - Odyssey: OdysseyModuleLoader : BaseEngineModule (Runtime.Games.Odyssey) - engine-specific addresses in subclass
    ///   - Aurora: AuroraModuleLoader : BaseEngineModule (Runtime.Games.Aurora) - engine-specific addresses in subclass
    ///   - Eclipse: EclipseModuleLoader : BaseEngineModule (Runtime.Games.Eclipse) - engine-specific addresses in subclass
    ///   - Infinity: InfinityModuleLoader : BaseEngineModule (Runtime.Games.Infinity) - engine-specific addresses in subclass
    /// - Engine-specific details (function addresses, string references, file formats) are documented in subclasses
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

