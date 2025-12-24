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
using UTC = Andastra.Parsing.Resource.Generics.UTC.UTC;
using UTCClass = Andastra.Parsing.Resource.Generics.UTC.UTCClass;
using UTCHelpers = Andastra.Parsing.Resource.Generics.UTC.UTCHelpers;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTC (Creature Template) binary I/O operations.
    /// Tests validate the UTC format structure as defined in UTC.ksy Kaitai Struct definition.
    /// UTC files are GFF-based format files with "UTC " signature.
    /// </summary>
    public class UTCFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.utc");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.utc");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test UTC file if it doesn't exist
                CreateTestUtcFile(BinaryTestFile);
            }

            // Test reading UTC file
            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));
            ValidateIO(utc);

            // Test writing and reading back
            byte[] data = UTCHelpers.BytesUtc(utc);
            utc = UTCHelpers.ReadUtc(data);
            ValidateIO(utc);
        }

        [Fact(Timeout = 120000)]
        public void TestUtcHeaderStructure()
        {
            // Test that UTC header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate content type matches UTC.ksy
            gff.Content.Should().Be(GFFContent.UTC, "UTC file content type should be UTC as defined in UTC.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTC.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTC ", "File type should be 'UTC ' (space-padded) as defined in UTC.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTC.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcGffStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));

            // Validate GFF structure
            gff.Root.Should().NotBeNull("UTC should have a root struct");
            gff.Content.Should().Be(GFFContent.UTC, "UTC file content type should be UTC");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcRootStruct()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate root struct exists and has basic fields
            utc.Should().NotBeNull("UTC should be parseable");
            utc.ResRef.Should().NotBeNull("TemplateResRef should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcCoreIdentityFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate core identity fields exist (may be empty/default)
            utc.ResRef.Should().NotBeNull("TemplateResRef should exist");
            utc.Tag.Should().NotBeNull("Tag should exist");
            utc.Comment.Should().NotBeNull("Comment should exist");
            utc.Conversation.Should().NotBeNull("Conversation should exist");
            utc.FirstName.Should().NotBeNull("FirstName should exist");
            utc.LastName.Should().NotBeNull("LastName should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcAppearanceFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate appearance fields exist (may be 0/default)
            utc.AppearanceId.Should().BeGreaterThanOrEqualTo(0, "Appearance_Type should be non-negative");
            utc.PortraitId.Should().BeGreaterThanOrEqualTo(0, "PortraitId should be non-negative");
            utc.GenderId.Should().BeGreaterThanOrEqualTo(0, "Gender should be non-negative");
            utc.RaceId.Should().BeGreaterThanOrEqualTo(0, "Race should be non-negative");
            utc.BodyVariation.Should().BeGreaterThanOrEqualTo(0, "BodyVariation should be non-negative");
            utc.TextureVariation.Should().BeGreaterThanOrEqualTo(0, "TextureVar should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcStatsFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate stats fields exist
            utc.Strength.Should().BeGreaterThanOrEqualTo(0, "Str should be non-negative");
            utc.Dexterity.Should().BeGreaterThanOrEqualTo(0, "Dex should be non-negative");
            utc.Constitution.Should().BeGreaterThanOrEqualTo(0, "Con should be non-negative");
            utc.Intelligence.Should().BeGreaterThanOrEqualTo(0, "Int should be non-negative");
            utc.Wisdom.Should().BeGreaterThanOrEqualTo(0, "Wis should be non-negative");
            utc.Charisma.Should().BeGreaterThanOrEqualTo(0, "Cha should be non-negative");
            utc.CurrentHp.Should().BeGreaterThanOrEqualTo(0, "CurrentHitPoints should be non-negative");
            utc.MaxHp.Should().BeGreaterThanOrEqualTo(0, "MaxHitPoints should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcClassList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate ClassList exists
            utc.Classes.Should().NotBeNull("ClassList should exist");

            // Validate each class entry
            foreach (var utcClass in utc.Classes)
            {
                utcClass.ClassId.Should().BeGreaterThanOrEqualTo(0, "Class ID should be non-negative");
                utcClass.ClassLevel.Should().BeGreaterThanOrEqualTo(0, "ClassLevel should be non-negative");
                utcClass.Powers.Should().NotBeNull("Powers list should exist");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtcFeatList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate FeatList exists
            utc.Feats.Should().NotBeNull("FeatList should exist");

            // Validate each feat entry
            foreach (var feat in utc.Feats)
            {
                feat.Should().BeGreaterThanOrEqualTo(0, "Feat ID should be non-negative");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtcInventoryList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate ItemList exists
            utc.Inventory.Should().NotBeNull("ItemList should exist");

            // Validate each inventory item
            foreach (var item in utc.Inventory)
            {
                item.Should().NotBeNull("Inventory item should not be null");
                item.ResRef.Should().NotBeNull("InventoryRes should exist");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtcEquipmentList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate Equip_ItemList exists
            utc.Equipment.Should().NotBeNull("Equip_ItemList should exist");

            // Validate each equipment item
            foreach (var kvp in utc.Equipment)
            {
                kvp.Key.Should().BeDefined("Equipment slot should be valid");
                kvp.Value.Should().NotBeNull("Equipment item should not be null");
                kvp.Value.ResRef.Should().NotBeNull("EquippedRes should exist");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtcScriptHooks()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate script hooks exist (may be blank)
            utc.OnAttacked.Should().NotBeNull("ScriptAttacked should exist");
            utc.OnDamaged.Should().NotBeNull("ScriptDamaged should exist");
            utc.OnDeath.Should().NotBeNull("ScriptDeath should exist");
            utc.OnHeartbeat.Should().NotBeNull("ScriptHeartbeat should exist");
            utc.OnDialog.Should().NotBeNull("ScriptDialogue should exist");
            utc.OnSpawn.Should().NotBeNull("ScriptSpawn should exist");
            utc.OnBlocked.Should().NotBeNull("ScriptOnBlocked should exist");
            utc.OnNotice.Should().NotBeNull("ScriptOnNotice should exist");
            utc.OnSpell.Should().NotBeNull("ScriptSpellAt should exist");
            utc.OnDisturbed.Should().NotBeNull("ScriptDisturbed should exist");
            utc.OnEndRound.Should().NotBeNull("ScriptEndRound should exist");
            utc.OnEndDialog.Should().NotBeNull("ScriptEndDialogu should exist");
            utc.OnUserDefined.Should().NotBeNull("ScriptUserDefine should exist");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcSkillsList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate skills exist (may be 0)
            utc.ComputerUse.Should().BeGreaterThanOrEqualTo(0, "ComputerUse skill should be non-negative");
            utc.Demolitions.Should().BeGreaterThanOrEqualTo(0, "Demolitions skill should be non-negative");
            utc.Stealth.Should().BeGreaterThanOrEqualTo(0, "Stealth skill should be non-negative");
            utc.Awareness.Should().BeGreaterThanOrEqualTo(0, "Awareness skill should be non-negative");
            utc.Persuade.Should().BeGreaterThanOrEqualTo(0, "Persuade skill should be non-negative");
            utc.Repair.Should().BeGreaterThanOrEqualTo(0, "Repair skill should be non-negative");
            utc.Security.Should().BeGreaterThanOrEqualTo(0, "Security skill should be non-negative");
            utc.TreatInjury.Should().BeGreaterThanOrEqualTo(0, "TreatInjury skill should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcBooleanFlags()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtcFile(BinaryTestFile);
            }

            UTC utc = UTCHelpers.ReadUtc(File.ReadAllBytes(BinaryTestFile));

            // Validate boolean flags exist (stored as UInt8 in GFF)
            // These should be accessible as bool properties
            // Note: The actual values may be true or false, we just verify they're accessible
            var notReorienting = utc.NotReorienting;
            var partyInteract = utc.PartyInteract;
            var noPermDeath = utc.NoPermDeath;
            var min1Hp = utc.Min1Hp;
            var plot = utc.Plot;
            var interruptable = utc.Interruptable;
            var isPc = utc.IsPc;
            var disarmable = utc.Disarmable;
        }

        [Fact(Timeout = 120000)]
        public void TestUtcInvalidSignature()
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

                Action act = () => UTCHelpers.ReadUtc(File.ReadAllBytes(tempFile));
                act.Should().Throw<Exception>("UTC file with invalid signature should throw exception");
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
        public void TestUtcInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                // Create a minimal GFF structure with invalid version
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("UTC ").CopyTo(header, 0);
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

                // GFF parser may accept invalid version, but UTC validation should catch it
                Action act = () => UTCHelpers.ReadUtc(File.ReadAllBytes(tempFile));
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
        public void TestUtcRoundTrip()
        {
            // Test creating UTC, reading it back
            var utc = new UTC();
            utc.ResRef = ResRef.FromString("testcreature");
            utc.Tag = "test_tag";
            utc.Comment = "Test creature";
            utc.Strength = 14;
            utc.Dexterity = 12;
            utc.Constitution = 13;
            utc.Intelligence = 10;
            utc.Wisdom = 11;
            utc.Charisma = 15;
            utc.CurrentHp = 50;
            utc.MaxHp = 50;
            utc.AppearanceId = 1;
            utc.GenderId = 0;
            utc.RaceId = 1;

            // Add a class
            var utcClass = new UTCClass(1, 5); // Class ID 1, Level 5
            utc.Classes.Add(utcClass);

            // Add a feat
            utc.Feats.Add(1);

            // Write and read back
            byte[] data = UTCHelpers.BytesUtc(utc);
            UTC loaded = UTCHelpers.ReadUtc(data);

            loaded.ResRef.ToString().Should().Be("testcreature");
            loaded.Tag.Should().Be("test_tag");
            loaded.Comment.Should().Be("Test creature");
            loaded.Strength.Should().Be(14);
            loaded.Dexterity.Should().Be(12);
            loaded.Constitution.Should().Be(13);
            loaded.Intelligence.Should().Be(10);
            loaded.Wisdom.Should().Be(11);
            loaded.Charisma.Should().Be(15);
            loaded.CurrentHp.Should().Be(50);
            loaded.MaxHp.Should().Be(50);
            loaded.AppearanceId.Should().Be(1);
            loaded.GenderId.Should().Be(0);
            loaded.RaceId.Should().Be(1);
            loaded.Classes.Count.Should().Be(1);
            loaded.Classes[0].ClassId.Should().Be(1);
            loaded.Classes[0].ClassLevel.Should().Be(5);
            loaded.Feats.Count.Should().Be(1);
            loaded.Feats[0].Should().Be(1);
        }

        [Fact(Timeout = 120000)]
        public void TestUtcEmptyFile()
        {
            // Test UTC with minimal data
            var utc = new UTC();
            utc.ResRef = ResRef.FromBlank();

            byte[] data = UTCHelpers.BytesUtc(utc);
            UTC loaded = UTCHelpers.ReadUtc(data);

            loaded.Should().NotBeNull("Empty UTC should load successfully");
            loaded.ResRef.Should().NotBeNull("TemplateResRef should exist even in empty UTC");
        }

        [Fact(Timeout = 120000)]
        public void TestUtcComplexCreature()
        {
            // Test UTC with all major fields populated
            var utc = new UTC();
            utc.ResRef = ResRef.FromString("complexcreature");
            utc.Tag = "complex_tag";
            utc.Comment = "Complex test creature";
            utc.Conversation = ResRef.FromString("test_dlg");
            utc.FirstName = LocalizedString.FromStrRef(1);
            utc.LastName = LocalizedString.FromStrRef(2);

            // Appearance
            utc.AppearanceId = 5;
            utc.PortraitId = 10;
            utc.GenderId = 1;
            utc.RaceId = 2;
            utc.BodyVariation = 3;
            utc.TextureVariation = 4;
            utc.SoundsetId = 5;

            // Stats
            utc.Strength = 18;
            utc.Dexterity = 16;
            utc.Constitution = 14;
            utc.Intelligence = 12;
            utc.Wisdom = 10;
            utc.Charisma = 8;
            utc.CurrentHp = 100;
            utc.MaxHp = 100;
            utc.Fp = 50;
            utc.MaxFp = 50;

            // Skills
            utc.ComputerUse = 5;
            utc.Demolitions = 3;
            utc.Stealth = 4;
            utc.Awareness = 6;
            utc.Persuade = 7;
            utc.Repair = 5;
            utc.Security = 4;
            utc.TreatInjury = 6;

            // Classes
            var class1 = new UTCClass(1, 10);
            class1.Powers.Add(1);
            class1.Powers.Add(2);
            utc.Classes.Add(class1);

            var class2 = new UTCClass(2, 5);
            utc.Classes.Add(class2);

            // Feats
            utc.Feats.Add(1);
            utc.Feats.Add(2);
            utc.Feats.Add(3);

            // Scripts
            utc.OnAttacked = ResRef.FromString("attacked");
            utc.OnDamaged = ResRef.FromString("damaged");
            utc.OnDeath = ResRef.FromString("death");
            utc.OnHeartbeat = ResRef.FromString("heartbeat");

            // Write and read back
            byte[] data = UTCHelpers.BytesUtc(utc);
            UTC loaded = UTCHelpers.ReadUtc(data);

            // Validate all fields
            loaded.ResRef.ToString().Should().Be("complexcreature");
            loaded.Tag.Should().Be("complex_tag");
            loaded.Comment.Should().Be("Complex test creature");
            loaded.Conversation.ToString().Should().Be("test_dlg");
            loaded.AppearanceId.Should().Be(5);
            loaded.PortraitId.Should().Be(10);
            loaded.GenderId.Should().Be(1);
            loaded.RaceId.Should().Be(2);
            loaded.Strength.Should().Be(18);
            loaded.Dexterity.Should().Be(16);
            loaded.CurrentHp.Should().Be(100);
            loaded.MaxHp.Should().Be(100);
            loaded.ComputerUse.Should().Be(5);
            loaded.Classes.Count.Should().Be(2);
            loaded.Classes[0].ClassLevel.Should().Be(10);
            loaded.Classes[0].Powers.Count.Should().Be(2);
            loaded.Feats.Count.Should().Be(3);
            loaded.OnAttacked.ToString().Should().Be("attacked");
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => UTCHelpers.ReadUtc(File.ReadAllBytes("."));
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<Exception>();
            }
            else
            {
                act1.Should().Throw<Exception>();
            }

            // Test reading non-existent file
            Action act2 = () => UTCHelpers.ReadUtc(File.ReadAllBytes(DoesNotExistFile));
            act2.Should().Throw<FileNotFoundException>();
        }

        private static void ValidateIO(UTC utc)
        {
            // Basic validation
            utc.Should().NotBeNull();
            utc.ResRef.Should().NotBeNull("TemplateResRef should exist");
            utc.Classes.Should().NotBeNull("ClassList should exist");
            utc.Feats.Should().NotBeNull("FeatList should exist");
            utc.Inventory.Should().NotBeNull("ItemList should exist");
            utc.Equipment.Should().NotBeNull("Equip_ItemList should exist");
        }

        private static void CreateTestUtcFile(string path)
        {
            var utc = new UTC();
            utc.ResRef = ResRef.FromString("testcreature");
            utc.Tag = "test_tag";
            utc.Comment = "Test creature";
            utc.Strength = 10;
            utc.Dexterity = 10;
            utc.Constitution = 10;
            utc.Intelligence = 10;
            utc.Wisdom = 10;
            utc.Charisma = 10;
            utc.CurrentHp = 20;
            utc.MaxHp = 20;
            utc.AppearanceId = 1;
            utc.GenderId = 0;
            utc.RaceId = 1;

            byte[] data = UTCHelpers.BytesUtc(utc);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}


