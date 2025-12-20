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
using static Andastra.Parsing.Formats.GFF.GFFAuto;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTD binary I/O operations.
    /// Tests validate the UTD format structure as defined in UTD.ksy Kaitai Struct definition.
    /// UTD files are GFF-based format files with file type signature "UTD ".
    /// </summary>
    public class UTDFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.utd");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.utd");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtdFile(BinaryTestFile);
            }

            // Test reading UTD file
            UTD utd = ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(BinaryTestFile));
            ValidateIO(utd);

            // Test writing and reading back
            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            utd = ResourceAutoHelpers.ReadUtd(data);
            ValidateIO(utd);
        }

        [Fact(Timeout = 120000)]
        public void TestUtdGffHeaderStructure()
        {
            // Test that UTD GFF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtdFile(BinaryTestFile);
            }

            // Read raw GFF header bytes (56 bytes)
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate file type signature matches UTD.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTD ", "File type should be 'UTD ' (space-padded) as defined in UTD.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTD.ksy valid values");

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
        public void TestUtdFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtdFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTD.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTD ", "File type should be 'UTD ' (space-padded) as defined in UTD.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match UTD.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdInvalidSignature()
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

                Action act = () => ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(tempFile));
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
        public void TestUtdInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("UTD ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(tempFile));
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
        public void TestUtdBasicFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtdFile(BinaryTestFile);
            }

            UTD utd = ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(BinaryTestFile));

            // Validate basic fields exist and have reasonable values
            utd.Should().NotBeNull("UTD should not be null");
            utd.Tag.Should().NotBeNull("Tag should not be null");
            utd.ResRef.Should().NotBeNull("ResRef should not be null");
            utd.Name.Should().NotBeNull("Name should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdLockMechanics()
        {
            var utd = new UTD();
            utd.Lockable = true;
            utd.Locked = true;
            utd.KeyRequired = true;
            utd.KeyName = "test_key";
            utd.UnlockDc = 25;
            utd.AutoRemoveKey = true;

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.Lockable.Should().BeTrue("Lockable should be preserved");
            loaded.Locked.Should().BeTrue("Locked should be preserved");
            loaded.KeyRequired.Should().BeTrue("KeyRequired should be preserved");
            loaded.KeyName.Should().Be("test_key", "KeyName should be preserved");
            loaded.UnlockDc.Should().Be(25, "UnlockDc should be preserved");
            loaded.AutoRemoveKey.Should().BeTrue("AutoRemoveKey should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdHpAndStats()
        {
            var utd = new UTD();
            utd.MaximumHp = 100;
            utd.CurrentHp = 75;
            utd.Hardness = 10;
            utd.Fortitude = 15;
            utd.Reflex = 12;
            utd.Willpower = 8;

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.MaximumHp.Should().Be(100, "MaximumHp should be preserved");
            loaded.CurrentHp.Should().Be(75, "CurrentHp should be preserved");
            loaded.Hardness.Should().Be(10, "Hardness should be preserved");
            loaded.Fortitude.Should().Be(15, "Fortitude should be preserved");
            loaded.Reflex.Should().Be(12, "Reflex should be preserved");
            loaded.Willpower.Should().Be(8, "Willpower should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdScriptHooks()
        {
            var utd = new UTD();
            utd.OnClick = new ResRef("onclick");
            utd.OnOpen = new ResRef("onopen");
            utd.OnClosed = new ResRef("onclosed");
            utd.OnDeath = new ResRef("ondeath");
            utd.OnHeartbeat = new ResRef("onheartbeat");
            utd.OnLock = new ResRef("onlock");
            utd.OnUnlock = new ResRef("onunlock");
            utd.OnMelee = new ResRef("onmelee");
            utd.OnDamaged = new ResRef("ondamaged");
            utd.OnOpenFailed = new ResRef("onfailtoopen");
            utd.OnUserDefined = new ResRef("onuserdefined");
            utd.OnPower = new ResRef("onspellcastat");

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.OnClick.ToString().Should().Be("onclick", "OnClick should be preserved");
            loaded.OnOpen.ToString().Should().Be("onopen", "OnOpen should be preserved");
            loaded.OnClosed.ToString().Should().Be("onclosed", "OnClosed should be preserved");
            loaded.OnDeath.ToString().Should().Be("ondeath", "OnDeath should be preserved");
            loaded.OnHeartbeat.ToString().Should().Be("onheartbeat", "OnHeartbeat should be preserved");
            loaded.OnLock.ToString().Should().Be("onlock", "OnLock should be preserved");
            loaded.OnUnlock.ToString().Should().Be("onunlock", "OnUnlock should be preserved");
            loaded.OnMelee.ToString().Should().Be("onmelee", "OnMelee should be preserved");
            loaded.OnDamaged.ToString().Should().Be("ondamaged", "OnDamaged should be preserved");
            loaded.OnOpenFailed.ToString().Should().Be("onfailtoopen", "OnOpenFailed should be preserved");
            loaded.OnUserDefined.ToString().Should().Be("onuserdefined", "OnUserDefined should be preserved");
            loaded.OnPower.ToString().Should().Be("onspellcastat", "OnPower should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdKotor2Fields()
        {
            var utd = new UTD();
            utd.UnlockDiff = 5;
            utd.UnlockDiffMod = -2;
            utd.OpenState = 1;
            utd.NotBlastable = true;
            utd.Min1Hp = true;

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.UnlockDiff.Should().Be(5, "UnlockDiff should be preserved");
            loaded.UnlockDiffMod.Should().Be(-2, "UnlockDiffMod should be preserved");
            loaded.OpenState.Should().Be(1, "OpenState should be preserved");
            loaded.NotBlastable.Should().BeTrue("NotBlastable should be preserved");
            loaded.Min1Hp.Should().BeTrue("Min1Hp should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdAppearanceAndAnimation()
        {
            var utd = new UTD();
            utd.AppearanceId = 42;
            utd.AnimationState = 3;
            utd.PaletteId = 7;

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.AppearanceId.Should().Be(42, "AppearanceId should be preserved");
            loaded.AnimationState.Should().Be(3, "AnimationState should be preserved");
            loaded.PaletteId.Should().Be(7, "PaletteId should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdTrapProperties()
        {
            var utd = new UTD();
            utd.TrapDetectable = true;
            utd.TrapDisarmable = true;
            utd.DisarmDc = 20;
            utd.TrapOneShot = false;
            utd.TrapType = 3;

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2, useDeprecated: true);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.TrapDetectable.Should().BeTrue("TrapDetectable should be preserved");
            loaded.TrapDisarmable.Should().BeTrue("TrapDisarmable should be preserved");
            loaded.DisarmDc.Should().Be(20, "DisarmDc should be preserved");
            loaded.TrapOneShot.Should().BeFalse("TrapOneShot should be preserved");
            loaded.TrapType.Should().Be(3, "TrapType should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdFactionAndConversation()
        {
            var utd = new UTD();
            utd.FactionId = 5;
            utd.Conversation = new ResRef("door_conv");
            utd.Plot = true;
            utd.Static = false;

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.FactionId.Should().Be(5, "FactionId should be preserved");
            loaded.Conversation.ToString().Should().Be("door_conv", "Conversation should be preserved");
            loaded.Plot.Should().BeTrue("Plot should be preserved");
            loaded.Static.Should().BeFalse("Static should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdLocalizedStrings()
        {
            var utd = new UTD();
            var name = new LocalizedString();
            name.SetData(Language.English, Gender.Male, "Test Door");
            name.SetData(Language.English, Gender.Female, "Test Door");
            utd.Name = name;

            var description = new LocalizedString();
            description.Set(Language.English, Gender.Male, "A test door");
            utd.Description = description;

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2, useDeprecated: true);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.Name.Get(Language.English, Gender.Male).Should().Be("Test Door", "Name should be preserved");
            loaded.Description.Get(Language.English, Gender.Male).Should().Be("A test door", "Description should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdEmptyFile()
        {
            // Test UTD with minimal fields
            var utd = new UTD();
            utd.Tag = "test_door";
            utd.ResRef = new ResRef("testdoor");

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K1);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.Tag.Should().Be("test_door");
            loaded.ResRef.ToString().Should().Be("testdoor");
        }

        [Fact(Timeout = 120000)]
        public void TestUtdRoundTripK1()
        {
            var utd = new UTD();
            utd.Tag = "k1_door";
            utd.ResRef = new ResRef("k1door");
            utd.Lockable = true;
            utd.Locked = false;
            utd.MaximumHp = 50;
            utd.CurrentHp = 50;

            // K1 should not include K2-only fields
            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K1);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.Tag.Should().Be("k1_door");
            loaded.ResRef.ToString().Should().Be("k1door");
            loaded.Lockable.Should().BeTrue();
            loaded.Locked.Should().BeFalse();
            loaded.MaximumHp.Should().Be(50);
            loaded.CurrentHp.Should().Be(50);
        }

        [Fact(Timeout = 120000)]
        public void TestUtdRoundTripK2()
        {
            var utd = new UTD();
            utd.Tag = "k2_door";
            utd.ResRef = new ResRef("k2door");
            utd.UnlockDiff = 10;
            utd.UnlockDiffMod = -5;
            utd.OpenState = 2;
            utd.NotBlastable = true;
            utd.Min1Hp = true;

            // K2 should include K2-only fields
            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            UTD loaded = ResourceAutoHelpers.ReadUtd(data);

            loaded.Tag.Should().Be("k2_door");
            loaded.ResRef.ToString().Should().Be("k2door");
            loaded.UnlockDiff.Should().Be(10);
            loaded.UnlockDiffMod.Should().Be(-5);
            loaded.OpenState.Should().Be(2);
            loaded.NotBlastable.Should().BeTrue();
            loaded.Min1Hp.Should().BeTrue();
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => ResourceAutoHelpers.ReadUtd(File.ReadAllBytes("."));
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(DoesNotExistFile));
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => ResourceAutoHelpers.ReadUtd(File.ReadAllBytes(CorruptBinaryTestFile));
                act3.Should().Throw<InvalidDataException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtdFieldTypes()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtdFile(BinaryTestFile);
            }

            var reader = new GFFBinaryReader(File.ReadAllBytes(BinaryTestFile));
            GFF gff = reader.Load();
            var root = gff.Root;

            // Test that fields are stored with correct types according to UTD.ksy
            // Boolean fields should be UInt8
            if (root.Exists("AutoRemoveKey"))
            {
                root.GetUInt8("AutoRemoveKey").Should().BeInRange((byte)0, (byte)1, "AutoRemoveKey should be UInt8");
            }
            if (root.Exists("Plot"))
            {
                root.GetUInt8("Plot").Should().BeInRange((byte)0, (byte)1, "Plot should be UInt8");
            }
            if (root.Exists("Lockable"))
            {
                root.GetUInt8("Lockable").Should().BeInRange((byte)0, (byte)1, "Lockable should be UInt8");
            }
            if (root.Exists("Locked"))
            {
                root.GetUInt8("Locked").Should().BeInRange((byte)0, (byte)1, "Locked should be UInt8");
            }

            // HP fields should be Int16
            if (root.Exists("HP"))
            {
                root.GetInt16("HP").Should().BeGreaterThanOrEqualTo((short)0, "HP should be Int16");
            }
            if (root.Exists("CurrentHP"))
            {
                root.GetInt16("CurrentHP").Should().BeGreaterThanOrEqualTo((short)0, "CurrentHP should be Int16");
            }

            // Faction should be UInt32
            if (root.Exists("Faction"))
            {
                root.GetUInt32("Faction").Should().BeGreaterThanOrEqualTo(0u, "Faction should be UInt32");
            }
        }

        private static void ValidateIO(UTD utd)
        {
            // Basic validation
            utd.Should().NotBeNull();
            utd.ResRef.Should().NotBeNull();
            utd.Tag.Should().NotBeNull();
            utd.Name.Should().NotBeNull();
        }

        private static void CreateTestUtdFile(string path)
        {
            var utd = new UTD();
            utd.Tag = "test_door";
            utd.ResRef = new ResRef("testdoor");
            var name = new LocalizedString();
            name.Set(Language.English, Gender.Male, "Test Door");
            utd.Name = name;
            utd.Lockable = true;
            utd.Locked = false;
            utd.MaximumHp = 100;
            utd.CurrentHp = 100;
            utd.Hardness = 5;
            utd.Fortitude = 10;
            utd.AppearanceId = 1;
            utd.OnClick = new ResRef("onclick");
            utd.OnOpen = new ResRef("onopen");

            GFF gff = UTDHelpers.DismantleUtd(utd, Game.K2);
            byte[] data = GFFAuto.BytesGff(gff, ResourceType.GFF);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}

