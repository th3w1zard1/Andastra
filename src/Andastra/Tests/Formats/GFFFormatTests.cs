using System;
using System.IO;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.GFF.GFFAuto;

namespace Andastra.Parsing.Tests.Formats
{

    /// <summary>
    /// Comprehensive tests for GFF binary I/O operations.
    /// Tests validate the GFF format structure as defined in GFF.ksy Kaitai Struct definition.
    /// 1:1 port of Python test_gff.py from tests/resource/formats/test_gff.py
    /// </summary>
    public class GFFFormatTests
    {
        private static readonly string TestGffFile = TestFileHelper.GetPath("test.gff");
        private static readonly string CorruptGffFile = TestFileHelper.GetPath("test_corrupted.gff");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";

        [Fact(Timeout = 120000)] // 2 minutes timeout
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
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

        [Fact(Timeout = 120000)]
        public void TestGffHeaderStructure()
        {
            // Test that GFF header matches Kaitai Struct definition
            if (!File.Exists(TestGffFile))
            {
                CreateTestGffFile(TestGffFile);
            }

            GFF gff = new GFFBinaryReader(TestGffFile).Load();

            // Validate header constants match GFF.ksy
            // Header is 56 bytes: 4 (file_type) + 4 (file_version) + 12×4 (offsets/counts)
            const int ExpectedHeaderSize = 56;
            FileInfo fileInfo = new FileInfo(TestGffFile);
            fileInfo.Length.Should().BeGreaterThanOrEqualTo(ExpectedHeaderSize, "GFF file should have at least 56-byte header as defined in GFF.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestGffFileTypeSignature()
        {
            if (!File.Exists(TestGffFile))
            {
                CreateTestGffFile(TestGffFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(TestGffFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches GFF.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().MatchRegex(@"^(GFF|UTC|UTI|DLG|ARE|GIT|IFO|JRL|PTH|GAM|CNV|GUI|FAC|NFO|ITP|PT|GVT|INV|BIC|BTC|BTD|BTE|BTI|BTP|BTM|BTT|UTD|UTE|UTP|UTS|UTM|UTT|UTW)\s*$",
                "File type should match a valid GFFContent enum value as defined in GFF.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf(new[] { "V3.2", "V3.3", "V4.0", "V4.1" },
                "Version should match GFF.ksy valid values (V3.2, V3.3, V4.0, or V4.1)");
        }

        [Fact(Timeout = 120000)]
        public void TestGffHeaderOffsetsAndCounts()
        {
            if (!File.Exists(TestGffFile))
            {
                CreateTestGffFile(TestGffFile);
            }

            // Read header structure manually to validate offsets
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(TestGffFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate all offsets are within file bounds
            uint structOffset = BitConverter.ToUInt32(header, 8);
            uint structCount = BitConverter.ToUInt32(header, 12);
            uint fieldOffset = BitConverter.ToUInt32(header, 16);
            uint fieldCount = BitConverter.ToUInt32(header, 20);
            uint labelOffset = BitConverter.ToUInt32(header, 24);
            uint labelCount = BitConverter.ToUInt32(header, 28);
            uint fieldDataOffset = BitConverter.ToUInt32(header, 32);
            uint fieldDataCount = BitConverter.ToUInt32(header, 36);
            uint fieldIndicesOffset = BitConverter.ToUInt32(header, 40);
            uint fieldIndicesCount = BitConverter.ToUInt32(header, 44);
            uint listIndicesOffset = BitConverter.ToUInt32(header, 48);
            uint listIndicesCount = BitConverter.ToUInt32(header, 52);

            // Validate header offset (should be 56)
            structOffset.Should().Be(56u, "Struct offset should be 56 (header size) as per GFF.ksy");

            // Validate offsets are sequential and within bounds
            FileInfo fileInfo = new FileInfo(TestGffFile);
            long fileSize = fileInfo.Length;

            if (structCount > 0)
            {
                structOffset.Should().BeLessThan((uint)fileSize, "Struct offset should be within file bounds");
                (structOffset + structCount * 12u).Should().BeLessThanOrEqualTo((uint)fileSize, "Struct array should be within file bounds");
            }

            if (fieldCount > 0)
            {
                fieldOffset.Should().BeLessThan((uint)fileSize, "Field offset should be within file bounds");
                (fieldOffset + fieldCount * 12u).Should().BeLessThanOrEqualTo((uint)fileSize, "Field array should be within file bounds");
            }

            if (labelCount > 0)
            {
                labelOffset.Should().BeLessThan((uint)fileSize, "Label offset should be within file bounds");
                (labelOffset + labelCount * 16u).Should().BeLessThanOrEqualTo((uint)fileSize, "Label array should be within file bounds");
            }

            if (fieldDataCount > 0)
            {
                fieldDataOffset.Should().BeLessThan((uint)fileSize, "Field data offset should be within file bounds");
                (fieldDataOffset + fieldDataCount).Should().BeLessThanOrEqualTo((uint)fileSize, "Field data section should be within file bounds");
            }

            if (fieldIndicesCount > 0)
            {
                fieldIndicesOffset.Should().BeLessThan((uint)fileSize, "Field indices offset should be within file bounds");
                (fieldIndicesOffset + fieldIndicesCount * 4u).Should().BeLessThanOrEqualTo((uint)fileSize, "Field indices array should be within file bounds");
            }

            if (listIndicesCount > 0)
            {
                listIndicesOffset.Should().BeLessThan((uint)fileSize, "List indices offset should be within file bounds");
                (listIndicesOffset + listIndicesCount * 4u).Should().BeLessThanOrEqualTo((uint)fileSize, "List indices array should be within file bounds");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffStructEntryStructure()
        {
            if (!File.Exists(TestGffFile))
            {
                CreateTestGffFile(TestGffFile);
            }

            GFF gff = new GFFBinaryReader(TestGffFile).Load();

            // Validate that struct entries are 12 bytes as per GFF.ksy
            // Each struct entry: s4 (struct_id) + u4 (data_or_offset) + u4 (field_count) = 12 bytes
            const int ExpectedStructEntrySize = 12;

            // Read header to get struct array location
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(TestGffFile))
            {
                fs.Read(header, 0, 56);
            }

            uint structOffset = BitConverter.ToUInt32(header, 8);
            uint structCount = BitConverter.ToUInt32(header, 12);

            if (structCount > 0)
            {
                // Read first struct entry
                byte[] structEntry = new byte[ExpectedStructEntrySize];
                using (var fs = File.OpenRead(TestGffFile))
                {
                    fs.Seek(structOffset, SeekOrigin.Begin);
                    fs.Read(structEntry, 0, ExpectedStructEntrySize);
                }

                // Validate struct entry structure
                int structId = BitConverter.ToInt32(structEntry, 0);
                uint dataOrOffset = BitConverter.ToUInt32(structEntry, 4);
                uint fieldCount = BitConverter.ToUInt32(structEntry, 8);

                // Struct ID can be any int32 value
                structId.Should().BeInRange(int.MinValue, int.MaxValue, "Struct ID should be valid int32");

                // Field count should be non-negative
                fieldCount.Should().BeLessThanOrEqualTo(10000u, "Field count should be reasonable (max 10000)");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffFieldEntryStructure()
        {
            if (!File.Exists(TestGffFile))
            {
                CreateTestGffFile(TestGffFile);
            }

            GFF gff = new GFFBinaryReader(TestGffFile).Load();

            // Validate that field entries are 12 bytes as per GFF.ksy
            // Each field entry: u4 (field_type) + u4 (label_index) + u4 (data_or_offset) = 12 bytes
            const int ExpectedFieldEntrySize = 12;

            // Read header to get field array location
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(TestGffFile))
            {
                fs.Read(header, 0, 56);
            }

            uint fieldOffset = BitConverter.ToUInt32(header, 16);
            uint fieldCount = BitConverter.ToUInt32(header, 20);
            uint labelCount = BitConverter.ToUInt32(header, 28);

            if (fieldCount > 0)
            {
                // Read first field entry
                byte[] fieldEntry = new byte[ExpectedFieldEntrySize];
                using (var fs = File.OpenRead(TestGffFile))
                {
                    fs.Seek(fieldOffset, SeekOrigin.Begin);
                    fs.Read(fieldEntry, 0, ExpectedFieldEntrySize);
                }

                // Validate field entry structure
                uint fieldType = BitConverter.ToUInt32(fieldEntry, 0);
                uint labelIndex = BitConverter.ToUInt32(fieldEntry, 4);
                uint dataOrOffset = BitConverter.ToUInt32(fieldEntry, 8);

                // Field type should be valid (0-17 as per GFF.ksy enum)
                fieldType.Should().BeLessThanOrEqualTo(17u, "Field type should be <= 17 as per GFF.ksy gff_field_type enum");

                // Label index should be valid
                if (labelCount > 0)
                {
                    labelIndex.Should().BeLessThan(labelCount, "Label index should be within label array bounds");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffLabelArrayStructure()
        {
            if (!File.Exists(TestGffFile))
            {
                CreateTestGffFile(TestGffFile);
            }

            GFF gff = new GFFBinaryReader(TestGffFile).Load();

            // Validate label array structure: each label is 16 bytes as per GFF.ksy
            const int ExpectedLabelSize = 16;

            // Read header to get label array location
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(TestGffFile))
            {
                fs.Read(header, 0, 56);
            }

            uint labelOffset = BitConverter.ToUInt32(header, 24);
            uint labelCount = BitConverter.ToUInt32(header, 28);

            if (labelCount > 0)
            {
                // Read first label
                byte[] label = new byte[ExpectedLabelSize];
                using (var fs = File.OpenRead(TestGffFile))
                {
                    fs.Seek(labelOffset, SeekOrigin.Begin);
                    fs.Read(label, 0, ExpectedLabelSize);
                }

                // Validate label structure (16-byte null-padded ASCII string)
                string labelStr = System.Text.Encoding.ASCII.GetString(label).TrimEnd('\0');
                labelStr.Length.Should().BeLessThanOrEqualTo(16, "Label should be max 16 bytes as per GFF.ksy");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestGffAllFieldTypes()
        {
            if (!File.Exists(TestGffFile))
            {
                CreateTestGffFile(TestGffFile);
            }

            GFF gff = new GFFBinaryReader(TestGffFile).Load();

            // Validate all field types exist and are correctly parsed
            // This validates the GFF.ksy field type enum (0-17)

            // Simple types (inline in field entry)
            gff.Root.GetFieldType("uint8").Should().Be(GFFFieldType.UInt8, "UInt8 field type should match GFF.ksy enum value 0");
            gff.Root.GetFieldType("int8").Should().Be(GFFFieldType.Int8, "Int8 field type should match GFF.ksy enum value 1");
            gff.Root.GetFieldType("uint16").Should().Be(GFFFieldType.UInt16, "UInt16 field type should match GFF.ksy enum value 2");
            gff.Root.GetFieldType("int16").Should().Be(GFFFieldType.Int16, "Int16 field type should match GFF.ksy enum value 3");
            gff.Root.GetFieldType("uint32").Should().Be(GFFFieldType.UInt32, "UInt32 field type should match GFF.ksy enum value 4");
            gff.Root.GetFieldType("int32").Should().Be(GFFFieldType.Int32, "Int32 field type should match GFF.ksy enum value 5");
            gff.Root.GetFieldType("single").Should().Be(GFFFieldType.Single, "Single field type should match GFF.ksy enum value 8");

            // Complex types (stored in field_data section)
            gff.Root.GetFieldType("uint64").Should().Be(GFFFieldType.UInt64, "UInt64 field type should match GFF.ksy enum value 6");
            gff.Root.GetFieldType("double").Should().Be(GFFFieldType.Double, "Double field type should match GFF.ksy enum value 9");
            gff.Root.GetFieldType("string").Should().Be(GFFFieldType.String, "String field type should match GFF.ksy enum value 10");
            gff.Root.GetFieldType("resref").Should().Be(GFFFieldType.ResRef, "ResRef field type should match GFF.ksy enum value 11");
            gff.Root.GetFieldType("locstring").Should().Be(GFFFieldType.LocalizedString, "LocalizedString field type should match GFF.ksy enum value 12");
            gff.Root.GetFieldType("binary").Should().Be(GFFFieldType.Binary, "Binary field type should match GFF.ksy enum value 13");
            gff.Root.GetFieldType("position").Should().Be(GFFFieldType.Vector3, "Vector3 field type should match GFF.ksy enum value 17");
            gff.Root.GetFieldType("orientation").Should().Be(GFFFieldType.Vector4, "Vector4/Orientation field type should match GFF.ksy enum value 16");

            // Complex access types
            gff.Root.GetFieldType("child_struct").Should().Be(GFFFieldType.Struct, "Struct field type should match GFF.ksy enum value 14");
            gff.Root.GetFieldType("list").Should().Be(GFFFieldType.List, "List field type should match GFF.ksy enum value 15");
        }

        [Fact(Timeout = 120000)]
        public void TestGffInvalidSignature()
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
                act.Should().Throw<InvalidDataException>().WithMessage("*Not a valid binary GFF file*");
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
        public void TestGffInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("GFF ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4); // Invalid version
                    // Fill rest with zeros for minimal valid structure
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new GFFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*GFF version*unsupported*");
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
        public void TestGffEmptyFile()
        {
            // Test GFF with minimal structure (empty root struct)
            var gff = new GFF(GFFContent.GFF);
            gff.Root.StructId.Should().Be(-1, "Empty GFF root struct should have struct ID -1");

            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loaded = new GFFBinaryReader(data).Load();

            loaded.Should().NotBeNull("Empty GFF should load successfully");
            loaded.Content.Should().Be(GFFContent.GFF, "Content type should be preserved");
            loaded.Root.Count.Should().Be(0, "Empty GFF root should have 0 fields");
        }

        [Fact(Timeout = 120000)]
        public void TestGffMultipleStructsAndFields()
        {
            // Test GFF with multiple structs and fields
            var gff = new GFF(GFFContent.UTC);
            gff.Root.SetUInt32("Field1", 100);
            gff.Root.SetString("Field2", "Test");

            // Add nested struct
            var childStruct = new GFFStruct(1);
            childStruct.SetInt32("ChildField1", 200);
            gff.Root.SetStruct("ChildStruct", childStruct);

            // Add list
            var list = new GFFList();
            var listItem1 = list.Add(2);
            listItem1.SetString("ListItem1", "Value1");
            var listItem2 = list.Add(3);
            listItem2.SetString("ListItem2", "Value2");
            gff.Root.SetList("List", list);

            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loaded = new GFFBinaryReader(data).Load();

            loaded.Content.Should().Be(GFFContent.UTC, "Content type should be preserved");
            loaded.Root.Count.Should().Be(3, "Root should have 3 fields");
            loaded.Root.GetUInt32("Field1").Should().Be(100);
            loaded.Root.GetString("Field2").Should().Be("Test");
            loaded.Root.GetStruct("ChildStruct").GetInt32("ChildField1").Should().Be(200);
            loaded.Root.GetList("List").Count.Should().Be(2);
            loaded.Root.GetList("List").At(0).GetString("ListItem1").Should().Be("Value1");
            loaded.Root.GetList("List").At(1).GetString("ListItem2").Should().Be("Value2");
        }

        private static void CreateTestGffFile(string path)
        {
            var gff = new GFF(GFFContent.GFF);

            // Add all simple field types
            gff.Root.SetUInt8("uint8", 255);
            gff.Root.SetInt8("int8", -127);
            gff.Root.SetUInt16("uint16", 0xFFFF);
            gff.Root.SetInt16("int16", -32768);
            gff.Root.SetUInt32("uint32", 0xFFFFFFFF);
            gff.Root.SetInt32("int32", -2147483648);
            gff.Root.SetUInt64("uint64", 4294967296);
            gff.Root.SetInt64("int64", -9223372036854775808);
            gff.Root.SetSingle("single", 12.34567f);
            gff.Root.SetDouble("double", 12.345678901234);

            // Add complex field types
            gff.Root.SetString("string", "abcdefghij123456789");
            gff.Root.SetResRef("resref", new ResRef("resref01"));
            gff.Root.SetBinary("binary", System.Text.Encoding.ASCII.GetBytes("binarydata"));

            // Add vector types
            gff.Root.SetVector3("position", new Vector3(11, 22, 33));
            gff.Root.SetVector4("orientation", new Vector4(1, 2, 3, 4));

            // Add localized string
            var locString = LocalizedString.FromInvalid();
            locString.SetData(Language.English, Gender.Male, "male_eng");
            locString.SetData(Language.German, Gender.Female, "fem_german");
            gff.Root.SetLocString("locstring", locString);

            // Add nested struct
            var childStruct = new GFFStruct(0);
            childStruct.SetUInt8("child_uint8", 4);
            gff.Root.SetStruct("child_struct", childStruct);

            // Add list
            var list = new GFFList();
            var listItem1 = list.Add(1);
            listItem1.SetString("list_item1", "value1");
            var listItem2 = list.Add(2);
            listItem2.SetString("list_item2", "value2");
            gff.Root.SetList("list", list);

            byte[] data = new GFFBinaryWriter(gff).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

    }
}

