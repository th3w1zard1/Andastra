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
            // Use reflection to load backend implementations from their respective assemblies
            // This allows the abstraction layer to remain independent
            string assemblyName;
            string typeName;

            switch (backendType)
            {
                case GraphicsBackendType.MonoGame:
                    assemblyName = "Odyssey.MonoGame";
                    typeName = "Odyssey.MonoGame.MonoGameGraphicsBackend";
                    break;
                case GraphicsBackendType.Stride:
                    assemblyName = "Odyssey.Stride";
                    typeName = "Odyssey.Stride.StrideGraphicsBackend";
                    break;
                default:
                    throw new ArgumentException("Unknown graphics backend type: " + backendType, nameof(backendType));
            }

            var assembly = System.Reflection.Assembly.Load(assemblyName);
            var type = assembly.GetType(typeName);
            if (type == null)
            {
                throw new InvalidOperationException("Could not find backend type: " + typeName + " in assembly: " + assemblyName);
            }

            return (IGraphicsBackend)Activator.CreateInstance(type);
        }
    }
}

