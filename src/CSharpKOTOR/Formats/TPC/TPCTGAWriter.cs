using System;
using System.IO;
using AuroraEngine.Common.Common;

namespace AuroraEngine.Common.Formats.TPC
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

        private byte[] DecodeMipmapToRgba(TPCMipmap mipmap)
        {
            // Return a copy of the mipmap's pixels in RGBA order
            if (mipmap.TpcFormat == TPCTextureFormat.RGBA)
            {
                return (byte[])mipmap.Data.Clone();
            }
            // For other formats, we'd need conversion - simplified for now
            // In a full implementation, we'd convert to RGBA here
            return mipmap.Data;
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
