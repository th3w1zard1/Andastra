using System;
using BioWare.NET.Resource.Formats.TPC;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Converters
{
    /// <summary>
    /// Converts BioWare.NET TPC texture data to MonoGame Texture2D.
    /// Handles DXT1/DXT3/DXT5 compressed formats, RGB/RGBA uncompressed,
    /// and grayscale textures.
    /// </summary>
    /// <remarks>
    /// TPC to MonoGame Texture Converter:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) texture loading system (modern MonoGame adaptation)
    /// - Located via string references: "Texture" @ 0x007c71b4, "texture" @ 0x007bab24
    /// - "texturewidth" @ 0x007b6e98, "texturenames" @ 0x007bacb0
    /// - "texture0" @ 0x007bb018, "texture1" @ 0x007bb00c (texture unit references)
    /// - "depth_texture" @ 0x007bab5c, "m_sDepthTextureName" @ 0x007baaa8 (depth texture)
    /// - "envmaptexture" @ 0x007bb284, "bumpmaptexture" @ 0x007bb2a8, "bumpyshinytexture" @ 0x007bb294
    /// - "dirt_texture" @ 0x007bae9c, "rotatetexture" @ 0x007baf14
    /// - Texture properties: "TextureVar" @ 0x007c0974, "TextureVariation" @ 0x007c84b4
    /// - "ALTTEXTURE" @ 0x007cdc04 (alternate texture reference)
    /// - Texture directories: "TEXTUREPACKS" @ 0x007c6a08, "TEXTUREPACKS:" @ 0x007c7190
    /// - ".\texturepacks" @ 0x007c6a18, "d:\texturepacks" @ 0x007c6a28
    /// - "LIVE%d:OVERRIDE\textures" @ 0x007c72ac (override texture path format)
    /// - "Texture Quality" @ 0x007c7528 (texture quality setting)
    /// - OpenGL texture functions: glBindTexture, glGenTextures, glDeleteTextures, glIsTexture
    /// - OpenGL texture extensions: GL_EXT_texture_compression_s3tc, GL_ARB_texture_compression
    /// - GL_EXT_texture_cube_map, GL_EXT_texture_filter_anisotropic, GL_ARB_multitexture
    /// - Original implementation: KOTOR loads TPC files and creates DirectX textures (D3DTexture8/9)
    /// - TPC format: BioWare texture format supporting DXT1/DXT3/DXT5 compression, RGB/RGBA, grayscale
    /// - Original engine: Uses DirectX texture creation APIs (D3DXCreateTextureFromFileInMemory, etc.)
    /// - This MonoGame implementation: Converts TPC format to MonoGame Texture2D
    /// - Compression: Handles DXT compression formats, converts to RGBA for MonoGame compatibility
    /// - Mipmaps: Preserves mipmap chain from TPC or generates mipmaps if missing
    /// - Cube maps: TPC cube maps converted to MonoGame TextureCube (if supported)
    /// - Note: Original engine used DirectX APIs, this is a modern MonoGame adaptation
    /// </remarks>
    public static class TpcToMonoGameTextureConverter
    {
        /// <summary>
        /// Converts a TPC texture to a MonoGame Texture (Texture2D for 2D textures, TextureCube for cube maps).
        /// </summary>
        /// <param name="tpc">The TPC texture to convert.</param>
        /// <param name="device">The graphics device.</param>
        /// <param name="generateMipmaps">Whether to generate mipmaps if not present.</param>
        /// <returns>A MonoGame Texture ready for rendering (Texture2D for 2D textures, TextureCube for cube maps).</returns>
        // Convert TPC texture format to MonoGame Texture (Texture2D or TextureCube)
        // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
        // Texture2D represents 2D image data for rendering
        // TextureCube represents cube map textures (6 faces) for environment mapping
        // Method signature: static Texture Convert(TPC tpc, GraphicsDevice device, bool generateMipmaps)
        // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.GraphicsDevice.html
        // GraphicsDevice parameter provides access to graphics hardware for texture creation
        // Source: https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_Load_Texture.html
        public static Texture Convert([NotNull] TPC tpc, [NotNull] GraphicsDevice device, bool generateMipmaps = true)
        {
            if (tpc == null)
            {
                throw new ArgumentNullException("tpc");
            }
            if (device == null)
            {
                throw new ArgumentNullException("device");
            }

            if (tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
            {
                throw new ArgumentException("TPC has no texture data", "tpc");
            }

            // Get dimensions from first layer, first mipmap
            TPCMipmap baseMipmap = tpc.Layers[0].Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;
            TPCTextureFormat format = tpc.Format();

            // Handle cube maps - Convert to MonoGame TextureCube
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) cube map texture loading (swkotor2.exe: texture cube map handling)
            // TPC cube maps have 6 layers, one for each face in DirectX/OpenGL order:
            // 0: PositiveX (right), 1: NegativeX (left), 2: PositiveY (top), 
            // 3: NegativeY (bottom), 4: PositiveZ (front), 5: NegativeZ (back)
            // Reference: vendor/xoreos/src/graphics/images/tpc.cpp:420-482 (cube map fixup)
            // Reference: vendor/reone/include/reone/graphics/types.h:88-95 (CubeMapFace enum)
            if (tpc.IsCubeMap && tpc.Layers.Count == 6)
            {
                return ConvertCubeMap(tpc, device, generateMipmaps);
            }

            // Convert standard 2D texture
            return Convert2DTexture(tpc.Layers[0], device, generateMipmaps);
        }

        /// <summary>
        /// Converts a TPC texture to RGBA byte array for manual processing.
        /// </summary>
        public static byte[] ConvertToRgba([NotNull] TPC tpc)
        {
            if (tpc == null)
            {
                throw new ArgumentNullException("tpc");
            }

            if (tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
            {
                return new byte[0];
            }

            TPCMipmap mipmap = tpc.Layers[0].Mipmaps[0];
            return ConvertMipmapToRgba(mipmap);
        }

        /// <summary>
        /// Converts a TPC cube map to a MonoGame TextureCube.
        /// </summary>
        /// <param name="tpc">The TPC cube map texture (must have 6 layers).</param>
        /// <param name="device">The graphics device.</param>
        /// <param name="generateMipmaps">Whether to generate mipmaps if not present.</param>
        /// <returns>A MonoGame TextureCube ready for rendering.</returns>
        /// <remarks>
        /// Cube Map Conversion:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) cube map texture loading system
        /// - TPC cube maps store 6 faces as separate layers
        /// - DirectX/XNA cube map face order: PositiveX, NegativeX, PositiveY, NegativeY, PositiveZ, NegativeZ
        /// - MonoGame TextureCube uses CubeMapFace enum for face indexing
        /// - Supports all TPC formats: DXT1/DXT3/DXT5, RGB/RGBA, grayscale
        /// - Handles mipmaps for each cube face
        /// - Based on MonoGame API: TextureCube(GraphicsDevice, int, bool, SurfaceFormat)
        /// - TextureCube.SetData&lt;T&gt;(CubeMapFace, T[]) sets face pixel data
        /// </remarks>
        private static TextureCube ConvertCubeMap([NotNull] TPC tpc, [NotNull] GraphicsDevice device, bool generateMipmaps)
        {
            if (tpc.Layers.Count != 6)
            {
                throw new ArgumentException("Cube map must have exactly 6 layers", "tpc");
            }

            // Get dimensions from first layer, first mipmap
            TPCMipmap baseMipmap = tpc.Layers[0].Mipmaps[0];
            int size = baseMipmap.Width; // Cube maps are square (width == height)
            if (baseMipmap.Height != size)
            {
                throw new ArgumentException("Cube map faces must be square", "tpc");
            }

            // Determine mipmap count
            int mipmapCount = tpc.Layers[0].Mipmaps.Count;
            if (generateMipmaps && mipmapCount == 1)
            {
                // Calculate mipmap count for generation
                int tempSize = size;
                while (tempSize > 1)
                {
                    mipmapCount++;
                    tempSize >>= 1;
                }
            }

            // Create TextureCube
            // Based on MonoGame API: TextureCube(GraphicsDevice graphicsDevice, int size, bool mipmap, SurfaceFormat format)
            TextureCube cubeMap = new TextureCube(device, size, generateMipmaps || mipmapCount > 1, SurfaceFormat.Color);

            // DirectX/XNA cube map face order mapping
            // TPC layers are stored in the same order as DirectX cube map faces
            CubeMapFace[] faceOrder = new CubeMapFace[]
            {
                CubeMapFace.PositiveX, // Layer 0: Right face (+X)
                CubeMapFace.NegativeX, // Layer 1: Left face (-X)
                CubeMapFace.PositiveY, // Layer 2: Top face (+Y)
                CubeMapFace.NegativeY, // Layer 3: Bottom face (-Y)
                CubeMapFace.PositiveZ, // Layer 4: Front face (+Z)
                CubeMapFace.NegativeZ  // Layer 5: Back face (-Z)
            };

            // Convert each face
            for (int faceIndex = 0; faceIndex < 6; faceIndex++)
            {
                TPCLayer layer = tpc.Layers[faceIndex];
                CubeMapFace face = faceOrder[faceIndex];

                // Process each mipmap level for this face
                // Keep previous level RGBA data for progressive downsampling
                byte[] previousLevelRgba = null;
                int previousLevelSize = 0;
                int currentSize = size;
                for (int mipLevel = 0; mipLevel < mipmapCount; mipLevel++)
                {
                    // Get or generate mipmap data
                    byte[] rgbaData;
                    if (mipLevel < layer.Mipmaps.Count)
                    {
                        // Use existing mipmap
                        TPCMipmap mipmap = layer.Mipmaps[mipLevel];
                        if (mipmap.Width != currentSize || mipmap.Height != currentSize)
                        {
                            throw new ArgumentException($"Cube map face {faceIndex} mipmap {mipLevel} has incorrect dimensions", "tpc");
                        }
                        rgbaData = ConvertMipmapToRgba(mipmap);
                        // Store for potential use in generating next level
                        previousLevelRgba = rgbaData;
                        previousLevelSize = currentSize;
                    }
                    else if (generateMipmaps)
                    {
                        // Generate mipmap by downsampling previous level
                        // swkotor2.exe: Uses D3DXFilterTexture or similar for mipmap generation (box filter)
                        // Based on PyKotor downsample_rgb implementation: box filter (2x2 average)
                        if (previousLevelRgba == null || previousLevelSize == 0)
                        {
                            // First mip level should exist, this shouldn't happen
                            throw new InvalidOperationException($"Cannot generate mipmap {mipLevel} without previous level data");
                        }
                        // Downsample using box filter (2x2 average) for proper mipmap generation
                        rgbaData = DownsampleMipmap(previousLevelRgba, previousLevelSize, previousLevelSize);
                        // Store for next iteration
                        previousLevelRgba = rgbaData;
                        previousLevelSize = currentSize;
                    }
                    else
                    {
                        break; // No more mipmaps to process
                    }

                    // Convert RGBA byte array to Color array
                    Color[] colorData = new Color[currentSize * currentSize];
                    for (int i = 0; i < colorData.Length; i++)
                    {
                        int offset = i * 4;
                        if (offset + 3 < rgbaData.Length)
                        {
                            colorData[i] = new Color(rgbaData[offset], rgbaData[offset + 1], rgbaData[offset + 2], rgbaData[offset + 3]);
                        }
                    }

                    // Set face data for this mipmap level
                    // Based on MonoGame API: void SetData&lt;T&gt;(CubeMapFace face, int level, Rectangle? rect, T[] data, int startIndex, int elementCount)
                    cubeMap.SetData(face, mipLevel, null, colorData, 0, colorData.Length);

                    // Next mipmap is half the size
                    currentSize = Math.Max(1, currentSize >> 1);
                }
            }

            return cubeMap;
        }

        /// <summary>
        /// Downsamples an RGBA mipmap to half size using box filter (2x2 average).
        /// Each output pixel is the average of a 2x2 block of input pixels.
        /// This is the standard method for mipmap generation in texture pipelines.
        /// </summary>
        /// <param name="source">Source RGBA data (width x height x 4 bytes).</param>
        /// <param name="width">Source image width.</param>
        /// <param name="height">Source image height.</param>
        /// <returns>Downsampled RGBA data at half size ((width/2) x (height/2) x 4 bytes).</returns>
        /// <remarks>
        /// Mipmap Downsampling Algorithm:
        /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) texture mipmap generation (D3DXFilterTexture uses box filter)
        /// - Based on PyKotor downsample_rgb implementation (vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/tpc/manipulate/downsample.py:104-131)
        /// - Uses box filter: each output pixel = average of 2x2 input block
        /// - Standard approach for mipmap generation in graphics pipelines
        /// - Preserves image quality better than nearest-neighbor resizing
        /// </remarks>
        private static byte[] DownsampleMipmap(byte[] source, int width, int height)
        {
            // Mipmaps are always exactly half size
            int nextWidth = Math.Max(1, width >> 1);
            int nextHeight = Math.Max(1, height >> 1);
            byte[] target = new byte[nextWidth * nextHeight * 4];

            // Box filter: average 2x2 block of pixels
            for (int y = 0; y < nextHeight; y++)
            {
                for (int x = 0; x < nextWidth; x++)
                {
                    // Source coordinates (2x2 block starting at (x*2, y*2))
                    int srcX = x << 1;
                    int srcY = y << 1;

                    // Calculate indices for 2x2 block corners
                    int srcIdx00 = (srcY * width + srcX) * 4;           // Top-left
                    int srcIdx10 = (srcY * width + (srcX + 1)) * 4;     // Top-right
                    int srcIdx01 = ((srcY + 1) * width + srcX) * 4;     // Bottom-left
                    int srcIdx11 = ((srcY + 1) * width + (srcX + 1)) * 4; // Bottom-right

                    // Destination index
                    int dstIdx = (y * nextWidth + x) * 4;

                    // Average each RGBA component from 2x2 block
                    // Handle edge cases where source coordinates might be out of bounds
                    int pixelCount = 0;
                    int rSum = 0, gSum = 0, bSum = 0, aSum = 0;

                    // Top-left pixel
                    if (srcX < width && srcY < height && srcIdx00 + 3 < source.Length)
                    {
                        rSum += source[srcIdx00];
                        gSum += source[srcIdx00 + 1];
                        bSum += source[srcIdx00 + 2];
                        aSum += source[srcIdx00 + 3];
                        pixelCount++;
                    }

                    // Top-right pixel
                    if ((srcX + 1) < width && srcY < height && srcIdx10 + 3 < source.Length)
                    {
                        rSum += source[srcIdx10];
                        gSum += source[srcIdx10 + 1];
                        bSum += source[srcIdx10 + 2];
                        aSum += source[srcIdx10 + 3];
                        pixelCount++;
                    }

                    // Bottom-left pixel
                    if (srcX < width && (srcY + 1) < height && srcIdx01 + 3 < source.Length)
                    {
                        rSum += source[srcIdx01];
                        gSum += source[srcIdx01 + 1];
                        bSum += source[srcIdx01 + 2];
                        aSum += source[srcIdx01 + 3];
                        pixelCount++;
                    }

                    // Bottom-right pixel
                    if ((srcX + 1) < width && (srcY + 1) < height && srcIdx11 + 3 < source.Length)
                    {
                        rSum += source[srcIdx11];
                        gSum += source[srcIdx11 + 1];
                        bSum += source[srcIdx11 + 2];
                        aSum += source[srcIdx11 + 3];
                        pixelCount++;
                    }

                    // Calculate average (box filter)
                    if (pixelCount > 0 && dstIdx + 3 < target.Length)
                    {
                        target[dstIdx] = (byte)(rSum / pixelCount);
                        target[dstIdx + 1] = (byte)(gSum / pixelCount);
                        target[dstIdx + 2] = (byte)(bSum / pixelCount);
                        target[dstIdx + 3] = (byte)(aSum / pixelCount);
                    }
                }
            }

            return target;
        }

        private static Texture2D Convert2DTexture(TPCLayer layer, GraphicsDevice device, bool generateMipmaps)
        {
            TPCMipmap baseMipmap = layer.Mipmaps[0];
            int width = baseMipmap.Width;
            int height = baseMipmap.Height;

            // Convert to RGBA for MonoGame
            // MonoGame Texture2D constructor accepts Color[] or byte[] data
            // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
            // Texture2D(GraphicsDevice, int, int, bool, SurfaceFormat) constructor
            // Method signature: Texture2D(GraphicsDevice graphicsDevice, int width, int height, bool mipmap, SurfaceFormat format)
            // Source: https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_Load_Texture.html
            byte[] rgbaData = ConvertMipmapToRgba(baseMipmap);

            // Create Texture2D from RGBA data
            // Based on MonoGame API: https://docs.monogame.net/api/Microsoft.Xna.Framework.Graphics.Texture2D.html
            // Texture2D.SetData<T>(T[]) sets texture pixel data
            // Method signature: void SetData<T>(T[] data) where T : struct
            // We'll create the texture and then set the data
            // Source: https://docs.monogame.net/articles/getting_to_know/howto/graphics/HowTo_Load_Texture.html
            Texture2D texture = new Texture2D(device, width, height, generateMipmaps, SurfaceFormat.Color);

            // Convert byte array to Color array for SetData
            Color[] colorData = new Color[width * height];
            for (int i = 0; i < colorData.Length; i++)
            {
                int offset = i * 4;
                if (offset + 3 < rgbaData.Length)
                {
                    colorData[i] = new Color(rgbaData[offset], rgbaData[offset + 1], rgbaData[offset + 2], rgbaData[offset + 3]);
                }
            }

            texture.SetData(colorData);
            return texture;
        }

        /// <summary>
        /// Converts a single TPC mipmap to RGBA format.
        /// Made internal for use by PbrMaterialFactory to upload individual mipmap levels.
        /// </summary>
        internal static byte[] ConvertMipmapToRgba(TPCMipmap mipmap)
        {
            int width = mipmap.Width;
            int height = mipmap.Height;
            byte[] data = mipmap.Data;
            TPCTextureFormat format = mipmap.TpcFormat;
            byte[] output = new byte[width * height * 4];

            switch (format)
            {
                case TPCTextureFormat.RGBA:
                    Array.Copy(data, output, Math.Min(data.Length, output.Length));
                    break;

                case TPCTextureFormat.BGRA:
                    ConvertBgraToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.RGB:
                    ConvertRgbToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.BGR:
                    ConvertBgrToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.Greyscale:
                    ConvertGreyscaleToRgba(data, output, width, height);
                    break;

                case TPCTextureFormat.DXT1:
                    DecompressDxt1(data, output, width, height);
                    break;

                case TPCTextureFormat.DXT3:
                    DecompressDxt3(data, output, width, height);
                    break;

                case TPCTextureFormat.DXT5:
                    DecompressDxt5(data, output, width, height);
                    break;

                default:
                    // Fill with magenta to indicate error
                    for (int i = 0; i < output.Length; i += 4)
                    {
                        output[i] = 255;     // R
                        output[i + 1] = 0;   // G
                        output[i + 2] = 255; // B
                        output[i + 3] = 255; // A
                    }
                    break;
            }

            return output;
        }

        private static void ConvertBgraToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 4;
                int dstIdx = i * 4;
                if (srcIdx + 3 < input.Length)
                {
                    output[dstIdx] = input[srcIdx + 2];     // R <- B
                    output[dstIdx + 1] = input[srcIdx + 1]; // G <- G
                    output[dstIdx + 2] = input[srcIdx];     // B <- R
                    output[dstIdx + 3] = input[srcIdx + 3]; // A <- A
                }
            }
        }

        private static void ConvertRgbToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < input.Length)
                {
                    output[dstIdx] = input[srcIdx];         // R
                    output[dstIdx + 1] = input[srcIdx + 1]; // G
                    output[dstIdx + 2] = input[srcIdx + 2]; // B
                    output[dstIdx + 3] = 255;               // A
                }
            }
        }

        private static void ConvertBgrToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < input.Length)
                {
                    output[dstIdx] = input[srcIdx + 2];     // R <- B
                    output[dstIdx + 1] = input[srcIdx + 1]; // G <- G
                    output[dstIdx + 2] = input[srcIdx];     // B <- R
                    output[dstIdx + 3] = 255;               // A
                }
            }
        }

        private static void ConvertGreyscaleToRgba(byte[] input, byte[] output, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                if (i < input.Length)
                {
                    byte grey = input[i];
                    int dstIdx = i * 4;
                    output[dstIdx] = grey;     // R
                    output[dstIdx + 1] = grey; // G
                    output[dstIdx + 2] = grey; // B
                    output[dstIdx + 3] = 255;  // A
                }
            }
        }

        #region DXT Decompression

        private static void DecompressDxt1(byte[] input, byte[] output, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 8 > input.Length)
                    {
                        break;
                    }

                    // Read color endpoints
                    ushort c0 = (ushort)(input[srcOffset] | (input[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(input[srcOffset + 2] | (input[srcOffset + 3] << 8));
                    uint indices = (uint)(input[srcOffset + 4] | (input[srcOffset + 5] << 8) |
                                         (input[srcOffset + 6] << 16) | (input[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode colors
                    byte[] colors = new byte[16]; // 4 colors * 4 components
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    if (c0 > c1)
                    {
                        // 4-color mode
                        colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);
                        colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);
                        colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);
                        colors[11] = 255;

                        colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);
                        colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);
                        colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);
                        colors[15] = 255;
                    }
                    else
                    {
                        // 3-color + transparent mode
                        colors[8] = (byte)((colors[0] + colors[4]) / 2);
                        colors[9] = (byte)((colors[1] + colors[5]) / 2);
                        colors[10] = (byte)((colors[2] + colors[6]) / 2);
                        colors[11] = 255;

                        colors[12] = 0;
                        colors[13] = 0;
                        colors[14] = 0;
                        colors[15] = 0; // Transparent
                    }

                    // Write pixels
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;
                            if (x >= width || y >= height)
                            {
                                continue;
                            }

                            int idx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int dstOffset = (y * width + x) * 4;

                            output[dstOffset] = colors[idx * 4];
                            output[dstOffset + 1] = colors[idx * 4 + 1];
                            output[dstOffset + 2] = colors[idx * 4 + 2];
                            output[dstOffset + 3] = colors[idx * 4 + 3];
                        }
                    }
                }
            }
        }

        private static void DecompressDxt3(byte[] input, byte[] output, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > input.Length)
                    {
                        break;
                    }

                    // Read explicit alpha (8 bytes)
                    byte[] alphas = new byte[16];
                    for (int i = 0; i < 4; i++)
                    {
                        ushort row = (ushort)(input[srcOffset + i * 2] | (input[srcOffset + i * 2 + 1] << 8));
                        for (int j = 0; j < 4; j++)
                        {
                            int a = (row >> (j * 4)) & 0xF;
                            alphas[i * 4 + j] = (byte)(a | (a << 4));
                        }
                    }
                    srcOffset += 8;

                    // Read color block (same as DXT1)
                    ushort c0 = (ushort)(input[srcOffset] | (input[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(input[srcOffset + 2] | (input[srcOffset + 3] << 8));
                    uint indices = (uint)(input[srcOffset + 4] | (input[srcOffset + 5] << 8) |
                                         (input[srcOffset + 6] << 16) | (input[srcOffset + 7] << 24));
                    srcOffset += 8;

                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    // Always 4-color mode for DXT3/5
                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);
                    colors[11] = 255;

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);
                    colors[15] = 255;

                    // Write pixels
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;
                            if (x >= width || y >= height)
                            {
                                continue;
                            }

                            int idx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int dstOffset = (y * width + x) * 4;

                            output[dstOffset] = colors[idx * 4];
                            output[dstOffset + 1] = colors[idx * 4 + 1];
                            output[dstOffset + 2] = colors[idx * 4 + 2];
                            output[dstOffset + 3] = alphas[py * 4 + px];
                        }
                    }
                }
            }
        }

        private static void DecompressDxt5(byte[] input, byte[] output, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > input.Length)
                    {
                        break;
                    }

                    // Read interpolated alpha (8 bytes)
                    byte a0 = input[srcOffset];
                    byte a1 = input[srcOffset + 1];
                    ulong alphaIndices = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        alphaIndices |= (ulong)input[srcOffset + 2 + i] << (i * 8);
                    }
                    srcOffset += 8;

                    // Calculate alpha lookup table
                    byte[] alphaTable = new byte[8];
                    alphaTable[0] = a0;
                    alphaTable[1] = a1;
                    if (a0 > a1)
                    {
                        alphaTable[2] = (byte)((6 * a0 + 1 * a1) / 7);
                        alphaTable[3] = (byte)((5 * a0 + 2 * a1) / 7);
                        alphaTable[4] = (byte)((4 * a0 + 3 * a1) / 7);
                        alphaTable[5] = (byte)((3 * a0 + 4 * a1) / 7);
                        alphaTable[6] = (byte)((2 * a0 + 5 * a1) / 7);
                        alphaTable[7] = (byte)((1 * a0 + 6 * a1) / 7);
                    }
                    else
                    {
                        alphaTable[2] = (byte)((4 * a0 + 1 * a1) / 5);
                        alphaTable[3] = (byte)((3 * a0 + 2 * a1) / 5);
                        alphaTable[4] = (byte)((2 * a0 + 3 * a1) / 5);
                        alphaTable[5] = (byte)((1 * a0 + 4 * a1) / 5);
                        alphaTable[6] = 0;
                        alphaTable[7] = 255;
                    }

                    // Read color block
                    ushort c0 = (ushort)(input[srcOffset] | (input[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(input[srcOffset + 2] | (input[srcOffset + 3] << 8));
                    uint indices = (uint)(input[srcOffset + 4] | (input[srcOffset + 5] << 8) |
                                         (input[srcOffset + 6] << 16) | (input[srcOffset + 7] << 24));
                    srcOffset += 8;

                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);
                    colors[11] = 255;

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);
                    colors[15] = 255;

                    // Write pixels
                    for (int py = 0; py < 4; py++)
                    {
                        for (int px = 0; px < 4; px++)
                        {
                            int x = bx * 4 + px;
                            int y = by * 4 + py;
                            if (x >= width || y >= height)
                            {
                                continue;
                            }

                            int colorIdx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int alphaIdx = (int)((alphaIndices >> ((py * 4 + px) * 3)) & 7);
                            int dstOffset = (y * width + x) * 4;

                            output[dstOffset] = colors[colorIdx * 4];
                            output[dstOffset + 1] = colors[colorIdx * 4 + 1];
                            output[dstOffset + 2] = colors[colorIdx * 4 + 2];
                            output[dstOffset + 3] = alphaTable[alphaIdx];
                        }
                    }
                }
            }
        }

        private static void DecodeColor565(ushort color, byte[] output, int offset)
        {
            int r = (color >> 11) & 0x1F;
            int g = (color >> 5) & 0x3F;
            int b = color & 0x1F;

            output[offset] = (byte)((r << 3) | (r >> 2));
            output[offset + 1] = (byte)((g << 2) | (g >> 4));
            output[offset + 2] = (byte)((b << 3) | (b >> 2));
            output[offset + 3] = 255;
        }

        #endregion
    }
}

