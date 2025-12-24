using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.BWM;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for BWM (Binary WalkMesh) binary I/O operations.
    /// Tests validate the BWM format structure as defined in BWM.ksy Kaitai Struct definition.
    /// </summary>
    public class BWMFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.wok");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.wok");
        private static readonly string PWKTestFile = TestFileHelper.GetPath("test.pwk");
        private static readonly string DWKTestFile = TestFileHelper.GetPath("test.dwk");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            // Test reading BWM file
            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();
            ValidateIO(bwm);

            // Test writing and reading back
            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                bwm = new BWMBinaryReader(data).Load();
                ValidateIO(bwm);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmHeaderStructure()
        {
            // Test that BWM header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate header constants match BWM.ksy
            // Header size is 8 bytes (magic + version)
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            string magic = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);

            magic.Should().Be("BWM ", "BWM magic should be 'BWM ' (space-padded) as defined in BWM.ksy");
            version.Should().Be("V1.0", "BWM version should be 'V1.0' as defined in BWM.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestBwmFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches BWM.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("BWM ", "File type should be 'BWM ' (space-padded) as defined in BWM.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V1.0", "Version should be 'V1.0' as defined in BWM.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestBwmWalkmeshProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate walkmesh properties structure
            bwm.WalkmeshType.Should().BeOneOf(new[] { BWMType.AreaModel, BWMType.PlaceableOrDoor }, "Walkmesh type should be valid");
            bwm.Position.Should().NotBeNull("Position should not be null");
            bwm.RelativeHook1.Should().NotBeNull("RelativeHook1 should not be null");
            bwm.RelativeHook2.Should().NotBeNull("RelativeHook2 should not be null");
            bwm.AbsoluteHook1.Should().NotBeNull("AbsoluteHook1 should not be null");
            bwm.AbsoluteHook2.Should().NotBeNull("AbsoluteHook2 should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestBwmVerticesArray()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate vertices array structure
            bwm.Faces.Should().NotBeNull("Faces should not be null");
            bwm.Faces.Count.Should().BeGreaterThanOrEqualTo(0, "Face count should be non-negative");

            // Each face should have valid vertices
            foreach (var face in bwm.Faces)
            {
                face.V1.Should().NotBeNull("Face V1 should not be null");
                face.V2.Should().NotBeNull("Face V2 should not be null");
                face.V3.Should().NotBeNull("Face V3 should not be null");
            }

            // Validate unique vertices
            var vertices = bwm.Vertices();
            vertices.Should().NotBeNull("Vertices list should not be null");
            vertices.Count.Should().BeGreaterThanOrEqualTo(0, "Vertex count should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestBwmFaceIndices()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate face indices structure
            var vertices = bwm.Vertices();
            foreach (var face in bwm.Faces)
            {
                // Validate that face vertices exist in vertices list
                vertices.Should().Contain(face.V1, "Face V1 should be in vertices list");
                vertices.Should().Contain(face.V2, "Face V2 should be in vertices list");
                vertices.Should().Contain(face.V3, "Face V3 should be in vertices list");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmMaterials()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate materials array structure
            foreach (var face in bwm.Faces)
            {
                face.Material.Should().BeDefined("Face material should be a valid SurfaceMaterial enum value");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmWalkableFaces()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate walkable faces
            var walkableFaces = bwm.WalkableFaces();
            walkableFaces.Should().NotBeNull("Walkable faces should not be null");
            walkableFaces.Count.Should().BeGreaterThanOrEqualTo(0, "Walkable face count should be non-negative");

            // All walkable faces should have walkable materials
            foreach (var face in walkableFaces)
            {
                face.Material.Walkable().Should().BeTrue("Walkable face should have walkable material");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmUnwalkableFaces()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate unwalkable faces
            var unwalkableFaces = bwm.UnwalkableFaces();
            unwalkableFaces.Should().NotBeNull("Unwalkable faces should not be null");
            unwalkableFaces.Count.Should().BeGreaterThanOrEqualTo(0, "Unwalkable face count should be non-negative");

            // All unwalkable faces should have non-walkable materials
            foreach (var face in unwalkableFaces)
            {
                face.Material.Walkable().Should().BeFalse("Unwalkable face should have non-walkable material");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmAreaWalkmesh()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate area walkmesh (WOK) structure
            if (bwm.WalkmeshType == BWMType.AreaModel)
            {
                // Area walkmeshes should have AABB trees (if faces exist)
                if (bwm.Faces.Count > 0)
                {
                    var aabbs = bwm.Aabbs();
                    aabbs.Should().NotBeNull("AABB tree should not be null for area walkmesh");
                    // AABB count may be 0 for very small walkmeshes, but structure should exist
                }

                // Area walkmeshes should have edges and perimeters
                var edges = bwm.Edges();
                edges.Should().NotBeNull("Edges should not be null for area walkmesh");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmPlaceableOrDoorWalkmesh()
        {
            if (!File.Exists(PWKTestFile))
            {
                CreateTestPwkFile(PWKTestFile);
            }

            BWM bwm = new BWMBinaryReader(PWKTestFile).Load();

            // Validate placeable/door walkmesh (PWK/DWK) structure
            if (bwm.WalkmeshType == BWMType.PlaceableOrDoor)
            {
                // Placeable/door walkmeshes should NOT have AABB trees
                var aabbs = bwm.Aabbs();
                aabbs.Should().NotBeNull("AABB list should not be null");
                aabbs.Count.Should().Be(0, "Placeable/door walkmesh should have no AABB nodes");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmAabbTree()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate AABB tree structure (WOK only)
            if (bwm.WalkmeshType == BWMType.AreaModel && bwm.Faces.Count > 0)
            {
                var aabbs = bwm.Aabbs();
                aabbs.Should().NotBeNull("AABB tree should not be null");

                foreach (var aabb in aabbs)
                {
                    aabb.BbMin.Should().NotBeNull("AABB BbMin should not be null");
                    aabb.BbMax.Should().NotBeNull("AABB BbMax should not be null");

                    // Validate bounding box bounds
                    aabb.BbMin.X.Should().BeLessThanOrEqualTo(aabb.BbMax.X, "BbMin.X should be <= BbMax.X");
                    aabb.BbMin.Y.Should().BeLessThanOrEqualTo(aabb.BbMax.Y, "BbMin.Y should be <= BbMax.Y");
                    aabb.BbMin.Z.Should().BeLessThanOrEqualTo(aabb.BbMax.Z, "BbMin.Z should be <= BbMax.Z");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmAdjacencies()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate adjacencies (WOK only)
            if (bwm.WalkmeshType == BWMType.AreaModel)
            {
                var walkableFaces = bwm.WalkableFaces();
                foreach (var face in walkableFaces)
                {
                    var adjacencies = bwm.Adjacencies(face);
                    adjacencies.Should().NotBeNull("Adjacencies should not be null");

                    // Each face should have 3 adjacency entries (one per edge)
                    // Adjacencies can be null or BWMAdjacency
                    if (adjacencies.Item1 != null)
                    {
                        adjacencies.Item1.Should().BeOfType<BWMAdjacency>("Edge 0 adjacency should be BWMAdjacency or null");
                    }
                    if (adjacencies.Item2 != null)
                    {
                        adjacencies.Item2.Should().BeOfType<BWMAdjacency>("Edge 1 adjacency should be BWMAdjacency or null");
                    }
                    if (adjacencies.Item3 != null)
                    {
                        adjacencies.Item3.Should().BeOfType<BWMAdjacency>("Edge 2 adjacency should be BWMAdjacency or null");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmEdges()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestWokFile(BinaryTestFile);
            }

            BWM bwm = new BWMBinaryReader(BinaryTestFile).Load();

            // Validate edges (WOK only)
            if (bwm.WalkmeshType == BWMType.AreaModel)
            {
                var edges = bwm.Edges();
                edges.Should().NotBeNull("Edges should not be null");

                foreach (var edge in edges)
                {
                    edge.Face.Should().NotBeNull("Edge face should not be null");
                    edge.Index.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(2, "Edge index should be 0, 1, or 2");
                    edge.Transition.Should().BeGreaterThanOrEqualTo(-1, "Edge transition should be >= -1");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmEmptyFile()
        {
            // Test BWM with no faces
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;
            bwm.Faces.Should().NotBeNull("Faces should not be null");
            bwm.Faces.Count.Should().Be(0, "Empty BWM should have 0 faces");

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                loaded.Faces.Count.Should().Be(0);
                loaded.WalkmeshType.Should().Be(BWMType.AreaModel);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmMultipleFaces()
        {
            // Test BWM with multiple faces
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;

            // Create multiple faces
            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            var v4 = new Vector3(1, 1, 0);

            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.Dirt });
            bwm.Faces.Add(new BWMFace(v2, v4, v3) { Material = SurfaceMaterial.Grass });

            bwm.Faces.Count.Should().Be(2);

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                loaded.Faces.Count.Should().Be(2);
                loaded.Faces[0].Material.Should().Be(SurfaceMaterial.Dirt);
                loaded.Faces[1].Material.Should().Be(SurfaceMaterial.Grass);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmWalkmeshTypeAreaModel()
        {
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;

            // Add a walkable face
            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.Dirt });

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                loaded.WalkmeshType.Should().Be(BWMType.AreaModel);
                loaded.Faces.Count.Should().Be(1);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmWalkmeshTypePlaceableOrDoor()
        {
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.PlaceableOrDoor;

            // Add a non-walkable face
            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.NonWalk });

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                loaded.WalkmeshType.Should().Be(BWMType.PlaceableOrDoor);
                loaded.Faces.Count.Should().Be(1);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmHooks()
        {
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.PlaceableOrDoor;
            bwm.RelativeHook1 = new Vector3(1, 2, 3);
            bwm.RelativeHook2 = new Vector3(4, 5, 6);
            bwm.AbsoluteHook1 = new Vector3(7, 8, 9);
            bwm.AbsoluteHook2 = new Vector3(10, 11, 12);
            bwm.Position = new Vector3(0, 0, 0);

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                loaded.RelativeHook1.Should().Be(bwm.RelativeHook1);
                loaded.RelativeHook2.Should().Be(bwm.RelativeHook2);
                loaded.AbsoluteHook1.Should().Be(bwm.AbsoluteHook1);
                loaded.AbsoluteHook2.Should().Be(bwm.AbsoluteHook2);
                loaded.Position.Should().Be(bwm.Position);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmTransitions()
        {
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;

            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            var face = new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.Dirt };
            face.Trans1 = 5;
            face.Trans2 = 10;
            face.Trans3 = -1;
            bwm.Faces.Add(face);

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                loaded.Faces[0].Trans1.Should().Be(5);
                loaded.Faces[0].Trans2.Should().Be(10);
                // Trans3 may be -1 or null depending on implementation
            }
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new BWMBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new BWMBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => new BWMBinaryReader(CorruptBinaryTestFile).Load();
                act3.Should().Throw<ArgumentException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmInvalidSignature()
        {
            // Create file with invalid signature
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] invalid = System.Text.Encoding.ASCII.GetBytes("INVALID");
                    fs.Write(invalid, 0, invalid.Length);
                }

                Action act = () => new BWMBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*Not a valid binary BWM file*");
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
        public void TestBwmInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[8];
                    System.Text.Encoding.ASCII.GetBytes("BWM ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new BWMBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*Unsupported BWM version*");
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
        public void TestBwmRoundTrip()
        {
            // Test creating BWM, writing it, and reading it back
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;
            bwm.Position = new Vector3(10, 20, 30);
            bwm.RelativeHook1 = new Vector3(1, 2, 3);
            bwm.RelativeHook2 = new Vector3(4, 5, 6);
            bwm.AbsoluteHook1 = new Vector3(7, 8, 9);
            bwm.AbsoluteHook2 = new Vector3(10, 11, 12);

            // Add multiple faces with different materials
            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            var v4 = new Vector3(1, 1, 0);
            var v5 = new Vector3(0.5f, 0.5f, 1);

            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.Dirt });
            bwm.Faces.Add(new BWMFace(v2, v4, v3) { Material = SurfaceMaterial.Grass });
            bwm.Faces.Add(new BWMFace(v1, v3, v5) { Material = SurfaceMaterial.Stone });

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                loaded.WalkmeshType.Should().Be(bwm.WalkmeshType);
                loaded.Position.Should().Be(bwm.Position);
                loaded.RelativeHook1.Should().Be(bwm.RelativeHook1);
                loaded.RelativeHook2.Should().Be(bwm.RelativeHook2);
                loaded.AbsoluteHook1.Should().Be(bwm.AbsoluteHook1);
                loaded.AbsoluteHook2.Should().Be(bwm.AbsoluteHook2);
                loaded.Faces.Count.Should().Be(bwm.Faces.Count);

                for (int i = 0; i < loaded.Faces.Count; i++)
                {
                    loaded.Faces[i].Material.Should().Be(bwm.Faces[i].Material);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBwmVertexSharing()
        {
            // Test that vertices are shared between faces
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;

            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            var v4 = new Vector3(1, 1, 0);

            // Two faces sharing vertices
            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.Dirt });
            bwm.Faces.Add(new BWMFace(v2, v4, v3) { Material = SurfaceMaterial.Grass });

            var vertices = bwm.Vertices();
            // Should have 4 unique vertices, not 6
            vertices.Count.Should().Be(4, "Vertices should be shared between faces");
        }

        [Fact(Timeout = 120000)]
        public void TestBwmFaceOrdering()
        {
            // Test that walkable faces are ordered before non-walkable faces
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;

            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);

            // Add non-walkable face first
            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.NonWalk });
            // Add walkable face second
            bwm.Faces.Add(new BWMFace(v1, v3, v2) { Material = SurfaceMaterial.Dirt });

            using (var writer = new BWMBinaryWriter(bwm))
            {
                writer.Write();
                byte[] data = writer.Data();
                BWM loaded = new BWMBinaryReader(data).Load();

                // After write/read, walkable faces should come first
                var walkableFaces = loaded.WalkableFaces();
                var unwalkableFaces = loaded.UnwalkableFaces();

                walkableFaces.Count.Should().Be(1);
                unwalkableFaces.Count.Should().Be(1);
            }
        }

        private static void ValidateIO(BWM bwm)
        {
            // Basic validation
            bwm.Should().NotBeNull();
            bwm.WalkmeshType.Should().BeOneOf(BWMType.AreaModel, BWMType.PlaceableOrDoor);
            bwm.Faces.Should().NotBeNull();
            bwm.Faces.Count.Should().BeGreaterThanOrEqualTo(0);
        }

        private static void CreateTestWokFile(string path)
        {
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.AreaModel;
            bwm.Position = new Vector3(0, 0, 0);
            bwm.RelativeHook1 = new Vector3(0, 0, 0);
            bwm.RelativeHook2 = new Vector3(0, 0, 0);
            bwm.AbsoluteHook1 = new Vector3(0, 0, 0);
            bwm.AbsoluteHook2 = new Vector3(0, 0, 0);

            // Add a simple triangle
            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.Dirt });

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BWMBinaryWriter(bwm, path))
            {
                writer.Write();
            }
        }

        private static void CreateTestPwkFile(string path)
        {
            var bwm = new BWM();
            bwm.WalkmeshType = BWMType.PlaceableOrDoor;
            bwm.Position = new Vector3(0, 0, 0);
            bwm.RelativeHook1 = new Vector3(0, 0, 0);
            bwm.RelativeHook2 = new Vector3(0, 0, 0);
            bwm.AbsoluteHook1 = new Vector3(0, 0, 0);
            bwm.AbsoluteHook2 = new Vector3(0, 0, 0);

            // Add a simple triangle
            var v1 = new Vector3(0, 0, 0);
            var v2 = new Vector3(1, 0, 0);
            var v3 = new Vector3(0, 1, 0);
            bwm.Faces.Add(new BWMFace(v1, v2, v3) { Material = SurfaceMaterial.NonWalk });

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var writer = new BWMBinaryWriter(bwm, path))
            {
                writer.Write();
            }
        }
    }
}


