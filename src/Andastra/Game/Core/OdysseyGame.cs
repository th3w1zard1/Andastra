using System;
using Andastra.Runtime.Core;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Engines.Odyssey.Game;
using Andastra.Game.Games.Odyssey;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Scripting.Interfaces;
using Andastra.Runtime.Scripting.VM;
using JetBrains.Annotations;

namespace Andastra.Game.Game.Core
{
    /// <summary>
    /// Main game wrapper for Odyssey Engine games (KOTOR 1/2).
    /// Coordinates graphics backend, engine initialization, and game session management.
    /// </summary>
    /// <remarks>
    /// Odyssey Game Wrapper:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FUN_00404250 @ 0x00404250 (WinMain equivalent, initializes game)
    /// - Original implementation: Initializes engine, creates game session, runs game loop
    /// - Graphics backend: Provides cross-platform graphics abstraction (MonoGame, Stride)
    /// - Game session: Manages all game systems (combat, dialogue, AI, scripts, etc.)
    /// - Game loop: Coordinates update/draw callbacks with graphics backend
    /// </remarks>
    public class OdysseyGame : IDisposable
    {
        private readonly GameSettings _settings;
        private readonly IGraphicsBackend _graphicsBackend;
        private GameSession _gameSession;
        private World _world;
        private NcsVm _vm;
        private IScriptGlobals _globals;
        private bool _disposed;
        private bool _initialized;

        /// <summary>
        /// Initializes a new instance of OdysseyGame.
        /// </summary>
        /// <param name="settings">Game settings including game path and configuration.</param>
        /// <param name="graphicsBackend">Graphics backend to use for rendering.</param>
        public OdysseyGame(GameSettings settings, IGraphicsBackend graphicsBackend)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (graphicsBackend == null)
            {
                throw new ArgumentNullException(nameof(graphicsBackend));
            }

            if (string.IsNullOrEmpty(settings.GamePath))
            {
                throw new ArgumentException("Game path cannot be null or empty", nameof(settings));
            }

            _settings = settings;
            _graphicsBackend = graphicsBackend;
        }

        /// <summary>
        /// Initializes the game systems and prepares for execution.
        /// </summary>
        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // Create engine-specific time manager
            var timeManager = new OdysseyTimeManager();

            // Create world instance with engine-specific time manager
            _world = new World(timeManager);

            // Create NCS VM for script execution
            _vm = new NcsVm();

            // Create script globals using factory pattern based on game type
            // Based on swkotor.exe and swkotor2.exe: Script globals system initializes global variables
            // Original implementation: Global variables initialized at game start based on game type
            // Factory pattern ensures correct globals instance is created (K1ScriptGlobals for K1, K2ScriptGlobals for K2)
            _globals = ScriptGlobalsFactory.Create(_settings.Game);

            // Create game session with all required dependencies
            _gameSession = new GameSession(_settings, _world, _vm, _globals);

            // Initialize graphics backend with settings
            string gameTitle = _settings.Game == KotorGame.K1
                ? "Star Wars: Knights of the Old Republic"
                : "Star Wars: Knights of the Old Republic II - The Sith Lords";

            // Set game path before initialization so content manager can use it
            if (_graphicsBackend is Andastra.Runtime.Graphics.Common.Backends.OdysseyGraphicsBackend odysseyBackend)
            {
                odysseyBackend.SetGamePath(_settings.GamePath);
            }

            _graphicsBackend.Initialize(
                _settings.Width,
                _settings.Height,
                gameTitle,
                _settings.Fullscreen);

            _initialized = true;
        }


        /// <summary>
        /// Runs the game loop (blocks until game exits).
        /// </summary>
        public void Run()
        {
            Initialize();

            // Set up update callback for game logic
            Action<float> updateAction = (deltaTime) =>
            {
                // Clear the screen BEFORE any rendering happens
                // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): glClear() happens before rendering (line 530 in World.cs comments)
                // Clear must happen before Update if rendering occurs during Update
                // This ensures the screen is cleared before any rendering operations
                if (_graphicsBackend != null && _graphicsBackend.GraphicsDevice != null)
                {
                    // Use dark blue color similar to KOTOR's main menu background
                    var clearColor = new Graphics.Color(0, 0, 32, 255); // Dark blue
                    _graphicsBackend.GraphicsDevice.Clear(clearColor);
                }

                if (_gameSession != null)
                {
                    _gameSession.Update(deltaTime);
                }
            };

            // Set up draw callback for rendering
            Action drawAction = () =>
            {
                // Rendering is handled by the graphics backend and renderer
                // The game session's Update method triggers rendering through the world/renderer systems
                // Screen clearing happens in updateAction before rendering begins
                // Additional render calls can be added here if needed
            };

            // Run game loop
            _graphicsBackend.Run(updateAction, drawAction);
        }

        /// <summary>
        /// Disposes of resources used by the game.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_gameSession != null)
                {
                    // Game session cleanup (if it implements IDisposable)
                    // Note: Check if GameSession needs explicit cleanup
                    _gameSession = null;
                }

                if (_vm != null)
                {
                    // NcsVm does not implement IDisposable
                    _vm = null;
                }

                if (_world != null)
                {
                    _world = null;
                }

                if (_graphicsBackend != null)
                {
                    _graphicsBackend.Dispose();
                }

                _disposed = true;
            }
        }
    }
}

