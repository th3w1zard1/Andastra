using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Module;
using Odyssey.Core.Navigation;
using Odyssey.Core.Party;
using Odyssey.Core.Actions;
using Odyssey.Core.Combat;
using Odyssey.Kotor.Combat;
using Odyssey.Kotor.Systems;
using Odyssey.Kotor.Dialogue;
using Odyssey.Kotor.Loading;
using Odyssey.Kotor.EngineApi;
using Odyssey.Scripting.VM;
using Odyssey.Scripting.Interfaces;
using CSharpKOTOR.Common;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using Odyssey.Core;
using Odyssey.Core.Journal;

namespace Odyssey.Kotor.Game
{
    /// <summary>
    /// Main game session manager that coordinates all game systems.
    /// </summary>
    /// <remarks>
    /// Game Session System:
    /// - Based on swkotor2.exe game session management
    /// - Located via string references: "GAMEINPROGRESS" @ 0x007c15c8 (game in progress flag), "GameSession" @ 0x007be620
    /// - "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58 (module state tracking)
    /// - "GameState" @ 0x007c15d0 (game state field), "GameMode" @ 0x007c15e0 (game mode field)
    /// - Coordinates: Module loading, entity management, script execution, combat, AI, triggers, dialogue, party
    /// - Game loop integration: Update() called every frame to update all systems (60 Hz fixed timestep)
    /// - Module transitions: Handles loading new modules and positioning player at entry waypoint
    /// - Script execution: Manages NCS VM and engine API integration (K1 vs K2 API based on game version)
    /// - System initialization order:
    ///   1. Installation/ModuleLoader setup
    ///   2. FactionManager (faction relationships)
    ///   3. PerceptionManager (perception system)
    ///   4. CombatManager (combat resolution)
    ///   5. PartySystem (party management)
    ///   6. Engine API (K1EngineApi or K2EngineApi based on game version)
    ///   7. ScriptExecutor (NCS VM execution)
    ///   8. TriggerSystem, AIController, DialogueManager, EncounterSystem
    /// - Entity serialization: FUN_005226d0 @ 0x005226d0 saves creature entity data to GFF (script hooks, inventory, perception, combat, position/orientation)
    /// - Based on game loop specification in monogame_odyssey_engine_e8927e4a.plan.md
    /// </remarks>
    public class GameSession
    {
        private readonly GameSettings _settings;
        private readonly World _world;
        private readonly NcsVm _vm;
        private readonly IScriptGlobals _globals;
        private readonly Installation _installation;
        private readonly ModuleLoader _moduleLoader;

        // Game systems
        private readonly TriggerSystem _triggerSystem;
        private readonly AIController _aiController;
        private readonly ModuleTransitionSystem _moduleTransitionSystem;
        private readonly DialogueManager _dialogueManager;
        private readonly FactionManager _factionManager;
        private readonly PerceptionManager _perceptionManager;
        private readonly CombatManager _combatManager;
        private readonly PartySystem _partySystem;
        private readonly ScriptExecutor _scriptExecutor;
        private readonly IEngineApi _engineApi;
        private readonly Systems.EncounterSystem _encounterSystem;
        private readonly JournalSystem _journalSystem;

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
                _moduleLoader = new ModuleLoader(_settings.GamePath, _world);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[GameSession] Failed to initialize installation: " + ex.Message);
                throw;
            }

            // Initialize game systems
            _factionManager = new FactionManager(_world);
            _perceptionManager = new PerceptionManager(_world, _world.EffectSystem);
            _combatManager = new CombatManager(_world, _factionManager);
            _partySystem = new PartySystem(_world);

            // Initialize engine API (K1 or K2 based on settings)
            _engineApi = _settings.Game == KotorGame.K1
                ? (IEngineApi)new Odyssey.Kotor.EngineApi.K1EngineApi()
                : (IEngineApi)new Odyssey.Kotor.EngineApi.K2EngineApi();

            _scriptExecutor = new ScriptExecutor(_vm, _world, _globals, _installation, _engineApi);

            // Initialize trigger system with script firing callback
            _triggerSystem = new TriggerSystem(_world, FireScriptEvent);

            // Initialize AI controller
            _aiController = new AIController(_world, FireScriptEvent);

            // Initialize dialogue manager
            _dialogueManager = new DialogueManager(
                _vm,
                _world,
                (resRef) => LoadDialogue(resRef),
                (resRef) => LoadScript(resRef),
                null, // voicePlayer
                null  // lipSyncController
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
                () => _currentModule != null ? new CSharpKOTOR.Common.Module(_currentModuleName, _installation) : null
            );

