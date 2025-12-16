using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Vertex buffer interface.
    /// </summary>
    public interface IVertexBuffer : IDisposable
    {
        /// <summary>
        /// Gets the number of vertices.
        /// </summary>
        int VertexCount { get; }

        /// <summary>
        /// Gets the vertex stride (bytes per vertex).
        /// </summary>
        int VertexStride { get; }

        /// <summary>
        /// Gets the native buffer handle.
        /// </summary>
        IntPtr NativeHandle { get; }

        /// <summary>
        /// Sets vertex data.
        /// </summary>
        /// <typeparam name="T">Vertex type.</typeparam>
        /// <param name="data">Vertex data.</param>
        void SetData<T>(T[] data) where T : struct;
    }
}

