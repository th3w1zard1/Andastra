// Comprehensive tests for InstallFolderDeterminer
// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py:1212-1236
using System.Collections.Generic;
using Andastra.Parsing.Mods;
using Andastra.Parsing.Mods.GFF;
using Andastra.Parsing.Mods.SSF;
using Andastra.Parsing.Mods.TLK;
using Andastra.Parsing.Mods.TwoDA;
using Andastra.Parsing.TSLPatcher;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Generator
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
            var appendMod = new ModifyTLK(0, false); // Append
            appendMod.Text = "Test";
            tlkMod.Modifiers.Add(appendMod);
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
            var replaceMod = new ModifyTLK(0, true); // Replacement
            replaceMod.Text = "Test";
            tlkMod.Modifiers.Add(replaceMod);
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
            var mod2DA = new Modifications2DA("test.2da");
            mod2DA.Destination = "Override";
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
            var mod2DA = new Modifications2DA("test.2da");
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
            var modGFF = new ModificationsGFF("test.utc", false);
            modGFF.Destination = "Override";
            modGFF.SaveAs = "test.utc";
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
            var modGFF = new ModificationsGFF("source.utc", false);
            modGFF.Destination = "Override";
            modGFF.SaveAs = "saved.utc";
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
            var modSSF = new ModificationsSSF("test.ssf", false);
            modSSF.Destination = "Override";
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
            var installFile = new InstallFile("existing.txt", null, "Override");
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
            var tlkModifier = new ModifyTLK(0, false);
            tlkModifier.Text = "Test";
            tlkMod.Modifiers.Add(tlkModifier);
            modifications.Tlk.Add(tlkMod);

            // 2DA
            var mod2DA = new Modifications2DA("test.2da");
            mod2DA.Destination = "Override";
            modifications.Twoda.Add(mod2DA);

            // GFF
            var modGFF = new ModificationsGFF("test.utc", false);
            modGFF.Destination = "Override";
            modGFF.SaveAs = "test.utc";
            modifications.Gff.Add(modGFF);

            // SSF
            var modSSF = new ModificationsSSF("test.ssf", false);
            modSSF.Destination = "Override";
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
            var mod2DA1 = new Modifications2DA("test.2da");
            mod2DA1.Destination = "Override";
            var mod2DA2 = new Modifications2DA("test.2da");
            mod2DA2.Destination = "Override";
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
            var installFile = new InstallFile("test.txt", null, ".");
            modifications.Install.Add(installFile);

            // Act
            var result = InstallFolderDeterminer.DetermineInstallFolders(modifications);

            // Assert
            result.Should().Contain(f => f.SourceFile == "test.txt" && f.Destination == "Override");
        }
    }
}

