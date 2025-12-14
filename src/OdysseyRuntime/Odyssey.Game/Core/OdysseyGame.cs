using System;
using System.IO;
using System.Linq;
using StrideEngine = Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using Stride.Rendering.UI;
using Stride.UI;
using Stride.UI.Panels;
using Stride.UI.Controls;
using Stride.UI.Events;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Interfaces.Components;
using Odyssey.Core.Enums;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.VM;
using Odyssey.Kotor.Game;
using Odyssey.Kotor.Systems;
using Odyssey.Kotor.Dialogue;
using Odyssey.Stride.Camera;
using Odyssey.Stride.Scene;
using Odyssey.Stride.UI;
using Odyssey.Content.Interfaces;
using Odyssey.Stride.GUI;
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;
using Myra.Graphics2D.UI;

namespace Odyssey.Game.Core
{
    public class OdysseyGame : StrideEngine.Game
    {
        // #region agent log
        private static readonly string DebugLogPath = @"g:\GitHub\HoloPatcher.NET\.cursor\debug.log";
        private static void DebugLog(string hypothesisId, string location, string message, object data = null)
        {
            try
            {
                string json = "{\"timestamp\":" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() +
                    ",\"sessionId\":\"debug-session\",\"hypothesisId\":\"" + hypothesisId +
                    "\",\"location\":\"" + location.Replace("\\", "\\\\") +
                    "\",\"message\":\"" + message.Replace("\"", "\\\"").Replace("\n", "\\n") +
                    "\",\"data\":" + (data != null ? "\"" + data.ToString().Replace("\"", "\\\"").Replace("\n", "\\n") + "\"" : "null") + "}";
                File.AppendAllText(DebugLogPath, json + "\n");
            }
            catch { }
        }
        // #endregion

        private readonly GameSettings _settings;
        private GameSession _session;
        private World _world;
        private ScriptGlobals _globals;
        private K1EngineApi _engineApi;
        private NcsVm _vm;

        // Game state
        private GameState _currentState = GameState.MainMenu;

        // Scene rendering
        private SceneBuilder _sceneBuilder;
        private StrideEngine.Entity _cameraEntity;
        private CameraComponent _cameraComponent;

        [CanBeNull]
        private readonly ChaseCamera _chaseCamera = null;
        private Odyssey.Kotor.Input.PlayerController _playerController;
        private TriggerSystem _triggerSystem;
        private HeartbeatSystem _heartbeatSystem;
        private ModuleTransitionSystem _transitionSystem;

        // UI
        private MainMenu _mainMenu;
        private BasicHUD _hud;
        private PauseMenu _pauseMenu;
        private LoadingScreen _loadingScreen;
        private DialoguePanel _dialoguePanel;
        private UIComponent _uiComponent;
        private SpriteFont _font;
        private bool _uiAvailable;

        // KOTOR GUI System
        private KotorGuiManager _kotorGuiManager;

        // Myra UI menu renderer (text-based buttons)
        private MyraMenuRenderer _fallbackMenuRenderer;

        private bool _showDebugInfo = true; // Default to debug mode for demo
        private bool _isPaused = false;
        private bool _inDialogue = false;

        // Debug text overlay
        private readonly TextBlock _debugTextBlock;

        public OdysseyGame(GameSettings settings)
        {
            _settings = settings;
        }

        protected override void Initialize()
        {
            // #region agent log
            DebugLog("A", "OdysseyGame.Initialize:start", "Initialize() starting");
            // #endregion

            base.Initialize();

            // #region agent log
            DebugLog("C", "OdysseyGame.Initialize:compositor_check", "Checking GraphicsCompositor", SceneSystem?.GraphicsCompositor != null ? "compositor_exists" : "compositor_null");
            // #endregion

            // Set window title
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.GameWindow.html
            // Window.Title property sets the window title bar text
            // Method signature: string Title { get; set; }
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/window-management.html
            Window.Title = "Odyssey Engine - " + (_settings.Game == KotorGame.K1 ? "Knights of the Old Republic" : "The Sith Lords");

            _world = new World();
            _globals = new ScriptGlobals();
            _engineApi = new K1EngineApi();
            _vm = new NcsVm();

            _session = new GameSession(_settings, _world, _vm, _globals);

            InitializeSystems();
            InitializeCamera();

            // #region agent log
            DebugLog("A", "OdysseyGame.Initialize:end", "Initialize() complete");
            // #endregion

            Console.WriteLine("[Odyssey] Core systems initialized");
        }

        private void InitializeCamera()
        {
            // #region agent log
            DebugLog("B", "OdysseyGame.InitializeCamera:start", "Creating camera entity");
            // #endregion

            // Create main camera entity
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity(string) constructor creates a new entity with the specified name
            // Source: https://doc.stride3d.net/latest/en/manual/entities/index.html
            _cameraEntity = new StrideEngine.Entity("MainCamera");
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.CameraComponent.html
            // CameraComponent defines camera properties for rendering
            // NearClipPlane/FarClipPlane set the clipping planes, UseCustomAspectRatio controls aspect ratio calculation
            // Slot property assigns camera to a GraphicsCompositor camera slot
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/cameras/index.html
            _cameraComponent = new CameraComponent
            {
                NearClipPlane = 0.1f,
                FarClipPlane = 1000f,
                UseCustomAspectRatio = false,
                Slot = new SceneCameraSlotId() // Assign to the default camera slot - will be updated in CreateDefaultGraphicsCompositor
            };
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity.Add(EntityComponent) adds a component to the entity
            // Method signature: void Add<T>(T component) where T : EntityComponent
            // Source: https://doc.stride3d.net/latest/en/manual/entities/index.html
            _cameraEntity.Add(_cameraComponent);

            // Position camera at origin - UI is rendered in screen space, camera position doesn't matter for UI
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
            // TransformComponent.Position sets the entity's world position
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
            // Vector3(float x, float y, float z) constructor creates a 3D position vector
            // Method signature: Vector3(float x, float y, float z)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
            // Quaternion.Identity is a static property representing no rotation (0, 0, 0, 1)
            // TransformComponent.Rotation sets the entity's rotation
            // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms/index.html
            _cameraEntity.Transform.Position = new Vector3(0, 0, 10);
            _cameraEntity.Transform.Rotation = Quaternion.Identity;

            // #region agent log
            DebugLog("B", "OdysseyGame.InitializeCamera:end", "Camera entity created", "slot=" + _cameraComponent.Slot.Id);
            // #endregion

            Console.WriteLine("[Odyssey] Camera entity created (will be added to scene in BeginRun)");
        }

        private void SetGameState(GameState newState)
        {
            GameState oldState = _currentState;
            _currentState = newState;

            Console.WriteLine($"[Odyssey] Game state changed: {oldState} -> {newState}");

            // Hide all UI elements first
            if (_mainMenu != null)
            {
                _mainMenu.IsVisible = false;
            }

            if (_hud != null)
            {
                _hud.IsVisible = false;
            }

            if (_pauseMenu != null)
            {
                _pauseMenu.IsVisible = false;
            }

            if (_loadingScreen != null)
            {
                _loadingScreen.IsVisible = false;
            }

            if (_dialoguePanel != null)
            {
                _dialoguePanel.IsVisible = false;
            }

            // Handle camera/scene visibility based on state
            UpdateCameraVisibility(newState);

            // Show UI appropriate for the new state
            switch (newState)
            {
                case GameState.MainMenu:
                    if (_mainMenu != null)
                    {
                        _mainMenu.IsVisible = true;
                    }

                    // Show fallback menu renderer if available
                    if (_fallbackMenuRenderer != null)
                    {
                        _fallbackMenuRenderer.SetVisible(true);
                    }

                    break;

                case GameState.Loading:
                    if (_loadingScreen != null)
                    {
                        _loadingScreen.Show("Loading...");
                    }

                    break;

                case GameState.InGame:
                    if (_hud != null)
                    {
                        _hud.IsVisible = true;
                    }

                    // Hide fallback menu when in game
                    if (_fallbackMenuRenderer != null)
                    {
                        _fallbackMenuRenderer.SetVisible(false);
                    }

                    break;

                case GameState.Paused:
                    if (_pauseMenu != null)
                    {
                        _pauseMenu.IsVisible = true;
                    }

                    break;
            }
        }

