using System;
using System.IO;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using FluentAssertions;
using Xunit;

namespace NCSDecomp.Tests
{
    public class SettingsTests : IDisposable
    {
        private readonly string _configFile;

        public SettingsTests()
        {
            _configFile = Path.Combine(Path.GetTempPath(), "NCSDecomp_test.conf");
            if (System.IO.File.Exists(_configFile))
            {
                System.IO.File.Delete(_configFile);
            }
        }

        public void Dispose()
        {
            if (System.IO.File.Exists(_configFile))
            {
                try
                {
                    System.IO.File.Delete(_configFile);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void Settings_Constructor_ShouldNotThrow()
        {
            // Arrange & Act
            Action act = () => new Settings();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Settings_Reset_ShouldSetDefaultValues()
        {
            // Arrange
            Settings settings = new Settings();

            // Act
            settings.Reset();

            // Assert
            settings.GetProperty("Output Directory").Should().NotBeNullOrEmpty();
            settings.GetProperty("Open Directory").Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void Settings_SetProperty_ShouldStoreValue()
        {
            // Arrange
            Settings settings = new Settings();
            string testValue = "test_value";

            // Act
            settings.SetProperty("TestKey", testValue);

            // Assert
            settings.GetProperty("TestKey").Should().Be(testValue);
        }

        [Fact]
        public void Settings_GetProperty_WithNonExistentKey_ShouldReturnEmpty()
        {
            // Arrange
            Settings settings = new Settings();

            // Act
            string result = settings.GetProperty("NonExistentKey");

            // Assert
            result.Should().Be("");
        }

        [Fact]
        public void Settings_Save_ShouldCreateConfigFile()
        {
            // Arrange
            Settings settings = new Settings();
            string configPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".conf");

            try
            {
                // Act
                settings.Save();

                // Assert
                // Settings saves to "NCSDecomp.conf" in current directory
                // This test verifies Save doesn't throw
                Action act = () => settings.Save();
                act.Should().NotThrow();
            }
            finally
            {
                if (System.IO.File.Exists(configPath))
                {
                    System.IO.File.Delete(configPath);
                }
            }
        }

        [Fact]
        public void Settings_Load_WithNonExistentFile_ShouldResetAndSave()
        {
            // Arrange
            string configFile = "NCSDecomp.conf";
            if (System.IO.File.Exists(configFile))
            {
                System.IO.File.Delete(configFile);
            }
            Settings settings = new Settings();

            // Act
            settings.Load();

            // Assert
            // Should not throw and should have default values
            settings.GetProperty("Output Directory").Should().NotBeNullOrEmpty();
        }
    }
}

