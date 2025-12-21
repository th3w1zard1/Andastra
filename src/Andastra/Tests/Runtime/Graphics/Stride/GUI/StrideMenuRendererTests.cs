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
            // Arrange
            Game game;
            var graphicsDevice = CreateTestGraphicsDeviceOrSkip(out game);
            if (graphicsDevice == null)
            {
                // Skip test if GraphicsDevice creation fails (e.g., no GPU in headless CI environment)
                return;
            }

            try
            {
                // Act
                var renderer = new StrideMenuRenderer(graphicsDevice);

                // Assert
                renderer.IsInitialized.Should().BeTrue("StrideMenuRenderer should be initialized after successful construction");
            }
            finally
            {
                // Cleanup
                try
                {
                    renderer?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                try
                {
                    game?.Dispose();
                }
                catch
                {
                    // Ignore cleanup errors
                }
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
            // Arrange
            var graphicsDevice = CreateTestGraphicsDeviceOrSkip();
            if (graphicsDevice == null)
            {
                // Skip test if GraphicsDevice creation fails
                return;
            }

            try
            {
                var renderer = new StrideMenuRenderer(graphicsDevice);
                int initialWidth = renderer.ViewportWidth;
                int initialHeight = renderer.ViewportHeight;

                // Act
                int newWidth = 1920;
                int newHeight = 1080;
                renderer.UpdateViewport(newWidth, newHeight);

                // Assert
                renderer.ViewportWidth.Should().Be(newWidth, "Viewport width should be updated");
                renderer.ViewportHeight.Should().Be(newHeight, "Viewport height should be updated");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (graphicsDevice != null && graphicsDevice.Game != null)
                    {
                        graphicsDevice.Game.Dispose();
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void Draw_WhenNotVisible_ShouldNotThrow()
        {
            // Arrange
            var graphicsDevice = CreateTestGraphicsDeviceOrSkip();
            if (graphicsDevice == null)
            {
                // Skip test if GraphicsDevice creation fails
                return;
            }

            try
            {
                var renderer = new StrideMenuRenderer(graphicsDevice);
                renderer.IsVisible.Should().BeFalse("Renderer should not be visible initially");

                // Act & Assert
                var drawAction = new Action(() => renderer.Draw());
                drawAction.Should().NotThrow("Draw should not throw when renderer is not visible");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (graphicsDevice != null && graphicsDevice.Game != null)
                    {
                        graphicsDevice.Game.Dispose();
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Arrange
            var graphicsDevice = CreateTestGraphicsDeviceOrSkip();
            if (graphicsDevice == null)
            {
                // Skip test if GraphicsDevice creation fails
                return;
            }

            try
            {
                var renderer = new StrideMenuRenderer(graphicsDevice);

                // Act & Assert
                var disposeAction = new Action(() => renderer.Dispose());
                disposeAction.Should().NotThrow("Dispose should not throw");

                // Verify disposed state
                renderer.IsInitialized.Should().BeFalse("IsInitialized should be false after disposal");
            }
            finally
            {
                // Cleanup
                try
                {
                    if (graphicsDevice != null && graphicsDevice.Game != null)
                    {
                        graphicsDevice.Game.Dispose();
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        /// <summary>
        /// Creates a Stride GraphicsDevice for testing using GraphicsTestHelper, or skips the test if creation fails.
        /// </summary>
        /// <param name="game">Output parameter that receives the Game instance for proper cleanup.</param>
        /// <returns>GraphicsDevice instance, or null if creation fails (test should be skipped).</returns>
        /// <remarks>
        /// Stride GraphicsDevice Creation for Tests:
        /// - Uses GraphicsTestHelper.CreateTestStrideGame() to create a minimal Game instance
        /// - Returns null if device creation fails (e.g., no GPU in headless CI environments)
        /// - Sets the out parameter to the Game instance so tests can properly dispose it
        /// - Tests should check for null and return early if device creation fails
        /// - Tests should dispose the Game instance in a finally block for proper resource cleanup
        /// </remarks>
        private GraphicsDevice CreateTestGraphicsDeviceOrSkip(out Game game)
        {
            game = GraphicsTestHelper.CreateTestStrideGame();
            if (game != null && game.GraphicsDevice != null)
            {
                return game.GraphicsDevice;
            }
            
            // If creation failed, ensure game is null and return null
            if (game != null)
            {
                try
                {
                    game.Dispose();
                }
                catch
                {
                    // Ignore disposal errors during cleanup
                }
                game = null;
            }
            
            return null;
        }
    }
}

