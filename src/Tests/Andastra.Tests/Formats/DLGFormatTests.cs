using System;
using System.IO;
using System.Linq;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics.DLG;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for DLG binary I/O operations.
    /// Tests validate the DLG format structure as defined in DLG.ksy Kaitai Struct definition.
    /// DLG files are GFF-based format files with file type signature "DLG ".
    /// </summary>
    public class DLGFormatTests
    {
        private static readonly string BinaryTestFile = TestFileHelper.GetPath("test.dlg");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string CorruptBinaryTestFile = TestFileHelper.GetPath("test_corrupted.dlg");

        [Fact(Timeout = 120000)]
        public void TestBinaryIO()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            // Test reading DLG file
            DLG dlg = DLGHelper.ReadDlg(File.ReadAllBytes(BinaryTestFile));
            ValidateIO(dlg);

            // Test writing and reading back
            byte[] data = DLGHelper.BytesDlg(dlg, BioWareGame.K1);
            dlg = DLGHelper.ReadDlg(data);
            ValidateIO(dlg);
        }

        [Fact(Timeout = 120000)]
        public void TestDlgGffHeaderStructure()
        {
            // Test that DLG GFF header matches Kaitai Struct definition
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            // Read raw GFF header bytes (56 bytes)
            byte[] header = new byte[56];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 56);
            }

            // Validate file type signature matches DLG.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("DLG ", "File type should be 'DLG ' (space-padded) as defined in DLG.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match DLG.ksy valid values");

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
        public void TestDlgFileTypeSignature()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            // Read raw header bytes
            byte[] header = new byte[8];
            using (var fs = File.OpenRead(BinaryTestFile))
            {
                fs.Read(header, 0, 8);
            }

            // Validate file type signature matches DLG.ksy
            string fileType = System.Text.Encoding.ASCII.GetString(header, 0, 4);
            fileType.Should().Be("DLG ", "File type should be 'DLG ' (space-padded) as defined in DLG.ksy");

            // Validate version
            string version = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            version.Should().BeOneOf("V3.2", "V3.3", "V4.0", "V4.1", "Version should match DLG.ksy valid values");
        }

        [Fact(Timeout = 120000)]
        public void TestDlgInvalidSignature()
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

                Action act = () => DLGHelper.ReadDlg(File.ReadAllBytes(tempFile));
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
        public void TestDlgInvalidVersion()
        {
            // Create file with invalid version
            string tempFile = Path.GetTempFileName();
            try
            {
                using (var fs = File.Create(tempFile))
                {
                    byte[] header = new byte[56];
                    System.Text.Encoding.ASCII.GetBytes("DLG ").CopyTo(header, 0);
                    System.Text.Encoding.ASCII.GetBytes("V2.0").CopyTo(header, 4);
                    fs.Write(header, 0, header.Length);
                }

                Action act = () => DLGHelper.ReadDlg(File.ReadAllBytes(tempFile));
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
        public void TestDlgRootStructFields()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            DLG dlg = DLGHelper.ReadDlg(File.ReadAllBytes(BinaryTestFile));

            // Validate root struct fields exist (as per DLG.ksy documentation)
            // Root struct should contain DLG-specific fields
            GFF gff = GFF.FromBytes(File.ReadAllBytes(BinaryTestFile));
            GFFStruct root = gff.Root;

            // Core DLG fields should be present or have defaults
            root.Acquire("NumWords", (uint)0).Should().BeGreaterThanOrEqualTo(0, "NumWords should be non-negative");
            byte skippable = root.Acquire("Skippable", (byte)0);
            skippable.Should().BeOneOf(new byte[] { 0, 1 }, "Skippable should be 0 or 1");
            byte computerType = root.Acquire("ComputerType", (byte)0);
            computerType.Should().BeOneOf(new byte[] { 0, 1 }, "ComputerType should be 0 (Modern) or 1 (Ancient)");
            root.Acquire("ConversationType", 0).Should().BeInRange(0, 3, "ConversationType should be 0-3");
        }

        [Fact(Timeout = 120000)]
        public void TestDlgEntryListStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            DLG dlg = DLGHelper.ReadDlg(File.ReadAllBytes(BinaryTestFile));

            // Validate EntryList structure
            var allEntries = dlg.AllEntries();
            allEntries.Should().NotBeNull("EntryList should not be null");

            // Each entry should have valid structure
            foreach (var entry in allEntries)
            {
                entry.Should().NotBeNull("Entry should not be null");
                entry.ListIndex.Should().BeGreaterThanOrEqualTo(-1, "ListIndex should be >= -1");
                entry.Speaker.Should().NotBeNull("Speaker should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestDlgReplyListStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            DLG dlg = DLGHelper.ReadDlg(File.ReadAllBytes(BinaryTestFile));

            // Validate ReplyList structure
            var allReplies = dlg.AllReplies();
            allReplies.Should().NotBeNull("ReplyList should not be null");

            // Each reply should have valid structure
            foreach (var reply in allReplies)
            {
                reply.Should().NotBeNull("Reply should not be null");
                reply.ListIndex.Should().BeGreaterThanOrEqualTo(-1, "ListIndex should be >= -1");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestDlgStartingListStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            DLG dlg = DLGHelper.ReadDlg(File.ReadAllBytes(BinaryTestFile));

            // Validate StartingList structure
            dlg.Starters.Should().NotBeNull("StartingList should not be null");

            // Each starter link should have valid structure
            foreach (var starter in dlg.Starters)
            {
                starter.Should().NotBeNull("Starter link should not be null");
                starter.Node.Should().NotBeNull("Starter link node should not be null");
                starter.Node.Should().BeOfType<DLGEntry>("Starter links should point to DLGEntry nodes");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestDlgStuntListStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            DLG dlg = DLGHelper.ReadDlg(File.ReadAllBytes(BinaryTestFile));

            // Validate StuntList structure
            dlg.Stunts.Should().NotBeNull("StuntList should not be null");

            // Each stunt should have valid structure
            foreach (var stunt in dlg.Stunts)
            {
                stunt.Should().NotBeNull("Stunt should not be null");
                stunt.Participant.Should().NotBeNull("Participant should not be null");
                stunt.StuntModel.Should().NotBeNull("StuntModel should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestDlgNodeLinksStructure()
        {
            if (!File.Exists(BinaryTestFile))
            {
                CreateTestDlgFile(BinaryTestFile);
            }

            DLG dlg = DLGHelper.ReadDlg(File.ReadAllBytes(BinaryTestFile));

            // Validate node links structure
            var allEntries = dlg.AllEntries();
            var allReplies = dlg.AllReplies();

            foreach (var entry in allEntries)
            {
                entry.Links.Should().NotBeNull($"Entry {entry.ListIndex} Links should not be null");
                foreach (var link in entry.Links)
                {
                    link.Should().NotBeNull("Link should not be null");
                    link.Node.Should().NotBeNull("Link node should not be null");
                    link.Node.Should().BeOfType<DLGReply>("Entry links should point to DLGReply nodes");
                }
            }

            foreach (var reply in allReplies)
            {
                reply.Links.Should().NotBeNull($"Reply {reply.ListIndex} Links should not be null");
                foreach (var link in reply.Links)
                {
                    link.Should().NotBeNull("Link should not be null");
                    link.Node.Should().NotBeNull("Link node should not be null");
                    link.Node.Should().BeOfType<DLGEntry>("Reply links should point to DLGEntry nodes");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestDlgEmptyFile()
        {
            // Test DLG with minimal structure
            var dlg = new DLG();
            dlg.WordCount = 0;
            dlg.OnAbort = ResRef.FromBlank();
            dlg.OnEnd = ResRef.FromBlank();

            byte[] data = DLGHelper.BytesDlg(dlg, BioWareGame.K1);
            DLG loaded = DLGHelper.ReadDlg(data);

            loaded.WordCount.Should().Be(0);
            loaded.Starters.Should().BeEmpty("Empty DLG should have no starters");
            loaded.Stunts.Should().BeEmpty("Empty DLG should have no stunts");
        }

        [Fact(Timeout = 120000)]
        public void TestDlgMultipleEntriesAndReplies()
        {
            // Test DLG with multiple entries and replies
            var dlg = new DLG();
            dlg.WordCount = 100;

            var entry1 = new DLGEntry { Speaker = "NPC1", ListIndex = 0 };
            entry1.Text = LocalizedString.FromEnglish("Entry 1");
            var entry2 = new DLGEntry { Speaker = "NPC2", ListIndex = 1 };
            entry2.Text = LocalizedString.FromEnglish("Entry 2");

            var reply1 = new DLGReply { ListIndex = 0 };
            reply1.Text = LocalizedString.FromEnglish("Reply 1");
            var reply2 = new DLGReply { ListIndex = 1 };
            reply2.Text = LocalizedString.FromEnglish("Reply 2");

            dlg.Starters.Add(new DLGLink(entry1, 0));
            entry1.Links.Add(new DLGLink(reply1, 0));
            entry1.Links.Add(new DLGLink(reply2, 1));
            reply1.Links.Add(new DLGLink(entry2, 0));

            byte[] data = DLGHelper.BytesDlg(dlg, BioWareGame.K1);
            DLG loaded = DLGHelper.ReadDlg(data);

            loaded.AllEntries().Count.Should().Be(2);
            loaded.AllReplies().Count.Should().Be(2);
            loaded.Starters.Count.Should().Be(1);
        }

        [Fact(Timeout = 120000)]
        public void TestDlgK1VersusK2Format()
        {
            // Test K1 format (no K2-specific fields)
            var dlgK1 = new DLG();
            dlgK1.WordCount = 50;
            dlgK1.AlienRaceOwner = 0;
            dlgK1.PostProcOwner = 0;
            dlgK1.RecordNoVo = 0;
            dlgK1.NextNodeId = 0;

            byte[] dataK1 = DLGHelper.BytesDlg(dlgK1, BioWareGame.K1);
            GFF gffK1 = GFF.FromBytes(dataK1);

            // K1 format should not have K2-specific root fields
            gffK1.Root.Exists("AlienRaceOwner").Should().BeFalse("K1 format should not include AlienRaceOwner");
            gffK1.Root.Exists("PostProcOwner").Should().BeFalse("K1 format should not include PostProcOwner");
            gffK1.Root.Exists("RecordNoVO").Should().BeFalse("K1 format should not include RecordNoVO");
            gffK1.Root.Exists("NextNodeID").Should().BeFalse("K1 format should not include NextNodeID");

            // Test K2 format (with K2-specific fields)
            var dlgK2 = new DLG();
            dlgK2.WordCount = 100;
            dlgK2.AlienRaceOwner = 5;
            dlgK2.PostProcOwner = 3;
            dlgK2.RecordNoVo = 1;
            dlgK2.NextNodeId = 42;

            byte[] dataK2 = DLGHelper.BytesDlg(dlgK2, BioWareGame.K2);
            GFF gffK2 = GFF.FromBytes(dataK2);

            // K2 format should have K2-specific root fields
            gffK2.Root.Exists("AlienRaceOwner").Should().BeTrue("K2 format should include AlienRaceOwner");
            gffK2.Root.Exists("PostProcOwner").Should().BeTrue("K2 format should include PostProcOwner");
            gffK2.Root.Exists("RecordNoVO").Should().BeTrue("K2 format should include RecordNoVO");
            gffK2.Root.Exists("NextNodeID").Should().BeTrue("K2 format should include NextNodeID");
        }

        [Fact(Timeout = 120000)]
        public void TestReadRaises()
        {
            // Test reading from directory
            Action act1 = () => DLGHelper.ReadDlg(File.ReadAllBytes("."));
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => DLGHelper.ReadDlg(File.ReadAllBytes(DoesNotExistFile));
            act2.Should().Throw<FileNotFoundException>();

            // Test reading corrupted file
            if (File.Exists(CorruptBinaryTestFile))
            {
                Action act3 = () => DLGHelper.ReadDlg(File.ReadAllBytes(CorruptBinaryTestFile));
                act3.Should().Throw<InvalidDataException>();
            }
        }

        [Fact(Timeout = 120000)]
        public void TestDlgRoundTripPreservesStructure()
        {
            // Create complex DLG structure
            var dlg = new DLG();
            dlg.WordCount = 200;
            dlg.Skippable = true;
            dlg.ConversationType = DLGConversationType.Human;
            dlg.ComputerType = DLGComputerType.Modern;
            dlg.OnAbort = new ResRef("abort_script");
            dlg.OnEnd = new ResRef("end_script");
            dlg.AmbientTrack = new ResRef("ambient_music");
            dlg.CameraModel = new ResRef("camera_model");

            var entry = new DLGEntry { Speaker = "TestNPC", ListIndex = 0 };
            entry.Text = LocalizedString.FromEnglish("Hello!");
            entry.Comment = "Test entry";
            entry.Script1 = new ResRef("entry_script");
            entry.CameraAngle = 45;
            entry.Delay = 100;

            var reply = new DLGReply { ListIndex = 0 };
            reply.Text = LocalizedString.FromEnglish("Greetings.");
            reply.Comment = "Test reply";

            var link = new DLGLink(reply, 0);
            link.Active1 = new ResRef("condition_script");
            link.Comment = "Link comment";
            entry.Links.Add(link);

            dlg.Starters.Add(new DLGLink(entry, 0));

            // Round-trip test
            byte[] data = DLGHelper.BytesDlg(dlg, BioWareGame.K1);
            DLG loaded = DLGHelper.ReadDlg(data);

            loaded.WordCount.Should().Be(200);
            loaded.Skippable.Should().BeTrue();
            loaded.ConversationType.Should().Be(DLGConversationType.Human);
            loaded.ComputerType.Should().Be(DLGComputerType.Modern);
            loaded.OnAbort.Should().Be(new ResRef("abort_script"));
            loaded.OnEnd.Should().Be(new ResRef("end_script"));
            loaded.AmbientTrack.Should().Be(new ResRef("ambient_music"));
            loaded.CameraModel.Should().Be(new ResRef("camera_model"));

            var loadedEntry = loaded.AllEntries().First();
            loadedEntry.Speaker.Should().Be("TestNPC");
            loadedEntry.Comment.Should().Be("Test entry");
            loadedEntry.Script1.Should().Be(new ResRef("entry_script"));
            loadedEntry.CameraAngle.Should().Be(45);
            loadedEntry.Delay.Should().Be(100);

            var loadedReply = loaded.AllReplies().First();
            loadedReply.Comment.Should().Be("Test reply");

            var loadedLink = loadedEntry.Links.First();
            loadedLink.Active1.Should().Be(new ResRef("condition_script"));
            loadedLink.Comment.Should().Be("Link comment");
        }

        private static void ValidateIO(DLG dlg)
        {
            // Basic validation
            dlg.Should().NotBeNull();
            dlg.WordCount.Should().BeGreaterThanOrEqualTo(0);
            dlg.Starters.Should().NotBeNull();
            dlg.Stunts.Should().NotBeNull();
        }

        private static void CreateTestDlgFile(string path)
        {
            var dlg = new DLG();
            dlg.WordCount = 50;
            dlg.OnAbort = ResRef.FromBlank();
            dlg.OnEnd = ResRef.FromBlank();
            dlg.Skippable = true;
            dlg.ConversationType = DLGConversationType.Human;
            dlg.ComputerType = DLGComputerType.Modern;

            var entry = new DLGEntry { Speaker = "TestNPC", ListIndex = 0 };
            entry.Text = LocalizedString.FromEnglish("Hello, world!");
            var reply = new DLGReply { ListIndex = 0 };
            reply.Text = LocalizedString.FromEnglish("Hi there!");

            dlg.Starters.Add(new DLGLink(entry, 0));
            entry.Links.Add(new DLGLink(reply, 0));

            byte[] data = DLGHelper.BytesDlg(dlg, BioWareGame.K1);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
        }
    }
}


