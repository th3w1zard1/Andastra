using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Rendering
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
        private readonly SpriteBatch _spriteBatch;
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
            _spriteBatch = new SpriteBatch(graphicsDevice);
            _currentX = _padding;
            _currentY = _padding;
            _currentRowHeight = 0;
            _currentPage = 0;

            CreateNewPage();
        }

        /// <summary>
        /// Adds a texture to the atlas.
        /// </summary>
        /// <param name="name">Texture name/identifier. Must not be null or empty.</param>
        /// <param name="texture">Texture to add. Must not be null.</param>
        /// <returns>True if texture was added successfully, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if name or texture is null.</exception>
        /// <exception cref="ArgumentException">Thrown if name is empty or texture dimensions are invalid.</exception>
        public bool AddTexture(string name, Texture2D texture)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            if (texture.Width <= 0 || texture.Height <= 0)
            {
                throw new ArgumentException("Texture dimensions must be greater than zero.", nameof(texture));
            }
            if (texture.Width > _pageSize || texture.Height > _pageSize)
            {
                throw new ArgumentException($"Texture dimensions ({texture.Width}x{texture.Height}) exceed page size ({_pageSize}).", nameof(texture));
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
            entry = default(AtlasEntry);
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }
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

        /// <summary>
        /// Copies a texture to the atlas at the specified position.
        /// </summary>
        /// <param name="texture">Texture to copy.</param>
        /// <param name="x">X position in atlas.</param>
        /// <param name="y">Y position in atlas.</param>
        /// <remarks>
        /// Texture Atlas Copy Implementation:
        /// - Copies source texture to atlas render target using SpriteBatch
        /// - Saves and restores the current render target to avoid side effects
        /// - Uses opaque blending to ensure textures are copied without transparency issues
        /// - Based on standard MonoGame render target blitting pattern
        /// </remarks>
        private void CopyTextureToAtlas(Texture2D texture, int x, int y)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }
            if (_atlasPages.Count == 0)
            {
                throw new InvalidOperationException("No atlas pages available.");
            }
            if (_currentPage < 0 || _currentPage >= _atlasPages.Count)
            {
                throw new InvalidOperationException($"Invalid current page index: {_currentPage}");
            }

            RenderTarget2D currentPage = _atlasPages[_currentPage];
            if (currentPage == null)
            {
                throw new InvalidOperationException("Current atlas page is null.");
            }

            // Save the current render target state
            RenderTargetBinding[] previousRenderTargets = _graphicsDevice.GetRenderTargets();

            try
            {
                // Set the atlas page as the render target
                _graphicsDevice.SetRenderTarget(currentPage);

                // Begin sprite batch with opaque blending for accurate texture copying
                _spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.Opaque,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone
                );

                // Draw the texture at the specified position
                // Use PointClamp to avoid filtering artifacts when copying
                Rectangle destinationRectangle = new Rectangle(x, y, texture.Width, texture.Height);
                _spriteBatch.Draw(texture, destinationRectangle, Color.White);

                // End sprite batch
                _spriteBatch.End();
            }
            finally
            {
                // Restore the previous render target state
                if (previousRenderTargets != null && previousRenderTargets.Length > 0)
                {
                    _graphicsDevice.SetRenderTargets(previousRenderTargets);
                }
                else
                {
                    _graphicsDevice.SetRenderTarget(null);
                }
            }
        }

        public void Dispose()
        {
            _spriteBatch?.Dispose();
            foreach (RenderTarget2D page in _atlasPages)
            {
                page?.Dispose();
            }
            _atlasPages.Clear();
            _entries.Clear();
        }
    }
}

