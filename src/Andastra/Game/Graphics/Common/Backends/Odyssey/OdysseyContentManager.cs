using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Backends.Odyssey;

namespace Andastra.Runtime.Graphics.Common.Backends.Odyssey
{
    /// <summary>
    /// Odyssey engine content manager implementation.
    /// Loads assets from KOTOR's ERF/BIF archives via CExoKeyTable pattern.
    /// </summary>
    /// <remarks>
    /// Odyssey Content Manager:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game content system: CExoResMan, CExoKeyTable, ERF/BIF archives
    /// - Located via string references: "Resource" @ 0x007c14d4, "Loading" @ 0x007c7e40
    /// - CExoKeyTable @ 0x007b6078, FUN_00633270 @ 0x00633270 (resource path resolution)
    /// - This implementation: Wraps resource provider for IContentManager interface
    /// </remarks>
    public class OdysseyContentManager : IContentManager
    {
        private string _rootDirectory;
        private readonly IGameResourceProvider _resourceProvider;
        private readonly Dictionary<string, object> _cache;
        private bool _disposed;
        private IGraphicsDevice _graphicsDevice;

        /// <summary>
        /// Creates a new Odyssey content manager.
        /// </summary>
        /// <param name="resourceProvider">Game resource provider for loading assets from ERF/BIF.</param>
        public OdysseyContentManager(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider;
            _cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _rootDirectory = string.Empty;
        }

        /// <summary>
        /// Creates a new Odyssey content manager with a root directory.
        /// </summary>
        /// <param name="rootDirectory">Root directory for content.</param>
        public OdysseyContentManager(string rootDirectory)
        {
            _resourceProvider = null;
            _cache = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            _rootDirectory = rootDirectory ?? string.Empty;
        }

