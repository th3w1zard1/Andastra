using System;
using System.IO;
using AuroraEngine.Common.Resources;
using Odyssey.Content.Interfaces;

namespace Odyssey.Content.MDL
{
    /// <summary>
    /// High-level MDL model loader that integrates with the resource provider system.
    /// </summary>
    /// <remarks>
    /// MDL Model Loader:
    /// - Based on swkotor2.exe model loading system
    /// - Located via string references: "ModelName" @ 0x007c1c8c, "Model" @ 0x007c1ca8, "ModelResRef" @ 0x007c2f6c
    /// - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (model loading error)
    /// - "Model %s nor the default model %s could be loaded." @ 0x007cad14 (model loading fallback error)
    /// - "Cannot load door model '%s'." @ 0x007d2488 (door model loading error)
    /// - "CSWCVisualEffect::LoadModel: Failed to load visual effect model '%s'." @ 0x007cd5a8 (VFX model error)
    /// - "CSWCCreatureAppearance::CreateBTypeBody(): Failed to load model '%s'." @ 0x007cdc40 (body model error)
    /// - Model loading: FUN_005261b0 @ 0x005261b0 loads creature models from appearance.2da
    /// - Original implementation: Loads MDL (model definition) and MDX (geometry data) files
    /// - Model resolution: Resolves model ResRefs from appearance.2da (ModelA, ModelB columns for variants)
    /// - Fallback models: Uses default models when specified model cannot be loaded
    /// - MDL files contain: Model structure, nodes, animations, bounding boxes, classification
    /// - MDX files contain: Vertex data, texture coordinates, normals, face indices
    /// - Original engine uses binary file format with specific offsets and structures
    /// 
    /// Optimization features:
    /// - Automatic model caching (configurable via UseCache property)
    /// - Ultra-optimized unsafe reader (MDLOptimizedReader) for maximum performance
    /// - Falls back to MDLBulkReader or MDLFastReader based on configuration
    /// 
    /// Performance characteristics:
    /// - Bulk read of entire MDL/MDX files into memory
    /// - Unsafe pointer operations for zero-copy direct memory access
    /// - Pre-allocated arrays based on header counts
    /// - Pre-computed vertex attribute offsets for single-pass reading
    /// - LRU cache with configurable size (default 100 models)
    /// 
    /// Reader selection (configurable via properties):
    /// - MDLOptimizedReader (default): Fastest, uses unsafe code and zero-copy operations
    /// - MDLBulkReader: Good performance with safe code, bulk operations
    /// - MDLFastReader: Stream-based loading for low-memory scenarios
    /// 
    /// Reference: KotOR.js MDLLoader.ts, reone mdlmdxreader.cpp, MDLOps
    /// Based on MDL file format documentation in vendor/PyKotor/wiki/MDL-MDX-File-Format.md
    /// </remarks>
    public sealed class MDLLoader
    {
        private readonly IResourceProvider _resourceProvider;
        private bool _useCache;
        private bool _useBulkReader;
        private bool _useOptimizedReader;

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
        /// Gets or sets whether to use the optimized unsafe reader (fastest). Default is true.
        /// The optimized reader uses unsafe code and zero-copy operations for maximum performance.
        /// Requires unsafe code to be enabled in the project.
        /// </summary>
        public bool UseOptimizedReader
        {
            get { return _useOptimizedReader; }
            set { _useOptimizedReader = value; }
        }

