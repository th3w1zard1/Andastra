using System;
using System.Collections.Generic;
using Stride.Graphics;
using StrideGraphics = Stride.Graphics;
using StrideEngine = Stride.Engine;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Extension methods for Stride GraphicsDevice to provide compatibility with older API.
    /// </summary>
    /// <remarks>
    /// ImmediateContext and Services were removed from GraphicsDevice in newer Stride versions.
    /// This implementation provides a compatibility layer:
    /// - ImmediateContext: Uses a static registry to map GraphicsDevice to CommandList from Game.GraphicsContext
    /// - Services: Should be obtained from Game.Services or ServiceRegistry, not from GraphicsDevice
    ///
    /// Based on Stride Graphics API: https://doc.stride3d.net/latest/en/manual/graphics/
    /// In newer Stride versions, CommandList is obtained from Game.GraphicsContext.CommandList.
    /// The registry allows code that has access to Game to register the CommandList for use by extension methods.
    /// </remarks>
    public static class GraphicsDeviceExtensions
    {
        // Static registry to map GraphicsDevice to its associated CommandList
        // This allows code with access to Game.GraphicsContext.CommandList to register it
        // for use by extension methods that only have access to GraphicsDevice
        private static readonly Dictionary<StrideGraphics.GraphicsDevice, StrideGraphics.CommandList> _commandListRegistry;

        // Static registry to map GraphicsDevice to its associated Game instance
        // This allows fallback access to Game.GraphicsContext.CommandList when CommandList is not directly registered
        // Based on Stride 4.2: Game instance is required to access GraphicsContext.CommandList
        private static readonly Dictionary<StrideGraphics.GraphicsDevice, StrideEngine.Game> _gameRegistry;

        private static readonly object _registryLock;

        static GraphicsDeviceExtensions()
        {
            _commandListRegistry = new Dictionary<StrideGraphics.GraphicsDevice, StrideGraphics.CommandList>();
            _gameRegistry = new Dictionary<StrideGraphics.GraphicsDevice, StrideEngine.Game>();
            _registryLock = new object();
        }

        /// <summary>
        /// Registers a CommandList for a GraphicsDevice.
        /// This should be called by code that has access to Game.GraphicsContext.CommandList.
        /// </summary>
        /// <param name="device">The GraphicsDevice to register.</param>
        /// <param name="commandList">The CommandList from Game.GraphicsContext.CommandList.</param>
        /// <remarks>
        /// Registration: Code that has access to Game instance should call this method to register
        /// the CommandList from Game.GraphicsContext.CommandList. This allows the ImmediateContext()
        /// extension method to retrieve the correct CommandList for immediate rendering operations.
        ///
        /// Example usage:
        /// GraphicsDeviceExtensions.RegisterCommandList(game.GraphicsDevice, game.GraphicsContext.CommandList);
        /// </remarks>
        public static void RegisterCommandList(StrideGraphics.GraphicsDevice device, StrideGraphics.CommandList commandList)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            lock (_registryLock)
            {
                _commandListRegistry[device] = commandList;
            }
        }

        /// <summary>
        /// Registers a Game instance for a GraphicsDevice.
        /// This allows fallback access to Game.GraphicsContext.CommandList when CommandList is not directly registered.
        /// </summary>
        /// <param name="device">The GraphicsDevice to register.</param>
        /// <param name="game">The Game instance that owns the GraphicsDevice.</param>
        /// <remarks>
        /// Game Registration: Code that has access to Game instance should call this method to register
        /// the Game instance. This allows the ImmediateContext() extension method to retrieve the CommandList
        /// from Game.GraphicsContext.CommandList as a fallback when the CommandList is not directly registered.
        ///
        /// Based on Stride 4.2: Game instance is required to access GraphicsContext.CommandList
        /// In Stride 4.2, GraphicsDevice doesn't have ResourceFactory, so CommandList must be obtained from Game.GraphicsContext.CommandList
        ///
        /// Example usage:
        /// GraphicsDeviceExtensions.RegisterGame(game.GraphicsDevice, game);
        /// </remarks>
        public static void RegisterGame(StrideGraphics.GraphicsDevice device, StrideEngine.Game game)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            if (game == null)
            {
                throw new ArgumentNullException(nameof(game));
            }

            lock (_registryLock)
            {
                _gameRegistry[device] = game;

                // Also register the CommandList if available from GraphicsContext
                // This ensures both registries are kept in sync
                if (game.GraphicsContext != null && game.GraphicsContext.CommandList != null)
                {
                    _commandListRegistry[device] = game.GraphicsContext.CommandList;
                }
            }
        }

        /// <summary>
        /// Unregisters a CommandList for a GraphicsDevice.
        /// Should be called when the GraphicsDevice is being disposed or no longer in use.
        /// </summary>
        /// <param name="device">The GraphicsDevice to unregister.</param>
        public static void UnregisterCommandList(StrideGraphics.GraphicsDevice device)
        {
            if (device == null)
            {
                return;
            }

            lock (_registryLock)
            {
                _commandListRegistry.Remove(device);
            }
        }

        /// <summary>
        /// Unregisters a Game instance for a GraphicsDevice.
        /// Should be called when the GraphicsDevice is being disposed or no longer in use.
        /// </summary>
        /// <param name="device">The GraphicsDevice to unregister.</param>
        public static void UnregisterGame(StrideGraphics.GraphicsDevice device)
        {
            if (device == null)
            {
                return;
            }

            lock (_registryLock)
            {
                _gameRegistry.Remove(device);
                // Also remove CommandList registration if it was auto-registered from Game
                _commandListRegistry.Remove(device);
            }
        }

        /// <summary>
        /// Gets the immediate command list for the graphics device.
        /// </summary>
        /// <param name="device">The GraphicsDevice to get the CommandList for.</param>
        /// <returns>The CommandList for immediate rendering operations, or null if not registered.</returns>
        /// <remarks>
        /// CommandList Retrieval:
        /// - First attempts to retrieve from the static registry (registered via RegisterCommandList)
        /// - If not found in registry, attempts to create a new CommandList using ResourceFactory
        /// - Returns null if both methods fail
        ///
        /// Based on Stride Graphics API: https://doc.stride3d.net/latest/en/manual/graphics/
        /// In newer Stride versions, the immediate CommandList is obtained from Game.GraphicsContext.CommandList.
        /// This extension method provides compatibility for code that only has access to GraphicsDevice.
        ///
        /// swkotor2.exe: Graphics device command list management @ 0x004eb750 (original engine behavior)
        /// </remarks>
        [JetBrains.Annotations.CanBeNull]
        public static StrideGraphics.CommandList ImmediateContext(this StrideGraphics.GraphicsDevice device)
        {
            if (device == null)
            {
                return null;
            }

            // First, try to get from registry (preferred method - uses the actual GraphicsContext CommandList)
            lock (_registryLock)
            {
                StrideGraphics.CommandList registeredCommandList;
                if (_commandListRegistry.TryGetValue(device, out registeredCommandList))
                {
                    return registeredCommandList;
                }

                // Fallback: Try to get CommandList from registered Game instance
                // Based on Stride 4.2: Game instance is required to access GraphicsContext.CommandList
                // In Stride 4.2, GraphicsDevice doesn't have ResourceFactory, so CommandList must be obtained from Game.GraphicsContext.CommandList
                StrideEngine.Game registeredGame;
                if (_gameRegistry.TryGetValue(device, out registeredGame))
                {
                    // Get CommandList from Game.GraphicsContext if available
                    // Based on Stride 4.2: GraphicsContext.CommandList provides the immediate command list for rendering
                    if (registeredGame != null && registeredGame.GraphicsContext != null && registeredGame.GraphicsContext.CommandList != null)
                    {
                        StrideGraphics.CommandList commandListFromGame = registeredGame.GraphicsContext.CommandList;

                        // Cache it in the CommandList registry for future use
                        // This improves performance by avoiding repeated Game.GraphicsContext access
                        _commandListRegistry[device] = commandListFromGame;

                        return commandListFromGame;
                    }
                }
            }

            // Final fallback: CommandList is not available
            // Based on Stride 4.2: GraphicsDevice doesn't have ResourceFactory or CreateCommandList method
            // CommandList must be obtained from Game.GraphicsContext.CommandList
            // The CommandList should be registered via RegisterCommandList() or RegisterGame() from code that has access to Game instance
            //
            // Implementation note: In Stride 4.2, you cannot create a CommandList directly from GraphicsDevice
            // The CommandList is managed by Game.GraphicsContext and is created per frame for thread safety
            // This is by design - Stride manages command list lifecycle to ensure proper resource management
            //
            // If you need a CommandList and only have access to GraphicsDevice:
            // 1. Register the Game instance: GraphicsDeviceExtensions.RegisterGame(device, game);
            // 2. Or register the CommandList directly: GraphicsDeviceExtensions.RegisterCommandList(device, game.GraphicsContext.CommandList);
            //
            // Based on Stride Graphics API: https://doc.stride3d.net/latest/en/manual/graphics/
            // CommandList is obtained from Game.GraphicsContext.CommandList, not created from GraphicsDevice
            // swkotor2.exe: Graphics device command list management @ 0x004eb750 (original engine behavior)
            return null;
        }

        /// <summary>
        /// Gets the GraphicsContext for the graphics device.
        /// </summary>
        /// <param name="device">The GraphicsDevice to get the GraphicsContext for.</param>
        /// <returns>The GraphicsContext for the device, or null if not available.</returns>
        /// <remarks>
        /// GraphicsContext Retrieval:
        /// - Attempts to retrieve from the registered Game instance's GraphicsContext
        /// - Returns null if no Game instance is registered or GraphicsContext is not available
        ///
        /// Based on Stride Graphics API: GraphicsContext is obtained from Game.GraphicsContext
        /// This extension method provides compatibility for code that only has access to GraphicsDevice.
        ///
        /// swkotor2.exe: Graphics context management @ 0x004eb750 (original engine behavior)
        /// </remarks>
        [JetBrains.Annotations.CanBeNull]
        public static StrideGraphics.GraphicsContext GraphicsContext(this StrideGraphics.GraphicsDevice device)
        {
            if (device == null)
            {
                return null;
            }

            // Try to get GraphicsContext from registered Game instance
            // Based on Stride API: Game.GraphicsContext provides the graphics context for rendering
            lock (_registryLock)
            {
                StrideEngine.Game registeredGame;
                if (_gameRegistry.TryGetValue(device, out registeredGame))
                {
                    // Get GraphicsContext from Game.GraphicsContext if available
                    // Based on Stride API: Game.GraphicsContext provides the rendering context
                    if (registeredGame != null && registeredGame.GraphicsContext != null)
                    {
                        return registeredGame.GraphicsContext;
                    }
                }
            }

            // GraphicsContext is not available
            // The Game instance should be registered via RegisterGame() from code that has access to Game instance
            // Based on Stride Graphics API: GraphicsContext is obtained from Game.GraphicsContext
            return null;
        }

        /// <summary>
        /// Gets the service registry for the graphics device.
        /// </summary>
        /// <param name="device">The GraphicsDevice to get services for.</param>
        /// <returns>Always returns null. Services should be obtained from Game.Services or ServiceRegistry.</returns>
        /// <remarks>
        /// GraphicsDevice.Services was removed in newer Stride versions.
        /// Services should be obtained from Game.Services or passed as a parameter.
        /// This method exists for API compatibility but always returns null.
        ///
        /// Based on Stride Engine API: Services are accessed through Game.Services, not GraphicsDevice.Services
        /// </remarks>
        [JetBrains.Annotations.CanBeNull]
        public static object Services(this StrideGraphics.GraphicsDevice device)
        {
            // GraphicsDevice.Services was removed in newer Stride versions
            // Services should be obtained from Game.Services or ServiceRegistry, not from GraphicsDevice
            // This method exists for API compatibility but always returns null
            // Code using this should obtain services from Game.Services instead
            return null;
        }
    }
}

