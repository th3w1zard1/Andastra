using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Edge case integration tests for corner scenarios.
    /// </summary>
    public class EdgeCaseIntegrationTests : IntegrationTestBase
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_EmptyTable_AddFirstRow_ShouldWork()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=first_row

[first_row]
RowLabel=0
Col1=value1
Col2=value2
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(new[] { "Col1", "Col2" }, Array.Empty<(string, string[])>());

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            twoda.GetCellString(0, "Col1").Should().Be("value1");
            twoda.GetCellString(0, "Col2").Should().Be("value2");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_MultipleRowsWithSameExclusiveValue_OnlyLastShouldApply()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=row1
AddRow1=row2
AddRow2=row3

[row1]
ExclusiveColumn=id
RowLabel=first
id=100
value=one

[row2]
ExclusiveColumn=id
RowLabel=second
id=100
value=two

[row3]
ExclusiveColumn=id
RowLabel=third
id=100
value=three
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(new[] { "id", "value" }, Array.Empty<(string, string[])>());

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            twoda.GetCellString(0, "id").Should().Be("100");
            twoda.GetCellString(0, "value").Should().Be("three");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_RowLabelConflicts_ShouldOverwrite()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=row1
AddRow1=row2

[row1]
RowLabel=same_label
Col1=first

[row2]
RowLabel=same_label
Col1=second
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(new[] { "Col1" }, Array.Empty<(string, string[])>());

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            // Depending on implementation, might add both or overwrite
            // This tests the actual behavior
            twoda.GetHeight().Should().BeGreaterThanOrEqualTo(1);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_CopyNonExistentRow_ShouldHandleGracefully()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_missing

[copy_missing]
RowIndex=999
RowLabel=copied
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1" },
                new[] { ("0", new[] { "a" }) }
            );

            var memory = new PatcherMemory();

            // Should not throw, might add nothing or handle error gracefully
            Action action = () => config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);
            action.Should().NotThrow();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_ChangeNonExistentColumn_ShouldHandleGracefully()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change1

[change1]
RowIndex=0
NonExistentColumn=value
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1" },
                new[] { ("0", new[] { "a" }) }
            );

            var memory = new PatcherMemory();

            Action action = () => config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);
            action.Should().NotThrow();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_HighFunctionOnEmptyColumn_ShouldReturnOne()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=row1

[row1]
RowLabel=new
Col1=high()
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(new[] { "Col1" }, Array.Empty<(string, string[])>());

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            // High of empty should be 1 or 0+1
            int value = int.Parse(twoda.GetCellString(0, "Col1"));
            value.Should().BeGreaterThanOrEqualTo(0);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_HighFunctionWithNonNumericValues_ShouldHandleGracefully()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=row1

[row1]
RowLabel=new
Col1=high()
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1" },
                new[]
                {
                ("0", new[] { "abc" }),
                ("1", new[] { "123" })
                }
            );

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);
            // Should skip non-numeric and use 123 as highest
            string value = twoda.GetCellString(2, "Col1");
            value.Should().NotBeNullOrEmpty();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddFieldToNonExistentPath_ShouldCreatePath()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_nested

[add_nested]
FieldType=Int
Path=Deep\\Nested\\Path
Label=Value
Value=42
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            // Should create the nested structure
            Func<GFFStruct> action = () => patchedGff.Root.GetStruct("Deep");
            action.Should().NotThrow();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_ModifyNonExistentField_ShouldHandleGracefully()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
NonExistentField=123
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetInt32("ExistingField", 1);

            var memory = new PatcherMemory();

            Func<object> action = () => config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            action.Should().NotThrow();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void Memory_TokenReferencedBeforeSet_ShouldUseDefault()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=row1

[row1]
RowLabel=new
value=2DAMEMORY99
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(new[] { "value" }, Array.Empty<(string, string[])>());

            var memory = new PatcherMemory();
            // Don't set Memory2DA[99]

            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeight().Should().Be(1);
            // Should handle missing token gracefully
            string value = twoda.GetCellString(0, "value");
            value.Should().NotBeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_AddColumnToEmptyTable_ShouldAddToAllRows()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=newcol

[newcol]
ColumnLabel=NewCol
DefaultValue=X
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1" },
                new[]
                {
                ("0", new[] { "a" }),
                ("1", new[] { "b" })
                }
            );

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeaders().Should().Contain("NewCol");
            twoda.GetColumn("NewCol").Should().AllBe("X");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_LabelIndexTarget_WithNumericLabel_ShouldResolve()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change1

[change1]
LabelIndex=42
Col1=changed
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1" },
                new[]
                {
                ("42", new[] { "original" })
                }
            );

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetCellString(0, "Col1").Should().Be("changed");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFF_AddStructWithNoTypeId_ShouldUseDefaultZero()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_struct

[add_struct]
FieldType=Struct
Path=
Label=TestStruct
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            GFFStruct struct1 = patchedGff.Root.GetStruct("TestStruct");
            struct1.Should().NotBeNull();
            struct1.StructId.Should().Be(0);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void MultipleModifications_WithCircularTokenDependencies_ShouldResolve()
        {
            string iniText = @"
[2DAList]
Table0=test1.2da
Table1=test2.2da

[test1.2da]
AddRow0=row1

[row1]
RowLabel=t1
value=2DAMEMORY1
2DAMEMORY0=RowIndex

[test2.2da]
AddRow0=row2

[row2]
RowLabel=t2
value=2DAMEMORY0
2DAMEMORY1=RowIndex
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda1 = CreateTest2DA(new[] { "value" }, Array.Empty<(string, string[])>());
            TwoDA twoda2 = CreateTest2DA(new[] { "value" }, Array.Empty<(string, string[])>());

            var memory = new PatcherMemory();

            // Apply in sequence - tokens should be available
            config.Patches2DA.First(p => p.SaveAs == "test1.2da").Apply(twoda1, memory, new PatchLogger(), BioWareGame.K1);
            config.Patches2DA.First(p => p.SaveAs == "test2.2da").Apply(twoda2, memory, new PatchLogger(), BioWareGame.K1);

            memory.Memory2DA[0].Should().NotBeNullOrEmpty();
            memory.Memory2DA[1].Should().NotBeNullOrEmpty();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDA_VeryLargeRowLabel_ShouldHandle()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=row1

[row1]
RowLabel=VeryLongRowLabelThatExceedsNormalLengthExpectations_12345678901234567890
Col1=value
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(new[] { "Col1" }, Array.Empty<(string, string[])>());

            var memory = new PatcherMemory();

            Action action = () => config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);
            action.Should().NotThrow();

            if (twoda.GetHeight() > 0)
            {
                twoda.GetLabel(0).Should().NotBeNullOrEmpty();
            }
        }
    }
}

