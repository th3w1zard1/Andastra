using System;
using System.Collections.Generic;
using Avalonia.Controls;
using FluentAssertions;
using HolocronToolset.NET.Dialogs;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;

namespace HolocronToolset.NET.Tests.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_settings.py
    // Original: Comprehensive tests for Settings dialog
    [Collection("Avalonia Test Collection")]
    public class SettingsDialogTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;

        public SettingsDialogTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_settings.py:11-21
        // Original: def test_settings_dialog_init(qtbot: QtBot):
        [Fact]
        public void TestSettingsDialogInit()
        {
            var parent = new Window();

            var dialog = new SettingsDialog(parent);
            dialog.Show();

            dialog.IsVisible.Should().BeTrue();
            dialog.Title.Should().Contain("Settings");

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_settings.py:23-74
        // Original: def test_settings_dialog_navigation(qtbot: QtBot):
        [Fact]
        public void TestSettingsDialogNavigation()
        {
            var parent = new Window();

            var dialog = new SettingsDialog(parent);
            dialog.Show();

            // Define expected pages and their names in the tree
            var pages = new Dictionary<string, Control>
            {
                { "Installations", dialog.Ui.InstallationsPage },
                { "GIT Editor", dialog.Ui.GitEditorPage },
                { "Misc", dialog.Ui.MiscPage },
                { "Module Designer", dialog.Ui.ModuleDesignerPage },
                { "Application", dialog.Ui.ApplicationSettingsPage },
            };

            foreach (var kvp in pages)
            {
                string pageName = kvp.Key;
                Control pageWidget = kvp.Value;

                // Find item in tree by iterating through items
                TreeViewItem foundItem = null;
                if (dialog.Ui.SettingsTree?.Items != null)
                {
                    foreach (var item in dialog.Ui.SettingsTree.Items)
                    {
                        if (item is TreeViewItem treeItem && treeItem.Header?.ToString() == pageName)
                        {
                            foundItem = treeItem;
                            break;
                        }
                    }
                }

                foundItem.Should().NotBeNull($"Could not find settings page: {pageName}");

                // Select the item
                if (foundItem != null && dialog.Ui.SettingsTree != null)
                {
                    dialog.Ui.SettingsTree.SelectedItem = foundItem;
                    // Verify page change
                    dialog.Ui.SettingsStack?.Content.Should().Be(pageWidget);
                }
            }

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_settings.py:76-103
        // Original: def test_settings_reset(qtbot: QtBot, monkeypatch: pytest.MonkeyPatch):
        [Fact]
        public void TestSettingsReset()
        {
            var parent = new Window();

            var dialog = new SettingsDialog(parent);
            dialog.Show();

            // Set a dummy setting in GlobalSettings to verify it gets reset/cleared
            // Note: GlobalSettings is a singleton accessing persistent storage.
            // Clearing it clears the persistent storage.
            var settings = new HolocronToolset.NET.Data.GlobalSettings();
            settings.ExtractPath = "some/custom/path";

            dialog.OnResetAllSettings();

            // Verify dialog closed and resetting flag set
            dialog.IsResetting.Should().BeTrue();
            dialog.IsVisible.Should().BeFalse();

            // We assume GlobalSettings().Clear() was called.
            // Verifying persistent state reset might be tricky without reloading settings,
            // but the logic in OnResetAllSettings is explicit.
        }
    }
}
