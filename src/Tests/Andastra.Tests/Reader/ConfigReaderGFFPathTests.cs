using System;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.GFF;
using Andastra.Parsing.Reader;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{

    /// <summary>
    /// ConfigReader tests for GFF path handling edge cases.
    /// </summary>
    public class ConfigReaderGFFPathTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _modPath;
        private readonly IniDataParser _parser;

        public ConfigReaderGFFPathTests()
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
        public void GFFModifierPathShorterThanSelfPath_ShouldOverlayCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct

[add_struct]
FieldType=Struct
Path=Root/ParentStruct
Label=ParentStruct
TypeId=100
AddField0=add_child

[add_child]
FieldType=Byte
Path=ChildField
Label=ChildField
Value=42
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            ModifyGFF mod0 = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[0];
            mod0.Should().BeOfType<AddFieldGFF>();
            var addField0 = (AddFieldGFF)mod0;

            addField0.Modifiers.Should().HaveCount(1);
            ModifyGFF mod1 = addField0.Modifiers[0];
            mod1.Should().BeOfType<AddFieldGFF>();
            var addField1 = (AddFieldGFF)mod1;

            addField1.Path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last().Should().Be("ChildField");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFFModifierPathLongerThanSelfPath_ShouldOverlayCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct

[add_struct]
FieldType=Struct
Path=Root
Label=Root
TypeId=200
AddField0=add_grandchild

[add_grandchild]
FieldType=Byte
Path=ChildStruct/GrandChildField
Label=GrandChildField
Value=99
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            ModifyGFF mod0 = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[0];
            mod0.Should().BeOfType<AddFieldGFF>();
            var addField0 = (AddFieldGFF)mod0;

            addField0.Modifiers.Should().HaveCount(1);
            ModifyGFF mod1 = addField0.Modifiers[0];
            mod1.Should().BeOfType<AddFieldGFF>();
            var addField1 = (AddFieldGFF)mod1;

            addField1.Path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last().Should().Be("GrandChildField");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFFModifierPathPartialAbsolute_ShouldNotDuplicateSegments()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct

[add_struct]
FieldType=Struct
Path=Root/StructA
Label=StructA
TypeId=300
AddField0=add_field_absolute

[add_field_absolute]
FieldType=Byte
Path=StructA/InnerField
Label=InnerField
Value=7
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            ModifyGFF mod0 = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[0];
            mod0.Should().BeOfType<AddFieldGFF>();
            var addField0 = (AddFieldGFF)mod0;

            addField0.Modifiers.Should().HaveCount(1);
            ModifyGFF mod1 = addField0.Modifiers[0];
            mod1.Should().BeOfType<AddFieldGFF>();
            var addField1 = (AddFieldGFF)mod1;

            addField1.Path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Count(p => p == "StructA").Should().Be(1);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFFAddFieldWithSentinelAtStart_ShouldHandleCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct

[add_struct]
FieldType=Struct
Path=Root/>>##INDEXINLIST##<<
Label=TempStruct
TypeId=400
AddField0=add_inside

[add_inside]
FieldType=Byte
Path=>>##INDEXINLIST##<</InnerField
Label=InnerField
Value=55
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            ModifyGFF mod0 = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[0];
            mod0.Should().BeOfType<AddFieldGFF>();
            var addField0 = (AddFieldGFF)mod0;

            addField0.Modifiers.Should().HaveCount(1);
            ModifyGFF mod1 = addField0.Modifiers[0];
            mod1.Should().BeOfType<AddFieldGFF>();
            var addField1 = (AddFieldGFF)mod1;

            addField1.Path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last().Should().Be("InnerField");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFFAddFieldWithEmptyPaths_ShouldDefaultToRoot()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_field

[add_field]
FieldType=Byte
Path=
Label=TopLevelField
Value=99
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            ModifyGFF mod0 = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[0];
            mod0.Should().BeOfType<AddFieldGFF>();
            var addField = (AddFieldGFF)mod0;

            addField.Label.Should().Be("TopLevelField");
            // When Path is empty and FieldType is Byte (not Struct), path should be empty
            // Only Struct fields get ">>##INDEXINLIST##<<" appended when Path is empty
            addField.Path.Should().BeEmpty("Path should be empty when Path= is empty and FieldType is not Struct");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFFAddInsideStruct_IntegrationTest()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct
AddField1=add_insidestruct

[add_struct]
FieldType=Struct
Path=
Label=SomeStruct
TypeId=321

[add_insidestruct]
FieldType=Byte
Path=SomeStruct
Label=InsideStruct
Value=123
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            ModifyGFF mod0 = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[0];
            mod0.Should().BeOfType<AddFieldGFF>();
            var addField0 = (AddFieldGFF)mod0;
            addField0.Value.Should().BeOfType<FieldValueConstant>();
            var fieldValue0 = (FieldValueConstant)addField0.Value;
            fieldValue0.Stored.Should().BeOfType<GFFStruct>();
            var struct0 = (GFFStruct)fieldValue0.Stored;
            struct0.StructId.Should().Be(321);
            addField0.Path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last().Should().Be(">>##INDEXINLIST##<<");
            addField0.Label.Should().Be("SomeStruct");

            ModifyGFF mod1 = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[1];
            mod1.Should().BeOfType<AddFieldGFF>();
            var addField1 = (AddFieldGFF)mod1;
            addField1.Path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last().Should().Be("SomeStruct");
            addField1.Label.Should().Be("InsideStruct");
            addField1.Value.Should().BeOfType<FieldValueConstant>();
            var fieldValue1 = (FieldValueConstant)addField1.Value;
            fieldValue1.Stored.Should().Be(123);

            // Apply patch end-to-end
            var gff = new GFF();
            var memory = new PatcherMemory();
            object bytes = result.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patched = GFF.FromBytes((byte[])bytes);

            GFFStruct someStruct = patched.Root.GetStruct("SomeStruct");
            someStruct.Should().NotBeNull();
            byte insideStructValue = someStruct.GetUInt8("InsideStruct");
            insideStructValue.Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFFAddFieldLocString_With2DAMemory_IntegrationTest()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_loc

[add_loc]
FieldType=ExoLocString
Path=
Label=Field1
StrRef=2DAMEMORY5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            ModifyGFF addMod = result.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers[0];
            addMod.Should().BeOfType<AddFieldGFF>();
            var addField = (AddFieldGFF)addMod;
            addField.Value.Should().BeOfType<FieldValueConstant>();
            var fieldValue = (FieldValueConstant)addField.Value;
            fieldValue.Stored.Should().BeOfType<LocalizedStringDelta>();

            // Apply patch end-to-end
            var gff = new GFF();
            gff.Root.SetLocString("Field1", new LocalizedString(0));

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123";

            object bytes = result.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patched = GFF.FromBytes((byte[])bytes);

            patched.Root.GetLocString("Field1").StringRef.Should().Be(123);
        }

    }
}
