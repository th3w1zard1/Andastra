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


