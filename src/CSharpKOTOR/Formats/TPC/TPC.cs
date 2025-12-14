using System;
using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Formats.TXI;

namespace CSharpKOTOR.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:317-529
    // Simplified: core fields and equality for texture container
    public class TPC : IEquatable<TPC>
    {
        public float AlphaTest { get; set; }
        public bool IsCubeMap { get; set; }
        public bool IsAnimated { get; set; }
        public string Txi { get; set; }
        public TXI.TXI TxiObject { get; set; }
        public List<TPCLayer> Layers { get; set; }
        internal TPCTextureFormat _format;

        public TPC()
        {
            AlphaTest = 0.0f;
            IsCubeMap = false;
            IsAnimated = false;
            Txi = string.Empty;
            TxiObject = new TXI.TXI();
            Layers = new List<TPCLayer>();
            _format = TPCTextureFormat.Invalid;
        }

        public TPCTextureFormat Format()
        {
            return _format;
        }

        public (int width, int height) Dimensions()
        {
            if (Layers.Count == 0 || Layers[0].Mipmaps.Count == 0)
            {
                return (0, 0);
            }
            return (Layers[0].Mipmaps[0].Width, Layers[0].Mipmaps[0].Height);
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

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:594-640
        // Original: def convert(self, target: TPCTextureFormat) -> None
        // Simplified: basic format conversion support
        public void Convert(TPCTextureFormat target)
        {
            if (_format == target)
            {
                return;
            }

            // Basic conversion: RGBA <-> RGB based on alpha channel
            if (target == TPCTextureFormat.RGB && _format == TPCTextureFormat.RGBA)
            {
                foreach (var layer in Layers)
                {
                    foreach (var mipmap in layer.Mipmaps)
                    {
                        if (mipmap.TpcFormat == TPCTextureFormat.RGBA)
                        {
                            byte[] rgbData = new byte[mipmap.Width * mipmap.Height * 3];
                            for (int i = 0; i < mipmap.Width * mipmap.Height; i++)
                            {
                                rgbData[i * 3] = mipmap.Data[i * 4];
                                rgbData[i * 3 + 1] = mipmap.Data[i * 4 + 1];
                                rgbData[i * 3 + 2] = mipmap.Data[i * 4 + 2];
                            }
                            mipmap.Data = rgbData;
                            mipmap.TpcFormat = TPCTextureFormat.RGB;
                        }
                    }
                }
                _format = TPCTextureFormat.RGB;
            }
            // More conversions can be added as needed
        }
    }
}

