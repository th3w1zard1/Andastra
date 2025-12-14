using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.UI
{
    /// <summary>
    /// Basic HUD component using MonoGame SpriteBatch rendering.
    /// </summary>
    public class BasicHUD
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private bool _showDebug = false;

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }

        public bool ShowDebug
        {
            get { return _showDebug; }
            set { _showDebug = value; }
        }

        public BasicHUD(GraphicsDevice device, SpriteFont font)
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

            // TODO: Implement HUD rendering (health bars, minimap, etc.)

            if (_showDebug && _font != null)
            {
                int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;
                _spriteBatch.DrawString(_font, "DEBUG MODE", new Vector2(10, viewportHeight - 30), Color.Yellow);
            }

            _spriteBatch.End();
        }
    }
}

