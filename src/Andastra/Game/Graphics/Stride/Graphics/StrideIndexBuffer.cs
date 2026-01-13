using System;
using Andastra.Runtime.Graphics;
using Andastra.Game.Stride.Graphics;
using StrideGraphics = Stride.Graphics;

namespace Andastra.Game.Stride.Graphics
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

        public IntPtr NativeHandle
        {
            get
            {
                // Stride Buffer doesn't expose NativeBuffer directly
                // Return IntPtr.Zero as Stride manages native resources internally
                return IntPtr.Zero;
            }
        }

        public void SetData(int[] indices)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            var commandList = _buffer.GraphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                throw new InvalidOperationException("CommandList is not available. Ensure GraphicsDeviceExtensions.RegisterCommandList() has been called.");
            }

            if (_isShort)
            {
                var shortIndices = new ushort[indices.Length];
                for (int i = 0; i < indices.Length; i++)
                {
                    shortIndices[i] = (ushort)indices[i];
                }
                _buffer.SetData(commandList, shortIndices);
            }
            else
            {
                _buffer.SetData(commandList, indices);
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

            var commandList = _buffer.GraphicsDevice.ImmediateContext();
            if (commandList == null)
            {
                throw new InvalidOperationException("CommandList is not available. Ensure GraphicsDeviceExtensions.RegisterCommandList() has been called.");
            }

            if (_isShort)
            {
                var shortIndices = new ushort[_indexCount];
                _buffer.GetData(commandList, shortIndices);
                for (int i = 0; i < indices.Length && i < shortIndices.Length; i++)
                {
                    indices[i] = shortIndices[i];
                }
            }
            else
            {
                _buffer.GetData(commandList, indices);
            }
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

