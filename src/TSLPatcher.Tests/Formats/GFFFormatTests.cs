using System;
using System.IO;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Resources;
using CSharpKOTOR.Tests.Common;
using FluentAssertions;
using Xunit;
using static CSharpKOTOR.Formats.GFF.GFFAuto;

namespace CSharpKOTOR.Tests.Formats
{

    /// <summary>
    /// Tests for GFF binary I/O.
    /// 1:1 port of Python test_gff.py from tests/resource/formats/test_gff.py
    /// </summary>
    public class GFFFormatTests
    {
        private static readonly string TestGffFile = TestFileHelper.GetPath("test.gff");
        private static readonly string CorruptGffFile = TestFileHelper.GetPath("test_corrupted.gff");

        [Fact]
        public void TestBinaryIO()
        {
            // Read GFF file
            GFF gff = new GFFBinaryReader(TestGffFile).Load();
            ValidateIO(gff);

            // Write and re-read to validate round-trip
            byte[] data = new GFFBinaryWriter(gff).Write();
            gff = new GFFBinaryReader(data).Load();
            ValidateIO(gff);
        }

        private static void ValidateIO(GFF gff)
        {
            gff.Root.GetUInt8("uint8").Should().Be(255);
            gff.Root.GetInt8("int8").Should().Be(-127);
            gff.Root.GetUInt16("uint16").Should().Be(0xFFFF);
            gff.Root.GetInt16("int16").Should().Be(-32768);
            gff.Root.GetUInt32("uint32").Should().Be(0xFFFFFFFF);
            gff.Root.GetInt32("int32").Should().Be(-2147483648);
            gff.Root.GetUInt64("uint64").Should().Be(4294967296);

            gff.Root.GetSingle("single").Should().BeApproximately(12.34567f, 0.00001f);
            gff.Root.GetDouble("double").Should().BeApproximately(12.345678901234, 0.00000000001);

            gff.Root.GetValue("string").Should().Be("abcdefghij123456789");
            gff.Root.GetResRef("resref").Should().Be(new ResRef("resref01"));
            gff.Root.GetBinary("binary").Should().Equal(System.Text.Encoding.ASCII.GetBytes("binarydata"));

            gff.Root.GetVector4("orientation").Should().Be(new Vector4(1, 2, 3, 4));
            gff.Root.GetVector3("position").Should().Be(new Vector3(11, 22, 33));

            LocalizedString locstring = gff.Root.GetLocString("locstring");
            locstring.StringRef.Should().Be(-1);
            locstring.Count.Should().Be(2);
            locstring.Get(Language.English, Gender.Male).Should().Be("male_eng");
            locstring.Get(Language.German, Gender.Female).Should().Be("fem_german");

            gff.Root.GetStruct("child_struct").GetUInt8("child_uint8").Should().Be(4);
            gff.Root.GetList("list").At(0).StructId.Should().Be(1);
            gff.Root.GetList("list").At(1).StructId.Should().Be(2);
        }

        [Fact]
        public void TestReadRaises()
        {
            // test_read_raises from Python
            // Test directory access
            Action act1 = () => new GFFBinaryReader(".").Load();
            act1.Should().Throw<Exception>(); // UnauthorizedAccessException or IOException

            // Test file not found
            Action act2 = () => new GFFBinaryReader("./thisfiledoesnotexist").Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test corrupted file
            Action act3 = () => new GFFBinaryReader(CorruptGffFile).Load();
            act3.Should().Throw<InvalidDataException>();
        }

        [Fact]
        public void TestWriteRaises()
        {
            // test_write_raises from Python
            var gff = new GFF();

            // Test writing to directory (should raise PermissionError on Windows, IsADirectoryError on Unix)
            // Python: write_gff(GFF(), ".", ResourceType.GFF)
            Action act1 = () => WriteGff(gff, ".", ResourceType.GFF);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Test invalid resource type (Python raises ValueError for ResourceType.INVALID)
            // Python: write_gff(GFF(), ".", ResourceType.INVALID)
            Action act2 = () => WriteGff(gff, ".", ResourceType.INVALID);
            act2.Should().Throw<ArgumentException>().WithMessage("*Unsupported format*");
        }

        [Fact]
        public void TestToRawDataSimpleReadSizeUnchanged()
        {
            // test_to_raw_data_simple_read_size_unchanged from Python
            if (!File.Exists(TestGffFile))
            {
                return; // Skip if test file doesn't exist
            }

            byte[] originalData = File.ReadAllBytes(TestGffFile);
            GFF gff = new GFFBinaryReader(originalData).Load();

            byte[] rawData = new GFFBinaryWriter(gff).Write();

            rawData.Length.Should().Be(originalData.Length, "Size of raw data has changed.");
        }

        [Fact]
        public void TestWriteToFileValidPathSizeUnchanged()
        {
            // test_write_to_file_valid_path_size_unchanged from Python
            string gitTestFile = TestFileHelper.GetPath("test.git");
            if (!File.Exists(gitTestFile))
            {
                return; // Skip if test file doesn't exist
            }

            long originalSize = new FileInfo(gitTestFile).Length;
            GFF gff = new GFFBinaryReader(gitTestFile).Load();

            string tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.git");
            try
            {
                File.WriteAllBytes(tempFile, new GFFBinaryWriter(gff).Write());

                File.Exists(tempFile).Should().BeTrue("GFF output file was not created.");
                new FileInfo(tempFile).Length.Should().Be(originalSize, "File size has changed.");
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

    }
}

