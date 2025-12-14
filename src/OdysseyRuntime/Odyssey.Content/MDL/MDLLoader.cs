using System;
using System.IO;
using Odyssey.Content.Interfaces;

namespace Odyssey.Content.MDL
{
    /// <summary>
    /// High-level MDL model loader that integrates with the resource provider system.
    /// 
    /// Usage:
    /// <code>
    /// var loader = new MDLLoader(resourceProvider);
    /// var model = loader.Load("c_hutt");
    /// </code>
    /// 
    /// Performance characteristics:
    /// - Buffered I/O with 64KB buffers for efficient disk access
    /// - Pre-allocated arrays to minimize GC pressure
    /// - Single-pass header reading
    /// - Direct MDX vertex data extraction
    /// </summary>
    public sealed class MDLLoader
    {
        private readonly IResourceProvider _resourceProvider;

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
        }

        /// <summary>
        /// Loads an MDL model by ResRef.
        /// </summary>
        /// <param name="resRef">Resource reference (model name without extension)</param>
        /// <returns>Loaded MDL model, or null if not found</returns>
        public MDLModel Load(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                throw new ArgumentNullException("resRef");
            }

            // Get MDL data
            byte[] mdlData = _resourceProvider.GetResource(resRef, "mdl");
            if (mdlData == null || mdlData.Length == 0)
            {
                Console.WriteLine("[MDLLoader] MDL not found: " + resRef);
                return null;
            }

            // Get MDX data
            byte[] mdxData = _resourceProvider.GetResource(resRef, "mdx");
            if (mdxData == null || mdxData.Length == 0)
            {
                Console.WriteLine("[MDLLoader] MDX not found: " + resRef);
                return null;
            }

            try
            {
                using (var reader = new MDLFastReader(mdlData, mdxData))
                {
                    return reader.Load();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[MDLLoader] Failed to load model '" + resRef + "': " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Loads an MDL model from file paths.
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

            using (var reader = new MDLFastReader(mdlPath, mdxPath))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads an MDL model from byte arrays.
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

            using (var reader = new MDLFastReader(mdlData, mdxData))
            {
                return reader.Load();
            }
        }

        /// <summary>
        /// Loads an MDL model from streams.
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

            using (var reader = new MDLFastReader(mdlStream, mdxStream, ownsStreams))
            {
                return reader.Load();
            }
        }
    }
}

