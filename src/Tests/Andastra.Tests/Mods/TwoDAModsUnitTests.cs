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
    /// Unit tests for 2DA modification classes without using ConfigReader.
    /// </summary>
    public class TwoDAModsUnitTests
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
        public void ChangeRow_ByRowIndex_ShouldModifyCorrectRow()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("X") } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "X");
            twoda.GetColumn("Col2").Should().Equal("b", "e");
            twoda.GetColumn("Col3").Should().Equal("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_ByRowLabel_ShouldModifyCorrectRow()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_LABEL, "1"),
                new Dictionary<string, RowValue> { { "Col1", new RowValueConstant("X") } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "X");
            twoda.GetColumn("Col2").Should().Equal("b", "e");
            twoda.GetColumn("Col3").Should().Equal("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_ByLabelIndex_ShouldModifyCorrectRow()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "label", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();
            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.LABEL_COLUMN, "d"),
                new Dictionary<string, RowValue> { { "Col2", new RowValueConstant("X") } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("label").Should().Equal("a", "d");
            twoda.GetColumn("Col2").Should().Equal("b", "X");
            twoda.GetColumn("Col3").Should().Equal("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_WithTLKMemory_ShouldUseMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            memory.MemoryStr[0] = 0;
            memory.MemoryStr[1] = 1;
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(0) } }
            ));
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue> { { "Col1", new RowValueTLKMemory(1) } }
            ));

            byte[] bytes = (byte[])config.PatchResource(twoda.ToBytes(), memory, logger, BioWareGame.K1);
            var patchedTwoda = TwoDAFile.FromBytes((byte[])bytes);

            patchedTwoda.GetColumn("Col1").Should().Equal("0", "1");
            patchedTwoda.GetColumn("Col2").Should().Equal("b", "e");
            patchedTwoda.GetColumn("Col3").Should().Equal("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_With2DAMemory_ShouldUseMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            memory.Memory2DA[0] = "mem0";
            memory.Memory2DA[1] = "mem1";
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(0) } }
            ));
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue> { { "Col1", new RowValue2DAMemory(1) } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("mem0", "mem1");
            twoda.GetColumn("Col2").Should().Equal("b", "e");
            twoda.GetColumn("Col3").Should().Equal("c", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_WithHigh_ShouldCalculateHighestValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { " ", "3", "5" }),
                ("1", new[] { "2", "4", "6" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col1", new RowValueHigh("Col1") } }
            ));
            config.Modifiers.Add(new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                new Dictionary<string, RowValue> { { "Col2", new RowValueHigh("Col2") } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("3", "2");
            twoda.GetColumn("Col2").Should().Equal("5", "4");
            twoda.GetColumn("Col3").Should().Equal("5", "6");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Store2DAMemoryRowIndex_ShouldStoreIndex()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var change = new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>()
            );
            change.Store2DA.Add(5, new RowValueRowIndex());
            config.Modifiers.Add(change);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "d");
            twoda.GetColumn("Col2").Should().Equal("b", "e");
            twoda.GetColumn("Col3").Should().Equal("c", "f");
            memory.Memory2DA[5].Should().Be("1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Store2DAMemoryRowLabel_ShouldStoreLabel()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("r1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var change = new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>()
            );
            change.Store2DA.Add(5, new RowValueRowLabel());
            config.Modifiers.Add(change);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "d");
            twoda.GetColumn("Col2").Should().Equal("b", "e");
            twoda.GetColumn("Col3").Should().Equal("c", "f");
            memory.Memory2DA[5].Should().Be("r1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_Store2DAMemoryColumnLabel_ShouldStoreValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "label", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var change = new ChangeRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 1),
                new Dictionary<string, RowValue>()
            );
            change.Store2DA.Add(5, new RowValueRowCell("label"));
            config.Modifiers.Add(change);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("label").Should().Equal("a", "d");
            twoda.GetColumn("Col2").Should().Equal("b", "e");
            twoda.GetColumn("Col3").Should().Equal("c", "f");
            memory.Memory2DA[5].Should().Be("d");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_AutoRowLabel_ShouldIncrementFromMax()
        {
            TwoDAFile twoda = CreateTest2DA(new[] { "Col1" }, ("0", new string[0]));

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, null, new Dictionary<string, RowValue>()));
            config.Modifiers.Add(new AddRow2DA("", null, null, new Dictionary<string, RowValue>()));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);
            twoda.GetLabel(0).Should().Be("0");
            twoda.GetLabel(1).Should().Be("1");
            twoda.GetLabel(2).Should().Be("2");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ExplicitRowLabel_ShouldUseProvidedLabel()
        {
            TwoDAFile twoda = CreateTest2DA(new[] { "Col1" });

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA("", null, "r1", new Dictionary<string, RowValue>()));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            twoda.GetLabel(0).Should().Be("r1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ExclusiveColumnNotExists_ShouldAddRow()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "Col1",
                "2",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("g") },
                { "Col2", new RowValueConstant("h") },
                { "Col3", new RowValueConstant("i") }
                }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);
            twoda.GetLabel(2).Should().Be("2");
            twoda.GetColumn("Col1").Should().Equal("a", "d", "g");
            twoda.GetColumn("Col2").Should().Equal("b", "e", "h");
            twoda.GetColumn("Col3").Should().Equal("c", "f", "i");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_ExclusiveColumnExists_ShouldUpdateExisting()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "Col1",
                null,
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("d") },
                { "Col2", new RowValueConstant("X") },
                { "Col3", new RowValueConstant("Y") }
                }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(2);
            twoda.GetColumn("Col1").Should().Equal("a", "d");
            twoda.GetColumn("Col2").Should().Equal("b", "X");
            twoda.GetColumn("Col3").Should().Equal("c", "Y");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_NoExclusiveColumn_ShouldAlwaysAdd()
        {
            TwoDAFile twoda = CreateTest2DA(
                new[] { "Col1", "Col2", "Col3" },
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new AddRow2DA(
                "",
                "",
                "2",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("g") },
                { "Col2", new RowValueConstant("h") },
                { "Col3", new RowValueConstant("i") }
                }
            ));
            config.Modifiers.Add(new AddRow2DA(
                "",
                null,
                "3",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("j") },
                { "Col2", new RowValueConstant("k") },
                { "Col3", new RowValueConstant("l") }
                }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(4);
            twoda.GetColumn("Col1").Should().Equal("a", "d", "g", "j");
            twoda.GetColumn("Col2").Should().Equal("b", "e", "h", "k");
            twoda.GetColumn("Col3").Should().Equal("c", "f", "i", "l");
        }
    }
}
