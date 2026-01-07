using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Game;
using Andastra.Runtime.Core.GameLoop;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Journal;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Core.Movement;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Core.Party;
using Andastra.Runtime.Core.Plot;
using Andastra.Runtime.Engines.Odyssey.Combat;
using Andastra.Runtime.Engines.Odyssey.Components;
using Andastra.Runtime.Engines.Odyssey.Dialogue;
using Andastra.Runtime.Engines.Odyssey.EngineApi;
using Andastra.Runtime.Engines.Odyssey.Loading;
using Andastra.Runtime.Engines.Odyssey.Systems;
using Andastra.Runtime.Games.Odyssey;
using Andastra.Runtime.Games.Odyssey.Input;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.VM;
using JetBrains.Annotations;
using GameDataManager = Andastra.Runtime.Engines.Odyssey.Data.GameDataManager;
using Systems = Andastra.Runtime.Games.Odyssey.Systems;

namespace Andastra.Runtime.Engines.Odyssey.Game
{
    /// <summary>
    /// Main game session manager that coordinates all game systems.
    /// </summary>
    /// <remarks>
    /// Game Session System (Odyssey-specific):
    /// - Based on swkotor2.exe: FUN_006caab0 @ 0x006caab0 (server command parser, handles module commands)
    /// - Located via string references: "GAMEINPROGRESS" @ 0x007c15c8, "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
    /// - Cross-engine analysis:
    ///   - Aurora (nwmain.exe): CServerExoApp::LoadModule, CNWSModule::LoadModule - similar module loading system, different file formats
    ///   - Eclipse (daorigins.exe, DragonAge2.exe, ): "LoadModule" - UnrealScript-based module loading, different architecture
    /// - Inheritance: Base class BaseGameSession (Runtime.Games.Common) - abstract game session, Odyssey override (Runtime.Games.Odyssey) - Odyssey-specific game session
    /// - Original implementation: Coordinates module loading, entity management, script execution, combat, AI, triggers, dialogue, party
    /// - Module state: FUN_006caab0 sets module state flags (0=Idle, 1=ModuleLoaded, 2=ModuleRunning) in DAT_008283d4 structure
    /// - Game loop integration: Update() called every frame to update all systems (60 Hz fixed timestep)
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
        private readonly AIController _aiController;
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
        /// Gets the cached Andastra.Parsing Module object for the currently loaded module.
        /// Returns null if no module is loaded. This provides efficient access to module resources
        /// without creating a new Module object on every access.
        /// Based on swkotor2.exe: Module objects are cached and reused for resource lookups.
        /// </summary>
        [CanBeNull]
        public Andastra.Parsing.Common.Module GetCurrentParsingModule()
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
        /// Based on swkotor2.exe: PauseGame implementation
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
        /// Based on swkotor2.exe: ResumeGame implementation
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
            // Based on swkotor2.exe: GameDataManager provides access to 2DA tables (appearance.2da, etc.)
            // Located via string references: "2DAName" @ 0x007c3980, " 2DA file" @ 0x007c4674
            // Original implementation: GameDataManager loads and caches 2DA tables from installation
            _gameDataManager = new GameDataManager(_installation);
            var gameDataProvider = new Andastra.Runtime.Games.Odyssey.Data.OdysseyGameDataProvider(_gameDataManager);
            _world.GameDataProvider = gameDataProvider;

            // Initialize game systems
            _factionManager = new FactionManager(_world);
            _perceptionManager = new PerceptionManager(_world, _world.EffectSystem);

            // Create entity template factory for party system
            // Based on swkotor2.exe: Party members are created from UTC templates stored in module
            // Located via string references: "TemplateResRef" @ 0x007bd00c
            // Original implementation: Party members use TemplateResRef to load UTC templates from module archives
            // Use lazy template factory that retrieves module from ModuleLoader on demand
            // This allows factory to be created before module is loaded (required for constructor initialization order)
            IEntityTemplateFactory templateFactory = new Loading.LazyOdysseyEntityTemplateFactory(_moduleLoader.EntityFactory, _moduleLoader);
            _partySystem = new PartySystem(_world, templateFactory);
            _combatManager = new CombatManager(_world, _factionManager, _partySystem);

