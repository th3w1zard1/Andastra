using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using BioWare.NET.Common;
using BioWare.NET.Resource.Formats.BWM;

namespace BioWare.NET.Resource.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/io_bwm.py:185-355
    // Original: class BWMBinaryWriter(ResourceWriter)
    public class BWMBinaryWriter : IDisposable
    {
        private const int HeaderSize = 136;
        private readonly BWM _wok;
        private readonly RawBinaryWriter _writer;

        public BWMBinaryWriter(BWM wok, string filepath)
        {
            _wok = wok ?? throw new ArgumentNullException(nameof(wok));
            _writer = RawBinaryWriter.ToFile(filepath);
        }

        public BWMBinaryWriter(BWM wok, Stream target)
        {
            _wok = wok ?? throw new ArgumentNullException(nameof(wok));
            _writer = RawBinaryWriter.ToStream(target);
        }

        public BWMBinaryWriter(BWM wok)
        {
            _wok = wok ?? throw new ArgumentNullException(nameof(wok));
            _writer = RawBinaryWriter.ToByteArray();
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/io_bwm.py:196-355
        // Original: @autoclose def write(self, *, auto_close: bool = True)
        public void Write(bool autoClose = true)
        {
            try
            {
                List<Vector3> vertices = _wok.Vertices();

                List<BWMFace> walkable = _wok.WalkableFaces();
                List<BWMFace> unwalkable = _wok.UnwalkableFaces();
                List<BWMFace> faces = new List<BWMFace>();
                faces.AddRange(walkable);
                faces.AddRange(unwalkable);
                List<BWMNodeAABB> aabbs = _wok.Aabbs();

                int vertexOffset = HeaderSize;
                byte[] vertexData = BuildVertexData(vertices);

                int indicesOffset = vertexOffset + vertexData.Length;
                byte[] indicesData = BuildIndicesData(faces, vertices);

                int materialOffset = indicesOffset + indicesData.Length;
                byte[] materialData = BuildMaterialData(faces);

                int normalOffset = materialOffset + materialData.Length;
                byte[] normalData = BuildNormalData(faces);

                int coefficientOffset = normalOffset + normalData.Length;
                byte[] coefficientData = BuildCoefficientData(faces);

                int aabbOffset = coefficientOffset + coefficientData.Length;
                byte[] aabbData = BuildAabbData(aabbs, faces);

                int adjacencyOffset = aabbOffset + aabbData.Length;
                byte[] adjacencyData = BuildAdjacencyData(walkable, faces);

                List<BWMEdge> perimeterEdges = _wok.Edges();
                List<BWMEdge> edges = RemapEdges(perimeterEdges, faces);

                int edgeOffset = adjacencyOffset + adjacencyData.Length;
                byte[] edgeData = BuildEdgeData(edges, faces);

                List<int> perimeters = BuildPerimeters(edges);
                int perimeterOffset = edgeOffset + edgeData.Length;
                byte[] perimeterData = BuildPerimeterData(perimeters);

                // Write header
                _writer.WriteString("BWM ", stringLength: 4);
                _writer.WriteString("V1.0", stringLength: 4);
                _writer.WriteUInt32((uint)_wok.WalkmeshType);
                _writer.WriteVector3(_wok.RelativeHook1);
                _writer.WriteVector3(_wok.RelativeHook2);
                _writer.WriteVector3(_wok.AbsoluteHook1);
                _writer.WriteVector3(_wok.AbsoluteHook2);
                _writer.WriteVector3(_wok.Position);

                _writer.WriteUInt32((uint)vertices.Count);
                _writer.WriteUInt32((uint)vertexOffset);
                _writer.WriteUInt32((uint)faces.Count);
                _writer.WriteUInt32((uint)indicesOffset);
                _writer.WriteUInt32((uint)materialOffset);
                _writer.WriteUInt32((uint)normalOffset);
                _writer.WriteUInt32((uint)coefficientOffset);
                _writer.WriteUInt32((uint)aabbs.Count);
                _writer.WriteUInt32((uint)aabbOffset);
                _writer.WriteUInt32(0);
                _writer.WriteUInt32((uint)walkable.Count);
                _writer.WriteUInt32((uint)adjacencyOffset);
                _writer.WriteUInt32((uint)edges.Count);
                _writer.WriteUInt32((uint)edgeOffset);
                _writer.WriteUInt32((uint)perimeters.Count);
                _writer.WriteUInt32((uint)perimeterOffset);

                // Write data sections
                _writer.WriteBytes(vertexData);
                _writer.WriteBytes(indicesData);
                _writer.WriteBytes(materialData);
                _writer.WriteBytes(normalData);
                _writer.WriteBytes(coefficientData);
                _writer.WriteBytes(aabbData);
                _writer.WriteBytes(adjacencyData);
                _writer.WriteBytes(edgeData);
                _writer.WriteBytes(perimeterData);
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        public byte[] Data()
        {
            return _writer.Data();
        }

        private byte[] BuildVertexData(List<Vector3> vertices)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var vertex in vertices)
                {
                    bw.Write(vertex.X);
                    bw.Write(vertex.Y);
                    bw.Write(vertex.Z);
                }
                return ms.ToArray();
            }
        }

        private byte[] BuildIndicesData(List<BWMFace> faces, List<Vector3> vertices)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var face in faces)
                {
                    // Find vertex indices by object identity (value equality for structs)
                    int i1 = FindVertexIndex(face.V1, vertices);
                    int i2 = FindVertexIndex(face.V2, vertices);
                    int i3 = FindVertexIndex(face.V3, vertices);
                    bw.Write((uint)i1);
                    bw.Write((uint)i2);
                    bw.Write((uint)i3);
                }
                return ms.ToArray();
            }
        }

        private int FindVertexIndex(Vector3 vertex, List<Vector3> vertices)
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertex.Equals(vertices[i]))
                {
                    return i;
                }
            }
            throw new ArgumentException("Vertex not found in vertices list");
        }

        private byte[] BuildMaterialData(List<BWMFace> faces)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var face in faces)
                {
                    bw.Write((uint)face.Material);
                }
                return ms.ToArray();
            }
        }

        private byte[] BuildNormalData(List<BWMFace> faces)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var face in faces)
                {
                    Vector3 normal = face.Normal();
                    bw.Write(normal.X);
                    bw.Write(normal.Y);
                    bw.Write(normal.Z);
                }
                return ms.ToArray();
            }
        }

        private byte[] BuildCoefficientData(List<BWMFace> faces)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var face in faces)
                {
                    bw.Write(face.PlanarDistance());
                }
                return ms.ToArray();
            }
        }

        private byte[] BuildAabbData(List<BWMNodeAABB> aabbs, List<BWMFace> faces)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var aabb in aabbs)
                {
                    bw.Write(aabb.BbMin.X);
                    bw.Write(aabb.BbMin.Y);
                    bw.Write(aabb.BbMin.Z);
                    bw.Write(aabb.BbMax.X);
                    bw.Write(aabb.BbMax.Y);
                    bw.Write(aabb.BbMax.Z);
                    // Find face index by object identity
                    uint faceIdx = aabb.Face == null ? 0xFFFFFFFF : (uint)FindFaceIndex(aabb.Face, faces);
                    bw.Write(faceIdx);
                    bw.Write((uint)4);
                    bw.Write((uint)(int)aabb.Sigplane);
                    // Find AABB indices by object identity
                    // CRITICAL FIX: Use 0-based indices (not 1-based) for AABB children
                    // The game engine (swkotor.exe/swkotor2.exe) reads these as direct array indices.
                    // Reference: vendor/reone/src/libs/graphics/format/bwmreader.cpp:164-167
                    // Reference: wiki/BWM-File-Format.md - AABB Tree section - Vendor Discrepancy
                    uint leftIdx = aabb.Left == null ? 0xFFFFFFFF : (uint)FindAabbIndex(aabb.Left, aabbs);
                    uint rightIdx = aabb.Right == null ? 0xFFFFFFFF : (uint)FindAabbIndex(aabb.Right, aabbs);
                    bw.Write(leftIdx);
                    bw.Write(rightIdx);
                }
                return ms.ToArray();
            }
        }

        private int FindFaceIndex(BWMFace face, List<BWMFace> faces)
        {
            for (int i = 0; i < faces.Count; i++)
            {
                if (ReferenceEquals(faces[i], face))
                {
                    return i;
                }
            }
            throw new ArgumentException("Face not found in faces list");
        }

        private int FindAabbIndex(BWMNodeAABB aabb, List<BWMNodeAABB> aabbs)
        {
            for (int i = 0; i < aabbs.Count; i++)
            {
                if (ReferenceEquals(aabbs[i], aabb))
                {
                    return i;
                }
            }
            throw new ArgumentException("AABB not found in aabbs list");
        }

        private byte[] BuildAdjacencyData(List<BWMFace> walkable, List<BWMFace> faces)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var face in walkable)
                {
                    var adjacencies = _wok.Adjacencies(face);
                    int[] indexes = new int[3];
                    indexes[0] = adjacencies.Item1 == null ? -1 : FindFaceIndex(adjacencies.Item1.Face, faces) * 3 + adjacencies.Item1.Edge;
                    indexes[1] = adjacencies.Item2 == null ? -1 : FindFaceIndex(adjacencies.Item2.Face, faces) * 3 + adjacencies.Item2.Edge;
                    indexes[2] = adjacencies.Item3 == null ? -1 : FindFaceIndex(adjacencies.Item3.Face, faces) * 3 + adjacencies.Item3.Edge;
                    bw.Write(indexes[0]);
                    bw.Write(indexes[1]);
                    bw.Write(indexes[2]);
                }
                return ms.ToArray();
            }
        }

        private List<BWMEdge> RemapEdges(List<BWMEdge> perimeterEdges, List<BWMFace> faces)
        {
            List<BWMEdge> edges = new List<BWMEdge>();
            foreach (var edge in perimeterEdges)
            {
                // Find the face index in the reordered list BY IDENTITY (not value equality)
                int? faceIdx = null;
                for (int i = 0; i < faces.Count; i++)
                {
                    if (ReferenceEquals(faces[i], edge.Face))
                    {
                        faceIdx = i;
                        break;
                    }
                }
                if (faceIdx.HasValue)
                {
                    edges.Add(new BWMEdge(faces[faceIdx.Value], edge.Index, edge.Transition));
                }
            }
            return edges;
        }

        private byte[] BuildEdgeData(List<BWMEdge> edges, List<BWMFace> faces)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var edge in edges)
                {
                    // Find face index by object identity
                    int faceIdx = FindFaceIndex(edge.Face, faces);
                    int edgeIndex = faceIdx * 3 + edge.Index;
                    bw.Write(edgeIndex);
                    bw.Write(edge.Transition);
                }
                return ms.ToArray();
            }
        }

        private List<int> BuildPerimeters(List<BWMEdge> edges)
        {
            List<int> perimeters = new List<int>();
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].Final)
                {
                    perimeters.Add(i + 1);
                }
            }
            return perimeters;
        }

        private byte[] BuildPerimeterData(List<int> perimeters)
        {
            using (var ms = new MemoryStream())
            using (var bw = new System.IO.BinaryWriter(ms))
            {
                foreach (var perimeter in perimeters)
                {
                    bw.Write((uint)perimeter);
                }
                return ms.ToArray();
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}
