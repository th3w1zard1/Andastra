using System;
using System.Threading.Tasks;
using BioWare.NET.Common;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Common;
using JetBrains.Annotations;

namespace Andastra.Game.Engines.Eclipse
{
    /// <summary>
    /// Abstract base class for Eclipse Engine game session implementations.
    /// </summary>
    /// <remarks>
    /// Eclipse Game Session Base:
    /// - Based on Eclipse/Unreal Engine game session management
    /// - Eclipse uses UnrealScript message passing system
    /// - Architecture: Message-based game state management vs Odyssey direct state management
    /// - Game-specific implementations: DragonAgeOriginsGameSession, DragonAge2GameSession, GameSession, 2GameSession
    /// </remarks>
    public abstract class EclipseGameSession : BaseEngineGame
    {
        protected readonly EclipseEngine _engine;
        protected readonly EclipseResourceProvider _eclipseResourceProvider;
        protected readonly EclipseModuleLoader _moduleLoader;

        protected EclipseGameSession(EclipseEngine engine)
            : base(engine)
        {
            if (engine == null)
            {
                throw new ArgumentNullException(nameof(engine));
            }

            _engine = engine;

            // Get Eclipse resource provider
            if (engine.ResourceProvider is EclipseResourceProvider eclipseProvider)
            {
                _eclipseResourceProvider = eclipseProvider;
            }
            else
            {
                throw new InvalidOperationException("Resource provider must be EclipseResourceProvider for Eclipse engine");
            }

            // Initialize module loader
            _moduleLoader = CreateModuleLoader();
        }

        protected abstract EclipseModuleLoader CreateModuleLoader();

        public override async Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                throw new ArgumentException("Module name cannot be null or empty", nameof(moduleName));
            }

            // Load module using Eclipse module loader
            await _moduleLoader.LoadModuleAsync(moduleName, progressCallback);

            // Update game session state
            CurrentModuleName = moduleName;
        }

        protected override void OnUnloadModule()
        {
            // Unload module using module loader
            if (_moduleLoader != null)
            {
                _moduleLoader.UnloadModule();
            }
        }
    }
}

