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
    /// Tests for 2DA AddColumn modifications (ported from test_mods.py - TestManipulate2DA)
    /// </summary>
    public class TwoDaAddColumnTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Empty()
        {
            // Python test: test_addcolumn_empty
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "", "" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Default()
        {
            // Python test: test_addcolumn_default
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "X", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "X", "X" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowIndex_Constant()
        {
            // Python test: test_addcolumn_rowindex_constant
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue> { [0] = new RowValueConstant("X") }, new Dictionary<string, RowValue>()));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "X", "" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabel_2DAMemory()
        {
            // Python test: test_addcolumn_rowlabel_2damemory
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "ABC";

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue> { ["1"] = new RowValue2DAMemory(5) }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "", "ABC" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabel_TLKMemory()
        {
            // Python test: test_addcolumn_rowlabel_tlkmemory
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.MemoryStr[5] = 123;

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue> { ["1"] = new RowValueTLKMemory(5) }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "", "123" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_2DAMemory_Index()
        {
            // Python test: test_addcolumn_2damemory_index
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            var addCol = new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue> { [0] = new RowValueConstant("X"), [1] = new RowValueConstant("Y") }, new Dictionary<string, RowValue>());
            addCol.Store2DA.Add(0, "I0");
            config.Modifiers.Add(addCol);

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "X", "Y" }, twoda.GetColumn("Col3"));
            Assert.Equal("X", memory.Memory2DA[0]);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_2DAMemory_Line()
        {
            // Python test: test_addcolumn_2damemory_line
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "c", ["Col2"] = "d" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            var addCol = new AddColumn2DA("", "Col3", "", new Dictionary<int, RowValue> { [0] = new RowValueConstant("X"), [1] = new RowValueConstant("Y") }, new Dictionary<string, RowValue>());
            addCol.Store2DA.Add(0, "L1");
            config.Modifiers.Add(addCol);

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "X", "Y" }, twoda.GetColumn("Col3"));
            Assert.Equal("Y", memory.Memory2DA[0]);
        }
    }
}
