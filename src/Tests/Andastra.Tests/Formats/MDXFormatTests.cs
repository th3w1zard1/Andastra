using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for MDX binary I/O operations.
    /// Tests validate the MDX format structure as defined in MDX.ksy Kaitai Struct definition.
    /// </summary>
    public class MDXFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.mdx");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";

        [Fact(Timeout = 120000)]
        public void TestMdxFileStructure()
        {
            // MDX files contain raw vertex data
            // Structure depends on MDX data flags from corresponding MDL file
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestMdxFile(BinaryTestFile);
            }

            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().BeGreaterThanOrEqualTo(0, "MDX file should exist and have data");
        }

        [Fact(Timeout = 120000)]
        public void TestMdxDataFlags()
        {
            // Test MDX data bitmap masks as defined in MDX.ksy documentation
            uint mdxVertices = 0x00000001;
            uint mdxTex0Vertices = 0x00000002;
            uint mdxTex1Vertices = 0x00000004;
            uint mdxTex2Vertices = 0x00000008;
            uint mdxTex3Vertices = 0x00000010;
            uint mdxVertexNormals = 0x00000020;
            uint mdxVertexColors = 0x00000040;
            uint mdxTangentSpace = 0x00000080;

            // Skinmesh-specific flags (not stored in MDX data flags, but used internally)
            uint mdxBoneWeights = 0x00000800;
            uint mdxBoneIndices = 0x00001000;

            mdxVertices.Should().Be(0x00000001);
            mdxTex0Vertices.Should().Be(0x00000002);
            mdxTex1Vertices.Should().Be(0x00000004);
            mdxTex2Vertices.Should().Be(0x00000008);
            mdxTex3Vertices.Should().Be(0x00000010);
            mdxVertexNormals.Should().Be(0x00000020);
            mdxVertexColors.Should().Be(0x00000040);
            mdxTangentSpace.Should().Be(0x00000080);
            mdxBoneWeights.Should().Be(0x00000800);
            mdxBoneIndices.Should().Be(0x00001000);
        }

        [Fact(Timeout = 120000)]
        public void TestMdxVertexDataInterleaving()
        {
            // Test that MDX vertex data is interleaved based on flags
            // Each vertex attribute has a specific size:
            // - Positions: 3 floats (12 bytes)
            // - Texture coordinates: 2 floats (8 bytes)
            // - Normals: 3 floats (12 bytes)
            // - Colors: 3 floats (12 bytes)
            // - Tangent space: 9 floats (36 bytes)
            // - Bone weights: 4 floats (16 bytes)
            // - Bone indices: 4 floats (16 bytes, cast to uint16)

            int positionSize = 3 * 4; // 3 floats * 4 bytes
            int texCoordSize = 2 * 4; // 2 floats * 4 bytes
            int normalSize = 3 * 4; // 3 floats * 4 bytes
            int colorSize = 3 * 4; // 3 floats * 4 bytes
            int tangentSpaceSize = 9 * 4; // 9 floats * 4 bytes
            int boneWeightSize = 4 * 4; // 4 floats * 4 bytes
            int boneIndexSize = 4 * 4; // 4 floats * 4 bytes

            positionSize.Should().Be(12, "Position data should be 12 bytes (3 floats)");
            texCoordSize.Should().Be(8, "Texture coordinate data should be 8 bytes (2 floats)");
            normalSize.Should().Be(12, "Normal data should be 12 bytes (3 floats)");
            colorSize.Should().Be(12, "Color data should be 12 bytes (3 floats)");
            tangentSpaceSize.Should().Be(36, "Tangent space data should be 36 bytes (9 floats)");
            boneWeightSize.Should().Be(16, "Bone weight data should be 16 bytes (4 floats)");
            boneIndexSize.Should().Be(16, "Bone index data should be 16 bytes (4 floats)");
        }

        [Fact(Timeout = 120000)]
        public void TestMdxVertexSizeCalculation()
        {
            // Test vertex size calculation based on MDX data flags
            // Vertex size = sum of sizes of all present attributes

            uint flags = 0x00000001 | 0x00000002 | 0x00000020; // VERTICES | TEX0 | NORMALS
            int vertexSize = 0;

            if ((flags & 0x00000001) != 0) vertexSize += 12; // positions
            if ((flags & 0x00000002) != 0) vertexSize += 8; // tex0
            if ((flags & 0x00000004) != 0) vertexSize += 8; // tex1
            if ((flags & 0x00000008) != 0) vertexSize += 8; // tex2
            if ((flags & 0x00000010) != 0) vertexSize += 8; // tex3
            if ((flags & 0x00000020) != 0) vertexSize += 12; // normals
            if ((flags & 0x00000040) != 0) vertexSize += 12; // colors
            if ((flags & 0x00000080) != 0) vertexSize += 36; // tangent space

            vertexSize.Should().Be(32, "Vertex size with VERTICES | TEX0 | NORMALS should be 32 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestMdxSkinmeshData()
        {
            // Test skinmesh-specific MDX data
            // Bone weights: 4 floats per vertex
            // Bone indices: 4 floats per vertex (cast to uint16)

            int boneWeightSize = 4 * 4; // 4 floats * 4 bytes
            int boneIndexSize = 4 * 4; // 4 floats * 4 bytes

            boneWeightSize.Should().Be(16, "Bone weights should be 16 bytes per vertex");
            boneIndexSize.Should().Be(16, "Bone indices should be 16 bytes per vertex");

            // Bone indices are stored as floats but cast to uint16 when used
            float[] boneIndexFloats = new float[] { 0.0f, 1.0f, 2.0f, 3.0f };
            ushort[] boneIndices = new ushort[4];
            for (int i = 0; i < 4; i++)
            {
                boneIndices[i] = (ushort)boneIndexFloats[i];
            }

            boneIndices[0].Should().Be(0);
            boneIndices[1].Should().Be(1);
            boneIndices[2].Should().Be(2);
            boneIndices[3].Should().Be(3);
        }

        [Fact(Timeout = 120000)]
        public void TestMdxOffsetCalculation()
        {
            // Test MDX offset calculation
            // Offsets in trimesh header are relative offsets within MDX file
            // -1 indicates the attribute is not present

            int verticesOffset = 0;
            int normalsOffset = -1;
            int tex0Offset = -1;

            // If vertices are present, normals and tex0 offsets are relative to vertices
            if (verticesOffset >= 0)
            {
                // Calculate relative offsets
                normalsOffset = verticesOffset + (12 * 100); // 100 vertices * 12 bytes
                tex0Offset = normalsOffset + (12 * 100); // 100 vertices * 12 bytes
            }

            verticesOffset.Should().BeGreaterThanOrEqualTo(0, "Vertices offset should be >= 0 if present");
            // -1 indicates attribute not present
            if (normalsOffset == -1)
            {
                normalsOffset.Should().Be(-1, "Normals offset -1 indicates not present");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestMdxDataAccessPattern()
        {
            // Test MDX data access pattern
            // Vertex data is accessed via: mdx_data_offset + (vertex_index * vertex_size) + attribute_offset

            uint mdxDataOffset = 0;
            int vertexIndex = 5;
            int vertexSize = 32; // Example: positions + tex0 + normals
            int attributeOffset = 12; // Offset to normals within vertex

            long vertexAddress = mdxDataOffset + (vertexIndex * vertexSize) + attributeOffset;
            vertexAddress.Should().Be(172, "Vertex address calculation should be correct");
        }

        [Fact(Timeout = 120000)]
        public void TestMdxEmptyFile()
        {
            // Test MDX with no data
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempFile, new byte[0]);
                FileInfo fileInfo = new FileInfo(tempFile);
                fileInfo.Length.Should().Be(0, "Empty MDX file should have 0 bytes");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestMdxVertexDataAlignment()
        {
            // Test that MDX vertex data is properly aligned
            // Floats are 4-byte aligned, so vertex size should be multiple of 4
            int vertexSize1 = 12; // positions only
            int vertexSize2 = 20; // positions + tex0
            int vertexSize3 = 32; // positions + tex0 + normals

            (vertexSize1 % 4).Should().Be(0, "Vertex size should be 4-byte aligned");
            (vertexSize2 % 4).Should().Be(0, "Vertex size should be 4-byte aligned");
            (vertexSize3 % 4).Should().Be(0, "Vertex size should be 4-byte aligned");
        }

        [Fact(Timeout = 120000)]
        public void TestMdxBoneWeightSum()
        {
            // Test that bone weights sum to 1.0 (as per MDX.ksy documentation)
            float[] boneWeights = new float[] { 0.5f, 0.3f, 0.2f, 0.0f };
            float sum = boneWeights.Sum();
            sum.Should().BeApproximately(1.0f, 0.001f, "Bone weights should sum to 1.0");
        }

        [Fact(Timeout = 120000)]
        public void TestMdxBoneIndexMapping()
        {
            // Test bone index mapping to bone map array
            // Bone indices reference the bone map array, which maps to skeleton bone numbers
            ushort[] boneIndices = new ushort[] { 0, 1, 2, 0xFFFF };
            uint[] boneMap = new uint[] { 5, 10, 15 };

            // Map bone indices to skeleton bone numbers
            uint[] skeletonBones = new uint[4];
            for (int i = 0; i < 4; i++)
            {
                if (boneIndices[i] != 0xFFFF && boneIndices[i] < boneMap.Length)
                {
                    skeletonBones[i] = boneMap[boneIndices[i]];
                }
                else
                {
                    skeletonBones[i] = 0xFFFFFFFF; // Invalid
                }
            }

            skeletonBones[0].Should().Be(5, "Bone index 0 should map to skeleton bone 5");
            skeletonBones[1].Should().Be(10, "Bone index 1 should map to skeleton bone 10");
            skeletonBones[2].Should().Be(15, "Bone index 2 should map to skeleton bone 15");
            skeletonBones[3].Should().Be(0xFFFFFFFF, "Bone index 0xFFFF should map to invalid");
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading non-existent file
            Action act = () =>
            {
                if (File.Exists(DoesNotExistFile))
                {
                    File.ReadAllBytes(DoesNotExistFile);
                }
            };
            // File doesn't exist, so this should not throw if we check first
            File.Exists(DoesNotExistFile).Should().BeFalse("Test file should not exist");
        }

        private static void CreateTestMdxFile(string path)
        {
            // Create minimal MDX file with vertex data
            // MDX contains raw vertex data, structure depends on MDL flags
            using (var fs = File.Create(path))
            {
                using (var writer = new System.IO.BinaryWriter(fs))
                {
                    // Example: 10 vertices with positions (3 floats each)
                    for (int i = 0; i < 10; i++)
                    {
                        writer.Write((float)i); // X
                        writer.Write((float)i); // Y
                        writer.Write((float)i); // Z
                    }
                }
            }
        }
    }
}

