using System;
using System.Collections.Generic;
using System.IO;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using File = CSharpKOTOR.Formats.NCS.NCSDecomp.File;
using FluentAssertions;
using Xunit;

namespace NCSDecomp.Tests
{
    public class NWScriptLocatorTests
    {
        [Fact]
        public void FindNWScriptFile_WithSettingsPath_ShouldReturnFile()
        {
            // Arrange
            Settings settings = new Settings();
            string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".nss");
            System.IO.File.WriteAllText(tempPath, "// 0\nvoid Test();\n");
            settings.SetProperty("NWScript Path", tempPath);

            try
            {
                // Act
                File result = NWScriptLocator.FindNWScriptFile(NWScriptLocator.GameType.K1, settings);

                // Assert
                result.Should().NotBeNull();
                result.GetAbsolutePath().Should().Be(tempPath);
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                {
                    System.IO.File.Delete(tempPath);
                }
            }
        }

        [Fact]
        public void FindNWScriptFile_WithInvalidSettingsPath_ShouldReturnNull()
        {
            // Arrange
            Settings settings = new Settings();
            settings.SetProperty("NWScript Path", Path.Combine(Path.GetTempPath(), "nonexistent.nss"));

            // Act
            File result = NWScriptLocator.FindNWScriptFile(NWScriptLocator.GameType.K1, settings);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetCandidatePaths_ShouldReturnList()
        {
            // Act
            List<string> paths = NWScriptLocator.GetCandidatePaths(NWScriptLocator.GameType.K1);

            // Assert
            paths.Should().NotBeNull();
            paths.Should().NotBeEmpty();
        }

        [Fact]
        public void GetCandidatePaths_ForK1_ShouldContainK1Paths()
        {
            // Act
            List<string> paths = NWScriptLocator.GetCandidatePaths(NWScriptLocator.GameType.K1);

            // Assert
            paths.Should().Contain(path => path.Contains("k1") || path.Contains("nwscript.nss"));
        }

        [Fact]
        public void GetCandidatePaths_ForTSL_ShouldContainK2Paths()
        {
            // Act
            List<string> paths = NWScriptLocator.GetCandidatePaths(NWScriptLocator.GameType.TSL);

            // Assert
            paths.Should().Contain(path => path.Contains("k2") || path.Contains("nwscript.nss"));
        }
    }
}

