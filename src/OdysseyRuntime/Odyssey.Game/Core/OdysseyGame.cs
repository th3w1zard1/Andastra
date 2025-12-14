using System;
using System.Linq;
using StrideEngine = Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Compositing;
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
using CSharpKOTOR.Formats.LYT;
using CSharpKOTOR.Formats.VIS;
using CSharpKOTOR.Resources;
using JetBrains.Annotations;

namespace Odyssey.Game.Core
{
    public class OdysseyGame : StrideEngine.Game
    {
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

        private bool _showDebugInfo = true; // Default to debug mode for demo
        private bool _isPaused = false;
        private bool _inDialogue = false;

        // Debug text overlay
        private TextBlock _debugTextBlock;

        public OdysseyGame(GameSettings settings)
        {
            _settings = settings;
        }

        protected override void Initialize()
        {
            base.Initialize();

            Window.Title = "Odyssey Engine - " + (_settings.Game == KotorGame.K1 ? "Knights of the Old Republic" : "The Sith Lords");

            _world = new World();
            _globals = new ScriptGlobals();
            _engineApi = new K1EngineApi();
            _vm = new NcsVm();

            _session = new GameSession(_settings, _world, _vm, _globals);

            InitializeSystems();
            InitializeCamera();

            Console.WriteLine("[Odyssey] Core systems initialized");
        }

        private void InitializeCamera()
        {
            // Create main camera entity
            _cameraEntity = new StrideEngine.Entity("MainCamera");
            _cameraComponent = new CameraComponent
            {
                NearClipPlane = 0.1f,
                FarClipPlane = 1000f,
                UseCustomAspectRatio = false
            };
            _cameraEntity.Add(_cameraComponent);

            // Position camera at a default viewing position
            _cameraEntity.Transform.Position = new Vector3(0, 5, 10);
            _cameraEntity.Transform.Rotation = Quaternion.RotationX(-0.3f);

            // Defer adding to scene until BeginRun when SceneSystem is available
            Console.WriteLine("[Odyssey] Camera entity created (will be added to scene in BeginRun)");
        }

