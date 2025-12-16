using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using JetBrains.Annotations;
using AuroraEngine.Common;
using AuroraEngine.Common.Formats.MDL;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Resources;
using Odyssey.Core.Entities;
using Odyssey.Core.Enums;
using Odyssey.Core.Interfaces;
using Odyssey.Graphics;
using Odyssey.Kotor.EngineApi;
using Odyssey.Kotor.Game;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.VM;
using Color = Odyssey.Graphics.Color;
using Rectangle = Odyssey.Graphics.Rectangle;
using Vector2 = Odyssey.Graphics.Vector2;

namespace Odyssey.Game.Core
{
    /// <summary>
    /// Odyssey game implementation using graphics abstraction layer.
    /// Supports both MonoGame and Stride backends.
    /// Simplified version focused on getting menu working and game launching.
    /// </summary>
    /// <remarks>
    /// Odyssey Game (Graphics Abstraction Implementation):
    /// - Based on swkotor2.exe: FUN_00404250 @ 0x00404250 (main game loop, WinMain equivalent)
    /// - Main loop structure: while (DAT_00828390 == 0) { PeekMessageA, update game, render, SwapBuffers }
    /// - Located via string references: "UpdateScenes" @ 0x007b8b54 (referenced by FUN_00452060, FUN_0045f960, FUN_004cbe40)
    /// - "update" @ 0x007bab3c (update constant), "GameObjUpdate" @ 0x007c246c (game object update)
    /// - "ForceAlwaysUpdate" @ 0x007bf5b4 (force always update flag, saved by FUN_005226d0 @ 0x005226d0)
    /// - Rendering: "DRAWSTYLE" @ 0x007b63d4 (draw style constant), "DRAWMODE" @ 0x007b6a4c (draw mode constant)
    /// - "glDrawArrays" @ 0x0080aab6, "glDrawElements" @ 0x0080aafe, "glDrawBuffer" @ 0x0080ac4e (OpenGL draw functions)
    /// - "mgs_drawmain" @ 0x007cc8f0 (main draw function), "hologram_donotdraw" @ 0x007bae78 (hologram don't draw flag)
    /// - Game loop phases (FUN_00404250): Message processing (PeekMessageA), game update (FUN_00638ca0), rendering (glClear, FUN_00461c20/FUN_00461c00), SwapBuffers
    /// - Entity serialization: FUN_005226d0 @ 0x005226d0 saves creature entity data to GFF (script hooks, inventory, perception, combat, position/orientation)
    /// - Update() called every frame (60 Hz fixed timestep), Draw() renders frame
    /// - Original implementation: Main loop processes Windows messages, updates game state, renders frame, swaps buffers
    /// - Graphics abstraction: Uses IGraphicsBackend for backend-agnostic rendering (MonoGame or Stride)
    /// </remarks>
    public class OdysseyGame : IDisposable
    {
        private readonly Odyssey.Core.GameSettings _settings;
        private readonly IGraphicsBackend _graphicsBackend;
        private IGraphicsDevice _graphicsDevice;
        private ISpriteBatch _spriteBatch;
        private IFont _font;

        // Game systems
        private GameSession _session;
        private World _world;
        private ScriptGlobals _globals;
        private K1EngineApi _engineApi;
        private NcsVm _vm;

        // Menu - Professional menu implementation
        private GameState _currentState = GameState.MainMenu;
        private int _selectedMenuIndex = 0;
        private readonly string[] _menuItems = { "Start Game", "Options", "Exit" };
        private ITexture2D _menuTexture; // 1x1 white texture for drawing rectangles
        private IKeyboardState _previousMenuKeyboardState;
        private IMouseState _previousMenuMouseState;
        private float _menuAnimationTime = 0f; // For smooth animations
        private int _hoveredMenuIndex = -1; // Track mouse hover

        // Installation path selection
        private List<string> _availablePaths = new List<string>();
        private int _selectedPathIndex = 0;
        private bool _isSelectingPath = false;

        // Basic 3D rendering (using abstraction layer)
        private IBasicEffect _basicEffect;
        private IVertexBuffer _groundVertexBuffer;
        private IIndexBuffer _groundIndexBuffer;
        private System.Numerics.Matrix4x4 _viewMatrix;
        private System.Numerics.Matrix4x4 _projectionMatrix;
        private float _cameraAngle = 0f;

        // Room rendering (using abstraction layer)
        private IRoomMeshRenderer _roomRenderer;
        private Dictionary<string, IRoomMeshData> _roomMeshes;

        // Entity model rendering (using abstraction layer)
        private IEntityModelRenderer _entityModelRenderer;

        // Save/Load system
        private Odyssey.Core.Save.SaveSystem _saveSystem;
        private List<Odyssey.Core.Save.SaveGameInfo> _availableSaves;
        private int _selectedSaveIndex = 0;
        private bool _isSaving = false;
        private string _newSaveName = string.Empty;

        // Input tracking
        private IMouseState _previousMouseState;
        private IKeyboardState _previousKeyboardState;

        public OdysseyGame(Odyssey.Core.GameSettings settings, IGraphicsBackend graphicsBackend)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _graphicsBackend = graphicsBackend ?? throw new ArgumentNullException(nameof(graphicsBackend));

            // Initialize graphics backend
            string windowTitle = "Odyssey Engine - " + (_settings.Game == Odyssey.Core.KotorGame.K1 ? "Knights of the Old Republic" : "The Sith Lords");
            _graphicsBackend.Initialize(1280, 720, windowTitle, false);

            // Get graphics components from backend
            _graphicsDevice = _graphicsBackend.GraphicsDevice;
            _graphicsBackend.Window.IsMouseVisible = true;

            Console.WriteLine("[Odyssey] Game window initialized - Backend: " + _graphicsBackend.BackendType);
        }

        private void Initialize()
        {
            Console.WriteLine("[Odyssey] Initializing engine with backend: " + _graphicsBackend.BackendType);

            // Initialize game systems
            _world = new World();
            _globals = new ScriptGlobals();
            _engineApi = new K1EngineApi();
            _vm = new NcsVm();
            _session = new GameSession(_settings, _world, _vm, _globals);

            // Initialize input state
            _previousMouseState = _graphicsBackend.InputManager.MouseState;
            _previousKeyboardState = _graphicsBackend.InputManager.KeyboardState;

            Console.WriteLine("[Odyssey] Core systems initialized");
        }

        private void LoadContent()
        {
            // Create SpriteBatch for rendering
            _spriteBatch = _graphicsDevice.CreateSpriteBatch();

            // Load font with comprehensive error handling
            try
            {
                _font = _graphicsBackend.ContentManager.Load<IFont>("Fonts/Arial");
                Console.WriteLine("[Odyssey] Font loaded successfully from 'Fonts/Arial'");
            }
            catch (Exception ex)
            {
                // Font not found - this is a critical issue for menu display
                Console.WriteLine("[Odyssey] ERROR: Failed to load font from 'Fonts/Arial': " + ex.Message);
                Console.WriteLine("[Odyssey] The font file must be built by the content pipeline");
                _font = null;
            }

            // Create 1x1 white texture for menu drawing
            byte[] whitePixel = new byte[] { 255, 255, 255, 255 }; // RGBA white
            _menuTexture = _graphicsDevice.CreateTexture2D(1, 1, whitePixel);

            // Initialize menu input states
            _previousMenuKeyboardState = _graphicsBackend.InputManager.KeyboardState;
            _previousMenuMouseState = _graphicsBackend.InputManager.MouseState;

            // Initialize installation path selection
            InitializeInstallationPaths();

            Console.WriteLine("[Odyssey] Menu system initialized");

            // Initialize game rendering
            InitializeGameRendering();

            // Initialize room renderer using abstraction layer
            _roomRenderer = _graphicsBackend.CreateRoomMeshRenderer();
            _roomMeshes = new Dictionary<string, IRoomMeshData>();

            // Initialize entity model renderer using abstraction layer
            // Will be created when module loads with proper dependencies
            _entityModelRenderer = null;

            Console.WriteLine("[Odyssey] Content loaded");
        }

