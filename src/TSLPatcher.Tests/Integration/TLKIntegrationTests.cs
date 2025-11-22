using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TLK;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.TLK;
using TSLPatcher.Core.Resources;
using Xunit;

namespace TSLPatcher.Tests.Integration;

/// <summary>
/// Integration tests for TLK patching workflows.
/// Ported from test_tslpatcher.py - TLK integration tests.
/// </summary>
public class TLKIntegrationTests : IntegrationTestBase
{
    [Fact]
    public void TLK_Append_ShouldAddNewEntry()
    {
        // Arrange
        var tlk = CreateTestTLK(new[]
        {
            ("Entry 0", "vo_0"),
            ("Entry 1", "vo_1")
        });

        var modify = new ModifyTLK(0, false);
        modify.Text = "New Entry";
        modify.Sound = new ResRef("new_vo");

        // Act
        var modifications = new ModificationsTLK("test.tlk", false);
        modifications.Modifiers.Add(modify);
        modifications.Apply(tlk, Memory, Logger, Game.K1);

        // Assert
        tlk.Count.Should().Be(3);
        tlk.String(2).Should().Be("New Entry");
        tlk[2].Voiceover.ToString().Should().Be("new_vo");
    }

    [Fact]
    public void TLK_Replace_ShouldModifyExistingEntry()
    {
        // Arrange
        var tlk = CreateTestTLK(new[]
        {
            ("Entry 0", "vo_0"),
            ("Entry 1", "vo_1"),
            ("Entry 2", "vo_2")
        });

        var modify = new ModifyTLK(0, true);
        modify.ModIndex = 1;
        modify.Text = "Modified Entry";
        modify.Sound = new ResRef("modified_vo");

        // Act
        var modifications = new ModificationsTLK("test.tlk", false);
        modifications.Modifiers.Add(modify);
        modifications.Apply(tlk, Memory, Logger, Game.K1);

        // Assert
        tlk.Count.Should().Be(3);
        tlk.String(1).Should().Be("Modified Entry");
        tlk[1].Voiceover.ToString().Should().Be("modified_vo");
    }

    [Fact]
    public void TLK_AppendMultiple_ShouldAddAllEntries()
    {
        // Arrange
        var tlk = CreateTestTLK(new[]
        {
            ("Entry 0", "vo_0")
        });

        var modify1 = new ModifyTLK(0, false);
        modify1.Text = "New Entry 1";
        modify1.Sound = new ResRef("new_vo_1");

        var modify2 = new ModifyTLK(1, false);
        modify2.Text = "New Entry 2";
        modify2.Sound = new ResRef("new_vo_2");

        // Act
        var modifications = new ModificationsTLK("test.tlk", false);
        modifications.Modifiers.Add(modify1);
        modifications.Modifiers.Add(modify2);
        modifications.Apply(tlk, Memory, Logger, Game.K1);

        // Assert
        tlk.Count.Should().Be(3);
        tlk.String(1).Should().Be("New Entry 1");
        tlk.String(2).Should().Be("New Entry 2");
    }

    [Fact]
    public void TLK_WithMemoryTokens_ShouldStoreStrRef()
    {
        // Arrange
        var tlk = CreateTestTLK(new[]
        {
            ("Entry 0", "vo_0")
        });

        var modify = new ModifyTLK(0, false);
        modify.Text = "New Entry";
        modify.Sound = new ResRef("new_vo");

        // Act
        var modifications = new ModificationsTLK("test.tlk", false);
        modifications.Modifiers.Add(modify);
        modifications.Apply(tlk, Memory, Logger, Game.K1);

        // Assert
        Memory.MemoryStr[0].Should().Be(1);
    }

    [Fact]
    public void TLK_LoadFromFile_ShouldPopulateFromTLK()
    {
        // Arrange
        var sourceTlk = CreateTestTLK(new[]
        {
            ("Source 0", "src_0"),
            ("Source 1", "src_1"),
            ("Source 2", "src_2")
        });
        string tlkPath = Path.Combine(TslPatchDataPath, "source.tlk");
        SaveTestTLK("source.tlk", sourceTlk);

        var targetTlk = CreateTestTLK(new[]
        {
            ("Entry 0", "vo_0")
        });

        var modify = new ModifyTLK(0, false);
        modify.TlkFilePath = tlkPath;
        modify.ModIndex = 1;

        // Act
        modify.Load();
        var modifications = new ModificationsTLK("test.tlk", false);
        modifications.Modifiers.Add(modify);
        modifications.Apply(targetTlk, Memory, Logger, Game.K1);

        // Assert
        targetTlk.String(1).Should().Be("Source 1");
        targetTlk[1].Voiceover.ToString().Should().Be("src_1");
    }

