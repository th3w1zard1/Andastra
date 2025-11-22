using System;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Mods.TwoDA;
using Xunit;

namespace TSLPatcher.Tests.Integration;

/// <summary>
/// Advanced 2DA tests covering edge cases and complex scenarios.
/// Ported from test_tslpatcher.py - Advanced 2DA scenarios.
/// </summary>
public class TwoDAAdvancedTests : IntegrationTestBase
{
    [Fact]
    public void AddColumn_Empty_ShouldFillWithStars()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            });

        var addColumn = new AddColumn2DA("add_col_0", "NewCol", "****", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

        // Act
        addColumn.Apply(twoda, Memory);

        // Assert
        twoda.GetHeaders().Should().Contain("NewCol");
        twoda.GetCellString(0, "NewCol").Should().Be("****");
        twoda.GetCellString(1, "NewCol").Should().Be("****");
    }

    [Fact]
    public void AddColumn_WithDefault_ShouldFillAllRows()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" }),
                ("1", new[] { "b" }),
                ("2", new[] { "c" })
            });

        var addColumn = new AddColumn2DA("add_col_0", "NewCol", "default", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

        // Act
        addColumn.Apply(twoda, Memory);

        // Assert
        twoda.GetCellString(0, "NewCol").Should().Be("default");
        twoda.GetCellString(1, "NewCol").Should().Be("default");
        twoda.GetCellString(2, "NewCol").Should().Be("default");
    }

    [Fact]
    public void AddColumn_RowIndexConstant_ShouldSetSpecificRow()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" }),
                ("1", new[] { "b" }),
                ("2", new[] { "c" })
            });

        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol(default)
RowIndex1=special_value
";
        var config = SetupIniAndConfig(iniText);
        var modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

        // Act
        modifications.Apply(twoda, Memory, Logger, Game.K1);

        // Assert
        twoda.GetCellString(0, "NewCol").Should().Be("default");
        twoda.GetCellString(1, "NewCol").Should().Be("special_value");
        twoda.GetCellString(2, "NewCol").Should().Be("default");
    }

    [Fact]
    public void AddColumn_RowLabel2DAMemory_ShouldUseTokenValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("row0", new[] { "a" }),
                ("row1", new[] { "b" }),
                ("row2", new[] { "c" })
            });

        Memory.Memory2DA[3] = "token_value";

        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol(default)
RowLabel(row1)=2DAMEMORY3
";
        var config = SetupIniAndConfig(iniText);
        var modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

        // Act
        modifications.Apply(twoda, Memory, Logger, Game.K1);

        // Assert
        twoda.GetCellString("row1", "NewCol").Should().Be("token_value");
    }

    [Fact]
    public void AddColumn_RowLabelTLKMemory_ShouldUseTokenValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("row0", new[] { "a" }),
                ("row1", new[] { "b" })
            });

        Memory.MemoryStr[5] = 12345;

        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol(default)
RowLabel(row1)=StrRef5
";
        var config = SetupIniAndConfig(iniText);
        var modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

        // Act
        modifications.Apply(twoda, Memory, Logger, Game.K1);

        // Assert
        twoda.GetCellString("row1", "NewCol").Should().Be("12345");
    }

    [Fact]
    public void AddColumn_2DAMemoryIndex_ShouldStoreColumnIndex()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" })
            });

        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol
2DAMEMORY0=ColumnIndex
";
        var config = SetupIniAndConfig(iniText);
        var modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

        // Act
        modifications.Apply(twoda, Memory, Logger, Game.K1);

        // Assert
        Memory.Memory2DA[0].Should().Be("2"); // Third column (0-indexed would be 2)
    }

    [Fact]
    public void AddColumn_2DAMemoryLine_ShouldStoreColumnLabel()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" })
            });

        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=MyNewColumn
