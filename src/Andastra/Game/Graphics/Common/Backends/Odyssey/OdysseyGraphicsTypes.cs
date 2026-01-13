using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Andastra.Runtime.Graphics;
using Andastra.Game.Games.Common;

namespace Andastra.Game.Graphics.Common.Backends.Odyssey
{
    /// <summary>
    /// Odyssey texture implementation.
    /// Wraps OpenGL texture object.
    /// </summary>
    public class OdysseyTexture2D : ITexture2D
    {
        private readonly uint _textureId;
        private readonly int _width;
        private readonly int _height;
        private byte[] _data;
        private bool _disposed;

        [DllImport("opengl32.dll", EntryPoint = "glDeleteTextures")]
        private static extern void glDeleteTextures(int n, uint[] textures);

        [DllImport("opengl32.dll", EntryPoint = "glBindTexture")]
        private static extern void glBindTexture(uint target, uint texture);

        [DllImport("opengl32.dll", EntryPoint = "glTexImage2D")]
        private static extern void glTexImage2D(uint target, int level, int internalformat, int width, int height, int border, uint format, uint type, IntPtr pixels);

        [DllImport("opengl32.dll", EntryPoint = "glGetTexImage")]
        private static extern void glGetTexImage(uint target, int level, uint format, uint type, IntPtr pixels);

        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_RGBA = 0x1908;
        private const uint GL_UNSIGNED_BYTE = 0x1401;

        public OdysseyTexture2D(uint textureId, int width, int height)
        {
            _textureId = textureId;
            _width = width;
            _height = height;
        }

        public int Width => _width;
        public int Height => _height;
        public IntPtr NativeHandle => new IntPtr(_textureId);

        public void SetData(byte[] data)
        {
            _data = data;
            if (data != null && data.Length > 0)
            {
                glBindTexture(GL_TEXTURE_2D, _textureId);
                IntPtr dataPtr = Marshal.AllocHGlobal(data.Length);
                try
                {
                    Marshal.Copy(data, 0, dataPtr, data.Length);
                    glTexImage2D(GL_TEXTURE_2D, 0, (int)GL_RGBA, _width, _height, 0, GL_RGBA, GL_UNSIGNED_BYTE, dataPtr);
                }
                finally
                {
                    Marshal.FreeHGlobal(dataPtr);
                }
            }
        }

