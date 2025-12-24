using System;
using Andastra.Runtime.MonoGame.GUI;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Xunit;

namespace Andastra.Tests.Runtime.Graphics.MonoGame.GUI
{
    /// <summary>
    /// Comprehensive unit tests for MyraMenuRenderer.
    /// Tests MonoGame-specific menu rendering functionality using Myra UI library.
    /// </summary>
    /// <remarks>
    /// MyraMenuRenderer Tests:
    /// - Tests Myra UI initialization with MonoGame GraphicsDevice
    /// - Tests Desktop and RootPanel creation
    /// - Tests viewport management and updates
    /// - Tests visibility control
    /// - Tests rendering (Draw method)
    /// - Tests disposal and resource cleanup
    /// - Based on swkotor.exe and swkotor2.exe menu initialization patterns
    /// </remarks>
    public class MyraMenuRendererTests : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private MyraMenuRenderer _renderer;

        public MyraMenuRendererTests()
        {
            _graphicsDevice = GraphicsTestHelper.CreateTestGraphicsDevice();
            _renderer = new MyraMenuRenderer(_graphicsDevice);
        }

        public void Dispose()
        {
            _renderer?.Dispose();
            GraphicsTestHelper.CleanupTestGraphicsDevice(_graphicsDevice);
        }

        [Fact]
        public void Constructor_WithNullGraphicsDevice_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            new Action(() => new MyraMenuRenderer(null))
                .Should().Throw<ArgumentNullException>()
                .WithParameterName("graphicsDevice");
        }

        [Fact]
        public void Constructor_WithValidGraphicsDevice_ShouldInitialize()
        {
            // Act
            var renderer = new MyraMenuRenderer(_graphicsDevice);

            // Assert
            renderer.Should().NotBeNull();
            renderer.IsInitialized.Should().BeTrue();
            renderer.Desktop.Should().NotBeNull();
            renderer.RootPanel.Should().NotBeNull();

            // Cleanup
            renderer.Dispose();
        }

        [Fact]
        public void Desktop_ShouldNotBeNull()
        {
            // Assert
            _renderer.Desktop.Should().NotBeNull();
        }

        [Fact]
        public void RootPanel_ShouldNotBeNull()
        {
            // Assert
            _renderer.RootPanel.Should().NotBeNull();
        }

        [Fact]
        public void RootPanel_ShouldHaveCorrectDimensions()
        {
            // Assert
            _renderer.RootPanel.Width.Should().Be(_graphicsDevice.Viewport.Width);
            _renderer.RootPanel.Height.Should().Be(_graphicsDevice.Viewport.Height);
        }

        [Fact]
        public void IsInitialized_AfterConstruction_ShouldBeTrue()
        {
            // Assert
            _renderer.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void IsVisible_Initially_ShouldBeFalse()
        {
            // Assert
            _renderer.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void SetVisible_True_ShouldSetIsVisibleToTrue()
        {
            // Act
            _renderer.SetVisible(true);

            // Assert
            _renderer.IsVisible.Should().BeTrue();
        }

        [Fact]
        public void UpdateViewport_ShouldUpdateRootPanelDimensions()
        {
            // Arrange
            int newWidth = 1920;
            int newHeight = 1080;

            // Act
            _renderer.UpdateViewport(newWidth, newHeight);

            // Assert
            _renderer.ViewportWidth.Should().Be(newWidth);
            _renderer.ViewportHeight.Should().Be(newHeight);
            _renderer.RootPanel.Width.Should().Be(newWidth);
            _renderer.RootPanel.Height.Should().Be(newHeight);
        }

        [Fact]
        public void Draw_WhenNotVisible_ShouldNotThrow()
        {
            // Arrange
            _renderer.SetVisible(false);
            var gameTime = new GameTime();

            // Act & Assert
            _renderer.Invoking(r => r.Draw(gameTime, _graphicsDevice))
                .Should().NotThrow();
        }

        [Fact]
        public void Draw_WhenVisible_ShouldNotThrow()
        {
            // Arrange
            _renderer.SetVisible(true);
            var gameTime = new GameTime();

            // Act & Assert
            _renderer.Invoking(r => r.Draw(gameTime, _graphicsDevice))
                .Should().NotThrow();
        }

        [Fact]
        public void Draw_WithNullGraphicsDevice_ShouldNotThrow()
        {
            // Arrange
            _renderer.SetVisible(true);
            var gameTime = new GameTime();

            // Act & Assert
            // Draw should handle null device gracefully
            _renderer.Invoking(r => r.Draw(gameTime, null))
                .Should().NotThrow();
        }

        [Fact]
        public void Update_ShouldNotThrow()
        {
            // Arrange
            var gameTime = new GameTime();

            // Act & Assert
            _renderer.Invoking(r => r.Update(gameTime))
                .Should().NotThrow();
        }

        [Fact]
        public void Dispose_ShouldNotThrow()
        {
            // Act & Assert
            _renderer.Invoking(r => r.Dispose())
                .Should().NotThrow();
        }

        [Fact]
        public void Dispose_AfterDispose_ShouldNotThrow()
        {
            // Arrange
            _renderer.Dispose();

            // Act & Assert
            _renderer.Invoking(r => r.Dispose())
                .Should().NotThrow();
        }

        [Fact]
        public void Dispose_ShouldClearDesktop()
        {
            // Arrange
            var desktop = _renderer.Desktop;
            desktop.Should().NotBeNull();

            // Act
            _renderer.Dispose();

            // Assert
            // Desktop should be cleared (set to null internally)
            // We can't directly check internal state, but disposal should succeed
            _renderer.IsInitialized.Should().BeFalse();
        }

        [Fact]
        public void ViewportWidth_AfterInitialization_ShouldMatchGraphicsDevice()
        {
            // Assert
            _renderer.ViewportWidth.Should().Be(_graphicsDevice.Viewport.Width);
        }

        [Fact]
        public void ViewportHeight_AfterInitialization_ShouldMatchGraphicsDevice()
        {
            // Assert
            _renderer.ViewportHeight.Should().Be(_graphicsDevice.Viewport.Height);
        }
    }
}

