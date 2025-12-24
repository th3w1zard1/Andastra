using System;
using System.IO;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TPC;

namespace Andastra.Parsing.Resource.Formats.TEX
{
    /// <summary>
    /// Parses TEX (Texture) format files and extracts width, height, and RGBA pixel data.
    /// </summary>
    /// <remarks>
    /// TEX Format Parser Implementation:
    /// - Based on Eclipse engine TEX texture loading (daorigins.exe, DragonAge2.exe)
    /// - Supports Eclipse engine TEX format variants
    /// - Handles DXT compression formats (DXT1/DXT3/DXT5) and uncompressed formats
    /// - Converts all formats to RGBA for use with IGraphicsDevice.CreateTexture2D
    /// 
    /// Format Support:
    /// - Eclipse TEX: May have a simple header or be DDS-compatible
    /// - Standard DDS variant: TEX files may contain DDS data with optional TEX header
    /// - BioWare variant: Simplified header similar to BioWare DDS format (width/height/bpp/dataSize/float, DXT1/DXT5 only, power-of-two)
    /// - Pixel formats: DXT1/DXT3/DXT5 compression, uncompressed RGB/RGBA/BGR/BGRA
    /// 
    /// Based on reverse engineering analysis:
    /// - daorigins.exe: TEX texture loading functions process texture data
    /// - DragonAge2.exe: TEX texture loading functions process texture data
    /// - Format compatibility: Eclipse TEX format supports DXT compression and uncompressed formats
    /// - Located via string references: TEX file extensions and texture loading in Eclipse engine
    /// 
    /// Implementation details:
    /// - Attempts to parse as standard DDS first (TEX may be DDS-compatible)
    /// - Falls back to BioWare-style TEX header parsing
    /// - Parses header to extract width and height
    /// - Detects pixel format from header or data
    /// - Decompresses DXT formats to RGBA
    /// - Converts uncompressed formats to RGBA
    /// - Returns parsed data structure with width, height, and RGBA pixel data
    /// </remarks>
    public class TexParser : IDisposable
    {
        private const uint DDS_MAGIC = 0x44445320; // "DDS "
        private const int DDS_HEADER_SIZE = 124;
        private const uint HEADER_FLAGS_MIPS = 0x00020000;
        private const uint PIXELFLAG_ALPHA = 0x00000001;
        private const uint PIXELFLAG_FOURCC = 0x00000004;
        private const uint PIXELFLAG_INDEXED = 0x00000020;
        private const uint PIXELFLAG_RGB = 0x00000040;
        private const uint FOURCC_DXT1 = 0x44585431; // "DXT1"
        private const uint FOURCC_DXT3 = 0x44585433; // "DXT3"
        private const uint FOURCC_DXT5 = 0x44585435; // "DXT5"
        private const int MAX_DIMENSION = 0x8000;
        private const int MIN_TEX_HEADER_SIZE = 16; // Minimum header size for TEX format

        private readonly RawBinaryReader _reader;

        private enum TEXDataLayout
        {
            Direct,
            Argb4444,
            A1R5G5B5,
            R5G6B5
        }

        private struct TEXPixelFormat
        {
            public int Size;
            public uint Flags;
            public uint FourCC;
            public int BitCount;
            public uint RMask;
            public uint GMask;
            public uint BMask;
            public uint AMask;
        }

        /// <summary>
        /// Result structure containing parsed TEX data.
        /// </summary>
        public struct TexParseResult
        {
            /// <summary>
            /// Texture width in pixels.
            /// </summary>
            public int Width { get; set; }

            /// <summary>
            /// Texture height in pixels.
            /// </summary>
            public int Height { get; set; }

            /// <summary>
            /// RGBA pixel data (width * height * 4 bytes).
            /// </summary>
            public byte[] RgbaData { get; set; }
        }

        /// <summary>
        /// Initializes a new instance of the TEX parser from byte array.
        /// </summary>
        /// <param name="data">TEX file data.</param>
        /// <param name="offset">Offset into the data array.</param>
        /// <param name="size">Size of data to read (null for all remaining data).</param>
        public TexParser(byte[] data, int offset = 0, int? size = null)
        {
            _reader = RawBinaryReader.FromBytes(data, offset, size);
        }

