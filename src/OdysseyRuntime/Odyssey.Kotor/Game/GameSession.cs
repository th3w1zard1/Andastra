using System;
using System.IO;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Navigation;
using Odyssey.Scripting.Interfaces;
using Odyssey.Scripting.VM;
using Odyssey.Kotor.Dialogue;
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
    /// Manages the current game session - module loading, saves, party, etc.
    /// </summary>
    public class GameSession : IDisposable
    {
        private readonly GameSessionSettings _settings;
        private readonly World _world;
        private readonly NcsVm _vm;
        private readonly IScriptGlobals _globals;
        private readonly ModuleLoader _moduleLoader;
        private readonly DialogueManager _dialogueManager;

        private string _currentModuleName;
        private bool _isRunning;
        private bool _isPaused;
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
        /// Gets the dialogue manager.
        /// </summary>
        public DialogueManager DialogueManager
        {
            get { return _dialogueManager; }
        }

        public GameSession(object settings, World world, NcsVm vm, IScriptGlobals globals)
        {
            _settings = GameSessionSettings.FromGameSettings(settings);
            _world = world;
            _vm = vm;
            _globals = globals;
            _moduleLoader = new ModuleLoader(_settings.GamePath, _world);

            // Create dialogue manager
            _dialogueManager = new DialogueManager(
                vm,
                world,
                LoadDialogue,
                LoadScript
            );

            // Load talk tables
            LoadTalkTables();
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
            // TODO: Implement save loading
            Console.WriteLine("[GameSession] FIXME: Save loading not implemented - " + saveName);
            throw new NotImplementedException("Save loading not yet implemented");
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
                _moduleLoader.LoadModule(moduleName);
                _currentModuleName = moduleName;

                // Get navigation mesh from loaded module
                _currentNavMesh = _moduleLoader.GetNavigationMesh();

                // Fire module OnModuleLoad script
                // TODO: Execute module OnModuleLoad script

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

            // TODO: Fire OnModuleLeave scripts
            // TODO: Save persistent state
            // TODO: Clear entities

            _currentModuleName = null;
        }

        private void CreateDefaultPlayer()
        {
            // TODO: Create proper player entity from template
            Console.WriteLine("[GameSession] FIXME: Creating placeholder player entity");

            // Create a basic player entity
            _playerEntity = _world.CreateEntity(Core.Enums.ObjectType.Creature, System.Numerics.Vector3.Zero, 0);
            _playerEntity.Tag = "Player";

            // TODO: Add components (stats, inventory, etc.)
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
            // TODO: Process action queues

            // Process delayed commands
            // TODO: Process delay scheduler

            // Fire heartbeat scripts
            // TODO: Fire heartbeat every 6 seconds

            // Check perception
            // TODO: Perception system

            // Process dialogue
            // TODO: Dialogue state machine
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
            // TODO: Load module
            // TODO: Position party at waypoint
            // TODO: Fade in

            LoadModule(moduleName);
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

            var type = settings.GetType();
            var gameProp = type.GetProperty("Game");
            var pathProp = type.GetProperty("GamePath");
            var moduleProp = type.GetProperty("StartModule");

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

