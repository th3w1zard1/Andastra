using System;
using System.Collections.Generic;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Formats.TXI;

namespace Andastra.Tests.Runtime.TestHelpers
{
    /// <summary>
    /// Helper class for creating test TPC and TXI data for font testing.
    /// </summary>
    public static class FontTestHelper
    {
        /// <summary>
        /// Creates a minimal test TPC texture for font testing.
        /// </summary>
        public static TPC CreateTestTPC(int width = 256, int height = 256, TPCTextureFormat format = TPCTextureFormat.RGBA)
        {
            var tpc = new TPC();
            tpc._format = format;

            var layer = new TPCLayer();
            int bytesPerPixel = format.BytesPerPixel();
            int dataSize = width * height * bytesPerPixel;
            byte[] textureData = new byte[dataSize];

            // Fill with a simple pattern (white texture)
            for (int i = 0; i < textureData.Length; i += bytesPerPixel)
            {
                if (format == TPCTextureFormat.RGBA || format == TPCTextureFormat.BGRA)
                {
                    textureData[i] = 255;     // R
                    textureData[i + 1] = 255; // G
                    textureData[i + 2] = 255; // B
                    textureData[i + 3] = 255; // A
                }
                else if (format == TPCTextureFormat.RGB || format == TPCTextureFormat.BGR)
                {
                    textureData[i] = 255;     // R
                    textureData[i + 1] = 255; // G
                    textureData[i + 2] = 255; // B
                }
                else if (format == TPCTextureFormat.Greyscale)
                {
                    textureData[i] = 255; // Grayscale
                }
            }

            var mipmap = new TPCMipmap(width, height, format, textureData);
            layer.Mipmaps.Add(mipmap);
            tpc.Layers.Add(layer);

            return tpc;
        }

        /// <summary>
        /// Creates a test TXI with font metrics.
        /// </summary>
        public static TXI CreateTestTXI(
            float fontHeight = 16.0f,
            float fontWidth = 8.0f,
            float baselineHeight = 12.0f,
            float spacingR = 1.0f,
            float spacingB = 2.0f,
            int cols = 16,
            int rows = 16,
            float textureWidth = 256.0f)
        {
            var txi = new TXI();
            txi.Features.Fontheight = fontHeight;
            txi.Features.Fontwidth = fontWidth;
            txi.Features.Baselineheight = baselineHeight;
            txi.Features.SpacingR = spacingR;
            txi.Features.SpacingB = spacingB;
            txi.Features.Cols = cols;
            txi.Features.Rows = rows;
            txi.Features.Texturewidth = textureWidth;

            // Create upperleftcoords and lowerrightcoords for a simple grid
            txi.Features.Upperleftcoords = new List<Tuple<float, float, int>>();
            txi.Features.Lowerrightcoords = new List<Tuple<float, float, int>>();

            int numChars = cols * rows;
            float cellWidth = textureWidth / cols;
            float cellHeight = (textureWidth / cols) * (rows > 0 ? (float)rows / cols : 1.0f);

            for (int i = 0; i < numChars; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x1 = col * cellWidth;
                float y1 = row * cellHeight;
                float x2 = x1 + cellWidth;
                float y2 = y1 + cellHeight;

                txi.Features.Upperleftcoords.Add(Tuple.Create(x1, y1, i));
                txi.Features.Lowerrightcoords.Add(Tuple.Create(x2, y2, i));
            }

            return txi;
        }

