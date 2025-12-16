using System;
using System.Collections.Generic;
using AuroraEngine.Common.Common;
using AuroraEngine.Common.Formats.GFF;
using AuroraEngine.Common.Resource.Generics;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;
using GFFAuto = AuroraEngine.Common.Formats.GFF.GFFAuto;

namespace HolocronToolset.NET.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py
    // Original: Comprehensive tests for UTC Editor
    [Collection("Avalonia Test Collection")]
    public class UTCEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTCEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtcEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py
            // Original: def test_utc_editor_new_file_creation(qtbot, installation):
            var editor = new UTCEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2413-2433
        // Original: def test_utc_editor_load_real_file(qtbot, installation: HTInstallation, test_files_dir):
        [Fact]
        public void TestUtcEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find a UTC file
            string utcFile = System.IO.Path.Combine(testFilesDir, "p_hk47.utc");
            if (!System.IO.File.Exists(utcFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utcFile = System.IO.Path.Combine(testFilesDir, "p_hk47.utc");
            }

            if (!System.IO.File.Exists(utcFile))
            {
                // Skip if no UTC files available for testing (matching Python pytest.skip behavior)
                return;
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            var editor = new UTCEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(utcFile);
            editor.Load(utcFile, "p_hk47", ResourceType.UTC, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            GFF gff = AuroraEngine.Common.Formats.GFF.GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1362-1465
        // Original: def test_utc_editor_gff_roundtrip_comparison(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtcEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find p_hk47.utc
            string utcFile = System.IO.Path.Combine(testFilesDir, "p_hk47.utc");
            if (!System.IO.File.Exists(utcFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utcFile = System.IO.Path.Combine(testFilesDir, "p_hk47.utc");
            }

            if (!System.IO.File.Exists(utcFile))
            {
                // Skip if test file not available
                return;
            }

            // Get installation if available
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            HTInstallation installation = null;
            if (System.IO.Directory.Exists(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                installation = new HTInstallation(k1Path, "Test Installation", tsl: false);
            }
            else
            {
                // Fallback to K2
                string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
                if (string.IsNullOrEmpty(k2Path))
                {
                    k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
                }

                if (System.IO.Directory.Exists(k2Path) && System.IO.File.Exists(System.IO.Path.Combine(k2Path, "chitin.key")))
                {
                    installation = new HTInstallation(k2Path, "Test Installation", tsl: true);
                }
            }

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            var editor = new UTCEditor(null, installation);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1378
            // Original: original_data = utc_file.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(utcFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1379
            // Original: original_utc = read_utc(original_data)
            var originalGff = AuroraEngine.Common.Formats.GFF.GFF.FromBytes(data);
            UTC originalUtc = UTCHelpers.ConstructUtc(originalGff);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1380
            // Original: editor.load(utc_file, "p_hk47", ResourceType.UTC, original_data)
            editor.Load(utcFile, "p_hk47", ResourceType.UTC, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1383
            // Original: data, _ = editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1384
            // Original: new_utc = read_utc(data)
            GFF newGff = AuroraEngine.Common.Formats.GFF.GFF.FromBytes(newData);
            UTC newUtc = UTCHelpers.ConstructUtc(newGff);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1389-1465
            // Original: assert str(new_utc.resref) == str(original_utc.resref) ... (many field comparisons)
            // Note: Python test does functional UTC comparison rather than raw GFF comparison
            // because dismantle_utc always writes all fields (including deprecated ones)
            newUtc.ResRef.ToString().Should().Be(originalUtc.ResRef.ToString());
            newUtc.Tag.Should().Be(originalUtc.Tag);
            newUtc.Comment.Should().Be(originalUtc.Comment);
            newUtc.Conversation.ToString().Should().Be(originalUtc.Conversation.ToString());
            newUtc.FirstName.StringRef.Should().Be(originalUtc.FirstName.StringRef);
            newUtc.LastName.StringRef.Should().Be(originalUtc.LastName.StringRef);
            newUtc.AppearanceId.Should().Be(originalUtc.AppearanceId);
            newUtc.SoundsetId.Should().Be(originalUtc.SoundsetId);
            newUtc.PortraitId.Should().Be(originalUtc.PortraitId);
            newUtc.RaceId.Should().Be(originalUtc.RaceId);
            newUtc.SubraceId.Should().Be(originalUtc.SubraceId);
            newUtc.WalkrateId.Should().Be(originalUtc.WalkrateId);
            newUtc.FactionId.Should().Be(originalUtc.FactionId);
            newUtc.GenderId.Should().Be(originalUtc.GenderId);
            newUtc.PerceptionId.Should().Be(originalUtc.PerceptionId);
            newUtc.Disarmable.Should().Be(originalUtc.Disarmable);
            newUtc.NoPermDeath.Should().Be(originalUtc.NoPermDeath);
            newUtc.Min1Hp.Should().Be(originalUtc.Min1Hp);
            newUtc.Plot.Should().Be(originalUtc.Plot);
            newUtc.IsPc.Should().Be(originalUtc.IsPc);
            newUtc.NotReorienting.Should().Be(originalUtc.NotReorienting);
            newUtc.IgnoreCrePath.Should().Be(originalUtc.IgnoreCrePath);
            newUtc.Hologram.Should().Be(originalUtc.Hologram);
            Math.Abs(newUtc.ChallengeRating - originalUtc.ChallengeRating).Should().BeLessThan(0.001f);
            newUtc.Alignment.Should().Be(originalUtc.Alignment);
            newUtc.Strength.Should().Be(originalUtc.Strength);
            newUtc.Dexterity.Should().Be(originalUtc.Dexterity);
            newUtc.Constitution.Should().Be(originalUtc.Constitution);
            newUtc.Intelligence.Should().Be(originalUtc.Intelligence);
            newUtc.Wisdom.Should().Be(originalUtc.Wisdom);
            newUtc.Charisma.Should().Be(originalUtc.Charisma);
            newUtc.Hp.Should().Be(originalUtc.Hp);
            newUtc.CurrentHp.Should().Be(originalUtc.CurrentHp);
            newUtc.MaxHp.Should().Be(originalUtc.MaxHp);
            newUtc.Fp.Should().Be(originalUtc.Fp);
            newUtc.NaturalAc.Should().Be(originalUtc.NaturalAc);
            newUtc.FortitudeBonus.Should().Be(originalUtc.FortitudeBonus);
            newUtc.ReflexBonus.Should().Be(originalUtc.ReflexBonus);
            newUtc.WillpowerBonus.Should().Be(originalUtc.WillpowerBonus);
            newUtc.ComputerUse.Should().Be(originalUtc.ComputerUse);
            newUtc.Demolitions.Should().Be(originalUtc.Demolitions);
            newUtc.Stealth.Should().Be(originalUtc.Stealth);
            newUtc.Awareness.Should().Be(originalUtc.Awareness);
            newUtc.Persuade.Should().Be(originalUtc.Persuade);
            newUtc.Repair.Should().Be(originalUtc.Repair);
            newUtc.Security.Should().Be(originalUtc.Security);
            newUtc.TreatInjury.Should().Be(originalUtc.TreatInjury);
            newUtc.OnBlocked.ToString().Should().Be(originalUtc.OnBlocked.ToString());
            newUtc.OnAttacked.ToString().Should().Be(originalUtc.OnAttacked.ToString());
            newUtc.OnNotice.ToString().Should().Be(originalUtc.OnNotice.ToString());
            newUtc.OnDialog.ToString().Should().Be(originalUtc.OnDialog.ToString());
            newUtc.OnDamaged.ToString().Should().Be(originalUtc.OnDamaged.ToString());
            newUtc.OnDeath.ToString().Should().Be(originalUtc.OnDeath.ToString());
            newUtc.OnEndRound.ToString().Should().Be(originalUtc.OnEndRound.ToString());
            newUtc.OnEndDialog.ToString().Should().Be(originalUtc.OnEndDialog.ToString());
            newUtc.OnDisturbed.ToString().Should().Be(originalUtc.OnDisturbed.ToString());
            newUtc.OnHeartbeat.ToString().Should().Be(originalUtc.OnHeartbeat.ToString());
            newUtc.OnSpawn.ToString().Should().Be(originalUtc.OnSpawn.ToString());
            newUtc.OnSpell.ToString().Should().Be(originalUtc.OnSpell.ToString());
            newUtc.OnUserDefined.ToString().Should().Be(originalUtc.OnUserDefined.ToString());
            newUtc.Classes.Count.Should().Be(originalUtc.Classes.Count);
            for (int i = 0; i < newUtc.Classes.Count && i < originalUtc.Classes.Count; i++)
            {
                newUtc.Classes[i].ClassId.Should().Be(originalUtc.Classes[i].ClassId);
                newUtc.Classes[i].ClassLevel.Should().Be(originalUtc.Classes[i].ClassLevel);
                newUtc.Classes[i].Powers.Count.Should().Be(originalUtc.Classes[i].Powers.Count);
                // Powers are stored per class - compare sets (Powers is List<int>, not List<ResRef>)
                var newPowersSet = new HashSet<int>(newUtc.Classes[i].Powers);
                var origPowersSet = new HashSet<int>(originalUtc.Classes[i].Powers);
                newPowersSet.SetEquals(origPowersSet).Should().BeTrue();
            }
            newUtc.Feats.Count.Should().Be(originalUtc.Feats.Count);
            var newFeatsSet = new HashSet<int>(newUtc.Feats);
            var origFeatsSet = new HashSet<int>(originalUtc.Feats);
            newFeatsSet.SetEquals(origFeatsSet).Should().BeTrue();
            newUtc.Inventory.Count.Should().Be(originalUtc.Inventory.Count);
            for (int i = 0; i < newUtc.Inventory.Count && i < originalUtc.Inventory.Count; i++)
            {
                newUtc.Inventory[i].ResRef.ToString().Should().Be(originalUtc.Inventory[i].ResRef.ToString());
                newUtc.Inventory[i].Infinite.Should().Be(originalUtc.Inventory[i].Infinite);
            }
            // K2-only fields - only compare if installation is K2
            if (installation.Tsl)
            {
                Math.Abs(newUtc.Blindspot - originalUtc.Blindspot).Should().BeLessThan(0.001f);
                newUtc.MultiplierSet.Should().Be(originalUtc.MultiplierSet);
            }
        }
    }
}
