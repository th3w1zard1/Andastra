using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Common;
using BioWare.NET.Resource;

namespace Andastra.Runtime.Content.Interfaces
{
    /// <summary>
    /// Interface for content converters that transform KOTOR assets to runtime format.
    /// </summary>
    /// <remarks>
    /// Content Converter Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) asset loading and conversion system
    /// - Located via string references: "Resource" @ 0x007c14d4, "Loading" @ 0x007c7e40, CExoKeyTable resource loading
    /// - Original implementation: Converts KOTOR file formats (MDL, TPC, BWM, etc.) to runtime formats
    /// - MDL conversion: Converts MDL/MDX (model/animation) to runtime model format
    /// - TPC conversion: Converts TPC (texture) to runtime texture format
    /// - BWM conversion: Converts BWM (walkmesh) to runtime navigation mesh format
    /// - Async conversion: ConvertAsync allows non-blocking asset conversion (prevents frame drops)
    /// - Conversion context: Provides resource provider, cache, BioWareGame type module name for dependency resolution
    /// - Converter version: Tracks converter implementation version for cache invalidation
    /// - CanConvert: Checks if converter can handle specific resource (by ResRef, type, etc.)
    /// - Supported types: Lists resource types this converter handles (MDL, TPC, BWM, etc.)
    /// - Note: Original engine loads formats directly via CExoKeyTable, this adds conversion layer for modern rendering APIs
    /// </remarks>
    public interface IContentConverter<TInput, TOutput>
    {
        /// <summary>
        /// Converts an input asset to the output format.
        /// </summary>
        Task<TOutput> ConvertAsync(TInput input, ConversionContext ctx, CancellationToken ct);

        /// <summary>
        /// Checks if this converter can handle the given resource.
        /// </summary>
        bool CanConvert(ResourceIdentifier id);

        /// <summary>
        /// The resource types this converter handles.
        /// </summary>
        ResourceType[] SupportedTypes { get; }
    }

    /// <summary>
    /// Context for content conversion.
    /// </summary>
    public class ConversionContext
    {
        /// <summary>
        /// The resource provider for loading dependencies.
        /// </summary>
        public IGameResourceProvider ResourceProvider { get; set; }

        /// <summary>
        /// The content cache for storing converted assets.
        /// </summary>
        public IContentCache ContentCache { get; set; }

        /// <summary>
        /// The game type (K1/K2).
        /// </summary>
        public GameType GameType { get; set; }

        /// <summary>
        /// The current module name (if applicable).
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// Converter version for cache invalidation.
        /// </summary>
        public int ConverterVersion { get; set; }
    }
}

