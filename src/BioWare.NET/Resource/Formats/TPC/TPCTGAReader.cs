using System;
using System.IO;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_tga.py:81-236
    // Original: class TPCTGAReader(ResourceReader)
    public class TPCTGAReader : IDisposable
    {
        private readonly BioWare.NET.Common.RawBinaryReader _reader;
        private TPC _tpc;

        public TPCTGAReader(byte[] data, int offset = 0, int? size = null)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromBytes(data, offset, size);
            _tpc = null;
        }

        public TPCTGAReader(string filepath, int offset = 0, int? size = null)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromFile(filepath, offset, size);
            _tpc = null;
        }

        public TPCTGAReader(Stream source, int offset = 0, int? size = null)
        {
            _reader = BioWare.NET.Common.RawBinaryReader.FromStream(source, offset, size);
            _tpc = null;
        }

        public TPC Load(bool autoClose = true)
        {
            try
            {
                _tpc = new TPC();
                byte[] raw = _reader.ReadAll();
                using (var ms = new MemoryStream(raw))
                {
                    TGAImage image = TGA.ReadTga(ms);

                    int width = image.Width;
                    int height = image.Height;
                    byte[] rgba = image.Data;
                    int faceCount = 1;
                    int faceHeight = height;

                    if (height > 0 && width > 0 && height % width == 0 && height / width == 6)
                    {
                        faceCount = 6;
                        faceHeight = height / 6;
                        _tpc.IsCubeMap = true;
                    }
                    else
                    {
                        _tpc.IsCubeMap = false;
                    }

                    _tpc.Layers = new System.Collections.Generic.List<TPCLayer>();
                    _tpc.IsAnimated = false;

                    bool hasAlpha = HasAlphaChannel(rgba);

                    for (int face = 0; face < faceCount; face++)
                    {
                        TPCLayer layer = new TPCLayer();
                        byte[] sliceRgba = new byte[faceHeight * width * 4];
                        for (int row = 0; row < faceHeight; row++)
                        {
                            int srcOffset = ((face * faceHeight) + row) * width * 4;
                            int dstOffset = row * width * 4;
                            Array.Copy(rgba, srcOffset, sliceRgba, dstOffset, width * 4);
                        }
                        layer.SetSingle(width, faceHeight, sliceRgba, TPCTextureFormat.RGBA);
                        _tpc.Layers.Add(layer);
                    }

                    _tpc._format = TPCTextureFormat.RGBA;
                    if (!hasAlpha)
                    {
                        _tpc.Convert(TPCTextureFormat.RGB);
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

        private static bool HasAlphaChannel(byte[] pixels)
        {
            if (pixels.Length < 4)
            {
                return false;
            }

            // Check alpha channel (every 4th byte starting at index 3)
            // Early exit on first transparent pixel
            for (int i = 3; i < pixels.Length; i += 4)
            {
                if (pixels[i] != 0xFF)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
