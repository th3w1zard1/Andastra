using System;
using System.Collections.Generic;
using System.Numerics;
using Andastra.Runtime.Graphics;
using GraphicsVector2 = Andastra.Runtime.Graphics.Vector2;

namespace Andastra.Runtime.MonoGame.Graphics.Cursor
{
    /// <summary>
    /// MonoGame implementation of ICursorManager.
    /// </summary>
    /// <remarks>
    /// MonoGame Cursor Manager:
    /// - Based on swkotor.exe and swkotor2.exe cursor management system
    /// - Cursor loading: Loads cursors from game resources or creates programmatic fallbacks
    /// - Cursor caching: Caches loaded cursors to avoid reloading
    /// - Cursor state: Tracks current cursor type and pressed state
    /// - Cursor position: Tracks mouse position for rendering
    /// </remarks>
    public class MonoGameCursorManager : ICursorManager
    {
        private readonly IGraphicsDevice _graphicsDevice;
        private readonly Dictionary<CursorType, ICursor> _cursorCache;
        private CursorType _currentCursorType;
        private bool _isPressed;
        private GraphicsVector2 _position;
        private bool _disposed;

        public MonoGameCursorManager(IGraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException("graphicsDevice");
            _cursorCache = new Dictionary<CursorType, ICursor>();
            _currentCursorType = CursorType.Default;
            _position = new GraphicsVector2(0, 0);
        }

        public ICursor CurrentCursor
        {
            get
            {
                ICursor cursor = GetCursor(_currentCursorType);
                return cursor;
            }
        }

        public bool IsPressed
        {
            get { return _isPressed; }
            set { _isPressed = value; }
        }

        public GraphicsVector2 Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public ICursor GetCursor(CursorType type)
        {
            if (_cursorCache.TryGetValue(type, out ICursor cached))
            {
                return cached;
            }

            ICursor cursor = CreateCursor(type);
            if (cursor != null)
            {
                _cursorCache[type] = cursor;
            }

            return cursor;
        }

        public void SetCursor(CursorType type)
        {
            _currentCursorType = type;
        }

        private ICursor CreateCursor(CursorType type)
        {
            // Create programmatic cursor textures as fallback
            // In a full implementation, these would be loaded from game resources
            ITexture2D textureUp = CreateCursorTexture(type, false);
            ITexture2D textureDown = CreateCursorTexture(type, true);

            if (textureUp == null || textureDown == null)
            {
                return null;
            }

            // Determine hotspot based on cursor type
            // Default hotspot is top-left (0, 0) for most cursors
            int hotspotX = 0;
            int hotspotY = 0;

            // For hand cursor, hotspot is typically at the tip of the pointing finger
            if (type == CursorType.Hand)
            {
                hotspotX = textureUp.Width / 4;
                hotspotY = textureUp.Height / 4;
            }

            return new MonoGameCursor(textureUp, textureDown, hotspotX, hotspotY);
        }

        private ITexture2D CreateCursorTexture(CursorType type, bool pressed)
        {
            int width = 32;
            int height = 32;
            byte[] pixels = new byte[width * height * 4]; // RGBA

            // Create cursor texture based on type
            if (type == CursorType.Hand)
            {
                CreateHandCursorPixels(pixels, width, height, pressed);
            }
            else
            {
                // Default cursor (arrow)
                CreateDefaultCursorPixels(pixels, width, height, pressed);
            }

            return _graphicsDevice.CreateTexture2D(width, height, pixels);
        }

        private void CreateDefaultCursorPixels(byte[] pixels, int width, int height, bool pressed)
        {
            // Create a simple arrow cursor
            // Arrow points up-left (typical default cursor)
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte r = 255;
                    byte g = 255;
                    byte b = 255;
                    byte a = 0;

                    // Draw arrow shape
                    if (x < width / 2 && y < height / 2)
                    {
                        // Arrow shaft (diagonal line)
                        if (x == y || x == y - 1 || x == y + 1)
                        {
                            a = 255;
                        }
                        // Arrow head (triangle)
                        else if (x < width / 4 && y < height / 4)
                        {
                            int dx = x - 0;
                            int dy = y - 0;
                            if (dx + dy < width / 4)
                            {
                                a = 255;
                            }
                        }
                    }

                    // Outline (black)
                    if (a > 0)
                    {
                        bool hasOutline = false;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    int nIndex = (ny * width + nx) * 4;
                                    if (pixels[nIndex + 3] == 0)
                                    {
                                        hasOutline = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (hasOutline && (x == 0 || y == 0 || x == width - 1 || y == height - 1 || 
                            (x > 0 && pixels[((y * width + (x - 1)) * 4) + 3] == 0) ||
                            (y > 0 && pixels[(((y - 1) * width + x) * 4) + 3] == 0)))
                        {
                            r = 0;
                            g = 0;
                            b = 0;
                        }
                    }

                    pixels[index] = r;
                    pixels[index + 1] = g;
                    pixels[index + 2] = b;
                    pixels[index + 3] = a;
                }
            }
        }

        private void CreateHandCursorPixels(byte[] pixels, int width, int height, bool pressed)
        {
            // Create a simple hand/pointer cursor
            // Hand shape: pointing finger with palm
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width + x) * 4;
                    byte r = 255;
                    byte g = 255;
                    byte b = 255;
                    byte a = 0;

                    // Hand shape: pointing finger pointing up-right
                    // Finger (top-right area)
                    if (x > width * 0.6f && y < height * 0.4f)
                    {
                        // Pointing finger
                        int fingerX = x - (int)(width * 0.6f);
                        int fingerY = y;
                        if (fingerX < fingerY * 0.5f + 2 && fingerX > fingerY * 0.5f - 2)
                        {
                            a = 255;
                        }
                        // Finger tip
                        if (x > width * 0.75f && y < height * 0.15f)
                        {
                            a = 255;
                        }
                    }

                    // Palm (middle area)
                    if (x > width * 0.3f && x < width * 0.7f && y > height * 0.3f && y < height * 0.7f)
                    {
                        a = 255;
                    }

                    // Thumb (left side)
                    if (x < width * 0.4f && y > height * 0.4f && y < height * 0.7f)
                    {
                        int thumbX = x;
                        int thumbY = y - (int)(height * 0.4f);
                        if (thumbX > thumbY * 0.8f - 2 && thumbX < thumbY * 0.8f + 2)
                        {
                            a = 255;
                        }
                    }

                    // Outline
                    if (a > 0)
                    {
                        // Check for outline pixels
                        bool needsOutline = false;
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int nx = x + dx;
                                int ny = y + dy;
                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    int nIndex = (ny * width + nx) * 4;
                                    if (pixels[nIndex + 3] == 0)
                                    {
                                        needsOutline = true;
                                        break;
                                    }
                                }
                            }
                            if (needsOutline)
                            {
                                break;
                            }
                        }
                        if (needsOutline)
                        {
                            r = 0;
                            g = 0;
                            b = 0;
                        }
                    }

                    // Pressed state: slightly darker
                    if (pressed && a > 0)
                    {
                        r = (byte)(r * 0.7f);
                        g = (byte)(g * 0.7f);
                        b = (byte)(b * 0.7f);
                    }

                    pixels[index] = r;
                    pixels[index + 1] = g;
                    pixels[index + 2] = b;
                    pixels[index + 3] = a;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose all cached cursors
                foreach (ICursor cursor in _cursorCache.Values)
                {
                    cursor?.Dispose();
                }
                _cursorCache.Clear();
                _disposed = true;
            }
        }
    }
}

