using NUnit.Framework;
using Andastra.Parsing.Resource;
using static NUnit.Framework.Assert;

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
            IsNotNull(gitType);
            AreEqual("GIT", gitType.Name);

            ResourceType tlkType = ResourceType.FromExtension(".tlk");
            IsNotNull(tlkType);
            AreEqual("TLK", tlkType.Name);

            ResourceType erfType = ResourceType.FromExtension(".erf");
            IsNotNull(erfType);
            AreEqual("ERF", erfType.Name);

            ResourceType rimType = ResourceType.FromExtension(".rim");
            IsNotNull(rimType);
            AreEqual("RIM", rimType.Name);

            ResourceType ncsType = ResourceType.FromExtension(".ncs");
            IsNotNull(ncsType);
            AreEqual("NCS", ncsType.Name);

            ResourceType utcType = ResourceType.FromExtension(".utc");
            IsNotNull(utcType);
            AreEqual("UTC", utcType.Name);
            IsFalse(utcType.IsInvalid);

            // Test case insensitivity
            ResourceType upperCase = ResourceType.FromExtension(".GIT");
            AreEqual(gitType, upperCase);
        }

        [Test]
        public void TestResourceTypeFromId()
        {
            // Test getting resource types by ID
            ResourceType gitType = ResourceType.FromId(2023); // GIT type ID (corrected from Python source)
            IsNotNull(gitType);
            AreEqual("GIT", gitType.Name);
            IsFalse(gitType.IsInvalid);

            ResourceType tlkType = ResourceType.FromId(2018); // TLK type ID
            IsNotNull(tlkType);
            AreEqual("TLK", tlkType.Name);
            IsFalse(tlkType.IsInvalid);
        }

        [Test]
        public void TestResourceTypeProperties()
        {
            ResourceType gitType = ResourceType.FromExtension(".git");
            IsNotNull(gitType);
            IsFalse(gitType.IsInvalid);

            AreEqual(2023, gitType.TypeId); // Corrected from Python source
            AreEqual("git", gitType.Extension); // Extension is stored without leading dot
            AreEqual("gff", gitType.Contents); // Corrected from Python source
            AreEqual("Module Data", gitType.Category); // Corrected from Python source
        }

        [Test]
        public void TestInvalidResourceType()
        {
            // Test invalid extension - should return INVALID ResourceType, not null
            ResourceType invalidType = ResourceType.FromExtension(".invalid");
            IsNotNull(invalidType);
            IsTrue(invalidType.IsInvalid);

            // Test invalid ID - should return INVALID ResourceType, not null
            ResourceType invalidId = ResourceType.FromId(-1);
            IsNotNull(invalidId);
            IsTrue(invalidId.IsInvalid);

            // Test invalid large ID - should return INVALID ResourceType, not null
            ResourceType invalidLargeId = ResourceType.FromId(99999);
            IsNotNull(invalidLargeId);
            IsTrue(invalidLargeId.IsInvalid);
        }
    }
}
