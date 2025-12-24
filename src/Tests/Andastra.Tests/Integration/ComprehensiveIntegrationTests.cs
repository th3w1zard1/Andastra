using System;
using System.Collections.Generic;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Comprehensive integration tests combining multiple modification types.
    /// </summary>
    public class ComprehensiveIntegrationTests : IntegrationTestBase
    {
        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void MultipleFileTypes_WithTokenSharing_ShouldApplyCorrectly()
        {
            string iniText = @"
[TLKList]
StrRef0=0

[append.tlk]
0=0

[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row

[add_row]
RowLabel=new_entry
strref_col=StrRef0
2DAMEMORY5=RowIndex

[GFFList]
File0=test.gff

[test.gff]
StrRefField=StrRef0
IndexField=2DAMEMORY5
";
            // Create append.tlk
            TLK appendTlk = CreateTestTLK(new[] { ("Test String", "") });
            SaveTestTLK("append.tlk", appendTlk);

            PatcherConfig config = SetupIniAndConfig(iniText);

            // Setup data
            TwoDA twoda = CreateTest2DA(new[] { "label", "strref_col" }, Array.Empty<(string, string[])>());
            var gff = new GFF();
            var tlk = new TLK();
            tlk.Add("Original");

            var memory = new PatcherMemory();

            // Apply all patches
            config.PatchesTLK.Apply(tlk, memory, new PatchLogger(), BioWareGame.K1);
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);
            byte[] gffBytes = (byte[])config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes(gffBytes);

            // Verify TLK
            tlk.Count.Should().Be(2);
            tlk.String(1).Should().Be("Test String");
            memory.MemoryStr[0].Should().Be(1);

            // Verify 2DA used TLK memory
            twoda.GetHeight().Should().Be(1);
            twoda.GetCellString(0, "strref_col").Should().Be("1");
            memory.Memory2DA[5].Should().Be("0");

            // Verify GFF used both TLK and 2DA memory
            patchedGff.Root.GetInt32("StrRefField").Should().Be(1);
            patchedGff.Root.GetInt32("IndexField").Should().Be(0);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDAWithAllOperationTypes_ShouldApplyInOrder()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change1
AddRow0=add1
CopyRow0=copy1
AddColumn0=newcol

[change1]
RowIndex=0
Col1=changed

[add1]
RowLabel=new_row
Col1=added
Col2=100

[copy1]
RowIndex=0
RowLabel=copied_row
Col2=200

[newcol]
ColumnLabel=Col3
DefaultValue=def
I0=special
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "Col1", "Col2" },
                new[]
                {
                ("0", new[] { "original", "50" })
                }
            );

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeaders().Should().Contain("Col3");
            twoda.GetHeight().Should().Be(3);

            // Changed row
            twoda.GetCellString(0, "Col1").Should().Be("changed");
            twoda.GetCellString(0, "Col2").Should().Be("50");
            twoda.GetCellString(0, "Col3").Should().Be("special");

            // Added row
            twoda.GetLabel(1).Should().Be("new_row");
            twoda.GetCellString(1, "Col1").Should().Be("added");
            twoda.GetCellString(1, "Col2").Should().Be("100");
            twoda.GetCellString(1, "Col3").Should().Be("def");

            // Copied row
            twoda.GetLabel(2).Should().Be("copied_row");
            twoda.GetCellString(2, "Col1").Should().Be("changed");
            twoda.GetCellString(2, "Col2").Should().Be("200");
            twoda.GetCellString(2, "Col3").Should().Be("special");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void GFFWithNestedStructsAndLists_ShouldApplyCorrectly()
        {
            string iniText = @"
[GFFList]
File0=test.gff

[test.gff]
AddField0=add_parent
AddField1=add_child
AddField2=add_list_item

[add_parent]
FieldType=Struct
Path=
Label=Parent
TypeId=100

[add_child]
FieldType=Int
Path=Parent
Label=ChildValue
Value=42

[add_list_item]
FieldType=Struct
Path=ItemList
Label=
TypeId=200
2DAMEMORY0=ListIndex
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var gff = new GFF();
            gff.Root.SetList("ItemList", new GFFList());

            var memory = new PatcherMemory();
            object bytes = config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes((byte[])bytes);

            GFFStruct parent = patchedGff.Root.GetStruct("Parent");
            parent.Should().NotBeNull();
            parent.GetInt32("ChildValue").Should().Be(42);

            GFFList list = patchedGff.Root.GetList("ItemList");
            list.Should().NotBeNull();
            list.Count.Should().Be(1);
            list[0].StructId.Should().Be(200);

            memory.Memory2DA[0].Should().Be("0");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SSFWithMultipleTokenTypes_ShouldApplyCorrectly()
        {
            string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 1=100
Battlecry 2=2DAMEMORY5
Battlecry 3=StrRef7
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            var ssf = new SSF();

            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "200";
            memory.MemoryStr[7] = 300;

            object bytes = config.PatchesSSF.First(p => p.SaveAs == "test.ssf").PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes((byte[])bytes);

            patchedSsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(100);
            patchedSsf.Get(SSFSound.BATTLE_CRY_2).Should().Be(200);
            patchedSsf.Get(SSFSound.BATTLE_CRY_3).Should().Be(300);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDAWithExclusiveColumns_MultipleScenarios_ShouldHandleCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_unique
AddRow1=add_duplicate
CopyRow0=copy_unique
CopyRow1=copy_duplicate

[add_unique]
ExclusiveColumn=id
RowLabel=unique_add
id=100
value=new

[add_duplicate]
ExclusiveColumn=id
RowLabel=duplicate_add
id=1
value=conflict

[copy_unique]
RowIndex=0
ExclusiveColumn=id
RowLabel=unique_copy
id=200
value=copied

[copy_duplicate]
RowIndex=0
ExclusiveColumn=id
RowLabel=duplicate_copy
id=1
value=also_conflict
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "id", "value" },
                new[]
                {
                ("0", new[] { "1", "original" })
                }
            );

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            // Should have: original (id=1 updated), unique_add (id=100), unique_copy (id=200)
            twoda.GetHeight().Should().Be(3);

            // Original row updated by add_duplicate
            twoda.GetCellString(0, "id").Should().Be("1");
            twoda.GetCellString(0, "value").Should().Be("also_conflict"); // last update wins

            // New unique entries
            string row1Id = twoda.GetCellString(1, "id");
            string row2Id = twoda.GetCellString(2, "id");

            (row1Id == "100" || row2Id == "100").Should().BeTrue();
            (row1Id == "200" || row2Id == "200").Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TwoDAWithHighFunction_AcrossColumns_ShouldCalculateCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_with_high
