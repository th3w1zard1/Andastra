using System;
using Microsoft.Xna.Framework.Graphics;
using Odyssey.Graphics;

namespace Odyssey.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of IIndexBuffer.
    /// </summary>
    public class MonoGameIndexBuffer : IIndexBuffer
    {
        private readonly IndexBuffer _buffer;
        private readonly int _indexCount;
        private readonly bool _isShort;

        internal IndexBuffer Buffer => _buffer;

        public MonoGameIndexBuffer(IndexBuffer buffer, int indexCount, bool isShort)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _indexCount = indexCount;
            _isShort = isShort;
        }

        public int IndexCount => _indexCount;

        public bool IsShort => _isShort;

        public IntPtr NativeHandle => _buffer.Handle;

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
                _buffer.SetData(shortIndices);
            }
            else
            {
                _buffer.SetData(indices);
            }
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

