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
/// Tests for 2DA AddColumn modifications (ported from test_mods.py - TestManipulate2DA)
/// </summary>
public class TwoDaAddColumnTests
{
    [Fact]
    public void AddColumn_Empty()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });
        twoda.AddRow("1", new() { ["Col1"] = "c", ["Col2"] = "d" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new AddColumn2DA("add_col_0", "Col3", "****", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(new[] { "Col1", "Col2", "Col3" }, twoda.GetHeaders());
        Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
        Assert.Equal(new[] { "****", "****" }, twoda.GetColumn("Col3"));
    }

    [Fact]
    public void AddColumn_WithDefaultValue()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });
        twoda.AddRow("1", new() { ["Col1"] = "c", ["Col2"] = "d" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new AddColumn2DA("add_col_0", "Col3", "X", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(new[] { "Col1", "Col2", "Col3" }, twoda.GetHeaders());
        Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
        Assert.Equal(new[] { "X", "X" }, twoda.GetColumn("Col3"));
    }

    [Fact]
    public void AddColumn_AlreadyExists()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> { "Col1", "Col2" });
        twoda.AddRow("0", new() { ["Col1"] = "a", ["Col2"] = "b" });
        twoda.AddRow("1", new() { ["Col1"] = "c", ["Col2"] = "d" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new AddColumn2DA("add_col_0", "Col2", "X", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        // Should not duplicate the column
        Assert.Equal(new[] { "Col1", "Col2" }, twoda.GetHeaders());
        Assert.Equal(new[] { "a", "c" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "b", "d" }, twoda.GetColumn("Col2"));
    }

    [Fact]
    public void AddColumn_Multiple()
    {
        // Arrange
        var twoda = new TwoDAFile(new List<string> { "Col1" });
        twoda.AddRow("0", new() { ["Col1"] = "a" });

        var logger = new PatchLogger();
        var memory = new PatcherMemory();

        var config = new Modifications2DA("");
        config.Modifiers.Add(new AddColumn2DA("add_col_1", "Col2", "X", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));
        config.Modifiers.Add(new AddColumn2DA("add_col_2", "Col3", "Y", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));
        config.Modifiers.Add(new AddColumn2DA("add_col_3", "Col4", "****", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>()));

        // Act
        config.Apply(twoda, memory, logger, Game.K1);

        // Assert
        Assert.Equal(new[] { "Col1", "Col2", "Col3", "Col4" }, twoda.GetHeaders());
        Assert.Equal(new[] { "a" }, twoda.GetColumn("Col1"));
        Assert.Equal(new[] { "X" }, twoda.GetColumn("Col2"));
        Assert.Equal(new[] { "Y" }, twoda.GetColumn("Col3"));
        Assert.Equal(new[] { "****" }, twoda.GetColumn("Col4"));
    }
}

