using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.UI
{
    /// <summary>
    /// Loading screen UI component using MonoGame SpriteBatch rendering.
    /// </summary>
    public class LoadingScreen
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private string _loadingText = "Loading...";

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }

        public LoadingScreen(GraphicsDevice device, SpriteFont font)
        {
            _spriteBatch = new SpriteBatch(device);
            _font = font;
        }

        public void Show(string text)
        {
            _loadingText = text;
            _isVisible = true;
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isVisible)
            {
                return;
            }

            _spriteBatch.Begin();

            // Draw loading screen background
            // TODO: Implement loading screen rendering

            if (_font != null)
            {
                int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
                int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;
                Vector2 textSize = _font.MeasureString(_loadingText);
                Vector2 position = new Vector2(
                    (viewportWidth - textSize.X) / 2,
                    (viewportHeight - textSize.Y) / 2
                );
                _spriteBatch.DrawString(_font, _loadingText, position, Color.White);
            }

            _spriteBatch.End();
        }
    }
}