        private void UpdateCameraVisibility(GameState state)
        {
            try
            {
                SceneSystem sceneSystem = Services.GetService<SceneSystem>();
                if (sceneSystem != null && sceneSystem.SceneInstance != null && _cameraEntity != null)
                {
                    // CRITICAL FIX: Camera must ALWAYS be in scene for rendering to work
                    // Removing the camera causes the purple screen because Stride needs an active camera
                    // to render anything, including UI. Keep camera in scene for all states.
                    // Check if camera is already in scene
                    // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.SceneInstance.html
                    // SceneInstance.RootScene.Entities collection contains all entities in the scene
                    // Contains(Entity) checks if entity is in the collection, Add(Entity) adds entity to scene
                    // Source: https://doc.stride3d.net/latest/en/manual/entities/scenes/index.html
                    if (!sceneSystem.SceneInstance.RootScene.Entities.Contains(_cameraEntity))
                    {
                        sceneSystem.SceneInstance.RootScene.Entities.Add(_cameraEntity);
                        Console.WriteLine("[Odyssey] Camera added to scene (required for rendering)");
                    }

                    // Adjust camera position/rotation based on state if needed
                    // For MainMenu, we can keep a simple default camera position
                    if (state == GameState.MainMenu)
                    {
                    // Ensure camera is positioned for menu viewing
                    // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                    // Vector3(float x, float y, float z) - same constructor as above
                    _cameraEntity.Transform.Position = new Vector3(0, 0, 0);
                    // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
                    // Quaternion.Identity static property - same as documented above
                    _cameraEntity.Transform.Rotation = Quaternion.Identity;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] WARNING: Failed to update camera visibility: {ex.Message}");
            }
        }

        private void OnStartGame(object sender, GameStartEventArgs e)
        {
            Console.WriteLine($"[Odyssey] Starting game with install path: {e.InstallPath}, module: {e.ModuleName}");

            // Update settings with user selections
            var updatedSettings = new GameSettings
            {
                Game = _settings.Game,
                GamePath = e.InstallPath,
                StartModule = e.ModuleName
            };

            // Create new session with updated settings
            _session = new GameSession(updatedSettings, _world, _vm, _globals);

            // Transition to loading state
            SetGameState(GameState.Loading);

            try
            {
                // Start the game session
                _session.StartNewGame();

                // If successful, transition to in-game
                SetGameState(GameState.InGame);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Odyssey] Failed to start game: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);

                // Show error and return to main menu
                if (_mainMenu != null)
                {
                    _mainMenu.SetStatusText($"Error starting game: {ex.Message}");
                }
                SetGameState(GameState.MainMenu);
            }
        }

