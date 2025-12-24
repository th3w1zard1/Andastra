using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Andastra.Runtime.Content.MDL
{
    /// <summary>
    /// Ultra-high-performance MDL/MDX reader using unsafe code and zero-copy operations.
    /// </summary>
    /// <remarks>
    /// MDL/MDX Optimized Reader:
    /// - Based on swkotor2.exe MDL/MDX file format and loading system
    /// - Located via string references: "ModelName" @ 0x007c1c8c, "Model" @ 0x007c1ca8, "ModelResRef" @ 0x007c2f6c
    /// - "CSWCCreature::LoadModel(): Failed to load creature model '%s'." @ 0x007c82fc (model loading error)
    /// - "Model %s nor the default model %s could be loaded." @ 0x007cad14 (model loading fallback error)
    /// - "DoubleMdlVar" @ 0x007d05d8, "ShortMdlVar" @ 0x007d05e8, "LongMdlVar" @ 0x007d05f4 (MDL variable types)
    /// - Model loading: FUN_005261b0 @ 0x005261b0 loads creature models from appearance.2da
    /// - Original implementation: Reads MDL (model definition) and MDX (geometry) binary files using direct memory access
    /// - Uses unsafe code and pointer arithmetic for maximum performance, matching original engine approach
    /// - MDL file structure: Header, geometry header, model header, names header, node tree, animations
    /// - MDX file structure: Vertex data arrays (positions, normals, UVs, face indices) with specific byte layouts
    /// 
    /// Performance optimizations (based on reone, KotOR.js, MDLOps analysis):
    /// 1. Unsafe pointer access for direct memory reading (with comprehensive bounds checking for security)
    /// 2. Pre-computed MDX vertex offsets to eliminate per-vertex calculations
    /// 3. Single-pass vertex data extraction with bulk operations
    /// 4. Efficient string reading with bounds validation
    /// 5. Struct-based header reading with fixed layouts
    /// 6. Bulk array operations with overflow protection
    /// 
    /// Security and robustness:
    /// - Comprehensive bounds checking on all memory accesses
    /// - Integer overflow protection using long arithmetic
    /// - Offset validation for all array and node accesses
    /// - Minimum file size and header size validation
    /// - Graceful handling of corrupted or truncated files
    /// 
    /// Reference implementations:
    /// - reone/src/libs/graphics/format/mdlmdxreader.cpp - C++ implementation with pointer arithmetic
    /// - KotOR.js/src/loaders/MDLLoader.ts - TypeScript with typed arrays and bulk operations
    /// - MDLOps/MDLOpsM.pm - Perl with optimized data structures
    /// - vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Complete format specification
    /// </remarks>
    public unsafe sealed class MDLOptimizedReader : IDisposable
    {
        private readonly byte[] _mdlData;
        private readonly byte[] _mdxData;
        private bool _isTSL;
        private string[] _nodeNames;
        private bool _disposed;

        // Pre-computed MDX vertex attribute offsets for fast access
        private struct VertexOffsets
        {
            public int Position;
            public int Normal;
            public int Color;
            public int Tex0;
            public int Tex1;
            public int Tex2;
            public int Tex3;
            public int Tangent;
            public int BoneWeights;
            public int BoneIndices;
        }

        /// <summary>
        /// True if the model is from KotOR 2: The Sith Lords.
        /// </summary>
        public bool IsTSL
        {
            get { return _isTSL; }
        }

        /// <summary>
        /// Creates an optimized reader from byte arrays (optimal - no additional copy needed).
        /// </summary>
        /// <param name="mdlData">MDL file data as byte array</param>
        /// <param name="mdxData">MDX file data as byte array</param>
        /// <exception cref="ArgumentNullException">Thrown when mdlData or mdxData is null.</exception>
        public MDLOptimizedReader(byte[] mdlData, byte[] mdxData)
        {
            if (mdlData == null)
            {
                throw new ArgumentNullException(nameof(mdlData));
            }
            if (mdxData == null)
            {
                throw new ArgumentNullException(nameof(mdxData));
            }

            _mdlData = mdlData;
            _mdxData = mdxData;
        }

        /// <summary>
        /// Creates an optimized reader from file paths (reads entire files into memory).
        /// </summary>
        /// <param name="mdlPath">Path to the MDL file</param>
        /// <param name="mdxPath">Path to the MDX file</param>
        /// <exception cref="ArgumentNullException">Thrown when mdlPath or mdxPath is null or empty.</exception>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
        /// <exception cref="DirectoryNotFoundException">Thrown when the specified path is invalid.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when access to the file is denied.</exception>
        /// <exception cref="IOException">Thrown when an I/O error occurs while reading the file.</exception>
        public MDLOptimizedReader(string mdlPath, string mdxPath)
        {
            if (string.IsNullOrEmpty(mdlPath))
            {
                throw new ArgumentNullException(nameof(mdlPath));
            }
            if (string.IsNullOrEmpty(mdxPath))
            {
                throw new ArgumentNullException(nameof(mdxPath));
            }

            _mdlData = File.ReadAllBytes(mdlPath);
            _mdxData = File.ReadAllBytes(mdxPath);
        }

        /// <summary>
        /// Loads the complete MDL model using optimized bulk operations.
        /// </summary>
        /// <returns>The loaded MDL model containing all geometry, animation, and node data.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the reader has been disposed.</exception>
        /// <exception cref="InvalidDataException">Thrown when the MDL or MDX file is corrupted, truncated, or has invalid data.</exception>
        /// <exception cref="InvalidOperationException">Thrown when data size calculations overflow or array bounds are exceeded.</exception>
        public MDLModel Load()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MDLOptimizedReader));
            }

            fixed (byte* mdlPtr = _mdlData)
            {
                return LoadInternal(mdlPtr);
            }
        }

        private MDLModel LoadInternal(byte* mdlPtr)
        {
            var model = new MDLModel();
            int pos = 0;

            // Validate minimum file size before reading
            // MDL file must be at least FILE_HEADER_SIZE (12 bytes) to read the header
            if (_mdlData.Length < MDLConstants.FILE_HEADER_SIZE)
            {
                throw new InvalidDataException(
                    $"MDL file is too small: expected at least {MDLConstants.FILE_HEADER_SIZE} bytes, " +
                    $"got {_mdlData.Length} bytes. File may be corrupted or truncated."
                );
            }

            // Phase 1: Read file header (12 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - MDL File Header
            int unused = ReadInt32(mdlPtr, ref pos); // Always 0
            int mdlSize = ReadInt32(mdlPtr, ref pos);
            int mdxSize = ReadInt32(mdlPtr, ref pos);

            // Validate header size fields against actual file sizes
            // This prevents reading beyond file bounds if header is corrupted
            if (mdlSize < 0 || mdlSize > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"MDL header size field ({mdlSize}) is invalid. " +
                    $"Expected value between 0 and {_mdlData.Length} (actual file size). " +
                    "File may be corrupted or truncated."
                );
            }
            if (mdxSize < 0 || mdxSize > _mdxData.Length)
            {
                throw new InvalidDataException(
                    $"MDX header size field ({mdxSize}) is invalid. " +
                    $"Expected value between 0 and {_mdxData.Length} (actual file size). " +
                    "File may be corrupted or truncated."
                );
            }

            // Validate that MDL file is large enough to contain the geometry header
            // Geometry header starts at offset FILE_HEADER_SIZE (12) and is 80 bytes
            int minimumRequiredSize = MDLConstants.FILE_HEADER_SIZE + MDLConstants.GEOMETRY_HEADER_SIZE;
            if (_mdlData.Length < minimumRequiredSize)
            {
                throw new InvalidDataException(
                    $"MDL file is too small to contain geometry header: expected at least {minimumRequiredSize} bytes, " +
                    $"got {_mdlData.Length} bytes. File may be corrupted or truncated."
                );
            }

            // Phase 2: Read geometry header (80 bytes at offset 12)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Geometry Header
            uint funcPtr0 = ReadUInt32(mdlPtr, ref pos);
            uint funcPtr1 = ReadUInt32(mdlPtr, ref pos);

            // Detect game version from function pointer
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - KotOR 1 vs KotOR 2 Models
            _isTSL = (funcPtr0 == MDLConstants.K2_PC_GEOMETRY_FP ||
                      funcPtr0 == MDLConstants.K2_XBOX_GEOMETRY_FP);

            model.Name = ReadFixedString(mdlPtr, ref pos, 32);
            int rootNodeOffset = ReadInt32(mdlPtr, ref pos);
            model.NodeCount = ReadInt32(mdlPtr, ref pos);

            // Skip unknown arrays (24 bytes)
            pos += 24;
            int refCount = ReadInt32(mdlPtr, ref pos);
            byte geometryType = ReadByte(mdlPtr, ref pos);
            pos += 3; // padding

            // Phase 3: Read model header (92 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Model Header
            model.Classification = ReadByte(mdlPtr, ref pos);
            model.SubClassification = ReadByte(mdlPtr, ref pos);
            pos += 1; // unknown
            model.AffectedByFog = ReadByte(mdlPtr, ref pos) != 0;
            int childModelCount = ReadInt32(mdlPtr, ref pos);

            model.AnimationArrayOffset = ReadInt32(mdlPtr, ref pos);
            model.AnimationCount = ReadInt32(mdlPtr, ref pos);
            int animCountDup = ReadInt32(mdlPtr, ref pos);
            int parentModelPtr = ReadInt32(mdlPtr, ref pos);

            model.BoundingBoxMin = ReadVector3(mdlPtr, ref pos);
            model.BoundingBoxMax = ReadVector3(mdlPtr, ref pos);
            model.Radius = ReadFloat(mdlPtr, ref pos);
            model.AnimationScale = ReadFloat(mdlPtr, ref pos);
            model.Supermodel = ReadFixedString(mdlPtr, ref pos, 32);

            // Phase 4: Read names header (28 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Names Header
            int animRootOffset = ReadInt32(mdlPtr, ref pos);
            int unknownPad = ReadInt32(mdlPtr, ref pos);
            int mdxDataSize = ReadInt32(mdlPtr, ref pos);
            int mdxDataOffset = ReadInt32(mdlPtr, ref pos);
            int namesArrayOffset = ReadInt32(mdlPtr, ref pos);
            int namesCount = ReadInt32(mdlPtr, ref pos);
            int namesCountDup = ReadInt32(mdlPtr, ref pos);

            // Bulk read name offsets and names
            if (namesCount > 0)
            {
                _nodeNames = new string[namesCount];
                int[] nameOffsets = ReadInt32Array(mdlPtr, MDLConstants.FILE_HEADER_SIZE + namesArrayOffset, namesCount);

                for (int i = 0; i < namesCount; i++)
                {
                    // Validate offset is non-negative (negative offsets indicate invalid data)
                    if (nameOffsets[i] >= 0)
                    {
                        long absoluteOffsetLong = (long)MDLConstants.FILE_HEADER_SIZE + nameOffsets[i];
                        if (absoluteOffsetLong >= 0 && absoluteOffsetLong < _mdlData.Length)
                        {
                            _nodeNames[i] = ReadNullTerminatedString(mdlPtr, (int)absoluteOffsetLong);
                        }
                        else
                        {
                            _nodeNames[i] = string.Empty;
                        }
                    }
                    else
                    {
                        _nodeNames[i] = string.Empty;
                    }
                }
            }
            else
            {
                _nodeNames = Array.Empty<string>();
            }

            // Phase 5: Read animations (bulk read offsets first)
            if (model.AnimationCount > 0)
            {
                int[] animOffsets = ReadInt32Array(mdlPtr, MDLConstants.FILE_HEADER_SIZE + model.AnimationArrayOffset, model.AnimationCount);
                model.Animations = new MDLAnimationData[model.AnimationCount];

                for (int i = 0; i < model.AnimationCount; i++)
                {
                    // Validate offset is non-negative (negative offsets indicate invalid data)
                    if (animOffsets[i] >= 0)
                    {
                        long absoluteOffsetLong = (long)MDLConstants.FILE_HEADER_SIZE + animOffsets[i];
                        if (absoluteOffsetLong >= 0 && absoluteOffsetLong < _mdlData.Length)
                        {
                            model.Animations[i] = ReadAnimation(mdlPtr, (int)absoluteOffsetLong);
                        }
                        else
                        {
                            throw new InvalidDataException(
                                $"Animation offset {i} is out of bounds: " +
                                $"FILE_HEADER_SIZE ({MDLConstants.FILE_HEADER_SIZE}) + " +
                                $"animOffsets[{i}] ({animOffsets[i]}) = {absoluteOffsetLong}, " +
                                $"file length = {_mdlData.Length}"
                            );
                        }
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"Animation offset {i} is negative ({animOffsets[i]}), indicating corrupted data."
                        );
                    }
                }
            }
            else
            {
                model.Animations = Array.Empty<MDLAnimationData>();
            }

            // Phase 6: Read node hierarchy
            // Validate root node offset before reading
            if (rootNodeOffset >= 0)
            {
                long absoluteOffsetLong = (long)MDLConstants.FILE_HEADER_SIZE + rootNodeOffset;
                if (absoluteOffsetLong >= 0 && absoluteOffsetLong < _mdlData.Length)
                {
                    model.RootNode = ReadNode(mdlPtr, (int)absoluteOffsetLong);
                }
                else
                {
                    throw new InvalidDataException(
                        $"Root node offset is out of bounds: " +
                        $"FILE_HEADER_SIZE ({MDLConstants.FILE_HEADER_SIZE}) + " +
                        $"rootNodeOffset ({rootNodeOffset}) = {absoluteOffsetLong}, " +
                        $"file length = {_mdlData.Length}"
                    );
                }
            }
            else
            {
                throw new InvalidDataException(
                    $"Root node offset is negative ({rootNodeOffset}), indicating corrupted data."
                );
            }

            return model;
        }

        #region Animation Reading

        /// <summary>
        /// Reads animation data from the MDL file.
        /// Includes geometry header, animation properties, events, and animation root node.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Animation Header
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="offset">Offset to animation data in MDL file</param>
        /// <returns>Loaded animation data</returns>
        /// <exception cref="InvalidDataException">Thrown when animation data is corrupted or out of bounds</exception>
        private MDLAnimationData ReadAnimation(byte* mdlPtr, int offset)
        {
            int pos = offset;
            var anim = new MDLAnimationData();

            // Geometry header for animation (80 bytes)
            uint fp0 = ReadUInt32(mdlPtr, ref pos);
            uint fp1 = ReadUInt32(mdlPtr, ref pos);
            anim.Name = ReadFixedString(mdlPtr, ref pos, 32);

            int animRootOffset = ReadInt32(mdlPtr, ref pos);
            int animNodeCount = ReadInt32(mdlPtr, ref pos);
            pos += 24; // unknown arrays
            int refCount = ReadByte(mdlPtr, ref pos);
            pos += 3;

            // Animation header (56 bytes)
            anim.Length = ReadFloat(mdlPtr, ref pos);
            anim.TransitionTime = ReadFloat(mdlPtr, ref pos);
            anim.AnimRoot = ReadFixedString(mdlPtr, ref pos, 32);

            int eventArrayOffset = ReadInt32(mdlPtr, ref pos);
            int eventCount = ReadInt32(mdlPtr, ref pos);
            int eventCountDup = ReadInt32(mdlPtr, ref pos);
            int unknown = ReadInt32(mdlPtr, ref pos);

            // Bulk read events
            if (eventCount > 0)
            {
                anim.Events = new MDLEventData[eventCount];
                int eventPos = MDLConstants.FILE_HEADER_SIZE + eventArrayOffset;

                for (int i = 0; i < eventCount; i++)
                {
                    anim.Events[i] = new MDLEventData
                    {
                        ActivationTime = ReadFloat(mdlPtr, ref eventPos),
                        Name = ReadFixedString(mdlPtr, ref eventPos, 32)
                    };
                }
            }
            else
            {
                anim.Events = Array.Empty<MDLEventData>();
            }

            // Read animation nodes
            // Validate animation root node offset before reading
            if (animRootOffset >= 0)
            {
                long absoluteOffsetLong = (long)MDLConstants.FILE_HEADER_SIZE + animRootOffset;
                if (absoluteOffsetLong >= 0 && absoluteOffsetLong < _mdlData.Length)
                {
                    anim.RootNode = ReadNode(mdlPtr, (int)absoluteOffsetLong);
                }
                else
                {
                    throw new InvalidDataException(
                        $"Animation root node offset is out of bounds: " +
                        $"FILE_HEADER_SIZE ({MDLConstants.FILE_HEADER_SIZE}) + " +
                        $"animRootOffset ({animRootOffset}) = {absoluteOffsetLong}, " +
                        $"file length = {_mdlData.Length}"
                    );
                }
            }
            else
            {
                throw new InvalidDataException(
                    $"Animation root node offset is negative ({animRootOffset}), indicating corrupted data."
                );
            }

            return anim;
        }

        #endregion

        #region Node Reading

        /// <summary>
        /// Reads a node from the MDL file, including type-specific data (mesh, light, emitter, reference).
        /// Recursively reads child nodes and controller data.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Node Header
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="offset">Offset to node data in MDL file</param>
        /// <returns>Loaded node data with children and controllers</returns>
        /// <exception cref="InvalidDataException">Thrown when node data is corrupted or out of bounds</exception>
        private MDLNodeData ReadNode(byte* mdlPtr, int offset)
        {
            int pos = offset;
            var node = new MDLNodeData();

            // Node header (80 bytes)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Node Header
            node.NodeType = ReadUInt16(mdlPtr, ref pos);
            node.NodeIndex = ReadUInt16(mdlPtr, ref pos);
            node.NameIndex = ReadUInt16(mdlPtr, ref pos);
            pos += 2; // padding

            int rootNodeOffset = ReadInt32(mdlPtr, ref pos);
            int parentNodeOffset = ReadInt32(mdlPtr, ref pos);

            node.Position = ReadVector3(mdlPtr, ref pos);
            node.Orientation = ReadQuaternion(mdlPtr, ref pos);

            int childArrayOffset = ReadInt32(mdlPtr, ref pos);
            int childCount = ReadInt32(mdlPtr, ref pos);
            int childCountDup = ReadInt32(mdlPtr, ref pos);
            int controllerArrayOffset = ReadInt32(mdlPtr, ref pos);
            int controllerCount = ReadInt32(mdlPtr, ref pos);
            int controllerCountDup = ReadInt32(mdlPtr, ref pos);
            int controllerDataOffset = ReadInt32(mdlPtr, ref pos);
            int controllerDataCount = ReadInt32(mdlPtr, ref pos);
            int controllerDataCountDup = ReadInt32(mdlPtr, ref pos);

            // Get node name
            if (node.NameIndex >= 0 && node.NameIndex < _nodeNames.Length)
            {
                node.Name = _nodeNames[node.NameIndex];
            }
            else
            {
                node.Name = string.Empty;
            }

            // Read type-specific data
            if ((node.NodeType & MDLConstants.NODE_HAS_LIGHT) != 0)
            {
                node.Light = ReadLightData(mdlPtr, ref pos);
            }
            if ((node.NodeType & MDLConstants.NODE_HAS_EMITTER) != 0)
            {
                node.Emitter = ReadEmitterData(mdlPtr, ref pos);
            }
            if ((node.NodeType & MDLConstants.NODE_HAS_REFERENCE) != 0)
            {
                node.Reference = ReadReferenceData(mdlPtr, ref pos);
            }
            if ((node.NodeType & MDLConstants.NODE_HAS_MESH) != 0)
            {
                node.Mesh = ReadMeshData(mdlPtr, ref pos, node.NodeType);
            }

            // Bulk read controller data first
            if (controllerCount > 0 && controllerDataCount > 0)
            {
                float[] controllerData = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + controllerDataOffset, controllerDataCount);
                node.Controllers = ReadControllers(mdlPtr, MDLConstants.FILE_HEADER_SIZE + controllerArrayOffset, controllerCount, controllerData);
            }
            else
            {
                node.Controllers = Array.Empty<MDLControllerData>();
            }

            // Bulk read child offsets, then read children
            if (childCount > 0)
            {
                int[] childOffsets = ReadInt32Array(mdlPtr, MDLConstants.FILE_HEADER_SIZE + childArrayOffset, childCount);
                node.Children = new MDLNodeData[childCount];

                for (int i = 0; i < childCount; i++)
                {
                    // Validate offset is non-negative (negative offsets indicate invalid data)
                    if (childOffsets[i] >= 0)
                    {
                        long absoluteOffsetLong = (long)MDLConstants.FILE_HEADER_SIZE + childOffsets[i];
                        if (absoluteOffsetLong >= 0 && absoluteOffsetLong < _mdlData.Length)
                        {
                            node.Children[i] = ReadNode(mdlPtr, (int)absoluteOffsetLong);
                        }
                        else
                        {
                            throw new InvalidDataException(
                                $"Child node offset {i} is out of bounds: " +
                                $"FILE_HEADER_SIZE ({MDLConstants.FILE_HEADER_SIZE}) + " +
                                $"childOffsets[{i}] ({childOffsets[i]}) = {absoluteOffsetLong}, " +
                                $"file length = {_mdlData.Length}"
                            );
                        }
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"Child node offset {i} is negative ({childOffsets[i]}), indicating corrupted data."
                        );
                    }
                }
            }
            else
            {
                node.Children = Array.Empty<MDLNodeData>();
            }

            return node;
        }

        /// <summary>
        /// Reads controller data array from the MDL file.
        /// Handles compressed quaternion decompression for orientation controllers.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Controller Data
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="arrayOffset">Offset to controller array in MDL file</param>
        /// <param name="count">Number of controllers to read</param>
        /// <param name="data">Pre-loaded controller data array (float values)</param>
        /// <returns>Array of controller data</returns>
        /// <exception cref="InvalidOperationException">Thrown when array size calculations overflow</exception>
        private MDLControllerData[] ReadControllers(byte* mdlPtr, int arrayOffset, int count, float[] data)
        {
            var controllers = new MDLControllerData[count];
            int pos = arrayOffset;

            for (int i = 0; i < count; i++)
            {
                var ctrl = new MDLControllerData();
                ctrl.Type = ReadInt32(mdlPtr, ref pos);
                ushort unknown = ReadUInt16(mdlPtr, ref pos);
                ctrl.RowCount = ReadUInt16(mdlPtr, ref pos);
                ctrl.TimeIndex = ReadUInt16(mdlPtr, ref pos);
                ctrl.DataIndex = ReadUInt16(mdlPtr, ref pos);
                byte columnByte = ReadByte(mdlPtr, ref pos);
                ctrl.IsBezier = (columnByte & MDLConstants.CONTROLLER_BEZIER_FLAG) != 0;
                ctrl.ColumnCount = columnByte & 0x0F;
                pos += 3; // padding

                // Handle compressed quaternion for orientation controller
                bool isCompressedQuat = (ctrl.Type == MDLConstants.CONTROLLER_ORIENTATION && ctrl.ColumnCount == 2);

                if (ctrl.RowCount > 0)
                {
                    ctrl.TimeKeys = new float[ctrl.RowCount];
                    int valuesPerRow = ctrl.ColumnCount;
                    if (ctrl.IsBezier)
                    {
                        valuesPerRow *= 3;
                    }

                    // Check for potential integer overflow
                    long valuesCountLong = (long)ctrl.RowCount * (isCompressedQuat ? 4 : valuesPerRow);
                    if (valuesCountLong > int.MaxValue)
                    {
                        throw new InvalidOperationException(
                            $"Controller values array size calculation overflow: rowCount={ctrl.RowCount}, " +
                            $"valuesPerRow={(isCompressedQuat ? 4 : valuesPerRow)}"
                        );
                    }
                    ctrl.Values = new float[(int)valuesCountLong];

                    for (int r = 0; r < ctrl.RowCount; r++)
                    {
                        // Time key
                        int timeIdx = ctrl.TimeIndex + r;
                        if (timeIdx < data.Length)
                        {
                            ctrl.TimeKeys[r] = data[timeIdx];
                        }

                        if (isCompressedQuat)
                        {
                            // Decompress quaternion from 32-bit packed value
                            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Compressed Quaternion
                            // Reference: src/Andastra/Parsing/Common/Vector4.cs:42-69 - FromCompressed implementation
                            int dataIdx = ctrl.DataIndex + r;
                            if (dataIdx < data.Length)
                            {
                                // Read the packed quaternion as uint32 (stored as float in the data array)
                                // We need to reinterpret the float bits as uint32
                                uint packed = BitConverter.ToUInt32(BitConverter.GetBytes(data[dataIdx]), 0);
                                DecompressQuaternion(packed, out float qx, out float qy, out float qz, out float qw);
                                ctrl.Values[r * 4 + 0] = qx;
                                ctrl.Values[r * 4 + 1] = qy;
                                ctrl.Values[r * 4 + 2] = qz;
                                ctrl.Values[r * 4 + 3] = qw;
                            }
                        }
                        else
                        {
                            for (int c = 0; c < valuesPerRow; c++)
                            {
                                int dataIdx = ctrl.DataIndex + r * valuesPerRow + c;
                                if (dataIdx < data.Length)
                                {
                                    ctrl.Values[r * valuesPerRow + c] = data[dataIdx];
                                }
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

        /// <summary>
        /// Decompresses a packed quaternion from 32 bits.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Compressed Quaternion
        /// Reference: src/Andastra/Parsing/Common/Vector4.cs:42-69 - FromCompressed implementation
        /// X: bits 0-10 (11 bits), Y: bits 11-21 (11 bits), Z: bits 22-31 (10 bits)
        /// W: computed from unit constraint (q.x² + q.y² + q.z² + q.w² = 1)
        /// </summary>
        private static void DecompressQuaternion(uint packed, out float x, out float y, out float z, out float w)
        {
            // Extract components (unsigned integers)
            uint xi = packed & 0x7FF;          // 11 bits (0-2047)
            uint yi = (packed >> 11) & 0x7FF;  // 11 bits (0-2047)
            uint zi = (packed >> 22) & 0x3FF;  // 10 bits (0-1023)

            // Map to [-1, 1] range
            x = (xi / 1023.0f) - 1.0f;
            y = (yi / 1023.0f) - 1.0f;
            z = (zi / 511.0f) - 1.0f;

            // Compute W from unit quaternion constraint: x² + y² + z² + w² = 1
            float temp = x * x + y * y + z * z;
            if (temp < 1.0f)
            {
                w = (float)Math.Sqrt(1.0f - temp);
            }
            else
            {
                // Handle edge case where quaternion is not properly normalized
                // Normalize x, y, z and set w = 0
                float sqrtTemp = (float)Math.Sqrt(temp);
                x /= sqrtTemp;
                y /= sqrtTemp;
                z /= sqrtTemp;
                w = 0.0f;
            }
        }

        #endregion

        #region Mesh Reading

        /// <summary>
        /// Reads mesh data from the MDL file, including trimesh header, textures, and MDX vertex data.
        /// Handles special mesh types: skinmesh, danglymesh, and saber mesh.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Trimesh Header
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="pos">Current position in MDL file (will be advanced)</param>
        /// <param name="nodeType">Node type flags to determine mesh variant</param>
        /// <returns>Loaded mesh data with vertex information</returns>
        /// <exception cref="InvalidDataException">Thrown when mesh data is corrupted or out of bounds</exception>
        private MDLMeshData ReadMeshData(byte* mdlPtr, ref int pos, ushort nodeType)
        {
            int meshStart = pos;
            var mesh = new MDLMeshData();

            // Trimesh header
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Trimesh Header
            uint fp0 = ReadUInt32(mdlPtr, ref pos);
            uint fp1 = ReadUInt32(mdlPtr, ref pos);

            int faceArrayOffset = ReadInt32(mdlPtr, ref pos);
            mesh.FaceCount = ReadInt32(mdlPtr, ref pos);
            int faceCountDup = ReadInt32(mdlPtr, ref pos);

            mesh.BoundingBoxMin = ReadVector3(mdlPtr, ref pos);
            mesh.BoundingBoxMax = ReadVector3(mdlPtr, ref pos);
            mesh.Radius = ReadFloat(mdlPtr, ref pos);
            mesh.AveragePoint = ReadVector3(mdlPtr, ref pos);

            mesh.DiffuseColor = ReadVector3(mdlPtr, ref pos);
            mesh.AmbientColor = ReadVector3(mdlPtr, ref pos);
            mesh.TransparencyHint = ReadUInt32(mdlPtr, ref pos);

            mesh.Texture0 = ReadFixedString(mdlPtr, ref pos, 32);
            mesh.Texture1 = ReadFixedString(mdlPtr, ref pos, 32);
            mesh.Texture2 = ReadFixedString(mdlPtr, ref pos, 12);
            mesh.Texture3 = ReadFixedString(mdlPtr, ref pos, 12);

            int indicesCountArrayOffset = ReadInt32(mdlPtr, ref pos);
            int indicesCountArrayCount = ReadInt32(mdlPtr, ref pos);
            int indicesCountArrayCountDup = ReadInt32(mdlPtr, ref pos);
            int indicesOffsetArrayOffset = ReadInt32(mdlPtr, ref pos);
            int indicesOffsetArrayCount = ReadInt32(mdlPtr, ref pos);
            int indicesOffsetArrayCountDup = ReadInt32(mdlPtr, ref pos);
            int invertedCounterOffset = ReadInt32(mdlPtr, ref pos);
            int invertedCounterCount = ReadInt32(mdlPtr, ref pos);
            int invertedCounterCountDup = ReadInt32(mdlPtr, ref pos);

            pos += 12; // unknown values
            pos += 8;  // saber unknown
            int unknown4 = ReadInt32(mdlPtr, ref pos);

            mesh.UVDirectionX = ReadFloat(mdlPtr, ref pos);
            mesh.UVDirectionY = ReadFloat(mdlPtr, ref pos);
            mesh.UVJitter = ReadFloat(mdlPtr, ref pos);
            mesh.UVJitterSpeed = ReadFloat(mdlPtr, ref pos);

            mesh.MDXVertexSize = ReadInt32(mdlPtr, ref pos);
            mesh.MDXDataFlags = ReadUInt32(mdlPtr, ref pos);
            mesh.MDXPositionOffset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXNormalOffset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXColorOffset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXTex0Offset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXTex1Offset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXTex2Offset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXTex3Offset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXTangentOffset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXUnknown1Offset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXUnknown2Offset = ReadInt32(mdlPtr, ref pos);
            mesh.MDXUnknown3Offset = ReadInt32(mdlPtr, ref pos);

            mesh.VertexCount = ReadUInt16(mdlPtr, ref pos);
            mesh.TextureCount = ReadUInt16(mdlPtr, ref pos);
            mesh.HasLightmap = ReadByte(mdlPtr, ref pos) != 0;
            mesh.RotateTexture = ReadByte(mdlPtr, ref pos) != 0;
            mesh.BackgroundGeometry = ReadByte(mdlPtr, ref pos) != 0;
            mesh.Shadow = ReadByte(mdlPtr, ref pos) != 0;
            mesh.Beaming = ReadByte(mdlPtr, ref pos) != 0;
            mesh.Render = ReadByte(mdlPtr, ref pos) != 0;
            pos += 2; // unknown + padding
            mesh.TotalArea = ReadFloat(mdlPtr, ref pos);
            pos += 4; // unknown

            if (_isTSL)
            {
                pos += 8; // TSL extra bytes
            }

            mesh.MDXDataOffset = ReadInt32(mdlPtr, ref pos);
            int vertexArrayOffset = ReadInt32(mdlPtr, ref pos);

            // Read skinmesh data if applicable
            if ((nodeType & MDLConstants.NODE_HAS_SKIN) != 0)
            {
                mesh.Skin = ReadSkinData(mdlPtr, ref pos);
            }
            else if ((nodeType & MDLConstants.NODE_HAS_DANGLY) != 0)
            {
                ReadDanglymeshData(mdlPtr, ref pos, mesh);
            }
            else if ((nodeType & MDLConstants.NODE_HAS_AABB) != 0)
            {
                // AABB tree offset
                int aabbTreeOffset = ReadInt32(mdlPtr, ref pos);
                // We could read the AABB tree here if needed for collision
            }
            else if ((nodeType & MDLConstants.NODE_HAS_SABER) != 0)
            {
                ReadSaberMeshData(mdlPtr, ref pos, mesh);
            }

            // Bulk read faces
            if (mesh.FaceCount > 0)
            {
                mesh.Faces = ReadFaces(mdlPtr, MDLConstants.FILE_HEADER_SIZE + faceArrayOffset, mesh.FaceCount);
            }
            else
            {
                mesh.Faces = Array.Empty<MDLFaceData>();
            }

            // Read vertex indices
            if (indicesCountArrayCount > 0 && indicesOffsetArrayCount > 0)
            {
                int[] indicesCounts = ReadInt32Array(mdlPtr, MDLConstants.FILE_HEADER_SIZE + indicesCountArrayOffset, indicesCountArrayCount);
                int[] indicesOffsets = ReadInt32Array(mdlPtr, MDLConstants.FILE_HEADER_SIZE + indicesOffsetArrayOffset, indicesOffsetArrayCount);

                if (indicesCounts.Length > 0 && indicesOffsets.Length > 0 && indicesCounts[0] > 0)
                {
                    // Validate offset is non-negative (negative offsets indicate invalid data)
                    if (indicesOffsets[0] >= 0)
                    {
                        long absoluteOffsetLong = (long)MDLConstants.FILE_HEADER_SIZE + indicesOffsets[0];
                        if (absoluteOffsetLong >= 0 && absoluteOffsetLong < _mdlData.Length)
                        {
                            mesh.Indices = ReadUInt16Array(mdlPtr, (int)absoluteOffsetLong, indicesCounts[0]);
                        }
                        else
                        {
                            throw new InvalidDataException(
                                $"Mesh indices offset is out of bounds: " +
                                $"FILE_HEADER_SIZE ({MDLConstants.FILE_HEADER_SIZE}) + " +
                                $"indicesOffsets[0] ({indicesOffsets[0]}) = {absoluteOffsetLong}, " +
                                $"file length = {_mdlData.Length}"
                            );
                        }
                    }
                    else
                    {
                        throw new InvalidDataException(
                            $"Mesh indices offset is negative ({indicesOffsets[0]}), indicating corrupted data."
                        );
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

            // OPTIMIZED: Single-pass bulk MDX vertex data reading
            // Reference: reone mdlmdxreader.cpp - bulk vertex reading
            ReadMdxVertexDataOptimized(mesh);

            return mesh;
        }

        /// <summary>
        /// Ultra-optimized MDX vertex data reading using pre-computed offsets and unsafe code.
        /// This is the key performance optimization - reads all vertex data in a single pass
        /// with minimal pointer arithmetic overhead.
        /// 
        /// Reference: reone/src/libs/graphics/format/mdlmdxreader.cpp:380-384
        /// Reference: KotOR.js/src/loaders/MDLLoader.ts - typed array bulk operations
        /// </summary>
        private void ReadMdxVertexDataOptimized(MDLMeshData mesh)
        {
            if (mesh.VertexCount == 0 || mesh.MDXVertexSize == 0)
            {
                mesh.Positions = Array.Empty<Vector3Data>();
                mesh.Normals = Array.Empty<Vector3Data>();
                mesh.TexCoords0 = Array.Empty<Vector2Data>();
                mesh.TexCoords1 = Array.Empty<Vector2Data>();
                return;
            }

            // Validate MDX data bounds (check for potential integer overflow)
            long totalVertexBytesLong = (long)mesh.VertexCount * mesh.MDXVertexSize;
            if (totalVertexBytesLong > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"MDX vertex data size calculation overflow: vertexCount={mesh.VertexCount}, " +
                    $"vertexSize={mesh.MDXVertexSize}"
                );
            }
            int totalVertexBytes = (int)totalVertexBytesLong;
            long maxOffsetLong = (long)mesh.MDXDataOffset + totalVertexBytes;
            if (maxOffsetLong > _mdxData.Length)
            {
                throw new InvalidOperationException(
                    $"MDX vertex data extends beyond file bounds: " +
                    $"offset={mesh.MDXDataOffset}, vertexCount={mesh.VertexCount}, " +
                    $"vertexSize={mesh.MDXVertexSize}, totalBytes={totalVertexBytes}, " +
                    $"fileLength={_mdxData.Length}"
                );
            }

            // Pre-allocate arrays for all possible vertex attributes
            mesh.Positions = new Vector3Data[mesh.VertexCount];
            mesh.Normals = new Vector3Data[mesh.VertexCount];

            // Allocate texture coordinate arrays only if needed
            uint flags = mesh.MDXDataFlags;
            if ((flags & MDLConstants.MDX_TEX0_VERTICES) != 0)
            {
                mesh.TexCoords0 = new Vector2Data[mesh.VertexCount];
            }
            if ((flags & MDLConstants.MDX_TEX1_VERTICES) != 0)
            {
                mesh.TexCoords1 = new Vector2Data[mesh.VertexCount];
            }
            if ((flags & MDLConstants.MDX_TEX2_VERTICES) != 0)
            {
                mesh.TexCoords2 = new Vector2Data[mesh.VertexCount];
            }
            if ((flags & MDLConstants.MDX_TEX3_VERTICES) != 0)
            {
                mesh.TexCoords3 = new Vector2Data[mesh.VertexCount];
            }
            if ((flags & MDLConstants.MDX_VERTEX_COLORS) != 0)
            {
                mesh.Colors = new Vector3Data[mesh.VertexCount];
            }
            if ((flags & MDLConstants.MDX_TANGENT_SPACE) != 0)
            {
                mesh.Tangents = new Vector3Data[mesh.VertexCount];
                mesh.Bitangents = new Vector3Data[mesh.VertexCount];
            }

            // Pre-compute all vertex attribute offsets once
            VertexOffsets offsets;
            offsets.Position = mesh.MDXPositionOffset;
            offsets.Normal = mesh.MDXNormalOffset;
            offsets.Color = mesh.MDXColorOffset;
            offsets.Tex0 = mesh.MDXTex0Offset;
            offsets.Tex1 = mesh.MDXTex1Offset;
            offsets.Tex2 = mesh.MDXTex2Offset;
            offsets.Tex3 = mesh.MDXTex3Offset;
            offsets.Tangent = mesh.MDXTangentOffset;
            offsets.BoneWeights = mesh.Skin?.MDXBoneWeightsOffset ?? -1;
            offsets.BoneIndices = mesh.Skin?.MDXBoneIndicesOffset ?? -1;

            // Use unsafe code for maximum performance
            fixed (byte* mdxPtr = _mdxData)
            {
                int baseOffset = mesh.MDXDataOffset;

                // Single-pass vertex reading with pre-computed offsets
                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    int vertexBase = baseOffset + i * mesh.MDXVertexSize;

                    // Validate vertex base offset is within bounds
                    if (vertexBase + mesh.MDXVertexSize > _mdxData.Length)
                    {
                        throw new InvalidOperationException(
                            $"Vertex {i} extends beyond MDX file bounds: " +
                            $"vertexBase={vertexBase}, vertexSize={mesh.MDXVertexSize}, " +
                            $"fileLength={_mdxData.Length}"
                        );
                    }

                    // Position (3 floats = 12 bytes)
                    // Positions array is always allocated, but check for safety
                    if ((flags & MDLConstants.MDX_VERTICES) != 0 && offsets.Position >= 0 && mesh.Positions != null)
                    {
                        int posOffset = vertexBase + offsets.Position;
                        if (posOffset + 12 <= _mdxData.Length)
                        {
                            float* posPtr = (float*)(mdxPtr + posOffset);
                            mesh.Positions[i] = new Vector3Data(posPtr[0], posPtr[1], posPtr[2]);
                        }
                    }

                    // Normal (3 floats = 12 bytes)
                    // Normals array is always allocated, but check for safety
                    if ((flags & MDLConstants.MDX_VERTEX_NORMALS) != 0 && offsets.Normal >= 0 && mesh.Normals != null)
                    {
                        int normOffset = vertexBase + offsets.Normal;
                        if (normOffset + 12 <= _mdxData.Length)
                        {
                            float* normPtr = (float*)(mdxPtr + normOffset);
                            mesh.Normals[i] = new Vector3Data(normPtr[0], normPtr[1], normPtr[2]);
                        }
                    }

                    // Texture coordinates 0 (2 floats = 8 bytes)
                    if ((flags & MDLConstants.MDX_TEX0_VERTICES) != 0 && offsets.Tex0 >= 0 && mesh.TexCoords0 != null)
                    {
                        int texOffset = vertexBase + offsets.Tex0;
                        if (texOffset + 8 <= _mdxData.Length)
                        {
                            float* texPtr = (float*)(mdxPtr + texOffset);
                            mesh.TexCoords0[i] = new Vector2Data(texPtr[0], texPtr[1]);
                        }
                    }

                    // Texture coordinates 1 (lightmap) (2 floats = 8 bytes)
                    if ((flags & MDLConstants.MDX_TEX1_VERTICES) != 0 && offsets.Tex1 >= 0 && mesh.TexCoords1 != null)
                    {
                        int texOffset = vertexBase + offsets.Tex1;
                        if (texOffset + 8 <= _mdxData.Length)
                        {
                            float* texPtr = (float*)(mdxPtr + texOffset);
                            mesh.TexCoords1[i] = new Vector2Data(texPtr[0], texPtr[1]);
                        }
                    }

                    // Texture coordinates 2 (2 floats = 8 bytes)
                    if ((flags & MDLConstants.MDX_TEX2_VERTICES) != 0 && offsets.Tex2 >= 0 && mesh.TexCoords2 != null)
                    {
                        int texOffset = vertexBase + offsets.Tex2;
                        if (texOffset + 8 <= _mdxData.Length)
                        {
                            float* texPtr = (float*)(mdxPtr + texOffset);
                            mesh.TexCoords2[i] = new Vector2Data(texPtr[0], texPtr[1]);
                        }
                    }

                    // Texture coordinates 3 (2 floats = 8 bytes)
                    if ((flags & MDLConstants.MDX_TEX3_VERTICES) != 0 && offsets.Tex3 >= 0 && mesh.TexCoords3 != null)
                    {
                        int texOffset = vertexBase + offsets.Tex3;
                        if (texOffset + 8 <= _mdxData.Length)
                        {
                            float* texPtr = (float*)(mdxPtr + texOffset);
                            mesh.TexCoords3[i] = new Vector2Data(texPtr[0], texPtr[1]);
                        }
                    }

                    // Vertex colors (3 floats = 12 bytes)
                    if ((flags & MDLConstants.MDX_VERTEX_COLORS) != 0 && offsets.Color >= 0 && mesh.Colors != null)
                    {
                        int colorOffset = vertexBase + offsets.Color;
                        if (colorOffset + 12 <= _mdxData.Length)
                        {
                            float* colorPtr = (float*)(mdxPtr + colorOffset);
                            mesh.Colors[i] = new Vector3Data(colorPtr[0], colorPtr[1], colorPtr[2]);
                        }
                    }

                    // Tangent space (9 floats = 36 bytes: tangent XYZ, bitangent XYZ, normal XYZ)
                    // We only read tangent and bitangent since we already have normals separately
                    if ((flags & MDLConstants.MDX_TANGENT_SPACE) != 0 && offsets.Tangent >= 0 &&
                        mesh.Tangents != null && mesh.Bitangents != null)
                    {
                        int tangentOffset = vertexBase + offsets.Tangent;
                        if (tangentOffset + 36 <= _mdxData.Length)
                        {
                            float* tangentPtr = (float*)(mdxPtr + tangentOffset);
                            // Tangent (first 3 floats)
                            mesh.Tangents[i] = new Vector3Data(tangentPtr[0], tangentPtr[1], tangentPtr[2]);
                            // Bitangent (floats 3-5)
                            mesh.Bitangents[i] = new Vector3Data(tangentPtr[3], tangentPtr[4], tangentPtr[5]);
                            // Normal (floats 6-8) - ignored, we use the separate normal attribute
                        }
                    }
                }

                // Read skin weights and indices if present
                if (mesh.Skin != null)
                {
                    ReadMdxSkinDataOptimized(mdxPtr, mesh, baseOffset, offsets);
                }
            }
        }

        /// <summary>
        /// Optimized skin data reading using unsafe pointers.
        /// Reads bone weights and indices from MDX vertex data for skeletal animation.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Skin mesh Specific data
        /// </summary>
        /// <param name="mdxPtr">Pointer to MDX data</param>
        /// <param name="mesh">Mesh data structure to populate</param>
        /// <param name="baseOffset">Base offset for vertex data in MDX file</param>
        /// <param name="offsets">Pre-computed vertex attribute offsets</param>
        /// <exception cref="InvalidOperationException">Thrown when skin data extends beyond MDX file bounds</exception>
        private void ReadMdxSkinDataOptimized(byte* mdxPtr, MDLMeshData mesh, int baseOffset, VertexOffsets offsets)
        {
            if (mesh.Skin == null)
            {
                return;
            }

            int vertexCount = mesh.VertexCount;

            // Check for potential integer overflow in array size calculations
            long boneWeightsCountLong = (long)vertexCount * 4;
            long boneIndicesCountLong = (long)vertexCount * 4;
            if (boneWeightsCountLong > int.MaxValue || boneIndicesCountLong > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Skin bone arrays size calculation overflow: vertexCount={vertexCount}"
                );
            }

            mesh.Skin.BoneWeights = new float[(int)boneWeightsCountLong];
            mesh.Skin.BoneIndices = new int[(int)boneIndicesCountLong];

            for (int i = 0; i < vertexCount; i++)
            {
                int vertexBase = baseOffset + i * mesh.MDXVertexSize;

                // Validate vertex base offset is within bounds
                if (vertexBase + mesh.MDXVertexSize > _mdxData.Length)
                {
                    throw new InvalidOperationException(
                        $"Skin vertex {i} extends beyond MDX file bounds: " +
                        $"vertexBase={vertexBase}, vertexSize={mesh.MDXVertexSize}, " +
                        $"fileLength={_mdxData.Length}"
                    );
                }

                // Bone weights (4 floats = 16 bytes)
                if (offsets.BoneWeights >= 0)
                {
                    int weightOffset = vertexBase + offsets.BoneWeights;
                    if (weightOffset + 16 <= _mdxData.Length)
                    {
                        float* weightPtr = (float*)(mdxPtr + weightOffset);
                        mesh.Skin.BoneWeights[i * 4 + 0] = weightPtr[0];
                        mesh.Skin.BoneWeights[i * 4 + 1] = weightPtr[1];
                        mesh.Skin.BoneWeights[i * 4 + 2] = weightPtr[2];
                        mesh.Skin.BoneWeights[i * 4 + 3] = weightPtr[3];
                    }
                }

                // Bone indices (4 floats cast to int = 16 bytes)
                // Note: Stored as floats but represent uint16 indices
                if (offsets.BoneIndices >= 0)
                {
                    int indexOffset = vertexBase + offsets.BoneIndices;
                    if (indexOffset + 16 <= _mdxData.Length)
                    {
                        float* indexPtr = (float*)(mdxPtr + indexOffset);
                        mesh.Skin.BoneIndices[i * 4 + 0] = (int)indexPtr[0];
                        mesh.Skin.BoneIndices[i * 4 + 1] = (int)indexPtr[1];
                        mesh.Skin.BoneIndices[i * 4 + 2] = (int)indexPtr[2];
                        mesh.Skin.BoneIndices[i * 4 + 3] = (int)indexPtr[3];
                    }
                }
            }
        }

        /// <summary>
        /// Reads skin mesh data from the MDL file, including bone maps, quaternion bind poses, and translation bind poses.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Skin mesh Specific data
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="pos">Current position in MDL file (will be advanced)</param>
        /// <returns>Loaded skin data for skeletal animation</returns>
        /// <exception cref="InvalidOperationException">Thrown when array size calculations overflow</exception>
        private MDLSkinData ReadSkinData(byte* mdlPtr, ref int pos)
        {
            var skin = new MDLSkinData();

            pos += 12; // unknown weights
            skin.MDXBoneWeightsOffset = ReadInt32(mdlPtr, ref pos);
            skin.MDXBoneIndicesOffset = ReadInt32(mdlPtr, ref pos);

            int boneMapOffset = ReadInt32(mdlPtr, ref pos);
            int boneCount = ReadInt32(mdlPtr, ref pos);

            int qBonesOffset = ReadInt32(mdlPtr, ref pos);
            int qBonesCount = ReadInt32(mdlPtr, ref pos);
            int qBonesCountDup = ReadInt32(mdlPtr, ref pos);

            int tBonesOffset = ReadInt32(mdlPtr, ref pos);
            int tBonesCount = ReadInt32(mdlPtr, ref pos);
            int tBonesCountDup = ReadInt32(mdlPtr, ref pos);

            pos += 12; // unknown array
            // Bone node serial numbers (16 x uint16)
            ushort[] boneNodeSerial = ReadUInt16Array(mdlPtr, pos, 16);
            pos += 32; // 16 * 2 bytes

            // Read bone map
            if (boneCount > 0)
            {
                float[] boneMapFloats = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + boneMapOffset, boneCount);
                skin.BoneMap = new int[boneCount];
                for (int i = 0; i < boneCount; i++)
                {
                    skin.BoneMap[i] = (int)boneMapFloats[i];
                }
            }
            else
            {
                skin.BoneMap = Array.Empty<int>();
            }

            // Read QBones (quaternion bind poses)
            if (qBonesCount > 0)
            {
                // Check for potential integer overflow
                long qBoneValuesCountLong = (long)qBonesCount * 4;
                if (qBoneValuesCountLong > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"QBones array size calculation overflow: qBonesCount={qBonesCount}"
                    );
                }
                float[] qBoneValues = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + qBonesOffset, (int)qBoneValuesCountLong);
                skin.QBones = new Vector4Data[qBonesCount];
                for (int i = 0; i < qBonesCount; i++)
                {
                    skin.QBones[i] = new Vector4Data(
                        qBoneValues[i * 4 + 0], // W
                        qBoneValues[i * 4 + 1], // X
                        qBoneValues[i * 4 + 2], // Y
                        qBoneValues[i * 4 + 3]  // Z
                    );
                }
            }
            else
            {
                skin.QBones = Array.Empty<Vector4Data>();
            }

            // Read TBones (translation bind poses)
            if (tBonesCount > 0)
            {
                // Check for potential integer overflow
                long tBoneValuesCountLong = (long)tBonesCount * 3;
                if (tBoneValuesCountLong > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"TBones array size calculation overflow: tBonesCount={tBonesCount}"
                    );
                }
                float[] tBoneValues = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + tBonesOffset, (int)tBoneValuesCountLong);
                skin.TBones = new Vector3Data[tBonesCount];
                for (int i = 0; i < tBonesCount; i++)
                {
                    skin.TBones[i] = new Vector3Data(
                        tBoneValues[i * 3 + 0],
                        tBoneValues[i * 3 + 1],
                        tBoneValues[i * 3 + 2]
                    );
                }
            }
            else
            {
                skin.TBones = Array.Empty<Vector3Data>();
            }

            return skin;
        }

        /// <summary>
        /// Reads danglymesh data for physics simulation (cloth/hair).
        /// Includes constraint values, displacement, tightness, and period parameters.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Danglymesh Header
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="pos">Current position in MDL file (will be advanced)</param>
        /// <param name="mesh">Mesh data structure to populate with danglymesh data</param>
        private void ReadDanglymeshData(byte* mdlPtr, ref int pos, MDLMeshData mesh)
        {
            int constraintArrayOffset = ReadInt32(mdlPtr, ref pos);
            int constraintCount = ReadInt32(mdlPtr, ref pos);
            int constraintCountDup = ReadInt32(mdlPtr, ref pos);
            float displacement = ReadFloat(mdlPtr, ref pos);
            float tightness = ReadFloat(mdlPtr, ref pos);
            float period = ReadFloat(mdlPtr, ref pos);
            int danglyVerticesOffset = ReadInt32(mdlPtr, ref pos);

            // Store danglymesh data for physics simulation
            var danglymesh = new MDLDanglymeshData
            {
                Displacement = displacement,
                Tightness = tightness,
                Period = period
            };

            // Read constraint array (one float per vertex)
            if (constraintCount > 0 && constraintArrayOffset >= 0)
            {
                danglymesh.Constraints = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + constraintArrayOffset, constraintCount);
            }
            else
            {
                danglymesh.Constraints = Array.Empty<float>();
            }

            mesh.Danglymesh = danglymesh;
        }

        /// <summary>
        /// Reads lightsaber mesh data from the MDL file.
        /// Saber meshes store vertices in MDL (not MDX) and require special vertex reordering.
        /// Reference: reone/src/libs/graphics/format/mdlmdxreader.cpp - Saber mesh handling
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="pos">Current position in MDL file (will be advanced)</param>
        /// <param name="mesh">Mesh data structure to populate with saber vertex data</param>
        /// <exception cref="InvalidOperationException">Thrown when array size calculations overflow</exception>
        private void ReadSaberMeshData(byte* mdlPtr, ref int pos, MDLMeshData mesh)
        {
            int saberVerticesOffset = ReadInt32(mdlPtr, ref pos);
            int texCoordsOffset = ReadInt32(mdlPtr, ref pos);
            int normalsOffset = ReadInt32(mdlPtr, ref pos);
            pos += 8; // unknown

            // Saber meshes store vertices in MDL, not MDX
            // Read and reorder vertices as per reone implementation
            if (mesh.VertexCount > 0)
            {
                // Check for potential integer overflow in array size calculations
                long vertsCountLong = (long)mesh.VertexCount * 3;
                long texCoordsCountLong = (long)mesh.VertexCount * 2;
                if (vertsCountLong > int.MaxValue || texCoordsCountLong > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Saber mesh vertex array size calculation overflow: vertexCount={mesh.VertexCount}"
                    );
                }
                float[] saberVerts = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + saberVerticesOffset, (int)vertsCountLong);
                float[] saberTexCoords = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + texCoordsOffset, (int)texCoordsCountLong);
                float[] saberNormals = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + normalsOffset, (int)vertsCountLong);

                mesh.Positions = new Vector3Data[mesh.VertexCount];
                mesh.Normals = new Vector3Data[mesh.VertexCount];
                mesh.TexCoords0 = new Vector2Data[mesh.VertexCount];
                mesh.TexCoords1 = new Vector2Data[mesh.VertexCount];

                for (int i = 0; i < mesh.VertexCount; i++)
                {
                    // Saber vertex reordering (from reone)
                    int vertexIdx;
                    if (i < 80)
                    {
                        vertexIdx = i + 8;
                    }
                    else if (i >= 80 && i < 88)
                    {
                        vertexIdx = i - 80;
                    }
                    else if (i >= 88 && i < 168)
                    {
                        vertexIdx = i + 8;
                    }
                    else
                    {
                        vertexIdx = i - 80;
                    }

                    // Validate vertexIdx is within bounds for all arrays
                    if (vertexIdx >= 0 && vertexIdx < mesh.VertexCount)
                    {
                        int vertBase = vertexIdx * 3;
                        int texBase = vertexIdx * 2;

                        // Additional bounds check for source arrays
                        if (vertBase + 2 < saberVerts.Length &&
                            vertBase + 2 < saberNormals.Length &&
                            texBase + 1 < saberTexCoords.Length)
                        {
                            mesh.Positions[i] = new Vector3Data(
                                saberVerts[vertBase],
                                saberVerts[vertBase + 1],
                                saberVerts[vertBase + 2]
                            );
                            mesh.Normals[i] = new Vector3Data(
                                saberNormals[vertBase],
                                saberNormals[vertBase + 1],
                                saberNormals[vertBase + 2]
                            );
                            mesh.TexCoords0[i] = new Vector2Data(
                                saberTexCoords[texBase],
                                saberTexCoords[texBase + 1]
                            );
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads face data array from the MDL file.
        /// Each face contains normal, plane distance, material, adjacent faces, and vertex indices.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Face Data
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="offset">Offset to face array in MDL file</param>
        /// <param name="count">Number of faces to read</param>
        /// <returns>Array of face data</returns>
        /// <exception cref="InvalidOperationException">Thrown when face array is out of bounds or size calculations overflow</exception>
        private MDLFaceData[] ReadFaces(byte* mdlPtr, int offset, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<MDLFaceData>();
            }

            // Validate bounds - each face is 32 bytes (3*float + float + int + 6*short = 12+4+4+12 = 32)
            // Check for potential integer overflow in multiplication
            long requiredBytesLong = (long)count * 32;
            if (requiredBytesLong > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Face array size calculation overflow: count={count}, bytesPerFace=32"
                );
            }
            int requiredBytes = (int)requiredBytesLong;
            if (offset < 0 || offset + requiredBytes > _mdlData.Length)
            {
                throw new InvalidOperationException(
                    $"Face array read out of bounds: offset={offset}, count={count}, " +
                    $"requiredBytes={requiredBytes}, dataLength={_mdlData.Length}"
                );
            }

            var faces = new MDLFaceData[count];
            int pos = offset;

            for (int i = 0; i < count; i++)
            {
                var face = new MDLFaceData();
                face.Normal = ReadVector3(mdlPtr, ref pos);
                face.PlaneDistance = ReadFloat(mdlPtr, ref pos);
                face.Material = ReadInt32(mdlPtr, ref pos);
                face.Adjacent0 = ReadInt16(mdlPtr, ref pos);
                face.Adjacent1 = ReadInt16(mdlPtr, ref pos);
                face.Adjacent2 = ReadInt16(mdlPtr, ref pos);
                face.Vertex0 = ReadInt16(mdlPtr, ref pos);
                face.Vertex1 = ReadInt16(mdlPtr, ref pos);
                face.Vertex2 = ReadInt16(mdlPtr, ref pos);
                faces[i] = face;
            }

            return faces;
        }

        #endregion

        #region Light/Emitter/Reference Reading

        /// <summary>
        /// Reads light node data from the MDL file, including flare properties and texture names.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Light Header
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="pos">Current position in MDL file (will be advanced)</param>
        /// <returns>Loaded light data</returns>
        /// <exception cref="InvalidOperationException">Thrown when array size calculations overflow</exception>
        private MDLLightData ReadLightData(byte* mdlPtr, ref int pos)
        {
            var light = new MDLLightData();
            pos += 16; // unknown padding

            int flareSizesOffset = ReadInt32(mdlPtr, ref pos);
            int flareSizesCount = ReadInt32(mdlPtr, ref pos);
            pos += 4;

            int flarePositionsOffset = ReadInt32(mdlPtr, ref pos);
            int flarePositionsCount = ReadInt32(mdlPtr, ref pos);
            pos += 4;

            int flareColorShiftsOffset = ReadInt32(mdlPtr, ref pos);
            int flareColorShiftsCount = ReadInt32(mdlPtr, ref pos);
            pos += 4;

            int flareTextureNamesOffset = ReadInt32(mdlPtr, ref pos);
            int flareTextureNamesCount = ReadInt32(mdlPtr, ref pos);
            pos += 4;

            light.FlareRadius = ReadFloat(mdlPtr, ref pos);
            light.LightPriority = ReadInt32(mdlPtr, ref pos);
            light.AmbientOnly = ReadInt32(mdlPtr, ref pos) != 0;
            light.DynamicType = ReadInt32(mdlPtr, ref pos);
            light.AffectDynamic = ReadInt32(mdlPtr, ref pos) != 0;
            light.Shadow = ReadInt32(mdlPtr, ref pos) != 0;
            light.Flare = ReadInt32(mdlPtr, ref pos) != 0;
            light.FadingLight = ReadInt32(mdlPtr, ref pos) != 0;

            // Bulk read flare data if present
            if (flareSizesCount > 0)
            {
                light.FlareSizes = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + flareSizesOffset, flareSizesCount);
            }
            if (flarePositionsCount > 0)
            {
                light.FlarePositions = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + flarePositionsOffset, flarePositionsCount);
            }
            if (flareColorShiftsCount > 0)
            {
                // Check for potential integer overflow
                long colorDataCountLong = (long)flareColorShiftsCount * 3;
                if (colorDataCountLong > int.MaxValue)
                {
                    throw new InvalidOperationException(
                        $"Flare color shifts array size calculation overflow: flareColorShiftsCount={flareColorShiftsCount}"
                    );
                }
                float[] colorData = ReadFloatArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + flareColorShiftsOffset, (int)colorDataCountLong);
                light.FlareColorShifts = new Vector3Data[flareColorShiftsCount];
                for (int i = 0; i < flareColorShiftsCount; i++)
                {
                    light.FlareColorShifts[i] = new Vector3Data(
                        colorData[i * 3],
                        colorData[i * 3 + 1],
                        colorData[i * 3 + 2]
                    );
                }
            }
            if (flareTextureNamesCount > 0)
            {
                light.FlareTextures = ReadStringArray(mdlPtr, MDLConstants.FILE_HEADER_SIZE + flareTextureNamesOffset, flareTextureNamesCount);
            }
            else
            {
                light.FlareTextures = Array.Empty<string>();
            }

            return light;
        }

        /// <summary>
        /// Reads emitter node data from the MDL file, including particle system properties and scripts.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Emitter Header
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="pos">Current position in MDL file (will be advanced)</param>
        /// <returns>Loaded emitter data</returns>
        private MDLEmitterData ReadEmitterData(byte* mdlPtr, ref int pos)
        {
            var emitter = new MDLEmitterData();

            emitter.DeadSpace = ReadFloat(mdlPtr, ref pos);
            emitter.BlastRadius = ReadFloat(mdlPtr, ref pos);
            emitter.BlastLength = ReadFloat(mdlPtr, ref pos);
            emitter.BranchCount = ReadInt32(mdlPtr, ref pos);
            emitter.ControlPtSmoothing = ReadFloat(mdlPtr, ref pos);
            emitter.XGrid = ReadInt32(mdlPtr, ref pos);
            emitter.YGrid = ReadInt32(mdlPtr, ref pos);
            pos += 4; // padding

            emitter.UpdateScript = ReadFixedString(mdlPtr, ref pos, 32);
            emitter.RenderScript = ReadFixedString(mdlPtr, ref pos, 32);
            emitter.BlendScript = ReadFixedString(mdlPtr, ref pos, 32);
            emitter.Texture = ReadFixedString(mdlPtr, ref pos, 32);
            emitter.ChunkName = ReadFixedString(mdlPtr, ref pos, 16);

            emitter.TwoSidedTex = ReadInt32(mdlPtr, ref pos) != 0;
            emitter.Loop = ReadInt32(mdlPtr, ref pos) != 0;
            emitter.RenderOrder = ReadUInt16(mdlPtr, ref pos);
            emitter.FrameBlending = ReadByte(mdlPtr, ref pos) != 0;

            emitter.DepthTexture = ReadFixedString(mdlPtr, ref pos, 33);
            pos += 1; // padding
            emitter.Flags = ReadUInt32(mdlPtr, ref pos);

            return emitter;
        }

        /// <summary>
        /// Reads reference node data from the MDL file.
        /// Reference nodes point to other MDL models that can be attached/detached.
        /// Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md - Reference Header
        /// </summary>
        /// <param name="mdlPtr">Pointer to MDL data</param>
        /// <param name="pos">Current position in MDL file (will be advanced)</param>
        /// <returns>Loaded reference data</returns>
        private MDLReferenceData ReadReferenceData(byte* mdlPtr, ref int pos)
        {
            var reference = new MDLReferenceData();
            reference.ModelResRef = ReadFixedString(mdlPtr, ref pos, 32);
            reference.Reattachable = ReadInt32(mdlPtr, ref pos) != 0;
            return reference;
        }

        #endregion

        #region Unsafe Primitive Reading (Zero-Copy Operations)

        /// <summary>
        /// Reads a single byte from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 1)</param>
        /// <returns>The byte value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private byte ReadByte(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos >= _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read byte at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            return ptr[pos++];
        }

        /// <summary>
        /// Reads a 16-bit signed integer from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 2)</param>
        /// <returns>The int16 value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private short ReadInt16(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos + 2 > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read int16 at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            short val = *(short*)(ptr + pos);
            pos += 2;
            return val;
        }

        /// <summary>
        /// Reads a 16-bit unsigned integer from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 2)</param>
        /// <returns>The uint16 value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private ushort ReadUInt16(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos + 2 > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read uint16 at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            ushort val = *(ushort*)(ptr + pos);
            pos += 2;
            return val;
        }

        /// <summary>
        /// Reads a 32-bit signed integer from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 4)</param>
        /// <returns>The int32 value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private int ReadInt32(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos + 4 > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read int32 at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            int val = *(int*)(ptr + pos);
            pos += 4;
            return val;
        }

        /// <summary>
        /// Reads a 32-bit unsigned integer from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 4)</param>
        /// <returns>The uint32 value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private uint ReadUInt32(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos + 4 > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read uint32 at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            uint val = *(uint*)(ptr + pos);
            pos += 4;
            return val;
        }

        /// <summary>
        /// Reads a 32-bit float from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 4)</param>
        /// <returns>The float value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private float ReadFloat(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos + 4 > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read float at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            float val = *(float*)(ptr + pos);
            pos += 4;
            return val;
        }

        /// <summary>
        /// Reads a Vector3 (3 floats = 12 bytes) from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 12)</param>
        /// <returns>The Vector3Data value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private Vector3Data ReadVector3(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos + 12 > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read Vector3 at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            float x = *(float*)(ptr + pos);
            float y = *(float*)(ptr + pos + 4);
            float z = *(float*)(ptr + pos + 8);
            pos += 12;
            return new Vector3Data(x, y, z);
        }

        /// <summary>
        /// Reads a Quaternion (4 floats = 16 bytes) from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by 16)</param>
        /// <returns>The Vector4Data value</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private Vector4Data ReadQuaternion(byte* ptr, ref int pos)
        {
            if (pos < 0 || pos + 16 > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read Quaternion at position {pos}, but MDL data length is {_mdlData.Length}. " +
                    "File may be corrupted or truncated."
                );
            }
            float w = *(float*)(ptr + pos);
            float x = *(float*)(ptr + pos + 4);
            float y = *(float*)(ptr + pos + 8);
            float z = *(float*)(ptr + pos + 12);
            pos += 16;
            return new Vector4Data(x, y, z, w);
        }

        /// <summary>
        /// Reads a fixed-length ASCII string from the MDL data with bounds checking.
        /// </summary>
        /// <param name="ptr">Pointer to MDL data</param>
        /// <param name="pos">Current position (will be incremented by length)</param>
        /// <param name="length">Number of bytes to read</param>
        /// <returns>The string value (null-terminated if found)</returns>
        /// <exception cref="InvalidDataException">Thrown when reading would exceed buffer bounds</exception>
        private string ReadFixedString(byte* ptr, ref int pos, int length)
        {
            if (pos < 0 || pos + length > _mdlData.Length)
            {
                throw new InvalidDataException(
                    $"Attempted to read fixed string of length {length} at position {pos}, " +
                    $"but MDL data length is {_mdlData.Length}. File may be corrupted or truncated."
                );
            }
            int start = pos;
            int end = start;
            int maxEnd = start + length;

            // Find null terminator (with bounds check)
            while (end < maxEnd && ptr[end] != 0)
            {
                end++;
            }

            string result = end > start ? Encoding.ASCII.GetString(ptr + start, end - start) : string.Empty;
            pos += length;
            return result;
        }

        /// <summary>
        /// Reads a null-terminated ASCII string from the MDL data.
        /// Includes bounds checking to prevent reading beyond data limits.
        /// </summary>
        private string ReadNullTerminatedString(byte* ptr, int offset)
        {
            if (offset < 0 || offset >= _mdlData.Length)
            {
                return string.Empty;
            }

            int start = offset;
            int end = start;
            int maxEnd = Math.Min(_mdlData.Length, start + 256); // Max 256 chars for safety, but also respect data bounds

            // Find null terminator (with bounds check)
            while (end < maxEnd && ptr[end] != 0)
            {
                end++;
            }

            // If we hit the max length without finding a null terminator, use empty string
            // (this indicates malformed data)
            if (end >= maxEnd && ptr[end - 1] != 0)
            {
                return string.Empty;
            }

            return end > start ? Encoding.ASCII.GetString(ptr + start, end - start) : string.Empty;
        }

        // Bulk array reading using unsafe pointers - key optimization
        // Uses direct pointer copying for maximum performance
        private int[] ReadInt32Array(byte* ptr, int offset, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<int>();
            }

            // Validate bounds (check for potential integer overflow in multiplication)
            long requiredBytesLong = (long)count * sizeof(int);
            if (requiredBytesLong > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Int32 array size calculation overflow: count={count}, sizeof(int)={sizeof(int)}"
                );
            }
            int requiredBytes = (int)requiredBytesLong;
            if (offset < 0 || offset + requiredBytes > _mdlData.Length)
            {
                throw new InvalidOperationException(
                    $"Int32 array read out of bounds: offset={offset}, count={count}, " +
                    $"requiredBytes={requiredBytes}, dataLength={_mdlData.Length}"
                );
            }

            int[] result = new int[count];
            fixed (int* resultPtr = result)
            {
                int* src = (int*)(ptr + offset);
                int* dst = resultPtr;
                for (int i = 0; i < count; i++)
                {
                    dst[i] = src[i];
                }
            }
            return result;
        }

        private float[] ReadFloatArray(byte* ptr, int offset, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<float>();
            }

            // Validate bounds (check for potential integer overflow in multiplication)
            long requiredBytesLong = (long)count * sizeof(float);
            if (requiredBytesLong > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Float array size calculation overflow: count={count}, sizeof(float)={sizeof(float)}"
                );
            }
            int requiredBytes = (int)requiredBytesLong;
            if (offset < 0 || offset + requiredBytes > _mdlData.Length)
            {
                throw new InvalidOperationException(
                    $"Float array read out of bounds: offset={offset}, count={count}, " +
                    $"requiredBytes={requiredBytes}, dataLength={_mdlData.Length}"
                );
            }

            float[] result = new float[count];
            fixed (float* resultPtr = result)
            {
                float* src = (float*)(ptr + offset);
                float* dst = resultPtr;
                for (int i = 0; i < count; i++)
                {
                    dst[i] = src[i];
                }
            }
            return result;
        }

        private ushort[] ReadUInt16Array(byte* ptr, int offset, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<ushort>();
            }

            // Validate bounds (check for potential integer overflow in multiplication)
            long requiredBytesLong = (long)count * sizeof(ushort);
            if (requiredBytesLong > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"UInt16 array size calculation overflow: count={count}, sizeof(ushort)={sizeof(ushort)}"
                );
            }
            int requiredBytes = (int)requiredBytesLong;
            if (offset < 0 || offset + requiredBytes > _mdlData.Length)
            {
                throw new InvalidOperationException(
                    $"UInt16 array read out of bounds: offset={offset}, count={count}, " +
                    $"requiredBytes={requiredBytes}, dataLength={_mdlData.Length}"
                );
            }

            ushort[] result = new ushort[count];
            fixed (ushort* resultPtr = result)
            {
                ushort* src = (ushort*)(ptr + offset);
                ushort* dst = resultPtr;
                for (int i = 0; i < count; i++)
                {
                    dst[i] = src[i];
                }
            }
            return result;
        }

        /// <summary>
        /// Reads an array of strings stored as offsets to null-terminated strings.
        /// Reference: vendor/Kotor.NET/Kotor.NET/Formats/BinaryMDL/MDLBinaryLight.cs:44-54
        /// </summary>
        private string[] ReadStringArray(byte* ptr, int offsetArrayOffset, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<string>();
            }

            string[] result = new string[count];

            // First read the array of offsets (int32 array)
            int[] offsets = ReadInt32Array(ptr, offsetArrayOffset, count);

            // Then read each string at its offset
            for (int i = 0; i < count; i++)
            {
                // Offset is relative to FILE_HEADER_SIZE (0x0C)
                // Validate offset is non-negative (negative offsets indicate invalid data)
                if (offsets[i] >= 0)
                {
                    int stringOffset = MDLConstants.FILE_HEADER_SIZE + offsets[i];
                    // Validate offset is within MDL data bounds
                    if (stringOffset >= 0 && stringOffset < _mdlData.Length)
                    {
                        result[i] = ReadNullTerminatedString(ptr, stringOffset);
                    }
                    else
                    {
                        // Invalid offset - use empty string
                        result[i] = string.Empty;
                    }
                }
                else
                {
                    result[i] = string.Empty;
                }
            }

            return result;
        }

        #endregion

        /// <summary>
        /// Releases all resources used by the MDLOptimizedReader.
        /// After disposal, calling Load() will throw ObjectDisposedException.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }
    }
}

