using System;
using Andastra.Parsing.Formats.TPC;
using Andastra.Runtime.Stride.Graphics;
using JetBrains.Annotations;
using Stride.Core.Mathematics;
using StrideGraphics = global::Stride.Graphics;

namespace Andastra.Runtime.Stride.Converters
{
    /// <summary>
    /// Converts Andastra.Parsing TPC texture data to Stride Graphics.Texture.
    /// Handles DXT1/DXT3/DXT5 compressed formats, RGB/RGBA uncompressed,
    /// and grayscale textures.
    /// </summary>
    /// <remarks>
    /// TPC to Stride Texture Converter:
    /// - Based on swkotor2.exe texture loading system (modern Stride adaptation)
    /// - Original game: DirectX 9 fixed-function pipeline (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
    /// - TPC format: BioWare texture format supporting DXT1/DXT3/DXT5 compression, RGB/RGBA, grayscale
    /// - Original engine: Uses DirectX texture creation APIs (D3DXCreateTextureFromFileInMemory, etc.)
    /// - This Stride implementation: Converts TPC format to Stride Graphics.Texture
    /// - Compression: Handles DXT compression formats, converts to RGBA for Stride compatibility
    /// - Mipmaps: Preserves mipmap chain from TPC or generates mipmaps if missing
    /// - Based on Stride Graphics API: Texture.New2D(GraphicsDevice, width, height, mipCount, PixelFormat)
    /// - Original game: swkotor2.exe: d3d9.dll texture loading @ 0x0080a6c0
    /// </remarks>
    public static class TpcToStrideTextureConverter
    {
        /// <summary>
        /// Converts a TPC texture to a Stride Graphics.Texture (2D texture).
        /// </summary>
        /// <param name="tpc">The TPC texture to convert.</param>
        /// <param name="device">The Stride graphics device.</param>
        /// <param name="commandList">The command list for texture operations (optional, will use device context if null).</param>
        /// <param name="generateMipmaps">Whether to generate mipmaps if not present.</param>
        /// <returns>A Stride Graphics.Texture ready for rendering.</returns>
        /// <remarks>
        /// Based on Stride API: Texture.New2D(GraphicsDevice, int width, int height, PixelFormat format, TextureFlags flags, int arraySize, ResourceUsage usage)
        /// Original game: DirectX 9 texture creation (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
        /// </remarks>
        public static StrideGraphics.Texture Convert(
            [NotNull] TPC tpc,
            [NotNull] StrideGraphics.GraphicsDevice device,
            [CanBeNull] StrideGraphics.CommandList commandList = null,
            bool generateMipmaps = true)
        {
            if (tpc == null)
            {
                throw new ArgumentNullException("tpc");
            }
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            if (tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
            {
                throw new ArgumentException("TPC has no texture data", "tpc");
            }

            // Get dimensions from first layer, first mipmap
            TPCMipmap baseMipmap = tpc.Layers[0].Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;
            TPCTextureFormat format = tpc.Format();

            // Handle cube maps - Not supported in this initial implementation
            // TPC cube maps have 6 layers, but Stride cube maps require special handling
            // For now, only support 2D textures
            if (tpc.IsCubeMap && tpc.Layers.Count == 6)
            {
                throw new NotSupportedException("Cube map textures are not yet supported in TpcToStrideTextureConverter");
            }

            // Convert standard 2D texture
            return Convert2DTexture(tpc.Layers[0], device, commandList, generateMipmaps);
        }

        /// <summary>
        /// Converts a TPC layer to a Stride 2D texture.
        /// </summary>
        private static StrideGraphics.Texture Convert2DTexture(
            TPCLayer layer,
            StrideGraphics.GraphicsDevice device,
            [CanBeNull] StrideGraphics.CommandList commandList,
            bool generateMipmaps)
        {
            TPCMipmap baseMipmap = layer.Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;

            // Determine mipmap count
            int mipmapCount = layer.Mipmaps.Count;
            if (generateMipmaps && mipmapCount == 1)
            {
                // Calculate mipmap count for generation
                int tempWidth = width;
                int tempHeight = height;
                while (tempWidth > 1 || tempHeight > 1)
                {
                    mipmapCount++;
                    tempWidth = Math.Max(1, tempWidth >> 1);
                    tempHeight = Math.Max(1, tempHeight >> 1);
                }
            }

            // Create Stride texture using Texture.New2D
            // Based on Stride API: Texture.New2D(GraphicsDevice device, int width, int height, int mipCount, PixelFormat format, TextureFlags flags)
            // PixelFormat.R8G8B8A8_UNorm for RGBA8888 format
            // TextureFlags.ShaderResource for textures used in shaders
            // Based on Stride Graphics API and usage in StrideOcclusionCuller.cs
            StrideGraphics.Texture texture = StrideGraphics.Texture.New2D(
                device,
                width,
                height,
                mipmapCount,
                StrideGraphics.PixelFormat.R8G8B8A8_UNorm,
                StrideGraphics.TextureFlags.ShaderResource
            );

            // Get or create command list
            StrideGraphics.CommandList cmd = commandList ?? device.ImmediateContext();

            // Convert each mipmap level to RGBA and upload to texture
            // Keep previous level RGBA data for progressive downsampling
            byte[] previousLevelRgba = null;
            int previousWidth = 0;
            int previousHeight = 0;

            for (int mipLevel = 0; mipLevel < mipmapCount; mipLevel++)
            {
                // Get or generate mipmap data
                byte[] rgbaData;
                int currentWidth;
                int currentHeight;

                if (mipLevel < layer.Mipmaps.Count)
                {
                    // Use existing mipmap
                    TPCMipmap mipmap = layer.Mipmaps[mipLevel];
                    currentWidth = mipmap.Width;
                    currentHeight = mipmap.Height;
                    rgbaData = ConvertMipmapToRgba(mipmap);
                    // Store for potential use in generating next level
                    previousLevelRgba = rgbaData;
                    previousWidth = currentWidth;
                    previousHeight = currentHeight;
                }
                else if (generateMipmaps && previousLevelRgba != null)
                {
                    // Generate mipmap by downsampling previous level
                    // Calculate next mip level dimensions
                    currentWidth = Math.Max(1, previousWidth >> 1);
                    currentHeight = Math.Max(1, previousHeight >> 1);

                    // Downsample using box filter (2x2 average) for proper mipmap generation
                    // Based on swkotor2.exe: Uses D3DXFilterTexture or similar for mipmap generation (box filter)
                    rgbaData = DownsampleMipmap(previousLevelRgba, previousWidth, previousHeight);
                    // Store for next iteration
                    previousLevelRgba = rgbaData;
                    previousWidth = currentWidth;
                    previousHeight = currentHeight;
                }
                else
                {
                    break; // No more mipmaps to process
                }

                // Convert RGBA byte array to Stride Color4 array
                // Based on Stride API: Color4 is the standard color type for textures
                Color4[] colorData = new Color4[currentWidth * currentHeight];
                for (int i = 0; i < colorData.Length; i++)
                {
                    int offset = i * 4;
                    if (offset + 3 < rgbaData.Length)
                    {
                        // Stride Color4 uses normalized floats (0.0-1.0), so convert from byte (0-255)
                        colorData[i] = new Color4(
                            rgbaData[offset] / 255.0f,     // R
                            rgbaData[offset + 1] / 255.0f, // G
                            rgbaData[offset + 2] / 255.0f, // B
                            rgbaData[offset + 3] / 255.0f  // A
                        );
                    }
                }

                // Set texture data for this mipmap level
                // Based on Stride API: Texture.SetData(CommandList, Color[] data, int mipLevel, int arraySlice)
                texture.SetData(cmd, colorData, mipLevel, 0);
            }

            return texture;
        }

        /// <summary>
        /// Converts a single TPC mipmap to RGBA format.
        /// Uses the same conversion logic as TpcToMonoGameTextureConverter.
        /// </summary>
        private static byte[] ConvertMipmapToRgba(TPCMipmap mipmap)
        {
            int width = mipmap.Width;
            int height = mipmap.Height;
            byte[] data = mipmap.Data;
            TPCTextureFormat format = mipmap.TpcFormat;
            byte[] output = new byte[width * height * 4];

            switch (format)
            {
                case TPCTextureFormat.RGBA:
                    Array.Copy(data, output, Math.Min(data.Length, output.Length));
                    break;

                case TPCTextureFormat.BGRA:
                    ConvertBgraToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.RGB:
                    ConvertRgbToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.BGR:
                    ConvertBgrToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.Greyscale:
                    ConvertGreyscaleToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.DXT1:
                    DecompressDxt1(data, output, width, height);
                    break;

                case TPCTextureFormat.DXT3:
                    DecompressDxt3(data, output, width, height);
                    break;

                case TPCTextureFormat.DXT5:
                    DecompressDxt5(data, output, width, height);
                    break;

                default:
                    // Fill with magenta to indicate error
                    for (int i = 0; i < output.Length; i += 4)
                    {
                        output[i] = 255;     // R
                        output[i + 1] = 0;   // G
                        output[i + 2] = 255; // B
                        output[i + 3] = 255; // A
                    }
                    break;
            }

            return output;
        }

        /// <summary>
        /// Downsamples an RGBA mipmap to half size using box filter (2x2 average).
        /// Based on swkotor2.exe texture mipmap generation (D3DXFilterTexture uses box filter).
        /// </summary>
        private static byte[] DownsampleMipmap(byte[] source, int width, int height)
        {
            // Mipmaps are always exactly half size
            int nextWidth = Math.Max(1, width >> 1);
            int nextHeight = Math.Max(1, height >> 1);
            byte[] target = new byte[nextWidth * nextHeight * 4];

            // Box filter: average 2x2 block of pixels
            for (int y = 0; y < nextHeight; y++)
            {
                for (int x = 0; x < nextWidth; x++)
                {
                    // Source coordinates (2x2 block starting at (x*2, y*2))
                    int srcX = x << 1;
                    int srcY = y << 1;

                    // Calculate indices for 2x2 block corners
                    int srcIdx00 = (srcY * width + srcX) * 4;           // Top-left
                    int srcIdx10 = (srcY * width + (srcX + 1)) * 4;     // Top-right
                    int srcIdx01 = ((srcY + 1) * width + srcX) * 4;     // Bottom-left
                    int srcIdx11 = ((srcY + 1) * width + (srcX + 1)) * 4; // Bottom-right

                    // Destination index
                    int dstIdx = (y * nextWidth + x) * 4;

                    // Average each RGBA component from 2x2 block
                    // Handle edge cases where source coordinates might be out of bounds
                    int pixelCount = 0;
                    int rSum = 0, gSum = 0, bSum = 0, aSum = 0;

                    // Top-left pixel
                    if (srcX < width && srcY < height && srcIdx00 + 3 < source.Length)
                    {
                        rSum += source[srcIdx00];
                        gSum += source[srcIdx00 + 1];
                        bSum += source[srcIdx00 + 2];
                        aSum += source[srcIdx00 + 3];
                        pixelCount++;
                    }

                    // Top-right pixel
                    if ((srcX + 1) < width && srcY < height && srcIdx10 + 3 < source.Length)
                    {
                        rSum += source[srcIdx10];
                        gSum += source[srcIdx10 + 1];
                        bSum += source[srcIdx10 + 2];
                        aSum += source[srcIdx10 + 3];
                        pixelCount++;
                    }

                    // Bottom-left pixel
                    if (srcX < width && (srcY + 1) < height && srcIdx01 + 3 < source.Length)
                    {
                        rSum += source[srcIdx01];
                        gSum += source[srcIdx01 + 1];
                        bSum += source[srcIdx01 + 2];
                        aSum += source[srcIdx01 + 3];
                        pixelCount++;
                    }

                    // Bottom-right pixel
                    if ((srcX + 1) < width && (srcY + 1) < height && srcIdx11 + 3 < source.Length)
                    {
                        rSum += source[srcIdx11];
                        gSum += source[srcIdx11 + 1];
                        bSum += source[srcIdx11 + 2];
                        aSum += source[srcIdx11 + 3];
                        pixelCount++;
                    }

                    // Calculate average (box filter)
                    if (pixelCount > 0 && dstIdx + 3 < target.Length)
                    {
                        target[dstIdx] = (byte)(rSum / pixelCount);
                        target[dstIdx + 1] = (byte)(gSum / pixelCount);
                        target[dstIdx + 2] = (byte)(bSum / pixelCount);
                        target[dstIdx + 3] = (byte)(aSum / pixelCount);
                    }
                }
            }

            return target;
        }

        // Conversion helper methods (same as TpcToMonoGameTextureConverter)
        private static void ConvertBgraToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 4;
                int dstIdx = i * 4;
                if (srcIdx + 3 < input.Length)
                {
                    output[dstIdx] = input[srcIdx + 2];     // R <- B
                    output[dstIdx + 1] = input[srcIdx + 1]; // G <- G
                    output[dstIdx + 2] = input[srcIdx];     // B <- R
                    output[dstIdx + 3] = input[srcIdx + 3]; // A <- A
                }
            }
        }

