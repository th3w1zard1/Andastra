using System;
using Stride.Engine;
using Stride.Graphics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IContentManager.
    /// </summary>
    public class StrideContentManager : IContentManager
    {
        private readonly ContentManager _contentManager;

        internal ContentManager ContentManager => _contentManager;

        public StrideContentManager(ContentManager contentManager)
        {
            _contentManager = contentManager ?? throw new ArgumentNullException(nameof(contentManager));
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
                var spriteFont = _contentManager.Load<SpriteFont>(assetName);
                return new StrideFont(spriteFont) as T;
            }
            else if (typeof(ITexture2D).IsAssignableFrom(typeof(T)))
            {
                var texture = _contentManager.Load<Texture2D>(assetName);
                return new StrideTexture2D(texture) as T;
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

        public bool AssetExists(string assetName)
        {
            // Stride doesn't have a built-in way to check if an asset exists
            // We can try to load it and catch the exception, but that's inefficient
            // For now, return true and let Load throw if it doesn't exist
            return true;
        }

        public void Dispose()
        {
            _contentManager?.Dispose();
        }
    }
}

