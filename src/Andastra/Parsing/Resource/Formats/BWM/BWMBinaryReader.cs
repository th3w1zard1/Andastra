using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.BWM;

namespace Andastra.Parsing.Formats.BWM
{
    // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/io_bwm.py:41-182
    // Original: class BWMBinaryReader(ResourceReader)
    public class BWMBinaryReader : IDisposable
    {
        private readonly Andastra.Parsing.Common.RawBinaryReader _reader;
        private BWM _wok;

        public BWMBinaryReader(byte[] data, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = Andastra.Parsing.Common.RawBinaryReader.FromBytes(data, offset, sizeNullable);
        }

        public BWMBinaryReader(string filepath, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = Andastra.Parsing.Common.RawBinaryReader.FromFile(filepath, offset, sizeNullable);
        }

        public BWMBinaryReader(Stream source, int offset = 0, int size = 0)
        {
            int? sizeNullable = size > 0 ? (int?)size : null;
            _reader = Andastra.Parsing.Common.RawBinaryReader.FromStream(source, offset, sizeNullable);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/resource/formats/bwm/io_bwm.py:84-182
        // Original: @autoclose def load(self, *, auto_close: bool = True) -> BWM
        public BWM Load(bool autoClose = true)
        {
            try
            {
                _wok = new BWM();

                string fileType = _reader.ReadString(4);
                string fileVersion = _reader.ReadString(4);

                if (fileType != "BWM ")
                {
                    throw new ArgumentException($"Not a valid binary BWM file. Expected 'BWM ', got '{fileType}'");
                }

                if (fileVersion != "V1.0")
                {
                    throw new ArgumentException($"Unsupported BWM version: got '{fileVersion}', expected 'V1.0'");
                }

                _wok.WalkmeshType = (BWMType)_reader.ReadUInt32();
                _wok.RelativeHook1 = _reader.ReadVector3();
                _wok.RelativeHook2 = _reader.ReadVector3();
                _wok.AbsoluteHook1 = _reader.ReadVector3();
                _wok.AbsoluteHook2 = _reader.ReadVector3();
                _wok.Position = _reader.ReadVector3();

                uint verticesCount = _reader.ReadUInt32();
                uint verticesOffset = _reader.ReadUInt32();
                uint faceCount = _reader.ReadUInt32();
                uint indicesOffset = _reader.ReadUInt32();
                uint materialsOffset = _reader.ReadUInt32();
                _reader.ReadUInt32(); // normals_offset
                _reader.ReadUInt32(); // planar_distances_offset

                _reader.ReadUInt32(); // aabb_count
                _reader.ReadUInt32(); // aabb_offset
                _reader.Skip(4);
                _reader.ReadUInt32(); // adjacencies_count
                _reader.ReadUInt32(); // adjacencies_offset
                uint edgesCount = _reader.ReadUInt32();
                uint edgesOffset = _reader.ReadUInt32();
                _reader.ReadUInt32(); // perimeters_count
                _reader.ReadUInt32(); // perimeters_offset

                _reader.Seek((int)verticesOffset);
                List<Vector3> vertices = new List<Vector3>();
                for (int i = 0; i < verticesCount; i++)
                {
                    vertices.Add(_reader.ReadVector3());
                }

                List<BWMFace> faces = new List<BWMFace>();
                _reader.Seek((int)indicesOffset);
                for (int i = 0; i < faceCount; i++)
                {
                    uint i1 = _reader.ReadUInt32();
                    uint i2 = _reader.ReadUInt32();
                    uint i3 = _reader.ReadUInt32();
                    Vector3 v1 = vertices[(int)i1];
                    Vector3 v2 = vertices[(int)i2];
                    Vector3 v3 = vertices[(int)i3];
                    faces.Add(new BWMFace(v1, v2, v3));
                }

                _reader.Seek((int)materialsOffset);
                foreach (var face in faces)
                {
                    uint materialId = _reader.ReadUInt32();
                    face.Material = (SurfaceMaterial)materialId;
                }

                _reader.Seek((int)edgesOffset);
                for (int i = 0; i < edgesCount; i++)
                {
                    uint edgeIndex = _reader.ReadUInt32();
                    uint transition = _reader.ReadUInt32();

                    if (transition != 0xFFFFFFFF)
                    {
                        int faceIndex = (int)(edgeIndex / 3);
                        int transIndex = (int)(edgeIndex % 3);
                        if (transIndex == 0)
                        {
                            faces[faceIndex].Trans1 = (int)transition;
                        }
                        else if (transIndex == 1)
                        {
                            faces[faceIndex].Trans2 = (int)transition;
                        }
                        else if (transIndex == 2)
                        {
                            faces[faceIndex].Trans3 = (int)transition;
                        }
                    }
                }

                _wok.Faces = faces;

                return _wok;
            }
            finally
            {
                if (autoClose)
                {
                    Dispose();
                }
            }
        }

        public void Dispose()
        {
            _reader?.Dispose();
        }
    }
}
