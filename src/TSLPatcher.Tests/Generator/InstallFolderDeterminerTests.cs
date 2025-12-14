// Comprehensive tests for InstallFolderDeterminer
// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1212-1236
using System.Collections.Generic;
using CSharpKOTOR.Mods;
using CSharpKOTOR.Mods.GFF;
using CSharpKOTOR.Mods.SSF;
using CSharpKOTOR.Mods.TLK;
using CSharpKOTOR.Mods.TwoDA;
using CSharpKOTOR.TSLPatcher;
using FluentAssertions;
using Xunit;

namespace CSharpKOTOR.Tests.Generator
{
    /// <summary>
    /// Comprehensive tests for InstallFolderDeterminer.
    /// Tests determination of install folders from modifications.
    /// </summary>
    public class InstallFolderDeterminerTests
    {
        [Fact]
        public void DetermineInstallFolders_ShouldReturnEmptyList_WhenNoModifications()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void DetermineInstallFolders_ShouldIncludeAppendTlk_WhenTLKAppendsExist()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var tlkMod = new ModificationsTLK("dialog.tlk");
            tlkMod.Modifiers.Add(new ModifyTLK(0, "Test", null, false)); // Append
            modifications.Tlk.Add(tlkMod);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "append.tlk" && f.Destination == ".");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldNotIncludeAppendTlk_WhenOnlyReplacementsExist()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var tlkMod = new ModificationsTLK("dialog.tlk");
            tlkMod.Modifiers.Add(new ModifyTLK(0, "Test", null, true)); // Replacement
            modifications.Tlk.Add(tlkMod);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().NotContain(f => f.SourceFile == "append.tlk");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldInclude2DAFile_When2DAModificationsExist()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var mod2DA = new Modifications2DA("test.2da", "Override");
            modifications.Twoda.Add(mod2DA);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "test.2da" && f.Destination == "Override");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldUseDefaultOverride_When2DADestinationIsEmpty()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var mod2DA = new Modifications2DA("test.2da", "");
            modifications.Twoda.Add(mod2DA);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "test.2da" && f.Destination == "Override");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldIncludeGFFFile_WhenGFFModificationsExist()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var modGFF = new ModificationsGFF("test.utc", "Override", "test.utc", false);
            modifications.Gff.Add(modGFF);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "test.utc" && f.Destination == "Override");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldUseSaveAs_WhenGFFSaveAsIsDifferent()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var modGFF = new ModificationsGFF("source.utc", "Override", "saved.utc", false);
            modifications.Gff.Add(modGFF);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "saved.utc" && f.Destination == "Override");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldIncludeSSFFile_WhenSSFModificationsExist()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var modSSF = new ModificationsSSF("test.ssf", "Override");
            modifications.Ssf.Add(modSSF);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "test.ssf" && f.Destination == "Override");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldMergeExistingInstallFiles()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var installFile = new InstallFile("existing.txt", "existing.txt", "Override");
            modifications.Install.Add(installFile);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "existing.txt" && f.Destination == "Override");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldHandleMultipleModificationTypes()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            
            // TLK
            var tlkMod = new ModificationsTLK("dialog.tlk");
            tlkMod.Modifiers.Add(new ModifyTLK(0, "Test", null, false));
            modifications.Tlk.Add(tlkMod);
            
            // 2DA
            var mod2DA = new Modifications2DA("test.2da", "Override");
            modifications.Twoda.Add(mod2DA);
            
            // GFF
            var modGFF = new ModificationsGFF("test.utc", "Override", "test.utc", false);
            modifications.Gff.Add(modGFF);
            
            // SSF
            var modSSF = new ModificationsSSF("test.ssf", "Override");
            modifications.Ssf.Add(modSSF);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().HaveCount(4);
            result.Should().Contain(f => f.SourceFile == "append.tlk");
            result.Should().Contain(f => f.SourceFile == "test.2da");
            result.Should().Contain(f => f.SourceFile == "test.utc");
            result.Should().Contain(f => f.SourceFile == "test.ssf");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldDeduplicateFiles()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            
            // Add same file multiple times
            var mod2DA1 = new Modifications2DA("test.2da", "Override");
            var mod2DA2 = new Modifications2DA("test.2da", "Override");
            modifications.Twoda.Add(mod2DA1);
            modifications.Twoda.Add(mod2DA2);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().HaveCount(1);
            result.Should().Contain(f => f.SourceFile == "test.2da");
        }

        [Fact]
        public void DetermineInstallFolders_ShouldHandleDotDestination()
        {
            // Arrange
            var modifications = ModificationsByType.CreateEmpty();
            var installFile = new InstallFile("test.txt", "test.txt", ".");
            modifications.Install.Add(installFile);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "test.txt" && f.Destination == "Override");
        }
    }
}

