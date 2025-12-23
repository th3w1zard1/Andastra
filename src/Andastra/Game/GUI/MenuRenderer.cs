using System;
using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework;

namespace Andastra.Runtime.Game.GUI
{
    /// <summary>
    /// Menu renderer with text labels and click handling using graphics abstraction layer.
    /// </summary>
    /// <remarks>
    /// Menu Renderer:
    /// - Based on swkotor2.exe menu rendering system
    /// - Located via string references: "RIMS:MAINMENU" @ 0x007b6044 (main menu RIM), "MAINMENU" @ 0x007cc030 (main menu constant)
    /// - "mainmenu_p" @ 0x007cc000 (main menu panel), "mainmenu8x6_p" @ 0x007cc00c (main menu 8x6 panel)
    /// - "mainmenu01" @ 0x007cc108, "mainmenu02" @ 0x007cc138, "mainmenu03" @ 0x007cc12c, "mainmenu04" @ 0x007cc120, "mainmenu05" @ 0x007cc114 (main menu variants)
    /// - "LBL_MENUBG" @ 0x007cbf80 (menu background label), "Action Menu" @ 0x007c8480 (action menu)
    /// - "CB_ACTIONMENU" @ 0x007d29d4 (action menu checkbox)
    /// - GUI elements: "gui_mp_arwalk00" through "gui_mp_arwalk15" @ 0x007b59cc-0x007b58dc (area walk GUI elements)
    /// - "gui_mp_arrun00" through "gui_mp_arrun15" @ 0x007b5a0c-0x007b59dc (area run GUI elements)
    /// - Original implementation: Renders main menu with buttons (Start Game, Options, Exit), handles input and selection
    /// - Menu panels: Uses GUI panel system with background textures and button overlays
    /// - Based on swkotor2.exe: Main menu uses GUI system with panel-based rendering
    /// </remarks>
    public class MenuRenderer
    {
        // Menu state
        private bool _isVisible = true;
        private int _selectedIndex = 0;
        private readonly MenuButton[] _menuButtons;

        // Input state
        private IKeyboardState _previousKeyboardState;
        private IMouseState _previousMouseState;

        // Rendering resources
        private ISpriteBatch _spriteBatch;
        private IFont _font;
        private ITexture2D _whiteTexture; // 1x1 white texture for drawing rectangles

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
        private readonly Color _borderColor = new Color(255, 255, 255, 255);

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

        public MenuRenderer(IGraphicsDevice graphicsDevice, IFont font)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _spriteBatch = graphicsDevice.CreateSpriteBatch();
            _font = font; // Can be null - we'll handle it in rendering

            // Create 1x1 white texture for drawing rectangles
            byte[] whitePixel = new byte[] { 255, 255, 255, 255 }; // RGBA white
            _whiteTexture = graphicsDevice.CreateTexture2D(1, 1, whitePixel);

            // Initialize menu buttons (will be positioned in CalculateLayout)
            // Create default buttons - they'll be repositioned in CalculateLayout
            _menuButtons = new MenuButton[3];
            for (int i = 0; i < _menuButtons.Length; i++)
            {
                _menuButtons[i] = new MenuButton(
                    new Rectangle(0, 0, 0, 0), // Will be set in CalculateLayout
                    new Color(255, 255, 255, 255),
                    new Color(255, 255, 255, 255),
                    i == 0 ? "Start Game" : (i == 1 ? "Options" : "Exit")
                );
            }

