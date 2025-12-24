using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.TLK;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Tests for TLK modification functionality.
    /// 1:1 port from tests/tslpatcher/test_mods.py (TestManipulateTLK)
    /// </summary>
    public class TlkModsTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestApplyAppend()
        {
            // Python test: test_apply_append
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

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
            dialogTlk.Add("Old1");
            dialogTlk.Add("Old2");

            config.Apply(dialogTlk, memory, logger, BioWareGame.K1);

            dialogTlk.Count.Should().Be(4);
            dialogTlk.Get(2).Text.Should().Be("Append2");
            dialogTlk.Get(3).Text.Should().Be("Append1");

            memory.MemoryStr[0].Should().Be(2);
            memory.MemoryStr[1].Should().Be(3);

            // [Dialog] [Append] [Token] [Text]
            // 0        -        -       Old1
            // 1        -        -       Old2
            // 2        1        0       Append2
            // 3        0        1       Append1
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestApplyReplace()
        {
            // Python test: test_apply_replace
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new ModificationsTLK();

            var m1 = new ModifyTLK(1, isReplacement: true)
            {
                Text = "Replace2",
                Sound = ResRef.FromBlank()
            };

            var m2 = new ModifyTLK(2, isReplacement: true)
            {
                Text = "Replace3",
                Sound = ResRef.FromBlank()
            };

            config.Modifiers.Add(m1);
            config.Modifiers.Add(m2);

            var dialogTlk = new TLK();
            dialogTlk.Add("Old1");
            dialogTlk.Add("Old2");
            dialogTlk.Add("Old3");
            dialogTlk.Add("Old4");

            config.Apply(dialogTlk, memory, logger, BioWareGame.K1);

            dialogTlk.Count.Should().Be(4);
            dialogTlk[0].Text.Should().Be("Old1");
            dialogTlk[1].Text.Should().Be("Replace2");
            dialogTlk[2].Text.Should().Be("Replace3");
            dialogTlk[3].Text.Should().Be("Old4");

            // Replace operations do NOT store memory (Python line 146: dialog.replace only, no memory assignment)
            memory.MemoryStr.Should().NotContainKey(1);
            memory.MemoryStr.Should().NotContainKey(2);

            // [Dialog] [Replace] [Token] [Text]
            // 0        -          -       Old1
            // 1        1          -       Replace2 (no memory stored)
            // 2        1          -       Replace3 (no memory stored)
            // 3        -          -       Old4
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TestPatchResource()
        {
            // Test that PatchResource does a full round-trip through bytes
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new ModificationsTLK();
            var m1 = new ModifyTLK(0)
            {
                Text = "NewEntry",
                Sound = ResRef.FromBlank()
            };
            config.Modifiers.Add(m1);

            // Create a TLK and serialize it
            var originalTlk = new TLK();
            originalTlk.Add("Original1");
            originalTlk.Add("Original2");

            var writer = new TLKBinaryWriter(originalTlk);
            byte[] sourceBytes = writer.Write();

            // Patch via PatchResource (this does bytes -> TLK -> modify -> bytes)
            byte[] patchedBytes = (byte[])config.PatchResource(sourceBytes, memory, logger, BioWareGame.K1);

            // Read back the patched TLK
            var reader = new TLKBinaryReader(patchedBytes);
            TLK patchedTlk = reader.Load();

            patchedTlk.Count.Should().Be(3);
            patchedTlk[0].Text.Should().Be("Original1");
            patchedTlk[1].Text.Should().Be("Original2");
            patchedTlk[2].Text.Should().Be("NewEntry");

            memory.MemoryStr[0].Should().Be(2);
        }

    }
}
