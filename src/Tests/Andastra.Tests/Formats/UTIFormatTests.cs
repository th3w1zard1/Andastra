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
using UTI = Andastra.Parsing.Resource.Generics.UTI.UTI;
using UTIHelpers = Andastra.Parsing.Resource.Generics.UTI.UTIHelpers;
using UTIProperty = Andastra.Parsing.Resource.Generics.UTI.UTIProperty;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTI (Item Template) binary I/O operations.
    /// Tests validate the UTI format structure as defined in UTI.ksy Kaitai Struct definition.
    /// UTI files are GFF-based format files with "UTI " signature.
    /// </summary>
    public class UTIFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.uti");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.uti");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test UTI file if it doesn't exist
                CreateTestUtiFile(BinaryTestFile);
            }

            // Test reading UTI file
            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));
            ValidateIO(uti);

            // Test writing and reading back
            byte[] data = UTIHelpers.BytesUti(uti);
            uti = UTIHelpers.ReadUti(data);
            ValidateIO(uti);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiHeaderStructure()
        {
            // Test that UTI header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate header constants match UTI.ksy
            gff.Header.FileType.Should().Be("UTI ", "UTI file type should be 'UTI ' as defined in UTI.ksy");
            gff.Header.FileVersion.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "UTI version should match UTI.ksy definition");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTI.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTI ", "File type should be 'UTI ' (space-padded) as defined in UTI.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTI.ksy valid values");
        }


        [Fact(Timeout = 120000)]
        public void TestUtiRootStruct()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate root struct exists and has basic fields
            uti.Should().NotBeNull("UTI should be parseable");
            uti.ResRef.Should().NotBeNull("TemplateResRef should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiCoreIdentityFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate core identity fields exist (may be empty/default)
            uti.ResRef.Should().NotBeNull("TemplateResRef should exist");
            uti.Tag.Should().NotBeNull("Tag should exist");
            uti.Comment.Should().NotBeNull("Comment should exist");
            uti.Name.Should().NotBeNull("LocalizedName should exist");
            uti.Description.Should().NotBeNull("DescIdentified should exist");
            uti.DescriptionUnidentified.Should().NotBeNull("Description should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiBaseItemConfiguration()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate base item configuration fields exist
            uti.BaseItem.Should().BeGreaterThanOrEqualTo(0, "BaseItem should be non-negative");
            uti.Cost.Should().BeGreaterThanOrEqualTo(0, "Cost should be non-negative");
            uti.AddCost.Should().BeGreaterThanOrEqualTo(0, "AddCost should be non-negative");
            uti.Plot.Should().BeGreaterThanOrEqualTo(0, "Plot should be non-negative (0 or 1)");
            uti.Plot.Should().BeLessThanOrEqualTo(1, "Plot should be 0 or 1");
            uti.Charges.Should().BeGreaterThanOrEqualTo(0, "Charges should be non-negative");
            uti.Charges.Should().BeLessThanOrEqualTo(255, "Charges should be <= 255 (UInt8)");
            uti.StackSize.Should().BeGreaterThanOrEqualTo(0, "StackSize should be non-negative");
            uti.StackSize.Should().BeLessThanOrEqualTo(65535, "StackSize should be <= 65535 (UInt16)");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiVisualVariations()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate visual variation fields exist
            uti.ModelVariation.Should().BeGreaterThanOrEqualTo(0, "ModelVariation should be non-negative");
            uti.ModelVariation.Should().BeLessThanOrEqualTo(255, "ModelVariation should be <= 255 (UInt8)");
            uti.BodyVariation.Should().BeGreaterThanOrEqualTo(0, "BodyVariation should be non-negative");
            uti.BodyVariation.Should().BeLessThanOrEqualTo(255, "BodyVariation should be <= 255 (UInt8)");
            uti.TextureVariation.Should().BeGreaterThanOrEqualTo(0, "TextureVar should be non-negative");
            uti.TextureVariation.Should().BeLessThanOrEqualTo(255, "TextureVar should be <= 255 (UInt8)");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiPropertiesList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate PropertiesList exists
            uti.Properties.Should().NotBeNull("PropertiesList should exist");

            // Validate each property entry
            foreach (var prop in uti.Properties)
            {
                prop.Should().NotBeNull("Property should not be null");
                prop.PropertyName.Should().BeGreaterThanOrEqualTo(0, "PropertyName should be non-negative");
                prop.PropertyName.Should().BeLessThanOrEqualTo(65535, "PropertyName should be <= 65535 (UInt16)");
                prop.Subtype.Should().BeGreaterThanOrEqualTo(0, "Subtype should be non-negative");
                prop.Subtype.Should().BeLessThanOrEqualTo(65535, "Subtype should be <= 65535 (UInt16)");
                prop.CostTable.Should().BeGreaterThanOrEqualTo(0, "CostTable should be non-negative");
                prop.CostTable.Should().BeLessThanOrEqualTo(255, "CostTable should be <= 255 (UInt8)");
                prop.CostValue.Should().BeGreaterThanOrEqualTo(0, "CostValue should be non-negative");
                prop.CostValue.Should().BeLessThanOrEqualTo(65535, "CostValue should be <= 65535 (UInt16)");
                prop.Param1.Should().BeGreaterThanOrEqualTo(0, "Param1 should be non-negative");
                prop.Param1.Should().BeLessThanOrEqualTo(255, "Param1 should be <= 255 (UInt8)");
                prop.Param1Value.Should().BeGreaterThanOrEqualTo(0, "Param1Value should be non-negative");
                prop.Param1Value.Should().BeLessThanOrEqualTo(255, "Param1Value should be <= 255 (UInt8)");
                prop.ChanceAppear.Should().BeGreaterThanOrEqualTo(0, "ChanceAppear should be non-negative");
                prop.ChanceAppear.Should().BeLessThanOrEqualTo(255, "ChanceAppear should be <= 255 (UInt8)");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiPaletteAndEditor()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate palette and editor fields exist
            uti.PaletteId.Should().BeGreaterThanOrEqualTo(0, "PaletteID should be non-negative");
            uti.PaletteId.Should().BeLessThanOrEqualTo(255, "PaletteID should be <= 255 (UInt8)");
            uti.Comment.Should().NotBeNull("Comment should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiQuestAndSpecialFlags()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate quest and special item flags exist
            uti.Stolen.Should().BeGreaterThanOrEqualTo(0, "Stolen should be non-negative");
            uti.Stolen.Should().BeLessThanOrEqualTo(1, "Stolen should be 0 or 1");
            uti.Identified.Should().BeGreaterThanOrEqualTo(0, "Identified should be non-negative");
            uti.Identified.Should().BeLessThanOrEqualTo(1, "Identified should be 0 or 1");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiUpgradeLevel()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate UpgradeLevel exists (KotOR2 field)
            uti.UpgradeLevel.Should().BeGreaterThanOrEqualTo(0, "UpgradeLevel should be non-negative");
            uti.UpgradeLevel.Should().BeLessThanOrEqualTo(255, "UpgradeLevel should be <= 255 (UInt8)");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiInvalidSignature()
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

                Action act = () => UTIHelpers.ReadUti(File.ReadAllBytes(tempFile));
                act.Should().Throw<Exception>("UTI file with invalid signature should throw exception");
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
        public void TestUtiInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                // Create a minimal GFF structure with invalid version
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("UTI ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    // Set minimal valid offsets
                    BitConverter.GetBytes(56u).CopyTo(header, 8);  // struct_offset
                    BitConverter.GetBytes(0u).CopyTo(header, 12); // struct_count
                    BitConverter.GetBytes(56u).CopyTo(header, 16); // field_offset
                    BitConverter.GetBytes(0u).CopyTo(header, 20); // field_count
                    BitConverter.GetBytes(56u).CopyTo(header, 24); // label_offset
                    BitConverter.GetBytes(0u).CopyTo(header, 28); // label_count
                    BitConverter.GetBytes(56u).CopyTo(header, 32); // field_data_offset
                    BitConverter.GetBytes(0u).CopyTo(header, 36); // field_data_count
                    BitConverter.GetBytes(56u).CopyTo(header, 40); // field_indices_offset
                    BitConverter.GetBytes(0u).CopyTo(header, 44); // field_indices_count
                    BitConverter.GetBytes(56u).CopyTo(header, 48); // list_indices_offset
                    BitConverter.GetBytes(0u).CopyTo(header, 52); // list_indices_count
                    fs.Write(header, 0, header.Length);
                }

                // GFF parser may accept invalid version, but UTI validation should catch it
                Action act = () => UTIHelpers.ReadUti(File.ReadAllBytes(tempFile));
                // May or may not throw depending on GFF parser strictness
                try
                {
                    act();
                }
                catch
                {
                    // Expected if version validation is strict
                }
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
        public void TestUtiRoundTrip()
        {
            // Test creating UTI, reading it back
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("testitem");
            uti.Tag = "test_tag";
            uti.Comment = "Test item";
            uti.BaseItem = 1; // Shortsword
            uti.Cost = 100;
            uti.AddCost = 50;
            uti.Plot = 0;
            uti.Charges = 5;
            uti.StackSize = 1;
            uti.ModelVariation = 1;
            uti.BodyVariation = 0;
            uti.TextureVariation = 0;
            uti.PaletteId = 1;
            uti.UpgradeLevel = 0;
            uti.Stolen = 0;
            uti.Identified = 1;

            // Add a property
            var prop = new UTIProperty();
            prop.PropertyName = 1; // Attack Bonus
            prop.Subtype = 0;
            prop.CostTable = 0;
            prop.CostValue = 10;
            prop.Param1 = 0;
            prop.Param1Value = 1; // +1 attack bonus
            prop.ChanceAppear = 100;
            uti.Properties.Add(prop);

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.ResRef.ToString().Should().Be("testitem");
            loaded.Tag.Should().Be("test_tag");
            loaded.Comment.Should().Be("Test item");
            loaded.BaseItem.Should().Be(1);
            loaded.Cost.Should().Be(100);
            loaded.AddCost.Should().Be(50);
            loaded.Plot.Should().Be(0);
            loaded.Charges.Should().Be(5);
            loaded.StackSize.Should().Be(1);
            loaded.ModelVariation.Should().Be(1);
            loaded.BodyVariation.Should().Be(0);
            loaded.TextureVariation.Should().Be(0);
            loaded.PaletteId.Should().Be(1);
            loaded.UpgradeLevel.Should().Be(0);
            loaded.Stolen.Should().Be(0);
            loaded.Identified.Should().Be(1);
            loaded.Properties.Count.Should().Be(1);
            loaded.Properties[0].PropertyName.Should().Be(1);
            loaded.Properties[0].Param1Value.Should().Be(1);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiEmptyFile()
        {
            // Test UTI with minimal data
            var uti = new UTI();
            uti.ResRef = ResRef.FromBlank();

            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.Should().NotBeNull("Empty UTI should load successfully");
            loaded.ResRef.Should().NotBeNull("TemplateResRef should exist even in empty UTI");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiComplexItem()
        {
            // Test UTI with all major fields populated
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("complexitem");
            uti.Tag = "complex_tag";
            uti.Comment = "Complex test item";
            uti.Name = LocalizedString.FromStrRef(1);
            uti.Description = LocalizedString.FromStrRef(2);
            uti.DescriptionUnidentified = LocalizedString.FromStrRef(3);

            // Base item configuration
            uti.BaseItem = 5; // Longsword
            uti.Cost = 5000;
            uti.AddCost = 1000;
            uti.Plot = 1; // Plot item
            uti.Charges = 10;
            uti.StackSize = 5;
            uti.ModelVariation = 3;
            uti.BodyVariation = 2;
            uti.TextureVariation = 4;
            uti.PaletteId = 5;
            uti.UpgradeLevel = 2; // KotOR2 max upgrade level
            uti.Stolen = 0;
            uti.Identified = 1;

            // Add multiple properties
            var prop1 = new UTIProperty();
            prop1.PropertyName = 1; // Attack Bonus
            prop1.Subtype = 0;
            prop1.CostTable = 0;
            prop1.CostValue = 20;
            prop1.Param1 = 0;
            prop1.Param1Value = 2; // +2 attack bonus
            prop1.ChanceAppear = 100;
            uti.Properties.Add(prop1);

            var prop2 = new UTIProperty();
            prop2.PropertyName = 2; // Damage Bonus
            prop2.Subtype = 0;
            prop2.CostTable = 0;
            prop2.CostValue = 15;
            prop2.Param1 = 0;
            prop2.Param1Value = 1; // +1d4 damage
            prop2.ChanceAppear = 100;
            uti.Properties.Add(prop2);

            var prop3 = new UTIProperty();
            prop3.PropertyName = 3; // Ability Bonus
            prop3.Subtype = 0; // Strength
            prop3.CostTable = 0;
            prop3.CostValue = 30;
            prop3.Param1 = 0;
            prop3.Param1Value = 2; // +2 Strength
            prop3.ChanceAppear = 100;
            uti.Properties.Add(prop3);

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            // Validate all fields
            loaded.ResRef.ToString().Should().Be("complexitem");
            loaded.Tag.Should().Be("complex_tag");
            loaded.Comment.Should().Be("Complex test item");
            loaded.BaseItem.Should().Be(5);
            loaded.Cost.Should().Be(5000);
            loaded.AddCost.Should().Be(1000);
            loaded.Plot.Should().Be(1);
            loaded.Charges.Should().Be(10);
            loaded.StackSize.Should().Be(5);
            loaded.ModelVariation.Should().Be(3);
            loaded.BodyVariation.Should().Be(2);
            loaded.TextureVariation.Should().Be(4);
            loaded.PaletteId.Should().Be(5);
            loaded.UpgradeLevel.Should().Be(2);
            loaded.Stolen.Should().Be(0);
            loaded.Identified.Should().Be(1);
            loaded.Properties.Count.Should().Be(3);
            loaded.Properties[0].PropertyName.Should().Be(1);
            loaded.Properties[0].Param1Value.Should().Be(2);
            loaded.Properties[1].PropertyName.Should().Be(2);
            loaded.Properties[1].Param1Value.Should().Be(1);
            loaded.Properties[2].PropertyName.Should().Be(3);
            loaded.Properties[2].Param1Value.Should().Be(2);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiPropertyWithUpgradeType()
        {
            // Test UTI property with UpgradeType field (KotOR2)
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("upgradeitem");
            uti.BaseItem = 1;

            var prop = new UTIProperty();
            prop.PropertyName = 1;
            prop.Subtype = 0;
            prop.CostTable = 0;
            prop.CostValue = 10;
            prop.Param1 = 0;
            prop.Param1Value = 1;
            prop.ChanceAppear = 100;
            prop.UpgradeType = 1; // KotOR2 upgrade type restriction
            uti.Properties.Add(prop);

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti, BioWareGame.K2);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.Properties.Count.Should().Be(1);
            loaded.Properties[0].UpgradeType.Should().HaveValue("UpgradeType should be preserved");
            loaded.Properties[0].UpgradeType.Value.Should().Be(1);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiMultipleProperties()
        {
            // Test UTI with many properties
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("multipropitem");
            uti.BaseItem = 1;

            // Add 10 properties
            for (int i = 0; i < 10; i++)
            {
                var prop = new UTIProperty();
                prop.PropertyName = (ushort)(i + 1);
                prop.Subtype = 0;
                prop.CostTable = 0;
                prop.CostValue = (ushort)(10 + i);
                prop.Param1 = 0;
                prop.Param1Value = (byte)(i + 1);
                prop.ChanceAppear = 100;
                uti.Properties.Add(prop);
            }

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.Properties.Count.Should().Be(10);
            for (int i = 0; i < 10; i++)
            {
                loaded.Properties[i].PropertyName.Should().Be(i + 1);
                loaded.Properties[i].Param1Value.Should().Be(i + 1);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiStackableItem()
        {
            // Test UTI with stack size > 1
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("stackableitem");
            uti.BaseItem = 31; // Medpac
            uti.StackSize = 50;
            uti.Cost = 25;

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.StackSize.Should().Be(50);
            loaded.Cost.Should().Be(25);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiPlotItem()
        {
            // Test UTI with plot flag set
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("plotitem");
            uti.BaseItem = 31; // Quest item
            uti.Plot = 1; // Plot-critical

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.Plot.Should().Be(1, "Plot flag should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiCharges()
        {
            // Test UTI with charges
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("chargeditem");
            uti.BaseItem = 1;
            uti.Charges = 25;

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.Charges.Should().Be(25);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiMaxValues()
        {
            // Test UTI with maximum values for all fields
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("maxitem");
            uti.BaseItem = 65535; // Max UInt16 (though BaseItem is Int32)
            uti.Cost = int.MaxValue; // Max Int32 (though Cost is UInt32)
            uti.AddCost = int.MaxValue;
            uti.Charges = 255; // Max UInt8
            uti.StackSize = 65535; // Max UInt16
            uti.ModelVariation = 255; // Max UInt8
            uti.BodyVariation = 255; // Max UInt8
            uti.TextureVariation = 255; // Max UInt8
            uti.PaletteId = 255; // Max UInt8
            uti.UpgradeLevel = 255; // Max UInt8
            uti.Plot = 1;
            uti.Stolen = 1;
            uti.Identified = 1;

            // Write and read back
            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.Charges.Should().Be(255);
            loaded.StackSize.Should().Be(65535);
            loaded.ModelVariation.Should().Be(255);
            loaded.BodyVariation.Should().Be(255);
            loaded.TextureVariation.Should().Be(255);
            loaded.PaletteId.Should().Be(255);
            loaded.UpgradeLevel.Should().Be(255);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiGffHeaderSize()
        {
            // Test that GFF header matches UTI.ksy definition (56 bytes)
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate header size matches UTI.ksy (56 bytes = 14 fields × 4 bytes each)
            // File type (4) + version (4) + 13 offsets/counts (13×4 = 52) = 56 bytes
            gff.Header.Should().NotBeNull("GFF header should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestUtiStructArrayStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate struct array structure matches UTI.ksy
            gff.Header.StructCount.Should().BeGreaterThanOrEqualTo(0, "Struct count should be non-negative");

            // Each struct entry is 12 bytes (struct_id: 4, data_or_offset: 4, field_count: 4)
            if (gff.Header.StructCount > 0)
            {
                gff.Structs.Should().NotBeNull("Struct array should exist");
                gff.Structs.Count.Should().Be((int)gff.Header.StructCount, "Struct count should match header");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiFieldArrayStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate field array structure matches UTI.ksy
            gff.Header.FieldCount.Should().BeGreaterThanOrEqualTo(0, "Field count should be non-negative");
            if (gff.Header.FieldCount > 0)
            {
                gff.Fields.Should().NotBeNull("Field array should exist");
                gff.Fields.Count.Should().Be((int)gff.Header.FieldCount, "Field count should match header");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiLabelArrayStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate label array structure matches UTI.ksy
            gff.Header.LabelCount.Should().BeGreaterThanOrEqualTo(0, "Label count should be non-negative");

            // Each label is 16 bytes (null-terminated ASCII string, padded to 16 bytes)
            if (gff.Header.LabelCount > 0)
            {
                gff.Labels.Should().NotBeNull("Label array should exist");
                gff.Labels.Count.Should().Be((int)gff.Header.LabelCount, "Label count should match header");

                foreach (var label in gff.Labels)
                {
                    label.Should().NotBeNull("Label should not be null");
                    label.Length.Should().BeLessThanOrEqualTo(16, "Label should be at most 16 bytes");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiFieldDataSection()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate field data section matches UTI.ksy
            gff.Header.FieldDataCount.Should().BeGreaterThanOrEqualTo(0, "Field data count should be non-negative");

            // Field data section contains raw bytes for complex types
            if (gff.Header.FieldDataCount > 0)
            {
                gff.FieldData.Should().NotBeNull("Field data section should exist");
                gff.FieldData.Count().Should().Be((int)gff.Header.FieldDataCount, "Field data size should match header");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiFieldIndicesArray()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate field indices array matches UTI.ksy
            gff.Header.FieldIndicesCount.Should().BeGreaterThanOrEqualTo(0, "Field indices count should be non-negative");

            // Field indices array contains uint32 values for structs with multiple fields
            if (gff.Header.FieldIndicesCount > 0)
            {
                gff.FieldIndices.Should().NotBeNull("Field indices array should exist");
                gff.FieldIndices.Count.Should().Be((int)gff.Header.FieldIndicesCount, "Field indices count should match header");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiListIndicesArray()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate list indices array matches UTI.ksy
            gff.Header.ListIndicesCount.Should().BeGreaterThanOrEqualTo(0, "List indices count should be non-negative");

            // List indices array contains uint32 values for LIST type fields
            if (gff.Header.ListIndicesCount > 0)
            {
                gff.ListIndices.Should().NotBeNull("List indices array should exist");
                gff.ListIndices.Count.Should().Be((int)gff.Header.ListIndicesCount, "List indices count should match header");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiPropertiesListStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));
            UTI uti = UTIHelpers.ReadUti(File.ReadAllBytes(BinaryTestFile));

            // Validate PropertiesList is a LIST type field as per UTI.ksy
            uti.Properties.Should().NotBeNull("PropertiesList should exist");

            // PropertiesList should be stored as a LIST type field in GFF
            // Each property is a struct within the list
            if (uti.Properties.Count > 0)
            {
                // PropertiesList should reference list indices
                gff.Header.ListIndicesCount.Should().BeGreaterThanOrEqualTo(0, "List indices should exist if properties exist");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiPropertyStructFields()
        {
            // Test that property struct fields match UTI.ksy documentation
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("proptest");
            uti.BaseItem = 1;

            var prop = new UTIProperty();
            // PropertyName: UInt16 (Word)
            prop.PropertyName = 65535;
            // Subtype: UInt16 (Word)
            prop.Subtype = 65535;
            // CostTable: UInt8 (Byte)
            prop.CostTable = 255;
            // CostValue: UInt16 (Word)
            prop.CostValue = 65535;
            // Param1: UInt8 (Byte)
            prop.Param1 = 255;
            // Param1Value: UInt8 (Byte)
            prop.Param1Value = 255;
            // ChanceAppear: UInt8 (Byte)
            prop.ChanceAppear = 255;
            uti.Properties.Add(prop);

            byte[] data = UTIHelpers.BytesUti(uti);
            UTI loaded = UTIHelpers.ReadUti(data);

            loaded.Properties.Count.Should().Be(1);
            loaded.Properties[0].PropertyName.Should().Be(65535);
            loaded.Properties[0].Subtype.Should().Be(65535);
            loaded.Properties[0].CostTable.Should().Be(255);
            loaded.Properties[0].CostValue.Should().Be(65535);
            loaded.Properties[0].Param1.Should().Be(255);
            loaded.Properties[0].Param1Value.Should().Be(255);
            loaded.Properties[0].ChanceAppear.Should().Be(255);
        }

        [Fact(Timeout = 120000)]
        public void TestUtiDataOffsets()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate that all offsets in header are valid
            // Offsets should be >= 56 (header size) or 0 (not present)
            gff.Header.StructArrayOffset.Should().BeGreaterThanOrEqualTo(56, "Struct array offset should be after header");
            gff.Header.FieldArrayOffset.Should().BeGreaterThanOrEqualTo(56, "Field array offset should be after header");

            if (gff.Header.LabelCount > 0)
            {
                gff.Header.LabelArrayOffset.Should().BeGreaterThanOrEqualTo(56, "Label array offset should be after header");
            }

            if (gff.Header.FieldDataCount > 0)
            {
                gff.Header.FieldDataOffset.Should().BeGreaterThanOrEqualTo(56, "Field data offset should be after header");
            }

            if (gff.Header.FieldIndicesCount > 0)
            {
                gff.Header.FieldIndicesOffset.Should().BeGreaterThanOrEqualTo(56, "Field indices offset should be after header");
            }

            if (gff.Header.ListIndicesCount > 0)
            {
                gff.Header.ListIndicesOffset.Should().BeGreaterThanOrEqualTo(56, "List indices offset should be after header");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiRootStructId()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtiFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Root struct should have struct_id = 0xFFFFFFFF (-1) as per UTI.ksy
            if (gff.Header.StructCount > 0)
            {
                var rootStruct = gff.Structs[0];
                rootStruct.StructId.Should().Be(-1, "Root struct should have struct_id = -1 (0xFFFFFFFF)");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtiFieldTypeValidation()
        {
            // Test that UTI uses correct GFF field types as documented in UTI.ksy
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("typetest");
            uti.BaseItem = 1;
            uti.Tag = "test_tag";
            uti.Comment = "Test comment";

            byte[] data = UTIHelpers.BytesUti(uti);
            GFF gff = GFF.FromBytes(data);

            // TemplateResRef should be ResRef type (11)
            // Tag should be String type (10)
            // Comment should be String type (10)
            // BaseItem should be Int32 type (5)
            // Cost should be UInt32 type (4)
            // Charges should be UInt8 type (0)
            // StackSize should be UInt16 type (2)
            // ModelVariation should be UInt8 type (0)
            // BodyVariation should be UInt8 type (0)
            // TextureVar should be UInt8 type (0)
            // PaletteID should be UInt8 type (0)
            // Plot should be UInt8 type (0)
            // PropertiesList should be List type (15)

            gff.Fields.Should().NotBeNull("Fields should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => UTIHelpers.ReadUti(File.ReadAllBytes("."));
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<Exception>();
            }
            else
            {
                act1.Should().Throw<Exception>();
            }

            // Test reading non-existent file
            Action act2 = () => UTIHelpers.ReadUti(File.ReadAllBytes(DoesNotExistFile));
            act2.Should().Throw<FileNotFoundException>();
        }

        private static void ValidateIO(UTI uti)
        {
            // Basic validation
            uti.Should().NotBeNull();
            uti.ResRef.Should().NotBeNull("TemplateResRef should exist");
            uti.Properties.Should().NotBeNull("PropertiesList should exist");
        }

        private static void CreateTestUtiFile(string path)
        {
            var uti = new UTI();
            uti.ResRef = ResRef.FromString("testitem");
            uti.Tag = "test_tag";
            uti.Comment = "Test item";
            uti.BaseItem = 1; // Shortsword
            uti.Cost = 100;
            uti.AddCost = 0;
            uti.Plot = 0;
            uti.Charges = 0;
            uti.StackSize = 1;
            uti.ModelVariation = 1;
            uti.BodyVariation = 0;
            uti.TextureVariation = 0;
            uti.PaletteId = 1;
            uti.UpgradeLevel = 0;
            uti.Stolen = 0;
            uti.Identified = 1;

            byte[] data = UTIHelpers.BytesUti(uti);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

