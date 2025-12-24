using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;

namespace Andastra.Parsing.Formats.MDL
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:1901-2376
    // Original: class MDLBinaryWriter
    // Full comprehensive implementation with 1:1 parity to PyKotor reference
    public class MDLBinaryWriter : IDisposable
    {
        private readonly MDLData.MDL _mdl;
        private readonly RawBinaryWriter _writer;
        private readonly RawBinaryWriter _mdxWriter;
        private readonly BioWareGame _game;

        // MDX data flags (matching PyKotor _MDXDataFlags)
        private static class MDXDataFlags
        {
            public const uint VERTEX = 0x0001;
            public const uint TEXTURE1 = 0x0002;
            public const uint TEXTURE2 = 0x0004;
            public const uint NORMAL = 0x0020;
            public const uint BUMPMAP = 0x0080;
        }

        // Internal state for writing
        private List<string> _names;
        private List<int> _nameOffsets;
        private List<int> _animOffsets;
        private List<int> _nodeOffsets;
        private List<MDLData.MDLNode> _mdlNodes;
        private List<_Node> _binNodes;
        private List<_Animation> _binAnims;
        private ModelHeader _fileHeader;

        // Internal binary structure classes (matching PyKotor _ModelHeader, _GeometryHeader, etc.)
        private class ModelHeader
        {
            public const int SIZE = 196;
            public GeometryHeader Geometry;
            public byte ModelType;
            public byte Unknown0;
            public byte Padding0;
            public byte Fog;
            public uint Unknown1;
            public uint OffsetToAnimations;
            public uint AnimationCount;
            public uint AnimationCount2;
            public uint Unknown2;
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

            public void Write(RawBinaryWriter writer)
            {
                Geometry.Write(writer);
                writer.WriteUInt8(ModelType);
                writer.WriteUInt8(Unknown0);
                writer.WriteUInt8(Padding0);
                writer.WriteUInt8(Fog);
                writer.WriteUInt32(Unknown1);
                writer.WriteUInt32(OffsetToAnimations);
                writer.WriteUInt32(AnimationCount);
                writer.WriteUInt32(AnimationCount2);
                writer.WriteUInt32(Unknown2);
                writer.WriteVector3(BoundingBoxMin);
                writer.WriteVector3(BoundingBoxMax);
                writer.WriteSingle(Radius);
                writer.WriteSingle(AnimScale);
                writer.WriteString(Supermodel, "ascii", 0, 32, '\0', false);
                writer.WriteUInt32(OffsetToSuperRoot);
                writer.WriteUInt32(Unknown3);
                writer.WriteUInt32(MdxSize);
                writer.WriteUInt32(MdxOffset);
                writer.WriteUInt32(OffsetToNameOffsets);
                writer.WriteUInt32(NameOffsetsCount);
                writer.WriteUInt32(NameOffsetsCount2);
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
            public int GeometryType;
            public string ModelName;
            public uint RootNodeOffset;
            public uint NodeCount;

            public void Write(RawBinaryWriter writer)
            {
                writer.WriteUInt32(FunctionPointer0);
                writer.WriteUInt32(FunctionPointer1);
                writer.WriteInt32(GeometryType);
                writer.WriteString(ModelName, "ascii", 0, 32, '\0', false);
                writer.WriteUInt32(RootNodeOffset);
                writer.WriteUInt32(NodeCount);
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

            public void Write(RawBinaryWriter writer)
            {
                writer.WriteUInt16(TypeId);
                writer.WriteUInt16(NodeId);
                writer.WriteUInt16(NameId);
                writer.WriteUInt16(Padding0);
                writer.WriteUInt32(OffsetToRoot);
                writer.WriteUInt32(OffsetToParent);
                writer.WriteVector3(Position);
                writer.WriteVector4(Orientation);
                writer.WriteUInt32(OffsetToChildren);
                writer.WriteUInt32(ChildrenCount);
                writer.WriteUInt32(ChildrenCount2);
                writer.WriteUInt32(OffsetToControllers);
                writer.WriteUInt32(ControllerCount);
                writer.WriteUInt32(ControllerCount2);
                writer.WriteUInt32(OffsetToControllerData);
                writer.WriteUInt32(ControllerDataLength);
                writer.WriteUInt32(ControllerDataLength2);
            }
        }

        private class TrimeshHeader
        {
            public const int K1_SIZE = 304;
            public const int K2_SIZE = 320;
            public const uint K1_FUNCTION_POINTER0 = 4273776;
            public const uint K2_FUNCTION_POINTER0 = 4285200;
            public const uint K1_SKIN_FUNCTION_POINTER0 = 4273776;
            public const uint K2_SKIN_FUNCTION_POINTER0 = 4285200;
            public const uint K1_DANGLY_FUNCTION_POINTER0 = 4273776;
            public const uint K2_DANGLY_FUNCTION_POINTER0 = 4285200;
            public const uint K1_FUNCTION_POINTER1 = 4216096;
            public const uint K2_FUNCTION_POINTER1 = 4216320;
            public const uint K1_SKIN_FUNCTION_POINTER1 = 4216096;
            public const uint K2_SKIN_FUNCTION_POINTER1 = 4216320;
            public const uint K1_DANGLY_FUNCTION_POINTER1 = 4216096;
            public const uint K2_DANGLY_FUNCTION_POINTER1 = 4216320;

            public uint FunctionPointer0;
            public uint FunctionPointer1;
            public Vector3 Average;
            public float Radius;
            public Vector3 BoundingBoxMax;
            public Vector3 BoundingBoxMin;
            public float TotalArea;
            public string Texture1;
            public string Texture2;
            public Vector3 Diffuse;
            public Vector3 Ambient;
            public uint TransparencyHint;
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
            public float TotalArea2;
            public uint Unknown11;
            public uint Unknown12;
            public uint Unknown13;
            public uint MdxDataOffset;
            public uint VerticesOffset;

            public List<uint> IndicesCounts;
            public List<uint> IndicesOffsets;
            public List<uint> InvertedCounters;
            public List<_Face> Faces;
            public List<Vector3> Vertices;

            public TrimeshHeader()
            {
                Unknown0 = new byte[4];
                Unknown1 = new byte[4];
                SaberUnknowns = new byte[8];
                IndicesCounts = new List<uint>();
                IndicesOffsets = new List<uint>();
                InvertedCounters = new List<uint>();
                Faces = new List<_Face>();
                Vertices = new List<Vector3>();
            }

            public int HeaderSize(bool isK2)
            {
                return isK2 ? K2_SIZE : K1_SIZE;
            }

            public int FacesSize()
            {
                return Faces.Count * _Face.SIZE;
            }

            public int VerticesSize()
            {
                return Vertices.Count * 12; // 3 floats * 4 bytes per Vector3
            }

            public void Write(RawBinaryWriter writer, bool isK2)
            {
                writer.WriteUInt32(FunctionPointer0);
                writer.WriteUInt32(FunctionPointer1);
                writer.WriteVector3(Average);
                writer.WriteSingle(Radius);
                writer.WriteVector3(BoundingBoxMax);
                writer.WriteVector3(BoundingBoxMin);
                writer.WriteSingle(TotalArea);
                writer.WriteString(Texture1, "ascii", 0, 32, '\0', false);
                writer.WriteString(Texture2, "ascii", 0, 32, '\0', false);
                writer.WriteVector3(Diffuse);
                writer.WriteVector3(Ambient);
                writer.WriteUInt32(TransparencyHint);
                writer.WriteBytes(Unknown0);
                writer.WriteUInt32(OffsetToIndicesCounts);
                writer.WriteUInt32(IndicesCountsCount);
                writer.WriteUInt32(IndicesCountsCount2);
                writer.WriteUInt32(OffsetToIndicesOffset);
                writer.WriteUInt32(IndicesOffsetsCount);
                writer.WriteUInt32(IndicesOffsetsCount2);
                writer.WriteUInt32(OffsetToCounters);
                writer.WriteUInt32(CountersCount);
                writer.WriteUInt32(CountersCount2);
                writer.WriteBytes(Unknown1);
                writer.WriteBytes(SaberUnknowns);
                writer.WriteUInt32(Unknown2);
                writer.WriteVector2(UvDirection);
                writer.WriteSingle(UvJitter);
                writer.WriteSingle(UvSpeed);
                writer.WriteUInt32(MdxDataSize);
                writer.WriteUInt32(MdxDataBitmap);
                writer.WriteUInt32(MdxVertexOffset);
                writer.WriteUInt32(MdxNormalOffset);
                writer.WriteUInt32(MdxColorOffset);
                writer.WriteUInt32(MdxTexture1Offset);
                writer.WriteUInt32(MdxTexture2Offset);
                writer.WriteUInt32(Unknown3);
                writer.WriteUInt32(Unknown4);
                writer.WriteUInt32(Unknown5);
                writer.WriteUInt32(Unknown6);
                writer.WriteUInt32(Unknown7);
                writer.WriteUInt32(Unknown8);
                writer.WriteUInt16(VertexCount);
                writer.WriteUInt16(TextureCount);
                writer.WriteUInt8(HasLightmap);
                writer.WriteUInt8(RotateTexture);
                writer.WriteUInt8(Background);
                writer.WriteUInt8(HasShadow);
                writer.WriteUInt8(Beaming);
                writer.WriteUInt8(Render);
                writer.WriteUInt8(Unknown9);
                writer.WriteUInt8(Unknown10);
                writer.WriteSingle(TotalArea2);
                writer.WriteUInt32(Unknown11);
                if (isK2)
                {
                    writer.WriteUInt32(Unknown12);
                    writer.WriteUInt32(Unknown13);
                }
                writer.WriteUInt32(MdxDataOffset);
                writer.WriteUInt32(VerticesOffset);
            }
        }

        private class _Face
        {
            public const int SIZE = 56;
            public ushort Vertex1;
            public ushort Vertex2;
            public ushort Vertex3;
            public ushort Adjacent1;
            public ushort Adjacent2;
            public ushort Adjacent3;
            public ushort Material;
            public float Coefficient;
            public Vector3 Normal;

            public void Write(RawBinaryWriter writer)
            {
                writer.WriteUInt16(Vertex1);
                writer.WriteUInt16(Vertex2);
                writer.WriteUInt16(Vertex3);
                writer.WriteUInt16(Adjacent1);
                writer.WriteUInt16(Adjacent2);
                writer.WriteUInt16(Adjacent3);
                writer.WriteUInt16(Material);
                writer.WriteSingle(Coefficient);
                writer.WriteVector3(Normal);
            }
        }

        private class _Controller
        {
            public const int SIZE = 24;
            public ushort TypeId;
            public ushort Unknown0;
            public uint RowCount;
            public uint ColumnCount;
            public uint TimeOffset;
            public uint DataOffset;

            public void Write(RawBinaryWriter writer)
            {
                writer.WriteUInt16(TypeId);
                writer.WriteUInt16(Unknown0);
                writer.WriteUInt32(RowCount);
                writer.WriteUInt32(ColumnCount);
                writer.WriteUInt32(TimeOffset);
                writer.WriteUInt32(DataOffset);
            }
        }

        private class _Node
        {
            public NodeHeader Header;
            public TrimeshHeader Trimesh;
            public List<int> ChildrenOffsets;
            public List<_Controller> Controllers;
            public List<float> ControllerData;

            public _Node()
            {
                Header = new NodeHeader();
                ChildrenOffsets = new List<int>();
                Controllers = new List<_Controller>();
                ControllerData = new List<float>();
            }

            public int AllHeadersSize(bool isK2)
            {
                int size = NodeHeader.SIZE;
                if (Trimesh != null)
                {
                    size += Trimesh.HeaderSize(isK2);
                }
                return size;
            }

            public int IndicesCountsOffset(bool isK2)
            {
                return AllHeadersSize(isK2);
            }

            public int IndicesOffsetsOffset(bool isK2)
            {
                int offset = IndicesCountsOffset(isK2);
                if (Trimesh != null)
                {
                    offset += Trimesh.IndicesCounts.Count * 4;
                }
                return offset;
            }

            public int InvertedCountersOffset(bool isK2)
            {
                int offset = IndicesOffsetsOffset(isK2);
                if (Trimesh != null)
                {
                    offset += Trimesh.IndicesOffsets.Count * 4;
                }
                return offset;
            }

            public int IndicesOffset(bool isK2)
            {
                int offset = InvertedCountersOffset(isK2);
                if (Trimesh != null)
                {
                    offset += Trimesh.InvertedCounters.Count * 4;
                }
                return offset;
            }

            public int VerticesOffset(bool isK2)
            {
                int offset = IndicesOffset(isK2);
                if (Trimesh != null)
                {
                    offset += Trimesh.Faces.Count * 3 * 2; // Each face has 3 vertices, each vertex index is 2 bytes (ushort)
                }
                return offset;
            }

            public int FacesOffset(bool isK2)
            {
                int size = VerticesOffset(isK2);
                if (Trimesh != null)
                {
                    size += Trimesh.VerticesSize();
                }
                return size;
            }

            public int ChildrenOffsetsOffset(bool isK2)
            {
                int size = FacesOffset(isK2);
                if (Trimesh != null)
                {
                    size += Trimesh.FacesSize();
                }
                return size;
            }

            public int ChildrenOffsetsSize()
            {
                return 4 * (int)Header.ChildrenCount;
            }

            public int ControllersOffset(bool isK2)
            {
                return ChildrenOffsetsOffset(isK2) + ChildrenOffsetsSize();
            }

            public int ControllersSize()
            {
                return _Controller.SIZE * Controllers.Count;
            }

            public int ControllerDataOffset(bool isK2)
            {
                return ControllersOffset(isK2) + ControllersSize();
            }

            public int ControllerDataSize()
            {
                return ControllerData.Count * 4;
            }

            public int CalcSize(bool isK2)
            {
                return ControllerDataOffset(isK2) + ControllerDataSize();
            }

            public void Write(RawBinaryWriter writer, bool isK2)
            {
                Header.Write(writer);
                if (Trimesh != null)
                {
                    Trimesh.Write(writer, isK2);
                    // Write faces immediately after TrimeshHeader
                    foreach (var face in Trimesh.Faces)
                    {
                        face.Write(writer);
                    }
                }
                foreach (var childOffset in ChildrenOffsets)
                {
                    writer.WriteUInt32((uint)childOffset);
                }
                foreach (var controller in Controllers)
                {
                    controller.Write(writer);
                }
                foreach (var data in ControllerData)
                {
                    writer.WriteSingle(data);
                }
                // Write trimesh data (IndicesCounts, IndicesOffsets, Counters, Vertices) after ControllerData
                if (Trimesh != null)
                {
                    WriteTrimeshData(writer);
                }
            }

            private void WriteTrimeshData(RawBinaryWriter writer)
            {
                if (Trimesh == null)
                {
                    return;
                }

                // Write indices counts array
                foreach (var count in Trimesh.IndicesCounts)
                {
                    writer.WriteUInt32(count);
                }

                // Write indices offsets array
                foreach (var offset in Trimesh.IndicesOffsets)
                {
                    writer.WriteUInt32(offset);
                }

                // Write inverted counters array
                foreach (var counter in Trimesh.InvertedCounters)
                {
                    writer.WriteUInt32(counter);
                }

                // Write face indices (ushort per index, 3 indices per face)
                foreach (var face in Trimesh.Faces)
                {
                    writer.WriteUInt16(face.Vertex1);
                    writer.WriteUInt16(face.Vertex2);
                    writer.WriteUInt16(face.Vertex3);
                }

                // Write vertices (Vector3 per vertex)
                foreach (var vertex in Trimesh.Vertices)
                {
                    writer.WriteVector3(vertex);
                }

                // Write faces (full _Face structure)
                foreach (var face in Trimesh.Faces)
                {
                    face.Write(writer);
                }
            }
        }

        private class AnimationHeader
        {
            public const int SIZE = 136; // GeometryHeader.SIZE (80) + 56 bytes animation fields
            public GeometryHeader Geometry;
            public float Duration;
            public float Transition;
            public string Root;
            public uint OffsetToEvents;
            public uint EventCount;
            public uint EventCount2;
            public uint Unknown0;

            public AnimationHeader()
            {
                Geometry = new GeometryHeader();
                Duration = 0.0f;
                Transition = 0.0f;
                Root = "";
                OffsetToEvents = 0;
                EventCount = 0;
                EventCount2 = 0;
                Unknown0 = 0;
            }

            public void Write(RawBinaryWriter writer)
            {
                Geometry.Write(writer);
                writer.WriteSingle(Duration);
                writer.WriteSingle(Transition);
                writer.WriteString(Root, "ascii", 0, 32, '\0', false);
                writer.WriteUInt32(OffsetToEvents);
                writer.WriteUInt32(EventCount);
                writer.WriteUInt32(EventCount2);
                writer.WriteUInt32(Unknown0);
            }
        }

        private class _EventStructure
        {
            public const int SIZE = 36;
            public float ActivationTime;
            public string EventName;

            public _EventStructure()
            {
                ActivationTime = 0.0f;
                EventName = "";
            }

            public void Write(RawBinaryWriter writer)
            {
                writer.WriteSingle(ActivationTime);
                writer.WriteString(EventName, "ascii", 0, 32, '\0', false);
            }
        }

        private class _Animation
        {
            public AnimationHeader Header;
            public List<_EventStructure> Events;
            public List<_Node> Nodes;

            public _Animation()
            {
                Header = new AnimationHeader();
                Events = new List<_EventStructure>();
                Nodes = new List<_Node>();
            }

            public int EventsOffset()
            {
                return AnimationHeader.SIZE;
            }

            public int EventsSize()
            {
                return _EventStructure.SIZE * Events.Count;
            }

            public int NodesOffset()
            {
                return EventsOffset() + EventsSize();
            }

            public int Size(bool isK2)
            {
                int size = NodesOffset();
                foreach (var node in Nodes)
                {
                    size += node.CalcSize(isK2);
                }
                return size;
            }

            public void Write(RawBinaryWriter writer, bool isK2)
            {
                Header.Write(writer);
                foreach (var evt in Events)
                {
                    evt.Write(writer);
                }
                foreach (var node in Nodes)
                {
                    node.Write(writer, isK2);
                }
            }
        }

        public MDLBinaryWriter(MDLData.MDL mdl, string mdlPath, string mdxPath, BioWareGame game = BioWareGame.K1)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _writer = RawBinaryWriter.ToFile(mdlPath);
            _mdxWriter = RawBinaryWriter.ToFile(mdxPath);
            _game = game;
        }

        public MDLBinaryWriter(MDLData.MDL mdl, object mdlTarget, object mdxTarget, BioWareGame game = BioWareGame.K1)
        {
            _mdl = mdl ?? throw new ArgumentNullException(nameof(mdl));
            _writer = RawBinaryWriter.ToAuto(mdlTarget);
            _mdxWriter = RawBinaryWriter.ToAuto(mdxTarget);
            _game = game;
        }

        public void Write(bool autoClose = true)
        {
            try
            {
                // Initialize state
                _mdlNodes = _mdl.AllNodes();
                _binNodes = new List<_Node>();
                for (int i = 0; i < _mdlNodes.Count; i++)
                {
                    _binNodes.Add(new _Node());
                }
                _binAnims = new List<_Animation>();
                for (int i = 0; i < _mdl.Anims.Count; i++)
                {
                    _binAnims.Add(new _Animation());
                }
                _names = _mdlNodes.Select(n => n.Name).ToList();
                _nameOffsets = new List<int>();
                _animOffsets = new List<int>();
                for (int i = 0; i < _binAnims.Count; i++)
                {
                    _animOffsets.Add(0);
                }
                _nodeOffsets = new List<int>();
                for (int i = 0; i < _binNodes.Count; i++)
                {
                    _nodeOffsets.Add(0);
                }
                _fileHeader = new ModelHeader();

                // Update all data
                _UpdateAllData();

                // Calculate offsets
                _CalcTopOffsets();
                _CalcInnerOffsets();

                // Write everything
                _WriteAll();
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        private void _UpdateAllData()
        {
            for (int i = 0; i < _binNodes.Count; i++)
            {
                _UpdateNode(_binNodes[i], _mdlNodes[i]);
            }

            for (int i = 0; i < _binAnims.Count; i++)
            {
                _UpdateAnim(_binAnims[i], _mdl.Anims[i]);
            }
        }

        private void _UpdateNode(_Node binNode, MDLData.MDLNode mdlNode)
        {
            binNode.Header.TypeId = (ushort)_NodeType(mdlNode);
            binNode.Header.Position = mdlNode.Position;
            binNode.Header.Orientation = mdlNode.Orientation;
            binNode.Header.ChildrenCount = (uint)mdlNode.Children.Count;
            binNode.Header.ChildrenCount2 = (uint)mdlNode.Children.Count;
            binNode.Header.NameId = (ushort)_names.IndexOf(mdlNode.Name);
            binNode.Header.NodeId = (ushort)_GetNodeId(binNode);

            // Determine function pointers based on game version
            bool isK2 = _game.IsK2();
            uint fp0, fp1;
            if (_game.IsK1())
            {
                if (mdlNode.Skin != null)
                {
                    fp0 = TrimeshHeader.K1_SKIN_FUNCTION_POINTER0;
                    fp1 = TrimeshHeader.K1_SKIN_FUNCTION_POINTER1;
                }
                else if (mdlNode.Dangly != null)
                {
                    fp0 = TrimeshHeader.K1_DANGLY_FUNCTION_POINTER0;
                    fp1 = TrimeshHeader.K1_DANGLY_FUNCTION_POINTER1;
                }
                else
                {
                    fp0 = TrimeshHeader.K1_FUNCTION_POINTER0;
                    fp1 = TrimeshHeader.K1_FUNCTION_POINTER1;
                }
            }
            else
            {
                if (mdlNode.Skin != null)
                {
                    fp0 = TrimeshHeader.K2_SKIN_FUNCTION_POINTER0;
                    fp1 = TrimeshHeader.K2_SKIN_FUNCTION_POINTER1;
                }
                else if (mdlNode.Dangly != null)
                {
                    fp0 = TrimeshHeader.K2_DANGLY_FUNCTION_POINTER0;
                    fp1 = TrimeshHeader.K2_DANGLY_FUNCTION_POINTER1;
                }
                else
                {
                    fp0 = TrimeshHeader.K2_FUNCTION_POINTER0;
                    fp1 = TrimeshHeader.K2_FUNCTION_POINTER1;
                }
            }

            if (mdlNode.Mesh != null)
            {
                binNode.Trimesh = new TrimeshHeader();
                binNode.Trimesh.FunctionPointer0 = fp0;
                binNode.Trimesh.FunctionPointer1 = fp1;
                binNode.Trimesh.Average = mdlNode.Mesh.AveragePoint;
                binNode.Trimesh.Radius = mdlNode.Mesh.Radius;
                binNode.Trimesh.BoundingBoxMax = mdlNode.Mesh.BbMax;
                binNode.Trimesh.BoundingBoxMin = mdlNode.Mesh.BbMin;
                binNode.Trimesh.TotalArea = mdlNode.Mesh.Area;
                binNode.Trimesh.Texture1 = mdlNode.Mesh.Texture1 ?? "NULL";
                binNode.Trimesh.Texture2 = mdlNode.Mesh.Texture2 ?? "NULL";
                binNode.Trimesh.BoundingBoxMax = mdlNode.Mesh.BbMax;
                binNode.Trimesh.BoundingBoxMin = mdlNode.Mesh.BbMin;
                // Convert RGB to BGR for diffuse and ambient (BGR is Z, Y, X)
                Vector3 diffuse = mdlNode.Mesh.Diffuse;
                binNode.Trimesh.Diffuse = new Vector3(diffuse.Z, diffuse.Y, diffuse.X);
                Vector3 ambient = mdlNode.Mesh.Ambient;
                binNode.Trimesh.Ambient = new Vector3(ambient.Z, ambient.Y, ambient.X);
                binNode.Trimesh.TransparencyHint = (uint)mdlNode.Mesh.TransparencyHint;

                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2019-2033
                // Set rendering and material properties
                binNode.Trimesh.Render = (byte)(mdlNode.Mesh.Render != 0 ? 1 : 0);
                binNode.Trimesh.Beaming = (byte)(mdlNode.Mesh.Beaming != 0 ? 1 : 0);
                binNode.Trimesh.HasShadow = (byte)(mdlNode.Mesh.Shadow != 0.0f ? 1 : 0);
                binNode.Trimesh.HasLightmap = (byte)(mdlNode.Mesh.HasLightmap ? 1 : 0);
                // Rotate texture flag (matching PyKotor io_mdl.py:2026)
                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2026
                binNode.Trimesh.RotateTexture = mdlNode.Mesh.RotateTexture ? (byte)1 : (byte)0;

                // Background geometry flag (matching PyKotor io_mdl.py:2027)
                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2027
                binNode.Trimesh.Background = mdlNode.Mesh.BackgroundGeometry ? (byte)1 : (byte)0;

                // UV animation properties (matching PyKotor io_mdl.py:2021-2024)
                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2021-2024
                binNode.Trimesh.UvJitter = mdlNode.Mesh.UvJitter;
                binNode.Trimesh.UvSpeed = mdlNode.Mesh.UvJitterSpeed;
                binNode.Trimesh.UvDirection = new Vector2(mdlNode.Mesh.UvDirectionX, mdlNode.Mesh.UvDirectionY);

                // Texture count: 2 if lightmap (texture2) is present, otherwise 1
                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2035
                // texture_count = 1 (always has texture1), +1 if has_lightmap (texture2)
                binNode.Trimesh.TextureCount = (ushort)(mdlNode.Mesh.HasLightmap ? 2 : 1);

                // TotalArea2: duplicate of TotalArea for compatibility
                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2030
                binNode.Trimesh.TotalArea2 = mdlNode.Mesh.Area;

                // Saber unknowns (matching PyKotor io_mdl.py:2033)
                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2033
                // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:1223 (default: (3, 0, 0, 0, 0, 0, 0, 0))
                if (mdlNode.Mesh.SaberUnknowns != null && mdlNode.Mesh.SaberUnknowns.Length == 8)
                {
                    binNode.Trimesh.SaberUnknowns = mdlNode.Mesh.SaberUnknowns;
                }
                else
                {
                    // Default: (3, 0, 0, 0, 0, 0, 0, 0) as per PyKotor mdl_data.py:1223
                    binNode.Trimesh.SaberUnknowns = new byte[] { 3, 0, 0, 0, 0, 0, 0, 0 };
                }
                if (binNode.Trimesh.SaberUnknowns == null || binNode.Trimesh.SaberUnknowns.Length != 8)
                {
                    binNode.Trimesh.SaberUnknowns = new byte[] { 3, 0, 0, 0, 0, 0, 0, 0 };
                }

                binNode.Trimesh.VertexCount = (ushort)mdlNode.Mesh.Vertices.Count;
                binNode.Trimesh.Vertices.Clear();
                binNode.Trimesh.Vertices.AddRange(mdlNode.Mesh.Vertices);
                binNode.Trimesh.Faces.Clear();
                foreach (var face in mdlNode.Mesh.Faces)
                {
                    var binFace = new _Face();
                    binFace.Vertex1 = (ushort)face.V1;
                    binFace.Vertex2 = (ushort)face.V2;
                    binFace.Vertex3 = (ushort)face.V3;
                    binFace.Adjacent1 = (ushort)face.A1;
                    binFace.Adjacent2 = (ushort)face.A2;
                    binFace.Adjacent3 = (ushort)face.A3;
                    binFace.Material = (ushort)face.Material;
                    binFace.Coefficient = face.Coefficient;
                    binFace.Normal = face.Normal;
                    binNode.Trimesh.Faces.Add(binFace);
                }
                binNode.Trimesh.IndicesCounts.Clear();
                binNode.Trimesh.IndicesCounts.Add((uint)(mdlNode.Mesh.Faces.Count * 3));
                binNode.Trimesh.IndicesCountsCount = 1;
                binNode.Trimesh.IndicesCountsCount2 = 1;
                binNode.Trimesh.IndicesOffsets.Clear();
                binNode.Trimesh.IndicesOffsets.Add(0); // Placeholder
                binNode.Trimesh.IndicesOffsetsCount = 1;
                binNode.Trimesh.IndicesOffsetsCount2 = 1;
                binNode.Trimesh.InvertedCounters.Clear();
                binNode.Trimesh.InvertedCounters.Add(0);
                binNode.Trimesh.CountersCount = 1;
                binNode.Trimesh.CountersCount2 = 1;
            }

            // Update controllers
            int dataOffset = 0;
            int keyOffset = 0;
            binNode.Controllers.Clear();
            binNode.ControllerData.Clear();
            foreach (var mdlController in mdlNode.Controllers)
            {
                var binController = new _Controller();
                binController.TypeId = (ushort)mdlController.ControllerType;
                binController.RowCount = (uint)mdlController.Rows.Count;
                if (mdlController.Rows.Count > 0)
                {
                    binController.ColumnCount = (uint)mdlController.Rows[0].Data.Count;
                }
                binController.TimeOffset = (uint)keyOffset;
                dataOffset += mdlController.Rows.Count;
                binController.DataOffset = (uint)dataOffset;
                binNode.Controllers.Add(binController);
                dataOffset += mdlController.Rows.Count * (mdlController.Rows.Count > 0 ? mdlController.Rows[0].Data.Count : 0);
                keyOffset += dataOffset;
            }

            foreach (var controller in mdlNode.Controllers)
            {
                foreach (var row in controller.Rows)
                {
                    binNode.ControllerData.Add(row.Time);
                }
                foreach (var row in controller.Rows)
                {
                    binNode.ControllerData.AddRange(row.Data);
                }
            }

            binNode.Header.ControllerCount = (uint)binNode.Controllers.Count;
            binNode.Header.ControllerCount2 = (uint)binNode.Controllers.Count;
            binNode.Header.ControllerDataLength = (uint)binNode.ControllerData.Count;
            binNode.Header.ControllerDataLength2 = (uint)binNode.ControllerData.Count;
        }

        private void _UpdateAnim(_Animation binAnim, MDLData.MDLAnimation mdlAnim)
        {
            if (_game.IsK1())
            {
                binAnim.Header.Geometry.FunctionPointer0 = GeometryHeader.K1_ANIM_FUNCTION_POINTER0;
                binAnim.Header.Geometry.FunctionPointer1 = GeometryHeader.K1_ANIM_FUNCTION_POINTER1;
            }
            else
            {
                binAnim.Header.Geometry.FunctionPointer0 = GeometryHeader.K2_ANIM_FUNCTION_POINTER0;
                binAnim.Header.Geometry.FunctionPointer1 = GeometryHeader.K2_ANIM_FUNCTION_POINTER1;
            }
            binAnim.Header.Geometry.GeometryType = GeometryHeader.GEOM_TYPE_ANIM;
            binAnim.Header.Geometry.ModelName = mdlAnim.Name;
            binAnim.Header.Geometry.NodeCount = 0;
            binAnim.Header.Duration = mdlAnim.AnimLength;
            binAnim.Header.Transition = mdlAnim.TransitionLength;
            binAnim.Header.Root = mdlAnim.RootModel ?? "";
            binAnim.Header.EventCount = (uint)mdlAnim.Events.Count;
            binAnim.Header.EventCount2 = (uint)mdlAnim.Events.Count;

            // Create event structures from MDL events
            binAnim.Events.Clear();
            foreach (var mdlEvent in mdlAnim.Events)
            {
                var binEvent = new _EventStructure();
                binEvent.ActivationTime = mdlEvent.ActivationTime;
                binEvent.EventName = mdlEvent.Name ?? "";
                binAnim.Events.Add(binEvent);
            }

            // Create binary nodes from animation nodes
            List<MDLData.MDLNode> allAnimNodes = mdlAnim.AllNodes();
            binAnim.Nodes.Clear();
            foreach (var mdlNode in allAnimNodes)
            {
                var binNode = new _Node();
                _UpdateNode(binNode, mdlNode);
                binAnim.Nodes.Add(binNode);
            }
        }

        private void _CalcTopOffsets()
        {
            int offsetToNameOffsets = ModelHeader.SIZE;

            int offsetToNames = offsetToNameOffsets + 4 * _names.Count;
            int nameOffset = offsetToNames;
            _nameOffsets.Clear();
            foreach (var name in _names)
            {
                _nameOffsets.Add(nameOffset);
                nameOffset += name.Length + 1;
            }

            int offsetToAnimOffsets = nameOffset;
            int offsetToAnims = nameOffset + (4 * _binAnims.Count);
            int animOffset = offsetToAnims;
            bool isK2 = _game.IsK2();
            for (int i = 0; i < _binAnims.Count; i++)
            {
                _animOffsets[i] = animOffset;
                animOffset += _binAnims[i].Size(isK2);
            }

            int offsetToNodeOffset = animOffset;
            int nodeOffset = offsetToNodeOffset;
            for (int i = 0; i < _binNodes.Count; i++)
            {
                _nodeOffsets[i] = nodeOffset;
                nodeOffset += _binNodes[i].CalcSize(isK2);
            }

            _fileHeader.Geometry.RootNodeOffset = (uint)offsetToNodeOffset;
            _fileHeader.OffsetToNameOffsets = (uint)offsetToNameOffsets;
            _fileHeader.OffsetToSuperRoot = 0;
            _fileHeader.OffsetToAnimations = (uint)offsetToAnimOffsets;
        }

        private void _CalcInnerOffsets()
        {
            bool isK2 = _game.IsK2();
            for (int i = 0; i < _binAnims.Count; i++)
            {
                // Set event offset in animation header
                if (_binAnims[i].Events.Count > 0)
                {
                    _binAnims[i].Header.OffsetToEvents = (uint)(_animOffsets[i] + _binAnims[i].EventsOffset());
                }
                else
                {
                    _binAnims[i].Header.OffsetToEvents = 0;
                }

                // Set root node offset in animation header geometry
                _binAnims[i].Header.Geometry.RootNodeOffset = (uint)(_animOffsets[i] + _binAnims[i].NodesOffset());

                List<int> nodeOffsets = new List<int>();
                int nodeOffset = _animOffsets[i] + _binAnims[i].NodesOffset();
                foreach (var binNode in _binAnims[i].Nodes)
                {
                    nodeOffsets.Add(nodeOffset);
                    nodeOffset += binNode.CalcSize(isK2);
                }

                for (int j = 0; j < _binAnims[i].Nodes.Count; j++)
                {
                    _CalcNodeOffset(j, _binAnims[i].Nodes, nodeOffsets, isK2);
                }
            }

            for (int i = 0; i < _binNodes.Count; i++)
            {
                _CalcNodeOffset(i, _binNodes, _nodeOffsets, isK2);
            }
        }

        private void _CalcNodeOffset(int index, List<_Node> binNodes, List<int> binOffsets, bool isK2)
        {
            _Node binNode = binNodes[index];
            int nodeOffset = binOffsets[index];

            foreach (var binChild in _GetBinChildren(binNode, binNodes))
            {
                int childIndex = binNodes.IndexOf(binChild);
                int offset = binOffsets[childIndex];
                binNode.ChildrenOffsets.Add(offset);
            }

            binNode.Header.OffsetToChildren = (uint)(nodeOffset + binNode.ChildrenOffsetsOffset(isK2));
            binNode.Header.OffsetToControllers = (uint)(nodeOffset + binNode.ControllersOffset(isK2));
            binNode.Header.OffsetToControllerData = (uint)(nodeOffset + binNode.ControllerDataOffset(isK2));
            binNode.Header.OffsetToRoot = 0;
            binNode.Header.OffsetToParent = index != 0 ? (uint)_nodeOffsets[0] : 0;

            if (binNode.Trimesh != null)
            {
                _CalcTrimeshOffsets(nodeOffset, binNode, isK2);
            }
        }

        private void _CalcTrimeshOffsets(int nodeOffset, _Node binNode, bool isK2)
        {
            // Calculate offsets for trimesh data structures
            // These offsets are relative to the start of the node, matching PyKotor reference
            // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2250-2262
            binNode.Trimesh.OffsetToCounters = (uint)(nodeOffset + binNode.InvertedCountersOffset(isK2));
            binNode.Trimesh.OffsetToIndicesCounts = (uint)(nodeOffset + binNode.IndicesCountsOffset(isK2));
            binNode.Trimesh.OffsetToIndicesOffset = (uint)(nodeOffset + binNode.IndicesOffsetsOffset(isK2));

            // Update indices_offsets array with actual indices offset
            binNode.Trimesh.IndicesOffsets.Clear();
            binNode.Trimesh.IndicesOffsets.Add((uint)(nodeOffset + binNode.IndicesOffset(isK2)));

            binNode.Trimesh.VerticesOffset = (uint)(nodeOffset + binNode.VerticesOffset(isK2));
        }

        private int _GetNodeId(_Node binNode)
        {
            int nameIndex = binNode.Header.NameId;
            for (int i = 0; i < _mdlNodes.Count; i++)
            {
                if (_names.IndexOf(_mdlNodes[i].Name) == nameIndex)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Node ID not found");
        }

        private List<_Node> _GetBinChildren(_Node binNode, List<_Node> allNodes)
        {
            int nameId = binNode.Header.NameId;
            MDLData.MDLNode mdlNode = null;
            for (int i = 0; i < _mdlNodes.Count; i++)
            {
                if (_names.IndexOf(_mdlNodes[i].Name) == nameId)
                {
                    mdlNode = _mdlNodes[i];
                    break;
                }
            }
            if (mdlNode == null)
            {
                throw new InvalidOperationException("MDL node not found");
            }

            List<int> childNameIds = new List<int>();
            foreach (var child in mdlNode.Children)
            {
                int childNameId = _names.IndexOf(child.Name);
                childNameIds.Add(childNameId);
            }

            List<_Node> binChildren = new List<_Node>();
            foreach (var childNameId in childNameIds)
            {
                foreach (var bn in allNodes)
                {
                    if (bn.Header.NameId == childNameId)
                    {
                        binChildren.Add(bn);
                    }
                }
            }
            return binChildren;
        }

        private int _NodeType(MDLData.MDLNode node)
        {
            // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_types.py:41-69
            // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/mdl_data.py:647-666 (get_flags method)
            // Reference: vendor/mdlops/MDLOpsM.pm:301-311 (Node type quick reference)
            // Reference: vendor/kotorblender/io_scene_kotor/format/mdl/types.py:93-101 (Node flags)
            // Matching PyKotor MDLNode.get_flags() implementation for 1:1 parity
            // Base node always has HEADER flag (0x0001)
            int typeId = (int)MDLNodeFlags.HEADER;

            // LIGHT flag (0x0002) - Light source data
            // Reference: vendor/PyKotor:mdl_data.py:650-651, vendor/mdlops/MDLOpsM.pm:303
            // Note: LIGHT does not require MESH (light = HEADER + LIGHT = 0x003 = 3)
            if (node.Light != null)
            {
                typeId |= (int)MDLNodeFlags.LIGHT;
            }

            // EMITTER flag (0x0004) - Particle emitter data
            // Reference: vendor/PyKotor:mdl_data.py:652-653, vendor/mdlops/MDLOpsM.pm:304
            // Note: EMITTER does not require MESH (emitter = HEADER + EMITTER = 0x005 = 5)
            if (node.Emitter != null)
            {
                typeId |= (int)MDLNodeFlags.EMITTER;
            }

            // REFERENCE flag (0x0010) - Reference to another model
            // Reference: vendor/PyKotor:mdl_data.py:654-655, vendor/mdlops/MDLOpsM.pm:305
            // Note: REFERENCE does not require MESH (reference = HEADER + REFERENCE = 0x011 = 17)
            if (node.Reference != null)
            {
                typeId |= (int)MDLNodeFlags.REFERENCE;
            }

            // MESH flag (0x0020) - Triangle mesh geometry
            // Reference: vendor/PyKotor:mdl_data.py:656-657, vendor/mdlops/MDLOpsM.pm:306
            if (node.Mesh != null)
            {
                typeId |= (int)MDLNodeFlags.MESH;
            }

            // SKIN flag (0x0040) - Skinned mesh with bone weighting
            // Reference: vendor/PyKotor:mdl_data.py:658-659, vendor/mdlops/MDLOpsM.pm:307
            // Note: SKIN requires MESH flag to be set (skin mesh = HEADER + MESH + SKIN = 0x061 = 97)
            // Check both node.Skin (direct property matching PyKotor) and node.Mesh.Skin (mesh property for compatibility)
            if (node.Skin != null || (node.Mesh != null && node.Mesh.Skin != null))
            {
                typeId |= (int)MDLNodeFlags.SKIN;
                // Ensure MESH flag is set when SKIN is present (SKIN always implies MESH)
                if ((typeId & (int)MDLNodeFlags.MESH) == 0)
                {
                    typeId |= (int)MDLNodeFlags.MESH;
                }
            }

            // DANGLY flag (0x0100) - Cloth/hair physics mesh with constraints
            // Reference: vendor/PyKotor:mdl_data.py:660-661, vendor/mdlops/MDLOpsM.pm:309
            // Note: DANGLY requires MESH flag to be set (dangly mesh = HEADER + MESH + DANGLY = 0x121 = 289)
            // Check both node.Dangly (direct property matching PyKotor) and node.Mesh.Dangly (mesh property for compatibility)
            if (node.Dangly != null || (node.Mesh != null && node.Mesh.Dangly != null))
            {
                typeId |= (int)MDLNodeFlags.DANGLY;
                // Ensure MESH flag is set when DANGLY is present (DANGLY always implies MESH)
                if ((typeId & (int)MDLNodeFlags.MESH) == 0)
                {
                    typeId |= (int)MDLNodeFlags.MESH;
                }
            }

            // AABB flag (0x0200) - Axis-aligned bounding box tree for walkmesh collision
            // Reference: vendor/PyKotor:mdl_data.py:662-663, vendor/mdlops/MDLOpsM.pm:310
            // Note: AABB requires MESH flag to be set (aabb mesh = HEADER + MESH + AABB = 0x221 = 545)
            if (node.Aabb != null)
            {
                typeId |= (int)MDLNodeFlags.AABB;
                // Ensure MESH flag is set when AABB is present (AABB always implies MESH)
                if ((typeId & (int)MDLNodeFlags.MESH) == 0)
                {
                    typeId |= (int)MDLNodeFlags.MESH;
                }
            }

            // SABER flag (0x0800) - Lightsaber blade mesh with special rendering
            // Reference: vendor/PyKotor:mdl_data.py:664-665, vendor/mdlops/MDLOpsM.pm:311
            // Note: SABER requires MESH flag to be set (saber mesh = HEADER + MESH + SABER = 0x821 = 2081)
            // Check both node.Saber (direct property matching PyKotor) and node.Mesh.Saber (mesh property for compatibility)
            if (node.Saber != null || (node.Mesh != null && node.Mesh.Saber != null))
            {
                typeId |= (int)MDLNodeFlags.SABER;
                // Ensure MESH flag is set when SABER is present (SABER always implies MESH)
                if ((typeId & (int)MDLNodeFlags.MESH) == 0)
                {
                    typeId |= (int)MDLNodeFlags.MESH;
                }
            }

            return typeId;
        }

        private void _UpdateMdx(_Node binNode, MDLData.MDLNode mdlNode)
        {
            // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2119-2176
            // Reference: vendor/mdlops/MDLOpsM.pm:6003-8112 (MDX writing)
            // Reference: vendor/PyKotor/wiki/MDL-MDX-File-Format.md (MDX data structure)
            if (binNode.Trimesh == null || mdlNode.Mesh == null)
            {
                return;
            }

            // Set MDX data offset to current position in MDX writer
            binNode.Trimesh.MdxDataOffset = (uint)_mdxWriter.Position();

            // Initialize offsets to 0xFFFFFFFF (invalid) and bitmap to 0
            binNode.Trimesh.MdxVertexOffset = 0xFFFFFFFF;
            binNode.Trimesh.MdxNormalOffset = 0xFFFFFFFF;
            binNode.Trimesh.MdxTexture1Offset = 0xFFFFFFFF;
            binNode.Trimesh.MdxTexture2Offset = 0xFFFFFFFF;
            binNode.Trimesh.MdxColorOffset = 0xFFFFFFFF;
            binNode.Trimesh.MdxDataBitmap = 0;

            // Calculate suboffsets for each data type
            uint suboffset = 0;

            // Vertex positions (always present if mesh exists)
            if (mdlNode.Mesh.VertexPositions != null && mdlNode.Mesh.VertexPositions.Count > 0)
            {
                binNode.Trimesh.MdxVertexOffset = suboffset;
                binNode.Trimesh.MdxDataBitmap |= MDXDataFlags.VERTEX;
                suboffset += 12; // 3 floats * 4 bytes = 12 bytes
            }

            // Vertex normals
            if (mdlNode.Mesh.VertexNormals != null && mdlNode.Mesh.VertexNormals.Count > 0)
            {
                binNode.Trimesh.MdxNormalOffset = suboffset;
                binNode.Trimesh.MdxDataBitmap |= MDXDataFlags.NORMAL;
                suboffset += 12; // 3 floats * 4 bytes = 12 bytes
            }

            // Texture coordinates 1 (UV1)
            if (mdlNode.Mesh.VertexUv1 != null && mdlNode.Mesh.VertexUv1.Count > 0)
            {
                binNode.Trimesh.MdxTexture1Offset = suboffset;
                binNode.Trimesh.MdxDataBitmap |= MDXDataFlags.TEXTURE1;
                suboffset += 8; // 2 floats * 4 bytes = 8 bytes
            }

            // Texture coordinates 2 (UV2)
            if (mdlNode.Mesh.VertexUv2 != null && mdlNode.Mesh.VertexUv2.Count > 0)
            {
                binNode.Trimesh.MdxTexture2Offset = suboffset;
                binNode.Trimesh.MdxDataBitmap |= MDXDataFlags.TEXTURE2;
                suboffset += 8; // 2 floats * 4 bytes = 8 bytes
            }

            // Handle skinning data if present
            uint skinBoneWeightsOffset = 0xFFFFFFFF;
            uint skinBoneIndicesOffset = 0xFFFFFFFF;
            if (mdlNode.Skin != null && mdlNode.Skin.VertexBones != null && mdlNode.Skin.VertexBones.Count > 0)
            {
                // Bone weights come after texture coordinates
                skinBoneWeightsOffset = suboffset;
                suboffset += 16; // 4 floats * 4 bytes = 16 bytes

                // Bone indices come after bone weights
                skinBoneIndicesOffset = suboffset;
                suboffset += 16; // 4 floats * 4 bytes = 16 bytes (stored as floats, cast to uint16 when used)
            }

            // Set the MDX data size (size of one vertex's data block)
            binNode.Trimesh.MdxDataSize = suboffset;

            // Get vertex count
            int vertexCount = mdlNode.Mesh.VertexPositions != null ? mdlNode.Mesh.VertexPositions.Count : 0;
            if (vertexCount == 0)
            {
                return;
            }

            // Write interleaved vertex data for each vertex
            for (int i = 0; i < vertexCount; i++)
            {
                // Write vertex position
                if (mdlNode.Mesh.VertexPositions != null && i < mdlNode.Mesh.VertexPositions.Count)
                {
                    _mdxWriter.WriteVector3(mdlNode.Mesh.VertexPositions[i]);
                }

                // Write vertex normal
                if (mdlNode.Mesh.VertexNormals != null && i < mdlNode.Mesh.VertexNormals.Count)
                {
                    _mdxWriter.WriteVector3(mdlNode.Mesh.VertexNormals[i]);
                }

                // Write texture coordinates 1
                if (mdlNode.Mesh.VertexUv1 != null && i < mdlNode.Mesh.VertexUv1.Count)
                {
                    _mdxWriter.WriteVector2(mdlNode.Mesh.VertexUv1[i]);
                }

                // Write texture coordinates 2
                if (mdlNode.Mesh.VertexUv2 != null && i < mdlNode.Mesh.VertexUv2.Count)
                {
                    _mdxWriter.WriteVector2(mdlNode.Mesh.VertexUv2[i]);
                }

                // Write skinning data if present
                if (mdlNode.Skin != null && mdlNode.Skin.VertexBones != null && i < mdlNode.Skin.VertexBones.Count)
                {
                    var boneVertex = mdlNode.Skin.VertexBones[i];
                    // Write bone weights (4 floats)
                    if (boneVertex.VertexWeights != null)
                    {
                        _mdxWriter.WriteSingle((float)boneVertex.VertexWeights.Item1);
                        _mdxWriter.WriteSingle((float)boneVertex.VertexWeights.Item2);
                        _mdxWriter.WriteSingle((float)boneVertex.VertexWeights.Item3);
                        _mdxWriter.WriteSingle((float)boneVertex.VertexWeights.Item4);
                    }
                    else
                    {
                        // Default weights if not present
                        _mdxWriter.WriteSingle(1.0f);
                        _mdxWriter.WriteSingle(0.0f);
                        _mdxWriter.WriteSingle(0.0f);
                        _mdxWriter.WriteSingle(0.0f);
                    }

                    // Write bone indices (4 floats, stored as floats but represent uint16 indices)
                    if (boneVertex.VertexIndices != null)
                    {
                        _mdxWriter.WriteSingle((float)boneVertex.VertexIndices.Item1);
                        _mdxWriter.WriteSingle((float)boneVertex.VertexIndices.Item2);
                        _mdxWriter.WriteSingle((float)boneVertex.VertexIndices.Item3);
                        _mdxWriter.WriteSingle((float)boneVertex.VertexIndices.Item4);
                    }
                    else
                    {
                        // Default indices if not present
                        _mdxWriter.WriteSingle(0.0f);
                        _mdxWriter.WriteSingle(0.0f);
                        _mdxWriter.WriteSingle(0.0f);
                        _mdxWriter.WriteSingle(0.0f);
                    }
                }
            }

            // Write padding/sentinel values (required by MDX format)
            // Reference: vendor/mdlops/MDLOpsM.pm:8099-8109 (padding logic)
            // Reference: vendor/PyKotor/Libraries/PyKotor/src/pykotor/resource/formats/mdl/io_mdl.py:2168-2176
            if (mdlNode.Mesh.VertexPositions != null && mdlNode.Mesh.VertexPositions.Count > 0)
            {
                // Write sentinel vector for positions (10000000, 10000000, 10000000)
                _mdxWriter.WriteVector3(new Vector3(10000000.0f, 10000000.0f, 10000000.0f));
            }

            if (mdlNode.Mesh.VertexNormals != null && mdlNode.Mesh.VertexNormals.Count > 0)
            {
                // Write null vector for normals (0, 0, 0)
                _mdxWriter.WriteVector3(Vector3.Zero);
            }

            if (mdlNode.Mesh.VertexUv1 != null && mdlNode.Mesh.VertexUv1.Count > 0)
            {
                // Write null vector2 for UV1 (0, 0)
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
            }

            if (mdlNode.Mesh.VertexUv2 != null && mdlNode.Mesh.VertexUv2.Count > 0)
            {
                // Write null vector2 for UV2 (0, 0)
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
            }

            // For skin meshes, write additional padding
            if (mdlNode.Skin != null && mdlNode.Skin.VertexBones != null && mdlNode.Skin.VertexBones.Count > 0)
            {
                // Reference: vendor/mdlops/MDLOpsM.pm:8102-8105 (skin padding)
                // Skin nodes have different padding: (1000000, 1000000, 1000000, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0)
                _mdxWriter.WriteSingle(1000000.0f);
                _mdxWriter.WriteSingle(1000000.0f);
                _mdxWriter.WriteSingle(1000000.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(1.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
                _mdxWriter.WriteSingle(0.0f);
            }
        }

        private void _WriteAll()
        {
            // Update MDX data for all nodes with meshes
            for (int i = 0; i < _binNodes.Count; i++)
            {
                if (_binNodes[i].Trimesh != null)
                {
                    _UpdateMdx(_binNodes[i], _mdlNodes[i]);
                }
            }

            // Set file header properties
            _fileHeader.Geometry.FunctionPointer0 = GeometryHeader.K1_FUNCTION_POINTER0;
            _fileHeader.Geometry.FunctionPointer1 = GeometryHeader.K1_FUNCTION_POINTER1;
            _fileHeader.Geometry.ModelName = _mdl.Name;
            _fileHeader.Geometry.NodeCount = (uint)_mdlNodes.Count;
            _fileHeader.Geometry.GeometryType = GeometryHeader.GEOM_TYPE_ROOT;
            _fileHeader.OffsetToSuperRoot = _fileHeader.Geometry.RootNodeOffset;
            _fileHeader.AnimationCount = (uint)_mdl.Anims.Count;
            _fileHeader.AnimationCount2 = (uint)_mdl.Anims.Count;
            _fileHeader.Supermodel = _mdl.Supermodel ?? "";
            _fileHeader.NameOffsetsCount = (uint)_names.Count;
            _fileHeader.NameOffsetsCount2 = (uint)_names.Count;

            // Write MDL data to memory buffer first
            using (var mdlBuffer = RawBinaryWriter.ToByteArray())
            {
                // Write file header
                _fileHeader.Write(mdlBuffer);

                // Write name offsets
                foreach (var nameOffset in _nameOffsets)
                {
                    mdlBuffer.WriteUInt32((uint)nameOffset);
                }

                // Write names
                foreach (var name in _names)
                {
                    mdlBuffer.WriteString(name + "\0", "ascii", 0, name.Length + 1, '\0', false);
                }

                // Write animation offsets
                foreach (var animOffset in _animOffsets)
                {
                    mdlBuffer.WriteUInt32((uint)animOffset);
                }

                // Write animations
                bool isK2Write = _game.IsK2();
                foreach (var binAnim in _binAnims)
                {
                    binAnim.Write(mdlBuffer, isK2Write);
                }

                // Write nodes
                foreach (var binNode in _binNodes)
                {
                    binNode.Write(mdlBuffer, isK2Write);
                }

                // Get MDL and MDX sizes
                uint mdlSize = (uint)mdlBuffer.Position();
                uint mdxSize = (uint)_mdxWriter.Position();

                // Update MDX size in file header (already written, but we'll overwrite)
                _fileHeader.MdxSize = mdxSize;

                // Write file header (12 bytes: unused, mdl_size, mdx_size)
                _writer.WriteUInt32(0); // Unused
                _writer.WriteUInt32(mdlSize);
                _writer.WriteUInt32(mdxSize);

                // Write MDL data
                byte[] mdlData = mdlBuffer.Data();
                _writer.WriteBytes(mdlData);
            }

            // MDX data is already written to _mdxWriter, no need to write again
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _mdxWriter?.Dispose();
        }
    }
}

