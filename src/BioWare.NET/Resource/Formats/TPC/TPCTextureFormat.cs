using System;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tpc_data.py:54-199
    // Original: class TPCTextureFormat(IntEnum)
    public enum TPCTextureFormat
    {
        Invalid = -1,
        Greyscale = 0,
        DXT1 = 1,
        DXT3 = 2,
        DXT5 = 3,
        RGB = 4,
        RGBA = 5,
        BGRA = 6,
        BGR = 7
    }

    public static class TPCTextureFormatExtensions
    {
        public static bool IsDxt(this TPCTextureFormat fmt)
        {
            return fmt == TPCTextureFormat.DXT1 ||
                   fmt == TPCTextureFormat.DXT3 ||
                   fmt == TPCTextureFormat.DXT5;
        }

        public static int BytesPerPixel(this TPCTextureFormat fmt)
        {
            if (fmt == TPCTextureFormat.Greyscale)
            {
                return 1;
            }
            if (fmt == TPCTextureFormat.DXT1 || fmt == TPCTextureFormat.DXT3 || fmt == TPCTextureFormat.DXT5)
            {
                return 1;
            }
            if (fmt == TPCTextureFormat.RGB || fmt == TPCTextureFormat.BGR)
            {
                return 3;
            }
            if (fmt == TPCTextureFormat.RGBA || fmt == TPCTextureFormat.BGRA)
            {
                return 4;
            }
            return 1;
        }

        public static int BytesPerBlock(this TPCTextureFormat fmt)
        {
            if (!fmt.IsDxt())
            {
                return 1;
            }
            return fmt == TPCTextureFormat.DXT1 ? 8 : 16;
        }

        public static int MinSize(this TPCTextureFormat fmt)
        {
            if (fmt == TPCTextureFormat.Greyscale)
            {
                return 1;
            }
            if (fmt == TPCTextureFormat.RGB || fmt == TPCTextureFormat.BGR)
            {
                return 3;
            }
            if (fmt == TPCTextureFormat.RGBA || fmt == TPCTextureFormat.BGRA)
            {
                return 4;
            }
            if (fmt.IsDxt())
            {
                return fmt.BytesPerBlock();
            }
            return 0;
        }

        public static int GetSize(this TPCTextureFormat fmt, int width, int height)
        {
            int size;
            if (fmt.IsDxt())
            {
                int bytesPerBlock = fmt.BytesPerBlock();
                size = ((width + 3) / 4) * ((height + 3) / 4) * bytesPerBlock;
            }
            else
            {
                size = width * height * fmt.BytesPerPixel();
            }
            return Math.Max(fmt.MinSize(), size);
        }
    }
}

