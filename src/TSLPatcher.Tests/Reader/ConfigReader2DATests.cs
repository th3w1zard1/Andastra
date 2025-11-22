using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using TSLPatcher.Core.Config;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Mods.TwoDA;
using TSLPatcher.Core.Reader;
using Xunit;

namespace TSLPatcher.Tests.Reader;

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

    [Fact]
    public void TwoDA_ChangeRow_ShouldLoadIdentifier()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
ChangeRow0=RowLabel0

[RowLabel0]
LabelIndex=5
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.Patches2DA.Should().Contain(p => p.SaveAs == "appearance.2da");
        var modifiers = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers;
        modifiers.Should().HaveCount(1);

        var changeRow = modifiers[0] as ChangeRow2DA;
        changeRow.Should().NotBeNull();
        changeRow!.Identifier.Should().Be("RowLabel0");
    }

    [Fact]
    public void TwoDA_ChangeRow_ShouldLoadRowIndexTarget()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
ChangeRow0=RowLabel0

[RowLabel0]
RowIndex=5
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var changeRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as ChangeRow2DA;
        changeRow!.Target.TargetType.Should().Be(TargetType.ROW_INDEX);

        var rowValue = changeRow.Target.Value as RowValueConstant;
        rowValue.Should().NotBeNull();
        rowValue!.Value(null, null, null).Should().Be("5");
    }

    [Fact]
    public void TwoDA_ChangeRow_ShouldLoadRowLabelTarget()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
ChangeRow0=RowLabel0

[RowLabel0]
RowLabel=P_BastilaBB
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var changeRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as ChangeRow2DA;
        changeRow!.Target.TargetType.Should().Be(TargetType.ROW_LABEL);

        var rowValue = changeRow.Target.Value as RowValueConstant;
        rowValue.Should().NotBeNull();
        rowValue!.Value(null, null, null).Should().Be("P_BastilaBB");
    }

    [Fact]
    public void TwoDA_ChangeRow_ShouldLoadLabelIndexTarget()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
ChangeRow0=RowLabel0

[RowLabel0]
LabelIndex=10
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var changeRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as ChangeRow2DA;
        changeRow!.Target.TargetType.Should().Be(TargetType.LABEL_COLUMN);

        var rowValue = changeRow.Target.Value as RowValueConstant;
        rowValue.Should().NotBeNull();
        rowValue!.Value(null, null, null).Should().Be("10");
    }

    [Fact]
    public void TwoDA_ChangeRow_ShouldLoadStore2DAMemory()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
ChangeRow0=RowLabel0

[RowLabel0]
RowIndex=5
2DAMEMORY0=RowIndex
2DAMEMORY1=RowLabel
2DAMEMORY2=normalhead
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var changeRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as ChangeRow2DA;
        changeRow!.Store2DA.Should().HaveCount(3);

        var store0 = changeRow.Store2DA[0] as RowValueRowIndex;
        store0.Should().NotBeNull();

        var store1 = changeRow.Store2DA[1] as RowValueRowLabel;
        store1.Should().NotBeNull();

        var store2 = changeRow.Store2DA[2] as RowValueRowCell;
        store2.Should().NotBeNull();
        store2!.Column.Should().Be("normalhead");
    }

    [Fact]
    public void TwoDA_ChangeRow_ShouldLoadCellAssignments()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
ChangeRow0=RowLabel0

[RowLabel0]
RowIndex=5
normalhead=123
backuphead=StrRef5
modelj=2DAMEMORY3
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var changeRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as ChangeRow2DA;
        changeRow!.Cells.Should().HaveCount(3);

        // Constant value
        var normalhead = changeRow.Cells["normalhead"] as RowValueConstant;
        normalhead.Should().NotBeNull();
        normalhead!.Value(null, null, null).Should().Be("123");

        // TLK memory reference
        var backuphead = changeRow.Cells["backuphead"] as RowValueTLKMemory;
        backuphead.Should().NotBeNull();
        backuphead!.TokenId.Should().Be(5);

        // 2DA memory reference
        var modelj = changeRow.Cells["modelj"] as RowValue2DAMemory;
        modelj.Should().NotBeNull();
        modelj!.TokenId.Should().Be(3);
    }

    #endregion

    #region AddRow Tests

    [Fact]
    public void TwoDA_AddRow_ShouldLoadIdentifier()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=spells.2da

[spells.2da]
AddRow0=NewSpell

