using System;
using Andastra.Runtime.Stride.GUI;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Xunit;

namespace Andastra.Tests.Runtime.Stride.GUI
{
    /// <summary>
    /// Comprehensive unit tests for StrideMenuRenderer.
    /// Tests Stride-specific menu rendering functionality using SpriteBatch.
    /// </summary>
    /// <remarks>
    /// StrideMenuRenderer Tests:
    /// - Tests Stride SpriteBatch initialization
    /// - Tests white texture creation for drawing
    /// - Tests viewport management and updates
    /// - Tests visibility control
    /// - Tests rendering (Draw method)
    /// - Tests disposal and resource cleanup
    /// - Based on swkotor.exe and swkotor2.exe menu initialization patterns
    /// 
    /// Note: Stride tests require actual Stride GraphicsDevice, which may not be available in test environment.
    /// These tests are designed to work with mocked or headless Stride setup.
    /// </remarks>
    public class StrideMenuRendererTests
    {
        [Fact]
        public void Constructor_WithNullGraphicsDevice_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            new Action(() => new StrideMenuRenderer(null))
                .Should().Throw<ArgumentNullException>()
                .WithParameterName("graphicsDevice");
        }

        [Fact]
        public void Constructor_WithNullGraphicsDeviceAndFont_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            new Action(() => new StrideMenuRenderer(null, null))
                .Should().Throw<ArgumentNullException>()
                .WithParameterName("graphicsDevice");
        }

        // Note: Full Stride tests require actual Stride GraphicsDevice initialization
        // which may not be available in headless test environment.
        // These tests validate the constructor and basic functionality.
        // Tests use GraphicsTestHelper.CreateTestStrideGraphicsDevice() which gracefully handles
        // cases where GraphicsDevice creation fails (e.g., no GPU in headless CI environments).

        [Fact]
        public void IsInitialized_AfterConstruction_ShouldBeTrue()
        {
            // Arrange - Create Stride GraphicsDevice for testing using helper method
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                // Act - Create StrideMenuRenderer with GraphicsDevice
                var renderer = new StrideMenuRenderer(graphicsDevice);

                // Assert - Renderer should be initialized after construction
                renderer.IsInitialized.Should().BeTrue("StrideMenuRenderer should be initialized after successful construction");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void IsVisible_Initially_ShouldBeFalse()
        {
            // Arrange - Create Stride GraphicsDevice for testing using helper method
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                // Act - Create StrideMenuRenderer with GraphicsDevice
                var renderer = new StrideMenuRenderer(graphicsDevice);

                // Assert - Menu should not be visible initially
                renderer.IsVisible.Should().BeFalse("StrideMenuRenderer should not be visible initially (default state is hidden)");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void SetVisible_True_ShouldSetIsVisibleToTrue()
        {
            // Arrange - Create Stride GraphicsDevice for testing using helper method
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.IsVisible.Should().BeFalse("Initial state should be invisible");

                // Act - Set visibility to true
                renderer.SetVisible(true);

                // Assert - Menu should now be visible
                renderer.IsVisible.Should().BeTrue("StrideMenuRenderer should be visible after SetVisible(true)");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void UpdateViewport_ShouldUpdateViewport()
        {
            // Arrange - Create Stride GraphicsDevice for testing using helper method
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                var initialWidth = renderer.ViewportWidth;
                var initialHeight = renderer.ViewportHeight;

                // Act - Update viewport to new dimensions
                int newWidth = 1920;
                int newHeight = 1080;
                renderer.UpdateViewport(newWidth, newHeight);

                // Assert - Viewport dimensions should be updated
                renderer.ViewportWidth.Should().Be(newWidth, "Viewport width should be updated after UpdateViewport");
                renderer.ViewportHeight.Should().Be(newHeight, "Viewport height should be updated after UpdateViewport");
                renderer.ViewportWidth.Should().NotBe(initialWidth, "Viewport width should have changed");
                renderer.ViewportHeight.Should().NotBe(initialHeight, "Viewport height should have changed");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void Draw_WhenNotVisible_ShouldNotThrow()
        {
            // Arrange - Create Stride GraphicsDevice for testing using helper method
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.IsVisible.Should().BeFalse("Menu should not be visible initially");

                // Act & Assert - Draw should not throw when menu is not visible
                // Draw() should return early when IsVisible is false, so it should not throw
                renderer.Invoking(r => r.Draw()).Should().NotThrow("Draw should not throw when menu is not visible (should return early)");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange - Create Stride GraphicsDevice for testing using helper method
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.IsInitialized.Should().BeTrue("Renderer should be initialized before disposal");

                // Act & Assert - Dispose should not throw
                renderer.Invoking(r => r.Dispose()).Should().NotThrow("Dispose should not throw when called on initialized renderer");

                // Assert - After disposal, renderer should no longer be initialized
                renderer.IsInitialized.Should().BeFalse("Renderer should not be initialized after disposal");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        #region Integration Tests

        /// <summary>
        /// Integration tests for StrideMenuRenderer with full Stride game setup.
        /// These tests simulate a complete game loop with Update/Draw cycles,
        /// viewport changes, and resource lifecycle management.
        /// </summary>
        /// <remarks>
        /// Integration Test Requirements:
        /// - Full Stride Game instance with GraphicsDevice
        /// - Multiple Update/Draw cycles to simulate game loop
        /// - Viewport changes during runtime
        /// - Resource lifecycle (creation, use, disposal)
        /// - Error handling during full game loop
        /// - Multiple visibility toggles during game loop
        /// 
        /// Based on swkotor.exe and swkotor2.exe menu rendering patterns:
        /// - swkotor2.exe: FUN_006d2350 @ 0x006d2350 (menu constructor/initializer)
        /// - swkotor.exe: FUN_0067c4c0 @ 0x0067c4c0 (menu constructor/initializer)
        /// - Menu rendering occurs in main game loop with Update/Draw cycles
        /// - Viewport changes handled during window resize events
        /// - Menu visibility toggled based on game state
        /// </remarks>

        [Fact]
        public void Integration_FullGameLoop_WithMultipleUpdateDrawCycles_ShouldRenderSuccessfully()
        {
            // Arrange - Create full Stride Game instance for integration testing
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.IsInitialized.Should().BeTrue("Renderer should be initialized");

                // Act - Simulate full game loop with multiple Update/Draw cycles
                // This tests the complete rendering pipeline in a realistic scenario
                const int frameCount = 10;
                const float deltaTime = 1.0f / 60.0f; // 60 FPS simulation

                for (int frame = 0; frame < frameCount; frame++)
                {
                    // Update phase (simulating game loop update)
                    renderer.Update(deltaTime);

                    // Render phase (simulating game loop draw)
                    // Set visible on frame 2 to test rendering
                    if (frame == 2)
                    {
                        renderer.SetVisible(true);
                    }

                    // Draw should not throw during game loop
                    renderer.Invoking(r => r.Draw()).Should().NotThrow(
                        $"Draw should not throw during game loop frame {frame}");

                    // Hide on frame 7 to test visibility toggle
                    if (frame == 7)
                    {
                        renderer.SetVisible(false);
                    }
                }

                // Assert - Renderer should still be initialized after game loop
                renderer.IsInitialized.Should().BeTrue("Renderer should remain initialized after game loop");
                renderer.IsVisible.Should().BeFalse("Menu should be hidden after game loop");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void Integration_ViewportChangesDuringRuntime_ShouldUpdateCorrectly()
        {
            // Arrange - Create full Stride Game instance for integration testing
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.SetVisible(true);

                // Act - Simulate viewport changes during runtime (window resize)
                // This tests viewport update handling in a realistic game scenario
                var viewportSizes = new[]
                {
                    new { Width = 1280, Height = 720 },
                    new { Width = 1920, Height = 1080 },
                    new { Width = 2560, Height = 1440 },
                    new { Width = 1024, Height = 768 },
                    new { Width = 1280, Height = 720 }
                };

                foreach (var size in viewportSizes)
                {
                    // Update viewport (simulating window resize)
                    renderer.UpdateViewport(size.Width, size.Height);

                    // Assert - Viewport should be updated
                    renderer.ViewportWidth.Should().Be(size.Width,
                        $"Viewport width should be updated to {size.Width}");
                    renderer.ViewportHeight.Should().Be(size.Height,
                        $"Viewport height should be updated to {size.Height}");

                    // Draw should work correctly with new viewport
                    renderer.Invoking(r => r.Draw()).Should().NotThrow(
                        $"Draw should not throw with viewport {size.Width}x{size.Height}");
                }

                // Assert - Final viewport should match last update
                renderer.ViewportWidth.Should().Be(1280, "Final viewport width should be 1280");
                renderer.ViewportHeight.Should().Be(720, "Final viewport height should be 720");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void Integration_MultipleVisibilityToggles_ShouldWorkCorrectly()
        {
            // Arrange - Create full Stride Game instance for integration testing
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.IsVisible.Should().BeFalse("Menu should not be visible initially");

                // Act - Toggle visibility multiple times during game loop
                // This tests visibility state management in a realistic scenario
                const int toggleCount = 5;
                const float deltaTime = 1.0f / 60.0f;

                for (int i = 0; i < toggleCount; i++)
                {
                    // Toggle visibility
                    bool shouldBeVisible = (i % 2 == 0);
                    renderer.SetVisible(shouldBeVisible);

                    // Assert - Visibility should match expected state
                    renderer.IsVisible.Should().Be(shouldBeVisible,
                        $"Menu visibility should be {shouldBeVisible} after toggle {i}");

                    // Update and draw should work regardless of visibility
                    renderer.Update(deltaTime);
                    renderer.Invoking(r => r.Draw()).Should().NotThrow(
                        $"Draw should not throw after visibility toggle {i}");
                }

                // Assert - Final state should be visible (last toggle was true)
                renderer.IsVisible.Should().BeTrue("Menu should be visible after final toggle");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void Integration_ResourceLifecycle_WithFullGameLoop_ShouldCleanupCorrectly()
        {
            // Arrange - Create full Stride Game instance for integration testing
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                StrideMenuRenderer renderer = null;

                try
                {
                    // Act - Create renderer and use it in game loop
                    renderer = new StrideMenuRenderer(graphicsDevice);
                    renderer.IsInitialized.Should().BeTrue("Renderer should be initialized");

                    // Simulate game loop usage
                    const int frameCount = 5;
                    const float deltaTime = 1.0f / 60.0f;

                    for (int frame = 0; frame < frameCount; frame++)
                    {
                        renderer.Update(deltaTime);
                        renderer.SetVisible(frame % 2 == 0);
                        renderer.Draw();
                    }

                    // Assert - Renderer should still be initialized after use
                    renderer.IsInitialized.Should().BeTrue("Renderer should remain initialized after game loop");
                }
                finally
                {
                    // Act - Dispose renderer (simulating game shutdown)
                    if (renderer != null)
                    {
                        renderer.Dispose();
                    }
                }

                // Assert - Renderer should be disposed
                if (renderer != null)
                {
                    renderer.IsInitialized.Should().BeFalse("Renderer should not be initialized after disposal");
                }
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void Integration_ExtendedGameLoop_WithContinuousRendering_ShouldMaintainStability()
        {
            // Arrange - Create full Stride Game instance for integration testing
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.SetVisible(true);

                // Act - Simulate extended game loop (stress test)
                // This tests stability over many frames
                const int frameCount = 100;
                const float deltaTime = 1.0f / 60.0f;

                for (int frame = 0; frame < frameCount; frame++)
                {
                    // Update phase
                    renderer.Update(deltaTime);

                    // Toggle visibility every 10 frames to test state changes
                    if (frame % 10 == 0)
                    {
                        renderer.SetVisible(!renderer.IsVisible);
                    }

                    // Draw phase
                    renderer.Invoking(r => r.Draw()).Should().NotThrow(
                        $"Draw should not throw during extended game loop at frame {frame}");

                    // Update viewport every 25 frames to test viewport changes
                    if (frame % 25 == 0)
                    {
                        int newWidth = 1280 + (frame % 500);
                        int newHeight = 720 + (frame % 300);
                        renderer.UpdateViewport(newWidth, newHeight);
                    }
                }

                // Assert - Renderer should still be initialized after extended game loop
                renderer.IsInitialized.Should().BeTrue(
                    "Renderer should remain initialized after extended game loop");
                renderer.ViewportWidth.Should().BeGreaterThan(0,
                    "Viewport width should be valid after extended game loop");
                renderer.ViewportHeight.Should().BeGreaterThan(0,
                    "Viewport height should be valid after extended game loop");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        [Fact]
        public void Integration_ConcurrentUpdateDraw_WithViewportChanges_ShouldHandleCorrectly()
        {
            // Arrange - Create full Stride Game instance for integration testing
            var game = GraphicsTestHelper.CreateTestStrideGame();
            if (game == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                var graphicsDevice = game.GraphicsDevice;
                if (graphicsDevice == null)
                {
                    return;
                }

                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.SetVisible(true);

                // Act - Simulate realistic game loop with concurrent operations
                // This tests handling of multiple operations happening in sequence
                const int frameCount = 20;
                const float deltaTime = 1.0f / 60.0f;

                for (int frame = 0; frame < frameCount; frame++)
                {
                    // Update phase
                    renderer.Update(deltaTime);

                    // Simulate viewport change during update (window resize event)
                    if (frame == 5)
                    {
                        renderer.UpdateViewport(1920, 1080);
                    }

                    // Simulate visibility toggle during update (menu open/close event)
                    if (frame == 10)
                    {
                        renderer.SetVisible(false);
                    }

                    if (frame == 15)
                    {
                        renderer.SetVisible(true);
                    }

                    // Draw phase (should handle all state changes correctly)
                    renderer.Invoking(r => r.Draw()).Should().NotThrow(
                        $"Draw should not throw with concurrent operations at frame {frame}");
                }

                // Assert - Renderer should be in correct final state
                renderer.IsInitialized.Should().BeTrue("Renderer should remain initialized");
                renderer.IsVisible.Should().BeTrue("Menu should be visible after final toggle");
                renderer.ViewportWidth.Should().Be(1920, "Viewport width should match last update");
                renderer.ViewportHeight.Should().Be(1080, "Viewport height should match last update");
            }
            finally
            {
                // Cleanup - Dispose of Game instance (which will clean up GraphicsDevice)
                GraphicsTestHelper.CleanupTestStrideGame(game);
            }
        }

        #endregion
    }
}

