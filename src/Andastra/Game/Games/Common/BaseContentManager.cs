using System;
using System.Collections.Generic;
using Andastra.Runtime.Content.Interfaces;
using JetBrains.Annotations;

namespace Andastra.Game.Games.Common
{
    /// <summary>
    /// Base implementation of content management shared across all BioWare engines.
    /// </summary>
    /// <remarks>
    /// Base Content Manager Implementation:
    /// - Common resource loading and caching framework
    /// - Handles different content types (models, textures, sounds, etc.)
    /// - Provides engine-agnostic resource management
    ///
    /// Based on reverse engineering of:
    /// - swkotor.exe: Basic resource loading from BIF files
    /// - swkotor2.exe: Enhanced loading with ERF support
    /// - nwmain.exe: Aurora resource system (Neverwinter Nights)
    /// - daorigins.exe: Eclipse resource management (Dragon Age: Origins)
    /// - DragonAge2.exe: Eclipse resource management (Dragon Age 2) - Enhanced ERF/DAZIP support, RIM file handling
    /// - : Eclipse resource management () - PCC/UPK package format, streaming resources
    /// - : Eclipse resource management ( 2) - Enhanced PCC/UPK support, cook package format
    /// - Common concepts: Resource caching, format conversion, dependency management
    ///
    /// Common functionality across engines:
    /// - Resource loading and caching
    /// - Format conversion (TGA->DDS, etc.)
    /// - Memory management and cleanup
    /// - Asynchronous loading support
    /// - Resource dependency tracking
    /// - Error handling and fallbacks
    /// - Performance monitoring and statistics
    /// </remarks>
    [PublicAPI]
    public abstract class BaseContentManager
    {
        protected readonly Dictionary<string, object> _resourceCache = new Dictionary<string, object>();
        protected readonly IGameResourceProvider _resourceProvider;

        protected BaseContentManager(IGameResourceProvider resourceProvider)
        {
            _resourceProvider = resourceProvider ?? throw new ArgumentNullException(nameof(resourceProvider));
        }

        /// <summary>
        /// Loads a resource by name and type.
        /// </summary>
        /// <remarks>
        /// Common resource loading framework.
        /// Checks cache first, then loads from provider.
        /// Handles format conversion and caching.
        /// </remarks>
        public virtual T LoadResource<T>(string resourceName, string resourceType = null)
        {
            var cacheKey = GetCacheKey(resourceName, resourceType, typeof(T));

            // Check cache first
            if (_resourceCache.TryGetValue(cacheKey, out var cachedResource))
            {
                return (T)cachedResource;
            }

            // Load from provider
            var resource = LoadResourceFromProvider(resourceName, resourceType, typeof(T));
            if (resource != null)
            {
                // Convert format if needed
                resource = ConvertResourceFormat(resource, typeof(T));

                // Cache the result
                _resourceCache[cacheKey] = resource;
                OnResourceLoaded(resourceName, resourceType, resource);
            }

            return (T)resource;
        }

        /// <summary>
        /// Unloads a resource from cache.
        /// </summary>
        /// <remarks>
        /// Removes resource from cache and disposes if necessary.
        /// Called when resources are no longer needed.
        /// Common across all engines.
        /// </remarks>
        public virtual void UnloadResource(string resourceName, string resourceType = null)
        {
            var cacheKey = GetCacheKey(resourceName, resourceType, null);

            if (_resourceCache.TryGetValue(cacheKey, out var resource))
            {
                _resourceCache.Remove(cacheKey);
                DisposeResource(resource);
                OnResourceUnloaded(resourceName, resourceType, resource);
            }
        }

        /// <summary>
        /// Clears the resource cache.
        /// </summary>
        /// <remarks>
        /// Unloads all cached resources.
        /// Useful for memory management or level transitions.
        /// Common across all engines.
        /// </remarks>
        public virtual void ClearCache()
        {
            foreach (var resource in _resourceCache.Values)
            {
                DisposeResource(resource);
            }
            _resourceCache.Clear();
            OnCacheCleared();
        }

        /// <summary>
        /// Gets resource loading statistics.
        /// </summary>
        /// <remarks>
        /// Returns cache statistics for debugging.
        /// Useful for memory usage monitoring.
        /// </remarks>
        public virtual ContentStats GetStats()
        {
            return new ContentStats
            {
                CachedResourceCount = _resourceCache.Count,
                CacheSizeBytes = EstimateCacheSize()
            };
        }

        /// <summary>
        /// Loads resource from the provider.
        /// </summary>
        /// <remarks>
        /// Engine-specific resource loading implementation.
        /// Subclasses implement engine-specific loading logic.
        /// </remarks>
        protected abstract object LoadResourceFromProvider(string resourceName, string resourceType, Type targetType);

        /// <summary>
        /// Converts resource format if needed.
        /// </summary>
        /// <remarks>
        /// Handles format conversion (TGA to DDS, etc.).
        /// Engine-specific conversion logic.
        /// </remarks>
        protected virtual object ConvertResourceFormat(object resource, Type targetType)
        {
            // Default: no conversion needed
            return resource;
        }

        /// <summary>
        /// Disposes of a resource.
        /// </summary>
        /// <remarks>
        /// Cleans up resource-specific data.
        /// Handles IDisposable and custom cleanup.
        /// </remarks>
        protected virtual void DisposeResource(object resource)
        {
            if (resource is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Gets a cache key for a resource.
        /// </summary>
        /// <remarks>
        /// Creates unique cache key from resource parameters.
        /// Ensures proper cache isolation.
        /// </remarks>
        protected virtual string GetCacheKey(string resourceName, string resourceType, Type targetType)
        {
            return $"{resourceName}:{resourceType ?? "default"}:{targetType?.Name ?? "any"}";
        }

        /// <summary>
        /// Estimates cache memory usage.
        /// </summary>
        /// <remarks>
        /// Rough estimate of cache memory usage.
        /// Engine-specific implementations can be more accurate.
        /// </remarks>
        protected virtual long EstimateCacheSize()
        {
            // Rough estimate: assume 1KB per cached resource
            return _resourceCache.Count * 1024;
        }

        /// <summary>
        /// Called when a resource is loaded.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific post-load logic.
        /// Subclasses can override for additional processing.
        /// </remarks>
        protected virtual void OnResourceLoaded(string resourceName, string resourceType, object resource)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when a resource is unloaded.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific cleanup logic.
        /// Subclasses can override for additional cleanup.
        /// </remarks>
        protected virtual void OnResourceUnloaded(string resourceName, string resourceType, object resource)
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Called when cache is cleared.
        /// </summary>
        /// <remarks>
        /// Hook for engine-specific cache clear logic.
        /// Subclasses can override for additional cleanup.
        /// </remarks>
        protected virtual void OnCacheCleared()
        {
            // Default: no additional logic
        }

        /// <summary>
        /// Preloads critical resources.
        /// </summary>
        /// <remarks>
        /// Loads essential resources upfront.
        /// Improves loading performance during gameplay.
        /// Engine-specific resource lists.
        /// </remarks>
        public virtual void PreloadCriticalResources(IEnumerable<string> resourceNames)
        {
            foreach (var resourceName in resourceNames)
            {
                // Asynchronous preloading could be implemented here
                LoadResource<object>(resourceName);
            }
        }
    }

    /// <summary>
    /// Content management statistics.
    /// </summary>
    public struct ContentStats
    {
        /// <summary>
        /// Number of cached resources.
        /// </summary>
        public int CachedResourceCount;

        /// <summary>
        /// Estimated cache size in bytes.
        /// </summary>
        public long CacheSizeBytes;
    }
}
