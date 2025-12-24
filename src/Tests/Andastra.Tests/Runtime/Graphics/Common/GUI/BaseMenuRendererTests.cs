using System;
using Andastra.Runtime.Graphics.Common.GUI;
using FluentAssertions;
using Xunit;

namespace Andastra.Tests.Runtime.Graphics.Common.GUI
{
    /// <summary>
    /// Comprehensive unit tests for BaseMenuRenderer.
    /// Tests all common menu functionality including initialization, visibility, viewport management, and disposal.
    /// </summary>
    /// <remarks>
    /// BaseMenuRenderer Tests:
    /// - Tests all abstract base class functionality
    /// - Uses a concrete test implementation to test abstract methods
    /// - Ensures proper initialization sequence matching original engines
    /// - Validates viewport management and visibility handling
    /// - Tests disposal and resource cleanup
    /// </remarks>
    public class BaseMenuRendererTests : IDisposable
    {
        private TestMenuRenderer _renderer;

        public BaseMenuRendererTests()
        {
            _renderer = new TestMenuRenderer();
        }

        public void Dispose()
        {
            _renderer?.Dispose();
        }

        [Fact]
        public void Initialize_WithValidDimensions_ShouldSucceed()
        {
            // Act
            _renderer.Initialize(1280, 720);

            // Assert
            _renderer.IsInitialized.Should().BeTrue();
            _renderer.ViewportWidth.Should().Be(1280);
            _renderer.ViewportHeight.Should().Be(720);
        }

        [Fact]
        public void Initialize_WithZeroWidth_ShouldThrowArgumentException()
        {
            // Act & Assert
            _renderer.Invoking(r => r.Initialize(0, 720))
                .Should().Throw<ArgumentException>()
                .WithMessage("*viewportWidth*");
        }

        [Fact]
        public void Initialize_WithZeroHeight_ShouldThrowArgumentException()
        {
            // Act & Assert
            _renderer.Invoking(r => r.Initialize(1280, 0))
                .Should().Throw<ArgumentException>()
                .WithMessage("*viewportHeight*");
        }

        [Fact]
        public void Initialize_WithNegativeWidth_ShouldThrowArgumentException()
        {
            // Act & Assert
            _renderer.Invoking(r => r.Initialize(-100, 720))
                .Should().Throw<ArgumentException>()
                .WithMessage("*viewportWidth*");
        }

