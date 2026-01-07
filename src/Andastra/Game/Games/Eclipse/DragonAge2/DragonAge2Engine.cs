using Andastra.Parsing.Common;
using Andastra.Runtime.Engines.Common;

namespace Andastra.Runtime.Engines.Eclipse.DragonAge2
{
    /// <summary>
    /// Dragon Age 2 engine implementation (DragonAge2.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age 2 Engine:
    /// - Based on DragonAge2.exe: Eclipse/Unreal Engine architecture
    /// - Save/Load: SaveGameMessage @ 0x00be37a8, DeleteSaveGameMessage @ 0x00be389c
    ///   SaveGameMessage -1 2 @ 0x00bff680, savegame @ 0x00c01f40, savegame_screenshot.dds @ 0x00c055d4
    ///   _CAN_IMPORT_SAVEGAME @ 0x00c06cd8, TaskSaveGame @ 0x00d36e5c
    ///   GameModeController::HandleMessage(SaveGameMessage) @ 0x00d2b330
    ///   SaveLoadProxy::HandleMessage(SaveGameMessage) @ 0x00d37e50
    ///   SaveLoadProxy::HandleMessage(DeleteSaveGameMessage) @ 0x00d38190
    /// - Module Loading: LoadModuleMessage @ 0x00bf5df8, MODULES: @ 0x00bf5d10, WRITE_MODULES: @ 0x00bf5d24
    ///   ModuleID @ 0x00be9688, ModuleStartupInfo @ 0x00bebb64, ModuleInfoList @ 0x00bfa278
    ///   GetMainModuleName @ 0x00c0ed00, GetCurrentModuleName @ 0x00c0ed24, CModule @ 0x00c236b4
    /// - Dialogue: ShowConversationGUIMessage @ 0x00bfca24, HideConversationGUIMessage @ 0x00bfca5c
    ///   Conversation @ 0x00bf8538, GameModeConversation @ 0x00bedd54, Conversation.OnNPCLineFinished @ 0x00bee28c
    ///   SkipConversationMessage @ 0x00be2f60, Conversation.PushResponseButtonFromGame @ 0x00be83b0
    /// - Combat: GameModeCombat @ 0x00beaf3c, InCombat @ 0x00bf4c10, CombatTarget @ 0x00bf4dc0
    ///   Combat_%u @ 0x00be0ba4, BInCombatMode @ 0x00beeed2, AutoPauseCombat @ 0x00bf6f9c
    /// </remarks>
    public class DragonAge2Engine : EclipseEngine
    {
        public DragonAge2Engine(IEngineProfile profile)
            : base(profile, BioWareGame.DA2)
        {
        }

        protected override IEngineGame CreateGameSessionInternal()
        {
            return new DragonAge2GameSession(this);
        }
    }
}

