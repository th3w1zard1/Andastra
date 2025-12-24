using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Config;
using Andastra.Parsing.Mods.TwoDA;
using Andastra.Parsing.Reader;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{

    /// <summary>
    /// Advanced ConfigReader tests for 2DA section parsing.
    /// </summary>
    public class ConfigReader2DAAdvancedTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _modPath;
        private readonly IniDataParser _parser;

        public ConfigReader2DAAdvancedTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _modPath = Path.Combine(_tempDir, "tslpatchdata");
            Directory.CreateDirectory(_modPath);

            _parser = new IniDataParser();
            _parser.Configuration.AllowDuplicateKeys = true;
            _parser.Configuration.AllowDuplicateSections = true;
            _parser.Configuration.CaseInsensitive = false;
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddRow_ComplexCells_ShouldParseAllTypes()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=NewRow

[NewRow]
RowLabel=new_row
col_constant=100
col_tlk=StrRef5
col_2da=2DAMEMORY3
        col_high=high()
col_rowindex=RowIndex
col_rowlabel=RowLabel
col_rowcell=other_column
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var addRow = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddRow2DA;
            addRow.Should().NotBeNull();
            addRow.Cells.Should().HaveCount(7);

            addRow.Cells["col_constant"].Should().BeOfType<RowValueConstant>();
            addRow.Cells["col_tlk"].Should().BeOfType<RowValueTLKMemory>();
            addRow.Cells["col_2da"].Should().BeOfType<RowValue2DAMemory>();
            addRow.Cells["col_high"].Should().BeOfType<RowValueHigh>();
            addRow.Cells["col_rowindex"].Should().BeOfType<RowValueRowIndex>();
            addRow.Cells["col_rowlabel"].Should().BeOfType<RowValueRowLabel>();
            // Python: RowValueRowCell is only created for store operations (2DAMEMORY#=column or StrRef#=column)
            // For regular cell assignments like col_rowcell=other_column, it's a RowValueConstant
            addRow.Cells["col_rowcell"].Should().BeOfType<RowValueConstant>();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyRow_High_ShouldParseAllHighVariants()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=CopyTest

[CopyTest]
RowIndex=0
RowLabel=copied
col_a=High()
col_b=High()
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var copyRow = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as CopyRow2DA;
            copyRow.Should().NotBeNull();

            var highA = copyRow.Cells["col_a"] as RowValueHigh;
            highA.Should().NotBeNull();
            highA.Column.Should().Be("col_a");

            var highB = copyRow.Cells["col_b"] as RowValueHigh;
            highB.Should().NotBeNull();
            highB.Column.Should().Be("col_b");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_MultipleModifications_ShouldParseInOrder()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=Change1
ChangeRow1=Change2
AddRow0=Add1
CopyRow0=Copy1
AddColumn0=NewCol

[Change1]
RowIndex=0
col1=value1

[Change2]
RowLabel=label2
col2=value2

[Add1]
RowLabel=new
col1=added

[Copy1]
RowIndex=1
RowLabel=copied

[NewCol]
ColumnLabel=NewColumn
DefaultValue=****
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            List<Modify2DA> modifiers = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers;
            modifiers.Should().HaveCount(5);
            modifiers[0].Should().BeOfType<ChangeRow2DA>();
            modifiers[1].Should().BeOfType<ChangeRow2DA>();
            modifiers[2].Should().BeOfType<AddRow2DA>();
            modifiers[3].Should().BeOfType<CopyRow2DA>();
            modifiers[4].Should().BeOfType<AddColumn2DA>();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_ChangeRow_MultipleTargets_ShouldUseFirst()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=Change

[Change]
RowIndex=5
RowLabel=test_label
col1=value
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var change = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as ChangeRow2DA;
            change.Should().NotBeNull();
            change.Target.TargetType.Should().Be(TargetType.ROW_INDEX);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddColumn_WithRowSpecificValues_ShouldParseCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=NewCol

[NewCol]
ColumnLabel=NewCol
DefaultValue=default_value
RowIndex0=specific_value_0
RowIndex1=specific_value_1
RowLabel(label_test)=labeled_value
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var addColumn = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            addColumn.Should().NotBeNull();
            addColumn.Header.Should().Be("NewCol");
            addColumn.Default.Should().Be("default_value");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AllStore2DATypes_ShouldParseCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=Change

[Change]
RowIndex=0
col1=value
2DAMEMORY0=RowIndex
2DAMEMORY1=RowLabel
2DAMEMORY2=col1
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var change = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as ChangeRow2DA;
            change.Should().NotBeNull();
            change.Store2DA.Should().HaveCount(3);
            change.Store2DA.Keys.Should().Contain(0);
            change.Store2DA.Keys.Should().Contain(1);
            change.Store2DA.Keys.Should().Contain(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_ExclusiveColumn_BothAddAndCopy_ShouldParse()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=Add1
CopyRow0=Copy1

[Add1]
ExclusiveColumn=unique_id
RowLabel=add_label
unique_id=100

[Copy1]
RowIndex=0
ExclusiveColumn=unique_id
RowLabel=copy_label
unique_id=200
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var add = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddRow2DA;
            add.Should().NotBeNull();
            add.ExclusiveColumn.Should().Be("unique_id");

            var copy = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[1] as CopyRow2DA;
            copy.Should().NotBeNull();
            copy.ExclusiveColumn.Should().Be("unique_id");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_LabelIndex_ShouldParseAsTarget()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=Change

[Change]
LabelIndex=5
col1=value
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var change = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as ChangeRow2DA;
            change.Should().NotBeNull();
            change.Target.TargetType.Should().Be(TargetType.LABEL_COLUMN);
            // Python: assert mod_2da_2.target.value == "3" - value is stored as string, not RowValueConstant
            change.Target.Value.Should().Be("5");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_EmptyCells_ShouldParseAsStars()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=NewRow

[NewRow]
RowLabel=test
col1=
col2=****
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var add = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddRow2DA;
            add.Should().NotBeNull();

            var col1 = add.Cells["col1"] as RowValueConstant;
            col1.Should().NotBeNull();
            col1.String.Should().Be("");

            var col2 = add.Cells["col2"] as RowValueConstant;
            col2.Should().NotBeNull();
            // Python: elif value == "****": row_value = RowValueConstant("")
            // "****" is converted to empty string in Python
            col2.String.Should().Be("");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_MultipleTables_ShouldParseIndependently()
        {
            string iniText = @"
[2DAList]
Table0=table1.2da
Table1=table2.2da

[table1.2da]
ChangeRow0=Change1

[Change1]
RowIndex=0
col1=value1

[table2.2da]
AddRow0=Add2

[Add2]
RowLabel=test
col2=value2
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            result.Patches2DA.Should().HaveCount(2);
            result.Patches2DA.Should().Contain(p => p.SaveAs == "table1.2da");
            result.Patches2DA.Should().Contain(p => p.SaveAs == "table2.2da");

            result.Patches2DA.First(p => p.SaveAs == "table1.2da").Modifiers.Should().HaveCount(1);
            result.Patches2DA.First(p => p.SaveAs == "table1.2da").Modifiers[0].Should().BeOfType<ChangeRow2DA>();

            result.Patches2DA.First(p => p.SaveAs == "table2.2da").Modifiers.Should().HaveCount(1);
            result.Patches2DA.First(p => p.SaveAs == "table2.2da").Modifiers[0].Should().BeOfType<AddRow2DA>();
        }

    }
}

