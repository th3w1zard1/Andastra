using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using HolocronToolset.NET;
using Xunit;

namespace HolocronToolset.NET.Tests.TestHelpers
{
    // Test fixture to initialize Avalonia for unit tests
    public class AvaloniaTestFixture : IDisposable
    {
        private static bool _initialized = false;
        private static readonly object _lock = new object();

        public AvaloniaTestFixture()
        {
            lock (_lock)
            {
                if (!_initialized)
                {
                    AppBuilder.Configure<App>()
                        .UseHeadless(new AvaloniaHeadlessPlatformOptions
                        {
                            UseHeadlessDrawing = true
                        })
                        .SetupWithoutStarting();
                    _initialized = true;
                }
            }
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    // XUnit collection fixture for Avalonia initialization
    [CollectionDefinition("Avalonia Test Collection")]
    public class AvaloniaTestCollection : ICollectionFixture<AvaloniaTestFixture>
    {
    }
}
