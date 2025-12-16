using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Graphics backend type enumeration.
    /// </summary>
    public enum GraphicsBackendType
    {
        /// <summary>
        /// MonoGame backend (DesktopGL, DirectX, etc.)
        /// </summary>
        MonoGame,

        /// <summary>
        /// Stride 3D engine backend.
        /// </summary>
        Stride
    }

    /// <summary>
    /// Factory for creating graphics backend instances.
    /// Note: Implementations are in Odyssey.MonoGame and Odyssey.Stride projects.
    /// </summary>
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

