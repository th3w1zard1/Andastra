using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HolocronToolset.NET.Windows;

namespace HolocronToolset.NET
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:208
    // Original: def main(): app = QApplication(sys.argv)
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/__main__.py:44
            // Original: main_init()
            MainInit.Initialize();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:217
            // Original: setup_pre_init_settings()
            MainSettings.SetupPreInitSettings();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:269
                // Original: if is_running_from_temp():
                if (MainInit.IsRunningFromTemp())
                {
                    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:270-275
                    // Original: QMessageBox.critical(...); sys.exit(...)
                    throw new InvalidOperationException(
                        "This application cannot be run from within a zip or temporary directory. " +
                        "Please extract it to a permanent location before running.");
                }

                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:278
                // Original: tool_window = ToolWindow()
                desktop.MainWindow = new MainWindow();
                
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:281
                // Original: tool_window.show()
                desktop.MainWindow.Show();
                
                // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:284
                // Original: tool_window.update_manager.check_for_updates(silent=True)
                if (desktop.MainWindow is MainWindow mainWindow)
                {
                    mainWindow.UpdateManager?.CheckForUpdates(silent: true);
                }
            }

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:266
            // Original: setup_post_init_settings()
            MainSettings.SetupPostInitSettings();

            // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/main_app.py:267
            // Original: setup_toolset_default_env()
            MainSettings.SetupToolsetDefaultEnv();

            base.OnFrameworkInitializationCompleted();
        }
    }
}
