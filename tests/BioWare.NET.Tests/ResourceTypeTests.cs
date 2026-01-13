using NUnit.Framework;
using NUnit.Framework.Legacy;
using Andastra.Parsing.Resource;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Andastra.Parsing.Tests
{
    [TestFixture]
    public class ResourceTypeTests
    {
        [Test]
        public void TestResourceTypeFromExtension()
        {
            // Test common resource type extensions
            ResourceType gitType = ResourceType.FromExtension(".git");
            Assert.IsTrue(gitType != null);
            Assert.IsTrue(gitType.Name == "GIT");

            ResourceType tlkType = ResourceType.FromExtension(".tlk");
            Assert.IsTrue(tlkType != null);
            Assert.IsTrue(tlkType.Name == "TLK");

            ResourceType erfType = ResourceType.FromExtension(".erf");
            Assert.IsTrue(erfType != null);
            Assert.IsTrue(erfType.Name == "ERF");

            ResourceType rimType = ResourceType.FromExtension(".rim");
            Assert.IsTrue(rimType != null);
            Assert.IsTrue(rimType.Name == "RIM");

            ResourceType ncsType = ResourceType.FromExtension(".ncs");
            Assert.IsTrue(ncsType != null);
            Assert.IsTrue(ncsType.Name == "NCS");

            ResourceType utcType = ResourceType.FromExtension(".utc");
            Assert.IsTrue(utcType != null);
            Assert.IsTrue(utcType.Name == "UTC");
            Assert.IsFalse(utcType.IsInvalid);

            // Test case insensitivity
            ResourceType upperCase = ResourceType.FromExtension(".GIT");
            Assert.IsTrue(gitType == upperCase);
        }

        [Test]
        public void TestResourceTypeFromId()
        {
            // Test getting resource types by ID
            ResourceType gitType = ResourceType.FromId(2023); // GIT type ID (corrected from Python source)
            Assert.IsTrue(gitType != null);
            Assert.IsTrue(gitType.Name == "GIT");
            Assert.IsFalse(gitType.IsInvalid);

            ResourceType tlkType = ResourceType.FromId(2018); // TLK type ID
            Assert.IsTrue(tlkType != null);
            Assert.IsTrue(tlkType.Name == "TLK");
            Assert.IsFalse(tlkType.IsInvalid);
        }

        [Test]
        public void TestResourceTypeProperties()
        {
            ResourceType gitType = ResourceType.FromExtension(".git");
            Assert.IsTrue(gitType != null);
            Assert.IsFalse(gitType.IsInvalid);

            Assert.IsTrue(gitType.TypeId == 2023); // Corrected from Python source
            Assert.IsTrue(gitType.Extension == "git"); // Extension is stored without leading dot
            Assert.IsTrue(gitType.Contents == "gff"); // Corrected from Python source
            Assert.IsTrue(gitType.Category == "Module Data"); // Corrected from Python source
        }

        [Test]
        public void TestInvalidResourceType()
        {
            // Test invalid extension - should return INVALID ResourceType, not null
            ResourceType invalidType = ResourceType.FromExtension(".invalid");
            Assert.IsTrue(invalidType != null);
            Assert.IsTrue(invalidType.IsInvalid);

            // Test invalid ID - should return INVALID ResourceType, not null
            ResourceType invalidId = ResourceType.FromId(-1);
            Assert.IsTrue(invalidId != null);
            Assert.IsTrue(invalidId.IsInvalid);

            // Test invalid large ID - should return INVALID ResourceType, not null
            ResourceType invalidLargeId = ResourceType.FromId(99999);
            Assert.IsTrue(invalidLargeId != null);
            Assert.IsTrue(invalidLargeId.IsInvalid);
        }
    }
}
