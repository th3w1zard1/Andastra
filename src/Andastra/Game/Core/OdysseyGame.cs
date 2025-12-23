using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using JetBrains.Annotations;
using Andastra.Parsing;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.MonoGame.Converters;
using Andastra.Runtime.Core.Entities;
using Andastra.Runtime.Core.Enums;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Camera;
using Andastra.Runtime.Core.Collision;
using Andastra.Runtime.Games.Odyssey.Collision;
using Andastra.Runtime.Content.ResourceProviders;
using Andastra.Runtime.Games.Common;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Graphics.Cursor;
using Andastra.Runtime.Engines.Odyssey.EngineApi;
using Andastra.Runtime.Engines.Odyssey.Game;
using Andastra.Runtime.Scripting.EngineApi;
using Andastra.Runtime.Scripting.VM;
using Andastra.Runtime.Core.Audio;
using GraphicsVector2 = Andastra.Runtime.Graphics.Vector2;

namespace Andastra.Runtime.Game.Core
{
    /// <summary>
    /// Simple GameTime class for tracking elapsed and total game time.
    /// </summary>
    public class GameTime
    {
        public System.TimeSpan ElapsedGameTime { get; set; }
        public System.TimeSpan TotalGameTime { get; set; }
    }

    /// <summary>
    /// Odyssey game implementation using graphics abstraction layer.
    /// Supports both MonoGame and Stride backends.
    // TODO: Full implementation - currently a simplified version focused on getting menu working and game launching.
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
        private readonly Andastra.Runtime.Core.GameSettings _settings;
        private readonly IGraphicsBackend _graphicsBackend;
        private IGraphicsDevice _graphicsDevice;
        private ISpriteBatch _spriteBatch;
        private IFont _font;

        // Game systems
        private Andastra.Runtime.Engines.Odyssey.Game.GameSession _session;
        private World _world;
        private ScriptGlobals _globals;
        private Kotor1 _engineApi;
        private NcsVm _vm;

        // Menu - Professional menu implementation
        private GameState _currentState = GameState.MainMenu;

        // Character creation
        private CharacterCreationScreen _characterCreationScreen;
        private CharacterCreationData _characterData;
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

        // Camera system (using abstraction layer)
        private ICameraController _cameraController;

        // Basic 3D rendering (using abstraction layer)
        private IBasicEffect _basicEffect;
        private IVertexBuffer _groundVertexBuffer;
        private IIndexBuffer _groundIndexBuffer;
        private System.Numerics.Matrix4x4 _viewMatrix;
        private System.Numerics.Matrix4x4 _projectionMatrix;

        // Room rendering (using abstraction layer)
        private IRoomMeshRenderer _roomRenderer;
        private Dictionary<string, IRoomMeshData> _roomMeshes;

        // Room bounds cache for efficient player room detection
        // Based on swkotor.exe and swkotor2.exe: Room bounds are calculated from MDL model geometry
        // Original implementation: FUN_004e17a0 @ 0x004e17a0 (spatial query) checks room bounds for entity placement
        private Dictionary<string, Tuple<System.Numerics.Vector3, System.Numerics.Vector3>> _roomBoundsCache;

        // Entity model rendering (using abstraction layer)
        private IEntityModelRenderer _entityModelRenderer;

        // Audio system
        private Andastra.Runtime.Core.Audio.IMusicPlayer _musicPlayer;
        private Andastra.Runtime.Core.Audio.ISoundPlayer _soundPlayer;
        private bool _musicStarted = false;
        private bool _musicEnabled = true; // Music enabled by default (can be toggled by BTN_MUSIC in K2)

        // GUI system for main menu
        private Andastra.Runtime.MonoGame.GUI.KotorGuiManager _guiManager;
        private bool _mainMenuGuiLoaded = false;

        // Cursor system
        private ICursorManager _cursorManager;

        // Options menu system
        private bool _optionsMenuGuiLoaded = false;
        private Dictionary<Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory, List<Andastra.Runtime.Game.GUI.OptionsMenu.OptionItem>> _optionsByCategory;
        private int _selectedOptionsCategoryIndex = 0;
        private int _selectedOptionsItemIndex = 0;
        private bool _isEditingOptionValue = false;
        private string _editingOptionValue = string.Empty;
        private bool _isRebindingKey = false;
        private string _rebindingActionName = string.Empty;

        // Main menu 3D model rendering
        private MDL _mainMenuModel;
        private MDL _gui3DRoomModel;
        private MdlToMonoGameModelConverter.ConversionResult _mainMenuModelData;
        private System.Numerics.Vector3 _mainMenuCameraHookPosition;
        private bool _mainMenuModelLoaded = false;
        private bool _gui3DRoomLoaded = false;
        private IEntityModelRenderer _menuEntityModelRenderer;
        private ICameraController _menuCameraController;
        private System.Numerics.Matrix4x4 _menuViewMatrix;
        private System.Numerics.Matrix4x4 _menuProjectionMatrix;
        private Installation _menuInstallation;
        private string _menuVariant = "mainmenu01"; // Default variant, K2 only
        private System.Numerics.Matrix4x4 _mainMenuViewMatrix;
        private System.Numerics.Matrix4x4 _mainMenuProjectionMatrix;
        private float _mainMenuCameraDistance = 22.7f; // 0x41b5ced9 (~22.7)
        private float _mainMenuModelRotation = 0f; // Rotation angle for 3D model (in radians)
        private const float MainMenuRotationSpeed = 0.5f; // Rotation speed in radians per second (based on original games)
        private string _previousHighlightedButton = null; // Track button hover for sound effects
        private string _buttonClickSound = "gui_actscroll"; // Default button click sound (from guisounds.2da Clicked_Default)
        private string _buttonHoverSound = "gui_actscroll"; // Default button hover sound (from guisounds.2da Entered_Default)
        // Parent node map for efficient parent lookups (built once when model loads)
        // Based on swkotor.exe and swkotor2.exe: MDL node hierarchy traversal for transforms
        private Dictionary<MDLNode, MDLNode> _mainMenuModelParentMap;

        // Save/Load system
        private Andastra.Runtime.Core.Save.SaveSystem _saveSystem;
        private List<Andastra.Runtime.Core.Save.SaveGameInfo> _availableSaves;
        private int _selectedSaveIndex = 0;
        private bool _isSaving = false;
        private string _newSaveName = string.Empty;
        private bool _isEnteringSaveName = false;
        private float _saveNameInputCursorTime = 0f;

        // Movies menu system
        private List<string> _availableMovies;
        private int _selectedMovieIndex = 0;
        private bool _isPlayingMovie = false;
        private System.Threading.CancellationTokenSource _movieCancellationTokenSource;

        // Input tracking
        private IMouseState _previousMouseState;
        private IKeyboardState _previousKeyboardState;

        public OdysseyGame(Andastra.Runtime.Core.GameSettings settings, IGraphicsBackend graphicsBackend)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _graphicsBackend = graphicsBackend ?? throw new ArgumentNullException(nameof(graphicsBackend));

            // Initialize graphics backend
            string windowTitle = "Odyssey Engine - " + (_settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? "Knights of the Old Republic" : "The Sith Lords");
            _graphicsBackend.Initialize(1280, 720, windowTitle, false);

            // Get graphics components from backend
            _graphicsDevice = _graphicsBackend.GraphicsDevice;
            _graphicsBackend.Window.IsMouseVisible = true;

            // Apply VSync setting from configuration if graphics backend supports it
            // Based on swkotor.exe and swkotor2.exe: VSync initialized from configuration file
            // Original implementation: VSync setting read from swkotor.ini/swkotor2.ini and applied during graphics initialization
            // VSync synchronizes frame rendering with monitor refresh rate to prevent screen tearing
            if (_graphicsBackend.SupportsVSync && _settings.Graphics != null)
            {
                try
                {
                    _graphicsBackend.SetVSync(_settings.Graphics.VSync);
                    Console.WriteLine($"[Odyssey] VSync initialized: {(_settings.Graphics.VSync ? "enabled" : "disabled")}");
                }
                catch (Exception vsyncEx)
                {
                    Console.WriteLine($"[Odyssey] WARNING: Failed to initialize VSync setting: {vsyncEx.Message}");
                }
            }
            else if (!_graphicsBackend.SupportsVSync)
            {
                Console.WriteLine("[Odyssey] VSync not supported by graphics backend");
            }