        /// <summary>
        /// Initializes a new instance of the TEX parser from file path.
        /// </summary>
        /// <param name="filepath">Path to TEX file.</param>
        /// <param name="offset">Offset into the file.</param>
        /// <param name="size">Size of data to read (null for all remaining data).</param>
        public TexParser(string filepath, int offset = 0, int? size = null)
        {
            _reader = RawBinaryReader.FromFile(filepath, offset, size);
        }

        /// <summary>
        /// Initializes a new instance of the TEX parser from stream.
        /// </summary>
        /// <param name="source">Source stream.</param>
        /// <param name="offset">Offset into the stream.</param>
        /// <param name="size">Size of data to read (null for all remaining data).</param>
        public TexParser(Stream source, int offset = 0, int? size = null)
        {
            _reader = RawBinaryReader.FromStream(source, offset, size);
        }

        /// <summary>
        /// Parses the TEX file and returns width, height, and RGBA pixel data.
        /// </summary>
        /// <returns>Parsed TEX data with width, height, and RGBA pixel data.</returns>
        public TexParseResult Parse()
        {
            int startPos = _reader.Position;

            // Check if it's a DDS file (TEX may be DDS-compatible)
            if (_reader.Remaining >= 4)
            {
                byte[] magicBytes = _reader.Peek(4);
                if (magicBytes.Length == 4)
                {
                    uint magic = BitConverter.ToUInt32(magicBytes, 0);
                    // DDS magic is big-endian "DDS "
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(magicBytes);
                        magic = BitConverter.ToUInt32(magicBytes, 0);
                    }
                    if (magic == DDS_MAGIC)
                    {
                        // TEX file contains DDS data directly
                        return ParseDDSCompatible();
                    }
                }
            }

            // Reset and try BioWare-style TEX header
            _reader.Seek(startPos);
            return ParseBiowareTEX();
        }

        /// <summary>
        /// Parses TEX file that contains DDS data directly.
        /// </summary>
        private TexParseResult ParseDDSCompatible()
        {
            uint magic = _reader.ReadUInt32(bigEndian: true);
            if (magic != DDS_MAGIC)
            {
                throw new ArgumentException("Invalid DDS magic in TEX file");
            }

            int headerSize = (int)_reader.ReadUInt32();
            if (headerSize != DDS_HEADER_SIZE)
            {
                throw new ArgumentException($"Invalid DDS header size in TEX file: {headerSize}");
            }

            uint flags = _reader.ReadUInt32();
            int height = (int)_reader.ReadUInt32();
            int width = (int)_reader.ReadUInt32();
            if (width >= MAX_DIMENSION || height >= MAX_DIMENSION)
            {
                throw new ArgumentException($"Unsupported TEX image dimensions ({width}x{height})");
            }

            _reader.Skip(8); // pitch + depth
            int mipCount = (int)_reader.ReadUInt32();
            if ((flags & HEADER_FLAGS_MIPS) == 0)
            {
                mipCount = 1;
            }

            _reader.Skip(44); // reserved
            TEXPixelFormat fmt = new TEXPixelFormat
            {
                Size = (int)_reader.ReadUInt32(),
                Flags = _reader.ReadUInt32(),
                FourCC = _reader.ReadUInt32(bigEndian: true),
                BitCount = (int)_reader.ReadUInt32(),
                RMask = _reader.ReadUInt32(),
                GMask = _reader.ReadUInt32(),
                BMask = _reader.ReadUInt32(),
                AMask = _reader.ReadUInt32()
            };

            _reader.ReadUInt32(); // caps1
            uint caps2 = _reader.ReadUInt32();
            _reader.Skip(8); // caps3 + caps4
            _reader.Skip(4); // reserved2

            // Check for cube maps (not supported for GUI textures)
            if ((caps2 & 0x00000200) != 0)
            {
                throw new ArgumentException("Cube maps are not supported for GUI textures");
            }

            (TPCTextureFormat tpcFormat, TEXDataLayout dataLayout) = DetectFormat(fmt);

            // Read first mipmap only (GUI textures don't need mipmaps)
            int fileSize = FileMipmapSize(width, height, tpcFormat, dataLayout);
            byte[] raw = _reader.ReadBytes(fileSize);
            if (raw.Length != fileSize)
            {
                throw new ArgumentException("TEX file truncated while reading pixel data.");
            }

            byte[] rgbaData = ConvertToRgba(raw, width, height, tpcFormat, dataLayout);

            return new TexParseResult
            {
                Width = width,
                Height = height,
                RgbaData = rgbaData
            };
        }

