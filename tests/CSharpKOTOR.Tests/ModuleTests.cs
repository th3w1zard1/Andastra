using System;
using System.IO;
using NUnit.Framework;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Installation;
using AuroraEngine.Common.Resources;

namespace AuroraEngine.Common.Tests
{
    [TestFixture]
    public class ModuleTests
    {
        private string _testInstallPath;

        [SetUp]
        public void Setup()
        {
            // Use a temporary directory or mock path for testing
            _testInstallPath = Path.GetTempPath();
        }

        [Test]
        public void TestModuleCreation()
        {
            // This test assumes we have a valid KotOR installation
            // In a real test environment, this would be set up with test data
            if (!Installation.Installation.DetermineGame(_testInstallPath).HasValue)
            {
                Assert.Ignore("Test requires a valid KotOR installation path");
                return;
            }

            var installation = new Installation.Installation(_testInstallPath);

            // Test module creation with a dummy module name
            var module = new Module("test", installation, true);

            Assert.IsNotNull(module);
            Assert.AreEqual("test", module.Root);
        }

        [Test]
        public void TestModuleResourceCreation()
        {
            // Test basic ModuleResource functionality
            // Skip this test if we can't create a valid installation
            // (requires a valid game installation path)
            var testPath = Path.GetTempPath();
            Game? gameType = Installation.Installation.DetermineGame(testPath);
            if (!gameType.HasValue)
            {
                // Skip test if no valid game installation
                Assert.Ignore("Skipping test - no valid game installation found");
            }

            // Create installation for testing
            Installation.Installation installation;
            try
            {
                installation = new Installation.Installation(testPath);
            }
            catch (InvalidOperationException)
            {
                Assert.Ignore("Skipping test - cannot create installation without valid game path");
                return; // Unreachable but needed for compiler
            }

            var moduleResource = new ModuleResource<object>("testres", ResourceType.GIT, installation, "testmodule");

            Assert.IsNotNull(moduleResource);
            Assert.AreEqual("testres", moduleResource.ResName);
            Assert.AreEqual(ResourceType.GIT, moduleResource.ResType);
        }

        [Test]
        public void TestGITCreation()
        {
            // Test GIT class creation and basic functionality
            var git = new AuroraEngine.Common.Resource.Generics.GIT();

            Assert.IsNotNull(git);
            Assert.IsNotNull(git.Cameras);
            Assert.IsNotNull(git.Creatures);
            Assert.IsNotNull(git.Doors);
            Assert.IsNotNull(git.Encounters);
            Assert.IsNotNull(git.Placeables);
            Assert.IsNotNull(git.Sounds);
            Assert.IsNotNull(git.Stores);
            Assert.IsNotNull(git.Triggers);
            Assert.IsNotNull(git.Waypoints);

            // Test resource identifier iteration
            var identifiers = git.GetResourceIdentifiers();
            Assert.IsNotNull(identifiers);
        }

        [Test]
        public void TestLYTCreation()
        {
            // Test LYT class creation
            var lyt = new AuroraEngine.Common.Resource.Formats.LYT.LYT();

            Assert.IsNotNull(lyt);
            Assert.IsNotNull(lyt.Rooms);
            Assert.IsNotNull(lyt.Tracks);
            Assert.IsNotNull(lyt.Obstacles);
            Assert.IsNotNull(lyt.DoorHooks);
        }

        [Test]
        public void TestVISCreation()
        {
            // Test VIS class creation
            var vis = new AuroraEngine.Common.Resource.Formats.VIS.VIS();

            Assert.IsNotNull(vis);
            var rooms = vis.AllRooms();
            Assert.IsNotNull(rooms);

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
            var utc = new AuroraEngine.Common.Resource.Generics.UTC();

            Assert.IsNotNull(utc);
            Assert.AreEqual(AuroraEngine.Common.Resource.Generics.UTC.BinaryType, ResourceType.UTC);
        }

        [Test]
        public void TestUTDCreation()
        {
            // Test UTD class creation
            var utd = new AuroraEngine.Common.Resource.Generics.UTD();

            Assert.IsNotNull(utd);
            Assert.AreEqual(AuroraEngine.Common.Resource.Generics.UTD.BinaryType, ResourceType.UTD);
        }

        [Test]
        public void TestUTPCreation()
        {
            // Test UTP class creation
            var utp = new AuroraEngine.Common.Resource.Generics.UTP();

            Assert.IsNotNull(utp);
            Assert.AreEqual(AuroraEngine.Common.Resource.Generics.UTP.BinaryType, ResourceType.UTP);
        }

        [Test]
        public void TestArchiveResourceCreation()
        {
            // Test ArchiveResource creation
            var resRef = new ResRef("testresource");
            var resType = ResourceType.GIT;
            var data = new byte[] { 1, 2, 3, 4 };

            var archiveResource = new ArchiveResource(resRef, resType, data);

            Assert.IsNotNull(archiveResource);
            Assert.AreEqual(resRef, archiveResource.ResRef);
            Assert.AreEqual(resType, archiveResource.ResType);
            Assert.AreEqual(data, archiveResource.Data);
        }

        [Test]
        public void TestResourceAutoLoad()
        {
            // Test ResourceAuto loading with null data
            var result = ResourceAuto.LoadResource(null, ResourceType.GIT);
            Assert.IsNull(result);

            // Test with empty data
            result = ResourceAuto.LoadResource(new byte[0], ResourceType.GIT);
            Assert.IsNull(result);
        }

        [Test]
        public void TestSalvageValidation()
        {
            // Test Salvage.ValidateResourceFile with null
            var result = Salvage.ValidateResourceFile(null);
            Assert.IsFalse(result);
        }
    }
}
