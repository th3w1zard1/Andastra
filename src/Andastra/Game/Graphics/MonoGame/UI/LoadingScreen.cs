using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.UI
{
    /// <summary>
    /// Loading screen UI component using MonoGame SpriteBatch rendering.
    /// </summary>
    /// <remarks>
    /// Loading Screen:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) loading screen system
    /// - Located via string references: "LoadScreenID" @ 0x007bd54c (loading screen ID)
    /// - "Loadscreens" @ 0x007c4c04 (loading screens resource), "loadscreenhints" @ 0x007c7350
    /// - "loadscreen_p" @ 0x007cbe40 (loading screen panel GUI)
    /// - "LBL_LOADING" @ 0x007cbe10 (loading label), "Load Bar = %d" @ 0x007c760c (load progress)
    /// - "LoadBar" @ 0x007cb33c (load progress bar)
    /// - Original implementation: KOTOR displays loading screen during module/area transitions
    /// - Loading screens: Defined in loadscreens.2da, displayed based on LoadScreenID
    /// - Loading hints: Text hints displayed during loading (loadscreenhints.2da)
    /// - Progress bar: Shows loading progress during resource loading
    /// </remarks>
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