[NewSpell]
label=new_spell_123
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addRow = result.Patches2DA.First(p => p.SaveAs == "spells.2da").Modifiers[0] as AddRow2DA;
        addRow.Should().NotBeNull();
        addRow!.Identifier.Should().Be("NewSpell");
    }

    [Fact]
    public void TwoDA_AddRow_ShouldLoadRowLabel()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=spells.2da

[spells.2da]
AddRow0=NewSpell

[NewSpell]
label=my_custom_spell
name=12345
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addRow = result.Patches2DA.First(p => p.SaveAs == "spells.2da").Modifiers[0] as AddRow2DA;
        addRow!.RowLabel.Should().Be("my_custom_spell");
    }

    [Fact]
    public void TwoDA_AddRow_ShouldLoadExclusiveColumn()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=spells.2da

[spells.2da]
AddRow0=NewSpell

[NewSpell]
ExclusiveColumn=label
label=unique_spell
name=12345
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addRow = result.Patches2DA.First(p => p.SaveAs == "spells.2da").Modifiers[0] as AddRow2DA;
        addRow!.ExclusiveColumn.Should().Be("label");
    }

    [Fact]
    public void TwoDA_AddRow_ShouldLoadStore2DAMemory()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=spells.2da

[spells.2da]
AddRow0=NewSpell

[NewSpell]
label=new_spell
2DAMEMORY0=RowIndex
2DAMEMORY1=RowLabel
2DAMEMORY2=name
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addRow = result.Patches2DA.First(p => p.SaveAs == "spells.2da").Modifiers[0] as AddRow2DA;
        addRow!.Store2DA.Should().HaveCount(3);

        var store0 = addRow.Store2DA[0] as RowValueRowIndex;
        store0.Should().NotBeNull();

        var store1 = addRow.Store2DA[1] as RowValueRowLabel;
        store1.Should().NotBeNull();

        var store2 = addRow.Store2DA[2] as RowValueRowCell;
        store2.Should().NotBeNull();
        store2!.Column.Should().Be("name");
    }

    #endregion

    #region CopyRow Tests

    [Fact]
    public void TwoDA_CopyRow_ShouldLoadIdentifier()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
CopyRow0=CopyLabel

[CopyLabel]
RowIndex=5
label=copied_row
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var copyRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as CopyRow2DA;
        copyRow.Should().NotBeNull();
        copyRow!.Identifier.Should().Be("CopyLabel");
    }

    [Fact]
    public void TwoDA_CopyRow_ShouldLoadSourceTarget()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
CopyRow0=CopyLabel

[CopyLabel]
RowIndex=5
label=copied_row
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var copyRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as CopyRow2DA;
        copyRow!.Target.TargetType.Should().Be(TargetType.ROW_INDEX);

        var rowValue = copyRow.Target.Value as RowValueConstant;
        rowValue.Should().NotBeNull();
        rowValue!.String.Should().Be("5");
    }

    [Fact]
    public void TwoDA_CopyRow_ShouldLoadExclusiveColumn()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
CopyRow0=CopyLabel

[CopyLabel]
RowIndex=5
ExclusiveColumn=label
label=unique_copy
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var copyRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as CopyRow2DA;
        copyRow!.ExclusiveColumn.Should().Be("label");
    }

    [Fact]
    public void TwoDA_CopyRow_ShouldLoadCellOverrides()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
CopyRow0=CopyLabel

[CopyLabel]
RowIndex=5
label=new_label
normalhead=999
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var copyRow = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as CopyRow2DA;
        copyRow!.Cells.Should().HaveCount(2);

        var labelCell = copyRow.Cells["label"] as RowValueConstant;
        labelCell.Should().NotBeNull();
        labelCell!.String.Should().Be("new_label");

        var normalheadCell = copyRow.Cells["normalhead"] as RowValueConstant;
        normalheadCell.Should().NotBeNull();
        normalheadCell!.String.Should().Be("999");
    }

    #endregion

    #region AddColumn Tests

    [Fact]
    public void TwoDA_AddColumn_ShouldLoadColumnName()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
AddColumn0=newcolumn
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addColumn = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as AddColumn2DA;
        addColumn.Should().NotBeNull();
        addColumn!.Header.Should().Be("newcolumn");
    }

    [Fact]
    public void TwoDA_AddColumn_ShouldLoadDefaultValue()
    {
        // Arrange
        string iniText = @"
[2DAList]
Table0=appearance.2da

[appearance.2da]
AddColumn0=newcolumn(123)
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addColumn = result.Patches2DA.First(p => p.SaveAs == "appearance.2da").Modifiers[0] as AddColumn2DA;
        addColumn!.Header.Should().Be("newcolumn");
        addColumn.Default.Should().Be("123");
    }

    #endregion
}

