using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Andastra.Runtime.Content.MDL
{
    /// <summary>
    /// High-performance MDL/MDX binary reader optimized for game asset loading.
    /// </summary>
    /// <remarks>
    /// MDL/MDX Fast Reader:
    /// - Based on swkotor2.exe MDL/MDX file format and loading system
    /// - Located via string references: "ModelName" @ 0x007c1c8c, "Model" @ 0x007c1ca8, "ModelResRef" @ 0x007c2f6c
    /// - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (model loading error)
    /// - "Model %s nor the default model %s could be loaded." @ 0x007cad14 (model loading fallback error)
    /// - "DoubleMdlVar" @ 0x007d05d8, "ShortMdlVar" @ 0x007d05e8, "LongMdlVar" @ 0x007d05f4 (MDL variable types)
    /// - Model loading: FUN_005261b0 @ 0x005261b0 loads creature models from appearance.2da
    /// - Original implementation: Reads MDL (model definition) and MDX (geometry) binary files using buffered I/O
    /// - MDL file structure: Header, geometry header, model header, names header, node tree, animations
    /// - MDX file structure: Vertex data arrays (positions, normals, UVs, face indices)
    /// - File format: Binary format with specific offsets, counts, and pointer structures
    /// 
    /// Performance optimizations implemented:
    /// 1. Buffered reading with configurable buffer size (default 64KB)
    /// 2. Pre-allocated arrays based on header counts to minimize GC pressure
    /// 3. Single-pass header reading followed by batch data loading
    /// 4. Reusable byte arrays for string parsing
    /// 5. Direct struct reading without intermediate boxing
    /// 
    /// Reference implementations analyzed:
    /// - reone: Uses pre-sized buffers and reads data in chunks
    /// - KotOR.js: Uses typed arrays and batch operations
    /// - Kotor.NET: Separates header parsing from data reading
    /// - MDLOps: Memory-efficient data structures with exact field sizes
    /// </remarks>
    public sealed class MDLFastReader : IDisposable
    {
        private const int DEFAULT_BUFFER_SIZE = 65536; // 64KB buffer for efficient I/O
        private const int STRING_BUFFER_SIZE = 64;     // Max string length in MDL

        private readonly BinaryReader _mdlReader;
        private readonly BinaryReader _mdxReader;
        private readonly byte[] _stringBuffer;
        private readonly bool _ownsStreams;
        private readonly int _mdlBaseOffset;
        private readonly int _mdxBaseOffset;

        private bool _disposed;
        private bool _isTSL;

        // Cached header data
        private int _mdlSize;
        private int _mdxSize;
        private string[] _nodeNames;
        private int _rootNodeOffset;

        /// <summary>
        /// True if the model is from KotOR 2: The Sith Lords.
        /// </summary>
        public bool IsTSL
        {
            get { return _isTSL; }
        }

        /// <summary>
        /// Creates a new MDL reader from file paths.
        /// </summary>
        /// <param name="mdlPath">Path to the MDL file</param>
        /// <param name="mdxPath">Path to the MDX file</param>
        public MDLFastReader(string mdlPath, string mdxPath)
        {
            if (string.IsNullOrEmpty(mdlPath))
                throw new ArgumentNullException(nameof(mdlPath));
            if (string.IsNullOrEmpty(mdxPath))
                throw new ArgumentNullException(nameof(mdxPath));

            _mdlReader = new BinaryReader(
                new BufferedStream(File.OpenRead(mdlPath), DEFAULT_BUFFER_SIZE),
                Encoding.ASCII);
            _mdxReader = new BinaryReader(
                new BufferedStream(File.OpenRead(mdxPath), DEFAULT_BUFFER_SIZE),
                Encoding.ASCII);
            _stringBuffer = new byte[STRING_BUFFER_SIZE];
            _ownsStreams = true;
            _mdlBaseOffset = 0;
            _mdxBaseOffset = 0;
        }

        /// <summary>
        /// Creates a new MDL reader from byte arrays.
        /// </summary>
        /// <param name="mdlData">MDL file data</param>
        /// <param name="mdxData">MDX file data</param>
        public MDLFastReader(byte[] mdlData, byte[] mdxData)
        {
            if (mdlData == null)
                throw new ArgumentNullException(nameof(mdlData));
            if (mdxData == null)
                throw new ArgumentNullException(nameof(mdxData));

            _mdlReader = new BinaryReader(new MemoryStream(mdlData, false), Encoding.ASCII);
            _mdxReader = new BinaryReader(new MemoryStream(mdxData, false), Encoding.ASCII);
            _stringBuffer = new byte[STRING_BUFFER_SIZE];
            _ownsStreams = true;
            _mdlBaseOffset = 0;
            _mdxBaseOffset = 0;
        }

        /// <summary>
        /// Creates a new MDL reader from streams.
        /// </summary>
        /// <param name="mdlStream">MDL stream</param>
        /// <param name="mdxStream">MDX stream</param>
        /// <param name="ownsStreams">If true, streams will be disposed when reader is disposed</param>
        public MDLFastReader(Stream mdlStream, Stream mdxStream, bool ownsStreams = false)
        {
            if (mdlStream == null)
                throw new ArgumentNullException(nameof(mdlStream));
            if (mdxStream == null)
                throw new ArgumentNullException(nameof(mdxStream));

            _mdlReader = new BinaryReader(mdlStream, Encoding.ASCII, !ownsStreams);
            _mdxReader = new BinaryReader(mdxStream, Encoding.ASCII, !ownsStreams);
            _stringBuffer = new byte[STRING_BUFFER_SIZE];
            _ownsStreams = ownsStreams;
            _mdlBaseOffset = 0;
            _mdxBaseOffset = 0;
        }

        /// <summary>
        /// Loads the complete MDL model.
        /// </summary>
        /// <returns>Parsed MDL model data</returns>
        public MDLModel Load()
        {
            var model = new MDLModel();

            // Phase 1: Read file header (12 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - MDL File Header
            ReadFileHeader(out _mdlSize, out _mdxSize);

            // Phase 2: Read geometry header (80 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Geometry Header
            ReadGeometryHeader(model);

            // Phase 3: Read model header (92 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Model Header
            ReadModelHeader(model);

            // Phase 4: Read names header and node names
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Names Header
            ReadNamesHeader(model);

            // Phase 5: Read animations
            ReadAnimations(model);

            // Phase 6: Read node hierarchy starting from root
            SeekMDL(_rootNodeOffset);
            model.RootNode = ReadNode();

            return model;
        }

        #region Header Reading

        private void ReadFileHeader(out int mdlSize, out int mdxSize)
        {
            // File header is at absolute position 0
            _mdlReader.BaseStream.Position = 0;
            int unused = _mdlReader.ReadInt32(); // Always 0
            mdlSize = _mdlReader.ReadInt32();
            mdxSize = _mdlReader.ReadInt32();
        }

        private void ReadGeometryHeader(MDLModel model)
        {
            // Geometry header starts at offset 12 (after file header)
            // All subsequent offsets are relative to offset 12
            uint functionPointer0 = _mdlReader.ReadUInt32();
            uint functionPointer1 = _mdlReader.ReadUInt32();

            // Detect game version from function pointer
            _isTSL = (functionPointer0 == MDLConstants.K2_PC_GEOMETRY_FP ||
                      functionPointer0 == MDLConstants.K2_XBOX_GEOMETRY_FP);

            // Model name (32 bytes, null-terminated)
            model.Name = ReadFixedString(32);

            _rootNodeOffset = _mdlReader.ReadInt32();
            model.NodeCount = _mdlReader.ReadInt32();

            // Skip unknown arrays (24 bytes total)
            _mdlReader.BaseStream.Position += 24;

            int refCount = _mdlReader.ReadInt32();
            byte geometryType = _mdlReader.ReadByte();
            // Skip 3 bytes padding
            _mdlReader.BaseStream.Position += 3;
        }

        private void ReadModelHeader(MDLModel model)
        {
            // Model header immediately follows geometry header
            model.Classification = _mdlReader.ReadByte();
            model.SubClassification = _mdlReader.ReadByte();
            byte unknown = _mdlReader.ReadByte();
            model.AffectedByFog = _mdlReader.ReadByte() != 0;

            int childModelCount = _mdlReader.ReadInt32();

            model.AnimationArrayOffset = _mdlReader.ReadInt32();
            model.AnimationCount = _mdlReader.ReadInt32();
            int animationCountDup = _mdlReader.ReadInt32();

            int parentModelPointer = _mdlReader.ReadInt32();

            // Bounding box
            model.BoundingBoxMin = ReadVector3();
            model.BoundingBoxMax = ReadVector3();
            model.Radius = _mdlReader.ReadSingle();
            model.AnimationScale = _mdlReader.ReadSingle();

            // Supermodel name (32 bytes)
            model.Supermodel = ReadFixedString(32);
        }

        private void ReadNamesHeader(MDLModel model)
        {
            // Names header at offset 180 (12 + 80 + 92 - 4 for padding adjustments)
            // Actually follows model header directly
            int rootNodeOffsetDup = _mdlReader.ReadInt32();
            int unknownPadding = _mdlReader.ReadInt32();
            int mdxDataSize = _mdlReader.ReadInt32();
            int mdxDataOffset = _mdlReader.ReadInt32();
            int namesArrayOffset = _mdlReader.ReadInt32();
            int namesCount = _mdlReader.ReadInt32();
            int namesCountDup = _mdlReader.ReadInt32();

            // Read name offsets
            if (namesCount > 0)
            {
                _nodeNames = new string[namesCount];
                int[] nameOffsets = new int[namesCount];

                SeekMDL(namesArrayOffset);
                for (int i = 0; i < namesCount; i++)
                {
                    nameOffsets[i] = _mdlReader.ReadInt32();
                }

                // Read actual names
                for (int i = 0; i < namesCount; i++)
                {
                    SeekMDL(nameOffsets[i]);
                    _nodeNames[i] = ReadNullTerminatedString();
                }
            }
            else
            {
                _nodeNames = Array.Empty<string>();
            }
        }

        private void ReadAnimations(MDLModel model)
        {
            if (model.AnimationCount <= 0)
            {
                model.Animations = Array.Empty<MDLAnimationData>();
                return;
            }

            // Read animation offsets
            SeekMDL(model.AnimationArrayOffset);
            int[] animOffsets = new int[model.AnimationCount];
            for (int i = 0; i < model.AnimationCount; i++)
            {
                animOffsets[i] = _mdlReader.ReadInt32();
            }

            model.Animations = new MDLAnimationData[model.AnimationCount];
            for (int i = 0; i < model.AnimationCount; i++)
            {
                SeekMDL(animOffsets[i]);
                model.Animations[i] = ReadAnimation();
            }
        }

        private MDLAnimationData ReadAnimation()
        {
            var anim = new MDLAnimationData();

            // Animation has its own geometry header (80 bytes)
            uint fp0 = _mdlReader.ReadUInt32();
            uint fp1 = _mdlReader.ReadUInt32();
            anim.Name = ReadFixedString(32);

            int animRootOffset = _mdlReader.ReadInt32();
            int animNodeCount = _mdlReader.ReadInt32();
            // Skip unknown arrays (24 bytes)
            _mdlReader.BaseStream.Position += 24;
            int refCount = _mdlReader.ReadInt32();
            byte geomType = _mdlReader.ReadByte();
            _mdlReader.BaseStream.Position += 3;

            // Animation-specific header (56 bytes)
            anim.Length = _mdlReader.ReadSingle();
            anim.TransitionTime = _mdlReader.ReadSingle();
            anim.AnimRoot = ReadFixedString(32);
            int eventArrayOffset = _mdlReader.ReadInt32();
            int eventCount = _mdlReader.ReadInt32();
            int eventCountDup = _mdlReader.ReadInt32();
            int unknown = _mdlReader.ReadInt32();

            // Read events
            if (eventCount > 0)
            {
                anim.Events = new MDLEventData[eventCount];
                long savedPos = _mdlReader.BaseStream.Position;
                SeekMDL(eventArrayOffset);
                for (int i = 0; i < eventCount; i++)
                {
                    anim.Events[i] = new MDLEventData
                    {
                        ActivationTime = _mdlReader.ReadSingle(),
                        Name = ReadFixedString(32)
                    };
                }
            }
            else
            {
                anim.Events = Array.Empty<MDLEventData>();
            }

            // Read animation node hierarchy
            SeekMDL(animRootOffset);
            anim.RootNode = ReadNode();

            return anim;
        }

        #endregion

        #region Node Reading

        private MDLNodeData ReadNode()
        {
            var node = new MDLNodeData();
            long nodeStartPos = _mdlReader.BaseStream.Position;

            // Read node header (80 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Node Header
            node.NodeType = _mdlReader.ReadUInt16();
            node.NodeIndex = _mdlReader.ReadUInt16();
            node.NameIndex = _mdlReader.ReadUInt16();
            ushort padding = _mdlReader.ReadUInt16();

            int rootNodeOffset = _mdlReader.ReadInt32();
            int parentNodeOffset = _mdlReader.ReadInt32();

            node.Position = ReadVector3();
            node.Orientation = ReadQuaternion();

            int childArrayOffset = _mdlReader.ReadInt32();
            int childCount = _mdlReader.ReadInt32();
            int childCountDup = _mdlReader.ReadInt32();
            int controllerArrayOffset = _mdlReader.ReadInt32();
            int controllerCount = _mdlReader.ReadInt32();
            int controllerCountDup = _mdlReader.ReadInt32();
            int controllerDataOffset = _mdlReader.ReadInt32();
            int controllerDataCount = _mdlReader.ReadInt32();
            int controllerDataCountDup = _mdlReader.ReadInt32();

            // Assign node name from name table
            if (node.NameIndex >= 0 && node.NameIndex < _nodeNames.Length)
            {
                node.Name = _nodeNames[node.NameIndex];
            }
            else
            {
                node.Name = string.Empty;
            }

            // Read type-specific headers based on node type flags
            if ((node.NodeType & MDLConstants.NODE_HAS_LIGHT) != 0)
            {
                node.Light = ReadLightData();
            }
            if ((node.NodeType & MDLConstants.NODE_HAS_EMITTER) != 0)
            {
                node.Emitter = ReadEmitterData();
            }
            if ((node.NodeType & MDLConstants.NODE_HAS_REFERENCE) != 0)
            {
                node.Reference = ReadReferenceData();
            }
            if ((node.NodeType & MDLConstants.NODE_HAS_MESH) != 0)
            {
                node.Mesh = ReadMeshData();
            }

            // Read controllers
            if (controllerCount > 0)
            {
                node.Controllers = ReadControllers(controllerArrayOffset, controllerCount,
                    controllerDataOffset, controllerDataCount);
            }
            else
            {
                node.Controllers = Array.Empty<MDLControllerData>();
            }

            // Read child nodes
            if (childCount > 0)
            {
                node.Children = new MDLNodeData[childCount];
                int[] childOffsets = new int[childCount];

                SeekMDL(childArrayOffset);
                for (int i = 0; i < childCount; i++)
                {
                    childOffsets[i] = _mdlReader.ReadInt32();
                }

                for (int i = 0; i < childCount; i++)
                {
                    SeekMDL(childOffsets[i]);
                    node.Children[i] = ReadNode();
                }
            }
            else
            {
                node.Children = Array.Empty<MDLNodeData>();
            }

            return node;
        }

        private MDLControllerData[] ReadControllers(int arrayOffset, int count, int dataOffset, int dataCount)
        {
            // First, read controller data array (floats)
            float[] controllerData = new float[dataCount];
            SeekMDL(dataOffset);
            for (int i = 0; i < dataCount; i++)
            {
                controllerData[i] = _mdlReader.ReadSingle();
            }

            // Then read controller headers
            var controllers = new MDLControllerData[count];
            SeekMDL(arrayOffset);
            for (int i = 0; i < count; i++)
            {
                var ctrl = new MDLControllerData();
                ctrl.Type = _mdlReader.ReadInt32();
                ushort unknown = _mdlReader.ReadUInt16();
                ctrl.RowCount = _mdlReader.ReadUInt16();
                ctrl.TimeIndex = _mdlReader.ReadUInt16();
                ctrl.DataIndex = _mdlReader.ReadUInt16();
                byte columnByte = _mdlReader.ReadByte();
                ctrl.IsBezier = (columnByte & MDLConstants.CONTROLLER_BEZIER_FLAG) != 0;
                ctrl.ColumnCount = columnByte & 0x0F;
                // 3 bytes padding
                _mdlReader.BaseStream.Position += 3;

                // Extract controller values from data array
                if (ctrl.RowCount > 0)
                {
                    ctrl.TimeKeys = new float[ctrl.RowCount];
                    int valuesPerRow = ctrl.ColumnCount;
                    if (ctrl.IsBezier) valuesPerRow *= 3; // Value + in-tangent + out-tangent
                    ctrl.Values = new float[ctrl.RowCount * valuesPerRow];

                    for (int r = 0; r < ctrl.RowCount; r++)
                    {
                        int timeIdx = ctrl.TimeIndex + r;
                        if (timeIdx < controllerData.Length)
                        {
                            ctrl.TimeKeys[r] = controllerData[timeIdx];
                        }

                        for (int c = 0; c < valuesPerRow; c++)
                        {
                            int dataIdx = ctrl.DataIndex + r * valuesPerRow + c;
                            if (dataIdx < controllerData.Length)
                            {
                                ctrl.Values[r * valuesPerRow + c] = controllerData[dataIdx];
                            }
                        }
                    }
                }
                else
                {
                    ctrl.TimeKeys = Array.Empty<float>();
                    ctrl.Values = Array.Empty<float>();
                }

                controllers[i] = ctrl;
            }

            return controllers;
        }

        private MDLMeshData ReadMeshData()
        {
            var mesh = new MDLMeshData();
            long meshStartPos = _mdlReader.BaseStream.Position;

            // Read trimesh header
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Trimesh Header
            uint fp0 = _mdlReader.ReadUInt32();
            uint fp1 = _mdlReader.ReadUInt32();

            int faceArrayOffset = _mdlReader.ReadInt32();
            mesh.FaceCount = _mdlReader.ReadInt32();
            int faceCountDup = _mdlReader.ReadInt32();

            mesh.BoundingBoxMin = ReadVector3();
            mesh.BoundingBoxMax = ReadVector3();
            mesh.Radius = _mdlReader.ReadSingle();
            mesh.AveragePoint = ReadVector3();

            mesh.DiffuseColor = ReadVector3();
            mesh.AmbientColor = ReadVector3();
            mesh.TransparencyHint = _mdlReader.ReadUInt32();

            mesh.Texture0 = ReadFixedString(32);
            mesh.Texture1 = ReadFixedString(32);
            mesh.Texture2 = ReadFixedString(12);
            mesh.Texture3 = ReadFixedString(12);

            int indicesCountArrayOffset = _mdlReader.ReadInt32();
            int indicesCountArrayCount = _mdlReader.ReadInt32();
            int indicesCountArrayCountDup = _mdlReader.ReadInt32();
            int indicesOffsetArrayOffset = _mdlReader.ReadInt32();
            int indicesOffsetArrayCount = _mdlReader.ReadInt32();
            int indicesOffsetArrayCountDup = _mdlReader.ReadInt32();
            int invertedCounterOffset = _mdlReader.ReadInt32();
            int invertedCounterCount = _mdlReader.ReadInt32();
            int invertedCounterCountDup = _mdlReader.ReadInt32();

            int unknown1 = _mdlReader.ReadInt32();
            int unknown2 = _mdlReader.ReadInt32();
            int unknown3 = _mdlReader.ReadInt32();
            // Skip saber unknown data (8 bytes)
            _mdlReader.BaseStream.Position += 8;
            int unknown4 = _mdlReader.ReadInt32();

            mesh.UVDirectionX = _mdlReader.ReadSingle();
            mesh.UVDirectionY = _mdlReader.ReadSingle();
            mesh.UVJitter = _mdlReader.ReadSingle();
            mesh.UVJitterSpeed = _mdlReader.ReadSingle();

            mesh.MDXVertexSize = _mdlReader.ReadInt32();
            mesh.MDXDataFlags = _mdlReader.ReadUInt32();
            mesh.MDXPositionOffset = _mdlReader.ReadInt32();
            mesh.MDXNormalOffset = _mdlReader.ReadInt32();
            mesh.MDXColorOffset = _mdlReader.ReadInt32();
            mesh.MDXTex0Offset = _mdlReader.ReadInt32();
            mesh.MDXTex1Offset = _mdlReader.ReadInt32();
            mesh.MDXTex2Offset = _mdlReader.ReadInt32();
            mesh.MDXTex3Offset = _mdlReader.ReadInt32();
            mesh.MDXTangentOffset = _mdlReader.ReadInt32();
            mesh.MDXUnknown1Offset = _mdlReader.ReadInt32();
            mesh.MDXUnknown2Offset = _mdlReader.ReadInt32();
            mesh.MDXUnknown3Offset = _mdlReader.ReadInt32();

            mesh.VertexCount = _mdlReader.ReadUInt16();
            mesh.TextureCount = _mdlReader.ReadUInt16();
            mesh.HasLightmap = _mdlReader.ReadByte() != 0;
            mesh.RotateTexture = _mdlReader.ReadByte() != 0;
            mesh.BackgroundGeometry = _mdlReader.ReadByte() != 0;
            mesh.Shadow = _mdlReader.ReadByte() != 0;
            mesh.Beaming = _mdlReader.ReadByte() != 0;
            mesh.Render = _mdlReader.ReadByte() != 0;
            byte unknown5 = _mdlReader.ReadByte();
            byte padding = _mdlReader.ReadByte();
            mesh.TotalArea = _mdlReader.ReadSingle();
            int unknown6 = _mdlReader.ReadInt32();

            // TSL has 8 extra bytes
            if (_isTSL)
            {
                _mdlReader.BaseStream.Position += 8;
            }

            mesh.MDXDataOffset = _mdlReader.ReadInt32();
            int vertexArrayOffset = _mdlReader.ReadInt32();

            // Read faces
            if (mesh.FaceCount > 0)
            {
                mesh.Faces = new MDLFaceData[mesh.FaceCount];
                SeekMDL(faceArrayOffset);
                for (int i = 0; i < mesh.FaceCount; i++)
                {
                    var face = new MDLFaceData();
                    face.Normal = ReadVector3();
                    face.PlaneDistance = _mdlReader.ReadSingle();
                    face.Material = _mdlReader.ReadInt32();
                    face.Adjacent0 = _mdlReader.ReadInt16();
                    face.Adjacent1 = _mdlReader.ReadInt16();
                    face.Adjacent2 = _mdlReader.ReadInt16();
                    face.Vertex0 = _mdlReader.ReadInt16();
                    face.Vertex1 = _mdlReader.ReadInt16();
                    face.Vertex2 = _mdlReader.ReadInt16();
                    mesh.Faces[i] = face;
                }
            }
            else
            {
                mesh.Faces = Array.Empty<MDLFaceData>();
            }

            // Read vertex indices
            if (indicesCountArrayCount > 0)
            {
                SeekMDL(indicesCountArrayOffset);
                int[] indicesCounts = new int[indicesCountArrayCount];
                for (int i = 0; i < indicesCountArrayCount; i++)
                {
                    indicesCounts[i] = _mdlReader.ReadInt32();
                }

                SeekMDL(indicesOffsetArrayOffset);
                int[] indicesOffsets = new int[indicesOffsetArrayCount];
                for (int i = 0; i < indicesOffsetArrayCount; i++)
                {
                    indicesOffsets[i] = _mdlReader.ReadInt32();
                }

                // Read first index group (primary vertex indices)
                if (indicesOffsetArrayCount > 0 && indicesCounts.Length > 0)
                {
                    mesh.Indices = new ushort[indicesCounts[0]];
                    SeekMDL(indicesOffsets[0]);
                    for (int i = 0; i < indicesCounts[0]; i++)
                    {
                        mesh.Indices[i] = _mdlReader.ReadUInt16();
                    }
                }
                else
                {
                    mesh.Indices = Array.Empty<ushort>();
                }
            }
            else
            {
                mesh.Indices = Array.Empty<ushort>();
            }

            // Read vertex data from MDX
            ReadMDXVertexData(mesh);

            return mesh;
        }

        private void ReadMDXVertexData(MDLMeshData mesh)
        {
            if (mesh.VertexCount == 0 || mesh.MDXVertexSize == 0)
            {
                mesh.Positions = Array.Empty<Vector3Data>();
                mesh.Normals = Array.Empty<Vector3Data>();
                mesh.TexCoords0 = Array.Empty<Vector2Data>();
                mesh.TexCoords1 = Array.Empty<Vector2Data>();
                return;
            }

            // Pre-allocate arrays
            mesh.Positions = new Vector3Data[mesh.VertexCount];
            mesh.Normals = new Vector3Data[mesh.VertexCount];
            mesh.TexCoords0 = new Vector2Data[mesh.VertexCount];
            mesh.TexCoords1 = new Vector2Data[mesh.VertexCount];

            // Seek to MDX data
            _mdxReader.BaseStream.Position = _mdxBaseOffset + mesh.MDXDataOffset;

            // Read interleaved vertex data
            for (int i = 0; i < mesh.VertexCount; i++)
            {
                long vertexStart = _mdxReader.BaseStream.Position;

                // Position
                if ((mesh.MDXDataFlags & MDLConstants.MDX_VERTICES) != 0 && mesh.MDXPositionOffset >= 0)
                {
                    _mdxReader.BaseStream.Position = vertexStart + mesh.MDXPositionOffset;
                    mesh.Positions[i] = new Vector3Data
                    {
                        X = _mdxReader.ReadSingle(),
                        Y = _mdxReader.ReadSingle(),
                        Z = _mdxReader.ReadSingle()
                    };
                }

                // Normal
                if ((mesh.MDXDataFlags & MDLConstants.MDX_VERTEX_NORMALS) != 0 && mesh.MDXNormalOffset >= 0)
                {
                    _mdxReader.BaseStream.Position = vertexStart + mesh.MDXNormalOffset;
                    mesh.Normals[i] = new Vector3Data
                    {
                        X = _mdxReader.ReadSingle(),
                        Y = _mdxReader.ReadSingle(),
                        Z = _mdxReader.ReadSingle()
                    };
                }

                // Texture coordinates 0
                if ((mesh.MDXDataFlags & MDLConstants.MDX_TEX0_VERTICES) != 0 && mesh.MDXTex0Offset >= 0)
                {
                    _mdxReader.BaseStream.Position = vertexStart + mesh.MDXTex0Offset;
                    mesh.TexCoords0[i] = new Vector2Data
                    {
                        X = _mdxReader.ReadSingle(),
                        Y = _mdxReader.ReadSingle()
                    };
                }

                // Texture coordinates 1 (lightmap)
                if ((mesh.MDXDataFlags & MDLConstants.MDX_TEX1_VERTICES) != 0 && mesh.MDXTex1Offset >= 0)
                {
                    _mdxReader.BaseStream.Position = vertexStart + mesh.MDXTex1Offset;
                    mesh.TexCoords1[i] = new Vector2Data
                    {
                        X = _mdxReader.ReadSingle(),
                        Y = _mdxReader.ReadSingle()
                    };
                }

                // Move to next vertex
                _mdxReader.BaseStream.Position = vertexStart + mesh.MDXVertexSize;
            }
        }

        private MDLLightData ReadLightData()
        {
            var light = new MDLLightData();
            // Skip 16 bytes of unknown/padding
            _mdlReader.BaseStream.Position += 16;

            int flareSizesOffset = _mdlReader.ReadInt32();
            int flareSizesCount = _mdlReader.ReadInt32();
            _mdlReader.ReadInt32(); // duplicate count

            int flarePositionsOffset = _mdlReader.ReadInt32();
            int flarePositionsCount = _mdlReader.ReadInt32();
            _mdlReader.ReadInt32();

            int flareColorShiftsOffset = _mdlReader.ReadInt32();
            int flareColorShiftsCount = _mdlReader.ReadInt32();
            _mdlReader.ReadInt32();

            int flareTextureNamesOffset = _mdlReader.ReadInt32();
            int flareTextureNamesCount = _mdlReader.ReadInt32();
            _mdlReader.ReadInt32();

            light.FlareRadius = _mdlReader.ReadSingle();
            light.LightPriority = _mdlReader.ReadInt32();
            light.AmbientOnly = _mdlReader.ReadInt32() != 0;
            light.DynamicType = _mdlReader.ReadInt32();
            light.AffectDynamic = _mdlReader.ReadInt32() != 0;
            light.Shadow = _mdlReader.ReadInt32() != 0;
            light.Flare = _mdlReader.ReadInt32() != 0;
            light.FadingLight = _mdlReader.ReadInt32() != 0;

            return light;
        }

        private MDLEmitterData ReadEmitterData()
        {
            var emitter = new MDLEmitterData();

            emitter.DeadSpace = _mdlReader.ReadSingle();
            emitter.BlastRadius = _mdlReader.ReadSingle();
            emitter.BlastLength = _mdlReader.ReadSingle();
            emitter.BranchCount = _mdlReader.ReadInt32();
            emitter.ControlPtSmoothing = _mdlReader.ReadSingle();
            emitter.XGrid = _mdlReader.ReadInt32();
            emitter.YGrid = _mdlReader.ReadInt32();
            _mdlReader.ReadInt32(); // padding

            emitter.UpdateScript = ReadFixedString(32);
            emitter.RenderScript = ReadFixedString(32);
            emitter.BlendScript = ReadFixedString(32);
            emitter.Texture = ReadFixedString(32);
            emitter.ChunkName = ReadFixedString(16);

            emitter.TwoSidedTex = _mdlReader.ReadInt32() != 0;
            emitter.Loop = _mdlReader.ReadInt32() != 0;
            emitter.RenderOrder = _mdlReader.ReadUInt16();
            emitter.FrameBlending = _mdlReader.ReadByte() != 0;

            emitter.DepthTexture = ReadFixedString(33);

            _mdlReader.ReadByte(); // padding
            emitter.Flags = _mdlReader.ReadUInt32();

            return emitter;
        }

        private MDLReferenceData ReadReferenceData()
        {
            var reference = new MDLReferenceData();
            reference.ModelResRef = ReadFixedString(32);
            reference.Reattachable = _mdlReader.ReadInt32() != 0;
            return reference;
        }

        #endregion

        #region Utility Methods

        private void SeekMDL(int offset)
        {
            // Offsets are relative to MDL data section (after 12-byte file header)
            _mdlReader.BaseStream.Position = _mdlBaseOffset + MDLConstants.FILE_HEADER_SIZE + offset;
        }

        private string ReadFixedString(int length)
        {
            int bytesToRead = Math.Min(length, _stringBuffer.Length);
            _mdlReader.Read(_stringBuffer, 0, bytesToRead);
            if (length > bytesToRead)
            {
                _mdlReader.BaseStream.Position += length - bytesToRead;
            }

            // Find null terminator
            int strLen = 0;
            while (strLen < bytesToRead && _stringBuffer[strLen] != 0)
            {
                strLen++;
            }

            return strLen > 0 ? Encoding.ASCII.GetString(_stringBuffer, 0, strLen) : string.Empty;
        }

        private string ReadNullTerminatedString()
        {
            int pos = 0;
            byte b;
            while ((b = _mdlReader.ReadByte()) != 0 && pos < _stringBuffer.Length - 1)
            {
                _stringBuffer[pos++] = b;
            }
            return pos > 0 ? Encoding.ASCII.GetString(_stringBuffer, 0, pos) : string.Empty;
        }

        private Vector3Data ReadVector3()
        {
            return new Vector3Data
            {
                X = _mdlReader.ReadSingle(),
                Y = _mdlReader.ReadSingle(),
                Z = _mdlReader.ReadSingle()
            };
        }

        private Vector4Data ReadQuaternion()
        {
            return new Vector4Data
            {
                W = _mdlReader.ReadSingle(),
                X = _mdlReader.ReadSingle(),
                Y = _mdlReader.ReadSingle(),
                Z = _mdlReader.ReadSingle()
            };
        }

        #endregion

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_ownsStreams)
                {
                    _mdlReader.Dispose();
                    _mdxReader.Dispose();
                }
                _disposed = true;
            }
        }
    }
}

