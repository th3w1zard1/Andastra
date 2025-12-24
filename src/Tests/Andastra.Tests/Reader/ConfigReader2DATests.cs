using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Config;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Mods.TwoDA;
using Andastra.Parsing.Reader;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{

    /// <summary>
    /// Tests for ConfigReader 2DA parsing functionality.
    /// Ported from test_reader.py - 2DA section tests.
    /// </summary>
    public class ConfigReader2DATests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _modPath;
        private readonly IniDataParser _parser;

        public ConfigReader2DATests()
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

        #region ChangeRow Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_ChangeRow_ShouldLoadIdentifier()
        {
            // Python test: test_2da_changerow_identifier
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0
ChangeRow1=change_row_1

[change_row_0]
RowIndex=1
[change_row_1]
RowLabel=1
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;
            modifiers.Should().HaveCount(2);

            var mod0 = modifiers[0] as ChangeRow2DA;
            mod0.Should().NotBeNull();
            mod0.Identifier.Should().Be("change_row_0");

            var mod1 = modifiers[1] as ChangeRow2DA;
            mod1.Should().NotBeNull();
            mod1.Identifier.Should().Be("change_row_1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_ChangeRow_ShouldLoadTargets()
        {
            // Python test: test_2da_changerow_targets
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0
ChangeRow1=change_row_1
ChangeRow2=change_row_2

[change_row_0]
RowIndex=1
[change_row_1]
RowLabel=2
[change_row_2]
LabelIndex=3
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_2da_0 = modifiers[0] as ChangeRow2DA;
            mod_2da_0.Should().NotBeNull();
            mod_2da_0.Target.TargetType.Should().Be(TargetType.ROW_INDEX);
            mod_2da_0.Target.Value.Should().Be(1);

            var mod_2da_1 = modifiers[1] as ChangeRow2DA;
            mod_2da_1.Should().NotBeNull();
            mod_2da_1.Target.TargetType.Should().Be(TargetType.ROW_LABEL);
            mod_2da_1.Target.Value.Should().Be("2");

            var mod_2da_2 = modifiers[2] as ChangeRow2DA;
            mod_2da_2.Should().NotBeNull();
            mod_2da_2.Target.TargetType.Should().Be(TargetType.LABEL_COLUMN);
            mod_2da_2.Target.Value.Should().Be("3");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_ChangeRow_ShouldLoadStore2DAMemory()
        {
            // Python test: test_2da_changerow_store2da
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0

[change_row_0]
RowIndex=0
2DAMEMORY0=RowIndex
2DAMEMORY1=RowLabel
2DAMEMORY2=label
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var mod_2da_0 = result.Patches2DA[0].Modifiers[0] as ChangeRow2DA;
            mod_2da_0.Should().NotBeNull();

            var store_2da_0a = mod_2da_0.Store2DA[0] as RowValueRowIndex;
            store_2da_0a.Should().NotBeNull();

            var store_2da_0b = mod_2da_0.Store2DA[1] as RowValueRowLabel;
            store_2da_0b.Should().NotBeNull();

            var store_2da_0c = mod_2da_0.Store2DA[2] as RowValueRowCell;
            store_2da_0c.Should().NotBeNull();
            store_2da_0c.Column.Should().Be("label");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_ChangeRow_ShouldLoadCells()
        {
            // Python test: test_2da_changerow_cells
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0

[change_row_0]
RowIndex=0
label=Test123
dialog=StrRef4
appearance=2DAMEMORY5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var mod_2da_0 = result.Patches2DA[0].Modifiers[0] as ChangeRow2DA;
            mod_2da_0.Should().NotBeNull();

            var cell_0_label = mod_2da_0.Cells["label"] as RowValueConstant;
            cell_0_label.Should().NotBeNull();
            cell_0_label.String.Should().Be("Test123");

            var cell_0_dialog = mod_2da_0.Cells["dialog"] as RowValueTLKMemory;
            cell_0_dialog.Should().NotBeNull();
            cell_0_dialog.TokenId.Should().Be(4);

            var cell_0_appearance = mod_2da_0.Cells["appearance"] as RowValue2DAMemory;
            cell_0_appearance.Should().NotBeNull();
            cell_0_appearance.TokenId.Should().Be(5);
        }

        #endregion

        #region AddRow Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddRow_ShouldLoadIdentifier()
        {
            // Python test: test_2da_addrow_identifier
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0
AddRow1=add_row_1

[add_row_0]
[add_row_1]
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_0 = modifiers[0] as AddRow2DA;
            mod_0.Should().NotBeNull();
            mod_0.Identifier.Should().Be("add_row_0");

            var mod_1 = modifiers[1] as AddRow2DA;
            mod_1.Should().NotBeNull();
            mod_1.Identifier.Should().Be("add_row_1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddRow_ShouldLoadRowLabel()
        {
            // Python test: test_2da_addrow_rowlabel
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0
AddRow1=add_row_1

[add_row_0]
RowLabel=123
[add_row_1]
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_0 = modifiers[0] as AddRow2DA;
            mod_0.Should().NotBeNull();
            mod_0.Should().BeOfType<AddRow2DA>();
            mod_0.Identifier.Should().Be("add_row_0");
            mod_0.RowLabel.Should().Be("123");

            var mod_1 = modifiers[1] as AddRow2DA;
            mod_1.Should().NotBeNull();
            mod_1.Should().BeOfType<AddRow2DA>();
            mod_1.Identifier.Should().Be("add_row_1");
            mod_1.RowLabel.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddRow_ShouldLoadExclusiveColumn()
        {
            // Python test: test_2da_addrow_exclusivecolumn
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0
AddRow1=add_row_1

[add_row_0]
ExclusiveColumn=label
[add_row_1]
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_0 = modifiers[0] as AddRow2DA;
            mod_0.Should().NotBeNull();
            mod_0.Should().BeOfType<AddRow2DA>();
            mod_0.Identifier.Should().Be("add_row_0");
            mod_0.ExclusiveColumn.Should().Be("label");

            var mod_1 = modifiers[1] as AddRow2DA;
            mod_1.Should().NotBeNull();
            mod_1.Should().BeOfType<AddRow2DA>();
            mod_1.Identifier.Should().Be("add_row_1");
            mod_1.ExclusiveColumn.Should().BeNull();
        }


        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddRow_ShouldLoadStore2DAMemory()
        {
            // Python test: test_2da_addrow_store2da
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
2DAMEMORY0=RowIndex
2DAMEMORY1=RowLabel
2DAMEMORY2=label
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var mod_0 = result.Patches2DA[0].Modifiers[0] as AddRow2DA;
            mod_0.Should().NotBeNull();

            var store_0a = mod_0.Store2DA[0] as RowValueRowIndex;
            store_0a.Should().NotBeNull();

            var store_0b = mod_0.Store2DA[1] as RowValueRowLabel;
            store_0b.Should().NotBeNull();

            var store_0c = mod_0.Store2DA[2] as RowValueRowCell;
            store_0c.Should().NotBeNull();
            store_0c.Column.Should().Be("label");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddRow_ShouldLoadCells()
        {
            // Python test: test_2da_addrow_cells
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
label=Test123
dialog=StrRef4
appearance=2DAMEMORY5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var mod_0 = result.Patches2DA[0].Modifiers[0] as AddRow2DA;
            mod_0.Should().NotBeNull();

            var cell_0_label = mod_0.Cells["label"] as RowValueConstant;
            cell_0_label.Should().NotBeNull();
            cell_0_label.String.Should().Be("Test123");

            var cell_0_dialog = mod_0.Cells["dialog"] as RowValueTLKMemory;
            cell_0_dialog.Should().NotBeNull();
            cell_0_dialog.TokenId.Should().Be(4);

            var cell_0_appearance = mod_0.Cells["appearance"] as RowValue2DAMemory;
            cell_0_appearance.Should().NotBeNull();
            cell_0_appearance.TokenId.Should().Be(5);
        }

        #endregion

        #region CopyRow Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyRow_ShouldLoadIdentifier()
        {
            // Python test: test_2da_copyrow_identifier
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0
CopyRow1=copy_row_1

[copy_row_0]
RowIndex=1
[copy_row_1]
RowLabel=1
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_0 = modifiers[0] as CopyRow2DA;
            mod_0.Should().NotBeNull();
            mod_0.Identifier.Should().Be("copy_row_0");

            var mod_1 = modifiers[1] as CopyRow2DA;
            mod_1.Should().NotBeNull();
            mod_1.Identifier.Should().Be("copy_row_1");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyRow_ShouldLoadTarget()
        {
            // Python test: test_2da_copyrow_target
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0
CopyRow1=copy_row_1
CopyRow2=copy_row_2

[copy_row_0]
RowIndex=1
[copy_row_1]
RowLabel=2
[copy_row_2]
LabelIndex=3
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_0 = modifiers[0] as CopyRow2DA;
            mod_0.Should().NotBeNull();
            mod_0.Target.TargetType.Should().Be(TargetType.ROW_INDEX);
            mod_0.Target.Value.Should().Be(1);

            var mod_1 = modifiers[1] as CopyRow2DA;
            mod_1.Should().NotBeNull();
            mod_1.Target.TargetType.Should().Be(TargetType.ROW_LABEL);
            mod_1.Target.Value.Should().Be("2");

            var mod_2 = modifiers[2] as CopyRow2DA;
            mod_2.Should().NotBeNull();
            mod_2.Target.TargetType.Should().Be(TargetType.LABEL_COLUMN);
            mod_2.Target.Value.Should().Be("3");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyRow_ShouldLoadExclusiveColumn()
        {
            // Python test: test_2da_copyrow_exclusivecolumn
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0
CopyRow1=copy_row_1

[copy_row_0]
RowIndex=0
ExclusiveColumn=label
[copy_row_1]
RowIndex=0
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_0 = modifiers[0] as CopyRow2DA;
            mod_0.Should().NotBeNull();
            mod_0.Should().BeOfType<CopyRow2DA>();
            mod_0.Identifier.Should().Be("copy_row_0");
            mod_0.ExclusiveColumn.Should().Be("label");

            var mod_1 = modifiers[1] as CopyRow2DA;
            mod_1.Should().NotBeNull();
            mod_1.Should().BeOfType<CopyRow2DA>();
            mod_1.Identifier.Should().Be("copy_row_1");
            mod_1.ExclusiveColumn.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyRow_ShouldLoadRowLabel()
        {
            // Python test: test_2da_copyrow_rowlabel
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0
CopyRow1=copy_row_1

[copy_row_0]
RowIndex=0
NewRowLabel=123
[copy_row_1]
RowIndex=0
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<Modify2DA> modifiers = result.Patches2DA[0].Modifiers;

            var mod_0 = modifiers[0] as CopyRow2DA;
            mod_0.Should().NotBeNull();
            mod_0.Should().BeOfType<CopyRow2DA>();
            mod_0.Identifier.Should().Be("copy_row_0");
            mod_0.RowLabel.Should().Be("123");

            var mod_1 = modifiers[1] as CopyRow2DA;
            mod_1.Should().NotBeNull();
            mod_1.Should().BeOfType<CopyRow2DA>();
            mod_1.Identifier.Should().Be("copy_row_1");
            mod_1.RowLabel.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyRow_ShouldLoadStore2DAMemory()
        {
            // Python test: test_2da_copyrow_store2da
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowLabel=0
2DAMEMORY0=RowIndex
2DAMEMORY1=RowLabel
2DAMEMORY2=label
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var mod_0 = result.Patches2DA[0].Modifiers[0] as CopyRow2DA;
            mod_0.Should().NotBeNull();

            var store_0a = mod_0.Store2DA[0] as RowValueRowIndex;
            store_0a.Should().NotBeNull();

            var store_0b = mod_0.Store2DA[1] as RowValueRowLabel;
            store_0b.Should().NotBeNull();

            var store_0c = mod_0.Store2DA[2] as RowValueRowCell;
            store_0c.Should().NotBeNull();
            store_0c.Column.Should().Be("label");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyRow_ShouldLoadCells()
        {
            // Python test: test_2da_copyrow_cells
            // Arrange
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowLabel=0
label=Test123
dialog=StrRef4
appearance=2DAMEMORY5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var mod_0 = result.Patches2DA[0].Modifiers[0] as CopyRow2DA;
            mod_0.Should().NotBeNull();

            var cell_0_label = mod_0.Cells["label"] as RowValueConstant;
            cell_0_label.Should().NotBeNull();
            cell_0_label.String.Should().Be("Test123");

            var cell_0_dialog = mod_0.Cells["dialog"] as RowValueTLKMemory;
            cell_0_dialog.Should().NotBeNull();
            cell_0_dialog.TokenId.Should().Be(4);

            var cell_0_appearance = mod_0.Cells["appearance"] as RowValue2DAMemory;
            cell_0_appearance.Should().NotBeNull();
            cell_0_appearance.TokenId.Should().Be(5);
        }

        #endregion

        #region AddColumn Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddColumn_ShouldLoadColumnName()
        {
            // Arrange
            string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
AddColumn0=newcolumn

[newcolumn]
ColumnLabel=newcolumn
DefaultValue=
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var addColumn = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as AddColumn2DA;
            addColumn.Should().NotBeNull();
            addColumn.Header.Should().Be("newcolumn");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddColumn_ShouldLoadDefaultValue()
        {
            // Arrange
            string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
AddColumn0=newcolumn

[newcolumn]
ColumnLabel=newcolumn
DefaultValue=123
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var addColumn = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as AddColumn2DA;
            addColumn.Header.Should().Be("newcolumn");
            addColumn.Default.Should().Be("123");
        }

        #endregion
    }
}

