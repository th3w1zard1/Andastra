using System.Collections.Generic;
using Odyssey.Content.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Engines.Common
{
    /// <summary>
    /// Base interface for game profiles across all engines.
    /// </summary>
    /// <remarks>
    /// Engine Profile Interface:
    /// - Based on swkotor2.exe game profile system
    /// - Located via string references: "config.txt" @ 0x007b5750 (configuration file loading)
    /// - Game profile determines game-specific behavior (K1 vs K2) through resource configs and table configs
    /// - Game profiles: Define resource configs, table configs, supported features, engine API creation
    /// - Resource config: Defines resource file locations (chitin.key, dialog.tlk, modules, override, saves), keyfile handling, resource precedence
    /// - Table config: Defines 2DA table locations (appearance.2da, baseitems.2da, etc.), table loading behavior
    /// - Original implementation: Game profiles configure engine behavior for specific games (KOTOR, KOTOR2, etc.)
    /// - Engine initialization: FUN_00404250 @ 0x00404250 loads game configuration based on profile
    /// - Resource loading: FUN_00633270 @ 0x00633270 handles resource path resolution based on profile
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
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


