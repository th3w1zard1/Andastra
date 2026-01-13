using System;
using System.IO;
using System.Text;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:289-448
    // Complete writer implementation with animated texture support, cube maps, dimension validation, and BGRA swizzling
    public class TPCBinaryWriter : IDisposable
    {
        private readonly TPC _tpc;
        private readonly RawBinaryWriter _writer;
        private readonly int _initialLayerCount;
        private readonly int _mipmapCount;

        // Constants matching PyKotor implementation
        private const int MAX_DIMENSIONS = 0x8000;
        private const int IMG_DATA_START_OFFSET = 0x80;

        // Encoding values matching PyKotor implementation
        private const int ENCODING_GRAY = 0x01;
        private const int ENCODING_RGB = 0x02;
        private const int ENCODING_RGBA = 0x04;
        private const int ENCODING_BGRA = 0x0C;

        public TPCBinaryWriter(TPC tpc, string filepath)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToFile(filepath);
            _initialLayerCount = tpc.Layers?.Count ?? 0;
            _mipmapCount = _initialLayerCount > 0 && tpc.Layers[0].Mipmaps.Count > 0 ? tpc.Layers[0].Mipmaps.Count : 0;
        }

        public TPCBinaryWriter(TPC tpc, Stream target)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToStream(target);
            _initialLayerCount = tpc.Layers?.Count ?? 0;
            _mipmapCount = _initialLayerCount > 0 && tpc.Layers[0].Mipmaps.Count > 0 ? tpc.Layers[0].Mipmaps.Count : 0;
        }

        public TPCBinaryWriter(TPC tpc)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToByteArray();
            _initialLayerCount = tpc.Layers?.Count ?? 0;
            _mipmapCount = _initialLayerCount > 0 && tpc.Layers[0].Mipmaps.Count > 0 ? tpc.Layers[0].Mipmaps.Count : 0;
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                if (_tpc.Layers == null || _tpc.Layers.Count == 0)
                {
                    throw new ArgumentException("TPC contains no layers");
                }

                var dimensions = _tpc.Dimensions();
                int frameWidth = dimensions.width;
                int frameHeight = dimensions.height;
                TPCTextureFormat format = _tpc.Format();

                // Validate dimensions
                ValidateDimensions(frameWidth, frameHeight);

                // Handle animated textures and cube maps
                int layerCount = _initialLayerCount;
                int layerWidth = frameWidth;
                int layerHeight = frameHeight;
                int width = frameWidth;
                int height = frameHeight;

                if (_tpc.IsAnimated)
                {
                    var txi = _tpc.TxiObject;
                    int numx = Math.Max(1, txi?.Features?.Numx ?? 0);
                    int numy = Math.Max(1, txi?.Features?.Numy ?? 0);

                    if (numx * numy != layerCount && layerCount > 0)
                    {
                        numx = layerCount;
                        numy = 1;
                    }
                    else if (layerCount == 0)
                    {
                        layerCount = Math.Max(1, numx * numy);
                    }

                    width = frameWidth * Math.Max(1, numx);
                    height = frameHeight * Math.Max(1, numy);
                    layerWidth = frameWidth;
                    layerHeight = frameHeight;

                    if (layerWidth <= 0 || layerHeight <= 0)
                    {
                        throw new ArgumentException($"Invalid layer dimensions ({layerWidth}x{layerHeight}) for animated texture");
                    }
                }
                else if (_tpc.IsCubeMap)
                {
                    if (layerCount != 6)
                    {
                        throw new ArgumentException($"Cubemap must have exactly 6 layers, found {layerCount}");
                    }
                    height = frameHeight * layerCount;
                }

                // Calculate data size for compressed formats
                int baseLevelSize = _tpc.Layers.Count > 0 && _tpc.Layers[0].Mipmaps.Count > 0
                    ? _tpc.Layers[0].Mipmaps[0].Data.Length
                    : 0;
                int dataSize = 0;
                if (format.IsDxt())
                {
                    int layers = _tpc.IsAnimated ? layerCount : 1;
                    dataSize = baseLevelSize * Math.Max(1, layers);
                }

                // Write header (128 bytes)
                int pixelEncoding = GetPixelEncoding(format);
                _writer.WriteUInt32((uint)dataSize); // 0x00-0x03: Data size (0 when uncompressed)
                _writer.WriteSingle(_tpc.AlphaTest); // 0x04-0x07: Alpha blending threshold
                _writer.WriteUInt16((ushort)width); // 0x08-0x09: Width
                _writer.WriteUInt16((ushort)height); // 0x0A-0x0B: Height
                _writer.WriteUInt8((byte)pixelEncoding); // 0x0C: Pixel encoding
                _writer.WriteUInt8((byte)_mipmapCount); // 0x0D: Mipmap count
                _writer.WriteBytes(new byte[0x72]); // 0x0E-0x7F: Reserved padding

                // Write texture data for each layer
                foreach (var layer in _tpc.Layers)
                {
                    for (int mipmapIdx = 0; mipmapIdx < _mipmapCount; mipmapIdx++)
                    {
                        if (mipmapIdx >= layer.Mipmaps.Count)
                        {
                            break;
                        }

                        var mipmap = layer.Mipmaps[mipmapIdx];
                        int currentWidth = Math.Max(1, layerWidth >> mipmapIdx);
                        int currentHeight = Math.Max(1, layerHeight >> mipmapIdx);

                        // Validate mipmap dimensions
                        if (mipmap.Width != currentWidth || mipmap.Height != currentHeight)
                        {
                            throw new ArgumentException(
                                $"Invalid mipmap dimensions at level {mipmapIdx}. " +
                                $"Expected {currentWidth}x{currentHeight}, " +
                                $"got {mipmap.Width}x{mipmap.Height}");
                        }

                        byte[] mipmapData = mipmap.Data;

                        // Apply BGRA swizzling for power-of-two textures
                        if (format == TPCTextureFormat.BGRA && IsPowerOfTwo(currentWidth))
                        {
                            mipmapData = Swizzle(mipmapData, currentWidth, currentHeight, format.BytesPerPixel());
                        }

                        _writer.WriteBytes(mipmapData);
                    }
                }

                // Write TXI data if present
                if (string.IsNullOrWhiteSpace(_tpc.Txi))
                {
                    return;
                }

                string txiPayload = _tpc.Txi.Trim().Replace("\r\n", "\n").Replace("\r", "\n");
                if (string.IsNullOrEmpty(txiPayload))
                {
                    return;
                }

                string[] txiLines = txiPayload.Split(new[] { '\n' }, StringSplitOptions.None);
                string normalized = string.Join("\r\n", txiLines);
                if (!normalized.EndsWith("\r\n"))
                {
                    normalized += "\r\n";
                }

                byte[] txiBytes = Encoding.ASCII.GetBytes(normalized);
                _writer.WriteBytes(txiBytes);
                _writer.WriteUInt8(0); // Null terminator
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        private void ValidateDimensions(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException($"Invalid dimensions: {width}x{height}");
            }
            if (width >= MAX_DIMENSIONS || height >= MAX_DIMENSIONS)
            {
                throw new ArgumentException($"Dimensions exceed maximum allowed: {width}x{height}");
            }
        }

        private int GetPixelEncoding(TPCTextureFormat format)
        {
            if (format == TPCTextureFormat.Greyscale)
            {
                return ENCODING_GRAY;
            }
            if (format == TPCTextureFormat.RGB || format == TPCTextureFormat.DXT1)
            {
                return ENCODING_RGB;
            }
            if (format == TPCTextureFormat.RGBA || format == TPCTextureFormat.DXT5)
            {
                return ENCODING_RGBA;
            }
            if (format == TPCTextureFormat.BGRA)
            {
                return ENCODING_BGRA;
            }
            throw new ArgumentException("Invalid TPC texture format: " + format);
        }

        private static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        // Swizzle pixel data for GPU-friendly access patterns
        // Matching PyKotor implementation at io_tpc.py:21-52
        private static byte[] Swizzle(byte[] data, int width, int height, int bytesPerPixel)
        {
            if (data == null || data.Length == 0)
            {
                return data;
            }

            byte[] swizzled = new byte[width * height * bytesPerPixel];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int srcOffset = (y * width + x) * bytesPerPixel;
                    int dstOffset = SwizzleOffset(x, y, width, height) * bytesPerPixel;

                    for (int i = 0; i < bytesPerPixel; i++)
                    {
                        if (srcOffset + i < data.Length && dstOffset + i < swizzled.Length)
                        {
                            swizzled[dstOffset + i] = data[srcOffset + i];
                        }
                    }
                }
            }

            return swizzled;
        }

        private static int SwizzleOffset(int x, int y, int w, int h)
        {
            int log2W = Log2(w);
            int log2H = Log2(h);
            int offset = 0;
            int shift = 0;
            int tempX = x;
            int tempY = y;

            while (log2W > 0 || log2H > 0)
            {
                if (log2W > 0)
                {
                    offset |= (tempX & 1) << shift;
                    tempX >>= 1;
                    shift++;
                    log2W--;
                }
                if (log2H > 0)
                {
                    offset |= (tempY & 1) << shift;
                    tempY >>= 1;
                    shift++;
                    log2H--;
                }
            }

            return offset;
        }

        private static int Log2(int value)
        {
            if (value <= 0)
            {
                return 0;
            }
            int result = 0;
            while (value > 1)
            {
                value >>= 1;
                result++;
            }
            return result;
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}
