using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing.Formats.TXI;

namespace Andastra.Parsing.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:317-529
    // TODO:  Simplified: core fields and equality for texture container
    public class TPC : IEquatable<TPC>
    {
        public float AlphaTest { get; set; }
        public bool IsCubeMap { get; set; }
        public bool IsAnimated { get; set; }
        public string Txi { get; set; }
        public TXI.TXI TxiObject { get; set; }
        public List<TPCLayer> Layers { get; set; }
        internal TPCTextureFormat _format;

        public TPC()
        {
            AlphaTest = 0.0f;
            IsCubeMap = false;
            IsAnimated = false;
            Txi = string.Empty;
            TxiObject = new TXI.TXI();
            Layers = new List<TPCLayer>();
            _format = TPCTextureFormat.Invalid;
        }

        public TPCTextureFormat Format()
        {
            return _format;
        }

        public (int width, int height) Dimensions()
        {
            if (Layers.Count == 0 || Layers[0].Mipmaps.Count == 0)
            {
                return (0, 0);
            }
            return (Layers[0].Mipmaps[0].Width, Layers[0].Mipmaps[0].Height);
        }

        public override bool Equals(object obj)
        {
            return obj is TPC other && Equals(other);
        }

        public bool Equals(TPC other)
        {
            if (other == null)
            {
                return false;
            }
            if (AlphaTest != other.AlphaTest || IsCubeMap != other.IsCubeMap || IsAnimated != other.IsAnimated)
            {
                return false;
            }
            if (_format != other._format)
            {
                return false;
            }
            if (!string.Equals(Txi, other.Txi, StringComparison.Ordinal))
            {
                return false;
            }
            if (Layers.Count != other.Layers.Count)
            {
                return false;
            }
            for (int i = 0; i < Layers.Count; i++)
            {
                if (!Layers[i].Equals(other.Layers[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(AlphaTest);
            hash.Add(IsCubeMap);
            hash.Add(IsAnimated);
            hash.Add(_format);
            foreach (var layer in Layers)
            {
                hash.Add(layer);
            }
            hash.Add(Txi ?? string.Empty);
            return hash.ToHashCode();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:547-553
        // Original: def set_single(self, data: bytes | bytearray, tpc_format: TPCTextureFormat, width: int, height: int)
        public void SetSingle(byte[] data, TPCTextureFormat tpcFormat, int width, int height)
        {
            Layers = new List<TPCLayer> { new TPCLayer() };
            IsCubeMap = false;
            IsAnimated = false;
            Layers[0].SetSingle(width, height, data, tpcFormat);
            _format = tpcFormat;
        }

        #region Format Conversion Helper Methods

        // Uncompressed format conversion helpers
        // Based on PyKotor implementation: pykotor/resource/formats/tpc/convert/

        private static byte[] RgbaToRgb(byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            byte[] rgb = new byte[pixelCount * 3];
            for (int i = 0; i < pixelCount; i++)
            {
                rgb[i * 3] = rgba[i * 4];
                rgb[i * 3 + 1] = rgba[i * 4 + 1];
                rgb[i * 3 + 2] = rgba[i * 4 + 2];
            }
            return rgb;
        }

        private static byte[] RgbToRgba(byte[] rgb, int width, int height)
        {
            int pixelCount = width * height;
            byte[] rgba = new byte[pixelCount * 4];
            for (int i = 0; i < pixelCount; i++)
            {
                rgba[i * 4] = rgb[i * 3];
                rgba[i * 4 + 1] = rgb[i * 3 + 1];
                rgba[i * 4 + 2] = rgb[i * 3 + 2];
                rgba[i * 4 + 3] = 255; // Full alpha
            }
            return rgba;
        }

        private static byte[] RgbaToBgra(byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            byte[] bgra = new byte[pixelCount * 4];
            for (int i = 0; i < pixelCount; i++)
            {
                bgra[i * 4] = rgba[i * 4 + 2];     // B <- R
                bgra[i * 4 + 1] = rgba[i * 4 + 1]; // G <- G
                bgra[i * 4 + 2] = rgba[i * 4];     // R <- B
                bgra[i * 4 + 3] = rgba[i * 4 + 3]; // A <- A
            }
            return bgra;
        }

        private static byte[] BgraToRgba(byte[] bgra, int width, int height)
        {
            int pixelCount = width * height;
            byte[] rgba = new byte[pixelCount * 4];
            for (int i = 0; i < pixelCount; i++)
            {
                rgba[i * 4] = bgra[i * 4 + 2];     // R <- B
                rgba[i * 4 + 1] = bgra[i * 4 + 1]; // G <- G
                rgba[i * 4 + 2] = bgra[i * 4];     // B <- R
                rgba[i * 4 + 3] = bgra[i * 4 + 3]; // A <- A
            }
            return rgba;
        }

        private static byte[] RgbToBgr(byte[] rgb, int width, int height)
        {
            int pixelCount = width * height;
            byte[] bgr = new byte[pixelCount * 3];
            for (int i = 0; i < pixelCount; i++)
            {
                bgr[i * 3] = rgb[i * 3 + 2];     // B <- R
                bgr[i * 3 + 1] = rgb[i * 3 + 1]; // G <- G
                bgr[i * 3 + 2] = rgb[i * 3];     // R <- B
            }
            return bgr;
        }

        private static byte[] BgrToRgb(byte[] bgr, int width, int height)
        {
            int pixelCount = width * height;
            byte[] rgb = new byte[pixelCount * 3];
            for (int i = 0; i < pixelCount; i++)
            {
                rgb[i * 3] = bgr[i * 3 + 2];     // R <- B
                rgb[i * 3 + 1] = bgr[i * 3 + 1]; // G <- G
                rgb[i * 3 + 2] = bgr[i * 3];     // B <- R
            }
            return rgb;
        }

        private static byte[] RgbaToGreyscale(byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            byte[] grey = new byte[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                // Use standard luminance formula: 0.299*R + 0.587*G + 0.114*B
                int r = rgba[i * 4];
                int g = rgba[i * 4 + 1];
                int b = rgba[i * 4 + 2];
                grey[i] = (byte)((299 * r + 587 * g + 114 * b) / 1000);
            }
            return grey;
        }

        private static byte[] RgbToGreyscale(byte[] rgb, int width, int height)
        {
            int pixelCount = width * height;
            byte[] grey = new byte[pixelCount];
            for (int i = 0; i < pixelCount; i++)
            {
                int r = rgb[i * 3];
                int g = rgb[i * 3 + 1];
                int b = rgb[i * 3 + 2];
                grey[i] = (byte)((299 * r + 587 * g + 114 * b) / 1000);
            }
            return grey;
        }

        private static byte[] BgraToGreyscale(byte[] bgra, int width, int height)
        {
            // Convert BGRA to RGBA first, then to greyscale
            byte[] rgba = BgraToRgba(bgra, width, height);
            return RgbaToGreyscale(rgba, width, height);
        }

        private static byte[] BgrToGreyscale(byte[] bgr, int width, int height)
        {
            // Convert BGR to RGB first, then to greyscale
            byte[] rgb = BgrToRgb(bgr, width, height);
            return RgbToGreyscale(rgb, width, height);
        }

        private static byte[] GreyscaleToRgb(byte[] grey, int width, int height)
        {
            int pixelCount = width * height;
            byte[] rgb = new byte[pixelCount * 3];
            for (int i = 0; i < pixelCount; i++)
            {
                rgb[i * 3] = grey[i];
                rgb[i * 3 + 1] = grey[i];
                rgb[i * 3 + 2] = grey[i];
            }
            return rgb;
        }

        private static byte[] GreyscaleToRgba(byte[] grey, int width, int height)
        {
            int pixelCount = width * height;
            byte[] rgba = new byte[pixelCount * 4];
            for (int i = 0; i < pixelCount; i++)
            {
                rgba[i * 4] = grey[i];
                rgba[i * 4 + 1] = grey[i];
                rgba[i * 4 + 2] = grey[i];
                rgba[i * 4 + 3] = 255;
            }
            return rgba;
        }

        // DXT decompression helpers
        // Based on standard DXT/S3TC decompression algorithms

        private static byte[] Dxt1ToRgb(byte[] dxt1, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            DecompressDxt1(dxt1, rgba, width, height);
            return RgbaToRgb(rgba, width, height);
        }

        private static byte[] Dxt3ToRgba(byte[] dxt3, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            DecompressDxt3(dxt3, rgba, width, height);
            return rgba;
        }

        private static byte[] Dxt5ToRgba(byte[] dxt5, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            DecompressDxt5(dxt5, rgba, width, height);
            return rgba;
        }

        // DXT compression helpers
        // Based on standard DXT/S3TC compression algorithms (simplified implementation)

        private static byte[] RgbToDxt1(byte[] rgb, int width, int height)
        {
            // TODO: STUB - DXT1 compression is complex and lossy
            // For now, return placeholder - in production would use proper DXT1 compression
            // DXT1 compression requires block-based encoding with color endpoint selection
            // This would require implementing BC1/DXT1 encoder algorithm
            throw new NotImplementedException("DXT1 compression not yet implemented. Use decompression for DXT formats.");
        }

        private static byte[] RgbaToDxt3(byte[] rgba, int width, int height)
        {
            // TODO: STUB - DXT3 compression is complex and lossy
            // For now, return placeholder - in production would use proper DXT3 compression
            // DXT3 compression requires block-based encoding with explicit alpha and color endpoint selection
            throw new NotImplementedException("DXT3 compression not yet implemented. Use decompression for DXT formats.");
        }

        private static byte[] RgbaToDxt5(byte[] rgba, int width, int height)
        {
            // TODO: STUB - DXT5 compression is complex and lossy
            // For now, return placeholder - in production would use proper DXT5 compression
            // DXT5 compression requires block-based encoding with interpolated alpha and color endpoint selection
            throw new NotImplementedException("DXT5 compression not yet implemented. Use decompression for DXT formats.");
        }

        // DXT decompression implementation (based on TpcToMonoGameTextureConverter.cs)

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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:594-640
        // Original: def convert(self, target: TPCTextureFormat) -> None
        /// <summary>
        /// Converts the TPC texture to the specified target format.
        /// Comprehensive format conversion supporting all TPC texture formats.
        /// Based on PyKotor implementation: Converts all layers and mipmaps to target format.
        /// </summary>
        /// <param name="target">Target texture format to convert to.</param>
        public void Convert(TPCTextureFormat target)
        {
            if (_format == target)
            {
                return;
            }

            // Convert all layers and mipmaps to target format
            foreach (var layer in Layers)
            {
                foreach (var mipmap in layer.Mipmaps)
                {
                    ConvertMipmap(mipmap, _format, target);
                }
            }

            _format = target;
        }

        /// <summary>
        /// Converts a single mipmap from source format to target format.
        /// Handles all format combinations including uncompressed and DXT formats.
        /// Based on PyKotor TPCMipmap.convert implementation.
        /// </summary>
        /// <param name="mipmap">Mipmap to convert.</param>
        /// <param name="sourceFormat">Current format of the mipmap.</param>
        /// <param name="targetFormat">Target format to convert to.</param>
        private void ConvertMipmap(TPCMipmap mipmap, TPCTextureFormat sourceFormat, TPCTextureFormat targetFormat)
        {
            if (mipmap.TpcFormat == targetFormat)
            {
                return;
            }

            int width = mipmap.Width;
            int height = mipmap.Height;
            byte[] data = mipmap.Data;

            // Handle conversions based on source format
            if (sourceFormat == TPCTextureFormat.RGBA)
            {
                if (targetFormat == TPCTextureFormat.RGB)
                {
                    mipmap.Data = RgbaToRgb(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGB;
                }
                else if (targetFormat == TPCTextureFormat.BGRA)
                {
                    mipmap.Data = RgbaToBgra(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGRA;
                }
                else if (targetFormat == TPCTextureFormat.BGR)
                {
                    mipmap.Data = RgbToBgr(RgbaToRgb(data, width, height), width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGR;
                }
                else if (targetFormat == TPCTextureFormat.Greyscale)
                {
                    mipmap.Data = RgbaToGreyscale(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.Greyscale;
                }
                else if (targetFormat == TPCTextureFormat.DXT1)
                {
                    // DXT1 compression requires RGB input (no alpha)
                    byte[] rgbData = RgbaToRgb(data, width, height);
                    mipmap.Data = RgbToDxt1(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT1;
                }
                else if (targetFormat == TPCTextureFormat.DXT3)
                {
                    mipmap.Data = RgbaToDxt3(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT3;
                }
                else if (targetFormat == TPCTextureFormat.DXT5)
                {
                    mipmap.Data = RgbaToDxt5(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT5;
                }
            }
            else if (sourceFormat == TPCTextureFormat.RGB)
            {
                if (targetFormat == TPCTextureFormat.RGBA)
                {
                    mipmap.Data = RgbToRgba(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGBA;
                }
                else if (targetFormat == TPCTextureFormat.BGR)
                {
                    mipmap.Data = RgbToBgr(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGR;
                }
                else if (targetFormat == TPCTextureFormat.BGRA)
                {
                    mipmap.Data = RgbaToBgra(RgbToRgba(data, width, height), width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGRA;
                }
                else if (targetFormat == TPCTextureFormat.Greyscale)
                {
                    mipmap.Data = RgbToGreyscale(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.Greyscale;
                }
                else if (targetFormat == TPCTextureFormat.DXT1)
                {
                    mipmap.Data = RgbToDxt1(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT1;
                }
                else if (targetFormat == TPCTextureFormat.DXT3)
                {
                    byte[] rgbaData = RgbToRgba(data, width, height);
                    mipmap.Data = RgbaToDxt3(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT3;
                }
                else if (targetFormat == TPCTextureFormat.DXT5)
                {
                    byte[] rgbaData = RgbToRgba(data, width, height);
                    mipmap.Data = RgbaToDxt5(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT5;
                }
            }
            else if (sourceFormat == TPCTextureFormat.BGRA)
            {
                if (targetFormat == TPCTextureFormat.RGBA)
                {
                    mipmap.Data = BgraToRgba(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGBA;
                }
                else if (targetFormat == TPCTextureFormat.RGB)
                {
                    byte[] rgbaData = BgraToRgba(data, width, height);
                    mipmap.Data = RgbaToRgb(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGB;
                }
                else if (targetFormat == TPCTextureFormat.BGR)
                {
                    byte[] rgbaData = BgraToRgba(data, width, height);
                    mipmap.Data = RgbToBgr(RgbaToRgb(rgbaData, width, height), width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGR;
                }
                else if (targetFormat == TPCTextureFormat.Greyscale)
                {
                    mipmap.Data = BgraToGreyscale(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.Greyscale;
                }
                else if (targetFormat == TPCTextureFormat.DXT1)
                {
                    byte[] rgbaData = BgraToRgba(data, width, height);
                    byte[] rgbData = RgbaToRgb(rgbaData, width, height);
                    mipmap.Data = RgbToDxt1(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT1;
                }
                else if (targetFormat == TPCTextureFormat.DXT3)
                {
                    byte[] rgbaData = BgraToRgba(data, width, height);
                    mipmap.Data = RgbaToDxt3(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT3;
                }
                else if (targetFormat == TPCTextureFormat.DXT5)
                {
                    byte[] rgbaData = BgraToRgba(data, width, height);
                    mipmap.Data = RgbaToDxt5(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT5;
                }
            }
            else if (sourceFormat == TPCTextureFormat.BGR)
            {
                if (targetFormat == TPCTextureFormat.RGB)
                {
                    mipmap.Data = BgrToRgb(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGB;
                }
                else if (targetFormat == TPCTextureFormat.RGBA)
                {
                    byte[] rgbData = BgrToRgb(data, width, height);
                    mipmap.Data = RgbToRgba(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGBA;
                }
                else if (targetFormat == TPCTextureFormat.BGRA)
                {
                    byte[] rgbData = BgrToRgb(data, width, height);
                    mipmap.Data = RgbaToBgra(RgbToRgba(rgbData, width, height), width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGRA;
                }
                else if (targetFormat == TPCTextureFormat.Greyscale)
                {
                    mipmap.Data = BgrToGreyscale(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.Greyscale;
                }
                else if (targetFormat == TPCTextureFormat.DXT1)
                {
                    byte[] rgbData = BgrToRgb(data, width, height);
                    mipmap.Data = RgbToDxt1(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT1;
                }
                else if (targetFormat == TPCTextureFormat.DXT3)
                {
                    byte[] rgbData = BgrToRgb(data, width, height);
                    byte[] rgbaData = RgbToRgba(rgbData, width, height);
                    mipmap.Data = RgbaToDxt3(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT3;
                }
                else if (targetFormat == TPCTextureFormat.DXT5)
                {
                    byte[] rgbData = BgrToRgb(data, width, height);
                    byte[] rgbaData = RgbToRgba(rgbData, width, height);
                    mipmap.Data = RgbaToDxt5(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT5;
                }
            }
            else if (sourceFormat == TPCTextureFormat.Greyscale)
            {
                if (targetFormat == TPCTextureFormat.RGB)
                {
                    mipmap.Data = GreyscaleToRgb(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGB;
                }
                else if (targetFormat == TPCTextureFormat.RGBA)
                {
                    mipmap.Data = GreyscaleToRgba(data, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGBA;
                }
                else if (targetFormat == TPCTextureFormat.BGR)
                {
                    byte[] rgbData = GreyscaleToRgb(data, width, height);
                    mipmap.Data = RgbToBgr(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGR;
                }
                else if (targetFormat == TPCTextureFormat.BGRA)
                {
                    byte[] rgbaData = GreyscaleToRgba(data, width, height);
                    mipmap.Data = RgbaToBgra(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGRA;
                }
                else if (targetFormat == TPCTextureFormat.DXT1)
                {
                    byte[] rgbData = GreyscaleToRgb(data, width, height);
                    mipmap.Data = RgbToDxt1(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT1;
                }
                else if (targetFormat == TPCTextureFormat.DXT3)
                {
                    byte[] rgbaData = GreyscaleToRgba(data, width, height);
                    mipmap.Data = RgbaToDxt3(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT3;
                }
                else if (targetFormat == TPCTextureFormat.DXT5)
                {
                    byte[] rgbaData = GreyscaleToRgba(data, width, height);
                    mipmap.Data = RgbaToDxt5(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT5;
                }
            }
            else if (sourceFormat == TPCTextureFormat.DXT1)
            {
                // Decompress DXT1 to RGB first
                byte[] rgbData = Dxt1ToRgb(data, width, height);
                if (targetFormat == TPCTextureFormat.RGB)
                {
                            mipmap.Data = rgbData;
                            mipmap.TpcFormat = TPCTextureFormat.RGB;
                        }
                else if (targetFormat == TPCTextureFormat.RGBA)
                {
                    mipmap.Data = RgbToRgba(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGBA;
                }
                else if (targetFormat == TPCTextureFormat.BGR)
                {
                    mipmap.Data = RgbToBgr(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGR;
                }
                else if (targetFormat == TPCTextureFormat.BGRA)
                {
                    byte[] rgbaData = RgbToRgba(rgbData, width, height);
                    mipmap.Data = RgbaToBgra(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGRA;
                }
                else if (targetFormat == TPCTextureFormat.Greyscale)
                {
                    mipmap.Data = RgbToGreyscale(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.Greyscale;
                }
                else if (targetFormat == TPCTextureFormat.DXT3)
                {
                    byte[] rgbaData = RgbToRgba(rgbData, width, height);
                    mipmap.Data = RgbaToDxt3(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT3;
                }
                else if (targetFormat == TPCTextureFormat.DXT5)
                {
                    byte[] rgbaData = RgbToRgba(rgbData, width, height);
                    mipmap.Data = RgbaToDxt5(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT5;
                }
            }
            else if (sourceFormat == TPCTextureFormat.DXT3 || sourceFormat == TPCTextureFormat.DXT5)
            {
                // Decompress DXT3/DXT5 to RGBA first
                byte[] rgbaData = sourceFormat == TPCTextureFormat.DXT3 
                    ? Dxt3ToRgba(data, width, height) 
                    : Dxt5ToRgba(data, width, height);
                
                if (targetFormat == TPCTextureFormat.RGBA)
                {
                    mipmap.Data = rgbaData;
                    mipmap.TpcFormat = TPCTextureFormat.RGBA;
                }
                else if (targetFormat == TPCTextureFormat.RGB)
                {
                    mipmap.Data = RgbaToRgb(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.RGB;
                }
                else if (targetFormat == TPCTextureFormat.BGR)
                {
                    byte[] rgbData = RgbaToRgb(rgbaData, width, height);
                    mipmap.Data = RgbToBgr(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGR;
                }
                else if (targetFormat == TPCTextureFormat.BGRA)
                {
                    mipmap.Data = RgbaToBgra(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.BGRA;
                }
                else if (targetFormat == TPCTextureFormat.Greyscale)
                {
                    mipmap.Data = RgbaToGreyscale(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.Greyscale;
                }
                else if (targetFormat == TPCTextureFormat.DXT1)
                {
                    byte[] rgbData = RgbaToRgb(rgbaData, width, height);
                    mipmap.Data = RgbToDxt1(rgbData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT1;
                }
                else if (targetFormat == TPCTextureFormat.DXT3 && sourceFormat != TPCTextureFormat.DXT3)
                {
                    mipmap.Data = RgbaToDxt3(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT3;
                }
                else if (targetFormat == TPCTextureFormat.DXT5 && sourceFormat != TPCTextureFormat.DXT5)
                {
                    mipmap.Data = RgbaToDxt5(rgbaData, width, height);
                    mipmap.TpcFormat = TPCTextureFormat.DXT5;
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py
        // Original: def copy(self) -> TPC:
        /// <summary>
        /// Creates a deep copy of this TPC texture.
        /// </summary>
        public TPC Copy()
        {
            var copy = new TPC
            {
                AlphaTest = AlphaTest,
                IsCubeMap = IsCubeMap,
                IsAnimated = IsAnimated,
                Txi = Txi,
                TxiObject = TxiObject,
                _format = _format
            };

            foreach (var layer in Layers)
            {
                var layerCopy = new TPCLayer();
                foreach (var mipmap in layer.Mipmaps)
                {
                    var mipmapCopy = new TPCMipmap(
                        mipmap.Width,
                        mipmap.Height,
                        mipmap.TpcFormat,
                        mipmap.Data != null ? (byte[])mipmap.Data.Clone() : null
                    );
                    layerCopy.Mipmaps.Add(mipmapCopy);
                }
                copy.Layers.Add(layerCopy);
            }

            return copy;
        }
    }
}

