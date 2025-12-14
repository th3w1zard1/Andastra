using System;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Input;
using Stride.Rendering;
using Stride.Rendering.Compositing;
using Stride.Games;

namespace Odyssey.Stride.GUI
{
    /// <summary>
    /// Custom SceneRendererBase implementation for fallback main menu.
    /// Uses pure SpriteBatch rendering with colored rectangles - NO FONT DEPENDENCY.
    /// Based on Stride's official SpriteBatch and custom renderer documentation.
    /// </summary>
    public class FallbackMenuRenderer : SceneRendererBase
    {
        // Menu state
        private bool _isVisible = true;
        private int _selectedIndex = 0;
        private readonly MenuButton[] _menuButtons;

        // Rendering resources
        private SpriteBatch _spriteBatch;
        private Texture _whiteTexture; // 1x1 white texture for drawing rectangles

        // Layout
        private Vector2 _screenCenter;
        private RectangleF _mainPanelRect;
        private RectangleF _headerRect;

        // Colors - BRIGHT for maximum visibility
        private readonly Color _backgroundColor = new Color(20, 30, 60, 255); // Dark blue background
        private readonly Color _panelBackgroundColor = new Color(40, 50, 80, 255); // Panel background
        private readonly Color _headerColor = new Color(255, 200, 50, 255); // Bright gold header
        private readonly Color _borderColor = new Color(255, 255, 255, 255); // White border

        // Button colors
        private readonly Color _buttonStartColor = new Color(100, 255, 100, 255); // Bright green
        private readonly Color _buttonStartSelectedColor = new Color(150, 255, 150, 255); // Brighter green when selected
        private readonly Color _buttonOptionsColor = new Color(100, 150, 255, 255); // Bright blue
        private readonly Color _buttonOptionsSelectedColor = new Color(150, 180, 255, 255); // Brighter blue when selected
        private readonly Color _buttonExitColor = new Color(255, 100, 100, 255); // Bright red
        private readonly Color _buttonExitSelectedColor = new Color(255, 150, 150, 255); // Brighter red when selected

        // Menu action callback
        public event EventHandler<int> MenuItemSelected;

        private struct MenuButton
        {
            public RectangleF Rect;
            public Color NormalColor;
            public Color SelectedColor;
            public string Label; // For logging only, not displayed

            public MenuButton(RectangleF rect, Color normalColor, Color selectedColor, string label)
            {
                Rect = rect;
                NormalColor = normalColor;
                SelectedColor = selectedColor;
                Label = label;
            }
        }

        public FallbackMenuRenderer()
        {
            // Initialize menu buttons (will be positioned in CalculateLayout)
            _menuButtons = new MenuButton[3];
        }

        // Initialize renderer resources
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneRendererBase.html
        // InitializeCore() - Called once during initialization to set up renderer resources
        // Must call base.InitializeCore() to initialize base class functionality
        protected override void InitializeCore()
        {
            // Call base initialization to set up SceneRendererBase
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneRendererBase.html
            base.InitializeCore();

            Console.WriteLine("[FallbackMenuRenderer] Initializing SpriteBatch-based menu renderer");

            // Initialize sprite batch for 2D rendering
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteBatch.html
            // SpriteBatch(GraphicsDevice) - Creates a new SpriteBatch instance for the specified graphics device
            // GraphicsDevice is provided by SceneRendererBase base class
            // Method signature: SpriteBatch(GraphicsDevice device)
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            // Create 1x1 WHITE texture for drawing rectangles
            // IMPORTANT: This MUST contain an actual white pixel (1,1,1,1).
            // If the texture is left uninitialized (all zeros), SpriteBatch tinting multiplies against 0,
            // which results in everything rendering as black/transparent (appearing as "nothing drawn").
            //
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.Texture.html
            // Texture.New2D<T>(GraphicsDevice, int, int, PixelFormat, T[], TextureFlags, GraphicsResourceUsage, TextureOptions)
            // Method signature: New2D<T>(GraphicsDevice device, int width, int height, PixelFormat format, T[] textureData, ...)
            // Each value in textureData is a pixel in the destination texture.
            _whiteTexture = Texture.New2D(GraphicsDevice, 1, 1, PixelFormat.R8G8B8A8_UNorm, new[] { Color.White });

            Console.WriteLine("[FallbackMenuRenderer] SpriteBatch and white texture created successfully");
        }

        private void CalculateLayout(float screenWidth, float screenHeight)
        {
            // Calculate layout based on screen dimensions

            _screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);

            // Main panel - 600x400, centered
            float panelWidth = 600;
            float panelHeight = 400;
            float panelX = _screenCenter.X - panelWidth * 0.5f;
            float panelY = _screenCenter.Y - panelHeight * 0.5f;
            _mainPanelRect = new RectangleF(panelX, panelY, panelWidth, panelHeight);

