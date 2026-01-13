using Andastra.Game.Games.Engines.Eclipse;

namespace Andastra.Game.Games.Engines.Eclipse.DragonAge2
{
    /// <summary>
    /// Dragon Age 2 game session implementation (DragonAge2.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age 2 Game Session:
    /// - Based on DragonAge2.exe: GameModeController::HandleMessage(SaveGameMessage) @ 0x00d2b330
    /// - Uses DragonAge2ModuleLoader for module loading
    /// - Uses DragonAge2-specific save/load system
    /// </remarks>
    public class DragonAge2GameSession : EclipseGameSession
    {
        public DragonAge2GameSession(DragonAge2Engine engine)
            : base(engine)
        {
        }

        protected override EclipseModuleLoader CreateModuleLoader()
        {
            return new DragonAge2ModuleLoader(_engine.World, _engine.ResourceProvider);
        }
    }
}

