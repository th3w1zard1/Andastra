using System;
using System.IO;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTT (Trigger Template) binary I/O operations.
    /// Tests validate the UTT format structure as defined in UTT.ksy Kaitai Struct definition.
    /// UTT files are GFF-based format files that store trigger template definitions including
    /// script hooks, trap properties, and transition data.
    /// </summary>
    public class UTTFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.utt");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.utt");

        [Fact(Timeout = 120000)]
        public void TestUttHeaderStructure()
        {
            // Test that UTT header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUttFile(BinaryTestFile);
            }

            // Read GFF header to validate structure
            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate GFF header constants match UTT.ksy
            gff.Content.Should().Be(GFFContent.UTT, "UTT file should have UTT content type");
        }

        [Fact(Timeout = 120000)]
        public void TestUttFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUttFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTT.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTT ", "File type should be 'UTT ' (space-padded) as defined in UTT.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf(new[] { "V3.2", "V3.3", "V4.0", "V4.1" }, "Version should match UTT.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestUttInvalidSignature()
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
        public void TestUttInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("UTT ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    // Fill rest with zeros for minimal valid GFF structure
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

        [Fact(Timeout = 120000)]
        public void TestUttGffStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUttFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate basic GFF structure
            gff.Should().NotBeNull("GFF object should not be null");
            gff.Content.Should().Be(GFFContent.UTT, "Content should be UTT");
            gff.Root.Should().NotBeNull("Root struct should not be null");
        }

        private static void CreateTestUttFile(string path)
        {
            // Create a minimal valid UTT GFF file
            // UTT files are GFF-based, so we create a minimal GFF structure
            var gff = new GFF(GFFContent.UTT);

            // Add minimal required fields for a trigger template
            var rootStruct = gff.Root;
            rootStruct.SetString("Tag", "TEST_TRIGGER");

            byte[] data = new GFFBinaryWriter(gff).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}
