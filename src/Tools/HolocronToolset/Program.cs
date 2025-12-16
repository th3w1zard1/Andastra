using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.ReactiveUI;
using HolocronToolset.Windows;

namespace HolocronToolset.NET
{
    // Matching PyKotor implementation at Tools/HolocronToolset/src/toolset/__main__.py:43
    // Original: if __name__ == "__main__": main_init(); main()
    internal class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace()
                .UseReactiveUI();
    }
}
