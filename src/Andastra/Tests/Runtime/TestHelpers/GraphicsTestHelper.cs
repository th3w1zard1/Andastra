using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Andastra.Parsing.Installation;
using Andastra.Parsing.Resource;
using Andastra.Runtime.Graphics;
using Andastra.Runtime.MonoGame.Graphics;
using Moq;
#
using Stride.Engine;
using StrideGraphics = Stride.Graphics;
using Stride.Core.Mathematics;

namespace Andastra.Tests.Runtime.TestHelpers
{
    /// <summary>
    /// Helper class for creating test graphics devices and installations.
    /// </summary>
    public static class GraphicsTestHelper
    {
        /// <summary>
        /// Creates a test MonoGame GraphicsDevice using a headless Game instance.
        /// </summary>
        public static GraphicsDevice CreateTestGraphicsDevice()
        {
            // Create a minimal Game instance for testing
            var game = new Game();
            game.Initialize();
            return game.GraphicsDevice;
        }

        /// <summary>
        /// Creates a test IGraphicsDevice wrapper.
        /// </summary>
        public static IGraphicsDevice CreateTestIGraphicsDevice()
        {
            var mgDevice = CreateTestGraphicsDevice();
            return new MonoGameGraphicsDevice(mgDevice);
        }

        /// <summary>
        /// Creates a mock Installation with resource lookup capabilities.
        /// </summary>
        public static Installation CreateMockInstallation()
        {
            // For testing, we'll create a real Installation pointing to a test directory
            // In a real scenario, you'd use Moq to mock the Installation
            string testPath = Path.Combine(Path.GetTempPath(), "AndastraTestInstallation");
            if (!Directory.Exists(testPath))
            {
                Directory.CreateDirectory(testPath);
            }

            // Create a minimal installation structure
            // Note: This is a simplified version - real tests would need proper game files
            try
            {
                return new Installation(testPath);
            }
            catch
            {
                // If installation creation fails, create a mock
                var mockInstallation = new Mock<Installation>(MockBehavior.Strict);
                var mockResources = new Mock<IResourceLookup>(MockBehavior.Strict);
                
                mockInstallation.Setup(i => i.Resources).Returns(mockResources.Object);
                
                // Setup default resource lookup to return null (resource not found)
                mockResources.Setup(r => r.LookupResource(
                    It.IsAny<string>(),
                    It.IsAny<ResourceType>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string[]>()))
                    .Returns((ResourceResult)null);

                return mockInstallation.Object;
            }
        }

        /// <summary>
        /// Creates a mock Installation with specific resource data.
        /// </summary>
        public static Installation CreateMockInstallationWithResource(string resRef, ResourceType resourceType, byte[] data)
        {
            var mockInstallation = new Mock<Installation>(MockBehavior.Strict);
            var mockResources = new Mock<IResourceLookup>(MockBehavior.Strict);
            
            mockInstallation.Setup(i => i.Resources).Returns(mockResources.Object);
            
            // Setup resource lookup for specific resource
            mockResources.Setup(r => r.LookupResource(
                resRef,
                resourceType,
                It.IsAny<string[]>(),
                It.IsAny<string[]>()))
                .Returns(new ResourceResult { Data = data });

            // Setup default lookup to return null
            mockResources.Setup(r => r.LookupResource(
                It.Is<string>(s => s != resRef),
                It.IsAny<ResourceType>(),
                It.IsAny<string[]>(),
                It.IsAny<string[]>()))
                .Returns((ResourceResult)null);

            return mockInstallation.Object;
        }

        /// <summary>
        /// Cleans up test resources.
        /// </summary>
        public static void CleanupTestGraphicsDevice(GraphicsDevice device)
        {
            if (device != null)
            {
                try
                {
                    device.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }

        /// <summary>
        /// Creates a test Stride GraphicsDevice using a minimal Game instance.
        /// Returns null if device creation fails (e.g., no GPU available in headless environment).
        /// </summary>
        /// <returns>Stride GraphicsDevice instance, or null if creation fails.</returns>
        /// <remarks>
        /// Stride GraphicsDevice Creation for Tests:
        /// - Creates a minimal Game instance for testing
        /// - Initializes the game to create GraphicsDevice
        /// - Sets up window properties for headless testing
        /// - Returns null if initialization fails (allows tests to skip gracefully)
        /// - Based on StrideGraphicsBackend initialization pattern
        /// - Tests should check for null and skip if device creation fails
        /// </remarks>
        public static StrideGraphics.GraphicsDevice CreateTestStrideGraphicsDevice()
        {
            try
            {
                // Create a minimal Game instance for testing
                // Stride Game constructor initializes GraphicsDevice automatically
                var game = new Stride.Engine.Game();
                
                // Set window properties for headless/minimal testing
                game.Window.ClientSize = new Int2(1280, 720);
                game.Window.Title = "Stride Test";
                game.Window.IsFullscreen = false;
                game.Window.IsMouseVisible = false;
                
                // Initialize the game to ensure GraphicsDevice is created
                // In a headless environment, this might fail if no GPU is available
                game.Initialize();
                
                // Return the GraphicsDevice from the game instance
                if (game.GraphicsDevice != null)
                {
                    return game.GraphicsDevice;
                }
                
                // If GraphicsDevice is null after initialization, dispose and return null
                game.Dispose();
                return null;
            }
            catch
            {
                // If device creation fails (e.g., no GPU in headless CI environment),
                // return null so tests can skip gracefully
                return null;
            }
        }

        /// <summary>
        /// Creates a test Stride Game instance for tests that need full game context.
        /// Returns null if game creation fails.
        /// </summary>
        /// <returns>Stride Game instance, or null if creation fails.</returns>
        /// <remarks>
        /// Stride Game Creation for Tests:
        /// - Creates a minimal Game instance with proper window configuration
        /// - Initializes the game for testing
        /// - Returns null if initialization fails (allows tests to skip gracefully)
        /// - Caller is responsible for disposing the Game instance
        /// </remarks>
        public static Stride.Engine.Game CreateTestStrideGame()
        {
            try
            {
                var game = new Stride.Engine.Game();
                game.Window.ClientSize = new Int2(1280, 720);
                game.Window.Title = "Stride Test";
                game.Window.IsFullscreen = false;
                game.Window.IsMouseVisible = false;
                game.Initialize();
                
                if (game.GraphicsDevice != null)
                {
                    return game;
                }
                
                game.Dispose();
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Cleans up test Stride GraphicsDevice resources.
        /// </summary>
        /// <param name="game">The Game instance to dispose.</param>
        /// <remarks>
        /// Stride Cleanup:
        /// - Disposes the Game instance which will clean up all graphics resources
        /// - Handles disposal errors gracefully for test robustness
        /// </remarks>
        public static void CleanupTestStrideGame(Stride.Engine.Game game)
        {
            if (game != null)
            {
                try
                {
                    game.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
        }
    }
}

