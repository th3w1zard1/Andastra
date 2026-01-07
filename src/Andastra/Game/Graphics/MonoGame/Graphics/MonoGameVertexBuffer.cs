using System;
using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IVertexBuffer.
    /// </summary>
    public class MonoGameVertexBuffer : IVertexBuffer
    {
        private readonly VertexBuffer _buffer;
        private readonly int _vertexCount;
        private readonly int _vertexStride;

        internal VertexBuffer Buffer => _buffer;

        public MonoGameVertexBuffer(VertexBuffer buffer, int vertexCount, int vertexStride)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _vertexCount = vertexCount;
            _vertexStride = vertexStride;
        }

        public int VertexCount => _vertexCount;

        public int VertexStride => _vertexStride;

        /// <summary>
        /// Gets the native handle for the buffer.
        /// Note: MonoGame's VertexBuffer does not expose a Handle property directly,
        /// so we return IntPtr.Zero. Use the Buffer property for direct access.
        /// </summary>
        public IntPtr NativeHandle => IntPtr.Zero;

        public void SetData<T>(T[] data) where T : struct
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _buffer.SetData(data);
        }

        public void GetData<T>(T[] data) where T : struct
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length > _vertexCount)
            {
                throw new ArgumentException("Data array length exceeds vertex count.", nameof(data));
            }

            _buffer.GetData(data);
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

