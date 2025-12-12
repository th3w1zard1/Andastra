using System;
using System.IO;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using FluentAssertions;
using NCSDecomp.Tests.TestHelpers;
using Xunit;
using File = CSharpKOTOR.Formats.NCS.NCSDecomp.File;

namespace NCSDecomp.Tests
{
    public class FileDecompilerTests : IDisposable
    {
        private readonly FileDecompiler _decompiler;
        private readonly Settings _settings;
        private readonly string _tempDir;

        public FileDecompilerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            
            _settings = new Settings();
            _settings.SetProperty("Output Directory", _tempDir);
            _settings.SetProperty("Game Type", "K1");
            
            // Create a dummy nwscript.nss for testing if it doesn't exist
            string nwscriptPath = Path.Combine(_tempDir, "nwscript.nss");
            if (!System.IO.File.Exists(nwscriptPath))
            {
                // Create minimal nwscript.nss content
                System.IO.File.WriteAllText(nwscriptPath, "// 0\nvoid ActionTest(int nAction);\n");
            }
            
            _decompiler = new FileDecompiler(_settings, NWScriptLocator.GameType.K1);
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
        public void FileDecompiler_Constructor_ShouldNotThrow()
        {
            // Arrange & Act
            Action act = () => new FileDecompiler();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void FileDecompiler_Constructor_WithSettings_ShouldNotThrow()
        {
            // Arrange & Act
            Action act = () => new FileDecompiler(_settings, NWScriptLocator.GameType.K1);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Decompile_WithNonExistentFile_ShouldReturnFailure()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            int result = _decompiler.Decompile(nonExistentFile);

            // Assert
            result.Should().Be(FileDecompiler.FAILURE);
        }

        [Fact]
        public void GetVariableData_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            var result = _decompiler.GetVariableData(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetGeneratedCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.GetGeneratedCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetOriginalByteCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.GetOriginalByteCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GetNewByteCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.GetNewByteCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void UpdateSubName_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            var result = _decompiler.UpdateSubName(nonExistentFile, "old", "new");

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void RegenerateCode_WithNonExistentFile_ShouldReturnNull()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            string result = _decompiler.RegenerateCode(nonExistentFile);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void CloseFile_WithNonExistentFile_ShouldNotThrow()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));

            // Act
            Action act = () => _decompiler.CloseFile(nonExistentFile);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CloseAllFiles_ShouldNotThrow()
        {
            // Act
            Action act = () => _decompiler.CloseAllFiles();

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CompileAndCompare_WithNonExistentFile_ShouldNotThrow()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.ncs"));
            File newFile = new File(Path.Combine(_tempDir, "new.nss"));

            // Act
            Action act = () => _decompiler.CompileAndCompare(nonExistentFile, newFile);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void CompileOnly_WithNonExistentFile_ShouldNotThrow()
        {
            // Arrange
            File nonExistentFile = new File(Path.Combine(_tempDir, "nonexistent.nss"));

            // Act
            Action act = () => _decompiler.CompileOnly(nonExistentFile);

            // Assert
            act.Should().NotThrow();
        }
    }
}

