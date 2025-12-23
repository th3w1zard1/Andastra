using System;
using System.Collections.Generic;
using StrideGraphics = Stride.Graphics;
using Stride.Engine;

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
        private static readonly object _registryLock;

        static GraphicsDeviceExtensions()
        {
            _commandListRegistry = new Dictionary<StrideGraphics.GraphicsDevice, StrideGraphics.CommandList>();
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
            }

            // Fallback: In Stride 4.2, GraphicsDevice doesn't have ResourceFactory
            // CommandList should be registered via RegisterCommandList() from Game.GraphicsContext.CommandList
            // If not registered, return null - calling code should register the CommandList if available
            // TODO: STUB - CommandList creation through GraphicsDevice is not available in Stride 4.2
            // The CommandList must be registered via RegisterCommandList() from Game.GraphicsContext.CommandList
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

