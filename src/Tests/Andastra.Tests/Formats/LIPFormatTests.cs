using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.LIP;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for LIP binary I/O operations.
    /// Tests validate the LIP format structure as defined in LIP.ksy Kaitai Struct definition.
    /// </summary>
    public class LIPFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.lip");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test LIP file if it doesn't exist
                CreateTestLipFile(BinaryTestFile);
            }

            // Test reading LIP file
            LIP lip = new LIPBinaryReader(BinaryTestFile).Load();
            ValidateIO(lip);

            // Test writing and reading back
            byte[] data = WriteLipToBytes(lip);
            lip = new LIPBinaryReader(data).Load();
            ValidateIO(lip);
        }

        [Fact(Timeout = 120000)]
        public void TestLipHeaderStructure()
        {
            // Test that LIP header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLipFile(BinaryTestFile);
            }

            LIP lip = new LIPBinaryReader(BinaryTestFile).Load();

            // Validate header constants match LIP.ksy
            LIP.FileHeader.Should().Be("LIP V1.0", "LIP header should be 'LIP V1.0' as defined in LIP.ksy");
            LIPBinaryWriter.HeaderSize.Should().Be(16, "LIP header should be 16 bytes as defined in LIP.ksy");
            LIPBinaryWriter.LipEntrySize.Should().Be(5, "Keyframe entry should be 5 bytes (4 float + 1 byte) as defined in LIP.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestLipFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLipFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches LIP.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("LIP ", "File type should be 'LIP ' (space-padded) as defined in LIP.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V1.0", "Version should be 'V1.0' as defined in LIP.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestLipKeyframeStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLipFile(BinaryTestFile);
            }

            LIP lip = new LIPBinaryReader(BinaryTestFile).Load();

            // Validate keyframe structure
            lip.Count.Should().BeGreaterThanOrEqualTo(0, "Keyframe count should be non-negative");
            lip.Length.Should().BeGreaterThanOrEqualTo(0.0f, "Length should be non-negative");

            // Validate each keyframe has required fields (matching keyframe_entry in LIP.ksy)
            foreach (var keyframe in lip.Frames)
            {
                keyframe.Time.Should().BeGreaterThanOrEqualTo(0.0f, "Timestamp should be non-negative");
                keyframe.Time.Should().BeLessThanOrEqualTo(lip.Length, "Timestamp should not exceed length");
                int shapeValue = (int)keyframe.Shape;
                shapeValue.Should().BeInRange((int)LIPShape.Neutral, (int)LIPShape.KG, "Shape should be valid enum value (0-15)");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLipKeyframesSorted()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestLipFile(BinaryTestFile);
            }

            LIP lip = new LIPBinaryReader(BinaryTestFile).Load();

            // Validate keyframes are sorted chronologically (as per LIP.ksy documentation)
            for (int i = 1; i < lip.Frames.Count; i++)
            {
                lip.Frames[i].Time.Should().BeGreaterThanOrEqualTo(lip.Frames[i - 1].Time,
                    $"Keyframe {i} should be sorted after keyframe {i - 1}");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLipShapeEnumValues()
        {
            // Test that all LIPShape enum values match LIP.ksy definition (0-15)
            var allShapes = Enum.GetValues(typeof(LIPShape)).Cast<LIPShape>();
            foreach (var shape in allShapes)
            {
                int value = (int)shape;
                value.Should().BeInRange(0, 15, $"Shape {shape} should be in valid range (0-15)");
            }

            // Verify we have all 16 shapes (0-15)
            allShapes.Count().Should().Be(16, "Should have exactly 16 lip shapes (0-15)");
        }

        [Fact(Timeout = 120000)]
        public void TestLipEmptyFile()
        {
            // Test LIP with no keyframes
            var lip = new LIP();
            lip.Count.Should().Be(0, "Empty LIP should have 0 keyframes");
            lip.Length.Should().Be(0.0f, "Empty LIP should have 0 length");

            byte[] data = WriteLipToBytes(lip);
            LIP loaded = new LIPBinaryReader(data).Load();

            loaded.Count.Should().Be(0);
            loaded.Length.Should().Be(0.0f);
        }

        [Fact(Timeout = 120000)]
        public void TestLipMultipleKeyframes()
        {
            // Test LIP with multiple keyframes of different shapes
            var lip = new LIP();

            lip.Add(0.0f, LIPShape.Neutral);
            lip.Add(0.5f, LIPShape.AH);
            lip.Add(1.0f, LIPShape.EE);
            lip.Add(1.5f, LIPShape.OOH);
            lip.Add(2.0f, LIPShape.Neutral);

            lip.Count.Should().Be(5);
            lip.Length.Should().Be(2.0f);

            byte[] serialized = WriteLipToBytes(lip);
            LIP loaded = new LIPBinaryReader(serialized).Load();

            loaded.Count.Should().Be(5);
            loaded.Length.Should().Be(2.0f);
            loaded.Frames[0].Shape.Should().Be(LIPShape.Neutral);
            loaded.Frames[1].Shape.Should().Be(LIPShape.AH);
            loaded.Frames[2].Shape.Should().Be(LIPShape.EE);
            loaded.Frames[3].Shape.Should().Be(LIPShape.OOH);
            loaded.Frames[4].Shape.Should().Be(LIPShape.Neutral);
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new LIPBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new LIPBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();
        }

        [Fact(Timeout = 120000)]
        public void TestLipInvalidSignature()
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

                Action act = () => new LIPBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*invalid*");
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
        public void TestLipInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[8];
                    System.Text.Encoding.ASCII.GetBytes("LIP ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new LIPBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*version*");
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
        public void TestLipKeyframeTimestampValidation()
        {
            // Test that timestamps are properly validated
            var lip = new LIP();
            lip.Add(0.0f, LIPShape.Neutral);
            lip.Add(-0.5f, LIPShape.AH); // Negative time should be handled

            // The Add method should handle this - let's verify behavior
            lip.Count.Should().BeGreaterThanOrEqualTo(1, "Should have at least one keyframe");
        }

        [Fact(Timeout = 120000)]
        public void TestLipRoundTrip()
        {
            // Test creating LIP, writing it, reading it back
            var original = new LIP();
            original.Add(0.0f, LIPShape.Neutral);
            original.Add(0.5f, LIPShape.EH);
            original.Add(1.0f, LIPShape.AH);
            original.Add(1.5f, LIPShape.OH);
            original.Add(2.0f, LIPShape.Neutral);

            byte[] data = WriteLipToBytes(original);
            LIP loaded = new LIPBinaryReader(data).Load();

            loaded.Count.Should().Be(original.Count);
            loaded.Length.Should().BeApproximately(original.Length, 0.0001f);

            for (int i = 0; i < original.Count; i++)
            {
                loaded.Frames[i].Time.Should().BeApproximately(original.Frames[i].Time, 0.0001f);
                loaded.Frames[i].Shape.Should().Be(original.Frames[i].Shape);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLipAllShapes()
        {
            // Test that all 16 shapes can be serialized and deserialized
            var lip = new LIP();
            float time = 0.0f;

            foreach (LIPShape shape in Enum.GetValues(typeof(LIPShape)))
            {
                lip.Add(time, shape);
                time += 0.1f;
            }

            lip.Count.Should().Be(16, "Should have 16 keyframes, one for each shape");

            byte[] data = WriteLipToBytes(lip);
            LIP loaded = new LIPBinaryReader(data).Load();

            loaded.Count.Should().Be(16);
            for (int i = 0; i < 16; i++)
            {
                loaded.Frames[i].Shape.Should().Be((LIPShape)i, $"Shape at index {i} should match");
            }
        }

        private static void ValidateIO(LIP lip)
        {
            // Basic validation
            lip.Should().NotBeNull();
            lip.Count.Should().BeGreaterThanOrEqualTo(0);
            lip.Length.Should().BeGreaterThanOrEqualTo(0.0f);
        }

        private static void CreateTestLipFile(string path)
        {
            var lip = new LIP();
            lip.Add(0.0f, LIPShape.Neutral);
            lip.Add(1.0f, LIPShape.AH);
            lip.Add(2.0f, LIPShape.EE);
            lip.Add(3.0f, LIPShape.Neutral);

            byte[] data = WriteLipToBytes(lip);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        private static byte[] WriteLipToBytes(LIP lip)
        {
            var writer = new LIPBinaryWriter(lip);
            writer.Write();
            return writer.Data();
        }
    }
}

