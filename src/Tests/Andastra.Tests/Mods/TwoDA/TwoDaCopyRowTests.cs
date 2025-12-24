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
    /// Tests for 2DA CopyRow modifications (ported from test_mods.py - TestManipulate2DA)
    /// </summary>
    public class TwoDaCopyRowTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Existing_RowIndex()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>() { ["Col2"] = new RowValueConstant("X") }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal(new[] { "a", "c", "a" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d", "X" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Existing_RowLabel()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_LABEL, "1"),
                null,
                null,
                new Dictionary<string, RowValue>() { ["Col2"] = new RowValueConstant("X") }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal(new[] { "a", "c", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d", "X" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Exclusive_NotExists()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                "Col1",
                null,
                new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("c"), ["Col2"] = new RowValueConstant("d") }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(2, twoda.GetHeight());
            Assert.Equal("1", twoda.GetLabel(1));
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Exclusive_Exists()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                "Col1",
                null,
                new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("a"), ["Col2"] = new RowValueConstant("X") }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(1, twoda.GetHeight());
            Assert.Equal("0", twoda.GetLabel(0));
            Assert.Equal(new[] { "a" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "X" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Exclusive_None()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("c"), ["Col2"] = new RowValueConstant("d") }
            ));
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                "",
                "r2",
                new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("e"), ["Col2"] = new RowValueConstant("f") }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal("1", twoda.GetLabel(1));
            Assert.Equal("r2", twoda.GetLabel(2));
            Assert.Equal(new[] { "a", "c", "e" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d", "f" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_SetNewRowLabel()
        {
            // Python test: test_copy_set_newrowlabel
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA("", new Target(TargetType.ROW_INDEX, 0), null, "r2", new Dictionary<string, RowValue>()));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal("r2", twoda.GetLabel(2));
            Assert.Equal(new[] { "a", "c", "a" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d", "b" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Assign_High()
        {
            // Python test: test_copy_assign_high
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "1" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "2" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>() { ["Col2"] = new RowValueHigh("Col2") }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal(new[] { "a", "c", "a" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "1", "2", "3" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Assign_TLKMemory()
        {
            // Python test: test_copy_assign_tlkmemory
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "1" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "2" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.MemoryStr[0] = 5;

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>() { ["Col2"] = new RowValueTLKMemory(0) }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c", "a" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "1", "2", "5" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Assign_2DAMemory()
        {
            // Python test: test_copy_assign_2damemory
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "1" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "2" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.Memory2DA[0] = "5";

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>() { ["Col2"] = new RowValue2DAMemory(0) }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c", "a" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "1", "2", "5" }, twoda.GetColumn("Col2"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_2DAMemory_RowIndex()
        {
            // Python test: test_copy_2damemory_rowindex
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>(),
                new Dictionary<int, RowValue>() { [5] = new RowValueRowIndex() }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c", "a" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d", "b" }, twoda.GetColumn("Col2"));
            Assert.Equal("2", memory.Memory2DA[5]);
        }
    }
}

