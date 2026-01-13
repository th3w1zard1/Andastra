using System;
using Microsoft.Xna.Framework.Graphics;

namespace Andastra.Game.Graphics.MonoGame.Graphics
{
    /// <summary>
    /// MonoGame implementation of GraphicsBuffer for structured data.
    /// 
    /// Uses VertexBuffer internally to store structured data for compute shaders
    /// and GPU-accessible buffers. This is the standard approach in MonoGame
    /// when GraphicsBuffer is not directly available.
    /// 
    /// Supports:
    /// - Structured buffers for compute shaders
    /// - Dynamic data updates
    /// - GPU read/write access
    /// </summary>
    public class MonoGameGraphicsBuffer : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly VertexBuffer _buffer;
        private readonly int _elementCount;
        private readonly int _elementStride;
        private readonly bool _isDynamic;
        private bool _disposed;

        /// <summary>
        /// Gets the underlying VertexBuffer.
        /// </summary>
        internal VertexBuffer Buffer => _buffer;

        /// <summary>
        /// Gets the number of elements in the buffer.
        /// </summary>
        public int ElementCount => _elementCount;

        /// <summary>
        /// Gets the stride (size in bytes) of each element.
        /// </summary>
        public int ElementStride => _elementStride;

        /// <summary>
        /// Gets the total size of the buffer in bytes.
        /// </summary>
        public int SizeInBytes => _elementCount * _elementStride;

        /// <summary>
        /// Gets the native handle of the buffer.
        /// Note: MonoGame's VertexBuffer does not expose a Handle property directly.
        /// </summary>
        public IntPtr NativeHandle => IntPtr.Zero;

        /// <summary>
        /// Initializes a new GraphicsBuffer for structured data.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device.</param>
        /// <param name="elementCount">Number of elements in the buffer.</param>
        /// <param name="elementStride">Size in bytes of each element.</param>
        /// <param name="isDynamic">Whether the buffer can be updated frequently.</param>
        public MonoGameGraphicsBuffer(GraphicsDevice graphicsDevice, int elementCount, int elementStride, bool isDynamic = true)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }
            if (elementCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount), "Element count must be greater than zero.");
            }
            if (elementStride <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elementStride), "Element stride must be greater than zero.");
            }

            _graphicsDevice = graphicsDevice;
            _elementCount = elementCount;
            _elementStride = elementStride;
            _isDynamic = isDynamic;

            // Create a vertex declaration for structured data
            // We use a simple float4-based declaration that can hold any structured data
            // The shader will interpret this based on the actual structure layout
            VertexDeclaration declaration = CreateStructuredVertexDeclaration(elementStride);

            // Create vertex buffer with appropriate usage
            // Note: MonoGame's BufferUsage only has None and WriteOnly options
            BufferUsage usage = isDynamic ? BufferUsage.WriteOnly : BufferUsage.None;
            _buffer = new VertexBuffer(graphicsDevice, declaration, elementCount, usage);
        }

        /// <summary>
        /// Creates a vertex declaration for structured data.
        /// </summary>
        private static VertexDeclaration CreateStructuredVertexDeclaration(int elementStride)
        {
            // Calculate how many Vector4 elements we need to represent the structured data
            // Vector4 is 16 bytes, so we need (elementStride + 15) / 16 Vector4 elements
            int vector4Count = (elementStride + 15) / 16;
            if (vector4Count == 0)
            {
                vector4Count = 1;
            }

            VertexElement[] elements = new VertexElement[vector4Count];
            for (int i = 0; i < vector4Count; i++)
            {
                elements[i] = new VertexElement(
                    i * 16,
                    VertexElementFormat.Vector4,
                    VertexElementUsage.Position,
                    i
                );
            }

            return new VertexDeclaration(elements);
        }

        /// <summary>
        /// Sets data in the buffer.
        /// </summary>
        /// <typeparam name="T">The type of data elements.</typeparam>
        /// <param name="data">Array of data to set.</param>
        public void SetData<T>(T[] data) where T : struct
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MonoGameGraphicsBuffer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length > _elementCount)
            {
                throw new ArgumentException("Data array length exceeds buffer element count.", nameof(data));
            }

            _buffer.SetData(data);
        }

        /// <summary>
        /// Sets data in the buffer at a specific offset.
        /// </summary>
        /// <typeparam name="T">The type of data elements.</typeparam>
        /// <param name="data">Array of data to set.</param>
        /// <param name="startIndex">Starting index in the data array.</param>
        /// <param name="elementCount">Number of elements to set.</param>
        /// <param name="offset">Offset in the buffer (in elements, not bytes).</param>
        public void SetData<T>(T[] data, int startIndex, int elementCount, int offset) where T : struct
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MonoGameGraphicsBuffer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (startIndex < 0 || startIndex >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
            if (elementCount <= 0 || startIndex + elementCount > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount));
            }
            if (offset < 0 || offset + elementCount > _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            _buffer.SetData(offset * _elementStride, data, startIndex, elementCount, _elementStride);
        }

        /// <summary>
        /// Gets data from the buffer.
        /// </summary>
        /// <typeparam name="T">The type of data elements.</typeparam>
        /// <param name="data">Array to receive the data.</param>
        public void GetData<T>(T[] data) where T : struct
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MonoGameGraphicsBuffer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (data.Length > _elementCount)
            {
                throw new ArgumentException("Data array length exceeds buffer element count.", nameof(data));
            }

            _buffer.GetData(data);
        }

        /// <summary>
        /// Gets data from the buffer at a specific offset.
        /// </summary>
        /// <typeparam name="T">The type of data elements.</typeparam>
        /// <param name="data">Array to receive the data.</param>
        /// <param name="startIndex">Starting index in the data array.</param>
        /// <param name="elementCount">Number of elements to get.</param>
        /// <param name="offset">Offset in the buffer (in elements, not bytes).</param>
        public void GetData<T>(T[] data, int startIndex, int elementCount, int offset) where T : struct
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MonoGameGraphicsBuffer));
            }
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            if (startIndex < 0 || startIndex >= data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
            if (elementCount <= 0 || startIndex + elementCount > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(elementCount));
            }
            if (offset < 0 || offset + elementCount > _elementCount)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            _buffer.GetData(offset * _elementStride, data, startIndex, elementCount);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _buffer?.Dispose();
                _disposed = true;
            }
        }
    }
}

