using System;
using Odyssey.Graphics;

namespace Odyssey.Game.Core
{
    /// <summary>
    /// Factory for creating graphics backend instances.
    /// Located in Odyssey.Game to avoid circular dependencies in the abstraction layer.
    /// </summary>
    /// <remarks>
    /// Graphics Backend Factory:
    /// - Based on swkotor2.exe graphics initialization system
    /// - Original game uses DirectX 8/9 for rendering (D3D8.dll, D3D9.dll)
    /// - Located via string references: "Graphics Options" @ 0x007b56a8, "BTN_GRAPHICS" @ 0x007d0d8c, "optgraphics_p" @ 0x007d2064
    /// - "2D3DBias" @ 0x007c612c, "2D3D Bias" @ 0x007c71f8 (graphics settings)
    /// - Original implementation: Initializes DirectX device, sets up rendering pipeline
    /// - This implementation: Factory for creating modern graphics backends (MonoGame, Stride)
    /// - Note: MonoGame and Stride are modern graphics frameworks, not present in original game
    /// - Original game rendering: DirectX 8/9 fixed-function pipeline
    /// </remarks>
    public static class GraphicsBackendFactory
    {
        /// <summary>
        /// Creates a graphics backend of the specified type.
        /// </summary>
        /// <param name="backendType">The backend type to create.</param>
        /// <returns>An instance of the graphics backend.</returns>
        public static IGraphicsBackend CreateBackend(GraphicsBackendType backendType)
        {
            switch (backendType)
            {
                case GraphicsBackendType.MonoGame:
                    return new Odyssey.MonoGame.Graphics.MonoGameGraphicsBackend();
                case GraphicsBackendType.Stride:
                    return new Odyssey.Stride.Graphics.StrideGraphicsBackend();
                default:
                    throw new ArgumentException("Unknown graphics backend type: " + backendType, nameof(backendType));
            }
        }
    }
}

