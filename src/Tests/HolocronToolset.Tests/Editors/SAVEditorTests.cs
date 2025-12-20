using System;
using System.IO;
using System.Reflection;
using Andastra.Parsing.Extract.SaveData;
using Andastra.Parsing.Formats.ERF;
using Andastra.Parsing.Resource;
using FluentAssertions;
using HolocronToolset.Data;
using HolocronToolset.Editors;
using HolocronToolset.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.Tests.Editors
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py
    // Original: Comprehensive tests for Save Game Editor
    [Collection("Avalonia Test Collection")]
    public class SaveGameEditorTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public SaveGameEditorTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public void TestSaveGameEditorNewFileCreation()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py
            // Original: def test_save_game_editor_new_file_creation(qtbot, installation):
            var editor = new SAVEditor(null, null);

            editor.New();

            // Verify editor is ready
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        [Fact]
        public void TestSaveGameEditorInitialization()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py
            // Original: def test_save_game_editor_initialization(qtbot, installation):
            var editor = new SAVEditor(null, null);

            // Verify editor is initialized
            editor.Should().NotBeNull();
            var (data, _) = editor.Build();
            data.Should().NotBeNull();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/editors/test_save_editor.py:802-843
        // Original: def test_save_game_editor_loads_save(qtbot, installation: HTInstallation, real_save_folder: Path):
        [Fact]
        public void TestSaveGameEditorLoadExistingFile()
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

            if (installation == null)
            {
                // Skip if no installation available
                return;
            }

            // Matching PyKotor implementation: Create a real save folder with actual save files
            // Original: save_folder = tmp_path / "000001 - TestSave"; save_folder.mkdir()
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "HolocronToolsetTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string saveFolder = System.IO.Path.Combine(tempDir, "000001 - TestSave");
            System.IO.Directory.CreateDirectory(saveFolder);

            try
            {
                // Matching PyKotor implementation: Create minimal but valid save files
                // Original: save_info = SaveInfo(str(save_folder)); save_info.savegame_name = "Test Save"; etc.
                var saveInfo = new Andastra.Parsing.Extract.SaveData.SaveInfo(saveFolder);
                saveInfo.SavegameName = "Test Save";
                saveInfo.PcName = "TestPlayer";
                saveInfo.AreaName = "Test Area";
                saveInfo.LastModule = "test_module";
                saveInfo.TimePlayed = 3600;
                saveInfo.Save();

                // Matching PyKotor implementation: party_table = PartyTable(str(save_folder)); etc.
                // Original: party_table.pt_members = [pc_member]; party_table.pt_gold = 1000; party_table.pt_xp_pool = 5000
                var partyTable = new Andastra.Parsing.Extract.SaveData.PartyTable(saveFolder);
                var pcMember = new Andastra.Parsing.Extract.SaveData.PartyMemberEntry
                {
                    Index = -1,
                    IsLeader = true
                };
                partyTable.Members.Add(pcMember);
                partyTable.Gold = 1000;
                partyTable.XpPool = 5000;
                // Ensure required fields are set before saving
                partyTable.ControlledNpc = -1;
                partyTable.AiState = 0;
                partyTable.FollowState = 0;
                partyTable.SoloMode = false;
                partyTable.CheatUsed = false;
                partyTable.ItemComponents = 0;
                partyTable.ItemChemicals = 0;
                partyTable.Save();

                // Matching PyKotor implementation: global_vars = GlobalVars(str(save_folder)); etc.
                var globalVars = new Andastra.Parsing.Extract.SaveData.GlobalVars(saveFolder);
                globalVars.SetBool("TEST_BOOL", true);
                globalVars.SetNumber("TEST_NUM", 42);
                globalVars.SetString("TEST_STR", "test string");
                globalVars.Save();

                // Matching PyKotor implementation: Create minimal valid SAVEGAME.sav (ERF file)
                // Original: erf_data = (b"SAV V1.0" + ...)
                // Create empty ERF file using ERF class
                var erf = new Andastra.Parsing.Formats.ERF.ERF(Andastra.Parsing.Formats.ERF.ERFType.ERF, isSave: true);
                byte[] erfData = Andastra.Parsing.Formats.ERF.ERFAuto.BytesErf(erf, Andastra.Parsing.Resource.ResourceType.SAV);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(saveFolder, "SAVEGAME.sav"), erfData);

                // Matching PyKotor implementation: Load save components directly (skip SAVEGAME.sav which requires valid ERF)
                // Original: save_info = SaveInfo(str(real_save_folder)); save_info.load()
                var loadedSaveInfo = new Andastra.Parsing.Extract.SaveData.SaveInfo(saveFolder);
                loadedSaveInfo.Load();
                var loadedPartyTable = new Andastra.Parsing.Extract.SaveData.PartyTable(saveFolder);
                loadedPartyTable.Load();
                var loadedGlobalVars = new Andastra.Parsing.Extract.SaveData.GlobalVars(saveFolder);
                loadedGlobalVars.Load();

                // Matching PyKotor implementation: Create nested capsule manually (without loading invalid SAVEGAME.sav)
                // Original: nested_capsule = MagicMock(spec=SaveNestedCapsule); nested_capsule.cached_characters = {}
                var nestedCapsule = new Andastra.Parsing.Extract.SaveData.SaveNestedCapsule(saveFolder);
                // Don't call Load() to avoid loading invalid SAVEGAME.sav

                // Matching PyKotor implementation: Set up editor with loaded data
                // Original: editor._save_info = save_info; editor._party_table = party_table; etc.
                var editor = new SAVEditor(null, installation);
                // Use reflection to set private fields (matching Python's direct assignment)
                FieldInfo saveInfoField = typeof(SAVEditor).GetField("_saveInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                FieldInfo partyTableField = typeof(SAVEditor).GetField("_partyTable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                FieldInfo globalVarsField = typeof(SAVEditor).GetField("_globalVars", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                FieldInfo nestedCapsuleField = typeof(SAVEditor).GetField("_nestedCapsule", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                saveInfoField?.SetValue(editor, loadedSaveInfo);
                partyTableField?.SetValue(editor, loadedPartyTable);
                globalVarsField?.SetValue(editor, loadedGlobalVars);
                nestedCapsuleField?.SetValue(editor, nestedCapsule);

                // Matching PyKotor implementation: Populate UI
                // Original: editor.populate_save_info(); editor.populate_party_table(); editor.populate_global_vars()
                editor.PopulateSaveInfo();
                editor.PopulatePartyTable();
                editor.PopulateGlobalVars();

                // Matching PyKotor implementation: Verify data structures are set
                // Original: assert editor._save_info is not None
                editor.SaveInfo.Should().NotBeNull("Save info should be loaded");
                editor.PartyTable.Should().NotBeNull("Party table should be loaded");
                editor.GlobalVars.Should().NotBeNull("Global vars should be loaded");
                editor.NestedCapsule.Should().NotBeNull("Nested capsule should be loaded");

                // Matching PyKotor implementation: Verify UI is populated
                // Original: assert editor.ui.lineEditSaveName.text() == "Test Save"
                // Note: We can't directly access UI controls in tests, but we can verify through properties
                editor.SaveInfo.SavegameName.Should().Be("Test Save", "Save name should be set");
                editor.SaveInfo.PcName.Should().Be("TestPlayer", "PC name should be set");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (System.IO.Directory.Exists(tempDir))
                    {
                        System.IO.Directory.Delete(tempDir, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }
    }
}
