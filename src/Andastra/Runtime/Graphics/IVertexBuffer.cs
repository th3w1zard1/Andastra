using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Vertex buffer interface.
    /// </summary>
    /// <remarks>
    /// Vertex Buffer Interface:
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address) DirectX vertex buffer system
    /// - Located via string references: "Disable Vertex Buffer Objects" @ 0x007b56bc (VBO option)
    /// - "glVertexArrayRangeNV" @ 0x007b7ce8, "glVertexAttrib4fvNV" @ 0x007b7d24, "glVertexAttrib3fvNV" @ 0x007b7d38
    /// - "glVertexAttrib2fvNV" @ 0x007b7d4c (OpenGL vertex array extensions)
    /// - "glDeleteVertexShadersEXT" @ 0x007b7974, "glGenVertexShadersEXT" @ 0x007b7990, "glBindVertexShaderEXT" @ 0x007b79a8
    /// - "glEndVertexShaderEXT" @ 0x007b79c0, "glBeginVertexShaderEXT" @ 0x007b79d8 (vertex shader extensions)
    /// - Original implementation: DirectX 8/9 vertex buffer (IDirect3DVertexBuffer8/IDirect3DVertexBuffer9) for vertex data
    /// - Vertex buffers: Store vertex data (position, normal, texture coordinates, colors) for rendering
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// </remarks>
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

        /// <summary>
        /// Gets vertex data from the buffer.
        /// </summary>
        /// <typeparam name="T">Vertex type.</typeparam>
        /// <param name="data">Array to receive the vertex data.</param>
        void GetData<T>(T[] data) where T : struct;
    }
}

