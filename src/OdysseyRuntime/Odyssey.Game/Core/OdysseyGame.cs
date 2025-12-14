using System;
using System.Collections.Generic;
using StrideEngine = Stride.Engine;
using Stride.Games;
using Stride.Graphics;
using Stride.Input;
using Stride.Core.Mathematics;
using Odyssey.Core.Entities;
using Odyssey.Core.Interfaces;
using Odyssey.Scripting.EngineApi;
using Odyssey.Scripting.VM;
using Odyssey.Kotor.Game;

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

        // TODO: Add proper camera system
        private Vector3 _cameraPosition = new Vector3(0, 5, -10);
        private float _cameraYaw = 0;
        private float _cameraPitch = 0.3f;

        // Debug rendering
        private bool _showDebugInfo = false;

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

            Console.WriteLine("[Odyssey] Core systems initialized");
        }

        protected override void BeginRun()
        {
            base.BeginRun();

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

            // Toggle debug info
            if (Input.IsKeyPressed(Keys.F1))
            {
                _showDebugInfo = !_showDebugInfo;
            }

            // Quick exit
            if (Input.IsKeyPressed(Keys.Escape))
            {
                Exit();
            }
        }

        private void ProcessInput(float deltaTime)
        {
            // TODO: Replace with proper PlayerController

            // Camera rotation with mouse
            if (Input.IsMouseButtonDown(MouseButton.Right))
            {
                _cameraYaw -= Input.MouseDelta.X * 0.003f;
                _cameraPitch -= Input.MouseDelta.Y * 0.003f;
                _cameraPitch = MathUtil.Clamp(_cameraPitch, -1.4f, 1.4f);
            }

            // Camera movement with WASD
            float moveSpeed = 10f * deltaTime;
            if (Input.IsKeyDown(Keys.LeftShift))
            {
                moveSpeed *= 3f;
            }

            Vector3 forward = new Vector3(
                (float)Math.Sin(_cameraYaw),
                0,
                (float)Math.Cos(_cameraYaw)
            );
            Vector3 right = new Vector3(forward.Z, 0, -forward.X);

            if (Input.IsKeyDown(Keys.W))
            {
                _cameraPosition += forward * moveSpeed;
            }
            if (Input.IsKeyDown(Keys.S))
            {
                _cameraPosition -= forward * moveSpeed;
            }
            if (Input.IsKeyDown(Keys.A))
            {
                _cameraPosition -= right * moveSpeed;
            }
            if (Input.IsKeyDown(Keys.D))
            {
                _cameraPosition += right * moveSpeed;
            }
            if (Input.IsKeyDown(Keys.Q))
            {
                _cameraPosition.Y -= moveSpeed;
            }
            if (Input.IsKeyDown(Keys.E))
            {
                _cameraPosition.Y += moveSpeed;
            }

            // Player movement (click to move)
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                // TODO: Raycast to walkmesh and pathfind
                Console.WriteLine("[Input] Click at: " + Input.MousePosition);
            }

            // Interact key
            if (Input.IsKeyPressed(Keys.Space))
            {
                // TODO: Interact with nearest object
                Console.WriteLine("[Input] Interact");
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            // Clear to dark blue (space/KOTOR feel)
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.BackBuffer, new Color4(0.02f, 0.02f, 0.08f, 1f));
            GraphicsContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);

            // TODO: Render loaded module geometry
            // TODO: Render entities (characters, placeables, doors)
            // TODO: Render UI (dialogue, menus)

            // Draw debug info
            if (_showDebugInfo && _session != null)
            {
                DrawDebugInfo();
            }

            base.Draw(gameTime);
        }

        private void DrawDebugInfo()
        {
            // TODO: Use SpriteBatch for debug text
            // For now just console output
        }

        protected override void Destroy()
        {
            if (_session != null)
            {
                _session.Dispose();
                _session = null;
            }

            base.Destroy();
        }
    }
}

