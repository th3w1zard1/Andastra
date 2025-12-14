using System;
using System.IO;
using CSharpKOTOR.Resources;
using Odyssey.Content.Interfaces;

namespace Odyssey.Content.MDL
{
    /// <summary>
    /// High-level MDL model loader that integrates with the resource provider system.
    /// 
    /// Optimization features:
    /// - Automatic model caching (configurable via UseCache property)
    /// - Bulk I/O operations using MDLBulkReader for best performance
    /// - Falls back to MDLFastReader for stream-based loading
    /// 
    /// Usage:
    /// <code>
    /// var loader = new MDLLoader(resourceProvider);
    /// var model = loader.Load("c_hutt");
    /// </code>
    /// 
    /// Performance characteristics:
    /// - Bulk read of entire MDL/MDX files into memory
    /// - Pre-allocated arrays based on header counts
    /// - Zero-copy struct reading where possible
    /// - LRU cache with configurable size (default 100 models)
    /// 
    /// Reference: KotOR.js MDLLoader.ts, reone mdlmdxreader.cpp
    /// </summary>
    public sealed class MDLLoader
    {
        private readonly IResourceProvider _resourceProvider;
        private bool _useCache;
        private bool _useBulkReader;

        /// <summary>
        /// Gets or sets whether to use the model cache. Default is true.
        /// </summary>
        public bool UseCache
        {
            get { return _useCache; }
            set { _useCache = value; }
        }

        /// <summary>
        /// Gets or sets whether to use the bulk reader (recommended). Default is true.
        /// The bulk reader provides better performance for most scenarios.
        /// </summary>
        public bool UseBulkReader
        {
            get { return _useBulkReader; }
            set { _useBulkReader = value; }
        }

        /// <summary>
        /// Creates a new MDL loader using the specified resource provider.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading MDL/MDX files</param>
        public MDLLoader(IResourceProvider resourceProvider)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException("resourceProvider");
            }
            _resourceProvider = resourceProvider;
            _useCache = true;
            _useBulkReader = true;
        }

        /// <summary>
        /// Loads an MDL model by ResRef with optional caching.
        /// </summary>
        /// <param name="resRef">Resource reference (model name without extension)</param>
        /// <returns>Loaded MDL model, or null if not found</returns>
        public MDLModel Load(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                throw new ArgumentNullException("resRef");
            }

            string normalizedRef = resRef.ToLowerInvariant();

            // Check cache first
            if (_useCache)
            {
                MDLModel cached;
                if (MDLCache.Instance.TryGet(normalizedRef, out cached))
                {
                    return cached;
                }
            }

            // Get MDL data
            byte[] mdlData = GetResourceData(resRef, ResourceType.MDL);
            if (mdlData == null || mdlData.Length == 0)
            {
                Console.WriteLine("[MDLLoader] MDL not found: " + resRef);
                return null;
            }

            // Get MDX data
            byte[] mdxData = GetResourceData(resRef, ResourceType.MDX);
            if (mdxData == null || mdxData.Length == 0)
            {
                Console.WriteLine("[MDLLoader] MDX not found: " + resRef);
                return null;
            }

            try
            {
                MDLModel model;

                if (_useBulkReader)
                {
                    // Use optimized bulk reader
                    using (var reader = new MDLBulkReader(mdlData, mdxData))
                    {
                        model = reader.Load();
                    }
                }
                else
                {
                    // Fall back to streaming reader
                    using (var reader = new MDLFastReader(mdlData, mdxData))
                    {
                        model = reader.Load();
                    }
                }

                // Add to cache
                if (_useCache && model != null)
                {
                    MDLCache.Instance.Add(normalizedRef, model);
                }

                return model;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MDLLoader] Failed to load model '" + resRef + "': " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Helper method to get resource data from provider.
        /// </summary>
        private byte[] GetResourceData(string resRef, ResourceType resType)
        {
            var id = new ResourceIdentifier(resRef, resType);
            Stream stream;

            if (!_resourceProvider.TryOpen(id, out stream))
            {
                return null;
            }

            try
            {
                using (var ms = new MemoryStream())
                {
                    stream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            finally
            {
                stream.Dispose();
            }
        }

        /// <summary>
        /// Loads an MDL model from file paths using the optimized bulk reader.
        /// </summary>
        /// <param name="mdlPath">Path to the MDL file</param>
        /// <param name="mdxPath">Path to the MDX file</param>
        /// <returns>Loaded MDL model</returns>
        public static MDLModel LoadFromFiles(string mdlPath, string mdxPath)
        {
            if (string.IsNullOrEmpty(mdlPath))
            {
                throw new ArgumentNullException("mdlPath");
            }
            if (string.IsNullOrEmpty(mdxPath))
            {
                throw new ArgumentNullException("mdxPath");
            }

            using (var reader = new MDLBulkReader(mdlPath, mdxPath))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads an MDL model from byte arrays using the optimized bulk reader.
        /// </summary>
        /// <param name="mdlData">MDL file data</param>
        /// <param name="mdxData">MDX file data</param>
        /// <returns>Loaded MDL model</returns>
        public static MDLModel LoadFromBytes(byte[] mdlData, byte[] mdxData)
        {
            if (mdlData == null)
            {
                throw new ArgumentNullException("mdlData");
            }
            if (mdxData == null)
            {
                throw new ArgumentNullException("mdxData");
            }

            using (var reader = new MDLBulkReader(mdlData, mdxData))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads an MDL model from streams.
        /// Note: This method reads streams into memory first for bulk processing.
        /// </summary>
        /// <param name="mdlStream">MDL stream</param>
        /// <param name="mdxStream">MDX stream</param>
        /// <param name="ownsStreams">If true, streams will be disposed after loading</param>
        /// <returns>Loaded MDL model</returns>
        public static MDLModel LoadFromStreams(Stream mdlStream, Stream mdxStream, bool ownsStreams = true)
        {
            if (mdlStream == null)
            {
                throw new ArgumentNullException("mdlStream");
            }
            if (mdxStream == null)
            {
                throw new ArgumentNullException("mdxStream");
            }

            try
            {
                // Read streams into byte arrays for bulk processing
                byte[] mdlData;
                byte[] mdxData;

                using (var ms = new MemoryStream())
                {
                    mdlStream.CopyTo(ms);
                    mdlData = ms.ToArray();
                }

                using (var ms = new MemoryStream())
                {
                    mdxStream.CopyTo(ms);
                    mdxData = ms.ToArray();
                }

                using (var reader = new MDLBulkReader(mdlData, mdxData))
                {
                    return reader.Load();
                }
            }
            finally
            {
                if (ownsStreams)
                {
                    mdlStream.Dispose();
                    mdxStream.Dispose();
                }
            }
        }

        /// <summary>
        /// Clears the model cache.
        /// </summary>
        public static void ClearCache()
        {
            MDLCache.Instance.Clear();
        }

        /// <summary>
        /// Sets the maximum number of models to cache.
        /// </summary>
        /// <param name="maxEntries">Maximum number of cached models</param>
        public static void SetCacheSize(int maxEntries)
        {
            MDLCache.Instance.MaxEntries = maxEntries;
        }
    }
}

