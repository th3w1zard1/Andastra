using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.GameLoop;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Andastra.Runtime.Core.Module;
using Andastra.Runtime.Core.Navigation;
using Andastra.Runtime.Core.Party;
using Andastra.Runtime.Core.Actions;
using Andastra.Runtime.Core.Combat;
using Andastra.Runtime.Core.Movement;
using Andastra.Runtime.Engines.Odyssey.Combat;
using Andastra.Runtime.Engines.Odyssey.Systems;
using Andastra.Runtime.Engines.Odyssey.Dialogue;
using Andastra.Runtime.Engines.Odyssey.Loading;
using Andastra.Runtime.Engines.Odyssey.EngineApi;
using Andastra.Runtime.Scripting.VM;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Core.Dialogue;
using Andastra.Runtime.Games.Odyssey;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Journal;
using Andastra.Runtime.Core.Plot;
using Andastra.Parsing.Installation;

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
        private readonly Data.GameDataManager _gameDataManager;

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
            _gameDataManager = new Data.GameDataManager(_installation);
            var gameDataProvider = new Data.OdysseyGameDataProvider(_gameDataManager);
            _world.GameDataProvider = gameDataProvider;

            // Initialize game systems
            _factionManager = new FactionManager(_world);
            _perceptionManager = new PerceptionManager(_world, _world.EffectSystem);

            // Create entity template factory for party system
            // Factory will be updated when module is loaded (see LoadModuleAsync)
            // For now, create without module (will be updated later)
            IEntityTemplateFactory templateFactory = null;
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
            // Based on swkotor2.exe: Input system handles click-to-move, object interaction, party control
            // Located via string references: "Input" @ 0x007c2520, "Mouse" @ 0x007cb908, "OnClick" @ 0x007c1a20
            // Original implementation: DirectInput8-based input system with click-to-move, object selection, party control
            _inputHandler = new PlayerInputHandler(_world, _partySystem);

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
        /// Starts a new game.
        /// </summary>
        public void StartNewGame()
        {
            Console.WriteLine("[GameSession] Starting new game");

            // Clear world - remove all entities
            var entities = new System.Collections.Generic.List<IEntity>(_world.GetAllEntities());
            foreach (IEntity entity in entities)
            {
                _world.DestroyEntity(entity.ObjectId);
            }

            // Load starting module (from settings or default)
            string startingModule = _settings.StartModule;
            if (string.IsNullOrEmpty(startingModule))
            {
                // Default starting modules
                startingModule = _settings.Game == KotorGame.K1 ? "end_m01aa" : "001ebo"; // Endar Spire or Peragus
            }

            // Load module synchronously for now (can be made async later)
            Task<bool> loadTask = LoadModuleAsync(startingModule);
            loadTask.Wait();
            bool success = loadTask.Result;

            if (!success)
            {
                Console.WriteLine("[GameSession] Failed to load starting module: " + startingModule);
                return;
            }

            Console.WriteLine("[GameSession] New game started in module: " + startingModule);
        }

        /// <summary>
        /// Loads a module asynchronously.
        /// </summary>
        private async Task<bool> LoadModuleAsync(string moduleName)
        {
            if (string.IsNullOrEmpty(moduleName))
            {
                return false;
            }

            try
            {
                Console.WriteLine("[GameSession] Loading module: " + moduleName);

                // Load module
                RuntimeModule module = _moduleLoader.LoadModule(moduleName);
                if (module == null)
                {
                    Console.WriteLine("[GameSession] Module loader returned null for: " + moduleName);
                    return false;
                }

                // Set current module
                _currentModule = module;
                _currentModuleName = moduleName;
                _world.SetCurrentModule(module);
                _moduleTransitionSystem?.SetCurrentModule(moduleName);

                // Update party system with template factory for current module
                // Based on swkotor2.exe: Party members are created from UTC templates stored in module
                // Located via string references: "TemplateResRef" @ 0x007bd00c
                // Original implementation: Party members use TemplateResRef to load UTC templates from module archives
                // Create template factory with current module for party member spawning
                if (_moduleLoader != null && _moduleLoader.EntityFactory != null)
                {
                    Andastra.Parsing.Common.Module parsingModule = _moduleLoader.GetCurrentModule();
                    if (parsingModule != null)
                    {
                        var templateFactory = new Loading.OdysseyEntityTemplateFactory(_moduleLoader.EntityFactory, parsingModule);
                        _partySystem?.SetTemplateFactory(templateFactory);
                    }
                }

                // Set world's current area
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
                        if (entryArea is RuntimeArea runtimeArea)
                        {
                            foreach (IEntity entity in runtimeArea.GetAllEntities())
                            {
                                if (entity != null && entity.IsValid)
                                {
                                    // Register entity in world (World is set during entity creation)
                                    _world.RegisterEntity(entity);

                                    // Register encounters with encounter system
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
                if (_playerEntity == null)
                {
                    SpawnPlayer();
                }
                else
                {
                    // Reposition existing player
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
        /// Spawns the player entity at the module entry point.
        /// </summary>
        private void SpawnPlayer()
        {
            if (_currentModule == null)
            {
                return;
            }

            System.Numerics.Vector3 entryPos = _currentModule.EntryPosition;
            float entryFacing = (float)Math.Atan2(_currentModule.EntryDirectionY, _currentModule.EntryDirectionX);

            // Create player entity
            _playerEntity = _world.CreateEntity(Andastra.Runtime.Core.Enums.ObjectType.Creature, entryPos, entryFacing);
            if (_playerEntity != null)
            {
                _playerEntity.Tag = "Player";
                _playerEntity.SetData("IsPlayer", true);

                // Add player to party as leader
                _partySystem?.SetPlayerCharacter(_playerEntity);
            }
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
            // Module scripts use module ResRef as context (no physical entity required)
            IEntity moduleEntity = _world.GetEntityByTag(_currentModule.ResRef, 0);
            if (moduleEntity == null)
            {
                // TODO: SIMPLIFIED - Create a temporary entity for module script execution
                moduleEntity = _world.CreateEntity(Andastra.Runtime.Core.Enums.ObjectType.Invalid, System.Numerics.Vector3.Zero, 0f);
                moduleEntity.Tag = _currentModule.ResRef;
            }
            _scriptExecutor.ExecuteScript(heartbeatScript, moduleEntity, null);
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

        #endregion
    }
}
