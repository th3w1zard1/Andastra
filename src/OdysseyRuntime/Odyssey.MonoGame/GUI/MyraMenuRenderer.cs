using System;
using Stride.Rendering;
using Stride.Graphics;
using Stride.Engine;
using Stride.Rendering.Compositing;
using Stride.Games;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Input;
using RenderContext = Stride.Rendering.RenderContext;
using Myra.Graphics2D;

using Myra;
using Myra.Graphics2D.UI;

namespace Odyssey.MonoGame.GUI
{
    /// <summary>
    /// Myra UI-based menu renderer with actual text buttons.
    /// Based on Myra UI in Stride Engine Tutorial: https://github.com/rds1983/Myra/wiki/Using-Myra-in-Stride-Engine-Tutorial
    /// </summary>
    public class MyraMenuRenderer : SceneRendererBase, IIdentifiable
    {
        private Desktop _desktop;
        private bool _isVisible = true;

        // Button references for keyboard navigation
        private Button _startButton;
        private Button _optionsButton;
        private Button _exitButton;
        private int _selectedIndex = 0;

        // Input debouncing
        private float _keyRepeatTimer = 0f;
        private const float KeyRepeatDelay = 0.15f; // 150ms delay between key repeats
        private bool _upKeyWasDown = false;
        private bool _downKeyWasDown = false;
        private bool _enterKeyWasDown = false;
        private bool _spaceKeyWasDown = false;

        // Menu action callback
        public event EventHandler<int> MenuItemSelected;

        // Initialize renderer resources
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneRendererBase.html
        // InitializeCore() - Called once during initialization to set up renderer resources
        // Must call base.InitializeCore() to initialize base class functionality
        protected override void InitializeCore()
        {
            // Call base initialization to set up SceneRendererBase
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneRendererBase.html
            // base.InitializeCore() initializes the base SceneRendererBase class
            // This sets up GraphicsDevice and other base class resources
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/index.html
            base.InitializeCore();

            // Initialize Myra environment
            // Based on Myra API: MyraEnvironment.Game must be set to the Stride Game instance
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Core.IServiceRegistry.html
            // Services.GetService<T>() retrieves a service from the service registry
            // Method signature: T GetService<T>() where T : class
            // IGame interface represents the game instance
            // Source: https://github.com/rds1983/Myra/wiki/Using-Myra-in-Stride-Engine-Tutorial
            // Source: https://doc.stride3d.net/latest/en/manual/engine/services-and-dependency-injection.html
            MyraEnvironment.Game = (Game)this.Services.GetService<IGame>();

            // Create main menu grid layout
            // Based on Myra API: Grid provides flexible layout system
            // Source: https://github.com/rds1983/Myra/wiki/Grid
            var grid = new Grid
            {
                RowSpacing = 20,
                ColumnSpacing = 20
            };

            // Set up grid columns - single centered column
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Pixels, 400)); // Main menu width
            grid.ColumnsProportions.Add(new Proportion(ProportionType.Auto));

