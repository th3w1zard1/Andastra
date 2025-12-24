using System;
using System.IO;
using System.Linq;
using Andastra.Parsing;
using Andastra.Parsing.Common;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.TLK;
using Andastra.Parsing.Logger;
using Andastra.Parsing.Mods.TLK;
using Andastra.Parsing.Reader;
using Andastra.Parsing.Resource;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{

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
            Directory.CreateDirectory(_tempDir); // Create mod root directory
            Directory.CreateDirectory(_modPath);

            _parser = new IniDataParser();
            _parser.Configuration.AllowDuplicateKeys = true;
            _parser.Configuration.AllowDuplicateSections = true;
            _parser.Configuration.CaseInsensitive = false;

            // Create test TLK files in mod root (where ConfigReader looks for them)
            CreateTestTLKFileInModRoot("test.tlk", new[]
            {
            ("Entry 0", "vo_0"),
            ("Entry 1", "vo_1"),
            ("Entry 2", "vo_2"),
            ("Entry 3", "vo_3"),
            ("Entry 4", "vo_4"),
        });

            // Note: append.tlk is created in _modPath (tslpatchdata) by test setup if needed
            // Python creates it in self.mod_path which is the tslpatchdata folder

            CreateTestTLKFile("tlk_modifications_file.tlk", new[]
            {
            ("Modified 0", "vo_mod_0"),
            ("Modified 1", "vo_mod_1"),
            ("Modified 2", "vo_mod_2"),
            ("Modified 3", "vo_mod_3"),
            ("Modified 4", "vo_mod_4"),
            ("Modified 5", "vo_mod_5"),
            ("Modified 6", "vo_mod_6"),
            ("Modified 7", "vo_mod_7"),
            ("Modified 8", "vo_mod_8"),
            ("Modified 9", "vo_mod_9"),
            ("Modified 10", "vo_mod_10"),
        });

            // Copy test files like Python does (lines 119-120)
            string testFilesDir = Path.Combine("..", "..", "..", "test_files");
            string complexTlkPath = Path.Combine(testFilesDir, "complex.tlk");
            string appendTlkPath = Path.Combine(testFilesDir, "append.tlk");

            if (File.Exists(complexTlkPath))
            {
                File.Copy(complexTlkPath, Path.Combine(_modPath, "complex.tlk"), true);
            }

            if (File.Exists(appendTlkPath))
            {
                File.Copy(appendTlkPath, Path.Combine(_modPath, "append.tlk"), true);
            }
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
            var tlk = new TLK(Language.English);
            foreach ((string text, string sound) in entries)
            {
                tlk.Add(text, sound);
            }

            string path = Path.Combine(_modPath, filename);
            tlk.Save(path);
        }

        private void CreateTestTLKFileInModRoot(string filename, (string text, string sound)[] entries)
        {
            var tlk = new TLK(Language.English);
            foreach ((string text, string sound) in entries)
            {
                tlk.Add(text, sound);
            }

            string path = Path.Combine(_tempDir, filename);
            tlk.Save(path);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TLK_AppendFile_ShouldLoadCorrectly()
        {
            // Python test: test_tlk_appendfile_functionality
            // Arrange
            string iniText = @"
[TLKList]
AppendFile4=tlk_modifications_file.tlk

[tlk_modifications_file.tlk]
0=4
1=5
2=6
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            // Python passes self.mod_path (tslpatchdata) as both mod_path and tslpatchdata_path
            var reader = new ConfigReader(ini, _modPath, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesTLK.Modifiers.Should().HaveCount(3);

            // Load the actual data
            foreach (ModifyTLK mod in result.PatchesTLK.Modifiers)
            {
                mod.Load();
            }

            var modifiersDict = result.PatchesTLK.Modifiers.ToDictionary(
                m => m.TokenId,
                m => new { Text = m.Text, Voiceover = m.Sound?.ToString(), Replace = m.IsReplacement }
            );

            modifiersDict.Should().HaveCount(3);
            modifiersDict[0].Text.Should().Be("Modified 4");
            modifiersDict[0].Voiceover.Should().Be("vo_mod_4");
            modifiersDict[0].Replace.Should().BeFalse();

            modifiersDict[1].Text.Should().Be("Modified 5");
            modifiersDict[1].Voiceover.Should().Be("vo_mod_5");
            modifiersDict[1].Replace.Should().BeFalse();

            modifiersDict[2].Text.Should().Be("Modified 6");
            modifiersDict[2].Voiceover.Should().Be("vo_mod_6");
            modifiersDict[2].Replace.Should().BeFalse();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TLK_ReplaceFile_ShouldMarkAsReplacement()
        {
            // Python test: test_tlk_replacefile_functionality
            // Arrange
            string iniText = @"
[TLKList]
Replacenothingafterreplaceischecked=tlk_modifications_file.tlk

[tlk_modifications_file.tlk]
0=2
1=3
2=4
3=5
4=6
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            // Python passes self.mod_path (tslpatchdata) as both mod_path and tslpatchdata_path
            var reader = new ConfigReader(ini, _modPath, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesTLK.Modifiers.Should().HaveCount(5);

            // Load the actual data
            foreach (ModifyTLK mod in result.PatchesTLK.Modifiers)
            {
                mod.Load();
            }

            var modifiersDict = result.PatchesTLK.Modifiers.ToDictionary(
                m => m.TokenId,
                m => new { Text = m.Text, Voiceover = m.Sound?.ToString() }
            );

            modifiersDict.Should().HaveCount(5);
            modifiersDict[0].Text.Should().Be("Modified 2");
            modifiersDict[0].Voiceover.Should().Be("vo_mod_2");

            modifiersDict[1].Text.Should().Be("Modified 3");
            modifiersDict[1].Voiceover.Should().Be("vo_mod_3");

            modifiersDict[2].Text.Should().Be("Modified 4");
            modifiersDict[2].Voiceover.Should().Be("vo_mod_4");

            modifiersDict[3].Text.Should().Be("Modified 5");
            modifiersDict[3].Voiceover.Should().Be("vo_mod_5");

            modifiersDict[4].Text.Should().Be("Modified 6");
            modifiersDict[4].Voiceover.Should().Be("vo_mod_6");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TLK_StrRef_ShouldLoadWithDefaultFile()
        {
            // Python test: test_tlk_strref_default_functionality
            // Arrange - overwrite append.tlk with modified_tlk_data
            CreateTestTLKFile("append.tlk", new[]
            {
            ("Modified 0", "vo_mod_0"),
            ("Modified 1", "vo_mod_1"),
            ("Modified 2", "vo_mod_2"),
            ("Modified 3", "vo_mod_3"),
            ("Modified 4", "vo_mod_4"),
            ("Modified 5", "vo_mod_5"),
            ("Modified 6", "vo_mod_6"),
            ("Modified 7", "vo_mod_7"),
            ("Modified 8", "vo_mod_8"),
            ("Modified 9", "vo_mod_9"),
            ("Modified 10", "vo_mod_10"),
        });

            string iniText = @"
[TLKList]
StrRef7=0
StrRef8=1
StrRef9=2
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            // Python passes self.mod_path (tslpatchdata) as both mod_path and tslpatchdata_path
            var reader = new ConfigReader(ini, _modPath, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesTLK.Modifiers.Should().HaveCount(3);

            // Load the actual data
            foreach (ModifyTLK mod in result.PatchesTLK.Modifiers)
            {
                mod.Load();
            }

            var modifiersDict = result.PatchesTLK.Modifiers.ToDictionary(
                m => m.TokenId,
                m => new { Text = m.Text, Voiceover = m.Sound?.ToString(), Replace = m.IsReplacement }
            );

            modifiersDict.Should().HaveCount(3);
            modifiersDict[7].Text.Should().Be("Modified 0");
            modifiersDict[7].Voiceover.Should().Be("vo_mod_0");
            modifiersDict[7].Replace.Should().BeFalse();

            modifiersDict[8].Text.Should().Be("Modified 1");
            modifiersDict[8].Voiceover.Should().Be("vo_mod_1");
            modifiersDict[8].Replace.Should().BeFalse();

            modifiersDict[9].Text.Should().Be("Modified 2");
            modifiersDict[9].Voiceover.Should().Be("vo_mod_2");
            modifiersDict[9].Replace.Should().BeFalse();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TLK_ComplexChanges_ShouldLoadAllModifiers()
        {
            // Python test: test_tlk_complex_changes
            // Arrange
            string iniText = @"
        [TLKList]
        ReplaceFile10=complex.tlk
        StrRef0=0
        StrRef1=1
        StrRef2=2
        StrRef3=3
        StrRef4=4
        StrRef5=5
        StrRef6=6
        StrRef7=7
        StrRef8=8
        StrRef9=9
        StrRef10=10
        StrRef11=11
        StrRef12=12
        StrRef13=13

        [complex.tlk]
        123716=0
        123717=1
        123718=2
        123720=3
        123722=4
        123724=5
        123726=6
        123728=7
        123730=8
        124112=9
        125863=10
        50302=11
        ";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            // Python passes self.mod_path (tslpatchdata) as both mod_path and tslpatchdata_path
            var reader = new ConfigReader(ini, _modPath, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert - Python creates a copy of modifiers list before loading (line 240)
            var modifiers2 = result.PatchesTLK.Modifiers.ToList();
            foreach (ModifyTLK modifier in modifiers2)
            {
                modifier.Load();
            }

            result.PatchesTLK.Modifiers.Should().HaveCount(26);

            var modifiersDict2 = result.PatchesTLK.Modifiers.ToDictionary(
                m => m.TokenId,
                m => new { Text = m.Text, Voiceover = m.Sound?.ToString() ?? "" }
            );

            // Python removes is_replacement from dict before comparison (lines 248-249)
            // Then compares the full expected dictionary (lines 252-335)
            modifiersDict2.Should().ContainKey(0);
            modifiersDict2[0].Text.Should().Be("Yavin");
            modifiersDict2[0].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(1);
            modifiersDict2[1].Text.Should().Be("Climate: Artificially Controled\nTerrain: Space Station\nDocking: Orbital Docking\nNative Species: Unknown");
            modifiersDict2[1].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(2);
            modifiersDict2[2].Text.Should().Be("Tatooine");
            modifiersDict2[2].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(3);
            modifiersDict2[3].Text.Should().Be("Climate: Arid\nTerrain: Desert\nDocking: Anchorhead Spaceport\nNative Species: Unknown");
            modifiersDict2[3].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(4);
            modifiersDict2[4].Text.Should().Be("Manaan");
            modifiersDict2[4].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(5);
            modifiersDict2[5].Text.Should().Be("Climate: Temperate\nTerrain: Ocean\nDocking: Ahto City Docking Bay\nNative Species: Selkath");
            modifiersDict2[5].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(6);
            modifiersDict2[6].Text.Should().Be("Kashyyyk");
            modifiersDict2[6].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(7);
            modifiersDict2[7].Text.Should().Be("Climate: Temperate\nTerrain: Forest\nDocking: Czerka Landing Pad\nNative Species: Wookies");
            modifiersDict2[7].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(8);
            modifiersDict2[8].Text.Should().Be("");
            modifiersDict2[8].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(9);
            modifiersDict2[9].Text.Should().Be("");
            modifiersDict2[9].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(10);
            modifiersDict2[10].Text.Should().Be("Sleheyron");
            modifiersDict2[10].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(11);
            modifiersDict2[11].Text.Should().Be("Climate: Unknown\nTerrain: Cityscape\nDocking: Unknown\nNative Species: Unknown");
            modifiersDict2[11].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(12);
            modifiersDict2[12].Text.Should().Be("Coruscant");
            modifiersDict2[12].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(13);
            modifiersDict2[13].Text.Should().Be("Climate: Unknown\nTerrain: Unknown\nDocking: Unknown\nNative Species: Unknown");
            modifiersDict2[13].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(50302);
            modifiersDict2[50302].Text.Should().Be("Opo Chano, Czerka's contracted droid technician, can't give you his droid credentials unless you help relieve his 2,500 credit gambling debt to the Exchange. Without them, you can't take B-4D4.");
            modifiersDict2[50302].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123716);
            modifiersDict2[123716].Text.Should().Be("Climate: None\nTerrain: Asteroid\nDocking: Peragus Mining Station\nNative Species: None");
            modifiersDict2[123716].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123717);
            modifiersDict2[123717].Text.Should().Be("Lehon");
            modifiersDict2[123717].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123718);
            modifiersDict2[123718].Text.Should().Be("Climate: Tropical\nTerrain: Islands\nDocking: Beach Landing\nNative Species: Rakata");
            modifiersDict2[123718].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123720);
            modifiersDict2[123720].Text.Should().Be("Climate: Temperate\nTerrain: Decaying urban zones\nDocking: Refugee Landing Pad\nNative Species: None");
            modifiersDict2[123720].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123722);
            modifiersDict2[123722].Text.Should().Be("Climate: Tropical\nTerrain: Jungle\nDocking: Jungle Clearing\nNative Species: None");
            modifiersDict2[123722].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123724);
            modifiersDict2[123724].Text.Should().Be("Climate: Temperate\nTerrain: Forest\nDocking: Iziz Spaceport\nNative Species: None");
            modifiersDict2[123724].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123726);
            modifiersDict2[123726].Text.Should().Be("Climate: Temperate\nTerrain: Grasslands\nDocking: Khoonda Plains Settlement\nNative Species: None");
            modifiersDict2[123726].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123728);
            modifiersDict2[123728].Text.Should().Be("Climate: Tectonic-Generated Storms\nTerrain: Shattered Planetoid\nDocking: No Docking Facilities Present\nNative Species: None");
            modifiersDict2[123728].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(123730);
            modifiersDict2[123730].Text.Should().Be("Climate: Arid\nTerrain: Volcanic\nDocking: Dreshae Settlement\nNative Species: Unknown");
            modifiersDict2[123730].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(124112);
            modifiersDict2[124112].Text.Should().Be("Climate: Artificially Maintained \nTerrain: Droid Cityscape\nDocking: Landing Arm\nNative Species: Unknown");
            modifiersDict2[124112].Voiceover.Should().Be("");

            modifiersDict2.Should().ContainKey(125863);
            modifiersDict2[125863].Text.Should().Be("Climate: Artificially Maintained\nTerrain: Space Station\nDocking: Landing Zone\nNative Species: None");
            modifiersDict2[125863].Voiceover.Should().Be("");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TLK_DirectTextAndSound_ShouldLoadWithoutFile()
        {
            // Arrange
            string iniText = @"
[TLKList]
0\text=Direct text entry
1\text=Another entry
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            // Python passes self.mod_path (tslpatchdata) as both mod_path and tslpatchdata_path
            var reader = new ConfigReader(ini, _modPath, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesTLK.Modifiers.Should().HaveCount(2);

            ModifyTLK mod0 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0);
            mod0.Text.Should().Be("Direct text entry");
            mod0.TlkFilePath.Should().BeNull();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TLK_SoundDirective_ShouldSetSound()
        {
            // Arrange
            string iniText = @"
[TLKList]
0\text=Test text
0\sound=test_sound
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            // Python passes self.mod_path (tslpatchdata) as both mod_path and tslpatchdata_path
            var reader = new ConfigReader(ini, _modPath, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            // Python creates separate ModifyTLK objects for text and sound (lines 415-421)
            result.PatchesTLK.Modifiers.Should().HaveCount(2);

            ModifyTLK textMod = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0 && m.Text == "Test text");
            textMod.Should().NotBeNull();
            textMod.IsReplacement.Should().BeTrue();

            ModifyTLK soundMod = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0 && m.Sound?.ToString() == "test_sound");
            soundMod.Should().NotBeNull();
            soundMod.IsReplacement.Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void TLK_MultipleFiles_ShouldLoadFromCorrectFiles()
        {
            // Arrange - Create test.tlk and append.tlk with the expected content for this test
            CreateTestTLKFile("test.tlk", new[]
            {
            ("Entry 0", "vo_0"),
            ("Entry 1", "vo_1"),
        });

            CreateTestTLKFile("append.tlk", new[]
            {
            ("Append 0", "append_0"),
            ("Append 1", "append_1"),
        });

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
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            // Python passes self.mod_path (tslpatchdata) as both mod_path and tslpatchdata_path
            var reader = new ConfigReader(ini, _modPath, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesTLK.Modifiers.Should().HaveCount(4);

            ModifyTLK mod0 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 0);
            ModifyTLK mod2 = result.PatchesTLK.Modifiers.First(m => m.TokenId == 2);

            mod0.TlkFilePath.Should().Contain("test.tlk");
            mod2.TlkFilePath.Should().Contain("append.tlk");

            // Load the actual data
            foreach (ModifyTLK mod in result.PatchesTLK.Modifiers)
            {
                mod.Load();
            }

            mod0.Text.Should().Be("Entry 0");
            mod2.Text.Should().Be("Append 0");
        }

    }
}

