using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TLK;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.TLK;
using TSLPatcher.Core.Logger;
using Xunit;

namespace TSLPatcher.Tests.Mods;

/// <summary>
/// Direct unit tests for TLK modification operations.
/// </summary>
public class TLKModsUnitTests
{
    [Fact]
    public void Apply_Append_ShouldAddNewEntries()
    {
        var memory = new PatcherMemory();
        var config = new ModificationsTLK();

        var m1 = new ModifyTLK(0) { Text = "Append2", Sound = ResRef.FromBlank() };
        var m2 = new ModifyTLK(1) { Text = "Append1", Sound = ResRef.FromBlank() };
        config.Modifiers.Add(m1);
        config.Modifiers.Add(m2);

        var dialogTlk = new TLK();
        dialogTlk.Add("Old1");
        dialogTlk.Add("Old2");

        config.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        dialogTlk.Count.Should().Be(4);
        dialogTlk.Get(2)!.Text.Should().Be("Append2");
        dialogTlk.Get(3)!.Text.Should().Be("Append1");

        memory.MemoryStr[0].Should().Be(2);
        memory.MemoryStr[1].Should().Be(3);
    }

    [Fact]
    public void Apply_Replace_ShouldModifyExistingEntries()
    {
        var memory = new PatcherMemory();
        var config = new ModificationsTLK();

        var m1 = new ModifyTLK(1) { Text = "Replace2", Sound = ResRef.FromBlank(), IsReplacement = true };
        var m2 = new ModifyTLK(2) { Text = "Replace3", Sound = ResRef.FromBlank(), IsReplacement = true };
        config.Modifiers.Add(m1);
        config.Modifiers.Add(m2);

        var dialogTlk = new TLK();
        dialogTlk.Add("Old1");
        dialogTlk.Add("Old2");
        dialogTlk.Add("Old3");
        dialogTlk.Add("Old4");

        config.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        dialogTlk.Count.Should().Be(4);
        dialogTlk.Get(1)!.Text.Should().Be("Replace2");
        dialogTlk.Get(2)!.Text.Should().Be("Replace3");

        // Replace operations do NOT store memory (Python line 146: dialog.replace only, no memory assignment)
        memory.MemoryStr.Should().NotContainKey(1);
        memory.MemoryStr.Should().NotContainKey(2);
    }

    [Fact]
    public void Apply_AppendMultiple_ShouldStoreCorrectTokens()
    {
        var memory = new PatcherMemory();
        var config = new ModificationsTLK();

        for (int i = 0; i < 5; i++)
        {
            config.Modifiers.Add(new ModifyTLK(i) { Text = $"Text{i}", Sound = ResRef.FromBlank() });
        }

        var dialogTlk = new TLK();
        dialogTlk.Add("Existing1");
        dialogTlk.Add("Existing2");

        config.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        dialogTlk.Count.Should().Be(7);
        memory.MemoryStr[0].Should().Be(2);
        memory.MemoryStr[1].Should().Be(3);
        memory.MemoryStr[2].Should().Be(4);
        memory.MemoryStr[3].Should().Be(5);
        memory.MemoryStr[4].Should().Be(6);
    }

    [Fact]
    public void Apply_WithSoundResRef_ShouldSetSound()
    {
        var memory = new PatcherMemory();
        var config = new ModificationsTLK();

        var m1 = new ModifyTLK(0) { Text = "Test", Sound = new ResRef("testsound") };
        config.Modifiers.Add(m1);

        var dialogTlk = new TLK();
        config.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        TLKEntry? entry = dialogTlk.Get(0);
        entry.Should().NotBeNull();
        entry!.Voiceover.ToString().Should().Be("testsound");
    }

    [Fact]
    public void Apply_ReplaceOutOfBounds_ShouldHandleGracefully()
    {
        var memory = new PatcherMemory();
        var config = new ModificationsTLK();

        var m1 = new ModifyTLK(999) { Text = "Replace", Sound = ResRef.FromBlank(), IsReplacement = true };
        config.Modifiers.Add(m1);

        var dialogTlk = new TLK();
        dialogTlk.Add("Old1");

        // Should not throw, but should log warning
        var logger = new PatchLogger();
        config.Apply(dialogTlk, memory, logger, Game.K1);

        dialogTlk.Count.Should().Be(1);
        dialogTlk.Get(0)!.Text.Should().Be("Old1");
    }

    [Fact]
    public void Apply_MixedAppendAndReplace_ShouldApplyCorrectly()
    {
        var memory = new PatcherMemory();
        var config = new ModificationsTLK();

        var m1 = new ModifyTLK(0) { Text = "Append1", Sound = ResRef.FromBlank() };
        var m2 = new ModifyTLK(0) { Text = "Replace0", Sound = ResRef.FromBlank(), IsReplacement = true };
        var m3 = new ModifyTLK(1) { Text = "Append2", Sound = ResRef.FromBlank() };
        config.Modifiers.Add(m1);
        config.Modifiers.Add(m2);
        config.Modifiers.Add(m3);

        var dialogTlk = new TLK();
        dialogTlk.Add("Original");

        config.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        dialogTlk.Count.Should().Be(3);
        dialogTlk.Get(0)!.Text.Should().Be("Replace0");
        dialogTlk.Get(1)!.Text.Should().Be("Append1");
        dialogTlk.Get(2)!.Text.Should().Be("Append2");

        memory.MemoryStr[0].Should().Be(1);
        memory.MemoryStr[1].Should().Be(2);
    }

    [Fact]
    public void Apply_EmptyTLK_ShouldAppendCorrectly()
    {
        var memory = new PatcherMemory();
        var config = new ModificationsTLK();

        var m1 = new ModifyTLK(0) { Text = "First", Sound = ResRef.FromBlank() };
        config.Modifiers.Add(m1);

        var dialogTlk = new TLK();
        config.Apply(dialogTlk, memory, new PatchLogger(), Game.K1);

        dialogTlk.Count.Should().Be(1);
        dialogTlk.Get(0)!.Text.Should().Be("First");
        memory.MemoryStr[0].Should().Be(0);
    }
}

