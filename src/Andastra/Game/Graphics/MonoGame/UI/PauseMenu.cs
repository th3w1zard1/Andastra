using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Andastra.Game.Graphics.MonoGame.UI
{
    /// <summary>
    /// Pause menu UI component using MonoGame SpriteBatch rendering.
    /// </summary>
    /// <remarks>
    /// Pause Menu:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) pause menu system
    /// - Located via string references: "Pause" @ 0x007c4de8, "PauseTimer" @ 0x007bfad4
    /// - "InfinitePause" @ 0x007bfae0, "RoundPaused" @ 0x007bfb00, "RoundPausedBy" @ 0x007bfaf0
    /// - "TIME_PAUSETIME" @ 0x007bdf88, "TIME_PAUSEDAY" @ 0x007bdf98 (pause time tracking)
    /// - "Mod_PauseTime" @ 0x007be89c, "Mod_PauseDay" @ 0x007be8ac (module pause time)
    /// - "PauseAndPlay" @ 0x007bda3c (pause/play toggle)
    /// - Pause sounds: "pause1" @ 0x007c7d74, "pause2" @ 0x007c7d7c, "pause3" @ 0x007c7d3c
    /// - "pausesh" @ 0x007c7d44, "pausebrd" @ 0x007c7d4c (pause sound effects)
    /// - GUI: "pause_p" @ 0x007cffb8 (pause panel), "BTN_UNPAUSE" @ 0x007cff90
    /// - "LBL_PAUSEREASON" @ 0x007cffa8, "TB_PAUSE" @ 0x007cd1f0
    /// - "Autopause Options" @ 0x007c848c, "BTN_AUTOPAUSE" @ 0x007d0d9c, "optautopause_p" @ 0x007d2a20
    /// - Options: "Game Options" @ 0x007b5654, "Graphics Options" @ 0x007b56a8, "Sound Options" @ 0x007b5720
    /// - "Display Options" @ 0x007ca3dc, "PlayOptions" @ 0x007bdaf8
    /// - "OPTIONS" @ 0x007c698c, "OPTIONS:" @ 0x007ced44, "OPTIONS:OPT" @ 0x007ced38
    /// - "BTN_OPTIONS" @ 0x007cbfac, "LB_OPTIONS" @ 0x007cf2f8
    /// - "optionsmain_p" @ 0x007d21ac, "optionsingame_p" @ 0x007d0dcc (options panels)
    /// - Original implementation: KOTOR pauses game time, displays pause menu with Resume/Options/Exit
    /// - Autopause: Game can auto-pause on certain events (combat, dialogue, etc.)
    /// - Pause time: Game time tracking pauses when game is paused
    /// </remarks>
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
            int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
            int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;
            Texture2D overlay = new Texture2D(_spriteBatch.GraphicsDevice, 1, 1);
            overlay.SetData(new[] { new Color(0, 0, 0, 180) }); // Semi-transparent black
            _spriteBatch.Draw(overlay, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.White);

            if (_font != null)
            {
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
            if (cancel)
            {
                _isVisible = false;
                OnMenuClosed?.Invoke();
            }
        }

        private void HandleSelection()
        {
            switch (_selectedIndex)
            {
                case 0: // Resume
                    _isVisible = false;
                    OnResume?.Invoke();
                    break;
                case 1: // Options
                    OnOptions?.Invoke();
                    break;
                case 2: // Exit
                    OnExit?.Invoke();
                    break;
            }
        }

        /// <summary>
        /// Event fired when resume is selected.
        /// </summary>
        public event Action OnResume;

        /// <summary>
        /// Event fired when options is selected.
        /// </summary>
        public event Action OnOptions;

        /// <summary>
        /// Event fired when exit is selected.
        /// </summary>
        public event Action OnExit;

        /// <summary>
        /// Event fired when menu is closed.
        /// </summary>
        public event Action OnMenuClosed;
    }
}

