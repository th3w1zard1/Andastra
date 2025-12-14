using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;
using JetBrains.Annotations;
using BinaryReader = CSharpKOTOR.Common.RawBinaryReader;

namespace CSharpKOTOR.Tools
{
    /// <summary>
    /// Utility functions for working with 3D model data.
    /// </summary>
    [PublicAPI]
    public static class ModelTools
    {
        /// <summary>
        /// Extracts texture and lightmap names from MDL model data.
        /// </summary>
        /// <param name="data">The binary MDL data.</param>
        /// <returns>An enumerable of texture and lightmap names.</returns>
        public static IEnumerable<string> IterateTexturesAndLightmaps(byte[] data)
        {
            HashSet<string> seenNames = new HashSet<string>();

            using (BinaryReader reader = BinaryReader.FromBytes(data, 12))
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

            using (BinaryReader reader = BinaryReader.FromBytes(data, 12))
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

            using (BinaryReader reader = BinaryReader.FromBytes(data, 12))
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

            using (BinaryReader reader = BinaryReader.FromBytes(parsedData, 12))
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

            using (BinaryReader reader = BinaryReader.FromBytes(parsedData, 12))
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
    }
}
