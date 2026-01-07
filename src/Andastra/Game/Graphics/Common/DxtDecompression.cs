using System;

namespace Andastra.Runtime.Graphics.Common
{
    /// <summary>
    /// DXT (S3TC) texture decompression utility.
    /// Provides decompression for DXT1, DXT3, and DXT5 compressed texture formats.
    /// </summary>
    /// <remarks>
    /// DXT Decompression Utility:
    /// - Based on S3TC (S3 Texture Compression) block compression algorithm
    /// - Original implementation: DirectX handles DXT decompression automatically (swkotor.exe, swkotor2.exe)
    /// - DXT formats are block-compressed: 4x4 pixel blocks
    /// - DXT1: 8 bytes per block (RGB, 1-bit alpha for transparent mode)
    /// - DXT3: 16 bytes per block (RGB + explicit 4-bit alpha)
    /// - DXT5: 16 bytes per block (RGB + interpolated 8-bit alpha)
    /// - Reference implementations:
    ///   - vendor/xoreos/src/graphics/images/s3tc.cpp (DXT1/DXT3/DXT5 decompression)
    ///   - vendor/reone/src/libs/graphics/dxtutil.cpp (DXT1/DXT5 decompression)
    ///   - vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/tpc/convert/dxt/decompress_dxt.py
    /// - Algorithm matches original engine behavior: DirectX D3DXCreateTextureFromFileInMemory handles DXT natively
    /// - This utility: Software decompression for cases where hardware support is unavailable
    /// </remarks>
    public static class DxtDecompression
    {
        /// <summary>
        /// Decompresses DXT1 compressed texture data to RGBA format.
        /// </summary>
        /// <param name="input">Compressed DXT1 data (8 bytes per 4x4 block).</param>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <param name="output">Output RGBA data (width * height * 4 bytes). Must be pre-allocated.</param>
        /// <exception cref="ArgumentNullException">Thrown if input or output is null.</exception>
        /// <exception cref="ArgumentException">Thrown if output buffer is too small.</exception>
        /// <remarks>
        /// DXT1 Format:
        /// - 8 bytes per 4x4 pixel block
        /// - Bytes 0-1: Color0 (RGB565)
        /// - Bytes 2-3: Color1 (RGB565)
        /// - Bytes 4-7: Color indices (2 bits per pixel, 16 pixels)
        /// - If Color0 > Color1: 4-color mode (interpolated colors)
        /// - If Color0 <= Color1: 3-color + transparent mode
        /// - Based on swkotor.exe and swkotor2.exe: DirectX D3DXCreateTextureFromFileInMemory handles DXT1
        /// </remarks>
        public static void DecompressDxt1(byte[] input, int width, int height, byte[] output)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            if (output == null)
            {
                throw new ArgumentNullException("output");
            }
            if (output.Length < width * height * 4)
            {
                throw new ArgumentException("Output buffer too small", "output");
            }

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

        /// <summary>
        /// Decompresses DXT3 compressed texture data to RGBA format.
        /// </summary>
        /// <param name="input">Compressed DXT3 data (16 bytes per 4x4 block).</param>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <param name="output">Output RGBA data (width * height * 4 bytes). Must be pre-allocated.</param>
        /// <exception cref="ArgumentNullException">Thrown if input or output is null.</exception>
        /// <exception cref="ArgumentException">Thrown if output buffer is too small.</exception>
        /// <remarks>
        /// DXT3 Format:
        /// - 16 bytes per 4x4 pixel block
        /// - Bytes 0-7: Explicit alpha values (4 bits per pixel, 16 pixels)
        /// - Bytes 8-15: DXT1 color block (same as DXT1)
        /// - Alpha is stored explicitly (not interpolated)
        /// - Based on swkotor.exe and swkotor2.exe: DirectX handles DXT3 decompression
        /// </remarks>
        public static void DecompressDxt3(byte[] input, int width, int height, byte[] output)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            if (output == null)
            {
                throw new ArgumentNullException("output");
            }
            if (output.Length < width * height * 4)
            {
                throw new ArgumentException("Output buffer too small", "output");
            }

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
                            alphas[i * 4 + j] = (byte)(a | (a << 4)); // Expand 4-bit to 8-bit
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

        /// <summary>
        /// Decompresses DXT5 compressed texture data to RGBA format.
        /// </summary>
        /// <param name="input">Compressed DXT5 data (16 bytes per 4x4 block).</param>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <param name="output">Output RGBA data (width * height * 4 bytes). Must be pre-allocated.</param>
        /// <exception cref="ArgumentNullException">Thrown if input or output is null.</exception>
        /// <exception cref="ArgumentException">Thrown if output buffer is too small.</exception>
        /// <remarks>
        /// DXT5 Format:
        /// - 16 bytes per 4x4 pixel block
        /// - Bytes 0-1: Alpha endpoints (a0, a1)
        /// - Bytes 2-7: Alpha indices (3 bits per pixel, 16 pixels)
        /// - Bytes 8-15: DXT1 color block (same as DXT1)
        /// - Alpha is interpolated (similar to color interpolation in DXT1)
        /// - Based on swkotor.exe and swkotor2.exe: DirectX handles DXT5 decompression
        /// </remarks>
        public static void DecompressDxt5(byte[] input, int width, int height, byte[] output)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }
            if (output == null)
            {
                throw new ArgumentNullException("output");
            }
            if (output.Length < width * height * 4)
            {
                throw new ArgumentException("Output buffer too small", "output");
            }

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
                        // 8-alpha mode
                        alphaTable[2] = (byte)((6 * a0 + 1 * a1) / 7);
                        alphaTable[3] = (byte)((5 * a0 + 2 * a1) / 7);
                        alphaTable[4] = (byte)((4 * a0 + 3 * a1) / 7);
                        alphaTable[5] = (byte)((3 * a0 + 4 * a1) / 7);
                        alphaTable[6] = (byte)((2 * a0 + 5 * a1) / 7);
                        alphaTable[7] = (byte)((1 * a0 + 6 * a1) / 7);
                    }
                    else
                    {
                        // 6-alpha mode
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

        /// <summary>
        /// Decodes a 16-bit RGB565 color to RGBA8888 format.
        /// </summary>
        /// <param name="color">16-bit RGB565 color value.</param>
        /// <param name="output">Output array to write RGBA values to.</param>
        /// <param name="offset">Offset in output array to write to.</param>
        /// <remarks>
        /// RGB565 Format:
        /// - Bits 15-11: Red (5 bits)
        /// - Bits 10-5: Green (6 bits)
        /// - Bits 4-0: Blue (5 bits)
        /// - Decoded to 8-bit per channel with proper bit expansion
        /// </remarks>
        private static void DecodeColor565(ushort color, byte[] output, int offset)
        {
            int r = (color >> 11) & 0x1F;
            int g = (color >> 5) & 0x3F;
            int b = color & 0x1F;

            // Expand 5/6/5 bits to 8 bits per channel
            output[offset] = (byte)((r << 3) | (r >> 2));
            output[offset + 1] = (byte)((g << 2) | (g >> 4));
            output[offset + 2] = (byte)((b << 3) | (b >> 2));
            output[offset + 3] = 255; // Alpha always 255 for RGB565
        }
    }
}