            // Subscribe to door opened events for module transitions
            _world.EventBus.Subscribe<DoorOpenedEvent>(OnDoorOpened);

            Console.WriteLine("[GameSession] Game session initialized");
        }
        
        /// <summary>
        /// Updates all game systems.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last update in seconds.</param>
        public void Update(float deltaTime)
        {
            if (_world == null)
            {
                return;
            }

            // Update world (time manager, delay scheduler, event bus)
            _world.Update(deltaTime);

            // Update trigger system (checks for entity entry/exit)
            if (_triggerSystem != null)
            {
                _triggerSystem.Update();
            }

            // Update AI controller (NPC behavior, heartbeats, combat)
            if (_aiController != null)
            {
                _aiController.Update(deltaTime);
            }

            // Update combat system
            if (_combatManager != null)
            {
                _combatManager.Update(deltaTime);
            }

            // Update perception system
            if (_perceptionManager != null)
            {
                _perceptionManager.Update(deltaTime);
            }

            // Update dialogue system
            if (_dialogueManager != null)
            {
                _dialogueManager.Update(deltaTime);
            }

            // Party system updates are handled by individual systems

            // Update encounter system (spawns creatures when triggered)
            if (_encounterSystem != null)
            {
                _encounterSystem.Update(deltaTime);
            }

            // Update module heartbeat (fires every 6 seconds)
            // Based on swkotor2.exe: Module heartbeat script execution
            // Located via string references: "Mod_OnHeartbeat" @ 0x007be840
            // Original implementation: Module heartbeat fires every 6 seconds for module-level scripts
            if (_currentModule != null)
            {
                _moduleHeartbeatTimer += deltaTime;
                if (_moduleHeartbeatTimer >= 6.0f)
                {
                    _moduleHeartbeatTimer -= 6.0f;
                    FireModuleHeartbeat();
                }
            }

            // Update all entities (action queues, transforms, etc.)
            foreach (IEntity entity in _world.GetAllEntities())
            {
                if (entity == null || !entity.IsValid)
                {
                    continue;
                }

                // Update action queues
                IActionQueueComponent actionQueue = entity.GetComponent<IActionQueueComponent>();
                if (actionQueue != null)
                {
                    actionQueue.Update(entity, deltaTime);
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

                // Load module (LoadModule sets _world.CurrentModule)
                _moduleLoader.LoadModule(moduleName);
                RuntimeModule module = _world.CurrentModule as RuntimeModule;
                if (module == null)
                {
                    Console.WriteLine("[GameSession] Module loader returned null for: " + moduleName);
                    return false;
                }

                // Set current module
                _currentModule = module;
                _currentModuleName = moduleName;
                _moduleTransitionSystem?.SetCurrentModule(moduleName);

                // Set world's current area
                if (!string.IsNullOrEmpty(module.EntryArea))
                {
                    IArea entryArea = module.GetArea(module.EntryArea);
                    if (entryArea != null)
                    {
                        _world.SetCurrentArea(entryArea);
                        
                        // Register all encounters in the area
                        if (_encounterSystem != null)
                        {
                            // IArea doesn't have GetAllEntities, but RuntimeArea does
                            if (entryArea is RuntimeArea runtimeArea)
                            {
                                foreach (IEntity entity in runtimeArea.GetAllEntities())
                                {
                                    if (entity != null && entity.ObjectType == Odyssey.Core.Enums.ObjectType.Encounter)
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
            _playerEntity = _world.CreateEntity(Odyssey.Core.Enums.ObjectType.Creature, entryPos, entryFacing);
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
        /// Fires a script event for an entity.
        /// </summary>
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
                // Create a temporary entity for module script execution
                moduleEntity = _world.CreateEntity(Odyssey.Core.Enums.ObjectType.Invalid, System.Numerics.Vector3.Zero, 0f);
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
        private CSharpKOTOR.Resource.Generics.DLG.DLG LoadDialogue(string resRef)
        {
            if (string.IsNullOrEmpty(resRef) || _installation == null)
            {
                return null;
            }

            try
            {
                CSharpKOTOR.Installation.ResourceResult resource = _installation.Resources.LookupResource(resRef, ResourceType.DLG);
                if (resource == null || resource.Data == null)
                {
                    return null;
                }

                return CSharpKOTOR.Resource.Generics.DLG.DLGHelper.ReadDlg(resource.Data);
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
                CSharpKOTOR.Installation.ResourceResult resource = _installation.Resources.LookupResource(resRef, ResourceType.NCS);
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
    }
}