        private static void ConvertRgbToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < input.Length)
                {
                    output[dstIdx] = input[srcIdx];         // R
                    output[dstIdx + 1] = input[srcIdx + 1]; // G
                    output[dstIdx + 2] = input[srcIdx + 2]; // B
                    output[dstIdx + 3] = 255;               // A
                }
            }
        }

        private static void ConvertBgrToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < input.Length)
                {
                    output[dstIdx] = input[srcIdx + 2];     // R <- B
                    output[dstIdx + 1] = input[srcIdx + 1]; // G <- G
                    output[dstIdx + 2] = input[srcIdx];     // B <- R
                    output[dstIdx + 3] = 255;               // A
                }
            }
        }

        private static void ConvertGreyscaleToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                if (i < input.Length)
                {
                    byte grey = input[i];
                    int dstIdx = i * 4;
                    output[dstIdx] = grey;     // R
                    output[dstIdx + 1] = grey; // G
                    output[dstIdx + 2] = grey; // B
                    output[dstIdx + 3] = 255;  // A
                }
            }
        }

        #region DXT Decompression

        // DXT decompression methods - Based on swkotor2.exe texture loading system
        // Original game: DirectX 9 DXT texture decompression (swkotor2.exe: d3d9.dll @ 0x0080a6c0)
        private static void DecompressDxt1(byte[] input, byte[] output, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 8 > input.Length)
                    {
                        break;
                    }

                    // Read color endpoints
                    ushort c0 = (ushort)(input[srcOffset] | (input[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(input[srcOffset + 2] | (input[srcOffset + 3] << 8));
                    uint indices = (uint)(input[srcOffset + 4] | (input[srcOffset + 5] << 8) |
                                         (input[srcOffset + 6] << 16) | (input[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode colors
                    byte[] colors = new byte[16]; // 4 colors * 4 components
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    if (c0 > c1)
                    {
                        // 4-color mode
                        colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);
                        colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);
                        colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);
                        colors[11] = 255;

                        colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);
                        colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);
                        colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);
                        colors[15] = 255;
                    }
                    else
                    {
                        // 3-color + transparent mode
                        colors[8] = (byte)((colors[0] + colors[4]) / 2);
                        colors[9] = (byte)((colors[1] + colors[5]) / 2);
                        colors[10] = (byte)((colors[2] + colors[6]) / 2);
                        colors[11] = 255;

                        colors[12] = 0;
                        colors[13] = 0;
                        colors[14] = 0;
                        colors[15] = 0; // Transparent
                    }

                    // Write pixels
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;
                            if (x >= width || y >= height)
                            {
                                continue;
                            }

                            int idx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int dstOffset = (y * width + x) * 4;

                            output[dstOffset] = colors[idx * 4];
                            output[dstOffset + 1] = colors[idx * 4 + 1];
                            output[dstOffset + 2] = colors[idx * 4 + 2];
                            output[dstOffset + 3] = colors[idx * 4 + 3];
                        }
                    }
                }
            }
        }

        private static void DecompressDxt3(byte[] input, byte[] output, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > input.Length)
                    {
                        break;
                    }

                    // Read explicit alpha (8 bytes)
                    byte[] alphas = new byte[16];
                    for (int i = 0; i < 4; i++)
                    {
                        ushort row = (ushort)(input[srcOffset + i * 2] | (input[srcOffset + i * 2 + 1] << 8));
                        for (int j = 0; j < 4; j++)
                        {
                            int a = (row >> (j * 4)) & 0xF;
                            alphas[i * 4 + j] = (byte)(a | (a << 4));
                        }
                    }
                    srcOffset += 8;

                    // Read color block (same as DXT1)
                    ushort c0 = (ushort)(input[srcOffset] | (input[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(input[srcOffset + 2] | (input[srcOffset + 3] << 8));
                    uint indices = (uint)(input[srcOffset + 4] | (input[srcOffset + 5] << 8) |
                                         (input[srcOffset + 6] << 16) | (input[srcOffset + 7] << 24));
                    srcOffset += 8;

                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    // Always 4-color mode for DXT3/5
                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);
                    colors[11] = 255;

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);
                    colors[15] = 255;

                    // Write pixels
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;
                            if (x >= width || y >= height)
                            {
                                continue;
                            }

                            int idx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int dstOffset = (y * width + x) * 4;

                            output[dstOffset] = colors[idx * 4];
                            output[dstOffset + 1] = colors[idx * 4 + 1];
                            output[dstOffset + 2] = colors[idx * 4 + 2];
                            output[dstOffset + 3] = alphas[py * 4 + px];
                        }
                    }
                }
            }
        }

        private static void DecompressDxt5(byte[] input, byte[] output, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > input.Length)
                    {
                        break;
                    }

                    // Read interpolated alpha (8 bytes)
                    byte a0 = input[srcOffset];
                    byte a1 = input[srcOffset + 1];
                    ulong alphaIndices = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        alphaIndices |= (ulong)input[srcOffset + 2 + i] << (i * 8);
                    }
                    srcOffset += 8;

                    // Calculate alpha lookup table
                    byte[] alphaTable = new byte[8];
                    alphaTable[0] = a0;
                    alphaTable[1] = a1;
                    if (a0 > a1)
                    {
                        alphaTable[2] = (byte)((6 * a0 + 1 * a1) / 7);
                        alphaTable[3] = (byte)((5 * a0 + 2 * a1) / 7);
                        alphaTable[4] = (byte)((4 * a0 + 3 * a1) / 7);
                        alphaTable[5] = (byte)((3 * a0 + 4 * a1) / 7);
                        alphaTable[6] = (byte)((2 * a0 + 5 * a1) / 7);
                        alphaTable[7] = (byte)((1 * a0 + 6 * a1) / 7);
                    }
                    else
                    {
                        alphaTable[2] = (byte)((4 * a0 + 1 * a1) / 5);
                        alphaTable[3] = (byte)((3 * a0 + 2 * a1) / 5);
                        alphaTable[4] = (byte)((2 * a0 + 3 * a1) / 5);
                        alphaTable[5] = (byte)((1 * a0 + 4 * a1) / 5);
                        alphaTable[6] = 0;
                        alphaTable[7] = 255;
                    }

                    // Read color block
                    ushort c0 = (ushort)(input[srcOffset] | (input[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(input[srcOffset + 2] | (input[srcOffset + 3] << 8));
                    uint indices = (uint)(input[srcOffset + 4] | (input[srcOffset + 5] << 8) |
                                         (input[srcOffset + 6] << 16) | (input[srcOffset + 7] << 24));
                    srcOffset += 8;

                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);
                    colors[11] = 255;

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);
                    colors[15] = 255;

                    // Write pixels
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;
                            if (x >= width || y >= height)
                            {
                                continue;
                            }

                            int colorIdx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int alphaIdx = (int)((alphaIndices >> ((py * 4 + px) * 3)) & 7);
                            int dstOffset = (y * width + x) * 4;

                            output[dstOffset] = colors[colorIdx * 4];
                            output[dstOffset + 1] = colors[colorIdx * 4 + 1];
                            output[dstOffset + 2] = colors[colorIdx * 4 + 2];
                            output[dstOffset + 3] = alphaTable[alphaIdx];
                        }
                    }
                }
            }
        }

        private static void DecodeColor565(ushort color, byte[] output, int offset)
        {
            int r = (color >> 11) & 0x1F;
            int g = (color >> 5) & 0x3F;
            int b = color & 0x1F;

            output[offset] = (byte)((r << 3) | (r >> 2));
            output[offset + 1] = (byte)((g << 2) | (g >> 4));
            output[offset + 2] = (byte)((b << 3) | (b >> 2));
            output[offset + 3] = 255;
        }

        #endregion
    }
}

