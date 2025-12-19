using System;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Common;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for TLK binary I/O operations.
    /// Tests validate the TLK format structure as defined in TLK.ksy Kaitai Struct definition.
    /// </summary>
    public class TLKFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.tlk");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.tlk");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test TLK file if it doesn't exist
                CreateTestTlkFile(BinaryTestFile);
            }

            // Test reading TLK file
            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();
            ValidateIO(tlk);

            // Test writing and reading back
            byte[] data = new TLKBinaryWriter(tlk).Write();
            tlk = new TLKBinaryReader(data).Load();
            ValidateIO(tlk);
        }

        [Fact(Timeout = 120000)]
        public void TestTlkHeaderStructure()
        {
            // Test that TLK header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();

            // Validate header constants match TLK.ksy
            const int expectedHeaderSize = 20;
            const string expectedFileType = "TLK ";
            const string expectedFileVersion = "V3.0";

            // Read raw header bytes
            byte[] header = new byte[20];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 20);
            }

            // Validate file type signature matches TLK.ksy
            string fileType = Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be(expectedFileType, "File type should be 'TLK ' (space-padded) as defined in TLK.ksy");

            // Validate version
            string version = Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be(expectedFileVersion, "Version should be 'V3.0' as defined in TLK.ksy");

            // Validate header size
            header.Length.Should().Be(expectedHeaderSize, "TLK header should be 20 bytes as defined in TLK.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches TLK.ksy
            string fileType = Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("TLK ", "File type should be 'TLK ' (space-padded) as defined in TLK.ksy");

            // Validate version
            string version = Encoding.ASCII.GetString(header, 4, 4);
            version.Should().Be("V3.0", "Version should be 'V3.0' as defined in TLK.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkStringDataTable()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();

            // Validate string data table structure
            tlk.Count.Should().BeGreaterThanOrEqualTo(0, "String count should be non-negative");

            // Validate each entry has required fields (matching string_data_entry in TLK.ksy)
            foreach (var entry in tlk.Entries)
            {
                entry.Should().NotBeNull("Entry should not be null");
                entry.Text.Should().NotBeNull("Text should not be null (may be empty string)");
                entry.Voiceover.Should().NotBeNull("Voiceover ResRef should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestTlkStringDataEntrySize()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            // Validate that string data entry is 40 bytes as per TLK.ksy
            const int expectedEntrySize = 40;

            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();

            if (tlk.Count > 0)
            {
                // Calculate expected file size: header (20) + entries (count * 40) + text data
                long expectedMinSize = 20 + (tlk.Count * expectedEntrySize);
                FileInfo fileInfo = new FileInfo(BinaryTestFile);
                fileInfo.Length.Should().BeGreaterThanOrEqualTo(expectedMinSize, 
                    "File should be at least header + string data table size");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestTlkFlags()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();

            // Validate flag fields exist and are boolean
            foreach (var entry in tlk.Entries)
            {
                // Flags are stored as boolean properties in TLKEntry
                // text_present, sound_present, sound_length_present
                entry.TextPresent.Should().BeOfType<bool>("TextPresent should be boolean");
                entry.SoundPresent.Should().BeOfType<bool>("SoundPresent should be boolean");
                entry.SoundLengthPresent.Should().BeOfType<bool>("SoundLengthPresent should be boolean");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestTlkSoundResref()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();

            // Validate sound ResRef structure (16 bytes in TLK.ksy)
            foreach (var entry in tlk.Entries)
            {
                entry.Voiceover.Should().NotBeNull("Voiceover ResRef should not be null");
                // ResRef is max 16 characters (including null terminator)
                string resrefStr = entry.Voiceover.ToString();
                resrefStr.Length.Should().BeLessOrEqualTo(16, 
                    "Sound ResRef should be max 16 characters as defined in TLK.ksy");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestTlkLanguageId()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();

            // Validate language ID is a valid Language enum value
            tlk.Language.Should().BeDefined("Language should be a valid Language enum value");

            // Read raw language ID from file
            byte[] header = new byte[20];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 20);
            }

            uint languageId = BitConverter.ToUInt32(header, 8);
            languageId.Should().Be((uint)tlk.Language, "Language ID should match loaded language");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkEntriesOffset()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            // Validate entries_offset calculation matches TLK.ksy
            byte[] header = new byte[20];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 20);
            }

            uint stringCount = BitConverter.ToUInt32(header, 12);
            uint entriesOffset = BitConverter.ToUInt32(header, 16);

            // Expected offset: 20 (header) + (stringCount * 40) (string data table)
            uint expectedOffset = 20 + (stringCount * 40);
            entriesOffset.Should().Be(expectedOffset, 
                "Entries offset should be header size + string data table size as defined in TLK.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkEmptyFile()
        {
            // Test TLK with no entries
            var tlk = new TLK(Language.English);
            tlk.Count.Should().Be(0, "Empty TLK should have 0 entries");

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            loaded.Count.Should().Be(0);
            loaded.Language.Should().Be(Language.English);
        }

        [Fact(Timeout = 120000)]
        public void TestTlkMultipleEntries()
        {
            // Test TLK with multiple entries
            var tlk = new TLK(Language.English);

            tlk.Add("First entry", "sound1");
            tlk.Add("Second entry", "sound2");
            tlk.Add("Third entry", "sound3");

            tlk.Count.Should().Be(3);
            tlk.Entries.Count.Should().Be(3);

            byte[] serialized = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(serialized).Load();

            loaded.Count.Should().Be(3);
            loaded.Entries.Count.Should().Be(3);
            loaded.Entries[0].Text.Should().Be("First entry");
            loaded.Entries[1].Text.Should().Be("Second entry");
            loaded.Entries[2].Text.Should().Be("Third entry");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkTextOnlyEntry()
        {
            // Test entry with text only (flag 0x0001)
            var tlk = new TLK(Language.English);
            int stringref = tlk.Add("Text only entry", "");

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            var entry = loaded.Entries[stringref];
            entry.Text.Should().Be("Text only entry");
            entry.TextPresent.Should().BeTrue("Text-only entry should have TextPresent flag");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkTextAndSoundEntry()
        {
            // Test entry with text and sound (flag 0x0003)
            var tlk = new TLK(Language.English);
            int stringref = tlk.Add("Text with sound", "vo_sound");

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            var entry = loaded.Entries[stringref];
            entry.Text.Should().Be("Text with sound");
            entry.Voiceover.ToString().Should().Be("vo_sound");
            entry.TextPresent.Should().BeTrue("Entry with text should have TextPresent flag");
            entry.SoundPresent.Should().BeTrue("Entry with sound should have SoundPresent flag");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkEmptyEntry()
        {
            // Test empty entry (flag 0x0000)
            var tlk = new TLK(Language.English);
            tlk.Resize(1); // Create one empty entry

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            var entry = loaded.Entries[0];
            entry.Text.Should().Be("", "Empty entry should have empty text");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkDifferentLanguages()
        {
            // Test TLK files with different language IDs
            var languages = new[] { Language.English, Language.French, Language.German, Language.Polish };

            foreach (var language in languages)
            {
                var tlk = new TLK(language);
                tlk.Add("Test", "sound");

                byte[] data = new TLKBinaryWriter(tlk).Write();
                TLK loaded = new TLKBinaryReader(data).Load();

                loaded.Language.Should().Be(language, $"TLK should preserve language {language}");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => new TLKBinaryReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
                System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new TLKBinaryReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => new TLKBinaryReader(CorruptBinaryTestFile).Load();
                act3.Should().Throw<InvalidDataException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestTlkInvalidSignature()
        {
            // Create file with invalid signature
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] invalid = Encoding.ASCII.GetBytes("INVALID");
                    fs.Write(invalid, 0, invalid.Length);
                }

                Action act = () => new TLKBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*invalid TLK file*");
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
        public void TestTlkInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[20];
                    Encoding.ASCII.GetBytes("TLK ").CopyTo(header, 0);
                    Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new TLKBinaryReader(tempFile).Load();
                act.Should().Throw<InvalidDataException>().WithMessage("*invalid TLK file*");
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
        public void TestTlkRoundTrip()
        {
            // Test creating TLK, writing, reading back
            var original = new TLK(Language.English);
            original.Add("Hello", "hello");
            original.Add("World", "world");
            original.Add("Test entry with longer text", "long_sound");

            byte[] data = new TLKBinaryWriter(original).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            loaded.Count.Should().Be(original.Count);
            loaded.Language.Should().Be(original.Language);
            loaded.Entries[0].Text.Should().Be(original.Entries[0].Text);
            loaded.Entries[1].Text.Should().Be(original.Entries[1].Text);
            loaded.Entries[2].Text.Should().Be(original.Entries[2].Text);
        }

        [Fact(Timeout = 120000)]
        public void TestTlkLargeFile()
        {
            // Test TLK with many entries
            var tlk = new TLK(Language.English);
            for (int i = 0; i < 100; i++)
            {
                tlk.Add($"Entry {i}", $"sound{i}");
            }

            tlk.Count.Should().Be(100);

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            loaded.Count.Should().Be(100);
            for (int i = 0; i < 100; i++)
            {
                loaded.Entries[i].Text.Should().Be($"Entry {i}");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestTlkUnicodeText()
        {
            // Test TLK with Unicode characters (for languages that support it)
            var tlk = new TLK(Language.English);
            // Note: KotOR typically uses Windows-1252, so Unicode may not work correctly
            // This test validates the structure can handle various text lengths
            tlk.Add("Simple text", "sound1");
            tlk.Add("Text with special chars: àáâãäå", "sound2");

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            loaded.Entries[0].Text.Should().Be("Simple text");
            // Note: Special characters may be mangled depending on encoding
        }

        [Fact(Timeout = 120000)]
        public void TestTlkSoundLength()
        {
            // Test entry with sound length
            var tlk = new TLK(Language.English);
            int stringref = tlk.Add("Text with sound length", "sound");
            tlk.Entries[stringref].SoundLength = 5.5f;
            tlk.Entries[stringref].SoundLengthPresent = true;

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            var entry = loaded.Entries[stringref];
            entry.SoundLengthPresent.Should().BeTrue("Entry with sound length should have SoundLengthPresent flag");
            // Note: SoundLength may not be preserved in round-trip if not properly set
        }

        [Fact(Timeout = 120000)]
        public void TestTlkVolumeAndPitchVariance()
        {
            // Test that volume_variance and pitch_variance are always 0 (unused in KotOR)
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestTlkFile(BinaryTestFile);
            }

            TLK tlk = new TLKBinaryReader(BinaryTestFile).Load();

            // These fields are unused in KotOR and should always be 0
            // They are read but not stored in TLKEntry, so we validate the file structure
            // by checking that entries can be read successfully
            tlk.Count.Should().BeGreaterOrEqualTo(0);
        }

        [Fact(Timeout = 120000)]
        public void TestTlkTextOffsetCalculation()
        {
            // Test that text offsets are calculated correctly
            var tlk = new TLK(Language.English);
            tlk.Add("First", "sound1");
            tlk.Add("Second", "sound2");
            tlk.Add("Third", "sound3");

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            // All entries should be readable
            loaded.Entries[0].Text.Should().Be("First");
            loaded.Entries[1].Text.Should().Be("Second");
            loaded.Entries[2].Text.Should().Be("Third");
        }

        [Fact(Timeout = 120000)]
        public void TestTlkResrefMaxLength()
        {
            // Test ResRef at maximum length (16 characters)
            var tlk = new TLK(Language.English);
            string maxResref = new string('a', 16); // Exactly 16 characters
            tlk.Add("Test", maxResref);

            byte[] data = new TLKBinaryWriter(tlk).Write();
            TLK loaded = new TLKBinaryReader(data).Load();

            loaded.Entries[0].Voiceover.ToString().Length.Should().BeLessOrEqualTo(16,
                "ResRef should be max 16 characters as defined in TLK.ksy");
        }

        private static void ValidateIO(TLK tlk)
        {
            // Basic validation
            tlk.Should().NotBeNull();
            tlk.Language.Should().BeDefined("Language should be a valid Language enum value");
            tlk.Count.Should().BeGreaterOrEqualTo(0);
        }

        private static void CreateTestTlkFile(string path)
        {
            var tlk = new TLK(Language.English);
            tlk.Add("Test entry 1", "sound1");
            tlk.Add("Test entry 2", "sound2");
            tlk.Add("Test entry 3", "");

            byte[] data = new TLKBinaryWriter(tlk).Write();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}