    [Fact]
    public void TLK_ReplaceWithNull_ShouldClearText()
    {
        // Arrange
        var tlk = CreateTestTLK(new[]
        {
            ("Entry 0", "vo_0"),
            ("Entry 1", "vo_1")
        });

        var modify = new ModifyTLK(0, true);
        modify.ModIndex = 1;
        modify.Text = null;
        modify.Sound = null;

        // Act
        var modifications = new ModificationsTLK("test.tlk", false);
        modifications.Modifiers.Add(modify);
        modifications.Apply(tlk, Memory, Logger, Game.K1);

        // Assert
        tlk.String(1).Should().BeNullOrEmpty();
    }

    [Fact]
    public void TLK_ComplexWorkflow_ShouldHandleMixedOperations()
    {
        // Arrange
        var tlk = CreateTestTLK(new[]
        {
            ("Entry 0", "vo_0"),
            ("Entry 1", "vo_1"),
            ("Entry 2", "vo_2")
        });

        var replace = new ModifyTLK(0, true);
        replace.ModIndex = 1;
        replace.Text = "Replaced";
        replace.Sound = new ResRef("replaced_vo");

        var append = new ModifyTLK(1, false);
        append.Text = "Appended";
        append.Sound = new ResRef("appended_vo");

        // Act
        var modifications = new ModificationsTLK("test.tlk", false);
        modifications.Modifiers.Add(replace);
        modifications.Modifiers.Add(append);
        modifications.Apply(tlk, Memory, Logger, Game.K1);

        // Assert
        tlk.Count.Should().Be(4);
        tlk.String(1).Should().Be("Replaced");
        tlk.String(3).Should().Be("Appended");
        Memory.MemoryStr[0].Should().Be(1);
        Memory.MemoryStr[1].Should().Be(3);
    }

    [Fact]
    public void TLK_ApplyAppend_AppliesPatchCorrectly()
    {
        string iniText = @"
[TLKList]
StrRef0=0
StrRef1=1

[append.tlk]
0=0
1=1
";
        // Create append.tlk file
        var appendTlk = CreateTestTLK(new[]
        {
            ("Append2", ""),
            ("Append1", "")
        });
        SaveTestTLK("append.tlk", appendTlk);

        var config = SetupIniAndConfig(iniText);
        var dialogTlk = new TLK();
        dialogTlk.Add("Old1");
        dialogTlk.Add("Old2");

        var memory = new PatcherMemory();
        config.PatchesTLK.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        dialogTlk.Count.Should().Be(4);
        dialogTlk.String(2).Should().Be("Append2");
        dialogTlk.String(3).Should().Be("Append1");
        memory.MemoryStr[0].Should().Be(2);
        memory.MemoryStr[1].Should().Be(3);
    }

    [Fact]
    public void TLK_ApplyReplace_AppliesPatchCorrectly()
    {
        string iniText = @"
[TLKList]
ReplaceFile0=replace.tlk

[replace.tlk]
1=0
2=1
";
        // Create replace.tlk file
        var replaceTlk = CreateTestTLK(new[]
        {
            ("Replace2", ""),
            ("Replace3", "")
        });
        SaveTestTLK("replace.tlk", replaceTlk);

        var config = SetupIniAndConfig(iniText);
        var dialogTlk = new TLK();
        dialogTlk.Add("Old1");
        dialogTlk.Add("Old2");
        dialogTlk.Add("Old3");
        dialogTlk.Add("Old4");

        var memory = new PatcherMemory();
        config.PatchesTLK.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        dialogTlk.Count.Should().Be(4);
        dialogTlk.String(1).Should().Be("Replace2");
        dialogTlk.String(2).Should().Be("Replace3");
        // Replace operations no longer store memory
        memory.MemoryStr.Should().NotContainKey(1);
        memory.MemoryStr.Should().NotContainKey(2);
    }
}

