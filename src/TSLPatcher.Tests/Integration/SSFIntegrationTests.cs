using System;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.SSF;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.SSF;
using Xunit;

namespace TSLPatcher.Tests.Integration;

/// <summary>
/// Integration tests for SSF patching workflows.
/// Ported from test_tslpatcher.py - SSF integration tests.
/// </summary>
public class SSFIntegrationTests : IntegrationTestBase
{
    [Fact]
    public void SSF_Replace_ShouldCreateNewSSF()
    {
        // Arrange
        var ssf = new SSF();
        ssf.SetData(SSFSound.BATTLE_CRY_1, 100);
        ssf.SetData(SSFSound.BATTLE_CRY_2, 200);

        var modifications = new ModificationsSSF("test.ssf", true);
        var modify = new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(999));
        modifications.Modifiers.Add(modify);

        // Act
        modifications.Apply(ssf, Memory, Logger, Game.K1);

        // Assert
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(999);
    }

    [Fact]
    public void SSF_Set_DirectValue_ShouldUpdateSlot()
    {
        // Arrange
        var ssf = new SSF();
        ssf.SetData(SSFSound.BATTLE_CRY_1, 100);

        var modifications = new ModificationsSSF("test.ssf", false);
        var modify = new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(12345));
        modifications.Modifiers.Add(modify);

        // Act
        modifications.Apply(ssf, Memory, Logger, Game.K1);

        // Assert
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(12345);
    }

    [Fact]
    public void SSF_Set_TLKMemory_ShouldUseToken()
    {
        // Arrange
        var ssf = new SSF();
        ssf.SetData(SSFSound.BATTLE_CRY_1, 100);

        Memory.MemoryStr[5] = 67890;

        var modifications = new ModificationsSSF("test.ssf", false);
        var modify = new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(0));
        modify.Stringref = new TokenUsageTLK(5);
        modifications.Modifiers.Add(modify);

        // Act
        modifications.Apply(ssf, Memory, Logger, Game.K1);

        // Assert
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(67890);
    }

    [Fact]
    public void SSF_Set_2DAMemory_ShouldUseToken()
    {
        // Arrange
        var ssf = new SSF();
        ssf.SetData(SSFSound.BATTLE_CRY_1, 100);

        Memory.Memory2DA[3] = "99999";

        var modifications = new ModificationsSSF("test.ssf", false);
        var modify = new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(0));
        modify.Stringref = new TokenUsage2DA(3);
        modifications.Modifiers.Add(modify);

        // Act
        modifications.Apply(ssf, Memory, Logger, Game.K1);

        // Assert
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(99999);
    }

    [Fact]
    public void SSF_SetMultiple_ShouldUpdateAllSlots()
    {
        // Arrange
        var ssf = new SSF();
        ssf.SetData(SSFSound.BATTLE_CRY_1, 100);
        ssf.SetData(SSFSound.BATTLE_CRY_2, 200);
        ssf.SetData(SSFSound.BATTLE_CRY_3, 300);

        var modifications = new ModificationsSSF("test.ssf", false);
        modifications.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(111)));
        modifications.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_2, new NoTokenUsage(222)));
        modifications.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_3, new NoTokenUsage(333)));

        // Act
        modifications.Apply(ssf, Memory, Logger, Game.K1);

        // Assert
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(111);
        ssf.Get(SSFSound.BATTLE_CRY_2).Should().Be(222);
        ssf.Get(SSFSound.BATTLE_CRY_3).Should().Be(333);
    }

    [Fact]
    public void SSF_MixedTokenTypes_ShouldHandleCorrectly()
    {
        // Arrange
        var ssf = new SSF();
        ssf.SetData(SSFSound.BATTLE_CRY_1, 100);
        ssf.SetData(SSFSound.BATTLE_CRY_2, 200);
        ssf.SetData(SSFSound.BATTLE_CRY_3, 300);

        Memory.MemoryStr[1] = 11111;
        Memory.Memory2DA[2] = "22222";

        var modifications = new ModificationsSSF("test.ssf", false);

        var modify1 = new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(999));
        modifications.Modifiers.Add(modify1);

        var modify2 = new ModifySSF(SSFSound.BATTLE_CRY_2, new NoTokenUsage(0));
        modify2.Stringref = new TokenUsageTLK(1);
        modifications.Modifiers.Add(modify2);

        var modify3 = new ModifySSF(SSFSound.BATTLE_CRY_3, new TokenUsage2DA(2));
        modifications.Modifiers.Add(modify3);

        // Act
        modifications.Apply(ssf, Memory, Logger, Game.K1);

        // Assert
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(999);
        ssf.Get(SSFSound.BATTLE_CRY_2).Should().Be(11111);
        ssf.Get(SSFSound.BATTLE_CRY_3).Should().Be(22222);
    }

    [Fact]
    public void SSF_UnsetSlots_ShouldRemainUnchanged()
    {
        // Arrange
        var ssf = new SSF();
        ssf.SetData(SSFSound.BATTLE_CRY_1, 100);
        ssf.SetData(SSFSound.BATTLE_CRY_2, 200);

        var modifications = new ModificationsSSF("test.ssf", false);
        modifications.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(999)));

        // Act
        modifications.Apply(ssf, Memory, Logger, Game.K1);

        // Assert
        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(999);
        ssf.Get(SSFSound.BATTLE_CRY_2).Should().Be(200);
    }

    [Fact]
    public void AssignInt_AppliesPatchCorrectly()
    {
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 1=5
";
        var config = SetupIniAndConfig(iniText);
        var ssf = new SSF();

        var memory = new PatcherMemory();
        var bytes = config.PatchesSSF.First(p => p.SaveAs == "test.ssf").PatchResource(ssf.ToBytes(), memory, new PatchLogger(), Game.K1);
        var patchedSsf = SSF.FromBytes((byte[])bytes);

        patchedSsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(5);
    }

    [Fact]
    public void Assign2DAToken_AppliesPatchCorrectly()
    {
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 2=2DAMEMORY5
";
        var config = SetupIniAndConfig(iniText);
        var ssf = new SSF();

        var memory = new PatcherMemory();
        memory.Memory2DA[5] = "123";
        var bytes = config.PatchesSSF.First(p => p.SaveAs == "test.ssf").PatchResource(ssf.ToBytes(), memory, new PatchLogger(), Game.K1);
        var patchedSsf = SSF.FromBytes((byte[])bytes);

        patchedSsf.Get(SSFSound.BATTLE_CRY_2).Should().Be(123);
    }

    [Fact]
    public void AssignTLKToken_AppliesPatchCorrectly()
    {
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 3=StrRef7
";
        var config = SetupIniAndConfig(iniText);
        var ssf = new SSF();

        var memory = new PatcherMemory();
        memory.MemoryStr[7] = 456;
        var bytes = config.PatchesSSF.First(p => p.SaveAs == "test.ssf").PatchResource(ssf.ToBytes(), memory, new PatchLogger(), Game.K1);
        var patchedSsf = SSF.FromBytes((byte[])bytes);

        patchedSsf.Get(SSFSound.BATTLE_CRY_3).Should().Be(456);
    }
}

