using System;
using System.IO;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.TPC
{
    /// <summary>
    /// Parses TPC (Texture Pack Container) format files and extracts width, height, and RGBA pixel data.
    /// </summary>
    /// <remarks>
    /// TPC Format Parser Implementation:
    /// - Based on KotOR engine TPC texture loading (swkotor.exe, swkotor2.exe)
    /// - Supports TPC format variants used in KotOR games
    /// - Handles DXT compression formats (DXT1/DXT5) and uncompressed formats (RGB/RGBA/Greyscale/BGRA)
    /// - Converts all formats to RGBA for use with IGraphicsDevice.CreateTexture2D
    /// 
    /// Format Specification (from KotOR Modding Wiki and PyKotor):
    /// - Header (128 bytes):
    ///   - 0x00-0x03: Data size (uint32 LE) - 0 if uncompressed
    ///   - 0x04-0x07: Alpha blend float (4 bytes)
    ///   - 0x08-0x09: Width (uint16 LE)
    ///   - 0x0A-0x0B: Height (uint16 LE)
    ///   - 0x0C: Encoding type (uint8) - 2 = RGB/DXT1, 4 = RGBA/DXT5, 1 = Grayscale, 12 = BGRA
    ///   - 0x0D: Mipmap count (uint8)
    ///   - 0x0E-0x7F: Reserved (114 bytes padding)
    /// - Texture data starts at offset 0x80
    /// 
    /// Based on reverse engineering analysis:
    /// - swkotor.exe: TPC texture loading functions process texture data
    /// - swkotor2.exe: TPC texture loading functions process texture data
    /// - Format compatibility: TPC format supports DXT compression and uncompressed formats
    /// - Located via string references: TPC file extensions and texture loading in KotOR engine
    /// 
    /// Implementation details:
    /// - Parses TPC header to extract width, height, encoding type, and mipmap count
    /// - Detects pixel format from encoding type and compression flag
    /// - Decompresses DXT formats to RGBA using DXT decompression algorithms
    /// - Converts uncompressed formats to RGBA
    /// - Handles mipmaps (uses first mipmap level for texture loading)
    /// - Returns parsed data structure with width, height, and RGBA pixel data
    /// </remarks>
    public class TpcParser : IDisposable
    {
        private const int TPC_HEADER_SIZE = 128;
        private const int TPC_DATA_START_OFFSET = 0x80;
        private const int MAX_DIMENSIONS = 0x8000;

        private readonly byte[] _data;
        private readonly System.IO.BinaryReader _reader;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the TPC parser.
        /// </summary>
        /// <param name="data">The TPC file data.</param>
        public TpcParser(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }
            if (data.Length < TPC_HEADER_SIZE)
            {
                throw new ArgumentException("TPC data is too small to contain a valid header", "data");
            }

            _data = data;
            _reader = new System.IO.BinaryReader(new MemoryStream(data));
        }

        /// <summary>
        /// Parse result containing width, height, and RGBA pixel data.
        /// </summary>
        public class TpcParseResult
        {
            /// <summary>
            /// Gets or sets the texture width in pixels.
            /// </summary>
            public int Width { get; set; }

            /// <summary>
            /// Gets or sets the texture height in pixels.
            /// </summary>
            public int Height { get; set; }

            /// <summary>
            /// Gets or sets the RGBA pixel data (4 bytes per pixel, row-major order).
            /// </summary>
            public byte[] RgbaData { get; set; }
        }

        /// <summary>
        /// Parses the TPC file and returns width, height, and RGBA pixel data.
        /// </summary>
        /// <returns>Parse result with width, height, and RGBA pixel data.</returns>
        /// <exception cref="InvalidDataException">Thrown when the TPC file is invalid or unsupported.</exception>
        public TpcParseResult Parse()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("TpcParser");
            }

            _reader.BaseStream.Position = 0;

            // Read TPC header
            uint dataSize = _reader.ReadUInt32(); // 0x00: Data size (0 if uncompressed)
            bool compressed = dataSize != 0;
            float alphaTest = _reader.ReadSingle(); // 0x04: Alpha blend float
            ushort width = _reader.ReadUInt16(); // 0x08: Width
            ushort height = _reader.ReadUInt16(); // 0x0A: Height

            if (width == 0 || height == 0 || width >= MAX_DIMENSIONS || height >= MAX_DIMENSIONS)
            {
                throw new InvalidDataException($"Invalid TPC dimensions: {width}x{height}");
            }

            byte pixelType = _reader.ReadByte(); // 0x0C: Encoding type
            byte mipmapCount = _reader.ReadByte(); // 0x0D: Mipmap count

            // Skip padding (0x0E-0x7F = 114 bytes)
            _reader.BaseStream.Position = TPC_DATA_START_OFFSET;

            // Determine texture format based on compression and pixel type
            TpcTextureFormat format = DetermineFormat(compressed, pixelType);
            if (format == TpcTextureFormat.Invalid)
            {
                throw new InvalidDataException($"Unsupported TPC format: compressed={compressed}, pixelType={pixelType}");
            }

            // Calculate data size for uncompressed formats
            if (!compressed)
            {
                dataSize = CalculateUncompressedSize(width, height, format);
            }

            // Read texture data (first mipmap level)
            int dataOffset = TPC_DATA_START_OFFSET;
            int mipmapDataSize = compressed ? CalculateCompressedSize(width, height, format) : (int)dataSize;

            if (dataOffset + mipmapDataSize > _data.Length)
            {
                throw new InvalidDataException($"TPC data is too small: expected at least {dataOffset + mipmapDataSize} bytes, got {_data.Length}");
            }

            byte[] textureData = new byte[mipmapDataSize];
            Array.Copy(_data, dataOffset, textureData, 0, mipmapDataSize);

            // Decompress/convert to RGBA
            byte[] rgbaData = ConvertToRgba(textureData, width, height, format);

            return new TpcParseResult
            {
                Width = width,
                Height = height,
                RgbaData = rgbaData
            };
        }

        private TpcTextureFormat DetermineFormat(bool compressed, byte pixelType)
        {
            if (compressed)
            {
                // Compressed formats
                if (pixelType == 2)
                {
                    return TpcTextureFormat.Dxt1;
                }
                if (pixelType == 4)
                {
                    return TpcTextureFormat.Dxt5;
                }
            }
            else
            {
                // Uncompressed formats
                if (pixelType == 1)
                {
                    return TpcTextureFormat.Greyscale;
                }
                if (pixelType == 2)
                {
                    return TpcTextureFormat.Rgb;
                }
                if (pixelType == 4)
                {
                    return TpcTextureFormat.Rgba;
                }
                if (pixelType == 12)
                {
                    return TpcTextureFormat.Bgra;
                }
            }

            return TpcTextureFormat.Invalid;
        }

        private uint CalculateUncompressedSize(int width, int height, TpcTextureFormat format)
        {
            int bytesPerPixel = GetBytesPerPixel(format);
            return (uint)(width * height * bytesPerPixel);
        }

        private int CalculateCompressedSize(int width, int height, TpcTextureFormat format)
        {
            if (format == TpcTextureFormat.Dxt1)
            {
                // DXT1: 8 bytes per 4x4 block
                int blocksW = (width + 3) / 4;
                int blocksH = (height + 3) / 4;
                return blocksW * blocksH * 8;
            }
            if (format == TpcTextureFormat.Dxt5)
            {
                // DXT5: 16 bytes per 4x4 block
                int blocksW = (width + 3) / 4;
                int blocksH = (height + 3) / 4;
                return blocksW * blocksH * 16;
            }

            throw new InvalidDataException($"Cannot calculate compressed size for format: {format}");
        }

        private int GetBytesPerPixel(TpcTextureFormat format)
        {
            switch (format)
            {
                case TpcTextureFormat.Greyscale:
                    return 1;
                case TpcTextureFormat.Rgb:
                    return 3;
                case TpcTextureFormat.Rgba:
                case TpcTextureFormat.Bgra:
                    return 4;
                default:
                    return 4; // Default to 4 for compressed formats (after decompression)
            }
        }

        private byte[] ConvertToRgba(byte[] data, int width, int height, TpcTextureFormat format)
        {
            switch (format)
            {
                case TpcTextureFormat.Dxt1:
                    return DecompressDxt1(data, width, height);
                case TpcTextureFormat.Dxt5:
                    return DecompressDxt5(data, width, height);
                case TpcTextureFormat.Greyscale:
                    return ConvertGreyscaleToRgba(data, width, height);
                case TpcTextureFormat.Rgb:
                    return ConvertRgbToRgba(data, width, height);
                case TpcTextureFormat.Rgba:
                    return data; // Already RGBA
                case TpcTextureFormat.Bgra:
                    return ConvertBgraToRgba(data, width, height);
                default:
                    throw new InvalidDataException($"Unsupported format for conversion: {format}");
            }
        }

        private byte[] DecompressDxt1(byte[] data, int width, int height)
        {
            // DXT1 decompression: 8 bytes per 4x4 block
            int blocksW = (width + 3) / 4;
            int blocksH = (height + 3) / 4;
            byte[] rgba = new byte[width * height * 4];

            for (int blockY = 0; blockY < blocksH; blockY++)
            {
                for (int blockX = 0; blockX < blocksW; blockX++)
                {
                    int blockOffset = (blockY * blocksW + blockX) * 8;
                    if (blockOffset + 8 > data.Length)
                    {
                        break;
                    }

                    // Read color endpoints (RGB565 format, little endian)
                    ushort color0 = (ushort)(data[blockOffset] | (data[blockOffset + 1] << 8));
                    ushort color1 = (ushort)(data[blockOffset + 2] | (data[blockOffset + 3] << 8));
                    uint indices = (uint)(data[blockOffset + 4] | (data[blockOffset + 5] << 8) |
                                         (data[blockOffset + 6] << 16) | (data[blockOffset + 7] << 24));

                    // Decode color endpoints to RGB888
                    byte[] colors = new byte[4 * 3];
                    DecodeRgb565(color0, colors, 0);
                    DecodeRgb565(color1, colors, 3);

                    // Interpolate colors
                    if (color0 > color1)
                    {
                        // 4-color mode
                        colors[6] = (byte)((2 * colors[0] + colors[3]) / 3);
                        colors[7] = (byte)((2 * colors[1] + colors[4]) / 3);
                        colors[8] = (byte)((2 * colors[2] + colors[5]) / 3);
                        colors[9] = (byte)((colors[0] + 2 * colors[3]) / 3);
                        colors[10] = (byte)((colors[1] + 2 * colors[4]) / 3);
                        colors[11] = (byte)((colors[2] + 2 * colors[5]) / 3);
                    }
                    else
                    {
                        // 3-color mode (transparent)
                        colors[6] = (byte)((colors[0] + colors[3]) / 2);
                        colors[7] = (byte)((colors[1] + colors[4]) / 2);
                        colors[8] = (byte)((colors[2] + colors[5]) / 2);
                        colors[9] = 0;
                        colors[10] = 0;
                        colors[11] = 0;
                    }

                    // Decode 4x4 block
                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            int pixelX = blockX * 4 + x;
                            int pixelY = blockY * 4 + y;
                            if (pixelX >= width || pixelY >= height)
                            {
                                continue;
                            }

                            int index = (int)((indices >> (y * 4 + x) * 2) & 0x3);
                            int rgbaOffset = (pixelY * width + pixelX) * 4;
                            rgba[rgbaOffset] = colors[index * 3];
                            rgba[rgbaOffset + 1] = colors[index * 3 + 1];
                            rgba[rgbaOffset + 2] = colors[index * 3 + 2];
                            rgba[rgbaOffset + 3] = (color0 > color1 || index != 3) ? (byte)255 : (byte)0;
                        }
                    }
                }
            }

            return rgba;
        }

        private byte[] DecompressDxt5(byte[] data, int width, int height)
        {
            // DXT5 decompression: 16 bytes per 4x4 block (8 bytes alpha + 8 bytes DXT1)
            int blocksW = (width + 3) / 4;
            int blocksH = (height + 3) / 4;
            byte[] rgba = new byte[width * height * 4];

            for (int blockY = 0; blockY < blocksH; blockY++)
            {
                for (int blockX = 0; blockX < blocksW; blockX++)
                {
                    int blockOffset = (blockY * blocksW + blockX) * 16;
                    if (blockOffset + 16 > data.Length)
                    {
                        break;
                    }

                    // Read alpha endpoints and indices
                    byte alpha0 = data[blockOffset];
                    byte alpha1 = data[blockOffset + 1];
                    ulong alphaIndices = ((ulong)data[blockOffset + 2]) | ((ulong)data[blockOffset + 3] << 8) |
                                         ((ulong)data[blockOffset + 4] << 16) | ((ulong)data[blockOffset + 5] << 24) |
                                         ((ulong)data[blockOffset + 6] << 32) | ((ulong)data[blockOffset + 7] << 40);

                    // Read color endpoints (RGB565 format, little endian)
                    ushort color0 = (ushort)(data[blockOffset + 8] | (data[blockOffset + 9] << 8));
                    ushort color1 = (ushort)(data[blockOffset + 10] | (data[blockOffset + 11] << 8));
                    uint colorIndices = (uint)(data[blockOffset + 12] | (data[blockOffset + 13] << 8) |
                                               (data[blockOffset + 14] << 16) | (data[blockOffset + 15] << 24));

                    // Decode alpha values
                    byte[] alphas = new byte[8];
                    alphas[0] = alpha0;
                    alphas[1] = alpha1;
                    if (alpha0 > alpha1)
                    {
                        // 8-alpha mode
                        alphas[2] = (byte)((6 * alpha0 + 1 * alpha1) / 7);
                        alphas[3] = (byte)((5 * alpha0 + 2 * alpha1) / 7);
                        alphas[4] = (byte)((4 * alpha0 + 3 * alpha1) / 7);
                        alphas[5] = (byte)((3 * alpha0 + 4 * alpha1) / 7);
                        alphas[6] = (byte)((2 * alpha0 + 5 * alpha1) / 7);
                        alphas[7] = (byte)((1 * alpha0 + 6 * alpha1) / 7);
                    }
                    else
                    {
                        // 6-alpha mode
                        alphas[2] = (byte)((4 * alpha0 + 1 * alpha1) / 5);
                        alphas[3] = (byte)((3 * alpha0 + 2 * alpha1) / 5);
                        alphas[4] = (byte)((2 * alpha0 + 3 * alpha1) / 5);
                        alphas[5] = (byte)((1 * alpha0 + 4 * alpha1) / 5);
                        alphas[6] = 0;
                        alphas[7] = 255;
                    }

                    // Decode color endpoints to RGB888
                    byte[] colors = new byte[4 * 3];
                    DecodeRgb565(color0, colors, 0);
                    DecodeRgb565(color1, colors, 3);

                    // Interpolate colors (4-color mode for DXT5)
                    colors[6] = (byte)((2 * colors[0] + colors[3]) / 3);
                    colors[7] = (byte)((2 * colors[1] + colors[4]) / 3);
                    colors[8] = (byte)((2 * colors[2] + colors[5]) / 3);
                    colors[9] = (byte)((colors[0] + 2 * colors[3]) / 3);
                    colors[10] = (byte)((colors[1] + 2 * colors[4]) / 3);
                    colors[11] = (byte)((colors[2] + 2 * colors[5]) / 3);

                    // Decode 4x4 block
                    for (int y = 0; y < 4; y++)
                    {
                        for (int x = 0; x < 4; x++)
                        {
                            int pixelX = blockX * 4 + x;
                            int pixelY = blockY * 4 + y;
                            if (pixelX >= width || pixelY >= height)
                            {
                                continue;
                            }

                            int alphaIndex = (int)((alphaIndices >> (y * 4 + x) * 3) & 0x7);
                            int colorIndex = (int)((colorIndices >> (y * 4 + x) * 2) & 0x3);
                            int rgbaOffset = (pixelY * width + pixelX) * 4;
                            rgba[rgbaOffset] = colors[colorIndex * 3];
                            rgba[rgbaOffset + 1] = colors[colorIndex * 3 + 1];
                            rgba[rgbaOffset + 2] = colors[colorIndex * 3 + 2];
                            rgba[rgbaOffset + 3] = alphas[alphaIndex];
                        }
                    }
                }
            }

            return rgba;
        }

        private void DecodeRgb565(ushort color, byte[] output, int offset)
        {
            // RGB565: RRRRR GGGGGG BBBBB
            output[offset] = (byte)(((color >> 11) & 0x1F) * 255 / 31); // R
            output[offset + 1] = (byte)(((color >> 5) & 0x3F) * 255 / 63); // G
            output[offset + 2] = (byte)((color & 0x1F) * 255 / 31); // B
        }

        private byte[] ConvertGreyscaleToRgba(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            for (int i = 0; i < data.Length; i++)
            {
                rgba[i * 4] = data[i];
                rgba[i * 4 + 1] = data[i];
                rgba[i * 4 + 2] = data[i];
                rgba[i * 4 + 3] = 255;
            }
            return rgba;
        }

        private byte[] ConvertRgbToRgba(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            for (int i = 0; i < data.Length / 3; i++)
            {
                rgba[i * 4] = data[i * 3];
                rgba[i * 4 + 1] = data[i * 3 + 1];
                rgba[i * 4 + 2] = data[i * 3 + 2];
                rgba[i * 4 + 3] = 255;
            }
            return rgba;
        }

        private byte[] ConvertBgraToRgba(byte[] data, int width, int height)
        {
            byte[] rgba = new byte[width * height * 4];
            for (int i = 0; i < data.Length / 4; i++)
            {
                rgba[i * 4] = data[i * 4 + 2]; // B -> R
                rgba[i * 4 + 1] = data[i * 4 + 1]; // G -> G
                rgba[i * 4 + 2] = data[i * 4]; // R -> B
                rgba[i * 4 + 3] = data[i * 4 + 3]; // A -> A
            }
            return rgba;
        }

        /// <summary>
        /// Disposes the parser and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _reader?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// TPC texture format enumeration.
    /// </summary>
    internal enum TpcTextureFormat
    {
        Invalid = -1,
        Greyscale = 0,
        Dxt1 = 1,
        Dxt5 = 2,
        Rgb = 3,
        Rgba = 4,
        Bgra = 5
    }
}

