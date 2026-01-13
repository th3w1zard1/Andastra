using System;
using BioWare.NET.Common;
using Andastra.Runtime.Core;
using Andastra.Game.Games.Common;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;
using JetBrains.Annotations;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Core
{
    /// <summary>
    /// Unified game launcher that supports all BioWare engine families.
    /// </summary>
    /// <remarks>
    /// Unified Game Launcher:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 0x00404250 @ 0x00404250 (WinMain equivalent, initializes game)
    /// - Original implementation: Determines game type, initializes engine, creates game session, runs game loop
    /// - This implementation: Unified launcher for all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Maps BioWareGame enum to appropriate engine instances via EngineFactory
    /// - Handles engine initialization, graphics backend setup, and game loop execution
    /// </remarks>
    public class UnifiedGameLauncher : IDisposable
    {
        private readonly BioWareGame _bioWareGame;
        private readonly string _gamePath;
        private readonly IGraphicsBackend _graphicsBackend;
        private readonly GameSettings _settings;
        private IEngine _engine;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the UnifiedGameLauncher.
        /// </summary>
        /// <param name="bioWareGame">The BioWare game to launch.</param>
        /// <param name="gamePath">Path to the game installation directory.</param>
        /// <param name="graphicsBackend">Graphics backend to use for rendering.</param>
        /// <param name="settings">Game settings (can be null for non-KOTOR games).</param>
        public UnifiedGameLauncher(
            BioWareGame bioWareGame,
            string gamePath,
            IGraphicsBackend graphicsBackend,
            [CanBeNull] GameSettings settings = null)
        {
            if (string.IsNullOrEmpty(gamePath))
            {
                throw new ArgumentException("Game path cannot be null or empty", nameof(gamePath));
            }

            if (graphicsBackend == null)
            {
                throw new ArgumentNullException(nameof(graphicsBackend));
            }

            _bioWareGame = bioWareGame;
            _gamePath = gamePath;
            _graphicsBackend = graphicsBackend;
            _settings = settings;
        }

        /// <summary>
        /// Initializes the engine and prepares for game execution.
        /// </summary>
        /// <exception cref="NotSupportedException">Thrown when the game type is not supported.</exception>
        /// <exception cref="InvalidOperationException">Thrown when initialization fails.</exception>
        public void Initialize()
        {
            if (_engine != null)
            {
                throw new InvalidOperationException("Launcher is already initialized");
            }

            // Create engine for the selected game
            try
            {
                _engine = EngineFactory.CreateEngine(_bioWareGame);
            }
            catch (NotSupportedException ex)
            {
                throw new NotSupportedException($"Game {_bioWareGame} is not yet fully supported: {ex.Message}", ex);
            }

            // Initialize engine with game installation path
            try
            {
                _engine.Initialize(_gamePath);
            }
            catch (Exception ex)
            {
                _engine = null;
                throw new InvalidOperationException($"Failed to initialize engine for {_bioWareGame}: {ex.Message}", ex);
            }

            // Initialize graphics backend
            // For Odyssey games, use settings for window configuration
            // For other engines, use default window settings
            int width = 1280;
            int height = 720;
            bool fullscreen = false;
            string title = GetGameTitle(_bioWareGame);

            if (_settings != null)
            {
                width = _settings.Width;
                height = _settings.Height;
                fullscreen = _settings.Fullscreen;
            }

            _graphicsBackend.Initialize(width, height, title, fullscreen);
        }

        /// <summary>
        /// Runs the game loop (blocks until game exits).
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when launcher is not initialized.</exception>
        public void Run()
        {
            if (_engine == null)
            {
                throw new InvalidOperationException("Launcher must be initialized before running");
            }

            // Create game session
            IEngineGame gameSession = _engine.CreateGameSession();

            // For Odyssey games, we need to integrate with the existing GameSession system
            // For other engines, use the engine's game session directly
            if (_bioWareGame.IsOdyssey() && _settings != null)
            {
                // Odyssey games use the existing GameSession class which requires special setup
                // This is handled by OdysseyGame wrapper class
                throw new InvalidOperationException("Odyssey games should use OdysseyGame class, not UnifiedGameLauncher directly");
            }

            // Set up update and draw callbacks for game loop
            Action<float> updateAction = (deltaTime) =>
            {
                // Update game session
                // Note: IEngineGame interface may need Update method, check interface definition
                // For now, this is a placeholder for the actual update logic
            };

            Action drawAction = () =>
            {
                // Render game session
                // Note: IEngineGame interface may need Draw method, check interface definition
                // For now, this is a placeholder for the actual rendering logic
            };

            // Run game loop
            _graphicsBackend.Run(updateAction, drawAction);
        }

        /// <summary>
        /// Gets a display-friendly title for the game.
        /// </summary>
        private static string GetGameTitle(BioWareGame bioWareGame)
        {
            if (bioWareGame.IsK1())
            {
                return "Star Wars: Knights of the Old Republic";
            }
            else if (bioWareGame.IsK2())
            {
                return "Star Wars: Knights of the Old Republic II - The Sith Lords";
            }
            else if (bioWareGame.IsDragonAgeOrigins())
            {
                return "Dragon Age: Origins";
            }
            else if (bioWareGame.IsDragonAge2())
            {
                return "Dragon Age II";
            }
            else if (bioWareGame.IsNWN1())
            {
                return "Neverwinter Nights";
            }
            else if (bioWareGame.IsNWN2())
            {
                return "Neverwinter Nights 2";
            }
            else if (bioWareGame.IsBaldursGate1())
            {
                return "Baldur's Gate";
            }
            else if (bioWareGame.IsBaldursGate2())
            {
                return "Baldur's Gate II";
            }
            else if (bioWareGame.IsIcewindDale1())
            {
                return "Icewind Dale";
            }
            else if (bioWareGame.IsIcewindDale2())
            {
                return "Icewind Dale II";
            }
            else if (bioWareGame.IsPlanescapeTorment())
            {
                return "Planescape: Torment";
            }
            else
            {
                return "Andastra Game Engine";
            }
        }

        /// <summary>
        /// Disposes of resources used by the launcher.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_engine != null)
                {
                    _engine.Shutdown();
                    _engine = null;
                }

                _disposed = true;
            }
        }
    }
}
