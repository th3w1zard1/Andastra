using System;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.Capsule;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods;
using Andastra.Parsing.Resource;
using Andastra.Parsing.TSLPatcher;
using Moq;
using Xunit;

namespace Andastra.Parsing.Tests.Patcher
{

    /// <summary>
    /// Tests for ModInstaller (ported from test_config.py)
    /// Tests lookup_resource and should_patch functionality
    /// </summary>
    public class ModInstallerTests : IDisposable
    {
        /// <summary>
        /// Concrete test implementation of PatcherModifications for testing.
        /// </summary>
        public class TestPatcherModifications : PatcherModifications
        {
            public TestPatcherModifications(string sourcefile = "test", bool? replace = null) : base(sourcefile, replace)
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

        /// <summary>
        /// Helper to create TestPatcherModifications instances for tests.
        /// </summary>
        private static TestPatcherModifications CreatePatch(string sourceFile = "file1", bool? replace = null,
            string destination = null, string saveAs = null, string action = null, bool? skipIfNotReplace = null)
        {
            var patch = new TestPatcherModifications(sourceFile, replace);
            if (destination != null) patch.Destination = destination;
            if (saveAs != null) patch.SaveAs = saveAs;
            if (action != null) patch.Action = action;
            if (skipIfNotReplace.HasValue) patch.SkipIfNotReplace = skipIfNotReplace.Value;
            return patch;
        }
        private readonly string _tempDirectory;
        private readonly string _tempChangesIni;
        private ModInstaller _installer;

        public ModInstallerTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(_tempDirectory);
            _tempChangesIni = Path.Combine(_tempDirectory, "changes.ini");
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_Exists_DestinationDot()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

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
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_Exists_SaveAs_DestinationDot()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, ".", "file2", "Patch ");

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_Exists_DestinationOverride()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "Override", "file1", "Patch ");

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_Exists_SaveAs_DestinationOverride()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "Override", "file2", "Compile");

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_NotExists_SaveAs_DestinationOverride()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "Override", "file2", "Copy ");

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_NotExists_DestinationOverride()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "Override", "file1", "Copy ");

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_Exists_DestinationCapsule()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "capsule.mod", "file1", "Patch ");

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true, capsule: null);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_Exists_SaveAs_DestinationCapsule()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "capsule.mod", "file2", "Patch ");

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true, capsule: null);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_NotReplaceFile_Exists_SkipFalse()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", false, "other", "file3", "Patching", false);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_SkipIfNotReplace_NotReplaceFile_Exists()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", false, "other", "file3", "Patching", true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: true);

            // Assert
            Assert.False(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_NotExists_SaveAs_DestinationCapsule()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "capsule.mod", "file2", "Copy ");

            var mockCapsule = new Capsule(Path.Combine(_tempDirectory, "capsule.mod"), createIfNotExist: true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false, capsule: mockCapsule);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_ReplaceFile_NotExists_DestinationCapsule()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", true, "capsule.mod", "file1", "Copy ");

            var mockCapsule = new Capsule(Path.Combine(_tempDirectory, "capsule.mod"), createIfNotExist: true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false, capsule: mockCapsule);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_CapsuleNotExist_ShouldReturnFalse()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", null, "capsule", null, "Patching");

            var mockCapsule = new Capsule(Path.Combine(_tempDirectory, "nonexistent.mod"), createIfNotExist: true);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false, capsule: mockCapsule);

            // Assert
            Assert.False(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ShouldPatch_DefaultBehavior()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


            TestPatcherModifications patch = CreatePatch("file1", false, "other", "file3", "Patching", false);

            // Act
            bool result = _installer.ShouldPatch(patch, exists: false);

            // Assert
            Assert.True(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_CapsuleExistsTrue_ShouldReturnNull()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

            TestPatcherModifications patch = CreatePatch("file1", false);

            var mockCapsule = new Capsule(Path.Combine(_tempDirectory, "test.mod"), createIfNotExist: true);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempDirectory, existsAtOutput: true, capsule: mockCapsule);

            // Assert
            Assert.Null(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_ReplaceFileTrueNoFile_ShouldReturnNull()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

            TestPatcherModifications patch = CreatePatch("nonexistent.txt", true);
            patch.SourceFolder = ".";

            // Act
            byte[] result = _installer.LookupResource(patch, _tempDirectory, existsAtOutput: false, capsule: null);

            // Assert
            Assert.Null(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_CapsuleExistsTrueNoFile_ShouldReturnNull()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

            TestPatcherModifications patch = CreatePatch("file1", false);

            var mockCapsule = new Capsule(Path.Combine(_tempDirectory, "test.mod"), createIfNotExist: true);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempDirectory, existsAtOutput: true, capsule: mockCapsule);

            // Assert
            Assert.Null(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_NoCapsuleExistsTrueNoFile_ShouldReturnNull()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

            TestPatcherModifications patch = CreatePatch("nonexistent.txt", false);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempDirectory, existsAtOutput: true, capsule: null);

            // Assert
            Assert.Null(result);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void LookupResource_NoCapsuleExistsFalseNoFile_ShouldReturnNull()
        {
            // Arrange
            _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

            TestPatcherModifications patch = CreatePatch("nonexistent.txt", false);

            // Act
            byte[] result = _installer.LookupResource(patch, _tempDirectory, existsAtOutput: false, capsule: null);

            // Assert
            Assert.Null(result);
        }
    }
}

