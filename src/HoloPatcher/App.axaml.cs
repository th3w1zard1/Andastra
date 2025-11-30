using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HoloPatcher.ViewModels;
using HoloPatcher.Views;

namespace HoloPatcher
{

    public partial class App : Application
    {
        private UpdateManager _updateManager;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                string title = $"HoloPatcher {Core.VersionLabel}";
                if (Core.IsAlphaVersion(Core.VersionLabel))
                {
                    title += " [ALPHA - NOT FOR PRODUCTION USE]";
                }
                desktop.MainWindow = new MainWindow
                {
                    Title = title,
                    DataContext = new MainWindowViewModel(),
                };

                // Initialize and start update manager on UI thread
                InitializeUpdateManager();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void InitializeUpdateManager()
        {
            try
            {
                _updateManager = new UpdateManager
                {
                    CheckOnStartup = true,
                    SilentCheck = true,
                    UseBetaChannel = false // Set to true to use beta channel
                };

                // Start update checking
                _updateManager.Start();
            }
            catch (System.Exception ex)
            {
                // Log error but don't crash the app if update system fails
                System.Diagnostics.Debug.WriteLine($"Failed to initialize update manager: {ex.Message}");
            }
        }

        protected override void OnExit(global::Avalonia.Controls.ApplicationLifetimes.IControlledApplicationLifetime appLifetime)
        {
            _updateManager?.Dispose();
            base.OnExit(appLifetime);
        }
    }
}

