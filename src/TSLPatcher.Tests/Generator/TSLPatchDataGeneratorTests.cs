// Comprehensive tests for TSLPatchDataGenerator
// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/src/pykotor/tslpatcher/diff/generator.py
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CSharpKOTOR.Common;
using CSharpKOTOR.Formats.GFF;
using CSharpKOTOR.Formats.SSF;
using CSharpKOTOR.Formats.TLK;
using CSharpKOTOR.Formats.TwoDA;
using TLKAuto = CSharpKOTOR.Formats.TLK.TLKAuto;
using TwoDAAuto = CSharpKOTOR.Formats.TwoDA.TwoDAAuto;
using GFFAuto = CSharpKOTOR.Formats.GFF.GFFAuto;
using SSFAuto = CSharpKOTOR.Formats.SSF.SSFAuto;
using CSharpKOTOR.Mods;
using CSharpKOTOR.Mods.GFF;
using CSharpKOTOR.Mods.SSF;
using CSharpKOTOR.Mods.TLK;
using CSharpKOTOR.Mods.TwoDA;
using CSharpKOTOR.Memory;
using CSharpKOTOR.TSLPatcher;
using FluentAssertions;
using Xunit;

namespace CSharpKOTOR.Tests.Generator
{
    /// <summary>
    /// Comprehensive tests for TSLPatchDataGenerator.
    /// Tests all file generation methods: TLK, 2DA, GFF, SSF, and install file copying.
    /// </summary>
    public class TSLPatchDataGeneratorTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly DirectoryInfo _tslpatchdataPath;

        public TSLPatchDataGeneratorTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _tslpatchdataPath = new DirectoryInfo(Path.Combine(_tempDir, "tslpatchdata"));
            _tslpatchdataPath.Create();
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
        public void Constructor_ShouldCreateDirectory_WhenPathDoesNotExist()
        {
            // Arrange
            string nonExistentPath = Path.Combine(_tempDir, "new_tslpatchdata");
            var dir = new DirectoryInfo(nonExistentPath);
            if (dir.Exists)
            {
                dir.Delete(true);
            }

            // Act
            var generator = new TSLPatchDataGenerator(dir);

            // Assert
            dir.Exists.Should().BeTrue();
        }

        [Fact]
        public void GenerateAllFiles_ShouldReturnEmptyDictionary_WhenNoModifications()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();

            // Act
            var result = generator.GenerateAllFiles(modifications);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public void GenerateAllFiles_ShouldGenerateTLKFile_WhenTLKModificationsExist()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            var tlkMod = new ModificationsTLK("dialog.tlk");
            var appendMod = new ModifyTLK(0, false);
            appendMod.Text = "Test Text";
            tlkMod.Modifiers.Add(appendMod);
            modifications.Tlk.Add(tlkMod);

            // Act
            var result = generator.GenerateAllFiles(modifications);

            // Assert
            result.Should().ContainKey("append.tlk");
            var appendTlkPath = result["append.tlk"];
            appendTlkPath.Exists.Should().BeTrue();
            