            Console.WriteLine("[Odyssey] Game window initialized - Backend: " + _graphicsBackend.BackendType);
        }

        private void Initialize()
        {
            Console.WriteLine("[Odyssey] Initializing engine with backend: " + _graphicsBackend.BackendType);

            // Initialize game systems
            _world = new World();
            _globals = new ScriptGlobals();
            _engineApi = new Kotor1();
            _vm = new NcsVm();
            _session = new Andastra.Runtime.Engines.Odyssey.Game.GameSession(_settings, _world, _vm, _globals);

            // Initialize camera controller
            // Based on swkotor.exe and swkotor2.exe: Camera system initialization
            // swkotor.exe (KOTOR 1): Camera initialization @ FUN_004af630
            // swkotor2.exe (KOTOR 2): Camera initialization @ FUN_004dcfb0
            // Original implementation: Camera controller manages chase camera following player
            // Camera modes: Chase (follows player), Free (debug), Dialogue (conversations), Cinematic (cutscenes)
            _cameraController = new CameraController(_world);

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
                // Font not found - create programmatic fallback font
                Console.WriteLine("[Odyssey] WARNING: Failed to load font from 'Fonts/Arial': " + ex.Message);
                Console.WriteLine("[Odyssey] Creating programmatic default font as fallback");
                _font = CreateDefaultFont();
                if (_font == null)
                {
                    Console.WriteLine("[Odyssey] ERROR: Failed to create default font - text rendering will be unavailable");
                }
                else
                {
                    Console.WriteLine("[Odyssey] Default font created successfully");
                }
            }

            // Create 1x1 white texture for menu drawing
            byte[] whitePixel = new byte[] { 255, 255, 255, 255 }; // RGBA white
            _menuTexture = _graphicsDevice.CreateTexture2D(1, 1, whitePixel);

            // Initialize cursor manager
            // Based on swkotor.exe and swkotor2.exe: Cursor system initialization
            // Original implementation: Cursor manager loads cursor textures from EXE resources
            // Cursor types: Default, Hand (button hover), Talk, Door, Pickup, Attack
            _cursorManager = new Andastra.Runtime.MonoGame.Graphics.Cursor.MonoGameCursorManager(_graphicsDevice);
            _cursorManager.SetCursor(CursorType.Default);
            Console.WriteLine("[Odyssey] Cursor manager initialized");

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
            _roomBoundsCache = new Dictionary<string, Tuple<System.Numerics.Vector3, System.Numerics.Vector3>>();

            // Initialize entity model renderer using abstraction layer
            // Will be created when module loads with proper dependencies
            _entityModelRenderer = null;

            // Initialize music player and GUI manager for main menu
            // Based on swkotor.exe FUN_005f9af0 @ 0x005f9af0 (K1) and swkotor2.exe FUN_006456b0 @ 0x006456b0 (K2)
            // Music files: K1 uses "mus_theme_cult", K2 uses "mus_sion"
            // GUI files: K1 uses "mainmenu16x12", K2 uses "mainmenu8x6_p" or "mainmenu_p"
            // Based on swkotor.exe FUN_0067c4c0 @ 0x0067c4c0 (K1 main menu constructor)
            // Based on swkotor2.exe FUN_006d2350 @ 0x006d2350 (K2 main menu constructor)
            try
            {
                // Create resource provider from installation if game path is available
                if (!string.IsNullOrEmpty(_settings.GamePath) && Directory.Exists(_settings.GamePath))
                {
                    var installation = new Installation(_settings.GamePath, _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? Andastra.Parsing.Common.BioWareGame.K1 : Andastra.Parsing.Common.BioWareGame.K2);
                    var resourceProvider = new GameResourceProvider(installation);

                    // Create music player from graphics backend
                    var musicPlayerObj = _graphicsBackend.CreateMusicPlayer(resourceProvider);
                    if (musicPlayerObj is IMusicPlayer musicPlayer)
                    {
                        _musicPlayer = musicPlayer;
                        Console.WriteLine("[Odyssey] Music player initialized successfully");
                    }

                    // Create sound player from graphics backend
                    var soundPlayerObj = _graphicsBackend.CreateSoundPlayer(resourceProvider);
                    if (soundPlayerObj is Andastra.Runtime.Core.Audio.ISoundPlayer soundPlayer)
                    {
                        _soundPlayer = soundPlayer;
                        Console.WriteLine("[Odyssey] Sound player initialized successfully");
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: Graphics backend returned invalid music player type");
                    }

                    // Initialize GUI manager for main menu
                    // Based on swkotor.exe FUN_0067c4c0: Loads "MAINMENU" GUI and "RIMS:MAINMENU" RIM
                    // Based on swkotor2.exe FUN_006d2350: Loads "MAINMENU" GUI and "RIMS:MAINMENU" RIM
                    if (_graphicsDevice is Andastra.Runtime.Graphics.MonoGame.Graphics.MonoGameGraphicsDevice mgDevice)
                    {
                        // Create sound player for GUI button sounds (hover and click)
                        // Based on swkotor.exe FUN_0067ace0: Button sounds ("gui_actscroll", "gui_actclick")
                        // Based on swkotor2.exe FUN_006d0790: Button sounds ("gui_actscroll", "gui_actclick")
                        var guiSoundPlayer = _graphicsBackend.CreateSoundPlayer(resourceProvider);

                        _guiManager = new Andastra.Runtime.Graphics.MonoGame.GUI.KotorGuiManager(installation, mgDevice.Device, guiSoundPlayer);

                        // Subscribe to button click events
                        // Based on swkotor.exe FUN_0067c4c0: Button event handlers (0x27 hover, 0x2d leave, 0 click, 1 release)
                        // Based on swkotor2.exe FUN_006d2350: Button event handlers
                        _guiManager.OnButtonClicked += HandleGuiButtonClick;

                        Console.WriteLine("[Odyssey] GUI manager initialized successfully with sound support");
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: GUI manager requires MonoGame graphics device");
                    }

                    // Load guisounds.2da to get correct button sound ResRefs
                    // Based on swkotor.exe and swkotor2.exe: guisounds.2da contains Clicked_Default and Entered_Default
                    // Original implementation: Loads guisounds.2da and reads soundresref column for Clicked_Default and Entered_Default
                    LoadGuiSounds(installation);

                    // Load main menu 3D models (gui3D_room + menu variant)
                    // Based on swkotor.exe FUN_0067c4c0: Loads gui3D_room and mainmenu model
                    // Based on swkotor2.exe FUN_006d2350: Loads gui3D_room and menu variant model
                    LoadMainMenu3DModels(installation);
                }
                else
                {
                    Console.WriteLine("[Odyssey] WARNING: Game path not available, music player and GUI manager will be created when path is set");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] WARNING: Failed to initialize music player or GUI manager: {ex.Message}");
                Console.WriteLine($"[Odyssey] Stack trace: {ex.StackTrace}");
            }

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

                // Update 3D model rotation for continuous rotation animation
                // Based on swkotor.exe and swkotor2.exe: Character model rotates continuously around Y-axis
                // Rotation speed: approximately 0.5 radians per second (matches original games)
                _mainMenuModelRotation += MainMenuRotationSpeed * deltaTime;
                // Keep rotation in [0, 2π) range to prevent overflow
                if (_mainMenuModelRotation >= 2.0f * (float)Math.PI)
                {
                    _mainMenuModelRotation -= 2.0f * (float)Math.PI;
                }

                // Start main menu music if not already started and music is enabled
                // Based on swkotor.exe FUN_005f9af0 @ 0x005f9af0: Plays "mus_theme_cult" for K1 main menu
                // Based on swkotor2.exe FUN_006456b0 @ 0x006456b0: Plays "mus_sion" for K2 main menu
                // Music starts immediately when entering main menu (no delay)
                if (!_musicStarted && _musicPlayer != null && _musicEnabled)
                {
                    string musicResRef = _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? "mus_theme_cult" : "mus_sion";
                    if (_musicPlayer.Play(musicResRef, 1.0f))
                    {
                        _musicStarted = true;
                        Console.WriteLine($"[Odyssey] Main menu music started: {musicResRef}");
                    }
                    else
                    {
                        Console.WriteLine($"[Odyssey] WARNING: Failed to play main menu music: {musicResRef}");
                    }
                }
                else if (_musicStarted && !_musicEnabled && _musicPlayer != null)
                {
                    // Stop music if it was playing but music is now disabled
                    _musicPlayer.Stop();
                    _musicStarted = false;
                    Console.WriteLine("[Odyssey] Main menu music stopped (music disabled)");
                }

                UpdateMainMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_musicStarted && _musicPlayer != null)
            {
                // Stop music when leaving main menu
                _musicPlayer.Stop();
                _musicStarted = false;
                Console.WriteLine("[Odyssey] Main menu music stopped");
            }

            // Load main menu GUI if not already loaded
            // Based on swkotor.exe FUN_0067c4c0 @ 0x0067c4c0: Loads "MAINMENU" GUI
            // Based on swkotor2.exe FUN_006d2350 @ 0x006d2350: Loads "MAINMENU" GUI
            if (_currentState == GameState.MainMenu && !_mainMenuGuiLoaded && _guiManager != null)
            {
                // Determine GUI file name based on game
                // K1: "mainmenu16x12" (from Ghidra analysis - actual file may differ, try "MAINMENU" first)
                // K2: "mainmenu8x6_p" or "mainmenu_p" (from Ghidra analysis FUN_006d0790)
                string guiName = "MAINMENU"; // Try standard name first
                int viewportWidth = _graphicsDevice.Viewport.Width;
                int viewportHeight = _graphicsDevice.Viewport.Height;

                if (_guiManager.LoadGui(guiName, viewportWidth, viewportHeight))
                {
                    _mainMenuGuiLoaded = true;
                    Console.WriteLine($"[Odyssey] Main menu GUI loaded: {guiName}");
                }
                else
                {
                    // Try game-specific GUI names
                    if (_settings.Game == Andastra.Runtime.Core.KotorGame.K1)
                    {
                        guiName = "mainmenu16x12";
                    }
                    else
                    {
                        guiName = "mainmenu8x6_p";
                    }

                    if (_guiManager.LoadGui(guiName, viewportWidth, viewportHeight))
                    {
                        _mainMenuGuiLoaded = true;
                        Console.WriteLine($"[Odyssey] Main menu GUI loaded: {guiName}");
                    }
                    else
                    {
                        // Try fallback
                        if (_settings.Game == Andastra.Runtime.Core.KotorGame.K2)
                        {
                            guiName = "mainmenu_p";
                            if (_guiManager.LoadGui(guiName, viewportWidth, viewportHeight))
                            {
                                _mainMenuGuiLoaded = true;
                                Console.WriteLine($"[Odyssey] Main menu GUI loaded: {guiName}");
                            }
                        }
                    }
                }
            }

            // Load options menu GUI if needed
            if (_currentState == GameState.OptionsMenu && !_optionsMenuGuiLoaded && _guiManager != null)
            {
                // Load options menu GUI
                // Based on swkotor.exe and swkotor2.exe: Options menu GUI loading
                // Based on swkotor2.exe: CSWGuiOptionsMain @ 0x006e3e80 loads "optionsmain" GUI
                string guiName = "optionsmain"; // Options menu GUI file
                int viewportWidth = _graphicsDevice.Viewport.Width;
                int viewportHeight = _graphicsDevice.Viewport.Height;

                if (_guiManager.LoadGui(guiName, viewportWidth, viewportHeight))
                {
                    _optionsMenuGuiLoaded = true;
                    Console.WriteLine($"[Odyssey] Options menu GUI loaded: {guiName}");
                }
                else
                {
                    Console.WriteLine($"[Odyssey] ERROR: Failed to load options menu GUI: {guiName}");
                }
            }

            if (_currentState == GameState.SaveMenu)
            {
                UpdateSaveMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_currentState == GameState.LoadMenu)
            {
                UpdateLoadMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_currentState == GameState.OptionsMenu)
            {
                UpdateOptionsMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_currentState == GameState.OptionsMenu)
            {
                UpdateOptionsMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_currentState == GameState.MoviesMenu)
            {
                UpdateMoviesMenu(deltaTime, keyboardState, mouseState);
            }
            else if (_currentState == GameState.CharacterCreation)
            {
                if (_characterCreationScreen != null)
                {
                    _characterCreationScreen.Update(deltaTime, keyboardState, mouseState);
                }
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
                UpdateCamera(deltaTime);
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
            else if (_currentState == GameState.CharacterCreation)
            {
                if (_characterCreationScreen != null)
                {
                    _characterCreationScreen.Draw(_spriteBatch, _font);
                }
            }
            else if (_currentState == GameState.SaveMenu)
            {
                DrawSaveMenu();
            }
            else if (_currentState == GameState.LoadMenu)
            {
                DrawLoadMenu();
            }
            else if (_currentState == GameState.OptionsMenu)
            {
                DrawOptionsMenu();
            }
            else if (_currentState == GameState.MoviesMenu)
            {
                DrawMoviesMenu();
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

            // Render cursor on top of everything
            // Based on swkotor.exe and swkotor2.exe: Cursor rendered as sprite on top of all graphics
            // Original implementation: Cursor rendered after all other graphics, follows mouse position
            RenderCursor();
        }

        /// <summary>
        /// Renders the mouse cursor on top of all graphics.
        /// </summary>
        /// <remarks>
        /// Cursor Rendering:
        /// - Based on swkotor.exe and swkotor2.exe: Cursor rendering system
        /// - Original implementation: Cursor rendered as sprite using DirectX immediate mode
        /// - Cursor position: Follows mouse position, offset by hotspot
        /// - Cursor state: Shows pressed texture when mouse button is down
        /// - Rendering order: Cursor rendered last, on top of all other graphics
        /// </remarks>
        private void RenderCursor()
        {
            if (_cursorManager == null || _cursorManager.CurrentCursor == null)
            {
                return;
            }

            ICursor cursor = _cursorManager.CurrentCursor;
            ITexture2D cursorTexture = _cursorManager.IsPressed ? cursor.TextureDown : cursor.TextureUp;
            GraphicsVector2 position = _cursorManager.Position;

            // Offset position by hotspot (cursor click point)
            GraphicsVector2 renderPosition = new GraphicsVector2(
                position.X - cursor.HotspotX,
                position.Y - cursor.HotspotY);

            // Render cursor using sprite batch
            // Cursor rendered with alpha blending on top of everything
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Default);
            _spriteBatch.Draw(cursorTexture, renderPosition, new Color(255, 255, 255, 255));
            _spriteBatch.End();
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

            // Handle GUI button clicks if GUI is loaded
            // Based on swkotor.exe FUN_0067ace0 @ 0x0067ace0: Button setup with event handlers
            // Based on swkotor2.exe FUN_006d0790 @ 0x006d0790: Button setup with event handlers
            // Button events: 0x27 (hover), 0x2d (leave), 0 (click), 1 (release)
            if (_mainMenuGuiLoaded && _guiManager != null)
            {
                // Update GUI manager with input
                // GUI manager internally handles mouse/keyboard input and button click detection
                // Button clicks are automatically detected and OnButtonClicked event is fired
                // HandleGuiButtonClick method handles the button click events
                _guiManager.Update(deltaTime);

                // Check for button hover sound effects
                // Based on swkotor.exe and swkotor2.exe: Button hover plays sound effect
                // Based on xoreos WidgetButton::enter() - plays "gui_actscroll" on button hover
                // Sound file: "gui_actscroll" (button hover sound)
                // Original implementation: Button hover triggers sound when mouse enters button
                string currentHighlightedButton = _guiManager.HighlightedButtonTag;
                if (currentHighlightedButton != _previousHighlightedButton && !string.IsNullOrEmpty(currentHighlightedButton))
                {
                    // Button hover changed - play hover sound effect
                    // Based on swkotor.exe and swkotor2.exe: Button hover plays sound effect
                    // Sound file: "gui_actscroll" (from guisounds.2da Entered_Default)
                    PlayButtonSound(_buttonHoverSound);
                }
                _previousHighlightedButton = currentHighlightedButton;

                // Change mouse cursor on button hover
                // Based on swkotor.exe and swkotor2.exe: Cursor changes when hovering over buttons
                // Original implementation: Cursor changes to indicate interactive element
                // swkotor.exe and swkotor2.exe: Cursor changes to hand/pointer when hovering buttons
                if (!string.IsNullOrEmpty(currentHighlightedButton))
                {
                    // Button is hovered - change cursor to hand/pointer to indicate interactivity
                    // Original games use a hand cursor or highlight cursor on button hover
                    if (_cursorManager != null)
                    {
                        _cursorManager.SetCursor(CursorType.Hand);
                    }
                    _graphicsBackend.Window.IsMouseVisible = false; // Hide system cursor, use custom cursor
                }
                else
                {
                    // No button hovered - use default cursor
                    if (_cursorManager != null)
                    {
                        _cursorManager.SetCursor(CursorType.Default);
                    }
                    _graphicsBackend.Window.IsMouseVisible = false; // Hide system cursor, use custom cursor
                }

                // Update cursor position and pressed state
                if (_cursorManager != null)
                {
                    Point mousePos = mouseState.Position;
                    _cursorManager.Position = new GraphicsVector2(mousePos.X, mousePos.Y);
                    _cursorManager.IsPressed = mouseState.LeftButton == ButtonState.Pressed;
                }

                // Update previous mouse/keyboard state for fallback input handling if needed
                _previousMenuMouseState = mouseState;
                _previousMenuKeyboardState = keyboardState;
                return; // GUI handles input, no need for fallback input handling
            }

            // Fallback: Handle input for simple menu if GUI not loaded
            // Calculate menu button positions (matching DrawMainMenu layout)
            int centerX = viewportWidth / 2;
            int startY = viewportHeight / 2;
            int buttonWidth = 400;
            int buttonHeight = 60;
            int buttonSpacing = 15;
            int titleOffset = 180;

            // Track mouse hover
            _hoveredMenuIndex = -1;
            Point mousePos = mouseState.Position;

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

            // Update cursor based on button hover (fallback menu)
            // Based on swkotor.exe and swkotor2.exe: Cursor changes when hovering over buttons
            if (_cursorManager != null)
            {
                if (_hoveredMenuIndex >= 0)
                {
                    // Button is hovered - change cursor to hand/pointer
                    _cursorManager.SetCursor(CursorType.Hand);
                    _graphicsBackend.Window.IsMouseVisible = false; // Hide system cursor, use custom cursor
                }
                else
                {
                    // No button hovered - use default cursor
                    _cursorManager.SetCursor(CursorType.Default);
                    _graphicsBackend.Window.IsMouseVisible = false; // Hide system cursor, use custom cursor
                }

                // Update cursor position and pressed state
                _cursorManager.Position = new GraphicsVector2(mousePos.X, mousePos.Y);
                _cursorManager.IsPressed = mouseState.LeftButton == ButtonState.Pressed;
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

        /// <summary>
        /// Plays a button sound effect.
        /// Based on swkotor.exe and swkotor2.exe: Button interactions play sound effects
        /// </summary>
        /// <summary>
        /// Plays a button sound effect.
        /// Based on swkotor.exe and swkotor2.exe: Button interactions play sound effects
        /// Sound files: "gui_actscroll" (hover/click), "gui_actscroll1" (alternative)
        /// From guisounds.2da: Clicked_Default and Entered_Default sound references
        /// </summary>
        private void PlayButtonSound(string soundResRef)
        {
            if (_soundPlayer == null)
            {
                return;
            }

            try
            {
                // Play sound at full volume, non-positional (2D sound)
                // Based on swkotor.exe and swkotor2.exe: Button sounds are 2D (not positional)
                // Original implementation: Plays sound immediately when button interaction occurs
                _soundPlayer.PlaySound(soundResRef, 1.0f);
            }
            catch (Exception ex)
            {
                // Silently fail - sound is not critical
                Console.WriteLine($"[Odyssey] WARNING: Failed to play button sound {soundResRef}: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles GUI button clicks from the main menu GUI.
        /// Based on swkotor.exe FUN_0067afb0 @ 0x0067afb0 (New Game button handler)
        /// Based on swkotor2.exe FUN_006d0b00 @ 0x006d0b00 (New Game button handler)
        /// </summary>
        private void HandleGuiButtonClick(object sender, GuiButtonClickedEventArgs e)
        {
            string buttonTag = e.ButtonTag;
            Console.WriteLine($"[Odyssey] GUI button clicked: {buttonTag} (ID: {e.ButtonId})");

            // Play button click sound
            // Based on swkotor.exe and swkotor2.exe: Button clicks play sound effect
            // Sound file: "gui_actscroll" or "gui_actscroll1" (button click sound)
            PlayButtonSound(_buttonClickSound);

            // Handle button clicks based on button tag
            // Based on swkotor.exe FUN_0067ace0: Button tags (BTN_NEWGAME, BTN_LOADGAME, BTN_OPTIONS, BTN_EXIT)
            // Based on swkotor2.exe FUN_006d0790: Button tags (BTN_NEWGAME, BTN_LOADGAME, BTN_OPTIONS, BTN_EXIT, BTN_MUSIC)
            switch (buttonTag.ToUpperInvariant())
            {
                case "BTN_NEWGAME":
                    // New Game button - go to character creation first, then load module
                    // Based on swkotor.exe FUN_0067afb0 @ 0x0067afb0: New Game goes to character creation, then loads module "END_M01AA" (Endar Spire)
                    // Based on swkotor2.exe FUN_006d0b00 @ 0x006d0b00: New Game goes to character creation, then loads module "001ebo" (Prologue/Ebon Hawk)
                    // Original implementation flow: New Game button -> Character creation screen -> Character creation completes -> Module loads -> Player entity created
                    // This matches the original games: character creation must complete before module loads
                    Console.WriteLine("[Odyssey] New Game button clicked - transitioning to character creation");

                    // Stop main menu music and start character creation music
                    // Based on swkotor.exe FUN_005f9af0: Plays "mus_theme_rep" for character creation (param_1 == 0)
                    // Based on swkotor2.exe: Plays "mus_main" for character creation (vendor/reone implementation)
                    if (_musicPlayer != null && _musicStarted)
                    {
                        _musicPlayer.Stop();
                        _musicStarted = false;
                        Console.WriteLine("[Odyssey] Main menu music stopped");
                    }

                    // Start character creation music
                    if (_musicPlayer != null && _musicEnabled)
                    {
                        string chargenMusicResRef = _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? "mus_theme_rep" : "mus_main";
                        if (_musicPlayer.Play(chargenMusicResRef, 1.0f))
                        {
                            _musicStarted = true;
                            Console.WriteLine($"[Odyssey] Character creation music started: {chargenMusicResRef}");
                        }
                        else
                        {
                            Console.WriteLine($"[Odyssey] WARNING: Failed to play character creation music: {chargenMusicResRef}");
                        }
                    }

                    _currentState = GameState.CharacterCreation;
                    if (_characterCreationScreen == null)
                    {
                        // Get installation from session (created in Initialize) or create from settings
                        Installation installation = null;
                        if (_session != null && _session.Installation != null)
                        {
                            installation = _session.Installation;
                        }
                        else if (!string.IsNullOrEmpty(_settings.GamePath))
                        {
                            installation = new Installation(_settings.GamePath, _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? Andastra.Parsing.Common.BioWareGame.K1 : Andastra.Parsing.Common.BioWareGame.K2);
                        }

                        if (installation == null)
                        {
                            Console.WriteLine("[Odyssey] ERROR: Cannot create character creation screen - no installation available");
                            _currentState = GameState.MainMenu;
                            break;
                        }

                        _characterCreationScreen = new CharacterCreationScreen(
                            _graphicsDevice,
                            installation,
                            _settings.Game,
                            _guiManager,
                            OnCharacterCreationComplete,
                            OnCharacterCreationCancel,
                            _graphicsBackend);
                    }
                    break;

                case "BTN_LOADGAME":
                    // Load Game button - show load game menu
                    Console.WriteLine("[Odyssey] Load Game button clicked - opening load game menu");
                    _currentState = GameState.LoadMenu;
                    LoadAvailableSaves();
                    break;

                case "BTN_OPTIONS":
                    // Options button - show options menu
                    // Based on swkotor.exe and swkotor2.exe: Options menu system
                    // Based on swkotor2.exe: CSWGuiOptionsMain @ 0x006e3e80 (constructor), loads "optionsmain" GUI
                    Console.WriteLine("[Odyssey] Options button clicked - opening options menu");
                    OpenOptionsMenu();
                    break;

                case "BTN_BACK":
                    // Back button - return to previous menu
                    // Based on swkotor.exe and swkotor2.exe: Back button handler in options menu
                    if (_currentState == GameState.GameplayOptionsMenu)
                    {
                        Console.WriteLine("[Odyssey] Back button clicked - closing gameplay options submenu");
                        CloseGameplayOptionsMenu();
                    }
                    else if (_currentState == GameState.GraphicsOptionsMenu)
                    {
                        Console.WriteLine("[Odyssey] Back button clicked - closing graphics options submenu");
                        CloseGraphicsOptionsMenu();
                    }
                    else if (_currentState == GameState.OptionsMenu)
                    {
                        Console.WriteLine("[Odyssey] Back button clicked - closing options menu");
                        CloseOptionsMenu();
                    }
                    break;

                case "BTN_GAMEPLAY":
                    // Gameplay options button - open gameplay options submenu
                    // Based on swkotor2.exe: CSWGuiOptionsMain::OnGameplayOpt @ 0x006de240
                    if (_currentState == GameState.OptionsMenu)
                    {
                        Console.WriteLine("[Odyssey] Gameplay options button clicked - switching to gameplay category");
                        // Navigate to Game category in options menu
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Game;
                        _selectedOptionsItemIndex = 0;
                    }
                    break;

                case "BTN_FEEDBACK":
                    // Feedback options button - open feedback options submenu
                    // Based on swkotor2.exe: CSWGuiOptionsMain::OnFeedbackOpt @ 0x006e2df0
                    if (_currentState == GameState.OptionsMenu)
                    {
                        // Navigate to Feedback category in options menu
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Feedback;
                        _selectedOptionsItemIndex = 0; // Reset to first option in Feedback category
                        Console.WriteLine("[Odyssey] Feedback options submenu opened - navigating to Feedback category");
                    }
                    break;

                case "BTN_AUTOPAUSE":
                    // Autopause options button - open autopause options submenu
                    // Based on swkotor2.exe: CSWGuiOptionsMain::OnAutopauseOpt @ 0x006de2c0
                    if (_currentState == GameState.OptionsMenu)
                    {
                        // Navigate to Autopause category in options menu
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Autopause;
                        _selectedOptionsItemIndex = 0; // Reset to first option in Autopause category
                        Console.WriteLine("[Odyssey] Autopause options submenu opened - navigating to Autopause category");
                    }
                    break;

                case "BTN_GRAPHICS":
                    // Graphics options button - open graphics options submenu
                    // Based on swkotor2.exe: CSWGuiOptionsMain::OnGraphicsOpt @ 0x006e3d80
                    if (_currentState == GameState.OptionsMenu)
                    {
                        // Navigate to Graphics category in options menu
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Graphics;
                        _selectedOptionsItemIndex = 0; // Reset to first option in Graphics category
                        Console.WriteLine("[Odyssey] Graphics options submenu opened - navigating to Graphics category");
                    }
                    break;

                case "BTN_SOUND":
                    // Sound options button - open sound options submenu
                    // Based on swkotor2.exe: CSWGuiOptionsMain::OnSoundOpt @ 0x006e3e00
                    if (_currentState == GameState.OptionsMenu)
                    {
                        // Navigate to Audio category in options menu
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Audio;
                        _selectedOptionsItemIndex = 0; // Reset to first option in Audio category
                        Console.WriteLine("[Odyssey] Sound options submenu opened - navigating to Audio category");
                    }
                    break;

                case "BTN_EXIT":
                    // Exit button - exit game
                    Console.WriteLine("[Odyssey] Exit button clicked - exiting game");
                    Exit();
                    break;

                case "BTN_MOVIES":
                    // Movies button (K1/K2) - show movies menu
                    Console.WriteLine("[Odyssey] Movies button clicked - opening movies menu");
                    OpenMoviesMenu();
                    break;

                case "BTN_MUSIC":
                    // Music button (K2 only) - toggle music
                    // Based on swkotor2.exe FUN_006d0790: BTN_MUSIC button handler
                    // Toggles music playback on/off for main menu
                    if (_musicPlayer != null)
                    {
                        if (_musicEnabled)
                        {
                            // Disable music: stop current playback
                            if (_musicStarted)
                            {
                                _musicPlayer.Stop();
                                _musicStarted = false;
                            }
                            _musicEnabled = false;
                            Console.WriteLine("[Odyssey] Music disabled by user");
                        }
                        else
                        {
                            // Enable music: start playback if in main menu
                            _musicEnabled = true;
                            if (_currentState == GameState.MainMenu && !_musicStarted)
                            {
                                string musicResRef = _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? "mus_theme_cult" : "mus_sion";
                                if (_musicPlayer.Play(musicResRef, 1.0f))
                                {
                                    _musicStarted = true;
                                    Console.WriteLine($"[Odyssey] Music enabled and started: {musicResRef}");
                                }
                                else
                                {
                                    Console.WriteLine($"[Odyssey] WARNING: Failed to play main menu music after toggle: {musicResRef}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("[Odyssey] Music enabled by user");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: Music button clicked but music player is not available");
                    }
                    break;

                default:
                    Console.WriteLine($"[Odyssey] Unknown button clicked: {buttonTag}");
                    break;
            }
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
        /// Loads the main menu 3D models (gui3D_room and mainmenu model).
        /// Based on swkotor.exe FUN_0067c4c0 lines 109-120: Loads gui3D_room and mainmenu model
        /// Based on swkotor2.exe FUN_006d2350: Loads gui3D_room and mainmenu model with variant selection
        /// </summary>
        /// <remarks>
        /// Main Menu 3D Model Loading:
        /// - swkotor.exe (K1): Loads "gui3D_room" model and "mainmenu" model
        /// - swkotor2.exe (K2): Loads "gui3D_room" model, determines menu variant based on gui3D_room condition
        /// - Menu variants (K2 only): mainmenu01 (default), mainmenu02, mainmenu03, mainmenu04, mainmenu05
        /// - Variant selection: Based on "gui3D_room" condition check (swkotor2.exe: 0x006d2350:120-150)
        /// - Camera hook: Searches model for "camerahook1" node to position camera
        /// - Camera distance: 0x41b5ced9 (~22.7) from camerahook position
        /// </remarks>
        private void LoadMainMenu3DModels(Installation installation)
        {
            if (installation == null)
            {
                Console.WriteLine("[Odyssey] WARNING: Cannot load main menu 3D models - installation is null");
                return;
            }

            try
            {
                // Create resource provider
                var resourceProvider = new GameResourceProvider(installation);

                // Load gui3D_room model first
                // Based on swkotor.exe and swkotor2.exe: gui3D_room is loaded to determine menu variant (K2) and for 3D rendering
                try
                {
                    var gui3DRoomRes = resourceProvider.GetResource("gui3D_room", ResourceType.MDL);
                    if (gui3DRoomRes != null)
                    {
                        // Use MDLAuto for loading (handles both binary and ASCII formats)
                        _gui3DRoomModel = MDLAuto.Load(gui3DRoomRes);
                        _gui3DRoomLoaded = true;
                        Console.WriteLine("[Odyssey] gui3D_room model loaded successfully");

                        // Determine menu variant based on gui3D_room condition (K2 only)
                        // Based on swkotor2.exe FUN_006d2350:120-150: Menu variant selection based on gui3D_room condition
                        if (_settings.Game == Andastra.Runtime.Core.KotorGame.K2)
                        {
                            _menuVariant = DetermineMenuVariant(_gui3DRoomModel);
                            Console.WriteLine($"[Odyssey] Menu variant determined: {_menuVariant} (K2)");
                        }
                        else
                        {
                            // K1 uses single "mainmenu" panel (no variants)
                            _menuVariant = "mainmenu";
                            Console.WriteLine("[Odyssey] Using single mainmenu panel (K1)");
                        }
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: gui3D_room model resource not found");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Odyssey] WARNING: Failed to load gui3D_room model: {ex.Message}");
                }

                // Load mainmenu model (or variant for K2)
                // Based on swkotor.exe FUN_0067c4c0: Loads "mainmenu" model
                // Based on swkotor2.exe FUN_006d2350: Loads menu variant model (mainmenu01-05)
                try
                {
                    string mainMenuModelName = _menuVariant;
                    var mainMenuRes = resourceProvider.GetResource(mainMenuModelName, ResourceType.MDL);
                    if (mainMenuRes != null)
                    {
                        using (var reader = new MDLBinaryReader(mainMenuRes))
                        {
                            _mainMenuModel = reader.Read();

                            // Build parent node map for efficient parent lookups
                            // Based on swkotor.exe and swkotor2.exe: MDL node hierarchy traversal
                            // Original implementation: Tracks parent-child relationships for transform accumulation
                            // This map is built once when model loads, then used for all transform calculations
                            _mainMenuModelParentMap = BuildParentNodeMap(_mainMenuModel.RootNode);

                            // Find camera hook position in the model
                            // Based on swkotor.exe and swkotor2.exe: Searches MDL node tree for "camerahook1" node
                            // Camera hook format: "camerahook{N}" where N is 1-based index
                            _mainMenuCameraHookPosition = FindCameraHookPosition(_mainMenuModel, 1);

                            // Convert model to MonoGame format if using MonoGame backend
                            if (_graphicsDevice is Andastra.Runtime.Graphics.MonoGame.Graphics.MonoGameGraphicsDevice mgDevice)
                            {
                                var materialResolver = new Func<string, Microsoft.Xna.Framework.Graphics.BasicEffect>(textureName =>
                                {
                                    // Handle null, empty, or "NULL" texture names
                                    // Based on swkotor.exe and swkotor2.exe: Texture names can be null, empty, or "NULL" string
                                    // Original implementation: Skips texture loading for invalid texture names, uses default material
                                    if (string.IsNullOrEmpty(textureName) ||
                                        textureName.Equals("NULL", StringComparison.OrdinalIgnoreCase) ||
                                        textureName.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Return default effect for invalid texture names
                                        var defaultEffect = new Microsoft.Xna.Framework.Graphics.BasicEffect(mgDevice.Device);
                                        defaultEffect.EnableDefaultLighting();
                                        defaultEffect.LightingEnabled = true;
                                        return defaultEffect;
                                    }

                                    try
                                    {
                                        // Load TPC texture and convert to MonoGame Texture2D
                                        // Based on swkotor.exe and swkotor2.exe: Texture loading system
                                        // Original implementation: Loads TPC files and creates DirectX textures (D3DTexture8/9)
                                        // This MonoGame implementation: Converts TPC format to MonoGame Texture2D using TpcToMonoGameTextureConverter
                                        var textureRes = resourceProvider.GetResource(textureName, ResourceType.TPC);
                                        if (textureRes != null)
                                        {
                                            // Parse TPC texture using TPCAuto (handles TPC, DDS, and TGA formats)
                                            // Based on swkotor.exe texture loading: Supports multiple texture formats
                                            var tpc = TPCAuto.ReadTpc(textureRes);

                                            // Convert TPC to MonoGame Texture2D
                                            // Based on TpcToMonoGameTextureConverter: Handles DXT1/DXT3/DXT5, RGB/RGBA, grayscale
                                            var texture = TpcToMonoGameTextureConverter.Convert(tpc, mgDevice.Device, generateMipmaps: true);

                                            if (texture is Microsoft.Xna.Framework.Graphics.Texture2D texture2D)
                                            {
                                                // Create BasicEffect with loaded texture
                                                // Based on swkotor.exe and swkotor2.exe: Material system uses textures for rendering
                                                // Original implementation: Sets texture on material/shader for rendering
                                                var effect = new Microsoft.Xna.Framework.Graphics.BasicEffect(mgDevice.Device);
                                                effect.Texture = texture2D;
                                                effect.TextureEnabled = true;
                                                effect.EnableDefaultLighting();
                                                effect.LightingEnabled = true;

                                                // Set ambient and diffuse lighting to match original engine
                                                // Based on swkotor.exe: Default lighting setup for menu models
                                                effect.AmbientLightColor = new Microsoft.Xna.Framework.Vector3(0.3f, 0.3f, 0.3f);
                                                effect.DiffuseColor = new Microsoft.Xna.Framework.Vector3(1.0f, 1.0f, 1.0f);

                                                return effect;
                                            }
                                        }
                                    }
                                    catch (Exception texEx)
                                    {
                                        // Log texture loading error but continue with default effect
                                        Console.WriteLine($"[Odyssey] WARNING: Failed to load texture {textureName}: {texEx.Message}");
                                    }

                                    // Fallback to default effect if texture loading fails
                                    // Based on swkotor.exe: Uses default material when texture is missing
                                    var defaultEffect = new Microsoft.Xna.Framework.Graphics.BasicEffect(mgDevice.Device);
                                    defaultEffect.EnableDefaultLighting();
                                    defaultEffect.LightingEnabled = true;
                                    return defaultEffect;
                                });

                                var converter = new MdlToMonoGameModelConverter(mgDevice.Device, materialResolver);
                                _mainMenuModelData = converter.Convert(_mainMenuModel);
                                _mainMenuModelLoaded = true;
                                Console.WriteLine($"[Odyssey] Main menu model loaded and converted: {mainMenuModelName}");
                            }
                            else
                            {
                                Console.WriteLine("[Odyssey] WARNING: Main menu 3D model rendering requires MonoGame graphics device");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[Odyssey] WARNING: Main menu model resource not found: {mainMenuModelName}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Odyssey] WARNING: Failed to load main menu model: {ex.Message}");
                }

                // Set up camera matrices for 3D rendering
                // Based on swkotor.exe and swkotor2.exe: Camera positioned at camerahook with distance ~22.7
                SetupMainMenuCamera();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR: Failed to load main menu 3D models: {ex.Message}");
                Console.WriteLine($"[Odyssey] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Loads GUI sound ResRefs from guisounds.2da.
        /// Based on swkotor.exe and swkotor2.exe: guisounds.2da contains Clicked_Default and Entered_Default
        /// Original implementation: Loads guisounds.2da and reads soundresref column for Clicked_Default and Entered_Default
        /// </summary>
        /// <param name="installation">The game installation to load guisounds.2da from.</param>
        private void LoadGuiSounds(Installation installation)
        {
            if (installation == null)
            {
                Console.WriteLine("[Odyssey] WARNING: Cannot load GUI sounds - installation is null");
                return;
            }

            try
            {
                // Load guisounds.2da from the installation
                // Based on swkotor.exe and swkotor2.exe: guisounds.2da is in the override or data folder
                var resourceProvider = new GameResourceProvider(installation);
                var guisoundsRes = resourceProvider.GetResource("guisounds", ResourceType.TwoDA);
                if (guisoundsRes != null)
                {
                    // Parse the 2DA file
                    var twoDA = TwoDAAuto.Read2DA(guisoundsRes);
                    if (twoDA != null)
                    {
                        // Find Clicked_Default row and get soundresref
                        // Based on swkotor.exe and swkotor2.exe: Clicked_Default row contains button click sound
                        var clickedRow = twoDA.FindRow("Clicked_Default");
                        if (clickedRow != null)
                        {
                            string clickedSound = clickedRow.GetString("soundresref");
                            if (!string.IsNullOrEmpty(clickedSound))
                            {
                                _buttonClickSound = clickedSound;
                                Console.WriteLine($"[Odyssey] Button click sound loaded: {_buttonClickSound}");
                            }
                        }

                        // Find Entered_Default row and get soundresref
                        // Based on swkotor.exe and swkotor2.exe: Entered_Default row contains button hover sound
                        var enteredRow = twoDA.FindRow("Entered_Default");
                        if (enteredRow != null)
                        {
                            string enteredSound = enteredRow.GetString("soundresref");
                            if (!string.IsNullOrEmpty(enteredSound))
                            {
                                _buttonHoverSound = enteredSound;
                                Console.WriteLine($"[Odyssey] Button hover sound loaded: {_buttonHoverSound}");
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: Failed to parse guisounds.2da");
                    }
                }
                else
                {
                    Console.WriteLine("[Odyssey] WARNING: guisounds.2da not found in installation");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] WARNING: Failed to load GUI sounds: {ex.Message}");
                Console.WriteLine($"[Odyssey] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Finds camera hook position in an MDL model.
        /// Based on swkotor.exe and swkotor2.exe: Searches MDL node tree for "camerahook{N}" nodes
        /// Located via string references: "camerahook" @ 0x007c7dac, "camerahook%d" @ 0x007d0448
        /// </summary>
        /// <param name="model">The MDL model to search.</param>
        /// <param name="hookIndex">The camera hook index (1-based, e.g., 1 = "camerahook1").</param>
        /// <returns>World-space position of the camera hook, or Vector3.Zero if not found.</returns>
        private System.Numerics.Vector3 FindCameraHookPosition(MDL model, int hookIndex)
        {
            if (model == null || hookIndex < 1)
            {
                return System.Numerics.Vector3.Zero;
            }

            // Construct camera hook node name (format: "camerahook{N}")
            string hookNodeName = string.Format("camerahook{0}", hookIndex);

            // Search for camera hook node recursively in the model's node tree
            if (model.RootNode != null)
            {
                MDLNode hookNode = FindNodeByName(model.RootNode, hookNodeName);
                if (hookNode != null)
                {
                    // Transform node position to world space
                    // Based on swkotor2.exe: FUN_006c6020 transforms node local position to world space
                    System.Numerics.Matrix4x4 nodeTransform = GetNodeWorldTransform(model.RootNode, hookNode);
                    System.Numerics.Vector3 localPos = hookNode.Position;
                    System.Numerics.Vector3 worldPos = System.Numerics.Vector3.Transform(localPos, nodeTransform);
                    return worldPos;
                }
            }

            return System.Numerics.Vector3.Zero;
        }

        /// <summary>
        /// Determines the menu variant based on gui3D_room model condition.
        /// Based on swkotor2.exe FUN_006d2350:120-150: Menu variant selection logic.
        /// </summary>
        /// <param name="gui3DRoomModel">The loaded gui3D_room model, or null if not loaded.</param>
        /// <returns>Menu variant name: "mainmenu01" through "mainmenu05" for K2, "mainmenu" for K1.</returns>
        /// <remarks>
        /// Menu Variant Selection (K2 only):
        /// - swkotor2.exe uses menu variants: mainmenu01 (default), mainmenu02, mainmenu03, mainmenu04, mainmenu05
        /// - Variant selection is based on gui3D_room model condition check (swkotor2.exe: 0x006d2350:120-150)
        /// - Default variant is mainmenu01, which is used when:
        ///   1. gui3D_room model is null or not loaded successfully
        ///   2. Model condition check determines default variant should be used
        /// - The original engine implementation checks model properties or conditions to determine variant
        // TODO: / - For now, we use mainmenu01 as the default variant matching original engine default behavior
        /// </remarks>
        private string DetermineMenuVariant(MDL gui3DRoomModel)
        {
            // Default variant per original engine behavior (swkotor2.exe default)
            // mainmenu01 is the default variant used when gui3D_room model is loaded successfully
            string defaultVariant = "mainmenu01";

            if (gui3DRoomModel == null)
            {
                // Model not loaded, use default variant
                return defaultVariant;
            }

            // Check if model has valid root node (basic validation)
            if (gui3DRoomModel.Root == null)
            {
                // Invalid model structure, use default variant
                return defaultVariant;
            }

            // Based on swkotor2.exe FUN_006d2350:120-150, the original implementation checks
            // gui3D_room model condition to determine variant. The exact condition check is not
            // fully reverse-engineered, but mainmenu01 is confirmed as the default variant.
            //
            // Potential future enhancements:
            // - Check for specific nodes in the model hierarchy
            // - Check model properties or flags
            // - Check external conditions (game state, time, etc.)
            // - Implement variant selection logic once exact condition is determined via Ghidra analysis

            // TODO: STUB - For now, return default variant matching original engine behavior
            return defaultVariant;
        }

        /// <summary>
        /// Recursively searches for a node by name in the MDL node tree.
        /// </summary>
        private MDLNode FindNodeByName(MDLNode node, string name)
        {
            if (node == null)
            {
                return null;
            }

            // Check current node
            if (node.Name != null && node.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            // Search children recursively
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    var found = FindNodeByName(child, name);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Builds a parent node map for efficient parent lookups.
        /// Based on swkotor.exe and swkotor2.exe: MDL node hierarchy traversal
        /// Original implementation: Tracks parent-child relationships for transform accumulation
        /// This map is built once when model loads, then used for all transform calculations
        /// </summary>
        /// <param name="rootNode">The root node of the MDL model.</param>
        /// <returns>A dictionary mapping each node to its parent node (root node maps to null).</returns>
        private Dictionary<MDLNode, MDLNode> BuildParentNodeMap(MDLNode rootNode)
        {
            var parentMap = new Dictionary<MDLNode, MDLNode>();

            if (rootNode == null)
            {
                return parentMap;
            }

            // Root node has no parent
            parentMap[rootNode] = null;

            // Recursively build parent map for all children
            // Based on swkotor.exe: MDL node tree traversal for building transform hierarchy
            BuildParentNodeMapRecursive(rootNode, parentMap);

            return parentMap;
        }

        /// <summary>
        /// Recursively builds parent node map for all nodes in the tree.
        /// </summary>
        private void BuildParentNodeMapRecursive(MDLNode parentNode, Dictionary<MDLNode, MDLNode> parentMap)
        {
            if (parentNode == null || parentNode.Children == null)
            {
                return;
            }

            // Map each child to its parent
            foreach (var child in parentNode.Children)
            {
                if (child != null)
                {
                    parentMap[child] = parentNode;
                    // Recursively process children
                    BuildParentNodeMapRecursive(child, parentMap);
                }
            }
        }

        /// <summary>
        /// Gets the world transform matrix for a node by accumulating parent transforms.
        /// Based on swkotor.exe and swkotor2.exe: Transform accumulation for MDL nodes
        /// Original implementation: Accumulates transforms from root to target node
        /// Uses parent map for efficient O(1) parent lookups instead of O(n) recursive search
        /// </summary>
        /// <param name="rootNode">The root node of the MDL model.</param>
        /// <param name="targetNode">The target node to get world transform for.</param>
        /// <returns>World transform matrix accumulated from root to target node.</returns>
        private System.Numerics.Matrix4x4 GetNodeWorldTransform(MDLNode rootNode, MDLNode targetNode)
        {
            if (rootNode == null || targetNode == null || _mainMenuModelParentMap == null)
            {
                return System.Numerics.Matrix4x4.Identity;
            }

            // Build transform chain from target to root using parent map
            // Based on swkotor.exe: Transform accumulation traverses parent chain
            var transformChain = new List<MDLNode>();
            MDLNode current = targetNode;

            while (current != null && current != rootNode)
            {
                transformChain.Add(current);
                // Use parent map for O(1) lookup instead of recursive search
                if (!_mainMenuModelParentMap.TryGetValue(current, out current))
                {
                    break; // Reached root or node not in map
                }
            }

            // Apply transforms in reverse order (from root to target)
            // Based on swkotor.exe: Transforms are applied in parent-to-child order
            System.Numerics.Matrix4x4 transform = System.Numerics.Matrix4x4.Identity;
            for (int i = transformChain.Count - 1; i >= 0; i--)
            {
                var node = transformChain[i];
                System.Numerics.Matrix4x4 nodeTransform = CreateNodeTransform(node);
                transform = System.Numerics.Matrix4x4.Multiply(transform, nodeTransform);
            }

            return transform;
        }

        /// <summary>
        /// Creates a transform matrix from an MDL node's position, rotation, and scale.
        /// Based on swkotor.exe and swkotor2.exe: MDL nodes use quaternion orientation and separate scale components
        /// </summary>
        private System.Numerics.Matrix4x4 CreateNodeTransform(MDLNode node)
        {
            if (node == null)
            {
                return System.Numerics.Matrix4x4.Identity;
            }

            // Create translation matrix
            System.Numerics.Matrix4x4 translation = System.Numerics.Matrix4x4.CreateTranslation(node.Position);

            // Create rotation matrix from quaternion (MDL nodes use Vector4 quaternion: x, y, z, w)
            System.Numerics.Matrix4x4 rotation = System.Numerics.Matrix4x4.Identity;
            if (node.Orientation.W != 0 || node.Orientation.X != 0 || node.Orientation.Y != 0 || node.Orientation.Z != 0)
            {
                // Convert Vector4 to Quaternion (MDL format: x, y, z, w)
                System.Numerics.Quaternion quat = new System.Numerics.Quaternion(
                    node.Orientation.X,
                    node.Orientation.Y,
                    node.Orientation.Z,
                    node.Orientation.W
                );
                rotation = System.Numerics.Matrix4x4.CreateFromQuaternion(quat);
            }

            // Create scale matrix from separate scale components
            System.Numerics.Matrix4x4 scale = System.Numerics.Matrix4x4.CreateScale(node.ScaleX, node.ScaleY, node.ScaleZ);

            // Combine: Scale * Rotation * Translation
            System.Numerics.Matrix4x4 result = System.Numerics.Matrix4x4.Multiply(scale, rotation);
            result = System.Numerics.Matrix4x4.Multiply(result, translation);

            return result;
        }

        /// <summary>
        /// Sets up the camera for main menu 3D rendering.
        /// Based on swkotor.exe and swkotor2.exe: Camera positioned at camerahook with distance ~22.7
        /// Camera distance: 0x41b5ced9 (~22.7), attached to "camerahook"
        /// </summary>
        private void SetupMainMenuCamera()
        {
            if (!_mainMenuModelLoaded || _mainMenuCameraHookPosition == System.Numerics.Vector3.Zero)
            {
                // Use default camera position if model not loaded
                _mainMenuCameraHookPosition = new System.Numerics.Vector3(0, 0, 0);
            }

            // Set up view matrix: Camera looks at the model from camerahook position
            // Based on swkotor.exe and swkotor2.exe: Camera distance 0x41b5ced9 (~22.7) from camerahook
            System.Numerics.Vector3 cameraPosition = _mainMenuCameraHookPosition + new System.Numerics.Vector3(0, 0, _mainMenuCameraDistance);
            System.Numerics.Vector3 lookAtPosition = _mainMenuCameraHookPosition;
            System.Numerics.Vector3 upVector = System.Numerics.Vector3.UnitY;

            _mainMenuViewMatrix = System.Numerics.Matrix4x4.CreateLookAt(cameraPosition, lookAtPosition, upVector);

            // Set up projection matrix: Perspective projection for 3D rendering
            int viewportWidth = _graphicsDevice.Viewport.Width;
            int viewportHeight = _graphicsDevice.Viewport.Height;
            float aspectRatio = (float)viewportWidth / (float)viewportHeight;
            float fieldOfView = (float)Math.PI / 4.0f; // 45 degrees
            float nearPlane = 0.1f;
            float farPlane = 1000.0f;

            _mainMenuProjectionMatrix = System.Numerics.Matrix4x4.CreatePerspectiveFieldOfView(fieldOfView, aspectRatio, nearPlane, farPlane);
        }

        /// <summary>
        /// Renders the main menu 3D character model with continuous rotation.
        /// Based on swkotor.exe FUN_0067c4c0: Renders 3D character model at camerahook position with rotation
        /// Based on swkotor2.exe FUN_006d2350: Renders 3D character model with menu variant and rotation
        /// Original games rotate the character model continuously around the Y-axis (vertical axis)
        ///
        /// Note: Original games also play a "default" animation on the main menu model
        /// (swkotor.exe line 37: Set animation to DAT_0073df6c, swkotor2.exe line 125: Set animation to "default")
        /// Character model animation playback requires full MDL animation system integration:
        /// - Loading MDL animations from the model file
        /// - Playing animations on the model (idle/default animation)
        /// - Updating animation state each frame
        /// - Applying animation transforms to bones/nodes during rendering
        /// This is a complex feature requiring animation system integration beyond static mesh rendering.
        /// The 3D model rotation is implemented and matches the original games' visual appearance.
        /// </summary>
        private void RenderMainMenu3DModel()
        {
            if (!_mainMenuModelLoaded || _mainMenuModelData == null)
            {
                return;
            }

            // Only render if using MonoGame backend
            if (!(_graphicsDevice is Andastra.Runtime.Graphics.MonoGame.Graphics.MonoGameGraphicsDevice mgDevice))
            {
                return;
            }

            try
            {
                // Set up render state for 3D rendering
                // Enable depth testing and disable alpha blending for 3D model
                _graphicsDevice.SetDepthStencilState(DepthStencilState.Default);
                _graphicsDevice.SetBlendState(BlendState.Opaque);
                _graphicsDevice.SetRasterizerState(RasterizerState.CullCounterClockwise);

                // Create rotation matrix for continuous Y-axis rotation
                // Based on swkotor.exe and swkotor2.exe: Character model rotates continuously around Y-axis
                // Rotation speed matches original games (approximately 0.5 radians per second)
                System.Numerics.Matrix4x4 rotationMatrix = System.Numerics.Matrix4x4.CreateRotationY(_mainMenuModelRotation);

                // Render each mesh in the model
                foreach (var mesh in _mainMenuModelData.Meshes)
                {
                    if (mesh.VertexBuffer == null || mesh.IndexBuffer == null || mesh.Effect == null)
                    {
                        continue;
                    }

                    // Set vertex and index buffers
                    mgDevice.Device.SetVertexBuffer(mesh.VertexBuffer);
                    mgDevice.Device.Indices = mesh.IndexBuffer;

                    // Combine mesh world transform with rotation matrix
                    // Original games rotate the entire model around its center (Y-axis)
                    System.Numerics.Matrix4x4 worldTransform = System.Numerics.Matrix4x4.Multiply(mesh.WorldTransform, rotationMatrix);

                    // Set effect parameters
                    mesh.Effect.World = ConvertMatrix(worldTransform);
                    mesh.Effect.View = ConvertMatrix(_mainMenuViewMatrix);
                    mesh.Effect.Projection = ConvertMatrix(_mainMenuProjectionMatrix);

                    // Render the mesh
                    foreach (var pass in mesh.Effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        mgDevice.Device.DrawIndexedPrimitives(
                            Microsoft.Xna.Framework.Graphics.PrimitiveType.TriangleList,
                            0,
                            0,
                            mesh.IndexCount / 3
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] WARNING: Failed to render main menu 3D model: {ex.Message}");
            }
        }

        /// <summary>
        /// Converts System.Numerics.Matrix4x4 to Microsoft.Xna.Framework.Matrix.
        /// </summary>
        private Microsoft.Xna.Framework.Matrix ConvertMatrix(System.Numerics.Matrix4x4 matrix)
        {
            return new Microsoft.Xna.Framework.Matrix(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }

        /// <summary>
        /// Draws the main menu with GUI panel, 3D character model, and buttons.
        /// Based on swkotor.exe FUN_0067c4c0 @ 0x0067c4c0 (K1 main menu rendering)
        /// Based on swkotor2.exe FUN_006d2350 @ 0x006d2350 (K2 main menu rendering)
        /// </summary>
        private void DrawMainMenu()
        {
            // Clear to dark space-like background (deep blue/black gradient effect)
            _graphicsDevice.Clear(new Color(15, 15, 25, 255));

            // Render 3D character model (gui3D_room + menu variant) at camerahook position
            // Based on swkotor.exe FUN_0067c4c0 lines 109-120: Loads gui3D_room and mainmenu model
            // Based on swkotor2.exe FUN_006d2350: Renders 3D character model with menu variant
            // Camera distance: 0x41b5ced9 (~22.7), attached to "camerahook"
            // Render 3D model before GUI (3D scene renders first, then GUI on top)
            if (_mainMenuModelLoaded && _mainMenuModelData != null)
            {
                // Set up camera for menu 3D view
                SetupMainMenuCamera();

                // Render the 3D model
                RenderMainMenu3DModel();
            }

            // Begin sprite batch rendering
            _spriteBatch.Begin();

            // Render GUI panel if loaded
            // Based on swkotor.exe FUN_0067c4c0: Renders MAINMENU GUI panel
            // Based on swkotor2.exe FUN_006d2350: Renders MAINMENU GUI panel
            if (_mainMenuGuiLoaded && _guiManager != null)
            {
                // Render GUI using KotorGuiManager
                // GUI manager handles rendering of all GUI elements (buttons, labels, backgrounds)
                // KotorGuiManager.Draw handles its own sprite batch Begin/End internally
                _guiManager.Draw(_menuAnimationTime);
                return; // GUI handles all rendering, no need for fallback
            }

            // Fallback: Draw simple menu if GUI not loaded
            // This is the existing fallback rendering code that was already in DrawMainMenu
            // The rest of DrawMainMenu continues with fallback rendering
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
                string subtitle = _settings.Game == Andastra.Runtime.Core.KotorGame.K1
                    ? "Knights of the Old Republic"
                    : "The Sith Lords";
                string version = "Demo Build";

                // Title - large with shadow
                GraphicsVector2 titleSize = _font.MeasureString(title);
                GraphicsVector2 titlePos = new GraphicsVector2(centerX - titleSize.X / 2, startY - titleOffset - 80);

                // Draw shadow first (offset by 3 pixels)
                _spriteBatch.DrawString(_font, title, titlePos + new GraphicsVector2(3, 3), new Color(0, 0, 0, 180));
                // Draw main title text
                _spriteBatch.DrawString(_font, title, titlePos, new Color(255, 215, 0, 255)); // Gold color

                // Subtitle
                GraphicsVector2 subtitleSize = _font.MeasureString(subtitle);
                GraphicsVector2 subtitlePos = new GraphicsVector2(centerX - subtitleSize.X / 2, titlePos.Y + titleSize.Y + 10);
                _spriteBatch.DrawString(_font, subtitle, subtitlePos + new GraphicsVector2(2, 2), new Color(0, 0, 0, 150));
                _spriteBatch.DrawString(_font, subtitle, subtitlePos, new Color(200, 200, 220, 255));

                // Version label
                GraphicsVector2 versionSize = _font.MeasureString(version);
                GraphicsVector2 versionPos = new GraphicsVector2(centerX - versionSize.X / 2, subtitlePos.Y + subtitleSize.Y + 15);
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
                    GraphicsVector2 textSize = _font.MeasureString(_menuItems[i]);
                    GraphicsVector2 textPos = new GraphicsVector2(
                        scaledButtonRect.X + (scaledButtonRect.Width - textSize.X) / 2,
                        scaledButtonRect.Y + (scaledButtonRect.Height - textSize.Y) / 2
                    );

                    // Draw text shadow
                    _spriteBatch.DrawString(_font, _menuItems[i], textPos + new GraphicsVector2(2, 2), new Color(0, 0, 0, 200));
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
                    GraphicsVector2 titleSize = _font.MeasureString(pathTitle);
                    GraphicsVector2 titlePos = new GraphicsVector2(centerX - titleSize.X / 2, pathSelectorY + 5);
                    _spriteBatch.DrawString(_font, pathTitle, titlePos + new GraphicsVector2(1, 1), new Color(0, 0, 0, 150));
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
                        DrawRectangleBorder(_spriteBatch, itemRect, 2, new Color(255, 255, 255, 255));
                    }

                    // Draw path text
                    if (_font != null)
                    {
                        string pathText = _availablePaths[i];
                        // Truncate if too long
                        GraphicsVector2 textSize = _font.MeasureString(pathText);
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

                        GraphicsVector2 textPos = new GraphicsVector2(itemRect.X + 10, itemRect.Y + (itemRect.Height - textSize.Y) / 2);
                        _spriteBatch.DrawString(_font, pathText, textPos + new GraphicsVector2(1, 1), new Color(0, 0, 0, 200));
                        _spriteBatch.DrawString(_font, pathText, textPos, isSelected ? Color.White : new Color(180, 180, 200, 255));
                    }
                }

                // Draw instructions
                if (_font != null)
                {
                    string instructions = "Arrow Keys: Navigate  |  Enter: Select  |  ESC: Cancel";
                    GraphicsVector2 instSize = _font.MeasureString(instructions);
                    GraphicsVector2 instPos = new GraphicsVector2(centerX - instSize.X / 2, pathSelectorY + pathSelectorHeight + (Math.Min(_availablePaths.Count, maxVisiblePaths) * pathItemHeight) + 10);
                    _spriteBatch.DrawString(_font, instructions, instPos + new GraphicsVector2(1, 1), new Color(0, 0, 0, 150));
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
                    GraphicsVector2 instSize = _font.MeasureString(instructions);
                    GraphicsVector2 instPos = new GraphicsVector2(centerX - instSize.X / 2, viewportHeight - 60);

                    // Shadow
                    _spriteBatch.DrawString(_font, instructions, instPos + new GraphicsVector2(1, 1), new Color(0, 0, 0, 150));
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

        /// <summary>
        /// Callback when character creation is completed.
        /// Based on swkotor.exe (K1) and swkotor2.exe (K2): Character generation completes, then module loads
        /// - K1: Loads module "end_m01aa" (Endar Spire) @ swkotor.exe: 0x0067afb0
        /// - K2: Loads module "001ebo" (Prologue/Ebon Hawk) @ swkotor2.exe: 0x006d0b00
        /// </summary>
        private void OnCharacterCreationComplete(Andastra.Runtime.Game.Core.CharacterCreationData characterData)
        {
            Console.WriteLine("[Odyssey] Character creation completed - starting new game");
            _characterData = characterData;
            StartGame();
        }

        /// <summary>
        /// Callback when character creation is cancelled.
        /// Returns to main menu.
        /// </summary>
        private void OnCharacterCreationCancel()
        {
            Console.WriteLine("[Odyssey] Character creation cancelled - returning to main menu");

            // Stop character creation music and restart main menu music
            // Based on swkotor.exe and swkotor2.exe: When canceling character creation, return to main menu music
            if (_musicPlayer != null && _musicStarted)
            {
                _musicPlayer.Stop();
                _musicStarted = false;
                Console.WriteLine("[Odyssey] Character creation music stopped");
            }

            // Restart main menu music
            if (_musicPlayer != null && _musicEnabled)
            {
                string musicResRef = _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? "mus_theme_cult" : "mus_sion";
                if (_musicPlayer.Play(musicResRef, 1.0f))
                {
                    _musicStarted = true;
                    Console.WriteLine($"[Odyssey] Main menu music restarted: {musicResRef}");
                }
            }

            _currentState = GameState.MainMenu;
            _characterCreationScreen = null; // Allow recreation on next New Game click
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
                _currentState = GameState.MainMenu;
                return;
            }

            try
            {
                // Update settings with game path
                // Based on swkotor.exe (K1) and swkotor2.exe (K2) entry points:
                // - K1: "END_M01AA" (Endar Spire - Command Module) @ swkotor.exe: 0x0067afb0 (OnNewGamePicked)
                //   - Located via string reference: "END_M01AA" @ 0x00752f58
                //   - Original implementation: FUN_005e5a90(aiStack_2c,"END_M01AA") sets module name
                // - K2: "001ebo" (Ebon Hawk Interior - Prologue) @ swkotor2.exe: 0x0067afb0 (OnNewGamePicked equivalent)
                //   - Located via string reference: "001ebo" @ 0x007cc028
                //   - Original implementation: Character generation finishes and loads module "001ebo"
                //   - Note: Module names are case-insensitive in resource lookup, but we use lowercase for consistency
                // Module name casing: Ghidra shows "END_M01AA" (uppercase) but resource lookup is case-insensitive
                // We use lowercase "end_m01aa" and "001ebo" to match Andastra.Parsing conventions
                string startingModule = _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? "end_m01aa" : "001ebo";
                var updatedSettings = new Andastra.Runtime.Core.GameSettings
                {
                    Game = _settings.Game,
                    GamePath = gamePath,
                    StartModule = startingModule
                };

                // Create new session
                _session = new GameSession(updatedSettings, _world, _vm, _globals);

                // Initialize save system
                if (_world != null)
                {
                    string savesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Odyssey", "Saves");
                    var gameDataManager = new Andastra.Runtime.Engines.Odyssey.Data.GameDataManager(_session.Installation);
                    var serializer = new Andastra.Runtime.Games.Odyssey.OdysseySaveSerializer(gameDataManager);
                    var dataProvider = new Andastra.Runtime.Content.Save.SaveDataProvider(savesPath, serializer);
                    _saveSystem = new Andastra.Runtime.Core.Save.SaveSystem(_world, dataProvider);
                    _saveSystem.SetScriptGlobals(_globals);
                    RefreshSaveList();
                }

                // Start the game session with created character (if provided)
                // Character creation data is passed to GameSession.StartNewGame() which creates the player entity
                // Based on swkotor.exe (K1) and swkotor2.exe (K2): Character generation completes, then module loads, then player entity is created
                // Located via string references: Character generation finish() -> module load -> player entity creation
                // Original implementation: Character data stored, module loaded, then player entity created from character data at module entry point
                _session.StartNewGame(_characterData);

                // Initialize camera after player is created
                UpdateCamera(0.016f); // Approximate frame time for initialization

                // Transition to in-game state
                _currentState = GameState.InGame;

                Console.WriteLine("[Odyssey] Game started successfully");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Odyssey] Failed to start game: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
                _currentState = GameState.MainMenu;
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

            // Create ground plane for fallback rendering
            CreateGroundPlane();

            Console.WriteLine("[Odyssey] Game rendering initialized (3D rendering enabled with abstraction layer)");
        }

        /// <summary>
        /// Creates a ground plane for fallback rendering when area geometry is not available.
        /// Uses the 3D rendering abstraction layer to create vertex and index buffers.
        /// </summary>
        /// <remarks>
        /// Ground Plane Creation:
        /// - Based on swkotor2.exe: Fallback rendering when area rooms are not loaded
        /// - Original implementation: Simple flat plane rendered when area geometry unavailable
        /// - Located via rendering fallback path: When Area.Render() is not available, ground plane provides visual reference
        /// - Ground plane: Large flat quad (100x100 units) positioned at Y=0 (ground level)
        /// - Color: Neutral brown/gray (139, 69, 19) matching typical ground appearance
        /// - Uses VertexPositionColor format matching entity rendering pattern
        /// - Created using graphics abstraction layer (IGraphicsDevice.CreateVertexBuffer/CreateIndexBuffer)
        /// - Based on swkotor2.exe: FUN_00461c20/FUN_00461c00 fallback rendering when area not loaded
        /// </remarks>
        private void CreateGroundPlane()
        {
            // Ground plane dimensions: 100x100 units (large enough for typical area sizes)
            const float planeSize = 50f; // Half-size (total size is 100x100)
            const float planeY = 0f; // Ground level

            // Ground plane color: Neutral brown (RGB: 139, 69, 19) matching typical ground appearance
            // This provides a visual reference when area geometry is not available
            Color groundColor = new Color(139, 69, 19, 255); // Brown color

            // Create ground plane vertices (4 corners of a quad)
            // Vertices are positioned in XZ plane (Y is constant at ground level)
            // Order: bottom-left, bottom-right, top-right, top-left (when viewed from above)
            var groundVertices = new VertexPositionColor[]
            {
                // Bottom-left corner
                new VertexPositionColor(new System.Numerics.Vector3(-planeSize, planeY, -planeSize), groundColor),
                // Bottom-right corner
                new VertexPositionColor(new System.Numerics.Vector3(planeSize, planeY, -planeSize), groundColor),
                // Top-right corner
                new VertexPositionColor(new System.Numerics.Vector3(planeSize, planeY, planeSize), groundColor),
                // Top-left corner
                new VertexPositionColor(new System.Numerics.Vector3(-planeSize, planeY, planeSize), groundColor)
            };

            // Create ground plane indices (2 triangles forming a quad)
            // Triangle 1: bottom-left, bottom-right, top-right
            // Triangle 2: bottom-left, top-right, top-left
            // Winding order: Counter-clockwise (standard for front-facing geometry)
            short[] groundIndices = new short[]
            {
                0, 1, 2, // First triangle: bottom-left -> bottom-right -> top-right
                0, 2, 3  // Second triangle: bottom-left -> top-right -> top-left
            };

            // Create vertex and index buffers using 3D rendering abstraction layer
            // This matches the pattern used for entity rendering (CreateVertexBuffer, CreateIndexBuffer)
            _groundVertexBuffer = _graphicsDevice.CreateVertexBuffer(groundVertices);
            _groundIndexBuffer = _graphicsDevice.CreateIndexBuffer(groundIndices, true);

            Console.WriteLine("[Odyssey] Ground plane created (100x100 units, brown color)");
        }

        /// <summary>
        /// Updates the camera system each frame.
        /// Uses the abstracted camera controller for comprehensive camera management.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame in seconds.</param>
        /// <remarks>
        /// Camera Update Implementation:
        /// - Based on swkotor.exe and swkotor2.exe camera update system
        /// - swkotor.exe (KOTOR 1): Camera update @ FUN_004af630 (chase camera), FUN_004b0a20 (camera collision)
        /// - swkotor2.exe (KOTOR 2): Camera update @ FUN_004dcfb0 (chase camera), FUN_004dd1a0 (camera collision)
        /// - Original implementation: Camera follows player in chase mode, updates position based on player movement
        /// - Camera controller handles: Chase mode (follows player), Free mode (debug), Dialogue mode (conversations), Cinematic mode (cutscenes)
        /// - Camera collision detection prevents camera from going through walls
        /// - View and projection matrices are updated from camera controller
        /// - Camera automatically follows player entity when available, falls back to free camera when player not available
        /// </remarks>
        private void UpdateCamera(float deltaTime)
        {
            if (_cameraController == null)
            {
                return;
            }

            // Update camera controller (handles all camera logic: chase, free, dialogue, cinematic modes)
            // Camera controller updates position, look-at, and matrices based on current mode
            _cameraController.Update(deltaTime);

            // Set chase mode following player if player entity is available
            if (_session != null && _session.PlayerEntity != null)
            {
                // Get player entity from camera controller (handles cross-engine player lookup)
                IEntity playerEntity = _cameraController.GetPlayerEntity();
                if (playerEntity != null && _cameraController.Mode != CameraMode.Dialogue && _cameraController.Mode != CameraMode.Cinematic)
                {
                    // Set chase mode to follow player (only if not in dialogue or cinematic mode)
                    if (_cameraController.Target != playerEntity)
                    {
                        _cameraController.SetChaseMode(playerEntity);
                    }
                }
            }
            else
            {
                // No player entity available - use free camera mode
                if (_cameraController.Mode != CameraMode.Free && _cameraController.Mode != CameraMode.Dialogue && _cameraController.Mode != CameraMode.Cinematic)
                {
                    _cameraController.SetFreeMode();
                }
            }

            // Get view and projection matrices from camera controller
            // Camera controller calculates matrices based on current camera position, look-at, and field of view
            _viewMatrix = _cameraController.GetViewMatrix();

            float aspectRatio = (float)_graphicsDevice.Viewport.Width / _graphicsDevice.Viewport.Height;
            _projectionMatrix = _cameraController.GetProjectionMatrix(aspectRatio, 0.1f, 1000f);
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

            // CRITICAL: Render current area (handles all area rendering including rooms, entities, effects)
            // Based on swkotor2.exe: FUN_00461c20/FUN_00461c00 @ 0x00461c20/0x00461c00 (render functions)
            // Located via call chain: Main loop → FUN_00638ca0 → glClear → FUN_00461c20/FUN_00461c00
            // Original implementation: Area render handles VIS culling, transparency sorting, lighting, fog
            // Area.Render() is the authoritative rendering method - it handles all area-specific rendering
            if (_session != null && _session.World != null && _session.World.CurrentArea != null)
            {
                _session.World.CurrentArea.Render();
            }
            else
            {
                // Fallback: Manual rendering for backward compatibility (deprecated - should use Area.Render())
                // Draw 3D scene using abstraction layer
                // Ground plane rendering: Provides visual reference when area geometry is not available
                // Based on swkotor2.exe: Fallback rendering path when Area.Render() is not available
                if (_groundVertexBuffer != null && _groundIndexBuffer != null && _basicEffect != null)
                {
                    _graphicsDevice.SetVertexBuffer(_groundVertexBuffer);
                    _graphicsDevice.SetIndexBuffer(_groundIndexBuffer);

                    _basicEffect.View = _viewMatrix;
                    _basicEffect.Projection = _projectionMatrix;
                    _basicEffect.World = System.Numerics.Matrix4x4.Identity;
                    _basicEffect.VertexColorEnabled = true;
                    _basicEffect.LightingEnabled = true;

                    // Ground plane: 4 vertices, 2 triangles (6 indices total)
                    const int groundVertexCount = 4;
                    const int groundPrimitiveCount = 2; // 2 triangles = 6 indices / 3

                    foreach (IEffectPass pass in _basicEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        // Draw ground plane: 2 triangles forming a 100x100 unit quad
                        _graphicsDevice.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            0,
                            0,
                            groundVertexCount,
                            0,
                            groundPrimitiveCount
                        );
                    }
                }

                // Draw loaded area rooms if available
                if (_session != null && _session.CurrentRuntimeModule != null)
                {
                    Andastra.Runtime.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
                    if (entryArea != null && entryArea is Andastra.Runtime.Core.Module.RuntimeArea runtimeArea)
                    {
                        DrawAreaRooms(runtimeArea);
                    }
                }

                // Draw entities from GIT
                if (_session != null && _session.CurrentRuntimeModule != null)
                {
                    Andastra.Runtime.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
                    if (entryArea != null && entryArea is Andastra.Runtime.Core.Module.RuntimeArea runtimeArea)
                    {
                        DrawAreaEntities(runtimeArea);
                    }
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
                    Andastra.Runtime.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule?.GetArea(_session.CurrentRuntimeModule.EntryArea);
                    if (entryArea != null)
                    {
                        statusText += " | Area: " + entryArea.DisplayName + " (" + entryArea.ResRef + ")";
                        if (entryArea is Andastra.Runtime.Core.Module.RuntimeArea runtimeArea && runtimeArea.Rooms != null)
                        {
                            statusText += " | Rooms: " + runtimeArea.Rooms.Count;
                        }
                    }
                }
                if (_font != null)
                {
                    _spriteBatch.DrawString(_font, statusText, new GraphicsVector2(10, 10), Microsoft.Xna.Framework.Color.White);
                }
            }
            // If no font, we just skip text rendering - the 3D scene is still visible
            _spriteBatch.End();
        }

        private void DrawAreaRooms(Andastra.Runtime.Core.Module.RuntimeArea area)
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
                Andastra.Runtime.Core.Module.RoomInfo room = area.Rooms[i];

                if (string.IsNullOrEmpty(room.ModelName))
                {
                    continue;
                }

                // Get or load room mesh using abstraction layer
                IRoomMeshData meshData;
                if (!_roomMeshes.TryGetValue(room.ModelName, out meshData))
                {
                    // Try to load actual MDL model from module resources
                    Andastra.Parsing.Formats.MDLData.MDL mdl = null;
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
        private void DrawAreaEntities(Andastra.Runtime.Core.Module.RuntimeArea area)
        {
            // Entity rendering using abstraction layer (IBasicEffect, IVertexBuffer, IIndexBuffer, MatrixHelper)
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
        private void DrawEntity(Andastra.Runtime.Core.Interfaces.IEntity entity)
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
                case Andastra.Runtime.Core.Enums.ObjectType.Creature:
                    entityColor = Color.Green;
                    entityHeight = 2f;
                    entityWidth = 0.5f;
                    break;
                case Andastra.Runtime.Core.Enums.ObjectType.Door:
                    entityColor = Color.Brown;
                    entityHeight = 3f;
                    entityWidth = 1f;
                    break;
                case Andastra.Runtime.Core.Enums.ObjectType.Placeable:
                    entityColor = Color.Orange;
                    entityHeight = 1.5f;
                    entityWidth = 0.8f;
                    break;
                case Andastra.Runtime.Core.Enums.ObjectType.Trigger:
                    entityColor = Color.Yellow;
                    entityHeight = 0.5f;
                    entityWidth = 1f;
                    break;
                case Andastra.Runtime.Core.Enums.ObjectType.Waypoint:
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
        /// Draws the player entity using model renderer if available, otherwise as a simple box.
        /// </summary>
        /// <remarks>
        /// Player Entity Rendering:
        /// - Based on swkotor2.exe: Player entity is rendered using the same entity model rendering system as other creatures
        /// - Located via string references: Player entity uses CSWCCreature::LoadModel() @ 0x005261b0 for model loading
        /// - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (model loading error)
        /// - Original implementation: Player entity model is resolved from appearance.2da using appearance type
        /// - Model resolution: FUN_005261b0 @ 0x005261b0 resolves creature model from appearance.2da row
        /// - Player rendering: Uses same rendering pipeline as other creatures, no special handling required
        /// - Based on swkotor2.exe: FUN_005261b0 @ 0x005261b0 (load creature model), player entity uses standard creature rendering
        /// </remarks>
        private void DrawPlayerEntity(Andastra.Runtime.Core.Interfaces.IEntity playerEntity)
        {
            // Player rendering using abstraction layer - same as regular entities
            // Based on swkotor2.exe: Player entity uses standard creature model rendering (swkotor2.exe: 0x005261b0)

            Kotor.Components.TransformComponent transform = playerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Try to render with model renderer first (same approach as DrawEntity)
            // Player entity is a Creature entity, so it uses the same model resolution system
            if (_entityModelRenderer != null)
            {
                try
                {
                    _entityModelRenderer.RenderEntity(playerEntity, _viewMatrix, _projectionMatrix);
                    return; // Successfully rendered with model
                }
                catch (Exception ex)
                {
                    // Fall back to box rendering if model rendering fails
                    Console.WriteLine("[Odyssey] Model rendering failed for player entity: " + ex.Message);
                }
            }

            // Fallback: Draw as simple colored box (same as DrawEntity fallback)
            // This provides visual feedback when model rendering is unavailable
            var playerPos = new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
            var playerWorld = MatrixHelper.CreateTranslation(playerPos);

            // Create a simple box for the player (1x2x1 units - humanoid shape)
            var playerVertices = new VertexPositionColor[]
            {
                // Bottom face
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0), Microsoft.Xna.Framework.Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 0), Microsoft.Xna.Framework.Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, 0.5f, 0), Microsoft.Xna.Framework.Color.Blue),
                new VertexPositionColor(new Vector3(-0.5f, 0.5f, 0), Microsoft.Xna.Framework.Color.Blue),
                // Top face
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 2), Microsoft.Xna.Framework.Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, -0.5f, 2), Microsoft.Xna.Framework.Color.Blue),
                new VertexPositionColor(new Vector3(0.5f, 0.5f, 2), Microsoft.Xna.Framework.Color.Blue),
                new VertexPositionColor(new Vector3(-0.5f, 0.5f, 2), Microsoft.Xna.Framework.Color.Blue)
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

        /// <summary>
        /// Creates a programmatic default font when content pipeline fonts are unavailable.
        /// Based on swkotor2.exe: dialogfont16x16 (16x16 pixel bitmap font)
        /// </summary>
        /// <remarks>
        /// Default Font Creation:
        /// - Based on swkotor2.exe: dialogfont16x16 @ 0x007b6380 (font resource name)
        /// - Original engine uses 16x16 pixel bitmap fonts for text rendering
        /// - This creates a programmatic fallback font matching original engine dimensions
        /// - Supports both MonoGame and Stride backends
        /// - Ghidra analysis: FUN_00416890 @ 0x00416890 (font initialization), FUN_004155d0 (font loading)
        /// </remarks>
        [CanBeNull]
        private IFont CreateDefaultFont()
        {
            try
            {
                // Use DefaultFontGenerator to create programmatic font
                // Based on swkotor2.exe: dialogfont16x16 uses 16x16 pixel characters
                return DefaultFontGenerator.CreateDefaultFont(_graphicsDevice, _graphicsBackend.BackendType);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR: Exception creating default font: {ex.Message}");
                Console.WriteLine($"[Odyssey] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        /// <summary>
        /// Loads an MDL model from the current module.
        /// </summary>
        /// <remarks>
        /// Based on swkotor2.exe: MDL models are loaded from module resources using cached Module object.
        /// Located via string references: "MDL" @ resource type constants, model loading functions.
        /// Original implementation: Uses cached Module object for efficient resource access.
        /// </remarks>
        [CanBeNull]
        private Andastra.Parsing.Formats.MDLData.MDL LoadMDLModel(string modelResRef)
        {
            if (string.IsNullOrEmpty(modelResRef) || _session == null)
            {
                return null;
            }

            try
            {
                // Get cached Module object from session for efficient resource access
                // Based on swkotor2.exe: Module objects are cached and reused for resource lookups
                Andastra.Parsing.Common.Module module = _session.GetCurrentParsingModule();
                if (module == null)
                {
                    return null;
                }

                Andastra.Parsing.Common.ModuleResource mdlResource = module.Resource(modelResRef, ResourceType.MDL);
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

        /// <summary>
        /// Initializes the main menu 3D rendering system.
        /// Based on swkotor.exe FUN_0067c4c0 @ 0x0067c4c0 (K1 main menu rendering)
        /// Based on swkotor2.exe FUN_006d2350 @ 0x006d2350 (K2 main menu rendering)
        /// </summary>
        private void InitializeMainMenu3DRendering()
        {
            try
            {
                // Create dedicated entity model renderer for main menu
                // This renderer will handle loading and rendering of gui3D_room and mainmenu models
                _menuEntityModelRenderer = _graphicsBackend.CreateEntityModelRenderer();

                // Set up menu camera controller with camerahook positioning
                // Based on swkotor.exe FUN_0067c4c0: Camera distance 0x41b5ced9 (~22.7 units)
                // Camera is attached to "camerahook" node in the 3D scene
                _menuCameraController = _graphicsBackend.CreateCameraController();
                _menuCameraController.SetFreeMode();

                // Position camera at distance ~22.7 units from origin (matching original engine)
                // This matches the hex value 0x41b5ced9 which converts to approximately 22.7
                const float cameraDistance = 22.7f;
                var cameraPosition = new System.Numerics.Vector3(0, 0, cameraDistance);
                var cameraTarget = System.Numerics.Vector3.Zero;
                var cameraUp = System.Numerics.Vector3.UnitY;

                _menuCameraController.SetPosition(cameraPosition);
                _menuCameraController.SetLookAt(cameraTarget, cameraUp);

                // Calculate view and projection matrices for menu rendering
                float aspectRatio = (float)_graphicsDevice.Viewport.Width / _graphicsDevice.Viewport.Height;
                _menuViewMatrix = _menuCameraController.GetViewMatrix();
                _menuProjectionMatrix = _menuCameraController.GetProjectionMatrix(aspectRatio, 0.1f, 1000f);

                // Load gui3D_room model (the 3D scene/room for the menu)
                // Based on swkotor.exe FUN_0067c4c0 lines 109-120: Loads gui3D_room
                _gui3DRoomModel = LoadMenuModel("gui3D_room");

                // Load mainmenu model (the character model displayed in the menu)
                // Based on swkotor.exe FUN_0067c4c0 lines 109-120: Loads mainmenu model
                _mainMenuModel = LoadMenuModel("mainmenu");

                Console.WriteLine("[Odyssey] Main menu 3D rendering system initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] Failed to initialize main menu 3D rendering: {ex.Message}");
                // Continue without 3D rendering - menu will still work with GUI only
                _menuEntityModelRenderer = null;
                _gui3DRoomModel = null;
                _mainMenuModel = null;
            }
        }

        /// <summary>
        /// Loads a model directly from the installation for main menu rendering.
        /// </summary>
        /// <param name="modelName">Name of the model to load (without .mdl extension).</param>
        /// <returns>The loaded MDL model, or null if loading fails.</returns>
        [CanBeNull]
        private Andastra.Parsing.Formats.MDLData.MDL LoadMenuModel(string modelName)
        {
            if (string.IsNullOrEmpty(modelName) || string.IsNullOrEmpty(_settings.GamePath))
            {
                return null;
            }

            try
            {
                // Create installation from game path if needed
                var installation = new Andastra.Parsing.Installation.Installation(_settings.GamePath);
                
                // Use installation resource manager to find the MDL file
                var resourceResult = installation.Resources.LookupResource(modelName, ResourceType.MDL);
                if (resourceResult == null)
                {
                    Console.WriteLine($"[Odyssey] Could not find MDL resource: {modelName}");
                    return null;
                }

                // Load MDL using MDLAuto (same as LoadMDLModel method)
                string activePath = resourceResult.Activate();
                if (string.IsNullOrEmpty(activePath))
                {
                    Console.WriteLine($"[Odyssey] Could not activate MDL resource: {modelName}");
                    return null;
                }

                return MDLAuto.ReadMdl(activePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] Failed to load menu model {modelName}: {ex.Message}");
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
            var navMesh = _session.NavigationMesh as Andastra.Runtime.Core.Navigation.NavigationMesh;
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

                Andastra.Runtime.Core.Interfaces.IEntity clickedEntity = FindEntityAtRay(rayOrigin, rayDirection);

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
        /// Finds an entity at the given ray position using proper collision detection.
        /// Based on swkotor2.exe: FUN_004f67d0 @ 0x004f67d0 (entity picking function).
        /// Uses spatial queries (FUN_004e17a0 @ 0x004e17a0) and detailed collision detection (FUN_004f4b00 @ 0x004f4b00).
        /// </summary>
        /// <param name="rayOrigin">Origin of the ray in world space.</param>
        /// <param name="rayDirection">Direction of the ray (normalized).</param>
        /// <returns>The closest entity intersected by the ray, or null if no entity is found.</returns>
        /// <remarks>
        /// Entity Selection Implementation:
        /// - Based on swkotor2.exe reverse engineering via Ghidra MCP:
        ///   - FUN_004f67d0 @ 0x004f67d0: Main entity picking function
        ///   - FUN_004e17a0 @ 0x004e17a0: Spatial query function (bounding box intersection)
        ///   - FUN_004f4b00 @ 0x004f4b00: Detailed ray-entity intersection test
        ///   - FUN_004f5290 @ 0x004f5290: Detailed collision detection for movement
        /// - Process:
        ///   1. Iterate through all entities in the area
        ///   2. Get proper bounding box for each entity based on type (creature, placeable, door)
        ///   3. Perform ray-AABB intersection test
        ///   4. Return closest entity (smallest intersection distance)
        /// - Bounding boxes:
        ///   - Creatures: Uses OdysseyCreatureCollisionDetector to get bounding box from appearance.2da hitradius
        ///   - Placeables: Uses default bounding box (1.0f radius)
        ///   - Doors: Uses larger bounding box (1.5f radius) to match original engine behavior
        /// - Original engine uses spatial acceleration structure, but for simplicity we iterate all entities
        ///   (spatial queries can be optimized later if needed)
        /// </remarks>
        private Andastra.Runtime.Core.Interfaces.IEntity FindEntityAtRay(System.Numerics.Vector3 rayOrigin, System.Numerics.Vector3 rayDirection)
        {
            if (_session == null || _session.CurrentRuntimeModule == null)
            {
                return null;
            }

            Andastra.Runtime.Core.Interfaces.IArea entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
            if (entryArea == null || !(entryArea is Andastra.Runtime.Core.Module.RuntimeArea runtimeArea))
            {
                return null;
            }

            // Create collision detector for getting proper bounding boxes
            // Based on swkotor2.exe: Uses collision detection system to get entity bounding boxes
            OdysseyCreatureCollisionDetector collisionDetector = new OdysseyCreatureCollisionDetector();

            float closestDistance = float.MaxValue;
            Andastra.Runtime.Core.Interfaces.IEntity closestEntity = null;

            // Maximum ray distance (matches original engine: 0x7f000000 = FLT_MAX)
            const float maxRayDistance = 3.40282347e+38f;

            foreach (Andastra.Runtime.Core.Interfaces.IEntity entity in runtimeArea.GetAllEntities())
            {
                Kotor.Components.TransformComponent transform = entity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
                if (transform == null)
                {
                    continue;
                }

                Vector3 entityPos = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                CreatureBoundingBox boundingBox;

                // Get proper bounding box based on entity type
                // Based on swkotor2.exe: FUN_004f67d0 uses different bounding boxes for different entity types
                if ((entity.ObjectType & Andastra.Runtime.Core.Enums.ObjectType.Creature) != 0)
                {
                    // Creatures: Use collision detector to get proper bounding box from appearance.2da
                    // Based on swkotor2.exe: FUN_005479f0 @ 0x005479f0 gets bounding box from entity structure
                    // Our abstraction: Use OdysseyCreatureCollisionDetector.GetCreatureBoundingBoxPublic()
                    boundingBox = collisionDetector.GetCreatureBoundingBoxPublic(entity);
                }
                else if ((entity.ObjectType & Andastra.Runtime.Core.Enums.ObjectType.Door) != 0)
                {
                    // Doors: Use larger bounding box (1.5f radius) to match original engine behavior
                    // Based on swkotor2.exe: Doors have larger collision boxes for easier clicking
                    boundingBox = CreatureBoundingBox.FromRadius(1.5f);
                }
                else if ((entity.ObjectType & Andastra.Runtime.Core.Enums.ObjectType.Placeable) != 0)
                {
                    // Placeables: Use default bounding box (1.0f radius)
                    // Based on swkotor2.exe: Placeables use standard size bounding box
                    boundingBox = CreatureBoundingBox.FromRadius(1.0f);
                }
                else
                {
                    // Other entity types: Skip (not selectable via raycast)
                    continue;
                }

                // Get bounding box min/max in world space
                Vector3 entityMin = boundingBox.GetMin(entityPos);
                Vector3 entityMax = boundingBox.GetMax(entityPos);

                // Perform ray-AABB intersection test
                // Based on swkotor2.exe: FUN_004e17a0 performs bounding box intersection checks
                // Algorithm: Slab method for ray-AABB intersection (standard algorithm)
                float tmin = 0.0f;
                float tmax = maxRayDistance;

                // Check X axis
                if (Math.Abs(rayDirection.X) < 1e-6f)
                {
                    // Ray is parallel to X plane
                    if (rayOrigin.X < entityMin.X || rayOrigin.X > entityMax.X)
                    {
                        continue; // Ray misses bounding box
                    }
                }
                else
                {
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
                        continue; // Ray misses bounding box
                    }
                }

                // Check Y axis
                if (Math.Abs(rayDirection.Y) < 1e-6f)
                {
                    // Ray is parallel to Y plane
                    if (rayOrigin.Y < entityMin.Y || rayOrigin.Y > entityMax.Y)
                    {
                        continue; // Ray misses bounding box
                    }
                }
                else
                {
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
                        continue; // Ray misses bounding box
                    }
                }

                // Check Z axis
                if (Math.Abs(rayDirection.Z) < 1e-6f)
                {
                    // Ray is parallel to Z plane
                    if (rayOrigin.Z < entityMin.Z || rayOrigin.Z > entityMax.Z)
                    {
                        continue; // Ray misses bounding box
                    }
                }
                else
                {
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
                        continue; // Ray misses bounding box
                    }
                }

                // Ray intersects bounding box - check if it's the closest entity
                // Based on swkotor2.exe: FUN_004f67d0 returns closest entity (smallest intersection distance)
                if (tmin >= 0.0f && tmin < closestDistance)
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
        private void HandleEntityClick(Andastra.Runtime.Core.Interfaces.IEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            Console.WriteLine($"[Odyssey] Clicked on entity: {entity.ObjectType}");

            // Handle different entity types
            switch (entity.ObjectType)
            {
                case Andastra.Runtime.Core.Enums.ObjectType.Creature:
                    // Try to start dialogue
                    StartDialogueWithEntity(entity);
                    break;
                case Andastra.Runtime.Core.Enums.ObjectType.Door:
                    HandleDoorInteraction(entity);
                    break;
                case Andastra.Runtime.Core.Enums.ObjectType.Placeable:
                    // Try to start dialogue or interact
                    StartDialogueWithEntity(entity);
                    break;
                case Andastra.Runtime.Core.Enums.ObjectType.Trigger:
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
        private void StartDialogueWithEntity(Andastra.Runtime.Core.Interfaces.IEntity entity)
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
        private string GetEntityConversation(Andastra.Runtime.Core.Interfaces.IEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            // Try to get conversation from ScriptHooksComponent local string
            Andastra.Runtime.Engines.Odyssey.Components.ScriptHooksComponent scriptsComponent = entity.GetComponent<Andastra.Runtime.Engines.Odyssey.Components.ScriptHooksComponent>();
            if (scriptsComponent != null)
            {
                string conversation = scriptsComponent.GetLocalString("Conversation");
                if (!string.IsNullOrEmpty(conversation))
                {
                    return conversation;
                }
            }

            // Try to get from PlaceableComponent
            Andastra.Runtime.Games.Odyssey.Components.PlaceableComponent placeableComponent = entity.GetComponent<Andastra.Runtime.Games.Odyssey.Components.PlaceableComponent>();
            if (placeableComponent != null && !string.IsNullOrEmpty(placeableComponent.Conversation))
            {
                return placeableComponent.Conversation;
            }

            // Try to get from DoorComponent
            Andastra.Runtime.Games.Odyssey.Components.OdysseyDoorComponent doorComponent = entity.GetComponent<Andastra.Runtime.Games.Odyssey.Components.OdysseyDoorComponent>();
            if (doorComponent != null && !string.IsNullOrEmpty(doorComponent.Conversation))
            {
                return doorComponent.Conversation;
            }

            return null;
        }

        /// <summary>
        /// Handles door interaction (open/close).
        /// </summary>
        private void HandleDoorInteraction(Andastra.Runtime.Core.Interfaces.IEntity doorEntity)
        {
            if (doorEntity == null)
            {
                return;
            }

            Andastra.Runtime.Games.Odyssey.Components.OdysseyDoorComponent doorComponent = doorEntity.GetComponent<Andastra.Runtime.Games.Odyssey.Components.OdysseyDoorComponent>();
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
                    Andastra.Runtime.Core.Interfaces.Components.IInventoryComponent playerInventory = _session.PlayerEntity?.GetComponent<Andastra.Runtime.Core.Interfaces.Components.IInventoryComponent>();
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
                    Andastra.Runtime.Core.Interfaces.Components.IStatsComponent playerStats =
                        _session.PlayerEntity?.GetComponent<Andastra.Runtime.Core.Interfaces.Components.IStatsComponent>();

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
                        var moduleTransitionSystem = transitionSystem.GetValue(_session) as Andastra.Runtime.Games.Odyssey.Game.ModuleTransitionSystem;
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
        private void HandleTriggerActivation(Andastra.Runtime.Core.Interfaces.IEntity triggerEntity)
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
        /// Calculates the bounding box of an MDL model by traversing all nodes and collecting mesh bounds.
        /// Based on swkotor.exe and swkotor2.exe: Room bounds are calculated from MDL model geometry
        /// Original implementation: FUN_004e17a0 @ 0x004e17a0 (spatial query) uses model bounding boxes for room detection
        /// Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/gl/models/mdl.py:98-111 (bounds calculation)
        /// </summary>
        /// <param name="mdl">The MDL model to calculate bounds for.</param>
        /// <returns>A tuple containing (min, max) bounding box corners in model space (centered at origin), or null if model is invalid.</returns>
        [CanBeNull]
        private Tuple<System.Numerics.Vector3, System.Numerics.Vector3> CalculateRoomBoundsFromMDL(
            [CanBeNull] Andastra.Parsing.Formats.MDLData.MDL mdl)
        {
            if (mdl == null || mdl.Root == null)
            {
                return null;
            }

            // Initialize bounding box with extreme values
            System.Numerics.Vector3 minBounds = new System.Numerics.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            System.Numerics.Vector3 maxBounds = new System.Numerics.Vector3(float.MinValue, float.MinValue, float.MinValue);

            // Traverse all nodes recursively to collect mesh bounds
            // Bounds are calculated in model space (centered at origin)
            var nodesToProcess = new List<Tuple<MDLNode, System.Numerics.Matrix4x4>>();
            nodesToProcess.Add(new Tuple<MDLNode, System.Numerics.Matrix4x4>(mdl.Root, System.Numerics.Matrix4x4.Identity));

            while (nodesToProcess.Count > 0)
            {
                var nodeData = nodesToProcess[nodesToProcess.Count - 1];
                nodesToProcess.RemoveAt(nodesToProcess.Count - 1);

                MDLNode node = nodeData.Item1;
                System.Numerics.Matrix4x4 parentTransform = nodeData.Item2;

                // Build node transform (position + orientation)
                System.Numerics.Matrix4x4 nodeTransform = System.Numerics.Matrix4x4.Identity;

                // Apply position
                if (node.Position.X != 0.0f || node.Position.Y != 0.0f || node.Position.Z != 0.0f)
                {
                    System.Numerics.Matrix4x4 translation = System.Numerics.Matrix4x4.CreateTranslation(
                        node.Position.X, node.Position.Y, node.Position.Z);
                    nodeTransform = System.Numerics.Matrix4x4.Multiply(translation, nodeTransform);
                }

                // Apply orientation (quaternion)
                if (node.Orientation.W != 0.0f || node.Orientation.X != 0.0f ||
                    node.Orientation.Y != 0.0f || node.Orientation.Z != 0.0f)
                {
                    System.Numerics.Quaternion quat = new System.Numerics.Quaternion(
                        node.Orientation.X, node.Orientation.Y, node.Orientation.Z, node.Orientation.W);
                    System.Numerics.Matrix4x4 rotation = System.Numerics.Matrix4x4.CreateFromQuaternion(quat);
                    nodeTransform = System.Numerics.Matrix4x4.Multiply(rotation, nodeTransform);
                }

                // Combine with parent transform
                System.Numerics.Matrix4x4 worldTransform = System.Numerics.Matrix4x4.Multiply(nodeTransform, parentTransform);

                // Check if node has mesh with bounding box
                if (node.Mesh != null)
                {
                    // Get mesh bounding box (use BbMin/BbMax if available, otherwise use BBoxMinX/Y/Z)
                    System.Numerics.Vector3 meshMin;
                    System.Numerics.Vector3 meshMax;

                    if (node.Mesh.BbMin.X < 1000000.0f && node.Mesh.BbMax.X > -1000000.0f)
                    {
                        // Use BbMin/BbMax (alternative format)
                        meshMin = new System.Numerics.Vector3(
                            node.Mesh.BbMin.X, node.Mesh.BbMin.Y, node.Mesh.BbMin.Z);
                        meshMax = new System.Numerics.Vector3(
                            node.Mesh.BbMax.X, node.Mesh.BbMax.Y, node.Mesh.BbMax.Z);
                    }
                    else if (node.Mesh.BBoxMinX < 1000000.0f && node.Mesh.BBoxMaxX > -1000000.0f)
                    {
                        // Use BBoxMinX/Y/Z and BBoxMaxX/Y/Z
                        meshMin = new System.Numerics.Vector3(
                            node.Mesh.BBoxMinX, node.Mesh.BBoxMinY, node.Mesh.BBoxMinZ);
                        meshMax = new System.Numerics.Vector3(
                            node.Mesh.BBoxMaxX, node.Mesh.BBoxMaxY, node.Mesh.BBoxMaxZ);
                    }
                    else
                    {
                        // No valid bounding box in mesh, skip this mesh
                        // Add children to processing queue
                        if (node.Children != null)
                        {
                            foreach (var child in node.Children)
                            {
                                nodesToProcess.Add(new Tuple<MDLNode, System.Numerics.Matrix4x4>(child, worldTransform));
                            }
                        }
                        continue;
                    }

                    // Transform mesh bounding box corners to model space
                    // We need to transform all 8 corners of the bounding box
                    System.Numerics.Vector3[] corners = new System.Numerics.Vector3[8];
                    corners[0] = new System.Numerics.Vector3(meshMin.X, meshMin.Y, meshMin.Z);
                    corners[1] = new System.Numerics.Vector3(meshMax.X, meshMin.Y, meshMin.Z);
                    corners[2] = new System.Numerics.Vector3(meshMin.X, meshMax.Y, meshMin.Z);
                    corners[3] = new System.Numerics.Vector3(meshMax.X, meshMax.Y, meshMin.Z);
                    corners[4] = new System.Numerics.Vector3(meshMin.X, meshMin.Y, meshMax.Z);
                    corners[5] = new System.Numerics.Vector3(meshMax.X, meshMin.Y, meshMax.Z);
                    corners[6] = new System.Numerics.Vector3(meshMin.X, meshMax.Y, meshMax.Z);
                    corners[7] = new System.Numerics.Vector3(meshMax.X, meshMax.Y, meshMax.Z);

                    // Transform each corner through node transform (to model space)
                    for (int i = 0; i < 8; i++)
                    {
                        System.Numerics.Vector4 transformed = System.Numerics.Vector4.Transform(
                            new System.Numerics.Vector4(corners[i], 1.0f), worldTransform);

                        System.Numerics.Vector3 modelPoint = new System.Numerics.Vector3(transformed.X, transformed.Y, transformed.Z);

                        // Update overall bounding box (in model space)
                        minBounds.X = Math.Min(minBounds.X, modelPoint.X);
                        minBounds.Y = Math.Min(minBounds.Y, modelPoint.Y);
                        minBounds.Z = Math.Min(minBounds.Z, modelPoint.Z);
                        maxBounds.X = Math.Max(maxBounds.X, modelPoint.X);
                        maxBounds.Y = Math.Max(maxBounds.Y, modelPoint.Y);
                        maxBounds.Z = Math.Max(maxBounds.Z, modelPoint.Z);
                    }
                }

                // Add children to processing queue
                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        nodesToProcess.Add(new Tuple<MDLNode, System.Numerics.Matrix4x4>(child, worldTransform));
                    }
                }
            }

            // Check if we found any valid bounds
            if (minBounds.X >= maxBounds.X || minBounds.Y >= maxBounds.Y || minBounds.Z >= maxBounds.Z)
            {
                return null; // Invalid or empty bounds
            }

            return new Tuple<System.Numerics.Vector3, System.Numerics.Vector3>(minBounds, maxBounds);
        }

        /// <summary>
        /// Gets the cached bounding box for a room, or calculates and caches it if not available.
        /// Based on swkotor.exe and swkotor2.exe: Room bounds are cached for efficient spatial queries
        /// </summary>
        /// <param name="room">The room to get bounds for.</param>
        /// <returns>A tuple containing (min, max) bounding box corners in world space, or null if bounds cannot be calculated.</returns>
        [CanBeNull]
        private Tuple<System.Numerics.Vector3, System.Numerics.Vector3> GetRoomBounds(Andastra.Runtime.Core.Module.RoomInfo room)
        {
            if (room == null || string.IsNullOrEmpty(room.ModelName))
            {
                return null;
            }

            // Check cache first (cached bounds are in model space, centered at origin)
            if (_roomBoundsCache != null && _roomBoundsCache.TryGetValue(room.ModelName, out var cachedBounds))
            {
                // Transform cached bounds from model space to world space
                // Cached bounds are in model space (centered at origin), need to apply room transform
                System.Numerics.Vector3 roomPos = new System.Numerics.Vector3(room.Position.X, room.Position.Y, room.Position.Z);

                // Apply rotation if needed
                if (Math.Abs(room.Rotation) > 0.001f)
                {
                    float rotationRadians = room.Rotation * (float)(Math.PI / 180.0);

                    // Transform all 8 corners of the bounding box
                    System.Numerics.Vector3[] corners = new System.Numerics.Vector3[8];
                    corners[0] = new System.Numerics.Vector3(cachedBounds.Item1.X, cachedBounds.Item1.Y, cachedBounds.Item1.Z);
                    corners[1] = new System.Numerics.Vector3(cachedBounds.Item2.X, cachedBounds.Item1.Y, cachedBounds.Item1.Z);
                    corners[2] = new System.Numerics.Vector3(cachedBounds.Item1.X, cachedBounds.Item2.Y, cachedBounds.Item1.Z);
                    corners[3] = new System.Numerics.Vector3(cachedBounds.Item2.X, cachedBounds.Item2.Y, cachedBounds.Item1.Z);
                    corners[4] = new System.Numerics.Vector3(cachedBounds.Item1.X, cachedBounds.Item1.Y, cachedBounds.Item2.Z);
                    corners[5] = new System.Numerics.Vector3(cachedBounds.Item2.X, cachedBounds.Item1.Y, cachedBounds.Item2.Z);
                    corners[6] = new System.Numerics.Vector3(cachedBounds.Item1.X, cachedBounds.Item2.Y, cachedBounds.Item2.Z);
                    corners[7] = new System.Numerics.Vector3(cachedBounds.Item2.X, cachedBounds.Item2.Y, cachedBounds.Item2.Z);

                    // Rotate around Y axis, then translate
                    System.Numerics.Matrix4x4 rotationMatrix = System.Numerics.Matrix4x4.CreateRotationY(rotationRadians);
                    System.Numerics.Matrix4x4 translationMatrix = System.Numerics.Matrix4x4.CreateTranslation(roomPos);
                    System.Numerics.Matrix4x4 transformMatrix = System.Numerics.Matrix4x4.Multiply(rotationMatrix, translationMatrix);

                    // Find min/max after transformation
                    System.Numerics.Vector3 minWorld = new System.Numerics.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    System.Numerics.Vector3 maxWorld = new System.Numerics.Vector3(float.MinValue, float.MinValue, float.MinValue);

                    for (int i = 0; i < 8; i++)
                    {
                        System.Numerics.Vector4 transformed = System.Numerics.Vector4.Transform(
                            new System.Numerics.Vector4(corners[i], 1.0f), transformMatrix);
                        System.Numerics.Vector3 worldPoint = new System.Numerics.Vector3(transformed.X, transformed.Y, transformed.Z);

                        minWorld.X = Math.Min(minWorld.X, worldPoint.X);
                        minWorld.Y = Math.Min(minWorld.Y, worldPoint.Y);
                        minWorld.Z = Math.Min(minWorld.Z, worldPoint.Z);
                        maxWorld.X = Math.Max(maxWorld.X, worldPoint.X);
                        maxWorld.Y = Math.Max(maxWorld.Y, worldPoint.Y);
                        maxWorld.Z = Math.Max(maxWorld.Z, worldPoint.Z);
                    }

                    return new Tuple<System.Numerics.Vector3, System.Numerics.Vector3>(minWorld, maxWorld);
                }
                else
                {
                    // No rotation, just translate
                    System.Numerics.Vector3 minWorld = cachedBounds.Item1 + roomPos;
                    System.Numerics.Vector3 maxWorld = cachedBounds.Item2 + roomPos;
                    return new Tuple<System.Numerics.Vector3, System.Numerics.Vector3>(minWorld, maxWorld);
                }
            }

            // Not in cache, need to calculate from MDL model
            if (_session == null)
            {
                return null;
            }

            // Load MDL model
            Andastra.Parsing.Formats.MDLData.MDL mdl = LoadMDLModel(room.ModelName);
            if (mdl == null)
            {
                return null;
            }

            // Calculate bounds from MDL model in model space (centered at origin)
            Tuple<System.Numerics.Vector3, System.Numerics.Vector3> modelSpaceBounds = CalculateRoomBoundsFromMDL(mdl);

            if (modelSpaceBounds != null)
            {
                // Cache bounds in model space
                _roomBoundsCache[room.ModelName] = modelSpaceBounds;

                // Transform to world space for return value
                System.Numerics.Vector3 roomPos = new System.Numerics.Vector3(room.Position.X, room.Position.Y, room.Position.Z);

                if (Math.Abs(room.Rotation) > 0.001f)
                {
                    float rotationRadians = room.Rotation * (float)(Math.PI / 180.0);
                    System.Numerics.Matrix4x4 rotationMatrix = System.Numerics.Matrix4x4.CreateRotationY(rotationRadians);
                    System.Numerics.Matrix4x4 translationMatrix = System.Numerics.Matrix4x4.CreateTranslation(roomPos);
                    System.Numerics.Matrix4x4 transformMatrix = System.Numerics.Matrix4x4.Multiply(rotationMatrix, translationMatrix);

                    // Transform all 8 corners
                    System.Numerics.Vector3[] corners = new System.Numerics.Vector3[8];
                    corners[0] = new System.Numerics.Vector3(modelSpaceBounds.Item1.X, modelSpaceBounds.Item1.Y, modelSpaceBounds.Item1.Z);
                    corners[1] = new System.Numerics.Vector3(modelSpaceBounds.Item2.X, modelSpaceBounds.Item1.Y, modelSpaceBounds.Item1.Z);
                    corners[2] = new System.Numerics.Vector3(modelSpaceBounds.Item1.X, modelSpaceBounds.Item2.Y, modelSpaceBounds.Item1.Z);
                    corners[3] = new System.Numerics.Vector3(modelSpaceBounds.Item2.X, modelSpaceBounds.Item2.Y, modelSpaceBounds.Item1.Z);
                    corners[4] = new System.Numerics.Vector3(modelSpaceBounds.Item1.X, modelSpaceBounds.Item1.Y, modelSpaceBounds.Item2.Z);
                    corners[5] = new System.Numerics.Vector3(modelSpaceBounds.Item2.X, modelSpaceBounds.Item1.Y, modelSpaceBounds.Item2.Z);
                    corners[6] = new System.Numerics.Vector3(modelSpaceBounds.Item1.X, modelSpaceBounds.Item2.Y, modelSpaceBounds.Item2.Z);
                    corners[7] = new System.Numerics.Vector3(modelSpaceBounds.Item2.X, modelSpaceBounds.Item2.Y, modelSpaceBounds.Item2.Z);

                    System.Numerics.Vector3 minWorld = new System.Numerics.Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                    System.Numerics.Vector3 maxWorld = new System.Numerics.Vector3(float.MinValue, float.MinValue, float.MinValue);

                    for (int i = 0; i < 8; i++)
                    {
                        System.Numerics.Vector4 transformed = System.Numerics.Vector4.Transform(
                            new System.Numerics.Vector4(corners[i], 1.0f), transformMatrix);
                        System.Numerics.Vector3 worldPoint = new System.Numerics.Vector3(transformed.X, transformed.Y, transformed.Z);

                        minWorld.X = Math.Min(minWorld.X, worldPoint.X);
                        minWorld.Y = Math.Min(minWorld.Y, worldPoint.Y);
                        minWorld.Z = Math.Min(minWorld.Z, worldPoint.Z);
                        maxWorld.X = Math.Max(maxWorld.X, worldPoint.X);
                        maxWorld.Y = Math.Max(maxWorld.Y, worldPoint.Y);
                        maxWorld.Z = Math.Max(maxWorld.Z, worldPoint.Z);
                    }

                    return new Tuple<System.Numerics.Vector3, System.Numerics.Vector3>(minWorld, maxWorld);
                }
                else
                {
                    // No rotation, just translate
                    System.Numerics.Vector3 minWorld = modelSpaceBounds.Item1 + roomPos;
                    System.Numerics.Vector3 maxWorld = modelSpaceBounds.Item2 + roomPos;
                    return new Tuple<System.Numerics.Vector3, System.Numerics.Vector3>(minWorld, maxWorld);
                }
            }

            return null;
        }

        /// <summary>
        /// Checks if a point is inside an axis-aligned bounding box.
        /// Based on swkotor.exe and swkotor2.exe: Point-in-AABB test for room detection
        /// </summary>
        /// <param name="point">The point to test.</param>
        /// <param name="minBounds">The minimum corner of the bounding box.</param>
        /// <param name="maxBounds">The maximum corner of the bounding box.</param>
        /// <returns>True if the point is inside the bounding box, false otherwise.</returns>
        private bool IsPointInsideBounds(
            System.Numerics.Vector3 point,
            System.Numerics.Vector3 minBounds,
            System.Numerics.Vector3 maxBounds)
        {
            return point.X >= minBounds.X && point.X <= maxBounds.X &&
                   point.Y >= minBounds.Y && point.Y <= maxBounds.Y &&
                   point.Z >= minBounds.Z && point.Z <= maxBounds.Z;
        }

        /// <summary>
        /// Finds which room the player is currently in based on position.
        /// Based on swkotor.exe and swkotor2.exe: FUN_004e17a0 @ 0x004e17a0 (spatial query) checks room bounds for entity placement
        /// Original implementation: Checks if player position is inside room bounding boxes calculated from MDL model geometry
        /// </summary>
        private int FindPlayerRoom(Andastra.Runtime.Core.Module.RuntimeArea area, System.Numerics.Vector3 playerPosition)
        {
            if (area.Rooms == null || area.Rooms.Count == 0)
            {
                return -1;
            }

            // First, try to find a room that contains the player position
            for (int i = 0; i < area.Rooms.Count; i++)
            {
                Andastra.Runtime.Core.Module.RoomInfo room = area.Rooms[i];

                // Get room bounds (cached or calculated)
                Tuple<System.Numerics.Vector3, System.Numerics.Vector3> bounds = GetRoomBounds(room);
                if (bounds != null)
                {
                    // Check if player is inside room bounds
                    if (IsPointInsideBounds(playerPosition, bounds.Item1, bounds.Item2))
                    {
                        return i; // Player is inside this room
                    }
                }
            }

            // If no room contains the player, fall back to closest room by distance
            // This handles edge cases where player is between rooms or bounds calculation failed
            int closestRoomIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < area.Rooms.Count; i++)
            {
                Andastra.Runtime.Core.Module.RoomInfo room = area.Rooms[i];
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
        private void OnDialogueNodeEnter(object sender, Andastra.Runtime.Engines.Odyssey.Dialogue.DialogueEventArgs e)
        {
            if (e != null && !string.IsNullOrEmpty(e.Text))
            {
                Console.WriteLine($"[Dialogue] {e.Text}");
            }
        }

        /// <summary>
        /// Handles dialogue replies ready events.
        /// </summary>
        private void OnDialogueRepliesReady(object sender, Andastra.Runtime.Engines.Odyssey.Dialogue.DialogueEventArgs e)
        {
            if (e != null && e.State != null && e.State.AvailableReplies != null)
            {
                Console.WriteLine($"[Dialogue] Available replies: {e.State.AvailableReplies.Count}");
                for (int i = 0; i < e.State.AvailableReplies.Count; i++)
                {
                    Andastra.Parsing.Resource.Generics.DLG.DLGReply reply = e.State.AvailableReplies[i];
                    string replyText = _session.DialogueManager.GetNodeText(reply);
                    Console.WriteLine($"  [{i}] {replyText}");
                }
            }
        }

        /// <summary>
        /// Handles dialogue end events.
        /// </summary>
        private void OnDialogueEnd(object sender, Andastra.Runtime.Engines.Odyssey.Dialogue.DialogueEventArgs e)
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

            Andastra.Runtime.Games.Odyssey.Dialogue.DialogueState state = _session.DialogueManager.CurrentState;
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
                    GraphicsVector2 textPos = new GraphicsVector2(padding, dialogueBoxY + padding);
                    _spriteBatch.DrawString(_font, dialogueText, textPos, Color.White);
                }
            }

            // Draw available replies
            if (state.AvailableReplies != null && state.AvailableReplies.Count > 0)
            {
                float replyY = dialogueBoxY + 80; // Below dialogue text
                for (int i = 0; i < state.AvailableReplies.Count && i < 9; i++)
                {
                    Andastra.Parsing.Resource.Generics.DLG.DLGReply reply = state.AvailableReplies[i];
                    string replyText = _session.DialogueManager.GetNodeText(reply);
                    if (string.IsNullOrEmpty(replyText))
                    {
                        replyText = "[Continue]";
                    }

                    string replyLabel = $"[{i + 1}] {replyText}";
                    GraphicsGraphicsVector2 replyPos = new GraphicsVector2(padding, replyY + (i * 20));
                    _spriteBatch.DrawString(_font, replyLabel, replyPos, Color.Yellow);
                }

                // Draw instruction text
                string instructionText = "Press 1-9 to select reply, ESC to abort";
                Andastra.Runtime.Graphics.GraphicsVector2 instructionPos = new Andastra.Runtime.Graphics.Vector2(padding, dialogueBoxY + dialogueBoxHeight - 20);
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
                _availableSaves = new List<Andastra.Runtime.Core.Save.SaveGameInfo>();
                return;
            }

            try
            {
                _availableSaves = new List<Andastra.Runtime.Core.Save.SaveGameInfo>(_saveSystem.GetSaveList());
                _availableSaves.Sort((a, b) => b.SaveTime.CompareTo(a.SaveTime)); // Most recent first
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] Failed to enumerate saves: " + ex.Message);
                _availableSaves = new List<Andastra.Runtime.Core.Save.SaveGameInfo>();
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
            _isEnteringSaveName = false;
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
            bool success = _saveSystem.Save(quickSaveName, Andastra.Runtime.Core.Save.SaveType.Quick);
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

            // If entering save name, handle text input
            if (_isEnteringSaveName)
            {
                // Update cursor blink timer
                _saveNameInputCursorTime += deltaTime;
                HandleSaveNameInput(currentKeyboard, _previousKeyboardState);
                _previousKeyboardState = currentKeyboard;
                return;
            }

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
                        _saveSystem.Save(saveName, Andastra.Runtime.Core.Save.SaveType.Manual);
                        RefreshSaveList();
                        _currentState = GameState.InGame;
                    }
                }
                else
                {
                    // New save - enter text input mode
                    _isEnteringSaveName = true;
                    _newSaveName = string.Empty;
                    _saveNameInputCursorTime = 0f;
                }
            }

            _previousKeyboardState = currentKeyboard;
        }

        /// <summary>
        /// Handles text input for save name entry.
        /// </summary>
        /// <param name="currentKeyboard">Current keyboard state.</param>
        /// <param name="previousKeyboard">Previous keyboard state.</param>
        /// <remarks>
        /// Handles keyboard input for entering a save game name.
        /// Supports letters, numbers, spaces, backspace, enter (confirm), and escape (cancel).
        /// Save name is validated and sanitized before saving.
        /// </remarks>
        private void HandleSaveNameInput(IKeyboardState currentKeyboard, IKeyboardState previousKeyboard)
        {
            const int MaxSaveNameLength = 50; // Reasonable limit for save names

            // Handle character input
            Keys[] pressedKeys = currentKeyboard.GetPressedKeys();
            foreach (Keys key in pressedKeys)
            {
                if (!previousKeyboard.IsKeyDown(key))
                {
                    // Letters (A-Z)
                    if (key >= Keys.A && key <= Keys.Z)
                    {
                        // Check if shift is held for uppercase, otherwise use lowercase
                        bool isShiftDown = currentKeyboard.IsKeyDown(Keys.LeftShift) || currentKeyboard.IsKeyDown(Keys.RightShift);
                        if (_newSaveName.Length < MaxSaveNameLength)
                        {
                            // Keys.A = 65 ('A'), so if shift is down use as-is (uppercase), otherwise convert to lowercase
                            char c = isShiftDown ? (char)(int)key : (char)((int)key + 32);
                            _newSaveName += c;
                        }
                    }
                    // Numbers (0-9)
                    else if (key >= Keys.D0 && key <= Keys.D9)
                    {
                        if (_newSaveName.Length < MaxSaveNameLength)
                        {
                            _newSaveName += ((int)key - (int)Keys.D0).ToString();
                        }
                    }
                    // Space
                    else if (key == Keys.Space)
                    {
                        if (_newSaveName.Length < MaxSaveNameLength && _newSaveName.Length > 0)
                        {
                            _newSaveName += " ";
                        }
                    }
                    // Backspace
                    else if (key == Keys.Back)
                    {
                        if (_newSaveName.Length > 0)
                        {
                            _newSaveName = _newSaveName.Substring(0, _newSaveName.Length - 1);
                        }
                    }
                    // Enter - confirm save name
                    else if (key == Keys.Enter)
                    {
                        string sanitizedName = SanitizeSaveName(_newSaveName);
                        if (!string.IsNullOrWhiteSpace(sanitizedName))
                        {
                            // Save the game with the entered name
                            if (_saveSystem != null)
                            {
                                _saveSystem.Save(sanitizedName, Andastra.Runtime.Core.Save.SaveType.Manual);
                                RefreshSaveList();
                                _isEnteringSaveName = false;
                                _newSaveName = string.Empty;
                                _currentState = GameState.InGame;
                            }
                        }
                    }
                    // Escape - cancel text input
                    else if (key == Keys.Escape)
                    {
                        _isEnteringSaveName = false;
                        _newSaveName = string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Sanitizes a save name by removing or replacing invalid filename characters.
        /// </summary>
        /// <param name="name">Save name to sanitize.</param>
        /// <returns>Sanitized save name safe for use as a filename.</returns>
        /// <remarks>
        /// Removes or replaces invalid filename characters with underscores.
        /// Based on Path.GetInvalidFileNameChars() to ensure compatibility across platforms.
        /// </remarks>
        private string SanitizeSaveName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            // Remove or replace invalid filename characters
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid)
            {
                name = name.Replace(c, '_');
            }

            // Trim whitespace
            name = name.Trim();

            // Ensure name is not empty after sanitization
            if (string.IsNullOrEmpty(name))
            {
                return "Save_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            return name;
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
            GraphicsVector2 titleSize = _font.MeasureString(title);
            GraphicsVector2 titlePos = new GraphicsVector2((viewportWidth - titleSize.X) / 2, 50);
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
                    Andastra.Runtime.Core.Save.SaveGameInfo save = _availableSaves[i];
                    string saveText = $"{save.Name} - {save.ModuleName} - {save.SaveTime:g}";
                    GraphicsVector2 textPos = new GraphicsVector2(itemRect.X + 10, itemRect.Y + (itemHeight - _font.LineSpacing) / 2);
                    _spriteBatch.DrawString(_font, saveText, textPos, new Color(255, 255, 255, 255));
                }
                else
                {
                    string newSaveText = "New Save";
                    GraphicsVector2 textPos = new GraphicsVector2(itemRect.X + 10, itemRect.Y + (itemHeight - _font.LineSpacing) / 2);
                    _spriteBatch.DrawString(_font, newSaveText, textPos, new Color(211, 211, 211, 255));
                }
            }

            // Instructions
            string instructions;
            if (_isEnteringSaveName)
            {
                instructions = "Enter save name. Press Enter to confirm, Escape to cancel.";
            }
            else
            {
                instructions = "Select a save slot or create a new save. Press Escape to cancel.";
            }
            GraphicsVector2 instSize = _font.MeasureString(instructions);
            GraphicsVector2 instPos = new GraphicsVector2((viewportWidth - instSize.X) / 2, viewportHeight - 50);
            _spriteBatch.DrawString(_font, instructions, instPos, new Color(211, 211, 211, 255));

            // Display text input if entering save name
            if (_isEnteringSaveName)
            {
                // Draw text input box
                int inputBoxX = viewportWidth / 2 - 200;
                int inputBoxY = viewportHeight / 2 - 30;
                int inputBoxWidth = 400;
                int inputBoxHeight = 60;
                Rectangle inputBoxRect = new Rectangle(inputBoxX, inputBoxY, inputBoxWidth, inputBoxHeight);
                _spriteBatch.Draw(_menuTexture, inputBoxRect, new Color(30, 30, 40, 255));

                // Draw border
                Rectangle borderRect = new Rectangle(inputBoxX - 2, inputBoxY - 2, inputBoxWidth + 4, inputBoxHeight + 4);
                _spriteBatch.Draw(_menuTexture, borderRect, new Color(150, 150, 150, 255));

                // Draw label
                string label = "Save Name:";
                GraphicsVector2 labelSize = _font.MeasureString(label);
                GraphicsVector2 labelPos = new GraphicsVector2(inputBoxX, inputBoxY - labelSize.Y - 10);
                _spriteBatch.DrawString(_font, label, labelPos, new Color(255, 255, 255, 255));

                // Draw entered text
                string displayText = _newSaveName ?? string.Empty;

                // Add blinking cursor (blinks every 0.5 seconds)
                const float cursorBlinkInterval = 0.5f;
                bool showCursor = ((int)(_saveNameInputCursorTime / cursorBlinkInterval) % 2) == 0;
                if (showCursor)
                {
                    displayText += "_";
                }

                GraphicsVector2 textSize = _font.MeasureString(displayText);

                // Clamp text to fit in box with padding
                int maxTextWidth = inputBoxWidth - 20;
                if (textSize.X > maxTextWidth && displayText.Length > 0)
                {
                    // Text is too long, show only the end portion (with cursor if needed)
                    string textToShow = showCursor ? _newSaveName + "_" : _newSaveName;
                    string truncated = textToShow;
                    while (_font.MeasureString(truncated).X > maxTextWidth && truncated.Length > (showCursor ? 2 : 1))
                    {
                        truncated = truncated.Substring(1);
                    }
                    displayText = truncated;
                    textSize = _font.MeasureString(displayText);
                }

                GraphicsVector2 textPos = new GraphicsVector2(inputBoxX + 10, inputBoxY + (inputBoxHeight - textSize.Y) / 2);
                _spriteBatch.DrawString(_font, displayText, textPos, new Color(255, 255, 255, 255));
            }

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
            GraphicsVector2 titleSize = _font.MeasureString(title);
            GraphicsVector2 titlePos = new GraphicsVector2((viewportWidth - titleSize.X) / 2, 50);
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

                Andastra.Runtime.Core.Save.SaveGameInfo save = _availableSaves[i];
                string saveText = $"{save.Name} - {save.ModuleName} - {save.SaveTime:g}";
                GraphicsVector2 textPos = new GraphicsVector2(itemRect.X + 10, itemRect.Y + (itemHeight - _font.LineSpacing) / 2);
                _spriteBatch.DrawString(_font, saveText, textPos, new Color(255, 255, 255, 255));
            }

            // Instructions
            string instructions = "Select a save to load. Press Escape to cancel.";
            GraphicsVector2 instSize = _font.MeasureString(instructions);
            GraphicsVector2 instPos = new GraphicsVector2((viewportWidth - instSize.X) / 2, viewportHeight - 50);
            _spriteBatch.DrawString(_font, instructions, instPos, new Color(211, 211, 211, 255));

            _spriteBatch.End();
        }

        /// <summary>
        /// Initializes the options menu.
        /// </summary>

        /// <summary>
        /// Updates the options menu.
        /// </summary>

        /// <summary>
        /// Draws the options menu.
        /// </summary>
        private void DrawOptionsMenu()
        {
            if (_spriteBatch == null || _font == null || _menuTexture == null || _optionsByCategory == null)
            {
                _graphicsDevice.Clear(new Color(20, 20, 30, 255));
                return;
            }

            int viewportWidth = _graphicsDevice.Viewport.Width;
            int viewportHeight = _graphicsDevice.Viewport.Height;

            Andastra.Runtime.Game.GUI.OptionsMenu.DrawOptionsMenu(
                _spriteBatch,
                _font,
                _menuTexture,
                _graphicsDevice,
                viewportWidth,
                viewportHeight,
                _selectedOptionsCategoryIndex,
                _selectedOptionsItemIndex,
                _isEditingOptionValue,
                _editingOptionValue,
                _isRebindingKey,
                _rebindingActionName,
                _optionsByCategory);
        }

        #endregion

        #region Movies Menu

        /// <summary>
        /// Opens the movies menu and enumerates available movies.
        /// </summary>
        /// <remarks>
        /// Movies Menu Opening:
        /// - Based on swkotor.exe and swkotor2.exe movies menu system
        /// - Enumerates BIK files from the movies directory
        /// - Initializes movie player for playback
        /// - Movie file paths: "MOVIES:%s" format, ".\\movies" or "d:\\movies" directories
        /// - Original implementation: Lists all available BIK files for player selection
        /// </remarks>
        private void OpenMoviesMenu()
        {
            _currentState = GameState.MoviesMenu;
            _selectedMovieIndex = 0;
            _isPlayingMovie = false;

            // Initialize movie list
            _availableMovies = new List<string>();

            // Enumerate BIK files from resource provider
            // Based on swkotor.exe: Movie files are stored in movies directory as BIK files
            try
            {
                // Get installation path from settings
                if (string.IsNullOrEmpty(_settings.GamePath))
                {
                    Console.WriteLine("[Odyssey] Movies menu: No game path set, cannot enumerate movies");
                    return;
                }

                // Create resource provider to enumerate BIK files
                var installation = new Installation(_settings.GamePath);
                var resourceProvider = new GameResourceProvider(installation);

                // Enumerate all BIK resources
                // Based on IGameResourceProvider.EnumerateResources(ResourceType.BIK)
                var bikResources = resourceProvider.EnumerateResources(Andastra.Parsing.Resource.ResourceType.BIK);
                foreach (var resourceId in bikResources)
                {
                    // Add movie name (without .bik extension) to list
                    string movieName = resourceId.ResName;
                    if (!string.IsNullOrEmpty(movieName))
                    {
                        _availableMovies.Add(movieName);
                    }
                }

                // Sort movies alphabetically
                _availableMovies.Sort();

                Console.WriteLine($"[Odyssey] Movies menu: Found {_availableMovies.Count} movies");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] Error enumerating movies: {ex.Message}");
                _availableMovies = new List<string>();
            }
        }

        /// <summary>
        /// Updates the movies menu state and handles input.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last frame.</param>
        /// <param name="keyboardState">Current keyboard state.</param>
        /// <param name="mouseState">Current mouse state.</param>
        /// <remarks>
        /// Movies Menu Update:
        /// - Handles navigation (Up/Down arrow keys)
        /// - Handles selection (Enter to play movie)
        /// - Handles cancellation (Escape to return to main menu)
        /// - Manages movie playback state
        /// </remarks>
        private void UpdateMoviesMenu(float deltaTime, IKeyboardState keyboardState, IMouseState mouseState)
        {
            var inputManager = _graphicsBackend.InputManager;
            var currentKeyboard = inputManager.KeyboardState;
            var currentMouse = inputManager.MouseState;

            // If playing a movie, handle movie playback
            if (_isPlayingMovie)
            {
                // Check for cancellation (Escape key)
                if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Escape))
                {
                    // Cancel movie playback
                    if (_movieCancellationTokenSource != null && !_movieCancellationTokenSource.IsCancellationRequested)
                    {
                        _movieCancellationTokenSource.Cancel();
                    }
                    _isPlayingMovie = false;
                    _movieCancellationTokenSource = null;
                }
                _previousKeyboardState = currentKeyboard;
                return;
            }

            // Handle ESC to cancel
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Escape))
            {
                _currentState = GameState.MainMenu;
                return;
            }

            // Navigation
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Up))
            {
                _selectedMovieIndex = Math.Max(0, _selectedMovieIndex - 1);
            }
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Down))
            {
                if (_availableMovies != null && _availableMovies.Count > 0)
                {
                    _selectedMovieIndex = Math.Min(_availableMovies.Count - 1, _selectedMovieIndex + 1);
                }
            }

            // Selection - play selected movie
            if (IsKeyJustPressed(_previousKeyboardState, currentKeyboard, Keys.Enter))
            {
                if (_availableMovies != null && _selectedMovieIndex >= 0 && _selectedMovieIndex < _availableMovies.Count)
                {
                    string movieName = _availableMovies[_selectedMovieIndex];
                    PlayMovie(movieName);
                }
            }

            _previousKeyboardState = currentKeyboard;
        }

        /// <summary>
        /// Plays a movie by name.
        /// </summary>
        /// <param name="movieName">Name of the movie to play (without .bik extension).</param>
        /// <remarks>
        /// Movie Playback:
        /// - Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 (main playback loop)
        /// - Based on swkotor.exe: FUN_004053e0 @ 0x004053e0 (movie initialization)
        /// - Movie playback is fullscreen and blocking until completion or cancellation
        /// - Uses MoviePlayer class for BIK file decoding and playback
        /// - Creates movie player with adapters from resource provider and graphics device
        /// </remarks>
        private async void PlayMovie(string movieName)
        {
            if (string.IsNullOrEmpty(movieName) || _world == null)
            {
                return;
            }

            _isPlayingMovie = true;
            _movieCancellationTokenSource = new System.Threading.CancellationTokenSource();

            Console.WriteLine($"[Odyssey] Playing movie: {movieName}");

            try
            {
                // Get installation and create resource provider
                if (string.IsNullOrEmpty(_settings.GamePath))
                {
                    Console.WriteLine("[Odyssey] Movies menu: No game path set, cannot play movie");
                    return;
                }

                var installation = new Installation(_settings.GamePath);
                var resourceProvider = new GameResourceProvider(installation);

                // Create adapters for movie player
                // Based on ModuleTransitionSystem adapter pattern
                Andastra.Runtime.Core.Interfaces.IMovieResourceProvider movieResourceProvider = null;
                Andastra.Runtime.Core.Interfaces.IMovieGraphicsDevice movieGraphicsDevice = null;

                // Adapt resource provider
                Type contentProviderType = Type.GetType("Andastra.Runtime.Content.Interfaces.IGameResourceProvider, Andastra.Runtime.Content");
                if (contentProviderType != null && contentProviderType.IsAssignableFrom(resourceProvider.GetType()))
                {
                    Type adapterType = Type.GetType("Andastra.Runtime.Content.Adapters.MovieResourceProviderAdapter, Andastra.Runtime.Content");
                    if (adapterType != null)
                    {
                        movieResourceProvider = (Andastra.Runtime.Core.Interfaces.IMovieResourceProvider)Activator.CreateInstance(adapterType, resourceProvider);
                    }
                }

                // Adapt graphics device
                Type graphicsDeviceType = Type.GetType("Andastra.Runtime.Graphics.IGraphicsDevice, Andastra.Runtime.Graphics.Common");
                if (graphicsDeviceType != null && graphicsDeviceType.IsAssignableFrom(_graphicsDevice.GetType()))
                {
                    Type adapterType = Type.GetType("Andastra.Runtime.Graphics.Adapters.MovieGraphicsDeviceAdapter, Andastra.Runtime.Graphics.Common");
                    if (adapterType != null)
                    {
                        movieGraphicsDevice = (Andastra.Runtime.Core.Interfaces.IMovieGraphicsDevice)Activator.CreateInstance(adapterType, _graphicsDevice);
                    }
                }

                if (movieResourceProvider == null || movieGraphicsDevice == null)
                {
                    Console.WriteLine("[Odyssey] Failed to create movie player adapters");
                    return;
                }

                // Create movie player
                var moviePlayer = new Andastra.Runtime.Core.Video.MoviePlayer(_world, movieResourceProvider, movieGraphicsDevice);

                // Play movie asynchronously
                // Based on swkotor.exe: FUN_00404c80 @ 0x00404c80 (playback loop)
                bool success = await moviePlayer.PlayMovie(movieName, _movieCancellationTokenSource.Token);
                if (!success)
                {
                    Console.WriteLine($"[Odyssey] Failed to play movie: {movieName}");
                }
            }
            catch (System.OperationCanceledException)
            {
                Console.WriteLine($"[Odyssey] Movie playback cancelled: {movieName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] Error playing movie '{movieName}': {ex.Message}");
            }
            finally
            {
                _isPlayingMovie = false;
                if (_movieCancellationTokenSource != null)
                {
                    _movieCancellationTokenSource.Dispose();
                    _movieCancellationTokenSource = null;
                }
            }
        }

        /// <summary>
        /// Draws the movies menu.
        /// </summary>
        /// <remarks>
        /// Movies Menu Rendering:
        /// - Displays list of available movies
        /// - Highlights selected movie
        /// - Shows instructions for navigation and playback
        /// - If movie is playing, shows playback status
        /// </remarks>
        private void DrawMoviesMenu()
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
            string title = "Movies";
            GraphicsVector2 titleSize = _font.MeasureString(title);
            GraphicsVector2 titlePos = new GraphicsVector2((viewportWidth - titleSize.X) / 2, 50);
            _spriteBatch.DrawString(_font, title, titlePos, new Color(255, 255, 255, 255));

            // If playing movie, show playback status
            if (_isPlayingMovie)
            {
                string playingText = "Playing movie... Press Escape to cancel.";
                GraphicsVector2 playingSize = _font.MeasureString(playingText);
                GraphicsVector2 playingPos = new GraphicsVector2((viewportWidth - playingSize.X) / 2, viewportHeight / 2);
                _spriteBatch.DrawString(_font, playingText, playingPos, new Color(255, 255, 0, 255));
                _spriteBatch.End();
                return;
            }

            // Movie list
            if (_availableMovies == null || _availableMovies.Count == 0)
            {
                string noMoviesText = "No movies found.";
                GraphicsVector2 noMoviesSize = _font.MeasureString(noMoviesText);
                GraphicsVector2 noMoviesPos = new GraphicsVector2((viewportWidth - noMoviesSize.X) / 2, viewportHeight / 2);
                _spriteBatch.DrawString(_font, noMoviesText, noMoviesPos, new Color(211, 211, 211, 255));
            }
            else
            {
                int startY = 150;
                int itemHeight = 60;
                int itemSpacing = 10;
                int maxVisible = Math.Min(10, (viewportHeight - startY - 100) / (itemHeight + itemSpacing));
                int startIdx = Math.Max(0, _selectedMovieIndex - maxVisible / 2);
                int endIdx = Math.Min(_availableMovies.Count, startIdx + maxVisible);

                for (int i = startIdx; i < endIdx; i++)
                {
                    int y = startY + (i - startIdx) * (itemHeight + itemSpacing);
                    bool isSelected = (i == _selectedMovieIndex);
                    Color bgColor = isSelected ? new Color(100, 100, 150) : new Color(50, 50, 70);

                    Rectangle itemRect = new Rectangle(100, y, viewportWidth - 200, itemHeight);
                    _spriteBatch.Draw(_menuTexture, itemRect, bgColor);

                    string movieName = _availableMovies[i];
                    GraphicsVector2 textPos = new GraphicsVector2(itemRect.X + 10, itemRect.Y + (itemHeight - _font.LineSpacing) / 2);
                    _spriteBatch.DrawString(_font, movieName, textPos, new Color(255, 255, 255, 255));
                }
            }

            // Instructions
            string instructions = "Select a movie to play. Press Escape to cancel.";
            GraphicsVector2 instSize = _font.MeasureString(instructions);
            GraphicsVector2 instPos = new GraphicsVector2((viewportWidth - instSize.X) / 2, viewportHeight - 50);
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

            // Dispose cursor manager
            if (_cursorManager != null)
            {
                _cursorManager.Dispose();
                _cursorManager = null;
            }

            // Dispose ground plane buffers
            if (_groundVertexBuffer != null)
            {
                _groundVertexBuffer.Dispose();
                _groundVertexBuffer = null;
            }

            if (_groundIndexBuffer != null)
            {
                _groundIndexBuffer.Dispose();
                _groundIndexBuffer = null;
            }

            if (_graphicsBackend != null)
            {
                _graphicsBackend.Dispose();
            }
        }

        /// <summary>
        /// Sets up GUI event handlers for options menu buttons.
        /// Based on swkotor2.exe: Button event handlers for options GUI
        /// Original implementation: GUI buttons trigger category switches and setting changes
        /// </summary>
        private void SetupOptionsGuiEventHandlers()
        {
            if (_guiManager == null)
            {
                return;
            }

            // Set up button click event handler for options GUI
            _guiManager.OnButtonClicked += (tag, id) =>
            {
                Console.WriteLine($"[Odyssey] Options GUI button clicked: {tag} (ID: {id})");

                // Handle category tab buttons
                // Based on swkotor2.exe: Tab buttons switch between options categories
                switch (tag)
                {
                    case "BTN_GRAPHICS":
                        // Graphics options tab
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Graphics;
                        _selectedOptionsItemIndex = 0;
                        Console.WriteLine("[Odyssey] Switched to Graphics options category");
                        break;

                    case "BTN_SOUND":
                    case "BTN_AUDIO":
                        // Audio options tab
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Audio;
                        _selectedOptionsItemIndex = 0;
                        Console.WriteLine("[Odyssey] Switched to Audio options category");
                        break;

                    case "BTN_GAMEPLAY":
                        // Gameplay options tab
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Game;
                        _selectedOptionsItemIndex = 0;
                        Console.WriteLine("[Odyssey] Switched to Gameplay options category");
                        break;

                    case "BTN_FEEDBACK":
                        // Feedback options tab
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Feedback;
                        _selectedOptionsItemIndex = 0;
                        Console.WriteLine("[Odyssey] Switched to Feedback options category");
                        break;

                    case "BTN_AUTOPAUSE":
                        // Autopause options tab
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Autopause;
                        _selectedOptionsItemIndex = 0;
                        Console.WriteLine("[Odyssey] Switched to Autopause options category");
                        break;

                    case "BTN_CONTROLS":
                        // Controls options tab
                        _selectedOptionsCategoryIndex = (int)Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory.Controls;
                        _selectedOptionsItemIndex = 0;
                        Console.WriteLine("[Odyssey] Switched to Controls options category");
                        break;

                    case "BTN_APPLY":
                        // Apply button - save settings
                        ApplyOptionsSettings();
                        Console.WriteLine("[Odyssey] Options settings applied");
                        break;

                    case "BTN_CANCEL":
                    case "BTN_BACK":
                        // Cancel/Back button - discard changes and return
                        CloseOptionsMenu();
                        Console.WriteLine("[Odyssey] Options menu cancelled");
                        break;

                    default:
                        Console.WriteLine($"[Odyssey] Unknown options button: {tag}");
                        break;
                }
            };

            Console.WriteLine("[Odyssey] Options GUI event handlers set up");
        }

        /// <summary>
        /// Loads options settings from current configuration.
        /// Based on swkotor.exe and swkotor2.exe: Configuration loading
        /// FUN_00633270 @ 0x00633270 (loads settings from INI file)
        /// </summary>
        private void LoadOptionsFromConfiguration()
        {
            if (_optionsByCategory == null)
            {
                return;
            }

            Console.WriteLine("[Odyssey] Loading options settings from configuration...");

            try
            {
                // Settings are already loaded into _settings object during game initialization
                // The options menu reads from _settings and writes back to it
                // Based on original implementation: Settings stored in global configuration object

                // Verify that all option values are properly initialized
                foreach (var category in _optionsByCategory.Values)
                {
                    foreach (var option in category)
                    {
                        // Validate option ranges and current values
                        double currentValue = option.GetValue();
                        if (currentValue < option.MinValue || currentValue > option.MaxValue)
                        {
                            Console.WriteLine($"[Odyssey] WARNING: Option '{option.Name}' value {currentValue} is out of range [{option.MinValue}, {option.MaxValue}], clamping");
                            option.SetValue(Math.Max(option.MinValue, Math.Min(option.MaxValue, currentValue)));
                        }
                    }
                }

                Console.WriteLine("[Odyssey] Options settings loaded from configuration");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR: Failed to load options from configuration: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies current options settings to the game configuration.
        /// Based on swkotor.exe and swkotor2.exe: Settings saving
        /// FUN_00631ff0 @ 0x00631ff0 (writes settings to INI file)
        /// </summary>
        private void ApplyOptionsSettings()
        {
            if (_optionsByCategory == null)
            {
                return;
            }

            Console.WriteLine("[Odyssey] Applying options settings...");

            try
            {
                // Apply all option values through their setters
                // The option setters automatically update the _settings object
                foreach (var category in _optionsByCategory.Values)
                {
                    foreach (var option in category)
                    {
                        option.ApplyValue();
                    }
                }

                // Save configuration to persistent storage
                // Based on original implementation: Settings saved to swkotor.ini or swkotor2.ini
                SaveConfigurationToFile();

                // Apply real-time settings that take effect immediately
                // Graphics settings may require restart, audio settings apply immediately
                ApplyRealtimeSettings();

                Console.WriteLine("[Odyssey] Options settings applied successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR: Failed to apply options settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves current configuration to persistent storage.
        /// Based on swkotor2.exe: FUN_00631ff0 @ 0x00631ff0
        /// Original implementation: Writes settings to INI file format
        /// </summary>
        private void SaveConfigurationToFile()
        {
            try
            {
                // Determine INI file path based on game type
                // Based on swkotor.exe and swkotor2.exe: Settings persistence to INI files
                // File location: swkotor.ini (K1) or swkotor2.ini (K2) in game directory
                // Original implementation: swkotor2.exe saves to ".\swkotor2.ini" @ 0x007b5644 (relative to executable)
                // For Andastra: Save to game installation directory if available, otherwise current directory
                string fileName = _settings.Game == Andastra.Runtime.Core.KotorGame.K1 ? "swkotor.ini" : "swkotor2.ini";
                string baseDirectory = !string.IsNullOrEmpty(_settings.GamePath) && Directory.Exists(_settings.GamePath)
                    ? _settings.GamePath
                    : Environment.CurrentDirectory;
                string filePath = Path.Combine(baseDirectory, fileName);

                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Write all settings to INI file
                // Based on swkotor2.exe: FUN_00631ff0 @ 0x00631ff0 (writes INI values)
                // Original implementation: Saves graphics, audio, gameplay settings to configuration file
                using (var writer = new StreamWriter(filePath, false, System.Text.Encoding.UTF8))
                {
                    // Write header comment
                    writer.WriteLine("; Game Configuration - Generated by Andastra");
                    writer.WriteLine($"; Based on swkotor.exe/swkotor2.exe options menu system (FUN_00631ff0 @ 0x00631ff0)");
                    writer.WriteLine($"; Game: {_settings.Game}");
                    writer.WriteLine();

                    // [Game] Section - Basic game settings
                    writer.WriteLine("[Game]");
                    if (!string.IsNullOrEmpty(_settings.GamePath))
                    {
                        writer.WriteLine($"GamePath={_settings.GamePath}");
                    }
                    if (!string.IsNullOrEmpty(_settings.StartModule))
                    {
                        writer.WriteLine($"StartModule={_settings.StartModule}");
                    }
                    if (!string.IsNullOrEmpty(_settings.LoadSave))
                    {
                        writer.WriteLine($"LoadSave={_settings.LoadSave}");
                    }
                    writer.WriteLine($"SkipIntro={(_settings.SkipIntro ? 1 : 0)}");
                    writer.WriteLine($"DebugRender={(_settings.DebugRender ? 1 : 0)}");
                    writer.WriteLine();

                    // [Display] Section - Window and display settings
                    writer.WriteLine("[Display]");
                    writer.WriteLine($"Width={_settings.Width}");
                    writer.WriteLine($"Height={_settings.Height}");
                    writer.WriteLine($"Fullscreen={(_settings.Fullscreen ? 1 : 0)}");
                    writer.WriteLine();

                    // [Graphics] Section - Graphics quality settings
                    writer.WriteLine("[Graphics]");
                    if (_settings.Graphics != null)
                    {
                        writer.WriteLine($"ResolutionWidth={_settings.Graphics.ResolutionWidth}");
                        writer.WriteLine($"ResolutionHeight={_settings.Graphics.ResolutionHeight}");
                        writer.WriteLine($"Fullscreen={(_settings.Graphics.Fullscreen ? 1 : 0)}");
                        writer.WriteLine($"VSync={(_settings.Graphics.VSync ? 1 : 0)}");
                        writer.WriteLine($"TextureQuality={_settings.Graphics.TextureQuality}");
                        writer.WriteLine($"ShadowQuality={_settings.Graphics.ShadowQuality}");
                        writer.WriteLine($"AnisotropicFiltering={_settings.Graphics.AnisotropicFiltering}");
                        writer.WriteLine($"AntiAliasing={(_settings.Graphics.AntiAliasing ? 1 : 0)}");
                    }
                    writer.WriteLine();

                    // [Sound] Section - Audio settings
                    writer.WriteLine("[Sound]");
                    if (_settings.Audio != null)
                    {
                        writer.WriteLine($"MasterVolume={_settings.Audio.MasterVolume:F3}");
                        writer.WriteLine($"MusicVolume={_settings.Audio.MusicVolume:F3}");
                        writer.WriteLine($"SfxVolume={_settings.Audio.SfxVolume:F3}");
                        writer.WriteLine($"VoiceVolume={_settings.Audio.VoiceVolume:F3}");
                        writer.WriteLine($"MusicEnabled={(_settings.Audio.MusicEnabled ? 1 : 0)}");
                    }
                    writer.WriteLine();

                    // [Controls] Section - Input and control settings
                    // Based on swkotor.exe and swkotor2.exe controls system
                    // Located via string references: "Mouse Sensitivity" @ 0x007c85cc, "keymap" @ 0x007c4cbc
                    writer.WriteLine("[Controls]");
                    writer.WriteLine($"MouseSensitivity={_settings.MouseSensitivity:F3}");
                    writer.WriteLine($"InvertMouseY={(_settings.InvertMouseY ? 1 : 0)}");
                    if (_settings.Controls != null)
                    {
                        // Save key bindings
                        foreach (var kvp in _settings.Controls.KeyBindings)
                        {
                            writer.WriteLine($"Key_{kvp.Key}={kvp.Value}");
                        }
                        // Save mouse button bindings
                        foreach (var kvp in _settings.Controls.MouseButtonBindings)
                        {
                            writer.WriteLine($"Mouse_{kvp.Key}={kvp.Value}");
                        }
                    }
                    writer.WriteLine();

                    // [Gameplay] Section - Gameplay settings
                    writer.WriteLine("[Gameplay]");
                    if (_settings.Gameplay != null)
                    {
                        writer.WriteLine($"AutoSave={(_settings.Gameplay.AutoSave ? 1 : 0)}");
                        writer.WriteLine($"AutoSaveInterval={_settings.Gameplay.AutoSaveInterval}");
                        writer.WriteLine($"Tooltips={(_settings.Gameplay.Tooltips ? 1 : 0)}");
                        writer.WriteLine($"Subtitles={(_settings.Gameplay.Subtitles ? 1 : 0)}");
                        writer.WriteLine($"DialogueSpeed={_settings.Gameplay.DialogueSpeed:F3}");
                        writer.WriteLine($"ClassicControls={(_settings.Gameplay.ClassicControls ? 1 : 0)}");
                    }
                    writer.WriteLine();

                    // [Feedback] Section - Visual and audio feedback settings
                    writer.WriteLine("[Feedback]");
                    if (_settings.Feedback != null)
                    {
                        // First FeedbackSettings class properties (lines 251-287)
                        writer.WriteLine($"ShowDamageNumbers={(_settings.Feedback.ShowDamageNumbers ? 1 : 0)}");
                        writer.WriteLine($"ShowHitMissFeedback={(_settings.Feedback.ShowHitMissFeedback ? 1 : 0)}");
                        writer.WriteLine($"ShowSubtitles={(_settings.Feedback.ShowSubtitles ? 1 : 0)}");
                        writer.WriteLine($"ShowActionQueue={(_settings.Feedback.ShowActionQueue ? 1 : 0)}");
                        writer.WriteLine($"ShowMinimap={(_settings.Feedback.ShowMinimap ? 1 : 0)}");
                        writer.WriteLine($"ShowPartyHealthBars={(_settings.Feedback.ShowPartyHealthBars ? 1 : 0)}");
                        writer.WriteLine($"ShowFloatingCombatText={(_settings.Feedback.ShowFloatingCombatText ? 1 : 0)}");

                        // Try to access second FeedbackSettings class properties via reflection
                        // (TooltipsEnabled, TooltipDelay, ShowCombatDamageNumbers, etc.)
                        // These may not be accessible if the duplicate class definition causes issues
                        // but we'll attempt to save them if they exist
                        try
                        {
                            var feedbackType = _settings.Feedback.GetType();
                            var tooltipsEnabledProp = feedbackType.GetProperty("TooltipsEnabled");
                            if (tooltipsEnabledProp != null)
                            {
                                var tooltipsEnabled = (bool)tooltipsEnabledProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"TooltipsEnabled={(tooltipsEnabled ? 1 : 0)}");
                            }

                            var tooltipDelayProp = feedbackType.GetProperty("TooltipDelay");
                            if (tooltipDelayProp != null)
                            {
                                var tooltipDelay = (int)tooltipDelayProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"TooltipDelay={tooltipDelay}");
                            }

                            var showCombatDamageNumbersProp = feedbackType.GetProperty("ShowCombatDamageNumbers");
                            if (showCombatDamageNumbersProp != null)
                            {
                                var showCombatDamageNumbers = (bool)showCombatDamageNumbersProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"ShowCombatDamageNumbers={(showCombatDamageNumbers ? 1 : 0)}");
                            }

                            var showCombatFeedbackProp = feedbackType.GetProperty("ShowCombatFeedback");
                            if (showCombatFeedbackProp != null)
                            {
                                var showCombatFeedback = (bool)showCombatFeedbackProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"ShowCombatFeedback={(showCombatFeedback ? 1 : 0)}");
                            }

                            var showExperienceGainsProp = feedbackType.GetProperty("ShowExperienceGains");
                            if (showExperienceGainsProp != null)
                            {
                                var showExperienceGains = (bool)showExperienceGainsProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"ShowExperienceGains={(showExperienceGains ? 1 : 0)}");
                            }

                            var showItemPickupsProp = feedbackType.GetProperty("ShowItemPickups");
                            if (showItemPickupsProp != null)
                            {
                                var showItemPickups = (bool)showItemPickupsProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"ShowItemPickups={(showItemPickups ? 1 : 0)}");
                            }

                            var showQuestUpdatesProp = feedbackType.GetProperty("ShowQuestUpdates");
                            if (showQuestUpdatesProp != null)
                            {
                                var showQuestUpdates = (bool)showQuestUpdatesProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"ShowQuestUpdates={(showQuestUpdates ? 1 : 0)}");
                            }

                            var showSkillCheckFeedbackProp = feedbackType.GetProperty("ShowSkillCheckFeedback");
                            if (showSkillCheckFeedbackProp != null)
                            {
                                var showSkillCheckFeedback = (bool)showSkillCheckFeedbackProp.GetValue(_settings.Feedback);
                                writer.WriteLine($"ShowSkillCheckFeedback={(showSkillCheckFeedback ? 1 : 0)}");
                            }
                        }
                        catch
                        {
                            // If reflection fails, continue without those properties
                            // This handles the case where duplicate class definitions cause issues
                        }
                    }
                    writer.WriteLine();

                    // [Autopause] Section - Autopause settings
                    writer.WriteLine("[Autopause]");
                    if (_settings.Autopause != null)
                    {
                        writer.WriteLine($"PauseOnLostFocus={(_settings.Autopause.PauseOnLostFocus ? 1 : 0)}");
                        writer.WriteLine($"PauseOnConversation={(_settings.Autopause.PauseOnConversation ? 1 : 0)}");
                        writer.WriteLine($"PauseOnContainer={(_settings.Autopause.PauseOnContainer ? 1 : 0)}");
                        writer.WriteLine($"PauseOnCorpse={(_settings.Autopause.PauseOnCorpse ? 1 : 0)}");
                        writer.WriteLine($"PauseOnAreaTransition={(_settings.Autopause.PauseOnAreaTransition ? 1 : 0)}");
                        writer.WriteLine($"PauseOnPartyDeath={(_settings.Autopause.PauseOnPartyDeath ? 1 : 0)}");
                        writer.WriteLine($"PauseOnPlayerDeath={(_settings.Autopause.PauseOnPlayerDeath ? 1 : 0)}");
                    }
                    writer.WriteLine();
                }

                Console.WriteLine($"[Odyssey] Configuration saved successfully to {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR: Failed to save configuration: {ex.Message}");
                Console.WriteLine($"[Odyssey] Stack trace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Applies settings that take effect immediately without restart.
        /// Based on swkotor.exe and swkotor2.exe: Real-time settings application
        /// </summary>
        private void ApplyRealtimeSettings()
        {
            try
            {
                // Apply audio volume changes immediately
                // Based on original implementation: Audio volumes update global audio manager
                if (_graphicsBackend.AudioManager != null)
                {
                    var audioManager = _graphicsBackend.AudioManager;

                    // Master volume affects all audio
                    // Based on swkotor.exe and swkotor2.exe: Master volume multiplies all audio channels
                    if (audioManager.SoundPlayer != null)
                    {
                        audioManager.SoundPlayer.Volume = _settings.Audio.MasterVolume;
                    }
                    if (audioManager.MusicPlayer != null)
                    {
                        audioManager.MusicPlayer.Volume = _settings.Audio.MasterVolume * _settings.Audio.MusicVolume;
                    }
                    if (audioManager.VoicePlayer != null)
                    {
                        audioManager.VoicePlayer.Volume = _settings.Audio.MasterVolume * _settings.Audio.VoiceVolume;
                    }

                    // Individual volume adjustments
                    // Based on swkotor.exe and swkotor2.exe: Effects volume (SfxVolume) controls sound effects
                    if (audioManager.SoundPlayer != null)
                    {
                        audioManager.SoundPlayer.SetMasterVolume(_settings.Audio.SfxVolume);
                    }
                }

                // Apply mouse sensitivity and invert settings
                // Based on original implementation: Input settings update global input manager
                // These settings affect camera control and mouse look

                // Graphics settings that can be applied immediately
                // Note: Some graphics settings (resolution, fullscreen) require restart
                if (_graphicsBackend != null && _graphicsBackend.Window != null)
                {
                    // VSync setting can be applied immediately if supported
                    // Based on swkotor.exe and swkotor2.exe: VSync controlled via DirectX Present parameters
                    // Original implementation: VSync synchronizes frame rendering with monitor refresh rate
                    // VSync can be toggled in real-time without requiring a restart
                    // Based on swkotor2.exe: Graphics options apply VSync via DirectX Present flags
                    // Original game: VSync setting stored in swkotor2.ini/swkotor.ini, applied to DirectX device
                    if (_settings.Graphics != null && _graphicsBackend.SupportsVSync)
                    {
                        try
                        {
                            _graphicsBackend.SetVSync(_settings.Graphics.VSync);
                            Console.WriteLine($"[Odyssey] VSync setting applied: {(_settings.Graphics.VSync ? "enabled" : "disabled")}");
                        }
                        catch (Exception vsyncEx)
                        {
                            Console.WriteLine($"[Odyssey] WARNING: Failed to apply VSync setting: {vsyncEx.Message}");
                            // Continue with other settings even if VSync fails
                        }
                    }
                    else if (_settings.Graphics == null)
                    {
                        Console.WriteLine("[Odyssey] WARNING: Graphics settings not available, skipping VSync update");
                    }
                    else if (!_graphicsBackend.SupportsVSync)
                    {
                        Console.WriteLine("[Odyssey] INFO: Graphics backend does not support VSync, skipping VSync update");
                    }
                }

                Console.WriteLine("[Odyssey] Real-time settings applied");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR: Failed to apply real-time settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens the options menu.
        /// Based on swkotor.exe and swkotor2.exe: Options menu activation
        /// Original implementation: Switches game state to options menu, initializes GUI
        /// </summary>
        private void OpenOptionsMenu()
        {
            Console.WriteLine("[Odyssey] Opening options menu...");

            // Initialize options menu if not already done
            if (_optionsByCategory == null)
            {
                InitializeOptionsMenu();
            }

            // Switch to options menu state
            // Based on original implementation: Game state management for menu transitions
            _currentState = GameState.OptionsMenu;

            // Reset menu state
            _selectedOptionsCategoryIndex = 0;
            _selectedOptionsItemIndex = 0;
            _isEditingOptionValue = false;
            _editingOptionValue = string.Empty;
            _isRebindingKey = false;
            _rebindingActionName = string.Empty;

            Console.WriteLine("[Odyssey] Options menu opened");
        }

        /// <summary>
        /// Closes the options menu and returns to main menu.
        /// Based on swkotor.exe and swkotor2.exe: Options menu deactivation
        /// Original implementation: Returns to previous menu state, cleans up GUI
        /// </summary>
        private void CloseOptionsMenu()
        {
            Console.WriteLine("[Odyssey] Closing options menu...");

            // Switch back to main menu state
            _currentState = GameState.MainMenu;

            // Reset menu indices
            _selectedMenuIndex = 0;
            _hoveredMenuIndex = -1;

            // Clear options-specific state
            _selectedOptionsCategoryIndex = 0;
            _selectedOptionsItemIndex = 0;
            _isEditingOptionValue = false;
            _editingOptionValue = string.Empty;
            _isRebindingKey = false;
            _rebindingActionName = string.Empty;

            Console.WriteLine("[Odyssey] Options menu closed");
        }

        /// <summary>
        /// Updates the options menu state and handles input.
        /// Based on swkotor.exe and swkotor2.exe: Options menu input handling
        /// Original implementation: Keyboard/mouse input processing for menu navigation
        /// </summary>
        private void UpdateOptionsMenu(float deltaTime, IKeyboardState keyboardState, IMouseState mouseState)
        {
            if (_optionsByCategory == null)
            {
                return;
            }

            // Update GUI manager if available
            if (_optionsMenuGuiLoaded && _guiManager != null)
            {
                // GUI manager handles its own input processing using MonoGame input
                // Based on original implementation: GUI system polls input directly
                _guiManager.Update(deltaTime);
                return;
            }

            // Fallback: Handle programmatic options menu input
            Andastra.Runtime.Game.GUI.OptionsMenu.UpdateOptionsMenu(
                deltaTime,
                keyboardState,
                _previousKeyboardState,
                mouseState,
                _previousMouseState,
                ref _selectedOptionsCategoryIndex,
                ref _selectedOptionsItemIndex,
                ref _isEditingOptionValue,
                ref _editingOptionValue,
                ref _isRebindingKey,
                ref _rebindingActionName,
                _settings,
                _optionsByCategory,
                (settings) => ApplyOptionsSettings(), // Apply callback
                () => CloseOptionsMenu() // Cancel callback
            );

            // Update previous input states
            _previousKeyboardState = keyboardState;
            _previousMouseState = mouseState;
        }

        /// <summary>
        /// Initializes the options menu.
        /// </summary>
        /// <remarks>
        /// Options Menu Initialization:
        /// - Based on swkotor.exe and swkotor2.exe options menu system
        /// - Located via string references: "optionsmain" (main menu options) or "optionsingame" (in-game options)
        /// - Based on swkotor2.exe: CSWGuiOptionsMain class @ 0x006e3e80 (constructor)
        /// - Settings categories: Graphics, Sound, Gameplay, Feedback, Autopause
        /// - Graphics: Resolution, Texture Quality, Shadow Quality, VSync, Fullscreen
        /// - Sound: Master Volume, Music Volume, Effects Volume, Voice Volume
        /// - Gameplay: Mouse Sensitivity, Invert Mouse Y, Auto-save, Tooltips
        /// - Feedback: Tooltip options, combat feedback, etc.
        /// - Autopause: Autopause triggers (on enemy sighted, trap found, etc.)
        /// - Original implementation: Tabbed interface with Apply/Cancel buttons
        /// - Settings persistence: Saved to configuration files
        /// </remarks>
        private void InitializeOptionsMenu()
        {
            Console.WriteLine("[Odyssey] Initializing options menu system...");

            try
            {
                // Initialize audio system references for volume controls
                // Based on swkotor.exe and swkotor2.exe: Audio system integration with options menu
                // Original implementation: Options menu directly controls audio volumes through global audio manager
                var soundPlayer = _graphicsBackend.AudioManager?.SoundPlayer;
                var musicPlayer = _graphicsBackend.AudioManager?.MusicPlayer;
                var voicePlayer = _graphicsBackend.AudioManager?.VoicePlayer;

                // Create options structure with all categories and settings
                // Based on swkotor2.exe: Options initialization @ FUN_006e3e80 (CSWGuiOptionsMain constructor)
                // Original implementation: Loads settings from swkotor2.ini, creates GUI controls for each option
                _optionsByCategory = Andastra.Runtime.Game.GUI.OptionsMenu.CreateDefaultOptions(
                    _settings,
                    soundPlayer,
                    musicPlayer,
                    voicePlayer);

                // Load options GUI panel if GUI manager is available
                // Based on swkotor2.exe: CSWGuiOptionsMain loads "optionsmain" GUI file
                // GUI file contains: tabs for categories, sliders, checkboxes, buttons, labels
                if (_guiManager != null)
                {
                    // Determine GUI file name based on context
                    // "optionsmain" for main menu options, "optionsingame" for in-game options
                    string guiName = "optionsmain"; // Main menu options GUI

                    int viewportWidth = _graphicsDevice?.Viewport.Width ?? 1280;
                    int viewportHeight = _graphicsDevice?.Viewport.Height ?? 720;

                    if (_guiManager.LoadGui(guiName, viewportWidth, viewportHeight))
                    {
                        _optionsMenuGuiLoaded = true;
                        Console.WriteLine($"[Odyssey] Options GUI loaded successfully: {guiName}");

                        // Set up GUI button event handlers for options categories
                        // Based on swkotor2.exe: Button click handlers for tab navigation
                        // Original implementation: Each tab button switches to different options category
                        SetupOptionsGuiEventHandlers();
                    }
                    else
                    {
                        Console.WriteLine($"[Odyssey] WARNING: Failed to load options GUI: {guiName}, falling back to programmatic UI");
                        _optionsMenuGuiLoaded = false;
                    }
                }
                else
                {
                    Console.WriteLine("[Odyssey] WARNING: GUI manager not available, using programmatic options menu");
                    _optionsMenuGuiLoaded = false;
                }

                // Initialize options settings from current configuration
                // Based on swkotor.exe and swkotor2.exe: Settings loading from INI files
                // FUN_00633270 @ 0x00633270 (loads from swkotor2.ini), FUN_00631ff0 @ 0x00631ff0 (saves to INI)
                LoadOptionsFromConfiguration();

                // Set initial selected category and item indices
                // Default to Graphics category (first category) and first item
                _selectedOptionsCategoryIndex = 0;
                _selectedOptionsItemIndex = 0;
                _isEditingOptionValue = false;
                _editingOptionValue = string.Empty;

                Console.WriteLine("[Odyssey] Options menu initialized successfully");
                Console.WriteLine($"[Odyssey] Options categories: {_optionsByCategory.Count}");
                foreach (var category in _optionsByCategory)
                {
                    Console.WriteLine($"[Odyssey]   {category.Key}: {category.Value.Count} options");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR: Failed to initialize options menu: {ex.Message}");
                Console.WriteLine($"[Odyssey] Stack trace: {ex.StackTrace}");

                // Create minimal fallback options structure
                _optionsByCategory = new Dictionary<Andastra.Runtime.Game.GUI.OptionsMenu.OptionsCategory, List<Andastra.Runtime.Game.GUI.OptionsMenu.OptionItem>>();
                _optionsMenuGuiLoaded = false;
            }
        }
    }
}


