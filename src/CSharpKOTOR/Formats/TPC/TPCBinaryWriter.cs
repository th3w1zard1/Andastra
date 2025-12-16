using System;
using System.IO;
using System.Text;
using BioWareCSharp.Common;
using BioWareCSharp.Common.Resources;

namespace BioWareCSharp.Common.Formats.TPC
{
    // Simplified writer matching core of PyKotor io_tpc.py:289-448
    public class TPCBinaryWriter : IDisposable
    {
        private readonly TPC _tpc;
        private readonly RawBinaryWriter _writer;
        private readonly int _layerCount;
        private readonly int _mipmapCount;

        public TPCBinaryWriter(TPC tpc, string filepath)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToFile(filepath);
            _layerCount = tpc.Layers?.Count ?? 0;
            _mipmapCount = _layerCount > 0 && tpc.Layers[0].Mipmaps.Count > 0 ? tpc.Layers[0].Mipmaps.Count : 0;
        }

        public TPCBinaryWriter(TPC tpc, Stream target)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToStream(target);
            _layerCount = tpc.Layers?.Count ?? 0;
            _mipmapCount = _layerCount > 0 && tpc.Layers[0].Mipmaps.Count > 0 ? tpc.Layers[0].Mipmaps.Count : 0;
        }

        public TPCBinaryWriter(TPC tpc)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToByteArray();
            _layerCount = tpc.Layers?.Count ?? 0;
            _mipmapCount = _layerCount > 0 && tpc.Layers[0].Mipmaps.Count > 0 ? tpc.Layers[0].Mipmaps.Count : 0;
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

                if (frameWidth <= 0 || frameHeight <= 0)
                {
                    throw new ArgumentException($"Invalid dimensions: {frameWidth}x{frameHeight}");
                }

                int pixelEncoding = GetPixelEncoding(format);
                int dataSize = 0;
                if (format.IsDxt() && _tpc.Layers.Count > 0 && _tpc.Layers[0].Mipmaps.Count > 0)
                {
                    dataSize = _tpc.Layers[0].Mipmaps[0].Data.Length;
                }

                _writer.WriteUInt32((uint)dataSize);
                _writer.WriteSingle(_tpc.AlphaTest);
                _writer.WriteUInt16((ushort)frameWidth);
                _writer.WriteUInt16((ushort)frameHeight);
                _writer.WriteUInt8((byte)pixelEncoding);
                _writer.WriteUInt8((byte)_mipmapCount);
                _writer.WriteBytes(new byte[0x72]);

                foreach (var layer in _tpc.Layers)
                {
                    for (int mip = 0; mip < _mipmapCount && mip < layer.Mipmaps.Count; mip++)
                    {
                        var mm = layer.Mipmaps[mip];
                        _writer.WriteBytes(mm.Data);
                    }
                }

                if (!string.IsNullOrWhiteSpace(_tpc.Txi))
                {
                    string normalized = _tpc.Txi.Replace("\r\n", "\n").Replace("\r", "\n");
                    if (!normalized.EndsWith("\n"))
                    {
                        normalized += "\n";
                    }
                    normalized = normalized.Replace("\n", "\r\n");
                    _writer.WriteBytes(Encoding.ASCII.GetBytes(normalized));
                    _writer.WriteUInt8(0);
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

        private int GetPixelEncoding(TPCTextureFormat format)
        {
            if (format == TPCTextureFormat.Greyscale) return 0x01;
            if (format == TPCTextureFormat.RGB || format == TPCTextureFormat.DXT1) return 0x02;
            if (format == TPCTextureFormat.RGBA || format == TPCTextureFormat.DXT5) return 0x04;
            if (format == TPCTextureFormat.BGRA) return 0x0C;
            throw new ArgumentException("Invalid TPC texture format: " + format);
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}

