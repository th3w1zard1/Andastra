using System.IO;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace Andastra.Runtime.Content.Interfaces
{
    /// <summary>
    /// Resource precedence chain element.
    /// </summary>
    /// <remarks>
    /// Resource Provider Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) resource loading system
    /// - Located via string references: "Resource" @ 0x007c14d4, "CExoKeyTable::DestroyTable: Resource %s still in demand during table deletion" @ 0x007b6078
    /// - "CExoKeyTable::AddKey: Duplicate Resource " @ 0x007b6124 (duplicate resource error)
    /// - Resource precedence: OVERRIDE > MODULE > SAVE > TEXTUREPACKS > CHITIN > HARDCODED
    /// - Original implementation: Resource providers checked in precedence order until resource found
    /// - OVERRIDE: User override directory (highest priority, allows modding)
    /// - MODULE: Module-specific resources (module.rim, module_s.rim)
    /// - SAVE: Save game resources (savegame.sav ERF archive)
    /// - TEXTUREPACKS: Texture pack ERF files (swpc_tex_tpa.erf, swpc_tex_tpb.erf, etc.)
    /// - CHITIN: Main game archive (chitin.key references BIF files)
    /// - HARDCODED: Built-in resources (lowest priority, fallback)
    /// - Resource lookup: 0x00633270 @ 0x00633270 sets up resource directories and precedence
    /// - Based on CExoKeyTable resource management system in original engine
    /// </remarks>
    public interface IResourceProvider
    {
        /// <summary>
        /// Priority of this provider (higher = checked first).
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Tries to open a resource stream.
        /// </summary>
        bool TryOpen(ResourceIdentifier id, out Stream stream);

        /// <summary>
        /// Checks if a resource exists.
        /// </summary>
        bool Exists(ResourceIdentifier id);

        /// <summary>
        /// The search location this provider represents.
        /// </summary>
        SearchLocation Location { get; }
    }
}

