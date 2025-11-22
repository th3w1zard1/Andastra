using System.Collections.Generic;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.TwoDA;
using Xunit;
using TwoDAFile = TSLPatcher.Core.Formats.TwoDA.TwoDA;

namespace TSLPatcher.Tests.Mods.TwoDA;

/// <summary>
/// Tests for 2DA CopyRow modifications (ported from test_mods.py - TestManipulate2DA)
/// </summary>
public class TwoDaCopyRowTests
{
    [Fact]
    public void CopyRow_Existing_RowIndex()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });
        twoda.AddRow("1", new() { ["Col1"] = "c", ["Col2"] = "d" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            null,
            null,
            new() { ["Col2"] = new RowValueConstant("X") }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(3, twoda.GetHeight());
        Assert.Equal(new[] { "a", "c", "a" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "b", "d", "X" }, twoda.GetColumn("Col2"));
    }

    [Fact]
    public void CopyRow_Existing_RowLabel()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });
        twoda.AddRow("1", new() { ["Col1"] = "c", ["Col2"] = "d" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_LABEL, "1"),
            null,
            null,
            new() { ["Col2"] = new RowValueConstant("X") }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(3, twoda.GetHeight());
        Assert.Equal(new[] { "a", "c", "c" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "b", "d", "X" }, twoda.GetColumn("Col2"));
    }

    [Fact]
    public void CopyRow_Exclusive_NotExists()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            "Col1",
            null,
            new() { ["Col1"] = new RowValueConstant("c"), ["Col2"] = new RowValueConstant("d") }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(2, twoda.GetHeight());
        Assert.Equal("1", twoda.GetLabel(1));
        Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
    }

    [Fact]
    public void CopyRow_Exclusive_Exists()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            "Col1",
            null,
            new() { ["Col1"] = new RowValueConstant("a"), ["Col2"] = new RowValueConstant("X") }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(1, twoda.GetHeight());
        Assert.Equal("0", twoda.GetLabel(0));
        Assert.Equal(new[] { "a" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "X" }, twoda.GetColumn("Col2"));
    }

    [Fact]
    public void CopyRow_Exclusive_None()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            null,
            null,
            new() { ["Col1"] = new RowValueConstant("c"), ["Col2"] = new RowValueConstant("d") }
        ));
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            "",
            "r2",
            new() { ["Col1"] = new RowValueConstant("e"), ["Col2"] = new RowValueConstant("f") }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(3, twoda.GetHeight());
        Assert.Equal("r2", twoda.GetLabel(2));
        Assert.Equal(new[] { "a", "c", "e" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "b", "d", "f" }, twoda.GetColumn("Col2"));
    }

    [Fact]
    public void CopyRow_Store2DAMemory_RowIndex()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            null,
            "r1",
            new(),
            new() { [5] = new RowValueRowIndex() }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(2, twoda.GetHeight());
        Assert.Equal("1", memory.Memory2DA[5]);
    }

    [Fact]
    public void CopyRow_Store2DAMemory_RowLabel()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            null,
            "r1",
            new(),
            new() { [5] = new RowValueRowLabel() }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(2, twoda.GetHeight());
        Assert.Equal("r1", memory.Memory2DA[5]);
    }

    [Fact]
    public void CopyRow_Store2DAMemory_Cell()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> {  "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new CopyRow2DA(
            "",
            new Target(TargetType.ROW_INDEX, 0),
            null,
            null,
            new() { ["Col2"] = new RowValueConstant("X") },
            new() { [5] = new RowValueRowCell("Col2") }
        ));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(2, twoda.GetHeight());
        Assert.Equal("X", memory.Memory2DA[5]);
    }
}