            // Header - golden bar at top of panel
            float headerHeight = 80;
            _headerRect = new RectangleF(panelX + 30, panelY + 10, panelWidth - 60, headerHeight);

            // Buttons - evenly spaced below header
            float buttonWidth = panelWidth - 100;
            float buttonHeight = 70;
            float buttonX = panelX + 50;
            float buttonSpacing = 15;
            float startY = panelY + headerHeight + 30;

            // Start Game button (green)
            _menuButtons[0] = new MenuButton(
                new RectangleF(buttonX, startY, buttonWidth, buttonHeight),
                _buttonStartColor,
                _buttonStartSelectedColor,
                "Start Game"
            );

            // Options button (blue) - disabled but visible
            _menuButtons[1] = new MenuButton(
                new RectangleF(buttonX, startY + buttonHeight + buttonSpacing, buttonWidth, buttonHeight),
                _buttonOptionsColor,
                _buttonOptionsSelectedColor,
                "Options"
            );

            // Exit button (red)
            _menuButtons[2] = new MenuButton(
                new RectangleF(buttonX, startY + (buttonHeight + buttonSpacing) * 2, buttonWidth, buttonHeight),
                _buttonExitColor,
                _buttonExitSelectedColor,
                "Exit"
            );

            Console.WriteLine($"[FallbackMenuRenderer] Layout calculated: Panel=({panelX}, {panelY}, {panelWidth}, {panelHeight})");
        }

        // Render the menu using SpriteBatch
        // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Rendering.Compositing.SceneRendererBase.html
        // DrawCore(RenderContext, RenderDrawContext) - Called each frame to render the scene
        // RenderContext: Contains scene information and rendering state
        // RenderDrawContext: Provides graphics context and command list for drawing operations
        protected override void DrawCore(RenderContext context, RenderDrawContext drawContext)
        {
            if (!_isVisible)
                return;

            // Begin sprite batch rendering
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteBatch.html
            // Begin(GraphicsContext, SpriteSortMode, BlendStateDescription?) - Prepares sprite batch for drawing
            // Method signature: Begin(GraphicsContext context, SpriteSortMode sortMode, BlendStateDescription? blendState)
            // SpriteSortMode.Deferred: Sprites are sorted and rendered in a single batch for better performance
            // Passing null for blendState uses default (BlendState.AlphaBlend equivalent)
            _spriteBatch.Begin(drawContext.GraphicsContext, SpriteSortMode.Deferred, null);

            // Access back buffer dimensions from GraphicsDevice.Presenter.BackBuffer
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.GraphicsPresenter.html
            // GraphicsPresenter.BackBuffer - Gets the back buffer texture used for rendering
            // BackBuffer is a Texture, access dimensions via Description property
            // TextureDescription.Width/Height - Gets the width and height of the texture in pixels
            var backBuffer = GraphicsDevice.Presenter.BackBuffer;
            float screenWidth = backBuffer.Description.Width;
            float screenHeight = backBuffer.Description.Height;

            // Calculate layout based on current screen size (may change on window resize)
            CalculateLayout(screenWidth, screenHeight);

            // Draw background (full screen dark blue)
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteBatch.html
            // Draw(Texture, RectangleF, Color) draws a texture as a colored rectangle
            // Method signature: Draw(Texture texture, RectangleF destinationRectangle, Color color)
            // Source: https://doc.stride3d.net/4.0/en/Manual/graphics/low-level-api/spritebatch.html
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(0, 0, screenWidth, screenHeight),
                _backgroundColor);

            // Draw main panel background
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteBatch.html
            // Draw(Texture, RectangleF, Color) - same method as above, drawing panel rectangle
            _spriteBatch.Draw(_whiteTexture, _mainPanelRect, _panelBackgroundColor);

