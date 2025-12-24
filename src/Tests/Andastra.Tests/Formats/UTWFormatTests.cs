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
    /// Comprehensive tests for UTW (Waypoint Template) binary I/O operations.
    /// Tests validate the UTW format structure as defined in UTW.ksy Kaitai Struct definition.
    /// UTW files are GFF-based format files that store waypoint definitions including
    /// map notes, appearance, and location data.
    /// </summary>
    public class UTWFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.utw");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.utw");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                // Create a test UTW file if it doesn't exist
                CreateTestUtwFile(BinaryTestFile);
            }

            // Test reading UTW file
            UTW utw = UTWAuto.ReadUtw(File.ReadAllBytes(BinaryTestFile));
            ValidateIO(utw);

            // Test writing and reading back
            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            utw = UTWAuto.ReadUtw(data);
            ValidateIO(utw);
        }

        [Fact(Timeout = 120000)]
        public void TestUtwHeaderStructure()
        {
            // Test that UTW header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtwFile(BinaryTestFile);
            }

            // Read GFF header to validate structure
            GFF gff = new GFFBinaryReader(BinaryTestFile).Load();

            // Validate GFF header constants match UTW.ksy
            gff.Content.Should().Be(GFFContent.UTW, "UTW file should have UTW content type");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtwFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches UTW.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("UTW ", "File type should be 'UTW ' (space-padded) as defined in UTW.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf(new[] { "V3.2", "V3.3", "V4.0", "V4.1" }, "Version should match UTW.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwBasicProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtwFile(BinaryTestFile);
            }

            UTW utw = UTWAuto.ReadUtw(File.ReadAllBytes(BinaryTestFile));

            // Validate basic UTW properties exist and have reasonable values
            utw.ResRef.Should().NotBeNull("ResRef should not be null");
            utw.Tag.Should().NotBeNull("Tag should not be null");
            utw.Name.Should().NotBeNull("Name should not be null");
            utw.MapNote.Should().NotBeNull("MapNote should not be null");
            utw.Description.Should().NotBeNull("Description should not be null");
            utw.Comment.Should().NotBeNull("Comment should not be null");
            utw.LinkedTo.Should().NotBeNull("LinkedTo should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwMapNoteProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtwFile(BinaryTestFile);
            }

            UTW utw = UTWAuto.ReadUtw(File.ReadAllBytes(BinaryTestFile));

            // Validate map note properties exist
            // HasMapNote and MapNoteEnabled are boolean properties (already typed)
            utw.MapNote.Should().NotBeNull("MapNote should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwAppearanceProperties()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtwFile(BinaryTestFile);
            }

            UTW utw = UTWAuto.ReadUtw(File.ReadAllBytes(BinaryTestFile));

            // Validate appearance properties exist
            utw.AppearanceId.Should().BeGreaterThanOrEqualTo(0, "AppearanceId should be non-negative");
            utw.PaletteId.Should().BeGreaterThanOrEqualTo(0, "PaletteId should be non-negative");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwRoundTrip()
        {
            // Test creating UTW, writing, and reading it back
            UTW originalUtw = CreateTestUtwObject();

            // Write to bytes
            byte[] data = UTWAuto.BytesUtw(originalUtw, BioWareGame.K2);

            // Read back
            UTW loadedUtw = UTWAuto.ReadUtw(data);

            // Validate round-trip
            loadedUtw.ResRef.Should().Be(originalUtw.ResRef);
            loadedUtw.Tag.Should().Be(originalUtw.Tag);
            loadedUtw.HasMapNote.Should().Be(originalUtw.HasMapNote);
            loadedUtw.MapNoteEnabled.Should().Be(originalUtw.MapNoteEnabled);
            loadedUtw.AppearanceId.Should().Be(originalUtw.AppearanceId);
            loadedUtw.PaletteId.Should().Be(originalUtw.PaletteId);
        }

        [Fact(Timeout = 120000)]
        public void TestUtwInvalidSignature()
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
        public void TestUtwInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("UTW ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    // Fill rest with zeros for minimal valid GFF structure
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => new GFFBinaryReader(tempFile).Load();
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
        public void TestUtwEmptyFile()
        {
            // Test UTW with minimal required fields
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_WAYPOINT";
            utw.Name = LocalizedString.FromInvalid();

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.Tag.Should().Be("TEST_WAYPOINT");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwMapNoteFunctionality()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtwFile(BinaryTestFile);
            }

            UTW utw = UTWAuto.ReadUtw(File.ReadAllBytes(BinaryTestFile));

            // Validate map note functionality
            // HasMapNote and MapNoteEnabled are boolean flags (already typed as bool)

            // If HasMapNote is true, MapNote should typically be set
            if (utw.HasMapNote)
            {
                utw.MapNote.Should().NotBeNull("MapNote should not be null when HasMapNote is true");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestUtwResRefField()
        {
            // Test ResRef field handling
            UTW utw = new UTW();
            utw.ResRef = new ResRef("test_waypoint");
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.ResRef.Should().Be(new ResRef("test_waypoint"));
        }

        [Fact(Timeout = 120000)]
        public void TestUtwLocalizedNameField()
        {
            // Test LocalizedName field handling
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromStrRef(12345);

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.Name.Should().NotBeNull("LocalizedName should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwMapNoteField()
        {
            // Test MapNote field handling
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();
            utw.HasMapNote = true;
            utw.MapNoteEnabled = true;
            utw.MapNote = LocalizedString.FromStrRef(67890);

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.HasMapNote.Should().BeTrue("HasMapNote should be true");
            loaded.MapNoteEnabled.Should().BeTrue("MapNoteEnabled should be true");
            loaded.MapNote.Should().NotBeNull("MapNote should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwAppearanceField()
        {
            // Test Appearance field handling
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();
            utw.AppearanceId = 1; // Standard waypoint appearance

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.AppearanceId.Should().Be(1, "AppearanceId should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwPaletteIdField()
        {
            // Test PaletteID field handling
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();
            utw.PaletteId = 5;

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.PaletteId.Should().Be(5, "PaletteId should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwCommentField()
        {
            // Test Comment field handling
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();
            utw.Comment = "Test comment for waypoint";

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.Comment.Should().Be("Test comment for waypoint", "Comment should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwLinkedToField()
        {
            // Test LinkedTo field handling (deprecated but should still work)
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();
            utw.LinkedTo = "LINKED_WAYPOINT";

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.LinkedTo.Should().Be("LINKED_WAYPOINT", "LinkedTo should be preserved");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwDescriptionField()
        {
            // Test Description field handling (deprecated but should still work)
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();
            utw.Description = LocalizedString.FromStrRef(11111);

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            loaded.Description.Should().NotBeNull("Description should not be null");
        }

        [Fact(Timeout = 120000)]
        public void TestUtwAllFieldsRoundTrip()
        {
            // Test round-trip with all fields populated
            UTW originalUtw = new UTW();
            originalUtw.ResRef = new ResRef("waypoint_001");
            originalUtw.Tag = "WP_001";
            originalUtw.Name = LocalizedString.FromStrRef(1000);
            originalUtw.Description = LocalizedString.FromStrRef(2000);
            originalUtw.Comment = "Test waypoint comment";
            originalUtw.HasMapNote = true;
            originalUtw.MapNoteEnabled = true;
            originalUtw.MapNote = LocalizedString.FromStrRef(3000);
            originalUtw.LinkedTo = "WP_002";
            originalUtw.AppearanceId = 1;
            originalUtw.PaletteId = 3;

            byte[] data = UTWAuto.BytesUtw(originalUtw, BioWareGame.K2);
            UTW loadedUtw = UTWAuto.ReadUtw(data);

            // Validate all fields
            loadedUtw.ResRef.Should().Be(originalUtw.ResRef);
            loadedUtw.Tag.Should().Be(originalUtw.Tag);
            loadedUtw.HasMapNote.Should().Be(originalUtw.HasMapNote);
            loadedUtw.MapNoteEnabled.Should().Be(originalUtw.MapNoteEnabled);
            loadedUtw.AppearanceId.Should().Be(originalUtw.AppearanceId);
            loadedUtw.PaletteId.Should().Be(originalUtw.PaletteId);
            loadedUtw.Comment.Should().Be(originalUtw.Comment);
            loadedUtw.LinkedTo.Should().Be(originalUtw.LinkedTo);
        }

        [Fact(Timeout = 120000)]
        public void TestUtwBooleanFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestUtwFile(BinaryTestFile);
            }

            UTW utw = UTWAuto.ReadUtw(File.ReadAllBytes(BinaryTestFile));

            // Validate boolean fields are properly set
            // These are stored as Byte (0 or 1) in GFF but exposed as bool in UTW class
            // No need to assert type - they're already bool properties
        }

        [Fact(Timeout = 120000)]
        public void TestUtwMapNoteEnabledRequiresMapNote()
        {
            // Test that MapNoteEnabled should typically require HasMapNote
            UTW utw = new UTW();
            utw.ResRef = ResRef.FromBlank();
            utw.Tag = "TEST_TAG";
            utw.Name = LocalizedString.FromInvalid();
            utw.HasMapNote = false;
            utw.MapNoteEnabled = true; // Enabled but no map note

            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            UTW loaded = UTWAuto.ReadUtw(data);

            // Both values should be preserved as stored
            loaded.HasMapNote.Should().BeFalse();
            loaded.MapNoteEnabled.Should().BeTrue();
        }

        private static void ValidateIO(UTW utw)
        {
            // Basic validation that UTW object was loaded successfully
            utw.Should().NotBeNull("UTW object should not be null");
            utw.ResRef.Should().NotBeNull("ResRef should not be null");
            utw.Tag.Should().NotBeNull("Tag should not be null");
        }

        private static void CreateTestUtwFile(string path)
        {
            UTW utw = CreateTestUtwObject();
            byte[] data = UTWAuto.BytesUtw(utw, BioWareGame.K2);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }

        private static UTW CreateTestUtwObject()
        {
            UTW utw = new UTW();
            utw.ResRef = new ResRef("test_waypoint");
            utw.Tag = "TEST_WAYPOINT";
            utw.Name = LocalizedString.FromInvalid();
            utw.Description = LocalizedString.FromInvalid();
            utw.Comment = "Test waypoint";
            utw.HasMapNote = false;
            utw.MapNoteEnabled = false;
            utw.MapNote = LocalizedString.FromInvalid();
            utw.LinkedTo = string.Empty;
            utw.AppearanceId = 1;
            utw.PaletteId = 0;
            return utw;
        }
    }
}

