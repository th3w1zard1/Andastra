using System;
using Stride.Graphics;
using CSharpKOTOR.Formats.TPC;
using JetBrains.Annotations;

namespace Odyssey.Stride.Converters
{
    /// <summary>
    /// Converts CSharpKOTOR TPC texture data to Stride Graphics Texture.
    /// Handles DXT1/DXT3/DXT5 compressed formats, RGB/RGBA uncompressed,
    /// and grayscale textures.
    /// </summary>
    public static class TpcToStrideTextureConverter
    {
        /// <summary>
        /// Converts a TPC texture to a Stride Texture.
        /// </summary>
        /// <param name="tpc">The TPC texture to convert.</param>
        /// <param name="device">The graphics device.</param>
        /// <param name="generateMipmaps">Whether to generate mipmaps if not present.</param>
        /// <returns>A Stride Texture ready for rendering.</returns>
        public static Texture Convert([NotNull] TPC tpc, [NotNull] GraphicsDevice device, bool generateMipmaps = true)
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
            var baseMipmap = tpc.Layers[0].Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;
            TPCTextureFormat format = tpc.Format();

            // Handle cube maps
            if (tpc.IsCubeMap && tpc.Layers.Count == 6)
            {
                return ConvertCubeMap(tpc, device);
            }

            // Convert standard 2D texture
            return Convert2DTexture(tpc, device, generateMipmaps);
        }

        /// <summary>
        /// Converts a TPC texture to RGBA byte array for manual processing.
        /// </summary>
        public static byte[] ConvertToRgba([NotNull] TPC tpc)
        {
            if (tpc == null)
            {
                throw new ArgumentNullException("tpc");
            }

            if (tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
            {
                return new byte[0];
            }

            var mipmap = tpc.Layers[0].Mipmaps[0];
            return ConvertMipmapToRgba(mipmap);
        }

        private static Texture Convert2DTexture(TPC tpc, GraphicsDevice device, bool generateMipmaps)
        {
            var layer = tpc.Layers[0];
            var baseMipmap = layer.Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;
            TPCTextureFormat format = tpc.Format();

            // Determine Stride pixel format
            PixelFormat strideFormat = GetStridePixelFormat(format);

            // Check if we can use compressed format directly
            if (format.IsDxt() && SupportsCompressedFormat(device, strideFormat))
            {
                return CreateCompressedTexture(tpc, device, strideFormat);
            }

            // Convert to RGBA for uncompressed upload
            byte[] rgbaData = ConvertMipmapToRgba(baseMipmap);

            // Create texture description
            var desc = TextureDescription.New2D(
                width,
                height,
                generateMipmaps ? CalculateMipmapCount(width, height) : 1,
                PixelFormat.R8G8B8A8_UNorm,
                TextureFlags.ShaderResource);

            // Create the texture with data
            var texture = Texture.New2D(device, width, height, PixelFormat.R8G8B8A8_UNorm, rgbaData);
            return texture;
        }

        private static Texture ConvertCubeMap(TPC tpc, GraphicsDevice device)
        {
            if (tpc.Layers.Count != 6)
            {
                throw new ArgumentException("Cube map must have exactly 6 layers", "tpc");
            }

            var baseMipmap = tpc.Layers[0].Mipmaps[0];
            int size = baseMipmap.Width;

            // Convert all faces to RGBA
            byte[][] faceData = new byte[6][];
            for (int i = 0; i < 6; i++)
            {
                if (tpc.Layers[i].Mipmaps.Count == 0)
                {
                    throw new ArgumentException(string.Format("Cube map layer {0} has no mipmaps", i), "tpc");
                }
                faceData[i] = ConvertMipmapToRgba(tpc.Layers[i].Mipmaps[0]);
            }

            // Create cube map texture
            var desc = TextureDescription.NewCube(
                size,
                CalculateMipmapCount(size, size),
                PixelFormat.R8G8B8A8_UNorm,
                TextureFlags.ShaderResource);

            // TODO: Cube map upload needs proper CommandList handling
            // For now, return a simple 2D texture from face 0
            return Texture.New2D(device, size, size, PixelFormat.R8G8B8A8_UNorm, faceData[0]);
        }

        private static Texture CreateCompressedTexture(TPC tpc, GraphicsDevice device, PixelFormat format)
        {
            var layer = tpc.Layers[0];
            var baseMipmap = layer.Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;
            int mipmapCount = layer.Mipmaps.Count;

            var desc = TextureDescription.New2D(
                width,
                height,
                mipmapCount,
                format,
                TextureFlags.ShaderResource);

            // TODO: Compressed texture upload needs proper CommandList handling  
            // For now, decompress base mipmap to RGBA
            byte[] rgbaData = ConvertMipmapToRgba(baseMipmap);
            return Texture.New2D(device, width, height, PixelFormat.R8G8B8A8_UNorm, rgbaData);
        }

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

        private static PixelFormat GetStridePixelFormat(TPCTextureFormat format)
        {
            switch (format)
            {
                case TPCTextureFormat.DXT1:
                    return PixelFormat.BC1_UNorm;
                case TPCTextureFormat.DXT3:
                    return PixelFormat.BC2_UNorm;
                case TPCTextureFormat.DXT5:
                    return PixelFormat.BC3_UNorm;
                case TPCTextureFormat.RGBA:
                case TPCTextureFormat.BGRA:
                    return PixelFormat.R8G8B8A8_UNorm;
                case TPCTextureFormat.RGB:
                case TPCTextureFormat.BGR:
                    return PixelFormat.R8G8B8A8_UNorm;
                case TPCTextureFormat.Greyscale:
                    return PixelFormat.R8_UNorm;
                default:
                    return PixelFormat.R8G8B8A8_UNorm;
            }
        }

        private static bool SupportsCompressedFormat(GraphicsDevice device, PixelFormat format)
        {
            // For now, always decompress to ensure compatibility
            // Can be extended to check device capabilities
            return false;
        }

        private static int CalculateMipmapCount(int width, int height)
        {
            int count = 1;
            while (width > 1 || height > 1)
            {
                width = Math.Max(1, width / 2);
                height = Math.Max(1, height / 2);
                count++;
            }
            return count;
        }
    }
}

