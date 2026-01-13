using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace Andastra.Runtime.Content.Interfaces
{
    /// <summary>
    /// Unified resource access - wraps all precedence logic for KOTOR resource lookup.
    /// </summary>
    /// <remarks>
    /// Game Resource Provider Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) resource loading system
    /// - Located via string references: "Resource" @ 0x007c14d4, resource table management functions
    /// - Resource precedence: OVERRIDE > MODULE > SAVE > TEXTUREPACKS > CHITIN > HARDCODED
    /// - Original implementation: Unified interface for accessing game resources with automatic precedence resolution
    /// - Resource lookup: Searches providers in precedence order until resource found
    /// - Async operations: Resource loading is asynchronous to prevent blocking game loop
    /// - Resource enumeration: Can enumerate all resources of a type (for modding tools, resource browsers)
    /// - Location tracking: LocateAsync returns all locations where resource exists (useful for debugging mod conflicts)
    /// - Based on CExoKeyTable and CExoResMan resource management in original engine
    /// - FUN_00633270 @ 0x00633270 sets up all resource directories and precedence chains
    /// </remarks>
    public interface IGameResourceProvider
    {
        /// <summary>
        /// Opens a resource stream asynchronously.
        /// </summary>
        Task<Stream> OpenResourceAsync(ResourceIdentifier id, CancellationToken ct);

        /// <summary>
        /// Checks if a resource exists without opening it.
        /// </summary>
        Task<bool> ExistsAsync(ResourceIdentifier id, CancellationToken ct);

        /// <summary>
        /// Locates a resource across multiple search locations.
        /// </summary>
        Task<IReadOnlyList<Andastra.Runtime.Content.Interfaces.LocationResult>> LocateAsync(ResourceIdentifier id, SearchLocation[] order, CancellationToken ct);

        /// <summary>
        /// Enumerates all resources of a specific type.
        /// </summary>
        IEnumerable<ResourceIdentifier> EnumerateResources(ResourceType type);

        /// <summary>
        /// Gets the raw bytes of a resource.
        /// </summary>
        Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, CancellationToken ct);

        /// <summary>
        /// Checks if a resource exists synchronously (blocking wrapper).
        /// </summary>
        bool Exists(ResourceIdentifier id);

        /// <summary>
        /// Gets the raw bytes of a resource synchronously (blocking wrapper).
        /// </summary>
        byte[] GetResourceBytes(ResourceIdentifier id);

        /// <summary>
        /// Loads a resource by ResRef and ResourceType synchronously.
        /// </summary>
        /// <param name="resRef">Resource reference.</param>
        /// <param name="resourceType">Resource type.</param>
        /// <returns>Resource data or null if not found.</returns>
        /// <remarks>
        /// This method provides synchronous resource loading by ResRef and ResourceType,
        /// complementing the ResourceIdentifier-based methods for cases where separate
        /// ResRef and ResourceType parameters are more convenient.
        /// </remarks>
        byte[] LoadResource(BioWare.NET.Common.ResRef resRef, ResourceType resourceType);

        /// <summary>
        /// The game type (K1, K2, BaldursGate, , , etc.).
        /// </summary>
        GameType GameType { get; }
    }

    /// <summary>
    /// Result of locating a resource.
    /// </summary>
    public struct LocationResult
    {
        public SearchLocation Location;
        public string Path;
        public long Size;
        public long Offset;
    }

    /// <summary>
    /// Search locations for resource lookup, in precedence order.
    /// </summary>
    public enum SearchLocation
    {
        Override,
        Module,
        Save,
        TexturePacks,
        Chitin,
        Hardcoded
    }

    /// <summary>
    /// Game type.
    /// </summary>
    public enum GameType
    {
        Unknown,
        // Odyssey Engine games
        K1,
        K2,
        // Aurora Engine games
        NWN,
        NWN2,
        NWNEE,
        BaldursGate,
        // Eclipse Engine games
        DA_ORIGINS,
        DA2
    }
}

