using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Runtime.Core.Journal;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andastra.Tests.Runtime.Core.Journal
{
    /// <summary>
    /// Tests for JRLLoader quest entry text lookup.
    /// </summary>
    public class JRLLoaderTests
    {
        private Mock<Installation> _mockInstallation;
        private Mock<InstallationResourceManager> _mockResourceManager;
        private JRLLoader _jrlLoader;

        public JRLLoaderTests()
        {
            _mockInstallation = new Mock<Installation>("");
            _mockResourceManager = new Mock<InstallationResourceManager>(_mockInstallation.Object);
            _mockInstallation.Setup(i => i.Resources).Returns(_mockResourceManager.Object);
            _jrlLoader = new JRLLoader(_mockInstallation.Object);
        }

        [Fact]
        public void LoadJRL_WithValidResRef_LoadsAndCachesJRL()
        {
            // Arrange
            string jrlResRef = "test_quest";
            JRL testJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns(resourceResult);

            // Act
            JRL loadedJrl = _jrlLoader.LoadJRL(jrlResRef);

            // Assert
            loadedJrl.Should().NotBeNull();
            loadedJrl.Quests.Count.Should().Be(1);
            loadedJrl.Quests[0].Tag.Should().Be("test_quest");

            // Verify caching - second call should use cache
            JRL cachedJrl = _jrlLoader.LoadJRL(jrlResRef);
            cachedJrl.Should().BeSameAs(loadedJrl);
            _mockResourceManager.Verify(m => m.LookupResource(jrlResRef, ResourceType.JRL, It.IsAny<SearchLocation[]>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public void LoadJRL_WithInvalidResRef_ReturnsNull()
        {
            // Arrange
            string jrlResRef = "nonexistent";

            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns((ResourceResult)null);

            // Act
            JRL loadedJrl = _jrlLoader.LoadJRL(jrlResRef);

            // Assert
            loadedJrl.Should().BeNull();
        }

        [Fact]
        public void GetQuestEntryText_WithValidQuestAndEntry_ReturnsText()
        {
            // Arrange
            string jrlResRef = "test_quest";
            string questTag = "test_quest";
            int entryId = 0;
            JRL testJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns(resourceResult);

            // Create mock TLK for text resolution
            TLK testTlk = CreateTestTLK("Test quest entry text");
            _jrlLoader.SetTalkTables(testTlk);

            // Act
            string entryText = _jrlLoader.GetQuestEntryText(questTag, entryId, jrlResRef);

            // Assert
            entryText.Should().Be("Test quest entry text");
        }

        [Fact]
        public void GetQuestEntryText_WithInvalidQuestTag_ReturnsNull()
        {
            // Arrange
            string jrlResRef = "test_quest";
            string questTag = "nonexistent_quest";
            int entryId = 0;
            JRL testJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns(resourceResult);

            // Act
            string entryText = _jrlLoader.GetQuestEntryText(questTag, entryId, jrlResRef);

            // Assert
            entryText.Should().BeNull();
        }

        [Fact]
        public void GetQuestEntryText_WithInvalidEntryId_ReturnsNull()
        {
            // Arrange
            string jrlResRef = "test_quest";
            string questTag = "test_quest";
            int entryId = 999; // Invalid entry ID
            JRL testJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns(resourceResult);

            // Act
            string entryText = _jrlLoader.GetQuestEntryText(questTag, entryId, jrlResRef);

            // Assert
            entryText.Should().BeNull();
        }

        [Fact]
        public void GetQuestEntryTextFromGlobal_WithValidQuest_ReturnsText()
        {
            // Arrange
            string questTag = "test_quest";
            int entryId = 0;
            JRL globalJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(globalJrl);

            _mockResourceManager
                .Setup(m => m.LookupResource("global", ResourceType.JRL, null, null))
                .Returns(new ResourceResult("global", ResourceType.JRL, "test.jrl", jrlData));

            // Create mock TLK for text resolution
            TLK testTlk = CreateTestTLK("Global quest entry text");
            _jrlLoader.SetTalkTables(testTlk);

            // Act
            string entryText = _jrlLoader.GetQuestEntryTextFromGlobal(questTag, entryId);

            // Assert
            entryText.Should().Be("Global quest entry text");
        }

        [Fact]
        public void GetQuestByTag_WithValidTag_ReturnsQuest()
        {
            // Arrange
            string jrlResRef = "test_quest";
            string questTag = "test_quest";
            JRL testJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns(resourceResult);

            // Act
            JRLQuest quest = _jrlLoader.GetQuestByTag(questTag, jrlResRef);

            // Assert
            quest.Should().NotBeNull();
            quest.Tag.Should().Be(questTag);
        }

        [Fact]
        public void GetQuestByTag_WithInvalidTag_ReturnsNull()
        {
            // Arrange
            string jrlResRef = "test_quest";
            string questTag = "nonexistent_quest";
            JRL testJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns(resourceResult);

            // Act
            JRLQuest quest = _jrlLoader.GetQuestByTag(questTag, jrlResRef);

            // Assert
            quest.Should().BeNull();
        }

        [Fact]
        public void ClearCache_RemovesAllCachedJRLs()
        {
            // Arrange
            string jrlResRef = "test_quest";
            JRL testJrl = CreateTestJRL();
            byte[] jrlData = JRLHelpers.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null))
                .Returns(resourceResult);

            _jrlLoader.LoadJRL(jrlResRef);

            // Act
            _jrlLoader.ClearCache();

            // Assert
            // Next load should call LookupResource again
            _jrlLoader.LoadJRL(jrlResRef);
            _mockResourceManager.Verify(m => m.LookupResource(jrlResRef, ResourceType.JRL, null, null), Times.Exactly(2));
        }

        /// <summary>
        /// Creates a test JRL with a single quest and entry.
        /// </summary>
        private JRL CreateTestJRL()
        {
            var jrl = new JRL();
            var quest = new JRLQuest
            {
                Tag = "test_quest",
                Name = LocalizedString.FromStringId(1),
                Entries = new List<JRLQuestEntry>
                {
                    new JRLQuestEntry
                    {
                        EntryId = 0,
                        Text = LocalizedString.FromStringId(1),
                        End = false,
                        XpPercentage = 1.0f
                    }
                }
            };
            jrl.Quests.Add(quest);
            return jrl;
        }

        /// <summary>
        /// Creates a test TLK with a single entry at index 1 containing the specified text.
        /// The TLK is resized to have at least 2 entries (indices 0 and 1), with entry 0 being empty
        /// and entry 1 containing the provided text.
        /// </summary>
        /// <param name="text">The text to store at entry index 1.</param>
        /// <returns>A TLK instance with the specified entry text at index 1.</returns>
        private TLK CreateTestTLK(string text)
        {
            var tlk = new TLK();
            // Resize to have at least 2 entries (indices 0 and 1)
            // Entry 0 will be empty, entry 1 will contain our text
            tlk.Resize(2);
            // Set entry 1 with the provided text
            // TLK.Resize creates empty entries, so we replace entry 1
            tlk.Replace(1, text, "");
            return tlk;
        }
    }
}

