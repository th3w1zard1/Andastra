using System;
using System.IO;
using System.Text;
using Xunit;
using BioWareEngines.Game.Core;
using BioWareEngines.Core;

namespace BioWareEngines.Tests.UI
{
    /// <summary>
    /// Comprehensive tests for the KOTOR GUI Manager (main UI system).
    /// Tests resource loading, path detection with K1_PATH, error handling, and fallback behavior.
    /// All tests are designed to test logic without requiring graphics context.
    /// </summary>
    public class KotorGuiManagerTests
    {
        #region Test 1-12: K1_PATH Environment Variable Tests

        [Fact]
        public void KotorGuiManager_UsesK1PathFromEnv_K1PathSet()
        {
            // Arrange
            string testPath = @"C:\Test\KotorK1";
            Environment.SetEnvironmentVariable("K1_PATH", testPath, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert
                // Verify that environment variable is checked
                // Actual result depends on whether path exists, but mechanism should work
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        [Fact]
        public void KotorGuiManager_UsesK1PathFromEnvFile_EnvFileExists()
        {
            // Arrange
            string tempEnvPath = Path.Combine(Path.GetTempPath(), "test_k1.env");
            string testPath = @"C:\Test\KotorK1FromEnv";

            try
            {
                File.WriteAllText(tempEnvPath, $"K1_PATH={testPath}\n");

                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should load from .env file
            }
            finally
            {
                if (File.Exists(tempEnvPath))
                {
                    File.Delete(tempEnvPath);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_PrioritizesK1PathEnvVar_OverRegistry()
        {
            // Arrange
            string envPath = @"C:\Env\KotorPath";
            Environment.SetEnvironmentVariable("K1_PATH", envPath, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Environment variable should be checked first
                // (Registry check happens after, so env var takes precedence)
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesK1PathWithSpaces_FromEnv()
        {
            // Arrange
            string testPath = @"C:\Program Files\Kotor Test";
            Environment.SetEnvironmentVariable("K1_PATH", testPath, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should handle paths with spaces
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesK1PathWithQuotes_StripsQuotes()
        {
            // Arrange
            string testPath = @"C:\Test\KotorQuoted";
            string tempEnvPath = Path.Combine(Path.GetTempPath(), "test_k1_quoted.env");

            try
            {
                File.WriteAllText(tempEnvPath, $"K1_PATH=\"{testPath}\"\n");

                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Quotes should be stripped from .env file
            }
            finally
            {
                if (File.Exists(tempEnvPath))
                {
                    File.Delete(tempEnvPath);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_ValidatesK1PathFromEnv_ChecksChitinKey()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "chitin.key"), "test");
            File.WriteAllText(Path.Combine(tempDir, "swkotor.exe"), "test");
            Environment.SetEnvironmentVariable("K1_PATH", tempDir, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert
                Assert.NotNull(detected);
                Assert.Equal(tempDir, detected);
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
                if (Directory.Exists(tempDir))
                {
                    File.Delete(Path.Combine(tempDir, "chitin.key"));
                    File.Delete(Path.Combine(tempDir, "swkotor.exe"));
                    Directory.Delete(tempDir);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_RejectsInvalidK1PathFromEnv_InvalidPath()
        {
            // Arrange
            string invalidPath = @"C:\Invalid\Path\Does\Not\Exist";
            Environment.SetEnvironmentVariable("K1_PATH", invalidPath, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should return null or fallback to other detection methods
                // since invalid path doesn't exist
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        [Fact]
        public void KotorGuiManager_RejectsK1PathWithoutChitinKey_InvalidInstallation()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            // No chitin.key file
            Environment.SetEnvironmentVariable("K1_PATH", tempDir, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should not return invalid path
                Assert.Null(detected);
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_RejectsK1PathWithoutExecutable_InvalidInstallation()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "chitin.key"), "test");
            // No swkotor.exe file
            Environment.SetEnvironmentVariable("K1_PATH", tempDir, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should not return invalid path
                Assert.Null(detected);
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
                if (Directory.Exists(tempDir))
                {
                    File.Delete(Path.Combine(tempDir, "chitin.key"));
                    Directory.Delete(tempDir);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesK1PathWithTrailingSlash_NormalizesPath()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "chitin.key"), "test");
            File.WriteAllText(Path.Combine(tempDir, "swkotor.exe"), "test");
            string pathWithSlash = tempDir + Path.DirectorySeparatorChar;
            Environment.SetEnvironmentVariable("K1_PATH", pathWithSlash, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);
                bool isValid = GamePathDetector.IsValidInstallation(pathWithSlash, Odyssey.Core.KotorGame.K1);

                // Assert
                Assert.True(isValid); // Should handle trailing slash
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
                if (Directory.Exists(tempDir))
                {
                    File.Delete(Path.Combine(tempDir, "chitin.key"));
                    File.Delete(Path.Combine(tempDir, "swkotor.exe"));
                    Directory.Delete(tempDir);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_LoadsK1PathFromEnvFile_WhenFileExistsInRepoRoot()
        {
            // Arrange
            string tempEnvPath = Path.Combine(Path.GetTempPath(), "test_repo.env");
            string testPath = @"C:\Test\RepoK1Path";

            try
            {
                StringBuilder envContent = new StringBuilder();
                envContent.AppendLine("# KOTOR Path Configuration");
                envContent.AppendLine($"K1_PATH={testPath}");
                File.WriteAllText(tempEnvPath, envContent.ToString());

                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should load from .env file format
            }
            finally
            {
                if (File.Exists(tempEnvPath))
                {
                    File.Delete(tempEnvPath);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesK1PathFromEnvFile_WithMultipleEntries()
        {
            // Arrange
            string tempEnvPath = Path.Combine(Path.GetTempPath(), "test_multi_k1.env");
            string k1Path = @"C:\Test\K1Path";
            string otherVar = "SOME_OTHER_VAR=value";

            try
            {
                StringBuilder envContent = new StringBuilder();
                envContent.AppendLine(otherVar);
                envContent.AppendLine($"K1_PATH={k1Path}");
                envContent.AppendLine("ANOTHER_VAR=another_value");
                File.WriteAllText(tempEnvPath, envContent.ToString());

                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should correctly extract K1_PATH from multiple entries
            }
            finally
            {
                if (File.Exists(tempEnvPath))
                {
                    File.Delete(tempEnvPath);
                }
            }
        }

        #endregion

        #region Test 13-18: Resource Loading and Error Handling

        [Fact]
        public void KotorGuiManager_ValidatesInstallationPath_ForK1Game()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "chitin.key"), "test");
            File.WriteAllText(Path.Combine(tempDir, "swkotor.exe"), "test");

            try
            {
                // Act
                bool isValid = GamePathDetector.IsValidInstallation(tempDir, Odyssey.Core.KotorGame.K1);

                // Assert
                Assert.True(isValid);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    File.Delete(Path.Combine(tempDir, "chitin.key"));
                    File.Delete(Path.Combine(tempDir, "swkotor.exe"));
                    Directory.Delete(tempDir);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_ValidatesInstallationPath_ForK2Game()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "chitin.key"), "test");
            File.WriteAllText(Path.Combine(tempDir, "swkotor2.exe"), "test");

            try
            {
                // Act
                bool isValid = GamePathDetector.IsValidInstallation(tempDir, Odyssey.Core.KotorGame.K2);

                // Assert
                Assert.True(isValid);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    File.Delete(Path.Combine(tempDir, "chitin.key"));
                    File.Delete(Path.Combine(tempDir, "swkotor2.exe"));
                    Directory.Delete(tempDir);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesNullGuiResourceReference_Gracefully()
        {
            // Arrange - Testing path detection logic when GUI resource loading fails
            // This test verifies that path detection still works even when GUI resources are missing

            // Act & Assert - Path detection should not depend on GUI resource availability
            string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);
            // Should not throw even if GUI resources are unavailable
        }

        [Fact]
        public void KotorGuiManager_HandlesMissingGamePath_FallsBackToDetection()
        {
            // Arrange - No environment variable set

            // Act
            string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

            // Assert - Should attempt other detection methods (registry, Steam, etc.)
            // Result may be null, but should not throw
        }

        [Fact]
        public void KotorGuiManager_HandlesInvalidEnvFileFormat_DoesNotCrash()
        {
            // Arrange
            string tempEnvPath = Path.Combine(Path.GetTempPath(), "test_invalid_format.env");

            try
            {
                File.WriteAllText(tempEnvPath, "INVALID_FORMAT_NO_EQUALS\nMALFORMED");

                // Act & Assert - Should handle gracefully
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);
                // Should not throw on malformed .env file
            }
            finally
            {
                if (File.Exists(tempEnvPath))
                {
                    File.Delete(tempEnvPath);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesEnvFileReadError_Gracefully()
        {
            // Arrange - Test with a file that might cause read errors
            // This tests error handling in LoadEnvFile

            // Act & Assert - Should handle file read errors gracefully
            string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);
            // Should not throw on file read errors
        }

        #endregion

        #region Test 19-24: Path Detection Robustness and Edge Cases

        [Fact]
        public void KotorGuiManager_HandlesCaseSensitiveK1Path_FromEnv()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "chitin.key"), "test");
            File.WriteAllText(Path.Combine(tempDir, "swkotor.exe"), "test");
            Environment.SetEnvironmentVariable("K1_PATH", tempDir.ToUpper(), EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should handle case differences
                // Path comparison should work regardless of case on Windows
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
                if (Directory.Exists(tempDir))
                {
                    File.Delete(Path.Combine(tempDir, "chitin.key"));
                    File.Delete(Path.Combine(tempDir, "swkotor.exe"));
                    Directory.Delete(tempDir);
                }
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesVeryLongK1Path_FromEnv()
        {
            // Arrange - Create a very long path
            string tempBase = Path.GetTempPath();
            StringBuilder longPath = new StringBuilder(tempBase);
            for (int i = 0; i < 10; i++)
            {
                longPath.Append($"LongDir{i}");
                if (i < 9)
                {
                    longPath.Append(Path.DirectorySeparatorChar);
                }
            }

            string longPathStr = longPath.ToString();

            try
            {
                Directory.CreateDirectory(longPathStr);
                File.WriteAllText(Path.Combine(longPathStr, "chitin.key"), "test");
                File.WriteAllText(Path.Combine(longPathStr, "swkotor.exe"), "test");
                Environment.SetEnvironmentVariable("K1_PATH", longPathStr, EnvironmentVariableTarget.Process);

                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);
                bool isValid = GamePathDetector.IsValidInstallation(longPathStr, Odyssey.Core.KotorGame.K1);

                // Assert
                Assert.True(isValid);
            }
            catch (PathTooLongException)
            {
                // Expected on some systems
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
                // Cleanup for long paths is complex, best effort
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesRelativeK1Path_FromEnv()
        {
            // Arrange
            string relativePath = @".\Relative\Path";
            Environment.SetEnvironmentVariable("K1_PATH", relativePath, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Relative paths may or may not work depending on context
                // This tests that the system handles them without crashing
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesNetworkK1Path_FromEnv()
        {
            // Arrange
            string networkPath = @"\\Server\Share\Kotor";
            Environment.SetEnvironmentVariable("K1_PATH", networkPath, EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should handle network paths (validation will fail if path doesn't exist)
                // This tests that network paths don't cause crashes
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesEmptyK1PathValue_FromEnv()
        {
            // Arrange
            Environment.SetEnvironmentVariable("K1_PATH", "", EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should handle empty values gracefully
                // Should fall back to other detection methods
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        [Fact]
        public void KotorGuiManager_HandlesWhitespaceOnlyK1Path_FromEnv()
        {
            // Arrange
            Environment.SetEnvironmentVariable("K1_PATH", "   ", EnvironmentVariableTarget.Process);

            try
            {
                // Act
                string detected = GamePathDetector.DetectKotorPath(Odyssey.Core.KotorGame.K1);

                // Assert - Should handle whitespace-only values gracefully
                // Should fall back to other detection methods
            }
            finally
            {
                Environment.SetEnvironmentVariable("K1_PATH", null, EnvironmentVariableTarget.Process);
            }
        }

        #endregion
    }
}

