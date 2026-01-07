using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Content manager interface for loading game assets.
    /// </summary>
    /// <remarks>
    /// Content Manager Interface:
    /// - Based on swkotor2.exe resource loading system
    /// - Located via string references: "Resource" @ 0x007c14d4, "Loading" @ 0x007c7e40
    /// - CExoKeyTable @ 0x007b6078, FUN_00633270 @ 0x00633270 (resource path resolution)
    /// - Original implementation: Loads game assets (textures, models, sounds) from ERF/BIF files via CExoKeyTable
    /// - Resource loading: Original game uses CExoKeyTable to resolve resource paths and load from ERF/BIF archives
    /// - This interface: Abstraction layer for modern content management (MonoGame ContentManager, Stride AssetManager)
    /// - Note: Modern content managers use different asset pipeline than original game's ERF/BIF system
    /// </remarks>
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

