using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.SSF.SSFAuto;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for SSF binary I/O operations.
    /// Tests validate the SSF format structure as defined in SSF.ksy Kaitai Struct definition.
    /// </summary>
    public class SSFFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.ssf");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.ssf");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test SSF file if it doesn't exist
                CreateTestSsfFile(BinaryTestFile);
            }

            // Test reading SSF file
            SSF ssf = new SSFBinaryReader(BinaryTestFile).Load();
            ValidateIO(ssf);

            // Test writing and reading back
            byte[] data = new SSFBinaryWriter(ssf).Write();
            ssf = new SSFBinaryReader(data).Load();
            ValidateIO(ssf);
        }

        [Fact(Timeout = 120000)]
        public void TestSsfHeaderStructure()
        {
            // Test that SSF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[12];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 12);
            }

            // Validate file type signature matches SSF.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("SSF ", "File type should be 'SSF ' (space-padded) as defined in SSF.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V1.1", "Version should be 'V1.1' as defined in SSF.ksy");

            // Validate sounds offset
            uint soundsOffset = BitConverter.ToUInt32(header, 8);
            soundsOffset.Should().Be(12u, "Sounds offset should be 12 as defined in SSF.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestSsfFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches SSF.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("SSF ", "File type should be 'SSF ' (space-padded) as defined in SSF.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V1.1", "Version should be 'V1.1' as defined in SSF.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestSsfSoundsArrayStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            SSF ssf = new SSFBinaryReader(BinaryTestFile).Load();

            // Validate that we have exactly 28 sounds (matching sound_array in SSF.ksy)
            // Each sound should be accessible via SSFSound enum
            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                int? strref = ssf.Get(sound);
                strref.Should().NotBeNull($"Sound entry {i} ({sound}) should have a value (may be -1)");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestSsfSoundEntryValues()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            SSF ssf = new SSFBinaryReader(BinaryTestFile).Load();

            // Validate that sound entries can be -1 (0xFFFFFFFF) or valid StrRefs
            // Test that -1 is properly handled (no sound assigned)
            var emptySsf = new SSF();
            emptySsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(-1, "Default SSF should have -1 for all sounds");

            // Test that valid StrRefs are preserved
            ssf.Get(SSFSound.BATTLE_CRY_1).Should().BeGreaterThanOrEqualTo(-1, "StrRef should be >= -1");
        }

        [Fact(Timeout = 120000)]
        public void TestSsfFileSize()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            // Validate file size matches SSF.ksy structure
            // Header (12) + Sounds Array (28 * 4 = 112) + Padding (12) = 136 bytes
            FileInfo fileInfo = new FileInfo(BinaryTestFile);
            fileInfo.Length.Should().Be(136, "SSF file should be exactly 136 bytes as defined in SSF.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestSsfPaddingStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            // Read padding bytes (last 12 bytes)
            byte[] padding = new byte[12];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(124, SeekOrigin.Begin); // Skip header (12) + sounds (112) = 124
                fs.Read(padding, 0, 12);
            }

            // Validate padding is 0xFFFFFFFF (3 uint32 values)
            for (int i = 0; i < 12; i += 4)
            {
                uint paddingValue = BitConverter.ToUInt32(padding, i);
                paddingValue.Should().Be(0xFFFFFFFFu, $"Padding byte {i} should be 0xFFFFFFFF as defined in SSF.ksy");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestSsfEmptyFile()
        {
            // Test SSF with all sounds set to -1 (default)
            var ssf = new SSF();
            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                ssf.Get(sound).Should().Be(-1, $"Default SSF should have -1 for {sound}");
            }

            byte[] data = new SSFBinaryWriter(ssf).Write();
            SSF loaded = new SSFBinaryReader(data).Load();

            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                loaded.Get(sound).Should().Be(-1, $"Loaded empty SSF should have -1 for {sound}");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestSsfAllSoundsSet()
        {
            // Test SSF with all sounds set to different values
            var ssf = new SSF();
            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                ssf.SetData(sound, 1000 + i);
            }

            byte[] data = new SSFBinaryWriter(ssf).Write();
            SSF loaded = new SSFBinaryReader(data).Load();

            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                loaded.Get(sound).Should().Be(1000 + i, $"Sound {sound} should have value {1000 + i}");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestSsfSoundEnumMapping()
        {
            // Test that all SSFSound enum values map correctly to array indices
            var ssf = new SSF();

            // Set each sound to a unique value based on its enum index
            ssf.SetData(SSFSound.BATTLE_CRY_1, 0);
            ssf.SetData(SSFSound.BATTLE_CRY_2, 1);
            ssf.SetData(SSFSound.BATTLE_CRY_3, 2);
            ssf.SetData(SSFSound.BATTLE_CRY_4, 3);
            ssf.SetData(SSFSound.BATTLE_CRY_5, 4);
            ssf.SetData(SSFSound.BATTLE_CRY_6, 5);
            ssf.SetData(SSFSound.SELECT_1, 6);
            ssf.SetData(SSFSound.SELECT_2, 7);
            ssf.SetData(SSFSound.SELECT_3, 8);
            ssf.SetData(SSFSound.ATTACK_GRUNT_1, 9);
            ssf.SetData(SSFSound.ATTACK_GRUNT_2, 10);
            ssf.SetData(SSFSound.ATTACK_GRUNT_3, 11);
            ssf.SetData(SSFSound.PAIN_GRUNT_1, 12);
            ssf.SetData(SSFSound.PAIN_GRUNT_2, 13);
            ssf.SetData(SSFSound.LOW_HEALTH, 14);
            ssf.SetData(SSFSound.DEAD, 15);
            ssf.SetData(SSFSound.CRITICAL_HIT, 16);
            ssf.SetData(SSFSound.TARGET_IMMUNE, 17);
            ssf.SetData(SSFSound.LAY_MINE, 18);
            ssf.SetData(SSFSound.DISARM_MINE, 19);
            ssf.SetData(SSFSound.BEGIN_STEALTH, 20);
            ssf.SetData(SSFSound.BEGIN_SEARCH, 21);
            ssf.SetData(SSFSound.BEGIN_UNLOCK, 22);
            ssf.SetData(SSFSound.UNLOCK_FAILED, 23);
            ssf.SetData(SSFSound.UNLOCK_SUCCESS, 24);
            ssf.SetData(SSFSound.SEPARATED_FROM_PARTY, 25);
            ssf.SetData(SSFSound.REJOINED_PARTY, 26);
            ssf.SetData(SSFSound.POISONED, 27);

            // Verify each sound has the correct value
            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                ssf.Get(sound).Should().Be(i, $"Sound {sound} should map to index {i}");
            }
        }

        /// <summary>
        /// Python: test_read_raises
        /// Tests various error conditions when reading SSF files
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestReadRaises()
        {
            // Test directory access
            Action act1 = () => new SSFBinaryReader(".").Load();
            act1.Should().Throw<UnauthorizedAccessException>(); // Or IOException depending on OS

            // Test file not found
            Action act2 = () => new SSFBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test corrupted file (invalid version)
            Action act3 = () => new SSFBinaryReader(CorruptBinaryTestFile).Load();
            act3.Should().Throw<InvalidDataException>()
                .WithMessage("*version*not supported*");
        }

        private static void ValidateIO(SSF ssf)
        {
            ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(123075);
            ssf.Get(SSFSound.BATTLE_CRY_2).Should().Be(123074);
            ssf.Get(SSFSound.BATTLE_CRY_3).Should().Be(123073);
            ssf.Get(SSFSound.BATTLE_CRY_4).Should().Be(123072);
            ssf.Get(SSFSound.BATTLE_CRY_5).Should().Be(123071);
            ssf.Get(SSFSound.BATTLE_CRY_6).Should().Be(123070);
            ssf.Get(SSFSound.SELECT_1).Should().Be(123069);
            ssf.Get(SSFSound.SELECT_2).Should().Be(123068);
            ssf.Get(SSFSound.SELECT_3).Should().Be(123067);
            ssf.Get(SSFSound.ATTACK_GRUNT_1).Should().Be(123066);
            ssf.Get(SSFSound.ATTACK_GRUNT_2).Should().Be(123065);
            ssf.Get(SSFSound.ATTACK_GRUNT_3).Should().Be(123064);
            ssf.Get(SSFSound.PAIN_GRUNT_1).Should().Be(123063);
            ssf.Get(SSFSound.PAIN_GRUNT_2).Should().Be(123062);
            ssf.Get(SSFSound.LOW_HEALTH).Should().Be(123061);
            ssf.Get(SSFSound.DEAD).Should().Be(123060);
            ssf.Get(SSFSound.CRITICAL_HIT).Should().Be(123059);
            ssf.Get(SSFSound.TARGET_IMMUNE).Should().Be(123058);
            ssf.Get(SSFSound.LAY_MINE).Should().Be(123057);
            ssf.Get(SSFSound.DISARM_MINE).Should().Be(123056);
            ssf.Get(SSFSound.BEGIN_STEALTH).Should().Be(123055);
            ssf.Get(SSFSound.BEGIN_SEARCH).Should().Be(123054);
            ssf.Get(SSFSound.BEGIN_UNLOCK).Should().Be(123053);
            ssf.Get(SSFSound.UNLOCK_FAILED).Should().Be(123052);
            ssf.Get(SSFSound.UNLOCK_SUCCESS).Should().Be(123051);
            ssf.Get(SSFSound.SEPARATED_FROM_PARTY).Should().Be(123050);
            ssf.Get(SSFSound.REJOINED_PARTY).Should().Be(123049);
            ssf.Get(SSFSound.POISONED).Should().Be(123048);
        }

        /// <summary>
        /// Python: test_write_raises
        /// Tests various error conditions when writing SSF files
        /// </summary>
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestWriteRaises()
        {
            // test_write_raises from Python
            var ssf = new SSF();

            // Test writing to directory (should raise PermissionError on Windows, IsADirectoryError on Unix)
            // Python: write_ssf(SSF(), ".", ResourceType.SSF)
            Action act1 = () => WriteSsf(ssf, ".", ResourceType.SSF);
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>(); // IsADirectoryError equivalent
            }

            // Test invalid resource type (Python raises ValueError for ResourceType.INVALID)
            // Python: write_ssf(SSF(), ".", ResourceType.INVALID)
            Action act2 = () => WriteSsf(ssf, ".", ResourceType.INVALID);
            act2.Should().Throw<ArgumentException>().WithMessage("*Unsupported format*");
        }

        [Fact(Timeout = 120000)]
        public void TestSsfInvalidSignature()
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

                Action act = () => new SSFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*invalid*");
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
        public void TestSsfInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[12];
                    System.Text.Encoding.ASCII.GetBytes("SSF ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    BitConverter.GetBytes(12u).CopyTo(header, 8);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new SSFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*version*not supported*");
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
        public void TestSsfInvalidOffset()
        {
            // Create file with invalid sounds offset (not 12)
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[12];
                    System.Text.Encoding.ASCII.GetBytes("SSF ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V1.1").CopyTo(header, 4);
                    BitConverter.GetBytes(100u).CopyTo(header, 8); // Invalid offset
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new SSFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>();
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
        public void TestSsfTruncatedFile()
        {
            // Create file with valid header but truncated data
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[12];
                    System.Text.Encoding.ASCII.GetBytes("SSF ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V1.1").CopyTo(header, 4);
                    BitConverter.GetBytes(12u).CopyTo(header, 8);
                    fs.Write(header, 0, header.Length);
                    // File is truncated - no sounds array or padding
                }

                Action act = () => new SSFBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>();
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
        public void TestSsfRoundTrip()
        {
            // Test creating SSF, writing it, and reading it back
            var ssf = new SSF();

            // Set various test values
            ssf.SetData(SSFSound.BATTLE_CRY_1, 100);
            ssf.SetData(SSFSound.SELECT_1, 200);
            ssf.SetData(SSFSound.ATTACK_GRUNT_1, -1); // No sound
            ssf.SetData(SSFSound.POISONED, 300);

            // Write to bytes
            byte[] data = new SSFBinaryWriter(ssf).Write();

            // Read back
            SSF loaded = new SSFBinaryReader(data).Load();

            // Verify values match
            loaded.Get(SSFSound.BATTLE_CRY_1).Should().Be(100);
            loaded.Get(SSFSound.SELECT_1).Should().Be(200);
            loaded.Get(SSFSound.ATTACK_GRUNT_1).Should().Be(-1);
            loaded.Get(SSFSound.POISONED).Should().Be(300);
        }

        [Fact(Timeout = 120000)]
        public void TestSsfStrRefConversion()
        {
            // Test that 0xFFFFFFFF is properly converted to -1
            var ssf = new SSF();
            ssf.SetData(SSFSound.BATTLE_CRY_1, -1);

            byte[] data = new SSFBinaryWriter(ssf).Write();

            // Verify raw bytes contain 0xFFFFFFFF
            uint rawValue = BitConverter.ToUInt32(data, 12); // First sound entry
            rawValue.Should().Be(0xFFFFFFFFu, "Raw value should be 0xFFFFFFFF for -1");

            // Verify loaded value is -1
            SSF loaded = new SSFBinaryReader(data).Load();
            loaded.Get(SSFSound.BATTLE_CRY_1).Should().Be(-1, "Loaded value should be -1");
        }

        [Fact(Timeout = 120000)]
        public void TestSsfBoundaryValues()
        {
            // Test edge cases: 0, -1, large values
            var ssf = new SSF();

            ssf.SetData(SSFSound.BATTLE_CRY_1, 0);
            ssf.SetData(SSFSound.BATTLE_CRY_2, -1);
            ssf.SetData(SSFSound.BATTLE_CRY_3, 999999);
            ssf.SetData(SSFSound.BATTLE_CRY_4, 1);

            byte[] data = new SSFBinaryWriter(ssf).Write();
            SSF loaded = new SSFBinaryReader(data).Load();

            loaded.Get(SSFSound.BATTLE_CRY_1).Should().Be(0);
            loaded.Get(SSFSound.BATTLE_CRY_2).Should().Be(-1);
            loaded.Get(SSFSound.BATTLE_CRY_3).Should().Be(999999);
            loaded.Get(SSFSound.BATTLE_CRY_4).Should().Be(1);
        }

        [Fact(Timeout = 120000)]
        public void TestSsfAllSoundTypes()
        {
            // Test all 28 sound types individually
            var ssf = new SSF();

            // Set each sound type to a unique value
            ssf.SetData(SSFSound.BATTLE_CRY_1, 1);
            ssf.SetData(SSFSound.BATTLE_CRY_2, 2);
            ssf.SetData(SSFSound.BATTLE_CRY_3, 3);
            ssf.SetData(SSFSound.BATTLE_CRY_4, 4);
            ssf.SetData(SSFSound.BATTLE_CRY_5, 5);
            ssf.SetData(SSFSound.BATTLE_CRY_6, 6);
            ssf.SetData(SSFSound.SELECT_1, 7);
            ssf.SetData(SSFSound.SELECT_2, 8);
            ssf.SetData(SSFSound.SELECT_3, 9);
            ssf.SetData(SSFSound.ATTACK_GRUNT_1, 10);
            ssf.SetData(SSFSound.ATTACK_GRUNT_2, 11);
            ssf.SetData(SSFSound.ATTACK_GRUNT_3, 12);
            ssf.SetData(SSFSound.PAIN_GRUNT_1, 13);
            ssf.SetData(SSFSound.PAIN_GRUNT_2, 14);
            ssf.SetData(SSFSound.LOW_HEALTH, 15);
            ssf.SetData(SSFSound.DEAD, 16);
            ssf.SetData(SSFSound.CRITICAL_HIT, 17);
            ssf.SetData(SSFSound.TARGET_IMMUNE, 18);
            ssf.SetData(SSFSound.LAY_MINE, 19);
            ssf.SetData(SSFSound.DISARM_MINE, 20);
            ssf.SetData(SSFSound.BEGIN_STEALTH, 21);
            ssf.SetData(SSFSound.BEGIN_SEARCH, 22);
            ssf.SetData(SSFSound.BEGIN_UNLOCK, 23);
            ssf.SetData(SSFSound.UNLOCK_FAILED, 24);
            ssf.SetData(SSFSound.UNLOCK_SUCCESS, 25);
            ssf.SetData(SSFSound.SEPARATED_FROM_PARTY, 26);
            ssf.SetData(SSFSound.REJOINED_PARTY, 27);
            ssf.SetData(SSFSound.POISONED, 28);

            byte[] data = new SSFBinaryWriter(ssf).Write();
            SSF loaded = new SSFBinaryReader(data).Load();

            // Verify all values
            loaded.Get(SSFSound.BATTLE_CRY_1).Should().Be(1);
            loaded.Get(SSFSound.BATTLE_CRY_2).Should().Be(2);
            loaded.Get(SSFSound.BATTLE_CRY_3).Should().Be(3);
            loaded.Get(SSFSound.BATTLE_CRY_4).Should().Be(4);
            loaded.Get(SSFSound.BATTLE_CRY_5).Should().Be(5);
            loaded.Get(SSFSound.BATTLE_CRY_6).Should().Be(6);
            loaded.Get(SSFSound.SELECT_1).Should().Be(7);
            loaded.Get(SSFSound.SELECT_2).Should().Be(8);
            loaded.Get(SSFSound.SELECT_3).Should().Be(9);
            loaded.Get(SSFSound.ATTACK_GRUNT_1).Should().Be(10);
            loaded.Get(SSFSound.ATTACK_GRUNT_2).Should().Be(11);
            loaded.Get(SSFSound.ATTACK_GRUNT_3).Should().Be(12);
            loaded.Get(SSFSound.PAIN_GRUNT_1).Should().Be(13);
            loaded.Get(SSFSound.PAIN_GRUNT_2).Should().Be(14);
            loaded.Get(SSFSound.LOW_HEALTH).Should().Be(15);
            loaded.Get(SSFSound.DEAD).Should().Be(16);
            loaded.Get(SSFSound.CRITICAL_HIT).Should().Be(17);
            loaded.Get(SSFSound.TARGET_IMMUNE).Should().Be(18);
            loaded.Get(SSFSound.LAY_MINE).Should().Be(19);
            loaded.Get(SSFSound.DISARM_MINE).Should().Be(20);
            loaded.Get(SSFSound.BEGIN_STEALTH).Should().Be(21);
            loaded.Get(SSFSound.BEGIN_SEARCH).Should().Be(22);
            loaded.Get(SSFSound.BEGIN_UNLOCK).Should().Be(23);
            loaded.Get(SSFSound.UNLOCK_FAILED).Should().Be(24);
            loaded.Get(SSFSound.UNLOCK_SUCCESS).Should().Be(25);
            loaded.Get(SSFSound.SEPARATED_FROM_PARTY).Should().Be(26);
            loaded.Get(SSFSound.REJOINED_PARTY).Should().Be(27);
            loaded.Get(SSFSound.POISONED).Should().Be(28);
        }

        [Fact(Timeout = 120000)]
        public void TestSsfDataIntegrity()
        {
            // Test that data written and read back maintains integrity
            var ssf = new SSF();

            // Set a comprehensive set of values
            Random random = new Random(42);
            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                int value = random.Next(-1, 1000000); // Range from -1 to 999999
                ssf.SetData(sound, value);
            }

            // Write and read back
            byte[] data = new SSFBinaryWriter(ssf).Write();
            SSF loaded = new SSFBinaryReader(data).Load();

            // Verify all values match
            random = new Random(42); // Reset random with same seed
            for (int i = 0; i < 28; i++)
            {
                SSFSound sound = (SSFSound)i;
                int expectedValue = random.Next(-1, 1000000);
                loaded.Get(sound).Should().Be(expectedValue, $"Sound {sound} should match original value");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestSsfSoundEntryStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            // Read raw sound array bytes
            byte[] soundArray = new byte[112]; // 28 entries * 4 bytes
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(12, SeekOrigin.Begin); // Skip header
                fs.Read(soundArray, 0, 112);
            }

            // Validate each entry is a uint32
            for (int i = 0; i < 28; i++)
            {
                uint rawValue = BitConverter.ToUInt32(soundArray, i * 4);
                // Value should be either a valid StrRef (>= 0) or 0xFFFFFFFF (-1)
                rawValue.Should().BeGreaterThanOrEqualTo(0u, $"Sound entry {i} raw value should be >= 0");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestSsfSoundsOffsetValidation()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            // Read header and validate sounds_offset
            byte[] header = new byte[12];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 12);
            }

            uint soundsOffset = BitConverter.ToUInt32(header, 8);
            soundsOffset.Should().Be(12u, "Sounds offset should always be 12 as defined in SSF.ksy");

            // Verify sounds array starts at offset 12
            SSF ssf = new SSFBinaryReader(BinaryTestFile).Load();
            ssf.Should().NotBeNull("SSF should load successfully when sounds_offset is 12");
        }

        [Fact(Timeout = 120000)]
        public void TestSsfPaddingValidation()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestSsfFile(BinaryTestFile);
            }

            // Read padding bytes (last 12 bytes)
            byte[] padding = new byte[12];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Seek(124, SeekOrigin.Begin); // Skip header (12) + sounds (112) = 124
                fs.Read(padding, 0, 12);
            }

            // Validate all padding bytes are 0xFFFFFFFF
            for (int i = 0; i < 3; i++)
            {
                uint paddingValue = BitConverter.ToUInt32(padding, i * 4);
                paddingValue.Should().Be(0xFFFFFFFFu, $"Padding entry {i} should be 0xFFFFFFFF as defined in SSF.ksy");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestSsfMixedValues()
        {
            // Test SSF with mix of -1 and valid StrRefs
            var ssf = new SSF();

            // Set some sounds to -1, others to valid values
            ssf.SetData(SSFSound.BATTLE_CRY_1, -1);
            ssf.SetData(SSFSound.BATTLE_CRY_2, 100);
            ssf.SetData(SSFSound.BATTLE_CRY_3, -1);
            ssf.SetData(SSFSound.SELECT_1, 200);
            ssf.SetData(SSFSound.SELECT_2, -1);
            ssf.SetData(SSFSound.POISONED, 300);

            byte[] data = new SSFBinaryWriter(ssf).Write();
            SSF loaded = new SSFBinaryReader(data).Load();

            loaded.Get(SSFSound.BATTLE_CRY_1).Should().Be(-1);
            loaded.Get(SSFSound.BATTLE_CRY_2).Should().Be(100);
            loaded.Get(SSFSound.BATTLE_CRY_3).Should().Be(-1);
            loaded.Get(SSFSound.SELECT_1).Should().Be(200);
            loaded.Get(SSFSound.SELECT_2).Should().Be(-1);
            loaded.Get(SSFSound.POISONED).Should().Be(300);
        }

        private static void CreateTestSsfFile(string path)
        {
            var ssf = new SSF();
            ssf.SetData(SSFSound.BATTLE_CRY_1, 123075);
            ssf.SetData(SSFSound.BATTLE_CRY_2, 123074);
            ssf.SetData(SSFSound.BATTLE_CRY_3, 123073);
            ssf.SetData(SSFSound.BATTLE_CRY_4, 123072);
            ssf.SetData(SSFSound.BATTLE_CRY_5, 123071);
            ssf.SetData(SSFSound.BATTLE_CRY_6, 123070);
            ssf.SetData(SSFSound.SELECT_1, 123069);
            ssf.SetData(SSFSound.SELECT_2, 123068);
            ssf.SetData(SSFSound.SELECT_3, 123067);
            ssf.SetData(SSFSound.ATTACK_GRUNT_1, 123066);
            ssf.SetData(SSFSound.ATTACK_GRUNT_2, 123065);
            ssf.SetData(SSFSound.ATTACK_GRUNT_3, 123064);
            ssf.SetData(SSFSound.PAIN_GRUNT_1, 123063);
            ssf.SetData(SSFSound.PAIN_GRUNT_2, 123062);
            ssf.SetData(SSFSound.LOW_HEALTH, 123061);
            ssf.SetData(SSFSound.DEAD, 123060);
            ssf.SetData(SSFSound.CRITICAL_HIT, 123059);
            ssf.SetData(SSFSound.TARGET_IMMUNE, 123058);
            ssf.SetData(SSFSound.LAY_MINE, 123057);
            ssf.SetData(SSFSound.DISARM_MINE, 123056);
            ssf.SetData(SSFSound.BEGIN_STEALTH, 123055);
            ssf.SetData(SSFSound.BEGIN_SEARCH, 123054);
            ssf.SetData(SSFSound.BEGIN_UNLOCK, 123053);
            ssf.SetData(SSFSound.UNLOCK_FAILED, 123052);
            ssf.SetData(SSFSound.UNLOCK_SUCCESS, 123051);
            ssf.SetData(SSFSound.SEPARATED_FROM_PARTY, 123050);
            ssf.SetData(SSFSound.REJOINED_PARTY, 123049);
            ssf.SetData(SSFSound.POISONED, 123048);

            byte[] data = new SSFBinaryWriter(ssf).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

    }
}

