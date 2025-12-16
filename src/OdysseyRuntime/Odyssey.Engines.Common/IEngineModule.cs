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
    /// <remarks>
    /// Engine Module Interface:
    /// - Based on swkotor2.exe module management system
    /// - Located via string references: "ModuleLoaded" @ 0x007bdd70, "ModuleRunning" @ 0x007bdd58, "MODULE" @ module loading
    /// - Module loading: FUN_006caab0 @ 0x006caab0 sets module state flags, manages module transitions
    /// - Module management: Loads module areas, navigation meshes, entities from GIT files
    /// - Original implementation: Module system loads areas, entities, navigation meshes, manages module state
    /// - Note: This is an abstraction layer for multiple BioWare engines (Odyssey, Aurora, Eclipse)
    /// </remarks>
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


