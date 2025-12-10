using System.Collections.Generic;
using System.Linq;
using CSharpKOTOR.Common;
using JetBrains.Annotations;

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
                    reader.Seek(nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek(nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek(childOffsetsOffset);
                    for (uint i = 0; i < childOffsetsCount; i++)
                    {
                        nodes.Enqueue(reader.ReadUInt32());
                    }

                    if ((nodeId & 32) != 0)
                    {
                        // Extract texture name
                        reader.Seek(nodeOffset + 168);
                        string name = reader.ReadString(32, encoding: "ascii", errors: "ignore").Trim().ToLower();
                        if (!string.IsNullOrEmpty(name) && name != "null" && !seenNames.Contains(name) && name != "dirt")
                        {
                            seenNames.Add(name);
                            yield return name;
                        }

                        // Extract lightmap name
                        reader.Seek(nodeOffset + 200);
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
                    reader.Seek(nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek(nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek(childOffsetsOffset);
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
                        reader.Seek(nodeOffset + 168);
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
                    reader.Seek(nodeOffset);
                    uint nodeId = reader.ReadUInt32();

                    reader.Seek(nodeOffset + 44);
                    uint childOffsetsOffset = reader.ReadUInt32();
                    uint childOffsetsCount = reader.ReadUInt32();

                    reader.Seek(childOffsetsOffset);
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
                        reader.Seek(nodeOffset + 200);
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
    }
}