            // Initialize input states - these will be set by Update method
            _previousKeyboardState = null;
            _previousMouseState = null;

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
                MenuButton btn = _menuButtons[i];
                Console.WriteLine($"  Button {i} ({btn.Label}): X={btn.Rect.X}, Y={btn.Rect.Y}, W={btn.Rect.Width}, H={btn.Rect.Height}");
            }
        }

        public void Update(float deltaTime, IGraphicsDevice graphicsDevice, IInputManager inputManager)
        {
            if (!_isVisible)
            {
                return;
            }

            // Log first update to verify Update is being called
            // (removed gameTime check - deltaTime is sufficient)

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

            if (inputManager == null)
            {
                Console.WriteLine("[MenuRenderer] WARNING: InputManager is null in Update!");
                return;
            }

            IKeyboardState currentKeyboardState = inputManager.KeyboardState;
            IMouseState currentMouseState = inputManager.MouseState;

            // Initialize previous states if null
            if (_previousKeyboardState == null)
            {
                _previousKeyboardState = currentKeyboardState;
            }
            if (_previousMouseState == null)
            {
                _previousMouseState = currentMouseState;
            }

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

            if (mouseJustPressed)
            {
                int mouseX = currentMouseState.X;
                int mouseY = currentMouseState.Y;
                Console.WriteLine($"[MenuRenderer] ====== MOUSE CLICK DETECTED ======");
                Console.WriteLine($"[MenuRenderer] Mouse clicked at: {mouseX}, {mouseY}");
                Console.WriteLine($"[MenuRenderer] Number of buttons: {_menuButtons.Length}");

                bool clicked = false;
                for (int i = 0; i < _menuButtons.Length; i++)
                {
                    MenuButton button = _menuButtons[i];
                    bool contains = mouseX >= button.Rect.X && mouseX < button.Rect.X + button.Rect.Width &&
                                    mouseY >= button.Rect.Y && mouseY < button.Rect.Y + button.Rect.Height;
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

        private bool IsKeyPressed(IKeyboardState previous, IKeyboardState current, Keys key)
        {
            return previous.IsKeyUp(key) && current.IsKeyDown(key);
        }

        public void Draw(IGraphicsDevice graphicsDevice)
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
                MenuButton button = _menuButtons[i];
                Color color = (i == _selectedIndex) ? button.SelectedColor : button.NormalColor;

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
                    _spriteBatch.DrawString(_font, button.Label, textPosition, new Color(255, 255, 255, 255));
                }
                else
                {
                    // Draw simple text indicator using rectangles when font is not available
                    // Draw a larger, more visible indicator in the center of each button
                    // Use different shapes/colors for each button to make them distinct
                    int indicatorSize = 30;
                    int indicatorX = button.Rect.X + (button.Rect.Width - indicatorSize) / 2;
                    int indicatorY = button.Rect.Y + (button.Rect.Height - indicatorSize) / 2;

                    // Different indicators for each button
                    if (i == 0) // Start Game - green circle
                    {
                        // Draw a filled circle (approximated as a square with rounded appearance)
                        _spriteBatch.Draw(_whiteTexture,
                            new Rectangle(indicatorX, indicatorY, indicatorSize, indicatorSize),
                            new Color(255, 255, 255, 255));
                    }
                    else if (i == 1) // Options - blue square with border
                    {
                        // Draw a square
                        _spriteBatch.Draw(_whiteTexture,
                            new Rectangle(indicatorX, indicatorY, indicatorSize, indicatorSize),
                            new Color(255, 255, 255, 255));
                        // Draw border
                        DrawBorder(_spriteBatch, new Rectangle(indicatorX, indicatorY, indicatorSize, indicatorSize), 2, new Color(0, 0, 0, 255));
                    }
                    else if (i == 2) // Exit - red X shape
                    {
                        // Draw an X shape using two rectangles
                        int xThickness = 4;
                        int xSize = indicatorSize;
                        // Diagonal line 1
                        _spriteBatch.Draw(_whiteTexture,
                            new Rectangle(indicatorX + xSize / 2 - xThickness / 2, indicatorY, xThickness, xSize),
                            new Color(255, 255, 255, 255));
                        // Diagonal line 2 (rotated - approximate with offset)
                        _spriteBatch.Draw(_whiteTexture,
                            new Rectangle(indicatorX, indicatorY + xSize / 2 - xThickness / 2, xSize, xThickness),
                            new Color(255, 255, 255, 255));
                    }
                }
            }

            // End sprite batch rendering
            _spriteBatch.End();
        }

        private void DrawBorder(ISpriteBatch spriteBatch, Rectangle rect, int thickness, Color color)
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

        /// <summary>
        /// Gets the white texture used for drawing rectangles.
        /// </summary>
        public ITexture2D GetWhiteTexture()
        {
            return _whiteTexture;
        }
    }
}

