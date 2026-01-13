using System;
using System.IO;
using BioWare.NET.Common;

namespace BioWare.NET.Resource.Formats.TPC
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/tpc/tga.py
    // Original: class TGAImage, read_tga, write_tga
    public class TGAImage
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public byte[] Data { get; set; } // RGBA8888, row-major, origin = top-left

        public int PixelDepth => 32;

        public TGAImage(int width, int height, byte[] data)
        {
            Width = width;
            Height = height;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }
    }

    public static class TGA
    {
        private const byte TGA_TYPE_TRUE_COLOR = 2;
        private const byte TGA_TYPE_GRAYSCALE = 3;
        private const byte TGA_TYPE_RLE_TRUE_COLOR = 10;

        private static void FlipVertically(byte[] buffer, int width, int height, int bpp)
        {
            int stride = width * bpp;
            for (int row = 0; row < height / 2; row++)
            {
                int a = row * stride;
                int b = (height - row - 1) * stride;
                for (int i = 0; i < stride; i++)
                {
                    byte temp = buffer[a + i];
                    buffer[a + i] = buffer[b + i];
                    buffer[b + i] = temp;
                }
            }
        }

        private static byte[] ReadRLE(Stream stream, int width, int height, int pixelDepth)
        {
            int bytesPerPixel = pixelDepth / 8;
            int totalPixels = width * height;
            byte[] result = new byte[totalPixels * bytesPerPixel];

            int dst = 0;
            while (dst < result.Length)
            {
                int headerByte = stream.ReadByte();
                if (headerByte == -1)
                {
                    throw new ArgumentException("Unexpected end of RLE stream");
                }

                byte header = (byte)headerByte;
                int count = (header & 0x7F) + 1;

                if ((header & 0x80) != 0)
                {
                    byte[] pixel = new byte[bytesPerPixel];
                    int read = stream.Read(pixel, 0, bytesPerPixel);
                    if (read != bytesPerPixel)
                    {
                        throw new ArgumentException("Incomplete RLE pixel data");
                    }
                    for (int i = 0; i < count; i++)
                    {
                        Array.Copy(pixel, 0, result, dst, bytesPerPixel);
                        dst += bytesPerPixel;
                    }
                }
                else
                {
                    byte[] raw = new byte[count * bytesPerPixel];
                    int read = stream.Read(raw, 0, raw.Length);
                    if (read != raw.Length)
                    {
                        throw new ArgumentException("Incomplete raw RLE span");
                    }
                    Array.Copy(raw, 0, result, dst, raw.Length);
                    dst += raw.Length;
                }
            }

            return result;
        }

        public static TGAImage ReadTga(Stream stream)
        {
            byte[] header = new byte[18];
            int read = stream.Read(header, 0, 18);
            if (read != 18)
            {
                throw new ArgumentException("Incomplete TGA header");
            }

            int idLength = header[0];
            byte colorMapType = header[1];
            byte imageType = header[2];
            ushort colorMapOrigin = BitConverter.ToUInt16(header, 3);
            ushort colorMapLength = BitConverter.ToUInt16(header, 5);
            byte colorMapDepth = header[7];
            ushort xOrigin = BitConverter.ToUInt16(header, 8);
            ushort yOrigin = BitConverter.ToUInt16(header, 10);
            ushort width = BitConverter.ToUInt16(header, 12);
            ushort height = BitConverter.ToUInt16(header, 14);
            byte pixelDepth = header[16];
            byte descriptor = header[17];

            if (colorMapType != 0)
            {
                throw new ArgumentException("Color-mapped TGAs are not supported");
            }

            if (imageType != TGA_TYPE_TRUE_COLOR && imageType != TGA_TYPE_GRAYSCALE && imageType != TGA_TYPE_RLE_TRUE_COLOR)
            {
                throw new ArgumentException($"Unsupported TGA image type: {imageType}");
            }

            stream.Seek(idLength, SeekOrigin.Current);

            byte[] raw;
            if (imageType == TGA_TYPE_RLE_TRUE_COLOR)
            {
                raw = ReadRLE(stream, width, height, pixelDepth);
            }
            else
            {
                int bytesPerPixel = pixelDepth / 8;
                int dataSize = width * height * bytesPerPixel;
                raw = new byte[dataSize];
                read = stream.Read(raw, 0, dataSize);
                if (read != dataSize)
                {
                    throw new ArgumentException("Unexpected end of TGA pixel data");
                }
            }

            byte[] rgba = new byte[width * height * 4];

            if (imageType == TGA_TYPE_GRAYSCALE || pixelDepth == 8)
            {
                for (int i = 0; i < raw.Length; i++)
                {
                    byte value = raw[i];
                    rgba[i * 4] = value;
                    rgba[i * 4 + 1] = value;
                    rgba[i * 4 + 2] = value;
                    rgba[i * 4 + 3] = 255;
                }
            }
            else if (pixelDepth == 24)
            {
                for (int i = 0; i < width * height; i++)
                {
                    byte b = raw[i * 3];
                    byte g = raw[i * 3 + 1];
                    byte r = raw[i * 3 + 2];
                    rgba[i * 4] = r;
                    rgba[i * 4 + 1] = g;
                    rgba[i * 4 + 2] = b;
                    rgba[i * 4 + 3] = 255;
                }
            }
            else if (pixelDepth == 32)
            {
                for (int i = 0; i < width * height; i++)
                {
                    byte b = raw[i * 4];
                    byte g = raw[i * 4 + 1];
                    byte r = raw[i * 4 + 2];
                    byte a = raw[i * 4 + 3];
                    rgba[i * 4] = r;
                    rgba[i * 4 + 1] = g;
                    rgba[i * 4 + 2] = b;
                    rgba[i * 4 + 3] = a;
                }
            }
            else
            {
                throw new ArgumentException($"Unsupported pixel depth: {pixelDepth}");
            }

            // Flip vertically if origin is bottom-left (bit 5 not set).
            if ((descriptor & 0x20) == 0)
            {
                FlipVertically(rgba, width, height, 4);
            }

            return new TGAImage(width, height, rgba);
        }

        public static void WriteTga(TGAImage image, Stream stream, bool rle = false)
        {
            int width = image.Width;
            int height = image.Height;
            byte pixelDepth = 32;
            byte descriptor = (byte)(0x20 | 0x08); // origin top-left, 8 bits of alpha

            byte[] header = new byte[18];
            header[0] = 0; // ID length
            header[1] = 0; // color map type
            header[2] = rle ? TGA_TYPE_RLE_TRUE_COLOR : TGA_TYPE_TRUE_COLOR;
            BitConverter.GetBytes((ushort)0).CopyTo(header, 3); // color map origin
            BitConverter.GetBytes((ushort)0).CopyTo(header, 5); // color map length
            header[7] = 0; // color map depth
            BitConverter.GetBytes((ushort)0).CopyTo(header, 8); // x origin
            BitConverter.GetBytes((ushort)0).CopyTo(header, 10); // y origin
            BitConverter.GetBytes((ushort)width).CopyTo(header, 12);
            BitConverter.GetBytes((ushort)height).CopyTo(header, 14);
            header[16] = pixelDepth;
            header[17] = descriptor;

            stream.Write(header, 0, 18);

            byte[] data = new byte[image.Data.Length];
            Array.Copy(image.Data, data, image.Data.Length);
            // Ensure origin is top-left; our in-memory data already is, but the TGA
            // writer expects bottom-left order before optional flipping. We invert so
            // that the stored data matches descriptor 0x20 (top origin).
            FlipVertically(data, width, height, 4);

            if (!rle)
            {
                for (int i = 0; i < width * height; i++)
                {
                    byte r = data[i * 4];
                    byte g = data[i * 4 + 1];
                    byte b = data[i * 4 + 2];
                    byte a = data[i * 4 + 3];
                    stream.WriteByte(b);
                    stream.WriteByte(g);
                    stream.WriteByte(r);
                    stream.WriteByte(a);
                }
                return;
            }

            // Simple RLE encoder that operates on scanlines.
            void WritePacket(byte[][] packetPixels, bool raw)
            {
                int count = packetPixels.Length;
                if (raw)
                {
                    stream.WriteByte((byte)(count - 1));
                    foreach (byte[] px in packetPixels)
                    {
                        stream.Write(px, 0, px.Length);
                    }
                }
                else
                {
                    stream.WriteByte((byte)(0x80 | (count - 1)));
                    stream.Write(packetPixels[0], 0, packetPixels[0].Length);
                }
            }

            int stride = width * 4;
            for (int row = 0; row < height; row++)
            {
                int start = row * stride;
                int x = 0;
                while (x < width)
                {
                    // Look ahead for repeated pixels.
                    byte[] current = new byte[4];
                    Array.Copy(data, start + x * 4, current, 0, 4);
                    int repeat = 1;
                    while (x + repeat < width)
                    {
                        byte[] next = new byte[4];
                        Array.Copy(data, start + (x + repeat) * 4, next, 0, 4);
                        if (next[0] == current[0] && next[1] == current[1] && next[2] == current[2] && next[3] == current[3] && repeat < 128)
                        {
                            repeat++;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (repeat > 1)
                    {
                        byte[] bgra = { current[2], current[1], current[0], current[3] };
                        WritePacket(new[] { bgra }, raw: false);
                        x += repeat;
                        continue;
                    }

                    // Gather raw run until we hit a repetition.
                    System.Collections.Generic.List<byte[]> rawPixels = new System.Collections.Generic.List<byte[]>();
                    while (x < width)
                    {
                        byte[] pixel = new byte[4];
                        Array.Copy(data, start + x * 4, pixel, 0, 4);
                        byte[] bgra = { pixel[2], pixel[1], pixel[0], pixel[3] };
                        rawPixels.Add(bgra);
                        x++;
                        if (x == width)
                        {
                            break;
                        }
                        byte[] nxt = new byte[4];
                        Array.Copy(data, start + x * 4, nxt, 0, 4);
                        if ((nxt[0] == pixel[0] && nxt[1] == pixel[1] && nxt[2] == pixel[2] && nxt[3] == pixel[3]) || rawPixels.Count == 128)
                        {
                            break;
                        }
                    }

                    WritePacket(rawPixels.ToArray(), raw: true);
                }
            }
        }
    }
}
