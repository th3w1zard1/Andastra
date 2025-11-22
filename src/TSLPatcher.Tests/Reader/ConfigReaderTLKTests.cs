using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using TSLPatcher.Core.Config;
using TSLPatcher.Core.Formats.TLK;
using TSLPatcher.Core.Logger;
using TSLPatcher.Core.Mods.TLK;
using TSLPatcher.Core.Reader;
using TSLPatcher.Core.Resources;
using Xunit;

namespace TSLPatcher.Tests.Reader;

/// <summary>
/// Tests for ConfigReader TLK parsing functionality.
/// Ported from test_reader.py - TLK section tests.
/// </summary>
public class ConfigReaderTLKTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _modPath;
    private readonly IniDataParser _parser;

    public ConfigReaderTLKTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _modPath = Path.Combine(_tempDir, "tslpatchdata");
        Directory.CreateDirectory(_modPath);

        _parser = new IniDataParser();
        _parser.Configuration.AllowDuplicateKeys = true;
        _parser.Configuration.AllowDuplicateSections = true;
        _parser.Configuration.CaseInsensitive = false;

        // Create test TLK files
        CreateTestTLKFile("test.tlk", new[]
        {
            ("Entry 0", "vo_0"),
            ("Entry 1", "vo_1"),
            ("Entry 2", "vo_2"),
            ("Entry 3", "vo_3"),
            ("Entry 4", "vo_4"),
        });

        CreateTestTLKFile("append.tlk", new[]
        {
            ("Append 0", "append_0"),
            ("Append 1", "append_1"),
            ("Append 2", "append_2"),
        });

        CreateTestTLKFile("modifications.tlk", new[]
        {
            ("Modified 0", "mod_0"),
            ("Modified 1", "mod_1"),
            ("Modified 2", "mod_2"),
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private void CreateTestTLKFile(string filename, (string text, string sound)[] entries)
    {
        var tlk = new TLK(Core.Common.Language.English);
        foreach (var (text, sound) in entries)
        {
            tlk.Add(text, sound);
        }

        string path = Path.Combine(_modPath, filename);
        tlk.Save(path);
    }

    [Fact]
    public void TLK_AppendFile_ShouldLoadCorrectly()
    {
        // Arrange
        string iniText = @"
[TLKList]
AppendFile0=modifications.tlk

[modifications.tlk]
0=0
1=1
2=2
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesTLK.Modifiers.Should().HaveCount(3);

        var mod0 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0);
        mod0.IsReplacement.Should().BeFalse();
        mod0.TlkFilePath.Should().Contain("modifications.tlk");
        mod0.ModIndex.Should().Be(0);

        // Load the actual data
        foreach (var mod in result.PatchesTLK.Modifiers)
        {
            mod.Load();
        }

        mod0.Text.Should().Be("Modified 0");
        mod0.Sound?.ToString().Should().Be("mod_0");
    }

    [Fact]
    public void TLK_ReplaceFile_ShouldMarkAsReplacement()
    {
        // Arrange
        string iniText = @"
[TLKList]
ReplaceFile0=modifications.tlk

[modifications.tlk]
0=0
1=1
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesTLK.Modifiers.Should().HaveCount(2);
        result.PatchesTLK.Modifiers.All(m => m.IsReplacement).Should().BeTrue();
    }

    [Fact]
    public void TLK_StrRef_ShouldLoadWithDefaultFile()
    {
        // Arrange
        string iniText = @"
[TLKList]
StrRef0=0
StrRef1=1
StrRef2=2
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesTLK.Modifiers.Should().HaveCount(3);

        var mod0 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0);
        mod0.TlkFilePath.Should().Contain("append.tlk");
        mod0.ModIndex.Should().Be(0);
        mod0.IsReplacement.Should().BeFalse();

        // Load the actual data
        foreach (var mod in result.PatchesTLK.Modifiers)
        {
            mod.Load();
        }

        mod0.Text.Should().Be("Append 0");
        mod0.Sound?.ToString().Should().Be("append_0");
    }

    [Fact]
    public void TLK_DirectTextAndSound_ShouldLoadWithoutFile()
    {
        // Arrange
        string iniText = @"
[TLKList]
StrRef0=Direct text entry
StrRef1=Another entry
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesTLK.Modifiers.Should().HaveCount(2);

        var mod0 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0);
        mod0.Text.Should().Be("Direct text entry");
        mod0.TlkFilePath.Should().BeNull();
    }

    [Fact]
    public void TLK_SoundDirective_ShouldSetSound()
    {
        // Arrange
        string iniText = @"
[TLKList]
StrRef0=Test text
StrRef0Sound=test_sound
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesTLK.Modifiers.Should().HaveCount(1);

        var mod0 = result.PatchesTLK.Modifiers.First();
        mod0.Text.Should().Be("Test text");
        mod0.Sound?.ToString().Should().Be("test_sound");
    }

    [Fact]
    public void TLK_MultipleFiles_ShouldLoadFromCorrectFiles()
    {
        // Arrange
        string iniText = @"
[TLKList]
AppendFile0=test.tlk
AppendFile1=append.tlk

[test.tlk]
0=0
1=1

[append.tlk]
2=0
3=1
";
        var ini = _parser.Parse(iniText);
        var config = new PatcherConfig();
        var reader = new ConfigReader(ini, _tempDir, null, _modPath);

        // Act
        var result = reader.Load(config);

        // Assert
        result.PatchesTLK.Modifiers.Should().HaveCount(4);

        var mod0 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0);
        var mod2 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 2);

        mod0.TlkFilePath.Should().Contain("test.tlk");
        mod2.TlkFilePath.Should().Contain("append.tlk");

        // Load the actual data
        foreach (var mod in result.PatchesTLK.Modifiers)
        {
            mod.Load();
        }

        mod0.Text.Should().Be("Entry 0");
        mod2.Text.Should().Be("Append 0");
    }
}

