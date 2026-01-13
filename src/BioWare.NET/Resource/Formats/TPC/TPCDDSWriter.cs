using System;
using System.IO;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_dds.py:339-475
    // Original: class TPCDDSWriter(ResourceWriter)
    public class TPCDDSWriter : IDisposable
    {
        private static readonly byte[] MAGIC = { (byte)'D', (byte)'D', (byte)'S', (byte)' ' };
        private const int HEADER_SIZE = 124;
        private const uint DDSD_CAPS = 0x1;
        private const uint DDSD_HEIGHT = 0x2;
        private const uint DDSD_WIDTH = 0x4;
        private const uint DDSD_PITCH = 0x8;
        private const uint DDSD_PIXELFORMAT = 0x1000;
        private const uint DDSD_MIPMAPCOUNT = 0x20000;
        private const uint DDSD_LINEARSIZE = 0x80000;
        private const uint DDSCAPS_TEXTURE = 0x1000;
        private const uint DDSCAPS_MIPMAP = 0x400000;
        private const uint DDSCAPS_COMPLEX = 0x8;
        private const uint DDSCAPS2_CUBEMAP = 0x00000200;
        private const uint DDSCAPS2_ALLFACES = 0x0000FC00;
        private const uint DDPF_ALPHAPIXELS = 0x1;
        private const uint DDPF_FOURCC = 0x4;
        private const uint DDPF_RGB = 0x40;

        private readonly TPC _tpc;
        private readonly RawBinaryWriter _writer;

        public TPCDDSWriter(TPC tpc, string filepath)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public TPCDDSWriter(TPC tpc, Stream target)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public TPCDDSWriter(TPC tpc)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToByteArray(null);
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                if (_tpc.Layers == null || _tpc.Layers.Count == 0 || _tpc.Layers[0].Mipmaps.Count == 0)
                {
                    throw new ArgumentException("TPC contains no mipmaps to write as DDS.");
                }

                TPCTextureFormat targetFormat = EnsureSupportedFormat();
                TPCLayer layer0 = _tpc.Layers[0];
                TPCMipmap baseMip = layer0.Mipmaps[0];
                int width = baseMip.Width;
                int height = baseMip.Height;
                int mipCount = layer0.Mipmaps.Count;
                int faceCount = _tpc.IsCubeMap ? _tpc.Layers.Count : 1;

                uint flags = DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT;
                int pitchOrLinear;
                if (targetFormat.IsDxt())
                {
                    flags |= DDSD_LINEARSIZE;
                    pitchOrLinear = targetFormat.GetSize(width, height);
                }
                else
                {
                    flags |= DDSD_PITCH;
                    pitchOrLinear = width * targetFormat.BytesPerPixel();
                }

                if (mipCount > 1)
                {
                    flags |= DDSD_MIPMAPCOUNT;
                }

                (uint pfFlags, uint fourcc, int bitcount, uint rmask, uint gmask, uint bmask, uint amask) =
                    PixelFormatFields(targetFormat);

                uint caps1 = DDSCAPS_TEXTURE;
                uint caps2 = 0;
                if (mipCount > 1)
                {
                    caps1 |= DDSCAPS_MIPMAP | DDSCAPS_COMPLEX;
                }
                if (_tpc.IsCubeMap)
                {
                    caps1 |= DDSCAPS_COMPLEX;
                    caps2 |= DDSCAPS2_CUBEMAP | DDSCAPS2_ALLFACES;
                }

                // Write header
                _writer.WriteBytes(MAGIC);
                _writer.WriteUInt32(HEADER_SIZE);
                _writer.WriteUInt32(flags);
                _writer.WriteUInt32((uint)height);
                _writer.WriteUInt32((uint)width);
                _writer.WriteUInt32((uint)pitchOrLinear);
                _writer.WriteUInt32(0); // depth
                _writer.WriteUInt32((uint)mipCount);
                _writer.WriteBytes(new byte[44]); // reserved
                _writer.WriteUInt32(32); // pixel format size
                _writer.WriteUInt32(pfFlags);
                _writer.WriteUInt32(fourcc, bigEndian: true);
                _writer.WriteUInt32((uint)bitcount);
                _writer.WriteUInt32(rmask);
                _writer.WriteUInt32(gmask);
                _writer.WriteUInt32(bmask);
                _writer.WriteUInt32(amask);
                _writer.WriteUInt32(caps1);
                _writer.WriteUInt32(caps2);
                _writer.WriteBytes(new byte[12]); // caps3, caps4, reserved

                // Write mipmaps
                for (int face = 0; face < faceCount; face++)
                {
                    TPCLayer layer = _tpc.IsCubeMap ? _tpc.Layers[face] : _tpc.Layers[0];
                    if (layer.Mipmaps.Count < mipCount)
                    {
                        throw new ArgumentException($"Layer {face} does not contain {mipCount} mipmaps required for DDS export.");
                    }
                    int mmWidth = width, mmHeight = height;
                    for (int mipIndex = 0; mipIndex < mipCount; mipIndex++)
                    {
                        TPCMipmap mipmap = layer.Mipmaps[mipIndex];
                        int expectedW = Math.Max(1, mmWidth);
                        int expectedH = Math.Max(1, mmHeight);
                        if (mipmap.Width != expectedW || mipmap.Height != expectedH)
                        {
                            throw new ArgumentException(
                                $"Mipmap {mipIndex} dimensions mismatch: expected {expectedW}x{expectedH}, " +
                                $"found {mipmap.Width}x{mipmap.Height}");
                        }
                        byte[] payload = ConvertMipmapPayload(mipmap, targetFormat);
                        _writer.WriteBytes(payload);
                        mmWidth >>= 1;
                        mmHeight >>= 1;
                    }
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

        private (uint, uint, int, uint, uint, uint, uint) PixelFormatFields(TPCTextureFormat fmt)
        {
            if (fmt == TPCTextureFormat.DXT1)
            {
                return (DDPF_FOURCC, 0x44585431, 0, 0, 0, 0, 0);
            }
            if (fmt == TPCTextureFormat.DXT3)
            {
                return (DDPF_FOURCC, 0x44585433, 0, 0, 0, 0, 0);
            }
            if (fmt == TPCTextureFormat.DXT5)
            {
                return (DDPF_FOURCC, 0x44585435, 0, 0, 0, 0, 0);
            }
            if (fmt == TPCTextureFormat.BGRA)
            {
                return (DDPF_RGB | DDPF_ALPHAPIXELS, 0, 32, 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000);
            }
            if (fmt == TPCTextureFormat.BGR)
            {
                return (DDPF_RGB, 0, 24, 0x00FF0000, 0x0000FF00, 0x000000FF, 0);
            }
            throw new ArgumentException($"DDS writer does not support format {fmt}");
        }

        private TPCTextureFormat EnsureSupportedFormat()
        {
            TPCTextureFormat fmt = _tpc.Format();
            if (fmt == TPCTextureFormat.DXT1 || fmt == TPCTextureFormat.DXT3 || fmt == TPCTextureFormat.DXT5 ||
                fmt == TPCTextureFormat.BGR || fmt == TPCTextureFormat.BGRA)
            {
                return fmt;
            }
            if (fmt == TPCTextureFormat.RGB)
            {
                return TPCTextureFormat.BGR;
            }
            if (fmt == TPCTextureFormat.RGBA)
            {
                return TPCTextureFormat.BGRA;
            }
            throw new ArgumentException($"Unsupported TPC format for DDS export: {fmt}");
        }

        private byte[] ConvertMipmapPayload(TPCMipmap mipmap, TPCTextureFormat targetFormat)
        {
            if (mipmap.TpcFormat == targetFormat)
            {
                return mipmap.Data;
            }
            // Basic format conversion - swap RGB/BGR channels
            if (mipmap.TpcFormat == TPCTextureFormat.RGBA && targetFormat == TPCTextureFormat.BGRA)
            {
                byte[] converted = new byte[mipmap.Data.Length];
                for (int i = 0; i < mipmap.Data.Length; i += 4)
                {
                    converted[i] = mipmap.Data[i + 2]; // B
                    converted[i + 1] = mipmap.Data[i + 1]; // G
                    converted[i + 2] = mipmap.Data[i]; // R
                    converted[i + 3] = mipmap.Data[i + 3]; // A
                }
                return converted;
            }
            if (mipmap.TpcFormat == TPCTextureFormat.RGB && targetFormat == TPCTextureFormat.BGR)
            {
                byte[] converted = new byte[mipmap.Data.Length];
                for (int i = 0; i < mipmap.Data.Length; i += 3)
                {
                    converted[i] = mipmap.Data[i + 2]; // B
                    converted[i + 1] = mipmap.Data[i + 1]; // G
                    converted[i + 2] = mipmap.Data[i]; // R
                }
                return converted;
            }
            // For unsupported conversions, return original data (may cause issues but allows basic functionality)
            return mipmap.Data;
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
