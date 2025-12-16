using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Content manager interface for loading game assets.
    /// </summary>
    public interface IContentManager : IDisposable
    {
        /// <summary>
        /// Gets or sets the root directory for content.
        /// </summary>
        string RootDirectory { get; set; }

        /// <summary>
        /// Loads an asset of the specified type.
        /// </summary>
        /// <typeparam name="T">Asset type.</typeparam>
        /// <param name="assetName">Asset name (without extension).</param>
        /// <returns>Loaded asset.</returns>
        T Load<T>(string assetName) where T : class;

        /// <summary>
        /// Unloads all content.
        /// </summary>
        void Unload();

        /// <summary>
        /// Checks if an asset exists.
        /// </summary>
        /// <param name="assetName">Asset name.</param>
        /// <returns>True if asset exists.</returns>
        bool AssetExists(string assetName);
    }
}

