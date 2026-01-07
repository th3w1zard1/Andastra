using System;
using System.Threading.Tasks;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Core.Module;

namespace Andastra.Runtime.Engines.Odyssey.Loading
{
    /// <summary>
    /// KOTOR implementation of IModuleLoader.
    /// Wraps ModuleLoader to provide async interface.
    /// </summary>
    /// <remarks>
    /// Module Loader (KOTOR Implementation):
    /// - Based on swkotor2.exe module loading system
    /// - LoadModule @ 0x004f20d0 - Main module loading function (located via "MODULES:" @ 0x007b58b4)
    /// - LoadModuleFromPath @ 0x004f3460 - Loads module from path with save integration (located via "MODULES:" @ 0x007b58b4)
    /// - Located via string references: "MODULES:" @ 0x007b58b4, "Module" @ 0x007bc4e0
    /// - Original implementation: Loads modules from MODULES directory or module archives
    /// - Module loading sequence: IFO → ARE → GIT → LYT → VIS → entity spawning
    /// - This wrapper provides async interface for ModuleTransitionSystem
    /// </remarks>
    public class KotorModuleLoader : IModuleLoader
    {
        private readonly ModuleLoader _moduleLoader;

        public KotorModuleLoader(ModuleLoader moduleLoader)
        {
            _moduleLoader = moduleLoader ?? throw new ArgumentNullException("moduleLoader");
        }

        /// <summary>
        /// Loads a module by resource reference.
        /// </summary>
        /// <param name="moduleResRef">The module resource reference.</param>
        /// <returns>The loaded module.</returns>
        public Task<IModule> LoadModule(string moduleResRef)
        {
            if (string.IsNullOrEmpty(moduleResRef))
            {
                throw new ArgumentException("Module ResRef cannot be null or empty", "moduleResRef");
            }

            // Run synchronous load on background thread
            return Task.Run(() =>
            {
                RuntimeModule module = _moduleLoader.LoadModule(moduleResRef);
                return (IModule)module;
            });
        }
    }
}


