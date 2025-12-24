using System;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods;
using Andastra.Parsing.Resource;
using Andastra.Parsing.TSLPatcher;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Config
{

    /// <summary>
    /// Tests for configuration and patching functionality
    /// Ported from tests/tslpatcher/test_config.py
    /// </summary>
    public class ConfigTests : IDisposable
    {
        /// <summary>
        /// Concrete test implementation of PatcherModifications for testing.
        /// </summary>
        public class TestPatcherModifications : PatcherModifications
        {
            public TestPatcherModifications(string sourcefile = "test_filename", bool? replace = null) : base(sourcefile, replace)
            {
            }

            public override object PatchResource(byte[] source, PatcherMemory memory, PatchLogger logger, BioWareGame game)
            {
                return source;
            }

            public override void Apply(object mutableData, PatcherMemory memory, PatchLogger logger, BioWareGame game)
            {
                // No-op for testing
            }
        }

        private readonly string _tempDirectory;
        private readonly string _tempModPath;
        private readonly string _tempGamePath;
        private readonly string _tempChangesIni;
        private ModInstaller _installer;
        private PatchLogger _logger;

        public ConfigTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            _tempModPath = Path.Combine(_tempDirectory, "mod");
            _tempGamePath = Path.Combine(_tempDirectory, "game");
            Directory.CreateDirectory(_tempModPath);
            Directory.CreateDirectory(_tempGamePath);
            _tempChangesIni = Path.Combine(_tempModPath, "changes.ini");
            File.WriteAllText(_tempChangesIni, "[Settings]\n");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_tempDirectory))
                {
                    Directory.Delete(_tempDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Lookup Resource Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_WithReplaceFile_ShouldReadFromModPath()
        {
            // Python test: test_lookup_resource_replace_file_true
            // When replace_file=True, reads from mod path

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);
            _installer.TslPatchDataPath = _tempModPath;

            var patch = new TestPatcherModifications("test_filename", true)
            {
                SourceFolder = ".",
                SaveAs = "test_filename"
            };

            // Create test file in mod path
            string testFilePath = Path.Combine(_tempModPath, "test_filename");
            byte[] testData = System.Text.Encoding.UTF8.GetBytes("BinaryReader read_all result");
            File.WriteAllBytes(testFilePath, testData);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: false, capsule: null);

            // Assert
            result.Should().NotBeNull();
            result.Should().Equal(testData);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_WithCapsuleExists_ShouldReturnNull()
        {
            // Python test: test_lookup_resource_capsule_exists_true
            // When capsule exists and replace_file=False, returns null (uses capsule version)

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("test_filename", false)
            {
                SaveAs = "test_filename"
            };

            string capsulePath = Path.Combine(_tempGamePath, "test.mod");
            var capsule = new Capsule(capsulePath, createIfNotExist: true);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: true, capsule: capsule);

            // Assert
            result.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_NoCapsuleExists_ShouldReadFromOutput()
        {
            // Python test: test_lookup_resource_no_capsule_exists_true
            // When no capsule but file exists, reads from output location

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("test_filename", false)
            {
                SaveAs = "test_filename"
            };

            // Create file at output location
            string outputPath = Path.Combine(_tempGamePath, "test_filename");
            byte[] testData = System.Text.Encoding.UTF8.GetBytes("BinaryReader read_all result");
            File.WriteAllBytes(outputPath, testData);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: true, capsule: null);

            // Assert
            result.Should().NotBeNull();
            result.Should().Equal(testData);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_NoCapsuleNotExists_ShouldReadFromMod()
        {
            // Python test: test_lookup_resource_no_capsule_exists_false
            // When no capsule and file doesn't exist at output, reads from mod

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);
            _installer.TslPatchDataPath = _tempModPath;

            var patch = new TestPatcherModifications("test_filename", false)
            {
                SourceFolder = ".",
                SaveAs = "test_filename"
            };

            // Create file in mod path
            string modFilePath = Path.Combine(_tempModPath, "test_filename");
            byte[] testData = System.Text.Encoding.UTF8.GetBytes("BinaryReader read_all result");
            File.WriteAllBytes(modFilePath, testData);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: false, capsule: null);

            // Assert
            result.Should().NotBeNull();
            result.Should().Equal(testData);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_CapsuleExistsFalse_ShouldReadFromCapsule()
        {
            // Python test: test_lookup_resource_capsule_exists_false (marked as @unittest.skip("broken test"))
            // Python test sets: replace_file=False, exists_at_output_location=False, capsule=capsule
            // Python logic: if replace_file or not exists_at_output_location: load from mod
            //              else if capsule is None: load from output
            //              else: load from capsule
            // When replace_file=False and exists_at_output_location=False, Python loads from mod (first condition: False or True = True)
            // But test name suggests capsule. Since Python test is broken, we test when exists_at_output_location=True:
            // When replace_file=False and exists_at_output_location=True, Python loads from capsule (second branch: capsule is not None)

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);
            _installer.TslPatchDataPath = _tempModPath;

            var patch = new TestPatcherModifications("test.txt", false)
            {
                SourceFolder = ".",
                SaveAs = "test.txt"
            };

            string capsulePath = Path.Combine(_tempGamePath, "test.mod");
            var capsule = new Capsule(capsulePath, createIfNotExist: true);

            // Add resource to capsule
            byte[] testData = System.Text.Encoding.UTF8.GetBytes("BinaryReader read_all result");
            (string resName, ResourceType resType) = ResourceIdentifier.FromPath("test.txt").Unpack();
            capsule.Add(resName, resType, testData);
            capsule.Save();

            // Act - When replace_file=False and existsAtOutput=True, Python loads from capsule
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: true, capsule: capsule);

            // Assert
            result.Should().NotBeNull();
            result.Should().Equal(testData);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_ReplaceFileNoFile_ShouldReturnNull()
        {
            // Python test: test_lookup_resource_replace_file_true_no_file
            // When replace_file=True but file not found, returns null

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("nonexistent.txt", true)
            {
                SourceFolder = ".",
                SaveAs = "nonexistent.txt"
            };

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: false, capsule: null);

            // Assert
            result.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_CapsuleExistsNoFile_ShouldReturnNull()
        {
            // Python test: test_lookup_resource_capsule_exists_true_no_file
            // When capsule exists but resource not in it, returns null

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("test_filename", false)
            {
                SaveAs = "test_filename"
            };

            string capsulePath = Path.Combine(_tempGamePath, "test.mod");
            var capsule = new Capsule(capsulePath, createIfNotExist: true);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: true, capsule: capsule);

            // Assert
            result.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_NoCapsuleExistsTrueNoFile_ShouldReturnNull()
        {
            // Python test: test_lookup_resource_no_capsule_exists_true_no_file
            // When no capsule, file exists at output location but not found, returns null

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("nonexistent.txt", false)
            {
                SaveAs = "nonexistent.txt"
            };

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: true, capsule: null);

            // Assert
            result.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_NoCapsuleExistsFalseNoFile_ShouldReturnNull()
        {
            // Python test: test_lookup_resource_no_capsule_exists_false_no_file
            // When no capsule, file doesn't exist at output and not found, returns null

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("nonexistent.txt", false)
            {
                SaveAs = "nonexistent.txt"
            };

            // Act
            byte[] result = _installer.LookupResource(patch, _tempGamePath, existsAtOutput: false, capsule: null);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region Should Patch Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileExistsDestinationDot()
        {
            // Python test: test_replace_file_exists_destination_dot
            // Tests message logging for patching file in root folder

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = ".",
                SaveAs = "file1",
                SourceFile = "file1",
                Action = "Patch "
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Patching 'file1' and replacing existing file in the"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileExistsSaveasDestinationDot()
        {
            // Python test: test_replace_file_exists_saveas_destination_dot
            // Tests when saveas != sourcefile

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = ".",
                SaveAs = "file2",
                SourceFile = "file1",
                Action = "Patch "
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Patching 'file1' and replacing existing file 'file2' in the"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileExistsDestinationOverride()
        {
            // Python test: test_replace_file_exists_destination_override
            // Tests patching to Override folder

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "Override",
                SaveAs = "file1",
                SourceFile = "file1",
                Action = "Patch "
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Patching 'file1' and replacing existing file in the 'Override' folder"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileExistsSaveasDestinationOverride()
        {
            // Python test: test_replace_file_exists_saveas_destination_override
            // Tests compiling with saveas to Override

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "Override",
                SaveAs = "file2",
                SourceFile = "file1",
                Action = "Compile"
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Compiling 'file1' and replacing existing file 'file2' in the 'Override' folder"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileNotExistsSaveasDestinationOverride()
        {
            // Python test: test_replace_file_not_exists_saveas_destination_override
            // Tests copying new file with saveas

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "Override",
                SaveAs = "file2",
                SourceFile = "file1",
                Action = "Copy "
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Copying 'file1' and saving as 'file2' in the 'Override' folder"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileNotExistsDestinationOverride()
        {
            // Python test: test_replace_file_not_exists_destination_override
            // Tests copying new file to Override

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "Override",
                SaveAs = "file1",
                SourceFile = "file1",
                Action = "Copy "
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Copying 'file1' and saving to the 'Override' folder"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileExistsDestinationCapsule()
        {
            // Python test: test_replace_file_exists_destination_capsule
            // Tests patching file in capsule (MOD/RIM/ERF)

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "capsule.mod",
                SaveAs = "file1",
                SourceFile = "file1",
                Action = "Patch "
            };

            string capsulePath = Path.Combine(_tempGamePath, "capsule.mod");
            var capsule = new Capsule(capsulePath, createIfNotExist: true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true, capsule: capsule);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Patching 'file1' and replacing existing file in the 'capsule.mod' archive"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileExistsSaveasDestinationCapsule()
        {
            // Python test: test_replace_file_exists_saveas_destination_capsule
            // Tests patching with saveas in capsule

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "capsule.mod",
                SaveAs = "file2",
                SourceFile = "file1",
                Action = "Patch "
            };

            string capsulePath = Path.Combine(_tempGamePath, "capsule.mod");
            var capsule = new Capsule(capsulePath, createIfNotExist: true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true, capsule: capsule);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Patching 'file1' and replacing existing file 'file2' in the 'capsule.mod' archive"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileNotExistsSaveasDestinationCapsule()
        {
            // Python test: test_replace_file_not_exists_saveas_destination_capsule
            // Tests copying with saveas to capsule

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "capsule.mod",
                SaveAs = "file2",
                SourceFile = "file1",
                Action = "Copy "
            };

            string capsulePath = Path.Combine(_tempGamePath, "capsule.mod");
            var capsule = new Capsule(capsulePath, createIfNotExist: true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false, capsule: capsule);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Copying 'file1' and saving as 'file2' in the 'capsule.mod' archive"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFileNotExistsDestinationCapsule()
        {
            // Python test: test_replace_file_not_exists_destination_capsule
            // Tests adding new file to capsule

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", true)
            {
                Destination = "capsule.mod",
                SaveAs = "file1",
                SourceFile = "file1",
                Action = "Copy "
            };

            string capsulePath = Path.Combine(_tempGamePath, "capsule.mod");
            var capsule = new Capsule(capsulePath, createIfNotExist: true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false, capsule: capsule);

            // Assert
            result.Should().BeTrue();
            _logger.Notes.Should().Contain(n => n.Message.Contains("Copying 'file1' and adding to the 'capsule.mod' archive"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_NotReplaceFileExistsSkipFalse()
        {
            // Python test: test_not_replace_file_exists_skip_false
            // When replace_file=False but skip_if_not_replace=False, should still patch

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", false)
            {
                Destination = "other",
                SaveAs = "file3",
                SourceFile = "file1",
                Action = "Patching",
                SkipIfNotReplace = false
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            result.Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_SkipIfNotReplaceExists()
        {
            // Python test: test_skip_if_not_replace_not_replace_file_exists
            // When skip_if_not_replace=True and file exists, should skip

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", false)
            {
                Destination = "other",
                SaveAs = "file3",
                SourceFile = "file1",
                Action = "Patching",
                SkipIfNotReplace = true
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            result.Should().BeFalse();
            _logger.Notes.Should().Contain(n => n.Message.Contains("already exists in the 'other' folder. Skipping file..."));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_CapsuleNotExist()
        {
            // Python test: test_capsule_not_exist
            // When destination capsule doesn't exist, should not patch

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", null)
            {
                Destination = "capsule",
                SourceFile = "file1",
                Action = "Patching"
            };

            // Create a capsule that doesn't exist on disk
            string nonExistentPath = Path.Combine(_tempGamePath, "nonexistent.mod");
            var mockCapsule = new Capsule(nonExistentPath, createIfNotExist: false);
            // Ensure it doesn't exist
            if (File.Exists(nonExistentPath))
            {
                File.Delete(nonExistentPath);
            }

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false, capsule: mockCapsule);

            // Assert
            result.Should().BeFalse();
            _logger.Errors.Should().Contain(e => e.Message.Contains("did not exist when attempting to"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_DefaultBehavior()
        {
            // Python test: test_default_behavior
            // Tests default case - new file, no special flags

            // Arrange
            _logger = new PatchLogger();
            _installer = new ModInstaller(_tempModPath, _tempGamePath, _tempChangesIni, _logger);

            var patch = new TestPatcherModifications("file1", false)
            {
                Destination = "other",
                SaveAs = "file3",
                SourceFile = "file1",
                Action = "Patching",
                SkipIfNotReplace = false
            };

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false);

            // Assert
            result.Should().BeTrue();
        }

        #endregion
    }
}
