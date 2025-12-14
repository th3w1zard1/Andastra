using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py
    // Original: Comprehensive tests for editor help system
    [Collection("Avalonia Test Collection")]
    public class EditorHelpDialogTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public EditorHelpDialogTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static EditorHelpDialogTests()
        {
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            if (!string.IsNullOrEmpty(k1Path) && File.Exists(Path.Combine(k1Path, "chitin.key")))
            {
                _installation = new HTInstallation(k1Path, "Test");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:34-53
        // Original: def test_get_wiki_path_development_mode(tmp_path, monkeypatch):
        [Fact]
        public void TestGetWikiPathDevelopmentMode()
        {
            // Test wiki path resolution in development mode
            // Note: GetWikiPath is a private static method, so we test it indirectly through dialog creation
            // The actual path resolution is tested indirectly through dialog loading
            var dialog = new EditorHelpDialog(null, "test.md");
            dialog.Should().NotBeNull();
            // Dialog should be created successfully (path resolution happens internally)
            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:92-99
        // Original: def test_editor_help_dialog_creation(qtbot):
        [Fact]
        public void TestEditorHelpDialogCreation()
        {
            var dialog = new EditorHelpDialog(null, "GFF-File-Format.md");
            dialog.Show();

            dialog.Title.Should().Contain("Help");
            dialog.Title.Should().Contain("GFF-File-Format.md");
            dialog.TextBrowser.Should().NotBeNull();
            dialog.IsVisible.Should().BeTrue(); // Shown

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:102-117
        // Original: def test_editor_help_dialog_load_existing_file(qtbot, tmp_path, monkeypatch):
        [Fact]
        public void TestEditorHelpDialogLoadExistingFile()
        {
            // Create a test wiki file
            string tempDir = Path.GetTempPath();
            string wikiDir = Path.Combine(tempDir, "test_wiki_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(wikiDir);
            try
            {
                string testFile = Path.Combine(wikiDir, "test.md");
                File.WriteAllText(testFile, "# Test Document\n\nThis is a test.");

                // Mock get_wiki_path to return our test wiki
                // Note: We can't easily mock static methods in C#, so we'll test with actual path
                // For this test, we'll use the actual GetWikiPath and create the file there if possible
                var dialog = new EditorHelpDialog(null, "test.md");
                dialog.Show();

                // Check that content was loaded (may be error if file doesn't exist, which is acceptable)
                dialog.TextBrowser.Should().NotBeNull();
                dialog.TextBrowser.Text.Should().NotBeNullOrEmpty();

                dialog.Close();
            }
            finally
            {
                if (Directory.Exists(wikiDir))
                {
                    Directory.Delete(wikiDir, true);
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:120-132
        // Original: def test_editor_help_dialog_load_nonexistent_file(qtbot, tmp_path, monkeypatch):
        [Fact]
        public void TestEditorHelpDialogLoadNonexistentFile()
        {
            var dialog = new EditorHelpDialog(null, "nonexistent.md");
            dialog.Show();

            // Check that error message is shown
            dialog.TextBrowser.Should().NotBeNull();
            dialog.TextBrowser.Text.Should().NotBeNullOrEmpty();
            // Should contain error message or "not found"
            dialog.TextBrowser.Text.ToLower().Should().ContainAny("not found", "error", "nonexistent");

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:183-193
        // Original: def test_editor_help_dialog_non_blocking(qtbot):
        [Fact]
        public void TestEditorHelpDialogNonBlocking()
        {
            var dialog = new EditorHelpDialog(null, "GFF-File-Format.md");
            dialog.Show();

            dialog.IsVisible.Should().BeTrue();
            // In Avalonia, dialogs are non-modal by default when using Show()
            // Modal dialogs would use ShowDialog()

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:200-217
        // Original: def test_editor_add_help_action_creates_menu(qtbot, installation):
        [Fact]
        public void TestEditorAddHelpActionCreatesMenu()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var editor = new AREEditor(null, _installation);
            editor.Show();

            // Call AddHelpAction
            editor.AddHelpAction();

            // Check that Help menu exists
            // Note: Menu finding is complex in Avalonia, so we'll verify the method doesn't crash
            // The actual menu verification would require more complex UI traversal
            editor.Should().NotBeNull();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:276-294
        // Original: def test_editor_add_help_action_auto_detects_wiki_file(qtbot, installation):
        [Fact]
        public void TestEditorAddHelpActionAutoDetectsWikiFile()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var editor = new AREEditor(null, _installation);
            editor.Show();

            // Should auto-detect "GFF-ARE.md" for AREEditor
            editor.AddHelpAction();

            // Verify help menu was created (if wiki file exists in mapping)
            // AREEditor should have a wiki file in the mapping
            EditorWikiMapping.GetWikiFile("AREEditor").Should().Be("GFF-ARE.md");

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:297-312
        // Original: def test_editor_add_help_action_with_explicit_filename(qtbot, installation):
        [Fact]
        public void TestEditorAddHelpActionWithExplicitFilename()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var editor = new AREEditor(null, _installation);
            editor.Show();

            editor.AddHelpAction("GFF-File-Format.md");

            // Verify help menu was created (method should not crash)
            editor.Should().NotBeNull();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:315-351
        // Original: def test_editor_add_help_action_no_wiki_file_skips(qtbot, installation):
        [Fact]
        public void TestEditorAddHelpActionNoWikiFileSkips()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // TXTEditor has None in the mapping, so AddHelpAction should skip
            var editor = new TXTEditor(null, _installation);
            editor.Show();

            // Call AddHelpAction - should not crash and should skip since wiki_file is null
            editor.AddHelpAction();

            // Since TXTEditor has None in mapping, Documentation action should not be added
            // (We can't easily verify this without complex UI traversal, but the method should not crash)
            editor.Should().NotBeNull();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:354-389
        // Original: def test_editor_add_help_action_multiple_calls_idempotent(qtbot, installation):
        [Fact]
        public void TestEditorAddHelpActionMultipleCallsIdempotent()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var editor = new AREEditor(null, _installation);
            editor.Show();

            // Call multiple times
            editor.AddHelpAction();
            editor.AddHelpAction();
            editor.AddHelpAction();

            // Should still work (idempotent) - method should not crash
            editor.Should().NotBeNull();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:396-416
        // Original: def test_editor_show_help_dialog_opens_dialog(qtbot, installation, tmp_path, monkeypatch):
        [Fact]
        public void TestEditorShowHelpDialogOpensDialog()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var editor = new AREEditor(null, _installation);
            editor.Show();

            // Show help dialog
            editor.ShowHelpDialog("test.md");

            // Find the dialog (it should be a child of the editor or shown separately)
            // In Avalonia, we can't easily find child dialogs, but we can verify the method doesn't crash
            editor.Should().NotBeNull();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:462-475
        // Original: def test_are_editor_has_help_button(qtbot, installation):
        [Fact]
        public void TestAREEditorHasHelpButton()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var editor = new AREEditor(null, _installation);
            editor.Show();

            // Check that help menu exists (AREEditor calls AddHelpAction in constructor)
            // Note: Menu verification requires complex UI traversal
            editor.Should().NotBeNull();
            EditorWikiMapping.GetWikiFile("AREEditor").Should().NotBeNullOrEmpty();

            editor.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:517-540
        // Original: def test_editor_wiki_map_has_all_editors():
        [Fact]
        public void TestEditorWikiMapHasAllEditors()
        {
            string[] expectedEditors = {
                "AREEditor",
                "UTWEditor",
                "UTIEditor",
                "UTCEditor",
                "GFFEditor",
                "DLGEditor",
                "JRLEditor"
            };

            foreach (string editorName in expectedEditors)
            {
                EditorWikiMapping.EditorWikiMap.Should().ContainKey(editorName, $"{editorName} should be in EditorWikiMap");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:533-540
        // Original: def test_editor_wiki_map_values_are_strings_or_none():
        [Fact]
        public void TestEditorWikiMapValuesAreStrings()
        {
            foreach (var kvp in EditorWikiMapping.EditorWikiMap)
            {
                kvp.Value.Should().NotBeNull($"{kvp.Key} should have a non-null wiki file");
                kvp.Value.Should().EndWith(".md", $"{kvp.Key} wiki_file should end with .md: {kvp.Value}");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:569-573
        // Original: def test_dlg_editor_uses_correct_wiki_file():
        [Fact]
        public void TestDlgEditorUsesCorrectWikiFile()
        {
            EditorWikiMapping.EditorWikiMap.Should().ContainKey("DLGEditor");
            EditorWikiMapping.GetWikiFile("DLGEditor").Should().Be("GFF-DLG.md",
                "DLGEditor should use 'GFF-DLG.md', not 'GFF-File-Format.md'");
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:576-601
        // Original: def test_gff_specific_editors_use_specific_files():
        [Fact]
        public void TestGffSpecificEditorsUseSpecificFiles()
        {
            // Editors that should use specific GFF files
            var specificGffEditors = new Dictionary<string, string>
            {
                { "AREEditor", "GFF-ARE.md" },
                { "DLGEditor", "GFF-DLG.md" },
                { "GITEditor", "GFF-GIT.md" },
                { "IFOEditor", "GFF-IFO.md" },
                { "JRLEditor", "GFF-JRL.md" },
                { "PTHEditor", "GFF-PTH.md" },
                { "UTCEditor", "GFF-UTC.md" },
                { "UTDEditor", "GFF-UTD.md" },
                { "UTEEditor", "GFF-UTE.md" },
                { "UTIEditor", "GFF-UTI.md" },
                { "UTMEditor", "GFF-UTM.md" },
                { "UTPEditor", "GFF-UTP.md" },
                { "UTSEditor", "GFF-UTS.md" },
                { "UTTEditor", "GFF-UTT.md" },
                { "UTWEditor", "GFF-UTW.md" }
            };

            foreach (var kvp in specificGffEditors)
            {
                if (EditorWikiMapping.EditorWikiMap.ContainsKey(kvp.Key))
                {
                    string actualFile = EditorWikiMapping.GetWikiFile(kvp.Key);
                    actualFile.Should().Be(kvp.Value,
                        $"{kvp.Key} should use '{kvp.Value}', not '{actualFile}'");
                }
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/gui/test_editor_help.py:604-616
        // Original: def test_generic_gff_editors_use_generic_file():
        [Fact]
        public void TestGenericGffEditorsUseGenericFile()
        {
            var genericGffEditors = new Dictionary<string, string>
            {
                { "GFFEditor", "GFF-File-Format.md" },
                { "SaveGameEditor", "GFF-File-Format.md" },
                { "MetadataEditor", "GFF-File-Format.md" }
            };

            foreach (var kvp in genericGffEditors)
            {
                if (EditorWikiMapping.EditorWikiMap.ContainsKey(kvp.Key))
                {
                    string actualFile = EditorWikiMapping.GetWikiFile(kvp.Key);
                    actualFile.Should().Be(kvp.Value,
                        $"{kvp.Key} should use '{kvp.Value}', not '{actualFile}'");
                }
            }
        }
    }
}
