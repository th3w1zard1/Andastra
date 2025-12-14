using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Odyssey.Game.GUI
{
    /// <summary>
    /// Simple MonoGame-based menu renderer with text labels and click handling.
    /// </summary>
    public class MonoGameMenuRenderer
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

        public MonoGameMenuRenderer(GraphicsDevice graphicsDevice, SpriteFont font)
        {
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _font = font; // Can be null - we'll handle it in rendering

            // Create 1x1 white texture for drawing rectangles
            _whiteTexture = new Texture2D(graphicsDevice, 1, 1);
            _whiteTexture.SetData(new[] { Color.White });

            // Initialize menu buttons (will be positioned in CalculateLayout)
            _menuButtons = new MenuButton[3];

            // Initialize input states
            _previousKeyboardState = Keyboard.GetState();
            _previousMouseState = Mouse.GetState();
        }

        private void CalculateLayout(int screenWidth, int screenHeight)
        {
            _screenCenter = new Vector2(screenWidth * 0.5f, screenHeight * 0.5f);

            // Main panel - 600x400, centered
            int panelWidth = 600;
            int panelHeight = 400;
            int panelX = (int)(_screenCenter.X - panelWidth * 0.5f);
            int panelY = (int)(_screenCenter.Y - panelHeight * 0.5f);
            _mainPanelRect = new Rectangle(panelX, panelY, panelWidth, panelHeight);

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
        }

        public void Update(GameTime gameTime, GraphicsDevice graphicsDevice)
        {
            if (!_isVisible)
                return;

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

            KeyboardState currentKeyboardState = Keyboard.GetState();
            MouseState currentMouseState = Mouse.GetState();

            // Handle keyboard navigation
            if (IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Up))
            {
                _selectedIndex = (_selectedIndex - 1 + _menuButtons.Length) % _menuButtons.Length;
                Console.WriteLine($"[MonoGameMenuRenderer] Selected: {_menuButtons[_selectedIndex].Label}");
            }

            if (IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Down))
            {
                _selectedIndex = (_selectedIndex + 1) % _menuButtons.Length;
                Console.WriteLine($"[MonoGameMenuRenderer] Selected: {_menuButtons[_selectedIndex].Label}");
            }

            // Handle Enter/Space - select menu item
            if (IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Enter) ||
                IsKeyPressed(_previousKeyboardState, currentKeyboardState, Keys.Space))
            {
                Console.WriteLine($"[MonoGameMenuRenderer] Menu item selected: {_menuButtons[_selectedIndex].Label}");
                MenuItemSelected?.Invoke(this, _selectedIndex);
            }

            // Handle mouse input - click detection
            if (currentMouseState.LeftButton == ButtonState.Pressed &&
                _previousMouseState.LeftButton == ButtonState.Released)
            {
                Point mousePos = currentMouseState.Position;
                Console.WriteLine($"[MonoGameMenuRenderer] Mouse clicked at: {mousePos.X}, {mousePos.Y}");
                
                bool clicked = false;
                for (int i = 0; i < _menuButtons.Length; i++)
                {
                    var button = _menuButtons[i];
                    Console.WriteLine($"[MonoGameMenuRenderer] Button {i} ({button.Label}) rect: X={button.Rect.X}, Y={button.Rect.Y}, W={button.Rect.Width}, H={button.Rect.Height}");
                    
                    if (button.Rect.Contains(mousePos))
                    {
                        _selectedIndex = i;
                        Console.WriteLine($"[MonoGameMenuRenderer] Mouse clicked on: {_menuButtons[_selectedIndex].Label}");
                        MenuItemSelected?.Invoke(this, _selectedIndex);
                        clicked = true;
                        break;
                    }
                }
                
                if (!clicked)
                {
                    Console.WriteLine($"[MonoGameMenuRenderer] Mouse click not on any button");
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
                return;

            // Calculate layout based on current screen size
            CalculateLayout(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

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
                // Note: If font is null, buttons will still be visible as colored rectangles
                // and fully functional - just without text labels
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
            Console.WriteLine($"[MonoGameMenuRenderer] Visibility set to: {visible}");
        }

        public bool IsVisible => _isVisible;
    }
}