        public byte[] GetData()
        {
            if (_data != null)
            {
                return _data;
            }

            // Read from GPU
            int size = _width * _height * 4;
            byte[] data = new byte[size];

            glBindTexture(GL_TEXTURE_2D, _textureId);
            IntPtr dataPtr = Marshal.AllocHGlobal(size);
            try
            {
                glGetTexImage(GL_TEXTURE_2D, 0, GL_RGBA, GL_UNSIGNED_BYTE, dataPtr);
                Marshal.Copy(dataPtr, data, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(dataPtr);
            }

            return data;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                uint[] textures = new uint[] { _textureId };
                glDeleteTextures(1, textures);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Odyssey render target implementation.
    /// Uses OpenGL framebuffer objects (FBO).
    /// </summary>
    public class OdysseyRenderTarget : IRenderTarget
    {
        private readonly int _width;
        private readonly int _height;
        private uint _framebufferId;
        private OdysseyTexture2D _colorTexture;
        private OdysseyDepthStencilBuffer _depthStencilBuffer;
        private bool _disposed;

        public OdysseyRenderTarget(int width, int height)
        {
            _width = width;
            _height = height;
            // TODO: STUB - Create FBO and attachments
        }

        public int Width => _width;
        public int Height => _height;
        public ITexture2D ColorTexture => _colorTexture;
        public IDepthStencilBuffer DepthStencilBuffer => _depthStencilBuffer;
        public IntPtr NativeHandle => new IntPtr(_framebufferId);

        public void Dispose()
        {
            if (!_disposed)
            {
                _colorTexture?.Dispose();
                _depthStencilBuffer?.Dispose();
                // TODO: STUB - Delete FBO
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Odyssey depth-stencil buffer implementation.
    /// Uses OpenGL renderbuffer.
    /// Based on swkotor.exe/swkotor2.exe: Depth-stencil buffer creation for z-buffering and stencil operations
    /// Matching reone Renderbuffer implementation: glGenRenderbuffers -> glBindRenderbuffer -> glRenderbufferStorage
    /// </summary>
    public class OdysseyDepthStencilBuffer : IDepthStencilBuffer
    {
        private readonly int _width;
        private readonly int _height;
        private uint _renderbufferId;
        private bool _disposed;

        #region OpenGL P/Invoke for Renderbuffers

        [System.Runtime.InteropServices.DllImport("opengl32.dll", EntryPoint = "glGenRenderbuffers")]
        private static extern void glGenRenderbuffers(int n, uint[] renderbuffers);

        [System.Runtime.InteropServices.DllImport("opengl32.dll", EntryPoint = "glBindRenderbuffer")]
        private static extern void glBindRenderbuffer(uint target, uint renderbuffer);

        [System.Runtime.InteropServices.DllImport("opengl32.dll", EntryPoint = "glRenderbufferStorage")]
        private static extern void glRenderbufferStorage(uint target, uint internalformat, int width, int height);

        [System.Runtime.InteropServices.DllImport("opengl32.dll", EntryPoint = "glDeleteRenderbuffers")]
        private static extern void glDeleteRenderbuffers(int n, uint[] renderbuffers);

        // OpenGL renderbuffer constants
        private const uint GL_RENDERBUFFER = 0x8D41;
        private const uint GL_DEPTH24_STENCIL8 = 0x88F0;

        #endregion

        /// <summary>
        /// Creates a new depth-stencil buffer.
        /// Based on swkotor.exe/swkotor2.exe: Depth-stencil buffer initialization
        /// Matching reone Renderbuffer::init(): glGenRenderbuffers -> glBindRenderbuffer -> glRenderbufferStorage
        /// </summary>
        /// <param name="width">Buffer width.</param>
        /// <param name="height">Buffer height.</param>
        public OdysseyDepthStencilBuffer(int width, int height)
        {
            if (width <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
            }
            if (height <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
            }

            _width = width;
            _height = height;

            // Matching reone Renderbuffer::init(): glGenRenderbuffers(1, &_nameGL)
            uint[] renderbufferIds = new uint[1];
            glGenRenderbuffers(1, renderbufferIds);
            _renderbufferId = renderbufferIds[0];

            // Matching reone Renderbuffer::bind(): glBindRenderbuffer(GL_RENDERBUFFER, _nameGL)
            glBindRenderbuffer(GL_RENDERBUFFER, _renderbufferId);

            // Matching reone Renderbuffer::refresh(): glRenderbufferStorage(GL_RENDERBUFFER, format, width, height)
            // Using GL_DEPTH24_STENCIL8 (0x88F0) for 24-bit depth and 8-bit stencil
            // This matches the original game's depth-stencil buffer format
            glRenderbufferStorage(GL_RENDERBUFFER, GL_DEPTH24_STENCIL8, width, height);
        }

        public int Width => _width;
        public int Height => _height;
        public IntPtr NativeHandle => new IntPtr(_renderbufferId);

        /// <summary>
        /// Disposes the depth-stencil buffer.
        /// Matching reone Renderbuffer::deinit(): glDeleteRenderbuffers(1, &_nameGL)
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Matching reone Renderbuffer::deinit(): glDeleteRenderbuffers(1, &_nameGL)
                if (_renderbufferId != 0)
                {
                    uint[] renderbufferIds = new uint[] { _renderbufferId };
                    glDeleteRenderbuffers(1, renderbufferIds);
                    _renderbufferId = 0;
                }
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Static helper class for OpenGL buffer P/Invoke methods.
    /// Required because DllImport cannot be used in generic classes.
    /// </summary>
    internal static class OdysseyBufferHelpers
    {
        [DllImport("opengl32.dll", EntryPoint = "glGenBuffers")]
        internal static extern void glGenBuffers(int n, uint[] buffers);

        [DllImport("opengl32.dll", EntryPoint = "glBindBuffer")]
        internal static extern void glBindBuffer(uint target, uint buffer);

        [DllImport("opengl32.dll", EntryPoint = "glBufferData")]
        internal static extern void glBufferData(uint target, int size, IntPtr data, uint usage);

        [DllImport("opengl32.dll", EntryPoint = "glBufferSubData")]
        internal static extern void glBufferSubData(uint target, int offset, int size, IntPtr data);

        [DllImport("opengl32.dll", EntryPoint = "glGetBufferSubData")]
        internal static extern void glGetBufferSubData(uint target, int offset, int size, IntPtr data);

        [DllImport("opengl32.dll", EntryPoint = "glDeleteBuffers")]
        internal static extern void glDeleteBuffers(int n, uint[] buffers);
    }

    /// <summary>
    /// Odyssey vertex buffer implementation.
    /// Uses OpenGL vertex buffer objects (VBO).
    /// Based on xoreos VertexBuffer and PyKotor Mesh VBO implementation.
    /// </summary>
    /// <remarks>
    /// OpenGL VBO Implementation:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game: Uses DirectX vertex buffers, but OpenGL backend uses VBOs
    /// - xoreos: Uses glGenBuffers, glBufferData, glBindBuffer for VBO management
    /// - PyKotor: Uses glGenBuffers(1, &vbo), glBufferData(GL_ARRAY_BUFFER, ...) for VBO creation
    /// - Matching xoreos vertexbuffer.cpp: initGL(), updateGL(), destroyGL()
    /// - Matching PyKotor mesh.py: glGenBuffers(1), glBufferData(GL_ARRAY_BUFFER, ...)
    /// </remarks>
    public class OdysseyVertexBuffer<T> : IVertexBuffer where T : struct
    {
        private T[] _data;
        private uint _bufferId;
        private int _vertexStride;
        private bool _disposed;
        private bool _vboCreated;

        private const uint GL_ARRAY_BUFFER = 0x8892;
        private const uint GL_STATIC_DRAW = 0x88E4;
        private const uint GL_DYNAMIC_DRAW = 0x88E8;

        /// <summary>
        /// Creates a new vertex buffer with the specified data.
        /// Matching xoreos: VertexBuffer::initGL() - glGenBuffers, glBufferData
        /// Matching PyKotor: glGenBuffers(1, &vbo), glBufferData(GL_ARRAY_BUFFER, ...)
        /// </summary>
        public OdysseyVertexBuffer(T[] data)
        {
            _data = data;
            _vertexStride = Marshal.SizeOf(typeof(T));
            _bufferId = 0;
            _vboCreated = false;
            _disposed = false;

            // Create VBO if data is provided
            if (data != null && data.Length > 0)
            {
                CreateVBO();
            }
        }

        /// <summary>
        /// Creates the OpenGL vertex buffer object.
        /// Matching xoreos: VertexBuffer::initGL() - glGenBuffers(1, &_vbo), glBufferData(...)
        /// Matching PyKotor: glGenBuffers(1, &vbo), glBufferData(GL_ARRAY_BUFFER, len(vertex_data), ...)
        /// </summary>
        private void CreateVBO()
        {
            if (_vboCreated || _bufferId != 0)
            {
                return; // Already created
            }

            // Generate buffer object
            // Matching xoreos: glGenBuffers(1, &_vbo)
            // Matching PyKotor: glGenBuffers(1, &vbo)
            uint[] buffers = new uint[1];
            OdysseyBufferHelpers.glGenBuffers(1, buffers);
            _bufferId = buffers[0];

            if (_bufferId == 0)
            {
                throw new InvalidOperationException("Failed to create OpenGL vertex buffer object.");
            }

            // Bind buffer and upload data
            // Matching xoreos: glBindBuffer(GL_ARRAY_BUFFER, _vbo), glBufferData(...)
            // Matching PyKotor: glBindBuffer(GL_ARRAY_BUFFER, vbo), glBufferData(GL_ARRAY_BUFFER, ...)
            OdysseyBufferHelpers.glBindBuffer(GL_ARRAY_BUFFER, _bufferId);

            int dataSize = _data.Length * _vertexStride;

            // Pin the array and pass pointer directly to glBufferData
            // Matching xoreos: glBufferData(GL_ARRAY_BUFFER, _count * _size, _data, _hint)
            // Matching PyKotor: glBufferData(GL_ARRAY_BUFFER, len(vertex_data), vertex_data_mv, GL_STATIC_DRAW)
            GCHandle handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
            try
            {
                IntPtr dataPtr = handle.AddrOfPinnedObject();
                // Upload data to GPU directly from pinned array
                OdysseyBufferHelpers.glBufferData(GL_ARRAY_BUFFER, dataSize, dataPtr, GL_STATIC_DRAW);
            }
            finally
            {
                handle.Free();
                // Unbind buffer
                OdysseyBufferHelpers.glBindBuffer(GL_ARRAY_BUFFER, 0);
            }

            _vboCreated = true;
        }

        public int VertexCount => _data != null ? _data.Length : 0;
        public int VertexStride => _vertexStride;
        public IntPtr NativeHandle => new IntPtr(_bufferId);

        /// <summary>
        /// Sets vertex data in the buffer.
        /// Matching xoreos: VertexBuffer::updateGL() - glBufferData or glBufferSubData
        /// </summary>
        public void SetData<TData>(TData[] data) where TData : struct
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            // Update local data
            if (typeof(TData) == typeof(T))
            {
                _data = data as T[];
            }
            else
            {
                throw new ArgumentException($"Data type {typeof(TData).Name} does not match buffer type {typeof(T).Name}.", nameof(data));
            }

            // Update VBO if it exists
            if (_vboCreated && _bufferId != 0)
            {
                OdysseyBufferHelpers.glBindBuffer(GL_ARRAY_BUFFER, _bufferId);

                int dataSize = _data.Length * _vertexStride;

                // Pin the array and pass pointer directly to glBufferData
                // Matching xoreos: glBufferData(GL_ARRAY_BUFFER, _count * _size, _data, _hint)
                GCHandle handle = GCHandle.Alloc(_data, GCHandleType.Pinned);
                try
                {
                    IntPtr dataPtr = handle.AddrOfPinnedObject();
                    // Update buffer data directly from pinned array
                    OdysseyBufferHelpers.glBufferData(GL_ARRAY_BUFFER, dataSize, dataPtr, GL_STATIC_DRAW);
                }
                finally
                {
                    handle.Free();
                    // Unbind buffer
                    OdysseyBufferHelpers.glBindBuffer(GL_ARRAY_BUFFER, 0);
                }
            }
            else
            {
                // Create VBO if it doesn't exist
                CreateVBO();
            }
        }

        /// <summary>
        /// Gets vertex data from the buffer.
        /// Matching xoreos: Reads from _data (CPU-side copy)
        /// </summary>
        public void GetData<TData>(TData[] data) where TData : struct
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (typeof(TData) != typeof(T))
            {
                throw new ArgumentException($"Data type {typeof(TData).Name} does not match buffer type {typeof(T).Name}.", nameof(data));
            }

            if (data.Length < VertexCount)
            {
                throw new ArgumentException("Data array is too small.", nameof(data));
            }

            // Copy from local data (CPU-side copy)
            // For GPU-side read, we would use glGetBufferSubData, but xoreos uses CPU-side _data
            if (_data != null)
            {
                Array.Copy(_data, data, Math.Min(_data.Length, data.Length));
            }
        }

        /// <summary>
        /// Disposes the vertex buffer and releases OpenGL resources.
        /// Matching xoreos: VertexBuffer::destroyGL() - glDeleteBuffers(1, &_vbo)
        /// Matching PyKotor: glDeleteBuffers(1, &vbo)
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Delete VBO
                // Matching xoreos: glDeleteBuffers(1, &_vbo)
                // Matching PyKotor: glDeleteBuffers(1, &vbo)
                if (_bufferId != 0)
                {
                    uint[] buffers = new uint[] { _bufferId };
                    OdysseyBufferHelpers.glDeleteBuffers(1, buffers);
                    _bufferId = 0;
                }

                _data = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Odyssey index buffer implementation.
    /// Uses OpenGL index buffer objects (IBO/EBO).
    /// Based on xoreos IndexBuffer implementation.
    /// </summary>
    /// <remarks>
    /// OpenGL IBO Implementation:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game: Uses DirectX index buffers, but OpenGL backend uses IBOs
    /// - xoreos: Uses glGenBuffers, glBufferData, glBindBuffer for IBO management
    /// - Matching xoreos indexbuffer.cpp: initGL(), updateGL(), destroyGL()
    /// - Index type: GL_UNSIGNED_SHORT (16-bit) if IsShort=true, GL_UNSIGNED_INT (32-bit) if IsShort=false
    /// - [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Index buffer system for indexed primitive rendering
    /// </remarks>
    public class OdysseyIndexBuffer : IIndexBuffer
    {
        private int[] _indices;
        private readonly bool _isShort;
        private uint _bufferId;
        private bool _disposed;
        private bool _iboCreated;

        private const uint GL_ELEMENT_ARRAY_BUFFER = 0x8893;
        private const uint GL_STATIC_DRAW = 0x88E4;
        private const uint GL_DYNAMIC_DRAW = 0x88E8;

        /// <summary>
        /// Creates a new index buffer with the specified data.
        /// Matching xoreos: IndexBuffer::initGL() - glGenBuffers, glBufferData
        /// </summary>
        public OdysseyIndexBuffer(int[] indices, bool isShort)
        {
            _indices = indices;
            _isShort = isShort;
            _bufferId = 0;
            _iboCreated = false;
            _disposed = false;

            // Create IBO if data is provided
            if (indices != null && indices.Length > 0)
            {
                CreateIBO();
            }
        }

        /// <summary>
        /// Creates the OpenGL index buffer object.
        /// Matching xoreos: IndexBuffer::initGL() - glGenBuffers(1, &_ibo), glBufferData(GL_ELEMENT_ARRAY_BUFFER, ...)
        /// Based on xoreos indexbuffer.cpp: initGL() @ lines 94-106
        /// </summary>
        private void CreateIBO()
        {
            if (_iboCreated || _bufferId != 0)
            {
                return; // Already created
            }

            // Generate buffer object
            // Matching xoreos: glGenBuffers(1, &_ibo)
            uint[] buffers = new uint[1];
            OdysseyBufferHelpers.glGenBuffers(1, buffers);
            _bufferId = buffers[0];

            if (_bufferId == 0)
            {
                throw new InvalidOperationException("Failed to create OpenGL index buffer object.");
            }

            // Bind buffer and upload data
            // Matching xoreos: glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ibo), glBufferData(...)
            OdysseyBufferHelpers.glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _bufferId);

            // Calculate data size based on index type (16-bit or 32-bit)
            int indexSize = _isShort ? sizeof(ushort) : sizeof(uint);
            int dataSize = _indices.Length * indexSize;

            // Convert indices to appropriate format (ushort[] or uint[])
            // Pin the array and pass pointer directly to glBufferData
            // Matching xoreos: glBufferData(GL_ELEMENT_ARRAY_BUFFER, _count * _size, _data, _hint)
            if (_isShort)
            {
                // Convert int[] to ushort[] for 16-bit indices
                ushort[] shortIndices = new ushort[_indices.Length];
                for (int i = 0; i < _indices.Length; i++)
                {
                    shortIndices[i] = (ushort)_indices[i];
                }

                GCHandle handle = GCHandle.Alloc(shortIndices, GCHandleType.Pinned);
                try
                {
                    IntPtr dataPtr = handle.AddrOfPinnedObject();
                    // Upload data to GPU directly from pinned array
                    OdysseyBufferHelpers.glBufferData(GL_ELEMENT_ARRAY_BUFFER, dataSize, dataPtr, GL_STATIC_DRAW);
                }
                finally
                {
                    handle.Free();
                }
            }
            else
            {
                // Convert int[] to uint[] for 32-bit indices
                uint[] uintIndices = new uint[_indices.Length];
                for (int i = 0; i < _indices.Length; i++)
                {
                    uintIndices[i] = (uint)_indices[i];
                }

                GCHandle handle = GCHandle.Alloc(uintIndices, GCHandleType.Pinned);
                try
                {
                    IntPtr dataPtr = handle.AddrOfPinnedObject();
                    // Upload data to GPU directly from pinned array
                    OdysseyBufferHelpers.glBufferData(GL_ELEMENT_ARRAY_BUFFER, dataSize, dataPtr, GL_STATIC_DRAW);
                }
                finally
                {
                    handle.Free();
                }
            }

            // Unbind buffer
            // Matching xoreos: glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0)
            OdysseyBufferHelpers.glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);

            _iboCreated = true;
        }

        public int IndexCount => _indices != null ? _indices.Length : 0;
        public bool IsShort => _isShort;
        public IntPtr NativeHandle => new IntPtr(_bufferId);

        /// <summary>
        /// Sets index data in the buffer.
        /// Matching xoreos: IndexBuffer::updateGL() - glBufferData or glBufferSubData
        /// Based on xoreos indexbuffer.cpp: updateGL() @ lines 108-114
        /// </summary>
        public void SetData(int[] indices)
        {
            if (indices == null)
            {
                throw new ArgumentNullException(nameof(indices));
            }

            _indices = indices;

            // Update IBO if it was already created
            if (_iboCreated && _bufferId != 0)
            {
                UpdateIBO();
            }
            else if (indices.Length > 0)
            {
                // Create IBO if it doesn't exist yet
                CreateIBO();
            }
        }

        /// <summary>
        /// Updates the OpenGL index buffer object with new data.
        /// Matching xoreos: IndexBuffer::updateGL() - glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ibo), glBufferData(...)
        /// Based on xoreos indexbuffer.cpp: updateGL() @ lines 108-114
        /// </summary>
        private void UpdateIBO()
        {
            if (!_iboCreated || _bufferId == 0 || _indices == null || _indices.Length == 0)
            {
                return;
            }

            // Bind buffer
            // Matching xoreos: glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _ibo)
            OdysseyBufferHelpers.glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, _bufferId);

            // Calculate data size based on index type (16-bit or 32-bit)
            int indexSize = _isShort ? sizeof(ushort) : sizeof(uint);
            int dataSize = _indices.Length * indexSize;

            // Convert indices to appropriate format (ushort[] or uint[])
            // Pin the array and pass pointer directly to glBufferData
            // Matching xoreos: glBufferData(GL_ELEMENT_ARRAY_BUFFER, _count * _size, _data, _hint)
            if (_isShort)
            {
                // Convert int[] to ushort[] for 16-bit indices
                ushort[] shortIndices = new ushort[_indices.Length];
                for (int i = 0; i < _indices.Length; i++)
                {
                    shortIndices[i] = (ushort)_indices[i];
                }

                GCHandle handle = GCHandle.Alloc(shortIndices, GCHandleType.Pinned);
                try
                {
                    IntPtr dataPtr = handle.AddrOfPinnedObject();
                    // Upload data to GPU directly from pinned array
                    // Using glBufferData for full buffer update (replaces entire buffer)
                    OdysseyBufferHelpers.glBufferData(GL_ELEMENT_ARRAY_BUFFER, dataSize, dataPtr, GL_STATIC_DRAW);
                }
                finally
                {
                    handle.Free();
                }
            }
            else
            {
                // Convert int[] to uint[] for 32-bit indices
                uint[] uintIndices = new uint[_indices.Length];
                for (int i = 0; i < _indices.Length; i++)
                {
                    uintIndices[i] = (uint)_indices[i];
                }

                GCHandle handle = GCHandle.Alloc(uintIndices, GCHandleType.Pinned);
                try
                {
                    IntPtr dataPtr = handle.AddrOfPinnedObject();
                    // Upload data to GPU directly from pinned array
                    // Using glBufferData for full buffer update (replaces entire buffer)
                    OdysseyBufferHelpers.glBufferData(GL_ELEMENT_ARRAY_BUFFER, dataSize, dataPtr, GL_STATIC_DRAW);
                }
                finally
                {
                    handle.Free();
                }
            }

            // Unbind buffer
            // Matching xoreos: glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0)
            OdysseyBufferHelpers.glBindBuffer(GL_ELEMENT_ARRAY_BUFFER, 0);
        }

        public void GetData(int[] indices)
        {
            if (_indices != null && indices != null)
            {
                Array.Copy(_indices, indices, Math.Min(_indices.Length, indices.Length));
            }
        }

        /// <summary>
        /// Disposes the index buffer and releases OpenGL resources.
        /// Matching xoreos: IndexBuffer::destroyGL() - glDeleteBuffers(1, &_ibo)
        /// Based on xoreos indexbuffer.cpp: destroyGL() @ lines 116-121
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Delete IBO if it was created
                // Matching xoreos: glDeleteBuffers(1, &_ibo)
                if (_iboCreated && _bufferId != 0)
                {
                    uint[] buffers = new uint[1] { _bufferId };
                    OdysseyBufferHelpers.glDeleteBuffers(1, buffers);
                    _bufferId = 0;
                    _iboCreated = false;
                }

                _indices = null;
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Odyssey sprite batch implementation.
    /// Uses immediate mode OpenGL for 2D rendering.
    /// Based on xoreos: guiquad.cpp render() @ lines 274-344
    /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): 2D sprite rendering for GUI elements
    /// </summary>
    public class OdysseySpriteBatch : ISpriteBatch
    {
        private readonly OdysseyGraphicsDevice _device;
        private bool _inBatch;
        private BlendState _currentBlendState;
        private int _savedMatrixMode;
        private float[] _savedProjectionMatrix = new float[16];
        private float[] _savedModelviewMatrix = new float[16];

        #region OpenGL P/Invoke for Sprite Rendering

        [DllImport("opengl32.dll", EntryPoint = "glPushAttrib")]
        private static extern void glPushAttrib(uint mask);

        [DllImport("opengl32.dll", EntryPoint = "glPopAttrib")]
        private static extern void glPopAttrib();

        [DllImport("opengl32.dll", EntryPoint = "glPushMatrix")]
        private static extern void glPushMatrix();

        [DllImport("opengl32.dll", EntryPoint = "glPopMatrix")]
        private static extern void glPopMatrix();

        [DllImport("opengl32.dll", EntryPoint = "glMatrixMode")]
        private static extern void glMatrixMode(uint mode);

        [DllImport("opengl32.dll", EntryPoint = "glLoadIdentity")]
        private static extern void glLoadIdentity();

        [DllImport("opengl32.dll", EntryPoint = "glOrtho")]
        private static extern void glOrtho(double left, double right, double bottom, double top, double zNear, double zFar);

        [DllImport("opengl32.dll", EntryPoint = "glTranslatef")]
        private static extern void glTranslatef(float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glRotatef")]
        private static extern void glRotatef(float angle, float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glScalef")]
        private static extern void glScalef(float x, float y, float z);

        [DllImport("opengl32.dll", EntryPoint = "glEnable")]
        private static extern void glEnable(uint cap);

        [DllImport("opengl32.dll", EntryPoint = "glDisable")]
        private static extern void glDisable(uint cap);

        [DllImport("opengl32.dll", EntryPoint = "glBlendFunc")]
        private static extern void glBlendFunc(uint sfactor, uint dfactor);

        [DllImport("opengl32.dll", EntryPoint = "glColor4f")]
        private static extern void glColor4f(float red, float green, float blue, float alpha);

        [DllImport("opengl32.dll", EntryPoint = "glBindTexture")]
        private static extern void glBindTexture(uint target, uint texture);

        [DllImport("opengl32.dll", EntryPoint = "glBegin")]
        private static extern void glBegin(uint mode);

        [DllImport("opengl32.dll", EntryPoint = "glEnd")]
        private static extern void glEnd();

        [DllImport("opengl32.dll", EntryPoint = "glTexCoord2f")]
        private static extern void glTexCoord2f(float s, float t);

        [DllImport("opengl32.dll", EntryPoint = "glVertex2f")]
        private static extern void glVertex2f(float x, float y);

        [DllImport("opengl32.dll", EntryPoint = "glGetIntegerv")]
        private static extern void glGetIntegerv(uint pname, int[] data);

        [DllImport("opengl32.dll", EntryPoint = "glGetFloatv")]
        private static extern void glGetFloatv(uint pname, float[] data);

        [DllImport("opengl32.dll", EntryPoint = "glLoadMatrixf")]
        private static extern void glLoadMatrixf([MarshalAs(UnmanagedType.LPArray)] float[] m);

        // OpenGL constants
        private const uint GL_PROJECTION = 0x1701;
        private const uint GL_MODELVIEW = 0x1700;
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_BLEND = 0x0BE2;
        private const uint GL_SRC_ALPHA = 0x0302;
        private const uint GL_ONE_MINUS_SRC_ALPHA = 0x0303;
        private const uint GL_ONE = 0x0001;
        private const uint GL_ZERO = 0x0000;
        private const uint GL_QUADS = 0x0007;
        private const uint GL_COLOR_BUFFER_BIT = 0x00004000;
        private const uint GL_TEXTURE_BIT = 0x00040000;
        private const uint GL_ENABLE_BIT = 0x00002000;
        private const uint GL_TRANSFORM_BIT = 0x00001000;
        private const uint GL_VIEWPORT = 0x0BA2;
        private const uint GL_PROJECTION_MATRIX = 0x0BA3;
        private const uint GL_MODELVIEW_MATRIX = 0x0BA6;

        #endregion

        public OdysseySpriteBatch(OdysseyGraphicsDevice device)
        {
            _device = device;
        }

        /// <summary>
        /// Begins sprite batch rendering.
        /// Sets up 2D orthographic projection and saves OpenGL state.
        /// Based on xoreos: graphics.cpp setOrthogonal() @ lines 561-575
        /// </summary>
        public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null)
        {
            _inBatch = true;
            _currentBlendState = blendState ?? BlendState.Default;

            // Save OpenGL state
            // Based on xoreos: guiquad.cpp render() saves state with glPushAttrib
            glPushAttrib(GL_COLOR_BUFFER_BIT | GL_TEXTURE_BIT | GL_ENABLE_BIT | GL_TRANSFORM_BIT);
            glPushMatrix();

            // Save current matrix mode and matrices
            int[] matrixMode = new int[1];
            glGetIntegerv(0x0BA0, matrixMode); // GL_MATRIX_MODE
            _savedMatrixMode = matrixMode[0];

            glGetFloatv(GL_PROJECTION_MATRIX, _savedProjectionMatrix);
            glGetFloatv(GL_MODELVIEW_MATRIX, _savedModelviewMatrix);

            // Get viewport dimensions
            int[] viewport = new int[4];
            glGetIntegerv(GL_VIEWPORT, viewport);
            int viewportWidth = viewport[2];
            int viewportHeight = viewport[3];

            // Setup 2D orthographic projection
            // Based on xoreos: graphics.cpp ortho() @ lines 577-609
            // Orthographic projection: left=0, right=width, bottom=0, top=height, zNear=-1, zFar=1
            glMatrixMode(GL_PROJECTION);
            glLoadIdentity();
            glOrtho(0.0, viewportWidth, viewportHeight, 0.0, -1.0, 1.0); // Note: Y is flipped for screen coordinates

            glMatrixMode(GL_MODELVIEW);
            glLoadIdentity();

            // Setup blending
            if (_currentBlendState.AlphaBlend)
            {
                glEnable(GL_BLEND);
                if (_currentBlendState.Additive)
                {
                    glBlendFunc(GL_ONE, GL_ONE);
                }
                else
                {
                    glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);
                }
            }
            else
            {
                glDisable(GL_BLEND);
            }

            // Enable texturing
            glEnable(GL_TEXTURE_2D);
        }

        /// <summary>
        /// Ends sprite batch rendering.
        /// Restores previous OpenGL state.
        /// </summary>
        public void End()
        {
            if (!_inBatch) return;

            // Restore color to white
            glColor4f(1.0f, 1.0f, 1.0f, 1.0f);

            // Restore matrices
            glMatrixMode(GL_PROJECTION);
            glLoadMatrixf(_savedProjectionMatrix);

            glMatrixMode(GL_MODELVIEW);
            glLoadMatrixf(_savedModelviewMatrix);

            glMatrixMode((uint)_savedMatrixMode);

            // Restore OpenGL state
            glPopMatrix();
            glPopAttrib();

            _inBatch = false;
        }

        /// <summary>
        /// Draws a texture at the specified position.
        /// Based on xoreos: guiquad.cpp render() @ lines 322-331
        /// </summary>
        public void Draw(ITexture2D texture, Andastra.Runtime.Graphics.Vector2 position, Color color)
        {
            if (!_inBatch || texture == null) return;

            Rectangle destinationRect = new Rectangle
            {
                X = (int)position.X,
                Y = (int)position.Y,
                Width = texture.Width,
                Height = texture.Height
            };

            Draw(texture, destinationRect, null, color, 0.0f, System.Numerics.Vector2.Zero, SpriteEffects.None, 0.0f);
        }

        /// <summary>
        /// Draws a texture with destination rectangle.
        /// </summary>
        public void Draw(ITexture2D texture, Rectangle destinationRectangle, Color color)
        {
            if (!_inBatch || texture == null) return;

            Draw(texture, destinationRectangle, null, color, 0.0f, Vector2.Zero, SpriteEffects.None, 0.0f);
        }

        /// <summary>
        /// Draws a texture with source rectangle.
        /// </summary>
        public void Draw(ITexture2D texture, Andastra.Runtime.Graphics.Vector2 position, Rectangle? sourceRectangle, Color color)
        {
            if (!_inBatch || texture == null) return;

            Rectangle destinationRect = new Rectangle
            {
                X = (int)position.X,
                Y = (int)position.Y,
                Width = sourceRectangle.HasValue ? sourceRectangle.Value.Width : texture.Width,
                Height = sourceRectangle.HasValue ? sourceRectangle.Value.Height : texture.Height
            };

            Draw(texture, destinationRect, sourceRectangle, color, 0.0f, Vector2.Zero, SpriteEffects.None, 0.0f);
        }

        /// <summary>
        /// Draws a texture with full parameters (rotation, origin, effects, layer depth).
        /// Based on xoreos: guiquad.cpp render() @ lines 274-344
        /// </summary>
        public void Draw(ITexture2D texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Andastra.Runtime.Graphics.Vector2 origin, SpriteEffects effects, float layerDepth)
        {
            if (!_inBatch || texture == null) return;

            // Bind texture
            if (texture is OdysseyTexture2D odysseyTexture)
            {
                glBindTexture(GL_TEXTURE_2D, (uint)odysseyTexture.NativeHandle.ToInt64());
            }

            // Set color with alpha
            float alpha = color.A / 255.0f;
            glColor4f(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f, alpha);

            // Calculate texture coordinates
            float texLeft, texTop, texRight, texBottom;
            if (sourceRectangle.HasValue)
            {
                Rectangle src = sourceRectangle.Value;
                texLeft = (float)src.X / texture.Width;
                texTop = (float)src.Y / texture.Height;
                texRight = (float)(src.X + src.Width) / texture.Width;
                texBottom = (float)(src.Y + src.Height) / texture.Height;
            }
            else
            {
                texLeft = 0.0f;
                texTop = 0.0f;
                texRight = 1.0f;
                texBottom = 1.0f;
            }

            // Apply sprite effects (flip texture coordinates)
            if ((effects & SpriteEffects.FlipHorizontally) != 0)
            {
                float temp = texLeft;
                texLeft = texRight;
                texRight = temp;
            }
            if ((effects & SpriteEffects.FlipVertically) != 0)
            {
                float temp = texTop;
                texTop = texBottom;
                texBottom = temp;
            }

            // Calculate vertex positions
            float x1 = destinationRectangle.X;
            float y1 = destinationRectangle.Y;
            float x2 = destinationRectangle.X + destinationRectangle.Width;
            float y2 = destinationRectangle.Y + destinationRectangle.Height;

            // Apply rotation if needed
            // Based on xoreos: guiquad.cpp render() @ lines 314-318
            if (rotation != 0.0f)
            {
                float centerX = x1 + destinationRectangle.Width * (origin.X / (float)destinationRectangle.Width);
                float centerY = y1 + destinationRectangle.Height * (origin.Y / (float)destinationRectangle.Height);

                glPushMatrix();
                glTranslatef(centerX, centerY, 0.0f);
                glRotatef(rotation * (180.0f / (float)System.Math.PI), 0.0f, 0.0f, 1.0f);
                glTranslatef(-centerX, -centerY, 0.0f);
            }

            // Draw quad using immediate mode
            // Based on xoreos: guiquad.cpp render() @ lines 322-331
            glBegin(GL_QUADS);

            // Top-left
            glTexCoord2f(texLeft, texTop);
            glVertex2f(x1, y1);

            // Top-right
            glTexCoord2f(texRight, texTop);
            glVertex2f(x2, y1);

            // Bottom-right
            glTexCoord2f(texRight, texBottom);
            glVertex2f(x2, y2);

            // Bottom-left
            glTexCoord2f(texLeft, texBottom);
            glVertex2f(x1, y2);

            glEnd();

            // Restore matrix if rotation was applied
            if (rotation != 0.0f)
            {
                glPopMatrix();
            }
        }

        /// <summary>
        /// Draws text using a sprite font.
        /// Based on xoreos: texturefont.cpp render() @ lines 52-94
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): Font rendering system @ 0x007b6380 (dialogfont16x16)
        /// </summary>
        public void DrawString(IFont font, string text, Andastra.Runtime.Graphics.Vector2 position, Color color)
        {
            if (!_inBatch || font == null || string.IsNullOrEmpty(text)) return;

            // Check if font is a bitmap font (BaseBitmapFont)
            // BaseBitmapFont provides GetCharacter() method and Texture property
            if (!(font is BaseBitmapFont bitmapFont))
            {
                // Font is not a bitmap font - cannot render
                return;
            }

            // Get font texture
            ITexture2D fontTexture = bitmapFont.Texture;
            if (fontTexture == null)
            {
                return;
            }

            // Current drawing position (starts at input position)
            float currentX = position.X;
            float currentY = position.Y;
            float lineHeight = bitmapFont.FontHeight + bitmapFont.SpacingB;

            // Iterate through each character in the text
            foreach (char c in text)
            {
                // Handle line breaks
                if (c == '\n')
                {
                    currentX = position.X;
                    currentY += lineHeight;
                    continue;
                }

                // Get character code
                int charCode = (int)c;

                // Get character glyph information
                BaseBitmapFont.CharacterGlyph? glyph = bitmapFont.GetCharacter(charCode);
                if (!glyph.HasValue)
                {
                    // Unknown character - skip or use default width
                    currentX += bitmapFont.FontWidth + bitmapFont.SpacingR;
                    continue;
                }

                var glyphValue = glyph.Value;

                // Create source rectangle from glyph coordinates
                Rectangle sourceRect = new Rectangle
                {
                    X = glyphValue.SourceX,
                    Y = glyphValue.SourceY,
                    Width = glyphValue.SourceWidth,
                    Height = glyphValue.SourceHeight
                };

                // Create destination rectangle
                Rectangle destRect = new Rectangle
                {
                    X = (int)currentX,
                    Y = (int)currentY,
                    Width = glyphValue.SourceWidth,
                    Height = glyphValue.SourceHeight
                };

                // Draw character as sprite using font texture and glyph coordinates
                // Based on xoreos: texturefont.cpp render() - draws each character as textured quad
                Draw(fontTexture, destRect, sourceRect, color, 0.0f, Vector2.Zero, SpriteEffects.None, 0.0f);

                // Advance position for next character
                // Based on xoreos: texturefont.cpp - advances X position by character width + spacing
                currentX += glyphValue.Width + bitmapFont.SpacingR;
            }
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Odyssey basic effect implementation.
    /// Uses OpenGL fixed-function pipeline or ARB shaders.
    /// </summary>
    /// <remarks>
    /// Odyssey Basic Effect:
    /// - Based on reverse engineering of swkotor.exe and swkotor2.exe
    /// - Original game graphics: OpenGL fixed-function pipeline (OPENGL32.dll @ 0x00809ce2)
    /// - Matrix application: Original engine uses glMatrixMode(GL_PROJECTION) and glMatrixMode(GL_MODELVIEW)
    /// - Based on xoreos implementation: graphics.cpp renderWorld() @ lines 1059-1081
    /// - Opacity/Alpha: Original engine uses glColor4f for alpha blending (swkotor2.exe: FadeTime @ 0x007c60ec)
    /// - This implementation: Applies matrices and opacity via OpenGL fixed-function pipeline
    /// </remarks>
    public class OdysseyBasicEffect : IBasicEffect
    {
        private readonly OdysseyGraphicsDevice _device;
        private Matrix4x4 _world = Matrix4x4.Identity;
        private Matrix4x4 _view = Matrix4x4.Identity;
        private Matrix4x4 _projection = Matrix4x4.Identity;
        private bool _textureEnabled;
        private bool _vertexColorEnabled;
        private bool _lightingEnabled;
        private Vector3 _ambientLightColor = new Vector3(0.2f, 0.2f, 0.2f);
        private Vector3 _diffuseColor = Vector3.One;
        private Vector3 _emissiveColor = Vector3.Zero;
        private Vector3 _specularColor = Vector3.One;
        private float _specularPower = 16.0f;
        private float _alpha = 1.0f;
        private ITexture2D _texture;
        private bool _fogEnabled;
        private Vector3 _fogColor = Vector3.One;
        private float _fogStart = 0.0f;
        private float _fogEnd = 1.0f;

        #region OpenGL P/Invoke for Matrix Operations

        [DllImport("opengl32.dll", EntryPoint = "glMatrixMode")]
        private static extern void glMatrixMode(uint mode);

        [DllImport("opengl32.dll", EntryPoint = "glLoadIdentity")]
        private static extern void glLoadIdentity();

        [DllImport("opengl32.dll", EntryPoint = "glLoadMatrixf")]
        private static extern void glLoadMatrixf([MarshalAs(UnmanagedType.LPArray)] float[] m);

        [DllImport("opengl32.dll", EntryPoint = "glMultMatrixf")]
        private static extern void glMultMatrixf([MarshalAs(UnmanagedType.LPArray)] float[] m);

        [DllImport("opengl32.dll", EntryPoint = "glPushMatrix")]
        private static extern void glPushMatrix();

        [DllImport("opengl32.dll", EntryPoint = "glPopMatrix")]
        private static extern void glPopMatrix();

        [DllImport("opengl32.dll", EntryPoint = "glEnable")]
        private static extern void glEnable(uint cap);

        [DllImport("opengl32.dll", EntryPoint = "glDisable")]
        private static extern void glDisable(uint cap);

        [DllImport("opengl32.dll", EntryPoint = "glColor4f")]
        private static extern void glColor4f(float red, float green, float blue, float alpha);

        [DllImport("opengl32.dll", EntryPoint = "glBlendFunc")]
        private static extern void glBlendFunc(uint sfactor, uint dfactor);

        [DllImport("opengl32.dll", EntryPoint = "glBindTexture")]
        private static extern void glBindTexture(uint target, uint texture);

        // OpenGL constants
        private const uint GL_PROJECTION = 0x1701;
        private const uint GL_MODELVIEW = 0x1700;
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_BLEND = 0x0BE2;
        private const uint GL_SRC_ALPHA = 0x0302;
        private const uint GL_ONE_MINUS_SRC_ALPHA = 0x0303;
        private const uint GL_ONE = 0x0001;
        private const uint GL_ZERO = 0x0000;

        #endregion

        public OdysseyBasicEffect(OdysseyGraphicsDevice device)
        {
            _device = device;
        }

        public IEffectTechnique CurrentTechnique => new OdysseyEffectTechnique("Default");
        public IEffectTechnique[] Techniques => new IEffectTechnique[] { CurrentTechnique };

        public Matrix4x4 World { get { return _world; } set { _world = value; } }
        public Matrix4x4 View { get { return _view; } set { _view = value; } }
        public Matrix4x4 Projection { get { return _projection; } set { _projection = value; } }
        public bool TextureEnabled { get { return _textureEnabled; } set { _textureEnabled = value; } }
        public bool VertexColorEnabled { get { return _vertexColorEnabled; } set { _vertexColorEnabled = value; } }
        public bool LightingEnabled { get { return _lightingEnabled; } set { _lightingEnabled = value; } }
        public Vector3 AmbientLightColor { get { return _ambientLightColor; } set { _ambientLightColor = value; } }
        public Vector3 DiffuseColor { get { return _diffuseColor; } set { _diffuseColor = value; } }
        public Vector3 EmissiveColor { get { return _emissiveColor; } set { _emissiveColor = value; } }
        public Vector3 SpecularColor { get { return _specularColor; } set { _specularColor = value; } }
        public float SpecularPower { get { return _specularPower; } set { _specularPower = value; } }
        public float Alpha { get { return _alpha; } set { _alpha = value; } }
        public ITexture2D Texture { get { return _texture; } set { _texture = value; } }
        public bool FogEnabled { get { return _fogEnabled; } set { _fogEnabled = value; } }
        public Vector3 FogColor { get { return _fogColor; } set { _fogColor = value; } }
        public float FogStart { get { return _fogStart; } set { _fogStart = value; } }
        public float FogEnd { get { return _fogEnd; } set { _fogEnd = value; } }

        /// <summary>
        /// Applies the effect to OpenGL state.
        /// Sets projection, view, and world matrices, and applies opacity via color/blending.
        /// [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): glDrawElements with proper matrix setup
        /// Based on xoreos: graphics.cpp renderWorld() @ lines 1059-1081
        ///
        /// Note: In OpenGL fixed-function pipeline:
        /// - Projection matrix is typically set once per frame
        /// - View matrix is typically set once per frame
        /// - World matrix is set per-object (caller should use glPushMatrix/glPopMatrix)
        /// This method applies all three matrices, so caller should manage matrix stack.
        /// </summary>
        public void Apply()
        {
            // Apply projection matrix
            // Based on xoreos: glMatrixMode(GL_PROJECTION); glMultMatrixf(_perspective);
            glMatrixMode(GL_PROJECTION);
            glLoadIdentity();
            float[] projectionArray = MatrixToFloatArray(_projection);
            glMultMatrixf(projectionArray);

            // Apply view and world matrices to MODELVIEW
            // Based on xoreos: glMatrixMode(GL_MODELVIEW); glLoadIdentity(); then apply view transform
            glMatrixMode(GL_MODELVIEW);
            glLoadIdentity();

            // Apply view matrix (camera transform)
            float[] viewArray = MatrixToFloatArray(_view);
            glMultMatrixf(viewArray);

            // Apply world matrix (object transform)
            // Note: Caller should use glPushMatrix() before Apply() and glPopMatrix() after drawing
            float[] worldArray = MatrixToFloatArray(_world);
            glMultMatrixf(worldArray);

            // Apply opacity via color and blending
            // [TODO: Function name] @ (K1: TODO: Find this address, TSL: TODO: Find this address address): FadeTime @ 0x007c60ec (fade duration), alpha blending for entity rendering
            // Opacity is updated by AppearAnimationFadeSystem for appear animations
            // Opacity is updated by ActionDestroyObject for destroy animations
            if (_alpha < 1.0f)
            {
                // Enable blending for transparency
                // Based on xoreos: guiquad.cpp render() @ lines 274-297 (alpha blending)
                glEnable(GL_BLEND);
                glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

                // Set color with alpha for vertex color modulation
                // glColor4f applies a color multiplier to all vertices
                glColor4f(_diffuseColor.X, _diffuseColor.Y, _diffuseColor.Z, _alpha);
            }
            else
            {
                // Full opacity - disable blending for performance
                glDisable(GL_BLEND);
                glColor4f(_diffuseColor.X, _diffuseColor.Y, _diffuseColor.Z, 1.0f);
            }

            // Apply texture if enabled
            if (_textureEnabled && _texture != null)
            {
                if (_texture is OdysseyTexture2D odysseyTexture)
                {
                    glBindTexture(GL_TEXTURE_2D, (uint)odysseyTexture.NativeHandle.ToInt64());
                }
            }
            else
            {
                glBindTexture(GL_TEXTURE_2D, 0);
            }
        }

        /// <summary>
        /// Converts System.Numerics.Matrix4x4 to OpenGL column-major float array.
        /// OpenGL matrices are column-major, System.Numerics uses row-major.
        /// Based on xoreos: glm::value_ptr() usage for glMultMatrixf
        /// </summary>
        private float[] MatrixToFloatArray(Matrix4x4 matrix)
        {
            // System.Numerics.Matrix4x4 is row-major, OpenGL expects column-major
            // We need to transpose the matrix for OpenGL
            float[] array = new float[16];

            // Transpose: row i, column j becomes column i, row j
            array[0] = matrix.M11; array[4] = matrix.M12; array[8] = matrix.M13; array[12] = matrix.M14;
            array[1] = matrix.M21; array[5] = matrix.M22; array[9] = matrix.M23; array[13] = matrix.M24;
            array[2] = matrix.M31; array[6] = matrix.M32; array[10] = matrix.M33; array[14] = matrix.M34;
            array[3] = matrix.M41; array[7] = matrix.M42; array[11] = matrix.M43; array[15] = matrix.M44;

            return array;
        }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Odyssey effect technique implementation.
    /// </summary>
    public class OdysseyEffectTechnique : IEffectTechnique
    {
        private readonly string _name;

        public OdysseyEffectTechnique(string name)
        {
            _name = name;
        }

        public string Name => _name;
        public IEffectPass[] Passes => new IEffectPass[] { new OdysseyEffectPass("Pass0") };
    }

    /// <summary>
    /// Odyssey effect pass implementation.
    /// </summary>
    public class OdysseyEffectPass : IEffectPass
    {
        private readonly string _name;

        public OdysseyEffectPass(string name)
        {
            _name = name;
        }

        public string Name => _name;

        public void Apply()
        {
            // TODO: STUB - Apply effect pass
        }
    }

    /// <summary>
    /// Odyssey rasterizer state implementation.
    /// </summary>
    public class OdysseyRasterizerState : IRasterizerState
    {
        public CullMode CullMode { get; set; } = CullMode.CullCounterClockwiseFace;
        public FillMode FillMode { get; set; } = FillMode.Solid;
        public bool DepthBiasEnabled { get; set; }
        public float DepthBias { get; set; }
        public float SlopeScaleDepthBias { get; set; }
        public bool ScissorTestEnabled { get; set; }
        public bool MultiSampleAntiAlias { get; set; }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Odyssey depth-stencil state implementation.
    /// </summary>
    public class OdysseyDepthStencilState : IDepthStencilState
    {
        public bool DepthBufferEnable { get; set; } = true;
        public bool DepthBufferWriteEnable { get; set; } = true;
        public CompareFunction DepthBufferFunction { get; set; } = CompareFunction.LessEqual;
        public bool StencilEnable { get; set; }
        public bool TwoSidedStencilMode { get; set; }
        public StencilOperation StencilFail { get; set; } = StencilOperation.Keep;
        public StencilOperation StencilDepthFail { get; set; } = StencilOperation.Keep;
        public StencilOperation StencilPass { get; set; } = StencilOperation.Keep;
        public CompareFunction StencilFunction { get; set; } = CompareFunction.Always;
        public int ReferenceStencil { get; set; }
        public int StencilMask { get; set; } = int.MaxValue;
        public int StencilWriteMask { get; set; } = int.MaxValue;

        // Legacy properties for backwards compatibility
        public bool DepthEnabled { get { return DepthBufferEnable; } set { DepthBufferEnable = value; } }
        public bool StencilEnabled { get { return StencilEnable; } set { StencilEnable = value; } }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Odyssey blend state implementation.
    /// </summary>
    public class OdysseyBlendState : IBlendState
    {
        public BlendFunction AlphaBlendFunction { get; set; } = BlendFunction.Add;
        public Blend AlphaDestinationBlend { get; set; } = Blend.Zero;
        public Blend AlphaSourceBlend { get; set; } = Blend.One;
        public BlendFunction ColorBlendFunction { get; set; } = BlendFunction.Add;
        public Blend ColorDestinationBlend { get; set; } = Blend.Zero;
        public Blend ColorSourceBlend { get; set; } = Blend.One;
        public ColorWriteChannels ColorWriteChannels { get; set; } = ColorWriteChannels.All;
        public ColorWriteChannels ColorWriteChannels1 { get; set; } = ColorWriteChannels.All;
        public ColorWriteChannels ColorWriteChannels2 { get; set; } = ColorWriteChannels.All;
        public ColorWriteChannels ColorWriteChannels3 { get; set; } = ColorWriteChannels.All;
        public bool BlendEnable { get; set; }
        public Color BlendFactor { get; set; } = Color.White;
        public int MultiSampleMask { get; set; } = -1;

        // Legacy property for backwards compatibility
        public bool BlendEnabled { get { return BlendEnable; } set { BlendEnable = value; } }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }

    /// <summary>
    /// Odyssey sampler state implementation.
    /// </summary>
    public class OdysseySamplerState : ISamplerState
    {
        public TextureAddressMode AddressU { get; set; } = TextureAddressMode.Wrap;
        public TextureAddressMode AddressV { get; set; } = TextureAddressMode.Wrap;
        public TextureAddressMode AddressW { get; set; } = TextureAddressMode.Wrap;
        public TextureFilter Filter { get; set; } = TextureFilter.Linear;
        public int MaxAnisotropy { get; set; } = 1;
        public int MaxMipLevel { get; set; }
        public float MipMapLevelOfDetailBias { get; set; }

        public void Dispose()
        {
            // Nothing to dispose
        }
    }
}