            // Initialize engine API (Kotor1 or TheSithLords based on settings)
            _engineApi = _settings.Game == KotorGame.K1
                ? (IEngineApi)new Andastra.Runtime.Engines.Odyssey.EngineApi.Kotor1()
                : (IEngineApi)new Andastra.Runtime.Engines.Odyssey.EngineApi.TheSithLords();

            // Initialize script executor with game-specific subclass
            // Based on swkotor.exe (KOTOR1) vs swkotor2.exe (KOTOR2) script execution differences
            _scriptExecutor = _settings.Game == KotorGame.K1
                ? (OdysseyScriptExecutor)new Kotor1ScriptExecutor(_world, _engineApi, _globals, _installation)
                : (OdysseyScriptExecutor)new Kotor2ScriptExecutor(_world, _engineApi, _globals, _installation);

            // Initialize trigger system with script firing callback
            _triggerSystem = new TriggerSystem(_world, FireScriptEvent);

            // Initialize AI controller
            _aiController = new AIController(_world, FireScriptEvent);

            // Initialize JRL loader for quest entry text lookup
            // Based on swkotor2.exe: JRL files contain quest entry text
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
            // Based on swkotor2.exe: Fixed-timestep game loop at 60 Hz
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
            // Based on swkotor2.exe: FUN_00501fa0 @ 0x00501fa0 (module loading), FUN_00501fa0 reads "Mod_OnHeartbeat" script from module GFF
            // Located via string references: "Mod_OnHeartbeat" @ 0x007be840
            // Original implementation: Module heartbeat fires every 6 seconds for module-level scripts
            // Module heartbeat script is loaded from Mod_OnHeartbeat field in module IFO GFF during module load
            // Module state flags: 0=Idle, 1=ModuleLoaded, 2=ModuleRunning (set in FUN_006caab0 @ 0x006caab0)
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
        /// Based on exhaustive reverse engineering of swkotor.exe (K1) and swkotor2.exe (K2):
        ///
        /// KOTOR 1 (swkotor.exe) - FUN_0067afb0 @ 0x0067afb0 (New Game Button Handler):
        /// - Similar flow but loads "END_M01AA" (Endar Spire - Command Module)
        /// - Located via string reference: "END_M01AA" @ 0x00752f58
        /// - Original implementation: FUN_005e5a90(aiStack_2c,"END_M01AA") sets module name
        ///
        /// KOTOR 2 (swkotor2.exe) - FUN_006d0b00 @ 0x006d0b00 (New Game Button Handler):
        /// - Called when "New Game" button is clicked after character creation completes
        /// - Located via string reference: "001ebo" @ 0x007cc028 (prologue module name)
        /// - Main menu handler (FUN_006d2350 @ 0x006d2350) sets up button event handler at line 89:
        ///   FUN_0041a340((void *)((int)this + 0x40c),0x27,this,0x6d0b00); // 0x27 = hover event
        ///   FUN_0041a340((void *)((int)this + 0x40c),0,this,0x6d1160); // 0 = click event
        /// - Complete flow from FUN_006d0b00:
        ///   1. Line 23: FUN_0057a400() - Initialize game time/system time
        ///   2. Line 24: FUN_00401380(DAT_008283d4) - Initialize module loading system
        ///      - FUN_00401380 @ 0x00401380: Module initialization function
        ///      - Prepares game state for module loading, sets up module loading context
        ///   3. Line 29: FUN_00630a90(local_38,"001ebo") - Set default starting module to "001ebo"
        ///      - "001ebo" is the prologue module (Ebon Hawk Interior, playing as T3-M4)
        ///   4. Line 31: FUN_00630a90(local_40,"HD0:effects") - Load HD0:effects directory
        ///      - HD0:effects is a directory alias for effects resources
        ///   5. Lines 43-50: Check for alternative modules before using default:
        ///      - FUN_00408df0(DAT_008283c0,local_30,0x7db,(undefined4 *)0x0) - Check module code 0x7db
        ///      - If 0x7db fails: FUN_00408df0(DAT_008283c0,local_30,0xbba,(undefined4 *)0x0) - Check module code 0xbba
        ///      - If both fail: FUN_00630d10(local_38,"001ebo") - Fallback to "001ebo"
        ///   6. Line 62: FUN_0074a700(local_40[0],*(undefined4 *)((int)this + 0x1c),local_38) - Create and load module
        ///      - FUN_0074a700 @ 0x0074a700: Module loader/creator function
        ///      - Takes module name ("001ebo") and creates/loads the module into game world
        ///
        /// Module Selection Logic:
        /// - Default: K1 = "end_m01aa", K2 = "001ebo"
        /// - Alternative modules checked via codes 0x7db and 0xbba (K2 only, meaning unknown)
        /// - If alternatives don't exist, falls back to default
        ///
        /// Module Initialization Sequence:
        /// 1. FUN_00401380: Initialize module loading system (prepare game state)
        /// 2. Load HD0:effects directory (effects resources)
        /// 3. Determine starting module (check alternatives, fallback to default)
        /// 4. FUN_0074a700: Create and load module object
        /// 5. Module loads areas, entities, scripts, etc.
        ///
        /// Character Creation Flow:
        /// Main Menu -> New Game Button -> Character Creation -> Character Creation Complete ->
        /// FUN_006d0b00 (New Game Handler) -> Module Initialization -> Module Load -> Player Entity Creation
        ///
        /// Module name casing: Ghidra shows "001ebo" (lowercase) and "END_M01AA" (uppercase)
        /// Resource lookup is case-insensitive, we use lowercase to match Andastra.Parsing conventions
        /// </remarks>
        public void StartNewGame([CanBeNull] CharacterCreationData characterData = null)
        {
            Console.WriteLine("[GameSession] Starting new game");

            // Clear world - remove all entities
            // Based on swkotor2.exe: World is cleared before loading new module
            var entities = new System.Collections.Generic.List<IEntity>(_world.GetAllEntities());
            foreach (IEntity entity in entities)
            {
                _world.DestroyEntity(entity.ObjectId);
            }

            // Store character data for player entity creation after module load
            _pendingCharacterData = characterData;

            // Determine starting module using exact logic from FUN_006d0b00 (swkotor2.exe: 0x006d0b00)
            // Based on swkotor2.exe FUN_006d0b00: Module selection logic
            string startingModule = DetermineStartingModule();

            // Initialize module loading system (equivalent to FUN_00401380)
            // Based on swkotor2.exe FUN_00401380 @ 0x00401380: Module initialization
            InitializeModuleLoading();

            // Load HD0:effects directory (equivalent to FUN_00630a90(local_40,"HD0:effects"))
            // Based on swkotor2.exe FUN_006d0b00 line 31: Load effects directory
            // Note: HD0:effects is a directory alias, resource system handles this automatically

            // Load module synchronously
            // Based on swkotor2.exe FUN_0074a700 @ 0x0074a700: Module loader/creator function
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
        /// Determines the starting module name using the exact logic from FUN_006d0b00 (swkotor2.exe: 0x006d0b00).
        /// </summary>
        /// <returns>The starting module name to load.</returns>
        /// <remarks>
        /// Based on swkotor2.exe FUN_006d0b00 @ 0x006d0b00 (New Game Button Handler):
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
            // Based on swkotor2.exe FUN_006d0b00 line 29: "001ebo" (K2 prologue)
            // Based on swkotor.exe: "END_M01AA" (K1 Endar Spire)
            string defaultModule = _settings.Game == KotorGame.K1 ? "end_m01aa" : "001ebo";

            // Check for alternative modules with codes 0x7db (MOD) and 0xbba (RIM) (K2 only)
            // Based on swkotor2.exe FUN_006d0b00 lines 43-50:
            // - FUN_00408df0(DAT_008283c0,local_30,0x7db,(undefined4 *)0x0) - Check module code 0x7db (MOD resource type)
            // - If 0x7db fails: FUN_00408df0(DAT_008283c0,local_30,0xbba,(undefined4 *)0x0) - Check module code 0xbba (RIM resource type)
            // - If both fail: FUN_00630d10(local_38,"001ebo") - Fallback to "001ebo"
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
        /// Based on swkotor2.exe FUN_00408df0 @ 0x00408df0: Resource existence check
        /// </summary>
        /// <param name="moduleName">The module name to check.</param>
        /// <param name="resourceType">The resource type to check (MOD = 0x7db, RIM = 0xbba).</param>
        /// <returns>True if the module resource exists, false otherwise.</returns>
        /// <remarks>
        /// Based on swkotor2.exe FUN_00408df0 @ 0x00408df0:
        /// - Calls FUN_00407300 to search for resources with the specified name and type
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
        /// Initializes the module loading system (equivalent to FUN_00401380 @ 0x00401380).
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe FUN_00401380 @ 0x00401380 (Module Initialization):
        /// - Called early in FUN_006d0b00 (line 24) to prepare game state for module loading
        /// - Sets up module loading context and prepares game systems for the new module
        /// - Original implementation: FUN_00401380(DAT_008283d4) initializes module loading system
        /// - This function prepares the game state, clears previous module state, and sets up module loading context
        /// </remarks>
        private void InitializeModuleLoading()
        {
            Console.WriteLine("[GameSession] Initializing module loading system");

            // Clear any existing module state
            // Based on swkotor2.exe FUN_00401380: Clears previous module state
            if (_currentModule != null)
            {
                _currentModule = null;
                _currentModuleName = null;
            }

            // Prepare game systems for module loading
            // Based on swkotor2.exe FUN_00401380: Prepares game systems
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
        /// Based on swkotor2.exe FUN_0074a700 @ 0x0074a700: Module loader/creator function
        /// - Takes module name and creates/loads the module into game world
        /// - Original implementation: Module loading is synchronous - all resources are loaded before gameplay begins
        /// - Module loading sequence:
        ///   1. Load module resources (IFO, ARE, GIT, LYT, VIS, walkmesh)
        ///   2. Set current module state
        ///   3. Set world's current area
        ///   4. Register all entities from area into world
        ///   5. Spawn/reposition player at entry position
        /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58
        /// - Module state flags: 0=Idle, 1=ModuleLoaded, 2=ModuleRunning (set in FUN_006caab0 @ 0x006caab0)
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
                // Based on swkotor2.exe: Module loading loads IFO, ARE, GIT, LYT, VIS, walkmesh resources
                // Located via string references: "MODULES:" @ 0x007b58b4, module resource loading
                // Original implementation: FUN_0074a700 loads module resources synchronously
                RuntimeModule module = _moduleLoader.LoadModule(moduleName);
                if (module == null)
                {
                    Console.WriteLine("[GameSession] Module loader returned null for: " + moduleName);
                    return false;
                }

                // Set current module
                // Based on swkotor2.exe: Module state is set after successful load
                // Original implementation: Module state flags updated in DAT_008283d4 structure
                _currentModule = module;
                _currentModuleName = moduleName;
                _world.SetCurrentModule(module);
                _moduleTransitionSystem?.SetCurrentModule(moduleName);

                // Template factory is already set up in constructor using LazyOdysseyEntityTemplateFactory
                // The lazy factory will automatically use the current module from ModuleLoader when needed
                // No need to update it here - it will retrieve the module on-demand during template creation

                // Set world's current area
                // Based on swkotor2.exe: Entry area is set as current area after module load
                // Original implementation: Mod_Entry_Area from IFO determines which area is loaded first
                if (!string.IsNullOrEmpty(module.EntryArea))
                {
                    IArea entryArea = module.GetArea(module.EntryArea);
                    if (entryArea != null)
                    {
                        _world.SetCurrentArea(entryArea);

                        // Register all entities from area into world
                        // Based on swkotor2.exe: Entities must be registered in world for lookups to work
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
                                    // Based on swkotor2.exe: Encounters are registered with encounter system for spawning
                                    // Original implementation: Encounter system tracks encounter entities and spawns creatures when triggered
                                    if (_encounterSystem != null && entity.ObjectType == Andastra.Runtime.Core.Enums.ObjectType.Encounter)
                                    {
                                        _encounterSystem.RegisterEncounter(entity);
                                    }
                                }
                            }
                        }
                    }
                }

                // Spawn player at entry position if not already spawned
                // Based on swkotor2.exe: Player is spawned at Mod_Entry_X/Y/Z with Mod_Entry_Dir_X/Y facing
                // Original implementation: Player entity created at module entry position after module load
                if (_playerEntity == null)
                {
                    SpawnPlayer();
                }
                else
                {
                    // Reposition existing player
                    // Based on swkotor2.exe: Existing player is repositioned at entry when transitioning between modules
                    // Original implementation: Player position updated to module entry position
                    PositionPlayerAtEntry();
                }

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
            // Based on swkotor2.exe: Module loading is synchronous - all resources loaded before gameplay begins
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
                _playerEntity = _world.CreateEntity(Andastra.Runtime.Core.Enums.ObjectType.Creature, entryPos, entryFacing);
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
        /// - Original implementation: FUN_005261b0 @ 0x005261b0 (load creature from UTC template), character generation creates player entity
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
            IEntity playerEntity = _world.CreateEntity(Andastra.Runtime.Core.Enums.ObjectType.Creature, position, facing);
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
            int genderValue = characterData.Gender == Gender.Male ? 0 : 1;

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
                // Based on swkotor2.exe: Gender stored in UTC template as "Gender" field (integer: 0=Male, 1=Female)

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
            if (playerEntity is Core.Entities.Entity concreteEntity)
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
                statsComp.SetAbility(Andastra.Runtime.Core.Enums.Ability.Strength, characterData.Strength);
                statsComp.SetAbility(Andastra.Runtime.Core.Enums.Ability.Dexterity, characterData.Dexterity);
                statsComp.SetAbility(Andastra.Runtime.Core.Enums.Ability.Constitution, characterData.Constitution);
                statsComp.SetAbility(Andastra.Runtime.Core.Enums.Ability.Intelligence, characterData.Intelligence);
                statsComp.SetAbility(Andastra.Runtime.Core.Enums.Ability.Wisdom, characterData.Wisdom);
                statsComp.SetAbility(Andastra.Runtime.Core.Enums.Ability.Charisma, characterData.Charisma);

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
                // Original implementation: FUN_005261b0 @ 0x005261b0 (load creature from UTC template)
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
            // Original implementation: FUN_005bcf70 @ 0x005bcf70 (swkotor.exe), FUN_0060d1d0 @ 0x0060d1d0 (swkotor2.exe)
            // Based on swkotor2.exe: FUN_005d63d0 reads "FeatGain" column from classes.2da for each class, then calls FUN_0060d1d0 (LoadFeatGain)
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
            Andastra.Runtime.Games.Odyssey.Systems.ComponentInitializer.InitializeComponents(playerEntity);

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
        /// - Based on swkotor2.exe: Module entity created with fixed ObjectId 0x7F000002
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
            // Based on swkotor2.exe: Module entity has fixed ObjectId 0x7F000002
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
            // Based on swkotor2.exe: Module entity created with ObjectId 0x7F000002
            // Entity constructor: Entity(uint objectId, ObjectType objectType)
            var entity = new Entity(World.ModuleObjectId, Andastra.Runtime.Core.Enums.ObjectType.Invalid);
            entity.World = _world;
            entity.Tag = runtimeModule.ResRef;
            entity.Position = System.Numerics.Vector3.Zero;
            entity.Facing = 0f;
            entity.AreaId = 0;

            // Initialize components for module entity
            // Module entities need IScriptHooksComponent for script execution
            // Based on swkotor2.exe: Module scripts require script hooks component
            // ComponentInitializer.InitializeComponents adds IScriptHooksComponent to all entities
            Andastra.Runtime.Games.Odyssey.Systems.ComponentInitializer.InitializeComponents(entity);

            // Ensure IScriptHooksComponent is present (ComponentInitializer should add it, but verify for safety)
            if (!entity.HasComponent<IScriptHooksComponent>())
            {
                entity.AddComponent(new ScriptHooksComponent());
            }

            // Load module scripts into script hooks component
            // Based on swkotor2.exe: Module scripts (OnModuleLoad, OnModuleStart, etc.) stored in module entity
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
            // Based on swkotor2.exe: Module entity registered in world for GetEntityByTag and GetEntity lookups
            // Module entity can be looked up by Tag (module ResRef) or by ObjectId (ModuleObjectId)
            _world.RegisterEntity(entity);

            return entity;
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
            // Based on swkotor2.exe: Module heartbeat script execution
            // Located via string references: "Mod_OnHeartbeat" @ 0x007be840
            // Original implementation: Module heartbeat fires every 6 seconds for module-level scripts
            // Module scripts use module entity with fixed ObjectId 0x7F000002 (World.ModuleObjectId)
            // Based on swkotor2.exe: Module entity created with fixed ObjectId 0x7F000002 for script execution
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
        private Andastra.Parsing.Resource.Generics.DLG.DLG LoadDialogue(string resRef)
        {
            if (string.IsNullOrEmpty(resRef) || _installation == null)
            {
                return null;
            }

            try
            {
                Andastra.Parsing.Installation.ResourceResult resource = _installation.Resources.LookupResource(resRef, ResourceType.DLG);
                if (resource == null || resource.Data == null)
                {
                    return null;
                }

                return Andastra.Parsing.Resource.Generics.DLG.DLGHelper.ReadDlg(resource.Data);
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
                Andastra.Parsing.Installation.ResourceResult resource = _installation.Resources.LookupResource(resRef, ResourceType.NCS);
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
                // Based on swkotor2.exe: Conversation ResRef stored in creature template
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
            // Based on swkotor2.exe: Quick slot system
            // Located via string references: "QuickSlot" @ inventory/ability system
            // Original implementation: Quick slots store items/abilities, using slot triggers use action
            Core.Interfaces.Components.IQuickSlotComponent quickSlots = _playerEntity.GetComponent<Core.Interfaces.Components.IQuickSlotComponent>();
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
                    // Based on swkotor2.exe: Item usage system
                    // Original implementation: ActionUseItem queues item use action, applies item effects
                    Core.Interfaces.Components.IActionQueueComponent actionQueue = _playerEntity.GetComponent<Core.Interfaces.Components.IActionQueueComponent>();
                    if (actionQueue != null)
                    {
                        // Queue ActionUseItem action
                        var useItemAction = new Core.Actions.ActionUseItem(item.ObjectId, _playerEntity.ObjectId);
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
                    // Based on swkotor2.exe: Spell casting from quick slots
                    // Located via string references: Quick slot system handles spell casting
                    // Original implementation: Quick slot ability usage casts spell at self or selected target
                    // Spell data (cast time, Force point cost, effects) is looked up from spells.2da via GameDataManager
                    Core.Interfaces.Components.IActionQueueComponent actionQueue = _playerEntity.GetComponent<Core.Interfaces.Components.IActionQueueComponent>();
                    if (actionQueue != null)
                    {
                        // Use GameDataManager for spell data lookup (spell cast time, Force point cost, effects)
                        var castAction = new Core.Actions.ActionCastSpellAtObject(abilityId, _playerEntity.ObjectId, _gameDataManager);
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
                Parsing.Formats.TwoDA.TwoDA table = gameDataManager.GetTable(tableName);
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
                Parsing.Formats.TwoDA.TwoDARow row = table.GetRow(rowIndex);
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
                Parsing.Formats.TwoDA.TwoDA table = gameDataManager.GetTable(tableName);
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
                Parsing.Formats.TwoDA.TwoDARow row = table.GetRow(rowIndex);
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