        private void SetGameState(GameState newState)
        {
            GameState oldState = _currentState;
            _currentState = newState;

            Console.WriteLine($"[Odyssey] Game state changed: {oldState} -> {newState}");

            // Hide all UI elements first
            if (_mainMenu != null) _mainMenu.IsVisible = false;
            if (_hud != null) _hud.IsVisible = false;
            if (_pauseMenu != null) _pauseMenu.IsVisible = false;
            if (_loadingScreen != null) _loadingScreen.IsVisible = false;
            if (_dialoguePanel != null) _dialoguePanel.IsVisible = false;

            // Handle camera/scene visibility based on state
            UpdateCameraVisibility(newState);

            // Show UI appropriate for the new state
            switch (newState)
            {
                case GameState.MainMenu:
                    if (_mainMenu != null) _mainMenu.IsVisible = true;
                    break;

                case GameState.Loading:
                    if (_loadingScreen != null) _loadingScreen.Show("Loading...");
                    break;

                case GameState.InGame:
                    if (_hud != null) _hud.IsVisible = true;
                    break;

                case GameState.Paused:
                    if (_pauseMenu != null) _pauseMenu.IsVisible = true;
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

        private void InitializeUI()
        {
            var uiEntity = new StrideEngine.Entity("UI");
            _uiComponent = new UIComponent();
            uiEntity.Add(_uiComponent);

            // Add UI entity to scene FIRST
            try
            {
                var sceneSystem = Services.GetService<SceneSystem>();
                if (sceneSystem != null && sceneSystem.SceneInstance != null)
                {
                    sceneSystem.SceneInstance.RootScene.Entities.Add(uiEntity);
                    Console.WriteLine("[Odyssey] UI entity added to scene");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] WARNING: Failed to add UI entity to scene: " + ex.Message);
            }

            // Try to load font - Stride's Content.Load may return null instead of throwing
            _font = null;
            try
            {
                _font = Content.Load<SpriteFont>("DefaultFont");
                if (_font != null)
                {
                    Console.WriteLine("[Odyssey] Font loaded successfully");
                }
                else
                {
                    Console.WriteLine("[Odyssey] Warning: DefaultFont returned null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] Warning: DefaultFont not found: " + ex.Message);
                _font = null;
            }

            // ALWAYS use fallback for now - the font system isn't working
            // TODO: Properly create and include a SpriteFont asset
            _font = null; // Force fallback mode

            if (_font != null)
            {
                // Full UI with fonts
                var canvas = new Canvas();
                var page = new UIPage { RootElement = canvas };
                _uiComponent.Page = page;

                _uiAvailable = true;
                _mainMenu = new MainMenu(_uiComponent, _font);
                _hud = new BasicHUD(_uiComponent, _font);
                _pauseMenu = new PauseMenu(_uiComponent, _font);
                _loadingScreen = new LoadingScreen(_uiComponent, _font);
                _dialoguePanel = new DialoguePanel(_uiComponent, _font);

                _mainMenu.OnStartGame += OnStartGame;
                _pauseMenu.OnResume += OnResumeGame;
                _pauseMenu.OnExit += OnExitGame;
                _dialoguePanel.OnReplySelected += OnDialogueReplySelected;
                _dialoguePanel.OnSkipRequested += OnDialogueSkip;

                Console.WriteLine("[Odyssey] UI initialized with font");
            }
            else
            {
                // FALLBACK: Create a simple colored UI without fonts
                // This ensures we always have SOMETHING visible on screen
                _uiAvailable = true; // Mark as available since we have fallback
                Console.WriteLine("[Odyssey] Creating fallback UI (no font available)");
                CreateFallbackMainMenu();
            }
        }

        /// <summary>
        /// Creates a simple fallback main menu using only colored rectangles.
        /// This is used when no font is available.
        /// </summary>
        private void CreateFallbackMainMenu()
        {
            // Create root canvas with full screen dark background
            var canvas = new Canvas
            {
                BackgroundColor = new Color(10, 10, 30, 255), // Dark blue background
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Main panel - centered colored rectangle
            var mainPanel = new Grid
            {
                Width = 600,
                Height = 450,
                BackgroundColor = new Color(20, 20, 50, 240), // Darker panel
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Add row definitions
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 80));  // Title area
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 20));  // Spacer
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 60));  // Info row 1
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 60));  // Info row 2
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 20));  // Spacer
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Fixed, 70));  // Start button
            mainPanel.RowDefinitions.Add(new StripDefinition(StripType.Star, 1));    // Status area

            // Title bar (golden/amber color for Star Wars feel)
            var titleBar = new Border
            {
                BackgroundColor = new Color(200, 150, 50, 255), // Gold
                Margin = new Thickness(20, 15, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            titleBar.SetGridRow(0);
            mainPanel.Children.Add(titleBar);

            // Add title text if we can
            var titleText = new TextBlock
            {
                Text = "ODYSSEY ENGINE",
                TextSize = 28,
                TextColor = new Color(10, 10, 30, 255), // Dark text on gold
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleBar.Content = titleText;

            // Info panel 1 - Game detected
            var infoPanel1 = new Border
            {
                BackgroundColor = new Color(40, 60, 80, 255), // Blue-gray
                Margin = new Thickness(20, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var infoText1 = new TextBlock
            {
                Text = "KOTOR 1 Detected",
                TextSize = 16,
                TextColor = new Color(150, 200, 255, 255), // Light blue
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            infoPanel1.Content = infoText1;
            infoPanel1.SetGridRow(2);
            mainPanel.Children.Add(infoPanel1);

            // Info panel 2 - Module
            var infoPanel2 = new Border
            {
                BackgroundColor = new Color(40, 60, 80, 255),
                Margin = new Thickness(20, 5, 20, 5),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var infoText2 = new TextBlock
            {
                Text = "Module: end_m01aa (Endar Spire)",
                TextSize = 14,
                TextColor = new Color(150, 200, 255, 255),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            infoPanel2.Content = infoText2;
            infoPanel2.SetGridRow(3);
            mainPanel.Children.Add(infoPanel2);

            // Start button (green)
            var startButton = new Button
            {
                BackgroundColor = new Color(30, 120, 30, 255), // Green
                Margin = new Thickness(80, 10, 80, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            var startText = new TextBlock
            {
                Text = "CLICK TO START",
                TextSize = 20,
                TextColor = Color.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            startButton.Content = startText;
            startButton.Click += OnFallbackStartClicked;
            startButton.SetGridRow(5);
            mainPanel.Children.Add(startButton);

            // Status text at bottom
            var statusText = new TextBlock
            {
                Text = "Press ESCAPE to exit",
                TextSize = 12,
                TextColor = new Color(180, 180, 100, 255), // Yellow-ish
                Margin = new Thickness(20, 10, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top
            };
            statusText.SetGridRow(6);
            mainPanel.Children.Add(statusText);

            // Outer border for visual polish
            var outerBorder = new Border
            {
                BorderColor = new Color(100, 80, 40, 255), // Bronze border
                BorderThickness = new Thickness(3, 3, 3, 3),
                Content = mainPanel,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            canvas.Children.Add(outerBorder);

            // Set the page
            var page = new UIPage { RootElement = canvas };
            _uiComponent.Page = page;

            Console.WriteLine("[Odyssey] Fallback main menu created");
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
            if (scriptHooks == null) return;

            string scriptResRef = scriptHooks.GetScript(scriptEvent);
            if (string.IsNullOrEmpty(scriptResRef)) return;

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
            if (player == null || string.IsNullOrEmpty(waypointTag)) return;

            var waypoint = _world.GetEntityByTag(waypointTag);
            if (waypoint == null) return;

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
            base.BeginRun();

            // CRITICAL: Set up the GraphicsCompositor for rendering
            // Without a compositor, NOTHING will render to screen
            SetupGraphicsCompositor();

            // CRITICAL: Create SceneInstance if it doesn't exist
            // Without a SceneInstance, nothing will render (purple screen)
            var sceneSystem = Services.GetService<SceneSystem>();
            if (sceneSystem != null && sceneSystem.SceneInstance == null)
            {
                var rootScene = new StrideEngine.Scene();
                sceneSystem.SceneInstance = new SceneInstance(Services, rootScene);
                Console.WriteLine("[Odyssey] Created root SceneInstance for rendering");
            }

            // Add camera to scene now that SceneSystem is available
            // Camera MUST be in scene for rendering to work, even in MainMenu state
            if (_cameraEntity != null)
            {
                try
                {
                    if (sceneSystem != null && sceneSystem.SceneInstance != null)
                    {
                        // Ensure camera is in scene (needed for all states including MainMenu)
                        if (!sceneSystem.SceneInstance.RootScene.Entities.Contains(_cameraEntity))
                        {
                            sceneSystem.SceneInstance.RootScene.Entities.Add(_cameraEntity);
                            Console.WriteLine("[Odyssey] Camera added to scene at " + _cameraEntity.Transform.Position);
                        }
                    }
                    else
                    {
                        Console.WriteLine("[Odyssey] WARNING: SceneSystem not available, camera not added");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[Odyssey] WARNING: Failed to add camera to scene: " + ex.Message);
                }
            }

            InitializeUI();
            InitializeSceneBuilder();

            // Start in main menu state
            SetGameState(GameState.MainMenu);
        }

        /// <summary>
        /// Sets up a basic GraphicsCompositor for rendering.
        /// This is REQUIRED for anything to appear on screen in Stride.
        /// </summary>
        private void SetupGraphicsCompositor()
        {
            var sceneSystem = Services.GetService<SceneSystem>();
            if (sceneSystem == null)
            {
                Console.WriteLine("[Odyssey] ERROR: SceneSystem not available for compositor setup");
                return;
            }

            try
            {
                // Create a basic graphics compositor
                var compositor = new GraphicsCompositor();

                // Create a camera slot
                var cameraSlot = new SceneCameraSlot();
                compositor.Cameras.Add(cameraSlot);

                // Create a simple game with just a scene renderer for UI
                var sceneRenderer = new SceneRendererCollection();

                // Add a clear renderer to clear the background
                var clearRenderer = new ClearRenderer
                {
                    Color = new Color4(0.04f, 0.04f, 0.12f, 1f), // Dark blue
                    ClearFlags = ClearRendererFlags.ColorAndDepth
                };
                sceneRenderer.Children.Add(clearRenderer);

                // Add the single render stage for 3D content and UI
                var singleStageRenderer = new SingleStageRenderer();
                sceneRenderer.Children.Add(singleStageRenderer);

                // Create game entry point
                var game = new SceneRendererCollection();
                game.Children.Add(sceneRenderer);
                compositor.Game = game;

                // Set the compositor
                sceneSystem.GraphicsCompositor = compositor;

                Console.WriteLine("[Odyssey] GraphicsCompositor set up successfully");
            }
            catch (Exception ex)
            {
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
            }

            if (_isPaused) return;
            if (_transitionSystem != null && _transitionSystem.IsTransitioning) return;

            _session?.Update(deltaTime);
            _world?.Update(deltaTime);
            if (_playerController != null && !_inDialogue) _playerController.Update(deltaTime);
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
                if (_hud != null) _hud.ShowDebug = _showDebugInfo;
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
                    if (_pauseMenu != null) _pauseMenu.IsVisible = true;
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

            if (Input.IsKeyPressed(Keys.Space)) TryInteractWithNearestObject();
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
            if (_cameraEntity == null) return;

            float moveSpeed = 20f * deltaTime;
            float rotateSpeed = 2f * deltaTime;

            // WASD movement
            Vector3 movement = Vector3.Zero;
            if (Input.IsKeyDown(Keys.W)) movement.Z -= 1;
            if (Input.IsKeyDown(Keys.S)) movement.Z += 1;
            if (Input.IsKeyDown(Keys.A)) movement.X -= 1;
            if (Input.IsKeyDown(Keys.D)) movement.X += 1;
            if (Input.IsKeyDown(Keys.Q)) movement.Y -= 1;
            if (Input.IsKeyDown(Keys.E)) movement.Y += 1;

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
            if (!_showDebugInfo) return;

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
            if (player == null) return;

            var playerTransform = player.GetComponent<ITransformComponent>();
            if (playerTransform == null) return;

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
                if (creature == player) continue;

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

            if (nearestEntity != null) InteractWith(nearestEntity);
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
            if (doorComponent == null) return;

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
            if (_session.DialogueManager == null) return;

            if (_session.DialogueManager.StartConversation(dialogueResRef, npc, _session.PlayerEntity))
            {
                _inDialogue = true;
                if (_hud != null) _hud.IsVisible = false;
            }
        }

        private void OnModuleLoaded(Odyssey.Kotor.Game.ModuleLoadedEventArgs e)
        {
            Console.WriteLine("[Odyssey] Module loaded: " + e.ModuleName);

            if (_loadingScreen != null) _loadingScreen.Hide();

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

            if (_hud != null) _hud.IsVisible = true;
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
            if (_loadingScreen != null) _loadingScreen.Show(e.TargetModule);
            if (_hud != null) _hud.IsVisible = false;
        }

        private void OnTransitionComplete(object sender, ModuleTransitionEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.TargetWaypoint))
                PositionPlayerAtWaypoint(_session.PlayerEntity, e.TargetWaypoint);
            if (_loadingScreen != null) _loadingScreen.Hide();
            if (_hud != null) _hud.IsVisible = true;
        }

        private void OnTransitionFailed(object sender, ModuleTransitionEventArgs e)
        {
            if (_loadingScreen != null) _loadingScreen.Hide();
        }

        private void OnResumeGame()
        {
            _isPaused = false;
            if (_pauseMenu != null) _pauseMenu.IsVisible = false;
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

        protected override void Draw(GameTime gameTime)
        {
            // CRITICAL: First set the render target to the back buffer
            GraphicsContext.CommandList.SetRenderTargetAndViewport(GraphicsDevice.Presenter.DepthStencilBuffer, GraphicsDevice.Presenter.BackBuffer);

            // Clear with a solid dark blue background
            // This ensures we ALWAYS have a visible background, not transparent/purple
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.BackBuffer, new Color4(0.04f, 0.04f, 0.12f, 1f));
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);

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