        private void InitializeSystems()
        {
            _triggerSystem = new TriggerSystem(_world, FireScriptEvent);
            _heartbeatSystem = new HeartbeatSystem(_world, FireScriptEvent);
            _transitionSystem = new ModuleTransitionSystem(
                async (moduleName) =>
                {
                    try
                    {
                        _session.LoadModule(moduleName);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                },
                PositionPlayerAtWaypoint
            );

            _transitionSystem.OnTransitionStart += OnTransitionStart;
            _transitionSystem.OnTransitionComplete += OnTransitionComplete;
            _transitionSystem.OnTransitionFailed += OnTransitionFailed;

            _session.OnModuleLoaded += (sender, e) => OnModuleLoaded(e);
        }

        private void OnKotorGuiButtonClicked(object sender, GuiButtonClickedEventArgs e)
        {
            Console.WriteLine($"[Odyssey] KOTOR GUI button clicked: Tag='{e.ButtonTag}', ID={e.ButtonId}");

            // Handle main menu button clicks
            // KOTOR main menu typical button tags: BTN_LOADGAME, BTN_NEWGAME, BTN_MOVIES, BTN_OPTIONS, BTN_EXIT
            // Button IDs vary, so we primarily use tags
            string buttonTag = e.ButtonTag?.ToLowerInvariant() ?? string.Empty;

            if (buttonTag.Contains("newgame") || buttonTag.Contains("new") || buttonTag.Contains("loadgame") || buttonTag.Contains("load"))
            {
                // Start the game - load first level
                Console.WriteLine("[Odyssey] Starting new game from KOTOR GUI");

                string gamePath = _settings.GamePath;
                if (string.IsNullOrEmpty(gamePath))
                {
                    gamePath = GamePathDetector.DetectKotorPath(_settings.Game);
                }

                if (!string.IsNullOrEmpty(gamePath))
                {
                    OnStartGame(this, new GameStartEventArgs(gamePath, "end_m01aa"));
                }
                else
                {
                    Console.WriteLine("[Odyssey] ERROR: No game path available to start game!");
                }
            }
            else if (buttonTag.Contains("exit") || buttonTag.Contains("quit"))
            {
                // Exit the game
                Console.WriteLine("[Odyssey] Exit requested from KOTOR GUI");
                Exit();
            }
            else
            {
                // For any other button, just start the game for testing
                // This ensures we can test with any clickable button
                Console.WriteLine($"[Odyssey] Unrecognized button '{e.ButtonTag}', starting game anyway for testing");

                string gamePath = _settings.GamePath;
                if (string.IsNullOrEmpty(gamePath))
                {
                    gamePath = GamePathDetector.DetectKotorPath(_settings.Game);
                }

                if (!string.IsNullOrEmpty(gamePath))
                {
                    OnStartGame(this, new GameStartEventArgs(gamePath, "end_m01aa"));
                }
            }
        }

        private void InitializeUI()
        {
            // #region agent log
            DebugLog("D", "OdysseyGame.InitializeUI:start", "InitializeUI starting");
            // #endregion

            // Create UI entity
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity(string) constructor creates a new entity
            var uiEntity = new StrideEngine.Entity("UI");
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
            // UIComponent enables UI rendering on an entity
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            _uiComponent = new UIComponent();

            // CRITICAL FIX: Set resolution for UI rendering
            // Without this, UI elements won't render at all
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.UIComponent.html
            // Resolution property sets the UI resolution (width, height, depth)
            // ResolutionStretch controls how UI scales to match screen size
            // IsBillboard controls if UI faces camera, IsFullScreen makes UI fill entire screen
            // RenderGroup sets which render group the UI belongs to
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
            // Vector3(float x, float y, float z) constructor creates UI resolution vector
            // Method signature: Vector3(float x, float y, float z)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.GameWindow.html
            // Window.ClientBounds property gets the client area bounds (width and height in pixels)
            // ClientBounds.Width/Height get dimensions in pixels
            // Source: https://doc.stride3d.net/latest/en/manual/user-interface/index.html
            _uiComponent.Resolution = new Vector3(Window.ClientBounds.Width, Window.ClientBounds.Height, 1000);
            _uiComponent.ResolutionStretch = ResolutionStretch.FixedWidthAdaptableHeight;
            _uiComponent.IsBillboard = false;
            _uiComponent.IsFullScreen = true;
            _uiComponent.RenderGroup = RenderGroup.Group0;

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Entity.html
            // Entity.Add(EntityComponent) adds component to entity
            uiEntity.Add(_uiComponent);

            // Add UI entity to scene FIRST
            try
            {
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Game.html
                // Game.Services.GetService<T>() retrieves a service from the service registry
                // SceneSystem manages scene rendering and entity management
                // Source: https://doc.stride3d.net/latest/en/manual/entities/scenes/index.html
                SceneSystem sceneSystem = Services.GetService<SceneSystem>();
                if (sceneSystem != null && sceneSystem.SceneInstance != null)
                {
                    // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.SceneInstance.html
                    // SceneInstance.RootScene.Entities collection contains all entities in the scene
                    // Add(Entity) adds an entity to the scene
                    // Source: https://doc.stride3d.net/latest/en/manual/entities/scenes/index.html
                    sceneSystem.SceneInstance.RootScene.Entities.Add(uiEntity);
                    // #region agent log
                    DebugLog("D", "OdysseyGame.InitializeUI:ui_added", "UI entity added to scene");
                    // #endregion
                    Console.WriteLine("[Odyssey] UI entity added to scene");
                }
                else
                {
                    // #region agent log
                    DebugLog("D", "OdysseyGame.InitializeUI:scene_null", "SceneSystem or SceneInstance null", sceneSystem == null ? "sceneSystem=null" : "sceneInstance=null");
                    // #endregion
                }
            }
            catch (Exception ex)
            {
                // #region agent log
                DebugLog("D", "OdysseyGame.InitializeUI:ui_error", "Failed to add UI entity", ex.Message);
                // #endregion
                Console.WriteLine("[Odyssey] WARNING: Failed to add UI entity to scene: " + ex.Message);
            }

            // Initialize KOTOR GUI System
            try
            {
                // Determine game path
                string gamePath = _settings.GamePath;
                if (string.IsNullOrEmpty(gamePath))
                {
                    gamePath = GamePathDetector.DetectKotorPath(_settings.Game);
                    Console.WriteLine($"[Odyssey] Auto-detected game path: {gamePath}");
                }
                else
                {
                    Console.WriteLine($"[Odyssey] Using configured game path: {gamePath}");
                }

                // #region agent log
                DebugLog("D", "OdysseyGame.InitializeUI:game_path", "Game path", gamePath ?? "null");
                // #endregion

                if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                {
                    Console.WriteLine($"[Odyssey] WARNING: Game path not found or invalid: {gamePath}");
                    Console.WriteLine("[Odyssey] Using fallback UI (no game files available)");
                    CreateFallbackMainMenu();
                    _uiAvailable = true;
                    return;
                }

                _kotorGuiManager = new KotorGuiManager(_uiComponent, GraphicsDevice, gamePath);
                _kotorGuiManager.OnButtonClicked += OnKotorGuiButtonClicked;

                // #region agent log
                DebugLog("D", "OdysseyGame.InitializeUI:gui_manager_created", "KotorGuiManager created");
                // #endregion

                // Load the main menu GUI from KOTOR game files
                // Standard KOTOR main menu GUIs: mainmenu8x6_p (K1), mainmenu16x12_p (K1 widescreen)
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.GameWindow.html
                // Window.ClientBounds.Width/Height get the client area dimensions in pixels
                bool guiLoaded = _kotorGuiManager.LoadGui("mainmenu8x6_p",
                    Window.ClientBounds.Width,
                    Window.ClientBounds.Height);

                if (guiLoaded)
                {
                    Console.WriteLine("[Odyssey] KOTOR main menu GUI loaded successfully");
                    _uiAvailable = true;

                    // #region agent log
                    DebugLog("D", "OdysseyGame.InitializeUI:kotor_gui_loaded", "KOTOR GUI loaded successfully");
                    // #endregion
                }
                else
                {
                    Console.WriteLine("[Odyssey] WARNING: Failed to load KOTOR main menu GUI, using fallback");
                    // #region agent log
                    DebugLog("D", "OdysseyGame.InitializeUI:kotor_gui_failed", "KOTOR GUI loading failed, using fallback");
                    // #endregion
                    CreateFallbackMainMenu();
                    _uiAvailable = true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Odyssey] ERROR initializing KOTOR GUI system: {ex.Message}");
                Console.WriteLine($"[Odyssey] Stack trace: {ex.StackTrace}");

                // #region agent log
                DebugLog("D", "OdysseyGame.InitializeUI:kotor_gui_error", "KOTOR GUI error", ex.Message);
                // #endregion

                // Fall back to simple UI
                CreateFallbackMainMenu();
                _uiAvailable = true;
            }

            // #region agent log
            DebugLog("D", "OdysseyGame.InitializeUI:end", "InitializeUI complete", "uiAvailable=" + _uiAvailable + " page=" + (_uiComponent.Page != null));
            // #endregion
        }

        /// <summary>
        /// Creates a fully functional visual fallback main menu without text dependency.
        /// Uses SpriteBatch-based custom renderer for reliable rendering.
        /// This is GUARANTEED to work as it doesn't depend on font assets or UIComponent.
        /// Strategy based on Stride's official SpriteBatch and custom renderer documentation.
        /// </summary>
        private void CreateFallbackMainMenu()
        {
            Console.WriteLine("[Odyssey] Using SpriteBatch-based fallback menu renderer");

            // The FallbackMenuRenderer is already created in SetupGraphicsCompositor
            // Just make sure it's visible
            if (_fallbackMenuRenderer != null)
            {
                _fallbackMenuRenderer.SetVisible(true);
                Console.WriteLine("[Odyssey] Fallback menu renderer enabled");
                Console.WriteLine("[Odyssey] Use UP/DOWN arrows to navigate, ENTER to select");
            }
            else
            {
                Console.WriteLine("[Odyssey] ERROR: FallbackMenuRenderer not initialized!");
            }

            // OLD UIComponent-based code removed - using SpriteBatch renderer instead
            return;

            /*
            // OLD CODE BELOW - KEPT FOR REFERENCE BUT NOT USED
            // Create root canvas - full screen with a clear background to see if UI renders at all
            var canvas = new Canvas
            {
                BackgroundColor = new Color(20, 30, 60, 255), // Visible dark blue
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Main panel - centered visual menu - much larger for visibility
            var mainPanel = new Grid
            {
                Width = 600,
                Height = 400,
                BackgroundColor = new Color(40, 50, 80, 255), // Visible background
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Simple row layout - larger spacing
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 80)); // Title/header
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 20));  // Spacer
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 80)); // Main button
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 20));  // Spacer
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 80)); // Options button
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 20));  // Spacer
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 80)); // Exit button
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));    // Bottom space

            // HEADER PANEL - Bright golden bar (represents title) - VERY visible
            var headerPanel = new Border
            {
                BackgroundColor = new Color(255, 200, 50, 255), // Bright gold
                BorderColor = new Color(255, 255, 255, 255), // White border for maximum visibility
                BorderThickness = new Thickness(4, 4, 4, 4),
                Margin = new Thickness(40, 10, 40, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            headerPanel.SetGridRow(0);
            mainPanel.Children.Add(headerPanel);

            // Determine if game path is available
            string gamePath = _settings.GamePath;
            if (string.IsNullOrEmpty(gamePath))
            {
                gamePath = GamePathDetector.DetectKotorPath(_settings.Game);
            }
            bool gameAvailable = !string.IsNullOrEmpty(gamePath) && Directory.Exists(gamePath);

            // START GAME BUTTON - Bright green means "go", highly visible
            var startButton = new Button
            {
                BackgroundColor = gameAvailable ? new Color(50, 200, 50, 255) : new Color(80, 80, 80, 255),
                Margin = new Thickness(50, 5, 50, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsEnabled = gameAvailable
            };

            // Inner colored box to make it clear it's interactive - BRIGHT for visibility
            var startInner = new Border
            {
                BackgroundColor = gameAvailable ? new Color(100, 255, 100, 255) : new Color(120, 120, 120, 255),
                Margin = new Thickness(12, 12, 12, 12),
                BorderColor = new Color(255, 255, 255, 255),
                BorderThickness = new Thickness(4, 4, 4, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            startButton.Content = startInner;
            startButton.Click += OnFallbackStartClicked;
            startButton.SetGridRow(2);
            mainPanel.Children.Add(startButton);

            // OPTIONS BUTTON - Bright blue panel (disabled for now but visible)
            var optionsButton = new Button
            {
                BackgroundColor = new Color(60, 100, 200, 255),
                Margin = new Thickness(50, 5, 50, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsEnabled = false // Disabled for minimal UI
            };

            var optionsInner = new Border
            {
                BackgroundColor = new Color(100, 150, 255, 255),
                Margin = new Thickness(12, 12, 12, 12),
                BorderColor = new Color(150, 150, 150, 255),
                BorderThickness = new Thickness(4, 4, 4, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            optionsButton.Content = optionsInner;
            optionsButton.SetGridRow(4);
            mainPanel.Children.Add(optionsButton);

            // EXIT BUTTON - Bright red means "stop/exit", highly visible
            var exitButton = new Button
            {
                BackgroundColor = new Color(200, 50, 50, 255),
                Margin = new Thickness(50, 5, 50, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var exitInner = new Border
            {
                BackgroundColor = new Color(255, 100, 100, 255),
                Margin = new Thickness(12, 12, 12, 12),
                BorderColor = new Color(255, 255, 255, 255),
                BorderThickness = new Thickness(4, 4, 4, 4),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            exitButton.Content = exitInner;
            exitButton.Click += (s, e) => Exit();
            exitButton.SetGridRow(6);
            mainPanel.Children.Add(exitButton);

            // Outer frame border - bright white border for maximum visibility
            var outerFrame = new Border
            {
                BorderColor = new Color(255, 255, 255, 255), // Bright white border
                BorderThickness = new Thickness(6, 6, 6, 6),
                Content = mainPanel,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            canvas.Children.Add(outerFrame);

            // Set the page - CRITICAL for rendering
            var page = new UIPage { RootElement = canvas };
            _uiComponent.Page = page;

            Console.WriteLine("[Odyssey] ====================================");
            Console.WriteLine("[Odyssey] OLD UIComponent CODE - NOT USED");
            Console.WriteLine("[Odyssey] ====================================");
            */
        }

        /// <summary>
        /// Handles menu item selection from the fallback menu renderer.
        /// </summary>
        private void OnFallbackMenuItemSelected(object sender, int menuIndex)
        {
            Console.WriteLine($"[Odyssey] Fallback menu item {menuIndex} selected");

            switch (menuIndex)
            {
                case 0: // Start Game
                    OnFallbackStartClicked(null, null);
                    break;
                case 1: // Options
                    Console.WriteLine("[Odyssey] Options menu not implemented");
                    break;
                case 2: // Exit
                    Exit();
                    break;
            }
        }

        private void OnFallbackStartClicked(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("[Odyssey] Start button clicked (fallback menu)");

            // Use detected game path
            string gamePath = _settings.GamePath;
            if (string.IsNullOrEmpty(gamePath))
            {
                gamePath = GamePathDetector.DetectKotorPath(_settings.Game);
            }

            if (!string.IsNullOrEmpty(gamePath))
            {
                OnStartGame(this, new GameStartEventArgs(gamePath, "end_m01aa"));
            }
            else
            {
                Console.WriteLine("[Odyssey] ERROR: No game path detected!");
            }
        }

        private void InitializeSceneBuilder()
        {
            if (_sceneBuilder != null)
            {
                return;
            }

            // Create resource provider wrapper
            var resourceProvider = new GameResourceProvider(_session, _settings);
            _sceneBuilder = new SceneBuilder(GraphicsDevice, resourceProvider);

            // Add scene root to Stride scene
            try
            {
                var sceneSystem = Services.GetService<SceneSystem>();
                if (sceneSystem != null && sceneSystem.SceneInstance != null)
                {
                    sceneSystem.SceneInstance.RootScene.Entities.Add(_sceneBuilder.RootEntity);
                    Console.WriteLine("[Odyssey] SceneBuilder initialized and added to scene");
                }
                else
                {
                    Console.WriteLine("[Odyssey] WARNING: SceneSystem not available for SceneBuilder");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] WARNING: Failed to add SceneBuilder root to scene: " + ex.Message);
            }
        }

        private void FireScriptEvent(IEntity entity, ScriptEvent scriptEvent, IEntity triggeredBy)
        {
            var scriptHooks = entity.GetComponent<IScriptHooksComponent>();
            if (scriptHooks == null)
            {
                return;
            }

            string scriptResRef = scriptHooks.GetScript(scriptEvent);
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return;
            }

            try
            {
                byte[] scriptData = _session.LoadScript(scriptResRef);
                if (scriptData != null && scriptData.Length > 0)
                {
                    _vm.Execute(scriptData, null);
                }
            }
            catch { }
        }

