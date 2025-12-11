using System;
using System.IO;

namespace CSharpKOTOR.Common.LZMA
{
    /// <summary>
    /// Minimal helper for raw LZMA1 compression/decompression (no headers) matching PyKotor bzf.py (lzma.FORMAT_RAW, FILTER_LZMA1).
    /// Uses SevenZip.Compression.LZMA encoder/decoder with fixed properties (lc=3, lp=0, pb=2, dict=8MB).
    /// </summary>
    internal static class LzmaHelper
    {
        // LZMA properties for raw LZMA1 format.
        private static readonly byte[] LzmaProperties = { 0x5D, 0x00, 0x00, 0x80, 0x00 }; // lc=3, lp=0, pb=2, dict=8MB

        public static byte[] Decompress(byte[] compressedData, int uncompressedSize)
        {
            using (var compressedStream = new MemoryStream(compressedData))
            using (var lzmaStream = new SevenZip.Compression.LZMA.LzmaStream(LzmaProperties, compressedStream, uncompressedSize))
            using (var decompressedStream = new MemoryStream())
            {
                lzmaStream.CopyTo(decompressedStream);
                byte[] decompressed = decompressedStream.ToArray();

                if (decompressed.Length != uncompressedSize)
                {
                    throw new InvalidDataException($"LZMA decompression resulted in unexpected size. Expected {uncompressedSize}, got {decompressed.Length}.");
                }
                return decompressed;
            }
        }

        public static byte[] Compress(byte[] uncompressedData)
        {
            using (var uncompressedStream = new MemoryStream(uncompressedData))
            using (var compressedStream = new MemoryStream())
            {
                var encoder = new SevenZip.Compression.LZMA.Encoder();
                encoder.SetLzmaProperties(LzmaProperties);
                encoder.WriteCoderProperties(compressedStream);
                encoder.Code(uncompressedStream, compressedStream, -1, -1, null);
                return compressedStream.ToArray();
            }
        }
    }
}

