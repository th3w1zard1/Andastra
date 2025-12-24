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
    /// Unit tests for CopyRow2DA modifications without using ConfigReader.
    /// </summary>
    public class TwoDAModsCopyRowUnitTests
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
        public void CopyRow_ByRowIndex_ShouldCopyAndModify()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } }),
                ("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueConstant("X") } }
            ));

            object bytes = config.PatchResource(twoda.ToBytes(), memory, logger, BioWareGame.K1);
            var patchedTwoda = TwoDAFile.FromBytes((byte[])bytes);

            patchedTwoda.GetHeight().Should().Be(3);
            patchedTwoda.GetColumn("Col1").Should().Equal("a", "c", "a");
            patchedTwoda.GetColumn("Col2").Should().Equal("b", "d", "X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ByRowLabel_ShouldCopyAndModify()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } }),
                ("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_LABEL, "1"),
                null,
                null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueConstant("X") } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);
            twoda.GetColumn("Col1").Should().Equal("a", "c", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d", "X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ExclusiveNotExists_ShouldAddRow()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                "Col1",
                null,
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("c") },
                { "Col2", new RowValueConstant("d") }
                }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(2);
            twoda.GetLabel(1).Should().Be("1");
            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_ExclusiveExists_ShouldUpdateExisting()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                "Col1",
                null,
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("a") },
                { "Col2", new RowValueConstant("X") }
                }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            twoda.GetLabel(0).Should().Be("0");
            twoda.GetColumn("Col1").Should().Equal("a");
            twoda.GetColumn("Col2").Should().Equal("X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_NoExclusive_ShouldAlwaysAdd()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("c") },
                { "Col2", new RowValueConstant("d") }
                }
            ));
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                "",
                "r2",
                new Dictionary<string, RowValue>
                {
                { "Col1", new RowValueConstant("e") },
                { "Col2", new RowValueConstant("f") }
                }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);
            twoda.GetLabel(1).Should().Be("1");
            twoda.GetLabel(2).Should().Be("r2");
            twoda.GetColumn("Col1").Should().Equal("a", "c", "e");
            twoda.GetColumn("Col2").Should().Equal("b", "d", "f");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_WithNewRowLabel_ShouldUseProvidedLabel()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2", "Col3" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } }),
                ("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "d" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                "r2",
                new Dictionary<string, RowValue>()
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetLabel(2).Should().Be("r2");
            twoda.GetColumn("Col1").Should().Equal("a", "c", "a");
            twoda.GetColumn("Col2").Should().Equal("b", "d", "b");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_WithHigh_ShouldCalculateHighest()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2", "Col3" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "1" } }),
                ("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "2" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueHigh("Col2") } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);
            twoda.GetColumn("Col1").Should().Equal("a", "c", "a");
            twoda.GetColumn("Col2").Should().Equal("1", "2", "3");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_WithTLKMemory_ShouldUseMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(
                new List<string> { "Col1", "Col2", "Col3" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "1" } }),
                ("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "2" } })
            );

            var memory = new PatcherMemory();
            memory.MemoryStr[0] = 5;
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue> { { "Col2", new RowValueTLKMemory(0) } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c", "a");
            twoda.GetColumn("Col2").Should().Equal("1", "2", "5");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_With2DAMemory_ShouldUseMemoryValue()
        {
            TwoDAFile twoda = CreateTest2DA(new List<string> { "Col1", "Col2" },
                ("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "1" } }),
                ("1", new Dictionary<string, object> { { "Col1", "c" }, { "Col2", "2" } })
            );

            var memory = new PatcherMemory();
            memory.Memory2DA[0] = "999";
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            config.Modifiers.Add(new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue> { { "Col2", new RowValue2DAMemory(0) } }
            ));

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetColumn("Col1").Should().Equal("a", "c", "a");
            twoda.GetColumn("Col2").Should().Equal("1", "2", "999");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_Store2DAMemoryRowIndex_ShouldStoreNewRowIndex()
        {
            TwoDAFile twoda = CreateTest2DA(new List<string> { "Col1" },
                ("0", new Dictionary<string, object> { { "Col1", "a" } }),
                ("1", new Dictionary<string, object> { { "Col1", "b" } })
            );

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            var config = new Modifications2DA("");
            var copy = new CopyRow2DA(
                "",
                new Target(TargetType.ROW_INDEX, 0),
                null,
                null,
                new Dictionary<string, RowValue>(),
                store2da: new Dictionary<int, RowValue> { { 5, new RowValueRowIndex() } }
            );
            config.Modifiers.Add(copy);

            config.Apply(twoda, memory, logger, BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);
            memory.Memory2DA[5].Should().Be("2");
        }

    }
}
