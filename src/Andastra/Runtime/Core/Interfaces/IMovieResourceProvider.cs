using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Resource;

namespace Andastra.Runtime.Core.Interfaces
{
    /// <summary>
    /// Minimal resource provider interface for movie playback.
    /// Allows Core to load movie files without depending on Content layer.
    /// </summary>
    /// <remarks>
    /// Movie Resource Provider Interface:
    /// - Based on swkotor.exe/swkotor2.exe: Movie file loading (FUN_005fbbf0 @ 0x005fbbf0)
    /// - Movie file paths: "MOVIES:%s" @ 0x0073d7d8 (path format string)
    /// - ".\\movies" @ 0x0074e004, "d:\\movies" @ 0x0074e010 (movie directory paths)
    /// - "LIVE%d:movies\\%s" @ 0x0074ec3c (LIVE path format for CD-based installations)
    /// - Original implementation: Loads BIK movie files from resource system
    /// - This interface: Minimal contract for Core to load movie resources
    /// - Content layer implementations (IGameResourceProvider) should implement this interface
    /// </remarks>
    public interface IMovieResourceProvider
    {
        /// <summary>
        /// Checks if a resource exists without opening it.
        /// </summary>
        /// <param name="id">Resource identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>True if resource exists, false otherwise.</returns>
        Task<bool> ExistsAsync(ResourceIdentifier id, CancellationToken ct);

        /// <summary>
        /// Gets the raw bytes of a resource.
        /// </summary>
        /// <param name="id">Resource identifier.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>Resource data, or null if resource not found.</returns>
        Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, CancellationToken ct);
    }
}

