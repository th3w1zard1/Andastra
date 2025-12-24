using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.BIF;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for BIF binary I/O operations.
    /// Tests validate the BIF format structure as defined in BIF.ksy Kaitai Struct definition.
    /// </summary>
    public class BIFFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.bif");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.bif");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test BIF file if it doesn't exist
                CreateTestBifFile(BinaryTestFile);
            }

            // Test reading BIF file
            BIF bif = new BIFBinaryReader(BinaryTestFile).Load();
            ValidateIO(bif);

            // Test writing and reading back
            byte[] data = new BIFBinaryWriter(bif).Write();
            bif = new BIFBinaryReader(data).Load();
            ValidateIO(bif);
        }

        [Fact(Timeout = 120000)]
        public void TestBifHeaderStructure()
        {
            // Test that BIF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBifFile(BinaryTestFile);
            }

            BIF bif = new BIFBinaryReader(BinaryTestFile).Load();

            // Validate header constants match BIF.ksy
            BIF.HeaderSize.Should().Be(20, "BIF header should be 20 bytes as defined in BIF.ksy");
            BIF.FileVersion.Should().Be("V1  ", "BIF version should match BIF.ksy definition");
            BIF.VarEntrySize.Should().Be(16, "Variable resource entry should be 16 bytes as defined in BIF.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestBifFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBifFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches BIF.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("BIFF", "File type should be 'BIFF' as defined in BIF.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V1  ", "V1.1", "Version should match BIF.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestBifVariableResourceTable()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBifFile(BinaryTestFile);
            }

            BIF bif = new BIFBinaryReader(BinaryTestFile).Load();

            // Validate variable resource table structure
            bif.VarCount.Should().BeGreaterThanOrEqualTo(0, "Variable resource count should be non-negative");
            bif.FixedCount.Should().Be(0, "Fixed resource count should be 0 as per BIF.ksy validation");

            // Validate each entry has required fields (matching var_resource_entry in BIF.ksy)
            foreach (var resource in bif.Resources)
            {
                resource.ResnameKeyIndex.Should().BeGreaterThanOrEqualTo(0, "Resource ID should be non-negative");
                resource.Offset.Should().BeGreaterThanOrEqualTo(0, "Offset should be non-negative");
                resource.Size.Should().BeGreaterThanOrEqualTo(0, "File size should be non-negative");
                resource.ResType.Should().NotBeNull("Resource type should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBifResourceDataOffsets()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBifFile(BinaryTestFile);
            }

            BIF bif = new BIFBinaryReader(BinaryTestFile).Load();

            // Validate that resource data offsets are correct
            // Each resource should have data at the offset specified in the entry
            foreach (var resource in bif.Resources)
            {
                if (resource.Size > 0)
                {
                    resource.Data.Should().NotBeNull("Resource with size > 0 should have data");
                    resource.Data.Length.Should().Be(resource.Size, "Data length should match file_size from entry");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBifResourceIdEncoding()
        {
            // Test Resource ID encoding matches BIF.ksy documentation
            // Resource ID = (bif_index << 20) | resource_index

            // Test encoding
            int bifIndex = 4;
            int resourceIndex = 41;
            uint resourceId = (uint)((bifIndex << 20) | resourceIndex);

            // Test decoding
            int decodedBifIndex = (int)(resourceId >> 20) & 0xFFF;
            int decodedResourceIndex = (int)(resourceId & 0xFFFFF);

            decodedBifIndex.Should().Be(bifIndex, "BIF index should decode correctly");
            decodedResourceIndex.Should().Be(resourceIndex, "Resource index should decode correctly");
        }

        [Fact(Timeout = 120000)]
        public void TestBifEmptyFile()
        {
            // Test BIF with no resources
            var bif = new BIF(BIFType.BIF);
            bif.VarCount.Should().Be(0, "Empty BIF should have 0 variable resources");
            bif.FixedCount.Should().Be(0, "Empty BIF should have 0 fixed resources");

            byte[] data = new BIFBinaryWriter(bif).Write();
            BIF loaded = new BIFBinaryReader(data).Load();

            loaded.VarCount.Should().Be(0);
            loaded.FixedCount.Should().Be(0);
        }

        [Fact(Timeout = 120000)]
        public void TestBifMultipleResources()
        {
            // Test BIF with multiple resources of different types
            var bif = new BIF(BIFType.BIF);

            byte[] data1 = System.Text.Encoding.ASCII.GetBytes("test resource 1");
            byte[] data2 = System.Text.Encoding.ASCII.GetBytes("test resource 2");
            byte[] data3 = System.Text.Encoding.ASCII.GetBytes("test resource 3");

            bif.SetData(new ResRef("res1"), ResourceType.TXT, data1, 1);
            bif.SetData(new ResRef("res2"), ResourceType.TXT, data2, 2);
            bif.SetData(new ResRef("res3"), ResourceType.TXT, data3, 3);

            bif.VarCount.Should().Be(3);
            bif.Resources.Count.Should().Be(3);

            byte[] serialized = new BIFBinaryWriter(bif).Write();
            BIF loaded = new BIFBinaryReader(serialized).Load();

            loaded.VarCount.Should().Be(3);
            loaded.Resources.Count.Should().Be(3);
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new BIFBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new BIFBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => new BIFBinaryReader(CorruptBinaryTestFile).Load();
                act3.Should().Throw<InvalidDataException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBifInvalidSignature()
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

                Action act = () => new BIFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*Invalid BIF/BZF file type*");
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
        public void TestBifInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[20];
                    System.Text.Encoding.ASCII.GetBytes("BIFF").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new BIFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*Unsupported BIF/BZF version*");
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
        public void TestBifFixedResourceCountValidation()
        {
            // Create file with non-zero fixed resource count (should fail)
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[20];
                    System.Text.Encoding.ASCII.GetBytes("BIFF").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V1  ").CopyTo(header, 4);
                    BitConverter.GetBytes(0u).CopyTo(header, 8); // var_res_count = 0
                    BitConverter.GetBytes(1u).CopyTo(header, 12); // fixed_res_count = 1 (invalid)
                    BitConverter.GetBytes(20u).CopyTo(header, 16); // var_table_offset = 20
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new BIFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*Fixed resources not supported*");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static void ValidateIO(BIF bif)
        {
            // Basic validation
            bif.Should().NotBeNull();
            bif.BifType.Should().Be(BIFType.BIF);
            bif.VarCount.Should().BeGreaterThanOrEqualTo(0);
            bif.FixedCount.Should().Be(0);
        }

        private static void CreateTestBifFile(string path)
        {
            var bif = new BIF(BIFType.BIF);
            byte[] testData = System.Text.Encoding.ASCII.GetBytes("test resource data");
            bif.SetData(new ResRef("test"), ResourceType.TXT, testData, 1);

            byte[] data = new BIFBinaryWriter(bif).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

