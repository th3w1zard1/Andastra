using System;
using BioWare.NET.Common;
using BioWare.NET.Extract.Installation;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Engines.Common;
using Andastra.Game.Games.Eclipse;

namespace Andastra.Game.Engines.Eclipse
{
    /// <summary>
    /// Abstract base class for Eclipse Engine implementations (Dragon Age series).
    /// </summary>
    /// <remarks>
    /// Eclipse Engine Base:
    /// - Based on Eclipse engine architecture (Dragon Age)
    /// - UnrealScript-based: Uses message passing system instead of direct function calls
    /// - Architecture: Different from Odyssey/Aurora (NCS VM) - uses UnrealScript bytecode
    /// - Game-specific implementations: DragonAgeOriginsEngine, DragonAge2Engine
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
        private readonly BioWareGame _game;

        protected EclipseEngine(IEngineProfile profile, BioWareGame game)
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

        public BioWareGame Game
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
        /// - Based on Eclipse Engine game detection patterns (Dragon Age series)
        /// - Detection method: Uses Game enum from profile, with fallback to executable detection
        /// - Dragon Age: Origins: BioWareGame.DA or BioWareGame.DA_ORIGINS, checks for "daorigins.exe"
        /// - Dragon Age 2: BioWareGame.DA2 or Game.DRAGON_AGE_2, checks for "DragonAge2.exe"
        /// - Similar to Odyssey Engine detection pattern (swkotor.exe/swkotor2.exe detection)
        /// - Original implementation: Eclipse Engine executables identify themselves via executable name
        /// - Cross-engine: Similar detection pattern across all BioWare engines (executable name + fallback file checks)
        /// </remarks>
        private static GameType DetectEclipseGameType(string installationPath, BioWareGame game)
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
                    return GameType.DA_ORIGINS;
                }
            }
            else if (game.IsDragonAge2())
            {
                // Verify with executable check
                string da2Exe = System.IO.Path.Combine(installationPath, "DragonAge2.exe");
                string da2ExeUpper = System.IO.Path.Combine(installationPath, "DRAGONAGE2.EXE");
                if (System.IO.File.Exists(da2Exe) || System.IO.File.Exists(da2ExeUpper))
                {
                    return GameType.DA2;
                }
            }

            // Fallback: Check for packages directory (indicates Eclipse Engine installation)
            string packagesPath = System.IO.Path.Combine(installationPath, "packages");
            if (System.IO.Directory.Exists(packagesPath))
            {
                // Eclipse Engine installation detected via directory structure
                // Try to determine which Eclipse game based on additional file checks
                // Dragon Age: Origins typically has "data" subdirectory with RIM files
                // Dragon Age 2 may have different structure or additional files
                string dataPath = System.IO.Path.Combine(installationPath, "data");
                if (System.IO.Directory.Exists(dataPath))
                {
                    // Check for Dragon Age: Origins specific files
                    string globalRim = System.IO.Path.Combine(dataPath, "global.rim");
                    if (System.IO.File.Exists(globalRim))
                    {
                        // Likely Dragon Age: Origins (has global.rim)
                        return GameType.DA_ORIGINS;
                    }
                }

                // If we can't determine, default to DA_ORIGINS for packages directory structure
                // (most Eclipse Engine installations are DA:O)
                return GameType.DA_ORIGINS;
            }

            return GameType.Unknown;
        }

        protected override World CreateWorld()
        {
            var timeManager = new EclipseTimeManager();
            return new World(timeManager);
        }
    }
}