        /// <summary>
        /// Parses BioWare-style TEX header format.
        /// Based on BioWare DDS variant format (daorigins.exe, DragonAge2.exe, swkotor.exe, swkotor2.exe).
        /// BioWare DDS header structure:
        /// - uint32: width (must be power-of-two)
        /// - uint32: height (must be power-of-two)
        /// - uint32: bytes-per-pixel (3 = DXT1, 4 = DXT5)
        /// - uint32: data size (validated against expected size)
        /// - float: unknown metadata (4 bytes, skipped)
        /// - Pixel data: DXT1 or DXT5 compressed texture data
        /// </summary>
        private TexParseResult ParseBiowareTEX()
        {
            // BioWare DDS variant requires at least 20 bytes (width + height + bpp + dataSize + float)
            const int BIOWARE_DDS_HEADER_SIZE = 20;

            if (_reader.Remaining < BIOWARE_DDS_HEADER_SIZE)
            {
                throw new ArgumentException("TEX file too small to contain valid BioWare DDS header");
            }

            // Read width and height
            int width = (int)_reader.ReadUInt32();
            int height = (int)_reader.ReadUInt32();

            if (width >= MAX_DIMENSION || height >= MAX_DIMENSION)
            {
                throw new ArgumentException($"Unsupported TEX image dimensions ({width}x{height})");
            }

            if (width == 0 || height == 0)
            {
                throw new ArgumentException("TEX file has invalid dimensions (width or height is 0)");
            }

            // BioWare DDS requires power-of-two dimensions
            // Check: (x & (x - 1)) == 0 for power-of-two
            if ((width & (width - 1)) != 0 || (height & (height - 1)) != 0)
            {
                throw new ArgumentException("BioWare DDS variant requires power-of-two dimensions");
            }

            // Read bytes-per-pixel (bpp)
            // BioWare DDS only supports:
            // - 3 = DXT1 (RGB compressed, no alpha)
            // - 4 = DXT5 (RGBA compressed, with alpha)
            int bpp = (int)_reader.ReadUInt32();
            TPCTextureFormat tpcFormat;
            TEXDataLayout dataLayout = TEXDataLayout.Direct;

            if (bpp == 3)
            {
                tpcFormat = TPCTextureFormat.DXT1;
            }
            else if (bpp == 4)
            {
                tpcFormat = TPCTextureFormat.DXT5;
            }
            else
            {
                throw new ArgumentException($"Unsupported BioWare DDS bytes-per-pixel value: {bpp} (only 3=DXT1 and 4=DXT5 are supported)");
            }

            // Read and validate data size
            int dataSize = (int)_reader.ReadUInt32();
            // Expected data size for first mipmap:
            // - DXT1 (bpp=3): (width * height) / 2
            // - DXT5 (bpp=4): width * height
            int expectedDataSize = (bpp == 3) ? (width * height) / 2 : width * height;

            if (dataSize != expectedDataSize)
            {
                throw new ArgumentException($"BioWare DDS data size mismatch: {dataSize} != {expectedDataSize} (width={width}, height={height}, bpp={bpp})");
            }

            // Skip unknown float field (4 bytes)
            // This field is present in BioWare DDS format but its purpose is unknown
            _reader.Skip(4);

            // Read pixel data (first mipmap only for GUI textures)
            // BioWare DDS may contain multiple mipmaps, but we only need the first one
            int fileSize = FileMipmapSize(width, height, tpcFormat, dataLayout);
            byte[] raw = _reader.ReadBytes(fileSize);
            if (raw.Length != fileSize)
            {
                throw new ArgumentException("TEX file truncated while reading pixel data.");
            }

            byte[] rgbaData = ConvertToRgba(raw, width, height, tpcFormat, dataLayout);

            return new TexParseResult
            {
                Width = width,
                Height = height,
                RgbaData = rgbaData
            };
        }

