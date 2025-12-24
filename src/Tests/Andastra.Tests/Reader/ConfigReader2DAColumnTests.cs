using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Config;
using Andastra.Parsing.Mods.TwoDA;
using Andastra.Parsing.Reader;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{

    /// <summary>
    /// ConfigReader tests for 2DA AddColumn operations.
    /// </summary>
    public class ConfigReader2DAColumnTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _modPath;
        private readonly IniDataParser _parser;

        public ConfigReader2DAColumnTests()
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
        public void AddColumn_Basic_ShouldParseCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0
AddColumn1=add_column_1

[add_column_0]
ColumnLabel=label
DefaultValue=****
2DAMEMORY2=I2

[add_column_1]
ColumnLabel=someint
DefaultValue=0
2DAMEMORY2=I2
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            result.Patches2DA.Should().Contain(p => p.SaveAs == "test.2da");
            List<Modify2DA> modifiers = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers;
            modifiers.Should().HaveCount(2);

            var mod0 = modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();
            mod0.Header.Should().Be("label");
            mod0.Default.Should().Be("");

            var mod1 = modifiers[1] as AddColumn2DA;
            mod1.Should().NotBeNull();
            mod1.Header.Should().Be("someint");
            mod1.Default.Should().Be("0");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_IndexInsert_ShouldParseCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=NewColumn
DefaultValue=****
I0=abc
I1=2DAMEMORY4
I2=StrRef5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();

            mod0.IndexInsert.Should().ContainKey(0);
            var value0 = mod0.IndexInsert[0] as RowValueConstant;
            value0.Should().NotBeNull();
            value0.Value(null, null, null).Should().Be("abc");

            mod0.IndexInsert.Should().ContainKey(1);
            var value1 = mod0.IndexInsert[1] as RowValue2DAMemory;
            value1.Should().NotBeNull();
            value1.TokenId.Should().Be(4);

            mod0.IndexInsert.Should().ContainKey(2);
            var value2 = mod0.IndexInsert[2] as RowValueTLKMemory;
            value2.Should().NotBeNull();
            value2.TokenId.Should().Be(5);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_LabelInsert_ShouldParseCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=NewColumn
DefaultValue=****
L0=abc
L1=2DAMEMORY4
L2=StrRef5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();

            mod0.LabelInsert.Should().ContainKey("0");
            var value0 = mod0.LabelInsert["0"] as RowValueConstant;
            value0.Should().NotBeNull();
            value0.Value(null, null, null).Should().Be("abc");

            mod0.LabelInsert.Should().ContainKey("1");
            var value1 = mod0.LabelInsert["1"] as RowValue2DAMemory;
            value1.Should().NotBeNull();
            value1.TokenId.Should().Be(4);

            mod0.LabelInsert.Should().ContainKey("2");
            var value2 = mod0.LabelInsert["2"] as RowValueTLKMemory;
            value2.Should().NotBeNull();
            value2.TokenId.Should().Be(5);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_2DAMemory_ShouldParseCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=NewColumn
DefaultValue=****
2DAMEMORY0=I0
2DAMEMORY1=L0
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();

            mod0.Store2DA.Should().HaveCount(2);
            mod0.Store2DA[0].Should().Be("I0");
            mod0.Store2DA[1].Should().Be("L0");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_WithMultipleInserts_ShouldParseAllCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=TestColumn
DefaultValue=def
I0=val0
I1=val1
L5=val5
L10=val10
2DAMEMORY3=I0
2DAMEMORY4=L5
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();

            mod0.Header.Should().Be("TestColumn");
            mod0.Default.Should().Be("def");
            mod0.IndexInsert.Should().HaveCount(2);
            mod0.LabelInsert.Should().HaveCount(2);
            mod0.Store2DA.Should().HaveCount(2);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_EmptyDefault_ShouldParseAsEmpty()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=EmptyCol
DefaultValue=
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();
            mod0.Default.Should().Be("");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_StarsDefault_ShouldParseAsStars()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=StarsCol
DefaultValue=****
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();
            mod0.Default.Should().Be("");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_MixedIndexAndLabel_ShouldNotConflict()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=MixedCol
DefaultValue=X
I5=indexValue
L5=labelValue
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();

            mod0.IndexInsert.Should().ContainKey(5);
            mod0.LabelInsert.Should().ContainKey("5");

            var indexVal = mod0.IndexInsert[5] as RowValueConstant;
            indexVal.Should().NotBeNull();
            indexVal.String.Should().Be("indexValue");

            var labelVal = mod0.LabelInsert["5"] as RowValueConstant;
            labelVal.Should().NotBeNull();
            labelVal.String.Should().Be("labelValue");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void AddColumn_TokensWithDifferentSources_ShouldParseCorrectly()
        {
            string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddColumn0=add_column_0

[add_column_0]
ColumnLabel=TokenCol
DefaultValue=****
2DAMEMORY0=I5
2DAMEMORY1=L10
2DAMEMORY2=Col1
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            PatcherConfig result = reader.Load(config);

            var mod0 = result.Patches2DA.First(p => p.SaveAs == "test.2da").Modifiers[0] as AddColumn2DA;
            mod0.Should().NotBeNull();

            mod0.Store2DA.Should().HaveCount(3);
            mod0.Store2DA[0].Should().Be("I5");
            mod0.Store2DA[1].Should().Be("L10");
            mod0.Store2DA[2].Should().Be("Col1");
        }
    }
}

