using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace HolocronToolset.NET.Editors
{
    // Helper class for editor initialization
    public static class EditorHelpers
    {
        public static void SafeInitializeComponent(Window window, Action fallbackSetup)
        {
            try
            {
                AvaloniaXamlLoader.Load(window);
            }
            catch
            {
                // XAML not available - use fallback setup
                fallbackSetup?.Invoke();
            }
        }
    }
}
