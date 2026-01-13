using System;
using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Game.Scripting.Interfaces;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Games.Odyssey.Profiles
{
    /// <summary>
    /// Defines game-specific behavior for different Aurora/Odyssey engine games.
    /// </summary>
    /// <remarks>
    /// Game Profile Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) game profile system
    /// - Located via string references: Game version detection, resource path resolution, 0x00633270 @ 0x00633270 sets up resource directories
    /// - Original implementation: Provides game-specific configuration for resource loading, NWScript functions, and feature support
    /// - The engine is designed for extensibility to support:
    ///   - KOTOR 1 (Odyssey) - K1GameProfile
    ///   - KOTOR 2 / TSL (Odyssey) - K2GameProfile
    ///   - Jade Empire (Odyssey variant) - future
    ///   - NWN (Aurora - future)
    ///   -  (Eclipse/Unreal - future)
    /// - Each game profile provides:
    ///   - Resource layout specifics (chitin.key, texture packs, modules directory)
    ///   - Script function sets (NWScript with game-specific variations, K1 ~850 functions, K2 ~950 functions)
    ///   - Table schemas (2DA column names and structure)
    ///   - Rule variants (combat, dialogue, feature support differences)
    /// - CreateEngineApi() returns game-specific IEngineApi implementation (K1 or K2)
    /// - SupportsFeature() checks if a game feature is available (e.g., Influence system in K2 only)
    /// </remarks>
    public interface IGameProfile
    {
        /// <summary>
        /// Gets the game type.
        /// </summary>
        GameType GameType { get; }

        /// <summary>
        /// Gets the display name of the game.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the engine family this game belongs to.
        /// </summary>
        EngineFamily EngineFamily { get; }

        /// <summary>
        /// Gets the engine API instance for this game.
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
        bool SupportsFeature(GameFeature feature);
    }

    /// <summary>
    /// Engine family enumeration for grouping related engines.
    /// </summary>
    public enum EngineFamily
    {
        /// <summary>
        /// Aurora Engine (NWN, NWN2)
        /// </summary>
        Aurora,

        /// <summary>
        /// Odyssey Engine (KOTOR, KOTOR2, Jade Empire)
        /// </summary>
        Odyssey,

        /// <summary>
        /// Eclipse/Unreal Engine ( series)
        /// </summary>
        Eclipse,

        /// <summary>
        /// Unknown or unsupported engine
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Game-specific features that may or may not be supported.
    /// </summary>
    public enum GameFeature
    {
        // Core game systems
        DialogSystem,
        JournalSystem,
        PartySystem,
        InventorySystem,
        CombatSystem,
        LevelingSystem,
        ForceSystem,
        CraftingSystem,
        MiniGames,

        // TSL-specific
        Influence,
        InfluenceSystem,
        ItemCrafting,
        CombatForms,
        PrestigeClasses,
        Workbench,
        LabStation,
        ItemBreakdown,

        // Minigames
        Pazaak,
        PazaakDen,
        SwoopRacing,
        Turret,

        // Other
        RemoteControl,
        AlignmentDialogue,
        PartyDeathHandling
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
