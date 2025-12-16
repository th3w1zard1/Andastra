using System;
using Odyssey.Content.Interfaces;
using Odyssey.Engines.Common;

namespace Odyssey.Engines.Eclipse
{
    /// <summary>
    /// Eclipse Engine implementation for Dragon Age and Mass Effect series.
    /// </summary>
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