        private void PositionPlayerAtWaypoint(IEntity player, string waypointTag)
        {
            if (player == null || string.IsNullOrEmpty(waypointTag))
            {
                return;
            }

            var waypoint = _world.GetEntityByTag(waypointTag);
            if (waypoint == null)
            {
                return;
            }

            var waypointTransform = waypoint.GetComponent<ITransformComponent>();
            var playerTransform = player.GetComponent<ITransformComponent>();

            if (waypointTransform != null && playerTransform != null)
            {
                playerTransform.Position = waypointTransform.Position;
                playerTransform.Facing = waypointTransform.Facing;
            }
        }

        protected override void BeginRun()
        {
            // #region agent log
            DebugLog("A", "OdysseyGame.BeginRun:start", "BeginRun() starting");
            // #endregion

            // Call base BeginRun to initialize Stride game systems
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.Game.html
            // Game.BeginRun() is called once before the game loop starts
            // Method signature: protected virtual void BeginRun()
            // Source: https://doc.stride3d.net/latest/en/manual/game-loop/index.html
            base.BeginRun();

            Console.WriteLine("[Odyssey] BeginRun - Setting up scene and UI...");

            // Get or create the scene system and instance
            var sceneSystem = Services.GetService<SceneSystem>();
            // #region agent log
            DebugLog("A", "OdysseyGame.BeginRun:scene_system", "SceneSystem check", sceneSystem != null ? "exists" : "null");
            // #endregion

            if (sceneSystem == null)
            {
                Console.WriteLine("[Odyssey] ERROR: SceneSystem service not found!");
                return;
            }

            // #region agent log
            DebugLog("E", "OdysseyGame.BeginRun:compositor_before", "GraphicsCompositor before setup", sceneSystem.GraphicsCompositor != null ? "exists_will_replace" : "null_will_create");
            // #endregion

            // CRITICAL FIX: ALWAYS set up our compositor - Stride's default doesn't work properly
            // The previous code checked for null but Stride creates a default that doesn't clear correctly
            SetupGraphicsCompositor(sceneSystem);

            // #region agent log
            DebugLog("E", "OdysseyGame.BeginRun:compositor_after", "GraphicsCompositor after setup", sceneSystem.GraphicsCompositor != null ? "exists" : "null");
            // #endregion

            // Create SceneInstance if it doesn't exist
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.Scene.html
            // Scene() constructor creates a new scene container for entities
            // Method signature: Scene()
            // Source: https://doc.stride3d.net/latest/en/manual/entities/scenes/index.html
            if (sceneSystem.SceneInstance == null)
            {
                var rootScene = new StrideEngine.Scene();
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.SceneInstance.html
                // SceneInstance(Services, Scene) constructor creates a scene instance for rendering
                // Services parameter provides access to game services, Scene is the root scene
                // Method signature: SceneInstance(IServiceRegistry services, Scene scene)
                // Source: https://doc.stride3d.net/latest/en/manual/entities/scenes/index.html
                sceneSystem.SceneInstance = new SceneInstance(Services, rootScene);
                Console.WriteLine("[Odyssey] Created root SceneInstance for rendering");
            }

            // #region agent log
            DebugLog("A", "OdysseyGame.BeginRun:scene_instance", "SceneInstance check", sceneSystem.SceneInstance != null ? "exists" : "null");
            // #endregion

            // Add camera to scene now that SceneSystem is available
            if (_cameraEntity != null && sceneSystem.SceneInstance != null)
            {
                try
                {
                    if (!sceneSystem.SceneInstance.RootScene.Entities.Contains(_cameraEntity))
                    {
                        sceneSystem.SceneInstance.RootScene.Entities.Add(_cameraEntity);
                        Console.WriteLine("[Odyssey] Camera added to scene at " + _cameraEntity.Transform.Position);
                    }

                    // #region agent log
                    DebugLog("B", "OdysseyGame.BeginRun:camera_added", "Camera entity added to scene", _cameraEntity.Transform.Position.ToString());
                    // #endregion
                }
                catch (Exception ex)
                {
                    // #region agent log
                    DebugLog("B", "OdysseyGame.BeginRun:camera_error", "Camera add failed", ex.Message);
                    // #endregion
                    Console.WriteLine("[Odyssey] WARNING: Failed to add camera to scene: " + ex.Message);
                }
            }

            InitializeUI();
            InitializeSceneBuilder();

            // Start in main menu state
            SetGameState(GameState.MainMenu);

            // #region agent log
            DebugLog("A", "OdysseyGame.BeginRun:end", "BeginRun() complete", "state=" + _currentState);
            // #endregion

            Console.WriteLine("[Odyssey] BeginRun complete - Game ready");
        }

