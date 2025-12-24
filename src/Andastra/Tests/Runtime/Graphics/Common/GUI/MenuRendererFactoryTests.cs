using System;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.Graphics.Common.Enums;
using Andastra.Runtime.Graphics.Common.GUI;
using Andastra.Runtime.MonoGame.GUI;
using Andastra.Tests.Runtime.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace Andastra.Tests.Runtime.Graphics.Common.GUI
{
    /// <summary>
    /// Comprehensive unit tests for MenuRendererFactory.
    /// Tests factory creation logic for all supported graphics backends.
    /// </summary>
    /// <remarks>
    /// MenuRendererFactory Tests:
    /// - Tests factory creation for MonoGame backend
    /// - Tests factory creation for Stride backend
    /// - Tests error handling (null backend, uninitialized backend, unsupported backend)
    /// - Tests reflection-based graphics device extraction
    /// - Ensures proper menu renderer instantiation
    /// </remarks>
    public class MenuRendererFactoryTests
    {
        [Fact]
        public void CreateMenuRenderer_WithNullBackend_ShouldThrowArgumentNullException()
        {
            // Act & Assert
            Action act = () => MenuRendererFactory.CreateMenuRenderer(null);
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("graphicsBackend");
        }

        [Fact]
        public void CreateMenuRenderer_WithUninitializedBackend_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var mockBackend = new Mock<IGraphicsBackend>(MockBehavior.Strict);
            mockBackend.Setup(b => b.IsInitialized).Returns(false);
            mockBackend.Setup(b => b.BackendType).Returns(GraphicsBackendType.MonoGame);

            // Act & Assert
            Action act = () => MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*initialized*");
        }

        [Fact]
        public void CreateMenuRenderer_WithMonoGameBackend_ShouldCreateMyraMenuRenderer()
        {
            // Arrange
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.MonoGame, graphicsDevice, true);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            renderer.Should().NotBeNull();
            renderer.Should().BeOfType<MyraMenuRenderer>();
            renderer.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void CreateMenuRenderer_WithMonoGameBackend_ShouldInitializeRenderer()
        {
            // Arrange
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.MonoGame, graphicsDevice, true);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            renderer.Should().NotBeNull();
            renderer.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void CreateMenuRenderer_WithStrideBackend_ShouldCreateStrideMenuRenderer()
        {
            // Arrange
            // Note: Stride backend requires actual Stride GraphicsDevice
            // Try to create a real Stride device wrapper if available, otherwise skip test
            var strideWrapper = GraphicsTestHelper.CreateTestStrideIGraphicsDevice();
            if (strideWrapper == null)
            {
                // Skip test if Stride device cannot be created (e.g., no GPU in headless CI environment)
                return;
            }

            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.Stride, strideWrapper, true);
            mockBackend.Setup(b => b.ContentManager).Returns((IContentManager)null);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            // If Stride device is available, renderer should be created successfully
            renderer.Should().NotBeNull();
            renderer.Should().BeOfType<Andastra.Runtime.Stride.GUI.StrideMenuRenderer>();
            renderer.IsInitialized.Should().BeTrue();
        }

        [Fact]
        public void CreateMenuRenderer_WithStrideBackend_AndNonStrideDevice_ShouldReturnNull()
        {
            // Arrange
            // Use MonoGame device (not Stride device) - this will cause type check to fail
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.Stride, graphicsDevice, true);
            mockBackend.Setup(b => b.ContentManager).Returns((IContentManager)null);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            // Device extraction fails because graphicsDevice is not StrideGraphicsDevice
            // Factory should return null when device extraction fails
            renderer.Should().BeNull("device extraction should fail when GraphicsDevice is not StrideGraphicsDevice");
        }

        [Fact]
        public void CreateMenuRenderer_WithStrideBackend_AndNullDevice_ShouldReturnNull()
        {
            // Arrange
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.Stride, null, true);
            mockBackend.Setup(b => b.ContentManager).Returns((IContentManager)null);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            // Factory should return null when GraphicsDevice is null
            renderer.Should().BeNull("factory should return null when GraphicsDevice is null");
        }

        [Fact]
        public void CreateMenuRenderer_WithStrideBackend_AndMockDevice_ShouldReturnNull()
        {
            // Arrange
            // Create a mock IGraphicsDevice that is not StrideGraphicsDevice
            // This simulates the case where device extraction fails
            var mockGraphicsDevice = new Mock<IGraphicsDevice>(MockBehavior.Strict);
            mockGraphicsDevice.Setup(d => d.Viewport).Returns(new Andastra.Runtime.Graphics.Viewport(0, 0, 1920, 1080, 0.0f, 1.0f));
            mockGraphicsDevice.Setup(d => d.RenderTarget).Returns((IRenderTarget)null);
            mockGraphicsDevice.Setup(d => d.DepthStencilBuffer).Returns((IDepthStencilBuffer)null);
            mockGraphicsDevice.Setup(d => d.NativeHandle).Returns(IntPtr.Zero);
            mockGraphicsDevice.Setup(d => d.Dispose());

            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.Stride, mockGraphicsDevice.Object, true);
            mockBackend.Setup(b => b.ContentManager).Returns((IContentManager)null);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            // Device extraction fails because mock device is not StrideGraphicsDevice
            // Factory should return null when device extraction fails
            renderer.Should().BeNull("device extraction should fail when GraphicsDevice is not StrideGraphicsDevice type");
        }

        [Fact]
        public void CreateMenuRenderer_WithUnsupportedBackend_ShouldReturnNull()
        {
            // Arrange
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend((GraphicsBackendType)999, graphicsDevice, true);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            renderer.Should().BeNull();
        }

        [Fact]
        public void CreateMenuRenderer_WithMonoGameBackend_ShouldSetViewport()
        {
            // Arrange
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.MonoGame, graphicsDevice, true);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            renderer.Should().NotBeNull();
            if (renderer != null)
            {
                renderer.ViewportWidth.Should().BeGreaterThan(0);
                renderer.ViewportHeight.Should().BeGreaterThan(0);
            }
        }

        [Fact]
        public void CreateMenuRenderer_WithMonoGameBackend_ShouldAllowVisibilityControl()
        {
            // Arrange
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.MonoGame, graphicsDevice, true);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            renderer.Should().NotBeNull();
            if (renderer != null)
            {
                renderer.IsVisible.Should().BeFalse();
                renderer.SetVisible(true);
                renderer.IsVisible.Should().BeTrue();
            }
        }

        [Fact]
        public void CreateMenuRenderer_WithMonoGameBackend_ShouldAllowViewportUpdates()
        {
            // Arrange
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.MonoGame, graphicsDevice, true);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            renderer.Should().NotBeNull();
            if (renderer != null)
            {
                renderer.Invoking(r => r.UpdateViewport(1920, 1080)).Should().NotThrow();
                renderer.ViewportWidth.Should().Be(1920);
                renderer.ViewportHeight.Should().Be(1080);
            }
        }

        [Fact]
        public void CreateMenuRenderer_WithMonoGameBackend_ShouldAllowDisposal()
        {
            // Arrange
            var graphicsDevice = GraphicsTestHelper.CreateTestIGraphicsDevice();
            var mockBackend = CreateMockGraphicsBackend(GraphicsBackendType.MonoGame, graphicsDevice, true);

            // Act
            var renderer = MenuRendererFactory.CreateMenuRenderer(mockBackend.Object);

            // Assert
            renderer.Should().NotBeNull();
            if (renderer != null)
            {
                renderer.Invoking(r => r.Dispose()).Should().NotThrow();
            }
        }

        /// <summary>
        /// Creates a mock IGraphicsBackend with the specified configuration.
        /// Sets up all interface members required by MockBehavior.Strict to ensure comprehensive mocking.
        /// </summary>
        /// <param name="backendType">The graphics backend type to mock.</param>
        /// <param name="graphicsDevice">The graphics device to use (required).</param>
        /// <param name="isInitialized">Whether the backend is initialized.</param>
        /// <returns>A fully configured mock IGraphicsBackend with all interface members set up.</returns>
        /// <remarks>
        /// Mock Setup Details:
        /// - Properties: BackendType, IsInitialized, GraphicsDevice (from parameters)
        /// - Properties: ContentManager, Window, InputManager (mocked, can return null)
        /// - Properties: SupportsVSync (defaults to true for testing)
        /// - Methods: All IGraphicsBackend interface methods are set up with default implementations
        /// - Methods returning interfaces: Return null by default (safe for testing scenarios)
        /// - Methods with void return: Set up as no-ops (safe for testing)
        /// - IDisposable: Dispose() is set up as a no-op
        /// </remarks>
        private Mock<IGraphicsBackend> CreateMockGraphicsBackend(
            GraphicsBackendType backendType,
            IGraphicsDevice graphicsDevice,
            bool isInitialized)
        {
            var mockBackend = new Mock<IGraphicsBackend>(MockBehavior.Strict);

            // Core properties (from parameters)
            mockBackend.Setup(b => b.BackendType).Returns(backendType);
            mockBackend.Setup(b => b.GraphicsDevice).Returns(graphicsDevice);
            mockBackend.Setup(b => b.IsInitialized).Returns(isInitialized);

            // Additional properties (mocked with null/default values for testing)
            mockBackend.Setup(b => b.ContentManager).Returns((IContentManager)null);
            mockBackend.Setup(b => b.Window).Returns((IWindow)null);
            mockBackend.Setup(b => b.InputManager).Returns((IInputManager)null);
            mockBackend.Setup(b => b.SupportsVSync).Returns(true);

            // Lifecycle methods (no-ops for testing)
            mockBackend.Setup(b => b.Initialize(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<bool>()));

            mockBackend.Setup(b => b.Run(
                It.IsAny<Action<float>>(),
                It.IsAny<Action>()));

            mockBackend.Setup(b => b.Exit());

            // Frame management methods (no-ops for testing)
            mockBackend.Setup(b => b.BeginFrame());
            mockBackend.Setup(b => b.EndFrame());

            // Renderer creation methods (return null for testing - callers should handle null)
            mockBackend.Setup(b => b.CreateRoomMeshRenderer()).Returns((IRoomMeshRenderer)null);
            mockBackend.Setup(b => b.CreateEntityModelRenderer(
                It.IsAny<object>(),
                It.IsAny<object>())).Returns((IEntityModelRenderer)null);

            // Audio creation methods (return null for testing)
            mockBackend.Setup(b => b.CreateSpatialAudio()).Returns((ISpatialAudio)null);

            // Player creation methods (return null for testing)
            mockBackend.Setup(b => b.CreateDialogueCameraController(
                It.IsAny<object>())).Returns((object)null);
            mockBackend.Setup(b => b.CreateSoundPlayer(
                It.IsAny<object>())).Returns((object)null);
            mockBackend.Setup(b => b.CreateMusicPlayer(
                It.IsAny<object>())).Returns((object)null);
            mockBackend.Setup(b => b.CreateVoicePlayer(
                It.IsAny<object>())).Returns((object)null);

            // VSync method (no-op for testing)
            mockBackend.Setup(b => b.SetVSync(It.IsAny<bool>()));

            // IDisposable implementation (no-op for testing)
            mockBackend.Setup(b => b.Dispose());

            return mockBackend;
        }
    }
}

