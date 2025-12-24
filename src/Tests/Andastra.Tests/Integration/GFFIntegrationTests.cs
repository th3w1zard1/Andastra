using System;
using System.Linq;
using System.Numerics;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.GFF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Integration tests for GFF patching workflows.
    /// Ported from test_tslpatcher.py - GFF integration tests.
    /// </summary>
    public class GFFIntegrationTests : IntegrationTestBase
    {
        #region ModifyField Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt8_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetUInt8("TestField", 10);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant((byte)25));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetUInt8("TestField").Should().Be(25);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int8_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt8("TestField", -10);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant((sbyte)-25));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetInt8("TestField").Should().Be(-25);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt16_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetUInt16("TestField", 100);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant((ushort)500));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetUInt16("TestField").Should().Be(500);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int16_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt16("TestField", -100);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant((short)-500));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetInt16("TestField").Should().Be(-500);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt32_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetUInt32("TestField", 1000);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant(5000u));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetUInt32("TestField").Should().Be(5000);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int32_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt32("TestField", -1000);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant(-5000));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetInt32("TestField").Should().Be(-5000);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_UInt64_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetUInt64("TestField", 10000);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant(50000ul));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetUInt64("TestField").Should().Be(50000);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Int64_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt64("TestField", -10000);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant(-50000L));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetInt64("TestField").Should().Be(-50000);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Float_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetSingle("TestField", 1.5f);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant(3.14f));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetSingle("TestField").Should().BeApproximately(3.14f, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Double_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetDouble("TestField", 1.5);

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant(3.14159));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetDouble("TestField").Should().BeApproximately(3.14159, 0.00001);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_String_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetString("TestField", "OldValue".ToString());

            var modify = new ModifyFieldGFF("TestField", new FieldValueConstant("NewValue"));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetValue("TestField").Should().Be("NewValue");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Vector3_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetVector3("Position", new Vector3(1, 2, 3));

            var newVector = new Vector3(10, 20, 30);
            var modify = new ModifyFieldGFF("Position", new FieldValueConstant(newVector));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            Vector3 result = gff.Root.GetVector3("Position");
            result.X.Should().BeApproximately(10, 0.0001f);
            result.Y.Should().BeApproximately(20, 0.0001f);
            result.Z.Should().BeApproximately(30, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_Vector4_ShouldUpdateValue()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetVector4("Rotation", new Vector4(1, 0, 0, 0));

            var newVector = new Vector4(0, 1, 0, 0);
            var modify = new ModifyFieldGFF("Rotation", new FieldValueConstant(newVector));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            Vector4 result = gff.Root.GetVector4("Rotation");
            result.X.Should().BeApproximately(0, 0.0001f);
            result.Y.Should().BeApproximately(1, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_LocalizedString_ShouldUpdateValue()
        {
            // Python test: test_modify_field_locstring
            // Python: gff.root.set_locstring("Field1", LocalizedString(0))
            var gff = new GFF();
            gff.Root.SetLocString("Field1", new LocalizedString(0));

            // Python: FieldValueConstant(LocalizedStringDelta(FieldValueConstant(1)))
            var delta = new LocalizedStringDelta(new FieldValueConstant(1));
            var modify = new ModifyFieldGFF("Field1", new FieldValueConstant(delta));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert - Python: assert gff.root.get_locstring("Field1").stringref == 1
            LocalizedString result = gff.Root.GetLocString("Field1");
            result.StringRef.Should().Be(1);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_WithTLKMemory_ShouldUseToken()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt32("NameStrRef", 100);

            Memory.MemoryStr[5] = 12345;

            var modify = new ModifyFieldGFF("NameStrRef", new FieldValueTLKMemory(5));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetInt32("NameStrRef").Should().Be(12345);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_With2DAMemory_ShouldUseToken()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt32("AppearanceType", 10);

            Memory.Memory2DA[3] = "999";

            var modify = new ModifyFieldGFF("AppearanceType", new FieldValue2DAMemory(3));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetInt32("AppearanceType").Should().Be(999);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void ModifyField_NestedPath_ShouldModifyCorrectField()
        {
            // Arrange
            var gff = new GFF();
            var nestedStruct = new GFFStruct(0);
            nestedStruct.SetString("InnerField", "OldValue".ToString());
            gff.Root.SetStruct("OuterStruct", nestedStruct);

            var modify = new ModifyFieldGFF("OuterStruct\\InnerField", new FieldValueConstant("NewValue"));

            // Act
            modify.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFStruct result = gff.Root.GetStruct("OuterStruct");
            result.GetValue("InnerField").Should().Be("NewValue");
        }

        #endregion

        #region AddField Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_Int_ShouldAddNewField()
        {
            // Arrange
            var gff = new GFF();

            var add = new AddFieldGFF("Test", "NewIntField", GFFFieldType.Int32, new FieldValueConstant(42), null);

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.Exists("NewIntField").Should().BeTrue();
            gff.Root.GetInt32("NewIntField").Should().Be(42);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_String_ShouldAddNewField()
        {
            // Arrange
            var gff = new GFF();

            var add = new AddFieldGFF("Test", "NewStringField", GFFFieldType.String, new FieldValueConstant("TestString"), null);

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.Exists("NewStringField").Should().BeTrue();
            gff.Root.GetValue("NewStringField").Should().Be("TestString");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_Float_ShouldAddNewField()
        {
            // Arrange
            var gff = new GFF();

            var add = new AddFieldGFF("Test", "NewFloatField", GFFFieldType.Single, new FieldValueConstant(3.14f), null);

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.Exists("NewFloatField").Should().BeTrue();
            gff.Root.GetSingle("NewFloatField").Should().BeApproximately(3.14f, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_Vector3_ShouldAddNewField()
        {
            // Arrange
            var gff = new GFF();
            var vector = new Vector3(1, 2, 3);

            var add = new AddFieldGFF("Test", "NewVector", GFFFieldType.Vector3, new FieldValueConstant(vector), null);

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.Exists("NewVector").Should().BeTrue();
            Vector3 result = gff.Root.GetVector3("NewVector");
            result.X.Should().BeApproximately(1, 0.0001f);
            result.Y.Should().BeApproximately(2, 0.0001f);
            result.Z.Should().BeApproximately(3, 0.0001f);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_InNestedStruct_ShouldAddAtCorrectPath()
        {
            // Arrange
            var gff = new GFF();
            var nestedStruct = new GFFStruct(0);
            gff.Root.SetStruct("OuterStruct", nestedStruct);

            var add = new AddFieldGFF("Test", "NewField", GFFFieldType.Int32, new FieldValueConstant(100), "OuterStruct");

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFStruct result = gff.Root.GetStruct("OuterStruct");
            result.Exists("NewField").Should().BeTrue();
            result.GetInt32("NewField").Should().Be(100);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddField_LocalizedString_ShouldAddWithSubstrings()
        {
            // Arrange
            var gff = new GFF();
            var locString = new LocalizedString(100);
            locString.SetData(Language.English, Gender.Male, "English text");
            locString.SetData(Language.French, Gender.Male, "French text");

            var add = new AddFieldGFF("Test", "Description", GFFFieldType.LocalizedString, new FieldValueConstant(locString), null);

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            LocalizedString result = gff.Root.GetLocString("Description");
            result.StringRef.Should().Be(100);
            result.Get(Language.English, Gender.Male).Should().Be("English text");
            result.Get(Language.French, Gender.Male).Should().Be("French text");
        }

        #endregion

        #region AddStruct Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddStruct_ToList_ShouldAddNewStruct()
        {
            // Arrange
            var gff = new GFF();
            var list = new GFFList();
            gff.Root.SetList("ItemList", list);

            var newStruct = new GFFStruct(0);
            newStruct.SetString("Tag", "item001");
            var add = new AddStructToListGFF("Test", new FieldValueConstant(newStruct), "ItemList", null);

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFList result = gff.Root.GetList("ItemList");
            result.Count.Should().Be(1);
            result[0].GetValue("Tag").Should().Be("item001");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddStruct_WithNestedFields_ShouldAddCompleteStruct()
        {
            // Arrange
            var gff = new GFF();
            var list = new GFFList();
            gff.Root.SetList("ItemList", list);

            var newStruct = new GFFStruct(0);
            newStruct.SetString("Tag", "item001");
            newStruct.SetInt32("StackSize", 10);
            var add = new AddStructToListGFF("Test", new FieldValueConstant(newStruct), "ItemList", null);

            // Act
            add.Apply(gff.Root, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFList result = gff.Root.GetList("ItemList");
            result[0].GetValue("Tag").Should().Be("item001");
            result[0].GetInt32("StackSize").Should().Be(10);
        }

        #endregion

        #region Memory2DA Modifier Tests

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Memory2DA_ShouldStoreFieldPath()
        {
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt32("AppearanceType", 123);

            var memory2DA = new Memory2DAModifierGFF("Test", "AppearanceType", 5);

            // Act
            memory2DA.Apply(gff.Root, Memory, Logger);

            // Assert - When src_token_id is None, Python stores the path, not the value
            Memory.Memory2DA[5].Should().Be("AppearanceType");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Memory2DA_WithSourceToken_ShouldStoreFromToken()
        {
            // Python: When dest_field exists, it assigns ptr_to_src to the field value, not to memory
            // Arrange
            var gff = new GFF();
            gff.Root.SetInt32("AppearanceType", 100);

            Memory.Memory2DA[3] = "999";

            var memory2DA = new Memory2DAModifierGFF("Test", "AppearanceType", 5, 3);

            // Act
            memory2DA.Apply(gff.Root, Memory, Logger);

            // Assert - Python assigns ptr_to_src to the field when dest_field exists
            // Python: dest_field._value = FieldValueConstant(ptr_to_src).value(memory, dest_field.field_type())
            gff.Root.GetInt32("AppearanceType").Should().Be(999);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddListListIndex_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct1
AddField1=add_struct2
AddField2=add_struct3

[add_struct1]
FieldType=Struct
Path=List
Label=
TypeId=5

[add_struct2]
FieldType=Struct
Path=List
Label=
TypeId=3

[add_struct3]
FieldType=Struct
Path=List
Label=
TypeId=1
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            var gffList = new GFFList();
            gff.Root.SetList("List", gffList);

            var memory = new PatcherMemory();
            var writer = new GFFBinaryWriter(gff);
            byte[] bytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(bytes, memory, new PatchLogger(), BioWareGame.K1);
            var reader = new GFFBinaryReader(patchedBytes);
            GFF patchedGff = reader.Load();

            GFFList list = patchedGff.Root.GetList("List");
            list.Should().NotBeNull();
            list.Count.Should().Be(3);
            list[0].StructId.Should().Be(5);
            list[1].StructId.Should().Be(3);
            list[2].StructId.Should().Be(1);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddListStore2DAMemory_AppliesPatchCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct1
AddField1=add_struct2

[add_struct1]
FieldType=Struct
Path=List
Label=
TypeId=0

[add_struct2]
FieldType=Struct
Path=List
Label=
TypeId=0
2DAMEMORY12=ListIndex
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetList("List", new GFFList());

            var memory = new PatcherMemory();
            var writer = new GFFBinaryWriter(gff);
            byte[] bytes = writer.Write();
            byte[] patchedBytes = (byte[])config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(bytes, memory, new PatchLogger(), BioWareGame.K1);
            var reader = new GFFBinaryReader(patchedBytes);
            GFF patchedGff = reader.Load();

            GFFList list = patchedGff.Root.GetList("List");
            list.Should().NotBeNull();
            list.Count.Should().Be(2);
            memory.Memory2DA[12].Should().Be("1");
        }

        #endregion
    }
}