        private (TPCTextureFormat, TEXDataLayout) DetectFormat(TEXPixelFormat fmt)
        {
            TEXDataLayout dataLayout = TEXDataLayout.Direct;

            if ((fmt.Flags & PIXELFLAG_FOURCC) != 0 && fmt.FourCC == FOURCC_DXT1)
            {
                return (TPCTextureFormat.DXT1, dataLayout);
            }
            if ((fmt.Flags & PIXELFLAG_FOURCC) != 0 && fmt.FourCC == FOURCC_DXT3)
            {
                return (TPCTextureFormat.DXT3, dataLayout);
            }
            if ((fmt.Flags & PIXELFLAG_FOURCC) != 0 && fmt.FourCC == FOURCC_DXT5)
            {
                return (TPCTextureFormat.DXT5, dataLayout);
            }
            if ((fmt.Flags & PIXELFLAG_RGB) != 0 && (fmt.Flags & PIXELFLAG_ALPHA) != 0 &&
                fmt.BitCount == 32 && fmt.RMask == 0x00FF0000 && fmt.GMask == 0x0000FF00 &&
                fmt.BMask == 0x000000FF && fmt.AMask == 0xFF000000)
            {
                return (TPCTextureFormat.BGRA, dataLayout);
            }
            if ((fmt.Flags & PIXELFLAG_RGB) != 0 && (fmt.Flags & PIXELFLAG_ALPHA) == 0 &&
                fmt.BitCount == 24 && fmt.RMask == 0x00FF0000 && fmt.GMask == 0x0000FF00 &&
                fmt.BMask == 0x000000FF)
            {
                return (TPCTextureFormat.BGR, dataLayout);
            }
            if ((fmt.Flags & PIXELFLAG_RGB) != 0 && (fmt.Flags & PIXELFLAG_ALPHA) != 0 &&
                fmt.BitCount == 16 && fmt.RMask == 0x00007C00 && fmt.GMask == 0x000003E0 &&
                fmt.BMask == 0x0000001F && fmt.AMask == 0x00008000)
            {
                return (TPCTextureFormat.RGBA, TEXDataLayout.A1R5G5B5);
            }
            if ((fmt.Flags & PIXELFLAG_RGB) != 0 && (fmt.Flags & PIXELFLAG_ALPHA) == 0 &&
                fmt.BitCount == 16 && fmt.RMask == 0x0000F800 && fmt.GMask == 0x000007E0 &&
                fmt.BMask == 0x0000001F)
            {
                return (TPCTextureFormat.RGB, TEXDataLayout.R5G6B5);
            }
            if ((fmt.Flags & PIXELFLAG_RGB) != 0 && (fmt.Flags & PIXELFLAG_ALPHA) != 0 &&
                fmt.BitCount == 16 && fmt.RMask == 0x00000F00 && fmt.GMask == 0x000000F0 &&
                fmt.BMask == 0x0000000F && fmt.AMask == 0x0000F000)
            {
                return (TPCTextureFormat.RGBA, TEXDataLayout.Argb4444);
            }
            if ((fmt.Flags & PIXELFLAG_INDEXED) != 0)
            {
                throw new ArgumentException("Palette-based TEX images are not supported.");
            }
            throw new ArgumentException(
                $"Unknown TEX pixel format: flags=0x{fmt.Flags:X} fourcc=0x{fmt.FourCC:X} " +
                $"bit_count={fmt.BitCount} masks=0x{fmt.RMask:X}/0x{fmt.GMask:X}/0x{fmt.BMask:X}/0x{fmt.AMask:X}");
        }

        private int FileMipmapSize(int width, int height, TPCTextureFormat tpcFormat, TEXDataLayout layout)
        {
            if (layout == TEXDataLayout.A1R5G5B5 || layout == TEXDataLayout.Argb4444 || layout == TEXDataLayout.R5G6B5)
            {
                return width * height * 2;
            }
            return tpcFormat.GetSize(width, height);
        }

        private byte[] ConvertToRgba(byte[] raw, int width, int height, TPCTextureFormat format, TEXDataLayout layout)
        {
            byte[] convertedData;
            TPCTextureFormat finalFormat = format;

            // Convert 16-bit formats to uncompressed
            if (layout == TEXDataLayout.Argb4444)
            {
                convertedData = Convert4444(raw);
                finalFormat = TPCTextureFormat.RGBA;
            }
            else if (layout == TEXDataLayout.A1R5G5B5)
            {
                convertedData = Convert1555(raw);
                finalFormat = TPCTextureFormat.RGBA;
            }
            else if (layout == TEXDataLayout.R5G6B5)
            {
                convertedData = Convert565(raw);
                finalFormat = TPCTextureFormat.RGB;
            }
            else
            {
                convertedData = raw;
            }

            // Convert to RGBA
            byte[] output = new byte[width * height * 4];
            ConvertFormatToRgba(convertedData, output, width, height, finalFormat);
            return output;
        }

