using System;
using System.Collections.Generic;

namespace BioWareCSharp.Common.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:241-315
    // Original: class TPCLayer
    public class TPCLayer : IEquatable<TPCLayer>
    {
        public List<TPCMipmap> Mipmaps { get; set; }

        public TPCLayer()
        {
            Mipmaps = new List<TPCMipmap>();
        }

        public override bool Equals(object obj)
        {
            return obj is TPCLayer other && Equals(other);
        }

        public bool Equals(TPCLayer other)
        {
            if (other == null)
            {
                return false;
            }
            if (Mipmaps.Count != other.Mipmaps.Count)
            {
                return false;
            }
            for (int i = 0; i < Mipmaps.Count; i++)
            {
                if (!Mipmaps[i].Equals(other.Mipmaps[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var mm in Mipmaps)
            {
                hash.Add(mm);
            }
            return hash.ToHashCode();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:392-427
        // Original: def set_single(self, width: int, height: int, data: bytes | bytearray, tpc_format: TPCTextureFormat)
        public void SetSingle(int width, int height, byte[] data, TPCTextureFormat tpcFormat)
        {
            Mipmaps.Clear();
            int mmWidth = width, mmHeight = height;
            byte[] currentData = data;

            while (mmWidth > 0 && mmHeight > 0)
            {
                int w = Math.Max(1, mmWidth);
                int h = Math.Max(1, mmHeight);
                var mm = new TPCMipmap(w, h, tpcFormat, currentData);
                Mipmaps.Add(mm);

                mmWidth >>= 1;
                mmHeight >>= 1;

                if (w > 1 && h > 1 && mmWidth >= 1 && mmHeight >= 1)
                {
                    // Simplified: just use the same data for now (downsampling would be complex)
                    // In a full implementation, we'd downsample here
                    currentData = new byte[mmWidth * mmHeight * tpcFormat.BytesPerPixel()];
                }
                else
                {
                    break;
                }
            }
        }
    }
}

