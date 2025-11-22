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
/// Integration tests for 2DA CopyRow operations.
/// </summary>
public class TwoDACopyRowTests : IntegrationTestBase
{
    [Fact]
    public void CopyExistingRowIndex_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
Col2=X
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        var bytes = config.Patches2DA.First(p => p.SaveAs == "test.2da").PatchResource(twoda.ToBytes(), memory, new PatchLogger(), Game.K1);
        var patchedTwoda = TwoDA.FromBytes((byte[])bytes);

        patchedTwoda.GetHeight().Should().Be(3);
        patchedTwoda.GetColumn("Col1").Should().Equal("a", "c", "a");
        patchedTwoda.GetColumn("Col2").Should().Equal("b", "d", "X");
    }

    [Fact]
    public void CopyExistingRowLabel_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowLabel=1
Col2=X
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        twoda.GetColumn("Col1").Should().Equal("a", "c", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d", "X");
    }

    [Fact]
    public void CopyExclusiveNotExists_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
ExclusiveColumn=Col1
Col1=c
Col2=d
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "a", "b" }) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(2);
        twoda.GetLabel(1).Should().Be("1");
        twoda.GetColumn("Col1").Should().Equal("a", "c");
        twoda.GetColumn("Col2").Should().Equal("b", "d");
    }

    [Fact]
    public void CopyExclusiveExists_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
ExclusiveColumn=Col1
Col1=a
Col2=X
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "a", "b" }) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(1);
        twoda.GetLabel(0).Should().Be("0");
        twoda.GetColumn("Col1").Should().Equal("a");
        twoda.GetColumn("Col2").Should().Equal("X");
    }

    [Fact]
    public void CopyExclusiveNone_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0
CopyRow1=copy_row_1

[copy_row_0]
RowIndex=0
Col1=c
Col2=d

[copy_row_1]
RowIndex=0
RowLabel=r2
Col1=e
Col2=f
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[] { ("0", new[] { "a", "b" }) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        twoda.GetLabel(1).Should().Be("1");
        twoda.GetLabel(2).Should().Be("r2");
        twoda.GetColumn("Col1").Should().Equal("a", "c", "e");
        twoda.GetColumn("Col2").Should().Equal("b", "d", "f");
    }

    [Fact]
    public void CopySetNewRowLabel_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
NewRowLabel=r2
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2", "Col3" },
            new[]
            {
                ("0", new[] { "a", "b" }),
                ("1", new[] { "c", "d" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetLabel(2).Should().Be("r2");
        twoda.GetColumn("Col1").Should().Equal("a", "c", "a");
        twoda.GetColumn("Col2").Should().Equal("b", "d", "b");
    }

    [Fact]
    public void CopyAssignHigh_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
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
    public void CopyAssignTLKMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
Col1=StrRef5
Col2=StrRef6
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "1", "2" }),
                ("1", new[] { "3", "4" })
            }
        );

        var memory = new PatcherMemory();
        memory.MemoryStr[5] = 100;
        memory.MemoryStr[6] = 200;

        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        twoda.GetCellString(2, "Col1").Should().Be("100");
        twoda.GetCellString(2, "Col2").Should().Be("200");
    }

    [Fact]
    public void CopyAssign2DAMemory_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
Col1=2DAMEMORY5
Col2=2DAMEMORY6
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1", "Col2" },
            new[]
            {
                ("0", new[] { "1", "2" }),
                ("1", new[] { "3", "4" })
            }
        );

        var memory = new PatcherMemory();
        memory.Memory2DA[5] = "AAA";
        memory.Memory2DA[6] = "BBB";

        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        twoda.GetCellString(2, "Col1").Should().Be("AAA");
        twoda.GetCellString(2, "Col2").Should().Be("BBB");
    }

    [Fact]
    public void Copy2DAMemoryRowIndex_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
CopyRow0=copy_row_0

[copy_row_0]
RowIndex=0
2DAMEMORY5=RowIndex
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[]
            {
                ("0", new[] { "a" }),
                ("1", new[] { "b" })
            }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(3);
        memory.Memory2DA[5].Should().Be("2");
    }

    [Fact]
    public void Add2DAMemoryRowIndexExclusive_AppliesPatchCorrectly()
    {
        string iniText = @"
[2DAList]
Table0=test.2da

[test.2da]
AddRow0=add_row_0
AddRow1=add_row_1

[add_row_0]
ExclusiveColumn=Col1
RowLabel=1
Col1=X
2DAMEMORY5=RowIndex

[add_row_1]
RowLabel=2
Col1=Y
2DAMEMORY6=RowIndex
";
        var config = SetupIniAndConfig(iniText);
        var twoda = CreateTest2DA(
            new[] { "Col1" },
            new[] { ("0", new[] { "X" }) }
        );

        var memory = new PatcherMemory();
        config.Patches2DA.First(p => p.SaveAs == "test.2da").Apply(twoda, memory, new PatchLogger(), Game.K1);

        twoda.GetHeight().Should().Be(2);
        twoda.GetColumn("Col1").Should().Equal("X", "Y");
        memory.Memory2DA[5].Should().Be("0");
        memory.Memory2DA[6].Should().Be("1");
    }
}