        private void ConvertFormatToRgba(byte[] input, byte[] output, int width, int height, TPCTextureFormat format)
        {
            switch (format)
            {
                case TPCTextureFormat.RGBA:
                    Array.Copy(input, output, Math.Min(input.Length, output.Length));
                    break;

                case TPCTextureFormat.BGRA:
                    ConvertBgraToRgba(input, output, width, height);
                    break;

                case TPCTextureFormat.RGB:
                    ConvertRgbToRgba(input, output, width, height);
                    break;

                case TPCTextureFormat.BGR:
                    ConvertBgrToRgba(input, output, width, height);
                    break;

                case TPCTextureFormat.DXT1:
                    DecompressDxt1(input, output, width, height);
                    break;

                case TPCTextureFormat.DXT3:
                    DecompressDxt3(input, output, width, height);
                    break;

                case TPCTextureFormat.DXT5:
                    DecompressDxt5(input, output, width, height);
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
        }

        private static int ScaleBits(int value, int bits)
        {
            if (bits <= 0)
            {
                return 0;
            }
            int maxIn = (1 << bits) - 1;
            return (value * 255) / maxIn;
        }

        private static byte[] Convert4444(byte[] raw)
        {
            byte[] rgba = new byte[raw.Length * 2];
            for (int i = 0; i < raw.Length; i += 2)
            {
                ushort pixel = BitConverter.ToUInt16(raw, i);
                int r = ScaleBits((pixel >> 8) & 0x0F, 4);
                int g = ScaleBits((pixel >> 4) & 0x0F, 4);
                int b = ScaleBits(pixel & 0x0F, 4);
                int a = ScaleBits((pixel >> 12) & 0x0F, 4);
                int idx = (i / 2) * 4;
                rgba[idx] = (byte)r;
                rgba[idx + 1] = (byte)g;
                rgba[idx + 2] = (byte)b;
                rgba[idx + 3] = (byte)a;
            }
            return rgba;
        }

        private static byte[] Convert1555(byte[] raw)
        {
            byte[] rgba = new byte[raw.Length * 2];
            for (int i = 0; i < raw.Length; i += 2)
            {
                ushort pixel = BitConverter.ToUInt16(raw, i);
                byte a = (byte)((pixel & 0x8000) != 0 ? 0xFF : 0x00);
                int r = ScaleBits((pixel >> 10) & 0x1F, 5);
                int g = ScaleBits((pixel >> 5) & 0x1F, 5);
                int b = ScaleBits(pixel & 0x1F, 5);
                int idx = (i / 2) * 4;
                rgba[idx] = (byte)r;
                rgba[idx + 1] = (byte)g;
                rgba[idx + 2] = (byte)b;
                rgba[idx + 3] = a;
            }
            return rgba;
        }

        private static byte[] Convert565(byte[] raw)
        {
            byte[] rgb = new byte[(raw.Length / 2) * 3];
            for (int i = 0; i < raw.Length; i += 2)
            {
                ushort pixel = BitConverter.ToUInt16(raw, i);
                int r = ScaleBits((pixel >> 11) & 0x1F, 5);
                int g = ScaleBits((pixel >> 5) & 0x3F, 6);
                int b = ScaleBits(pixel & 0x1F, 5);
                int idx = (i / 2) * 3;
                rgb[idx] = (byte)r;
                rgb[idx + 1] = (byte)g;
                rgb[idx + 2] = (byte)b;
            }
            return rgb;
        }

        private void ConvertBgraToRgba(byte[] input, byte[] output, int width, int height)
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

        private void ConvertRgbToRgba(byte[] input, byte[] output, int width, int height)
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

        private void ConvertBgrToRgba(byte[] input, byte[] output, int width, int height)
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

        #region DXT Decompression

        private void DecompressDxt1(byte[] input, byte[] output, int width, int height)
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

        private void DecompressDxt3(byte[] input, byte[] output, int width, int height)
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

                            int colorIdx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int alphaIdx = py * 4 + px;
                            int dstOffset = (y * width + x) * 4;

                            output[dstOffset] = colors[colorIdx * 4];
                            output[dstOffset + 1] = colors[colorIdx * 4 + 1];
                            output[dstOffset + 2] = colors[colorIdx * 4 + 2];
                            output[dstOffset + 3] = alphas[alphaIdx];
                        }
                    }
                }
            }
        }

        private void DecompressDxt5(byte[] input, byte[] output, int width, int height)
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

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

