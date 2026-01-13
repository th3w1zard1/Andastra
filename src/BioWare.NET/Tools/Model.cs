using System;
using System.Collections.Generic;
using BioWare.NET.Common;
using JetBrains.Annotations;

namespace BioWare.NET.Tools
{
    /// <summary>
    /// Tuple class for returning MDL and MDX data pairs.
    /// Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:87-89
    /// </summary>
    public class MDLMDXTuple
    {
        public byte[] Mdl { get; set; }
        public byte[] Mdx { get; set; }

        public MDLMDXTuple(byte[] mdl, byte[] mdx)
        {
            Mdl = mdl;
            Mdx = mdx;
        }
    }

    /// <summary>
    /// Utility functions for working with 3D model data.
    /// </summary>
    [PublicAPI]
    public static class ModelTools
    {
        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:34-78
        // Geometry root function pointer constants
        private const uint _GEOM_ROOT_FP0_K1 = 4273776;
        private const uint _GEOM_ROOT_FP1_K1 = 4216096;
        private const uint _GEOM_ROOT_FP0_K2 = 4285200;
        private const uint _GEOM_ROOT_FP1_K2 = 4216320;

        // Geometry animation function pointer constants
        private const uint _GEOM_ANIM_FP0_K1 = 4273392;
        private const uint _GEOM_ANIM_FP1_K1 = 4451552;
        private const uint _GEOM_ANIM_FP0_K2 = 4284816;
        private const uint _GEOM_ANIM_FP1_K2 = 4522928;

        // Mesh type constants for determining TSL vs K1
        private const uint _MESH_FP0_K1 = 4216656;
        private const uint _MESH_FP1_K1 = 4216672;
        private const uint _MESH_FP0_K2 = 4216880;
        private const uint _MESH_FP1_K2 = 4216896;
        private const int _MESH_HEADER_SIZE_K1 = 332;
        private const int _MESH_HEADER_SIZE_K2 = 340;

        // Skin constants
        private const uint _SKIN_FP0_K1 = 4216592;
        private const uint _SKIN_FP1_K1 = 4216608;
        private const uint _SKIN_FP0_K2 = 4216816;
        private const uint _SKIN_FP1_K2 = 4216832;
        private const int _SKIN_HEADER_SIZE = 108;

        // Dangly constants
        private const uint _DANGLY_FP0_K1 = 4216640;
        private const uint _DANGLY_FP1_K1 = 4216624;
        private const uint _DANGLY_FP0_K2 = 4216864;
        private const uint _DANGLY_FP1_K2 = 4216848;
        private const int _DANGLY_HEADER_SIZE = 28;

        // Saber constants
        private const uint _SABER_FP0_K1 = 4216656;
        private const uint _SABER_FP1_K1 = 4216672;
        private const uint _SABER_FP0_K2 = 4216880;
        private const uint _SABER_FP1_K2 = 4216896;
        private const int _SABER_HEADER_SIZE = 20;

        // AABB constants
        private const uint _AABB_FP0_K1 = 4216656;
        private const uint _AABB_FP1_K1 = 4216672;
        private const uint _AABB_FP0_K2 = 4216880;
        private const uint _AABB_FP1_K2 = 4216896;
        private const int _AABB_HEADER_SIZE = 4;

