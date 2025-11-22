using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using TSLPatcher.Core.Config;
using TSLPatcher.Core.Formats.SSF;
using TSLPatcher.Core.Memory;
using TSLPatcher.Core.Mods.SSF;
using TSLPatcher.Core.Reader;
using Xunit;

namespace TSLPatcher.Tests.Reader;

/// <summary>
/// Tests for ConfigReader SSF parsing functionality.
/// Ported from test_reader.py - SSF section tests.
/// </summary>
public class ConfigReaderSSFTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _modPath;
    private readonly IniDataParser _parser;

    public ConfigReaderSSFTests()
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

    [Fact]
    public void SSF_Replace_ShouldLoadCorrectly()
    {
        // Arrange
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
ReplaceFile=1
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesSSF.Should().Contain(p => p.SaveAs == "test.ssf");
        result.PatchesSSF.First(p => p.SaveAs == "test.ssf").ReplaceFile.Should().BeTrue();
    }

    [Fact]
    public void SSF_Set_ShouldLoadDirectAssignment()
    {
        // Arrange
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Set0=12345
Set1=67890
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modifiers = result.PatchesSSF.First(p => p.SaveAs == "test.ssf").Modifiers;
        modifiers.Should().HaveCount(2);

        var set0 = modifiers[0];
        set0.Sound.Should().Be(SSFSound.BATTLE_CRY_1);
        ((NoTokenUsage)set0.Stringref).Value(null).Should().Be("12345");

        var set1 = modifiers[1];
        set1.Sound.Should().Be(SSFSound.BATTLE_CRY_2);
        ((NoTokenUsage)set1.Stringref).Value(null).Should().Be("67890");
    }

    [Fact]
    public void SSF_Set_ShouldLoadTLKMemoryReference()
    {
        // Arrange
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Set0=StrRef5
Set1=StrRef10
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modifiers = result.PatchesSSF.First(p => p.SaveAs == "test.ssf").Modifiers;
        modifiers.Should().HaveCount(2);

        var set0 = modifiers[0];
        set0.Sound.Should().Be(SSFSound.BATTLE_CRY_1);
        set0.Stringref.Should().NotBeNull();
        ((TokenUsageTLK)set0.Stringref!).TokenId.Should().Be(5);

        var set1 = modifiers[1];
        set1.Sound.Should().Be(SSFSound.BATTLE_CRY_2);
        ((TokenUsageTLK)set1.Stringref!).TokenId.Should().Be(10);
    }

    [Fact]
    public void SSF_Set_ShouldLoad2DAMemoryReference()
    {
        // Arrange
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Set0=2DAMEMORY3
Set2=2DAMEMORY7
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        var modifiers = result.PatchesSSF.First(p => p.SaveAs == "test.ssf").Modifiers;
        modifiers.Should().HaveCount(2);

        var set0 = modifiers[0];
        set0.Sound.Should().Be(SSFSound.BATTLE_CRY_1);
        set0.Stringref.Should().NotBeNull();
        ((TokenUsage2DA)set0.Stringref!).TokenId.Should().Be(3);

        var set2 = modifiers[1];
        set2.Sound.Should().Be(SSFSound.BATTLE_CRY_3);
        ((TokenUsage2DA)set2.Stringref!).TokenId.Should().Be(7);
    }

    [Fact]
    public void SSF_MultipleFiles_ShouldLoadSeparately()
    {
        // Arrange
        string iniText = @"
[SSFList]
File0=test1.ssf
File1=test2.ssf

[test1.ssf]
Set0=100

[test2.ssf]
Set5=200
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesSSF.Should().HaveCount(2);
        result.PatchesSSF.Should().Contain(p => p.SaveAs == "test1.ssf");
        result.PatchesSSF.Should().Contain(p => p.SaveAs == "test2.ssf");

        result.PatchesSSF.First(p => p.SaveAs == "test1.ssf").Modifiers.Should().HaveCount(1);
        result.PatchesSSF.First(p => p.SaveAs == "test2.ssf").Modifiers.Should().HaveCount(1);

        result.PatchesSSF.First(p => p.SaveAs == "test1.ssf").Modifiers[0].Sound.Should().Be(SSFSound.BATTLE_CRY_1);
        result.PatchesSSF.First(p => p.SaveAs == "test2.ssf").Modifiers[0].Sound.Should().Be(SSFSound.SELECT_1);
    }

    [Fact]
    public void SSF_Destination_ShouldParseCorrectly()
    {
        // Arrange
        string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Destination=modules\test.mod
Set0=100
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesSSF.First(p => p.SaveAs == "test.ssf").Destination.Should().Be("modules\\test.mod");
    }
}