ChangeRow0=change_with_high

[add_with_high]
RowLabel=new
col1=high()
col2=high()
col3=100

[change_with_high]
RowIndex=0
col1=high()
";
            PatcherConfig config = SetupIniAndConfig(iniText);
            TwoDA twoda = CreateTest2DA(
                new[] { "col1", "col2", "col3" },
                new[]
                {
                ("0", new[] { "5", "10", "3" }),
                ("1", new[] { "7", "8", "15" })
                }
            );

            var memory = new PatcherMemory();
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);

            twoda.GetHeight().Should().Be(3);

            // Changed row should have high from col1
            twoda.GetCellString(0, "col1").Should().Be("8"); // high of col1 was 7, but we're setting it to high()

            // New row should have high values
            int newCol1 = int.Parse(twoda.GetCellString(2, "col1"));
            int newCol2 = int.Parse(twoda.GetCellString(2, "col2"));

            newCol1.Should().BeGreaterThan(7); // Should be higher than previous high
            newCol2.Should().BeGreaterThan(10); // Should be higher than previous high
            twoda.GetCellString(2, "col3").Should().Be("100");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AllFileTypes_InSingleConfiguration_ShouldApplyWithoutConflicts()
        {
            string iniText = @"
[TLKList]
StrRef0=0
StrRef1=1

[append.tlk]
0=0
1=1

[2DAList]
Table0=test.2da

[test.2da]
AddRow0=row1
2DAMEMORY0=RowIndex

[row1]
RowLabel=entry
value=StrRef0

[GFFList]
File0=test.gff

[test.gff]
Field1=2DAMEMORY0
Field2=StrRef1

[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 1=StrRef0
Battlecry 2=2DAMEMORY0
";
            // Create TLK file
            TLK appendTlk = CreateTestTLK(new[]
            {
            ("String0", ""),
            ("String1", "")
        });
            SaveTestTLK("append.tlk", appendTlk);

            PatcherConfig config = SetupIniAndConfig(iniText);

            var tlk = new TLK();
            TwoDA twoda = CreateTest2DA(new[] { "label", "value" }, Array.Empty<(string, string[])>());
            var gff = new GFF();
            var ssf = new SSF();

            var memory = new PatcherMemory();

            // Apply all patches
            config.PatchesTLK.Apply(tlk, memory, new PatchLogger(), BioWareGame.K1);
            config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), BioWareGame.K1);
            byte[] gffBytes = (byte[])config.PatchesGFF.First(p => p.SaveAs == "test.gff").PatchResource(gff.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedGff = GFF.FromBytes(gffBytes);
            byte[] ssfBytes = (byte[])config.PatchesSSF.First(p => p.SaveAs == "test.ssf").PatchResource(ssf.ToBytes(), memory, new PatchLogger(), BioWareGame.K1);
            var patchedSsf = SSF.FromBytes(ssfBytes);

            // Verify all modifications
            tlk.Count.Should().Be(2);
            memory.MemoryStr[0].Should().Be(0);
            memory.MemoryStr[1].Should().Be(1);

            twoda.GetHeight().Should().Be(1);
            twoda.GetCellString(0, "value").Should().Be("0");
            memory.Memory2DA[0].Should().Be("0");

            patchedGff.Root.GetInt32("Field1").Should().Be(0);
            patchedGff.Root.GetInt32("Field2").Should().Be(1);

            patchedSsf.Get(SSFSound.BATTLE_CRY_1).Should().Be(0);
            patchedSsf.Get(SSFSound.BATTLE_CRY_2).Should().Be(0);
        }
    }
}

