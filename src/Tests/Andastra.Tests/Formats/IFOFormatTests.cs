using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for IFO binary I/O operations.
    /// Tests validate the IFO format structure as defined in IFO.ksy Kaitai Struct definition.
    /// </summary>
    public class IFOFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.ifo");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.ifo");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            // Test reading IFO file via GFF
            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);
            ValidateIO(ifo);

            // Test writing and reading back
            GFF writtenGff = IFOHelpers.DismantleIfo(ifo);
            byte[] data = new GFFBinaryWriter(writtenGff).Write();
            gff = new GFFBinaryReader(data).Load();
            ifo = IFOHelpers.ConstructIfo(gff);
            ValidateIO(ifo);
        }

        [Fact(Timeout = 120000)]
        public void TestIfoHeaderStructure()
        {
            // Test that IFO header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate header constants match IFO.ksy
            // Header is 56 bytes: 4 (file_type) + 4 (file_version) + 11×4 (offsets/counts)
            const int ExpectedHeaderSize = 56;
            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().BeGreaterThanOrEqualTo(ExpectedHeaderSize, "IFO file should have at least 56-byte header as defined in IFO.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate file type signature matches IFO.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("IFO ", "File type should be 'IFO ' (space-padded) as defined in IFO.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V3.2", "Version should be 'V3.2' for KotOR as defined in IFO.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoModId()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate Mod_ID is 16 bytes (or 32 bytes in savegames)
            ifo.ModId.Should().NotBeNull("Mod_ID should not be null");
            ifo.ModId.Length.Should().BeGreaterThanOrEqualTo(16, "Mod_ID should be at least 16 bytes");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoModName()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate Mod_Name is a LocalizedString
            ifo.ModName.Should().NotBeNull("Mod_Name should not be null");
            ifo.Name.Should().NotBeNull("Name should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoEntryConfiguration()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate entry area
            ifo.EntryArea.Should().NotBeNull("Entry_Area should not be null");
            ifo.ResRef.Should().NotBeNull("ResRef should not be null");

            // Validate entry position (coordinates can be any float value)
            // Just check that they are valid floats
            float.IsNaN(ifo.EntryX).Should().BeFalse("Entry_X should be a valid float");
            float.IsNaN(ifo.EntryY).Should().BeFalse("Entry_Y should be a valid float");
            float.IsNaN(ifo.EntryZ).Should().BeFalse("Entry_Z should be a valid float");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoEntryDirection()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate entry direction components
            float.IsNaN(ifo.EntryDirectionX).Should().BeFalse("Entry_Dir_X should be a valid float");
            float.IsNaN(ifo.EntryDirectionY).Should().BeFalse("Entry_Dir_Y should be a valid float");
            float.IsNaN(ifo.EntryDirectionZ).Should().BeFalse("Entry_Dir_Z should be a valid float");
            float.IsNaN(ifo.EntryDirection).Should().BeFalse("EntryDirection should be a valid float");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoScriptHooks()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate all script hooks are ResRefs (can be blank)
            ifo.OnClientEnter.Should().NotBeNull("OnClientEnter should not be null");
            ifo.OnClientLeave.Should().NotBeNull("OnClientLeave should not be null");
            ifo.OnHeartbeat.Should().NotBeNull("OnHeartbeat should not be null");
            ifo.OnUserDefined.Should().NotBeNull("OnUserDefined should not be null");
            ifo.OnActivateItem.Should().NotBeNull("OnActivateItem should not be null");
            ifo.OnAcquireItem.Should().NotBeNull("OnAcquireItem should not be null");
            ifo.OnUnacquireItem.Should().NotBeNull("OnUnacquireItem should not be null");
            ifo.OnPlayerDeath.Should().NotBeNull("OnPlayerDeath should not be null");
            ifo.OnPlayerDying.Should().NotBeNull("OnPlayerDying should not be null");
            ifo.OnPlayerRespawn.Should().NotBeNull("OnPlayerRespawn should not be null");
            ifo.OnPlayerRest.Should().NotBeNull("OnPlayerRest should not be null");
            ifo.OnPlayerLevelUp.Should().NotBeNull("OnPlayerLevelUp should not be null");
            ifo.OnLoad.Should().NotBeNull("OnLoad should not be null");
            ifo.OnStart.Should().NotBeNull("OnStart should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoAreaList()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate area list structure
            ifo.AreaList.Should().NotBeNull("AreaList should not be null");
            ifo.AreaList.Count.Should().BeGreaterThanOrEqualTo(0, "AreaList count should be non-negative");

            // Each area should be a valid ResRef
            foreach (var area in ifo.AreaList)
            {
                area.Should().NotBeNull("Area ResRef should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestIfoExpansionPack()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate expansion pack is a valid integer (bit flags)
            ifo.ExpansionPack.Should().BeGreaterThanOrEqualTo(0, "Expansion_Pack should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoTimeSettings()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate time settings
            ifo.DawnHour.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(23, "DawnHour should be 0-23");
            ifo.DuskHour.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(23, "DuskHour should be 0-23");
            ifo.TimeScale.Should().BeGreaterThanOrEqualTo(0, "TimeScale should be non-negative");
            ifo.StartMonth.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(24, "StartMonth should be 0-24");
            ifo.StartDay.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(31, "StartDay should be 0-31");
            ifo.StartHour.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(23, "StartHour should be 0-23");
            ifo.StartYear.Should().BeGreaterThanOrEqualTo(0, "StartYear should be non-negative");
            ifo.XpScale.Should().BeGreaterThanOrEqualTo(0).And.BeLessThanOrEqualTo(200, "XpScale should be 0-200");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoModuleMetadata()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate module metadata
            ifo.Tag.Should().NotBeNull("Tag should not be null");
            ifo.VoId.Should().NotBeNull("VoId should not be null");
            ifo.Hak.Should().NotBeNull("Hak should not be null");
            ifo.Description.Should().NotBeNull("Description should not be null");
            ifo.ModVersion.Should().BeGreaterThanOrEqualTo(0, "ModVersion should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoStartMovie()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();
            IFO ifo = IFOHelpers.ConstructIfo(gff);

            // Validate start movie is a ResRef
            ifo.StartMovie.Should().NotBeNull("StartMovie should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoRoundTrip()
        {
            // Test creating IFO, writing it, and reading it back
            var ifo = new IFO();
            ifo.ModId = new byte[16] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
            ifo.ModName = LocalizedString.FromInvalid();
            ifo.Tag = "TEST_MODULE";
            ifo.ResRef = ResRef.FromString("testarea");
            ifo.EntryX = 10.5f;
            ifo.EntryY = 20.5f;
            ifo.EntryZ = 30.5f;
            ifo.EntryDirectionX = 1.0f;
            ifo.EntryDirectionY = 0.0f;
            ifo.OnHeartbeat = ResRef.FromString("test_heartbeat");
            ifo.OnLoad = ResRef.FromString("test_load");
            ifo.OnStart = ResRef.FromString("test_start");
            ifo.AreaList.Add(ResRef.FromString("testarea"));
            ifo.ExpansionPack = 0;
            ifo.DawnHour = 6;
            ifo.DuskHour = 18;
            ifo.XpScale = 100;

            // Write to GFF and back
            GFF gff = IFOHelpers.DismantleIfo(ifo);
            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loadedGff = new GFFBinaryReader(data).Load();
            IFO loaded = IFOHelpers.ConstructIfo(loadedGff);

            // Verify values match
            loaded.ModId.Should().BeEquivalentTo(ifo.ModId);
            loaded.Tag.Should().Be(ifo.Tag);
            loaded.ResRef.ToString().Should().Be(ifo.ResRef.ToString());
            loaded.EntryX.Should().BeApproximately(ifo.EntryX, 0.0001f);
            loaded.EntryY.Should().BeApproximately(ifo.EntryY, 0.0001f);
            loaded.EntryZ.Should().BeApproximately(ifo.EntryZ, 0.0001f);
            loaded.EntryDirectionX.Should().BeApproximately(ifo.EntryDirectionX, 0.0001f);
            loaded.EntryDirectionY.Should().BeApproximately(ifo.EntryDirectionY, 0.0001f);
            loaded.OnHeartbeat.ToString().Should().Be(ifo.OnHeartbeat.ToString());
            loaded.OnLoad.ToString().Should().Be(ifo.OnLoad.ToString());
            loaded.OnStart.ToString().Should().Be(ifo.OnStart.ToString());
            loaded.AreaList.Count.Should().Be(ifo.AreaList.Count);
            loaded.ExpansionPack.Should().Be(ifo.ExpansionPack);
            loaded.DawnHour.Should().Be(ifo.DawnHour);
            loaded.DuskHour.Should().Be(ifo.DuskHour);
            loaded.XpScale.Should().Be(ifo.XpScale);
        }

        [Fact(Timeout = 120000)]
        public void TestIfoEmptyFile()
        {
            // Test IFO with minimal/default values
            var ifo = new IFO();

            // Write and read back
            GFF gff = IFOHelpers.DismantleIfo(ifo);
            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loadedGff = new GFFBinaryReader(data).Load();
            IFO loaded = IFOHelpers.ConstructIfo(loadedGff);

            // Verify basic structure exists
            loaded.ModId.Should().NotBeNull();
            loaded.ModName.Should().NotBeNull();
            loaded.AreaList.Should().NotBeNull();
        }

        [Fact(Timeout = 120000)]
        public void TestIfoMultipleAreas()
        {
            // Test IFO with multiple areas
            var ifo = new IFO();
            ifo.AreaList.Add(ResRef.FromString("area1"));
            ifo.AreaList.Add(ResRef.FromString("area2"));
            ifo.AreaList.Add(ResRef.FromString("area3"));

            // Write and read back
            GFF gff = IFOHelpers.DismantleIfo(ifo);
            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loadedGff = new GFFBinaryReader(data).Load();
            IFO loaded = IFOHelpers.ConstructIfo(loadedGff);

            loaded.AreaList.Count.Should().Be(3);
            loaded.AreaList[0].ToString().Should().Be("area1");
            loaded.AreaList[1].ToString().Should().Be("area2");
            loaded.AreaList[2].ToString().Should().Be("area3");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoAllScriptHooks()
        {
            // Test setting all script hooks
            var ifo = new IFO();
            ifo.OnClientEnter = ResRef.FromString("on_enter");
            ifo.OnClientLeave = ResRef.FromString("on_leave");
            ifo.OnHeartbeat = ResRef.FromString("on_heartbeat");
            ifo.OnUserDefined = ResRef.FromString("on_user");
            ifo.OnActivateItem = ResRef.FromString("on_activate");
            ifo.OnAcquireItem = ResRef.FromString("on_acquire");
            ifo.OnUnacquireItem = ResRef.FromString("on_unacquire");
            ifo.OnPlayerDeath = ResRef.FromString("on_death");
            ifo.OnPlayerDying = ResRef.FromString("on_dying");
            ifo.OnPlayerRespawn = ResRef.FromString("on_respawn");
            ifo.OnPlayerRest = ResRef.FromString("on_rest");
            ifo.OnPlayerLevelUp = ResRef.FromString("on_levelup");
            ifo.OnLoad = ResRef.FromString("on_load");
            ifo.OnStart = ResRef.FromString("on_start");
            ifo.StartMovie = ResRef.FromString("start_movie");

            // Write and read back
            GFF gff = IFOHelpers.DismantleIfo(ifo);
            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loadedGff = new GFFBinaryReader(data).Load();
            IFO loaded = IFOHelpers.ConstructIfo(loadedGff);

            // Verify all script hooks match
            loaded.OnClientEnter.ToString().Should().Be("on_enter");
            loaded.OnClientLeave.ToString().Should().Be("on_leave");
            loaded.OnHeartbeat.ToString().Should().Be("on_heartbeat");
            loaded.OnUserDefined.ToString().Should().Be("on_user");
            loaded.OnActivateItem.ToString().Should().Be("on_activate");
            loaded.OnAcquireItem.ToString().Should().Be("on_acquire");
            loaded.OnUnacquireItem.ToString().Should().Be("on_unacquire");
            loaded.OnPlayerDeath.ToString().Should().Be("on_death");
            loaded.OnPlayerDying.ToString().Should().Be("on_dying");
            loaded.OnPlayerRespawn.ToString().Should().Be("on_respawn");
            loaded.OnPlayerRest.ToString().Should().Be("on_rest");
            loaded.OnPlayerLevelUp.ToString().Should().Be("on_levelup");
            loaded.OnLoad.ToString().Should().Be("on_load");
            loaded.OnStart.ToString().Should().Be("on_start");
            loaded.StartMovie.ToString().Should().Be("start_movie");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoDataIntegrity()
        {
            // Test that data written and read back maintains integrity
            var ifo = new IFO();
            ifo.ModId = new byte[16];
            System.Random.Shared.NextBytes(ifo.ModId);
            ifo.Tag = "INTEGRITY_TEST";
            ifo.VoId = "test_vo";
            ifo.Hak = "test_hak";
            ifo.ResRef = ResRef.FromString("test_entry");
            ifo.EntryX = 123.456f;
            ifo.EntryY = 789.012f;
            ifo.EntryZ = 345.678f;
            ifo.EntryDirectionX = 0.707f;
            ifo.EntryDirectionY = 0.707f;
            ifo.ExpansionPack = 3;
            ifo.ModVersion = 42;
            ifo.DawnHour = 5;
            ifo.DuskHour = 19;
            ifo.TimeScale = 60;
            ifo.StartMonth = 6;
            ifo.StartDay = 15;
            ifo.StartHour = 12;
            ifo.StartYear = 3956;
            ifo.XpScale = 150;

            // Write and read back
            GFF gff = IFOHelpers.DismantleIfo(ifo);
            byte[] data = new GFFBinaryWriter(gff).Write();
            GFF loadedGff = new GFFBinaryReader(data).Load();
            IFO loaded = IFOHelpers.ConstructIfo(loadedGff);

            // Verify all values match
            loaded.ModId.Should().BeEquivalentTo(ifo.ModId);
            loaded.Tag.Should().Be(ifo.Tag);
            loaded.VoId.Should().Be(ifo.VoId);
            loaded.Hak.Should().Be(ifo.Hak);
            loaded.ResRef.ToString().Should().Be(ifo.ResRef.ToString());
            loaded.EntryX.Should().BeApproximately(ifo.EntryX, 0.0001f);
            loaded.EntryY.Should().BeApproximately(ifo.EntryY, 0.0001f);
            loaded.EntryZ.Should().BeApproximately(ifo.EntryZ, 0.0001f);
            loaded.EntryDirectionX.Should().BeApproximately(ifo.EntryDirectionX, 0.0001f);
            loaded.EntryDirectionY.Should().BeApproximately(ifo.EntryDirectionY, 0.0001f);
            loaded.ExpansionPack.Should().Be(ifo.ExpansionPack);
            loaded.ModVersion.Should().Be(ifo.ModVersion);
            loaded.DawnHour.Should().Be(ifo.DawnHour);
            loaded.DuskHour.Should().Be(ifo.DuskHour);
            loaded.TimeScale.Should().Be(ifo.TimeScale);
            loaded.StartMonth.Should().Be(ifo.StartMonth);
            loaded.StartDay.Should().Be(ifo.StartDay);
            loaded.StartHour.Should().Be(ifo.StartHour);
            loaded.StartYear.Should().Be(ifo.StartYear);
            loaded.XpScale.Should().Be(ifo.XpScale);
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
        public void TestIfoInvalidSignature()
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
        public void TestIfoInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("IFO ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V4.0").CopyTo(header, 4);
                    // Fill rest with zeros for minimal valid GFF structure
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new GFFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*unsupported*");
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
        public void TestIfoGffStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate GFF structure
            gff.Should().NotBeNull("GFF should not be null");
            gff.Content.Should().Be(GFFContent.IFO, "GFF content should be IFO");
            gff.Root.Should().NotBeNull("GFF root should not be null");

            // Validate that root struct exists
            gff.Root.StructId.Should().Be(-1, "Root struct should have struct_id = -1");
        }

        [Fact(Timeout = 120000)]
        public void TestIfoFieldTypes()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate that IFO fields can be accessed
            // Mod_ID should be Binary type
            if (gff.Root.Exists("Mod_ID"))
            {
                var modId = gff.Root.GetBinary("Mod_ID");
                modId.Should().NotBeNull("Mod_ID should not be null");
                modId.Length.Should().BeGreaterThanOrEqualTo(16, "Mod_ID should be at least 16 bytes");
            }

            // Mod_Tag should be String type
            if (gff.Root.Exists("Mod_Tag"))
            {
                var tag = gff.Root.GetString("Mod_Tag");
                tag.Should().NotBeNull("Mod_Tag should not be null");
            }

            // Mod_Entry_Area should be ResRef type
            if (gff.Root.Exists("Mod_Entry_Area"))
            {
                var entryArea = gff.Root.GetResRef("Mod_Entry_Area");
                entryArea.Should().NotBeNull("Mod_Entry_Area should not be null");
            }

            // Mod_Entry_X/Y/Z should be Single (Float) type
            if (gff.Root.Exists("Mod_Entry_X"))
            {
                var entryX = gff.Root.GetSingle("Mod_Entry_X");
                float.IsNaN(entryX).Should().BeFalse("Mod_Entry_X should be a valid float");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestIfoListStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestIfoFile(BinaryTestFile);
            }

            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate Mod_Area_list is a List type
            if (gff.Root.Exists("Mod_Area_list"))
            {
                var areaList = gff.Root.GetList("Mod_Area_list");
                areaList.Should().NotBeNull("Mod_Area_list should not be null");

                // Each area entry should have Area_Name field
                foreach (var areaStruct in areaList)
                {
                    areaStruct.Should().NotBeNull("Area struct should not be null");
                    if (areaStruct.Exists("Area_Name"))
                    {
                        var areaName = areaStruct.GetResRef("Area_Name");
                        areaName.Should().NotBeNull("Area_Name should not be null");
                    }
                }
            }
        }

        private static void ValidateIO(IFO ifo)
        {
            // Basic validation
            ifo.Should().NotBeNull("IFO should not be null");

            // Validate module ID
            ifo.ModId.Should().NotBeNull("Mod_ID should not be null");

            // Validate module name
            ifo.ModName.Should().NotBeNull("Mod_Name should not be null");
            ifo.Name.Should().NotBeNull("Name should not be null");

            // Validate entry configuration
            ifo.ResRef.Should().NotBeNull("ResRef should not be null");
            ifo.EntryArea.Should().NotBeNull("EntryArea should not be null");

            // Validate area list
            ifo.AreaList.Should().NotBeNull("AreaList should not be null");
        }

        private static void CreateTestIfoFile(string path)
        {
            var ifo = new IFO();

            // Set basic module information
            ifo.ModId = new byte[16] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
            ifo.ModName = LocalizedString.FromInvalid();
            ifo.Tag = "TEST_MODULE";
            ifo.VoId = "test_vo";
            ifo.Hak = "test_hak";

            // Set entry configuration
            ifo.ResRef = ResRef.FromString("testarea");
            ifo.EntryX = 0.0f;
            ifo.EntryY = 0.0f;
            ifo.EntryZ = 0.0f;
            ifo.EntryDirectionX = 1.0f;
            ifo.EntryDirectionY = 0.0f;

            // Set script hooks
            ifo.OnHeartbeat = ResRef.FromString("test_heartbeat");
            ifo.OnLoad = ResRef.FromString("test_load");
            ifo.OnStart = ResRef.FromString("test_start");

            // Set area list
            ifo.AreaList.Add(ResRef.FromString("testarea"));

            // Set other fields
            ifo.ExpansionPack = 0;
            ifo.ModVersion = 3;
            ifo.DawnHour = 6;
            ifo.DuskHour = 18;
            ifo.TimeScale = 60;
            ifo.StartMonth = 1;
            ifo.StartDay = 1;
            ifo.StartHour = 12;
            ifo.StartYear = 3956;
            ifo.XpScale = 100;

            // Write to file
            GFF gff = IFOHelpers.DismantleIfo(ifo);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var writer = new GFFBinaryWriter(gff);
            byte[] data = writer.Write();
            File.WriteAllBytes(path, data);
        }
    }
}

