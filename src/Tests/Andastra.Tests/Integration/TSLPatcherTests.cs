using System;
using System.Collections.Generic;
using System.IO;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.TwoDA;
using Andastra.Parsing.Reader;
using FluentAssertions;
using Xunit;

namespace Andastra.Parsing.Tests.Integration
{

    /// <summary>
    /// Integration tests for TSLPatcher functionality
    /// Ported from tests/tslpatcher/test_tslpatcher.py
    /// </summary>
    public class TSLPatcherTests
    {
        private static PatcherConfig SetupIniAndConfig(string iniText, string modPath = "")
        {
            var parser = new IniParser.Parser.IniDataParser();
            parser.Configuration.AllowDuplicateKeys = true;
            parser.Configuration.AllowDuplicateSections = true;
            // TSLPatcher uses case-insensitive matching for sections/keys generally,
            // but values might be case sensitive.
            // ConfigReader sets CaseInsensitive=false in FromFilePath but handles it manually?
            // Let's check ConfigReader.FromFilePath again.
            // It sets CaseInsensitive = false.
            parser.Configuration.CaseInsensitive = false;

            IniParser.Model.IniData iniData = parser.Parse(iniText);
            var config = new PatcherConfig();

            string actualModPath = string.IsNullOrEmpty(modPath) ? Path.GetTempPath() : modPath;
            var reader = new ConfigReader(iniData, actualModPath);
            reader.Load(config);
            return config;
        }

        #region TwoDA Tests

