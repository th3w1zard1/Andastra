using System;
using System.Collections.Generic;
using System.IO;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Module;
using Odyssey.Core.Navigation;
using Odyssey.Core.Enums;
using Odyssey.Core.Save;
using Odyssey.Core.Actions;
using Odyssey.Scripting.Interfaces;
using Odyssey.Scripting.VM;
using Odyssey.Kotor.Dialogue;
using Odyssey.Kotor.Components;
using Odyssey.Kotor.Combat;
using Odyssey.Kotor.Loading;
using Odyssey.Kotor.Systems;
using Odyssey.Content.Save;
using CSharpKOTOR.Formats.TLK;
using CSharpKOTOR.Resource.Generics.DLG;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Event arguments for module loaded events.
    /// </summary>
    public class ModuleLoadedEventArgs : EventArgs
    {
        public string ModuleName { get; set; }
    }

    /// <summary>
    /// Container for GameSession services accessible from execution context.
    /// </summary>
    public class GameServicesContext
    {
        public DialogueManager DialogueManager { get; set; }
        public IEntity PlayerEntity { get; set; }
        public CombatManager CombatManager { get; set; }
        public PartyManager PartyManager { get; set; }
        public Loading.ModuleLoader ModuleLoader { get; set; }
        public FactionManager FactionManager { get; set; }
        public PerceptionManager PerceptionManager { get; set; }
        public bool IsLoadingFromSave { get; set; }
        public GameSession GameSession { get; set; }
        public Core.Camera.CameraController CameraController { get; set; }
    }

    /// <summary>
    /// Manages the current game session - module loading, saves, party, etc.
    /// </summary>
    /// <remarks>
    /// Game Session Management:
    /// - Based on swkotor2.exe game session system
    /// - Located via string references: "GAMEINPROGRESS" @ 0x007c68f0, "GAMEINPROGRESS:" @ 0x007bde44
    /// - "MODULES:" @ 0x007b58b4, "Module" @ 0x007bc4e0, "MODULE" @ 0x007beab8
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_LOAD" @ 0x007bc91c
    /// - "CSWSSCRIPTEVENT_EVENTTYPE_ON_MODULE_START" @ 0x007bc948
    /// - "ModuleName" @ 0x007bde2c, "LASTMODULE" @ 0x007be1d0, "ModuleLoaded" @ 0x007bdd70
    /// - Original engine uses "GAMEINPROGRESS:" prefix for game state directory (z:\gameinprogress, .\gameinprogress)
    /// - Game state stored in: GAMEINPROGRESS:PC, GAMEINPROGRESS:INVENTORY, GAMEINPROGRESS:REPUTE, etc.
    /// - Module loading triggers ON_MODULE_LOAD and ON_MODULE_START script events
    /// - Directory setup: FUN_00633270 @ 0x00633270 (sets up all game directories including GAMEINPROGRESS, MODULES)
    /// </remarks>
    public class GameSession : IDisposable
    {
        private readonly GameSessionSettings _settings;
        private readonly World _world;
        private readonly NcsVm _vm;
        private readonly IScriptGlobals _globals;
        private readonly Loading.ModuleLoader _moduleLoader;
        private readonly DialogueManager _dialogueManager;
        private readonly TriggerSystem _triggerSystem;
        private readonly HeartbeatSystem _heartbeatSystem;
        private readonly FactionManager _factionManager;
        private readonly CombatManager _combatManager;
        private readonly Odyssey.Core.Combat.EffectSystem _effectSystem;
        private readonly PerceptionManager _perceptionManager;
        private readonly ModuleTransitionSystem _moduleTransitionSystem;
        private readonly SaveSystem _saveSystem;
        private readonly PartyManager _partyManager;
        private readonly EncounterSystem _encounterSystem;

        private string _currentModuleName;
        private RuntimeModule _currentRuntimeModule;
        private bool _isRunning;
        private bool _isPaused;
        private bool _isLoadingFromSave;
        private NavigationMesh _currentNavMesh;
        private TLK _baseTlk;
        private TLK _customTlk;

        // Player and party
        private IEntity _playerEntity;

        /// <summary>
        /// Event fired when a module is loaded.
        /// </summary>
        public event EventHandler<ModuleLoadedEventArgs> OnModuleLoaded;

        /// <summary>
        /// Gets the current module name.
        /// </summary>
        public string CurrentModuleName
        {
            get { return _currentModuleName; }
        }

        /// <summary>
        /// Gets the player entity.
        /// </summary>
        public IEntity PlayerEntity
        {
            get { return _playerEntity; }
        }

        /// <summary>
        /// Gets the current navigation mesh.
        /// </summary>
        public NavigationMesh NavigationMesh
        {
            get { return _currentNavMesh; }
        }

        /// <summary>
        /// Gets the current runtime module.
        /// </summary>
        public RuntimeModule CurrentRuntimeModule
        {
            get { return _currentRuntimeModule; }
        }

        /// <summary>
        /// Gets the dialogue manager.
        /// </summary>
        public DialogueManager DialogueManager
        {
            get { return _dialogueManager; }
        }

        /// <summary>
        /// Gets the combat manager.
        /// </summary>
        public CombatManager CombatManager
        {
            get { return _combatManager; }
        }

        /// <summary>
        /// Gets the perception manager.
        /// </summary>
        public PerceptionManager PerceptionManager
        {
            get { return _perceptionManager; }
        }

        /// <summary>
        /// Gets the faction manager.
        /// </summary>
        public FactionManager FactionManager
        {
            get { return _factionManager; }
        }

        /// <summary>
        /// Gets the module transition system.
        /// </summary>
        public ModuleTransitionSystem ModuleTransitionSystem
        {
            get { return _moduleTransitionSystem; }
        }

        /// <summary>
        /// Gets the save system.
        /// </summary>
        public SaveSystem SaveSystem
        {
            get { return _saveSystem; }
        }

        public GameSession(object settings, World world, NcsVm vm, IScriptGlobals globals)
        {
            _settings = GameSessionSettings.FromGameSettings(settings);
            _world = world;
            _vm = vm;
            _globals = globals;

            // Create Installation from game path
            CSharpKOTOR.Installation.Installation installation = null;
            if (!string.IsNullOrEmpty(_settings.GamePath))
            {
                try
                {
                    installation = new CSharpKOTOR.Installation.Installation(_settings.GamePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GameSession] Failed to create Installation: " + ex.Message);
                }
            }

            if (installation == null)
            {
                throw new InvalidOperationException("Failed to create Installation from game path: " + _settings.GamePath);
            }

            _moduleLoader = new Loading.ModuleLoader(installation);

            // Create dialogue manager
            _dialogueManager = new DialogueManager(
                vm,
                world,
                LoadDialogue,
                LoadScript
            );

            // Create trigger system
            _triggerSystem = new TriggerSystem(world, FireScriptEvent);

            // Create heartbeat system
            _heartbeatSystem = new HeartbeatSystem(world, FireScriptEvent);

            // Create faction manager
            _factionManager = new FactionManager(world);

            // Create effect system
            _effectSystem = new Odyssey.Core.Combat.EffectSystem(world);

            // Create combat manager
            _combatManager = new CombatManager(world, _factionManager);

            // Create perception manager
            _perceptionManager = new PerceptionManager(world, _effectSystem);

            // Create party manager
            _partyManager = new PartyManager(world);

            // Create encounter system
            _encounterSystem = new EncounterSystem(
                world, 
                _factionManager, 
                FireScriptEvent, 
                _moduleLoader,
                (creature) => creature == _playerEntity, // IsPlayerCheck
                () => _moduleLoader?.GetCurrentModule() // GetCurrentModule
            );

            // Create module transition system
            _moduleTransitionSystem = new ModuleTransitionSystem(
                LoadModuleAsync,
                waypointTag => PositionPlayerAtWaypoint(_playerEntity, waypointTag)
            );

            // Subscribe to door events for module transitions
            world.EventBus.Subscribe<DoorOpenedEvent>(OnDoorOpened);

            // Create save system
            string savesDirectory = Path.Combine(_settings.GamePath, "saves");
            var saveSerializer = new Odyssey.Content.Save.SaveSerializer();
            var saveDataProvider = new SaveDataProvider(savesDirectory, saveSerializer);
            _saveSystem = new SaveSystem(world, saveDataProvider);
            _saveSystem.SetScriptGlobals(globals);

            // Create party manager
            _partyManager = new PartyManager(world);

            // Load talk tables
            LoadTalkTables();
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
        /// Loads a module asynchronously.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe module loading
        /// Original implementation: Loads module, triggers ON_MODULE_LOAD and ON_MODULE_START events
        /// Module loading progress tracked via progress callbacks
        /// </remarks>
        private async System.Threading.Tasks.Task<bool> LoadModuleAsync(string moduleName)
        {
            try
            {
                return await System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        LoadModule(moduleName);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSession] Error loading module {moduleName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Positions the player at a waypoint.
        /// </summary>
        private void PositionPlayerAtWaypoint(IEntity player, string waypointTag)
        {
            if (player == null || string.IsNullOrEmpty(waypointTag))
            {
                return;
            }

            IEntity waypoint = _world.GetEntityByTag(waypointTag);
            if (waypoint == null)
            {
                Console.WriteLine($"[GameSession] Waypoint not found: {waypointTag}");
                return;
            }

            ITransformComponent waypointTransform = waypoint.GetComponent<ITransformComponent>();
            if (waypointTransform == null)
            {
                return;
            }

            ITransformComponent playerTransform = player.GetComponent<ITransformComponent>();
            if (playerTransform != null)
            {
                playerTransform.Position = waypointTransform.Position;
                playerTransform.Facing = waypointTransform.Facing;
            }
        }

        /// <summary>
        /// Fires a script event for an entity.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe script event system
        /// Located via string references: "CSWSSCRIPTEVENT_EVENTTYPE_ON_*" @ 0x007bc594+
        /// Original implementation: Looks up script ResRef from entity's script hooks component,
        /// executes script with entity as OBJECT_SELF, triggerer as GetEnteringObject/GetExitingObject
        /// </remarks>
        private void FireScriptEvent(IEntity entity, ScriptEvent eventType, IEntity triggerer)
        {
            if (entity == null)
            {
                return;
            }

            IScriptHooksComponent scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks == null)
            {
                return;
            }

            string scriptResRef = scriptHooks.GetScript(eventType);
            if (!string.IsNullOrEmpty(scriptResRef))
            {
                ExecuteEntityScript(scriptResRef, entity, triggerer);
            }
        }

        /// <summary>
        /// Executes a script for an entity.
        /// </summary>
        private void ExecuteEntityScript(string scriptResRef, IEntity owner, IEntity triggerer)
        {
            byte[] scriptBytes = LoadScript(scriptResRef);
            if (scriptBytes == null || scriptBytes.Length == 0)
            {
                return;
            }

            try
            {
                // Create engine API instance
                var engineApi = new Odyssey.Scripting.EngineApi.K1EngineApi();
                
                // Create execution context
                var ctx = new Odyssey.Scripting.VM.ExecutionContext(
                    owner,
                    _world,
                    engineApi,
                    _globals
                );
                ctx.SetTriggerer(triggerer);
                ctx.ResourceProvider = _moduleLoader;
                
                // Store GameSession services in additional context
                var gameServices = new GameServicesContext
                {
                    DialogueManager = _dialogueManager,
                    PlayerEntity = _playerEntity,
                    CombatManager = _combatManager,
                    PartyManager = _partyManager,
                    ModuleLoader = _moduleLoader,
                    FactionManager = _factionManager,
                    PerceptionManager = _perceptionManager,
                    IsLoadingFromSave = _isLoadingFromSave,
                    GameSession = this
                };
                ctx.AdditionalContext = gameServices;

                _vm.Execute(scriptBytes, ctx);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSession] Error executing script {scriptResRef}: {ex.Message}");
            }
        }

        private void LoadTalkTables()
        {
            // Load base TLK
            string baseTlkPath = Path.Combine(_settings.GamePath, "dialog.tlk");
            if (File.Exists(baseTlkPath))
            {
                try
                {
                    _baseTlk = TLKAuto.ReadTlk(baseTlkPath);
                    Console.WriteLine("[GameSession] Loaded base TLK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GameSession] Failed to load base TLK: " + ex.Message);
                }
            }

            // Load custom TLK if present
            string customTlkPath = Path.Combine(_settings.GamePath, "dialogf.tlk");
            if (File.Exists(customTlkPath))
            {
                try
                {
                    _customTlk = TLKAuto.ReadTlk(customTlkPath);
                    Console.WriteLine("[GameSession] Loaded custom TLK");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[GameSession] Failed to load custom TLK: " + ex.Message);
                }
            }

            // Set TLK tables in dialogue manager
            _dialogueManager.SetTalkTables(_baseTlk, _customTlk);
        }

        /// <summary>
        /// Loads a dialogue by ResRef.
        /// </summary>
        public DLG LoadDialogue(string resRef)
        {
            // Load from module or global resources
            // This would use the resource provider
            return _moduleLoader.LoadDialogue(resRef);
        }

        /// <summary>
        /// Loads a script by ResRef.
        /// </summary>
        public byte[] LoadScript(string resRef)
        {
            return _moduleLoader.LoadScript(resRef);
        }

        /// <summary>
        /// Start a new game from the beginning.
        /// </summary>
        public void StartNewGame()
        {
            Console.WriteLine("[GameSession] Starting new game...");

            // Determine starting module
            string startModule = _settings.StartModule;
            if (string.IsNullOrEmpty(startModule))
            {
                // Default starting modules
                if (_settings.Game == KotorGameType.K1)
                {
                    startModule = "end_m01aa"; // Endar Spire - Command Module
                }
                else
                {
                    startModule = "001EBO"; // Ebon Hawk - Prologue
                }
            }

            // Load the module
            LoadModule(startModule);

            // Create player character
            // TODO: Character creation screen
            CreateDefaultPlayer();

            _isRunning = true;
            Console.WriteLine("[GameSession] Game started in module: " + startModule);
        }

        /// <summary>
        /// Load a save game.
        /// </summary>
        public void LoadSaveGame(string saveName)
        {
            // Use the existing LoadGame method
            bool success = LoadGame(saveName);
            if (!success)
            {
                throw new InvalidOperationException("Failed to load save game: " + saveName);
            }
        }

        /// <summary>
        /// Load a module by name.
        /// </summary>
        public void LoadModule(string moduleName)
        {
            Console.WriteLine("[GameSession] Loading module: " + moduleName);

            // Unload current module
            if (!string.IsNullOrEmpty(_currentModuleName))
            {
                UnloadCurrentModule();
            }

            // Load new module
            try
            {
                _currentRuntimeModule = _moduleLoader.LoadModule(moduleName);
                _currentModuleName = moduleName;

                // Get navigation mesh from loaded module
                _currentNavMesh = _moduleLoader.GetNavigationMesh();

                // Set current module in world
                IArea entryArea = null;
                if (_currentRuntimeModule != null)
                {
                    entryArea = _currentRuntimeModule.GetArea(_currentRuntimeModule.EntryArea);
                    if (entryArea != null && _world != null)
                    {
                        // Set current area in world
                        if (_world is World concreteWorld)
                        {
                            concreteWorld.SetCurrentArea(entryArea);
                        }
                    }
                }

                // Fire module OnModuleLoad script
                ExecuteModuleScript(Odyssey.Core.Enums.ScriptEvent.OnModuleLoad, _currentRuntimeModule);

                // Register all entities with heartbeat system
                if (_heartbeatSystem != null)
                {
                    _heartbeatSystem.RegisterAllEntities();
                }

                // Register all encounters with encounter system
                if (_encounterSystem != null && entryArea != null)
                {
                    RegisterAllEncounters(entryArea);
                }

                Console.WriteLine("[GameSession] Module loaded: " + moduleName);

                // Fire event
                OnModuleLoaded?.Invoke(this, new ModuleLoadedEventArgs { ModuleName = moduleName });
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[GameSession] Failed to load module: " + ex.Message);
                throw;
            }
        }

        private void UnloadCurrentModule()
        {
            Console.WriteLine("[GameSession] Unloading module: " + _currentModuleName);

            if (_currentRuntimeModule != null)
            {
                // Fire OnModuleLeave scripts
                ExecuteModuleScript(Odyssey.Core.Enums.ScriptEvent.OnModuleLeave, _currentRuntimeModule);
            }

            // TODO: Save persistent state (area states, etc.)
            // This would be handled by the save system when saving

            // Clear entities
            if (_world != null)
            {
                // Remove all entities except the player
                var entitiesToRemove = new List<IEntity>();
                foreach (IEntity entity in _world.GetAllEntities())
                {
                    if (entity != _playerEntity && entity != null)
                    {
                        entitiesToRemove.Add(entity);
                    }
                }

                foreach (IEntity entity in entitiesToRemove)
                {
                    _world.DestroyEntity(entity.ObjectId);
                }
            }

            _currentRuntimeModule = null;
            _currentModuleName = null;
        }

        private void CreateDefaultPlayer()
        {
            Console.WriteLine("[GameSession] Creating player entity...");

            // Get entry position from module
            System.Numerics.Vector3 entryPos = System.Numerics.Vector3.Zero;
            float entryFacing = 0f;

            if (_moduleLoader != null)
            {
                entryPos = _moduleLoader.GetEntryPosition();
                entryFacing = _moduleLoader.GetEntryFacing();
            }

            // Create player entity
            _playerEntity = _world.CreateEntity(Core.Enums.ObjectType.Creature, entryPos, entryFacing);
            _playerEntity.Tag = "Player";

            // Add transform component
            var transformComponent = new TransformComponent();
            transformComponent.Position = entryPos;
            transformComponent.Facing = entryFacing;
            _playerEntity.AddComponent(transformComponent);

            // Add stats component with default KOTOR starting stats
            var statsComponent = new StatsComponent();
            statsComponent.SetAbility(Ability.Strength, 12);
            statsComponent.SetAbility(Ability.Dexterity, 12);
            statsComponent.SetAbility(Ability.Constitution, 12);
            statsComponent.SetAbility(Ability.Intelligence, 12);
            statsComponent.SetAbility(Ability.Wisdom, 12);
            statsComponent.SetAbility(Ability.Charisma, 12);
            statsComponent.SetMaxHP(30); // Level 1 Soldier base HP
            statsComponent.CurrentHP = 30;
            statsComponent.MaxFP = 0; // No force powers at start
            statsComponent.CurrentFP = 0;
            statsComponent.Level = 1;
            statsComponent.Experience = 0;
            statsComponent.SetBaseAttackBonus(1); // Level 1 BAB
            statsComponent.SetBaseSaves(2, 0, 0); // Level 1 Soldier saves
            _playerEntity.AddComponent(statsComponent);

            // Add creature component
            var creatureComponent = new CreatureComponent();
            creatureComponent.AppearanceType = 1; // Default human male
            creatureComponent.BodyVariation = 0;
            creatureComponent.TextureVar = 0;
            _playerEntity.AddComponent(creatureComponent);

            // Add script hooks component
            var scriptHooksComponent = new ScriptHooksComponent();
            _playerEntity.AddComponent(scriptHooksComponent);

            // Register entity in world
            if (_playerEntity is Odyssey.Core.Entities.Entity concreteEntity)
            {
                _world.RegisterEntity(concreteEntity);
            }

            Console.WriteLine("[GameSession] Player entity created at " + entryPos + " with facing " + entryFacing);
        }

        /// <summary>
        /// Update the game session each frame.
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!_isRunning || _isPaused)
            {
                return;
            }

            // Update action queues for all entities
            foreach (IEntity entity in _world.GetAllEntities())
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                IActionQueueComponent actionQueue = entity.GetComponent<IActionQueueComponent>();
                if (actionQueue != null)
                {
                    actionQueue.Update(entity, deltaTime);
                }
            }

            // Process delayed commands (handled by World.Update)

            // Update heartbeat system (fires OnHeartbeat scripts for entities)
            if (_heartbeatSystem != null)
            {
                _heartbeatSystem.Update(deltaTime);
            }

            // Fire module heartbeat script
            if (_currentRuntimeModule != null)
            {
                ExecuteModuleScript(Odyssey.Core.Enums.ScriptEvent.OnHeartbeat, _currentRuntimeModule);
            }

            // Update dialogue manager
            if (_dialogueManager != null)
            {
                _dialogueManager.Update(deltaTime);
            }

            // Update trigger system (fires OnEnter/OnExit scripts)
            if (_triggerSystem != null)
            {
                _triggerSystem.Update();
            }

            // Update encounter system (spawns creatures when entered)
            if (_encounterSystem != null)
            {
                _encounterSystem.Update(deltaTime);
            }

            // Update perception system
            if (_perceptionManager != null)
            {
                _perceptionManager.Update(deltaTime);
            }

            // Update combat system
            if (_combatManager != null)
            {
                _combatManager.Update(deltaTime);
            }

            // Update world (processes delay scheduler)
            _world.Update(deltaTime);
        }

        /// <summary>
        /// Executes a module script event.
        /// </summary>
        private void ExecuteModuleScript(Odyssey.Core.Enums.ScriptEvent eventType, Odyssey.Core.Module.RuntimeModule module)
        {
            if (module == null)
            {
                return;
            }

            string scriptResRef = module.GetScript(eventType);
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return; // No script for this event
            }

            try
            {
                // Load script bytes
                byte[] scriptBytes = LoadScript(scriptResRef);
                if (scriptBytes == null || scriptBytes.Length == 0)
                {
                    Console.WriteLine($"[GameSession] Script not found: {scriptResRef}");
                    return;
                }

                // Create execution context
                // Note: We need to create an engine API instance
                // For now, create a K1EngineApi (will be game-specific later)
                var engineApi = new Odyssey.Scripting.EngineApi.K1EngineApi();
                var ctx = new Odyssey.Scripting.VM.ExecutionContext(
                    null, // Module scripts have no caller
                    _world,
                    engineApi,
                    _globals
                );

                // Set resource provider (use Installation for now)
                try
                {
                    var installation = new CSharpKOTOR.Installation.Installation(_settings.GamePath);
                    ctx.ResourceProvider = installation;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameSession] Failed to create Installation for script context: {ex.Message}");
                }

                // Execute script
                int result = _vm.Execute(scriptBytes, ctx);
                Console.WriteLine($"[GameSession] Executed {eventType} script {scriptResRef}, result: {result}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameSession] Error executing {eventType} script {scriptResRef}: {ex.Message}");
                // Don't throw - script errors shouldn't crash the game
            }
        }

        /// <summary>
        /// Pause the game.
        /// </summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>
        /// Resume the game.
        /// </summary>
        public void Resume()
        {
            _isPaused = false;
        }

        /// <summary>
        /// Transition to another module.
        /// </summary>
        public void TransitionToModule(string moduleName, string waypointTag = null)
        {
            Console.WriteLine("[GameSession] Transitioning to: " + moduleName + " at waypoint: " + (waypointTag ?? "(default)"));

            // TODO: Save party state
            // TODO: Fade out
            
            // Load the new module
            LoadModule(moduleName);
            
            // Position player at destination waypoint if specified
            if (!string.IsNullOrEmpty(waypointTag))
            {
                PositionPlayerAtWaypoint(waypointTag);
            }
            
            // TODO: Fade in
        }

        /// <summary>
        /// Transitions to a different area within the current module.
        /// </summary>
        public void TransitionToArea(string areaResRef, string waypointTag)
        {
            if (_currentRuntimeModule == null)
            {
                Console.WriteLine("[GameSession] Cannot transition: no module loaded");
                return;
            }

            Console.WriteLine("[GameSession] Transitioning to area: " + areaResRef + " (waypoint: " + waypointTag + ")");

            // Get the destination area
            IArea destinationArea = _currentRuntimeModule.GetArea(areaResRef);
            if (destinationArea == null)
            {
                Console.WriteLine("[GameSession] Area not found: " + areaResRef);
                return;
            }

            // Update world's current area
            if (_world is World concreteWorld)
            {
                concreteWorld.SetCurrentArea(destinationArea);
            }

            // Position player at destination waypoint
            if (!string.IsNullOrEmpty(waypointTag))
            {
                PositionPlayerAtWaypoint(waypointTag);
            }
            else
            {
                // Use module's entry position if no waypoint specified
                if (_currentRuntimeModule != null)
                {
                    TransformComponent transform = _playerEntity?.GetComponent<Components.TransformComponent>();
                    if (transform != null)
                    {
                        transform.Position = _currentRuntimeModule.EntryPosition;
                        transform.Facing = (float)Math.Atan2(_currentRuntimeModule.EntryDirectionY, _currentRuntimeModule.EntryDirectionX);
                    }
                }
            }

            Console.WriteLine("[GameSession] Transitioned to area: " + areaResRef);
        }

        /// <summary>
        /// Positions the player at a waypoint by tag.
        /// </summary>
        private void PositionPlayerAtWaypoint(string waypointTag)
        {
            if (_playerEntity == null || _currentRuntimeModule == null)
            {
                return;
            }

            // Search all areas in the current module for the waypoint
            foreach (var area in _currentRuntimeModule.Areas)
            {
                IEntity waypoint = area.GetObjectByTag(waypointTag);
                if (waypoint != null)
                {
                    var transform = waypoint.GetComponent<Components.TransformComponent>();
                    if (transform != null)
                    {
                    var playerTransform = _playerEntity.GetComponent<Components.TransformComponent>();
                    if (playerTransform != null)
                    {
                        playerTransform.Position = transform.Position;
                        playerTransform.Facing = transform.Facing;
                        Console.WriteLine("[GameSession] Positioned player at waypoint: " + waypointTag);
                        return;
                    }
                    }
                }
            }

            Console.WriteLine("[GameSession] Waypoint not found: " + waypointTag);
        }

        /// <summary>
        /// Saves the current game state.
        /// </summary>
        public bool SaveGame(string saveName, SaveType saveType = SaveType.Manual)
        {
            if (_saveSystem == null)
            {
                return false;
            }

            return _saveSystem.Save(saveName, saveType);
        }

        /// <summary>
        /// Loads a saved game.
        /// </summary>
        public bool LoadGame(string saveName)
        {
            if (_saveSystem == null)
            {
                return false;
            }

            // Set loading flag before loading
            bool wasLoading = _isLoadingFromSave;
            _isLoadingFromSave = true;
            
            if (_vm != null && _vm.CurrentContext != null && _vm.CurrentContext.AdditionalContext is GameServicesContext services)
            {
                services.IsLoadingFromSave = true;
            }

            try
            {
                bool success = _saveSystem.Load(saveName);
                if (!success)
                {
                    return false;
                }

                SaveGameData saveData = _saveSystem.CurrentSave;
                if (saveData == null)
                {
                    return false;
                }

                // Load the module
                if (!string.IsNullOrEmpty(saveData.CurrentModule))
                {
                    LoadModule(saveData.CurrentModule);
                }

                // Restore player position
                if (saveData.EntryPosition != System.Numerics.Vector3.Zero && _playerEntity != null)
                {
                    ITransformComponent transform = _playerEntity.GetComponent<ITransformComponent>();
                    if (transform != null)
                    {
                        transform.Position = saveData.EntryPosition;
                        transform.Facing = saveData.EntryFacing;
                    }
                }

                // Restore global variables (handled by SaveSystem.ApplySaveData)

                return true;
            }
            finally
            {
                // Restore loading flag
                _isLoadingFromSave = wasLoading;
                if (_vm != null && _vm.CurrentContext != null && _vm.CurrentContext.AdditionalContext is GameServicesContext services2)
                {
                    services2.IsLoadingFromSave = wasLoading;
                }
            }
        }

        /// <summary>
        /// Registers all encounters in the current area with the encounter system.
        /// </summary>
        private void RegisterAllEncounters(IArea area)
        {
            if (_encounterSystem == null || area == null || _world == null)
            {
                return;
            }

            // Get all encounter entities from the world filtered by area
            IEnumerable<IEntity> allEncounters = _world.GetEntitiesOfType(ObjectType.Encounter);
            if (allEncounters != null)
            {
                int count = 0;
                foreach (IEntity encounter in allEncounters)
                {
                    // Check if encounter is in the current area
                    if (encounter.World != null && encounter.World.CurrentArea == area)
                    {
                        _encounterSystem.RegisterEncounter(encounter);
                        count++;
                    }
                }
                Console.WriteLine($"[GameSession] Registered {count} encounters");
            }
        }

        public void Dispose()
        {
            if (!string.IsNullOrEmpty(_currentModuleName))
            {
                UnloadCurrentModule();
            }
            _isRunning = false;
        }
    }

    /// <summary>
    /// Session settings extracted from game settings.
    /// </summary>
    public class GameSessionSettings
    {
        public KotorGameType Game { get; set; }
        public string GamePath { get; set; }
        public string StartModule { get; set; }

        public static GameSessionSettings FromGameSettings(object settings)
        {
            // Use reflection to extract settings from Odyssey.Game.Core.GameSettings
            // to avoid circular dependency
            var result = new GameSessionSettings();

            Type type = settings.GetType();
            System.Reflection.PropertyInfo gameProp = type.GetProperty("Game");
            System.Reflection.PropertyInfo pathProp = type.GetProperty("GamePath");
            System.Reflection.PropertyInfo moduleProp = type.GetProperty("StartModule");

            if (gameProp != null)
            {
                int gameValue = (int)gameProp.GetValue(settings);
                result.Game = gameValue == 0 ? KotorGameType.K1 : KotorGameType.K2;
            }

            if (pathProp != null)
            {
                result.GamePath = pathProp.GetValue(settings) as string;
            }

            if (moduleProp != null)
            {
                result.StartModule = moduleProp.GetValue(settings) as string;
            }

            return result;
        }
    }

    public enum KotorGameType
    {
        K1,
        K2
    }
}

