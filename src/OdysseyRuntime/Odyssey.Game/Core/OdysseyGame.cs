using System;
using System.Collections.Generic;
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
using CSharpKOTOR.Common;
using CSharpKOTOR.Installation;
using CSharpKOTOR.Resources;
using CSharpKOTOR.Formats.MDL;
using Game = Microsoft.Xna.Framework.Game;
using Matrix = Microsoft.Xna.Framework.Matrix;
using Vector4 = Microsoft.Xna.Framework.Vector4;
using Vector3 = Microsoft.Xna.Framework.Vector3;
using Vector2 = Microsoft.Xna.Framework.Vector2;
using Color = Microsoft.Xna.Framework.Color;

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

        // Basic 3D rendering
        private BasicEffect _basicEffect;
        private VertexBuffer _groundVertexBuffer;
        private IndexBuffer _groundIndexBuffer;
        private Matrix _viewMatrix;
        private Matrix _projectionMatrix;
        private float _cameraAngle = 0f;

        // Room rendering
        private Odyssey.MonoGame.Converters.RoomMeshRenderer _roomRenderer;
        private Dictionary<string, Odyssey.MonoGame.Converters.RoomMeshData> _roomMeshes;

        // Input tracking
        private Microsoft.Xna.Framework.Input.MouseState _previousMouseState;

        public OdysseyGame(GameSettings settings)
        {
            _settings = settings;
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            // Set window title
            Window.Title = "Odyssey Engine - " + (_settings.Game == KotorGame.K1 ? "Knights of the Old Republic" : "The Sith Lords");

            Console.WriteLine("[Odyssey] Game window initialized - IsMouseVisible: " + IsMouseVisible);
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

            // Initialize input state
            _previousMouseState = Microsoft.Xna.Framework.Input.Mouse.GetState();

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

            // Initialize game rendering
            InitializeGameRendering();

            // Initialize room renderer
            _roomRenderer = new Odyssey.MonoGame.Converters.RoomMeshRenderer(GraphicsDevice);
            _roomMeshes = new Dictionary<string, Odyssey.MonoGame.Converters.RoomMeshData>();

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
                // Update game session
                if (_session != null)
                {
                    float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
                    _session.Update(deltaTime);
                }

                // Update camera to follow player
                UpdateCamera();
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
                DrawGameWorld(gameTime);
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

        private void InitializeGameRendering()
        {
            try
            {
                // Initialize basic 3D effect
                _basicEffect = new BasicEffect(GraphicsDevice);
                _basicEffect.VertexColorEnabled = true;
                _basicEffect.LightingEnabled = false;

                // Create a simple ground plane
                CreateGroundPlane();

                // Initialize camera
                UpdateCamera();

                Console.WriteLine("[Odyssey] Game rendering initialized");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[Odyssey] WARNING: Failed to initialize game rendering: " + ex.Message);
            }
        }

        private void CreateGroundPlane()
        {
            // Create a simple 10x10 ground plane
            var vertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(-5, 0, -5), Color.Gray),
                new VertexPositionColor(new Vector3(5, 0, -5), Color.Gray),
                new VertexPositionColor(new Vector3(5, 0, 5), Color.Gray),
                new VertexPositionColor(new Vector3(-5, 0, 5), Color.Gray)
            };

            var indices = new short[]
            {
                0, 1, 2,
                0, 2, 3
            };

            _groundVertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), vertices.Length, BufferUsage.WriteOnly);
            _groundVertexBuffer.SetData(vertices);

            _groundIndexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, indices.Length, BufferUsage.WriteOnly);
            _groundIndexBuffer.SetData(indices);
        }

        private void UpdateCamera()
        {
            Microsoft.Xna.Framework.Vector3 target = new Microsoft.Xna.Framework.Vector3(0, 0, 0);
            Microsoft.Xna.Framework.Vector3 cameraPosition;
            Microsoft.Xna.Framework.Vector3 up = new Microsoft.Xna.Framework.Vector3(0, 1, 0);

            // Try to follow player if available
            if (_session != null && _session.PlayerEntity != null)
            {
                var transform = _session.PlayerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
                if (transform != null)
                {
                    target = new Microsoft.Xna.Framework.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);

                    // Camera follows behind and above player
                    float cameraDistance = 8f;
                    float cameraHeight = 4f;
                    float cameraAngle = transform.Facing + (float)Math.PI; // Behind player

                    cameraPosition = new Vector3(
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
                    cameraPosition = new Vector3(
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
                cameraPosition = new Vector3(
                    (float)Math.Sin(_cameraAngle) * distance,
                    height,
                    (float)Math.Cos(_cameraAngle) * distance
                );
            }

            _viewMatrix = Matrix.CreateLookAt(cameraPosition, target, up);

            float aspectRatio = (float)GraphicsDevice.Viewport.Width / GraphicsDevice.Viewport.Height;
            _projectionMatrix = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(60f),
                aspectRatio,
                0.1f,
                100f
            );
        }

        private void DrawGameWorld(GameTime gameTime)
        {
            // Clear with a sky color
            GraphicsDevice.Clear(new Color(135, 206, 250, 255)); // Sky blue

            // Draw 3D scene
            if (_groundVertexBuffer != null && _groundIndexBuffer != null && _basicEffect != null)
            {
                GraphicsDevice.SetVertexBuffer(_groundVertexBuffer);
                GraphicsDevice.Indices = _groundIndexBuffer;

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = Matrix.Identity;

                foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        2 // 2 triangles
                    );
                }
            }

            // Draw loaded area rooms if available
            if (_session != null && _session.CurrentRuntimeModule != null)
            {
                var entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
                if (entryArea != null && entryArea is Odyssey.Core.Module.RuntimeArea runtimeArea)
                {
                    DrawAreaRooms(runtimeArea);
                }
            }

            // Draw entities from GIT
            if (_session != null && _session.CurrentRuntimeModule != null)
            {
                var entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
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
            string statusText = "Game Running - Press ESC to return to menu";
            if (_session != null && _session.CurrentModuleName != null)
            {
                statusText = "Module: " + _session.CurrentModuleName + " - Press ESC to return to menu";
                var entryArea = _session.CurrentRuntimeModule?.GetArea(_session.CurrentRuntimeModule.EntryArea);
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
            // If no font, we just skip text rendering - the 3D scene is still visible
            _spriteBatch.End();
        }

        private void DrawAreaRooms(Odyssey.Core.Module.RuntimeArea area)
        {
            if (area.Rooms == null || area.Rooms.Count == 0 || _basicEffect == null || _roomRenderer == null)
            {
                return;
            }

            // Load and render room meshes
            foreach (var room in area.Rooms)
            {
                if (string.IsNullOrEmpty(room.ModelName))
                {
                    continue;
                }

                // Get or load room mesh
                Odyssey.MonoGame.Converters.RoomMeshData meshData;
                if (!_roomMeshes.TryGetValue(room.ModelName, out meshData))
                {
                    // Try to load actual MDL model from module resources
                    CSharpKOTOR.Formats.MDLData.MDL mdl = null;
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
                    continue;
                }

                // Set up transform
                var roomPos = new Vector3(room.Position.X, room.Position.Y, room.Position.Z);
                var roomWorld = Matrix.CreateTranslation(roomPos);

                // Set up rendering state
                GraphicsDevice.SetVertexBuffer(meshData.VertexBuffer);
                GraphicsDevice.Indices = meshData.IndexBuffer;

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = roomWorld;
                _basicEffect.VertexColorEnabled = true;

                // Draw the mesh
                foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
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
            if (area == null || _basicEffect == null)
            {
                return;
            }

            // Draw all entities as simple colored boxes
            foreach (var entity in area.GetAllEntities())
            {
                DrawEntity(entity);
            }
        }

        /// <summary>
        /// Draws a single entity as a simple representation.
        /// </summary>
        private void DrawEntity(Odyssey.Core.Interfaces.IEntity entity)
        {
            if (entity == null || _basicEffect == null)
            {
                return;
            }

            var transform = entity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Choose color based on entity type
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

            var entityPos = new Microsoft.Xna.Framework.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
            var entityWorld = Matrix.CreateTranslation(entityPos);

            // Create a simple box for the entity
            float hw = entityWidth * 0.5f;
            var entityVertices = new VertexPositionColor[]
            {
                // Bottom face
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-hw, -hw, 0), entityColor),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(hw, -hw, 0), entityColor),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(hw, hw, 0), entityColor),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-hw, hw, 0), entityColor),
                // Top face
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-hw, -hw, entityHeight), entityColor),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(hw, -hw, entityHeight), entityColor),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(hw, hw, entityHeight), entityColor),
                new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(-hw, hw, entityHeight), entityColor)
            };

            var entityIndices = new short[]
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

            // Create temporary buffers for entity
            using (var entityVb = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), entityVertices.Length, BufferUsage.WriteOnly))
            using (var entityIb = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, entityIndices.Length, BufferUsage.WriteOnly))
            {
                entityVb.SetData(entityVertices);
                entityIb.SetData(entityIndices);

                GraphicsDevice.SetVertexBuffer(entityVb);
                GraphicsDevice.Indices = entityIb;

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = entityWorld;
                _basicEffect.VertexColorEnabled = true;

                foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        entityIndices.Length / 3
                    );
                }
            }
        }

        /// <summary>
        /// Draws the player entity as a simple representation.
        /// </summary>
        private void DrawPlayerEntity(Odyssey.Core.Interfaces.IEntity playerEntity)
        {
            if (_basicEffect == null)
            {
                return;
            }

            var transform = playerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
            if (transform == null)
            {
                return;
            }

            // Create a simple representation of the player (colored box)
            // TODO: Replace with actual player model rendering
            var playerPos = new Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
            var playerWorld = Matrix.CreateTranslation(playerPos);

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

            var playerIndices = new short[]
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

            // Create temporary buffers for player
            using (var playerVb = new VertexBuffer(GraphicsDevice, typeof(VertexPositionColor), playerVertices.Length, BufferUsage.WriteOnly))
            using (var playerIb = new IndexBuffer(GraphicsDevice, IndexElementSize.SixteenBits, playerIndices.Length, BufferUsage.WriteOnly))
            {
                playerVb.SetData(playerVertices);
                playerIb.SetData(playerIndices);

                GraphicsDevice.SetVertexBuffer(playerVb);
                GraphicsDevice.Indices = playerIb;

                _basicEffect.View = _viewMatrix;
                _basicEffect.Projection = _projectionMatrix;
                _basicEffect.World = playerWorld;
                _basicEffect.VertexColorEnabled = true;

                foreach (EffectPass pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        playerIndices.Length / 3
                    );
                }
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
        private CSharpKOTOR.Formats.MDLData.MDL LoadMDLModel(string modelResRef)
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
                var moduleName = _session.CurrentModuleName;
                if (string.IsNullOrEmpty(moduleName))
                {
                    return null;
                }

                // Create a temporary module to load the resource
                // Note: This is inefficient - we should cache the Module object
                var installation = new Installation(_settings.GamePath);
                var module = new Module(moduleName, installation);

                var mdlResource = module.Resource(modelResRef, ResourceType.MDL);
                if (mdlResource == null)
                {
                    return null;
                }

                var activePath = mdlResource.Activate();
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
        /// Handles player input for movement.
        /// </summary>
        private void HandlePlayerInput(KeyboardState keyboardState, Microsoft.Xna.Framework.Input.MouseState mouseState, GameTime gameTime)
        {
            if (_session == null || _session.PlayerEntity == null)
            {
                return;
            }

            var transform = _session.PlayerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
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
                var pos = transform.Position;
                pos.X += (float)Math.Sin(transform.Facing) * moveSpeed;
                pos.Z += (float)Math.Cos(transform.Facing) * moveSpeed;
                transform.Position = pos;
                moved = true;
            }
            if (keyboardState.IsKeyDown(Keys.S))
            {
                // Move backward
                var pos = transform.Position;
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
            if (mouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed &&
                _previousMouseState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released)
            {
                // Only trigger on click (not hold)
                // First check if we clicked on an entity
                Vector3 rayOrigin = GetCameraPosition();
                Vector3 rayDirection = GetMouseRayDirection(mouseState.X, mouseState.Y);

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
                        var nearest = navMesh.GetNearestPoint(hitPoint);
                        if (nearest.HasValue)
                        {
                            var targetPos = nearest.Value;
                            transform.Position = new System.Numerics.Vector3(targetPos.X, targetPos.Y, targetPos.Z);
                            // Face towards target
                            var dir = targetPos - new System.Numerics.Vector3(transform.Position.X, transform.Position.Y, transform.Position.Z);
                            if (dir.LengthSquared() > 0.01f)
                            {
                                transform.Facing = (float)Math.Atan2(dir.X, dir.Z);
                            }
                            moved = true;
                        }
                    }
                }
            }

            // Clamp player to walkmesh surface
            if (hasNavMesh && moved)
            {
                var pos = transform.Position;
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
                    var nearest = navMesh.GetNearestPoint(worldPos);
                    if (nearest.HasValue)
                    {
                        var nearestPos = nearest.Value;
                        transform.Position = new System.Numerics.Vector3(nearestPos.X, nearestPos.Y, nearestPos.Z);
                    }
                }
            }

            _previousMouseState = mouseState;
        }

        /// <summary>
        /// Gets the camera position for raycasting.
        /// </summary>
        private Microsoft.Xna.Framework.Vector3 GetCameraPosition()
        {
            if (_session != null && _session.PlayerEntity != null)
            {
                var transform = _session.PlayerEntity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
                if (transform != null)
                {
                    // Camera is behind and above player
                    float cameraDistance = 8f;
                    float cameraHeight = 4f;
                    float cameraAngle = transform.Facing + (float)Math.PI;

                    var playerPos = transform.Position;
                    return new Microsoft.Xna.Framework.Vector3(
                        playerPos.X + (float)Math.Sin(cameraAngle) * cameraDistance,
                        playerPos.Y + cameraHeight,
                        playerPos.Z + (float)Math.Cos(cameraAngle) * cameraDistance
                    );
                }
            }
            return Microsoft.Xna.Framework.Vector3.Zero;
        }

        /// <summary>
        /// Gets the ray direction from mouse position.
        /// </summary>
        private Microsoft.Xna.Framework.Vector3 GetMouseRayDirection(int mouseX, int mouseY)
        {
            // Convert mouse position to normalized device coordinates (-1 to 1)
            float x = (2.0f * mouseX / GraphicsDevice.Viewport.Width) - 1.0f;
            float y = 1.0f - (2.0f * mouseY / GraphicsDevice.Viewport.Height);

            // Create ray in view space
            Vector4 rayClip = new Vector4(x, y, -1.0f, 1.0f);

            // Transform to eye space
            Matrix invProjection = Matrix.Invert(_projectionMatrix);
            Vector4 rayEye = Vector4.Transform(rayClip, invProjection);
            rayEye = new Vector4(rayEye.X, rayEye.Y, -1.0f, 0.0f);

            // Transform to world space
            Matrix invView = Matrix.Invert(_viewMatrix);
            Vector4 rayWorld = Vector4.Transform(rayEye, invView);
            Vector3 rayDir = new Vector3(rayWorld.X, rayWorld.Y, rayWorld.Z);
            rayDir.Normalize();

            return rayDir;
        }

        /// <summary>
        /// Finds an entity at the given ray position.
        /// </summary>
        private Odyssey.Core.Interfaces.IEntity FindEntityAtRay(Vector3 rayOrigin, Vector3 rayDirection)
        {
            if (_session == null || _session.CurrentRuntimeModule == null)
            {
                return null;
            }

            var entryArea = _session.CurrentRuntimeModule.GetArea(_session.CurrentRuntimeModule.EntryArea);
            if (entryArea == null || !(entryArea is Odyssey.Core.Module.RuntimeArea runtimeArea))
            {
                return null;
            }

            // Simple AABB raycast for entities
            // For a quick demo, use bounding box intersection
            float closestDistance = float.MaxValue;
            Odyssey.Core.Interfaces.IEntity closestEntity = null;

            foreach (var entity in runtimeArea.GetAllEntities())
            {
                var transform = entity.GetComponent<Odyssey.Kotor.Components.TransformComponent>();
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
                var entityMin = entityPos - new Vector3(entitySize, entitySize, entitySize);
                var entityMax = entityPos + new Vector3(entitySize, entitySize, entitySize);

                // Simple ray-AABB intersection
                float tmin = 0.0f;
                float tmax = 1000.0f;

                for (int i = 0; i < 3; i++)
                {
                    float invD = 1.0f / rayDirection[i];
                    float t0 = (entityMin[i] - rayOrigin[i]) * invD;
                    float t1 = (entityMax[i] - rayOrigin[i]) * invD;
                    if (invD < 0.0f)
                    {
                        float temp = t0;
                        t0 = t1;
                        t1 = temp;
                    }
                    tmin = t0 > tmin ? t0 : tmin;
                    tmax = t1 < tmax ? t1 : tmax;
                    if (tmax < tmin)
                    {
                        break;
                    }
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
                    Console.WriteLine("[Odyssey] Creature clicked - would start dialogue or attack");
                    // TODO: Start dialogue or attack based on creature type
                    break;
                case Odyssey.Core.Enums.ObjectType.Door:
                    Console.WriteLine("[Odyssey] Door clicked - would open/close door");
                    // TODO: Open/close door
                    break;
                case Odyssey.Core.Enums.ObjectType.Placeable:
                    Console.WriteLine("[Odyssey] Placeable clicked - would interact with placeable");
                    // TODO: Interact with placeable (container, etc.)
                    break;
                case Odyssey.Core.Enums.ObjectType.Trigger:
                    Console.WriteLine("[Odyssey] Trigger clicked - would activate trigger");
                    // TODO: Activate trigger
                    break;
                default:
                    Console.WriteLine($"[Odyssey] Unknown entity type clicked: {entity.ObjectType}");
                    break;
            }
        }
    }
}

