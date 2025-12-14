using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Odyssey.Game.GUI;
using Odyssey.Kotor.Game;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.VM;
using Odyssey.Core.Entities;
using JetBrains.Annotations;
using Game = Microsoft.Xna.Framework.Game;

namespace Odyssey.Game.Core
{
    /// <summary>
    /// MonoGame-based Odyssey game implementation.
    /// Simplified version focused on getting menu working and game launching.
    /// </summary>
    public class OdysseyGame : Microsoft.Xna.Framework.Game
    {
        private readonly GameSettings _settings;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;

        // Game systems
        private GameSession _session;
        private World _world;
        private ScriptGlobals _globals;
        private K1EngineApi _engineApi;
        private NcsVm _vm;

        // Menu
        private MenuRenderer _menuRenderer;
        private GameState _currentState = GameState.MainMenu;

        public OdysseyGame(GameSettings settings)
        {
            _settings = settings;
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window title
            Window.Title = "Odyssey Engine - " + (_settings.Game == KotorGame.K1 ? "Knights of the Old Republic" : "The Sith Lords");
        }

        protected override void Initialize()
        {
            Console.WriteLine("[Odyssey] Initializing MonoGame-based engine");

            // Initialize game systems
            _world = new World();
            _globals = new ScriptGlobals();
            _engineApi = new K1EngineApi();
            _vm = new NcsVm();
            _session = new GameSession(_settings, _world, _vm, _globals);

            base.Initialize();

            Console.WriteLine("[Odyssey] Core systems initialized");
        }

        protected override void LoadContent()
        {
            // Create SpriteBatch for rendering
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Try to load a font - if it doesn't exist, menu will still work without text labels
            // To add a font: Create Content/Fonts/Arial.spritefont using MonoGame Content Pipeline
            // Or use any TTF font and convert it using the Content Pipeline tool
            try
            {
                _font = Content.Load<SpriteFont>("Fonts/Arial");
                Console.WriteLine("[Odyssey] Font loaded successfully");
            }
            catch
            {
                // Font not found - menu will work but without text labels
                // Buttons are still fully functional (colored rectangles, clickable)
                Console.WriteLine("[Odyssey] WARNING: Font not found at 'Fonts/Arial'");
                Console.WriteLine("[Odyssey] Menu will work without text labels - buttons are still clickable");
                _font = null;
            }

            // Create menu renderer
            _menuRenderer = new MenuRenderer(GraphicsDevice, _font);

            // CRITICAL: Connect event handler BEFORE setting visible
            _menuRenderer.MenuItemSelected += OnMenuItemSelected;
            Console.WriteLine("[Odyssey] MenuItemSelected event handler connected");

            _menuRenderer.SetVisible(true);
            Console.WriteLine($"[Odyssey] Menu renderer visible: {_menuRenderer.IsVisible}");

            Console.WriteLine("[Odyssey] Menu renderer created and initialized");
            Console.WriteLine("[Odyssey] Content loaded");
        }

        protected override void Update(GameTime gameTime)
        {
            // Handle exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed ||
                Keyboard.GetState().IsKeyDown(Keys.Escape))
            {
                if (_currentState == GameState.MainMenu)
                {
                    Exit();
                }
                else
                {
                    // Return to main menu
                    _currentState = GameState.MainMenu;
                    if (_menuRenderer != null)
                    {
                        _menuRenderer.SetVisible(true);
                    }
                }
            }

            // Update menu if visible
            if (_currentState == GameState.MainMenu)
            {
                if (_menuRenderer != null)
                {
                    _menuRenderer.Update(gameTime, GraphicsDevice);
                }
                else
                {
                    // Log error only once per second to avoid spam
                    if (gameTime.TotalGameTime.TotalSeconds % 1.0 < 0.016)
                    {
                        Console.WriteLine("[Odyssey] ERROR: Menu renderer is null in Update!");
                    }
                }
            }

            // Update game systems if in game
            if (_currentState == GameState.InGame)
            {
                // TODO: Update game world, entities, etc.
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(20, 30, 60, 255)); // Dark blue background

            // Draw menu if in main menu state
            if (_currentState == GameState.MainMenu)
            {
                if (_menuRenderer != null)
                {
                    if (_menuRenderer.IsVisible)
                    {
                        _menuRenderer.Draw(gameTime, GraphicsDevice);
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: Menu renderer exists but is not visible!");
                    }
                }
                else
                {
                    Console.WriteLine("[Odyssey] ERROR: Menu renderer is null in Draw! Menu cannot be displayed.");
                }
            }

            // Draw game if in game state
            if (_currentState == GameState.InGame)
            {
                // TODO: Draw game world
                _spriteBatch.Begin();
                _spriteBatch.DrawString(_font ?? CreateDefaultFont(), "Game Running - Press ESC to return to menu",
                    new Vector2(10, 10), Color.White);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        private void OnMenuItemSelected(object sender, int menuIndex)
        {
            Console.WriteLine($"[Odyssey] ====== OnMenuItemSelected CALLED ======");
            Console.WriteLine($"[Odyssey] Menu item {menuIndex} selected");
            Console.WriteLine($"[Odyssey] Current game state: {_currentState}");

            switch (menuIndex)
            {
                case 0: // Start Game
                    Console.WriteLine("[Odyssey] Calling StartGame()...");
                    StartGame();
                    break;
                case 1: // Options
                    Console.WriteLine("[Odyssey] Options menu not implemented");
                    break;
                case 2: // Exit
                    Console.WriteLine("[Odyssey] Exiting game...");
                    Exit();
                    break;
                default:
                    Console.WriteLine($"[Odyssey] WARNING: Unknown menu index: {menuIndex}");
                    break;
            }
        }

        private void StartGame()
        {
            Console.WriteLine("[Odyssey] Starting game");

            // Use detected game path
            string gamePath = _settings.GamePath;
            if (string.IsNullOrEmpty(gamePath))
            {
                gamePath = GamePathDetector.DetectKotorPath(_settings.Game);
            }

            if (string.IsNullOrEmpty(gamePath))
            {
                Console.WriteLine("[Odyssey] ERROR: No game path detected!");
                return;
            }

            try
            {
                // Update settings with game path
                var updatedSettings = new GameSettings
                {
                    Game = _settings.Game,
                    GamePath = gamePath,
                    StartModule = "end_m01aa" // Default starting module
                };

                // Create new session
                _session = new GameSession(updatedSettings, _world, _vm, _globals);

                // Start the game session
                _session.StartNewGame();

                // Transition to in-game state
                _currentState = GameState.InGame;
                if (_menuRenderer != null)
                {
                    _menuRenderer.SetVisible(false);
                }

                Console.WriteLine("[Odyssey] Game started successfully");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Odyssey] Failed to start game: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
        }

        [CanBeNull]
        private SpriteFont CreateDefaultFont()
        {
            // Create a simple default font if none is loaded
            // This is a fallback - ideally we'd have a proper font file
            // For now, return null - text won't display but menu is still functional
            return null;
        }
    }
}

