using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Andastra.Runtime.MonoGame.UI
{
    /// <summary>
    /// Main menu UI component using MonoGame SpriteBatch rendering.
    /// Displays main menu options: New Game, Load Game, Options, Exit.
    /// </summary>
    /// <remarks>
    /// Main Menu Rendering:
    /// - Based on swkotor2.exe main menu system
    /// - Located via string references: "MAINMENU" @ 0x007cc030, "RIMS:MAINMENU" @ 0x007b6044
    /// - "mainmenu_p" @ 0x007cc000, "mainmenu8x6_p" @ 0x007cc00c (main menu panel GUI)
    /// - "mainmenu01-05" @ 0x007cc108-0x007cc114 (main menu button GUI elements)
    /// - "Action Menu" @ 0x007c8480, "CB_ACTIONMENU" @ 0x007d29d4 (action menu checkbox)
    /// - "LBL_MENUBG" @ 0x007cbf80 (menu background label)
    /// - "mgs_drawmain" @ 0x007cc8f0 (main menu draw function reference)
    /// - Original implementation: Renders main menu with game title and menu options
    /// - Menu options: New Game, Load Game, Options, Exit
    /// - Main menu uses GUI system with panel files (mainmenu_p.gui)
    /// - Based on KOTOR main menu conventions from vendor/PyKotor/wiki/
    /// </remarks>
    public class MainMenu
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private string _statusText = "";
        private int _selectedIndex = 0;
        private readonly string[] _menuItems = { "New Game", "Load Game", "Options", "Exit" };
        private Texture2D _backgroundTexture;
        private Texture2D _pixelTexture;

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                if (value >= 0 && value < _menuItems.Length)
                {
                    _selectedIndex = value;
                }
            }
        }

        public MainMenu(GraphicsDevice device, SpriteFont font)
        {
            _spriteBatch = new SpriteBatch(device);
            _font = font ?? throw new System.ArgumentNullException("font");

            // Create a simple 1x1 texture for drawing rectangles
            _pixelTexture = new Texture2D(device, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isVisible)
            {
                return;
            }

            int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
            int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw semi-transparent background overlay
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, viewportWidth, viewportHeight),
                new Color(0, 0, 0, 220)); // Dark semi-transparent background

            // Draw game title (centered at top)
            if (_font != null)
            {
                string title = "KNIGHTS OF THE OLD REPUBLIC";
                Vector2 titleSize = _font.MeasureString(title);
                Vector2 titlePos = new Vector2(
                    (viewportWidth - titleSize.X) / 2,
                    viewportHeight / 4
                );
                _spriteBatch.DrawString(_font, title, titlePos, Color.White);
            }

            // Draw menu items (centered)
            if (_font != null)
            {
                float startY = viewportHeight / 2;
                float itemSpacing = 50f;

                for (int i = 0; i < _menuItems.Length; i++)
                {
                    Color itemColor = (i == _selectedIndex) ? Color.Yellow : Color.White;
                    string itemText = _menuItems[i];
                    Vector2 itemSize = _font.MeasureString(itemText);
                    Vector2 itemPos = new Vector2(
                        (viewportWidth - itemSize.X) / 2,
                        startY + (i * itemSpacing)
                    );
                    _spriteBatch.DrawString(_font, itemText, itemPos, itemColor);
                }
            }

            // Draw status text at bottom
            if (_font != null && !string.IsNullOrEmpty(_statusText))
            {
                Vector2 statusSize = _font.MeasureString(_statusText);
                Vector2 statusPos = new Vector2(10, viewportHeight - statusSize.Y - 10);
                _spriteBatch.DrawString(_font, _statusText, statusPos, Color.Gray);
            }

            _spriteBatch.End();
        }

        public void HandleInput(bool up, bool down, bool select, bool cancel)
        {
            if (!_isVisible)
            {
                return;
            }

            if (up && _selectedIndex > 0)
            {
                _selectedIndex--;
            }
            if (down && _selectedIndex < _menuItems.Length - 1)
            {
                _selectedIndex++;
            }
            if (select)
            {
                HandleSelection();
            }
        }

        private void HandleSelection()
        {
            switch (_selectedIndex)
            {
                case 0: // New BioWareGame OnNewGame?.Invoke();
                    break;
                case 1: // Load BioWareGame OnLoadGame?.Invoke();
                    break;
                case 2: // Options
                    OnOptions?.Invoke();
                    break;
                case 3: // Exit
                    OnExit?.Invoke();
                    break;
            }
        }

        public void SetStatusText(string text)
        {
            _statusText = text ?? string.Empty;
        }

        /// <summary>
        /// Event fired when New Game is selected.
        /// </summary>
        public event System.Action OnNewGame;

        /// <summary>
        /// Event fired when Load Game is selected.
        /// </summary>
        public event System.Action OnLoadGame;

        /// <summary>
        /// Event fired when Options is selected.
        /// </summary>
        public event System.Action OnOptions;

        /// <summary>
        /// Event fired when Exit is selected.
        /// </summary>
        public event System.Action OnExit;

        public void Dispose()
        {
            if (_pixelTexture != null)
            {
                _pixelTexture.Dispose();
                _pixelTexture = null;
            }
            if (_backgroundTexture != null)
            {
                _backgroundTexture.Dispose();
                _backgroundTexture = null;
            }
        }
    }
}

