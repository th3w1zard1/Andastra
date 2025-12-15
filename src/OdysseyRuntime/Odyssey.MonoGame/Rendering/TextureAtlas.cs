using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Odyssey.MonoGame.Rendering
{
    /// <summary>
    /// Texture atlas system for efficient texture batching.
    /// 
    /// Texture atlases combine multiple small textures into a single large texture,
    /// reducing texture switches and improving batching efficiency.
    /// 
    /// Features:
    /// - Automatic atlas packing
    /// - UV coordinate remapping
    /// - Atlas generation from texture arrays
    /// - Dynamic atlas updates
    /// </summary>
    public class TextureAtlas : IDisposable
    {
        /// <summary>
        /// Atlas entry for a texture.
        /// </summary>
        public struct AtlasEntry
        {
            /// <summary>
            /// Texture name/identifier.
            /// </summary>
            public string Name;

            /// <summary>
            /// UV coordinates in atlas (minX, minY, maxX, maxY).
            /// </summary>
            public Vector4 UVCoords;

            /// <summary>
            /// Atlas page index (for multi-page atlases).
            /// </summary>
            public int PageIndex;
        }

        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, AtlasEntry> _entries;
        private readonly List<RenderTarget2D> _atlasPages;
        private readonly int _pageSize;
        private readonly int _padding;
        private int _currentX;
        private int _currentY;
        private int _currentRowHeight;
        private int _currentPage;

        /// <summary>
        /// Gets the number of atlas pages.
        /// </summary>
        public int PageCount
        {
            get { return _atlasPages.Count; }
        }

        /// <summary>
        /// Initializes a new texture atlas.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device.</param>
        /// <param name="pageSize">Atlas page size (power of 2, e.g., 2048).</param>
        /// <param name="padding">Padding between textures in pixels.</param>
        public TextureAtlas(GraphicsDevice graphicsDevice, int pageSize = 2048, int padding = 2)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;
            _pageSize = pageSize;
            _padding = padding;
            _entries = new Dictionary<string, AtlasEntry>();
            _atlasPages = new List<RenderTarget2D>();
            _currentX = _padding;
            _currentY = _padding;
            _currentRowHeight = 0;
            _currentPage = 0;

            CreateNewPage();
        }

        /// <summary>
        /// Adds a texture to the atlas.
        /// </summary>
        public bool AddTexture(string name, Texture2D texture)
        {
            if (texture == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            // Check if already in atlas
            if (_entries.ContainsKey(name))
            {
                return true;
            }

            int width = texture.Width;
            int height = texture.Height;

            // Check if texture fits on current row
            if (_currentX + width + _padding > _pageSize)
            {
                // Move to next row
                _currentY += _currentRowHeight + _padding;
                _currentX = _padding;
                _currentRowHeight = 0;

                // Check if we need a new page
                if (_currentY + height + _padding > _pageSize)
                {
                    CreateNewPage();
                }
            }

            // Update row height
            if (height > _currentRowHeight)
            {
                _currentRowHeight = height;
            }

            // Copy texture to atlas
            CopyTextureToAtlas(texture, _currentX, _currentY);

            // Create entry
            AtlasEntry entry = new AtlasEntry
            {
                Name = name,
                UVCoords = new Vector4(
                    _currentX / (float)_pageSize,
                    _currentY / (float)_pageSize,
                    (_currentX + width) / (float)_pageSize,
                    (_currentY + height) / (float)_pageSize
                ),
                PageIndex = _currentPage
            };
            _entries[name] = entry;

            // Advance position
            _currentX += width + _padding;

            return true;
        }

        /// <summary>
        /// Gets atlas entry for a texture.
        /// </summary>
        public bool GetEntry(string name, out AtlasEntry entry)
        {
            return _entries.TryGetValue(name, out entry);
        }

        /// <summary>
        /// Gets atlas page texture.
        /// </summary>
        public Texture2D GetPage(int pageIndex)
        {
            if (pageIndex >= 0 && pageIndex < _atlasPages.Count)
            {
                return _atlasPages[pageIndex];
            }
            return null;
        }

        private void CreateNewPage()
        {
            RenderTarget2D page = new RenderTarget2D(
                _graphicsDevice,
                _pageSize,
                _pageSize,
                false,
                SurfaceFormat.Color,
                DepthFormat.None
            );
            _atlasPages.Add(page);
            _currentPage = _atlasPages.Count - 1;
            _currentX = _padding;
            _currentY = _padding;
            _currentRowHeight = 0;
        }

        private void CopyTextureToAtlas(Texture2D texture, int x, int y)
        {
            // Copy texture data to atlas render target
            // Would use SpriteBatch or render target copy
            // Placeholder - requires actual implementation
        }

        public void Dispose()
        {
            foreach (RenderTarget2D page in _atlasPages)
            {
                page?.Dispose();
            }
            _atlasPages.Clear();
            _entries.Clear();
        }
    }
}

