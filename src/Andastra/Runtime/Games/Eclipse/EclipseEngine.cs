using System;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Engines.Common;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;

namespace Andastra.Runtime.Engines.Eclipse
{
    /// <summary>
    /// Abstract base class for Eclipse Engine implementations (Dragon Age and Mass Effect series).
    /// </summary>
    /// <remarks>
    /// Eclipse Engine Base:
    /// - Based on Eclipse/Unreal Engine architecture (Dragon Age, Mass Effect)
    /// - UnrealScript-based: Uses message passing system instead of direct function calls
    /// - Architecture: Different from Odyssey/Aurora (NCS VM) - uses UnrealScript bytecode
    /// - Game-specific implementations: DragonAgeOriginsEngine, DragonAge2Engine, MassEffectEngine, MassEffect2Engine
    ///
    /// Dragon Age: Origins (daorigins.exe):
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
    /// - Inventory/Party/Item: ShowInventoryGUIMessage @ 0x00ae8998, HideInventoryGUIMessage @ 0x00ae89c8
    ///   EquipItemMessage @ 0x00aec670, UnequipItemMessage @ 0x00aec694, AcquireItemMessage @ 0x00aec5f8
    ///   SelectPartyMemberIndexMessage @ 0x00aec88c, ToggleFullPartySelectMessage @ 0x00aec8c8
    ///   ShowPartyPickerMessage @ 0x00aec9c0, ApplyPartyPickerChangesMessage @ 0x00aecb0c
    ///   StoreList @ 0x00af4d88, OpenStoreMessage @ 0x00aef1d0, CloseStoreMessage @ 0x00b017b8
    /// - Journal/Quest: ShowJournalGUIMessage @ 0x00ae89f8, HideJournalGUIMessage @ 0x00ae8a24
    ///   GoToQuestMessage @ 0x00ae8bbc, VSetActiveQuestMessage @ 0x00afa6fa, QuestCompleted @ 0x00b0847c
    /// - Animation: Animation @ 0x00ae5e10, KAnimationNode @ 0x00ae5e52, AnimationTree @ 0x00ae5e70
    ///   ModelAnimationTree @ 0x00ae5e8c, Facial Animation @ 0x00b1adcc, Initialize - Facial Animation @ 0x00ad930c
    /// - Audio/Sound: Initialize - Sound @ 0x00ad9094, Initialize - GUISounds @ 0x00ad9020
    ///   PlaySound @ 0x00b17d7c, PlayGUISound @ 0x00b17af4, StopGUISound @ 0x00b17ad8
    ///   SetSoundSetMessage @ 0x00ae906c, PlaySoundSetEntryMessage @ 0x00ae9094, SoundList @ 0x00af4e80
    /// - Camera: SceneCamera @ 0x00ad8ec4, PanCamera @ 0x00af9fe0, CameraPosition @ 0x00b1913c
    ///   CameraZoomInMessage @ 0x00aecd88, CameraZoomOutMessage @ 0x00aecdb0
    ///   ToggleTacticalCameraMessage @ 0x00aebb88, RotatePaperdollCameraMessage @ 0x00ae8eb8
    /// - Effect: Effect @ 0x00b19014, EffectList @ 0x00af7828, EffectID @ 0x00b19700
    ///   EffectName @ 0x00b19714, EffectParameter @ 0x00b19940, AreaEffectList @ 0x00af4ea0
    ///   CAreaOfEffectObject @ 0x00b0d460, CCVisualEffect @ 0x00b0d440
    /// - Entity Systems: TriggerList @ 0x00af5060, CTrigger @ 0x00b0d4cc, Trigger @ 0x00ae5a7c
    ///   PlaceableList @ 0x00af5028, CPlaceable @ 0x00b0d488, IsPlaceable @ 0x00b047d8
    ///   KWaypointList @ 0x00af4e8f, CWaypoint @ 0x00b0d4b8, WaypointOverride @ 0x00b0b654
    ///   CreatureList @ 0x00af5044, IsCreature @ 0x00af7954, SelectNextCreatureTargetMessage @ 0x00aec1d8
    /// - Perception: PerceptionClass @ 0x00afeb6c
    ///
    /// Dragon Age 2 (DragonAge2.exe):
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
    ///
    /// Mass Effect (MassEffect.exe):
    /// - Save/Load: intABioWorldInfoexecBioSaveGame @ 0x11800ca0
    ///   intABioWorldInfoexecSaveGamesExist @ 0x117fef90
    ///   intABioWorldInfoexecOnSaveGameNotFound @ 0x117fefd8
    ///   intABioWorldInfoexecSetDisplayRealSaveGameNames @ 0x117ff320
    ///   intABioWorldInfoexecGetDisplayRealSaveGameNames @ 0x117ff380
    ///   intUBioSFHandler_PCSaveGameexecSaveComplete @ 0x11811870
    ///   intUBioSFHandler_SaveGameexecSaveComplete @ 0x11812920
    ///   intUBioSFHandler_SaveGameexecSaveGameConfirm @ 0x11812978
    ///   intUBioSaveGameexecApplyGameOptions @ 0x11813c38
    ///   intUBioSaveGameexecSetGameOptions @ 0x11813c80
    ///   intUBioSaveGameexecNativeReset @ 0x11813cc4
    ///   intUBioSaveGameexecGetTimePlayed @ 0x11813d08
    ///   intUBioSaveGameexecGetStorageDevice @ 0x11813d50
    ///   intUBioSaveGameexecSaveCharacter @ 0x11813d98
    ///   intUBioSaveGameexecClearWorldSaveObject @ 0x11813de0
    ///   intUBioSaveGameexecEmptySavedMaps @ 0x11813e30
    ///   intUBioSaveGameexecShowSavingMessageBox @ 0x11813e78
    ///   intUBioSaveGameexecIsAutoSaveComplete @ 0x11813ec8
    ///   intUBioSaveGameexecAutoSaveDelegate @ 0x11813f18
    ///   intUBioSaveGameexecTryAutoSaving @ 0x11813f60
    /// - Dialogue: intUBioConversationexecStartConversation @ 0x117fb620, intUBioConversationexecEndConversation @ 0x117fb5d0
    ///   intUBioConversationexecGetReplyText @ 0x117fb1a0, intUBioConversationexecGetEntryText @ 0x117fb1e8
    ///   intUBioConversationexecGetSpeaker @ 0x117fb230, intUBioConversationexecSelectReply @ 0x117fb530
    ///   intUBioConversationexecUpdateConversation @ 0x117fb578, intUBioConversationexecIsAmbient @ 0x117fb3b8
    ///   intABioWorldInfoexecStartConversation @ 0x117ffa78, intABioWorldInfoexecEndCurrentConversation @ 0x117ffa20
    ///   intABioWorldInfoexecInterruptConversation @ 0x117ff970, intUMassEffectGuiManagerexecIsInConversation @ 0x11813280
    /// - Combat: intUBioActorBehaviorexecEnterCombatStasis @ 0x117ed418, intUBioActorBehaviorexecExitCombatStasis @ 0x117ed3c0
    ///   intABioPlayerSquadexecIsInCombat @ 0x11809418, intABioPlayerSquadexecProbeOnCombatBegin @ 0x118093c0
    ///   intABioPlayerSquadexecProbeOnCombatEnd @ 0x11809370, intUBioGamerProfileexecGetCombatDifficulty @ 0x117e7fe8
    ///   intUBioGamerProfileexecSetCombatDifficulty @ 0x117e8040, intUBioProbeCombatexecStart @ 0x11813920
    ///   intUBioProbeCombatexecStop @ 0x118138e8, intUBioProbeCombatexecReset @ 0x118138b0
    /// - Module/Package: intABioSPGameexecPreloadPackage @ 0x117fede8, Engine.StartupPackages @ 0x11849d54
    ///   Package @ 0x11849d84, intUBioMorphFaceFrontEndexecPreload2DAPackage @ 0x1180ecc0
    ///
    /// Cross-engine notes:
    /// - Eclipse uses UnrealScript message passing (SaveGameMessage, LoadGameMessage) vs Odyssey GFF serialization
    /// - Module system uses MODULES/WRITE_MODULES strings similar to Odyssey but different implementation
    /// - Dialogue uses Conversation class with message handlers vs Odyssey DLG/TLK system
    /// - Combat uses command constants (COMMAND_GETCOMBATSTATE) vs Odyssey combat round system
    /// - Save system uses message-based architecture vs Odyssey direct GFF file I/O
    /// </remarks>
    public abstract class EclipseEngine : BaseEngine
    {
        protected string _installationPath;
        private readonly Game _game;