        /// <summary>
        /// Sets up a basic GraphicsCompositor for rendering.
        /// This is REQUIRED for anything to appear on screen in Stride.
        /// Based on the working version that successfully displayed the dark blue background.
        /// </summary>
        private void SetupGraphicsCompositor(SceneSystem sceneSystem)
        {
            // #region agent log
            DebugLog("E", "SetupGraphicsCompositor:start", "Setting up compositor");
            // #endregion

            try
            {
            // Create a basic graphics compositor
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.GraphicsCompositor.html
            // GraphicsCompositor() constructor creates a new graphics compositor
            // GraphicsCompositor defines the rendering pipeline and camera slots
            // Method signature: GraphicsCompositor()
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/index.html
            var compositor = new GraphicsCompositor();

            // Create a camera slot
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneCameraSlot.html
            // SceneCameraSlot() constructor creates a new camera slot
            // SceneCameraSlot defines a camera slot in the compositor
            // Method signature: SceneCameraSlot()
            // GraphicsCompositor.Cameras collection contains all camera slots
            // Add(SceneCameraSlot) adds a camera slot to the compositor
            // Method signature: void Add(SceneCameraSlot slot)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/index.html
            var cameraSlot = new SceneCameraSlot();
            compositor.Cameras.Add(cameraSlot);

                // #region agent log
                DebugLog("E", "SetupGraphicsCompositor:camera_slot", "Camera slot created", cameraSlot.Id.ToString());
                // #endregion

                // IMPORTANT: Stride currently requires renderers to have a camera (or be a child of a renderer that has a camera).
                // This applies even to renderers that don't necessarily use cameras.
                // Based on Stride documentation: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/scene-renderers.html
                //
                // Our previous compositor created a camera slot but never used SceneCameraRenderer. This prevented the
                // UI system and custom renderers from being executed correctly (symptom: only clear color visible).

                // Create the forward renderer (Stride standard renderer; also where UI is rendered by default)
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.ForwardRenderer.html
                // ForwardRenderer() constructor creates a new forward renderer
                // ForwardRenderer should use the current RenderView and the current camera
                // Method signature: ForwardRenderer()
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/scene-renderers.html
                var forwardRenderer = new ForwardRenderer
                {
                    // Clear renderer used by ForwardRenderer
                    // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.ClearRenderer.html
                    // ClearRenderer() constructor creates a new clear renderer
                    // Method signature: ClearRenderer()
                    // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/scene-renderers.html
                    Clear = new ClearRenderer
                    {
                        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color4.html
                        // Color4(float r, float g, float b, float a) constructor creates a color from RGBA float values (0-1)
                        // Method signature: Color4(float r, float g, float b, float a)
                        // Source: https://doc.stride3d.net/latest/en/manual/graphics/colors.html
                        Color = new Color4(0.04f, 0.04f, 0.12f, 1f), // Dark blue
                        ClearFlags = ClearRendererFlags.ColorAndDepth
                    }
                };

                // #region agent log
                DebugLog("E", "SetupGraphicsCompositor:clear_added", "ClearRenderer added", forwardRenderer.Clear.Color.ToString());
                // #endregion

                // Combine forward rendering + our fallback SpriteBatch overlay.
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneRendererCollection.html
                // SceneRendererCollection() constructor creates a new scene renderer collection
                // SceneRendererCollection allows combining multiple renderers
                // Method signature: SceneRendererCollection()
                // Children.Add(ISceneRenderer) adds a child renderer to the collection
                // Method signature: void Add(ISceneRenderer renderer)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/scene-renderers.html
                var sceneRenderer = new SceneRendererCollection();
                sceneRenderer.Children.Add(forwardRenderer);

                // Create and add Myra UI menu renderer (text-based buttons, renders on top)
                _fallbackMenuRenderer = new MyraMenuRenderer();
                _fallbackMenuRenderer.MenuItemSelected += OnFallbackMenuItemSelected;
                sceneRenderer.Children.Add(_fallbackMenuRenderer);

                // Bind the render pipeline to our camera slot
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneCameraRenderer.html
                // SceneCameraRenderer() constructor creates a new camera renderer
                // Camera property type: SceneCameraSlot - assigns camera slot to renderer
                // Child property type: ISceneRenderer - sets the child renderer to execute
                // Method signature: SceneCameraRenderer()
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/scene-renderers.html
                var cameraRenderer = new SceneCameraRenderer
                {
                    Camera = cameraSlot,
                    Child = sceneRenderer
                };

                // Create game entry point
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.GraphicsCompositor.html
                // GraphicsCompositor.Game property sets the root renderer for game rendering
                // Method signature: ISceneRenderer Game { get; set; }
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/index.html
                compositor.Game = cameraRenderer;

                // Update our camera component to use this slot
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.CameraComponent.html
                // CameraComponent.Slot property assigns camera to a specific camera slot
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneCameraSlotId.html
                // SceneCameraSlotId(Guid) constructor wraps the slot identifier
                // Method signature: SceneCameraSlotId(Guid id)
                // SceneCameraSlot.Id property gets the unique identifier for the slot
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/cameras/index.html
                if (_cameraComponent != null)
                {
                    _cameraComponent.Slot = new SceneCameraSlotId(cameraSlot.Id);
                    // #region agent log
                    DebugLog("E", "SetupGraphicsCompositor:camera_assigned", "Camera slot assigned", cameraSlot.Id.ToString());
                    // #endregion
                }

                // Set the compositor - ALWAYS replace the default one
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.SceneSystem.html
                // SceneSystem.GraphicsCompositor property sets the active graphics compositor
                // Method signature: GraphicsCompositor GraphicsCompositor { get; set; }
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/index.html
                sceneSystem.GraphicsCompositor = compositor;

                // #region agent log
                DebugLog("E", "SetupGraphicsCompositor:complete", "Compositor set successfully");
                // #endregion

                Console.WriteLine("[Odyssey] GraphicsCompositor set up successfully");
            }
            catch (Exception ex)
            {
                // #region agent log
                DebugLog("E", "SetupGraphicsCompositor:error", "Failed to set up compositor", ex.Message);
                // #endregion
                Console.WriteLine("[Odyssey] ERROR: Failed to set up GraphicsCompositor: " + ex.Message);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            // Call base Update to process Stride game loop
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.Game.html
            // Game.Update(GameTime) is called each frame to update game logic
            // Method signature: protected virtual void Update(GameTime gameTime)
            // Source: https://doc.stride3d.net/latest/en/manual/game-loop/index.html
            base.Update(gameTime);

            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.GameTime.html
            // GameTime.Elapsed property gets the elapsed time since the last frame
            // TotalSeconds property gets the elapsed time in seconds as a double
            // Source: https://doc.stride3d.net/latest/en/manual/game-loop/index.html
            float deltaTime = (float)gameTime.Elapsed.TotalSeconds;

            // Only process game input when actually in-game
            if (_currentState == GameState.InGame)
            {
                ProcessInput(deltaTime);
            }
            else if (_currentState == GameState.MainMenu)
            {
                ProcessMainMenuInput();

                // Update fallback menu renderer input
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.Game.html
                // Game.Input property provides access to the InputManager for handling user input
                // Input property is inherited from Game base class
                // Source: https://doc.stride3d.net/latest/en/manual/input/index.html
                if (_fallbackMenuRenderer != null && _fallbackMenuRenderer.IsVisible)
                {
                    _fallbackMenuRenderer.UpdateMenu(Input);
                }
            }

            // Note: UI input is handled automatically by Stride's UI system processor
            // No manual Update() call needed - UIComponent is processed by UISystem automatically

            if (_isPaused)
            {
                return;
            }

            if (_transitionSystem != null && _transitionSystem.IsTransitioning)
            {
                return;
            }

            _session?.Update(deltaTime);
            _world?.Update(deltaTime);
            if (_playerController != null && !_inDialogue)
            {
                _playerController.Update(deltaTime);
            }

            _triggerSystem?.Update();
            _heartbeatSystem?.Update(deltaTime);
            _session?.DialogueManager?.Update(deltaTime);

            UpdateCamera(deltaTime);
            UpdateDebugDisplay();
        }

        private void ProcessInput(float deltaTime)
        {
            // Check keyboard input
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsKeyPressed(Keys) checks if a key was just pressed this frame (returns true once per press)
            // Method signature: bool IsKeyPressed(Keys key)
            // Keys enum defines keyboard key codes (F1, Escape, Space, etc.)
            // Source: https://doc.stride3d.net/latest/en/manual/input/keyboard.html
            if (Input.IsKeyPressed(Keys.F1))
            {
                _showDebugInfo = !_showDebugInfo;
                if (_hud != null)
                {
                    _hud.ShowDebug = _showDebugInfo;
                }
            }

            if (Input.IsKeyPressed(Keys.Escape))
            {
                if (_inDialogue)
                {
                    _session.DialogueManager?.SkipNode();
                }
                else if (_isPaused)
                {
                    OnResumeGame();
                }
                else if (_uiAvailable)
                {
                    _isPaused = true;
                    if (_pauseMenu != null)
                    {
                        _pauseMenu.IsVisible = true;
                    }
                }
                else
                {
                    // No UI, just exit
                    Exit();
                }
                return;
            }

            if (_isPaused && _pauseMenu != null)
            {
                _pauseMenu.HandleInput(
                    Input.IsKeyPressed(Keys.Up) || Input.IsKeyPressed(Keys.W),
                    Input.IsKeyPressed(Keys.Down) || Input.IsKeyPressed(Keys.S),
                    Input.IsKeyPressed(Keys.Enter) || Input.IsKeyPressed(Keys.Space),
                    Input.IsKeyPressed(Keys.Escape)
                );
                return;
            }

            if (_inDialogue && _dialoguePanel != null)
            {
                _dialoguePanel.HandleInput(
                    Input.IsKeyPressed(Keys.Up) || Input.IsKeyPressed(Keys.W),
                    Input.IsKeyPressed(Keys.Down) || Input.IsKeyPressed(Keys.S),
                    Input.IsKeyPressed(Keys.Enter) || Input.IsKeyPressed(Keys.Space),
                    Input.IsKeyPressed(Keys.Escape)
                );

                for (int i = 1; i <= 9; i++)
                {
                    if (Input.IsKeyPressed((Keys)((int)Keys.D1 + i - 1)))
                    {
                        _dialoguePanel.HandleNumberKey(i);
                    }
                }
                return;
            }

            // Camera controls (WASD + mouse)
            ProcessCameraInput(deltaTime);

            // Click to move
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsMouseButtonPressed(MouseButton) checks if a mouse button was just pressed this frame
            // MousePosition property gets the current mouse position in screen coordinates (X, Y)
            // Method signatures: bool IsMouseButtonPressed(MouseButton button), Vector2 MousePosition { get; }
            // Source: https://doc.stride3d.net/latest/en/manual/input/mouse.html
            // Check for mouse click to move player
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsMouseButtonPressed(MouseButton) checks if a mouse button was just pressed this frame
            // Method signature: bool IsMouseButtonPressed(MouseButton button)
            // MouseButton.Left represents the left mouse button
            // Source: https://doc.stride3d.net/latest/en/manual/input/mouse.html
            if (Input.IsMouseButtonPressed(MouseButton.Left) && _playerController != null)
            {
                System.Numerics.Vector3 worldPos;
                // Convert mouse screen position to normalized coordinates (0-1 range)
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
                // MousePosition property gets the current mouse position in screen coordinates (X, Y)
                // Method signature: Vector2 MousePosition { get; }
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.GameWindow.html
                // Window.ClientBounds.Width/Height get the client area dimensions in pixels
                // Dividing MousePosition by ClientBounds dimensions converts to normalized coordinates
                // Source: https://doc.stride3d.net/latest/en/manual/input/mouse.html
                if (_playerController.ScreenToWorld(
                    Input.MousePosition.X / Window.ClientBounds.Width,
                    Input.MousePosition.Y / Window.ClientBounds.Height,
                    System.Numerics.Matrix4x4.Identity,
                    System.Numerics.Matrix4x4.Identity,
                    out worldPos))
                {
                    _playerController.MoveToPosition(worldPos, !Input.IsKeyDown(Keys.LeftShift));
                }
            }

            if (Input.IsKeyPressed(Keys.Space))
            {
                TryInteractWithNearestObject();
            }
        }

        private void ProcessMainMenuInput()
        {
            // Allow escape to exit from main menu
            if (Input.IsKeyPressed(Keys.Escape))
            {
                Exit();
            }

            // For now, just handle escape. UI elements handle their own input through events.
        }

        private void ProcessCameraInput(float deltaTime)
        {
            if (_cameraEntity == null)
            {
                return;
            }

            float moveSpeed = 20f * deltaTime;
            float rotateSpeed = 2f * deltaTime;

            // WASD movement
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsKeyDown(Keys) checks if a key is currently held down (returns true while key is pressed)
            // Method signature: bool IsKeyDown(Keys key)
            // Source: https://doc.stride3d.net/latest/en/manual/input/keyboard.html
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
            // Vector3.Zero is a static property representing the zero vector (0, 0, 0)
            // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
            Vector3 movement = Vector3.Zero;
            if (Input.IsKeyDown(Keys.W))
            {
                movement.Z -= 1;
            }

            if (Input.IsKeyDown(Keys.S))
            {
                movement.Z += 1;
            }

            if (Input.IsKeyDown(Keys.A))
            {
                movement.X -= 1;
            }

            if (Input.IsKeyDown(Keys.D))
            {
                movement.X += 1;
            }

            if (Input.IsKeyDown(Keys.Q))
            {
                movement.Y -= 1;
            }

            if (Input.IsKeyDown(Keys.E))
            {
                movement.Y += 1;
            }

            if (movement != Vector3.Zero)
            {
                // Transform movement by camera rotation
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Matrix.html
                // Matrix.RotationQuaternion(Quaternion) creates a rotation matrix from a quaternion
                // Vector3.TransformNormal(Vector3, Matrix) transforms a vector by a matrix (ignores translation)
                // Method signatures: static Matrix RotationQuaternion(Quaternion rotation), static Vector3 TransformNormal(Vector3 vector, Matrix transform)
                // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
                var rotation = Matrix.RotationQuaternion(_cameraEntity.Transform.Rotation);
                movement = Vector3.TransformNormal(movement, rotation);
                _cameraEntity.Transform.Position += movement * moveSpeed;
            }

            // Mouse look when right button held
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsMouseButtonDown(MouseButton) checks if a mouse button is currently held down
            // MouseDelta property gets the mouse movement delta since last frame (X, Y)
            // Method signatures: bool IsMouseButtonDown(MouseButton button), Vector2 MouseDelta { get; }
            // Source: https://doc.stride3d.net/latest/en/manual/input/mouse.html
            if (Input.IsMouseButtonDown(MouseButton.Right))
            {
                float yaw = -Input.MouseDelta.X * rotateSpeed;
                float pitch = -Input.MouseDelta.Y * rotateSpeed;

                var currentRotation = _cameraEntity.Transform.Rotation;
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
                // Quaternion.RotationY(float) creates a quaternion representing rotation around Y axis
                // Quaternion.RotationX(float) creates a quaternion representing rotation around X axis
                // Quaternion multiplication combines rotations
                // Method signatures: static Quaternion RotationY(float angle), static Quaternion RotationX(float angle)
                // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
                var yawRotation = Quaternion.RotationY(yaw);
                var pitchRotation = Quaternion.RotationX(pitch);

                _cameraEntity.Transform.Rotation = yawRotation * currentRotation * pitchRotation;
            }

            // Mouse wheel zoom
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // MouseWheelDelta property gets the mouse wheel scroll delta since last frame
            // Method signature: float MouseWheelDelta { get; }
            // Source: https://doc.stride3d.net/latest/en/manual/input/mouse.html
            if (Math.Abs(Input.MouseWheelDelta) > 0.01f)
            {
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Engine.TransformComponent.html
                // Transform.WorldMatrix property gets the world transformation matrix
                // WorldMatrix.Forward property gets the forward vector from the matrix
                // Source: https://doc.stride3d.net/latest/en/manual/entities/transforms.html
                var forward = _cameraEntity.Transform.WorldMatrix.Forward;
                _cameraEntity.Transform.Position += forward * Input.MouseWheelDelta * 2f;
            }
        }

        private void UpdateCamera(float deltaTime)
        {
            // Follow player if available
            var player = _session?.PlayerEntity;
            if (player != null && _chaseCamera != null)
            {
                var transform = player.GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    // _chaseCamera.Update(deltaTime, Input);
                }
            }
        }

