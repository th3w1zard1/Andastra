using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.TLK;
using Xunit;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Tests for TLK modifications (ported from test_mods.py - TestManipulateTLK)
    /// </summary>
    public class TlkModificationTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_Append()
        {
            // Arrange
            var memory = new PatcherMemory();
            var config = new ModificationsTLK();

            var m1 = new ModifyTLK(0)
            {
                Text = "Append2",
                Sound = ResRef.FromBlank()
            };
            var m2 = new ModifyTLK(1)
            {
                Text = "Append1",
                Sound = ResRef.FromBlank()
            };

            config.Modifiers.Add(m1);
            config.Modifiers.Add(m2);

            var dialogTlk = new TLK();
            dialogTlk.Add("Old1", "");
            dialogTlk.Add("Old2", "");

            // Act
            config.Apply(dialogTlk, memory, new PatchLogger(), BioWareGame.K1);

            // Assert
            Assert.Equal(4, dialogTlk.Count);
            TLKEntry entry2 = dialogTlk.Get(2);
            TLKEntry entry3 = dialogTlk.Get(3);
            Assert.NotNull(entry2);
            Assert.NotNull(entry3);
            Assert.Equal("Append2", entry2.Text);
            Assert.Equal("Append1", entry3.Text);

            Assert.Equal(2, memory.MemoryStr[0]);
            Assert.Equal(3, memory.MemoryStr[1]);

            // [Dialog] [Append] [Token] [Text]
            // 0        -        -       Old1
            // 1        -        -       Old2
            // 2        1        0       Append2
            // 3        0        1       Append1
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_Replace()
        {
            // Arrange
            var memory = new PatcherMemory();
            var config = new ModificationsTLK();

            var m1 = new ModifyTLK(1)
            {
                Text = "Replace2",
                Sound = ResRef.FromBlank(),
                IsReplacement = true
            };
            var m2 = new ModifyTLK(2)
            {
                Text = "Replace3",
                Sound = ResRef.FromBlank(),
                IsReplacement = true
            };

            config.Modifiers.Add(m1);
            config.Modifiers.Add(m2);

            var dialogTlk = new TLK();
            dialogTlk.Add("Old1", "");
            dialogTlk.Add("Old2", "");
            dialogTlk.Add("Old3", "");
            dialogTlk.Add("Old4", "");

            // Act
            config.Apply(dialogTlk, memory, new PatchLogger(), BioWareGame.K1);

            // Assert
            Assert.Equal(4, dialogTlk.Count);
            TLKEntry entry1 = dialogTlk.Get(1);
            TLKEntry entry2 = dialogTlk.Get(2);
            Assert.NotNull(entry1);
            Assert.NotNull(entry2);
            Assert.Equal("Replace2", entry1.Text);
            Assert.Equal("Replace3", entry2.Text);

            // Replace operations do NOT store memory (Python line 146: dialog.replace only, no memory assignment)
            Assert.False(memory.MemoryStr.ContainsKey(1));
            Assert.False(memory.MemoryStr.ContainsKey(2));
        }
    }
}
