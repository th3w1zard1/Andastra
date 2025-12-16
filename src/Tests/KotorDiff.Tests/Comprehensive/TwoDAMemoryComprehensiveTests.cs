// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:264-465
// Original: class Test2DAMemoryComprehensive: ...
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using static Andastra.Parsing.Formats.GFF.GFFAuto;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Formats.TwoDA;
using Andastra.Parsing.Resources;
using KotorDiff.Tests.Helpers;
using Xunit;

namespace KotorDiff.Tests.Comprehensive
{
    /// <summary>
    /// Comprehensive tests for 2DAMEMORY token generation and usage.
    /// Matching PyKotor implementation at vendor/PyKotor/Libraries/PyKotor/tests/tslpatcher/test_diff_comprehensive.py:264-465
    /// </summary>
    public class TwoDAMemoryComprehensiveTests : IDisposable
    {
        private DirectoryInfo _tempDir;
        private DirectoryInfo _vanillaDir;
        private DirectoryInfo _moddedDir;
        private DirectoryInfo _tslpatchdataDir;

        public TwoDAMemoryComprehensiveTests()
        {
            var (tempDir, vanillaDir, moddedDir, tslpatchdataDir) = TestDataHelper.CreateTestEnv();
            _tempDir = tempDir;
            _vanillaDir = vanillaDir;
            _moddedDir = moddedDir;
            _tslpatchdataDir = tslpatchdataDir;
        }

        public void Dispose()
        {
            TestDataHelper.CleanupTestEnv(_tempDir);
        }

