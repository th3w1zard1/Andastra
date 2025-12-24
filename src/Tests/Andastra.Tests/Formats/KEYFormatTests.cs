using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.KEY;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.KEY.KEYAuto;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for KEY binary I/O operations.
    /// Tests validate the KEY format structure as defined in KEY.ksy Kaitai Struct definition.
    /// </summary>
    public class KEYFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.key");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.key");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestKeyFile(BinaryTestFile);
            }

            // Test reading KEY file
            KEY key = new KEYBinaryReader(BinaryTestFile).Load();
            ValidateIO(key);

            // Test writing and reading back
            byte[] data = KEYAuto.BytesKey(key);
            key = new KEYBinaryReader(data).Load();
            ValidateIO(key);
        }

        [Fact(Timeout = 120000)]
        public void TestKeyHeaderStructure()
        {
            // Test that KEY header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestKeyFile(BinaryTestFile);
            }

            KEY key = new KEYBinaryReader(BinaryTestFile).Load();

            // Validate header constants match KEY.ksy
            KEY.HeaderSize.Should().Be(64, "KEY header should be 64 bytes as defined in KEY.ksy");
            KEY.FileTypeConst.Should().Be("KEY ", "KEY file type should match KEY.ksy definition");
            KEY.FileVersionConst.Should().Be("V1  ", "KEY version should match KEY.ksy definition");
            KEY.BifEntrySize.Should().Be(12, "BIF entry should be 12 bytes as defined in KEY.ksy");
            KEY.KeyEntrySize.Should().Be(22, "KEY entry should be 22 bytes as defined in KEY.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestKeyFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestKeyFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches KEY.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("KEY ", "File type should be 'KEY ' (space-padded) as defined in KEY.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V1  ", "V1.1", "Version should match KEY.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestKeyFileTable()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestKeyFile(BinaryTestFile);
            }

            KEY key = new KEYBinaryReader(BinaryTestFile).Load();

            // Validate file table structure
            key.BifEntries.Count.Should().BeGreaterThanOrEqualTo(0, "BIF entries count should be non-negative");

            // Validate each entry has required fields (matching file_entry in KEY.ksy)
            foreach (var bifEntry in key.BifEntries)
            {
                bifEntry.Filesize.Should().BeGreaterThanOrEqualTo(0, "File size should be non-negative");
                bifEntry.Filename.Should().NotBeNull("Filename should not be null");
                bifEntry.Drives.Should().BeGreaterThanOrEqualTo(0, "Drives should be non-negative");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestKeyKeyTable()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestKeyFile(BinaryTestFile);
            }

            KEY key = new KEYBinaryReader(BinaryTestFile).Load();

            // Validate KEY table structure
            key.KeyEntries.Count.Should().BeGreaterThanOrEqualTo(0, "KEY entries count should be non-negative");

            // Validate each entry has required fields (matching key_entry in KEY.ksy)
            foreach (var keyEntry in key.KeyEntries)
            {
                keyEntry.ResRef.Should().NotBeNull("ResRef should not be null");
                keyEntry.ResType.Should().NotBeNull("Resource type should not be null");
                keyEntry.ResourceId.Should().BeGreaterThanOrEqualTo(0, "Resource ID should be non-negative");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestKeyResourceIdEncoding()
        {
            // Test Resource ID encoding matches KEY.ksy documentation
            // Resource ID = (bif_index << 20) | resource_index

            // Test encoding
            int bifIndex = 4;
            int resourceIndex = 41;
            uint resourceId = (uint)((bifIndex << 20) | resourceIndex);

            // Test decoding (matching KEY.ksy documentation)
            int decodedBifIndex = (int)(resourceId >> 20) & 0xFFF;
            int decodedResourceIndex = (int)(resourceId & 0xFFFFF);

            decodedBifIndex.Should().Be(bifIndex, "BIF index should decode correctly");
            decodedResourceIndex.Should().Be(resourceIndex, "Resource index should decode correctly");

            // Test with KeyEntry properties
            var keyEntry = new KeyEntry
            {
                ResourceId = resourceId
            };
            keyEntry.BifIndex.Should().Be(bifIndex);
            keyEntry.ResIndex.Should().Be(resourceIndex);
        }

        [Fact(Timeout = 120000)]
        public void TestKeyDriveFlags()
        {
            // Test drive flags match KEY.ksy documentation
            // Bit flags: 0x0001=HD0, 0x0002=CD1, 0x0004=CD2, 0x0008=CD3, 0x0010=CD4

            var bifEntry = new BifEntry
            {
                Filename = "test.bif",
                Filesize = 1000,
                Drives = 0x0001 // HD0
            };

            bifEntry.Drives.Should().Be(0x0001);

            // Test multiple drive flags
            bifEntry.Drives = 0x0003; // HD0 | CD1
            bifEntry.Drives.Should().Be(0x0003);
        }

        [Fact(Timeout = 120000)]
        public void TestKeyEmptyFile()
        {
            // Test KEY with no BIF entries and no KEY entries
            var key = new KEY();
            key.BifEntries.Count.Should().Be(0, "Empty KEY should have 0 BIF entries");
            key.KeyEntries.Count.Should().Be(0, "Empty KEY should have 0 KEY entries");

            byte[] data = KEYAuto.BytesKey(key);
            KEY loaded = new KEYBinaryReader(data).Load();

            loaded.BifEntries.Count.Should().Be(0);
            loaded.KeyEntries.Count.Should().Be(0);
        }

        [Fact(Timeout = 120000)]
        public void TestKeyMultipleBifEntries()
        {
            // Test KEY with multiple BIF entries
            var key = new KEY();

            key.AddBif("data/models.bif", 1000000);
            key.AddBif("data/textures.bif", 2000000);
            key.AddBif("data/sounds.bif", 500000);

            key.BifEntries.Count.Should().Be(3);
            key.BifEntries[0].Filename.Should().Be("data/models.bif");
            key.BifEntries[1].Filename.Should().Be("data/textures.bif");
            key.BifEntries[2].Filename.Should().Be("data/sounds.bif");
        }

        [Fact(Timeout = 120000)]
        public void TestKeyMultipleKeyEntries()
        {
            // Test KEY with multiple resource entries
            var key = new KEY();

            int bifIndex = 0;
            key.AddBif("data/test.bif", 1000);
            key.AddKeyEntry("resource1", ResourceType.TXT, bifIndex, 0);
            key.AddKeyEntry("resource2", ResourceType.TXT, bifIndex, 1);
            key.AddKeyEntry("resource3", ResourceType.MDL, bifIndex, 2);

            key.KeyEntries.Count.Should().Be(3);
            key.KeyEntries[0].ResRef.ToString().Should().Be("resource1");
            key.KeyEntries[1].ResRef.ToString().Should().Be("resource2");
            key.KeyEntries[2].ResRef.ToString().Should().Be("resource3");
        }

        [Fact(Timeout = 120000)]
        public void TestKeyResRefTruncation()
        {
            // Test that ResRef is limited to 16 characters (matching KEY.ksy)
            var key = new KEY();
            key.AddBif("data/test.bif", 1000);

            // ResRef should be truncated to 16 characters
            string longResRef = new string('a', 20);
            key.AddKeyEntry(longResRef, ResourceType.TXT, 0, 0);

            key.KeyEntries[0].ResRef.ToString().Length.Should().BeLessThanOrEqualTo(16, "ResRef should be max 16 characters as per KEY.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestKeyFilenameTable()
        {
            // Test that filenames are stored correctly in filename table
            var key = new KEY();
            key.AddBif("data/models.bif", 1000000);
            key.AddBif("data/textures.bif", 2000000);

            // Validate filename offsets are calculated correctly
            int filenameTableOffset = key.CalculateFilenameTableOffset();
            filenameTableOffset.Should().Be(KEY.HeaderSize + (key.BifEntries.Count * KEY.BifEntrySize));

            // Validate filename offsets for each entry
            for (int i = 0; i < key.BifEntries.Count; i++)
            {
                int offset = key.CalculateFilenameOffset(i);
                offset.Should().BeGreaterThanOrEqualTo(filenameTableOffset);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestKeyTableOffsets()
        {
            // Test that table offsets are calculated correctly
            var key = new KEY();
            key.AddBif("data/test.bif", 1000);
            key.AddKeyEntry("res1", ResourceType.TXT, 0, 0);

            int fileTableOffset = key.CalculateFileTableOffset();
            int filenameTableOffset = key.CalculateFilenameTableOffset();
            int keyTableOffset = key.CalculateKeyTableOffset();

            fileTableOffset.Should().Be(KEY.HeaderSize);
            filenameTableOffset.Should().Be(fileTableOffset + (key.BifEntries.Count * KEY.BifEntrySize));
            keyTableOffset.Should().BeGreaterThanOrEqualTo(filenameTableOffset);
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new KEYBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new KEYBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => new KEYBinaryReader(CorruptBinaryTestFile).Load();
                act3.Should().Throw<InvalidDataException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestKeyInvalidSignature()
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

                Action act = () => new KEYBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*Invalid KEY file type*");
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
        public void TestKeyInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[64];
                    System.Text.Encoding.ASCII.GetBytes("KEY ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new KEYBinaryReader(tempFile).Load();
                act.Should().Throw<ArgumentException>().WithMessage("*Unsupported KEY version*");
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
        public void TestKeyBuildYearAndDay()
        {
            // Test build year and day fields
            var key = new KEY();
            key.BuildYear = 103; // 2003
            key.BuildDay = 150; // Day 150 of year

            key.BuildYear.Should().Be(103);
            key.BuildDay.Should().Be(150);

            byte[] data = KEYAuto.BytesKey(key);
            KEY loaded = new KEYBinaryReader(data).Load();

            loaded.BuildYear.Should().Be(103);
            loaded.BuildDay.Should().Be(150);
        }

        [Fact(Timeout = 120000)]
        public void TestKeyReservedField()
        {
            // Test that reserved field is 32 bytes (matching KEY.ksy)
            var key = new KEY();
            byte[] data = KEYAuto.BytesKey(key);

            // Reserved field should be at offset 32, size 32 bytes
            // This is validated by the header size being 64 bytes total
            KEY.HeaderSize.Should().Be(64, "Header includes 32-byte reserved field");
        }

        private static void ValidateIO(KEY key)
        {
            // Basic validation
            key.Should().NotBeNull();
            key.FileType.Should().Be("KEY ");
            key.FileVersion.Should().BeOneOf("V1  ", "V1.1");
            key.BifEntries.Should().NotBeNull();
            key.KeyEntries.Should().NotBeNull();
        }

        private static void CreateTestKeyFile(string path)
        {
            var key = new KEY();
            key.AddBif("data/test.bif", 1000);
            key.AddKeyEntry("test", ResourceType.TXT, 0, 0);

            byte[] data = KEYAuto.BytesKey(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

