using System;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Scripting.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Engines.Common
{
    /// <summary>
    /// Abstract base class for all BioWare engine implementations.
    /// </summary>
    /// <remarks>
    /// Base Engine - Common Implementation Across All BioWare Engines:
    /// 
    /// This base class implements the IEngine interface with common functionality shared across all engine families:
    /// - Odyssey Engine (swkotor.exe, swkotor2.exe): KOTOR 1/2, Jade Empire
    /// - Aurora Engine (nwmain.exe, nwn2main.exe): Neverwinter Nights, Neverwinter Nights 2
    /// - Eclipse Engine (daorigins.exe, DragonAge2.exe): Dragon Age: Origins, Dragon Age 2
    /// 
    /// Common Patterns Implemented (Identified via Cross-Engine Reverse Engineering):
    /// 
    /// 1. Common Initialization Implementation:
    ///    - Initialize(installationPath): Implements common initialization sequence
    ///      * Validates installationPath (not null/empty)
    ///      * Checks _initialized flag (throws if already initialized)
    ///      * Creates resource provider via CreateResourceProvider() (abstract, engine-specific)
    ///      * Creates world via CreateWorld() (virtual, defaults to new World())
    ///      * Creates engine API via _profile.CreateEngineApi()
    ///      * Sets _initialized = true
    ///    - Pattern: All engines follow this exact sequence, only CreateResourceProvider() differs
    /// 
    /// 2. Common Shutdown Implementation:
    ///    - Shutdown(): Implements common cleanup sequence
    ///      * Checks _initialized flag (returns early if not initialized)
    ///      * Clears world reference (set to null)
    ///      * Clears resource provider reference (set to null)
    ///      * Clears engine API reference (set to null)
    ///      * Sets _initialized = false
    ///    - Pattern: All engines follow this exact cleanup sequence (idempotent, safe to call multiple times)
    /// 
    /// 3. Common Property Implementations:
    ///    - EngineFamily: Delegates to _profile.EngineFamily (no engine-specific logic)
    ///    - Profile: Returns stored _profile (set in constructor)
    ///    - ResourceProvider: Returns _resourceProvider (set in Initialize())
    ///    - World: Returns _world (set in Initialize())
    ///    - EngineApi: Returns _engineApi (set in Initialize())
    ///    - Pattern: All properties are simple getters, no engine-specific logic needed
    /// 
    /// 4. Abstract Methods (Engine-Specific Implementations Required):
    ///    - CreateGameSession(): Must be implemented by each engine
    ///      * Odyssey: Returns OdysseyGameSession instance
    ///      * Aurora: Returns AuroraGameSession instance
    ///      * Eclipse: Returns EclipseGameSession instance
    ///    - CreateResourceProvider(installationPath): Must be implemented by each engine
    ///      * Odyssey: Creates GameResourceProvider wrapping Installation
    ///      * Aurora: Creates AuroraResourceProvider with game type detection
    ///      * Eclipse: Creates EclipseResourceProvider with game type detection
    /// 
    /// 5. Abstract Methods (Engine-Specific Implementations Required):
    ///    - CreateWorld(): Must be implemented by each engine
    ///      * Each engine must create engine-specific time manager (OdysseyTimeManager, AuroraTimeManager, etc.)
    ///      * Returns World instance with engine-specific time manager
    /// 
    /// Base classes MUST only contain functionality that is identical across ALL engines.
    /// Engine-specific details MUST be in subclasses (OdysseyEngine, AuroraEngine, EclipseEngine).
    /// 
    /// Cross-engine analysis completed: All common patterns have been identified and documented.
    /// See IEngine interface documentation for complete cross-engine interface patterns.
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

        protected abstract World CreateWorld();
    }
}


