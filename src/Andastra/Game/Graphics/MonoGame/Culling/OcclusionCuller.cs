using System;
using System.Numerics;
using Andastra.Runtime.Graphics.Common.Culling;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Culling
{
    /// <summary>
    /// MonoGame implementation of occlusion culling system using Hi-Z (Hierarchical-Z) buffer.
    ///
    /// Occlusion culling determines which objects are hidden behind other objects,
    /// allowing us to skip rendering entirely hidden geometry.
    ///
    /// Features:
    /// - Hi-Z buffer generation from depth buffer
    /// - Hardware occlusion queries
    /// - Software occlusion culling for distant objects
    /// - Temporal coherence (objects stay occluded for multiple frames)
    /// </summary>
    /// <remarks>
    /// MonoGame Occlusion Culling System (Modern Enhancement):
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) rendering system architecture
    /// - Located via string references: Original engine uses VIS file-based room visibility culling
    /// - VIS file format: "%s/%s.VIS" @ 0x007b972c (VIS file path format), "visasmarr" @ 0x007bf720 (VIS file reference)
    /// - Original implementation: KOTOR uses VIS (visibility) files for room-based occlusion culling
    /// - VIS files: Pre-computed room-to-room visibility relationships for efficient occlusion culling
    /// - Original occlusion culling: Room-based visibility from VIS files combined with frustum culling
    /// - This is a modernization feature: Hi-Z buffer provides GPU-accelerated pixel-accurate occlusion testing
    /// - Modern enhancement: More accurate than VIS files for dynamic objects, requires modern GPU features
    /// - Original engine: DirectX 8/9 era, Hi-Z buffers not available, relied on VIS file pre-computation
    /// - Combined approaches: Modern renderer can use both VIS-based room culling + Hi-Z for best performance
    ///
    /// Inheritance:
    /// - BaseOcclusionCuller (Runtime.Graphics.Common.Culling) - Common occlusion culling logic
    ///   - MonoGameOcclusionCuller (this class) - MonoGame-specific implementation using RenderTarget2D and SpriteBatch
    /// </remarks>
    public class OcclusionCuller : BaseOcclusionCuller
    {
        private readonly GraphicsDevice _graphicsDevice;

        // Hi-Z buffer for hierarchical depth testing
        private RenderTarget2D _hiZBuffer;

        // SpriteBatch for downsampling depth buffer
        private SpriteBatch _spriteBatch;

        /// <summary>
        /// Initializes a new occlusion culler.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device.</param>
        /// <param name="width">Buffer width. Must be greater than zero.</param>
        /// <param name="height">Buffer height. Must be greater than zero.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        /// <exception cref="ArgumentException">Thrown if width or height is less than or equal to zero.</exception>
        public OcclusionCuller(GraphicsDevice graphicsDevice, int width, int height)
            : base(width, height)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;

            // Create SpriteBatch for downsampling
            _spriteBatch = new SpriteBatch(_graphicsDevice);

            // Create Hi-Z buffer
            CreateHiZBuffer();
        }

        /// <summary>
        /// Generates Hi-Z buffer from depth buffer.
        /// Must be called after depth pre-pass or main depth rendering.
        /// Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.SpriteBatch.html
        /// Downsamples depth buffer into mipmap levels where each level stores maximum depth from previous level.
        /// </summary>
        /// <param name="depthBuffer">Depth buffer to downsample. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if depthBuffer is null.</exception>
        public override void GenerateHiZBuffer(object depthBuffer)
        {
            Texture2D depthTexture = depthBuffer as Texture2D;
            if (depthTexture == null)
            {
                throw new ArgumentException("depthBuffer must be a Texture2D", nameof(depthBuffer));
            }
            GenerateHiZBufferInternal(depthTexture);
        }

        /// <summary>
        /// Internal method for generating Hi-Z buffer from MonoGame Texture2D.
        /// </summary>
        private void GenerateHiZBufferInternal(Texture2D depthBuffer)
        {
            if (!Enabled)
            {
                return;
            }

            if (depthBuffer == null)
            {
                throw new ArgumentNullException(nameof(depthBuffer));
            }

            if (_hiZBuffer == null || _spriteBatch == null)
            {
                return;
            }

            // Copy level 0 (full resolution) from depth buffer to Hi-Z buffer
            // Store current render target to restore later
            RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
            RenderTarget2D previousTarget = previousTargets.Length > 0 ?
                previousTargets[0].RenderTarget as RenderTarget2D : null;

            try
            {
                _graphicsDevice.SetRenderTarget(_hiZBuffer);
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                _spriteBatch.Draw(depthBuffer, new Rectangle(0, 0, _width, _height), Color.White);
                _spriteBatch.End();

                // Generate mipmap levels by downsampling with max depth operation
                // Each mip level stores the maximum depth from 2x2 region of previous level
                // Uses CPU-side max depth calculation for accurate Hi-Z generation
                // Note: MonoGame doesn't support rendering directly to specific mip levels,
                // so we maintain a CPU-side cache of mip levels with proper max depth values
                int maxMipLevels = _hiZBuffer != null ? _hiZBuffer.LevelCount : 1;
                GenerateMipLevelsWithMaxDepth(maxMipLevels);
            }
            finally
            {
                // Always restore previous render target, even if an exception occurs
                if (previousTarget != null)
                {
                    _graphicsDevice.SetRenderTarget(previousTarget);
                }
                else
                {
                    _graphicsDevice.SetRenderTarget(null);
                }
            }
        }


        /// <summary>
        /// Calculates the appropriate mip level based on screen space size.
        /// </summary>
        protected override int CalculateMipLevel(float screenSize)
        {
            if (screenSize > 0 && _hiZBuffer != null)
            {
                // Select mip level where one texel covers approximately the screen space region
                // This ensures we sample at a resolution that matches the object size
                float mipScale = Math.Max(_width, _height) / screenSize;
                int mipLevel = (int)Math.Floor(Math.Log(mipScale, 2));
                int maxMipLevel = _hiZBuffer.LevelCount - 1;
                return Math.Max(0, Math.Min(mipLevel, maxMipLevel));
            }
            return 0;
        }

        /// <summary>
        /// Samples Hi-Z buffer to get maximum depth in a screen space region.
        /// Uses CPU-side texture readback for sampling.
        /// </summary>
        /// <param name="minX">Minimum X coordinate in screen space.</param>
        /// <param name="minY">Minimum Y coordinate in screen space.</param>
        /// <param name="maxX">Maximum X coordinate in screen space.</param>
        /// <param name="maxY">Maximum Y coordinate in screen space.</param>
        /// <param name="mipLevel">Mip level to sample from.</param>
        /// <returns>Maximum depth value in the region, or 0 if sampling fails.</returns>
        protected override float SampleHiZBufferMaxDepth(int minX, int minY, int maxX, int maxY, int mipLevel)
        {
            if (_hiZBuffer == null)
            {
                return 0.0f;
            }

            // Calculate mip level dimensions
            int mipWidth = Math.Max(1, _width >> mipLevel);
            int mipHeight = Math.Max(1, _height >> mipLevel);

            // Convert screen space coordinates to mip level coordinates
            int mipMinX = Math.Max(0, Math.Min(minX >> mipLevel, mipWidth - 1));
            int mipMaxX = Math.Max(0, Math.Min(maxX >> mipLevel, mipWidth - 1));
            int mipMinY = Math.Max(0, Math.Min(minY >> mipLevel, mipHeight - 1));
            int mipMaxY = Math.Max(0, Math.Min(maxY >> mipLevel, mipHeight - 1));

            // Read Hi-Z buffer data if not cached or if mip level changed
            if (!_hiZBufferDataValid || _hiZBufferDataMipLevel != mipLevel)
            {
                // Allocate buffer if needed
                int pixelCount = mipWidth * mipHeight;
                if (_hiZBufferData == null || _hiZBufferData.Length < pixelCount)
                {
                    _hiZBufferData = new float[pixelCount];
                }

                // Use CPU-side mip level cache if available (contains proper max depth values)
                if (_mipLevelCacheValid && _mipLevelCache != null && mipLevel < _mipLevelCache.Length && _mipLevelCache[mipLevel] != null)
                {
                    // Copy from cache
                    float[] cachedData = _mipLevelCache[mipLevel];
                    int copyCount = Math.Min(cachedData.Length, _hiZBufferData.Length);
                    Array.Copy(cachedData, _hiZBufferData, copyCount);
                    _hiZBufferDataMipLevel = mipLevel;
                    _hiZBufferDataValid = true;
                }
                else
                {
                    // Fallback: Read from GPU and manually downsample
                    // Note: MonoGame's GetData reads from mip level 0 by default
                    // For other mip levels, we need to read the full texture and manually downsample
                    try
                    {
                        // Read full resolution texture (mip 0)
                        float[] fullResData = new float[_width * _height];
                        _hiZBuffer.GetData(fullResData);

                        // Downsample to mip level resolution by taking maximum of 2x2 regions
                        for (int y = 0; y < mipHeight; y++)
                        {
                            for (int x = 0; x < mipWidth; x++)
                            {
                                // Calculate source region in full resolution
                                int srcX = x << mipLevel;
                                int srcY = y << mipLevel;
                                int srcWidth = Math.Min(1 << mipLevel, _width - srcX);
                                int srcHeight = Math.Min(1 << mipLevel, _height - srcY);

                                // Find maximum depth in source region
                                float maxDepth = 0.0f;
                                for (int sy = 0; sy < srcHeight; sy++)
                                {
                                    for (int sx = 0; sx < srcWidth; sx++)
                                    {
                                        int srcIndex = (srcY + sy) * _width + (srcX + sx);
                                        if (srcIndex < fullResData.Length)
                                        {
                                            maxDepth = Math.Max(maxDepth, fullResData[srcIndex]);
                                        }
                                    }
                                }

                                // Store in mip level buffer
                                int mipIndex = y * mipWidth + x;
                                if (mipIndex < _hiZBufferData.Length)
                                {
                                    _hiZBufferData[mipIndex] = maxDepth;
                                }
                            }
                        }

                        _hiZBufferDataMipLevel = mipLevel;
                        _hiZBufferDataValid = true;
                    }
                    catch
                    {
                        // If readback fails (e.g., render target not readable), return 0 (assume visible)
                        return 0.0f;
                    }
                }
            }

            // Sample maximum depth in the region
            float regionMaxDepth = 0.0f;
            for (int y = mipMinY; y <= mipMaxY; y++)
            {
                for (int x = mipMinX; x <= mipMaxX; x++)
                {
                    int index = y * mipWidth + x;
                    if (index >= 0 && index < _hiZBufferData.Length)
                    {
                        regionMaxDepth = Math.Max(regionMaxDepth, _hiZBufferData[index]);
                    }
                }
            }

            return regionMaxDepth;
        }

        /// <summary>
        /// Updates view and projection matrices for screen space projection.
        /// Must be called each frame before occlusion testing.
        /// </summary>
        /// <param name="viewMatrix">View matrix (world to camera space).</param>
        /// <param name="projectionMatrix">Projection matrix (camera to clip space).</param>
        public void UpdateMatrices(Matrix viewMatrix, Matrix projectionMatrix)
        {
            // Convert MonoGame matrices to System.Numerics matrices
            Matrix4x4 view = ConvertMatrix(viewMatrix);
            Matrix4x4 projection = ConvertMatrix(projectionMatrix);
            base.UpdateMatrices(view, projection);
        }

        /// <summary>
        /// Converts MonoGame Matrix to System.Numerics Matrix4x4.
        /// </summary>
        private Matrix4x4 ConvertMatrix(Matrix matrix)
        {
            return new Matrix4x4(
                matrix.M11, matrix.M12, matrix.M13, matrix.M14,
                matrix.M21, matrix.M22, matrix.M23, matrix.M24,
                matrix.M31, matrix.M32, matrix.M33, matrix.M34,
                matrix.M41, matrix.M42, matrix.M43, matrix.M44
            );
        }

        /// <summary>
        /// Resizes the occlusion culler for new resolution.
        /// </summary>
        public override void Resize(int width, int height)
        {
            base.Resize(width, height);

            // Recreate Hi-Z buffer with new size (will recalculate mip levels automatically)
            if (_hiZBuffer != null)
            {
                _hiZBuffer.Dispose();
                _hiZBuffer = null;
            }

            // Recreate buffer with new dimensions (mip levels calculated dynamically)
            CreateHiZBuffer();
        }

        /// <summary>
        /// Checks if Hi-Z buffer is available.
        /// </summary>
        protected override bool HasHiZBuffer()
        {
            return _hiZBuffer != null;
        }

        /// <summary>
        /// Reads mip level 0 (full resolution) from the Hi-Z buffer.
        /// </summary>
        protected override float[] ReadMipLevel0()
        {
            if (_hiZBuffer == null)
            {
                return null;
            }

            try
            {
                float[] mip0Data = new float[_width * _height];
                _hiZBuffer.GetData(mip0Data);
                return mip0Data;
            }
            catch
            {
                return null;
            }
        }

        private void CreateHiZBuffer()
        {
            // Create Hi-Z buffer as render target with mipmaps
            // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.RenderTarget2D.html
            // RenderTarget2D(GraphicsDevice, int, int, bool, SurfaceFormat, DepthFormat, int, RenderTargetUsage, bool, int)
            // SurfaceFormat.Single stores depth as 32-bit float, mipmaps enabled for hierarchical depth testing
            // Calculate mip levels: log2(max(width, height)) + 1
            // This gives us a full mip chain down to 1x1
            int maxDimension = Math.Max(_width, _height);
            int calculatedMipLevels = maxDimension > 0 ? ((int)Math.Log(maxDimension, 2) + 1) : 1;

            _hiZBuffer = new RenderTarget2D(
                _graphicsDevice,
                _width,
                _height,
                false,
                SurfaceFormat.Single,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents,
                true,
                calculatedMipLevels
            );
        }

        public override void Dispose()
        {
            if (_hiZBuffer != null)
            {
                _hiZBuffer.Dispose();
                _hiZBuffer = null;
            }
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
                _spriteBatch = null;
            }
        }
    }
}

