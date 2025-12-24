using System;
using System.IO;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Reader;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{
    /// <summary>
    /// Tests for ConfigReader INI comment parsing, specifically for comments that look like section headers.
    /// This test reproduces the exact bug where a comment line like ";[K1] Legends - Ajunta Pall's Blade v1.0.2b"
    /// causes a ParsingException.
    /// </summary>
    public class ConfigReaderCommentParsingTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _iniFilePath;

        public ConfigReaderCommentParsingTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            _iniFilePath = Path.Combine(_tempDir, "main_install.ini");
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FromFilePath_WithCommentLineLookingLikeSectionHeader_ShouldNotThrow()
        {
            // Arrange - Create INI file with the exact problematic content from the bug report
            // The error shows: "Couldn't parse the line: '[K1] Legends - Ajunta Pall's Blade v1.0.2b'"
            // while parsing line number 1 with value ';[K1] Legends - Ajunta Pall's Blade v1.0.2b'
            string iniContent = @";[K1] Legends - Ajunta Pall's Blade v1.0.2b

[Settings]
ModName=Test Mod
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert - Should not throw an exception
            // Before the fix, this would throw InvalidOperationException with inner ParsingException
            Action act = () => ConfigReader.FromFilePath(_iniFilePath, logger);
            act.Should().NotThrow("because comment lines starting with ; should be ignored");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FromFilePath_WithCommentLineLookingLikeSectionHeader_ShouldParseSuccessfully()
        {
            // Arrange - Create INI file with comment that looks like section header
            string iniContent = @";[K1] Legends - Ajunta Pall's Blade v1.0.2b

[Settings]
ModName=Test Mod
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act
            ConfigReader reader = ConfigReader.FromFilePath(_iniFilePath, logger);

            // Assert - Should successfully parse and create ConfigReader
            reader.Should().NotBeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FromFilePath_WithMultipleCommentLinesLookingLikeSectionHeaders_ShouldNotThrow()
        {
            // Arrange - Multiple comment lines that look like section headers
            string iniContent = @";[K1] Legends - Ajunta Pall's Blade v1.0.2b
;[K2] Another mod name
;[TSL] Some other mod

[Settings]
ModName=Test Mod
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert
            Action act = () => ConfigReader.FromFilePath(_iniFilePath, logger);
            act.Should().NotThrow("because all comment lines should be ignored");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FromFilePath_WithCommentLineContainingBrackets_ShouldNotThrow()
        {
            // Arrange - Comment line with brackets that could be mistaken for section header
            string iniContent = @"; This is a comment with [brackets] in it
;[K1] This looks like a section but is a comment

[Settings]
ModName=Test Mod
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert
            Action act = () => ConfigReader.FromFilePath(_iniFilePath, logger);
            act.Should().NotThrow("because comment lines should be completely ignored");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void FromFilePath_WithCommentAfterSectionHeader_ShouldNotThrow()
        {
            // Arrange - Valid section header with comment after it
            string iniContent = @"[Settings] ; This is a comment after a section header
ModName=Test Mod
";
            File.WriteAllText(_iniFilePath, iniContent);

            var logger = new PatchLogger();

            // Act & Assert
            Action act = () => ConfigReader.FromFilePath(_iniFilePath, logger);
            act.Should().NotThrow("because inline comments should be handled correctly");
        }
    }
}

