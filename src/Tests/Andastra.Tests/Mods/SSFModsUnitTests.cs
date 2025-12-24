using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.SSF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Direct unit tests for SSF modification operations.
    /// </summary>
    public class SSFModsUnitTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_ConstantInt_ShouldSetValue()
        {
            var ssf = new SSF();
            var memory = new PatcherMemory();

            var config = new ModificationsSSF("", false, new List<ModifySSF>
        {
            new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(5))
        });

            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(5);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_2DAToken_ShouldUseMemoryValue()
        {
            var ssf = new SSF();
            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123";

            var config = new ModificationsSSF("", false, new List<ModifySSF>
        {
            new ModifySSF(SSFSound.BATTLE_CRY_2, new TokenUsage2DA(5))
        });

            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.BATTLE_CRY_2).Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_TLKToken_ShouldUseMemoryValue()
        {
            var ssf = new SSF();
            var memory = new PatcherMemory();
            memory.MemoryStr[5] = 321;

            var config = new ModificationsSSF("", false, new List<ModifySSF>
        {
            new ModifySSF(SSFSound.BATTLE_CRY_3, new TokenUsageTLK(5))
        });

            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.BATTLE_CRY_3).Should().Be(321);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_MultipleSounds_ShouldSetAll()
        {
            var ssf = new SSF();
            var memory = new PatcherMemory();

            var config = new ModificationsSSF("", false, new List<ModifySSF>
        {
            new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(10)),
            new ModifySSF(SSFSound.BATTLE_CRY_2, new NoTokenUsage(20)),
            new ModifySSF(SSFSound.BATTLE_CRY_3, new NoTokenUsage(30)),
            new ModifySSF(SSFSound.SELECT_1, new NoTokenUsage(40)),
            new ModifySSF(SSFSound.SELECT_2, new NoTokenUsage(50))
        });

            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(10);
            patchedSsf.Get(SSFSound.BATTLE_CRY_2).Should().Be(20);
            patchedSsf.Get(SSFSound.BATTLE_CRY_3).Should().Be(30);
            patchedSsf.Get(SSFSound.SELECT_1).Should().Be(40);
            patchedSsf.Get(SSFSound.SELECT_2).Should().Be(50);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_WithMixed2DAAndTLKTokens_ShouldResolveAll()
        {
            var ssf = new SSF();
            var memory = new PatcherMemory();
            memory.Memory2DA[1] = "100";
            memory.Memory2DA[2] = "200";
            memory.MemoryStr[3] = 300;
            memory.MemoryStr[4] = 400;

            var config = new ModificationsSSF("", false, new List<ModifySSF>
        {
            new ModifySSF(SSFSound.BATTLE_CRY_1, new TokenUsage2DA(1)),
            new ModifySSF(SSFSound.BATTLE_CRY_2, new TokenUsage2DA(2)),
            new ModifySSF(SSFSound.BATTLE_CRY_3, new TokenUsageTLK(3)),
            new ModifySSF(SSFSound.SELECT_1, new TokenUsageTLK(4))
        });

            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(100);
            patchedSsf.Get(SSFSound.BATTLE_CRY_2).Should().Be(200);
            patchedSsf.Get(SSFSound.BATTLE_CRY_3).Should().Be(300);
            patchedSsf.Get(SSFSound.SELECT_1).Should().Be(400);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_EmptySSF_ShouldInitializeCorrectly()
        {
            var ssf = new SSF();
            var memory = new PatcherMemory();

            var config = new ModificationsSSF("", false, new List<ModifySSF>
        {
            new ModifySSF(SSFSound.CRITICAL_HIT, new NoTokenUsage(999))
        });

            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.CRITICAL_HIT).Should().Be(999);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_OverwriteExisting_ShouldReplace()
        {
            var ssf = new SSF();
            ssf.SetData(SSFSound.BATTLE_CRY_1, 1);
            ssf.SetData(SSFSound.BATTLE_CRY_2, 2);

            var memory = new PatcherMemory();

            var config = new ModificationsSSF("", false, new List<ModifySSF>
        {
            new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(100)),
            new ModifySSF(SSFSound.BATTLE_CRY_2, new NoTokenUsage(200))
        });

            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(100);
            patchedSsf.Get(SSFSound.BATTLE_CRY_2).Should().Be(200);
        }


        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Assign_AllSSFSounds_ShouldSetCorrectly()
        {
            var ssf = new SSF();
            var memory = new PatcherMemory();
            var modifiers = new System.Collections.Generic.List<ModifySSF>();

            int value = 1;
            foreach (SSFSound sound in System.Enum.GetValues(typeof(SSFSound)))
            {
                modifiers.Add(new ModifySSF(sound, new NoTokenUsage(value++)));
            }

            var config = new ModificationsSSF("", false, modifiers);
            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            value = 1;
            foreach (SSFSound sound in System.Enum.GetValues(typeof(SSFSound)))
            {
                patchedSsf.Get(sound).Should().Be(value++);
            }
        }

    }
}
