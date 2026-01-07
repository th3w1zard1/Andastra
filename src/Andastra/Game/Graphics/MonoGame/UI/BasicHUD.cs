using System;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Interfaces.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.UI
{
    /// <summary>
    /// Basic HUD component using MonoGame SpriteBatch rendering.
    /// Displays health bars, party status, and debug information.
    /// </summary>
    /// <remarks>
    /// HUD Rendering:
    /// - Based on swkotor2.exe HUD system
    /// - Located via string references: GUI panel references for HUD elements
    /// - "GuiQuickbar" @ 0x007c2484 (quickbar GUI), "GuiCharacterSheet" @ 0x007c24ac (character sheet GUI)
    /// - "GuiContainer" @ 0x007c24e4 (container GUI), "GuiInventory" @ 0x007c24f4 (inventory GUI)
    /// - "PT_LAST_GUI_PNL" @ 0x007c16bc (last GUI panel in PARTYTABLE)
    /// - GUI cursor references: "gui_mp_*" cursor elements for various actions (walk, talk, use, door, etc.)
    /// - "guisounds" @ 0x007b5f7c (GUI sound effects), ";gui_mouse" @ 0x007b5f93 (GUI mouse reference)
    /// - "PM_IsDisguised" @ 0x007bf5e4 (party member disguise flag)
    /// - Original implementation: Renders health/force bars, party portraits, minimap overlay
    /// - Health bar: Red bar showing current HP / max HP
    /// - Force bar: Blue bar showing current FP / max FP (KOTOR force points)
    /// - Party portraits: Small portraits of party members with status indicators
    /// - GUI system: Uses GUI panel files (.gui) for HUD and menu rendering
    /// - Based on KOTOR HUD conventions from vendor/PyKotor/wiki/
    /// </remarks>
    public class BasicHUD
    {
        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private bool _isVisible = false;
        private bool _showDebug = false;
        private Texture2D _barTexture;
        private IEntity _playerEntity;
        private float _fps;
        private int _frameCount;
        private float _elapsedTime;

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

        public IEntity PlayerEntity
        {
            get { return _playerEntity; }
            set { _playerEntity = value; }
        }

        public BasicHUD(GraphicsDevice device, SpriteFont font)
        {
            _spriteBatch = new SpriteBatch(device);
            _font = font ?? throw new ArgumentNullException("font");

            // Create a simple 1x1 texture for drawing bars
            _barTexture = new Texture2D(device, 1, 1);
            _barTexture.SetData(new[] { Color.White });
        }

        public void Update(GameTime gameTime)
        {
            if (gameTime != null)
            {
                _elapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
                _frameCount++;

                if (_elapsedTime >= 1.0f)
                {
                    _fps = _frameCount / _elapsedTime;
                    _frameCount = 0;
                    _elapsedTime = 0f;
                }
            }
        }

        public void Draw(GameTime gameTime)
        {
            if (!_isVisible || _font == null)
            {
                return;
            }

            int viewportWidth = _spriteBatch.GraphicsDevice.Viewport.Width;
            int viewportHeight = _spriteBatch.GraphicsDevice.Viewport.Height;

            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw health/force bars for player
            if (_playerEntity != null)
            {
                IStatsComponent stats = _playerEntity.GetComponent<IStatsComponent>();
                if (stats != null)
                {
                    int barX = 20;
                    int barY = viewportHeight - 100;
                    int barWidth = 200;
                    int barHeight = 20;

                    // Health bar
                    DrawHealthBar(_spriteBatch, barX, barY, barWidth, barHeight,
                        stats.CurrentHP, stats.MaxHP, Color.Red, Color.DarkRed);

                    // Force bar (below health)
                    barY += barHeight + 10;
                    DrawHealthBar(_spriteBatch, barX, barY, barWidth, barHeight,
                        stats.CurrentFP, stats.MaxFP, Color.Blue, Color.DarkBlue);

                    // HP/FP text
                    string hpText = string.Format("HP: {0}/{1}", stats.CurrentHP, stats.MaxHP);
                    string fpText = string.Format("FP: {0}/{1}", stats.CurrentFP, stats.MaxFP);
                    _spriteBatch.DrawString(_font, hpText, new Vector2(barX, barY - barHeight - 5), Color.White);
                    _spriteBatch.DrawString(_font, fpText, new Vector2(barX, barY + 5), Color.White);
                }
            }

            // Debug overlay
            if (_showDebug)
            {
                DrawDebugInfo(_spriteBatch, viewportWidth, viewportHeight);
            }

            _spriteBatch.End();
        }

        private void DrawHealthBar(SpriteBatch spriteBatch, int x, int y, int width, int height,
            int current, int max, Color fillColor, Color bgColor)
        {
            // Background
            spriteBatch.Draw(_barTexture, new Rectangle(x, y, width, height), bgColor);

            // Fill
            if (max > 0)
            {
                int fillWidth = (int)((float)current / max * width);
                if (fillWidth > 0)
                {
                    spriteBatch.Draw(_barTexture, new Rectangle(x, y, fillWidth, height), fillColor);
                }
            }

            // Border
            DrawRectangle(spriteBatch, x, y, width, height, Color.White, 1);
        }

        private void DrawRectangle(SpriteBatch spriteBatch, int x, int y, int width, int height, Color color, int thickness)
        {
            // Top
            spriteBatch.Draw(_barTexture, new Rectangle(x, y, width, thickness), color);
            // Bottom
            spriteBatch.Draw(_barTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
            // Left
            spriteBatch.Draw(_barTexture, new Rectangle(x, y, thickness, height), color);
            // Right
            spriteBatch.Draw(_barTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
        }

        private void DrawDebugInfo(SpriteBatch spriteBatch, int viewportWidth, int viewportHeight)
        {
            float y = 10;
            Color debugColor = Color.Yellow;

            spriteBatch.DrawString(_font, "DEBUG MODE", new Vector2(10, y), debugColor);
            y += _font.LineSpacing + 5;

            spriteBatch.DrawString(_font, string.Format("FPS: {0:F1}", _fps), new Vector2(10, y), debugColor);
            y += _font.LineSpacing + 5;

            if (_playerEntity != null)
            {
                ITransformComponent transform = _playerEntity.GetComponent<ITransformComponent>();
                if (transform != null)
                {
                    spriteBatch.DrawString(_font,
                        string.Format("Pos: ({0:F1}, {1:F1}, {2:F1})",
                            transform.Position.X, transform.Position.Y, transform.Position.Z),
                        new Vector2(10, y), debugColor);
                    y += _font.LineSpacing + 5;
                }
            }

            spriteBatch.DrawString(_font, "Press F1 to toggle debug",
                new Vector2(10, viewportHeight - 30), Color.Gray);
        }

        public void Dispose()
        {
            if (_barTexture != null)
            {
                _barTexture.Dispose();
                _barTexture = null;
            }
        }
    }
}