            // Set up grid rows - header + buttons + spacing
            grid.RowsProportions.Add(new Proportion(ProportionType.Auto));
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 100)); // Header space
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 60)); // Start Game button
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 20)); // Spacing
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 60)); // Options button
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 20)); // Spacing
            grid.RowsProportions.Add(new Proportion(ProportionType.Pixels, 60)); // Exit button
            grid.RowsProportions.Add(new Proportion(ProportionType.Fill)); // Fill remaining space

            // Title label
            // Based on Myra API: Label displays text
            // Source: https://github.com/rds1983/Myra/wiki/Label
            var titleLabel = new Label
            {
                Id = "title",
                Text = "ODYSSEY ENGINE"
            };
            // Use attached properties via extension methods or direct property access
            titleLabel.GridColumn = 1;
            titleLabel.GridRow = 1;
            grid.Widgets.Add(titleLabel);

            // Start Game button
            // Based on Myra API: Button with Click event handler
            // Source: https://github.com/rds1983/Myra/wiki/Button
            _startButton = new Button
            {
                Content = new Label
                {
                    Text = "Start Game"
                }
            };
            _startButton.GridColumn = 1;
            _startButton.GridRow = 2;
            _startButton.Click += (s, a) =>
            {
                Console.WriteLine("[MyraMenuRenderer] Start Game clicked");
                MenuItemSelected?.Invoke(this, 0);
            };
            grid.Widgets.Add(_startButton);

            // Options button
            _optionsButton = new Button
            {
                Content = new Label
                {
                    Text = "Options"
                }
            };
            _optionsButton.GridColumn = 1;
            _optionsButton.GridRow = 4;
            _optionsButton.Click += (s, a) =>
            {
                Console.WriteLine("[MyraMenuRenderer] Options clicked");
                MenuItemSelected?.Invoke(this, 1);
            };
            grid.Widgets.Add(_optionsButton);

            // Exit button
            _exitButton = new Button
            {
                Content = new Label
                {
                    Text = "Exit"
                }
            };
            _exitButton.GridColumn = 1;
            _exitButton.GridRow = 6;
            _exitButton.Click += (s, a) =>
            {
                Console.WriteLine("[MyraMenuRenderer] Exit clicked");
                MenuItemSelected?.Invoke(this, 2);
            };
            grid.Widgets.Add(_exitButton);

            // Create desktop with grid as root
            // Based on Myra API: Desktop is the root container for all UI widgets
            // Source: https://github.com/rds1983/Myra/wiki/Desktop
            _desktop = new Desktop
            {
                Root = grid
            };

            Console.WriteLine("[MyraMenuRenderer] Myra UI menu initialized with text buttons");
        }

        // Render the menu using Myra UI
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneRendererBase.html
        // DrawCore(RenderContext, RenderDrawContext) - Called each frame to render the scene
        // Method signature: protected virtual void DrawCore(RenderContext context, RenderDrawContext drawContext)
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.RenderContext.html
        // RenderContext contains scene information and rendering state
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.RenderDrawContext.html
        // RenderDrawContext provides graphics context and command list for drawing operations
        // Source: https://doc.stride3d.net/latest/en/manual/graphics/graphics-compositor/index.html
        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        {
            if (!_isVisible)
                return;

            // Clear depth buffer
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.CommandList.html
            // CommandList.Clear(Texture, ClearOptions) clears render targets and buffers
            // Method signature: void Clear(Texture texture, ClearOptions options)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.GraphicsPresenter.html
            // GraphicsPresenter.DepthStencilBuffer property gets the depth-stencil buffer texture
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.ClearOptions.html
            // DepthStencilClearOptions.DepthBuffer clears only the depth buffer (not stencil)
            // Source: https://doc.stride3d.net/latest/en/manual/graphics/low-level-api/index.html
            drawContext.CommandList.Clear(GraphicsDevice.Presenter.DepthStencilBuffer, DepthStencilClearOptions.DepthBuffer);

            // Render Myra UI
            // Based on Myra API: Desktop.Render() renders all UI widgets
            // Source: https://github.com/rds1983/Myra/wiki/Desktop
            _desktop.Render();
        }

        /// <summary>
        /// Updates menu state based on input.
        /// </summary>
        // Update menu based on input
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
        // InputManager provides access to input devices (keyboard, mouse, gamepad)
        // Source: https://doc.stride3d.net/latest/en/manual/input/index.html
        public void UpdateMenu(InputManager input, GameTime gameTime = null)
        {
            if (!_isVisible)
                return;

            // Get delta time for debouncing
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Games.GameTime.html
            // GameTime.Elapsed property gets the elapsed time since the last frame
            // TotalSeconds property gets the elapsed time in seconds as a double
            // Source: https://doc.stride3d.net/latest/en/manual/game-loop/index.html
            float deltaTime = gameTime != null ? (float)gameTime.Elapsed.TotalSeconds : 0.016f; // Default to ~60fps if no gameTime
            _keyRepeatTimer += deltaTime;

            // Handle keyboard navigation with debouncing
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsKeyPressed(Keys) checks if a key was just pressed this frame (returns true once per press)
            // IsKeyDown(Keys) checks if a key is currently held down (returns true while key is pressed)
            // Method signatures: bool IsKeyPressed(Keys key), bool IsKeyDown(Keys key)
            // Keys enum defines keyboard key codes (Up, Down, Enter, Space, etc.)
            // Source: https://doc.stride3d.net/latest/en/manual/input/keyboard.html
            bool upPressed = input.IsKeyPressed(Keys.Up);
            bool upDown = input.IsKeyDown(Keys.Up);
            bool downPressed = input.IsKeyPressed(Keys.Down);
            bool downDown = input.IsKeyDown(Keys.Down);
            bool enterPressed = input.IsKeyPressed(Keys.Enter);
            bool enterDown = input.IsKeyDown(Keys.Enter);
            bool spacePressed = input.IsKeyPressed(Keys.Space);
            bool spaceDown = input.IsKeyDown(Keys.Space);

            // Handle Up key - move selection up
            if (upPressed || (upDown && _upKeyWasDown && _keyRepeatTimer >= KeyRepeatDelay))
            {
                if (upPressed || _keyRepeatTimer >= KeyRepeatDelay)
                {
                    _selectedIndex = (_selectedIndex - 1 + 3) % 3; // 3 buttons: Start, Options, Exit
                    Console.WriteLine($"[MyraMenuRenderer] Selected index: {_selectedIndex}");
                    _keyRepeatTimer = 0f; // Reset timer on selection change
                }
                _upKeyWasDown = true;
            }
            else
            {
                _upKeyWasDown = false;
            }

            // Handle Down key - move selection down
            if (downPressed || (downDown && _downKeyWasDown && _keyRepeatTimer >= KeyRepeatDelay))
            {
                if (downPressed || _keyRepeatTimer >= KeyRepeatDelay)
                {
                    _selectedIndex = (_selectedIndex + 1) % 3; // 3 buttons: Start, Options, Exit
                    Console.WriteLine($"[MyraMenuRenderer] Selected index: {_selectedIndex}");
                    _keyRepeatTimer = 0f; // Reset timer on selection change
                }
                _downKeyWasDown = true;
            }
            else
            {
                _downKeyWasDown = false;
            }

            // Handle Enter/Space - select menu item
            if ((enterPressed && !_enterKeyWasDown) || (spacePressed && !_spaceKeyWasDown))
            {
                Console.WriteLine($"[MyraMenuRenderer] Menu item selected via keyboard: index {_selectedIndex}");
                // Directly invoke MenuItemSelected event (same as button Click handler does)
                MenuItemSelected?.Invoke(this, _selectedIndex);
            }
            _enterKeyWasDown = enterDown;
            _spaceKeyWasDown = spaceDown;

            // Reset timer if no keys are being held
            if (!upDown && !downDown)
            {
                _keyRepeatTimer = 0f;
            }
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            Console.WriteLine($"[MyraMenuRenderer] Visibility set to: {visible}");
        }

        public bool IsVisible => _isVisible;
    }
}
