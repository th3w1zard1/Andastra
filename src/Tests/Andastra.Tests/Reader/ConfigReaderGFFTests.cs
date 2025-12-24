using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Mods.GFF;
using Andastra.Parsing.Reader;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{

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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyField_ShouldLoadIntValue()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
Appearance_Type=123
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesGFF.Should().Contain(p => p.SaveAs == "test.utc");
            List<ModifyGFF> modifiers = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers;
            modifiers.Should().HaveCount(1);

            var modify = modifiers[0] as ModifyFieldGFF;
            modify.Should().NotBeNull();
            modify.Path.Should().Be("Appearance_Type");

            var value = modify.Value as FieldValueConstant;
            value.Should().NotBeNull();
            value.Value(null, GFFFieldType.Int32).Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyField_ShouldLoadStringValue()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
FirstName=TestName
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
            modify.Path.Should().Be("FirstName");

            var value = modify.Value as FieldValueConstant;
            value.Value(null, GFFFieldType.String).Should().Be("TestName");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyField_ShouldLoadFloatValue()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
ChallengeRating=5.5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
            modify.Path.Should().Be("ChallengeRating");

            var value = modify.Value as FieldValueConstant;
            value.Value(null, GFFFieldType.Single).Should().Be(5.5f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyField_ShouldLoadTLKMemoryReference()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
Description=StrRef5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
            modify.Path.Should().Be("Description");

            var value = modify.Value as FieldValueTLKMemory;
            value.Should().NotBeNull();
            value.TokenId.Should().Be(5);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyField_ShouldLoad2DAMemoryReference()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
Appearance_Type=2DAMEMORY3
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
            modify.Path.Should().Be("Appearance_Type");

            var value = modify.Value as FieldValue2DAMemory;
            value.Should().NotBeNull();
            value.TokenId.Should().Be(3);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyField_ShouldLoadNestedPath()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
ScriptEndRound\1\ScriptEndRound=k_ai_master
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var modify = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as ModifyFieldGFF;
            modify.Path.Should().Be("ScriptEndRound\\1\\ScriptEndRound");

            var value = modify.Value as FieldValueConstant;
            value.Value(null, GFFFieldType.String).Should().Be("k_ai_master");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
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
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
            addField.Should().NotBeNull();
            addField.Label.Should().Be("CustomInt");
            addField.FieldType.Should().Be(GFFFieldType.Int32);

            var value = addField.Value as FieldValueConstant;
            value.Should().NotBeNull();
            value.Value(null, GFFFieldType.Int32).Should().Be(999);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddField_ShouldLoadStringField()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=NewField

[NewField]
FieldType=ExoString
Label=CustomString
Value=TestString
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
            addField.Label.Should().Be("CustomString");
            addField.FieldType.Should().Be(GFFFieldType.String);

            var value = addField.Value as FieldValueConstant;
            value.Should().NotBeNull();
            value.Value(null, GFFFieldType.String).Should().Be("TestString");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddField_ShouldLoadVector3Field()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=NewField

[NewField]
FieldType=Position
Label=Position
Value=1.0|2.0|3.0
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
            addField.Label.Should().Be("Position");
            addField.FieldType.Should().Be(GFFFieldType.Vector3);

            var value = addField.Value as FieldValueConstant;
            value.Should().NotBeNull();
            object vectorValue = value.Value(null, GFFFieldType.Vector3);
            vectorValue.Should().BeOfType<Vector3>();
            var vector = (Vector3)vectorValue;
            vector.X.Should().BeApproximately(1.0f, 0.0001f);
            vector.Y.Should().BeApproximately(2.0f, 0.0001f);
            vector.Z.Should().BeApproximately(3.0f, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
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
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
            addField.Path.Should().Be("ItemList\\0");
            addField.Label.Should().Be("CustomInt");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddStruct_ShouldLoadToList()
        {
            // Python test: test_gff_add_inside_list
            // Arrange
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_list

[add_list]
FieldType=List
Path=
Label=SomeList
AddField0=add_insidelist

[add_insidelist]
FieldType=Struct
Label=
TypeId=111
2DAMEMORY5=ListIndex
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var mod_0 = result.PatchesGFF[0].Modifiers[0] as AddFieldGFF;
            mod_0.Should().NotBeNull();
            mod_0.Should().BeOfType<AddFieldGFF>();
            mod_0.Value.Should().BeOfType<FieldValueConstant>();
            // Python: assert not mod_0.path.name - checks if path name is empty/falsy
            // When Path is empty and FieldType is List (not Struct), path should be empty
            // In Python, path.name is the last component - if path is empty, name is empty
            mod_0.Path.Should().BeEmpty("Path should be empty when Path= is empty and FieldType is List");
            mod_0.Label.Should().Be("SomeList");

            var mod_1 = mod_0.Modifiers[0] as AddStructToListGFF;
            mod_1.Should().NotBeNull();
            mod_1.Should().BeOfType<AddStructToListGFF>();
            mod_1.Value.Should().BeOfType<FieldValueConstant>();
            var gffStruct = mod_1.Value.Value(null, GFFFieldType.Struct) as GFFStruct;
            gffStruct.Should().NotBeNull();
            gffStruct.StructId.Should().Be(111);
            mod_1.IndexToToken.Should().Be(5);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_Memory2DA_ShouldLoadModifier()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.utc

[test.utc]
AddField0=new_field

[new_field]
FieldType=Int
Label=MyField
Value=0
2DAMEMORY5=!FieldPath
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            var addField = result.PatchesGFF.First(p => p.SaveAs == "test.utc").Modifiers[0] as AddFieldGFF;
            addField.Should().NotBeNull();
            var memory2DA = addField.Modifiers[0] as Memory2DAModifierGFF;
            memory2DA.Should().NotBeNull();
            memory2DA.DestTokenId.Should().Be(5);
            memory2DA.Path.Should().Be("MyField");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
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
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesGFF.Should().HaveCount(2);
            result.PatchesGFF.Should().Contain(p => p.SaveAs == "test1.utc");
            result.PatchesGFF.Should().Contain(p => p.SaveAs == "test2.utc");
        }

    }
}

