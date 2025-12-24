using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Formats.RIM;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.RIM.RIMAuto;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Comprehensive, exhaustive tests for RIM format parsing.
    /// Tests all fields, edge cases, and round-trip scenarios.
    /// 1:1 port from PyKotor with additional comprehensive coverage.
    /// </summary>
    public class RIMFormatComprehensiveTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.rim");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.rim");

        #region Basic I/O Tests

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            // Python: test_binary_io
            if (!File.Exists(BinaryTestFile))
            {
                // Skip if test file doesn't exist
                return;
            }

            // Python: rim: RIM = RIMBinaryReader(BINARY_TEST_FILE).load()
            RIM rim = new RIMBinaryReader(BinaryTestFile).Load();
            ValidateIO(rim);

            // Python: data: bytearray = bytearray()
            // Python: write_rim(rim, data)
            byte[] data = BytesRim(rim);

            // Python: rim = read_rim(data)
            rim = ReadRim(data);
            ValidateIO(rim);
        }

        private static void ValidateIO(RIM rim)
        {
            // Python: validate_io
            // Python: assert len(rim) == 3
            rim.Count.Should().Be(3);
            // Python: assert rim.get("1", ResourceType.TXT) == b"abc"
            rim.Get("1", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("abc"));
            // Python: assert rim.get("2", ResourceType.TXT) == b"def"
            rim.Get("2", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("def"));
            // Python: assert rim.get("3", ResourceType.TXT) == b"ghi"
            rim.Get("3", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("ghi"));
        }

        #endregion

        #region Error Handling Tests

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // test_read_raises from Python
            // Python: read_rim(".") raises PermissionError on Windows, IsADirectoryError on Unix
            Action act1 = () => ReadRim(".");
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Python: read_rim(DOES_NOT_EXIST_FILE) raises FileNotFoundError
            Action act2 = () => ReadRim(DoesNotExistFile);
            act2.Should().Throw<FileNotFoundException>();

            // Python: read_rim(CORRUPT_BINARY_TEST_FILE) raises ValueError
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => ReadRim(CorruptBinaryTestFile);
                act3.Should().Throw<InvalidDataException>(); // ValueError equivalent
            }
        }

        [Fact(Timeout = 120000)]
        public void TestWriteRaises()
        {
            // test_write_raises from Python
            var rim = new RIM();

            // Test writing to directory (should raise PermissionError on Windows, IsADirectoryError on Unix)
            // Python: write_rim(RIM(), ".", ResourceType.RIM)
            Action act1 = () => WriteRim(rim, ".", ResourceType.RIM);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Test invalid resource type (Python raises ValueError for ResourceType.INVALID)
            // Python: write_rim(RIM(), ".", ResourceType.INVALID)
            Action act2 = () => WriteRim(rim, ".", ResourceType.INVALID);
            act2.Should().Throw<ArgumentException>().WithMessage("*Unsupported format*");
        }

        #endregion

        #region Header Field Tests

        [Fact(Timeout = 120000)]
        public void TestHeaderFileType()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify file type signature "RIM " at offset 0
            string fileType = Encoding.ASCII.GetString(data, 0, 4);
            fileType.Should().Be("RIM ");
        }

        [Fact(Timeout = 120000)]
        public void TestHeaderFileVersion()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify file version "V1.0" at offset 4
            string fileVersion = Encoding.ASCII.GetString(data, 4, 4);
            fileVersion.Should().Be("V1.0");
        }

        [Fact(Timeout = 120000)]
        public void TestHeaderReserved()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify reserved field (typically 0) at offset 8
            uint reserved = BitConverter.ToUInt32(data, 8);
            reserved.Should().Be(0u);
        }

        [Fact(Timeout = 120000)]
        public void TestHeaderResourceCount()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify resource count at offset 12
            uint resourceCount = BitConverter.ToUInt32(data, 12);
            resourceCount.Should().Be((uint)rim.Count);
        }

        [Fact(Timeout = 120000)]
        public void TestHeaderOffsetToResourceTable()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify offset to resource table at offset 16
            uint offsetToKeys = BitConverter.ToUInt32(data, 16);
            offsetToKeys.Should().Be(120u); // Standard header size
        }

        [Fact(Timeout = 120000)]
        public void TestExtendedHeaderPadding()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify extended header padding (100 bytes of zeros) at offset 20
            for (int i = 20; i < 120; i++)
            {
                data[i].Should().Be(0, $"Extended header padding byte at offset {i} should be 0");
            }
        }

        #endregion

        #region Resource Entry Tests

        [Fact(Timeout = 120000)]
        public void TestResourceEntryResRef()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Read first resource entry at offset 120
            string resref = Encoding.ASCII.GetString(data, 120, 16).TrimEnd('\0');
            resref.Should().Be("1");
        }

        [Fact(Timeout = 120000)]
        public void TestResourceEntryResRefNullPadding()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify ResRef is null-padded to 16 bytes
            byte[] resrefBytes = new byte[16];
            Array.Copy(data, 120, resrefBytes, 0, 16);
            resrefBytes[1].Should().Be(0); // After "1", should be null bytes
            for (int i = 2; i < 16; i++)
            {
                resrefBytes[i].Should().Be(0, $"ResRef padding byte at index {i} should be 0");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestResourceEntryResourceType()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify resource type at offset 136 (120 + 16)
            uint resourceType = BitConverter.ToUInt32(data, 136);
            resourceType.Should().Be((uint)ResourceType.TXT.TypeId);
        }

        [Fact(Timeout = 120000)]
        public void TestResourceEntryResourceID()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify resource ID at offset 140 (120 + 16 + 4)
            uint resourceId = BitConverter.ToUInt32(data, 140);
            resourceId.Should().Be(0u); // First resource has ID 0
        }

        [Fact(Timeout = 120000)]
        public void TestResourceEntryOffsetToData()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Calculate expected data offset
            uint entryCount = (uint)rim.Count;
            uint expectedDataOffset = 120u + (32u * entryCount); // Header + entries

            // Verify offset to data at offset 144 (120 + 16 + 4 + 4)
            uint offsetToData = BitConverter.ToUInt32(data, 144);
            offsetToData.Should().Be(expectedDataOffset);
        }

        [Fact(Timeout = 120000)]
        public void TestResourceEntryResourceSize()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify resource size at offset 148 (120 + 16 + 4 + 4 + 4)
            uint resourceSize = BitConverter.ToUInt32(data, 148);
            resourceSize.Should().Be(3u); // "abc" is 3 bytes
        }

        [Fact(Timeout = 120000)]
        public void TestResourceEntryFullStructure()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify complete structure of first resource entry (32 bytes)
            uint entryCount = (uint)rim.Count;
            uint dataStart = 120u + (32u * entryCount);

            // First entry
            string resref1 = Encoding.ASCII.GetString(data, 120, 16).TrimEnd('\0');
            uint restype1 = BitConverter.ToUInt32(data, 136);
            uint resid1 = BitConverter.ToUInt32(data, 140);
            uint offset1 = BitConverter.ToUInt32(data, 144);
            uint size1 = BitConverter.ToUInt32(data, 148);

            resref1.Should().Be("1");
            restype1.Should().Be((uint)ResourceType.TXT.TypeId);
            resid1.Should().Be(0u);
            offset1.Should().Be(dataStart);
            size1.Should().Be(3u);

            // Second entry (offset 120 + 32)
            string resref2 = Encoding.ASCII.GetString(data, 152, 16).TrimEnd('\0');
            uint restype2 = BitConverter.ToUInt32(data, 168);
            uint resid2 = BitConverter.ToUInt32(data, 172);
            uint offset2 = BitConverter.ToUInt32(data, 176);
            uint size2 = BitConverter.ToUInt32(data, 180);

            resref2.Should().Be("2");
            restype2.Should().Be((uint)ResourceType.TXT.TypeId);
            resid2.Should().Be(1u);
            offset2.Should().Be(dataStart + 3u); // After first resource
            size2.Should().Be(3u);

            // Third entry (offset 120 + 64)
            string resref3 = Encoding.ASCII.GetString(data, 184, 16).TrimEnd('\0');
            uint restype3 = BitConverter.ToUInt32(data, 200);
            uint resid3 = BitConverter.ToUInt32(data, 204);
            uint offset3 = BitConverter.ToUInt32(data, 208);
            uint size3 = BitConverter.ToUInt32(data, 212);

            resref3.Should().Be("3");
            restype3.Should().Be((uint)ResourceType.TXT.TypeId);
            resid3.Should().Be(2u);
            offset3.Should().Be(dataStart + 6u); // After first two resources
            size3.Should().Be(3u);
        }

        #endregion

        #region Resource Data Tests

        [Fact(Timeout = 120000)]
        public void TestResourceDataContent()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Calculate data start offset
            uint entryCount = (uint)rim.Count;
            uint dataStart = 120u + (32u * entryCount);

            // Verify first resource data
            byte[] resource1Data = new byte[3];
            Array.Copy(data, (int)dataStart, resource1Data, 0, 3);
            Encoding.ASCII.GetString(resource1Data).Should().Be("abc");

            // Verify second resource data
            byte[] resource2Data = new byte[3];
            Array.Copy(data, (int)(dataStart + 3), resource2Data, 0, 3);
            Encoding.ASCII.GetString(resource2Data).Should().Be("def");

            // Verify third resource data
            byte[] resource3Data = new byte[3];
            Array.Copy(data, (int)(dataStart + 6), resource3Data, 0, 3);
            Encoding.ASCII.GetString(resource3Data).Should().Be("ghi");
        }

        [Fact(Timeout = 120000)]
        public void TestResourceDataContiguity()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Verify resources are stored contiguously
            uint entryCount = (uint)rim.Count;
            uint dataStart = 120u + (32u * entryCount);

            // All three resources should be stored back-to-back
            byte[] allData = new byte[9];
            Array.Copy(data, (int)dataStart, allData, 0, 9);
            Encoding.ASCII.GetString(allData).Should().Be("abcdefghi");
        }

        #endregion

        #region Round-Trip Tests

        [Fact(Timeout = 120000)]
        public void TestRoundTripEmptyRIM()
        {
            var rim1 = new RIM();
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(0);
        }

        [Fact(Timeout = 120000)]
        public void TestRoundTripSingleResource()
        {
            var rim1 = new RIM();
            rim1.SetData("test", ResourceType.TXT, Encoding.ASCII.GetBytes("test data"));
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(1);
            rim2.Get("test", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("test data"));
        }

        [Fact(Timeout = 120000)]
        public void TestRoundTripMultipleResources()
        {
            var rim1 = CreateTestRIM();
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(rim1.Count);
            foreach (var resource in rim1)
            {
                byte[] originalData = resource.Data;
                byte[] roundTripData = rim2.Get(resource.ResRef.ToString(), resource.ResType);
                roundTripData.Should().NotBeNull();
                roundTripData.Should().Equal(originalData);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestRoundTripDifferentResourceTypes()
        {
            var rim1 = new RIM();
            rim1.SetData("text", ResourceType.TXT, Encoding.ASCII.GetBytes("text"));
            rim1.SetData("module", ResourceType.MOD, Encoding.ASCII.GetBytes("module"));
            rim1.SetData("script", ResourceType.NCS, Encoding.ASCII.GetBytes("script"));
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(3);
            rim2.Get("text", ResourceType.TXT).Should().Equal(Encoding.ASCII.GetBytes("text"));
            rim2.Get("module", ResourceType.MOD).Should().Equal(Encoding.ASCII.GetBytes("module"));
            rim2.Get("script", ResourceType.NCS).Should().Equal(Encoding.ASCII.GetBytes("script"));
        }

        [Fact(Timeout = 120000)]
        public void TestRoundTripLargeResource()
        {
            var rim1 = new RIM();
            byte[] largeData = new byte[10000];
            for (int i = 0; i < largeData.Length; i++)
            {
                largeData[i] = (byte)(i % 256);
            }
            rim1.SetData("large", ResourceType.TXT, largeData);
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(1);
            byte[] roundTripData = rim2.Get("large", ResourceType.TXT);
            roundTripData.Should().NotBeNull();
            roundTripData.Length.Should().Be(10000);
            roundTripData.Should().Equal(largeData);
        }

        [Fact(Timeout = 120000)]
        public void TestRoundTripManyResources()
        {
            var rim1 = new RIM();
            for (int i = 0; i < 100; i++)
            {
                rim1.SetData($"res{i}", ResourceType.TXT, Encoding.ASCII.GetBytes($"data{i}"));
            }
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(100);
            for (int i = 0; i < 100; i++)
            {
                byte[] expected = Encoding.ASCII.GetBytes($"data{i}");
                byte[] actual = rim2.Get($"res{i}", ResourceType.TXT);
                actual.Should().NotBeNull();
                actual.Should().Equal(expected);
            }
        }

        #endregion

        #region Edge Case Tests

        [Fact(Timeout = 120000)]
        public void TestResRefMaxLength()
        {
            var rim1 = new RIM();
            string maxResRef = new string('a', 16); // Exactly 16 characters
            rim1.SetData(maxResRef, ResourceType.TXT, Encoding.ASCII.GetBytes("test"));
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(1);
            rim2.Get(maxResRef, ResourceType.TXT).Should().NotBeNull();
        }

        [Fact(Timeout = 120000)]
        public void TestResRefCaseInsensitive()
        {
            var rim1 = new RIM();
            rim1.SetData("Test", ResourceType.TXT, Encoding.ASCII.GetBytes("test"));
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            // RIM reader lowercases ResRefs
            rim2.Get("test", ResourceType.TXT).Should().NotBeNull();
            rim2.Get("TEST", ResourceType.TXT).Should().NotBeNull();
        }

        [Fact(Timeout = 120000)]
        public void TestEmptyResourceData()
        {
            var rim1 = new RIM();
            rim1.SetData("empty", ResourceType.TXT, new byte[0]);
            byte[] data = BytesRim(rim1);
            RIM rim2 = ReadRim(data);

            rim2.Count.Should().Be(1);
            byte[] emptyData = rim2.Get("empty", ResourceType.TXT);
            emptyData.Should().NotBeNull();
            emptyData.Length.Should().Be(0);
        }

        [Fact(Timeout = 120000)]
        public void TestZeroResourceCount()
        {
            var rim1 = new RIM();
            byte[] data = BytesRim(rim1);

            // Verify resource count is 0
            uint resourceCount = BitConverter.ToUInt32(data, 12);
            resourceCount.Should().Be(0u);

            // Verify offset to resource table is still 120
            uint offsetToKeys = BitConverter.ToUInt32(data, 16);
            offsetToKeys.Should().Be(120u);
        }

        [Fact(Timeout = 120000)]
        public void TestInvalidFileTypeThrows()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Corrupt file type
            data[0] = 0xFF;
            data[1] = 0xFF;
            data[2] = 0xFF;
            data[3] = 0xFF;

            Action act = () => ReadRim(data);
            act.Should().Throw<InvalidDataException>();
        }

        [Fact(Timeout = 120000)]
        public void TestInvalidFileVersionThrows()
        {
            var rim = CreateTestRIM();
            byte[] data = BytesRim(rim);

            // Corrupt file version
            data[4] = 0xFF;
            data[5] = 0xFF;
            data[6] = 0xFF;
            data[7] = 0xFF;

            Action act = () => ReadRim(data);
            act.Should().Throw<InvalidDataException>();
        }

        #endregion

        #region Helper Methods

        private static RIM CreateTestRIM()
        {
            var rim = new RIM();
            rim.SetData("1", ResourceType.TXT, Encoding.ASCII.GetBytes("abc"));
            rim.SetData("2", ResourceType.TXT, Encoding.ASCII.GetBytes("def"));
            rim.SetData("3", ResourceType.TXT, Encoding.ASCII.GetBytes("ghi"));
            return rim;
        }

        #endregion
    }
}


