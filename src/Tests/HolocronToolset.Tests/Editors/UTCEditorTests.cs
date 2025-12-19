using System;
using System.Collections.Generic;
using System.Reflection;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resource;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;
using Andastra.Parsing.Common;
using Avalonia.Controls;
using GFFAuto = Andastra.Parsing.Formats.GFF.GFFAuto;

namespace HolocronToolset.Tests.Editors
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
            GFF gff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
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
            var originalGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
            UTC originalUtc = UTCHelpers.ConstructUtc(originalGff);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1380
            // Original: editor.load(utc_file, "p_hk47", ResourceType.UTC, original_data)
            editor.Load(utcFile, "p_hk47", ResourceType.UTC, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1383
            // Original: data, _ = editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1384
            // Original: new_utc = read_utc(data)
            GFF newGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(newData);
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

        // TODO: STUB - Implement test_utc_editor_manipulate_firstname_locstring (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:23-48)
        // Original: def test_utc_editor_manipulate_firstname_locstring(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating firstname LocalizedString field.
        [Fact]
        public void TestUtcEditorManipulateFirstnameLocstring()
        {
            // TODO: STUB - Implement firstname LocalizedString manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:23-48
            throw new NotImplementedException("TestUtcEditorManipulateFirstnameLocstring: Firstname LocalizedString manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_lastname_locstring (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:50-73)
        // Original: def test_utc_editor_manipulate_lastname_locstring(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating lastname LocalizedString field.
        [Fact]
        public void TestUtcEditorManipulateLastnameLocstring()
        {
            // TODO: STUB - Implement lastname LocalizedString manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:50-73
            throw new NotImplementedException("TestUtcEditorManipulateLastnameLocstring: Lastname LocalizedString manipulation test not yet implemented");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:75-97
        // Original: def test_utc_editor_manipulate_tag(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating tag field.
        [Fact]
        public void TestUtcEditorManipulateTag()
        {
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

            var editor = new UTCEditor(null, installation);

            // Test 1: Set tag to empty string
            editor.New();
            var tagEdit = GetTagEdit(editor);
            tagEdit.Should().NotBeNull("Tag edit box should exist");

            tagEdit.Text = "";
            var (data1, _) = editor.Build();
            data1.Should().NotBeNull();
            var gff1 = GFF.FromBytes(data1);
            var utc1 = UTCHelpers.ConstructUtc(gff1);
            utc1.Tag.Should().Be("", "Tag should be empty string");

            // Test 2: Set tag to a simple value
            tagEdit.Text = "test_creature";
            var (data2, _) = editor.Build();
            data2.Should().NotBeNull();
            var gff2 = GFF.FromBytes(data2);
            var utc2 = UTCHelpers.ConstructUtc(gff2);
            utc2.Tag.Should().Be("test_creature", "Tag should be 'test_creature'");

            // Test 3: Set tag to a longer value
            tagEdit.Text = "my_custom_creature_tag_123";
            var (data3, _) = editor.Build();
            data3.Should().NotBeNull();
            var gff3 = GFF.FromBytes(data3);
            var utc3 = UTCHelpers.ConstructUtc(gff3);
            utc3.Tag.Should().Be("my_custom_creature_tag_123", "Tag should be 'my_custom_creature_tag_123'");

            // Test 4: Set tag with numbers
            tagEdit.Text = "creature_001";
            var (data4, _) = editor.Build();
            data4.Should().NotBeNull();
            var gff4 = GFF.FromBytes(data4);
            var utc4 = UTCHelpers.ConstructUtc(gff4);
            utc4.Tag.Should().Be("creature_001", "Tag should be 'creature_001'");

            // Test 5: Verify value persists through load/save cycle
            tagEdit.Text = "persistent_tag";
            var (data5, _) = editor.Build();
            data5.Should().NotBeNull();
            
            // Load the data back
            editor.Load("test_creature", "test_creature", ResourceType.UTC, data5);
            var tagEditReloaded = GetTagEdit(editor);
            tagEditReloaded.Should().NotBeNull();
            tagEditReloaded.Text.Should().Be("persistent_tag", "Tag should persist through load/save cycle");

            // Test 6: Verify value is correctly read from loaded UTC
            var (data6, _) = editor.Build();
            data6.Should().NotBeNull();
            var gff6 = GFF.FromBytes(data6);
            var utc6 = UTCHelpers.ConstructUtc(gff6);
            utc6.Tag.Should().Be("persistent_tag", "Tag should be correctly read from loaded UTC");

            // Test 7: Test edge case - very long tag
            tagEdit.Text = new string('a', 32);
            var (data7, _) = editor.Build();
            data7.Should().NotBeNull();
            var gff7 = GFF.FromBytes(data7);
            var utc7 = UTCHelpers.ConstructUtc(gff7);
            utc7.Tag.Should().Be(new string('a', 32), "Tag should handle very long values");

            // Test 8: Test edge case - tag with special characters (underscores and numbers are typical)
            tagEdit.Text = "creature_tag_123";
            var (data8, _) = editor.Build();
            data8.Should().NotBeNull();
            var gff8 = GFF.FromBytes(data8);
            var utc8 = UTCHelpers.ConstructUtc(gff8);
            utc8.Tag.Should().Be("creature_tag_123", "Tag should handle special characters");
        }

        /// <summary>
        /// Helper method to get the tag edit box from the editor using reflection.
        /// </summary>
        private static TextBox GetTagEdit(UTCEditor editor)
        {
            var field = typeof(UTCEditor).GetField("_tagEdit", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException("_tagEdit field not found in UTCEditor");
            }
            return field.GetValue(editor) as TextBox;
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_tag_generate_button (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:99-119)
        // Original: def test_utc_editor_manipulate_tag_generate_button(qtbot, installation: HTInstallation, test_files_dir: Path): Test tag generation button.
        [Fact]
        public void TestUtcEditorManipulateTagGenerateButton()
        {
            // TODO: STUB - Implement tag generation button test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:99-119
            throw new NotImplementedException("TestUtcEditorManipulateTagGenerateButton: Tag generation button test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_resref (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:121-143)
        // Original: def test_utc_editor_manipulate_resref(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating resref field.
        [Fact]
        public void TestUtcEditorManipulateResref()
        {
            // TODO: STUB - Implement resref field manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:121-143
            throw new NotImplementedException("TestUtcEditorManipulateResref: Resref field manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_appearance_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:145-170)
        // Original: def test_utc_editor_manipulate_appearance_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating appearance combo box.
        [Fact]
        public void TestUtcEditorManipulateAppearanceSelect()
        {
            // TODO: STUB - Implement appearance combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:145-170
            throw new NotImplementedException("TestUtcEditorManipulateAppearanceSelect: Appearance combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_soundset_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:172-192)
        // Original: def test_utc_editor_manipulate_soundset_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating soundset combo box.
        [Fact]
        public void TestUtcEditorManipulateSoundsetSelect()
        {
            // TODO: STUB - Implement soundset combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:172-192
            throw new NotImplementedException("TestUtcEditorManipulateSoundsetSelect: Soundset combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_portrait_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:194-214)
        // Original: def test_utc_editor_manipulate_portrait_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating portrait combo box.
        [Fact]
        public void TestUtcEditorManipulatePortraitSelect()
        {
            // TODO: STUB - Implement portrait combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:194-214
            throw new NotImplementedException("TestUtcEditorManipulatePortraitSelect: Portrait combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_conversation (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:216-238)
        // Original: def test_utc_editor_manipulate_conversation(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating conversation field.
        [Fact]
        public void TestUtcEditorManipulateConversation()
        {
            // TODO: STUB - Implement conversation field manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:216-238
            throw new NotImplementedException("TestUtcEditorManipulateConversation: Conversation field manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_alignment_slider (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:240-268)
        // Original: def test_utc_editor_manipulate_alignment_slider(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating alignment slider.
        [Fact]
        public void TestUtcEditorManipulateAlignmentSlider()
        {
            // TODO: STUB - Implement alignment slider manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:240-268
            throw new NotImplementedException("TestUtcEditorManipulateAlignmentSlider: Alignment slider manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_disarmable_checkbox (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:270-291)
        // Original: def test_utc_editor_manipulate_disarmable_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating disarmable checkbox.
        [Fact]
        public void TestUtcEditorManipulateDisarmableCheckbox()
        {
            // TODO: STUB - Implement disarmable checkbox manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:270-291
            throw new NotImplementedException("TestUtcEditorManipulateDisarmableCheckbox: Disarmable checkbox manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_no_perm_death_checkbox (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:293-314)
        // Original: def test_utc_editor_manipulate_no_perm_death_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating no perm death checkbox.
        [Fact]
        public void TestUtcEditorManipulateNoPermDeathCheckbox()
        {
            // TODO: STUB - Implement no perm death checkbox manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:293-314
            throw new NotImplementedException("TestUtcEditorManipulateNoPermDeathCheckbox: No perm death checkbox manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_min1_hp_checkbox (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:316-337)
        // Original: def test_utc_editor_manipulate_min1_hp_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating min1 hp checkbox.
        [Fact]
        public void TestUtcEditorManipulateMin1HpCheckbox()
        {
            // TODO: STUB - Implement min1 hp checkbox manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:316-337
            throw new NotImplementedException("TestUtcEditorManipulateMin1HpCheckbox: Min1 hp checkbox manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_plot_checkbox (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:339-360)
        // Original: def test_utc_editor_manipulate_plot_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating plot checkbox.
        [Fact]
        public void TestUtcEditorManipulatePlotCheckbox()
        {
            // TODO: STUB - Implement plot checkbox manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:339-360
            throw new NotImplementedException("TestUtcEditorManipulatePlotCheckbox: Plot checkbox manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_is_pc_checkbox (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:362-383)
        // Original: def test_utc_editor_manipulate_is_pc_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating is pc checkbox.
        [Fact]
        public void TestUtcEditorManipulateIsPcCheckbox()
        {
            // TODO: STUB - Implement is pc checkbox manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:362-383
            throw new NotImplementedException("TestUtcEditorManipulateIsPcCheckbox: Is pc checkbox manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_no_reorientate_checkbox (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:385-406)
        // Original: def test_utc_editor_manipulate_no_reorientate_checkbox(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating no reorientate checkbox.
        [Fact]
        public void TestUtcEditorManipulateNoReorientateCheckbox()
        {
            // TODO: STUB - Implement no reorientate checkbox manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:385-406
            throw new NotImplementedException("TestUtcEditorManipulateNoReorientateCheckbox: No reorientate checkbox manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_tsl_only_checkboxes (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:408-447)
        // Original: def test_utc_editor_manipulate_tsl_only_checkboxes(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating TSL-only checkboxes (ignoreCrePath, hologram).
        [Fact]
        public void TestUtcEditorManipulateTslOnlyCheckboxes()
        {
            // TODO: STUB - Implement TSL-only checkboxes manipulation test (ignoreCrePath, hologram)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:408-447
            throw new NotImplementedException("TestUtcEditorManipulateTslOnlyCheckboxes: TSL-only checkboxes manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_race_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:449-479)
        // Original: def test_utc_editor_manipulate_race_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating race combo box.
        [Fact]
        public void TestUtcEditorManipulateRaceSelect()
        {
            // TODO: STUB - Implement race combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:449-479
            throw new NotImplementedException("TestUtcEditorManipulateRaceSelect: Race combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_subrace_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:481-501)
        // Original: def test_utc_editor_manipulate_subrace_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating subrace combo box.
        [Fact]
        public void TestUtcEditorManipulateSubraceSelect()
        {
            // TODO: STUB - Implement subrace combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:481-501
            throw new NotImplementedException("TestUtcEditorManipulateSubraceSelect: Subrace combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_speed_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:503-523)
        // Original: def test_utc_editor_manipulate_speed_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating speed combo box.
        [Fact]
        public void TestUtcEditorManipulateSpeedSelect()
        {
            // TODO: STUB - Implement speed combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:503-523
            throw new NotImplementedException("TestUtcEditorManipulateSpeedSelect: Speed combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_faction_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:525-545)
        // Original: def test_utc_editor_manipulate_faction_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating faction combo box.
        [Fact]
        public void TestUtcEditorManipulateFactionSelect()
        {
            // TODO: STUB - Implement faction combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:525-545
            throw new NotImplementedException("TestUtcEditorManipulateFactionSelect: Faction combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_gender_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:547-567)
        // Original: def test_utc_editor_manipulate_gender_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating gender combo box.
        [Fact]
        public void TestUtcEditorManipulateGenderSelect()
        {
            // TODO: STUB - Implement gender combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:547-567
            throw new NotImplementedException("TestUtcEditorManipulateGenderSelect: Gender combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_perception_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:569-593)
        // Original: def test_utc_editor_manipulate_perception_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating perception combo box.
        [Fact]
        public void TestUtcEditorManipulatePerceptionSelect()
        {
            // TODO: STUB - Implement perception combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:569-593
            throw new NotImplementedException("TestUtcEditorManipulatePerceptionSelect: Perception combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_challenge_rating_spin (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:595-615)
        // Original: def test_utc_editor_manipulate_challenge_rating_spin(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating challenge rating spin box.
        [Fact]
        public void TestUtcEditorManipulateChallengeRatingSpin()
        {
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

            var editor = new UTCEditor(null, installation);

            // Test 1: Set challenge rating to 0
            editor.New();
            var challengeRatingSpin = GetChallengeRatingSpin(editor);
            challengeRatingSpin.Should().NotBeNull("Challenge rating spin box should exist");

            challengeRatingSpin.Value = 0;
            var (data1, _) = editor.Build();
            data1.Should().NotBeNull();
            var gff1 = GFF.FromBytes(data1);
            var utc1 = UTCHelpers.ConstructUtc(gff1);
            Math.Abs(utc1.ChallengeRating - 0.0f).Should().BeLessThan(0.001f, "Challenge rating should be 0");

            // Test 2: Set challenge rating to a positive integer value
            challengeRatingSpin.Value = 5;
            var (data2, _) = editor.Build();
            data2.Should().NotBeNull();
            var gff2 = GFF.FromBytes(data2);
            var utc2 = UTCHelpers.ConstructUtc(gff2);
            Math.Abs(utc2.ChallengeRating - 5.0f).Should().BeLessThan(0.001f, "Challenge rating should be 5");

            // Test 3: Set challenge rating to a decimal value
            challengeRatingSpin.Value = 12.5m;
            var (data3, _) = editor.Build();
            data3.Should().NotBeNull();
            var gff3 = GFF.FromBytes(data3);
            var utc3 = UTCHelpers.ConstructUtc(gff3);
            Math.Abs(utc3.ChallengeRating - 12.5f).Should().BeLessThan(0.001f, "Challenge rating should be 12.5");

            // Test 4: Set challenge rating to a larger value
            challengeRatingSpin.Value = 25.75m;
            var (data4, _) = editor.Build();
            data4.Should().NotBeNull();
            var gff4 = GFF.FromBytes(data4);
            var utc4 = UTCHelpers.ConstructUtc(gff4);
            Math.Abs(utc4.ChallengeRating - 25.75f).Should().BeLessThan(0.001f, "Challenge rating should be 25.75");

            // Test 5: Verify value persists through load/save cycle
            challengeRatingSpin.Value = 15.25m;
            var (data5, _) = editor.Build();
            data5.Should().NotBeNull();
            
            // Load the data back
            editor.Load("test_creature", "test_creature", ResourceType.UTC, data5);
            var challengeRatingSpinReloaded = GetChallengeRatingSpin(editor);
            challengeRatingSpinReloaded.Should().NotBeNull();
            Math.Abs((float)(challengeRatingSpinReloaded.Value ?? 0) - 15.25f).Should().BeLessThan(0.001f, "Challenge rating should persist through load/save cycle");

            // Test 6: Verify value is correctly read from loaded UTC
            var (data6, _) = editor.Build();
            data6.Should().NotBeNull();
            var gff6 = GFF.FromBytes(data6);
            var utc6 = UTCHelpers.ConstructUtc(gff6);
            Math.Abs(utc6.ChallengeRating - 15.25f).Should().BeLessThan(0.001f, "Challenge rating should be correctly read from loaded UTC");

            // Test 7: Test edge case - very small decimal value
            challengeRatingSpin.Value = 0.1m;
            var (data7, _) = editor.Build();
            data7.Should().NotBeNull();
            var gff7 = GFF.FromBytes(data7);
            var utc7 = UTCHelpers.ConstructUtc(gff7);
            Math.Abs(utc7.ChallengeRating - 0.1f).Should().BeLessThan(0.001f, "Challenge rating should handle very small decimal values");

            // Test 8: Test edge case - large value
            challengeRatingSpin.Value = 100.0m;
            var (data8, _) = editor.Build();
            data8.Should().NotBeNull();
            var gff8 = GFF.FromBytes(data8);
            var utc8 = UTCHelpers.ConstructUtc(gff8);
            Math.Abs(utc8.ChallengeRating - 100.0f).Should().BeLessThan(0.001f, "Challenge rating should handle large values");
        }

        /// <summary>
        /// Helper method to get the challenge rating spin box from the editor using reflection.
        /// </summary>
        private static NumericUpDown GetChallengeRatingSpin(UTCEditor editor)
        {
            var field = typeof(UTCEditor).GetField("_challengeRatingSpin", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                throw new InvalidOperationException("_challengeRatingSpin field not found in UTCEditor");
            }
            return field.GetValue(editor) as NumericUpDown;
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_blindspot_spin (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:617-641)
        // Original: def test_utc_editor_manipulate_blindspot_spin(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating blindspot spin box (TSL only).
        [Fact]
        public void TestUtcEditorManipulateBlindspotSpin()
        {
            // TODO: STUB - Implement blindspot spin box manipulation test (TSL only)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:617-641
            throw new NotImplementedException("TestUtcEditorManipulateBlindspotSpin: Blindspot spin box manipulation test not yet implemented (TSL-only feature)");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_multiplier_set_spin (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:643-671)
        // Original: def test_utc_editor_manipulate_multiplier_set_spin(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating multiplier set spin box (TSL only).
        [Fact]
        public void TestUtcEditorManipulateMultiplierSetSpin()
        {
            // TODO: STUB - Implement multiplier set spin box manipulation test (TSL only)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:643-671
            throw new NotImplementedException("TestUtcEditorManipulateMultiplierSetSpin: Multiplier set spin box manipulation test not yet implemented (TSL-only feature)");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_computer_use_spin (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:673-693)
        // Original: def test_utc_editor_manipulate_computer_use_spin(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating computer use spin box.
        [Fact]
        public void TestUtcEditorManipulateComputerUseSpin()
        {
            // TODO: STUB - Implement computer use spin box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:673-693
            throw new NotImplementedException("TestUtcEditorManipulateComputerUseSpin: Computer use spin box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_all_skill_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:695-727)
        // Original: def test_utc_editor_manipulate_all_skill_spins(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating all skill spin boxes.
        [Fact]
        public void TestUtcEditorManipulateAllSkillSpins()
        {
            // TODO: STUB - Implement all skill spin boxes manipulation test (demolitions, stealth, awareness, persuade, repair, security, treat injury)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:695-727
            throw new NotImplementedException("TestUtcEditorManipulateAllSkillSpins: All skill spin boxes manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_all_save_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:729-756)
        // Original: def test_utc_editor_manipulate_all_save_spins(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating all save spin boxes.
        [Fact]
        public void TestUtcEditorManipulateAllSaveSpins()
        {
            // TODO: STUB - Implement all save spin boxes manipulation test (fortitude, reflex, willpower)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:729-756
            throw new NotImplementedException("TestUtcEditorManipulateAllSaveSpins: All save spin boxes manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_all_ability_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:758-789)
        // Original: def test_utc_editor_manipulate_all_ability_spins(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating all ability spin boxes.
        [Fact]
        public void TestUtcEditorManipulateAllAbilitySpins()
        {
            // TODO: STUB - Implement all ability spin boxes manipulation test (strength, dexterity, constitution, intelligence, wisdom, charisma)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:758-789
            throw new NotImplementedException("TestUtcEditorManipulateAllAbilitySpins: All ability spin boxes manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_hp_fp_spins (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:791-838)
        // Original: def test_utc_editor_manipulate_hp_fp_spins(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating HP and FP spin boxes.
        [Fact]
        public void TestUtcEditorManipulateHpFpSpins()
        {
            // TODO: STUB - Implement HP and FP spin boxes manipulation test (hp, currentHp, maxHp, fp, naturalAc)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:791-838
            throw new NotImplementedException("TestUtcEditorManipulateHpFpSpins: HP and FP spin boxes manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_class1_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:840-861)
        // Original: def test_utc_editor_manipulate_class1_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating class1 combo box.
        [Fact]
        public void TestUtcEditorManipulateClass1Select()
        {
            // TODO: STUB - Implement class1 combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:840-861
            throw new NotImplementedException("TestUtcEditorManipulateClass1Select: Class1 combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_class1_level_spin (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:863-888)
        // Original: def test_utc_editor_manipulate_class1_level_spin(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating class1 level spin box.
        [Fact]
        public void TestUtcEditorManipulateClass1LevelSpin()
        {
            // TODO: STUB - Implement class1 level spin box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:863-888
            throw new NotImplementedException("TestUtcEditorManipulateClass1LevelSpin: Class1 level spin box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_class2_select (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:890-911)
        // Original: def test_utc_editor_manipulate_class2_select(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating class2 combo box.
        [Fact]
        public void TestUtcEditorManipulateClass2Select()
        {
            // TODO: STUB - Implement class2 combo box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:890-911
            throw new NotImplementedException("TestUtcEditorManipulateClass2Select: Class2 combo box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_class2_level_spin (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:913-942)
        // Original: def test_utc_editor_manipulate_class2_level_spin(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating class2 level spin box.
        [Fact]
        public void TestUtcEditorManipulateClass2LevelSpin()
        {
            // TODO: STUB - Implement class2 level spin box manipulation test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:913-942
            throw new NotImplementedException("TestUtcEditorManipulateClass2LevelSpin: Class2 level spin box manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_feats_list (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:944-985)
        // Original: def test_utc_editor_manipulate_feats_list(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating feats list.
        [Fact]
        public void TestUtcEditorManipulateFeatsList()
        {
            // TODO: STUB - Implement feats list manipulation test (add, remove, reorder feats)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:944-985
            throw new NotImplementedException("TestUtcEditorManipulateFeatsList: Feats list manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_powers_list (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:987-1024)
        // Original: def test_utc_editor_manipulate_powers_list(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating powers list.
        [Fact]
        public void TestUtcEditorManipulatePowersList()
        {
            // TODO: STUB - Implement powers list manipulation test (add, remove, reorder powers per class)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:987-1024
            throw new NotImplementedException("TestUtcEditorManipulatePowersList: Powers list manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_all_script_fields (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1026-1067)
        // Original: def test_utc_editor_manipulate_all_script_fields(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating all script fields.
        [Fact]
        public void TestUtcEditorManipulateAllScriptFields()
        {
            // TODO: STUB - Implement all script fields manipulation test (onBlocked, onAttacked, onNotice, onDialog, onDamaged, onDeath, onEndRound, onEndDialog, onDisturbed, onHeartbeat, onSpawn, onSpell, onUserDefined)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1026-1067
            throw new NotImplementedException("TestUtcEditorManipulateAllScriptFields: All script fields manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_comments (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1069-1103)
        // Original: def test_utc_editor_manipulate_comments(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating comments field.
        [Fact]
        public void TestUtcEditorManipulateComments()
        {
            // TODO: STUB - Implement comments field manipulation test (empty, single line, multi-line, special chars, very long)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1069-1103
            throw new NotImplementedException("TestUtcEditorManipulateComments: Comments field manipulation test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_all_basic_fields_combination (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1105-1140)
        // Original: def test_utc_editor_manipulate_all_basic_fields_combination(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating all basic fields simultaneously.
        [Fact]
        public void TestUtcEditorManipulateAllBasicFieldsCombination()
        {
            // TODO: STUB - Implement combination test for all basic fields manipulated simultaneously
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1105-1140
            throw new NotImplementedException("TestUtcEditorManipulateAllBasicFieldsCombination: All basic fields combination test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_all_advanced_fields_combination (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1142-1195)
        // Original: def test_utc_editor_manipulate_all_advanced_fields_combination(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating all advanced fields simultaneously.
        [Fact]
        public void TestUtcEditorManipulateAllAdvancedFieldsCombination()
        {
            // TODO: STUB - Implement combination test for all advanced fields manipulated simultaneously
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1142-1195
            throw new NotImplementedException("TestUtcEditorManipulateAllAdvancedFieldsCombination: All advanced fields combination test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_manipulate_all_stats_fields_combination (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1197-1249)
        // Original: def test_utc_editor_manipulate_all_stats_fields_combination(qtbot, installation: HTInstallation, test_files_dir: Path): Test manipulating all stats fields simultaneously.
        [Fact]
        public void TestUtcEditorManipulateAllStatsFieldsCombination()
        {
            // TODO: STUB - Implement combination test for all stats fields manipulated simultaneously
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1197-1249
            throw new NotImplementedException("TestUtcEditorManipulateAllStatsFieldsCombination: All stats fields combination test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_save_load_roundtrip_identity (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1251-1281)
        // Original: def test_utc_editor_save_load_roundtrip_identity(qtbot, installation: HTInstallation, test_files_dir: Path): Test that save/load roundtrip preserves all data exactly.
        [Fact]
        public void TestUtcEditorSaveLoadRoundtripIdentity()
        {
            // TODO: STUB - Implement save/load roundtrip identity test (preserves all data exactly without modifications)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1251-1281
            throw new NotImplementedException("TestUtcEditorSaveLoadRoundtripIdentity: Save/load roundtrip identity test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_save_load_roundtrip_with_modifications (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1283-1323)
        // Original: def test_utc_editor_save_load_roundtrip_with_modifications(qtbot, installation: HTInstallation, test_files_dir: Path): Test save/load roundtrip with modifications preserves changes.
        [Fact]
        public void TestUtcEditorSaveLoadRoundtripWithModifications()
        {
            // TODO: STUB - Implement save/load roundtrip with modifications test (preserves changes through multiple cycles)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1283-1323
            throw new NotImplementedException("TestUtcEditorSaveLoadRoundtripWithModifications: Save/load roundtrip with modifications test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_multiple_save_load_cycles (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1325-1360)
        // Original: def test_utc_editor_multiple_save_load_cycles(qtbot, installation: HTInstallation, test_files_dir: Path): Test multiple save/load cycles preserve data correctly.
        [Fact]
        public void TestUtcEditorMultipleSaveLoadCycles()
        {
            // TODO: STUB - Implement multiple save/load cycles test (5 cycles with different modifications each time)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1325-1360
            throw new NotImplementedException("TestUtcEditorMultipleSaveLoadCycles: Multiple save/load cycles test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_gff_roundtrip_with_modifications (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1467-1500)
        // Original: def test_utc_editor_gff_roundtrip_with_modifications(qtbot, installation: HTInstallation, test_files_dir: Path): Test GFF roundtrip with modifications still produces valid GFF.
        [Fact]
        public void TestUtcEditorGffRoundtripWithModifications()
        {
            // TODO: STUB - Implement GFF roundtrip with modifications test (validates GFF structure after modifications)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1467-1500
            throw new NotImplementedException("TestUtcEditorGffRoundtripWithModifications: GFF roundtrip with modifications test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_new_file_creation (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1502-1534)
        // Original: def test_utc_editor_new_file_creation(qtbot, installation: HTInstallation): Test creating a new UTC file from scratch.
        [Fact]
        public void TestUtcEditorNewFileCreation()
        {
            // TODO: STUB - Implement new file creation test (already exists as TestUtcEditorNewFileCreation but needs full implementation matching Python)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1502-1534
            // Note: TestUtcEditorNewFileCreation exists but is SIMPLIFIED - needs full field setting and verification
        }

        // TODO: STUB - Implement test_utc_editor_minimum_values (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1536-1563)
        // Original: def test_utc_editor_minimum_values(qtbot, installation: HTInstallation, test_files_dir: Path): Test setting all fields to minimum values.
        [Fact]
        public void TestUtcEditorMinimumValues()
        {
            // TODO: STUB - Implement minimum values edge case test (all fields set to minimums)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1536-1563
            throw new NotImplementedException("TestUtcEditorMinimumValues: Minimum values edge case test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_maximum_values (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1565-1595)
        // Original: def test_utc_editor_maximum_values(qtbot, installation: HTInstallation, test_files_dir: Path): Test setting all fields to maximum values.
        [Fact]
        public void TestUtcEditorMaximumValues()
        {
            // TODO: STUB - Implement maximum values edge case test (all fields set to maximums)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1565-1595
            throw new NotImplementedException("TestUtcEditorMaximumValues: Maximum values edge case test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_feats_powers_combinations (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1597-1631)
        // Original: def test_utc_editor_feats_powers_combinations(qtbot, installation: HTInstallation, test_files_dir: Path): Test feats and powers combinations.
        [Fact]
        public void TestUtcEditorFeatsPowersCombinations()
        {
            // TODO: STUB - Implement feats and powers combinations test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1597-1631
            throw new NotImplementedException("TestUtcEditorFeatsPowersCombinations: Feats and powers combinations test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_classes_combinations (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1633-1661)
        // Original: def test_utc_editor_classes_combinations(qtbot, installation: HTInstallation, test_files_dir: Path): Test classes combinations.
        [Fact]
        public void TestUtcEditorClassesCombinations()
        {
            // TODO: STUB - Implement classes combinations test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1633-1661
            throw new NotImplementedException("TestUtcEditorClassesCombinations: Classes combinations test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_scripts_combination (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1663-1690)
        // Original: def test_utc_editor_all_scripts_combination(qtbot, installation: HTInstallation, test_files_dir: Path): Test all scripts combination.
        [Fact]
        public void TestUtcEditorAllScriptsCombination()
        {
            // TODO: STUB - Implement all scripts combination test
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1663-1690
            throw new NotImplementedException("TestUtcEditorAllScriptsCombination: All scripts combination test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_preview_updates (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1692-1713)
        // Original: def test_utc_editor_preview_updates(qtbot, installation: HTInstallation, test_files_dir: Path): Test preview updates.
        [Fact]
        public void TestUtcEditorPreviewUpdates()
        {
            // TODO: STUB - Implement preview updates test (verifies preview widget updates when fields change)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1692-1713
            throw new NotImplementedException("TestUtcEditorPreviewUpdates: Preview updates test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_random_name_buttons (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1715-1745)
        // Original: def test_utc_editor_random_name_buttons(qtbot, installation: HTInstallation, test_files_dir: Path): Test random name buttons.
        [Fact]
        public void TestUtcEditorRandomNameButtons()
        {
            // TODO: STUB - Implement random name buttons test (firstname and lastname random generation)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1715-1745
            throw new NotImplementedException("TestUtcEditorRandomNameButtons: Random name buttons test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_inventory_button (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1747-1761)
        // Original: def test_utc_editor_inventory_button(qtbot, installation: HTInstallation, test_files_dir: Path): Test inventory button.
        [Fact]
        public void TestUtcEditorInventoryButton()
        {
            // TODO: STUB - Implement inventory button test (opens inventory dialog)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1747-1761
            throw new NotImplementedException("TestUtcEditorInventoryButton: Inventory button test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_menu_actions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1763-1792)
        // Original: def test_utc_editor_menu_actions(qtbot, installation: HTInstallation, test_files_dir: Path): Test menu actions.
        [Fact]
        public void TestUtcEditorMenuActions()
        {
            // TODO: STUB - Implement menu actions test (verifies menu actions exist and are callable)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1763-1792
            throw new NotImplementedException("TestUtcEditorMenuActions: Menu actions test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_comments_tab_title_update (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1794-1817)
        // Original: def test_utc_editor_comments_tab_title_update(qtbot, installation: HTInstallation, test_files_dir: Path): Test comments tab title updates.
        [Fact]
        public void TestUtcEditorCommentsTabTitleUpdate()
        {
            // TODO: STUB - Implement comments tab title update test (verifies tab title updates when comments change)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1794-1817
            throw new NotImplementedException("TestUtcEditorCommentsTabTitleUpdate: Comments tab title update test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_feat_summary_updates (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1819-1842)
        // Original: def test_utc_editor_feat_summary_updates(qtbot, installation: HTInstallation, test_files_dir: Path): Test feat summary updates.
        [Fact]
        public void TestUtcEditorFeatSummaryUpdates()
        {
            // TODO: STUB - Implement feat summary updates test (verifies feat summary widget updates when feats change)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1819-1842
            throw new NotImplementedException("TestUtcEditorFeatSummaryUpdates: Feat summary updates test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_power_summary_updates (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1844-1863)
        // Original: def test_utc_editor_power_summary_updates(qtbot, installation: HTInstallation, test_files_dir: Path): Test power summary updates.
        [Fact]
        public void TestUtcEditorPowerSummaryUpdates()
        {
            // TODO: STUB - Implement power summary updates test (verifies power summary widget updates when powers change)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1844-1863
            throw new NotImplementedException("TestUtcEditorPowerSummaryUpdates: Power summary updates test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_item_count_updates (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1865-1891)
        // Original: def test_utc_editor_item_count_updates(qtbot, installation: HTInstallation, test_files_dir: Path): Test item count updates.
        [Fact]
        public void TestUtcEditorItemCountUpdates()
        {
            // TODO: STUB - Implement item count updates test (verifies item count widget updates when inventory changes)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1865-1891
            throw new NotImplementedException("TestUtcEditorItemCountUpdates: Item count updates test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_widgets_exist (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1893-1996)
        // Original: def test_utc_editor_all_widgets_exist(qtbot, installation: HTInstallation): Test all widgets exist.
        [Fact]
        public void TestUtcEditorAllWidgetsExist()
        {
            // TODO: STUB - Implement all widgets exist test (verifies all UI widgets are accessible and exist)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1893-1996
            throw new NotImplementedException("TestUtcEditorAllWidgetsExist: All widgets exist test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_basic_widgets_interactions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1998-2053)
        // Original: def test_utc_editor_all_basic_widgets_interactions(qtbot, installation: HTInstallation): Test all basic widgets interactions.
        [Fact]
        public void TestUtcEditorAllBasicWidgetsInteractions()
        {
            // TODO: STUB - Implement all basic widgets interactions test (verifies all basic tab widgets can be interacted with)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:1998-2053
            throw new NotImplementedException("TestUtcEditorAllBasicWidgetsInteractions: All basic widgets interactions test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_advanced_widgets_interactions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2055-2115)
        // Original: def test_utc_editor_all_advanced_widgets_interactions(qtbot, installation: HTInstallation): Test all advanced widgets interactions.
        [Fact]
        public void TestUtcEditorAllAdvancedWidgetsInteractions()
        {
            // TODO: STUB - Implement all advanced widgets interactions test (verifies all advanced tab widgets can be interacted with)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2055-2115
            throw new NotImplementedException("TestUtcEditorAllAdvancedWidgetsInteractions: All advanced widgets interactions test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_stats_widgets_interactions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2117-2184)
        // Original: def test_utc_editor_all_stats_widgets_interactions(qtbot, installation: HTInstallation): Test all stats widgets interactions.
        [Fact]
        public void TestUtcEditorAllStatsWidgetsInteractions()
        {
            // TODO: STUB - Implement all stats widgets interactions test (verifies all stats tab widgets can be interacted with)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2117-2184
            throw new NotImplementedException("TestUtcEditorAllStatsWidgetsInteractions: All stats widgets interactions test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_classes_widgets_interactions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2186-2217)
        // Original: def test_utc_editor_all_classes_widgets_interactions(qtbot, installation: HTInstallation): Test all classes widgets interactions.
        [Fact]
        public void TestUtcEditorAllClassesWidgetsInteractions()
        {
            // TODO: STUB - Implement all classes widgets interactions test (verifies all classes tab widgets can be interacted with)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2186-2217
            throw new NotImplementedException("TestUtcEditorAllClassesWidgetsInteractions: All classes widgets interactions test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_feats_powers_widgets_interactions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2219-2269)
        // Original: def test_utc_editor_all_feats_powers_widgets_interactions(qtbot, installation: HTInstallation): Test all feats/powers widgets interactions.
        [Fact]
        public void TestUtcEditorAllFeatsPowersWidgetsInteractions()
        {
            // TODO: STUB - Implement all feats/powers widgets interactions test (verifies all feats/powers tab widgets can be interacted with)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2219-2269
            throw new NotImplementedException("TestUtcEditorAllFeatsPowersWidgetsInteractions: All feats/powers widgets interactions test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_scripts_widgets_interactions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2271-2302)
        // Original: def test_utc_editor_all_scripts_widgets_interactions(qtbot, installation: HTInstallation): Test all scripts widgets interactions.
        [Fact]
        public void TestUtcEditorAllScriptsWidgetsInteractions()
        {
            // TODO: STUB - Implement all scripts widgets interactions test (verifies all scripts tab widgets can be interacted with)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2271-2302
            throw new NotImplementedException("TestUtcEditorAllScriptsWidgetsInteractions: All scripts widgets interactions test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_all_widgets_build_verification (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2304-2411)
        // Original: def test_utc_editor_all_widgets_build_verification(qtbot, installation: HTInstallation): Test all widgets build verification.
        [Fact]
        public void TestUtcEditorAllWidgetsBuildVerification()
        {
            // TODO: STUB - Implement all widgets build verification test (verifies all widgets can be set and build() produces valid UTC)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2304-2411
            throw new NotImplementedException("TestUtcEditorAllWidgetsBuildVerification: All widgets build verification test not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_load_real_file (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2413-2433)
        // Original: def test_utc_editor_load_real_file(qtbot, installation: HTInstallation, test_files_dir): Test loading a real UTC file.
        [Fact]
        public void TestUtcEditorLoadRealFile()
        {
            // TODO: STUB - Implement load real file test (already exists as TestUtcEditorLoadExistingFile but needs full implementation matching Python)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2413-2433
            // Note: TestUtcEditorLoadExistingFile exists but is SIMPLIFIED - needs full field verification
        }

        // TODO: STUB - Implement test_utc_editor_menu_actions (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2435-2488)
        // Original: def test_utc_editor_menu_actions(qtbot, installation: HTInstallation): Test menu actions (duplicate name, different test).
        [Fact]
        public void TestUtcEditorMenuActions2()
        {
            // TODO: STUB - Implement menu actions test (duplicate name in Python, different implementation - verifies menu actions work correctly)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2435-2488
            throw new NotImplementedException("TestUtcEditorMenuActions2: Menu actions test (second implementation) not yet implemented");
        }

        // TODO: STUB - Implement test_utc_editor_inventory_button (vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2490-2544)
        // Original: def test_utc_editor_inventory_button(qtbot, installation: HTInstallation): Test inventory button (duplicate name, different test).
        [Fact]
        public void TestUtcEditorInventoryButton2()
        {
            // TODO: STUB - Implement inventory button test (duplicate name in Python, different implementation - verifies inventory dialog functionality)
            // Based on vendor/PyKotor/Tools/HolocronToolset/tests/gui/editors/test_utc_editor.py:2490-2544
            throw new NotImplementedException("TestUtcEditorInventoryButton2: Inventory button test (second implementation) not yet implemented");
        }
    }
}