        // Node type constants
        private const int _NODE_TYPE_MESH = 32;
        private const int _NODE_TYPE_SKIN = 64;
        private const int _NODE_TYPE_DANGLY = 256;
        private const int _NODE_TYPE_SABER = 2048;
        private const int _NODE_TYPE_AABB = 512;
        private const int _NODE_TYPE_LIGHT = 2;
        private const int _NODE_TYPE_EMITTER = 4;
        /// <summary>
        /// Extracts texture and lightmap names from MDL model data.
        /// </summary>
        /// <param name="data">The binary MDL data.</param>
        /// <returns>An enumerable of texture and lightmap names.</returns>
        public static IEnumerable<string> IterateTexturesAndLightmaps(byte[] data)
        {
            HashSet<string> seenNames = new HashSet<string>();

            using (RawBinaryReader reader = RawBinaryReader.FromBytes(data, 12))
            {
                reader.Seek(168);
                uint rootOffset = reader.ReadUInt32();

                Queue<uint> nodes = new Queue<uint>();
                nodes.Enqueue(rootOffset);

                while (nodes.Count > 0)
                {
                    uint nodeOffset = nodes.Dequeue();
                    reader.Seek((int)nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek((int)nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek((int)childOffsetsOffset);
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        nodes.Enqueue(reader.ReadUInt32());
                    }

                    if ((nodeId & 32) != 0)
                    {
                        // Extract texture name
                        reader.Seek((int)nodeOffset + 168);
                        string name = reader.ReadString(32, encoding: "ascii", errors: "ignore").Trim().ToLower();
                        if (!string.IsNullOrEmpty(name) && name != "null" && !seenNames.Contains(name) && name != "dirt")
                        {
                            seenNames.Add(name);
                            yield return name;
                        }

                        // Extract lightmap name
                        reader.Seek((int)nodeOffset + 200);
                        name = reader.ReadString(32, encoding: "ascii", errors: "ignore").Trim().ToLower();
                        if (!string.IsNullOrEmpty(name) && name != "null" && !seenNames.Contains(name))
                        {
                            seenNames.Add(name);
                            yield return name;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts texture names from MDL model data.
        /// </summary>
        /// <param name="data">The binary MDL data.</param>
        /// <returns>An enumerable of texture names.</returns>
        public static IEnumerable<string> IterateTextures(byte[] data)
        {
            HashSet<string> textureCaseset = new HashSet<string>();

            using (RawBinaryReader reader = RawBinaryReader.FromBytes(data, 12))
            {
                reader.Seek(168);
                uint rootOffset = reader.ReadUInt32();

                Stack<uint> nodes = new Stack<uint>();
                nodes.Push(rootOffset);

                while (nodes.Count > 0)
                {
                    uint nodeOffset = nodes.Pop();
                    reader.Seek((int)nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek((int)nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek((int)childOffsetsOffset);
                    Stack<uint> childOffsets = new Stack<uint>();
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        childOffsets.Push(reader.ReadUInt32());
                    }
                    while (childOffsets.Count > 0)
                    {
                        nodes.Push(childOffsets.Pop());
                    }

                    if ((nodeId & 32) != 0)
                    {
                        reader.Seek((int)nodeOffset + 168);
                        string texture = reader.ReadString(32, encoding: "ascii", errors: "ignore").Trim();
                        string lowerTexture = texture.ToLower();
                        if (!string.IsNullOrEmpty(texture)
                            && texture.ToUpper() != "NULL"
                            && !textureCaseset.Contains(lowerTexture)
                            && lowerTexture != "dirt")
                        {
                            textureCaseset.Add(lowerTexture);
                            yield return lowerTexture;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts lightmap names from MDL model data.
        /// </summary>
        /// <param name="data">The binary MDL data.</param>
        /// <returns>An enumerable of lightmap names.</returns>
        public static IEnumerable<string> IterateLightmaps(byte[] data)
        {
            HashSet<string> lightmapsCaseset = new HashSet<string>();

            using (RawBinaryReader reader = RawBinaryReader.FromBytes(data, 12))
            {
                reader.Seek(168);
                uint rootOffset = reader.ReadUInt32();

                Stack<uint> nodes = new Stack<uint>();
                nodes.Push(rootOffset);

                while (nodes.Count > 0)
                {
                    uint nodeOffset = nodes.Pop();
                    reader.Seek((int)nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek((int)nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek((int)childOffsetsOffset);
                    Stack<uint> childOffsets = new Stack<uint>();
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        childOffsets.Push(reader.ReadUInt32());
                    }
                    while (childOffsets.Count > 0)
                    {
                        nodes.Push(childOffsets.Pop());
                    }

                    if ((nodeId & 32) != 0)
                    {
                        reader.Seek((int)nodeOffset + 200);
                        string lightmap = reader.ReadString(32, encoding: "ascii", errors: "ignore").Trim().ToLower();
                        if (!string.IsNullOrEmpty(lightmap) && lightmap != "null" && !lightmapsCaseset.Contains(lightmap))
                        {
                            lightmapsCaseset.Add(lightmap);
                            yield return lightmap;
                        }
                    }
                }
            }
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:92-96
        // Original: def rename(data: bytes, name: str) -> bytes:
        /// <summary>
        /// Renames an MDL model by replacing the name field at offset 20.
        /// </summary>
        public static byte[] Rename(byte[] data, string name)
        {
            if (data == null || data.Length < 52)
            {
                throw new ArgumentException("Invalid MDL data");
            }
            byte[] result = new byte[data.Length];
            Array.Copy(data, 0, result, 0, 20);
            byte[] nameBytes = new byte[32];
            System.Text.Encoding.ASCII.GetBytes(name.PadRight(32, '\0'), 0, Math.Min(name.Length, 32), nameBytes, 0);
            Array.Copy(nameBytes, 0, result, 20, 32);
            Array.Copy(data, 52, result, 52, data.Length - 52);
            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:197-248
        // Original: def change_textures(data: bytes | bytearray, textures: dict[str, str]) -> bytes | bytearray:
        /// <summary>
        /// Changes texture names in MDL model data.
        /// </summary>
        public static byte[] ChangeTextures(byte[] data, Dictionary<string, string> textures)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (textures == null)
            {
                return data;
            }

            byte[] parsedData = new byte[data.Length];
            Array.Copy(data, parsedData, data.Length);
            Dictionary<string, List<int>> offsets = new Dictionary<string, List<int>>();

            // Normalize texture names to lowercase
            Dictionary<string, string> texturesLower = new Dictionary<string, string>();
            foreach (var kvp in textures)
            {
                texturesLower[kvp.Key.ToLowerInvariant()] = kvp.Value.ToLowerInvariant();
            }

            using (RawBinaryReader reader = RawBinaryReader.FromBytes(parsedData, 12))
            {
                reader.Seek(168);
                uint rootOffset = reader.ReadUInt32();

                Stack<uint> nodes = new Stack<uint>();
                nodes.Push(rootOffset);

                while (nodes.Count > 0)
                {
                    uint nodeOffset = nodes.Pop();
                    reader.Seek((int)nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek((int)nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek((int)childOffsetsOffset);
                    Stack<uint> childOffsets = new Stack<uint>();
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        childOffsets.Push(reader.ReadUInt32());
                    }
                    while (childOffsets.Count > 0)
                    {
                        nodes.Push(childOffsets.Pop());
                    }

                    if ((nodeId & 32) != 0)
                    {
                        reader.Seek((int)nodeOffset + 168);
                        string texture = reader.ReadString(32, encoding: "ascii", errors: "ignore").Trim().ToLowerInvariant();

                        if (texturesLower.ContainsKey(texture))
                        {
                            if (!offsets.ContainsKey(texture))
                            {
                                offsets[texture] = new List<int>();
                            }
                            offsets[texture].Add((int)nodeOffset + 168);
                        }
                    }
                }
            }

            // Replace texture names at found offsets
            foreach (var kvp in offsets)
            {
                string newTexture = texturesLower[kvp.Key];
                byte[] newTextureBytes = new byte[32];
                System.Text.Encoding.ASCII.GetBytes(newTexture.PadRight(32, '\0'), 0, Math.Min(newTexture.Length, 32), newTextureBytes, 0);
                foreach (int offset in kvp.Value)
                {
                    int actualOffset = offset + 12;
                    Array.Copy(newTextureBytes, 0, parsedData, actualOffset, 32);
                }
            }

            return parsedData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:251-302
        // Original: def change_lightmaps(data: bytes | bytearray, textures: dict[str, str]) -> bytes | bytearray:
        /// <summary>
        /// Changes lightmap names in MDL model data.
        /// </summary>
        public static byte[] ChangeLightmaps(byte[] data, Dictionary<string, string> lightmaps)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (lightmaps == null)
            {
                return data;
            }

            byte[] parsedData = new byte[data.Length];
            Array.Copy(data, parsedData, data.Length);
            Dictionary<string, List<int>> offsets = new Dictionary<string, List<int>>();

            // Normalize lightmap names to lowercase
            Dictionary<string, string> lightmapsLower = new Dictionary<string, string>();
            foreach (var kvp in lightmaps)
            {
                lightmapsLower[kvp.Key.ToLowerInvariant()] = kvp.Value.ToLowerInvariant();
            }

            using (RawBinaryReader reader = RawBinaryReader.FromBytes(parsedData, 12))
            {
                reader.Seek(168);
                uint rootOffset = reader.ReadUInt32();

                Stack<uint> nodes = new Stack<uint>();
                nodes.Push(rootOffset);

                while (nodes.Count > 0)
                {
                    uint nodeOffset = nodes.Pop();
                    reader.Seek((int)nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek((int)nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek((int)childOffsetsOffset);
                    Stack<uint> childOffsets = new Stack<uint>();
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        childOffsets.Push(reader.ReadUInt32());
                    }
                    while (childOffsets.Count > 0)
                    {
                        nodes.Push(childOffsets.Pop());
                    }

                    if ((nodeId & 32) != 0)
                    {
                        reader.Seek((int)nodeOffset + 200);
                        string lightmap = reader.ReadString(32, encoding: "ascii", errors: "ignore").Trim().ToLowerInvariant();

                        if (lightmapsLower.ContainsKey(lightmap))
                        {
                            if (!offsets.ContainsKey(lightmap))
                            {
                                offsets[lightmap] = new List<int>();
                            }
                            offsets[lightmap].Add((int)nodeOffset + 200);
                        }
                    }
                }
            }

            // Replace lightmap names at found offsets
            foreach (var kvp in offsets)
            {
                string newLightmap = lightmapsLower[kvp.Key];
                byte[] newLightmapBytes = new byte[32];
                System.Text.Encoding.ASCII.GetBytes(newLightmap.PadRight(32, '\0'), 0, Math.Min(newLightmap.Length, 32), newLightmapBytes, 0);
                foreach (int offset in kvp.Value)
                {
                    int actualOffset = offset + 12;
                    Array.Copy(newLightmapBytes, 0, parsedData, actualOffset, 32);
                }
            }

            return parsedData;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:724-886
        // Original: def flip(mdl_data: bytes | bytearray, mdx_data: bytes | bytearray, *, flip_x: bool, flip_y: bool) -> MDLMDXTuple:
        /// <summary>
        /// Flips a model by negating X and/or Y coordinates in vertices and normals.
        /// </summary>
        /// <param name="mdlData">The MDL model data.</param>
        /// <param name="mdxData">The MDX material index data.</param>
        /// <param name="flipX">Whether to flip along the X axis.</param>
        /// <param name="flipY">Whether to flip along the Y axis.</param>
        /// <returns>A tuple containing the flipped MDL and MDX data.</returns>
        public static MDLMDXTuple Flip(byte[] mdlData, byte[] mdxData, bool flipX, bool flipY)
        {
            // If neither bools are set to True, no transformations need to be done and we can just return the original data
            // Matching PyKotor implementation: if not flip_x and not flip_y: return MDLMDXTuple(mdl_data, mdx_data)
            if (!flipX && !flipY)
            {
                return new MDLMDXTuple(mdlData, mdxData);
            }

            // The data we need to change:
            //    1. The vertices stored in the MDL
            //    2. The vertex positions, normals, stored in the MDX

            // Trim the data to correct the offsets
            byte[] mdlStart = new byte[12];
            Array.Copy(mdlData, 0, mdlStart, 0, 12);
            byte[] parsedMdlData = new byte[mdlData.Length - 12];
            Array.Copy(mdlData, 12, parsedMdlData, 0, parsedMdlData.Length);
            byte[] parsedMdxData = new byte[mdxData.Length];
            Array.Copy(mdxData, 0, parsedMdxData, 0, mdxData.Length);

            // Lists to store offsets: (count, offset) for MDL vertices
            var mdlVertexOffsets = new List<Tuple<int, int>>();
            // Lists to store offsets: (count, offset, stride, position) for MDX vertices and normals
            var mdxVertexOffsets = new List<Tuple<int, int, int, int>>();
            var mdxNormalOffsets = new List<Tuple<int, int, int, int>>();
            var elementsOffsets = new List<Tuple<int, int>>();
            var facesOffsets = new List<Tuple<int, int>>();

            using (RawBinaryReader reader = RawBinaryReader.FromBytes(parsedMdlData, 0))
            {
                reader.Seek(168);
                uint rootOffset = reader.ReadUInt32();

                var nodes = new Stack<uint>();
                nodes.Push(rootOffset);

                while (nodes.Count > 0)
                {
                    uint nodeOffset = nodes.Pop();
                    reader.Seek((int)nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    mdlVertexOffsets.Add(new Tuple<int, int>(1, (int)nodeOffset + 16));

                    // Need to determine the location of the position controller
                    reader.Seek((int)nodeOffset + 56);
                    uint controllersOffset = reader.ReadUInt32();
                    uint controllersCount = reader.ReadUInt32();

                    reader.Seek((int)nodeOffset + 68);
                    uint controllerDatasOffset = reader.ReadUInt32();
                    reader.ReadUInt32(); // Skip next uint32

                    for (uint i = 0; i < controllersCount; i++)
                    {
                        reader.Seek((int)(controllersOffset + i * 16));
                        uint controllerType = reader.ReadUInt32();
                        if (controllerType == 8)
                        {
                            reader.Seek((int)(controllersOffset + i * 16 + 6));
                            ushort dataOffset = reader.ReadUInt16();
                            mdlVertexOffsets.Add(new Tuple<int, int>(1, (int)(controllerDatasOffset + dataOffset * 4)));
                        }
                    }

                    reader.Seek((int)nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek((int)childOffsetsOffset);
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        nodes.Push(reader.ReadUInt32());
                    }

                    if ((nodeId & _NODE_TYPE_MESH) != 0)
                    {
                        reader.Seek((int)nodeOffset + 80);
                        uint fp = reader.ReadUInt32();
                        bool tsl = fp != _MESH_FP0_K1 && fp != _SKIN_FP0_K1 && fp != _DANGLY_FP0_K2 && fp != _AABB_FP0_K1 && fp != _SABER_FP0_K1;

                        reader.Seek((int)nodeOffset + 80 + 8);
                        uint facesOffset = reader.ReadUInt32();
                        uint facesCount = reader.ReadUInt32();
                        facesOffsets.Add(new Tuple<int, int>((int)facesCount, (int)facesOffset));

                        reader.Seek((int)nodeOffset + 80 + 188);
                        uint offsetToElementsOffset = reader.ReadUInt32();
                        reader.Seek((int)offsetToElementsOffset);
                        uint elementsOffset = reader.ReadUInt32();
                        elementsOffsets.Add(new Tuple<int, int>((int)facesCount, (int)elementsOffset));

                        reader.Seek((int)nodeOffset + 80 + 304);
                        ushort vertexCount = reader.ReadUInt16();
                        reader.Seek((int)(nodeOffset + 80 + (tsl ? 336 : 328)));
                        uint vertexOffset = reader.ReadUInt32();
                        mdlVertexOffsets.Add(new Tuple<int, int>(vertexCount, (int)vertexOffset));

                        reader.Seek((int)nodeOffset + 80 + 252);
                        uint mdxStride = reader.ReadUInt32();
                        reader.ReadUInt32(); // Skip next uint32
                        reader.Seek((int)nodeOffset + 80 + 260);
                        uint mdxOffsetPos = reader.ReadUInt32();
                        uint mdxOffsetNorm = reader.ReadUInt32();
                        reader.Seek((int)(nodeOffset + 80 + (tsl ? 332 : 324)));
                        uint mdxStart = reader.ReadUInt32();
                        mdxVertexOffsets.Add(new Tuple<int, int, int, int>((int)vertexCount, (int)mdxStart, (int)mdxStride, (int)mdxOffsetPos));
                        mdxNormalOffsets.Add(new Tuple<int, int, int, int>((int)vertexCount, (int)mdxStart, (int)mdxStride, (int)mdxOffsetNorm));
                    }
                }
            }

            // Fix vertex order
            if (flipX != flipY)
            {
                foreach (var tuple in elementsOffsets)
                {
                    int count = tuple.Item1;
                    int startOffset = tuple.Item2;
                    for (int i = 0; i < count; i++)
                    {
                        int offset = startOffset + i * 6;
                        ushort v1 = BitConverter.ToUInt16(parsedMdlData, offset);
                        ushort v2 = BitConverter.ToUInt16(parsedMdlData, offset + 2);
                        ushort v3 = BitConverter.ToUInt16(parsedMdlData, offset + 4);
                        byte[] v1Bytes = BitConverter.GetBytes(v1);
                        byte[] v3Bytes = BitConverter.GetBytes(v3);
                        byte[] v2Bytes = BitConverter.GetBytes(v2);
                        Array.Copy(v1Bytes, 0, parsedMdlData, offset, 2);
                        Array.Copy(v3Bytes, 0, parsedMdlData, offset + 2, 2);
                        Array.Copy(v2Bytes, 0, parsedMdlData, offset + 4, 2);
                    }
                }

                foreach (var tuple in facesOffsets)
                {
                    int count = tuple.Item1;
                    int startOffset = tuple.Item2;
                    for (int i = 0; i < count; i++)
                    {
                        int offset = startOffset + i * 32 + 26;
                        ushort v1 = BitConverter.ToUInt16(parsedMdlData, offset);
                        ushort v2 = BitConverter.ToUInt16(parsedMdlData, offset + 2);
                        ushort v3 = BitConverter.ToUInt16(parsedMdlData, offset + 4);
                        byte[] v1Bytes = BitConverter.GetBytes(v1);
                        byte[] v3Bytes = BitConverter.GetBytes(v3);
                        byte[] v2Bytes = BitConverter.GetBytes(v2);
                        Array.Copy(v1Bytes, 0, parsedMdlData, offset, 2);
                        Array.Copy(v3Bytes, 0, parsedMdlData, offset + 2, 2);
                        Array.Copy(v2Bytes, 0, parsedMdlData, offset + 4, 2);
                    }
                }
            }

            // Update the MDL vertices
            foreach (var tuple in mdlVertexOffsets)
            {
                int count = tuple.Item1;
                int startOffset = tuple.Item2;
                for (int i = 0; i < count; i++)
                {
                    int offset = startOffset + i * 12;
                    if (flipX)
                    {
                        float x = BitConverter.ToSingle(parsedMdlData, offset);
                        byte[] xBytes = BitConverter.GetBytes(-x);
                        Array.Copy(xBytes, 0, parsedMdlData, offset, 4);
                    }
                    if (flipY)
                    {
                        float y = BitConverter.ToSingle(parsedMdlData, offset + 4);
                        byte[] yBytes = BitConverter.GetBytes(-y);
                        Array.Copy(yBytes, 0, parsedMdlData, offset + 4, 4);
                    }
                }
            }

            // Update the MDX vertices
            foreach (var tuple in mdxVertexOffsets)
            {
                int count = tuple.Item1;
                int startOffset = tuple.Item2;
                int stride = tuple.Item3;
                int position = tuple.Item4;
                for (int i = 0; i < count; i++)
                {
                    int offset = startOffset + i * stride + position;
                    if (flipX)
                    {
                        float x = BitConverter.ToSingle(parsedMdxData, offset);
                        byte[] xBytes = BitConverter.GetBytes(-x);
                        Array.Copy(xBytes, 0, parsedMdxData, offset, 4);
                    }
                    if (flipY)
                    {
                        float y = BitConverter.ToSingle(parsedMdxData, offset + 4);
                        byte[] yBytes = BitConverter.GetBytes(-y);
                        Array.Copy(yBytes, 0, parsedMdxData, offset + 4, 4);
                    }
                }
            }

            // Update the MDX normals
            foreach (var tuple in mdxNormalOffsets)
            {
                int count = tuple.Item1;
                int startOffset = tuple.Item2;
                int stride = tuple.Item3;
                int position = tuple.Item4;
                for (int i = 0; i < count; i++)
                {
                    int offset = startOffset + i * stride + position;
                    if (flipX)
                    {
                        float x = BitConverter.ToSingle(parsedMdxData, offset);
                        byte[] xBytes = BitConverter.GetBytes(-x);
                        Array.Copy(xBytes, 0, parsedMdxData, offset, 4);
                    }
                    if (flipY)
                    {
                        float y = BitConverter.ToSingle(parsedMdxData, offset + 4);
                        byte[] yBytes = BitConverter.GetBytes(-y);
                        Array.Copy(yBytes, 0, parsedMdxData, offset + 4, 4);
                    }
                }
            }

            // Re-add the first 12 bytes
            byte[] resultMdl = new byte[mdlStart.Length + parsedMdlData.Length];
            Array.Copy(mdlStart, 0, resultMdl, 0, mdlStart.Length);
            Array.Copy(parsedMdlData, 0, resultMdl, mdlStart.Length, parsedMdlData.Length);

            return new MDLMDXTuple(resultMdl, parsedMdxData);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:638-721
        // Original: def transform(data: bytes | bytearray, translation: Vector3, rotation: float) -> bytes | bytearray:
        /// <summary>
        /// Transforms a model by injecting a new transform node that applies translation and rotation.
        /// This creates a parent node that applies the transformation to the entire model hierarchy.
        /// </summary>
        /// <param name="data">The MDL model data (with 12-byte header: unused, mdl_size, mdx_size).</param>
        /// <param name="translation">The translation to apply (X, Y, Z).</param>
        /// <param name="rotation">The rotation angle in degrees (around Z-axis).</param>
        /// <returns>The transformed MDL data with the new transform node injected.</returns>
        public static byte[] Transform(byte[] data, System.Numerics.Vector3 translation, float rotation)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length < 12)
            {
                throw new ArgumentException("Invalid MDL data: must be at least 12 bytes", nameof(data));
            }

            // Create quaternion orientation from rotation (Z-axis rotation, roll=0, pitch=0, yaw=rotation)
            // Matching Python: orientation: Vector4 = Vector4.from_euler(0, 0, math.radians(rotation))
            float rotationRadians = (float)(rotation * Math.PI / 180.0);
            System.Numerics.Quaternion orientation = QuaternionFromEuler(0.0, 0.0, rotationRadians);

            // Read MDX size from offset 8-12 (4 bytes)
            // Matching Python: mdx_size: int = struct.unpack("I", data[8:12])[0]
            uint mdxSize = BitConverter.ToUInt32(data, 8);

            // Extract parsed data starting at offset 12
            // Matching Python: parsed_data: bytearray = bytearray(data[12:])
            byte[] parsedData = new byte[data.Length - 12];
            Array.Copy(data, 12, parsedData, 0, parsedData.Length);

            if (parsedData.Length < 180)
            {
                // Not enough data to have a root node, return original
                return data;
            }

            uint nodeCount;
            uint rootOffset;
            uint childArrayOffset;
            uint childCount;

            using (RawBinaryReader reader = RawBinaryReader.FromBytes(parsedData, 0))
            {
                // Read node count at offset 44 (relative to parsed data)
                // Matching Python: reader.seek(44); node_count: int = reader.read_uint32()
                reader.Seek(44);
                nodeCount = reader.ReadUInt32();

                // Read root offset at offset 168 (relative to parsed data)
                // Matching Python: reader.seek(168); root_offset: int = reader.read_uint32()
                reader.Seek(168);
                rootOffset = reader.ReadUInt32();

                if (rootOffset >= parsedData.Length)
                {
                    // Invalid root offset, return original
                    return data;
                }

                // Read root node header (skip various fields)
                // Matching Python: reader.seek(root_offset); reader.read_uint16(); reader.read_uint16(); reader.read_uint32(); reader.skip(6); reader.skip(4); reader.skip(4); reader.skip(4 * 3); reader.skip(4 * 4);
                reader.Seek((int)rootOffset);
                reader.ReadUInt16(); // Skip first uint16
                reader.ReadUInt16(); // Skip second uint16
                reader.ReadUInt32(); // Skip uint32
                reader.Skip(6); // Skip 6 bytes
                reader.Skip(4); // Skip 4 bytes
                reader.Skip(4); // Skip 4 bytes
                reader.Skip(4 * 3); // Skip 3 floats (12 bytes)
                reader.Skip(4 * 4); // Skip 4 floats (16 bytes)

                // Read child array offset and count at root_offset + 44
                // Matching Python: reader.seek(root_offset + 44); child_array_offset: int = reader.read_uint32(); child_count: int = reader.ReadUInt32()
                reader.Seek((int)(rootOffset + 44));
                childArrayOffset = reader.ReadUInt32();
                childCount = reader.ReadUInt32();
            }

            // If no children, return original data (no transformation needed)
            // Matching Python: if child_count == 0: return parsed_data
            if (childCount == 0)
            {
                return data;
            }

            // Calculate offsets for injected data
            // Matching Python: root_child_array_offset: int = len(parsed_data)
            int rootChildArrayOffset = parsedData.Length;
            // Matching Python: insert_node_offset: int = len(parsed_data) + 4
            int insertNodeOffset = parsedData.Length + 4;
            // Matching Python: insert_controller_offset: int = insert_node_offset + 80
            int insertControllerOffset = insertNodeOffset + 80;
            // Matching Python: insert_controller_data_offset: int = insert_controller_offset + 32
            int insertControllerDataOffset = insertControllerOffset + 32;

            // Increase global node count by 1
            // Matching Python: parsed_data[44:48] = struct.pack("I", node_count + 1)
            byte[] newNodeCountBytes = BitConverter.GetBytes(nodeCount + 1);
            Array.Copy(newNodeCountBytes, 0, parsedData, 44, 4);

            // Update the offset the array of child offsets to our injected array
            // Matching Python: parsed_data[root_offset + 44 : root_offset + 48] = struct.pack("I", root_child_array_offset)
            byte[] newChildArrayOffsetBytes = BitConverter.GetBytes((uint)rootChildArrayOffset);
            Array.Copy(newChildArrayOffsetBytes, 0, parsedData, (int)(rootOffset + 44), 4);

            // Set the root node to have 1 child
            // Matching Python: parsed_data[root_offset + 48 : root_offset + 52] = struct.pack("I", 1)
            // Matching Python: parsed_data[root_offset + 52 : root_offset + 56] = struct.pack("I", 1)
            byte[] oneBytes = BitConverter.GetBytes(1u);
            Array.Copy(oneBytes, 0, parsedData, (int)(rootOffset + 48), 4);
            Array.Copy(oneBytes, 0, parsedData, (int)(rootOffset + 52), 4);

            // Create new byte array with injected data
            // Start with existing parsed data
            List<byte> newParsedData = new List<byte>(parsedData);

            // Populate the injected new root child offsets array
            // It will contain our new node
            // Matching Python: parsed_data += struct.pack("I", insert_node_offset)
            newParsedData.AddRange(BitConverter.GetBytes((uint)insertNodeOffset));

            // Create the new node
            // Matching Python: parsed_data += struct.pack("HHHH II fff ffff III III III", ...)
            // Node structure: 2+2+2+2 bytes (4 ushorts), 4+4 bytes (2 uints), 3 floats, 4 floats, 3 uints, 3 uints, 3 uints
            newParsedData.AddRange(BitConverter.GetBytes((ushort)1)); // Node Type
            newParsedData.AddRange(BitConverter.GetBytes((ushort)(nodeCount + 1))); // Node ID
            newParsedData.AddRange(BitConverter.GetBytes((ushort)1)); // Label ID (steal some existing node's label)
            newParsedData.AddRange(BitConverter.GetBytes((ushort)0)); // Padding
            newParsedData.AddRange(BitConverter.GetBytes(0u)); // Padding uint
            newParsedData.AddRange(BitConverter.GetBytes(rootOffset)); // Parent offset
            newParsedData.AddRange(BitConverter.GetBytes(translation.X)); // Node Position X
            newParsedData.AddRange(BitConverter.GetBytes(translation.Y)); // Node Position Y
            newParsedData.AddRange(BitConverter.GetBytes(translation.Z)); // Node Position Z
            newParsedData.AddRange(BitConverter.GetBytes(orientation.W)); // Node Orientation W
            newParsedData.AddRange(BitConverter.GetBytes(orientation.X)); // Node Orientation X
            newParsedData.AddRange(BitConverter.GetBytes(orientation.Y)); // Node Orientation Y
            newParsedData.AddRange(BitConverter.GetBytes(orientation.Z)); // Node Orientation Z
            newParsedData.AddRange(BitConverter.GetBytes((uint)childArrayOffset)); // Child Array Offset
            newParsedData.AddRange(BitConverter.GetBytes(childCount)); // Child Count
            newParsedData.AddRange(BitConverter.GetBytes(childCount)); // Child Count (duplicate)
            newParsedData.AddRange(BitConverter.GetBytes((uint)insertControllerOffset)); // Controller Array
            newParsedData.AddRange(BitConverter.GetBytes(2u)); // Controller Count
            newParsedData.AddRange(BitConverter.GetBytes(2u)); // Controller Count (duplicate)
            newParsedData.AddRange(BitConverter.GetBytes((uint)insertControllerDataOffset)); // Controller Data Array
            newParsedData.AddRange(BitConverter.GetBytes(9u)); // Controller Data Count
            newParsedData.AddRange(BitConverter.GetBytes(9u)); // Controller Data Count (duplicate)

            // Inject controller and controller data of new node to the end of the file
            // Matching Python: parsed_data += struct.pack("IHHHHBBBB", 8, 0xFFFF, 1, 0, 1, 3, 0, 0, 0)
            newParsedData.AddRange(BitConverter.GetBytes(8u)); // Controller type (position)
            newParsedData.AddRange(BitConverter.GetBytes((ushort)0xFFFF)); // Unknown
            newParsedData.AddRange(BitConverter.GetBytes((ushort)1)); // Unknown
            newParsedData.AddRange(BitConverter.GetBytes((ushort)0)); // Unknown
            newParsedData.AddRange(BitConverter.GetBytes((ushort)1)); // Unknown
            newParsedData.Add((byte)3); // Unknown
            newParsedData.Add((byte)0); // Unknown
            newParsedData.Add((byte)0); // Unknown
            newParsedData.Add((byte)0); // Unknown

            // Matching Python: parsed_data += struct.pack("IHHHHBBBB", 20, 0xFFFF, 1, 4, 5, 4, 0, 0, 0)
            newParsedData.AddRange(BitConverter.GetBytes(20u)); // Controller type (orientation)
            newParsedData.AddRange(BitConverter.GetBytes((ushort)0xFFFF)); // Unknown
            newParsedData.AddRange(BitConverter.GetBytes((ushort)1)); // Unknown
            newParsedData.AddRange(BitConverter.GetBytes((ushort)4)); // Unknown
            newParsedData.AddRange(BitConverter.GetBytes((ushort)5)); // Unknown
            newParsedData.Add((byte)4); // Unknown
            newParsedData.Add((byte)0); // Unknown
            newParsedData.Add((byte)0); // Unknown
            newParsedData.Add((byte)0); // Unknown

            // Matching Python: parsed_data += struct.pack("ffff", 0.0, *translation)
            newParsedData.AddRange(BitConverter.GetBytes(0.0f)); // Time
            newParsedData.AddRange(BitConverter.GetBytes(translation.X)); // Translation X
            newParsedData.AddRange(BitConverter.GetBytes(translation.Y)); // Translation Y
            newParsedData.AddRange(BitConverter.GetBytes(translation.Z)); // Translation Z

            // Matching Python: parsed_data += struct.pack("fffff", 0.0, *orientation)
            newParsedData.AddRange(BitConverter.GetBytes(0.0f)); // Time
            newParsedData.AddRange(BitConverter.GetBytes(orientation.W)); // Orientation W
            newParsedData.AddRange(BitConverter.GetBytes(orientation.X)); // Orientation X
            newParsedData.AddRange(BitConverter.GetBytes(orientation.Y)); // Orientation Y
            newParsedData.AddRange(BitConverter.GetBytes(orientation.Z)); // Orientation Z

            byte[] finalParsedData = newParsedData.ToArray();

            // Return with header prepended
            // Matching Python: return struct.pack("III", 0, len(parsed_data), mdx_size) + parsed_data
            byte[] result = new byte[12 + finalParsedData.Length];
            Array.Copy(BitConverter.GetBytes(0u), 0, result, 0, 4); // Unused (always 0)
            Array.Copy(BitConverter.GetBytes((uint)finalParsedData.Length), 0, result, 4, 4); // MDL size
            Array.Copy(BitConverter.GetBytes(mdxSize), 0, result, 8, 4); // MDX size
            Array.Copy(finalParsedData, 0, result, 12, finalParsedData.Length); // Parsed data

            return result;
        }

        /// <summary>
        /// Creates a quaternion from Euler angles (roll, pitch, yaw).
        /// Matching PyKotor implementation at utility/common/geometry.py:887-914
        /// </summary>
        /// <param name="roll">Rotation around X axis in radians.</param>
        /// <param name="pitch">Rotation around Y axis in radians.</param>
        /// <param name="yaw">Rotation around Z axis in radians.</param>
        /// <returns>A quaternion representing the rotation.</returns>
        private static System.Numerics.Quaternion QuaternionFromEuler(double roll, double pitch, double yaw)
        {
            // Matching Python implementation: Vector4.from_euler
            double qx = Math.Sin(roll / 2) * Math.Cos(pitch / 2) * Math.Cos(yaw / 2) - Math.Cos(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);
            double qy = Math.Cos(roll / 2) * Math.Sin(pitch / 2) * Math.Cos(yaw / 2) + Math.Sin(roll / 2) * Math.Cos(pitch / 2) * Math.Sin(yaw / 2);
            double qz = Math.Cos(roll / 2) * Math.Cos(pitch / 2) * Math.Sin(yaw / 2) - Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Cos(yaw / 2);
            double qw = Math.Cos(roll / 2) * Math.Cos(pitch / 2) * Math.Cos(yaw / 2) + Math.Sin(roll / 2) * Math.Sin(pitch / 2) * Math.Sin(yaw / 2);

            return new System.Numerics.Quaternion((float)qx, (float)qy, (float)qz, (float)qw);
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:305-310
        // Original: def detect_version(data: bytes | bytearray) -> Game:
        /// <summary>
        /// Detects whether an MDL model is K1 or K2 format by checking the geometry root function pointer.
        /// </summary>
        /// <param name="data">The binary MDL data (with 12-byte header).</param>
        /// <returns>True if K2 (TSL), false if K1.</returns>
        private static bool DetectVersion(byte[] data)
        {
            if (data == null || data.Length < 16)
            {
                throw new ArgumentException("Invalid MDL data: must be at least 16 bytes", nameof(data));
            }

            // Read pointer at offset 12-16 (after 12-byte header)
            // Matching Python: pointer: int = struct.unpack("I", data[12:16])[0]
            uint pointer = BitConverter.ToUInt32(data, 12);
            // Matching Python: return BioWareGame.K1 if pointer == _GEOM_ROOT_FP0_K1 else BioWareGame.K2
            return pointer != _GEOM_ROOT_FP0_K1;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:312-375
        // Original: def convert_to_k1(data: bytes | bytearray) -> bytes | bytearray:
        /// <summary>
        /// Converts a K2 (TSL) model to K1 format by updating function pointers and adjusting mesh headers.
        /// </summary>
        /// <param name="data">The binary MDL data (with 12-byte header).</param>
        /// <returns>The converted MDL data in K1 format.</returns>
        public static byte[] ConvertToK1(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length < 12)
            {
                throw new ArgumentException("Invalid MDL data: must be at least 12 bytes", nameof(data));
            }

            // If already K1, return as-is
            // Matching Python: if detect_version(data) == BioWareGame.K1: return data
            if (!DetectVersion(data))
            {
                return data;
            }

            var trim = new List<Tuple<int, int>>(); // List of (node_type, node_offset) tuples

            // First pass: collect all mesh nodes that need trimming
            // Matching Python: with BinaryReader.from_bytes(data, 12) as reader:
            using (RawBinaryReader reader = RawBinaryReader.FromBytes(data, 12))
            {
                reader.Seek(168);
                uint rootOffset = reader.ReadUInt32();

                var nodes = new Stack<uint>();
                nodes.Push(rootOffset);

                while (nodes.Count > 0)
                {
                    uint nodeOffset = nodes.Pop();
                    reader.Seek((int)nodeOffset);
                    ushort nodeType = reader.ReadUInt16();

                    // If this is a mesh node, add it to trim list
                    // Matching Python: if node_type & 32: trim.append((node_type, node_offset))
                    if ((nodeType & _NODE_TYPE_MESH) != 0)
                    {
                        trim.Add(new Tuple<int, int>(nodeType, (int)nodeOffset));
                    }

                    reader.Seek((int)nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek((int)childOffsetsOffset);
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        nodes.Push(reader.ReadUInt32());
                    }
                }
            }

            // Extract header and parsed data
            // Matching Python: start: bytes | bytearray = data[:12]
            byte[] start = new byte[12];
            Array.Copy(data, 0, start, 0, 12);
            // Matching Python: parsed_data: bytearray = bytearray(data[12:])
            byte[] parsedData = new byte[data.Length - 12];
            Array.Copy(data, 12, parsedData, 0, parsedData.Length);

            // Update geometry root function pointers to K1 values
            // Matching Python: parsed_data[:4] = struct.pack("I", _GEOM_ROOT_FP0_K1)
            byte[] fp0Bytes = BitConverter.GetBytes(_GEOM_ROOT_FP0_K1);
            Array.Copy(fp0Bytes, 0, parsedData, 0, 4);
            // Matching Python: parsed_data[4:8] = struct.pack("I", _GEOM_ROOT_FP1_K1)
            byte[] fp1Bytes = BitConverter.GetBytes(_GEOM_ROOT_FP1_K1);
            Array.Copy(fp1Bytes, 0, parsedData, 4, 4);

            // Process each mesh node that needs trimming
            // Matching Python: for node_type, node_offset in trim:
            foreach (var tuple in trim)
            {
                int nodeType = tuple.Item1;
                int nodeOffset = tuple.Item2;
                // Matching Python: mesh_start: int = node_offset + 80  # Start of mesh header
                int meshStart = nodeOffset + 80;

                // Matching Python: offset_start: int = node_offset + 80 + 332  # Location of start of bytes to be shifted
                int offsetStart = nodeOffset + 80 + 332;
                // Matching Python: offset_size: int = 8  # How many bytes we have to shift
                int offsetSize = 8;

                // Handle different node types and update function pointers
                // Matching Python: if node_type & _NODE_TYPE_SKIN:
                if ((nodeType & _NODE_TYPE_SKIN) != 0)
                {
                    offsetSize += _SKIN_HEADER_SIZE;
                    byte[] meshFp0Bytes = BitConverter.GetBytes(_MESH_FP0_K1);
                    Array.Copy(meshFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] meshFp1Bytes = BitConverter.GetBytes(_MESH_FP1_K1);
                    Array.Copy(meshFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }

                // Matching Python: if node_type & _NODE_TYPE_DANGLY:
                if ((nodeType & _NODE_TYPE_DANGLY) != 0)
                {
                    offsetSize += _DANGLY_HEADER_SIZE;
                    byte[] danglyFp0Bytes = BitConverter.GetBytes(_DANGLY_FP0_K1);
                    Array.Copy(danglyFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] danglyFp1Bytes = BitConverter.GetBytes(_DANGLY_FP1_K1);
                    Array.Copy(danglyFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }

                // Matching Python: if node_type & _NODE_TYPE_SABER:
                if ((nodeType & _NODE_TYPE_SABER) != 0)
                {
                    offsetSize += _SABER_HEADER_SIZE;
                    byte[] saberFp0Bytes = BitConverter.GetBytes(_SABER_FP0_K1);
                    Array.Copy(saberFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] saberFp1Bytes = BitConverter.GetBytes(_SABER_FP1_K1);
                    Array.Copy(saberFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }

                // Matching Python: if node_type & _NODE_TYPE_AABB:
                if ((nodeType & _NODE_TYPE_AABB) != 0)
                {
                    offsetSize += _AABB_HEADER_SIZE;
                    byte[] aabbFp0Bytes = BitConverter.GetBytes(_AABB_FP0_K1);
                    Array.Copy(aabbFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] aabbFp1Bytes = BitConverter.GetBytes(_AABB_FP1_K1);
                    Array.Copy(aabbFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }

                // Shift data to remove 8 bytes (K2 has 8 extra bytes in mesh header)
                // Matching Python: shifting: bytearray = parsed_data[offset_start : offset_start + offset_size]
                byte[] shifting = new byte[offsetSize];
                Array.Copy(parsedData, offsetStart, shifting, 0, offsetSize);
                // Matching Python: parsed_data[offset_start - 8 : offset_start - 8 + offset_size] = shifting
                Array.Copy(shifting, 0, parsedData, offsetStart - 8, offsetSize);
            }

            // Reconstruct full data with header
            // Matching Python: return bytes(start + parsed_data)
            byte[] result = new byte[start.Length + parsedData.Length];
            Array.Copy(start, 0, result, 0, start.Length);
            Array.Copy(parsedData, 0, result, start.Length, parsedData.Length);
            return result;
        }

        // Matching PyKotor implementation at Libraries/PyKotor/src/pykotor/tools/model.py:378-635
        // Original: def convert_to_k2(data: bytes | bytearray) -> bytes | bytearray:
        /// <summary>
        /// Converts a K1 model to K2 (TSL) format by updating function pointers and adding extra bytes to mesh headers.
        /// This is a complex operation that requires tracking and updating all offsets in the file.
        /// </summary>
        /// <param name="data">The binary MDL data (with 12-byte header).</param>
        /// <returns>The converted MDL data in K2 format.</returns>
        public static byte[] ConvertToK2(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length < 12)
            {
                throw new ArgumentException("Invalid MDL data: must be at least 12 bytes", nameof(data));
            }

            // If already K2, return as-is
            // Matching Python: if detect_version(data) == BioWareGame.K2: return data
            if (DetectVersion(data))
            {
                return data;
            }

            // Maps offset location to offset value (for tracking all offsets that need updating)
            // Matching Python: offsets: dict[int, int] = {}  # Maps the offset for an offset to its offset
            var offsets = new Dictionary<int, int>();
            // List of mesh node offsets and types
            // Matching Python: mesh_offsets: list[list[int]] = []  # tuple of (Offset to every mesh node, Node type)
            var meshOffsets = new List<Tuple<int, int>>();
            // List of animation offsets
            // Matching Python: anim_offsets: list[int] = []
            var animOffsets = new List<int>();

            // First pass: build dictionary of all offsets in the file
            // Matching Python: with BinaryReader.from_bytes(data, 12) as reader:
            using (RawBinaryReader reader = RawBinaryReader.FromBytes(data, 12))
            {
                // Recursive function to traverse nodes and collect offsets
                // Matching Python: def node_recursive(offset_to_root_offset: int) -> None:
                Action<int> nodeRecursive = null;
                nodeRecursive = (offsetToRootOffset) =>
                {
                    // Matching Python: nodes: list[int] = [offset_to_root_offset]
                    // Use a mutable container to allow reassignment within the lambda
                    var nodesContainer = new List<int> { offsetToRootOffset };

                    while (nodesContainer.Count > 0)
                    {
                        // Matching Python: offset_to_node_offset: int = nodes.pop()
                        int offsetToNodeOffset = nodesContainer[nodesContainer.Count - 1];
                        nodesContainer.RemoveAt(nodesContainer.Count - 1);
                        reader.Seek(offsetToNodeOffset);
                        int nodeOffset = (int)reader.ReadUInt32();
                        // Matching Python: offsets[offset_to_node_offset] = node_offset
                        offsets[offsetToNodeOffset] = nodeOffset;

                        reader.Seek(nodeOffset);
                        ushort nodeType = reader.ReadUInt16();

                        int baseOffset = nodeOffset + 80;

                        // Matching Python: if node_type & _NODE_TYPE_MESH:
                        if ((nodeType & _NODE_TYPE_MESH) != 0)
                        {
                            // Matching Python: mesh_offsets.append([node_offset, node_type])
                            meshOffsets.Add(new Tuple<int, int>(nodeOffset, nodeType));

                            reader.Seek(baseOffset + 8);
                            offsets[baseOffset + 8] = (int)reader.ReadUInt32(); // Face array offset

                            reader.Seek(baseOffset + 176);
                            offsets[baseOffset + 176] = (int)reader.ReadUInt32(); // Vertex indices count array
                            uint indicesArrayCount = reader.ReadUInt32();

                            reader.Seek(baseOffset + 188);
                            offsets[baseOffset + 188] = (int)reader.ReadUInt32(); // Vertex indices locations array
                            if (indicesArrayCount == 1)
                            {
                                int indicesLocationsOffset = offsets[baseOffset + 188];
                                reader.Seek(indicesLocationsOffset);
                                offsets[indicesLocationsOffset] = (int)reader.ReadUInt32(); // Vertex indices array
                            }

                            reader.Seek(baseOffset + 200);
                            offsets[baseOffset + 200] = (int)reader.ReadUInt32(); // Inverted counter array

                            reader.Seek(baseOffset + 328);
                            offsets[baseOffset + 328] = (int)reader.ReadUInt32(); // Vertex array

                            baseOffset += _MESH_HEADER_SIZE_K1;
                        }

                        // Matching Python: if node_type & _NODE_TYPE_LIGHT:
                        if ((nodeType & _NODE_TYPE_LIGHT) != 0)
                        {
                            reader.Seek(baseOffset + 4);
                            offsets[baseOffset + 4] = (int)reader.ReadUInt32(); // Lens flare size array

                            reader.Seek(baseOffset + 16);
                            offsets[baseOffset + 16] = (int)reader.ReadUInt32(); // Lens flare size array

                            reader.Seek(baseOffset + 28);
                            offsets[baseOffset + 28] = (int)reader.ReadUInt32(); // Lens flare positions array

                            reader.Seek(baseOffset + 40);
                            offsets[baseOffset + 40] = (int)reader.ReadUInt32(); // Flare colour shifts array

                            reader.Seek(baseOffset + 52);
                            offsets[baseOffset + 52] = (int)reader.ReadUInt32(); // Flare texture names offsets array
                            uint textureCount = reader.ReadUInt32();

                            for (uint i = 0; i < textureCount; i++)
                            {
                                reader.Seek(offsets[baseOffset + 52] + (int)(i * 4));
                                offsets[offsets[baseOffset + 52] + (int)(i * 4)] = (int)reader.ReadUInt32();
                            }
                        }

                        // Matching Python: if node_type & _NODE_TYPE_EMITTER:
                        // No offsets to track for emitters

                        // Matching Python: if node_type & _NODE_TYPE_SKIN:
                        if ((nodeType & _NODE_TYPE_SKIN) != 0)
                        {
                            reader.Seek(baseOffset + 20);
                            offsets[baseOffset + 20] = (int)reader.ReadUInt32(); // Bone map array

                            reader.Seek(baseOffset + 28);
                            offsets[baseOffset + 28] = (int)reader.ReadUInt32(); // QBones array

                            reader.Seek(baseOffset + 40);
                            offsets[baseOffset + 40] = (int)reader.ReadUInt32(); // TBones Array

                            reader.Seek(baseOffset + 52);
                            offsets[baseOffset + 52] = (int)reader.ReadUInt32(); // Array8

                            baseOffset += _SKIN_HEADER_SIZE;
                        }

                        // Matching Python: if node_type & _NODE_TYPE_DANGLY:
                        if ((nodeType & _NODE_TYPE_DANGLY) != 0)
                        {
                            reader.Seek(baseOffset + 0);
                            offsets[baseOffset + 0] = (int)reader.ReadUInt32(); // Dangly constraint array

                            reader.Seek(baseOffset + 24);
                            offsets[baseOffset + 24] = (int)reader.ReadUInt32(); // Unknown

                            baseOffset += _DANGLY_HEADER_SIZE;
                        }

                        // Matching Python: if node_type & _NODE_TYPE_AABB:
                        if ((nodeType & _NODE_TYPE_AABB) != 0)
                        {
                            reader.Seek(baseOffset + 0);
                            offsets[baseOffset + 0] = (int)reader.ReadUInt32(); // AABB root node

                            var aabbs = new Stack<int>();
                            aabbs.Push(offsets[baseOffset + 0]);

                            while (aabbs.Count > 0)
                            {
                                int aabb = aabbs.Pop();

                                reader.Seek(aabb + 24);
                                int leaf0 = (int)reader.ReadUInt32();
                                if (leaf0 != 0)
                                {
                                    aabbs.Push(leaf0);
                                    offsets[aabb + 24] = leaf0; // AABB child node
                                }

                                reader.Seek(aabb + 28);
                                int leaf1 = (int)reader.ReadUInt32();
                                if (leaf1 != 0)
                                {
                                    aabbs.Push(leaf1);
                                    offsets[aabb + 28] = leaf1; // AABB child node
                                }
                            }

                            baseOffset += _AABB_HEADER_SIZE;
                        }

                        // Matching Python: if node_type & _NODE_TYPE_SABER:
                        if ((nodeType & _NODE_TYPE_SABER) != 0)
                        {
                            reader.Seek(baseOffset + 0);
                            offsets[baseOffset + 0] = (int)reader.ReadUInt32(); // Saber Verts array

                            reader.Seek(baseOffset + 4);
                            offsets[baseOffset + 4] = (int)reader.ReadUInt32(); // Saber UVs array

                            reader.Seek(baseOffset + 8);
                            offsets[baseOffset + 8] = (int)reader.ReadUInt32(); // Saber Normals array

                            baseOffset += _SABER_HEADER_SIZE;
                        }

                        reader.Seek(nodeOffset + 8);
                        offsets[nodeOffset + 8] = (int)reader.ReadUInt32(); // Geometry header

                        reader.Seek(nodeOffset + 12);
                        offsets[nodeOffset + 12] = (int)reader.ReadUInt32(); // Parent node

                        reader.Seek(nodeOffset + 56);
                        offsets[nodeOffset + 56] = (int)reader.ReadUInt32(); // Controller array offset

                        reader.Seek(nodeOffset + 68);
                        offsets[nodeOffset + 68] = (int)reader.ReadUInt32(); // Controller data offset

                        reader.Seek(nodeOffset + 44);
                        int childOffsetsOffset = (int)reader.ReadUInt32();
                        int childOffsetsCount = (int)reader.ReadUInt32();
                        offsets[nodeOffset + 44] = childOffsetsOffset; // Child node offsets array

                        // Matching Python: nodes = [child_offsets_offset + i * 4 for i in range(child_offsets_count)]
                        // Matching Python: nodes.insert(0, offset_to_root_offset)
                        // Note: The Python code replaces the nodes list and adds the current offset back
                        // This continues the while loop to process child nodes
                        nodesContainer.Clear();
                        for (int i = 0; i < childOffsetsCount; i++)
                        {
                            nodesContainer.Add(childOffsetsOffset + i * 4);
                        }
                        nodesContainer.Insert(0, offsetToRootOffset); // Add current offset back to continue processing
                    }
                };

                // Process animations
                // Matching Python: reader.seek(88)
                reader.Seek(88);
                int animLocationsOffset = (int)reader.ReadUInt32();
                int animCount = (int)reader.ReadUInt32();

                reader.Seek(168);
                reader.ReadUInt32();
                reader.ReadUInt32();

                reader.Seek(184);
                reader.ReadUInt32();
                int nameCount = (int)reader.ReadUInt32();

                reader.Seek(40);
                offsets[40] = (int)reader.ReadUInt32(); // Root node

                reader.Seek(88);
                offsets[88] = (int)reader.ReadUInt32(); // Animation array

                reader.Seek(168);
                offsets[168] = (int)reader.ReadUInt32(); // Head root

                reader.Seek(184);
                offsets[184] = (int)reader.ReadUInt32(); // Name offsets array

                for (int i = 0; i < nameCount; i++)
                {
                    reader.Seek(offsets[184] + i * 4);
                    offsets[offsets[184] + i * 4] = (int)reader.ReadUInt32(); // Name
                }

                for (int i = 0; i < animCount; i++)
                {
                    reader.Seek(animLocationsOffset + i * 4);
                    int animationOffset = (int)reader.ReadUInt32();
                    offsets[animLocationsOffset + i * 4] = animationOffset; // Offset to event
                    animOffsets.Add(animationOffset);

                    reader.Seek(animationOffset + 120);
                    offsets[animationOffset + 120] = (int)reader.ReadUInt32(); // Offset to event array

                    reader.Seek(animationOffset + 40);
                    nodeRecursive(animationOffset + 40);
                }

                // Process root node
                nodeRecursive(168);
            }

            // Second pass: update function pointers to K2 values
            // Matching Python: mdx_size: bytes | bytearray = data[8:12]
            byte[] mdxSizeBytes = new byte[4];
            Array.Copy(data, 8, mdxSizeBytes, 0, 4);
            // Matching Python: parsed_data: bytearray = bytearray(data[12:])
            byte[] parsedData = new byte[data.Length - 12];
            Array.Copy(data, 12, parsedData, 0, parsedData.Length);

            // Update geometry root function pointers
            // Matching Python: parsed_data[:4] = struct.pack("I", _GEOM_ROOT_FP0_K2)
            byte[] fp0Bytes = BitConverter.GetBytes(_GEOM_ROOT_FP0_K2);
            Array.Copy(fp0Bytes, 0, parsedData, 0, 4);
            // Matching Python: parsed_data[4:8] = struct.pack("I", _GEOM_ROOT_FP1_K2)
            byte[] fp1Bytes = BitConverter.GetBytes(_GEOM_ROOT_FP1_K2);
            Array.Copy(fp1Bytes, 0, parsedData, 4, 4);

            // Update animation function pointers
            // Matching Python: for anim_offset in anim_offsets:
            foreach (int animOffset in animOffsets)
            {
                byte[] animFp0Bytes = BitConverter.GetBytes(_GEOM_ANIM_FP0_K2);
                Array.Copy(animFp0Bytes, 0, parsedData, animOffset, 4);
                byte[] animFp1Bytes = BitConverter.GetBytes(_GEOM_ANIM_FP1_K2);
                Array.Copy(animFp1Bytes, 0, parsedData, animOffset + 4, 4);
            }

            // Update mesh function pointers
            // Matching Python: for node_offset, node_type in mesh_offsets:
            foreach (var tuple in meshOffsets)
            {
                int nodeOffset = tuple.Item1;
                int nodeType = tuple.Item2;
                // Matching Python: mesh_start: int = node_offset + 80  # Start of mesh header
                int meshStart = nodeOffset + 80;

                // Matching Python: if node_type & _NODE_TYPE_SKIN:
                if ((nodeType & _NODE_TYPE_SKIN) != 0)
                {
                    byte[] meshFp0Bytes = BitConverter.GetBytes(_MESH_FP0_K2);
                    Array.Copy(meshFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] meshFp1Bytes = BitConverter.GetBytes(_MESH_FP1_K2);
                    Array.Copy(meshFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }

                // Matching Python: if node_type & _NODE_TYPE_DANGLY:
                if ((nodeType & _NODE_TYPE_DANGLY) != 0)
                {
                    byte[] danglyFp0Bytes = BitConverter.GetBytes(_DANGLY_FP0_K2);
                    Array.Copy(danglyFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] danglyFp1Bytes = BitConverter.GetBytes(_DANGLY_FP1_K2);
                    Array.Copy(danglyFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }

                // Matching Python: if node_type & _NODE_TYPE_SABER:
                if ((nodeType & _NODE_TYPE_SABER) != 0)
                {
                    byte[] saberFp0Bytes = BitConverter.GetBytes(_SABER_FP0_K1); // Note: K1 value for FP0
                    Array.Copy(saberFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] saberFp1Bytes = BitConverter.GetBytes(_SABER_FP1_K2);
                    Array.Copy(saberFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }

                // Matching Python: if node_type & _NODE_TYPE_AABB:
                if ((nodeType & _NODE_TYPE_AABB) != 0)
                {
                    byte[] aabbFp0Bytes = BitConverter.GetBytes(_AABB_FP0_K2);
                    Array.Copy(aabbFp0Bytes, 0, parsedData, meshStart, 4);
                    byte[] aabbFp1Bytes = BitConverter.GetBytes(_AABB_FP1_K2);
                    Array.Copy(aabbFp1Bytes, 0, parsedData, meshStart + 4, 4);
                }
            }

            // Sort offsets in reverse order for processing
            // Matching Python: offsets = dict(sorted(offsets.items(), reverse=True))
            var sortedOffsets = new List<KeyValuePair<int, int>>(offsets);
            sortedOffsets.Sort((a, b) => b.Key.CompareTo(a.Key));

            // Third pass: add 8 extra bytes to each mesh header and update all offsets
            // Matching Python: shifted: int = 0
            int shifted = 0;
            // Matching Python: for i in range(len(mesh_offsets)):
            for (int i = 0; i < meshOffsets.Count; i++)
            {
                var tuple = meshOffsets[i];
                int nodeOffset = tuple.Item1;
                // Matching Python: insert_location: int = node_offset + 80 + 324
                int insertLocation = nodeOffset + 80 + 324;

                // Insert 8 zero bytes at insert location
                // Matching Python: parsed_data = bytearray(parsed_data[:insert_location] + bytes([0] * 8) + parsed_data[insert_location:])
                byte[] beforeInsert = new byte[insertLocation];
                Array.Copy(parsedData, 0, beforeInsert, 0, insertLocation);
                byte[] insertBytes = new byte[8]; // 8 zero bytes
                byte[] afterInsert = new byte[parsedData.Length - insertLocation];
                Array.Copy(parsedData, insertLocation, afterInsert, 0, afterInsert.Length);
                byte[] newParsedData = new byte[beforeInsert.Length + insertBytes.Length + afterInsert.Length];
                Array.Copy(beforeInsert, 0, newParsedData, 0, beforeInsert.Length);
                Array.Copy(insertBytes, 0, newParsedData, beforeInsert.Length, insertBytes.Length);
                Array.Copy(afterInsert, 0, newParsedData, beforeInsert.Length + insertBytes.Length, afterInsert.Length);
                parsedData = newParsedData;

                // Update offsets that come after the insert location
                // Matching Python: for offset_location, offset_value in deepcopy(offsets).items():
                var offsetsCopy = new Dictionary<int, int>(offsets);
                foreach (var kvp in offsetsCopy)
                {
                    int offsetLocation = kvp.Key;
                    int offsetValue = kvp.Value;

                    // Matching Python: if insert_location < offset_location:
                    if (insertLocation < offsetLocation)
                    {
                        offsets.Remove(offsetLocation);
                        // Matching Python: if offset_location + 8 in offsets: raise ValueError("whoops")
                        if (offsets.ContainsKey(offsetLocation + 8))
                        {
                            throw new InvalidOperationException("Offset collision detected during K2 conversion");
                        }
                        offsets[offsetLocation + 8] = offsetValue;
                    }
                }

                // Update offset values that point to data after insert location
                // Matching Python: for offset_location, offset_value in deepcopy(offsets).items():
                var offsetsCopy2 = new Dictionary<int, int>(offsets);
                foreach (var kvp in offsetsCopy2)
                {
                    int offsetLocation = kvp.Key;
                    int offsetValue = kvp.Value;

                    // Matching Python: if insert_location < offset_value:
                    if (insertLocation < offsetValue)
                    {
                        offsets[offsetLocation] = offsetValue + 8;
                    }
                }

                // Update mesh offsets that come after insert location
                // Matching Python: for j in range(len(mesh_offsets)):
                for (int j = 0; j < meshOffsets.Count; j++)
                {
                    var meshTuple = meshOffsets[j];
                    // Matching Python: if insert_location < mesh_offsets[j][0]:
                    if (insertLocation < meshTuple.Item1)
                    {
                        meshOffsets[j] = new Tuple<int, int>(meshTuple.Item1 + 8, meshTuple.Item2);
                    }
                }

                shifted += 8;
            }

            // Final pass: update all offsets in the file
            // Matching Python: for offset_location, offset_value in offsets.items():
            foreach (var kvp in offsets)
            {
                int offsetLocation = kvp.Key;
                int offsetValue = kvp.Value;
                // Matching Python: parsed_data[offset_location : offset_location + 4] = struct.pack("I", offset_value)
                byte[] offsetBytes = BitConverter.GetBytes((uint)offsetValue);
                Array.Copy(offsetBytes, 0, parsedData, offsetLocation, 4);
            }

            // Reconstruct full data with header
            // Matching Python: return struct.pack("I", 0) + struct.pack("I", len(parsed_data)) + mdx_size + parsed_data
            byte[] result = new byte[4 + 4 + 4 + parsedData.Length];
            Array.Copy(BitConverter.GetBytes(0u), 0, result, 0, 4); // Unused (always 0)
            Array.Copy(BitConverter.GetBytes((uint)parsedData.Length), 0, result, 4, 4); // MDL size
            Array.Copy(mdxSizeBytes, 0, result, 8, 4); // MDX size
            Array.Copy(parsedData, 0, result, 12, parsedData.Length); // Parsed data
            return result;
        }
    }
}
