using System;
using System.IO;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/io_bmp.py:16-78
    // Complete BMP writer implementation with standard Windows Bitmap format support
    // BMP format specification: Standard Windows Bitmap (24-bit RGB, bottom-to-top pixel order)
    // swkotor.exe/swkotor2.exe: BMP format support for texture export (standard format, no vendor-specific implementation needed)
    public class TPCBMPWriter : IDisposable
    {
        private readonly TPC _tpc;
        private readonly RawBinaryWriter _writer;

        // BMP format constants
        private const int BMP_FILE_HEADER_SIZE = 14;
        private const int BMP_INFO_HEADER_SIZE = 40;
        private const int BMP_HEADER_SIZE = BMP_FILE_HEADER_SIZE + BMP_INFO_HEADER_SIZE; // 54 bytes
        private const ushort BMP_BITS_PER_PIXEL = 24; // 24-bit RGB
        private const ushort BMP_PLANES = 1;
        private const uint BMP_COMPRESSION_BI_RGB = 0; // No compression

        public TPCBMPWriter(TPC tpc, string filepath)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public TPCBMPWriter(TPC tpc, Stream target)
        {
            _tpc = tpc ?? throw new ArgumentNullException(nameof(tpc));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public TPCBMPWriter(TPC tpc)
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
                    throw new ArgumentException("TPC contains no mipmaps to write as BMP.");
                }

                // Convert TPC to RGB format (BMP only supports 24-bit RGB, no alpha)
                // Matching PyKotor: self._tpc.convert(TPCTextureFormat.RGB)
                _tpc.Convert(TPCTextureFormat.RGB);

                // Get first mipmap (layer 0, mipmap 0)
                // Matching PyKotor: mm: TPCMipmap = self._tpc.get(0, 0)
                TPCMipmap mipmap = _tpc.Get(0, 0);
                int width = mipmap.Width;
                int height = mipmap.Height;
                byte[] rgbData = mipmap.Data;

                // Validate RGB data size
                int expectedRgbSize = width * height * 3;
                if (rgbData.Length < expectedRgbSize)
                {
                    throw new ArgumentException($"RGB data size mismatch: expected {expectedRgbSize} bytes, got {rgbData.Length}");
                }

                // Calculate row size with padding (BMP rows must be aligned to 4-byte boundary)
                int rowSize = width * 3; // 3 bytes per pixel (RGB)
                int rowPadding = (4 - (rowSize % 4)) % 4; // Padding to align to 4 bytes
                int paddedRowSize = rowSize + rowPadding;

                // Calculate file size: header (54 bytes) + pixel data
                uint fileSize = (uint)(BMP_HEADER_SIZE + paddedRowSize * height);

                // Write BMP File Header (14 bytes)
                // Matching PyKotor: self._writer.write_string("BM")
                _writer.WriteString("BM", encoding: "ascii");
                // Matching PyKotor: self._writer.write_uint32(file_size)
                _writer.WriteUInt32(fileSize);
                // Matching PyKotor: self._writer.write_uint32(0) - reserved
                _writer.WriteUInt32(0);
                // Matching PyKotor: self._writer.write_uint32(54) - data offset
                _writer.WriteUInt32(BMP_HEADER_SIZE);

                // Write BMP Info Header (40 bytes)
                // Matching PyKotor: self._writer.write_uint32(40) - header size
                _writer.WriteUInt32(BMP_INFO_HEADER_SIZE);
                // Matching PyKotor: self._writer.write_uint32(mm.width) - width
                _writer.WriteUInt32((uint)width);
                // Matching PyKotor: self._writer.write_uint32(mm.height) - height (positive = bottom-up)
                _writer.WriteUInt32((uint)height);
                // Matching PyKotor: self._writer.write_uint16(1) - planes
                _writer.WriteUInt16(BMP_PLANES);
                // Matching PyKotor: self._writer.write_uint16(24) - bits per pixel
                _writer.WriteUInt16(BMP_BITS_PER_PIXEL);
                // Matching PyKotor: self._writer.write_uint32(0) - compression (BI_RGB = 0)
                _writer.WriteUInt32(BMP_COMPRESSION_BI_RGB);
                // Matching PyKotor: self._writer.write_uint32(0) - image size (0 for uncompressed)
                _writer.WriteUInt32(0);
                // Matching PyKotor: self._writer.write_uint32(1) - X pixels per meter
                _writer.WriteUInt32(1);
                // Matching PyKotor: self._writer.write_uint32(1) - Y pixels per meter
                _writer.WriteUInt32(1);
                // Matching PyKotor: self._writer.write_uint32(0) - colors used
                _writer.WriteUInt32(0);
                // Matching PyKotor: self._writer.write_uint32(0) - important colors
                _writer.WriteUInt32(0);

                // Write pixel data
                // BMP format stores pixels bottom-to-top, so we need to reverse the row order
                // Also convert RGB to BGR (BMP uses BGR byte order)
                // Matching PyKotor pixel conversion logic:
                // pixel_reader: BinaryReader = BinaryReader.from_bytes(mm.data)
                // temp_pixels: list[list[int]] = []
                // for _ in range(len(mm.data) // 3):
                //     r = pixel_reader.read_uint8()
                //     g = pixel_reader.read_uint8()
                //     b = pixel_reader.read_uint8()
                //     temp_pixels.append([b, g, r])
                // for i in range(len(temp_pixels)):
                //     x = i % mm.width
                //     y = mm.height - (i // mm.width) - 1
                //     index = x + mm.width * y
                //     self._writer.write_bytes(struct.pack("BBB", *temp_pixels[index]))

                // Convert RGB to BGR and flip vertically (bottom-to-top)
                for (int y = height - 1; y >= 0; y--)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int srcIndex = (y * width + x) * 3;
                        if (srcIndex + 2 < rgbData.Length)
                        {
                            byte r = rgbData[srcIndex];
                            byte g = rgbData[srcIndex + 1];
                            byte b = rgbData[srcIndex + 2];

                            // Write BGR (BMP byte order)
                            _writer.WriteUInt8(b);
                            _writer.WriteUInt8(g);
                            _writer.WriteUInt8(r);
                        }
                    }

                    // Write row padding (align to 4-byte boundary)
                    for (int p = 0; p < rowPadding; p++)
                    {
                        _writer.WriteUInt8(0);
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

        /// <summary>
        /// Gets the written BMP data as a byte array.
        /// Only available when using the byte array constructor.
        /// </summary>
        /// <returns>BMP file data as byte array.</returns>
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