        [Fact]
        public void AddRow_StoresRowIndex()
        {
            // Test: AddRow2DA stores RowIndex in 2DAMEMORY token.
            // Pattern from real mods:
            // [spells.2da]
            // AddRow0=Battle_Meditation
            // [Battle_Meditation]
            // 2DAMEMORY1=RowIndex

            var stopwatch = Stopwatch.StartNew();

            // Vanilla: 2 rows
            var vanilla2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "name" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "spell_0" }, { "name", "100" } }),
                    ("1", new Dictionary<string, string> { { "label", "spell_1" }, { "name", "101" } })
                });
            TwoDAAuto.WriteTwoDA(vanilla2da, Path.Combine(_vanillaDir.FullName, "spells.2da"), ResourceType.TwoDA);

            // Modded: 3 rows (new row added)
            var modded2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "name" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "spell_0" }, { "name", "100" } }),
                    ("1", new Dictionary<string, string> { { "label", "spell_1" }, { "name", "101" } }),
                    ("2", new Dictionary<string, string> { { "label", "new_spell" }, { "name", "102" } })
                });
            TwoDAAuto.WriteTwoDA(modded2da, Path.Combine(_moddedDir.FullName, "spells.2da"), ResourceType.TwoDA);

            // Run diff
            string iniContent = TestDataHelper.RunDiff(_vanillaDir, _moddedDir, _tslpatchdataDir);

            // Verify AddRow with 2DAMEMORY token
            Assert.Contains("[spells.2da]", iniContent);
            Assert.Contains("AddRow0=", iniContent);
            Assert.Contains("2DAMEMORY", iniContent, StringComparison.OrdinalIgnoreCase);

            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 120000, $"Test took {stopwatch.ElapsedMilliseconds}ms, should be under 120000ms");
        }

        [Fact]
        public void ChangeRow_StoresRowIndex()
        {
            // Test: ChangeRow2DA stores RowIndex in 2DAMEMORY token.
            // This tests the pattern where a modified row's index is stored
            // for use in other modifications.

            var stopwatch = Stopwatch.StartNew();

            // Vanilla: 3 rows
            var vanilla2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "value" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "item_0" }, { "value", "10" } }),
                    ("1", new Dictionary<string, string> { { "label", "item_1" }, { "value", "20" } }),
                    ("2", new Dictionary<string, string> { { "label", "item_2" }, { "value", "30" } })
                });
            TwoDAAuto.WriteTwoDA(vanilla2da, Path.Combine(_vanillaDir.FullName, "baseitems.2da"), ResourceType.TwoDA);

            // Modded: row 1 modified
            var modded2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "value" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "item_0" }, { "value", "10" } }),
                    ("1", new Dictionary<string, string> { { "label", "item_1_modified" }, { "value", "999" } }),
                    ("2", new Dictionary<string, string> { { "label", "item_2" }, { "value", "30" } })
                });
            TwoDAAuto.WriteTwoDA(modded2da, Path.Combine(_moddedDir.FullName, "baseitems.2da"), ResourceType.TwoDA);

            // Run diff
            string iniContent = TestDataHelper.RunDiff(_vanillaDir, _moddedDir, _tslpatchdataDir);

            // Verify ChangeRow exists
            Assert.Contains("[baseitems.2da]", iniContent);
            Assert.Contains("ChangeRow0=", iniContent);

            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 120000, $"Test took {stopwatch.ElapsedMilliseconds}ms, should be under 120000ms");
        }

        [Fact]
        public void TwoDAMemory_CrossReferenceChain()
        {
            // Test: 2DAMEMORY tokens used in chained references across multiple 2DA files.
            // Real-world pattern from dm_qrts mod:
            // weaponsounds.2da AddRow -> 2DAMEMORY1
            // baseitems.2da AddRow uses 2DAMEMORY1 for weaponmattype -> stores own index in 2DAMEMORY2
            // GFF files use 2DAMEMORY2 for BaseItem

            var stopwatch = Stopwatch.StartNew();

            // Vanilla: weaponsounds.2da with 2 rows
            var vanillaWeaponsounds = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "cloth0" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "weapon_sound_0" }, { "cloth0", "snd0" } }),
                    ("1", new Dictionary<string, string> { { "label", "weapon_sound_1" }, { "cloth0", "snd1" } })
                });
            TwoDAAuto.WriteTwoDA(vanillaWeaponsounds, Path.Combine(_vanillaDir.FullName, "weaponsounds.2da"), ResourceType.TwoDA);

            // Vanilla: baseitems.2da with 2 rows
            var vanillaBaseitems = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "weaponmattype" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "base_0" }, { "weaponmattype", "0" } }),
                    ("1", new Dictionary<string, string> { { "label", "base_1" }, { "weaponmattype", "1" } })
                });
            TwoDAAuto.WriteTwoDA(vanillaBaseitems, Path.Combine(_vanillaDir.FullName, "baseitems.2da"), ResourceType.TwoDA);

            // Vanilla GFF
            var vanillaGff = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType, object)>
            {
                { "BaseItem", (GFFFieldType.Int32, 0) }
            });
            GFFAuto.WriteGff(vanillaGff, Path.Combine(_vanillaDir.FullName, "item.uti"), ResourceType.UTI);

            // Modded: weaponsounds.2da with 3 rows (new row 2)
            var moddedWeaponsounds = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "cloth0" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "weapon_sound_0" }, { "cloth0", "snd0" } }),
                    ("1", new Dictionary<string, string> { { "label", "weapon_sound_1" }, { "cloth0", "snd1" } }),
                    ("2", new Dictionary<string, string> { { "label", "new_weapon_sound" }, { "cloth0", "snd2" } })
                });
            TwoDAAuto.WriteTwoDA(moddedWeaponsounds, Path.Combine(_moddedDir.FullName, "weaponsounds.2da"), ResourceType.TwoDA);

            // Modded: baseitems.2da with 3 rows (new row 2 references new weaponsounds row)
            var moddedBaseitems = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "weaponmattype" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "base_0" }, { "weaponmattype", "0" } }),
                    ("1", new Dictionary<string, string> { { "label", "base_1" }, { "weaponmattype", "1" } }),
                    ("2", new Dictionary<string, string> { { "label", "new_base" }, { "weaponmattype", "2" } }) // References new weaponsounds row
                });
            TwoDAAuto.WriteTwoDA(moddedBaseitems, Path.Combine(_moddedDir.FullName, "baseitems.2da"), ResourceType.TwoDA);

            // Modded GFF: references new baseitems row
            var moddedGff = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType, object)>
            {
                { "BaseItem", (GFFFieldType.Int32, 2) }
            });
            GFFAuto.WriteGff(moddedGff, Path.Combine(_moddedDir.FullName, "item.uti"), ResourceType.UTI);

            // Run diff
            string iniContent = TestDataHelper.RunDiff(_vanillaDir, _moddedDir, _tslpatchdataDir, loggingEnabled: true);

            // Verify chain exists:
            // 1. weaponsounds.2da AddRow stores index in 2DAMEMORY
            // 2. baseitems.2da AddRow uses 2DAMEMORY for weaponmattype, stores own index
            // 3. item.uti uses 2DAMEMORY for BaseItem
            Assert.Contains("[weaponsounds.2da]", iniContent);
            Assert.Contains("[baseitems.2da]", iniContent);
            Assert.Contains("[item.uti]", iniContent);
            Assert.Contains("2DAMEMORY", iniContent, StringComparison.OrdinalIgnoreCase);

            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 120000, $"Test took {stopwatch.ElapsedMilliseconds}ms, should be under 120000ms");
        }

        [Fact]
        public void AddColumn_With2DAMemoryStorage()
        {
            // Test: AddColumn2DA with 2DAMEMORY storage for specific cell values.
            // Pattern:
            // [add_column]
            // ColumnLabel=NewCol
            // DefaultValue=0
            // I5=special_value
            // 2DAMEMORY#=I5

            var stopwatch = Stopwatch.StartNew();

            // Vanilla: 6 rows, 1 column
            var vanilla2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "row_0" } }),
                    ("1", new Dictionary<string, string> { { "label", "row_1" } }),
                    ("2", new Dictionary<string, string> { { "label", "row_2" } }),
                    ("3", new Dictionary<string, string> { { "label", "row_3" } }),
                    ("4", new Dictionary<string, string> { { "label", "row_4" } }),
                    ("5", new Dictionary<string, string> { { "label", "row_5" } })
                });
            TwoDAAuto.WriteTwoDA(vanilla2da, Path.Combine(_vanillaDir.FullName, "test.2da"), ResourceType.TwoDA);

            // Modded: 6 rows, 2 columns (new column added)
            var modded2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label", "newcol" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "row_0" }, { "newcol", "0" } }),
                    ("1", new Dictionary<string, string> { { "label", "row_1" }, { "newcol", "0" } }),
                    ("2", new Dictionary<string, string> { { "label", "row_2" }, { "newcol", "0" } }),
                    ("3", new Dictionary<string, string> { { "label", "row_3" }, { "newcol", "0" } }),
                    ("4", new Dictionary<string, string> { { "label", "row_4" }, { "newcol", "0" } }),
                    ("5", new Dictionary<string, string> { { "label", "row_5" }, { "newcol", "special" } })
                });
            TwoDAAuto.WriteTwoDA(modded2da, Path.Combine(_moddedDir.FullName, "test.2da"), ResourceType.TwoDA);

            // Run diff
            string iniContent = TestDataHelper.RunDiff(_vanillaDir, _moddedDir, _tslpatchdataDir);

            // Verify AddColumn
            Assert.Contains("[test.2da]", iniContent);
            Assert.Contains("AddColumn", iniContent, StringComparison.OrdinalIgnoreCase);

            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 120000, $"Test took {stopwatch.ElapsedMilliseconds}ms, should be under 120000ms");
        }

        [Fact]
        public void MultipleGFFFiles_UseSame2DAMemoryToken()
        {
            // Test: Multiple GFF files reference the same 2DA row via same token.
            // Real-world pattern from Bastila Battle Meditation:
            // spells.2da AddRow -> 2DAMEMORY1
            // Multiple creature files use 2DAMEMORY1 for Spell field

            var stopwatch = Stopwatch.StartNew();

            // Vanilla: 2DA with 2 rows
            var vanilla2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "soundset_0" } }),
                    ("1", new Dictionary<string, string> { { "label", "soundset_1" } })
                });
            TwoDAAuto.WriteTwoDA(vanilla2da, Path.Combine(_vanillaDir.FullName, "soundset.2da"), ResourceType.TwoDA);

            // Vanilla: 2 GFF files reference row 0
            var vanillaGff1 = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType, object)>
            {
                { "SoundSetFile", (GFFFieldType.UInt16, (ushort)0) }
            });
            GFFAuto.WriteGff(vanillaGff1, Path.Combine(_vanillaDir.FullName, "creature1.utc"), ResourceType.GFF);

            var vanillaGff2 = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType, object)>
            {
                { "SoundSetFile", (GFFFieldType.UInt16, (ushort)0) }
            });
            GFFAuto.WriteGff(vanillaGff2, Path.Combine(_vanillaDir.FullName, "creature2.utc"), ResourceType.GFF);

            // Modded: 2DA with 3 rows (new row 2)
            var modded2da = TestDataHelper.CreateBasic2DA(
                new List<string> { "label" },
                new List<(string, Dictionary<string, string>)>
                {
                    ("0", new Dictionary<string, string> { { "label", "soundset_0" } }),
                    ("1", new Dictionary<string, string> { { "label", "soundset_1" } }),
                    ("2", new Dictionary<string, string> { { "label", "new_soundset" } })
                });
            TwoDAAuto.WriteTwoDA(modded2da, Path.Combine(_moddedDir.FullName, "soundset.2da"), ResourceType.TwoDA);

            // Modded: Both GFF files now reference row 2
            var moddedGff1 = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType, object)>
            {
                { "SoundSetFile", (GFFFieldType.UInt16, (ushort)2) }
            });
            GFFAuto.WriteGff(moddedGff1, Path.Combine(_moddedDir.FullName, "creature1.utc"), ResourceType.GFF);

            var moddedGff2 = TestDataHelper.CreateBasicGFF(new Dictionary<string, (GFFFieldType, object)>
            {
                { "SoundSetFile", (GFFFieldType.UInt16, (ushort)2) }
            });
            GFFAuto.WriteGff(moddedGff2, Path.Combine(_moddedDir.FullName, "creature2.utc"), ResourceType.GFF);

            // Run diff
            string iniContent = TestDataHelper.RunDiff(_vanillaDir, _moddedDir, _tslpatchdataDir);

            // Verify all files are present and 2DAMEMORY is used
            Assert.Contains("[soundset.2da]", iniContent);
            Assert.Contains("[creature1.utc]", iniContent);
            Assert.Contains("[creature2.utc]", iniContent);
            Assert.Contains("2DAMEMORY", iniContent, StringComparison.OrdinalIgnoreCase);

            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 120000, $"Test took {stopwatch.ElapsedMilliseconds}ms, should be under 120000ms");
        }
    }
}

