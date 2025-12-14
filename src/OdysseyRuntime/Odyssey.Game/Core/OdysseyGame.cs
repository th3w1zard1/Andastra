using System;
using System.Collections.Generic;
using StrideEngine = Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Rendering;
using Stride.UI;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.VM;
using Odyssey.Kotor.Game;
using Odyssey.Kotor.Input;
using Odyssey.Kotor.Systems;
using Odyssey.Kotor.Dialogue;
using Odyssey.Stride.Camera;
using Odyssey.Stride.Scene;
using Odyssey.Stride.UI;

namespace Odyssey.Game.Core
{
    /// <summary>
    /// Main game class - Stride integration for Odyssey Engine.
    /// </summary>
    public class OdysseyGame : StrideEngine.Game
    {
        private readonly GameSettings _settings;
        private GameSession _session;
        private World _world;
        private ScriptGlobals _globals;
        private K1EngineApi _engineApi;
        private NcsVm _vm;

        // Systems
        private ChaseCamera _chaseCamera;
        private PlayerController _playerController;
        private TriggerSystem _triggerSystem;
        private HeartbeatSystem _heartbeatSystem;
        private ModuleTransitionSystem _transitionSystem;

        // UI
        private BasicHUD _hud;
        private PauseMenu _pauseMenu;
        private LoadingScreen _loadingScreen;
        private DialoguePanel _dialoguePanel;
        private UIComponent _uiComponent;

        // Scene
        private SceneBuilder _sceneBuilder;

        // Debug rendering
        private bool _showDebugInfo = false;
        private bool _isPaused = false;
        private bool _inDialogue = false;

        public OdysseyGame(GameSettings settings)
        {
            _settings = settings;
        }

        protected override void Initialize()
        {
            base.Initialize();

            // Set window title
            Window.Title = "Odyssey Engine - " + (_settings.Game == KotorGame.K1 ? "Knights of the Old Republic" : "The Sith Lords");

            // Initialize core systems
            _world = new World();
            _globals = new ScriptGlobals();
            _engineApi = new K1EngineApi();
            _vm = new NcsVm();

            // Create game session
            _session = new GameSession(_settings, _world, _vm, _globals);

            // Initialize systems
            InitializeSystems();

            Console.WriteLine("[Odyssey] Core systems initialized");
        }

        private void InitializeSystems()
        {
            // Trigger system
            _triggerSystem = new TriggerSystem(_world, FireScriptEvent);

            // Heartbeat system
            _heartbeatSystem = new HeartbeatSystem(_world, FireScriptEvent);

            // Module transition system
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

            // Subscribe to session events
            _session.OnModuleLoaded += OnModuleLoaded;
        }

        private void InitializeUI()
        {
            // Create UI entity and component
            var uiEntity = new Entity("UI");
            _uiComponent = new UIComponent();
            uiEntity.Add(_uiComponent);

            // Create UI page with Canvas root
            var page = new UIPage { RootElement = new Stride.UI.Panels.Canvas() };
            _uiComponent.Page = page;

            // Add to scene
            SceneSystem.SceneInstance.RootScene.Entities.Add(uiEntity);

            // Create font (using default)
            var font = Content.Load<SpriteFont>("DefaultFont");
            if (font == null)
            {
                Console.WriteLine("[Odyssey] Warning: DefaultFont not found, UI will have limited functionality");
                return;
            }

            // Create UI components
            _hud = new BasicHUD(_uiComponent, font);
            _pauseMenu = new PauseMenu(_uiComponent, font);
            _loadingScreen = new LoadingScreen(_uiComponent, font);
            _dialoguePanel = new DialoguePanel(_uiComponent, font);

            // Wire up events
            _pauseMenu.OnResume += OnResumeGame;
            _pauseMenu.OnExit += OnExitGame;
            _dialoguePanel.OnReplySelected += OnDialogueReplySelected;
            _dialoguePanel.OnSkipRequested += OnDialogueSkip;
        }

