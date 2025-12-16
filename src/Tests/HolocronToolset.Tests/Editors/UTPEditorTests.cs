using System;
using System.Collections.Generic;
using Andastra.Parsing;
using Andastra.Parsing.Formats.GFF;
using Andastra.Parsing.Resource.Generics;
using Andastra.Parsing.Resources;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py
    // Original: Comprehensive tests for UTP Editor
    [Collection("Avalonia Test Collection")]
    public class UTPEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public UTPEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestUtpEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py
            // Original: def test_utp_editor_new_file_creation(qtbot, installation):
            var editor = new UTPEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
            data.Length.Should().BeGreaterThan(0);
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:24-50
        // Original: def test_utp_editor_manipulate_name_locstring(qtbot, installation: HTInstallation, test_files_dir: Path):
        // Note: This test loads an existing UTP file and verifies it works, similar to UTI/UTC test patterns
        [Fact]
        public void TestUtpEditorLoadExistingFile()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find ebcont001.utp (used in Python tests)
            string utpFile = System.IO.Path.Combine(testFilesDir, "ebcont001.utp");
            if (!System.IO.File.Exists(utpFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utpFile = System.IO.Path.Combine(testFilesDir, "ebcont001.utp");
            }

            if (!System.IO.File.Exists(utpFile))
            {
                // Skip if no UTP files available for testing (matching Python pytest.skip behavior)
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

            var editor = new UTPEditor(null, installation);

            byte[] originalData = System.IO.File.ReadAllBytes(utpFile);
            editor.Load(utpFile, "ebcont001", ResourceType.UTP, originalData);

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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:1194-1265
        // Original: def test_utp_editor_gff_roundtrip_comparison(qtbot, installation: HTInstallation, test_files_dir: Path):
        [Fact]
        public void TestUtpEditorSaveLoadRoundtrip()
        {
            // Get test files directory
            string testFilesDir = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");

            // Try to find ebcont001.utp
            string utpFile = System.IO.Path.Combine(testFilesDir, "ebcont001.utp");
            if (!System.IO.File.Exists(utpFile))
            {
                // Try alternative location
                testFilesDir = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                    "..", "..", "..", "..", "..", "vendor", "PyKotor", "Tools", "HolocronToolset", "tests", "test_files");
                utpFile = System.IO.Path.Combine(testFilesDir, "ebcont001.utp");
            }

            if (!System.IO.File.Exists(utpFile))
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

            var editor = new UTPEditor(null, installation);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:1210
            // Original: original_data = utp_file.read_bytes()
            byte[] data = System.IO.File.ReadAllBytes(utpFile);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:1211
            // Original: original_utp = read_utp(original_data)
            var originalGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(data);
            UTP originalUtp = UTPHelpers.ConstructUtp(originalGff);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:1212
            // Original: editor.load(utp_file, "ebcont001", ResourceType.UTP, original_data)
            editor.Load(utpFile, "ebcont001", ResourceType.UTP, data);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:1215
            // Original: data, _ = editor.build()
            var (newData, _) = editor.Build();

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:1216
            // Original: new_utp = read_utp(data)
            GFF newGff = Andastra.Parsing.Formats.GFF.GFF.FromBytes(newData);
            UTP newUtp = UTPHelpers.ConstructUtp(newGff);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_utp_editor.py:1221-1264
            // Original: assert new_utp.tag == original_utp.tag ... (many field comparisons)
            // Note: Python test does functional UTP comparison rather than raw GFF comparison
            // because dismantle_utp always writes all fields (including deprecated ones)
            newUtp.Tag.Should().Be(originalUtp.Tag);
            newUtp.ResRef.ToString().Should().Be(originalUtp.ResRef.ToString());
            newUtp.AppearanceId.Should().Be(originalUtp.AppearanceId);
            newUtp.Conversation.ToString().Should().Be(originalUtp.Conversation.ToString());
            newUtp.HasInventory.Should().Be(originalUtp.HasInventory);
            newUtp.Useable.Should().Be(originalUtp.Useable);
            newUtp.Plot.Should().Be(originalUtp.Plot);
            newUtp.Static.Should().Be(originalUtp.Static);
            newUtp.Min1Hp.Should().Be(originalUtp.Min1Hp);
            newUtp.PartyInteract.Should().Be(originalUtp.PartyInteract);
            newUtp.NotBlastable.Should().Be(originalUtp.NotBlastable);
            newUtp.FactionId.Should().Be(originalUtp.FactionId);
            newUtp.AnimationState.Should().Be(originalUtp.AnimationState);
            newUtp.CurrentHp.Should().Be(originalUtp.CurrentHp);
            newUtp.MaximumHp.Should().Be(originalUtp.MaximumHp);
            newUtp.Hardness.Should().Be(originalUtp.Hardness);
            newUtp.Fortitude.Should().Be(originalUtp.Fortitude);
            newUtp.Reflex.Should().Be(originalUtp.Reflex);
            newUtp.Will.Should().Be(originalUtp.Will);
            newUtp.Locked.Should().Be(originalUtp.Locked);
            newUtp.UnlockDc.Should().Be(originalUtp.UnlockDc);
            newUtp.UnlockDiff.Should().Be(originalUtp.UnlockDiff);
            newUtp.UnlockDiffMod.Should().Be(originalUtp.UnlockDiffMod);
            newUtp.KeyRequired.Should().Be(originalUtp.KeyRequired);
            newUtp.AutoRemoveKey.Should().Be(originalUtp.AutoRemoveKey);
            newUtp.KeyName.Should().Be(originalUtp.KeyName);
            newUtp.OnClosed.ToString().Should().Be(originalUtp.OnClosed.ToString());
            newUtp.OnDamaged.ToString().Should().Be(originalUtp.OnDamaged.ToString());
            newUtp.OnDeath.ToString().Should().Be(originalUtp.OnDeath.ToString());
            newUtp.OnHeartbeat.ToString().Should().Be(originalUtp.OnHeartbeat.ToString());
            newUtp.OnLock.ToString().Should().Be(originalUtp.OnLock.ToString());
            newUtp.OnMelee.ToString().Should().Be(originalUtp.OnMelee.ToString());
            newUtp.OnOpen.ToString().Should().Be(originalUtp.OnOpen.ToString());
            newUtp.OnPower.ToString().Should().Be(originalUtp.OnPower.ToString());
            newUtp.OnUnlock.ToString().Should().Be(originalUtp.OnUnlock.ToString());
            newUtp.OnUserDefined.ToString().Should().Be(originalUtp.OnUserDefined.ToString());
            newUtp.OnEndDialog.ToString().Should().Be(originalUtp.OnEndDialog.ToString());
            newUtp.OnInventory.ToString().Should().Be(originalUtp.OnInventory.ToString());
            newUtp.OnUsed.ToString().Should().Be(originalUtp.OnUsed.ToString());
            newUtp.OnOpenFailed.ToString().Should().Be(originalUtp.OnOpenFailed.ToString());
            newUtp.Comment.Should().Be(originalUtp.Comment);
            newUtp.Inventory.Count.Should().Be(originalUtp.Inventory.Count);
            for (int i = 0; i < newUtp.Inventory.Count && i < originalUtp.Inventory.Count; i++)
            {
                newUtp.Inventory[i].ResRef.ToString().Should().Be(originalUtp.Inventory[i].ResRef.ToString());
                newUtp.Inventory[i].Droppable.Should().Be(originalUtp.Inventory[i].Droppable);
            }
        }
    }
}
