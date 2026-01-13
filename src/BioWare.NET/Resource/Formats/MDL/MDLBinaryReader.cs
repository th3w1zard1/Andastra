using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.MDL;
using BioWare.NET.Resource.Formats.MDLData;

namespace BioWare.NET.Resource.Formats.MDL
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:1580-1899
    // Original: class MDLBinaryReader
    // Full comprehensive implementation with 1:1 parity to PyKotor reference
    public class MDLBinaryReader : IDisposable
    {
        private readonly RawBinaryReader _reader;
        private readonly RawBinaryReader _readerExt;
        private readonly bool _fastLoad;
        private MDLData.MDL _mdl;
        private List<string> _names;

        // MDX data flags (matching PyKotor _MDXDataFlags)
        private static class MDXDataFlags
        {
            public const int VERTEX = 0x0001;
            public const int TEXTURE1 = 0x0002;
            public const int TEXTURE2 = 0x0004;
            public const int NORMAL = 0x0020;
            public const int BUMPMAP = 0x0080;
        }

        // Internal binary structure classes (matching PyKotor _ModelHeader, _GeometryHeader, etc.)
        private class ModelHeader
        {
            public const int SIZE = 196;
            public GeometryHeader Geometry;
            public byte ModelType;  // Model classification (see ModelClassificationFlags)
            public byte Unknown0;   // Subclassification value - purpose unknown
            public byte Padding0;   // Padding byte for alignment
            public byte Fog;        // Fog enabled flag
            public uint Unknown1;   // Unknown uint32 field - possibly reserved or deprecated
            public uint OffsetToAnimations;
            public uint AnimationCount;
            public uint AnimationCount2;
            public uint Unknown2;   // Unknown uint32 field - possibly reserved or deprecated
            public Vector3 BoundingBoxMin;
            public Vector3 BoundingBoxMax;
            public float Radius;
            public float AnimScale;
            public string Supermodel;
            public uint OffsetToSuperRoot;
            public uint Unknown3;
            public uint MdxSize;
            public uint MdxOffset;
            public uint OffsetToNameOffsets;
            public uint NameOffsetsCount;
            public uint NameOffsetsCount2;

            public ModelHeader Read(RawBinaryReader reader)
            {
                Geometry = new GeometryHeader().Read(reader);
                ModelType = reader.ReadUInt8();
                Unknown0 = reader.ReadUInt8();
                Padding0 = reader.ReadUInt8();
                Fog = reader.ReadUInt8();
                Unknown1 = reader.ReadUInt32();
                OffsetToAnimations = reader.ReadUInt32();
                AnimationCount = reader.ReadUInt32();
                AnimationCount2 = reader.ReadUInt32();
                Unknown2 = reader.ReadUInt32();
                BoundingBoxMin = reader.ReadVector3();
                BoundingBoxMax = reader.ReadVector3();
                Radius = reader.ReadSingle();
                AnimScale = reader.ReadSingle();
                Supermodel = reader.ReadTerminatedString('\0', 32, "ascii");
                OffsetToSuperRoot = reader.ReadUInt32();
                Unknown3 = reader.ReadUInt32();
                MdxSize = reader.ReadUInt32();
                MdxOffset = reader.ReadUInt32();
                OffsetToNameOffsets = reader.ReadUInt32();
                NameOffsetsCount = reader.ReadUInt32();
                NameOffsetsCount2 = reader.ReadUInt32();
                return this;
            }
        }

        private class GeometryHeader
        {
            public const int SIZE = 80;
            public const uint K1_FUNCTION_POINTER0 = 4273776;
            public const uint K2_FUNCTION_POINTER0 = 4285200;
            public const uint K1_ANIM_FUNCTION_POINTER0 = 4273392;
            public const uint K2_ANIM_FUNCTION_POINTER0 = 4284816;
            public const uint K1_FUNCTION_POINTER1 = 4216096;
            public const uint K2_FUNCTION_POINTER1 = 4216320;
            public const uint K1_ANIM_FUNCTION_POINTER1 = 4451552;
            public const uint K2_ANIM_FUNCTION_POINTER1 = 4522928;
            public const int GEOM_TYPE_ROOT = 2;
            public const int GEOM_TYPE_ANIM = 5;

            public uint FunctionPointer0;
            public uint FunctionPointer1;
            public string ModelName;
            public uint RootNodeOffset;
            public uint NodeCount;
            public byte[] Unknown0;  // Unknown data block (28 bytes) - includes unknown arrays from geometry header
            public int GeometryType;
            public byte[] Padding;

            public GeometryHeader()
            {
                Unknown0 = new byte[28];
                Padding = new byte[3];
            }

            public GeometryHeader Read(RawBinaryReader reader)
            {
                FunctionPointer0 = reader.ReadUInt32();
                FunctionPointer1 = reader.ReadUInt32();
                ModelName = reader.ReadTerminatedString('\0', 32, "ascii");
                RootNodeOffset = reader.ReadUInt32();
                NodeCount = reader.ReadUInt32();
                Unknown0 = reader.ReadBytes(28);
                GeometryType = reader.ReadUInt8();
                Padding = reader.ReadBytes(3);
                return this;
            }
        }

        private class AnimationHeader
        {
            public const int SIZE = GeometryHeader.SIZE + 56;
            public GeometryHeader Geometry;
            public float Duration;
            public float Transition;
            public string Root;
            public uint OffsetToEvents;
            public uint EventCount;
            public uint EventCount2;
            public uint Unknown0;  // Unknown uint32 field in animation header - purpose unknown

            public AnimationHeader Read(RawBinaryReader reader)
            {
                Geometry = new GeometryHeader().Read(reader);
                Duration = reader.ReadSingle();
                Transition = reader.ReadSingle();
                Root = reader.ReadTerminatedString('\0', 32, "ascii");
                OffsetToEvents = reader.ReadUInt32();
                EventCount = reader.ReadUInt32();
                EventCount2 = reader.ReadUInt32();
                Unknown0 = reader.ReadUInt32();
                return this;
            }
        }

        private class EventStructure
        {
            public const int SIZE = 36;
            public float ActivationTime;
            public string EventName;

            public EventStructure Read(RawBinaryReader reader)
            {
                ActivationTime = reader.ReadSingle();
                EventName = reader.ReadTerminatedString('\0', 32, "ascii");
                return this;
            }
        }

        private class Controller
        {
            public const int SIZE = 16;
            public uint TypeId;     // Controller type identifier
            public ushort Unknown0;  // Unknown field in controller header - possibly unused/reserved
            public ushort RowCount;
            public ushort KeyOffset;
            public ushort DataOffset;
            public byte ColumnCount;
            public byte[] Unknown1;  // Unknown padding bytes (3 bytes) for 16-byte alignment

            public Controller()
            {
                Unknown1 = new byte[3];
            }

            public Controller Read(RawBinaryReader reader)
            {
                TypeId = reader.ReadUInt32();
                Unknown0 = reader.ReadUInt16();
                RowCount = reader.ReadUInt16();
                KeyOffset = reader.ReadUInt16();
                DataOffset = reader.ReadUInt16();
                ColumnCount = reader.ReadUInt8();
                Unknown1 = reader.ReadBytes(3);
                return this;
            }
        }

        private class NodeHeader
        {
            public const int SIZE = 80;
            public ushort TypeId;
            public ushort NodeId;
            public ushort NameId;
            public ushort Padding0;
            public uint OffsetToRoot;
            public uint OffsetToParent;
            public Vector3 Position;
            public Vector4 Orientation;
            public uint OffsetToChildren;
            public uint ChildrenCount;
            public uint ChildrenCount2;
            public uint OffsetToControllers;
            public uint ControllerCount;
            public uint ControllerCount2;
            public uint OffsetToControllerData;
            public uint ControllerDataLength;
            public uint ControllerDataLength2;

            public NodeHeader Read(RawBinaryReader reader)
            {
                TypeId = reader.ReadUInt16();
                NodeId = reader.ReadUInt16();
                NameId = reader.ReadUInt16();
                Padding0 = reader.ReadUInt16();
                OffsetToRoot = reader.ReadUInt32();
                OffsetToParent = reader.ReadUInt32();
                Position = reader.ReadVector3();
                // Quaternion is stored as w, x, y, z in binary format
                float w = reader.ReadSingle();
                float x = reader.ReadSingle();
                float y = reader.ReadSingle();
                float z = reader.ReadSingle();
                Orientation = new Vector4(x, y, z, w);
                OffsetToChildren = reader.ReadUInt32();
                ChildrenCount = reader.ReadUInt32();
                ChildrenCount2 = reader.ReadUInt32();
                OffsetToControllers = reader.ReadUInt32();
                ControllerCount = reader.ReadUInt32();
                ControllerCount2 = reader.ReadUInt32();
                OffsetToControllerData = reader.ReadUInt32();
                ControllerDataLength = reader.ReadUInt32();
                ControllerDataLength2 = reader.ReadUInt32();
                return this;
            }
        }

        private class TrimeshHeader
        {
            public const int K1_SIZE = 332;
            public const int K2_SIZE = 340;
            public const uint K1_FUNCTION_POINTER0 = 4216656;
            public const uint K2_FUNCTION_POINTER0 = 4216880;
            public const uint K1_SKIN_FUNCTION_POINTER0 = 4216592;
            public const uint K2_SKIN_FUNCTION_POINTER0 = 4216816;
            public const uint K1_DANGLY_FUNCTION_POINTER0 = 4216640;
            public const uint K2_DANGLY_FUNCTION_POINTER0 = 4216864;
            public const uint K1_FUNCTION_POINTER1 = 4216672;
            public const uint K2_FUNCTION_POINTER1 = 4216896;
            public const uint K1_SKIN_FUNCTION_POINTER1 = 4216608;
            public const uint K2_SKIN_FUNCTION_POINTER1 = 4216832;
            public const uint K1_DANGLY_FUNCTION_POINTER1 = 4216624;
            public const uint K2_DANGLY_FUNCTION_POINTER1 = 4216848;

            public uint FunctionPointer0;
            public uint FunctionPointer1;
            public uint OffsetToFaces;
            public uint FacesCount;
            public uint FacesCount2;
            public Vector3 BoundingBoxMin;
            public Vector3 BoundingBoxMax;
            public float Radius;
            public Vector3 Average;
            public Vector3 Diffuse;
            public Vector3 Ambient;
            public uint TransparencyHint;
            public string Texture1;
            public string Texture2;
            public byte[] Unknown0;
            public uint OffsetToIndicesCounts;
            public uint IndicesCountsCount;
            public uint IndicesCountsCount2;
            public uint OffsetToIndicesOffset;
            public uint IndicesOffsetsCount;
            public uint IndicesOffsetsCount2;
            public uint OffsetToCounters;
            public uint CountersCount;
            public uint CountersCount2;
            public byte[] Unknown1;
            public byte[] SaberUnknowns;
            public uint Unknown2;
            public Vector2 UvDirection;
            public float UvJitter;
            public float UvSpeed;
            public uint MdxDataSize;
            public uint MdxDataBitmap;
            public uint MdxVertexOffset;
            public uint MdxNormalOffset;
            public uint MdxColorOffset;
            public uint MdxTexture1Offset;
            public uint MdxTexture2Offset;
            public uint Unknown3;
            public uint Unknown4;
            public uint Unknown5;
            public uint Unknown6;
            public uint Unknown7;
            public uint Unknown8;
            public ushort VertexCount;
            public ushort TextureCount;
            public byte HasLightmap;
            public byte RotateTexture;
            public byte Background;
            public byte HasShadow;
            public byte Beaming;
            public byte Render;
            public byte Unknown9;
            public byte Unknown10;
            public float TotalArea;
            public uint Unknown11;
            public uint Unknown12;
            public uint Unknown13;
            public uint MdxDataOffset;
            public uint VerticesOffset;

            public List<Face> Faces;
            public List<Vector3> Vertices;
            public List<uint> IndicesOffsets;
            public List<uint> IndicesCounts;
            public List<uint> InvertedCounters;

            public TrimeshHeader()
            {
                Unknown0 = new byte[24];
                Unknown1 = new byte[12];
                SaberUnknowns = new byte[8];
                Faces = new List<Face>();
                Vertices = new List<Vector3>();
                IndicesOffsets = new List<uint>();
                IndicesCounts = new List<uint>();
                InvertedCounters = new List<uint>();
            }

            public TrimeshHeader Read(RawBinaryReader reader)
            {
                FunctionPointer0 = reader.ReadUInt32();
                FunctionPointer1 = reader.ReadUInt32();
                OffsetToFaces = reader.ReadUInt32();
                FacesCount = reader.ReadUInt32();
                FacesCount2 = reader.ReadUInt32();
                BoundingBoxMin = reader.ReadVector3();
                BoundingBoxMax = reader.ReadVector3();
                Radius = reader.ReadSingle();
                Average = reader.ReadVector3();
                Diffuse = reader.ReadVector3();
                Ambient = reader.ReadVector3();
                TransparencyHint = reader.ReadUInt32();
                Texture1 = reader.ReadTerminatedString('\0', 32, "ascii");
                Texture2 = reader.ReadTerminatedString('\0', 32, "ascii");
                Unknown0 = reader.ReadBytes(24);
                OffsetToIndicesCounts = reader.ReadUInt32();
                IndicesCountsCount = reader.ReadUInt32();
                IndicesCountsCount2 = reader.ReadUInt32();
                OffsetToIndicesOffset = reader.ReadUInt32();
                IndicesOffsetsCount = reader.ReadUInt32();
                IndicesOffsetsCount2 = reader.ReadUInt32();
                OffsetToCounters = reader.ReadUInt32();
                CountersCount = reader.ReadUInt32();
                CountersCount2 = reader.ReadUInt32();
                Unknown1 = reader.ReadBytes(12);
                SaberUnknowns = reader.ReadBytes(8);
                Unknown2 = reader.ReadUInt32();
                UvDirection = reader.ReadVector2();
                UvJitter = reader.ReadSingle();
                UvSpeed = reader.ReadSingle();
                MdxDataSize = reader.ReadUInt32();
                MdxDataBitmap = reader.ReadUInt32();
                MdxVertexOffset = reader.ReadUInt32();
                MdxNormalOffset = reader.ReadUInt32();
                MdxColorOffset = reader.ReadUInt32();
                MdxTexture1Offset = reader.ReadUInt32();
                MdxTexture2Offset = reader.ReadUInt32();
                Unknown3 = reader.ReadUInt32();
                Unknown4 = reader.ReadUInt32();
                Unknown5 = reader.ReadUInt32();
                Unknown6 = reader.ReadUInt32();
                Unknown7 = reader.ReadUInt32();
                Unknown8 = reader.ReadUInt32();
                VertexCount = reader.ReadUInt16();
                TextureCount = reader.ReadUInt16();
                HasLightmap = reader.ReadUInt8();
                RotateTexture = reader.ReadUInt8();
                Background = reader.ReadUInt8();
                HasShadow = reader.ReadUInt8();
                Beaming = reader.ReadUInt8();
                Render = reader.ReadUInt8();
                Unknown9 = reader.ReadUInt8();
                Unknown10 = reader.ReadUInt8();
                TotalArea = reader.ReadSingle();
                Unknown11 = reader.ReadUInt32();
                // K2 has 2 additional uint32 fields
                if (FunctionPointer0 == K2_FUNCTION_POINTER0 || FunctionPointer0 == K2_DANGLY_FUNCTION_POINTER0 || FunctionPointer0 == K2_SKIN_FUNCTION_POINTER0)
                {
                    Unknown12 = reader.ReadUInt32();
                    Unknown13 = reader.ReadUInt32();
                }
                MdxDataOffset = reader.ReadUInt32();
                VerticesOffset = reader.ReadUInt32();
                return this;
            }

            public void ReadExtra(RawBinaryReader reader)
            {
                reader.Seek((int)VerticesOffset);
                Vertices.Clear();
                for (int i = 0; i < VertexCount; i++)
                {
                    Vertices.Add(reader.ReadVector3());
                }

                reader.Seek((int)OffsetToFaces);
                Faces.Clear();
                for (int i = 0; i < FacesCount; i++)
                {
                    Faces.Add(new Face().Read(reader));
                }
            }
        }

        private class Face
        {
            public const int SIZE = 32;
            public Vector3 Normal;
            public float PlaneCoefficient;
            public uint Material;
            public ushort Adjacent1;
            public ushort Adjacent2;
            public ushort Adjacent3;
            public ushort Vertex1;
            public ushort Vertex2;
            public ushort Vertex3;

            public Face Read(RawBinaryReader reader)
            {
                Normal = reader.ReadVector3();
                PlaneCoefficient = reader.ReadSingle();
                Material = reader.ReadUInt32();
                Adjacent1 = reader.ReadUInt16();
                Adjacent2 = reader.ReadUInt16();
                Adjacent3 = reader.ReadUInt16();
                Vertex1 = reader.ReadUInt16();
                Vertex2 = reader.ReadUInt16();
                Vertex3 = reader.ReadUInt16();
                return this;
            }
        }

        private class SkinmeshHeader
        {
            public int Unknown2;
            public int Unknown3;
            public int Unknown4;
            public uint OffsetToMdxWeights;
            public uint OffsetToMdxBones;
            public uint OffsetToBonemap;
            public uint BonemapCount;
            public uint OffsetToQbones;
            public uint QbonesCount;
            public uint QbonesCount2;
            public uint OffsetToTbones;
            public uint TbonesCount;
            public uint TbonesCount2;
            public uint OffsetToUnknown0;
            public uint Unknown0Count;
            public uint Unknown0Count2;
            public ushort[] Bones;
            public uint Unknown1;

            public List<float> Bonemap;
            public List<Vector3> Tbones;
            public List<Vector4> Qbones;

            public SkinmeshHeader()
            {
                Bones = new ushort[16];
                Bonemap = new List<float>();
                Tbones = new List<Vector3>();
                Qbones = new List<Vector4>();
            }

            public SkinmeshHeader Read(RawBinaryReader reader)
            {
                Unknown2 = reader.ReadInt32();
                Unknown3 = reader.ReadInt32();
                Unknown4 = reader.ReadInt32();
                OffsetToMdxWeights = reader.ReadUInt32();
                OffsetToMdxBones = reader.ReadUInt32();
                OffsetToBonemap = reader.ReadUInt32();
                BonemapCount = reader.ReadUInt32();
                OffsetToQbones = reader.ReadUInt32();
                QbonesCount = reader.ReadUInt32();
                QbonesCount2 = reader.ReadUInt32();
                OffsetToTbones = reader.ReadUInt32();
                TbonesCount = reader.ReadUInt32();
                TbonesCount2 = reader.ReadUInt32();
                OffsetToUnknown0 = reader.ReadUInt32();
                Unknown0Count = reader.ReadUInt32();
                Unknown0Count2 = reader.ReadUInt32();
                for (int i = 0; i < 16; i++)
                {
                    Bones[i] = reader.ReadUInt16();
                }
                Unknown1 = reader.ReadUInt32();
                return this;
            }

            public void ReadExtra(RawBinaryReader reader)
            {
                reader.Seek((int)OffsetToBonemap);
                Bonemap.Clear();
                for (int i = 0; i < BonemapCount; i++)
                {
                    Bonemap.Add(reader.ReadSingle());
                }
                reader.Seek((int)OffsetToTbones);
                Tbones.Clear();
                for (int i = 0; i < TbonesCount; i++)
                {
                    Tbones.Add(reader.ReadVector3());
                }
                reader.Seek((int)OffsetToQbones);
                Qbones.Clear();
                for (int i = 0; i < QbonesCount; i++)
                {
                    float w = reader.ReadSingle();
                    float x = reader.ReadSingle();
                    float y = reader.ReadSingle();
                    float z = reader.ReadSingle();
                    Qbones.Add(new Vector4(x, y, z, w));
                }
            }
        }

        private class Node
        {
            public const int SIZE = 80;
            public NodeHeader Header;
            public TrimeshHeader Trimesh;
            public SkinmeshHeader Skin;
            public List<uint> ChildrenOffsets;

            public Node()
            {
                Header = new NodeHeader();
                ChildrenOffsets = new List<uint>();
            }

            public Node Read(RawBinaryReader reader)
            {
                Header = new NodeHeader().Read(reader);

                if ((Header.TypeId & (int)MDLNodeFlags.MESH) != 0)
                {
                    Trimesh = new TrimeshHeader().Read(reader);
                }

                if ((Header.TypeId & (int)MDLNodeFlags.SKIN) != 0)
                {
                    Skin = new SkinmeshHeader().Read(reader);
                }

                if (Trimesh != null)
                {
                    Trimesh.ReadExtra(reader);
                }
                if (Skin != null)
                {
                    Skin.ReadExtra(reader);
                }

                reader.Seek((int)Header.OffsetToChildren);
                ChildrenOffsets.Clear();
                for (int i = 0; i < Header.ChildrenCount; i++)
                {
                    ChildrenOffsets.Add(reader.ReadUInt32());
                }
                return this;
            }
        }

        private static RawBinaryReader CreateReader(object source, int offset, int? size = null)
        {
            if (source is string path)
            {
                return RawBinaryReader.FromFile(path, offset, size);
            }
            if (source is byte[] bytes)
            {
                return RawBinaryReader.FromBytes(bytes, offset, size);
            }
            if (source is Stream stream)
            {
                return RawBinaryReader.FromStream(stream, offset, size);
            }
            throw new ArgumentException("Unsupported source type for MDL");
        }

        public MDLBinaryReader(object source, int offset = 0, int size = 0, object mdxSource = null, int mdxOffset = 0, int mdxSize = 0, bool fastLoad = false)
        {
            _reader = CreateReader(source, offset, size > 0 ? (int?)size : null);
            // First 12 bytes do not count in offsets used within the file
            // Matching PyKotor: self._reader.set_offset(self._reader.offset() + 12)
            // Skip the file header (12 bytes: unused, mdl_size, mdx_size)
            // We need to read the file header first to get sizes, then adjust offset
            RawBinaryReader tempReader = CreateReader(source, offset, 12);
            tempReader.ReadUInt32(); // unused (always 0)
            uint mdlSize = tempReader.ReadUInt32();
            uint mdxSizeFromHeader = tempReader.ReadUInt32();
            tempReader.Dispose();

            // Now create reader with offset 12 (after file header)
            _reader = CreateReader(source, offset + 12, size > 12 ? (int?)(size - 12) : (int?)(mdlSize - 12));

            if (mdxSource != null)
            {
                _readerExt = CreateReader(mdxSource, mdxOffset, mdxSize > 0 ? (int?)mdxSize : (mdxSizeFromHeader > 0 ? (int?)mdxSizeFromHeader : null));
            }
            else
            {
                _readerExt = null;
            }
            _fastLoad = fastLoad;
        }

        public MDLData.MDL Load(bool autoClose = true)
        {
            try
            {
                _mdl = new MDLData.MDL();
                _names = new List<string>();

                ModelHeader modelHeader = new ModelHeader().Read(_reader);

                _mdl.Name = modelHeader.Geometry.ModelName;
                _mdl.Supermodel = modelHeader.Supermodel;
                _mdl.Fog = modelHeader.Fog != 0;

                LoadNames(modelHeader);
                _mdl.Root = LoadNode(modelHeader.Geometry.RootNodeOffset);

                // Skip animations when fast loading (not needed for rendering)
                if (!_fastLoad)
                {
                    _reader.Seek((int)modelHeader.OffsetToAnimations);
                    List<uint> animationOffsets = new List<uint>();
                    for (int i = 0; i < modelHeader.AnimationCount; i++)
                    {
                        animationOffsets.Add(_reader.ReadUInt32());
                    }
                    foreach (uint animationOffset in animationOffsets)
                    {
                        MDLAnimation anim = LoadAnim(animationOffset);
                        _mdl.Anims.Add(anim);
                    }
                }

                if (autoClose)
                {
                    _reader?.Dispose();
                    _readerExt?.Dispose();
                }

                return _mdl;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        private void LoadNames(ModelHeader modelHeader)
        {
            _reader.Seek((int)modelHeader.OffsetToNameOffsets);
            List<uint> nameOffsets = new List<uint>();
            for (int i = 0; i < modelHeader.NameOffsetsCount; i++)
            {
                nameOffsets.Add(_reader.ReadUInt32());
            }
            foreach (uint offset in nameOffsets)
            {
                _reader.Seek((int)offset);
                string name = _reader.ReadTerminatedString('\0');
                _names.Add(name);
            }
        }

        private MDLNode LoadNode(uint offset)
        {
            _reader.Seek((int)offset);
            Node binNode = new Node().Read(_reader);

            MDLNode node = new MDLNode();
            node.NodeId = binNode.Header.NodeId;
            if (binNode.Header.NameId < _names.Count)
            {
                node.Name = _names[(int)binNode.Header.NameId];
            }
            node.Position = binNode.Header.Position;
            node.Orientation = binNode.Header.Orientation;

            if (binNode.Trimesh != null)
            {
                node.Mesh = new MDLMesh();
                node.Mesh.Shadow = binNode.Trimesh.HasShadow;
                node.Mesh.Render = binNode.Trimesh.Render;
                node.Mesh.BackgroundGeometry = binNode.Trimesh.Background != 0;
                node.Mesh.HasLightmap = binNode.Trimesh.HasLightmap != 0;
                node.Mesh.Beaming = binNode.Trimesh.Beaming;
                // Convert BGR Vector3 to RGB Vector3 (BGR is stored as Z, Y, X)
                node.Mesh.Diffuse = new Vector3(binNode.Trimesh.Diffuse.Z, binNode.Trimesh.Diffuse.Y, binNode.Trimesh.Diffuse.X);
                node.Mesh.Ambient = new Vector3(binNode.Trimesh.Ambient.Z, binNode.Trimesh.Ambient.Y, binNode.Trimesh.Ambient.X);
                node.Mesh.Texture1 = binNode.Trimesh.Texture1;
                node.Mesh.Texture2 = binNode.Trimesh.Texture2;
                node.Mesh.BBoxMinX = binNode.Trimesh.BoundingBoxMin.X;
                node.Mesh.BBoxMinY = binNode.Trimesh.BoundingBoxMin.Y;
                node.Mesh.BBoxMinZ = binNode.Trimesh.BoundingBoxMin.Z;
                node.Mesh.BBoxMaxX = binNode.Trimesh.BoundingBoxMax.X;
                node.Mesh.BBoxMaxY = binNode.Trimesh.BoundingBoxMax.Y;
                node.Mesh.BBoxMaxZ = binNode.Trimesh.BoundingBoxMax.Z;
                node.Mesh.BbMin = binNode.Trimesh.BoundingBoxMin;
                node.Mesh.BbMax = binNode.Trimesh.BoundingBoxMax;
                node.Mesh.Radius = binNode.Trimesh.Radius;
                node.Mesh.AveragePoint = binNode.Trimesh.Average;
                node.Mesh.Area = binNode.Trimesh.TotalArea;
                // Saber unknowns stored as bytes - convert to tuple of ints
                if (binNode.Trimesh.SaberUnknowns != null && binNode.Trimesh.SaberUnknowns.Length == 8)
                {
                    // Store as individual properties if needed, or keep as bytes
                }

                node.Mesh.VertexPositions = binNode.Trimesh.Vertices.ToList();

                // Initialize MDX data lists
                if ((binNode.Trimesh.MdxDataBitmap & MDXDataFlags.NORMAL) != 0 && _readerExt != null)
                {
                    node.Mesh.VertexNormals = new List<Vector3>();
                }
                if ((binNode.Trimesh.MdxDataBitmap & MDXDataFlags.TEXTURE1) != 0 && _readerExt != null)
                {
                    node.Mesh.VertexUv1 = new List<Vector2>();
                }
                if ((binNode.Trimesh.MdxDataBitmap & MDXDataFlags.TEXTURE2) != 0 && _readerExt != null)
                {
                    node.Mesh.VertexUv2 = new List<Vector2>();
                }

                // Read MDX data for each vertex
                uint mdxOffset = binNode.Trimesh.MdxDataOffset;
                uint mdxBlockSize = binNode.Trimesh.MdxDataSize;
                for (int i = 0; i < binNode.Trimesh.Vertices.Count; i++)
                {
                    if ((binNode.Trimesh.MdxDataBitmap & MDXDataFlags.NORMAL) != 0 && _readerExt != null)
                    {
                        _readerExt.Seek((int)(mdxOffset + i * mdxBlockSize + binNode.Trimesh.MdxNormalOffset));
                        float x = _readerExt.ReadSingle();
                        float y = _readerExt.ReadSingle();
                        float z = _readerExt.ReadSingle();
                        node.Mesh.VertexNormals.Add(new Vector3(x, y, z));
                    }

                    if ((binNode.Trimesh.MdxDataBitmap & MDXDataFlags.TEXTURE1) != 0 && _readerExt != null)
                    {
                        _readerExt.Seek((int)(mdxOffset + i * mdxBlockSize + binNode.Trimesh.MdxTexture1Offset));
                        float u = _readerExt.ReadSingle();
                        float v = _readerExt.ReadSingle();
                        node.Mesh.VertexUv1.Add(new Vector2(u, v));
                    }

                    if ((binNode.Trimesh.MdxDataBitmap & MDXDataFlags.TEXTURE2) != 0 && _readerExt != null)
                    {
                        _readerExt.Seek((int)(mdxOffset + i * mdxBlockSize + binNode.Trimesh.MdxTexture2Offset));
                        float u = _readerExt.ReadSingle();
                        float v = _readerExt.ReadSingle();
                        node.Mesh.VertexUv2.Add(new Vector2(u, v));
                    }
                }

                // Convert faces
                foreach (Face binFace in binNode.Trimesh.Faces)
                {
                    MDLFace face = new MDLFace();
                    node.Mesh.Faces.Add(face);
                    face.V1 = binFace.Vertex1;
                    face.V2 = binFace.Vertex2;
                    face.V3 = binFace.Vertex3;
                    face.A1 = binFace.Adjacent1;
                    face.A2 = binFace.Adjacent2;
                    face.A3 = binFace.Adjacent3;
                    face.Normal = binFace.Normal;
                    face.Coefficient = (int)binFace.PlaneCoefficient;
                    face.PlaneDistance = binFace.PlaneCoefficient;
                    // Extract material from packed 32-bit value (low 5 bits)
                    face.Material = (SurfaceMaterial)(binFace.Material & 0x1F);
                    // Store smoothing group from upper bits if needed
                    face.SmoothingGroup = (int)((binFace.Material >> 5) & 0x7FFFFFF);
                }
            }

            if (binNode.Skin != null)
            {
                node.Skin = new MDLSkin();
                node.Skin.BoneIndices = binNode.Skin.Bones.Select(b => (int)b).ToList();
                // Bonemap is stored as floats in binary, convert to ints
                node.Skin.BoneNumbers = binNode.Skin.Bonemap.Select(b => (int)b).ToList();
                node.Skin.Tbones = binNode.Skin.Tbones.ToList();
                node.Skin.Qbones = binNode.Skin.Qbones.ToList();

                if (_readerExt != null && binNode.Trimesh != null)
                {
                    for (int i = 0; i < binNode.Trimesh.Vertices.Count; i++)
                    {
                        MDLBoneVertex vertexBone = new MDLBoneVertex();
                        node.Skin.VertexBones.Add(vertexBone);

                        uint mdxOffset = binNode.Trimesh.MdxDataOffset + (uint)(i * binNode.Trimesh.MdxDataSize);
                        _readerExt.Seek((int)(mdxOffset + binNode.Skin.OffsetToMdxBones));
                        float t1 = _readerExt.ReadSingle();
                        float t2 = _readerExt.ReadSingle();
                        float t3 = _readerExt.ReadSingle();
                        float t4 = _readerExt.ReadSingle();
                        vertexBone.VertexIndices = Tuple.Create(t1, t2, t3, t4);

                        _readerExt.Seek((int)(mdxOffset + binNode.Skin.OffsetToMdxWeights));
                        float w1 = _readerExt.ReadSingle();
                        float w2 = _readerExt.ReadSingle();
                        float w3 = _readerExt.ReadSingle();
                        float w4 = _readerExt.ReadSingle();
                        vertexBone.VertexWeights = Tuple.Create(w1, w2, w3, w4);
                    }
                }
            }

            // Load child nodes
            foreach (uint childOffset in binNode.ChildrenOffsets)
            {
                MDLNode childNode = LoadNode(childOffset);
                node.Children.Add(childNode);
            }

            // Skip controllers when fast loading (not needed for rendering)
            if (!_fastLoad)
            {
                for (int i = 0; i < binNode.Header.ControllerCount; i++)
                {
                    uint controllerOffset = binNode.Header.OffsetToControllers + (uint)(i * Controller.SIZE);
                    MDLController controller = LoadController(controllerOffset, binNode.Header.OffsetToControllerData);
                    node.Controllers.Add(controller);
                }
            }

            return node;
        }

        private MDLAnimation LoadAnim(uint offset)
        {
            _reader.Seek((int)offset);

            AnimationHeader binAnim = new AnimationHeader().Read(_reader);

            List<EventStructure> binEvents = new List<EventStructure>();
            _reader.Seek((int)binAnim.OffsetToEvents);
            for (int i = 0; i < binAnim.EventCount; i++)
            {
                binEvents.Add(new EventStructure().Read(_reader));
            }

            MDLAnimation anim = new MDLAnimation();
            anim.Name = binAnim.Geometry.ModelName;
            anim.RootModel = binAnim.Root;
            anim.AnimLength = binAnim.Duration;
            anim.TransitionLength = binAnim.Transition;

            foreach (EventStructure binEvent in binEvents)
            {
                MDLEvent evt = new MDLEvent();
                evt.Name = binEvent.EventName;
                evt.ActivationTime = binEvent.ActivationTime;
                anim.Events.Add(evt);
            }

            anim.Root = LoadNode(binAnim.Geometry.RootNodeOffset);

            return anim;
        }

        private MDLController LoadController(uint offset, uint dataOffset)
        {
            _reader.Seek((int)offset);
            Controller binController = new Controller().Read(_reader);

            int rowCount = binController.RowCount;
            int columnCount = binController.ColumnCount;

            _reader.Seek((int)(dataOffset + binController.KeyOffset));
            List<float> timeKeys = new List<float>();
            for (int i = 0; i < rowCount; i++)
            {
                timeKeys.Add(_reader.ReadSingle());
            }

            // Detect bezier interpolation flag (bit 4 = 0x10) in column count
            // vendor/mdlops/MDLOpsM.pm:1704-1710 - Bezier flag detection
            const int bezierFlag = 0x10;
            bool isBezier = (columnCount & bezierFlag) != 0;

            // Orientation data stored in controllers is sometimes compressed into 4 bytes
            // vendor/mdlops/MDLOpsM.pm:1714-1719 - Compressed quaternion detection
            uint dataPointer = dataOffset + binController.DataOffset;
            _reader.Seek((int)dataPointer);

            List<List<float>> data;
            if (binController.TypeId == (uint)MDLControllerType.ORIENTATION && binController.ColumnCount == 2)
            {
                data = new List<List<float>>();
                for (int i = 0; i < binController.RowCount; i++)
                {
                    uint compressed = _reader.ReadUInt32();
                    Vector4 decompressed = DecompressQuaternion(compressed);
                    data.Add(new List<float> { decompressed.X, decompressed.Y, decompressed.Z, decompressed.W });
                }
            }
            else
            {
                // vendor/mdlops/MDLOpsM.pm:1721-1726 - Bezier data reading
                // Bezier controllers store 3 floats per column: (value, in_tangent, out_tangent)
                // Non-bezier controllers store 1 float per column
                int effectiveColumns;
                if (isBezier)
                {
                    int baseColumns = columnCount - bezierFlag;
                    effectiveColumns = baseColumns * 3;
                }
                else
                {
                    effectiveColumns = columnCount;
                }

                // Ensure we have at least some columns to read
                if (effectiveColumns <= 0)
                {
                    effectiveColumns = columnCount & ~bezierFlag; // Strip bezier flag
                }

                data = new List<List<float>>();
                for (int i = 0; i < rowCount; i++)
                {
                    List<float> row = new List<float>();
                    for (int j = 0; j < effectiveColumns; j++)
                    {
                        row.Add(_reader.ReadSingle());
                    }
                    data.Add(row);
                }
            }

            MDLControllerType controllerType = (MDLControllerType)binController.TypeId;
            List<MDLControllerRow> rows = new List<MDLControllerRow>();
            for (int i = 0; i < rowCount; i++)
            {
                MDLControllerRow row = new MDLControllerRow();
                row.Time = timeKeys[i];
                row.Data = data[i];
                rows.Add(row);
            }
            // vendor/mdlops/MDLOpsM.pm:1709 - Store bezier flag with controller
            MDLController controller = new MDLController();
            controller.ControllerType = controllerType;
            controller.Rows = rows;
            controller.IsBezier = isBezier;
            return controller;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:1294-1342
        // Original: def _decompress_quaternion(compressed: int) -> Vector4:
        private static Vector4 DecompressQuaternion(uint compressed)
        {
            // Extract components from packed integer (kotorblender:855-858)
            // X component: bits 0-10 (11 bits, mask 0x7FF = 2047)
            float x = ((compressed & 0x7FF) / 1023.0f) - 1.0f;

            // Y component: bits 11-21 (11 bits, shift 11 then mask 0x7FF)
            float y = (((compressed >> 11) & 0x7FF) / 1023.0f) - 1.0f;

            // Z component: bits 22-31 (10 bits, shift 22, max value 1023)
            float z = ((compressed >> 22) / 511.0f) - 1.0f;

            // Calculate W from quaternion unit constraint (kotorblender:859-863)
            float mag2 = x * x + y * y + z * z;
            float w;
            if (mag2 < 1.0f)
            {
                w = (float)Math.Sqrt(1.0f - mag2);
            }
            else
            {
                w = 0.0f;
            }

            return new Vector4(x, y, z, w);
        }

        public void Dispose()
        {
            _reader?.Dispose();
            _readerExt?.Dispose();
        }
    }
}
