using System.IO;
using Moq;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.Capsule;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Mods;
using TSLPatcher.Core.Patcher;
using TSLPatcher.Core.Resources;
using Xunit;

namespace TSLPatcher.Tests.Patcher;

/// <summary>
/// Tests for ModInstaller (ported from test_config.py)
/// Tests lookup_resource and should_patch functionality
/// </summary>
public class ModInstallerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _tempChangesIni;
    private ModInstaller? _installer;

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

    [Fact]
    public void ShouldPatch_ReplaceFile_Exists_DestinationDot()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns(".");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file1");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patch ");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_Exists_SaveAs_DestinationDot()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns(".");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file2");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patch ");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_Exists_DestinationOverride()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("Override");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file1");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patch ");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_Exists_SaveAs_DestinationOverride()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("Override");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file2");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Compile");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_NotExists_SaveAs_DestinationOverride()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("Override");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file2");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Copy ");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_NotExists_DestinationOverride()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("Override");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file1");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Copy ");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_Exists_DestinationCapsule()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("capsule.mod");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file1");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patch ");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true, capsule: null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_Exists_SaveAs_DestinationCapsule()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("capsule.mod");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file2");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patch ");

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true, capsule: null);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_NotReplaceFile_Exists_SkipFalse()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("other");
        patch.Setup(p => p.ReplaceFile).Returns(false);
        patch.Setup(p => p.SaveAs).Returns("file3");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patching");
        patch.Setup(p => p.SkipIfNotReplace).Returns(false);

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_SkipIfNotReplace_NotReplaceFile_Exists()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("other");
        patch.Setup(p => p.ReplaceFile).Returns(false);
        patch.Setup(p => p.SaveAs).Returns("file3");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patching");
        patch.Setup(p => p.SkipIfNotReplace).Returns(true);

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: true);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_NotExists_SaveAs_DestinationCapsule()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("capsule.mod");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file2");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Copy ");

        var mockCapsule = new Mock<Capsule>(MockBehavior.Strict, Path.Combine(_tempDirectory, "capsule.mod"));
        mockCapsule.Setup(c => c.Path.ToString()).Returns(Path.Combine(_tempDirectory, "capsule.mod"));

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: false, capsule: mockCapsule.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_ReplaceFile_NotExists_DestinationCapsule()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("capsule.mod");
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SaveAs).Returns("file1");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Copy ");

        var mockCapsule = new Mock<Capsule>(MockBehavior.Strict, Path.Combine(_tempDirectory, "capsule.mod"));
        mockCapsule.Setup(c => c.Path.ToString()).Returns(Path.Combine(_tempDirectory, "capsule.mod"));

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: false, capsule: mockCapsule.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldPatch_CapsuleNotExist_ShouldReturnFalse()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("capsule");
        patch.Setup(p => p.Action).Returns("Patching");
        patch.Setup(p => p.SourceFile).Returns("file1");

        var mockCapsule = new Mock<Capsule>(MockBehavior.Strict, Path.Combine(_tempDirectory, "capsule.mod"));
        mockCapsule.Setup(c => c.Path.ToString()).Returns(Path.Combine(_tempDirectory, "nonexistent.mod"));

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: false, capsule: mockCapsule.Object);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldPatch_DefaultBehavior()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);


        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.Destination).Returns("other");
        patch.Setup(p => p.SaveAs).Returns("file3");
        patch.Setup(p => p.SourceFile).Returns("file1");
        patch.Setup(p => p.Action).Returns("Patching");
        patch.Setup(p => p.SkipIfNotReplace).Returns(false);
        patch.Setup(p => p.ReplaceFile).Returns(false);

        // Act
        bool result = _installer.ShouldPatch(patch.Object, exists: false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void LookupResource_CapsuleExistsTrue_ShouldReturnNull()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.ReplaceFile).Returns(false);

        var mockCapsule = new Mock<Capsule>();

        // Act
        byte[]? result = _installer.LookupResource(patch.Object, _tempDirectory, existsAtOutput: true, capsule: mockCapsule.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LookupResource_ReplaceFileTrueNoFile_ShouldReturnNull()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.ReplaceFile).Returns(true);
        patch.Setup(p => p.SourceFile).Returns("nonexistent.txt");
        patch.Setup(p => p.SourceFolder).Returns(".");

        // Act
        byte[] result = _installer.LookupResource(patch.Object, _tempDirectory, existsAtOutput: false, capsule: null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LookupResource_CapsuleExistsTrueNoFile_ShouldReturnNull()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.ReplaceFile).Returns(false);

        var mockCapsule = new Mock<Capsule>();
        mockCapsule.Setup(c => c.GetResource(It.IsAny<string>(), It.IsAny<ResourceType>())).Returns((byte[]?)null);

        // Act
        byte[] result = _installer.LookupResource(patch.Object, _tempDirectory, existsAtOutput: true, capsule: mockCapsule.Object);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LookupResource_NoCapsuleExistsTrueNoFile_ShouldReturnNull()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.ReplaceFile).Returns(false);
        patch.Setup(p => p.SourceFile).Returns("nonexistent.txt");

        // Act
        byte[] result = _installer.LookupResource(patch.Object, _tempDirectory, existsAtOutput: true, capsule: null);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void LookupResource_NoCapsuleExistsFalseNoFile_ShouldReturnNull()
    {
        // Arrange
        _installer = new ModInstaller(_tempDirectory, _tempDirectory, _tempChangesIni);

        var patch = new Mock<PatcherModifications>();
        patch.Setup(p => p.ReplaceFile).Returns(false);
        patch.Setup(p => p.SourceFile).Returns("nonexistent.txt");

        // Act
        byte[] result = _installer.LookupResource(patch.Object, _tempDirectory, existsAtOutput: false, capsule: null);

        // Assert
        Assert.Null(result);
    }
}

