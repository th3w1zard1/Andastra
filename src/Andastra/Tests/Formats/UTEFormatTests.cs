using System;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for UTE (Encounter Template) binary I/O operations.
    /// Tests validate the UTE format structure as defined in UTE.ksy Kaitai Struct definition.
    /// UTE files are GFF-based format files that store encounter definitions including
    /// creature spawn lists, difficulty, respawn settings, and script hooks.
    /// </summary>
    public class UTEFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.ute");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.ute");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            // Test reading UTE file
            GFF gff = GFFAuto.ReadGff(BinaryTestFile, 0, null);
            UTE ute = UTEHelpers.ConstructUte(gff);
            ValidateIO(ute);

            // Test writing and reading back
            GFF writtenGff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(writtenGff);
            gff = GFFAuto.ReadGff(data, 0, null);
            ute = UTEHelpers.ConstructUte(gff);
            ValidateIO(ute);
        }

        [Fact(Timeout = 120000)]
        public void TestUteHeaderStructure()
        {
            // Test that UTE header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            // Read GFF header to validate structure
            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate GFF header constants match UTE.ksy
            // GFF header is 56 bytes (14 fields * 4 bytes each)
            gff.Content.Should().Be(GFFContent.UTE, "UTE file should have UTE content type");
        }

        [Fact(Timeout = 120000)]
        public void TestUteFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTE.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTE ", "File type should be 'UTE ' (space-padded) as defined in UTE.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf(new[] { "V3.2", "V3.3", "V4.0", "V4.1" }, "Version should match UTE.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestUteBasicProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            GFF gff = GFFAuto.ReadGff(BinaryTestFile, 0, null);
            UTE ute = UTEHelpers.ConstructUte(gff);

            // Validate basic UTE properties exist
            ute.Tag.Should().NotBeNull("Tag should not be null");
            ute.ResRef.Should().NotBeNull("ResRef should not be null");
            ute.Comment.Should().NotBeNull("Comment should not be null");
            ute.Name.Should().NotBeNull("Name should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUteSpawnConfiguration()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            GFF gff = GFFAuto.ReadGff(BinaryTestFile, 0, null);
            UTE ute = UTEHelpers.ConstructUte(gff);

            // Validate spawn configuration properties
            // Active is already a bool type, so we just verify it's accessible
            _ = ute.Active; // Verify property is accessible
            ute.DifficultyId.Should().BeGreaterThanOrEqualTo(0, "DifficultyId should be non-negative");
            ute.DifficultyIndex.Should().BeGreaterThanOrEqualTo(0, "DifficultyIndex should be non-negative");
            ute.Faction.Should().BeGreaterThanOrEqualTo(0, "Faction should be non-negative");
            ute.MaxCreatures.Should().BeGreaterThanOrEqualTo(0, "MaxCreatures should be non-negative");
            ute.RecCreatures.Should().BeGreaterThanOrEqualTo(0, "RecCreatures should be non-negative");
            ute.SingleSpawn.Should().BeGreaterThanOrEqualTo(0, "SingleSpawn should be non-negative");
            ute.PlayerOnly.Should().BeGreaterThanOrEqualTo(0, "PlayerOnly should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestUteRespawnLogic()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            GFF gff = GFFAuto.ReadGff(BinaryTestFile, 0, null);
            UTE ute = UTEHelpers.ConstructUte(gff);

            // Validate respawn logic properties
            ute.Reset.Should().BeGreaterThanOrEqualTo(0, "Reset should be non-negative");
            ute.ResetTime.Should().BeGreaterThanOrEqualTo(0, "ResetTime should be non-negative");
            // Respawns can be -1 for infinite
            ute.Respawn.Should().BeGreaterThanOrEqualTo(-1, "Respawn should be >= -1 (infinite allowed)");
        }

        [Fact(Timeout = 120000)]
        public void TestUteScriptHooks()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            GFF gff = GFFAuto.ReadGff(BinaryTestFile, 0, null);
            UTE ute = UTEHelpers.ConstructUte(gff);

            // Validate script hooks exist
            ute.OnEnteredScript.Should().NotBeNull("OnEnteredScript should not be null");
            ute.OnExitScript.Should().NotBeNull("OnExitScript should not be null");
            ute.OnExhaustedScript.Should().NotBeNull("OnExhaustedScript should not be null");
            ute.OnHeartbeatScript.Should().NotBeNull("OnHeartbeatScript should not be null");
            ute.OnUserDefinedScript.Should().NotBeNull("OnUserDefinedScript should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUteCreatureList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUteFile(BinaryTestFile);
            }

            GFF gff = GFFAuto.ReadGff(BinaryTestFile, 0, null);
            UTE ute = UTEHelpers.ConstructUte(gff);

            // Validate creature list exists
            ute.Creatures.Should().NotBeNull("Creatures list should not be null");
            ute.Creatures.Count.Should().BeGreaterThanOrEqualTo(0, "Creature count should be non-negative");

            // Validate each creature in the list
            foreach (var creature in ute.Creatures)
            {
                creature.Should().NotBeNull("Creature should not be null");
                creature.ResRef.Should().NotBeNull("Creature ResRef should not be null");
                creature.Appearance.Should().BeGreaterThanOrEqualTo(0, "Creature Appearance should be non-negative");
                creature.CR.Should().BeGreaterThanOrEqualTo(0, "Creature CR should be non-negative");
                creature.SingleSpawn.Should().BeGreaterThanOrEqualTo(0, "Creature SingleSpawn should be non-negative");
                creature.GuaranteedCount.Should().BeGreaterThanOrEqualTo(0, "Creature GuaranteedCount should be non-negative");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUteEmptyFile()
        {
            // Test UTE with no creatures
            var ute = new UTE();
            ute.Tag = "test_encounter";
            ute.ResRef = ResRef.FromString("test_encounter");
            ute.Creatures.Should().NotBeNull("Creatures should not be null");
            ute.Creatures.Count.Should().Be(0, "Empty UTE should have 0 creatures");

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Creatures.Count.Should().Be(0);
            loaded.Tag.Should().Be("test_encounter");
        }

        [Fact(Timeout = 120000)]
        public void TestUteMultipleCreatures()
        {
            // Test UTE with multiple creatures
            var ute = new UTE();
            ute.Tag = "multi_creature_encounter";
            ute.ResRef = ResRef.FromString("multi_encounter");
            ute.MaxCreatures = 5;
            ute.RecCreatures = 3;

            // Create multiple creatures
            var creature1 = new UTECreature
            {
                ResRef = ResRef.FromString("creature1"),
                Appearance = 1,
                CR = 5,
                SingleSpawn = 0,
                GuaranteedCount = 1
            };
            var creature2 = new UTECreature
            {
                ResRef = ResRef.FromString("creature2"),
                Appearance = 2,
                CR = 3,
                SingleSpawn = 1,
                GuaranteedCount = 0
            };
            ute.Creatures.Add(creature1);
            ute.Creatures.Add(creature2);

            ute.Creatures.Count.Should().Be(2);

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Creatures.Count.Should().Be(2);
            loaded.Creatures[0].ResRef.ToString().Should().Be("creature1");
            loaded.Creatures[1].ResRef.ToString().Should().Be("creature2");
        }

        [Fact(Timeout = 120000)]
        public void TestUteActiveProperty()
        {
            var ute = new UTE();
            ute.Tag = "active_encounter";
            ute.Active = true;

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Active.Should().BeTrue();
        }

        [Fact(Timeout = 120000)]
        public void TestUteInactiveProperty()
        {
            var ute = new UTE();
            ute.Tag = "inactive_encounter";
            ute.Active = false;

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Active.Should().BeFalse();
        }

        [Fact(Timeout = 120000)]
        public void TestUteDifficultySettings()
        {
            var ute = new UTE();
            ute.Tag = "difficulty_encounter";
            ute.DifficultyId = 5;
            ute.DifficultyIndex = 3;
            ute.Faction = 2;

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.DifficultyId.Should().Be(5);
            loaded.DifficultyIndex.Should().Be(3);
            loaded.Faction.Should().Be(2);
        }

        [Fact(Timeout = 120000)]
        public void TestUteSpawnLimits()
        {
            var ute = new UTE();
            ute.Tag = "spawn_limits_encounter";
            ute.MaxCreatures = 10;
            ute.RecCreatures = 7;
            ute.SingleSpawn = 1;
            ute.PlayerOnly = 1;

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.MaxCreatures.Should().Be(10);
            loaded.RecCreatures.Should().Be(7);
            loaded.SingleSpawn.Should().Be(1);
            loaded.PlayerOnly.Should().Be(1);
        }

        [Fact(Timeout = 120000)]
        public void TestUteRespawnSettings()
        {
            var ute = new UTE();
            ute.Tag = "respawn_encounter";
            ute.Reset = 1;
            ute.ResetTime = 300;
            ute.Respawn = -1; // Infinite respawns

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Reset.Should().Be(1);
            loaded.ResetTime.Should().Be(300);
            loaded.Respawn.Should().Be(-1);
        }

        [Fact(Timeout = 120000)]
        public void TestUteScriptReferences()
        {
            var ute = new UTE();
            ute.Tag = "script_encounter";
            ute.OnEnteredScript = ResRef.FromString("on_entered");
            ute.OnExitScript = ResRef.FromString("on_exit");
            ute.OnExhaustedScript = ResRef.FromString("on_exhausted");
            ute.OnHeartbeatScript = ResRef.FromString("on_heartbeat");
            ute.OnUserDefinedScript = ResRef.FromString("on_user");

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.OnEnteredScript.ToString().Should().Be("on_entered");
            loaded.OnExitScript.ToString().Should().Be("on_exit");
            loaded.OnExhaustedScript.ToString().Should().Be("on_exhausted");
            loaded.OnHeartbeatScript.ToString().Should().Be("on_heartbeat");
            loaded.OnUserDefinedScript.ToString().Should().Be("on_user");
        }

        [Fact(Timeout = 120000)]
        public void TestUteCreatureProperties()
        {
            var ute = new UTE();
            ute.Tag = "creature_props_encounter";

            var creature = new UTECreature
            {
                ResRef = ResRef.FromString("test_creature"),
                Appearance = 42,
                CR = 10,
                SingleSpawn = 1,
                GuaranteedCount = 2
            };
            ute.Creatures.Add(creature);

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Creatures.Count.Should().Be(1);
            loaded.Creatures[0].ResRef.ToString().Should().Be("test_creature");
            loaded.Creatures[0].Appearance.Should().Be(42);
            loaded.Creatures[0].CR.Should().Be(10);
            loaded.Creatures[0].SingleSpawn.Should().Be(1);
            loaded.Creatures[0].GuaranteedCount.Should().Be(2);
        }

        [Fact(Timeout = 120000)]
        public void TestUteRoundTrip()
        {
            // Test creating UTE, writing it, and reading it back
            var ute = new UTE();
            ute.Tag = "roundtrip_encounter";
            ute.ResRef = ResRef.FromString("roundtrip_encounter");
            ute.Comment = "Test encounter for roundtrip";
            ute.Active = true;
            ute.DifficultyId = 3;
            ute.DifficultyIndex = 2;
            ute.Faction = 1;
            ute.MaxCreatures = 8;
            ute.RecCreatures = 5;
            ute.Reset = 1;
            ute.ResetTime = 180;
            ute.Respawn = 3;
            ute.SingleSpawn = 0;
            ute.PlayerOnly = 1;
            ute.OnEnteredScript = ResRef.FromString("entered_script");
            ute.OnExitScript = ResRef.FromString("exit_script");
            ute.OnExhaustedScript = ResRef.FromString("exhausted_script");
            ute.OnHeartbeatScript = ResRef.FromString("heartbeat_script");
            ute.OnUserDefinedScript = ResRef.FromString("user_script");

            // Add multiple creatures with different properties
            var creature1 = new UTECreature
            {
                ResRef = ResRef.FromString("creature_a"),
                Appearance = 10,
                CR = 5,
                SingleSpawn = 0,
                GuaranteedCount = 1
            };
            var creature2 = new UTECreature
            {
                ResRef = ResRef.FromString("creature_b"),
                Appearance = 20,
                CR = 8,
                SingleSpawn = 1,
                GuaranteedCount = 0
            };
            ute.Creatures.Add(creature1);
            ute.Creatures.Add(creature2);

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Tag.Should().Be(ute.Tag);
            loaded.ResRef.ToString().Should().Be(ute.ResRef.ToString());
            loaded.Comment.Should().Be(ute.Comment);
            loaded.Active.Should().Be(ute.Active);
            loaded.DifficultyId.Should().Be(ute.DifficultyId);
            loaded.DifficultyIndex.Should().Be(ute.DifficultyIndex);
            loaded.Faction.Should().Be(ute.Faction);
            loaded.MaxCreatures.Should().Be(ute.MaxCreatures);
            loaded.RecCreatures.Should().Be(ute.RecCreatures);
            loaded.Reset.Should().Be(ute.Reset);
            loaded.ResetTime.Should().Be(ute.ResetTime);
            loaded.Respawn.Should().Be(ute.Respawn);
            loaded.SingleSpawn.Should().Be(ute.SingleSpawn);
            loaded.PlayerOnly.Should().Be(ute.PlayerOnly);
            loaded.OnEnteredScript.ToString().Should().Be(ute.OnEnteredScript.ToString());
            loaded.OnExitScript.ToString().Should().Be(ute.OnExitScript.ToString());
            loaded.OnExhaustedScript.ToString().Should().Be(ute.OnExhaustedScript.ToString());
            loaded.OnHeartbeatScript.ToString().Should().Be(ute.OnHeartbeatScript.ToString());
            loaded.OnUserDefinedScript.ToString().Should().Be(ute.OnUserDefinedScript.ToString());
            loaded.Creatures.Count.Should().Be(ute.Creatures.Count);

            for (int i = 0; i < loaded.Creatures.Count; i++)
            {
                loaded.Creatures[i].ResRef.ToString().Should().Be(ute.Creatures[i].ResRef.ToString());
                loaded.Creatures[i].Appearance.Should().Be(ute.Creatures[i].Appearance);
                loaded.Creatures[i].CR.Should().Be(ute.Creatures[i].CR);
                loaded.Creatures[i].SingleSpawn.Should().Be(ute.Creatures[i].SingleSpawn);
                loaded.Creatures[i].GuaranteedCount.Should().Be(ute.Creatures[i].GuaranteedCount);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => GFFAuto.ReadGff(".", 0, null);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => GFFAuto.ReadGff(DoesNotExistFile, 0, null);
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => GFFAuto.ReadGff(CorruptBinaryTestFile, 0, null);
                act3.Should().Throw<ArgumentException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUteInvalidSignature()
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

                Action act = () => GFFAuto.ReadGff(tempFile, 0, null);
                act.Should().Throw<ArgumentException>();
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
        public void TestUteInvalidVersion()
        {
            // Create file with invalid version by manually constructing GFF binary data
            string tempFile = Path.GetTempFileName();
            try
            {
                // Manually construct a minimal valid GFF binary with invalid version
                // This provides comprehensive testing of version validation rather than
                // creating a valid GFF and corrupting it post-write

                using (var fs = File.Create(tempFile))
                using (var writer = new Andastra.Parsing.Common.RawBinaryWriterFile(fs))
                {
                    // GFF Header (56 bytes total)
                    // Content type: "UTE " (4 bytes)
                    writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes("UTE "));

                    // Version: "V9.9" (invalid version - should cause validation failure)
                    writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes("V9.9"));

                    // Struct section: offset=56, count=1 (root struct)
                    writer.WriteUInt32(56u);

                    // Field section: offset=68, count=1 (single "Tag" field)
                    writer.WriteUInt32(68u);

                    // Label section: offset=84, count=1 (single "Tag" label)
                    writer.WriteUInt32(84u);

                    // Field data section: offset=100, count=4 (4 bytes for "test" string)
                    writer.WriteUInt32(100u);

                    // Field indices section: offset=104, count=4 (4 bytes for field index)
                    writer.WriteUInt32(104u);

                    // List indices section: offset=108, count=0 (no lists)
                    writer.WriteUInt32(108u);

                    // Struct Data Section (12 bytes for root struct)
                    // Struct entry: fieldCount=1, fieldIndex=0, type=GFFStructType.Normal (0)
                    writer.WriteUInt32(1u);  // fieldCount
                    writer.WriteUInt32(0u);  // fieldIndex
                    writer.WriteUInt32(0u);  // structType

                    // Field Data Section (16 bytes for single field)
                    // Field entry: type=GFFFieldType.String (10), labelIndex=0, dataOrDataOffset=0
                    writer.WriteUInt32((uint)GFFFieldType.String);  // fieldType
                    writer.WriteUInt32(0u);  // labelIndex
                    writer.WriteUInt32(0u);  // dataOrDataOffset

                    // Label Data Section (16 bytes for "Tag" label, null-padded)
                    writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes("Tag"));  // 3 bytes
                    writer.WriteBytes(new byte[13]);  // 13 null bytes for padding

                    // Field Data Content Section (4 bytes + string data)
                    // String length (4 bytes) + "test" (4 bytes)
                    writer.WriteUInt32(4u);  // string length
                    writer.WriteBytes(System.Text.Encoding.ASCII.GetBytes("test"));  // string data

                    // Field Indices Section (4 bytes for single field index)
                    writer.WriteUInt32(0u);  // field index 0

                    // List Indices Section (empty - 0 bytes)
                    // No list data needed
                }

                // This should fail when reading due to invalid version "V9.9"
                Action act = () => GFFAuto.ReadGff(tempFile, 0, null);
                act.Should().Throw<InvalidDataException>("Reading GFF with invalid version should throw InvalidDataException")
                   .WithMessage("The GFF version of the file is unsupported.");
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
        public void TestUtePaletteId()
        {
            var ute = new UTE();
            ute.Tag = "palette_encounter";
            ute.PaletteId = 5;

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2, true);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.PaletteId.Should().Be(5);
        }

        [Fact(Timeout = 120000)]
        public void TestUteLocalizedName()
        {
            var ute = new UTE();
            ute.Tag = "localized_encounter";
            ute.Name = LocalizedString.FromStringId(12345);

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2, true);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Name.StringRef.Should().Be(12345);
        }

        [Fact(Timeout = 120000)]
        public void TestUteCreatureChallengeRating()
        {
            var ute = new UTE();
            ute.Tag = "cr_encounter";

            var creature = new UTECreature
            {
                ResRef = ResRef.FromString("cr_creature"),
                CR = 15
            };
            ute.Creatures.Add(creature);

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Creatures[0].CR.Should().Be(15);
            loaded.Creatures[0].ChallengeRating.Should().Be(15.0f);
        }

        [Fact(Timeout = 120000)]
        public void TestUteCreatureSingleSpawnBool()
        {
            var ute = new UTE();
            ute.Tag = "single_spawn_encounter";

            var creature = new UTECreature
            {
                ResRef = ResRef.FromString("single_creature"),
                SingleSpawnBool = true
            };
            ute.Creatures.Add(creature);

            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Creatures[0].SingleSpawn.Should().Be(1);
            loaded.Creatures[0].SingleSpawnBool.Should().BeTrue();
        }

        [Fact(Timeout = 120000)]
        public void TestUteKotor2GuaranteedCount()
        {
            var ute = new UTE();
            ute.Tag = "k2_guaranteed_encounter";

            var creature = new UTECreature
            {
                ResRef = ResRef.FromString("k2_creature"),
                GuaranteedCount = 3
            };
            ute.Creatures.Add(creature);

            // Test with K2 game
            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            GFF loadedGff = GFFAuto.ReadGff(data, 0, null);
            UTE loaded = UTEHelpers.ConstructUte(loadedGff);

            loaded.Creatures[0].GuaranteedCount.Should().Be(3);
        }

        private static void ValidateIO(UTE ute)
        {
            // Basic validation
            ute.Should().NotBeNull("UTE should not be null");
            ute.Tag.Should().NotBeNull("Tag should not be null");
            ute.ResRef.Should().NotBeNull("ResRef should not be null");
            ute.Creatures.Should().NotBeNull("Creatures should not be null");
            ute.Creatures.Count.Should().BeGreaterThanOrEqualTo(0, "Creature count should be non-negative");
        }

        private static void CreateTestUteFile(string path)
        {
            var ute = new UTE();
            ute.Tag = "test_encounter";
            ute.ResRef = ResRef.FromString("test_encounter");
            ute.Comment = "Test encounter file";
            ute.Active = true;
            ute.DifficultyId = 1;
            ute.DifficultyIndex = 0;
            ute.Faction = 0;
            ute.MaxCreatures = 5;
            ute.RecCreatures = 3;
            ute.Reset = 0;
            ute.ResetTime = 0;
            ute.Respawn = 0;
            ute.SingleSpawn = 0;
            ute.PlayerOnly = 0;

            // Add a test creature
            var creature = new UTECreature
            {
                ResRef = ResRef.FromString("test_creature"),
                Appearance = 1,
                CR = 5,
                SingleSpawn = 0,
                GuaranteedCount = 0
            };
            ute.Creatures.Add(creature);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            GFF gff = UTEHelpers.DismantleUte(ute, BioWareGame.K2);
            byte[] data = GFFAuto.BytesGff(gff);
            File.WriteAllBytes(path, data);
        }
    }
}

