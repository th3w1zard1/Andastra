using System;
using System.Collections.Generic;
using System.IO;
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
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py
    // Original: Comprehensive tests for UTE Editor
    [Collection("Avalonia Test Collection")]
    public class UTEEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTEEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        // Helper method to get test file path
        private string GetTestFilePath(string filename)
        {
            string testFilesDir = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
            testFilesDir = Path.GetFullPath(testFilesDir);

            string filePath = Path.Combine(testFilesDir, filename);
            if (File.Exists(filePath))
            {
                return filePath;
            }

            // Try alternative location
            testFilesDir = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
            testFilesDir = Path.GetFullPath(testFilesDir);
            filePath = Path.Combine(testFilesDir, filename);

            return File.Exists(filePath) ? filePath : null;
        }

        // Helper method to get installation
        private HTInstallation GetInstallation()
        {
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            if (Directory.Exists(k1Path) && File.Exists(Path.Combine(k1Path, "chitin.key")))
            {
                return new HTInstallation(k1Path, "Test Installation", tsl: false);
            }

            // Fallback to K2
            string k2Path = Environment.GetEnvironmentVariable("K2_PATH");
            if (string.IsNullOrEmpty(k2Path))
            {
                k2Path = @"C:\Program Files (x86)\Steam\steamapps\common\Knights of the Old Republic II";
            }

            if (Directory.Exists(k2Path) && File.Exists(Path.Combine(k2Path, "chitin.key")))
            {
                return new HTInstallation(k2Path, "Test Installation", tsl: true);
            }

            return null;
        }

        // Helper method to access private field via reflection
        private T GetPrivateField<T>(object obj, string fieldName)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                return default(T);
            }
            return (T)field.GetValue(obj);
        }

        // Helper method to set private field via reflection
        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }

        [Fact]
        public void TestUteEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:1006-1037
            // Original: def test_ute_editor_new_file_creation(qtbot, installation):
            var editor = new UTEEditor(null, null);

            editor.New();

            // Verify UTE object exists
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            var gff = GFF.FromBytes(data);
            gff.Should().NotBeNull();
        }

        [Fact]
        public void TestUteEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:32-44
            // Original: def test_ute_editor_initialization(qtbot, installation):
            var editor = new UTEEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestUteEditorLoadExistingFile()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:24-49
            // Original: def test_ute_editor_manipulate_name_locstring
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                // Skip if test file not found
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            // Verify editor loaded the data
            editor.Should().NotBeNull();

            // Build and verify it works
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);

            // Verify we can read it back
            var gff = GFF.FromBytes(data);
            gff.Should().NotBeNull();

            // Verify we can parse it as UTE
            var ute = UTEHelpers.ConstructUte(gff);
            ute.Should().NotBeNull();
        }

        [Fact]
        public void TestUteEditorManipulateTag()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:51-75
            // Original: def test_ute_editor_manipulate_tag
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var originalGff = GFF.FromBytes(originalData);
            var originalUte = UTEHelpers.ConstructUte(originalGff);

            // Access private field via reflection
            var tagEdit = GetPrivateField<TextBox>(editor, "_tagEdit");
            if (tagEdit != null)
            {
                tagEdit.Text = "modified_tag";

                // Save and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);

                modifiedUte.Tag.Should().Be("modified_tag");
                modifiedUte.Tag.Should().NotBe(originalUte.Tag);

                // Load back and verify
                editor.Load(uteFile, resref, ResourceType.UTE, data);
                tagEdit.Text.Should().Be("modified_tag");
            }
        }

        [Fact]
        public void TestUteEditorManipulateResref()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:77-99
            // Original: def test_ute_editor_manipulate_resref
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var resrefEdit = GetPrivateField<TextBox>(editor, "_resrefEdit");
            if (resrefEdit != null)
            {
                resrefEdit.Text = "modified_resref";

                // Save and verify
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);

                modifiedUte.ResRef.ToString().Should().Be("modified_resref");

                // Load back and verify
                editor.Load(uteFile, resref, ResourceType.UTE, data);
                resrefEdit.Text.Should().Be("modified_resref");
            }
        }

        [Fact]
        public void TestUteEditorManipulateSpawnSelect()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:127-150
            // Original: def test_ute_editor_manipulate_spawn_select
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var spawnSelect = GetPrivateField<ComboBox>(editor, "_spawnSelect");
            if (spawnSelect != null && spawnSelect.Items.Count >= 2)
            {
                // Test both options: 0 = Single Shot, 1 = Continuous
                for (int i = 0; i < 2; i++)
                {
                    spawnSelect.SelectedIndex = i;

                    // Save and verify
                    var (data, _) = editor.Build();
                    var modifiedGff = GFF.FromBytes(data);
                    var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);

                    // Python: single_shot = bool(i), so i=0 -> False, i=1 -> True
                    modifiedUte.SingleShot.Should().Be(i == 1);

                    // Load back and verify
                    editor.Load(uteFile, resref, ResourceType.UTE, data);
                    spawnSelect.SelectedIndex.Should().Be(i);
                }
            }
        }

        [Fact]
        public void TestUteEditorManipulateCreatureCounts()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:152-178
            // Original: def test_ute_editor_manipulate_creature_counts
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var minCreatureSpin = GetPrivateField<NumericUpDown>(editor, "_minCreatureSpin");
            var maxCreatureSpin = GetPrivateField<NumericUpDown>(editor, "_maxCreatureSpin");

            if (minCreatureSpin != null)
            {
                // Test min creature count
                int[] testMinValues = { 1, 2, 3, 5, 10 };
                foreach (int val in testMinValues)
                {
                    minCreatureSpin.Value = val;
                    var (data, _) = editor.Build();
                    var modifiedGff = GFF.FromBytes(data);
                    var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                    modifiedUte.RecCreatures.Should().Be(val);
                }
            }

            if (maxCreatureSpin != null)
            {
                // Test max creature count
                int[] testMaxValues = { 1, 3, 5, 10, 20 };
                foreach (int val in testMaxValues)
                {
                    maxCreatureSpin.Value = val;
                    var (data, _) = editor.Build();
                    var modifiedGff = GFF.FromBytes(data);
                    var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                    modifiedUte.MaxCreatures.Should().Be(val);
                }
            }
        }

        [Fact]
        public void TestUteEditorManipulateActiveCheckbox()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:184-205
            // Original: def test_ute_editor_manipulate_active_checkbox
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var activeCheckbox = GetPrivateField<CheckBox>(editor, "_activeCheckbox");
            if (activeCheckbox != null)
            {
                // Toggle checkbox
                activeCheckbox.IsChecked = true;
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                modifiedUte.Active.Should().BeTrue();

                activeCheckbox.IsChecked = false;
                (data, _) = editor.Build();
                modifiedGff = GFF.FromBytes(data);
                modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                modifiedUte.Active.Should().BeFalse();
            }
        }

        [Fact]
        public void TestUteEditorManipulatePlayerOnlyCheckbox()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:207-228
            // Original: def test_ute_editor_manipulate_player_only_checkbox
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var playerOnlyCheckbox = GetPrivateField<CheckBox>(editor, "_playerOnlyCheckbox");
            if (playerOnlyCheckbox != null)
            {
                // Toggle checkbox
                playerOnlyCheckbox.IsChecked = true;
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                modifiedUte.PlayerOnly.Should().NotBe(0);

                playerOnlyCheckbox.IsChecked = false;
                (data, _) = editor.Build();
                modifiedGff = GFF.FromBytes(data);
                modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                modifiedUte.PlayerOnly.Should().Be(0);
            }
        }

        [Fact]
        public void TestUteEditorManipulateRespawnsCheckbox()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:252-273
            // Original: def test_ute_editor_manipulate_respawns_checkbox
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var respawnsCheckbox = GetPrivateField<CheckBox>(editor, "_respawnsCheckbox");
            if (respawnsCheckbox != null)
            {
                // Toggle checkbox
                respawnsCheckbox.IsChecked = true;
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                modifiedUte.Reset.Should().NotBe(0);

                respawnsCheckbox.IsChecked = false;
                (data, _) = editor.Build();
                modifiedGff = GFF.FromBytes(data);
                modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                modifiedUte.Reset.Should().Be(0);
            }
        }

        [Fact]
        public void TestUteEditorManipulateInfiniteRespawnCheckbox()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:275-300
            // Original: def test_ute_editor_manipulate_infinite_respawn_checkbox
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var infiniteRespawnCheckbox = GetPrivateField<CheckBox>(editor, "_infiniteRespawnCheckbox");
            var respawnCountSpin = GetPrivateField<NumericUpDown>(editor, "_respawnCountSpin");

            if (infiniteRespawnCheckbox != null && respawnCountSpin != null)
            {
                // Enable infinite respawn
                infiniteRespawnCheckbox.IsChecked = true;

                // Verify respawn count spin is disabled and value is -1
                respawnCountSpin.IsEnabled.Should().BeFalse();
                respawnCountSpin.Value.Should().Be(-1);

                // Disable infinite respawn
                infiniteRespawnCheckbox.IsChecked = false;

                // Verify respawn count spin is enabled
                respawnCountSpin.IsEnabled.Should().BeTrue();
            }
        }

        [Fact]
        public void TestUteEditorManipulateRespawnCounts()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:302-332
            // Original: def test_ute_editor_manipulate_respawn_counts
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var infiniteRespawnCheckbox = GetPrivateField<CheckBox>(editor, "_infiniteRespawnCheckbox");
            var respawnCountSpin = GetPrivateField<NumericUpDown>(editor, "_respawnCountSpin");
            var respawnTimeSpin = GetPrivateField<NumericUpDown>(editor, "_respawnTimeSpin");

            // Disable infinite respawn first
            if (infiniteRespawnCheckbox != null)
            {
                infiniteRespawnCheckbox.IsChecked = false;
            }

            if (respawnCountSpin != null)
            {
                // Test respawn count
                int[] testValues = { 0, 1, 5, 10, 50 };
                foreach (int val in testValues)
                {
                    respawnCountSpin.Value = val;
                    var (data, _) = editor.Build();
                    var modifiedGff = GFF.FromBytes(data);
                    var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                    modifiedUte.Respawns.Should().Be(val);
                }
            }

            if (respawnTimeSpin != null)
            {
                // Test respawn time
                int[] testTimeValues = { 0, 1, 5, 10, 60 };
                foreach (int val in testTimeValues)
                {
                    respawnTimeSpin.Value = val;
                    var (data, _) = editor.Build();
                    var modifiedGff = GFF.FromBytes(data);
                    var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                    modifiedUte.ResetTime.Should().Be(val);
                }
            }
        }

        [Fact]
        public void TestUteEditorAddCreature()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:338-363
            // Original: def test_ute_editor_add_creature
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            // Get initial creature count from the UTE
            var initialGff = GFF.FromBytes(originalData);
            var initialUte = UTEHelpers.ConstructUte(initialGff);
            int initialCount = initialUte.Creatures.Count;

            // Use reflection to call AddCreature method
            var addCreatureMethod = editor.GetType().GetMethod("AddCreature", BindingFlags.NonPublic | BindingFlags.Instance);
            if (addCreatureMethod != null)
            {
                addCreatureMethod.Invoke(editor, new object[] { "test_creature", 1, 2.0f, false });

                // Verify creature was added
                var (data, _) = editor.Build();
                var modifiedGff = GFF.FromBytes(data);
                var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                modifiedUte.Creatures.Count.Should().BeGreaterThanOrEqualTo(initialCount + 1);
                if (modifiedUte.Creatures.Count > 0)
                {
                    modifiedUte.Creatures[modifiedUte.Creatures.Count - 1].ResRef.ToString().Should().Be("test_creature");
                }
            }
        }

        [Fact]
        public void TestUteEditorManipulateComments()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:605-636
            // Original: def test_ute_editor_manipulate_comments
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var commentsEdit = GetPrivateField<TextBox>(editor, "_commentsEdit");
            if (commentsEdit != null)
            {
                string[] testComments = {
                    "",
                    "Test comment",
                    "Multi\nline\ncomment",
                    "Comment with special chars !@#$%^&*()",
                    new string('A', 1000) // Very long comment
                };

                foreach (string comment in testComments)
                {
                    commentsEdit.Text = comment;

                    // Save and verify
                    var (data, _) = editor.Build();
                    var modifiedGff = GFF.FromBytes(data);
                    var modifiedUte = UTEHelpers.ConstructUte(modifiedGff);
                    modifiedUte.Comment.Should().Be(comment);

                    // Load back and verify
                    editor.Load(uteFile, resref, ResourceType.UTE, data);
                    commentsEdit.Text.Should().Be(comment);
                }
            }
        }

        [Fact]
        public void TestUteEditorSaveLoadRoundtrip()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:711-788
            // Original: def test_ute_editor_save_load_roundtrip_identity
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var originalGff = GFF.FromBytes(originalData);
            var originalUte = UTEHelpers.ConstructUte(originalGff);

            // Save without modifications
            var (data, _) = editor.Build();
            var savedGff = GFF.FromBytes(data);
            var savedUte = UTEHelpers.ConstructUte(savedGff);

            // Verify key fields match
            savedUte.Tag.Should().Be(originalUte.Tag);
            savedUte.ResRef.ToString().Should().Be(originalUte.ResRef.ToString());
            savedUte.DifficultyId.Should().Be(originalUte.DifficultyId);
            savedUte.Active.Should().Be(originalUte.Active);

            // Load saved data back
            editor.Load(uteFile, resref, ResourceType.UTE, data);

            // Verify UI matches
            var tagEdit = GetPrivateField<TextBox>(editor, "_tagEdit");
            var resrefEdit = GetPrivateField<TextBox>(editor, "_resrefEdit");
            if (tagEdit != null && resrefEdit != null)
            {
                tagEdit.Text.Should().Be(originalUte.Tag);
                resrefEdit.Text.Should().Be(originalUte.ResRef.ToString());
            }
        }

        [Fact]
        public void TestUteEditorContinuousRespawn()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_ute_editor.py:1189-1218
            // Original: def test_uteeditor_continuous_respawn
            string uteFile = GetTestFilePath("newtransition.ute");
            if (uteFile == null)
            {
                return;
            }

            HTInstallation installation = GetInstallation();
            byte[] originalData = File.ReadAllBytes(uteFile);
            string resref = Path.GetFileNameWithoutExtension(uteFile);

            var editor = new UTEEditor(null, installation);
            editor.Load(uteFile, resref, ResourceType.UTE, originalData);

            var spawnSelect = GetPrivateField<ComboBox>(editor, "_spawnSelect");
            var respawnsCheckbox = GetPrivateField<CheckBox>(editor, "_respawnsCheckbox");
            var infiniteRespawnCheckbox = GetPrivateField<CheckBox>(editor, "_infiniteRespawnCheckbox");
            var respawnCountSpin = GetPrivateField<NumericUpDown>(editor, "_respawnCountSpin");
            var respawnTimeSpin = GetPrivateField<NumericUpDown>(editor, "_respawnTimeSpin");

            if (spawnSelect != null && respawnsCheckbox != null && infiniteRespawnCheckbox != null &&
                respawnCountSpin != null && respawnTimeSpin != null)
            {
                // Set to continuous spawn
                spawnSelect.SelectedIndex = 1; // Continuous

                // Verify respawn fields are enabled
                respawnsCheckbox.IsEnabled.Should().BeTrue();
                infiniteRespawnCheckbox.IsEnabled.Should().BeTrue();
                respawnCountSpin.IsEnabled.Should().BeTrue();
                respawnTimeSpin.IsEnabled.Should().BeTrue();

                // Set back to single shot
                spawnSelect.SelectedIndex = 0; // Single shot

                // Verify respawn fields are disabled
                respawnsCheckbox.IsEnabled.Should().BeFalse();
                infiniteRespawnCheckbox.IsEnabled.Should().BeFalse();
                respawnCountSpin.IsEnabled.Should().BeFalse();
                respawnTimeSpin.IsEnabled.Should().BeFalse();
            }
        }
    }
}
