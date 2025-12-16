using System;

namespace Odyssey.Graphics
{
    /// <summary>
    /// Index buffer interface.
    /// </summary>
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
    }
}

