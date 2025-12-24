using Andastra.Parsing.Common;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Runtime.Engines.Eclipse.DragonAgeOrigins
{
    /// <summary>
    /// Dragon Age: Origins engine implementation (daorigins.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age: Origins Engine:
    /// - Based on daorigins.exe: Eclipse/Unreal Engine architecture
    /// - Save/Load: SaveGameMessage @ 0x00ae6276, LoadGameMessage @ 0x00ae9f9c, DeleteSaveGameMessage @ 0x00aec46c
    ///   COMMAND_SAVEGAME @ 0x00af15d4, COMMAND_SAVEGAMEPOSTCAMPAIGN @ 0x00af1500
    /// - Module Loading: LoadModule @ 0x00b17da4, MODULES @ 0x00ad9810, WRITE_MODULES @ 0x00ad98d8
    ///   LoadModuleSkipUnauthorizedMessage @ 0x00ae63f0, CClientExoApp.LoadModuleSkipUnauthorized @ 0x00ae69b0
    /// - Dialogue: ShowConversationGUIMessage @ 0x00ae8a50, HideConversationGUIMessage @ 0x00ae8a88
    ///   Conversation @ 0x00af5888, Conversation.HandleResponseSelection @ 0x00af54b8
    ///   Conversation.OnNPCLineFinished @ 0x00af543f, COMMAND_COMMANDSTARTCONVERSATION @ 0x00af0a64
    ///   COMMAND_HASCONVERSATION @ 0x00af1dc8, COMMAND_INCONVERSATION @ 0x00af1e08
    ///   COMMAND_BEGINCONVERSATION @ 0x00af1e38, COMMAND_SPEAKONELINERCONVERSATION @ 0x00af1e84
    /// - Combat: COMMAND_GETCOMBATSTATE @ 0x00af12fc, COMMAND_SETCOMBATSTATE @ 0x00af1314
    ///   InCombat @ 0x00af76b0, CombatTarget @ 0x00af7840, GameModeCombat @ 0x00af9d9c
    ///   AutoPauseCombat @ 0x00ae7660, Combat_%u @ 0x00af5f80
    /// </remarks>
    public class DragonAgeOriginsEngine : EclipseEngine
    {
        public DragonAgeOriginsEngine(IEngineProfile profile)
            : base(profile, BioWareGame.DA)
        {
        }

        protected override IEngineGame CreateGameSessionInternal()
        {
            return new DragonAgeOriginsGameSession(this);
        }
    }
}

