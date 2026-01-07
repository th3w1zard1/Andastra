using System;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.MonoGame.Graphics.Cursor
{
    /// <summary>
    /// MonoGame implementation of ICursor.
    /// </summary>
    /// <remarks>
    /// MonoGame Cursor Implementation:
    /// - Based on swkotor.exe and swkotor2.exe cursor system
    /// - Cursor rendering: Rendered as sprite using SpriteBatch on top of all graphics
    /// - Cursor states: Up (normal) and Down (pressed) textures for visual feedback
    /// - Hotspot: Cursor hotspot determines click point (typically top-left corner or center)
    /// </remarks>
    public class MonoGameCursor : ICursor
    {
        private readonly ITexture2D _textureUp;
        private readonly ITexture2D _textureDown;
        private readonly int _hotspotX;
        private readonly int _hotspotY;
        private bool _disposed;

        public MonoGameCursor(ITexture2D textureUp, ITexture2D textureDown, int hotspotX, int hotspotY)
        {
            _textureUp = textureUp ?? throw new ArgumentNullException("textureUp");
            _textureDown = textureDown ?? throw new ArgumentNullException("textureDown");
            _hotspotX = hotspotX;
            _hotspotY = hotspotY;
        }

        public ITexture2D TextureUp => _textureUp;

        public ITexture2D TextureDown => _textureDown;

        public int HotspotX => _hotspotX;

        public int HotspotY => _hotspotY;

        public int Width => _textureUp.Width;

        public int Height => _textureUp.Height;

        public void Dispose()
        {
            if (!_disposed)
            {
                // Textures are managed by the cursor manager, don't dispose them here
                _disposed = true;
            }
        }
    }
}

