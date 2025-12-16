using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;

namespace Odyssey.Engines.Common
{
    /// <summary>
    /// Base interface for game session management across all engines.
    /// </summary>
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


