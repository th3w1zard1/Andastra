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
    /// Tests for 2DA AddRow modifications (ported from test_mods.py - TestManipulate2DA)
    /// </summary>
    public class TwoDaAddRowTests
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_RowLabel_UseMaxRowLabel()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1" });
            twoda.AddRow("0", new Dictionary<string, object>());

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, null, new Dictionary<string, RowValue>()));
            config.Modifiers.Add(new AddRow2DA("", null, null, new Dictionary<string, RowValue>()));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal("0", twoda.GetLabel(0));
            Assert.Equal("1", twoda.GetLabel(1));
            Assert.Equal("2", twoda.GetLabel(2));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_RowLabel_UseConstant()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "r1", new Dictionary<string, RowValue>()));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(1, twoda.GetHeight());
            Assert.Equal("r1", twoda.GetLabel(0));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Exclusive_NotExists()
        {
            // Exclusive column is specified and the value in the new row is unique. Add a new row.
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "Col1",
                "2",
                new Dictionary<string, RowValue>()
                {
                    ["Col1"] = new RowValueConstant("g"),
                    ["Col2"] = new RowValueConstant("h"),
                    ["Col3"] = new RowValueConstant("i")
                }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal("2", twoda.GetLabel(2));
            Assert.Equal(new[] { "a", "d", "g" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e", "h" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f", "i" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Exclusive_Exists()
        {
            // Exclusive column is specified but the value in the new row is already used. Edit the existing row.
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });
            twoda.AddRow("2", new Dictionary<string, object>() { ["Col1"] = "g", ["Col2"] = "h", ["Col3"] = "i" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "Col1",
                "3",
                new Dictionary<string, RowValue>()
                {
                    ["Col1"] = new RowValueConstant("g"),
                    ["Col2"] = new RowValueConstant("X"),
                    ["Col3"] = new RowValueConstant("Y")
                }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(3, twoda.GetHeight());
            Assert.Equal(new[] { "a", "d", "g" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e", "X" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f", "Y" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Exclusive_None()
        {
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "a", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "d", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "",
                "2",
                new Dictionary<string, RowValue>()
                {
                    ["Col1"] = new RowValueConstant("g"),
                    ["Col2"] = new RowValueConstant("h"),
                    ["Col3"] = new RowValueConstant("i")
                }
            ));
            config.Modifiers.Add(new AddRow2DA(
                "",
                null,
                "3",
                new Dictionary<string, RowValue>()
                {
                    ["Col1"] = new RowValueConstant("j"),
                    ["Col2"] = new RowValueConstant("k"),
                    ["Col3"] = new RowValueConstant("l")
                }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(4, twoda.GetHeight());
            Assert.Equal(new[] { "a", "d", "g", "j" }, twoda.GetColumn("Col1"));
            Assert.Equal(new[] { "b", "e", "h", "k" }, twoda.GetColumn("Col2"));
            Assert.Equal(new[] { "c", "f", "i", "l" }, twoda.GetColumn("Col3"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_RowLabel_Existing()
        {
            // Python test: test_add_rowlabel_existing
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "123", ["Col2"] = "456" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "Col1",
                null,
                new Dictionary<string, RowValue>()
                {
                    ["Col1"] = new RowValueConstant("123"),
                    ["Col2"] = new RowValueConstant("ABC")
                }
            ));

            // Act - Python uses patch_resource which does bytes round-trip
            byte[] bytes = (byte[])config.PatchResource(twoda.ToBytes(), memory, logger, BioWareGame.K1);
            var patchedTwoda = TwoDAFile.FromBytes((byte[])bytes);

            // Assert
            Assert.Equal(1, patchedTwoda.GetHeight());
            Assert.Equal("0", patchedTwoda.GetLabel(0));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Assign_High()
        {
            // Python test: test_add_assign_high
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "1", ["Col2"] = "b", ["Col3"] = "c" });
            twoda.AddRow("1", new Dictionary<string, object>() { ["Col1"] = "2", ["Col2"] = "e", ["Col3"] = "f" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", "", "2", new Dictionary<string, RowValue>() { ["Col1"] = new RowValueHigh("Col1") }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "1", "2", "3" }, twoda.GetColumn("Col1"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Assign_TLKMemory()
        {
            // Python test: test_add_assign_tlkmemory
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.MemoryStr[0] = 5;
            memory.MemoryStr[1] = 6;

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "0", new Dictionary<string, RowValue>() { ["Col1"] = new RowValueTLKMemory(0) }));
            config.Modifiers.Add(new AddRow2DA("", null, "1", new Dictionary<string, RowValue>() { ["Col1"] = new RowValueTLKMemory(1) }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "5", "6" }, twoda.GetColumn("Col1"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Assign_2DAMemory()
        {
            // Python test: test_add_assign_2damemory
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            memory.Memory2DA[0] = "5";
            memory.Memory2DA[1] = "6";

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "0", new Dictionary<string, RowValue>() { ["Col1"] = new RowValue2DAMemory(0) }));
            config.Modifiers.Add(new AddRow2DA("", null, "1", new Dictionary<string, RowValue>() { ["Col1"] = new RowValue2DAMemory(1) }));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(new[] { "5", "6" }, twoda.GetColumn("Col1"));
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_2DAMemory_RowIndex()
        {
            // Python test: test_add_2damemory_rowindex
            // Arrange
            var twoda = new TwoDAFile(new List<string> { "Col1" });
            twoda.AddRow("0", new Dictionary<string, object>() { ["Col1"] = "X" });

            var logger = new PatchLogger();
            var memory = new PatcherMemory();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "Col1",
                "1",
                new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("X") },
                new Dictionary<int, RowValue>() { [5] = new RowValueRowIndex() }
            ));
            config.Modifiers.Add(new AddRow2DA(
                "",
                null,
                "2",
                new Dictionary<string, RowValue>() { ["Col1"] = new RowValueConstant("Y") },
                new Dictionary<int, RowValue>() { [6] = new RowValueRowIndex() }
            ));

            // Act
            config.Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            Assert.Equal(2, twoda.GetHeight());
            Assert.Equal(new[] { "X", "Y" }, twoda.GetColumn("Col1"));
            Assert.Equal("0", memory.Memory2DA[5]);
            Assert.Equal("1", memory.Memory2DA[6]);
        }
    }
}
