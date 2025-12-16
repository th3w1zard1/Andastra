using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;

namespace Odyssey.Engines.Common
{
    /// <summary>
    /// Base interface for game session management across all engines.
    /// </summary>
    /// <remarks>
    /// Engine Game Interface:
    /// - Based on swkotor2.exe game session management
    /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58, module loading functions
    /// - Module loading: FUN_006caab0 @ 0x006caab0 sets module state flags, manages module transitions
    /// - Game session: Manages current module, player entity, world state
    /// - Original implementation: Game session loads modules, manages player entity, handles module transitions
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
    /// </remarks>
    public interface IEngineGame
    {
        /// <summary>
        /// Gets the current module name.
        /// </summary>
        [CanBeNull]
        string CurrentModuleName { get; }

        /// <summary>
        /// Gets the current player entity.
        /// </summary>
        [CanBeNull]
        IEntity PlayerEntity { get; }

        /// <summary>
        /// Gets the world instance.
        /// </summary>
        IWorld World { get; }

        /// <summary>
        /// Loads a module by name.
        /// </summary>
        Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null);

        /// <summary>
        /// Unloads the current module.
        /// </summary>
        void UnloadModule();

        /// <summary>
        /// Updates the game session (called every frame).
        /// </summary>
        void Update(float deltaTime);

        /// <summary>
        /// Shuts down the game session.
        /// </summary>
        void Shutdown();
    }
}


