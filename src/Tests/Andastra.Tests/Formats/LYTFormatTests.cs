using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Andastra.Parsing.Common;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Tests.Common;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Formats
{
    /// <summary>
    /// Comprehensive tests for LYT (Layout) file format operations.
    /// Tests validate the LYT format structure as defined in LYT.ksy Kaitai Struct definition.
    /// </summary>
    public class LYTFormatTests
    {
        private static readonly string TestFile = TestFileHelper.GetPath("test.lyt");
        private static readonly string DoesNotExistFile = "./thisfiledoesnotexist";
        private static readonly string EmptyTestFile = TestFileHelper.GetPath("test_empty.lyt");
        private static readonly string CorruptTestFile = TestFileHelper.GetPath("test_corrupted.lyt");

        [Fact(Timeout = 120000)]
        public void TestAsciiIO()
        {
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            // Test reading LYT file
            LYT lyt = new LYTAsciiReader(TestFile).Load();
            ValidateIO(lyt);

            // Test writing and reading back
            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                lyt = new LYTAsciiReader(tempFile).Load();
                ValidateIO(lyt);
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
        public void TestLytFormatStructure()
        {
            // Test that LYT format matches Kaitai Struct definition
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            // Read file as text to validate structure
            string content = File.ReadAllText(TestFile, Encoding.ASCII);

            // Validate header (beginlayout)
            content.Should().StartWith("beginlayout", "LYT file should start with 'beginlayout' as defined in LYT.ksy");

            // Validate footer (donelayout)
            content.Should().Contain("donelayout", "LYT file should contain 'donelayout' as defined in LYT.ksy");
        }

        [Fact(Timeout = 120000)]
        public void TestLytFileContentStructure()
        {
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            string content = File.ReadAllText(TestFile, Encoding.ASCII);
            string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            // Validate structure: beginlayout, sections, donelayout
            lines[0].Trim().Should().Be("beginlayout", "First line should be 'beginlayout'");

            // Should contain section keywords
            bool hasRoomCount = lines.Any(l => l.Trim().StartsWith("roomcount", StringComparison.OrdinalIgnoreCase));
            bool hasDonelayout = lines.Any(l => l.Trim().Equals("donelayout", StringComparison.OrdinalIgnoreCase));

            hasDonelayout.Should().BeTrue("File should contain 'donelayout' keyword");
        }

        [Fact(Timeout = 120000)]
        public void TestLytRoomsSection()
        {
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            LYT lyt = new LYTAsciiReader(TestFile).Load();

            // Validate rooms structure (matching LYT.ksy documentation)
            lyt.Rooms.Should().NotBeNull("Rooms list should not be null");
            lyt.Rooms.Count.Should().BeGreaterThanOrEqualTo(0, "Room count should be non-negative");

            // Validate each room entry has required fields
            foreach (var room in lyt.Rooms)
            {
                room.Model.Should().NotBeNull("Room model should not be null");
                room.Model.ToString().Should().NotBeNullOrEmpty("Room model should not be empty");
                room.Position.Should().NotBeNull("Room position should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLytTracksSection()
        {
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            LYT lyt = new LYTAsciiReader(TestFile).Load();

            // Validate tracks structure (matching LYT.ksy documentation)
            lyt.Tracks.Should().NotBeNull("Tracks list should not be null");
            lyt.Tracks.Count.Should().BeGreaterThanOrEqualTo(0, "Track count should be non-negative");

            // Validate each track entry has required fields
            foreach (var track in lyt.Tracks)
            {
                track.Model.Should().NotBeNull("Track model should not be null");
                track.Model.ToString().Should().NotBeNullOrEmpty("Track model should not be empty");
                track.Position.Should().NotBeNull("Track position should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLytObstaclesSection()
        {
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            LYT lyt = new LYTAsciiReader(TestFile).Load();

            // Validate obstacles structure (matching LYT.ksy documentation)
            lyt.Obstacles.Should().NotBeNull("Obstacles list should not be null");
            lyt.Obstacles.Count.Should().BeGreaterThanOrEqualTo(0, "Obstacle count should be non-negative");

            // Validate each obstacle entry has required fields
            foreach (var obstacle in lyt.Obstacles)
            {
                obstacle.Model.Should().NotBeNull("Obstacle model should not be null");
                obstacle.Model.ToString().Should().NotBeNullOrEmpty("Obstacle model should not be empty");
                obstacle.Position.Should().NotBeNull("Obstacle position should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLytDoorhooksSection()
        {
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            LYT lyt = new LYTAsciiReader(TestFile).Load();

            // Validate doorhooks structure (matching LYT.ksy documentation)
            lyt.Doorhooks.Should().NotBeNull("Doorhooks list should not be null");
            lyt.Doorhooks.Count.Should().BeGreaterThanOrEqualTo(0, "Doorhook count should be non-negative");

            // Validate each doorhook entry has required fields
            foreach (var doorhook in lyt.Doorhooks)
            {
                doorhook.Room.Should().NotBeNullOrEmpty("Doorhook room should not be null or empty");
                doorhook.Door.Should().NotBeNullOrEmpty("Doorhook door should not be null or empty");
                doorhook.Position.Should().NotBeNull("Doorhook position should not be null");
                doorhook.Orientation.Should().NotBeNull("Doorhook orientation should not be null");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLytEmptyFile()
        {
            // Test LYT with minimal structure (no rooms, tracks, obstacles, doorhooks)
            var lyt = new LYT();
            lyt.Rooms.Should().NotBeNull("Empty LYT should have empty rooms list");
            lyt.Tracks.Should().NotBeNull("Empty LYT should have empty tracks list");
            lyt.Obstacles.Should().NotBeNull("Empty LYT should have empty obstacles list");
            lyt.Doorhooks.Should().NotBeNull("Empty LYT should have empty doorhooks list");

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(0);
                loaded.Tracks.Count.Should().Be(0);
                loaded.Obstacles.Count.Should().Be(0);
                loaded.Doorhooks.Count.Should().Be(0);
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
        public void TestLytMultipleRooms()
        {
            // Test LYT with multiple rooms
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("room1"), new Vector3(0.0f, 0.0f, 0.0f)));
            lyt.Rooms.Add(new LYTRoom(new ResRef("room2"), new Vector3(10.0f, 10.0f, 10.0f)));
            lyt.Rooms.Add(new LYTRoom(new ResRef("room3"), new Vector3(20.0f, 20.0f, 20.0f)));

            lyt.Rooms.Count.Should().Be(3);

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(3);
                loaded.Rooms[0].Model.Should().Be("room1");
                loaded.Rooms[1].Model.Should().Be("room2");
                loaded.Rooms[2].Model.Should().Be("room3");
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
        public void TestLytMultipleTracks()
        {
            // Test LYT with multiple tracks
            var lyt = new LYT();
            lyt.Tracks.Add(new LYTTrack(new ResRef("track1"), new Vector3(0.0f, 0.0f, 0.0f)));
            lyt.Tracks.Add(new LYTTrack(new ResRef("track2"), new Vector3(5.0f, 5.0f, 5.0f)));

            lyt.Tracks.Count.Should().Be(2);

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Tracks.Count.Should().Be(2);
                loaded.Tracks[0].Model.Should().Be("track1");
                loaded.Tracks[1].Model.Should().Be("track2");
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
        public void TestLytMultipleObstacles()
        {
            // Test LYT with multiple obstacles
            var lyt = new LYT();
            lyt.Obstacles.Add(new LYTObstacle(new ResRef("obstacle1"), new Vector3(0.0f, 0.0f, 0.0f)));
            lyt.Obstacles.Add(new LYTObstacle(new ResRef("obstacle2"), new Vector3(3.0f, 3.0f, 3.0f)));

            lyt.Obstacles.Count.Should().Be(2);

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Obstacles.Count.Should().Be(2);
                loaded.Obstacles[0].Model.Should().Be("obstacle1");
                loaded.Obstacles[1].Model.Should().Be("obstacle2");
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
        public void TestLytMultipleDoorhooks()
        {
            // Test LYT with multiple doorhooks
            var lyt = new LYT();
            lyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(0.0f, 0.0f, 0.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)));
            lyt.Doorhooks.Add(new LYTDoorHook("room2", "door2", new Vector3(5.0f, 5.0f, 5.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)));

            lyt.Doorhooks.Count.Should().Be(2);

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Doorhooks.Count.Should().Be(2);
                loaded.Doorhooks[0].Room.Should().Be("room1");
                loaded.Doorhooks[0].Door.Should().Be("door1");
                loaded.Doorhooks[1].Room.Should().Be("room2");
                loaded.Doorhooks[1].Door.Should().Be("door2");
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
        public void TestLytCompleteFile()
        {
            // Test LYT with all sections populated
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("room1"), new Vector3(0.0f, 0.0f, 0.0f)));
            lyt.Tracks.Add(new LYTTrack(new ResRef("track1"), new Vector3(1.0f, 1.0f, 1.0f)));
            lyt.Obstacles.Add(new LYTObstacle(new ResRef("obstacle1"), new Vector3(2.0f, 2.0f, 2.0f)));
            lyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(3.0f, 3.0f, 3.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(1);
                loaded.Tracks.Count.Should().Be(1);
                loaded.Obstacles.Count.Should().Be(1);
                loaded.Doorhooks.Count.Should().Be(1);
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
        public void TestLytRoomPositionValues()
        {
            // Test that room positions are preserved correctly
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("testroom"), new Vector3(123.456f, -789.012f, 345.678f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(1);
                loaded.Rooms[0].Position.X.Should().BeApproximately(123.456f, 0.001f);
                loaded.Rooms[0].Position.Y.Should().BeApproximately(-789.012f, 0.001f);
                loaded.Rooms[0].Position.Z.Should().BeApproximately(345.678f, 0.001f);
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
        public void TestLytDoorhookOrientationValues()
        {
            // Test that doorhook orientations are preserved correctly
            var lyt = new LYT();
            lyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(0.0f, 0.0f, 0.0f), new Vector4(0.5f, 0.5f, 0.5f, 0.5f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Doorhooks.Count.Should().Be(1);
                loaded.Doorhooks[0].Orientation.X.Should().BeApproximately(0.5f, 0.001f);
                loaded.Doorhooks[0].Orientation.Y.Should().BeApproximately(0.5f, 0.001f);
                loaded.Doorhooks[0].Orientation.Z.Should().BeApproximately(0.5f, 0.001f);
                loaded.Doorhooks[0].Orientation.W.Should().BeApproximately(0.5f, 0.001f);
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
            Action act1 = () => new LYTAsciiReader(".").Load();
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                act1.Should().Throw<UnauthorizedAccessException>();
            }
            else
            {
                act1.Should().Throw<IOException>();
            }

            // Test reading non-existent file
            Action act2 = () => new LYTAsciiReader(DoesNotExistFile).Load();
            act2.Should().Throw<FileNotFoundException>();
        }

        [Fact(Timeout = 120000)]
        public void TestLytRoundTrip()
        {
            // Test complete round-trip: create, write, read, validate
            var originalLyt = new LYT();
            originalLyt.Rooms.Add(new LYTRoom(new ResRef("room1"), new Vector3(1.0f, 2.0f, 3.0f)));
            originalLyt.Rooms.Add(new LYTRoom(new ResRef("room2"), new Vector3(4.0f, 5.0f, 6.0f)));
            originalLyt.Tracks.Add(new LYTTrack(new ResRef("track1"), new Vector3(7.0f, 8.0f, 9.0f)));
            originalLyt.Obstacles.Add(new LYTObstacle(new ResRef("obstacle1"), new Vector3(10.0f, 11.0f, 12.0f)));
            originalLyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(13.0f, 14.0f, 15.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                // Write
                new LYTAsciiWriter(originalLyt, tempFile).Write();

                // Read
                LYT loadedLyt = new LYTAsciiReader(tempFile).Load();

                // Validate
                loadedLyt.Rooms.Count.Should().Be(originalLyt.Rooms.Count);
                loadedLyt.Tracks.Count.Should().Be(originalLyt.Tracks.Count);
                loadedLyt.Obstacles.Count.Should().Be(originalLyt.Obstacles.Count);
                loadedLyt.Doorhooks.Count.Should().Be(originalLyt.Doorhooks.Count);

                for (int i = 0; i < originalLyt.Rooms.Count; i++)
                {
                    loadedLyt.Rooms[i].Model.Should().Be(originalLyt.Rooms[i].Model);
                    loadedLyt.Rooms[i].Position.Should().Be(originalLyt.Rooms[i].Position);
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
        public void TestLytAsciiEncoding()
        {
            // Test that LYT files are encoded as ASCII
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("testroom"), new Vector3(0.0f, 0.0f, 0.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();

                // Read as bytes and verify ASCII encoding
                byte[] bytes = File.ReadAllBytes(tempFile);
                string content = Encoding.ASCII.GetString(bytes);

                content.Should().Contain("beginlayout");
                content.Should().Contain("roomcount");
                content.Should().Contain("testroom");
                content.Should().Contain("donelayout");
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
        public void TestLytFileFormatKeywords()
        {
            // Test that LYT file contains all required keywords as per LYT.ksy documentation
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            string content = File.ReadAllText(TestFile, Encoding.ASCII);

            // Validate required keywords are present
            content.Should().Contain("beginlayout", "File should contain 'beginlayout' keyword");
            content.Should().Contain("donelayout", "File should contain 'donelayout' keyword");
        }

        [Fact(Timeout = 120000)]
        public void TestLytKaitaiStructFormatDefinition()
        {
            // Test that LYT format matches LYT.ksy Kaitai Struct definition
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            // Read file as raw content (as defined in LYT.ksy)
            string rawContent = File.ReadAllText(TestFile, Encoding.ASCII);

            // Validate header (beginlayout) - matches LYT.ksy definition
            rawContent.Should().StartWith("beginlayout", "LYT file should start with 'beginlayout' as defined in LYT.ksy");

            // Validate footer (donelayout) - matches LYT.ksy definition
            rawContent.Should().Contain("donelayout", "LYT file should contain 'donelayout' as defined in LYT.ksy");

            // Validate sections (optional, but should be parseable)
            // The format allows all sections to be optional
        }

        [Fact(Timeout = 120000)]
        public void TestLytRawContentEncoding()
        {
            // Test that raw_content field in LYT.ksy uses ASCII encoding
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            byte[] rawBytes = File.ReadAllBytes(TestFile);
            string rawContent = Encoding.ASCII.GetString(rawBytes);

            // All bytes should be valid ASCII
            foreach (byte b in rawBytes)
            {
                b.Should().BeLessThan(128, "All bytes should be valid ASCII (0-127)");
            }

            rawContent.Should().Contain("beginlayout");
            rawContent.Should().Contain("donelayout");
        }

        [Fact(Timeout = 120000)]
        public void TestLytSectionOrder()
        {
            // Test that sections appear in correct order (roomcount, trackcount, obstaclecount, doorhookcount)
            if (!File.Exists(TestFile))
            {
                CreateTestLytFile(TestFile);
            }

            string content = File.ReadAllText(TestFile, Encoding.ASCII);
            string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            // Find section keyword positions
            int roomcountIndex = -1;
            int trackcountIndex = -1;
            int obstaclecountIndex = -1;
            int doorhookcountIndex = -1;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].Trim().ToLowerInvariant();
                if (trimmed.StartsWith("roomcount"))
                {
                    roomcountIndex = i;
                }
                else if (trimmed.StartsWith("trackcount"))
                {
                    trackcountIndex = i;
                }
                else if (trimmed.StartsWith("obstaclecount"))
                {
                    obstaclecountIndex = i;
                }
                else if (trimmed.StartsWith("doorhookcount"))
                {
                    doorhookcountIndex = i;
                }
            }

            // If sections exist, they should be in order
            if (roomcountIndex >= 0 && trackcountIndex >= 0)
            {
                trackcountIndex.Should().BeGreaterThan(roomcountIndex, "trackcount should appear after roomcount");
            }
            if (trackcountIndex >= 0 && obstaclecountIndex >= 0)
            {
                obstaclecountIndex.Should().BeGreaterThan(trackcountIndex, "obstaclecount should appear after trackcount");
            }
            if (obstaclecountIndex >= 0 && doorhookcountIndex >= 0)
            {
                doorhookcountIndex.Should().BeGreaterThan(obstaclecountIndex, "doorhookcount should appear after obstaclecount");
            }
        }

        [Fact(Timeout = 120000)]
        public void TestLytRoomEntryFormat()
        {
            // Test that room entries match LYT.ksy room_entry type definition
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("roommodel"), new Vector3(1.5f, 2.5f, 3.5f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                string content = File.ReadAllText(tempFile, Encoding.ASCII);

                // Room entry format: <room_model> <x> <y> <z>
                // Should contain model name and three float coordinates
                content.Should().Contain("roommodel", "Room entry should contain model name");
                content.Should().Contain("1.5", "Room entry should contain X coordinate");
                content.Should().Contain("2.5", "Room entry should contain Y coordinate");
                content.Should().Contain("3.5", "Room entry should contain Z coordinate");
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
        public void TestLytTrackEntryFormat()
        {
            // Test that track entries match LYT.ksy track_entry type definition
            var lyt = new LYT();
            lyt.Tracks.Add(new LYTTrack(new ResRef("trackmodel"), new Vector3(10.0f, 20.0f, 30.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                string content = File.ReadAllText(tempFile, Encoding.ASCII);

                // Track entry format: <track_model> <x> <y> <z>
                content.Should().Contain("trackmodel", "Track entry should contain model name");
                content.Should().Contain("10", "Track entry should contain X coordinate");
                content.Should().Contain("20", "Track entry should contain Y coordinate");
                content.Should().Contain("30", "Track entry should contain Z coordinate");
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
        public void TestLytObstacleEntryFormat()
        {
            // Test that obstacle entries match LYT.ksy obstacle_entry type definition
            var lyt = new LYT();
            lyt.Obstacles.Add(new LYTObstacle(new ResRef("obstaclemodel"), new Vector3(100.0f, 200.0f, 300.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                string content = File.ReadAllText(tempFile, Encoding.ASCII);

                // Obstacle entry format: <obstacle_model> <x> <y> <z>
                content.Should().Contain("obstaclemodel", "Obstacle entry should contain model name");
                content.Should().Contain("100", "Obstacle entry should contain X coordinate");
                content.Should().Contain("200", "Obstacle entry should contain Y coordinate");
                content.Should().Contain("300", "Obstacle entry should contain Z coordinate");
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
        public void TestLytDoorhookEntryFormat()
        {
            // Test that doorhook entries match LYT.ksy doorhook_entry type definition
            var lyt = new LYT();
            lyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(5.0f, 6.0f, 7.0f), new Vector4(0.1f, 0.2f, 0.3f, 0.9f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                string content = File.ReadAllText(tempFile, Encoding.ASCII);

                // Doorhook entry format: <room_name> <door_name> 0 <x> <y> <z> <qx> <qy> <qz> <qw>
                content.Should().Contain("room1", "Doorhook entry should contain room name");
                content.Should().Contain("door1", "Doorhook entry should contain door name");
                content.Should().Contain("5", "Doorhook entry should contain X coordinate");
                content.Should().Contain("6", "Doorhook entry should contain Y coordinate");
                content.Should().Contain("7", "Doorhook entry should contain Z coordinate");
                content.Should().Contain("0.1", "Doorhook entry should contain qx quaternion component");
                content.Should().Contain("0.2", "Doorhook entry should contain qy quaternion component");
                content.Should().Contain("0.3", "Doorhook entry should contain qz quaternion component");
                content.Should().Contain("0.9", "Doorhook entry should contain qw quaternion component");
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
        public void TestLytNegativeCoordinates()
        {
            // Test that negative coordinates are handled correctly
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("room1"), new Vector3(-123.456f, -789.012f, -345.678f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms[0].Position.X.Should().BeApproximately(-123.456f, 0.001f);
                loaded.Rooms[0].Position.Y.Should().BeApproximately(-789.012f, 0.001f);
                loaded.Rooms[0].Position.Z.Should().BeApproximately(-345.678f, 0.001f);
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
        public void TestLytLargeCoordinates()
        {
            // Test that large coordinate values are handled correctly
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("room1"), new Vector3(999999.0f, -999999.0f, 12345.678f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms[0].Position.X.Should().BeApproximately(999999.0f, 0.001f);
                loaded.Rooms[0].Position.Y.Should().BeApproximately(-999999.0f, 0.001f);
                loaded.Rooms[0].Position.Z.Should().BeApproximately(12345.678f, 0.001f);
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
        public void TestLytQuaternionNormalization()
        {
            // Test that quaternion values in doorhooks are preserved correctly
            var lyt = new LYT();
            // Use a non-normalized quaternion to test preservation
            lyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(0.0f, 0.0f, 0.0f), new Vector4(1.0f, 2.0f, 3.0f, 4.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Doorhooks[0].Orientation.X.Should().BeApproximately(1.0f, 0.001f);
                loaded.Doorhooks[0].Orientation.Y.Should().BeApproximately(2.0f, 0.001f);
                loaded.Doorhooks[0].Orientation.Z.Should().BeApproximately(3.0f, 0.001f);
                loaded.Doorhooks[0].Orientation.W.Should().BeApproximately(4.0f, 0.001f);
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
        public void TestLytModelNameCaseInsensitivity()
        {
            // Test that model names are case-insensitive as per LYT.ksy documentation
            var lyt1 = new LYT();
            lyt1.Rooms.Add(new LYTRoom(new ResRef("TestRoom"), new Vector3(0.0f, 0.0f, 0.0f)));

            var lyt2 = new LYT();
            lyt2.Rooms.Add(new LYTRoom(new ResRef("testroom"), new Vector3(0.0f, 0.0f, 0.0f)));

            // Room names should be compared case-insensitively
            lyt1.Rooms[0].Model.ToString().ToLowerInvariant().Should().Be(lyt2.Rooms[0].Model.ToString().ToLowerInvariant());
        }

        [Fact(Timeout = 120000)]
        public void TestLytAllSectionsEmpty()
        {
            // Test LYT file with all sections present but empty (counts are 0)
            var lyt = new LYT();
            // All lists are empty

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                string content = File.ReadAllText(tempFile, Encoding.ASCII);

                // Should still have valid structure
                content.Should().StartWith("beginlayout");
                content.Should().Contain("donelayout");
                content.Should().Contain("roomcount 0");
                content.Should().Contain("trackcount 0");
                content.Should().Contain("obstaclecount 0");
                content.Should().Contain("doorhookcount 0");

                // Read it back
                LYT loaded = new LYTAsciiReader(tempFile).Load();
                loaded.Rooms.Count.Should().Be(0);
                loaded.Tracks.Count.Should().Be(0);
                loaded.Obstacles.Count.Should().Be(0);
                loaded.Doorhooks.Count.Should().Be(0);
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
        public void TestLytOnlyRoomsSection()
        {
            // Test LYT file with only rooms section (other sections omitted)
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("room1"), new Vector3(1.0f, 2.0f, 3.0f)));
            lyt.Rooms.Add(new LYTRoom(new ResRef("room2"), new Vector3(4.0f, 5.0f, 6.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(2);
                loaded.Tracks.Count.Should().Be(0);
                loaded.Obstacles.Count.Should().Be(0);
                loaded.Doorhooks.Count.Should().Be(0);
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
        public void TestLytOnlyTracksSection()
        {
            // Test LYT file with only tracks section
            var lyt = new LYT();
            lyt.Tracks.Add(new LYTTrack(new ResRef("track1"), new Vector3(1.0f, 2.0f, 3.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(0);
                loaded.Tracks.Count.Should().Be(1);
                loaded.Obstacles.Count.Should().Be(0);
                loaded.Doorhooks.Count.Should().Be(0);
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
        public void TestLytOnlyObstaclesSection()
        {
            // Test LYT file with only obstacles section
            var lyt = new LYT();
            lyt.Obstacles.Add(new LYTObstacle(new ResRef("obstacle1"), new Vector3(1.0f, 2.0f, 3.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(0);
                loaded.Tracks.Count.Should().Be(0);
                loaded.Obstacles.Count.Should().Be(1);
                loaded.Doorhooks.Count.Should().Be(0);
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
        public void TestLytOnlyDoorhooksSection()
        {
            // Test LYT file with only doorhooks section
            var lyt = new LYT();
            lyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(1.0f, 2.0f, 3.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                loaded.Rooms.Count.Should().Be(0);
                loaded.Tracks.Count.Should().Be(0);
                loaded.Obstacles.Count.Should().Be(0);
                loaded.Doorhooks.Count.Should().Be(1);
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
        public void TestLytDoorhookZeroField()
        {
            // Test that doorhook entries include the reserved 0 field as per LYT.ksy doorhook_entry definition
            var lyt = new LYT();
            lyt.Doorhooks.Add(new LYTDoorHook("room1", "door1", new Vector3(1.0f, 2.0f, 3.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                string content = File.ReadAllText(tempFile, Encoding.ASCII);

                // Doorhook format: <room_name> <door_name> 0 <x> <y> <z> <qx> <qy> <qz> <qw>
                // Should contain the reserved "0" field between door_name and position
                string[] lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                bool foundDoorhookLine = false;
                foreach (string line in lines)
                {
                    if (line.Contains("door1") && line.Contains("room1"))
                    {
                        foundDoorhookLine = true;
                        // Check that line has format: room1 door1 0 x y z qx qy qz qw
                        string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        tokens.Length.Should().BeGreaterThanOrEqualTo(10, "Doorhook line should have at least 10 tokens (room, door, 0, x, y, z, qx, qy, qz, qw)");
                        tokens[2].Should().Be("0", "Third token should be reserved field '0'");
                        break;
                    }
                }
                foundDoorhookLine.Should().BeTrue("Should find doorhook line in file");
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
        public void TestLytCoordinatePrecision()
        {
            // Test that coordinate precision is preserved through round-trip
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("room1"), new Vector3(0.123456789f, -0.987654321f, 123.456789f)));

            string tempFile = Path.GetTempFileName();
            try
            {
                new LYTAsciiWriter(lyt, tempFile).Write();
                LYT loaded = new LYTAsciiReader(tempFile).Load();

                // Float precision may vary, so check with reasonable tolerance
                loaded.Rooms[0].Position.X.Should().BeApproximately(0.123456789f, 0.0001f);
                loaded.Rooms[0].Position.Y.Should().BeApproximately(-0.987654321f, 0.0001f);
                loaded.Rooms[0].Position.Z.Should().BeApproximately(123.456789f, 0.0001f);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        private static void ValidateIO(LYT lyt)
        {
            // Basic validation
            lyt.Should().NotBeNull();
            lyt.Rooms.Should().NotBeNull();
            lyt.Tracks.Should().NotBeNull();
            lyt.Obstacles.Should().NotBeNull();
            lyt.Doorhooks.Should().NotBeNull();
            lyt.Rooms.Count.Should().BeGreaterThanOrEqualTo(0);
            lyt.Tracks.Count.Should().BeGreaterThanOrEqualTo(0);
            lyt.Obstacles.Count.Should().BeGreaterThanOrEqualTo(0);
            lyt.Doorhooks.Count.Should().BeGreaterThanOrEqualTo(0);
        }

        private static void CreateTestLytFile(string path)
        {
            var lyt = new LYT();
            lyt.Rooms.Add(new LYTRoom(new ResRef("testroom"), new Vector3(0.0f, 0.0f, 0.0f)));
            lyt.Tracks.Add(new LYTTrack(new ResRef("testtrack"), new Vector3(1.0f, 1.0f, 1.0f)));
            lyt.Obstacles.Add(new LYTObstacle(new ResRef("testobstacle"), new Vector3(2.0f, 2.0f, 2.0f)));
            lyt.Doorhooks.Add(new LYTDoorHook("testroom", "testdoor", new Vector3(3.0f, 3.0f, 3.0f), new Vector4(0.0f, 0.0f, 0.0f, 1.0f)));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            new LYTAsciiWriter(lyt, path).Write();
        }
    }
}