        [Fact]
        public void Initialize_WithNegativeHeight_ShouldThrowArgumentException()
        {
            // Act & Assert
            _renderer.Invoking(r => r.Initialize(1280, -100))
                .Should().Throw<ArgumentException>()
                .WithMessage("*viewportHeight*");
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
        public void SetVisible_False_ShouldSetIsVisibleToFalse()
        {
            // Arrange
            _renderer.SetVisible(true);

            // Act
            _renderer.SetVisible(false);

            // Assert
            _renderer.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void IsVisible_Property_ShouldTriggerOnVisibilityChanged()
        {
            // Arrange
            bool visibilityChangedCalled = false;
            bool newVisibility = false;
            _renderer.VisibilityChangedCallback = (visible) =>
            {
                visibilityChangedCalled = true;
                newVisibility = visible;
            };

            // Act
            _renderer.IsVisible = true;

            // Assert
            visibilityChangedCalled.Should().BeTrue();
            newVisibility.Should().BeTrue();
        }

        [Fact]
        public void IsVisible_Property_WhenUnchanged_ShouldNotTriggerOnVisibilityChanged()
        {
            // Arrange
            bool visibilityChangedCalled = false;
            _renderer.VisibilityChangedCallback = (visible) =>
            {
                visibilityChangedCalled = true;
            };

            // Act - set to same value
            _renderer.IsVisible = false;

            // Assert
            visibilityChangedCalled.Should().BeFalse();
        }

        [Fact]
        public void UpdateViewport_WithValidDimensions_ShouldUpdateViewport()
        {
            // Arrange
            _renderer.Initialize(1280, 720);
            bool viewportChangedCalled = false;
            int newWidth = 0;
            int newHeight = 0;
            _renderer.ViewportChangedCallback = (width, height) =>
            {
                viewportChangedCalled = true;
                newWidth = width;
                newHeight = height;
            };

            // Act
            _renderer.UpdateViewport(1920, 1080);

            // Assert
            _renderer.ViewportWidth.Should().Be(1920);
            _renderer.ViewportHeight.Should().Be(1080);
            viewportChangedCalled.Should().BeTrue();
            newWidth.Should().Be(1920);
            newHeight.Should().Be(1080);
        }

        [Fact]
        public void UpdateViewport_WithZeroWidth_ShouldThrowArgumentException()
        {
            // Arrange
            _renderer.Initialize(1280, 720);

            // Act & Assert
            _renderer.Invoking(r => r.UpdateViewport(0, 1080))
                .Should().Throw<ArgumentException>()
                .WithMessage("*width*");
        }

        [Fact]
        public void UpdateViewport_WithZeroHeight_ShouldThrowArgumentException()
        {
            // Arrange
            _renderer.Initialize(1280, 720);

            // Act & Assert
            _renderer.Invoking(r => r.UpdateViewport(1920, 0))
                .Should().Throw<ArgumentException>()
                .WithMessage("*height*");
        }

        [Fact]
        public void UpdateViewport_WithSameDimensions_ShouldNotTriggerOnViewportChanged()
        {
            // Arrange
            _renderer.Initialize(1280, 720);
            bool viewportChangedCalled = false;
            _renderer.ViewportChangedCallback = (width, height) =>
            {
                viewportChangedCalled = true;
            };

            // Act - update with same dimensions
            _renderer.UpdateViewport(1280, 720);

            // Assert
            viewportChangedCalled.Should().BeFalse();
        }

        [Fact]
        public void Dispose_ShouldSetIsInitializedToFalse()
        {
            // Arrange
            _renderer.Initialize(1280, 720);

            // Act
            _renderer.Dispose();

            // Assert
            _renderer.IsInitialized.Should().BeFalse();
        }

        [Fact]
        public void Dispose_ShouldSetIsVisibleToFalse()
        {
            // Arrange
            _renderer.Initialize(1280, 720);
            _renderer.SetVisible(true);

            // Act
            _renderer.Dispose();

            // Assert
            _renderer.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void Dispose_AfterDispose_ShouldNotThrow()
        {
            // Arrange
            _renderer.Initialize(1280, 720);
            _renderer.Dispose();

            // Act & Assert
            _renderer.Invoking(r => r.Dispose()).Should().NotThrow();
        }

        [Fact]
        public void IsInitialized_BeforeInitialize_ShouldBeFalse()
        {
            // Assert
            _renderer.IsInitialized.Should().BeFalse();
        }

        [Fact]
        public void ViewportWidth_BeforeInitialize_ShouldBeZero()
        {
            // Assert
            _renderer.ViewportWidth.Should().Be(0);
        }

        [Fact]
        public void ViewportHeight_BeforeInitialize_ShouldBeZero()
        {
            // Assert
            _renderer.ViewportHeight.Should().Be(0);
        }

        /// <summary>
        /// Test implementation of BaseMenuRenderer for testing abstract base class functionality.
        /// </summary>
        private class TestMenuRenderer : BaseMenuRenderer
        {
            public Action<bool> VisibilityChangedCallback { get; set; }
            public Action<int, int> ViewportChangedCallback { get; set; }

            protected override void OnVisibilityChanged(bool visible)
            {
                base.OnVisibilityChanged(visible);
                VisibilityChangedCallback?.Invoke(visible);
            }

            protected override void OnViewportChanged(int width, int height)
            {
                base.OnViewportChanged(width, height);
                ViewportChangedCallback?.Invoke(width, height);
            }
        }
    }
}

