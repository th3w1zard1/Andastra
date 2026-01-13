using System;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:199-238
    // Original: @dataclass class TPCMipmap
    public class TPCMipmap : IEquatable<TPCMipmap>
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public TPCTextureFormat TpcFormat { get; set; }
        public byte[] Data { get; set; }

        public TPCMipmap(int width, int height, TPCTextureFormat tpcFormat, byte[] data)
        {
            Width = width;
            Height = height;
            TpcFormat = tpcFormat;
            Data = data ?? Array.Empty<byte>();
        }

        public int Size
        {
            get { return Data.Length; }
        }

        public override bool Equals(object obj)
        {
            return obj is TPCMipmap other && Equals(other);
        }

        public bool Equals(TPCMipmap other)
        {
            if (other == null)
            {
                return false;
            }
            if (Width != other.Width || Height != other.Height || TpcFormat != other.TpcFormat)
            {
                return false;
            }
            if (Data.Length != other.Data.Length)
            {
                return false;
            }
            for (int i = 0; i < Data.Length; i++)
            {
                if (Data[i] != other.Data[i])
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Width, Height, TpcFormat, Data.Length);
        }
    }
}