            // Draw panel border (thick white border)
            float borderThickness = 6;
            // Top border
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_mainPanelRect.X, _mainPanelRect.Y, _mainPanelRect.Width, borderThickness),
                _borderColor);
            // Bottom border
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_mainPanelRect.X, _mainPanelRect.Y + _mainPanelRect.Height - borderThickness,
                    _mainPanelRect.Width, borderThickness),
                _borderColor);
            // Left border
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_mainPanelRect.X, _mainPanelRect.Y, borderThickness, _mainPanelRect.Height),
                _borderColor);
            // Right border
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_mainPanelRect.X + _mainPanelRect.Width - borderThickness, _mainPanelRect.Y,
                    borderThickness, _mainPanelRect.Height),
                _borderColor);

            // Draw header (golden bar)
            _spriteBatch.Draw(_whiteTexture, _headerRect, _headerColor);
            // Header border
            float headerBorderThickness = 4;
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_headerRect.X, _headerRect.Y, _headerRect.Width, headerBorderThickness),
                _borderColor);
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_headerRect.X, _headerRect.Y + _headerRect.Height - headerBorderThickness,
                    _headerRect.Width, headerBorderThickness),
                _borderColor);
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_headerRect.X, _headerRect.Y, headerBorderThickness, _headerRect.Height),
                _borderColor);
            _spriteBatch.Draw(_whiteTexture,
                new RectangleF(_headerRect.X + _headerRect.Width - headerBorderThickness, _headerRect.Y,
                    headerBorderThickness, _headerRect.Height),
                _borderColor);

            // Draw menu buttons
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                var button = _menuButtons[i];
                var color = (i == _selectedIndex) ? button.SelectedColor : button.NormalColor;

                // Button background
                // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteBatch.html
                // Draw(Texture, RectangleF, Color) - Draws button as a colored rectangle
                _spriteBatch.Draw(_whiteTexture, button.Rect, color);

                // Button border (white, thicker when selected)
                float btnBorderThickness = (i == _selectedIndex) ? 6 : 4;
                // Top
                _spriteBatch.Draw(_whiteTexture,
                    new RectangleF(button.Rect.X, button.Rect.Y, button.Rect.Width, btnBorderThickness),
                    _borderColor);
                // Bottom
                _spriteBatch.Draw(_whiteTexture,
                    new RectangleF(button.Rect.X, button.Rect.Y + button.Rect.Height - btnBorderThickness,
                        button.Rect.Width, btnBorderThickness),
                    _borderColor);
                // Left
                _spriteBatch.Draw(_whiteTexture,
                    new RectangleF(button.Rect.X, button.Rect.Y, btnBorderThickness, button.Rect.Height),
                    _borderColor);
                // Right
                _spriteBatch.Draw(_whiteTexture,
                    new RectangleF(button.Rect.X + button.Rect.Width - btnBorderThickness, button.Rect.Y,
                        btnBorderThickness, button.Rect.Height),
                    _borderColor);

                // Inner border for visual depth (lighter version)
                if (i == _selectedIndex)
                {
                    float innerMargin = 8;
                    var innerRect = new RectangleF(
                        button.Rect.X + innerMargin,
                        button.Rect.Y + innerMargin,
                        button.Rect.Width - innerMargin * 2,
                        button.Rect.Height - innerMargin * 2
                    );
                    // Draw inner border highlight for selected button
                    // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteBatch.html
                    // Draw(Texture, RectangleF, Color) - Draws inner highlight with semi-transparent white
                    // Color(255, 255, 255, 128) creates a 50% opacity white overlay
                    _spriteBatch.Draw(_whiteTexture, innerRect, new Color(255, 255, 255, 128));
                }
            }

            // End sprite batch rendering
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Graphics.SpriteBatch.html
            // End() finalizes sprite batch operations and submits draw calls
            // Must be called after all Draw() calls and before Begin() is called again
            // Source: https://doc.stride3d.net/4.0/en/Manual/graphics/low-level-api/spritebatch.html
            _spriteBatch.End();
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

            // Handle keyboard navigation
            // Based on Stride API: https://doc.stride3d.net/latest/en/api/Stride.Input.InputManager.html
            // IsKeyPressed(Keys) checks if a key is currently pressed (returns true while key is held)
            // Method signature: bool IsKeyPressed(Keys key)
            // Keys enum defines keyboard key codes (Up, Down, Enter, Space, etc.)
            // Source: https://doc.stride3d.net/latest/en/manual/input/keyboard.html
            if (input.IsKeyPressed(Keys.Up))
            {
                _selectedIndex = (_selectedIndex - 1 + _menuButtons.Length) % _menuButtons.Length;
                Console.WriteLine($"[FallbackMenuRenderer] Selected: {_menuButtons[_selectedIndex].Label}");
            }
            else if (input.IsKeyPressed(Keys.Down))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuButtons.Length;
                Console.WriteLine($"[FallbackMenuRenderer] Selected: {_menuButtons[_selectedIndex].Label}");
            }
            else if (input.IsKeyPressed(Keys.Enter) || input.IsKeyPressed(Keys.Space))
            {
                Console.WriteLine($"[FallbackMenuRenderer] Menu item selected: {_menuButtons[_selectedIndex].Label}");
                MenuItemSelected?.Invoke(this, _selectedIndex);
            }
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            Console.WriteLine($"[FallbackMenuRenderer] Visibility set to: {visible}");
        }

        public bool IsVisible => _isVisible;
    }
}
