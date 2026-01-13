using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.MonoGame.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IContentManager.
    /// </summary>
    public class MonoGameContentManager : IContentManager
    {
        private readonly ContentManager _contentManager;
        private readonly Dictionary<string, bool> _assetExistsCache;

        internal ContentManager ContentManager => _contentManager;

        public MonoGameContentManager(ContentManager contentManager)
        {
            _contentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
            _assetExistsCache = new Dictionary<string, bool>();
        }

        public string RootDirectory
        {
            get { return _contentManager.RootDirectory; }
            set { _contentManager.RootDirectory = value; }
        }

        public T Load<T>(string assetName) where T : class
        {
            if (typeof(IFont).IsAssignableFrom(typeof(T)))
            {
                var spriteFont = _contentManager.Load<Microsoft.Xna.Framework.Graphics.SpriteFont>(assetName);
                return new MonoGameFont(spriteFont) as T;
            }
            else if (typeof(ITexture2D).IsAssignableFrom(typeof(T)))
            {
                var texture = _contentManager.Load<Microsoft.Xna.Framework.Graphics.Texture2D>(assetName);
                return new MonoGameTexture2D(texture) as T;
            }
            else
            {
                return _contentManager.Load<T>(assetName);
            }
        }

        public void Unload()
        {
            _contentManager.Unload();
            // Clear cache when content is unloaded to maintain consistency
            // Assets may be removed or changed after Unload()
            _assetExistsCache.Clear();
        }

        /// <summary>
        /// Checks if an asset exists in the content manager.
        /// </summary>
        /// <remarks>
        /// MonoGame ContentManager doesn't provide a direct API to check asset existence.
        /// This implementation checks if the compiled .xnb file exists on disk, which is
        /// the standard MonoGame content format. The method includes:
        /// - Input validation (null/empty checks)
        /// - Path normalization (handles forward/backward slashes, relative paths)
        /// - Case-sensitive file checking on Unix platforms
        /// - Caching for performance (results are cached until Unload() is called)
        /// 
        /// Based on MonoGame ContentManager behavior:
        /// - Assets are compiled to .xnb files in the RootDirectory
        /// - Asset names use forward slashes as separators (e.g., "Fonts/Arial")
        /// - Paths are resolved relative to RootDirectory
        /// - MonoGame's Load&lt;T&gt; throws ContentLoadException if asset not found
        /// 
        /// Original game reference (swkotor2.exe):
        /// - CExoKeyTable @ 0x007b6078 - resource lookup table
        /// - FUN_00633270 @ 0x00633270 - resource path resolution
        /// - Original game checks ERF/BIF archives for resource existence
        /// - This implementation: Modern equivalent for MonoGame's file-based content system
        /// </remarks>
        /// <param name="assetName">Asset name (without extension, e.g., "Fonts/Arial").</param>
        /// <returns>True if asset exists, false otherwise.</returns>
        public bool AssetExists(string assetName)
        {
            // Validate input
            if (string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            // Check cache first for performance
            if (_assetExistsCache.TryGetValue(assetName, out var cachedResult))
            {
                return cachedResult;
            }

            // Normalize asset name: convert forward slashes to platform-specific separators
            // MonoGame uses forward slashes in asset names regardless of platform
            string normalizedAssetName = assetName.Replace('/', Path.DirectorySeparatorChar);

            // Build full path to .xnb file
            // MonoGame compiles all assets to .xnb files in the RootDirectory
            string rootDir = _contentManager.RootDirectory;
            if (string.IsNullOrEmpty(rootDir))
            {
                // If RootDirectory is not set, asset cannot exist
                _assetExistsCache[assetName] = false;
                return false;
            }

            // Combine root directory with normalized asset name and .xnb extension
            string assetPath = Path.Combine(rootDir, normalizedAssetName + ".xnb");

            // Check if file exists
            // On Windows, File.Exists is case-insensitive
            // On Unix, we need to handle case sensitivity properly
            bool exists = CheckFileExists(assetPath);

            // Cache the result
            _assetExistsCache[assetName] = exists;

            return exists;
        }

        /// <summary>
        /// Checks if a file exists, handling case sensitivity appropriately for the platform.
        /// </summary>
        /// <remarks>
        /// On Windows, file system is case-insensitive, so File.Exists is sufficient.
        /// On Unix platforms, file system is case-sensitive, so we need to check more carefully.
        /// This method ensures consistent behavior across platforms.
        /// </remarks>
        /// <param name="filePath">Full path to the file to check.</param>
        /// <returns>True if file exists, false otherwise.</returns>
        private bool CheckFileExists(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            // On Windows, File.Exists handles case-insensitivity automatically
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return File.Exists(filePath);
            }

            // On Unix platforms, File.Exists is case-sensitive
            // First try direct check (fast path for exact match)
            if (File.Exists(filePath))
            {
                return true;
            }

            // If direct check fails, try case-insensitive search in the directory
            // This handles cases where asset name has wrong case but file exists
            string directory = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileName(filePath);

            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            // Check if directory exists
            if (!Directory.Exists(directory))
            {
                return false;
            }

            // Search for file with case-insensitive comparison
            // This is more expensive but necessary for cross-platform compatibility
            try
            {
                string[] files = Directory.GetFiles(directory);
                foreach (string file in files)
                {
                    if (string.Equals(Path.GetFileName(file), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // If directory access fails, return false
                // This handles permission errors, deleted directories, etc.
                return false;
            }

            return false;
        }

        public void Dispose()
        {
            _contentManager?.Dispose();
        }
    }
}

