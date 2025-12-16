using System;
using JetBrains.Annotations;
using BioWareEngines.Core.Entities;
using BioWareEngines.Core.Interfaces;
using BioWareEngines.Content.Interfaces;
using BioWareEngines.Scripting.Interfaces;

namespace BioWareEngines.Engines.Common
{
    /// <summary>
    /// Abstract base class for all BioWare engine implementations.
    /// </summary>
    /// <remarks>
    /// Base Engine:
    /// - Based on swkotor2.exe engine initialization system
    /// - Located via string references: Engine initialization in FUN_00404250 @ 0x00404250 (WinMain equivalent)
    /// - Engine initialization: FUN_00404250 initializes engine objects, loads configuration, creates game instance
    /// - Resource provider: CExoKeyTable handles resource loading, tracks loaded resources
    /// - World management: Engine manages world instance, module loading, entity management
    /// - Engine API: Provides NWScript engine functions (GetObjectByTag, GetNearestObject, etc.)
    /// - Original implementation: Engine initialization creates resource provider, world, engine API, game session
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
    /// </remarks>
    public abstract class BaseEngine : IEngine
    {
        protected readonly IEngineProfile _profile;
        protected IGameResourceProvider _resourceProvider;
        protected World _world;
        protected IEngineApi _engineApi;
        protected bool _initialized;

        protected BaseEngine(IEngineProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            _profile = profile;
        }

        public EngineFamily EngineFamily
        {
            get { return _profile.EngineFamily; }
        }

        public IEngineProfile Profile
        {
            get { return _profile; }
        }

        public IGameResourceProvider ResourceProvider
        {
            get { return _resourceProvider; }
        }

        public IWorld World
        {
            get { return _world; }
        }

        public IEngineApi EngineApi
        {
            get { return _engineApi; }
        }

        public virtual void Initialize(string installationPath)
        {
            if (string.IsNullOrEmpty(installationPath))
            {
                throw new ArgumentException("Installation path cannot be null or empty", nameof(installationPath));
            }

            if (_initialized)
            {
                throw new InvalidOperationException("Engine is already initialized");
            }

            _resourceProvider = CreateResourceProvider(installationPath);
            _world = CreateWorld();
            _engineApi = _profile.CreateEngineApi();
            _initialized = true;
        }

        public virtual void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            if (_world != null)
            {
                _world = null;
            }

            if (_resourceProvider != null)
            {
                _resourceProvider = null;
            }

            if (_engineApi != null)
            {
                _engineApi = null;
            }

            _initialized = false;
        }

        public abstract IEngineGame CreateGameSession();

        protected abstract IGameResourceProvider CreateResourceProvider(string installationPath);

        protected virtual World CreateWorld()
        {
            return new World();
        }
    }
}


