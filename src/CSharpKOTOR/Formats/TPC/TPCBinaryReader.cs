using System;
using System.IO;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.TXI;

namespace CSharpKOTOR.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tpc.py:85-270
    // Simplified: reads core header, mipmaps, and TXI text without conversions
    public class TPCBinaryReader : IDisposable
    {
        private readonly RawBinaryReader _reader;
        private TPC _tpc;
        private int _layerCount;
        private int _mipmapCount;

        public TPCBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromBytes(data, offset, size > 0 ? size : (int?)null);
        }

        public TPCBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromFile(filepath, offset, size > 0 ? size : (int?)null);
        }

        public TPCBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            _reader = RawBinaryReader.FromStream(source, offset, size > 0 ? size : (int?)null);
        }

        public TPC Load(bool autoClose = true)
        {
            try
            {
                _tpc = new TPC();
                _layerCount = 1;
                _mipmapCount = 0;

                int dataSize = (int)_reader.ReadUInt32();
                bool compressed = dataSize != 0;
                _tpc.AlphaTest = _reader.ReadSingle();
                int width = _reader.ReadUInt16();
                int height = _reader.ReadUInt16();

                byte pixelType = _reader.ReadUInt8();
                _mipmapCount = _reader.ReadUInt8();
                TPCTextureFormat format = TPCTextureFormat.Invalid;
                if (compressed)
                {
                    if (pixelType == 2)
                    {
                        format = TPCTextureFormat.DXT1;
                    }
                    else if (pixelType == 4)
                    {
                        format = TPCTextureFormat.DXT5;
                    }
                }
                else
                {
                    if (pixelType == 1)
                    {
                        format = TPCTextureFormat.Greyscale;
                    }
                    else if (pixelType == 2)
                    {
                        format = TPCTextureFormat.RGB;
                    }
                    else if (pixelType == 4)
                    {
                        format = TPCTextureFormat.RGBA;
                    }
                    else if (pixelType == 12)
                    {
                        format = TPCTextureFormat.BGRA;
                    }
                }
                if (format == TPCTextureFormat.Invalid)
                {
                    throw new ArgumentException("Unsupported texture format");
                }
                _tpc._format = format;

                const int totalCubeSides = 6;
                if (!compressed)
                {
                    dataSize = format.GetSize(width, height);
                }
                else if (height != 0 && width != 0 && (height / width) == totalCubeSides)
                {
                    _tpc.IsCubeMap = true;
                    height = height / totalCubeSides;
                    _layerCount = totalCubeSides;
                }

                int completeDataSize = dataSize;
                for (int level = 1; level < _mipmapCount; level++)
                {
                    int reducedWidth = Math.Max(width >> level, 1);
                    int reducedHeight = Math.Max(height >> level, 1);
                    completeDataSize += format.GetSize(reducedWidth, reducedHeight);
                }
                completeDataSize *= _layerCount;

                _reader.Skip(0x72 + completeDataSize);
                int txiSize = _reader.Size - _reader.Position;
                if (txiSize > 0)
                {
                    _tpc.Txi = _reader.ReadString(txiSize, System.Text.Encoding.ASCII.WebName);
                    _tpc.TxiObject = new TXI.TXI(_tpc.Txi);
                }

                _reader.Seek(0x80);
                for (int layerIndex = 0; layerIndex < _layerCount; layerIndex++)
                {
                    TPCLayer layer = new TPCLayer();
                    _tpc.Layers.Add(layer);
                    int layerWidth = width;
                    int layerHeight = height;
                    int layerSize = format.GetSize(layerWidth, layerHeight);

                    for (int mip = 0; mip < _mipmapCount; mip++)
                    {
                        int mmWidth = Math.Max(1, layerWidth);
                        int mmHeight = Math.Max(1, layerHeight);
                        int mmSize = Math.Max(layerSize, format.MinSize());
                        byte[] data = _reader.ReadBytes(mmSize);
                        layer.Mipmaps.Add(new TPCMipmap(mmWidth, mmHeight, format, data));

                        if (_reader.Remaining <= 0)
                        {
                            break;
                        }

                        layerWidth >>= 1;
                        layerHeight >>= 1;
                        layerSize = format.GetSize(Math.Max(1, layerWidth), Math.Max(1, layerHeight));
                        if (layerWidth < 1 && layerHeight < 1)
                        {
                            break;
                        }
                    }
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

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}

