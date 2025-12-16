using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Odyssey.Core.Interfaces;
using Odyssey.Core.Navigation;

namespace Odyssey.Engines.Common
{
    /// <summary>
    /// Base interface for module management across all engines.
    /// </summary>
    public interface IEngineModule
    {
        /// <summary>
        /// Gets the current module name.
        /// </summary>
        [CanBeNull]
        string CurrentModuleName { get; }

        /// <summary>
        /// Gets the current area.
        /// </summary>
        [CanBeNull]
        IArea CurrentArea { get; }

        /// <summary>
        /// Gets the current navigation mesh.
        /// </summary>
        [CanBeNull]
        NavigationMesh CurrentNavigationMesh { get; }

        /// <summary>
        /// Loads a module by name.
        /// </summary>
        Task LoadModuleAsync(string moduleName, [CanBeNull] Action<float> progressCallback = null);

        /// <summary>
        /// Unloads the current module.
        /// </summary>
        void UnloadModule();

        /// <summary>
        /// Checks if a module exists.
        /// </summary>
        bool HasModule(string moduleName);
    }
}


