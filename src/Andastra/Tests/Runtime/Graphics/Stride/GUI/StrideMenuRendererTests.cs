using System;
using Stride.Graphics;
using Stride.Core.Mathematics;
using Stride.Engine;
using Andastra.Runtime.Stride.GUI;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
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
        // Integration tests would require full Stride game setup.
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
    }
}