        private void UpdateDebugDisplay()
        {
            if (!_showDebugInfo)
            {
                return;
            }

            string debug = "=== Odyssey Engine Demo ===\n";
            debug += "Module: " + (_session?.CurrentModuleName ?? "none") + "\n";
            debug += "Entities: " + _world.GetAllEntities().Count() + "\n";

            var player = _session?.PlayerEntity;
            if (player != null)
            {
                var transform = player.GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    debug += "Player: " + transform.Position.ToString() + "\n";
                }
            }

            if (_cameraEntity != null)
            {
                debug += "Camera: " + _cameraEntity.Transform.Position.ToString() + "\n";
            }

            debug += "\nControls:\n";
            debug += "WASD - Move camera\n";
            debug += "QE - Up/Down\n";
            debug += "Right mouse - Look around\n";
            debug += "Scroll - Zoom\n";
            debug += "F1 - Toggle debug\n";
            debug += "ESC - Exit\n";

            if (_hud != null)
            {
                _hud.SetDebugText(debug);
            }
            else if (_debugTextBlock != null)
            {
                _debugTextBlock.Text = debug;
            }
        }

        private void TryInteractWithNearestObject()
        {
            var player = _session?.PlayerEntity;
            if (player == null)
            {
                return;
            }

            var playerTransform = player.GetComponent<ITransformComponent>();
            if (playerTransform == null)
            {
                return;
            }

            float nearestDist = 3.0f;
            IEntity nearestEntity = null;

            foreach (var door in _world.GetEntitiesOfType(ObjectType.Door))
            {
                var doorTransform = door.GetComponent<ITransformComponent>();
                if (doorTransform != null)
                {
                    float dist = System.Numerics.Vector3.Distance(playerTransform.Position, doorTransform.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestEntity = door;
                    }
                }
            }

            foreach (var placeable in _world.GetEntitiesOfType(ObjectType.Placeable))
            {
                var placeableTransform = placeable.GetComponent<ITransformComponent>();
                if (placeableTransform != null)
                {
                    float dist = System.Numerics.Vector3.Distance(playerTransform.Position, placeableTransform.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestEntity = placeable;
                    }
                }
            }

            foreach (var creature in _world.GetEntitiesOfType(ObjectType.Creature))
            {
                if (creature == player)
                {
                    continue;
                }

                var creatureTransform = creature.GetComponent<ITransformComponent>();
                if (creatureTransform != null)
                {
                    float dist = System.Numerics.Vector3.Distance(playerTransform.Position, creatureTransform.Position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestEntity = creature;
                    }
                }
            }

            if (nearestEntity != null)
            {
                InteractWith(nearestEntity);
            }
        }

        private void InteractWith(IEntity entity)
        {
            switch (entity.ObjectType)
            {
                case ObjectType.Door: InteractWithDoor(entity); break;
                case ObjectType.Placeable: InteractWithPlaceable(entity); break;
                case ObjectType.Creature: InteractWithCreature(entity); break;
            }
        }

        private void InteractWithDoor(IEntity door)
        {
            var doorComponent = door.GetComponent<IDoorComponent>();
            if (doorComponent == null)
            {
                return;
            }

            if (_transitionSystem != null && _transitionSystem.CanDoorTransition(door))
            {
                _transitionSystem.TransitionThroughDoor(door, _session.PlayerEntity);
                return;
            }

            if (doorComponent.IsLocked)
            {
                FireScriptEvent(door, ScriptEvent.OnFailToOpen, _session.PlayerEntity);
                return;
            }

            if (doorComponent.IsOpen)
            {
                doorComponent.Close();
                FireScriptEvent(door, ScriptEvent.OnClose, _session.PlayerEntity);
            }
            else
            {
                doorComponent.Open();
                FireScriptEvent(door, ScriptEvent.OnOpen, _session.PlayerEntity);
            }
        }

        private void InteractWithPlaceable(IEntity placeable)
        {
            FireScriptEvent(placeable, ScriptEvent.OnUsed, _session.PlayerEntity);
        }

        private void InteractWithCreature(IEntity creature)
        {
            var scriptHooks = creature.GetComponent<IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                string conversation = scriptHooks.GetLocalString("Conversation");
                if (!string.IsNullOrEmpty(conversation))
                {
                    StartConversation(creature, conversation);
                    return;
                }
            }

            FireScriptEvent(creature, ScriptEvent.OnConversation, _session.PlayerEntity);
        }

        private void StartConversation(IEntity npc, string dialogueResRef)
        {
            if (_session.DialogueManager == null)
            {
                return;
            }

            if (_session.DialogueManager.StartConversation(dialogueResRef, npc, _session.PlayerEntity))
            {
                _inDialogue = true;
                if (_hud != null)
                {
                    _hud.IsVisible = false;
                }
            }
        }

        private void OnModuleLoaded(Odyssey.Kotor.Game.ModuleLoadedEventArgs e)
        {
            Console.WriteLine("[Odyssey] Module loaded: " + e.ModuleName);

            if (_loadingScreen != null)
            {
                _loadingScreen.Hide();
            }

            // Build scene from LYT
            BuildSceneFromModule();

            // Setup player controller
            if (_session.PlayerEntity != null && _session.NavigationMesh != null)
            {
                _playerController = new Odyssey.Kotor.Input.PlayerController(_session.PlayerEntity, _session.NavigationMesh);
            }

            // Position player at entry point
            if (_session.PlayerEntity != null)
            {
                var playerTransform = _session.PlayerEntity.GetComponent<ITransformComponent>();
                var moduleLoader = GetModuleLoader();
                if (playerTransform != null && moduleLoader != null)
                {
                    playerTransform.Position = moduleLoader.GetEntryPosition();
                    playerTransform.Facing = moduleLoader.GetEntryFacing();

                    // Position camera above player
                    if (_cameraEntity != null)
                    {
                        var playerPos = playerTransform.Position;
                        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Vector3.html
                        // Vector3(float x, float y, float z) constructor creates camera position offset from player
                        // Method signature: Vector3(float x, float y, float z)
                        _cameraEntity.Transform.Position = new Vector3(playerPos.X, playerPos.Z + 15, playerPos.Y + 15);
                        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Quaternion.html
                        // Quaternion.RotationX(float) creates a quaternion representing rotation around X axis
                        // Method signature: static Quaternion RotationX(float angle)
                        // Source: https://doc.stride3d.net/latest/en/manual/mathematics/index.html
                        _cameraEntity.Transform.Rotation = Quaternion.RotationX(-0.5f);
                    }
                }
            }

            if (_heartbeatSystem != null)
            {
                _heartbeatSystem.Clear();
                _heartbeatSystem.RegisterAllEntities();
            }

            // Load in-game HUD GUI from KOTOR game files
            if (_kotorGuiManager != null)
            {
                try
                {
                    // Standard KOTOR HUD: "default" or "partybar"
                    bool hudLoaded = _kotorGuiManager.LoadGui("partybar",
                        Window.ClientBounds.Width,
                        Window.ClientBounds.Height);

                    if (!hudLoaded)
                    {
                        // Try alternate HUD name
                        hudLoaded = _kotorGuiManager.LoadGui("default",
                            Window.ClientBounds.Width,
                            Window.ClientBounds.Height);
                    }

                    if (hudLoaded)
                    {
                        Console.WriteLine("[Odyssey] In-game HUD GUI loaded");
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: Could not load in-game HUD GUI");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Odyssey] ERROR loading HUD GUI: {ex.Message}");
                }
            }
            else if (_hud != null)
            {
                // Fallback to custom HUD if KOTOR GUI failed
                _hud.IsVisible = true;
            }
        }

        private ModuleLoader GetModuleLoader()
        {
            // Access module loader through reflection (GameSession wraps it)
            var sessionType = _session.GetType();
            var field = sessionType.GetField("_moduleLoader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                return field.GetValue(_session) as ModuleLoader;
            }
            return null;
        }

        private void BuildSceneFromModule()
        {
            if (_sceneBuilder == null)
            {
                Console.WriteLine("[Odyssey] Warning: SceneBuilder not initialized");
                return;
            }

            var moduleLoader = GetModuleLoader();
            if (moduleLoader == null)
            {
                Console.WriteLine("[Odyssey] Warning: ModuleLoader not accessible");
                return;
            }

            LYT lyt = moduleLoader.CurrentLYT;
            VIS vis = moduleLoader.CurrentVIS;

            if (lyt == null)
            {
                Console.WriteLine("[Odyssey] Warning: No LYT data for scene building");
                return;
            }

            Console.WriteLine("[Odyssey] Building scene from LYT with " + lyt.Rooms.Count + " rooms");

            try
            {
                _sceneBuilder.BuildScene(
                    lyt,
                    vis,
                    (resref) => moduleLoader.LoadModel(resref),
                    (resref) => moduleLoader.LoadTexture(resref)
                );
                Console.WriteLine("[Odyssey] Scene built successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] Failed to build scene: " + ex.Message);
            }
        }

        private void OnTransitionStart(object sender, ModuleTransitionEventArgs e)
        {
            if (_loadingScreen != null)
            {
                _loadingScreen.Show(e.TargetModule);
            }

            if (_hud != null)
            {
                _hud.IsVisible = false;
            }
        }

        private void OnTransitionComplete(object sender, ModuleTransitionEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.TargetWaypoint))
            {
                PositionPlayerAtWaypoint(_session.PlayerEntity, e.TargetWaypoint);
            }

            if (_loadingScreen != null)
            {
                _loadingScreen.Hide();
            }

            if (_hud != null)
            {
                _hud.IsVisible = true;
            }
        }

        private void OnTransitionFailed(object sender, ModuleTransitionEventArgs e)
        {
            if (_loadingScreen != null)
            {
                _loadingScreen.Hide();
            }
        }

        private void OnResumeGame()
        {
            _isPaused = false;
            if (_pauseMenu != null)
            {
                _pauseMenu.IsVisible = false;
            }

            _session?.Resume();
        }

        private void OnExitGame()
        {
            Exit();
        }

        private void OnDialogueReplySelected(int replyIndex)
        {
            _session.DialogueManager?.SelectReply(replyIndex);
        }

        private void OnDialogueSkip()
        {
            _session.DialogueManager?.SkipNode();
        }

        private int _drawCallCount = 0;

        protected override void Draw(GameTime gameTime)
        {
            // #region agent log
            if (_drawCallCount == 0 || _drawCallCount == 60)
            {
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.Game.html
                // Game.Services property provides access to the service registry
                // GetService<T>() retrieves a service of type T from the registry
                // Method signature: T GetService<T>() where T : class
                // Source: https://doc.stride3d.net/latest/en/manual/game-loop/index.html
                var sceneSystem = Services.GetService<SceneSystem>();
                DebugLog("F", "OdysseyGame.Draw", "Draw frame " + _drawCallCount,
                    "compositor=" + (sceneSystem?.GraphicsCompositor != null) +
                    " sceneInstance=" + (sceneSystem?.SceneInstance != null) +
                    " entities=" + (sceneSystem?.SceneInstance?.RootScene?.Entities?.Count ?? 0) +
                    " presenter=" + (GraphicsDevice?.Presenter != null) +
                    " backBuffer=" + (GraphicsDevice?.Presenter?.BackBuffer != null));
            }
            _drawCallCount++;
            // #endregion

            // CRITICAL: First set the render target to the back buffer
            // Without this, rendering may go to the wrong target (black screen)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.GraphicsDevice.html
            // GraphicsDevice.Presenter provides access to the presentation surface
            // Presenter.BackBuffer is the main render target, Presenter.DepthStencilBuffer is the depth buffer
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
            if (GraphicsDevice?.Presenter?.BackBuffer != null && GraphicsDevice?.Presenter?.DepthStencilBuffer != null)
            {
                // Set render target and viewport for rendering
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.Game.html
                // Game.GraphicsContext property provides access to the graphics context for rendering operations
                // GraphicsContext.CommandList property provides access to the command list for GPU operations
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.CommandList.html
                // CommandList.SetRenderTargetAndViewport(Texture, Texture) sets render target and viewport
                // Method signature: void SetRenderTargetAndViewport(Texture depthStencilBuffer, Texture renderTarget)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
                GraphicsContext.CommandList.SetRenderTargetAndViewport(
                    GraphicsDevice.Presenter.DepthStencilBuffer,
                    GraphicsDevice.Presenter.BackBuffer);

                // Clear with a solid dark blue background
                // This ensures we ALWAYS have a visible background, not transparent/black
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.CommandList.html
                // CommandList.Clear(Texture, Color4) clears a render target with a color
                // Method signature: void Clear(Texture renderTarget, Color4 color)
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.Mathematics.Color4.html
                // Color4(float r, float g, float b, float a) constructor creates a color from RGBA float values (0-1)
                // Method signature: Color4(float r, float g, float b, float a)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
                GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.BackBuffer, new Color4(0.04f, 0.04f, 0.12f, 1f));
                // Clear depth buffer
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.CommandList.html
                // CommandList.Clear(Texture, DepthStencilClearOptions) clears depth/stencil buffer
                // Method signature: void Clear(Texture depthStencilBuffer, DepthStencilClearOptions options)
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.DepthStencilClearOptions.html
                // DepthStencilClearOptions.DepthBuffer clears only the depth buffer (not stencil)
                // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
                GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);
            }

            // Call base to render scene and UI
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.Game.html
            // Game.Draw(GameTime) is called each frame to render the scene
            // Method signature: protected virtual void Draw(GameTime gameTime)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.GameTime.html
            // GameTime parameter provides timing information for the current frame
            // Source: https://doc.stride3d.net/latest/en/manual/game-loop/index.html
            base.Draw(gameTime);
        }

        protected override void Destroy()
        {
            if (_transitionSystem != null)
            {
                _transitionSystem.OnTransitionStart -= OnTransitionStart;
                _transitionSystem.OnTransitionComplete -= OnTransitionComplete;
                _transitionSystem.OnTransitionFailed -= OnTransitionFailed;
            }

            if (_session != null)
            {
                _session.OnModuleLoaded -= (sender, e) => OnModuleLoaded(e);
                _session.Dispose();
            }

            _triggerSystem?.Clear();
            _heartbeatSystem?.Clear();

            // Call base Destroy to clean up Stride game systems
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.Game.html
            // Game.Destroy() is called when the game is shutting down
            // Method signature: protected virtual void Destroy()
            // Source: https://doc.stride3d.net/latest/en/manual/game-loop/index.html
            // Note: On some systems, the OpenGL context may be destroyed before resources are cleaned up,
            // causing a "No GL driver has been loaded" error from Silk.NET during cleanup.
            // This is safe to suppress during shutdown as the application is already closing.
            try
            {
                base.Destroy();
            }
            catch (Exception ex) when (ex.Message != null && (
                ex.Message.Contains("GL driver") ||
                ex.Message.Contains("No GL") ||
                ex.Message.Contains("OpenGL") ||
                ex.GetType().Name.Contains("OpenGL") ||
                ex.GetType().Name.Contains("GL")))
            {
                // Suppress OpenGL/graphics cleanup errors during shutdown - the application is already closing
                // This can occur when the OpenGL context is destroyed before resources are cleaned up
                Console.WriteLine("[Odyssey] Suppressed graphics cleanup error during shutdown: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Simple resource provider that wraps GameSession for SceneBuilder.
    /// </summary>
    internal class GameResourceProvider : IGameResourceProvider
    {
        private readonly GameSession _session;
        private readonly GameSettings _settings;

        public GameResourceProvider(GameSession session, GameSettings settings)
        {
            _session = session;
            _settings = settings;
        }

        public GameType GameType
        {
            get { return _settings.Game == KotorGame.K1 ? GameType.K1 : GameType.K2; }
        }

        public System.Threading.Tasks.Task<System.IO.Stream> OpenResourceAsync(ResourceIdentifier id, System.Threading.CancellationToken ct)
        {
            return System.Threading.Tasks.Task.FromResult<System.IO.Stream>(null);
        }

        public System.Threading.Tasks.Task<bool> ExistsAsync(ResourceIdentifier id, System.Threading.CancellationToken ct)
        {
            return System.Threading.Tasks.Task.FromResult(false);
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.IReadOnlyList<Odyssey.Content.Interfaces.LocationResult>> LocateAsync(
            ResourceIdentifier id,
            Odyssey.Content.Interfaces.SearchLocation[] order,
            System.Threading.CancellationToken ct)
        {
            var result = new System.Collections.Generic.List<Odyssey.Content.Interfaces.LocationResult>();
            return System.Threading.Tasks.Task.FromResult<System.Collections.Generic.IReadOnlyList<Odyssey.Content.Interfaces.LocationResult>>(result);
        }

        public System.Collections.Generic.IEnumerable<ResourceIdentifier> EnumerateResources(CSharpKOTOR.Resources.ResourceType type)
        {
            return new System.Collections.Generic.List<ResourceIdentifier>();
        }

        public System.Threading.Tasks.Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, System.Threading.CancellationToken ct)
        {
            return System.Threading.Tasks.Task.FromResult<byte[]>(null);
        }
    }
}
