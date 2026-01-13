using System;
using System.IO;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tga.py:238-301
    // Original: class TPCTGAWriter(ResourceWriter)
    public class TPCTGAWriter : IDisposable
    {
        private readonly TPC _tpc;
        private readonly RawBinaryWriter _writer;

        public TPCTGAWriter(TPC tpc, string filepath)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public TPCTGAWriter(TPC tpc, Stream target)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public TPCTGAWriter(TPC tpc)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToByteArray(null);
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                if (_tpc == null)
                {
                    throw new ArgumentException("TPC instance is not set.");
                }
                if (_tpc.Layers == null || _tpc.Layers.Count == 0 || _tpc.Layers[0].Mipmaps.Count == 0)
                {
                    throw new ArgumentException("TPC contains no mipmaps to write as TGA.");
                }

                TPCMipmap baseMip = _tpc.Layers[0].Mipmaps[0];
                int frameWidth = baseMip.Width;
                int frameHeight = baseMip.Height;

                byte[] canvas;

                if (_tpc.IsAnimated)
                {
                    // Handle animated flipbook
                    int numx = 1, numy = 1;
                    if (_tpc.TxiObject != null && _tpc.TxiObject.Features != null)
                    {
                        numx = Math.Max(1, _tpc.TxiObject.Features.Numx ?? 0);
                        numy = Math.Max(1, _tpc.TxiObject.Features.Numy ?? 0);
                    }
                    if (numx * numy != _tpc.Layers.Count)
                    {
                        numx = _tpc.Layers.Count;
                        numy = 1;
                    }
                    int width = frameWidth * numx;
                    int height = frameHeight * numy;
                    canvas = new byte[width * height * 4];

                    for (int index = 0; index < _tpc.Layers.Count; index++)
                    {
                        TPCLayer layer = _tpc.Layers[index];
                        byte[] rgbaFrame = DecodeMipmapToRgba(layer.Mipmaps[0]);
                        int tileX = index % numx;
                        int tileY = index / numx;
                        for (int row = 0; row < frameHeight; row++)
                        {
                            int src = row * frameWidth * 4;
                            int dstRow = tileY * frameHeight + row;
                            int dst = (dstRow * width + tileX * frameWidth) * 4;
                            Array.Copy(rgbaFrame, src, canvas, dst, frameWidth * 4);
                        }
                    }
                    WriteTgaRgba(width, height, canvas);
                }
                else if (_tpc.IsCubeMap)
                {
                    // Handle cube map
                    int width = frameWidth;
                    int height = frameHeight * _tpc.Layers.Count;
                    canvas = new byte[width * height * 4];
                    for (int index = 0; index < _tpc.Layers.Count; index++)
                    {
                        TPCLayer layer = _tpc.Layers[index];
                        byte[] rgbaFace = DecodeMipmapToRgba(layer.Mipmaps[0]);
                        for (int row = 0; row < frameHeight; row++)
                        {
                            int src = row * width * 4;
                            int dstRow = index * frameHeight + row;
                            int dst = dstRow * width * 4;
                            Array.Copy(rgbaFace, src, canvas, dst, width * 4);
                        }
                    }
                    WriteTgaRgba(width, height, canvas);
                }
                else
                {
                    // Single frame
                    byte[] rgba = DecodeMipmapToRgba(_tpc.Layers[0].Mipmaps[0]);
                    WriteTgaRgba(frameWidth, frameHeight, rgba);
                }
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        /// <summary>
        /// Decodes a TPC mipmap to RGBA format for TGA writing.
        /// Handles all TPC texture formats: RGBA, RGB, BGRA, BGR, Greyscale, DXT1, DXT3, DXT5.
        /// Based on PyKotor implementation and standard texture format conversion algorithms.
        /// </summary>
        /// <param name="mipmap">The mipmap to decode.</param>
        /// <returns>RGBA pixel data as byte array (width * height * 4 bytes).</returns>
        private byte[] DecodeMipmapToRgba(TPCMipmap mipmap)
        {
            if (mipmap == null)
            {
                throw new ArgumentNullException(nameof(mipmap));
            }

            int width = mipmap.Width;
            int height = mipmap.Height;
            byte[] data = mipmap.Data;
            TPCTextureFormat format = mipmap.TpcFormat;
            byte[] rgba = new byte[width * height * 4];

            switch (format)
            {
                case TPCTextureFormat.RGBA:
                    // Already in RGBA format - just copy
                    Array.Copy(data, rgba, Math.Min(data.Length, rgba.Length));
                    break;

                case TPCTextureFormat.RGB:
                    // Convert RGB to RGBA (add alpha channel with value 255)
                    ConvertRgbToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.BGRA:
                    // Convert BGRA to RGBA (swap R and B channels)
                    ConvertBgraToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.BGR:
                    // Convert BGR to RGBA (swap R and B, add alpha)
                    ConvertBgrToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.Greyscale:
                    // Convert greyscale to RGBA (replicate grey value to RGB, add alpha)
                    ConvertGreyscaleToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.DXT1:
                    // Decompress DXT1 to RGBA
                    DecompressDxt1ToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.DXT3:
                    // Decompress DXT3 to RGBA
                    DecompressDxt3ToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.DXT5:
                    // Decompress DXT5 to RGBA
                    DecompressDxt5ToRgba(data, rgba, width, height);
                    break;

                default:
                    // Unknown format - fill with magenta to indicate error
                    for (int i = 0; i < rgba.Length; i += 4)
                    {
                        rgba[i] = 255;     // R
                        rgba[i + 1] = 0;   // G
                        rgba[i + 2] = 255; // B
                        rgba[i + 3] = 255; // A
                    }
                    break;
            }

            return rgba;
        }

        /// <summary>
        /// Converts RGB data to RGBA format by adding an alpha channel (255).
        /// </summary>
        private void ConvertRgbToRgba(byte[] rgb, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < rgb.Length)
                {
                    rgba[dstIdx] = rgb[srcIdx];         // R
                    rgba[dstIdx + 1] = rgb[srcIdx + 1]; // G
                    rgba[dstIdx + 2] = rgb[srcIdx + 2]; // B
                    rgba[dstIdx + 3] = 255;             // A (full opacity)
                }
            }
        }

        /// <summary>
        /// Converts BGRA data to RGBA format by swapping R and B channels.
        /// </summary>
        private void ConvertBgraToRgba(byte[] bgra, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 4;
                int dstIdx = i * 4;
                if (srcIdx + 3 < bgra.Length)
                {
                    rgba[dstIdx] = bgra[srcIdx + 2];     // R <- B
                    rgba[dstIdx + 1] = bgra[srcIdx + 1]; // G <- G
                    rgba[dstIdx + 2] = bgra[srcIdx];     // B <- R
                    rgba[dstIdx + 3] = bgra[srcIdx + 3]; // A <- A
                }
            }
        }

        /// <summary>
        /// Converts BGR data to RGBA format by swapping R and B channels and adding alpha.
        /// </summary>
        private void ConvertBgrToRgba(byte[] bgr, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < bgr.Length)
                {
                    rgba[dstIdx] = bgr[srcIdx + 2];     // R <- B
                    rgba[dstIdx + 1] = bgr[srcIdx + 1]; // G <- G
                    rgba[dstIdx + 2] = bgr[srcIdx];     // B <- R
                    rgba[dstIdx + 3] = 255;             // A (full opacity)
                }
            }
        }

        /// <summary>
        /// Converts greyscale data to RGBA format by replicating the grey value to RGB channels.
        /// </summary>
        private void ConvertGreyscaleToRgba(byte[] grey, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                if (i < grey.Length)
                {
                    byte greyValue = grey[i];
                    int dstIdx = i * 4;
                    rgba[dstIdx] = greyValue;     // R
                    rgba[dstIdx + 1] = greyValue; // G
                    rgba[dstIdx + 2] = greyValue; // B
                    rgba[dstIdx + 3] = 255;       // A (full opacity)
                }
            }
        }

        /// <summary>
        /// Decompresses DXT1 (BC1) compressed texture data to RGBA format.
        /// Based on standard DXT/S3TC decompression algorithm.
        /// DXT1 uses 4x4 pixel blocks with 8 bytes per block (2 color endpoints + 16 2-bit indices).
        /// </summary>
        private void DecompressDxt1ToRgba(byte[] dxt1, byte[] rgba, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 8 > dxt1.Length)
                    {
                        break;
                    }

                    // Read color endpoints (16-bit RGB565 format)
                    ushort c0 = (ushort)(dxt1[srcOffset] | (dxt1[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(dxt1[srcOffset + 2] | (dxt1[srcOffset + 3] << 8));
                    uint indices = (uint)(dxt1[srcOffset + 4] | (dxt1[srcOffset + 5] << 8) |
                                         (dxt1[srcOffset + 6] << 16) | (dxt1[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode color endpoints from RGB565
                    byte[] colors = new byte[16]; // 4 colors * 4 components (RGBA)
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    // Calculate interpolated colors
                    if (c0 > c1)
                    {
                        // 4-color mode: interpolate between c0 and c1
                        colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);   // R
                        colors[9] = (byte)((2 * colors[1] + colors[5]) / 3); // G
                        colors[10] = (byte)((2 * colors[2] + colors[6]) / 3); // B
                        colors[11] = 255; // A

                        colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);   // R
                        colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);   // G
                        colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);   // B
                        colors[15] = 255; // A
                    }
                    else
                    {
                        // 3-color + transparent mode
                        colors[8] = (byte)((colors[0] + colors[4]) / 2);   // R
                        colors[9] = (byte)((colors[1] + colors[5]) / 2);   // G
                        colors[10] = (byte)((colors[2] + colors[6]) / 2);   // B
                        colors[11] = 255; // A

                        colors[12] = 0; // R (transparent)
                        colors[13] = 0; // G
                        colors[14] = 0; // B
                        colors[15] = 0; // A (transparent)
                    }

                    // Write pixels for this 4x4 block
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

                            // Extract 2-bit color index for this pixel
                            int idx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int dstOffset = (y * width + x) * 4;

                            rgba[dstOffset] = colors[idx * 4];         // R
                            rgba[dstOffset + 1] = colors[idx * 4 + 1]; // G
                            rgba[dstOffset + 2] = colors[idx * 4 + 2]; // B
                            rgba[dstOffset + 3] = colors[idx * 4 + 3]; // A
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decompresses DXT3 (BC2) compressed texture data to RGBA format.
        /// Based on standard DXT/S3TC decompression algorithm.
        /// DXT3 uses 4x4 pixel blocks with 16 bytes per block (8 bytes explicit alpha + 8 bytes DXT1 Color).
        /// </summary>
        private void DecompressDxt3ToRgba(byte[] dxt3, byte[] rgba, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > dxt3.Length)
                    {
                        break;
                    }

                    // Read explicit alpha values (8 bytes, 4 bits per pixel)
                    byte[] alphas = new byte[16];
                    for (int i = 0; i < 4; i++)
                    {
                        ushort row = (ushort)(dxt3[srcOffset + i * 2] | (dxt3[srcOffset + i * 2 + 1] << 8));
                        for (int j = 0; j < 4; j++)
                        {
                            int a = (row >> (j * 4)) & 0xF;
                            // Expand 4-bit alpha to 8-bit: a * 17 (0x11) = a | (a << 4)
                            alphas[i * 4 + j] = (byte)(a | (a << 4));
                        }
                    }
                    srcOffset += 8;

                    // Read color block (same as DXT1)
                    ushort c0 = (ushort)(dxt3[srcOffset] | (dxt3[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(dxt3[srcOffset + 2] | (dxt3[srcOffset + 3] << 8));
                    uint indices = (uint)(dxt3[srcOffset + 4] | (dxt3[srcOffset + 5] << 8) |
                                         (dxt3[srcOffset + 6] << 16) | (dxt3[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode colors (always 4-color mode for DXT3/5)
                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);   // R
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);   // G
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);  // B
                    colors[11] = 255; // A

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);   // R
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);   // G
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);   // B
                    colors[15] = 255; // A

                    // Write pixels for this 4x4 block
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
                            int dstOffset = (y * width + x) * 4;

                            rgba[dstOffset] = colors[colorIdx * 4];         // R
                            rgba[dstOffset + 1] = colors[colorIdx * 4 + 1]; // G
                            rgba[dstOffset + 2] = colors[colorIdx * 4 + 2]; // B
                            rgba[dstOffset + 3] = alphas[py * 4 + px];      // A (from explicit alpha)
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decompresses DXT5 (BC3) compressed texture data to RGBA format.
        /// Based on standard DXT/S3TC decompression algorithm.
        /// DXT5 uses 4x4 pixel blocks with 16 bytes per block (8 bytes interpolated alpha + 8 bytes DXT1 Color).
        /// </summary>
        private void DecompressDxt5ToRgba(byte[] dxt5, byte[] rgba, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > dxt5.Length)
                    {
                        break;
                    }

                    // Read interpolated alpha endpoints and indices
                    byte a0 = dxt5[srcOffset];
                    byte a1 = dxt5[srcOffset + 1];
                    ulong alphaIndices = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        alphaIndices |= (ulong)dxt5[srcOffset + 2 + i] << (i * 8);
                    }
                    srcOffset += 8;

                    // Calculate alpha lookup table (8 alpha values)
                    byte[] alphaTable = new byte[8];
                    alphaTable[0] = a0;
                    alphaTable[1] = a1;
                    if (a0 > a1)
                    {
                        // 8-alpha mode: interpolate 6 intermediate values
                        alphaTable[2] = (byte)((6 * a0 + 1 * a1) / 7);
                        alphaTable[3] = (byte)((5 * a0 + 2 * a1) / 7);
                        alphaTable[4] = (byte)((4 * a0 + 3 * a1) / 7);
                        alphaTable[5] = (byte)((3 * a0 + 4 * a1) / 7);
                        alphaTable[6] = (byte)((2 * a0 + 5 * a1) / 7);
                        alphaTable[7] = (byte)((1 * a0 + 6 * a1) / 7);
                    }
                    else
                    {
                        // 6-alpha mode: interpolate 4 intermediate values, 0 and 255 reserved
                        alphaTable[2] = (byte)((4 * a0 + 1 * a1) / 5);
                        alphaTable[3] = (byte)((3 * a0 + 2 * a1) / 5);
                        alphaTable[4] = (byte)((2 * a0 + 3 * a1) / 5);
                        alphaTable[5] = (byte)((1 * a0 + 4 * a1) / 5);
                        alphaTable[6] = 0;   // Reserved
                        alphaTable[7] = 255; // Reserved
                    }

                    // Read color block (same as DXT1)
                    ushort c0 = (ushort)(dxt5[srcOffset] | (dxt5[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(dxt5[srcOffset + 2] | (dxt5[srcOffset + 3] << 8));
                    uint indices = (uint)(dxt5[srcOffset + 4] | (dxt5[srcOffset + 5] << 8) |
                                         (dxt5[srcOffset + 6] << 16) | (dxt5[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode colors (always 4-color mode for DXT3/5)
                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);   // R
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);   // G
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);  // B
                    colors[11] = 255; // A

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);   // R
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);   // G
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);   // B
                    colors[15] = 255; // A

                    // Write pixels for this 4x4 block
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

                            rgba[dstOffset] = colors[colorIdx * 4];         // R
                            rgba[dstOffset + 1] = colors[colorIdx * 4 + 1]; // G
                            rgba[dstOffset + 2] = colors[colorIdx * 4 + 2]; // B
                            rgba[dstOffset + 3] = alphaTable[alphaIdx];     // A (from interpolated alpha)
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decodes a 16-bit RGB565 color value to RGBA format.
        /// RGB565 format: 5 bits red, 6 bits green, 5 bits blue.
        /// </summary>
        /// <param name="color565">16-bit RGB565 color value.</param>
        /// <param name="output">Output byte array to write RGBA values to.</param>
        /// <param name="offset">Offset in output array to write to.</param>
        private void DecodeColor565(ushort color565, byte[] output, int offset)
        {
            // Extract color components from RGB565 format
            // Bit layout: RRRRR GGGGGG BBBBB (5 bits R, 6 bits G, 5 bits B)
            int r = (color565 >> 11) & 0x1F; // 5 bits
            int g = (color565 >> 5) & 0x3F;  // 6 bits
            int b = color565 & 0x1F;          // 5 bits

            // Expand to 8 bits per channel
            // For 5-bit: multiply by 8 and add top 3 bits (replicate MSB)
            // For 6-bit: multiply by 4 and add top 2 bits (replicate MSB)
            output[offset] = (byte)((r << 3) | (r >> 2));         // R: 5 bits -> 8 bits
            output[offset + 1] = (byte)((g << 2) | (g >> 4));     // G: 6 bits -> 8 bits
            output[offset + 2] = (byte)((b << 3) | (b >> 2));     // B: 5 bits -> 8 bits
            output[offset + 3] = 255;                              // A: full opacity
        }

        private void WriteTgaRgba(int width, int height, byte[] rgba)
        {
            // Write a simple uncompressed RGBA TGA image
            _writer.WriteUInt8(0); // ID length
            _writer.WriteUInt8(0); // colour map type
            _writer.WriteUInt8(2); // image type (uncompressed true colour)
            _writer.WriteBytes(new byte[5]); // colour map specification
            _writer.WriteUInt16(0); // x origin
            _writer.WriteUInt16(0); // y origin
            _writer.WriteUInt16((ushort)width);
            _writer.WriteUInt16((ushort)height);
            _writer.WriteUInt8(32);
            _writer.WriteUInt8((byte)(0x20 | 0x08)); // top-left origin, 8-bit alpha

            // Convert RGBA to BGRA format in one batch operation
            int totalPixels = width * height;
            byte[] bgra = new byte[totalPixels * 4];
            for (int i = 0; i < totalPixels; i++)
            {
                int offset = i * 4;
                bgra[offset] = rgba[offset + 2]; // B
                bgra[offset + 1] = rgba[offset + 1]; // G
                bgra[offset + 2] = rgba[offset]; // R
                bgra[offset + 3] = rgba[offset + 3]; // A
            }
            _writer.WriteBytes(bgra);
        }

        public byte[] GetBytes()
        {
            return _writer.Data();
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}
