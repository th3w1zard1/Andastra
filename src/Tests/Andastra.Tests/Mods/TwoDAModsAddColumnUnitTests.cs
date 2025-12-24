using System.Collections.Generic;
using System.Linq;
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
    /// Unit tests for AddColumn2DA modifications without using ConfigReader.
    /// </summary>
    public class TwoDAModsAddColumnUnitTests
    {
        private static TwoDAFile CreateTest2DA(string[] columns, params (string label, string[] values)[] rows)
        {
            var twoda = new TwoDAFile(columns.ToList());
            foreach ((string label, string[] values) in rows)
            {
                var cells = new Dictionary<string, object>();
                for (int i = 0; i < values.Length && i < columns.Length; i++)
                {
                    cells[columns[i]] = values[i];
                }
                twoda.AddRow(label, cells);
            }
            return twoda;
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Empty_ShouldAddEmptyColumn()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA(
                "",
                "Col3",
                "",
                new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, string>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("Col3").Should().Equal("", "");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_WithDefault_ShouldFillWithDefaultValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA(
                "",
                "Col3",
                "X",
                new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, string>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("Col3").Should().Equal("X", "X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowIndexConstant_ShouldSetSpecificRowValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA(
                "",
                "Col3",
                "",
                new Dictionary<int, RowValue> { { 0, new RowValueConstant("X") } },
                new Dictionary<string, RowValue>(),
                new Dictionary<int, string>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("Col3").Should().Equal("X", "");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabel2DAMemory_ShouldUseMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            );

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "ABC";
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA(
                "",
                "Col3",
                "",
                new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue> { { "1", new RowValue2DAMemory(5) } },
                new Dictionary<int, string>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("Col3").Should().Equal("", "ABC");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabelTLKMemory_ShouldUseMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            );

            var memory = new PatcherMemory();
            memory.MemoryStr[5] = 123;
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA(
                "",
                "Col3",
                "",
                new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue> { { "1", new RowValueTLKMemory(5) } },
                new Dictionary<int, string>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("Col3").Should().Equal("", "123");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Store2DAMemoryFromIndex_ShouldStoreValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var addColumn = new AddColumn2DA(
                "",
                "Col3",
                "",
                new Dictionary<int, RowValue>
                {
                { 0, new RowValueConstant("X") },
                { 1, new RowValueConstant("Y") }
                },
                new Dictionary<string, RowValue>(),
                new Dictionary<int, string> { { 0, "I0" } } // Python: store_2da={0: "I0"}
            );
            config.Modifiers.Add(addColumn);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("Col3").Should().Equal("X", "Y");
            // Python: assert memory.memory_2da[0] == "X" (from row index 0, column Col3)
            memory.Memory2DA[0].Should().Be("X");
        }


        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_WithMultipleIndexAndLabelInserts_ShouldApplyAll()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1" },
                ("0", new[] { "a" }),
                ("1", new[] { "b" }),
                ("row2", new[] { "c" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA(
                "",
                "NewCol",
                "default",
                new Dictionary<int, RowValue> { { 0, new RowValueConstant("idx0") } },
                new Dictionary<string, RowValue> { { "row2", new RowValueConstant("lbl2") } },
                new Dictionary<int, string>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("NewCol").Should().Equal("idx0", "default", "lbl2");
        }


        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_ToEmptyTable_ShouldStillAddColumn()
        {
            TwoDAFile twoda = CreateTest2DA(new[] { "Col1" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA(
                "",
                "Col2",
                "X",
                new Dictionary<int, RowValue>(),
                new Dictionary<string, RowValue>(),
                new Dictionary<int, string>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeaders().Should().Contain("Col2");
            twoda.GetHeight().Should().Be(0);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_MultipleColumns_ShouldAddInOrder()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1" },
                ("0", new[] { "a" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddColumn2DA("", "Col2", "X", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>(), new Dictionary<int, string>()));
            config.Modifiers.Add(new AddColumn2DA("", "Col3", "Y", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>(), new Dictionary<int, string>()));
            config.Modifiers.Add(new AddColumn2DA("", "Col4", "Z", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>(), new Dictionary<int, string>()));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeaders().Should().Equal("Col1", "Col2", "Col3", "Col4");
            twoda.GetCellString(0, "Col2").Should().Be("X");
            twoda.GetCellString(0, "Col3").Should().Be("Y");
            twoda.GetCellString(0, "Col4").Should().Be("Z");
        }

    }
}
