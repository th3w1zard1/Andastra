// Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py:90-94
// Original: def kotordiff_cleanup_func(app: KotorDiffApp): ... app.root.destroy()
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace KotorDiff
{
    // Matching PyKotor implementation at vendor/PyKotor/Tools/KotorDiff/src/kotordiff/__main__.py
    // Original: class KotorDiffApp(ThemedApp): ... app.root.mainloop()
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new KotorDiffApp();
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