        /// <summary>
        /// Creates a new MDL loader using the specified resource provider.
        /// </summary>
        /// <param name="resourceProvider">Resource provider for loading MDL/MDX files</param>
        public MDLLoader(IResourceProvider resourceProvider)
        {
            if (resourceProvider == null)
            {
                throw new ArgumentNullException(nameof(resourceProvider));
            }
            _resourceProvider = resourceProvider;
            _useCache = true;
            _useBulkReader = true;
            _useOptimizedReader = true;
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
                throw new ArgumentNullException(nameof(resRef));
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

                if (_useOptimizedReader)
                {
                    // Use ultra-optimized unsafe reader (fastest)
                    using (var reader = new MDLOptimizedReader(mdlData, mdxData))
                    {
                        model = reader.Load();
                    }
                }
                else if (_useBulkReader)
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
        /// Reads the entire resource stream into a byte array.
        /// </summary>
        /// <param name="resRef">Resource reference (case-sensitive)</param>
        /// <param name="resType">Resource type (MDL or MDX)</param>
        /// <returns>Resource data as byte array, or null if resource not found</returns>
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
        /// Loads an MDL model from file paths using the optimized unsafe reader (fastest).
        /// </summary>
        /// <param name="mdlPath">Path to the MDL file</param>
        /// <param name="mdxPath">Path to the MDX file</param>
        /// <returns>Loaded MDL model</returns>
        /// <exception cref="ArgumentNullException">Thrown when mdlPath or mdxPath is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the specified path is invalid.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs while reading the file.</exception>
        /// <exception cref="InvalidDataException">Thrown when the MDL or MDX file is corrupted or has invalid data.</exception>
        public static MDLModel LoadFromFiles(string mdlPath, string mdxPath)
        {
            if (string.IsNullOrEmpty(mdlPath))
            {
                throw new ArgumentNullException(nameof(mdlPath));
            }
            if (string.IsNullOrEmpty(mdxPath))
            {
                throw new ArgumentNullException(nameof(mdxPath));
            }

            using (var reader = new MDLOptimizedReader(mdlPath, mdxPath))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads an MDL model from byte arrays using the optimized unsafe reader (fastest).
        /// This is the most efficient method when you already have the file data in memory.
        /// </summary>
        /// <param name="mdlData">MDL file data</param>
        /// <param name="mdxData">MDX file data</param>
        /// <returns>Loaded MDL model</returns>
        /// <exception cref="ArgumentNullException">Thrown when mdlData or mdxData is null.</exception>
        /// <exception cref="InvalidDataException">Thrown when the MDL or MDX file is corrupted or has invalid data.</exception>
        /// <exception cref="InvalidOperationException">Thrown when data size calculations overflow or array bounds are exceeded.</exception>
        public static MDLModel LoadFromBytes(byte[] mdlData, byte[] mdxData)
        {
            if (mdlData == null)
            {
                throw new ArgumentNullException(nameof(mdlData));
            }
            if (mdxData == null)
            {
                throw new ArgumentNullException(nameof(mdxData));
            }

            using (var reader = new MDLOptimizedReader(mdlData, mdxData))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads an MDL model from streams using the optimized unsafe reader (fastest).
        /// Note: This method reads streams into memory first for bulk processing.
        /// </summary>
        /// <param name="mdlStream">MDL stream</param>
        /// <param name="mdxStream">MDX stream</param>
        /// <param name="ownsStreams">If true, streams will be disposed after loading</param>
        /// <returns>Loaded MDL model</returns>
        /// <exception cref="ArgumentNullException">Thrown when mdlStream or mdxStream is null.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs while reading the stream.</exception>
        /// <exception cref="InvalidDataException">Thrown when the MDL or MDX file is corrupted or has invalid data.</exception>
        /// <exception cref="InvalidOperationException">Thrown when data size calculations overflow or array bounds are exceeded.</exception>
        public static MDLModel LoadFromStreams(Stream mdlStream, Stream mdxStream, bool ownsStreams = true)
        {
            if (mdlStream == null)
            {
                throw new ArgumentNullException(nameof(mdlStream));
            }
            if (mdxStream == null)
            {
                throw new ArgumentNullException(nameof(mdxStream));
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

                using (var reader = new MDLOptimizedReader(mdlData, mdxData))
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
        /// This removes all cached models from memory.
        /// </summary>
        public static void ClearCache()
        {
            MDLCache.Instance.Clear();
        }

        /// <summary>
        /// Sets the maximum number of models to cache.
        /// When the cache exceeds this limit, least recently used models are evicted.
        /// </summary>
        /// <param name="maxEntries">Maximum number of cached models (must be at least 1)</param>
        /// <exception cref="ArgumentException">Thrown when maxEntries is less than 1 (handled internally, value is clamped to 1)</exception>
        public static void SetCacheSize(int maxEntries)
        {
            MDLCache.Instance.MaxEntries = maxEntries;
        }
    }
}

