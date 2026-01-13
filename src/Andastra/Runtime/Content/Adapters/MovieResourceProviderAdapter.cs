using System.Threading;
using System.Threading.Tasks;
using BioWare.NET.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.Interfaces;

namespace Andastra.Runtime.Content.Adapters
{
    /// <summary>
    /// Adapter that makes IGameResourceProvider implement IMovieResourceProvider.
    /// Allows Core to use Content layer resource providers without depending on Content.
    /// </summary>
    public class MovieResourceProviderAdapter : IMovieResourceProvider
    {
        private readonly IGameResourceProvider _provider;

        /// <summary>
        /// Initializes a new instance of the MovieResourceProviderAdapter class.
        /// </summary>
        /// <param name="provider">The game resource provider to adapt.</param>
        public MovieResourceProviderAdapter(IGameResourceProvider provider)
        {
            _provider = provider ?? throw new System.ArgumentNullException("provider");
        }

        /// <summary>
        /// Checks if a resource exists without opening it.
        /// </summary>
        public Task<bool> ExistsAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return _provider.ExistsAsync(id, ct);
        }

        /// <summary>
        /// Gets the raw bytes of a resource.
        /// </summary>
        public Task<byte[]> GetResourceBytesAsync(ResourceIdentifier id, CancellationToken ct)
        {
            return _provider.GetResourceBytesAsync(id, ct);
        }
    }
}

