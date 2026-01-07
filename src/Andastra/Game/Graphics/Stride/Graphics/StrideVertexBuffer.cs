using System;
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

        public IntPtr NativeHandle
        {
            get
            {
                // Stride Buffer doesn't expose NativeBuffer directly
                // Return IntPtr.Zero as Stride manages native resources internally
                return IntPtr.Zero;
            }
        }

        public void SetData<T>(T[] data) where T : struct
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var commandList = _buffer.GraphicsDevice.ImmediateContext()
                            ?? throw new InvalidOperationException("CommandList is not available. Ensure GraphicsDeviceExtensions.RegisterCommandList() has been called.");

            // Stride's Buffer.SetData requires unmanaged constraint, but interface only allows struct
            // Use dynamic to bypass compile-time constraint checking (T must be unmanaged at runtime)
            ((dynamic)_buffer).SetData(commandList, data);
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

            var commandList = _buffer.GraphicsDevice.ImmediateContext()
                                ?? throw new InvalidOperationException("CommandList is not available. Ensure GraphicsDeviceExtensions.RegisterCommandList() has been called.");

            // Stride's Buffer.GetData requires unmanaged constraint, but interface only allows struct
            // Use dynamic to bypass compile-time constraint checking (T must be unmanaged at runtime)
            ((dynamic)_buffer).GetData(commandList, data);
        }

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}

