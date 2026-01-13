using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Scripting.Interfaces;

namespace Andastra.Game.Engines.Common
{
    /// <summary>
    /// Base interface for game profiles across all engines.
    /// </summary>
    /// <remarks>
    /// Engine Profile Interface:
    /// - Common contract shared across all BioWare engines (Odyssey, Aurora, Eclipse, Infinity)
    /// - Defines the interface for game profiles that all engines must provide
    /// - Engine-specific implementations must be in concrete classes (OdysseyEngineProfile, AuroraEngineProfile, EclipseEngineProfile)
    ///
    /// Common Profile Patterns (Reverse Engineered):
    /// - All engines implement resource configuration (IResourceConfig) with modules, override, saves directories
    /// - All engines implement table configuration (ITableConfig) for 2DA table schemas
    /// - All engines support feature detection via SupportsFeature() method
    /// - All engines identify themselves via GameType property (e.g., "K1", "K2", "NWN", "DAO")
    /// - See BaseEngineProfile for detailed reverse engineering documentation
    /// </remarks>
    public interface IEngineProfile
    {
        /// <summary>
        /// Gets the game type identifier.
        /// </summary>
        string GameType { get; }

        /// <summary>
        /// Gets the display name of the game.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the engine family this game belongs to.
        /// </summary>
        EngineFamily EngineFamily { get; }

        /// <summary>
        /// Creates the engine API instance for this game.
        /// </summary>
        IEngineApi CreateEngineApi();

        /// <summary>
        /// Gets game-specific configuration for resource loading.
        /// </summary>
        IResourceConfig ResourceConfig { get; }

        /// <summary>
        /// Gets game-specific 2DA table configuration.
        /// </summary>
        ITableConfig TableConfig { get; }

        /// <summary>
        /// Gets whether this game supports a specific feature.
        /// </summary>
        bool SupportsFeature(string feature);
    }

    /// <summary>
    /// Game-specific resource configuration.
    /// </summary>
    public interface IResourceConfig
    {
        /// <summary>
        /// Gets the chitin.key filename.
        /// </summary>
        string ChitinKeyFile { get; }

        /// <summary>
        /// Gets the texture pack ERF filenames.
        /// </summary>
        IReadOnlyList<string> TexturePackFiles { get; }

        /// <summary>
        /// Gets the dialog.tlk filename.
        /// </summary>
        string DialogTlkFile { get; }

        /// <summary>
        /// Gets the modules directory name.
        /// </summary>
        string ModulesDirectory { get; }

        /// <summary>
        /// Gets the override directory name.
        /// </summary>
        string OverrideDirectory { get; }

        /// <summary>
        /// Gets the save game directory name.
        /// </summary>
        string SavesDirectory { get; }
    }

    /// <summary>
    /// Game-specific 2DA table configuration.
    /// </summary>
    public interface ITableConfig
    {
        /// <summary>
        /// Gets required 2DA tables for the game.
        /// </summary>
        IReadOnlyList<string> RequiredTables { get; }

        /// <summary>
        /// Gets the appearance.2da column configuration.
        /// </summary>
        IReadOnlyDictionary<string, string> AppearanceColumns { get; }

        /// <summary>
        /// Gets the baseitems.2da column configuration.
        /// </summary>
        IReadOnlyDictionary<string, string> BaseItemsColumns { get; }
    }
}


