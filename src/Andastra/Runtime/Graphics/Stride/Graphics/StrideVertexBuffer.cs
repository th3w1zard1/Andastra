using System;
using Stride.Graphics;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IVertexBuffer.
    /// </summary>
    public class StrideVertexBuffer : IVertexBuffer
    {
        private readonly global::Stride.Graphics.Buffer _buffer;
        private readonly int _vertexCount;
        private readonly int _vertexStride;

        internal global::Stride.Graphics.Buffer Buffer => _buffer;

        public StrideVertexBuffer(global::Stride.Graphics.Buffer buffer, int vertexCount, int vertexStride)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _vertexCount = vertexCount;
            _vertexStride = vertexStride;
        }

        public int VertexCount => _vertexCount;

        public int VertexStride => _vertexStride;

        public IntPtr NativeHandle => _buffer.NativeBuffer;

        public void SetData<T>(T[] data) where T : struct
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _buffer.SetData(_buffer.GraphicsDevice.ImmediateContext, data);
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

            _buffer.GetData(_buffer.GraphicsDevice.ImmediateContext, data);
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

