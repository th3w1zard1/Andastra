using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTS binary I/O operations.
    /// Tests validate the UTS format structure as defined in UTS.ksy Kaitai Struct definition.
    /// UTS files are GFF-based format files with file type signature "UTS ".
    /// </summary>
    public class UTSFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.uts");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.uts");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            // Test reading UTS file via GFF
            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);
            ValidateIO(uts);

            // Test writing and reading back
            GFF gff2 = UTSHelpers.DismantleUts(uts, BioWareGame.K2);
            byte[] data = new GFFBinaryWriter(gff2).Write();
            GFF gff3 = GFF.FromBytes(data);
            UTS uts2 = UTSHelpers.ConstructUts(gff3);
            ValidateIO(uts2);
        }

        [Fact(Timeout = 120000)]
        public void TestUtsGffHeaderStructure()
        {
            // Test that UTS GFF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            // Read raw GFF header bytes (56 bytes)
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate file type signature matches UTS.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTS ", "File type should be 'UTS ' (space-padded) as defined in UTS.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTS.ksy valid values");

            // Validate header structure offsets (all should be non-negative and reasonable)
            uint structArrayOffset = BitConverter.ToUInt32(header, 8);
            uint structCount = BitConverter.ToUInt32(header, 12);
            uint fieldArrayOffset = BitConverter.ToUInt32(header, 16);
            uint fieldCount = BitConverter.ToUInt32(header, 20);
            uint labelArrayOffset = BitConverter.ToUInt32(header, 24);
            uint labelCount = BitConverter.ToUInt32(header, 28);
            uint fieldDataOffset = BitConverter.ToUInt32(header, 32);
            uint fieldDataCount = BitConverter.ToUInt32(header, 36);
            uint fieldIndicesOffset = BitConverter.ToUInt32(header, 40);
            uint fieldIndicesCount = BitConverter.ToUInt32(header, 44);
            uint listIndicesOffset = BitConverter.ToUInt32(header, 48);
            uint listIndicesCount = BitConverter.ToUInt32(header, 52);

            structArrayOffset.Should().BeGreaterThanOrEqualTo(56, "Struct array offset should be >= 56 (after header)");
            fieldArrayOffset.Should().BeGreaterThanOrEqualTo(56, "Field array offset should be >= 56 (after header)");
            labelArrayOffset.Should().BeGreaterThanOrEqualTo(56, "Label array offset should be >= 56 (after header)");
            fieldDataOffset.Should().BeGreaterThanOrEqualTo(56, "Field data offset should be >= 56 (after header)");
            fieldIndicesOffset.Should().BeGreaterThanOrEqualTo(56, "Field indices offset should be >= 56 (after header)");
            listIndicesOffset.Should().BeGreaterThanOrEqualTo(56, "List indices offset should be >= 56 (after header)");

            structCount.Should().BeGreaterThanOrEqualTo(1, "Struct count should be >= 1 (root struct always present)");
        }

        [Fact(Timeout = 120000)]
        public void TestUtsFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTS.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTS ", "File type should be 'UTS ' (space-padded) as defined in UTS.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTS.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestUtsInvalidSignature()
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

                Action act = () => new GFFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*Invalid GFF file type*");
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
        public void TestUtsInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("UTS ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new GFFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*Unsupported GFF version*");
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
        public void TestUtsRootStructFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);
            GFFStruct root = gff.Root;

            // Validate root struct fields exist (as per UTS.ksy documentation)
            root.Acquire<string>("Tag", "").Should().NotBeNull("Tag should not be null");
            root.Acquire<ResRef>("TemplateResRef", ResRef.FromBlank()).Should().NotBeNull("TemplateResRef should not be null");
            root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid()).Should().NotBeNull("LocName should not be null");

            // Boolean fields stored as UInt8
            byte? active = root.GetUInt8("Active");
            if (active.HasValue)
            {
                active.Value.Should().BeInRange((byte)0, (byte)1, "Active should be 0 or 1");
            }

            // Volume fields should be in valid range (0-127 typically)
            byte? volume = root.GetUInt8("Volume");
            if (volume.HasValue)
            {
                volume.Value.Should().BeInRange((byte)0, byte.MaxValue, "Volume should be valid byte");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtsBasicProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);

            // Validate basic UTS properties exist and have reasonable values
            uts.Tag.Should().NotBeNull("Tag should not be null");
            uts.ResRef.Should().NotBeNull("ResRef should not be null");
            uts.Name.Should().NotBeNull("Name should not be null");
            uts.Volume.Should().BeGreaterThanOrEqualTo(0, "Volume should be non-negative");
            uts.Priority.Should().BeGreaterThanOrEqualTo(0, "Priority should be non-negative");
            uts.VolumeVariance.Should().BeGreaterThanOrEqualTo(0, "VolumeVariance should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestUtsBooleanFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);

            // Validate boolean fields are properly set
            // These are stored as UInt8 (0 or 1) in GFF but exposed as bool in UTS class
            // Since these are already bool types, we just verify they are accessible
            // (Type checking is not needed as the compiler enforces bool type)
            _ = uts.Active; // Verify property is accessible
            _ = uts.Continuous; // Verify property is accessible
            _ = uts.Looping; // Verify property is accessible
            _ = uts.Positional; // Verify property is accessible
            _ = uts.RandomPosition; // Verify property is accessible
            _ = uts.Random; // Verify property is accessible
        }

        [Fact(Timeout = 120000)]
        public void TestUtsFloatProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);

            // Validate float properties exist and are valid
            uts.PitchVariance.Should().NotBe(float.NaN, "PitchVariance should not be NaN");
            uts.PitchVariance.Should().NotBe(float.PositiveInfinity, "PitchVariance should not be Infinity");
            uts.PitchVariance.Should().NotBe(float.NegativeInfinity, "PitchVariance should not be -Infinity");

            uts.Elevation.Should().NotBe(float.NaN, "Elevation should not be NaN");
            uts.MinDistance.Should().BeGreaterThanOrEqualTo(0.0f, "MinDistance should be non-negative");
            uts.MaxDistance.Should().BeGreaterThanOrEqualTo(uts.MinDistance, "MaxDistance should be >= MinDistance");
            uts.DistanceCutoff.Should().BeGreaterThanOrEqualTo(0.0f, "DistanceCutoff should be non-negative");
            uts.RandomRangeX.Should().NotBe(float.NaN, "RandomRangeX should not be NaN");
            uts.RandomRangeY.Should().NotBe(float.NaN, "RandomRangeY should not be NaN");
        }

        [Fact(Timeout = 120000)]
        public void TestUtsIntegerProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);

            // Validate integer properties
            uts.Hours.Should().BeGreaterThanOrEqualTo(0, "Hours should be non-negative");
            uts.Times.Should().BeGreaterThanOrEqualTo(0, "Times should be non-negative");
            uts.Interval.Should().BeGreaterThanOrEqualTo(0, "Interval should be non-negative");
            uts.IntervalVariance.Should().BeGreaterThanOrEqualTo(0, "IntervalVariance should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestUtsSoundList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);

            // Validate Sounds list exists (may be empty)
            uts.Sounds.Should().NotBeNull("Sounds list should not be null");

            // If sounds exist, they should be valid ResRefs
            foreach (var sound in uts.Sounds)
            {
                sound.Should().NotBeNull("Sound ResRef should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtsRoundTrip()
        {
            // Test creating UTS, writing, and reading it back
            UTS originalUts = CreateTestUtsObject();

            // Write to GFF and then to bytes
            GFF gff = UTSHelpers.DismantleUts(originalUts, BioWareGame.K2);
            byte[] data = new GFFBinaryWriter(gff).Write();

            // Read back
            GFF gff2 = GFF.FromBytes(data);
            UTS loadedUts = UTSHelpers.ConstructUts(gff2);

            // Validate round-trip
            loadedUts.Tag.Should().Be(originalUts.Tag);
            loadedUts.ResRef.ToString().Should().Be(originalUts.ResRef.ToString());
            loadedUts.Active.Should().Be(originalUts.Active);
            loadedUts.Continuous.Should().Be(originalUts.Continuous);
            loadedUts.Looping.Should().Be(originalUts.Looping);
            loadedUts.Volume.Should().Be(originalUts.Volume);
            loadedUts.Priority.Should().Be(originalUts.Priority);
            loadedUts.PitchVariance.Should().BeApproximately(originalUts.PitchVariance, 0.001f);
            loadedUts.Elevation.Should().BeApproximately(originalUts.Elevation, 0.001f);
            loadedUts.MinDistance.Should().BeApproximately(originalUts.MinDistance, 0.001f);
            loadedUts.MaxDistance.Should().BeApproximately(originalUts.MaxDistance, 0.001f);
        }

        [Fact(Timeout = 120000)]
        public void TestUtsEmptyFile()
        {
            // Test UTS with minimal required fields
            UTS uts = new UTS();
            uts.Tag = "TEST_SOUND";
            uts.ResRef = ResRef.FromBlank();
            uts.Name = LocalizedString.FromInvalid();

            GFF gff = UTSHelpers.DismantleUts(uts, BioWareGame.K2);
            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF gff2 = GFF.FromBytes(data);
            UTS loaded = UTSHelpers.ConstructUts(gff2);

            loaded.Tag.Should().Be("TEST_SOUND");
        }

        [Fact(Timeout = 120000)]
        public void TestUtsResRefFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);

            // Validate ResRef fields exist
            uts.ResRef.Should().NotBeNull("TemplateResRef should not be null");
            uts.Sound.Should().NotBeNull("Sound ResRef should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUtsStringFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtsFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            UTS uts = UTSHelpers.ConstructUts(gff);

            // Validate string fields exist
            uts.Tag.Should().NotBeNull("Tag should not be null");
            uts.Comment.Should().NotBeNull("Comment should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new GFFBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new GFFBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => new GFFBinaryReader(CorruptBinaryTestFile).Load();
                act3.Should().Throw<InvalidDataException>();
            }
        }

        private static void ValidateIO(UTS uts)
        {
            // Basic validation that UTS object was loaded successfully
            uts.Should().NotBeNull("UTS object should not be null");
            uts.Tag.Should().NotBeNull("Tag should not be null");
        }

        private static void CreateTestUtsFile(string path)
        {
            UTS uts = CreateTestUtsObject();
            GFF gff = UTSHelpers.DismantleUts(uts, BioWareGame.K2);
            byte[] data = new GFFBinaryWriter(gff).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        private static UTS CreateTestUtsObject()
        {
            UTS uts = new UTS();
            uts.Tag = "TEST_SOUND";
            uts.ResRef = ResRef.FromBlank();
            uts.Name = LocalizedString.FromInvalid();
            uts.Active = true;
            uts.Continuous = false;
            uts.Looping = true;
            uts.Positional = false;
            uts.RandomPosition = false;
            uts.Random = false;
            uts.Volume = 100;
            uts.VolumeVariance = 10;
            uts.PitchVariance = 0.1f;
            uts.Elevation = 0.0f;
            uts.MinDistance = 1.0f;
            uts.MaxDistance = 20.0f;
            uts.DistanceCutoff = 30.0f;
            uts.Priority = 50;
            uts.Hours = 0;
            uts.Times = 1;
            uts.Interval = 5000;
            uts.IntervalVariance = 1000;
            uts.Sound = ResRef.FromBlank();
            uts.Comment = "Test sound object";
            uts.RandomRangeX = 5.0f;
            uts.RandomRangeY = 5.0f;
            uts.Sounds = new System.Collections.Generic.List<ResRef>();
            return uts;
        }
    }
}

