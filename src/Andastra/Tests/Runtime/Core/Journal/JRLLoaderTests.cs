using System;
using System.Collections.Generic;
using Xunit;
using FluentAssertions;
using Andastra.Parsing;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TLK;
using Andastra.Runtime.Core.Journal;
using Moq;

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
            byte[] jrlData = JRLHelper.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
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
            _mockResourceManager.Verify(m => m.LookupResource(jrlResRef, ResourceType.JRL), Times.Once);
        }

        [Fact]
        public void LoadJRL_WithInvalidResRef_ReturnsNull()
        {
            // Arrange
            string jrlResRef = "nonexistent";

            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
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
            byte[] jrlData = JRLHelper.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
                .Returns(resourceResult);

            // Create mock TLK for text resolution
            var mockTlk = new Mock<TLK>();
            mockTlk.Setup(t => t.GetString(1)).Returns("Test quest entry text");
            _jrlLoader.SetTalkTables(mockTlk.Object);

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
            byte[] jrlData = JRLHelper.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
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
            byte[] jrlData = JRLHelper.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
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
            byte[] jrlData = JRLHelper.BytesJrl(globalJrl);

            _mockResourceManager
                .Setup(m => m.LookupResource("global", ResourceType.JRL))
                .Returns(new ResourceResult { Data = jrlData });

            // Create mock TLK for text resolution
            var mockTlk = new Mock<TLK>();
            mockTlk.Setup(t => t.GetString(1)).Returns("Global quest entry text");
            _jrlLoader.SetTalkTables(mockTlk.Object);

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
            byte[] jrlData = JRLHelper.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
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
            byte[] jrlData = JRLHelper.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
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
            byte[] jrlData = JRLHelper.BytesJrl(testJrl);

            var resourceResult = new ResourceResult(jrlResRef, ResourceType.JRL, "", jrlData);
            _mockResourceManager
                .Setup(m => m.LookupResource(jrlResRef, ResourceType.JRL))
                .Returns(resourceResult);

            _jrlLoader.LoadJRL(jrlResRef);

            // Act
            _jrlLoader.ClearCache();

            // Assert
            // Next load should call LookupResource again
            _jrlLoader.LoadJRL(jrlResRef);
            _mockResourceManager.Verify(m => m.LookupResource(jrlResRef, ResourceType.JRL), Times.Exactly(2));
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
    }
}

