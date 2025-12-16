using System;
using Microsoft.Xna.Framework.Graphics;
using BioWareEngines.Graphics;

namespace BioWareEngines.MonoGame.Graphics
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

        public IntPtr NativeHandle => _buffer.Handle;

        public void SetData<T>(T[] data) where T : struct
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            _buffer.SetData(data);
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

