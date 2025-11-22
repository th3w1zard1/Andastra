using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Config;
using TSLPatcher.Core.Formats.GFF;
using TSLPatcher.Core.Mods.GFF;
using TSLPatcher.Core.Reader;
using Xunit;

namespace TSLPatcher.Tests.Reader;

/// <summary>
/// Tests for ConfigReader GFF parsing functionality.
/// Ported from test_reader.py - GFF section tests.
/// </summary>
public class ConfigReaderGFFTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _modPath;
    private readonly IniDataParser _parser;

    public ConfigReaderGFFTests()
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

    [Fact]
    public void GFF_ModifyField_ShouldLoadIntValue()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
Appearance_Type=123
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesGFF.Should().Contain(p => p.SaveAs == "test.utc");
        var modifiers = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers;
        modifiers.Should().HaveCount(1);

        var modify = modifiers[0] as ModifyFieldGFF;
        modify.Should().NotBeNull();
        modify!.Path.Should().Be("Appearance_Type");

        var value = modify.Value as FieldValueConstant;
        value.Should().NotBeNull();
        value!.Value(null, GFFFieldType.Int32).Should().Be(123);
    }

    [Fact]
    public void GFF_ModifyField_ShouldLoadStringValue()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
FirstName=TestName
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
        modify!.Path.Should().Be("FirstName");

        var value = modify.Value as FieldValueConstant;
        value!.Value(null, GFFFieldType.Int32).Should().Be("TestName");
    }

    [Fact]
    public void GFF_ModifyField_ShouldLoadFloatValue()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
ChallengeRating=5.5
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
        modify!.Path.Should().Be("ChallengeRating");

        var value = modify.Value as FieldValueConstant;
        value!.Value(null, GFFFieldType.Int32).Should().Be(5.5f);
    }

    [Fact]
    public void GFF_ModifyField_ShouldLoadTLKMemoryReference()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
Description=StrRef5
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
        modify!.Path.Should().Be("Description");

        var value = modify.Value as FieldValueTLKMemory;
        value.Should().NotBeNull();
        value!.TokenId.Should().Be(5);
    }

    [Fact]
    public void GFF_ModifyField_ShouldLoad2DAMemoryReference()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
Appearance_Type=2DAMEMORY3
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
        modify!.Path.Should().Be("Appearance_Type");

        var value = modify.Value as FieldValue2DAMemory;
        value.Should().NotBeNull();
        value!.TokenId.Should().Be(3);
    }

    [Fact]
    public void GFF_ModifyField_ShouldLoadNestedPath()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
ScriptEndRound\1\ScriptEndRound=k_ai_master
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
        modify!.Path.Should().Be("ScriptEndRound\\1\\ScriptEndRound");

        var value = modify.Value as FieldValueConstant;
        value!.Value(null, GFFFieldType.Int32).Should().Be("k_ai_master");
    }

    [Fact]
    public void GFF_AddField_ShouldLoadIntField()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=NewField

[NewField]
FieldType=Int
Label=CustomInt
Value=999
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
        addField.Should().NotBeNull();
        addField!.Label.Should().Be("CustomInt");
        addField.FieldType.Should().Be(GFFFieldType.Int32);

        var value = addField.Value as FieldValueConstant;
        value.Should().NotBeNull();
        value!.Value(null, GFFFieldType.Int32).Should().Be(999);
    }

    [Fact]
    public void GFF_AddField_ShouldLoadStringField()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=NewField

[NewField]
FieldType=CExoString
Label=CustomString
Value=TestString
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
        addField!.Label.Should().Be("CustomString");
        addField.FieldType.Should().Be(GFFFieldType.String);

        var value = addField.Value as FieldValueConstant;
        value.Should().NotBeNull();
        value!.Value(null, GFFFieldType.String).Should().Be("TestString");
    }

    [Fact]
    public void GFF_AddField_ShouldLoadVector3Field()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=NewField

[NewField]
FieldType=Vector
Label=Position
Value=1.0 2.0 3.0
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
        addField!.Label.Should().Be("Position");
        addField.FieldType.Should().Be(GFFFieldType.Vector3);

        var value = addField.Value as FieldValueConstant;
        value.Should().NotBeNull();
        var vectorValue = value!.Value(null, GFFFieldType.Vector3);
        vectorValue.Should().BeOfType<Vector3>();
        var vector = (Vector3)vectorValue;
        vector.X.Should().BeApproximately(1.0f, 0.0001f);
        vector.Y.Should().BeApproximately(2.0f, 0.0001f);
        vector.Z.Should().BeApproximately(3.0f, 0.0001f);
    }

    [Fact]
    public void GFF_AddField_ShouldLoadInNestedStruct()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=NewField

[NewField]
FieldType=Int
Label=CustomInt
Value=123
Path=ItemList\0
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
        addField!.Path.Should().Be("ItemList\\0");
        addField.Label.Should().Be("CustomInt");
    }

    [Fact]
    public void GFF_AddStruct_ShouldLoadToList()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddStruct0=NewStruct

[NewStruct]
TypeId=0
Path=ItemList
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var addStruct = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddStructToListGFF;
        addStruct.Should().NotBeNull();
        addStruct!.Path.Should().Be("ItemList");

        var value = addStruct.Value as FieldValueConstant;
        value.Should().NotBeNull();
        var gffStruct = value!.Value(null, GFFFieldType.Struct) as GFFStruct;
        gffStruct.Should().NotBeNull();
    }

    [Fact]
    public void GFF_Memory2DA_ShouldLoadModifier()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=2DAMEMORY5

[2DAMEMORY5]
Path=SomeField
2DAMEMORY5=appearance
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var memory2DA = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as Memory2DAModifierGFF;
        memory2DA.Should().NotBeNull();
        memory2DA!.DestTokenId.Should().Be(5);
        memory2DA.Path.Should().Be("SomeField");
    }

    [Fact]
    public void GFF_MultipleFiles_ShouldLoadSeparately()
    {
        // Arrange
        string iniText = @"
[GFFList]
File0=test1.utc
File1=test2.utc

[test1.utc]
Appearance_Type=100

[test2.utc]
Appearance_Type=200
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesGFF.Should().HaveCount(2);
        result.PatchesGFF.Should().Contain(p => p.SaveAs == "test1.utc");
        result.PatchesGFF.Should().Contain(p => p.SaveAs == "test2.utc");
    }
}

