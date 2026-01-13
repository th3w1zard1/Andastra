using System;
using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Andastra.Parsing.Common;
using Andastra.Parsing.Installation;
using InstallationClass = Andastra.Parsing.Installation.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource.Formats.LYT;
using Andastra.Parsing.Resource.Formats.VIS;
using UTC = Andastra.Parsing.Resource.Generics.UTC.UTC;
using Andastra.Parsing.Resource.Generics.UTC;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Andastra.Parsing.Tests
{
    [TestFixture]
    public class ModuleTests
    {
        private string _testInstallPath;

        [SetUp]
        public void Setup()
        {
            // TODO:  Use a temporary directory or mock path for testing
            _testInstallPath = Path.GetTempPath();
        }

        [Test]
        public void TestModuleCreation()
        {
            // This test assumes we have a valid KotOR installation
            // In a real test environment, this would be set up with test data
            if (!InstallationClass.DetermineGame(_testInstallPath).HasValue)
            {
                Assert.Ignore("Test requires a valid KotOR installation path");
                return;
            }

            var installation = new InstallationClass(_testInstallPath);

            // TODO:  Test module creation with a dummy module name
            var module = new Module("test", installation, true);

            Assert.IsTrue(module != null);
            Assert.IsTrue(module.Root == "test");
        }

        [Test]
        public void TestModuleResourceCreation()
        {
            // Test basic ModuleResource functionality
            // Skip this test if we can't create a valid installation
            // (requires a valid game installation path)
            var testPath = Path.GetTempPath();
            BioWareGame? gameType = InstallationClass.DetermineGame(testPath);
            if (!gameType.HasValue)
            {
                // Skip test if no valid game installation
                Assert.Ignore("Skipping test - no valid game installation found");
            }

            // Create installation for testing
            InstallationClass installation;
            try
            {
                installation = new InstallationClass(testPath);
            }
            catch (InvalidOperationException)
            {
                Assert.Ignore("Skipping test - cannot create installation without valid game path");
                return; // Unreachable but needed for compiler
            }

            var moduleResource = new ModuleResource<object>("testres", ResourceType.GIT, installation, "testmodule");

            Assert.IsTrue(moduleResource != null);
            Assert.IsTrue(moduleResource.ResName == "testres");
            Assert.IsTrue(moduleResource.ResType == ResourceType.GIT);
        }

        [Test]
        public void TestGITCreation()
        {
            // Test GIT class creation and basic functionality
            var git = new GIT();

            Assert.IsTrue(git != null);
            Assert.IsTrue(git.Cameras != null);
            Assert.IsTrue(git.Creatures != null);
            Assert.IsTrue(git.Doors != null);
            Assert.IsTrue(git.Encounters != null);
            Assert.IsTrue(git.Placeables != null);
            Assert.IsTrue(git.Sounds != null);
            Assert.IsTrue(git.Stores != null);
            Assert.IsTrue(git.Triggers != null);
            Assert.IsTrue(git.Waypoints != null);

            // Test resource identifier iteration
            var identifiers = git.GetResourceIdentifiers();
            Assert.IsTrue(identifiers != null);
        }

        [Test]
        public void TestLYTCreation()
        {
            // Test LYT class creation
            var lyt = new LYT();

            Assert.IsTrue(lyt != null);
            Assert.IsTrue(lyt.Rooms != null);
            Assert.IsTrue(lyt.Tracks != null);
            Assert.IsTrue(lyt.Obstacles != null);
            Assert.IsTrue(lyt.DoorHooks != null);
        }

        [Test]
        public void TestVISCreation()
        {
            // Test VIS class creation
            var vis = new VIS();

            Assert.IsTrue(vis != null);
            var rooms = vis.AllRooms();
            Assert.IsTrue(rooms != null);

            // Test adding rooms
            vis.AddRoom("testroom1");
            vis.AddRoom("testroom2");

            rooms = vis.AllRooms();
            Assert.IsTrue(rooms.Contains("testroom1"));
            Assert.IsTrue(rooms.Contains("testroom2"));
        }

        [Test]
        public void TestUTCCreation()
        {
            // Test UTC class creation
            var utc = new UTC();

            Assert.IsTrue(utc != null);
            Assert.IsTrue(UTC.BinaryType == ResourceType.UTC);
        }

        [Test]
        public void TestUTDCreation()
        {
            // Test UTD class creation
            var utd = new UTD();

            Assert.IsTrue(utd != null);
            Assert.IsTrue(UTD.BinaryType == ResourceType.UTD);
        }

        [Test]
        public void TestUTPCreation()
        {
            // Test UTP class creation
            var utp = new UTP();

            Assert.IsTrue(utp != null);
            Assert.IsTrue(UTP.BinaryType == ResourceType.UTP);
        }

        [Test]
        public void TestArchiveResourceCreation()
        {
            // Test ArchiveResource creation
            var resRef = new ResRef("testresource");
            var resType = ResourceType.GIT;
            var data = new byte[] { 1, 2, 3, 4 };

            var archiveResource = new ArchiveResource(resRef, resType, data);

            Assert.IsTrue(archiveResource != null);
            Assert.IsTrue(archiveResource.ResRef == resRef);
            Assert.IsTrue(archiveResource.ResType == resType);
            Assert.IsTrue(archiveResource.Data == data);
        }

        [Test]
        public void TestResourceAutoLoad()
        {
            // Test ResourceAuto loading with null data
            var result = ResourceAuto.LoadResource(null, ResourceType.GIT);
            Assert.IsTrue(result == null);

            // Test with empty data
            result = ResourceAuto.LoadResource(new byte[0], ResourceType.GIT);
            Assert.IsTrue(result == null);
        }

        [Test]
        public void TestSalvageValidation()
        {
            // Test Salvage.ValidateResourceFile with null
            var result = Salvage.ValidateResourceFile(null);
            Assert.IsTrue(result == false);
        }
    }
}
