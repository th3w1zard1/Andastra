using System;
using System.Collections.Generic;
using System.Linq;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.TXI;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:317-529
    // swkotor.exe/swkotor2.exe: TPC texture container with full feature set
    public class TPC : IEquatable<TPC>
    {
        public static readonly ResourceType BINARY_TYPE = ResourceType.TPC;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:476-486
        // Original: BLANK_LAYER = TPCLayer([...])
        public static readonly TPCLayer BLANK_LAYER = new TPCLayer
        {
            Mipmaps = new List<TPCMipmap>
            {
                new TPCMipmap(256, 256, TPCTextureFormat.RGBA, new byte[256 * 256 * 4]),
                new TPCMipmap(128, 128, TPCTextureFormat.RGBA, new byte[128 * 128 * 4]),
                new TPCMipmap(64, 64, TPCTextureFormat.RGBA, new byte[64 * 64 * 4]),
                new TPCMipmap(32, 32, TPCTextureFormat.RGBA, new byte[32 * 32 * 4]),
                new TPCMipmap(16, 16, TPCTextureFormat.RGBA, new byte[16 * 16 * 4]),
                new TPCMipmap(8, 8, TPCTextureFormat.RGBA, new byte[8 * 8 * 4]),
                new TPCMipmap(4, 4, TPCTextureFormat.RGBA, new byte[4 * 4 * 4]),
                new TPCMipmap(2, 2, TPCTextureFormat.RGBA, new byte[2 * 2 * 4]),
                new TPCMipmap(1, 1, TPCTextureFormat.RGBA, new byte[1 * 1 * 4])
            }
        };

        public float AlphaTest { get; set; }
        public bool IsCubeMap { get; set; }
        public bool IsAnimated { get; set; }
        private TXI.TXI _txi;
        public List<TPCLayer> Layers { get; set; }
        internal TPCTextureFormat _format;

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:504-512
        // Original: @property def txi(self) -> str: ... @txi.setter def txi(self, value: str): ...
        public string Txi
        {
            get
            {
                return _txi != null ? _txi.ToString() : string.Empty;
            }
            set
            {
                if (_txi == null)
                {
                    _txi = new TXI.TXI();
                }
                if (!string.IsNullOrEmpty(value))
                {
                    _txi.Load(value);
                }
            }
        }

        public TXI.TXI TxiObject
        {
            get
            {
                if (_txi == null)
                {
                    _txi = new TXI.TXI();
                }
                return _txi;
            }
            set
            {
                _txi = value ?? new TXI.TXI();
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:488-494
        // Original: def __init__(self)
        public TPC()
        {
            _txi = new TXI.TXI();
            _format = TPCTextureFormat.Invalid;
            Layers = new List<TPCLayer>();
            IsAnimated = false;
            IsCubeMap = false;
            AlphaTest = 1.0f;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:496-502
        // Original: @classmethod def from_blank(cls) -> Self
        public static TPC FromBlank()
        {
            TPC instance = new TPC();
            instance.Layers = new List<TPCLayer> { BLANK_LAYER };
            instance._format = TPCTextureFormat.RGBA;
            return instance;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:514-518
        // Original: def format(self) -> TPCTextureFormat
        public TPCTextureFormat Format()
        {
            return _format;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:520-524
        // Original: def is_compressed(self) -> bool
        public bool IsCompressed()
        {
            return _format == TPCTextureFormat.DXT1 ||
                   _format == TPCTextureFormat.DXT3 ||
                   _format == TPCTextureFormat.DXT5;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:526-529
        // Original: def mipmap_size(self, layer: int, mipmap: int) -> tuple[int, int]
        public (int width, int height) MipmapSize(int layer, int mipmap)
        {
            TPCMipmap mm = Layers[layer].Mipmaps[mipmap];
            return (mm.Width, mm.Height);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:531-537
        // Original: def dimensions(self) -> tuple[int, int]
        public (int width, int height) Dimensions()
        {
            if (Layers.Count == 0 || Layers[0].Mipmaps.Count == 0)
            {
                return (0, 0);
            }
            return (Layers[0].Mipmaps[0].Width, Layers[0].Mipmaps[0].Height);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:539-545
        // Original: def get(self, layer: int = 0, mipmap: int = 0) -> TPCMipmap
        public TPCMipmap Get(int layer = 0, int mipmap = 0)
        {
            return Layers[layer].Mipmaps[mipmap];
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
        // Based on standard DXT/S3TC compression algorithms
        // Reference: PyKotor compress_dxt.py - comprehensive BC1/DXT1 encoder implementation
        // swkotor.exe/swkotor2.exe/daorigins.exe: DXT1 compression uses standard S3TC algorithm

        private static byte[] RgbToDxt1(byte[] rgb, int width, int height)
        {
            // Calculate output size: 8 bytes per 4x4 block
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int outputSize = blockCountX * blockCountY * 8;
            byte[] dxt1Data = new byte[outputSize];

            int dstOffset = 0;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    // Extract 4x4 block from source (16 pixels, 3 bytes each = 48 bytes, but we store as RGBA = 64 bytes)
                    int[] block = ExtractBlock(rgb, x, y, width, height, 3);
                    byte[] dest = new byte[8];
                    CompressDxt1Block(dest, block);

                    // Copy compressed block to output
                    for (int i = 0; i < 8; i++)
                    {
                        dxt1Data[dstOffset + i] = dest[i];
                    }
                    dstOffset += 8;
                }
            }

            return dxt1Data;
        }

        // Extract a 4x4 block from source image
        // Returns array of 64 bytes (16 pixels * 4 components: R, G, B, A)
        private static int[] ExtractBlock(byte[] src, int x, int y, int w, int h, int channels)
        {
            int[] block = new int[64]; // 16 pixels * 4 components
            int blockIdx = 0;

            for (int by = 0; by < 4; by++)
            {
                for (int bx = 0; bx < 4; bx++)
                {
                    int sx = x + bx;
                    int sy = y + by;

                    if (sx < w && sy < h)
                    {
                        int srcIdx = (sy * w + sx) * channels;
                        // Copy RGB channels
                        block[blockIdx] = src[srcIdx];
                        block[blockIdx + 1] = src[srcIdx + 1];
                        block[blockIdx + 2] = src[srcIdx + 2];
                        if (channels == 3)
                        {
                            block[blockIdx + 3] = 255; // Add alpha for RGB
                        }
                        else
                        {
                            block[blockIdx + 3] = src[srcIdx + 3];
                        }
                    }
                    else
                    {
                        // Out of bounds - fill with zeros
                        block[blockIdx] = 0;
                        block[blockIdx + 1] = 0;
                        block[blockIdx + 2] = 0;
                        block[blockIdx + 3] = 0;
                    }
                    blockIdx += 4;
                }
            }

            return block;
        }

        // Compress a single DXT1 block (4x4 pixels = 8 bytes output)
        private static void CompressDxt1Block(byte[] dest, int[] src)
        {
            CompressColorBlock(dest, src);
        }

        // Core color block compression algorithm
        // Based on PyKotor _compress_color_block implementation
        private static void CompressColorBlock(byte[] dest, int[] src)
        {
            // Check if all pixels are the same color
            bool allSame = true;
            for (int i = 4; i < 64; i += 4)
            {
                if (src[i] != src[0] || src[i + 1] != src[1] || src[i + 2] != src[2])
                {
                    allSame = false;
                    break;
                }
            }

            ushort max16;
            ushort min16;
            uint mask;

            if (allSame)
            {
                // All pixels same - use single color
                int r = src[0];
                int g = src[1];
                int b = src[2];
                mask = 0xAAAAAAAA; // All pixels use color 0
                max16 = As16Bit(QuantizeRb(r), QuantizeG(g), QuantizeRb(b));
                min16 = As16Bit(QuantizeRb(r), QuantizeG(g), QuantizeRb(b));
            }
            else
            {
                // Dither block to improve quality
                int[] dblock = DitherBlock(src);
                var optimized = OptimizeColorsBlock(dblock);
                max16 = optimized.max;
                min16 = optimized.min;

                if (max16 != min16)
                {
                    int[] color = EvalColors(max16, min16);
                    mask = MatchColorsBlock(src, color);
                }
                else
                {
                    mask = 0;
                }

                // Refine block up to 2 iterations
                for (int iter = 0; iter < 2; iter++)
                {
                    uint lastMask = mask;
                    if (RefineBlock(src, ref max16, ref min16, mask))
                    {
                        if (max16 != min16)
                        {
                            int[] color = EvalColors(max16, min16);
                            mask = MatchColorsBlock(src, color);
                        }
                        else
                        {
                            mask = 0;
                            break;
                        }
                    }
                    if (mask == lastMask)
                    {
                        break;
                    }
                }
            }

            // Ensure max16 >= min16 (swap if needed)
            if (max16 < min16)
            {
                ushort temp = max16;
                max16 = min16;
                min16 = temp;
                mask ^= 0x55555555; // Flip all bits
            }

            // Write block data: 2 bytes color0, 2 bytes color1, 4 bytes indices
            dest[0] = (byte)(max16 & 0xFF);
            dest[1] = (byte)(max16 >> 8);
            dest[2] = (byte)(min16 & 0xFF);
            dest[3] = (byte)(min16 >> 8);
            dest[4] = (byte)(mask & 0xFF);
            dest[5] = (byte)((mask >> 8) & 0xFF);
            dest[6] = (byte)((mask >> 16) & 0xFF);
            dest[7] = (byte)((mask >> 24) & 0xFF);
        }

        // Dither block to improve compression quality
        private static int[] DitherBlock(int[] block)
        {
            int[] dblock = new int[64];
            for (int i = 0; i < 64; i++)
            {
                dblock[i] = block[i];
            }

            int[] err = new int[8];
            for (int ch = 0; ch < 3; ch++)
            {
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        int idx = (y * 4 + x) * 4 + ch;
                        int old = dblock[idx];
                        int newVal;
                        if (ch != 1)
                        {
                            newVal = QuantizeRb(old);
                        }
                        else
                        {
                            newVal = QuantizeG(old);
                        }
                        dblock[idx] = newVal;
                        int errVal = old - newVal;

                        // Distribute error using Floyd-Steinberg dithering
                        if (x < 3)
                        {
                            dblock[idx + 4] += (errVal * 7) >> 4;
                        }
                        if (y < 3)
                        {
                            if (x > 0)
                            {
                                dblock[idx + 12] += (errVal * 3) >> 4;
                            }
                            dblock[idx + 16] += (errVal * 5) >> 4;
                            if (x < 3)
                            {
                                dblock[idx + 20] += errVal >> 4;
                            }
                        }
                    }
                }
            }

            return dblock;
        }

        // Optimize color endpoints using principal component analysis
        private static (ushort max, ushort min) OptimizeColorsBlock(int[] block)
        {
            int[] cov = new int[6];
            int[] mu = new int[3];
            int[] minColor = new int[3] { 255, 255, 255 };
            int[] maxColor = new int[3] { 0, 0, 0 };

            // Calculate mean and min/max
            for (int i = 0; i < 16; i++)
            {
                int r = block[i * 4];
                int g = block[i * 4 + 1];
                int b = block[i * 4 + 2];
                mu[0] += r;
                mu[1] += g;
                mu[2] += b;
                if (r < minColor[0]) minColor[0] = r;
                if (g < minColor[1]) minColor[1] = g;
                if (b < minColor[2]) minColor[2] = b;
                if (r > maxColor[0]) maxColor[0] = r;
                if (g > maxColor[1]) maxColor[1] = g;
                if (b > maxColor[2]) maxColor[2] = b;
            }

            mu[0] = (mu[0] + 8) >> 4;
            mu[1] = (mu[1] + 8) >> 4;
            mu[2] = (mu[2] + 8) >> 4;

            // Calculate covariance matrix
            for (int i = 0; i < 16; i++)
            {
                int r = block[i * 4] - mu[0];
                int g = block[i * 4 + 1] - mu[1];
                int b = block[i * 4 + 2] - mu[2];
                cov[0] += r * r;
                cov[1] += r * g;
                cov[2] += r * b;
                cov[3] += g * g;
                cov[4] += g * b;
                cov[5] += b * b;
            }

            // Calculate principal direction
            int vfr = maxColor[0] - minColor[0];
            int vfg = maxColor[1] - minColor[1];
            int vfb = maxColor[2] - minColor[2];

            // Power iteration (4 iterations)
            for (int iter = 0; iter < 4; iter++)
            {
                int r = vfr * cov[0] + vfg * cov[1] + vfb * cov[2];
                int g = vfr * cov[1] + vfg * cov[3] + vfb * cov[4];
                int b = vfr * cov[2] + vfg * cov[4] + vfb * cov[5];
                vfr = r;
                vfg = g;
                vfb = b;
            }

            // Normalize
            int magn = Math.Max(Math.Max(Math.Abs(vfr), Math.Abs(vfg)), Math.Abs(vfb));
            int v_r, v_g, v_b;
            if (magn < 4)
            {
                v_r = 299;
                v_g = 587;
                v_b = 114;
            }
            else
            {
                v_r = (vfr * 512) / magn;
                v_g = (vfg * 512) / magn;
                v_b = (vfb * 512) / magn;
            }

            // Find min and max projections
            float minD = float.MaxValue;
            float maxD = float.MinValue;
            int minP = 0;
            int maxP = 0;

            for (int i = 0; i < 16; i++)
            {
                int dot = block[i * 4] * v_r + block[i * 4 + 1] * v_g + block[i * 4 + 2] * v_b;
                if (dot < minD)
                {
                    minD = dot;
                    minP = i;
                }
                if (dot > maxD)
                {
                    maxD = dot;
                    maxP = i;
                }
            }

            return (
                As16Bit(block[maxP * 4], block[maxP * 4 + 1], block[maxP * 4 + 2]),
                As16Bit(block[minP * 4], block[minP * 4 + 1], block[minP * 4 + 2])
            );
        }

        // Evaluate color palette from two endpoints
        private static int[] EvalColors(ushort color0, ushort color1)
        {
            // Expand 565 to RGB
            void Expand565(ushort c, out int r, out int g, out int b)
            {
                r = ((c >> 11) & 31) * 8;
                g = ((c >> 5) & 63) * 4;
                b = (c & 31) * 8;
            }

            Expand565(color0, out int r0, out int g0, out int b0);
            Expand565(color1, out int r1, out int g1, out int b1);

            // Generate 4-color palette
            int[] colors = new int[16]
            {
                r0, g0, b0, 255,
                r1, g1, b1, 255,
                (2 * r0 + r1) / 3, (2 * g0 + g1) / 3, (2 * b0 + b1) / 3, 255,
                (r0 + 2 * r1) / 3, (g0 + 2 * g1) / 3, (b0 + 2 * b1) / 3, 255
            };

            return colors;
        }

        // Match block pixels to color palette
        private static uint MatchColorsBlock(int[] block, int[] color)
        {
            uint mask = 0;
            int dirR = color[0] - color[4];
            int dirG = color[1] - color[5];
            int dirB = color[2] - color[6];

            int[] dots = new int[16];
            int[] stops = new int[4];

            // Calculate dot products for all pixels
            for (int i = 0; i < 16; i++)
            {
                dots[i] = block[i * 4] * dirR + block[i * 4 + 1] * dirG + block[i * 4 + 2] * dirB;
            }

            // Calculate dot products for color stops
            for (int i = 0; i < 4; i++)
            {
                stops[i] = color[i * 4] * dirR + color[i * 4 + 1] * dirG + color[i * 4 + 2] * dirB;
            }

            // Determine thresholds
            int c0Point = (stops[1] + stops[3]) >> 1;
            int halfPoint = (stops[3] + stops[2]) >> 1;
            int c3Point = (stops[2] + stops[0]) >> 1;

            // Match each pixel to closest color
            for (int i = 0; i < 16; i++)
            {
                int dot = dots[i];
                int code;
                if (dot < halfPoint)
                {
                    code = (dot < c0Point) ? 3 : 1;
                }
                else
                {
                    code = (dot < c3Point) ? 2 : 0;
                }
                mask |= (uint)(code << (i * 2));
            }

            return mask;
        }

        // Refine color endpoints based on current mask
        private static bool RefineBlock(int[] block, ref ushort max16, ref ushort min16, uint mask)
        {
            ushort oldMin = min16;
            ushort oldMax = max16;

            int at1R = 0, at1G = 0, at1B = 0;
            int at2R = 0, at2G = 0, at2B = 0;
            int akku = 0;

            uint currentMask = mask;
            for (int i = 0; i < 16; i++)
            {
                int step = (int)(currentMask & 3);
                int[] w1Table = new int[] { 3, 0, 2, 1 };
                int w1 = w1Table[step];
                int r = block[i * 4];
                int g = block[i * 4 + 1];
                int b = block[i * 4 + 2];

                int[] akkuTable = new int[] { 0x090000, 0x000900, 0x040102, 0x010402 };
                akku += akkuTable[step];

                at1R += w1 * r;
                at1G += w1 * g;
                at1B += w1 * b;
                at2R += r;
                at2G += g;
                at2B += b;

                currentMask >>= 2;
            }

            at2R = 3 * at2R - at1R;
            at2G = 3 * at2G - at1G;
            at2B = 3 * at2B - at1B;

            int xx = akku >> 16;
            int yy = (akku >> 8) & 255;
            int xy = akku & 255;

            int denom = xx * yy - xy * xy;
            if (denom == 0)
            {
                return false;
            }

            float fRb = (3.0f * 31.0f) / 255.0f / denom;
            float fG = fRb * 63.0f / 31.0f;

            max16 = (ushort)(
                (Sclamp((at1R * yy - at2R * xy) * fRb + 0.5f, 0, 31) << 11) |
                (Sclamp((at1G * yy - at2G * xy) * fG + 0.5f, 0, 63) << 5) |
                Sclamp((at1B * yy - at2B * xy) * fRb + 0.5f, 0, 31)
            );

            min16 = (ushort)(
                (Sclamp((at2R * xx - at1R * xy) * fRb + 0.5f, 0, 31) << 11) |
                (Sclamp((at2G * xx - at1G * xy) * fG + 0.5f, 0, 63) << 5) |
                Sclamp((at2B * xx - at1B * xy) * fRb + 0.5f, 0, 31)
            );

            return oldMin != min16 || oldMax != max16;
        }

        // Convert RGB to 16-bit 565 format
        private static ushort As16Bit(int r, int g, int b)
        {
            return (ushort)(((r >> 3) << 11) | ((g >> 2) << 5) | (b >> 3));
        }

        // Clamp float to integer range
        private static int Sclamp(float y, int p0, int p1)
        {
            int x = (int)y;
            if (x < p0) return p0;
            if (x > p1) return p1;
            return x;
        }

        // Quantize red/blue channel to 5 bits
        private static int QuantizeRb(int x)
        {
            return (x * 31 + 127) / 255;
        }

        // Quantize green channel to 6 bits
        private static int QuantizeG(int x)
        {
            return (x * 63 + 127) / 255;
        }

        private static byte[] RgbaToDxt3(byte[] rgba, int width, int height)
        {
            // Calculate output size: 16 bytes per 4x4 block
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int outputSize = blockCountX * blockCountY * 16;
            byte[] dxt3Data = new byte[outputSize];

            int dstOffset = 0;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    // Extract 4x4 block from source (16 pixels, 4 bytes each = 64 bytes)
                    int[] block = ExtractBlock(rgba, x, y, width, height, 4);
                    byte[] dest = new byte[16];
                    CompressDxt3Block(dest, block);

                    // Copy compressed block to output
                    for (int i = 0; i < 16; i++)
                    {
                        dxt3Data[dstOffset + i] = dest[i];
                    }
                    dstOffset += 16;
                }
            }

            return dxt3Data;
        }

        private static byte[] RgbaToDxt5(byte[] rgba, int width, int height)
        {
            // Calculate output size: 16 bytes per 4x4 block
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;
            int outputSize = blockCountX * blockCountY * 16;
            byte[] dxt5Data = new byte[outputSize];

            int dstOffset = 0;
            for (int y = 0; y < height; y += 4)
            {
                for (int x = 0; x < width; x += 4)
                {
                    // Extract 4x4 block from source (16 pixels, 4 bytes each = 64 bytes)
                    int[] block = ExtractBlock(rgba, x, y, width, height, 4);
                    byte[] dest = new byte[16];
                    CompressDxt5Block(dest, block);

                    // Copy compressed block to output
                    for (int i = 0; i < 16; i++)
                    {
                        dxt5Data[dstOffset + i] = dest[i];
                    }
                    dstOffset += 16;
                }
            }

            return dxt5Data;
        }

        // Compress a single DXT3 block (4x4 pixels = 16 bytes output: 8 bytes alpha + 8 bytes Color)
        private static void CompressDxt3Block(byte[] dest, int[] src)
        {
            CompressAlphaBlockDxt3(dest, src);
            CompressColorBlock(dest, 8, src);
        }

        // Compress explicit alpha for DXT3 (4 bits per pixel, 16 pixels = 64 bits = 8 bytes)
        // Based on PyKotor _compress_alpha_block_dxt3 implementation
        private static void CompressAlphaBlockDxt3(byte[] dest, int[] src)
        {
            // DXT3 stores alpha as 4 bits per pixel, packed into 8 bytes
            // Each byte contains 2 alpha values: lower 4 bits for first pixel, upper 4 bits for second pixel
            for (int i = 0; i < 8; i++)
            {
                // Get alpha values for two pixels
                int alpha1 = src[i * 8 + 3] >> 4; // First pixel alpha (upper 4 bits)
                int alpha2 = src[i * 8 + 7] >> 4; // Second pixel alpha (upper 4 bits)
                dest[i] = (byte)((alpha2 << 4) | alpha1);
            }
        }

        // Compress a single DXT5 block (4x4 pixels = 16 bytes output: 8 bytes interpolated alpha + 8 bytes Color)
        private static void CompressDxt5Block(byte[] dest, int[] src)
        {
            CompressAlphaBlockDxt5(dest, src);
            CompressColorBlock(dest, 8, src);
        }

        // Compress interpolated alpha for DXT5 (similar to color interpolation)
        // Based on PyKotor _compress_alpha_block_dxt5 implementation
        private static void CompressAlphaBlockDxt5(byte[] dest, int[] src)
        {
            // Extract alpha channel from all 16 pixels
            int[] alpha = new int[16];
            for (int i = 0; i < 16; i++)
            {
                alpha[i] = src[i * 4 + 3];
            }

            // Find min and max alpha values
            int minA = alpha[0];
            int maxA = alpha[0];
            for (int i = 1; i < 16; i++)
            {
                if (alpha[i] < minA) minA = alpha[i];
                if (alpha[i] > maxA) maxA = alpha[i];
            }

            // If all alphas are the same, use simple encoding
            if (minA == maxA)
            {
                dest[0] = (byte)maxA;
                dest[1] = (byte)minA;
                for (int i = 2; i < 8; i++)
                {
                    dest[i] = 0;
                }
                return;
            }

            // Ensure maxA >= minA (swap if needed)
            if (minA > maxA)
            {
                int temp = minA;
                minA = maxA;
                maxA = temp;
            }

            dest[0] = (byte)maxA;
            dest[1] = (byte)minA;

            // Calculate indices for each pixel
            ulong indices = 0;
            for (int i = 0; i < 16; i++)
            {
                int code;
                if (alpha[i] == 0)
                {
                    code = 6;
                }
                else if (alpha[i] == 255)
                {
                    code = 7;
                }
                else
                {
                    // Interpolate: t = (alpha[i] - minA) * 7 / (maxA - minA)
                    int t = (alpha[i] - minA) * 7 / (maxA - minA);
                    code = Math.Min(7, t);
                }

                indices |= (ulong)code << (3 * i);
            }

            // Write indices (6 bytes, 48 bits for 16 pixels * 3 bits)
            for (int i = 0; i < 6; i++)
            {
                dest[2 + i] = (byte)((indices >> (8 * i)) & 0xFF);
            }
        }

        // Overload CompressColorBlock to support offset for DXT3/DXT5
        private static void CompressColorBlock(byte[] dest, int offset, int[] src)
        {
            byte[] tempDest = new byte[8];
            CompressColorBlock(tempDest, src);
            for (int i = 0; i < 8; i++)
            {
                dest[offset + i] = tempDest[i];
            }
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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:632-640
        // Original: def copy(self) -> Self
        /// <summary>
        /// Creates a deep copy of this TPC texture.
        /// </summary>
        public TPC Copy()
        {
            TPC instance = FromBlank();
            instance.Layers.Clear();
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
                instance.Layers.Add(layerCopy);
            }
            instance._format = _format;
            instance.IsAnimated = IsAnimated;
            instance.IsCubeMap = IsCubeMap;
            instance._txi = _txi;
            return instance;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:555-574
        // Original: def rotate90(self, times: int) -> None
        /// <summary>
        /// Rotates all mipmaps in 90° steps, clockwise for positive times, counter-clockwise for negative times.
        /// </summary>
        /// <param name="times">Number of 90° rotations (positive for clockwise, negative for counter-clockwise).</param>
        public void Rotate90(int times)
        {
            times = times % 4; // Normalize rotation to 0-3
            if (times == 0)
            {
                return; // No rotation needed
            }

            foreach (var layer in Layers)
            {
                foreach (var mipmap in layer.Mipmaps)
                {
                    if (_format == TPCTextureFormat.DXT1)
                    {
                        mipmap.Data = RotateDxt1(mipmap.Data, mipmap.Width, mipmap.Height, times);
                    }
                    else if (_format == TPCTextureFormat.DXT5)
                    {
                        mipmap.Data = RotateDxt5(mipmap.Data, mipmap.Width, mipmap.Height, times);
                    }
                    else if (!_format.IsDxt())
                    {
                        mipmap.Data = RotateRgbRgba(mipmap.Data, mipmap.Width, mipmap.Height, _format.BytesPerPixel(), times);
                    }
                    else
                    {
                        throw new ArgumentException($"Unsupported format for rotation: {_format}");
                    }

                    // Swap width and height for 90° or 270° rotations
                    if (times % 2 != 0)
                    {
                        int temp = mipmap.Width;
                        mipmap.Width = mipmap.Height;
                        mipmap.Height = temp;
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:576-583
        // Original: def flip_vertically(self) -> None
        /// <summary>
        /// Flips all mipmaps vertically.
        /// </summary>
        public void FlipVertically()
        {
            foreach (var layer in Layers)
            {
                foreach (var mipmap in layer.Mipmaps)
                {
                    if (_format.IsDxt())
                    {
                        mipmap.Data = FlipVerticallyDxt(mipmap.Data, mipmap.Width, mipmap.Height, _format.BytesPerBlock());
                    }
                    else
                    {
                        mipmap.Data = FlipVerticallyRgbRgba(mipmap.Data, mipmap.Width, mipmap.Height, _format.BytesPerPixel());
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:585-592
        // Original: def flip_horizontally(self) -> None
        /// <summary>
        /// Flips all mipmaps horizontally.
        /// </summary>
        public void FlipHorizontally()
        {
            foreach (var layer in Layers)
            {
                foreach (var mipmap in layer.Mipmaps)
                {
                    if (_format.IsDxt())
                    {
                        mipmap.Data = FlipHorizontallyDxt(mipmap.Data, mipmap.Width, mipmap.Height, _format.BytesPerBlock());
                    }
                    else
                    {
                        mipmap.Data = FlipHorizontallyRgbRgba(mipmap.Data, mipmap.Width, mipmap.Height, _format.BytesPerPixel());
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:605-610
        // Original: def decode(self)
        /// <summary>
        /// Decodes compressed formats to their uncompressed equivalents.
        /// </summary>
        public void Decode()
        {
            if (_format == TPCTextureFormat.BGR || _format == TPCTextureFormat.DXT1 || _format == TPCTextureFormat.Greyscale)
            {
                Convert(TPCTextureFormat.RGB);
            }
            else if (_format == TPCTextureFormat.BGRA || _format == TPCTextureFormat.DXT3 || _format == TPCTextureFormat.DXT5)
            {
                Convert(TPCTextureFormat.RGBA);
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:612-617
        // Original: def encode(self)
        /// <summary>
        /// Encodes uncompressed formats to their compressed equivalents.
        /// </summary>
        public void Encode()
        {
            if (_format == TPCTextureFormat.RGB || _format == TPCTextureFormat.BGR || _format == TPCTextureFormat.Greyscale)
            {
                Convert(TPCTextureFormat.DXT1);
            }
            else if (_format == TPCTextureFormat.RGBA || _format == TPCTextureFormat.BGRA)
            {
                Convert(TPCTextureFormat.DXT5);
            }
        }

        #region Rotation and Flip Helper Methods

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/rotate.py:4-35
        // Original: def rotate_rgb_rgba(data: bytearray, width: int, height: int, bytes_per_pixel: int, times: int) -> bytearray
        private static byte[] RotateRgbRgba(byte[] data, int width, int height, int bytesPerPixel, int times)
        {
            times = times % 4; // Normalize to 0-3 range
            if (times == 0)
            {
                return data;
            }

            byte[] newData = new byte[data.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcIdx = (y * width + x) * bytesPerPixel;
                    int dstIdx;
                    if (times == 1 || times == -3)
                    {
                        dstIdx = ((width - 1 - x) * height + y) * bytesPerPixel;
                    }
                    else if (times == 2 || times == -2)
                    {
                        dstIdx = ((height - 1 - y) * width + (width - 1 - x)) * bytesPerPixel;
                    }
                    else if (times == 3 || times == -1)
                    {
                        dstIdx = (x * height + (height - 1 - y)) * bytesPerPixel;
                    }
                    else
                    {
                        dstIdx = srcIdx;
                    }

                    for (int i = 0; i < bytesPerPixel; i++)
                    {
                        newData[dstIdx + i] = data[srcIdx + i];
                    }
                }
            }

            return newData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/dxt_manipulate.py:4-59
        // Original: def rotate_dxt1(data: bytearray, width: int, height: int, times: int) -> bytearray
        private static byte[] RotateDxt1(byte[] data, int width, int height, int times)
        {
            times = times % 4;
            if (times == 0)
            {
                return data;
            }

            int blocksX = width / 4;
            int blocksY = height / 4;
            byte[] newData = new byte[data.Length];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int srcBlockIdx = (by * blocksX + bx) * 8;
                    int dstBlockIdx = srcBlockIdx;
                    if (times == 1 || times == -3)
                    {
                        dstBlockIdx = ((blocksX - 1 - bx) * blocksY + by) * 8;
                    }
                    else if (times == 2 || times == -2)
                    {
                        dstBlockIdx = ((blocksY - 1 - by) * blocksX + (blocksX - 1 - bx)) * 8;
                    }
                    else if (times == 3 || times == -1)
                    {
                        dstBlockIdx = (bx * blocksY + (blocksY - 1 - by)) * 8;
                    }

                    // Copy color data
                    Array.Copy(data, srcBlockIdx, newData, dstBlockIdx, 4);

                    // Rotate pixel indices
                    uint pixels = (uint)(data[srcBlockIdx + 4] | (data[srcBlockIdx + 5] << 8) |
                                        (data[srcBlockIdx + 6] << 16) | (data[srcBlockIdx + 7] << 24));
                    uint rotatedPixels = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        uint srcPixel = (pixels >> (i * 2)) & 0b11;
                        int dstPixel;
                        if (times == 1 || times == -3)
                        {
                            dstPixel = ((i % 4) * 4 + (3 - i / 4)) * 2;
                        }
                        else if (times == 2 || times == -2)
                        {
                            dstPixel = (15 - i) * 2;
                        }
                        else if (times == 3 || times == -1)
                        {
                            dstPixel = ((3 - i % 4) * 4 + (i / 4)) * 2;
                        }
                        else
                        {
                            dstPixel = i * 2;
                        }
                        rotatedPixels |= srcPixel << dstPixel;
                    }

                    newData[dstBlockIdx + 4] = (byte)(rotatedPixels & 0xFF);
                    newData[dstBlockIdx + 5] = (byte)((rotatedPixels >> 8) & 0xFF);
                    newData[dstBlockIdx + 6] = (byte)((rotatedPixels >> 16) & 0xFF);
                    newData[dstBlockIdx + 7] = (byte)((rotatedPixels >> 24) & 0xFF);
                }
            }

            return newData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/dxt_manipulate.py:62-137
        // Original: def rotate_dxt5(data: bytearray, width: int, height: int, times: int) -> bytearray
        private static byte[] RotateDxt5(byte[] data, int width, int height, int times)
        {
            times = times % 4;
            if (times == 0)
            {
                return data;
            }

            int blocksX = width / 4;
            int blocksY = height / 4;
            byte[] newData = new byte[data.Length];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int srcBlockIdx = (by * blocksX + bx) * 16;
                    int dstBlockIdx = srcBlockIdx;
                    if (times == 1 || times == -3)
                    {
                        dstBlockIdx = ((blocksX - 1 - bx) * blocksY + by) * 16;
                    }
                    else if (times == 2 || times == -2)
                    {
                        dstBlockIdx = ((blocksY - 1 - by) * blocksX + (blocksX - 1 - bx)) * 16;
                    }
                    else if (times == 3 || times == -1)
                    {
                        dstBlockIdx = (bx * blocksY + (blocksY - 1 - by)) * 16;
                    }

                    // Copy alpha min/max
                    newData[dstBlockIdx] = data[srcBlockIdx];
                    newData[dstBlockIdx + 1] = data[srcBlockIdx + 1];

                    // Rotate alpha indices
                    ulong alphaIndices = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        alphaIndices |= (ulong)data[srcBlockIdx + 2 + i] << (i * 8);
                    }
                    ulong rotatedAlpha = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        ulong srcAlpha = (alphaIndices >> (i * 3)) & 0b111;
                        int dstAlpha;
                        if (times == 1 || times == -3)
                        {
                            dstAlpha = ((i % 4) * 4 + (3 - i / 4)) * 3;
                        }
                        else if (times == 2 || times == -2)
                        {
                            dstAlpha = (15 - i) * 3;
                        }
                        else if (times == 3 || times == -1)
                        {
                            dstAlpha = ((3 - i % 4) * 4 + (i / 4)) * 3;
                        }
                        else
                        {
                            dstAlpha = i * 3;
                        }
                        rotatedAlpha |= srcAlpha << dstAlpha;
                    }

                    for (int i = 0; i < 6; i++)
                    {
                        newData[dstBlockIdx + 2 + i] = (byte)((rotatedAlpha >> (i * 8)) & 0xFF);
                    }

                    // Copy color data
                    Array.Copy(data, srcBlockIdx + 8, newData, dstBlockIdx + 8, 4);

                    // Rotate color indices (same as DXT1)
                    uint pixels = (uint)(data[srcBlockIdx + 12] | (data[srcBlockIdx + 13] << 8) |
                                        (data[srcBlockIdx + 14] << 16) | (data[srcBlockIdx + 15] << 24));
                    uint rotatedPixels = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        uint srcPixel = (pixels >> (i * 2)) & 0b11;
                        int dstPixel;
                        if (times == 1 || times == -3)
                        {
                            dstPixel = ((i % 4) * 4 + (3 - i / 4)) * 2;
                        }
                        else if (times == 2 || times == -2)
                        {
                            dstPixel = (15 - i) * 2;
                        }
                        else if (times == 3 || times == -1)
                        {
                            dstPixel = ((3 - i % 4) * 4 + (i / 4)) * 2;
                        }
                        else
                        {
                            dstPixel = i * 2;
                        }
                        rotatedPixels |= srcPixel << dstPixel;
                    }

                    newData[dstBlockIdx + 12] = (byte)(rotatedPixels & 0xFF);
                    newData[dstBlockIdx + 13] = (byte)((rotatedPixels >> 8) & 0xFF);
                    newData[dstBlockIdx + 14] = (byte)((rotatedPixels >> 16) & 0xFF);
                    newData[dstBlockIdx + 15] = (byte)((rotatedPixels >> 24) & 0xFF);
                }
            }

            return newData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/rotate.py:37-54
        // Original: def flip_vertically_rgb_rgba(data: bytearray, width: int, height: int, bytes_per_pixel: int) -> bytearray
        private static byte[] FlipVerticallyRgbRgba(byte[] data, int width, int height, int bytesPerPixel)
        {
            byte[] newData = new byte[data.Length];
            int rowSize = width * bytesPerPixel;

            for (int y = 0; y < height; y++)
            {
                int srcRowStart = y * rowSize;
                int dstRowStart = (height - 1 - y) * rowSize;
                Array.Copy(data, srcRowStart, newData, dstRowStart, rowSize);
            }

            return newData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/rotate.py:56-76
        // Original: def flip_horizontally_rgb_rgba(data: bytearray, width: int, height: int, bytes_per_pixel: int) -> bytearray
        private static byte[] FlipHorizontallyRgbRgba(byte[] data, int width, int height, int bytesPerPixel)
        {
            byte[] newData = new byte[data.Length];
            int rowSize = width * bytesPerPixel;

            for (int y = 0; y < height; y++)
            {
                int rowStart = y * rowSize;
                for (int x = 0; x < width; x++)
                {
                    int srcPixelStart = rowStart + x * bytesPerPixel;
                    int dstPixelStart = rowStart + (width - 1 - x) * bytesPerPixel;
                    for (int i = 0; i < bytesPerPixel; i++)
                    {
                        newData[dstPixelStart + i] = data[srcPixelStart + i];
                    }
                }
            }

            return newData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/dxt_manipulate.py:140-163
        // Original: def flip_vertically_dxt(data: bytearray, width: int, height: int, block_size: int) -> bytearray
        private static byte[] FlipVerticallyDxt(byte[] data, int width, int height, int blockSize)
        {
            int blocksX = width / 4;
            int blocksY = height / 4;
            byte[] newData = new byte[data.Length];

            for (int by = 0; by < blocksY; by++)
            {
                int srcRowStart = by * blocksX * blockSize;
                int dstRowStart = (blocksY - 1 - by) * blocksX * blockSize;
                Array.Copy(data, srcRowStart, newData, dstRowStart, blocksX * blockSize);
            }

            return newData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/dxt_manipulate.py:166-220
        // Original: def flip_horizontally_dxt(data: bytearray, width: int, height: int, bytes_per_block: int) -> bytearray
        private static byte[] FlipHorizontallyDxt(byte[] data, int width, int height, int bytesPerBlock)
        {
            int blocksX = width / 4;
            int blocksY = height / 4;
            byte[] newData = new byte[data.Length];

            for (int by = 0; by < blocksY; by++)
            {
                for (int bx = 0; bx < blocksX; bx++)
                {
                    int srcBlockIdx = (by * blocksX + bx) * bytesPerBlock;
                    int dstBlockIdx = (by * blocksX + (blocksX - 1 - bx)) * bytesPerBlock;

                    // Copy block data
                    Array.Copy(data, srcBlockIdx, newData, dstBlockIdx, bytesPerBlock);

                    // Flip pixel indices horizontally
                    if (bytesPerBlock == 8) // DXT1
                    {
                        uint pixels = (uint)(newData[dstBlockIdx + 4] | (newData[dstBlockIdx + 5] << 8) |
                                            (newData[dstBlockIdx + 6] << 16) | (newData[dstBlockIdx + 7] << 24));
                        uint flippedPixels = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            uint row = (pixels >> (i * 8)) & 0xFF;
                            uint flippedRow = ((row & 0b11) << 6) | ((row & 0b1100) << 2) | ((row & 0b110000) >> 2) | ((row & 0b11000000) >> 6);
                            flippedPixels |= flippedRow << (i * 8);
                        }
                        newData[dstBlockIdx + 4] = (byte)(flippedPixels & 0xFF);
                        newData[dstBlockIdx + 5] = (byte)((flippedPixels >> 8) & 0xFF);
                        newData[dstBlockIdx + 6] = (byte)((flippedPixels >> 16) & 0xFF);
                        newData[dstBlockIdx + 7] = (byte)((flippedPixels >> 24) & 0xFF);
                    }
                    else if (bytesPerBlock == 16) // DXT5
                    {
                        // Flip alpha indices
                        ulong alphaIndices = 0;
                        for (int i = 0; i < 6; i++)
                        {
                            alphaIndices |= (ulong)newData[dstBlockIdx + 2 + i] << (i * 8);
                        }
                        ulong flippedAlpha = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            ulong row = (alphaIndices >> (i * 12)) & 0xFFF;
                            ulong flippedRow = ((row & 0b111) << 9) | ((row & 0b111000) << 3) | ((row & 0b111000000) >> 3) | ((row & 0b111000000000) >> 9);
                            flippedAlpha |= flippedRow << (i * 12);
                        }
                        for (int i = 0; i < 6; i++)
                        {
                            newData[dstBlockIdx + 2 + i] = (byte)((flippedAlpha >> (i * 8)) & 0xFF);
                        }

                        // Flip color indices (same as DXT1)
                        uint pixels = (uint)(newData[dstBlockIdx + 12] | (newData[dstBlockIdx + 13] << 8) |
                                            (newData[dstBlockIdx + 14] << 16) | (newData[dstBlockIdx + 15] << 24));
                        uint flippedPixels = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            uint row = (pixels >> (i * 8)) & 0xFF;
                            uint flippedRow = ((row & 0b11) << 6) | ((row & 0b1100) << 2) | ((row & 0b110000) >> 2) | ((row & 0b11000000) >> 6);
                            flippedPixels |= flippedRow << (i * 8);
                        }
                        newData[dstBlockIdx + 12] = (byte)(flippedPixels & 0xFF);
                        newData[dstBlockIdx + 13] = (byte)((flippedPixels >> 8) & 0xFF);
                        newData[dstBlockIdx + 14] = (byte)((flippedPixels >> 16) & 0xFF);
                        newData[dstBlockIdx + 15] = (byte)((flippedPixels >> 24) & 0xFF);
                    }
                }
            }

            return newData;
        }

        #endregion
    }
}