2DAMEMORY1=ColumnLabel
";
        var config = SetupIniAndConfig(iniText);
        var modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

        // Act
        modifications.Apply(twoda, Memory, Logger, Game.K1);

        // Assert
        Memory.Memory2DA[1].Should().Be("MyNewColumn");
    }

    [Fact]
    public void ChangeRow_WithAllRowValueTypes_ShouldApplyCorrectly()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "id", "name", "value", "ref" },
            new[]
            {
                ("0", new[] { "1", "Old", "100", "****" }),
                ("1", new[] { "5", "Test", "200", "****" }),
                ("2", new[] { "3", "Data", "300", "****" })
            });

        Memory.Memory2DA[10] = "999";
        Memory.MemoryStr[20] = 12345;

        var target = new Target(TargetType.ROW_INDEX, new RowValueConstant("1"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Cells["id"] = new RowValueHigh("id");
        change.Cells["name"] = new RowValueConstant("Modified");
        change.Cells["value"] = new RowValue2DAMemory(10);
        change.Cells["ref"] = new RowValueTLKMemory(20);

        // Act
        change.Apply(twoda, Memory);

        // Assert
        twoda.GetCellString(1, "id").Should().Be("6"); // High(id) = max(1,5,3) + 1 = 6
        twoda.GetCellString(1, "name").Should().Be("Modified");
        twoda.GetCellString(1, "value").Should().Be("999");
        twoda.GetCellString(1, "ref").Should().Be("12345");
    }

    [Fact]
    public void AddRow_WithAllCellTypes_ShouldPopulateCorrectly()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "id", "name", "value", "index", "label", "cell" },
            new[]
            {
                ("0", new[] { "1", "Test", "100", "0", "0", "x" })
            });

        Memory.Memory2DA[5] = "from_token";
        Memory.MemoryStr[6] = 54321;

        var add = new AddRow2DA("Test", null, "1", null, null, null);
        add.Cells["id"] = new RowValueHigh("id");
        add.Cells["name"] = new RowValueConstant("NewRow");
        add.Cells["value"] = new RowValue2DAMemory(5);
        add.Cells["index"] = new RowValueRowIndex();
        add.Cells["label"] = new RowValueRowLabel();
        add.Cells["cell"] = new RowValueRowCell("id");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetCellString("1", "id").Should().Be("2"); // High + 1
        twoda.GetCellString("1", "name").Should().Be("NewRow");
        twoda.GetCellString("1", "value").Should().Be("from_token");
        twoda.GetCellString("1", "index").Should().Be("1");
        twoda.GetCellString("1", "label").Should().Be("1");
        twoda.GetCellString("1", "cell").Should().Be("2"); // Value of id column
    }

    [Fact]
    public void CopyRow_WithOverrides_ShouldMergeProperly()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "id", "name", "health", "damage" },
            new[]
            {
                ("original", new[] { "10", "Original", "100", "20" })
            });

        var source = new Target(TargetType.ROW_LABEL, new RowValueConstant("original"));
        var copy = new CopyRow2DA("Test", source, null, "copy", null, null, null);
        copy.Cells["id"] = new RowValueHigh("id");
        copy.Cells["name"] = new RowValueConstant("Copy");
        // health and damage should be copied from source

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        twoda.GetCellString("copy", "id").Should().Be("11");
        twoda.GetCellString("copy", "name").Should().Be("Copy");
        twoda.GetCellString("copy", "health").Should().Be("100"); // Copied
        twoda.GetCellString("copy", "damage").Should().Be("20"); // Copied
    }

    [Fact]
    public void ComplexWorkflow_MultipleOperations_ShouldApplyInOrder()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "id", "name" },
            new[]
            {
                ("0", new[] { "1", "Initial" })
            });

        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change1
AddRow0=add1
CopyRow0=copy1
AddColumn0=NewCol(default)

[change1]
RowIndex=0
name=Changed

[add1]
label=1
id=2
name=Added

[copy1]
RowIndex=1
label=2
id=3
name=Copied
";
        var config = SetupIniAndConfig(iniText);
        var modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

        // Act
        modifications.Apply(twoda, Memory, Logger, Game.K1);

        // Assert
        twoda.GetHeight().Should().Be(3);
        twoda.GetHeaders().Should().Contain("NewCol");
        twoda.GetCellString("0", "name").Should().Be("Changed");
        twoda.GetCellString("1", "name").Should().Be("Added");
        twoda.GetCellString("2", "name").Should().Be("Copied");
    }

    [Fact]
    public void ExclusiveColumn_MultipleAttempts_ShouldOnlyAddOnce()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "unique_id", "name" },
            new[]
            {
                ("0", new[] { "100", "Existing" })
            });

        var add1 = new AddRow2DA("Test1", "unique_id", "1", null, null, null);
        add1.Cells["unique_id"] = new RowValueConstant("200");
        add1.Cells["name"] = new RowValueConstant("First");

        var add2 = new AddRow2DA("Test2", "unique_id", "2", null, null, null);
        add2.Cells["unique_id"] = new RowValueConstant("200"); // Same value
        add2.Cells["name"] = new RowValueConstant("Second");

        var add3 = new AddRow2DA("Test3", "unique_id", "3", null, null, null);
        add3.Cells["unique_id"] = new RowValueConstant("300"); // Different value
        add3.Cells["name"] = new RowValueConstant("Third");

        // Act
        add1.Apply(twoda, Memory);
        add2.Apply(twoda, Memory);
        add3.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(3); // Original + First + Third (Second skipped)
        twoda.GetCellString("1", "unique_id").Should().Be("200");
        twoda.GetCellString("3", "unique_id").Should().Be("300");
    }

    [Fact]
    public void HighValue_EmptyColumn_ShouldReturnZero()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "id", "empty_col" },
            new[]
            {
                ("0", new[] { "1", "****" }),
                ("1", new[] { "2", "****" })
            });

        var add = new AddRow2DA("Test", null, "2", null, null, null);
        add.Cells["id"] = new RowValueConstant("3");
        add.Cells["empty_col"] = new RowValueHigh("empty_col");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetCellString("2", "empty_col").Should().Be("0");
    }

    [Fact]
    public void RowValueRowCell_ShouldGetValueFromSpecifiedColumn()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "id", "reference", "data" },
            new[]
            {
                ("0", new[] { "10", "20", "30" })
            });

        var add = new AddRow2DA("Test", null, "1", null, null, null);
        add.Cells["id"] = new RowValueConstant("15");
        add.Cells["reference"] = new RowValueRowCell("id");
        add.Cells["data"] = new RowValueRowCell("reference");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetCellString("1", "reference").Should().Be("15"); // Value from id
        twoda.GetCellString("1", "data").Should().Be("15"); // Value from reference (which got it from id)
    }
}

