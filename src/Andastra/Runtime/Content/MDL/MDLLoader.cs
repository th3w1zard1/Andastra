using System;
using System.IO;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Content.Interfaces;
using Andastra.Runtime.Core.MDL;
using ContentMDLAnimationData = Andastra.Runtime.Content.MDL.MDLAnimationData;
using ContentMDLControllerData = Andastra.Runtime.Content.MDL.MDLControllerData;
using ContentMDLDanglymeshData = Andastra.Runtime.Content.MDL.MDLDanglymeshData;
using ContentMDLEmitterData = Andastra.Runtime.Content.MDL.MDLEmitterData;
using ContentMDLEventData = Andastra.Runtime.Content.MDL.MDLEventData;
using ContentMDLFaceData = Andastra.Runtime.Content.MDL.MDLFaceData;
using ContentMDLLightData = Andastra.Runtime.Content.MDL.MDLLightData;
using ContentMDLMeshData = Andastra.Runtime.Content.MDL.MDLMeshData;
using ContentMDLModel = Andastra.Runtime.Content.MDL.MDLModel;
using ContentMDLNodeData = Andastra.Runtime.Content.MDL.MDLNodeData;
using ContentMDLReferenceData = Andastra.Runtime.Content.MDL.MDLReferenceData;
using ContentMDLSkinData = Andastra.Runtime.Content.MDL.MDLSkinData;
using ContentVector2Data = Andastra.Runtime.Content.MDL.Vector2Data;
using ContentVector3Data = Andastra.Runtime.Content.MDL.Vector3Data;
using ContentVector4Data = Andastra.Runtime.Content.MDL.Vector4Data;
using CoreMDLAnimationData = Andastra.Runtime.Core.MDL.MDLAnimationData;
using CoreMDLControllerData = Andastra.Runtime.Core.MDL.MDLControllerData;
using CoreMDLDanglymeshData = Andastra.Runtime.Core.MDL.MDLDanglymeshData;
using CoreMDLEmitterData = Andastra.Runtime.Core.MDL.MDLEmitterData;
using CoreMDLEventData = Andastra.Runtime.Core.MDL.MDLEventData;
using CoreMDLFaceData = Andastra.Runtime.Core.MDL.MDLFaceData;
using CoreMDLLightData = Andastra.Runtime.Core.MDL.MDLLightData;
using CoreMDLMeshData = Andastra.Runtime.Core.MDL.MDLMeshData;
using CoreMDLModel = Andastra.Runtime.Core.MDL.MDLModel;
using CoreMDLNodeData = Andastra.Runtime.Core.MDL.MDLNodeData;
using CoreMDLReferenceData = Andastra.Runtime.Core.MDL.MDLReferenceData;
using CoreMDLSkinData = Andastra.Runtime.Core.MDL.MDLSkinData;
using CoreVector2Data = Andastra.Runtime.Core.MDL.Vector2Data;
using CoreVector3Data = Andastra.Runtime.Core.MDL.Vector3Data;
using CoreVector4Data = Andastra.Runtime.Core.MDL.Vector4Data;

