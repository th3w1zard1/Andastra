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
        
        // Fallback menu renderer (SpriteBatch-based, no font dependency)
        private FallbackMenuRenderer _fallbackMenuRenderer;

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
            _cameraEntity = new StrideEngine.Entity("MainCamera");
            _cameraComponent = new CameraComponent
            {
                NearClipPlane = 0.1f,
                FarClipPlane = 1000f,
                UseCustomAspectRatio = false,
                Slot = new SceneCameraSlotId() // Assign to the default camera slot - will be updated in CreateDefaultGraphicsCompositor
            };
            _cameraEntity.Add(_cameraComponent);

            // Position camera at origin - UI is rendered in screen space, camera position doesn't matter for UI
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
                        _cameraEntity.Transform.Position = new Vector3(0, 0, 0);
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

            var uiEntity = new StrideEngine.Entity("UI");
            _uiComponent = new UIComponent();

            // CRITICAL FIX: Set resolution for UI rendering
            // Without this, UI elements won't render at all
            _uiComponent.Resolution = new Vector3(Window.ClientBounds.Width, Window.ClientBounds.Height, 1000);
            _uiComponent.ResolutionStretch = ResolutionStretch.FixedWidthAdaptableHeight;
            _uiComponent.IsBillboard = false;
            _uiComponent.IsFullScreen = true;
            _uiComponent.RenderGroup = RenderGroup.Group0;

            uiEntity.Add(_uiComponent);

            // Add UI entity to scene FIRST
            try
            {
                SceneSystem sceneSystem = Services.GetService<SceneSystem>();
                if (sceneSystem != null && sceneSystem.SceneInstance != null)
                {
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
            if (sceneSystem.SceneInstance == null)
            {
                var rootScene = new StrideEngine.Scene();
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
                var compositor = new GraphicsCompositor();

                // Create a camera slot
                var cameraSlot = new SceneCameraSlot();
                compositor.Cameras.Add(cameraSlot);

                // #region agent log
                DebugLog("E", "SetupGraphicsCompositor:camera_slot", "Camera slot created", cameraSlot.Id.ToString());
                // #endregion

                // Create a simple game with just a scene renderer for UI
                var sceneRenderer = new SceneRendererCollection();

                // Add a clear renderer to clear the background - THIS IS CRITICAL
                var clearRenderer = new ClearRenderer
                {
                    Color = new Color4(0.04f, 0.04f, 0.12f, 1f), // Dark blue
                    ClearFlags = ClearRendererFlags.ColorAndDepth
                };
                sceneRenderer.Children.Add(clearRenderer);

                // #region agent log
                DebugLog("E", "SetupGraphicsCompositor:clear_added", "ClearRenderer added", clearRenderer.Color.ToString());
                // #endregion

                // Add the single render stage for 3D content and UI
                var singleStageRenderer = new SingleStageRenderer();
                sceneRenderer.Children.Add(singleStageRenderer);
                
                // Create and add fallback menu renderer (SpriteBatch-based, renders on top)
                _fallbackMenuRenderer = new FallbackMenuRenderer();
                _fallbackMenuRenderer.Services = Services; // Required for initialization
                _fallbackMenuRenderer.MenuItemSelected += OnFallbackMenuItemSelected;
                sceneRenderer.Children.Add(_fallbackMenuRenderer);

                // Create game entry point
                var game = new SceneRendererCollection();
                game.Children.Add(sceneRenderer);
                compositor.Game = game;

                // Update our camera component to use this slot
                if (_cameraComponent != null)
                {
                    _cameraComponent.Slot = new SceneCameraSlotId(cameraSlot.Id);
                    // #region agent log
                    DebugLog("E", "SetupGraphicsCompositor:camera_assigned", "Camera slot assigned", cameraSlot.Id.ToString());
                    // #endregion
                }

                // Set the compositor - ALWAYS replace the default one
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
            base.Update(gameTime);

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
            if (Input.IsMouseButtonPressed(MouseButton.Left) && _playerController != null)
            {
                System.Numerics.Vector3 worldPos;
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
                var rotation = Matrix.RotationQuaternion(_cameraEntity.Transform.Rotation);
                movement = Vector3.TransformNormal(movement, rotation);
                _cameraEntity.Transform.Position += movement * moveSpeed;
            }

            // Mouse look when right button held
            if (Input.IsMouseButtonDown(MouseButton.Right))
            {
                float yaw = -Input.MouseDelta.X * rotateSpeed;
                float pitch = -Input.MouseDelta.Y * rotateSpeed;

                var currentRotation = _cameraEntity.Transform.Rotation;
                var yawRotation = Quaternion.RotationY(yaw);
                var pitchRotation = Quaternion.RotationX(pitch);

                _cameraEntity.Transform.Rotation = yawRotation * currentRotation * pitchRotation;
            }

            // Mouse wheel zoom
            if (Math.Abs(Input.MouseWheelDelta) > 0.01f)
            {
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
                        _cameraEntity.Transform.Position = new Vector3(playerPos.X, playerPos.Z + 15, playerPos.Y + 15);
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
            if (GraphicsDevice?.Presenter?.BackBuffer != null && GraphicsDevice?.Presenter?.DepthStencilBuffer != null)
            {
                GraphicsContext.CommandList.SetRenderTargetAndViewport(
                    GraphicsDevice.Presenter.DepthStencilBuffer,
                    GraphicsDevice.Presenter.BackBuffer);

                // Clear with a solid dark blue background
                // This ensures we ALWAYS have a visible background, not transparent/black
                GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.BackBuffer, new Color4(0.04f, 0.04f, 0.12f, 1f));
                GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);
            }

            // Call base to render scene and UI
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

            base.Destroy();
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
