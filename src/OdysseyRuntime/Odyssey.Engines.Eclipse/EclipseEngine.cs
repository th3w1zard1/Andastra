using System;
using Odyssey.Content.Interfaces;
using Odyssey.Engines.Common;

namespace Odyssey.Engines.Eclipse
{
    /// <summary>
    /// Eclipse Engine implementation for Dragon Age and Mass Effect series.
    /// </summary>
    /// <remarks>
    /// Eclipse Engine:
    /// - Based on Eclipse/Unreal Engine architecture (Dragon Age, Mass Effect)
    /// - Note: This engine is for different games (DA/ME), not KOTOR, so no swkotor2.exe references needed
    /// - Engine initialization: Similar pattern to Odyssey engine but for Eclipse-based games
    /// - Resource provider: Uses Eclipse-specific resource system (package files, etc.)
    /// - Game session: Coordinates module loading, entity management, script execution for Eclipse games
    /// </remarks>
    public class EclipseEngine : BaseEngine
    {
        public EclipseEngine(IEngineProfile profile)
            : base(profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (profile.EngineFamily != EngineFamily.Eclipse)
            {
                throw new ArgumentException("Profile must be for Eclipse engine family", nameof(profile));
            }
        }

        public override IEngineGame CreateGameSession()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Engine must be initialized before creating game session");
            }

            // TODO: Implement EclipseGameSession
            throw new NotImplementedException("Eclipse game session not yet implemented");
        }

        protected override IGameResourceProvider CreateResourceProvider(string installationPath)
        {
            // TODO: Implement Eclipse-specific resource provider
            throw new NotImplementedException("Eclipse resource provider not yet implemented");
        }
    }
}


