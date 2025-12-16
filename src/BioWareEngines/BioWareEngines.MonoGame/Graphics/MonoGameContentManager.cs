using System;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using BioWareEngines.Graphics;

namespace BioWareEngines.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IContentManager.
    /// </summary>
    public class MonoGameContentManager : IContentManager
    {
        private readonly ContentManager _contentManager;

        internal ContentManager ContentManager => _contentManager;

        public MonoGameContentManager(ContentManager contentManager)
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
        }

        public bool AssetExists(string assetName)
        {
            // MonoGame doesn't have a built-in way to check if an asset exists
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

