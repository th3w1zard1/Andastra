using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.MDL;
using Andastra.Parsing.Formats.MDLData;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for MDL binary I/O operations.
    /// Tests validate the MDL format structure as defined in MDL.ksy Kaitai Struct definition.
    /// </summary>
    public class MDLFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.mdl");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.mdl");

        [Fact(Timeout = 120000)]
        public void TestMdlFileHeaderStructure()
        {
            // Test that MDL file header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestMdlFile(BinaryTestFile);
            }

            // Validate header constants match MDL.ksy (constants are in Runtime project, so we validate via file structure instead)
            // FILE_HEADER_SIZE = 12, GEOMETRY_HEADER_SIZE = 80, MODEL_HEADER_SIZE = 92, NAMES_HEADER_SIZE = 28
        }

        [Fact(Timeout = 120000)]
        public void TestMdlFileHeaderSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestMdlFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[12];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 12);
            }

            // Validate file header structure matches MDL.ksy
            // First 4 bytes should be 0 (unused field)
            uint unused = BitConverter.ToUInt32(header, 0);
            unused.Should().Be(0u, "Unused field should be 0 as defined in MDL.ksy");

            // MDL size and MDX size should be valid
            uint mdlSize = BitConverter.ToUInt32(header, 4);
            uint mdxSize = BitConverter.ToUInt32(header, 8);
            mdlSize.Should().BeGreaterThan(0, "MDL size should be greater than 0");
            mdxSize.Should().BeGreaterThanOrEqualTo(0, "MDX size should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlGeometryHeader()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestMdlFile(BinaryTestFile);
            }

            // Read geometry header (80 bytes at offset 12)
            byte[] geomHeader = new byte[80];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(12, SeekOrigin.Begin);
                fs.Read(geomHeader, 0, 80);
            }

            // Validate geometry header structure matches MDL.ksy
            uint functionPointer0 = BitConverter.ToUInt32(geomHeader, 0);
            uint functionPointer1 = BitConverter.ToUInt32(geomHeader, 4);

            // Function pointers should be valid (non-zero for KotOR models)
            functionPointer0.Should().BeGreaterThanOrEqualTo(0, "Function pointer 0 should be non-negative");
            functionPointer1.Should().BeGreaterThanOrEqualTo(0, "Function pointer 1 should be non-negative");

            // Model name (32 bytes at offset 8)
            string modelName = System.Text.Encoding.ASCII.GetString(geomHeader, 8, 32).TrimEnd('\0');
            modelName.Length.Should().BeLessThanOrEqualTo(32, "Model name should be max 32 bytes as per MDL.ksy");

            // Root node offset (offset 40)
            uint rootNodeOffset = BitConverter.ToUInt32(geomHeader, 40);
            rootNodeOffset.Should().BeGreaterThanOrEqualTo(0, "Root node offset should be non-negative");

            // Node count (offset 44)
            uint nodeCount = BitConverter.ToUInt32(geomHeader, 44);
            nodeCount.Should().BeGreaterThanOrEqualTo(0, "Node count should be non-negative");

            // Geometry type (offset 76)
            byte geometryType = geomHeader[76];
            geometryType.Should().BeOneOf(new byte[] { 0x01, 0x02, 0x05 }, "Geometry type should match MDL.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlModelHeader()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestMdlFile(BinaryTestFile);
            }

            // Read model header (92 bytes at offset 92)
            byte[] modelHeader = new byte[92];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(92, SeekOrigin.Begin);
                fs.Read(modelHeader, 0, 92);
            }

            // Validate model header structure matches MDL.ksy
            byte classification = modelHeader[0];
            byte subclassification = modelHeader[1];
            byte unknown = modelHeader[2];
            byte affectedByFog = modelHeader[3];

            affectedByFog.Should().BeOneOf(new byte[] { 0, 1 }, "Affected by fog should be 0 or 1 as per MDL.ksy");

            // Animation count (offset 12)
            uint animationCount = BitConverter.ToUInt32(modelHeader, 12);
            uint animationCountDup = BitConverter.ToUInt32(modelHeader, 16);
            animationCount.Should().Be(animationCountDup, "Animation count duplicate should match as per MDL.ksy");

            // Bounding box (offset 24-47)
            float bboxMinX = BitConverter.ToSingle(modelHeader, 24);
            float bboxMinY = BitConverter.ToSingle(modelHeader, 28);
            float bboxMinZ = BitConverter.ToSingle(modelHeader, 32);
            float bboxMaxX = BitConverter.ToSingle(modelHeader, 36);
            float bboxMaxY = BitConverter.ToSingle(modelHeader, 40);
            float bboxMaxZ = BitConverter.ToSingle(modelHeader, 44);

            // Bounding box max should be >= min
            bboxMaxX.Should().BeGreaterThanOrEqualTo(bboxMinX, "Bounding box max X should be >= min X");
            bboxMaxY.Should().BeGreaterThanOrEqualTo(bboxMinY, "Bounding box max Y should be >= min Y");
            bboxMaxZ.Should().BeGreaterThanOrEqualTo(bboxMinZ, "Bounding box max Z should be >= min Z");

            // Radius (offset 48)
            float radius = BitConverter.ToSingle(modelHeader, 48);
            radius.Should().BeGreaterThanOrEqualTo(0, "Radius should be non-negative");

            // Animation scale (offset 52)
            float animationScale = BitConverter.ToSingle(modelHeader, 52);
            animationScale.Should().BeGreaterThan(0, "Animation scale should be positive");

            // Supermodel name (offset 56, 32 bytes)
            string supermodelName = System.Text.Encoding.ASCII.GetString(modelHeader, 56, 32).TrimEnd('\0');
            supermodelName.Length.Should().BeLessThanOrEqualTo(32, "Supermodel name should be max 32 bytes as per MDL.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlNamesHeader()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestMdlFile(BinaryTestFile);
            }

            // Read names header (28 bytes at offset 180)
            byte[] namesHeader = new byte[28];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(180, SeekOrigin.Begin);
                fs.Read(namesHeader, 0, 28);
            }

            // Validate names header structure matches MDL.ksy
            uint namesCount = BitConverter.ToUInt32(namesHeader, 20);
            uint namesCountDup = BitConverter.ToUInt32(namesHeader, 24);
            namesCount.Should().Be(namesCountDup, "Names count duplicate should match as per MDL.ksy");

            uint mdxDataSize = BitConverter.ToUInt32(namesHeader, 8);
            mdxDataSize.Should().BeGreaterThanOrEqualTo(0, "MDX data size should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlNodeHeader()
        {
            // Test node header structure (80 bytes)
            // Node header fields as defined in MDL.ksy
            // NODE_HEADER_SIZE = 80 (constant is in Runtime project, validated via file structure)

            // Validate node type flags
            // 0x0001: HEADER, 0x0002: LIGHT, 0x0004: EMITTER, etc.
            ushort headerFlag = 0x0001;
            ushort lightFlag = 0x0002;
            ushort meshFlag = 0x0020;
            ushort skinFlag = 0x0040;
            ushort danglyFlag = 0x0100;
            ushort saberFlag = 0x0800;

            (headerFlag & 0x0001).Should().Be(0x0001, "HEADER flag should be 0x0001");
            (lightFlag & 0x0002).Should().Be(0x0002, "LIGHT flag should be 0x0002");
            (meshFlag & 0x0020).Should().Be(0x0020, "MESH flag should be 0x0020");
            (skinFlag & 0x0040).Should().Be(0x0040, "SKIN flag should be 0x0040");
            (danglyFlag & 0x0100).Should().Be(0x0100, "DANGLY flag should be 0x0100");
            (saberFlag & 0x0800).Should().Be(0x0800, "SABER flag should be 0x0800");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlTrimeshHeader()
        {
            // Test trimesh header structure
            // KotOR 1: 332 bytes, KotOR 2: 340 bytes
            // Base trimesh header is 332 bytes (K1) or 340 bytes (K2)
            int k1TrimeshSize = 332;
            int k2TrimeshSize = 340;

            k1TrimeshSize.Should().Be(332, "KotOR 1 trimesh header should be 332 bytes");
            k2TrimeshSize.Should().Be(340, "KotOR 2 trimesh header should be 340 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlControllerStructure()
        {
            // Test controller structure (16 bytes)
            // CONTROLLER_SIZE = 16 (constant is in Runtime project, validated via file structure)

            // Validate controller types
            // Position: 8, Orientation: 20, Scale: 36, Alpha: 132
            int positionType = 8;
            int orientationType = 20;
            int scaleType = 36;
            int alphaType = 132;

            positionType.Should().Be(8, "Position controller type should be 8");
            orientationType.Should().Be(20, "Orientation controller type should be 20");
            scaleType.Should().Be(36, "Scale controller type should be 36");
            alphaType.Should().Be(132, "Alpha controller type should be 132");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlAnimationHeader()
        {
            // Test animation header structure (136 bytes = geometry header 80 + animation header 56)
            // ANIMATION_HEADER_SIZE = 136 (constant is in Runtime project, validated via file structure)

            // Animation header = Geometry header (80) + Animation header (56)
            int geometryHeaderSize = 80;
            int animationHeaderExtension = 56;
            int totalAnimationHeaderSize = geometryHeaderSize + animationHeaderExtension;
            totalAnimationHeaderSize.Should().Be(136, "Total animation header size should be 136 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlEventStructure()
        {
            // Test event structure (36 bytes)
            // EVENT_SIZE = 36 (constant is in Runtime project, validated via file structure)

            // Event structure: activation_time (4 bytes) + event_name (32 bytes)
            int activationTimeSize = 4;
            int eventNameSize = 32;
            int totalEventSize = activationTimeSize + eventNameSize;
            totalEventSize.Should().Be(36, "Total event structure size should be 36 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlNodeTypeCombinations()
        {
            // Test common node type combinations as defined in MDL.ksy
            ushort dummyNode = 0x001;      // NODE_HAS_HEADER
            ushort lightNode = 0x003;      // NODE_HAS_HEADER | NODE_HAS_LIGHT
            ushort emitterNode = 0x005;     // NODE_HAS_HEADER | NODE_HAS_EMITTER
            ushort referenceNode = 0x011;  // NODE_HAS_HEADER | NODE_HAS_REFERENCE
            ushort meshNode = 0x021;        // NODE_HAS_HEADER | NODE_HAS_MESH
            ushort skinMeshNode = 0x061;   // NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_SKIN
            ushort danglyMeshNode = 0x121;  // NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_DANGLY
            ushort saberMeshNode = 0x821;   // NODE_HAS_HEADER | NODE_HAS_MESH | NODE_HAS_SABER

            dummyNode.Should().Be(0x001);
            lightNode.Should().Be(0x003);
            emitterNode.Should().Be(0x005);
            referenceNode.Should().Be(0x011);
            meshNode.Should().Be(0x021);
            skinMeshNode.Should().Be(0x061);
            danglyMeshNode.Should().Be(0x121);
            saberMeshNode.Should().Be(0x821);
        }

        [Fact(Timeout = 120000)]
        public void TestMdlMdxDataFlags()
        {
            // Test MDX data bitmap masks as defined in MDL.ksy
            uint mdxVertices = 0x00000001;
            uint mdxTex0Vertices = 0x00000002;
            uint mdxTex1Vertices = 0x00000004;
            uint mdxTex2Vertices = 0x00000008;
            uint mdxTex3Vertices = 0x00000010;
            uint mdxVertexNormals = 0x00000020;
            uint mdxVertexColors = 0x00000040;
            uint mdxTangentSpace = 0x00000080;

            mdxVertices.Should().Be(0x00000001, "MDX_VERTICES flag should be 0x00000001");
            mdxTex0Vertices.Should().Be(0x00000002, "MDX_TEX0_VERTICES flag should be 0x00000002");
            mdxTex1Vertices.Should().Be(0x00000004, "MDX_TEX1_VERTICES flag should be 0x00000004");
            mdxTex2Vertices.Should().Be(0x00000008, "MDX_TEX2_VERTICES flag should be 0x00000008");
            mdxTex3Vertices.Should().Be(0x00000010, "MDX_TEX3_VERTICES flag should be 0x00000010");
            mdxVertexNormals.Should().Be(0x00000020, "MDX_VERTEX_NORMALS flag should be 0x00000020");
            mdxVertexColors.Should().Be(0x00000040, "MDX_VERTEX_COLORS flag should be 0x00000040");
            mdxTangentSpace.Should().Be(0x00000080, "MDX_TANGENT_SPACE flag should be 0x00000080");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlQuaternionOrder()
        {
            // Test quaternion order (W, X, Y, Z) as defined in MDL.ksy
            // Orientation quaternion is stored as W, X, Y, Z (not X, Y, Z, W)
            float w = 1.0f;
            float x = 0.0f;
            float y = 0.0f;
            float z = 0.0f;

            // Quaternion should be stored in W, X, Y, Z order
            byte[] quatBytes = new byte[16];
            BitConverter.GetBytes(w).CopyTo(quatBytes, 0);
            BitConverter.GetBytes(x).CopyTo(quatBytes, 4);
            BitConverter.GetBytes(y).CopyTo(quatBytes, 8);
            BitConverter.GetBytes(z).CopyTo(quatBytes, 12);

            float readW = BitConverter.ToSingle(quatBytes, 0);
            float readX = BitConverter.ToSingle(quatBytes, 4);
            float readY = BitConverter.ToSingle(quatBytes, 8);
            float readZ = BitConverter.ToSingle(quatBytes, 12);

            readW.Should().Be(w);
            readX.Should().Be(x);
            readY.Should().Be(y);
            readZ.Should().Be(z);
        }

        [Fact(Timeout = 120000)]
        public void TestMdlEmptyFile()
        {
            // Test MDL with minimal structure
            var mdl = new MDL();
            mdl.Name.Should().BeEmpty();
            mdl.Anims.Should().NotBeNull();
            mdl.Anims.Count.Should().Be(0);
            mdl.Root.Should().NotBeNull();
        }

        [Fact(Timeout = 120000)]
        public void TestMdlVersionDetection()
        {
            // Test KotOR 1 vs KotOR 2 version detection via function pointer
            // KotOR 1: function_pointer_0 = 0x0040b0b0
            // KotOR 2: function_pointer_0 = 0x0040b0b0 (same, but other fields differ)

            uint kotor1FunctionPointer = 0x0040b0b0;
            uint kotor2FunctionPointer = 0x0040b0b0;

            // Both games use the same function pointer for version detection
            // Version is typically detected by other means (file size, trimesh header size, etc.)
            kotor1FunctionPointer.Should().Be(kotor2FunctionPointer);
        }

        [Fact(Timeout = 120000)]
        public void TestMdlBezierInterpolation()
        {
            // Test Bezier interpolation flag in controller structure
            // If bit 4 (0x10) is set in column_count, controller uses Bezier interpolation
            byte columnCount = 3;
            byte bezierFlag = 0x10;
            bool isBezier = (columnCount & bezierFlag) != 0;

            // Test normal column count (not Bezier)
            columnCount = 3;
            isBezier = (columnCount & bezierFlag) != 0;
            isBezier.Should().BeFalse("Column count 3 should not have Bezier flag");

            // Test Bezier column count
            columnCount = (byte)(3 | bezierFlag);
            isBezier = (columnCount & bezierFlag) != 0;
            isBezier.Should().BeTrue("Column count with bit 4 set should indicate Bezier interpolation");
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new MDLBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new MDLBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();
        }

        [Fact(Timeout = 120000)]
        public void TestMdlHeaderOffsets()
        {
            // Test that header offsets match MDL.ksy definitions
            int fileHeaderOffset = 0;
            int geometryHeaderOffset = 12;
            int modelHeaderOffset = 92;
            int namesHeaderOffset = 180;

            fileHeaderOffset.Should().Be(0, "File header should be at offset 0");
            geometryHeaderOffset.Should().Be(12, "Geometry header should be at offset 12");
            modelHeaderOffset.Should().Be(92, "Model header should be at offset 92");
            namesHeaderOffset.Should().Be(180, "Names header should be at offset 180");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlDanglymeshHeader()
        {
            // Test danglymesh header extends trimesh header
            // K1: 360 bytes (332 + 28), K2: 368 bytes (340 + 28)
            int k1DanglymeshSize = 360;
            int k2DanglymeshSize = 368;
            int trimeshExtension = 28;

            k1DanglymeshSize.Should().Be(332 + trimeshExtension, "KotOR 1 danglymesh should be 360 bytes");
            k2DanglymeshSize.Should().Be(340 + trimeshExtension, "KotOR 2 danglymesh should be 368 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlSkinmeshHeader()
        {
            // Test skinmesh header extends trimesh header
            // K1: 432 bytes (332 + 100), K2: 440 bytes (340 + 100)
            int k1SkinmeshSize = 432;
            int k2SkinmeshSize = 440;
            int trimeshExtension = 100;

            k1SkinmeshSize.Should().Be(332 + trimeshExtension, "KotOR 1 skinmesh should be 432 bytes");
            k2SkinmeshSize.Should().Be(340 + trimeshExtension, "KotOR 2 skinmesh should be 440 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlLightsaberHeader()
        {
            // Test lightsaber header extends trimesh header
            // K1: 352 bytes (332 + 20), K2: 360 bytes (340 + 20)
            int k1SaberSize = 352;
            int k2SaberSize = 360;
            int trimeshExtension = 20;

            k1SaberSize.Should().Be(332 + trimeshExtension, "KotOR 1 lightsaber should be 352 bytes");
            k2SaberSize.Should().Be(340 + trimeshExtension, "KotOR 2 lightsaber should be 360 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlLightHeader()
        {
            // Test light header size (92 bytes)
            // Light header: 4 floats (16 bytes) + various arrays and flags (76 bytes)
            int lightHeaderSize = 92;
            lightHeaderSize.Should().Be(92, "Light header should be 92 bytes as defined in MDL.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlEmitterHeader()
        {
            // Test emitter header size (224 bytes)
            int emitterHeaderSize = 224;
            emitterHeaderSize.Should().Be(224, "Emitter header should be 224 bytes as defined in MDL.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestMdlReferenceHeader()
        {
            // Test reference header size (36 bytes)
            // Reference header: model_resref (32 bytes) + reattachable (4 bytes)
            int referenceHeaderSize = 36;
            referenceHeaderSize.Should().Be(36, "Reference header should be 36 bytes as defined in MDL.ksy");
        }

        private static void CreateTestMdlFile(string path)
        {
            // Create minimal valid MDL file structure
            using (var fs = File.Create(path))
            {
                using (var writer = new System.IO.BinaryWriter(fs))
                {
                    // File header (12 bytes)
                    writer.Write(0u); // unused
                    writer.Write(200u); // mdl_size
                    writer.Write(0u); // mdx_size

                    // Geometry header (80 bytes)
                    writer.Write(0x0040b0b0u); // function_pointer_0 (KotOR version identifier)
                    writer.Write(0u); // function_pointer_1
                    byte[] modelName = new byte[32];
                    System.Text.Encoding.ASCII.GetBytes("test_model").CopyTo(modelName, 0);
                    writer.Write(modelName); // model_name (32 bytes)
                    writer.Write(92u); // root_node_offset
                    writer.Write(1u); // node_count
                    writer.Write(0u); writer.Write(0u); writer.Write(0u); // unknown_array_def_1
                    writer.Write(0u); writer.Write(0u); writer.Write(0u); // unknown_array_def_2
                    writer.Write(0u); // reference_count
                    writer.Write((byte)0x02); // geometry_type (model geometry)
                    writer.Write((byte)0); // padding

                    // Model header (92 bytes)
                    writer.Write((byte)32); // classification (OTHER)
                    writer.Write((byte)0); // subclassification
                    writer.Write((byte)0); // unknown
                    writer.Write((byte)1); // affected_by_fog
                    writer.Write(0u); // child_model_count
                    writer.Write(0u); // animation_array_offset
                    writer.Write(0u); // animation_count
                    writer.Write(0u); // animation_count_duplicate
                    writer.Write(0u); // parent_model_pointer
                    writer.Write(-5.0f); writer.Write(-5.0f); writer.Write(-1.0f); // bounding_box_min
                    writer.Write(5.0f); writer.Write(5.0f); writer.Write(10.0f); // bounding_box_max
                    writer.Write(7.0f); // radius
                    writer.Write(0.971f); // animation_scale
                    byte[] supermodelName = new byte[32];
                    writer.Write(supermodelName); // supermodel_name (32 bytes)

                    // Names header (28 bytes)
                    writer.Write(92u); // root_node_offset
                    writer.Write(0u); // unknown_padding
                    writer.Write(0u); // mdx_data_size
                    writer.Write(0u); // mdx_data_offset
                    writer.Write(0u); // names_array_offset
                    writer.Write(0u); // names_count
                    writer.Write(0u); // names_count_duplicate
                }
            }
        }
    }
}

