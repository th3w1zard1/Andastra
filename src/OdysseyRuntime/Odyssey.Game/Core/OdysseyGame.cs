using System;
using System.Linq;
using StrideEngine = Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.UI;
using Stride.UI.Panels;
using Stride.UI.Controls;
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

        // Scene rendering
        private SceneBuilder _sceneBuilder;
        private StrideEngine.Entity _cameraEntity;
        private CameraComponent _cameraComponent;

        private ChaseCamera _chaseCamera;
        private Odyssey.Kotor.Input.PlayerController _playerController;
        private TriggerSystem _triggerSystem;
        private HeartbeatSystem _heartbeatSystem;
        private ModuleTransitionSystem _transitionSystem;

        // UI
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

            SceneSystem.SceneInstance.RootScene.Entities.Add(_cameraEntity);

            Console.WriteLine("[Odyssey] Camera initialized at " + _cameraEntity.Transform.Position);
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

            var canvas = new Canvas();
            var page = new UIPage { RootElement = canvas };
            _uiComponent.Page = page;

            SceneSystem.SceneInstance.RootScene.Entities.Add(uiEntity);

            // Try to load font
            try
            {
                _font = Content.Load<SpriteFont>("DefaultFont");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] Warning: DefaultFont not found: " + ex.Message);
                _font = null;
            }

            if (_font != null)
            {
                _uiAvailable = true;
                _hud = new BasicHUD(_uiComponent, _font);
                _pauseMenu = new PauseMenu(_uiComponent, _font);
                _loadingScreen = new LoadingScreen(_uiComponent, _font);
                _dialoguePanel = new DialoguePanel(_uiComponent, _font);

                _pauseMenu.OnResume += OnResumeGame;
                _pauseMenu.OnExit += OnExitGame;
                _dialoguePanel.OnReplySelected += OnDialogueReplySelected;
                _dialoguePanel.OnSkipRequested += OnDialogueSkip;

                Console.WriteLine("[Odyssey] UI initialized with font");
            }
            else
            {
                _uiAvailable = false;
                Console.WriteLine("[Odyssey] Running without UI (no font available)");

                // Create simple debug text without font
                _debugTextBlock = new TextBlock
                {
                    TextSize = 14,
                    TextColor = Color.Yellow,
                    Margin = new Thickness(10, 10, 0, 0)
                };
                canvas.Children.Add(_debugTextBlock);
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
            SceneSystem.SceneInstance.RootScene.Entities.Add(_sceneBuilder.RootEntity);

            Console.WriteLine("[Odyssey] SceneBuilder initialized");
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

            InitializeUI();
            InitializeSceneBuilder();

            if (_loadingScreen != null) _loadingScreen.Show("Starting Game");

            try
            {
                _session.StartNewGame();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Odyssey] Failed to start: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float deltaTime = (float)gameTime.Elapsed.TotalSeconds;

            ProcessInput(deltaTime);

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
            // Clear with a dark space-ish blue
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.BackBuffer, new Color4(0.02f, 0.02f, 0.08f, 1f));
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);
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
