using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Logger;
using CSharpKOTOR.Uninstall;
using FluentAssertions;
using Xunit;

namespace CSharpKOTOR.Tests.Uninstall
{

    /// <summary>
    /// Tests for ModUninstaller functionality
    /// Ported from Python test_mods.py uninstall tests (if they exist)
    /// </summary>
    public class ModUninstallerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _backupDir;
        private readonly string _gameDir;

        public ModUninstallerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), $"holopatcher_test_{Guid.NewGuid():N}");
            _backupDir = Path.Combine(_tempDir, "backup");
            _gameDir = Path.Combine(_tempDir, "game");

            Directory.CreateDirectory(_tempDir);
            Directory.CreateDirectory(_backupDir);
            Directory.CreateDirectory(_gameDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
                catch
                {
                    // Best effort cleanup
                }
            }
        }

        [Fact]
        public void IsValidBackupFolder_ValidFormat_ReturnsTrue()
        {
            // Arrange
            string validFolderName = "2024-01-15_14.30.45";
            var folderPath = new CaseAwarePath(Path.Combine(_backupDir, validFolderName));

            // Act
            bool result = ModUninstaller.IsValidBackupFolder(folderPath);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsValidBackupFolder_InvalidFormat_ReturnsFalse()
        {
            // Arrange
            string invalidFolderName = "not_a_valid_date";
            var folderPath = new CaseAwarePath(Path.Combine(_backupDir, invalidFolderName));

            // Act
            bool result = ModUninstaller.IsValidBackupFolder(folderPath);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetMostRecentBackup_NoBackups_ReturnsNull()
        {
            // Arrange
            var backupPath = new CaseAwarePath(_backupDir);
            bool errorShown = false;

            // [CanBeNull] Act
            CaseAwarePath result = ModUninstaller.GetMostRecentBackup(
                backupPath,
                (title, msg) => errorShown = true
            );

            // Assert
            result.Should().BeNull();
            errorShown.Should().BeTrue();
        }

        [Fact]
        public void GetMostRecentBackup_MultipleBackups_ReturnsNewest()
        {
            // Arrange
            string backup1 = Path.Combine(_backupDir, "2024-01-15_14.30.45");
            string backup2 = Path.Combine(_backupDir, "2024-01-16_10.20.30");
            string backup3 = Path.Combine(_backupDir, "2024-01-14_08.15.00");

            Directory.CreateDirectory(backup1);
            Directory.CreateDirectory(backup2);
            Directory.CreateDirectory(backup3);

            // Create dummy files so folders aren't empty
            File.WriteAllText(Path.Combine(backup1, "file1.txt"), "test");
            File.WriteAllText(Path.Combine(backup2, "file2.txt"), "test");
            File.WriteAllText(Path.Combine(backup3, "file3.txt"), "test");

            var backupPath = new CaseAwarePath(_backupDir);

            // [CanBeNull] Act
            CaseAwarePath result = ModUninstaller.GetMostRecentBackup(backupPath);

            // Assert
            result.Should().NotBeNull();
            result.Name.Should().Be("2024-01-16_10.20.30");
        }

        [Fact]
        public void GetMostRecentBackup_EmptyFolders_ReturnsNull()
        {
            // Arrange
            string backup1 = Path.Combine(_backupDir, "2024-01-15_14.30.45");
            Directory.CreateDirectory(backup1); // Empty folder

            var backupPath = new CaseAwarePath(_backupDir);
            bool errorShown = false;

            // [CanBeNull] Act
            CaseAwarePath result = ModUninstaller.GetMostRecentBackup(
                backupPath,
                (title, msg) => errorShown = true
            );

            // Assert
            result.Should().BeNull();
            errorShown.Should().BeTrue();
        }

        [Fact]
        public void RestoreBackup_RestoresFilesCorrectly()
        {
            // Arrange
            string backupFolder = Path.Combine(_backupDir, "2024-01-15_14.30.45");
            Directory.CreateDirectory(backupFolder);

            string testFile = Path.Combine(backupFolder, "test_file.txt");
            File.WriteAllText(testFile, "test content");

            string gameSubDir = Path.Combine(_gameDir, "Override");
            Directory.CreateDirectory(gameSubDir);

            string existingFile = Path.Combine(gameSubDir, "existing.txt");
            File.WriteAllText(existingFile, "existing content");

            var logger = new PatchLogger();
            var uninstaller = new ModUninstaller(
                new CaseAwarePath(_backupDir),
                new CaseAwarePath(_gameDir),
                logger
            );

            var existingFiles = new HashSet<string> { existingFile };
            var filesInBackup = new List<CaseAwarePath> { new CaseAwarePath(testFile) };

            // Act
            uninstaller.RestoreBackup(
                new CaseAwarePath(backupFolder),
                existingFiles,
                filesInBackup
            );

            // Assert
            File.Exists(existingFile).Should().BeFalse();
            File.Exists(Path.Combine(_gameDir, "test_file.txt")).Should().BeTrue();
        }

        [Fact]
        public void GetBackupInfo_ValidBackup_ReturnsCorrectInfo()
        {
            // Arrange
            string backupFolder = Path.Combine(_backupDir, "2024-01-15_14.30.45");
            Directory.CreateDirectory(backupFolder);

            string testFile = Path.Combine(backupFolder, "test_file.txt");
            File.WriteAllText(testFile, "test content");

            string deleteListFile = Path.Combine(backupFolder, "remove these files.txt");
            string fileToDelete = Path.Combine(_gameDir, "file_to_delete.txt");
            File.WriteAllText(fileToDelete, "content");
            File.WriteAllText(deleteListFile, fileToDelete);

            var logger = new PatchLogger();
            var uninstaller = new ModUninstaller(
                new CaseAwarePath(_backupDir),
                new CaseAwarePath(_gameDir),
                logger
            );

            // Act
            (CaseAwarePath backupPath, HashSet<string> existingFiles, List<CaseAwarePath> filesInBackup, int folderCount) = uninstaller.GetBackupInfo();

            // Assert
            backupPath.Should().NotBeNull();
            existingFiles.Should().Contain(fileToDelete);
            filesInBackup.Should().HaveCountGreaterThanOrEqualTo(1);
        }

        [Fact]
        public void UninstallSelectedMod_WithUserConfirmation_CompletesSuccessfully()
        {
            // Arrange
            string backupFolder = Path.Combine(_backupDir, "2024-01-15_14.30.45");
            Directory.CreateDirectory(backupFolder);

            string testFile = Path.Combine(backupFolder, "test_file.txt");
            File.WriteAllText(testFile, "test content");

            string deleteListFile = Path.Combine(backupFolder, "remove these files.txt");
            string fileToDelete = Path.Combine(_gameDir, "file_to_delete.txt");
            File.WriteAllText(fileToDelete, "content");
            File.WriteAllText(deleteListFile, fileToDelete);

            var logger = new PatchLogger();
            var uninstaller = new ModUninstaller(
                new CaseAwarePath(_backupDir),
                new CaseAwarePath(_gameDir),
                logger
            );

            // Act
            bool result = uninstaller.UninstallSelectedMod(
                showErrorDialog: null,
                showYesNoDialog: (title, msg) => true, // Confirm
                showYesNoCancelDialog: (title, msg) => false // Don't delete backup
            );

            // Assert
            result.Should().BeTrue();
            File.Exists(fileToDelete).Should().BeFalse();
            File.Exists(Path.Combine(_gameDir, "test_file.txt")).Should().BeTrue();
        }

        [Fact]
        public void UninstallSelectedMod_WithoutUserConfirmation_ReturnsFalse()
        {
            // Arrange
            string backupFolder = Path.Combine(_backupDir, "2024-01-15_14.30.45");
            Directory.CreateDirectory(backupFolder);
            File.WriteAllText(Path.Combine(backupFolder, "test.txt"), "test");

            var logger = new PatchLogger();
            var uninstaller = new ModUninstaller(
                new CaseAwarePath(_backupDir),
                new CaseAwarePath(_gameDir),
                logger
            );

            // Act
            bool result = uninstaller.UninstallSelectedMod(
                showErrorDialog: null,
                showYesNoDialog: (title, msg) => false, // Cancel
                showYesNoCancelDialog: null
            );

            // Assert
            result.Should().BeFalse();
        }

    }
}

