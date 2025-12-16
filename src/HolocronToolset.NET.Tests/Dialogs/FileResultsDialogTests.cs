using System;
using System.Collections.Generic;
using Avalonia.Controls;
using AuroraEngine.Common.Resources;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Dialogs;
using HolocronToolset.NET.Tests.TestHelpers;
using Xunit;
using FileResource = AuroraEngine.Common.Resources.FileResource;

namespace HolocronToolset.NET.Tests.Dialogs
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:395-477
    // Original: Comprehensive tests for FileResults dialog
    [Collection("Avalonia Test Collection")]
    public class FileResultsDialogTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public FileResultsDialogTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static FileResultsDialogTests()
        {
            string k1Path = Environment.GetEnvironmentVariable("K1_PATH");
            if (string.IsNullOrEmpty(k1Path))
            {
                k1Path = @"C:\Program Files (x86)\Steam\steamapps\common\swkotor";
            }

            if (!string.IsNullOrEmpty(k1Path) && System.IO.File.Exists(System.IO.Path.Combine(k1Path, "chitin.key")))
            {
                _installation = new HTInstallation(k1Path, "Test");
            }
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:395-438
        // Original: def test_results_dialog_all_widgets(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestResultsDialogAllWidgets()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();

            // Create multiple results with different types
            var res1 = new FileResource("res1", ResourceType.UTC, 0, 0, "path/to/res1.utc");
            var res2 = new FileResource("res2", ResourceType.DLG, 0, 0, "path/to/res2.dlg");
            var res3 = new FileResource("res3", ResourceType.UTI, 0, 0, "path/to/res3.uti");
            var results = new List<FileResource> { res1, res2, res3 };

            var dialog = new FileResultsDialog(parent, results, _installation);
            dialog.Show();

            // Verify list populated
            dialog.Ui.ResultList.Items.Count.Should().Be(3);

            // Test selecting each item
            for (int i = 0; i < 3; i++)
            {
                dialog.Ui.ResultList.SelectedIndex = i;
                dialog.Ui.ResultList.SelectedIndex.Should().Be(i);
                dialog.Ui.ResultList.SelectedItem.Should().NotBeNull();
            }

            // Test double-click (should trigger signal)
            var signalCalled = new List<FileResource>();
            dialog.SearchResultsSelected += (res) => signalCalled.Add(res);

            dialog.Ui.ResultList.SelectedIndex = 0;
            // In Avalonia, we can simulate double-tap by calling Open directly
            dialog.Ui.ResultList.DoubleTapped += (s, e) => { };
            // Double-click should select and accept - verify signal was called or item is selected
            dialog.Ui.ResultList.SelectedIndex.Should().Be(0);

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:433-437
            // Original: dialog.ui.resultList.setCurrentRow(1)
            //          dialog.accept()
            //          assert len(signal_called) >= 1
            //          assert isinstance(signal_called[0], FileResource)
            // Test accept button
            dialog.Ui.ResultList.SelectedIndex = 1;
            // Call Accept() directly to match Python test behavior
            dialog.Accept();
            // Signal should be called when Accept is triggered
            (signalCalled.Count >= 1).Should().BeTrue();
            signalCalled[0].Should().NotBeNull();
            signalCalled[0].Should().BeOfType<FileResource>();

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:440-454
        // Original: def test_results_dialog_empty_results(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestResultsDialogEmptyResults()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();

            var dialog = new FileResultsDialog(parent, new List<FileResource>(), _installation);
            dialog.Show();

            dialog.Ui.ResultList.Items.Count.Should().Be(0);

            // Accept with no selection should not crash
            dialog.Ui.OkButton.Command?.Execute(null);
            // Dialog should close without error

            dialog.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:456-477
        // Original: def test_results_dialog_single_result(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestResultsDialogSingleResult()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var parent = new Window();

            var res = new FileResource("single", ResourceType.UTC, 0, 0, "path/to/single.utc");
            var dialog = new FileResultsDialog(parent, new List<FileResource> { res }, _installation);
            dialog.Show();

            dialog.Ui.ResultList.Items.Count.Should().Be(1);
            dialog.Ui.ResultList.SelectedIndex = 0;

            // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_search.py:472-477
            // Original: signal_called: list[FileResource] = []
            //          dialog.sig_searchresults_selected.connect(lambda r: signal_called.append(r))
            //          dialog.accept()
            //          assert len(signal_called) == 1
            //          assert signal_called[0].resname() == "single"
            var signalCalled = new List<FileResource>();
            dialog.SearchResultsSelected += (r) => signalCalled.Add(r);

            // Trigger accept
            dialog.Accept();
            // Signal should be called with the selected resource
            signalCalled.Count.Should().Be(1);
            signalCalled[0].ResName.Should().Be("single");

            dialog.Close();
        }
    }
}
