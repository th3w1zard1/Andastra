using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Andastra.Game.Games.Odyssey.Combat;
using Andastra.Game.Games.Odyssey.Components;
using Andastra.Game.Games.Odyssey.Dialogue;
using Andastra.Game.Games.Odyssey.EngineApi;
using Andastra.Game.Games.Odyssey.Input;
using Andastra.Game.Scripting.Interfaces;
using Andastra.Game.Scripting.VM;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Game;
using Andastra.Runtime.Core.GameLoop;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Journal;
using RuntimeCore = Andastra.Runtime.Core;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Core.Movement;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Core.Party;
using Andastra.Runtime.Core.Plot;
using ModuleState = Andastra.Runtime.Core.Module.ModuleState;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using JetBrains.Annotations;
using Andastra.Game.Games.Common;
using Andastra.Game.Games.Odyssey.Systems;
using GameDataManager = Andastra.Game.Games.Odyssey.Data.GameDataManager;
using TriggerSystem = Andastra.Runtime.Core.Triggers.TriggerSystem;
using AIControllerSystem = Andastra.Runtime.Games.Common.AIControllerSystem;

namespace Andastra.Game.Games.Odyssey.Game
{
    /// <summary>
    /// Main game session manager that coordinates all game systems.
    /// </summary>
    /// <remarks>
    /// Game Session System (Odyssey-specific):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x006caab0 @ 0x006caab0 (server command parser, handles module commands)
    /// - Located via string references:
    ///   - ["GAMEINPROGRESS"] @ (K1: TODO: Find address, TSL: 0x007c15c8)
    ///   - ["ModuleLoaded"] @ (K1: TODO: Find address, TSL: 0x007bdd70)
    ///   - ["ModuleRunning"] @ (K1: TODO: Find address, TSL: 0x007bdd58)
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe):
    ///     - ["CServerExoApp::LoadModule"] @ (K1: TODO: Find address, TSL: TODO: Find this address address, NWN:EE: TODO: Find this address address)
    ///     - ["CNWSModule::LoadModule"] @ (K1: TODO: Find address, TSL: TODO: Find this address address, NWN:EE: TODO: Find this address address) - similar module loading system, different file formats
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ):
    ///     - ["LoadModule"] @ (K1: TODO: Find address, TSL: TODO: Find this address address, NWN:EE: TODO: Find this address address) - UnrealScript-based module loading, different architecture
    /// - Inheritance: Base class BaseGameSession (Andastra.Game.Games.Common) - abstract game session, Odyssey override (Runtime.Games.Odyssey) - Odyssey-specific game session
    /// - Original implementation: Coordinates module loading, entity management, script execution, combat, AI, triggers, dialogue, party
        /// - Module state:
        ///   - SetModuleState @ (K1: TODO: Find address, TSL: 0x006caab0) - sets module state flags
        ///   - in ["DAT_008283d4"] @ (K1: TODO: Find address, TSL: TODO: 0x008283d4) structure:
        ///     * 0=Idle
        ///     * 1=ModuleLoaded
        ///     * 2=ModuleRunning
    /// - Game loop integration:
    ///   - [Update]() @ (K1: TODO: Find address, TSL: TODO: Find this address address) - called every frame to update all systems (60 Hz fixed timestep)
    /// - System initialization order: Installation/ModuleLoader -> FactionManager -> PerceptionManager -> CombatManager -> PartySystem -> Engine API -> ScriptExecutor -> TriggerSystem/AIController/DialogueManager/EncounterSystem
    /// </remarks>
    public class GameSession
    {
        private readonly GameSettings _settings;
        private readonly World _world;
        private readonly NcsVm _vm;
        private readonly IScriptGlobals _globals;
        private readonly Installation _installation;
        private readonly Loading.ModuleLoader _moduleLoader;
        private readonly GameDataManager _gameDataManager;

        // Game systems
        private readonly TriggerSystem _triggerSystem;
        private readonly AIControllerSystem _aiController;
        private readonly ModuleTransitionSystem _moduleTransitionSystem;
        private readonly DialogueManager _dialogueManager;
        private readonly FactionManager _factionManager;
        private readonly PerceptionManager _perceptionManager;
        private readonly CombatManager _combatManager;
        private readonly PartySystem _partySystem;
        private readonly IScriptExecutor _scriptExecutor;
        private readonly IEngineApi _engineApi;
        private readonly Systems.EncounterSystem _encounterSystem;
        private readonly JournalSystem _journalSystem;
        private readonly PlotSystem _plotSystem;
        private readonly FixedTimestepGameLoop _gameLoop;
        private readonly PlayerInputHandler _inputHandler;
        private readonly ModuleStateManager _moduleStateManager;

        // Current game state
        private RuntimeModule _currentModule;
        private IEntity _playerEntity;
        private string _currentModuleName;
        private float _moduleHeartbeatTimer;
        private CharacterCreationData _pendingCharacterData;

        /// <summary>
        /// Gets the current player entity.
        /// </summary>
        [CanBeNull]
        public IEntity PlayerEntity
        {
            get { return _playerEntity; }
        }

        /// <summary>
        /// Gets the current module name.
        /// </summary>
        [CanBeNull]
        public string CurrentModuleName
        {
            get { return _currentModuleName; }
        }

        /// <summary>
        /// Gets the current runtime module.
        /// </summary>
        [CanBeNull]
        public RuntimeModule CurrentRuntimeModule
        {
            get { return _currentModule; }
        }

        /// <summary>
        /// Gets the dialogue manager.
        /// </summary>
        [CanBeNull]
        public DialogueManager DialogueManager
        {
            get { return _dialogueManager; }
        }

        /// <summary>
        /// Gets the navigation mesh for the current area.
        /// </summary>
        [CanBeNull]
        public INavigationMesh NavigationMesh
        {
            get
            {
                if (_currentModule == null)
                {
                    return null;
                }
                IArea currentArea = _world.CurrentArea;
                return currentArea?.NavigationMesh;
            }
        }

        /// <summary>
        /// Gets the installation for resource access.
        /// </summary>
        [CanBeNull]
        public Installation Installation
        {
            get { return _installation; }
        }

        /// <summary>
        /// Gets the world instance.
        /// </summary>
        [CanBeNull]
        public World World
        {
            get { return _world; }
        }

        /// <summary>
        /// Gets the cached BioWare.NET Module object for the currently loaded module.
        /// Returns null if no module is loaded. This provides efficient access to module resources
        /// without creating a new Module object on every access.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module objects are cached and reused for resource lookups.
        /// </summary>
        [CanBeNull]
        public BioWare.NET.Common.Module GetCurrentParsingModule()
        {
            return _moduleLoader?.GetCurrentModule();
        }

        /// <summary>
        /// Gets whether the game is currently paused.
        /// </summary>
        public bool IsPaused
        {
            get
            {
                return _world != null && _world.TimeManager != null && _world.TimeManager.IsPaused;
            }
        }

        /// <summary>
        /// Pauses the game.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): PauseGame implementation
        /// Located via string references: Game pause system
        /// Original implementation: Pauses all game systems except UI (combat, movement, scripts suspended)
        /// </remarks>
        public void Pause()
        {
            OnPauseChanged(true);
        }

        /// <summary>
        /// Resumes the game.
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): ResumeGame implementation
        /// Located via string references: Game pause system
        /// Original implementation: Resumes all game systems (combat, movement, scripts resume)
        /// </remarks>
        public void Resume()
        {
            OnPauseChanged(false);
        }

        /// <summary>
        /// Creates a new game session.
        /// </summary>
        public GameSession(GameSettings settings, World world, NcsVm vm, IScriptGlobals globals)
        {
            _settings = settings ?? throw new ArgumentNullException("settings");
            _world = world ?? throw new ArgumentNullException("world");
            _vm = vm ?? throw new ArgumentNullException("vm");
            _globals = globals ?? throw new ArgumentNullException("globals");

            // Initialize installation and module loader
            try
            {
                _installation = new Installation(_settings.GamePath);
                _moduleLoader = new Loading.ModuleLoader(_installation);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GameSession] Failed to initialize installation: " + ex.Message);
                throw;
            }

            // Initialize game data provider for world
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): GameDataManager provides access to 2DA tables (appearance.2da, etc.)
            // Located via string references: "2DAName" @ 0x007c3980, " 2DA file" @ 0x007c4674
            // Original implementation: GameDataManager loads and caches 2DA tables from installation
            _gameDataManager = new GameDataManager(_installation);
            var gameDataProvider = new Andastra.Game.Games.Odyssey.Data.OdysseyGameDataProvider(_gameDataManager);
            _world.GameDataProvider = gameDataProvider;

            // Initialize game systems
            _factionManager = new FactionManager(_world);
            _perceptionManager = new PerceptionManager(_world, _world.EffectSystem);

            // Create entity template factory for party system
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Party members are created from UTC templates stored in module
            // Located via string references: "TemplateResRef" @ 0x007bd00c
            // Original implementation: Party members use TemplateResRef to load UTC templates from module archives
            // Use lazy template factory that retrieves module from ModuleLoader on demand
            // This allows factory to be created before module is loaded (required for constructor initialization order)
            IEntityTemplateFactory templateFactory = new Loading.LazyOdysseyEntityTemplateFactory(_moduleLoader.EntityFactory, _moduleLoader);
            _partySystem = new PartySystem(_world, templateFactory);
            _combatManager = new CombatManager(_world, _factionManager, _partySystem);

            // Initialize engine API (unified OdysseyEngineApi with conditional logic based on game type)
            BioWareGame gameType = _settings.Game == KotorGame.K1 ? BioWareGame.K1 : BioWareGame.K2;
            _engineApi = new OdysseyEngineApi(gameType);

            // Initialize script executor (unified OdysseyScriptExecutor handles both K1 and TSL)
            _scriptExecutor = new OdysseyScriptExecutor(_world, _engineApi, _globals, _installation, null);

            // Initialize trigger system with script firing callback
            _triggerSystem = new TriggerSystem(_world, FireScriptEvent);

            // Initialize AI controller (unified system with Odyssey engine)
            _aiController = new AIControllerSystem(_world, EngineFamily.Odyssey, FireScriptEvent);

            // Initialize JRL loader for quest entry text lookup
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): JRL files contain quest entry text
            // Original implementation: Loads JRL files to look up quest entry text
            JRLLoader jrlLoader = new JRLLoader(_installation);

            // Initialize journal system with JRL loader
            _journalSystem = new JournalSystem(jrlLoader);

            // Initialize plot system
            _plotSystem = new PlotSystem();

            // Initialize dialogue manager
            _dialogueManager = new DialogueManager(
                _vm,
                _world,
                _engineApi,
                _globals,
                (resRef) => LoadDialogue(resRef),
                (resRef) => LoadScript(resRef),
                null, // voicePlayer
                null, // lipSyncController
                _journalSystem, // journalSystem
                _gameDataManager, // gameDataManager (for plot.2da access)
                _partySystem, // partySystem (for XP awards)
                _plotSystem, // plotSystem (for plot state tracking)
                jrlLoader // jrlLoader (for quest entry text lookup)
            );

            // Initialize module transition system
            _moduleTransitionSystem = new ModuleTransitionSystem(
                async (moduleName) => await LoadModuleAsync(moduleName),
                (waypointTag) => PositionPlayerAtWaypoint(waypointTag)
            );

            // Initialize encounter system
            _encounterSystem = new Systems.EncounterSystem(
                _world,
                _factionManager,
                FireScriptEvent,
                null, // Loading.ModuleLoader not used - we use Game.ModuleLoader instead
                (entity) => entity == _playerEntity || (entity != null && entity.GetData<bool>("IsPlayer")),
                () => _moduleLoader?.GetCurrentModule() // Use cached Module object for efficiency
            );

            // Initialize input handler for player control
            // Based on swkotor.exe (K1) vs swkotor2.exe (K2) input system differences
            // Located via string references: "Input" @ 0x007c2520, "Mouse" @ 0x007cb908, "OnClick" @ 0x007c1a20
            // Original implementation: DirectInput8-based input system with click-to-move, object selection, party control
            // K1 (swkotor.exe): Simpler input system without K2-specific features (Influence, Prestige Classes, Combat Forms)
            // K2 (swkotor2.exe): Enhanced input system with additional features
            _inputHandler = _settings.Game == KotorGame.K1
                ? (PlayerInputHandler)new K1PlayerInputHandler(_world, _partySystem)
                : (PlayerInputHandler)new K2PlayerInputHandler(_world, _partySystem);

            // Wire up input handler events
            _inputHandler.OnMoveCommand += OnMoveCommand;
            _inputHandler.OnAttackCommand += OnAttackCommand;
            _inputHandler.OnInteractCommand += OnInteractCommand;
            _inputHandler.OnTalkCommand += OnTalkCommand;
            _inputHandler.OnPauseChanged += OnPauseChanged;
            _inputHandler.OnLeaderCycled += OnLeaderCycled;
            _inputHandler.OnQuickSlotUsed += OnQuickSlotUsed;

            // Subscribe to door opened events for module transitions
            _world.EventBus.Subscribe<DoorOpenedEvent>(OnDoorOpened);

            // Initialize fixed-timestep game loop
            _gameLoop = new FixedTimestepGameLoop(_world);

            // Initialize module state manager (swkotor2.exe: 0x006caab0 @ 0x006caab0)
            _moduleStateManager = new ModuleStateManager();
            _moduleStateManager.SetModuleState(ModuleState.Idle);

            Console.WriteLine("[GameSession] Game session initialized");
        }

        /// <summary>
        /// Updates all game systems using fixed-timestep game loop.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        public void Update(float deltaTime)
        {
            if (_world == null)
            {
                return;
            }

            // Update fixed-timestep game loop (handles all simulation phases)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Fixed-timestep game loop at 60 Hz
            // Located via string references: Game loop runs at fixed timestep for deterministic simulation
            // Original implementation: Fixed timestep ensures deterministic behavior for scripts, combat, AI
            // Game loop phases: Input, Script, Simulation, Animation, Scene Sync, Render, Audio
            if (_gameLoop != null)
            {
                _gameLoop.Update(deltaTime);
            }

            // Update dialogue system (handled separately as it may need variable timestep for VO timing)
            if (_dialogueManager != null)
            {
                _dialogueManager.Update(deltaTime);
            }

            // Update encounter system (spawns creatures when triggered)
            if (_encounterSystem != null)
            {
                _encounterSystem.Update(deltaTime);
            }

            // Update module heartbeat (fires every 6 seconds)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00501fa0 @ 0x00501fa0 (module loading), 0x00501fa0 reads "Mod_OnHeartbeat" script from module GFF
            // Located via string references: "Mod_OnHeartbeat" @ 0x007be840
            // Original implementation: Module heartbeat fires every 6 seconds for module-level scripts
            // Module heartbeat script is loaded from Mod_OnHeartbeat field in module IFO GFF during module load
            // Module state flags: 0=Idle, 1=ModuleLoaded, 2=ModuleRunning (set in 0x006caab0 @ 0x006caab0)
            if (_currentModule != null)
            {
                _moduleHeartbeatTimer += deltaTime;
                if (_moduleHeartbeatTimer >= 6.0f)
                {
                    _moduleHeartbeatTimer -= 6.0f;
                    FireModuleHeartbeat();
                }
            }
        }

        /// <summary>
        /// Starts a new game, loading the starting module and creating the player entity.
        /// </summary>
        /// <param name="characterData">Optional character creation data. If provided, player entity will be created from this data. If null, a default player entity will be created.</param>
        /// <remarks>
        /// Low-level implementation documentation for New Game button handler across all executables.
        /// All addresses documented for swkotor.exe, swkotor2.exe, and swkotor2_aspyr.exe.
        ///
        /// ENTRY POINT FUNCTIONS:
        /// - swkotor.exe: CSWGuiMainMenu::OnNewGamePicked @ 0x0067afb0 (member function of CSWGuiMainMenu class, called directly from GUI event system)
        /// - swkotor2.exe: OnNewGameButtonClicked @ 0x006d0b00 (registered by FUN_006d2350 @ 0x006d2350 via FUN_0041a340 @ 0x0041a340 with event type 0x27 at line 89, also registered with event type 0x2d at line 96)
        /// - swkotor2_aspyr.exe: FUN_00882230 @ 0x00882230 (equivalent entry point function, called from GUI event system)
        ///
        /// EVENT HANDLER REGISTRATION:
        /// - swkotor.exe: Direct function call from GUI system (no explicit event registration function used)
        /// - swkotor2.exe: Main menu handler FUN_006d2350 @ 0x006d2350 (constructor/initializer) registers handlers via FUN_0041a340 @ 0x0041a340 (event registration function that manages event handler table, searches existing handlers by event type, adds new handlers or updates existing ones). New Game button hover event (0x27): Registered at FUN_006d2350 line 89, callback @ 0x006d0b00. New Game button click event (0x2d): Registered at FUN_006d2350 line 96, callback @ 0x006d0b00
        /// - swkotor2_aspyr.exe: Event registration handled through equivalent GUI system (no explicit registration function address available in this context)
        ///
        /// STRING REFERENCES:
        /// - Module name "END_M01AA": swkotor.exe @ 0x00752f58 (referenced at 0x0067b01b in CExoString::CExoString constructor call, referenced at 0x0067b0b9 in CExoString::operator= fallback assignment). swkotor2.exe: Not used (uses "001ebo" instead). swkotor2_aspyr.exe: Not used (uses "001ebo" instead).
        /// - Module name "001ebo": swkotor.exe: Not used (uses "END_M01AA" instead). swkotor2.exe @ 0x007cc028 (referenced at 0x006d0b7d in CExoString::CExoString constructor call with "001ebo", referenced at 0x006d0c5e in FUN_00630d10 fallback assignment). swkotor2_aspyr.exe @ 0x009a5ab0 (referenced at 0x008822ba in FUN_00733570 constructor call with "001ebo", referenced at 0x00882385 in FUN_007338d0 fallback assignment)
        /// - Resource directory "MODULES:": swkotor.exe @ 0x0073d90c (referenced at 0x0067b033 in CExoString::CExoString constructor for AddResourceDirectory call, referenced at 0x0067b0c7 in CExoString::CExoString constructor for RemoveResourceDirectory call). swkotor2.exe @ 0x007b58b4 (referenced at 0x006d0bdc in CExoString::CExoString constructor for FUN_00408a30 call, referenced at 0x006d0c6c in CExoString::CExoString constructor for FUN_00408b00 call). swkotor2_aspyr.exe @ 0x00993e50 (referenced in FUN_00733570 constructor calls for FUN_00711690 and FUN_00711710 operations)
        /// - Resource directory "HD0:effects": swkotor.exe: Not used. swkotor2.exe @ 0x007cc01c (referenced at 0x006d0b95 in CExoString::CExoString constructor call, used in FUN_004087d0 for resource directory addition). swkotor2_aspyr.exe @ 0x009a5aa4 (referenced at 0x008822cb in FUN_00733570 constructor call, used in FUN_00716da0 for resource directory addition)
        ///
        /// GLOBAL DATA REFERENCES:
        /// - ExoResMan (CExoResMan*): swkotor.exe @ 0x007a39e8 (used in CExoResMan::AddResourceDirectory @ 0x00408800, CExoResMan::Exists @ 0x00408bc0, CExoResMan::RemoveResourceDirectory @ 0x004088d0). swkotor2.exe: Equivalent global at DAT_008283c0 @ 0x008283c0. swkotor2_aspyr.exe: Equivalent global at DAT_00a1b490 @ 0x00a1b490
        /// - ExoSound (CExoSound*): swkotor.exe @ 0x007a39ec (used in CExoSoundInternal::SetSoundMode @ 0x005d5e80). swkotor2.exe: Equivalent global at DAT_008283c4 @ 0x008283c4. swkotor2_aspyr.exe: Equivalent global at DAT_00a1b494 @ 0x00a1b494
        /// - CAppManager* global: swkotor.exe: Not used in New Game handler context. swkotor2.exe @ DAT_008283d4 @ 0x008283d4 (used in FUN_00401380 @ 0x00401380 for module loading system initialization, used in FUN_006394b0 @ 0x006394b0 for unknown function call). swkotor2_aspyr.exe @ DAT_00a1b4a4 @ 0x00a1b4a4 (used in FUN_00401bc0 @ 0x00401bc0 for module loading system initialization, used in FUN_00741360 @ 0x00741360 for unknown function call)
        /// - CExoResMan* global (swkotor2/swkotor2_aspyr): swkotor.exe: Uses ExoResMan @ 0x007a39e8 instead. swkotor2.exe @ DAT_008283c0 @ 0x008283c0 (used in FUN_00408a30 @ 0x00408a30 for AddResourceDirectory equivalent, used in FUN_00408df0 @ 0x00408df0 for Exists equivalent, used in FUN_00408b00 @ 0x00408b00 for RemoveResourceDirectory equivalent). swkotor2_aspyr.exe @ DAT_00a1b490 @ 0x00a1b490 (used in FUN_00711690 @ 0x00711690 for AddResourceDirectory equivalent, used in FUN_00711ed0 @ 0x00711ed0 for Exists equivalent, used in FUN_00711710 @ 0x00711710 for RemoveResourceDirectory equivalent)
        /// - Sound system global (swkotor2/swkotor2_aspyr): swkotor.exe: Uses ExoSound @ 0x007a39ec instead. swkotor2.exe @ DAT_008283c4 @ 0x008283c4 (used in FUN_00621ab0 @ 0x00621ab0 for SetSoundMode equivalent). swkotor2_aspyr.exe @ DAT_00a1b494 @ 0x00a1b494 (used in FUN_0070bc60 @ 0x0070bc60 for SetSoundMode equivalent)
        /// - Sound mode constant: swkotor.exe @ DAT_0074c5ec @ 0x0074c5ec (used in CExoSoundInternal::SetSoundMode @ 0x005d5e80). swkotor2.exe @ DAT_007c5474 @ 0x007c5474 (used in FUN_00621ab0 @ 0x00621ab0). swkotor2_aspyr.exe @ DAT_0099c2a8 @ 0x0099c2a8 (used in FUN_0070bc60 @ 0x0070bc60)
        ///
        /// RESOURCE TYPE CONSTANTS:
        /// - MOD (Module): 0x7db (2011 decimal) - swkotor.exe: Uses MOD type constant via CExoResMan::Exists() @ 0x00408bc0 with MOD parameter (no explicit constant value, uses ResourceType enum, referenced at 0x0067b0a5 in first Exists call, referenced at 0x0067b0b3 in second Exists call with RIM fallback). swkotor2.exe @ 0x006d0c22 (FUN_00408df0 call with 0x7db constant, checks if module resource exists as MOD type before fallback to RIM). swkotor2_aspyr.exe @ 0x008822d1 (FUN_00711ed0 call with 0x7db constant, checks if module resource exists as MOD type before fallback to RIM)
        /// - RIM (Resource Information Module): 0xbba (3002 decimal) - swkotor.exe: Uses RIM type constant via CExoResMan::Exists() @ 0x00408bc0 with RIM parameter (no explicit constant value, uses ResourceType enum, referenced at 0x0067b0b3 as fallback when MOD check fails). swkotor2.exe @ 0x006d0c4b (FUN_00408df0 call with 0xbba constant, fallback check when MOD type (0x7db) resource not found). swkotor2_aspyr.exe @ 0x008822e4 (FUN_00711ed0 call with 0xbba constant, fallback check when MOD type (0x7db) resource not found)
        /// - DIRECTORY: 2 (directory resource type constant) - swkotor.exe @ 0x00408800 (CExoResMan::AddResourceDirectory calls AddKeyTable with DIRECTORY parameter value 2, referenced at 0x0067b03a). swkotor.exe @ 0x004088d0 (CExoResMan::RemoveResourceDirectory calls RemoveKeyTable with DIRECTORY parameter value 2, referenced at 0x0067b0c7). swkotor2.exe @ 0x00408a30 (FUN_00408a30 calls FUN_00406ef0 with DIRECTORY parameter 2, referenced at 0x006d0bfa). swkotor2.exe @ 0x00408b00 (FUN_00408b00 calls FUN_00407900 with DIRECTORY parameter 2, referenced at 0x006d0c8a). swkotor2_aspyr.exe @ 0x00711690 (FUN_00711690 calls FUN_00711750 with DIRECTORY parameter 2, referenced at 0x0088230f). swkotor2_aspyr.exe @ 0x00711710 (FUN_00711710 calls FUN_007131d0 with DIRECTORY parameter 2, referenced at 0x008823a3)
        ///
        /// UTILITY FUNCTIONS:
        /// - CExoString constructors: swkotor.exe @ 0x005b3190 (empty), @ 0x005e5a90 (from char*), swkotor2.exe @ 0x005ff130 (empty), @ 0x00630a90 (from char*), swkotor2_aspyr.exe @ 0x00733540 (empty), @ 0x00733570 (from uint*)
        /// - CExoString destructors: swkotor.exe @ 0x005e5c20, swkotor2.exe @ 0x00630c20, swkotor2_aspyr.exe @ 0x00733780
        /// - CExoString assignment: swkotor.exe @ 0x005e5140 (operator= from char*)
        /// - CResRef constructors: swkotor.exe @ 0x00406d60 (from CExoString*), swkotor2.exe @ 0x00406e70 (from undefined4*), swkotor2_aspyr.exe @ 0x00710810 (from int*)
        /// - Memory allocation: swkotor.exe @ 0x006fa7e6 (operator_new), swkotor2.exe @ 0x0076d9f6 (operator_new), swkotor2_aspyr.exe @ 0x00919723 (FUN_00919723 via _malloc)
        /// - CExoIni constructors/destructors: swkotor.exe @ 0x005e6750 (constructor), @ 0x005e67e0 (destructor)
        /// - Temporary object management: swkotor2.exe @ 0x00631f70 (FUN_00631f70 constructor), @ 0x00632000 (FUN_00632000 destructor), swkotor2_aspyr.exe @ 0x00736240 (FUN_00736240 constructor), @ 0x007362c0 (FUN_007362c0 destructor)
        /// - Resource directory operations: swkotor2.exe @ 0x004087d0 (FUN_004087d0 copy assignment), swkotor2_aspyr.exe @ 0x00716da0 (FUN_00716da0 copy assignment)
        /// - Module resource existence checks: swkotor2.exe @ 0x00408df0 (FUN_00408df0), swkotor2_aspyr.exe @ 0x00711ed0 (FUN_00711ed0)
        /// - Panel management: swkotor2.exe @ 0x0040bf90 (FUN_0040bf90 AddPanel equivalent), swkotor2_aspyr.exe @ 0x00410530 (FUN_00410530 AddPanel equivalent)
        /// - Sound mode setting: swkotor2.exe @ 0x00621ab0 (FUN_00621ab0), swkotor2_aspyr.exe @ 0x0070bc60 (FUN_0070bc60)
        /// - Game time/system initialization: swkotor2.exe @ 0x0057a400 (FUN_0057a400), swkotor2_aspyr.exe @ 0x005ff000 (FUN_005ff000)
        /// - Module loading system initialization: swkotor2.exe @ 0x00401380 (FUN_00401380), swkotor2_aspyr.exe @ 0x00401bc0 (FUN_00401bc0)
        /// - Post-cleanup checks: swkotor2.exe @ 0x006387d0 (FUN_006387d0), @ 0x00682b40 (FUN_00682b40), swkotor2_aspyr.exe @ 0x0073f750 (FUN_0073f750), @ 0x007d21e0 (FUN_007d21e0)
        /// - Module directory setup: swkotor2_aspyr.exe @ 0x005564b0 (FUN_005564b0 adds module directories), @ 0x00556590 (FUN_00556590 removes module directories)
        ///
        /// KEY EXECUTION FLOW FUNCTIONS (New Game Handler):
        /// - Session time reset: swkotor.exe @ CSWPartyTable::ResetCurrentSessionStartTim() @ 0x00563cf0 (calls GetSystemTimeAsFileTime, stores to PTR__g_nCurrentSessionStartFILETIME_007a3a20 @ 0x007a3a20 and DAT_007a3a24 @ 0x007a3a24). swkotor2.exe @ FUN_0057a400 @ 0x0057a400 (calls GetSystemTimeAsFileTime, stores to DAT_00828400 @ 0x00828400 and DAT_00828404 @ 0x00828404). swkotor2_aspyr.exe @ FUN_005ff000 @ 0x005ff000 (equivalent game time initialization)
        /// - Module loading system init: swkotor.exe: Not used (no explicit module loading system initialization in New Game handler). swkotor2.exe @ FUN_00401380 @ 0x00401380 (initializes module loading system, parameter DAT_008283d4 @ 0x008283d4). swkotor2_aspyr.exe @ FUN_00401bc0 @ 0x00401bc0 (initializes module loading system, parameter DAT_00a1b4a4 @ 0x00a1b4a4)
        /// - Resource directory add: swkotor.exe @ CExoResMan::AddResourceDirectory() @ 0x00408800 (ExoResMan @ 0x007a39e8, calls AddKeyTable with DIRECTORY parameter 2). swkotor2.exe @ FUN_00408a30 @ 0x00408a30 (DAT_008283c0 @ 0x008283c0, calls FUN_00406ef0 with DIRECTORY parameter 2). swkotor2_aspyr.exe @ FUN_00711690 @ 0x00711690 (DAT_00a1b490 @ 0x00a1b490, calls FUN_00711750 with DIRECTORY parameter 2)
        /// - Resource existence check: swkotor.exe @ CExoResMan::Exists() @ 0x00408bc0 (ExoResMan @ 0x007a39e8, checks MOD then RIM types, calls GetKeyEntry internally). swkotor2.exe @ FUN_00408df0 @ 0x00408df0 (DAT_008283c0 @ 0x008283c0, checks MOD type 0x7db then RIM type 0xbba). swkotor2_aspyr.exe @ FUN_00711ed0 @ 0x00711ed0 (DAT_00a1b490 @ 0x00a1b490, checks MOD type 0x7db then RIM type 0xbba)
        /// - Resource directory remove: swkotor.exe @ CExoResMan::RemoveResourceDirectory() @ 0x004088d0 (ExoResMan @ 0x007a39e8, calls RemoveKeyTable with DIRECTORY parameter 2). swkotor2.exe @ FUN_00408b00 @ 0x00408b00 (DAT_008283c0 @ 0x008283c0, calls FUN_00407900 with DIRECTORY parameter 2). swkotor2_aspyr.exe @ FUN_00711710 @ 0x00711710 (DAT_00a1b490 @ 0x00a1b490, calls FUN_007131d0 with DIRECTORY parameter 2)
        /// - GUI panel constructor: swkotor.exe @ CSWGuiClassSelection::CSWGuiClassSelection() @ 0x006dc3c0 (allocates 0x1560 bytes via operator_new @ 0x006fa7e6, parameters: allocated memory, panel manager, module name CExoString). swkotor2.exe @ FUN_0074a700 @ 0x0074a700 (allocates 0x15f0 bytes via operator_new @ 0x0076d9f6, parameters: allocated memory, panel manager, module name CExoString). swkotor2_aspyr.exe @ FUN_008f92b0 @ 0x008f92b0 (allocates 0x15f0 bytes via FUN_00919723 @ 0x00919723, parameters: allocated memory, panel manager, module name CExoString)
        /// - Panel registration: swkotor.exe @ CSWGuiManager::AddPanel() @ 0x0040bc70 (panel manager, panel pointer, flag 2, flag 1). swkotor2.exe @ FUN_0040bf90 @ 0x0040bf90 (panel manager, panel pointer, flag 2, flag 1). swkotor2_aspyr.exe @ FUN_00410530 @ 0x00410530 (panel manager, panel pointer, flag 2, flag 1)
        /// - Sound mode set: swkotor.exe @ CExoSoundInternal::SetSoundMode() @ 0x005d5e80 (ExoSound @ 0x007a39ec, DAT_0074c5ec @ 0x0074c5ec). swkotor2.exe @ FUN_00621ab0 @ 0x00621ab0 (DAT_008283c4 @ 0x008283c4, DAT_007c5474 @ 0x007c5474, parameter 0). swkotor2_aspyr.exe @ FUN_0070bc60 @ 0x0070bc60 (DAT_00a1b494 @ 0x00a1b494, DAT_0099c2a8 @ 0x0099c2a8, parameter 0)
        /// - Unknown function calls: swkotor.exe: Not used. swkotor2.exe @ FUN_006394b0 @ 0x006394b0 (parameter: *(int *)(DAT_008283d4 + 4)). swkotor2_aspyr.exe @ FUN_00741360 @ 0x00741360 (parameter: *(int *)(DAT_00a1b4a4 + 4))
        /// - Post-cleanup checks: swkotor.exe: Not used. swkotor2.exe @ FUN_006387d0 @ 0x006387d0 (returns *(undefined4 *)(*(int *)(DAT_008283d4 + 4) + 0x40)), @ FUN_00682b40 @ 0x00682b40 (clears memory at offset 0xf88, 33 bytes). swkotor2_aspyr.exe @ FUN_0073f750 @ 0x0073f750 (returns *(undefined4 *)(*(int *)(DAT_00a1b4a4 + 4) + 0x40)), @ FUN_007d21e0 @ 0x007d21e0 (clears memory at offset 0xf88, 33 bytes)
        ///
        /// DATA STRUCTURES AND MEMORY LAYOUTS:
        /// - CExoString structure: swkotor.exe/swkotor2.exe/swkotor2_aspyr.exe: Contains c_string pointer (char*) and length field (size_t), used for module names and resource directory paths
        /// - CResRef structure: swkotor.exe/swkotor2.exe/swkotor2_aspyr.exe: Resource reference structure (16 bytes), constructed from CExoString for resource existence checks
        /// - CSWGuiClassSelection structure: swkotor.exe: Allocated size 0x1560 (5472 bytes) @ operator_new @ 0x006fa7e6. swkotor2.exe: Allocated size 0x15f0 (5616 bytes) @ operator_new @ 0x0076d9f6. swkotor2_aspyr.exe: Allocated size 0x15f0 (5616 bytes) @ FUN_00919723 @ 0x00919723
        /// - Exception handling structures: swkotor.exe: FrameHandler_0072e2f3 @ 0x0072e2f3, ExceptionList saved/restored. swkotor2.exe: LAB_007a3adb @ 0x007a3adb, ExceptionList saved/restored. swkotor2_aspyr.exe: LAB_00974a8b @ 0x00974a8b, ExceptionList saved/restored
        /// - Panel state structure offsets: swkotor.exe: panel.bit_flags checked at offset 0x0, field20_0x140c at offset 0x140c. swkotor2.exe: bit_flags at offset 0x48, field at offset 0x18f4. swkotor2_aspyr.exe: bit_flags at offset 0x48, field at offset 0x1c98
        /// - FILETIME storage: swkotor.exe: PTR__g_nCurrentSessionStartFILETIME_007a3a20 @ 0x007a3a20 (dwLowDateTime), DAT_007a3a24 @ 0x007a3a24 (dwHighDateTime). swkotor2.exe: DAT_00828400 @ 0x00828400 (dwLowDateTime), DAT_00828404 @ 0x00828404 (dwHighDateTime). swkotor2_aspyr.exe: Equivalent FILETIME storage locations (addresses not explicitly documented in execution flow)
        ///
        /// PANEL FLAG CONSTANTS:
        /// - 0x600 (1536): Bit mask for panel state flags (swkotor.exe @ 0x0067afce)
        /// - 0x400 (1024): Panel active/visible flag (swkotor.exe @ 0x0067afd3, 0x0067b14f)
        /// - 0x300 (768): Bit mask for panel state flags (swkotor2.exe @ 0x006d0b1a, swkotor2_aspyr.exe @ 0x0088224a)
        /// - 0x200 (512): Panel active/visible flag (swkotor2.exe @ 0x006d0b1a, 0x006d0d0a, swkotor2_aspyr.exe @ 0x0088224a, 0x008822d8)
        ///
        /// EXECUTION FLOW - swkotor.exe @ 0x0067afb0 (CSWGuiMainMenu::OnNewGamePicked):
        /// 1. Structured exception handling setup:
        ///    - FrameHandler_0072e2f3 @ 0x0072e2f3 stored in pcStack_8
        ///    - ExceptionList saved to local_c
        ///    - local_4 initialized to 0xffffffff (exception state tracking)
        /// 2. Panel state validation:
        ///    - Check (this->panel).bit_flags &amp; 0x600 != 0x400 (panel not already in active state)
        ///    - Check this->field20_0x140c != 0 (panel field validation)
        ///    - Check param_1->is_active != 0 (control is active)
        /// 3. Exception handler activation: ExceptionList = &amp;local_c
        /// 4. Session time reset: CSWPartyTable::ResetCurrentSessionStartTim() @ 0x00563cf0
        ///    - Calls GetSystemTimeAsFileTime() to capture current system time
        ///    - Stores FILETIME.dwLowDateTime to PTR__g_nCurrentSessionStartFILETIME_007a3a20 @ 0x007a3a20
        ///    - Stores FILETIME.dwHighDateTime to DAT_007a3a24 @ 0x007a3a24
        /// 5. INI initialization: CExoIni::CExoIni() @ 0x005e6750
        ///    - Constructed in param_1 stack location (temporary object)
        ///    - local_4 set to 0 (exception state tracking)
        /// 6. String construction:
        ///    - CExoString::CExoString() @ 0x005b3190 creates empty string in local_14
        ///    - local_4._0_1_ set to 1 (exception state tracking)
        ///    - CExoString::CExoString() @ 0x005e5a90 creates string from "END_M01AA" @ 0x00752f58 in local_2c
        ///    - local_4._0_1_ set to 2 (exception state tracking)
        /// 7. Resource directory management:
        ///    - CExoString::CExoString() @ 0x005e5a90 creates string from "MODULES:" @ 0x0073d90c in local_34
        ///    - local_4._0_1_ set to 3 (exception state tracking)
        ///    - CExoResMan::AddResourceDirectory() @ 0x00408800 (ExoResMan @ 0x007a39e8, &amp;local_34)
        ///      - Calls AddKeyTable(this, param_1, DIRECTORY, 0) internally
        ///    - local_4 = CONCAT31(local_4._1_3_, 2) (exception state tracking)
        ///    - CExoString::~CExoString() @ 0x005e5c20 destroys local_34
        /// 8. Module resource existence check:
        ///    - CResRef::CResRef() @ 0x00406d60 constructs CResRef from local_2c in local_24
        ///    - CExoResMan::Exists() @ 0x00408bc0 (ExoResMan, &amp;local_24, MOD, null)
        ///      - Calls GetKeyEntry(this, param_1, param_2, &amp;local_4, &amp;param_2) internally
        ///      - Returns non-zero if resource exists
        ///    - If MOD not found (iVar1 == 0):
        ///      - CResRef::CResRef() @ 0x00406d60 reconstructs CResRef from local_2c
        ///      - CExoResMan::Exists() @ 0x00408bc0 (ExoResMan, &amp;local_24, RIM, null)
        ///      - If RIM also not found: CExoString::operator=() @ 0x005e5140 reassigns "END_M01AA" to local_2c
        /// 9. Resource directory cleanup:
        ///    - CExoString::CExoString() @ 0x005e5a90 creates string from "MODULES:" in local_24 (reusing CResRef location)
        ///    - local_4._0_1_ set to 4 (exception state tracking)
        ///    - CExoResMan::RemoveResourceDirectory() @ 0x004088d0 (ExoResMan, &amp;local_24)
        ///    - local_4._0_1_ set to 2 (exception state tracking)
        ///    - CExoString::~CExoString() @ 0x005e5c20 destroys local_24
        /// 10. GUI panel allocation and creation:
        ///     - operator_new() @ 0x006fa7e6 allocates 0x1560 (5472) bytes, stored in local_34.c_string
        ///     - local_4._0_1_ set to 5 (exception state tracking)
        ///     - If allocation succeeds (local_34.c_string != null):
        ///       - CSWGuiClassSelection::CSWGuiClassSelection() @ 0x006dc3c0
        ///         - Parameters: allocated memory, (this->panel).manager, &amp;local_2c (module name)
        ///         - Initializes class selection GUI panel with starting module name
        ///       - panel set to constructor return value
        ///     - Else: panel set to null
        /// 11. Panel registration: CSWGuiManager::AddPanel() @ 0x0040bc70
        ///     - Parameters: (this->panel).manager, panel, 2, 1
        ///     - If panel != null: plays GUI sound, updates panel bit flags, adds to panel list, calls OnPanelAdded
        ///     - local_4._0_1_ set to 2 (exception state tracking)
        /// 12. Sound mode: CExoSoundInternal::SetSoundMode() @ 0x005d5e80
        ///     - Parameters: ExoSound @ 0x007a39ec, DAT_0074c5ec @ 0x0074c5ec
        ///     - Manages sound system state transitions (menu mode, pause states, etc.)
        /// 13. Panel flags update: (this->panel).bit_flags = (this->panel).bit_flags &amp; 0xfffffcff | 0x400
        ///     - Clears bits 0x300, sets bit 0x400 (panel active flag)
        /// 14. Cleanup:
        ///     - local_4._0_1_ set to 1 (exception state tracking)
        ///     - CExoString::~CExoString() @ 0x005e5c20 destroys local_2c
        ///     - local_4 = (uint)local_4._1_3_ &lt;&lt; 8 (exception state tracking)
        ///     - CExoString::~CExoString() @ 0x005e5c20 destroys local_14
        ///     - local_4 = 0xffffffff (exception state tracking)
        ///     - CExoIni::~CExoIni() @ 0x005e67e0 destroys temporary INI object
        /// 15. Exception handler restoration: ExceptionList = local_c
        ///
        /// EXECUTION FLOW - swkotor2.exe @ 0x006d0b00 (OnNewGameButtonClicked):
        /// 1. Structured exception handling setup:
        ///    - LAB_007a3adb @ 0x007a3adb stored in puStack_10
        ///    - ExceptionList saved to local_14
        ///    - local_c initialized to 0xffffffff (exception state tracking)
        /// 2. Panel state validation:
        ///    - Check *(uint *)((int)this + 0x48) &amp; 0x300 != 0x200 (panel not already in active state)
        ///    - Check *(int *)((int)this + 0x18f4) != 0 (panel field validation)
        ///    - Check *(int *)(param_1 + 0x50) != 0 (control is active)
        /// 3. Exception handler activation: ExceptionList = &amp;local_14
        /// 4. Game time initialization: FUN_0057a400() @ 0x0057a400
        ///    - Calls GetSystemTimeAsFileTime() to capture current system time
        ///    - Stores FILETIME.dwLowDateTime to DAT_00828400 @ 0x00828400
        ///    - Stores FILETIME.dwHighDateTime to DAT_00828404 @ 0x00828404
        /// 5. Module loading system initialization: FUN_00401380() @ 0x00401380
        ///    - Parameter: DAT_008283d4 @ 0x008283d4 (CAppManager*)
        ///    - If existing server exists (*(int *)(param_1 + 8) != 0):
        ///      - Calls FUN_00638c70() to stop services
        ///      - Calls FUN_004dc1c0() to cleanup existing server
        ///      - Frees existing server memory
        ///      - Calls FUN_00401440() for cleanup
        ///    - Allocates new server via operator_new(8)
        ///    - Calls FUN_004dc4c0() to construct CServerExoApp
        ///    - Stores in *(undefined4 **)(param_1 + 8)
        ///    - Calls FUN_004dc1b0() and FUN_004dc110() to initialize server
        /// 6. Context initialization: FUN_00631f70() @ 0x00631f70
        ///    - Parameter: &amp;local_44
        ///    - Allocates 0xc (12) bytes via operator_new
        ///    - Calls FUN_00635e30() to initialize context object
        ///    - Stores pointer in local_44
        /// 7. String construction:
        ///    - local_c set to 0 (exception state tracking)
        ///    - CExoString__CExoString_empty() creates empty string in local_20
        ///    - local_c._0_1_ set to 1 (exception state tracking)
        ///    - CExoString__CExoString() @ 0x00630a90 creates string from "001ebo" @ 0x007cc028 in local_38
        ///    - local_c._0_1_ set to 2 (exception state tracking)
        /// 8. Effects resource directory loading:
        ///    - CExoString__CExoString() @ 0x00630a90 creates string from "HD0:effects" @ 0x007cc01c in local_40
        ///    - local_c._0_1_ set to 3 (exception state tracking)
        ///    - FUN_004087d0() @ 0x004087d0 (local_30, local_40) adds effects directory
        ///    - CExoString___CExoString() destroys local_30
        ///    - local_c._0_1_ set to 2 (exception state tracking)
        ///    - CExoString___CExoString() destroys local_40
        /// 9. MODULES resource directory management:
        ///    - CExoString__CExoString() @ 0x00630a90 creates string from "MODULES:" in local_40
        ///    - local_c._0_1_ set to 4 (exception state tracking)
        ///    - FUN_00408a30() @ 0x00408a30 (DAT_008283c0 @ 0x008283c0, local_40) adds MODULES directory
        ///    - local_c = CONCAT31(local_c._1_3_, 2) (exception state tracking)
        ///    - CExoString___CExoString() destroys local_40
        /// 10. Module resource existence check:
        ///     - FUN_00406e70() @ 0x00406e70 (local_30, local_38) converts CExoString to CResRef
        ///     - FUN_00408df0() @ 0x00408df0 (DAT_008283c0, local_30, 0x7db, null) checks for MOD type
        ///       - Calls CExoResMan__GetKeyEntry() internally
        ///       - 0x7db = MOD resource type constant
        ///     - If MOD not found (iVar1 == 0):
        ///       - FUN_00406e70() reconstructs CResRef from local_38
        ///       - FUN_00408df0() (DAT_008283c0, local_30, 0xbba, null) checks for RIM type
        ///         - 0xbba = RIM resource type constant
        ///       - If RIM also not found: FUN_00630d10() @ 0x00630d10 (local_38, "001ebo") reassigns default module
        /// 11. Resource directory cleanup:
        ///     - CExoString__CExoString() creates string from "MODULES:" in local_30
        ///     - local_c._0_1_ set to 5 (exception state tracking)
        ///     - FUN_00408b00() @ 0x00408b00 (DAT_008283c0, local_30) removes MODULES directory
        ///     - local_c._0_1_ set to 2 (exception state tracking)
        ///     - CExoString___CExoString() destroys local_30
        /// 12. Module loader allocation and creation:
        ///     - operator_new() @ 0x0076d9f6 allocates 0x15f0 (5616) bytes, stored in local_40[0]
        ///     - local_c._0_1_ set to 6 (exception state tracking)
        ///     - If allocation succeeds (local_40[0] != null):
        ///       - FUN_0074a700() @ 0x0074a700 (local_40[0], *(undefined4 *)((int)this + 0x1c), local_38)
        ///         - Creates and initializes module loader with module name "001ebo"
        ///         - Takes GUI manager from *(undefined4 *)((int)this + 0x1c)
        ///       - piVar2 set to return value
        ///     - Else: piVar2 set to null
        /// 13. Panel registration: FUN_0040bf90() @ 0x0040bf90
        ///     - Parameters: *(void **)((int)this + 0x1c) (GUI manager), piVar2 (panel), 2, 1
        ///     - local_c._0_1_ set to 2 (exception state tracking)
        /// 14. Sound/music initialization: FUN_00621ab0() @ 0x00621ab0
        ///     - Parameters: DAT_008283c4 @ 0x008283c4, DAT_007c5474 @ 0x007c5474, 0
        ///     - Calls FUN_00624380() internally if first parameter is not null
        /// 15. Panel flags update: *(uint *)((int)this + 0x48) = *(uint *)((int)this + 0x48) &amp; 0xfffffe7f | 0x200
        ///     - Clears bit 0x80, sets bit 0x200 (panel active flag)
        /// 16. Server state reset: FUN_006394b0() @ 0x006394b0
        ///     - Parameter: *(int *)(DAT_008283d4 + 4) (server pointer)
        ///     - Sets *(undefined4 *)(*(int *)(param_1 + 4) + 0x280) = 0
        /// 17. Cleanup:
        ///     - local_c._0_1_ set to 1 (exception state tracking)
        ///     - CExoString___CExoString() destroys local_38
        ///     - local_c = (uint)local_c._1_3_ &lt;&lt; 8 (exception state tracking)
        ///     - CExoString___CExoString() destroys local_20
        ///     - local_c = 0xffffffff (exception state tracking)
        ///     - FUN_00632000() @ 0x00632000 destroys context in local_44
        /// 18. Post-processing check:
        ///     - FUN_006387d0() @ 0x006387d0 (*(int *)(DAT_008283d4 + 4)) checks server state
        ///     - If result != 0: calls FUN_00682b40() @ 0x00682b40 with server pointer
        /// 19. Exception handler restoration: ExceptionList = local_14
        ///
        /// EXECUTION FLOW - swkotor2_aspyr.exe @ 0x00882230 (FUN_00882230):
        /// 1. Structured exception handling setup:
        ///    - LAB_00974a8b @ 0x00974a8b stored in puStack_c
        ///    - ExceptionList saved to local_10
        ///    - local_8 initialized to 0xffffffff (exception state tracking)
        /// 2. Panel state validation:
        ///    - Check *(uint *)((int)this + 0x48) &gt;&gt; 8 &amp; 3 != 2 (panel not already in active state)
        ///    - Check *(int *)((int)this + 0x1c98) != 0 (panel field validation)
        ///    - Check *(int *)(param_1 + 0x50) != 0 (control is active)
        /// 3. Exception handler activation: ExceptionList = &amp;local_10
        /// 4. Game time initialization: FUN_005ff000() @ 0x005ff000
        ///    - Equivalent to FUN_0057a400() in swkotor2.exe
        ///    - Calls GetSystemTimeAsFileTime() to capture current system time
        /// 5. Module loading system initialization: FUN_00401bc0() @ 0x00401bc0
        ///    - Parameter: DAT_00a1b4a4 @ 0x00a1b4a4 (CAppManager*)
        ///    - Equivalent to FUN_00401380() in swkotor2.exe
        /// 6. Context initialization: FUN_00736240() @ 0x00736240
        ///    - Parameter: &amp;local_28
        ///    - Equivalent to FUN_00631f70() in swkotor2.exe
        /// 7. String construction:
        ///    - local_8 set to 0 (exception state tracking)
        ///    - FUN_00733540() creates empty string in local_20
        ///    - local_8._0_1_ set to 1 (exception state tracking)
        ///    - FUN_00733570() @ 0x00733570 creates string from "001ebo" @ 0x009a5ab0 in local_18
        ///    - local_8._0_1_ set to 2 (exception state tracking)
        /// 8. Effects resource directory loading:
        ///    - FUN_00733570() creates string from "HD0:effects" in local_30
        ///    - local_8._0_1_ set to 3 (exception state tracking)
        ///    - FUN_00716da0() @ 0x00716da0 (local_38, local_30) adds effects directory
        ///    - FUN_00733780() destroys local_38
        ///    - local_8._0_1_ set to 2 (exception state tracking)
        ///    - FUN_00733780() destroys local_30
        /// 9. MODULES resource directory management:
        ///    - FUN_00733570() creates string from "MODULES:" in local_40
        ///    - local_8._0_1_ set to 4 (exception state tracking)
        ///    - FUN_00711690() @ 0x00711690 (DAT_00a1b490 @ 0x00a1b490, local_40) adds MODULES directory
        ///    - local_8 = CONCAT31(local_8._1_3_, 2) (exception state tracking)
        ///    - FUN_00733780() destroys local_40
        /// 10. Module resource existence check:
        ///     - FUN_005564b0() performs initialization
        ///     - FUN_00710810() @ 0x00710810 (local_50, local_18) converts CExoString to CResRef
        ///     - FUN_00711ed0() @ 0x00711ed0 (DAT_00a1b490, local_50, 0x7db, null) checks for MOD type
        ///       - 0x7db = MOD resource type constant
        ///     - If MOD not found (iVar1 == 0):
        ///       - FUN_00710810() reconstructs CResRef from local_18 in local_60
        ///       - FUN_00711ed0() (DAT_00a1b490, local_60, 0xbba, null) checks for RIM type
        ///         - 0xbba = RIM resource type constant
        ///       - If RIM also not found: FUN_007338d0() @ 0x007338d0 (local_18, "001ebo") reassigns default module
        /// 11. Resource directory cleanup:
        ///     - FUN_00733570() creates string from "MODULES:" in local_68
        ///     - local_8._0_1_ set to 5 (exception state tracking)
        ///     - FUN_00711710() @ 0x00711710 (DAT_00a1b490, local_68) removes MODULES directory
        ///     - local_8._0_1_ set to 2 (exception state tracking)
        ///     - FUN_00733780() destroys local_68
        /// 12. Module loader allocation and creation:
        ///     - FUN_00556590() performs cleanup
        ///     - FUN_00919723() allocates 0x15f0 (5616) bytes, stored in this_00
        ///     - local_8._0_1_ set to 6 (exception state tracking)
        ///     - If allocation succeeds (this_00 != null):
        ///       - FUN_008f92b0() @ 0x008f92b0 (this_00, *(undefined4 *)((int)this + 0x1c), local_18)
        ///         - Equivalent to FUN_0074a700() in swkotor2.exe
        ///         - Creates and initializes module loader with module name "001ebo"
        ///       - local_88 set to return value
        ///     - Else: local_88 set to null
        /// 13. Panel registration: FUN_00410530() @ 0x00410530
        ///     - Parameters: *(void **)((int)this + 0x1c) (GUI manager), local_88 (panel), 2, 1
        ///     - local_8._0_1_ set to 2 (exception state tracking)
        /// 14. Sound/music initialization: FUN_0070bc60() @ 0x0070bc60
        ///     - Parameters: DAT_00a1b494 @ 0x00a1b494, DAT_0099c2a8 @ 0x0099c2a8, 0
        ///     - Equivalent to FUN_00621ab0() in swkotor2.exe
        /// 15. Panel flags update:
        ///     - *(uint *)((int)this + 0x48) = *(uint *)((int)this + 0x48) &amp; 0xffffff7f (clears bit 0x80)
        ///     - *(uint *)((int)this + 0x48) = *(uint *)((int)this + 0x48) &amp; 0xfffffcff | 0x200 (sets bit 0x200)
        /// 16. Server state reset: FUN_00741360() @ 0x00741360
        ///     - Parameter: *(int *)(DAT_00a1b4a4 + 4) (server pointer)
        ///     - Equivalent to FUN_006394b0() in swkotor2.exe
        /// 17. Cleanup:
        ///     - local_8._0_1_ set to 1 (exception state tracking)
        ///     - FUN_00733780() destroys local_18
        ///     - local_8 = (uint)local_8._1_3_ &lt;&lt; 8 (exception state tracking)
        ///     - FUN_00733780() destroys local_20
        ///     - local_8 = 0xffffffff (exception state tracking)
        ///     - FUN_007362c0() @ 0x007362c0 destroys context in local_28
        /// 18. Post-processing check:
        ///     - FUN_0073f750() @ 0x0073f750 (*(int *)(DAT_00a1b4a4 + 4)) checks server state
        ///     - If result != 0: calls FUN_007d21e0() @ 0x007d21e0 with server pointer
        /// 19. Exception handler restoration: ExceptionList = local_10
        ///
        /// KEY FUNCTION DETAILS:
        /// - CSWPartyTable::ResetCurrentSessionStartTim() @ 0x00563cf0 (swkotor.exe):
        ///   Captures system time via GetSystemTimeAsFileTime() and stores in global session start time variables.
        /// - FUN_0057a400() @ 0x0057a400 (swkotor2.exe):
        ///   Captures system time via GetSystemTimeAsFileTime() and stores in DAT_00828400/DAT_00828404.
        /// - FUN_005ff000() @ 0x005ff000 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_0057a400(), captures system time for game session tracking.
        /// - CAppManager::CreateServer() @ 0x00401380 (swkotor.exe):
        ///   Creates and initializes CServerExoApp instance, calls StartServices() and Initialize().
        /// - FUN_00401380() @ 0x00401380 (swkotor2.exe):
        ///   Creates and initializes server instance, equivalent functionality to CAppManager::CreateServer().
        /// - FUN_00401bc0() @ 0x00401bc0 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_00401380(), creates and initializes server instance.
        /// - CExoResMan::AddResourceDirectory() @ 0x00408800 (swkotor.exe):
        ///   Adds resource directory to resource manager via AddKeyTable(this, param_1, DIRECTORY, 0).
        /// - FUN_00408a30() @ 0x00408a30 (swkotor2.exe):
        ///   Equivalent to CExoResMan::AddResourceDirectory(), adds MODULES directory to resource manager.
        /// - FUN_00711690() @ 0x00711690 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_00408a30(), adds MODULES directory to resource manager.
        /// - CExoResMan::Exists() @ 0x00408bc0 (swkotor.exe):
        ///   Checks if resource exists via GetKeyEntry(), returns non-zero if found.
        /// - FUN_00408df0() @ 0x00408df0 (swkotor2.exe):
        ///   Checks if resource exists via CExoResMan__GetKeyEntry(), takes resource type constant (0x7db for MOD, 0xbba for RIM).
        /// - FUN_00711ed0() @ 0x00711ed0 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_00408df0(), checks resource existence with type constants.
        /// - CSWGuiClassSelection::CSWGuiClassSelection() @ 0x006dc3c0 (swkotor.exe):
        ///   Constructs class selection GUI panel with starting module name, allocates 0x1560 bytes.
        /// - FUN_0074a700() @ 0x0074a700 (swkotor2.exe):
        ///   Creates and initializes module loader, allocates 0x15f0 bytes, takes GUI manager and module name.
        /// - FUN_008f92b0() @ 0x008f92b0 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_0074a700(), creates module loader with 0x15f0 byte allocation.
        /// - CSWGuiManager::AddPanel() @ 0x0040bc70 (swkotor.exe):
        ///   Adds panel to GUI manager, plays sound if requested, updates panel flags, calls OnPanelAdded callback.
        /// - FUN_0040bf90() @ 0x0040bf90 (swkotor2.exe):
        ///   Equivalent to CSWGuiManager::AddPanel(), adds panel to GUI manager.
        /// - FUN_00410530() @ 0x00410530 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_0040bf90(), adds panel to GUI manager.
        /// - CExoSoundInternal::SetSoundMode() @ 0x005d5e80 (swkotor.exe):
        ///   Manages sound system state transitions, handles pause/resume, mute/unmute operations.
        /// - FUN_00621ab0() @ 0x00621ab0 (swkotor2.exe):
        ///   Calls FUN_00624380() if first parameter is not null, handles sound/music initialization.
        /// - FUN_0070bc60() @ 0x0070bc60 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_00621ab0(), handles sound/music initialization.
        /// - FUN_00630d10() @ 0x00630d10 (swkotor2.exe):
        ///   CExoString assignment operator, reassigns string value, handles memory allocation if needed.
        /// - FUN_007338d0() @ 0x007338d0 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_00630d10(), reassigns string value.
        /// - FUN_006394b0() @ 0x006394b0 (swkotor2.exe):
        ///   Resets server state field at offset 0x280 to 0.
        /// - FUN_00741360() @ 0x00741360 (swkotor2_aspyr.exe):
        ///   Equivalent to FUN_006394b0(), resets server state.
        ///
        /// STRUCTURE OFFSETS:
        /// - CSWGuiMainMenu panel offset: this->panel (swkotor.exe)
        /// - Panel bit_flags offset: (this->panel).bit_flags (swkotor.exe), *(uint *)((int)this + 0x48) (swkotor2.exe, swkotor2_aspyr.exe)
        /// - Panel manager offset: (this->panel).manager (swkotor.exe), *(undefined4 *)((int)this + 0x1c) (swkotor2.exe, swkotor2_aspyr.exe)
        /// - Panel field validation: this->field20_0x140c (swkotor.exe), *(int *)((int)this + 0x18f4) (swkotor2.exe), *(int *)((int)this + 0x1c98) (swkotor2_aspyr.exe)
        /// - Control is_active offset: param_1->is_active (swkotor.exe), *(int *)(param_1 + 0x50) (swkotor2.exe, swkotor2_aspyr.exe)
        ///
        /// MEMORY ALLOCATION SIZES:
        /// - CSWGuiClassSelection: 0x1560 (5472) bytes (swkotor.exe)
        /// - Module loader: 0x15f0 (5616) bytes (swkotor2.exe, swkotor2_aspyr.exe)
        ///
        /// EXECUTABLE-SPECIFIC DIFFERENCES:
        /// - swkotor.exe uses "END_M01AA" as default starting module, swkotor2.exe and swkotor2_aspyr.exe use "001ebo"
        /// - swkotor.exe creates CSWGuiClassSelection panel, swkotor2.exe and swkotor2_aspyr.exe create module loader directly
        /// - swkotor.exe includes INI initialization step, swkotor2.exe and swkotor2_aspyr.exe do not
        /// - swkotor.exe includes explicit sound mode setting, swkotor2.exe and swkotor2_aspyr.exe use different sound initialization
        /// - swkotor2.exe and swkotor2_aspyr.exe include effects resource directory loading step, swkotor.exe does not
        /// - Panel state validation bit masks differ: 0x600/0x400 (swkotor.exe) vs 0x300/0x200 (swkotor2.exe, swkotor2_aspyr.exe)
        /// - swkotor2_aspyr.exe uses different function names but equivalent functionality to swkotor2.exe
        /// - swkotor2_aspyr.exe panel state check uses bit shift: *(uint *)((int)this + 0x48) &gt;&gt; 8 &amp; 3 != 2
        /// - swkotor2_aspyr.exe panel flags update includes two-step operation: first clears 0x80, then sets 0x200
        /// 4. Module loading system initialization: FUN_00401380() @ 0x00401380 (DAT_008283d4 @ 0x008283d4)
        /// 5. Context initialization: FUN_00631f70() @ 0x00631f70 (local_44)
        /// 6. String construction:
        ///    - CExoString::CExoString_empty() @ 0x005ff130 (local_20)
        ///    - CExoString::CExoString() @ 0x00630a90 (local_38, "001ebo" @ 0x007cc028)
        ///    - CExoString::CExoString() @ 0x00630a90 (local_40, "HD0:effects" @ 0x007cc01c)
        /// 7. Effects resource directory loading:
        ///    - FUN_004087d0() @ 0x004087d0 (local_30, local_40 "HD0:effects")
        ///    - CExoString::~CExoString() @ 0x00630c20 (local_30, local_40)
        /// 8. MODULES: resource directory management:
        ///    - CExoString::CExoString() @ 0x00630a90 (local_40, "MODULES:" @ 0x007b58b4)
        ///    - FUN_00408a30() @ 0x00408a30 (DAT_008283c0 @ 0x008283c0, local_40 "MODULES:")
        ///    - CExoString::~CExoString() @ 0x00630c20 (local_40)
        /// 9. Module resource existence check with alternative codes:
        ///    - FUN_00406e70() @ 0x00406e70 (local_30, local_38 "001ebo")
        ///    - FUN_00408df0() @ 0x00408df0 (DAT_008283c0, local_30, 0x7db (2011), null)
        ///    - If 0x7db fails: FUN_00406e70() @ 0x00406e70, FUN_00408df0() @ 0x00408df0 (0xbba (3002))
        ///    - If both fail: FUN_00630d10() @ 0x00630d10 (local_38, "001ebo" @ 0x007cc028)
        /// 10. MODULES: resource directory cleanup:
        ///     - CExoString::CExoString() @ 0x00630a90 (local_30, "MODULES:" @ 0x007b58b4)
        ///     - FUN_00408b00() @ 0x00408b00 (DAT_008283c0, local_30)
        ///     - CExoString::~CExoString() @ 0x00630c20 (local_30)
        /// 11. Module object allocation and creation:
        ///     - operator_new() @ 0x0076d9f6 (0x15f0 bytes)
        ///     - If allocation succeeds: FUN_0074a700() @ 0x0074a700 (allocated memory, *(undefined4 *)((int)this + 0x1c), local_38 module name)
        /// 12. Panel registration: FUN_0040bf90() @ 0x0040bf90 (*(void **)((int)this + 0x1c), module object, 2, 1)
        /// 13. Sound system: FUN_00621ab0() @ 0x00621ab0 (DAT_008283c4 @ 0x008283c4, DAT_007c5474 @ 0x007c5474, 0)
        /// 14. Panel flags: *(uint *)((int)this + 0x48) = *(uint *)((int)this + 0x48) &amp; 0xfffffe7f | 0x200
        /// 15. Module system: FUN_006394b0() @ 0x006394b0 (*(int *)(DAT_008283d4 + 4))
        /// 16. Cleanup: CExoString destructors, FUN_00632000() @ 0x00632000 (local_44), ExceptionList restoration
        /// 17. Post-execution check: FUN_006387d0() @ 0x006387d0 (*(int *)(DAT_008283d4 + 4)), if non-zero: FUN_00682b40() @ 0x00682b40
        ///
        /// Execution Flow (swkotor2_aspyr.exe @ 0x00882230):
        /// 1. Structured exception handling setup: LAB_00974a8b @ 0x00974a8b, ExceptionList registration
        /// 2. Panel state validation: Check *(uint *)((int)this + 0x48) >> 8 &amp; 3 != 2, *(int *)((int)this + 0x1c98) != 0, *(int *)(param_1 + 0x50) != 0
        /// 3. Game time initialization: FUN_005ff000() @ 0x005ff000
        /// 4. Module loading system initialization: FUN_00401bc0() @ 0x00401bc0 (DAT_00a1b4a4 @ 0x00a1b4a4)
        /// 5. Context initialization: FUN_00736240() @ 0x00736240 (local_28)
        /// 6. String construction:
        ///    - FUN_00733540() @ 0x00733540 (local_20)
        ///    - FUN_00733570() @ 0x00733570 (local_18, "001ebo" @ 0x009a5ab0)
        ///    - FUN_00733570() @ 0x00733570 (local_30, "HD0:effects" @ 0x009a5aa4)
        /// 7. Effects resource directory loading:
        ///    - FUN_00716da0() @ 0x00716da0 (local_38, local_30 "HD0:effects")
        ///    - FUN_00733780() @ 0x00733780 (local_38, local_30)
        /// 8. MODULES: resource directory management:
        ///    - FUN_00733570() @ 0x00733570 (local_40, "MODULES:" @ 0x00993e50)
        ///    - FUN_00711690() @ 0x00711690 (DAT_00a1b490 @ 0x00a1b490, local_40 "MODULES:")
        ///    - FUN_00733780() @ 0x00733780 (local_40)
        /// 9. Module resource existence check with alternative codes:
        ///    - FUN_005564b0() @ 0x005564b0
        ///    - FUN_00710810() @ 0x00710810 (local_50, local_18 "001ebo")
        ///    - FUN_00711ed0() @ 0x00711ed0 (DAT_00a1b490, local_50, 0x7db (2011), null)
        ///    - If 0x7db fails: FUN_00710810() @ 0x00710810 (local_60, local_18), FUN_00711ed0() @ 0x00711ed0 (0xbba (3002))
        ///    - If both fail: FUN_007338d0() @ 0x007338d0 (local_18, "001ebo" @ 0x009a5ab0)
        /// 10. MODULES: resource directory cleanup:
        ///     - FUN_00733570() @ 0x00733570 (local_68, "MODULES:" @ 0x00993e50)
        ///     - FUN_00711710() @ 0x00711710 (DAT_00a1b490, local_68)
        ///     - FUN_00733780() @ 0x00733780 (local_68)
        /// 11. Module object allocation and creation:
        ///     - FUN_00556590() @ 0x00556590
        ///     - FUN_00919723() @ 0x00919723 (0x15f0 bytes)
        ///     - If allocation succeeds: FUN_008f92b0() @ 0x008f92b0 (allocated memory, *(undefined4 *)((int)this + 0x1c), local_18 module name)
        /// 12. Panel registration: FUN_00410530() @ 0x00410530 (*(void **)((int)this + 0x1c), module object, 2, 1)
        /// 13. Sound system: FUN_0070bc60() @ 0x0070bc60 (DAT_00a1b494 @ 0x00a1b494, DAT_0099c2a8 @ 0x0099c2a8, 0)
        /// 14. Panel flags: *(uint *)((int)this + 0x48) = *(uint *)((int)this + 0x48) &amp; 0xffffff7f, then | 0x200
        /// 15. Module system: FUN_00741360() @ 0x00741360 (*(int *)(DAT_00a1b4a4 + 4))
        /// 16. Cleanup: FUN_00733780() destructors, FUN_007362c0() @ 0x007362c0 (local_28), ExceptionList restoration
        /// 17. Post-execution check: FUN_0073f750() @ 0x0073f750 (*(int *)(DAT_00a1b4a4 + 4)), if non-zero: FUN_007d21e0() @ 0x007d21e0
        ///
        /// Constants:
        /// - Module resource type code 0x7db (2011): Used in all three executables for MOD resource type checks
        ///   - swkotor.exe: Referenced at 0x0067b077 within OnNewGamePicked
        ///   - swkotor2.exe: Referenced at 0x006d0c22 within OnNewGameButtonClicked
        ///   - swkotor2_aspyr.exe: Referenced at 0x00882347 within FUN_00882230
        /// - Module resource type code 0xbba (3002): Used in swkotor2.exe and swkotor2_aspyr.exe for RIM resource type checks
        ///   - swkotor2.exe: Referenced at 0x006d0c4b within OnNewGameButtonClicked
        ///   - swkotor2_aspyr.exe: Referenced at 0x0088236d within FUN_00882230
        /// - Allocation sizes: swkotor.exe uses 0x1560 bytes, swkotor2.exe and swkotor2_aspyr.exe use 0x15f0 bytes
        ///
        /// Data References:
        /// - swkotor.exe: ExoResMan @ 0x007a39e8, ExoSound @ 0x007a39ec, DAT_0074c5ec @ 0x0074c5ec
        /// - swkotor2.exe: DAT_008283c0 @ 0x008283c0, DAT_008283c4 @ 0x008283c4, DAT_008283d4 @ 0x008283d4, DAT_007c5474 @ 0x007c5474
        /// - swkotor2_aspyr.exe: DAT_00a1b490 @ 0x00a1b490, DAT_00a1b494 @ 0x00a1b494, DAT_00a1b4a4 @ 0x00a1b4a4, DAT_0099c2a8 @ 0x0099c2a8
        ///
        /// Function Call Chains:
        /// - swkotor.exe: Referenced from CSWGuiMainMenu constructor/initialization @ 0x0067c682, 0x0067c6f0
        /// - swkotor2.exe: Referenced from FUN_006d2350 (main menu handler) @ 0x006d258b, 0x006d260c
        /// - swkotor2_aspyr.exe: Referenced from FUN_00880740 (main menu handler) @ 0x00880b45, 0x00880c3d
        ///
        /// Module Selection Logic:
        /// - swkotor.exe: Default module "END_M01AA", checks MOD then RIM resource types, no alternative codes
        /// - swkotor2.exe: Default module "001ebo", checks 0x7db (MOD) then 0xbba (RIM) alternative codes, fallback to "001ebo"
        /// - swkotor2_aspyr.exe: Default module "001ebo", checks 0x7db (MOD) then 0xbba (RIM) alternative codes, fallback to "001ebo"
        /// 5. Module loads areas, entities, scripts, etc.
        ///
        /// Character Creation Flow:
        /// Main Menu -> New Game Button -> Character Creation -> Character Creation Complete ->
        /// 0x006d0b00 (New Game Handler) -> Module Initialization -> Module Load -> Player Entity Creation
        ///
        /// Module name casing: Ghidra shows "001ebo" (lowercase) and "END_M01AA" (uppercase)
        /// Resource lookup is case-insensitive, we use lowercase to match BioWare.NET conventions
        /// </remarks>
        public void StartNewGame([CanBeNull] CharacterCreationData characterData = null)
        {
            Console.WriteLine("[GameSession] Starting new game");

            // Clear world - remove all entities
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): World is cleared before loading new module
            var entities = new System.Collections.Generic.List<IEntity>(_world.GetAllEntities());
            foreach (IEntity entity in entities)
            {
                _world.DestroyEntity(entity.ObjectId);
            }

            // Store character data for player entity creation after module load
            _pendingCharacterData = characterData;

            // Determine starting module using exact logic from 0x006d0b00 (swkotor2.exe: 0x006d0b00)
            // OnNewGameButtonClicked @ (K1: TODO: Find this address, TSL: 0x006d0b00): Module selection logic
            string startingModule = DetermineStartingModule();

            // Initialize module loading system (equivalent to 0x00401380)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x00401380 @ 0x00401380: Module initialization
            InitializeModuleLoading();

            // Load HD0:effects directory (equivalent to 0x00630a90(local_40,"HD0:effects"))
            // OnNewGameButtonClicked @ (K1: TODO: Find this address, TSL: 0x006d0b00) line 31: Load effects directory
            // Note: HD0:effects is a directory alias, resource system handles this automatically

            // Load module synchronously
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x0074a700 @ 0x0074a700: Module loader/creator function
            // Original implementation: Takes module name and creates/loads the module into game world
            // Module loading is synchronous in the original engine - all resources are loaded before gameplay begins
            bool success = LoadModule(startingModule);

            if (!success)
            {
                Console.WriteLine("[GameSession] Failed to load starting module: " + startingModule);
                return;
            }

            Console.WriteLine("[GameSession] New game started in module: " + startingModule);
        }

        /// <summary>
        /// Determines the starting module name using the exact logic from 0x006d0b00 (swkotor2.exe: 0x006d0b00).
        /// </summary>
        /// <returns>The starting module name to load.</returns>
        /// <remarks>
        /// OnNewGameButtonClicked @ (K1: TODO: Find this address, TSL: 0x006d0b00) (New Game Button Handler):
        /// - Line 29: Sets default to "001ebo" (prologue module)
        /// - Lines 43-50: Checks for alternative modules with codes 0x7db and 0xbba
        /// - If alternatives don't exist, falls back to "001ebo"
        /// - K1 uses "end_m01aa" (Endar Spire) as default
        /// </remarks>
        private string DetermineStartingModule()
        {
            // Check if module is specified in settings (highest priority)
            if (!string.IsNullOrEmpty(_settings.StartModule))
            {
                Console.WriteLine("[GameSession] Using module from settings: " + _settings.StartModule);
                return _settings.StartModule;
            }

            // Default starting modules based on game version
            // OnNewGameButtonClicked @ (K1: TODO: Find this address, TSL: 0x006d0b00) line 29: "001ebo" (K2 prologue)
            // Based on swkotor.exe: "END_M01AA" (K1 Endar Spire)
            string defaultModule = _settings.Game == KotorGame.K1 ? "end_m01aa" : "001ebo";

            // Check for alternative modules with codes 0x7db (MOD) and 0xbba (RIM) (K2 only)
            // OnNewGameButtonClicked @ (K1: TODO: Find this address, TSL: 0x006d0b00) lines 43-50:
            // - 0x00408df0(DAT_008283c0,local_30,0x7db,(undefined4 *)0x0) - Check module code 0x7db (MOD resource type)
            // - If 0x7db fails: 0x00408df0(DAT_008283c0,local_30,0xbba,(undefined4 *)0x0) - Check module code 0xbba (RIM resource type)
            // - If both fail: 0x00630d10(local_38,"001ebo") - Fallback to "001ebo"
            // Resource type codes: 0x7db = 2011 = MOD (.mod file), 0xbba = 3002 = RIM (.rim file)
            // The engine checks if a resource with the module name and specified type exists in the resource manager
            // This effectively checks if a .mod or .rim file exists for the module name
            if (_settings.Game == KotorGame.K2 && _installation != null)
            {
                string moduleName = defaultModule;
                bool moduleExists = CheckModuleResourceExists(moduleName, ResourceType.MOD);
                if (!moduleExists)
                {
                    moduleExists = CheckModuleResourceExists(moduleName, ResourceType.RIM);
                }

                if (!moduleExists)
                {
                    // Module doesn't exist, use default
                    Console.WriteLine("[GameSession] Module '" + moduleName + "' not found, using default: " + defaultModule);
                    return defaultModule;
                }

                Console.WriteLine("[GameSession] Found alternative module: " + moduleName);
                return moduleName;
            }

            Console.WriteLine("[GameSession] Using default starting module: " + defaultModule);
            return defaultModule;
        }

        /// <summary>
        /// Checks if a module resource exists with the specified resource type.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x00408df0 @ 0x00408df0: Resource existence check
        /// </summary>
        /// <param name="moduleName">The module name to check.</param>
        /// <param name="resourceType">The resource type to check (MOD = 0x7db, RIM = 0xbba).</param>
        /// <returns>True if the module resource exists, false otherwise.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x00408df0 @ 0x00408df0:
        /// - Calls 0x00407300 to search for resources with the specified name and type
        /// - Returns non-zero if resource exists, zero if not found
        /// - Resource type 0x7db (MOD) checks for .mod files
        /// - Resource type 0xbba (RIM) checks for .rim files
        /// - This is used to verify if a module file exists before attempting to load it
        /// </remarks>
        private bool CheckModuleResourceExists(string moduleName, ResourceType resourceType)
        {
            if (string.IsNullOrEmpty(moduleName) || _installation == null)
            {
                return false;
            }

            try
            {
                // Check if a module file exists with the specified resource type
                // For MOD type (0x7db), check if {moduleName}.mod exists
                // For RIM type (0xbba), check if {moduleName}.rim exists
                string modulesPath = Installation.GetModulesPath(_installation.Path);
                if (!System.IO.Directory.Exists(modulesPath))
                {
                    return false;
                }

                if (resourceType == ResourceType.MOD)
                {
                    // Check for .mod file
                    string modPath = System.IO.Path.Combine(modulesPath, moduleName + ".mod");
                    if (System.IO.File.Exists(modPath))
                    {
                        return true;
                    }
                }
                else if (resourceType == ResourceType.RIM)
                {
                    // Check for .rim file (main rim, not _s.rim)
                    string rimPath = System.IO.Path.Combine(modulesPath, moduleName + ".rim");
                    if (System.IO.File.Exists(rimPath))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GameSession] Error checking module resource existence: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Initializes the module loading system (equivalent to 0x00401380 @ 0x00401380).
        /// </summary>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x00401380 @ 0x00401380 (Module Initialization):
        /// - Called early in 0x006d0b00 (line 24) to prepare game state for module loading
        /// - Sets up module loading context and prepares game systems for the new module
        /// - Original implementation: 0x00401380(DAT_008283d4) initializes module loading system
        /// - This function prepares the game state, clears previous module state, and sets up module loading context
        /// </remarks>
        private void InitializeModuleLoading()
        {
            Console.WriteLine("[GameSession] Initializing module loading system");

            // Clear any existing module state
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x00401380: Clears previous module state
            if (_currentModule != null)
            {
                _currentModule = null;
                _currentModuleName = null;
            }

            // Set module state to Idle (swkotor2.exe: 0x006caab0 @ 0x006caab0)
            // Module unloading clears state back to Idle
            _moduleStateManager?.SetModuleState(ModuleState.Idle);

            // Prepare game systems for module loading
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x00401380: Prepares game systems
            // This includes clearing entity lists, resetting world state, etc.
            // (Already done in StartNewGame by clearing world entities)

            Console.WriteLine("[GameSession] Module loading system initialized");
        }

        /// <summary>
        /// Loads a module synchronously.
        /// </summary>
        /// <param name="moduleName">The module name to load.</param>
        /// <returns>True if the module was loaded successfully, false otherwise.</returns>
        /// <remarks>
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) 0x0074a700 @ 0x0074a700: Module loader/creator function
        /// - Takes module name and creates/loads the module into game world
        /// - Original implementation: Module loading is synchronous - all resources are loaded before gameplay begins
        /// - Module loading sequence:
        ///   1. Load module resources (IFO, ARE, GIT, LYT, VIS, walkmesh)
        ///   2. Set current module state
        ///   3. Set world's current area
        ///   4. Register all entities from area into world
        ///   5. Spawn/reposition player at entry position
        /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
        /// - Module state flags: 0=Idle, 1=ModuleLoaded, 2=ModuleRunning (set in 0x006caab0 @ 0x006caab0)
        /// </remarks>
        private bool LoadModule(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            try
            {
                Console.WriteLine("[GameSession] Loading module: " + moduleName);

                // Load module
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module loading loads IFO, ARE, GIT, LYT, VIS, walkmesh resources
                // Located via string references: "MODULES:" @ 0x007b58b4, module resource loading
                // Original implementation: 0x0074a700 loads module resources synchronously
                RuntimeModule module = _moduleLoader.LoadModule(moduleName);
                if (module == null)
                {
                    Console.WriteLine("[GameSession] Module loader returned null for: " + moduleName);
                    return false;
                }

                // Set current module
                // SetModuleState @ (K1: TODO: Find address, TSL: 0x006caab0): Module state is set after successful load
                // Original implementation: Module state flags updated in DAT_008283d4 structure
                _currentModule = module;
                _currentModuleName = moduleName;
                _world.SetCurrentModule(module);
                _moduleTransitionSystem?.SetCurrentModule(moduleName);

                // Set module state to ModuleLoaded (swkotor2.exe: 0x006caab0 @ 0x006caab0)
                // Module is loaded but OnModuleStart has not been called yet
                _moduleStateManager?.SetModuleState(ModuleState.ModuleLoaded);

                // Template factory is already set up in constructor using LazyOdysseyEntityTemplateFactory
                // The lazy factory will automatically use the current module from ModuleLoader when needed
                // No need to update it here - it will retrieve the module on-demand during template creation

                // Set world's current area
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entry area is set as current area after module load
                // Original implementation: Mod_Entry_Area from IFO determines which area is loaded first
                if (!string.IsNullOrEmpty(module.EntryArea))
                {
                    IArea entryArea = module.GetArea(module.EntryArea);
                    if (entryArea != null)
                    {
                        _world.SetCurrentArea(entryArea);

                        // Register all entities from area into world
                        // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Entities must be registered in world for lookups to work
                        // Located via string references: "ObjectId" @ 0x007bce5c, "ObjectIDList" @ 0x007bfd7c
                        // Original implementation: All entities are registered in world's ObjectId, Tag, and ObjectType indices
                        // Entity registration allows GetEntity, GetEntityByTag, GetEntityByObjectType lookups to work
                        if (entryArea is RuntimeArea runtimeArea)
                        {
                            foreach (IEntity entity in runtimeArea.GetAllEntities())
                            {
                                if (entity != null && entity.IsValid)
                                {
                                    // Register entity in world (World is set during entity creation)
                                    _world.RegisterEntity(entity);

                                    // Register encounters with encounter system
                                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Encounters are registered with encounter system for spawning
                                    // Original implementation: Encounter system tracks encounter entities and spawns creatures when triggered
                                    if (_encounterSystem != null && entity.ObjectType == Runtime.Core.Enums.ObjectType.Encounter)
                                    {
                                        _encounterSystem.RegisterEncounter(entity);
                                    }
                                }
                            }
                        }
                    }
                }

                // Fire OnModuleLoad script (swkotor2.exe: Mod_OnModLoad script execution)
                // OnModuleLoad fires when module finishes loading, before player spawn
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): OnModuleLoad script execution
                // Located via string references: "Mod_OnModLoad" @ IFO file, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
                // Original implementation: OnModuleLoad script executes after module resources are loaded but before player spawn
                FireModuleScript(ScriptEvent.OnModuleLoad);

                // Spawn player at entry position if not already spawned
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Player is spawned at Mod_Entry_X/Y/Z with Mod_Entry_Dir_X/Y facing
                // Original implementation: Player entity created at module entry position after module load
                if (_playerEntity == null)
                {
                    SpawnPlayer();
                }
                else
                {
                    // Reposition existing player
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Existing player is repositioned at entry when transitioning between modules
                    // Original implementation: Player position updated to module entry position
                    PositionPlayerAtEntry();
                }

                // Fire OnModuleStart script (swkotor2.exe: Mod_OnModStart script execution)
                // OnModuleStart fires after player is spawned, before gameplay begins
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): OnModuleStart script execution
                // Located via string references: "Mod_OnModStart" @ IFO file, "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
                // Original implementation: OnModuleStart script executes after player spawn, before gameplay begins
                FireModuleScript(ScriptEvent.OnModuleStart);

                // Set module state to ModuleRunning (swkotor2.exe: 0x006caab0 @ 0x006caab0)
                // Module is fully loaded, OnModuleStart has been called, gameplay is active
                _moduleStateManager?.SetModuleState(ModuleState.ModuleRunning);

                Console.WriteLine("[GameSession] Module loaded successfully: " + moduleName);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GameSession] Error loading module " + moduleName + ": " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Loads a module asynchronously.
        /// </summary>
        /// <param name="moduleName">The module name to load.</param>
        /// <returns>Task that completes with true if the module was loaded successfully, false otherwise.</returns>
        /// <remarks>
        /// This method wraps the synchronous LoadModule method for use in async contexts (e.g., module transitions).
        /// The underlying module loading is synchronous (matching original engine behavior), but this allows
        /// callers to use async/await patterns without blocking the calling thread.
        /// </remarks>
        private Task<bool> LoadModuleAsync(string moduleName)
        {
            // Wrap synchronous LoadModule in Task.FromResult since module loading is inherently synchronous
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module loading is synchronous - all resources loaded before gameplay begins
            // This async wrapper allows ModuleTransitionSystem to use async/await patterns without blocking
            return Task.FromResult(LoadModule(moduleName));
        }

        /// <summary>
        /// Spawns the player entity at the module entry point.
        /// </summary>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe: Player entity creation
        /// - Located via string references: "Player" tag @ creature creation, player entity initialization
        /// - Original implementation: Player entity created with Tag "Player", IsPlayer flag, Faction Friendly1, Immortal flag
        /// - If character creation data is provided, player entity is created from character data with proper attributes, skills, feats, appearance, class
        /// - If no character data provided, default player entity is created (backwards compatibility)
        /// </remarks>
        private void SpawnPlayer()
        {
            if (_currentModule == null)
            {
                return;
            }

            System.Numerics.Vector3 entryPos = _currentModule.EntryPosition;
            float entryFacing = (float)Math.Atan2(_currentModule.EntryDirectionY, _currentModule.EntryDirectionX);

            // Create player entity from character data if available, otherwise create default
            if (_pendingCharacterData != null)
            {
                _playerEntity = CreatePlayerFromCharacterData(_pendingCharacterData, entryPos, entryFacing);
                _pendingCharacterData = null; // Clear pending data after use
            }
            else
            {
                // Default player entity creation (backwards compatibility)
                _playerEntity = _world.CreateEntity(Runtime.Core.Enums.ObjectType.Creature, entryPos, entryFacing);
                if (_playerEntity != null)
                {
                    _playerEntity.Tag = "Player";
                    _playerEntity.SetData("IsPlayer", true);

                    // Add player to party as leader
                    _partySystem?.SetPlayerCharacter(_playerEntity);
                }
            }
        }

        /// <summary>
        /// Creates a player entity from character creation data.
        /// </summary>
        /// <param name="characterData">Character creation data containing class, attributes, skills, appearance, etc.</param>
        /// <param name="position">World position for the player entity.</param>
        /// <param name="facing">Facing angle in radians for the player entity.</param>
        /// <returns>The created player entity, or null if creation failed.</returns>
        /// <remarks>
        /// Based on swkotor.exe and swkotor2.exe: Player entity creation from character generation
        /// - Located via string references: Character generation finish() function, player entity creation
        /// - Original implementation: 0x005261b0 @ 0x005261b0 (load creature from UTC template), character generation creates player entity
        /// - Character creation flow:
        ///   1. Create entity with Tag "Player", IsPlayer flag, Faction Friendly1, Immortal flag
        ///   2. Set appearance (Appearance_Type, Gender, PortraitId)
        ///   3. Set class (ClassList with class ID and level 1)
        ///   4. Set attributes (STR, DEX, CON, INT, WIS, CHA)
        ///   5. Set skills (based on class, INT modifier, and character creation allocation)
        ///   6. Set feats (starting feats from class and character creation selection)
        ///   7. Calculate HP from class hit dice + CON modifier
        ///   8. Calculate FP from Force-using class levels (if applicable)
        ///   9. Set name (FirstName from character creation)
        ///   10. Initialize all creature components (StatsComponent, CreatureComponent, InventoryComponent, etc.)
        ///   11. Register with party system as leader
        /// - Class IDs: Soldier=0, Scout=1, Scoundrel=2, JediGuardian=3, JediConsular=4, JediSentinel=5
        /// - Gender: Male=0, Female=1
        /// - Faction Friendly1 = 2 (standard player faction)
        /// </remarks>
        private IEntity CreatePlayerFromCharacterData(CharacterCreationData characterData, System.Numerics.Vector3 position, float facing)
        {
            if (characterData == null || _world == null)
            {
                Console.WriteLine("[GameSession] Cannot create player: characterData or world is null");
                return null;
            }

            Console.WriteLine("[GameSession] Creating player entity from character creation data");

            // Create player entity
            IEntity playerEntity = _world.CreateEntity(Runtime.Core.Enums.ObjectType.Creature, position, facing);
            if (playerEntity == null)
            {
                Console.WriteLine("[GameSession] Failed to create player entity");
                return null;
            }

            // Set basic player properties
            playerEntity.Tag = "Player";
            playerEntity.SetData("IsPlayer", true);

            // Map CharacterClass enum to class ID
            // Based on swkotor.exe and swkotor2.exe: Class IDs in classes.2da
            // CLASS_TYPE_SOLDIER=0, CLASS_TYPE_SCOUT=1, CLASS_TYPE_SCOUNDREL=2
            // CLASS_TYPE_JEDIGUARDIAN=3, CLASS_TYPE_JEDICONSULAR=4, CLASS_TYPE_JEDISENTINEL=5
            int classId;
            switch (characterData.Class)
            {
                case CharacterClass.Soldier:
                    classId = 0;
                    break;
                case CharacterClass.Scout:
                    classId = 1;
                    break;
                case CharacterClass.Scoundrel:
                    classId = 2;
                    break;
                case CharacterClass.JediGuardian:
                    classId = 3;
                    break;
                case CharacterClass.JediConsular:
                    classId = 4;
                    break;
                case CharacterClass.JediSentinel:
                    classId = 5;
                    break;
                default:
                    Console.WriteLine("[GameSession] Unknown character class, defaulting to Scout");
                    classId = 1;
                    break;
            }

            // Map Gender enum to integer (Male=0, Female=1)
            int genderValue = characterData.Gender == Runtime.Core.Game.Gender.Male ? 0 : 1;

            // Get class data for HP/FP calculations
            GameDataManager.ClassData classData = _gameDataManager?.GetClass(classId);
            int hitDie = classData?.HitDie ?? 8; // Default to d8 if class data unavailable
            bool isForceUser = classData?.ForceUser ?? false;

            // Calculate HP: Hit die + CON modifier
            int conModifier = (characterData.Constitution - 10) / 2;
            int maxHP = hitDie + conModifier;
            if (maxHP < 1)
            {
                maxHP = 1; // Minimum 1 HP
            }

            // Calculate FP: Force-using classes get FP based on class level
            // Level 1 Force-using classes typically start with base FP + WIS modifier
            int maxFP = 0;
            if (isForceUser)
            {
                // Base FP for level 1 Force user: typically 1 or base amount from class
                // KOTOR Force users start with base FP + WIS modifier
                int wisModifier = (characterData.Wisdom - 10) / 2;
                maxFP = 1 + wisModifier; // Base 1 FP for level 1 + WIS mod
                if (maxFP < 1)
                {
                    maxFP = 1; // Minimum 1 FP for Force users
                }
            }

            // Get CreatureComponent and set appearance and class data
            Components.CreatureComponent creatureComp = playerEntity.GetComponent<Components.CreatureComponent>();
            if (creatureComp != null)
            {
                // Set appearance
                creatureComp.AppearanceType = characterData.Appearance;
                creatureComp.RaceId = 0; // Default to Human (0) - can be customized later if needed
                creatureComp.PortraitId = characterData.Portrait;
                // Gender is typically stored in entity data, not CreatureComponent directly
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Gender stored in UTC template as "Gender" field (integer: 0=Male, 1=Female)

                // Set class
                creatureComp.ClassList.Clear();
                creatureComp.ClassList.Add(new Components.CreatureClass { ClassId = classId, Level = 1 });

                // Set attributes
                creatureComp.Strength = characterData.Strength;
                creatureComp.Dexterity = characterData.Dexterity;
                creatureComp.Constitution = characterData.Constitution;
                creatureComp.Intelligence = characterData.Intelligence;
                creatureComp.Wisdom = characterData.Wisdom;
                creatureComp.Charisma = characterData.Charisma;

                // Set HP/FP
                creatureComp.CurrentHP = maxHP;
                creatureComp.MaxHP = maxHP;
                creatureComp.CurrentForce = maxFP;
                creatureComp.MaxForce = maxFP;

                // Set player-specific flags
                creatureComp.FactionId = 2; // Friendly1 faction
                creatureComp.IsImmortal = true; // Player is immortal
                creatureComp.Tag = "Player";

                // Starting feats from class will be set below from GameDataManager using featgain.2da
            }

            // Set entity data for appearance, gender, name
            if (playerEntity is RuntimeCore.Entities.Entity concreteEntity)
            {
                concreteEntity.SetData("Appearance_Type", characterData.Appearance);
                concreteEntity.SetData("Gender", genderValue);
                concreteEntity.SetData("FirstName", characterData.Name ?? "Player");
                concreteEntity.SetData("RaceId", 0); // Default to Human
                concreteEntity.SetData("FactionID", 2); // Friendly1 faction
            }

            // Get StatsComponent and set attributes and skills
            Components.StatsComponent statsComp = playerEntity.GetComponent<Components.StatsComponent>();
            if (statsComp != null)
            {
                // Set ability scores
                statsComp.SetAbility(Runtime.Core.Enums.Ability.Strength, characterData.Strength);
                statsComp.SetAbility(Runtime.Core.Enums.Ability.Dexterity, characterData.Dexterity);
                statsComp.SetAbility(Runtime.Core.Enums.Ability.Constitution, characterData.Constitution);
                statsComp.SetAbility(Runtime.Core.Enums.Ability.Intelligence, characterData.Intelligence);
                statsComp.SetAbility(Runtime.Core.Enums.Ability.Wisdom, characterData.Wisdom);
                statsComp.SetAbility(Runtime.Core.Enums.Ability.Charisma, characterData.Charisma);

                // Set HP/FP
                statsComp.MaxHP = maxHP;
                statsComp.CurrentHP = maxHP;
                statsComp.MaxFP = maxFP;
                statsComp.CurrentFP = maxFP;

                // Set base level
                statsComp.Level = 1;

                // Calculate and set skills
                // KOTOR has 8 skills: COMPUTER_USE=0, DEMOLITIONS=1, STEALTH=2, AWARENESS=3, PERSUADE=4, REPAIR=5, SECURITY=6, TREAT_INJURY=7
                // Based on swkotor.exe and swkotor2.exe: Skill calculation during character creation
                // Located via string references: Skill allocation system in character creation
                // Original implementation: 0x005261b0 @ 0x005261b0 (load creature from UTC template)
                // Skill ranks = INT modifier + allocated skill points from character creation
                // - Level 1 characters start with 0 base skill ranks (no class levels yet)
                // - INT modifier applies to all skills (keyability from skills.2da)
                // - Skill points allocated during character creation are added to INT modifier
                // - Class skills vs cross-class skills affect point cost but not the final rank calculation
                // - Final skill rank = INT modifier + allocated skill points (from characterData.SkillRanks)
                int intModifier = (characterData.Intelligence - 10) / 2;

                // Get skill ranks allocated during character creation
                System.Collections.Generic.Dictionary<int, int> skillRanks = characterData.SkillRanks;
                if (skillRanks == null)
                {
                    // If no skill ranks provided, initialize empty dictionary
                    skillRanks = new System.Collections.Generic.Dictionary<int, int>();
                }

                // Set skills for all 8 KOTOR skills
                for (int skillId = 0; skillId < 8; skillId++)
                {
                    // Get allocated skill points for this skill (0 if not allocated)
                    int allocatedPoints = 0;
                    if (skillRanks.ContainsKey(skillId))
                    {
                        allocatedPoints = skillRanks[skillId];
                    }

                    // Final skill rank = INT modifier + allocated skill points
                    // Based on swkotor.exe and swkotor2.exe: Skill rank calculation
                    // Original implementation: Skill rank = ability modifier + skill ranks
                    // Level 1 characters have no class-based skill ranks yet, only INT modifier and allocated points
                    int finalSkillRank = intModifier + allocatedPoints;

                    // Ensure skill rank is non-negative (can't go below 0)
                    if (finalSkillRank < 0)
                    {
                        finalSkillRank = 0;
                    }

                    statsComp.SetSkillRank(skillId, finalSkillRank);
                }

                // Calculate BAB, saves from class
                // BAB and saves are calculated based on class progression tables
                // Level 1 BAB is typically +0 or +1 depending on class
                // Saves start at +0, +0, +0 for most classes at level 1, or +2 for good saves
                // Uses classes.2da attackbonustable and savingthrowtable
                if (classData != null && _gameDataManager != null)
                {
                    // Get BAB from attack bonus table
                    int baseAttackBonus = GetAttackBonusFromTable(classData.AttackBonusTable, 1, _gameDataManager);
                    statsComp.SetBaseAttackBonus(baseAttackBonus);

                    // Get saves from saving throw table
                    int fortSave = 0;
                    int reflexSave = 0;
                    int willSave = 0;
                    GetSavingThrowsFromTable(classData.SavingThrowTable, 1, _gameDataManager, out fortSave, out reflexSave, out willSave);
                    statsComp.SetBaseSaves(fortSave, reflexSave, willSave);
                }
                else
                {
                    // Fallback: Set default values if class data unavailable
                    statsComp.SetBaseAttackBonus(0);
                    statsComp.SetBaseSaves(0, 0, 0);
                }
            }

            // Add starting feats from class
            // Based on swkotor.exe and swkotor2.exe: Starting feats come from class featgain.2da
            // Located via string references: "CSWClass::LoadFeatGain: can't load featgain.2da" @ swkotor.exe: 0x0074b370, swkotor2.exe: 0x007c46bc
            // Original implementation:
            // - ["CSWClass::LoadFeatGain"] @ (K1: 0x005bcf70, TSL: 0x0060d1d0)
            // - 0x005d63d0 reads "FeatGain" column from classes.2da for each class, then calls 0x0060d1d0 (LoadFeatGain)
            // LoadFeatGain loads featgain.2da table, finds row by label (from FeatGain column), reads "_REG" and "_BON" columns
            // Each class has automatic feats granted at level 1 from featgain.2da
            if (_gameDataManager != null && creatureComp != null && creatureComp.FeatList != null)
            {
                List<int> startingFeats = _gameDataManager.GetStartingFeats(classId);
                foreach (int featId in startingFeats)
                {
                    if (!creatureComp.FeatList.Contains(featId))
                    {
                        creatureComp.FeatList.Add(featId);
                        Console.WriteLine("[GameSession] Added starting feat ID " + featId + " for class " + classId);
                    }
                }
            }

            // Initialize all other components (ComponentInitializer already handles this, but ensure they exist)
            ComponentInitializer.InitializeComponents(playerEntity);

            // Add player to party as leader
            _partySystem?.SetPlayerCharacter(playerEntity);

            Console.WriteLine("[GameSession] Player entity created: Name=\"" + (characterData.Name ?? "Player") + "\", Class=" + characterData.Class + ", Appearance=" + characterData.Appearance);
            return playerEntity;
        }

        /// <summary>
        /// Positions the player at the module entry point.
        /// </summary>
        private void PositionPlayerAtEntry()
        {
            if (_playerEntity == null || _currentModule == null)
            {
                return;
            }

            ITransformComponent transform = _playerEntity.GetComponent<ITransformComponent>();
            if (transform != null)
            {
                transform.Position = _currentModule.EntryPosition;
                transform.Facing = (float)Math.Atan2(_currentModule.EntryDirectionY, _currentModule.EntryDirectionX);
            }
        }

        /// <summary>
        /// Positions the player at a waypoint by tag.
        /// </summary>
        private void PositionPlayerAtWaypoint(string waypointTag)
        {
            if (_playerEntity == null || string.IsNullOrEmpty(waypointTag))
            {
                return;
            }

            // Find waypoint entity by tag
            IEntity waypoint = _world.GetEntityByTag(waypointTag);
            if (waypoint == null)
            {
                Console.WriteLine("[GameSession] Waypoint not found: " + waypointTag);
                return;
            }

            ITransformComponent waypointTransform = waypoint.GetComponent<ITransformComponent>();
            if (waypointTransform == null)
            {
                return;
            }

            // Position player at waypoint
            ITransformComponent playerTransform = _playerEntity.GetComponent<ITransformComponent>();
            if (playerTransform != null)
            {
                playerTransform.Position = waypointTransform.Position;
                playerTransform.Facing = waypointTransform.Facing;
            }
        }

        /// <summary>
        /// Handles door opened events and triggers module transitions if needed.
        /// </summary>
        private void OnDoorOpened(DoorOpenedEvent evt)
        {
            if (evt == null || evt.Door == null)
            {
                return;
            }

            // Check if door triggers a module transition
            if (_moduleTransitionSystem != null && _moduleTransitionSystem.CanDoorTransition(evt.Door))
            {
                _moduleTransitionSystem.TransitionThroughDoor(evt.Door, evt.Actor);
            }
        }

        /// <summary>
        /// Creates or retrieves the module entity for script execution.
        /// </summary>
        /// <param name="runtimeModule">The runtime module to create entity for.</param>
        /// <returns>The module entity, or null if module is invalid.</returns>
        /// <remarks>
        /// Module Entity Creation (Odyssey-specific):
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module entity created with fixed ObjectId 0x7F000002
        /// - Located via string references: Module entity ObjectId constant, GetModule() NWScript function
        /// - Cross-engine analysis:
        ///   - Aurora (nwmain.exe): Similar module entity system with fixed ObjectId
        ///   - Eclipse (daorigins.exe, DragonAge2.exe): Different module entity system (UnrealScript-based)
        /// - Inheritance: Base class BaseGameSession (Runtime.Games.Common) - abstract module entity creation, Odyssey override (Runtime.Games.Odyssey) - Odyssey-specific module entity
        /// - Original implementation: Module entity is a special system entity with fixed ObjectId (0x7F000002)
        /// - Entity Properties:
        ///   - ObjectId: Fixed at 0x7F000002 (World.ModuleObjectId)
        ///   - ObjectType: Invalid (no Module ObjectType in enum, modules are special system entities)
        ///   - Tag: Module ResRef (used for GetEntityByTag lookups)
        ///   - Components: IScriptHooksComponent (required for script execution)
        ///   - Position: Vector3.Zero (modules have no physical position)
        ///   - AreaId: 0 (modules are not area entities)
        /// - Module scripts: All module scripts (OnModuleLoad, OnModuleStart, OnClientEnter, OnClientLeave, OnModuleHeartbeat) are stored in module entity's IScriptHooksComponent
        /// - This allows GetModule() NWScript function to access module scripts via GetScript()
        /// - Module entity is registered in world for GetEntityByTag and GetEntity lookups
        /// </remarks>
        private IEntity CreateOrGetModuleEntity(RuntimeModule runtimeModule)
        {
            if (runtimeModule == null || string.IsNullOrEmpty(runtimeModule.ResRef))
            {
                return null;
            }

            // Check if module entity with fixed ObjectId already exists (canonical check)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module entity has fixed ObjectId 0x7F000002
            // Original implementation: Module entity persists across module heartbeat calls
            // Module entity is created during module load and reused for all module script execution
            IEntity existingModuleEntity = _world.GetEntity(World.ModuleObjectId);
            if (existingModuleEntity != null)
            {
                // Verify it has the correct Tag
                if (existingModuleEntity.Tag == runtimeModule.ResRef)
                {
                    return existingModuleEntity;
                }
                // If existing module entity has wrong Tag, destroy it and create a new one
                // (This shouldn't happen in normal operation, but handle defensively)
                _world.DestroyEntity(existingModuleEntity.ObjectId);
            }

            // Create new module entity with fixed ObjectId
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module entity created with ObjectId 0x7F000002
            // Entity constructor: Entity(uint objectId, ObjectType objectType)
            var entity = new Entity(World.ModuleObjectId, Runtime.Core.Enums.ObjectType.Invalid);
            entity.World = _world;
            entity.Tag = runtimeModule.ResRef;
            entity.Position = System.Numerics.Vector3.Zero;
            entity.Facing = 0f;
            entity.AreaId = 0;

            // Initialize components for module entity
            // Module entities need IScriptHooksComponent for script execution
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module scripts require script hooks component
            // ComponentInitializer.InitializeComponents adds IScriptHooksComponent to all entities
            Andastra.Game.Games.Odyssey.Systems.ComponentInitializer.InitializeComponents(entity);

            // Ensure IScriptHooksComponent is present (ComponentInitializer should add it, but verify for safety)
            if (!entity.HasComponent<IScriptHooksComponent>())
            {
                entity.AddComponent(new ScriptHooksComponent());
            }

            // Load module scripts into script hooks component
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module scripts (OnModuleLoad, OnModuleStart, etc.) stored in module entity
            // This allows GetModule() NWScript function to access module scripts via GetScript()
            IScriptHooksComponent scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                // Copy scripts from RuntimeModule to module entity's script hooks component
                // Module scripts are executed with module entity as owner (OBJECT_SELF in script context)
                if (!string.IsNullOrEmpty(runtimeModule.GetScript(ScriptEvent.OnModuleLoad)))
                {
                    scriptHooks.SetScript(ScriptEvent.OnModuleLoad, runtimeModule.GetScript(ScriptEvent.OnModuleLoad));
                }
                if (!string.IsNullOrEmpty(runtimeModule.GetScript(ScriptEvent.OnModuleStart)))
                {
                    scriptHooks.SetScript(ScriptEvent.OnModuleStart, runtimeModule.GetScript(ScriptEvent.OnModuleStart));
                }
                if (!string.IsNullOrEmpty(runtimeModule.GetScript(ScriptEvent.OnClientEnter)))
                {
                    scriptHooks.SetScript(ScriptEvent.OnClientEnter, runtimeModule.GetScript(ScriptEvent.OnClientEnter));
                }
                if (!string.IsNullOrEmpty(runtimeModule.GetScript(ScriptEvent.OnClientLeave)))
                {
                    scriptHooks.SetScript(ScriptEvent.OnClientLeave, runtimeModule.GetScript(ScriptEvent.OnClientLeave));
                }
                if (!string.IsNullOrEmpty(runtimeModule.GetScript(ScriptEvent.OnModuleHeartbeat)))
                {
                    scriptHooks.SetScript(ScriptEvent.OnModuleHeartbeat, runtimeModule.GetScript(ScriptEvent.OnModuleHeartbeat));
                }
            }

            // Register module entity with world
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module entity registered in world for GetEntityByTag and GetEntity lookups
            // Module entity can be looked up by Tag (module ResRef) or by ObjectId (ModuleObjectId)
            _world.RegisterEntity(entity);

            return entity;
        }

        /// <summary>
        /// Fires a module script (OnModuleLoad, OnModuleStart, etc.).
        /// </summary>
        /// <param name="scriptEvent">The script event to fire.</param>
        /// <remarks>
        /// swkotor2.exe: Module script execution system
        /// - Module scripts are executed with module entity as owner (OBJECT_SELF in script context)
        /// - Module entity has fixed ObjectId 0x7F000002 (World.ModuleObjectId)
        /// - Scripts are stored in module entity's IScriptHooksComponent
        /// </remarks>
        private void FireModuleScript(ScriptEvent scriptEvent)
        {
            if (_currentModule == null || _scriptExecutor == null)
            {
                return;
            }

            // Get module script
            string scriptResRef = _currentModule.GetScript(scriptEvent);
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return;
            }

            // Execute module script with module entity as owner
            // Module scripts use module entity with fixed ObjectId 0x7F000002 (World.ModuleObjectId)
            IEntity moduleEntity = CreateOrGetModuleEntity(_currentModule);
            if (moduleEntity != null)
            {
                _scriptExecutor.ExecuteScript(scriptResRef, moduleEntity, null);
            }
        }

        /// <summary>
        /// Fires the module heartbeat script.
        /// </summary>
        private void FireModuleHeartbeat()
        {
            if (_currentModule == null || _scriptExecutor == null)
            {
                return;
            }

            // Get module heartbeat script
            string heartbeatScript = _currentModule.GetScript(ScriptEvent.OnModuleHeartbeat);
            if (string.IsNullOrEmpty(heartbeatScript))
            {
                return;
            }

            // Execute module heartbeat script
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module heartbeat script execution
            // Located via string references: "Mod_OnHeartbeat" @ 0x007be840
            // Original implementation: Module heartbeat fires every 6 seconds for module-level scripts
            // Module scripts use module entity with fixed ObjectId 0x7F000002 (World.ModuleObjectId)
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Module entity created with fixed ObjectId 0x7F000002 for script execution
            // Module entity has IScriptHooksComponent for script execution, Tag set to module ResRef
            IEntity moduleEntity = CreateOrGetModuleEntity(_currentModule);
            if (moduleEntity != null)
            {
                _scriptExecutor.ExecuteScript(heartbeatScript, moduleEntity, null);
            }
        }

        private void FireScriptEvent(IEntity entity, ScriptEvent scriptEvent, IEntity target)
        {
            if (entity == null || _scriptExecutor == null)
            {
                return;
            }

            IScriptHooksComponent scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks == null)
            {
                return;
            }

            string scriptResRef = scriptHooks.GetScript(scriptEvent);
            if (!string.IsNullOrEmpty(scriptResRef))
            {
                _scriptExecutor.ExecuteScript(scriptResRef, entity, target);
            }
        }

        /// <summary>
        /// Loads a dialogue file.
        /// </summary>
        private BioWare.NET.Resource.Formats.GFF.Generics.DLG.DLG LoadDialogue(string resRef)
        {
            if (string.IsNullOrEmpty(resRef) || _installation == null)
            {
                return null;
            }

            try
            {
                BioWare.NET.Extract.ResourceResult resource = _installation.Resources.LookupResource(resRef, ResourceType.DLG);
                if (resource == null || resource.Data == null)
                {
                    return null;
                }

                return BioWare.NET.Resource.Formats.GFF.Generics.DLG.DLGHelper.ReadDlg(resource.Data);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GameSession] Error loading dialogue " + resRef + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Loads a script file.
        /// </summary>
        private byte[] LoadScript(string resRef)
        {
            if (string.IsNullOrEmpty(resRef) || _installation == null)
            {
                return null;
            }

            try
            {
                BioWare.NET.Extract.ResourceResult resource = _installation.Resources.LookupResource(resRef, ResourceType.NCS);
                if (resource == null || resource.Data == null)
                {
                    return null;
                }

                return resource.Data;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GameSession] Error loading script " + resRef + ": " + ex.Message);
                return null;
            }
        }

        #region Input Handler Event Handlers

        /// <summary>
        /// Handles move command from input handler.
        /// </summary>
        private void OnMoveCommand(System.Numerics.Vector3 destination)
        {
            if (_playerEntity == null)
            {
                return;
            }

            // Get character controller for player
            CharacterController controller = GetCharacterController(_playerEntity);
            if (controller != null)
            {
                controller.MoveTo(destination, true);
            }
        }

        /// <summary>
        /// Handles attack command from input handler.
        /// </summary>
        private void OnAttackCommand(IEntity target)
        {
            if (_playerEntity == null || target == null)
            {
                return;
            }

            // Queue attack action
            IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Add(new ActionAttack(target.ObjectId));
            }
        }

        /// <summary>
        /// Handles interact command from input handler.
        /// </summary>
        private void OnInteractCommand(IEntity target)
        {
            if (_playerEntity == null || target == null)
            {
                return;
            }

            // Queue use object action
            IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
            if (actionQueue != null)
            {
                actionQueue.Add(new ActionUseObject(target.ObjectId));
            }
        }

        /// <summary>
        /// Handles talk command from input handler.
        /// </summary>
        private void OnTalkCommand(IEntity target)
        {
            if (_playerEntity == null || target == null || _dialogueManager == null)
            {
                return;
            }

            // Start conversation with target
            // Get dialogue ResRef from target entity (stored in entity data or component)
            string dialogueResRef = null;
            if (target is Entity concreteEntity)
            {
                dialogueResRef = concreteEntity.GetData<string>("Conversation", null);
            }

            if (string.IsNullOrEmpty(dialogueResRef))
            {
                // Try to get from creature component
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Conversation ResRef stored in creature template
                // Located via string references: "Conversation" @ creature template fields
                // Original implementation: Conversation field in UTC template contains dialogue ResRef
                Console.WriteLine("[GameSession] No conversation found for entity: " + target.Tag);
                return;
            }

            _dialogueManager.StartConversation(dialogueResRef, target, _playerEntity);
        }

        /// <summary>
        /// Handles pause state change from input handler.
        /// </summary>
        private void OnPauseChanged(bool isPaused)
        {
            // Update world time manager pause state
            if (_world != null && _world.TimeManager != null)
            {
                _world.TimeManager.IsPaused = isPaused;
            }
        }

        /// <summary>
        /// Handles leader cycle from input handler.
        /// </summary>
        private void OnLeaderCycled()
        {
            // Update input handler controller for new leader
            if (_inputHandler != null && _partySystem != null && _partySystem.Leader != null)
            {
                IEntity leaderEntity = _partySystem.Leader.Entity;
                if (leaderEntity != null)
                {
                    CharacterController controller = GetCharacterController(leaderEntity);
                    _inputHandler.SetController(controller);
                }
            }
        }

        /// <summary>
        /// Handles quick slot usage from input handler.
        /// </summary>
        private void OnQuickSlotUsed(int slotIndex)
        {
            if (_playerEntity == null)
            {
                return;
            }

            // Get quick slot item/ability and use it
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Quick slot system
            // Located via string references: "QuickSlot" @ inventory/ability system
            // Original implementation: Quick slots store items/abilities, using slot triggers use action
            IQuickSlotComponent quickSlots = _playerEntity.GetComponent<IQuickSlotComponent>();
            if (quickSlots == null)
            {
                return;
            }

            int slotType = quickSlots.GetQuickSlotType(slotIndex);
            if (slotType < 0)
            {
                return; // Empty slot
            }

            if (slotType == 0)
            {
                // Item slot: Use the item
                IEntity item = quickSlots.GetQuickSlotItem(slotIndex);
                if (item != null && item.IsValid)
                {
                    // Queue ActionUseItem action
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Item usage system
                    // Original implementation: ActionUseItem queues item use action, applies item effects
                    IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
                    if (actionQueue != null)
                    {
                        // Queue ActionUseItem action
                        var useItemAction = new RuntimeCore.Actions.ActionUseItem(item.ObjectId, _playerEntity.ObjectId);
                        actionQueue.Add(useItemAction);
                    }
                }
            }
            else if (slotType == 1)
            {
                // Ability slot: Cast the spell/feat
                int abilityId = quickSlots.GetQuickSlotAbility(slotIndex);
                if (abilityId >= 0)
                {
                    // Queue ActionCastSpellAtObject action (target self for now)
                    // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Spell casting from quick slots
                    // Located via string references: Quick slot system handles spell casting
                    // Original implementation: Quick slot ability usage casts spell at self or selected target
                    // Spell data (cast time, Force point cost, effects) is looked up from spells.2da via GameDataManager
                    IActionQueueComponent actionQueue = _playerEntity.GetComponent<IActionQueueComponent>();
                    if (actionQueue != null)
                    {
                        // Use GameDataManager for spell data lookup (spell cast time, Force point cost, effects)
                        var castAction = new RuntimeCore.Actions.ActionCastSpellAtObject(abilityId, _playerEntity.ObjectId, _gameDataManager);
                        actionQueue.Add(castAction);
                    }
                }
            }
        }

        /// <summary>
        /// Gets or creates a character controller for an entity.
        /// </summary>
        private CharacterController GetCharacterController(IEntity entity)
        {
            if (entity == null || _world == null || _world.CurrentArea == null)
            {
                return null;
            }

            // Check if entity already has a controller stored
            if (entity is Entity concreteEntity && concreteEntity.HasData("CharacterController"))
            {
                return concreteEntity.GetData<CharacterController>("CharacterController");
            }

            // Create new controller
            INavigationMesh navMesh = _world.CurrentArea.NavigationMesh;
            if (navMesh == null)
            {
                return null;
            }

            CharacterController controller = new CharacterController(
                entity,
                _world,
                navMesh as NavigationMesh
            );

            // Store controller in entity data
            if (entity is Entity concreteEntity2)
            {
                concreteEntity2.SetData("CharacterController", controller);
            }

            return controller;
        }

        /// <summary>
        /// Gets the base attack bonus for a given level from an attack bonus table.
        /// </summary>
        /// <param name="tableName">Name of the attack bonus table (e.g., "BAB_FAST", "BAB_SLOW").</param>
        /// <param name="level">Character level (1-based).</param>
        /// <param name="gameDataManager">GameDataManager to look up the table.</param>
        /// <returns>The base attack bonus for the level, or 0 if table not found.</returns>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Attack bonus table lookup
        /// Attack bonus tables (e.g., BAB_FAST.2da, BAB_SLOW.2da) contain BAB progression
        /// Table structure: Row 0 = level 1, Row 1 = level 2, etc.
        /// Columns typically: Level, BAB value
        /// </remarks>
        private int GetAttackBonusFromTable(string tableName, int level, Data.GameDataManager gameDataManager)
        {
            if (string.IsNullOrEmpty(tableName) || gameDataManager == null || level < 1)
            {
                return 0;
            }

            try
            {
                // Look up the attack bonus table
                BioWare.NET.Resource.Formats.TwoDA.TwoDA table = gameDataManager.GetTable(tableName);
                if (table == null)
                {
                    Console.WriteLine($"[GameSession] GetAttackBonusFromTable: Table '{tableName}' not found");
                    return 0;
                }

                // Level is 1-based, table rows are 0-based
                int rowIndex = level - 1;
                if (rowIndex < 0 || rowIndex >= table.GetHeight())
                {
                    Console.WriteLine($"[GameSession] GetAttackBonusFromTable: Level {level} out of range for table '{tableName}' (height: {table.GetHeight()})");
                    return 0;
                }

                // Get row for this level
                BioWare.NET.Resource.Formats.TwoDA.TwoDARow row = table.GetRow(rowIndex);
                if (row == null)
                {
                    return 0;
                }

                // Get BAB value from row
                // Column name may vary: "BAB", "Value", or just the first numeric column
                int? babValue = row.GetInteger("BAB");
                if (!babValue.HasValue)
                {
                    babValue = row.GetInteger("Value");
                }
                if (!babValue.HasValue)
                {
                    // Try to get first integer column
                    var columns = row.GetColumnNames();
                    foreach (string colName in columns)
                    {
                        if (colName != "Label" && colName != "Name")
                        {
                            babValue = row.GetInteger(colName);
                            if (babValue.HasValue)
                            {
                                break;
                            }
                        }
                    }
                }

                return babValue ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSession] GetAttackBonusFromTable: Exception reading table '{tableName}': {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Gets the saving throw values for a given level from a saving throw table.
        /// </summary>
        /// <param name="tableName">Name of the saving throw table (e.g., "SAVE_GOOD", "SAVE_BAD").</param>
        /// <param name="level">Character level (1-based).</param>
        /// <param name="gameDataManager">GameDataManager to look up the table.</param>
        /// <param name="fortSave">Output: Fortitude save value.</param>
        /// <param name="reflexSave">Output: Reflex save value.</param>
        /// <param name="willSave">Output: Will save value.</param>
        /// <remarks>
        /// Based on swkotor.exe, swkotor2.exe: Saving throw table lookup
        /// Saving throw tables (e.g., SAVE_GOOD.2da, SAVE_BAD.2da) contain save progression
        /// Table structure: Row 0 = level 1, Row 1 = level 2, etc.
        /// Columns typically: Level, Fort, Reflex, Will
        /// Good saves: +2 at level 1, +1 per 2 levels
        /// Bad saves: +0 at level 1, +1 per 3 levels
        /// </remarks>
        private void GetSavingThrowsFromTable(string tableName, int level, Data.GameDataManager gameDataManager, out int fortSave, out int reflexSave, out int willSave)
        {
            fortSave = 0;
            reflexSave = 0;
            willSave = 0;

            if (string.IsNullOrEmpty(tableName) || gameDataManager == null || level < 1)
            {
                return;
            }

            try
            {
                // Look up the saving throw table
                BioWare.NET.Resource.Formats.TwoDA.TwoDA table = gameDataManager.GetTable(tableName);
                if (table == null)
                {
                    Console.WriteLine($"[GameSession] GetSavingThrowsFromTable: Table '{tableName}' not found");
                    return;
                }

                // Level is 1-based, table rows are 0-based
                int rowIndex = level - 1;
                if (rowIndex < 0 || rowIndex >= table.GetHeight())
                {
                    Console.WriteLine($"[GameSession] GetSavingThrowsFromTable: Level {level} out of range for table '{tableName}' (height: {table.GetHeight()})");
                    return;
                }

                // Get row for this level
                BioWare.NET.Resource.Formats.TwoDA.TwoDARow row = table.GetRow(rowIndex);
                if (row == null)
                {
                    return;
                }

                // Get save values from row
                // Column names may vary: "Fort", "Reflex", "Will" or "FORT", "REFLEX", "WILL"
                int? fort = row.GetInteger("Fort") ?? row.GetInteger("FORT");
                int? reflex = row.GetInteger("Reflex") ?? row.GetInteger("REFLEX");
                int? will = row.GetInteger("Will") ?? row.GetInteger("WILL");

                // If column names don't match, try numeric column indices
                if (!fort.HasValue || !reflex.HasValue || !will.HasValue)
                {
                    var columns = row.GetColumnNames();
                    int colIndex = 0;
                    foreach (string colName in columns)
                    {
                        if (colName != "Label" && colName != "Name" && colName != "Level")
                        {
                            int? value = row.GetInteger(colName);
                            if (value.HasValue)
                            {
                                if (colIndex == 0) fort = value;
                                else if (colIndex == 1) reflex = value;
                                else if (colIndex == 2)
                                {
                                    will = value;
                                    break;
                                }
                                colIndex++;
                            }
                        }
                    }
                }

                fortSave = fort ?? 0;
                reflexSave = reflex ?? 0;
                willSave = will ?? 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSession] GetSavingThrowsFromTable: Exception reading table '{tableName}': {ex.Message}");
            }
        }

        #endregion
    }
}
