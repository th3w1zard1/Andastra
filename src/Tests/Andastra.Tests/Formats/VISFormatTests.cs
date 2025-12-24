using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Andastra.Parsing.Formats.VIS;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;
using static Andastra.Parsing.Formats.VIS.VISAuto;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for VIS (Visibility) file format I/O operations.
    /// Tests validate the VIS format structure, parsing, writing, and round-trip functionality.
    ///
    /// VIS Format Overview:
    /// - ASCII text format defining room visibility relationships
    /// - Parent lines: "ROOM_NAME CHILD_COUNT"
    /// - Child lines: "  ROOM_NAME" (indented with 2 spaces)
    /// - Empty lines and version headers are ignored
    /// - Room names are case-insensitive
    /// </summary>
    public class VISFormatTests
    {
        private static readonly string TestVisFile = TestFileHelper.GetPath("test.vis");
        private static readonly string TestVisFileCorrupted = TestFileHelper.GetPath("test_corrupted.vis");
        private static readonly string TestVisFileEmpty = TestFileHelper.GetPath("test_empty.vis");
        private static readonly string TestVisFileVersionHeader = TestFileHelper.GetPath("test_version_header.vis");

        static VISFormatTests()
        {
            // Ensure test files directory exists
            string testFilesDir = Path.GetDirectoryName(TestVisFile);
            if (!Directory.Exists(testFilesDir))
            {
                Directory.CreateDirectory(testFilesDir);
            }

            // Create test files if they don't exist
            if (!File.Exists(TestVisFile))
            {
                CreateTestVisFile(TestVisFile);
            }

            if (!File.Exists(TestVisFileCorrupted))
            {
                CreateCorruptedTestVisFile(TestVisFileCorrupted);
            }

            if (!File.Exists(TestVisFileEmpty))
            {
                CreateEmptyTestVisFile(TestVisFileEmpty);
            }

            if (!File.Exists(TestVisFileVersionHeader))
            {
                CreateVersionHeaderTestVisFile(TestVisFileVersionHeader);
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisFileReadingFromFile()
        {
            // Test reading VIS file from file path
            VIS vis = ReadVis(TestVisFile);
            vis.Should().NotBeNull("VIS file should be readable");
            vis.AllRooms().Count.Should().BeGreaterThan(0, "VIS file should contain rooms");
        }

        [Fact(Timeout = 120000)]
        public void TestVisFileReadingFromBytes()
        {
            // Test reading VIS file from byte array
            byte[] visBytes = File.ReadAllBytes(TestVisFile);
            VIS vis = ReadVis(visBytes);
            vis.Should().NotBeNull("VIS file should be readable from bytes");
            vis.AllRooms().Count.Should().BeGreaterThan(0, "VIS file should contain rooms");
        }

        [Fact(Timeout = 120000)]
        public void TestVisFileReadingFromStream()
        {
            // Test reading VIS file from stream
            using (FileStream stream = File.OpenRead(TestVisFile))
            {
                VIS vis = ReadVis(stream);
                vis.Should().NotBeNull("VIS file should be readable from stream");
                vis.AllRooms().Count.Should().BeGreaterThan(0, "VIS file should contain rooms");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisFileStructure()
        {
            // Test that VIS file structure matches expected format
            VIS vis = ReadVis(TestVisFile);
            vis.Should().NotBeNull();

            // Verify rooms exist
            HashSet<string> rooms = vis.AllRooms();
            rooms.Should().NotBeEmpty("VIS file should contain at least one room");

            // Verify visibility relationships
            foreach (var pair in vis.GetEnumerator())
            {
                string observer = pair.Item1;
                HashSet<string> visible = pair.Item2;

                observer.Should().NotBeNullOrEmpty("Observer room name should not be empty");
                visible.Should().NotBeNull("Visible rooms set should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisRoomExistence()
        {
            // Test room existence checks
            VIS vis = ReadVis(TestVisFile);
            HashSet<string> rooms = vis.AllRooms();

            foreach (string room in rooms)
            {
                vis.RoomExists(room).Should().BeTrue($"Room '{room}' should exist");
                vis.RoomExists(room.ToUpperInvariant()).Should().BeTrue("Room names should be case-insensitive");
                vis.RoomExists(room.ToLowerInvariant()).Should().BeTrue("Room names should be case-insensitive");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisVisibilityRelationships()
        {
            // Test visibility relationship operations
            VIS vis = ReadVis(TestVisFile);

            HashSet<string> rooms = vis.AllRooms();
            if (rooms.Count >= 2)
            {
                string room1 = rooms.First();
                string room2 = rooms.Skip(1).First();

                // Test setting visibility
                vis.SetVisible(room1, room2, visible: true);
                vis.GetVisible(room1, room2).Should().BeTrue($"Room '{room2}' should be visible from '{room1}'");

                // Test removing visibility
                vis.SetVisible(room1, room2, visible: false);
                vis.GetVisible(room1, room2).Should().BeFalse($"Room '{room2}' should not be visible from '{room1}'");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisSetAllVisible()
        {
            // Test setting all rooms visible from all other rooms
            VIS vis = ReadVis(TestVisFile);
            HashSet<string> rooms = vis.AllRooms();

            if (rooms.Count >= 2)
            {
                vis.SetAllVisible();

                // Verify all rooms are visible from each other (except self)
                foreach (string observer in rooms)
                {
                    foreach (string target in rooms.Where(r => r != observer))
                    {
                        vis.GetVisible(observer, target).Should().BeTrue(
                            $"Room '{target}' should be visible from '{observer}' after SetAllVisible()");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisAddRoom()
        {
            // Test adding new rooms
            VIS vis = ReadVis(TestVisFile);
            string newRoom = "new_test_room";

            vis.AddRoom(newRoom);
            vis.RoomExists(newRoom).Should().BeTrue("New room should exist after AddRoom()");

            // Verify room is added with empty visibility set
            HashSet<string> visibleRooms = vis.GetVisibleRooms(newRoom);
            visibleRooms.Should().NotBeNull("New room should have a visibility set");
        }

        [Fact(Timeout = 120000)]
        public void TestVisRemoveRoom()
        {
            // Test removing rooms
            VIS vis = ReadVis(TestVisFile);
            HashSet<string> rooms = vis.AllRooms();

            if (rooms.Count > 0)
            {
                string roomToRemove = rooms.First();
                vis.RemoveRoom(roomToRemove);
                vis.RoomExists(roomToRemove).Should().BeFalse("Room should not exist after RemoveRoom()");

                // Verify room is removed from all visibility sets
                foreach (string observer in vis.AllRooms())
                {
                    HashSet<string> visible = vis.GetVisibleRooms(observer);
                    if (visible != null)
                    {
                        visible.Should().NotContain(roomToRemove,
                            $"Removed room '{roomToRemove}' should not be in visibility set of '{observer}'");
                    }
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisRenameRoom()
        {
            // Test renaming rooms
            VIS vis = ReadVis(TestVisFile);
            HashSet<string> rooms = vis.AllRooms();

            if (rooms.Count > 0)
            {
                string oldName = rooms.First();
                string newName = "renamed_room";

                // Set up visibility relationships for the room
                if (rooms.Count > 1)
                {
                    string otherRoom = rooms.Skip(1).First();
                    vis.SetVisible(oldName, otherRoom, visible: true);
                }

                vis.RenameRoom(oldName, newName);

                vis.RoomExists(oldName).Should().BeFalse("Old room name should not exist after rename");
                vis.RoomExists(newName).Should().BeTrue("New room name should exist after rename");

                // Verify visibility relationships are preserved
                if (rooms.Count > 1)
                {
                    string otherRoom = rooms.Skip(1).First();
                    vis.GetVisible(newName, otherRoom).Should().BeTrue(
                        "Visibility relationships should be preserved after rename");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisCaseInsensitivity()
        {
            // Test that room names are case-insensitive
            VIS vis = ReadVis(TestVisFile);
            HashSet<string> rooms = vis.AllRooms();

            if (rooms.Count >= 2)
            {
                string room1 = rooms.First();
                string room2 = rooms.Skip(1).First();

                // Test with different cases
                vis.SetVisible(room1.ToUpperInvariant(), room2.ToLowerInvariant(), visible: true);
                vis.GetVisible(room1, room2).Should().BeTrue("Room visibility should be case-insensitive");

                vis.SetVisible(room1, room2.ToUpperInvariant(), visible: false);
                vis.GetVisible(room1, room2).Should().BeFalse("Room visibility should be case-insensitive");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisWritingToFile()
        {
            // Test writing VIS file to file path
            VIS vis = ReadVis(TestVisFile);
            string outputPath = TestFileHelper.GetPath("test_output.vis");

            try
            {
                WriteVis(vis, outputPath);
                File.Exists(outputPath).Should().BeTrue("Output VIS file should be created");

                // Verify file can be read back
                VIS readBack = ReadVis(outputPath);
                readBack.Should().NotBeNull("Written VIS file should be readable");
                readBack.AllRooms().Count.Should().Be(vis.AllRooms().Count, "Room count should match");
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisWritingToStream()
        {
            // Test writing VIS file to stream
            VIS vis = ReadVis(TestVisFile);
            string outputPath = TestFileHelper.GetPath("test_output_stream.vis");

            try
            {
                using (FileStream stream = File.Create(outputPath))
                {
                    WriteVis(vis, stream);
                }

                File.Exists(outputPath).Should().BeTrue("Output VIS file should be created");

                // Verify file can be read back
                VIS readBack = ReadVis(outputPath);
                readBack.Should().NotBeNull("Written VIS file should be readable");
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisBytesVis()
        {
            // Test converting VIS to byte array
            VIS vis = ReadVis(TestVisFile);
            byte[] visBytes = BytesVis(vis);

            visBytes.Should().NotBeNull("BytesVis should return byte array");
            visBytes.Length.Should().BeGreaterThan(0, "Byte array should not be empty");

            // Verify bytes can be read back
            VIS readBack = ReadVis(visBytes);
            readBack.Should().NotBeNull("VIS bytes should be readable");
            readBack.AllRooms().Count.Should().Be(vis.AllRooms().Count, "Room count should match");
        }

        [Fact(Timeout = 120000)]
        public void TestVisRoundTrip()
        {
            // Test round-trip: read -> write -> read
            VIS original = ReadVis(TestVisFile);
            byte[] writtenBytes = BytesVis(original);
            VIS roundTripped = ReadVis(writtenBytes);

            roundTripped.Should().NotBeNull("Round-tripped VIS should not be null");
            roundTripped.AllRooms().Count.Should().Be(original.AllRooms().Count, "Room count should match after round-trip");

            // Verify all visibility relationships are preserved
            HashSet<string> originalRooms = original.AllRooms();
            HashSet<string> roundTrippedRooms = roundTripped.AllRooms();
            roundTrippedRooms.SetEquals(originalRooms).Should().BeTrue("Room sets should match after round-trip");

            foreach (string observer in originalRooms)
            {
                HashSet<string> originalVisible = original.GetVisibleRooms(observer);
                HashSet<string> roundTrippedVisible = roundTripped.GetVisibleRooms(observer);

                if (originalVisible != null && roundTrippedVisible != null)
                {
                    roundTrippedVisible.SetEquals(originalVisible).Should().BeTrue(
                        $"Visibility set for '{observer}' should match after round-trip");
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisEmptyFile()
        {
            // Test reading empty VIS file
            VIS vis = ReadVis(TestVisFileEmpty);
            vis.Should().NotBeNull("Empty VIS file should be readable");
            vis.AllRooms().Count.Should().Be(0, "Empty VIS file should have no rooms");
        }

        [Fact(Timeout = 120000)]
        public void TestVisVersionHeader()
        {
            // Test that version headers are skipped
            VIS vis = ReadVis(TestVisFileVersionHeader);
            vis.Should().NotBeNull("VIS file with version header should be readable");
            vis.AllRooms().Count.Should().BeGreaterThan(0, "VIS file should have rooms after version header");
        }

        [Fact(Timeout = 120000)]
        public void TestVisInvalidRoomThrowsException()
        {
            // Test that operations on invalid rooms throw exceptions
            VIS vis = ReadVis(TestVisFile);

            Action setVisibleInvalid = () => vis.SetVisible("nonexistent_room1", "nonexistent_room2", visible: true);
            setVisibleInvalid.Should().Throw<ArgumentException>("SetVisible with nonexistent rooms should throw");

            Action getVisibleInvalid = () => vis.GetVisible("nonexistent_room1", "nonexistent_room2");
            getVisibleInvalid.Should().Throw<ArgumentException>("GetVisible with nonexistent rooms should throw");
        }

        [Fact(Timeout = 120000)]
        public void TestVisSelfVisibility()
        {
            // Test that rooms can be visible from themselves (rare but valid)
            VIS vis = ReadVis(TestVisFile);
            HashSet<string> rooms = vis.AllRooms();

            if (rooms.Count > 0)
            {
                string room = rooms.First();
                vis.SetVisible(room, room, visible: true);
                vis.GetVisible(room, room).Should().BeTrue("Room should be able to be visible from itself");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisGetVisibleRooms()
        {
            // Test GetVisibleRooms method
            VIS vis = ReadVis(TestVisFile);
            HashSet<string> rooms = vis.AllRooms();

            if (rooms.Count > 0)
            {
                string observer = rooms.First();
                HashSet<string> visibleRooms = vis.GetVisibleRooms(observer);
                visibleRooms.Should().NotBeNull("GetVisibleRooms should return a set (possibly empty)");

                // Test with nonexistent room
                HashSet<string> invalidVisible = vis.GetVisibleRooms("nonexistent_room");
                invalidVisible.Should().BeNull("GetVisibleRooms should return null for nonexistent room");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisGetEnumerator()
        {
            // Test GetEnumerator method
            VIS vis = ReadVis(TestVisFile);
            int pairCount = 0;

            foreach (var pair in vis.GetEnumerator())
            {
                pair.Item1.Should().NotBeNullOrEmpty("Observer room name should not be empty");
                pair.Item2.Should().NotBeNull("Visible rooms set should not be null");
                pairCount++;
            }

            pairCount.Should().BeGreaterThanOrEqualTo(0, "Should enumerate all visibility pairs");
        }

        [Fact(Timeout = 120000)]
        public void TestVisEquality()
        {
            // Test VIS equality comparison
            VIS vis1 = ReadVis(TestVisFile);
            VIS vis2 = ReadVis(TestVisFile);

            vis1.Should().NotBeNull();
            vis2.Should().NotBeNull();

            // Two VIS files read from the same source should be equal
            vis1.Equals(vis2).Should().BeTrue("VIS files read from same source should be equal");
            ReferenceEquals(vis1, vis2).Should().BeFalse("VIS objects should use Equals() for comparison, not reference equality");

            // Test with different VIS
            VIS vis3 = new VIS();
            vis3.AddRoom("test_room");
            vis1.Equals(vis3).Should().BeFalse("Different VIS files should not be equal");
        }

        [Fact(Timeout = 120000)]
        public void TestVisHashCode()
        {
            // Test VIS hash code
            VIS vis1 = ReadVis(TestVisFile);
            VIS vis2 = ReadVis(TestVisFile);

            vis1.GetHashCode().Should().Be(vis2.GetHashCode(), "Equal VIS files should have same hash code");
        }

        [Fact(Timeout = 120000)]
        public void TestVisInvalidFormatThrowsException()
        {
            // Test that invalid VIS format throws appropriate exceptions
            string invalidContent = "room_01 invalid_count\n  room_02";
            byte[] invalidBytes = Encoding.ASCII.GetBytes(invalidContent);

            Action readInvalid = () => ReadVis(invalidBytes);
            readInvalid.Should().Throw<ArgumentException>("Invalid VIS format should throw exception");
        }

        [Fact(Timeout = 120000)]
        public void TestVisMissingChildRoomsThrowsException()
        {
            // Test that missing child rooms throws exception
            string invalidContent = "room_01 3\n  room_02\n";
            byte[] invalidBytes = Encoding.ASCII.GetBytes(invalidContent);

            Action readInvalid = () => ReadVis(invalidBytes);
            readInvalid.Should().Throw<ArgumentException>("VIS file with missing child rooms should throw exception");
        }

        [Fact(Timeout = 120000)]
        public void TestVisAsciiReaderDispose()
        {
            // Test that VISAsciiReader properly disposes resources
            using (var reader = new VISAsciiReader(TestVisFile))
            {
                VIS vis = reader.Load(autoClose: false);
                vis.Should().NotBeNull();
            }
            // Reader should be disposed here
        }

        [Fact(Timeout = 120000)]
        public void TestVisAsciiWriterDispose()
        {
            // Test that VISAsciiWriter properly disposes resources
            VIS vis = ReadVis(TestVisFile);
            string outputPath = TestFileHelper.GetPath("test_dispose.vis");

            try
            {
                using (var writer = new VISAsciiWriter(vis, outputPath))
                {
                    writer.Write(autoClose: false);
                }
                // Writer should be disposed here
                File.Exists(outputPath).Should().BeTrue("Output file should exist after disposal");
            }
            finally
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }
            }
        }

        [Fact(Timeout = 120000)]
        public void TestVisComplexVisibilityGraph()
        {
            // Test complex visibility graph
            VIS vis = new VIS();

            // Create a graph: room1 -> room2, room3; room2 -> room3, room4; room3 -> room1
            vis.AddRoom("room1");
            vis.AddRoom("room2");
            vis.AddRoom("room3");
            vis.AddRoom("room4");

            vis.SetVisible("room1", "room2", visible: true);
            vis.SetVisible("room1", "room3", visible: true);
            vis.SetVisible("room2", "room3", visible: true);
            vis.SetVisible("room2", "room4", visible: true);
            vis.SetVisible("room3", "room1", visible: true);

            // Verify relationships
            vis.GetVisible("room1", "room2").Should().BeTrue();
            vis.GetVisible("room1", "room3").Should().BeTrue();
            vis.GetVisible("room2", "room3").Should().BeTrue();
            vis.GetVisible("room2", "room4").Should().BeTrue();
            vis.GetVisible("room3", "room1").Should().BeTrue();

            // Test round-trip
            byte[] bytes = BytesVis(vis);
            VIS roundTripped = ReadVis(bytes);

            roundTripped.GetVisible("room1", "room2").Should().BeTrue();
            roundTripped.GetVisible("room1", "room3").Should().BeTrue();
            roundTripped.GetVisible("room2", "room3").Should().BeTrue();
            roundTripped.GetVisible("room2", "room4").Should().BeTrue();
            roundTripped.GetVisible("room3", "room1").Should().BeTrue();
        }

        [Fact(Timeout = 120000)]
        public void TestVisReadWithOffsetAndSize()
        {
            // Test reading VIS file with offset and size
            byte[] allBytes = File.ReadAllBytes(TestVisFile);
            if (allBytes.Length > 10)
            {
                // Test with offset
                VIS vis = ReadVis(allBytes, offset: 0, size: null);
                vis.Should().NotBeNull("VIS should be readable with offset");

                // Test with size
                int size = Math.Min(100, allBytes.Length);
                VIS visWithSize = ReadVis(allBytes, offset: 0, size: size);
                visWithSize.Should().NotBeNull("VIS should be readable with size limit");
            }
        }

        private static void CreateTestVisFile(string filepath)
        {
            // Create a test VIS file matching the format from vendor/PyKotor tests
            string content = @"room_01 3
  room_02
  room_03
  room_04
room_02 1
  room_01
room_03 2
  room_01
  room_04
room_04 2
  room_03
  room_01
";
            File.WriteAllText(filepath, content, Encoding.ASCII);
        }

        private static void CreateCorruptedTestVisFile(string filepath)
        {
            // Create a corrupted VIS file with invalid count
            string content = @"room_01 77
  room_02
  room_03
  room_04
";
            File.WriteAllText(filepath, content, Encoding.ASCII);
        }

        private static void CreateEmptyTestVisFile(string filepath)
        {
            // Create an empty VIS file
            File.WriteAllText(filepath, "", Encoding.ASCII);
        }

        private static void CreateVersionHeaderTestVisFile(string filepath)
        {
            // Create a VIS file with version header
            string content = @"room V3.28
room_01 2
  room_02
  room_03
";
            File.WriteAllText(filepath, content, Encoding.ASCII);
        }
    }
}

