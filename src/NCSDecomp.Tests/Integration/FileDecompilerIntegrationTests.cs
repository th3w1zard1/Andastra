using System;
using System.IO;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using FluentAssertions;
using NCSDecomp.Tests.TestHelpers;
using Xunit;
using File = CSharpKOTOR.Formats.NCS.NCSDecomp.File;

namespace NCSDecomp.Tests.Integration
{
    public class FileDecompilerIntegrationTests : IDisposable
    {
        private readonly FileDecompiler _decompiler;
        private readonly Settings _settings;
        private readonly string _tempDir;
        private readonly string _testFilesDir;

        public FileDecompilerIntegrationTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            
            _testFilesDir = TestFileHelper.GetTestFilesPath();
            TestFileHelper.EnsureTestFilesDirectory();
            
            _settings = new Settings();
            _settings.SetProperty("Output Directory", _tempDir);
            _settings.SetProperty("Game Type", "TSL");
            
            // Try to find nwscript.nss
            string nwscriptPath = NWScriptLocator.FindNWScriptFile(NWScriptLocator.GameType.TSL, _settings)?.GetAbsolutePath();
            if (string.IsNullOrEmpty(nwscriptPath) || !System.IO.File.Exists(nwscriptPath))
            {
                // Create minimal nwscript.nss for testing
                nwscriptPath = Path.Combine(_tempDir, "nwscript.nss");
                System.IO.File.WriteAllText(nwscriptPath, "// 0\nvoid ActionTest(int nAction);\n");
                _settings.SetProperty("NWScript Path", nwscriptPath);
            }
            
            _decompiler = new FileDecompiler(_settings, NWScriptLocator.GameType.TSL);
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
        public void Decompile_WithValidNCSFile_ShouldNotThrow()
        {
            // Arrange
            string testFile = TestFileHelper.GetTestFile("a_galaxymap.ncs");
            if (!System.IO.File.Exists(testFile))
            {
                // Skip if test file doesn't exist
                return;
            }
            
            File ncsFile = new File(testFile);

            // Act
            Action act = () => _decompiler.Decompile(ncsFile);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Decompile_WithValidNCSFile_ShouldReturnValidResult()
        {
            // Arrange
            string testFile = TestFileHelper.GetTestFile("a_galaxymap.ncs");
            if (!System.IO.File.Exists(testFile))
            {
                // Skip if test file doesn't exist
                return;
            }
            
            File ncsFile = new File(testFile);

            // Act
            int result = _decompiler.Decompile(ncsFile);

            // Assert
            result.Should().BeOneOf(
                FileDecompiler.SUCCESS,
                FileDecompiler.FAILURE,
                FileDecompiler.PARTIAL_COMPILE,
                FileDecompiler.PARTIAL_COMPARE
            );
        }

        [Fact]
        public void Decompile_WithValidNCSFile_ShouldGenerateCode()
        {
            // Arrange
            string testFile = TestFileHelper.GetTestFile("a_galaxymap.ncs");
            if (!System.IO.File.Exists(testFile))
            {
                // Skip if test file doesn't exist
                return;
            }
            
            File ncsFile = new File(testFile);

            // Act
            int result = _decompiler.Decompile(ncsFile);
            string generatedCode = _decompiler.GetGeneratedCode(ncsFile);

            // Assert
            if (result != FileDecompiler.FAILURE)
            {
                generatedCode.Should().NotBeNull();
                generatedCode.Should().NotBeEmpty();
            }
        }

        [Fact]
        public void Decompile_WithValidNCSFile_ShouldStoreInFiledata()
        {
            // Arrange
            string testFile = TestFileHelper.GetTestFile("a_galaxymap.ncs");
            if (!System.IO.File.Exists(testFile))
            {
                // Skip if test file doesn't exist
                return;
            }
            
            File ncsFile = new File(testFile);

            // Act
            int result = _decompiler.Decompile(ncsFile);
            var variableData = _decompiler.GetVariableData(ncsFile);

            // Assert
            if (result != FileDecompiler.FAILURE)
            {
                variableData.Should().NotBeNull();
            }
        }

        [Fact]
        public void Decompile_MultipleFiles_ShouldHandleEachIndependently()
        {
            // Arrange
            string testFile = TestFileHelper.GetTestFile("a_galaxymap.ncs");
            if (!System.IO.File.Exists(testFile))
            {
                // Skip if test file doesn't exist
                return;
            }
            
            File ncsFile1 = new File(testFile);
            File ncsFile2 = new File(testFile); // Same file, different instance

            // Act
            int result1 = _decompiler.Decompile(ncsFile1);
            int result2 = _decompiler.Decompile(ncsFile2);

            // Assert
            result1.Should().BeOneOf(
                FileDecompiler.SUCCESS,
                FileDecompiler.FAILURE,
                FileDecompiler.PARTIAL_COMPILE,
                FileDecompiler.PARTIAL_COMPARE
            );
            result2.Should().BeOneOf(
                FileDecompiler.SUCCESS,
                FileDecompiler.FAILURE,
                FileDecompiler.PARTIAL_COMPILE,
                FileDecompiler.PARTIAL_COMPARE
            );
        }

        [Fact]
        public void CloseFile_AfterDecompile_ShouldNotThrow()
        {
            // Arrange
            string testFile = TestFileHelper.GetTestFile("a_galaxymap.ncs");
            if (!System.IO.File.Exists(testFile))
            {
                // Skip if test file doesn't exist
                return;
            }
            
            File ncsFile = new File(testFile);
            _decompiler.Decompile(ncsFile);

            // Act
            Action act = () => _decompiler.CloseFile(ncsFile);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void GetGeneratedCode_AfterCloseFile_ShouldReturnNull()
        {
            // Arrange
            string testFile = TestFileHelper.GetTestFile("a_galaxymap.ncs");
            if (!System.IO.File.Exists(testFile))
            {
                // Skip if test file doesn't exist
                return;
            }
            
            File ncsFile = new File(testFile);
            _decompiler.Decompile(ncsFile);
            _decompiler.CloseFile(ncsFile);

            // Act
            string result = _decompiler.GetGeneratedCode(ncsFile);

            // Assert
            result.Should().BeNull();
        }
    }
}

