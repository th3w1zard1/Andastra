using System;
using Stride.Graphics;
using Odyssey.Graphics;

namespace Odyssey.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IIndexBuffer.
    /// </summary>
    public class StrideIndexBuffer : IIndexBuffer
    {
        private readonly Buffer _buffer;
        private readonly int _indexCount;
        private readonly bool _isShort;

        internal Buffer Buffer => _buffer;

        public StrideIndexBuffer(Buffer buffer, int indexCount, bool isShort)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _indexCount = indexCount;
            _isShort = isShort;
        }

        public int IndexCount => _indexCount;

        public bool IsShort => _isShort;

        public IntPtr NativeHandle => _buffer.NativeBuffer;

        public void SetData(int[] indices)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            if (_isShort)
            {
                var shortIndices = new ushort[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    shortIndices[i] = (ushort)indices[i];
                }
                _buffer.SetData(_buffer.GraphicsDevice.ImmediateContext, shortIndices);
            }
            else
            {
                _buffer.SetData(_buffer.GraphicsDevice.ImmediateContext, indices);
            }
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

