using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.TwoDA;
using Xunit;
using TwoDAFile = TSLPatcher.Core.Formats.TwoDA.TwoDA;

namespace TSLPatcher.Tests.Mods;

/// <summary>
/// Unit tests for AddColumn2DA modifications without using ConfigReader.
/// </summary>
public class TwoDAModsAddColumnUnitTests
{
    private TwoDAFile CreateTestTwoDA(string[] columns, params (string label, string[] values)[] rows)
    {
        var twoda = new TwoDAFile(columns.ToList());
        foreach (var (label, values) in rows)
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

    [Fact]
    public void AddColumn_Empty_ShouldAddEmptyColumn()
    {
        var twoda = CreateTestTwoDA(
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

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("", "");
    }

    [Fact]
    public void AddColumn_WithDefault_ShouldFillWithDefaultValue()
    {
        var twoda = CreateTestTwoDA(
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

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "X");
    }

    [Fact]
    public void AddColumn_RowIndexConstant_ShouldSetSpecificRowValue()
    {
        var twoda = CreateTestTwoDA(
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

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "");
    }

    [Fact]
    public void AddColumn_RowLabel2DAMemory_ShouldUseMemoryValue()
    {
        var twoda = CreateTestTwoDA(
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

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("", "ABC");
    }

    [Fact]
    public void AddColumn_RowLabelTLKMemory_ShouldUseMemoryValue()
    {
        var twoda = CreateTestTwoDA(
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

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("", "123");
    }

    [Fact]
    public void AddColumn_Store2DAMemoryFromIndex_ShouldStoreValue()
    {
        var twoda = CreateTestTwoDA(
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
            new Dictionary<int, string> { { 0, "0" } }
        );
        config.Modifiers.Add(addColumn);

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "Y");
        memory.Memory2DA[0].Should().Be("X");
    }

    [Fact]
    public void AddColumn_Store2DAMemoryFromLabel_ShouldStoreValue()
    {
        var twoda = CreateTestTwoDA(
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
            new Dictionary<int, string> { { 0, "0" } }
        );
        config.Modifiers.Add(addColumn);

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "Y");
        memory.Memory2DA[0].Should().Be("Y");
    }

    [Fact]
    public void AddColumn_WithMultipleIndexAndLabelInserts_ShouldApplyAll()
    {
        var twoda = CreateTestTwoDA(
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

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetColumn("NewCol").Should().Equal("idx0", "default", "lbl2");
    }

    [Fact]
    public void AddColumn_OverwritesIndexWithLabel_ShouldUseLastSet()
    {
        var twoda = CreateTestTwoDA(
            new[] { "Col1" },
            ("5", new[] { "a" })
        );

        var memory = new PatcherMemory();
        var logger = new PatchLogger();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new AddColumn2DA(
            "",
            "NewCol",
            "def",
            new Dictionary<int, RowValue> { { 0, new RowValueConstant("from_index") } },
            new Dictionary<string, RowValue> { { "5", new RowValueConstant("from_label") } },
            new Dictionary<int, string>()
        ));

        config.Apply(twoda, memory, logger, Game.K1);

        // Label insert should overwrite index insert for the same row
        twoda.GetColumn("NewCol").Should().Contain("from_label");
    }

    [Fact]
    public void AddColumn_ToEmptyTable_ShouldStillAddColumn()
    {
        var twoda = CreateTestTwoDA(new[] { "Col1" });

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

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetHeaders().Should().Contain("Col2");
        twoda.GetHeight().Should().Be(0);
    }

    [Fact]
    public void AddColumn_MultipleColumns_ShouldAddInOrder()
    {
        var twoda = CreateTestTwoDA(
            new[] { "Col1" },
            ("0", new[] { "a" })
        );

        var memory = new PatcherMemory();
        var logger = new PatchLogger();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new AddColumn2DA("", "Col2", "X", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>(), new Dictionary<int, string>()));
        config.Modifiers.Add(new AddColumn2DA("", "Col3", "Y", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>(), new Dictionary<int, string>()));
        config.Modifiers.Add(new AddColumn2DA("", "Col4", "Z", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>(), new Dictionary<int, string>()));

        config.Apply(twoda, memory, logger, Game.K1);

        twoda.GetHeaders().Should().Equal("Col1", "Col2", "Col3", "Col4");
        twoda.GetCellString(0, "Col2").Should().Be("X");
        twoda.GetCellString(0, "Col3").Should().Be("Y");
        twoda.GetCellString(0, "Col4").Should().Be("Z");
    }
}

