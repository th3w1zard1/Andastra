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
using UTM = Andastra.Parsing.Resource.Generics.UTM.UTM;
using UTMHelpers = Andastra.Parsing.Resource.Generics.UTM.UTMHelpers;
using UTMItem = Andastra.Parsing.Resource.Generics.UTM.UTMItem;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTM binary I/O operations.
    /// Tests validate the UTM format structure as defined in UTM.ksy Kaitai Struct definition.
    /// UTM files are GFF-based format files with file type signature "UTM ".
    /// </summary>
    public class UTMFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.utm");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.utm");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtmFile(BinaryTestFile);
            }

            // Test reading UTM file
            UTM utm = UTMHelpers.ReadUtm(File.ReadAllBytes(BinaryTestFile));
            ValidateIO(utm);

            // Test writing and reading back
            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            utm = UTMHelpers.ReadUtm(data);
            ValidateIO(utm);
        }

        [Fact(Timeout = 120000)]
        public void TestUtmGffHeaderStructure()
        {
            // Test that UTM GFF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtmFile(BinaryTestFile);
            }

            // Read raw GFF header bytes (56 bytes)
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate file type signature matches UTM.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTM ", "File type should be 'UTM ' (space-padded) as defined in UTM.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTM.ksy valid values");

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
        public void TestUtmFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtmFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTM.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTM ", "File type should be 'UTM ' (space-padded) as defined in UTM.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTM.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestUtmInvalidSignature()
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

                Action act = () => UTMHelpers.ReadUtm(File.ReadAllBytes(tempFile));
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
        public void TestUtmInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("UTM ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => UTMHelpers.ReadUtm(File.ReadAllBytes(tempFile));
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
        public void TestUtmRootStructFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtmFile(BinaryTestFile);
            }

            UTM utm = UTMHelpers.ReadUtm(File.ReadAllBytes(BinaryTestFile));

            // Validate root struct fields exist (as per UTM.ksy documentation)
            // Root struct should contain UTM-specific fields
            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));
            GFFStruct root = gff.Root;

            // Core UTM fields should be present or have defaults
            root.Acquire<ResRef>("ResRef", ResRef.FromBlank()).Should().NotBeNull("ResRef should not be null");
            root.Acquire<LocalizedString>("LocName", LocalizedString.FromInvalid()).Should().NotBeNull("LocName should not be null");
            root.Acquire<string>("Tag", "").Should().NotBeNull("Tag should not be null");
            root.Acquire<int>("MarkUp", 0).Should().BeGreaterThanOrEqualTo(0, "MarkUp should be non-negative");
            root.Acquire<int>("MarkDown", 0).Should().BeGreaterThanOrEqualTo(0, "MarkDown should be non-negative");

            // BuySellFlag should be valid byte (0-3, as it's a 2-bit flag)
            byte? buySellFlag = root.GetUInt8("BuySellFlag");
            if (buySellFlag.HasValue)
            {
                buySellFlag.Value.Should().BeInRange((byte)0, (byte)3, "BuySellFlag should be 0-3 (2-bit flag)");
            }

            // ID should be valid byte if present
            byte? id = root.GetUInt8("ID");
            if (id.HasValue)
            {
                id.Value.Should().BeInRange((byte)0, byte.MaxValue, "ID should be valid byte");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtmBuySellFlagBits()
        {
            // Test BuySellFlag bit extraction (can_buy = bit 0, can_sell = bit 1)
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtmFile(BinaryTestFile);
            }

            UTM utm = UTMHelpers.ReadUtm(File.ReadAllBytes(BinaryTestFile));

            // BuySellFlag bit 0 = can_buy, bit 1 = can_sell
            // Test various flag combinations
            var testCases = new[]
            {
                (flag: (byte)0, canBuy: false, canSell: false),
                (flag: (byte)1, canBuy: true, canSell: false),
                (flag: (byte)2, canBuy: false, canSell: true),
                (flag: (byte)3, canBuy: true, canSell: true),
            };

            foreach (var testCase in testCases)
            {
                var testUtm = new UTM();
                testUtm.CanBuy = testCase.canBuy;
                testUtm.CanSell = testCase.canSell;

                GFF gff = UTMHelpers.DismantleUtm(testUtm, BioWareGame.K2);
                byte? buySellFlagNullable = gff.Root.GetUInt8("BuySellFlag");
                byte buySellFlag = buySellFlagNullable ?? 0;

                buySellFlag.Should().Be(testCase.flag, $"BuySellFlag should be {testCase.flag} for canBuy={testCase.canBuy}, canSell={testCase.canSell}");

                // Round-trip test
                UTM loaded = UTMHelpers.ConstructUtm(gff);
                loaded.CanBuy.Should().Be(testCase.canBuy, "CanBuy should match after round-trip");
                loaded.CanSell.Should().Be(testCase.canSell, "CanSell should match after round-trip");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtmItemListStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtmFile(BinaryTestFile);
            }

            UTM utm = UTMHelpers.ReadUtm(File.ReadAllBytes(BinaryTestFile));

            // Validate ItemList structure
            utm.Items.Should().NotBeNull("ItemList should not be null");

            // Each item should have valid structure
            foreach (var item in utm.Items)
            {
                item.Should().NotBeNull("Item should not be null");
                item.ResRef.Should().NotBeNull("Item ResRef should not be null");
                item.Infinite.Should().BeInRange(0, 1, "Infinite should be 0 or 1");
                item.Droppable.Should().BeInRange(0, 1, "Droppable should be 0 or 1");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtmItemFields()
        {
            // Test individual item fields (InventoryRes, Infinite, Dropable, Repos_PosX, Repos_PosY)
            var utm = new UTM();
            utm.ResRef = new ResRef("test_merchant");
            utm.Tag = "TESTMERCH";
            utm.MarkUp = 10;
            utm.MarkDown = 5;
            utm.CanBuy = true;
            utm.CanSell = true;

            // Add test items
            var item1 = new UTMItem();
            item1.ResRef = new ResRef("item1");
            item1.Infinite = 1;
            item1.Droppable = 0;
            utm.Items.Add(item1);

            var item2 = new UTMItem();
            item2.ResRef = new ResRef("item2");
            item2.Infinite = 0;
            item2.Droppable = 1;
            utm.Items.Add(item2);

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.Items.Count.Should().Be(2, "Should have 2 items");

            var loadedItem1 = loaded.Items[0];
            loadedItem1.ResRef.Should().Be(new ResRef("item1"), "First item ResRef should match");
            loadedItem1.Infinite.Should().Be(1, "First item Infinite should be 1");
            loadedItem1.Droppable.Should().Be(0, "First item Droppable should be 0");

            var loadedItem2 = loaded.Items[1];
            loadedItem2.ResRef.Should().Be(new ResRef("item2"), "Second item ResRef should match");
            loadedItem2.Infinite.Should().Be(0, "Second item Infinite should be 0");
            loadedItem2.Droppable.Should().Be(1, "Second item Droppable should be 1");
        }

        [Fact(Timeout = 120000)]
        public void TestUtmEmptyFile()
        {
            // Test UTM with minimal structure
            var utm = new UTM();
            utm.ResRef = ResRef.FromBlank();
            utm.Name = LocalizedString.FromInvalid();
            utm.Tag = "";
            utm.MarkUp = 0;
            utm.MarkDown = 0;
            utm.CanBuy = false;
            utm.CanSell = false;
            utm.Id = 0;

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.ResRef.Should().Be(ResRef.FromBlank());
            loaded.Tag.Should().Be("");
            loaded.Items.Should().BeEmpty("Empty UTM should have no items");
        }

        [Fact(Timeout = 120000)]
        public void TestUtmMultipleItems()
        {
            // Test UTM with multiple items
            var utm = new UTM();
            utm.ResRef = new ResRef("merchant_01");
            utm.Name = LocalizedString.FromEnglish("Test Merchant");
            utm.Tag = "MERCH01";
            utm.MarkUp = 15;
            utm.MarkDown = 10;
            utm.CanBuy = true;
            utm.CanSell = true;
            utm.OnOpenScript = new ResRef("open_script");

            // Add multiple items
            for (int i = 0; i < 5; i++)
            {
                var item = new UTMItem();
                item.ResRef = new ResRef($"item_{i:D3}");
                item.Infinite = (i % 2 == 0) ? 1 : 0;
                item.Droppable = (i % 3 == 0) ? 1 : 0;
                utm.Items.Add(item);
            }

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.Items.Count.Should().Be(5);
            loaded.ResRef.Should().Be(new ResRef("merchant_01"));
            loaded.Tag.Should().Be("MERCH01");
            loaded.MarkUp.Should().Be(15);
            loaded.MarkDown.Should().Be(10);
            loaded.CanBuy.Should().BeTrue();
            loaded.CanSell.Should().BeTrue();
            loaded.OnOpenScript.Should().Be(new ResRef("open_script"));

            for (int i = 0; i < 5; i++)
            {
                var item = loaded.Items[i];
                item.ResRef.Should().Be(new ResRef($"item_{i:D3}"), $"Item {i} ResRef should match");
                item.Infinite.Should().Be((i % 2 == 0) ? 1 : 0, $"Item {i} Infinite should match");
                item.Droppable.Should().Be((i % 3 == 0) ? 1 : 0, $"Item {i} Droppable should match");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtmDeprecatedIdField()
        {
            // Test deprecated ID field handling
            var utm = new UTM();
            utm.Id = 42;

            // Test with useDeprecated = true (default)
            byte[] data1 = UTMHelpers.BytesUtm(utm, BioWareGame.K2, useDeprecated: true);
            GFF gff1 = GFF.FromBytes(data1);
            gff1.Root.Exists("ID").Should().BeTrue("ID field should exist when useDeprecated=true");
            gff1.Root.GetUInt8("ID").Should().Be(42, "ID field should be 42");

            // Test with useDeprecated = false
            byte[] data2 = UTMHelpers.BytesUtm(utm, BioWareGame.K2, useDeprecated: false);
            GFF gff2 = GFF.FromBytes(data2);
            gff2.Root.Exists("ID").Should().BeFalse("ID field should not exist when useDeprecated=false");
        }

        [Fact(Timeout = 120000)]
        public void TestUtmLocalizedStringName()
        {
            // Test LocName field (LocalizedString)
            var utm = new UTM();
            utm.Name = LocalizedString.FromEnglish("English Merchant Name");
            utm.Name.SetData(Language.German, Gender.Male, "Deutscher Händlername");

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.Name.Should().NotBeNull("Name should not be null");
            loaded.Name.Get(Language.English, Gender.Male).Should().Be("English Merchant Name", "English name should match");
            loaded.Name.Get(Language.German, Gender.Male).Should().Be("Deutscher Händlername", "German name should match");
        }

        [Fact(Timeout = 120000)]
        public void TestUtmCommentField()
        {
            // Test Comment field
            var utm = new UTM();
            utm.Comment = "This is a test merchant comment";

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.Comment.Should().Be("This is a test merchant comment", "Comment should match");
        }

        [Fact(Timeout = 120000)]
        public void TestUtmOnOpenStoreScript()
        {
            // Test OnOpenStore script ResRef
            var utm = new UTM();
            utm.OnOpenScript = new ResRef("merchant_open");

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.OnOpenScript.Should().Be(new ResRef("merchant_open"), "OnOpenScript should match");
        }

        [Fact(Timeout = 120000)]
        public void TestUtmMarkUpAndMarkDown()
        {
            // Test MarkUp and MarkDown fields
            var utm = new UTM();
            utm.MarkUp = 25;
            utm.MarkDown = 20;

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.MarkUp.Should().Be(25, "MarkUp should match");
            loaded.MarkDown.Should().Be(20, "MarkDown should match");
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => UTMHelpers.ReadUtm(File.ReadAllBytes("."));
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => UTMHelpers.ReadUtm(File.ReadAllBytes(DoesNotExistFile));
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => UTMHelpers.ReadUtm(File.ReadAllBytes(CorruptBinaryTestFile));
                act3.Should().Throw<InvalidDataException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtmRoundTripPreservesStructure()
        {
            // Create complex UTM structure
            var utm = new UTM();
            utm.ResRef = new ResRef("complex_merchant");
            utm.Name = LocalizedString.FromEnglish("Complex Merchant");
            utm.Tag = "COMPLEX";
            utm.MarkUp = 30;
            utm.MarkDown = 25;
            utm.Comment = "Complex merchant with many items";
            utm.OnOpenScript = new ResRef("complex_open");
            utm.CanBuy = true;
            utm.CanSell = true;
            utm.Id = 100;

            // Add complex items
            for (int i = 0; i < 10; i++)
            {
                var item = new UTMItem();
                item.ResRef = new ResRef($"complex_item_{i:D2}");
                item.Infinite = (i % 2 == 0) ? 1 : 0;
                item.Droppable = (i % 3 == 0) ? 1 : 0;
                utm.Items.Add(item);
            }

            // Round-trip test
            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            loaded.ResRef.Should().Be(new ResRef("complex_merchant"));
            loaded.Tag.Should().Be("COMPLEX");
            loaded.MarkUp.Should().Be(30);
            loaded.MarkDown.Should().Be(25);
            loaded.Comment.Should().Be("Complex merchant with many items");
            loaded.OnOpenScript.Should().Be(new ResRef("complex_open"));
            loaded.CanBuy.Should().BeTrue();
            loaded.CanSell.Should().BeTrue();
            loaded.Items.Count.Should().Be(10);

            for (int i = 0; i < 10; i++)
            {
                var item = loaded.Items[i];
                item.ResRef.Should().Be(new ResRef($"complex_item_{i:D2}"));
                item.Infinite.Should().Be((i % 2 == 0) ? 1 : 0);
                item.Droppable.Should().Be((i % 3 == 0) ? 1 : 0);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtmAllFieldsRoundTrip()
        {
            // Test all UTM fields in a comprehensive round-trip
            var utm = new UTM();
            utm.ResRef = new ResRef("all_fields_merchant");
            utm.Name = LocalizedString.FromEnglish("All Fields Merchant");
            utm.Name.SetData(Language.French, Gender.Female, "Marchand Tous Champs");
            utm.Tag = "ALLFIELDS";
            utm.MarkUp = 50;
            utm.MarkDown = 40;
            utm.Comment = "Merchant with all fields set";
            utm.OnOpenScript = new ResRef("all_open");
            utm.CanBuy = true;
            utm.CanSell = true;
            utm.Id = 99;

            var item1 = new UTMItem();
            item1.ResRef = new ResRef("item_001");
            item1.Infinite = 1;
            item1.Droppable = 1;
            utm.Items.Add(item1);

            var item2 = new UTMItem();
            item2.ResRef = new ResRef("item_002");
            item2.Infinite = 0;
            item2.Droppable = 0;
            utm.Items.Add(item2);

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            UTM loaded = UTMHelpers.ReadUtm(data);

            // Validate all fields
            loaded.ResRef.Should().Be(utm.ResRef);
            loaded.Name.Get(Language.English, Gender.Male).Should().Be(utm.Name.Get(Language.English, Gender.Male));
            loaded.Name.Get(Language.French, Gender.Female).Should().Be(utm.Name.Get(Language.French, Gender.Female));
            loaded.Tag.Should().Be(utm.Tag);
            loaded.MarkUp.Should().Be(utm.MarkUp);
            loaded.MarkDown.Should().Be(utm.MarkDown);
            loaded.Comment.Should().Be(utm.Comment);
            loaded.OnOpenScript.Should().Be(utm.OnOpenScript);
            loaded.CanBuy.Should().Be(utm.CanBuy);
            loaded.CanSell.Should().Be(utm.CanSell);
            loaded.Id.Should().Be(utm.Id);
            loaded.Items.Count.Should().Be(utm.Items.Count);

            for (int i = 0; i < utm.Items.Count; i++)
            {
                loaded.Items[i].ResRef.Should().Be(utm.Items[i].ResRef);
                loaded.Items[i].Infinite.Should().Be(utm.Items[i].Infinite);
                loaded.Items[i].Droppable.Should().Be(utm.Items[i].Droppable);
            }
        }

        private static void ValidateIO(UTM utm)
        {
            // Basic validation
            utm.Should().NotBeNull();
            utm.ResRef.Should().NotBeNull();
            utm.Name.Should().NotBeNull();
            utm.Items.Should().NotBeNull();
            utm.MarkUp.Should().BeGreaterThanOrEqualTo(0);
            utm.MarkDown.Should().BeGreaterThanOrEqualTo(0);
        }

        private static void CreateTestUtmFile(string path)
        {
            var utm = new UTM();
            utm.ResRef = new ResRef("test_merchant");
            utm.Name = LocalizedString.FromEnglish("Test Merchant");
            utm.Tag = "TEST";
            utm.MarkUp = 10;
            utm.MarkDown = 5;
            utm.CanBuy = true;
            utm.CanSell = true;
            utm.Comment = "Test merchant comment";
            utm.OnOpenScript = new ResRef("test_open");

            // Add a test item
            var item = new UTMItem();
            item.ResRef = new ResRef("test_item");
            item.Infinite = 1;
            item.Droppable = 0;
            utm.Items.Add(item);

            byte[] data = UTMHelpers.BytesUtm(utm, BioWareGame.K2);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

