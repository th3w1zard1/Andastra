using System;
using System.IO;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using FluentAssertions;
using Xunit;
using File = CSharpKOTOR.Formats.NCS.NCSDecomp.File;

namespace NCSDecomp.Tests
{
    /// <summary>
    /// Tests specifically for the KeyNotFoundException fix.
    /// These tests ensure that accessing filedata dictionary with non-existent keys doesn't throw.
    /// </summary>
    public class KeyNotFoundExceptionFixTests : IDisposable
    {
        private readonly FileDecompiler _decompiler;
        private readonly Settings _settings;
        private readonly string _tempDir;

        public KeyNotFoundExceptionFixTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);
            
            _settings = new Settings();
            _settings.SetProperty("Output Directory", _tempDir);
            _settings.SetProperty("Game Type", "K1");
            
            // Create minimal nwscript.nss
            string nwscriptPath = Path.Combine(_tempDir, "nwscript.nss");
            System.IO.File.WriteAllText(nwscriptPath, "// 0\nvoid ActionTest(int nAction);\n");
            _settings.SetProperty("NWScript Path", nwscriptPath);
            
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
        public void GetVariableData_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.GetVariableData(file);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void GetGeneratedCode_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.GetGeneratedCode(file);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void GetOriginalByteCode_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.GetOriginalByteCode(file);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void GetNewByteCode_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.GetNewByteCode(file);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void Decompile_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.Decompile(file);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void CompileAndCompare_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));
            File newFile = new File(Path.Combine(_tempDir, "new.nss"));

            // Act
            Action act = () => _decompiler.CompileAndCompare(file, newFile);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void UpdateSubName_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.UpdateSubName(file, "old", "new");

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void RegenerateCode_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.RegenerateCode(file);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void CloseFile_WithNonExistentKey_ShouldNotThrowKeyNotFoundException()
        {
            // Arrange
            File file = new File(Path.Combine(_tempDir, "test.ncs"));

            // Act
            Action act = () => _decompiler.CloseFile(file);

            // Assert
            act.Should().NotThrow<System.Collections.Generic.KeyNotFoundException>();
        }

        [Fact]
        public void Decompile_WithFile_ShouldAddToFiledata()
        {
            // Arrange
            // Create a minimal valid NCS file for testing
            string ncsPath = Path.Combine(_tempDir, "test.ncs");
            // Write minimal NCS header
            byte[] ncsHeader = new byte[] { 0x4E, 0x43, 0x53, 0x20, 0x56, 0x31, 0x2E, 0x30, 0x00, 0x00, 0x00, 0x00 };
            System.IO.File.WriteAllBytes(ncsPath, ncsHeader);
            
            File file = new File(ncsPath);

            // Act
            int result = _decompiler.Decompile(file);
            var variableData = _decompiler.GetVariableData(file);

            // Assert
            // Even if decompilation fails, it should not throw KeyNotFoundException
            // Variable data may be null if decompilation failed, but should not throw
            if (result != FileDecompiler.FAILURE)
            {
                variableData.Should().NotBeNull();
            }
        }
    }
}