        private void Update(float deltaTime)
        {
            var inputManager = _graphicsBackend.InputManager;
            var keyboardState = inputManager.KeyboardState;
            var mouseState = inputManager.MouseState;

            // Handle exit
            if (keyboardState.IsKeyDown(Keys.Escape))
            {
                if (_currentState == GameState.MainMenu)
                {
                    _graphicsBackend.Exit();
                    return;
                }
                else if (_currentState == GameState.SaveMenu || _currentState == GameState.LoadMenu)
                {
                    // Return to game from save/load menu
                    _currentState = GameState.InGame;
                }
                else
                {
                    // Return to main menu
                    _currentState = GameState.MainMenu;
                }
            }

            // Update menu if visible
            if (_currentState == GameState.MainMenu)
            {
                _menuAnimationTime += deltaTime;
                UpdateMainMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_currentState == GameState.SaveMenu)
            {
                UpdateSaveMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_currentState == GameState.LoadMenu)
            {
                UpdateLoadMenu(deltaTime, keyboardState, mouseState);
            }

            // Update game systems if in game
            if (_currentState == GameState.InGame)
            {
                // Handle save/load shortcuts
                if (keyboardState.IsKeyDown(Keys.F5) && !_previousKeyboardState.IsKeyDown(Keys.F5))
                {
                    // Quick save
                    QuickSave();
                }
                if (keyboardState.IsKeyDown(Keys.F9) && !_previousKeyboardState.IsKeyDown(Keys.F9))
                {
                    // Quick load
                    QuickLoad();
                }
                if (keyboardState.IsKeyDown(Keys.S) && keyboardState.IsKeyDown(Keys.LeftControl) &&
                    !_previousKeyboardState.IsKeyDown(Keys.S))
                {
                    // Ctrl+S - Open save menu
                    OpenSaveMenu();
                }
                if (keyboardState.IsKeyDown(Keys.L) && keyboardState.IsKeyDown(Keys.LeftControl) &&
                    !_previousKeyboardState.IsKeyDown(Keys.L))
                {
                    // Ctrl+L - Open load menu
                    OpenLoadMenu();
                }

                // Update game session
                if (_session != null)
                {
                    _session.Update(deltaTime);

                    // Handle player input for movement and interaction
                    HandlePlayerInput(keyboardState, mouseState, new GameTime { ElapsedGameTime = System.TimeSpan.FromSeconds(deltaTime), TotalGameTime = System.TimeSpan.Zero });
                }

                // Update camera to follow player
                UpdateCamera();
            }

            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        private void Draw()
        {
            // Draw menu if in main menu state
            if (_currentState == GameState.MainMenu)
            {
                DrawMainMenu();
            }
            else if (_currentState == GameState.SaveMenu)
            {
                DrawSaveMenu();
            }
            else if (_currentState == GameState.LoadMenu)
            {
                DrawLoadMenu();
            }
            else if (_currentState == GameState.InGame)
            {
                DrawGameWorld();
            }
            else
            {
                // Fallback: clear to black
                _graphicsDevice.Clear(new Color(0, 0, 0, 255));
            }
        }

        public void Run()
        {
            Initialize();
            LoadContent();

            // Run the game loop using the graphics backend
            _graphicsBackend.Run(Update, Draw);
        }

        /// <summary>
        /// Updates the main menu state and handles input.
        /// Professional menu implementation with keyboard and mouse support.
        /// </summary>
        private void UpdateMainMenu(float deltaTime, IKeyboardState keyboardState, IMouseState mouseState)
        {
            int viewportWidth = _graphicsDevice.Viewport.Width;
            int viewportHeight = _graphicsDevice.Viewport.Height;

            // Calculate menu button positions (matching DrawMainMenu layout)
            int centerX = viewportWidth / 2;
            int startY = viewportHeight / 2;
            int buttonWidth = 400;
            int buttonHeight = 60;
            int buttonSpacing = 15;
            int titleOffset = 180;

            // Track mouse hover
            _hoveredMenuIndex = -1;
            Point mousePos = currentMouseState.Position;

            // Check which button the mouse is over
            for (int i = 0; i < _menuItems.Length; i++)
            {
                int buttonY = startY - titleOffset + i * (buttonHeight + buttonSpacing);
                Rectangle buttonRect = new Rectangle(centerX - buttonWidth / 2, buttonY, buttonWidth, buttonHeight);

                if (buttonRect.Contains(mousePos))
                {
                    _hoveredMenuIndex = i;
                    _selectedMenuIndex = i; // Update selection on hover
                    break;
                }
            }

            // Handle path selection navigation
            if (_isSelectingPath)
            {
                if (IsKeyJustPressed(_previousMenuKeyboardState, currentKeyboardState, Keys.Up))
                {
                    _selectedPathIndex = (_selectedPathIndex - 1 + _availablePaths.Count) % _availablePaths.Count;
                }

                if (IsKeyJustPressed(_previousMenuKeyboardState, currentKeyboardState, Keys.Down))
                {
                    _selectedPathIndex = (_selectedPathIndex + 1) % _availablePaths.Count;
                }

                // ESC to cancel path selection
                if (IsKeyJustPressed(_previousMenuKeyboardState, currentKeyboardState, Keys.Escape))
                {
                    _isSelectingPath = false;
                }
            }
            else
            {
                // Keyboard navigation
                if (IsKeyJustPressed(_previousMenuKeyboardState, currentKeyboardState, Keys.Up))
                {
                    _selectedMenuIndex = (_selectedMenuIndex - 1 + _menuItems.Length) % _menuItems.Length;
                }

                if (IsKeyJustPressed(_previousMenuKeyboardState, currentKeyboardState, Keys.Down))
                {
                    _selectedMenuIndex = (_selectedMenuIndex + 1) % _menuItems.Length;
                }
            }

            // Select menu item
            if (IsKeyJustPressed(_previousMenuKeyboardState, currentKeyboardState, Keys.Enter) ||
                IsKeyJustPressed(_previousMenuKeyboardState, currentKeyboardState, Keys.Space))
            {
                if (_isSelectingPath)
                {
                    // Confirm path selection
                    if (_selectedPathIndex >= 0 && _selectedPathIndex < _availablePaths.Count)
                    {
                        _settings.GamePath = _availablePaths[_selectedPathIndex];
                        _isSelectingPath = false;
                        StartGame();
                    }
                }
                else
                {
                    HandleMenuSelection(_selectedMenuIndex);
                }
            }

            // Mouse click
            if (currentMouseState.LeftButton == ButtonState.Pressed &&
                _previousMenuMouseState.LeftButton == ButtonState.Released)
            {
                if (_hoveredMenuIndex >= 0 && _hoveredMenuIndex < _menuItems.Length)
                {
                    HandleMenuSelection(_hoveredMenuIndex);
                }
            }

            _previousMenuKeyboardState = currentKeyboardState;
            _previousMenuMouseState = currentMouseState;
        }

        private bool IsKeyJustPressed(IKeyboardState previous, IKeyboardState current, Keys key)
        {
            return previous.IsKeyUp(key) && current.IsKeyDown(key);
        }

        private void HandleMenuSelection(int menuIndex)
        {
            switch (menuIndex)
            {
                case 0: // Start Game
                    if (_isSelectingPath)
                    {
                        // Confirm path selection and start game
                        if (_selectedPathIndex >= 0 && _selectedPathIndex < _availablePaths.Count)
                        {
                            _settings.GamePath = _availablePaths[_selectedPathIndex];
                            _isSelectingPath = false;
                            StartGame();
                        }
                    }
                    else
                    {
                        // Toggle path selection mode
                        _isSelectingPath = true;
                    }
                    break;
                case 1: // Options
                    Console.WriteLine("[Odyssey] Options menu not implemented");
                    break;
                case 2: // Exit
                    Exit();
                    break;
            }
        }

        /// <summary>
        /// Initializes available installation paths.
        /// </summary>
        private void InitializeInstallationPaths()
        {
            // Get paths from GamePathDetector
            _availablePaths = GamePathDetector.FindKotorPathsFromDefault(_settings.Game);

            // If no paths found, try single detection
            if (_availablePaths.Count == 0)
            {
                string detectedPath = GamePathDetector.DetectKotorPath(_settings.Game);
                if (!string.IsNullOrEmpty(detectedPath))
                {
                    _availablePaths.Add(detectedPath);
                }
            }

            // If we have a path from settings, use it and add to list if not present
            if (!string.IsNullOrEmpty(_settings.GamePath))
            {
                if (!_availablePaths.Contains(_settings.GamePath))
                {
                    _availablePaths.Insert(0, _settings.GamePath);
                }
                _selectedPathIndex = _availablePaths.IndexOf(_settings.GamePath);
            }
            else if (_availablePaths.Count > 0)
            {
                // Use first available path
                _selectedPathIndex = 0;
                _settings.GamePath = _availablePaths[0];
            }

            Console.WriteLine($"[Odyssey] Found {_availablePaths.Count} installation path(s)");
            foreach (string path in _availablePaths)
            {
                Console.WriteLine($"[Odyssey]   - {path}");
            }
        }

        /// <summary>
        /// Draws a professional main menu with proper text rendering, shadows, and visual effects.
        /// </summary>
        private void DrawMainMenu()
        {
            // Clear to dark space-like background (deep blue/black gradient effect)
            _graphicsDevice.Clear(new Color(15, 15, 25, 255));

            // Begin sprite batch rendering
            _spriteBatch.Begin();

            int viewportWidth = _graphicsDevice.Viewport.Width;
            int viewportHeight = _graphicsDevice.Viewport.Height;
            int centerX = viewportWidth / 2;
            int centerY = viewportHeight / 2;

            // Menu layout constants
            int titleOffset = 180;
            int buttonWidth = 400;
            int buttonHeight = 60;
            int buttonSpacing = 15;
            int startY = centerY;

            // Draw background gradient effect (subtle)
            Rectangle backgroundRect = new Rectangle(0, 0, viewportWidth, viewportHeight);
            _spriteBatch.Draw(_menuTexture, backgroundRect, new Color(20, 20, 35, 255));

            // Draw title with shadow effect
            // Shadow rendering: draw text twice - once offset for shadow, once for main text
            if (_font != null)
            {
                string title = "ODYSSEY ENGINE";
                string subtitle = _settings.Game == Odyssey.Core.KotorGame.K1
                    ? "Knights of the Old Republic"
                    : "The Sith Lords";
                string version = "Demo Build";

                // Title - large with shadow
                Vector2 titleSize = _font.MeasureString(title);
                Vector2 titlePos = new Vector2(centerX - titleSize.X / 2, startY - titleOffset - 80);

                // Draw shadow first (offset by 3 pixels)
                _spriteBatch.DrawString(_font, title, titlePos + new Vector2(3, 3), new Color(0, 0, 0, 180));
                // Draw main title text
                _spriteBatch.DrawString(_font, title, titlePos, new Color(255, 215, 0, 255)); // Gold color

                // Subtitle
                Vector2 subtitleSize = _font.MeasureString(subtitle);
                Vector2 subtitlePos = new Vector2(centerX - subtitleSize.X / 2, titlePos.Y + titleSize.Y + 10);
                _spriteBatch.DrawString(_font, subtitle, subtitlePos + new Vector2(2, 2), new Color(0, 0, 0, 150));
                _spriteBatch.DrawString(_font, subtitle, subtitlePos, new Color(200, 200, 220, 255));

                // Version label
                Vector2 versionSize = _font.MeasureString(version);
                Vector2 versionPos = new Vector2(centerX - versionSize.X / 2, subtitlePos.Y + subtitleSize.Y + 15);
                _spriteBatch.DrawString(_font, version, versionPos, new Color(150, 150, 150, 255));
            }
            else
            {
                // Fallback: draw professional title logo
                // Draw a stylized "O" symbol for Odyssey with smooth rendering
                int titleSize = 100;
                int titleX = centerX - titleSize / 2;
                int titleY = startY - titleOffset - 80;

                // Add subtle glow effect (outermost, semi-transparent)
                Rectangle glowRing = new Rectangle(titleX - 4, titleY - 4, titleSize + 8, titleSize + 8);
                DrawFilledCircle(_spriteBatch, glowRing, new Color(255, 215, 0, 40));

                // Outer ring (gold) - filled circle with border
                Rectangle outerRing = new Rectangle(titleX, titleY, titleSize, titleSize);
                DrawFilledCircle(_spriteBatch, outerRing, new Color(255, 215, 0, 255));
                DrawRoundedRectangle(_spriteBatch, outerRing, 6, new Color(255, 200, 0, 255));

                // Inner ring (hollow) - creates elegant "O" shape by clearing center
                int innerSize = titleSize - 32;
                Rectangle innerRing = new Rectangle(titleX + 16, titleY + 16, innerSize, innerSize);
                DrawFilledCircle(_spriteBatch, innerRing, new Color(15, 15, 25, 255)); // Clear with background color
                DrawRoundedRectangle(_spriteBatch, innerRing, 4, new Color(255, 215, 0, 220));

                Console.WriteLine("[Odyssey] WARNING: Font not available - using visual fallback indicators");
            }

            // Draw menu buttons with professional styling
            for (int i = 0; i < _menuItems.Length; i++)
            {
                int buttonY = startY - titleOffset + i * (buttonHeight + buttonSpacing);
                Rectangle buttonRect = new Rectangle(centerX - buttonWidth / 2, buttonY, buttonWidth, buttonHeight);

                // Determine if button is selected or hovered
                bool isSelected = (i == _selectedMenuIndex);
                bool isHovered = (_hoveredMenuIndex == i);

                // Button colors with smooth transitions
                Color buttonBgColor;
                Color buttonBorderColor;
                Color buttonTextColor = new Color(255, 255, 255, 255);
                float buttonScale = 1.0f;

                if (isSelected || isHovered)
                {
                    // Selected/hovered: bright blue with white border
                    buttonBgColor = new Color(60, 120, 200, 255);
                    buttonBorderColor = new Color(255, 255, 255, 255);
                    buttonScale = 1.05f; // Slightly larger when selected
                }
                else
                {
                    // Normal: dark blue-gray
                    buttonBgColor = new Color(40, 50, 70, 220);
                    buttonBorderColor = new Color(100, 120, 150, 255);
                }

                // Apply scale to button (center the scaling)
                int scaledWidth = (int)(buttonWidth * buttonScale);
                int scaledHeight = (int)(buttonHeight * buttonScale);
                int scaledX = centerX - scaledWidth / 2;
                int scaledY = buttonY - (scaledHeight - buttonHeight) / 2;
                Rectangle scaledButtonRect = new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight);

                // Draw button shadow (subtle)
                Rectangle shadowRect = new Rectangle(scaledButtonRect.X + 4, scaledButtonRect.Y + 4, scaledButtonRect.Width, scaledButtonRect.Height);
                _spriteBatch.Draw(_menuTexture, shadowRect, new Color(0, 0, 0, 100));

                // Draw button background
                _spriteBatch.Draw(_menuTexture, scaledButtonRect, buttonBgColor);

                // Draw button border (thicker when selected)
                int borderThickness = isSelected ? 4 : 3;
                DrawRectangleBorder(_spriteBatch, scaledButtonRect, borderThickness, buttonBorderColor);

                // Draw button text with shadow
                if (_font != null)
                {
                    Vector2 textSize = _font.MeasureString(_menuItems[i]);
                    Vector2 textPos = new Vector2(
                        scaledButtonRect.X + (scaledButtonRect.Width - textSize.X) / 2,
                        scaledButtonRect.Y + (scaledButtonRect.Height - textSize.Y) / 2
                    );

                    // Draw text shadow
                    _spriteBatch.DrawString(_font, _menuItems[i], textPos + new Vector2(2, 2), new Color(0, 0, 0, 200));
                    // Draw main text
                    _spriteBatch.DrawString(_font, _menuItems[i], textPos, buttonTextColor);
                }
                else
                {
                    // Fallback: draw professional icons for each button
                    int iconSize = 36;
                    int iconX = scaledButtonRect.X + (scaledButtonRect.Width - iconSize) / 2;
                    int iconY = scaledButtonRect.Y + (scaledButtonRect.Height - iconSize) / 2;
                    Rectangle iconRect = new Rectangle(iconX, iconY, iconSize, iconSize);

                    Color iconColor = isSelected ? Color.White : new Color(220, 220, 220, 255);

                    if (i == 0)
                    {
                        // Start Game: Professional play triangle (right-pointing, centered)
                        int padding = 10;
                        int[] triangleX = { iconRect.X + padding, iconRect.X + padding, iconRect.X + iconSize - padding };
                        int[] triangleY = { iconRect.Y + padding, iconRect.Y + iconSize - padding, iconRect.Y + iconSize / 2 };
                        DrawFilledTriangle(_spriteBatch, triangleX, triangleY, iconColor);
                        // Add subtle border for definition
                        DrawTriangleOutline(_spriteBatch, triangleX, triangleY, new Color(iconColor.R / 2, iconColor.G / 2, iconColor.B / 2, iconColor.A));
                    }
                    else if (i == 1)
                    {
                        // Options: Professional gear/settings icon (rounded square with plus)
                        int padding = 8;
                        Rectangle gearRect = new Rectangle(iconRect.X + padding, iconRect.Y + padding, iconSize - padding * 2, iconSize - padding * 2);
                        DrawRoundedRectangle(_spriteBatch, gearRect, 3, iconColor);
                        // Draw plus sign in center
                        int plusThickness = 3;
                        int plusSize = iconSize / 3;
                        int plusX = iconRect.X + (iconSize - plusThickness) / 2;
                        int plusY = iconRect.Y + (iconSize - plusSize) / 2;
                        // Horizontal bar
                        _spriteBatch.Draw(_menuTexture, new Rectangle(plusX - plusSize / 2, plusY + plusSize / 2 - plusThickness / 2, plusSize, plusThickness), iconColor);
                        // Vertical bar
                        _spriteBatch.Draw(_menuTexture, new Rectangle(plusX - plusThickness / 2, plusY, plusThickness, plusSize), iconColor);
                    }
                    else if (i == 2)
                    {
                        // Exit: Professional X/close icon (diagonal lines)
                        int padding = 10;
                        int thickness = 4;
                        // Top-left to bottom-right
                        DrawDiagonalLine(_spriteBatch,
                            iconRect.X + padding, iconRect.Y + padding,
                            iconRect.X + iconSize - padding, iconRect.Y + iconSize - padding,
                            thickness, iconColor);
                        // Top-right to bottom-left
                        DrawDiagonalLine(_spriteBatch,
                            iconRect.X + iconSize - padding, iconRect.Y + padding,
                            iconRect.X + padding, iconRect.Y + iconSize - padding,
                            thickness, iconColor);
                    }
                }
            }

            // Draw installation path selector if in path selection mode
            if (_isSelectingPath && _availablePaths.Count > 0)
            {
                int pathSelectorY = startY - titleOffset - 100;
                int pathSelectorWidth = 600;
                int pathSelectorHeight = 40;
                int pathItemHeight = 35;
                int maxVisiblePaths = 5;

                // Draw path selector background
                Rectangle pathSelectorRect = new Rectangle(centerX - pathSelectorWidth / 2, pathSelectorY, pathSelectorWidth, pathSelectorHeight + (Math.Min(_availablePaths.Count, maxVisiblePaths) * pathItemHeight));
                _spriteBatch.Draw(_menuTexture, pathSelectorRect, new Color(30, 30, 45, 240));
                DrawRectangleBorder(_spriteBatch, pathSelectorRect, 2, new Color(100, 120, 150, 255));

                // Draw title
                if (_font != null)
                {
                    string pathTitle = "Select Installation Path:";
                    Vector2 titleSize = _font.MeasureString(pathTitle);
                    Vector2 titlePos = new Vector2(centerX - titleSize.X / 2, pathSelectorY + 5);
                    _spriteBatch.DrawString(_font, pathTitle, titlePos + new Vector2(1, 1), new Color(0, 0, 0, 150));
                    _spriteBatch.DrawString(_font, pathTitle, titlePos, new Color(200, 200, 220, 255));
                }

                // Draw path items
                int startIndex = Math.Max(0, _selectedPathIndex - maxVisiblePaths / 2);
                int endIndex = Math.Min(_availablePaths.Count, startIndex + maxVisiblePaths);

                for (int i = startIndex; i < endIndex; i++)
                {
                    int itemY = pathSelectorY + pathSelectorHeight + (i - startIndex) * pathItemHeight;
                    bool isSelected = (i == _selectedPathIndex);

                    // Draw item background
                    Rectangle itemRect = new Rectangle(centerX - pathSelectorWidth / 2 + 10, itemY, pathSelectorWidth - 20, pathItemHeight - 5);
                    Color itemBgColor = isSelected ? new Color(60, 120, 200, 255) : new Color(40, 50, 70, 200);
                    _spriteBatch.Draw(_menuTexture, itemRect, itemBgColor);

                    if (isSelected)
                    {
                        DrawRectangleBorder(_spriteBatch, itemRect, 2, Color.White);
                    }

                    // Draw path text
                    if (_font != null)
                    {
                        string pathText = _availablePaths[i];
                        // Truncate if too long
                        Vector2 textSize = _font.MeasureString(pathText);
                        if (textSize.X > itemRect.Width - 20)
                        {
                            // Truncate with ellipsis
                            while (textSize.X > itemRect.Width - 40 && pathText.Length > 0)
                            {
                                pathText = pathText.Substring(0, pathText.Length - 1);
                                textSize = _font.MeasureString(pathText + "...");
                            }
                            pathText = pathText + "...";
                        }

                        Vector2 textPos = new Vector2(itemRect.X + 10, itemRect.Y + (itemRect.Height - textSize.Y) / 2);
                        _spriteBatch.DrawString(_font, pathText, textPos + new Vector2(1, 1), new Color(0, 0, 0, 200));
                        _spriteBatch.DrawString(_font, pathText, textPos, isSelected ? Color.White : new Color(180, 180, 200, 255));
                    }
                }

                // Draw instructions
                if (_font != null)
                {
                    string instructions = "Arrow Keys: Navigate  |  Enter: Select  |  ESC: Cancel";
                    Vector2 instSize = _font.MeasureString(instructions);
                    Vector2 instPos = new Vector2(centerX - instSize.X / 2, pathSelectorY + pathSelectorHeight + (Math.Min(_availablePaths.Count, maxVisiblePaths) * pathItemHeight) + 10);
                    _spriteBatch.DrawString(_font, instructions, instPos + new Vector2(1, 1), new Color(0, 0, 0, 150));
                    _spriteBatch.DrawString(_font, instructions, instPos, new Color(150, 150, 170, 255));
                }
            }
            else
            {
                // Draw instructions at bottom with shadow
                if (_font != null)
                {
                    string instructions = "Arrow Keys / Mouse: Navigate  |  Enter / Space / Click: Select  |  ESC: Exit";
                    if (_availablePaths.Count > 1)
                    {
                        instructions += "  |  Select 'Start Game' to choose installation path";
                    }
                    Vector2 instSize = _font.MeasureString(instructions);
                    Vector2 instPos = new Vector2(centerX - instSize.X / 2, viewportHeight - 60);

                    // Shadow
                    _spriteBatch.DrawString(_font, instructions, instPos + new Vector2(1, 1), new Color(0, 0, 0, 150));
                    // Main text
                    _spriteBatch.DrawString(_font, instructions, instPos, new Color(150, 150, 170, 255));
                }
                else
                {
                    // Fallback: draw professional instruction indicators (minimal, clean design)
                    int indicatorY = viewportHeight - 50;
                    int indicatorSize = 12;
                    Color indicatorColor = new Color(180, 180, 200, 200);

                    // Draw subtle separator line
                    int lineWidth = 300;
                    int lineX = centerX - lineWidth / 2;
                    _spriteBatch.Draw(_menuTexture, new Rectangle(lineX, indicatorY - 15, lineWidth, 2), new Color(100, 100, 120, 100));

                    // Arrow keys indicator (up arrow) - clean design
                    int arrowX = centerX - 120;
                    int arrowY = indicatorY;
                    // Arrow shaft
                    _spriteBatch.Draw(_menuTexture, new Rectangle(arrowX + indicatorSize / 2 - 1, arrowY, 2, indicatorSize), indicatorColor);
                    // Arrow head (triangle)
                    int[] arrowHeadX = { arrowX, arrowX + indicatorSize, arrowX + indicatorSize / 2 };
                    int[] arrowHeadY = { arrowY + indicatorSize / 2, arrowY + indicatorSize / 2, arrowY };
                    DrawFilledTriangle(_spriteBatch, arrowHeadX, arrowHeadY, indicatorColor);

                    // Mouse indicator (rounded square)
                    int mouseX = centerX;
                    Rectangle mouseRect = new Rectangle(mouseX - indicatorSize / 2, arrowY - indicatorSize / 2, indicatorSize, indicatorSize);
                    DrawRoundedRectangle(_spriteBatch, mouseRect, 2, indicatorColor);

                    // Enter key indicator (rounded rectangle with arrow)
                    int enterX = centerX + 120;
                    Rectangle enterRect = new Rectangle(enterX - indicatorSize, arrowY - indicatorSize / 2, indicatorSize * 2, indicatorSize);
                    DrawRoundedRectangle(_spriteBatch, enterRect, 2, indicatorColor);
                    // Small arrow inside
                    int[] enterArrowX = { enterX + indicatorSize - 6, enterX + indicatorSize - 6, enterX + indicatorSize - 2 };
                    int[] enterArrowY = { arrowY - 3, arrowY + 3, arrowY };
                    DrawFilledTriangle(_spriteBatch, enterArrowX, enterArrowY, new Color(100, 100, 120, 255));
                }
            }

            _spriteBatch.End();
        }

