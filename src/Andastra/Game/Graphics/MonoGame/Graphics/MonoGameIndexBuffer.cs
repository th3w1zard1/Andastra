using System;
using Andastra.Runtime.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Runtime.MonoGame.Graphics
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

        public IntPtr NativeHandle => IntPtr.Zero; // MonoGame IndexBuffer does not expose Handle directly

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
                _buffer.GetData(shortIndices);
                for (int i = 0; i < indices.Length && i < shortIndices.Length; i++)
                {
                    indices[i] = shortIndices[i];
                }
            }
            else
            {
                _buffer.GetData(indices);
            }
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

