using System;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;

namespace Andastra.Parsing.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_dds.py:44-337
    // Original: class TPCDDSReader(ResourceReader)
    public class TPCDDSReader : IDisposable
    {
        private const uint MAGIC = 0x44445320; // "DDS "
        private const int HEADER_SIZE = 124;
        private const uint HEADER_FLAGS_MIPS = 0x00020000;
        private const uint PIXELFLAG_ALPHA = 0x00000001;
        private const uint PIXELFLAG_FOURCC = 0x00000004;
        private const uint PIXELFLAG_INDEXED = 0x00000020;
        private const uint PIXELFLAG_RGB = 0x00000040;
        private const uint CAP_CUBEMAP = 0x00000200;
        private const uint CAP_CUBEMAP_ALLFACES = 0x0000FC00;
        private const uint FOURCC_DXT1 = 0x44585431; // "DXT1"
        private const uint FOURCC_DXT3 = 0x44585433; // "DXT3"
        private const uint FOURCC_DXT5 = 0x44585435; // "DXT5"
        private const int MAX_DIMENSION = 0x8000;

        private readonly Andastra.Parsing.Common.RawBinaryReader _reader;
        private TPC _tpc;

        private enum DDSDataLayout
        {
            Direct,
            Argb4444,
            A1R5G5B5,
            R5G6B5
        }

        private struct DDSPixelFormat
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

        public TPCDDSReader(byte[] data, int offset = 0, int? size = null)
        {
            _reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(data, offset, size);
            _tpc = new TPC();
        }

        public TPCDDSReader(string filepath, int offset = 0, int? size = null)
        {
            _reader = Andastra.Parsing.Common.RawBinaryReader.FromFile(filepath, offset, size);
            _tpc = new TPC();
        }

        public TPCDDSReader(Stream source, int offset = 0, int? size = null)
        {
            _reader = Andastra.Parsing.Common.RawBinaryReader.FromStream(source, offset, size);
            _tpc = new TPC();
        }

        public TPC Load(bool autoClose = true)
        {
            try
            {
                _tpc = new TPC();
                int startPos = _reader.Position;
                uint magic = _reader.ReadUInt32(bigEndian: true);
                if (magic == MAGIC)
                {
                    ReadStandardHeader();
                }
                else
                {
                    _reader.Seek(startPos);
                    ReadBiowareHeader();
                }
                return _tpc;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
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

        private (TPCTextureFormat, DDSDataLayout) DetectFormat(DDSPixelFormat fmt)
        {
            DDSDataLayout dataLayout = DDSDataLayout.Direct;

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
                return (TPCTextureFormat.RGBA, DDSDataLayout.A1R5G5B5);
            }
            if ((fmt.Flags & PIXELFLAG_RGB) != 0 && (fmt.Flags & PIXELFLAG_ALPHA) == 0 &&
                fmt.BitCount == 16 && fmt.RMask == 0x0000F800 && fmt.GMask == 0x000007E0 &&
                fmt.BMask == 0x0000001F)
            {
                return (TPCTextureFormat.RGB, DDSDataLayout.R5G6B5);
            }
            if ((fmt.Flags & PIXELFLAG_RGB) != 0 && (fmt.Flags & PIXELFLAG_ALPHA) != 0 &&
                fmt.BitCount == 16 && fmt.RMask == 0x00000F00 && fmt.GMask == 0x000000F0 &&
                fmt.BMask == 0x0000000F && fmt.AMask == 0x0000F000)
            {
                return (TPCTextureFormat.RGBA, DDSDataLayout.Argb4444);
            }
            if ((fmt.Flags & PIXELFLAG_INDEXED) != 0)
            {
                throw new ArgumentException("Palette-based DDS images are not supported.");
            }
            throw new ArgumentException(
                $"Unknown DDS pixel format: flags=0x{fmt.Flags:X} fourcc=0x{fmt.FourCC:X} " +
                $"bit_count={fmt.BitCount} masks=0x{fmt.RMask:X}/0x{fmt.GMask:X}/0x{fmt.BMask:X}/0x{fmt.AMask:X}");
        }

        private void ReadStandardHeader()
        {
            int headerSize = (int)_reader.ReadUInt32();
            if (headerSize != HEADER_SIZE)
            {
                throw new ArgumentException($"Invalid DDS header size: {headerSize}");
            }

            uint flags = _reader.ReadUInt32();
            int height = (int)_reader.ReadUInt32();
            int width = (int)_reader.ReadUInt32();
            if (width >= MAX_DIMENSION || height >= MAX_DIMENSION)
            {
                throw new ArgumentException($"Unsupported image dimensions ({width}x{height})");
            }

            _reader.Skip(8); // pitch + depth
            int mipCount = (int)_reader.ReadUInt32();
            if ((flags & HEADER_FLAGS_MIPS) == 0)
            {
                mipCount = 1;
            }

            _reader.Skip(44);
            DDSPixelFormat fmt = new DDSPixelFormat
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
            (TPCTextureFormat tpcFormat, DDSDataLayout dataLayout) = DetectFormat(fmt);

            _reader.ReadUInt32(); // caps1
            uint caps2 = _reader.ReadUInt32();
            _reader.Skip(8); // caps3 + caps4
            _reader.Skip(4); // reserved2

            int faceCount = 1;
            if ((caps2 & CAP_CUBEMAP) != 0)
            {
                uint faceBits = caps2 & CAP_CUBEMAP_ALLFACES;
                faceCount = CountBits(faceBits);
                if (faceCount == 0)
                {
                    faceCount = 6;
                }
                _tpc.IsCubeMap = true;
            }

            _tpc.IsAnimated = false;
            _tpc._format = tpcFormat;
            ReadSurfaces(width, height, mipCount, faceCount, tpcFormat, dataLayout);
        }

        private void ReadBiowareHeader()
        {
            int width = (int)_reader.ReadUInt32();
            int height = (int)_reader.ReadUInt32();
            if (width >= MAX_DIMENSION || height >= MAX_DIMENSION)
            {
                throw new ArgumentException($"Unsupported image dimensions ({width}x{height})");
            }
            if (width == 0 || height == 0 || (width & (width - 1)) != 0 || (height & (height - 1)) != 0)
            {
                throw new ArgumentException("BioWare DDS requires power-of-two dimensions.");
            }

            int bpp = (int)_reader.ReadUInt32();
            TPCTextureFormat tpcFormat;
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
                throw new ArgumentException($"Unsupported BioWare DDS bytes-per-pixel value: {bpp}");
            }

            int dataSize = (int)_reader.ReadUInt32();
            int expected = (bpp == 3) ? (width * height) / 2 : width * height;
            if (dataSize != expected)
            {
                throw new ArgumentException($"BioWare DDS data size mismatch: {dataSize} != {expected}");
            }

            _reader.Skip(4); // Unknown float
            int fullDataSize = _reader.Size - _reader.Position;

            int mipCount = 0;
            int tmpWidth = width, tmpHeight = height;
            while (tmpWidth >= 1 && tmpHeight >= 1)
            {
                int sizeNeeded = tpcFormat.GetSize(tmpWidth, tmpHeight);
                if (fullDataSize < sizeNeeded)
                {
                    break;
                }
                fullDataSize -= sizeNeeded;
                mipCount++;
                tmpWidth >>= 1;
                tmpHeight >>= 1;
            }

            mipCount = Math.Max(1, mipCount);
            _tpc.IsCubeMap = false;
            _tpc.IsAnimated = false;
            _tpc._format = tpcFormat;
            ReadSurfaces(width, height, mipCount, 1, tpcFormat, DDSDataLayout.Direct);
        }

        private int FileMipmapSize(int width, int height, TPCTextureFormat tpcFormat, DDSDataLayout layout)
        {
            if (layout == DDSDataLayout.A1R5G5B5 || layout == DDSDataLayout.Argb4444 || layout == DDSDataLayout.R5G6B5)
            {
                return width * height * 2;
            }
            return tpcFormat.GetSize(width, height);
        }

        private byte[] ConvertData(byte[] raw, int width, int height, DDSDataLayout layout)
        {
            if (layout == DDSDataLayout.Direct)
            {
                return raw;
            }
            if (layout == DDSDataLayout.Argb4444)
            {
                return Convert4444(raw);
            }
            if (layout == DDSDataLayout.A1R5G5B5)
            {
                return Convert1555(raw);
            }
            if (layout == DDSDataLayout.R5G6B5)
            {
                return Convert565(raw);
            }
            throw new ArgumentException($"Unsupported DDS data layout: {layout}");
        }

        private void ReadSurfaces(int width, int height, int mipCount, int faceCount, TPCTextureFormat tpcFormat, DDSDataLayout layout)
        {
            faceCount = Math.Max(1, faceCount);
            for (int face = 0; face < faceCount; face++)
            {
                TPCLayer layer = new TPCLayer();
                _tpc.Layers.Add(layer);
                int mmWidth = width, mmHeight = height;
                for (int mip = 0; mip < mipCount; mip++)
                {
                    mmWidth = Math.Max(1, mmWidth);
                    mmHeight = Math.Max(1, mmHeight);
                    int fileSize = FileMipmapSize(mmWidth, mmHeight, tpcFormat, layout);
                    byte[] raw = _reader.ReadBytes(fileSize);
                    if (raw.Length != fileSize)
                    {
                        throw new ArgumentException("DDS truncated while reading mip data.");
                    }
                    byte[] data = ConvertData(raw, mmWidth, mmHeight, layout);
                    TPCTextureFormat finalFormat = (layout == DDSDataLayout.Direct) ? tpcFormat :
                        (layout == DDSDataLayout.R5G6B5 ? TPCTextureFormat.RGB : TPCTextureFormat.RGBA);
                    layer.Mipmaps.Add(new TPCMipmap(mmWidth, mmHeight, finalFormat, data));
                    mmWidth >>= 1;
                    mmHeight >>= 1;
                }
            }
        }

        private static int CountBits(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= (value - 1);
            }
            return count;
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