namespace Andastra.Runtime.Content.MDL
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
    public sealed class MDLLoader : IMDLLoader
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
        public Andastra.Runtime.Core.MDL.MDLModel Load(string resRef)
        {
            if (string.IsNullOrEmpty(resRef))
            {
                throw new ArgumentNullException(nameof(resRef));
            }

            string normalizedRef = resRef.ToLowerInvariant();

            // Check cache first
            if (_useCache)
            {
                ContentMDLModel cached;
                if (MDLCache.Instance.TryGet(normalizedRef, out cached))
                {
                    return MDLLoader.ConvertToCoreModel(cached);
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
                ContentMDLModel contentModel;

                if (_useOptimizedReader)
                {
                    // Use ultra-optimized unsafe reader (fastest)
                    using (var reader = new MDLOptimizedReader(mdlData, mdxData))
                    {
                        contentModel = reader.Load();
                    }
                }
                else if (_useBulkReader)
                {
                    // Use optimized bulk reader
                    using (var reader = new MDLBulkReader(mdlData, mdxData))
                    {
                        contentModel = reader.Load();
                    }
                }
                else
                {
                    // Fall back to streaming reader
                    using (var reader = new MDLFastReader(mdlData, mdxData))
                    {
                        contentModel = reader.Load();
                    }
                }

                if (contentModel == null)
                {
                    return null;
                }

                // Convert Content.MDL.MDLModel to Core.MDL.MDLModel
                CoreMDLModel coreModel = MDLLoader.ConvertToCoreModel(contentModel);

                // Add to cache (cache uses Content model)
                if (_useCache)
                {
                    MDLCache.Instance.Add(normalizedRef, contentModel);
                }

                return coreModel;
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

        /// <summary>
        /// Converts a Content.MDL.MDLModel to Core.MDL.MDLModel.
        /// This is a public static helper method that can be used to convert Content MDL models to Core MDL models
        /// without requiring an MDLLoader instance.
        /// </summary>
        /// <param name="contentModel">The Content.MDL.MDLModel to convert</param>
        /// <returns>The converted Core.MDL.MDLModel, or null if contentModel is null</returns>
        public static CoreMDLModel ConvertToCoreModel(ContentMDLModel contentModel)
        {
            if (contentModel == null)
            {
                return null;
            }

            // Convert Content.MDL.MDLModel to Core.MDL.MDLModel
            // Since they have the same structure, we can copy the fields
            var coreModel = new CoreMDLModel
            {
                Name = contentModel.Name,
                Supermodel = contentModel.Supermodel,
                Classification = contentModel.Classification,
                SubClassification = contentModel.SubClassification,
                AffectedByFog = contentModel.AffectedByFog,
                BoundingBoxMin = new CoreVector3Data(contentModel.BoundingBoxMin.X, contentModel.BoundingBoxMin.Y, contentModel.BoundingBoxMin.Z),
                BoundingBoxMax = new CoreVector3Data(contentModel.BoundingBoxMax.X, contentModel.BoundingBoxMax.Y, contentModel.BoundingBoxMax.Z),
                Radius = contentModel.Radius,
                AnimationScale = contentModel.AnimationScale,
                NodeCount = contentModel.NodeCount,
                AnimationArrayOffset = contentModel.AnimationArrayOffset,
                AnimationCount = contentModel.AnimationCount,
                RootNode = ConvertNode(contentModel.RootNode),
                Animations = ConvertAnimations(contentModel.Animations)
            };
            return coreModel;
        }

        /// <summary>
        /// Converts a Content.MDL.MDLNodeData to Core.MDL.MDLNodeData recursively.
        /// </summary>
        private static CoreMDLNodeData ConvertNode(ContentMDLNodeData contentNode)
        {
            if (contentNode == null)
            {
                return null;
            }

            var coreNode = new CoreMDLNodeData
            {
                Name = contentNode.Name,
                NodeType = contentNode.NodeType,
                NodeIndex = contentNode.NodeIndex,
                NameIndex = contentNode.NameIndex,
                Position = new CoreVector3Data(contentNode.Position.X, contentNode.Position.Y, contentNode.Position.Z),
                Orientation = new CoreVector4Data(contentNode.Orientation.X, contentNode.Orientation.Y, contentNode.Orientation.Z, contentNode.Orientation.W),
                Controllers = ConvertControllers(contentNode.Controllers),
                Mesh = ConvertMesh(contentNode.Mesh),
                Light = ConvertLight(contentNode.Light),
                Emitter = ConvertEmitter(contentNode.Emitter),
                Reference = ConvertReference(contentNode.Reference)
            };

            // Convert children recursively
            if (contentNode.Children != null && contentNode.Children.Length > 0)
            {
                coreNode.Children = new CoreMDLNodeData[contentNode.Children.Length];
                for (int i = 0; i < contentNode.Children.Length; i++)
                {
                    coreNode.Children[i] = ConvertNode(contentNode.Children[i]);
                }
            }

            return coreNode;
        }

        /// <summary>
        /// Converts Content.MDL.MDLAnimationData array to Core.MDL.MDLAnimationData array.
        /// </summary>
        private static CoreMDLAnimationData[] ConvertAnimations(ContentMDLAnimationData[] contentAnimations)
        {
            if (contentAnimations == null || contentAnimations.Length == 0)
            {
                return null;
            }

            var coreAnimations = new CoreMDLAnimationData[contentAnimations.Length];
            for (int i = 0; i < contentAnimations.Length; i++)
            {
                var contentAnim = contentAnimations[i];
                if (contentAnim != null)
                {
                    coreAnimations[i] = new CoreMDLAnimationData
                    {
                        Name = contentAnim.Name,
                        AnimRoot = contentAnim.AnimRoot,
                        Length = contentAnim.Length,
                        TransitionTime = contentAnim.TransitionTime,
                        Events = ConvertEvents(contentAnim.Events),
                        RootNode = ConvertNode(contentAnim.RootNode)
                    };
                }
            }
            return coreAnimations;
        }

        /// <summary>
        /// Converts Content.MDL.MDLEventData array to Core.MDL.MDLEventData array.
        /// </summary>
        private static CoreMDLEventData[] ConvertEvents(ContentMDLEventData[] contentEvents)
        {
            if (contentEvents == null || contentEvents.Length == 0)
            {
                return null;
            }

            var coreEvents = new CoreMDLEventData[contentEvents.Length];
            for (int i = 0; i < contentEvents.Length; i++)
            {
                var contentEvent = contentEvents[i];
                coreEvents[i] = new CoreMDLEventData
                {
                    ActivationTime = contentEvent.ActivationTime,
                    Name = contentEvent.Name
                };
            }
            return coreEvents;
        }

        /// <summary>
        /// Converts Content.MDL.MDLControllerData array to Core.MDL.MDLControllerData array.
        /// </summary>
        private static CoreMDLControllerData[] ConvertControllers(ContentMDLControllerData[] contentControllers)
        {
            if (contentControllers == null || contentControllers.Length == 0)
            {
                return null;
            }

            var coreControllers = new CoreMDLControllerData[contentControllers.Length];
            for (int i = 0; i < contentControllers.Length; i++)
            {
                var contentController = contentControllers[i];
                if (contentController != null)
                {
                    coreControllers[i] = new CoreMDLControllerData
                    {
                        Type = contentController.Type,
                        RowCount = contentController.RowCount,
                        TimeIndex = contentController.TimeIndex,
                        DataIndex = contentController.DataIndex,
                        ColumnCount = contentController.ColumnCount,
                        IsBezier = contentController.IsBezier,
                        TimeKeys = contentController.TimeKeys != null ? (float[])contentController.TimeKeys.Clone() : null,
                        Values = contentController.Values != null ? (float[])contentController.Values.Clone() : null
                    };
                }
            }
            return coreControllers;
        }

        /// <summary>
        /// Converts Content.MDL.MDLMeshData to Core.MDL.MDLMeshData.
        /// </summary>
        private static CoreMDLMeshData ConvertMesh(ContentMDLMeshData contentMesh)
        {
            if (contentMesh == null)
            {
                return null;
            }

            var coreMesh = new CoreMDLMeshData
            {
                BoundingBoxMin = new CoreVector3Data(contentMesh.BoundingBoxMin.X, contentMesh.BoundingBoxMin.Y, contentMesh.BoundingBoxMin.Z),
                BoundingBoxMax = new CoreVector3Data(contentMesh.BoundingBoxMax.X, contentMesh.BoundingBoxMax.Y, contentMesh.BoundingBoxMax.Z),
                Radius = contentMesh.Radius,
                AveragePoint = new CoreVector3Data(contentMesh.AveragePoint.X, contentMesh.AveragePoint.Y, contentMesh.AveragePoint.Z),
                DiffuseColor = new CoreVector3Data(contentMesh.DiffuseColor.X, contentMesh.DiffuseColor.Y, contentMesh.DiffuseColor.Z),
                AmbientColor = new CoreVector3Data(contentMesh.AmbientColor.X, contentMesh.AmbientColor.Y, contentMesh.AmbientColor.Z),
                TransparencyHint = contentMesh.TransparencyHint,
                Texture0 = contentMesh.Texture0,
                Texture1 = contentMesh.Texture1,
                Texture2 = contentMesh.Texture2,
                Texture3 = contentMesh.Texture3,
                UVDirectionX = contentMesh.UVDirectionX,
                UVDirectionY = contentMesh.UVDirectionY,
                UVJitter = contentMesh.UVJitter,
                UVJitterSpeed = contentMesh.UVJitterSpeed,
                MDXVertexSize = contentMesh.MDXVertexSize,
                MDXDataFlags = contentMesh.MDXDataFlags,
                MDXDataOffset = contentMesh.MDXDataOffset,
                MDXPositionOffset = contentMesh.MDXPositionOffset,
                MDXNormalOffset = contentMesh.MDXNormalOffset,
                MDXColorOffset = contentMesh.MDXColorOffset,
                MDXTex0Offset = contentMesh.MDXTex0Offset,
                MDXTex1Offset = contentMesh.MDXTex1Offset,
                MDXTex2Offset = contentMesh.MDXTex2Offset,
                MDXTex3Offset = contentMesh.MDXTex3Offset,
                MDXTangentOffset = contentMesh.MDXTangentOffset,
                MDXUnknown1Offset = contentMesh.MDXUnknown1Offset,
                MDXUnknown2Offset = contentMesh.MDXUnknown2Offset,
                MDXUnknown3Offset = contentMesh.MDXUnknown3Offset,
                VertexCount = contentMesh.VertexCount,
                FaceCount = contentMesh.FaceCount,
                TextureCount = contentMesh.TextureCount,
                HasLightmap = contentMesh.HasLightmap,
                RotateTexture = contentMesh.RotateTexture,
                BackgroundGeometry = contentMesh.BackgroundGeometry,
                Shadow = contentMesh.Shadow,
                Beaming = contentMesh.Beaming,
                Render = contentMesh.Render,
                TotalArea = contentMesh.TotalArea,
                Positions = ConvertVector3Array(contentMesh.Positions),
                Normals = ConvertVector3Array(contentMesh.Normals),
                TexCoords0 = ConvertVector2Array(contentMesh.TexCoords0),
                TexCoords1 = ConvertVector2Array(contentMesh.TexCoords1),
                TexCoords2 = ConvertVector2Array(contentMesh.TexCoords2),
                TexCoords3 = ConvertVector2Array(contentMesh.TexCoords3),
                Colors = ConvertVector3Array(contentMesh.Colors),
                Tangents = ConvertVector3Array(contentMesh.Tangents),
                Bitangents = ConvertVector3Array(contentMesh.Bitangents),
                Indices = contentMesh.Indices != null ? (ushort[])contentMesh.Indices.Clone() : null,
                Faces = ConvertFaces(contentMesh.Faces),
                Skin = ConvertSkin(contentMesh.Skin),
                Danglymesh = ConvertDanglymesh(contentMesh.Danglymesh)
            };
            return coreMesh;
        }

        /// <summary>
        /// Converts Content.MDL.MDLFaceData array to Core.MDL.MDLFaceData array.
        /// </summary>
        private static CoreMDLFaceData[] ConvertFaces(ContentMDLFaceData[] contentFaces)
        {
            if (contentFaces == null || contentFaces.Length == 0)
            {
                return null;
            }

            var coreFaces = new CoreMDLFaceData[contentFaces.Length];
            for (int i = 0; i < contentFaces.Length; i++)
            {
                var contentFace = contentFaces[i];
                coreFaces[i] = new CoreMDLFaceData
                {
                    Normal = new CoreVector3Data(contentFace.Normal.X, contentFace.Normal.Y, contentFace.Normal.Z),
                    PlaneDistance = contentFace.PlaneDistance,
                    Material = contentFace.Material,
                    Adjacent0 = contentFace.Adjacent0,
                    Adjacent1 = contentFace.Adjacent1,
                    Adjacent2 = contentFace.Adjacent2,
                    Vertex0 = contentFace.Vertex0,
                    Vertex1 = contentFace.Vertex1,
                    Vertex2 = contentFace.Vertex2
                };
            }
            return coreFaces;
        }

        /// <summary>
        /// Converts Content.MDL.MDLSkinData to Core.MDL.MDLSkinData.
        /// </summary>
        private static CoreMDLSkinData ConvertSkin(ContentMDLSkinData contentSkin)
        {
            if (contentSkin == null)
            {
                return null;
            }

            return new CoreMDLSkinData
            {
                MDXBoneWeightsOffset = contentSkin.MDXBoneWeightsOffset,
                MDXBoneIndicesOffset = contentSkin.MDXBoneIndicesOffset,
                BoneWeights = contentSkin.BoneWeights != null ? (float[])contentSkin.BoneWeights.Clone() : null,
                BoneIndices = contentSkin.BoneIndices != null ? (int[])contentSkin.BoneIndices.Clone() : null,
                BoneMap = contentSkin.BoneMap != null ? (int[])contentSkin.BoneMap.Clone() : null,
                QBones = ConvertVector4Array(contentSkin.QBones),
                TBones = ConvertVector3Array(contentSkin.TBones)
            };
        }

        /// <summary>
        /// Converts Content.MDL.MDLDanglymeshData to Core.MDL.MDLDanglymeshData.
        /// </summary>
        private static CoreMDLDanglymeshData ConvertDanglymesh(ContentMDLDanglymeshData contentDanglymesh)
        {
            if (contentDanglymesh == null)
            {
                return null;
            }

            return new CoreMDLDanglymeshData
            {
                Constraints = contentDanglymesh.Constraints != null ? (float[])contentDanglymesh.Constraints.Clone() : null,
                Displacement = contentDanglymesh.Displacement,
                Tightness = contentDanglymesh.Tightness,
                Period = contentDanglymesh.Period
            };
        }

        /// <summary>
        /// Converts Content.MDL.MDLLightData to Core.MDL.MDLLightData.
        /// </summary>
        private static CoreMDLLightData ConvertLight(ContentMDLLightData contentLight)
        {
            if (contentLight == null)
            {
                return null;
            }

            return new CoreMDLLightData
            {
                FlareRadius = contentLight.FlareRadius,
                LightPriority = contentLight.LightPriority,
                AmbientOnly = contentLight.AmbientOnly,
                DynamicType = contentLight.DynamicType,
                AffectDynamic = contentLight.AffectDynamic,
                Shadow = contentLight.Shadow,
                Flare = contentLight.Flare,
                FadingLight = contentLight.FadingLight,
                FlareSizes = contentLight.FlareSizes != null ? (float[])contentLight.FlareSizes.Clone() : null,
                FlarePositions = contentLight.FlarePositions != null ? (float[])contentLight.FlarePositions.Clone() : null,
                FlareColorShifts = ConvertVector3Array(contentLight.FlareColorShifts),
                FlareTextures = contentLight.FlareTextures != null ? (string[])contentLight.FlareTextures.Clone() : null
            };
        }

        /// <summary>
        /// Converts Content.MDL.MDLEmitterData to Core.MDL.MDLEmitterData.
        /// </summary>
        private static CoreMDLEmitterData ConvertEmitter(ContentMDLEmitterData contentEmitter)
        {
            if (contentEmitter == null)
            {
                return null;
            }

            return new CoreMDLEmitterData
            {
                DeadSpace = contentEmitter.DeadSpace,
                BlastRadius = contentEmitter.BlastRadius,
                BlastLength = contentEmitter.BlastLength,
                BranchCount = contentEmitter.BranchCount,
                ControlPtSmoothing = contentEmitter.ControlPtSmoothing,
                XGrid = contentEmitter.XGrid,
                YGrid = contentEmitter.YGrid,
                UpdateScript = contentEmitter.UpdateScript,
                RenderScript = contentEmitter.RenderScript,
                BlendScript = contentEmitter.BlendScript,
                Texture = contentEmitter.Texture,
                ChunkName = contentEmitter.ChunkName,
                TwoSidedTex = contentEmitter.TwoSidedTex,
                Loop = contentEmitter.Loop,
                RenderOrder = contentEmitter.RenderOrder,
                FrameBlending = contentEmitter.FrameBlending,
                DepthTexture = contentEmitter.DepthTexture,
                Flags = contentEmitter.Flags
            };
        }

        /// <summary>
        /// Converts Content.MDL.MDLReferenceData to Core.MDL.MDLReferenceData.
        /// </summary>
        private static CoreMDLReferenceData ConvertReference(ContentMDLReferenceData contentReference)
        {
            if (contentReference == null)
            {
                return null;
            }

            return new CoreMDLReferenceData
            {
                ModelResRef = contentReference.ModelResRef,
                Reattachable = contentReference.Reattachable
            };
        }

        /// <summary>
        /// Converts Content.MDL.Vector3Data array to Core.MDL.Vector3Data array.
        /// </summary>
        private static CoreVector3Data[] ConvertVector3Array(ContentVector3Data[] contentVectors)
        {
            if (contentVectors == null || contentVectors.Length == 0)
            {
                return null;
            }

            var coreVectors = new CoreVector3Data[contentVectors.Length];
            for (int i = 0; i < contentVectors.Length; i++)
            {
                var contentVec = contentVectors[i];
                coreVectors[i] = new CoreVector3Data(contentVec.X, contentVec.Y, contentVec.Z);
            }
            return coreVectors;
        }

        /// <summary>
        /// Converts Content.MDL.Vector2Data array to Core.MDL.Vector2Data array.
        /// </summary>
        private static CoreVector2Data[] ConvertVector2Array(ContentVector2Data[] contentVectors)
        {
            if (contentVectors == null || contentVectors.Length == 0)
            {
                return null;
            }

            var coreVectors = new CoreVector2Data[contentVectors.Length];
            for (int i = 0; i < contentVectors.Length; i++)
            {
                var contentVec = contentVectors[i];
                coreVectors[i] = new CoreVector2Data(contentVec.X, contentVec.Y);
            }
            return coreVectors;
        }

        /// <summary>
        /// Converts Content.MDL.Vector4Data array to Core.MDL.Vector4Data array.
        /// </summary>
        private static CoreVector4Data[] ConvertVector4Array(ContentVector4Data[] contentVectors)
        {
            if (contentVectors == null || contentVectors.Length == 0)
            {
                return null;
            }

            var coreVectors = new CoreVector4Data[contentVectors.Length];
            for (int i = 0; i < contentVectors.Length; i++)
            {
                var contentVec = contentVectors[i];
                coreVectors[i] = new CoreVector4Data(contentVec.X, contentVec.Y, contentVec.Z, contentVec.W);
            }
            return coreVectors;
        }
    }
}


