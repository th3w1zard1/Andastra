using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.TwoDA;
using Xunit;
using TwoDAFile = Andastra.Parsing.Formats.TwoDA.TwoDA;

namespace Andastra.Parsing.Tests.Mods.TwoDA
{

    /// <summary>
    /// Tests for 2DA ChangeRow modifications (ported from test_mods.py - TestManipulate2DA)
    /// </summary>
    public class TwoDaChangeRowTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Existing_RowIndex()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1), new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("X") }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "X" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Existing_RowLabel()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_LABEL, "1"), new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("X") }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "X" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Existing_LabelIndex()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "label", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["label"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["label"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.LABEL_COLUMN, "d"), new Dictionary<string, RowValue>() { ["Col2"] = new RowValueConstant("X") }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "d" }, twoda.GetColumn("label"));
            Assert.Equal(new[] { "b", "X" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Assign_TLKMemory()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.MemoryStr[0] = 0;
            memory.MemoryStr[1] = 1;

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0), new Dictionary<string, RowValue>() { ["Col1"] = new RowValueTLKMemory(0) }));
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1), new Dictionary<string, RowValue>() { ["Col1"] = new RowValueTLKMemory(1) }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "0", "1" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Assign_2DAMemory()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.Memory2DA[0] = "mem0";
            memory.Memory2DA[1] = "mem1";

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0), new Dictionary<string, RowValue>() { ["Col1"] = new RowValue2DAMemory(0) }));
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 1), new Dictionary<string, RowValue>() { ["Col1"] = new RowValue2DAMemory(1) }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "mem0", "mem1" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Assign_High()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = " ", ["Col2"] = "3", ["Col3"] = "5" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "2", ["Col2"] = "4", ["Col3"] = "6" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0), new Dictionary<string, RowValue>() { ["Col1"] = new RowValueHigh("Col1") }));
            config.Modifiers.Add(new ChangeRow2DA("", new Target(TargetType.ROW_INDEX, 0), new Dictionary<string, RowValue>() { ["Col2"] = new RowValueHigh("Col2") }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "3", "2" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "5", "4" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "5", "6" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Set2DAMemory_RowIndex()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue>() { [5] = new RowValueRowIndex() }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "d" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
            Assert.Equal("1", memory.Memory2DA[5]);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Set2DAMemory_RowLabel()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("r1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue>() { [5] = new RowValueRowLabel() }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "d" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
            Assert.Equal("r1", memory.Memory2DA[5]);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Set2DAMemory_ColumnLabel()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "label", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["label"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["label"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue>() { [5] = new RowValueRowCell("label") }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "d" }, twoda.GetColumn("label"));
            Assert.Equal(new[] { "b", "e" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f" }, twoda.GetColumn("Col3"));
            Assert.Equal("d", memory.Memory2DA[5]);
        }
    }
}
