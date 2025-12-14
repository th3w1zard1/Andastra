using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Odyssey.Game.GUI
{
    /// <summary>
    /// Simple MonoGame-based menu renderer with text labels and click handling.
    /// </summary>
    public class MenuRenderer
    {
        // Menu state
        private bool _isVisible = true;
        private int _selectedIndex = 0;
        private readonly MenuButton[] _menuButtons;

        // Input state
        private KeyboardState _previousKeyboardState;
        private MouseState _previousMouseState;

        // Rendering resources
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private Texture2D _whiteTexture; // 1x1 white texture for drawing rectangles

        // Layout
        private Vector2 _screenCenter;
        private Rectangle _mainPanelRect;
        private Rectangle _headerRect;
        private int _lastScreenWidth = 0;
        private int _lastScreenHeight = 0;

        // Colors
        private readonly Color _backgroundColor = new Color(20, 30, 60, 255); // Dark blue background
        private readonly Color _panelBackgroundColor = new Color(40, 50, 80, 255); // Panel background
        private readonly Color _headerColor = new Color(255, 200, 50, 255); // Bright gold header
        private readonly Color _borderColor = Color.White;

        // Button colors
        private readonly Color _buttonStartColor = new Color(100, 255, 100, 255); // Bright green
        private readonly Color _buttonStartSelectedColor = new Color(150, 255, 150, 255);
        private readonly Color _buttonOptionsColor = new Color(100, 150, 255, 255); // Bright blue
        private readonly Color _buttonOptionsSelectedColor = new Color(150, 180, 255, 255);
        private readonly Color _buttonExitColor = new Color(255, 100, 100, 255); // Bright red
        private readonly Color _buttonExitSelectedColor = new Color(255, 150, 150, 255);

        // Menu action callback
        public event EventHandler<int> MenuItemSelected;

        private struct MenuButton
        {
            public Rectangle Rect;
            public Color NormalColor;
            public Color SelectedColor;
            public string Label;

            public MenuButton(Rectangle rect, Color normalColor, Color selectedColor, string label)
            {
                Rect = rect;
                NormalColor = normalColor;
                SelectedColor = selectedColor;
                Label = label;
            }
        }

        public MenuRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _spriteBatch = new SpriteBatch(graphicsDevice);
            _font = font; // Can be null - we'll handle it in rendering

            // Create 1x1 white texture for drawing rectangles
            _whiteTexture = new Texture2D(graphicsDevice, 1, 1);
            _whiteTexture.SetData(new[] { Color.White });

            // Initialize menu buttons (will be positioned in CalculateLayout)
            // Create default buttons - they'll be repositioned in CalculateLayout
            _menuButtons = new MenuButton[3];
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                _menuButtons[i] = new MenuButton(
                    new Rectangle(0, 0, 0, 0), // Will be set in CalculateLayout
                    Color.White,
                    Color.White,
                    i == 0 ? "Start Game" : (i == 1 ? "Options" : "Exit")
                );
            }

            // Initialize input states
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();

            Console.WriteLine("[MenuRenderer] Initialized successfully");
            Console.WriteLine($"[MenuRenderer] Font available: {_font != null}");
            Console.WriteLine($"[MenuRenderer] Menu buttons array initialized with {_menuButtons.Length} buttons");
        }

        private void CalculateLayout(int screenWidth, int screenHeight)
        {
            // Ensure we have valid screen dimensions
            if (screenWidth <= 0) screenWidth = 1280;
            if (screenHeight <= 0) screenHeight = 720;

            _screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);

            // Main panel - 600x400, centered
            int panelWidth = 600;
            int panelHeight = 400;
            int panelX = (int)(_screenCenter.X - panelWidth * 0.5f);
            int panelY = (int)(_screenCenter.Y - panelHeight * 0.5f);
            _mainPanelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);

            Console.WriteLine($"[MenuRenderer] Layout calculated: Screen={screenWidth}x{screenHeight}, Panel={panelX},{panelY},{panelWidth}x{panelHeight}");

            // Header - golden bar at top of panel
            int headerHeight = 80;
            _headerRect = new Rectangle(panelX + 30, panelY + 10, panelWidth - 60, headerHeight);

            // Buttons - evenly spaced below header
            int buttonWidth = panelWidth - 100;
            int buttonHeight = 70;
            int buttonX = panelX + 50;
            int buttonSpacing = 15;
            int startY = panelY + headerHeight + 30;

            // Start Game button (green)
            _menuButtons[0] = new MenuButton(
                new Rectangle(buttonX, startY, buttonWidth, buttonHeight),
                _buttonStartColor,
                _buttonStartSelectedColor,
                "Start Game"
            );

            // Options button (blue)
            _menuButtons[1] = new MenuButton(
                new Rectangle(buttonX, startY + buttonHeight + buttonSpacing, buttonWidth, buttonHeight),
                _buttonOptionsColor,
                _buttonOptionsSelectedColor,
                "Options"
            );

            // Exit button (red)
            _menuButtons[2] = new MenuButton(
                new Rectangle(buttonX, startY + (buttonHeight + buttonSpacing) * 2, buttonWidth, buttonHeight),
                _buttonExitColor,
                _buttonExitSelectedColor,
                "Exit"
            );

            Console.WriteLine($"[MenuRenderer] Buttons initialized:");
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                var btn = _menuButtons[i];
                Console.WriteLine($"  Button {i} ({btn.Label}): X={btn.Rect.X}, Y={btn.Rect.Y}, W={btn.Rect.Width}, H={btn.Rect.Height}");
            }
        }

        public void Update(GameTime gameTime, GraphicsDevice graphicsDevice)
        {
            if (!_isVisible)
            {
                return;
            }

            // Log first update to verify Update is being called
            if (gameTime.TotalGameTime.TotalSeconds < 0.1)
            {
                Console.WriteLine("[MenuRenderer] Update called - menu is visible and active");
            }

            // Ensure layout is calculated before handling input
            // Layout needs to be up-to-date for click detection to work
            if (graphicsDevice != null)
            {
                int screenWidth = graphicsDevice.Viewport.Width;
                int screenHeight = graphicsDevice.Viewport.Height;

                // Recalculate layout if screen size changed or if not yet calculated
                if (screenWidth != _lastScreenWidth || screenHeight != _lastScreenHeight || _lastScreenWidth == 0)
                {
                    CalculateLayout(screenWidth, screenHeight);
                    _lastScreenWidth = screenWidth;
                    _lastScreenHeight = screenHeight;
                }
            }
            else
            {
                Console.WriteLine("[MenuRenderer] WARNING: GraphicsDevice is null in Update!");
                return;
            }

            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();

            // Handle keyboard navigation
            if (IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Up))
            {
                _selectedIndex = (_selectedIndex - 1 + _menuButtons.Length) % _menuButtons.Length;
                Console.WriteLine($"[MenuRenderer] Selected: {_menuButtons[_selectedIndex].Label}");
            }

            if (IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Down))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuButtons.Length;
                Console.WriteLine($"[MenuRenderer] Selected: {_menuButtons[_selectedIndex].Label}");
            }

            // Handle Enter/Space - select menu item
            if (IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Enter) ||
                IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Space))
            {
                Console.WriteLine($"[MenuRenderer] Menu item selected: {_menuButtons[_selectedIndex].Label}");
                MenuItemSelected?.Invoke(this, _selectedIndex);
            }

            // Handle mouse input - click detection
            // Check for mouse button press (transition from released to pressed)
            bool mouseJustPressed = currentMouseState.LeftButton == ButtonState.Pressed &&
                                    _previousMouseState.LeftButton == ButtonState.Released;
            
            // Also check if mouse button is currently pressed (for debugging)
            bool mouseCurrentlyPressed = currentMouseState.LeftButton == ButtonState.Pressed;
            if (mouseCurrentlyPressed && gameTime.TotalGameTime.TotalSeconds < 2.0)
            {
                // Log mouse position every frame for first 2 seconds to debug
                Point mousePos = currentMouseState.Position;
                Console.WriteLine($"[MenuRenderer] Mouse at: {mousePos.X}, {mousePos.Y} (Button: {mouseCurrentlyPressed})");
            }

            if (mouseJustPressed)
            {
                Point mousePos = currentMouseState.Position;
                Console.WriteLine($"[MenuRenderer] ====== MOUSE CLICK DETECTED ======");
                Console.WriteLine($"[MenuRenderer] Mouse clicked at: {mousePos.X}, {mousePos.Y}");
                Console.WriteLine($"[MenuRenderer] Number of buttons: {_menuButtons.Length}");

                bool clicked = false;
                for (int i = 0; i < _menuButtons.Length; i++)
                {
                    var button = _menuButtons[i];
                    bool contains = button.Rect.Contains(mousePos);
                    Console.WriteLine($"[MenuRenderer] Button {i} ({button.Label}) rect: X={button.Rect.X}, Y={button.Rect.Y}, W={button.Rect.Width}, H={button.Rect.Height}, Contains={contains}");

                    if (contains)
                    {
                        _selectedIndex = i;
                        Console.WriteLine($"[MenuRenderer] ====== BUTTON CLICKED: {_menuButtons[_selectedIndex].Label} ======");

                        // Verify event handler is not null before invoking
                        if (MenuItemSelected != null)
                        {
                            Console.WriteLine($"[MenuRenderer] Invoking MenuItemSelected event with index {_selectedIndex}");
                            MenuItemSelected.Invoke(this, _selectedIndex);
                        }
                        else
                        {
                            Console.WriteLine($"[MenuRenderer] ERROR: MenuItemSelected event handler is NULL!");
                        }

                        clicked = true;
                        break;
                    }
                }

                if (!clicked)
                {
                    Console.WriteLine($"[MenuRenderer] Mouse click not on any button - click was outside all button rectangles");
                }
            }

            _previousKeyboardState = currentKeyboardState;
            _previousMouseState = currentMouseState;
        }

        private bool IsKeyPressed(KeyboardState previous, KeyboardState current, Keys key)
        {
            return previous.IsKeyUp(key) && current.IsKeyDown(key);
        }

        public void Draw(GameTime gameTime, GraphicsDevice graphicsDevice)
        {
            if (!_isVisible)
            {
                return;
            }

            if (graphicsDevice == null)
            {
                Console.WriteLine("[MenuRenderer] ERROR: GraphicsDevice is null in Draw!");
                return;
            }

            // Calculate layout based on current screen size
            int screenWidth = graphicsDevice.Viewport.Width;
            int screenHeight = graphicsDevice.Viewport.Height;

            // Always ensure layout is calculated - buttons must be positioned for click detection
            // Only recalculate if dimensions changed to avoid excessive logging
            if (screenWidth != _lastScreenWidth || screenHeight != _lastScreenHeight || _lastScreenWidth == 0)
            {
                CalculateLayout(screenWidth, screenHeight);
                _lastScreenWidth = screenWidth;
                _lastScreenHeight = screenHeight;
            }

            // Verify buttons are initialized (safety check)
            if (_menuButtons == null || _menuButtons.Length == 0)
            {
                Console.WriteLine("[MenuRenderer] ERROR: Menu buttons array is null or empty in Draw!");
                return;
            }

            // Begin sprite batch rendering
            _spriteBatch.Begin();

            // Draw background (full screen dark blue)
            _spriteBatch.Draw(_whiteTexture,
                new Rectangle(0, 0, graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height),
                _backgroundColor);

            // Draw main panel background
            _spriteBatch.Draw(_whiteTexture, _mainPanelRect, _panelBackgroundColor);

            // Draw panel border
            int borderThickness = 6;
            DrawBorder(_spriteBatch, _mainPanelRect, borderThickness, _borderColor);

            // Draw header (golden bar)
            _spriteBatch.Draw(_whiteTexture, _headerRect, _headerColor);
            DrawBorder(_spriteBatch, _headerRect, 4, _borderColor);

            // Draw menu buttons
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                var button = _menuButtons[i];
                var color = (i == _selectedIndex) ? button.SelectedColor : button.NormalColor;

                // Button background
                _spriteBatch.Draw(_whiteTexture, button.Rect, color);

                // Button border (thicker when selected)
                int btnBorderThickness = (i == _selectedIndex) ? 6 : 4;
                DrawBorder(_spriteBatch, button.Rect, btnBorderThickness, _borderColor);

                // Draw button text (if font is available)
                if (_font != null)
                {
                    Vector2 textSize = _font.MeasureString(button.Label);
                    Vector2 textPosition = new Vector2(
                        button.Rect.X + (button.Rect.Width - textSize.X) * 0.5f,
                        button.Rect.Y + (button.Rect.Height - textSize.Y) * 0.5f
                    );
                    _spriteBatch.DrawString(_font, button.Label, textPosition, Color.White);
                }
                else
                {
                    // Draw simple text indicator using rectangles when font is not available
                    // Draw a small indicator in the center of each button
                    int indicatorSize = 20;
                    int indicatorX = button.Rect.X + (button.Rect.Width - indicatorSize) / 2;
                    int indicatorY = button.Rect.Y + (button.Rect.Height - indicatorSize) / 2;
                    _spriteBatch.Draw(_whiteTexture,
                        new Rectangle(indicatorX, indicatorY, indicatorSize, indicatorSize),
                        Color.White);
                }
            }

            // End sprite batch rendering
            _spriteBatch.End();
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, int thickness, Color color)
        {
            // Top border
            spriteBatch.Draw(_whiteTexture,
                new Rectangle(rect.X, rect.Y, rect.Width, thickness),
                color);
            // Bottom border
            spriteBatch.Draw(_whiteTexture,
                new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness),
                color);
            // Left border
            spriteBatch.Draw(_whiteTexture,
                new Rectangle(rect.X, rect.Y, thickness, rect.Height),
                color);
            // Right border
            spriteBatch.Draw(_whiteTexture,
                new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height),
                color);
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            Console.WriteLine($"[MenuRenderer] Visibility set to: {visible}");
        }

        public bool IsVisible => _isVisible;
    }
}

