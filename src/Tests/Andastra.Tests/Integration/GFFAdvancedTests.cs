using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Mods.GFF;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Advanced GFF tests covering edge cases and complex path handling.
    /// Ported from test_tslpatcher.py - Advanced GFF scenarios.
    /// </summary>
    public class GFFAdvancedTests : IntegrationTestBase
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddInsideStruct_ShouldRegisterModifiersCorrectly()
        {
            // Arrange - test from test_gff_add_inside_struct
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct
AddField1=add_insidestruct

[add_struct]
FieldType=Struct
TypeId=0
Label=TestStruct

[add_insidestruct]
FieldType=Byte
Label=InnerField
Value=123
Path=TestStruct
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Assert
            config.PatchesGFF.Should().Contain(p => p.SaveAs == "test.gff");
            List<ModifyGFF> modifiers = config.PatchesGFF.First(p => p.SaveAs == "test.gff").Modifiers;
            modifiers.Should().HaveCount(2);

            var addStruct = modifiers[0] as AddFieldGFF;
            addStruct.Should().NotBeNull();
            addStruct.Label.Should().Be("TestStruct");
            addStruct.FieldType.Should().Be(GFFFieldType.Struct);

            var addInside = modifiers[1] as AddFieldGFF;
            addInside.Should().NotBeNull();
            addInside.Label.Should().Be("InnerField");
            addInside.Path.Should().Be("TestStruct");

            // Act - Apply to actual GFF
            var gff = new GFF();
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.Exists("TestStruct").Should().BeTrue();
            GFFStruct testStruct = gff.Root.GetStruct("TestStruct");
            testStruct.Exists("InnerField").Should().BeTrue();
            testStruct.GetUInt8("InnerField").Should().Be(123);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddFieldWithLocalizedString_ShouldHandleSubstrings()
        {
            // Arrange - test from test_gff_add_field_locstring
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_locstring

[add_locstring]
FieldType=ExoLocString
Label=Description
Value(strref)=100
Value(lang0)=English text
Value(lang2)=French text
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Act
            var gff = new GFF();
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.Exists("Description").Should().BeTrue();
            LocalizedString locString = gff.Root.GetLocString("Description");
            locString.StringRef.Should().Be(100);
            locString.Get(Language.English, Gender.Male).Should().Be("English text");
            locString.Get(Language.French, Gender.Male).Should().Be("French text");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifierPath_ShorterThanSelfPath_ShouldApplyCorrectly()
        {
            // Arrange - test from test_gff_modifier_path_shorter_than_self_path
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=outer_struct
AddField1=inner_field

[outer_struct]
FieldType=Struct
TypeId=0
Label=OuterStruct

[inner_field]
FieldType=Byte
Label=InnerField
Value=42
Path=OuterStruct
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Act
            var gff = new GFF();
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFStruct outerStruct = gff.Root.GetStruct("OuterStruct");
            outerStruct.GetUInt8("InnerField").Should().Be(42);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifierPath_LongerThanSelfPath_ShouldNavigateCorrectly()
        {
            // Arrange - test from test_gff_modifier_path_longer_than_self_path (Python line 358)
            // Python test only checks path resolution, not field addition
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
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Act - Python test only checks path resolution
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            var mod0 = modifications.Modifiers[0] as AddFieldGFF;
            mod0.Should().NotBeNull();
            var mod1 = mod0.Modifiers[0] as AddFieldGFF;
            mod1.Should().NotBeNull();

            // Assert - Python line 390: checks that path.parts[-1] == "GrandChildField"
            mod1.Path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Last().Should().Be("GrandChildField");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifierPath_PartialAbsolute_ShouldResolveCorrectly()
        {
            // Arrange - test from test_gff_modifier_path_partial_absolute
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=struct_a
AddField1=struct_b
AddField2=field_in_b

[struct_a]
FieldType=Struct
TypeId=0
Label=StructA

[struct_b]
FieldType=Struct
TypeId=0
Label=StructB
Path=StructA

[field_in_b]
FieldType=Byte
Label=FieldInB
Value=77
Path=StructA\StructB
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Act
            var gff = new GFF();
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFStruct structA = gff.Root.GetStruct("StructA");
            GFFStruct structB = structA.GetStruct("StructB");
            structB.GetUInt8("FieldInB").Should().Be(77);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddFieldWithSentinelAtStart_ShouldHandleCorrectly()
        {
            // Arrange - test from test_gff_add_field_with_sentinel_at_start
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=sentinel_field

[sentinel_field]
FieldType=Byte
Label=SentinelField
Value=55
Path=\ExistingStruct
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Create GFF with existing struct
            var gff = new GFF();
            var existingStruct = new GFFStruct(0);
            gff.Root.SetStruct("ExistingStruct", existingStruct);

            // Act
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFStruct existingStructResult = gff.Root.GetStruct("ExistingStruct");
            existingStructResult.GetUInt8("SentinelField").Should().Be(55);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddFieldWithEmptyPaths_ShouldAddToRoot()
        {
            // Arrange - test from test_gff_add_field_with_empty_paths
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=root_field

[root_field]
FieldType=Byte
Label=RootField
Value=88
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Act
            var gff = new GFF();
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.Exists("RootField").Should().BeTrue();
            gff.Root.GetUInt8("RootField").Should().Be(88);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddStructToList_ShouldHandleListIndex()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=new_item

[new_item]
FieldType=Struct
Label=
Path=ItemList
TypeId=0
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Create GFF with list
            var gff = new GFF();
            var itemList = new GFFList();
            gff.Root.SetList("ItemList", itemList);

            // Act
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFList resultList = gff.Root.GetList("ItemList");
            resultList.Count.Should().BeGreaterThan(0);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ComplexNesting_ShouldNavigateDeepStructures()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
Level1\Level2\Level3\DeepField=999
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Create deeply nested structure
            var gff = new GFF();
            var level1 = new GFFStruct(0);
            var level2 = new GFFStruct(0);
            var level3 = new GFFStruct(0);
            level3.SetInt32("DeepField", 0);
            level2.SetStruct("Level3", level3);
            level1.SetStruct("Level2", level2);
            gff.Root.SetStruct("Level1", level1);

            // Act
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFStruct level1Result = gff.Root.GetStruct("Level1");
            GFFStruct level2Result = level1Result.GetStruct("Level2");
            GFFStruct level3Result = level2Result.GetStruct("Level3");
            level3Result.GetInt32("DeepField").Should().Be(999);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyListElement_ShouldAccessByIndex()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
ItemList\0\Tag=modified_tag
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Create GFF with list containing a struct
            var gff = new GFF();
            var itemList = new GFFList();
            GFFStruct item = itemList.Add(0);
            item.SetString("Tag", "original_tag");
            gff.Root.SetList("ItemList", itemList);

            // Act
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFList resultList = gff.Root.GetList("ItemList");
            resultList[0].GetValue("Tag").Should().Be("modified_tag");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_Memory2DA_WithComplexPath_ShouldStoreValue()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
2DAMEMORY5=!FieldPath

[!FieldPath]
Path=Nested\\Field
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Create GFF with nested structure
            var gff = new GFF();
            var nested = new GFFStruct(0);
            nested.SetInt32("Field", 123);
            gff.Root.SetStruct("Nested", nested);

            // Act
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert - Python: memory.memory_2da[self.dest_token_id] = self.path
            Memory.Memory2DA[5].Should().Be("Nested\\Field");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddMultipleFieldsToSameStruct_ShouldAddAll()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=field1
AddField1=field2
AddField2=field3

[field1]
FieldType=Byte
Label=Field1
Value=1
Path=TestStruct

[field2]
FieldType=Byte
Label=Field2
Value=2
Path=TestStruct

[field3]
FieldType=Byte
Label=Field3
Value=3
Path=TestStruct
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Create GFF with struct
            var gff = new GFF();
            var testStruct = new GFFStruct(0);
            gff.Root.SetStruct("TestStruct", testStruct);

            // Act
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            GFFStruct result = gff.Root.GetStruct("TestStruct");
            result.GetUInt8("Field1").Should().Be(1);
            result.GetUInt8("Field2").Should().Be(2);
            result.GetUInt8("Field3").Should().Be(3);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_MixedModifyAndAdd_ShouldApplyInOrder()
        {
            // Arrange
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
ExistingField=modified_value
AddField0=new_field

[new_field]
FieldType=Byte
Label=NewField
Value=42
";
            Andastra.Parsing.Config.PatcherConfig config = SetupIniAndConfig(iniText);

            // Create GFF with existing field
            var gff = new GFF();
            gff.Root.SetString("ExistingField", "original_value".ToString());

            // Act
            ModificationsGFF modifications = config.PatchesGFF.First(p => p.SaveAs == "test.gff");
            modifications.Apply(gff, Memory, Logger, BioWareGame.K1);

            // Assert
            gff.Root.GetValue("ExistingField").Should().Be("modified_value");
            gff.Root.GetUInt8("NewField").Should().Be(42);
        }
    }
}
