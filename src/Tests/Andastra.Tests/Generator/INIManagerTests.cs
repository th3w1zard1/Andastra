// Comprehensive tests for INIManager
// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/ini_manager.py
using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing.TSLPatcher;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Generator
{
    /// <summary>
    /// Comprehensive tests for INIManager.
    /// Tests INI file loading, section management, merging, and writing.
    /// </summary>
    public class INIManagerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _iniPath;

        public INIManagerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _iniPath = Path.Combine(_tempDir, "test.ini");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void Constructor_ShouldInitialize_WhenPathProvided()
        {
            // Act
            var manager = new INIManager(_iniPath);

            // Assert
            manager.Should().NotBeNull();
        }

        [Fact]
        public void Load_ShouldCreateEmptyConfig_WhenFileDoesNotExist()
        {
            // Arrange
            var manager = new INIManager(_iniPath);

            // Act
            manager.Load();

            // Assert
            var section = manager.GetSection("TestSection");
            section.Should().BeNull();
        }

        [Fact]
        public void Load_ShouldLoadExistingFile()
        {
            // Arrange
            File.WriteAllText(_iniPath, "[Section1]\nkey1=value1\nkey2=value2\n");
            var manager = new INIManager(_iniPath);

            // Act
            manager.Load();

            // Assert
            var section = manager.GetSection("Section1");
            section.Should().NotBeNull();
            section["key1"].Should().Be("value1");
            section["key2"].Should().Be("value2");
        }

        [Fact]
        public void InitializeSections_ShouldCreateSections()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            var sections = new List<string> { "[Section1]", "[Section2]" };

            // Act
            manager.InitializeSections(sections);

            // Assert
            manager.SectionExists("Section1").Should().BeTrue();
            manager.SectionExists("Section2").Should().BeTrue();
        }

        [Fact]
        public void InitializeSections_ShouldHandleBrackets()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            var sections = new List<string> { "[Section1]", "Section2" };

            // Act
            manager.InitializeSections(sections);

            // Assert
            manager.SectionExists("Section1").Should().BeTrue();
            manager.SectionExists("Section2").Should().BeTrue();
        }

        [Fact]
        public void MergeSectionLines_ShouldAddNewKeys()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            var lines = new List<string> { "key1=value1", "key2=value2" };

            // Act
            manager.MergeSectionLines("TestSection", lines);

            // Assert
            var section = manager.GetSection("TestSection");
            section.Should().NotBeNull();
            section["key1"].Should().Be("value1");
            section["key2"].Should().Be("value2");
        }

        [Fact]
        public void MergeSectionLines_ShouldSkipComments()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            var lines = new List<string> { "; comment", "# another comment", "key1=value1" };

            // Act
            manager.MergeSectionLines("TestSection", lines);

            // Assert
            var section = manager.GetSection("TestSection");
            section.Should().NotBeNull();
            section.ContainsKey("key1").Should().BeTrue();
            section.ContainsKey("; comment").Should().BeFalse();
            section.ContainsKey("# another comment").Should().BeFalse();
        }

        [Fact]
        public void MergeSectionLines_ShouldRemoveQuotes()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            var lines = new List<string> { "key1=\"quoted value\"", "key2='single quoted'" };

            // Act
            manager.MergeSectionLines("TestSection", lines);

            // Assert
            var section = manager.GetSection("TestSection");
            section["key1"].Should().Be("quoted value");
            section["key2"].Should().Be("single quoted");
        }

        [Fact]
        public void MergeSectionLines_ShouldConvertToList_WhenDuplicateKeys()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            var lines1 = new List<string> { "key1=value1" };
            var lines2 = new List<string> { "key1=value2" };

            // Act
            manager.MergeSectionLines("TestSection", lines1);
            manager.MergeSectionLines("TestSection", lines2);

            // Assert
            var section = manager.GetSection("TestSection");
            var value = section["key1"];
            value.Should().BeOfType<List<string>>();
            var list = value as List<string>;
            list.Should().Contain("value1");
            list.Should().Contain("value2");
        }

        [Fact]
        public void MergeSectionsFromSerializer_ShouldParseMultipleSections()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            var lines = new List<string>
            {
                "[Section1]",
                "key1=value1",
                "[Section2]",
                "key2=value2"
            };

            // Act
            manager.MergeSectionsFromSerializer(lines);

            // Assert
            var section1 = manager.GetSection("Section1");
            section1.Should().NotBeNull();
            section1["key1"].Should().Be("value1");

            var section2 = manager.GetSection("Section2");
            section2.Should().NotBeNull();
            section2["key2"].Should().Be("value2");
        }

        [Fact]
        public void Write_ShouldCreateFile_WhenConfigExists()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            manager.MergeSectionLines("TestSection", new List<string> { "key1=value1" });

            // Act
            manager.Write();

            // Assert
            File.Exists(_iniPath).Should().BeTrue();
            var content = File.ReadAllText(_iniPath);
            content.Should().Contain("[TestSection]");
            content.Should().Contain("key1=value1");
        }

        [Fact]
        public void Write_ShouldNotCreateFile_WhenConfigIsNull()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            // Don't call Load()

            // Act
            manager.Write();

            // Assert
            File.Exists(_iniPath).Should().BeFalse();
        }

        [Fact]
        public void GetSection_ShouldReturnNull_WhenSectionDoesNotExist()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();

            // Act
            var section = manager.GetSection("NonExistent");

            // Assert
            section.Should().BeNull();
        }

        [Fact]
        public void GetSection_ShouldReturnNull_WhenConfigNotLoaded()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            // Don't call Load()

            // Act
            var section = manager.GetSection("TestSection");

            // Assert
            section.Should().BeNull();
        }

        [Fact]
        public void SectionExists_ShouldReturnFalse_WhenSectionDoesNotExist()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();

            // Act
            var exists = manager.SectionExists("NonExistent");

            // Assert
            exists.Should().BeFalse();
        }

        [Fact]
        public void SectionExists_ShouldReturnTrue_WhenSectionExists()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            manager.MergeSectionLines("TestSection", new List<string> { "key1=value1" });

            // Act
            var exists = manager.SectionExists("TestSection");

            // Assert
            exists.Should().BeTrue();
        }

        [Fact]
        public void Write_ShouldHandleListValues()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            manager.MergeSectionLines("TestSection", new List<string> { "key1=value1" });
            manager.MergeSectionLines("TestSection", new List<string> { "key1=value2" });

            // Act
            manager.Write();

            // Assert
            File.Exists(_iniPath).Should().BeTrue();
            var content = File.ReadAllText(_iniPath);
            content.Should().Contain("key1=value1");
            content.Should().Contain("key1=value2");
        }

        [Fact]
        public void Write_ShouldQuoteValues_WhenContainSpaces()
        {
            // Arrange
            var manager = new INIManager(_iniPath);
            manager.Load();
            manager.MergeSectionLines("TestSection", new List<string> { "key1=value with spaces" });

            // Act
            manager.Write();

            // Assert
            File.Exists(_iniPath).Should().BeTrue();
            var content = File.ReadAllText(_iniPath);
            content.Should().Contain("key1=\"value with spaces\"");
        }
    }
}