        private void DrawRectangleBorder(ISpriteBatch spriteBatch, Rectangle rect, int thickness, Color color)
        {
            // Top
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }

        /// <summary>
        /// Draws a filled triangle using rectangles (approximation).
        /// Used for play button icon when font is not available.
        /// </summary>
        private void DrawTriangle(ISpriteBatch spriteBatch, int[] x, int[] y, Color color)
        {
            // Simple triangle drawing using line approximation
            // Draw lines between points
            int minY = Math.Min(Math.Min(y[0], y[1]), y[2]);
            int maxY = Math.Max(Math.Max(y[0], y[1]), y[2]);

            for (int py = minY; py <= maxY; py++)
            {
                // Find intersections with horizontal line at py
                System.Collections.Generic.List<int> intersections = new System.Collections.Generic.List<int>();

                for (int i = 0; i < 3; i++)
                {
                    int next = (i + 1) % 3;
                    if ((y[i] <= py && py < y[next]) || (y[next] <= py && py < y[i]))
                    {
                        if (y[i] != y[next])
                        {
                            float t = (float)(py - y[i]) / (y[next] - y[i]);
                            int ix = (int)(x[i] + t * (x[next] - x[i]));
                            intersections.Add(ix);
                        }
                    }
                }

                if (intersections.Count >= 2)
                {
                    int minX = Math.Min(intersections[0], intersections[1]);
                    int maxX = Math.Max(intersections[0], intersections[1]);
                    spriteBatch.Draw(_menuTexture, new Rectangle(minX, py, maxX - minX, 1), color);
                }
            }
        }

        /// <summary>
        /// Draws a filled triangle with smooth rendering.
        /// </summary>
        private void DrawFilledTriangle(ISpriteBatch spriteBatch, int[] x, int[] y, Color color)
        {
            DrawTriangle(spriteBatch, x, y, color);
        }

        /// <summary>
        /// Draws a triangle outline (border only).
        /// </summary>
        private void DrawTriangleOutline(ISpriteBatch spriteBatch, int[] x, int[] y, Color color)
        {
            int thickness = 2;
            // Draw three edges of the triangle
            DrawLine(spriteBatch, x[0], y[0], x[1], y[1], thickness, color);
            DrawLine(spriteBatch, x[1], y[1], x[2], y[2], thickness, color);
            DrawLine(spriteBatch, x[2], y[2], x[0], y[0], thickness, color);
        }