        private void FireScriptEvent(Core.Interfaces.IEntity entity, Core.Enums.ScriptEvent scriptEvent, Core.Interfaces.IEntity triggeredBy)
        {
            // Get script resref from entity
            var scriptHooks = entity.GetComponent<Core.Interfaces.Components.IScriptHooksComponent>();
            if (scriptHooks == null)
            {
                return;
            }

            string scriptResRef = scriptHooks.GetScript(scriptEvent);
            if (string.IsNullOrEmpty(scriptResRef))
            {
                return;
            }

            Console.WriteLine("[Script] Firing " + scriptEvent + " on " + entity.Tag + ": " + scriptResRef);

            // Load and execute script
            try
            {
                byte[] scriptData = _session.LoadScript(scriptResRef);
                if (scriptData != null && scriptData.Length > 0)
                {
                    // Execute script with entity as caller
                    _vm.Execute(scriptData, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Script] Error executing " + scriptResRef + ": " + ex.Message);
            }
        }

        private void PositionPlayerAtWaypoint(Core.Interfaces.IEntity player, string waypointTag)
        {
            if (player == null || string.IsNullOrEmpty(waypointTag))
            {
                return;
            }

            var waypoint = _world.GetEntityByTag(waypointTag);
            if (waypoint == null)
            {
                Console.WriteLine("[Odyssey] Waypoint not found: " + waypointTag);
                return;
            }

            var waypointTransform = waypoint.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            var playerTransform = player.GetComponent<Core.Interfaces.Components.ITransformComponent>();

            if (waypointTransform != null && playerTransform != null)
            {
                playerTransform.Position = waypointTransform.Position;
                playerTransform.Facing = waypointTransform.Facing;
            }
        }

        protected override void BeginRun()
        {
            base.BeginRun();

            // Initialize UI (must be after scene is ready)
            InitializeUI();

            // Show loading screen
            if (_loadingScreen != null)
            {
                _loadingScreen.Show("Starting Game");
            }

            // Load initial module
            try
            {
                _session.StartNewGame();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("[Odyssey] Failed to start game: " + ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float deltaTime = (float)gameTime.Elapsed.TotalSeconds;

            // Handle input
            ProcessInput(deltaTime);

            // Skip updates if paused (but still process pause menu)
            if (_isPaused)
            {
                return;
            }

            // Skip updates during transitions
            if (_transitionSystem != null && _transitionSystem.IsTransitioning)
            {
                return;
            }

            // Update game session
            if (_session != null)
            {
                _session.Update(deltaTime);
            }

            // Update world
            if (_world != null)
            {
                _world.Update(deltaTime);
            }

            // Update player controller
            if (_playerController != null && !_inDialogue)
            {
                _playerController.Update(deltaTime);
            }

            // Update trigger system
            if (_triggerSystem != null)
            {
                _triggerSystem.Update();
            }

            // Update heartbeat system
            if (_heartbeatSystem != null)
            {
                _heartbeatSystem.Update(deltaTime);
            }

            // Update dialogue
            if (_session.DialogueManager != null)
            {
                _session.DialogueManager.Update(deltaTime);
            }

            // Update HUD
            UpdateHUD();
        }

        private void ProcessInput(float deltaTime)
        {
            // Toggle debug info
            if (Input.IsKeyPressed(Keys.F1))
            {
                _showDebugInfo = !_showDebugInfo;
                if (_hud != null)
                {
                    _hud.ShowDebug = _showDebugInfo;
                }
            }

            // Pause/ESC handling
            if (Input.IsKeyPressed(Keys.Escape))
            {
                if (_inDialogue)
                {
                    // Skip dialogue
                    _session.DialogueManager?.SkipNode();
                }
                else if (_isPaused)
                {
                    // Resume
                    OnResumeGame();
                }
                else
                {
                    // Show pause menu
                    _isPaused = true;
                    if (_pauseMenu != null)
                    {
                        _pauseMenu.IsVisible = true;
                    }
                }
                return;
            }

            // Handle pause menu input
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

            // Handle dialogue input
            if (_inDialogue && _dialoguePanel != null)
            {
                _dialoguePanel.HandleInput(
                    Input.IsKeyPressed(Keys.Up) || Input.IsKeyPressed(Keys.W),
                    Input.IsKeyPressed(Keys.Down) || Input.IsKeyPressed(Keys.S),
                    Input.IsKeyPressed(Keys.Enter) || Input.IsKeyPressed(Keys.Space),
                    Input.IsKeyPressed(Keys.Escape)
                );

                // Number keys for quick reply selection
                for (int i = 1; i <= 9; i++)
                {
                    if (Input.IsKeyPressed((Keys)((int)Keys.D1 + i - 1)))
                    {
                        _dialoguePanel.HandleNumberKey(i);
                    }
                }
                return;
            }

            // Camera rotation with mouse (right-click drag)
            if (_chaseCamera != null)
            {
                if (Input.IsMouseButtonDown(MouseButton.Right))
                {
                    _chaseCamera.AdjustYaw(-Input.MouseDelta.X * 0.005f);
                }

                // Mouse wheel for zoom
                if (Math.Abs(Input.MouseWheelDelta) > 0.01f)
                {
                    _chaseCamera.AdjustDistance(-Input.MouseWheelDelta * 0.5f);
                }

                // Update camera
                _chaseCamera.Update(deltaTime);
            }

            // Player click-to-move
            if (Input.IsMouseButtonPressed(MouseButton.Left) && _playerController != null)
            {
                // Get screen position normalized
                float screenX = Input.MousePosition.X / Window.ClientBounds.Width;
                float screenY = Input.MousePosition.Y / Window.ClientBounds.Height;

                System.Numerics.Vector3 worldPos;
                if (_playerController.ScreenToWorld(
                    screenX,
                    screenY,
                    GetViewMatrix(),
                    GetProjectionMatrix(),
                    out worldPos))
                {
                    bool run = !Input.IsKeyDown(Keys.LeftShift);
                    _playerController.MoveToPosition(worldPos, run);
                }
            }

            // Interact key
            if (Input.IsKeyPressed(Keys.Space))
            {
                TryInteractWithNearestObject();
            }
        }

        private void UpdateHUD()
        {
            if (_hud == null)
            {
                return;
            }

            // Get player stats
            var player = _session.PlayerEntity;
            if (player != null)
            {
                var stats = player.GetComponent<Core.Interfaces.Components.IStatsComponent>();
                if (stats != null)
                {
                    _hud.SetHealth(stats.CurrentHitPoints, stats.MaxHitPoints);
                    _hud.SetForcePoints(stats.CurrentForcePoints, stats.MaxForcePoints);
                }
            }

            // Update debug text
            if (_showDebugInfo)
            {
                string debug = "Module: " + (_session.CurrentModuleName ?? "none");
                debug += "\nEntities: " + _world.EntityCount;
                if (player != null)
                {
                    var transform = player.GetComponent<Core.Interfaces.Components.ITransformComponent>();
                    if (transform != null)
                    {
                        debug += "\nPos: " + transform.Position;
                    }
                }
                _hud.SetDebugText(debug);
            }
        }

        private void TryInteractWithNearestObject()
        {
            // Find nearest interactable
            var player = _session.PlayerEntity;
            if (player == null)
            {
                return;
            }

            var playerTransform = player.GetComponent<Core.Interfaces.Components.ITransformComponent>();
            if (playerTransform == null)
            {
                return;
            }

            float nearestDist = 3.0f; // Interaction range
            Core.Interfaces.IEntity nearestEntity = null;

            // Check doors
            foreach (var door in _world.GetEntitiesByType(Core.Enums.ObjectType.Door) ?? Array.Empty<Core.Interfaces.IEntity>())
            {
                var doorTransform = door.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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

            // Check placeables
            foreach (var placeable in _world.GetEntitiesByType(Core.Enums.ObjectType.Placeable) ?? Array.Empty<Core.Interfaces.IEntity>())
            {
                var placeableTransform = placeable.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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

            // Check creatures (for dialogue)
            foreach (var creature in _world.GetEntitiesByType(Core.Enums.ObjectType.Creature) ?? Array.Empty<Core.Interfaces.IEntity>())
            {
                if (creature == player)
                {
                    continue;
                }

                var creatureTransform = creature.GetComponent<Core.Interfaces.Components.ITransformComponent>();
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

        private void InteractWith(Core.Interfaces.IEntity entity)
        {
            Console.WriteLine("[Interact] " + entity.ObjectType + ": " + entity.Tag);

            switch (entity.ObjectType)
            {
                case Core.Enums.ObjectType.Door:
                    InteractWithDoor(entity);
                    break;

                case Core.Enums.ObjectType.Placeable:
                    InteractWithPlaceable(entity);
                    break;

                case Core.Enums.ObjectType.Creature:
                    InteractWithCreature(entity);
                    break;
            }
        }

        private void InteractWithDoor(Core.Interfaces.IEntity door)
        {
            var doorComponent = door.GetComponent<Core.Interfaces.Components.IDoorComponent>();
            if (doorComponent == null)
            {
                return;
            }

            // Check for transition
            if (_transitionSystem != null && _transitionSystem.CanDoorTransition(door))
            {
                _transitionSystem.TransitionThroughDoor(door, _session.PlayerEntity);
                return;
            }

            // Check if locked
            if (doorComponent.IsLocked)
            {
                Console.WriteLine("[Door] Locked!");
                // TODO: Play locked sound
                FireScriptEvent(door, Core.Enums.ScriptEvent.OnFailedOpen, _session.PlayerEntity);
                return;
            }

            // Open/close door
            if (doorComponent.IsOpen)
            {
                doorComponent.Close();
                FireScriptEvent(door, Core.Enums.ScriptEvent.OnClose, _session.PlayerEntity);
            }
            else
            {
                doorComponent.Open();
                FireScriptEvent(door, Core.Enums.ScriptEvent.OnOpen, _session.PlayerEntity);
            }
        }

        private void InteractWithPlaceable(Core.Interfaces.IEntity placeable)
        {
            FireScriptEvent(placeable, Core.Enums.ScriptEvent.OnUsed, _session.PlayerEntity);
        }

        private void InteractWithCreature(Core.Interfaces.IEntity creature)
        {
            // Try to start conversation
            var scriptHooks = creature.GetComponent<Core.Interfaces.Components.IScriptHooksComponent>();
            if (scriptHooks != null)
            {
                string conversation = scriptHooks.GetLocalString("Conversation");
                if (!string.IsNullOrEmpty(conversation))
                {
                    StartConversation(creature, conversation);
                    return;
                }
            }

            // Fire OnDialogue script
            FireScriptEvent(creature, Core.Enums.ScriptEvent.OnDialogue, _session.PlayerEntity);
        }

        private void StartConversation(Core.Interfaces.IEntity npc, string dialogueResRef)
        {
            if (_session.DialogueManager == null)
            {
                return;
            }

            bool started = _session.DialogueManager.StartConversation(dialogueResRef, npc, _session.PlayerEntity);
            if (started)
            {
                _inDialogue = true;
                if (_hud != null)
                {
                    _hud.IsVisible = false;
                }
            }
        }

        private System.Numerics.Matrix4x4 GetViewMatrix()
        {
            // Get from chase camera or default
            // Placeholder - would need proper camera integration
            return System.Numerics.Matrix4x4.Identity;
        }

        private System.Numerics.Matrix4x4 GetProjectionMatrix()
        {
            // Placeholder
            return System.Numerics.Matrix4x4.Identity;
        }

        #region Event Handlers

        private void OnModuleLoaded(object sender, ModuleLoadedEventArgs e)
        {
            Console.WriteLine("[Odyssey] Module loaded: " + e.ModuleName);

            // Hide loading screen
            if (_loadingScreen != null)
            {
                _loadingScreen.Hide();
            }

            // Initialize player controller with navmesh
            if (_session.PlayerEntity != null && _session.NavigationMesh != null)
            {
                _playerController = new PlayerController(_session.PlayerEntity, _session.NavigationMesh);
            }

            // Initialize chase camera
            // Would need camera entity from scene

            // Register entities for heartbeat
            if (_heartbeatSystem != null)
            {
                _heartbeatSystem.Clear();
                _heartbeatSystem.RegisterAllEntities();
            }

            // Show HUD
            if (_hud != null)
            {
                _hud.IsVisible = true;
            }
        }

        private void OnTransitionStart(object sender, ModuleTransitionEventArgs e)
        {
            Console.WriteLine("[Odyssey] Transition starting to: " + e.TargetModule);

            // Show loading screen
            if (_loadingScreen != null)
            {
                _loadingScreen.Show(e.TargetModule);
            }

            // Hide HUD
            if (_hud != null)
            {
                _hud.IsVisible = false;
            }
        }

        private void OnTransitionComplete(object sender, ModuleTransitionEventArgs e)
        {
            Console.WriteLine("[Odyssey] Transition complete to: " + e.TargetModule);

            // Position player at waypoint
            if (!string.IsNullOrEmpty(e.TargetWaypoint))
            {
                PositionPlayerAtWaypoint(_session.PlayerEntity, e.TargetWaypoint);
            }

            // Hide loading screen
            if (_loadingScreen != null)
            {
                _loadingScreen.Hide();
            }

            // Show HUD
            if (_hud != null)
            {
                _hud.IsVisible = true;
            }
        }

        private void OnTransitionFailed(object sender, ModuleTransitionEventArgs e)
        {
            Console.WriteLine("[Odyssey] Transition failed to: " + e.TargetModule);

            // Hide loading screen
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

        private void OnDialogueEnded()
        {
            _inDialogue = false;
            if (_dialoguePanel != null)
            {
                _dialoguePanel.Hide();
            }
            if (_hud != null)
            {
                _hud.IsVisible = true;
            }
        }

        #endregion

        protected override void Draw(GameTime gameTime)
        {
            // Clear to dark blue (space/KOTOR feel)
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.BackBuffer, new Color4(0.02f, 0.02f, 0.08f, 1f));
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);

            // Scene rendering is handled by Stride's built-in renderer
            // Our SceneBuilder adds entities to the scene which Stride renders

            base.Draw(gameTime);
        }

        protected override void Destroy()
        {
            // Unsubscribe events
            if (_transitionSystem != null)
            {
                _transitionSystem.OnTransitionStart -= OnTransitionStart;
                _transitionSystem.OnTransitionComplete -= OnTransitionComplete;
                _transitionSystem.OnTransitionFailed -= OnTransitionFailed;
            }

            if (_session != null)
            {
                _session.OnModuleLoaded -= OnModuleLoaded;
                _session.Dispose();
                _session = null;
            }

            _triggerSystem?.Clear();
            _heartbeatSystem?.Clear();

            base.Destroy();
        }
    }

    /// <summary>
    /// Event arguments for module loaded events.
    /// </summary>
    public class ModuleLoadedEventArgs : EventArgs
    {
        public string ModuleName { get; set; }
    }
}

