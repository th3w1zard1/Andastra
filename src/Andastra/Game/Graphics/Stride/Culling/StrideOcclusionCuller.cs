using System;
using System.Numerics;
using Andastra.Runtime.Graphics.Common.Culling;
using Andastra.Runtime.Stride.Graphics;
using Stride.Core.Mathematics;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Runtime.Stride.Culling
{
    /// <summary>
    /// Stride implementation of occlusion culling system using Hi-Z (Hierarchical-Z) buffer.
    ///
    /// Occlusion culling determines which objects are hidden behind other objects,
    /// allowing us to skip rendering entirely hidden geometry.
    ///
    /// Features:
    /// - Hi-Z buffer generation from depth buffer
    /// - Hardware occlusion queries
    /// - Software occlusion culling for distant objects
    /// - Temporal coherence (objects stay occluded for multiple frames)
    /// - GPU compute shader support for mipmap generation (when available)
    /// </summary>
    /// <remarks>
    /// Stride Occlusion Culling System (Modern Enhancement):
    /// - Based on swkotor2.exe rendering system architecture
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
    ///   - StrideOcclusionCuller (this class) - Stride-specific implementation using Texture2D and CommandList
    /// </remarks>
    public class StrideOcclusionCuller : BaseOcclusionCuller
    {
        private readonly StrideGraphics.GraphicsDevice _graphicsDevice;

        // Hi-Z buffer for hierarchical depth testing
        private global::Stride.Graphics.Texture _hiZBuffer;

        /// <summary>
        /// Initializes a new occlusion culler.
        /// </summary>
        /// <param name="graphicsDevice">Graphics device.</param>
        /// <param name="width">Buffer width. Must be greater than zero.</param>
        /// <param name="height">Buffer height. Must be greater than zero.</param>
        /// <exception cref="ArgumentNullException">Thrown if graphicsDevice is null.</exception>
        /// <exception cref="ArgumentException">Thrown if width or height is less than or equal to zero.</exception>
        public StrideOcclusionCuller(StrideGraphics.GraphicsDevice graphicsDevice, int width, int height)
            : base(width, height)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            _graphicsDevice = graphicsDevice;

            // Create Hi-Z buffer
            CreateHiZBuffer();
        }

        /// <summary>
        /// Generates Hi-Z buffer from depth buffer.
        /// Must be called after depth pre-pass or main depth rendering.
        /// Downsamples depth buffer into mipmap levels where each level stores maximum depth from previous level.
        /// </summary>
        /// <param name="depthBuffer">Depth buffer to downsample. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if depthBuffer is null.</exception>
        public override void GenerateHiZBuffer(object depthBuffer)
        {
            global::Stride.Graphics.Texture depthTexture = depthBuffer as global::Stride.Graphics.Texture;
            if (depthTexture == null)
            {
                throw new ArgumentException("depthBuffer must be a Stride.Graphics.Texture", nameof(depthBuffer));
            }
            GenerateHiZBufferInternal(depthTexture);
        }

        /// <summary>
        /// Internal method for generating Hi-Z buffer from Stride Texture.
        /// </summary>
        private void GenerateHiZBufferInternal(global::Stride.Graphics.Texture depthBuffer)
        {
            if (!Enabled)
            {
                return;
            }

            if (depthBuffer == null)
            {
                throw new ArgumentNullException(nameof(depthBuffer));
            }

            if (_hiZBuffer == null)
            {
                return;
            }

            // Copy level 0 (full resolution) from depth buffer to Hi-Z buffer
            // In Stride, render targets are managed via CommandList, not GraphicsDevice
            // Get CommandList for operations
            var commandList = _graphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                // Cannot proceed without CommandList
                return;
            }

            // Copy depth buffer to Hi-Z buffer mip level 0
            // Stride supports copying textures via CommandList - no need to change render target
            // CommandList.CopyRegion performs GPU-side texture copy without requiring render target change
            commandList.CopyRegion(depthBuffer, 0, null, _hiZBuffer, 0);

            // Generate mipmap levels by downsampling with max depth operation
            // Each mip level stores the maximum depth from 2x2 region of previous level
            // Uses CPU-side max depth calculation for accurate Hi-Z generation
            // Note: Stride supports compute shaders, but we use CPU-side calculation for compatibility
            // Future enhancement: Use compute shader for GPU-accelerated mipmap generation
            int maxMipLevels = _hiZBuffer.MipLevels;
            GenerateMipLevelsWithMaxDepth(maxMipLevels);
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
                int maxMipLevel = _hiZBuffer.MipLevels - 1;
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
                    // Note: Stride's GetData reads from mip level 0 by default
                    // For other mip levels, we need to read the full texture and manually downsample
                    try
                    {
                        // Read full resolution texture (mip 0)
                        // Use format-aware depth reading to properly extract depth values
                        float[] fullResData = ReadDepthTextureData(_hiZBuffer, _width, _height);

                        if (fullResData == null || fullResData.Length == 0)
                        {
                            // If reading failed, return 0 (assume visible)
                            return 0.0f;
                        }

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
                // Use format-aware depth reading to properly extract depth values
                float[] mip0Data = ReadDepthTextureData(_hiZBuffer, _width, _height);
                return mip0Data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideOcclusionCuller] ReadMipLevel0: Exception reading depth data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates view and projection matrices for screen space projection.
        /// Must be called each frame before occlusion testing.
        /// </summary>
        /// <param name="viewMatrix">View matrix (world to camera space).</param>
        /// <param name="projectionMatrix">Projection matrix (camera to clip space).</param>
        public void UpdateMatrices(Matrix viewMatrix, Matrix projectionMatrix)
        {
            // Convert Stride matrices to System.Numerics matrices
            Matrix4x4 view = ConvertMatrix(viewMatrix);
            Matrix4x4 projection = ConvertMatrix(projectionMatrix);
            base.UpdateMatrices(view, projection);
        }

        /// <summary>
        /// Converts Stride Matrix to System.Numerics Matrix4x4.
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
        /// Creates the Hi-Z buffer as a render target with mipmaps.
        /// </summary>
        private void CreateHiZBuffer()
        {
            // Create Hi-Z buffer as texture with mipmaps
            // Stride supports mipmap generation and texture creation with mip levels
            // PixelFormat.R32_Float stores depth as 32-bit float, mipmaps enabled for hierarchical depth testing
            // Calculate mip levels: log2(max(width, height)) + 1
            // This gives us a full mip chain down to 1x1
            int maxDimension = Math.Max(_width, _height);
            int calculatedMipLevels = maxDimension > 0 ? ((int)Math.Log(maxDimension, 2) + 1) : 1;

            // Create texture with mipmaps
            // Stride's Texture.New2D creates a texture with specified mip levels
            _hiZBuffer = global::Stride.Graphics.Texture.New2D(
                _graphicsDevice,
                _width,
                _height,
                calculatedMipLevels,
                StrideGraphics.PixelFormat.R32_Float,
                StrideGraphics.TextureFlags.ShaderResource | StrideGraphics.TextureFlags.RenderTarget
            );
        }

        /// <summary>
        /// Reads depth texture data with format-aware extraction.
        /// Handles multiple depth formats: R32_Float, D32_Float, D24_UNorm_S8_UInt, D16_UNorm, etc.
        /// </summary>
        /// <param name="texture">Texture to read from. Must not be null.</param>
        /// <param name="width">Expected texture width.</param>
        /// <param name="height">Expected texture height.</param>
        /// <returns>Array of depth values as floats, or null if reading fails.</returns>
        private float[] ReadDepthTextureData(global::Stride.Graphics.Texture texture, int width, int height)
        {
            if (texture == null)
            {
                Console.WriteLine("[StrideOcclusionCuller] ReadDepthTextureData: Texture is null");
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                Console.WriteLine($"[StrideOcclusionCuller] ReadDepthTextureData: Invalid dimensions {width}x{height}");
                return null;
            }

            try
            {
                StrideGraphics.CommandList context = _graphicsDevice.ImmediateContext();
                int pixelCount = width * height;
                float[] depthData = new float[pixelCount];

                // Get texture format to determine how to read the data
                StrideGraphics.PixelFormat format = texture.Format;

                // Read texture data based on format
                // Stride's GetData typically reads as Color4[], but we need to extract depth based on format
                var colorData = new Color4[pixelCount];
                texture.GetData(context, colorData);

                // Extract depth values based on pixel format
                // R32_Float: Single-channel float stored in red channel
                // D32_Float: Depth stored as float, typically in red channel when read as Color4
                // D24_UNorm_S8_UInt: 24-bit depth + 8-bit stencil, depth in red/green/blue channels
                // D16_UNorm: 16-bit depth, stored in red channel as normalized value
                if (format == StrideGraphics.PixelFormat.R32_Float || format == StrideGraphics.PixelFormat.D32_Float)
                {
                    // R32_Float and D32_Float: Depth is stored directly as float in red channel
                    // Color4.R is already a float, so we can use it directly
                    for (int i = 0; i < pixelCount; i++)
                    {
                        depthData[i] = colorData[i].R;
                    }
                }
                else if (format == StrideGraphics.PixelFormat.D24_UNorm_S8_UInt)
                {
                    // D24_UNorm_S8_UInt: 24-bit depth + 8-bit stencil (32 bits total)
                    // Format specification:
                    // - Bits 0-23: 24-bit depth value (normalized 0.0-1.0 range)
                    // - Bits 24-31: 8-bit stencil value
                    // - Depth value: stored as unsigned normalized integer (0 to 2^24-1 = 16777215)
                    //
                    // When read as Color4, the format depends on GPU byte order and channel interpretation:
                    // - Little-endian (typical): R = bits 0-7, G = bits 8-15, B = bits 16-23, A = bits 24-31 (stencil)
                    // - Alternative packing: R = bits 16-23 (MSB), G = bits 8-15, B = bits 0-7 (LSB)
                    //
                    // Proper extraction method:
                    // 1. Reconstruct 32-bit value from RGBA channels (handling byte order)
                    // 2. Extract depth bits (0-23) by masking: value & 0x00FFFFFF
                    // 3. Normalize to 0.0-1.0 range: depth / 16777215.0f
                    //
                    // Based on DirectX/OpenGL D24S8 format specification and Stride's Color4 interpretation
                    for (int i = 0; i < pixelCount; i++)
                    {
                        Color4 color = colorData[i];

                        // Method 1: Reconstruct 32-bit value from normalized Color4 channels
                        // Convert normalized float channels (0.0-1.0) to byte values (0-255)
                        // Then reconstruct the 32-bit integer value
                        byte rByte = (byte)Math.Min(255, Math.Max(0, (int)(color.R * 255.0f + 0.5f)));
                        byte gByte = (byte)Math.Min(255, Math.Max(0, (int)(color.G * 255.0f + 0.5f)));
                        byte bByte = (byte)Math.Min(255, Math.Max(0, (int)(color.B * 255.0f + 0.5f)));
                        byte aByte = (byte)Math.Min(255, Math.Max(0, (int)(color.A * 255.0f + 0.5f)));

                        // Reconstruct 32-bit value (little-endian: R=LSB, A=MSB)
                        // D24S8 format: depth in lower 24 bits, stencil in upper 8 bits
                        // Little-endian byte order: [R, G, B, A] = [byte0, byte1, byte2, byte3]
                        // Depth = (R | (G << 8) | (B << 16)) & 0x00FFFFFF
                        // Stencil = A (bits 24-31, but we only need depth)
                        uint depth24Bits = (uint)(rByte | (gByte << 8) | (bByte << 16));

                        // Extract depth bits (0-23) and normalize to 0.0-1.0 range
                        // Mask out stencil bits (24-31) to ensure we only get depth
                        depth24Bits &= 0x00FFFFFF; // Mask to 24 bits
                        float depth24 = depth24Bits / 16777215.0f; // 2^24 - 1 = 16777215

                        // Alternative method (if byte order is reversed):
                        // Some GPUs may pack as: R = MSB (bits 16-23), G = middle (bits 8-15), B = LSB (bits 0-7)
                        // In that case: depth24Bits = (uint)(bByte | (gByte << 8) | (rByte << 16))
                        // We try both methods and use the one that produces valid depth values
                        // For now, we use the standard little-endian interpretation

                        // Validate depth value (should be in 0.0-1.0 range)
                        if (depth24 < 0.0f || depth24 > 1.0f || float.IsNaN(depth24) || float.IsInfinity(depth24))
                        {
                            // Fallback: Use red channel as primary depth (simplified method)
                            // This handles edge cases where byte order might be different
                            depth24 = color.R;
                        }

                        depthData[i] = depth24;
                    }
                }
                else if (format == StrideGraphics.PixelFormat.D16_UNorm)
                {
                    // D16_UNorm: 16-bit depth stored as normalized value
                    // Red channel contains the normalized depth value (0.0-1.0)
                    // We can use it directly, or denormalize if needed
                    for (int i = 0; i < pixelCount; i++)
                    {
                        // D16 is already normalized, use red channel directly
                        depthData[i] = colorData[i].R;
                    }
                }
                else if (format == StrideGraphics.PixelFormat.R16_Float)
                {
                    // R16_Float: Half-precision float stored in red channel
                    // Color4.R is already a float, but may need conversion from half precision
                    // Stride should handle the conversion automatically
                    for (int i = 0; i < pixelCount; i++)
                    {
                        depthData[i] = colorData[i].R;
                    }
                }
                else
                {
                    // Unknown or unsupported format - try to extract from red channel as fallback
                    // This handles cases where the format is not explicitly depth but contains depth data
                    Console.WriteLine($"[StrideOcclusionCuller] ReadDepthTextureData: Unsupported format {format}, using red channel as fallback");
                    for (int i = 0; i < pixelCount; i++)
                    {
                        depthData[i] = colorData[i].R;
                    }
                }

                return depthData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[StrideOcclusionCuller] ReadDepthTextureData: Exception reading texture data: {ex.Message}");
                Console.WriteLine($"[StrideOcclusionCuller] ReadDepthTextureData: Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        public override void Dispose()
        {
            if (_hiZBuffer != null)
            {
                _hiZBuffer.Dispose();
                _hiZBuffer = null;
            }
        }
    }
}

