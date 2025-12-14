using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Odyssey.MonoGame.UI
{
    /// <summary>
    /// Dialogue panel UI component using MonoGame SpriteBatch rendering.
    /// </summary>
    public class DialoguePanel
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private int _selectedReplyIndex = 0;

        public bool IsVisible
        {
            get { return _isVisible; }
            set { _isVisible = value; }
        }

        public DialoguePanel(GraphicsDevice device, SpriteFont font)
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

            // TODO: Implement dialogue panel rendering

            _spriteBatch.End();
        }

        public void HandleInput(bool up, bool down, bool select, bool cancel)
        {
            if (up && _selectedReplyIndex > 0)
            {
                _selectedReplyIndex--;
            }
            if (down)
            {
                _selectedReplyIndex++;
            }
            if (select)
            {
                // Handle reply selection
            }
            if (cancel)
            {
                _isVisible = false;
            }
        }

        public void HandleNumberKey(int number)
        {
            // Handle number key selection for dialogue replies
            _selectedReplyIndex = number - 1;
        }
    }
}

