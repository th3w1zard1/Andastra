using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Mods.TwoDA;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Advanced 2DA tests covering edge cases and complex scenarios.
    /// Ported from test_tslpatcher.py - Advanced 2DA scenarios.
    /// </summary>
    public class TwoDAAdvancedTests : IntegrationTestBase
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_Empty_ShouldFillWithStars()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_WithDefault_ShouldFillAllRows()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowIndexConstant_ShouldSetSpecificRow()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
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
AddColumn0=NewCol

[NewCol]
ColumnLabel=NewCol
DefaultValue=default
I1=special_value
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            Modifications2DA modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

            // Act
            modifications.Apply(twoda, Memory, Logger, BioWareGame.K1);

            // Assert
            twoda.GetCellString(0, "NewCol").Should().Be("default");
            twoda.GetCellString(1, "NewCol").Should().Be("special_value");
            twoda.GetCellString(2, "NewCol").Should().Be("default");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabel2DAMemory_ShouldUseTokenValue()
        {
            // Python test: test_addcolumn_rowlabel_2damemory
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                new[]
                {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
                });

            Memory.Memory2DA[5] = "ABC";

            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol

[NewCol]
ColumnLabel=NewCol
DefaultValue=
L1=2DAMEMORY5
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            Modifications2DA modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

            // Act
            modifications.Apply(twoda, Memory, Logger, BioWareGame.K1);

            // Assert - Python: assert twoda.get_column("Col3") == ["", "ABC"]
            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("NewCol").Should().Equal("", "ABC");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_RowLabelTLKMemory_ShouldUseTokenValue()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1" },
                new[]
                {
                ("0", new[] { "a" }),
                ("1", new[] { "b" })
                });

            Memory.MemoryStr[5] = 12345;

            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol

[NewCol]
ColumnLabel=NewCol
DefaultValue=
L1=StrRef5
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            Modifications2DA modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

            // Act
            modifications.Apply(twoda, Memory, Logger, BioWareGame.K1);

            // Assert
            twoda.GetCellString("1", "NewCol").Should().Be("12345");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_2DAMemoryIndex_ShouldStoreColumnIndex()
        {
            // Python test: test_addcolumn_2damemory_index
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                new[]
                {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
                });

            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol

[NewCol]
ColumnLabel=NewCol
DefaultValue=
I0=X
I1=Y
2DAMEMORY0=I0
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            Modifications2DA modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

            // Act
            modifications.Apply(twoda, Memory, Logger, BioWareGame.K1);

            // Assert - Python: assert memory.memory_2da[0] == "X" (value from row 0, column NewCol)
            Memory.Memory2DA[0].Should().Be("X");
            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("NewCol").Should().Equal("X", "Y");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_2DAMemoryLine_ShouldStoreColumnLabel()
        {
            // Python test: test_addcolumn_2damemory_line
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                new[]
                {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
                });

            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=Col3
DefaultValue=
I0=X
I1=Y
2DAMEMORY0=L1
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            Modifications2DA modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

            // Act
            modifications.Apply(twoda, Memory, Logger, BioWareGame.K1);

            // Assert - Python: assert memory.memory_2da[0] == "Y"
            Memory.Memory2DA[0].Should().Be("Y");
            twoda.GetColumn("Col1").Should().Equal("a", "c");
            twoda.GetColumn("Col2").Should().Equal("b", "d");
            twoda.GetColumn("Col3").Should().Equal("X", "Y");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ChangeRow_WithAllRowValueTypes_ShouldApplyCorrectly()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "id", "name", "value", "ref" },
                new[]
                {
                ("0", new[] { "1", "Old", "100", "****" }),
                ("1", new[] { "5", "Test", "200", "****" }),
                ("2", new[] { "3", "Data", "300", "****" })
                });

            Memory.Memory2DA[10] = "999";
            Memory.MemoryStr[20] = 12345;

            var target = new Target(TargetType.ROW_INDEX, 1);
            var change = new ChangeRow2DA("Test", target, new Dictionary<string, RowValue>(), null);
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddRow_WithAllCellTypes_ShouldPopulateCorrectly()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "id", "name", "value", "index", "label", "cell" },
                new[]
                {
                ("0", new[] { "1", "Test", "100", "0", "0", "x" })
                });

            Memory.Memory2DA[5] = "from_token";
            Memory.MemoryStr[6] = 54321;

            var add = new AddRow2DA("Test", null, "1", new Dictionary<string, RowValue>(), null, null);
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void CopyRow_WithOverrides_ShouldMergeProperly()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "id", "name", "health", "damage" },
                new[]
                {
                ("original", new[] { "10", "Original", "100", "20" })
                });

            var source = new Target(TargetType.ROW_LABEL, new RowValueConstant("original"));
            var copy = new CopyRow2DA("Test", source, null, "copy", new Dictionary<string, RowValue>(), null, null);
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ComplexWorkflow_MultipleOperations_ShouldApplyInOrder()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
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
AddColumn0=NewCol

[change1]
RowIndex=0
name=Changed

[add1]
RowLabel=1
id=2
name=Added

[copy1]
RowIndex=1
RowLabel=2
id=3
name=Copied

[NewCol]
ColumnLabel=NewCol
DefaultValue=default
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            Modifications2DA modifications = config.Patches2DA.First(p => p.SaveAs == "test.2da");

            // Act
            modifications.Apply(twoda, Memory, Logger, BioWareGame.K1);

            // Assert
            twoda.GetHeight().Should().Be(3);
            twoda.GetHeaders().Should().Contain("NewCol");
            twoda.GetCellString("0", "name").Should().Be("Changed");
            twoda.GetCellString("1", "name").Should().Be("Added");
            twoda.GetCellString("2", "name").Should().Be("Copied");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ExclusiveColumn_MultipleAttempts_ShouldOnlyAddOnce()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "unique_id", "name" },
                new[]
                {
                ("0", new[] { "100", "Existing" })
                });

            var add1 = new AddRow2DA("Test1", "unique_id", "1", new Dictionary<string, RowValue>(), null, null);
            add1.Cells["unique_id"] = new RowValueConstant("200");
            add1.Cells["name"] = new RowValueConstant("First");

            var add2 = new AddRow2DA("Test2", "unique_id", "2", new Dictionary<string, RowValue>(), null, null);
            add2.Cells["unique_id"] = new RowValueConstant("200"); // Same value
            add2.Cells["name"] = new RowValueConstant("Second");

            var add3 = new AddRow2DA("Test3", "unique_id", "3", new Dictionary<string, RowValue>(), null, null);
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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void HighValue_EmptyColumn_ShouldReturnZero()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "id", "empty_col" },
                new[]
                {
                ("0", new[] { "1", "****" }),
                ("1", new[] { "2", "****" })
                });

            var add = new AddRow2DA("Test", null, "2", new Dictionary<string, RowValue>(), null, null);
            add.Cells["id"] = new RowValueConstant("3");
            add.Cells["empty_col"] = new RowValueHigh("empty_col");

            // Act
            add.Apply(twoda, Memory);

            // Assert
            twoda.GetCellString("2", "empty_col").Should().Be("0");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void RowValueRowCell_ShouldGetValueFromSpecifiedColumn()
        {
            // Arrange
            TwoDA twoda = CreateTest2DA(
                new[] { "id", "reference", "data" },
                new[]
                {
                ("0", new[] { "10", "20", "30" })
                });

            var add = new AddRow2DA("Test", null, "1", new Dictionary<string, RowValue>(), null, null);
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
}
