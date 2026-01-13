using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;
using Andastra.Runtime.Engines.Eclipse.DragonAge;

namespace Andastra.Game.Engines.Eclipse.DragonAgeOrigins
{
    /// <summary>
    /// Dragon Age: Origins module loader implementation (daorigins.exe).
    /// </summary>
    /// <remarks>
    /// Dragon Age: Origins Module Loading:
    /// - Based on daorigins.exe: LoadModule @ 0x00b17da4, MODULES @ 0x00ad9810, WRITE_MODULES @ 0x00ad98d8
    /// - LoadModuleSkipUnauthorizedMessage @ 0x00ae63f0, CClientExoApp.LoadModuleSkipUnauthorized @ 0x00ae69b0
    /// - Inherits common Dragon Age module loading from DragonAgeModuleLoader
    /// </remarks>
    public class DragonAgeOriginsModuleLoader : DragonAgeModuleLoader
    {
        public DragonAgeOriginsModuleLoader(IWorld world, IGameResourceProvider resourceProvider)
            : base(world, resourceProvider)
        {
        }

        // All common Dragon Age module loading logic is in DragonAgeModuleLoader
        // Override LoadDragonAgeModuleResourcesAsync if DA:O has specific differences
    }
}

