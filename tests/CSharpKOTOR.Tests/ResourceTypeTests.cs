using NUnit.Framework;
using AuroraEngine.Common.Resources;

namespace AuroraEngine.Common.Tests
{
    [TestFixture]
    public class ResourceTypeTests
    {
        [Test]
        public void TestResourceTypeFromExtension()
        {
            // Test common resource type extensions
            ResourceType gitType = ResourceType.FromExtension(".git");
            Assert.IsNotNull(gitType);
            Assert.AreEqual("GIT", gitType.Name);

            ResourceType tlkType = ResourceType.FromExtension(".tlk");
            Assert.IsNotNull(tlkType);
            Assert.AreEqual("TLK", tlkType.Name);

            ResourceType erfType = ResourceType.FromExtension(".erf");
            Assert.IsNotNull(erfType);
            Assert.AreEqual("ERF", erfType.Name);

            ResourceType rimType = ResourceType.FromExtension(".rim");
            Assert.IsNotNull(rimType);
            Assert.AreEqual("RIM", rimType.Name);

            ResourceType ncsType = ResourceType.FromExtension(".ncs");
            Assert.IsNotNull(ncsType);
            Assert.AreEqual("NCS", ncsType.Name);

            ResourceType utcType = ResourceType.FromExtension(".utc");
            Assert.IsNotNull(utcType);
            Assert.AreEqual("UTC", utcType.Name);
            Assert.IsFalse(utcType.IsInvalid);

            // Test case insensitivity
            ResourceType upperCase = ResourceType.FromExtension(".GIT");
            Assert.AreEqual(gitType, upperCase);
        }

        [Test]
        public void TestResourceTypeFromId()
        {
            // Test getting resource types by ID
            ResourceType gitType = ResourceType.FromId(2023); // GIT type ID (corrected from Python source)
            Assert.IsNotNull(gitType);
            Assert.AreEqual("GIT", gitType.Name);
            Assert.IsFalse(gitType.IsInvalid);

            ResourceType tlkType = ResourceType.FromId(2018); // TLK type ID
            Assert.IsNotNull(tlkType);
            Assert.AreEqual("TLK", tlkType.Name);
            Assert.IsFalse(tlkType.IsInvalid);
        }

        [Test]
        public void TestResourceTypeProperties()
        {
            ResourceType gitType = ResourceType.FromExtension(".git");
            Assert.IsNotNull(gitType);
            Assert.IsFalse(gitType.IsInvalid);

            Assert.AreEqual(2023, gitType.TypeId); // Corrected from Python source
            Assert.AreEqual("git", gitType.Extension); // Extension is stored without leading dot
            Assert.AreEqual("gff", gitType.Contents); // Corrected from Python source
            Assert.AreEqual("Module Data", gitType.Category); // Corrected from Python source
        }

        [Test]
        public void TestInvalidResourceType()
        {
            // Test invalid extension - should return INVALID ResourceType, not null
            ResourceType invalidType = ResourceType.FromExtension(".invalid");
            Assert.IsNotNull(invalidType);
            Assert.IsTrue(invalidType.IsInvalid);

            // Test invalid ID - should return INVALID ResourceType, not null
            ResourceType invalidId = ResourceType.FromId(-1);
            Assert.IsNotNull(invalidId);
            Assert.IsTrue(invalidId.IsInvalid);

            // Test invalid large ID - should return INVALID ResourceType, not null
            ResourceType invalidLargeId = ResourceType.FromId(99999);
            Assert.IsNotNull(invalidLargeId);
            Assert.IsTrue(invalidLargeId.IsInvalid);
        }
    }
}
