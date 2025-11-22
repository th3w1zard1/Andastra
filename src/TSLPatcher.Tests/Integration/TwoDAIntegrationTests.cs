using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.TwoDA;
using Xunit;

namespace TSLPatcher.Tests.Integration;

/// <summary>
/// Integration tests for 2DA patching workflows.
/// Ported from test_tslpatcher.py - 2DA integration tests.
/// </summary>
public class TwoDAIntegrationTests : IntegrationTestBase
{
    #region ChangeRow Integration Tests

    [Fact]
    public void ChangeRow_ExistingRowIndex_ShouldModifyCorrectRow()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            });

        var target = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Cells["Col1"] = new RowValueConstant("x");
        change.Cells["Col2"] = new RowValueConstant("y");
        change.Cells["Col3"] = new RowValueConstant("z");

        // Act
        change.Apply(twoda, Memory);

        // Assert
        AssertCellValue(twoda, 0, "Col1", "x");
        AssertCellValue(twoda, 0, "Col2", "y");
        AssertCellValue(twoda, 0, "Col3", "z");
        AssertCellValue(twoda, 1, "Col1", "d");
        AssertCellValue(twoda, 1, "Col2", "e");
    }

    [Fact]
    public void ChangeRow_ExistingRowLabel_ShouldModifyCorrectRow()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("Row0", new[] { "a", "b", "c" }),
                ("Row1", new[] { "d", "e", "f" })
            });

        var target = new Target(TargetType.ROW_LABEL, new RowValueConstant("Row1"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Cells["Col1"] = new RowValueConstant("x");
        change.Cells["Col2"] = new RowValueConstant("y");

        // Act
        change.Apply(twoda, Memory);

        // Assert
        AssertCellValue(twoda, "Row1", "Col1", "x");
        AssertCellValue(twoda, "Row1", "Col2", "y");
        AssertCellValue(twoda, "Row0", "Col1", "a");
    }

    [Fact]
    public void ChangeRow_ExistingLabelIndex_ShouldModifyCorrectRow()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("Row0", new[] { "a", "b", "c" }),
                ("Row1", new[] { "d", "e", "f" }),
                ("Row2", new[] { "g", "h", "i" })
            });

        var target = new Target(TargetType.LABEL_COLUMN, new RowValueConstant("1"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Cells["Col1"] = new RowValueConstant("modified");

        // Act
        change.Apply(twoda, Memory);

        // Assert
        AssertCellValue(twoda, "Row1", "Col1", "modified");
        AssertCellValue(twoda, "Row0", "Col1", "a");
        AssertCellValue(twoda, "Row2", "Col1", "g");
    }

    [Fact]
    public void ChangeRow_AssignTLKMemory_ShouldUseTokenValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "name", "description" },
            new[]
            {
                ("0", new[] { "100", "200" })
            });

        Memory.MemoryStr[5] = 12345;

        var target = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Cells["name"] = new RowValueTLKMemory(5);

        // Act
        change.Apply(twoda, Memory);

        // Assert
        AssertCellValue(twoda, 0, "name", "12345");
        AssertCellValue(twoda, 0, "description", "200");
    }

    [Fact]
    public void ChangeRow_Assign2DAMemory_ShouldUseTokenValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "appearance", "health" },
            new[]
            {
                ("0", new[] { "10", "50" })
            });

        Memory.Memory2DA[3] = "999";

        var target = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Cells["appearance"] = new RowValue2DAMemory(3);

        // Act
        change.Apply(twoda, Memory);

        // Assert
        AssertCellValue(twoda, 0, "appearance", "999");
    }

    [Fact]
    public void ChangeRow_AssignHigh_ShouldUseHighestValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "id", "value" },
            new[]
            {
                ("0", new[] { "1", "10" }),
                ("1", new[] { "5", "20" }),
                ("2", new[] { "3", "30" })
            });

        var target = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Cells["id"] = new RowValueHigh("id");

        // Act
        change.Apply(twoda, Memory);

        // Assert
        // High(id) should be 6 (one more than the highest existing value of 5)
        AssertCellValue(twoda, 0, "id", "6");
    }

    [Fact]
    public void ChangeRow_Set2DAMemory_RowIndex_ShouldStoreRowIndex()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            });

        var target = new Target(TargetType.ROW_INDEX, new RowValueConstant("1"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Store2DA[0] = new RowValueRowIndex();
        change.Cells["Col1"] = new RowValueConstant("modified");

        // Act
        change.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[0].Should().Be("1");
    }

    [Fact]
    public void ChangeRow_Set2DAMemory_RowLabel_ShouldStoreRowLabel()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("TestLabel", new[] { "a", "b" }),
                ("OtherLabel", new[] { "c", "d" })
            });

        var target = new Target(TargetType.ROW_LABEL, new RowValueConstant("TestLabel"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Store2DA[1] = new RowValueRowLabel();

        // Act
        change.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[1].Should().Be("TestLabel");
    }

    [Fact]
    public void ChangeRow_Set2DAMemory_ColumnLabel_ShouldStoreCellValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "name", "value" },
            new[]
            {
                ("0", new[] { "TestName", "100" })
            });

        var target = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var change = new ChangeRow2DA("Test", target, null, null);
        change.Store2DA[2] = new RowValueRowCell("value");

        // Act
        change.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[2].Should().Be("100");
    }

    #endregion

    #region AddRow Integration Tests

    [Fact]
    public void AddRow_RowLabel_UseMaxRowLabel_ShouldGenerateNewLabel()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" }),
                ("5", new[] { "e", "f" })
            });

        var add = new AddRow2DA("Test", null, "RowLabel", null, null, null);
        add.Cells["Col1"] = new RowValueConstant("new");
        add.Cells["Col2"] = new RowValueConstant("row");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(4);
        AssertCellValue(twoda, "6", "Col1", "new");
        AssertCellValue(twoda, "6", "Col2", "row");
    }

    [Fact]
    public void AddRow_RowLabel_UseConstant_ShouldUseProvidedLabel()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" })
            });

        var add = new AddRow2DA("Test", null, "CustomLabel", null, null, null);
        add.Cells["Col1"] = new RowValueConstant("x");
        add.Cells["Col2"] = new RowValueConstant("y");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(2);
        AssertCellValue(twoda, "CustomLabel", "Col1", "x");
        AssertCellValue(twoda, "CustomLabel", "Col2", "y");
    }

    [Fact]
    public void AddRow_Exclusive_NotExists_ShouldAddRow()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "label", "value" },
            new[]
            {
                ("0", new[] { "existing", "100" })
            });

        var add = new AddRow2DA("Test", "label", "NewRow", null, null, null);
        add.Cells["label"] = new RowValueConstant("unique");
        add.Cells["value"] = new RowValueConstant("200");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(2);
        AssertCellValue(twoda, "NewRow", "label", "unique");
    }

    [Fact]
    public void AddRow_Exclusive_Exists_ShouldSkipAddition()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "label", "value" },
            new[]
            {
                ("0", new[] { "existing", "100" })
            });

        var add = new AddRow2DA("Test", "label", "NewRow", null, null, null);
        add.Cells["label"] = new RowValueConstant("existing");
        add.Cells["value"] = new RowValueConstant("200");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(1);
        AssertCellValue(twoda, 0, "value", "100");
    }

    [Fact]
    public void AddRow_Exclusive_None_ShouldAlwaysAdd()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "label", "value" },
            new[]
            {
                ("0", new[] { "existing", "100" })
            });

        var add = new AddRow2DA("Test", null, "NewRow", null, null, null);
        add.Cells["label"] = new RowValueConstant("existing");
        add.Cells["value"] = new RowValueConstant("200");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(2);
        AssertCellValue(twoda, "NewRow", "label", "existing");
        AssertCellValue(twoda, "NewRow", "value", "200");
    }

    [Fact]
    public void AddRow_Store2DAMemory_RowIndex_ShouldStoreIndex()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" }),
                ("1", new[] { "b" })
            });

        var add = new AddRow2DA("Test", null, "2", null, null, null);
        add.Store2DA[0] = new RowValueRowIndex();
        add.Cells["Col1"] = new RowValueConstant("c");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[0].Should().Be("2");
    }

    [Fact]
    public void AddRow_Store2DAMemory_RowLabel_ShouldStoreLabel()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" })
            });

        var add = new AddRow2DA("Test", null, "NewLabel", null, null, null);
        add.Store2DA[1] = new RowValueRowLabel();
        add.Cells["Col1"] = new RowValueConstant("b");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[1].Should().Be("NewLabel");
    }

    [Fact]
    public void AddRow_Store2DAMemory_Cell_ShouldStoreCellValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "name", "value" },
            new[]
            {
                ("0", new[] { "Test", "100" })
            });

        var add = new AddRow2DA("Test", null, "1", null, null, null);
        add.Store2DA[2] = new RowValueRowCell("value");
        add.Cells["name"] = new RowValueConstant("New");
        add.Cells["value"] = new RowValueConstant("999");

        // Act
        add.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[2].Should().Be("999");
    }

    #endregion

    #region CopyRow Integration Tests

    [Fact]
    public void CopyRow_ExistingRowIndex_ShouldCopyAndModify()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            });

        var source = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var copy = new CopyRow2DA("Test", source, null, "NewRow", null, null, null);
        copy.Cells["Col1"] = new RowValueConstant("modified");

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(3);
        AssertCellValue(twoda, "NewRow", "Col1", "modified");
        AssertCellValue(twoda, "NewRow", "Col2", "b");
        AssertCellValue(twoda, "NewRow", "Col3", "c");
    }

    [Fact]
    public void CopyRow_ExistingRowLabel_ShouldCopyCorrectRow()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("SourceRow", new[] { "x", "y" }),
                ("OtherRow", new[] { "a", "b" })
            });

        var source = new Target(TargetType.ROW_LABEL, new RowValueConstant("SourceRow"));
        var copy = new CopyRow2DA("Test", source, null, "CopiedRow", null, null, null);

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        AssertCellValue(twoda, "CopiedRow", "Col1", "x");
        AssertCellValue(twoda, "CopiedRow", "Col2", "y");
    }

    [Fact]
    public void CopyRow_Exclusive_NotExists_ShouldCopy()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "label", "value" },
            new[]
            {
                ("0", new[] { "existing", "100" })
            });

        var source = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var copy = new CopyRow2DA("Test", source, "label", "NewRow", null, null, null);
        copy.Cells["label"] = new RowValueConstant("unique");

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(2);
    }

    [Fact]
    public void CopyRow_Exclusive_Exists_ShouldSkipCopy()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "label", "value" },
            new[]
            {
                ("0", new[] { "existing", "100" })
            });

        var source = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var copy = new CopyRow2DA("Test", source, "label", "NewRow", null, null, null);
        copy.Cells["label"] = new RowValueConstant("existing");

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        twoda.GetHeight().Should().Be(1);
    }

    [Fact]
    public void CopyRow_Store2DAMemory_RowIndex_ShouldStoreNewIndex()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" })
            });

        var source = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var copy = new CopyRow2DA("Test", source, null, "1", null, null, null);
        copy.Store2DA[0] = new RowValueRowIndex();

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[0].Should().Be("1");
    }

    [Fact]
    public void CopyRow_Store2DAMemory_RowLabel_ShouldStoreNewLabel()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" })
            });

        var source = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var copy = new CopyRow2DA("Test", source, null, "CopiedLabel", null, null, null);
        copy.Store2DA[1] = new RowValueRowLabel();

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[1].Should().Be("CopiedLabel");
    }

    [Fact]
    public void CopyRow_Store2DAMemory_Cell_ShouldStoreCellValue()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "name", "value" },
            new[]
            {
                ("0", new[] { "Test", "999" })
            });

        var source = new Target(TargetType.ROW_INDEX, new RowValueConstant("0"));
        var copy = new CopyRow2DA("Test", source, null, "1", null, null, null);
        copy.Store2DA[2] = new RowValueRowCell("value");

        // Act
        copy.Apply(twoda, Memory);

        // Assert
        Memory.Memory2DA[2].Should().Be("999");
    }

    #endregion

    #region AddColumn Integration Tests

    [Fact]
    public void AddColumn_Empty_ShouldAddColumnWithEmptyCells()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            });

        var addColumn = new AddColumn2DA("NewCol", "NewCol", "", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

        // Act
        addColumn.Apply(twoda, Memory);

        // Assert
        twoda.GetHeaders().Should().Contain("NewCol");
        AssertCellValue(twoda, 0, "NewCol", "");
        AssertCellValue(twoda, 1, "NewCol", "");
    }

    [Fact]
    public void AddColumn_WithDefaultValue_ShouldFillCells()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" }),
                ("1", new[] { "b" })
            });

        var addColumn = new AddColumn2DA("add_col_0", "NewCol", "default", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

        // Act
        addColumn.Apply(twoda, Memory);

        // Assert
        twoda.GetHeaders().Should().Contain("NewCol");
        AssertCellValue(twoda, 0, "NewCol", "default");
        AssertCellValue(twoda, 1, "NewCol", "default");
    }

    [Fact]
    public void AddColumn_AlreadyExists_ShouldNotAddDuplicate()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" })
            });

        var addColumn = new AddColumn2DA("add_col_0", "Col1", "****", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

        // Act
        addColumn.Apply(twoda, Memory);

        // Assert
        twoda.GetHeaders().Count(c => c == "Col1").Should().Be(1);
    }

    [Fact]
    public void AddColumn_Multiple_ShouldAddAllColumns()
    {
        // Arrange
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" })
            });

        var addColumn1 = new AddColumn2DA("add_col_1", "NewCol1", "val1", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());
        var addColumn2 = new AddColumn2DA("add_col_2", "NewCol2", "val2", new Dictionary<int, RowValue>(), new Dictionary<string, RowValue>());

        // Act
        addColumn1.Apply(twoda, Memory);
        addColumn2.Apply(twoda, Memory);

        // Assert
        twoda.GetHeaders().Should().Contain("NewCol1");
        twoda.GetHeaders().Should().Contain("NewCol2");
        AssertCellValue(twoda, 0, "NewCol1", "val1");
        AssertCellValue(twoda, 0, "NewCol2", "val2");
    }

    [Fact]
    public void AddColumnEmpty_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=****
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("", "");
    }

    [Fact]
    public void AddColumnDefault_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=X
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "X");
    }

    [Fact]
    public void AddColumnRowIndexConstant_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=****
I0=X
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "");
    }

    [Fact]
    public void AddColumnRowLabel2DAMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=****
L1=2DAMEMORY5
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        memory.Memory2DA[5] = "ABC";
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("", "ABC");
    }

    [Fact]
    public void AddColumnRowLabelTLKMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=****
L1=StrRef5
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        memory.MemoryStr[5] = 123;
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("", "123");
    }

    [Fact]
    public void AddColumn2DAMemoryIndex_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=****
I0=X
I1=Y
2DAMEMORY0=I0
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "Y");
        memory.Memory2DA[0].Should().Be("X");
    }

    [Fact]
    public void AddColumn2DAMemoryLabel_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=****
I0=X
I1=Y
2DAMEMORY0=L1
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
        twoda.GetColumn("Col3").Should().Equal("X", "Y");
        memory.Memory2DA[0].Should().Be("Y");
    }

    #endregion
}

