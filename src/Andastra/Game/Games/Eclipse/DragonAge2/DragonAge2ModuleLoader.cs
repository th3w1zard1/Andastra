using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Eclipse.DragonAge;

namespace Andastra.Game.Engines.Eclipse.DragonAge2
{
    /// <summary>
    /// Dragon Age 2 module loader implementation (DragonAge2.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age 2 Module Loading:
    /// - Based on DragonAge2.exe: LoadModuleMessage @ 0x00bf5df8, MODULES: @ 0x00bf5d10, WRITE_MODULES: @ 0x00bf5d24
    /// - ModuleID @ 0x00be9688, ModuleStartupInfo @ 0x00bebb64, ModuleInfoList @ 0x00bfa278
    /// - GetMainModuleName @ 0x00c0ed00, GetCurrentModuleName @ 0x00c0ed24, CModule @ 0x00c236b4
    /// - Inherits common Dragon Age module loading from DragonAgeModuleLoader
    /// </remarks>
    public class DragonAge2ModuleLoader : DragonAgeModuleLoader
    {
        public DragonAge2ModuleLoader(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
        {
        }

        // All common Dragon Age module loading logic is in DragonAgeModuleLoader
        // Override LoadDragonAgeModuleResourcesAsync if DA2 has specific differences
    }
}

