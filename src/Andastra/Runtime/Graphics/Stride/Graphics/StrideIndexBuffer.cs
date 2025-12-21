using System;
using StrideGraphics = Stride.Graphics;
using Andastra.Runtime.Graphics;

namespace Andastra.Runtime.Stride.Graphics
{
    /// <summary>
    /// Stride implementation of IIndexBuffer.
    /// </summary>
    public class StrideIndexBuffer : IIndexBuffer
    {
        private readonly StrideGraphics.Buffer _buffer;
        private readonly int _indexCount;
        private readonly bool _isShort;

        internal StrideGraphics.Buffer Buffer => _buffer;

        public StrideIndexBuffer(StrideGraphics.Buffer buffer, int indexCount, bool isShort)
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

        public void GetData(int[] indices)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            if (indices.Length > _indexCount)
            {
                throw new ArgumentException("Data array length exceeds index count.", nameof(indices));
            }

            if (_isShort)
            {
                var shortIndices = new ushort[_indexCount];
                _buffer.GetData(_buffer.GraphicsDevice.ImmediateContext, shortIndices);
                for (int i = 0; i < indices.Length && i < shortIndices.Length; i++)
                {
                    indices[i] = shortIndices[i];
                }
            }
            else
            {
                _buffer.GetData(_buffer.GraphicsDevice.ImmediateContext, indices);
            }
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

