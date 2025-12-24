using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Andastra.Parsing.Config;
using Andastra.Parsing.Formats.SSF;
using Andastra.Parsing.Memory;
using Andastra.Parsing.Mods.SSF;
using Andastra.Parsing.Reader;
using FluentAssertions;
using IniParser.Model;
using IniParser.Parser;
using Xunit;

namespace Andastra.Parsing.Tests.Reader
{

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

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SSF_Replace_ShouldLoadCorrectly()
        {
            // Python test: test_ssf_replace
            // Arrange
            string iniText = @"
[SSFList]
File0=test1.ssf
Replace0=test2.ssf

[test1.ssf]
[test2.ssf]
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            result.PatchesSSF[0].ReplaceFile.Should().BeFalse();
            result.PatchesSSF[1].ReplaceFile.Should().BeTrue();
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SSF_Set_ShouldLoadDirectAssignment()
        {
            // Python test: test_ssf_stored_constant
            // Arrange
            string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 1=123
Battlecry 2=456
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<ModifySSF> modifiers = result.PatchesSSF[0].Modifiers;

            ModifySSF mod_0 = modifiers[0];
            mod_0.Stringref.Should().BeOfType<NoTokenUsage>();
            ((NoTokenUsage)mod_0.Stringref).Stored.Should().Be("123");

            ModifySSF mod_1 = modifiers[1];
            mod_1.Stringref.Should().BeOfType<NoTokenUsage>();
            ((NoTokenUsage)mod_1.Stringref).Stored.Should().Be("456");
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SSF_Set_ShouldLoadTLKMemoryReference()
        {
            // Python test: test_ssf_stored_tlk
            // Arrange
            string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 1=StrRef5
Battlecry 2=StrRef6
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<ModifySSF> modifiers = result.PatchesSSF[0].Modifiers;

            ModifySSF mod_0 = modifiers[0];
            mod_0.Stringref.Should().BeOfType<TokenUsageTLK>();
            ((TokenUsageTLK)mod_0.Stringref).TokenId.Should().Be(5);

            ModifySSF mod_1 = modifiers[1];
            mod_1.Stringref.Should().BeOfType<TokenUsageTLK>();
            ((TokenUsageTLK)mod_1.Stringref).TokenId.Should().Be(6);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SSF_Set_ShouldLoad2DAMemoryReference()
        {
            // Python test: test_ssf_stored_2da
            // Arrange
            string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 1=2DAMEMORY5
Battlecry 2=2DAMEMORY6
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<ModifySSF> modifiers = result.PatchesSSF[0].Modifiers;

            ModifySSF mod_0 = modifiers[0];
            mod_0.Stringref.Should().BeOfType<TokenUsage2DA>();
            ((TokenUsage2DA)mod_0.Stringref).TokenId.Should().Be(5);

            ModifySSF mod_1 = modifiers[1];
            mod_1.Stringref.Should().BeOfType<TokenUsage2DA>();
            ((TokenUsage2DA)mod_1.Stringref).TokenId.Should().Be(6);
        }

        [Fact(Timeout = 120000)] // 2 minutes timeout
        public void SSF_Set_ShouldMapAllSounds()
        {
            // Python test: test_ssf_set
            // Arrange
            string iniText = @"
[SSFList]
File0=test.ssf

[test.ssf]
Battlecry 1=1
Battlecry 2=2
Battlecry 3=3
Battlecry 4=4
Battlecry 5=5
Battlecry 6=6
Selected 1=7
Selected 2=8
Selected 3=9
Attack 1=10
Attack 2=11
Attack 3=12
Pain 1=13
Pain 2=14
Low health=15
Death=16
Critical hit=17
Target immune=18
Place mine=19
Disarm mine=20
Stealth on=21
Search=22
Pick lock start=23
Pick lock fail=24
Pick lock done=25
Leave party=26
Rejoin party=27
Poisoned=28
";
            IniData ini = _parser.Parse(iniText);
            var config = new PatcherConfig();
            var reader = new ConfigReader(ini, _tempDir, null, _modPath);

            // Act
            PatcherConfig result = reader.Load(config);

            // Assert
            List<ModifySSF> modifiers = result.PatchesSSF[0].Modifiers;

            ModifySSF mod_battlecry1 = modifiers[0];
            mod_battlecry1.Sound.Should().Be(SSFSound.BATTLE_CRY_1);

            ModifySSF mod_battlecry2 = modifiers[1];
            mod_battlecry2.Sound.Should().Be(SSFSound.BATTLE_CRY_2);

            ModifySSF mod_battlecry3 = modifiers[2];
            mod_battlecry3.Sound.Should().Be(SSFSound.BATTLE_CRY_3);

            ModifySSF mod_battlecry4 = modifiers[3];
            mod_battlecry4.Sound.Should().Be(SSFSound.BATTLE_CRY_4);

            ModifySSF mod_battlecry5 = modifiers[4];
            mod_battlecry5.Sound.Should().Be(SSFSound.BATTLE_CRY_5);

            ModifySSF mod_battlecry6 = modifiers[5];
            mod_battlecry6.Sound.Should().Be(SSFSound.BATTLE_CRY_6);

            ModifySSF mod_selected1 = modifiers[6];
            mod_selected1.Sound.Should().Be(SSFSound.SELECT_1);

            ModifySSF mod_selected2 = modifiers[7];
            mod_selected2.Sound.Should().Be(SSFSound.SELECT_2);

            ModifySSF mod_selected3 = modifiers[8];
            mod_selected3.Sound.Should().Be(SSFSound.SELECT_3);

            ModifySSF mod_attack1 = modifiers[9];
            mod_attack1.Sound.Should().Be(SSFSound.ATTACK_GRUNT_1);

            ModifySSF mod_attack2 = modifiers[10];
            mod_attack2.Sound.Should().Be(SSFSound.ATTACK_GRUNT_2);

            ModifySSF mod_attack3 = modifiers[11];
            mod_attack3.Sound.Should().Be(SSFSound.ATTACK_GRUNT_3);

            ModifySSF mod_pain1 = modifiers[12];
            mod_pain1.Sound.Should().Be(SSFSound.PAIN_GRUNT_1);

            ModifySSF mod_pain2 = modifiers[13];
            mod_pain2.Sound.Should().Be(SSFSound.PAIN_GRUNT_2);

            ModifySSF mod_lowhealth = modifiers[14];
            mod_lowhealth.Sound.Should().Be(SSFSound.LOW_HEALTH);

            ModifySSF mod_death = modifiers[15];
            mod_death.Sound.Should().Be(SSFSound.DEAD);

            ModifySSF mod_criticalhit = modifiers[16];
            mod_criticalhit.Sound.Should().Be(SSFSound.CRITICAL_HIT);

            ModifySSF mod_targetimmune = modifiers[17];
            mod_targetimmune.Sound.Should().Be(SSFSound.TARGET_IMMUNE);

            ModifySSF mod_placemine = modifiers[18];
            mod_placemine.Sound.Should().Be(SSFSound.LAY_MINE);

            ModifySSF mod_disarmmine = modifiers[19];
            mod_disarmmine.Sound.Should().Be(SSFSound.DISARM_MINE);

            ModifySSF mod_stealthon = modifiers[20];
            mod_stealthon.Sound.Should().Be(SSFSound.BEGIN_STEALTH);

            ModifySSF mod_search = modifiers[21];
            mod_search.Sound.Should().Be(SSFSound.BEGIN_SEARCH);

            ModifySSF mod_picklockstart = modifiers[22];
            mod_picklockstart.Sound.Should().Be(SSFSound.BEGIN_UNLOCK);

            ModifySSF mod_picklockfail = modifiers[23];
            mod_picklockfail.Sound.Should().Be(SSFSound.UNLOCK_FAILED);

            ModifySSF mod_picklockdone = modifiers[24];
            mod_picklockdone.Sound.Should().Be(SSFSound.UNLOCK_SUCCESS);

            ModifySSF mod_leaveparty = modifiers[25];
            mod_leaveparty.Sound.Should().Be(SSFSound.SEPARATED_FROM_PARTY);

            ModifySSF mod_rejoinparty = modifiers[26];
            mod_rejoinparty.Sound.Should().Be(SSFSound.REJOINED_PARTY);

            ModifySSF mod_poisoned = modifiers[27];
            mod_poisoned.Sound.Should().Be(SSFSound.POISONED);
        }

    }
}

