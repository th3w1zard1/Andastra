using System.Collections.Generic;
using FluentAssertions;
using TSLPatcher.Core.Common;
using TSLPatcher.Core.Config;
using TSLPatcher.Core.Formats.TwoDA;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Logger;
using Xunit;

namespace TSLPatcher.Tests.Integration;

/// <summary>
/// Integration tests for 2DA memory token operations.
/// </summary>
public class TwoDAMemoryTests : IntegrationTestBase
{
    [Fact]
    public void ChangeLabelIndex_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0

[change_row_0]
LabelIndex=d
Col2=X
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "label", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("label").Should().Equal("a", "d");
        twoda.GetColumn("Col2").Should().Equal("b", "X");
        twoda.GetColumn("Col3").Should().Equal("c", "f");
    }

    [Fact]
    public void ChangeAssignTLKMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0
ChangeRow1=change_row_1

[change_row_0]
RowIndex=0
Col1=StrRef0

[change_row_1]
RowIndex=1
Col1=StrRef1
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            }
        );

        var memory = new PatcherMemory();
        memory.MemoryStr[0] = 0;
        memory.MemoryStr[1] = 1;

        var bytes = config.Patches2DA.First(p => p.SaveAs == "test.2da").PatchResource(twoda.ToBytes(), memory, new PatchLogger(), Game.K1);
        var patchedTwoda = TwoDA.FromBytes((byte[])bytes);

        patchedTwoda.GetColumn("Col1").Should().Equal("0", "1");
        patchedTwoda.GetColumn("Col2").Should().Equal("b", "e");
        patchedTwoda.GetColumn("Col3").Should().Equal("c", "f");
    }

    [Fact]
    public void ChangeAssign2DAMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0
ChangeRow1=change_row_1

[change_row_0]
RowIndex=0
Col1=2DAMEMORY0

[change_row_1]
RowIndex=1
Col1=2DAMEMORY1
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            }
        );

        var memory = new PatcherMemory();
        memory.Memory2DA[0] = "mem0";
        memory.Memory2DA[1] = "mem1";

        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("mem0", "mem1");
        twoda.GetColumn("Col2").Should().Equal("b", "e");
        twoda.GetColumn("Col3").Should().Equal("c", "f");
    }

    [Fact]
    public void ChangeAssignHigh_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0
ChangeRow1=change_row_1

[change_row_0]
RowIndex=0
Col1=high()

[change_row_1]
RowIndex=0
Col2=high()
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { " ", "3", "5" }),
                ("1", new[] { "2", "4", "6" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("3", "2");
        twoda.GetColumn("Col2").Should().Equal("5", "4");
        twoda.GetColumn("Col3").Should().Equal("5", "6");
    }

    [Fact]
    public void Set2DAMemoryRowIndex_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0

[change_row_0]
RowIndex=1
2DAMEMORY5=RowIndex
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "d");
        twoda.GetColumn("Col2").Should().Equal("b", "e");
        twoda.GetColumn("Col3").Should().Equal("c", "f");
        memory.Memory2DA[5].Should().Be("1");
    }

    [Fact]
    public void Set2DAMemoryRowLabel_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0

[change_row_0]
RowIndex=1
2DAMEMORY5=RowLabel
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("r1", new[] { "d", "e", "f" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("Col1").Should().Equal("a", "d");
        twoda.GetColumn("Col2").Should().Equal("b", "e");
        twoda.GetColumn("Col3").Should().Equal("c", "f");
        memory.Memory2DA[5].Should().Be("r1");
    }

    [Fact]
    public void Set2DAMemoryColumnLabel_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
ChangeRow0=change_row_0

[change_row_0]
RowIndex=1
2DAMEMORY5=label
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "label", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b", "c" }),
                ("1", new[] { "d", "e", "f" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetColumn("label").Should().Equal("a", "d");
        twoda.GetColumn("Col2").Should().Equal("b", "e");
        twoda.GetColumn("Col3").Should().Equal("c", "f");
        memory.Memory2DA[5].Should().Be("d");
    }

    [Fact]
    public void AddRowLabelUseMaxRowLabel_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0
AddRow1=add_row_1

[add_row_0]

[add_row_1]
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[] { ("0", new string[0]) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        twoda.GetLabel(0).Should().Be("0");
        twoda.GetLabel(1).Should().Be("1");
        twoda.GetLabel(2).Should().Be("2");
    }

    [Fact]
    public void AddRowLabelUseConstant_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
RowLabel=r1
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(new[] { "Col1" }, Array.Empty<(string label, string[] values)>());

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(1);
        twoda.GetLabel(0).Should().Be("r1");
    }

    [Fact]
    public void AddRowLabelExisting_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
ExclusiveColumn=Col1
Col1=123
Col2=ABC
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "123", "456" }) }
        );

        var memory = new PatcherMemory();
        var bytes = config.Patches2DA.First(p => p.SaveAs == "test.2da").PatchResource(twoda.ToBytes(), memory, new PatchLogger(), Game.K1);
        var patchedTwoda = TwoDA.FromBytes((byte[])bytes);

        patchedTwoda.GetHeight().Should().Be(1);
        patchedTwoda.GetLabel(0).Should().Be("0");
    }

    [Fact]
    public void AddExclusiveNotExists_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
ExclusiveColumn=Col1
Col1=999
Col2=ABC
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "123", "456" }) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(2);
        twoda.GetCellString(1, "Col1").Should().Be("999");
        twoda.GetCellString(1, "Col2").Should().Be("ABC");
    }

    [Fact]
    public void AddExclusiveExists_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
ExclusiveColumn=Col1
Col1=123
Col2=ABC
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "123", "456" }) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(1);
        twoda.GetCellString(0, "Col1").Should().Be("123");
        twoda.GetCellString(0, "Col2").Should().Be("ABC");
    }

    [Fact]
    public void AddExclusiveNone_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
Col1=999
Col2=ABC
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "123", "456" }),
                ("1", new[] { "789", "012" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        twoda.GetCellString(2, "Col1").Should().Be("999");
        twoda.GetCellString(2, "Col2").Should().Be("ABC");
    }

    [Fact]
    public void AddAssignHigh_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
Col1=high()
Col2=high()
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "1", "3" }),
                ("1", new[] { "2", "4" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        twoda.GetCellString(2, "Col1").Should().Be("3");
        twoda.GetCellString(2, "Col2").Should().Be("5");
    }

    [Fact]
    public void AddAssignTLKMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
Col1=StrRef5
Col2=StrRef6
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "1", "2" }) }
        );

        var memory = new PatcherMemory();
        memory.MemoryStr[5] = 100;
        memory.MemoryStr[6] = 200;

        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(2);
        twoda.GetCellString(1, "Col1").Should().Be("100");
        twoda.GetCellString(1, "Col2").Should().Be("200");
    }

    [Fact]
    public void AddAssign2DAMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
Col1=2DAMEMORY5
Col2=2DAMEMORY6
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "1", "2" }) }
        );

        var memory = new PatcherMemory();
        memory.Memory2DA[5] = "AAA";
        memory.Memory2DA[6] = "BBB";

        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(2);
        twoda.GetCellString(1, "Col1").Should().Be("AAA");
        twoda.GetCellString(1, "Col2").Should().Be("BBB");
    }

    [Fact]
    public void Add2DAMemoryRowIndex_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0

[add_row_0]
Col1=test
2DAMEMORY5=RowIndex
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[] { ("0", new[] { "existing" }) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(2);
        twoda.GetCellString(1, "Col1").Should().Be("test");
        memory.Memory2DA[5].Should().Be("1");
    }
}

