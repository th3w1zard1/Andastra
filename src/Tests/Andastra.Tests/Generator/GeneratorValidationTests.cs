// Comprehensive tests for GeneratorValidation
// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:975-1104
using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing.TSLPatcher;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Generator
{
    /// <summary>
    /// Comprehensive tests for GeneratorValidation.
    /// Tests validation of INI filenames, installation paths, and tslpatchdata arguments.
    /// </summary>
    public class GeneratorValidationTests : IDisposable
    {
        private readonly string _tempDir;

        public GeneratorValidationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
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
        public void ValidateIniFilename_ShouldReturnDefault_WhenNull()
        {
            // Act
            var result = GeneratorValidation.ValidateIniFilename(null);

            // Assert
            result.Should().Be("changes.ini");
        }

        [Fact]
        public void ValidateIniFilename_ShouldReturnDefault_WhenEmpty()
        {
            // Act
            var result = GeneratorValidation.ValidateIniFilename("");

            // Assert
            result.Should().Be("changes.ini");
        }

        [Fact]
        public void ValidateIniFilename_ShouldReturnAsIs_WhenHasIniExtension()
        {
            // Act
            var result = GeneratorValidation.ValidateIniFilename("test.ini");

            // Assert
            result.Should().Be("test.ini");
        }

        [Fact]
        public void ValidateIniFilename_ShouldAddExtension_WhenNoExtension()
        {
            // Act
            var result = GeneratorValidation.ValidateIniFilename("test");

            // Assert
            result.Should().Be("test.ini");
        }

        [Fact]
        public void ValidateIniFilename_ShouldThrow_WhenContainsPathSeparator()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => GeneratorValidation.ValidateIniFilename("path/test.ini"));
            Assert.Throws<ArgumentException>(() => GeneratorValidation.ValidateIniFilename("path\\test.ini"));
        }

        [Fact]
        public void ValidateIniFilename_ShouldThrow_WhenHasWrongExtension()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() => GeneratorValidation.ValidateIniFilename("test.txt"));
        }

        [Fact]
        public void ValidateIniFilename_ShouldHandleCaseInsensitive()
        {
            // Act
            var result1 = GeneratorValidation.ValidateIniFilename("test.INI");
            var result2 = GeneratorValidation.ValidateIniFilename("TEST.ini");

            // Assert
            result1.Should().Be("test.INI");
            result2.Should().Be("TEST.ini");
        }

        [Fact]
        public void ValidateInstallationPath_ShouldReturnFalse_WhenPathIsNull()
        {
            // Act
            var result = GeneratorValidation.ValidateInstallationPath(null);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateInstallationPath_ShouldReturnFalse_WhenPathIsEmpty()
        {
            // Act
            var result = GeneratorValidation.ValidateInstallationPath("");

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateInstallationPath_ShouldReturnFalse_WhenPathDoesNotExist()
        {
            // Act
            var result = GeneratorValidation.ValidateInstallationPath(Path.Combine(_tempDir, "nonexistent"));

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void ValidateTslpatchdataArguments_ShouldReturnNull_WhenBothArgumentsNull()
        {
            // Act
            var result = GeneratorValidation.ValidateTslpatchdataArguments(null, null, null);

            // Assert
            result.validatedIni.Should().BeNull();
            result.tslpatchdataPath.Should().BeNull();
        }

        [Fact]
        public void ValidateTslpatchdataArguments_ShouldThrow_WhenIniProvidedButNotTslpatchdata()
        {
            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                GeneratorValidation.ValidateTslpatchdataArguments("test.ini", null, null));
        }

        [Fact]
        public void ValidateTslpatchdataArguments_ShouldSetDefaultIni_WhenTslpatchdataProvidedButNotIni()
        {
            // Arrange - Create a valid KOTOR installation (requires swkotor.exe or swkotor2.exe or chitin.key)
            var installationPath = Path.Combine(_tempDir, "kotor_install");
            Directory.CreateDirectory(installationPath);
            // Create swkotor.exe to make it a valid KOTOR installation
            File.WriteAllText(Path.Combine(installationPath, "swkotor.exe"), "");

            var tslpatchdata = Path.Combine(_tempDir, "tslpatchdata");
            Directory.CreateDirectory(tslpatchdata);
            var paths = new List<object> { installationPath };

            // Act
            var result = GeneratorValidation.ValidateTslpatchdataArguments(null, tslpatchdata, paths);

            // Assert
            result.validatedIni.Should().Be("changes.ini");
            result.tslpatchdataPath.Should().NotBeNull();
            result.tslpatchdataPath.FullName.Should().Be(Path.GetFullPath(tslpatchdata));
        }

        [Fact]
        public void ValidateTslpatchdataArguments_ShouldNormalizeTslpatchdataPath()
        {
            // Arrange - Create a valid KOTOR installation to satisfy validation requirement
            var installationPath = Path.Combine(_tempDir, "kotor_install");
            Directory.CreateDirectory(installationPath);
            // Create swkotor.exe to make it a valid KOTOR installation
            File.WriteAllText(Path.Combine(installationPath, "swkotor.exe"), "");

            // Create a base path that is NOT named "tslpatchdata" - this should be normalized
            var basePath = Path.Combine(_tempDir, "base");
            Directory.CreateDirectory(basePath);
            var tslpatchdata = basePath; // Not named "tslpatchdata", should be normalized to basePath/tslpatchdata

            // Include both the installation path and the tslpatchdata path in the paths list
            // The installation path satisfies the validation requirement
            var paths = new List<object> { installationPath, tslpatchdata };

            // Act
            var result = GeneratorValidation.ValidateTslpatchdataArguments("test.ini", tslpatchdata, paths);

            // Assert - The tslpatchdata path should be normalized to include "tslpatchdata" directory
            result.validatedIni.Should().Be("test.ini");
            result.tslpatchdataPath.Should().NotBeNull();
            // The normalized path should be basePath/tslpatchdata
            result.tslpatchdataPath.FullName.Should().Be(Path.GetFullPath(Path.Combine(basePath, "tslpatchdata")));
            result.tslpatchdataPath.Name.Should().Be("tslpatchdata");
        }

        [Fact]
        public void ValidateTslpatchdataArguments_ShouldThrow_WhenNoValidInstallation()
        {
            // Arrange
            var tslpatchdata = Path.Combine(_tempDir, "tslpatchdata");
            Directory.CreateDirectory(tslpatchdata);
            var paths = new List<object> { "not_an_installation" };

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                GeneratorValidation.ValidateTslpatchdataArguments("test.ini", tslpatchdata, paths));
        }
    }
}

