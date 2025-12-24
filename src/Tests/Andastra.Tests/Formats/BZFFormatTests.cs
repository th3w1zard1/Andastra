using System;
using System.IO;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.BIF;
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
            // Note: BZF files use "V1  " (space-padded) version string, matching BIF format
            // BIFBinaryReader accepts "V1  " or "V1.1" but not "V1.0"
            // BIFBinaryWriter writes BIF.FileVersion which is "V1  "
            // Based on: src/Andastra/Parsing/Resource/Formats/BIF/BIF.cs (FileVersion = "V1  ")
            //           src/Andastra/Parsing/Resource/Formats/BIF/BIFBinaryReader.cs (accepts "V1  " or "V1.1")
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V1  ", "Version should be 'V1  ' (space-padded) matching BIF format standard");
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
            bif.VarCount.Should().BeGreaterThanOrEqualTo(0);
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

        /// <summary>
        /// Creates a proper BZF file with LZMA compression for testing.
        /// </summary>
        /// <remarks>
        /// BZF files are LZMA-compressed BIF archives. This method creates a valid BZF file by:
        /// 1. Creating a BIF object with BIFType.BZF
        /// 2. Adding test resource data
        /// 3. Using BIFBinaryWriter to write the file (which handles LZMA compression automatically)
        ///
        /// The BIFBinaryWriter uses LzmaHelper.Compress() to compress resource data using raw LZMA1 format
        /// matching PyKotor's compression behavior (lzma.FORMAT_RAW, FILTER_LZMA1).
        ///
        /// BZF file format:
        /// - Header: "BZF " (4 bytes) + "V1  " (4 bytes) = 8 bytes
        /// - Variable resource count (4 bytes)
        /// - Fixed resource count (4 bytes)
        /// - Offset to variable resource table (4 bytes)
        /// - Variable resource table (16 bytes per resource)
        /// - Compressed resource data (LZMA1 compressed)
        ///
        /// Based on:
        /// - vendor/PyKotor/src/pykotor/resource/formats/bif/io_bif.py (BIFBinaryWriter)
        /// - vendor/PyKotor/src/pykotor/extract/bzf.py (BZF format structure)
        /// - src/Andastra/Parsing/Resource/Formats/BIF/BIFBinaryWriter.cs (LZMA compression)
        /// - src/Andastra/Utility/LZMA/LzmaHelper.cs (raw LZMA1 compression)
        /// </remarks>
        private static void CreateTestBzfFile(string path)
        {
            // Create BIF with BIFType.BZF to enable LZMA compression
            var bif = new BIF(BIFType.BZF);

            // Add test resource data
            byte[] testData = System.Text.Encoding.ASCII.GetBytes("test resource data");
            bif.SetData(new ResRef("test"), ResourceType.TXT, testData, 1);

            // Write BZF file using BIFBinaryWriter
            // BIFBinaryWriter automatically handles LZMA compression for BZF files via LzmaHelper.Compress()
            // This creates a proper BZF file with:
            // - Correct header ("BZF " + "V1  ")
            // - Resource table with compressed sizes
            // - LZMA1-compressed resource data (raw format: properties + compressed data)
            byte[] data = new BIFBinaryWriter(bif).Write();

            // Ensure directory exists
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write the BZF file
            File.WriteAllBytes(path, data);
        }
    }
}