            // Verify TLK file can be read
            var tlk = TLKAuto.Load(appendTlkPath.FullName);
            tlk.Should().NotBeNull();
            tlk.Count.Should().BeGreaterThan(0);
        }

        [Fact]
        public void GenerateAllFiles_ShouldGenerate2DAFile_When2DAModificationsExist()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            // Create a base 2DA file in temp dir
            var base2DAPath = Path.Combine(_tempDir, "test.2da");
            var twoda = new TwoDA(new List<string> { "Col1", "Col2" });
            twoda.AddRow(null, new Dictionary<string, object> { { "Col1", "Value1" }, { "Col2", "Value2" } });
            twoda.Save(base2DAPath);

            var mod2DA = new Modifications2DA("test.2da");
            modifications.Twoda.Add(mod2DA);

            // Act
            var result = generator.GenerateAllFiles(modifications, new DirectoryInfo(_tempDir));

            // Assert
            result.Should().ContainKey("test.2da");
            var generated2DAPath = result["test.2da"];
            generated2DAPath.Exists.Should().BeTrue();
            
            // Verify 2DA file can be read
            var generated2DA = TwoDAAuto.Load(generated2DAPath.FullName);
            generated2DA.Should().NotBeNull();
            generated2DA.GetHeaders().Should().Contain("Col1");
            generated2DA.GetHeaders().Should().Contain("Col2");
        }

        [Fact]
        public void GenerateAllFiles_ShouldGenerateGFFFile_WhenGFFModificationsExist()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            var modGFF = new ModificationsGFF("test.utc", false);
            var addField = new AddFieldGFF("Tag", "Tag", GFFFieldType.Int32, new FieldValueConstant(42), "");
            modGFF.Modifiers.Add(addField);
            modifications.Gff.Add(modGFF);

            // Act
            var result = generator.GenerateAllFiles(modifications);

            // Assert
            result.Should().ContainKey("test.utc");
            var generatedGFFPath = result["test.utc"];
            generatedGFFPath.Exists.Should().BeTrue();
            
            // Verify GFF file can be read
            var gff = GFFAuto.Load(generatedGFFPath.FullName);
            gff.Should().NotBeNull();
        }

        [Fact]
        public void GenerateAllFiles_ShouldGenerateSSFFile_WhenSSFModificationsExist()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            var modSSF = new ModificationsSSF("test.ssf", false);
            var modifySSF = new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage("new_sound"));
            modSSF.Modifiers.Add(modifySSF);
            modifications.Ssf.Add(modSSF);

            // Act
            var result = generator.GenerateAllFiles(modifications);

            // Assert
            result.Should().ContainKey("test.ssf");
            var generatedSSFPath = result["test.ssf"];
            generatedSSFPath.Exists.Should().BeTrue();
        }

        [Fact]
        public void GenerateAllFiles_ShouldHandleMultipleModificationTypes()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            // Add TLK modification
            var tlkMod = new ModificationsTLK("dialog.tlk");
            var tlkModifier = new ModifyTLK(0, false);
            tlkModifier.Text = "Test";
            tlkMod.Modifiers.Add(tlkModifier);
            modifications.Tlk.Add(tlkMod);
            
            // Add 2DA modification
            var mod2DA = new Modifications2DA("test.2da");
            modifications.Twoda.Add(mod2DA);
            
            // Add GFF modification
            var modGFF = new ModificationsGFF("test.utc", false);
            modifications.Gff.Add(modGFF);

            // Act
            var result = generator.GenerateAllFiles(modifications);

            // Assert
            result.Should().HaveCountGreaterThan(0);
            result.Should().ContainKey("append.tlk");
        }

        [Fact]
        public void GenerateAllFiles_ShouldCopyInstallFiles_WhenInstallModificationsExist()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            // Create a test file to copy
            var testFile = Path.Combine(_tempDir, "test_file.txt");
            File.WriteAllText(testFile, "Test content");
            
            var installFile = new InstallFile("test_file.txt", null, "Override");
            modifications.Install.Add(installFile);

            // Act
            var result = generator.GenerateAllFiles(modifications, new DirectoryInfo(_tempDir));

            // Assert
            result.Should().ContainKey("test_file.txt");
            var copiedFile = result["test_file.txt"];
            copiedFile.Exists.Should().BeTrue();
            File.ReadAllText(copiedFile.FullName).Should().Be("Test content");
        }

        [Fact]
        public void GenerateAllFiles_ShouldHandleBaseDataPath_WhenProvided()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            // Create base 2DA file
            var baseDir = new DirectoryInfo(Path.Combine(_tempDir, "base"));
            baseDir.Create();
            var base2DAPath = Path.Combine(baseDir.FullName, "Override", "test.2da");
            Directory.CreateDirectory(Path.GetDirectoryName(base2DAPath));
            
            var twoda = new TwoDA(new List<string> { "Col1" });
            twoda.AddRow(null, new Dictionary<string, object> { { "Col1", "Value1" } });
            twoda.Save(base2DAPath);
            
            var mod2DA = new Modifications2DA("test.2da");
            modifications.Twoda.Add(mod2DA);

            // Act
            var result = generator.GenerateAllFiles(modifications, baseDir);

            // Assert
            result.Should().ContainKey("test.2da");
            result["test.2da"].Exists.Should().BeTrue();
        }

        [Fact]
        public void GenerateAllFiles_ShouldHandleNullBaseDataPath()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            
            var modGFF = new ModificationsGFF("new.utc", false);
            modifications.Gff.Add(modGFF);

            // Act
            var result = generator.GenerateAllFiles(modifications, null);

            // Assert
            result.Should().ContainKey("new.utc");
            result["new.utc"].Exists.Should().BeTrue();
        }

        [Fact]
        public void GenerateAllFiles_ShouldHandleEmptyModifications()
        {
            // Arrange
            var generator = new TSLPatchDataGenerator(_tslpatchdataPath);
            var modifications = ModificationsByType.CreateEmpty();
            modifications.Tlk = new List<ModificationsTLK>();
            modifications.Twoda = new List<Modifications2DA>();
            modifications.Gff = new List<ModificationsGFF>();
            modifications.Ssf = new List<ModificationsSSF>();
            modifications.Install = new List<InstallFile>();

            // Act
            var result = generator.GenerateAllFiles(modifications);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }
    }
}