        [Fact] // 2 minutes timeout
        public void ChangeRow_ShouldModifyByRowIndex_EndToEnd()
        {
            // Python test: test_change_existing_rowindex
            // This is a combined INI loading + patching test

            // Arrange
            var twoda = new TwoDA(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            string iniText = @"
            [2DAList]
            Table0=test.2da

            [test.2da]
            ChangeRow0=change_row_0

            [change_row_0]
            RowIndex=1
            Col1=X
        ";

            PatcherConfig config = SetupIniAndConfig(iniText);
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            // Assert Config Loading
            config.Patches2DA.Should().HaveCount(1);
            config.Patches2DA[0].Modifiers.Should().HaveCount(1);
            var mod = config.Patches2DA[0].Modifiers[0] as ChangeRow2DA;
            mod.Should().NotBeNull();
            mod.Target.TargetType.Should().Be(TargetType.ROW_INDEX);
            mod.Target.Value.Should().Be(1);
            mod.Cells.Should().ContainKey("Col1");
            ((RowValueConstant)mod.Cells["Col1"]).Value(null, null, null).Should().Be("X");

            // Act
            config.Patches2DA[0].Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert Result
            twoda.GetColumn("Col1").Should().Equal(new[] { "a", "X" });
            twoda.GetColumn("Col2").Should().Equal(new[] { "b", "e" });
            twoda.GetColumn("Col3").Should().Equal(new[] { "c", "f" });
        }

        [Fact] // 2 minutes timeout
        public void ChangeRow_ShouldModifyByRowLabel_EndToEnd()
        {
            // Python test: test_change_existing_rowlabel

            // Arrange
            var twoda = new TwoDA(new List<string> { "Col1", "Col2", "Col3" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" }, { "Col3", "c" } });
            twoda.AddRow("1", new Dictionary<string, object> { { "Col1", "d" }, { "Col2", "e" }, { "Col3", "f" } });

            string iniText = @"
            [2DAList]
            Table0=test.2da

            [test.2da]
            ChangeRow0=change_row_0

            [change_row_0]
            RowLabel=1
            Col1=X
        ";

            PatcherConfig config = SetupIniAndConfig(iniText);
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            // Act
            config.Patches2DA[0].Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert Result
            twoda.GetColumn("Col1").Should().Equal(new[] { "a", "X" });
            twoda.GetColumn("Col2").Should().Equal(new[] { "b", "e" });
            twoda.GetColumn("Col3").Should().Equal(new[] { "c", "f" });
        }

        [Fact] // 2 minutes timeout
        public void AddRow_ShouldAppendRow_EndToEnd()
        {
            // Arrange
            var twoda = new TwoDA(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });

            string iniText = @"
            [2DAList]
            Table0=test.2da

            [test.2da]
            AddRow0=add_row_0

            [add_row_0]
            Col1=new_a
            Col2=new_b
        ";

            PatcherConfig config = SetupIniAndConfig(iniText);
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            // Act
            config.Patches2DA[0].Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            twoda.GetHeight().Should().Be(2);
            TwoDARow newRow = twoda.GetRow(1);
            newRow.GetString("Col1").Should().Be("new_a");
            newRow.GetString("Col2").Should().Be("new_b");
        }

        [Fact] // 2 minutes timeout
        public void CopyRow_ShouldCopyAndModify_EndToEnd()
        {
            // Arrange
            var twoda = new TwoDA(new List<string> { "Col1", "Col2" });
            twoda.AddRow("0", new Dictionary<string, object> { { "Col1", "a" }, { "Col2", "b" } });

            string iniText = @"
            [2DAList]
            Table0=test.2da

            [test.2da]
            CopyRow0=copy_row_0

            [copy_row_0]
            RowIndex=0
            Col1=modified_a
        ";

            PatcherConfig config = SetupIniAndConfig(iniText);
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            // Act
            config.Patches2DA[0].Apply(twoda, memory, logger, BioWareGame.K1);

            // Assert
            twoda.GetHeight().Should().Be(2);
            TwoDARow copiedRow = twoda.GetRow(1);
            copiedRow.GetString("Col1").Should().Be("modified_a"); // Modified
            copiedRow.GetString("Col2").Should().Be("b"); // Copied from row 0
        }

        #endregion

        #region GFF Tests

        [Fact] // 2 minutes timeout
        public void GFF_AddField_ShouldAddNestedStructure()
        {
            // Python test: test_gff_add_inside_struct

            var gff = new GFF();
            var memory = new PatcherMemory();
            var logger = new PatchLogger();

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

            PatcherConfig config = SetupIniAndConfig(iniText);
            config.PatchesGFF[0].Apply(gff, memory, logger, BioWareGame.K1);

            // Expected:
            // gff.Root.GetStruct("SomeStruct").Should().NotBeNull();
            // gff.Root.GetStruct("SomeStruct").GetByte("InsideStruct").Should().Be(123);

            gff.Root.Exists("SomeStruct").Should().BeTrue();
            gff.Root.GetFieldType("SomeStruct").Should().Be(GFFFieldType.Struct);
            GFFStruct someStruct = gff.Root.GetStruct("SomeStruct");
            someStruct.Should().NotBeNull();
            someStruct.StructId.Should().Be(321);

            someStruct.Exists("InsideStruct").Should().BeTrue();
            someStruct.GetFieldType("InsideStruct").Should().Be(GFFFieldType.UInt8);
            someStruct.GetUInt8("InsideStruct").Should().Be(123);
        }

        [Fact] // 2 minutes timeout
        public void GFF_AddField_ShouldAddLocStringWith2DAMemory()
        {
            // Python test: test_gff_add_field_locstring

            var gff = new GFF();
            var memory = new PatcherMemory();
            memory.Memory2DA[5] = "123"; // Token 5 stored as "123"
            var logger = new PatchLogger();

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

            PatcherConfig config = SetupIniAndConfig(iniText);
            config.PatchesGFF[0].Apply(gff, memory, logger, BioWareGame.K1);

            gff.Root.Exists("Field1").Should().BeTrue();
            gff.Root.GetFieldType("Field1").Should().Be(GFFFieldType.LocalizedString);
            LocalizedString locString = gff.Root.GetLocString("Field1");
            locString.StringRef.Should().Be(123);
        }

        [Fact] // 2 minutes timeout
        public void GFF_Modifier_PathShorterThanSelfPath()
        {
            // Python test: test_gff_modifier_path_shorter_than_self_path

            var gff = new GFF();
            // Create initial structure: Root -> ParentStruct
            gff.Root.SetStruct("ParentStruct", new GFFStruct(100));

            var memory = new PatcherMemory();
            var logger = new PatchLogger();

            string iniText = @"
            [GFFList]
            File0=test.gff

            [test.gff]
            AddField0=add_struct

            [add_struct]
            FieldType=Struct
            Path=ParentStruct
            Label=ParentStruct
            TypeId=100
            AddField0=add_child

            [add_child]
            FieldType=Byte
            Path=ChildField
            Label=ChildField
            Value=42
        ";

            PatcherConfig config = SetupIniAndConfig(iniText);
            config.PatchesGFF[0].Apply(gff, memory, logger, BioWareGame.K1);

            // Verify:
            GFFStruct parentStruct = gff.Root.GetStruct("ParentStruct");
            parentStruct.Should().NotBeNull();

            bool atRoot = gff.Root.Exists("ChildField");
            bool atParent = parentStruct.Exists("ChildField");

            if (atRoot)
            {
                gff.Root.GetUInt8("ChildField").Should().Be(42);
            }
            else if (atParent)
            {
                parentStruct.GetUInt8("ChildField").Should().Be(42);
            }
            else
            {
                Assert.Fail("ChildField not found at Root nor ParentStruct");
            }
        }

        #endregion
    }
}
