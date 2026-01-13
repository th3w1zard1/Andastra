using System;
using System.Collections.Generic;

namespace BioWare.NET.Resource.Formats.TPC
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
                    // Downsample the current mipmap data to generate the next smaller mipmap level
                    // Based on PyKotor: Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/downsample.py
                    // Uses box filter (2x2 pixel averaging) for RGB formats, block averaging for DXT formats
                    currentData = Downsample(currentData, w, h, tpcFormat);
                }
                else
                {
                    break;
                }
            }
        }

        // swkotor.exe/swkotor2.exe: Mipmap downsampling for TPC textures
        // Based on PyKotor: Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/downsample.py
        // Downsamples texture data from current mipmap level to next smaller level (half width/height)
        // - RGB formats: Uses box filter (averages 2x2 pixel blocks)
        // - DXT formats: Averages color endpoints and indices from 2x2 block groups
        private static byte[] Downsample(byte[] data, int width, int height, TPCTextureFormat tpcFormat)
        {
            if (data == null || data.Length == 0)
            {
                return new byte[0];
            }

            if (tpcFormat.IsDxt())
            {
                return DownsampleDxt(data, width, height, tpcFormat.BytesPerBlock());
            }
            else
            {
                return DownsampleRgb(data, width, height, tpcFormat.BytesPerPixel());
            }
        }

        // Downsample DXT compressed image data
        // Based on PyKotor: downsample_dxt() function
        // DXT formats use 4x4 pixel blocks, so we downsample by averaging 2x2 groups of blocks
        // DXT block structure:
        // - DXT1 (8 bytes): color0 (2) + color1 (2) + color indices (4)
        // - DXT3 (16 bytes): alpha per-pixel (8) + color0 (2) + color1 (2) + color indices (4)
        // - DXT5 (16 bytes): alpha endpoints (2) + alpha indices (6) + color0 (2) + color1 (2) + color indices (4)
        // Note: PyKotor implementation treats all 16-byte formats as DXT5 structure for downsampling
        private static byte[] DownsampleDxt(byte[] data, int width, int height, int bytesPerBlock)
        {
            if (data == null || data.Length == 0 || (bytesPerBlock != 8 && bytesPerBlock != 16))
            {
                return new byte[0];
            }

            // Calculate block counts
            int blocksX = (width + 3) / 4;  // Round up to nearest block boundary
            int blocksY = (height + 3) / 4;
            int newBlocksX = (blocksX + 1) / 2;  // Half the blocks (rounded up)
            int newBlocksY = (blocksY + 1) / 2;
            byte[] newData = new byte[newBlocksX * newBlocksY * bytesPerBlock];

            for (int y = 0; y < newBlocksY; y++)
            {
                for (int x = 0; x < newBlocksX; x++)
                {
                    int newBlockIndex = (y * newBlocksX + x) * bytesPerBlock;

                    // Average color endpoints from up to four source blocks
                    int[] color0Sum = new int[3]; // R, G, B
                    int[] color1Sum = new int[3];
                    int alpha0Sum = 0;
                    int alpha1Sum = 0;
                    int blockCount = 0;

                    // Sample up to 2x2 blocks
                    for (int dy = 0; dy < 2; dy++)
                    {
                        for (int dx = 0; dx < 2; dx++)
                        {
                            int srcX = x * 2 + dx;
                            int srcY = y * 2 + dy;
                            if (srcX < blocksX && srcY < blocksY)
                            {
                                int srcBlockIndex = (srcY * blocksX + srcX) * bytesPerBlock;
                                if (srcBlockIndex + 4 <= data.Length)
                                {
                                    // Extract color endpoints (16-bit 565 format)
                                    // For DXT1: bytes 0-3 contain color0 and color1
                                    // For DXT3/DXT5: bytes 8-11 contain color0 and color1 (after alpha data)
                                    // PyKotor code reads from bytes 0-3, which works for DXT1 but assumes
                                    // a different layout for DXT3/DXT5. We'll match PyKotor's behavior.
                                    ushort color0 = (ushort)(data[srcBlockIndex] | (data[srcBlockIndex + 1] << 8));
                                    ushort color1 = (ushort)(data[srcBlockIndex + 2] | (data[srcBlockIndex + 3] << 8));

                                    // Convert 565 to RGB and accumulate
                                    // R: bits 11-15 (5 bits), G: bits 5-10 (6 bits), B: bits 0-4 (5 bits)
                                    color0Sum[0] += ((color0 >> 11) & 0x1F) << 3; // R: 5 bits -> 8 bits
                                    color0Sum[1] += ((color0 >> 5) & 0x3F) << 2;   // G: 6 bits -> 8 bits
                                    color0Sum[2] += (color0 & 0x1F) << 3;          // B: 5 bits -> 8 bits
                                    color1Sum[0] += ((color1 >> 11) & 0x1F) << 3;
                                    color1Sum[1] += ((color1 >> 5) & 0x3F) << 2;
                                    color1Sum[2] += (color1 & 0x1F) << 3;

                                    // For DXT3/DXT5 (16 bytes), handle alpha
                                    // PyKotor reads alpha from bytes 0-1 (treating as DXT5 alpha endpoints)
                                    if (bytesPerBlock == 16 && srcBlockIndex + 2 <= data.Length)
                                    {
                                        alpha0Sum += data[srcBlockIndex];
                                        alpha1Sum += data[srcBlockIndex + 1];
                                    }

                                    blockCount++;
                                }
                            }
                        }
                    }

                    if (blockCount == 0)
                    {
                        // No source blocks available, fill with zeros
                        Array.Clear(newData, newBlockIndex, bytesPerBlock);
                        continue;
                    }

                    // Average the color endpoints
                    int[] color0Avg = new int[3];
                    int[] color1Avg = new int[3];
                    for (int i = 0; i < 3; i++)
                    {
                        color0Avg[i] = color0Sum[i] / blockCount;
                        color1Avg[i] = color1Sum[i] / blockCount;
                    }

                    // Convert back to 565 format
                    ushort color0_565 = (ushort)(((color0Avg[0] >> 3) << 11) | ((color0Avg[1] >> 2) << 5) | (color0Avg[2] >> 3));
                    ushort color1_565 = (ushort)(((color1Avg[0] >> 3) << 11) | ((color1Avg[1] >> 2) << 5) | (color1Avg[2] >> 3));

                    // Write block data matching PyKotor's structure
                    // PyKotor writes color to bytes 0-3, then overwrites 0-1 with alpha for DXT5
                    // Then writes color indices to 4-7, then alpha indices to 2-7 (overwriting color1 and indices)
                    // This matches PyKotor's behavior exactly, even though it may not match standard DXT format
                    // Write color endpoints first (will be overwritten for DXT5)
                    newData[newBlockIndex] = (byte)(color0_565 & 0xFF);
                    newData[newBlockIndex + 1] = (byte)((color0_565 >> 8) & 0xFF);
                    newData[newBlockIndex + 2] = (byte)(color1_565 & 0xFF);
                    newData[newBlockIndex + 3] = (byte)((color1_565 >> 8) & 0xFF);

                    // For DXT3/DXT5, overwrite bytes 0-1 with alpha endpoints (matching PyKotor)
                    if (bytesPerBlock == 16)
                    {
                        int alpha0Avg = alpha0Sum / blockCount;
                        int alpha1Avg = alpha1Sum / blockCount;
                        newData[newBlockIndex] = (byte)alpha0Avg;
                        newData[newBlockIndex + 1] = (byte)alpha1Avg;
                    }

                    // Copy/average the color indices (4 bytes for 4x4 pixel block, at bytes 4-7)
                    if (blockCount == 4)
                    {
                        // Average the color indices from four blocks
                        for (int i = 0; i < 4; i++)
                        {
                            int indicesSum = 0;
                            int indicesCount = 0;
                            for (int dy = 0; dy < 2; dy++)
                            {
                                for (int dx = 0; dx < 2; dx++)
                                {
                                    int srcX = x * 2 + dx;
                                    int srcY = y * 2 + dy;
                                    if (srcX < blocksX && srcY < blocksY)
                                    {
                                        int srcBlockIndex = (srcY * blocksX + srcX) * bytesPerBlock;
                                        // PyKotor reads color indices from offset 4 (matching DXT1 structure)
                                        if (srcBlockIndex + 4 + i < data.Length)
                                        {
                                            indicesSum += data[srcBlockIndex + 4 + i];
                                            indicesCount++;
                                        }
                                    }
                                }
                            }
                            if (indicesCount > 0 && newBlockIndex + 4 + i < newData.Length)
                            {
                                newData[newBlockIndex + 4 + i] = (byte)(indicesSum / indicesCount);
                            }
                        }
                    }
                    else
                    {
                        // If we don't have all four blocks, copy from the first available block
                        int srcX = x * 2;
                        int srcY = y * 2;
                        if (srcX < blocksX && srcY < blocksY)
                        {
                            int srcBlockIndex = (srcY * blocksX + srcX) * bytesPerBlock;
                            if (srcBlockIndex + 8 <= data.Length && newBlockIndex + 8 <= newData.Length)
                            {
                                Array.Copy(data, srcBlockIndex + 4, newData, newBlockIndex + 4, 4);
                            }
                        }
                    }

                    // For DXT5, handle alpha indices (6 bytes, written to bytes 2-7, overwriting color1 and color indices)
                    // Note: This matches PyKotor's behavior, which writes alpha indices after color indices
                    if (bytesPerBlock == 16)
                    {
                        if (blockCount == 4)
                        {
                            // Average the alpha indices from four blocks
                            for (int i = 0; i < 6; i++)
                            {
                                int alphaIndicesSum = 0;
                                int alphaIndicesCount = 0;
                                for (int dy = 0; dy < 2; dy++)
                                {
                                    for (int dx = 0; dx < 2; dx++)
                                    {
                                        int srcX = x * 2 + dx;
                                        int srcY = y * 2 + dy;
                                        if (srcX < blocksX && srcY < blocksY)
                                        {
                                            int srcBlockIndex = (srcY * blocksX + srcX) * bytesPerBlock;
                                            // PyKotor reads alpha indices from offset 2
                                            if (srcBlockIndex + 2 + i < data.Length)
                                            {
                                                alphaIndicesSum += data[srcBlockIndex + 2 + i];
                                                alphaIndicesCount++;
                                            }
                                        }
                                    }
                                }
                                if (alphaIndicesCount > 0 && newBlockIndex + 2 + i < newData.Length)
                                {
                                    newData[newBlockIndex + 2 + i] = (byte)(alphaIndicesSum / alphaIndicesCount);
                                }
                            }
                        }
                        else
                        {
                            // If we don't have all four blocks, copy from the first available block
                            int srcX = x * 2;
                            int srcY = y * 2;
                            if (srcX < blocksX && srcY < blocksY)
                            {
                                int srcBlockIndex = (srcY * blocksX + srcX) * bytesPerBlock;
                                if (srcBlockIndex + 8 <= data.Length && newBlockIndex + 8 <= newData.Length)
                                {
                                    // Copy alpha indices (bytes 2-7, overwriting color1 and color indices)
                                    Array.Copy(data, srcBlockIndex + 2, newData, newBlockIndex + 2, 6);
                                }
                            }
                        }
                    }
                    {
                        // DXT1 (8 bytes): Write color endpoints and indices
                        newData[newBlockIndex] = (byte)(color0_565 & 0xFF);
                        newData[newBlockIndex + 1] = (byte)((color0_565 >> 8) & 0xFF);
                        newData[newBlockIndex + 2] = (byte)(color1_565 & 0xFF);
                        newData[newBlockIndex + 3] = (byte)((color1_565 >> 8) & 0xFF);

                        // Copy/average the color indices (4 bytes for 4x4 pixel block)
                        if (blockCount == 4)
                        {
                            // Average the color indices from four blocks
                            for (int i = 0; i < 4; i++)
                            {
                                int indicesSum = 0;
                                int indicesCount = 0;
                                for (int dy = 0; dy < 2; dy++)
                                {
                                    for (int dx = 0; dx < 2; dx++)
                                    {
                                        int srcX = x * 2 + dx;
                                        int srcY = y * 2 + dy;
                                        if (srcX < blocksX && srcY < blocksY)
                                        {
                                            int srcBlockIndex = (srcY * blocksX + srcX) * bytesPerBlock;
                                            if (srcBlockIndex + 4 + i < data.Length)
                                            {
                                                indicesSum += data[srcBlockIndex + 4 + i];
                                                indicesCount++;
                                            }
                                        }
                                    }
                                }
                                if (indicesCount > 0 && newBlockIndex + 4 + i < newData.Length)
                                {
                                    newData[newBlockIndex + 4 + i] = (byte)(indicesSum / indicesCount);
                                }
                            }
                        }
                        else
                        {
                            // If we don't have all four blocks, copy from the first available block
                            int srcX = x * 2;
                            int srcY = y * 2;
                            if (srcX < blocksX && srcY < blocksY)
                            {
                                int srcBlockIndex = (srcY * blocksX + srcX) * bytesPerBlock;
                                if (srcBlockIndex + 8 <= data.Length && newBlockIndex + 8 <= newData.Length)
                                {
                                    Array.Copy(data, srcBlockIndex + 4, newData, newBlockIndex + 4, 4);
                                }
                            }
                        }
                    }
                }
            }

            return newData;
        }

        // Downsample RGB/RGBA image data using box filter (2x2 pixel averaging)
        // Based on PyKotor: downsample_rgb() function
        // Averages each 2x2 block of pixels into a single pixel
        private static byte[] DownsampleRgb(byte[] data, int width, int height, int bytesPerPixel)
        {
            if (data == null || data.Length == 0 || bytesPerPixel <= 0)
            {
                return new byte[0];
            }

            int nextWidth = Math.Max(1, width / 2);
            int nextHeight = Math.Max(1, height / 2);
            int nextSize = nextWidth * nextHeight * bytesPerPixel;
            byte[] nextData = new byte[nextSize];

            for (int y = 0; y < nextHeight; y++)
            {
                for (int x = 0; x < nextWidth; x++)
                {
                    int srcX = x * 2;
                    int srcY = y * 2;
                    int srcOffset = (srcY * width + srcX) * bytesPerPixel;
                    int dstOffset = (y * nextWidth + x) * bytesPerPixel;

                    // Average the 2x2 block of pixels
                    for (int p = 0; p < bytesPerPixel; p++)
                    {
                        int sum = 0;
                        int count = 0;

                        // Top-left pixel
                        if (srcOffset + p < data.Length)
                        {
                            sum += data[srcOffset + p];
                            count++;
                        }

                        // Top-right pixel
                        int topRightOffset = srcOffset + bytesPerPixel + p;
                        if (srcX + 1 < width && topRightOffset < data.Length)
                        {
                            sum += data[topRightOffset];
                            count++;
                        }

                        // Bottom-left pixel
                        int bottomLeftOffset = srcOffset + width * bytesPerPixel + p;
                        if (srcY + 1 < height && bottomLeftOffset < data.Length)
                        {
                            sum += data[bottomLeftOffset];
                            count++;
                        }

                        // Bottom-right pixel
                        int bottomRightOffset = srcOffset + width * bytesPerPixel + bytesPerPixel + p;
                        if (srcX + 1 < width && srcY + 1 < height && bottomRightOffset < data.Length)
                        {
                            sum += data[bottomRightOffset];
                            count++;
                        }

                        // Average and store
                        if (count > 0 && dstOffset + p < nextData.Length)
                        {
                            nextData[dstOffset + p] = (byte)(sum / count);
                        }
                    }
                }
            }

            return nextData;
        }
    }
}

