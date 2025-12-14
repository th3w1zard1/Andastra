using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Odyssey.MonoGame.UI
{
    /// <summary>
    /// Main menu UI component using MonoGame SpriteBatch rendering.
    /// </summary>
    public class MainMenu
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private string _statusText = "";

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }

        public MainMenu(GraphicsDevice device, SpriteFont font)
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
            
            // Draw menu background
            // TODO: Implement full menu rendering
            
            if (_font != null && !string.IsNullOrEmpty(_statusText))
            {
                _spriteBatch.DrawString(_font, _statusText, new Vector2(10, 10), Color.White);
            }

            _spriteBatch.End();
        }

        public void SetStatusText(string text)
        {
            _statusText = text;
        }
    }
}