        protected EclipseEngine(IEngineProfile profile, Game game)
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

            _game = game;
        }

        public Game Game
        {
            get { return _game; }
        }

        public override IEngineGame CreateGameSession()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("Engine must be initialized before creating game session");
            }

            return CreateGameSessionInternal();
        }

        protected abstract IEngineGame CreateGameSessionInternal();

        protected override IGameResourceProvider CreateResourceProvider(string installationPath)
        {
            if (string.IsNullOrEmpty(installationPath))
            {
                throw new ArgumentException("Installation path cannot be null or empty", nameof(installationPath));
            }

            _installationPath = installationPath;

            // Determine game type from installation path and Game enum
            GameType gameType = DetectEclipseGameType(installationPath, _game);

            return new EclipseResourceProvider(installationPath, gameType);
        }

        /// <summary>
        /// Detects the specific Eclipse Engine game type from the installation path and Game enum.
        /// </summary>
        /// <param name="installationPath">The installation path to check.</param>
        /// <param name="game">The Game enum value (already determined from profile).</param>
        /// <returns>The detected game type, or Unknown if detection fails.</returns>
        /// <remarks>
        /// Eclipse Engine Game Detection:
        /// - Based on Eclipse Engine game detection patterns (Dragon Age series, Mass Effect series)
        /// - Detection method: Uses Game enum from profile, with fallback to executable detection
        /// - Dragon Age: Origins: Game.DA or Game.DA_ORIGINS, checks for "daorigins.exe"
        /// - Dragon Age 2: Game.DA2 or Game.DRAGON_AGE_2, checks for "DragonAge2.exe"
        /// - Mass Effect: Game.ME or Game.MASS_EFFECT or Game.ME1, checks for "MassEffect.exe"
        /// - Mass Effect 2: Game.ME2 or Game.MASS_EFFECT_2, checks for "MassEffect2.exe"
        /// - Similar to Odyssey Engine detection pattern (swkotor.exe/swkotor2.exe detection)
        /// - Original implementation: Eclipse Engine executables identify themselves via executable name
        /// - Cross-engine: Similar detection pattern across all BioWare engines (executable name + fallback file checks)
        /// </remarks>
        private static GameType DetectEclipseGameType(string installationPath, Game game)
        {
            if (string.IsNullOrEmpty(installationPath) || !System.IO.Directory.Exists(installationPath))
            {
                return GameType.Unknown;
            }

            // Use Game enum to determine game type
            if (game.IsDragonAgeOrigins())
            {
                // Verify with executable check
                string daOriginsExe = System.IO.Path.Combine(installationPath, "daorigins.exe");
                string daOriginsExeUpper = System.IO.Path.Combine(installationPath, "DAORIGINS.EXE");
                if (System.IO.File.Exists(daOriginsExe) || System.IO.File.Exists(daOriginsExeUpper))
                {
                    // GameType enum doesn't have Dragon Age games yet, return Unknown for now
                    // TODO: Extend GameType enum to support Eclipse Engine games
                    return GameType.Unknown;
                }
            }
            else if (game.IsDragonAge2())
            {
                // Verify with executable check
                string da2Exe = System.IO.Path.Combine(installationPath, "DragonAge2.exe");
                string da2ExeUpper = System.IO.Path.Combine(installationPath, "DRAGONAGE2.EXE");
                if (System.IO.File.Exists(da2Exe) || System.IO.File.Exists(da2ExeUpper))
                {
                    // GameType enum doesn't have Dragon Age games yet, return Unknown for now
                    // TODO: Extend GameType enum to support Eclipse Engine games
                    return GameType.Unknown;
                }
            }
            else if (game.IsMassEffect1())
            {
                // Verify with executable check
                string meExe = System.IO.Path.Combine(installationPath, "MassEffect.exe");
                string meExeUpper = System.IO.Path.Combine(installationPath, "MASSEFFECT.EXE");
                if (System.IO.File.Exists(meExe) || System.IO.File.Exists(meExeUpper))
                {
                    // GameType enum doesn't have Mass Effect games yet, return Unknown for now
                    // TODO: Extend GameType enum to support Eclipse Engine games
                    return GameType.Unknown;
                }
            }
            else if (game.IsMassEffect2())
            {
                // Verify with executable check
                string me2Exe = System.IO.Path.Combine(installationPath, "MassEffect2.exe");
                string me2ExeUpper = System.IO.Path.Combine(installationPath, "MASSEFFECT2.EXE");
                if (System.IO.File.Exists(me2Exe) || System.IO.File.Exists(me2ExeUpper))
                {
                    // GameType enum doesn't have Mass Effect games yet, return Unknown for now
                    // TODO: Extend GameType enum to support Eclipse Engine games
                    return GameType.Unknown;
                }
            }

            // Fallback: Check for packages directory (indicates Eclipse Engine installation)
            string packagesPath = System.IO.Path.Combine(installationPath, "packages");
            if (System.IO.Directory.Exists(packagesPath))
            {
                // Eclipse Engine installation detected via directory structure
                return GameType.Unknown; // GameType enum doesn't support Eclipse games yet
            }

            return GameType.Unknown;
        }
    }
}


