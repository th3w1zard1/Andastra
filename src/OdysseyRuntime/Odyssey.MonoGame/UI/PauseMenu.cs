using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Odyssey.MonoGame.UI
{
    /// <summary>
    /// Pause menu UI component using MonoGame SpriteBatch rendering.
    /// </summary>
    public class PauseMenu
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private int _selectedIndex = 0;
        private readonly string[] _menuItems = { "Resume", "Options", "Exit" };

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }

        public PauseMenu(GraphicsDevice device, SpriteFont font)
        {
            _spriteBatch = new SpriteBatch(device);
            _font = font;
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isVisible)
            {
                return;
            }

            _spriteBatch.Begin();

            // Draw semi-transparent overlay
            // TODO: Draw pause menu UI

            if (_font != null)
            {
                int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
                int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;
                Vector2 position = new Vector2(viewportWidth / 2 - 100, viewportHeight / 2 - 50);
                for (int i = 0; i < _menuItems.Length; i++)
                {
                    Color color = (i == _selectedIndex) ? Color.Yellow : Color.White;
                    _spriteBatch.DrawString(_font, _menuItems[i], position, color);
                    position.Y += 40;
                }
            }

            _spriteBatch.End();
        }

        public void HandleInput(bool up, bool down, bool select, bool cancel)
        {
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
                // Handle selection
            }
            if (cancel)
            {
                _isVisible = false;
            }
        }
    }
}

