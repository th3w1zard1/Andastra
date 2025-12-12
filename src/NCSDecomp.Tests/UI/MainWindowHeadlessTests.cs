using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using CSharpKOTOR.Formats.NCS.NCSDecomp;
using FluentAssertions;
using Xunit;
using File = CSharpKOTOR.Formats.NCS.NCSDecomp.File;

namespace NCSDecomp.Tests.UI
{
    public class MainWindowHeadlessTests : IDisposable
    {
        private readonly MainWindow _window;
        private readonly string _tempDir;

        public MainWindowHeadlessTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempDir);

            // Initialize settings
            Decompiler.settings = new Settings();
            Decompiler.settings.SetProperty("Output Directory", _tempDir);
            Decompiler.settings.SetProperty("Game Type", "K1");

            // Create minimal nwscript.nss
            string nwscriptPath = Path.Combine(_tempDir, "nwscript.nss");
            System.IO.File.WriteAllText(nwscriptPath, "// 0\nvoid ActionTest(int nAction);\n");
            Decompiler.settings.SetProperty("NWScript Path", nwscriptPath);

            // Initialize Avalonia headless before creating window
            if (Avalonia.Application.Current == null)
            {
                Avalonia.AppBuilder.Configure<NCSDecomp.App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }

            _window = new MainWindow();
        }

        public void Dispose()
        {
            _window?.Close();
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void MainWindow_Constructor_ShouldNotThrow()
        {
            // Arrange - Initialize Avalonia headless using AppBuilder if not already initialized
            if (Avalonia.Application.Current == null)
            {
                Avalonia.AppBuilder.Configure<NCSDecomp.App>()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions())
                    .SetupWithoutStarting();
            }

            // Act
            Action act = () => new MainWindow();

            // Assert
            act.Should().NotThrow();
        }
    }
}

