using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.SSF;
using Xunit;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Tests for SSF modifications (ported from test_mods.py - TestManipulateSSF)
    /// </summary>
    public class SsfModificationTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_Assign_Int()
        {
            // Arrange
            var ssf = new SSF();
            var memory = new PatcherMemory();

            var config = new ModificationsSSF("", false, new List<ModifySSF>());
            config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(5)));

            // Act
            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            ssf = SSF.FromBytes((byte[])bytes);

            // Assert
            Assert.Equal(5, ssf.Get(SSFSound.BATTLE_CRY_1));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_Assign_2DAToken()
        {
            // Arrange
            var ssf = new SSF();
            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123";

            var config = new ModificationsSSF("", false, new List<ModifySSF>());
            config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_2, new TokenUsage2DA(5)));

            // Act
            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            ssf = SSF.FromBytes((byte[])bytes);

            // Assert
            Assert.Equal(123, ssf.Get(SSFSound.BATTLE_CRY_2));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_Assign_TLKToken()
        {
            // Arrange
            var ssf = new SSF();
            var memory = new PatcherMemory();
            memory.MemoryStr[5] = 321;

            var config = new ModificationsSSF("", false, new List<ModifySSF>());
            config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_3, new TokenUsageTLK(5)));

            // Act
            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            ssf = SSF.FromBytes((byte[])bytes);

            // Assert
            Assert.Equal(321, ssf.Get(SSFSound.BATTLE_CRY_3));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Apply_Multiple_Assignments()
        {
            // Arrange
            var ssf = new SSF();
            var memory = new PatcherMemory();
            memory.Memory2DA[1] = "100";
            memory.MemoryStr[2] = 200;

            var config = new ModificationsSSF("", false, new List<ModifySSF>());
            config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_1, new NoTokenUsage(50)));
            config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_2, new TokenUsage2DA(1)));
            config.Modifiers.Add(new ModifySSF(SSFSound.BATTLE_CRY_3, new TokenUsageTLK(2)));

            // Act
            object bytes = config.PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            ssf = SSF.FromBytes((byte[])bytes);

            // Assert
            Assert.Equal(50, ssf.Get(SSFSound.BATTLE_CRY_1));
            Assert.Equal(100, ssf.Get(SSFSound.BATTLE_CRY_2));
            Assert.Equal(200, ssf.Get(SSFSound.BATTLE_CRY_3));
        }
    }
}
