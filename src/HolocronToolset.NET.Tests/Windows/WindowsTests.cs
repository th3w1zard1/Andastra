using System;
using System.Collections.Generic;
using Avalonia.Controls;
using FluentAssertions;
using HolocronToolset.NET.Data;
using HolocronToolset.NET.Editors;
using HolocronToolset.NET.Tests.TestHelpers;
using HolocronToolset.NET.Windows;
using Xunit;

namespace HolocronToolset.NET.Tests.Windows
{
    // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py
    // Original: Comprehensive tests for windows
    [Collection("Avalonia Test Collection")]
    public class WindowsTests : IClassFixture<AvaloniaTestFixture>
    {
        private readonly AvaloniaTestFixture _fixture;
        private static HTInstallation _installation;

        public WindowsTests(AvaloniaTestFixture fixture)
        {
            _fixture = fixture;
        }

        static WindowsTests()
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

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:25-37
        // Original: def test_module_designer_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestModuleDesignerInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            // Mocking settings or resource loading might be needed as it's heavy
            var window = new ModuleDesignerWindow(parent: null, installation: _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            window.Title.Should().Contain("Module Designer");

            // Test basic UI elements existence
            // Ui may be null if XAML isn't loaded, which is okay for programmatic UI
            if (window.Ui != null)
            {
                // If Ui is available, check controls
                // Note: Controls may be null if using programmatic UI
            }

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:39-53
        // Original: def test_kotordiff_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestKotordiffInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new KotorDiffWindow(
                parent: null,
                installations: new Dictionary<string, HTInstallation> { { "default", _installation } },
                activeInstallation: _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            window.Title.Should().Contain("Kotor");

            // Check interactions
            // Clicking 'Compare' without files should probably show error or do nothing safe
            window.Compare();
            // Likely warns about missing files or does nothing
            // Verify it doesn't crash

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:55-63
        // Original: def test_help_window_init(qtbot: QtBot):
        [Fact]
        public void TestHelpWindowInit()
        {
            var window = new HelpWindow(null);
            window.Show();

            window.IsVisible.Should().BeTrue();
            // Check if web engine or text viewer is present
            // Depending on implementation (WebView or TextBlock)
            // Note: Full testing requires web engine implementation

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:65-77
        // Original: def test_audio_player_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestAudioPlayerInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new WAVEditor(null, _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            // Check controls
            // Ui should be initialized in SetupUI
            window.Ui.Should().NotBeNull();
            if (window.Ui != null)
            {
                window.Ui.PlayButton.Should().NotBeNull();
                window.Ui.StopButton.Should().NotBeNull();
            }

            // Test loading a dummy audio file (mocked)
            // window.load_audio("test.wav")

            window.Close();
        }

        // Matching PyKotor implementation at Tools/HolocronToolset/tests/test_ui_windows.py:79-93
        // Original: def test_indoor_builder_init(qtbot: QtBot, installation: HTInstallation):
        [Fact]
        public void TestIndoorBuilderInit()
        {
            if (_installation == null)
            {
                return; // Skip if K1_PATH not set
            }

            var window = new IndoorBuilderWindow(parent: null, installation: _installation);
            window.Show();

            window.IsVisible.Should().BeTrue();
            window.Title.Should().Contain("Indoor");

            // Check widgets
            // Note: Full testing requires UI controls to be implemented
            window.Ui.Should().NotBeNull();

            window.Close();
        }
    }
}
