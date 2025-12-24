using Andastra.Parsing.Namespaces;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Namespaces
{

    /// <summary>
    /// Tests for PatcherNamespace functionality
    /// </summary>
    public class PatcherNamespaceTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Constructor_ShouldInitializeWithDefaults()
        {
            // Act
            var ns = new PatcherNamespace();

            // Assert
            ns.NamespaceId.Should().BeEmpty();
            ns.IniFilename.Should().Be(PatcherNamespace.DefaultIniFilename);
            ns.InfoFilename.Should().Be(PatcherNamespace.DefaultInfoFilename);
            ns.DataFolderPath.Should().BeEmpty();
            ns.Name.Should().BeEmpty();
            ns.Description.Should().BeEmpty();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void NamespaceId_ShouldBeSettable()
        {
            // Arrange
            var ns = new PatcherNamespace();

            // Act
            ns.NamespaceId = "mod1";

            // Assert
            ns.NamespaceId.Should().Be("mod1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void IniFilename_ShouldBeSettable()
        {
            // Arrange
            var ns = new PatcherNamespace();

            // Act
            ns.IniFilename = "custom.ini";

            // Assert
            ns.IniFilename.Should().Be("custom.ini");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void InfoFilename_ShouldBeSettable()
        {
            // Arrange
            var ns = new PatcherNamespace();

            // Act
            ns.InfoFilename = "readme.rtf";

            // Assert
            ns.InfoFilename.Should().Be("readme.rtf");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void DataFolderPath_ShouldBeSettable()
        {
            // Arrange
            var ns = new PatcherNamespace();

            // Act
            ns.DataFolderPath = "C:\\mods\\test";

            // Assert
            ns.DataFolderPath.Should().Be("C:\\mods\\test");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangesFilePath_ShouldCombineFolderAndIniFilename()
        {
            // Arrange
            var ns = new PatcherNamespace
            {
                DataFolderPath = "C:\\mods\\test",
                IniFilename = "changes.ini"
            };

            // Act
            string path = ns.ChangesFilePath();

            // Assert
            path.Should().Be("C:\\mods\\test\\changes.ini");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void RtfFilePath_ShouldCombineFolderAndInfoFilename()
        {
            // Arrange
            var ns = new PatcherNamespace
            {
                DataFolderPath = "C:\\mods\\test",
                InfoFilename = "info.rtf"
            };

            // Act
            string path = ns.RtfFilePath();

            // Assert
            path.Should().Be("C:\\mods\\test\\info.rtf");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Name_ShouldBeSettable()
        {
            // Arrange
            var ns = new PatcherNamespace();

            // Act
            ns.Name = "Test Mod";

            // Assert
            ns.Name.Should().Be("Test Mod");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Description_ShouldBeSettable()
        {
            // Arrange
            var ns = new PatcherNamespace();

            // Act
            ns.Description = "This is a test mod";

            // Assert
            ns.Description.Should().Be("This is a test mod");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ToString_ShouldReturnFormattedString()
        {
            // Arrange
            var ns = new PatcherNamespace
            {
                NamespaceId = "mod1",
                Name = "Test Mod"
            };

            // Act
            string result = ns.ToString();

            // Assert
            result.Should().Contain("mod1");
            result.Should().Contain("Test Mod");
        }

    }
}

