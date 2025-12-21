using System;

namespace Andastra.Runtime.Graphics
{
    /// <summary>
    /// Index buffer interface.
    /// </summary>
    /// <remarks>
    /// Index Buffer Interface:
    /// - Based on swkotor2.exe DirectX index buffer system
    /// - Located via string references: "GetNextIndex: Duplicate triangle sent\n" @ 0x007bb308, "GetNextIndex: Duplicate triangle probably got us derailed\n" @ 0x007bb330
    /// - "GetNextIndex: Triangle doesn't have all of its vertices\n" @ 0x007bb36c (index buffer validation)
    /// - Original implementation: DirectX 8/9 index buffer (IDirect3DIndexBuffer8/IDirect3DIndexBuffer9) for indexed rendering
    /// - Index buffers: Store triangle indices for indexed primitive rendering (reduces vertex duplication)
    /// - This interface: Abstraction layer for modern graphics APIs (DirectX 11/12, OpenGL, Vulkan)
    /// </remarks>
    public interface IIndexBuffer : IDisposable
    {
        /// <summary>
        /// Gets the number of indices.
        /// </summary>
        int IndexCount { get; }

        /// <summary>
        /// Gets whether indices are 16-bit (true) or 32-bit (false).
        /// </summary>
        bool IsShort { get; }

        /// <summary>
        /// Gets the native buffer handle.
        /// </summary>
        IntPtr NativeHandle { get; }

        /// <summary>
        /// Sets index data.
        /// </summary>
        /// <param name="indices">Index data.</param>
        void SetData(int[] indices);

        /// <summary>
        /// Gets index data from the buffer.
        /// </summary>
        /// <param name="indices">Array to receive the index data.</param>
        void GetData(int[] indices);
    }
}