        /// <summary>
        /// Creates test TPC data as byte array (complete TGA format).
        /// Based on TGA.cs WriteTga implementation for 1:1 parity with TGA format specification.
        /// </summary>
        public static byte[] CreateTestTPCData(int width = 256, int height = 256)
        {
            // Create complete TGA header + data
            // TGA header: 18 bytes (Truevision Graphics Adapter format)
            // Based on TGA.cs: WriteTga method for uncompressed RGBA format
            int dataSize = width * height * 4;
            byte[] tgaData = new byte[18 + dataSize];

            // Complete TGA header structure (18 bytes)
            // Byte 0: ID length (0 = no image ID field)
            tgaData[0] = 0;

            // Byte 1: Color map type (0 = no color map)
            tgaData[1] = 0;

            // Byte 2: Image type (2 = uncompressed true color)
            tgaData[2] = 2;

            // Bytes 3-4: Color map origin (first entry index, 0 = no color map)
            tgaData[3] = 0;
            tgaData[4] = 0;

            // Bytes 5-6: Color map length (number of entries, 0 = no color map)
            tgaData[5] = 0;
            tgaData[6] = 0;

            // Byte 7: Color map entry size (bits per entry, 0 = no color map)
            tgaData[7] = 0;

            // Bytes 8-9: X origin (horizontal coordinate of lower left corner, 0 = top-left origin)
            tgaData[8] = 0;
            tgaData[9] = 0;

            // Bytes 10-11: Y origin (vertical coordinate of lower left corner, 0 = top-left origin)
            tgaData[10] = 0;
            tgaData[11] = 0;

            // Bytes 12-13: Width (little-endian ushort)
            tgaData[12] = (byte)(width & 0xFF);
            tgaData[13] = (byte)((width >> 8) & 0xFF);

            // Bytes 14-15: Height (little-endian ushort)
            tgaData[14] = (byte)(height & 0xFF);
            tgaData[15] = (byte)((height >> 8) & 0xFF);

            // Byte 16: Pixel depth (32 bits per pixel for RGBA)
            tgaData[16] = 32;

            // Byte 17: Image descriptor
            // Bit 0-3: Alpha channel bits (0x08 = 8 bits of alpha)
            // Bit 4: Origin (0x20 = top-left origin, 0x00 = bottom-left origin)
            // Bit 5-7: Unused (must be 0)
            // Value: 0x28 = 0x20 | 0x08 (top-left origin, 8-bit alpha)
            tgaData[17] = 0x28;

            // Fill pixel data with white texture (BGRA byte order for TGA format)
            // TGA format stores pixels in BGRA order (blue, green, red, alpha)
            // Based on TGA.cs: WriteTga method pixel data format
            for (int i = 18; i < tgaData.Length; i += 4)
            {
                tgaData[i] = 255;     // B (blue)
                tgaData[i + 1] = 255; // G (green)
                tgaData[i + 2] = 255; // R (red)
                tgaData[i + 3] = 255; // A (alpha)
            }

            return tgaData;
        }

        /// <summary>
        /// Creates test TXI data as string.
        /// </summary>
        public static string CreateTestTXIData(
            float fontHeight = 16.0f,
            float fontWidth = 8.0f,
            float baselineHeight = 12.0f,
            float spacingR = 1.0f,
            float spacingB = 2.0f,
            int cols = 16,
            int rows = 16,
            float textureWidth = 256.0f)
        {
            var lines = new List<string>
            {
                $"fontheight {fontHeight}",
                $"fontwidth {fontWidth}",
                $"baselineheight {baselineHeight}",
                $"spacingr {spacingR}",
                $"spacingb {spacingB}",
                $"cols {cols}",
                $"rows {rows}",
                $"texturewidth {textureWidth}",
                $"numchars {cols * rows}",
                "upperleftcoords",
            };

            int numChars = cols * rows;
            float cellWidth = textureWidth / cols;
            float cellHeight = (textureWidth / cols) * (rows > 0 ? (float)rows / cols : 1.0f);

            for (int i = 0; i < numChars; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x1 = col * cellWidth;
                float y1 = row * cellHeight;
                lines.Add($"{x1} {y1} {i}");
            }

            lines.Add("lowerrightcoords");
            for (int i = 0; i < numChars; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float x1 = col * cellWidth;
                float y1 = row * cellHeight;
                float x2 = x1 + cellWidth;
                float y2 = y1 + cellHeight;
                lines.Add($"{x2} {y2} {i}");
            }

            return string.Join("\n", lines);
        }
    }
}

