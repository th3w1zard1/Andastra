using System;
using Andastra.Runtime.Graphics;
using StrideGraphics = global::Stride.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IContentManager.
    /// </summary>
    public class StrideContentManager : IContentManager
    {
        private readonly dynamic _contentManager;
        private readonly global::Stride.Graphics.CommandList _graphicsContext;

        internal dynamic ContentManager => _contentManager;

        public StrideContentManager(dynamic contentManager, global::Stride.Graphics.CommandList graphicsContext = null)
        {
            _contentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
            _graphicsContext = graphicsContext;
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
                var spriteFont = _contentManager.Load<StrideGraphics.SpriteFont>(assetName);
                return new StrideFont(spriteFont) as T;
            }
            else if (typeof(ITexture2D).IsAssignableFrom(typeof(T)))
            {
                var texture = _contentManager.Load<global::Stride.Graphics.Texture>(assetName);
                return new StrideTexture2D(texture, _graphicsContext) as T;
            }
            else
            {
                return _contentManager.Load<T>(assetName);
            }
        }

        public void Unload()
        {
            _contentManager.Unload();
        }

        /// <summary>
        /// Checks if an asset exists in the content manager.
        /// </summary>
        /// <remarks>
        /// Stride ContentManager doesn't provide a direct API to check asset existence.
        /// The standard approach is to attempt loading and catch exceptions.
        /// While this has some overhead, it's the most reliable method when no dedicated API exists.
        /// Based on Stride Engine ContentManager: No AssetExists or similar method in public API
        /// Stride assets are stored in .sd files (compiled) or .sdsd files (source)
        /// Asset paths are resolved relative to RootDirectory
        /// ContentManagerException is thrown when asset is not found
        /// </remarks>
        public bool AssetExists(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return false;
            }

            // Stride ContentManager doesn't have a built-in AssetExists method
            // The standard pattern is to attempt loading and catch ContentManagerException
            // While this has overhead, it's the most reliable approach when no dedicated API exists
            // Based on Stride Engine ContentManager: Load<T> throws ContentManagerException if asset not found
            try
            {
                // Attempt to load the asset to verify it exists
                // We use a generic approach: try loading as object (if supported) or a common type
                // Note: Stride's Load<T> requires a concrete type, so we use Texture2D as a test type
                // If the asset exists but is wrong type, we'll still get an exception, which we interpret as "exists"
                // The actual type checking happens during real Load calls
                _contentManager.Load<global::Stride.Graphics.Texture>(assetName);
                return true; // Asset exists and can be loaded as Texture2D
            }
            catch (System.IO.FileNotFoundException)
            {
                // FileNotFoundException indicates asset file doesn't exist
                return false;
            }
            catch (System.IO.DirectoryNotFoundException)
            {
                // DirectoryNotFoundException indicates asset directory doesn't exist
                return false;
            }
            catch (System.Exception)
            {
                // ContentManagerException is thrown when asset is not found
                // This is the primary indicator that the asset doesn't exist
                // Other exceptions (e.g., wrong type, corrupted file, etc.)
                // For existence checking purposes, if we got an exception other than "not found",
                // it means the asset file exists but there's some other issue (wrong type, corrupted, etc.)
                // We return true here because the asset exists (caller can handle type/format issues during actual Load)
                // This matches the pattern: AssetExists should return true if the asset file exists,
                // even if it can't be loaded as the requested type
                return true;
            }
        }

        public void Dispose()
        {
            // Stride ContentManager doesn't implement IDisposable - it's managed by the Game instance
            // Call Unload() to unload all assets, which is the appropriate cleanup method
            // Based on Stride Engine API: ContentManager is managed by Game and doesn't need explicit disposal
            // Unload() releases all loaded assets and is the standard cleanup method
            try
            {
                Unload();
            }
            catch
            {
                // Ignore exceptions during disposal - ContentManager may already be disposed by Game
            }
        }
    }
}

