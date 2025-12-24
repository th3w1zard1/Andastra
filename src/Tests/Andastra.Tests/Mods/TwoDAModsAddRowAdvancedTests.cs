using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.TwoDA;
using FluentAssertions;
using Xunit;
using TwoDAFile = Andastra.Parsing.Formats.TwoDA.TwoDA;

namespace Andastra.Parsing.Tests.Mods
{

    /// <summary>
    /// Advanced unit tests for AddRow2DA with High() function and memory token storage.
    /// </summary>
    public class TwoDAModsAddRowAdvancedTests
    {
        private static TwoDAFile CreateTest2DA(List<string> columns, params (string label, Dictionary<string, object> data)[] rows)
        {
            var twoda = new TwoDAFile(columns);
            foreach ((string label, Dictionary<string, object> data) in rows)
            {
                twoda.AddRow(label, data);
            }
            return twoda;
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_WithHigh_ShouldCalculateHighestValuePlusOne()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2", "Col3" },
                ("0", new Dictionary<string, object> { { "Col1", "1" }, { "Col2", "b" }, { "Col3", "c" } }),
                ("1", new Dictionary<string, object> { { "Col1", "2" }, { "Col2", "e" }, { "Col3", "f" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "",
                "2",
                new Dictionary<string, RowValue> { { "Col1", new RowValueHigh("Col1") } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("1", "2", "3");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_WithTLKMemory_ShouldUseTLKMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(new List<string> { "Col1" });

            var memory = new PatcherMemory();
            memory.MemoryStr[0] = 5;
            memory.MemoryStr[1] = 6;
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "0", new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(0) } }));
            config.Modifiers.Add(new AddRow2DA("", null, "1", new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(1) } }));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("5", "6");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_With2DAMemory_ShouldUse2DAMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(new List<string> { "Col1" });

            var memory = new PatcherMemory();
            memory.Memory2DA[0] = "5";
            memory.Memory2DA[1] = "6";
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "0", new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(0) } }));
            config.Modifiers.Add(new AddRow2DA("", null, "1", new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(1) } }));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("5", "6");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Store2DAMemoryRowIndex_WithExclusive_ShouldStoreExistingRowIndex()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1" },
                ("0", new Dictionary<string, object> { { "Col1", "X" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var addRow = new AddRow2DA(
                "",
                "Col1",
                "1",
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("X") } }
            );
            addRow.Store2DA.Add(5, new RowValueRowIndex());
            config.Modifiers.Add(addRow);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            twoda.GetColumn("Col1").Should().Equal("X");
            memory.Memory2DA[5].Should().Be("0");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_Store2DAMemoryRowIndex_WithoutExclusive_ShouldStoreNewRowIndex()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1" },
                ("0", new Dictionary<string, object> { { "Col1", "X" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var addRow = new AddRow2DA(
                "",
                null,
                "2",
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("Y") } }
            );
            addRow.Store2DA.Add(5, new RowValueRowIndex());
            config.Modifiers.Add(addRow);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(2);
            twoda.GetColumn("Col1").Should().Equal("X", "Y");
            memory.Memory2DA[5].Should().Be("1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_MultipleTokensStoredInDifferentMemorySlots()
        {
            TwoDAFile twoda = CreateTest2DA(new List<string> { "Col1" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");

            var addRow1 = new AddRow2DA("", null, "0", new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("A") } });
            addRow1.Store2DA.Add(5, new RowValueRowIndex());
            addRow1.Store2DA.Add(6, new RowValueRowLabel());
            config.Modifiers.Add(addRow1);

            var addRow2 = new AddRow2DA("", null, "1", new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("B") } });
            addRow2.Store2DA.Add(7, new RowValueRowIndex());
            addRow2.Store2DA.Add(8, new RowValueRowLabel());
            config.Modifiers.Add(addRow2);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            memory.Memory2DA[5].Should().Be("0");
            memory.Memory2DA[6].Should().Be("0");
            memory.Memory2DA[7].Should().Be("1");
            memory.Memory2DA[8].Should().Be("1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_StoreRowLabelFromTokenUsage()
        {
            TwoDAFile twoda = CreateTest2DA(new List<string> { "Col1" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var addRow = new AddRow2DA(
                "",
                null,
                "my_row",
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("X") } }
            );
            addRow.Store2DA.Add(5, new RowValueRowLabel());
            config.Modifiers.Add(addRow);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetLabel(0).Should().Be("my_row");
            memory.Memory2DA[5].Should().Be("my_row");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_StoreCellDataFromTokenUsage()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var addRow = new AddRow2DA(
                "",
                null,
                "1",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("new_val") },
                { "Col2", new RowValueConstant("other") }
                }
            );
            addRow.Store2DA.Add(5, new RowValueRowCell("Col1"));
            config.Modifiers.Add(addRow);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            memory.Memory2DA[5].Should().Be("new_val");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_WithHighAndTokenStorage_ShouldStoreCalculatedValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1" },
                ("0", new Dictionary<string, object> { { "Col1", "5" } }),
                ("1", new Dictionary<string, object> { { "Col1", "10" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var addRow = new AddRow2DA(
                "",
                null,
                "2",
                new Dictionary<string, RowValue> { { "Col1", new RowValueHigh("Col1") } }
            );
            addRow.Store2DA.Add(5, new RowValueRowCell("Col1"));
            config.Modifiers.Add(addRow);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("5", "10", "11");
            memory.Memory2DA[5].Should().Be("11");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ExclusiveMatchExisting_TokenUsageShouldReferToExistingRow()
        {
            TwoDAFile twoda = CreateTest2DA(new List<string> { "Col1", "Col2" },
                ("original_label", new Dictionary<string, object> { { "Col1", "unique_val" }, { "Col2", "old" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var addRow = new AddRow2DA(
                "",
                "Col1",
                "new_label",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("unique_val") },
                { "Col2", new RowValueConstant("new") }
                }
            );
            addRow.Store2DA.Add(5, new RowValueRowLabel());
            config.Modifiers.Add(addRow);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            twoda.GetCellString(0, "Col2").Should().Be("new");
            memory.Memory2DA[5].Should().Be("original_label");
        }

    }
}
