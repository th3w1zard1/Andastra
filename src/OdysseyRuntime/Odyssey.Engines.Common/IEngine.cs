using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;
using Odyssey.Content.Interfaces;
using Odyssey.Scripting.Interfaces;

namespace Odyssey.Engines.Common
{
    /// <summary>
    /// Base interface for all BioWare engine implementations.
    /// </summary>
    /// <remarks>
    /// Engine Interface:
    /// - Based on swkotor2.exe engine architecture
    /// - Located via string references: Engine initialization in FUN_00404250 @ 0x00404250 (WinMain equivalent)
    /// - Engine initialization: FUN_00404250 initializes engine objects, loads configuration, creates game instance
    /// - Resource provider: CExoKeyTable handles resource loading, tracks loaded resources
    /// - World management: Engine manages world instance, module loading, entity management
    /// - Engine API: Provides NWScript engine functions (GetObjectByTag, GetNearestObject, etc.)
    /// - Original implementation: Engine provides resource provider, world, engine API, game session creation
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
    /// </remarks>
    public interface IEngine
    {
        /// <summary>
        /// Gets the engine family (Odyssey, Aurora, Eclipse).
        /// </summary>
        EngineFamily EngineFamily { get; }

        /// <summary>
        /// Gets the game profile for this engine instance.
        /// </summary>
        IEngineProfile Profile { get; }

        /// <summary>
        /// Gets the resource provider for loading game resources.
        /// </summary>
        IGameResourceProvider ResourceProvider { get; }

        /// <summary>
        /// Gets the world instance.
        /// </summary>
        IWorld World { get; }

        /// <summary>
        /// Gets the engine API instance.
        /// </summary>
        IEngineApi EngineApi { get; }

        /// <summary>
        /// Creates a new game session for this engine.
        /// </summary>
        IEngineGame CreateGameSession();

        /// <summary>
        /// Initializes the engine with the specified installation path.
        /// </summary>
        void Initialize(string installationPath);

        /// <summary>
        /// Shuts down the engine and cleans up resources.
        /// </summary>
        void Shutdown();
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
        /// Eclipse/Unreal Engine (Mass Effect series, Dragon Age)
        /// </summary>
        Eclipse,

        /// <summary>
        /// Unknown or unsupported engine
        /// </summary>
        Unknown
    }
}