        /// <summary>
        /// Draws a line between two points.
        /// </summary>
        private void DrawLine(ISpriteBatch spriteBatch, int x1, int y1, int x2, int y2, int thickness, Color color)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.1f)
            {
                return;
            }

            float angle = (float)Math.Atan2(dy, dx);
            float halfThickness = thickness / 2.0f;

            // Draw line as a rotated rectangle
            for (int i = 0; i < thickness; i++)
            {
                float offset = i - halfThickness;
                float perpX = (float)(-Math.Sin(angle) * offset);
                float perpY = (float)(Math.Cos(angle) * offset);

                int startX = (int)(x1 + perpX);
                int startY = (int)(y1 + perpY);
                int endX = (int)(x2 + perpX);
                int endY = (int)(y2 + perpY);

                // Draw line segment
                int lineLength = (int)Math.Ceiling(length);
                for (int j = 0; j <= lineLength; j++)
                {
                    float t = (float)j / lineLength;
                    int px = (int)(startX + (endX - startX) * t);
                    int py = (int)(startY + (endY - startY) * t);
                    spriteBatch.Draw(_menuTexture, new Rectangle(px, py, 1, 1), color);
                }
            }
        }

        /// <summary>
        /// Draws a diagonal line between two points.
        /// </summary>
        private void DrawDiagonalLine(ISpriteBatch spriteBatch, int x1, int y1, int x2, int y2, int thickness, Color color)
        {
            DrawLine(spriteBatch, x1, y1, x2, y2, thickness, color);
        }

        /// <summary>
        /// Draws a rounded rectangle border with smooth corners.
        /// Creates the appearance of rounded corners using border lines with corner arcs.
        /// </summary>
        private void DrawRoundedRectangle(ISpriteBatch spriteBatch, Rectangle rect, int borderThickness, Color color)
        {
            int cornerRadius = borderThickness * 2;
            int cornerGap = cornerRadius;

            // Draw border edges (with gaps at corners for rounded effect)
            // Top edge
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X + cornerGap, rect.Y, rect.Width - cornerGap * 2, borderThickness), color);
            // Bottom edge
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X + cornerGap, rect.Y + rect.Height - borderThickness, rect.Width - cornerGap * 2, borderThickness), color);
            // Left edge
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X, rect.Y + cornerGap, borderThickness, rect.Height - cornerGap * 2), color);
            // Right edge
            spriteBatch.Draw(_menuTexture, new Rectangle(rect.X + rect.Width - borderThickness, rect.Y + cornerGap, borderThickness, rect.Height - cornerGap * 2), color);

            // Draw corner arcs for smooth rounded appearance
            DrawCornerArc(spriteBatch, rect.X + cornerRadius, rect.Y + cornerRadius, cornerRadius, borderThickness, color, true, true);
            DrawCornerArc(spriteBatch, rect.X + rect.Width - cornerRadius, rect.Y + cornerRadius, cornerRadius, borderThickness, color, false, true);
            DrawCornerArc(spriteBatch, rect.X + cornerRadius, rect.Y + rect.Height - cornerRadius, cornerRadius, borderThickness, color, true, false);
            DrawCornerArc(spriteBatch, rect.X + rect.Width - cornerRadius, rect.Y + rect.Height - cornerRadius, cornerRadius, borderThickness, color, false, false);
        }

        /// <summary>
        /// Draws a corner arc (quarter circle border) for rounded rectangle corners.
        /// </summary>
        private void DrawCornerArc(ISpriteBatch spriteBatch, int centerX, int centerY, int radius, int thickness, Color color, bool leftSide, bool topSide)
        {
            // Draw quarter circle arc using border approach
            for (int y = -radius; y <= 0; y++)
            {
                for (int x = -radius; x <= 0; x++)
                {
                    float dist = (float)Math.Sqrt(x * x + y * y);
                    // Draw border pixels (within thickness range from edge)
                    if (dist <= radius && dist >= radius - thickness)
                    {
                        int drawX = leftSide ? centerX + x : centerX - x;
                        int drawY = topSide ? centerY + y : centerY - y;
                        spriteBatch.Draw(_menuTexture, new Rectangle(drawX, drawY, 1, 1), color);
                    }
                }
            }
        }

        /// <summary>
        /// Draws a filled circle (approximated with rectangle).
        /// </summary>
        private void DrawFilledCircle(ISpriteBatch spriteBatch, Rectangle bounds, Color color)
        {
            int centerX = bounds.X + bounds.Width / 2;
            int centerY = bounds.Y + bounds.Height / 2;
            int radius = Math.Min(bounds.Width, bounds.Height) / 2;

            // Draw filled circle by checking each pixel
            for (int y = bounds.Y; y < bounds.Y + bounds.Height; y++)
            {
                for (int x = bounds.X; x < bounds.X + bounds.Width; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius)
                    {
                        spriteBatch.Draw(_menuTexture, new Rectangle(x, y, 1, 1), color);
                    }
                }
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
                var updatedSettings = new Odyssey.Core.GameSettings
                {
                    Game = _settings.Game,
                    GamePath = gamePath,
                    StartModule = "end_m01aa" // Default starting module
                };

                // Create new session
                _session = new GameSession(updatedSettings, _world, _vm, _globals);

                // Initialize save system
                if (_world != null)
                {
                    string savesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Odyssey", "Saves");
                    var gameDataManager = new Odyssey.Kotor.Data.GameDataManager(_session.Installation);
                    var serializer = new Odyssey.Content.Save.SaveSerializer(gameDataManager);
                    var dataProvider = new Odyssey.Content.Save.SaveDataProvider(savesPath, serializer);
                    _saveSystem = new Odyssey.Core.Save.SaveSystem(_world, dataProvider);
                    _saveSystem.SetScriptGlobals(_globals);
                    RefreshSaveList();
                }

                // Start the game session
                _session.StartNewGame();

                // Initialize camera after player is created
                UpdateCamera();

                // Transition to in-game state
                _currentState = GameState.InGame;

                Console.WriteLine("[Odyssey] Game started successfully");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Odyssey] Failed to start game: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
        }

        private void InitializeGameRendering()
        {
            // Initialize 3D rendering using abstraction layer
            _basicEffect = _graphicsDevice.CreateBasicEffect();

            // Set up default effect parameters
            _basicEffect.VertexColorEnabled = true;
            _basicEffect.LightingEnabled = true;
            _basicEffect.TextureEnabled = false;
            _basicEffect.AmbientLightColor = new System.Numerics.Vector3(0.2f, 0.2f, 0.2f);
            _basicEffect.DiffuseColor = new System.Numerics.Vector3(1.0f, 1.0f, 1.0f);
            _basicEffect.Alpha = 1.0f;

            // Initialize matrices
            _viewMatrix = System.Numerics.Matrix4x4.Identity;
            _projectionMatrix = System.Numerics.Matrix4x4.Identity;

            Console.WriteLine("[Odyssey] Game rendering initialized (3D rendering enabled with abstraction layer)");
        }

        private void CreateGroundPlane()
        {
            // TODO: Ground plane creation needs 3D rendering abstraction
            // This will be implemented once 3D rendering abstraction is complete
        }

        private void UpdateCamera()
        {
            // TODO: 3D camera system needs abstraction
            // For now, camera update is simplified
            System.Numerics.Vector3 target = new System.Numerics.Vector3(0, 0, 0);
            System.Numerics.Vector3 cameraPosition;
            System.Numerics.Vector3 up = new System.Numerics.Vector3(0, 1, 0);

            // Try to follow player if available
            if (_session != null && _session.PlayerEntity != null)
            {
                Kotor.Components.TransformComponent transform = _session.PlayerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
                if (transform != null)
                {
                    target = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);

                    // Camera follows behind and above player
                    float cameraDistance = 8f;
                    float cameraHeight = 4f;
                    float cameraAngle = transform.Facing + (float)Math.PI; // Behind player

                    cameraPosition = new System.Numerics.Vector3(
                        target.X + (float)Math.Sin(cameraAngle) * cameraDistance,
                        target.Y + cameraHeight,
                        target.Z + (float)Math.Cos(cameraAngle) * cameraDistance
                    );
                }
                else
                {
                    // Fallback: simple orbit around origin
                    _cameraAngle += 0.01f;
                    float distance = 10f;
                    float height = 5f;
                    cameraPosition = new System.Numerics.Vector3(
                        (float)Math.Sin(_cameraAngle) * distance,
                        height,
                        (float)Math.Cos(_cameraAngle) * distance
                    );
                }
            }
            else
            {
                // Fallback: simple orbit around origin
                _cameraAngle += 0.01f;
                float distance = 10f;
                float height = 5f;
                cameraPosition = new System.Numerics.Vector3(
                    (float)Math.Sin(_cameraAngle) * distance,
                    height,
                    (float)Math.Cos(_cameraAngle) * distance
                );
            }

            // Use MatrixHelper for matrix operations (abstraction layer)
            _viewMatrix = MatrixHelper.CreateLookAt(cameraPosition, target, up);

            float aspectRatio = (float)_graphicsDevice.Viewport.Width / _graphicsDevice.Viewport.Height;
            _projectionMatrix = MatrixHelper.CreatePerspectiveFieldOfView(
                (float)(60.0 * Math.PI / 180.0),
                aspectRatio,
                0.1f,
                100f
            );
        }

        private void DrawGameWorld()
        {
            // Clear with a sky color
            _graphicsDevice.Clear(new Color(135, 206, 250, 255)); // Sky blue

            // Set 3D rendering states using abstraction layer
            _graphicsDevice.SetDepthStencilState(_graphicsDevice.CreateDepthStencilState());
            _graphicsDevice.SetRasterizerState(_graphicsDevice.CreateRasterizerState());
            _graphicsDevice.SetBlendState(_graphicsDevice.CreateBlendState());
            _graphicsDevice.SetSamplerState(0, _graphicsDevice.CreateSamplerState());

            // Draw 3D scene using abstraction layer
            if (_groundVertexBuffer != null && _groundIndexBuffer != null && _basicEffect != null)
            {
                _graphicsDevice.SetVertexBuffer(_groundVertexBuffer);
                _graphicsDevice.SetIndexBuffer(_groundIndexBuffer);

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = System.Numerics.Matrix4x4.Identity;

                foreach (IEffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    // Draw ground plane (would need to know primitive count)
                    // _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, vertexCount, 0, primitiveCount);
                }
            }

            // Draw loaded area rooms if available
            if (_session != null && _session.CurrentRuntimeModule != null)
            {
                Odyssey.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
                if (entryArea != null && entryArea is Odyssey.Core.Module.RuntimeArea runtimeArea)
                {
                    DrawAreaRooms(runtimeArea);
                }
            }

            // Draw entities from GIT
            if (_session != null && _session.CurrentRuntimeModule != null)
            {
                Odyssey.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
                if (entryArea != null && entryArea is Odyssey.Core.Module.RuntimeArea runtimeArea)
                {
                    DrawAreaEntities(runtimeArea);
                }
            }

            // Draw player entity
            if (_session != null && _session.PlayerEntity != null)
            {
                DrawPlayerEntity(_session.PlayerEntity);
            }

            // Draw UI overlay
            _spriteBatch.Begin();

            // Draw dialogue UI if in conversation
            if (_session != null && _session.DialogueManager != null && _session.DialogueManager.IsConversationActive)
            {
                DrawDialogueUI();
            }
            else
            {
                // Draw status text when not in dialogue
                string statusText = "Game Running - Press ESC to return to menu";
                if (_session != null && _session.CurrentModuleName != null)
                {
                    statusText = "Module: " + _session.CurrentModuleName + " - Press ESC to return to menu";
                    Odyssey.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule?.GetArea(_session.CurrentRuntimeModule.EntryArea);
                    if (entryArea != null)
                    {
                        statusText += " | Area: " + entryArea.DisplayName + " (" + entryArea.ResRef + ")";
                        if (entryArea is Odyssey.Core.Module.RuntimeArea runtimeArea && runtimeArea.Rooms != null)
                        {
                            statusText += " | Rooms: " + runtimeArea.Rooms.Count;
                        }
                    }
                }
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, statusText, new Vector2(10, 10), Color.White);
                }
            }
            // If no font, we just skip text rendering - the 3D scene is still visible
            _spriteBatch.End();
        }

        private void DrawAreaRooms(Odyssey.Core.Module.RuntimeArea area)
        {
            // Room rendering using abstraction layer
            if (area.Rooms == null || area.Rooms.Count == 0)
            {
                return;
            }

            if (_roomRenderer == null)
            {
                return; // Room renderer not initialized
            }

            // Determine which room the player is in for VIS culling
            int currentRoomIndex = -1;
            if (_session != null && _session.PlayerEntity != null)
            {
                Kotor.Components.TransformComponent transform = _session.PlayerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
                if (transform != null)
                {
                    currentRoomIndex = FindPlayerRoom(area, transform.Position);
                }
            }

            // Load and render room meshes (with VIS culling if possible)
            for (int i = 0; i < area.Rooms.Count; i++)
            {
                Odyssey.Core.Module.RoomInfo room = area.Rooms[i];

                if (string.IsNullOrEmpty(room.ModelName))
                {
                    continue;
                }

                // Get or load room mesh using abstraction layer
                IRoomMeshData meshData;
                if (!_roomMeshes.TryGetValue(room.ModelName, out meshData))
                {
                    // Try to load actual MDL model from module resources
                    AuroraEngine.Common.Formats.MDLData.MDL mdl = null;
                    if (_session != null && _session.CurrentRuntimeModule != null)
                    {
                        mdl = LoadMDLModel(room.ModelName);
                    }

                    meshData = _roomRenderer.LoadRoomMesh(room.ModelName, mdl);
                    if (meshData != null)
                    {
                        _roomMeshes[room.ModelName] = meshData;
                    }
                }

                if (meshData == null || meshData.VertexBuffer == null || meshData.IndexBuffer == null)
                {
                    // Skip rooms that failed to load - this is normal for some modules
                    continue;
                }

                // Validate mesh data before rendering
                if (meshData.IndexCount < 3)
                {
                    continue; // Need at least one triangle
                }

                // Set up transform using abstraction layer
                var roomPos = new System.Numerics.Vector3(room.Position.X, room.Position.Y, room.Position.Z);
                var roomWorld = MatrixHelper.CreateTranslation(roomPos);

                // Set up rendering state using abstraction layer
                _graphicsDevice.SetVertexBuffer(meshData.VertexBuffer);
                _graphicsDevice.SetIndexBuffer(meshData.IndexBuffer);

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = roomWorld;
                _basicEffect.VertexColorEnabled = true;
                _basicEffect.LightingEnabled = true; // Ensure lighting is enabled

                // Draw the mesh using abstraction layer
                foreach (IEffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        meshData.IndexCount,
                        0,
                        meshData.IndexCount / 3 // Number of triangles
                    );
                }
            }
        }

        /// <summary>
        /// Draws entities from the area (NPCs, doors, placeables, etc.).
        /// </summary>
        private void DrawAreaEntities(Odyssey.Core.Module.RuntimeArea area)
        {
            // TODO: 3D entity rendering needs abstraction (BasicEffect, VertexBuffer, IndexBuffer, Matrix)
            // For now, entity rendering is disabled until 3D abstraction is complete
            if (area == null)
            {
                return;
            }

            // Entity rendering disabled - needs 3D abstraction
            return;
        }

        /// <summary>
        /// Draws a single entity using model renderer if available, otherwise as a simple box.
        /// </summary>
        private void DrawEntity(Odyssey.Core.Interfaces.IEntity entity)
        {
            // Entity rendering using abstraction layer

            Kotor.Components.TransformComponent transform = entity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Try to render with model renderer first
            if (_entityModelRenderer != null)
            {
                try
                {
                    _entityModelRenderer.RenderEntity(entity, _viewMatrix, _projectionMatrix);
                    return; // Successfully rendered with model
                }
                catch (Exception ex)
                {
                    // Fall back to box rendering if model rendering fails
                    Console.WriteLine("[Odyssey] Model rendering failed for entity " + entity.Tag + ": " + ex.Message);
                }
            }

            // Fallback: Draw as simple colored box
            Color entityColor = Color.Gray;
            float entityHeight = 1f;
            float entityWidth = 0.5f;

            switch (entity.ObjectType)
            {
                case Odyssey.Core.Enums.ObjectType.Creature:
                    entityColor = Color.Green;
                    entityHeight = 2f;
                    entityWidth = 0.5f;
                    break;
                case Odyssey.Core.Enums.ObjectType.Door:
                    entityColor = Color.Brown;
                    entityHeight = 3f;
                    entityWidth = 1f;
                    break;
                case Odyssey.Core.Enums.ObjectType.Placeable:
                    entityColor = Color.Orange;
                    entityHeight = 1.5f;
                    entityWidth = 0.8f;
                    break;
                case Odyssey.Core.Enums.ObjectType.Trigger:
                    entityColor = Color.Yellow;
                    entityHeight = 0.5f;
                    entityWidth = 1f;
                    break;
                case Odyssey.Core.Enums.ObjectType.Waypoint:
                    entityColor = Color.Cyan;
                    entityHeight = 0.3f;
                    entityWidth = 0.3f;
                    break;
            }

            var entityPos = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
            var entityWorld = MatrixHelper.CreateTranslation(entityPos);

            // Create a simple box for the entity using abstraction layer
            float hw = entityWidth * 0.5f;
            var entityVertices = new VertexPositionColor[]
            {
                // Bottom face
                new VertexPositionColor(new System.Numerics.Vector3(-hw, -hw, 0), entityColor),
                new VertexPositionColor(new System.Numerics.Vector3(hw, -hw, 0), entityColor),
                new VertexPositionColor(new System.Numerics.Vector3(hw, hw, 0), entityColor),
                new VertexPositionColor(new System.Numerics.Vector3(-hw, hw, 0), entityColor),
                // Top face
                new VertexPositionColor(new System.Numerics.Vector3(-hw, -hw, entityHeight), entityColor),
                new VertexPositionColor(new System.Numerics.Vector3(hw, -hw, entityHeight), entityColor),
                new VertexPositionColor(new System.Numerics.Vector3(hw, hw, entityHeight), entityColor),
                new VertexPositionColor(new System.Numerics.Vector3(-hw, hw, entityHeight), entityColor)
            };

            short[] entityIndices = new short[]
            {
                // Bottom
                0, 1, 2, 0, 2, 3,
                // Top
                4, 6, 5, 4, 7, 6,
                // Sides
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };

            // Create temporary buffers for entity using abstraction layer
            var entityVb = _graphicsDevice.CreateVertexBuffer(entityVertices);
            var entityIb = _graphicsDevice.CreateIndexBuffer(entityIndices, true);
            try
            {
                _graphicsDevice.SetVertexBuffer(entityVb);
                _graphicsDevice.SetIndexBuffer(entityIb);

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = entityWorld;
                _basicEffect.VertexColorEnabled = true;

                foreach (IEffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        entityVertices.Length,
                        0,
                        entityIndices.Length / 3
                    );
                }
            }
            finally
            {
                entityVb?.Dispose();
                entityIb?.Dispose();
            }
        }

        /// <summary>
        /// Draws the player entity as a simple representation.
        /// </summary>
        private void DrawPlayerEntity(Odyssey.Core.Interfaces.IEntity playerEntity)
        {
            // Player rendering using abstraction layer

            Kotor.Components.TransformComponent transform = playerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Create a simple representation of the player (colored box)
            // TODO: Replace with actual player model rendering
            var playerPos = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
            var playerWorld = MatrixHelper.CreateTranslation(playerPos);

            // Create a simple box for the player (1x2x1 units - humanoid shape)
            var playerVertices = new VertexPositionColor[]
            {
                // Bottom face
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0), Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 0), Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, 0.5f, 0), Color.Blue),
                new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0), Color.Blue),
                // Top face
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 2), Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 2), Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, 0.5f, 2), Color.Blue),
                new VertexPositionColor(new Vector3(-0.5f, 0.5f, 2), Color.Blue)
            };

            short[] playerIndices = new short[]
            {
                // Bottom
                0, 1, 2, 0, 2, 3,
                // Top
                4, 6, 5, 4, 7, 6,
                // Sides
                0, 4, 5, 0, 5, 1,
                1, 5, 6, 1, 6, 2,
                2, 6, 7, 2, 7, 3,
                3, 7, 4, 3, 4, 0
            };

            // Create temporary buffers for player using abstraction layer
            var playerVb = _graphicsDevice.CreateVertexBuffer(playerVertices);
            var playerIb = _graphicsDevice.CreateIndexBuffer(playerIndices, true);
            try
            {
                _graphicsDevice.SetVertexBuffer(playerVb);
                _graphicsDevice.SetIndexBuffer(playerIb);

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = playerWorld;
                _basicEffect.VertexColorEnabled = true;
                _basicEffect.LightingEnabled = true; // Ensure lighting is enabled

                foreach (IEffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        playerVertices.Length,
                        0,
                        playerIndices.Length / 3
                    );
                }
            }
            finally
            {
                playerVb?.Dispose();
                playerIb?.Dispose();
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

        /// <summary>
        /// Loads an MDL model from the current module.
        /// </summary>
        [CanBeNull]
        private AuroraEngine.Common.Formats.MDLData.MDL LoadMDLModel(string modelResRef)
        {
            if (string.IsNullOrEmpty(modelResRef) || _session == null)
            {
                return null;
            }

            try
            {
                // Get module from session - we need access to the CSharpKOTOR Module object
                // For now, we'll need to store it or access it differently
                // This is a simplified approach - in a full implementation, we'd cache the Module object
                string moduleName = _session.CurrentModuleName;
                if (string.IsNullOrEmpty(moduleName))
                {
                    return null;
                }

                // Create a temporary module to load the resource
                // Note: This is inefficient - we should cache the Module object
                var installation = new Installation(_settings.GamePath);
                var module = new Module(moduleName, installation);

                ModuleResource mdlResource = module.Resource(modelResRef, ResourceType.MDL);
                if (mdlResource == null)
                {
                    return null;
                }

                string activePath = mdlResource.Activate();
                if (string.IsNullOrEmpty(activePath))
                {
                    return null;
                }

                // Load MDL directly using MDLAuto for better performance
                return MDLAuto.ReadMdl(activePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] Failed to load MDL model {modelResRef}: {ex.Message}");
                return null;
            }
        }

        // Track previous keyboard state for dialogue input (using abstraction layer)
        private IKeyboardState _previousPlayerKeyboardState;

        /// <summary>
        /// Handles player input for movement.
        /// </summary>
        private void HandlePlayerInput(IKeyboardState keyboardState, IMouseState mouseState, GameTime gameTime)
        {
            // Handle dialogue input first (if in dialogue)
            if (_session != null && _session.DialogueManager != null && _session.DialogueManager.IsConversationActive)
            {
                HandleDialogueInput(keyboardState);
                _previousPlayerKeyboardState = keyboardState;
                return; // Don't process movement while in dialogue
            }

            if (_session == null || _session.PlayerEntity == null)
            {
                _previousPlayerKeyboardState = keyboardState;
                return;
            }

            Kotor.Components.TransformComponent transform = _session.PlayerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
            if (transform == null)
            {
                return;
            }

            float moveSpeed = 5f * (float)gameTime.ElapsedGameTime.TotalSeconds;
            float turnSpeed = 2f * (float)gameTime.ElapsedGameTime.TotalSeconds;
            bool moved = false;

            // Get navigation mesh for collision
            var navMesh = _session.NavigationMesh as Odyssey.Core.Navigation.NavigationMesh;
            bool hasNavMesh = navMesh != null && navMesh.FaceCount > 0;

            // Keyboard movement (WASD)
            if (keyboardState.IsKeyDown(Keys.W))
            {
                // Move forward
                System.Numerics.Vector3 pos = transform.Position;
                pos.X += (float)Math.Sin(transform.Facing) * moveSpeed;
                pos.Z += (float)Math.Cos(transform.Facing) * moveSpeed;
                transform.Position = pos;
                moved = true;
            }
            if (keyboardState.IsKeyDown(Keys.S))
            {
                // Move backward
                System.Numerics.Vector3 pos = transform.Position;
                pos.X -= (float)Math.Sin(transform.Facing) * moveSpeed;
                pos.Z -= (float)Math.Cos(transform.Facing) * moveSpeed;
                transform.Position = pos;
                moved = true;
            }
            if (keyboardState.IsKeyDown(Keys.A))
            {
                // Turn left
                transform.Facing -= turnSpeed;
                moved = true;
            }
            if (keyboardState.IsKeyDown(Keys.D))
            {
                // Turn right
                transform.Facing += turnSpeed;
                moved = true;
            }

            // Click-to-move with walkmesh raycasting
            if (mouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                // Only trigger on click (not hold)
                // First check if we clicked on an entity
                System.Numerics.Vector3 rayOrigin = GetCameraPosition();
                System.Numerics.Vector3 rayDirection = GetMouseRayDirection(mouseState.X, mouseState.Y);

                Odyssey.Core.Interfaces.IEntity clickedEntity = FindEntityAtRay(rayOrigin, rayDirection);

                if (clickedEntity != null)
                {
                    // Clicked on an entity - interact with it
                    HandleEntityClick(clickedEntity);
                }
                else if (hasNavMesh)
                {
                    // No entity clicked - move to clicked position
                    System.Numerics.Vector3 hitPoint;
                    if (navMesh.Raycast(
                        new System.Numerics.Vector3(rayOrigin.X, rayOrigin.Y, rayOrigin.Z),
                        new System.Numerics.Vector3(rayDirection.X, rayDirection.Y, rayDirection.Z),
                        1000f,
                        out hitPoint))
                    {
                        // Project to walkable surface
                        System.Numerics.Vector3? nearest = navMesh.GetNearestPoint(hitPoint);
                        if (nearest.HasValue)
                        {
                            System.Numerics.Vector3 targetPos = nearest.Value;
                            transform.Position = new System.Numerics.Vector3(targetPos.X, targetPos.Y, targetPos.Z);
                            // Face towards target
                            System.Numerics.Vector3 dir = targetPos - new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                            if (dir.LengthSquared() > 0.01f)
                            {
                                // Y-up system: Atan2(Y, X) for 2D plane facing
                                transform.Facing = (float)Math.Atan2(dir.Y, dir.X);
                            }
                            moved = true;
                        }
                    }
                }
            }

            // Clamp player to walkmesh surface
            if (hasNavMesh && moved)
            {
                System.Numerics.Vector3 pos = transform.Position;
                var worldPos = new System.Numerics.Vector3(pos.X, pos.Y, pos.Z);

                // Project position to walkmesh surface
                System.Numerics.Vector3 projectedPos;
                float height;
                if (navMesh.ProjectToSurface(worldPos, out projectedPos, out height))
                {
                    // Update Z coordinate to match walkmesh height
                    transform.Position = new System.Numerics.Vector3(projectedPos.X, projectedPos.Y, projectedPos.Z);
                }
                else
                {
                    // If not on walkmesh, find nearest walkable point
                    System.Numerics.Vector3? nearest = navMesh.GetNearestPoint(worldPos);
                    if (nearest.HasValue)
                    {
                        System.Numerics.Vector3 nearestPos = nearest.Value;
                        transform.Position = new System.Numerics.Vector3(nearestPos.X, nearestPos.Y, nearestPos.Z);
                    }
                }
            }

            _previousMouseState = mouseState;
            _previousPlayerKeyboardState = keyboardState;
        }

        /// <summary>
        /// Handles keyboard input for dialogue replies.
        /// </summary>
        private void HandleDialogueInput(IKeyboardState keyboardState)
        {
            if (_session == null || _session.DialogueManager == null || !_session.DialogueManager.IsConversationActive)
            {
                return;
            }

            Kotor.Dialogue.DialogueState state = _session.DialogueManager.CurrentState;
            if (state == null || state.AvailableReplies == null || state.AvailableReplies.Count == 0)
            {
                return;
            }

            // Check number keys 1-9 for reply selection
            Keys[] numberKeys = { Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9 };

            for (int i = 0; i < Math.Min(numberKeys.Length, state.AvailableReplies.Count); i++)
            {
                if (keyboardState.IsKeyDown(numberKeys[i]) && _previousPlayerKeyboardState.IsKeyUp(numberKeys[i]))
                {
                    // Key was just pressed
                    Console.WriteLine($"[Dialogue] Selected reply {i + 1}");
                    _session.DialogueManager.SelectReply(i);
                    break;
                }
            }

            // ESC to abort conversation
            if (keyboardState.IsKeyDown(Keys.Escape) && _previousPlayerKeyboardState.IsKeyUp(Keys.Escape))
            {
                Console.WriteLine("[Dialogue] Conversation aborted");
                _session.DialogueManager.AbortConversation();
            }
        }

        /// <summary>
        /// Gets the camera position for raycasting.
        /// </summary>
        private System.Numerics.Vector3 GetCameraPosition()
        {
            if (_session != null && _session.PlayerEntity != null)
            {
                Kotor.Components.TransformComponent transform = _session.PlayerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
                if (transform != null)
                {
                    // Camera is behind and above player
                    float cameraDistance = 8f;
                    float cameraHeight = 4f;
                    float cameraAngle = transform.Facing + (float)Math.PI;

                    System.Numerics.Vector3 playerPos = transform.Position;
                    return new System.Numerics.Vector3(
                        playerPos.X + (float)Math.Sin(cameraAngle) * cameraDistance,
                        playerPos.Y + cameraHeight,
                        playerPos.Z + (float)Math.Cos(cameraAngle) * cameraDistance
                    );
                }
            }
            return System.Numerics.Vector3.Zero;
        }

        /// <summary>
        /// Gets the ray direction from mouse position.
        /// </summary>
        private System.Numerics.Vector3 GetMouseRayDirection(int mouseX, int mouseY)
        {
            // Convert mouse position to normalized device coordinates (-1 to 1)
            float x = (2.0f * mouseX / _graphicsDevice.Viewport.Width) - 1.0f;
            float y = 1.0f - (2.0f * mouseY / _graphicsDevice.Viewport.Height);

            // Create ray in view space
            System.Numerics.Vector4 rayClip = new System.Numerics.Vector4(x, y, -1.0f, 1.0f);

            // Transform to eye space
            System.Numerics.Matrix4x4 invProjection = System.Numerics.Matrix4x4.Invert(_projectionMatrix);
            System.Numerics.Vector4 rayEye = System.Numerics.Vector4.Transform(rayClip, invProjection);
            rayEye = new System.Numerics.Vector4(rayEye.X, rayEye.Y, -1.0f, 0.0f);

            // Transform to world space
            System.Numerics.Matrix4x4 invView = System.Numerics.Matrix4x4.Invert(_viewMatrix);
            System.Numerics.Vector4 rayWorld = System.Numerics.Vector4.Transform(rayEye, invView);
            System.Numerics.Vector3 rayDir = new System.Numerics.Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z);
            rayDir = System.Numerics.Vector3.Normalize(rayDir);

            return rayDir;
        }

        /// <summary>
        /// Finds an entity at the given ray position.
        /// </summary>
        private Odyssey.Core.Interfaces.IEntity FindEntityAtRay(System.Numerics.Vector3 rayOrigin, System.Numerics.Vector3 rayDirection)
        {
            if (_session == null || _session.CurrentRuntimeModule == null)
            {
                return null;
            }

            Odyssey.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
            if (entryArea == null || !(entryArea is Odyssey.Core.Module.RuntimeArea runtimeArea))
            {
                return null;
            }

            // Simple AABB raycast for entities
            // For a quick demo, use bounding box intersection
            float closestDistance = float.MaxValue;
            Odyssey.Core.Interfaces.IEntity closestEntity = null;

            foreach (Odyssey.Core.Interfaces.IEntity entity in runtimeArea.GetAllEntities())
            {
                Kotor.Components.TransformComponent transform = entity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
                if (transform == null)
                {
                    continue;
                }

                // Create a simple bounding box around the entity
                float entitySize = 1.0f; // Default size
                switch (entity.ObjectType)
                {
                    case Odyssey.Core.Enums.ObjectType.Creature:
                        entitySize = 1.0f;
                        break;
                    case Odyssey.Core.Enums.ObjectType.Door:
                        entitySize = 1.5f;
                        break;
                    case Odyssey.Core.Enums.ObjectType.Placeable:
                        entitySize = 1.0f;
                        break;
                }

                var entityPos = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                Vector3 entityMin = entityPos - new Vector3(entitySize, entitySize, entitySize);
                Vector3 entityMax = entityPos + new Vector3(entitySize, entitySize, entitySize);

                // Simple ray-AABB intersection
                float tmin = 0.0f;
                float tmax = 1000.0f;

                // Check X axis
                float invDx = 1.0f / rayDirection.X;
                float t0x = (entityMin.X - rayOrigin.X) * invDx;
                float t1x = (entityMax.X - rayOrigin.X) * invDx;
                if (invDx < 0.0f)
                {
                    float temp = t0x;
                    t0x = t1x;
                    t1x = temp;
                }
                tmin = t0x > tmin ? t0x : tmin;
                tmax = t1x < tmax ? t1x : tmax;
                if (tmax < tmin)
                {
                    continue;
                }

                // Check Y axis
                float invDy = 1.0f / rayDirection.Y;
                float t0y = (entityMin.Y - rayOrigin.Y) * invDy;
                float t1y = (entityMax.Y - rayOrigin.Y) * invDy;
                if (invDy < 0.0f)
                {
                    float temp = t0y;
                    t0y = t1y;
                    t1y = temp;
                }
                tmin = t0y > tmin ? t0y : tmin;
                tmax = t1y < tmax ? t1y : tmax;
                if (tmax < tmin)
                {
                    continue;
                }

                // Check Z axis
                float invDz = 1.0f / rayDirection.Z;
                float t0z = (entityMin.Z - rayOrigin.Z) * invDz;
                float t1z = (entityMax.Z - rayOrigin.Z) * invDz;
                if (invDz < 0.0f)
                {
                    float temp = t0z;
                    t0z = t1z;
                    t1z = temp;
                }
                tmin = t0z > tmin ? t0z : tmin;
                tmax = t1z < tmax ? t1z : tmax;
                if (tmax < tmin)
                {
                    continue;
                }

                if (tmax >= tmin && tmin < closestDistance)
                {
                    closestDistance = tmin;
                    closestEntity = entity;
                }
            }

            return closestEntity;
        }

        /// <summary>
        /// Handles clicking on an entity.
        /// </summary>
        private void HandleEntityClick(Odyssey.Core.Interfaces.IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            Console.WriteLine($"[Odyssey] Clicked on entity: {entity.ObjectType}");

            // Handle different entity types
            switch (entity.ObjectType)
            {
                case Odyssey.Core.Enums.ObjectType.Creature:
                    // Try to start dialogue
                    StartDialogueWithEntity(entity);
                    break;
                case Odyssey.Core.Enums.ObjectType.Door:
                    HandleDoorInteraction(entity);
                    break;
                case Odyssey.Core.Enums.ObjectType.Placeable:
                    // Try to start dialogue or interact
                    StartDialogueWithEntity(entity);
                    break;
                case Odyssey.Core.Enums.ObjectType.Trigger:
                    HandleTriggerActivation(entity);
                    break;
                default:
                    Console.WriteLine($"[Odyssey] Unknown entity type clicked: {entity.ObjectType}");
                    break;
            }
        }

        /// <summary>
        /// Starts a dialogue with an entity if it has a conversation.
        /// </summary>
        private void StartDialogueWithEntity(Odyssey.Core.Interfaces.IEntity entity)
        {
            if (_session == null || _session.PlayerEntity == null || entity == null)
            {
                return;
            }

            // Get conversation ResRef from entity
            string conversationResRef = GetEntityConversation(entity);
            if (string.IsNullOrEmpty(conversationResRef))
            {
                Console.WriteLine($"[Odyssey] Entity {entity.ObjectType} has no conversation");
                return;
            }

            Console.WriteLine($"[Odyssey] Starting dialogue: {conversationResRef}");

            // Start conversation using dialogue manager
            if (_session.DialogueManager != null)
            {
                bool started = _session.DialogueManager.StartConversation(conversationResRef, entity, _session.PlayerEntity);
                if (started)
                {
                    Console.WriteLine($"[Odyssey] Dialogue started successfully");
                    // Subscribe to dialogue events for console output
                    _session.DialogueManager.OnNodeEnter += OnDialogueNodeEnter;
                    _session.DialogueManager.OnRepliesReady += OnDialogueRepliesReady;
                    _session.DialogueManager.OnConversationEnd += OnDialogueEnd;
                }
                else
                {
                    Console.WriteLine($"[Odyssey] Failed to start dialogue: {conversationResRef}");
                }
            }
        }

        /// <summary>
        /// Gets the conversation ResRef from an entity.
        /// </summary>
        private string GetEntityConversation(Odyssey.Core.Interfaces.IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            // Try to get conversation from ScriptHooksComponent local string
            Kotor.Components.ScriptHooksComponent scriptsComponent = entity.GetComponent<Odyssey.Kotor.Components.ScriptHooksComponent>();
            if (scriptsComponent != null)
            {
                string conversation = scriptsComponent.GetLocalString("Conversation");
                if (!string.IsNullOrEmpty(conversation))
                {
                    return conversation;
                }
            }

            // Try to get from PlaceableComponent
            Kotor.Components.PlaceableComponent placeableComponent = entity.GetComponent<Odyssey.Kotor.Components.PlaceableComponent>();
            if (placeableComponent != null && !string.IsNullOrEmpty(placeableComponent.Conversation))
            {
                return placeableComponent.Conversation;
            }

            // Try to get from DoorComponent
            Kotor.Components.DoorComponent doorComponent = entity.GetComponent<Odyssey.Kotor.Components.DoorComponent>();
            if (doorComponent != null && !string.IsNullOrEmpty(doorComponent.Conversation))
            {
                return doorComponent.Conversation;
            }

            return null;
        }

        /// <summary>
        /// Handles door interaction (open/close).
        /// </summary>
        private void HandleDoorInteraction(Odyssey.Core.Interfaces.IEntity doorEntity)
        {
            if (doorEntity == null)
            {
                return;
            }

            Kotor.Components.DoorComponent doorComponent = doorEntity.GetComponent<Odyssey.Kotor.Components.DoorComponent>();
            if (doorComponent == null)
            {
                Console.WriteLine("[Odyssey] Door entity has no DoorComponent");
                return;
            }

            // Check if door is locked
            if (doorComponent.IsLocked)
            {
                Console.WriteLine("[Odyssey] Door is locked");

                // Check if player has the required key
                if (doorComponent.KeyRequired && !string.IsNullOrEmpty(doorComponent.KeyName))
                {
                    Odyssey.Core.Interfaces.Components.IInventoryComponent playerInventory = _session.PlayerEntity?.GetComponent<Odyssey.Core.Interfaces.Components.IInventoryComponent>();
                    if (playerInventory != null && playerInventory.HasItemByTag(doorComponent.KeyName))
                    {
                        // Player has the key, unlock the door
                        doorComponent.Unlock();
                        Console.WriteLine($"[Odyssey] Door unlocked with key: {doorComponent.KeyName}");

                        // Auto-remove key if configured
                        if (doorComponent.AutoRemoveKey)
                        {
                            // Find and remove the key item from inventory
                            foreach (IEntity item in playerInventory.GetAllItems())
                            {
                                if (item != null && item.Tag != null &&
                                    item.Tag.Equals(doorComponent.KeyName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (playerInventory.RemoveItem(item))
                                    {
                                        Console.WriteLine($"[Odyssey] Key {doorComponent.KeyName} removed from inventory (auto-remove)");
                                    }
                                    break; // Only remove one key
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Odyssey] Door requires key: {doorComponent.KeyName} (player does not have it)");
                        return;
                    }
                }
                else if (doorComponent.Lockable && doorComponent.LockDC > 0)
                {
                    // Door can be lockpicked (Security skill check)
                    // Based on swkotor2.exe lockpicking system
                    // Located via string references: "OpenLockDC" @ 0x007c1b08 (door lock DC field), "gui_lockpick" @ 0x007c2ff4 (lockpick GUI)
                    // "setsecurity" @ 0x007c7a30 (set security skill command), "SECURITY_LBL" @ 0x007d33b8 (security label)
                    // "SECURITY_POINTS_BTN" @ 0x007d33c8 (security points button)
                    // Security skill check: d20 + Security skill rank vs LockDC
                    // Skill constant: SKILL_SECURITY = 6 (from skills.2da table, Security skill index)
                    // Original implementation: Roll d20 (1-20), add Security skill rank, compare to door's OpenLockDC
                    // Lockpicking success: If (d20 + Security skill rank) >= OpenLockDC, door unlocks
                    // Lockpicking failure: If (d20 + Security skill rank) < OpenLockDC, door remains locked
                    // Security skill rank: Retrieved from creature's skill ranks (stored in UTC template or calculated from class/level)
                    Odyssey.Core.Interfaces.Components.IStatsComponent playerStats =
                        _session.PlayerEntity?.GetComponent<Odyssey.Core.Interfaces.Components.IStatsComponent>();

                    if (playerStats != null)
                    {
                        // Get Security skill rank (skill 6)
                        int securitySkill = playerStats.GetSkillRank(6);

                        // Roll d20 (1-20)
                        Random random = new Random();
                        int roll = random.Next(1, 21);
                        int total = roll + securitySkill;

                        if (total >= doorComponent.LockDC)
                        {
                            // Lockpicking successful
                            doorComponent.Unlock();
                            Console.WriteLine($"[Odyssey] Door lockpicked (roll: {roll} + skill: {securitySkill} = {total} >= DC: {doorComponent.LockDC})");
                        }
                        else
                        {
                            // Lockpicking failed
                            Console.WriteLine($"[Odyssey] Lockpicking failed (roll: {roll} + skill: {securitySkill} = {total} < DC: {doorComponent.LockDC})");
                            return;
                        }
                    }
                    else
                    {
                        // Player has no stats component, cannot lockpick
                        Console.WriteLine("[Odyssey] Cannot lockpick - player has no stats component");
                        return;
                    }
                }
                else
                {
                    // Door is locked but cannot be unlocked (plot door, etc.)
                    Console.WriteLine("[Odyssey] Door is locked and cannot be unlocked");
                    return;
                }
            }

            // Check if door has conversation (some doors have dialogue)
            if (!string.IsNullOrEmpty(doorComponent.Conversation))
            {
                StartDialogueWithEntity(doorEntity);
                return;
            }

            // Toggle door state
            doorComponent.IsOpen = !doorComponent.IsOpen;
            Console.WriteLine("[Odyssey] Door " + (doorComponent.IsOpen ? "opened" : "closed"));

            // Handle module/area transitions
            if (doorComponent.IsModuleTransition || doorComponent.IsAreaTransition)
            {
                if (_session != null && _session.PlayerEntity != null)
                {
                    // Use ModuleTransitionSystem to handle door transitions
                    // The system will determine if it's a module or area transition
                    var transitionSystem = _session.GetType().GetField("_moduleTransitionSystem",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (transitionSystem != null)
                    {
                        var moduleTransitionSystem = transitionSystem.GetValue(_session) as Odyssey.Kotor.Game.ModuleTransitionSystem;
                        if (moduleTransitionSystem != null)
                        {
                            moduleTransitionSystem.TransitionThroughDoor(doorEntity, _session.PlayerEntity);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handles trigger activation.
        /// </summary>
        private void HandleTriggerActivation(Odyssey.Core.Interfaces.IEntity triggerEntity)
        {
            if (triggerEntity == null)
            {
                return;
            }

            Kotor.Components.TriggerComponent triggerComponent = triggerEntity.GetComponent<Odyssey.Kotor.Components.TriggerComponent>();
            if (triggerComponent == null)
            {
                Console.WriteLine("[Odyssey] Trigger entity has no TriggerComponent");
                return;
            }

            Console.WriteLine("[Odyssey] Trigger activated");

            // Handle module/area transitions
            // Handle module/area transitions
            if (triggerComponent.IsModuleTransition || triggerComponent.IsAreaTransition)
            {
                if (_session != null && _session.PlayerEntity != null)
                {
                    // Use ModuleTransitionSystem to handle trigger transitions
                    var transitionSystem = _session.GetType().GetField("_moduleTransitionSystem",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (transitionSystem != null)
                    {
                        var moduleTransitionSystem = transitionSystem.GetValue(_session) as Odyssey.Kotor.Game.ModuleTransitionSystem;
                        if (moduleTransitionSystem != null)
                        {
                            moduleTransitionSystem.TransitionThroughTrigger(triggerEntity, _session.PlayerEntity);
                        }
                    }
                }
            }

            // Fire OnClick script event for trigger
            if (_world != null && _world.EventBus != null)
            {
                IEntity playerEntity = _session != null ? _session.PlayerEntity : null;
                _world.EventBus.FireScriptEvent(triggerEntity, ScriptEvent.OnClick, playerEntity);
            }

            // Handle trap triggers
            if (triggerComponent.IsTrap && triggerComponent.TrapActive && !triggerComponent.TrapDisarmed)
            {
                // Check if trap should trigger (one-shot traps that already fired should not trigger again)
                if (triggerComponent.TrapOneShot && triggerComponent.HasFired)
                {
                    return;
                }

                // Fire OnTrapTriggered script event
                if (_world != null && _world.EventBus != null)
                {
                    IEntity playerEntity = _session != null ? _session.PlayerEntity : null;
                    _world.EventBus.FireScriptEvent(triggerEntity, ScriptEvent.OnTrapTriggered, playerEntity);
                    triggerComponent.HasFired = true;
                }
            }
        }

        /// <summary>
        /// Finds which room the player is currently in based on position.
        /// </summary>
        private int FindPlayerRoom(Odyssey.Core.Module.RuntimeArea area, System.Numerics.Vector3 playerPosition)
        {
            if (area.Rooms == null || area.Rooms.Count == 0)
            {
                return -1;
            }

            // Find the room closest to the player (simple distance-based approach)
            // In a full implementation, we'd check if player is inside room bounds
            int closestRoomIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < area.Rooms.Count; i++)
            {
                Odyssey.Core.Module.RoomInfo room = area.Rooms[i];
                var roomPos = new System.Numerics.Vector3(room.Position.X, room.Position.Y, room.Position.Z);
                float distance = System.Numerics.Vector3.Distance(playerPosition, roomPos);

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestRoomIndex = i;
                }
            }

            return closestRoomIndex;
        }

        /// <summary>
        /// Handles dialogue node enter events.
        /// </summary>
        private void OnDialogueNodeEnter(object sender, Odyssey.Kotor.Dialogue.DialogueEventArgs e)
        {
            if (e != null && !string.IsNullOrEmpty(e.Text))
            {
                Console.WriteLine($"[Dialogue] {e.Text}");
            }
        }

        /// <summary>
        /// Handles dialogue replies ready events.
        /// </summary>
        private void OnDialogueRepliesReady(object sender, Odyssey.Kotor.Dialogue.DialogueEventArgs e)
        {
            if (e != null && e.State != null && e.State.AvailableReplies != null)
            {
                Console.WriteLine($"[Dialogue] Available replies: {e.State.AvailableReplies.Count}");
                for (int i = 0; i < e.State.AvailableReplies.Count; i++)
                {
                    AuroraEngine.Common.Resource.Generics.DLG.DLGReply reply = e.State.AvailableReplies[i];
                    string replyText = _session.DialogueManager.GetNodeText(reply);
                    Console.WriteLine($"  [{i}] {replyText}");
                }
            }
        }

        /// <summary>
        /// Handles dialogue end events.
        /// </summary>
        private void OnDialogueEnd(object sender, Odyssey.Kotor.Dialogue.DialogueEventArgs e)
        {
            Console.WriteLine("[Dialogue] Conversation ended");
            // Unsubscribe from events
            if (_session != null && _session.DialogueManager != null)
            {
                _session.DialogueManager.OnNodeEnter -= OnDialogueNodeEnter;
                _session.DialogueManager.OnRepliesReady -= OnDialogueRepliesReady;
                _session.DialogueManager.OnConversationEnd -= OnDialogueEnd;
            }
        }

        /// <summary>
        /// Draws the dialogue UI on screen.
        /// </summary>
        private void DrawDialogueUI()
        {
            if (_session == null || _session.DialogueManager == null || !_session.DialogueManager.IsConversationActive)
            {
                return;
            }

            if (_font == null)
            {
                // No font available - can't draw dialogue UI
                return;
            }

            Kotor.Dialogue.DialogueState state = _session.DialogueManager.CurrentState;
            if (state == null)
            {
                return;
            }

            int screenWidth = _graphicsDevice.Viewport.Width;
            int screenHeight = _graphicsDevice.Viewport.Height;

            // Draw dialogue box at bottom of screen
            float dialogueBoxY = screenHeight - 200; // Bottom of screen
            float dialogueBoxHeight = 180;
            float padding = 10;

            // Draw current dialogue text
            if (state.CurrentEntry != null)
            {
                string dialogueText = _session.DialogueManager.GetNodeText(state.CurrentEntry);
                if (!string.IsNullOrEmpty(dialogueText))
                {
                    // Word wrap dialogue text (simple implementation)
                    Vector2 textPos = new Vector2(padding, dialogueBoxY + padding);
                    _spriteBatch.DrawString(_font, dialogueText, textPos, Color.White);
                }
            }

            // Draw available replies
            if (state.AvailableReplies != null && state.AvailableReplies.Count > 0)
            {
                float replyY = dialogueBoxY + 80; // Below dialogue text
                for (int i = 0; i < state.AvailableReplies.Count && i < 9; i++)
                {
                    AuroraEngine.Common.Resource.Generics.DLG.DLGReply reply = state.AvailableReplies[i];
                    string replyText = _session.DialogueManager.GetNodeText(reply);
                    if (string.IsNullOrEmpty(replyText))
                    {
                        replyText = "[Continue]";
                    }

                    string replyLabel = $"[{i + 1}] {replyText}";
                    Vector2 replyPos = new Vector2(padding, replyY + (i * 20));
                    _spriteBatch.DrawString(_font, replyLabel, replyPos, Color.Yellow);
                }

                // Draw instruction text
                string instructionText = "Press 1-9 to select reply, ESC to abort";
                Vector2 instructionPos = new Vector2(padding, dialogueBoxY + dialogueBoxHeight - 20);
                _spriteBatch.DrawString(_font, instructionText, instructionPos, Color.Gray);
            }
        }

        #region Save/Load Menu

        /// <summary>
        /// Refreshes the list of available saves.
        /// </summary>
        private void RefreshSaveList()
        {
            if (_saveSystem == null)
            {
                _availableSaves = new List<Odyssey.Core.Save.SaveGameInfo>();
                return;
            }

            try
            {
                _availableSaves = new List<Odyssey.Core.Save.SaveGameInfo>(_saveSystem.GetSaveList());
                _availableSaves.Sort((a, b) => b.SaveTime.CompareTo(a.SaveTime)); // Most recent first
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] Failed to enumerate saves: " + ex.Message);
                _availableSaves = new List<Odyssey.Core.Save.SaveGameInfo>();
            }
        }

        /// <summary>
        /// Opens the save menu.
        /// </summary>
        private void OpenSaveMenu()
        {
            if (_currentState != GameState.InGame)
            {
                return;
            }

            RefreshSaveList();
            _selectedSaveIndex = 0;
            _isSaving = true;
            _newSaveName = string.Empty;
            _currentState = GameState.SaveMenu;
        }

        /// <summary>
        /// Opens the load menu.
        /// </summary>
        private void OpenLoadMenu()
        {
            if (_currentState != GameState.InGame)
            {
                return;
            }

            RefreshSaveList();
            _selectedSaveIndex = 0;
            _currentState = GameState.LoadMenu;
        }

        /// <summary>
        /// Performs a quick save.
        /// </summary>
        private void QuickSave()
        {
            if (_saveSystem == null || _session == null)
            {
                return;
            }

            string quickSaveName = "QuickSave";
            bool success = _saveSystem.Save(quickSaveName, Odyssey.Core.Save.SaveType.Quick);
            if (success)
            {
                Console.WriteLine("[Odyssey] Quick save successful: " + quickSaveName);
            }
            else
            {
                Console.WriteLine("[Odyssey] Quick save failed: " + quickSaveName);
            }
        }

        /// <summary>
        /// Performs a quick load.
        /// </summary>
        private void QuickLoad()
        {
            if (_saveSystem == null)
            {
                return;
            }

            string quickSaveName = "QuickSave";
            if (_saveSystem.SaveExists(quickSaveName))
            {
                LoadGame(quickSaveName);
            }
            else
            {
                Console.WriteLine("[Odyssey] No quick save found");
            }
        }

        /// <summary>
        /// Loads a game from a save name.
        /// </summary>
        private void LoadGame(string saveName)
        {
            if (_saveSystem == null || _session == null)
            {
                return;
            }

            try
            {
                Console.WriteLine("[Odyssey] Loading game: " + saveName);
                bool success = _saveSystem.Load(saveName);
                if (success)
                {
                    Console.WriteLine("[Odyssey] Game loaded successfully");
                    _currentState = GameState.InGame;
                }
                else
                {
                    Console.WriteLine("[Odyssey] Failed to load game: " + saveName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] Error loading game: " + ex.Message);
            }
        }

        /// <summary>
        /// Updates the save menu.
        /// </summary>
        private void UpdateSaveMenu(float deltaTime, IKeyboardState keyboardState, IMouseState mouseState)
        {
            var inputManager = _graphicsBackend.InputManager;
            var currentKeyboard = inputManager.KeyboardState;
            var currentMouse = inputManager.MouseState;

            // Handle ESC to cancel
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Escape))
            {
                _currentState = GameState.InGame;
                return;
            }

            // Navigation
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Up))
            {
                _selectedSaveIndex = Math.Max(0, _selectedSaveIndex - 1);
            }
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Down))
            {
                _selectedSaveIndex = Math.Min(_availableSaves.Count, _selectedSaveIndex + 1);
            }

            // Selection
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Enter))
            {
                if (_selectedSaveIndex < _availableSaves.Count)
                {
                    // Overwrite existing save
                    string saveName = _availableSaves[_selectedSaveIndex].Name;
                    if (_saveSystem != null)
                    {
                        _saveSystem.Save(saveName, Odyssey.Core.Save.SaveType.Manual);
                        RefreshSaveList();
                        _currentState = GameState.InGame;
                    }
                }
                else
                {
                    // New save - prompt for name (simplified: use timestamp)
                    string newSaveName = "Save_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    if (_saveSystem != null)
                    {
                        _saveSystem.Save(newSaveName, Odyssey.Core.Save.SaveType.Manual);
                        RefreshSaveList();
                        _currentState = GameState.InGame;
                    }
                }
            }

            _previousKeyboardState = currentKeyboard;
        }

        /// <summary>
        /// Updates the load menu.
        /// </summary>
        private void UpdateLoadMenu(float deltaTime, IKeyboardState keyboardState, IMouseState mouseState)
        {
            var inputManager = _graphicsBackend.InputManager;
            var currentKeyboard = inputManager.KeyboardState;
            var currentMouse = inputManager.MouseState;

            // Handle ESC to cancel
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Escape))
            {
                _currentState = GameState.InGame;
                return;
            }

            // Navigation
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Up))
            {
                _selectedSaveIndex = Math.Max(0, _selectedSaveIndex - 1);
            }
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Down))
            {
                _selectedSaveIndex = Math.Min(_availableSaves.Count - 1, _selectedSaveIndex + 1);
            }

            // Selection
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Enter))
            {
                if (_selectedSaveIndex >= 0 && _selectedSaveIndex < _availableSaves.Count)
                {
                    string saveName = _availableSaves[_selectedSaveIndex].Name;
                    LoadGame(saveName);
                }
            }

            _previousKeyboardState = currentKeyboard;
        }

        /// <summary>
        /// Draws the save menu.
        /// </summary>
        private void DrawSaveMenu()
        {
            _graphicsDevice.Clear(new Color(20, 20, 30, 255));

            if (_spriteBatch == null || _font == null || _menuTexture == null)
            {
                return;
            }

            _spriteBatch.Begin();

            int viewportWidth = _graphicsDevice.Viewport.Width;
            int viewportHeight = _graphicsDevice.Viewport.Height;

            // Title
            string title = "Save Game";
            Vector2 titleSize = _font.MeasureString(title);
            Vector2 titlePos = new Vector2((viewportWidth - titleSize.X) / 2, 50);
            _spriteBatch.DrawString(_font, title, titlePos, new Color(255, 255, 255, 255));

            // Save list
            int startY = 150;
            int itemHeight = 60;
            int itemSpacing = 10;
            int maxVisible = Math.Min(10, (viewportHeight - startY - 100) / (itemHeight + itemSpacing));
            int startIdx = Math.Max(0, _selectedSaveIndex - maxVisible / 2);
            int endIdx = Math.Min(_availableSaves.Count + 1, startIdx + maxVisible);

            for (int i = startIdx; i < endIdx; i++)
            {
                int y = startY + (i - startIdx) * (itemHeight + itemSpacing);
                bool isSelected = (i == _selectedSaveIndex);
                Color bgColor = isSelected ? new Color(100, 100, 150) : new Color(50, 50, 70);

                Rectangle itemRect = new Rectangle(100, y, viewportWidth - 200, itemHeight);
                _spriteBatch.Draw(_menuTexture, itemRect, bgColor);

                if (i < _availableSaves.Count)
                {
                    Odyssey.Core.Save.SaveGameInfo save = _availableSaves[i];
                    string saveText = $"{save.Name} - {save.ModuleName} - {save.SaveTime:g}";
                    Vector2 textPos = new Vector2(itemRect.X + 10, itemRect.Y + (itemHeight - _font.LineSpacing) / 2);
                    _spriteBatch.DrawString(_font, saveText, textPos, new Color(255, 255, 255, 255));
                }
                else
                {
                    string newSaveText = "New Save";
                    Vector2 textPos = new Vector2(itemRect.X + 10, itemRect.Y + (itemHeight - _font.LineSpacing) / 2);
                    _spriteBatch.DrawString(_font, newSaveText, textPos, new Color(211, 211, 211, 255));
                }
            }

            // Instructions
            string instructions = "Select a save slot or create a new save. Press Escape to cancel.";
            Vector2 instSize = _font.MeasureString(instructions);
            Vector2 instPos = new Vector2((viewportWidth - instSize.X) / 2, viewportHeight - 50);
            _spriteBatch.DrawString(_font, instructions, instPos, new Color(211, 211, 211, 255));

            _spriteBatch.End();
        }

        /// <summary>
        /// Draws the load menu.
        /// </summary>
        private void DrawLoadMenu()
        {
            _graphicsDevice.Clear(new Color(20, 20, 30, 255));

            if (_spriteBatch == null || _font == null || _menuTexture == null)
            {
                return;
            }

            _spriteBatch.Begin();

            int viewportWidth = _graphicsDevice.Viewport.Width;
            int viewportHeight = _graphicsDevice.Viewport.Height;

            // Title
            string title = "Load Game";
            Vector2 titleSize = _font.MeasureString(title);
            Vector2 titlePos = new Vector2((viewportWidth - titleSize.X) / 2, 50);
            _spriteBatch.DrawString(_font, title, titlePos, new Color(255, 255, 255, 255));

            // Save list
            int startY = 150;
            int itemHeight = 60;
            int itemSpacing = 10;
            int maxVisible = Math.Min(10, (viewportHeight - startY - 100) / (itemHeight + itemSpacing));
            int startIdx = Math.Max(0, _selectedSaveIndex - maxVisible / 2);
            int endIdx = Math.Min(_availableSaves.Count, startIdx + maxVisible);

            for (int i = startIdx; i < endIdx; i++)
            {
                int y = startY + (i - startIdx) * (itemHeight + itemSpacing);
                bool isSelected = (i == _selectedSaveIndex);
                Color bgColor = isSelected ? new Color(100, 100, 150) : new Color(50, 50, 70);

                Rectangle itemRect = new Rectangle(100, y, viewportWidth - 200, itemHeight);
                _spriteBatch.Draw(_menuTexture, itemRect, bgColor);

                Odyssey.Core.Save.SaveGameInfo save = _availableSaves[i];
                string saveText = $"{save.Name} - {save.ModuleName} - {save.SaveTime:g}";
                Vector2 textPos = new Vector2(itemRect.X + 10, itemRect.Y + (itemHeight - _font.LineSpacing) / 2);
                _spriteBatch.DrawString(_font, saveText, textPos, new Color(255, 255, 255, 255));
            }

            // Instructions
            string instructions = "Select a save to load. Press Escape to cancel.";
            Vector2 instSize = _font.MeasureString(instructions);
            Vector2 instPos = new Vector2((viewportWidth - instSize.X) / 2, viewportHeight - 50);
            _spriteBatch.DrawString(_font, instructions, instPos, new Color(211, 211, 211, 255));

            _spriteBatch.End();
        }

        #endregion

        public void Dispose()
        {
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
                _spriteBatch = null;
            }

            if (_menuTexture != null)
            {
                _menuTexture.Dispose();
                _menuTexture = null;
            }

            if (_graphicsBackend != null)
            {
                _graphicsBackend.Dispose();
            }
        }
    }
}

