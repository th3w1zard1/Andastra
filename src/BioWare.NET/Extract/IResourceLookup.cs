using System.Collections.Generic;
using BioWare.NET.Common;
using BioWare.NET.Extract;
using BioWare.NET.Resource;
using JetBrains.Annotations;

namespace BioWare.NET.Extract
{
    /// <summary>
    /// Interface for resource lookup capabilities in a game installation.
    /// Provides methods for looking up and locating resources across different search locations.
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) resource management system (CExoKeyTable, CExoResMan).
    /// </summary>
    /// <remarks>
    /// Resource Lookup Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) resource loading system
    /// - Located via string references: "Resource" @ (K1: 0x007c14d4, TSL: TODO: Find this address) resource table management functions
    /// - Resource precedence: OVERRIDE > MODULES > CHITIN > TEXTUREPACKS > STREAM directories
    /// - Original implementation: InstallationResourceManager provides the concrete implementation
    /// - Resource lookup: Searches locations in precedence order until resource found
    /// - Location tracking: LocateResource returns all locations where resource exists
    /// - Cache management: ClearCache and ReloadModule methods for cache control
    /// - Based on CExoKeyTable and CExoResMan resource management in original engine
    /// </remarks>
    public interface IResourceLookup
    {
        /// <summary>
        /// Looks up a single resource by name and type.
        /// </summary>
        /// <param name="resname">The resource name (case-insensitive).</param>
        /// <param name="restype">The resource type.</param>
        /// <param name="searchOrder">Optional search order. If null, uses default order.</param>
        /// <param name="moduleRoot">Optional module root to limit search scope.</param>
        /// <returns>ResourceResult if found, null otherwise.</returns>
        [CanBeNull]
        ResourceResult LookupResource(
            string resname,
            ResourceType restype,
            [CanBeNull] SearchLocation[] searchOrder = null,
            [CanBeNull] string moduleRoot = null);

        /// <summary>
        /// Locates all instances of a resource across the installation.
        /// </summary>
        /// <param name="resname">The resource name (case-insensitive).</param>
        /// <param name="restype">The resource type.</param>
        /// <param name="searchOrder">Optional search order. If null, uses default order.</param>
        /// <param name="moduleRoot">Optional module root to limit search scope.</param>
        /// <returns>List of LocationResult objects for all found instances.</returns>
        List<LocationResult> LocateResource(
            string resname,
            ResourceType restype,
            [CanBeNull] SearchLocation[] searchOrder = null,
            [CanBeNull] string moduleRoot = null);

        /// <summary>
        /// Clears all cached resources, forcing a reload on next access.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Reloads a specific module's resources.
        /// </summary>
        /// <param name="moduleName">The module name to reload.</param>
        void ReloadModule(string moduleName);

        /// <summary>
        /// Returns a shallow copy of the list of FileResources stored in the Chitin linked to the Installation.
        /// </summary>
        /// <returns>List of FileResource objects from Chitin.</returns>
        List<FileResource> GetChitinResources();

        /// <summary>
        /// Returns the list of FileResources stored in patch.erf (K1 only).
        /// </summary>
        /// <param name="game">The game type to check.</param>
        /// <returns>List of FileResource objects from patch.erf.</returns>
        List<FileResource> GetPatchErfResources(BioWareGame game);
    }
}