        /// <summary>
        /// Sets the graphics device for texture creation.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device to use for creating textures.</param>
        public void SetGraphicsDevice(IGraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Gets or sets the root directory for content.
        /// </summary>
        public string RootDirectory
        {
            get { return _rootDirectory; }
            set { _rootDirectory = value ?? string.Empty; }
        }

        /// <summary>
        /// Loads an asset of the specified type.
        /// Based on swkotor.exe/swkotor2.exe: CExoResMan resource loading
        /// </summary>
        /// <typeparam name="T">Asset type.</typeparam>
        /// <param name="assetName">Asset name (without extension).</param>
        /// <returns>Loaded asset.</returns>
        public T Load<T>(string assetName) where T : class
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OdysseyContentManager));
            }

            if (string.IsNullOrEmpty(assetName))
            {
                throw new ArgumentNullException(nameof(assetName));
            }

            // Check cache first
            string cacheKey = typeof(T).Name + ":" + assetName;
            if (_cache.TryGetValue(cacheKey, out object cached))
            {
                return cached as T;
            }

            // Try to load from resource provider if available
            if (_resourceProvider != null)
            {
                T asset = LoadFromResourceProvider<T>(assetName);
                if (asset != null)
                {
                    _cache[cacheKey] = asset;
                    return asset;
                }
            }

            // Try to load from file system
            T fileAsset = LoadFromFileSystem<T>(assetName);
            if (fileAsset != null)
            {
                _cache[cacheKey] = fileAsset;
                return fileAsset;
            }

            throw new FileNotFoundException($"Asset not found: {assetName}");
        }

        /// <summary>
        /// Unloads all content.
        /// </summary>
        public void Unload()
        {
            foreach (var item in _cache.Values)
            {
                if (item is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _cache.Clear();
        }

        /// <summary>
        /// Checks if an asset exists.
        /// </summary>
        /// <param name="assetName">Asset name.</param>
        /// <returns>True if asset exists.</returns>
        public bool AssetExists(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            // Check resource provider
            if (_resourceProvider != null)
            {
                // Check common resource types in ERF/BIF archives
                // Try TPC first, then TGA, then MDL, then other formats
                ResourceType[] typesToCheck = new[]
                {
                    ResourceType.TPC,
                    ResourceType.TGA,
                    ResourceType.MDL,
                    ResourceType.MDX,
                    ResourceType.WAV,
                    ResourceType.TwoDA,
                    ResourceType.GFF,
                    ResourceType.DLG,
                    ResourceType.UTC,
                    ResourceType.UTD,
                    ResourceType.UTP,
                    ResourceType.UTI,
                    ResourceType.UTS,
                    ResourceType.UTT,
                    ResourceType.UTW,
                    ResourceType.UTE,
                    ResourceType.UTM,
                    ResourceType.ARE,
                    ResourceType.GIT,
                    ResourceType.IFO
                };

                foreach (var resType in typesToCheck)
                {
                    if (ResourceExistsSync(assetName, resType))
                    {
                        return true;
                    }
                }

                return false;
            }

            // Check file system
            string fullPath = Path.Combine(_rootDirectory, assetName);
            return File.Exists(fullPath) ||
                   File.Exists(fullPath + ".tpc") ||
                   File.Exists(fullPath + ".tga") ||
                   File.Exists(fullPath + ".mdl") ||
                   File.Exists(fullPath + ".mdx");
        }

        /// <summary>
        /// Synchronously checks if a resource exists using the async IGameResourceProvider.
        /// </summary>
        private bool ResourceExistsSync(string resRef, ResourceType type)
        {
            if (_resourceProvider == null)
            {
                return false;
            }

            try
            {
                var id = new ResourceIdentifier(resRef, type);

                // Use GetAwaiter().GetResult() to run async method synchronously
                // NOTE: This is not ideal but necessary for synchronous existence checks
                return _resourceProvider.ExistsAsync(id, System.Threading.CancellationToken.None)
                    .GetAwaiter().GetResult();
            }
            catch (Exception)
            {
                // If the check fails, assume the resource doesn't exist
                return false;
            }
        }

        /// <summary>
        /// Disposes the content manager.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Unload();
                _disposed = true;
            }
        }

        #region Private Loading Methods

        private T LoadFromResourceProvider<T>(string assetName) where T : class
        {
            if (_resourceProvider == null)
            {
                return null;
            }

            // Try to determine resource type from T
            try
            {
                if (typeof(T) == typeof(ITexture2D) || typeof(T).Name.Contains("Texture"))
                {
                    // Load TPC/TGA texture using async resource provider
                    // NOTE: IGameResourceProvider uses async methods - we need to run synchronously here
                    byte[] data = LoadResourceDataSync(assetName, ResourceType.TPC);
                    if (data == null || data.Length == 0)
                    {
                        data = LoadResourceDataSync(assetName, ResourceType.TGA);
                    }

                    if (data != null && data.Length > 0)
                    {
                        // Convert TPC/TGA data to texture
                        // Based on swkotor2.exe texture loading system
                        // Based on xoreos: texture.cpp texture loading
                        // Based on PyKotor: texture_loader.py load_tpc
                        ITexture2D texture = ConvertTpcTgaToTexture(data);
                        if (texture != null)
                        {
                            return texture as T;
                        }
                    }
                }
                // TODO: Handle other resource types (MDL, WAV, etc.)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyContentManager] Failed to load '{assetName}' from resource provider: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Synchronously loads resource data from the async IGameResourceProvider.
        /// </summary>
        private byte[] LoadResourceDataSync(string resRef, ResourceType type)
        {
            if (_resourceProvider == null)
            {
                return null;
            }

            try
            {
                var id = new ResourceIdentifier(resRef, type);

                // Use GetAwaiter().GetResult() to run async method synchronously
                // NOTE: This is not ideal but necessary for synchronous content loading
                var stream = _resourceProvider.OpenResourceAsync(id, System.Threading.CancellationToken.None)
                    .GetAwaiter().GetResult();

                if (stream == null)
                {
                    return null;
                }

                using (stream)
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyContentManager] LoadResourceDataSync failed for '{resRef}.{type}': {ex.Message}");
                return null;
            }
        }

        private T LoadFromFileSystem<T>(string assetName) where T : class
        {
            string fullPath = Path.Combine(_rootDirectory, assetName);

            try
            {
                // Try different extensions based on type
                if (typeof(T) == typeof(ITexture2D) || typeof(T).Name.Contains("Texture"))
                {
                    string[] extensions = new[] { ".tpc", ".tga", ".png", ".jpg", ".bmp" };
                    foreach (var ext in extensions)
                    {
                        string texturePath = fullPath + ext;
                        if (File.Exists(texturePath))
                        {
                            // Load texture from file
                            byte[] fileData = File.ReadAllBytes(texturePath);
                            ITexture2D texture = ConvertTpcTgaToTexture(fileData);
                            if (texture != null)
                            {
                                return texture as T;
                            }
                        }
                    }
                }

                // Try direct path
                if (File.Exists(fullPath))
                {
                    // Load based on file extension
                    string ext = Path.GetExtension(fullPath).ToLowerInvariant();
                    if (ext == ".tpc" || ext == ".tga" || ext == ".png" || ext == ".jpg" || ext == ".bmp")
                    {
                        byte[] fileData = File.ReadAllBytes(fullPath);
                        ITexture2D texture = ConvertTpcTgaToTexture(fileData);
                        if (texture != null)
                        {
                            return texture as T;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyContentManager] Failed to load '{assetName}' from file system: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Converts TPC/TGA data to an ITexture2D.
        /// Handles all TPC formats: RGBA, RGB, BGRA, BGR, Greyscale, DXT1, DXT3, DXT5.
        /// Based on swkotor2.exe texture loading system
        /// Based on xoreos: texture.cpp texture loading
        /// Based on PyKotor: texture_loader.py load_tpc
        /// </summary>
        /// <param name="data">TPC or TGA file data.</param>
        /// <returns>Created texture, or null if conversion failed.</returns>
        private ITexture2D ConvertTpcTgaToTexture(byte[] data)
        {
            if (data == null || data.Length == 0 || _graphicsDevice == null)
            {
                return null;
            }

            try
            {
                // Parse TPC/TGA data
                // TPCAuto.ReadTpc handles both TPC and TGA formats automatically
                TPC tpc = TPCAuto.ReadTpc(data);
                if (tpc == null || tpc.Layers.Count == 0 || tpc.Layers[0].Mipmaps.Count == 0)
                {
                    Console.WriteLine("[OdysseyContentManager] Invalid TPC/TGA data: no layers or mipmaps");
                    return null;
                }

                // Get first mipmap from first layer
                TPCMipmap mipmap = tpc.Layers[0].Mipmaps[0];
                int width = mipmap.Width;
                int height = mipmap.Height;

                // Convert mipmap to RGBA format
                // Based on TPCTGAWriter.DecodeMipmapToRgba implementation
                byte[] rgbaData = ConvertMipmapToRgba(mipmap);
                if (rgbaData == null || rgbaData.Length == 0)
                {
                    Console.WriteLine("[OdysseyContentManager] Failed to convert mipmap to RGBA");
                    return null;
                }

                // Create texture using graphics device
                // Based on OdysseyGraphicsDevice.CreateTexture2D
                if (_graphicsDevice is OdysseyGraphicsDevice odysseyDevice)
                {
                    return odysseyDevice.CreateTexture2D(width, height, rgbaData);
                }

                Console.WriteLine("[OdysseyContentManager] Graphics device is not an OdysseyGraphicsDevice");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyContentManager] Failed to convert TPC/TGA to texture: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts a TPC mipmap to RGBA format.
        /// Handles all TPC texture formats: RGBA, RGB, BGRA, BGR, Greyscale, DXT1, DXT3, DXT5.
        /// Based on TPCTGAWriter.DecodeMipmapToRgba implementation
        /// Based on PyKotor: tpc_data.py _decode_mipmap_to_rgba
        /// </summary>
        /// <param name="mipmap">The mipmap to convert.</param>
        /// <returns>RGBA pixel data as byte array (width * height * 4 bytes).</returns>
        private byte[] ConvertMipmapToRgba(TPCMipmap mipmap)
        {
            if (mipmap == null)
            {
                return null;
            }

            int width = mipmap.Width;
            int height = mipmap.Height;
            byte[] data = mipmap.Data;
            TPCTextureFormat format = mipmap.TpcFormat;
            byte[] rgba = new byte[width * height * 4];

            switch (format)
            {
                case TPCTextureFormat.RGBA:
                    // Already in RGBA format - just copy
                    Array.Copy(data, rgba, Math.Min(data.Length, rgba.Length));
                    break;

                case TPCTextureFormat.RGB:
                    // Convert RGB to RGBA (add alpha channel with value 255)
                    ConvertRgbToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.BGRA:
                    // Convert BGRA to RGBA (swap R and B channels)
                    ConvertBgraToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.BGR:
                    // Convert BGR to RGBA (swap R and B, add alpha)
                    ConvertBgrToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.Greyscale:
                    // Convert greyscale to RGBA (replicate grey value to RGB, add alpha)
                    ConvertGreyscaleToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.DXT1:
                    // Decompress DXT1 to RGBA
                    DecompressDxt1ToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.DXT3:
                    // Decompress DXT3 to RGBA
                    DecompressDxt3ToRgba(data, rgba, width, height);
                    break;

                case TPCTextureFormat.DXT5:
                    // Decompress DXT5 to RGBA
                    DecompressDxt5ToRgba(data, rgba, width, height);
                    break;

                default:
                    // Unknown format - fill with magenta to indicate error
                    for (int i = 0; i < rgba.Length; i += 4)
                    {
                        rgba[i] = 255;     // R
                        rgba[i + 1] = 0;   // G
                        rgba[i + 2] = 255; // B
                        rgba[i + 3] = 255; // A
                    }
                    Console.WriteLine($"[OdysseyContentManager] Unknown TPC format: {format}, filling with magenta");
                    break;
            }

            return rgba;
        }

        /// <summary>
        /// Converts RGB data to RGBA format by adding an alpha channel (255).
        /// </summary>
        private void ConvertRgbToRgba(byte[] rgb, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < rgb.Length && dstIdx + 3 < rgba.Length)
                {
                    rgba[dstIdx] = rgb[srcIdx];         // R
                    rgba[dstIdx + 1] = rgb[srcIdx + 1]; // G
                    rgba[dstIdx + 2] = rgb[srcIdx + 2]; // B
                    rgba[dstIdx + 3] = 255;             // A (full opacity)
                }
            }
        }

        /// <summary>
        /// Converts BGRA data to RGBA format by swapping R and B channels.
        /// </summary>
        private void ConvertBgraToRgba(byte[] bgra, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 4;
                int dstIdx = i * 4;
                if (srcIdx + 3 < bgra.Length && dstIdx + 3 < rgba.Length)
                {
                    rgba[dstIdx] = bgra[srcIdx + 2];     // R <- B
                    rgba[dstIdx + 1] = bgra[srcIdx + 1]; // G <- G
                    rgba[dstIdx + 2] = bgra[srcIdx];     // B <- R
                    rgba[dstIdx + 3] = bgra[srcIdx + 3]; // A <- A
                }
            }
        }

        /// <summary>
        /// Converts BGR data to RGBA format by swapping R and B channels and adding alpha.
        /// </summary>
        private void ConvertBgrToRgba(byte[] bgr, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                int srcIdx = i * 3;
                int dstIdx = i * 4;
                if (srcIdx + 2 < bgr.Length && dstIdx + 3 < rgba.Length)
                {
                    rgba[dstIdx] = bgr[srcIdx + 2];     // R <- B
                    rgba[dstIdx + 1] = bgr[srcIdx + 1]; // G <- G
                    rgba[dstIdx + 2] = bgr[srcIdx];     // B <- R
                    rgba[dstIdx + 3] = 255;             // A (full opacity)
                }
            }
        }

        /// <summary>
        /// Converts greyscale data to RGBA format by replicating the grey value to RGB channels.
        /// </summary>
        private void ConvertGreyscaleToRgba(byte[] grey, byte[] rgba, int width, int height)
        {
            int pixelCount = width * height;
            for (int i = 0; i < pixelCount; i++)
            {
                if (i < grey.Length)
                {
                    byte greyValue = grey[i];
                    int dstIdx = i * 4;
                    if (dstIdx + 3 < rgba.Length)
                    {
                        rgba[dstIdx] = greyValue;     // R
                        rgba[dstIdx + 1] = greyValue; // G
                        rgba[dstIdx + 2] = greyValue; // B
                        rgba[dstIdx + 3] = 255;       // A (full opacity)
                    }
                }
            }
        }

        /// <summary>
        /// Decompresses DXT1 (BC1) compressed texture data to RGBA format.
        /// Based on standard DXT/S3TC decompression algorithm.
        /// DXT1 uses 4x4 pixel blocks with 8 bytes per block (2 color endpoints + 16 2-bit indices).
        /// Based on TPCTGAWriter.DecompressDxt1ToRgba implementation
        /// </summary>
        private void DecompressDxt1ToRgba(byte[] dxt1, byte[] rgba, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 8 > dxt1.Length)
                    {
                        break;
                    }

                    // Read color endpoints (16-bit RGB565 format)
                    ushort c0 = (ushort)(dxt1[srcOffset] | (dxt1[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(dxt1[srcOffset + 2] | (dxt1[srcOffset + 3] << 8));
                    uint indices = (uint)(dxt1[srcOffset + 4] | (dxt1[srcOffset + 5] << 8) |
                                         (dxt1[srcOffset + 6] << 16) | (dxt1[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode color endpoints from RGB565
                    byte[] colors = new byte[16]; // 4 colors * 4 components (RGBA)
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    // Calculate interpolated colors
                    if (c0 > c1)
                    {
                        // 4-color mode: interpolate between c0 and c1
                        colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);   // R
                        colors[9] = (byte)((2 * colors[1] + colors[5]) / 3); // G
                        colors[10] = (byte)((2 * colors[2] + colors[6]) / 3); // B
                        colors[11] = 255; // A

                        colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);   // R
                        colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);   // G
                        colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);   // B
                        colors[15] = 255; // A
                    }
                    else
                    {
                        // 3-color + transparent mode
                        colors[8] = (byte)((colors[0] + colors[4]) / 2);   // R
                        colors[9] = (byte)((colors[1] + colors[5]) / 2);   // G
                        colors[10] = (byte)((colors[2] + colors[6]) / 2);   // B
                        colors[11] = 255; // A

                        colors[12] = 0; // R (transparent)
                        colors[13] = 0; // G
                        colors[14] = 0; // B
                        colors[15] = 0; // A (transparent)
                    }

                    // Write pixels for this 4x4 block
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

                            // Extract 2-bit color index for this pixel
                            int idx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int dstOffset = (y * width + x) * 4;

                            if (dstOffset + 3 < rgba.Length)
                            {
                                rgba[dstOffset] = colors[idx * 4];         // R
                                rgba[dstOffset + 1] = colors[idx * 4 + 1]; // G
                                rgba[dstOffset + 2] = colors[idx * 4 + 2]; // B
                                rgba[dstOffset + 3] = colors[idx * 4 + 3]; // A
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decompresses DXT3 (BC2) compressed texture data to RGBA format.
        /// Based on standard DXT/S3TC decompression algorithm.
        /// DXT3 uses 4x4 pixel blocks with 16 bytes per block (8 bytes explicit alpha + 8 bytes DXT1 color).
        /// Based on TPCTGAWriter.DecompressDxt3ToRgba implementation
        /// </summary>
        private void DecompressDxt3ToRgba(byte[] dxt3, byte[] rgba, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > dxt3.Length)
                    {
                        break;
                    }

                    // Read explicit alpha values (8 bytes, 4 bits per pixel)
                    byte[] alphas = new byte[16];
                    for (int i = 0; i < 4; i++)
                    {
                        ushort row = (ushort)(dxt3[srcOffset + i * 2] | (dxt3[srcOffset + i * 2 + 1] << 8));
                        for (int j = 0; j < 4; j++)
                        {
                            int a = (row >> (j * 4)) & 0xF;
                            // Expand 4-bit alpha to 8-bit: a * 17 (0x11) = a | (a << 4)
                            alphas[i * 4 + j] = (byte)(a | (a << 4));
                        }
                    }
                    srcOffset += 8;

                    // Read color block (same as DXT1)
                    ushort c0 = (ushort)(dxt3[srcOffset] | (dxt3[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(dxt3[srcOffset + 2] | (dxt3[srcOffset + 3] << 8));
                    uint indices = (uint)(dxt3[srcOffset + 4] | (dxt3[srcOffset + 5] << 8) |
                                         (dxt3[srcOffset + 6] << 16) | (dxt3[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode colors (always 4-color mode for DXT3/5)
                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);   // R
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);   // G
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);  // B
                    colors[11] = 255; // A

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);   // R
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);   // G
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);   // B
                    colors[15] = 255; // A

                    // Write pixels for this 4x4 block
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
                            int dstOffset = (y * width + x) * 4;

                            if (dstOffset + 3 < rgba.Length)
                            {
                                rgba[dstOffset] = colors[colorIdx * 4];         // R
                                rgba[dstOffset + 1] = colors[colorIdx * 4 + 1]; // G
                                rgba[dstOffset + 2] = colors[colorIdx * 4 + 2]; // B
                                rgba[dstOffset + 3] = alphas[py * 4 + px];      // A (from explicit alpha)
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decompresses DXT5 (BC3) compressed texture data to RGBA format.
        /// Based on standard DXT/S3TC decompression algorithm.
        /// DXT5 uses 4x4 pixel blocks with 16 bytes per block (8 bytes interpolated alpha + 8 bytes DXT1 color).
        /// Based on TPCTGAWriter.DecompressDxt5ToRgba implementation
        /// </summary>
        private void DecompressDxt5ToRgba(byte[] dxt5, byte[] rgba, int width, int height)
        {
            int blockCountX = (width + 3) / 4;
            int blockCountY = (height + 3) / 4;

            int srcOffset = 0;
            for (int by = 0; by < blockCountY; by++)
            {
                for (int bx = 0; bx < blockCountX; bx++)
                {
                    if (srcOffset + 16 > dxt5.Length)
                    {
                        break;
                    }

                    // Read alpha endpoints (1 byte each)
                    byte a0 = dxt5[srcOffset];
                    byte a1 = dxt5[srcOffset + 1];
                    srcOffset += 2;

                    // Read alpha indices (6 bytes, 3 bits per pixel)
                    ulong alphaIndices = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        alphaIndices |= ((ulong)dxt5[srcOffset + i]) << (i * 8);
                    }
                    srcOffset += 6;

                    // Calculate interpolated alpha values
                    byte[] alphas = new byte[8];
                    alphas[0] = a0;
                    alphas[1] = a1;
                    if (a0 > a1)
                    {
                        // 8-alpha mode: interpolate between a0 and a1
                        alphas[2] = (byte)((6 * a0 + 1 * a1) / 7);
                        alphas[3] = (byte)((5 * a0 + 2 * a1) / 7);
                        alphas[4] = (byte)((4 * a0 + 3 * a1) / 7);
                        alphas[5] = (byte)((3 * a0 + 4 * a1) / 7);
                        alphas[6] = (byte)((2 * a0 + 5 * a1) / 7);
                        alphas[7] = (byte)((1 * a0 + 6 * a1) / 7);
                    }
                    else
                    {
                        // 6-alpha mode: interpolate between a0 and a1, with 0 and 255
                        alphas[2] = (byte)((4 * a0 + 1 * a1) / 5);
                        alphas[3] = (byte)((3 * a0 + 2 * a1) / 5);
                        alphas[4] = (byte)((2 * a0 + 3 * a1) / 5);
                        alphas[5] = (byte)((1 * a0 + 4 * a1) / 5);
                        alphas[6] = 0;
                        alphas[7] = 255;
                    }

                    // Read color block (same as DXT1)
                    ushort c0 = (ushort)(dxt5[srcOffset] | (dxt5[srcOffset + 1] << 8));
                    ushort c1 = (ushort)(dxt5[srcOffset + 2] | (dxt5[srcOffset + 3] << 8));
                    uint indices = (uint)(dxt5[srcOffset + 4] | (dxt5[srcOffset + 5] << 8) |
                                         (dxt5[srcOffset + 6] << 16) | (dxt5[srcOffset + 7] << 24));
                    srcOffset += 8;

                    // Decode colors (always 4-color mode for DXT3/5)
                    byte[] colors = new byte[16];
                    DecodeColor565(c0, colors, 0);
                    DecodeColor565(c1, colors, 4);

                    colors[8] = (byte)((2 * colors[0] + colors[4]) / 3);   // R
                    colors[9] = (byte)((2 * colors[1] + colors[5]) / 3);   // G
                    colors[10] = (byte)((2 * colors[2] + colors[6]) / 3);  // B
                    colors[11] = 255; // A

                    colors[12] = (byte)((colors[0] + 2 * colors[4]) / 3);   // R
                    colors[13] = (byte)((colors[1] + 2 * colors[5]) / 3);   // G
                    colors[14] = (byte)((colors[2] + 2 * colors[6]) / 3);   // B
                    colors[15] = 255; // A

                    // Write pixels for this 4x4 block
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

                            // Extract 3-bit alpha index for this pixel
                            int alphaIdx = (int)((alphaIndices >> ((py * 4 + px) * 3)) & 7);
                            // Extract 2-bit color index for this pixel
                            int colorIdx = (int)((indices >> ((py * 4 + px) * 2)) & 3);
                            int dstOffset = (y * width + x) * 4;

                            if (dstOffset + 3 < rgba.Length)
                            {
                                rgba[dstOffset] = colors[colorIdx * 4];         // R
                                rgba[dstOffset + 1] = colors[colorIdx * 4 + 1]; // G
                                rgba[dstOffset + 2] = colors[colorIdx * 4 + 2]; // B
                                rgba[dstOffset + 3] = alphas[alphaIdx];        // A (from interpolated alpha)
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Decodes a 16-bit RGB565 color to RGBA format.
        /// </summary>
        /// <param name="color565">16-bit RGB565 color value.</param>
        /// <param name="output">Output array to write RGBA values to.</param>
        /// <param name="offset">Offset in output array to write to.</param>
        private void DecodeColor565(ushort color565, byte[] output, int offset)
        {
            // Extract RGB components from RGB565 format
            // RGB565: RRRRR GGGGGG BBBBB (5 bits R, 6 bits G, 5 bits B)
            byte r = (byte)(((color565 >> 11) & 0x1F) * 255 / 31);  // 5 bits -> 8 bits
            byte g = (byte)(((color565 >> 5) & 0x3F) * 255 / 63);   // 6 bits -> 8 bits
            byte b = (byte)((color565 & 0x1F) * 255 / 31);           // 5 bits -> 8 bits

            if (offset + 3 < output.Length)
            {
                output[offset] = r;     // R
                output[offset + 1] = g; // G
                output[offset + 2] = b; // B
                output[offset + 3] = 255; // A (full opacity)
            }
        }

        #endregion
    }
}

