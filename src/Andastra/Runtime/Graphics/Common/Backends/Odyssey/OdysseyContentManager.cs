using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Formats.TPC;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Graphics;

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
                // TODO: Check if resource exists in ERF/BIF
                return true;
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
                        // TODO: Convert TPC/TGA data to texture
                        // This requires the OdysseyGraphicsDevice to create textures
                        return null;
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
                            // TODO: Load texture from file
                            return null;
                        }
                    }
                }
                
                // Try direct path
                if (File.Exists(fullPath))
                {
                    // TODO: Load based on file extension
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OdysseyContentManager] Failed to load '{assetName}' from file system: {ex.Message}");
            }
            
            return null;
        }
        
        #endregion
    }
}

