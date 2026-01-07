using Andastra.Runtime.Engines.Eclipse;

namespace Andastra.Runtime.Engines.Eclipse.DragonAgeOrigins
{
    /// <summary>
    /// Dragon Age: Origins game session implementation (daorigins.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age: Origins Game Session:
    /// - Based on daorigins.exe: Game session management
    /// - Uses DragonAgeOriginsModuleLoader for module loading
    /// - Uses DragonAgeOrigins-specific save/load system
    /// </remarks>
    public class DragonAgeOriginsGameSession : EclipseGameSession
    {
        public DragonAgeOriginsGameSession(DragonAgeOriginsEngine engine)
            : base(engine)
        {
        }

        protected override EclipseModuleLoader CreateModuleLoader()
        {
            return new DragonAgeOriginsModuleLoader(_engine.World, _engine.ResourceProvider);
        }
    }
}

