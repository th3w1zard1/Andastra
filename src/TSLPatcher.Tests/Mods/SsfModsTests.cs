using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.SSF;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.SSF;
using Xunit;

namespace TSLPatcher.Tests.Mods;

/// <summary>
/// Tests for SSF modification functionality
/// 1:1 port of tests/tslpatcher/test_mods.py (TestManipulateSSF)
/// </summary>
public class SsfModsTests
{
    /// <summary>
    /// Python: test_assign_int
    /// Tests assigning constant integer value to SSF sound
    /// </summary>
    [Fact]
    public void TestAssignInt()
    {
        var ssf = new SSF();
        var memory = new PatcherMemory();

        var config = new ModificationsSSF("", false);
        config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(5)));

        // Python does: ssf = read_ssf(config.patch_resource(bytes_ssf(ssf), memory, PatchLogger(), Game.K1))
        // Full round-trip through bytes
        var writer = new SSFBinaryWriter(ssf);
        byte[] data = writer.Write();
        byte[] patchedData = (byte[])config.PatchResource(data, memory, new PatchLogger(), Game.K2);
        var reader = new SSFBinaryReader(patchedData);
        ssf = reader.Load();

        ssf.Get(SSFSound.BATTLE_CRY_1).Should().Be(5);
    }

    /// <summary>
    /// Python: test_assign_2datoken
    /// Tests assigning value from memory_2da using TokenUsage2DA
    /// </summary>
    [Fact]
    public void TestAssign2DAToken()
    {
        var ssf = new SSF();
        var memory = new PatcherMemory { Memory2DA = { [5] = "123" } };

        var config = new ModificationsSSF("", false);
        config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_2, new TokenUsage2DA(5)));

        // Full round-trip through bytes
        var writer = new SSFBinaryWriter(ssf);
        byte[] data = writer.Write();
        byte[] patchedData = (byte[])config.PatchResource(data, memory, new PatchLogger(), Game.K2);
        var reader = new SSFBinaryReader(patchedData);
        ssf = reader.Load();

        ssf.Get(SSFSound.BATTLE_CRY_2).Should().Be(123);
    }

    /// <summary>
    /// Python: test_assign_tlktoken
    /// Tests assigning value from memory_str using TokenUsageTLK
    /// </summary>
    [Fact]
    public void TestAssignTLKToken()
    {
        var ssf = new SSF();
        var memory = new PatcherMemory { MemoryStr = { [5] = 321 } };

        var config = new ModificationsSSF("", false);
        config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_3, new TokenUsageTLK(5)));

        // Full round-trip through bytes
        var writer = new SSFBinaryWriter(ssf);
        byte[] data = writer.Write();
        byte[] patchedData = (byte[])config.PatchResource(data, memory, new PatchLogger(), Game.K2);
        var reader = new SSFBinaryReader(patchedData);
        ssf = reader.Load();

        ssf.Get(SSFSound.BATTLE_CRY_3).Should().Be(321);
    }
}

