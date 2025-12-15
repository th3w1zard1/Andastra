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

            int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
            int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw semi-transparent black background overlay
            Texture2D pixel = new Texture2D(_spriteBatch.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.Black });
            _spriteBatch.Draw(pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), 
                new Color(0, 0, 0, 200)); // Semi-transparent black

            // Draw loading text centered
            if (_font != null)
            {
                Vector2 textSize = _font.MeasureString(_loadingText);
                Vector2 position = new Vector2(
                    (viewportWidth - textSize.X) / 2,
                    (viewportHeight - textSize.Y) / 2
                );
                _spriteBatch.DrawString(_font, _loadingText, position, Color.White);
            }

            _spriteBatch.End();

            if (pixel != null)
            {
                pixel.Dispose();
            }
        }
    }
}

