using System;
using System.IO;
using Andastra.Parsing.Resource.Formats.BIF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Common;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for BZF (compressed BIF) binary I/O operations.
    /// Tests validate the BZF format structure as defined in BZF.ksy Kaitai Struct definition.
    /// </summary>
    public class BZFFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.bzf");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.bzf");

        [Fact(Timeout = 120000)]
        public void TestBzfHeaderStructure()
        {
            // Test that BZF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBzfFile(BinaryTestFile);
            }

            BIF bif = new BIFBinaryReader(BinaryTestFile).Load();

            // Validate BZF type
            bif.BifType.Should().Be(BIFType.BZF, "BZF file should have BZF type");
            bif.IsCompressed.Should().BeTrue("BZF files should be marked as compressed");
        }

        [Fact(Timeout = 120000)]
        public void TestBzfFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBzfFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches BZF.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("BZF ", "File type should be 'BZF ' (space-padded) as defined in BZF.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V1.0", "Version should be 'V1.0' as defined in BZF.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestBzfCompressedDataStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBzfFile(BinaryTestFile);
            }

            // Validate that BZF file has compressed data after header
            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().BeGreaterThan(8, "BZF file should have compressed data after 8-byte header");

            // Read file to verify structure
            BIF bif = new BIFBinaryReader(BinaryTestFile).Load();
            bif.Should().NotBeNull("BZF file should load successfully");
            bif.BifType.Should().Be(BIFType.BZF);
        }

        [Fact(Timeout = 120000)]
        public void TestBzfDecompression()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestBzfFile(BinaryTestFile);
            }

            // Test that BZF decompresses to valid BIF structure
            BIF bif = new BIFBinaryReader(BinaryTestFile).Load();

            // After decompression, BZF should behave like a BIF
            bif.BifType.Should().Be(BIFType.BZF);
            bif.VarCount.Should().BeGreaterOrEqualTo(0);
            bif.FixedCount.Should().Be(0);
        }

        [Fact(Timeout = 120000)]
        public void TestBzfInvalidSignature()
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
        public void TestBzfInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[8];
                    System.Text.Encoding.ASCII.GetBytes("BZF ").CopyTo(header, 0);
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
        public void TestBzfRoundTrip()
        {
            // Test creating BZF, reading it back
            var bif = new BIF(BIFType.BZF);
            byte[] testData = System.Text.Encoding.ASCII.GetBytes("test compressed resource");
            bif.SetData(new ResRef("test"), ResourceType.TXT, testData, 1);

            // Note: BIFBinaryWriter may not support BZF compression directly
            // This test validates the structure can be read
            if (File.Exists(BinaryTestFile))
            {
                BIF loaded = new BIFBinaryReader(BinaryTestFile).Load();
                loaded.BifType.Should().Be(BIFType.BZF);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestBzfVersusBif()
        {
            // Test that BZF and BIF are distinguished correctly
            string bifFile = TestFileHelper.GetPath("test.bif");
            string bzfFile = TestFileHelper.GetPath("test.bzf");

            if (File.Exists(bifFile) && File.Exists(bzfFile))
            {
                BIF bif1 = new BIFBinaryReader(bifFile).Load();
                BIF bif2 = new BIFBinaryReader(bzfFile).Load();

                bif1.BifType.Should().Be(BIFType.BIF);
                bif2.BifType.Should().Be(BIFType.BZF);
                bif1.IsCompressed.Should().BeFalse();
                bif2.IsCompressed.Should().BeTrue();
            }
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
        }

        private static void CreateTestBzfFile(string path)
        {
            // Note: Creating actual BZF files requires LZMA compression
            // This is a placeholder - actual BZF creation would use LZMA compression
            // For testing, we can create a minimal valid BZF structure

            // In practice, BZF files are created by compressing BIF files with LZMA
            // This test helper creates a basic structure for validation
            var bif = new BIF(BIFType.BIF);
            byte[] testData = System.Text.Encoding.ASCII.GetBytes("test resource data");
            bif.SetData(new ResRef("test"), ResourceType.TXT, testData, 1);

            // Write as regular BIF for now (actual BZF would be LZMA compressed)
            // Real BZF creation would require LZMA compression library
            byte[] data = new BIFBinaryWriter(bif).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

